using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public partial class HomePageUI
{
    private CustomerServiceEntryData customerServiceEntry;
    private bool customerServiceRequestRunning;
    private bool customerServiceSdkRetryAttempted;
    private Button customerServiceButton;
    private Transform customerServiceRoot;
    private const int CustomerServiceRetryLimit = 3;
    private static readonly float[] CustomerServiceRetryDelays = { 2f, 5f, 15f };

    private void ConfigureCustomerServiceButton(Transform root)
    {
        if (root == null) return;

        customerServiceRoot = FindSceneTransform(root, "SettingsPanel");
        if (customerServiceRoot == null) customerServiceRoot = root;

        customerServiceButton = FindSceneComponent<Button>(customerServiceRoot, "SettingsCustomerServiceButton");
        if (customerServiceButton == null)
        {
            customerServiceButton = CreateButton(
                customerServiceRoot,
                "SettingsCustomerServiceButton",
                "联系客服",
                new Color(0.22f, 0.52f, 0.68f, 1f),
                Color.white);
            SetRect(
                customerServiceButton.GetComponent<RectTransform>(),
                TopLeftAnchor(),
                TopLeftAnchor(),
                TopLeftAnchor(),
                new Vector2(560f, -724f),
                new Vector2(178f, 52f));
            ConfigureClearButtonLabel(customerServiceButton, 18);
        }

        bool canUseCustomerService = HasAuthSession() && WeChatPlatformBridge.IsAvailable;
        bool entryClosed = customerServiceEntry != null && !customerServiceEntry.enabled;
        customerServiceButton.interactable = canUseCustomerService && !entryClosed && !customerServiceRequestRunning;
        SetSceneButtonText(
            customerServiceButton,
            customerServiceRequestRunning
                ? "读取客服配置..."
                : !HasAuthSession()
                    ? "登录后联系客服"
                    : !WeChatPlatformBridge.IsAvailable
                        ? "微信环境可用"
                        : entryClosed
                            ? "客服暂未开放"
                        : IsCustomerServiceEntryFresh()
                            ? "联系客服"
                            : "刷新客服入口");
        BindSceneButton(customerServiceButton, BeginCustomerService);

        if (HasAuthSession() && !IsCustomerServiceEntryFresh() && !customerServiceRequestRunning)
        {
            StartCoroutine(LoadCustomerServiceEntry(false));
        }
    }

    private void BeginCustomerService()
    {
        if (!HasAuthSession())
        {
            SetCustomerServiceStatus("请先登录账号");
            return;
        }
        if (!WeChatPlatformBridge.IsAvailable)
        {
            SetCustomerServiceStatus("客服入口只能在微信小游戏环境中使用");
            return;
        }
        if (!IsCustomerServiceEntryFresh())
        {
            SetCustomerServiceStatus("客服入口配置已过期，请稍后再试");
            if (!customerServiceRequestRunning) StartCoroutine(LoadCustomerServiceEntry(true));
            return;
        }

        customerServiceSdkRetryAttempted = false;
        WeChatPlatformBridge.OpenCustomerServiceConversation(
            this,
            customerServiceEntry.sessionFrom,
            customerServiceEntry.showMessageCard,
            customerServiceEntry.sendMessageTitle,
            customerServiceEntry.sendMessagePath,
            customerServiceEntry.sendMessageImg,
            customerServiceEntry.minimumBaseLibraryVersion);
    }

    private IEnumerator LoadCustomerServiceEntry(bool force)
    {
        if (customerServiceRequestRunning || (!force && IsCustomerServiceEntryFresh())) yield break;
        if (!HasAuthSession())
        {
            SetCustomerServiceStatus("请先登录账号");
            yield break;
        }
        customerServiceRequestRunning = true;
        RefreshCustomerServiceButtonState();
        int retryAttempt = 0;
        while (true)
        {
            UnityWebRequest request = CreateAuthorizedGet(
                "/customer-service/entry?scene=PLAYER_SUPPORT&page=pages/index/index");
            if (request == null)
            {
                customerServiceRequestRunning = false;
                SetCustomerServiceStatus(requestCreationError);
                RefreshCustomerServiceButtonState();
                yield break;
            }

            bool retryScheduled = false;
            using (request)
            {
                yield return request.SendWebRequest();
                if (HandleUnauthorizedResponse(request))
                {
                    customerServiceRequestRunning = false;
                    yield break;
                }

                CustomerServiceEntryApiResponse response = ParseJson<CustomerServiceEntryApiResponse>(request.downloadHandler.text);
                if (request.result == UnityWebRequest.Result.Success
                    && response != null
                    && response.code == 200
                    && response.data != null)
                {
                    customerServiceEntry = response.data;
                    customerServiceSdkRetryAttempted = false;
                    if (!customerServiceEntry.enabled)
                    {
                        SetCustomerServiceStatus("客服入口当前未开放");
                    }
                    else
                    {
                        SetCustomerServiceStatus("客服入口已准备就绪");
                    }
                    customerServiceRequestRunning = false;
                    RefreshCustomerServiceButtonState();
                    yield break;
                }

                customerServiceEntry = null;
                string message = response != null && response.code == 409
                    ? "当前账号尚未绑定微信，请先完成微信绑定"
                    : ApiRequestUtility.GetErrorMessage(request, "客服入口读取失败");
                bool shouldRetry = IsCustomerServiceRetryable(request, response);
                if (shouldRetry && retryAttempt < CustomerServiceRetryLimit)
                {
                    float delay = CustomerServiceRetryDelays[Mathf.Min(retryAttempt, CustomerServiceRetryDelays.Length - 1)];
                    retryAttempt++;
                    retryScheduled = true;
                    SetCustomerServiceStatus(message + $"，{delay:0} 秒后重试 ({retryAttempt}/{CustomerServiceRetryLimit})");
                    yield return new WaitForSecondsRealtime(delay);
                }
                else
                {
                    SetCustomerServiceStatus(message);
                }
            }

            if (!retryScheduled)
            {
                customerServiceRequestRunning = false;
                RefreshCustomerServiceButtonState();
                yield break;
            }
        }
    }

    private static bool IsCustomerServiceRetryable(
        UnityWebRequest request,
        CustomerServiceEntryApiResponse response)
    {
        if (request == null)
        {
            return false;
        }

        return request.result == UnityWebRequest.Result.ConnectionError
            || request.result == UnityWebRequest.Result.DataProcessingError
            || request.responseCode == 429
            || request.responseCode >= 500
            || (response == null && request.result != UnityWebRequest.Result.Success);
    }

    private bool IsCustomerServiceEntryFresh()
    {
        if (customerServiceEntry == null || !customerServiceEntry.enabled || string.IsNullOrEmpty(customerServiceEntry.sessionFrom))
        {
            return false;
        }
        if (string.IsNullOrEmpty(customerServiceEntry.sessionFromExpiresAt)) return true;
        if (!DateTimeOffset.TryParse(customerServiceEntry.sessionFromExpiresAt, out DateTimeOffset expiresAt)) return false;
        return expiresAt > DateTimeOffset.UtcNow.AddSeconds(30);
    }

    private void RefreshCustomerServiceButtonState()
    {
        if (customerServiceButton == null) return;
        bool fresh = IsCustomerServiceEntryFresh();
        bool entryClosed = customerServiceEntry != null && !customerServiceEntry.enabled;
        customerServiceButton.interactable = HasAuthSession()
            && WeChatPlatformBridge.IsAvailable
            && !entryClosed
            && !customerServiceRequestRunning;
        SetSceneButtonText(
            customerServiceButton,
            customerServiceRequestRunning
                ? "读取客服配置..."
                : !HasAuthSession()
                    ? "登录后联系客服"
                    : !WeChatPlatformBridge.IsAvailable
                        ? "微信环境可用"
                        : entryClosed
                            ? "客服暂未开放"
                        : fresh ? "联系客服" : "刷新客服入口");
    }

    private void SetCustomerServiceStatus(string message)
    {
        if (customerServiceRoot != null)
        {
            SetSceneText(customerServiceRoot, "SettingsStatus", message);
        }
        SetStatus(message);
    }

    public void OnWeChatCustomerServiceOpened(string message)
    {
        SetCustomerServiceStatus("客服会话已打开");
    }

    public void OnWeChatCustomerServiceFailed(string message)
    {
        string failureMessage = string.IsNullOrEmpty(message) ? "客服会话打开失败" : message;
        if (!customerServiceSdkRetryAttempted && !customerServiceRequestRunning && HasAuthSession())
        {
            customerServiceSdkRetryAttempted = true;
            SetCustomerServiceStatus(failureMessage + "，正在刷新客服会话…");
            StartCoroutine(LoadCustomerServiceEntry(true));
            return;
        }

        SetCustomerServiceStatus(failureMessage + "，请稍后重试");
        RefreshCustomerServiceButtonState();
    }

    [Serializable]
    private sealed class CustomerServiceEntryApiResponse : BasicApiResponse
    {
        public CustomerServiceEntryData data;
    }

    [Serializable]
    private sealed class CustomerServiceEntryData
    {
        public bool enabled;
        public string sessionFrom;
        public string sessionFromExpiresAt;
        public bool showMessageCard;
        public string sendMessageTitle;
        public string sendMessagePath;
        public string sendMessageImg;
        public string minimumBaseLibraryVersion;
    }
}
