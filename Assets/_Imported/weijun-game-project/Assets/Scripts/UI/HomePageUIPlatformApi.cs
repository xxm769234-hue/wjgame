using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Text = TMPro.TMP_Text;
using InputField = TMPro.TMP_InputField;

public partial class HomePageUI
{
    private RealtimeNotification[] currentNotifications = new RealtimeNotification[0];
    private bool mailShowingNotifications;
    private bool notificationListRunning;
    private bool notificationMutationRunning;
    private int notificationPageIndex;
    private long selectedNotificationId;
    private bool notificationNavigationRunning;

    private SubscribeTemplateResponse[] pendingSubscribeTemplates = new SubscribeTemplateResponse[0];
    private bool subscribeRequestRunning;

    private GameObject refundPanelObject;
    private RefundResponse[] currentRefunds = new RefundResponse[0];
    private readonly List<long> refundPageCursors = new List<long> { 0 };
    private int refundPageIndex;
    private long refundNextBeforeId;
    private bool refundRequestRunning;
    private string refundStatusMessage = string.Empty;
    private string pendingRefundIdempotencyKey = string.Empty;
    private string pendingRefundOrderNo = string.Empty;
    private string pendingRefundReason = string.Empty;
    private readonly HashSet<string> refundAccountSyncKeys = new HashSet<string>();

    private void ShowNotificationCenter()
    {
        if (!HasAuthSession())
        {
            SetStatus("请先登录账号后读取服务器站内提醒。 ");
            return;
        }

        HomePageScenePanel panel = OpenRegisteredScenePanel(HomePageScenePanelType.Mail);
        if (panel == null)
        {
            SetLeftContent("站内提醒", "站内提醒面板场景组件未找到。", "无法打开站内提醒");
            return;
        }

        mailShowingNotifications = true;
        mailPanelObject = panel.gameObject;
        RenderNotificationCenter(panel.PanelRoot, "正在读取服务器站内提醒...");
        if (!notificationListRunning)
        {
            StartCoroutine(GetNotificationCenterList());
        }
    }

    private IEnumerator GetNotificationCenterList()
    {
        if (notificationListRunning || !HasAuthSession())
        {
            yield break;
        }

        notificationListRunning = true;
        UnityWebRequest request = CreateAuthorizedGet($"/notification/list/{GameSession.PlayerId}?limit=50");
        if (request == null)
        {
            notificationListRunning = false;
            RenderNotificationCenter(GetNotificationPanelRoot(), requestCreationError);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                notificationListRunning = false;
                yield break;
            }

            NotificationListApiResponse response = ParseJson<NotificationListApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200)
            {
                notificationListRunning = false;
                RenderNotificationCenter(
                    GetNotificationPanelRoot(),
                    "提醒同步失败：" + (response == null
                        ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                        : GetApiError(response)));
                yield break;
            }

            currentNotifications = response.data ?? new RealtimeNotification[0];
            notificationListRunning = false;
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(currentNotifications.Length / 5f));
            notificationPageIndex = Mathf.Clamp(notificationPageIndex, 0, pageCount - 1);
            if (FindNotification(selectedNotificationId) == null)
            {
                selectedNotificationId = currentNotifications.Length > 0 && currentNotifications[0] != null
                    ? currentNotifications[0].id
                    : 0;
            }
            RenderNotificationCenter(GetNotificationPanelRoot(), $"已读取 {currentNotifications.Length} 条站内提醒");
        }
    }

    private Transform GetNotificationPanelRoot()
    {
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Mail);
        return panel != null ? panel.PanelRoot : null;
    }

    private void RenderNotificationCenter(Transform root, string status)
    {
        if (root == null || !mailShowingNotifications)
        {
            return;
        }

        SetSceneText(root, "MailTitle", "站内提醒");
        SetSceneText(root, "MailListTitle", "提醒列表");
        BindSceneButton(FindSceneComponent<Button>(root, "SystemMailCloseButton"), HideNotificationCenter);
        BindSceneButton(FindSceneComponent<Button>(root, "MailTabSystem"), ShowMailContent);
        BindSceneButton(FindSceneComponent<Button>(root, "MailTabReward"), ShowMailContent);
        BindSceneButton(FindSceneComponent<Button>(root, "MailTabNotice"), ShowNotificationCenter);
        SetSceneButtonText(FindSceneComponent<Button>(root, "MailTabNotice"), "提醒");
        SetSceneObjectActive(root, "DeleteMailButton", false);

        int unreadCount = 0;
        for (int i = 0; i < currentNotifications.Length; i++)
        {
            if (currentNotifications[i] != null && !currentNotifications[i].readFlag)
            {
                unreadCount++;
            }
        }

        Button readAllButton = FindSceneComponent<Button>(root, "ClaimAllButton");
        if (readAllButton != null)
        {
            readAllButton.interactable = unreadCount > 0 && !notificationMutationRunning;
        }
        SetSceneButtonText(readAllButton, notificationMutationRunning ? "提交中..." : "全部已读");
        BindSceneButton(readAllButton, MarkAllNotificationsRead);

        int pageCount = Mathf.Max(1, Mathf.CeilToInt(currentNotifications.Length / 5f));
        notificationPageIndex = Mathf.Clamp(notificationPageIndex, 0, pageCount - 1);
        int firstIndex = notificationPageIndex * 5;
        int visibleCount = Mathf.Min(5, Mathf.Max(0, currentNotifications.Length - firstIndex));
        Transform emptyCard = FindSceneTransform(root, "MailEmptyCard");
        if (emptyCard != null)
        {
            emptyCard.gameObject.SetActive(visibleCount == 0);
        }
        SetSceneText(root, "MailEmptyText", "暂无站内提醒");

        RealtimeNotification selected = FindNotification(selectedNotificationId);
        bool selectedOnPage = false;
        for (int i = 0; i < visibleCount; i++)
        {
            if (currentNotifications[firstIndex + i] != null && currentNotifications[firstIndex + i].id == selectedNotificationId)
            {
                selectedOnPage = true;
                break;
            }
        }
        if (!selectedOnPage)
        {
            selected = visibleCount > 0 ? currentNotifications[firstIndex] : null;
            selectedNotificationId = selected != null ? selected.id : 0;
        }

        for (int rowIndex = 0; rowIndex < 5; rowIndex++)
        {
            Button row = FindSceneComponent<Button>(root, "MailItem" + rowIndex);
            if (row == null)
            {
                continue;
            }

            bool visible = rowIndex < visibleCount;
            row.gameObject.SetActive(visible);
            if (!visible)
            {
                continue;
            }

            RealtimeNotification notification = currentNotifications[firstIndex + rowIndex];
            bool isSelected = notification.id == selectedNotificationId;
            Image background = row.GetComponent<Image>();
            if (background != null)
            {
                background.color = isSelected
                    ? new Color(1f, 0.92f, 0.45f, 1f)
                    : new Color(1f, 0.96f, 0.78f, 1f);
            }
            Image unreadMark = FindSceneComponent<Image>(row.transform, "UnreadMark");
            if (unreadMark != null)
            {
                unreadMark.color = notification.readFlag
                    ? new Color(0.62f, 0.56f, 0.48f, 1f)
                    : new Color(0.90f, 0.22f, 0.16f, 1f);
            }
            SetSceneText(row.transform, "MailItemTitle", string.IsNullOrEmpty(notification.title) ? "领地提醒" : notification.title);
            SetSceneText(row.transform, "MailItemMeta", $"{GetNotificationTypeLabel(notification.type)}  {notification.createTime}");
            long notificationId = notification.id;
            BindSceneButton(row, () => SelectNotification(notificationId));
        }

        RenderNotificationDetail(root, selected);
        ConfigureNotificationPaging(root, pageCount);
        SetSceneText(root, "MailStatus", $"未读 {unreadCount} 条 · 第 {notificationPageIndex + 1}/{pageCount} 页 · {status}");
        SetStatus(status);
    }

    private void RenderNotificationDetail(Transform root, RealtimeNotification notification)
    {
        if (notification == null)
        {
            SetSceneText(root, "MailDetailTitle", "系统提示");
            SetSceneText(root, "MailDetailBody", "当前没有可展示的站内提醒。 ");
            SetSceneText(root, "MailRewardTitle", "状态");
            SetRepeatedSceneText(root, "MailRewardText", "暂无");
            Button emptyReadButton = FindSceneComponent<Button>(root, "ClaimMailButton");
            if (emptyReadButton != null) emptyReadButton.interactable = false;
            SetSceneButtonText(emptyReadButton, "标记已读");
            BindSceneButton(emptyReadButton, null);
            ConfigureNotificationAction(root, null);
            return;
        }

        SetSceneText(root, "MailDetailTitle", string.IsNullOrEmpty(notification.title) ? "领地提醒" : notification.title);
        SetSceneText(
            root,
            "MailDetailBody",
            $"{notification.content}\n\n类型：{GetNotificationTypeLabel(notification.type)}\n时间：{notification.createTime}\n状态：{(notification.readFlag ? "已读" : "未读")}");
        SetSceneText(root, "MailRewardTitle", "提醒状态");
        SetRepeatedSceneText(root, "MailRewardText", notification.readFlag ? "已读" : "未读");
        Button readButton = FindSceneComponent<Button>(root, "ClaimMailButton");
        if (readButton != null)
        {
            readButton.interactable = !notification.readFlag && !notificationMutationRunning;
        }
        SetSceneButtonText(readButton, notification.readFlag ? "已读" : "标记已读");
        BindSceneButton(readButton, MarkSelectedNotificationRead);
        ConfigureNotificationAction(root, notification);
    }

    private void ConfigureNotificationAction(Transform root, RealtimeNotification notification)
    {
        if (root == null)
        {
            return;
        }

        Button actionButton = FindSceneComponent<Button>(root, "NotificationActionButton");
        if (actionButton == null)
        {
            Transform parent = FindSceneTransform(root, "MailDetailArea");
            if (parent == null)
            {
                parent = FindSceneTransform(root, "NotificationDetailArea");
            }
            if (parent == null)
            {
                parent = FindSceneTransform(root, "SystemMailPanel");
            }
            if (parent == null)
            {
                parent = root;
            }

            actionButton = CreateButton(
                parent,
                "NotificationActionButton",
                string.Empty,
                new Color(0.25f, 0.52f, 0.64f, 1f),
                Color.white);
            SetRect(
                actionButton.GetComponent<RectTransform>(),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-24f, 22f),
                new Vector2(190f, 48f));
            ConfigureClearButtonLabel(actionButton, 18);
        }

        if (!TryGetNotificationAction(notification, out NotificationPayload payload, out string actionText))
        {
            actionButton.gameObject.SetActive(false);
            BindSceneButton(actionButton, null);
            return;
        }

        actionButton.gameObject.SetActive(true);
        actionButton.interactable = !notificationNavigationRunning && !notificationMutationRunning;
        SetSceneButtonText(actionButton, notificationNavigationRunning ? "处理中..." : actionText);
        BindSceneButton(actionButton, () => StartCoroutine(NavigateNotification(notification, payload)));
    }

    private bool TryGetNotificationAction(
        RealtimeNotification notification,
        out NotificationPayload payload,
        out string actionText)
    {
        payload = null;
        actionText = string.Empty;
        if (notification == null)
        {
            return false;
        }

        payload = ParseNotificationPayload(notification.payload);
        string scene = payload == null ? string.Empty : payload.scene;
        if (string.IsNullOrEmpty(scene))
        {
            scene = notification.type;
            if (payload == null)
            {
                payload = new NotificationPayload();
            }
            payload.scene = scene;
        }

        scene = scene.Trim().ToUpperInvariant();
        switch (scene)
        {
            case "MAIL":
            case "MAIL_RECEIVED":
            case "MAIL_ATTACHMENT_CLAIMED":
                if (payload.ResolvedMailId <= 0L)
                {
                    return false;
                }
                actionText = "查看邮件";
                return true;
            case "PAYMENT_DELIVERED":
                if (string.IsNullOrWhiteSpace(payload.ResolvedOrderNo))
                {
                    return false;
                }
                actionText = "查看充值订单";
                return true;
            case "RESOURCE_READY":
                actionText = "前往经营场景";
                return true;
            case "BUILDING_COMPLETED":
                if (payload.ResolvedPlayerBuildingId <= 0L && payload.ResolvedPlayerLandId <= 0L)
                {
                    return false;
                }
                actionText = "查看建筑";
                return true;
            case "AD_REWARD_CLAIMED":
                actionText = "打开背包";
                return true;
            default:
                return false;
        }
    }

    private static NotificationPayload ParseNotificationPayload(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        NotificationPayload payload = ParseJson<NotificationPayload>(raw);
        if (payload != null)
        {
            return payload;
        }

        string unwrapped = raw.Trim();
        if (unwrapped.Length >= 2 && unwrapped[0] == '"' && unwrapped[unwrapped.Length - 1] == '"')
        {
            unwrapped = unwrapped.Substring(1, unwrapped.Length - 2)
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
            payload = ParseJson<NotificationPayload>(unwrapped);
        }

        return payload;
    }

    private IEnumerator NavigateNotification(RealtimeNotification notification, NotificationPayload payload)
    {
        if (notification == null || payload == null || notificationNavigationRunning)
        {
            yield break;
        }

        notificationNavigationRunning = true;
        ConfigureNotificationAction(GetNotificationPanelRoot(), notification);
        if (!notification.readFlag)
        {
            yield return MarkNotificationRead(notification);
        }

        string scene = string.IsNullOrEmpty(payload.scene)
            ? notification.type
            : payload.scene.Trim().ToUpperInvariant();
        switch (scene)
        {
            case "MAIL":
            case "MAIL_RECEIVED":
            case "MAIL_ATTACHMENT_CLAIMED":
                selectedMailId = payload.ResolvedMailId;
                if (selectedMailId > 0L)
                {
                    ShowMailContent();
                }
                else
                {
                    SetStatus("通知未提供有效邮件编号，无法定位邮件。");
                }
                break;
            case "PAYMENT_DELIVERED":
                SetPendingPaymentOrderFromNotification(payload.ResolvedOrderNo);
                ShowRechargeContent();
                StartCoroutine(RecoverPendingPaymentOrder("正在查询通知中的充值订单..."));
                break;
            case "RESOURCE_READY":
                QueueNotificationSceneTarget(payload);
                HideNotificationCenter();
                SceneManager.LoadScene("Simulation management mode");
                break;
            case "BUILDING_COMPLETED":
                HomePageUINotificationDeepLink.QueueBuilding(
                    payload.ResolvedSceneId,
                    payload.ResolvedPlayerBuildingId,
                    payload.ResolvedPlayerLandId);
                HideNotificationCenter();
                SceneManager.LoadScene("Simulation management mode");
                break;
            case "AD_REWARD_CLAIMED":
                HideNotificationCenter();
                ShowBagContent();
                StartCoroutine(RefreshAccountSnapshot("广告奖励已到账，背包已刷新。"));
                break;
            default:
                SetStatus("该通知暂时没有可用跳转。");
                break;
        }

        notificationNavigationRunning = false;
        ConfigureNotificationAction(GetNotificationPanelRoot(), FindNotification(selectedNotificationId));
    }

    private static void QueueNotificationSceneTarget(NotificationPayload payload)
    {
        if (payload == null)
        {
            return;
        }

        int sceneId = payload.ResolvedSceneId;
        HomePageUINotificationDeepLink.QueueScene(sceneId);
    }

    private void ConfigureNotificationPaging(Transform root, int pageCount)
    {
        Transform panel = FindSceneTransform(root, "SystemMailPanel");
        if (panel == null)
        {
            return;
        }

        Button previous = FindSceneComponent<Button>(panel, "NotificationPreviousButton");
        Button next = FindSceneComponent<Button>(panel, "NotificationNextButton");
        if (previous == null)
        {
            previous = CreateButton(panel, "NotificationPreviousButton", "<", new Color(0.72f, 0.42f, 0.24f, 1f), Color.white);
            SetRect(previous.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(1010f, -700f), new Vector2(48f, 42f));
            ConfigureClearButtonLabel(previous, 22);
        }
        if (next == null)
        {
            next = CreateButton(panel, "NotificationNextButton", ">", new Color(0.72f, 0.42f, 0.24f, 1f), Color.white);
            SetRect(next.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(1068f, -700f), new Vector2(48f, 42f));
            ConfigureClearButtonLabel(next, 22);
        }

        previous.gameObject.SetActive(pageCount > 1);
        next.gameObject.SetActive(pageCount > 1);
        previous.interactable = notificationPageIndex > 0 && !notificationMutationRunning;
        next.interactable = notificationPageIndex + 1 < pageCount && !notificationMutationRunning;
        BindSceneButton(previous, () => ChangeNotificationPage(-1));
        BindSceneButton(next, () => ChangeNotificationPage(1));
    }

    private void ChangeNotificationPage(int offset)
    {
        notificationPageIndex += offset;
        selectedNotificationId = 0;
        RenderNotificationCenter(GetNotificationPanelRoot(), "已切换提醒页");
    }

    private void SelectNotification(long notificationId)
    {
        selectedNotificationId = notificationId;
        RenderNotificationCenter(GetNotificationPanelRoot(), "已选择站内提醒");
    }

    private RealtimeNotification FindNotification(long notificationId)
    {
        for (int i = 0; i < currentNotifications.Length; i++)
        {
            if (currentNotifications[i] != null && currentNotifications[i].id == notificationId)
            {
                return currentNotifications[i];
            }
        }
        return null;
    }

    private void MarkSelectedNotificationRead()
    {
        RealtimeNotification notification = FindNotification(selectedNotificationId);
        if (notification != null && !notification.readFlag && !notificationMutationRunning)
        {
            StartCoroutine(MarkNotificationRead(notification));
        }
    }

    private IEnumerator MarkNotificationRead(RealtimeNotification notification)
    {
        notificationMutationRunning = true;
        RenderNotificationCenter(GetNotificationPanelRoot(), "正在标记提醒已读...");
        NotificationReadRequest payload = new NotificationReadRequest
        {
            playerId = GameSession.PlayerId,
            notificationId = notification.id
        };
        if (!TryCreateAuthorizedPost("/notification/read", JsonUtility.ToJson(payload), out UnityWebRequest request, out string error))
        {
            notificationMutationRunning = false;
            RenderNotificationCenter(GetNotificationPanelRoot(), error);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                notificationMutationRunning = false;
                yield break;
            }

            BasicApiResponse response = ParseJson<BasicApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200)
            {
                notificationMutationRunning = false;
                RenderNotificationCenter(
                    GetNotificationPanelRoot(),
                    "标记已读失败：" + (response == null
                        ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                        : GetApiError(response)));
                yield break;
            }
        }

        notification.readFlag = true;
        notificationMutationRunning = false;
        RenderNotificationCenter(GetNotificationPanelRoot(), "提醒已标记为已读");
    }

    private void MarkAllNotificationsRead()
    {
        if (!notificationMutationRunning)
        {
            StartCoroutine(MarkAllNotificationsReadRequest());
        }
    }

    private IEnumerator MarkAllNotificationsReadRequest()
    {
        notificationMutationRunning = true;
        RenderNotificationCenter(GetNotificationPanelRoot(), "正在标记全部提醒已读...");
        NotificationReadAllRequest payload = new NotificationReadAllRequest { playerId = GameSession.PlayerId };
        if (!TryCreateAuthorizedPost("/notification/read_all", JsonUtility.ToJson(payload), out UnityWebRequest request, out string error))
        {
            notificationMutationRunning = false;
            RenderNotificationCenter(GetNotificationPanelRoot(), error);
            yield break;
        }

        int changedCount = 0;
        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                notificationMutationRunning = false;
                yield break;
            }

            NotificationReadAllApiResponse response = ParseJson<NotificationReadAllApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200)
            {
                notificationMutationRunning = false;
                RenderNotificationCenter(
                    GetNotificationPanelRoot(),
                    "全部已读提交失败：" + (response == null
                        ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                        : GetApiError(response)));
                yield break;
            }
            changedCount = response.data;
        }

        for (int i = 0; i < currentNotifications.Length; i++)
        {
            if (currentNotifications[i] != null)
            {
                currentNotifications[i].readFlag = true;
            }
        }
        notificationMutationRunning = false;
        RenderNotificationCenter(GetNotificationPanelRoot(), $"已标记 {changedCount} 条提醒为已读");
    }

    private void HideNotificationCenter()
    {
        mailShowingNotifications = false;
        notificationMutationRunning = false;
        HideSystemMailPanel();
    }

    private static string GetNotificationTypeLabel(string type)
    {
        if (type == "RESOURCE_READY") return "资源可收取";
        if (type == "OPERATION_NOTICE") return "运营提醒";
        if (type == "PAYMENT_DELIVERED") return "支付到账";
        if (type == "AD_REWARD_CLAIMED") return "广告奖励";
        if (type == "BUILDING_COMPLETED") return "建筑完成";
        if (type == "MAIL_RECEIVED") return "新邮件";
        return string.IsNullOrEmpty(type) ? "系统提醒" : type;
    }

    private static void SetRepeatedSceneText(Transform root, string objectName, string firstValue)
    {
        if (root == null)
        {
            return;
        }

        Text[] texts = root.GetComponentsInChildren<Text>(true);
        bool first = true;
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].name == objectName)
            {
                texts[i].text = first ? firstValue : string.Empty;
                first = false;
            }
        }
    }

    private void ConfigureNotificationSubscriptionButton(Transform root)
    {
        Transform panel = FindSceneTransform(root, "SettingsPanel");
        if (panel == null)
        {
            return;
        }

        Button button = FindSceneComponent<Button>(panel, "SettingsSubscribeButton");
        if (button == null)
        {
            button = CreateButton(panel, "SettingsSubscribeButton", "微信通知授权", new Color(0.22f, 0.58f, 0.62f, 1f), Color.white);
            SetRect(button.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(350f, -724f), new Vector2(178f, 52f));
            ConfigureClearButtonLabel(button, 18);
        }

        button.interactable = HasAuthSession() && !subscribeRequestRunning;
        SetSceneButtonText(button, subscribeRequestRunning ? "授权中..." : (HasAuthSession() ? "微信通知授权" : "登录后授权"));
        BindSceneButton(button, BeginNotificationSubscription);
    }

    private void BeginNotificationSubscription()
    {
        if (HasAuthSession() && !subscribeRequestRunning)
        {
            StartCoroutine(GetSubscribeTemplates());
        }
    }

    private IEnumerator GetSubscribeTemplates()
    {
        subscribeRequestRunning = true;
        RefreshSubscriptionSettings("正在读取微信订阅模板...");
        UnityWebRequest request = CreateAuthorizedGet($"/notification/subscribe/templates/{GameSession.PlayerId}");
        if (request == null)
        {
            subscribeRequestRunning = false;
            RefreshSubscriptionSettings(requestCreationError);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                subscribeRequestRunning = false;
                yield break;
            }

            SubscribeTemplateListApiResponse response = ParseJson<SubscribeTemplateListApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200)
            {
                subscribeRequestRunning = false;
                RefreshSubscriptionSettings("订阅模板读取失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response)));
                yield break;
            }

            List<SubscribeTemplateResponse> availableTemplates = new List<SubscribeTemplateResponse>();
            if (response.data != null)
            {
                for (int i = 0; i < response.data.Length; i++)
                {
                    SubscribeTemplateResponse template = response.data[i];
                    if (template != null && !string.IsNullOrEmpty(template.templateId))
                    {
                        availableTemplates.Add(template);
                    }
                }
            }

            pendingSubscribeTemplates = availableTemplates.ToArray();
            if (pendingSubscribeTemplates.Length == 0)
            {
                subscribeRequestRunning = false;
                RefreshSubscriptionSettings("服务器当前没有启用的微信订阅模板");
                yield break;
            }
        }

        string[] templateIds = new string[pendingSubscribeTemplates.Length];
        for (int i = 0; i < pendingSubscribeTemplates.Length; i++)
        {
            templateIds[i] = pendingSubscribeTemplates[i].templateId;
        }
        WeChatPlatformBridge.RequestSubscribeMessages(
            this,
            JsonUtility.ToJson(new WeChatSubscribeRequest { templateIds = templateIds }));
    }

    public void OnWeChatSubscribeResult(string json)
    {
        WeChatSubscribeResult result = ParseJson<WeChatSubscribeResult>(json);
        if (result == null || result.results == null)
        {
            subscribeRequestRunning = false;
            RefreshSubscriptionSettings("微信订阅授权结果格式错误");
            return;
        }

        StartCoroutine(SaveSubscribeResults(result.results));
    }

    public void OnWeChatSubscribeFailed(string message)
    {
        subscribeRequestRunning = false;
        RefreshSubscriptionSettings("微信订阅授权失败：" + message);
    }

    private IEnumerator SaveSubscribeResults(WeChatSubscribeResultItem[] results)
    {
        int acceptedCount = 0;
        for (int i = 0; i < pendingSubscribeTemplates.Length; i++)
        {
            SubscribeTemplateResponse template = pendingSubscribeTemplates[i];
            string status = "reject";
            for (int resultIndex = 0; resultIndex < results.Length; resultIndex++)
            {
                if (results[resultIndex] != null && results[resultIndex].templateId == template.templateId)
                {
                    status = results[resultIndex].status;
                    break;
                }
            }

            bool accepted = string.Equals(status, "accept", StringComparison.OrdinalIgnoreCase);
            SubscribeSaveRequest payload = new SubscribeSaveRequest
            {
                playerId = GameSession.PlayerId,
                templateType = template.templateType,
                templateId = template.templateId,
                accepted = accepted
            };
            if (!TryCreateAuthorizedPost("/notification/subscribe/save", JsonUtility.ToJson(payload), out UnityWebRequest request, out string error))
            {
                subscribeRequestRunning = false;
                RefreshSubscriptionSettings(error);
                yield break;
            }

            using (request)
            {
                yield return request.SendWebRequest();
                if (HandleUnauthorizedResponse(request))
                {
                    subscribeRequestRunning = false;
                    yield break;
                }

                BasicApiResponse response = ParseJson<BasicApiResponse>(request.downloadHandler.text);
                if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200)
                {
                    subscribeRequestRunning = false;
                    RefreshSubscriptionSettings("订阅授权结果保存失败：" + (response == null
                        ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                        : GetApiError(response)));
                    yield break;
                }
            }

            if (accepted)
            {
                acceptedCount++;
            }
        }

        subscribeRequestRunning = false;
        pendingSubscribeTemplates = new SubscribeTemplateResponse[0];
        RefreshSubscriptionSettings($"微信通知授权已保存，同意 {acceptedCount} 项");
    }

    private void RefreshSubscriptionSettings(string status)
    {
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Settings);
        if (panel != null && panel.gameObject.activeInHierarchy)
        {
            RefreshSettingsScenePanel(panel.PanelRoot, status);
        }
        SetStatus(status);
    }

    private void ShowRefundCenter()
    {
        if (!HasAuthSession())
        {
            SetStatus("请先登录账号后申请或查询支付退款。 ");
            return;
        }

        HomePageScenePanel scenePanel = GetRegisteredScenePanel(HomePageScenePanelType.Recharge);
        Transform rechargePanel = scenePanel != null ? FindSceneTransform(scenePanel.PanelRoot, "RechargePanel") : null;
        if (rechargePanel == null)
        {
            SetStatus("充值场景组件未找到，无法打开退款中心。 ");
            return;
        }

        EnsureRefundPanel(rechargePanel);
        refundPanelObject.SetActive(true);
        refundPanelObject.transform.SetAsLastSibling();
        refundStatusMessage = PlayerPrefs.GetInt("PlayerEconomyFrozen", 0) == 1
            ? "当前账号经济状态已冻结，请查看退款进度。"
            : "可申请已支付或已发货的钻石订单全额退款。";
        RenderRefundCenter();
        if (!refundRequestRunning)
        {
            StartCoroutine(GetRefundListPage());
        }
    }

    private void EnsureRefundPanel(Transform parent)
    {
        if (refundPanelObject != null)
        {
            return;
        }

        Image overlay = CreateImage("RefundCenterPanel", parent, new Color(0.97f, 0.91f, 0.76f, 1f));
        refundPanelObject = overlay.gameObject;
        SetRect(overlay.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(76f, -88f), new Vector2(1258f, 620f));

        Text title = CreateMailText("RefundCenterTitle", overlay.transform, "退款申请与进度", 30, FontStyle.Bold, new Color(0.38f, 0.17f, 0.07f, 1f), TextAnchor.MiddleLeft);
        SetRect(title.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(30f, -18f), new Vector2(420f, 44f));
        Button close = CreateButton(overlay.transform, "RefundCenterCloseButton", "X", new Color(0.82f, 0.30f, 0.24f, 1f), Color.white);
        SetRect(close.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(1180f, -16f), new Vector2(48f, 48f));
        ConfigureClearButtonLabel(close, 22);

        CreateRuntimeInputField("RefundOrderNoInput", overlay.transform, "支付订单号", new Vector2(30f, -82f), new Vector2(500f, 48f), 64, false);
        CreateRuntimeInputField("RefundReasonInput", overlay.transform, "退款原因（最多80字符）", new Vector2(30f, -146f), new Vector2(500f, 82f), 80, true);
        Button submit = CreateButton(overlay.transform, "RefundSubmitButton", "提交退款申请", new Color(0.78f, 0.31f, 0.23f, 1f), Color.white);
        SetRect(submit.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(30f, -244f), new Vector2(238f, 48f));
        ConfigureClearButtonLabel(submit, 19);

        CreateRuntimeInputField("RefundRequestNoInput", overlay.transform, "退款申请号", new Vector2(30f, -318f), new Vector2(500f, 48f), 64, false);
        Button query = CreateButton(overlay.transform, "RefundQueryButton", "查询单笔进度", new Color(0.26f, 0.55f, 0.58f, 1f), Color.white);
        SetRect(query.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(30f, -382f), new Vector2(238f, 48f));
        ConfigureClearButtonLabel(query, 19);
        Button refresh = CreateButton(overlay.transform, "RefundRefreshButton", "刷新退款列表", new Color(0.42f, 0.58f, 0.31f, 1f), Color.white);
        SetRect(refresh.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(292f, -382f), new Vector2(238f, 48f));
        ConfigureClearButtonLabel(refresh, 19);

        Text status = CreateMailText("RefundStatusText", overlay.transform, string.Empty, 16, FontStyle.Bold, new Color(0.48f, 0.25f, 0.12f, 1f), TextAnchor.UpperLeft);
        SetRect(status.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(30f, -452f), new Vector2(500f, 130f));

        Image list = CreateImage("RefundListPanel", overlay.transform, new Color(1f, 0.97f, 0.86f, 1f));
        SetRect(list.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(566f, -82f), new Vector2(662f, 500f));
        Text listTitle = CreateMailText("RefundListTitle", list.transform, "退款记录", 24, FontStyle.Bold, new Color(0.38f, 0.17f, 0.07f, 1f), TextAnchor.MiddleLeft);
        SetRect(listTitle.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(20f, -12f), new Vector2(300f, 38f));
        Text listBody = CreateMailText("RefundListBody", list.transform, "正在读取退款记录...", 16, FontStyle.Normal, new Color(0.22f, 0.18f, 0.14f, 1f), TextAnchor.UpperLeft);
        SetRect(listBody.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(20f, -62f), new Vector2(622f, 360f));
        Button previous = CreateButton(list.transform, "RefundPreviousButton", "<", new Color(0.68f, 0.47f, 0.25f, 1f), Color.white);
        SetRect(previous.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(448f, -440f), new Vector2(48f, 42f));
        Button next = CreateButton(list.transform, "RefundNextButton", ">", new Color(0.68f, 0.47f, 0.25f, 1f), Color.white);
        SetRect(next.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(558f, -440f), new Vector2(48f, 42f));
        Text page = CreateMailText("RefundPageText", list.transform, "1/1", 17, FontStyle.Bold, new Color(0.38f, 0.22f, 0.12f, 1f), TextAnchor.MiddleCenter);
        SetRect(page.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(496f, -440f), new Vector2(62f, 42f));

        BindSceneButton(close, HideRefundCenter);
        BindSceneButton(submit, SubmitRefundRequest);
        BindSceneButton(query, QueryRefundDetail);
        BindSceneButton(refresh, RefreshRefundListFromFirstPage);
        BindSceneButton(previous, () => ChangeRefundPage(-1));
        BindSceneButton(next, () => ChangeRefundPage(1));

        InputField orderInput = FindSceneComponent<InputField>(overlay.transform, "RefundOrderNoInput");
        if (orderInput != null && !string.IsNullOrEmpty(pendingPaymentOrderNo))
        {
            orderInput.SetTextWithoutNotify(pendingPaymentOrderNo);
        }
    }

    private InputField CreateRuntimeInputField(
        string name,
        Transform parent,
        string placeholderValue,
        Vector2 position,
        Vector2 size,
        int characterLimit,
        bool multiline)
    {
        Image background = CreateImage(name, parent, new Color(1f, 0.98f, 0.90f, 1f));
        SetRect(background.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, size);
        InputField input = background.gameObject.AddComponent<InputField>();
        input.targetGraphic = background;
        input.characterLimit = characterLimit;
        input.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;

        Text text = CreateMailText("Text", background.transform, string.Empty, 17, FontStyle.Normal, new Color(0.18f, 0.15f, 0.12f, 1f), TextAnchor.UpperLeft);
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-24f, -14f));
        Text placeholder = CreateMailText("Placeholder", background.transform, placeholderValue, 17, FontStyle.Normal, new Color(0.50f, 0.45f, 0.38f, 0.72f), TextAnchor.UpperLeft);
        SetRect(placeholder.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-24f, -14f));
        input.textComponent = text;
        input.placeholder = placeholder;
        return input;
    }

    private void RenderRefundCenter()
    {
        if (refundPanelObject == null || !refundPanelObject.activeSelf)
        {
            return;
        }

        Transform root = refundPanelObject.transform;
        Button submit = FindSceneComponent<Button>(root, "RefundSubmitButton");
        Button query = FindSceneComponent<Button>(root, "RefundQueryButton");
        Button refresh = FindSceneComponent<Button>(root, "RefundRefreshButton");
        if (submit != null) submit.interactable = !refundRequestRunning;
        if (query != null) query.interactable = !refundRequestRunning;
        if (refresh != null) refresh.interactable = !refundRequestRunning;
        SetSceneButtonText(submit, refundRequestRunning ? "提交中..." : "提交退款申请");
        SetSceneText(root, "RefundStatusText", refundStatusMessage);
        SetSceneText(root, "RefundListBody", BuildRefundListText());
        SetSceneText(root, "RefundPageText", (refundPageIndex + 1).ToString());

        Button previous = FindSceneComponent<Button>(root, "RefundPreviousButton");
        Button next = FindSceneComponent<Button>(root, "RefundNextButton");
        if (previous != null) previous.interactable = refundPageIndex > 0 && !refundRequestRunning;
        if (next != null) next.interactable = refundNextBeforeId > 0 && !refundRequestRunning;
    }

    private string BuildRefundListText()
    {
        if (currentRefunds == null || currentRefunds.Length == 0)
        {
            return refundRequestRunning ? "正在读取退款记录..." : "当前没有退款记录。";
        }

        List<string> lines = new List<string>();
        for (int i = 0; i < currentRefunds.Length; i++)
        {
            RefundResponse item = currentRefunds[i];
            if (item == null)
            {
                continue;
            }
            lines.Add($"{i + 1}. {item.requestNo}  {item.statusMessage}\n   订单 {item.orderNo} · ￥{item.refundAmountFen / 100f:0.##} · 钻石 {item.rewardAmount}\n   {item.updateTime}");
        }
        return string.Join("\n\n", lines.ToArray());
    }

    private void SubmitRefundRequest()
    {
        if (refundRequestRunning || refundPanelObject == null)
        {
            return;
        }

        InputField orderInput = FindSceneComponent<InputField>(refundPanelObject.transform, "RefundOrderNoInput");
        InputField reasonInput = FindSceneComponent<InputField>(refundPanelObject.transform, "RefundReasonInput");
        string orderNo = orderInput != null ? orderInput.text.Trim() : string.Empty;
        string reason = NormalizeRefundReason(reasonInput != null ? reasonInput.text : string.Empty);
        if (string.IsNullOrEmpty(orderNo) || orderNo.Length > 64)
        {
            refundStatusMessage = "请输入正确的支付订单号。";
            RenderRefundCenter();
            return;
        }
        if (string.IsNullOrEmpty(reason) || reason.Length > 80)
        {
            refundStatusMessage = "退款原因不能为空，压缩空白后最多 80 个字符。";
            RenderRefundCenter();
            return;
        }

        if (pendingRefundOrderNo != orderNo || pendingRefundReason != reason || string.IsNullOrEmpty(pendingRefundIdempotencyKey))
        {
            pendingRefundOrderNo = orderNo;
            pendingRefundReason = reason;
            pendingRefundIdempotencyKey = Guid.NewGuid().ToString();
        }
        StartCoroutine(RequestPaymentRefund(orderNo, reason, pendingRefundIdempotencyKey));
    }

    private IEnumerator RequestPaymentRefund(string orderNo, string reason, string idempotencyKey)
    {
        refundRequestRunning = true;
        refundStatusMessage = "正在提交退款申请...";
        RenderRefundCenter();
        RefundRequest payload = new RefundRequest { orderNo = orderNo, reason = reason };
        if (!TryCreateAuthorizedPost("/payment/refund/request", JsonUtility.ToJson(payload), out UnityWebRequest request, out string error))
        {
            refundRequestRunning = false;
            refundStatusMessage = error;
            RenderRefundCenter();
            yield break;
        }
        request.SetRequestHeader("Idempotency-Key", idempotencyKey);

        RefundResponse created = null;
        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                refundRequestRunning = false;
                yield break;
            }

            RefundApiResponse response = ParseJson<RefundApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                bool hasDefinitiveClientResponse = request.responseCode >= 400 && request.responseCode < 500;
                bool outcomeUncertain = request.result == UnityWebRequest.Result.ConnectionError
                    || request.result == UnityWebRequest.Result.DataProcessingError
                    || request.responseCode >= 500
                    || (!hasDefinitiveClientResponse && response == null);
                bool definitiveFailure = !outcomeUncertain
                    && response != null
                    && response.code != 200;
                if (definitiveFailure)
                {
                    pendingRefundIdempotencyKey = string.Empty;
                    pendingRefundOrderNo = string.Empty;
                    pendingRefundReason = string.Empty;
                }
                refundRequestRunning = false;
                string failure = response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response);
                refundStatusMessage = definitiveFailure
                    ? "退款申请失败：" + failure
                    : "网络状态未知，再次提交相同订单与原因会复用同一幂等键：" + failure;
                RenderRefundCenter();
                yield break;
            }
            created = response.data;
        }

        pendingRefundIdempotencyKey = string.Empty;
        pendingRefundOrderNo = string.Empty;
        pendingRefundReason = string.Empty;
        refundRequestRunning = false;
        InputField requestInput = FindSceneComponent<InputField>(refundPanelObject.transform, "RefundRequestNoInput");
        if (requestInput != null)
        {
            requestInput.SetTextWithoutNotify(created.requestNo);
        }
        refundStatusMessage = $"退款申请已提交：{created.requestNo}\n{created.statusMessage}";
        refundPageCursors.Clear();
        refundPageCursors.Add(0);
        refundPageIndex = 0;
        yield return GetRefundListPage();
    }

    private void QueryRefundDetail()
    {
        if (refundRequestRunning || refundPanelObject == null)
        {
            return;
        }

        InputField input = FindSceneComponent<InputField>(refundPanelObject.transform, "RefundRequestNoInput");
        string requestNo = input != null ? input.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(requestNo))
        {
            refundStatusMessage = "请输入退款申请号。";
            RenderRefundCenter();
            return;
        }
        StartCoroutine(GetRefundDetail(requestNo));
    }

    private IEnumerator GetRefundDetail(string requestNo)
    {
        refundRequestRunning = true;
        refundStatusMessage = "正在查询退款进度...";
        RenderRefundCenter();
        UnityWebRequest request = CreateAuthorizedGet("/payment/refund/" + UnityWebRequest.EscapeURL(requestNo));
        if (request == null)
        {
            refundRequestRunning = false;
            refundStatusMessage = requestCreationError;
            RenderRefundCenter();
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                refundRequestRunning = false;
                yield break;
            }

            RefundApiResponse response = ParseJson<RefundApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                refundRequestRunning = false;
                refundStatusMessage = "退款进度查询失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response));
                RenderRefundCenter();
                yield break;
            }

            RefundResponse item = response.data;
            refundRequestRunning = false;
            refundStatusMessage = $"{item.requestNo}\n{item.statusMessage}\n订单：{item.orderNo}\n更新时间：{item.updateTime}";
            RenderRefundCenter();
            RefreshAccountForRefundStatus(item);
        }
    }

    private void RefreshRefundListFromFirstPage()
    {
        if (refundRequestRunning)
        {
            return;
        }
        refundPageCursors.Clear();
        refundPageCursors.Add(0);
        refundPageIndex = 0;
        StartCoroutine(GetRefundListPage());
    }

    private void ChangeRefundPage(int offset)
    {
        if (refundRequestRunning)
        {
            return;
        }

        if (offset > 0 && refundNextBeforeId > 0)
        {
            if (refundPageIndex + 1 >= refundPageCursors.Count)
            {
                refundPageCursors.Add(refundNextBeforeId);
            }
            refundPageIndex++;
        }
        else if (offset < 0 && refundPageIndex > 0)
        {
            refundPageIndex--;
        }
        StartCoroutine(GetRefundListPage());
    }

    private IEnumerator GetRefundListPage()
    {
        if (refundRequestRunning)
        {
            yield break;
        }

        refundRequestRunning = true;
        RenderRefundCenter();
        long cursor = refundPageIndex < refundPageCursors.Count ? refundPageCursors[refundPageIndex] : 0;
        string path = cursor > 0
            ? $"/payment/refund/list?limit=20&beforeId={cursor}"
            : "/payment/refund/list?limit=20";
        UnityWebRequest request = CreateAuthorizedGet(path);
        if (request == null)
        {
            refundRequestRunning = false;
            refundStatusMessage = requestCreationError;
            RenderRefundCenter();
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                refundRequestRunning = false;
                yield break;
            }

            RefundListApiResponse response = ParseJson<RefundListApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                refundRequestRunning = false;
                refundStatusMessage = "退款列表读取失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response));
                RenderRefundCenter();
                yield break;
            }

            currentRefunds = response.data.items ?? new RefundResponse[0];
            refundNextBeforeId = response.data.nextBeforeId;
            if (refundNextBeforeId > 0 && refundPageIndex + 1 < refundPageCursors.Count)
            {
                refundPageCursors[refundPageIndex + 1] = refundNextBeforeId;
            }
            refundRequestRunning = false;
            if (string.IsNullOrEmpty(refundStatusMessage))
            {
                refundStatusMessage = "退款记录已刷新。";
            }
            RenderRefundCenter();
            for (int i = 0; i < currentRefunds.Length; i++)
            {
                RefreshAccountForRefundStatus(currentRefunds[i]);
            }
        }
    }

    private void RefreshAccountForRefundStatus(RefundResponse refund)
    {
        if (refund == null || string.IsNullOrEmpty(refund.requestNo))
        {
            return;
        }

        string status = refund.status ?? string.Empty;
        bool requiresAccountRefresh = status == "APPROVED"
            || status == "PROCESSING"
            || status == "MANUAL_REVIEW"
            || status == "SUCCESS";
        if (!requiresAccountRefresh)
        {
            return;
        }

        string syncKey = refund.requestNo + "|" + status;
        if (!refundAccountSyncKeys.Add(syncKey))
        {
            return;
        }

        string message = status == "SUCCESS"
            ? "退款已完成，账户资源已从服务器刷新。"
            : "退款处理中，账户经济状态已从服务器刷新。";
        StartCoroutine(RefreshAccountSnapshot(message));
    }

    private void HideRefundCenter()
    {
        if (refundPanelObject != null)
        {
            refundPanelObject.SetActive(false);
        }
    }

    private static string NormalizeRefundReason(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string[] parts = value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts);
    }

#pragma warning disable 0649
    [Serializable]
    private class NotificationReadRequest
    {
        public int playerId;
        public long notificationId;
    }

    [Serializable]
    private class NotificationReadAllRequest
    {
        public int playerId;
    }

    [Serializable]
    private class NotificationReadAllApiResponse : BasicApiResponse
    {
        public int data;
    }

    [Serializable]
    private class NotificationPayload
    {
        public string scene;
        public long mailId;
        public long mail_id;
        public long playerBuildingId;
        public long player_building_id;
        public long buildingId;
        public long building_id;
        public long sceneId;
        public long scene_id;
        public long playerLandId;
        public long player_land_id;
        public string orderNo;
        public string order_no;
        public string adScene;
        public string ad_scene;
        public string watchId;
        public string watch_id;

        public long ResolvedMailId => mailId > 0L ? mailId : mail_id;
        public long ResolvedPlayerBuildingId => playerBuildingId > 0L ? playerBuildingId : player_building_id;
        public long ResolvedBuildingId => buildingId > 0L ? buildingId : building_id;
        public int ResolvedSceneId
        {
            get
            {
                long value = sceneId > 0L ? sceneId : scene_id;
                return value > int.MaxValue ? 0 : (int)value;
            }
        }
        public long ResolvedPlayerLandId => playerLandId > 0L ? playerLandId : player_land_id;
        public string ResolvedOrderNo => string.IsNullOrWhiteSpace(orderNo) ? order_no : orderNo;
    }

    [Serializable]
    private class SubscribeTemplateListApiResponse : BasicApiResponse
    {
        public SubscribeTemplateResponse[] data;
    }

    [Serializable]
    private class SubscribeTemplateResponse
    {
        public string templateType;
        public string templateId;
        public string page;
        public bool enabled;
    }

    [Serializable]
    private class WeChatSubscribeRequest
    {
        public string[] templateIds;
    }

    [Serializable]
    private class WeChatSubscribeResult
    {
        public WeChatSubscribeResultItem[] results;
    }

    [Serializable]
    private class WeChatSubscribeResultItem
    {
        public string templateId;
        public string status;
    }

    [Serializable]
    private class SubscribeSaveRequest
    {
        public int playerId;
        public string templateType;
        public string templateId;
        public bool accepted;
    }

    [Serializable]
    private class RefundRequest
    {
        public string orderNo;
        public string reason;
    }

    [Serializable]
    private class RefundApiResponse : BasicApiResponse
    {
        public RefundResponse data;
    }

    [Serializable]
    private class RefundListApiResponse : BasicApiResponse
    {
        public RefundListData data;
    }

    [Serializable]
    private class RefundListData
    {
        public RefundResponse[] items;
        public long nextBeforeId;
    }

    [Serializable]
    private class RefundResponse
    {
        public string requestNo;
        public string orderNo;
        public string status;
        public string statusMessage;
        public int refundAmountFen;
        public string currency;
        public string rewardItemKey;
        public int rewardAmount;
        public string reason;
        public string requestedTime;
        public string reviewTime;
        public string successTime;
        public string updateTime;
    }
#pragma warning restore 0649
}
