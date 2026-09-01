using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[ExecuteAlways]
public sealed class IdleAdventureModeController : MonoBehaviour
{
    private const int FormationRows = 3;
    private const int FormationColumns = 2;
    private const int MaxPartySize = FormationRows * FormationColumns;
    private const int FriendlySkillSlotCount = 2;
    private const int EnemySkillSlotCount = 1;
    private const int MaxBuffIconSlots = 6;
    private const float AttributeRestraintMultiplier = 0.2f;
    private const string BackgroundCanvasName = "Idle Adventure Background Canvas";
    private const string StartCanvasName = "Idle Adventure Start Canvas";
    private const string HudCanvasName = "Idle Adventure Canvas";
    private const string AfkRewardCanvasName = "Idle Adventure AFK Reward Canvas";
    private const long BackendBattleFocusBuffPresetId = 16001L;
    private const long BackendArmorBreakBuffPresetId = 16002L;
    private const float FormationRealtimeRefreshDelaySeconds = 0.15f;
    private const float FormationRealtimeHandshakeTimeoutSeconds = 10f;
    private const float FormationRealtimeFallbackIntervalSeconds = 20f;
    private const string PendingRoleAcquisitionRouteKey = "weijun.npcAcquisition.routeKey";
    private const string PendingRoleAcquisitionRoutePlayerIdKey = "weijun.npcAcquisition.playerId";
    private const string PendingRoleAcquisitionRouteExpiresAtKey = "weijun.npcAcquisition.expiresAt";
    private const string PendingRoleAcquisitionRouteModeCodeKey = "weijun.npcAcquisition.modeCode";
    private const string PendingRoleAcquisitionRouteStageKey = "weijun.npcAcquisition.stage";
    private const string PendingRoleAcquisitionRoutePoolKeyKey = "weijun.npcAcquisition.poolKey";
    private const int MaxRoleAcquisitionRouteTokenLength = 128;

    [Header("Mode Config")]
    [SerializeField] private IdleAdventureModeConfig modeConfig;

    [Header("Party Size")]
    [SerializeField, Range(1, MaxPartySize)] private int startingFriendlyCount = 3;
    [SerializeField, Range(1, MaxPartySize)] private int baseEnemyCount = 2;
    [SerializeField, Min(0)] private int addEnemyEveryStages = 2;

    [Header("Friendly Stats")]
    [SerializeField, Min(1)] private int friendlyBaseHealth = 160;
    [SerializeField, Min(0)] private int friendlyBaseAttack = 28;
    [SerializeField, Min(0)] private int friendlyBaseDefense = 5;
    [SerializeField, Min(1)] private int friendlyBaseSpeed = 12;

    [Header("Enemy Stats")]
    [SerializeField, Min(1)] private int enemyBaseHealth = 95;
    [SerializeField, Min(0)] private int enemyBaseAttack = 15;
    [SerializeField, Min(0)] private int enemyBaseDefense = 1;
    [SerializeField, Min(1)] private int enemyBaseSpeed = 9;
    [SerializeField, Min(0)] private int enemyHealthPerStage = 22;
    [SerializeField, Min(0)] private int enemyAttackPerStage = 4;
    [SerializeField, Min(0)] private int enemySpeedPerStage = 1;

    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float turnInterval = 0.75f;
    [SerializeField, Min(0.05f)] private float attackAnimationDuration = 0.28f;
    [SerializeField, Min(0.05f)] private float hitFlashDuration = 0.18f;

    [Header("Combatant Prefabs")]
    [SerializeField] private IdleAdventureCombatant friendlyCombatantPrefab;
    [SerializeField] private IdleAdventureCombatant enemyCombatantPrefab;

    [Header("Runtime Parents")]
    [SerializeField] private Transform combatantRootOverride;

    [Header("Rewards")]
    [SerializeField] private IdleAdventureRewardLedger rewardLedger;
    [SerializeField] private IdleAdventureAfkRewardSource afkRewardSource;

    [Header("Progress")]
    [SerializeField] private IdleAdventureStageProgress stageProgress;

    [Header("Editor Debug")]
    [SerializeField] private bool allowLocalCombatSimulation;

    private readonly Queue<string> battleLog = new Queue<string>();
    private readonly List<IdleAdventureCombatant> friendlies = new List<IdleAdventureCombatant>(MaxPartySize);
    private readonly List<IdleAdventureCombatant> enemies = new List<IdleAdventureCombatant>(MaxPartySize);
    private readonly int[] friendlyFormation = new int[MaxPartySize];
    private readonly Dictionary<string, IdleAdventureCombatant> backendRuntimeCombatants = new Dictionary<string, IdleAdventureCombatant>();
    private readonly List<int> clearedRewardStages = new List<int>();
    private readonly Dictionary<string, int> pendingRunRewards = new Dictionary<string, int>();
    private readonly Dictionary<string, int> grantedRewards = new Dictionary<string, int>();
    private readonly Dictionary<string, string> rewardDisplayNames = new Dictionary<string, string>();
    private readonly HashSet<string> backendStageClearRequests = new HashSet<string>();

    [Header("Persistent Scene References")]
    [SerializeField] private Camera sceneCamera;
    [SerializeField] private EventSystem sceneEventSystem;
    [SerializeField] private Transform combatantRoot;
    [SerializeField] private Canvas backgroundCanvas;
    [SerializeField] private Canvas startCanvas;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Canvas afkRewardCanvas;
    [SerializeField] private IdleAdventureBattleHud battleHudView;
    [SerializeField] private IdleAdventureStartPanel startPanelView;
    [SerializeField] private IdleAdventureFormationPanel formationPanelView;
    [SerializeField] private IdleAdventureStageEndPanel stageEndPanelView;
    [SerializeField] private IdleAdventurePausePanel pausePanelView;
    [SerializeField] private IdleAdventureActorView[] friendlyViews = new IdleAdventureActorView[MaxPartySize];
    [SerializeField] private IdleAdventureActorView[] enemyViews = new IdleAdventureActorView[MaxPartySize];

    private TextMeshProUGUI stageText;
    private TextMeshProUGUI turnText;
    private TextMeshProUGUI friendlyCountText;
    private TextMeshProUGUI enemyCountText;
    private TextMeshProUGUI logText;
    private TextMeshProUGUI resultText;
    private Button pauseButton;
    private TextMeshProUGUI pauseButtonText;
    private TextMeshProUGUI staminaText;
    private TextMeshProUGUI accelerationText;
    private Button accelerateButton;
    private bool interfaceBuildInProgress;
    private bool staminaRequestPending;
    private IdleAdventureAfkRewardStateResponse accelerationPolicyState;
    private bool accelerationPolicyLoaded;
    private bool accelerationEnabled;
    private int accelerationStaminaCost;

    private int stage = 1;
    private bool acquisitionRouteApplied;
    private string acquisitionRouteModeCode = string.Empty;
    private int acquisitionRouteStage;
    private string acquisitionRoutePoolKey = string.Empty;
    private int round = 1;
    private int pendingNextStage = -1;
    private bool battleStarted;
    private bool stageChoicePending;
    private bool stageFailedPending;
    private bool modePausedByChoice;
    private bool paused;
    private bool backendModeActive;
    private bool backendRequestPending;
    private bool backendFormationDirty;
    private bool localSimulationBlockedLogged;
    private int backendFormationRevision;
    private string backendSessionKey;
    private string backendRunningSessionKey;
    private int backendHighestStage;
    private int backendNextStage = 1;
    private int selectedReplayStage;
    private IdleAdventurePlayerNpcResponse[] backendAvailableNpcs = new IdleAdventurePlayerNpcResponse[0];
    private Coroutine backendHomeLoadRoutine;
    private bool backendHomeLoadRunning;
    private Coroutine backendReferenceDataRoutine;
    private bool backendReferenceDataLoaded;
    private int backendReferenceDataStage;
    private IdleAdventureStageEnemiesResponse backendEnemyPreview;
    private int backendStatePlayerId;
    private string backendStateToken = string.Empty;
    private bool backendStateIdentityInitialized;
    private Coroutine formationRealtimeConnectionRoutine;
    private Coroutine formationRealtimeReconnectRoutine;
    private Coroutine formationRealtimeHandshakeRoutine;
    private Coroutine formationRealtimeHeartbeatRoutine;
    private int formationRealtimeHeartbeatSequence;
    private Coroutine formationRealtimeRefreshRoutine;
    private Coroutine formationRealtimeFallbackRoutine;
    private readonly HashSet<string> formationRealtimeEventIds = new HashSet<string>();
    private readonly Queue<string> formationRealtimeEventOrder = new Queue<string>();
    private bool formationRealtimeConnected;
    private bool formationRealtimeTransportOpen;
    private bool formationRealtimeClosing;
    private bool formationRealtimeRefreshQueued;
    private bool resumeFormationRealtimeAfterPause;
    private Coroutine battleRoutine;

    public IReadOnlyDictionary<string, int> GrantedRewards => grantedRewards;
    public IReadOnlyDictionary<string, int> PendingRunRewards => pendingRunRewards;

    private void Awake()
    {
        EnsureViewCacheArrays();
        ApplyModeConfig();
        ConfigureCamera();
        EnsureEventSystem();
        BuildInterface();
        EnsureCombatantRoot();
        EnsureRewardLedger();
        EnsureStageProgress();
        EnsureAfkRewardSource();
        ResetToStartScreen();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (Application.isPlaying) return;

        UnityEditor.EditorApplication.delayCall -= EnsurePersistentSceneComponentsInEditMode;
        UnityEditor.EditorApplication.delayCall += EnsurePersistentSceneComponentsInEditMode;
#endif
    }

#if UNITY_EDITOR
    private void EnsurePersistentSceneComponentsInEditMode()
    {
        if (this == null || Application.isPlaying) return;

        EnsureViewCacheArrays();
        ApplyModeConfig();
        ConfigureCamera();
        EnsureEventSystem();
        BuildInterface();
        EnsureCombatantRoot();
        EnsureRewardLedger();
        EnsureStageProgress();
        EnsureAfkRewardSource();
        ResetToStartScreen();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif

    [ContextMenu("Rebuild Persistent Scene Components")]
    private void RebuildPersistentSceneComponents()
    {
        EnsureViewCacheArrays();
        ClearGeneratedInterface();
        BuildInterface();
        EnsureCombatantRoot();
        EnsureRewardLedger();
        EnsureStageProgress();
        EnsureAfkRewardSource();
        ResetToStartScreen();
    }

    private void ApplyModeConfig()
    {
        if (modeConfig == null) return;

        startingFriendlyCount = Mathf.Clamp(modeConfig.StartingFriendlyCount, 1, MaxPartySize);
        baseEnemyCount = Mathf.Clamp(modeConfig.BaseEnemyCount, 1, MaxPartySize);
        addEnemyEveryStages = Mathf.Max(0, modeConfig.AddEnemyEveryStages);
        friendlyBaseHealth = Mathf.Max(1, modeConfig.FriendlyBaseHealth);
        friendlyBaseAttack = Mathf.Max(0, modeConfig.FriendlyBaseAttack);
        friendlyBaseDefense = Mathf.Max(0, modeConfig.FriendlyBaseDefense);
        friendlyBaseSpeed = Mathf.Max(1, modeConfig.FriendlyBaseSpeed);
        enemyBaseHealth = Mathf.Max(1, modeConfig.EnemyBaseHealth);
        enemyBaseAttack = Mathf.Max(0, modeConfig.EnemyBaseAttack);
        enemyBaseDefense = Mathf.Max(0, modeConfig.EnemyBaseDefense);
        enemyBaseSpeed = Mathf.Max(1, modeConfig.EnemyBaseSpeed);
        enemyHealthPerStage = Mathf.Max(0, modeConfig.EnemyHealthPerStage);
        enemyAttackPerStage = Mathf.Max(0, modeConfig.EnemyAttackPerStage);
        enemySpeedPerStage = Mathf.Max(0, modeConfig.EnemySpeedPerStage);
        turnInterval = Mathf.Max(0.1f, modeConfig.TurnInterval);
        attackAnimationDuration = Mathf.Max(0.05f, modeConfig.AttackAnimationDuration);
        hitFlashDuration = Mathf.Max(0.05f, modeConfig.HitFlashDuration);
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;

        ApplyModeConfig();
        ConfigureCamera();
        EnsureEventSystem();
        BuildInterface();
        EnsureCombatantRoot();
        EnsureRewardLedger();
        EnsureStageProgress();
        EnsureAfkRewardSource();
        ClearBackendAccountState();
        CaptureBackendAccountIdentity();
        ResetToStartScreen();
        ApplyPendingRoleAcquisitionRoute();
        formationRealtimeClosing = false;
        QueueBackendHomeLoad();
        QueueFormationRealtimeConnection();

        if (battleRoutine != null)
        {
            StopCoroutine(battleRoutine);
            battleRoutine = null;
        }

        battleRoutine = StartCoroutine(BattleLoop());
    }

    private void Update()
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            return;
        }

        EnsureBackendAccountIsolation();
        SyncAccelerationPolicyFromService();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;

        if (battleRoutine != null)
        {
            StopCoroutine(battleRoutine);
            battleRoutine = null;
        }

        if (backendHomeLoadRoutine != null)
        {
            StopCoroutine(backendHomeLoadRoutine);
            backendHomeLoadRoutine = null;
        }
        backendHomeLoadRunning = false;
        if (backendReferenceDataRoutine != null)
        {
            StopCoroutine(backendReferenceDataRoutine);
            backendReferenceDataRoutine = null;
        }
        backendReferenceDataLoaded = false;
        StopFormationRealtimeConnection();
    }

    private void QueueFormationRealtimeConnection()
    {
        if (!Application.isPlaying || formationRealtimeClosing || formationRealtimeConnected || formationRealtimeTransportOpen)
        {
            return;
        }

        if (formationRealtimeConnectionRoutine == null)
        {
            formationRealtimeConnectionRoutine = StartCoroutine(ConnectFormationRealtimeWhenReady());
        }
    }

    private IEnumerator ConnectFormationRealtimeWhenReady()
    {
        BackendGameService service = ResolveBackendService();
        float deadline = Time.unscaledTime + 8f;
        while (service != null
            && (!service.IsAuthenticated || service.IsConnecting)
            && Time.unscaledTime < deadline)
        {
            yield return null;
        }

        formationRealtimeConnectionRoutine = null;
        if (formationRealtimeClosing || !isActiveAndEnabled || service == null || !service.IsAuthenticated)
        {
            BeginFormationRealtimeFallbackPolling();
            yield break;
        }

        BeginFormationRealtimeConnection();
    }

    private void BeginFormationRealtimeConnection()
    {
        if (formationRealtimeClosing || !isActiveAndEnabled || !ShouldUseBackendApi())
        {
            return;
        }

        if (!IsFormationRealtimeAvailable())
        {
            BeginFormationRealtimeFallbackPolling();
            return;
        }

        if (formationRealtimeTransportOpen || formationRealtimeConnected)
        {
            return;
        }

        if (!TryBuildFormationRealtimeUrl(out string url, out string error))
        {
            Debug.LogWarning("Idle Adventure formation realtime URL is invalid: " + error, this);
            BeginFormationRealtimeFallbackPolling();
            return;
        }

        formationRealtimeClosing = false;
        formationRealtimeTransportOpen = true;
        ConnectFormationRealtime(url, "Bearer " + GetBackendSessionToken());
    }

    private static Type ResolveImportedRuntimeType(string typeName)
    {
        Type resolvedType = Type.GetType(typeName + ", WeijunGameProject");
        if (resolvedType != null)
        {
            return resolvedType;
        }

        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            resolvedType = assemblies[i].GetType(typeName);
            if (resolvedType != null)
            {
                return resolvedType;
            }
        }

        return null;
    }

    private static bool IsFormationRealtimeAvailable()
    {
        Type bridgeType = ResolveImportedRuntimeType("RealtimePlatformBridge");
        PropertyInfo availableProperty = bridgeType == null
            ? null
            : bridgeType.GetProperty("IsAvailable", BindingFlags.Public | BindingFlags.Static);
        if (availableProperty == null)
        {
            Debug.LogWarning("Idle Adventure formation realtime bridge is unavailable.");
            return false;
        }

        try
        {
            return availableProperty.GetValue(null, null) is bool available && available;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Idle Adventure formation realtime availability check failed: " + exception.Message);
            return false;
        }
    }

    private static bool TryBuildFormationRealtimeUrl(out string url, out string error)
    {
        url = string.Empty;
        error = string.Empty;
        Type configType = ResolveImportedRuntimeType("ApiRuntimeConfig");
        MethodInfo method = configType == null
            ? null
            : configType.GetMethod("TryBuildRealtimeUrl", BindingFlags.Public | BindingFlags.Static);
        if (method == null)
        {
            error = "ApiRuntimeConfig.TryBuildRealtimeUrl is unavailable.";
            return false;
        }

        object[] arguments = { string.Empty, string.Empty };
        try
        {
            bool success = method.Invoke(null, arguments) is bool result && result;
            url = arguments[0] as string ?? string.Empty;
            error = arguments[1] as string ?? string.Empty;
            return success;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private void ConnectFormationRealtime(string url, string authorization)
    {
        Type bridgeType = ResolveImportedRuntimeType("RealtimePlatformBridge");
        MethodInfo method = bridgeType == null
            ? null
            : bridgeType.GetMethod("Connect", BindingFlags.Public | BindingFlags.Static);
        if (method == null)
        {
            Debug.LogWarning("Idle Adventure formation realtime Connect method is unavailable.", this);
            formationRealtimeTransportOpen = false;
            BeginFormationRealtimeFallbackPolling();
            return;
        }

        try
        {
            method.Invoke(null, new object[] { this, url, authorization });
        }
        catch (Exception exception)
        {
            formationRealtimeTransportOpen = false;
            Debug.LogWarning("Idle Adventure formation realtime connection failed: " + exception.Message, this);
            BeginFormationRealtimeFallbackPolling();
        }
    }

    private static void SendFormationRealtime(string message)
    {
        Type bridgeType = ResolveImportedRuntimeType("RealtimePlatformBridge");
        MethodInfo method = bridgeType == null
            ? null
            : bridgeType.GetMethod("Send", BindingFlags.Public | BindingFlags.Static);
        if (method == null)
        {
            return;
        }

        try
        {
            method.Invoke(null, new object[] { message ?? string.Empty });
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Idle Adventure formation realtime heartbeat failed: " + exception.Message);
        }
    }

    private static void CloseFormationRealtime()
    {
        Type bridgeType = ResolveImportedRuntimeType("RealtimePlatformBridge");
        MethodInfo method = bridgeType == null
            ? null
            : bridgeType.GetMethod("Close", BindingFlags.Public | BindingFlags.Static);
        if (method == null)
        {
            return;
        }

        try
        {
            method.Invoke(null, null);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Idle Adventure formation realtime close failed: " + exception.Message);
        }
    }

    private int GetBackendPlayerId()
    {
        BackendGameService service = BackendGameService.Instance;
        return service == null ? 0 : service.PlayerId;
    }

    private string GetBackendSessionToken()
    {
        BackendGameService service = BackendGameService.Instance;
        return service == null ? string.Empty : service.Token;
    }

    private void CaptureBackendAccountIdentity()
    {
        BackendGameService service = ResolveBackendService();
        backendStatePlayerId = service != null && service.IsAuthenticated ? service.PlayerId : 0;
        backendStateToken = service != null && service.IsAuthenticated
            ? service.Token ?? string.Empty
            : string.Empty;
        backendStateIdentityInitialized = true;
    }

    private void EnsureBackendAccountIsolation()
    {
        BackendGameService service = BackendGameService.Instance;
        int currentPlayerId = service != null && service.IsAuthenticated ? service.PlayerId : 0;
        string currentToken = service != null && service.IsAuthenticated
            ? service.Token ?? string.Empty
            : string.Empty;
        if (!backendStateIdentityInitialized)
        {
            backendStatePlayerId = currentPlayerId;
            backendStateToken = currentToken;
            backendStateIdentityInitialized = true;
            return;
        }

        if (backendStatePlayerId == currentPlayerId
            && string.Equals(backendStateToken, currentToken, StringComparison.Ordinal))
        {
            return;
        }

        ClearBackendAccountState();
        backendStatePlayerId = currentPlayerId;
        backendStateToken = currentToken;
        backendStateIdentityInitialized = true;
        ResetToStartScreen();
        ApplyPendingRoleAcquisitionRoute();

        if (currentPlayerId > 0 && !string.IsNullOrEmpty(currentToken))
        {
            formationRealtimeClosing = false;
            QueueBackendHomeLoad();
            QueueFormationRealtimeConnection();
            if (afkRewardSource != null)
            {
                afkRewardSource.TryLoadServerState();
            }
        }
    }

    private void ClearBackendAccountState()
    {
        if (backendHomeLoadRoutine != null)
        {
            StopCoroutine(backendHomeLoadRoutine);
            backendHomeLoadRoutine = null;
        }
        backendHomeLoadRunning = false;

        if (backendReferenceDataRoutine != null)
        {
            StopCoroutine(backendReferenceDataRoutine);
            backendReferenceDataRoutine = null;
        }
        backendReferenceDataLoaded = false;
        backendReferenceDataStage = 0;
        backendEnemyPreview = null;

        StopFormationRealtimeConnection();
        formationRealtimeEventIds.Clear();
        formationRealtimeEventOrder.Clear();
        formationRealtimeHeartbeatSequence = 0;
        resumeFormationRealtimeAfterPause = false;

        backendModeActive = false;
        backendRequestPending = false;
        backendFormationDirty = false;
        backendFormationRevision++;
        backendSessionKey = string.Empty;
        backendRunningSessionKey = string.Empty;
        backendHighestStage = 0;
        backendNextStage = 1;
        selectedReplayStage = 0;
        backendAvailableNpcs = new IdleAdventurePlayerNpcResponse[0];
        backendRuntimeCombatants.Clear();
        backendStageClearRequests.Clear();
        staminaRequestPending = false;
        ResetAccelerationPolicy();

        acquisitionRouteApplied = false;
        acquisitionRouteModeCode = string.Empty;
        acquisitionRouteStage = 0;
        acquisitionRoutePoolKey = string.Empty;

        if (afkRewardSource != null)
        {
            afkRewardSource.ClearServerStateForAccountChange();
        }
    }

    public void OnRealtimeOpened(string message)
    {
        if (formationRealtimeClosing)
        {
            return;
        }

        formationRealtimeTransportOpen = true;
        if (!formationRealtimeConnected && formationRealtimeHandshakeRoutine == null)
        {
            formationRealtimeHandshakeRoutine = StartCoroutine(WaitForFormationRealtimeHandshake());
        }
    }

    public void OnRealtimeMessage(string json)
    {
        if (formationRealtimeClosing || !ShouldUseBackendApi())
        {
            StopFormationRealtimeConnection();
            return;
        }

        FormationRealtimeEvent realtimeEvent;
        try
        {
            realtimeEvent = JsonUtility.FromJson<FormationRealtimeEvent>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Idle Adventure realtime message could not be parsed: " + exception.Message, this);
            return;
        }

        if (realtimeEvent == null || string.IsNullOrEmpty(realtimeEvent.type))
        {
            return;
        }

        if (!string.IsNullOrEmpty(realtimeEvent.eventId))
        {
            if (!formationRealtimeEventIds.Add(realtimeEvent.eventId))
            {
                return;
            }

            formationRealtimeEventOrder.Enqueue(realtimeEvent.eventId);
            while (formationRealtimeEventOrder.Count > 512)
            {
                string oldestEventId = formationRealtimeEventOrder.Dequeue();
                formationRealtimeEventIds.Remove(oldestEventId);
            }
        }

        if (realtimeEvent.type == "CONNECTED")
        {
            ConfirmFormationRealtimeHandshake(realtimeEvent);
            return;
        }

        if (!formationRealtimeConnected || realtimeEvent.playerId != GetBackendPlayerId())
        {
            return;
        }

        if (realtimeEvent.type == "IDLE_ADVENTURE_FORMATION_CHANGED")
        {
            QueueFormationRealtimeRefresh();
        }
    }

    private void ConfirmFormationRealtimeHandshake(FormationRealtimeEvent realtimeEvent)
    {
        if (realtimeEvent.playerId != GetBackendPlayerId())
        {
            Debug.LogWarning("Idle Adventure realtime CONNECTED event belongs to a different player.", this);
            StopFormationRealtimeConnection();
            return;
        }

        if (formationRealtimeConnected)
        {
            return;
        }

        formationRealtimeConnected = true;
        formationRealtimeTransportOpen = true;
        formationRealtimeClosing = false;
        formationRealtimeHeartbeatSequence = 0;
        StopFormationRealtimeHandshake();
        if (formationRealtimeReconnectRoutine != null)
        {
            StopCoroutine(formationRealtimeReconnectRoutine);
            formationRealtimeReconnectRoutine = null;
        }

        StopFormationRealtimeHeartbeat();
        formationRealtimeHeartbeatRoutine = StartCoroutine(SendFormationRealtimeHeartbeats());
        StopFormationRealtimeFallbackPolling();
        backendReferenceDataLoaded = false;
        BackendGameService service = ResolveBackendService();
        if (service != null && service.IsAuthenticated)
        {
            StartCoroutine(RefreshBackendProgress(service));
            QueueBackendReferenceDataLoad(Mathf.Max(1, backendNextStage));
        }
        QueueFormationRealtimeRefresh();
    }

    private void QueueFormationRealtimeRefresh()
    {
        if (!Application.isPlaying || !ShouldUseBackendApi() || formationRealtimeClosing)
        {
            return;
        }

        formationRealtimeRefreshQueued = true;
        if (formationRealtimeRefreshRoutine == null)
        {
            formationRealtimeRefreshRoutine = StartCoroutine(RefreshFormationAfterRealtimeSignal());
        }
    }

    private IEnumerator RefreshFormationAfterRealtimeSignal()
    {
        while (backendHomeLoadRunning && isActiveAndEnabled && !formationRealtimeClosing)
        {
            yield return null;
        }

        while (formationRealtimeRefreshQueued
            && !formationRealtimeClosing
            && isActiveAndEnabled
            && ShouldUseBackendApi())
        {
            formationRealtimeRefreshQueued = false;
            yield return new WaitForSecondsRealtime(FormationRealtimeRefreshDelaySeconds);
            if (formationRealtimeRefreshQueued)
            {
                continue;
            }

            while (backendRequestPending && isActiveAndEnabled && !formationRealtimeClosing)
            {
                yield return null;
            }

            BackendGameService service = ResolveBackendService();
            if (service == null || !service.IsAuthenticated)
            {
                break;
            }

            int requestRevision = backendFormationRevision;
            BackendResult<IdleAdventureFormationResponse> result = null;
            yield return service.LoadIdleAdventureFormation(value => result = value);

            if (result == null || !result.Success || result.Data == null)
            {
                AddLog("Backend formation realtime refresh failed: " + BackendResultMessage(result));
                continue;
            }

            if (backendFormationDirty || requestRevision != backendFormationRevision)
            {
                Debug.Log("Idle Adventure formation refresh kept local edits because a newer local change exists.", this);
                continue;
            }

            ApplyBackendFormation(result.Data);
            if (formationPanelView != null && formationPanelView.HasBindings)
            {
                formationPanelView.SetStatus("Formation refreshed from server");
            }
        }

        formationRealtimeRefreshRoutine = null;
    }

    private void BeginFormationRealtimeFallbackPolling()
    {
        if (formationRealtimeFallbackRoutine == null
            && !formationRealtimeClosing
            && isActiveAndEnabled)
        {
            formationRealtimeFallbackRoutine = StartCoroutine(FormationRealtimeFallbackPolling());
        }
    }

    private IEnumerator FormationRealtimeFallbackPolling()
    {
        while (!formationRealtimeClosing && isActiveAndEnabled && ShouldUseBackendApi())
        {
            yield return new WaitForSecondsRealtime(FormationRealtimeFallbackIntervalSeconds);
            if (!formationRealtimeConnected && !formationRealtimeClosing)
            {
                QueueFormationRealtimeRefresh();
                QueueFormationRealtimeConnection();
            }
        }

        formationRealtimeFallbackRoutine = null;
    }

    private void StopFormationRealtimeConnection()
    {
        formationRealtimeClosing = true;
        formationRealtimeConnected = false;
        formationRealtimeTransportOpen = false;
        formationRealtimeRefreshQueued = false;

        if (formationRealtimeConnectionRoutine != null)
        {
            StopCoroutine(formationRealtimeConnectionRoutine);
            formationRealtimeConnectionRoutine = null;
        }

        if (formationRealtimeReconnectRoutine != null)
        {
            StopCoroutine(formationRealtimeReconnectRoutine);
            formationRealtimeReconnectRoutine = null;
        }

        StopFormationRealtimeHandshake();
        if (formationRealtimeRefreshRoutine != null)
        {
            StopCoroutine(formationRealtimeRefreshRoutine);
            formationRealtimeRefreshRoutine = null;
        }

        StopFormationRealtimeHeartbeat();
        StopFormationRealtimeFallbackPolling();
        CloseFormationRealtime();
    }

    private void StopFormationRealtimeHandshake()
    {
        if (formationRealtimeHandshakeRoutine != null)
        {
            StopCoroutine(formationRealtimeHandshakeRoutine);
            formationRealtimeHandshakeRoutine = null;
        }
    }

    private IEnumerator SendFormationRealtimeHeartbeats()
    {
        while (formationRealtimeConnected && !formationRealtimeClosing && ShouldUseBackendApi())
        {
            yield return new WaitForSecondsRealtime(25f);
            if (formationRealtimeConnected && !formationRealtimeClosing)
            {
                formationRealtimeHeartbeatSequence++;
                SendFormationRealtime("{\"type\":\"PING\",\"requestId\":\"idle-formation-"
                    + formationRealtimeHeartbeatSequence + "\"}");
            }
        }

        formationRealtimeHeartbeatRoutine = null;
    }

    private void StopFormationRealtimeHeartbeat()
    {
        if (formationRealtimeHeartbeatRoutine != null)
        {
            StopCoroutine(formationRealtimeHeartbeatRoutine);
            formationRealtimeHeartbeatRoutine = null;
        }
    }

    private IEnumerator WaitForFormationRealtimeHandshake()
    {
        yield return new WaitForSecondsRealtime(FormationRealtimeHandshakeTimeoutSeconds);
        formationRealtimeHandshakeRoutine = null;
        if (!formationRealtimeClosing && formationRealtimeTransportOpen && !formationRealtimeConnected)
        {
            Debug.LogWarning("Idle Adventure formation realtime handshake timed out.", this);
            formationRealtimeTransportOpen = false;
            CloseFormationRealtime();
            ScheduleFormationRealtimeReconnect();
            BeginFormationRealtimeFallbackPolling();
        }
    }

    private void ScheduleFormationRealtimeReconnect()
    {
        if (formationRealtimeReconnectRoutine == null
            && !formationRealtimeClosing
            && isActiveAndEnabled
            && ShouldUseBackendApi())
        {
            formationRealtimeReconnectRoutine = StartCoroutine(ReconnectFormationRealtime());
        }
    }

    private IEnumerator ReconnectFormationRealtime()
    {
        yield return new WaitForSecondsRealtime(5f);
        formationRealtimeReconnectRoutine = null;
        if (!formationRealtimeClosing && isActiveAndEnabled && ShouldUseBackendApi())
        {
            BeginFormationRealtimeConnection();
        }
    }

    private void StopFormationRealtimeFallbackPolling()
    {
        if (formationRealtimeFallbackRoutine != null)
        {
            StopCoroutine(formationRealtimeFallbackRoutine);
            formationRealtimeFallbackRoutine = null;
        }
    }

    public void OnRealtimeClosed(string message)
    {
        formationRealtimeConnected = false;
        formationRealtimeTransportOpen = false;
        StopFormationRealtimeHandshake();
        StopFormationRealtimeHeartbeat();
        QueueFormationRealtimeRefresh();
        ScheduleFormationRealtimeReconnect();
        BeginFormationRealtimeFallbackPolling();
    }

    public void OnRealtimeError(string message)
    {
        formationRealtimeConnected = false;
        formationRealtimeTransportOpen = false;
        StopFormationRealtimeHandshake();
        StopFormationRealtimeHeartbeat();
        if (!string.IsNullOrEmpty(message)
            && message.IndexOf("cannot send the required Authorization", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            formationRealtimeClosing = true;
            Debug.LogWarning("Idle Adventure formation realtime authorization failed: " + message, this);
            return;
        }

        if (!formationRealtimeClosing && !string.IsNullOrEmpty(message))
        {
            Debug.LogWarning("Idle Adventure formation realtime error: " + message, this);
        }

        QueueFormationRealtimeRefresh();
        ScheduleFormationRealtimeReconnect();
        BeginFormationRealtimeFallbackPolling();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            resumeFormationRealtimeAfterPause = ShouldUseBackendApi();
            StopFormationRealtimeConnection();
        }
        else if (resumeFormationRealtimeAfterPause && isActiveAndEnabled && ShouldUseBackendApi())
        {
            resumeFormationRealtimeAfterPause = false;
            formationRealtimeClosing = false;
            backendReferenceDataLoaded = false;
            QueueBackendHomeLoad();
            if (afkRewardSource != null) afkRewardSource.TryLoadServerState();
            QueueFormationRealtimeRefresh();
            QueueFormationRealtimeConnection();
        }
    }

    #pragma warning disable 0649
    [Serializable]
    private sealed class FormationRealtimeEvent
    {
        public string type;
        public string requestId;
        public string eventId;
        public int playerId;
        public string serverTime;
    }
    #pragma warning restore 0649

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public void SetFriendlyPartySize(int count)
    {
        if (!CanUseLocalCombatSimulation())
        {
            Debug.LogWarning("SetFriendlyPartySize is local simulation only and is disabled while Idle Adventure is backend-authoritative.", this);
            return;
        }

        startingFriendlyCount = Mathf.Clamp(count, 1, MaxPartySize);
        InitializeDefaultFormation();
        BuildFriendlyParty(startingFriendlyCount);
        round = 1;
        AddLog($"Friendly party size changed to {DeployedCount(friendlies)}.");
        RefreshFormationView();
        RefreshBattleView();
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public void SetEnemyPartySize(int count)
    {
        if (!CanUseLocalCombatSimulation())
        {
            Debug.LogWarning("SetEnemyPartySize is local simulation only and is disabled while Idle Adventure is backend-authoritative.", this);
            return;
        }

        BuildEnemyParty(Mathf.Clamp(count, 1, MaxPartySize));
        round = 1;
        AddLog($"Enemy party size changed to {DeployedCount(enemies)}.");
        RefreshBattleView();
    }

    public void AutoFillFormation()
    {
        if (!ShouldUseBackendApi())
        {
            AddLog("Backend formation auto-fill requires authentication.");
            if (startPanelView != null) startPanelView.SetContent("Idle Adventure mode", "Login is required before editing formation.");
            UpdateButtonStates();
            return;
        }

        if (!backendRequestPending) StartCoroutine(AutoFillBackendFormationRoutine());
    }

    public void ClearFormation()
    {
        if (!ShouldUseBackendApi())
        {
            AddLog("Backend formation clear requires authentication.");
            if (startPanelView != null) startPanelView.SetContent("Idle Adventure mode", "Login is required before editing formation.");
            UpdateButtonStates();
            return;
        }

        if (!backendRequestPending) StartCoroutine(ClearBackendFormationRoutine());
    }

    public void OpenFormationPanel()
    {
        if (formationPanelView == null) return;

        formationPanelView.ShowFormationWindow();
        if (ShouldUseBackendApi() && !HasBackendNpcDefinitionsReady())
        {
            QueueBackendHomeLoad();
        }

        if (ShouldUseBackendApi())
        {
            QueueFormationRealtimeRefresh();
        }

        RefreshFormationView();
    }

    public void CloseFormationPanel()
    {
        if (formationPanelView == null) return;

        formationPanelView.HideFormationWindow();
        RefreshFormationView();
    }

    public void AssignFormationSlot(int slotIndex, int definitionIndex)
    {
        if (slotIndex < 0 || slotIndex >= MaxPartySize) return;
        if (definitionIndex < 0 || definitionIndex >= AvailableFriendlyDefinitionCount()) return;
        if (!ShouldUseBackendApi())
        {
            AddLog("Backend formation save requires authentication.");
            QueueBackendHomeLoad();
            return;
        }

        Debug.Log($"Formation assign requested: slot={slotIndex}, npcIndex={definitionIndex}, backend={ShouldUseBackendApi()}, backendNpcCount={(backendAvailableNpcs != null ? backendAvailableNpcs.Length : 0)}, backendDefinitionId={ResolveBackendDefinitionId(definitionIndex)}", this);

        for (int i = 0; i < MaxPartySize; i++)
        {
            if (friendlyFormation[i] == definitionIndex) friendlyFormation[i] = -1;
        }

        friendlyFormation[slotIndex] = definitionIndex;
        backendFormationDirty = true;
        backendFormationRevision++;
        RefreshFormationView();
        UpdateButtonStates();
        SaveBackendFormationIfAvailable();
    }

    public void ClearFormationSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MaxPartySize) return;
        if (!ShouldUseBackendApi())
        {
            AddLog("Backend formation save requires authentication.");
            QueueBackendHomeLoad();
            return;
        }

        friendlyFormation[slotIndex] = -1;
        backendFormationDirty = true;
        backendFormationRevision++;
        RefreshFormationView();
        UpdateButtonStates();
        SaveBackendFormationIfAvailable();
    }

    public void ApplyAttackBuffToFriendlySlot(int slotIndex)
    {
        ApplyBackendBuffPresetToFriendlySlot(slotIndex, (int)BackendBattleFocusBuffPresetId);
    }

    public void ApplyDefenseDebuffToEnemySlot(int slotIndex)
    {
        ApplyBackendBuffPresetToEnemySlot(slotIndex, (int)BackendArmorBreakBuffPresetId);
    }

    public void ApplyBackendBuffPresetToFriendlySlot(int slotIndex, int buffPresetId)
    {
        TryRequestBackendBuff("FRIENDLY", slotIndex, buffPresetId);
    }

    public void ApplyBackendBuffPresetToEnemySlot(int slotIndex, int buffPresetId)
    {
        TryRequestBackendBuff("ENEMY", slotIndex, buffPresetId);
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [Obsolete("Local buff mutation is disabled. Use ApplyBackendBuffPresetToFriendlySlot or ApplyBackendBuffPresetToEnemySlot.")]
    public bool ApplyBuffPresetToFriendlySlot(int slotIndex, int presetIndex)
    {
        return DeprecatedLocalBuffApi();
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [Obsolete("Local buff mutation is disabled. Use ApplyBackendBuffPresetToFriendlySlot or ApplyBackendBuffPresetToEnemySlot.")]
    public bool ApplyBuffPresetToEnemySlot(int slotIndex, int presetIndex)
    {
        return DeprecatedLocalBuffApi();
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [Obsolete("Local buff mutation is disabled. Use backend /buffs/apply with a configured buff_preset_id.")]
    public bool ApplyStatBuffToFriendlySlot(int slotIndex, string buffId, string displayName, bool isBenefit, int durationRounds, int attackModifier, int defenseModifier, int speedModifier)
    {
        return DeprecatedLocalBuffApi();
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [Obsolete("Local buff mutation is disabled. Use backend /buffs/apply with a configured buff_preset_id.")]
    public bool ApplyStatBuffToEnemySlot(int slotIndex, string buffId, string displayName, bool isBenefit, int durationRounds, int attackModifier, int defenseModifier, int speedModifier)
    {
        return DeprecatedLocalBuffApi();
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [Obsolete("Local buff mutation is disabled. Character skill buffs are applied by backend round resolution.")]
    public bool ApplySkillStatBuffToFriendlySlot(int slotIndex, string buffId, string displayName, bool isBenefit, int durationRounds, int attackModifier, int defenseModifier, int speedModifier)
    {
        return DeprecatedLocalBuffApi();
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [Obsolete("Local buff mutation is disabled. Character skill buffs are applied by backend round resolution.")]
    public bool ApplySkillStatBuffToEnemySlot(int slotIndex, string buffId, string displayName, bool isBenefit, int durationRounds, int attackModifier, int defenseModifier, int speedModifier)
    {
        return DeprecatedLocalBuffApi();
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [Obsolete("Local buff mutation is disabled. Use backend /buffs/apply with a configured buff_preset_id.")]
    public bool ApplyOtherStatBuffToFriendlySlot(int slotIndex, string buffId, string displayName, bool isBenefit, int durationRounds, int attackModifier, int defenseModifier, int speedModifier)
    {
        return DeprecatedLocalBuffApi();
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [Obsolete("Local buff mutation is disabled. Use backend /buffs/apply with a configured buff_preset_id.")]
    public bool ApplyOtherStatBuffToEnemySlot(int slotIndex, string buffId, string displayName, bool isBenefit, int durationRounds, int attackModifier, int defenseModifier, int speedModifier)
    {
        return DeprecatedLocalBuffApi();
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [Obsolete("Local buff mutation is disabled. Character skill buffs are applied by backend round resolution.")]
    public bool ApplySkillNormalDamageBuffToFriendlySlot(int slotIndex, string buffId, string displayName, bool isBenefit, int durationRounds, float multiplier)
    {
        return DeprecatedLocalBuffApi();
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [Obsolete("Local buff mutation is disabled. Character skill buffs are applied by backend round resolution.")]
    public bool ApplySkillNormalDamageBuffToEnemySlot(int slotIndex, string buffId, string displayName, bool isBenefit, int durationRounds, float multiplier)
    {
        return DeprecatedLocalBuffApi();
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [Obsolete("Local buff mutation is disabled. Use backend /buffs/apply with a configured buff_preset_id.")]
    public bool ApplyOtherNormalDamageBuffToFriendlySlot(int slotIndex, string buffId, string displayName, bool isBenefit, int durationRounds, float multiplier)
    {
        return DeprecatedLocalBuffApi();
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [Obsolete("Local buff mutation is disabled. Use backend /buffs/apply with a configured buff_preset_id.")]
    public bool ApplyOtherNormalDamageBuffToEnemySlot(int slotIndex, string buffId, string displayName, bool isBenefit, int durationRounds, float multiplier)
    {
        return DeprecatedLocalBuffApi();
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [Obsolete("Local buff mutation is disabled. Character skill buffs are applied by backend round resolution.")]
    public bool ApplySkillAttributeDamageBuffToFriendlySlot(int slotIndex, string buffId, string displayName, bool isBenefit, int durationRounds, float multiplier, IdleAdventureDamageAttribute affectedAttribute)
    {
        return DeprecatedLocalBuffApi();
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [Obsolete("Local buff mutation is disabled. Character skill buffs are applied by backend round resolution.")]
    public bool ApplySkillAttributeDamageBuffToEnemySlot(int slotIndex, string buffId, string displayName, bool isBenefit, int durationRounds, float multiplier, IdleAdventureDamageAttribute affectedAttribute)
    {
        return DeprecatedLocalBuffApi();
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [Obsolete("Local buff mutation is disabled. Use backend /buffs/apply with a configured buff_preset_id.")]
    public bool ApplyOtherAttributeDamageBuffToFriendlySlot(int slotIndex, string buffId, string displayName, bool isBenefit, int durationRounds, float multiplier, IdleAdventureDamageAttribute affectedAttribute)
    {
        return DeprecatedLocalBuffApi();
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [Obsolete("Local buff mutation is disabled. Use backend /buffs/apply with a configured buff_preset_id.")]
    public bool ApplyOtherAttributeDamageBuffToEnemySlot(int slotIndex, string buffId, string displayName, bool isBenefit, int durationRounds, float multiplier, IdleAdventureDamageAttribute affectedAttribute)
    {
        return DeprecatedLocalBuffApi();
    }

    private bool DeprecatedLocalBuffApi()
    {
        Debug.LogWarning("Local Idle Adventure buff mutation is disabled. Buff changes must be requested from backend /buffs/apply.", this);
        return false;
    }

    private bool TryRequestBackendBuff(string targetSide, int slotIndex, long buffPresetId)
    {
        if (slotIndex < 0 || slotIndex >= MaxPartySize || buffPresetId <= 0L) return false;

        BackendGameService service = ResolveBackendService();
        if (service == null || !service.IsAuthenticated || string.IsNullOrEmpty(backendSessionKey) || !backendModeActive)
        {
            AddLog("Backend buff apply requires an active authenticated battle session.");
            return false;
        }

        if (backendRequestPending)
        {
            AddLog("Backend is busy. Buff apply skipped.");
            return false;
        }

        StartCoroutine(ApplyBackendBuffRoutine(targetSide, slotIndex, buffPresetId));
        return true;
    }

    private IEnumerator ApplyBackendBuffRoutine(string targetSide, int slotIndex, long buffPresetId)
    {
        BackendGameService service = ResolveBackendService();
        if (service == null || !service.IsAuthenticated || string.IsNullOrEmpty(backendSessionKey)) yield break;

        backendRequestPending = true;
        UpdateButtonStates();
        BackendResult<IdleAdventureBuffAppliedResponse> result = null;
        IdleAdventureApplyBuffRequest request = new IdleAdventureApplyBuffRequest
        {
            target_side = targetSide,
            slot_index = slotIndex,
            buff_preset_id = buffPresetId,
            source = "OTHER"
        };
        yield return service.ApplyIdleAdventureBuff(backendSessionKey, request, value => result = value);
        backendRequestPending = false;

        if (result != null && result.Success && result.Data != null)
        {
            ApplyBackendBuffTargetState(result.Data.target, targetSide);
            if (!string.IsNullOrEmpty(result.Data.log)) AddLog(result.Data.log);
            else AddLog($"Backend buff {buffPresetId} applied to {targetSide} slot {slotIndex + 1}.");
        }
        else
        {
            AddLog($"Backend buff apply failed: {BackendResultMessage(result)}");
        }

        RefreshBattleView();
        UpdateButtonStates();
    }

    private bool ApplyStatBuffToParty(List<IdleAdventureCombatant> party, int slotIndex, string buffId, string displayName, bool isBenefit, IdleAdventureBuffSource source, int durationRounds, int attackModifier, int defenseModifier, int speedModifier)
    {
        IdleAdventureBuffCategory category = isBenefit ? IdleAdventureBuffCategory.Benefit : IdleAdventureBuffCategory.Detriment;
        IdleAdventureBattleBuff buff = IdleAdventureBattleBuff.StatBuff(buffId, displayName, category, source, durationRounds, attackModifier, defenseModifier, speedModifier);
        return ApplyRuntimeBuffToParty(party, slotIndex, buff);
    }

    private bool ApplyDamageMultiplierBuffToParty(List<IdleAdventureCombatant> party, int slotIndex, string buffId, string displayName, bool isBenefit, IdleAdventureBuffSource source, IdleAdventureBuffEffectType effectType, int durationRounds, float normalDamageMultiplier, float attributeDamageMultiplier, IdleAdventureDamageAttribute affectedAttribute)
    {
        IdleAdventureBuffCategory category = isBenefit ? IdleAdventureBuffCategory.Benefit : IdleAdventureBuffCategory.Detriment;
        float sign = isBenefit ? 1f : -1f;
        IdleAdventureBattleBuff buff = IdleAdventureBattleBuff.DamageMultiplierBuff(buffId, displayName, category, source, effectType, durationRounds, Mathf.Abs(normalDamageMultiplier) * sign, Mathf.Abs(attributeDamageMultiplier) * sign, affectedAttribute);
        return ApplyRuntimeBuffToParty(party, slotIndex, buff);
    }

    private bool ApplyBuffPresetToParty(List<IdleAdventureCombatant> party, int slotIndex, int presetIndex)
    {
        if (modeConfig == null || modeConfig.BuffPresets == null || presetIndex < 0 || presetIndex >= modeConfig.BuffPresets.Length) return false;
        IdleAdventureBuffDefinition preset = modeConfig.BuffPresets[presetIndex];
        if (preset == null) return false;

        return ApplyRuntimeBuffToParty(party, slotIndex, preset.CreateRuntimeBuff());
    }

    private bool ApplyRuntimeBuffToParty(List<IdleAdventureCombatant> party, int slotIndex, IdleAdventureBattleBuff buff)
    {
        if (buff == null) return false;

        if (TryApplyBuff(party, slotIndex, buff))
        {
            string verb = buff.Category == IdleAdventureBuffCategory.Benefit ? "gained" : "suffered";
            AddLog($"{party[slotIndex].CombatantName} {verb} {buff.DisplayName} from {buff.SourceLabel}.");
            RefreshBattleView();
            return true;
        }

        return false;
    }

    private void EnsureViewCacheArrays()
    {
        if (friendlyViews == null || friendlyViews.Length != MaxPartySize)
        {
            IdleAdventureActorView[] resized = new IdleAdventureActorView[MaxPartySize];
            if (friendlyViews != null)
            {
                int copyCount = Mathf.Min(friendlyViews.Length, resized.Length);
                for (int i = 0; i < copyCount; i++) resized[i] = friendlyViews[i];
            }

            friendlyViews = resized;
        }

        if (enemyViews == null || enemyViews.Length != MaxPartySize)
        {
            IdleAdventureActorView[] resized = new IdleAdventureActorView[MaxPartySize];
            if (enemyViews != null)
            {
                int copyCount = Mathf.Min(enemyViews.Length, resized.Length);
                for (int i = 0; i < copyCount; i++) resized[i] = enemyViews[i];
            }

            enemyViews = resized;
        }
    }

    private void ConfigureCamera()
    {
        if (sceneCamera == null) sceneCamera = Camera.main;
        if (sceneCamera == null)
        {
            Transform existingCamera = transform.root != null ? transform.root.Find("Main Camera") : null;
            if (existingCamera != null) sceneCamera = existingCamera.GetComponent<Camera>();
        }

        if (sceneCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            sceneCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }

        sceneCamera.clearFlags = CameraClearFlags.SolidColor;
        sceneCamera.backgroundColor = new Color(0.08f, 0.09f, 0.11f);
        sceneCamera.orthographic = true;
        sceneCamera.orthographicSize = 5f;
        sceneCamera.transform.position = new Vector3(0f, 0f, -10f);
    }

    private void EnsureEventSystem()
    {
        if (sceneEventSystem == null) sceneEventSystem = FindObjectOfType<EventSystem>();
        if (sceneEventSystem != null) return;

        GameObject eventSystemObject = new GameObject("EventSystem");
        sceneEventSystem = eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private void EnsureCombatantRoot()
    {
        if (combatantRoot != null) return;
        if (combatantRootOverride != null)
        {
            combatantRoot = combatantRootOverride;
            return;
        }

        Transform existingRoot = transform.Find("Combatants");
        if (existingRoot != null)
        {
            combatantRoot = existingRoot;
            return;
        }

        GameObject rootObject = new GameObject("Combatants");
        rootObject.transform.SetParent(transform, false);
        combatantRoot = rootObject.transform;
    }

    private void EnsureRewardLedger()
    {
        if (rewardLedger != null) return;

        Transform existingLedger = transform.Find("Idle Adventure Reward Ledger");
        if (existingLedger != null)
        {
            rewardLedger = existingLedger.GetComponent<IdleAdventureRewardLedger>();
            if (rewardLedger != null) return;
        }

        IdleAdventureRewardLedger existingComponent = GetComponentInChildren<IdleAdventureRewardLedger>(true);
        if (existingComponent != null)
        {
            rewardLedger = existingComponent;
            return;
        }

        GameObject ledgerObject = new GameObject("Idle Adventure Reward Ledger");
        ledgerObject.transform.SetParent(transform, false);
        rewardLedger = ledgerObject.AddComponent<IdleAdventureRewardLedger>();
    }

    private void EnsureStageProgress()
    {
        if (stageProgress != null) return;

        Transform existingProgress = transform.Find("Idle Adventure Stage Progress");
        if (existingProgress != null)
        {
            stageProgress = existingProgress.GetComponent<IdleAdventureStageProgress>();
            if (stageProgress != null) return;
        }

        IdleAdventureStageProgress existingComponent = GetComponentInChildren<IdleAdventureStageProgress>(true);
        if (existingComponent != null)
        {
            stageProgress = existingComponent;
            return;
        }

        GameObject progressObject = new GameObject("Idle Adventure Stage Progress");
        progressObject.transform.SetParent(transform, false);
        stageProgress = progressObject.AddComponent<IdleAdventureStageProgress>();
    }

    private void EnsureAfkRewardSource()
    {
        EnsureRewardLedger();
        EnsureStageProgress();

        Canvas rewardCanvas = EnsureAfkRewardCanvas();
        if (rewardCanvas == null) return;

        IdleAdventureAfkRewardSource existingSource = afkRewardSource != null ? afkRewardSource : rewardCanvas.GetComponentInChildren<IdleAdventureAfkRewardSource>(true);
        if (existingSource != null && existingSource.HasViewBindings)
        {
            afkRewardSource = existingSource;
            afkRewardSource.SetRewardLedger(rewardLedger);
            afkRewardSource.SetHighestClearedStage(HighestClearedStage());
            return;
        }

        if (existingSource != null) DestroyRuntimeObject(existingSource.gameObject);
        BuildAfkRewardWidget(rewardCanvas.GetComponent<RectTransform>());
    }

    private void BuildAfkRewardWidget(RectTransform root)
    {
        if (root == null) return;

        RectTransform panel = CreatePanel("AFK Reward Widget", root, new Color(0.10f, 0.12f, 0.14f, 0.94f));
        Anchor(panel, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-226f, 28f), new Vector2(-28f, 196f));

        Image iconImage = CreateImage("Reward Icon", panel, new Color(0.32f, 0.36f, 0.40f, 1f));
        Anchor(iconImage.rectTransform, new Vector2(0f, 0.22f), new Vector2(0.58f, 1f), new Vector2(12f, -10f), new Vector2(-8f, -12f));

        Image progressFill = CreateImage("Progress Fill", iconImage.transform, new Color(0.42f, 0.78f, 0.46f, 0.92f));
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = 0;
        progressFill.fillAmount = 0f;
        progressFill.raycastTarget = false;
        Anchor(progressFill.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.16f), Vector2.zero, Vector2.zero);

        Button collectButton = CreateButton("Collect Reward Button", panel, "领取奖励");
        Anchor(collectButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.58f, 0.22f), new Vector2(12f, 4f), new Vector2(-8f, -2f));
        TextMeshProUGUI collectButtonText = collectButton.GetComponentInChildren<TextMeshProUGUI>(true);

        TextMeshProUGUI iconText = CreateText("Icon Text", iconImage.transform, "G", 34, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(iconText.rectTransform);

        TextMeshProUGUI amountText = CreateText("Amount Text", panel, "x0", 21, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(amountText.rectTransform, new Vector2(0.58f, 0.50f), new Vector2(1f, 0.86f), new Vector2(0f, 0f), new Vector2(-10f, 0f));

        TextMeshProUGUI timerText = CreateText("Timer Text", panel, "30s", 16, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(timerText.rectTransform, new Vector2(0.58f, 0.34f), new Vector2(1f, 0.50f), new Vector2(0f, 0f), new Vector2(-10f, 0f));

        staminaText = CreateText("Stamina Text", panel, "体力 --", 14, FontStyles.Normal, TextAlignmentOptions.Center);
        Anchor(staminaText.rectTransform, new Vector2(0.58f, 0.86f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(-10f, 0f));

        accelerateButton = CreateButton("Accelerate Button", panel, "加速");
        Anchor(accelerateButton.GetComponent<RectTransform>(), new Vector2(0.58f, 0f), new Vector2(1f, 0.18f), new Vector2(0f, 4f), new Vector2(-10f, -2f));
        accelerateButton.onClick.AddListener(AccelerateAfkReward);
        accelerationText = CreateText("Acceleration Text", panel, string.Empty, 11, FontStyles.Normal, TextAlignmentOptions.Center);
        Anchor(accelerationText.rectTransform, new Vector2(0.58f, 0.18f), new Vector2(1f, 0.34f), new Vector2(0f, 0f), new Vector2(-10f, 0f));

        afkRewardSource = panel.gameObject.AddComponent<IdleAdventureAfkRewardSource>();
        afkRewardSource.Initialize(rewardLedger, collectButton, iconImage, progressFill, iconText, amountText, timerText, collectButtonText);
        afkRewardSource.SetHighestClearedStage(HighestClearedStage());
        RefreshStaminaView();
    }

    private void BuildInterface()
    {
        if (interfaceBuildInProgress) return;
        interfaceBuildInProgress = true;

        try
        {
            IdleAdventureBattleHud existingHud = ResolveExistingBattleHud();
            if (existingHud != null && existingHud.HasBindings)
            {
                EnsureBattleBackgroundCanvas();
                EnsureStartCanvas();
                BindBattleHud(existingHud);
                EnsurePersistentPanels(existingHud.Root);
                return;
            }

            ClearGeneratedInterface();
            EnsureBattleBackgroundCanvas();
            startCanvas = EnsureStartCanvas();

            canvas = CreateCanvas();
            RectTransform root = canvas.GetComponent<RectTransform>();
            ConfigureCombatScreenVisible(false);

            Image background = CreateImage("Background", root, new Color(0f, 0f, 0f, 0f));
            background.raycastTarget = false;
            Stretch(background.rectTransform);

            RectTransform header = CreatePanel("Header", root, new Color(0.13f, 0.15f, 0.18f, 0.94f));
            Anchor(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -74f), new Vector2(0f, 0f));

            stageText = CreateText("Stage Text", header, "Idle Adventure mode", 28, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(stageText.rectTransform, new Vector2(0f, 0f), new Vector2(0.58f, 1f), new Vector2(28f, 0f), new Vector2(-8f, 0f));

            turnText = CreateText("Turn Text", header, string.Empty, 20, FontStyles.Bold, TextAlignmentOptions.Right);
            Anchor(turnText.rectTransform, new Vector2(0.58f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-28f, 0f));

            RectTransform battlefield = CreatePanel("Battlefield", root, new Color(0f, 0f, 0f, 0f));
            Anchor(battlefield, new Vector2(0f, 0.23f), new Vector2(1f, 1f), new Vector2(28f, 20f), new Vector2(-28f, -92f));

            RectTransform playerPanel = CreatePanel("Friendly Party Area", battlefield, new Color(0.12f, 0.18f, 0.22f, 0.78f));
            Anchor(playerPanel, new Vector2(0f, 0f), new Vector2(0.48f, 1f), Vector2.zero, Vector2.zero);

            RectTransform enemyPanel = CreatePanel("Enemy Party Area", battlefield, new Color(0.22f, 0.13f, 0.14f, 0.78f));
            Anchor(enemyPanel, new Vector2(0.52f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            RectTransform friendlySlotsRoot = BuildPartyPanel(playerPanel, true);
            RectTransform enemySlotsRoot = BuildPartyPanel(enemyPanel, false);

            RectTransform footer = CreatePanel("Battle Log Panel", root, new Color(0.12f, 0.12f, 0.14f, 0.94f));
            Anchor(footer, new Vector2(0f, 0f), new Vector2(1f, 0.23f), new Vector2(28f, 24f), new Vector2(-28f, -14f));

            logText = CreateText("Battle Log", footer, string.Empty, 18, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Anchor(logText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(20f, 18f), new Vector2(-180f, -18f));

            pauseButton = CreateButton("Pause Button", footer, "Pause");
            Anchor(pauseButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-152f, -28f), new Vector2(-20f, 28f));
            pauseButton.onClick.AddListener(TogglePause);
            pauseButtonText = pauseButton.GetComponentInChildren<TextMeshProUGUI>();

            resultText = CreateText("Result Text", footer, string.Empty, 18, FontStyles.Bold, TextAlignmentOptions.Center);
            Anchor(resultText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-152f, 16f), new Vector2(-20f, 54f));

            battleHudView = canvas.gameObject.AddComponent<IdleAdventureBattleHud>();
            battleHudView.Initialize(canvas, root, stageText, turnText, friendlyCountText, enemyCountText, logText, resultText, pauseButton, pauseButtonText, friendlySlotsRoot, enemySlotsRoot);

            BuildStartPanel(startCanvas.GetComponent<RectTransform>());
            BuildEndPanel(root);
            BuildPausePanel(root);
            HideEndPanel();
            HidePausePanel();
        }
        finally
        {
            interfaceBuildInProgress = false;
        }
    }

    private IdleAdventureBattleHud ResolveExistingBattleHud()
    {
        if (battleHudView != null && battleHudView.HasBindings) return battleHudView;

        IdleAdventureBattleHud[] huds = GetComponentsInChildren<IdleAdventureBattleHud>(true);
        for (int i = 0; i < huds.Length; i++)
        {
            if (huds[i] != null && huds[i].HasBindings) return huds[i];
        }

        return null;
    }

    private void BindBattleHud(IdleAdventureBattleHud hud)
    {
        battleHudView = hud;
        canvas = hud.Canvas;
        ConfigureHudCanvas(canvas);
        ConfigureHudSurfaceTransparency(hud.Root);
        stageText = hud.StageText;
        turnText = hud.TurnText;
        friendlyCountText = hud.FriendlyCountText;
        enemyCountText = hud.EnemyCountText;
        logText = hud.LogText;
        resultText = hud.ResultText;
        pauseButton = hud.PauseButton;
        pauseButtonText = hud.PauseButtonText;

        if (pauseButton != null)
        {
            pauseButton.onClick.RemoveAllListeners();
            pauseButton.onClick.AddListener(TogglePause);
        }

        BindActorViews(hud.FriendlySlotsRoot, true);
        BindActorViews(hud.EnemySlotsRoot, false);
    }

    private Canvas EnsureBattleBackgroundCanvas()
    {
        Transform existingCanvasTransform = transform.Find(BackgroundCanvasName);
        if (existingCanvasTransform == null)
        {
            GameObject canvasObject = new GameObject(BackgroundCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            backgroundCanvas = canvasObject.GetComponent<Canvas>();
        }
        else
        {
            backgroundCanvas = EnsureComponent<Canvas>(existingCanvasTransform.gameObject);
            EnsureComponent<CanvasScaler>(existingCanvasTransform.gameObject);
        }

        ConfigureBackgroundCanvas(backgroundCanvas);
        RectTransform root = backgroundCanvas.GetComponent<RectTransform>();

        Image baseImage = EnsureImage("Battle Background", root, new Color(0.055f, 0.06f, 0.075f, 1f));
        Stretch(baseImage.rectTransform);
        baseImage.rectTransform.SetSiblingIndex(0);

        Image distantLight = EnsureImage("Distant Light", root, new Color(0.15f, 0.20f, 0.24f, 0.86f));
        Anchor(distantLight.rectTransform, new Vector2(0f, 0.38f), new Vector2(1f, 0.72f), Vector2.zero, Vector2.zero);
        distantLight.rectTransform.SetSiblingIndex(1);

        Image battleGround = EnsureImage("Battle Ground", root, new Color(0.10f, 0.13f, 0.12f, 1f));
        Anchor(battleGround.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.34f), Vector2.zero, Vector2.zero);
        battleGround.rectTransform.SetSiblingIndex(2);

        Image centerLane = EnsureImage("Center Lane", root, new Color(0.18f, 0.17f, 0.14f, 0.62f));
        Anchor(centerLane.rectTransform, new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.28f), Vector2.zero, Vector2.zero);
        centerLane.rectTransform.SetSiblingIndex(3);

        return backgroundCanvas;
    }

    private Canvas EnsureStartCanvas()
    {
        Transform existingCanvasTransform = transform.Find(StartCanvasName);
        if (existingCanvasTransform == null)
        {
            GameObject canvasObject = new GameObject(StartCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            startCanvas = canvasObject.GetComponent<Canvas>();
        }
        else
        {
            startCanvas = EnsureComponent<Canvas>(existingCanvasTransform.gameObject);
            EnsureComponent<CanvasScaler>(existingCanvasTransform.gameObject);
            EnsureComponent<GraphicRaycaster>(existingCanvasTransform.gameObject);
        }

        ConfigureStartCanvas(startCanvas);
        return startCanvas;
    }

    private Canvas EnsureAfkRewardCanvas()
    {
        Transform existingCanvasTransform = transform.Find(AfkRewardCanvasName);
        if (existingCanvasTransform == null && startCanvas != null)
        {
            existingCanvasTransform = startCanvas.transform.Find(AfkRewardCanvasName);
        }
        if (existingCanvasTransform == null)
        {
            GameObject canvasObject = new GameObject(AfkRewardCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            afkRewardCanvas = canvasObject.GetComponent<Canvas>();
        }
        else
        {
            if (existingCanvasTransform.parent != transform)
            {
                existingCanvasTransform.SetParent(transform, false);
            }
            afkRewardCanvas = EnsureComponent<Canvas>(existingCanvasTransform.gameObject);
            EnsureComponent<CanvasScaler>(existingCanvasTransform.gameObject);
            EnsureComponent<GraphicRaycaster>(existingCanvasTransform.gameObject);
        }

        ConfigureAfkRewardCanvas(afkRewardCanvas);
        return afkRewardCanvas;
    }

    private void ConfigureBackgroundCanvas(Canvas targetCanvas)
    {
        if (targetCanvas == null) return;

        targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        targetCanvas.overrideSorting = true;
        targetCanvas.sortingOrder = -20;

        CanvasScaler scaler = targetCanvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }
    }

    private void ConfigureStartCanvas(Canvas targetCanvas)
    {
        if (targetCanvas == null) return;

        targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        targetCanvas.overrideSorting = true;
        targetCanvas.sortingOrder = 40;

        CanvasScaler scaler = targetCanvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }
    }

    private void ConfigureAfkRewardCanvas(Canvas targetCanvas)
    {
        if (targetCanvas == null) return;

        targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        targetCanvas.overrideSorting = true;
        targetCanvas.sortingOrder = 70;

        CanvasScaler scaler = targetCanvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }
    }

    private void ConfigureHudCanvas(Canvas targetCanvas)
    {
        if (targetCanvas == null) return;

        targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        targetCanvas.overrideSorting = true;
        targetCanvas.sortingOrder = 10;
    }

    private void ConfigureHudSurfaceTransparency(RectTransform root)
    {
        if (root == null) return;

        SetChildImageColor(root, "Background", new Color(0f, 0f, 0f, 0f), false);
        SetChildImageColor(root, "Header", new Color(0.13f, 0.15f, 0.18f, 0.94f), true);
        SetChildImageColor(root, "Battlefield/Friendly Party Area", new Color(0.12f, 0.18f, 0.22f, 0.78f), true);
        SetChildImageColor(root, "Battlefield/Enemy Party Area", new Color(0.22f, 0.13f, 0.14f, 0.78f), true);
        SetChildImageColor(root, "Battle Log Panel", new Color(0.12f, 0.12f, 0.14f, 0.94f), true);
    }

    private void ConfigureCombatScreenVisible(bool visible)
    {
        if (backgroundCanvas != null) backgroundCanvas.gameObject.SetActive(visible);
        if (canvas != null) canvas.gameObject.SetActive(visible);
    }

    private void ConfigureStartScreenVisible(bool visible)
    {
        if (startCanvas != null) startCanvas.gameObject.SetActive(visible);
        if (afkRewardCanvas != null) afkRewardCanvas.gameObject.SetActive(visible);
    }

    private void BindActorViews(RectTransform slotsRoot, bool friendlySide)
    {
        IdleAdventureActorView[] targetViews = friendlySide ? friendlyViews : enemyViews;
        for (int i = 0; i < MaxPartySize; i++)
        {
            targetViews[i] = EnsureActorView(slotsRoot, friendlySide, i);
        }
    }

    private IdleAdventureActorView EnsureActorView(RectTransform slotsRoot, bool friendlySide, int index)
    {
        if (slotsRoot == null) return null;

        string columnName = IsFrontColumnSlot(index) ? "Front Column" : "Back Column";
        string slotName = $"{(friendlySide ? "Friendly" : "Enemy")} {columnName} Slot {index + 1}";
        Transform slotTransform = slotsRoot.Find(slotName);

        IdleAdventureActorView view = slotTransform != null ? slotTransform.GetComponent<IdleAdventureActorView>() : null;
        if (view == null || !view.HasViewBindings)
        {
            if (slotTransform != null) DestroyRuntimeObject(slotTransform.gameObject);
            view = CreateActorSlot(slotsRoot, friendlySide, index);
        }

        bool frontColumn = IsFrontColumnSlot(index);
        int row = GetFormationRow(index);
        int column = frontColumn == friendlySide ? 1 : 0;
        float left = column == 0 ? 0f : 0.52f;
        float right = column == 0 ? 0.48f : 1f;
        float top = 1f - row / (float)FormationRows;
        float bottom = 1f - (row + 1f) / FormationRows;
        Anchor(view.Root, new Vector2(left, bottom), new Vector2(right, top), new Vector2(6f, 8f), new Vector2(-6f, -8f));
        return view;
    }

    private void EnsurePersistentPanels(RectTransform root)
    {
        if (root == null) return;

        Canvas startScreenCanvas = EnsureStartCanvas();
        RectTransform startRoot = startScreenCanvas != null ? startScreenCanvas.GetComponent<RectTransform>() : root;
        RemoveLegacyStartPanels(root, startRoot);

        startPanelView = ResolveExistingStartPanel(startRoot);
        formationPanelView = ResolveExistingFormationPanel(startRoot);
        if (startPanelView == null || !startPanelView.HasBindings || formationPanelView == null || !formationPanelView.HasBindings)
        {
            if (startPanelView != null) DestroyRuntimeObject(startPanelView.gameObject);
            startPanelView = null;
            formationPanelView = null;
            BuildStartPanel(startRoot);
        }
        else
        {
            startPanelView.Bind(StartAdventure);
            startPanelView.BindStageSelection(SelectPreviousStartStage, SelectNextStartStage);
            formationPanelView.Bind(OpenFormationPanel, CloseFormationPanel, AutoFillFormation, ClearFormation, AssignFormationSlot, ClearFormationSlot);
            formationPanelView.ApplySlotLayout(true);
            RefreshStartStageSelectionView();
            RefreshFormationView();
        }

        stageEndPanelView = ResolveExistingStageEndPanel(root);
        if (stageEndPanelView == null || !stageEndPanelView.HasBindings) BuildEndPanel(root);
        else
        {
            stageEndPanelView.Bind(ContinueNextStage, EndMode);
            stageEndPanelView.SetButtonLabels("Continue", "Exit Mode");
        }
        RemoveExtraStageEndPanels(root, stageEndPanelView);

        pausePanelView = ResolveExistingPausePanel(root);
        if (pausePanelView == null || !pausePanelView.HasBindings) BuildPausePanel(root);
        else
        {
            pausePanelView.Bind(ResumePausedBattle, ExitPausedBattle);
            pausePanelView.SetButtonLabels("Continue Battle", "Exit Mode");
        }
        RemoveExtraPausePanels(root, pausePanelView);

        HideEndPanel();
        HidePausePanel();
    }

    private IdleAdventureStartPanel ResolveExistingStartPanel(RectTransform root)
    {
        if (startPanelView != null && startPanelView.HasBindings) return startPanelView;
        return root.GetComponentInChildren<IdleAdventureStartPanel>(true);
    }

    private IdleAdventureStageEndPanel ResolveExistingStageEndPanel(RectTransform root)
    {
        if (stageEndPanelView != null && stageEndPanelView.HasBindings) return stageEndPanelView;
        return root.GetComponentInChildren<IdleAdventureStageEndPanel>(true);
    }

    private IdleAdventurePausePanel ResolveExistingPausePanel(RectTransform root)
    {
        if (pausePanelView != null && pausePanelView.HasBindings) return pausePanelView;
        return root.GetComponentInChildren<IdleAdventurePausePanel>(true);
    }

    private IdleAdventureFormationPanel ResolveExistingFormationPanel(RectTransform root)
    {
        if (formationPanelView != null && formationPanelView.HasBindings) return formationPanelView;
        return root.GetComponentInChildren<IdleAdventureFormationPanel>(true);
    }

    private void RemoveLegacyStartPanels(RectTransform hudRoot, RectTransform validStartRoot)
    {
        if (hudRoot == null) return;

        IdleAdventureStartPanel[] panels = hudRoot.GetComponentsInChildren<IdleAdventureStartPanel>(true);
        for (int i = panels.Length - 1; i >= 0; i--)
        {
            IdleAdventureStartPanel panel = panels[i];
            if (panel == null) continue;
            if (validStartRoot != null && panel.transform.IsChildOf(validStartRoot)) continue;

            DestroyRuntimeObject(panel.gameObject);
        }
    }

    private void RemoveExtraStageEndPanels(RectTransform root, IdleAdventureStageEndPanel panelToKeep)
    {
        if (root == null) return;

        IdleAdventureStageEndPanel[] panels = root.GetComponentsInChildren<IdleAdventureStageEndPanel>(true);
        for (int i = panels.Length - 1; i >= 0; i--)
        {
            IdleAdventureStageEndPanel panel = panels[i];
            if (panel == null || panel == panelToKeep) continue;

            DestroyRuntimeObject(panel.gameObject);
        }
    }

    private void RemoveExtraPausePanels(RectTransform root, IdleAdventurePausePanel panelToKeep)
    {
        if (root == null) return;

        IdleAdventurePausePanel[] panels = root.GetComponentsInChildren<IdleAdventurePausePanel>(true);
        for (int i = panels.Length - 1; i >= 0; i--)
        {
            IdleAdventurePausePanel panel = panels[i];
            if (panel == null || panel == panelToKeep) continue;

            DestroyRuntimeObject(panel.gameObject);
        }
    }

    private void ClearGeneratedInterface()
    {
        Transform existingBackgroundCanvas = transform.Find(BackgroundCanvasName);
        if (existingBackgroundCanvas != null) DestroyRuntimeObject(existingBackgroundCanvas.gameObject);

        Transform existingStartCanvas = transform.Find(StartCanvasName);
        if (existingStartCanvas != null) DestroyRuntimeObject(existingStartCanvas.gameObject);

        Transform existingCanvas = transform.Find(HudCanvasName);
        if (existingCanvas != null) DestroyRuntimeObject(existingCanvas.gameObject);

        Transform existingAfkRewardCanvas = transform.Find(AfkRewardCanvasName);
        if (existingAfkRewardCanvas == null && startCanvas != null) existingAfkRewardCanvas = startCanvas.transform.Find(AfkRewardCanvasName);
        if (existingAfkRewardCanvas != null) DestroyRuntimeObject(existingAfkRewardCanvas.gameObject);

        backgroundCanvas = null;
        startCanvas = null;
        afkRewardCanvas = null;
        canvas = null;
        afkRewardSource = null;
        battleHudView = null;
        startPanelView = null;
        stageEndPanelView = null;
        pausePanelView = null;
        formationPanelView = null;
        stageText = null;
        turnText = null;
        friendlyCountText = null;
        enemyCountText = null;
        logText = null;
        resultText = null;
        pauseButton = null;
        pauseButtonText = null;

        for (int i = 0; i < MaxPartySize; i++)
        {
            friendlyViews[i] = null;
            enemyViews[i] = null;
        }
    }

    private void BuildStartPanel(RectTransform root)
    {
        RectTransform startPanel = CreatePanel("Start Panel", root, new Color(0.045f, 0.052f, 0.065f, 1f));
        Stretch(startPanel);

        RectTransform skyBand = CreatePanel("Start Sky Band", startPanel, new Color(0.09f, 0.13f, 0.17f, 1f));
        Anchor(skyBand, new Vector2(0f, 0.48f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

        RectTransform groundBand = CreatePanel("Start Ground Band", startPanel, new Color(0.08f, 0.11f, 0.10f, 1f));
        Anchor(groundBand, new Vector2(0f, 0f), new Vector2(1f, 0.48f), Vector2.zero, Vector2.zero);

        RectTransform titleBlock = CreatePanel("Start Title Block", startPanel, new Color(0f, 0f, 0f, 0f));
        Anchor(titleBlock, new Vector2(0.08f, 0.54f), new Vector2(0.92f, 0.88f), Vector2.zero, Vector2.zero);

        TextMeshProUGUI startPanelTitleText = CreateText("Start Panel Title", titleBlock, "Idle Adventure mode", 54, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(startPanelTitleText.rectTransform, new Vector2(0f, 0.48f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

        TextMeshProUGUI startPanelBodyText = CreateText("Start Panel Body Text", titleBlock, "Expedition ready", 24, FontStyles.Normal, TextAlignmentOptions.Center);
        Anchor(startPanelBodyText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.42f), new Vector2(20f, 0f), new Vector2(-20f, 0f));

        RectTransform statusStrip = CreatePanel("Start Status Strip", startPanel, new Color(0.12f, 0.14f, 0.16f, 0.94f));
        Anchor(statusStrip, new Vector2(0.22f, 0.39f), new Vector2(0.78f, 0.48f), Vector2.zero, Vector2.zero);

        TextMeshProUGUI formationText = CreateText("Formation Status", statusStrip, "Camp prepared  |  Gate open  |  Team ready", 19, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(formationText.rectTransform);

        RectTransform stageSelector = CreatePanel("Stage Selector", startPanel, new Color(0.10f, 0.12f, 0.15f, 0.96f));
        Anchor(stageSelector, new Vector2(0.28f, 0.24f), new Vector2(0.72f, 0.35f), Vector2.zero, Vector2.zero);

        Button previousStageButton = CreateButton("Previous Stage Button", stageSelector, "Previous");
        Anchor(previousStageButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.22f, 1f), Vector2.zero, Vector2.zero);

        TextMeshProUGUI stageSelectionText = CreateText("Stage Selection Text", stageSelector, "Loading authoritative progress...", 18, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(stageSelectionText.rectTransform, new Vector2(0.23f, 0f), new Vector2(0.77f, 1f), new Vector2(8f, 4f), new Vector2(-8f, -4f));

        Button nextStageButton = CreateButton("Next Stage Button", stageSelector, "Next");
        Anchor(nextStageButton.GetComponent<RectTransform>(), new Vector2(0.78f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

        Button formationButton = CreateButton("Open Formation Button", startPanel, "Formation");
        Anchor(formationButton.GetComponent<RectTransform>(), new Vector2(0.28f, 0.13f), new Vector2(0.44f, 0.19f), Vector2.zero, Vector2.zero);

        Button startButton = CreateButton("Start Battle Button", startPanel, "Start Battle");
        Anchor(startButton.GetComponent<RectTransform>(), new Vector2(0.56f, 0.13f), new Vector2(0.72f, 0.19f), Vector2.zero, Vector2.zero);

        RectTransform formationWindow = CreatePanel("Formation Window", startPanel, new Color(0.035f, 0.04f, 0.05f, 0.98f));
        Stretch(formationWindow);
        formationWindow.gameObject.SetActive(false);

        TextMeshProUGUI formationTitle = CreateText("Formation Title", formationWindow, "Formation", 42, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(formationTitle.rectTransform, new Vector2(0f, 0.88f), new Vector2(1f, 0.98f), new Vector2(28f, 0f), new Vector2(-28f, 0f));

        RectTransform slotArea = CreatePanel("Formation Slot Area", formationWindow, new Color(0.08f, 0.09f, 0.11f, 0.92f));
        Anchor(slotArea, new Vector2(0.24f, 0.32f), new Vector2(0.76f, 0.82f), Vector2.zero, Vector2.zero);

        Image[] formationSlots = new Image[MaxPartySize];
        TextMeshProUGUI[] formationLabels = new TextMeshProUGUI[MaxPartySize];
        IdleAdventureFormationSlotDropTarget[] formationDropTargets = new IdleAdventureFormationSlotDropTarget[MaxPartySize];
        for (int i = 0; i < MaxPartySize; i++)
        {
            bool frontColumn = IsFrontColumnSlot(i);
            int row = GetFormationRow(i);
            int column = frontColumn ? 1 : 0;
            float left = column == 0 ? 0.05f : 0.53f;
            float right = column == 0 ? 0.47f : 0.95f;
            float top = 0.92f - row * 0.30f;
            float bottom = top - 0.22f;

            Image slotImage = CreateImage($"Formation Slot {i + 1}", slotArea, new Color(0.08f, 0.09f, 0.10f, 0.82f));
            Anchor(slotImage.rectTransform, new Vector2(left, bottom), new Vector2(right, top), Vector2.zero, Vector2.zero);
            IdleAdventureFormationSlotDropTarget dropTarget = slotImage.gameObject.AddComponent<IdleAdventureFormationSlotDropTarget>();
            dropTarget.Initialize(i, AssignFormationSlot, ClearFormationSlot);

            TextMeshProUGUI label = CreateText("Label", slotImage.transform, string.Empty, 15, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            if (label != null) label.fontSize = 15f;
            formationSlots[i] = slotImage;
            formationLabels[i] = label;
            formationDropTargets[i] = dropTarget;
        }

        RectTransform npcListArea = CreatePanel("Available NPC List", formationWindow, new Color(0.07f, 0.075f, 0.085f, 0.94f));
        Anchor(npcListArea, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.26f), Vector2.zero, Vector2.zero);

        Image[] npcItems = new Image[MaxPartySize];
        TextMeshProUGUI[] npcLabels = new TextMeshProUGUI[MaxPartySize];
        IdleAdventureFormationDragItem[] npcDragItems = new IdleAdventureFormationDragItem[MaxPartySize];
        Canvas ownerCanvas = startCanvas != null ? startCanvas : GetComponentInParent<Canvas>();
        for (int i = 0; i < MaxPartySize; i++)
        {
            Image npcItem = CreateImage($"Available NPC {i + 1}", npcListArea, new Color(0.18f, 0.28f, 0.34f, 0.94f));
            float left = 0.02f + i * 0.16f;
            float right = left + 0.14f;
            Anchor(npcItem.rectTransform, new Vector2(left, 0.16f), new Vector2(right, 0.84f), Vector2.zero, Vector2.zero);
            npcItem.gameObject.AddComponent<CanvasGroup>();
            IdleAdventureFormationDragItem dragItem = npcItem.gameObject.AddComponent<IdleAdventureFormationDragItem>();
            dragItem.Initialize(i, ownerCanvas);

            TextMeshProUGUI npcLabel = CreateText("Label", npcItem.transform, string.Empty, 15, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(npcLabel.rectTransform);
            npcItems[i] = npcItem;
            npcLabels[i] = npcLabel;
            npcDragItems[i] = dragItem;
        }

        Button autoFillButton = CreateButton("Formation Auto Fill Button", formationWindow, "Auto Fill");
        Anchor(autoFillButton.GetComponent<RectTransform>(), new Vector2(0.08f, 0.86f), new Vector2(0.20f, 0.93f), Vector2.zero, Vector2.zero);

        Button clearButton = CreateButton("Formation Clear Button", formationWindow, "Clear");
        Anchor(clearButton.GetComponent<RectTransform>(), new Vector2(0.80f, 0.86f), new Vector2(0.92f, 0.93f), Vector2.zero, Vector2.zero);

        Button closeButton = CreateButton("Formation Close Button", formationWindow, "Confirm");
        Anchor(closeButton.GetComponent<RectTransform>(), new Vector2(0.42f, 0.86f), new Vector2(0.58f, 0.93f), Vector2.zero, Vector2.zero);

        startPanelView = startPanel.gameObject.AddComponent<IdleAdventureStartPanel>();
        startPanelView.Initialize(
            startPanelTitleText,
            startPanelBodyText,
            stageSelectionText,
            previousStageButton,
            nextStageButton,
            startButton);
        startPanelView.Bind(StartAdventure);
        startPanelView.BindStageSelection(SelectPreviousStartStage, SelectNextStartStage);
        RefreshStartStageSelectionView();

        formationPanelView = startPanel.gameObject.AddComponent<IdleAdventureFormationPanel>();
        formationPanelView.Initialize(formationWindow, formationText, formationButton, closeButton, autoFillButton, clearButton, formationSlots, formationLabels, formationDropTargets, npcItems, npcLabels, npcDragItems, ownerCanvas);
        formationPanelView.Bind(OpenFormationPanel, CloseFormationPanel, AutoFillFormation, ClearFormation, AssignFormationSlot, ClearFormationSlot);
        formationPanelView.ApplySlotLayout(true);
        RefreshFormationView();
    }

    private void BuildEndPanel(RectTransform root)
    {
        RectTransform endPanel = CreatePanel("End Panel", root, new Color(0f, 0f, 0f, 0.74f));
        Stretch(endPanel);

        RectTransform panelBody = CreatePanel("End Panel Body", endPanel, new Color(0.12f, 0.14f, 0.17f, 1f));
        Anchor(panelBody, new Vector2(0.30f, 0.32f), new Vector2(0.70f, 0.68f), Vector2.zero, Vector2.zero);

        TextMeshProUGUI endPanelTitleText = CreateText("End Panel Title", panelBody, "Stage Cleared", 30, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(endPanelTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -82f), new Vector2(-30f, -26f));

        TextMeshProUGUI endPanelBodyText = CreateText("End Panel Body Text", panelBody, string.Empty, 18, FontStyles.Normal, TextAlignmentOptions.Center);
        Anchor(endPanelBodyText.rectTransform, new Vector2(0f, 0.42f), new Vector2(1f, 0.74f), new Vector2(40f, 0f), new Vector2(-40f, 0f));

        Button continueButton = CreateButton("Continue Stage Button", panelBody, "Continue");
        Anchor(continueButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-256f, 44f), new Vector2(-24f, 98f));

        Button endModeButton = CreateButton("End Mode Button", panelBody, "Exit Mode");
        Anchor(endModeButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(24f, 44f), new Vector2(256f, 98f));

        stageEndPanelView = endPanel.gameObject.AddComponent<IdleAdventureStageEndPanel>();
        stageEndPanelView.Initialize(endPanelTitleText, endPanelBodyText, continueButton, endModeButton);
        stageEndPanelView.Bind(ContinueNextStage, EndMode);
    }

    private void BuildPausePanel(RectTransform root)
    {
        RectTransform pausePanel = CreatePanel("Pause Panel", root, new Color(0f, 0f, 0f, 0.62f));
        Stretch(pausePanel);

        RectTransform panelBody = CreatePanel("Pause Panel Body", pausePanel, new Color(0.10f, 0.12f, 0.15f, 0.98f));
        Anchor(panelBody, new Vector2(0.32f, 0.34f), new Vector2(0.68f, 0.66f), Vector2.zero, Vector2.zero);

        TextMeshProUGUI pausePanelTitleText = CreateText("Pause Panel Title", panelBody, "Battle Paused", 30, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(pausePanelTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -78f), new Vector2(-30f, -24f));

        TextMeshProUGUI pausePanelBodyText = CreateText("Pause Panel Body Text", panelBody, "Continue the current battle or exit back to the mode page.", 18, FontStyles.Normal, TextAlignmentOptions.Center);
        Anchor(pausePanelBodyText.rectTransform, new Vector2(0f, 0.42f), new Vector2(1f, 0.74f), new Vector2(40f, 0f), new Vector2(-40f, 0f));

        Button resumeButton = CreateButton("Continue Battle Button", panelBody, "Continue Battle");
        Anchor(resumeButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-260f, 42f), new Vector2(-24f, 96f));

        Button exitButton = CreateButton("Exit Battle Button", panelBody, "Exit Mode");
        Anchor(exitButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(24f, 42f), new Vector2(260f, 96f));

        pausePanelView = pausePanel.gameObject.AddComponent<IdleAdventurePausePanel>();
        pausePanelView.Initialize(pausePanelTitleText, pausePanelBodyText, resumeButton, exitButton);
        pausePanelView.Bind(ResumePausedBattle, ExitPausedBattle);
        pausePanelView.SetButtonLabels("Continue Battle", "Exit Mode");
    }

    private void ApplyPendingRoleAcquisitionRoute()
    {
        acquisitionRouteApplied = false;
        acquisitionRouteModeCode = string.Empty;
        acquisitionRouteStage = 0;
        acquisitionRoutePoolKey = string.Empty;

        string routeKey = PlayerPrefs.GetString(PendingRoleAcquisitionRouteKey, string.Empty).Trim().ToUpperInvariant();
        if (routeKey.Length == 0)
        {
            return;
        }

        string expiresAt = PlayerPrefs.GetString(PendingRoleAcquisitionRouteExpiresAtKey, string.Empty);
        if (!string.IsNullOrEmpty(expiresAt)
            && (!DateTimeOffset.TryParse(
                    expiresAt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out DateTimeOffset parsedExpiry)
                || DateTimeOffset.UtcNow > parsedExpiry))
        {
            ClearPendingRoleAcquisitionRouteStorage();
            return;
        }

        if (routeKey != "IDLE_ADVENTURE")
        {
            ClearPendingRoleAcquisitionRouteStorage();
            return;
        }

        string modeCode = PlayerPrefs.GetString(PendingRoleAcquisitionRouteModeCodeKey, string.Empty).Trim().ToUpperInvariant();
        string poolKey = PlayerPrefs.GetString(PendingRoleAcquisitionRoutePoolKeyKey, string.Empty).Trim();
        int requestedStage = PlayerPrefs.GetInt(PendingRoleAcquisitionRouteStageKey, 0);
        if ((modeCode != "IDLE_ADVENTURE" && modeCode != "IDLE_ADVENTURE_MODE" && modeCode != "BATTLE")
            || !IsSafeRoleAcquisitionRouteToken(poolKey)
            || requestedStage < 1
            || requestedStage > 10000)
        {
            ClearPendingRoleAcquisitionRouteStorage();
            return;
        }

        int routePlayerId = PlayerPrefs.GetInt(PendingRoleAcquisitionRoutePlayerIdKey, 0);
        if (routePlayerId <= 0)
        {
            ClearPendingRoleAcquisitionRouteStorage();
            return;
        }

        BackendGameService service = BackendGameService.Instance;
        if (service == null || !service.IsAuthenticated)
        {
            return;
        }

        if (service.PlayerId != routePlayerId)
        {
            ClearPendingRoleAcquisitionRouteStorage();
            return;
        }

        acquisitionRouteApplied = true;
        acquisitionRouteModeCode = modeCode;
        acquisitionRouteStage = requestedStage;
        acquisitionRoutePoolKey = poolKey;
        if (startPanelView != null)
        {
            startPanelView.SetContent(
                "Idle Adventure mode",
                $"Guide target: Stage {requestedStage}  Drop pool: {poolKey}. Use the stage selector to replay it.");
        }
        RefreshStartStageSelectionView();
        AddLog($"Acquisition guide target: mode={modeCode}, stage={requestedStage}, pool={poolKey}.");
        ClearPendingRoleAcquisitionRouteStorage();
    }

    private static bool IsSafeRoleAcquisitionRouteToken(string value)
    {
        string token = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(token) || token.Length > MaxRoleAcquisitionRouteTokenLength)
        {
            return false;
        }

        for (int i = 0; i < token.Length; i++)
        {
            if (char.IsControl(token[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static void ClearPendingRoleAcquisitionRouteStorage()
    {
        PlayerPrefs.DeleteKey(PendingRoleAcquisitionRouteKey);
        PlayerPrefs.DeleteKey(PendingRoleAcquisitionRoutePlayerIdKey);
        PlayerPrefs.DeleteKey(PendingRoleAcquisitionRouteExpiresAtKey);
        PlayerPrefs.DeleteKey("weijun.npcAcquisition.poolCode");
        PlayerPrefs.DeleteKey("weijun.npcAcquisition.highlightNpcId");
        PlayerPrefs.DeleteKey("weijun.npcAcquisition.taskCode");
        PlayerPrefs.DeleteKey("weijun.npcAcquisition.planCode");
        PlayerPrefs.DeleteKey(PendingRoleAcquisitionRouteModeCodeKey);
        PlayerPrefs.DeleteKey(PendingRoleAcquisitionRouteStageKey);
        PlayerPrefs.DeleteKey(PendingRoleAcquisitionRoutePoolKeyKey);
        PlayerPrefs.DeleteKey("weijun.npcAcquisition.mailId");
        PlayerPrefs.Save();
    }

    private void ResetToStartScreen()
    {
        battleStarted = false;
        stageChoicePending = false;
        stageFailedPending = false;
        modePausedByChoice = false;
        paused = false;
        backendModeActive = false;
        backendRequestPending = false;
        backendFormationDirty = false;
        backendFormationRevision++;
        backendSessionKey = string.Empty;
        backendRunningSessionKey = string.Empty;
        backendRuntimeCombatants.Clear();
        localSimulationBlockedLogged = false;
        pendingNextStage = -1;
        selectedReplayStage = 0;
        stage = 1;
        round = 1;
        battleLog.Clear();
        LoadStageProgress();
        ClearRunRewards();
        grantedRewards.Clear();
        rewardDisplayNames.Clear();
        if (rewardLedger != null) rewardLedger.ClearGrantedRewards();
        InitializeDefaultFormation();

        ClearParty(friendlies);
        ClearParty(enemies);
        ClearCombatantRootChildren();

        if (pauseButtonText != null) pauseButtonText.text = "Pause";
        if (resultText != null) resultText.text = string.Empty;
        HidePausePanel();
        HideEndPanel();
        ShowStartPanel();
        SetWaitingText();
        if (startPanelView != null) startPanelView.SetContent("Idle Adventure mode", "Loading authoritative progress...");
        RefreshStartStageSelectionView();
        RefreshFormationView();
    }

    private void InitializeDefaultFormation()
    {
        for (int i = 0; i < MaxPartySize; i++)
        {
            friendlyFormation[i] = -1;
        }

        int availableCount = AvailableFriendlyDefinitionCount();
        int deployCount = Mathf.Clamp(startingFriendlyCount, 1, Mathf.Max(1, availableCount));
        deployCount = Mathf.Min(deployCount, MaxPartySize);
        for (int i = 0; i < deployCount; i++)
        {
            friendlyFormation[i] = i;
        }
    }

    private int AvailableFriendlyDefinitionCount()
    {
        if (backendAvailableNpcs != null && backendAvailableNpcs.Length > 0)
        {
            return Mathf.Clamp(backendAvailableNpcs.Length, 1, MaxPartySize);
        }

        int configuredCount = modeConfig != null && modeConfig.FriendlyLineup != null ? modeConfig.FriendlyLineup.Length : 0;
        return Mathf.Clamp(Mathf.Max(configuredCount, startingFriendlyCount), 1, MaxPartySize);
    }

    private int FormationDeployedCount()
    {
        int count = 0;
        for (int i = 0; i < MaxPartySize; i++)
        {
            if (friendlyFormation[i] >= 0) count++;
        }

        return count;
    }

    private bool IsFriendlyDefinitionAssigned(int definitionIndex, int ignoredSlot)
    {
        for (int i = 0; i < MaxPartySize; i++)
        {
            if (i == ignoredSlot) continue;
            if (friendlyFormation[i] == definitionIndex) return true;
        }

        return false;
    }

    private void RefreshFormationView()
    {
        if (formationPanelView == null || !formationPanelView.HasBindings) return;

        int deployedCount = FormationDeployedCount();
        formationPanelView.SetStatus($"{deployedCount}/{MaxPartySize} deployed");

        for (int i = 0; i < MaxPartySize; i++)
        {
            int definitionIndex = friendlyFormation[i];
            bool occupied = definitionIndex >= 0;
            string rowName = $"Row {GetFormationRow(i) + 1}";
            string columnName = IsFrontColumnSlot(i) ? "Front" : "Back";
            string unitName = occupied ? ResolveFriendlyFormationName(definitionIndex) : "Empty";
            formationPanelView.SetSlot(i, $"{columnName} {rowName}\n{unitName}", occupied, IsFrontColumnSlot(i));
        }

        int availableCount = AvailableFriendlyDefinitionCount();
        for (int i = 0; i < MaxPartySize; i++)
        {
            bool available = i < availableCount;
            bool assigned = available && IsFriendlyDefinitionAssigned(i, -1);
            string unitName = available ? ResolveFriendlyFormationName(i) : string.Empty;
            string label = assigned ? $"{unitName}\nAssigned" : unitName;
            formationPanelView.SetNpc(i, label, available, assigned);
        }
    }

    private string ResolveFriendlyFormationName(int definitionIndex)
    {
        string fallbackName = $"Hero {definitionIndex + 1}";
        if (backendAvailableNpcs != null && definitionIndex >= 0 && definitionIndex < backendAvailableNpcs.Length && backendAvailableNpcs[definitionIndex] != null)
        {
            string backendName = backendAvailableNpcs[definitionIndex].ResolvedCombatantName;
            return string.IsNullOrEmpty(backendName) ? fallbackName : backendName;
        }

        IdleAdventureCombatantDefinition[] lineup = modeConfig != null ? modeConfig.FriendlyLineup : null;
        if (lineup == null || definitionIndex < 0 || definitionIndex >= lineup.Length || lineup[definitionIndex] == null) return fallbackName;
        return lineup[definitionIndex].ResolveName(fallbackName);
    }

    private void ShowStartPanel()
    {
        battleStarted = false;
        stageChoicePending = false;
        stageFailedPending = false;
        modePausedByChoice = false;
        paused = false;
        pendingNextStage = -1;
        ConfigureCombatScreenVisible(false);
        ConfigureStartScreenVisible(true);
        if (startPanelView != null) startPanelView.Show();
        if (formationPanelView != null) formationPanelView.HideFormationWindow();
        RefreshFormationView();
        HidePausePanel();
        HideEndPanel();
        UpdateButtonStates();
    }

    private void HideStartPanel()
    {
        if (startPanelView != null) startPanelView.Hide();
        ConfigureStartScreenVisible(false);
    }

    private void ShowEndPanel()
    {
        HidePausePanel();
        if (stageEndPanelView != null) stageEndPanelView.Show();
        UpdateButtonStates();
    }

    private void HideEndPanel()
    {
        if (stageEndPanelView != null) stageEndPanelView.Hide();
        UpdateButtonStates();
    }

    private void ShowPausePanel()
    {
        if (pausePanelView == null) return;

        pausePanelView.SetContent("Battle Paused", "Continue the current battle or exit back to the mode page.");
        pausePanelView.SetButtonLabels("Continue Battle", "Exit Battle");
        pausePanelView.Show();
        UpdateButtonStates();
    }

    private void HidePausePanel()
    {
        if (pausePanelView != null) pausePanelView.Hide();
        UpdateButtonStates();
    }

    private void SetBattleRunningLocally()
    {
        paused = false;
        modePausedByChoice = false;
        if (pauseButtonText != null) pauseButtonText.text = "Pause";
        HidePausePanel();
    }

    private void SelectPreviousStartStage()
    {
        ChangeSelectedStartStage(-1);
    }

    private void SelectNextStartStage()
    {
        ChangeSelectedStartStage(1);
    }

    private void ChangeSelectedStartStage(int direction)
    {
        if (backendRequestPending || battleStarted || backendHighestStage <= 0 || direction == 0) return;

        SanitizeSelectedReplayStage();
        int optionCount = backendHighestStage + 1;
        int optionIndex = selectedReplayStage;
        optionIndex = (optionIndex + direction) % optionCount;
        if (optionIndex < 0) optionIndex += optionCount;
        selectedReplayStage = optionIndex;

        int requestedStage = ResolveSelectedStartStage();
        RefreshStartStageSelectionView();
        if (startPanelView != null)
        {
            string selectionText = selectedReplayStage > 0
                ? $"Loading Stage {selectedReplayStage} replay preview..."
                : $"Loading authoritative Stage {backendNextStage} preview...";
            startPanelView.SetContent("Idle Adventure mode", selectionText);
        }
        QueueBackendReferenceDataLoad(requestedStage);
    }

    private int ResolveSelectedStartStage()
    {
        SanitizeSelectedReplayStage();
        return selectedReplayStage > 0
            ? selectedReplayStage
            : Mathf.Max(1, backendNextStage);
    }

    private void SanitizeSelectedReplayStage()
    {
        if (selectedReplayStage < 1 || selectedReplayStage > backendHighestStage)
        {
            selectedReplayStage = 0;
        }
    }

    private void RefreshStartStageSelectionView()
    {
        if (startPanelView == null) return;

        SanitizeSelectedReplayStage();
        string selection = selectedReplayStage > 0
            ? $"Replay Stage {selectedReplayStage}"
            : $"Continue Stage {Mathf.Max(1, backendNextStage)}";
        string guide = acquisitionRouteApplied && acquisitionRouteStage > 0
            ? $"  |  Guide target: Stage {acquisitionRouteStage}"
            : string.Empty;
        startPanelView.SetStageSelection(
            $"{selection}\nHighest cleared: {Mathf.Max(0, backendHighestStage)}{guide}");
    }

    private void StartAdventure()
    {
        if (battleStarted) return;
        if (FormationDeployedCount() <= 0)
        {
            RefreshFormationView();
            return;
        }

        BackendGameService service = ResolveBackendService();
        if (service == null || !service.IsAuthenticated)
        {
            string message = "Backend session is not authenticated. Login is required before starting Idle Adventure.";
            AddLog(message);
            if (startPanelView != null) startPanelView.SetContent("Idle Adventure mode", message);
            UpdateButtonStates();
            return;
        }

        if (!backendRequestPending) StartCoroutine(StartBackendAdventureRoutine());
    }

    private IEnumerator StartBackendAdventureRoutine()
    {
        BackendGameService service = ResolveBackendService();
        if (service == null || !service.IsAuthenticated) yield break;

        backendRequestPending = true;
        if (startPanelView != null) startPanelView.SetContent("Idle Adventure mode", "Starting from backend...");

        yield return RefreshBackendStamina(service);
        bool homeRefreshSucceeded = false;
        yield return RefreshBackendHomeBeforeSessionStart(service, succeeded => homeRefreshSucceeded = succeeded);
        if (!homeRefreshSucceeded)
        {
            backendRequestPending = false;
            if (startPanelView != null)
            {
                startPanelView.SetContent("Idle Adventure mode", "Unable to refresh authoritative stage progress. Please retry.");
            }
            UpdateButtonStates();
            yield break;
        }
        if (!string.IsNullOrEmpty(backendRunningSessionKey))
        {
            BackendResult<IdleAdventureSessionResponse> endResult = null;
            yield return service.EndIdleAdventureSession(backendRunningSessionKey, value => endResult = value);
            if (endResult == null || !endResult.Success || endResult.Data == null)
            {
                backendRequestPending = false;
                AddLog($"Backend active session cleanup failed: {BackendResultMessage(endResult)}");
                if (startPanelView != null) startPanelView.SetContent("Idle Adventure mode", $"Backend cleanup failed: {BackendResultMessage(endResult)}");
                UpdateButtonStates();
                yield break;
            }

            AddLog("Previous backend battle session ended before starting a new formation.");
            backendSessionKey = string.Empty;
            backendRunningSessionKey = string.Empty;
        }

        int requestedStage = ResolveSelectedStartStage();
        yield return StartBackendSessionFromSavedFormation(service, requestedStage, "Backend battle start failed");
    }

    private IEnumerator RefreshBackendHomeBeforeSessionStart(BackendGameService service, Action<bool> onComplete)
    {
        if (service == null || !service.IsAuthenticated)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        BackendResult<IdleAdventureHomeResponse> homeResult = null;
        yield return service.LoadIdleAdventureHome(value => homeResult = value);
        if (homeResult != null && homeResult.Success && homeResult.Data != null)
        {
            ApplyBackendHome(homeResult.Data);
            onComplete?.Invoke(true);
        }
        else
        {
            AddLog($"Backend home refresh before start failed: {BackendResultMessage(homeResult)}");
            onComplete?.Invoke(false);
        }
    }

    private IEnumerator StartBackendSessionFromSavedFormation(BackendGameService service, int requestedStage, string failurePrefix)
    {
        IdleAdventureStartSessionRequest request = new IdleAdventureStartSessionRequest
        {
            stage = Mathf.Max(1, requestedStage),
            use_saved_formation = true,
            formation_slots = null
        };

        BackendResult<IdleAdventureSessionResponse> result = null;
        yield return service.StartIdleAdventureSession(request, value => result = value);
        backendRequestPending = false;

        if (result == null || !result.Success || result.Data == null)
        {
            AddLog($"{failurePrefix}: {BackendResultMessage(result)}");
            if (startPanelView != null) startPanelView.SetContent("Idle Adventure mode", $"{failurePrefix}: {BackendResultMessage(result)}");
            UpdateButtonStates();
            yield break;
        }

        backendModeActive = true;
        backendSessionKey = result.Data.session_key;
        backendRunningSessionKey = backendSessionKey;
        localSimulationBlockedLogged = false;
        battleStarted = true;
        stageChoicePending = false;
        stageFailedPending = false;
        modePausedByChoice = false;
        pendingNextStage = -1;
        SetBattleRunningLocally();
        ClearRunRewards();
        grantedRewards.Clear();
        rewardDisplayNames.Clear();
        if (rewardLedger != null) rewardLedger.ClearGrantedRewards();
        ConfigureCombatScreenVisible(true);
        HideStartPanel();
        HideEndPanel();
        HidePausePanel();
        ApplyBackendSessionSnapshot(result.Data, true);
        SetBattleRunningLocally();
        selectedReplayStage = 0;
        acquisitionRouteApplied = false;
        acquisitionRouteModeCode = string.Empty;
        acquisitionRouteStage = 0;
        acquisitionRoutePoolKey = string.Empty;
        AddLog(string.IsNullOrEmpty(result.Data.message) ? $"Backend battle started from Stage {stage}." : result.Data.message);
        UpdateButtonStates();
    }

    private void ApplyBackendSessionSnapshot(IdleAdventureSessionResponse session, bool rebuildParties)
    {
        if (session == null) return;

        backendSessionKey = string.IsNullOrEmpty(session.session_key) ? backendSessionKey : session.session_key;
        stage = Mathf.Max(1, session.stage);
        round = Mathf.Max(1, session.round);
        string status = NormalizeBackendStatus(session.status);
        backendRunningSessionKey = status == "ENDED" ? string.Empty : backendSessionKey;
        paused = status == "RUNNING" ? false : status == "PAUSED" || session.paused;
        pendingNextStage = session.pending_next_stage > 0 ? session.pending_next_stage : pendingNextStage;
        if (pauseButtonText != null) pauseButtonText.text = paused ? "Resume" : "Pause";

        SyncRewardDictionary(pendingRunRewards, session.pending_rewards);
        SyncRewardDictionary(grantedRewards, session.granted_rewards);
        SyncBackendProgressFromSession(session);

        if (rebuildParties)
        {
            BuildBackendParty(friendlies, session.friendlies, true);
            BuildBackendParty(enemies, session.enemies, false);
        }
        else
        {
            UpdateBackendParty(friendlies, session.friendlies, true);
            UpdateBackendParty(enemies, session.enemies, false);
        }

        backendRuntimeCombatants.Clear();
        RegisterBackendRuntimeCombatants(friendlies);
        RegisterBackendRuntimeCombatants(enemies);

        if (!string.IsNullOrEmpty(session.message)) AddLog(session.message);
        RefreshBattleView();
    }

    private void BuildBackendParty(List<IdleAdventureCombatant> party, IdleAdventureCombatantStateResponse[] states, bool isFriendly)
    {
        ClearParty(party);
        for (int i = 0; i < MaxPartySize; i++)
        {
            party.Add(null);
        }

        if (states == null) return;

        for (int i = 0; i < states.Length; i++)
        {
            IdleAdventureCombatantStateResponse state = states[i];
            if (IsEmptyBackendCombatantPlaceholder(state)) continue;
            if (state == null || state.slot_index < 0 || state.slot_index >= MaxPartySize) continue;
            if (ShouldHideBackendCombatantState(state)) continue;

            IdleAdventureCombatant combatant = CreateCombatant(
                string.IsNullOrEmpty(state.combatant_name) ? $"{(isFriendly ? "Hero" : "Enemy")} {state.slot_index + 1}" : state.combatant_name,
                ToInt(state.max_health),
                ToInt(state.attack),
                ToInt(state.defense),
                ToInt(state.speed),
                isFriendly,
                state.slot_index,
                null,
                ParseAttribute(state.attribute_type));
            ApplyBackendCombatantState(combatant, state, isFriendly);
            party[state.slot_index] = combatant;
        }
    }

    private void UpdateBackendParty(List<IdleAdventureCombatant> party, IdleAdventureCombatantStateResponse[] states, bool isFriendly)
    {
        if (party.Count != MaxPartySize)
        {
            BuildBackendParty(party, states, isFriendly);
            return;
        }

        bool[] seenSlots = new bool[MaxPartySize];
        if (states != null)
        {
            for (int i = 0; i < states.Length; i++)
            {
                IdleAdventureCombatantStateResponse state = states[i];
                if (IsEmptyBackendCombatantPlaceholder(state)) continue;
                if (state == null || state.slot_index < 0 || state.slot_index >= MaxPartySize) continue;

                seenSlots[state.slot_index] = true;
                if (ShouldHideBackendCombatantState(state))
                {
                    if (party[state.slot_index] != null)
                    {
                        DestroyRuntimeObject(party[state.slot_index].gameObject);
                        party[state.slot_index] = null;
                    }

                    continue;
                }

                IdleAdventureCombatant combatant = party[state.slot_index];
                if (combatant == null)
                {
                    combatant = CreateCombatant(
                        string.IsNullOrEmpty(state.combatant_name) ? $"{(isFriendly ? "Hero" : "Enemy")} {state.slot_index + 1}" : state.combatant_name,
                        ToInt(state.max_health),
                        ToInt(state.attack),
                        ToInt(state.defense),
                        ToInt(state.speed),
                        isFriendly,
                        state.slot_index,
                        null,
                        ParseAttribute(state.attribute_type));
                    party[state.slot_index] = combatant;
                }

                ApplyBackendCombatantState(combatant, state, isFriendly);
            }
        }

        for (int i = 0; i < MaxPartySize; i++)
        {
            if (seenSlots[i]) continue;
            if (party[i] == null) continue;

            DestroyRuntimeObject(party[i].gameObject);
            party[i] = null;
        }
    }

    private void ApplyBackendCombatantState(IdleAdventureCombatant combatant, IdleAdventureCombatantStateResponse state, bool isFriendly)
    {
        if (combatant == null || state == null) return;

        List<IdleAdventureBattleBuff> buffs = ConvertBackendBuffs(state.active_buffs);
        int currentHealth = state.defeated ? 0 : ToInt(state.current_health);
        combatant.ApplyBackendState(
            state.runtime_id,
            state.combatant_name,
            ToInt(state.max_health),
            currentHealth,
            ToInt(state.attack),
            ToInt(state.defense),
            ToInt(state.speed),
            isFriendly,
            state.slot_index,
            ParseAttribute(state.attribute_type),
            buffs);
    }

    private void ApplyBackendBuffTargetState(IdleAdventureCombatantStateResponse state, string requestedSide)
    {
        if (IsEmptyBackendCombatantPlaceholder(state)) return;

        string resolvedSide = state != null && !string.IsNullOrEmpty(state.side) ? state.side : requestedSide;
        bool isFriendly = string.Equals(resolvedSide, "FRIENDLY", StringComparison.OrdinalIgnoreCase);
        List<IdleAdventureCombatant> party = isFriendly ? friendlies : enemies;
        if (state == null || state.slot_index < 0 || state.slot_index >= MaxPartySize) return;

        while (party.Count < MaxPartySize) party.Add(null);

        if (ShouldHideBackendCombatantState(state))
        {
            if (party[state.slot_index] != null)
            {
                if (!string.IsNullOrEmpty(party[state.slot_index].RuntimeId)) backendRuntimeCombatants.Remove(party[state.slot_index].RuntimeId);
                DestroyRuntimeObject(party[state.slot_index].gameObject);
                party[state.slot_index] = null;
            }

            return;
        }

        IdleAdventureCombatant combatant = party[state.slot_index];
        if (combatant == null)
        {
            combatant = CreateCombatant(
                string.IsNullOrEmpty(state.combatant_name) ? $"{(isFriendly ? "Hero" : "Enemy")} {state.slot_index + 1}" : state.combatant_name,
                ToInt(state.max_health),
                ToInt(state.attack),
                ToInt(state.defense),
                ToInt(state.speed),
                isFriendly,
                state.slot_index,
                null,
                ParseAttribute(state.attribute_type));
            party[state.slot_index] = combatant;
        }

        ApplyBackendCombatantState(combatant, state, isFriendly);
        if (!string.IsNullOrEmpty(combatant.RuntimeId)) backendRuntimeCombatants[combatant.RuntimeId] = combatant;
    }

    private static bool ShouldHideBackendCombatantState(IdleAdventureCombatantStateResponse state)
    {
        if (state == null) return true;
        return state.defeated || state.current_health <= 0 || state.max_health <= 0;
    }

    private static bool IsEmptyBackendCombatantPlaceholder(IdleAdventureCombatantStateResponse state)
    {
        if (state == null) return true;

        return string.IsNullOrEmpty(state.runtime_id) &&
               state.definition_id <= 0 &&
               string.IsNullOrEmpty(state.combatant_name) &&
               string.IsNullOrEmpty(state.side) &&
               state.max_health <= 0 &&
               state.current_health <= 0 &&
               state.attack <= 0 &&
               state.defense <= 0 &&
               state.speed <= 0 &&
               state.effective_attack <= 0 &&
               state.effective_defense <= 0 &&
               state.effective_speed <= 0 &&
               !state.defeated;
    }

    private void RegisterBackendRuntimeCombatants(List<IdleAdventureCombatant> party)
    {
        for (int i = 0; i < party.Count; i++)
        {
            IdleAdventureCombatant combatant = party[i];
            if (combatant == null || string.IsNullOrEmpty(combatant.RuntimeId)) continue;
            backendRuntimeCombatants[combatant.RuntimeId] = combatant;
        }
    }

    private IdleAdventureCombatant FindBackendCombatant(string runtimeId)
    {
        if (string.IsNullOrEmpty(runtimeId)) return null;
        if (backendRuntimeCombatants.TryGetValue(runtimeId, out IdleAdventureCombatant combatant)) return combatant;
        return FindBackendCombatantByRuntimeSlot(runtimeId);
    }

    private IdleAdventureCombatant FindBackendCombatantByRuntimeSlot(string runtimeId)
    {
        if (!TryResolveBackendRuntimeSlot(runtimeId, out List<IdleAdventureCombatant> party, out int slotIndex, out bool _)) return null;

        if (slotIndex >= party.Count) return null;
        IdleAdventureCombatant combatant = party[slotIndex];
        if (combatant == null) return null;
        return string.IsNullOrEmpty(combatant.RuntimeId) || combatant.RuntimeId == runtimeId ? combatant : null;
    }

    private bool TryResolveBackendRuntimeSlot(string runtimeId, out List<IdleAdventureCombatant> party, out int slotIndex, out bool isFriendly)
    {
        party = null;
        slotIndex = -1;
        isFriendly = false;
        if (string.IsNullOrEmpty(runtimeId) || runtimeId.Length < 3 || runtimeId[1] != '-') return false;
        if (!int.TryParse(runtimeId.Substring(2), out slotIndex)) return false;
        if (slotIndex < 0 || slotIndex >= MaxPartySize) return false;

        if (runtimeId[0] == 'F')
        {
            party = friendlies;
            isFriendly = true;
            return true;
        }

        if (runtimeId[0] == 'E')
        {
            party = enemies;
            isFriendly = false;
            return true;
        }

        return false;
    }

    private static IdleAdventureInitiativeEntryResponse FindInitiativeEntry(IdleAdventureInitiativeEntryResponse[] initiative, string runtimeId)
    {
        if (initiative == null || string.IsNullOrEmpty(runtimeId)) return null;

        for (int i = 0; i < initiative.Length; i++)
        {
            IdleAdventureInitiativeEntryResponse entry = initiative[i];
            if (entry != null && entry.runtime_id == runtimeId) return entry;
        }

        return null;
    }

    private List<IdleAdventureBattleBuff> ConvertBackendBuffs(IdleAdventureActiveBuffResponse[] backendBuffs)
    {
        List<IdleAdventureBattleBuff> buffs = new List<IdleAdventureBattleBuff>();
        if (backendBuffs == null) return buffs;

        for (int i = 0; i < backendBuffs.Length; i++)
        {
            IdleAdventureActiveBuffResponse source = backendBuffs[i];
            if (source == null) continue;

            buffs.Add(IdleAdventureBattleBuff.Create(
                source.buff_key,
                source.display_name,
                ParseBuffCategory(source.category),
                ParseBuffSource(source.source),
                ParseBuffEffectType(source.effect_type),
                Mathf.Max(1, source.remaining_rounds),
                ToInt(source.attack_modifier),
                ToInt(source.defense_modifier),
                ToInt(source.speed_modifier),
                source.normal_damage_multiplier,
                source.attribute_damage_multiplier,
                ParseAttribute(source.affected_attribute)));
        }

        return buffs;
    }

    private void SyncRewardDictionary(Dictionary<string, int> target, IdleAdventureRewardItemResponse[] rewards)
    {
        if (target == null) return;

        target.Clear();
        if (rewards == null) return;

        for (int i = 0; i < rewards.Length; i++)
        {
            IdleAdventureRewardItemResponse reward = rewards[i];
            if (reward == null || reward.quantity <= 0) continue;
            AddRewardToLedger(
                target,
                reward.ResolvedItemKey,
                reward.ResolvedDisplayName,
                ToInt(reward.quantity));
        }
    }

    private void SyncBackendProgressFromSession(IdleAdventureSessionResponse session)
    {
        if (session == null || session.settled_stages == null || session.settled_stages.Length == 0) return;

        int highest = backendHighestStage;
        for (int i = 0; i < session.settled_stages.Length; i++)
        {
            highest = Mathf.Max(highest, session.settled_stages[i]);
        }

        backendHighestStage = highest;
        backendNextStage = Mathf.Max(1, highest + 1);
        SanitizeSelectedReplayStage();
        RefreshStartStageSelectionView();
        if (stageProgress != null) stageProgress.SetProgressFromServer(highest);
        if (afkRewardSource != null) afkRewardSource.SetHighestClearedStageFromProgress(highest);
    }

    private static IdleAdventureDamageAttribute ParseAttribute(string value)
    {
        if (string.IsNullOrEmpty(value)) return IdleAdventureDamageAttribute.Physical;
        switch (value.Trim().ToUpperInvariant())
        {
            case "FIRE":
                return IdleAdventureDamageAttribute.Fire;
            case "WATER":
                return IdleAdventureDamageAttribute.Water;
            case "WOOD":
                return IdleAdventureDamageAttribute.Wood;
            case "EARTH":
                return IdleAdventureDamageAttribute.Earth;
            case "LIGHT":
                return IdleAdventureDamageAttribute.Light;
            case "DARK":
                return IdleAdventureDamageAttribute.Dark;
            case "NONE":
                return IdleAdventureDamageAttribute.None;
            default:
                return IdleAdventureDamageAttribute.Physical;
        }
    }

    private static IdleAdventureBuffCategory ParseBuffCategory(string value)
    {
        return string.Equals(value, "DETRIMENT", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "DEBUFF", StringComparison.OrdinalIgnoreCase)
            ? IdleAdventureBuffCategory.Detriment
            : IdleAdventureBuffCategory.Benefit;
    }

    private static IdleAdventureBuffSource ParseBuffSource(string value)
    {
        return string.Equals(value, "CHARACTER_SKILL", StringComparison.OrdinalIgnoreCase)
            ? IdleAdventureBuffSource.CharacterSkill
            : IdleAdventureBuffSource.Other;
    }

    private static IdleAdventureBuffEffectType ParseBuffEffectType(string value)
    {
        if (string.Equals(value, "ATTRIBUTE_DAMAGE", StringComparison.OrdinalIgnoreCase)) return IdleAdventureBuffEffectType.AttributeDamage;
        if (string.Equals(value, "FIELD", StringComparison.OrdinalIgnoreCase)) return IdleAdventureBuffEffectType.Field;
        return IdleAdventureBuffEffectType.NormalDamage;
    }

    private void OpenStageCompletePanel()
    {
        RegisterStageClearRewards(stage);
        stageChoicePending = true;
        stageFailedPending = false;
        modePausedByChoice = false;
        pendingNextStage = stage + 1;
        paused = true;
        if (pauseButtonText != null) pauseButtonText.text = "Resume";
        if (resultText != null) resultText.text = "Victory";

        if (stageEndPanelView != null)
        {
            string pendingRewards = FormatRewardSummary(pendingRunRewards);
            stageEndPanelView.SetContent($"Stage {stage} Cleared", $"Pending rewards: {pendingRewards}\nChoose whether to continue to the next stage or exit this mode.");
            stageEndPanelView.SetButtonLabels("Continue", "Exit Mode");
        }
        AddLog($"Stage {stage} cleared. Waiting for player choice.");
        ShowEndPanel();
    }

    private void OpenStageFailedPanel()
    {
        stageChoicePending = false;
        stageFailedPending = true;
        modePausedByChoice = false;
        pendingNextStage = -1;
        paused = true;
        if (pauseButtonText != null) pauseButtonText.text = "Resume";
        if (resultText != null) resultText.text = "Defeated";

        if (stageEndPanelView != null)
        {
            string pendingRewards = FormatRewardSummary(pendingRunRewards);
            stageEndPanelView.SetContent("Party Defeated", $"Pending rewards: {pendingRewards}\nChoose whether to restart this stage or exit this mode.");
            stageEndPanelView.SetButtonLabels("Restart", "Exit Mode");
        }

        AddLog($"Friendly party defeated on Stage {stage}. Waiting for player choice.");
        ShowEndPanel();
    }

    private void ContinueNextStage()
    {
        if (!battleStarted) return;
        if (backendModeActive)
        {
            if (!backendRequestPending) StartCoroutine(stageFailedPending ? RetryBackendStageRoutine() : ContinueBackendStageRoutine());
            return;
        }

        if (!CanUseLocalCombatSimulation())
        {
            AddLog("Local stage transition is disabled. Backend session is required.");
            UpdateButtonStates();
            return;
        }

        if (stageFailedPending)
        {
            RestartCurrentStage();
            return;
        }

        if (!stageChoicePending || pendingNextStage < 1) return;

        int nextStage = pendingNextStage;
        stageChoicePending = false;
        stageFailedPending = false;
        modePausedByChoice = false;
        pendingNextStage = -1;
        HideEndPanel();
        HidePausePanel();
        paused = false;
        StartStage(nextStage);
        UpdateButtonStates();
    }

    private void RestartCurrentStage()
    {
        if (!battleStarted || !stageFailedPending) return;
        if (backendModeActive)
        {
            if (!backendRequestPending) StartCoroutine(RetryBackendStageRoutine());
            return;
        }

        if (!CanUseLocalCombatSimulation())
        {
            AddLog("Local restart is disabled. Backend retry is required.");
            UpdateButtonStates();
            return;
        }

        int retryStage = Mathf.Max(1, stage);
        stageChoicePending = false;
        stageFailedPending = false;
        modePausedByChoice = false;
        pendingNextStage = -1;
        HideEndPanel();
        HidePausePanel();
        paused = false;
        StartStage(retryStage);
        AddLog($"Restarting Stage {retryStage}.");
        UpdateButtonStates();
    }

    private void EndMode()
    {
        if (!battleStarted) return;
        if (backendModeActive)
        {
            if (!backendRequestPending) StartCoroutine(EndBackendModeRoutine());
            return;
        }

        if (!CanUseLocalCombatSimulation())
        {
            AddLog("Local reward settlement is disabled. Backend end session is required.");
            UpdateButtonStates();
            return;
        }

        string rewardSummary = SettlePendingRewards();
        stageChoicePending = false;
        stageFailedPending = false;
        modePausedByChoice = true;
        pendingNextStage = -1;
        paused = true;
        battleStarted = false;
        if (pauseButtonText != null) pauseButtonText.text = "Pause";
        if (resultText != null) resultText.text = string.Empty;

        ClearParty(friendlies);
        ClearParty(enemies);
        ClearCombatantRootChildren();
        HideEndPanel();
        HidePausePanel();
        ConfigureCombatScreenVisible(false);
        AddLog("Idle Adventure battle exited to the mode home page.");
        SetWaitingText();
        ShowStartPanel();
        if (startPanelView != null) startPanelView.SetContent("Idle Adventure mode", $"Rewards granted: {rewardSummary}");
        UpdateButtonStates();
    }

    private void ResumePausedBattle()
    {
        if (!battleStarted || !paused || stageChoicePending || stageFailedPending || modePausedByChoice) return;

        if (backendModeActive)
        {
            if (!backendRequestPending) StartCoroutine(ResumeBackendPausedBattleRoutine());
            return;
        }

        if (!CanUseLocalCombatSimulation())
        {
            AddLog("Local resume is disabled. Backend session is required.");
            UpdateButtonStates();
            return;
        }

        SetBattleRunningLocally();
        AddLog("Battle resumed.");
        RefreshBattleView();
        UpdateButtonStates();
    }

    private void ExitPausedBattle()
    {
        if (!battleStarted || stageChoicePending || stageFailedPending || modePausedByChoice) return;

        if (backendModeActive)
        {
            if (!backendRequestPending) StartCoroutine(EndBackendModeRoutine());
            return;
        }

        if (!CanUseLocalCombatSimulation())
        {
            AddLog("Local exit is disabled. Backend end session is required.");
            UpdateButtonStates();
            return;
        }

        EndMode();
    }

    private IEnumerator ResumeBackendPausedBattleRoutine()
    {
        BackendGameService service = ResolveBackendService();
        if (service == null || !service.IsAuthenticated || string.IsNullOrEmpty(backendSessionKey)) yield break;

        backendRequestPending = true;
        UpdateButtonStates();
        BackendResult<IdleAdventureSessionResponse> result = null;
        yield return service.ResumeIdleAdventureSession(backendSessionKey, value => result = value);
        backendRequestPending = false;

        if (result == null || !result.Success || result.Data == null)
        {
            AddLog($"Backend resume failed: {BackendResultMessage(result)}");
            yield return RefreshBackendSessionAfterCommandFailure("resume");
            UpdateButtonStates();
            yield break;
        }

        string status = NormalizeBackendStatus(result.Data.status);
        if (status == "RUNNING")
        {
            SetBattleRunningLocally();
            RefreshBattleView();
            AddLog("Battle resumed by backend.");
        }
        else if (status == "PAUSED" || result.Data.paused)
        {
            paused = true;
            if (pauseButtonText != null) pauseButtonText.text = "Resume";
            ShowPausePanel();
            AddLog("Backend battle is still paused.");
        }
        else
        {
            ApplyBackendSessionSnapshot(result.Data, true);
            HandleBackendSessionStatus(result.Data);
        }

        UpdateButtonStates();
    }

    private IEnumerator ContinueBackendStageRoutine()
    {
        BackendGameService service = ResolveBackendService();
        if (service == null || !service.IsAuthenticated || string.IsNullOrEmpty(backendSessionKey)) yield break;

        backendRequestPending = true;
        UpdateButtonStates();
        BackendResult<IdleAdventureSessionResponse> result = null;
        yield return service.ContinueIdleAdventureStage(backendSessionKey, value => result = value);
        backendRequestPending = false;

        if (result == null || !result.Success || result.Data == null)
        {
            AddLog($"Backend continue failed: {BackendResultMessage(result)}");
            yield return RefreshBackendSessionAfterCommandFailure("continue");
            UpdateButtonStates();
            yield break;
        }

        stageChoicePending = false;
        stageFailedPending = false;
        modePausedByChoice = false;
        pendingNextStage = -1;
        SetBattleRunningLocally();
        HideEndPanel();
        HidePausePanel();
        ApplyBackendSessionSnapshot(result.Data, true);
        yield return ResumeBackendStageTransitionIfPaused("continue", result.Data);
        UpdateButtonStates();
    }

    private IEnumerator RetryBackendStageRoutine()
    {
        BackendGameService service = ResolveBackendService();
        if (service == null || !service.IsAuthenticated || string.IsNullOrEmpty(backendSessionKey)) yield break;

        backendRequestPending = true;
        UpdateButtonStates();
        BackendResult<IdleAdventureSessionResponse> result = null;
        yield return service.RetryIdleAdventureStage(backendSessionKey, value => result = value);
        backendRequestPending = false;

        if (result == null || !result.Success || result.Data == null)
        {
            AddLog($"Backend retry failed: {BackendResultMessage(result)}");
            yield return RefreshBackendSessionAfterCommandFailure("retry");
            UpdateButtonStates();
            yield break;
        }

        stageChoicePending = false;
        stageFailedPending = false;
        modePausedByChoice = false;
        pendingNextStage = -1;
        SetBattleRunningLocally();
        HideEndPanel();
        HidePausePanel();
        ApplyBackendSessionSnapshot(result.Data, true);
        yield return ResumeBackendStageTransitionIfPaused("retry", result.Data);
        UpdateButtonStates();
    }

    private IEnumerator EndBackendModeRoutine()
    {
        BackendGameService service = ResolveBackendService();
        if (service == null || !service.IsAuthenticated || string.IsNullOrEmpty(backendSessionKey)) yield break;

        backendRequestPending = true;
        UpdateButtonStates();
        BackendResult<IdleAdventureSessionResponse> result = null;
        yield return service.EndIdleAdventureSession(backendSessionKey, value => result = value);
        backendRequestPending = false;

        if (result == null || !result.Success || result.Data == null)
        {
            AddLog($"Backend end mode failed: {BackendResultMessage(result)}");
            yield return RefreshBackendSessionAfterCommandFailure("end");
            UpdateButtonStates();
            yield break;
        }

        ApplyBackendSessionSnapshot(result.Data, false);
        FinishBackendExit(result.Data);
        yield return service.LoadIdleAdventureHome(value =>
        {
            if (value != null && value.Success && value.Data != null) ApplyBackendHome(value.Data);
        });
    }

    private IEnumerator RefreshBackendSessionAfterCommandFailure(string commandName)
    {
        BackendGameService service = ResolveBackendService();
        if (service == null || !service.IsAuthenticated || string.IsNullOrEmpty(backendSessionKey)) yield break;

        BackendResult<IdleAdventureSessionResponse> sessionResult = null;
        yield return service.LoadIdleAdventureSession(backendSessionKey, value => sessionResult = value);
        if (sessionResult == null || !sessionResult.Success || sessionResult.Data == null)
        {
            AddLog($"Backend session refresh after {commandName} failed: {BackendResultMessage(sessionResult)}");
            yield break;
        }

        string status = NormalizeBackendStatus(sessionResult.Data.status);
        AddLog($"Backend session after {commandName}: status={status}, stage={sessionResult.Data.stage}, round={sessionResult.Data.round}.");
        ApplyBackendSessionSnapshot(sessionResult.Data, true);

        if (status == "RUNNING" || (IsStageTransitionCommand(commandName) && status == "PAUSED"))
        {
            stageChoicePending = false;
            stageFailedPending = false;
            modePausedByChoice = false;
            pendingNextStage = -1;
            SetBattleRunningLocally();
            HideEndPanel();
            HidePausePanel();
            RefreshBattleView();
            if (status == "PAUSED") yield return ResumeBackendStageTransitionIfPaused(commandName, sessionResult.Data);
            yield break;
        }

        HandleBackendSessionStatus(sessionResult.Data);
    }

    private IEnumerator ResumeBackendStageTransitionIfPaused(string commandName, IdleAdventureSessionResponse session)
    {
        string status = NormalizeBackendStatus(session != null ? session.status : string.Empty);
        if (status == "RUNNING")
        {
            SetBattleRunningLocally();
            HideEndPanel();
            HidePausePanel();
            RefreshBattleView();
            yield break;
        }

        if (!IsStageTransitionCommand(commandName) || status != "PAUSED")
        {
            HandleBackendSessionStatus(session);
            yield break;
        }

        BackendGameService service = ResolveBackendService();
        if (service == null || !service.IsAuthenticated || string.IsNullOrEmpty(backendSessionKey))
        {
            HandleBackendSessionStatus(session);
            yield break;
        }

        AddLog($"Backend {commandName} entered a paused stage. Resuming battle.");
        backendRequestPending = true;
        UpdateButtonStates();
        BackendResult<IdleAdventureSessionResponse> resumeResult = null;
        yield return service.ResumeIdleAdventureSession(backendSessionKey, value => resumeResult = value);
        backendRequestPending = false;

        if (resumeResult != null && resumeResult.Success && resumeResult.Data != null)
        {
            ApplyBackendSessionSnapshot(resumeResult.Data, true);
            if (NormalizeBackendStatus(resumeResult.Data.status) == "RUNNING")
            {
                SetBattleRunningLocally();
                HideEndPanel();
                HidePausePanel();
                RefreshBattleView();
                yield break;
            }

            HandleBackendSessionStatus(resumeResult.Data);
            yield break;
        }

        AddLog($"Backend resume after {commandName} failed: {BackendResultMessage(resumeResult)}");
        HandleBackendSessionStatus(session);
    }

    private static bool IsStageTransitionCommand(string commandName)
    {
        return string.Equals(commandName, "continue", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(commandName, "retry", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeBackendStatus(string status)
    {
        return string.IsNullOrEmpty(status) ? string.Empty : status.Trim().ToUpperInvariant();
    }

    private void FinishBackendExit(IdleAdventureSessionResponse session)
    {
        string rewardSummary = FormatRewardSummary(grantedRewards);
        if (rewardLedger != null) rewardLedger.GrantRewards(grantedRewards, rewardDisplayNames, rewardSummary);

        stageChoicePending = false;
        stageFailedPending = false;
        modePausedByChoice = true;
        pendingNextStage = -1;
        paused = true;
        battleStarted = false;
        backendModeActive = false;
        backendSessionKey = string.Empty;
        backendRuntimeCombatants.Clear();
        if (pauseButtonText != null) pauseButtonText.text = "Pause";
        if (resultText != null) resultText.text = string.Empty;

        ClearParty(friendlies);
        ClearParty(enemies);
        ClearCombatantRootChildren();
        HideEndPanel();
        HidePausePanel();
        ConfigureCombatScreenVisible(false);
        ShowStartPanel();
        SetWaitingText();
        if (startPanelView != null) startPanelView.SetContent("Idle Adventure mode", $"Rewards granted: {rewardSummary}");
        UpdateButtonStates();
    }

    private void SetWaitingText()
    {
        if (stageText != null) stageText.text = "Idle Adventure mode";
        if (turnText != null) turnText.text = "Waiting to start";
        if (resultText != null) resultText.text = string.Empty;
        if (friendlyCountText != null) friendlyCountText.text = $"0/{MaxPartySize} deployed";
        if (enemyCountText != null) enemyCountText.text = $"0/{MaxPartySize} present";
    }

    private void ClearRunRewards()
    {
        clearedRewardStages.Clear();
        pendingRunRewards.Clear();
    }

    private int NextUnclearedStage()
    {
        EnsureStageProgress();
        return stageProgress != null ? stageProgress.NextUnclearedStage : 1;
    }

    private int HighestClearedStage()
    {
        EnsureStageProgress();
        return stageProgress != null ? stageProgress.HighestClearedStage : 0;
    }

    private void LoadStageProgress()
    {
        EnsureStageProgress();
        int loadedStage = stageProgress != null ? stageProgress.LoadProgress() : 0;
        if (afkRewardSource != null) afkRewardSource.SetHighestClearedStageFromProgress(loadedStage);
    }

    private bool ShouldUseBackendApi()
    {
        if (!Application.isPlaying) return false;

        BackendGameService service = BackendGameService.Instance;
        return service != null && service.IsAuthenticated;
    }

    private bool CanUseLocalCombatSimulation()
    {
        return allowLocalCombatSimulation && Application.isEditor;
    }

    private BackendGameService ResolveBackendService()
    {
        BackendGameService service = BackendGameService.Instance;
        if (service == null && Application.isPlaying) service = BackendGameService.EnsureInstance();
        return service;
    }

    private void QueueBackendHomeLoad()
    {
        if (!Application.isPlaying) return;
        if (backendHomeLoadRoutine != null) StopCoroutine(backendHomeLoadRoutine);
        backendHomeLoadRoutine = StartCoroutine(LoadBackendHomeWhenReady());
    }

    private void QueueBackendReferenceDataLoad(int requestedStage)
    {
        if (!Application.isPlaying || !ShouldUseBackendApi()) return;

        int normalizedStage = Mathf.Clamp(requestedStage, 1, 10000);
        if (backendReferenceDataLoaded
            && backendReferenceDataStage == normalizedStage)
        {
            return;
        }

        if (backendReferenceDataRoutine != null)
        {
            StopCoroutine(backendReferenceDataRoutine);
        }

        backendReferenceDataRoutine = StartCoroutine(
            LoadBackendReferenceDataWhenReady(normalizedStage));
    }

    private IEnumerator LoadBackendReferenceDataWhenReady(int requestedStage)
    {
        BackendGameService service = ResolveBackendService();
        float deadline = Time.unscaledTime + 8f;
        while (service != null
            && (!service.IsAuthenticated || service.IsConnecting)
            && Time.unscaledTime < deadline)
        {
            yield return null;
        }

        if (service == null || !service.IsAuthenticated)
        {
            backendReferenceDataRoutine = null;
            yield break;
        }

        BackendResult<IdleAdventureReferenceDataResponse> result = null;
        yield return service.RefreshIdleAdventureReferenceData(
            requestedStage,
            value => result = value);
        backendReferenceDataRoutine = null;

        if (result == null || result.Data == null)
        {
            AddLog("Idle Adventure reference data refresh failed: " + BackendResultMessage(result));
            yield break;
        }

        ApplyBackendReferenceData(result.Data, requestedStage);
        backendReferenceDataLoaded = result.Success;
        backendReferenceDataStage = requestedStage;
        if (!result.Success)
        {
            AddLog("Idle Adventure reference data partially refreshed: " + BackendResultMessage(result));
        }
    }

    private IEnumerator RefreshBackendProgress(BackendGameService service)
    {
        if (service == null || !service.IsAuthenticated) yield break;

        BackendResult<IdleAdventureProgressResponse> result = null;
        yield return service.RefreshIdleAdventureProgress(value => result = value);
        if (result != null && result.Success && result.Data != null)
        {
            ApplyBackendProgress(result.Data);
        }
        else if (result != null)
        {
            AddLog("Idle Adventure progress refresh failed: " + BackendResultMessage(result));
        }
    }

    private IEnumerator LoadBackendHomeWhenReady()
    {
        backendHomeLoadRunning = true;
        BackendGameService service = ResolveBackendService();
        for (int attempt = 0; attempt < 3; attempt++)
        {
            float deadline = Time.unscaledTime + 8f;
            while (service != null && (!service.IsAuthenticated || service.IsConnecting) && Time.unscaledTime < deadline)
            {
                yield return null;
            }

            if (service == null || !service.IsAuthenticated)
            {
                if (attempt < 2)
                {
                    yield return new WaitForSecondsRealtime(2f * (attempt + 1));
                    service = ResolveBackendService();
                    continue;
                }

                break;
            }

            yield return RefreshBackendStamina(service);

            BackendResult<IdleAdventureHomeResponse> result = null;
            yield return service.LoadIdleAdventureHome(value => result = value);
            if (result != null && result.Success && result.Data != null)
            {
                ApplyBackendHome(result.Data);
                yield return RefreshBackendProgress(service);
                QueueBackendReferenceDataLoad(Mathf.Max(1, backendNextStage));
                break;
            }

            AddLog($"Backend home load failed (attempt {attempt + 1}/3): {BackendResultMessage(result)}");
            if (attempt < 2)
            {
                yield return new WaitForSecondsRealtime(2f * (attempt + 1));
            }
        }

        backendHomeLoadRoutine = null;
        backendHomeLoadRunning = false;
    }

    private IEnumerator RefreshBackendStamina(BackendGameService service)
    {
        if (staminaRequestPending || service == null || !service.IsAuthenticated)
        {
            RefreshStaminaView();
            yield break;
        }

        staminaRequestPending = true;
        BackendResult<IdleAdventureStaminaResponse> result = null;
        yield return service.LoadStamina(value => result = value);
        staminaRequestPending = false;
        if (result == null || !result.Success || result.Data == null)
        {
            AddLog("体力同步失败：" + BackendResultMessage(result));
        }
        RefreshStaminaView();
    }

    private void RefreshStaminaView()
    {
        BackendGameService service = BackendGameService.Instance;
        IdleAdventureStaminaResponse stamina = service == null ? null : service.CurrentStamina;
        bool authenticated = service != null && service.IsAuthenticated;
        if (staminaText != null)
        {
            staminaText.text = stamina == null
                ? "体力 --"
                : $"体力 {Mathf.Max(0, ToInt(stamina.CurrentStamina))}/{Mathf.Max(0, ToInt(stamina.MaxStamina))}";
        }

        if (accelerationText != null)
        {
            if (!authenticated)
            {
                accelerationText.text = "请先登录";
            }
            else if (!accelerationPolicyLoaded || stamina == null)
            {
                accelerationText.text = "同步加速配置";
            }
            else if (!accelerationEnabled)
            {
                accelerationText.text = "挂机加速未开放";
            }
            else if (accelerationStaminaCost <= 0)
            {
                accelerationText.text = "挂机加速配置无效";
            }
            else if (stamina.CurrentStamina < accelerationStaminaCost)
            {
                accelerationText.text = $"体力不足，需要 {accelerationStaminaCost}";
            }
            else
            {
                accelerationText.text = $"消耗 {accelerationStaminaCost} 体力 · 下次恢复 {stamina.SecondsToNextRecovery}s";
            }
        }

        if (accelerateButton != null)
        {
            accelerateButton.interactable = !staminaRequestPending
                && authenticated
                && accelerationPolicyLoaded
                && accelerationEnabled
                && accelerationStaminaCost > 0
                && stamina != null
                && stamina.CurrentStamina >= accelerationStaminaCost;
        }
    }

    private void AccelerateAfkReward()
    {
        if (staminaRequestPending) return;

        BackendGameService service = ResolveBackendService();
        if (service == null || !service.IsAuthenticated)
        {
            AddLog("请先登录账号后再加速挂机收益。");
            return;
        }

        SyncAccelerationPolicyFromService();
        if (!accelerationPolicyLoaded)
        {
            AddLog("挂机加速配置尚未同步，请稍后重试。");
            QueueBackendHomeLoad();
            return;
        }
        if (!accelerationEnabled)
        {
            AddLog("挂机加速当前未开放。");
            return;
        }
        if (accelerationStaminaCost <= 0)
        {
            AddLog("挂机加速成本配置无效，请刷新后重试。");
            QueueBackendHomeLoad();
            return;
        }

        IdleAdventureStaminaResponse stamina = service.CurrentStamina;
        if (stamina == null || stamina.CurrentStamina < accelerationStaminaCost)
        {
            AddLog($"体力不足，挂机加速需要 {accelerationStaminaCost} 点体力。");
            StartCoroutine(RefreshBackendStamina(service));
            return;
        }

        staminaRequestPending = true;
        RefreshStaminaView();
        service.AccelerateIdleAdventure(result =>
        {
            staminaRequestPending = false;
            if (result != null && result.Success && result.Data != null)
            {
                ApplyAccelerationPolicy(result.Data.state ?? service.CurrentIdleAdventureAfkReward);
                int cycles = Mathf.Max(0, result.Data.ResolvedAccelerationCycles);
                if (result.Data.ResolvedIdempotentReplay)
                {
                    AddLog("已恢复上一次挂机加速结果，体力和奖励不会重复结算。");
                }
                else
                {
                    AddLog(cycles > 0 ? $"挂机加速完成，结算 {cycles} 个周期。" : "挂机加速完成。");
                }
                if (afkRewardSource != null) afkRewardSource.TryLoadServerState();
            }
            else
            {
                string failureMessage = "挂机加速失败：" + BackendResultMessage(result);
                AddLog(failureMessage);
                Debug.LogWarning(failureMessage, this);
                if (result != null && result.Code == 6513)
                {
                    accelerationPolicyLoaded = true;
                    accelerationEnabled = false;
                    QueueBackendHomeLoad();
                }
                else if (result != null && result.Code == 6511)
                {
                    StartCoroutine(RefreshBackendStamina(service));
                }
            }
            RefreshStaminaView();
        });
    }

    private void SyncAccelerationPolicyFromService()
    {
        BackendGameService service = BackendGameService.Instance;
        IdleAdventureAfkRewardStateResponse state = service == null || !service.IsAuthenticated
            ? null
            : service.CurrentIdleAdventureAfkReward;
        if (state != null && state.ResolvedPlayerId > 0 && state.ResolvedPlayerId != service.PlayerId)
        {
            state = null;
        }
        if (ReferenceEquals(accelerationPolicyState, state)) return;

        ApplyAccelerationPolicy(state);
        RefreshStaminaView();
    }

    private void ApplyAccelerationPolicy(IdleAdventureAfkRewardStateResponse state)
    {
        accelerationPolicyState = state;
        int staminaCost = state == null ? 0 : state.ResolvedAccelerationStaminaCost;
        accelerationPolicyLoaded = state != null;
        accelerationEnabled = state != null && state.ResolvedAccelerationEnabled;
        accelerationStaminaCost = Mathf.Max(0, staminaCost);
    }

    private void ResetAccelerationPolicy()
    {
        accelerationPolicyState = null;
        accelerationPolicyLoaded = false;
        accelerationEnabled = false;
        accelerationStaminaCost = 0;
    }

    private void ApplyBackendHome(IdleAdventureHomeResponse home)
    {
        if (home == null) return;

        BackendGameService service = BackendGameService.Instance;
        ApplyAccelerationPolicy(home.ResolvedAfkReward
            ?? (service == null ? null : service.CurrentIdleAdventureAfkReward));
        RefreshStaminaView();

        int resolvedHighestStage = home.ResolvedHighestStage;
        if (resolvedHighestStage <= 0 && home.progress != null)
        {
            resolvedHighestStage = home.progress.ResolvedHighestStage;
        }

        int resolvedNextStage = home.ResolvedNextStage;
        if (resolvedNextStage <= 0 && home.progress != null)
        {
            resolvedNextStage = home.progress.ResolvedNextStage;
        }
        backendHighestStage = Mathf.Max(0, resolvedHighestStage);
        backendNextStage = Mathf.Max(1, resolvedNextStage > 0 ? resolvedNextStage : backendHighestStage + 1);
        SanitizeSelectedReplayStage();

        IdleAdventureSessionSummaryResponse activeSession = home.ResolvedActiveSession;
        string resolvedSessionKey = activeSession != null
            ? activeSession.ResolvedSessionKey
            : home.ResolvedRunningSessionKey;
        bool hasRunningSession = home.ResolvedHasRunningSession;
        backendRunningSessionKey = hasRunningSession && !string.IsNullOrEmpty(resolvedSessionKey)
            ? resolvedSessionKey
            : string.Empty;
        backendAvailableNpcs = home.available_npc_list;
        if (backendAvailableNpcs == null || backendAvailableNpcs.Length == 0)
        {
            backendAvailableNpcs = ConvertAvailableNpcSummaries(home.ResolvedAvailableNpcs);
        }
        if (backendAvailableNpcs == null) backendAvailableNpcs = new IdleAdventurePlayerNpcResponse[0];

        if (stageProgress != null) stageProgress.SetProgressFromServer(backendHighestStage);
        if (afkRewardSource != null) afkRewardSource.SetHighestClearedStageFromProgress(backendHighestStage);

        IdleAdventureFormationDataResponse resolvedFormation = home.ResolvedFormation;
        if (resolvedFormation != null && !backendFormationDirty)
        {
            ApplyBackendFormation(resolvedFormation.slots);
        }
        else if (backendFormationDirty)
        {
            SanitizeCurrentFormationForBackend();
        }

        if (startPanelView != null)
        {
            string runningText = hasRunningSession && !string.IsNullOrEmpty(resolvedSessionKey)
                ? "Running session can be resumed from backend"
                : acquisitionRouteApplied && acquisitionRouteStage > 0
                    ? $"Authoritative next stage: {backendNextStage}. Guide target: Stage {acquisitionRouteStage}."
                    : $"Authoritative next stage: {backendNextStage}.";
            startPanelView.SetContent("Idle Adventure mode", runningText);
        }

        RefreshStartStageSelectionView();
        RefreshFormationView();
        if (!backendRequestPending) UpdateButtonStates();
        if (backendFormationDirty) SaveBackendFormationIfAvailable();
    }

    private void ApplyBackendProgress(IdleAdventureProgressResponse progress)
    {
        if (progress == null) return;

        int highestStage = Mathf.Max(0, progress.ResolvedHighestStage);
        backendHighestStage = Mathf.Max(backendHighestStage, highestStage);
        if (string.IsNullOrEmpty(backendRunningSessionKey))
        {
            int resolvedLastStage = progress.ResolvedLastStage;
            backendNextStage = Mathf.Max(
                1,
                highestStage > 0
                    ? highestStage + 1
                    : resolvedLastStage > 0 ? resolvedLastStage + 1 : backendNextStage);
        }

        if (stageProgress != null) stageProgress.SetProgressFromServer(highestStage);
        if (afkRewardSource != null) afkRewardSource.SetHighestClearedStageFromProgress(highestStage);
        SanitizeSelectedReplayStage();
        RefreshStartStageSelectionView();
        RefreshStaminaView();
    }

    private void ApplyBackendReferenceData(
        IdleAdventureReferenceDataResponse referenceData,
        int requestedStage)
    {
        if (referenceData == null) return;

        if (referenceData.config != null)
        {
            ApplyBackendModeConfig(referenceData.config);
        }

        if (referenceData.player_npcs != null)
        {
            backendAvailableNpcs = referenceData.player_npcs;
            SanitizeCurrentFormationForBackend();
        }

        if (referenceData.stage_enemies != null)
        {
            backendEnemyPreview = referenceData.stage_enemies;
            if (startPanelView != null && !battleStarted)
            {
                string preview = FormatBackendEnemyPreview(backendEnemyPreview, requestedStage);
                if (!string.IsNullOrEmpty(preview))
                {
                    startPanelView.SetContent("Idle Adventure mode", preview);
                }
            }
        }

        RefreshFormationView();
        if (!backendRequestPending) UpdateButtonStates();
    }

    private void ApplyBackendModeConfig(IdleAdventureModeConfigResponse config)
    {
        if (config == null) return;

        int resolvedStartingFriendlyCount = config.ResolvedStartingFriendlyCount;
        int resolvedBaseEnemyCount = config.ResolvedBaseEnemyCount;
        int resolvedAddEnemyEveryStages = config.ResolvedAddEnemyEveryStages;
        float resolvedTurnInterval = config.ResolvedTurnInterval;
        float resolvedAttackAnimationDuration = config.ResolvedAttackAnimationDuration;
        float resolvedHitFlashDuration = config.ResolvedHitFlashDuration;

        if (resolvedStartingFriendlyCount > 0)
        {
            startingFriendlyCount = Mathf.Clamp(resolvedStartingFriendlyCount, 1, MaxPartySize);
        }
        if (resolvedBaseEnemyCount > 0)
        {
            baseEnemyCount = Mathf.Clamp(resolvedBaseEnemyCount, 1, MaxPartySize);
        }
        if (resolvedAddEnemyEveryStages >= 0)
        {
            addEnemyEveryStages = resolvedAddEnemyEveryStages;
        }
        if (resolvedTurnInterval > 0f)
        {
            turnInterval = Mathf.Max(0.1f, resolvedTurnInterval);
        }
        if (resolvedAttackAnimationDuration > 0f)
        {
            attackAnimationDuration = Mathf.Max(0.05f, resolvedAttackAnimationDuration);
        }
        if (resolvedHitFlashDuration > 0f)
        {
            hitFlashDuration = Mathf.Max(0.05f, resolvedHitFlashDuration);
        }
    }

    private static string FormatBackendEnemyPreview(
        IdleAdventureStageEnemiesResponse preview,
        int requestedStage)
    {
        if (preview == null) return string.Empty;

        int previewStage = preview.stage > 0 ? preview.stage : requestedStage;
        int enemyCount = preview.ResolvedEnemyCount;
        if (enemyCount <= 0 && preview.slots != null)
        {
            for (int i = 0; i < preview.slots.Length; i++)
            {
                if (preview.slots[i] != null && preview.slots[i].enemy != null) enemyCount++;
            }
        }

        List<string> names = new List<string>();
        if (preview.slots != null)
        {
            for (int i = 0; i < preview.slots.Length; i++)
            {
                IdleAdventureEnemySlotResponse slot = preview.slots[i];
                if (slot == null || slot.enemy == null) continue;
                string name = slot.enemy.ResolvedCombatantName;
                if (string.IsNullOrEmpty(name)) name = slot.enemy.ResolvedCombatantKey;
                if (string.IsNullOrEmpty(name)) name = "Enemy";
                if (slot.enemy.level > 0) name += " Lv." + slot.enemy.level;
                names.Add(name);
            }
        }

        string stageName = preview.ResolvedStageName;
        string stageLabel = string.IsNullOrEmpty(stageName)
            ? $"Next stage: {previewStage}"
            : $"Next stage: {previewStage} - {stageName}";
        string summary = stageLabel + $"\nEnemy preview: {enemyCount}";
        return names.Count == 0 ? summary : summary + " (" + string.Join(", ", names.ToArray()) + ")";
    }

    private static IdleAdventurePlayerNpcResponse[] ConvertAvailableNpcSummaries(
        IdleAdventureNpcSummaryResponse[] summaries)
    {
        if (summaries == null || summaries.Length == 0) return null;

        List<IdleAdventurePlayerNpcResponse> converted = new List<IdleAdventurePlayerNpcResponse>(summaries.Length);
        for (int i = 0; i < summaries.Length; i++)
        {
            IdleAdventureNpcSummaryResponse source = summaries[i];
            if (source == null || source.ResolvedNpcId <= 0L) continue;
            converted.Add(new IdleAdventurePlayerNpcResponse
            {
                definition_id = source.ResolvedNpcId,
                combatant_key = source.ResolvedNpcCode,
                combatant_name = source.name,
                side = "FRIENDLY",
                max_health = source.ResolvedTotalHealth > 0L ? source.ResolvedTotalHealth : source.ResolvedBaseHealth,
                attack = source.ResolvedTotalAttack > 0L ? source.ResolvedTotalAttack : source.ResolvedBaseAttack,
                defense = source.ResolvedTotalDefense > 0L ? source.ResolvedTotalDefense : source.ResolvedBaseDefense,
                speed = source.speed,
                attribute_type = source.ResolvedCombatAttribute,
                assigned = false
            });
        }
        return converted.ToArray();
    }

    private void ApplyBackendFormation(IdleAdventureFormationSlotResponse[] slots)
    {
        for (int i = 0; i < MaxPartySize; i++)
        {
            friendlyFormation[i] = -1;
        }

        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            IdleAdventureFormationSlotResponse slot = slots[i];
            if (slot == null) continue;

            int slotIndex = slot.ResolvedSlotIndex;
            long definitionId = slot.ResolvedDefinitionId;
            if (slotIndex < 0 || slotIndex >= MaxPartySize || definitionId <= 0L) continue;

            int definitionIndex = FindBackendNpcIndex(definitionId);
            if (definitionIndex >= 0) friendlyFormation[slotIndex] = definitionIndex;
        }
    }

    private void ApplyBackendFormation(IdleAdventureFormationResponse formation)
    {
        if (formation == null) return;

        ApplyBackendFormation(formation.slots);
        RefreshFormationView();
        UpdateButtonStates();
    }

    private int FindBackendNpcIndex(long definitionId)
    {
        if (backendAvailableNpcs == null) return -1;

        for (int i = 0; i < backendAvailableNpcs.Length; i++)
        {
            if (backendAvailableNpcs[i] != null
                && backendAvailableNpcs[i].ResolvedDefinitionId == definitionId)
            {
                return i;
            }
        }

        return -1;
    }

    private IEnumerator AutoFillBackendFormationRoutine()
    {
        BackendGameService service = ResolveBackendService();
        if (service == null || !service.IsAuthenticated) yield break;

        backendRequestPending = true;
        BackendResult<IdleAdventureFormationResponse> result = null;
        yield return service.AutoFillIdleAdventureFormation(value => result = value);
        backendRequestPending = false;

        if (result != null && result.Success && result.Data != null)
        {
            backendFormationDirty = false;
            ApplyBackendFormation(result.Data);
            AddLog("Backend formation auto-filled.");
        }
        else
        {
            AddLog($"Backend auto-fill failed: {BackendResultMessage(result)}");
        }
    }

    private IEnumerator ClearBackendFormationRoutine()
    {
        BackendGameService service = ResolveBackendService();
        if (service == null || !service.IsAuthenticated) yield break;

        backendRequestPending = true;
        BackendResult<IdleAdventureFormationResponse> result = null;
        yield return service.ClearIdleAdventureFormation(value => result = value);
        backendRequestPending = false;

        if (result != null && result.Success && result.Data != null)
        {
            backendFormationDirty = false;
            ApplyBackendFormation(result.Data);
            AddLog("Backend formation cleared.");
        }
        else
        {
            AddLog($"Backend clear formation failed: {BackendResultMessage(result)}");
        }
    }

    private void SaveBackendFormationIfAvailable()
    {
        if (!ShouldUseBackendApi() || backendRequestPending) return;
        if (!CurrentFormationCanResolveBackendDefinitions())
        {
            Debug.LogWarning("Backend formation save delayed: available NPC definitions are not ready for the current formation.", this);
            QueueBackendHomeLoad();
            return;
        }

        StartCoroutine(SaveBackendFormationRoutine(backendFormationRevision));
    }

    private IEnumerator SaveBackendFormationRoutine(int saveRevision)
    {
        BackendGameService service = ResolveBackendService();
        if (service == null || !service.IsAuthenticated) yield break;

        backendRequestPending = true;
        BackendResult<IdleAdventureFormationResponse> result = null;
        yield return service.SaveIdleAdventureFormation(CreateBackendSaveFormationRequest(), value => result = value);
        backendRequestPending = false;

        if (saveRevision != backendFormationRevision)
        {
            Debug.Log("Backend formation save response ignored because a newer local formation edit exists.", this);
            SaveBackendFormationIfAvailable();
            yield break;
        }

        if (result != null && result.Success && result.Data != null)
        {
            backendFormationDirty = false;
            ApplyBackendFormation(result.Data);
            AddLog("Backend formation saved.");
        }
        else
        {
            AddLog($"Backend formation save failed: {BackendResultMessage(result)}");
        }
    }

    private IdleAdventureSaveFormationRequest CreateBackendSaveFormationRequest()
    {
        IdleAdventureFormationSlotRequest[] slots = new IdleAdventureFormationSlotRequest[MaxPartySize];
        for (int i = 0; i < MaxPartySize; i++)
        {
            int definitionIndex = friendlyFormation[i];
            long definitionId = ResolveBackendDefinitionId(definitionIndex);
            slots[i] = new IdleAdventureFormationSlotRequest
            {
                slot_index = i,
                definition_id = definitionId,
                has_definition_id = definitionId > 0
            };
        }

        return new IdleAdventureSaveFormationRequest { slots = slots };
    }

    private long ResolveBackendDefinitionId(int definitionIndex)
    {
        if (backendAvailableNpcs == null || definitionIndex < 0 || definitionIndex >= backendAvailableNpcs.Length) return 0L;
        IdleAdventurePlayerNpcResponse npc = backendAvailableNpcs[definitionIndex];
        return npc != null ? npc.ResolvedDefinitionId : 0L;
    }

    private bool HasBackendNpcDefinitionsReady()
    {
        return backendAvailableNpcs != null && backendAvailableNpcs.Length > 0;
    }

    private bool CurrentFormationCanResolveBackendDefinitions()
    {
        for (int i = 0; i < MaxPartySize; i++)
        {
            int definitionIndex = friendlyFormation[i];
            if (definitionIndex < 0) continue;
            if (ResolveBackendDefinitionId(definitionIndex) <= 0) return false;
        }

        return true;
    }

    private void SanitizeCurrentFormationForBackend()
    {
        bool changed = false;
        for (int i = 0; i < MaxPartySize; i++)
        {
            int definitionIndex = friendlyFormation[i];
            if (definitionIndex < 0) continue;
            if (ResolveBackendDefinitionId(definitionIndex) > 0) continue;

            friendlyFormation[i] = -1;
            changed = true;
        }

        if (changed)
        {
            backendFormationRevision++;
            Debug.Log("Removed formation slots that are not present in the backend available NPC list.", this);
        }
    }

    private int RecordStageProgress(int clearedStage)
    {
        EnsureStageProgress();
        return stageProgress != null ? stageProgress.RecordStageCleared(clearedStage) : Mathf.Max(0, clearedStage);
    }

    private void RegisterStageClearRewards(int clearedStage)
    {
        int rewardStage = Mathf.Max(1, clearedStage);
        if (clearedRewardStages.Contains(rewardStage)) return;

        clearedRewardStages.Add(rewardStage);
        int highestStage = RecordStageProgress(rewardStage);
        if (afkRewardSource != null) afkRewardSource.SetHighestClearedStage(highestStage);
        IdleAdventureRewardItemDefinition[] rewards = modeConfig != null ? modeConfig.RewardsForStage(rewardStage) : null;
        if (rewards == null || rewards.Length == 0)
        {
            AddLog($"Stage {rewardStage} cleared. No reward configured.");
            return;
        }

        for (int i = 0; i < rewards.Length; i++)
        {
            IdleAdventureRewardItemDefinition reward = rewards[i];
            if (reward == null) continue;
            AddRewardToLedger(pendingRunRewards, reward.ItemKey, reward.DisplayName, reward.Quantity);
        }

        AddLog($"Stage {rewardStage} rewards banked: {FormatRewardsForStage(rewards)}.");
    }

    private string SettlePendingRewards()
    {
        if (pendingRunRewards.Count == 0)
        {
            EnsureRewardLedger();
            if (rewardLedger != null) rewardLedger.GrantRewards(null, rewardDisplayNames, "None");
            ClearRunRewards();
            return "None";
        }

        Dictionary<string, int> settlementRewards = new Dictionary<string, int>(pendingRunRewards);
        string summary = FormatRewardSummary(pendingRunRewards);

        foreach (KeyValuePair<string, int> reward in settlementRewards)
        {
            string displayName = rewardDisplayNames.ContainsKey(reward.Key) ? rewardDisplayNames[reward.Key] : reward.Key;
            AddRewardToLedger(grantedRewards, reward.Key, displayName, reward.Value);
        }

        EnsureRewardLedger();
        if (rewardLedger != null) rewardLedger.GrantRewards(settlementRewards, rewardDisplayNames, summary);

        AddLog($"Rewards granted: {summary}.");
        ClearRunRewards();
        return summary;
    }

    private void AddRewardToLedger(Dictionary<string, int> ledger, string itemKey, string displayName, int quantity)
    {
        if (ledger == null || quantity <= 0) return;

        string key = string.IsNullOrEmpty(itemKey) ? "reward" : itemKey;
        string label = string.IsNullOrEmpty(displayName) ? key : displayName;
        if (!rewardDisplayNames.ContainsKey(key)) rewardDisplayNames.Add(key, label);

        if (ledger.ContainsKey(key)) ledger[key] += quantity;
        else ledger.Add(key, quantity);
    }

    private string FormatRewardsForStage(IdleAdventureRewardItemDefinition[] rewards)
    {
        if (rewards == null || rewards.Length == 0) return "None";

        List<string> parts = new List<string>();
        for (int i = 0; i < rewards.Length; i++)
        {
            IdleAdventureRewardItemDefinition reward = rewards[i];
            if (reward == null) continue;
            parts.Add($"{reward.DisplayName} x{reward.Quantity}");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "None";
    }

    private string FormatRewardSummary(Dictionary<string, int> rewards)
    {
        if (rewards == null || rewards.Count == 0) return "None";

        List<string> parts = new List<string>();
        foreach (KeyValuePair<string, int> reward in rewards)
        {
            string displayName = rewardDisplayNames.ContainsKey(reward.Key) ? rewardDisplayNames[reward.Key] : reward.Key;
            parts.Add($"{displayName} x{reward.Value}");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "None";
    }

    private void UpdateButtonStates()
    {
        bool normalPausePending = battleStarted && paused && !stageChoicePending && !stageFailedPending && !modePausedByChoice;
        bool pausePanelOpen = pausePanelView != null && pausePanelView.IsVisible;
        if (pauseButton != null) pauseButton.interactable = battleStarted && !backendRequestPending && !stageChoicePending && !stageFailedPending && !modePausedByChoice && !pausePanelOpen;
        if (startPanelView != null)
        {
            startPanelView.SetInteractable(!backendRequestPending && !battleStarted && FormationDeployedCount() > 0);
            startPanelView.SetStageSelectionInteractable(!backendRequestPending && !battleStarted && backendHighestStage > 0);
        }
        if (stageEndPanelView != null)
        {
            stageEndPanelView.SetContinueInteractable(!backendRequestPending && battleStarted && !modePausedByChoice && ((stageChoicePending && pendingNextStage > 0) || stageFailedPending));
            stageEndPanelView.SetEndModeInteractable(!backendRequestPending && battleStarted && !modePausedByChoice && (stageChoicePending || stageFailedPending));
        }
        if (pausePanelView != null) pausePanelView.SetInteractable(!backendRequestPending && normalPausePending && pausePanelOpen);
    }

    private RectTransform BuildPartyPanel(RectTransform panel, bool friendlySide)
    {
        TextMeshProUGUI title = CreateText(friendlySide ? "Friendly Party Title" : "Enemy Party Title", panel, friendlySide ? "Friendly Party" : "Enemy Party", 28, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -62f), new Vector2(-20f, -18f));

        TextMeshProUGUI countText = CreateText(friendlySide ? "Friendly Count" : "Enemy Count", panel, string.Empty, 18, FontStyles.Normal, TextAlignmentOptions.Center);
        Anchor(countText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -96f), new Vector2(-20f, -64f));
        if (friendlySide) friendlyCountText = countText;
        else enemyCountText = countText;

        RectTransform grid = CreatePanel(friendlySide ? "Friendly Slots" : "Enemy Slots", panel, new Color(0f, 0f, 0f, 0f));
        Anchor(grid, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(24f, 28f), new Vector2(-24f, -112f));
        BuildPartySlots(grid, friendlySide);
        return grid;
    }

    private void BuildPartySlots(RectTransform grid, bool friendlySide)
    {
        if (grid == null) return;
        for (int i = 0; i < MaxPartySize; i++)
        {
            bool frontColumn = IsFrontColumnSlot(i);
            int row = GetFormationRow(i);
            int column = frontColumn == friendlySide ? 1 : 0;
            float left = column == 0 ? 0f : 0.52f;
            float right = column == 0 ? 0.48f : 1f;
            float top = 1f - row / (float)FormationRows;
            float bottom = 1f - (row + 1f) / FormationRows;

            IdleAdventureActorView view = CreateActorSlot(grid, friendlySide, i);
            Anchor(view.Root, new Vector2(left, bottom), new Vector2(right, top), new Vector2(6f, 8f), new Vector2(-6f, -8f));
            if (friendlySide) friendlyViews[i] = view;
            else enemyViews[i] = view;
        }
    }

    private IdleAdventureActorView CreateActorSlot(RectTransform parent, bool friendlySide, int index)
    {
        string columnName = IsFrontColumnSlot(index) ? "Front Column" : "Back Column";
        RectTransform root = CreatePanel($"{(friendlySide ? "Friendly" : "Enemy")} {columnName} Slot {index + 1}", parent, new Color(0.07f, 0.08f, 0.1f, 0.68f));

        TextMeshProUGUI nameText = CreateText("Name", root, string.Empty, 17, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -30f), new Vector2(-8f, -6f));

        RectTransform healthBar = CreatePanel("Health Bar", root, new Color(0.03f, 0.035f, 0.04f, 1f));
        Anchor(healthBar, new Vector2(0.12f, 1f), new Vector2(0.88f, 1f), new Vector2(0f, -52f), new Vector2(0f, -34f));

        Image healthFill = CreateImage("Health Fill", healthBar, friendlySide ? new Color(0.23f, 0.74f, 0.43f, 1f) : new Color(0.88f, 0.25f, 0.22f, 1f));
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Horizontal;
        healthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        Stretch(healthFill.rectTransform);

        TextMeshProUGUI healthText = CreateText("Health Text", healthBar, string.Empty, 12, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(healthText.rectTransform);

        RectTransform actorRoot = CreatePanel("Actor", root, new Color(0f, 0f, 0f, 0f));
        Anchor(actorRoot, new Vector2(0.5f, 0.46f), new Vector2(0.5f, 0.46f), new Vector2(-42f, -48f), new Vector2(42f, 36f));

        Image body = CreateImage("Body", actorRoot, friendlySide ? new Color(0.18f, 0.62f, 0.93f, 1f) : new Color(0.9f, 0.32f, 0.28f, 1f));
        Anchor(body.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-28f, 8f), new Vector2(28f, 58f));

        Image head = CreateImage("Head", actorRoot, friendlySide ? new Color(0.93f, 0.78f, 0.55f, 1f) : new Color(0.55f, 0.24f, 0.24f, 1f));
        Anchor(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-20f, -26f), new Vector2(20f, 14f));

        RectTransform buffIconRow = CreatePanel("Buff Icon Row", root, new Color(0f, 0f, 0f, 0f));
        Anchor(buffIconRow, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 12f), new Vector2(-10f, 38f));
        Image[] buffIconBackgrounds = new Image[MaxBuffIconSlots];
        TextMeshProUGUI[] buffIconLabels = new TextMeshProUGUI[MaxBuffIconSlots];
        for (int i = 0; i < MaxBuffIconSlots; i++)
        {
            Image buffIcon = CreateImage($"Buff Icon {i + 1}", buffIconRow, new Color(0.2f, 0.22f, 0.25f, 1f));
            float left = i / (float)MaxBuffIconSlots;
            float right = (i + 1f) / MaxBuffIconSlots;
            Anchor(buffIcon.rectTransform, new Vector2(left, 0f), new Vector2(right, 1f), new Vector2(3f, 0f), new Vector2(-3f, 0f));

            TextMeshProUGUI buffIconLabel = CreateText("Label", buffIcon.transform, string.Empty, 10, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(buffIconLabel.rectTransform);
            buffIconBackgrounds[i] = buffIcon;
            buffIconLabels[i] = buffIconLabel;
        }

        TextMeshProUGUI emptyText = CreateText("Empty Text", root, "Empty", 18, FontStyles.Normal, TextAlignmentOptions.Center);
        Stretch(emptyText.rectTransform);
        healthBar.SetAsLastSibling();

        IdleAdventureActorView view = root.gameObject.AddComponent<IdleAdventureActorView>();
        view.Initialize(root, actorRoot, body, healthFill, nameText, healthText, buffIconRow, buffIconBackgrounds, buffIconLabels, emptyText, head);
        return view;
    }

    private IEnumerator BattleLoop()
    {
        yield return null;

        while (enabled)
        {
            while (!battleStarted || paused || stageChoicePending || stageFailedPending || modePausedByChoice) yield return null;

            if (turnText != null) turnText.text = $"Round {round}: speed order";
            if (backendModeActive)
            {
                yield return ResolveBackendRound();
                yield return new WaitForSeconds(turnInterval);
                continue;
            }

            if (!CanUseLocalCombatSimulation())
            {
                if (!localSimulationBlockedLogged)
                {
                    AddLog("Local Idle Adventure combat simulation is disabled. Start an authenticated backend session instead.");
                    localSimulationBlockedLogged = true;
                }

                battleStarted = false;
                paused = true;
                SetWaitingText();
                UpdateButtonStates();
                yield return null;
                continue;
            }

            yield return ResolveInitiativeRound();
            if (AllDefeated(enemies))
            {
                AddLog($"Enemy party defeated. Stage {stage} cleared.");
                OpenStageCompletePanel();
                continue;
            }

            if (AllDefeated(friendlies))
            {
                AddLog("Friendly party was defeated.");
                OpenStageFailedPanel();
                continue;
            }

            round++;
            RefreshBattleView();
            yield return new WaitForSeconds(turnInterval);
        }
    }

    private IEnumerator ResolveBackendRound()
    {
        if (backendRequestPending || string.IsNullOrEmpty(backendSessionKey)) yield break;

        BackendGameService service = ResolveBackendService();
        if (service == null || !service.IsAuthenticated)
        {
            AddLog("Backend session is not authenticated.");
            paused = true;
            UpdateButtonStates();
            yield break;
        }

        backendRequestPending = true;
        int requestedRound = round;
        BackendResult<IdleAdventureSessionResponse> result = null;
        yield return service.ResolveIdleAdventureRound(backendSessionKey, requestedRound, value => result = value);
        backendRequestPending = false;

        if (result == null || !result.Success || result.Data == null)
        {
            AddLog($"Backend round failed: {BackendResultMessage(result)}");
            paused = true;
            UpdateButtonStates();
            yield break;
        }

        if (IsBackendRoundOutOfSync(result.Data, requestedRound))
        {
            Debug.LogWarning($"Backend round playback skipped because local round is out of sync: requested={requestedRound}, responseBefore={result.Data.round_before}, responseRound={result.Data.round}. Applying backend snapshot only.", this);
            AddLog("Backend round replay detected. Synced latest battle state without replaying stale actions.");
            ApplyBackendSessionSnapshot(result.Data, false);
            HandleBackendSessionStatus(result.Data);
            yield break;
        }

        NormalizeBackendActionRuntimeIds(result.Data.actions, result.Data.initiative_order);
        Debug.Log($"Backend resolved round snapshot before playback: round={result.Data.round}, friendlies={FormatBackendStateSlots(result.Data.friendlies)}, enemies={FormatBackendStateSlots(result.Data.enemies)}, actions={FormatBackendActions(result.Data.actions)}.", this);
        if (HasInvalidBackendDamageActions(result.Data.actions))
        {
            AddLog("Backend returned an invalid same-side damage action. Battle paused before applying the corrupted snapshot.");
            paused = true;
            if (pauseButtonText != null) pauseButtonText.text = "Resume";
            UpdateButtonStates();
            yield break;
        }

        yield return PlayBackendActions(result.Data.actions, result.Data.initiative_order);
        ApplyBackendSessionSnapshot(result.Data, false);
        HandleBackendSessionStatus(result.Data);
    }

    private static bool IsBackendRoundOutOfSync(IdleAdventureSessionResponse session, int requestedRound)
    {
        if (session == null || session.round_before <= 0) return false;
        return session.round_before != requestedRound;
    }

    private void NormalizeBackendActionRuntimeIds(IdleAdventureBattleActionResponse[] actions, IdleAdventureInitiativeEntryResponse[] initiative)
    {
        if (actions == null || actions.Length == 0 || initiative == null || initiative.Length == 0) return;

        int initiativeCursor = 0;
        for (int i = 0; i < actions.Length; i++)
        {
            IdleAdventureBattleActionResponse action = actions[i];
            if (action == null) continue;

            bool actorMissing = string.IsNullOrEmpty(action.actor_runtime_id) || FindBackendCombatant(action.actor_runtime_id) == null;
            bool invalidSameSide = action.damage > 0 && SameBackendRuntimeSide(action.actor_runtime_id, action.target_runtime_id);
            int actorIndex = actorMissing || invalidSameSide
                ? -1
                : FindInitiativeIndex(initiative, action.actor_runtime_id, initiativeCursor);
            if (actorIndex >= initiativeCursor)
            {
                initiativeCursor = actorIndex + 1;
                continue;
            }

            if (!actorMissing && !invalidSameSide) continue;

            IdleAdventureInitiativeEntryResponse correctedActor = FindNextInitiativeActorForAction(
                initiative,
                ref initiativeCursor,
                action.target_runtime_id);
            if (correctedActor == null || string.IsNullOrEmpty(correctedActor.runtime_id)) continue;

            string previousActor = action.actor_runtime_id;
            action.actor_runtime_id = correctedActor.runtime_id;
            if (!string.IsNullOrEmpty(correctedActor.combatant_name)) action.actor_name = correctedActor.combatant_name;
            Debug.LogWarning($"Backend action actor corrected from initiative order: order={action.action_order}, actor={previousActor}, correctedActor={action.actor_runtime_id}, target={action.target_runtime_id}.", this);
        }
    }

    private static int FindInitiativeIndex(IdleAdventureInitiativeEntryResponse[] initiative, string runtimeId, int startIndex)
    {
        if (initiative == null || string.IsNullOrEmpty(runtimeId)) return -1;

        int safeStart = Mathf.Clamp(startIndex, 0, initiative.Length);
        for (int i = safeStart; i < initiative.Length; i++)
        {
            IdleAdventureInitiativeEntryResponse entry = initiative[i];
            if (entry != null && entry.runtime_id == runtimeId) return i;
        }

        return -1;
    }

    private IdleAdventureInitiativeEntryResponse FindNextInitiativeActorForAction(
        IdleAdventureInitiativeEntryResponse[] initiative,
        ref int initiativeCursor,
        string targetRuntimeId)
    {
        if (initiative == null) return null;

        int safeStart = Mathf.Clamp(initiativeCursor, 0, initiative.Length);
        for (int i = safeStart; i < initiative.Length; i++)
        {
            IdleAdventureInitiativeEntryResponse entry = initiative[i];
            if (entry == null || string.IsNullOrEmpty(entry.runtime_id)) continue;
            if (!string.IsNullOrEmpty(targetRuntimeId) && SameBackendRuntimeSide(entry.runtime_id, targetRuntimeId)) continue;

            initiativeCursor = i + 1;
            return entry;
        }

        return null;
    }

    private IEnumerator PlayBackendActions(IdleAdventureBattleActionResponse[] actions, IdleAdventureInitiativeEntryResponse[] initiative)
    {
        if (actions == null || actions.Length == 0) yield break;

        for (int i = 0; i < actions.Length; i++)
        {
            while (paused) yield return null;

            IdleAdventureBattleActionResponse action = actions[i];
            if (action == null) continue;

            IdleAdventureCombatant attacker = FindBackendCombatant(action.actor_runtime_id);
            IdleAdventureCombatant target = FindBackendCombatant(action.target_runtime_id);

            if (attacker == null)
            {
                Debug.LogWarning($"Backend action animation skipped because actor is not visible in the current backend snapshot: actor={action.actor_runtime_id}, target={action.target_runtime_id}, order={action.action_order}.", this);
                ApplyBackendDefeatedRuntimeIds(action.defeated_runtime_ids);
                RefreshBattleView();
                continue;
            }

            if (attacker.IsDefeated)
            {
                Debug.LogWarning($"Backend action actor was locally defeated but backend returned an action, correcting local playback state: actor={action.actor_runtime_id}, target={action.target_runtime_id}, order={action.action_order}.", this);
                attacker.SetCurrentHealth(Mathf.Max(1, attacker.CurrentHealth));
                RefreshBattleView();
            }

            IdleAdventureCombatant visualTarget = target != null && !target.IsDefeated
                ? target
                : FirstTargetByFormation(attacker.IsFriendly ? enemies : friendlies, attacker.SlotIndex);
            if (visualTarget == null)
            {
                Debug.LogWarning($"Backend action has no visible target, attack animation will still play: actor={action.actor_runtime_id}, target={action.target_runtime_id}, order={action.action_order}.", this);
            }

            Debug.Log($"Backend action playback: order={action.action_order}, actor={action.actor_runtime_id}, target={action.target_runtime_id}, damage={action.damage}, hp={action.target_health_before}->{action.target_health_after}, defeated={FormatRuntimeIds(action.defeated_runtime_ids)}.", this);
            if (!string.IsNullOrEmpty(action.log)) AddLog(action.log);

            float direction = action.animation != null && action.animation.attack_direction != 0
                ? Mathf.Sign(action.animation.attack_direction)
                : attacker.IsFriendly ? 1f : -1f;
            float previousAttackDuration = attackAnimationDuration;
            float previousHitDuration = hitFlashDuration;
            if (action.animation != null)
            {
                if (action.animation.attack_duration > 0f) attackAnimationDuration = action.animation.attack_duration;
                if (action.animation.hit_flash_duration > 0f) hitFlashDuration = action.animation.hit_flash_duration;
            }

            IdleAdventureActorView attackerView = GetView(attacker);
            IdleAdventureActorView visualTargetView = GetView(visualTarget);
            if (attackerView == null)
            {
                Debug.LogWarning($"Backend action actor has no bound view: actor={action.actor_runtime_id}, slot={attacker.SlotIndex}, order={action.action_order}.", this);
            }

            if (visualTarget != null && visualTargetView == null)
            {
                Debug.LogWarning($"Backend action visual target has no bound view: visualTarget={visualTarget.RuntimeId}, slot={visualTarget.SlotIndex}, actionTarget={action.target_runtime_id}, order={action.action_order}.", this);
            }

            yield return AnimateAttack(attackerView, direction);
            ApplyBackendActionTargetHealth(target, action);
            ApplyBackendDefeatedRuntimeIds(action.defeated_runtime_ids);
            if (visualTargetView != null) yield return AnimateHit(visualTargetView);
            attackAnimationDuration = previousAttackDuration;
            hitFlashDuration = previousHitDuration;
            RefreshBattleView();

            yield return new WaitForSeconds(turnInterval * 0.35f);
        }
    }

    private void ApplyBackendActionTargetHealth(IdleAdventureCombatant target, IdleAdventureBattleActionResponse action)
    {
        if (target == null || action == null) return;

        int nextHealth = ToInt(action.target_health_after);
        bool targetDefeatedByAction = RuntimeIdIn(action.defeated_runtime_ids, target.RuntimeId);
        if (nextHealth <= 0 && !targetDefeatedByAction)
        {
            Debug.LogWarning($"Backend action target health was zero but target was not listed as defeated, keeping local target alive: target={target.RuntimeId}, actionTarget={action.target_runtime_id}, order={action.action_order}.", this);
            return;
        }

        target.SetCurrentHealth(nextHealth);
    }

    private bool HasInvalidBackendDamageActions(IdleAdventureBattleActionResponse[] actions)
    {
        if (actions == null) return false;

        for (int i = 0; i < actions.Length; i++)
        {
            IdleAdventureBattleActionResponse action = actions[i];
            if (action == null || action.damage <= 0) continue;
            if (!SameBackendRuntimeSide(action.actor_runtime_id, action.target_runtime_id)) continue;

            Debug.LogError($"Invalid backend damage action: actor={action.actor_runtime_id}, target={action.target_runtime_id}, damage={action.damage}, order={action.action_order}. The frontend will not apply this backend snapshot.", this);
            return true;
        }

        return false;
    }

    private static bool SameBackendRuntimeSide(string actorRuntimeId, string targetRuntimeId)
    {
        if (string.IsNullOrEmpty(actorRuntimeId) || string.IsNullOrEmpty(targetRuntimeId)) return false;
        if (actorRuntimeId.Length < 1 || targetRuntimeId.Length < 1) return false;

        char actorSide = actorRuntimeId[0];
        char targetSide = targetRuntimeId[0];
        if ((actorSide != 'F' && actorSide != 'E') || (targetSide != 'F' && targetSide != 'E')) return false;
        return actorSide == targetSide;
    }

    private void ApplyBackendDefeatedRuntimeIds(string[] defeatedRuntimeIds)
    {
        if (defeatedRuntimeIds == null) return;

        for (int i = 0; i < defeatedRuntimeIds.Length; i++)
        {
            IdleAdventureCombatant defeated = FindBackendCombatant(defeatedRuntimeIds[i]);
            if (defeated != null) defeated.SetCurrentHealth(0);
        }
    }

    private static bool RuntimeIdIn(string[] runtimeIds, string runtimeId)
    {
        if (runtimeIds == null || string.IsNullOrEmpty(runtimeId)) return false;

        for (int i = 0; i < runtimeIds.Length; i++)
        {
            if (runtimeIds[i] == runtimeId) return true;
        }

        return false;
    }

    private static string FormatRuntimeIds(string[] runtimeIds)
    {
        return runtimeIds == null || runtimeIds.Length == 0 ? "none" : string.Join(",", runtimeIds);
    }

    private static string FormatBackendActions(IdleAdventureBattleActionResponse[] actions)
    {
        if (actions == null || actions.Length == 0) return "none";

        List<string> parts = new List<string>();
        for (int i = 0; i < actions.Length; i++)
        {
            IdleAdventureBattleActionResponse action = actions[i];
            if (action == null) continue;

            parts.Add($"{action.action_order}:{action.actor_runtime_id}->{action.target_runtime_id} dmg={action.damage}");
        }

        return parts.Count == 0 ? "none" : string.Join(", ", parts);
    }

    private static string FormatBackendStateSlots(IdleAdventureCombatantStateResponse[] states)
    {
        if (states == null || states.Length == 0) return "none";

        List<string> parts = new List<string>();
        for (int i = 0; i < states.Length; i++)
        {
            IdleAdventureCombatantStateResponse state = states[i];
            if (state == null) continue;

            string runtimeId = string.IsNullOrEmpty(state.runtime_id) ? $"slot-{state.slot_index}" : state.runtime_id;
            string defeated = state.defeated ? " defeated" : string.Empty;
            parts.Add($"{runtimeId}:{state.current_health}/{state.max_health}{defeated}");
        }

        return parts.Count == 0 ? "none" : string.Join(", ", parts);
    }

    private void HandleBackendSessionStatus(IdleAdventureSessionResponse session)
    {
        if (session == null) return;

        string status = NormalizeBackendStatus(session.status);
        if (session.stage_choice_pending || status == "STAGE_COMPLETE")
        {
            OpenBackendStageCompletePanel(session);
            return;
        }

        if (status == "FAILED" || status == "DEFEATED")
        {
            OpenBackendStageFailedPanel(session);
            return;
        }

        if (status == "ENDED")
        {
            FinishBackendExit(session);
            return;
        }

        stageChoicePending = false;
        stageFailedPending = false;
        modePausedByChoice = false;
        if (status == "RUNNING") SetBattleRunningLocally();
        else
        {
            paused = status == "PAUSED" || session.paused;
            if (pauseButtonText != null) pauseButtonText.text = paused ? "Resume" : "Pause";
            if (paused) ShowPausePanel();
            else HidePausePanel();
        }
        RefreshBattleView();
        UpdateButtonStates();
    }

    private void OpenBackendStageCompletePanel(IdleAdventureSessionResponse session)
    {
        if (session != null && !string.IsNullOrEmpty(session.session_key))
        {
            string clearKey = session.session_key + ":" + Mathf.Max(1, session.stage);
            if (backendStageClearRequests.Add(clearKey))
            {
                StartCoroutine(ConfirmBackendStageClear(session.session_key, Mathf.Max(1, session.stage)));
            }
        }

        stageChoicePending = true;
        stageFailedPending = false;
        modePausedByChoice = false;
        pendingNextStage = session != null && session.pending_next_stage > 0 ? session.pending_next_stage : stage + 1;
        paused = true;
        if (pauseButtonText != null) pauseButtonText.text = "Resume";
        if (resultText != null) resultText.text = "Victory";
        ClearParty(enemies);

        if (stageEndPanelView != null)
        {
            string pendingRewards = FormatRewardSummary(pendingRunRewards);
            stageEndPanelView.SetContent($"Stage {stage} Cleared", $"Pending rewards: {pendingRewards}\nChoose whether to continue to the next stage or exit this mode.");
            stageEndPanelView.SetButtonLabels("Continue", "Exit Mode");
        }

        AddLog($"Stage {stage} cleared by backend. Waiting for player choice.");
        RefreshBattleView();
        HidePausePanel();
        ShowEndPanel();
    }

    private IEnumerator ConfirmBackendStageClear(string sessionKey, int clearedStage)
    {
        BackendGameService service = ResolveBackendService();
        if (service == null || !service.IsAuthenticated)
        {
            yield break;
        }

        BackendResult<IdleAdventureStageClearResponse> result = null;
        yield return service.ClearIdleAdventureStage(sessionKey, clearedStage, value => result = value);
        if (result == null || !result.Success)
        {
            AddLog("关卡结算同步失败：" + BackendResultMessage(result));
            yield break;
        }

        if (result.Data != null && result.Data.ResolvedProgress != null)
        {
            int highestStage = result.Data.ResolvedProgress.ResolvedHighestStage;
            backendHighestStage = Mathf.Max(backendHighestStage, highestStage);
            backendNextStage = Mathf.Max(1, highestStage + 1);
            if (stageProgress != null) stageProgress.SetProgressFromServer(highestStage);
            if (afkRewardSource != null) afkRewardSource.SetHighestClearedStageFromProgress(highestStage);
        }
        RefreshStaminaView();
    }

    private void OpenBackendStageFailedPanel(IdleAdventureSessionResponse session)
    {
        stageChoicePending = false;
        stageFailedPending = true;
        modePausedByChoice = false;
        pendingNextStage = -1;
        paused = true;
        if (pauseButtonText != null) pauseButtonText.text = "Resume";
        if (resultText != null) resultText.text = "Defeated";

        if (stageEndPanelView != null)
        {
            string pendingRewards = FormatRewardSummary(pendingRunRewards);
            stageEndPanelView.SetContent("Party Defeated", $"Pending rewards: {pendingRewards}\nChoose whether to restart this stage or exit this mode.");
            stageEndPanelView.SetButtonLabels("Restart", "Exit Mode");
        }

        AddLog("Friendly party was defeated by backend. Waiting for player choice.");
        HidePausePanel();
        ShowEndPanel();
    }

    private IEnumerator ResolveInitiativeRound()
    {
        List<IdleAdventureCombatant> turnOrder = BuildInitiativeOrder();
        AddLog($"Round {round} order: {FormatInitiativeOrder(turnOrder)}");

        for (int i = 0; i < turnOrder.Count; i++)
        {
            while (paused) yield return null;

            IdleAdventureCombatant attacker = turnOrder[i];
            if (attacker == null || attacker.IsDefeated) continue;

            List<IdleAdventureCombatant> defenders = attacker.IsFriendly ? enemies : friendlies;
            IdleAdventureCombatant target = FirstTargetByFormation(defenders, attacker.SlotIndex);
            if (target == null) yield break;

            IdleAdventureBattleSkill skill = attacker.FirstReadySkill();
            int damage = CalculateDamage(attacker, target, skill != null ? skill.Definition : null);
            string actionName = skill != null ? skill.Definition.SkillName : "Attack";
            AddLog($"{attacker.CombatantName} (SPD {attacker.EffectiveSpeed}) uses {actionName} on {target.CombatantName} for {damage}.");
            yield return AnimateAttack(GetView(attacker), attacker.IsFriendly ? 1f : -1f);
            target.TakeDamage(damage);
            ApplySkillBuffs(attacker, target, skill);
            if (skill != null) skill.TriggerCooldown();
            yield return AnimateHit(GetView(target));
            RefreshBattleView();

            if (target.IsDefeated)
            {
                AddLog($"{target.CombatantName} is defeated.");
                RefreshBattleView();
            }

            if (AllDefeated(defenders)) yield break;
            yield return new WaitForSeconds(turnInterval * 0.35f);
        }

        TickBuffs(friendlies);
        TickBuffs(enemies);
        TickSkillCooldowns(friendlies);
        TickSkillCooldowns(enemies);
        RefreshBattleView();
    }

    private List<IdleAdventureCombatant> BuildInitiativeOrder()
    {
        List<IdleAdventureCombatant> turnOrder = new List<IdleAdventureCombatant>(friendlies.Count + enemies.Count);
        AddLivingCombatants(friendlies, turnOrder);
        AddLivingCombatants(enemies, turnOrder);
        turnOrder.Sort(CompareInitiative);
        return turnOrder;
    }

    private static void AddLivingCombatants(List<IdleAdventureCombatant> source, List<IdleAdventureCombatant> destination)
    {
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null && !source[i].IsDefeated) destination.Add(source[i]);
        }
    }

    private static int CompareInitiative(IdleAdventureCombatant a, IdleAdventureCombatant b)
    {
        int speedComparison = b.EffectiveSpeed.CompareTo(a.EffectiveSpeed);
        if (speedComparison != 0) return speedComparison;
        if (a.IsFriendly != b.IsFriendly) return a.IsFriendly ? -1 : 1;
        return a.SlotIndex.CompareTo(b.SlotIndex);
    }

    private static string FormatInitiativeOrder(List<IdleAdventureCombatant> turnOrder)
    {
        if (turnOrder == null || turnOrder.Count == 0) return "none";

        List<string> names = new List<string>();
        int count = Mathf.Min(6, turnOrder.Count);
        for (int i = 0; i < count; i++)
        {
            names.Add($"{turnOrder[i].CombatantName}({turnOrder[i].EffectiveSpeed})");
        }
        return string.Join(" > ", names);
    }

    private void StartStage(int nextStage)
    {
        stage = nextStage;
        round = 1;
        stageChoicePending = false;
        stageFailedPending = false;
        modePausedByChoice = false;
        pendingNextStage = -1;
        paused = false;
        if (pauseButtonText != null) pauseButtonText.text = "Pause";
        if (resultText != null) resultText.text = string.Empty;
        HideEndPanel();
        HidePausePanel();

        BuildFriendlyParty(Mathf.Clamp(startingFriendlyCount, 1, MaxPartySize));
        BuildEnemyParty(CalculateEnemyCountForStage(stage));

        AddLog($"Stage {stage} begins. {DeployedCount(friendlies)} friendly unit(s) vs {DeployedCount(enemies)} enemy unit(s).");
        RefreshBattleView();
        UpdateButtonStates();
    }

    private void BuildFriendlyParty(int count)
    {
        ClearParty(friendlies);
        if (FormationDeployedCount() <= 0) InitializeDefaultFormation();

        for (int slotIndex = 0; slotIndex < MaxPartySize; slotIndex++)
        {
            int definitionIndex = friendlyFormation[slotIndex];
            friendlies.Add(definitionIndex >= 0 ? CreateFriendlyCombatantForFormation(definitionIndex, slotIndex) : null);
        }
    }

    private void BuildEnemyParty(int count)
    {
        ClearParty(enemies);
        int enemyCount = Mathf.Clamp(count, 1, MaxPartySize);
        for (int slotIndex = 0; slotIndex < MaxPartySize; slotIndex++)
        {
            enemies.Add(slotIndex < enemyCount ? CreateEnemyCombatantForSlot(slotIndex) : null);
        }
    }

    private IdleAdventureCombatant CreateFriendlyCombatantForFormation(int definitionIndex, int slotIndex)
    {
        IdleAdventureCombatantDefinition[] lineup = modeConfig != null ? modeConfig.FriendlyLineup : null;
        IdleAdventureCombatantDefinition definition = lineup != null && definitionIndex >= 0 && definitionIndex < lineup.Length ? lineup[definitionIndex] : null;
        if (definition != null)
        {
            string combatantName = definition.ResolveName($"Hero {definitionIndex + 1}");
            return CreateCombatant(combatantName, Mathf.Max(1, definition.MaxHealth), Mathf.Max(0, definition.Attack), Mathf.Max(0, definition.Defense), Mathf.Max(1, definition.Speed), true, slotIndex, definition.SkillSlots, definition.AttributeType);
        }

        int health = friendlyBaseHealth + definitionIndex * 14;
        int attack = friendlyBaseAttack + definitionIndex * 3;
        int defense = friendlyBaseDefense + definitionIndex;
        int speed = friendlyBaseSpeed + (MaxPartySize - definitionIndex);
        return CreateCombatant($"Hero {definitionIndex + 1}", health, attack, defense, speed, true, slotIndex);
    }

    private IdleAdventureCombatant CreateEnemyCombatantForSlot(int slotIndex)
    {
        IdleAdventureCombatantDefinition[] lineup = modeConfig != null ? modeConfig.EnemyLineup : null;
        IdleAdventureCombatantDefinition definition = lineup != null && slotIndex >= 0 && slotIndex < lineup.Length ? lineup[slotIndex] : null;
        if (definition != null)
        {
            int stageOffset = stage - 1;
            string combatantName = definition.ResolveName($"Enemy {stage}-{slotIndex + 1}");
            int health = Mathf.Max(1, definition.MaxHealth + stageOffset * enemyHealthPerStage);
            int attack = Mathf.Max(0, definition.Attack + stageOffset * enemyAttackPerStage);
            int defense = Mathf.Max(0, definition.Defense + Mathf.Max(0, stage / 3));
            int speed = Mathf.Max(1, definition.Speed + stageOffset * enemySpeedPerStage);
            return CreateCombatant(combatantName, health, attack, defense, speed, false, slotIndex, definition.SkillSlots, definition.AttributeType);
        }

        int fallbackHealth = enemyBaseHealth + (stage - 1) * enemyHealthPerStage + slotIndex * 12;
        int fallbackAttack = enemyBaseAttack + (stage - 1) * enemyAttackPerStage + slotIndex * 2;
        int fallbackDefense = enemyBaseDefense + Mathf.Max(0, stage / 3) + slotIndex / 2;
        int fallbackSpeed = enemyBaseSpeed + (stage - 1) * enemySpeedPerStage + (MaxPartySize - slotIndex);
        return CreateCombatant($"Enemy {stage}-{slotIndex + 1}", fallbackHealth, fallbackAttack, fallbackDefense, fallbackSpeed, false, slotIndex);
    }

    private int AddConfiguredCombatants(List<IdleAdventureCombatant> party, IdleAdventureCombatantDefinition[] lineup, int requestedCount, bool isFriendly)
    {
        if (lineup == null || lineup.Length == 0) return 0;

        int definitionCount = Mathf.Min(lineup.Length, requestedCount, MaxPartySize);
        for (int i = 0; i < definitionCount; i++)
        {
            IdleAdventureCombatantDefinition definition = lineup[i];
            if (definition == null) continue;

            int slotIndex = party.Count;
            int stageOffset = isFriendly ? 0 : stage - 1;
            int health = Mathf.Max(1, definition.MaxHealth + stageOffset * enemyHealthPerStage);
            int attack = Mathf.Max(0, definition.Attack + stageOffset * enemyAttackPerStage);
            int defense = Mathf.Max(0, definition.Defense + (isFriendly ? 0 : Mathf.Max(0, stage / 3)));
            int speed = Mathf.Max(1, definition.Speed + stageOffset * enemySpeedPerStage);
            string fallbackName = isFriendly ? $"Hero {slotIndex + 1}" : $"Enemy {stage}-{slotIndex + 1}";
            party.Add(CreateCombatant(definition.ResolveName(fallbackName), health, attack, defense, speed, isFriendly, slotIndex, definition.SkillSlots, definition.AttributeType));
        }

        return party.Count;
    }

    private IdleAdventureCombatant CreateCombatant(string combatantName, int health, int attack, int defense, int speed, bool isFriendly, int slotIndex, IdleAdventureSkillDefinition[] skillDefinitions = null, IdleAdventureDamageAttribute attributeType = IdleAdventureDamageAttribute.Physical)
    {
        EnsureCombatantRoot();

        IdleAdventureCombatant combatantPrefab = isFriendly ? friendlyCombatantPrefab : enemyCombatantPrefab;
        IdleAdventureCombatant combatant;
        if (combatantPrefab != null)
        {
            combatant = Instantiate(combatantPrefab, combatantRoot, false);
            combatant.name = combatantName;
        }
        else
        {
            GameObject combatantObject = new GameObject(combatantName, typeof(IdleAdventureCombatant));
            combatantObject.transform.SetParent(combatantRoot, false);
            combatant = combatantObject.GetComponent<IdleAdventureCombatant>();
        }

        combatant.Initialize(combatantName, health, attack, defense, speed, isFriendly, slotIndex, skillDefinitions, isFriendly ? FriendlySkillSlotCount : EnemySkillSlotCount, attributeType);
        return combatant;
    }

    private void ClearParty(List<IdleAdventureCombatant> party)
    {
        for (int i = 0; i < party.Count; i++)
        {
            if (party[i] == null) continue;
            DestroyRuntimeObject(party[i].gameObject);
        }

        party.Clear();
    }

    private void ClearCombatantRootChildren()
    {
        EnsureCombatantRoot();
        if (combatantRoot == null) return;

        for (int i = combatantRoot.childCount - 1; i >= 0; i--)
        {
            DestroyRuntimeObject(combatantRoot.GetChild(i).gameObject);
        }
    }

    private void DestroyRuntimeObject(GameObject runtimeObject)
    {
        if (runtimeObject == null) return;

        if (Application.isPlaying) Destroy(runtimeObject);
        else DestroyImmediate(runtimeObject);
    }

    private int CalculateEnemyCountForStage(int currentStage)
    {
        int extra = addEnemyEveryStages <= 0 ? 0 : (currentStage - 1) / addEnemyEveryStages;
        return Mathf.Clamp(baseEnemyCount + extra, 1, MaxPartySize);
    }

    private void ReviveFriendlyParty()
    {
        for (int i = 0; i < friendlies.Count; i++)
        {
            if (friendlies[i] != null) friendlies[i].HealToFull();
        }

        BuildEnemyParty(CalculateEnemyCountForStage(stage));
        round = 1;
        paused = false;
        if (resultText != null) resultText.text = string.Empty;
        AddLog($"Friendly party recovered. Retrying Stage {stage}.");
        RefreshBattleView();
        UpdateButtonStates();
    }

    private void RefreshBattleView()
    {
        if (stageText != null) stageText.text = $"Idle Adventure mode  |  Stage {stage}";
        if (turnText != null) turnText.text = $"Round {round}  |  Allies {LivingCount(friendlies)}/{DeployedCount(friendlies)}  Enemies {LivingCount(enemies)}/{DeployedCount(enemies)}";
        if (friendlyCountText != null) friendlyCountText.text = $"{DeployedCount(friendlies)}/{MaxPartySize} deployed";
        if (enemyCountText != null) enemyCountText.text = $"{DeployedCount(enemies)}/{MaxPartySize} present";

        RefreshPartyViews(friendlies, friendlyViews);
        RefreshPartyViews(enemies, enemyViews);
        if (logText != null) logText.text = string.Join("\n", battleLog.ToArray());
    }

    private void RefreshPartyViews(List<IdleAdventureCombatant> party, IdleAdventureActorView[] views)
    {
        for (int i = 0; i < MaxPartySize; i++)
        {
            IdleAdventureCombatant combatant = i < party.Count ? party[i] : null;
            if (views[i] != null) views[i].Apply(combatant);
        }
    }

    private IdleAdventureActorView GetView(IdleAdventureCombatant combatant)
    {
        if (combatant == null) return null;
        IdleAdventureActorView[] views = combatant.IsFriendly ? friendlyViews : enemyViews;
        return combatant.SlotIndex >= 0 && combatant.SlotIndex < views.Length ? views[combatant.SlotIndex] : null;
    }

    private static bool IsFrontColumnSlot(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < FormationRows;
    }

    private static int GetFormationRow(int slotIndex)
    {
        return Mathf.Clamp(slotIndex % FormationRows, 0, FormationRows - 1);
    }

    private static IdleAdventureCombatant FirstLiving(List<IdleAdventureCombatant> party)
    {
        for (int i = 0; i < party.Count; i++)
        {
            if (party[i] != null && !party[i].IsDefeated) return party[i];
        }
        return null;
    }

    private static IdleAdventureCombatant FirstTargetByFormation(List<IdleAdventureCombatant> party, int attackerSlotIndex)
    {
        IdleAdventureCombatant frontTarget = FirstLivingInRange(party, 0, FormationRows);
        int start = frontTarget != null ? 0 : FormationRows;
        int end = frontTarget != null ? FormationRows : MaxPartySize;
        return FirstLivingByRowPriority(party, start, end, GetFormationRow(attackerSlotIndex));
    }

    private static IdleAdventureCombatant FirstLivingInRange(List<IdleAdventureCombatant> party, int startInclusive, int endExclusive)
    {
        int end = Mathf.Min(endExclusive, party.Count);
        for (int i = startInclusive; i < end; i++)
        {
            if (party[i] != null && !party[i].IsDefeated) return party[i];
        }
        return null;
    }

    private static IdleAdventureCombatant FirstLivingByRowPriority(List<IdleAdventureCombatant> party, int startInclusive, int endExclusive, int preferredRow)
    {
        IdleAdventureCombatant sameRow = LivingAtSlot(party, startInclusive + preferredRow);
        if (sameRow != null) return sameRow;

        for (int row = preferredRow - 1; row >= 0; row--)
        {
            IdleAdventureCombatant upperRow = LivingAtSlot(party, startInclusive + row);
            if (upperRow != null) return upperRow;
        }

        int rowCount = endExclusive - startInclusive;
        for (int row = preferredRow + 1; row < rowCount; row++)
        {
            IdleAdventureCombatant lowerRow = LivingAtSlot(party, startInclusive + row);
            if (lowerRow != null) return lowerRow;
        }

        return null;
    }

    private static IdleAdventureCombatant LivingAtSlot(List<IdleAdventureCombatant> party, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= party.Count) return null;
        return party[slotIndex] != null && !party[slotIndex].IsDefeated ? party[slotIndex] : null;
    }

    private static bool AllDefeated(List<IdleAdventureCombatant> party)
    {
        return FirstLiving(party) == null;
    }

    private static int LivingCount(List<IdleAdventureCombatant> party)
    {
        int count = 0;
        for (int i = 0; i < party.Count; i++)
        {
            if (party[i] != null && !party[i].IsDefeated) count++;
        }
        return count;
    }

    private static int DeployedCount(List<IdleAdventureCombatant> party)
    {
        int count = 0;
        for (int i = 0; i < party.Count; i++)
        {
            if (party[i] != null) count++;
        }

        return count;
    }

    private static bool TryApplyBuff(List<IdleAdventureCombatant> party, int slotIndex, IdleAdventureBattleBuff buff)
    {
        if (buff == null) return false;
        if (slotIndex < 0 || slotIndex >= party.Count) return false;
        IdleAdventureCombatant target = party[slotIndex];
        if (target == null || target.IsDefeated) return false;

        target.AddBuff(buff);
        return true;
    }

    private static void TickBuffs(List<IdleAdventureCombatant> party)
    {
        for (int i = 0; i < party.Count; i++)
        {
            if (party[i] != null) party[i].TickBuffs();
        }
    }

    private static void TickSkillCooldowns(List<IdleAdventureCombatant> party)
    {
        for (int i = 0; i < party.Count; i++)
        {
            if (party[i] != null) party[i].TickSkillCooldowns();
        }
    }

    private static int ToInt(long value)
    {
        if (value <= int.MinValue) return int.MinValue;
        if (value >= int.MaxValue) return int.MaxValue;
        return (int)value;
    }

    private static string BackendResultMessage<T>(BackendResult<T> result)
    {
        if (result == null) return "No backend response.";
        string message = string.IsNullOrEmpty(result.Message) ? "No backend message." : result.Message;
        string businessCode = result.Code > 0 ? $", code {result.Code}" : string.Empty;
        return $"HTTP {result.HttpStatus}{businessCode}: {message}";
    }

    private int CalculateDamage(IdleAdventureCombatant attacker, IdleAdventureCombatant target, IdleAdventureSkillDefinition skill)
    {
        if (attacker == null || target == null) return 0;

        int baseDamage = Mathf.Max(0, attacker.EffectiveAttack - target.EffectiveDefense);
        float skillMultiplier = skill != null ? skill.SkillMultiplier : 1f;
        float normalBuffMultiplier = attacker.SumNormalDamageBuffMultiplier();
        float attributeBuffMultiplier = attacker.SumAttributeDamageBuffMultiplier();
        float fieldBuffMultiplier = CalculateFieldBuffMultiplier(attacker);
        float restraintMultiplier = IsAttributeRestrained(attacker.AttributeType, target.AttributeType) ? AttributeRestraintMultiplier : 0f;
        float bonusMultiplier = 1f + normalBuffMultiplier + attributeBuffMultiplier + fieldBuffMultiplier + restraintMultiplier;
        float finalDamage = baseDamage * skillMultiplier * bonusMultiplier;
        return Mathf.Max(0, Mathf.FloorToInt(finalDamage));
    }

    private float CalculateFieldBuffMultiplier(IdleAdventureCombatant attacker)
    {
        if (attacker == null || modeConfig == null || modeConfig.FieldBuffs == null) return 0f;

        float value = 0f;
        IdleAdventureFieldBuffDefinition[] fieldBuffs = modeConfig.FieldBuffs;
        for (int i = 0; i < fieldBuffs.Length; i++)
        {
            IdleAdventureFieldBuffDefinition fieldBuff = fieldBuffs[i];
            if (fieldBuff == null) continue;
            if (attacker.IsFriendly && !fieldBuff.AffectFriendlies) continue;
            if (!attacker.IsFriendly && !fieldBuff.AffectEnemies) continue;
            if (!IdleAdventureCombatant.AttributeMatches(attacker.AttributeType, fieldBuff.AffectedAttribute)) continue;

            value += fieldBuff.FieldDamageMultiplier;
        }

        return value;
    }

    private bool IsAttributeRestrained(IdleAdventureDamageAttribute attackerAttribute, IdleAdventureDamageAttribute defenderAttribute)
    {
        if (attackerAttribute == IdleAdventureDamageAttribute.None || defenderAttribute == IdleAdventureDamageAttribute.None) return false;
        if (modeConfig == null || modeConfig.AttributeRestraints == null) return false;

        IdleAdventureAttributeRestraintDefinition[] restraints = modeConfig.AttributeRestraints;
        for (int i = 0; i < restraints.Length; i++)
        {
            IdleAdventureAttributeRestraintDefinition restraint = restraints[i];
            if (restraint == null) continue;
            if (restraint.AttackerAttribute == attackerAttribute && restraint.DefenderAttribute == defenderAttribute) return true;
        }

        return false;
    }

    private void ApplySkillBuffs(IdleAdventureCombatant attacker, IdleAdventureCombatant target, IdleAdventureBattleSkill skill)
    {
        if (skill == null || skill.Definition == null) return;

        if (skill.Definition.HasSelfBuff) ApplyBuffPresetToCombatant(attacker, skill.Definition.SelfBuffPresetIndex);
        if (skill.Definition.HasTargetBuff) ApplyBuffPresetToCombatant(target, skill.Definition.TargetBuffPresetIndex);
    }

    private bool ApplyBuffPresetToCombatant(IdleAdventureCombatant target, int presetIndex)
    {
        if (target == null || target.IsDefeated || modeConfig == null || modeConfig.BuffPresets == null || presetIndex < 0 || presetIndex >= modeConfig.BuffPresets.Length) return false;

        IdleAdventureBuffDefinition preset = modeConfig.BuffPresets[presetIndex];
        if (preset == null) return false;

        IdleAdventureBattleBuff buff = preset.CreateRuntimeBuff();
        target.AddBuff(buff);
        string verb = buff.Category == IdleAdventureBuffCategory.Benefit ? "gained" : "suffered";
        AddLog($"{target.CombatantName} {verb} {buff.DisplayName} from {buff.SourceLabel}.");
        return true;
    }

    private IEnumerator AnimateAttack(IdleAdventureActorView attacker, float direction)
    {
        if (attacker == null || attacker.ActorRoot == null) yield break;

        RectTransform rectTransform = attacker.ActorRoot;
        Vector2 start = rectTransform.anchoredPosition;
        Vector2 lunge = start + new Vector2(36f * direction, 8f);
        float halfDuration = attackAnimationDuration * 0.5f;

        yield return MoveRect(rectTransform, start, lunge, halfDuration);
        yield return MoveRect(rectTransform, lunge, start, halfDuration);
    }

    private IEnumerator MoveRect(RectTransform rectTransform, Vector2 from, Vector2 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = 1f - Mathf.Pow(1f - t, 3f);
            rectTransform.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
            yield return null;
        }

        rectTransform.anchoredPosition = to;
    }

    private IEnumerator AnimateHit(IdleAdventureActorView target)
    {
        if (target == null || target.Body == null) yield break;

        Color originalColor = target.Body.color;
        target.Body.color = Color.white;
        yield return new WaitForSeconds(hitFlashDuration);
        target.Body.color = originalColor;
    }

    private void AddLog(string message)
    {
        battleLog.Enqueue(message);
        while (battleLog.Count > 5) battleLog.Dequeue();
        if (logText != null) logText.text = string.Join("\n", battleLog.ToArray());
    }

    private void TogglePause()
    {
        if (!battleStarted || stageChoicePending || stageFailedPending || modePausedByChoice) return;

        if (backendModeActive)
        {
            if (!backendRequestPending) StartCoroutine(ToggleBackendPauseRoutine());
            return;
        }

        if (!CanUseLocalCombatSimulation())
        {
            AddLog("Local pause is disabled. Backend session is required.");
            UpdateButtonStates();
            return;
        }

        paused = !paused;
        if (pauseButtonText != null) pauseButtonText.text = paused ? "Resume" : "Pause";
        if (paused) ShowPausePanel();
        else HidePausePanel();
        AddLog(paused ? "Battle paused." : "Battle resumed.");
        UpdateButtonStates();
    }

    private IEnumerator ToggleBackendPauseRoutine()
    {
        BackendGameService service = ResolveBackendService();
        if (service == null || !service.IsAuthenticated || string.IsNullOrEmpty(backendSessionKey)) yield break;

        backendRequestPending = true;
        UpdateButtonStates();
        bool requestedResume = paused;
        BackendResult<IdleAdventureSessionResponse> result = null;
        if (requestedResume) yield return service.ResumeIdleAdventureSession(backendSessionKey, value => result = value);
        else yield return service.PauseIdleAdventureSession(backendSessionKey, value => result = value);
        backendRequestPending = false;

        if (result != null && result.Success && result.Data != null)
        {
            string status = NormalizeBackendStatus(result.Data.status);
            paused = status == "PAUSED" || (result.Data.paused && status != "RUNNING");
            if (pauseButtonText != null) pauseButtonText.text = paused ? "Resume" : "Pause";
            if (paused) ShowPausePanel();
            else HidePausePanel();
            AddLog(paused ? "Battle paused by backend." : "Battle resumed by backend.");
        }
        else
        {
            AddLog($"Backend pause toggle failed: {BackendResultMessage(result)}");
        }

        UpdateButtonStates();
    }

    private Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject(HudCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas newCanvas = canvasObject.GetComponent<Canvas>();
        ConfigureHudCanvas(newCanvas);

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return newCanvas;
    }

    private RectTransform CreatePanel(string objectName, Transform parent, Color color)
    {
        Image image = CreateImage(objectName, parent, color);
        return image.rectTransform;
    }

    private Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private Image EnsureImage(string objectName, Transform parent, Color color)
    {
        Transform existing = parent != null ? parent.Find(objectName) : null;
        Image image;
        if (existing == null)
        {
            image = CreateImage(objectName, parent, color);
        }
        else
        {
            EnsureComponent<CanvasRenderer>(existing.gameObject);
            image = EnsureComponent<Image>(existing.gameObject);
            image.color = color;
        }

        image.raycastTarget = false;
        return image;
    }

    private void SetChildImageColor(RectTransform root, string childPath, Color color, bool raycastTarget)
    {
        Transform child = root != null ? root.Find(childPath) : null;
        if (child == null) return;

        Image image = child.GetComponent<Image>();
        if (image == null) return;

        image.color = color;
        image.raycastTarget = raycastTarget;
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        if (target == null) return null;

        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private TextMeshProUGUI CreateText(string objectName, Transform parent, string text, float fontSize, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.font = RuntimeFontAssetProvider.Resolve();
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = alignment;
        label.color = new Color(0.93f, 0.94f, 0.95f, 1f);
        label.raycastTarget = false;
        label.enableWordWrapping = true;
        return label;
    }

    private Button CreateButton(string objectName, Transform parent, string labelText)
    {
        Image image = CreateImage(objectName, parent, new Color(0.22f, 0.3f, 0.38f, 1f));
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI label = CreateText("Label", image.transform, labelText, 18, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        return button;
    }

    private static void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void Anchor(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
    }

}
