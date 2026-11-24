using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public partial class GameBoot
{
    private Toggle privacyConsentToggle;
    private Button privacyPolicyButton;
    private TMP_Text privacyConsentLabel;
    private RectTransform privacyControlsHome;
    private GameObject privacyPolicyOverlay;
    private TMP_Text privacyPolicyBody;
    private RectTransform privacyPolicyContentRect;
    private GameObject accountManagementPanel;
    private GameObject realNamePanel;
    private TMP_Text accountManagementStatus;
    private TMP_Text accountManagementCompliance;
    private Button accountConsentButton;
    private Button accountWithdrawButton;
    private Button accountApplyCancellationButton;
    private Button accountCancelCancellationButton;
    private Button accountRefreshButton;
    private Button accountRealNameButton;
    private Button accountReloginButton;
    private TMP_InputField realNameInput;
    private TMP_InputField identityNumberInput;
    private TMP_Text realNameStatus;

    private string currentPolicyVersion;
    private string currentPolicyContent;
    private string currentPolicyContentSha256;
    private string currentPolicyContentUrl;
    private string privacyConsentRequestId;
    private string withdrawalRequestId;
    private string cancellationRequestId;
    private string cancellationRecoveryRequestId;
    private string realNameRequestId;
    private string complianceToken;
    private bool waitingForManagementConsent;
    private bool lastLoginWasWeChat;
    private bool pendingCancellationRecovery;
    private bool privacyPolicyLoaded;
    private bool managementFromWeChat;
    private LoginData managementLoginData;
    private ComplianceData managementCompliance;
    private ConsentData managementConsent;
    private CancellationData managementCancellation;

    private void EnsurePrivacyControls(RectTransform root)
    {
        if (root == null)
        {
            Debug.LogWarning("GameBoot 无法创建隐私授权控件：登录内容根节点为空。");
            return;
        }
        privacyControlsHome = root;

        Transform existingToggle = root.Find("PrivacyConsentToggle");
        if (existingToggle != null)
        {
            privacyConsentToggle = existingToggle.GetComponent<Toggle>();
            Transform existingLabel = existingToggle.Find("Label");
            privacyConsentLabel = existingLabel != null ? existingLabel.GetComponent<TMP_Text>() : null;
        }

        if (privacyConsentToggle == null)
        {
            GameObject toggleObject = new GameObject("PrivacyConsentToggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(root, false);
            privacyConsentToggle = toggleObject.GetComponent<Toggle>();
            SetRect(
                privacyConsentToggle.GetComponent<RectTransform>(),
                CenterAnchor(),
                CenterAnchor(),
                CenterAnchor(),
                new Vector2(-60f, -332f),
                new Vector2(500f, 34f));

            Image box = CreateImage("Box", toggleObject.transform, Color.white);
            SetRect(box.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
            Image checkmark = CreateImage("Checkmark", box.transform, PrimaryColor);
            SetRect(checkmark.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(13f, 13f));
            privacyConsentToggle.targetGraphic = box;
            privacyConsentToggle.graphic = checkmark;
            privacyConsentToggle.isOn = false;

            privacyConsentLabel = CreateComplianceText(
                "Label",
                toggleObject.transform,
                "我已阅读并同意当前隐私协议",
                15,
                FontStyle.Normal,
                FieldTextColor,
                TextAnchor.MiddleLeft);
            SetRect(privacyConsentLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(32f, 0f), new Vector2(-44f, 0f));
        }

        Transform existingPolicyButton = root.Find("PrivacyPolicyButton");
        if (existingPolicyButton != null)
        {
            privacyPolicyButton = existingPolicyButton.GetComponent<Button>();
        }

        if (privacyPolicyButton == null)
        {
            privacyPolicyButton = CreateComplianceButton(
                root,
                "PrivacyPolicyButton",
                "查看隐私协议",
                new Vector2(250f, -332f),
                new Vector2(170f, 38f),
                SecondaryButtonColor,
                FieldTextColor,
                15);
        }

        LoginCanvasStyle.ApplyDialogButton(privacyPolicyButton, false);
        LoginCanvasStyle.ApplyCheckbox(privacyConsentToggle);
        if (privacyConsentToggle.targetGraphic != null)
            privacyConsentToggle.targetGraphic.rectTransform.sizeDelta = new Vector2(28f, 28f);
        if (privacyConsentToggle.graphic != null)
            privacyConsentToggle.graphic.rectTransform.sizeDelta = new Vector2(20f, 20f);
        Image consentHitArea = privacyConsentToggle.GetComponent<Image>();
        if (consentHitArea == null) consentHitArea = privacyConsentToggle.gameObject.AddComponent<Image>();
        consentHitArea.color = Color.clear;
        consentHitArea.raycastTarget = true;
        if (privacyConsentLabel != null)
        {
            privacyConsentLabel.rectTransform.offsetMin = new Vector2(40f, 0f);
            privacyConsentLabel.rectTransform.offsetMax = new Vector2(-4f, 0f);
        }
        privacyPolicyButton.onClick.RemoveListener(ShowPrivacyPolicyOverlay);
        privacyPolicyButton.onClick.AddListener(ShowPrivacyPolicyOverlay);
        EnsureAgeRatingControl(root);
        if (privacyConsentToggle != null)
        {
            privacyConsentToggle.isOn = false;
        }
        PlacePrivacyControlsInLogin(loginDialog != null && loginDialog.activeSelf);
    }

    private void PlacePrivacyControlsInLogin(bool inDialog)
    {
        if (privacyControlsHome == null || privacyConsentToggle == null || privacyPolicyButton == null) return;
        Transform parent = inDialog && loginDialog != null ? loginDialog.transform : privacyControlsHome;
        privacyConsentToggle.transform.SetParent(parent, false);
        privacyPolicyButton.transform.SetParent(parent, false);
        SetRect(privacyConsentToggle.GetComponent<RectTransform>(), CenterAnchor(), CenterAnchor(), CenterAnchor(),
            inDialog ? new Vector2(-116f, -282f) : new Vector2(-84f, -336f),
            inDialog ? new Vector2(412f, 44f) : new Vector2(380f, 44f));
        SetRect(privacyPolicyButton.GetComponent<RectTransform>(), CenterAnchor(), CenterAnchor(), CenterAnchor(),
            inDialog ? new Vector2(250f, -282f) : new Vector2(210f, -336f), new Vector2(180f, 44f));
        if (privacyConsentLabel != null)
        {
            privacyConsentLabel.color = inDialog ? new Color32(88, 66, 47, 255) : new Color32(255, 241, 213, 255);
            privacyConsentLabel.fontSize = 19f;
            privacyConsentLabel.enableAutoSizing = false;
        }
        TMP_Text policyLabel = privacyPolicyButton.GetComponentInChildren<TMP_Text>(true);
        if (policyLabel != null)
        {
            policyLabel.fontSize = 18f;
            policyLabel.color = new Color32(62, 48, 33, 255);
        }
        Text legacyPolicyLabel = privacyPolicyButton.GetComponentInChildren<Text>(true);
        if (legacyPolicyLabel != null)
        {
            legacyPolicyLabel.fontSize = 18;
            legacyPolicyLabel.resizeTextForBestFit = false;
            legacyPolicyLabel.color = new Color32(62, 48, 33, 255);
        }
        if (inDialog && statusText != null)
            SetRect(statusText.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(),
                new Vector2(0f, -324f), new Vector2(650f, 30f));
    }

    private IEnumerator LoadPrivacyPolicies()
    {
        privacyPolicyLoaded = false;
        currentPolicyContent = string.Empty;
        currentPolicyContentSha256 = string.Empty;
        PolicyMetadataResponse metadataResponse = null;
        yield return RequestPolicyMetadata(value => metadataResponse = value);
        if (!IsValidPolicyMetadata(metadataResponse))
        {
            BlockForPolicyFailure("隐私协议读取失败，请稍后重试。");
            yield break;
        }

        currentPolicyVersion = metadataResponse.data.policyVersion;
        currentPolicyContentUrl = metadataResponse.data.contentUrl;
        string metadataContentSha256 = metadataResponse.data.contentSha256;

        PolicyContentResponse contentResponse = null;
        yield return RequestPolicyContent(value => contentResponse = value);
        if (!IsValidPolicyContent(contentResponse)
            || !string.Equals(currentPolicyVersion, contentResponse.data.policyVersion, StringComparison.Ordinal))
        {
            BlockForPolicyFailure("隐私协议版本校验失败，请稍后重试。");
            yield break;
        }

        currentPolicyContent = contentResponse.data.content ?? string.Empty;
        currentPolicyContentSha256 = contentResponse.data.contentSha256;
        if (!string.IsNullOrWhiteSpace(metadataContentSha256)
            && !string.IsNullOrWhiteSpace(currentPolicyContentSha256)
            && !string.Equals(metadataContentSha256, currentPolicyContentSha256, StringComparison.OrdinalIgnoreCase))
        {
            BlockForPolicyFailure("隐私协议内容校验失败，请稍后重试。");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(currentPolicyContentSha256))
        {
            currentPolicyContentSha256 = metadataContentSha256;
        }

        if (!string.IsNullOrWhiteSpace(currentPolicyContentSha256)
            && !string.Equals(
                ComputeSha256(currentPolicyContent),
                currentPolicyContentSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            BlockForPolicyFailure("隐私协议完整性校验失败，请稍后重试。");
            yield break;
        }

        privacyPolicyLoaded = true;
        if (privacyConsentLabel != null)
        {
            privacyConsentLabel.text = "我已阅读并同意《隐私协议》";
        }
        RefreshPrivacyPolicyOverlayContent();
    }

    private IEnumerator RequestPolicyMetadata(Action<PolicyMetadataResponse> completed)
    {
        if (!ApiRequestUtility.TryCreateGet(
                "/privacy/policy/current",
                false,
                out UnityWebRequest request,
                out string error))
        {
            Debug.LogWarning("隐私协议元数据请求未创建：" + error);
            completed(null);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            PolicyMetadataResponse response = ParseResponse<PolicyMetadataResponse>(request);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200)
            {
                completed(null);
                yield break;
            }

            completed(response);
        }
    }

    private IEnumerator RequestPolicyContent(Action<PolicyContentResponse> completed)
    {
        if (!ApiRequestUtility.TryCreateGet(
                "/privacy/policy/current/content",
                false,
                out UnityWebRequest request,
                out string error))
        {
            Debug.LogWarning("隐私协议正文请求未创建：" + error);
            completed(null);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            PolicyContentResponse response = ParseResponse<PolicyContentResponse>(request);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200)
            {
                completed(null);
                yield break;
            }

            completed(response);
        }
    }

    private void BlockForPolicyFailure(string message)
    {
        runtimePolicyBlocked = true;
        SetBusyState(false, message);
        ShowRuntimePolicyOverlay("隐私协议不可用", message, currentPolicyContentUrl);
    }

    private void ShowPrivacyPolicyOverlay()
    {
        if (privacyAgreementView != null)
        {
            RefreshPrivacyPolicyOverlayContent();
            privacyAgreementView.Show();
            return;
        }

        RectTransform root = loginCanvasView != null ? loginCanvasView.PrepareContentRoot() : null;
        if (root == null)
        {
            ShowError("隐私协议界面创建失败");
            return;
        }

        privacyAgreementView = LoginAgreementPanel.Create(root, "PrivacyPolicyOverlay", "隐私协议", "同意并继续",
            AgreePrivacyPolicyFromOverlay, HidePrivacyPolicyOverlay);
        privacyPolicyOverlay = privacyAgreementView.gameObject;
        privacyPolicyBody = privacyAgreementView.Body;
        privacyPolicyContentRect = privacyAgreementView.Content;
        RefreshPrivacyPolicyOverlayContent();
        privacyAgreementView.Show();
    }

    private void RefreshPrivacyPolicyOverlayContent()
    {
        if (privacyAgreementView == null) return;
        privacyAgreementView.SetBody(string.IsNullOrEmpty(currentPolicyContent)
            ? "暂未获取隐私协议正文。"
            : currentPolicyContent, false);
        privacyAgreementView.ConfirmButton.interactable = privacyPolicyLoaded;
    }

    private void HidePrivacyPolicyOverlay()
    {
        if (privacyPolicyOverlay != null)
        {
            privacyPolicyOverlay.SetActive(false);
        }
    }

    private void AgreePrivacyPolicyFromOverlay()
    {
        if (!privacyPolicyLoaded) return;
        if (privacyConsentToggle != null)
        {
            privacyConsentToggle.isOn = true;
        }

        HidePrivacyPolicyOverlay();
        if (requestRunning)
        {
            return;
        }

        if (accountManagementPanel != null && accountManagementPanel.activeSelf)
        {
            SetManagementStatus("已确认隐私协议，请再次点击同意授权。", MutedTextColor);
            return;
        }

        SetStatus("已同意隐私协议，请重新点击登录。", MutedTextColor);
    }

    private void OpenAccountManagementScreen(LoginData data, bool fromWeChat)
    {
        managementLoginData = data;
        managementCompliance = data.compliance;
        managementFromWeChat = fromWeChat;
        complianceToken = data.token;
        lastLoginWasWeChat = fromWeChat;
        pendingCancellationRecovery = false;
        waitingForManagementConsent = false;
        withdrawalRequestId = string.Empty;
        cancellationRequestId = string.Empty;
        cancellationRecoveryRequestId = string.Empty;
        realNameRequestId = string.Empty;
        SetBusyState(false, string.Empty);

        if (accountManagementPanel != null)
        {
            Destroy(accountManagementPanel);
        }

        RectTransform root = loginCanvasView != null ? loginCanvasView.PrepareContentRoot() : null;
        if (root == null)
        {
            ShowError("账号管理界面创建失败");
            return;
        }

        Image overlay = CreateImage("AccountManagementPanel", root, new Color(0.08f, 0.05f, 0.03f, 0.76f));
        accountManagementPanel = overlay.gameObject;
        Stretch(overlay.rectTransform);

        Image card = CreateImage("AccountManagementCard", overlay.transform, new Color(0.98f, 0.95f, 0.86f));
        SetRect(card.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), Vector2.zero, new Vector2(900f, 900f));
        Outline cardOutline = card.gameObject.AddComponent<Outline>();
        cardOutline.effectColor = new Color(0.47f, 0.31f, 0.18f, 0.8f);
        cardOutline.effectDistance = new Vector2(2f, -2f);
        LoginCanvasStyle.ApplyDialogPaper(card);
        TMP_Text title = CreateComplianceText("Title", card.transform, "账号管理", 32, FontStyle.Bold, new Color(0.28f, 0.17f, 0.10f), TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(-70f, 54f));

        TMP_Text description = CreateComplianceText(
            "Description",
            card.transform,
            "当前账号只能进行隐私授权、实名和注销管理，完成准入后需要重新登录。",
            20,
            FontStyle.Normal,
            MutedTextColor,
            TextAnchor.MiddleCenter);
        SetRect(description.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(-90f, 48f));

        Image viewport = CreateImage("ComplianceViewport", card.transform, new Color(0.82f, 0.71f, 0.52f, 0.18f));
        SetRect(viewport.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-10f, -146f), new Vector2(-110f, 330f));
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = new GameObject("ComplianceContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
        content.SetParent(viewport.transform, false);
        SetRect(content, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 14, 14);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        accountManagementCompliance = CreateComplianceText("Compliance", content, string.Empty, 22, FontStyle.Normal, FieldTextColor, TextAnchor.UpperLeft);
        accountManagementCompliance.enableWordWrapping = true;
        accountManagementCompliance.overflowMode = TextOverflowModes.Overflow;
        accountManagementCompliance.lineSpacing = 8f;
        accountManagementCompliance.richText = false;
        ScrollRect complianceScroll = viewport.gameObject.AddComponent<ScrollRect>();
        complianceScroll.viewport = viewport.rectTransform;
        complianceScroll.content = content;
        complianceScroll.horizontal = false;
        complianceScroll.movementType = ScrollRect.MovementType.Clamped;
        complianceScroll.scrollSensitivity = 35f;
        Image scrollRail = CreateImage("ComplianceScrollbar", card.transform, new Color(0.61f, 0.45f, 0.28f, 0.25f));
        SetRect(scrollRail.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -146f), new Vector2(12f, 330f));
        Scrollbar scrollBar = scrollRail.gameObject.AddComponent<Scrollbar>();
        Image scrollHandle = CreateImage("Handle", scrollRail.transform, new Color(0.57f, 0.38f, 0.21f, 0.85f));
        Stretch(scrollHandle.rectTransform);
        scrollBar.handleRect = scrollHandle.rectTransform;
        scrollBar.targetGraphic = scrollHandle;
        scrollBar.direction = Scrollbar.Direction.BottomToTop;
        complianceScroll.verticalScrollbar = scrollBar;
        complianceScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        accountManagementStatus = CreateComplianceText("Status", card.transform, string.Empty, 20, FontStyle.Normal, ErrorColor, TextAnchor.MiddleCenter);
        accountManagementStatus.enableAutoSizing = true;
        accountManagementStatus.fontSizeMin = 18f;
        accountManagementStatus.fontSizeMax = 20f;
        accountManagementStatus.overflowMode = TextOverflowModes.Ellipsis;
        SetRect(accountManagementStatus.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -492f), new Vector2(-90f, 78f));

        accountConsentButton = CreateComplianceButton(card.transform, "ConsentButton", "同意隐私协议", new Vector2(-250f, -178f), new Vector2(230f, 58f), PrimaryColor, Color.white, 22);
        accountConsentButton.onClick.AddListener(BeginPrivacyConsent);
        accountWithdrawButton = CreateComplianceButton(card.transform, "WithdrawButton", "撤回隐私授权", new Vector2(0f, -178f), new Vector2(230f, 58f), SecondaryButtonColor, FieldTextColor, 22);
        accountWithdrawButton.onClick.AddListener(BeginWithdrawConsent);
        accountRefreshButton = CreateComplianceButton(card.transform, "RefreshButton", "刷新状态", new Vector2(250f, -178f), new Vector2(230f, 58f), SecondaryButtonColor, FieldTextColor, 22);
        accountRefreshButton.onClick.AddListener(() => StartCoroutine(RefreshManagementState()));

        accountApplyCancellationButton = CreateComplianceButton(card.transform, "ApplyCancellationButton", "申请注销", new Vector2(-250f, -256f), new Vector2(230f, 58f), new Color(0.70f, 0.25f, 0.20f, 1f), Color.white, 22);
        accountApplyCancellationButton.onClick.AddListener(BeginAccountCancellation);
        accountCancelCancellationButton = CreateComplianceButton(card.transform, "CancelCancellationButton", "撤销注销", new Vector2(0f, -256f), new Vector2(230f, 58f), SecondaryButtonColor, FieldTextColor, 22);
        accountCancelCancellationButton.onClick.AddListener(BeginCancelAccountCancellation);
        accountRealNameButton = CreateComplianceButton(card.transform, "RealNameButton", "实名认证", new Vector2(250f, -256f), new Vector2(230f, 58f), PrimaryColor, Color.white, 22);
        accountRealNameButton.onClick.AddListener(ShowRealNamePanel);

        accountReloginButton = CreateComplianceButton(card.transform, "ReloginButton", "重新登录", new Vector2(-130f, -358f), new Vector2(240f, 64f), PrimaryColor, Color.white, 24);
        accountReloginButton.onClick.AddListener(ReloginAfterCompliance);
        Button closeButton = CreateComplianceButton(card.transform, "CloseButton", "返回", new Vector2(130f, -358f), new Vector2(240f, 64f), SecondaryButtonColor, FieldTextColor, 24);
        closeButton.onClick.AddListener(CloseAccountManagementPanel);

        accountManagementPanel.transform.SetAsLastSibling();
        StartCoroutine(RefreshManagementState());
        if (managementCompliance != null
            && (string.Equals(managementCompliance.verificationStatus, "UNVERIFIED", StringComparison.OrdinalIgnoreCase)
                || string.Equals(managementCompliance.verificationStatus, "REJECTED", StringComparison.OrdinalIgnoreCase)))
        {
            ShowRealNamePanel();
        }
    }

    private IEnumerator RefreshManagementState()
    {
        if (string.IsNullOrEmpty(complianceToken))
        {
            yield break;
        }

        SetManagementStatus("正在刷新账号合规状态...", MutedTextColor);
        yield return RequestConsentStatus();
        yield return RequestCancellationStatus();
        yield return RequestComplianceStatus();
        UpdateManagementView();
    }

    private IEnumerator RequestConsentStatus()
    {
        if (!ApiRequestUtility.TryCreateGetWithToken(
                "/user/privacy/consent",
                complianceToken,
                out UnityWebRequest request,
                out string error))
        {
            SetManagementStatus(error, ErrorColor);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            ConsentResponse response = ParseResponse<ConsentResponse>(request);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200)
            {
                SetManagementStatus(ApiRequestUtility.GetErrorMessage(request, "隐私授权状态读取失败"), ErrorColor);
                yield break;
            }

            managementConsent = response.data;
        }
    }

    private IEnumerator RequestCancellationStatus()
    {
        if (!ApiRequestUtility.TryCreateGetWithToken(
                "/user/privacy/account-cancellation",
                complianceToken,
                out UnityWebRequest request,
                out string error))
        {
            SetManagementStatus(error, ErrorColor);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            CancellationResponse response = ParseResponse<CancellationResponse>(request);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200)
            {
                SetManagementStatus(ApiRequestUtility.GetErrorMessage(request, "账号注销状态读取失败"), ErrorColor);
                yield break;
            }

            managementCancellation = response.data;
        }
    }

    private IEnumerator RequestComplianceStatus()
    {
        if (!ApiRequestUtility.TryCreateGetWithToken(
                "/anti-addiction/status",
                complianceToken,
                out UnityWebRequest request,
                out string error))
        {
            SetManagementStatus(error, ErrorColor);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            ComplianceResponse response = ParseResponse<ComplianceResponse>(request);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200)
            {
                SetManagementStatus(ApiRequestUtility.GetErrorMessage(request, "实名及防沉迷状态读取失败"), ErrorColor);
                yield break;
            }

            managementCompliance = response.data;
        }
    }

    private void UpdateManagementView()
    {
        if (accountManagementCompliance != null)
        {
            accountManagementCompliance.text = BuildComplianceSummary(managementCompliance);
            Canvas.ForceUpdateCanvases();
            ScrollRect complianceScroll = accountManagementCompliance.GetComponentInParent<ScrollRect>();
            if (complianceScroll != null && complianceScroll.verticalScrollbar != null)
            {
                float viewportHeight = complianceScroll.viewport.rect.height;
                float contentHeight = complianceScroll.content.rect.height;
                complianceScroll.verticalScrollbar.size = Mathf.Clamp01(viewportHeight / Mathf.Max(viewportHeight, contentHeight));
                complianceScroll.verticalScrollbar.SetValueWithoutNotify(1f);
            }
        }

        if (accountConsentButton != null)
        {
            accountConsentButton.interactable = privacyPolicyLoaded
                && managementConsent != null
                && !managementConsent.consentValid;
        }

        if (accountWithdrawButton != null)
        {
            accountWithdrawButton.interactable = managementConsent != null
                && managementConsent.consentValid;
        }

        if (accountApplyCancellationButton != null)
        {
            accountApplyCancellationButton.interactable = managementCancellation == null
                || !managementCancellation.cancellable;
        }

        if (accountCancelCancellationButton != null)
        {
            accountCancelCancellationButton.interactable = managementCancellation != null
                && managementCancellation.cancellable;
        }

        if (accountRealNameButton != null)
        {
            accountRealNameButton.interactable = !string.IsNullOrEmpty(complianceToken);
        }
    }

    private string BuildComplianceSummary(ComplianceData compliance)
    {
        if (compliance == null)
        {
            return "暂未获取实名和防沉迷状态。";
        }

        string access = compliance.gameAccessAllowed ? "允许进入游戏" : "暂不允许进入游戏";
        string reason = string.IsNullOrEmpty(compliance.blockReason) ? "NONE" : compliance.blockReason;
        string consentSummary = managementConsent == null
            ? "未读取"
            : (managementConsent.consentValid ? "有效" : "无效")
                + "（协议版本 " + (managementConsent.currentPolicyVersion ?? "待确认") + "）";
        string latestConsent = managementConsent == null || string.IsNullOrEmpty(managementConsent.latestAction)
            ? "暂无授权记录"
            : FormatComplianceDisplay(managementConsent.latestAction)
                + " / "
                + (string.IsNullOrEmpty(managementConsent.latestPolicyVersion)
                    ? "版本待确认"
                    : managementConsent.latestPolicyVersion)
                + " / "
                + (string.IsNullOrEmpty(managementConsent.latestOccurredTime)
                    ? "时间未知"
                    : managementConsent.latestOccurredTime);
        return "实名状态：" + FormatComplianceDisplay(compliance.verificationStatus)
            + "\n年龄范围：" + FormatComplianceDisplay(compliance.ageGroup)
            + "\n游戏权限：" + access
            + "\n限制原因：" + FormatComplianceDisplay(reason)
            + (string.IsNullOrEmpty(compliance.message) ? string.Empty : "\n提示：" + compliance.message)
            + "\n剩余时长：" + Mathf.Max(0, compliance.remainingSeconds) + " 秒"
            + "\n隐私授权：" + consentSummary
            + "\n最近授权记录：" + latestConsent;
    }

    private static string FormatComplianceDisplay(string value)
    {
        switch ((value ?? string.Empty).ToUpperInvariant())
        {
            case "":
            case "UNKNOWN": return "待确认";
            case "VERIFIED": return "已认证";
            case "UNVERIFIED": return "未认证";
            case "PENDING": return "审核中";
            case "REJECTED": return "认证未通过";
            case "AGE_16_TO_17": return "16至17岁";
            case "AGE_8_TO_15": return "8至15岁";
            case "UNDER_8": return "8岁以下";
            case "ADULT": return "成年人";
            case "MINOR": return "未成年人";
            case "NONE": return "无";
            case "CONSENT":
            case "GRANTED": return "已授权";
            case "WITHDRAW":
            case "WITHDRAWN": return "已撤回";
            default: return FrontendTextLocalization.DisplayStatus(value);
        }
    }

    private void BeginPrivacyConsent()
    {
        if (privacyConsentToggle == null || !privacyConsentToggle.isOn)
        {
            SetManagementStatus("请先阅读隐私协议并确认同意。", ErrorColor);
            ShowPrivacyPolicyOverlay();
            return;
        }

        if (!privacyPolicyLoaded || string.IsNullOrEmpty(currentPolicyVersion) || string.IsNullOrEmpty(complianceToken))
        {
            SetManagementStatus("当前隐私协议不可用，暂时无法授权。", ErrorColor);
            return;
        }

        privacyConsentRequestId = NewRequestId();
        waitingForManagementConsent = true;
        SetBusyState(true, "正在提交隐私授权...");
        if (managementFromWeChat && WeChatPlatformBridge.IsAvailable)
        {
            WeChatPlatformBridge.RequestPrivacyAuthorization(this);
        }
        else
        {
            StartCoroutine(SendPrivacyConsent());
        }
    }

    private IEnumerator SendPrivacyConsent()
    {
        waitingForManagementConsent = false;
        PrivacyConsentRequest payload = new PrivacyConsentRequest
        {
            policyVersion = currentPolicyVersion,
            requestId = privacyConsentRequestId,
            clientVersion = Application.version
        };

        if (!ApiRequestUtility.TryCreateJsonRequestWithToken(
                "/user/privacy/consent",
                UnityWebRequest.kHttpVerbPOST,
                JsonUtility.ToJson(payload),
                complianceToken,
                out UnityWebRequest request,
                out string error))
        {
            SetBusyState(false, error);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            BasicResponse response = ParseResponse<BasicResponse>(request);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200)
            {
                if (response != null && response.code == 7703)
                {
                    privacyConsentRequestId = NewRequestId();
                    SetBusyState(false, "隐私协议已更新，请重新阅读并确认。");
                    yield return LoadPrivacyPolicies();
                    if (!runtimePolicyBlocked)
                    {
                        ShowPrivacyPolicyOverlay();
                    }
                    yield break;
                }

                SetBusyState(false, ApiRequestUtility.GetErrorMessage(request, "隐私授权失败"));
                yield break;
            }

            SetBusyState(false, "隐私授权成功，请重新登录获取完整权限。" );
            if (accountManagementStatus != null)
            {
                accountManagementStatus.text = "隐私授权成功，请点击重新登录。";
            }
            if (accountConsentButton != null)
            {
                accountConsentButton.interactable = false;
            }
        }
    }

    private void BeginWithdrawConsent()
    {
        if (string.IsNullOrEmpty(withdrawalRequestId))
        {
            withdrawalRequestId = NewRequestId();
        }

        StartCoroutine(SendWithdrawConsent());
    }

    private IEnumerator SendWithdrawConsent()
    {
        PrivacyWithdrawRequest payload = new PrivacyWithdrawRequest
        {
            requestId = withdrawalRequestId,
            clientVersion = Application.version
        };

        if (!ApiRequestUtility.TryCreateJsonRequestWithToken(
                "/user/privacy/consent/withdraw",
                UnityWebRequest.kHttpVerbPOST,
                JsonUtility.ToJson(payload),
                complianceToken,
                out UnityWebRequest request,
                out string error))
        {
            SetManagementStatus(error, ErrorColor);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            BasicResponse response = ParseResponse<BasicResponse>(request);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200)
            {
                SetManagementStatus(ApiRequestUtility.GetErrorMessage(request, "撤回隐私授权失败"), ErrorColor);
                yield break;
            }

            SetManagementStatus("隐私授权已撤回，普通游戏权限已关闭。", ErrorColor);
            StartCoroutine(RefreshManagementState());
        }
    }

    private void BeginAccountCancellation()
    {
        if (string.IsNullOrEmpty(cancellationRequestId))
        {
            cancellationRequestId = NewRequestId();
        }

        StartCoroutine(SendAccountCancellation());
    }

    private IEnumerator SendAccountCancellation()
    {
        AccountCancellationRequest payload = new AccountCancellationRequest
        {
            requestId = cancellationRequestId,
            reason = string.Empty
        };

        if (!ApiRequestUtility.TryCreateJsonRequestWithToken(
                "/user/privacy/account-cancellation",
                UnityWebRequest.kHttpVerbPOST,
                JsonUtility.ToJson(payload),
                complianceToken,
                out UnityWebRequest request,
                out string error))
        {
            SetManagementStatus(error, ErrorColor);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            CancellationResponse response = ParseResponse<CancellationResponse>(request);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200)
            {
                SetManagementStatus(ApiRequestUtility.GetErrorMessage(request, "申请账号注销失败"), ErrorColor);
                yield break;
            }

            managementCancellation = response.data;
            SetManagementStatus("账号已进入注销冷静期。", ErrorColor);
            UpdateManagementView();
        }
    }

    private void BeginCancelAccountCancellation()
    {
        if (string.IsNullOrEmpty(cancellationRecoveryRequestId))
        {
            cancellationRecoveryRequestId = NewRequestId();
        }

        StartCoroutine(SendCancelAccountCancellation());
    }

    private IEnumerator SendCancelAccountCancellation()
    {
        CancellationCancelRequest payload = new CancellationCancelRequest
        {
            requestId = cancellationRecoveryRequestId
        };

        if (!ApiRequestUtility.TryCreateJsonRequestWithToken(
                "/user/privacy/account-cancellation/cancel",
                UnityWebRequest.kHttpVerbPOST,
                JsonUtility.ToJson(payload),
                complianceToken,
                out UnityWebRequest request,
                out string error))
        {
            SetManagementStatus(error, ErrorColor);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            CancellationResponse response = ParseResponse<CancellationResponse>(request);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200)
            {
                SetManagementStatus(ApiRequestUtility.GetErrorMessage(request, "撤销账号注销失败"), ErrorColor);
                yield break;
            }

            managementCancellation = response.data;
            SetManagementStatus("注销申请已撤销，请重新登录。", MutedTextColor);
            UpdateManagementView();
        }
    }

    private void ShowRealNamePanel()
    {
        if (realNamePanel != null)
        {
            if (accountManagementPanel != null) accountManagementPanel.SetActive(false);
            realNameView.Show();
            return;
        }

        if (accountManagementPanel == null)
        {
            return;
        }

        realNameView = LoginAgreementPanel.Create(accountManagementPanel.transform.parent, "RealNamePanel", "实名认证", "提交认证",
            SubmitRealName, HideRealNamePanel);
        realNamePanel = realNameView.gameObject;
        realNameView.ConfigureForm();
        realNameView.SetBody(ReadAgreementText("RealName"));
        realNameInput = realNameView.AddInput("RealNameInput", "真实姓名：", "请输入您的真实姓名", 0.385f, 64);
        identityNumberInput = realNameView.AddInput("IdentityNumberInput", "身份证号：", "请输入您的居民身份证号码", 0.295f, 32);
        realNameStatus = realNameView.AddStatus();
        accountManagementPanel.SetActive(false);
        realNameView.Show();
    }

    private void SubmitRealName()
    {
        string realName = realNameInput == null ? string.Empty : realNameInput.text.Trim();
        string identityNumber = identityNumberInput == null ? string.Empty : identityNumberInput.text.Trim();
        if (string.IsNullOrEmpty(realName) || realName.Length > 64)
        {
            SetRealNameStatus("请输入有效的真实姓名。", ErrorColor);
            return;
        }

        if (string.IsNullOrEmpty(identityNumber) || identityNumber.Length > 32)
        {
            SetRealNameStatus("请输入有效的证件号码。", ErrorColor);
            return;
        }

        realNameRequestId = NewRequestId();
        realNameInput.text = string.Empty;
        identityNumberInput.text = string.Empty;
        SetRealNameStatus("正在提交实名认证...", MutedTextColor);
        StartCoroutine(SendRealName(realName, identityNumber, realNameRequestId));
    }

    private IEnumerator SendRealName(string realName, string identityNumber, string requestId)
    {
        RealNameRequest payload = new RealNameRequest
        {
            requestId = requestId,
            realName = realName,
            identityNumber = identityNumber
        };

        if (!ApiRequestUtility.TryCreateJsonRequestWithToken(
                "/anti-addiction/real-name",
                UnityWebRequest.kHttpVerbPOST,
                JsonUtility.ToJson(payload),
                complianceToken,
                out UnityWebRequest request,
                out string error))
        {
            SetRealNameStatus(error, ErrorColor);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            RealNameResponse response = ParseResponse<RealNameResponse>(request);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200)
            {
                SetRealNameStatus(ApiRequestUtility.GetErrorMessage(request, "实名认证失败，请重新提交"), ErrorColor);
                yield break;
            }

            HandleRealNameResult(response.data);
        }
    }

    private void HideRealNamePanel()
    {
        if (realNamePanel != null)
        {
            if (realNameInput != null) realNameInput.text = string.Empty;
            if (identityNumberInput != null) identityNumberInput.text = string.Empty;
            realNamePanel.SetActive(false);
        }
        if (accountManagementPanel != null) accountManagementPanel.SetActive(true);
    }

    private void CloseRealNamePanel()
    {
        if (realNamePanel != null)
        {
            realNamePanel.SetActive(false);
            Destroy(realNamePanel);
            realNamePanel = null;
            realNameView = null;
        }
    }

    private void ReloginAfterCompliance()
    {
        complianceToken = string.Empty;
        CloseRealNamePanel();
        CloseAccountManagementPanel();
        pendingCancellationRecovery = false;
        SetBusyState(false, "请重新登录。");
        if (lastLoginWasWeChat)
        {
            TryWeChatLogin();
        }
        else
        {
            TryLogin();
        }
    }

    private void CloseAccountManagementPanel()
    {
        CloseRealNamePanel();
        if (accountManagementPanel != null)
        {
            accountManagementPanel.SetActive(false);
            Destroy(accountManagementPanel);
            accountManagementPanel = null;
        }

        complianceToken = string.Empty;
        managementLoginData = null;
        managementConsent = null;
        managementCancellation = null;
        managementCompliance = null;
    }

    private TMP_Text CreateComplianceText(
        string name,
        Transform parent,
        string value,
        int fontSize,
        FontStyle fontStyle,
        Color color,
        TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.font = RuntimeFontAssetProvider.Resolve();
        text.text = FrontendTextLocalization.Normalize(value);
        text.fontSize = fontSize;
        text.fontStyle = fontStyle == FontStyle.Bold
            ? FontStyles.Bold
            : fontStyle == FontStyle.Italic ? FontStyles.Italic : FontStyles.Normal;
        text.color = color;
        text.alignment = ResolveComplianceAlignment(alignment);
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateComplianceButton(
        Transform parent,
        string name,
        string value,
        Vector2 position,
        Vector2 size,
        Color color,
        Color textColor,
        int fontSize)
    {
        Image image = CreateImage(name, parent, color);
        SetRect(image.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), position, size);
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        TMP_Text label = CreateComplianceText("Text", image.transform, value, fontSize, FontStyle.Normal, textColor, TextAnchor.MiddleCenter);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;
        bool primary = name == "ConsentButton" || name == "RealNameButton" || name == "ReloginButton" || name == "ApplyCancellationButton";
        LoginCanvasStyle.ApplyDialogButton(button, primary, name == "ApplyCancellationButton");
        return button;
    }

    private TMP_InputField CreateComplianceInput(Transform parent, string name, string placeholder)
    {
        Image frame = CreateImage(name, parent, FieldColor);
        frame.raycastTarget = true;
        TMP_InputField input = frame.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = frame;
        input.lineType = TMP_InputField.LineType.SingleLine;

        TMP_Text text = CreateComplianceText("Text", frame.transform, string.Empty, 14, FontStyle.Normal, FieldTextColor, TextAnchor.MiddleLeft);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(16f, 0f);
        text.rectTransform.offsetMax = new Vector2(-16f, 0f);
        input.textComponent = text;

        TMP_Text placeholderText = CreateComplianceText("Placeholder", frame.transform, placeholder, 12, FontStyle.Normal, new Color(0.56f, 0.61f, 0.68f, 1f), TextAnchor.MiddleLeft);
        placeholderText.rectTransform.anchorMin = Vector2.zero;
        placeholderText.rectTransform.anchorMax = Vector2.one;
        placeholderText.rectTransform.offsetMin = new Vector2(16f, 0f);
        placeholderText.rectTransform.offsetMax = new Vector2(-16f, 0f);
        input.placeholder = placeholderText;
        return input;
    }

    private static TextAlignmentOptions ResolveComplianceAlignment(TextAnchor alignment)
    {
        switch (alignment)
        {
            case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft: return TextAlignmentOptions.MidlineLeft;
            case TextAnchor.MiddleRight: return TextAlignmentOptions.MidlineRight;
            case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
            default: return TextAlignmentOptions.Center;
        }
    }

    private void ClearClosedAccountLocalState()
    {
        complianceToken = string.Empty;
        GameSession.Clear();
        string[] accountStateKeys =
        {
            "LoginUsername",
            "PlayerNickname",
            "PlayerAvatarUrl",
            "PlayerAvatarSource",
            "PlayerAvatarCode",
            "PlayerAvatarVersion",
            "PlayerAvatarUpdatedTime",
            "PlayerAvatarResourcePath",
            "AccountStatus",
            "AccountManagementOnly",
            "PrivacyConsentRequired",
            "PrivacyPolicyVersion",
            "PlayerTotalExperience",
            "PlayerLevelCurveRevision",
            "PlayerCurrentLevelExperience",
            "PlayerRequiredExperience",
            "PlayerExperienceProgressBps",
            "PlayerMaxLevel",
            "PlayerEconomyFrozen",
            "PlayerLv",
            "Gold",
            "Wood",
            "Stone",
            "Iron",
            "Crystal",
            "SoulCrystal"
        };
        for (int index = 0; index < accountStateKeys.Length; index++)
        {
            PlayerPrefs.DeleteKey(accountStateKeys[index]);
        }

        PlayerPrefs.Save();
    }

    private void SetManagementStatus(string message, Color color)
    {
        if (accountManagementStatus != null)
        {
            accountManagementStatus.color = color;
            accountManagementStatus.text = string.IsNullOrWhiteSpace(message) ? string.Empty : FrontendTextLocalization.NormalizePlayerMessage(message);
        }
    }

    private void SetRealNameStatus(string message, Color color)
    {
        if (realNameStatus != null)
        {
            realNameStatus.color = color;
            realNameStatus.text = string.IsNullOrWhiteSpace(message) ? string.Empty : FrontendTextLocalization.NormalizePlayerMessage(message);
        }
    }

    private static string NewRequestId()
    {
        return Guid.NewGuid().ToString("D").ToLowerInvariant();
    }

    private static string ComputeSha256(string value)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            for (int index = 0; index < bytes.Length; index++)
            {
                builder.Append(bytes[index].ToString("x2"));
            }

            return builder.ToString();
        }
    }

    private static bool IsValidPolicyMetadata(PolicyMetadataResponse response)
    {
        return response != null
            && response.data != null
            && !string.IsNullOrWhiteSpace(response.data.policyVersion);
    }

    private static bool IsValidPolicyContent(PolicyContentResponse response)
    {
        return response != null
            && response.data != null
            && response.data.content != null;
    }

    private static T ParseResponse<T>(UnityWebRequest request) where T : class
    {
        if (request == null || request.downloadHandler == null || string.IsNullOrWhiteSpace(request.downloadHandler.text))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<T>(request.downloadHandler.text);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("合规接口响应解析失败：" + exception.Message);
            return null;
        }
    }

    [Serializable]
    private class BasicResponse
    {
        public int code;
        public string message;
    }

    [Serializable]
    private class PolicyMetadataResponse : BasicResponse
    {
        public PolicyMetadataData data;
    }

    [Serializable]
    private class PolicyMetadataData
    {
        public string policyVersion;
        public string title;
        public string summary;
        public string contentUrl;
        public string contentSha256;
        public string operatorName;
        public string contactEmail;
        public string effectiveTime;
        public bool requiresReconsent;
    }

    [Serializable]
    private class PolicyContentResponse : BasicResponse
    {
        public PolicyContentData data;
    }

    [Serializable]
    private class PolicyContentData
    {
        public string policyVersion;
        public string content;
        public string contentSha256;
    }

    [Serializable]
    private class PrivacyConsentRequest
    {
        public string policyVersion;
        public string requestId;
        public string clientVersion;
    }

    [Serializable]
    private class PrivacyWithdrawRequest
    {
        public string requestId;
        public string clientVersion;
    }

    [Serializable]
    private class ConsentResponse : BasicResponse
    {
        public ConsentData data;
    }

    [Serializable]
    private class ConsentData
    {
        public string accountStatus;
        public string currentPolicyVersion;
        public bool consentValid;
        public string latestAction;
        public string latestPolicyVersion;
        public string latestOccurredTime;
    }

    [Serializable]
    private class CancellationResponse : BasicResponse
    {
        public CancellationData data;
    }

    [Serializable]
    private class CancellationData
    {
        public long cancellationId;
        public string accountStatus;
        public string cancellationStatus;
        public string requestedTime;
        public string scheduledTime;
        public string cancelledTime;
        public string completedTime;
        public bool cancellable;
    }

    [Serializable]
    private class ComplianceResponse : BasicResponse
    {
        public ComplianceData data;
    }

    [Serializable]
    private class AccountCancellationRequest
    {
        public string requestId;
        public string reason;
    }

    [Serializable]
    private class CancellationCancelRequest
    {
        public string requestId;
    }

    [Serializable]
    private class RealNameRequest
    {
        public string requestId;
        public string realName;
        public string identityNumber;
    }

    [Serializable]
    private class RealNameResponse : BasicResponse
    {
        public RealNameResult data;
    }

    [Serializable]
    private class RealNameResult
    {
        public bool verified;
        public bool reloginRequired;
        public ComplianceData compliance;
    }
}
