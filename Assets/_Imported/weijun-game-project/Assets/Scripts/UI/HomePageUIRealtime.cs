using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

public partial class HomePageUI
{
    private const float RealtimeHandshakeTimeoutSeconds = 10f;

    private Coroutine realtimeHeartbeatCoroutine;
    private Coroutine realtimeReconnectCoroutine;
    private Coroutine realtimeHandshakeTimeoutCoroutine;
    private Coroutine realtimeFallbackCoroutine;
    private readonly HashSet<string> realtimeEventIds = new HashSet<string>();
    private readonly Queue<string> realtimeEventOrder = new Queue<string>();
    private bool realtimeConnected;
    private bool realtimeTransportOpen;
    private bool realtimeClosing;
    private bool realtimeBackfillRunning;
    private bool realtimeLandRefreshRunning;
    private bool realtimeLandRefreshQueued;
    private bool realtimeLandRefreshScenesQueued;
    private bool realtimeLandRefreshInventoryQueued;
    private string realtimeLandRefreshStatus = string.Empty;
    private bool resumeRealtimeAfterPause;
    private int realtimeHeartbeatSequence;

    private void BeginRealtimeConnection()
    {
        if (!HasAuthSession())
        {
            return;
        }

        if (!RealtimePlatformBridge.IsAvailable)
        {
            BeginRealtimeFallbackPolling();
            return;
        }

        StopRealtimeFallbackPolling();

        if (realtimeTransportOpen || realtimeConnected)
        {
            return;
        }

        if (!ApiRuntimeConfig.TryBuildRealtimeUrl(out string url, out string error))
        {
            SetStatus(error);
            return;
        }

        realtimeClosing = false;
        realtimeTransportOpen = true;
        RealtimePlatformBridge.Connect(this, url, "Bearer " + GameSession.Token);
    }

    public void OnRealtimeOpened(string message)
    {
        realtimeTransportOpen = true;
        if (!realtimeConnected && realtimeHandshakeTimeoutCoroutine == null)
        {
            realtimeHandshakeTimeoutCoroutine = StartCoroutine(WaitForRealtimeHandshake());
        }
    }

    public void OnRealtimeMessage(string json)
    {
        if (!HasAuthSession())
        {
            StopRealtimeConnection();
            return;
        }

        RealtimeEvent realtimeEvent = ParseJson<RealtimeEvent>(json);
        if (realtimeEvent == null || string.IsNullOrEmpty(realtimeEvent.type))
        {
            return;
        }

        if (!string.IsNullOrEmpty(realtimeEvent.eventId))
        {
            if (!realtimeEventIds.Add(realtimeEvent.eventId))
            {
                return;
            }

            realtimeEventOrder.Enqueue(realtimeEvent.eventId);
            while (realtimeEventOrder.Count > 512)
            {
                string oldestEventId = realtimeEventOrder.Dequeue();
                realtimeEventIds.Remove(oldestEventId);
            }
        }

        if (realtimeEvent.type == "CONNECTED")
        {
            ConfirmRealtimeHandshake(realtimeEvent);
            return;
        }

        bool isPublicBroadcast = realtimeEvent.type == "CARD_DRAW_RARE_BROADCAST";
        if (!realtimeConnected || (!isPublicBroadcast && !IsCurrentPlayerRealtimeEvent(realtimeEvent)))
        {
            return;
        }

        if (realtimeEvent.type == "PONG")
        {
            return;
        }

        if (realtimeEvent.type == "ANTI_ADDICTION_WARNING")
        {
            AntiAddictionWarningRealtimeEvent warningEvent = ParseJson<AntiAddictionWarningRealtimeEvent>(json);
            if (warningEvent != null && warningEvent.data != null)
            {
                PlayerComplianceGuard.ShowRealtimeWarning(
                    warningEvent.data.warningMinutes,
                    warningEvent.data.remainingSeconds,
                    warningEvent.data.message);
            }
            return;
        }

        if (realtimeEvent.type == "ANTI_ADDICTION_FORCED_OFFLINE")
        {
            AntiAddictionForcedOfflineRealtimeEvent forcedOfflineEvent = ParseJson<AntiAddictionForcedOfflineRealtimeEvent>(json);
            StopRealtimeConnection();
            PlayerComplianceGuard.ForceOffline(forcedOfflineEvent == null || forcedOfflineEvent.data == null
                ? "当前防沉迷规则不允许继续游戏。"
                : forcedOfflineEvent.data.message);
            return;
        }

        if (realtimeEvent.type == "FRIEND_PRESENCE_CHANGED")
        {
            FriendPresenceRealtimeEvent presenceEvent = ParseJson<FriendPresenceRealtimeEvent>(json);
            ApplyV38FriendPresence(presenceEvent == null ? null : presenceEvent.data);
            return;
        }

        if (realtimeEvent.type == "COIN_SHOP_PURCHASED")
        {
            CoinShopPurchaseRealtimeEvent purchaseEvent = ParseJson<CoinShopPurchaseRealtimeEvent>(json);
            HandleCoinShopPurchaseRealtime(purchaseEvent == null ? null : purchaseEvent.data, realtimeEvent.eventId);
            return;
        }

        if (realtimeEvent.type == "DIAMOND_SHOP_PURCHASED")
        {
            CoinShopPurchaseRealtimeEvent purchaseEvent = ParseJson<CoinShopPurchaseRealtimeEvent>(json);
            HandleDiamondShopPurchaseRealtime(purchaseEvent == null ? null : purchaseEvent.data, realtimeEvent.eventId);
            return;
        }

        if (realtimeEvent.type == "CARD_DRAW_RARE_BROADCAST")
        {
            RareCardDrawRealtimeEvent rareEvent = ParseJson<RareCardDrawRealtimeEvent>(json);
            RareCardDrawData rareData = rareEvent == null ? null : rareEvent.data;
            string reward = rareData == null ? string.Empty : rareData.rewardName;
            if (string.IsNullOrEmpty(reward) && rareData != null)
            {
                reward = rareData.rewardKey;
            }

            string content = rareData == null ? string.Empty : rareData.content;
            if (string.IsNullOrEmpty(content))
            {
                content = string.IsNullOrEmpty(reward) ? "抽奖稀有奖品" : reward;
            }

            string expiresAt = rareData == null ? string.Empty : rareData.expiresAt;
            SetStatus("抽奖稀有奖品广播：" + content
                + (string.IsNullOrEmpty(expiresAt) ? string.Empty : "（有效期至 " + expiresAt + "）"));
            return;
        }

        if (realtimeEvent.type == "PLAYER_PROGRESSION_CHANGED")
        {
            StartCoroutine(RefreshAccountSnapshot("玩家成长数据已更新"));
            return;
        }

        if (realtimeEvent.type == "LAND_SCENE_UNLOCKED" || realtimeEvent.type == "LAND_SCENE_SWITCHED")
        {
            QueueRealtimeLandRefresh(true, true, "经营场景状态已刷新");
            return;
        }

        if (realtimeEvent.type == "LAND_SLOT_STATE_CHANGED" || realtimeEvent.type == "PRODUCTION_COLLECTED")
        {
            QueueRealtimeLandRefresh(false, true, "经营数据已刷新");
            return;
        }

        if (realtimeEvent.type == "PLAYER_ONBOARDING_UPDATED")
        {
            SetStatus("新手引导状态已更新，请按当前提示继续");
            return;
        }

        if (realtimeEvent.type == "PLAYER_NPC_ACQUISITION_RESOLVED")
        {
            PlayerNpcAcquisitionResolvedRealtimeEvent npcEvent = ParseJson<PlayerNpcAcquisitionResolvedRealtimeEvent>(json);
            RefreshRolePanelFromRealtime(npcEvent == null || npcEvent.data == null
                ? string.Empty
                : npcEvent.data.acquisitionId);
            return;
        }

        if (realtimeEvent.type == "NOTIFICATION_CREATED" && realtimeEvent.data != null)
        {
            string title = string.IsNullOrEmpty(realtimeEvent.data.title) ? "收到新的领地提醒" : realtimeEvent.data.title;
            SetStatus(title);

            if (mailShowingNotifications && mailPanelObject != null)
            {
                StartCoroutine(GetNotificationCenterList());
            }

            if (realtimeEvent.data.type == "MAIL_RECEIVED" && mailPanelObject != null)
            {
                StartCoroutine(GetMailList());
            }
            else if (realtimeEvent.data.type == "PAYMENT_DELIVERED")
            {
                StartCoroutine(RefreshAccountSnapshot("支付奖励已到账，资源已刷新。"));
            }
        }
    }

    private void ConfirmRealtimeHandshake(RealtimeEvent realtimeEvent)
    {
        if (!IsCurrentPlayerRealtimeEvent(realtimeEvent))
        {
            Debug.LogWarning("Realtime WebSocket CONNECTED event belongs to a different player.");
            StopRealtimeConnection();
            return;
        }

        if (realtimeConnected)
        {
            return;
        }

        realtimeConnected = true;
        realtimeTransportOpen = true;
        realtimeHeartbeatSequence = 0;
        StopRealtimeHandshakeTimeout();
        if (realtimeReconnectCoroutine != null)
        {
            StopCoroutine(realtimeReconnectCoroutine);
            realtimeReconnectCoroutine = null;
        }

        if (realtimeHeartbeatCoroutine != null)
        {
            StopCoroutine(realtimeHeartbeatCoroutine);
        }
        realtimeHeartbeatCoroutine = StartCoroutine(SendRealtimeHeartbeats());
        RefreshV38FriendPresenceAfterRealtimeConnected();

        if (!realtimeBackfillRunning)
        {
            StartCoroutine(RefreshRealtimeBacklog());
        }
    }

    private static bool IsCurrentPlayerRealtimeEvent(RealtimeEvent realtimeEvent)
    {
        return realtimeEvent != null && realtimeEvent.playerId == GameSession.PlayerId;
    }

    private void QueueRealtimeLandRefresh(bool reloadScenes, bool refreshInventory, string status)
    {
        if (!HasAuthSession())
        {
            return;
        }

        realtimeLandRefreshQueued = true;
        realtimeLandRefreshScenesQueued |= reloadScenes;
        realtimeLandRefreshInventoryQueued |= refreshInventory;
        realtimeLandRefreshStatus = status ?? string.Empty;
        if (!realtimeLandRefreshRunning)
        {
            StartCoroutine(RefreshRealtimeLandState());
        }
    }

    private IEnumerator RefreshRealtimeLandState()
    {
        realtimeLandRefreshRunning = true;
        while (realtimeLandRefreshQueued && HasAuthSession())
        {
            realtimeLandRefreshQueued = false;
            bool reloadScenes = realtimeLandRefreshScenesQueued;
            bool refreshInventory = realtimeLandRefreshInventoryQueued;
            realtimeLandRefreshScenesQueued = false;
            realtimeLandRefreshInventoryQueued = false;

            object service;
            Type serviceType;
            if (!TryGetBackendGameService(out service, out serviceType))
            {
                break;
            }

            if (reloadScenes)
            {
                yield return InvokeBackendServiceCoroutine(service, serviceType, "LoadLandScenes");
            }

            yield return InvokeBackendServiceCoroutine(service, serviceType, "SyncMap");
            yield return InvokeBackendServiceCoroutine(service, serviceType, "RefreshProductions");
            if (refreshInventory)
            {
                yield return InvokeBackendServiceCoroutine(service, serviceType, "RefreshInventory");
            }

            yield return RefreshAccountSnapshot(realtimeLandRefreshStatus);
        }

        realtimeLandRefreshStatus = string.Empty;
        realtimeLandRefreshRunning = false;
    }

    private static bool TryGetBackendGameService(out object service, out Type serviceType)
    {
        service = null;
        serviceType = Type.GetType("BackendGameService, Assembly-CSharp");
        if (serviceType == null)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length && serviceType == null; i++)
            {
                serviceType = assemblies[i].GetType("BackendGameService");
            }
        }

        if (serviceType == null)
        {
            Debug.LogWarning("Realtime refresh could not find BackendGameService.");
            return false;
        }

        PropertyInfo instanceProperty = serviceType.GetProperty(
            "Instance",
            BindingFlags.Public | BindingFlags.Static);
        PropertyInfo authenticatedProperty = serviceType.GetProperty(
            "IsAuthenticated",
            BindingFlags.Public | BindingFlags.Instance);
        service = instanceProperty == null ? null : instanceProperty.GetValue(null, null);
        return service != null
            && authenticatedProperty != null
            && authenticatedProperty.GetValue(service, null) is bool authenticated
            && authenticated;
    }

    private static IEnumerator InvokeBackendServiceCoroutine(object service, Type serviceType, string methodName)
    {
        MethodInfo method = serviceType.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Instance);
        if (method == null)
        {
            Debug.LogWarning("Realtime refresh could not find BackendGameService." + methodName + ".");
            yield break;
        }

        object operation;
        try
        {
            operation = method.Invoke(service, new object[] { null });
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Realtime refresh failed to invoke BackendGameService." + methodName + ": " + exception.Message);
            yield break;
        }

        if (operation != null)
        {
            yield return operation;
        }
    }

    public void OnRealtimeClosed(string message)
    {
        realtimeConnected = false;
        realtimeTransportOpen = false;
        StopRealtimeHandshakeTimeout();
        StopRealtimeHeartbeat();
        ScheduleRealtimeReconnect();
        BeginRealtimeFallbackPolling();
    }

    public void OnRealtimeError(string message)
    {
        realtimeConnected = false;
        realtimeTransportOpen = false;
        StopRealtimeHandshakeTimeout();
        if (!string.IsNullOrEmpty(message)
            && message.IndexOf("cannot send the required Authorization", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            realtimeClosing = true;
            SetStatus(message);
            return;
        }

        if (!realtimeClosing && !string.IsNullOrEmpty(message))
        {
            Debug.LogWarning("实时连接异常：" + message);
        }
        ScheduleRealtimeReconnect();
        BeginRealtimeFallbackPolling();
    }

    private void BeginRealtimeFallbackPolling()
    {
        if (realtimeFallbackCoroutine == null && HasAuthSession() && isActiveAndEnabled)
        {
            realtimeFallbackCoroutine = StartCoroutine(RealtimeFallbackPolling());
        }
    }

    private void StopRealtimeFallbackPolling()
    {
        if (realtimeFallbackCoroutine != null)
        {
            StopCoroutine(realtimeFallbackCoroutine);
            realtimeFallbackCoroutine = null;
        }
    }

    private IEnumerator RealtimeFallbackPolling()
    {
        while (HasAuthSession() && isActiveAndEnabled)
        {
            yield return new WaitForSecondsRealtime(20f);
            if (HasAuthSession() && !realtimeConnected && !realtimeBackfillRunning)
            {
                StartCoroutine(RefreshRealtimeBacklog(true));
                StartCoroutine(RefreshAccountSnapshot(string.Empty));
            }
        }
        realtimeFallbackCoroutine = null;
    }

    private IEnumerator RefreshRealtimeBacklog(bool allowFallback = false)
    {
        if ((!realtimeConnected && !allowFallback) || !HasAuthSession())
        {
            yield break;
        }

        realtimeBackfillRunning = true;
        int playerId = GameSession.PlayerId;

        UnityWebRequest notificationRequest = CreateAuthorizedGet($"/notification/list/{playerId}?limit=50");
        if (notificationRequest != null)
        {
            using (notificationRequest)
            {
                yield return notificationRequest.SendWebRequest();
                if (HandleUnauthorizedResponse(notificationRequest))
                {
                    realtimeBackfillRunning = false;
                    yield break;
                }

                if (notificationRequest.result == UnityWebRequest.Result.Success)
                {
                    NotificationListApiResponse response = ParseJson<NotificationListApiResponse>(notificationRequest.downloadHandler.text);
                    if (response != null && response.code == 200 && response.data != null)
                    {
                        int unreadCount = 0;
                        string latestTitle = string.Empty;
                        for (int i = 0; i < response.data.Length; i++)
                        {
                            RealtimeNotification notification = response.data[i];
                            if (notification != null && !notification.readFlag)
                            {
                                unreadCount++;
                                if (string.IsNullOrEmpty(latestTitle))
                                {
                                    latestTitle = notification.title;
                                }
                            }
                        }

                        if (unreadCount > 0)
                        {
                            SetStatus(string.IsNullOrEmpty(latestTitle)
                                ? $"有 {unreadCount} 条未读领地提醒"
                                : $"{latestTitle}（未读提醒 {unreadCount} 条）");
                        }
                    }
                }
            }
        }

        if ((!realtimeConnected && !allowFallback) || !HasAuthSession())
        {
            realtimeBackfillRunning = false;
            yield break;
        }

        UnityWebRequest mailRequest = CreateAuthorizedGet($"/mail/list/{playerId}?limit=50");
        if (mailRequest != null)
        {
            using (mailRequest)
            {
                yield return mailRequest.SendWebRequest();
                if (HandleUnauthorizedResponse(mailRequest))
                {
                    realtimeBackfillRunning = false;
                    yield break;
                }

                if (mailRequest.result == UnityWebRequest.Result.Success)
                {
                    MailListApiResponse response = ParseJson<MailListApiResponse>(mailRequest.downloadHandler.text);
                    if (response != null && response.code == 200)
                    {
                        CaptureMailList(response.data);
                        if (mailPanelObject != null && mailPanelObject.activeInHierarchy)
                        {
                            ShowSystemMailPanel(currentServerMails, "实时连接已补齐最新邮件");
                        }
                    }
                }
            }
        }

        realtimeBackfillRunning = false;
    }

    private void ScheduleRealtimeReconnect()
    {
        if (!realtimeClosing && HasAuthSession() && isActiveAndEnabled && realtimeReconnectCoroutine == null)
        {
            realtimeReconnectCoroutine = StartCoroutine(ReconnectRealtime());
        }
    }

    private IEnumerator SendRealtimeHeartbeats()
    {
        while (realtimeConnected && !realtimeClosing && HasAuthSession())
        {
            yield return new WaitForSecondsRealtime(25f);
            if (realtimeConnected && !realtimeClosing)
            {
                realtimeHeartbeatSequence++;
                RealtimePlatformBridge.Send($"{{\"type\":\"PING\",\"requestId\":\"unity-{realtimeHeartbeatSequence}\"}}");
            }
        }
        realtimeHeartbeatCoroutine = null;
    }

    private IEnumerator ReconnectRealtime()
    {
        yield return new WaitForSecondsRealtime(5f);
        realtimeReconnectCoroutine = null;
        if (!realtimeClosing && HasAuthSession() && isActiveAndEnabled)
        {
            BeginRealtimeConnection();
        }
    }

    private IEnumerator WaitForRealtimeHandshake()
    {
        yield return new WaitForSecondsRealtime(RealtimeHandshakeTimeoutSeconds);
        realtimeHandshakeTimeoutCoroutine = null;
        if (!realtimeClosing && realtimeTransportOpen && !realtimeConnected)
        {
            Debug.LogWarning("Realtime WebSocket did not receive CONNECTED before the handshake timeout.");
            realtimeTransportOpen = false;
            RealtimePlatformBridge.Close();
            ScheduleRealtimeReconnect();
        }
    }

    private void StopRealtimeConnection()
    {
        realtimeClosing = true;
        realtimeConnected = false;
        realtimeTransportOpen = false;
        StopRealtimeHandshakeTimeout();
        StopRealtimeHeartbeat();
        if (realtimeReconnectCoroutine != null)
        {
            StopCoroutine(realtimeReconnectCoroutine);
            realtimeReconnectCoroutine = null;
        }
        RealtimePlatformBridge.Close();
        StopRealtimeFallbackPolling();
        realtimeLandRefreshQueued = false;
        realtimeLandRefreshScenesQueued = false;
        realtimeLandRefreshInventoryQueued = false;
    }

    private void StopRealtimeHeartbeat()
    {
        if (realtimeHeartbeatCoroutine != null)
        {
            StopCoroutine(realtimeHeartbeatCoroutine);
            realtimeHeartbeatCoroutine = null;
        }
    }

    private void StopRealtimeHandshakeTimeout()
    {
        if (realtimeHandshakeTimeoutCoroutine != null)
        {
            StopCoroutine(realtimeHandshakeTimeoutCoroutine);
            realtimeHandshakeTimeoutCoroutine = null;
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            resumeRealtimeAfterPause = HasAuthSession();
            StopRealtimeConnection();
        }
        else if (resumeRealtimeAfterPause && HasAuthSession() && isActiveAndEnabled)
        {
            resumeRealtimeAfterPause = false;
            StartCoroutine(RefreshServerStamina());
            BeginRealtimeConnection();
        }
    }

    private void OnDestroy()
    {
        StopRealtimeConnection();
        WeChatPlatformBridge.DestroyFriendGiftPage();
        DisposeAvatarCache();
    }

#pragma warning disable 0649
    [Serializable]
    private class RealtimeEvent
    {
        public string type;
        public string requestId;
        public string eventId;
        public int playerId;
        public RealtimeNotification data;
        public string serverTime;
    }

    [Serializable]
    private class RealtimeNotification
    {
        public long id;
        public int playerId;
        public string type;
        public string title;
        public string content;
        public string payload;
        public bool readFlag;
        public string createTime;
    }

    [Serializable]
    private class FriendPresenceRealtimeEvent
    {
        public string type;
        public string eventId;
        public int playerId;
        public FriendPresenceChange data;
        public string serverTime;
    }

    [Serializable]
    private class FriendPresenceChange
    {
        public int playerId;
        public string onlineStatus;
        public long presenceVersion;
        public string lastActiveTime;
        public string lastOfflineTime;
    }

    [Serializable]
    private class AntiAddictionWarningRealtimeEvent
    {
        public string type;
        public string eventId;
        public int playerId;
        public AntiAddictionWarningData data;
        public string serverTime;
    }

    [Serializable]
    private class AntiAddictionWarningData
    {
        public int warningMinutes;
        public int remainingSeconds;
        public string message;
        public string serverTime;
    }

    [Serializable]
    private class AntiAddictionForcedOfflineRealtimeEvent
    {
        public string type;
        public string eventId;
        public int playerId;
        public AntiAddictionForcedOfflineData data;
        public string serverTime;
    }

    [Serializable]
    private class AntiAddictionForcedOfflineData
    {
        public string reason;
        public string message;
        public string serverTime;
    }

    [Serializable]
    private class CoinShopPurchaseRealtimeEvent
    {
        public string type;
        public string eventId;
        public int playerId;
        public CoinShopPurchaseResult data;
        public string serverTime;
    }

    [Serializable]
    private class RareCardDrawRealtimeEvent
    {
        public RareCardDrawData data;
    }

    [Serializable]
    private class RareCardDrawData
    {
        public string rewardName;
        public string rewardKey;
        public string rarity;
        public string content;
        public string expiresAt;
    }

    [Serializable]
    private class PlayerNpcAcquisitionResolvedRealtimeEvent
    {
        public string type;
        public string eventId;
        public int playerId;
        public PlayerNpcAcquisitionResolvedData data;
        public string serverTime;
    }

    [Serializable]
    private class PlayerNpcAcquisitionResolvedData
    {
        public string acquisitionId;
        public int playerId;
        public int npcId;
        public long playerNpcId;
        public string changeType;
        public string sourceType;
        public string sourceRefKey;
        public string occurredTime;
    }

    [Serializable]
    private class NotificationListApiResponse : BasicApiResponse
    {
        public RealtimeNotification[] data;
    }
#pragma warning restore 0649
}
