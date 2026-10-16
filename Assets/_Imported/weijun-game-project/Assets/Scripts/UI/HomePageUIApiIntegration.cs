using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Text = TMPro.TMP_Text;

public partial class HomePageUI
{
    private readonly Dictionary<string, Button> mapSelectorButtons = new Dictionary<string, Button>();
    private GameObject mapSelectorObject;
    private string requestCreationError = string.Empty;
    private bool mapEntryRunning;
    private bool logoutRunning;
    private bool paymentRequestRunning;
    private bool paymentPolling;
    private bool paymentRecoveryRunning;
    private bool paymentAccountRefreshRunning;
    private bool paymentDeliveryRefreshPending;
    private string pendingPaymentOrderNo = string.Empty;
    private string pendingPaymentClientOrderId = string.Empty;
    private string pendingPaymentProductCode = string.Empty;
    private bool paymentStateLoaded;
    private int paymentStatePlayerId;
    private float nextPaymentRecoveryCheckTime;

    private const string PendingPaymentKeyPrefix = "Payment.Pending.Player.";
    private const string PendingPaymentOrderSuffix = ".OrderNo";
    private const string PendingPaymentClientOrderSuffix = ".ClientOrderId";
    private const string PendingPaymentProductSuffix = ".ProductCode";
    private const string PendingPaymentStatusSuffix = ".Status";
    private const string LegacyPendingPaymentPlayerKey = "Payment.Pending.PlayerId";
    private const string LegacyPendingPaymentOrderKey = "Payment.Pending.OrderNo";
    private const string LegacyPendingPaymentClientOrderKey = "Payment.Pending.ClientOrderId";
    private const string LegacyPendingPaymentProductKey = "Payment.Pending.ProductCode";
    private const string LegacyPendingPaymentStatusKey = "Payment.Pending.Status";
    private const float PaymentBackgroundRecoveryIntervalSeconds = 15f;

    private void Start()
    {
        LoadPendingPaymentState();
        if (HasAuthSession())
        {
            StartPendingPaymentRecovery("正在恢复未完成的支付订单...", 2f);
        }
    }

    private void Update()
    {
        UpdateHomeStaminaCountdown();
        if (!HasAuthSession())
        {
            paymentStateLoaded = false;
            paymentStatePlayerId = 0;
            return;
        }

        if (!paymentStateLoaded || paymentStatePlayerId != GameSession.PlayerId)
        {
            LoadPendingPaymentState();
            if (!paymentPolling && !paymentRequestRunning && !paymentAccountRefreshRunning)
            {
                StartCoroutine(RecoverPendingPaymentOrder("正在恢复未完成的支付订单..."));
            }
        }

        bool rechargeVisible = rechargePanelObject != null && rechargePanelObject.activeInHierarchy;
        if (!string.IsNullOrEmpty(pendingPaymentOrderNo)
            && Time.unscaledTime >= nextPaymentRecoveryCheckTime
            && !paymentPolling
            && !paymentRecoveryRunning
            && !paymentRequestRunning
            && !paymentAccountRefreshRunning)
        {
            StartPendingPaymentRecovery(
                rechargeVisible ? "充值页面已打开，正在补查支付订单..." : string.Empty,
                rechargeVisible ? 2f : PaymentBackgroundRecoveryIntervalSeconds);
        }
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused || !HasAuthSession())
        {
            return;
        }

        LoadPendingPaymentState();
        StartPendingPaymentRecovery("应用恢复前台，正在补查支付订单...", 2f);
        RefreshServerSignInStatus();
        StartCoroutine(RefreshServerStamina());
    }

    private void BuildHomeMapSelector(Transform parent)
    {
        Image selector = CreateImage("HomeMapSelector", parent, new Color(1f, 0.91f, 0.66f, 0.24f));
        selector.raycastTarget = false;
        mapSelectorObject = selector.gameObject;
        SetRect(selector.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(20f, -64f), new Vector2(1000f, 590f));

        Text title = CreateText("MapSelectorTitle", selector.transform, "地图选择", 30, FontStyle.Bold, InkColor, TextAnchor.MiddleLeft);
        SetRect(title.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(20f, -8f), new Vector2(280f, 42f));

        Text subtitle = CreateText("MapSelectorSubtitle", selector.transform, "选择领地区域后进入地图", 16, FontStyle.Normal, MutedColor, TextAnchor.MiddleRight);
        SetRect(subtitle.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(650f, -14f), new Vector2(320f, 32f));

        mapSelectorButtons.Clear();
        for (int i = 0; i < areas.Length; i++)
        {
            AreaInfo area = areas[i];
            float x = 18f + i * 193f;
            Button card = CreateButton(
                selector.transform,
                "MapSelector" + area.landType,
                string.Empty,
                Color.Lerp(area.color, new Color(1f, 0.92f, 0.72f, 1f), 0.52f),
                InkColor);
            SetRect(card.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(x, -62f), new Vector2(176f, 214f));
            card.onClick.AddListener(() => SelectAreaAndEnter(area));
            mapSelectorButtons[area.landType] = card;

            Image accent = CreateImage("Accent", card.transform, area.color);
            accent.raycastTarget = false;
            SetRect(accent.rectTransform, TopLeftAnchor(), new Vector2(1f, 1f), TopLeftAnchor(), Vector2.zero, new Vector2(0f, 12f));

            Text areaTitle = CreateText("AreaTitle", card.transform, area.name, 25, FontStyle.Bold, InkColor, TextAnchor.MiddleCenter);
            SetRect(areaTitle.rectTransform, TopLeftAnchor(), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(-20f, 42f));

            Text description = CreateText("AreaDescription", card.transform, area.description, 15, FontStyle.Normal, new Color(0.32f, 0.25f, 0.19f, 1f), TextAnchor.UpperCenter);
            description.enableWordWrapping = true;
            description.overflowMode = TextOverflowModes.Truncate;
            SetRect(description.rectTransform, TopLeftAnchor(), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(-24f, 82f));

            Text hint = CreateText("AreaHint", card.transform, area.unlockHint, 14, FontStyle.Bold, area.color, TextAnchor.MiddleCenter);
            SetRect(hint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(-18f, 34f));
        }

        Image detail = CreateImage("MapSelectorDetail", selector.transform, new Color(1f, 0.96f, 0.82f, 0.90f));
        SetRect(detail.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(18f, -294f), new Vector2(954f, 188f));

        selectedAreaTitle = CreateText("SelectedAreaTitle", detail.transform, string.Empty, 27, FontStyle.Bold, InkColor, TextAnchor.MiddleLeft);
        SetRect(selectedAreaTitle.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(22f, -16f), new Vector2(260f, 40f));

        selectedAreaDescription = CreateText("SelectedAreaDescription", detail.transform, string.Empty, 17, FontStyle.Normal, MutedColor, TextAnchor.UpperLeft);
        SetRect(selectedAreaDescription.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(22f, -62f), new Vector2(640f, 88f));

        selectedAreaHint = CreateText("SelectedAreaHint", detail.transform, string.Empty, 16, FontStyle.Bold, PrimaryColor, TextAnchor.MiddleLeft);
        SetRect(selectedAreaHint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(22f, 16f), new Vector2(540f, 30f));

        Button enter = CreateButton(detail.transform, "MapSelectorEnterButton", "进入选中地图", PrimaryColor, Color.white);
        enter.onClick.AddListener(EnterSelectedArea);
        SetRect(enter.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-22f, 18f), new Vector2(230f, 52f));
        ShrinkButtonLabel(enter, 20);
    }

    private void ShowMapSelectionContent()
    {
        if (contentPanelObject != null)
        {
            contentPanelObject.SetActive(true);
        }

        if (contentTitleText != null)
        {
            contentTitleText.gameObject.SetActive(false);
        }

        if (contentBodyText != null)
        {
            contentBodyText.gameObject.SetActive(false);
        }

        if (mapSelectorObject != null)
        {
            mapSelectorObject.SetActive(true);
        }

        RefreshMapSelectorState();
        SetStatus("请选择地图区域");
    }

    private void RefreshMapSelectorState()
    {
        if (selectedArea == null)
        {
            selectedArea = GetLastArea();
        }

        foreach (KeyValuePair<string, Button> pair in mapSelectorButtons)
        {
            AreaInfo area = GetArea(pair.Key);
            Image background = pair.Value != null ? pair.Value.GetComponent<Image>() : null;
            if (background != null)
            {
                bool selected = selectedArea != null && selectedArea.landType == pair.Key;
                background.color = selected
                    ? Color.Lerp(area.color, Color.white, 0.18f)
                    : Color.Lerp(area.color, new Color(1f, 0.92f, 0.72f, 1f), 0.52f);
            }
        }
    }

    private IEnumerator GetMapDataAndEnter(string landType)
    {
        mapEntryRunning = true;
        SetStatus("正在同步服务器地图...");

        int playerId = GameSession.PlayerId;
        UnityWebRequest request = CreateAuthorizedGet(AppendSceneIdQuery($"/land/map/{playerId}"));
        if (request == null)
        {
            mapEntryRunning = false;
            SetStatus(requestCreationError);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                mapEntryRunning = false;
                yield break;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                mapEntryRunning = false;
                SetStatus("地图同步失败：" + ApiRequestUtility.GetErrorMessage(request, "请求失败"));
                yield break;
            }

            MapDataApiResponse response = ParseJson<MapDataApiResponse>(request.downloadHandler.text);
            if (response == null || response.code != 200 || response.data == null)
            {
                mapEntryRunning = false;
                SetStatus("地图同步失败：" + (response == null ? "返回数据格式错误" : GetApiError(response)));
                yield break;
            }

            PlayerPrefs.SetInt("ServerMapWidth", response.data.width);
            PlayerPrefs.SetInt("ServerMapHeight", response.data.height);
            PlayerPrefs.SetInt("ServerLandCount", response.data.lands == null ? 0 : response.data.lands.Length);
            PlayerPrefs.SetInt("ServerBuildingCount", response.data.buildings == null ? 0 : response.data.buildings.Length);
            int responseSceneId = response.data.sceneId > 0 ? response.data.sceneId : response.data.scene_id;
            if (responseSceneId > 0) PlayerPrefs.SetInt("weijun.backend.sceneId", responseSceneId);
            PlayerPrefs.SetString("ServerMapLastSync", DateTime.UtcNow.ToString("O"));
            PlayerPrefs.Save();
        }

        mapEntryRunning = false;
        CompleteEnterArea(landType);
    }

    private void CompleteEnterArea(string landType)
    {
        PlayerPrefs.SetString("CurrentAreaLandType", landType);
        PlayerPrefs.SetString("OpenAreaLandType", landType);
        PlayerPrefs.SetInt("TaskMapEntered", 1);
        PlayerPrefs.SetString("TaskMapDate", GetTaskDate());
        if (string.IsNullOrEmpty(PlayerPrefs.GetString("HomeSelectedMode", string.Empty)))
        {
            PlayerPrefs.SetString("HomeSelectedMode", "build");
        }

        PlayerPrefs.Save();
        SceneManager.LoadScene(SimulationSceneName);
    }

    private IEnumerator LogoutAndReturnToLogin()
    {
        logoutRunning = true;
        StopRealtimeConnection();
        ClearRareBroadcastState();
        bool shouldNotifyServer = GameSession.IsAuthenticated;
        if (shouldNotifyServer)
        {
            SetStatus("正在退出当前账号...");
            if (TryCreateAuthorizedPost("/user/logout", "{}", out UnityWebRequest request, out string error))
            {
                using (request)
                {
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success && !ApiRequestUtility.IsUnauthorized(request))
                    {
                        Debug.LogWarning("服务端退出失败，仍将清理本地会话：" + ApiRequestUtility.GetErrorMessage(request, error));
                    }
                }
            }
            else
            {
                Debug.LogWarning("未能创建退出请求：" + error);
            }
        }

        GameSession.Clear();
        logoutRunning = false;
        SceneManager.LoadScene(LaunchSceneName);
    }

    private void BeginPaymentOrder(PaymentProductResponse product, string productName)
    {
        if (!HasAuthSession() || GameSession.PlayerId <= 0)
        {
            SetStatus("请先登录账号或微信账号后再充值。");
            return;
        }

        if (paymentRequestRunning || product == null || string.IsNullOrWhiteSpace(product.productCode))
        {
            SetStatus("支付商品信息无效，请刷新商品列表后重试。");
            return;
        }

        LoadPendingPaymentState();
        if (!string.IsNullOrEmpty(pendingPaymentOrderNo))
        {
            SetStatus("已有未完成支付订单，正在先查询该订单状态；请勿重复下单。");
            if (!paymentPolling)
            {
                StartCoroutine(RecoverPendingPaymentOrder(string.Empty));
            }
            return;
        }

        if (!string.IsNullOrEmpty(pendingPaymentClientOrderId)
            && !string.Equals(pendingPaymentProductCode, product.productCode, StringComparison.Ordinal))
        {
            SetStatus("上一笔支付订单创建结果尚未确定，只能重试同一商品；请先不要切换充值档位。");
            return;
        }

        StartCoroutine(CreatePaymentOrder(product, productName));
    }

    private IEnumerator CreatePaymentOrder(PaymentProductResponse product, string productName)
    {
        paymentRequestRunning = true;
        string clientOrderId = GetOrCreatePaymentClientOrderId(product.productCode);
        SavePendingPaymentState(string.Empty, clientOrderId, product.productCode, string.Empty);
        PaymentOrderCreateRequest payload = new PaymentOrderCreateRequest
        {
            playerId = GameSession.PlayerId,
            clientOrderId = clientOrderId,
            productCode = product.productCode
        };

        if (!TryCreateAuthorizedPost(
                "/payment/order/create",
                JsonUtility.ToJson(payload),
                out UnityWebRequest request,
                out string error))
        {
            paymentRequestRunning = false;
            SetStatus(error);
            yield break;
        }

        SetStatus($"正在为 {productName} 创建订单...");
        using (request)
        {
            yield return request.SendWebRequest();
            paymentRequestRunning = false;
            if (HandleUnauthorizedResponse(request))
            {
                yield break;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                bool outcomeUncertain = request.result == UnityWebRequest.Result.ConnectionError
                    || request.result == UnityWebRequest.Result.DataProcessingError
                    || request.responseCode >= 500;
                if (!outcomeUncertain)
                {
                    ClearPendingPaymentCreateAttempt(true);
                }

                SetStatus(outcomeUncertain
                    ? "创建订单结果未知，重新点击同一商品会复用本次订单号：" + ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : "创建订单失败：" + ApiRequestUtility.GetErrorMessage(request, "请求失败"));
                yield break;
            }

            PaymentOrderCreateApiResponse response = ParseJson<PaymentOrderCreateApiResponse>(request.downloadHandler.text);
            if (response == null)
            {
                SetStatus("创建订单结果未知，重新点击同一商品会复用本次订单号：订单响应格式错误。");
                yield break;
            }

            if (response.code != 200)
            {
                ClearPendingPaymentCreateAttempt(true);
                SetStatus("创建订单失败：" + GetApiError(response));
                yield break;
            }

            if (response.data == null || response.data.order == null)
            {
                SetStatus("创建订单结果未知：响应缺少订单数据；重新点击同一商品会复用本次客户端订单号。");
                yield break;
            }

            pendingPaymentOrderNo = response.data.order.orderNo ?? string.Empty;
            SavePendingPaymentState(
                pendingPaymentOrderNo,
                response.data.order.clientOrderId ?? clientOrderId,
                response.data.order.productCode ?? product.productCode,
                response.data.order.status);
            if (string.IsNullOrEmpty(pendingPaymentOrderNo))
            {
                SetStatus("创建订单结果未知：后端未返回订单号；重新点击同一商品会复用本次客户端订单号。");
                yield break;
            }

            if (!response.data.payReady)
            {
                SetStatus($"订单 {pendingPaymentOrderNo} 已创建；当前为后端本地订单测试模式，不会发放资源。");
                yield break;
            }

            if (!HasCompletePaymentParameters(response.data.payParams))
            {
                SetStatus("订单已创建，但后端未返回微信支付参数。");
                yield break;
            }

            SetStatus("订单已创建，正在调起微信支付...");
            WeChatPlatformBridge.RequestPayment(this, JsonUtility.ToJson(response.data.payParams));
        }
    }

    public void OnWeChatPaymentSucceeded(string message)
    {
        LoadPendingPaymentState();
        if (paymentPolling || string.IsNullOrEmpty(pendingPaymentOrderNo))
        {
            if (string.IsNullOrEmpty(pendingPaymentOrderNo))
            {
                SetStatus("支付回调已收到，但没有可查询的订单号；请重新打开充值页面查看订单。");
            }
            return;
        }

        if (paymentAccountRefreshRunning || paymentDeliveryRefreshPending || paymentRecoveryRunning)
        {
            return;
        }

        StartCoroutine(PollPaymentOrder(pendingPaymentOrderNo));
    }

    public void OnWeChatPaymentFailed(string message)
    {
        paymentRequestRunning = false;
        LoadPendingPaymentState();
        ClearPendingPaymentCreateAttempt();
        if (!string.IsNullOrEmpty(pendingPaymentOrderNo))
        {
            SetStatus("支付回调未完成，正在保留订单并补查服务端状态：" + (string.IsNullOrEmpty(message) ? "用户取消或支付失败" : message));
            if (!paymentPolling)
            {
                StartCoroutine(RecoverPendingPaymentOrder(string.Empty));
            }
            return;
        }
        SetStatus("微信支付未完成：" + (string.IsNullOrEmpty(message) ? "用户取消或支付失败" : message));
    }

    private IEnumerator PollPaymentOrder(string orderNo)
    {
        if (string.IsNullOrWhiteSpace(orderNo)
            || !HasAuthSession()
            || paymentPolling
            || paymentAccountRefreshRunning
            || paymentDeliveryRefreshPending)
        {
            yield break;
        }

        paymentPolling = true;
        orderNo = orderNo.Trim();
        int playerId = GameSession.PlayerId;
        for (int attempt = 0; attempt < 15; attempt++)
        {
            UnityWebRequest request = CreateAuthorizedGet($"/payment/order/{playerId}/{UnityWebRequest.EscapeURL(orderNo)}");
            if (request == null)
            {
                SetStatus(requestCreationError);
                break;
            }

            using (request)
            {
                yield return request.SendWebRequest();
                if (HandleUnauthorizedResponse(request))
                {
                    paymentPolling = false;
                    yield break;
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    PaymentOrderApiResponse response = ParseJson<PaymentOrderApiResponse>(request.downloadHandler.text);
                    if (response != null && response.code == 200 && response.data != null)
                    {
                        string status = (response.data.status ?? string.Empty).Trim().ToUpperInvariant();
                        SavePendingPaymentState(orderNo, response.data.clientOrderId, response.data.productCode, status);
                        SetStatus(BuildPaymentOrderStatusMessage(orderNo, status));
                        if (status == "DELIVERED")
                        {
                            paymentPolling = false;
                            BeginPaymentDeliveryRefresh();
                            yield break;
                        }

                        if (IsPaymentOrderTerminalStatus(status))
                        {
                            paymentPolling = false;
                            ClearPendingPaymentState();
                            yield break;
                        }

                        if (status == "MANUAL_REVIEW")
                        {
                            paymentPolling = false;
                            nextPaymentRecoveryCheckTime = Time.unscaledTime
                                + PaymentBackgroundRecoveryIntervalSeconds;
                            yield break;
                        }
                    }
                }
            }

            yield return new WaitForSecondsRealtime(GetPaymentPollIntervalSeconds());
        }

        paymentPolling = false;
        nextPaymentRecoveryCheckTime = Time.unscaledTime + PaymentBackgroundRecoveryIntervalSeconds;
        SetStatus("支付结果仍在处理中，请稍后重新打开充值面板刷新状态。");
    }

    private void StartPendingPaymentRecovery(string statusMessage, float retryIntervalSeconds)
    {
        if (string.IsNullOrEmpty(pendingPaymentOrderNo)
            || !HasAuthSession()
            || paymentPolling
            || paymentRecoveryRunning
            || paymentRequestRunning
            || paymentAccountRefreshRunning)
        {
            return;
        }

        nextPaymentRecoveryCheckTime = Time.unscaledTime + Mathf.Max(0.1f, retryIntervalSeconds);
        StartCoroutine(RecoverPendingPaymentOrder(statusMessage));
    }

    private IEnumerator RecoverPendingPaymentOrder(string statusMessage)
    {
        if (!HasAuthSession()
            || paymentPolling
            || paymentRecoveryRunning
            || paymentRequestRunning
            || paymentAccountRefreshRunning)
        {
            yield break;
        }

        paymentRecoveryRunning = true;
        try
        {
            LoadPendingPaymentState();
            if (string.IsNullOrWhiteSpace(pendingPaymentOrderNo))
            {
                yield break;
            }

            if (!string.IsNullOrEmpty(statusMessage))
            {
                SetStatus(statusMessage);
            }

            yield return PollPaymentOrder(pendingPaymentOrderNo);
        }
        finally
        {
            paymentRecoveryRunning = false;
        }
    }

    private static float GetPaymentPollIntervalSeconds()
    {
        return 3f;
    }

    private void SetPendingPaymentOrderFromNotification(string orderNo)
    {
        if (string.IsNullOrWhiteSpace(orderNo) || !HasAuthSession())
        {
            return;
        }

        LoadPendingPaymentState();
        SavePendingPaymentState(orderNo.Trim(), pendingPaymentClientOrderId, pendingPaymentProductCode, "");
    }

    private string GetOrCreatePaymentClientOrderId(string productCode)
    {
        if (!string.IsNullOrEmpty(pendingPaymentClientOrderId)
            && string.Equals(pendingPaymentProductCode, productCode, StringComparison.Ordinal))
        {
            return pendingPaymentClientOrderId;
        }

        pendingPaymentProductCode = productCode ?? string.Empty;
        pendingPaymentClientOrderId = "unity-"
            + GameSession.PlayerId
            + "-"
            + DateTime.UtcNow.ToString("yyyyMMddHHmmss")
            + "-"
            + Guid.NewGuid().ToString("N").Substring(0, 8);
        return pendingPaymentClientOrderId;
    }

    private void ClearPendingPaymentCreateAttempt(bool clearOrder = false)
    {
        pendingPaymentClientOrderId = string.Empty;
        pendingPaymentProductCode = string.Empty;
        if (clearOrder)
        {
            ClearPendingPaymentState();
        }
        else if (paymentStateLoaded)
        {
            SavePendingPaymentState(
                pendingPaymentOrderNo,
                string.Empty,
                string.Empty,
                GetPendingPaymentStatus(GameSession.PlayerId));
        }
    }

    private void LoadPendingPaymentState()
    {
        int currentPlayerId = GameSession.PlayerId;
        if (currentPlayerId <= 0)
        {
            paymentStateLoaded = false;
            return;
        }

        MigrateLegacyPendingPaymentState();
        pendingPaymentOrderNo = PlayerPrefs.GetString(
            BuildPendingPaymentKey(currentPlayerId, PendingPaymentOrderSuffix),
            string.Empty);
        pendingPaymentClientOrderId = PlayerPrefs.GetString(
            BuildPendingPaymentKey(currentPlayerId, PendingPaymentClientOrderSuffix),
            string.Empty);
        pendingPaymentProductCode = PlayerPrefs.GetString(
            BuildPendingPaymentKey(currentPlayerId, PendingPaymentProductSuffix),
            string.Empty);

        paymentStateLoaded = true;
        paymentStatePlayerId = currentPlayerId;
    }

    private void SavePendingPaymentState(string orderNo, string clientOrderId, string productCode, string status)
    {
        int playerId = GameSession.PlayerId;
        if (playerId <= 0)
        {
            return;
        }

        pendingPaymentOrderNo = orderNo ?? string.Empty;
        if (!string.IsNullOrEmpty(clientOrderId)) pendingPaymentClientOrderId = clientOrderId;
        if (!string.IsNullOrEmpty(productCode)) pendingPaymentProductCode = productCode;
        PlayerPrefs.SetString(
            BuildPendingPaymentKey(playerId, PendingPaymentOrderSuffix),
            pendingPaymentOrderNo);
        PlayerPrefs.SetString(
            BuildPendingPaymentKey(playerId, PendingPaymentClientOrderSuffix),
            pendingPaymentClientOrderId ?? string.Empty);
        PlayerPrefs.SetString(
            BuildPendingPaymentKey(playerId, PendingPaymentProductSuffix),
            pendingPaymentProductCode ?? string.Empty);
        PlayerPrefs.SetString(
            BuildPendingPaymentKey(playerId, PendingPaymentStatusSuffix),
            status ?? string.Empty);
        PlayerPrefs.Save();
        paymentStateLoaded = true;
        paymentStatePlayerId = playerId;
    }

    private void ClearPendingPaymentState()
    {
        if (paymentDeliveryRefreshPending)
        {
            return;
        }

        pendingPaymentOrderNo = string.Empty;
        pendingPaymentClientOrderId = string.Empty;
        pendingPaymentProductCode = string.Empty;
        int playerId = paymentStatePlayerId > 0 ? paymentStatePlayerId : GameSession.PlayerId;
        if (playerId > 0)
        {
            PlayerPrefs.DeleteKey(BuildPendingPaymentKey(playerId, PendingPaymentOrderSuffix));
            PlayerPrefs.DeleteKey(BuildPendingPaymentKey(playerId, PendingPaymentClientOrderSuffix));
            PlayerPrefs.DeleteKey(BuildPendingPaymentKey(playerId, PendingPaymentProductSuffix));
            PlayerPrefs.DeleteKey(BuildPendingPaymentKey(playerId, PendingPaymentStatusSuffix));
        }
        PlayerPrefs.Save();
        paymentStateLoaded = true;
    }

    private static string BuildPendingPaymentKey(int playerId, string suffix)
    {
        return PendingPaymentKeyPrefix + playerId + suffix;
    }

    private static string GetPendingPaymentStatus(int playerId)
    {
        return playerId > 0
            ? PlayerPrefs.GetString(
                BuildPendingPaymentKey(playerId, PendingPaymentStatusSuffix),
                string.Empty)
            : string.Empty;
    }

    private static void MigrateLegacyPendingPaymentState()
    {
        int legacyPlayerId = PlayerPrefs.GetInt(LegacyPendingPaymentPlayerKey, 0);
        if (legacyPlayerId <= 0)
        {
            return;
        }

        MigrateLegacyPendingPaymentValue(
            LegacyPendingPaymentOrderKey,
            BuildPendingPaymentKey(legacyPlayerId, PendingPaymentOrderSuffix));
        MigrateLegacyPendingPaymentValue(
            LegacyPendingPaymentClientOrderKey,
            BuildPendingPaymentKey(legacyPlayerId, PendingPaymentClientOrderSuffix));
        MigrateLegacyPendingPaymentValue(
            LegacyPendingPaymentProductKey,
            BuildPendingPaymentKey(legacyPlayerId, PendingPaymentProductSuffix));
        MigrateLegacyPendingPaymentValue(
            LegacyPendingPaymentStatusKey,
            BuildPendingPaymentKey(legacyPlayerId, PendingPaymentStatusSuffix));
        PlayerPrefs.DeleteKey(LegacyPendingPaymentPlayerKey);
        PlayerPrefs.DeleteKey(LegacyPendingPaymentOrderKey);
        PlayerPrefs.DeleteKey(LegacyPendingPaymentClientOrderKey);
        PlayerPrefs.DeleteKey(LegacyPendingPaymentProductKey);
        PlayerPrefs.DeleteKey(LegacyPendingPaymentStatusKey);
        PlayerPrefs.Save();
    }

    private static void MigrateLegacyPendingPaymentValue(string legacyKey, string scopedKey)
    {
        if (!PlayerPrefs.HasKey(scopedKey) && PlayerPrefs.HasKey(legacyKey))
        {
            PlayerPrefs.SetString(scopedKey, PlayerPrefs.GetString(legacyKey, string.Empty));
        }
    }

    private static bool HasCompletePaymentParameters(PaymentParameters parameters)
    {
        return parameters != null
            && !string.IsNullOrWhiteSpace(parameters.timeStamp)
            && !string.IsNullOrWhiteSpace(parameters.nonceStr)
            && !string.IsNullOrWhiteSpace(parameters.package)
            && !string.IsNullOrWhiteSpace(parameters.signType)
            && !string.IsNullOrWhiteSpace(parameters.paySign);
    }

    private static bool IsPaymentOrderTerminalStatus(string status)
    {
        return status == "CLOSED"
            || status == "FAILED"
            || status == "CANCELLED"
            || status == "REFUNDED";
    }

    private static string BuildPaymentOrderStatusMessage(string orderNo, string status)
    {
        switch (status)
        {
            case "DELIVERED":
                return $"订单 {orderNo} 已到账，正在刷新钻石余额。";
            case "PAID":
                return $"订单 {orderNo} 已支付，等待服务器发放钻石。";
            case "PAYING":
            case "CREATED":
                return $"订单 {orderNo} 正在等待支付结果。";
            case "MANUAL_REVIEW":
                return $"订单 {orderNo} 需要运营人工核对，暂不会自动到账。";
            case "CLOSED":
                return $"订单 {orderNo} 已关闭，请重新下单。";
            case "REFUNDED":
                return $"订单 {orderNo} 已完成退款。";
            case "CANCELLED":
                return $"订单 {orderNo} 已取消。";
            case "FAILED":
                return $"订单 {orderNo} 支付未完成。";
            default:
                return $"订单 {orderNo}：{status}";
        }
    }

    private IEnumerator RefreshAccountSnapshot(string successMessage)
    {
        return RefreshAccountSnapshot(successMessage, false);
    }

    private IEnumerator RefreshAccountSnapshot(string successMessage, bool isPaymentDeliveryRefresh, Action<string> onFailure = null)
    {
        bool accountRefreshSucceeded = false;
        string requestToken = GameSession.Token;
        Action<string> reportFailure = message =>
        {
            if (onFailure != null) onFailure(message);
            else SetStatus(message);
        };
        UnityWebRequest request = CreateAuthorizedGet("/user/me");
        if (request == null)
        {
            reportFailure(requestCreationError);
            if (isPaymentDeliveryRefresh)
            {
                FinishPaymentDeliveryRefresh(false);
            }
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (!string.Equals(requestToken, GameSession.Token, StringComparison.Ordinal))
            {
                if (isPaymentDeliveryRefresh) FinishPaymentDeliveryRefresh(false);
                yield break;
            }
            if (HandleUnauthorizedResponse(request))
            {
                if (isPaymentDeliveryRefresh)
                {
                    FinishPaymentDeliveryRefresh(false);
                }
                yield break;
            }

            UserProfileApiResponse response = ParseJson<UserProfileApiResponse>(request.downloadHandler.text);
            if (request.result == UnityWebRequest.Result.Success && response != null && response.code == 200 && response.data != null)
            {
                SaveUserProfile(response.data);
                QueueBackendSnapshotRefresh(true, true);
                accountRefreshSucceeded = true;
                yield return RefreshServerStamina();
                if (!string.IsNullOrEmpty(successMessage))
                {
                    SetStatus(successMessage);
                }
            }
            else
            {
                string prefix = isPaymentDeliveryRefresh ? "订单已完成，但资源刷新失败：" : "玩家资料刷新失败：";
                string error = response != null && response.code != 200
                    ? GetApiError(response)
                    : ApiRequestUtility.GetErrorMessage(request, "请稍后重试");
                reportFailure(prefix + error);
            }
        }
        if (isPaymentDeliveryRefresh)
        {
            FinishPaymentDeliveryRefresh(accountRefreshSucceeded);
        }
    }

    private void BeginPaymentDeliveryRefresh()
    {
        if (paymentDeliveryRefreshPending || paymentAccountRefreshRunning)
        {
            return;
        }

        paymentAccountRefreshRunning = true;
        paymentDeliveryRefreshPending = true;
        StartCoroutine(RefreshAccountSnapshot("支付已到账，资源已从服务器刷新。", true));
    }

    private void HandlePaymentDeliveredRealtimeNotification(string payload)
    {
        NotificationPayload notificationPayload = ParseNotificationPayload(payload);
        string orderNo = notificationPayload == null
            ? string.Empty
            : notificationPayload.ResolvedOrderNo;
        if (!string.IsNullOrWhiteSpace(orderNo))
        {
            SetPendingPaymentOrderFromNotification(orderNo);
            StartPendingPaymentRecovery("正在查询支付到账通知中的订单...", 2f);
            return;
        }

        if (!paymentAccountRefreshRunning && !paymentDeliveryRefreshPending)
        {
            StartCoroutine(RefreshAccountSnapshot("支付奖励已到账，资源已刷新。"));
        }
    }

    private void FinishPaymentDeliveryRefresh(bool refreshed)
    {
        if (!paymentDeliveryRefreshPending)
        {
            return;
        }

        paymentDeliveryRefreshPending = false;
        paymentAccountRefreshRunning = false;
        if (refreshed)
        {
            ClearPendingPaymentState();
            return;
        }

        nextPaymentRecoveryCheckTime = 0f;
        SetStatus("Payment delivered, but the account refresh failed; the order will be retried.");
    }

    private bool TryCreateAuthorizedPost(string path, string json, out UnityWebRequest request, out string error)
    {
        bool created = ApiRequestUtility.TryCreateJsonRequest(
            path,
            UnityWebRequest.kHttpVerbPOST,
            json,
            true,
            out request,
            out error);
        if (created) ApiRequestUtility.SetIdempotencyKey(request, Guid.NewGuid().ToString("D"));
        return created;
    }

    private bool HandleUnauthorizedResponse(UnityWebRequest request)
    {
        if (!ApiRequestUtility.IsUnauthorized(request))
        {
            return false;
        }

        ClearRareBroadcastState();
        GameSession.Clear();
        SceneManager.LoadScene(LaunchSceneName);
        return true;
    }

    private static T ParseJson<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception)
        {
            return null;
        }
    }

#pragma warning disable 0649
    [Serializable]
    private class MapDataApiResponse : BasicApiResponse
    {
        public MapDataResponse data;
    }

    [Serializable]
    private class MapDataResponse
    {
        public int playerId;
        public int sceneId;
        public int scene_id;
        public int width;
        public int height;
        public MapLandResponse[] lands;
        public MapBuildingResponse[] buildings;
    }

    [Serializable]
    private class MapLandResponse
    {
        public long playerLandId;
        public int landId;
        public string landType;
        public int positionX;
        public int positionY;
    }

    [Serializable]
    private class MapBuildingResponse
    {
        public long playerBuildingId;
        public int buildingId;
        public long playerLandId;
        public int level;
        public string status;
    }

    private static string AppendSceneIdQuery(string path)
    {
        int sceneId = PlayerPrefs.GetInt("weijun.backend.sceneId", 0);
        if (sceneId <= 0) return path;
        return path + (path.Contains("?") ? "&" : "?") + "sceneId=" + sceneId;
    }

    [Serializable]
    private class PaymentOrderCreateRequest
    {
        public int playerId;
        public string clientOrderId;
        public string productCode;
    }

    [Serializable]
    private class PaymentOrderCreateApiResponse : BasicApiResponse
    {
        public PaymentOrderCreateData data;
    }

    [Serializable]
    private class PaymentOrderCreateData
    {
        public PaymentOrderResponse order;
        public string paymentProvider;
        public bool payReady;
        public PaymentParameters payParams;
        public string message;
    }

    [Serializable]
    private class PaymentOrderApiResponse : BasicApiResponse
    {
        public PaymentOrderResponse data;
    }

    [Serializable]
    private class PaymentOrderResponse
    {
        public string orderNo;
        public int playerId;
        public string clientOrderId;
        public string productCode;
        public string productName;
        public int priceFen;
        public string currency;
        public string rewardItemKey;
        public int rewardAmount;
        public string status;
        public string paidTime;
        public string deliveredTime;
        public string expireTime;
        public string closeTime;
        public string createTime;
    }

    [Serializable]
    private class PaymentParameters
    {
        public string appId;
        public string timeStamp;
        public string nonceStr;
        public string package;
        public string signType;
        public string paySign;
    }
#pragma warning restore 0649
}
