using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public partial class HomePageUI
{
    private readonly Dictionary<string, Button> mapSelectorButtons = new Dictionary<string, Button>();
    private GameObject mapSelectorObject;
    private string requestCreationError = string.Empty;
    private bool mapEntryRunning;
    private bool logoutRunning;
    private bool paymentRequestRunning;
    private bool paymentPolling;
    private string pendingPaymentOrderNo = string.Empty;

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
            card.onClick.AddListener(() => SelectArea(area));
            mapSelectorButtons[area.landType] = card;

            Image accent = CreateImage("Accent", card.transform, area.color);
            accent.raycastTarget = false;
            SetRect(accent.rectTransform, TopLeftAnchor(), new Vector2(1f, 1f), TopLeftAnchor(), Vector2.zero, new Vector2(0f, 12f));

            Text areaTitle = CreateText("AreaTitle", card.transform, area.name, 25, FontStyle.Bold, InkColor, TextAnchor.MiddleCenter);
            SetRect(areaTitle.rectTransform, TopLeftAnchor(), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(-20f, 42f));

            Text description = CreateText("AreaDescription", card.transform, area.description, 15, FontStyle.Normal, new Color(0.32f, 0.25f, 0.19f, 1f), TextAnchor.UpperCenter);
            description.horizontalOverflow = HorizontalWrapMode.Wrap;
            description.verticalOverflow = VerticalWrapMode.Truncate;
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
        UnityWebRequest request = CreateAuthorizedGet($"/land/map/{playerId}");
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
        SceneManager.LoadScene(GetSelectedModeSceneName());
    }

    private IEnumerator LogoutAndReturnToLogin()
    {
        logoutRunning = true;
        StopRealtimeConnection();
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
        if (paymentRequestRunning || product == null || string.IsNullOrWhiteSpace(product.productCode))
        {
            SetStatus("支付商品信息无效，请刷新商品列表后重试。");
            return;
        }

        StartCoroutine(CreatePaymentOrder(product, productName));
    }

    private IEnumerator CreatePaymentOrder(PaymentProductResponse product, string productName)
    {
        paymentRequestRunning = true;
        string clientOrderId = "unity-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
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
                SetStatus("创建订单失败：" + ApiRequestUtility.GetErrorMessage(request, "请求失败"));
                yield break;
            }

            PaymentOrderCreateApiResponse response = ParseJson<PaymentOrderCreateApiResponse>(request.downloadHandler.text);
            if (response == null || response.code != 200 || response.data == null || response.data.order == null)
            {
                SetStatus("创建订单失败：" + (response == null ? "返回数据格式错误" : GetApiError(response)));
                yield break;
            }

            pendingPaymentOrderNo = response.data.order.orderNo ?? string.Empty;
            if (!response.data.payReady)
            {
                SetStatus($"订单 {pendingPaymentOrderNo} 已创建；当前为后端本地订单测试模式，不会发放资源。");
                yield break;
            }

            if (response.data.payParams == null)
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
        if (paymentPolling || string.IsNullOrEmpty(pendingPaymentOrderNo))
        {
            return;
        }

        StartCoroutine(PollPaymentOrder(pendingPaymentOrderNo));
    }

    public void OnWeChatPaymentFailed(string message)
    {
        paymentRequestRunning = false;
        paymentPolling = false;
        SetStatus("微信支付未完成：" + (string.IsNullOrEmpty(message) ? "用户取消或支付失败" : message));
    }

    private IEnumerator PollPaymentOrder(string orderNo)
    {
        paymentPolling = true;
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
                        string status = response.data.status ?? string.Empty;
                        SetStatus($"订单 {orderNo}：{status}");
                        if (status == "DELIVERED")
                        {
                            paymentPolling = false;
                            StartCoroutine(RefreshAccountSnapshot("支付已到账，资源已从服务器刷新。"));
                            yield break;
                        }

                        if (status == "CLOSED" || status == "FAILED" || status == "REFUNDED")
                        {
                            paymentPolling = false;
                            yield break;
                        }
                    }
                }
            }

            yield return new WaitForSecondsRealtime(2f);
        }

        paymentPolling = false;
        SetStatus("支付结果仍在处理中，请稍后重新打开充值面板刷新状态。");
    }

    private IEnumerator RefreshAccountSnapshot(string successMessage)
    {
        UnityWebRequest request = CreateAuthorizedGet("/user/me");
        if (request == null)
        {
            SetStatus(requestCreationError);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                yield break;
            }

            UserProfileApiResponse response = ParseJson<UserProfileApiResponse>(request.downloadHandler.text);
            if (request.result == UnityWebRequest.Result.Success && response != null && response.code == 200 && response.data != null)
            {
                SaveUserProfile(response.data);
                if (!string.IsNullOrEmpty(successMessage))
                {
                    SetStatus(successMessage);
                }
            }
            else
            {
                SetStatus("订单已完成，但资源刷新失败：" + ApiRequestUtility.GetErrorMessage(request, "请重新登录后查看"));
            }
        }
    }

    private bool TryCreateAuthorizedPost(string path, string json, out UnityWebRequest request, out string error)
    {
        return ApiRequestUtility.TryCreateJsonRequest(
            path,
            UnityWebRequest.kHttpVerbPOST,
            json,
            true,
            out request,
            out error);
    }

    private bool HandleUnauthorizedResponse(UnityWebRequest request)
    {
        if (!ApiRequestUtility.IsUnauthorized(request))
        {
            return false;
        }

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
