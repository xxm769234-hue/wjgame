using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public partial class HomePageUI
{
    public static void RegisterOnboardingTarget(string targetKey, RectTransform target)
    {
        PlayerOnboardingManager.RegisterTarget(targetKey, target);
    }

    public static void UnregisterOnboardingTarget(string targetKey, RectTransform target)
    {
        PlayerOnboardingManager.UnregisterTarget(targetKey, target);
    }

    private void LoadServerOnboarding()
    {
        if (!HasAuthSession())
        {
            return;
        }

        PlayerOnboardingManager.EnsureInstance().RefreshCurrent();
    }
}

/// <summary>
/// Owns the shared onboarding input state for the imported UI assembly.
/// </summary>
public static class PlayerOnboardingInputLock
{
    private const string LockedKey = "weijun.onboarding.inputLocked";
    private const string TargetOnlyKey = "weijun.onboarding.targetOnly";
    private const string ActionKey = "weijun.onboarding.actionKey";

    /// <summary>Returns true while the current onboarding step blocks input.</summary>
    public static bool IsLocked => PlayerPrefs.GetInt(LockedKey, 0) == 1;

    /// <summary>Returns true when only the server-highlighted target should be used.</summary>
    public static bool IsTargetOnly => PlayerPrefs.GetInt(TargetOnlyKey, 0) == 1;

    /// <summary>Returns the server-provided action key for the current step.</summary>
    public static string CurrentActionKey => PlayerPrefs.GetString(ActionKey, string.Empty);

    internal static void SetState(bool locked, bool targetOnly, string actionKey, string targetKey = "", string sceneCode = "")
    {
        PlayerPrefs.SetInt(LockedKey, locked ? 1 : 0);
        PlayerPrefs.SetInt(TargetOnlyKey, targetOnly ? 1 : 0);
        PlayerPrefs.SetString(ActionKey, actionKey ?? string.Empty);
        PlayerPrefs.SetString("weijun.onboarding.targetKey", targetKey ?? string.Empty);
        PlayerPrefs.SetString("weijun.onboarding.sceneCode", sceneCode ?? string.Empty);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetStateAtStartup()
    {
        SetState(false, false, string.Empty);
    }
}

/// <summary>
/// Owns the server-authoritative onboarding state and its reusable overlay.
/// </summary>
public sealed class PlayerOnboardingManager : MonoBehaviour
{
    private const string LaunchSceneName = "Launch";
    private const int SupportedContractVersion = 2;
    private const float BusinessRefreshIntervalSeconds = 8f;
    private const float QueryRetryDelaySeconds = 5f;
    private const float TargetRefreshDelaySeconds = 0.75f;
    private const float TargetResolveIntervalSeconds = 0.25f;
    private const float TargetPadding = 12f;
    private const int MaxMutationRetries = 2;
    private static readonly Color ShadeColor = new Color(0.03f, 0.04f, 0.05f, 0.62f);
    private static readonly Dictionary<string, RectTransform> RegisteredTargets =
        new Dictionary<string, RectTransform>(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] GuideCodes =
    {
        "MAIN_FLOW", "PRODUCTION_FLOW", "NPC_SETTLEMENT_FLOW", "EQUIPMENT_FLOW", "SUPPLY_FLOW",
        "EXPANSION_FLOW", "TASK_FLOW", "SIGN_IN_FLOW", "AFK_FLOW", "SHOP_FLOW", "MAIL_FLOW", "SOCIAL_FLOW"
    };
    private static readonly HashSet<string> SupportedActions = new HashSet<string>(StringComparer.Ordinal)
    {
        "CONFIRM", "OPEN_HOME_MAP", "OPEN_LAND_PLACEMENT", "OPEN_BUILDING_PANEL", "FOCUS_BUILDING",
        "FOCUS_FREE_LAND", "PLACE_LAND", "START_BUILDING",
        "OPEN_GENDER_SELECTION", "OPEN_CARD_DRAW", "OPEN_NPC_UPGRADE", "OPEN_FORMATION", "OPEN_ADVENTURE",
        "OPEN_PRODUCTION", "OPEN_NPC_SETTLEMENT", "OPEN_EQUIPMENT", "OPEN_SUPPLY", "OPEN_EXPANSION",
        "OPEN_TASKS", "OPEN_SIGN_IN", "OPEN_AFK", "OPEN_SHOP", "OPEN_MAIL", "OPEN_FRIENDS"
    };
    private static readonly HashSet<string> SupportedTargets = new HashSet<string>(StringComparer.Ordinal)
    {
        "NAV_HOME_MAP", "PLAYER_BUILDING_FARM", "HOME_MAP_ENTRY", "FREE_GRASSLAND_TILE", "HOME_FREE_LAND_SLOT", "LAND_BUILD_BUTTON", "BUILDING_FARM_CARD", "BUILDING_FARM_INSTANCE",
        "GENDER_SELECTION", "CARD_DRAW_SINGLE", "NPC_UPGRADE", "ADVENTURE_FORMATION", "ADVENTURE_START",
        "PRODUCTION_COLLECT", "NPC_SETTLEMENT", "EQUIPMENT_EQUIP", "PRODUCTION_SUPPLY", "LAND_EXPANSION",
        "TASK_CLAIM", "SIGN_IN_CLAIM", "AFK_COLLECT", "SHOP_ENTRY", "MAIL_CLAIM", "FRIEND_ENTRY"
    };
    private static readonly HashSet<string> HomePageActions = new HashSet<string>(StringComparer.Ordinal)
    {
        "OPEN_GENDER_SELECTION", "OPEN_CARD_DRAW", "OPEN_NPC_UPGRADE", "OPEN_EQUIPMENT", "OPEN_TASKS",
        "OPEN_SIGN_IN", "OPEN_SHOP", "OPEN_MAIL", "OPEN_FRIENDS"
    };
    private readonly Dictionary<string, OnboardingCurrentData> guideSnapshots =
        new Dictionary<string, OnboardingCurrentData>(StringComparer.Ordinal);
    private readonly HashSet<string> requestedGuides = new HashSet<string>(StringComparer.Ordinal);

    [SerializeField] private Sprite panelBackground;
    [SerializeField] private Sprite actionButtonBackground;
    [SerializeField] private Sprite guideIcon;
    [SerializeField] private Sprite targetFrame;

    private static PlayerOnboardingManager instance;

    private OnboardingCurrentData currentSnapshot;
    private GameObject overlayObject;
    private Image overlayImage;
    private RectTransform panelRect;
    private readonly Image[] inputBlockers = new Image[4];
    private Image targetHighlight;
    private RectTransform resolvedTarget;
    private Button boundTargetButton;
    private TMP_Text titleText;
    private TMP_Text stepText;
    private TMP_Text contentText;
    private ScrollRect contentScroll;
    private string displayedContent;
    private TMP_Text targetText;
    private TMP_Text stateText;
    private Button acknowledgeButton;
    private TMP_Text acknowledgeButtonText;
    private Button skipButton;
    private TMP_Text skipButtonText;
    private bool queryRunning;
    private bool queryQueued;
    private bool mutationRunning;
    private bool hasPendingMutation;
    private bool messageMode;
    private bool pendingMutationIsSkip;
    private string pendingMutationKey = string.Empty;
    private string pendingMutationStepCode = string.Empty;
    private OnboardingMutationRequest pendingMutationRequest;
    private Coroutine businessPollingCoroutine;
    private Coroutine queryRetryCoroutine;
    private Coroutine presentationTrackingCoroutine;
    private Coroutine targetActivationRefreshCoroutine;
    private float nextTargetResolveTime;
    private string missingTargetWarningSignature = string.Empty;
    private TMP_Text targetArrow;
    private Vector2 authoredPanelPosition;
    private Vector2 authoredPanelSize;
    private bool compactPresentation;
    private Image panelSurface;
    private Sprite authoredPanelSprite;
    private Color authoredPanelColor;
    private Image.Type authoredPanelType;
    private RectTransform compactPanelFrame;
    private readonly Dictionary<RectTransform, PanelElementLayout> authoredElementLayouts = new Dictionary<RectTransform, PanelElementLayout>();
    private bool panelLayoutCached;
    private Rect cachedPanelTarget;
    private Rect cachedPanelBounds;
    private RectTransform cachedPanelTargetTransform;
    private string cachedPanelContent;
    private int cachedPanelButtons;
    private float nextPanelLayoutCheckTime;
    private Vector2 appliedCompactSize;
    private int appliedCompactButtons;
    private float measuredCompactWidth;
    private string measuredCompactContent;
    private float measuredCompactHeight;
    private int snapshotPlayerId;
    private string snapshotToken;
    private string pendingMutationPath;
    private string pendingMutationJson;
    private bool pendingRetryBlocked;
    private MethodInfo resolveGameplayTarget;
    private MethodInfo gameplayStepReady;
    private MethodInfo gameplayOpenPage;
    private MethodInfo gameplayPresentationSuspended;
    private bool gameplayBridgeResolved;
    private float nextPendingRetryTime;
    private bool returnToUpdateFlow;
    private string lastSnapshotDiagnostic;
    private string pendingMutationGuideCode;
    private bool refreshAllRequested;
    private bool legacyMainRoute;
    private float nextQueryTime;
    private int queryFailureCount;
    private bool presentationUnsupported;
    private bool presentationSuspended;
    private int pendingRetryAttempts;
    private bool retryPendingOnClick;
    private bool targetMissing;
    private string deferredStepIdentity;
    private string navigationStepIdentity;
    private float nextNavigationTime;
    private int navigationAttempts;
    private bool accessCheckPending;
    private AsyncOperation homeNavigation;
    private string homeNavigationIdentity;
    private bool recoveryMessageMode;
    private bool recoveryCanRetry;
    private bool recoveryDismissed;
    private string snoozedStepIdentity;

    /// <summary>Returns the persistent onboarding manager instance.</summary>
    public static PlayerOnboardingManager Instance => instance;

    public static void NotifyBusinessSucceeded()
    {
        if (GameSession.IsAuthenticated) EnsureInstance().RefreshCurrent();
    }

    public void SnoozeCurrentStep()
    {
        if (currentSnapshot == null) return;
        snoozedStepIdentity = StepIdentity(currentSnapshot);
        deferredStepIdentity = snoozedStepIdentity;
        HideOverlay();
    }

    public void ResumeCurrentStep()
    {
        FindObjectOfType<HomePageUI>()?.ResumeStarterSelection();
        snoozedStepIdentity = null;
        deferredStepIdentity = null;
        recoveryDismissed = false;
        navigationAttempts = 0;
        nextNavigationTime = 0f;
        RefreshCurrent();
    }

    public void RefreshGuide(string guideCode)
    {
        if (!IsKnownGuide(guideCode)) return;
        if (snapshotPlayerId != GameSession.PlayerId || snapshotToken != GameSession.Token)
        {
            RefreshCurrent();
            return;
        }
        requestedGuides.Add(guideCode);
        QueueRefresh();
    }

    /// <summary>Realtime messages are refresh hints, never authoritative step snapshots.</summary>
    public void HandleRealtimeHint(string json)
    {
        OnboardingRealtimeEnvelope envelope = ParseResponse<OnboardingRealtimeEnvelope>(json);
        OnboardingRealtimeData data = envelope == null ? null : envelope.data;
        if (!GameSession.IsAuthenticated || data == null || data.playerId != GameSession.PlayerId
            || envelope.playerId != GameSession.PlayerId) return;
        string guideCode = string.IsNullOrEmpty(data.guideCode) ? "MAIN_FLOW" : data.guideCode;
        if (!IsKnownGuide(guideCode)) return;
        if (guideSnapshots.TryGetValue(guideCode, out OnboardingCurrentData snapshot)
            && data.instanceId == snapshot.instanceId && data.guideVersion == snapshot.guideVersion
            && data.stateVersion <= snapshot.stateVersion) return;
        RefreshGuide(guideCode);
    }

    internal static void RegisterTarget(string targetKey, RectTransform target)
    {
        string normalizedKey = NormalizeKey(targetKey);
        if (string.IsNullOrEmpty(normalizedKey) || target == null)
        {
            return;
        }

        RegisteredTargets[normalizedKey] = target;
    }

    internal static void UnregisterTarget(string targetKey, RectTransform target)
    {
        string normalizedKey = NormalizeKey(targetKey);
        if (string.IsNullOrEmpty(normalizedKey)
            || !RegisteredTargets.TryGetValue(normalizedKey, out RectTransform registeredTarget)
            || registeredTarget != target)
        {
            return;
        }

        RegisteredTargets.Remove(normalizedKey);
    }

    /// <summary>Creates or returns the persistent onboarding manager.</summary>
    public static PlayerOnboardingManager EnsureInstance()
    {
        if (instance != null)
        {
            instance.gameObject.SetActive(true);
            instance.enabled = true;
            return instance;
        }

        PlayerOnboardingManager existing = FindObjectOfType<PlayerOnboardingManager>(true);
        if (existing != null)
        {
            instance = existing;
            existing.gameObject.SetActive(true);
            existing.enabled = true;
            return existing;
        }

        GameObject managerObject = new GameObject("PlayerOnboardingManager");
        return managerObject.AddComponent<PlayerOnboardingManager>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        PlayerOnboardingInputLock.SetState(false, false, string.Empty);
        SceneManager.sceneLoaded += HandleSceneLoaded;
        Transform sceneOverlay = transform.Find("PlayerOnboardingCanvas/Overlay");
        if (sceneOverlay != null) sceneOverlay.gameObject.SetActive(false);
        StartCoroutine(RefreshAfterStartup());
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (instance == this)
        {
            ResetTargetPresentation();
            instance = null;
            PlayerOnboardingInputLock.SetState(false, false, string.Empty);
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            deferredStepIdentity = null;
            recoveryDismissed = false;
            RefreshCurrent();
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused)
        {
            deferredStepIdentity = null;
            recoveryDismissed = false;
            RefreshCurrent();
        }
    }

    /// <summary>Refreshes the current server snapshot.</summary>
    public void RefreshCurrent()
    {
        if (accessCheckPending && SceneManager.GetActiveScene().name == LaunchSceneName) return;
        accessCheckPending = false;
        if (snapshotPlayerId != GameSession.PlayerId || snapshotToken != GameSession.Token)
        {
            StopAllCoroutines();
            queryRunning = mutationRunning = false;
            businessPollingCoroutine = queryRetryCoroutine = presentationTrackingCoroutine = targetActivationRefreshCoroutine = null;
            ClearSnapshot();
            snapshotPlayerId = GameSession.PlayerId;
            snapshotToken = GameSession.Token;
            if (GameSession.IsAuthenticated) RestorePendingMutation();
        }
        if (!GameSession.IsAuthenticated)
        {
            ClearSnapshot();
            return;
        }

        refreshAllRequested = true;
        QueueRefresh();
    }

    private void QueueRefresh()
    {
        if (!GameSession.IsAuthenticated) return;
        if (queryRunning)
        {
            queryQueued = true;
            return;
        }
        if (Time.unscaledTime < nextQueryTime)
        {
            ScheduleQueryRetry();
            return;
        }
        StartCoroutine(RequestCurrentRoutine());
    }

    private void Update()
    {
        RefreshButtonTextContrast();
        if (snapshotPlayerId != GameSession.PlayerId || snapshotToken != GameSession.Token)
            RefreshCurrent();
        if (currentSnapshot == null || messageMode || !IsSupportedStep(currentSnapshot.currentStep)) return;
        if (snoozedStepIdentity == StepIdentity(currentSnapshot)) return;
        bool suspended = IsGameplaySuspended(currentSnapshot.currentStep);
        if (suspended && !presentationSuspended)
        {
            presentationSuspended = true;
            HideOverlay();
            EnsureBusinessPollingState();
        }
        else if (!suspended && presentationSuspended)
        {
            presentationSuspended = false;
            RenderSnapshot();
        }
    }

    private IEnumerator RefreshAfterStartup()
    {
        yield return null;
        RefreshCurrent();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        deferredStepIdentity = null;
        recoveryDismissed = false;
        navigationAttempts = 0;
        nextNavigationTime = 0f;
        ResetTargetPresentation();
        RefreshCurrent();
    }

    private IEnumerator RequestCurrentRoutine()
    {
        queryRunning = true;
        do
        {
            queryQueued = false;
            if (!GameSession.IsAuthenticated)
            {
                ClearSnapshot();
                break;
            }

            int requestPlayerId = GameSession.PlayerId;
            string requestToken = GameSession.Token;
            bool requestList = refreshAllRequested || requestedGuides.Count == 0;
            string requestGuide = null;
            if (requestList)
            {
                refreshAllRequested = false;
                requestedGuides.Clear();
            }
            else
            {
                foreach (string guideCode in GuideCodes)
                    if (requestedGuides.Remove(guideCode)) { requestGuide = guideCode; break; }
            }
            string path = legacyMainRoute ? "/onboarding/current" : requestList ? "/onboarding/guides"
                : "/onboarding/guides/" + UnityWebRequest.EscapeURL(requestGuide) + "/current";

            UnityWebRequest request;
            string requestError;
            if (!ApiRequestUtility.TryCreateGet(path, true, out request, out requestError))
            {
                Debug.LogWarning("新手引导查询请求创建失败：" + requestError, this);
                ScheduleQueryRetry();
                break;
            }

            using (request)
            {
                yield return request.SendWebRequest();
                if (!IsSessionCurrent(requestPlayerId, requestToken))
                {
                    queryQueued = GameSession.IsAuthenticated;
                    continue;
                }

                if (ApiRequestUtility.IsUnauthorized(request))
                {
                    HandleUnauthorized();
                    break;
                }

                if (!legacyMainRoute && request.responseCode == 404)
                {
                    legacyMainRoute = true;
                    refreshAllRequested = queryQueued = true;
                    continue;
                }
                OnboardingCurrentEnvelope response = null;
                OnboardingListEnvelope list = null;
                if (requestList && !legacyMainRoute) list = ParseResponse<OnboardingListEnvelope>(request.downloadHandler.text);
                else response = ParseResponse<OnboardingCurrentEnvelope>(request.downloadHandler.text);
                bool valid = list != null && list.code == 200 && list.data != null
                    || response != null && response.code == 200 && response.data != null;
                if (request.result == UnityWebRequest.Result.Success && valid)
                {
                    queryFailureCount = 0;
                    nextQueryTime = 0f;
                    StopQueryRetry();
                    if (list != null)
                    {
                        foreach (OnboardingCurrentData snapshot in list.data) StoreSnapshot(snapshot, null);
                    }
                    else StoreSnapshot(response.data, legacyMainRoute ? "MAIN_FLOW" : requestGuide);
                    ReconcilePendingMutation();
                    SelectActiveSnapshot();
                    if (hasPendingMutation && !mutationRunning && !pendingRetryBlocked && pendingRetryAttempts < 3 && Time.unscaledTime >= nextPendingRetryTime
                        && guideSnapshots.TryGetValue(pendingMutationGuideCode, out OnboardingCurrentData pendingSnapshot)
                        && IsSupportedSnapshot(pendingSnapshot) && pendingSnapshot.available
                        && pendingSnapshot.status != "PAUSED")
                    {
                        StartCoroutine(MutateRoutine(pendingMutationIsSkip));
                    }
                    else if (hasPendingMutation && !mutationRunning && !pendingRetryBlocked)
                    {
                        if (pendingRetryAttempts >= 3) ShowPendingRecovery();
                        else ScheduleQueryRetry();
                    }
                }
                else
                {
                    OnboardingErrorEnvelope error = ParseResponse<OnboardingErrorEnvelope>(request.downloadHandler.text);
                    int code = error == null ? 0 : error.code;
                    HandleQueryFailure(code, request);
                }
            }
            queryQueued |= refreshAllRequested || requestedGuides.Count > 0;
        }
        while (queryQueued && GameSession.IsAuthenticated && Time.unscaledTime >= nextQueryTime);

        queryRunning = false;
    }

    private void ApplySnapshot(OnboardingCurrentData snapshot)
    {
        StoreSnapshot(snapshot, null);
        SelectActiveSnapshot();
    }

    private bool StoreSnapshot(OnboardingCurrentData snapshot, string expectedGuide)
    {
        if (snapshot == null) return false;
        if (string.IsNullOrEmpty(snapshot.guideCode)) snapshot.guideCode = expectedGuide ?? "MAIN_FLOW";
        if (!IsKnownGuide(snapshot.guideCode) || expectedGuide != null && expectedGuide != snapshot.guideCode) return false;
        if (guideSnapshots.TryGetValue(snapshot.guideCode, out OnboardingCurrentData previous)
            && (snapshot.guideVersion < previous.guideVersion
                || snapshot.guideVersion == previous.guideVersion && snapshot.stateVersion < previous.stateVersion
                || IsTerminalStatus(previous.status) && !IsTerminalStatus(snapshot.status)))
        {
            Debug.LogWarning("丢弃较旧的引导快照：" + snapshot.guideCode, this);
            return false;
        }
        guideSnapshots[snapshot.guideCode] = snapshot;
        return true;
    }

    private void SelectActiveSnapshot()
    {
        OnboardingCurrentData selected = null;
        foreach (string guideCode in GuideCodes)
        {
            if (!guideSnapshots.TryGetValue(guideCode, out OnboardingCurrentData candidate)) continue;
            if (candidate.available && !IsTerminalStatus(candidate.status)) { selected = candidate; break; }
            if (guideCode == "MAIN_FLOW" && !IsTerminalStatus(candidate.status)) break;
        }
        if (hasPendingMutation && guideSnapshots.TryGetValue(pendingMutationGuideCode, out OnboardingCurrentData pendingSnapshot)
            && !IsTerminalStatus(pendingSnapshot.status)) selected = pendingSnapshot;
        currentSnapshot = selected;
        if (snoozedStepIdentity != StepIdentity(selected)) snoozedStepIdentity = null;
        messageMode = false;
        presentationUnsupported = false;
        if (selected == null)
        {
            FindObjectOfType<HomePageUI>(true)?.HideStarterSelection();
            HideOverlay();
            EnsureBusinessPollingState();
            return;
        }
        if (!IsSupportedSnapshot(selected))
        {
            FindObjectOfType<HomePageUI>(true)?.HideStarterSelection();
            PlayerOnboardingInputLock.SetState(false, false, string.Empty);
            ShowMessage(
                "需要更新游戏",
                "当前版本暂不支持此引导，请更新游戏后继续。",
                true,
                false);
            returnToUpdateFlow = true;
            if (acknowledgeButtonText != null) acknowledgeButtonText.text = "检查更新";
            Debug.LogWarning(
                "新手引导版本不受支持：contractVersion=" + selected.contractVersion
                + ", minimumClientVersionCode=" + selected.minimumClientVersionCode,
                this);
            return;
        }

        string diagnostic = "guide=" + selected.guideCode + ", status=" + selected.status
            + ", step=" + (selected.currentStep == null ? "null" : selected.currentStep.stepCode);
        if (lastSnapshotDiagnostic != diagnostic)
        {
            lastSnapshotDiagnostic = diagnostic;
            Debug.Log("[Onboarding] " + diagnostic, this);
        }
        if (pendingRetryBlocked)
        {
            ShowRecoveryMessage("引导进度待确认", "当前进度暂时无法确认。可以收起提示继续游戏，稍后联系客服。", false);
            return;
        }
        RenderSnapshot();
    }

    private void RenderSnapshot()
    {
        if (messageMode)
        {
            return;
        }

        if (!CanDisplayStep(currentSnapshot))
        {
            FindObjectOfType<HomePageUI>(true)?.HideStarterSelection();
            HideOverlay();
            EnsureBusinessPollingState();
            if (currentSnapshot != null && string.Equals(currentSnapshot.status, "PAUSED", StringComparison.OrdinalIgnoreCase))
                ScheduleQueryRetry();
            return;
        }

        OnboardingStepData step = currentSnapshot.currentStep;
        if (deferredStepIdentity == StepIdentity(currentSnapshot) || snoozedStepIdentity == StepIdentity(currentSnapshot))
        {
            HideOverlay();
            EnsureBusinessPollingState();
            return;
        }
        EnsureOverlay();
        if (overlayObject == null)
        {
            return;
        }

        if (!IsSupportedStep(step))
        {
            presentationUnsupported = true;
            ShowMessage("引导内容暂不支持", "此步骤需要更新引导内容。可以暂时收起，或跳过当前教程。", true, currentSnapshot.allowSkip, false);
            acknowledgeButtonText.text = "暂时收起";
            Debug.LogWarning("引导展示键不受支持：" + currentSnapshot.guideCode + "/" + step.stepCode
                + ", action=" + step.actionKey + ", presentation=" + step.presentationType, this);
            return;
        }
        if (IsGameplaySuspended(step))
        {
            presentationSuspended = true;
            HideOverlay();
            EnsureBusinessPollingState();
            return;
        }

        if (step.actionKey == "OPEN_GENDER_SELECTION")
        {
            ResolveTarget(step);
            if (FindObjectOfType<HomePageUI>() != null)
            {
                HideOverlay();
                EnsureBusinessPollingState();
                return;
            }
        }
        else FindObjectOfType<HomePageUI>(true)?.HideStarterSelection();

        bool isClientAck = string.Equals(step.completionType, "CLIENT_ACK", StringComparison.OrdinalIgnoreCase);
        bool isBusinessEvent = string.Equals(step.completionType, "BUSINESS_EVENT", StringComparison.OrdinalIgnoreCase);
        bool blocksAllInput = IsExplanationStep(step) || string.Equals(step.inputMode, "BLOCK_ALL", StringComparison.OrdinalIgnoreCase);
        bool targetOnly = !IsExplanationStep(step) && string.Equals(step.inputMode, "ALLOW_TARGET_ONLY", StringComparison.OrdinalIgnoreCase);
        bool needsMap = NeedsMapNavigation(step);

        titleText.text = FrontendTextLocalization.NormalizePlayerMessage(step.title, "新手引导");
        stepText.text = step.sequence > 0 ? "第 " + step.sequence + " 步" : "新手引导";
        SetContentText(string.IsNullOrEmpty(step.content) ? "请按照当前提示完成操作。" : step.content);
        targetText.text = string.Empty;
        stateText.text = isBusinessEvent
            ? "完成目标操作后继续"
            : "准备好后，继续下一步";

        acknowledgeButton.gameObject.SetActive(isClientAck || needsMap);
        acknowledgeButton.interactable = !mutationRunning && (needsMap || (isClientAck && IsStepReady(step)));
        acknowledgeButtonText.text = mutationRunning ? "提交中..." : needsMap ? "前往经营地图" : GetAcknowledgeLabel(step);

        skipButton.gameObject.SetActive(currentSnapshot.allowSkip);
        skipButton.interactable = !mutationRunning;
        skipButtonText.text = currentSnapshot.guideCode == "MAIN_FLOW" ? "跳过引导" : "跳过此教程";

        ShowOverlayHierarchy();
        transform.SetAsLastSibling();
        ConfigureTargetPresentation(step, blocksAllInput, targetOnly);
        EnsureBusinessPollingState();
    }

    private void EnsureBusinessPollingState()
    {
        bool shouldPoll = CanDisplayStep(currentSnapshot)
            && string.Equals(currentSnapshot.currentStep.completionType, "BUSINESS_EVENT", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(currentSnapshot.status, "PAUSED", StringComparison.OrdinalIgnoreCase)
            && GameSession.IsAuthenticated;

        if (shouldPoll && businessPollingCoroutine == null)
        {
            businessPollingCoroutine = StartCoroutine(PollBusinessStep());
        }
        else if (!shouldPoll && businessPollingCoroutine != null)
        {
            StopCoroutine(businessPollingCoroutine);
            businessPollingCoroutine = null;
        }
    }

    private IEnumerator PollBusinessStep()
    {
        while (GameSession.IsAuthenticated
            && currentSnapshot != null
            && currentSnapshot.currentStep != null
            && currentSnapshot.available
            && !messageMode
            && currentSnapshot.status != "PAUSED"
            && string.Equals(currentSnapshot.currentStep.completionType, "BUSINESS_EVENT", StringComparison.OrdinalIgnoreCase))
        {
            yield return new WaitForSecondsRealtime(BusinessRefreshIntervalSeconds);
            if (currentSnapshot != null) RefreshGuide(currentSnapshot.guideCode);
        }

        businessPollingCoroutine = null;
    }

    private void EnsureOverlay()
    {
        if (overlayObject != null)
        {
            return;
        }

        Transform existingCanvas = transform.Find("PlayerOnboardingCanvas");
        if (existingCanvas != null)
        {
            if (!BindSceneOverlay(existingCanvas))
                Debug.LogWarning("新手引导场景面板缺少必要组件，请检查 Overlay/Panel。", this);
            return;
        }

        GameObject canvasObject = new GameObject(
            "PlayerOnboardingCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32760;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        overlayObject = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
        overlayObject.transform.SetParent(canvasObject.transform, false);
        overlayImage = overlayObject.GetComponent<Image>();
        overlayImage.raycastTarget = true;
        SetStretch(overlayObject.GetComponent<RectTransform>());

        for (int index = 0; index < inputBlockers.Length; index++)
        {
            GameObject blockerObject = new GameObject(
                "InputBlocker" + (index + 1),
                typeof(RectTransform),
                typeof(Image));
            blockerObject.transform.SetParent(overlayObject.transform, false);
            Image blocker = blockerObject.GetComponent<Image>();
            blocker.color = ShadeColor;
            blocker.raycastTarget = true;
            blockerObject.SetActive(false);
            inputBlockers[index] = blocker;
        }

        GameObject highlightObject = new GameObject(
            "TargetHighlight",
            typeof(RectTransform),
            typeof(Image));
        highlightObject.transform.SetParent(overlayObject.transform, false);
        targetHighlight = highlightObject.GetComponent<Image>();
        targetHighlight.color = Color.clear;
        targetHighlight.raycastTarget = false;
        highlightObject.SetActive(false);

        GameObject panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(overlayObject.transform, false);
        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.sprite = panelBackground;
        panelImage.color = panelBackground != null ? Color.white : new Color(0.97f, 0.92f, 0.81f, 1f);
        panelImage.raycastTarget = true;
        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(800f, 510f);
        authoredPanelPosition = panelRect.anchoredPosition;

        if (guideIcon != null)
        {
            GameObject iconObject = new GameObject("GuideIcon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(panelObject.transform, false);
            Image iconImage = iconObject.GetComponent<Image>();
            iconImage.sprite = guideIcon;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            SetRect(iconImage.rectTransform, new Vector2(96f, 400f), new Vector2(160f, 464f));
        }

        titleText = CreateText("Title", panelObject.transform, 30f, TextAlignmentOptions.Center);
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 20f;
        titleText.fontSizeMax = 30f;
        SetRect(titleText.rectTransform, new Vector2(176f, 404f), new Vector2(656f, 456f));
        stepText = CreateText("Step", panelObject.transform, 20f, TextAlignmentOptions.Center);
        stepText.color = new Color(0.20f, 0.39f, 0.43f, 1f);
        SetRect(stepText.rectTransform, new Vector2(110f, 344f), new Vector2(690f, 374f));
        contentText = CreateText("Content", panelObject.transform, 25f, TextAlignmentOptions.TopLeft);
        contentText.enableWordWrapping = true;
        contentText.enableAutoSizing = true;
        contentText.fontSizeMin = 20f;
        contentText.fontSizeMax = 25f;
        contentText.overflowMode = TextOverflowModes.Ellipsis;
        SetRect(contentText.rectTransform, new Vector2(110f, 206f), new Vector2(690f, 332f));
        targetText = CreateText("Target", panelObject.transform, 18f, TextAlignmentOptions.TopLeft);
        targetText.enableWordWrapping = true;
        SetRect(targetText.rectTransform, new Vector2(110f, 180f), new Vector2(690f, 202f));
        stateText = CreateText("State", panelObject.transform, 20f, TextAlignmentOptions.Center);
        stateText.color = new Color(0.42f, 0.33f, 0.24f, 1f);
        stateText.enableWordWrapping = true;
        SetRect(stateText.rectTransform, new Vector2(110f, 132f), new Vector2(690f, 176f));

        acknowledgeButton = CreateButton(
            "AcknowledgeButton",
            panelObject.transform,
            "继续",
            new Vector2(140f, 82f),
            out acknowledgeButtonText);
        acknowledgeButton.onClick.AddListener(AcknowledgeCurrentStep);
        skipButton = CreateButton(
            "SkipButton",
            panelObject.transform,
            "跳过引导",
            new Vector2(-140f, 82f),
            out skipButtonText);
        skipButton.onClick.AddListener(SkipCurrentOnboarding);
        skipButton.GetComponent<Image>().color = new Color(0.86f, 0.90f, 0.91f, 1f);
        targetArrow = CreateText("TargetArrow", overlayObject.transform, 32f, TextAlignmentOptions.Center);
        targetArrow.text = "^";
        targetArrow.color = new Color(0.20f, 0.39f, 0.43f, 1f);
        targetArrow.gameObject.SetActive(false);
        panelRect.SetAsLastSibling();
        ConfigureTextLayout();
    }

    private bool BindSceneOverlay(Transform canvas)
    {
        Transform overlay = canvas.Find("Overlay");
        Transform panel = overlay == null ? null : overlay.Find("Panel");
        if (panel == null) return false;
        panelRect = panel as RectTransform;
        overlayImage = overlay.GetComponent<Image>();
        titleText = panel.Find("Title")?.GetComponent<TMP_Text>();
        stepText = panel.Find("Step")?.GetComponent<TMP_Text>();
        contentText = (panel.Find("Content") ?? panel.Find("ContentViewport/Content"))?.GetComponent<TMP_Text>();
        targetText = panel.Find("Target")?.GetComponent<TMP_Text>();
        stateText = panel.Find("State")?.GetComponent<TMP_Text>();
        acknowledgeButton = panel.Find("AcknowledgeButton")?.GetComponent<Button>();
        skipButton = panel.Find("SkipButton")?.GetComponent<Button>();
        acknowledgeButtonText = acknowledgeButton?.GetComponentInChildren<TMP_Text>(true);
        skipButtonText = skipButton?.GetComponentInChildren<TMP_Text>(true);
        if (panelRect == null || overlayImage == null || titleText == null || stepText == null
            || contentText == null || targetText == null || stateText == null
            || acknowledgeButton == null || skipButton == null
            || acknowledgeButtonText == null || skipButtonText == null) return false;

        overlayObject = overlay.gameObject;
        authoredPanelPosition = panelRect.anchoredPosition;
        targetHighlight = (overlay.Find("TargetHighlight") ?? canvas.Find("TargetHighlight"))?.GetComponent<Image>();
        targetArrow = (overlay.Find("TargetArrow") ?? canvas.Find("TargetArrow"))?.GetComponent<TMP_Text>();
        if (targetHighlight != null) targetHighlight.raycastTarget = false;
        if (targetArrow != null) targetArrow.raycastTarget = false;
        foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(true))
        {
            text.richText = false;
            text.raycastTarget = false;
        }
        Image panelImage = panel.GetComponent<Image>();
        if (panelImage != null) panelImage.raycastTarget = true;
        for (int index = 0; index < inputBlockers.Length; index++)
        {
            string name = "InputBlocker" + (index + 1);
            Transform existing = overlay.Find(name);
            GameObject blocker = existing == null ? new GameObject(name, typeof(RectTransform), typeof(Image)) : existing.gameObject;
            if (existing == null) blocker.transform.SetParent(overlay, false);
            inputBlockers[index] = blocker.GetComponent<Image>();
            blocker.transform.SetAsFirstSibling();
            blocker.SetActive(false);
        }
        acknowledgeButton.onClick.AddListener(AcknowledgeCurrentStep);
        skipButton.onClick.AddListener(SkipCurrentOnboarding);
        ConfigureTextLayout();
        return true;
    }

    private void ConfigureTextLayout()
    {
        ConfigureShortText(titleText, 20f, 30f);
        ConfigureShortText(stepText, 16f, 20f);
        ConfigureShortText(targetText, 16f, 18f);
        ConfigureShortText(stateText, 17f, 20f);
        ConfigureShortText(acknowledgeButtonText, 18f, 23f);
        ConfigureShortText(skipButtonText, 18f, 23f);
        ConfigureButtonContrast(acknowledgeButton);
        ConfigureButtonContrast(skipButton);
        ApplySecondaryButtonStyle();
        RefreshButtonTextContrast();
        RectTransform contentRect = contentText.rectTransform;
        Transform existingViewport = panelRect.Find("ContentViewport");
        if (existingViewport == null)
        {
            GameObject viewportObject = new GameObject("ContentViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewportObject.layer = contentText.gameObject.layer;
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            viewport.SetParent(contentRect.parent, false);
            viewport.SetSiblingIndex(contentRect.GetSiblingIndex());
            viewport.anchorMin = contentRect.anchorMin;
            viewport.anchorMax = contentRect.anchorMax;
            viewport.pivot = contentRect.pivot;
            viewport.sizeDelta = contentRect.sizeDelta;
            viewport.anchoredPosition = contentRect.anchoredPosition;
            viewport.localRotation = contentRect.localRotation;
            viewport.localScale = contentRect.localScale;
            viewportObject.GetComponent<Image>().color = Color.clear;
            contentRect.SetParent(viewport, false);
            contentScroll = viewportObject.GetComponent<ScrollRect>();
            contentScroll.viewport = viewport;
            contentScroll.content = contentRect;
            contentScroll.horizontal = false;
            contentScroll.vertical = true;
            contentScroll.movementType = ScrollRect.MovementType.Clamped;
            contentScroll.scrollSensitivity = 28f;
        }
        else
        {
            contentScroll = existingViewport.GetComponent<ScrollRect>();
        }
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = Vector2.one;
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;
        contentText.enableAutoSizing = false;
        contentText.fontSize = 22f;
        contentText.enableWordWrapping = true;
        contentText.overflowMode = TextOverflowModes.Overflow;
        contentText.alignment = TextAlignmentOptions.TopLeft;
        contentText.margin = new Vector4(2f, 2f, 20f, 2f);
        ContentSizeFitter fitter = contentRect.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = contentRect.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        EnsureContentScrollbar();
        CapturePanelLayout();
        ConfigureTargetHighlight();
    }

    private sealed class PanelElementLayout
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 position;
        public Vector2 size;
        public bool active;
    }

    private void CapturePanelLayout()
    {
        if (panelSurface != null) return;
        panelSurface = panelRect.GetComponent<Image>();
        authoredPanelSize = panelRect.sizeDelta;
        authoredPanelSprite = panelSurface.sprite;
        authoredPanelColor = panelSurface.color;
        authoredPanelType = panelSurface.type;
        foreach (Transform child in panelRect)
        {
            if (!(child is RectTransform element)) continue;
            authoredElementLayouts[element] = new PanelElementLayout
            {
                anchorMin = element.anchorMin, anchorMax = element.anchorMax, pivot = element.pivot,
                position = element.anchoredPosition, size = element.sizeDelta, active = element.gameObject.activeSelf
            };
        }
        GameObject border = new GameObject("CompactPanelFrame", typeof(RectTransform));
        compactPanelFrame = border.GetComponent<RectTransform>();
        compactPanelFrame.SetParent(panelRect, false);
        SetStretch(compactPanelFrame);
        CreateFrameEdges(compactPanelFrame, new Color(.48f, .36f, .18f, .75f), 1.5f, false);
        border.SetActive(false);
    }

    private void ConfigureTargetHighlight()
    {
        if (targetHighlight == null)
        {
            GameObject highlight = new GameObject("TargetHighlight", typeof(RectTransform), typeof(Image));
            targetHighlight = highlight.GetComponent<Image>();
        }
        targetHighlight.transform.SetParent(overlayObject.transform, false);
        targetHighlight.transform.localScale = Vector3.one;
        targetHighlight.transform.localRotation = Quaternion.identity;
        targetHighlight.sprite = null;
        targetHighlight.color = Color.clear;
        targetHighlight.raycastTarget = false;
        foreach (BaseMeshEffect effect in targetHighlight.GetComponents<BaseMeshEffect>()) effect.enabled = false;
        if (targetHighlight.transform.childCount == 0)
            CreateFrameEdges(targetHighlight.rectTransform, new Color(.96f, .78f, .36f, .95f), 2.5f, true);
        foreach (Graphic graphic in targetHighlight.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        if (targetArrow != null)
        {
            targetArrow.transform.SetParent(overlayObject.transform, false);
            targetArrow.raycastTarget = false;
        }
        panelRect.SetAsLastSibling();
        targetHighlight.gameObject.SetActive(false);
    }

    private static void CreateFrameEdges(RectTransform parent, Color color, float thickness, bool cornersOnly)
    {
        for (int cornerIndex = 0; cornerIndex < 4; cornerIndex++)
        {
            bool right = cornerIndex % 2 == 1;
            bool top = cornerIndex >= 2;
            Vector2 corner = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
            for (int directionIndex = 0; directionIndex < 2; directionIndex++)
            {
                if (!cornersOnly && directionIndex == 1) continue;
                GameObject stroke = new GameObject("FrameStroke" + cornerIndex + "_" + directionIndex, typeof(RectTransform), typeof(Image));
                RectTransform strokeRect = stroke.GetComponent<RectTransform>();
                strokeRect.SetParent(parent, false);
                Image image = stroke.GetComponent<Image>();
                image.color = color;
                image.raycastTarget = false;
                if (cornersOnly)
                {
                    strokeRect.anchorMin = strokeRect.anchorMax = corner;
                    strokeRect.pivot = corner;
                    strokeRect.sizeDelta = directionIndex == 0 ? new Vector2(30f, thickness) : new Vector2(thickness, 30f);
                    strokeRect.anchoredPosition = Vector2.zero;
                }
                else
                {
                    bool horizontal = cornerIndex < 2;
                    strokeRect.anchorMin = horizontal ? new Vector2(0f, right ? 1f : 0f) : new Vector2(top && right ? 1f : 0f, 0f);
                    strokeRect.anchorMax = horizontal ? new Vector2(1f, right ? 1f : 0f) : new Vector2(top && right ? 1f : 0f, 1f);
                    strokeRect.pivot = new Vector2(.5f, .5f);
                    strokeRect.sizeDelta = horizontal ? new Vector2(0f, thickness) : new Vector2(thickness, 0f);
                    strokeRect.anchoredPosition = Vector2.zero;
                }
            }
        }
    }

    private void UseFullPanelLayout()
    {
        if (panelSurface == null || !compactPresentation) return;
        compactPresentation = false;
        panelLayoutCached = false;
        panelRect.sizeDelta = authoredPanelSize;
        panelSurface.sprite = authoredPanelSprite;
        panelSurface.color = authoredPanelColor;
        panelSurface.type = authoredPanelType;
        compactPanelFrame.gameObject.SetActive(false);
        foreach (KeyValuePair<RectTransform, PanelElementLayout> pair in authoredElementLayouts)
        {
            RectTransform element = pair.Key;
            if (element == null) continue;
            PanelElementLayout layout = pair.Value;
            element.anchorMin = layout.anchorMin;
            element.anchorMax = layout.anchorMax;
            element.pivot = layout.pivot;
            element.anchoredPosition = layout.position;
            element.sizeDelta = layout.size;
            if (element.name == "GuideIcon" || element == targetText.rectTransform) element.gameObject.SetActive(layout.active);
        }
        titleText.alignment = TextAlignmentOptions.Center;
        ConfigureShortText(titleText, 20f, 30f);
        ConfigureShortText(stepText, 16f, 20f);
        ConfigureShortText(stateText, 17f, 20f);
        ConfigureShortText(acknowledgeButtonText, 18f, 23f);
        ConfigureShortText(skipButtonText, 18f, 23f);
        acknowledgeButtonText.margin = skipButtonText.margin = new Vector4(24f, 8f, 24f, 8f);
        contentText.fontSize = 22f;
    }

    private void UseCompactPanelLayout(Vector2 size)
    {
        if (panelSurface == null) return;
        int buttonVisibility = (acknowledgeButton.gameObject.activeSelf ? 1 : 0) | (skipButton.gameObject.activeSelf ? 2 : 0);
        if (compactPresentation && appliedCompactSize == size && appliedCompactButtons == buttonVisibility) return;
        compactPresentation = true;
        appliedCompactSize = size;
        appliedCompactButtons = buttonVisibility;
        panelRect.sizeDelta = size;
        panelSurface.sprite = null;
        panelSurface.type = Image.Type.Simple;
        panelSurface.color = new Color(.98f, .95f, .86f, .99f);
        compactPanelFrame.gameObject.SetActive(true);
        Transform icon = panelRect.Find("GuideIcon");
        if (icon != null) icon.gameObject.SetActive(false);
        targetText.gameObject.SetActive(false);
        float width = size.x;
        float height = size.y;
        SetRect(titleText.rectTransform, new Vector2(22f, height - 50f), new Vector2(width - 110f, height - 18f));
        SetRect(stepText.rectTransform, new Vector2(width - 102f, height - 48f), new Vector2(width - 20f, height - 20f));
        SetRect(contentScroll.viewport, new Vector2(22f, 98f), new Vector2(width - 22f, height - 58f));
        SetRect(stateText.rectTransform, new Vector2(22f, 68f), new Vector2(width - 22f, 90f));
        bool bothButtons = acknowledgeButton.gameObject.activeSelf && skipButton.gameObject.activeSelf;
        float buttonWidth = bothButtons ? (width - 56f) * .5f : Mathf.Min(228f, width - 44f);
        float left = bothButtons ? 22f : (width - buttonWidth) * .5f;
        SetRect(skipButton.transform as RectTransform, new Vector2(left, 16f), new Vector2(left + buttonWidth, 60f));
        float acknowledgeLeft = bothButtons ? width - 22f - buttonWidth : left;
        SetRect(acknowledgeButton.transform as RectTransform, new Vector2(acknowledgeLeft, 16f), new Vector2(acknowledgeLeft + buttonWidth, 60f));
        titleText.alignment = TextAlignmentOptions.MidlineLeft;
        ConfigureShortText(titleText, 20f, 25f);
        ConfigureShortText(stepText, 16f, 18f);
        ConfigureShortText(stateText, 16f, 18f);
        ConfigureShortText(acknowledgeButtonText, 17f, 21f);
        ConfigureShortText(skipButtonText, 17f, 21f);
        acknowledgeButtonText.margin = skipButtonText.margin = new Vector4(14f, 4f, 14f, 4f);
        contentText.fontSize = 21f;
    }

    private void EnsureContentScrollbar()
    {
        if (contentScroll == null || contentScroll.verticalScrollbar != null) return;
        GameObject barObject = new GameObject("ContentScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        barObject.layer = contentText.gameObject.layer;
        RectTransform barRect = barObject.GetComponent<RectTransform>();
        barRect.SetParent(contentScroll.viewport, false);
        barRect.anchorMin = new Vector2(1f, 0f);
        barRect.anchorMax = Vector2.one;
        barRect.offsetMin = new Vector2(-10f, 3f);
        barRect.offsetMax = new Vector2(-2f, -3f);
        Image track = barObject.GetComponent<Image>();
        track.color = new Color(0.48f, 0.34f, 0.20f, 0.16f);

        GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObject.layer = barObject.layer;
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.SetParent(barRect, false);
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = Vector2.zero;
        handleRect.offsetMax = Vector2.zero;
        Image handle = handleObject.GetComponent<Image>();
        handle.color = new Color(0.48f, 0.34f, 0.20f, 0.62f);
        Scrollbar scrollbar = barObject.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handle;
        contentScroll.verticalScrollbar = scrollbar;
        contentScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    private static void ConfigureShortText(TMP_Text text, float minimum, float maximum)
    {
        if (text == null) return;
        text.enableWordWrapping = false;
        text.enableAutoSizing = true;
        text.fontSizeMin = minimum;
        text.fontSizeMax = maximum;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private static void ConfigureButtonContrast(Button button)
    {
        if (button == null) return;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.97f, 0.9f, 1f);
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.88f, 0.84f, 0.76f, 1f);
        colors.disabledColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
    }

    private void RefreshButtonTextContrast()
    {
        if (skipButtonText != null)
            skipButtonText.color = skipButton != null && skipButton.interactable
                ? new Color(73f / 255f, 55f / 255f, 34f / 255f, 1f) : new Color(0.35f, 0.29f, 0.23f, 1f);
        if (acknowledgeButtonText != null)
            acknowledgeButtonText.color = acknowledgeButton != null && acknowledgeButton.interactable
                ? new Color(1f, 0.96f, 0.85f, 1f) : new Color(0.92f, 0.88f, 0.8f, 1f);
    }

    private void SetContentText(string value)
    {
        value = FrontendTextLocalization.NormalizePlayerMessage(value, "请按照当前提示完成操作。");
        bool changed = !string.Equals(displayedContent, value, StringComparison.Ordinal);
        contentText.text = value;
        displayedContent = value;
        if (!changed || contentScroll == null) return;
        contentScroll.StopMovement();
        contentScroll.content.anchoredPosition = Vector2.zero;
    }

    private void ApplySecondaryButtonStyle()
    {
        if (skipButton == null || !(skipButton.targetGraphic is Image image)) return;
        Sprite sprite = Resources.Load<Sprite>("UI/ApprovedUi/Controls/SecondaryButton");
        if (sprite == null) return;
        image.sprite = sprite;
        image.overrideSprite = null;
        image.color = Color.white;
        image.type = Image.Type.Simple;
    }

    private TMP_Text CreateText(string objectName, Transform parent, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.font = RuntimeFontAssetProvider.Resolve();
        if (text.font == null)
        {
            Debug.LogWarning("新手引导文本没有找到 TMP Font Asset。", this);
        }
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(0.25f, 0.20f, 0.16f, 1f);
        text.raycastTarget = false;
        text.richText = false;
        return text;
    }

    private Button CreateButton(
        string objectName,
        Transform parent,
        string label,
        Vector2 position,
        out TMP_Text labelText)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = position;
        buttonRect.sizeDelta = new Vector2(252f, 76f);

        Image image = buttonObject.GetComponent<Image>();
        image.sprite = actionButtonBackground;
        image.color = actionButtonBackground != null ? Color.white : new Color(0.64f, 0.31f, 0.18f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        labelText = CreateText("Label", buttonObject.transform, 23f, TextAlignmentOptions.Center);
        labelText.text = label;
        labelText.color = new Color(1f, 0.96f, 0.85f, 1f);
        SetStretch(labelText.rectTransform);
        labelText.margin = new Vector4(24f, 8f, 24f, 8f);
        labelText.enableWordWrapping = false;
        return button;
    }

    private static string GetAcknowledgeLabel(OnboardingStepData step)
    {
        if (step.stepCode == "WELCOME") return "开始旅程";
        if (step.stepCode == "FINISH") return "完成引导";
        return "继续";
    }

    private void ConfigureTargetPresentation(OnboardingStepData step, bool blocksAllInput, bool targetOnly)
    {
        ResetTargetPresentation();
        nextTargetResolveTime = 0f;
        PlayerOnboardingInputLock.SetState(blocksAllInput, targetOnly, step.actionKey, step.uiTargetKey, step.sceneCode);
        PlayerPrefs.SetString("weijun.onboarding.stepCode", step.stepCode ?? string.Empty);
        OnboardingBusinessContextData context = step.businessContext;
        PlayerPrefs.SetString("weijun.onboarding.playerLandId", context == null ? "0" : context.playerLandId.ToString());
        PlayerPrefs.SetString("weijun.onboarding.playerBuildingId", context == null ? "0" : context.playerBuildingId.ToString());
        PlayerPrefs.SetString("weijun.onboarding.landConfigId", context == null ? "0" : context.landConfigId.ToString());
        PlayerPrefs.SetString("weijun.onboarding.buildingConfigId", context == null ? "0" : context.buildingConfigId.ToString());
        ApplyInputPolicy(step, blocksAllInput, targetOnly);
        if (presentationTrackingCoroutine == null)
        {
            presentationTrackingCoroutine = StartCoroutine(TrackTargetPresentation());
        }
    }

    private IEnumerator TrackTargetPresentation()
    {
        while (overlayObject != null
            && overlayObject.activeInHierarchy
            && !messageMode
            && currentSnapshot != null
            && currentSnapshot.currentStep != null)
        {
            OnboardingStepData step = currentSnapshot.currentStep;
            bool isClientAck = string.Equals(step.completionType, "CLIENT_ACK", StringComparison.OrdinalIgnoreCase);
            bool isBusinessEvent = string.Equals(step.completionType, "BUSINESS_EVENT", StringComparison.OrdinalIgnoreCase);
            bool blocksAllInput = IsExplanationStep(step) || string.Equals(step.inputMode, "BLOCK_ALL", StringComparison.OrdinalIgnoreCase);
            bool targetOnly = !IsExplanationStep(step) && string.Equals(step.inputMode, "ALLOW_TARGET_ONLY", StringComparison.OrdinalIgnoreCase);
            ApplyInputPolicy(step, blocksAllInput, targetOnly);
            if (acknowledgeButton != null)
            {
                bool needsMap = NeedsMapNavigation(step);
                acknowledgeButton.gameObject.SetActive(targetMissing || isClientAck || needsMap);
                acknowledgeButton.interactable = !mutationRunning && !pendingRetryBlocked
                    && (targetMissing || needsMap || (isClientAck && IsStepReady(step)));
                acknowledgeButtonText.text = mutationRunning ? "提交中..." : targetMissing ? "暂时收起" : needsMap ? "前往经营地图" : GetAcknowledgeLabel(step);
            }
            yield return null;
        }

        presentationTrackingCoroutine = null;
    }

    private void ApplyInputPolicy(OnboardingStepData step, bool blocksAllInput, bool targetOnly)
    {
        if (overlayImage == null || overlayObject == null || step == null)
        {
            return;
        }

        bool useCompact = !IsExplanationStep(step)
            && (targetOnly || IsTargetPresentation(step.presentationType) || !string.IsNullOrEmpty(step.actionKey));
        if (useCompact)
        {
            if (!compactPresentation) UseCompactPanelLayout(new Vector2(520f, 220f));
        }
        else UseFullPanelLayout();
        PlayerOnboardingInputLock.SetState(blocksAllInput, targetOnly, step.actionKey, step.uiTargetKey, step.sceneCode);
        targetMissing = false;
        if (IsExplanationStep(step))
        {
            SetBackdrop(true, ShadeColor);
            DisableInputBlockers();
            ResetPanelPosition();
            return;
        }
        bool needsTarget = targetOnly || IsTargetPresentation(step.presentationType) || !string.IsNullOrEmpty(step.actionKey)
            || step.stepCode == "OPEN_HOME_MAP" || step.stepCode == "FOCUS_FREE_LAND";
        if (needsTarget)
        {
            if (Time.unscaledTime >= nextTargetResolveTime)
            {
                nextTargetResolveTime = Time.unscaledTime + TargetResolveIntervalSeconds;
                resolvedTarget = ResolveTarget(step);
                BindTargetButton(step, resolvedTarget);
            }
        }
        targetMissing = needsTarget && resolvedTarget == null;

        if (step.stepCode == "OPEN_HOME_MAP" && IsStepReady(step))
        {
            UseFullPanelLayout();
            SetBackdrop(true, ShadeColor);
            DisableInputBlockers();
            ResetPanelPosition();
            return;
        }

        if (IsExplanationStep(step))
        {
            SetBackdrop(true, ShadeColor);
            DisableInputBlockers();
            ResetPanelPosition();
            return;
        }

        if (targetOnly)
        {
            if (ApplyTargetPresentation(step, true))
            {
                SetBackdrop(false, Color.clear);
                return;
            }

            SetBackdrop(false, new Color(ShadeColor.r, ShadeColor.g, ShadeColor.b, 0.18f));
            targetMissing = true;
            PlayerOnboardingInputLock.SetState(false, false, string.Empty);
            DisableInputBlockers();
            ApplyTargetPresentation(step, false);
            stateText.text = "正在寻找操作入口，也可先浏览页面或跳过此教程";
            WarnMissingTarget(step);
            return;
        }

        SetBackdrop(
            blocksAllInput,
            blocksAllInput
                ? ShadeColor
                : new Color(ShadeColor.r, ShadeColor.g, ShadeColor.b, 0.18f));
        DisableInputBlockers();
        ApplyTargetPresentation(step, false);
    }

    private bool ApplyTargetPresentation(OnboardingStepData step, bool restrictToTarget)
    {
        if (resolvedTarget == null
            || !resolvedTarget.gameObject.activeInHierarchy
            || !TryGetTargetRect(resolvedTarget, out Rect targetRect))
        {
            if (targetHighlight != null)
            {
                targetHighlight.gameObject.SetActive(false);
            }

            if (restrictToTarget)
            {
                DisableInputBlockers();
            }

            ResetPanelPosition();
            return false;
        }

        if (restrictToTarget)
        {
            SetInputBlockerRects(targetRect);
        }
        else
        {
            DisableInputBlockers();
        }

        bool showHighlight = restrictToTarget || IsTargetPresentation(step == null ? null : step.presentationType);
        if (targetHighlight != null)
        {
            targetHighlight.gameObject.SetActive(showHighlight);
            if (showHighlight)
            {
                SetRect(targetHighlight.rectTransform, targetRect);
            }
        }

        PositionPanelAwayFrom(targetRect);
        stateText.text = string.Equals(step.completionType, "BUSINESS_EVENT", StringComparison.OrdinalIgnoreCase)
            ? "完成目标操作后继续" : "准备好后，继续下一步";
        if (targetArrow != null)
        {
            targetArrow.gameObject.SetActive(string.Equals(step.presentationType, "ARROW", StringComparison.OrdinalIgnoreCase));
            string direction = NormalizeKey(step.presentationParams == null ? null : step.presentationParams.arrowDirection);
            Vector2 offset = direction == "LEFT" ? Vector2.right : direction == "RIGHT" ? Vector2.left
                : direction == "UP" ? Vector2.down : Vector2.up;
            Vector2 center = targetRect.center + Vector2.Scale(offset, targetRect.size * 0.5f + Vector2.one * 34f);
            SetRect(targetArrow.rectTransform, new Rect(center - Vector2.one * 24f, Vector2.one * 48f));
            targetArrow.text = "^";
            targetArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f,
                direction == "LEFT" ? 90f : direction == "RIGHT" ? -90f : direction == "UP" ? 0f : 180f);
        }
        return true;
    }

    private RectTransform ResolveTarget(OnboardingStepData step)
    {
        if (step == null || !IsSupportedStep(step))
        {
            return null;
        }

        HomePageUI home = FindObjectOfType<HomePageUI>(true);
        RectTransform rewardConfirmation = home != null ? home.ResolveOnboardingRewardConfirmation() : null;
        if (rewardConfirmation != null) return rewardConfirmation;
        bool activate = ShouldActivateNavigation(step);
        if (home == null && activate && HomePageActions.Contains(step.actionKey) && !IsBattlePresentationActive()
            && (homeNavigation == null || homeNavigation.isDone) && homeNavigationIdentity != StepIdentity(currentSnapshot)
            && SceneManager.GetActiveScene().name != "HomeScene" && Application.CanStreamedLevelBeLoaded("HomeScene"))
        {
            homeNavigationIdentity = StepIdentity(currentSnapshot);
            homeNavigation = SceneManager.LoadSceneAsync("HomeScene");
            return null;
        }
        if (home != null && !string.IsNullOrEmpty(step.actionKey))
        {
            RectTransform navigationTarget = home.ResolveOnboardingNavigationTarget(JsonUtility.ToJson(step), activate);
            if (navigationTarget != null)
            {
                navigationAttempts = 3;
                return navigationTarget;
            }
        }

        ResolveGameplayBridge();
        if (gameplayOpenPage != null && activate && !string.IsNullOrEmpty(step.actionKey))
        {
            try { gameplayOpenPage.Invoke(null, new object[] { step.actionKey, GameSession.PlayerId, GameSession.Token }); }
            catch (Exception exception) { Debug.LogWarning("引导页面暂不可用：" + exception.GetBaseException().Message, this); }
        }
        if (resolveGameplayTarget != null)
        {
            try
            {
                RectTransform gameplayTarget = resolveGameplayTarget.Invoke(null,
                    new object[] { JsonUtility.ToJson(step), GameSession.PlayerId, GameSession.Token }) as RectTransform;
                if (gameplayTarget != null)
                {
                    navigationAttempts = 3;
                    return gameplayTarget;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("引导地图目标暂不可用：" + exception.GetBaseException().Message, this);
            }
        }

        RectTransform registeredTarget = ResolveRegisteredTarget(step.uiTargetKey);
        if (registeredTarget == null)
        {
            registeredTarget = ResolveRegisteredTarget(step.actionKey);
        }

        if (registeredTarget != null)
        {
            navigationAttempts = 3;
            return registeredTarget;
        }

        RectTransform actionTarget = ResolveBuiltInActionTarget(step.actionKey);
        if (actionTarget != null)
        {
            return PromoteTargetToButton(actionTarget);
        }

        return null;
    }

    private static RectTransform ResolveRegisteredTarget(string targetKey)
    {
        string normalizedKey = NormalizeKey(targetKey);
        if (string.IsNullOrEmpty(normalizedKey)
            || !RegisteredTargets.TryGetValue(normalizedKey, out RectTransform target))
        {
            return null;
        }

        if (target != null && target.gameObject.activeInHierarchy)
        {
            return target;
        }

        RegisteredTargets.Remove(normalizedKey);
        return null;
    }

    private RectTransform ResolveBuiltInActionTarget(string actionKey)
    {
        string normalizedAction = NormalizeKey(actionKey);
        if (normalizedAction != "OPEN_HOME_MAP")
        {
            return null;
        }

        string expectedButtonName = normalizedAction == "OPEN_HOME_MAP"
            ? "ENTERMAPBUTTON"
            : normalizedAction;
        Button[] buttons = FindObjectsOfType<Button>(true);
        for (int index = 0; index < buttons.Length; index++)
        {
            Button button = buttons[index];
            if (button == null
                || !button.gameObject.activeInHierarchy
                || (overlayObject != null && button.transform.IsChildOf(overlayObject.transform)))
            {
                continue;
            }

            string buttonName = NormalizeKey(button.name);
            if (buttonName == expectedButtonName || buttonName == normalizedAction
                || (normalizedAction == "OPEN_HOME_MAP" && buttonName == "MANAGEMENTENTERBUTTON"))
            {
                return button.GetComponent<RectTransform>();
            }
        }

        return null;
    }

    private static RectTransform PromoteTargetToButton(RectTransform target)
    {
        if (target == null)
        {
            return null;
        }

        Button button = target.GetComponent<Button>();
        if (button == null)
        {
            button = target.GetComponentInParent<Button>();
        }

        return button == null ? target : button.GetComponent<RectTransform>();
    }

    private void BindTargetButton(OnboardingStepData step, RectTransform target)
    {
        UnbindTargetButton();
        if (target == null || step == null || string.IsNullOrEmpty(step.actionKey))
        {
            return;
        }

        boundTargetButton = target.GetComponent<Button>();
        if (boundTargetButton != null)
        {
            boundTargetButton.onClick.AddListener(HandleTargetActivated);
        }
    }

    private void UnbindTargetButton()
    {
        if (boundTargetButton == null)
        {
            return;
        }

        boundTargetButton.onClick.RemoveListener(HandleTargetActivated);
        boundTargetButton = null;
    }

    private void HandleTargetActivated()
    {
        if (targetActivationRefreshCoroutine != null)
        {
            StopCoroutine(targetActivationRefreshCoroutine);
        }

        targetActivationRefreshCoroutine = StartCoroutine(RefreshAfterTargetActivation());
    }

    private IEnumerator RefreshAfterTargetActivation()
    {
        yield return new WaitForSecondsRealtime(TargetRefreshDelaySeconds);
        targetActivationRefreshCoroutine = null;
        RefreshCurrent();
    }

    private bool TryGetTargetRect(RectTransform target, out Rect targetRect)
    {
        targetRect = default;
        RectTransform overlayRect = overlayObject == null
            ? null
            : overlayObject.GetComponent<RectTransform>();
        if (target == null
            || overlayRect == null
            || overlayRect.rect.width <= 0f
            || overlayRect.rect.height <= 0f)
        {
            return false;
        }

        Vector3[] worldCorners = new Vector3[4];
        target.GetWorldCorners(worldCorners);
        Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int index = 0; index < worldCorners.Length; index++)
        {
            Canvas targetCanvas = target.GetComponentInParent<Canvas>();
            Camera targetCamera = targetCanvas == null ? Camera.main
                : targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera;
            Canvas overlayCanvas = overlayRect.GetComponentInParent<Canvas>();
            Camera overlayCamera = overlayCanvas == null || overlayCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : overlayCanvas.worldCamera;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(targetCamera, worldCorners[index]);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRect, screenPoint, overlayCamera, out Vector2 localCorner))
                return false;
            minimum = Vector2.Min(minimum, localCorner);
            maximum = Vector2.Max(maximum, localCorner);
        }

        Rect overlayBounds = overlayRect.rect;
        minimum.x = Mathf.Clamp(minimum.x - TargetPadding, overlayBounds.xMin, overlayBounds.xMax);
        minimum.y = Mathf.Clamp(minimum.y - TargetPadding, overlayBounds.yMin, overlayBounds.yMax);
        maximum.x = Mathf.Clamp(maximum.x + TargetPadding, overlayBounds.xMin, overlayBounds.xMax);
        maximum.y = Mathf.Clamp(maximum.y + TargetPadding, overlayBounds.yMin, overlayBounds.yMax);
        if (maximum.x <= minimum.x || maximum.y <= minimum.y)
        {
            return false;
        }

        targetRect = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        return true;
    }

    private void SetInputBlockerRects(Rect targetRect)
    {
        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        Rect bounds = overlayRect.rect;
        SetInputBlocker(0, Rect.MinMaxRect(bounds.xMin, bounds.yMin, targetRect.xMin, bounds.yMax));
        SetInputBlocker(1, Rect.MinMaxRect(targetRect.xMax, bounds.yMin, bounds.xMax, bounds.yMax));
        SetInputBlocker(2, Rect.MinMaxRect(targetRect.xMin, bounds.yMin, targetRect.xMax, targetRect.yMin));
        SetInputBlocker(3, Rect.MinMaxRect(targetRect.xMin, targetRect.yMax, targetRect.xMax, bounds.yMax));
    }

    private void SetInputBlocker(int index, Rect rect)
    {
        Image blocker = inputBlockers[index];
        if (blocker == null)
        {
            return;
        }

        bool active = rect.width > 0f && rect.height > 0f;
        blocker.gameObject.SetActive(active);
        if (!active)
        {
            return;
        }

        blocker.color = ShadeColor;
        blocker.raycastTarget = true;
        SetRect(blocker.rectTransform, rect);
    }

    private static void SetRect(RectTransform rectTransform, Rect rect)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = rect.center;
        rectTransform.sizeDelta = rect.size;
    }

    private void DisableInputBlockers()
    {
        for (int index = 0; index < inputBlockers.Length; index++)
        {
            if (inputBlockers[index] != null)
            {
                inputBlockers[index].gameObject.SetActive(false);
            }
        }
    }

    private void SetBackdrop(bool blocksInput, Color color)
    {
        if (overlayImage == null)
        {
            return;
        }

        overlayImage.raycastTarget = blocksInput;
        overlayImage.color = color;
    }

    private void PositionPanelAwayFrom(Rect targetRect)
    {
        if (panelRect == null || overlayObject == null)
        {
            return;
        }

        Rect bounds = overlayObject.GetComponent<RectTransform>().rect;
        if (!compactPresentation)
        {
            panelRect.anchoredPosition = authoredPanelPosition;
            return;
        }
        int buttonVisibility = (acknowledgeButton.gameObject.activeSelf ? 1 : 0) | (skipButton.gameObject.activeSelf ? 2 : 0);
        if (panelLayoutCached && cachedPanelTarget == targetRect && cachedPanelBounds == bounds
            && cachedPanelTargetTransform == resolvedTarget && cachedPanelContent == contentText.text
            && cachedPanelButtons == buttonVisibility && Time.unscaledTime < nextPanelLayoutCheckTime) return;
        panelLayoutCached = true;
        cachedPanelTarget = targetRect;
        cachedPanelBounds = bounds;
        cachedPanelTargetTransform = resolvedTarget;
        cachedPanelContent = contentText.text;
        cachedPanelButtons = buttonVisibility;
        nextPanelLayoutCheckTime = Time.unscaledTime + .25f;
        Rect safe = Rect.MinMaxRect(bounds.xMin + 24f, bounds.yMin + 24f, bounds.xMax - 24f, bounds.yMax - 24f);
        Rect[] spaces =
        {
            Rect.MinMaxRect(safe.xMin, Mathf.Max(safe.yMin, targetRect.yMax + 18f), safe.xMax, safe.yMax),
            Rect.MinMaxRect(safe.xMin, safe.yMin, safe.xMax, Mathf.Min(safe.yMax, targetRect.yMin - 18f)),
            Rect.MinMaxRect(safe.xMin, safe.yMin, Mathf.Min(safe.xMax, targetRect.xMin - 18f), safe.yMax),
            Rect.MinMaxRect(Mathf.Max(safe.xMin, targetRect.xMax + 18f), safe.yMin, safe.xMax, safe.yMax)
        };
        Rect chosen = default;
        float bestScore = float.NegativeInfinity;
        for (int spaceIndex = 0; spaceIndex < spaces.Length; spaceIndex++)
        {
            Rect space = spaces[spaceIndex];
            if (space.width < 320f || space.height < 188f) continue;
            float width = Mathf.Min(540f, space.width);
            float preferredHeight = CompactPanelHeight(width);
            float height = Mathf.Min(preferredHeight, space.height);
            Vector2 center = new Vector2(
                Mathf.Clamp(targetRect.center.x, space.xMin + width * .5f, space.xMax - width * .5f),
                Mathf.Clamp(targetRect.center.y, space.yMin + height * .5f, space.yMax - height * .5f));
            float score = width / 540f + height / preferredHeight - Vector2.Distance(center, targetRect.center) * .0001f;
            if (score <= bestScore) continue;
            bestScore = score;
            chosen = new Rect(center - new Vector2(width, height) * .5f, new Vector2(width, height));
        }
        if (float.IsNegativeInfinity(bestScore))
        {
            Vector2 size = new Vector2(Mathf.Min(480f, safe.width), Mathf.Min(220f, safe.height));
            List<Rect> controls = new List<Rect>();
            List<Rect> labels = new List<Rect>();
            if (resolvedTarget != null)
            {
                foreach (Selectable control in resolvedTarget.GetComponentsInChildren<Selectable>())
                    if (control.isActiveAndEnabled && TryGetTargetRect(control.transform as RectTransform, out Rect controlRect)) controls.Add(controlRect);
                foreach (TMP_Text label in resolvedTarget.GetComponentsInChildren<TMP_Text>())
                    if (label.isActiveAndEnabled && !string.IsNullOrEmpty(label.text) && TryGetTargetRect(label.rectTransform, out Rect labelRect)) labels.Add(labelRect);
            }
            float leastObstruction = float.PositiveInfinity;
            for (int columnIndex = 0; columnIndex < 5; columnIndex++)
            {
                for (int rowIndex = 0; rowIndex < 5; rowIndex++)
                {
                    Vector2 minimum = new Vector2(Mathf.Lerp(safe.xMin, safe.xMax - size.x, columnIndex / 4f),
                        Mathf.Lerp(safe.yMax - size.y, safe.yMin, rowIndex / 4f));
                    Rect candidate = new Rect(minimum, size);
                    float obstruction = PanelOverlap(candidate, targetRect) * .01f;
                    foreach (Rect control in controls) obstruction += PanelOverlap(candidate, control) * 100f;
                    foreach (Rect label in labels) obstruction += PanelOverlap(candidate, label) * 2f;
                    if (obstruction >= leastObstruction) continue;
                    leastObstruction = obstruction;
                    chosen = candidate;
                }
            }
        }
        UseCompactPanelLayout(chosen.size);
        SetRect(panelRect, chosen);
        panelRect.SetAsLastSibling();
    }

    private float CompactPanelHeight(float width)
    {
        if (measuredCompactWidth == width && measuredCompactContent == contentText.text) return measuredCompactHeight;
        float bodyHeight = contentText.GetPreferredValues(contentText.text, Mathf.Max(200f, width - 66f), float.PositiveInfinity).y;
        measuredCompactWidth = width;
        measuredCompactContent = contentText.text;
        measuredCompactHeight = 156f + Mathf.Clamp(bodyHeight + 4f, 44f, 112f);
        return measuredCompactHeight;
    }

    private static float PanelOverlap(Rect panel, Rect target)
    {
        return Mathf.Max(0f, Mathf.Min(panel.xMax, target.xMax) - Mathf.Max(panel.xMin, target.xMin))
            * Mathf.Max(0f, Mathf.Min(panel.yMax, target.yMax) - Mathf.Max(panel.yMin, target.yMin));
    }

    private void ResetPanelPosition()
    {
        panelLayoutCached = false;
        if (panelRect != null)
        {
            if (compactPresentation && overlayObject != null)
            {
                Rect bounds = overlayObject.GetComponent<RectTransform>().rect;
                float width = Mathf.Min(520f, bounds.width - 48f);
                UseCompactPanelLayout(new Vector2(width, CompactPanelHeight(width)));
                panelRect.anchoredPosition = new Vector2(bounds.xMax - panelRect.rect.width * .5f - 24f,
                    bounds.yMax - panelRect.rect.height * .5f - 24f);
            }
            else panelRect.anchoredPosition = authoredPanelPosition;
        }

        if (targetHighlight != null)
        {
            targetHighlight.gameObject.SetActive(false);
        }
        if (targetArrow != null) targetArrow.gameObject.SetActive(false);
    }

    private void ResetTargetPresentation()
    {
        UnbindTargetButton();
        resolvedTarget = null;
        nextTargetResolveTime = 0f;
        missingTargetWarningSignature = string.Empty;
        DisableInputBlockers();
        ResetPanelPosition();
    }

    private void WarnMissingTarget(OnboardingStepData step)
    {
        string signature = StepIdentity(currentSnapshot) + "|" + (step.uiTargetKey ?? string.Empty);
        if (missingTargetWarningSignature == signature)
        {
            return;
        }

        missingTargetWarningSignature = signature;
        Debug.LogWarning(
            "引导 " + (currentSnapshot == null ? "" : currentSnapshot.guideCode + "/" + currentSnapshot.guideVersion)
            + " 步骤 " + step.stepCode
            + " 要求仅允许目标输入，但未找到 UI 目标 " + step.uiTargetKey
            + "；已降级普通提示，不自动确认步骤。",
            this);
    }

    private static bool IsTargetPresentation(string presentationType)
    {
        string normalizedType = NormalizeKey(presentationType);
        return normalizedType == "SPOTLIGHT" || normalizedType == "ARROW" || normalizedType == "TOOLTIP";
    }

    private static string NormalizeKey(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace('-', '_').Replace(' ', '_').ToUpperInvariant();
    }

    private void AcknowledgeCurrentStep()
    {
        if (retryPendingOnClick && !mutationRunning)
        {
            retryPendingOnClick = false;
            pendingRetryAttempts = 0;
            SavePendingMutation();
            messageMode = false;
            RefreshGuide(pendingMutationGuideCode);
            return;
        }
        if (recoveryMessageMode && !mutationRunning)
        {
            if (recoveryCanRetry)
            {
                recoveryMessageMode = false;
                messageMode = false;
                recoveryDismissed = false;
                RefreshCurrent();
            }
            else DismissRecoveryMessage();
            return;
        }
        if ((targetMissing || presentationUnsupported) && !mutationRunning && currentSnapshot != null)
        {
            deferredStepIdentity = StepIdentity(currentSnapshot);
            HideOverlay();
            return;
        }
        if (returnToUpdateFlow)
        {
            returnToUpdateFlow = false;
            ClearSnapshot();
            GameSession.Clear();
            SceneManager.LoadScene(LaunchSceneName);
            return;
        }
        if (mutationRunning || pendingRetryBlocked || messageMode) return;
        if (currentSnapshot != null && NeedsMapNavigation(currentSnapshot.currentStep))
        {
            RectTransform entry = ResolveBuiltInActionTarget("OPEN_HOME_MAP");
            Button entryButton = entry == null ? null : entry.GetComponent<Button>();
            if (entryButton != null && entryButton.interactable) entryButton.onClick.Invoke();
            else Debug.LogWarning("未找到可用的经营地图入口，等待场景加载。", this);
            return;
        }
        if (hasPendingMutation && !mutationRunning && !pendingRetryBlocked)
        {
            StartCoroutine(MutateRoutine(pendingMutationIsSkip));
            return;
        }
        if (currentSnapshot == null
            || currentSnapshot.currentStep == null
            || !string.Equals(currentSnapshot.currentStep.completionType, "CLIENT_ACK", StringComparison.OrdinalIgnoreCase)
            || mutationRunning || messageMode || !IsStepReady(currentSnapshot.currentStep))
        {
            return;
        }

        BeginMutation(false);
    }

    private void SkipCurrentOnboarding()
    {
        if (recoveryMessageMode)
        {
            DismissRecoveryMessage();
            return;
        }
        if (currentSnapshot == null || !currentSnapshot.allowSkip || mutationRunning || messageMode && !presentationUnsupported)
        {
            return;
        }

        BeginMutation(true);
    }

    private void BeginMutation(bool skip)
    {
        if (!CanDisplayStep(currentSnapshot) || !IsSupportedSnapshot(currentSnapshot) || !skip && !IsSupportedStep(currentSnapshot.currentStep))
        {
            return;
        }

        string stepCode = currentSnapshot.currentStep.stepCode ?? string.Empty;
        if (!skip && !string.Equals(currentSnapshot.currentStep.completionType, "CLIENT_ACK", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning("当前新手引导步骤必须由真实业务事件完成，不能调用 ACK：" + stepCode, this);
            return;
        }

        if (pendingRetryBlocked) return;
        if (hasPendingMutation && pendingMutationGuideCode != currentSnapshot.guideCode) return;
        if (!hasPendingMutation)
        {
            pendingMutationGuideCode = currentSnapshot.guideCode;
            pendingRetryAttempts = 0;
            pendingMutationIsSkip = skip;
            pendingMutationStepCode = stepCode;
            pendingMutationKey = Guid.NewGuid().ToString("D");
            pendingMutationRequest = new OnboardingMutationRequest
            {
                expectedGuideVersion = currentSnapshot.guideVersion,
                expectedStateVersion = currentSnapshot.stateVersion
            };
            hasPendingMutation = true;
            string route = legacyMainRoute && pendingMutationGuideCode == "MAIN_FLOW" ? "/onboarding"
                : "/onboarding/guides/" + UnityWebRequest.EscapeURL(pendingMutationGuideCode);
            pendingMutationPath = skip ? route + "/skip"
                : route + "/steps/" + UnityWebRequest.EscapeURL(stepCode) + "/ack";
            pendingMutationJson = JsonUtility.ToJson(pendingMutationRequest);
            SavePendingMutation();
        }

        StartCoroutine(MutateRoutine(pendingMutationIsSkip));
    }

    private static string PendingStorageKey(int playerId, string guideCode)
    {
        return "weijun.onboarding.pending." + playerId + "." + guideCode;
    }

    private void SavePendingMutation()
    {
        PlayerPrefs.SetString(PendingStorageKey(GameSession.PlayerId, pendingMutationGuideCode), JsonUtility.ToJson(new PendingIntent
        {
            guideCode = pendingMutationGuideCode,
            key = pendingMutationKey,
            path = pendingMutationPath,
            body = pendingMutationJson,
            stepCode = pendingMutationStepCode,
            skip = pendingMutationIsSkip,
            blocked = pendingRetryBlocked,
            attempts = pendingRetryAttempts
        }));
        PlayerPrefs.Save();
    }

    private void RestorePendingMutation()
    {
        if (hasPendingMutation) return;
        string legacyKey = "weijun.onboarding.pending." + GameSession.PlayerId;
        string mainKey = PendingStorageKey(GameSession.PlayerId, "MAIN_FLOW");
        if (PlayerPrefs.HasKey(legacyKey) && !PlayerPrefs.HasKey(mainKey))
        {
            PlayerPrefs.SetString(mainKey, PlayerPrefs.GetString(legacyKey));
            PlayerPrefs.DeleteKey(legacyKey);
            PlayerPrefs.Save();
        }
        foreach (string guideCode in GuideCodes)
        {
            string json = PlayerPrefs.GetString(PendingStorageKey(GameSession.PlayerId, guideCode), string.Empty);
            if (string.IsNullOrEmpty(json)) continue;
            RestorePendingIntent(json, guideCode);
            return;
        }
    }

    private void RestorePendingIntent(string json, string guideCode)
    {
        pendingMutationGuideCode = guideCode;
        PendingIntent intent = ParseResponse<PendingIntent>(json);
        if (intent == null)
        {
            pendingRetryBlocked = true;
            return;
        }
        OnboardingMutationRequest body = ParseResponse<OnboardingMutationRequest>(intent.body);
        string suffix = intent.skip ? "/skip" : "/steps/" + UnityWebRequest.EscapeURL(intent.stepCode ?? string.Empty) + "/ack";
        string expectedPath = "/onboarding/guides/" + guideCode + suffix;
        bool validPath = intent.path == expectedPath || guideCode == "MAIN_FLOW" && intent.path == "/onboarding" + suffix;
        if (!Guid.TryParseExact(intent.key, "D", out _) || body == null || !validPath
            || !string.IsNullOrEmpty(intent.guideCode) && intent.guideCode != guideCode
            || body.expectedGuideVersion <= 0 || body.expectedStateVersion <= 0)
        {
            pendingRetryBlocked = true;
            Debug.LogWarning("新手引导待重试记录损坏，已停止自动提交。", this);
            return;
        }
        pendingMutationKey = intent.key;
        pendingMutationPath = intent.path;
        pendingMutationJson = intent.body;
        pendingMutationStepCode = intent.stepCode;
        pendingMutationIsSkip = intent.skip;
        pendingMutationRequest = body;
        pendingRetryBlocked = intent.blocked;
        pendingRetryAttempts = Mathf.Clamp(intent.attempts, 0, 3);
        hasPendingMutation = true;
    }

    private void ClearPendingMutation(int playerId)
    {
        if (IsKnownGuide(pendingMutationGuideCode)) PlayerPrefs.DeleteKey(PendingStorageKey(playerId, pendingMutationGuideCode));
        PlayerPrefs.Save();
        hasPendingMutation = false;
        pendingRetryBlocked = false;
        retryPendingOnClick = false;
        pendingRetryAttempts = 0;
    }

    private void ReconcilePendingMutation()
    {
        if (!hasPendingMutation || mutationRunning || pendingRetryBlocked || pendingMutationRequest == null) return;
        if (!guideSnapshots.TryGetValue(pendingMutationGuideCode, out OnboardingCurrentData snapshot)) return;
        if (snapshot.guideVersion == pendingMutationRequest.expectedGuideVersion
            && snapshot.stateVersion > pendingMutationRequest.expectedStateVersion
            && (IsTerminalStatus(snapshot.status) || snapshot.currentStep != null && snapshot.currentStep.stepCode != pendingMutationStepCode))
        {
            ClearPendingMutation(GameSession.PlayerId);
            pendingMutationKey = string.Empty;
            pendingMutationRequest = null;
            RestorePendingMutation();
        }
    }

    private void ResolveGameplayBridge()
    {
        if (gameplayBridgeResolved) return;
        gameplayBridgeResolved = true;
        Type bridge = Type.GetType("PlayerOnboardingGameplayBridge, Assembly-CSharp");
        if (bridge == null)
        {
            Debug.LogWarning("新手引导经营地图桥接未加载。", this);
            return;
        }
        resolveGameplayTarget = bridge.GetMethod("ResolveTarget", BindingFlags.Public | BindingFlags.Static);
        gameplayStepReady = bridge.GetMethod("IsStepReady", BindingFlags.Public | BindingFlags.Static);
        gameplayOpenPage = bridge.GetMethod("OpenPage", BindingFlags.Public | BindingFlags.Static);
        gameplayPresentationSuspended = bridge.GetMethod("IsPresentationSuspended", BindingFlags.Public | BindingFlags.Static);
    }

    private bool IsGameplaySuspended(OnboardingStepData step)
    {
        if (step == null || !IsSupportedStep(step)) return false;
        HomePageUI home = FindObjectOfType<HomePageUI>();
        if (step.actionKey != "OPEN_GENDER_SELECTION" && home != null && home.HasActiveStarterSelection) return true;
        ResolveGameplayBridge();
        try
        {
            return gameplayPresentationSuspended != null && (bool)gameplayPresentationSuspended.Invoke(null,
                new object[] { JsonUtility.ToJson(step), GameSession.PlayerId, GameSession.Token });
        }
        catch (Exception) { return false; }
    }

    private bool IsBattlePresentationActive()
    {
        return IsGameplaySuspended(new OnboardingStepData
        {
            stepCode = "FIRST_BATTLE_REWARD", actionKey = "OPEN_ADVENTURE", uiTargetKey = "ADVENTURE_START",
            completionType = "BUSINESS_EVENT", presentationType = "SPOTLIGHT", inputMode = "ALLOW_TARGET_ONLY"
        });
    }

    private static bool NeedsMapNavigation(OnboardingStepData step)
    {
        if (step == null) return false;
        bool mapStep = step.stepCode == "OPEN_HOME_MAP" || step.stepCode == "FOCUS_FREE_LAND"
            || step.stepCode == "PLACE_FARM_LAND" || step.stepCode == "START_FARM_BUILD" || step.stepCode == "COMPLETE_FARM_BUILD";
        return mapStep && SceneManager.GetActiveScene().name != "Simulation management mode";
    }

    private bool IsStepReady(OnboardingStepData step)
    {
        if (step == null || pendingRetryBlocked || !IsSupportedStep(step)) return false;
        if (IsExplanationStep(step)) return true;
        HomePageUI home = FindObjectOfType<HomePageUI>(true);
        if (HomePageActions.Contains(step.actionKey ?? string.Empty))
            return home != null && home.IsOnboardingNavigationReady(JsonUtility.ToJson(step));
        if (home != null && home.IsOnboardingNavigationReady(JsonUtility.ToJson(step))) return true;
        ResolveGameplayBridge();
        try
        {
            return gameplayStepReady != null && (bool)gameplayStepReady.Invoke(null,
                new object[] { step.stepCode, GameSession.PlayerId, GameSession.Token });
        }
        catch (Exception exception)
        {
            Debug.LogWarning("新手引导目标状态检查失败：" + exception.GetBaseException().Message, this);
            return false;
        }
    }

    [Serializable]
    private sealed class PendingIntent
    {
        public string guideCode;
        public string key;
        public string path;
        public string body;
        public string stepCode;
        public bool skip;
        public bool blocked;
        public int attempts;
    }

    private IEnumerator MutateRoutine(bool skip)
    {
        mutationRunning = true;
        int requestPlayerId = GameSession.PlayerId;
        string requestToken = GameSession.Token;
        string requestGuide = pendingMutationGuideCode;
        int retryCount = 0;
        bool complete = false;

        while (!complete && IsSessionCurrent(requestPlayerId, requestToken))
        {
            string path = pendingMutationPath;
            string json = pendingMutationJson;
            UnityWebRequest request;
            string requestError;
            if (!ApiRequestUtility.TryCreateJsonRequest(
                path,
                UnityWebRequest.kHttpVerbPOST,
                json,
                true,
                out request,
                out requestError))
            {
                Debug.LogWarning("新手引导操作请求创建失败：" + requestError, this);
                break;
            }

            ApiRequestUtility.SetIdempotencyKey(request, pendingMutationKey);
            pendingRetryAttempts++;
            SavePendingMutation();
            using (request)
            {
                yield return request.SendWebRequest();
                if (!IsSessionCurrent(requestPlayerId, requestToken))
                {
                    hasPendingMutation = false;
                    pendingMutationKey = string.Empty;
                    pendingMutationStepCode = string.Empty;
                    pendingMutationRequest = null;
                    mutationRunning = false;
                    ClearSnapshot();
                    RefreshCurrent();
                    yield break;
                }

                if (ApiRequestUtility.IsUnauthorized(request))
                {
                    HandleUnauthorized();
                    complete = true;
                    continue;
                }

                OnboardingCurrentEnvelope response = ParseResponse<OnboardingCurrentEnvelope>(request.downloadHandler.text);
                if (request.result == UnityWebRequest.Result.Success
                    && response != null
                    && response.code == 200
                    && response.data != null)
                {
                    if (!string.IsNullOrEmpty(response.data.guideCode) && response.data.guideCode != requestGuide)
                    {
                        Debug.LogWarning("引导写响应教程不匹配，保留原请求并重新查询。", this);
                        nextPendingRetryTime = Time.unscaledTime + QueryRetryDelaySeconds;
                        RefreshGuide(requestGuide);
                        complete = true;
                        continue;
                    }
                    ClearPendingMutation(requestPlayerId);
                    ApplyMutationResponse(response.data, requestGuide);
                    hasPendingMutation = false;
                    pendingMutationKey = string.Empty;
                    pendingMutationStepCode = string.Empty;
                    pendingMutationRequest = null;
                    complete = true;
                    continue;
                }

                int code = response == null ? 0 : response.code;
                if (code == 8001)
                {
                    HandleMutationFailure(code, request);
                    complete = true;
                    continue;
                }
                bool uncertain = IsRetryable(request) || (request.result == UnityWebRequest.Result.Success
                    && (response == null || (response.code == 200 && response.data == null)));
                if (uncertain && retryCount < MaxMutationRetries && pendingRetryAttempts < 3)
                {
                    retryCount++;
                    yield return new WaitForSecondsRealtime(GetRetryDelay(request, Mathf.Pow(2f, retryCount - 1)));
                    continue;
                }

                if (uncertain)
                {
                    Debug.LogWarning("新手引导写请求结果不确定，重新查询当前快照。", this);
                    nextPendingRetryTime = Time.unscaledTime + QueryRetryDelaySeconds;
                    ScheduleQueryRetry();
                    RefreshCurrent();
                    complete = true;
                    continue;
                }

                HandleMutationFailure(code, request);
                complete = true;
            }
        }

        if (!IsSessionCurrent(requestPlayerId, requestToken))
        {
            hasPendingMutation = false;
            pendingMutationKey = string.Empty;
            pendingMutationStepCode = string.Empty;
            pendingMutationRequest = null;
            mutationRunning = false;
            ClearSnapshot();
            RefreshCurrent();
            yield break;
        }

        mutationRunning = false;
        if (!hasPendingMutation) RestorePendingMutation();
        if (hasPendingMutation && !pendingRetryBlocked && pendingRetryAttempts >= 3)
        {
            ShowPendingRecovery();
            yield break;
        }
        RenderSnapshot();
    }

    private void ApplyMutationResponse(OnboardingCurrentData response, string requestGuide)
    {
        if (response == null || !IsKnownGuide(requestGuide))
        {
            RefreshCurrent();
            return;
        }

        if (string.IsNullOrEmpty(response.guideCode) && requestGuide == "MAIN_FLOW") response.guideCode = "MAIN_FLOW";
        if (response.guideCode != requestGuide || !guideSnapshots.TryGetValue(requestGuide, out OnboardingCurrentData previous)
            || !CanApplyWriteResponse(previous.guideVersion, previous.stateVersion, response.guideVersion, response.stateVersion))
        {
            Debug.LogWarning("新手引导写响应版本过旧，重新查询当前快照。", this);
            RefreshCurrent();
            return;
        }

        ApplySnapshot(response);
        RefreshCurrent();
    }

    private void HandleQueryFailure(int code, UnityWebRequest request)
    {
        string message = ApiRequestUtility.GetErrorMessage(request, "新手引导查询失败");
        queryFailureCount++;
        nextQueryTime = Time.unscaledTime + GetRetryDelay(request,
            Mathf.Min(60f, QueryRetryDelaySeconds * Mathf.Pow(2f, Mathf.Min(queryFailureCount - 1, 4))));
        if (request != null && request.responseCode == 403 && code != 8006)
        {
            ReturnToAccessCheck();
            return;
        }
        if (code == 8001)
        {
            ShowRecoveryMessage("新手引导暂不可用", "引导配置暂时不可用，请稍后重新查询。可以先收起提示继续游戏。", true);
            ScheduleQueryRetry();
        }
        else if (code == 8004)
        {
            ShowRecoveryMessage("引导状态同步中", "正在重新查询当前进度，可以先收起提示。", true);
            ScheduleQueryRetry();
        }
        else
        {
            Debug.LogWarning("新手引导查询失败：" + (code > 0 ? "[" + code + "] " : string.Empty) + message, this);
            if (request != null && (request.result == UnityWebRequest.Result.ConnectionError
                || request.responseCode == 429
                || request.responseCode >= 500))
            {
                if (currentSnapshot != null)
                    ShowRecoveryMessage("引导连接恢复中", "暂时无法同步引导进度，可以重试查询或先收起提示。", true);
                ScheduleQueryRetry();
            }
        }
    }

    private void HandleMutationFailure(int code, UnityWebRequest request)
    {
        string message = ApiRequestUtility.GetErrorMessage(request, "新手引导操作失败");
        if (request != null && request.responseCode == 403 && code != 8006)
        {
            ReturnToAccessCheck();
            return;
        }
        if (code == 409
            || (code >= 4090 && code < 4100)
            || code == 8002
            || code == 8003
            || code == 8004
            || code == 8006)
        {
            if (code == 8006 && currentSnapshot != null) currentSnapshot.allowSkip = false;
            ClearPendingMutation(GameSession.PlayerId);
            hasPendingMutation = false;
            pendingMutationKey = string.Empty;
            pendingMutationStepCode = string.Empty;
            pendingMutationRequest = null;
            ShowRecoveryMessage("引导状态同步中", "正在重新查询当前进度，可以先收起提示。", true);
            RefreshCurrent();
            return;
        }

        if (code == 8001)
        {
            nextQueryTime = Time.unscaledTime + GetRetryDelay(request, QueryRetryDelaySeconds);
            ShowRecoveryMessage("新手引导暂不可用", "暂时无法读取引导内容。可以稍后重试或先收起提示。", true);
            ScheduleQueryRetry();
            return;
        }

        if (code == 8005)
        {
            pendingRetryBlocked = true;
            SavePendingMutation();
            ShowRecoveryMessage("引导进度待确认", "当前进度暂时无法确认。可以先收起提示继续游戏，并联系客服处理。", false);
            Debug.LogWarning("新手引导幂等键被重复用于不同请求：" + message, this);
            return;
        }

        Debug.LogWarning("新手引导操作失败：" + (code > 0 ? "[" + code + "] " : string.Empty) + message, this);
        ClearPendingMutation(GameSession.PlayerId);
        RefreshCurrent();
    }

    private void ShowMessage(string title, string message, bool showAcknowledge, bool showSkip, bool blockInput = true)
    {
        EnsureOverlay();
        if (overlayObject == null)
        {
            return;
        }

        messageMode = true;
        recoveryMessageMode = false;
        returnToUpdateFlow = false;
        targetMissing = false;
        retryPendingOnClick = false;
        PlayerPrefs.SetString("weijun.onboarding.stepCode", string.Empty);
        UseFullPanelLayout();
        ResetTargetPresentation();
        titleText.text = FrontendTextLocalization.NormalizePlayerMessage(title, "温馨提示");
        stepText.text = string.Empty;
        SetContentText(message);
        targetText.text = string.Empty;
        stateText.text = string.Empty;
        acknowledgeButton.gameObject.SetActive(showAcknowledge);
        acknowledgeButton.interactable = showAcknowledge && !mutationRunning;
        skipButton.gameObject.SetActive(showSkip);
        bool shouldBlockInput = blockInput;
        overlayImage.raycastTarget = shouldBlockInput;
        overlayImage.color = shouldBlockInput ? ShadeColor : Color.clear;
        PlayerOnboardingInputLock.SetState(shouldBlockInput, false, string.Empty);
        ShowOverlayHierarchy();
    }

    private void ShowOverlayHierarchy()
    {
        if (overlayObject == null || panelRect == null) return;
        Canvas canvas = overlayObject.GetComponentInParent<Canvas>(true);
        if (canvas == null)
        {
            Debug.LogWarning("[Onboarding] Overlay has no parent Canvas.", this);
            return;
        }

        Transform parent = overlayObject.transform.parent;
        while (parent != null && parent != transform)
        {
            parent.gameObject.SetActive(true);
            parent = parent.parent;
        }
        canvas.enabled = true;
        Vector3 scale = canvas.transform.localScale;
        if (Mathf.Approximately(scale.x, 0f) || Mathf.Approximately(scale.y, 0f))
            canvas.transform.localScale = Vector3.one;
        panelRect.gameObject.SetActive(true);
        overlayObject.SetActive(true);
    }

    private static bool CanDisplayStep(OnboardingCurrentData snapshot)
    {
        return snapshot != null && snapshot.available && snapshot.currentStep != null
            && string.Equals(snapshot.status, "IN_PROGRESS", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExplanationStep(OnboardingStepData step)
    {
        return step != null && string.Equals(step.completionType, "CLIENT_ACK", StringComparison.OrdinalIgnoreCase)
            && (step.stepCode == "WELCOME" || step.stepCode == "INTRO" || step.stepCode == "FINISH");
    }

    private void HideOverlay()
    {
        returnToUpdateFlow = false;
        PlayerPrefs.SetString("weijun.onboarding.stepCode", string.Empty);
        messageMode = false;
        ResetTargetPresentation();
        if (overlayObject != null)
        {
            overlayObject.SetActive(false);
        }

        PlayerOnboardingInputLock.SetState(false, false, string.Empty);
    }

    private void ClearSnapshot()
    {
        FindObjectOfType<HomePageUI>(true)?.HideStarterSelection();
        currentSnapshot = null;
        snoozedStepIdentity = null;
        guideSnapshots.Clear();
        requestedGuides.Clear();
        refreshAllRequested = false;
        legacyMainRoute = false;
        nextQueryTime = 0f;
        queryFailureCount = 0;
        presentationSuspended = false;
        deferredStepIdentity = null;
        navigationStepIdentity = null;
        homeNavigationIdentity = null;
        targetMissing = false;
        retryPendingOnClick = false;
        recoveryMessageMode = false;
        recoveryDismissed = false;
        pendingRetryBlocked = false;
        nextPendingRetryTime = 0f;
        queryQueued = false;
        StopQueryRetry();
        hasPendingMutation = false;
        pendingMutationKey = string.Empty;
        pendingMutationStepCode = string.Empty;
        pendingMutationRequest = null;
        pendingMutationGuideCode = null;
        HideOverlay();
    }

    private void ScheduleQueryRetry()
    {
        if (queryRetryCoroutine == null && isActiveAndEnabled)
        {
            queryRetryCoroutine = StartCoroutine(RetryQueryRoutine());
        }
    }

    private IEnumerator RetryQueryRoutine()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(QueryRetryDelaySeconds, nextQueryTime - Time.unscaledTime));
        queryRetryCoroutine = null;
        RefreshCurrent();
    }

    private void StopQueryRetry()
    {
        if (queryRetryCoroutine != null)
        {
            StopCoroutine(queryRetryCoroutine);
            queryRetryCoroutine = null;
        }
    }

    private void HandleUnauthorized()
    {
        ClearSnapshot();
        GameSession.Clear();
        if (SceneManager.GetActiveScene().name != LaunchSceneName)
        {
            SceneManager.LoadScene(LaunchSceneName);
        }
    }

    private static bool IsRetryable(UnityWebRequest request)
    {
        if (request == null)
        {
            return false;
        }

        return request.result == UnityWebRequest.Result.ConnectionError
            || request.result == UnityWebRequest.Result.DataProcessingError
            || request.responseCode == 429
            || request.responseCode >= 500;
    }

    private static bool IsSessionCurrent(int playerId, string token)
    {
        return GameSession.IsAuthenticated
            && GameSession.PlayerId == playerId
            && string.Equals(GameSession.Token, token, StringComparison.Ordinal);
    }

    private static bool IsTerminalStatus(string status)
    {
        return string.Equals(status, "COMPLETED", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "SKIPPED", StringComparison.OrdinalIgnoreCase);
    }

    private static bool CanApplyWriteResponse(long guideVersion, long stateVersion, long responseGuideVersion, long responseStateVersion)
    {
        return guideVersion == responseGuideVersion && responseStateVersion >= stateVersion;
    }

    private static bool IsKnownGuide(string guideCode)
    {
        return !string.IsNullOrEmpty(guideCode) && Array.IndexOf(GuideCodes, guideCode) >= 0;
    }

    private static bool IsSupportedSnapshot(OnboardingCurrentData snapshot)
    {
        return snapshot != null && snapshot.contractVersion >= 1 && snapshot.contractVersion <= SupportedContractVersion
            && snapshot.minimumClientVersionCode <= ApiRuntimeConfig.GetClientVersionCode();
    }

    private static bool IsSupportedStep(OnboardingStepData step)
    {
        if (step == null) return false;
        bool presentation = step.presentationType == "DIALOG" || step.presentationType == "SPOTLIGHT"
            || step.presentationType == "TOOLTIP" || step.presentationType == "ARROW";
        bool input = step.inputMode == "BLOCK_ALL" || step.inputMode == "ALLOW_TARGET_ONLY" || step.inputMode == "ALLOW_ALL";
        return presentation && input && (step.completionType == "CLIENT_ACK" || step.completionType == "BUSINESS_EVENT")
            && (string.IsNullOrEmpty(step.actionKey) || SupportedActions.Contains(step.actionKey))
            && (string.IsNullOrEmpty(step.uiTargetKey) || SupportedTargets.Contains(step.uiTargetKey));
    }

    private static string StepIdentity(OnboardingCurrentData snapshot)
    {
        return snapshot == null ? string.Empty : snapshot.guideCode + "/" + snapshot.guideVersion + "/"
            + snapshot.stateVersion + "/" + (snapshot.currentStep == null ? "" : snapshot.currentStep.stepCode);
    }

    private bool ShouldActivateNavigation(OnboardingStepData step)
    {
        if (IsExplanationStep(step) || string.IsNullOrEmpty(step.actionKey)) return false;
        string identity = StepIdentity(currentSnapshot);
        if (navigationStepIdentity != identity)
        {
            navigationStepIdentity = identity;
            navigationAttempts = 0;
            nextNavigationTime = 0f;
        }
        if (navigationAttempts >= 3 || Time.unscaledTime < nextNavigationTime) return false;
        navigationAttempts++;
        nextNavigationTime = Time.unscaledTime + 3f;
        return true;
    }

    private static float GetRetryDelay(UnityWebRequest request, float fallback)
    {
        string header = request == null ? null : request.GetResponseHeader("Retry-After");
        if (float.TryParse(header, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float seconds))
            return Mathf.Clamp(Mathf.Max(fallback, seconds), 1f, 300f);
        return fallback;
    }

    private void ShowPendingRecovery()
    {
        ShowRecoveryMessage("引导进度待确认", "网络未能确认刚才的操作，请重试确认。重试不会重复推进引导。", true);
        if (recoveryDismissed) return;
        retryPendingOnClick = true;
        acknowledgeButtonText.text = "重试确认";
    }

    private void ShowRecoveryMessage(string title, string message, bool canRetry)
    {
        if (recoveryDismissed)
        {
            HideOverlay();
            return;
        }
        ShowMessage(title, message, true, canRetry, false);
        recoveryMessageMode = true;
        recoveryCanRetry = canRetry;
        acknowledgeButtonText.text = canRetry ? "重新查询" : "暂时收起";
        skipButtonText.text = "暂时收起";
        acknowledgeButton.interactable = true;
        skipButton.interactable = true;
    }

    private void DismissRecoveryMessage()
    {
        recoveryDismissed = true;
        deferredStepIdentity = StepIdentity(currentSnapshot);
        recoveryMessageMode = false;
        retryPendingOnClick = false;
        HideOverlay();
    }

    private void ReturnToAccessCheck()
    {
        ClearSnapshot();
        accessCheckPending = true;
        snapshotPlayerId = GameSession.PlayerId;
        snapshotToken = GameSession.Token;
        if (SceneManager.GetActiveScene().name != LaunchSceneName) SceneManager.LoadScene(LaunchSceneName);
    }

    [Serializable]
    private sealed class OnboardingRealtimeEnvelope
    {
        public int playerId;
        public OnboardingRealtimeData data;
    }

    [Serializable]
    private sealed class OnboardingRealtimeData
    {
        public int playerId;
        public string guideCode;
        public long instanceId;
        public long guideVersion;
        public long stateVersion;
    }

    private static string BuildTargetText(OnboardingStepData step, bool targetOnly)
    {
        if (step == null || step.businessContext == null)
        {
            return string.Empty;
        }

        OnboardingBusinessContextData context = step.businessContext;
        string target = targetOnly ? "操作目标：" : "服务器定位：";
        if (!string.IsNullOrEmpty(context.eventType)) target += context.eventType;
        if (context.landConfigId > 0) target += "  地块配置 " + context.landConfigId;
        if (context.buildingConfigId > 0) target += "  建筑配置 " + context.buildingConfigId;
        if (context.playerLandId > 0) target += "  玩家地块 " + context.playerLandId;
        if (context.playerBuildingId > 0) target += "  玩家建筑 " + context.playerBuildingId;
        return target;
    }

    private static void SetStretch(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = position;
        rect.sizeDelta = size - position;
    }

    private static T ParseResponse<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("新手引导响应解析失败：" + exception.Message);
            return null;
        }
    }

    [Serializable]
    private sealed class OnboardingCurrentEnvelope
    {
        public int code;
        public string message;
        public OnboardingCurrentData data;
    }

    [Serializable]
    private sealed class OnboardingListEnvelope
    {
        public int code;
        public string message;
        public OnboardingCurrentData[] data;
    }

    [Serializable]
    private sealed class OnboardingErrorEnvelope
    {
        public int code;
        public string message;
    }

    [Serializable]
    private sealed class OnboardingCurrentData
    {
        public bool available;
        public int contractVersion;
        public long minimumClientVersionCode;
        public string guideCode;
        public long guideVersion;
        public long instanceId;
        public long stateVersion;
        public string status;
        public bool allowSkip;
        public OnboardingStepData currentStep;
        public string serverTime;
    }

    [Serializable]
    private sealed class OnboardingStepData
    {
        public string stepCode;
        public int sequence;
        public string sceneCode;
        public string presentationType;
        public string uiTargetKey;
        public string inputMode;
        public string actionKey;
        public string completionType;
        public string title;
        public string content;
        public OnboardingPresentationParamsData presentationParams;
        public OnboardingBusinessContextData businessContext;
    }

    [Serializable]
    private sealed class OnboardingPresentationParamsData
    {
        public string highlightColor;
        public string arrowDirection;
        public string animation;
        public string optionCode;
    }

    [Serializable]
    private sealed class OnboardingBusinessContextData
    {
        public string eventType;
        public long landConfigId;
        public long buildingConfigId;
        public long playerLandId;
        public long playerBuildingId;
    }

    [Serializable]
    private sealed class OnboardingMutationRequest
    {
        public long expectedGuideVersion;
        public long expectedStateVersion;
    }
}
