using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public partial class HomePageUI
{
    private const float RealtimeHandshakeTimeoutSeconds = 10f;
    private const float RareBroadcastDisplaySeconds = 5f;
    private const int RareBroadcastHistoryLimit = 512;
    private const int RareBroadcastQueueLimit = 512;

    private Coroutine realtimeHeartbeatCoroutine;
    private Coroutine realtimeReconnectCoroutine;
    private Coroutine realtimeHandshakeTimeoutCoroutine;
    private Coroutine realtimeFallbackCoroutine;
    private readonly HashSet<string> realtimeEventIds = new HashSet<string>();
    private readonly Queue<string> realtimeEventOrder = new Queue<string>();
    private static readonly Dictionary<string, DateTimeOffset> rareBroadcastSeen = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
    private static readonly Queue<RareBroadcastSeen> rareBroadcastSeenOrder = new Queue<RareBroadcastSeen>();
    private static readonly Queue<RareBroadcastQueueItem> rareBroadcastQueue = new Queue<RareBroadcastQueueItem>();
    private static int rareBroadcastPlayerId;
    private Coroutine rareBroadcastPlaybackCoroutine;
    private TMP_Text rareBroadcastText;
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
    private readonly Queue<RareCardDrawBroadcastMessage> rareCardDrawBroadcastQueue = new Queue<RareCardDrawBroadcastMessage>();
    private Coroutine rareCardDrawBroadcastCoroutine;
    private GameObject rareCardDrawBroadcastObject;
    private RectTransform rareCardDrawBroadcastViewport;
    private TextMeshProUGUI rareCardDrawBroadcastText;

    private const int MaxRareCardDrawBroadcastQueueSize = 16;
    private const float RareCardDrawBroadcastSpeed = 82f;

    private void BeginRealtimeConnection()
    {
        if (!HasAuthSession())
        {
            ClearRareBroadcastState();
            return;
        }

        PrepareRareBroadcastSession();
        EnsureRareBroadcastUi();
        StartRareBroadcastPlaybackIfNeeded();

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

        bool isPublicBroadcast = realtimeEvent.type == "CARD_DRAW_RARE_BROADCAST"
            || realtimeEvent.type == "AVATAR_CATALOG_CHANGED";
        bool isAvatarChange = realtimeEvent.type == "AVATAR_CHANGED";
        if (!realtimeConnected
            || (!isPublicBroadcast
                && !IsCurrentPlayerRealtimeEvent(realtimeEvent)
                && (!isAvatarChange || !IsKnownAvatarPlayerEvent(realtimeEvent, json))))
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

        if (realtimeEvent.type == "AVATAR_CHANGED")
        {
            AvatarChangedRealtimeEvent avatarEvent = ParseJson<AvatarChangedRealtimeEvent>(json);
            ApplyAvatarChangedRealtime(avatarEvent == null ? null : avatarEvent.data);
            return;
        }

        if (realtimeEvent.type == "AVATAR_CATALOG_CHANGED")
        {
            AvatarCatalogChangedRealtimeEvent catalogEvent = ParseJson<AvatarCatalogChangedRealtimeEvent>(json);
            if (catalogEvent == null || catalogEvent.data == null)
            {
                Debug.LogWarning("Avatar catalog change event did not contain data.");
                return;
            }

            RefreshAvatarCatalogFromRealtime();
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
            RouteRareCardDrawBroadcast(realtimeEvent, json);
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
            PlayerOnboardingManager.EnsureInstance().HandleRealtimeHint(json);
            return;
        }

        if (realtimeEvent.type == "PLAYER_NPC_ACQUISITION_RESOLVED")
        {
            PlayerNpcAcquisitionResolvedRealtimeEvent npcEvent = ParseJson<PlayerNpcAcquisitionResolvedRealtimeEvent>(json);
            if (npcEvent == null || npcEvent.data == null)
            {
                return;
            }

            int currentPlayerId = GameSession.PlayerId;
            if (currentPlayerId <= 0
                || (npcEvent.playerId > 0 && npcEvent.playerId != currentPlayerId)
                || (npcEvent.data.playerId > 0 && npcEvent.data.playerId != currentPlayerId))
            {
                return;
            }

            RefreshRolePanelFromRealtime(npcEvent.data.acquisitionId);
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
                HandlePaymentDeliveredRealtimeNotification(realtimeEvent.data.payload);
            }
        }
    }

    private void PrepareRareBroadcastSession()
    {
        int playerId = GameSession.PlayerId;
        if (rareBroadcastPlayerId > 0 && rareBroadcastPlayerId != playerId)
        {
            ClearRareBroadcastState();
        }
        rareBroadcastPlayerId = playerId;
        PurgeRareBroadcastHistory(DateTimeOffset.UtcNow);
    }

    private void EnsureRareBroadcastUi()
    {
        if (rareBroadcastText != null || mainPageTransform == null)
        {
            return;
        }

        Transform existing = mainPageTransform.Find("Rare Reward Broadcast");
        if (existing != null)
        {
            rareBroadcastText = existing.GetComponent<TMP_Text>();
        }

        if (rareBroadcastText == null)
        {
            rareBroadcastText = CreateText(
                "Rare Reward Broadcast",
                mainPageTransform,
                string.Empty,
                18,
                FontStyle.Bold,
                new Color(0.36f, 0.16f, 0.04f, 1f),
                TextAnchor.MiddleCenter);
        }

        RectTransform rect = rareBroadcastText.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -76f);
        rect.sizeDelta = new Vector2(920f, 42f);
        rareBroadcastText.enableWordWrapping = true;
        rareBroadcastText.overflowMode = TextOverflowModes.Truncate;
        rareBroadcastText.gameObject.SetActive(false);
    }

    private void RefreshAvatarCatalogFromRealtime()
    {
        avatarPresetCatalogLoaded = false;
        if (avatarPresetCatalogLoading || !HasAuthSession() || !isActiveAndEnabled)
        {
            return;
        }

        StartCoroutine(RefreshPresetAvatarCatalog(
            avatarPresetPickerObject != null && avatarPresetPickerObject.activeInHierarchy));
    }

    private void RouteRareCardDrawBroadcast(RealtimeEvent realtimeEvent, string json)
    {
        if (!TryAcceptRareBroadcast(realtimeEvent, json, out RareBroadcastQueueItem item))
        {
            return;
        }

        if (IsLotteryScenePanelVisible()
            && rareCardDrawBroadcastText != null
            && rareCardDrawBroadcastViewport != null)
        {
            EnqueueRareCardDrawBroadcast(
                item.content,
                item.expiresAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            return;
        }

        EnqueueRareBroadcast(item);
    }

    private bool TryAcceptRareBroadcast(
        RealtimeEvent realtimeEvent,
        string json,
        out RareBroadcastQueueItem item)
    {
        item = null;
        if (realtimeEvent == null || string.IsNullOrWhiteSpace(realtimeEvent.eventId))
        {
            return false;
        }

        RareCardDrawRealtimeEvent rareEvent = ParseJson<RareCardDrawRealtimeEvent>(json);
        RareCardDrawData rareData = rareEvent == null ? null : rareEvent.data;
        if (rareData == null || string.IsNullOrWhiteSpace(rareData.content))
        {
            Debug.LogWarning("Rare card draw broadcast did not contain data.content.");
            return false;
        }

        if (!TryParseRareBroadcastExpiry(rareData, out DateTimeOffset expiresAt))
        {
            return false;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (expiresAt <= now)
        {
            return false;
        }

        PrepareRareBroadcastSession();
        PurgeRareBroadcastHistory(now);
        if (rareBroadcastSeen.ContainsKey(realtimeEvent.eventId))
        {
            return false;
        }

        rareBroadcastSeen[realtimeEvent.eventId] = expiresAt;
        rareBroadcastSeenOrder.Enqueue(new RareBroadcastSeen(realtimeEvent.eventId, expiresAt));
        while (rareBroadcastSeenOrder.Count > RareBroadcastHistoryLimit)
        {
            RemoveOldestRareBroadcastHistoryEntry();
        }

        item = new RareBroadcastQueueItem(
            realtimeEvent.eventId,
            rareData.content.Trim(),
            expiresAt);
        return true;
    }

    private void EnqueueRareBroadcast(RareBroadcastQueueItem item)
    {
        rareBroadcastQueue.Enqueue(item);
        while (rareBroadcastQueue.Count > RareBroadcastQueueLimit)
        {
            rareBroadcastQueue.Dequeue();
        }
        EnsureRareBroadcastUi();
        StartRareBroadcastPlaybackIfNeeded();
    }

    private void StartRareBroadcastPlaybackIfNeeded()
    {
        if (rareBroadcastPlaybackCoroutine == null && rareBroadcastQueue.Count > 0 && isActiveAndEnabled)
        {
            rareBroadcastPlaybackCoroutine = StartCoroutine(PlayRareBroadcastQueue());
        }
    }

    private IEnumerator PlayRareBroadcastQueue()
    {
        while (rareBroadcastQueue.Count > 0 && HasAuthSession())
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            PurgeRareBroadcastHistory(now);
            RareBroadcastQueueItem item = rareBroadcastQueue.Dequeue();
            if (item == null || item.expiresAt <= now)
            {
                continue;
            }

            EnsureRareBroadcastUi();
            if (rareBroadcastText == null)
            {
                continue;
            }

            rareBroadcastText.text = item.content;
            rareBroadcastText.gameObject.SetActive(true);
            float displaySeconds = RareBroadcastDisplaySeconds;
            if (item.expiresAt - now < TimeSpan.FromSeconds(displaySeconds))
            {
                displaySeconds = Mathf.Max(0.1f, (float)(item.expiresAt - now).TotalSeconds);
            }
            yield return new WaitForSecondsRealtime(displaySeconds);
            if (rareBroadcastText != null)
            {
                rareBroadcastText.text = string.Empty;
                rareBroadcastText.gameObject.SetActive(false);
            }
        }

        if (rareBroadcastText != null && rareBroadcastQueue.Count == 0)
        {
            rareBroadcastText.text = string.Empty;
            rareBroadcastText.gameObject.SetActive(false);
        }
        rareBroadcastPlaybackCoroutine = null;
    }

    private static bool TryParseRareBroadcastExpiry(RareCardDrawData data, out DateTimeOffset expiresAt)
    {
        expiresAt = default(DateTimeOffset);
        string value = string.IsNullOrWhiteSpace(data.expiresAt) ? data.expires_at : data.expiresAt;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (long.TryParse(value.Trim(), out long unixSeconds) && unixSeconds > 0L)
        {
            try
            {
                expiresAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        return DateTimeOffset.TryParse(
            value,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AllowWhiteSpaces
                | System.Globalization.DateTimeStyles.AssumeLocal,
            out expiresAt);
    }

    private static void PurgeRareBroadcastHistory(DateTimeOffset now)
    {
        int pending = rareBroadcastSeenOrder.Count;
        for (int index = 0; index < pending; index++)
        {
            RareBroadcastSeen entry = rareBroadcastSeenOrder.Dequeue();
            if (entry.expiresAt <= now)
            {
                if (rareBroadcastSeen.TryGetValue(entry.eventId, out DateTimeOffset expiry)
                    && expiry == entry.expiresAt)
                {
                    rareBroadcastSeen.Remove(entry.eventId);
                }
                continue;
            }

            rareBroadcastSeenOrder.Enqueue(entry);
        }

        while (rareBroadcastSeenOrder.Count > RareBroadcastHistoryLimit)
        {
            RemoveOldestRareBroadcastHistoryEntry();
        }
    }

    private static void RemoveOldestRareBroadcastHistoryEntry()
    {
        if (rareBroadcastSeenOrder.Count == 0)
        {
            return;
        }

        RareBroadcastSeen oldest = rareBroadcastSeenOrder.Dequeue();
        if (rareBroadcastSeen.TryGetValue(oldest.eventId, out DateTimeOffset expiry)
            && expiry == oldest.expiresAt)
        {
            rareBroadcastSeen.Remove(oldest.eventId);
        }
    }

    private void ClearRareBroadcastState()
    {
        realtimeEventIds.Clear();
        realtimeEventOrder.Clear();
        rareBroadcastQueue.Clear();
        rareBroadcastSeen.Clear();
        rareBroadcastSeenOrder.Clear();
        rareBroadcastPlayerId = 0;
        if (rareBroadcastPlaybackCoroutine != null)
        {
            StopCoroutine(rareBroadcastPlaybackCoroutine);
            rareBroadcastPlaybackCoroutine = null;
        }
        if (rareBroadcastText != null)
        {
            rareBroadcastText.text = string.Empty;
            rareBroadcastText.gameObject.SetActive(false);
        }
        ClearRareCardDrawBroadcastState();
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
        if (PlayerOnboardingManager.Instance != null)
        {
            PlayerOnboardingManager.Instance.RefreshCurrent();
        }
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

    private bool IsKnownAvatarPlayerEvent(RealtimeEvent realtimeEvent, string json)
    {
        if (realtimeEvent == null)
        {
            return false;
        }

        AvatarChangedRealtimeEvent avatarEvent = ParseJson<AvatarChangedRealtimeEvent>(json);
        long playerId = avatarEvent != null && avatarEvent.data != null && avatarEvent.data.playerId > 0
            ? avatarEvent.data.playerId
            : realtimeEvent.playerId;
        if (playerId == GameSession.PlayerId)
        {
            return true;
        }
        if (playerId > int.MaxValue)
        {
            return false;
        }

        return FindV37Friend((int)playerId) != null;
    }

    private void ApplyAvatarChangedRealtime(AvatarChangedRealtimeData message)
    {
        if (message == null || message.playerId <= 0 || message.avatarVersion <= 0)
        {
            Debug.LogWarning("Avatar change event did not contain a valid player or version.", this);
            return;
        }

        bool applied = false;
        if (message.playerId == GameSession.PlayerId)
        {
            if (message.avatarVersion < GetCurrentAvatarVersion())
            {
                return;
            }

            SaveCurrentAvatarState(
                message.avatarUrl,
                message.avatarSource,
                message.avatarCode,
                message.avatarVersion,
                message.avatarUpdatedTime);
            applied = true;
            RefreshCurrentAvatarVisuals();
        }

        if (message.playerId <= int.MaxValue)
        {
            ServerFriendResponse friend = FindV37Friend((int)message.playerId);
            if (friend != null && message.avatarVersion > friend.avatarVersion)
            {
                friend.avatarUrl = message.avatarUrl;
                friend.avatarCode = message.avatarCode;
                friend.avatarVersion = message.avatarVersion;
                applied = true;
            }
        }

        if (v37FriendSearchResult != null && v37FriendSearchResult.playerId == message.playerId
            && message.avatarVersion > v37FriendSearchResult.avatarVersion)
        {
            v37FriendSearchResult.avatarUrl = message.avatarUrl;
            v37FriendSearchResult.avatarCode = message.avatarCode;
            v37FriendSearchResult.avatarVersion = message.avatarVersion;
            applied = true;
        }

        UpdateAvatarInFriendRequests(message);

        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Friends);
        if (applied && panel != null && panel.PanelRoot != null && panel.gameObject.activeInHierarchy)
        {
            RefreshServerFriendRequestPanel(panel.PanelRoot, "好友头像已更新");
        }
    }

    private void UpdateAvatarInFriendRequests(AvatarChangedRealtimeData message)
    {
        for (int i = 0; i < v37IncomingRequests.Count; i++)
        {
            UpdateAvatarInFriendRequest(v37IncomingRequests[i], message);
        }
        for (int i = 0; i < v37OutgoingRequests.Count; i++)
        {
            UpdateAvatarInFriendRequest(v37OutgoingRequests[i], message);
        }
    }

    private static void UpdateAvatarInFriendRequest(ServerFriendRequestResponse request, AvatarChangedRealtimeData message)
    {
        if (request == null || message == null)
        {
            return;
        }

        if (request.requesterPlayerId == message.playerId
            && message.avatarVersion > request.requesterAvatarVersion)
        {
            request.requesterAvatarUrl = message.avatarUrl;
            request.requesterAvatarCode = message.avatarCode;
            request.requesterAvatarVersion = message.avatarVersion;
        }

        if (request.recipientPlayerId == message.playerId
            && message.avatarVersion > request.recipientAvatarVersion)
        {
            request.recipientAvatarUrl = message.avatarUrl;
            request.recipientAvatarCode = message.avatarCode;
            request.recipientAvatarVersion = message.avatarVersion;
        }
    }

    private void EnqueueRareCardDrawBroadcast(string content, string expiresAt)
    {
        if (string.IsNullOrWhiteSpace(content) || IsRareCardDrawBroadcastExpired(expiresAt))
        {
            return;
        }

        while (rareCardDrawBroadcastQueue.Count >= MaxRareCardDrawBroadcastQueueSize)
        {
            rareCardDrawBroadcastQueue.Dequeue();
        }

        rareCardDrawBroadcastQueue.Enqueue(new RareCardDrawBroadcastMessage
        {
            content = content,
            expiresAt = expiresAt
        });
        StartRareCardDrawBroadcast();
    }

    private void StartRareCardDrawBroadcast()
    {
        if (rareCardDrawBroadcastCoroutine == null
            && IsLotteryScenePanelVisible()
            && rareCardDrawBroadcastQueue.Count > 0)
        {
            rareCardDrawBroadcastCoroutine = StartCoroutine(PlayRareCardDrawBroadcasts());
        }
    }

    private IEnumerator PlayRareCardDrawBroadcasts()
    {
        while (rareCardDrawBroadcastQueue.Count > 0)
        {
            RareCardDrawBroadcastMessage message = rareCardDrawBroadcastQueue.Dequeue();
            if (message == null || IsRareCardDrawBroadcastExpired(message.expiresAt))
            {
                continue;
            }

            if (!IsLotteryScenePanelVisible())
            {
                rareCardDrawBroadcastQueue.Enqueue(message);
                HideRareCardDrawBroadcast();
                rareCardDrawBroadcastCoroutine = null;
                yield break;
            }

            if (rareCardDrawBroadcastText == null || rareCardDrawBroadcastViewport == null)
            {
                Debug.LogWarning("Lottery rare broadcast view is unavailable.");
                rareCardDrawBroadcastCoroutine = null;
                yield break;
            }

            rareCardDrawBroadcastObject.SetActive(true);
            rareCardDrawBroadcastText.text = message.content;
            rareCardDrawBroadcastText.ForceMeshUpdate();

            float viewportWidth = Mathf.Max(1f, rareCardDrawBroadcastViewport.rect.width);
            float textWidth = Mathf.Max(viewportWidth, rareCardDrawBroadcastText.preferredWidth);
            RectTransform textRect = rareCardDrawBroadcastText.rectTransform;
            textRect.sizeDelta = new Vector2(textWidth, rareCardDrawBroadcastViewport.rect.height);
            textRect.anchoredPosition = new Vector2(viewportWidth * 0.5f, 0f);

            float targetX = -viewportWidth * 0.5f - textWidth;
            while (IsLotteryScenePanelVisible()
                && !IsRareCardDrawBroadcastExpired(message.expiresAt)
                && textRect.anchoredPosition.x > targetX)
            {
                textRect.anchoredPosition += Vector2.left * RareCardDrawBroadcastSpeed * Time.unscaledDeltaTime;
                yield return null;
            }

            if (!IsLotteryScenePanelVisible())
            {
                rareCardDrawBroadcastQueue.Enqueue(message);
                HideRareCardDrawBroadcast();
                rareCardDrawBroadcastCoroutine = null;
                yield break;
            }

            yield return new WaitForSecondsRealtime(0.35f);
        }

        HideRareCardDrawBroadcast();
        rareCardDrawBroadcastCoroutine = null;
    }

    private void EnsureRareCardDrawBroadcastView(Transform lotteryRoot)
    {
        if (lotteryRoot == null)
        {
            Debug.LogWarning("Cannot create lottery rare broadcast without a lottery root.");
            return;
        }

        Transform viewportTransform = FindSceneTransform(lotteryRoot, "LotteryRareBroadcast");
        if (viewportTransform == null)
        {
            Transform parent = FindSceneTransform(lotteryRoot, "LotteryHeader");
            if (parent == null)
            {
                parent = lotteryRoot;
            }

            GameObject viewportObject = new GameObject(
                "LotteryRareBroadcast",
                typeof(RectTransform),
                typeof(RectMask2D));
            viewportTransform = viewportObject.transform;
            viewportTransform.SetParent(parent, false);
            viewportTransform.SetAsLastSibling();

            RectTransform viewportRect = viewportTransform as RectTransform;
            RectTransform subtitleRect = FindSceneTransform(lotteryRoot, "LotterySubtitle") as RectTransform;
            if (subtitleRect != null)
            {
                viewportRect.anchorMin = subtitleRect.anchorMin;
                viewportRect.anchorMax = subtitleRect.anchorMax;
                viewportRect.pivot = subtitleRect.pivot;
                viewportRect.anchoredPosition = subtitleRect.anchoredPosition
                    - new Vector2(0f, Mathf.Max(1f, subtitleRect.rect.height));
                viewportRect.sizeDelta = new Vector2(
                    subtitleRect.sizeDelta.x,
                    Mathf.Max(24f, subtitleRect.sizeDelta.y * 0.67f));
            }
            else
            {
                viewportRect.anchorMin = new Vector2(0.5f, 0.5f);
                viewportRect.anchorMax = new Vector2(0.5f, 0.5f);
                viewportRect.pivot = new Vector2(0.5f, 0.5f);
                viewportRect.anchoredPosition = Vector2.zero;
                viewportRect.sizeDelta = new Vector2(420f, 24f);
            }
        }

        rareCardDrawBroadcastObject = viewportTransform.gameObject;
        rareCardDrawBroadcastViewport = viewportTransform as RectTransform;
        if (rareCardDrawBroadcastCoroutine == null)
        {
            rareCardDrawBroadcastObject.SetActive(false);
        }

        Transform textTransform = FindSceneTransform(viewportTransform, "LotteryRareBroadcastText");
        bool createText = textTransform == null;
        if (textTransform == null)
        {
            GameObject textObject = new GameObject(
                "LotteryRareBroadcastText",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textTransform = textObject.transform;
            textTransform.SetParent(viewportTransform, false);
        }

        rareCardDrawBroadcastText = textTransform.GetComponent<TextMeshProUGUI>();
        if (rareCardDrawBroadcastText == null)
        {
            Debug.LogWarning("LotteryRareBroadcastText is missing TextMeshProUGUI.");
            return;
        }

        if (createText)
        {
            rareCardDrawBroadcastText.font = RuntimeFontAssetProvider.Resolve();
            rareCardDrawBroadcastText.fontStyle = FontStyles.Normal;
            rareCardDrawBroadcastText.fontSize = 16f;
            rareCardDrawBroadcastText.alignment = TextAlignmentOptions.MidlineLeft;
            rareCardDrawBroadcastText.enableWordWrapping = false;
            rareCardDrawBroadcastText.overflowMode = TextOverflowModes.Overflow;
            rareCardDrawBroadcastText.color = new Color(1f, 0.91f, 0.63f, 1f);
        }
        else if (rareCardDrawBroadcastText.font == null)
        {
            rareCardDrawBroadcastText.font = RuntimeFontAssetProvider.Resolve();
        }
        rareCardDrawBroadcastText.raycastTarget = false;

        RectTransform textRect = rareCardDrawBroadcastText.rectTransform;
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(1f, rareCardDrawBroadcastViewport.rect.height);
    }

    private void HideRareCardDrawBroadcast()
    {
        if (rareCardDrawBroadcastObject != null)
        {
            rareCardDrawBroadcastObject.SetActive(false);
        }
    }

    private void ClearRareCardDrawBroadcastState()
    {
        rareCardDrawBroadcastQueue.Clear();
        if (rareCardDrawBroadcastCoroutine != null)
        {
            StopCoroutine(rareCardDrawBroadcastCoroutine);
            rareCardDrawBroadcastCoroutine = null;
        }
        if (rareCardDrawBroadcastText != null)
        {
            rareCardDrawBroadcastText.text = string.Empty;
        }
        HideRareCardDrawBroadcast();
    }

    private bool IsLotteryScenePanelVisible()
    {
        return lotteryPanelObject != null && lotteryPanelObject.activeInHierarchy;
    }

    private static bool IsRareCardDrawBroadcastExpired(string expiresAt)
    {
        if (string.IsNullOrWhiteSpace(expiresAt))
        {
            return false;
        }

        if (!DateTimeOffset.TryParse(
                expiresAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset parsed))
        {
            return false;
        }

        return parsed <= DateTimeOffset.UtcNow;
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
            ParameterInfo[] parameters = method.GetParameters();
            operation = method.Invoke(service, parameters.Length == 0 ? null : new object[] { null });
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
            LoadPendingPaymentState();
            StartCoroutine(RecoverPendingPaymentOrder("应用恢复前台，正在补查支付订单..."));
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
    private class AvatarChangedRealtimeEvent
    {
        public string type;
        public string eventId;
        public long playerId;
        public AvatarChangedRealtimeData data;
        public string serverTime;
    }

    [Serializable]
    private class AvatarChangedRealtimeData
    {
        public long playerId;
        public string avatarUrl;
        public string avatarSource;
        public string avatarCode;
        public long avatarVersion;
        public string avatarUpdatedTime;
    }

    [Serializable]
    private class AvatarCatalogChangedRealtimeEvent
    {
        public string type;
        public string eventId;
        public long playerId;
        public AvatarCatalogChangedData data;
        public string serverTime;
    }

    [Serializable]
    private class AvatarCatalogChangedData
    {
        public long catalogVersion;
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
    private sealed class RareCardDrawBroadcastMessage
    {
        public string content;
        public string expiresAt;
    }

    [Serializable]
    private class RareCardDrawRealtimeEvent
    {
        public string type;
        public string eventId;
        public int playerId;
        public RareCardDrawData data;
    }

    [Serializable]
    private class RareCardDrawData
    {
        public string playerName;
        public string poolName;
        public string rewardName;
        public string rewardKey;
        public string rarity;
        public string content;
        public RareCardDrawReward[] rewards;
        public int omittedRewardTypeCount;
        public string occurredAt;
        public string expiresAt;
        public string occurred_at;
        public string expires_at;
    }

    [Serializable]
    private class RareCardDrawReward
    {
        public string rewardName;
        public string rewardCategory;
        public int hitCount;
        public long configuredAmount;
        public bool duplicateNpcConverted;
        public RareCardDrawGrant[] grants;
    }

    [Serializable]
    private class RareCardDrawGrant
    {
        public string rewardType;
        public string rewardName;
        public string rewardCategory;
        public int hitCount;
        public long amount;
        public bool duplicateNpcConverted;
    }

    private sealed class RareBroadcastSeen
    {
        public readonly string eventId;
        public readonly DateTimeOffset expiresAt;

        public RareBroadcastSeen(string eventId, DateTimeOffset expiresAt)
        {
            this.eventId = eventId;
            this.expiresAt = expiresAt;
        }
    }

    private sealed class RareBroadcastQueueItem
    {
        public readonly string eventId;
        public readonly string content;
        public readonly DateTimeOffset expiresAt;

        public RareBroadcastQueueItem(string eventId, string content, DateTimeOffset expiresAt)
        {
            this.eventId = eventId;
            this.content = content;
            this.expiresAt = expiresAt;
        }
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
