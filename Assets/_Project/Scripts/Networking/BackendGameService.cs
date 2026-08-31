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
    private const string AreaSelectionOwnerPlayerIdKey = "weijun.areaSelection.playerId";
    private const string OpenAreaLandTypeKey = "OpenAreaLandType";
    private const string CurrentAreaLandTypeKey = "CurrentAreaLandType";
    private const string ApiOverrideKey = "ApiBaseUrlOverride";
    private const string ApiEnvironmentVariableName = "WEIJUN_API_BASE_URL";
    private const string ProductionCollectKeyPrefix = "weijun.backend.production.collect.";
    private const string ProductionSnapshotKeyPrefix = "weijun.backend.production.snapshot.";
    private const string AfkCollectKeyPrefix = "weijun.backend.idleAdventure.afkCollect.";
    private const string AfkAccelerateKeyPrefix = "weijun.backend.idleAdventure.afkAccelerate.";
    private const string AfkSnapshotKeyPrefix = "weijun.backend.idleAdventure.afkSnapshot.";
    private const string AfkStageClearKeyPrefix = "weijun.backend.idleAdventure.stageClear.";
    private const string IdleAdventureCommandKeyPrefix = "weijun.backend.idleAdventure.command.";
    private const string IdleAdventureStartKeyPrefix = "weijun.backend.idleAdventure.start.";
    private const string IdleAdventureSessionSnapshotKeyPrefix = "weijun.backend.idleAdventure.sessionSnapshot.";
    private const string IdempotencyContextSuffix = ".context";

    private static readonly string[] AccountSnapshotPreferenceKeys =
    {
        "LoginUsername",
        "PlayerNickname",
        "PlayerNicknameVersion",
        "PlayerNicknameUpdatedTime",
        "PlayerNicknameChangeCount",
        "PlayerNicknameChangeRequired",
        "PlayerAvatarOwnerId",
        "PlayerAvatarUrl",
        "PlayerAvatarSource",
        "PlayerAvatarVersion",
        "PlayerAvatarUpdatedTime",
        "PlayerTotalExperience",
        "PlayerLevelCurveRevision",
        "PlayerCurrentLevelExperience",
        "PlayerRequiredExperience",
        "PlayerExperienceProgressBps",
        "PlayerMaxLevel",
        "PlayerEconomyFrozen",
        "PlayerStamina",
        "ProfileLastSync",
        "weijun.backend.sceneId",
        "ServerMapWidth",
        "ServerMapHeight",
        "ServerLandCount",
        "ServerBuildingCount",
        "ServerMapLastSync",
        "weijun.idleAdventure.highestClearedStage",
        "Notification.DeepLink.Type",
        "Notification.DeepLink.SceneId",
        "Notification.DeepLink.PlayerBuildingId",
        "Notification.DeepLink.PlayerLandId",
        "weijun.npcAcquisition.routeKey",
        "weijun.npcAcquisition.playerId",
        "weijun.npcAcquisition.expiresAt",
        "weijun.npcAcquisition.poolCode",
        "weijun.npcAcquisition.highlightNpcId",
        "weijun.npcAcquisition.taskCode",
        "weijun.npcAcquisition.planCode",
        "weijun.npcAcquisition.modeCode",
        "weijun.npcAcquisition.stage",
        "weijun.npcAcquisition.poolKey",
        "weijun.npcAcquisition.mailId"
    };

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
    public IReadOnlyList<LandConfig> RemoteAvailableLands => remoteAvailableLands;
    public event Action<UserResponse> UserSnapshotChanged;
    public event Action<InventoryItemResponse[]> InventorySnapshotChanged;
    private bool productionPollPending;
    private bool buildingSyncPending;
    private bool sceneSwitchPending;
    private float nextProductionPollAt;
    private int observedSessionRevision;
    private int sceneRequestGeneration;
    private readonly List<LandConfig> remoteAvailableLands = new List<LandConfig>();

    private sealed class SessionRequestSnapshot
    {
        public readonly string Token;
        public readonly int PlayerId;
        public readonly int Revision;

        public SessionRequestSnapshot(string token, int playerId, int revision)
        {
            Token = token;
            PlayerId = playerId;
            Revision = revision;
        }
    }

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
        Action<BackendResult<LandSlotStateResponse>> onComplete)
    {
        if (!IsAuthenticated || slot == null)
        {
            onComplete?.Invoke(new BackendResult<LandSlotStateResponse>(false, 0, "Backend session or slot is missing.", null));
            return null;
        }
        return StartCoroutine(PurchaseLandSlotRoutine(slot, onComplete));
    }

    public Coroutine PurifyLandSlot(
        LandSlotController slot,
        Action<BackendResult<LandSlotStateResponse>> onComplete)
    {
        if (!IsAuthenticated || slot == null)
        {
            onComplete?.Invoke(new BackendResult<LandSlotStateResponse>(false, 0, "Backend session or slot is missing.", null));
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
        return RunPlayerBuildingAction("building/complete", slot, true, onComplete);
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
        string storageKey = ProductionCollectKeyPrefix + PlayerId + ".single." + slot.PlayerBuildingId;
        string snapshotToken = GetOrCreateProductionBuildingSnapshotToken(slot.PlayerBuildingId);
        string persistedKey = GetOrCreatePersistedIdempotencyKey(storageKey, snapshotToken);
        return CollectProduction(slot, persistedKey, onComplete);
    }

    public Coroutine CollectProduction(
        LandSlotController slot,
        string idempotencyKey,
        Action<bool, string> onComplete)
    {
        if (!IsAuthenticated || slot == null)
        {
            onComplete?.Invoke(false, "Backend session or slot is missing.");
            return null;
        }
        if (!Guid.TryParse(idempotencyKey, out _))
        {
            onComplete?.Invoke(false, "A valid idempotency key is required.");
            return null;
        }
        SessionRequestSnapshot session = CaptureSessionRequest();
        string storageKey = ProductionCollectKeyPrefix + session.PlayerId + ".single." + slot.PlayerBuildingId;
        SavePersistedIdempotencyKey(
            storageKey,
            idempotencyKey,
            GetOrCreateProductionBuildingSnapshotToken(slot.PlayerBuildingId));
        return StartCoroutine(CollectProductionRoutine(slot, idempotencyKey, storageKey, session, CurrentSceneId, onComplete));
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
        return CollectAllProductions(CurrentSceneId, onComplete);
    }

    public Coroutine CollectAllProductions(string idempotencyKey, Action<bool, string, int> onComplete)
    {
        return CollectAllProductions(CurrentSceneId, idempotencyKey, onComplete);
    }

    public Coroutine CollectAllProductions(int sceneId, Action<bool, string, int> onComplete)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(false, "Backend session is not authenticated.", 0);
            return null;
        }
        string storageKey = BuildProductionCollectAllStorageKey(sceneId);
        string snapshotToken = GetOrCreateProductionSceneSnapshotToken(sceneId);
        string persistedKey = GetOrCreatePersistedIdempotencyKey(storageKey, snapshotToken);
        return CollectAllProductions(sceneId, persistedKey, onComplete);
    }

    public Coroutine CollectAllProductions(int sceneId, string idempotencyKey, Action<bool, string, int> onComplete)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(false, "Backend session is not authenticated.", 0);
            return null;
        }
        if (!Guid.TryParse(idempotencyKey, out _))
        {
            onComplete?.Invoke(false, "A valid idempotency key is required.", 0);
            return null;
        }
        SessionRequestSnapshot session = CaptureSessionRequest();
        string storageKey = BuildProductionCollectAllStorageKey(sceneId);
        SavePersistedIdempotencyKey(
            storageKey,
            idempotencyKey,
            GetOrCreateProductionSceneSnapshotToken(sceneId));
        return StartCoroutine(CollectAllProductionsRoutine(sceneId, idempotencyKey, storageKey, session, onComplete));
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

        if (request == null)
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureSessionResponse>(false, 0, "Idle Adventure start request is missing.", null));
            return null;
        }

        SessionRequestSnapshot session = CaptureSessionRequest();
        string storageKey = BuildIdleAdventureStartStorageKey(session.PlayerId);
        string requestContext = BuildIdleAdventureStartRequestContext(request);
        string idempotencyKey = GetOrCreatePersistedIdempotencyKey(storageKey, requestContext);
        return StartCoroutine(StartIdleAdventureSessionRoutine(
            request,
            idempotencyKey,
            storageKey,
            session,
            onComplete));
    }

    public Coroutine LoadIdleAdventureSession(string sessionKey, Action<BackendResult<IdleAdventureSessionResponse>> onComplete = null)
    {
        if (!IsAuthenticated || string.IsNullOrEmpty(sessionKey))
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureSessionResponse>(false, 0, "Backend session or session key is missing.", null));
            return null;
        }

        return StartCoroutine(LoadIdleAdventureSessionRoutine(sessionKey, CaptureSessionRequest(), onComplete));
    }

    public Coroutine ResolveIdleAdventureRound(string sessionKey, int clientRound, Action<BackendResult<IdleAdventureSessionResponse>> onComplete = null)
    {
        if (!IsAuthenticated || string.IsNullOrEmpty(sessionKey))
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureSessionResponse>(false, 0, "Backend session or session key is missing.", null));
            return null;
        }

        int normalizedRound = Mathf.Max(1, clientRound);
        SessionRequestSnapshot session = CaptureSessionRequest();
        string storageKey = BuildIdleAdventureCommandStorageKey(
            session.PlayerId,
            "round",
            sessionKey,
            normalizedRound.ToString());
        string snapshotToken = GetOrCreateIdleAdventureSessionSnapshotToken(session.PlayerId, sessionKey);
        string idempotencyKey = GetOrCreatePersistedIdempotencyKey(storageKey, snapshotToken);
        return StartCoroutine(ResolveIdleAdventureRoundRoutine(
            sessionKey,
            normalizedRound,
            idempotencyKey,
            storageKey,
            session,
            onComplete));
    }

    public Coroutine ApplyIdleAdventureBuff(string sessionKey, IdleAdventureApplyBuffRequest request, Action<BackendResult<IdleAdventureBuffAppliedResponse>> onComplete = null)
    {
        if (!IsAuthenticated || string.IsNullOrEmpty(sessionKey) || request == null)
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureBuffAppliedResponse>(false, 0, "Backend session, session key, or buff request is missing.", null));
            return null;
        }

        string discriminator = (request.target_side ?? string.Empty).Trim().ToUpperInvariant()
            + "." + request.slot_index
            + "." + request.buff_preset_id;
        SessionRequestSnapshot session = CaptureSessionRequest();
        string storageKey = BuildIdleAdventureCommandStorageKey(
            session.PlayerId,
            "buff",
            sessionKey,
            discriminator);
        string snapshotToken = GetOrCreateIdleAdventureSessionSnapshotToken(session.PlayerId, sessionKey);
        string idempotencyKey = GetOrCreatePersistedIdempotencyKey(storageKey, snapshotToken);
        return StartCoroutine(ApplyIdleAdventureBuffRoutine(
            sessionKey,
            request,
            idempotencyKey,
            storageKey,
            session,
            onComplete));
    }

    public Coroutine ContinueIdleAdventureStage(string sessionKey, Action<BackendResult<IdleAdventureSessionResponse>> onComplete = null)
    {
        if (!IsAuthenticated || string.IsNullOrEmpty(sessionKey))
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureSessionResponse>(false, 0, "Backend session or session key is missing.", null));
            return null;
        }

        return StartCoroutine(PostIdleAdventureSessionCommandRoutine(
            sessionKey,
            "stages/continue",
            "continue",
            CaptureSessionRequest(),
            onComplete));
    }

    public Coroutine RetryIdleAdventureStage(string sessionKey, Action<BackendResult<IdleAdventureSessionResponse>> onComplete = null)
    {
        if (!IsAuthenticated || string.IsNullOrEmpty(sessionKey))
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureSessionResponse>(false, 0, "Backend session or session key is missing.", null));
            return null;
        }

        return StartCoroutine(PostIdleAdventureSessionCommandRoutine(
            sessionKey,
            "stages/retry",
            "retry",
            CaptureSessionRequest(),
            onComplete));
    }

    public Coroutine PauseIdleAdventureSession(string sessionKey, Action<BackendResult<IdleAdventureSessionResponse>> onComplete = null)
    {
        if (!IsAuthenticated || string.IsNullOrEmpty(sessionKey))
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureSessionResponse>(false, 0, "Backend session or session key is missing.", null));
            return null;
        }

        return StartCoroutine(PostIdleAdventureSessionCommandRoutine(
            sessionKey,
            "pause",
            "pause",
            CaptureSessionRequest(),
            onComplete));
    }

    public Coroutine ResumeIdleAdventureSession(string sessionKey, Action<BackendResult<IdleAdventureSessionResponse>> onComplete = null)
    {
        if (!IsAuthenticated || string.IsNullOrEmpty(sessionKey))
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureSessionResponse>(false, 0, "Backend session or session key is missing.", null));
            return null;
        }

        return StartCoroutine(PostIdleAdventureSessionCommandRoutine(
            sessionKey,
            "resume",
            "resume",
            CaptureSessionRequest(),
            onComplete));
    }

    public Coroutine EndIdleAdventureSession(string sessionKey, Action<BackendResult<IdleAdventureSessionResponse>> onComplete = null)
    {
        if (!IsAuthenticated || string.IsNullOrEmpty(sessionKey))
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureSessionResponse>(false, 0, "Backend session or session key is missing.", null));
            return null;
        }

        return StartCoroutine(PostIdleAdventureSessionCommandRoutine(
            sessionKey,
            "end",
            "end",
            CaptureSessionRequest(),
            onComplete));
    }

    public Coroutine LoadIdleAdventureProgress(Action<BackendResult<IdleAdventureProgressResponse>> onComplete = null)
    {
        return RefreshIdleAdventureProgress(onComplete);
    }

    public Coroutine CollectIdleAdventureAfkReward(Action<BackendResult<IdleAdventureAfkRewardCollectResponse>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureAfkRewardCollectResponse>(false, 0, "Backend session is not authenticated.", null));
            return null;
        }
        string storageKey = AfkCollectKeyPrefix + PlayerId;
        string snapshotToken = GetOrCreateIdleAdventureAfkSnapshotToken();
        string key = GetOrCreatePersistedIdempotencyKey(storageKey, snapshotToken);
        return CollectIdleAdventureAfkReward(key, onComplete);
    }

    public Coroutine CollectIdleAdventureAfkReward(
        string idempotencyKey,
        Action<BackendResult<IdleAdventureAfkRewardCollectResponse>> onComplete = null)
    {
        if (!IsAuthenticated)
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureAfkRewardCollectResponse>(false, 0, "Backend session is not authenticated.", null));
            return null;
        }
        if (!Guid.TryParse(idempotencyKey, out _))
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureAfkRewardCollectResponse>(false, 0, "A valid idempotency key is required.", null));
            return null;
        }
        SessionRequestSnapshot session = CaptureSessionRequest();
        string storageKey = AfkCollectKeyPrefix + session.PlayerId;
        SavePersistedIdempotencyKey(
            storageKey,
            idempotencyKey,
            GetOrCreateIdleAdventureAfkSnapshotToken());

        return StartCoroutine(CollectIdleAdventureAfkRewardRoutine(idempotencyKey, storageKey, session, onComplete));
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
        yield return RefreshIdleAdventureProgress(null);
        ScheduleNextProductionPoll();
    }

    private IEnumerator RequestUserRoutine(Action<bool> onComplete)
    {
        string requestToken = Token;
        int requestPlayerId = PlayerId;
        int requestSessionRevision = observedSessionRevision;
        BackendResult<UserResponse> result = null;
        yield return BackendApiClient.Get<UserResponse>(baseUrl, requestToken, "user/me", value => result = value);

        if (!IsSessionRequestCurrent(requestToken, requestPlayerId, requestSessionRevision))
        {
            onComplete?.Invoke(false);
            yield break;
        }

        bool success = result != null && result.Success && result.Data != null;
        if (success)
        {
            ApplyUserSnapshot(result.Data);
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
        string requestToken = Token;
        int requestPlayerId = PlayerId;
        int requestSessionRevision = observedSessionRevision;
        BackendResult<InventoryItemResponse[]> result = null;
        yield return BackendApiClient.Get<InventoryItemResponse[]>(
            baseUrl,
            requestToken,
            $"inventory/list/{requestPlayerId}",
            value => result = value);

        if (!IsSessionRequestCurrent(requestToken, requestPlayerId, requestSessionRevision))
        {
            onComplete?.Invoke(new BackendResult<InventoryItemResponse[]>(
                false,
                0,
                "Backend session changed while inventory was loading.",
                null));
            yield break;
        }

        if (result != null && result.Success && result.Data != null)
        {
            ApplyInventorySnapshot(result.Data);
        }
        else if (result != null && result.HttpStatus == 401)
        {
            ClearSession();
        }
        else
        {
            Debug.LogWarning($"Backend inventory refresh failed: {ResultMessage(result)}", this);
        }

        onComplete?.Invoke(result);
    }

    private bool IsSessionRequestCurrent(string requestToken, int requestPlayerId, int requestSessionRevision)
    {
        return requestSessionRevision == observedSessionRevision
            && requestSessionRevision == PlayerPrefs.GetInt(SessionRevisionKey, 0)
            && requestPlayerId == PlayerId
            && string.Equals(requestToken, Token, StringComparison.Ordinal);
    }

    private SessionRequestSnapshot CaptureSessionRequest()
    {
        return new SessionRequestSnapshot(Token, PlayerId, observedSessionRevision);
    }

    private bool IsSessionRequestCurrent(SessionRequestSnapshot request)
    {
        return request != null
            && IsSessionRequestCurrent(request.Token, request.PlayerId, request.Revision);
    }

    private int AdvanceSceneRequestGeneration()
    {
        sceneRequestGeneration = sceneRequestGeneration == int.MaxValue
            ? 1
            : sceneRequestGeneration + 1;
        return sceneRequestGeneration;
    }

    private bool IsSceneRequestGenerationCurrent(int requestGeneration)
    {
        return requestGeneration == sceneRequestGeneration;
    }

    private bool IsSceneRequestCurrent(int expectedSceneId, int requestGeneration)
    {
        return IsSceneRequestGenerationCurrent(requestGeneration)
            && (expectedSceneId <= 0 || CurrentSceneId == expectedSceneId);
    }

    private IEnumerator LoadAvailableBuildingsRoutine()
    {
        SessionRequestSnapshot session = CaptureSessionRequest();
        BackendResult<BuildingConfigResponse[]> result = null;
        yield return BackendApiClient.Get<BuildingConfigResponse[]>(baseUrl, session.Token, $"building/available/{session.PlayerId}", value => result = value);

        if (!IsSessionRequestCurrent(session)) yield break;

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
        SessionRequestSnapshot session = CaptureSessionRequest();
        BackendResult<LandConfigResponse[]> result = null;
        yield return BackendApiClient.Get<LandConfigResponse[]>(baseUrl, session.Token, $"land/available/{session.PlayerId}", value => result = value);

        if (!IsSessionRequestCurrent(session)) yield break;

        remoteAvailableLands.Clear();
        if (result == null || !result.Success || result.Data == null)
        {
            Debug.LogWarning($"Backend available lands failed: {ResultMessage(result)}", this);
            ApplyRemoteLandDragItems();
            yield break;
        }

        int placeableCount = 0;
        for (int i = 0; i < result.Data.Length; i++)
        {
            LandConfigResponse response = result.Data[i];
            if (response == null) continue;

            LandConfig config = RuntimeConfigFactory.ResolveLand(response);
            if (config == null) continue;

            remoteAvailableLands.Add(config);
            if (response.canPlace) placeableCount++;
        }

        Debug.Log($"Backend available lands loaded: total={remoteAvailableLands.Count}, placeable={placeableCount}.", this);
        ApplyRemoteLandDragItems();
    }

    private void ApplyRemoteLandDragItems()
    {
        BuildPanelController panel = FindFirstObjectByType<BuildPanelController>();
        if (panel == null) return;
        if (CurrentUser == null)
        {
            panel.SetRemoteAvailableLands(null);
            return;
        }

        int playerLevel = Mathf.Max(1, CurrentUser.level);
        List<LandConfig> unlockedLands = new List<LandConfig>();
        for (int i = 0; i < remoteAvailableLands.Count; i++)
        {
            LandConfig config = remoteAvailableLands[i];
            if (config != null && config.unlockLevel <= playerLevel) unlockedLands.Add(config);
        }

        panel.SetRemoteAvailableLands(unlockedLands);
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
        yield return BackendApiClient.Get<BuildingResponse[]>(baseUrl, Token, AddSceneQuery($"building/list/{PlayerId}"), value => result = value);

        if (result != null && result.Success && result.Data != null)
        {
            ApplyBuildingExperienceGrants(result.Data);
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
            ApplyBuildingExperienceGrant(result.Data);
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
        SessionRequestSnapshot session = CaptureSessionRequest();
        int sceneId = CurrentSceneId;
        string path = $"building/sync/{session.PlayerId}";
        if (sceneId > 0) path += "?sceneId=" + sceneId;
        BackendResult<BuildingResponse[]> result = null;
        yield return BackendApiClient.Post<BuildingResponse[]>(baseUrl, session.Token, path, null, value => result = value);
        buildingSyncPending = false;
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(new BackendResult<BuildingResponse[]>(false, 0, "Backend session changed while buildings were syncing.", null));
            yield break;
        }

        if (result != null && result.Success && result.Data != null)
        {
            ApplyBuildingExperienceGrants(result.Data);
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
        SessionRequestSnapshot session = CaptureSessionRequest();
        int sceneId = CurrentSceneId;
        int requestSceneGeneration = sceneRequestGeneration;
        string path = $"land/map/{session.PlayerId}";
        if (sceneId > 0) path += "?sceneId=" + sceneId;
        BackendResult<MapDataResponse> result = null;
        yield return BackendApiClient.Get<MapDataResponse>(baseUrl, session.Token, path, value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(false, "Backend session changed while the land map was syncing.");
            yield break;
        }
        if (!IsSceneRequestCurrent(sceneId, requestSceneGeneration))
        {
            onComplete?.Invoke(false, "Land map response was superseded by a newer scene request.");
            yield break;
        }

        bool success = result != null && result.Success && result.Data != null;
        if (success)
        {
            if (sceneId > 0 && result.Data.sceneId > 0 && result.Data.sceneId != sceneId)
            {
                onComplete?.Invoke(false, "Land map response did not match the requested scene.");
                yield break;
            }
            int resolvedSceneId = result.Data.sceneId > 0 ? result.Data.sceneId : CurrentSceneId;
            if (resolvedSceneId != CurrentSceneId)
            {
                CurrentSceneId = resolvedSceneId;
                AdvanceSceneRequestGeneration();
            }
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
        SessionRequestSnapshot session = CaptureSessionRequest();
        int sceneId = CurrentSceneId;
        int requestSceneGeneration = sceneRequestGeneration;
        string path = $"production/list/{session.PlayerId}";
        if (sceneId > 0) path += "?sceneId=" + sceneId;
        BackendResult<ProductionResponse[]> result = null;
        yield return BackendApiClient.Get<ProductionResponse[]>(baseUrl, session.Token, path, value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(false, "Backend session changed while productions were refreshing.");
            yield break;
        }
        if (!IsSceneRequestCurrent(sceneId, requestSceneGeneration))
        {
            onComplete?.Invoke(false, "Production response was superseded by a newer scene request.");
            yield break;
        }

        bool success = result != null && result.Success;
        if (success)
        {
            RememberProductionSceneSnapshot(result.Data, sceneId);
            RememberProductionBuildingSnapshots(result.Data);
            if (MapManager.Instance != null)
            {
                MapManager.Instance.ApplyRemoteProductionSnapshotForScene(
                    result.Data ?? new ProductionResponse[0],
                    sceneId);
            }
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
        SessionRequestSnapshot session = CaptureSessionRequest();
        int sceneId = CurrentSceneId;
        int requestSceneGeneration = sceneRequestGeneration;
        int playerBuildingId = slot.PlayerBuildingId;
        string path = $"production/detail/{session.PlayerId}/{playerBuildingId}";
        BackendResult<ProductionResponse[]> result = null;
        yield return BackendApiClient.Get<ProductionResponse[]>(baseUrl, session.Token, path, value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(new BackendResult<ProductionResponse[]>(false, 0, "Backend session changed while production detail was loading.", null));
            yield break;
        }
        if (!IsSceneRequestCurrent(sceneId, requestSceneGeneration))
        {
            onComplete?.Invoke(new BackendResult<ProductionResponse[]>(false, 0, "Production detail response was superseded by a newer scene request.", null));
            yield break;
        }

        if (result != null && result.Success)
        {
            ProductionResponse[] productions = result.Data ?? new ProductionResponse[0];
            RememberProductionBuildingSnapshot(playerBuildingId, productions);
            if (MapManager.Instance != null)
            {
                MapManager.Instance.ApplyRemoteProductionSnapshotForBuilding(playerBuildingId, productions);
            }
            if (result.Data == null)
            {
                result = new BackendResult<ProductionResponse[]>(
                    true,
                    result.HttpStatus,
                    result.Code,
                    result.Message,
                    productions);
            }
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
            buildingId = config.buildingId,
            playerLandId = slot.PlayerLandId
        };

        BackendResult<BuildingResponse> result = null;
        yield return BackendApiClient.Post<BuildingResponse>(baseUrl, Token, "building/build", request, value => result = value);

        bool success = result != null && result.Success && result.Data != null;
        if (success)
        {
            ApplyBuildingExperienceGrant(result.Data);
            slot.ApplyRemoteBuilding(RuntimeConfigFactory.ResolveBuilding(result.Data), result.Data);
            yield return RequestUserRoutine(null);
            yield return LoadAvailableBuildingsRoutine();
        }
        else if (result != null && result.Code == 1009)
        {
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
            if (result.Data != null)
            {
                ApplyBuildingExperienceGrant(result.Data);
                slot.ApplyRemoteBuilding(RuntimeConfigFactory.ResolveBuilding(result.Data), result.Data);
            }
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
            yield return RefreshUserAndInventoryAfterMutation();
        }

        onComplete?.Invoke(success, ResultMessage(result));
    }

    private IEnumerator MoveBuildingRoutine(LandSlotController source, LandSlotController target, Action<bool, string> onComplete)
    {
        MoveBuildingRequest request = new MoveBuildingRequest
        {
            playerId = PlayerId,
            playerBuildingId = source.PlayerBuildingId,
            targetPlayerLandId = target.PlayerLandId
        };

        BackendResult<BuildingResponse> result = null;
        yield return BackendApiClient.Post<BuildingResponse>(baseUrl, Token, "building/move", request, value => result = value);

        bool success = result != null && result.Success;
        if (success)
        {
            yield return SyncMapRoutine(null);
            yield return RefreshUserAndInventoryAfterMutation();
        }

        onComplete?.Invoke(success, ResultMessage(result));
    }

    private IEnumerator CollectProductionRoutine(
        LandSlotController slot,
        string idempotencyKey,
        string storageKey,
        SessionRequestSnapshot session,
        int sceneId,
        Action<bool, string> onComplete)
    {
        int requestSceneGeneration = sceneRequestGeneration;
        PlayerBuildingRequest request = new PlayerBuildingRequest
        {
            playerId = session.PlayerId,
            playerBuildingId = slot.PlayerBuildingId
        };

        BackendResult<ProductionCollectResponse> result = null;
        yield return BackendApiClient.Post<ProductionCollectResponse>(baseUrl, session.Token, "production/collect", request, idempotencyKey, value => result = value);

        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(false, "Backend session changed while production was being collected.");
            yield break;
        }
        if (!IsSceneRequestCurrent(sceneId, requestSceneGeneration))
        {
            onComplete?.Invoke(false, "Production collect response was superseded by a newer scene request.");
            yield break;
        }

        bool success = result != null && result.Success && result.Data != null;
        if (success)
        {
            if (!DeletePersistedIdempotencyKey(storageKey, idempotencyKey))
            {
                onComplete?.Invoke(false, "Production collect response was superseded by a newer request.");
                yield break;
            }
            ApplyProductionCollect(result.Data, slot.PlayerBuildingId);
            yield return RefreshUserAndInventoryAfterMutation();
            if (!IsSessionRequestCurrent(session))
            {
                onComplete?.Invoke(false, "Backend session changed while production state was refreshing.");
                yield break;
            }
            yield return LoadAvailableLandsRoutine();
            if (!IsSessionRequestCurrent(session))
            {
                onComplete?.Invoke(false, "Backend session changed while available lands were refreshing.");
                yield break;
            }
            yield return LoadAvailableBuildingsRoutine();
            if (!IsSessionRequestCurrent(session))
            {
                onComplete?.Invoke(false, "Backend session changed while available buildings were refreshing.");
                yield break;
            }
        }
        onComplete?.Invoke(success, ResultMessage(result));
    }

    private IEnumerator PurchaseLandSlotRoutine(
        LandSlotController slot,
        Action<BackendResult<LandSlotStateResponse>> onComplete)
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

        onComplete?.Invoke(result);
    }

    private IEnumerator PurifyLandSlotRoutine(
        LandSlotController slot,
        Action<BackendResult<LandSlotStateResponse>> onComplete)
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
            "land/slot/purify",
            request,
            value => result = value);

        bool success = result != null && result.Success && result.Data != null;
        if (success)
        {
            slot.ApplyRemoteUnlockState(result.Data);
            yield return RefreshUserAndInventoryAfterMutation();
            yield return SyncMapRoutine(null);
        }

        onComplete?.Invoke(result);
    }

    private IEnumerator CollectAllProductionsRoutine(
        int sceneId,
        string idempotencyKey,
        string storageKey,
        SessionRequestSnapshot session,
        Action<bool, string, int> onComplete)
    {
        int requestSceneGeneration = sceneRequestGeneration;
        string request = sceneId > 0
            ? "{\"playerId\":" + session.PlayerId + ",\"sceneId\":" + sceneId + "}"
            : "{\"playerId\":" + session.PlayerId + "}";
        BackendResult<ProductionCollectResponse> result = null;
        yield return BackendApiClient.Post<ProductionCollectResponse>(baseUrl, session.Token, "production/collect-all", request, idempotencyKey, value => result = value);

        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(false, "Backend session changed while productions were being collected.", 0);
            yield break;
        }
        if (!IsSceneRequestCurrent(sceneId, requestSceneGeneration))
        {
            onComplete?.Invoke(false, "Production collect-all response was superseded by a newer scene request.", 0);
            yield break;
        }

        bool success = result != null && result.Success && result.Data != null;
        int collectedCount = 0;
        if (success)
        {
            if (!DeletePersistedIdempotencyKey(storageKey, idempotencyKey))
            {
                onComplete?.Invoke(false, "Production collect-all response was superseded by a newer request.", 0);
                yield break;
            }
            collectedCount = result.Data.collectedItems != null ? result.Data.collectedItems.Length : 0;
            if (MapManager.Instance != null) MapManager.Instance.ClearReadyRemoteProductions();
            ApplyProductionCollect(result.Data);
            yield return RefreshUserAndInventoryAfterMutation();
            if (!IsSessionRequestCurrent(session))
            {
                onComplete?.Invoke(false, "Backend session changed while production state was refreshing.", 0);
                yield break;
            }
            yield return LoadAvailableLandsRoutine();
            if (!IsSessionRequestCurrent(session))
            {
                onComplete?.Invoke(false, "Backend session changed while available lands were refreshing.", 0);
                yield break;
            }
            yield return LoadAvailableBuildingsRoutine();
            if (!IsSessionRequestCurrent(session))
            {
                onComplete?.Invoke(false, "Backend session changed while available buildings were refreshing.", 0);
                yield break;
            }
        }

        onComplete?.Invoke(success, ResultMessage(result), collectedCount);
    }

    private IEnumerator LoadIdleAdventureAfkRewardRoutine(Action<BackendResult<IdleAdventureAfkRewardStateResponse>> onComplete)
    {
        SessionRequestSnapshot session = CaptureSessionRequest();
        BackendResult<IdleAdventureAfkRewardStateResponse> result = null;
        yield return BackendApiClient.Get<IdleAdventureAfkRewardStateResponse>(baseUrl, session.Token, $"idle-adventure/players/{session.PlayerId}/afk-reward", value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureAfkRewardStateResponse>(false, 0, "Backend session changed while AFK state was loading.", null));
            yield break;
        }
        if (result != null && result.Success && result.Data != null)
        {
            RememberIdleAdventureAfkSnapshot(result.Data);
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator LoadIdleAdventureHomeRoutine(Action<BackendResult<IdleAdventureHomeResponse>> onComplete)
    {
        SessionRequestSnapshot session = CaptureSessionRequest();
        BackendResult<IdleAdventureHomeResponse> result = null;
        yield return BackendApiClient.Get<IdleAdventureHomeResponse>(baseUrl, session.Token, $"idle-adventure/players/{session.PlayerId}/home", value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureHomeResponse>(false, 0, "Backend session changed while adventure home was loading.", null));
            yield break;
        }
        if (result != null && result.Success && result.Data != null && result.Data.stamina != null)
        {
            ApplyAuthoritativeStamina(result.Data.stamina);
        }
        if (result != null && result.Success && result.Data != null)
        {
            IdleAdventureAfkRewardStateResponse afkState = result.Data.ResolvedAfkReward;
            if (afkState == null)
            {
                BackendResult<IdleAdventureAfkRewardStateResponse> afkResult = null;
                yield return BackendApiClient.Get<IdleAdventureAfkRewardStateResponse>(
                    baseUrl,
                    session.Token,
                    $"idle-adventure/players/{session.PlayerId}/afk-reward",
                    value => afkResult = value);
                if (!IsSessionRequestCurrent(session))
                {
                    onComplete?.Invoke(new BackendResult<IdleAdventureHomeResponse>(false, 0, "Backend session changed while adventure home was reconciling.", null));
                    yield break;
                }
                if (afkResult != null && afkResult.Success) afkState = afkResult.Data;
            }
            RememberIdleAdventureAfkSnapshot(afkState);
            ApplyIdleAdventureAfkStateToSources(afkState);
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator LoadIdleAdventureFormationRoutine(Action<BackendResult<IdleAdventureFormationResponse>> onComplete)
    {
        SessionRequestSnapshot session = CaptureSessionRequest();
        BackendResult<IdleAdventureFormationResponse> result = null;
        yield return BackendApiClient.Get<IdleAdventureFormationResponse>(
            baseUrl,
            session.Token,
            $"idle-adventure/players/{session.PlayerId}/formation",
            value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureFormationResponse>(false, 0, "Backend session changed while adventure formation was loading.", null));
            yield break;
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator SaveIdleAdventureFormationRoutine(IdleAdventureSaveFormationRequest request, Action<BackendResult<IdleAdventureFormationResponse>> onComplete)
    {
        SessionRequestSnapshot session = CaptureSessionRequest();
        string json = request != null ? request.ToJson() : "{\"slots\":[]}";
        BackendResult<IdleAdventureFormationResponse> result = null;
        yield return BackendApiClient.Put<IdleAdventureFormationResponse>(
            baseUrl,
            session.Token,
            $"idle-adventure/players/{session.PlayerId}/formation",
            json,
            value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureFormationResponse>(false, 0, "Backend session changed while adventure formation was saving.", null));
            yield break;
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator AutoFillIdleAdventureFormationRoutine(Action<BackendResult<IdleAdventureFormationResponse>> onComplete)
    {
        SessionRequestSnapshot session = CaptureSessionRequest();
        BackendResult<IdleAdventureFormationResponse> result = null;
        yield return BackendApiClient.Post<IdleAdventureFormationResponse>(
            baseUrl,
            session.Token,
            $"idle-adventure/players/{session.PlayerId}/formation/auto-fill",
            null,
            value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureFormationResponse>(false, 0, "Backend session changed while adventure formation was auto-filling.", null));
            yield break;
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator ClearIdleAdventureFormationRoutine(Action<BackendResult<IdleAdventureFormationResponse>> onComplete)
    {
        SessionRequestSnapshot session = CaptureSessionRequest();
        BackendResult<IdleAdventureFormationResponse> result = null;
        yield return BackendApiClient.Post<IdleAdventureFormationResponse>(
            baseUrl,
            session.Token,
            $"idle-adventure/players/{session.PlayerId}/formation/clear",
            null,
            value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureFormationResponse>(false, 0, "Backend session changed while adventure formation was clearing.", null));
            yield break;
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator StartIdleAdventureSessionRoutine(
        IdleAdventureStartSessionRequest request,
        string idempotencyKey,
        string storageKey,
        SessionRequestSnapshot session,
        Action<BackendResult<IdleAdventureSessionResponse>> onComplete)
    {
        BackendResult<IdleAdventureSessionResponse> result = null;
        yield return BackendApiClient.Post<IdleAdventureSessionResponse>(
            baseUrl,
            session.Token,
            $"idle-adventure/players/{session.PlayerId}/sessions/start",
            request,
            idempotencyKey,
            value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureSessionResponse>(false, 0, "Backend session changed while an adventure session was starting.", null));
            yield break;
        }
        if (result != null && result.Success && result.Data != null)
        {
            DeletePersistedIdempotencyKey(storageKey, idempotencyKey);
            RememberIdleAdventureSessionSnapshot(session.PlayerId, result.Data.session_key, result.Data);
        }
        else if (ShouldDiscardIdleAdventureStartKey(result))
        {
            DeletePersistedIdempotencyKey(storageKey, idempotencyKey);
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator LoadIdleAdventureSessionRoutine(
        string sessionKey,
        SessionRequestSnapshot session,
        Action<BackendResult<IdleAdventureSessionResponse>> onComplete)
    {
        BackendResult<IdleAdventureSessionResponse> result = null;
        yield return BackendApiClient.Get<IdleAdventureSessionResponse>(baseUrl, session.Token, $"idle-adventure/players/{session.PlayerId}/sessions/{sessionKey}", value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureSessionResponse>(false, 0, "Backend session changed while adventure state was loading.", null));
            yield break;
        }
        if (result != null && result.Success && result.Data != null)
        {
            RememberIdleAdventureSessionSnapshot(session.PlayerId, sessionKey, result.Data);
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator ResolveIdleAdventureRoundRoutine(
        string sessionKey,
        int clientRound,
        string idempotencyKey,
        string storageKey,
        SessionRequestSnapshot session,
        Action<BackendResult<IdleAdventureSessionResponse>> onComplete)
    {
        IdleAdventureResolveRoundRequest request = new IdleAdventureResolveRoundRequest { client_round = Mathf.Max(1, clientRound) };
        BackendResult<IdleAdventureSessionResponse> result = null;
        yield return BackendApiClient.Post<IdleAdventureSessionResponse>(
            baseUrl,
            session.Token,
            $"idle-adventure/players/{session.PlayerId}/sessions/{sessionKey}/rounds/resolve",
            request,
            idempotencyKey,
            value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureSessionResponse>(false, 0, "Backend session changed while the adventure round was resolving.", null));
            yield break;
        }
        if (result != null && result.Success && result.Data != null)
        {
            if (!DeletePersistedIdempotencyKey(storageKey, idempotencyKey))
            {
                onComplete?.Invoke(new BackendResult<IdleAdventureSessionResponse>(false, 0, "Adventure round response was superseded by a newer request.", null));
                yield break;
            }
            RememberIdleAdventureSessionSnapshot(session.PlayerId, sessionKey, result.Data);
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator ApplyIdleAdventureBuffRoutine(
        string sessionKey,
        IdleAdventureApplyBuffRequest request,
        string idempotencyKey,
        string storageKey,
        SessionRequestSnapshot session,
        Action<BackendResult<IdleAdventureBuffAppliedResponse>> onComplete)
    {
        BackendResult<IdleAdventureBuffAppliedResponse> result = null;
        yield return BackendApiClient.Post<IdleAdventureBuffAppliedResponse>(
            baseUrl,
            session.Token,
            $"idle-adventure/players/{session.PlayerId}/sessions/{sessionKey}/buffs/apply",
            request,
            idempotencyKey,
            value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureBuffAppliedResponse>(false, 0, "Backend session changed while the adventure buff was applying.", null));
            yield break;
        }
        if (result != null && result.Success && result.Data != null)
        {
            if (!DeletePersistedIdempotencyKey(storageKey, idempotencyKey))
            {
                onComplete?.Invoke(new BackendResult<IdleAdventureBuffAppliedResponse>(false, 0, "Adventure buff response was superseded by a newer request.", null));
                yield break;
            }
            RotateIdleAdventureSessionSnapshotToken(session.PlayerId, sessionKey);
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator PostIdleAdventureSessionCommandRoutine(
        string sessionKey,
        string commandPath,
        string operation,
        SessionRequestSnapshot session,
        Action<BackendResult<IdleAdventureSessionResponse>> onComplete)
    {
        string storageKey = BuildIdleAdventureCommandStorageKey(session.PlayerId, operation, sessionKey, string.Empty);
        string snapshotToken = GetOrCreateIdleAdventureSessionSnapshotToken(session.PlayerId, sessionKey);
        string idempotencyKey = string.Equals(operation, "end", StringComparison.Ordinal)
            ? GetOrCreatePersistedIdempotencyKey(storageKey)
            : GetOrCreatePersistedIdempotencyKey(storageKey, snapshotToken);
        BackendResult<IdleAdventureSessionResponse> result = null;
        yield return BackendApiClient.Post<IdleAdventureSessionResponse>(
            baseUrl,
            session.Token,
            $"idle-adventure/players/{session.PlayerId}/sessions/{sessionKey}/{commandPath}",
            null,
            idempotencyKey,
            value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureSessionResponse>(false, 0, "Backend session changed while the adventure command was running.", null));
            yield break;
        }
        if (result != null && result.Success && result.Data != null)
        {
            if (!DeletePersistedIdempotencyKey(storageKey, idempotencyKey))
            {
                onComplete?.Invoke(new BackendResult<IdleAdventureSessionResponse>(false, 0, "Adventure command response was superseded by a newer request.", null));
                yield break;
            }
            RememberIdleAdventureSessionSnapshot(session.PlayerId, sessionKey, result.Data);
        }
        if (result != null && result.Success && result.Data != null && result.Data.items != null)
        {
            ApplyInventorySnapshot(ConvertIdleAdventureInventory(result.Data.items));
            yield return RefreshUserAndInventoryAfterMutation();
            if (!IsSessionRequestCurrent(session))
            {
                onComplete?.Invoke(new BackendResult<IdleAdventureSessionResponse>(false, 0, "Backend session changed while adventure rewards were refreshing.", null));
                yield break;
            }
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator LoadIdleAdventureProgressRoutine(Action<BackendResult<IdleAdventureProgressResponse>> onComplete)
    {
        SessionRequestSnapshot session = CaptureSessionRequest();
        BackendResult<IdleAdventureProgressResponse> result = null;
        yield return BackendApiClient.Get<IdleAdventureProgressResponse>(baseUrl, session.Token, $"idle-adventure/players/{session.PlayerId}/progress", value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureProgressResponse>(false, 0, "Backend session changed while adventure progress was loading.", null));
            yield break;
        }
        onComplete?.Invoke(result);
    }

    private IEnumerator CollectIdleAdventureAfkRewardRoutine(
        string idempotencyKey,
        string storageKey,
        SessionRequestSnapshot session,
        Action<BackendResult<IdleAdventureAfkRewardCollectResponse>> onComplete)
    {
        BackendResult<IdleAdventureAfkRewardCollectResponse> result = null;
        yield return BackendApiClient.Post<IdleAdventureAfkRewardCollectResponse>(
            baseUrl,
            session.Token,
            $"idle-adventure/players/{session.PlayerId}/afk-reward/collect",
            null,
            idempotencyKey,
            value => result = value);

        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(new BackendResult<IdleAdventureAfkRewardCollectResponse>(false, 0, "Backend session changed while AFK rewards were being collected.", null));
            yield break;
        }

        if (result != null && result.Success && result.Data != null)
        {
            if (!OwnsPersistedIdempotencyKey(storageKey, idempotencyKey))
            {
                onComplete?.Invoke(new BackendResult<IdleAdventureAfkRewardCollectResponse>(false, 0, "AFK reward response was superseded by a newer request.", null));
                yield break;
            }
            if (result.Data.items != null)
            {
                ApplyInventorySnapshot(ConvertIdleAdventureInventory(result.Data.items));
            }

            bool stateConfirmed = IdleAdventureAfkRewardSource.IsUsableBackendStateResponse(result.Data.state);
            if (!stateConfirmed)
            {
                BackendResult<IdleAdventureAfkRewardStateResponse> stateResult = null;
                yield return BackendApiClient.Get<IdleAdventureAfkRewardStateResponse>(
                    baseUrl,
                    session.Token,
                    $"idle-adventure/players/{session.PlayerId}/afk-reward",
                    value => stateResult = value);
                if (!IsSessionRequestCurrent(session))
                {
                    onComplete?.Invoke(new BackendResult<IdleAdventureAfkRewardCollectResponse>(false, 0, "Backend session changed while AFK state was refreshing.", null));
                    yield break;
                }
                if (!OwnsPersistedIdempotencyKey(storageKey, idempotencyKey))
                {
                    onComplete?.Invoke(new BackendResult<IdleAdventureAfkRewardCollectResponse>(false, 0, "AFK reward response was superseded while state was refreshing.", null));
                    yield break;
                }
                if (stateResult != null
                    && stateResult.Success
                    && IdleAdventureAfkRewardSource.IsUsableBackendStateResponse(stateResult.Data))
                {
                    result.Data.state = stateResult.Data;
                    stateConfirmed = true;
                }
            }

            if (stateConfirmed)
            {
                if (!DeletePersistedIdempotencyKey(storageKey, idempotencyKey))
                {
                    onComplete?.Invoke(new BackendResult<IdleAdventureAfkRewardCollectResponse>(false, 0, "AFK reward response was superseded before state was applied.", null));
                    yield break;
                }
                RememberIdleAdventureAfkSnapshot(result.Data.state);
            }
            else if (!OwnsPersistedIdempotencyKey(storageKey, idempotencyKey))
            {
                onComplete?.Invoke(new BackendResult<IdleAdventureAfkRewardCollectResponse>(false, 0, "AFK reward response was superseded before reconciliation completed.", null));
                yield break;
            }

            ApplyIdleAdventureAfkStateToSources(result.Data.state);
            yield return RefreshUserAndInventoryAfterMutation();
            if (!IsSessionRequestCurrent(session))
            {
                onComplete?.Invoke(new BackendResult<IdleAdventureAfkRewardCollectResponse>(false, 0, "Backend session changed while AFK rewards were refreshing.", null));
                yield break;
            }
            if (!stateConfirmed && !OwnsPersistedIdempotencyKey(storageKey, idempotencyKey))
            {
                onComplete?.Invoke(new BackendResult<IdleAdventureAfkRewardCollectResponse>(false, 0, "AFK reward response was superseded while rewards were refreshing.", null));
                yield break;
            }
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
        SessionRequestSnapshot session = CaptureSessionRequest();
        int sceneId = CurrentSceneId;
        int requestSceneGeneration = sceneRequestGeneration;
        int playerBuildingId = slot.PlayerBuildingId;
        NpcSettleRequest request = new NpcSettleRequest
        {
            playerId = session.PlayerId,
            playerBuildingId = playerBuildingId,
            playerNpcId = playerNpcId
        };

        BackendResult<ProductionResponse[]> result = null;
        yield return BackendApiClient.Post<ProductionResponse[]>(baseUrl, session.Token, "production/npc/settle", request, value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(false, "Backend session changed while the NPC was settling.");
            yield break;
        }
        if (!IsSceneRequestCurrent(sceneId, requestSceneGeneration))
        {
            onComplete?.Invoke(false, "NPC settle response was superseded by a newer scene request.");
            yield break;
        }

        bool success = result != null && result.Success;
        if (success)
        {
            ProductionResponse[] productions = result.Data ?? new ProductionResponse[0];
            RememberProductionBuildingSnapshot(playerBuildingId, productions);
            if (MapManager.Instance != null)
            {
                MapManager.Instance.ApplyRemoteProductionSnapshotForBuilding(playerBuildingId, productions);
            }
            yield return LoadPlayerNpcsRoutine(null);
            if (!IsSessionRequestCurrent(session) || !IsSceneRequestCurrent(sceneId, requestSceneGeneration))
            {
                onComplete?.Invoke(false, "NPC settle response was superseded while related state was refreshing.");
                yield break;
            }
            yield return RefreshUserAndInventoryAfterMutation();
            if (!IsSessionRequestCurrent(session) || !IsSceneRequestCurrent(sceneId, requestSceneGeneration))
            {
                onComplete?.Invoke(false, "NPC settle response was superseded while resources were refreshing.");
                yield break;
            }
            ScheduleNextProductionPoll();
        }

        onComplete?.Invoke(success, ResultMessage(result));
    }

    private IEnumerator RemoveNpcRoutine(LandSlotController slot, Action<bool, string> onComplete)
    {
        SessionRequestSnapshot session = CaptureSessionRequest();
        int sceneId = CurrentSceneId;
        int requestSceneGeneration = sceneRequestGeneration;
        int playerBuildingId = slot.PlayerBuildingId;
        PlayerBuildingRequest request = new PlayerBuildingRequest
        {
            playerId = session.PlayerId,
            playerBuildingId = playerBuildingId
        };

        BackendResult<ProductionResponse[]> result = null;
        yield return BackendApiClient.Post<ProductionResponse[]>(baseUrl, session.Token, "production/npc/remove", request, value => result = value);
        if (!IsSessionRequestCurrent(session))
        {
            onComplete?.Invoke(false, "Backend session changed while the NPC was being removed.");
            yield break;
        }
        if (!IsSceneRequestCurrent(sceneId, requestSceneGeneration))
        {
            onComplete?.Invoke(false, "NPC remove response was superseded by a newer scene request.");
            yield break;
        }

        bool success = result != null && result.Success;
        if (success)
        {
            ProductionResponse[] productions = result.Data ?? new ProductionResponse[0];
            RememberProductionBuildingSnapshot(playerBuildingId, productions);
            if (MapManager.Instance != null)
            {
                MapManager.Instance.ApplyRemoteProductionSnapshotForBuilding(playerBuildingId, productions);
            }
            yield return LoadPlayerNpcsRoutine(null);
            if (!IsSessionRequestCurrent(session) || !IsSceneRequestCurrent(sceneId, requestSceneGeneration))
            {
                onComplete?.Invoke(false, "NPC remove response was superseded while related state was refreshing.");
                yield break;
            }
            yield return RefreshUserAndInventoryAfterMutation();
            if (!IsSessionRequestCurrent(session) || !IsSceneRequestCurrent(sceneId, requestSceneGeneration))
            {
                onComplete?.Invoke(false, "NPC remove response was superseded while resources were refreshing.");
                yield break;
            }
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

    private void ApplyProductionCollect(ProductionCollectResponse response, int playerBuildingId = 0)
    {
        if (response == null) return;
        ProductionResponse[] productions = response.productions ?? new ProductionResponse[0];
        bool buildingScope = string.Equals(response.collectScope, "BUILDING", StringComparison.OrdinalIgnoreCase);
        bool sceneScope = string.Equals(response.collectScope, "SCENE", StringComparison.OrdinalIgnoreCase);
        bool allScenes = string.Equals(response.collectScope, "ALL_SCENES", StringComparison.OrdinalIgnoreCase);
        RememberProductionBuildingSnapshots(productions);
        if (buildingScope && playerBuildingId > 0)
        {
            RememberProductionBuildingSnapshot(playerBuildingId, productions);
        }
        else if (sceneScope)
        {
            RememberProductionSceneSnapshot(productions, response.sceneId);
        }
        else if (allScenes)
        {
            RememberProductionSceneSnapshot(productions, 0);
        }
        if (MapManager.Instance != null)
        {
            if (buildingScope && playerBuildingId > 0)
            {
                MapManager.Instance.ApplyRemoteProductionSnapshotForBuilding(playerBuildingId, productions);
            }
            else if (sceneScope && response.sceneId == CurrentSceneId)
            {
                MapManager.Instance.ApplyRemoteProductionSnapshotForScene(productions, response.sceneId);
            }
            else if (allScenes)
            {
                MapManager.Instance.ApplyRemoteProductionSnapshotForScene(productions, CurrentSceneId);
            }
        }
        if (response.items != null) ApplyInventorySnapshot(response.items);
        Debug.Log("Backend production collect succeeded; inventory snapshot refreshed.", this);
    }

    private IEnumerator RefreshUserAndInventoryAfterMutation()
    {
        bool userLoaded = false;
        yield return RequestUserRoutine(success => userLoaded = success);
        if (!userLoaded || CurrentUser == null || CurrentUser.items == null)
        {
            yield return RequestInventoryRoutine(null);
        }
    }

    private void ApplyUserSnapshot(UserResponse snapshot)
    {
        if (snapshot == null) return;

        UserResponse existing = CurrentUser;
        bool samePlayer = existing != null
            && existing.id > 0L
            && snapshot.id == existing.id;
        bool wouldRollbackProgression = samePlayer
            && (existing.totalExperience > snapshot.totalExperience
                || (existing.totalExperience == snapshot.totalExperience
                    && existing.level > snapshot.level));
        if (wouldRollbackProgression)
        {
            snapshot.totalExperience = existing.totalExperience;
            snapshot.level = existing.level;
            snapshot.currentLevelExperience = existing.currentLevelExperience;
            snapshot.requiredExperienceForNextLevel = existing.requiredExperienceForNextLevel;
            snapshot.experienceProgressBps = existing.experienceProgressBps;
            snapshot.levelCurveRevision = existing.levelCurveRevision;
            snapshot.maxLevel = existing.maxLevel;
        }

        CurrentUser = snapshot;
        if (snapshot.id > 0) PlayerId = ToSafeInt(snapshot.id);
        if (snapshot.items != null)
        {
            CurrentInventory = snapshot.items;
        }

        PlayerPrefs.SetInt("PlayerLv", Mathf.Max(1, snapshot.level));
        PlayerPrefs.SetString("LoginUsername", snapshot.username ?? string.Empty);
        PlayerPrefs.SetString("PlayerNickname", snapshot.nickname ?? string.Empty);
        PlayerPrefs.SetString("PlayerNicknameVersion", Math.Max(0L, snapshot.nicknameVersion).ToString());
        PlayerPrefs.SetString("PlayerNicknameUpdatedTime", snapshot.nicknameUpdatedTime ?? string.Empty);
        PlayerPrefs.SetInt("PlayerNicknameChangeCount", Math.Max(0, snapshot.nicknameChangeCount));
        PlayerPrefs.SetInt("PlayerNicknameChangeRequired", snapshot.nicknameChangeRequired ? 1 : 0);
        PlayerPrefs.SetInt("PlayerAvatarOwnerId", ToSafeInt(snapshot.id));
        PlayerPrefs.SetString("PlayerAvatarUrl", snapshot.avatarUrl ?? string.Empty);
        PlayerPrefs.SetString("PlayerAvatarSource", snapshot.avatarSource ?? "DEFAULT");
        PlayerPrefs.SetString("PlayerAvatarVersion", Math.Max(0L, snapshot.avatarVersion).ToString());
        PlayerPrefs.SetString("PlayerAvatarUpdatedTime", snapshot.avatarUpdatedTime ?? string.Empty);
        PlayerPrefs.SetString("PlayerTotalExperience", Math.Max(0L, snapshot.totalExperience).ToString());
        PlayerPrefs.SetInt("PlayerCurrentLevelExperience", ToPlayerPrefsInt(snapshot.currentLevelExperience));
        PlayerPrefs.SetInt("PlayerRequiredExperience", ToPlayerPrefsInt(snapshot.requiredExperienceForNextLevel));
        PlayerPrefs.SetInt("PlayerExperienceProgressBps", Mathf.Clamp(snapshot.experienceProgressBps, 0, 10000));
        PlayerPrefs.SetInt("PlayerLevelCurveRevision", ToPlayerPrefsInt(snapshot.levelCurveRevision));
        PlayerPrefs.SetInt("PlayerMaxLevel", snapshot.maxLevel ? 1 : 0);
        PlayerPrefs.SetInt("PlayerEconomyFrozen", snapshot.economyFrozen ? 1 : 0);
        SetResourcePreference("Gold", snapshot.gold);
        SetResourcePreference("Wood", snapshot.wood);
        SetResourcePreference("Stone", snapshot.stone);
        SetResourcePreference("Iron", snapshot.iron);
        SetResourcePreference("Crystal", snapshot.diamond);
        PlayerPrefs.Save();
        SyncWeChatFriendLevel(snapshot.level);
        RefreshLandUnlockState();
        UserSnapshotChanged?.Invoke(CurrentUser);
        if (snapshot.items != null) InventorySnapshotChanged?.Invoke(snapshot.items);
    }

    private void ApplyInventorySnapshot(InventoryItemResponse[] items)
    {
        if (items == null) return;

        CurrentInventory = items;
        if (CurrentUser == null) CurrentUser = new UserResponse();
        CurrentUser.items = items;
        for (int i = 0; i < items.Length; i++)
        {
            InventoryItemResponse item = items[i];
            if (item == null) continue;
            string key = NormalizeResourceKey(item.itemKey, item.name);
            if (string.IsNullOrEmpty(key)) continue;
            SetResourcePreference(key, item.quantity);
            if (CurrentUser != null)
            {
                switch (key)
                {
                    case "Gold": CurrentUser.gold = ToSafeInt(item.quantity); break;
                    case "Wood": CurrentUser.wood = ToSafeInt(item.quantity); break;
                    case "Stone": CurrentUser.stone = ToSafeInt(item.quantity); break;
                    case "Iron": CurrentUser.iron = ToSafeInt(item.quantity); break;
                    case "Crystal": CurrentUser.diamond = ToSafeInt(item.quantity); break;
                }
            }
        }
        PlayerPrefs.Save();
        InventorySnapshotChanged?.Invoke(items);
        UserSnapshotChanged?.Invoke(CurrentUser);
    }

    private void ApplyBuildingExperienceGrants(BuildingResponse[] buildings)
    {
        if (buildings == null) return;

        BuildingResponse latest = null;
        for (int i = 0; i < buildings.Length; i++)
        {
            BuildingResponse candidate = buildings[i];
            if (candidate == null || candidate.experienceGrant == null) continue;
            if (latest == null
                || candidate.experienceGrant.afterExperience > latest.experienceGrant.afterExperience)
            {
                latest = candidate;
            }
        }

        ApplyBuildingExperienceGrant(latest);
    }

    private void ApplyBuildingExperienceGrant(BuildingResponse building)
    {
        if (building == null || building.experienceGrant == null) return;
        ExperienceGrantResponse grant = building.experienceGrant;
        if (grant.playerId > 0L && grant.playerId != PlayerId) return;
        if (CurrentUser != null && CurrentUser.totalExperience > grant.afterExperience) return;
        if (CurrentUser == null) CurrentUser = new UserResponse();

        CurrentUser.totalExperience = Math.Max(0L, grant.afterExperience);
        CurrentUser.level = Mathf.Max(1, grant.afterLevel);
        CurrentUser.currentLevelExperience = Math.Max(0L, grant.currentLevelExperience);
        CurrentUser.requiredExperienceForNextLevel = Math.Max(0L, grant.requiredExperienceForNextLevel);
        CurrentUser.experienceProgressBps = Mathf.Clamp(grant.experienceProgressBps, 0, 10000);
        CurrentUser.levelCurveRevision = Math.Max(0L, grant.curveRevision);
        CurrentUser.maxLevel = grant.maxLevel;

        PlayerPrefs.SetInt("PlayerLv", CurrentUser.level);
        PlayerPrefs.SetString("PlayerTotalExperience", CurrentUser.totalExperience.ToString());
        PlayerPrefs.SetInt("PlayerCurrentLevelExperience", ToPlayerPrefsInt(CurrentUser.currentLevelExperience));
        PlayerPrefs.SetInt("PlayerRequiredExperience", ToPlayerPrefsInt(CurrentUser.requiredExperienceForNextLevel));
        PlayerPrefs.SetInt("PlayerExperienceProgressBps", CurrentUser.experienceProgressBps);
        PlayerPrefs.SetInt("PlayerLevelCurveRevision", ToPlayerPrefsInt(CurrentUser.levelCurveRevision));
        PlayerPrefs.SetInt("PlayerMaxLevel", CurrentUser.maxLevel ? 1 : 0);
        PlayerPrefs.Save();
        SyncWeChatFriendLevel(CurrentUser.level);
        RefreshLandUnlockState();
        UserSnapshotChanged?.Invoke(CurrentUser);
    }

    private static string NormalizeResourceKey(string itemKey, string itemName)
    {
        string value = string.IsNullOrEmpty(itemKey) ? itemName : itemKey;
        if (string.IsNullOrEmpty(value)) return string.Empty;
        string normalized = value.Trim().Replace("-", "_").Replace(" ", "_").ToLowerInvariant();
        switch (normalized)
        {
            case "gold":
            case "coin":
            case "coins":
            case "金币": return "Gold";
            case "wood":
            case "lumber":
            case "木材": return "Wood";
            case "stone":
            case "石材": return "Stone";
            case "iron":
            case "ore":
            case "铁矿": return "Iron";
            case "diamond":
            case "crystal":
            case "diamonds":
            case "钻石": return "Crystal";
            default: return string.Empty;
        }
    }

    private static int ToSafeInt(long value)
    {
        if (value <= 0L) return 0;
        return value >= int.MaxValue ? int.MaxValue : (int)value;
    }

    private static int ToPlayerPrefsInt(long value)
    {
        return ToSafeInt(value);
    }

    private static void SetResourcePreference(string key, long value)
    {
        if (string.IsNullOrEmpty(key)) return;
        PlayerPrefs.SetInt(key, ToPlayerPrefsInt(value));
    }

    private static string GetOrCreatePersistedIdempotencyKey(string key)
    {
        string existing = PlayerPrefs.GetString(key, string.Empty);
        if (Guid.TryParse(existing, out _)) return existing;

        string created = Guid.NewGuid().ToString("D");
        PlayerPrefs.SetString(key, created);
        PlayerPrefs.Save();
        return created;
    }

    private static string GetOrCreatePersistedIdempotencyKey(string key, string context)
    {
        string existing = PlayerPrefs.GetString(key, string.Empty);
        string existingContext = PlayerPrefs.GetString(key + IdempotencyContextSuffix, string.Empty);
        if (Guid.TryParse(existing, out _)
            && !string.IsNullOrEmpty(context)
            && string.Equals(existingContext, context, StringComparison.Ordinal))
        {
            return existing;
        }

        string created = Guid.NewGuid().ToString("D");
        PlayerPrefs.SetString(key, created);
        PlayerPrefs.SetString(key + IdempotencyContextSuffix, context ?? string.Empty);
        PlayerPrefs.Save();
        return created;
    }

    private string BuildProductionCollectAllStorageKey(int sceneId)
    {
        return ProductionCollectKeyPrefix + PlayerId + ".all." + (sceneId > 0 ? sceneId.ToString() : "global");
    }

    private string GetOrCreateProductionBuildingSnapshotToken(int playerBuildingId)
    {
        return GetOrCreatePersistedIdempotencyKey(
            ProductionSnapshotKeyPrefix + PlayerId + ".building." + playerBuildingId);
    }

    private string GetOrCreateProductionSceneSnapshotToken(int sceneId)
    {
        return GetOrCreatePersistedIdempotencyKey(
            ProductionSnapshotKeyPrefix + PlayerId + ".scene." + (sceneId > 0 ? sceneId.ToString() : "global"));
    }

    private void RememberProductionBuildingSnapshots(ProductionResponse[] productions)
    {
        if (productions == null) return;
        Dictionary<int, List<ProductionResponse>> grouped = new Dictionary<int, List<ProductionResponse>>();
        for (int index = 0; index < productions.Length; index++)
        {
            ProductionResponse production = productions[index];
            if (production == null || production.playerBuildingId <= 0) continue;
            if (!grouped.TryGetValue(production.playerBuildingId, out List<ProductionResponse> values))
            {
                values = new List<ProductionResponse>();
                grouped.Add(production.playerBuildingId, values);
            }
            values.Add(production);
        }

        foreach (KeyValuePair<int, List<ProductionResponse>> entry in grouped)
        {
            RememberProductionBuildingSnapshot(entry.Key, entry.Value.ToArray());
        }
    }

    private void RememberProductionBuildingSnapshot(int playerBuildingId, ProductionResponse[] productions)
    {
        if (playerBuildingId <= 0) return;
        RememberPersistedSnapshot(
            ProductionSnapshotKeyPrefix + PlayerId + ".building." + playerBuildingId,
            BuildProductionSnapshotFingerprint(productions, 0, playerBuildingId));
    }

    private void RememberProductionSceneSnapshot(ProductionResponse[] productions, int sceneId)
    {
        RememberPersistedSnapshot(
            ProductionSnapshotKeyPrefix + PlayerId + ".scene." + (sceneId > 0 ? sceneId.ToString() : "global"),
            BuildProductionSnapshotFingerprint(productions, sceneId, 0));
    }

    private static string BuildProductionSnapshotFingerprint(
        ProductionResponse[] productions,
        int sceneId,
        int playerBuildingId)
    {
        List<string> entries = new List<string>();
        if (productions != null)
        {
            for (int index = 0; index < productions.Length; index++)
            {
                ProductionResponse production = productions[index];
                if (production == null) continue;
                if (playerBuildingId > 0 && production.playerBuildingId != playerBuildingId) continue;
                if (sceneId > 0 && production.sceneId > 0 && production.sceneId != sceneId) continue;
                entries.Add(
                    production.sceneId + "|"
                    + production.playerBuildingId + "|"
                    + production.itemId + "|"
                    + (production.itemKey ?? string.Empty) + "|"
                    + production.storedAmount + "|"
                    + (production.lastSettleTime ?? string.Empty) + "|"
                    + production.storageCapacity);
            }
        }
        entries.Sort(StringComparer.Ordinal);
        return ComputeSnapshotFingerprint(string.Join("\n", entries.ToArray()));
    }

    private string GetOrCreateIdleAdventureAfkSnapshotToken()
    {
        return GetOrCreatePersistedIdempotencyKey(AfkSnapshotKeyPrefix + PlayerId);
    }

    private void RememberIdleAdventureAfkSnapshot(IdleAdventureAfkRewardStateResponse state)
    {
        if (state == null) return;
        CurrentIdleAdventureAfkReward = state;
        string canonical = state.ResolvedLastSettledServerUnixSeconds
            + "|" + state.ResolvedItemKey
            + "|" + state.ResolvedBaseQuantityPerCycle
            + "|" + state.ResolvedQuantityPerHighestStage
            + "|" + state.ResolvedProductionIntervalSeconds
            + "|" + state.ResolvedMaxStoredQuantity;
        RememberPersistedSnapshot(AfkSnapshotKeyPrefix + PlayerId, ComputeSnapshotFingerprint(canonical));
    }

    private static void RememberPersistedSnapshot(string key, string fingerprint)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(fingerprint)) return;
        string fingerprintKey = key + ".fingerprint";
        string existingToken = PlayerPrefs.GetString(key, string.Empty);
        string existingFingerprint = PlayerPrefs.GetString(fingerprintKey, string.Empty);
        if (Guid.TryParse(existingToken, out _)
            && string.Equals(existingFingerprint, fingerprint, StringComparison.Ordinal))
        {
            return;
        }

        PlayerPrefs.SetString(key, Guid.NewGuid().ToString("D"));
        PlayerPrefs.SetString(fingerprintKey, fingerprint);
        PlayerPrefs.Save();
    }

    private static string ComputeSnapshotFingerprint(string canonical)
    {
        using (System.Security.Cryptography.SHA256 sha256 = System.Security.Cryptography.SHA256.Create())
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(canonical ?? string.Empty);
            return BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", string.Empty);
        }
    }

    private string BuildIdleAdventureCommandStorageKey(
        string operation,
        string sessionKey,
        string discriminator)
    {
        return BuildIdleAdventureCommandStorageKey(PlayerId, operation, sessionKey, discriminator);
    }

    private static string BuildIdleAdventureCommandStorageKey(
        int playerId,
        string operation,
        string sessionKey,
        string discriminator)
    {
        return IdleAdventureCommandKeyPrefix
            + playerId
            + "."
            + NormalizeStorageKeySegment(operation)
            + "."
            + NormalizeStorageKeySegment(sessionKey)
            + "."
            + NormalizeStorageKeySegment(discriminator);
    }

    private static string BuildIdleAdventureStartStorageKey(int playerId)
    {
        return IdleAdventureStartKeyPrefix + playerId;
    }

    private static string BuildIdleAdventureStartRequestContext(IdleAdventureStartSessionRequest request)
    {
        if (request == null) return string.Empty;

        string canonical = request.stage
            + "|"
            + request.use_saved_formation
            + "|"
            + (request.formation_slots == null
                ? string.Empty
                : JsonUtility.ToJson(new IdleAdventureSaveFormationRequest
                {
                    slots = request.formation_slots
                }));
        return ComputeSnapshotFingerprint(canonical);
    }

    private static bool ShouldDiscardIdleAdventureStartKey(BackendResult<IdleAdventureSessionResponse> result)
    {
        if (result == null || result.HttpStatus <= 0) return false;
        if (result.HttpStatus == 408 || result.HttpStatus == 429) return false;
        if (result.Code == 0 && result.HttpStatus >= 200 && result.HttpStatus < 300) return false;
        return result.HttpStatus < 500;
    }

    private string GetOrCreateIdleAdventureSessionSnapshotToken(string sessionKey)
    {
        return GetOrCreateIdleAdventureSessionSnapshotToken(PlayerId, sessionKey);
    }

    private static string GetOrCreateIdleAdventureSessionSnapshotToken(int playerId, string sessionKey)
    {
        return GetOrCreatePersistedIdempotencyKey(BuildIdleAdventureSessionSnapshotStorageKey(playerId, sessionKey));
    }

    private static void RememberIdleAdventureSessionSnapshot(
        int playerId,
        string sessionKey,
        IdleAdventureSessionResponse state)
    {
        if (playerId <= 0 || string.IsNullOrWhiteSpace(sessionKey) || state == null) return;

        List<string> entries = new List<string>
        {
            "stage=" + state.stage,
            "attempt=" + state.stage_attempt,
            "round=" + state.round,
            "status=" + (state.status ?? string.Empty),
            "paused=" + state.paused,
            "choice=" + state.stage_choice_pending,
            "next=" + state.pending_next_stage
        };
        AppendIdleAdventureSnapshotEntries(entries, "friendly", state.friendlies);
        AppendIdleAdventureSnapshotEntries(entries, "enemy", state.enemies);
        AppendIdleAdventureSnapshotEntries(entries, "reward", state.pending_rewards);
        entries.Sort(StringComparer.Ordinal);
        RememberPersistedSnapshot(
            BuildIdleAdventureSessionSnapshotStorageKey(playerId, sessionKey),
            ComputeSnapshotFingerprint(string.Join("\n", entries.ToArray())));
    }

    private static void AppendIdleAdventureSnapshotEntries<T>(List<string> entries, string prefix, T[] values)
    {
        if (entries == null || values == null) return;
        for (int index = 0; index < values.Length; index++)
        {
            T value = values[index];
            if (ReferenceEquals(value, null)) continue;
            entries.Add(prefix + "=" + JsonUtility.ToJson(value));
        }
    }

    private void RotateIdleAdventureSessionSnapshotToken(string sessionKey)
    {
        RotateIdleAdventureSessionSnapshotToken(PlayerId, sessionKey);
    }

    private static void RotateIdleAdventureSessionSnapshotToken(int playerId, string sessionKey)
    {
        if (playerId <= 0 || string.IsNullOrWhiteSpace(sessionKey)) return;
        SavePersistedIdempotencyKey(
            BuildIdleAdventureSessionSnapshotStorageKey(playerId, sessionKey),
            Guid.NewGuid().ToString("D"));
    }

    private string BuildIdleAdventureSessionSnapshotStorageKey(string sessionKey)
    {
        return BuildIdleAdventureSessionSnapshotStorageKey(PlayerId, sessionKey);
    }

    private static string BuildIdleAdventureSessionSnapshotStorageKey(int playerId, string sessionKey)
    {
        return IdleAdventureSessionSnapshotKeyPrefix
            + playerId
            + "."
            + NormalizeStorageKeySegment(sessionKey);
    }

    private static string NormalizeStorageKeySegment(string value)
    {
        if (string.IsNullOrEmpty(value)) return "none";

        System.Text.StringBuilder builder = new System.Text.StringBuilder(value.Length);
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if ((character >= 'a' && character <= 'z')
                || (character >= 'A' && character <= 'Z')
                || (character >= '0' && character <= '9')
                || character == '-' || character == '_')
            {
                builder.Append(character);
            }
            else
            {
                builder.Append('_');
            }
        }

        return builder.Length == 0 ? "none" : builder.ToString();
    }

    private static void SavePersistedIdempotencyKey(string key, string value)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value)) return;
        PlayerPrefs.SetString(key, value);
        PlayerPrefs.Save();
    }

    private static void SavePersistedIdempotencyKey(string key, string value, string context)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value)) return;
        PlayerPrefs.SetString(key, value);
        PlayerPrefs.SetString(key + IdempotencyContextSuffix, context ?? string.Empty);
        PlayerPrefs.Save();
    }

    private static bool OwnsPersistedIdempotencyKey(string key, string expectedValue)
    {
        return !string.IsNullOrEmpty(key)
            && !string.IsNullOrEmpty(expectedValue)
            && string.Equals(PlayerPrefs.GetString(key, string.Empty), expectedValue, StringComparison.Ordinal);
    }

    private static bool DeletePersistedIdempotencyKey(string key, string expectedValue)
    {
        if (!OwnsPersistedIdempotencyKey(key, expectedValue)) return false;
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.DeleteKey(key + IdempotencyContextSuffix);
        PlayerPrefs.Save();
        return true;
    }

    private void ApplyIdleAdventureAfkStateToSources(IdleAdventureAfkRewardStateResponse state)
    {
        if (state == null) return;
        IdleAdventureAfkRewardSource[] sources = FindObjectsByType<IdleAdventureAfkRewardSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] != null) sources[i].ApplyBackendStateFromService(state);
        }
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
                itemId = (int)Math.Min(int.MaxValue, item.ResolvedItemId),
                itemKey = item.ResolvedItemKey,
                name = item.ResolvedName,
                itemType = item.ResolvedItemType,
                icon = item.icon,
                quantity = Math.Max(0L, item.quantity)
            };
        }

        return converted;
    }

    private void StoreLogin(LoginResponse response)
    {
        sceneSwitchPending = false;
        AdvanceSceneRequestGeneration();
        int authenticatedPlayerId = response.user != null ? ToSafeInt(response.user.id) : 0;
        ReconcileAreaSelectionForPlayer(authenticatedPlayerId);
        Token = response.token;
        ApplyUserSnapshot(response.user);
        PlayerId = authenticatedPlayerId;
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
        Debug.Log($"Backend login succeeded: playerId={PlayerId}", this);
    }

    private void ClearSession()
    {
        sceneSwitchPending = false;
        AdvanceSceneRequestGeneration();
        int authenticatedPlayerId = PlayerPrefs.GetInt(HomePlayerIdKey, 0);
        if (authenticatedPlayerId <= 0)
        {
            authenticatedPlayerId = PlayerId;
        }

        ReconcileAreaSelectionForPlayer(authenticatedPlayerId);
        bool wasAccountSession = (PlayerPrefs.GetInt(HomeLoginStateKey, 0) == 1
                && string.Equals(
                    PlayerPrefs.GetString(HomeSessionModeKey, string.Empty),
                    "account",
                    StringComparison.Ordinal))
            || PlayerPrefs.GetInt(SessionManagedKey, 0) == 1
            || !string.IsNullOrEmpty(Token)
            || PlayerId > 0;

        Token = string.Empty;
        PlayerId = 0;
        CurrentUser = null;
        remoteAvailableLands.Clear();
        purificationSnapshots.Clear();
        RefreshLandUnlockState();
        CurrentInventory = null;
        NpcCatalog = null;
        PlayerNpcs = null;
        LandScenes = null;
        CurrentSceneId = 0;
        CurrentSceneConfigVersion = 0L;
        CurrentLandPolicyRevision = 0L;
        CurrentStamina = null;
        CurrentIdleAdventureAfkReward = null;
        CurrentWorkState = null;
        IdleAdventureConfig = null;
        IdleAdventureBuffPresets = null;
        IdleAdventureSkills = null;
        IdleAdventureFieldBuffs = null;
        IdleAdventureAttributeRestraints = null;
        IdleAdventureStageRewards = null;
        IdleAdventureAvailableNpcs = null;
        IdleAdventureStageEnemyPreview = null;
        IdleAdventureProgress = null;
        PlayerPrefs.DeleteKey(SceneIdKey);
        PlayerPrefs.DeleteKey(TokenKey);
        PlayerPrefs.DeleteKey(PlayerIdKey);
        PlayerPrefs.DeleteKey(HomeTokenKey);
        PlayerPrefs.DeleteKey(HomePlayerIdKey);
        PlayerPrefs.DeleteKey(HomeTokenExpiresAtKey);
        PlayerPrefs.SetInt(HomeLoginStateKey, 0);
        PlayerPrefs.DeleteKey(HomeSessionModeKey);
        PlayerPrefs.SetInt(SessionManagedKey, 0);
        if (wasAccountSession)
        {
            ClearAccountSnapshotPreferences();
        }
        observedSessionRevision = IncrementSessionRevision();
        PlayerPrefs.Save();
    }

    private static void ClearAccountSnapshotPreferences()
    {
        for (int index = 0; index < AccountSnapshotPreferenceKeys.Length; index++)
        {
            PlayerPrefs.DeleteKey(AccountSnapshotPreferenceKeys[index]);
        }
    }

    private static void ReconcileAreaSelectionForPlayer(int playerId)
    {
        if (playerId <= 0)
        {
            return;
        }

        int ownerPlayerId = PlayerPrefs.GetInt(AreaSelectionOwnerPlayerIdKey, 0);
        if (ownerPlayerId <= 0)
        {
            ownerPlayerId = PlayerPrefs.GetInt(HomePlayerIdKey, 0);
        }

        if (ownerPlayerId > 0 && ownerPlayerId != playerId)
        {
            PlayerPrefs.DeleteKey(OpenAreaLandTypeKey);
            PlayerPrefs.DeleteKey(CurrentAreaLandTypeKey);
        }

        PlayerPrefs.SetInt(AreaSelectionOwnerPlayerIdKey, playerId);
    }

    private void RefreshLandUnlockState()
    {
        int playerLevel = CurrentUser != null ? Mathf.Max(1, CurrentUser.level) : 1;
        if (MapManager.Instance != null) MapManager.Instance.RefreshLandUnlockState(playerLevel);
        ApplyRemoteLandDragItems();
    }

    private void RefreshSharedSessionIfNeeded()
    {
        int currentRevision = PlayerPrefs.GetInt(SessionRevisionKey, 0);
        if (currentRevision == observedSessionRevision)
        {
            return;
        }

        sceneSwitchPending = false;
        AdvanceSceneRequestGeneration();
        observedSessionRevision = currentRevision;
        Token = PlayerPrefs.GetString(TokenKey, string.Empty);
        PlayerId = PlayerPrefs.GetInt(PlayerIdKey, 0);
        CurrentUser = null;
        RefreshLandUnlockState();
        CurrentInventory = null;
        NpcCatalog = null;
        PlayerNpcs = null;
        remoteAvailableLands.Clear();
        purificationSnapshots.Clear();
        LandScenes = null;
        CurrentStamina = null;
        CurrentIdleAdventureAfkReward = null;
        CurrentWorkState = null;
        IdleAdventureConfig = null;
        IdleAdventureBuffPresets = null;
        IdleAdventureSkills = null;
        IdleAdventureFieldBuffs = null;
        IdleAdventureAttributeRestraints = null;
        IdleAdventureStageRewards = null;
        IdleAdventureAvailableNpcs = null;
        IdleAdventureStageEnemyPreview = null;
        IdleAdventureProgress = null;
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
