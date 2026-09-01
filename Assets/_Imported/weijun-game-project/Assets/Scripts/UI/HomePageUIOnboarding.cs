using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public partial class HomePageUI
{
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
/// Shares the server onboarding input state with gameplay assemblies that are
/// loaded separately from the imported UI assembly.
/// </summary>
public static class PlayerOnboardingInputLock
{
    private const string LockedKey = "weijun.onboarding.inputLocked";
    private const string TargetOnlyKey = "weijun.onboarding.targetOnly";
    private const string ActionKey = "weijun.onboarding.actionKey";
    private const string TargetKey = "weijun.onboarding.targetKey";
    private const string SceneCodeKey = "weijun.onboarding.sceneCode";

    /// <summary>Returns true while the current onboarding step blocks input.</summary>
    public static bool IsLocked => PlayerPrefs.GetInt(LockedKey, 0) == 1;

    /// <summary>Returns true when only the server-highlighted target should be used.</summary>
    public static bool IsTargetOnly => PlayerPrefs.GetInt(TargetOnlyKey, 0) == 1;

    /// <summary>Returns the server-provided action key for the current step.</summary>
    public static string CurrentActionKey => PlayerPrefs.GetString(ActionKey, string.Empty);

    /// <summary>Returns the server-provided target key for the current step.</summary>
    public static string CurrentTargetKey => PlayerPrefs.GetString(TargetKey, string.Empty);

    /// <summary>Returns the server-provided scene code for the current step.</summary>
    public static string CurrentSceneCode => PlayerPrefs.GetString(SceneCodeKey, string.Empty);

    /// <summary>Returns true while any gameplay input should be blocked.</summary>
    public static bool IsGameplayInputBlocked => IsLocked || IsTargetOnly;

    /// <summary>Checks an action and target against the current target-only step.</summary>
    public static bool IsInputAllowed(string actionKey, string targetKey)
    {
        if (IsLocked)
        {
            return false;
        }

        if (!IsTargetOnly)
        {
            return true;
        }

        return Matches(CurrentActionKey, actionKey) || Matches(CurrentTargetKey, targetKey);
    }

    /// <summary>Checks only the server-highlighted target, without allowing an action-key shortcut.</summary>
    public static bool IsTargetAllowed(string targetKey)
    {
        if (IsLocked)
        {
            return false;
        }

        return !IsTargetOnly || Matches(CurrentTargetKey, targetKey);
    }

    internal static void SetState(bool locked, bool targetOnly, string actionKey)
    {
        SetState(locked, targetOnly, actionKey, string.Empty, string.Empty);
    }

    internal static void SetState(
        bool locked,
        bool targetOnly,
        string actionKey,
        string targetKey,
        string sceneCode)
    {
        PlayerPrefs.SetInt(LockedKey, locked ? 1 : 0);
        PlayerPrefs.SetInt(TargetOnlyKey, targetOnly ? 1 : 0);
        PlayerPrefs.SetString(ActionKey, actionKey ?? string.Empty);
        PlayerPrefs.SetString(TargetKey, targetKey ?? string.Empty);
        PlayerPrefs.SetString(SceneCodeKey, sceneCode ?? string.Empty);
    }

    private static bool Matches(string current, string requested)
    {
        if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(requested))
        {
            return false;
        }

        return string.Equals(current, requested, StringComparison.OrdinalIgnoreCase)
            || current.IndexOf(requested, StringComparison.OrdinalIgnoreCase) >= 0
            || requested.IndexOf(current, StringComparison.OrdinalIgnoreCase) >= 0;
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
    private const string HomeSceneName = "HomeScene";
    private const string ManagementSceneName = "Simulation management mode";
    private const string IdleAdventureSceneName = "Idle Adventure mode";
    private const int SupportedContractVersion = 1;
    private const float BusinessRefreshIntervalSeconds = 2f;
    private const float QueryRetryDelaySeconds = 5f;
    private const int MaxMutationRetries = 2;
    private const float TargetRefreshIntervalSeconds = 0.2f;

    private static PlayerOnboardingManager instance;

    private OnboardingCurrentData currentSnapshot;
    private GameObject overlayObject;
    private Image overlayImage;
    private TargetOnlyRaycastFilter targetOnlyRaycastFilter;
    private GameObject targetHighlightObject;
    private RectTransform targetHighlightRect;
    private Image targetHighlightImage;
    private Outline targetHighlightOutline;
    private TMP_Text targetArrowText;
    private RectTransform targetArrowRect;
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
    private float nextTargetRefreshAt;
    private bool targetLookupWarningShown;

    /// <summary>Returns the persistent onboarding manager instance.</summary>
    public static PlayerOnboardingManager Instance => instance;

    /// <summary>Creates or returns the persistent onboarding manager.</summary>
    public static PlayerOnboardingManager EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        PlayerOnboardingManager existing = FindObjectOfType<PlayerOnboardingManager>(true);
        if (existing != null)
        {
            instance = existing;
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
        PlayerOnboardingInputLock.SetState(false, false, string.Empty, string.Empty, string.Empty);
        SceneManager.sceneLoaded += HandleSceneLoaded;
        StartCoroutine(RefreshAfterStartup());
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (instance == this)
        {
            instance = null;
            PlayerOnboardingInputLock.SetState(false, false, string.Empty, string.Empty, string.Empty);
        }
    }

    private void Update()
    {
        if (messageMode
            || overlayObject == null
            || !overlayObject.activeInHierarchy
            || currentSnapshot == null
            || currentSnapshot.currentStep == null
            || !string.Equals(currentSnapshot.currentStep.inputMode, "ALLOW_TARGET_ONLY", StringComparison.OrdinalIgnoreCase)
            || !IsCurrentSceneForStep(currentSnapshot.currentStep.sceneCode)
            || Time.unscaledTime < nextTargetRefreshAt)
        {
            return;
        }

        nextTargetRefreshAt = Time.unscaledTime + TargetRefreshIntervalSeconds;
        RefreshTargetPresentation(currentSnapshot.currentStep, true);
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

    private IEnumerator RefreshAfterStartup()
    {
        yield return null;
        RefreshCurrent();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
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

            UnityWebRequest request;
            string requestError;
            if (!ApiRequestUtility.TryCreateGet("/onboarding/current", true, out request, out requestError))
            {
                Debug.LogWarning("新手引导查询请求创建失败：" + requestError, this);
                break;
            }

            using (request)
            {
                yield return request.SendWebRequest();
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
                false,
                false);
            Debug.LogWarning(
                "新手引导版本不受支持：contractVersion=" + snapshot.contractVersion
                + ", minimumClientVersionCode=" + snapshot.minimumClientVersionCode,
                this);
            return;
        }

        if (currentSnapshot != null
            && snapshot.guideVersion == currentSnapshot.guideVersion
            && snapshot.stateVersion < currentSnapshot.stateVersion)
        {
            Debug.LogWarning(
                "丢弃较旧的新手引导快照：localStateVersion=" + currentSnapshot.stateVersion
                + ", responseStateVersion=" + snapshot.stateVersion,
                this);
            return;
        }

        currentSnapshot = snapshot;
        RenderSnapshot();
    }

    private void RenderSnapshot()
    {
        if (messageMode)
        {
            return;
        }

        if (currentSnapshot == null
            || !currentSnapshot.available
            || currentSnapshot.currentStep == null
            || IsTerminalStatus(currentSnapshot.status))
        {
            HideOverlay();
            EnsureBusinessPollingState();
            return;
        }

        if (!IsCurrentSceneForStep(currentSnapshot.currentStep.sceneCode))
        {
            HideOverlay();
            EnsureBusinessPollingState();
            return;
        }

        if (string.Equals(currentSnapshot.status, "PAUSED", StringComparison.OrdinalIgnoreCase))
        {
            ShowMessage("新手引导暂不可用", "引导当前处于暂停状态，请稍后重新查询。", false, false);
            EnsureBusinessPollingState();
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
        bool blocksAllInput = isClientAck
            || string.Equals(step.inputMode, "BLOCK_ALL", StringComparison.OrdinalIgnoreCase);
        bool targetOnly = string.Equals(step.inputMode, "ALLOW_TARGET_ONLY", StringComparison.OrdinalIgnoreCase);

        titleText.text = string.IsNullOrEmpty(step.title) ? "新手引导" : step.title;
        stepText.text = step.sequence > 0
            ? "步骤 " + step.sequence + (string.IsNullOrEmpty(currentSnapshot.status) ? string.Empty : "  ·  " + currentSnapshot.status)
            : currentSnapshot.status;
        contentText.text = string.IsNullOrEmpty(step.content) ? "请按照当前提示完成操作。" : step.content;
        targetText.text = BuildTargetText(step, targetOnly);
        stateText.text = isBusinessEvent
            ? "请完成实际游戏操作，完成后由服务器自动推进。"
            : "请确认已阅读当前说明。";

        acknowledgeButton.gameObject.SetActive(isClientAck);
        acknowledgeButton.interactable = isClientAck && !mutationRunning;
        acknowledgeButtonText.text = mutationRunning ? "提交中..." : "完成当前步骤";

        skipButton.gameObject.SetActive(currentSnapshot.allowSkip);
        skipButton.interactable = !mutationRunning;
        skipButtonText.text = "跳过引导";

        overlayImage.raycastTarget = blocksAllInput || targetOnly;
        overlayImage.color = blocksAllInput
            ? new Color(0.03f, 0.04f, 0.05f, 0.64f)
            : new Color(0.03f, 0.04f, 0.05f, 0f);
        PlayerOnboardingInputLock.SetState(
            blocksAllInput,
            targetOnly,
            step.actionKey,
            step.uiTargetKey,
            step.sceneCode);
        RefreshTargetPresentation(step, targetOnly);
        overlayObject.SetActive(true);
        transform.SetAsLastSibling();
        EnsureBusinessPollingState();
    }

    private void EnsureBusinessPollingState()
    {
        bool shouldPoll = currentSnapshot != null
            && currentSnapshot.available
            && currentSnapshot.currentStep != null
            && string.Equals(currentSnapshot.currentStep.completionType, "BUSINESS_EVENT", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(currentSnapshot.status, "PAUSED", StringComparison.OrdinalIgnoreCase)
            && GameSession.IsAuthenticated;

        if (shouldPoll && businessPollingCoroutine == null)
        {
            businessPollingCoroutine = StartCoroutine(PollBusinessStep());
        }
    }

    private IEnumerator PollBusinessStep()
    {
        while (GameSession.IsAuthenticated
            && currentSnapshot != null
            && currentSnapshot.currentStep != null
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

        Transform existingCanvasTransform = transform.Find("PlayerOnboardingCanvas");
        GameObject canvasObject = existingCanvasTransform != null
            ? existingCanvasTransform.gameObject
            : new GameObject(
                "PlayerOnboardingCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
        if (existingCanvasTransform == null)
        {
            canvasObject.transform.SetParent(transform, false);
        }

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = canvasObject.AddComponent<Canvas>();
        }
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32760;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvasObject.AddComponent<CanvasScaler>();
        }
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
        {
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        if (existingCanvasTransform != null && TryBindSceneOverlay(canvasObject.transform))
        {
            return;
        }

        overlayObject = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
        overlayObject.transform.SetParent(canvasObject.transform, false);
        overlayImage = overlayObject.GetComponent<Image>();
        overlayImage.raycastTarget = true;
        targetOnlyRaycastFilter = overlayObject.AddComponent<TargetOnlyRaycastFilter>();
        SetStretch(overlayObject.GetComponent<RectTransform>());

        targetHighlightObject = new GameObject("TargetHighlight", typeof(RectTransform), typeof(Image), typeof(Outline));
        targetHighlightObject.transform.SetParent(canvasObject.transform, false);
        targetHighlightRect = targetHighlightObject.GetComponent<RectTransform>();
        targetHighlightRect.anchorMin = new Vector2(0.5f, 0.5f);
        targetHighlightRect.anchorMax = new Vector2(0.5f, 0.5f);
        targetHighlightRect.pivot = new Vector2(0.5f, 0.5f);
        targetHighlightImage = targetHighlightObject.GetComponent<Image>();
        targetHighlightImage.sprite = CreateHighlightSprite();
        targetHighlightImage.color = new Color(1f, 0.84f, 0.24f, 0.12f);
        targetHighlightImage.raycastTarget = false;
        targetHighlightOutline = targetHighlightObject.GetComponent<Outline>();
        targetHighlightOutline.effectColor = new Color(1f, 0.84f, 0.24f, 0.95f);
        targetHighlightOutline.effectDistance = new Vector2(4f, 4f);
        targetHighlightObject.SetActive(false);

        targetArrowText = CreateText("TargetArrow", canvasObject.transform, 32f, TextAlignmentOptions.Center);
        targetArrowText.color = new Color(1f, 0.84f, 0.24f, 1f);
        targetArrowRect = targetArrowText.rectTransform;
        targetArrowRect.anchorMin = new Vector2(0.5f, 0.5f);
        targetArrowRect.anchorMax = new Vector2(0.5f, 0.5f);
        targetArrowRect.pivot = new Vector2(0.5f, 0.5f);
        targetArrowRect.sizeDelta = new Vector2(60f, 60f);
        targetArrowText.gameObject.SetActive(false);

        GameObject panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(overlayObject.transform, false);
        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.97f, 0.92f, 0.81f, 1f);
        panelImage.raycastTarget = false;
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(760f, 410f);

        titleText = CreateText("Title", panelObject.transform, 30f, TextAlignmentOptions.Center);
        SetRect(titleText.rectTransform, new Vector2(24f, 330f), new Vector2(736f, 386f));
        stepText = CreateText("Step", panelObject.transform, 16f, TextAlignmentOptions.Right);
        SetRect(stepText.rectTransform, new Vector2(24f, 294f), new Vector2(736f, 328f));
        contentText = CreateText("Content", panelObject.transform, 21f, TextAlignmentOptions.TopLeft);
        contentText.enableWordWrapping = true;
        SetRect(contentText.rectTransform, new Vector2(38f, 166f), new Vector2(722f, 286f));
        targetText = CreateText("Target", panelObject.transform, 16f, TextAlignmentOptions.TopLeft);
        targetText.enableWordWrapping = true;
        SetRect(targetText.rectTransform, new Vector2(38f, 100f), new Vector2(722f, 158f));
        stateText = CreateText("State", panelObject.transform, 15f, TextAlignmentOptions.Center);
        stateText.enableWordWrapping = true;
        SetRect(stateText.rectTransform, new Vector2(38f, 62f), new Vector2(722f, 98f));

        acknowledgeButton = CreateButton(
            "AcknowledgeButton",
            panelObject.transform,
            "完成当前步骤",
            new Vector2(-112f, 24f),
            out acknowledgeButtonText);
        acknowledgeButton.onClick.AddListener(AcknowledgeCurrentStep);
        skipButton = CreateButton(
            "SkipButton",
            panelObject.transform,
            "跳过引导",
            new Vector2(112f, 24f),
            out skipButtonText);
        skipButton.onClick.AddListener(SkipCurrentOnboarding);
    }

    private bool TryBindSceneOverlay(Transform canvasTransform)
    {
        Transform overlayTransform = canvasTransform.Find("Overlay");
        Transform targetHighlightTransform = canvasTransform.Find("TargetHighlight");
        Transform targetArrowTransform = canvasTransform.Find("TargetArrow");
        Transform panelTransform = overlayTransform == null ? null : overlayTransform.Find("Panel");
        if (overlayTransform == null
            || targetHighlightTransform == null
            || targetArrowTransform == null
            || panelTransform == null)
        {
            return false;
        }

        overlayObject = overlayTransform.gameObject;
        overlayImage = overlayObject.GetComponent<Image>();
        targetOnlyRaycastFilter = overlayObject.GetComponent<TargetOnlyRaycastFilter>();
        targetHighlightObject = targetHighlightTransform.gameObject;
        targetHighlightRect = targetHighlightTransform as RectTransform;
        targetHighlightImage = targetHighlightObject.GetComponent<Image>();
        targetHighlightOutline = targetHighlightObject.GetComponent<Outline>();
        targetArrowText = targetArrowTransform.GetComponent<TMP_Text>();
        targetArrowRect = targetArrowTransform as RectTransform;
        titleText = FindSceneText(panelTransform, "Title");
        stepText = FindSceneText(panelTransform, "Step");
        contentText = FindSceneText(panelTransform, "Content");
        targetText = FindSceneText(panelTransform, "Target");
        stateText = FindSceneText(panelTransform, "State");
        acknowledgeButton = FindSceneButton(panelTransform, "AcknowledgeButton");
        skipButton = FindSceneButton(panelTransform, "SkipButton");
        acknowledgeButtonText = FindSceneText(acknowledgeButton == null ? null : acknowledgeButton.transform, "Label");
        skipButtonText = FindSceneText(skipButton == null ? null : skipButton.transform, "Label");

        if (overlayImage == null
            || targetHighlightRect == null
            || targetHighlightImage == null
            || targetHighlightOutline == null
            || targetArrowText == null
            || targetArrowRect == null
            || titleText == null
            || stepText == null
            || contentText == null
            || targetText == null
            || stateText == null
            || acknowledgeButton == null
            || skipButton == null
            || acknowledgeButtonText == null
            || skipButtonText == null)
        {
            Debug.LogWarning("PlayerOnboardingCanvas exists but its editable child components are incomplete.", this);
            overlayObject = null;
            return false;
        }

        if (targetHighlightImage.sprite == null)
        {
            targetHighlightImage.sprite = CreateHighlightSprite();
        }

        if (targetOnlyRaycastFilter == null)
        {
            targetOnlyRaycastFilter = overlayObject.AddComponent<TargetOnlyRaycastFilter>();
        }

        acknowledgeButton.onClick.AddListener(AcknowledgeCurrentStep);
        skipButton.onClick.AddListener(SkipCurrentOnboarding);
        return true;
    }

    private static TMP_Text FindSceneText(Transform parent, string objectName)
    {
        if (parent == null)
        {
            return null;
        }

        Transform child = parent.Find(objectName);
        return child == null ? null : child.GetComponent<TMP_Text>();
    }

    private static Button FindSceneButton(Transform parent, string objectName)
    {
        if (parent == null)
        {
            return null;
        }

        Transform child = parent.Find(objectName);
        return child == null ? null : child.GetComponent<Button>();
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
        text.color = new Color(0.16f, 0.12f, 0.09f, 1f);
        text.raycastTarget = false;
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
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = position;
        buttonRect.sizeDelta = new Vector2(190f, 52f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.25f, 0.48f, 0.62f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        labelText = CreateText("Label", buttonObject.transform, 17f, TextAlignmentOptions.Center);
        labelText.text = label;
        labelText.color = Color.white;
        SetStretch(labelText.rectTransform);
        return button;
    }

    private void AcknowledgeCurrentStep()
    {
        if (currentSnapshot == null
            || currentSnapshot.currentStep == null
            || !string.Equals(currentSnapshot.currentStep.completionType, "CLIENT_ACK", StringComparison.OrdinalIgnoreCase)
            || mutationRunning)
        {
            return;
        }

        BeginMutation(false);
    }

    private void SkipCurrentOnboarding()
    {
        if (currentSnapshot == null || !currentSnapshot.allowSkip || mutationRunning)
        {
            return;
        }

        BeginMutation(true);
    }

    private void BeginMutation(bool skip)
    {
        if (currentSnapshot == null || currentSnapshot.currentStep == null)
        {
            return;
        }

        string stepCode = currentSnapshot.currentStep.stepCode ?? string.Empty;
        if (!skip && !string.Equals(currentSnapshot.currentStep.completionType, "CLIENT_ACK", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning("当前新手引导步骤必须由真实业务事件完成，不能调用 ACK：" + stepCode, this);
            return;
        }

        messageMode = false;

        if (!hasPendingMutation
            || pendingMutationIsSkip != skip
            || !string.Equals(pendingMutationStepCode, stepCode, StringComparison.Ordinal)
            || pendingMutationRequest == null
            || pendingMutationRequest.expectedGuideVersion != currentSnapshot.guideVersion
            || pendingMutationRequest.expectedStateVersion != currentSnapshot.stateVersion)
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
        }

        StartCoroutine(MutateRoutine(skip));
    }

    private IEnumerator MutateRoutine(bool skip)
    {
        mutationRunning = true;
        int retryCount = 0;
        bool complete = false;

        while (!complete && GameSession.IsAuthenticated)
        {
            string path = skip
                ? "/onboarding/skip"
                : "/onboarding/steps/" + UnityWebRequest.EscapeURL(pendingMutationStepCode) + "/ack";
            string json = JsonUtility.ToJson(pendingMutationRequest);
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
                    ApplyMutationResponse(response.data);
                    hasPendingMutation = false;
                    pendingMutationKey = string.Empty;
                    pendingMutationStepCode = string.Empty;
                    pendingMutationRequest = null;
                    complete = true;
                    continue;
                }

                int code = response == null ? 0 : response.code;
                if (IsRetryable(request) && retryCount < MaxMutationRetries)
                {
                    retryCount++;
                    yield return new WaitForSecondsRealtime(Mathf.Pow(2f, retryCount - 1));
                    continue;
                }

                if (IsRetryable(request))
                {
                    Debug.LogWarning("新手引导写请求结果不确定，重新查询当前快照。", this);
                    RefreshCurrent();
                    complete = true;
                    continue;
                }

                HandleMutationFailure(code, request);
                complete = true;
            }
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

        if (response.guideVersion != currentSnapshot.guideVersion
            || response.stateVersion < currentSnapshot.stateVersion)
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
            RefreshCurrent();
        }
        else
        {
            Debug.LogWarning("新手引导查询失败：" + (code > 0 ? "[" + code + "] " : string.Empty) + message, this);
            if (request != null && (request.result == UnityWebRequest.Result.ConnectionError
                || request.responseCode == 429
                || request.responseCode >= 500))
            {
                ScheduleQueryRetry();
            }
        }
    }

    private void HandleMutationFailure(int code, UnityWebRequest request)
    {
        string message = ApiRequestUtility.GetErrorMessage(request, "新手引导操作失败");
        if (code == 8002 || code == 8003 || code == 8004 || code == 8006)
        {
            hasPendingMutation = false;
            pendingMutationKey = string.Empty;
            pendingMutationStepCode = string.Empty;
            pendingMutationRequest = null;
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
            HandleMutationRequestConflict(message);
            ShowMessage("Onboarding request conflict", "The previous request was retained. Confirm again to create a new request UUID.", true, false);
            Debug.LogWarning("新手引导幂等键被重复用于不同请求：" + message, this);
            return;
        }

        Debug.LogWarning("新手引导操作失败：" + (code > 0 ? "[" + code + "] " : string.Empty) + message, this);
    }

    private void HandleMutationRequestConflict(string message)
    {
        string originalKey = pendingMutationKey;
        string originalStepCode = pendingMutationStepCode;
        ResetPendingMutation();
        Debug.LogWarning(
            "Onboarding request UUID was reused for a different request. The original request remains recorded by the server; a new confirmation will create a new UUID. uuid="
            + originalKey + ", step=" + originalStepCode + ", message=" + message,
            this);
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
        titleText.text = title;
        stepText.text = string.Empty;
        contentText.text = message;
        targetText.text = string.Empty;
        stateText.text = string.Empty;
        acknowledgeButton.gameObject.SetActive(showAcknowledge);
        acknowledgeButton.interactable = showAcknowledge && !mutationRunning;
        skipButton.gameObject.SetActive(showSkip);
        bool shouldBlockInput = blockInput && (showAcknowledge || showSkip);
        overlayImage.raycastTarget = shouldBlockInput;
        overlayImage.color = shouldBlockInput
            ? new Color(0.03f, 0.04f, 0.05f, 0.64f)
            : new Color(0.03f, 0.04f, 0.05f, 0f);
        PlayerOnboardingInputLock.SetState(shouldBlockInput, false, string.Empty, string.Empty, string.Empty);
        RefreshTargetPresentation(null, false);
        overlayObject.SetActive(true);
    }

    private void HideOverlay()
    {
        messageMode = false;
        if (overlayObject != null)
        {
            overlayObject.SetActive(false);
        }

        PlayerOnboardingInputLock.SetState(false, false, string.Empty, string.Empty, string.Empty);
        RefreshTargetPresentation(null, false);
    }

    private void ClearSnapshot()
    {
        currentSnapshot = null;
        queryQueued = false;
        StopQueryRetry();
        hasPendingMutation = false;
        pendingMutationKey = string.Empty;
        pendingMutationStepCode = string.Empty;
        pendingMutationRequest = null;
        HideOverlay();
    }

    private void ResetPendingMutation()
    {
        hasPendingMutation = false;
        pendingMutationKey = string.Empty;
        pendingMutationStepCode = string.Empty;
        pendingMutationRequest = null;
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
            || request.responseCode >= 500;
    }

    private static bool IsTerminalStatus(string status)
    {
        return string.Equals(status, "COMPLETED", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "SKIPPED", StringComparison.OrdinalIgnoreCase);
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

    private void RefreshTargetPresentation(OnboardingStepData step, bool targetOnly)
    {
        if (targetOnlyRaycastFilter == null || targetHighlightObject == null)
        {
            return;
        }

        if (!targetOnly || step == null || string.IsNullOrEmpty(step.uiTargetKey))
        {
            targetOnlyRaycastFilter.SetAllowedTargets(null, null, null);
            targetHighlightObject.SetActive(false);
            if (targetArrowText != null)
            {
                targetArrowText.gameObject.SetActive(false);
            }
            return;
        }

        RectTransform targetRect = ResolveTargetRect(step);
        RectTransform skipRect = skipButton != null ? skipButton.GetComponent<RectTransform>() : null;
        Canvas targetCanvas = targetRect != null ? targetRect.GetComponentInParent<Canvas>() : null;
        Camera targetCamera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? targetCanvas.worldCamera
            : null;
        targetOnlyRaycastFilter.SetAllowedTargets(targetRect, skipRect, targetCamera);
        if (targetRect == null)
        {
            targetHighlightObject.SetActive(false);
            if (targetArrowText != null)
            {
                targetArrowText.gameObject.SetActive(false);
            }
            if (!targetLookupWarningShown)
            {
                targetLookupWarningShown = true;
                Debug.LogWarning(
                    "Onboarding target was not found for uiTargetKey=" + step.uiTargetKey
                    + ". Add an OnboardingTargetAnchor to the target object or use a registered scene name.",
                    this);
            }

            return;
        }

        targetLookupWarningShown = false;
        if (!TrySetHighlightRect(targetRect, targetCamera))
        {
            targetHighlightObject.SetActive(false);
            if (targetArrowText != null)
            {
                targetArrowText.gameObject.SetActive(false);
            }
            return;
        }

        Color highlightColor = ParseHighlightColor(step.presentationParams);
        targetHighlightImage.color = new Color(highlightColor.r, highlightColor.g, highlightColor.b, 0.12f);
        targetHighlightOutline.effectColor = new Color(highlightColor.r, highlightColor.g, highlightColor.b, 0.95f);
        targetHighlightObject.SetActive(true);
        targetHighlightObject.transform.SetAsLastSibling();
        ConfigureTargetArrow(step.presentationParams, highlightColor);
    }

    private void ConfigureTargetArrow(
        OnboardingPresentationParamsData presentationParams,
        Color highlightColor)
    {
        if (targetArrowText == null || targetArrowRect == null || targetHighlightRect == null)
        {
            return;
        }

        string direction = presentationParams == null || string.IsNullOrEmpty(presentationParams.arrowDirection)
            ? "UP"
            : presentationParams.arrowDirection.Trim().ToUpperInvariant();
        Vector2 offset = Vector2.up * (targetHighlightRect.sizeDelta.y * 0.5f + 34f);
        string symbol = "^";
        if (direction == "DOWN" || direction == "BOTTOM")
        {
            offset = Vector2.down * (targetHighlightRect.sizeDelta.y * 0.5f + 34f);
            symbol = "v";
        }
        else if (direction == "LEFT")
        {
            offset = Vector2.left * (targetHighlightRect.sizeDelta.x * 0.5f + 34f);
            symbol = "<";
        }
        else if (direction == "RIGHT")
        {
            offset = Vector2.right * (targetHighlightRect.sizeDelta.x * 0.5f + 34f);
            symbol = ">";
        }

        targetArrowText.text = symbol;
        targetArrowText.color = new Color(highlightColor.r, highlightColor.g, highlightColor.b, 1f);
        targetArrowRect.anchoredPosition = targetHighlightRect.anchoredPosition + offset;
        targetArrowText.transform.SetAsLastSibling();
        targetArrowText.gameObject.SetActive(true);
    }

    private RectTransform ResolveTargetRect(OnboardingStepData step)
    {
        if (step == null || string.IsNullOrEmpty(step.uiTargetKey))
        {
            return null;
        }

        OnboardingTargetAnchor[] anchors = FindObjectsOfType<OnboardingTargetAnchor>(true);
        for (int i = 0; i < anchors.Length; i++)
        {
            OnboardingTargetAnchor anchor = anchors[i];
            if (anchor == null || !anchor.isActiveAndEnabled || !anchor.Matches(step.uiTargetKey, step.actionKey))
            {
                continue;
            }

            RectTransform anchorRect = anchor.RectTransform;
            if (anchorRect != null && anchorRect.gameObject.activeInHierarchy)
            {
                return anchorRect;
            }
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            return null;
        }

        string[] exactNames = { step.uiTargetKey };
        RectTransform exactMatch = FindTargetRectInScene(activeScene, exactNames, false);
        if (exactMatch != null)
        {
            return exactMatch;
        }

        return FindTargetRectInScene(activeScene, GetFallbackTargetNames(step.uiTargetKey), true);
    }

    private static RectTransform FindTargetRectInScene(Scene scene, string[] names, bool allowContains)
    {
        if (names == null || names.Length == 0)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] == null || !roots[i].activeInHierarchy)
            {
                continue;
            }

            RectTransform match = FindTargetRectRecursive(roots[i].transform, names, allowContains);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static RectTransform FindTargetRectRecursive(Transform current, string[] names, bool allowContains)
    {
        if (current == null || !current.gameObject.activeInHierarchy)
        {
            return null;
        }

        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            bool matches = string.Equals(current.name, name, StringComparison.OrdinalIgnoreCase);
            if (!matches && allowContains)
            {
                matches = current.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (matches)
            {
                RectTransform rect = current as RectTransform;
                if (rect != null)
                {
                    return rect;
                }
            }
        }

        for (int i = 0; i < current.childCount; i++)
        {
            RectTransform match = FindTargetRectRecursive(current.GetChild(i), names, allowContains);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static string[] GetFallbackTargetNames(string targetKey)
    {
        string normalized = targetKey == null ? string.Empty : targetKey.ToUpperInvariant();
        if (normalized.IndexOf("BUILDING", StringComparison.Ordinal) >= 0)
        {
            return new[] { "Building Item", "BuildButton", "BuildingButton", "LandInfoPanel" };
        }

        if (normalized.IndexOf("LAND", StringComparison.Ordinal) >= 0)
        {
            return new[] { "LandSlot", "Landslot", "FreeLand", "MapRoot" };
        }

        if (normalized.IndexOf("MAP", StringComparison.Ordinal) >= 0)
        {
            return new[] { "MapRoot", "HomeMapSelector", "EnterMapButton" };
        }

        if (normalized.IndexOf("HOME", StringComparison.Ordinal) >= 0)
        {
            return new[] { "BookHomeCanvas", "HomePanelsCanvas", "MainBookPage" };
        }

        return new[] { targetKey };
    }

    private bool TrySetHighlightRect(RectTransform targetRect, Camera targetCamera)
    {
        if (targetRect == null || targetHighlightRect == null)
        {
            return false;
        }

        Canvas highlightCanvas = targetHighlightObject.GetComponentInParent<Canvas>();
        RectTransform canvasRect = highlightCanvas != null ? highlightCanvas.transform as RectTransform : null;
        if (canvasRect == null)
        {
            Debug.LogWarning("Onboarding highlight canvas is missing a RectTransform.", this);
            return false;
        }

        Vector3[] corners = new Vector3[4];
        targetRect.GetWorldCorners(corners);
        Vector2 minimum = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 maximum = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < corners.Length; i++)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(targetCamera, corners[i]);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out Vector2 localPoint))
            {
                return false;
            }

            minimum = Vector2.Min(minimum, localPoint);
            maximum = Vector2.Max(maximum, localPoint);
        }

        Vector2 size = maximum - minimum;
        targetHighlightRect.anchoredPosition = (minimum + maximum) * 0.5f;
        targetHighlightRect.sizeDelta = new Vector2(Mathf.Max(36f, size.x + 24f), Mathf.Max(36f, size.y + 24f));
        return true;
    }

    private static Color ParseHighlightColor(OnboardingPresentationParamsData presentationParams)
    {
        if (presentationParams == null || string.IsNullOrEmpty(presentationParams.highlightColor))
        {
            return new Color(1f, 0.84f, 0.24f, 1f);
        }

        string value = presentationParams.highlightColor.Trim();
        if (!value.StartsWith("#", StringComparison.Ordinal))
        {
            value = "#" + value;
        }

        if (ColorUtility.TryParseHtmlString(value, out Color color))
        {
            return color;
        }

        return new Color(1f, 0.84f, 0.24f, 1f);
    }

    private static bool IsCurrentSceneForStep(string sceneCode)
    {
        if (string.IsNullOrEmpty(sceneCode))
        {
            return true;
        }

        string activeSceneName = SceneManager.GetActiveScene().name;
        if (string.Equals(sceneCode, activeSceneName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string normalized = sceneCode.Replace("-", "_").Replace(" ", "_").ToUpperInvariant();
        if (normalized == "HOME" || normalized == "HOME_SCENE")
        {
            return string.Equals(activeSceneName, HomeSceneName, StringComparison.OrdinalIgnoreCase);
        }

        if (normalized == "LAND"
            || normalized == "MAP"
            || normalized == "BUILD"
            || normalized == "BUILDING"
            || normalized == "SIMULATION_MANAGEMENT"
            || normalized == "SIMULATION_MANAGEMENT_MODE")
        {
            return string.Equals(activeSceneName, ManagementSceneName, StringComparison.OrdinalIgnoreCase);
        }

        if (normalized == "IDLE_ADVENTURE" || normalized == "IDLE_ADVENTURE_MODE")
        {
            return string.Equals(activeSceneName, IdleAdventureSceneName, StringComparison.OrdinalIgnoreCase);
        }

        if (normalized == "LAUNCH")
        {
            return string.Equals(activeSceneName, LaunchSceneName, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static Sprite CreateHighlightSprite()
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.name = "RuntimeOnboardingHighlightTexture";
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.hideFlags = HideFlags.DontSave;
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        sprite.name = "RuntimeOnboardingHighlightSprite";
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    private sealed class TargetOnlyRaycastFilter : MonoBehaviour, ICanvasRaycastFilter
    {
        private RectTransform targetRect;
        private RectTransform additionalAllowedRect;
        private Camera targetCamera;

        public void SetAllowedTargets(RectTransform target, RectTransform additional, Camera camera)
        {
            targetRect = target;
            additionalAllowedRect = additional;
            targetCamera = camera;
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (targetRect != null && RectTransformUtility.RectangleContainsScreenPoint(targetRect, screenPoint, targetCamera))
            {
                return false;
            }

            if (additionalAllowedRect != null
                && RectTransformUtility.RectangleContainsScreenPoint(additionalAllowedRect, screenPoint, eventCamera))
            {
                return false;
            }

            return true;
        }
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
        public int guideVersion;
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
        public int guideVersion;
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
        public int landConfigId;
        public int buildingConfigId;
        public int playerLandId;
        public int playerBuildingId;
    }

    [Serializable]
    private sealed class OnboardingMutationRequest
    {
        public long expectedGuideVersion;
        public long expectedStateVersion;
    }
}

/// <summary>
/// Marks a scene object as a stable onboarding presentation target.
/// Configure this component on a UI object instead of coupling the server to a hierarchy path.
/// </summary>
[DisallowMultipleComponent]
public sealed class OnboardingTargetAnchor : MonoBehaviour
{
    [Header("Onboarding target")]
    [Tooltip("The uiTargetKey published by the onboarding configuration.")]
    [SerializeField] private string targetKey;
    [Tooltip("Optional actionKey accepted for this target.")]
    [SerializeField] private string actionKey;

    /// <summary>Returns the configured server target key.</summary>
    public string TargetKey => targetKey;

    /// <summary>Returns the optional action key accepted by this target.</summary>
    public string ActionKey => actionKey;

    /// <summary>Returns this target's UI rectangle, when it is a UGUI object.</summary>
    public RectTransform RectTransform => GetComponent<RectTransform>();

    /// <summary>Checks whether this anchor represents the requested server target.</summary>
    public bool Matches(string requestedTargetKey, string requestedActionKey)
    {
        if (!MatchesValue(targetKey, requestedTargetKey))
        {
            return false;
        }

        return string.IsNullOrEmpty(actionKey)
            || string.IsNullOrEmpty(requestedActionKey)
            || string.Equals(actionKey, requestedActionKey, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesValue(string current, string requested)
    {
        if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(requested))
        {
            return false;
        }

        return string.Equals(current, requested, StringComparison.OrdinalIgnoreCase);
    }
}
