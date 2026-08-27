using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public sealed partial class BackendGameService : MonoBehaviour
{
    private const string TokenKey = "weijun.backend.token";
    private const string PlayerIdKey = "weijun.backend.playerId";
    private const string SessionManagedKey = "weijun.backend.managedSession";
    private const string SessionRevisionKey = "weijun.backend.sessionRevision";
    private const string HomeLoginStateKey = "LoginState";
    private const string HomeTokenKey = "AuthToken";
    private const string HomePlayerIdKey = "PlayerId";
    private const string HomeSessionModeKey = "SessionMode";
    private const string HomeTokenExpiresAtKey = "TokenExpiresAtUtc";
    private const string ApiOverrideKey = "ApiBaseUrlOverride";
    private const string ApiEnvironmentVariableName = "WEIJUN_API_BASE_URL";

    public static BackendGameService Instance { get; private set; }

    [Header("Connection")]
    [SerializeField] private string baseUrl = "http://localhost:8080/api";
    [SerializeField] private bool connectOnStart = true;
    [SerializeField] private bool autoLoginWhenMissingToken = false;
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
    private int observedSessionRevision;
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
        if (TryResolveConfiguredBaseUrl(out string configuredBaseUrl, out string configurationError))
        {
            baseUrl = configuredBaseUrl;
        }
        else
        {
            Debug.LogWarning("Backend API 地址配置无效：" + configurationError, this);
        }
        Token = PlayerPrefs.GetString(TokenKey, string.Empty);
        PlayerId = PlayerPrefs.GetInt(PlayerIdKey, 0);
        CurrentSceneId = PlayerPrefs.GetInt(SceneIdKey, 0);
        observedSessionRevision = PlayerPrefs.GetInt(SessionRevisionKey, 0);
    }

    private static bool TryResolveConfiguredBaseUrl(out string configuredBaseUrl, out string error)
    {
        if (TryNormalizeBaseUrl(PlayerPrefs.GetString(ApiOverrideKey, string.Empty), out configuredBaseUrl, out error))
        {
            return true;
        }

        if (TryNormalizeBaseUrl(Environment.GetEnvironmentVariable(ApiEnvironmentVariableName), out configuredBaseUrl, out error))
        {
            return true;
        }

        TextAsset configAsset = Resources.Load<TextAsset>("ApiConfig");
        BackendApiConfigData config = null;
        if (configAsset != null && !string.IsNullOrWhiteSpace(configAsset.text))
        {
            try
            {
                config = JsonUtility.FromJson<BackendApiConfigData>(configAsset.text);
            }
            catch (Exception exception)
            {
                error = "ApiConfig.json 解析失败：" + exception.Message;
                configuredBaseUrl = string.Empty;
                return false;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string candidate = config != null ? config.developmentBaseUrl : string.Empty;
#else
        string candidate = config != null ? config.productionBaseUrl : string.Empty;
#endif
        if (TryNormalizeBaseUrl(candidate, out configuredBaseUrl, out error))
        {
            return true;
        }

        configuredBaseUrl = string.Empty;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        error = "开发接口地址无效，请检查 Resources/ApiConfig.json。";
#else
        error = "尚未配置生产接口地址，请设置 ApiConfig.json 的 productionBaseUrl。";
#endif
        return false;
    }

    private static bool TryNormalizeBaseUrl(string rawUrl, out string normalizedUrl, out string error)
    {
        normalizedUrl = string.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return false;
        }

        string candidate = rawUrl.Trim().TrimEnd('/');
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            error = "接口地址格式无效：" + candidate;
            return false;
        }

        normalizedUrl = candidate;
        return true;
    }

    [Serializable]
    private class BackendApiConfigData
    {
        public string developmentBaseUrl;
        public string productionBaseUrl;
    }

    private void Start()
    {
        if (connectOnStart) Connect();
    }

    private void Update()
    {
        RefreshSharedSessionIfNeeded();
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

    public Coroutine PurchaseLandSlot(
        LandSlotController slot,
        Action<bool, string, LandSlotStateResponse> onComplete)
    {
        if (!IsAuthenticated || slot == null)
        {
            onComplete?.Invoke(false, "Backend session or slot is missing.", null);
            return null;
        }
        return StartCoroutine(PurchaseLandSlotRoutine(slot, onComplete));
    }

    public Coroutine PurifyLandSlot(
        LandSlotController slot,
        Action<bool, string, LandSlotStateResponse> onComplete)
    {
        if (!IsAuthenticated || slot == null)
        {
            onComplete?.Invoke(false, "Backend session or slot is missing.", null);
            return null;
        }
        return StartCoroutine(PurifyLandSlotRoutine(slot, onComplete));
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

    public Coroutine LoadIdleAdventureAfkReward(Action<BackendResult<IdleAdventureAfkRewardStateResponse>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureAfkRewardStateResponse>(false, 0, "Backend session is not authenticated.", null));
            return null;
        }

        return StartCoroutine(LoadIdleAdventureAfkRewardRoutine(onComplete));
    }

    public Coroutine LoadIdleAdventureHome(Action<BackendResult<IdleAdventureHomeResponse>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureHomeResponse>(false, 0, "Backend session is not authenticated.", null));
            return null;
        }

        return StartCoroutine(LoadIdleAdventureHomeRoutine(onComplete));
    }

    public Coroutine LoadIdleAdventureFormation(Action<BackendResult<IdleAdventureFormationResponse>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureFormationResponse>(false, 0, "Backend session is not authenticated.", null));
            return null;
        }

        return StartCoroutine(LoadIdleAdventureFormationRoutine(onComplete));
    }

    public Coroutine SaveIdleAdventureFormation(IdleAdventureSaveFormationRequest request, Action<BackendResult<IdleAdventureFormationResponse>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureFormationResponse>(false, 0, "Backend session is not authenticated.", null));
            return null;
        }

        return StartCoroutine(SaveIdleAdventureFormationRoutine(request, onComplete));
    }

    public Coroutine AutoFillIdleAdventureFormation(Action<BackendResult<IdleAdventureFormationResponse>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureFormationResponse>(false, 0, "Backend session is not authenticated.", null));
            return null;
        }

        return StartCoroutine(AutoFillIdleAdventureFormationRoutine(onComplete));
    }

    public Coroutine ClearIdleAdventureFormation(Action<BackendResult<IdleAdventureFormationResponse>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureFormationResponse>(false, 0, "Backend session is not authenticated.", null));
            return null;
        }

        return StartCoroutine(ClearIdleAdventureFormationRoutine(onComplete));
    }

    public Coroutine StartIdleAdventureSession(IdleAdventureStartSessionRequest request, Action<BackendResult<IdleAdventureSessionResponse>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureSessionResponse>(false, 0, "Backend session is not authenticated.", null));
            return null;
        }

        return StartCoroutine(StartIdleAdventureSessionRoutine(request, onComplete));
    }

    public Coroutine LoadIdleAdventureSession(string sessionKey, Action<BackendResult<IdleAdventureSessionResponse>> onComplete = null)
    {
        if (!IsAuthenticated || string.IsNullOrEmpty(sessionKey))
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureSessionResponse>(false, 0, "Backend session or session key is missing.", null));
            return null;
        }

        return StartCoroutine(LoadIdleAdventureSessionRoutine(sessionKey, onComplete));
    }

    public Coroutine ResolveIdleAdventureRound(string sessionKey, int clientRound, Action<BackendResult<IdleAdventureSessionResponse>> onComplete = null)
    {
        if (!IsAuthenticated || string.IsNullOrEmpty(sessionKey))
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureSessionResponse>(false, 0, "Backend session or session key is missing.", null));
            return null;
        }

        return StartCoroutine(ResolveIdleAdventureRoundRoutine(sessionKey, clientRound, onComplete));
    }

    public Coroutine ApplyIdleAdventureBuff(string sessionKey, IdleAdventureApplyBuffRequest request, Action<BackendResult<IdleAdventureBuffAppliedResponse>> onComplete = null)
    {
        if (!IsAuthenticated || string.IsNullOrEmpty(sessionKey) || request == null)
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureBuffAppliedResponse>(false, 0, "Backend session, session key, or buff request is missing.", null));
            return null;
        }

        return StartCoroutine(ApplyIdleAdventureBuffRoutine(sessionKey, request, onComplete));
    }

    public Coroutine ContinueIdleAdventureStage(string sessionKey, Action<BackendResult<IdleAdventureSessionResponse>> onComplete = null)
    {
        if (!IsAuthenticated || string.IsNullOrEmpty(sessionKey))
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureSessionResponse>(false, 0, "Backend session or session key is missing.", null));
            return null;
        }

        return StartCoroutine(PostIdleAdventureSessionCommandRoutine(sessionKey, "stages/continue", onComplete));
    }

    public Coroutine RetryIdleAdventureStage(string sessionKey, Action<BackendResult<IdleAdventureSessionResponse>> onComplete = null)
    {
        if (!IsAuthenticated || string.IsNullOrEmpty(sessionKey))
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureSessionResponse>(false, 0, "Backend session or session key is missing.", null));
            return null;
        }

        return StartCoroutine(PostIdleAdventureSessionCommandRoutine(sessionKey, "stages/retry", onComplete));
    }

    public Coroutine PauseIdleAdventureSession(string sessionKey, Action<BackendResult<IdleAdventureSessionResponse>> onComplete = null)
    {
        if (!IsAuthenticated || string.IsNullOrEmpty(sessionKey))
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureSessionResponse>(false, 0, "Backend session or session key is missing.", null));
            return null;
        }

        return StartCoroutine(PostIdleAdventureSessionCommandRoutine(sessionKey, "pause", onComplete));
    }

    public Coroutine ResumeIdleAdventureSession(string sessionKey, Action<BackendResult<IdleAdventureSessionResponse>> onComplete = null)
    {
        if (!IsAuthenticated || string.IsNullOrEmpty(sessionKey))
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureSessionResponse>(false, 0, "Backend session or session key is missing.", null));
            return null;
        }

        return StartCoroutine(PostIdleAdventureSessionCommandRoutine(sessionKey, "resume", onComplete));
    }

    public Coroutine EndIdleAdventureSession(string sessionKey, Action<BackendResult<IdleAdventureSessionResponse>> onComplete = null)
    {
        if (!IsAuthenticated || string.IsNullOrEmpty(sessionKey))
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureSessionResponse>(false, 0, "Backend session or session key is missing.", null));
            return null;
        }

        return StartCoroutine(PostIdleAdventureSessionCommandRoutine(sessionKey, "end", onComplete));
    }

    public Coroutine LoadIdleAdventureProgress(Action<BackendResult<IdleAdventureProgressResponse>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureProgressResponse>(false, 0, "Backend session is not authenticated.", null));
            return null;
        }

        return StartCoroutine(LoadIdleAdventureProgressRoutine(onComplete));
    }

    public Coroutine CollectIdleAdventureAfkReward(Action<BackendResult<IdleAdventureAfkRewardCollectResponse>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureAfkRewardCollectResponse>(false, 0, "Backend session is not authenticated.", null));
            return null;
        }

        return StartCoroutine(CollectIdleAdventureAfkRewardRoutine(onComplete));
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

        if (autoLoginWhenMissingToken && PlayerPrefs.GetInt(SessionManagedKey, 0) != 1)
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
        yield return LoadLandScenesRoutine(null);
        yield return LoadAvailableLandsRoutine();
        yield return LoadAvailableBuildingsRoutine();
        yield return SyncBuildingStatusRoutine(null);
        yield return SyncMapRoutine(null);
        yield return RefreshProductionsRoutine(null);
        yield return LoadProductionWorkStateRoutine(null);
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
            SyncWeChatFriendLevel(CurrentUser.level);
            RefreshLandUnlockState();
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
        yield return BackendApiClient.Get<BuildingConditionResponse>(baseUrl, Token, AddSceneQuery($"building/check/{PlayerId}/{config.buildingId}"), value => result = value);
        onComplete?.Invoke(result);
    }

    private IEnumerator CheckBuildingForLandRoutine(LandSlotController slot, BuildingConfig config, Action<BackendResult<BuildingConditionResponse>> onComplete)
    {
        BackendResult<BuildingConditionResponse> result = null;
        yield return BackendApiClient.Get<BuildingConditionResponse>(baseUrl, Token, AddSceneQuery($"building/check/{PlayerId}/{config.buildingId}/{slot.PlayerLandId}"), value => result = value);
        onComplete?.Invoke(result);
    }

    private IEnumerator LoadBuildingListRoutine(Action<BackendResult<BuildingResponse[]>> onComplete)
    {
        BackendResult<BuildingResponse[]> result = null;
        yield return BackendApiClient.Get<BuildingResponse[]>(baseUrl, Token, AddSceneQuery($"building/list/{PlayerId}"), value => result = value);

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
        yield return BackendApiClient.Get<BuildingResponse>(baseUrl, Token, AddSceneQuery($"building/detail/{PlayerId}/{slot.PlayerBuildingId}"), value => result = value);

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
        yield return BackendApiClient.Post<BuildingResponse[]>(baseUrl, Token, AddSceneQuery($"building/sync/{PlayerId}"), null, value => result = value);
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
        yield return BackendApiClient.Get<MapDataResponse>(baseUrl, Token, AddSceneQuery($"land/map/{PlayerId}"), value => result = value);

        bool success = result != null && result.Success && result.Data != null;
        if (success)
        {
            CurrentSceneId = result.Data.sceneId > 0 ? result.Data.sceneId : CurrentSceneId;
            if (result.Data.sceneConfigVersion > 0L)
            {
                CurrentSceneConfigVersion = result.Data.sceneConfigVersion;
            }
            if (result.Data.landPolicyRevision > 0L)
            {
                CurrentLandPolicyRevision = result.Data.landPolicyRevision;
            }
            if (CurrentSceneId > 0) PlayerPrefs.SetInt(SceneIdKey, CurrentSceneId);
            PlayerPrefs.Save();
            purificationSnapshots.Clear();
            if (result.Data.landSlots != null)
            {
                for (int i = 0; i < result.Data.landSlots.Length; i++)
                {
                    LandSlotStateResponse state = result.Data.landSlots[i];
                    if (state != null) purificationSnapshots[BuildPurificationKey(state.positionX, state.positionY)] = state;
                }
            }
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
        yield return BackendApiClient.Get<ProductionResponse[]>(baseUrl, Token, AddSceneQuery($"production/list/{PlayerId}"), value => result = value);

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
        yield return BackendApiClient.Get<ProductionResponse[]>(baseUrl, Token, AddSceneQuery($"production/detail/{PlayerId}/{slot.PlayerBuildingId}"), value => result = value);

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
            sceneId = CurrentSceneId,
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
            sceneId = CurrentSceneId,
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
            sceneId = CurrentSceneId,
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
            sceneId = CurrentSceneId,
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
            sceneId = CurrentSceneId,
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
            sceneId = CurrentSceneId,
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

    private IEnumerator PurchaseLandSlotRoutine(
        LandSlotController slot,
        Action<bool, string, LandSlotStateResponse> onComplete)
    {
        PurchaseLandSlotRequest request = new PurchaseLandSlotRequest
        {
            playerId = PlayerId,
            sceneId = CurrentSceneId,
            positionX = slot.GridPosition.x,
            positionY = slot.GridPosition.y,
            expectedSceneVersion = CurrentSceneConfigVersion,
            expectedPolicyRevision = CurrentLandPolicyRevision
        };

        BackendResult<LandSlotStateResponse> result = null;
        yield return BackendApiClient.Post<LandSlotStateResponse>(
            baseUrl,
            Token,
            "land/slot/purchase",
            request,
            value => result = value);

        bool success = result != null && result.Success && result.Data != null;
        if (success)
        {
            slot.ApplyRemoteUnlockState(result.Data);
            yield return RequestUserRoutine(null);
            yield return SyncMapRoutine(null);
        }

        onComplete?.Invoke(success, ResultMessage(result), result != null ? result.Data : null);
    }

    private IEnumerator PurifyLandSlotRoutine(
        LandSlotController slot,
        Action<bool, string, LandSlotStateResponse> onComplete)
    {
        PurchaseLandSlotRequest request = new PurchaseLandSlotRequest
        {
            playerId = PlayerId,
            sceneId = CurrentSceneId,
            positionX = slot.GridPosition.x,
            positionY = slot.GridPosition.y,
            expectedSceneVersion = CurrentSceneConfigVersion,
            expectedPolicyRevision = CurrentLandPolicyRevision
        };

        LandSlotStateResponse snapshot = FindPurificationSnapshot(slot.GridPosition.x, slot.GridPosition.y);
        if (snapshot != null)
        {
            request.purificationItemId = snapshot.purificationItemId;
            request.purificationItemKey = snapshot.purificationItemKey;
            request.purificationItemAmount = snapshot.purificationItemAmount;
        }

        BackendResult<LandSlotStateResponse> result = null;
        yield return BackendApiClient.Post<LandSlotStateResponse>(
            baseUrl,
            Token,
            "land/slot/purify",
            request,
            value => result = value);

        bool success = result != null && result.Success && result.Data != null;
        if (success)
        {
            slot.ApplyRemoteUnlockState(result.Data);
            yield return SyncMapRoutine(null);
        }

        onComplete?.Invoke(success, ResultMessage(result), result != null ? result.Data : null);
    }

    private IEnumerator CollectAllProductionsRoutine(Action<bool, string, int> onComplete)
    {
        PlayerIdRequest request = new PlayerIdRequest { playerId = PlayerId, sceneId = CurrentSceneId };
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

    private IEnumerator LoadIdleAdventureAfkRewardRoutine(Action<BackendResult<IdleAdventureAfkRewardStateResponse>> onComplete)
    {
        BackendResult<IdleAdventureAfkRewardStateResponse> result = null;
        yield return BackendApiClient.Get<IdleAdventureAfkRewardStateResponse>(baseUrl, Token, $"idle-adventure/players/{PlayerId}/afk-reward", value => result = value);
        onComplete?.Invoke(result);
    }

    private IEnumerator LoadIdleAdventureHomeRoutine(Action<BackendResult<IdleAdventureHomeResponse>> onComplete)
    {
        BackendResult<IdleAdventureHomeResponse> result = null;
        yield return BackendApiClient.Get<IdleAdventureHomeResponse>(baseUrl, Token, $"idle-adventure/players/{PlayerId}/home", value => result = value);
        if (result != null && result.Success && result.Data != null && result.Data.stamina != null)
        {
            ApplyAuthoritativeStamina(result.Data.stamina);
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator LoadIdleAdventureFormationRoutine(Action<BackendResult<IdleAdventureFormationResponse>> onComplete)
    {
        BackendResult<IdleAdventureFormationResponse> result = null;
        yield return BackendApiClient.Get<IdleAdventureFormationResponse>(baseUrl, Token, $"idle-adventure/players/{PlayerId}/formation", value => result = value);
        onComplete?.Invoke(result);
    }

    private IEnumerator SaveIdleAdventureFormationRoutine(IdleAdventureSaveFormationRequest request, Action<BackendResult<IdleAdventureFormationResponse>> onComplete)
    {
        string json = request != null ? request.ToJson() : "{\"slots\":[]}";
        BackendResult<IdleAdventureFormationResponse> result = null;
        yield return BackendApiClient.Put<IdleAdventureFormationResponse>(baseUrl, Token, $"idle-adventure/players/{PlayerId}/formation", json, value => result = value);
        onComplete?.Invoke(result);
    }

    private IEnumerator AutoFillIdleAdventureFormationRoutine(Action<BackendResult<IdleAdventureFormationResponse>> onComplete)
    {
        BackendResult<IdleAdventureFormationResponse> result = null;
        yield return BackendApiClient.Post<IdleAdventureFormationResponse>(baseUrl, Token, $"idle-adventure/players/{PlayerId}/formation/auto-fill", null, value => result = value);
        onComplete?.Invoke(result);
    }

    private IEnumerator ClearIdleAdventureFormationRoutine(Action<BackendResult<IdleAdventureFormationResponse>> onComplete)
    {
        BackendResult<IdleAdventureFormationResponse> result = null;
        yield return BackendApiClient.Post<IdleAdventureFormationResponse>(baseUrl, Token, $"idle-adventure/players/{PlayerId}/formation/clear", null, value => result = value);
        onComplete?.Invoke(result);
    }

    private IEnumerator StartIdleAdventureSessionRoutine(IdleAdventureStartSessionRequest request, Action<BackendResult<IdleAdventureSessionResponse>> onComplete)
    {
        BackendResult<IdleAdventureSessionResponse> result = null;
        yield return BackendApiClient.Post<IdleAdventureSessionResponse>(baseUrl, Token, $"idle-adventure/players/{PlayerId}/sessions/start", request, value => result = value);
        onComplete?.Invoke(result);
    }

    private IEnumerator LoadIdleAdventureSessionRoutine(string sessionKey, Action<BackendResult<IdleAdventureSessionResponse>> onComplete)
    {
        BackendResult<IdleAdventureSessionResponse> result = null;
        yield return BackendApiClient.Get<IdleAdventureSessionResponse>(baseUrl, Token, $"idle-adventure/players/{PlayerId}/sessions/{sessionKey}", value => result = value);
        onComplete?.Invoke(result);
    }

    private IEnumerator ResolveIdleAdventureRoundRoutine(string sessionKey, int clientRound, Action<BackendResult<IdleAdventureSessionResponse>> onComplete)
    {
        IdleAdventureResolveRoundRequest request = new IdleAdventureResolveRoundRequest { client_round = Mathf.Max(1, clientRound) };
        BackendResult<IdleAdventureSessionResponse> result = null;
        yield return BackendApiClient.Post<IdleAdventureSessionResponse>(baseUrl, Token, $"idle-adventure/players/{PlayerId}/sessions/{sessionKey}/rounds/resolve", request, value => result = value);
        onComplete?.Invoke(result);
    }

    private IEnumerator ApplyIdleAdventureBuffRoutine(string sessionKey, IdleAdventureApplyBuffRequest request, Action<BackendResult<IdleAdventureBuffAppliedResponse>> onComplete)
    {
        BackendResult<IdleAdventureBuffAppliedResponse> result = null;
        yield return BackendApiClient.Post<IdleAdventureBuffAppliedResponse>(baseUrl, Token, $"idle-adventure/players/{PlayerId}/sessions/{sessionKey}/buffs/apply", request, value => result = value);
        onComplete?.Invoke(result);
    }

    private IEnumerator PostIdleAdventureSessionCommandRoutine(string sessionKey, string commandPath, Action<BackendResult<IdleAdventureSessionResponse>> onComplete)
    {
        BackendResult<IdleAdventureSessionResponse> result = null;
        yield return BackendApiClient.Post<IdleAdventureSessionResponse>(baseUrl, Token, $"idle-adventure/players/{PlayerId}/sessions/{sessionKey}/{commandPath}", null, value => result = value);
        if (result != null && result.Success && result.Data != null && result.Data.items != null)
        {
            CurrentInventory = ConvertIdleAdventureInventory(result.Data.items);
            if (CurrentUser == null) CurrentUser = new UserResponse();
            CurrentUser.items = CurrentInventory;
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator LoadIdleAdventureProgressRoutine(Action<BackendResult<IdleAdventureProgressResponse>> onComplete)
    {
        BackendResult<IdleAdventureProgressResponse> result = null;
        yield return BackendApiClient.Get<IdleAdventureProgressResponse>(baseUrl, Token, $"idle-adventure/players/{PlayerId}/progress", value => result = value);
        onComplete?.Invoke(result);
    }

    private IEnumerator CollectIdleAdventureAfkRewardRoutine(Action<BackendResult<IdleAdventureAfkRewardCollectResponse>> onComplete)
    {
        BackendResult<IdleAdventureAfkRewardCollectResponse> result = null;
        yield return BackendApiClient.Post<IdleAdventureAfkRewardCollectResponse>(baseUrl, Token, $"idle-adventure/players/{PlayerId}/afk-reward/collect", null, value => result = value);

        if (result != null && result.Success && result.Data != null)
        {
            CurrentInventory = ConvertIdleAdventureInventory(result.Data.items);
            if (CurrentUser == null) CurrentUser = new UserResponse();
            CurrentUser.items = CurrentInventory;
        }

        onComplete?.Invoke(result);
    }

    private IEnumerator GetAvailableNpcsRoutine(LandSlotController slot, Action<BackendResult<NpcResponse[]>> onComplete)
    {
        BackendResult<NpcResponse[]> result = null;
        yield return BackendApiClient.Get<NpcResponse[]>(baseUrl, Token, AddSceneQuery($"npc/available/{PlayerId}?playerBuildingId={slot.PlayerBuildingId}"), value => result = value);
        onComplete?.Invoke(result);
    }

    private IEnumerator LoadNpcCatalogRoutine(Action<BackendResult<NpcConfigResponse[]>> onComplete)
    {
        BackendResult<NpcConfigResponse[]> result = null;
        yield return BackendApiClient.Get<NpcConfigResponse[]>(baseUrl, Token, "npc/catalog", value => result = value);

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
            sceneId = CurrentSceneId,
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
            sceneId = CurrentSceneId,
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

    private static InventoryItemResponse[] ConvertIdleAdventureInventory(IdleAdventureInventoryItemResponse[] source)
    {
        if (source == null) return null;

        InventoryItemResponse[] converted = new InventoryItemResponse[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            IdleAdventureInventoryItemResponse item = source[i];
            if (item == null) continue;

            converted[i] = new InventoryItemResponse
            {
                itemId = (int)item.item_id,
                itemKey = item.item_key,
                name = string.IsNullOrEmpty(item.display_name) ? item.name : item.display_name,
                itemType = item.item_type,
                icon = item.icon,
                quantity = (int)item.quantity
            };
        }

        return converted;
    }

    private void StoreLogin(LoginResponse response)
    {
        Token = response.token;
        CurrentUser = response.user;
        PlayerId = response.user != null ? response.user.id : 0;
        PlayerPrefs.SetString(TokenKey, Token);
        PlayerPrefs.SetInt(PlayerIdKey, PlayerId);
        PlayerPrefs.SetInt(SessionManagedKey, 1);
        PlayerPrefs.SetInt(HomeLoginStateKey, 1);
        PlayerPrefs.SetString(HomeSessionModeKey, "account");
        PlayerPrefs.SetString(HomeTokenKey, Token);
        PlayerPrefs.SetInt(HomePlayerIdKey, PlayerId);
        PlayerPrefs.SetString(
            HomeTokenExpiresAtKey,
            (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Math.Max(1, response.expiresIn)).ToString());
        observedSessionRevision = IncrementSessionRevision();
        PlayerPrefs.Save();
        if (CurrentUser != null)
        {
            SyncWeChatFriendLevel(CurrentUser.level);
            RefreshLandUnlockState();
        }
        Debug.Log($"Backend login succeeded: playerId={PlayerId}", this);
    }

    private void ClearSession()
    {
        Token = string.Empty;
        PlayerId = 0;
        CurrentUser = null;
        RefreshLandUnlockState();
        CurrentInventory = null;
        NpcCatalog = null;
        PlayerNpcs = null;
        LandScenes = null;
        CurrentSceneId = 0;
        CurrentSceneConfigVersion = 0L;
        CurrentLandPolicyRevision = 0L;
        CurrentStamina = null;
        CurrentWorkState = null;
        PlayerPrefs.DeleteKey(SceneIdKey);
        PlayerPrefs.DeleteKey(TokenKey);
        PlayerPrefs.DeleteKey(PlayerIdKey);
        PlayerPrefs.DeleteKey(HomeTokenKey);
        PlayerPrefs.DeleteKey(HomePlayerIdKey);
        PlayerPrefs.DeleteKey(HomeTokenExpiresAtKey);
        PlayerPrefs.SetInt(HomeLoginStateKey, 0);
        PlayerPrefs.DeleteKey(HomeSessionModeKey);
        PlayerPrefs.SetInt(SessionManagedKey, 0);
        observedSessionRevision = IncrementSessionRevision();
        PlayerPrefs.Save();
    }

    private void RefreshLandUnlockState()
    {
        if (MapManager.Instance == null) return;
        MapManager.Instance.RefreshLandUnlockState(CurrentUser != null ? CurrentUser.level : 1);
    }

    private void RefreshSharedSessionIfNeeded()
    {
        int currentRevision = PlayerPrefs.GetInt(SessionRevisionKey, 0);
        if (currentRevision == observedSessionRevision)
        {
            return;
        }

        observedSessionRevision = currentRevision;
        Token = PlayerPrefs.GetString(TokenKey, string.Empty);
        PlayerId = PlayerPrefs.GetInt(PlayerIdKey, 0);
        CurrentUser = null;
        CurrentInventory = null;
        NpcCatalog = null;
        PlayerNpcs = null;
        LandScenes = null;
        CurrentStamina = null;
        CurrentWorkState = null;
        CurrentSceneId = PlayerPrefs.GetInt(SceneIdKey, 0);
        CurrentSceneConfigVersion = 0L;
        CurrentLandPolicyRevision = 0L;
        nextProductionPollAt = 0f;

        if (IsAuthenticated && !IsConnecting)
        {
            Connect();
        }
    }

    private static int IncrementSessionRevision()
    {
        int nextRevision = PlayerPrefs.GetInt(SessionRevisionKey, 0) + 1;
        PlayerPrefs.SetInt(SessionRevisionKey, nextRevision);
        return nextRevision;
    }

    private static string ResultMessage<T>(BackendResult<T> result)
    {
        if (result == null) return "No backend response.";
        if (!string.IsNullOrEmpty(result.Message))
        {
            return result.Code > 0 ? $"[{result.Code}] {result.Message}" : result.Message;
        }
        return result.Code > 0 ? $"code={result.Code}, HTTP {result.HttpStatus}" : $"HTTP {result.HttpStatus}";
    }

    private static void SyncWeChatFriendLevel(int level)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WJ_WeChatSyncFriendLevel(Mathf.Max(0, level));
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void WJ_WeChatSyncFriendLevel(int level);
#endif
}
