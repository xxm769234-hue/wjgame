using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public partial class HomePageUI
{
    private Coroutine realtimeHeartbeatCoroutine;
    private Coroutine realtimeReconnectCoroutine;
    private bool realtimeConnected;
    private bool realtimeClosing;
    private bool realtimeBackfillRunning;
    private bool resumeRealtimeAfterPause;

    private void BeginRealtimeConnection()
    {
        if (!HasAuthSession() || !RealtimePlatformBridge.IsAvailable)
        {
            return;
        }

        if (!ApiRuntimeConfig.TryBuildRealtimeUrl(GameSession.Token, out string url, out string error))
        {
            SetStatus(error);
            return;
        }

        realtimeClosing = false;
        RealtimePlatformBridge.Connect(this, url);
    }

    public void OnRealtimeOpened(string message)
    {
        realtimeConnected = true;
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

        if (!realtimeBackfillRunning)
        {
            StartCoroutine(RefreshRealtimeBacklog());
        }
    }

    public void OnRealtimeMessage(string json)
    {
        RealtimeEvent realtimeEvent = ParseJson<RealtimeEvent>(json);
        if (realtimeEvent == null || string.IsNullOrEmpty(realtimeEvent.type))
        {
            return;
        }

        if (realtimeEvent.type == "NOTIFICATION_CREATED" && realtimeEvent.data != null)
        {
            string title = string.IsNullOrEmpty(realtimeEvent.data.title) ? "收到新的领地提醒" : realtimeEvent.data.title;
            SetStatus(title);

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

    public void OnRealtimeClosed(string message)
    {
        realtimeConnected = false;
        StopRealtimeHeartbeat();
        ScheduleRealtimeReconnect();
    }

    public void OnRealtimeError(string message)
    {
        realtimeConnected = false;
        if (!realtimeClosing && !string.IsNullOrEmpty(message))
        {
            Debug.LogWarning("实时连接异常：" + message);
        }
        ScheduleRealtimeReconnect();
    }

    private IEnumerator RefreshRealtimeBacklog()
    {
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

        if (!HasAuthSession())
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
        int sequence = 0;
        while (realtimeConnected && HasAuthSession())
        {
            yield return new WaitForSecondsRealtime(25f);
            if (realtimeConnected)
            {
                sequence++;
                RealtimePlatformBridge.Send($"{{\"type\":\"PING\",\"requestId\":\"unity-{sequence}\"}}");
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

    private void StopRealtimeConnection()
    {
        realtimeClosing = true;
        realtimeConnected = false;
        StopRealtimeHeartbeat();
        if (realtimeReconnectCoroutine != null)
        {
            StopCoroutine(realtimeReconnectCoroutine);
            realtimeReconnectCoroutine = null;
        }
        RealtimePlatformBridge.Close();
    }

    private void StopRealtimeHeartbeat()
    {
        if (realtimeHeartbeatCoroutine != null)
        {
            StopCoroutine(realtimeHeartbeatCoroutine);
            realtimeHeartbeatCoroutine = null;
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
            BeginRealtimeConnection();
        }
    }

    private void OnDestroy()
    {
        StopRealtimeConnection();
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
    private class NotificationListApiResponse : BasicApiResponse
    {
        public RealtimeNotification[] data;
    }
#pragma warning restore 0649
}
