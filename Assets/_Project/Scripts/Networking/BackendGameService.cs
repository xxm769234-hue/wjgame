using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class BackendGameService : MonoBehaviour
{
    private const string TokenKey = "weijun.backend.token";
    private const string PlayerIdKey = "weijun.backend.playerId";

    public static BackendGameService Instance { get; private set; }

    [Header("Connection")]
    [SerializeField] private string baseUrl = "http://localhost:8080/api";
    [SerializeField] private bool connectOnStart = true;
    [SerializeField] private bool autoLoginWhenMissingToken = true;
    [SerializeField] private string defaultUsername = "player1";
    [SerializeField] private string defaultPassword = "123456";
    [SerializeField] private bool registerDefaultUserIfLoginFails;

    [Header("Polling")]
    [SerializeField] private bool pollProductions = true;
    [SerializeField, Min(1f)] private float productionPollInterval = 30f;

    public bool IsAuthenticated => !string.IsNullOrEmpty(Token) && PlayerId > 0;
    public bool IsConnecting { get; private set; }
    public string Token { get; private set; }
    public int PlayerId { get; private set; }
    public UserResponse CurrentUser { get; private set; }
    public InventoryItemResponse[] CurrentInventory { get; private set; }
    public NpcConfigResponse[] NpcCatalog { get; private set; }
    public NpcResponse[] PlayerNpcs { get; private set; }
    private bool productionPollPending;
    private bool buildingSyncPending;
    private float nextProductionPollAt;
    private readonly List<LandConfig> remoteAvailableLands = new List<LandConfig>();

    public static BackendGameService EnsureInstance()
    {
        if (Instance != null) return Instance;

        BackendGameService existing = FindFirstObjectByType<BackendGameService>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject serviceObject = new GameObject("BackendGameService");
        return serviceObject.AddComponent<BackendGameService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Token = PlayerPrefs.GetString(TokenKey, string.Empty);
        PlayerId = PlayerPrefs.GetInt(PlayerIdKey, 0);
    }

    private void Start()
    {
        if (connectOnStart) Connect();
    }

    private void Update()
    {
        if (!pollProductions || IsConnecting || !IsAuthenticated || productionPollPending) return;
        if (Time.unscaledTime < nextProductionPollAt) return;

        productionPollPending = true;
        StartCoroutine(RefreshProductionsRoutine((success, message) =>
        {
            productionPollPending = false;
            ScheduleNextProductionPoll();
        }));
    }

    public void Connect()
    {
        if (IsConnecting) return;
        StartCoroutine(ConnectRoutine());
    }

    public Coroutine RefreshUser()
    {
        if (!IsAuthenticated) return null;
        return StartCoroutine(RequestUserRoutine(null));
    }

    public Coroutine RefreshInventory(Action<BackendResult<InventoryItemResponse[]>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(new BackendResult<InventoryItemResponse[]>(false, 0, "Backend session is not authenticated.", null));
            return null;
        }
        return StartCoroutine(RequestInventoryRoutine(onComplete));
    }

    public Coroutine SyncMap(Action<bool, string> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(false, "Backend session is not authenticated.");
            return null;
        }
        return StartCoroutine(SyncMapRoutine(onComplete));
    }

    public Coroutine RefreshProductions(Action<bool, string> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(false, "Backend session is not authenticated.");
            return null;
        }
        return StartCoroutine(RefreshProductionsRoutine(onComplete));
    }

    public Coroutine PlaceLand(LandSlotController slot, LandConfig config, Action<bool, string> onComplete)
    {
        if (!IsAuthenticated || slot == null || config == null)
        {
            onComplete?.Invoke(false, "Backend session, slot, or land config is missing.");
            return null;
        }
        return StartCoroutine(PlaceLandRoutine(slot, config, onComplete));
    }

    public Coroutine BuildBuilding(LandSlotController slot, BuildingConfig config, Action<bool, string> onComplete)
    {
        if (!IsAuthenticated || slot == null || config == null)
        {
            onComplete?.Invoke(false, "Backend session, slot, or building config is missing.");
            return null;
        }
        return StartCoroutine(BuildBuildingRoutine(slot, config, onComplete));
    }

    public Coroutine CheckBuilding(BuildingConfig config, Action<BackendResult<BuildingConditionResponse>> onComplete)
    {
        if (!IsAuthenticated || config == null)
        {
            onComplete?.Invoke(new BackendResult<BuildingConditionResponse>(false, 0, "Backend session or building config is missing.", null));
            return null;
        }
        return StartCoroutine(CheckBuildingRoutine(config, onComplete));
    }

    public Coroutine CheckBuildingForLand(LandSlotController slot, BuildingConfig config, Action<BackendResult<BuildingConditionResponse>> onComplete)
    {
        if (!IsAuthenticated || slot == null || config == null)
        {
            onComplete?.Invoke(new BackendResult<BuildingConditionResponse>(false, 0, "Backend session, slot, or building config is missing.", null));
            return null;
        }
        return StartCoroutine(CheckBuildingForLandRoutine(slot, config, onComplete));
    }

    public Coroutine LoadBuildingList(Action<BackendResult<BuildingResponse[]>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(new BackendResult<BuildingResponse[]>(false, 0, "Backend session is not authenticated.", null));
            return null;
        }
        return StartCoroutine(LoadBuildingListRoutine(onComplete));
    }

    public Coroutine LoadBuildingDetail(LandSlotController slot, Action<BackendResult<BuildingResponse>> onComplete = null)
    {
        if (!IsAuthenticated || slot == null)
        {
            onComplete?.Invoke(new BackendResult<BuildingResponse>(false, 0, "Backend session or slot is missing.", null));
            return null;
        }
        return StartCoroutine(LoadBuildingDetailRoutine(slot, onComplete));
    }

    public Coroutine SyncBuildingStatus(Action<BackendResult<BuildingResponse[]>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(new BackendResult<BuildingResponse[]>(false, 0, "Backend session is not authenticated.", null));
            return null;
        }
        return StartCoroutine(SyncBuildingStatusRoutine(onComplete));
    }

    public Coroutine UpgradeBuilding(LandSlotController slot, Action<bool, string> onComplete)
    {
        return RunPlayerBuildingAction("building/upgrade", slot, true, onComplete);
    }

    public Coroutine CancelBuildingAction(LandSlotController slot, Action<bool, string> onComplete)
    {
        return RunPlayerBuildingAction("building/cancel", slot, true, onComplete);
    }

    public Coroutine CompleteBuildingAction(LandSlotController slot, Action<bool, string> onComplete)
    {
        return RunPlayerBuildingAction("building/complete", slot, false, onComplete);
    }

    public Coroutine DemolishBuilding(LandSlotController slot, Action<bool, string> onComplete)
    {
        if (!IsAuthenticated || slot == null)
        {
            onComplete?.Invoke(false, "Backend session or slot is missing.");
            return null;
        }
        return StartCoroutine(DemolishBuildingRoutine(slot, onComplete));
    }

    public Coroutine MoveBuilding(LandSlotController source, LandSlotController target, Action<bool, string> onComplete)
    {
        if (!IsAuthenticated || source == null || target == null)
        {
            onComplete?.Invoke(false, "Backend session, source, or target slot is missing.");
            return null;
        }
        return StartCoroutine(MoveBuildingRoutine(source, target, onComplete));
    }

    public Coroutine CollectProduction(LandSlotController slot, Action<bool, string> onComplete)
    {
        if (!IsAuthenticated || slot == null)
        {
            onComplete?.Invoke(false, "Backend session or slot is missing.");
            return null;
        }
        return StartCoroutine(CollectProductionRoutine(slot, onComplete));
    }

    public Coroutine LoadProductionDetail(LandSlotController slot, Action<BackendResult<ProductionResponse[]>> onComplete = null)
    {
        if (!IsAuthenticated || slot == null)
        {
            onComplete?.Invoke(new BackendResult<ProductionResponse[]>(false, 0, "Backend session or slot is missing.", null));
            return null;
        }
        return StartCoroutine(LoadProductionDetailRoutine(slot, onComplete));
    }

    public Coroutine CollectAllProductions(Action<bool, string, int> onComplete)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(false, "Backend session is not authenticated.", 0);
            return null;
        }
        return StartCoroutine(CollectAllProductionsRoutine(onComplete));
    }

    public Coroutine GetAvailableNpcs(LandSlotController slot, Action<BackendResult<NpcResponse[]>> onComplete)
    {
        if (!IsAuthenticated || slot == null)
        {
            onComplete?.Invoke(new BackendResult<NpcResponse[]>(false, 0, "Backend session or slot is missing.", null));
            return null;
        }
        return StartCoroutine(GetAvailableNpcsRoutine(slot, onComplete));
    }

    public Coroutine LoadNpcCatalog(Action<BackendResult<NpcConfigResponse[]>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(new BackendResult<NpcConfigResponse[]>(false, 0, "Backend session is not authenticated.", null));
            return null;
        }
        return StartCoroutine(LoadNpcCatalogRoutine(onComplete));
    }

    public Coroutine LoadPlayerNpcs(Action<BackendResult<NpcResponse[]>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(new BackendResult<NpcResponse[]>(false, 0, "Backend session is not authenticated.", null));
            return null;
        }
        return StartCoroutine(LoadPlayerNpcsRoutine(onComplete));
    }

    public Coroutine SettleNpc(LandSlotController slot, int playerNpcId, Action<bool, string> onComplete)
    {
        if (!IsAuthenticated || slot == null || playerNpcId <= 0)
        {
            onComplete?.Invoke(false, "Backend session, slot, or player NPC id is missing.");
            return null;
        }
        return StartCoroutine(SettleNpcRoutine(slot, playerNpcId, onComplete));
    }

    public Coroutine RemoveNpc(LandSlotController slot, Action<bool, string> onComplete)
    {
        if (!IsAuthenticated || slot == null)
        {
            onComplete?.Invoke(false, "Backend session or slot is missing.");
            return null;
        }
        return StartCoroutine(RemoveNpcRoutine(slot, onComplete));
    }

    public Coroutine Logout(Action<bool, string> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            ClearSession();
            onComplete?.Invoke(true, "Already logged out.");
            return null;
        }
        return StartCoroutine(LogoutRoutine(onComplete));
    }

    private IEnumerator ConnectRoutine()
    {
        IsConnecting = true;

        if (IsAuthenticated)
        {
            bool valid = false;
            yield return RequestUserRoutine(result => valid = result);
            if (valid)
            {
                yield return LoadInitialDataRoutine();
                IsConnecting = false;
                yield break;
            }
        }

        if (autoLoginWhenMissingToken)
        {
            bool loginSuccess = false;
            yield return LoginRoutine(defaultUsername, defaultPassword, result => loginSuccess = result);
            if (!loginSuccess && registerDefaultUserIfLoginFails)
            {
                yield return RegisterRoutine(defaultUsername, defaultPassword, defaultUsername, result => loginSuccess = result);
            }
            if (IsAuthenticated) yield return LoadInitialDataRoutine();
        }

        IsConnecting = false;
    }

    private IEnumerator LoginRoutine(string username, string password, Action<bool> onComplete)
    {
        LoginRequest request = new LoginRequest { username = username, password = password };
        BackendResult<LoginResponse> result = null;
        yield return BackendApiClient.Post<LoginResponse>(baseUrl, null, "user/login", request, value => result = value);

        bool success = result != null && result.Success && result.Data != null;
        if (success) StoreLogin(result.Data);
        else Debug.LogWarning($"Backend login failed: {ResultMessage(result)}", this);
        onComplete?.Invoke(success);
    }

    private IEnumerator RegisterRoutine(string username, string password, string nickname, Action<bool> onComplete)
    {
        RegisterRequest request = new RegisterRequest { username = username, password = password, nickname = nickname };
        BackendResult<LoginResponse> result = null;
        yield return BackendApiClient.Post<LoginResponse>(baseUrl, null, "user/register", request, value => result = value);

        bool success = result != null && result.Success && result.Data != null;
        if (success) StoreLogin(result.Data);
        else Debug.LogWarning($"Backend register failed: {ResultMessage(result)}", this);
        onComplete?.Invoke(success);
    }

    private IEnumerator LoadInitialDataRoutine()
    {
        yield return LoadAvailableLandsRoutine();
        yield return LoadAvailableBuildingsRoutine();
        yield return SyncBuildingStatusRoutine(null);
        yield return SyncMapRoutine(null);
        yield return RefreshProductionsRoutine(null);
        yield return RequestUserRoutine(null);
        yield return RequestInventoryRoutine(null);
        yield return LoadNpcCatalogRoutine(null);
        yield return LoadPlayerNpcsRoutine(null);
        ScheduleNextProductionPoll();
    }

    private IEnumerator RequestUserRoutine(Action<bool> onComplete)
    {
        BackendResult<UserResponse> result = null;
        yield return BackendApiClient.Get<UserResponse>(baseUrl, Token, "user/me", value => result = value);

        bool success = result != null && result.Success && result.Data != null;
        if (success)
        {
            CurrentUser = result.Data;
            PlayerId = CurrentUser.id;
            PlayerPrefs.SetInt(PlayerIdKey, PlayerId);
            PlayerPrefs.Save();
            Debug.Log($"Backend user loaded: {CurrentUser.nickname} gold={CurrentUser.gold} wood={CurrentUser.wood} stone={CurrentUser.stone} iron={CurrentUser.iron}", this);
        }
        else if (result != null && result.HttpStatus == 401)
        {
            ClearSession();
        }
        else
        {
            Debug.LogWarning($"Backend user refresh failed: {ResultMessage(result)}", this);
        }

        onComplete?.Invoke(success);
    }

    private IEnumerator RequestInventoryRoutine(Action<BackendResult<InventoryItemResponse[]>> onComplete)
    {
        BackendResult<InventoryItemResponse[]> result = null;
        yield return BackendApiClient.Get<InventoryItemResponse[]>(baseUrl, Token, $"inventory/list/{PlayerId}", value => result = value);

        if (result != null && result.Success && result.Data != null)
        {
            CurrentInventory = result.Data;
            if (CurrentUser == null) CurrentUser = new UserResponse();
            CurrentUser.items = result.Data;
        }
        else
        {
            Debug.LogWarning($"Backend inventory refresh failed: {ResultMessage(result)}", this);
        }

        onComplete?.Invoke(result);
    }

    private IEnumerator LoadAvailableBuildingsRoutine()
    {
        BackendResult<BuildingConfigResponse[]> result = null;
        yield return BackendApiClient.Get<BuildingConfigResponse[]>(baseUrl, Token, $"building/available/{PlayerId}", value => result = value);

        if (result == null || !result.Success || result.Data == null)
        {
            Debug.LogWarning($"Backend available buildings failed: {ResultMessage(result)}", this);
            yield break;
        }

        List<BuildingConfig> configs = new List<BuildingConfig>();
        for (int i = 0; i < result.Data.Length; i++)
        {
            BuildingConfig config = RuntimeConfigFactory.ResolveBuilding(result.Data[i]);
            if (config != null) configs.Add(config);
        }

        if (BuildingManager.Instance != null) BuildingManager.Instance.SetRemoteAvailableBuildings(configs);
    }

    private IEnumerator LoadAvailableLandsRoutine()
    {
        BackendResult<LandConfigResponse[]> result = null;
        yield return BackendApiClient.Get<LandConfigResponse[]>(baseUrl, Token, $"land/available/{PlayerId}", value => result = value);

        remoteAvailableLands.Clear();
        if (result == null || !result.Success || result.Data == null)
        {
            Debug.LogWarning($"Backend available lands failed: {ResultMessage(result)}", this);
            ApplyRemoteLandDragItems();
            yield break;
        }

        for (int i = 0; i < result.Data.Length; i++)
        {
            LandConfigResponse response = result.Data[i];
            if (response == null || !response.canPlace) continue;

            LandConfig config = RuntimeConfigFactory.ResolveLand(response);
            if (config != null) remoteAvailableLands.Add(config);
        }

        ApplyRemoteLandDragItems();
    }

    private void ApplyRemoteLandDragItems()
    {
        BuildPanelController panel = FindFirstObjectByType<BuildPanelController>();
        if (panel != null) panel.SetRemoteAvailableLands(remoteAvailableLands);
    }

    private IEnumerator CheckBuildingRoutine(BuildingConfig config, Action<BackendResult<BuildingConditionResponse>> onComplete)
    {
        BackendResult<BuildingConditionResponse> result = null;
        yield return BackendApiClient.Get<BuildingConditionResponse>(baseUrl, Token, $"building/check/{PlayerId}/{config.buildingId}", value => result = value);
        onComplete?.Invoke(result);
    }

    private IEnumerator CheckBuildingForLandRoutine(LandSlotController slot, BuildingConfig config, Action<BackendResult<BuildingConditionResponse>> onComplete)
    {
        BackendResult<BuildingConditionResponse> result = null;
        yield return BackendApiClient.Get<BuildingConditionResponse>(baseUrl, Token, $"building/check/{PlayerId}/{config.buildingId}/{slot.PlayerLandId}", value => result = value);
        onComplete?.Invoke(result);
    }

    private IEnumerator LoadBuildingListRoutine(Action<BackendResult<BuildingResponse[]>> onComplete)
    {
        BackendResult<BuildingResponse[]> result = null;
        yield return BackendApiClient.Get<BuildingResponse[]>(baseUrl, Token, $"building/list/{PlayerId}", value => result = value);

        if (result != null && result.Success && result.Data != null)
        {
            if (MapManager.Instance != null) MapManager.Instance.ApplyRemoteBuildings(result.Data);
        }
        else
        {
            Debug.LogWarning($"Backend building list failed: {ResultMessage(result)}", this);
        }

        onComplete?.Invoke(result);
    }

    private IEnumerator LoadBuildingDetailRoutine(LandSlotController slot, Action<BackendResult<BuildingResponse>> onComplete)
    {
        BackendResult<BuildingResponse> result = null;
        yield return BackendApiClient.Get<BuildingResponse>(baseUrl, Token, $"building/detail/{PlayerId}/{slot.PlayerBuildingId}", value => result = value);

        if (result != null && result.Success && result.Data != null)
        {
            slot.ApplyRemoteBuilding(RuntimeConfigFactory.ResolveBuilding(result.Data), result.Data);
        }
        else
        {
            Debug.LogWarning($"Backend building detail failed: {ResultMessage(result)}", slot);
        }

        onComplete?.Invoke(result);
    }

    private IEnumerator SyncBuildingStatusRoutine(Action<BackendResult<BuildingResponse[]>> onComplete)
    {
        if (buildingSyncPending)
        {
            onComplete?.Invoke(new BackendResult<BuildingResponse[]>(false, 0, "A building sync is already pending.", null));
            yield break;
        }

        buildingSyncPending = true;
        BackendResult<BuildingResponse[]> result = null;
        yield return BackendApiClient.Post<BuildingResponse[]>(baseUrl, Token, $"building/sync/{PlayerId}", null, value => result = value);
        buildingSyncPending = false;

        if (result != null && result.Success && result.Data != null)
        {
            if (MapManager.Instance != null) MapManager.Instance.ApplyRemoteBuildings(result.Data);
        }
        else
        {
            Debug.LogWarning($"Backend building sync failed: {ResultMessage(result)}", this);
        }

        onComplete?.Invoke(result);
    }

    private IEnumerator SyncMapRoutine(Action<bool, string> onComplete)
    {
        BackendResult<MapDataResponse> result = null;
        yield return BackendApiClient.Get<MapDataResponse>(baseUrl, Token, $"land/map/{PlayerId}", value => result = value);

        bool success = result != null && result.Success && result.Data != null;
        if (success)
        {
            if (MapManager.Instance != null) MapManager.Instance.ApplyRemoteMapData(result.Data);
        }
        else
        {
            Debug.LogWarning($"Backend map sync failed: {ResultMessage(result)}", this);
        }

        onComplete?.Invoke(success, ResultMessage(result));
    }

    private IEnumerator RefreshProductionsRoutine(Action<bool, string> onComplete)
    {
        BackendResult<ProductionResponse[]> result = null;
        yield return BackendApiClient.Get<ProductionResponse[]>(baseUrl, Token, $"production/list/{PlayerId}", value => result = value);

        bool success = result != null && result.Success && result.Data != null;
        if (success)
        {
            if (MapManager.Instance != null) MapManager.Instance.ApplyRemoteProductions(result.Data);
        }
        else
        {
            Debug.LogWarning($"Backend production refresh failed: {ResultMessage(result)}", this);
        }

        onComplete?.Invoke(success, ResultMessage(result));
    }

    private void ScheduleNextProductionPoll()
    {
        nextProductionPollAt = Time.unscaledTime + Mathf.Max(1f, productionPollInterval);
    }

    private IEnumerator LoadProductionDetailRoutine(LandSlotController slot, Action<BackendResult<ProductionResponse[]>> onComplete)
    {
        BackendResult<ProductionResponse[]> result = null;
        yield return BackendApiClient.Get<ProductionResponse[]>(baseUrl, Token, $"production/detail/{PlayerId}/{slot.PlayerBuildingId}", value => result = value);

        if (result != null && result.Success && result.Data != null)
        {
            if (MapManager.Instance != null) MapManager.Instance.ApplyRemoteProductions(result.Data);
        }
        else
        {
            Debug.LogWarning($"Backend production detail failed: {ResultMessage(result)}", slot);
        }

        onComplete?.Invoke(result);
    }

    private IEnumerator PlaceLandRoutine(LandSlotController slot, LandConfig config, Action<bool, string> onComplete)
    {
        PlaceLandRequest request = new PlaceLandRequest
        {
            playerId = PlayerId,
            landId = config.landId,
            positionX = slot.GridPosition.x,
            positionY = slot.GridPosition.y
        };

        BackendResult<LandTileResponse> result = null;
        yield return BackendApiClient.Post<LandTileResponse>(baseUrl, Token, "land/place", request, value => result = value);

        bool success = result != null && result.Success && result.Data != null;
        if (success)
        {
            slot.ApplyRemoteLand(RuntimeConfigFactory.ResolveLand(result.Data), result.Data);
            if (MapManager.Instance != null) MapManager.Instance.MarkOccupied(slot);
            yield return RequestUserRoutine(null);
            yield return LoadAvailableLandsRoutine();
        }

        onComplete?.Invoke(success, ResultMessage(result));
    }

    private IEnumerator BuildBuildingRoutine(LandSlotController slot, BuildingConfig config, Action<bool, string> onComplete)
    {
        BuildBuildingRequest request = new BuildBuildingRequest
        {
            playerId = PlayerId,
            buildingId = config.buildingId,
            landId = slot.PlayerLandId
        };

        BackendResult<BuildingResponse> result = null;
        yield return BackendApiClient.Post<BuildingResponse>(baseUrl, Token, "building/build", request, value => result = value);

        bool success = result != null && result.Success && result.Data != null;
        if (success)
        {
            slot.ApplyRemoteBuilding(RuntimeConfigFactory.ResolveBuilding(result.Data), result.Data);
            yield return RequestUserRoutine(null);
            yield return LoadAvailableBuildingsRoutine();
        }

        onComplete?.Invoke(success, ResultMessage(result));
    }

    private Coroutine RunPlayerBuildingAction(string path, LandSlotController slot, bool refreshUser, Action<bool, string> onComplete)
    {
        if (!IsAuthenticated || slot == null)
        {
            onComplete?.Invoke(false, "Backend session or slot is missing.");
            return null;
        }
        return StartCoroutine(PlayerBuildingActionRoutine(path, slot, refreshUser, onComplete));
    }

    private IEnumerator PlayerBuildingActionRoutine(string path, LandSlotController slot, bool refreshUser, Action<bool, string> onComplete)
    {
        PlayerBuildingRequest request = new PlayerBuildingRequest
        {
            playerId = PlayerId,
            playerBuildingId = slot.PlayerBuildingId
        };

        BackendResult<BuildingResponse> result = null;
        yield return BackendApiClient.Post<BuildingResponse>(baseUrl, Token, path, request, value => result = value);

        bool success = result != null && result.Success;
        if (success)
        {
            if (result.Data != null) slot.ApplyRemoteBuilding(RuntimeConfigFactory.ResolveBuilding(result.Data), result.Data);
            yield return SyncMapRoutine(null);
            if (refreshUser)
            {
                yield return RequestUserRoutine(null);
                yield return LoadAvailableBuildingsRoutine();
                yield return LoadAvailableLandsRoutine();
            }
        }

        onComplete?.Invoke(success, ResultMessage(result));
    }

    private IEnumerator DemolishBuildingRoutine(LandSlotController slot, Action<bool, string> onComplete)
    {
        PlayerBuildingRequest request = new PlayerBuildingRequest
        {
            playerId = PlayerId,
            playerBuildingId = slot.PlayerBuildingId
        };

        BackendResult<EmptyResponse> result = null;
        yield return BackendApiClient.Post<EmptyResponse>(baseUrl, Token, "building/demolish", request, value => result = value);

        bool success = result != null && result.Success;
        if (success)
        {
            slot.ClearBuildingStateFromBackend();
            yield return SyncMapRoutine(null);
        }

        onComplete?.Invoke(success, ResultMessage(result));
    }

    private IEnumerator MoveBuildingRoutine(LandSlotController source, LandSlotController target, Action<bool, string> onComplete)
    {
        MoveBuildingRequest request = new MoveBuildingRequest
        {
            playerId = PlayerId,
            playerBuildingId = source.PlayerBuildingId,
            targetLandId = target.PlayerLandId
        };

        BackendResult<BuildingResponse> result = null;
        yield return BackendApiClient.Post<BuildingResponse>(baseUrl, Token, "building/move", request, value => result = value);

        bool success = result != null && result.Success;
        if (success)
        {
            yield return SyncMapRoutine(null);
        }

        onComplete?.Invoke(success, ResultMessage(result));
    }

    private IEnumerator CollectProductionRoutine(LandSlotController slot, Action<bool, string> onComplete)
    {
        PlayerBuildingRequest request = new PlayerBuildingRequest
        {
            playerId = PlayerId,
            playerBuildingId = slot.PlayerBuildingId
        };

        BackendResult<ProductionCollectResponse> result = null;
        yield return BackendApiClient.Post<ProductionCollectResponse>(baseUrl, Token, "production/collect", request, value => result = value);

        bool success = result != null && result.Success && result.Data != null;
        if (success)
        {
            slot.ClearRemoteProductionState();
            ApplyProductionCollect(result.Data);
            yield return LoadAvailableLandsRoutine();
            yield return LoadAvailableBuildingsRoutine();
        }
        onComplete?.Invoke(success, ResultMessage(result));
    }

    private IEnumerator CollectAllProductionsRoutine(Action<bool, string, int> onComplete)
    {
        PlayerIdRequest request = new PlayerIdRequest { playerId = PlayerId };
        BackendResult<ProductionCollectResponse> result = null;
        yield return BackendApiClient.Post<ProductionCollectResponse>(baseUrl, Token, "production/collect-all", request, value => result = value);

        bool success = result != null && result.Success && result.Data != null;
        int collectedCount = 0;
        if (success)
        {
            collectedCount = result.Data.collectedItems != null ? result.Data.collectedItems.Length : 0;
            if (MapManager.Instance != null) MapManager.Instance.ClearReadyRemoteProductions();
            ApplyProductionCollect(result.Data);
            yield return LoadAvailableLandsRoutine();
            yield return LoadAvailableBuildingsRoutine();
        }

        onComplete?.Invoke(success, ResultMessage(result), collectedCount);
    }

    private IEnumerator GetAvailableNpcsRoutine(LandSlotController slot, Action<BackendResult<NpcResponse[]>> onComplete)
    {
        BackendResult<NpcResponse[]> result = null;
        yield return BackendApiClient.Get<NpcResponse[]>(baseUrl, Token, $"npc/available/{PlayerId}?playerBuildingId={slot.PlayerBuildingId}", value => result = value);
        onComplete?.Invoke(result);
    }

    private IEnumerator LoadNpcCatalogRoutine(Action<BackendResult<NpcConfigResponse[]>> onComplete)
    {
        BackendResult<NpcConfigResponse[]> result = null;
        yield return BackendApiClient.Get<NpcConfigResponse[]>(baseUrl, Token, "npc/config/list", value => result = value);

        if (result != null && result.Success && result.Data != null)
        {
            NpcCatalog = result.Data;
        }
        else
        {
            Debug.LogWarning($"Backend NPC catalog failed: {ResultMessage(result)}", this);
        }

        onComplete?.Invoke(result);
    }

    private IEnumerator LoadPlayerNpcsRoutine(Action<BackendResult<NpcResponse[]>> onComplete)
    {
        BackendResult<NpcResponse[]> result = null;
        yield return BackendApiClient.Get<NpcResponse[]>(baseUrl, Token, $"npc/list/{PlayerId}", value => result = value);

        if (result != null && result.Success && result.Data != null)
        {
            PlayerNpcs = result.Data;
        }
        else
        {
            Debug.LogWarning($"Backend player NPC list failed: {ResultMessage(result)}", this);
        }

        onComplete?.Invoke(result);
    }

    private IEnumerator SettleNpcRoutine(LandSlotController slot, int playerNpcId, Action<bool, string> onComplete)
    {
        NpcSettleRequest request = new NpcSettleRequest
        {
            playerId = PlayerId,
            playerBuildingId = slot.PlayerBuildingId,
            playerNpcId = playerNpcId
        };

        BackendResult<ProductionResponse[]> result = null;
        yield return BackendApiClient.Post<ProductionResponse[]>(baseUrl, Token, "production/npc/settle", request, value => result = value);

        bool success = result != null && result.Success && result.Data != null;
        if (success)
        {
            if (MapManager.Instance != null) MapManager.Instance.ApplyRemoteProductions(result.Data);
            yield return LoadPlayerNpcsRoutine(null);
            ScheduleNextProductionPoll();
        }

        onComplete?.Invoke(success, ResultMessage(result));
    }

    private IEnumerator RemoveNpcRoutine(LandSlotController slot, Action<bool, string> onComplete)
    {
        PlayerBuildingRequest request = new PlayerBuildingRequest
        {
            playerId = PlayerId,
            playerBuildingId = slot.PlayerBuildingId
        };

        BackendResult<ProductionResponse[]> result = null;
        yield return BackendApiClient.Post<ProductionResponse[]>(baseUrl, Token, "production/npc/remove", request, value => result = value);

        bool success = result != null && result.Success && result.Data != null;
        if (success)
        {
            if (MapManager.Instance != null) MapManager.Instance.ApplyRemoteProductions(result.Data);
            yield return LoadPlayerNpcsRoutine(null);
            ScheduleNextProductionPoll();
        }

        onComplete?.Invoke(success, ResultMessage(result));
    }

    private IEnumerator LogoutRoutine(Action<bool, string> onComplete)
    {
        BackendResult<EmptyResponse> result = null;
        yield return BackendApiClient.Post<EmptyResponse>(baseUrl, Token, "user/logout", null, value => result = value);

        bool success = result != null && result.Success;
        if (success || result != null && result.HttpStatus == 401)
        {
            ClearSession();
        }
        else
        {
            Debug.LogWarning($"Backend logout failed: {ResultMessage(result)}", this);
        }

        onComplete?.Invoke(success, ResultMessage(result));
    }

    private void ApplyProductionCollect(ProductionCollectResponse response)
    {
        if (response == null) return;
        if (MapManager.Instance != null) MapManager.Instance.ApplyRemoteProductions(response.productions);
        if (CurrentUser == null) CurrentUser = new UserResponse();
        CurrentUser.items = response.items;
        CurrentInventory = response.items;
        Debug.Log("Backend production collect succeeded; inventory snapshot refreshed.", this);
    }

    private void StoreLogin(LoginResponse response)
    {
        Token = response.token;
        CurrentUser = response.user;
        PlayerId = response.user != null ? response.user.id : 0;
        PlayerPrefs.SetString(TokenKey, Token);
        PlayerPrefs.SetInt(PlayerIdKey, PlayerId);
        PlayerPrefs.Save();
        Debug.Log($"Backend login succeeded: playerId={PlayerId}", this);
    }

    private void ClearSession()
    {
        Token = string.Empty;
        PlayerId = 0;
        CurrentUser = null;
        CurrentInventory = null;
        NpcCatalog = null;
        PlayerNpcs = null;
        PlayerPrefs.DeleteKey(TokenKey);
        PlayerPrefs.DeleteKey(PlayerIdKey);
        PlayerPrefs.Save();
    }

    private static string ResultMessage<T>(BackendResult<T> result)
    {
        if (result == null) return "No backend response.";
        return string.IsNullOrEmpty(result.Message) ? $"HTTP {result.HttpStatus}" : result.Message;
    }
}
