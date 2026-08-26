using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public partial class HomePageUI
{
    private GameObject onboardingOverlay;
    private TMP_Text onboardingTitle;
    private TMP_Text onboardingContent;
    private TMP_Text onboardingStep;
    private Button onboardingAckButton;
    private Button onboardingSkipButton;
    private OnboardingCurrentData onboardingData;
    private bool onboardingRequestRunning;

    private void LoadServerOnboarding()
    {
        if (HasAuthSession() && !onboardingRequestRunning)
        {
            StartCoroutine(RequestServerOnboarding());
        }
    }

    private IEnumerator RequestServerOnboarding()
    {
        onboardingRequestRunning = true;
        UnityWebRequest request = CreateAuthorizedGet("/onboarding/current");
        if (request == null)
        {
            onboardingRequestRunning = false;
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                onboardingRequestRunning = false;
                yield break;
            }

            OnboardingCurrentApiResponse response = ParseJson<OnboardingCurrentApiResponse>(request.downloadHandler.text);
            if (request.result == UnityWebRequest.Result.Success && response != null && response.code == 200 && response.data != null)
            {
                onboardingData = response.data;
                ShowServerOnboarding(onboardingData);
            }
            else
            {
                Debug.LogWarning("服务器新手引导读取失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response)), this);
            }
        }

        onboardingRequestRunning = false;
    }

    private void ShowServerOnboarding(OnboardingCurrentData data)
    {
        if (data == null || !data.available || data.currentStep == null)
        {
            HideServerOnboarding();
            return;
        }

        EnsureOnboardingOverlay();
        if (onboardingOverlay == null) return;

        OnboardingStepData step = data.currentStep;
        onboardingTitle.text = string.IsNullOrEmpty(step.title) ? "新手引导" : step.title;
        onboardingContent.text = string.IsNullOrEmpty(step.content) ? "请按当前提示完成操作。" : step.content;
        onboardingStep.text = step.sequence > 0 ? $"步骤 {step.sequence}" : string.Empty;
        onboardingAckButton.interactable = !onboardingRequestRunning;
        onboardingSkipButton.gameObject.SetActive(data.allowSkip);
        onboardingOverlay.SetActive(true);
        onboardingOverlay.transform.SetAsLastSibling();
    }

    private void EnsureOnboardingOverlay()
    {
        if (onboardingOverlay != null || mainPageTransform == null) return;

        GameObject overlayObject = new GameObject("ServerOnboardingOverlay", typeof(RectTransform), typeof(Image));
        overlayObject.transform.SetParent(mainPageTransform, false);
        onboardingOverlay = overlayObject;
        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image blocker = overlayObject.GetComponent<Image>();
        blocker.color = new Color(0.03f, 0.04f, 0.05f, 0.62f);
        blocker.raycastTarget = true;

        GameObject card = new GameObject("Content", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(overlayObject.transform, false);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(720f, 390f);
        card.GetComponent<Image>().color = new Color(0.97f, 0.92f, 0.81f, 1f);

        onboardingTitle = CreateOnboardingText("Title", card.transform, 30f, TextAlignmentOptions.Center);
        AnchorOnboardingText(onboardingTitle.rectTransform, new Vector2(0f, 0.76f), new Vector2(1f, 0.96f), new Vector2(24f, 0f), new Vector2(-24f, 0f));

        onboardingStep = CreateOnboardingText("Step", card.transform, 16f, TextAlignmentOptions.Right);
        AnchorOnboardingText(onboardingStep.rectTransform, new Vector2(0f, 0.64f), new Vector2(1f, 0.78f), new Vector2(24f, 0f), new Vector2(-24f, 0f));

        onboardingContent = CreateOnboardingText("Content", card.transform, 21f, TextAlignmentOptions.TopLeft);
        onboardingContent.enableWordWrapping = true;
        AnchorOnboardingText(onboardingContent.rectTransform, new Vector2(0f, 0.28f), new Vector2(1f, 0.68f), new Vector2(42f, 0f), new Vector2(-42f, 0f));

        onboardingAckButton = CreateOnboardingButton(card.transform, "AcknowledgeButton", "完成当前步骤", new Vector2(-105f, -135f));
        onboardingAckButton.onClick.AddListener(AcknowledgeServerOnboardingStep);
        onboardingSkipButton = CreateOnboardingButton(card.transform, "SkipButton", "跳过引导", new Vector2(105f, -135f));
        onboardingSkipButton.onClick.AddListener(SkipServerOnboarding);
    }

    private TMP_Text CreateOnboardingText(string objectName, Transform parent, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = RuntimeFontAssetProvider.Resolve();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(0.16f, 0.12f, 0.09f, 1f);
        text.raycastTarget = false;
        return text;
    }

    private Button CreateOnboardingButton(Transform parent, string objectName, string label, Vector2 position)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(190f, 54f);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.34f, 0.54f, 0.65f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        TMP_Text text = CreateOnboardingText("Label", buttonObject.transform, 17f, TextAlignmentOptions.Center);
        text.text = label;
        text.color = Color.white;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private static void AnchorOnboardingText(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private void AcknowledgeServerOnboardingStep()
    {
        if (onboardingData == null || onboardingData.currentStep == null || onboardingRequestRunning) return;
        StartCoroutine(MutateServerOnboarding($"/onboarding/steps/{Uri.EscapeDataString(onboardingData.currentStep.stepCode)}/ack", false));
    }

    private void SkipServerOnboarding()
    {
        if (onboardingData == null || onboardingRequestRunning || !onboardingData.allowSkip) return;
        StartCoroutine(MutateServerOnboarding("/onboarding/skip", true));
    }

    private IEnumerator MutateServerOnboarding(string path, bool skip)
    {
        onboardingRequestRunning = true;
        string json = JsonUtility.ToJson(new OnboardingMutationRequest
        {
            expectedGuideVersion = onboardingData == null ? 0L : onboardingData.guideVersion,
            expectedStateVersion = onboardingData == null ? 0L : onboardingData.stateVersion
        });
        if (!ApiRequestUtility.TryCreateJsonRequest(path, UnityWebRequest.kHttpVerbPOST, json, true, out UnityWebRequest request, out string error))
        {
            onboardingRequestRunning = false;
            SetStatus("引导操作失败：" + error);
            yield break;
        }

        ApiRequestUtility.SetIdempotencyKey(request, Guid.NewGuid().ToString("D"));
        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                onboardingRequestRunning = false;
                yield break;
            }

            OnboardingMutationApiResponse response = ParseJson<OnboardingMutationApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                onboardingRequestRunning = false;
                SetStatus("引导操作失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response)));
                yield break;
            }

            onboardingData.available = response.data.available;
            onboardingData.guideVersion = response.data.guideVersion;
            onboardingData.stateVersion = response.data.stateVersion;
            onboardingData.status = response.data.status;
            onboardingData.currentStep = response.data.currentStep;
            if (skip) SetStatus("已跳过新手引导");
        }

        onboardingRequestRunning = false;
        ShowServerOnboarding(onboardingData);
    }

    private void HideServerOnboarding()
    {
        if (onboardingOverlay != null) onboardingOverlay.SetActive(false);
    }

    [Serializable]
    private sealed class OnboardingCurrentApiResponse : BasicApiResponse
    {
        public OnboardingCurrentData data;
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
        public string presentationParams;
    }

    [Serializable]
    private sealed class OnboardingMutationRequest
    {
        public long expectedGuideVersion;
        public long expectedStateVersion;
    }

    [Serializable]
    private sealed class OnboardingMutationApiResponse : BasicApiResponse
    {
        public OnboardingMutationData data;
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
}
