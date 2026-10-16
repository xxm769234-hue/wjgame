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
    private const int SupportedContractVersion = 1;
    private const float BusinessRefreshIntervalSeconds = 2f;
    private const float QueryRetryDelaySeconds = 5f;
    private const float TargetRefreshDelaySeconds = 0.75f;
    private const float TargetResolveIntervalSeconds = 0.25f;
    private const float TargetPadding = 12f;
    private const int MaxMutationRetries = 2;
    private static readonly Color ShadeColor = new Color(0.03f, 0.04f, 0.05f, 0.62f);
    private static readonly Dictionary<string, RectTransform> RegisteredTargets =
        new Dictionary<string, RectTransform>(StringComparer.OrdinalIgnoreCase);

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
    private int snapshotPlayerId;
    private string snapshotToken;
    private string pendingMutationPath;
    private string pendingMutationJson;
    private bool pendingRetryBlocked;
    private MethodInfo resolveGameplayTarget;
    private MethodInfo gameplayStepReady;
    private bool gameplayBridgeResolved;
    private float nextPendingRetryTime;
    private bool returnToUpdateFlow;
    private string lastSnapshotDiagnostic;

    /// <summary>Returns the persistent onboarding manager instance.</summary>
    public static PlayerOnboardingManager Instance => instance;

    /// <summary>Realtime messages are refresh hints, never authoritative step snapshots.</summary>
    public void HandleRealtimeHint(string json)
    {
        OnboardingRealtimeEnvelope envelope = ParseResponse<OnboardingRealtimeEnvelope>(json);
        OnboardingRealtimeData data = envelope == null ? null : envelope.data;
        if (!GameSession.IsAuthenticated || data == null || data.playerId != GameSession.PlayerId
            || envelope.playerId != GameSession.PlayerId) return;
        if (currentSnapshot != null && snapshotPlayerId == GameSession.PlayerId
            && data.instanceId == currentSnapshot.instanceId && data.guideVersion == currentSnapshot.guideVersion
            && data.stateVersion <= currentSnapshot.stateVersion) return;
        RefreshCurrent();
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
            RefreshCurrent();
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused)
        {
            RefreshCurrent();
        }
    }

    /// <summary>Refreshes the current server snapshot.</summary>
    public void RefreshCurrent()
    {
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

        if (queryRunning)
        {
            queryQueued = true;
            return;
        }

        StartCoroutine(RequestCurrentRoutine());
    }

    private void Update()
    {
        if (snapshotPlayerId != GameSession.PlayerId || snapshotToken != GameSession.Token)
            RefreshCurrent();
    }

    private IEnumerator RefreshAfterStartup()
    {
        yield return null;
        RefreshCurrent();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
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

            UnityWebRequest request;
            string requestError;
            if (!ApiRequestUtility.TryCreateGet("/onboarding/current", true, out request, out requestError))
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

                OnboardingCurrentEnvelope response = ParseResponse<OnboardingCurrentEnvelope>(request.downloadHandler.text);
                if (request.result == UnityWebRequest.Result.Success
                    && response != null
                    && response.code == 200
                    && response.data != null)
                {
                    StopQueryRetry();
                    ApplySnapshot(response.data);
                    if (hasPendingMutation && !mutationRunning && !pendingRetryBlocked && Time.unscaledTime >= nextPendingRetryTime
                        && response.data.available && response.data.contractVersion <= SupportedContractVersion
                        && response.data.minimumClientVersionCode <= ApiRuntimeConfig.GetClientVersionCode()
                        && response.data.status != "PAUSED")
                    {
                        StartCoroutine(MutateRoutine(pendingMutationIsSkip));
                    }
                    else if (hasPendingMutation && !mutationRunning && !pendingRetryBlocked)
                    {
                        ScheduleQueryRetry();
                    }
                }
                else
                {
                    int code = response == null ? 0 : response.code;
                    HandleQueryFailure(code, request);
                }
            }
        }
        while (queryQueued && GameSession.IsAuthenticated);

        queryRunning = false;
    }

    private void ApplySnapshot(OnboardingCurrentData snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        messageMode = false;

        if (snapshot.contractVersion > SupportedContractVersion
            || snapshot.minimumClientVersionCode > ApiRuntimeConfig.GetClientVersionCode())
        {
            currentSnapshot = null;
            PlayerOnboardingInputLock.SetState(false, false, string.Empty);
            ShowMessage(
                "新手引导需要更新客户端",
                "当前客户端不支持服务器下发的新手引导版本，请更新后再继续游戏。",
                true,
                false);
            returnToUpdateFlow = true;
            if (acknowledgeButtonText != null) acknowledgeButtonText.text = "检查客户端更新";
            Debug.LogWarning(
                "新手引导版本不受支持：contractVersion=" + snapshot.contractVersion
                + ", minimumClientVersionCode=" + snapshot.minimumClientVersionCode,
                this);
            return;
        }

        if (currentSnapshot != null
            && string.Equals(snapshot.guideCode, currentSnapshot.guideCode, StringComparison.Ordinal)
            && (snapshot.guideVersion < currentSnapshot.guideVersion
                || (snapshot.guideVersion == currentSnapshot.guideVersion
                    && snapshot.stateVersion < currentSnapshot.stateVersion)))
        {
            Debug.LogWarning(
                "丢弃较旧的新手引导快照：localStateVersion=" + currentSnapshot.stateVersion
                + ", responseStateVersion=" + snapshot.stateVersion,
                this);
            return;
        }

        currentSnapshot = snapshot;
        string diagnostic = "available=" + snapshot.available + ", status=" + snapshot.status
            + ", step=" + (snapshot.currentStep == null ? "null" : snapshot.currentStep.stepCode);
        if (lastSnapshotDiagnostic != diagnostic)
        {
            lastSnapshotDiagnostic = diagnostic;
            Debug.Log("[Onboarding] " + diagnostic, this);
        }
        if (pendingRetryBlocked)
        {
            ShowMessage("引导请求需检查", "待处理请求已保留，暂时停止自动操作，请联系客服。", false, false);
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
            HideOverlay();
            EnsureBusinessPollingState();
            if (currentSnapshot != null && string.Equals(currentSnapshot.status, "PAUSED", StringComparison.OrdinalIgnoreCase))
                ScheduleQueryRetry();
            return;
        }

        OnboardingStepData step = currentSnapshot.currentStep;
        EnsureOverlay();
        if (overlayObject == null)
        {
            return;
        }

        bool isClientAck = string.Equals(step.completionType, "CLIENT_ACK", StringComparison.OrdinalIgnoreCase);
        bool isBusinessEvent = string.Equals(step.completionType, "BUSINESS_EVENT", StringComparison.OrdinalIgnoreCase);
        bool blocksAllInput = IsExplanationStep(step) || string.Equals(step.inputMode, "BLOCK_ALL", StringComparison.OrdinalIgnoreCase);
        bool targetOnly = !IsExplanationStep(step) && string.Equals(step.inputMode, "ALLOW_TARGET_ONLY", StringComparison.OrdinalIgnoreCase);
        bool needsMap = NeedsMapNavigation(step);

        titleText.text = string.IsNullOrEmpty(step.title) ? "新手引导" : step.title;
        stepText.text = step.sequence > 0 ? "第 " + step.sequence + " 步" : "新手引导";
        contentText.text = string.IsNullOrEmpty(step.content) ? "请按照当前提示完成操作。" : step.content;
        targetText.text = string.Empty;
        stateText.text = isBusinessEvent
            ? "完成目标操作后继续"
            : "准备好后，继续下一步";

        acknowledgeButton.gameObject.SetActive(isClientAck || needsMap);
        acknowledgeButton.interactable = !mutationRunning && (needsMap || (isClientAck && IsStepReady(step)));
        acknowledgeButtonText.text = mutationRunning ? "提交中..." : needsMap ? "前往经营地图" : GetAcknowledgeLabel(step);

        skipButton.gameObject.SetActive(currentSnapshot.allowSkip);
        skipButton.interactable = !mutationRunning;
        skipButtonText.text = "跳过引导";

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
            RefreshCurrent();
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
            typeof(Image),
            typeof(Outline));
        highlightObject.transform.SetParent(overlayObject.transform, false);
        targetHighlight = highlightObject.GetComponent<Image>();
        targetHighlight.sprite = targetFrame;
        targetHighlight.color = targetFrame != null ? Color.white : new Color(1f, 0.77f, 0.18f, 0.10f);
        targetHighlight.raycastTarget = false;
        Outline highlightOutline = highlightObject.GetComponent<Outline>();
        highlightOutline.enabled = targetFrame == null;
        highlightOutline.effectColor = new Color(1f, 0.78f, 0.20f, 0.95f);
        highlightOutline.effectDistance = new Vector2(4f, -4f);
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
        contentText = panel.Find("Content")?.GetComponent<TMP_Text>();
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
        targetHighlight = canvas.Find("TargetHighlight")?.GetComponent<Image>();
        targetArrow = canvas.Find("TargetArrow")?.GetComponent<TMP_Text>();
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
        return true;
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
                acknowledgeButton.gameObject.SetActive(isClientAck || needsMap);
                acknowledgeButton.interactable = !mutationRunning && !pendingRetryBlocked
                    && (needsMap || (isClientAck && IsStepReady(step)));
                acknowledgeButtonText.text = mutationRunning ? "提交中..." : needsMap ? "前往经营地图" : GetAcknowledgeLabel(step);
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

        bool needsTarget = targetOnly || IsTargetPresentation(step.presentationType)
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

        if (step.stepCode == "OPEN_HOME_MAP" && IsStepReady(step))
        {
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

            SetBackdrop(true, ShadeColor);
            DisableInputBlockers();
            ApplyTargetPresentation(step, false);
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
        if (step == null)
        {
            return null;
        }

        ResolveGameplayBridge();
        if (resolveGameplayTarget != null)
        {
            try
            {
                RectTransform gameplayTarget = resolveGameplayTarget.Invoke(null,
                    new object[] { JsonUtility.ToJson(step), GameSession.PlayerId, GameSession.Token }) as RectTransform;
                if (gameplayTarget != null) return gameplayTarget;
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
            return PromoteTargetToButton(registeredTarget);
        }

        RectTransform actionTarget = ResolveBuiltInActionTarget(step.actionKey);
        if (actionTarget != null)
        {
            return PromoteTargetToButton(actionTarget);
        }

        string normalizedTargetKey = NormalizeKey(step.uiTargetKey);
        if (string.IsNullOrEmpty(normalizedTargetKey))
        {
            return null;
        }

        RectTransform[] candidates = FindObjectsOfType<RectTransform>(true);
        for (int index = 0; index < candidates.Length; index++)
        {
            RectTransform candidate = candidates[index];
            if (candidate == null
                || !candidate.gameObject.activeInHierarchy
                || (overlayObject != null && candidate.IsChildOf(overlayObject.transform)))
            {
                continue;
            }

            if (NormalizeKey(candidate.name) == normalizedTargetKey)
            {
                return PromoteTargetToButton(candidate);
            }
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
        if (string.IsNullOrEmpty(normalizedAction))
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
        Rect authoredBounds = new Rect(authoredPanelPosition - panelRect.rect.size * panelRect.pivot, panelRect.rect.size);
        if (!authoredBounds.Overlaps(targetRect))
        {
            panelRect.anchoredPosition = authoredPanelPosition;
            return;
        }
        float halfHeight = panelRect.rect.height * 0.5f;
        float margin = 24f;
        float upperY = bounds.yMax - halfHeight - margin;
        float lowerY = bounds.yMin + halfHeight + margin;
        bool targetBelowCenter = targetRect.center.y <= bounds.center.y;
        panelRect.anchoredPosition = new Vector2(0f, targetBelowCenter ? upperY : lowerY);
    }

    private void ResetPanelPosition()
    {
        if (panelRect != null)
        {
            panelRect.anchoredPosition = authoredPanelPosition;
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
        string signature = (step.stepCode ?? string.Empty) + "|" + (step.uiTargetKey ?? string.Empty);
        if (missingTargetWarningSignature == signature)
        {
            return;
        }

        missingTargetWarningSignature = signature;
        Debug.LogWarning(
            "引导步骤 " + step.stepCode
            + " 要求仅允许目标输入，但未找到 UI 目标 " + step.uiTargetKey
            + "；保持输入保护，等待地图和目标加载。",
            this);
    }

    private static bool IsTargetPresentation(string presentationType)
    {
        string normalizedType = NormalizeKey(presentationType);
        return normalizedType == "SPOTLIGHT" || normalizedType == "ARROW";
    }

    private static string NormalizeKey(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace('-', '_').Replace(' ', '_').ToUpperInvariant();
    }

    private void AcknowledgeCurrentStep()
    {
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
        if (currentSnapshot == null || !currentSnapshot.allowSkip || mutationRunning || messageMode)
        {
            return;
        }

        BeginMutation(true);
    }

    private void BeginMutation(bool skip)
    {
        if (!CanDisplayStep(currentSnapshot))
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
        if (!hasPendingMutation)
        {
            pendingMutationIsSkip = skip;
            pendingMutationStepCode = stepCode;
            pendingMutationKey = Guid.NewGuid().ToString("D");
            pendingMutationRequest = new OnboardingMutationRequest
            {
                expectedGuideVersion = currentSnapshot.guideVersion,
                expectedStateVersion = currentSnapshot.stateVersion
            };
            hasPendingMutation = true;
            pendingMutationPath = skip ? "/onboarding/skip"
                : "/onboarding/steps/" + UnityWebRequest.EscapeURL(stepCode) + "/ack";
            pendingMutationJson = JsonUtility.ToJson(pendingMutationRequest);
            SavePendingMutation();
        }

        StartCoroutine(MutateRoutine(pendingMutationIsSkip));
    }

    private static string PendingStorageKey(int playerId)
    {
        return "weijun.onboarding.pending." + playerId;
    }

    private void SavePendingMutation()
    {
        PlayerPrefs.SetString(PendingStorageKey(GameSession.PlayerId), JsonUtility.ToJson(new PendingIntent
        {
            key = pendingMutationKey,
            path = pendingMutationPath,
            body = pendingMutationJson,
            stepCode = pendingMutationStepCode,
            skip = pendingMutationIsSkip,
            blocked = pendingRetryBlocked
        }));
        PlayerPrefs.Save();
    }

    private void RestorePendingMutation()
    {
        PendingIntent intent = ParseResponse<PendingIntent>(PlayerPrefs.GetString(PendingStorageKey(GameSession.PlayerId), string.Empty));
        if (intent == null) return;
        OnboardingMutationRequest body = ParseResponse<OnboardingMutationRequest>(intent.body);
        string expectedPath = intent.skip ? "/onboarding/skip"
            : "/onboarding/steps/" + UnityWebRequest.EscapeURL(intent.stepCode ?? string.Empty) + "/ack";
        if (!Guid.TryParseExact(intent.key, "D", out _) || body == null || intent.path != expectedPath
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
        hasPendingMutation = true;
    }

    private void ClearPendingMutation(int playerId)
    {
        PlayerPrefs.DeleteKey(PendingStorageKey(playerId));
        PlayerPrefs.Save();
        hasPendingMutation = false;
        pendingRetryBlocked = false;
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
        if (step == null || pendingRetryBlocked) return false;
        if (step.stepCode == "WELCOME" || step.stepCode == "FINISH") return true;
        if (step.stepCode != "OPEN_HOME_MAP" && step.stepCode != "FOCUS_FREE_LAND") return false;
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
        public string key;
        public string path;
        public string body;
        public string stepCode;
        public bool skip;
        public bool blocked;
    }

    private IEnumerator MutateRoutine(bool skip)
    {
        mutationRunning = true;
        int requestPlayerId = GameSession.PlayerId;
        string requestToken = GameSession.Token;
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

                OnboardingMutationEnvelope response = ParseResponse<OnboardingMutationEnvelope>(request.downloadHandler.text);
                if (request.result == UnityWebRequest.Result.Success
                    && response != null
                    && response.code == 200
                    && response.data != null)
                {
                    ClearPendingMutation(requestPlayerId);
                    ApplyMutationResponse(response.data);
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
                if (uncertain && retryCount < MaxMutationRetries)
                {
                    retryCount++;
                    yield return new WaitForSecondsRealtime(Mathf.Pow(2f, retryCount - 1));
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
        RenderSnapshot();
    }

    private void ApplyMutationResponse(OnboardingMutationData response)
    {
        if (currentSnapshot == null || response == null)
        {
            RefreshCurrent();
            return;
        }

        if (!CanApplyWriteResponse(currentSnapshot.guideVersion, currentSnapshot.stateVersion,
            response.guideVersion, response.stateVersion))
        {
            Debug.LogWarning("新手引导写响应版本过旧，重新查询当前快照。", this);
            RefreshCurrent();
            return;
        }

        currentSnapshot.available = response.available;
        currentSnapshot.guideVersion = response.guideVersion;
        currentSnapshot.stateVersion = response.stateVersion;
        currentSnapshot.status = response.status;
        currentSnapshot.currentStep = response.currentStep;
        RenderSnapshot();
        RefreshCurrent();
    }

    private void HandleQueryFailure(int code, UnityWebRequest request)
    {
        string message = ApiRequestUtility.GetErrorMessage(request, "新手引导查询失败");
        if (code == 8001)
        {
            ShowMessage("新手引导暂不可用", "引导配置暂时不可用，请稍后重新查询。", false, false);
            ScheduleQueryRetry();
        }
        else if (code == 8004)
        {
            ShowMessage("引导状态同步中", "正在重新查询当前进度，请稍候。", false, false);
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
                    ShowMessage("引导连接恢复中", "暂时无法同步引导进度，请稍候。", false, false);
                ScheduleQueryRetry();
            }
        }
    }

    private void HandleMutationFailure(int code, UnityWebRequest request)
    {
        string message = ApiRequestUtility.GetErrorMessage(request, "新手引导操作失败");
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
            ShowMessage("引导状态同步中", "正在重新查询当前进度，请稍候。", false, false);
            RefreshCurrent();
            return;
        }

        if (code == 8001)
        {
            ShowMessage("新手引导暂不可用", "引导配置暂时不可用，请稍后重试。", false, false);
            ScheduleQueryRetry();
            return;
        }

        if (code == 8005)
        {
            pendingRetryBlocked = true;
            SavePendingMutation();
            ShowMessage("引导请求冲突", "原请求已保留，请联系客服处理后重试。", false, false);
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
        returnToUpdateFlow = false;
        PlayerPrefs.SetString("weijun.onboarding.stepCode", string.Empty);
        ResetTargetPresentation();
        titleText.text = title;
        stepText.text = string.Empty;
        contentText.text = message;
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
            && (step.stepCode == "WELCOME" || step.stepCode == "FINISH");
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
        currentSnapshot = null;
        pendingRetryBlocked = false;
        nextPendingRetryTime = 0f;
        queryQueued = false;
        StopQueryRetry();
        hasPendingMutation = false;
        pendingMutationKey = string.Empty;
        pendingMutationStepCode = string.Empty;
        pendingMutationRequest = null;
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
        yield return new WaitForSecondsRealtime(QueryRetryDelaySeconds);
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
    private sealed class OnboardingMutationEnvelope
    {
        public int code;
        public string message;
        public OnboardingMutationData data;
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
    private sealed class OnboardingMutationData
    {
        public bool available;
        public long guideVersion;
        public long stateVersion;
        public string status;
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
