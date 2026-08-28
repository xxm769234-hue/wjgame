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

    /// <summary>Returns true while the current onboarding step blocks input.</summary>
    public static bool IsLocked => PlayerPrefs.GetInt(LockedKey, 0) == 1;

    /// <summary>Returns true when only the server-highlighted target should be used.</summary>
    public static bool IsTargetOnly => PlayerPrefs.GetInt(TargetOnlyKey, 0) == 1;

    /// <summary>Returns the server-provided action key for the current step.</summary>
    public static string CurrentActionKey => PlayerPrefs.GetString(ActionKey, string.Empty);

    internal static void SetState(bool locked, bool targetOnly, string actionKey)
    {
        PlayerPrefs.SetInt(LockedKey, locked ? 1 : 0);
        PlayerPrefs.SetInt(TargetOnlyKey, targetOnly ? 1 : 0);
        PlayerPrefs.SetString(ActionKey, actionKey ?? string.Empty);
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
    private const int MaxMutationRetries = 2;

    private static PlayerOnboardingManager instance;

    private OnboardingCurrentData currentSnapshot;
    private GameObject overlayObject;
    private Image overlayImage;
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
        PlayerOnboardingInputLock.SetState(false, false, string.Empty);
        SceneManager.sceneLoaded += HandleSceneLoaded;
        StartCoroutine(RefreshAfterStartup());
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (instance == this)
        {
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
        bool targetOnly = isBusinessEvent
            && string.Equals(step.inputMode, "ALLOW_TARGET_ONLY", StringComparison.OrdinalIgnoreCase);

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

        overlayImage.raycastTarget = blocksAllInput;
        overlayImage.color = blocksAllInput
            ? new Color(0.03f, 0.04f, 0.05f, 0.64f)
            : new Color(0.03f, 0.04f, 0.05f, 0f);
        PlayerOnboardingInputLock.SetState(blocksAllInput, targetOnly, step.actionKey);
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
            ShowMessage("引导请求已保留", "本次确认请求需要使用原请求号重试，请再次点击确认。", true, false);
            Debug.LogWarning("新手引导幂等键被重复用于不同请求：" + message, this);
            return;
        }

        Debug.LogWarning("新手引导操作失败：" + (code > 0 ? "[" + code + "] " : string.Empty) + message, this);
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
        PlayerOnboardingInputLock.SetState(shouldBlockInput, false, string.Empty);
        overlayObject.SetActive(true);
    }

    private void HideOverlay()
    {
        messageMode = false;
        if (overlayObject != null)
        {
            overlayObject.SetActive(false);
        }

        PlayerOnboardingInputLock.SetState(false, false, string.Empty);
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
