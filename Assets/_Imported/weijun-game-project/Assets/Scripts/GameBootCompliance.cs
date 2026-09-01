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
    private GameObject privacyPolicyOverlay;
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

        Transform existingToggle = root.Find("PrivacyConsentToggle");
        if (existingToggle != null)
        {
            privacyConsentToggle = existingToggle.GetComponent<Toggle>();
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
                "我已阅读并同意当前隐私政策",
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
                "查看隐私政策",
                new Vector2(250f, -332f),
                new Vector2(170f, 38f),
                SecondaryButtonColor,
                FieldTextColor,
                15);
        }

        privacyPolicyButton.onClick.RemoveListener(ShowPrivacyPolicyOverlay);
        privacyPolicyButton.onClick.AddListener(ShowPrivacyPolicyOverlay);
        if (privacyConsentToggle != null)
        {
            privacyConsentToggle.isOn = false;
        }
    }

    private IEnumerator LoadPrivacyPolicies()
    {
        privacyPolicyLoaded = false;
        PolicyMetadataResponse metadataResponse = null;
        yield return RequestPolicyMetadata(value => metadataResponse = value);
        if (!IsValidPolicyMetadata(metadataResponse))
        {
            BlockForPolicyFailure("隐私政策读取失败，请稍后重试。");
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
            BlockForPolicyFailure("隐私政策版本校验失败，请稍后重试。");
            yield break;
        }

        currentPolicyContent = contentResponse.data.content ?? string.Empty;
        currentPolicyContentSha256 = contentResponse.data.contentSha256;
        if (!string.IsNullOrWhiteSpace(metadataContentSha256)
            && !string.IsNullOrWhiteSpace(currentPolicyContentSha256)
            && !string.Equals(metadataContentSha256, currentPolicyContentSha256, StringComparison.OrdinalIgnoreCase))
        {
            BlockForPolicyFailure("隐私政策元数据与正文校验值不一致，请稍后重试。");
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
            BlockForPolicyFailure("隐私政策完整性校验失败，请稍后重试。");
            yield break;
        }

        privacyPolicyLoaded = true;
        if (privacyConsentLabel != null)
        {
            privacyConsentLabel.text = "我已阅读并同意《隐私政策 " + currentPolicyVersion + "》";
        }
    }

    private IEnumerator RequestPolicyMetadata(Action<PolicyMetadataResponse> completed)
    {
        if (!ApiRequestUtility.TryCreateGet(
                "/privacy/policy/current",
                false,
                out UnityWebRequest request,
                out string error))
        {
            Debug.LogWarning("隐私政策元数据请求未创建：" + error);
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
            Debug.LogWarning("隐私政策正文请求未创建：" + error);
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
        ShowRuntimePolicyOverlay("隐私政策不可用", message, currentPolicyContentUrl);
    }

    private void ShowPrivacyPolicyOverlay()
    {
        if (privacyPolicyOverlay != null)
        {
            privacyPolicyOverlay.SetActive(true);
            privacyPolicyOverlay.transform.SetAsLastSibling();
            return;
        }

        RectTransform root = loginCanvasView != null ? loginCanvasView.PrepareContentRoot() : null;
        if (root == null)
        {
            ShowError("隐私政策界面创建失败");
            return;
        }

        Image overlay = CreateImage("PrivacyPolicyOverlay", root, new Color(0.04f, 0.07f, 0.12f, 0.96f));
        privacyPolicyOverlay = overlay.gameObject;
        Stretch(overlay.rectTransform);

        Image card = CreateImage("PrivacyPolicyCard", overlay.transform, new Color(1f, 1f, 1f, 0.98f));
        SetRect(card.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), Vector2.zero, new Vector2(860f, 720f));
        TMP_Text title = CreateComplianceText("Title", card.transform, "隐私政策", 30, FontStyle.Bold, BrandColor, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(-70f, 54f));

        TMP_Text metadata = CreateComplianceText(
            "Metadata",
            card.transform,
            "版本：" + (currentPolicyVersion ?? "未获取") + "\n正文校验：" + (string.IsNullOrEmpty(currentPolicyContentSha256) ? "未提供" : "已校验"),
            15,
            FontStyle.Normal,
            MutedTextColor,
            TextAnchor.MiddleLeft);
        SetRect(metadata.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(-70f, 48f));

        Image viewport = CreateImage("Viewport", card.transform, new Color(0f, 0f, 0f, 0.04f));
        SetRect(viewport.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(-70f, -250f));
        Mask mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(-28f, 500f);

        TMP_Text body = CreateComplianceText("Body", contentObject.transform, currentPolicyContent ?? "暂未获取隐私政策正文。", 17, FontStyle.Normal, FieldTextColor, TextAnchor.UpperLeft);
        body.enableWordWrapping = true;
        body.overflowMode = TextOverflowModes.Overflow;
        SetRect(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Canvas.ForceUpdateCanvases();
        contentRect.sizeDelta = new Vector2(-28f, Mathf.Max(500f, body.preferredHeight + 24f));

        ScrollRect scrollRect = viewport.gameObject.AddComponent<ScrollRect>();
        scrollRect.content = contentRect;
        scrollRect.viewport = viewport.rectTransform;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        Button closeButton = CreateComplianceButton(
            card.transform,
            "CloseButton",
            "关闭",
            new Vector2(-110f, -298f),
            new Vector2(170f, 52f),
            SecondaryButtonColor,
            FieldTextColor,
            18);
        closeButton.onClick.AddListener(HidePrivacyPolicyOverlay);

        Button agreeButton = CreateComplianceButton(
            card.transform,
            "AgreeButton",
            "同意并继续",
            new Vector2(110f, -298f),
            new Vector2(170f, 52f),
            PrimaryColor,
            Color.white,
            18);
        agreeButton.onClick.AddListener(AgreePrivacyPolicyFromOverlay);

        privacyPolicyOverlay.transform.SetAsLastSibling();
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
            SetManagementStatus("已确认隐私政策，请再次点击同意授权。", MutedTextColor);
            return;
        }

        SetStatus("已同意隐私政策，请重新点击登录。", MutedTextColor);
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

        Image overlay = CreateImage("AccountManagementPanel", root, new Color(0.04f, 0.07f, 0.12f, 0.96f));
        accountManagementPanel = overlay.gameObject;
        Stretch(overlay.rectTransform);

        Image card = CreateImage("AccountManagementCard", overlay.transform, new Color(1f, 1f, 1f, 0.98f));
        SetRect(card.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), Vector2.zero, new Vector2(900f, 790f));
        TMP_Text title = CreateComplianceText("Title", card.transform, "账号管理", 32, FontStyle.Bold, BrandColor, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(-70f, 54f));

        TMP_Text description = CreateComplianceText(
            "Description",
            card.transform,
            "当前账号只能进行隐私授权、实名和注销管理，完成准入后需要重新登录。",
            16,
            FontStyle.Normal,
            MutedTextColor,
            TextAnchor.MiddleCenter);
        SetRect(description.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(-70f, 44f));

        accountManagementCompliance = CreateComplianceText("Compliance", card.transform, string.Empty, 18, FontStyle.Normal, FieldTextColor, TextAnchor.UpperLeft);
        accountManagementCompliance.enableWordWrapping = true;
        accountManagementCompliance.overflowMode = TextOverflowModes.Overflow;
        SetRect(accountManagementCompliance.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(-90f, 94f));

        accountManagementStatus = CreateComplianceText("Status", card.transform, string.Empty, 16, FontStyle.Normal, ErrorColor, TextAnchor.MiddleCenter);
        SetRect(accountManagementStatus.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -222f), new Vector2(-90f, 42f));

        accountConsentButton = CreateComplianceButton(card.transform, "ConsentButton", "同意隐私政策", new Vector2(-225f, -292f), new Vector2(210f, 52f), PrimaryColor, Color.white, 17);
        accountConsentButton.onClick.AddListener(BeginPrivacyConsent);
        accountWithdrawButton = CreateComplianceButton(card.transform, "WithdrawButton", "撤回隐私授权", new Vector2(0f, -292f), new Vector2(210f, 52f), SecondaryButtonColor, FieldTextColor, 17);
        accountWithdrawButton.onClick.AddListener(BeginWithdrawConsent);
        accountRefreshButton = CreateComplianceButton(card.transform, "RefreshButton", "刷新状态", new Vector2(225f, -292f), new Vector2(160f, 52f), SecondaryButtonColor, FieldTextColor, 17);
        accountRefreshButton.onClick.AddListener(() => StartCoroutine(RefreshManagementState()));

        accountApplyCancellationButton = CreateComplianceButton(card.transform, "ApplyCancellationButton", "申请注销", new Vector2(-225f, -366f), new Vector2(210f, 52f), new Color(0.70f, 0.25f, 0.20f, 1f), Color.white, 17);
        accountApplyCancellationButton.onClick.AddListener(BeginAccountCancellation);
        accountCancelCancellationButton = CreateComplianceButton(card.transform, "CancelCancellationButton", "撤销注销", new Vector2(0f, -366f), new Vector2(210f, 52f), SecondaryButtonColor, FieldTextColor, 17);
        accountCancelCancellationButton.onClick.AddListener(BeginCancelAccountCancellation);
        accountRealNameButton = CreateComplianceButton(card.transform, "RealNameButton", "实名认证", new Vector2(225f, -366f), new Vector2(160f, 52f), PrimaryColor, Color.white, 17);
        accountRealNameButton.onClick.AddListener(ShowRealNamePanel);

        accountReloginButton = CreateComplianceButton(card.transform, "ReloginButton", "重新登录", new Vector2(-100f, -444f), new Vector2(200f, 52f), PrimaryColor, Color.white, 18);
        accountReloginButton.onClick.AddListener(ReloginAfterCompliance);
        Button closeButton = CreateComplianceButton(card.transform, "CloseButton", "返回", new Vector2(125f, -444f), new Vector2(160f, 52f), SecondaryButtonColor, FieldTextColor, 18);
        closeButton.onClick.AddListener(CloseAccountManagementPanel);

        accountManagementPanel.transform.SetAsLastSibling();
        StartCoroutine(RefreshManagementState());
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
                + "（当前政策 " + (managementConsent.currentPolicyVersion ?? "UNKNOWN") + "）";
        string latestConsent = managementConsent == null || string.IsNullOrEmpty(managementConsent.latestAction)
            ? "暂无授权记录"
            : managementConsent.latestAction
                + " / "
                + (string.IsNullOrEmpty(managementConsent.latestPolicyVersion)
                    ? "UNKNOWN"
                    : managementConsent.latestPolicyVersion)
                + " / "
                + (string.IsNullOrEmpty(managementConsent.latestOccurredTime)
                    ? "时间未知"
                    : managementConsent.latestOccurredTime);
        return "实名状态：" + (compliance.verificationStatus ?? "UNKNOWN")
            + "\n年龄档位：" + (compliance.ageGroup ?? "UNKNOWN")
            + "\n准入结果：" + access
            + "\n限制原因：" + reason
            + "\n服务端提示：" + (string.IsNullOrEmpty(compliance.message) ? "无" : compliance.message)
            + "\n剩余时长：" + Mathf.Max(0, compliance.remainingSeconds) + " 秒"
            + "\n隐私授权：" + consentSummary
            + "\n最近授权动作：" + latestConsent;
    }

    private void BeginPrivacyConsent()
    {
        if (privacyConsentToggle == null || !privacyConsentToggle.isOn)
        {
            SetManagementStatus("请先阅读隐私政策并确认同意。", ErrorColor);
            ShowPrivacyPolicyOverlay();
            return;
        }

        if (!privacyPolicyLoaded || string.IsNullOrEmpty(currentPolicyVersion) || string.IsNullOrEmpty(complianceToken))
        {
            SetManagementStatus("当前隐私政策不可用，暂时无法授权。", ErrorColor);
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
                    SetBusyState(false, "隐私政策已更新，请重新阅读并确认。");
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
            realNamePanel.SetActive(true);
            realNamePanel.transform.SetAsLastSibling();
            return;
        }

        if (accountManagementPanel == null)
        {
            return;
        }

        Image panel = CreateImage("RealNamePanel", accountManagementPanel.transform, new Color(0.04f, 0.07f, 0.12f, 0.98f));
        realNamePanel = panel.gameObject;
        Stretch(panel.rectTransform);
        Image card = CreateImage("RealNameCard", panel.transform, new Color(1f, 1f, 1f, 0.98f));
        SetRect(card.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), Vector2.zero, new Vector2(660f, 470f));
        TMP_Text title = CreateComplianceText("Title", card.transform, "实名认证", 30, FontStyle.Bold, BrandColor, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(-70f, 50f));
        TMP_Text hint = CreateComplianceText("Hint", card.transform, "信息仅用于本次认证请求，不会写入客户端日志。", 15, FontStyle.Normal, MutedTextColor, TextAnchor.MiddleCenter);
        SetRect(hint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(-70f, 36f));

        TMP_Text nameLabel = CreateComplianceText("NameLabel", card.transform, "真实姓名", 16, FontStyle.Normal, FieldTextColor, TextAnchor.MiddleLeft);
        SetRect(nameLabel.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), new Vector2(-220f, 76f), new Vector2(120f, 38f));
        realNameInput = CreateComplianceInput(card.transform, "RealNameInput", "请输入真实姓名");
        realNameInput.characterLimit = 64;
        SetRect(realNameInput.GetComponent<RectTransform>(), CenterAnchor(), CenterAnchor(), CenterAnchor(), new Vector2(90f, 76f), new Vector2(360f, 42f));

        TMP_Text identityLabel = CreateComplianceText("IdentityLabel", card.transform, "证件号码", 16, FontStyle.Normal, FieldTextColor, TextAnchor.MiddleLeft);
        SetRect(identityLabel.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), new Vector2(-220f, 18f), new Vector2(120f, 38f));
        identityNumberInput = CreateComplianceInput(card.transform, "IdentityNumberInput", "请输入证件号码");
        identityNumberInput.characterLimit = 32;
        SetRect(identityNumberInput.GetComponent<RectTransform>(), CenterAnchor(), CenterAnchor(), CenterAnchor(), new Vector2(90f, 18f), new Vector2(360f, 42f));

        realNameStatus = CreateComplianceText("Status", card.transform, string.Empty, 15, FontStyle.Normal, ErrorColor, TextAnchor.MiddleCenter);
        SetRect(realNameStatus.rectTransform, CenterAnchor(), CenterAnchor(), CenterAnchor(), new Vector2(0f, -52f), new Vector2(560f, 38f));

        Button submit = CreateComplianceButton(card.transform, "SubmitButton", "提交认证", new Vector2(-105f, -125f), new Vector2(190f, 50f), PrimaryColor, Color.white, 18);
        submit.onClick.AddListener(SubmitRealName);
        Button close = CreateComplianceButton(card.transform, "CloseButton", "取消", new Vector2(105f, -125f), new Vector2(150f, 50f), SecondaryButtonColor, FieldTextColor, 18);
        close.onClick.AddListener(HideRealNamePanel);
        realNamePanel.transform.SetAsLastSibling();
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

            if (response.data != null && response.data.reloginRequired)
            {
                SetRealNameStatus("实名认证已通过，请重新登录。", MutedTextColor);
                CloseRealNamePanel();
                CloseAccountManagementPanel();
                complianceToken = string.Empty;
                SetBusyState(false, "实名认证已通过，请重新登录。");
                yield break;
            }

            SetRealNameStatus("实名认证已提交。", MutedTextColor);
        }
    }

    private void HideRealNamePanel()
    {
        if (realNamePanel != null)
        {
            realNamePanel.SetActive(false);
        }
    }

    private void CloseRealNamePanel()
    {
        if (realNamePanel != null)
        {
            Destroy(realNamePanel);
            realNamePanel = null;
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
        text.text = value ?? string.Empty;
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
        int playerId = GameSession.PlayerId;
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

        if (playerId > 0)
        {
            string avatarStateKeyPrefix = "PlayerAvatarState." + playerId + ".";
            PlayerPrefs.DeleteKey(avatarStateKeyPrefix + "Code");
            PlayerPrefs.DeleteKey(avatarStateKeyPrefix + "Source");
            PlayerPrefs.DeleteKey(avatarStateKeyPrefix + "Version");
            PlayerPrefs.DeleteKey(avatarStateKeyPrefix + "UpdatedTime");
            PlayerPrefs.DeleteKey(avatarStateKeyPrefix + "HasState");
        }

        PlayerPrefs.Save();
    }

    private void SetManagementStatus(string message, Color color)
    {
        if (accountManagementStatus != null)
        {
            accountManagementStatus.color = color;
            accountManagementStatus.text = message ?? string.Empty;
        }
    }

    private void SetRealNameStatus(string message, Color color)
    {
        if (realNameStatus != null)
        {
            realNameStatus.color = color;
            realNameStatus.text = message ?? string.Empty;
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
