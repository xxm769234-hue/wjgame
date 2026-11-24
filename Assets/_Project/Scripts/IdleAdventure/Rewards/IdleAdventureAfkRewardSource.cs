using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class IdleAdventureAfkRewardSource : MonoBehaviour
{
    [Header("Production")]
    [SerializeField] private string itemKey = "gold";
    [SerializeField] private string displayName = "金币";
    [SerializeField, Min(1)] private int baseQuantityPerCycle = 10;
    [SerializeField, Min(0)] private int quantityPerHighestStage = 2;
    [SerializeField, Min(1f)] private float productionIntervalSeconds = 30f;
    [SerializeField, Min(1)] private int maxStoredQuantity = 1000;
    [SerializeField] private bool useBackendWhenAuthenticated = true;

    [Header("Server Time State")]
    [SerializeField, Min(0)] private int highestClearedStage;
    [SerializeField, Min(0)] private int serverStoredQuantity;
    [SerializeField] private long lastSettledServerUnixSeconds;
    [SerializeField] private long syncedServerUnixSeconds;
    [SerializeField] private bool hasServerSnapshot;

    [Header("View")]
    [SerializeField] private Button collectButton;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image progressFill;
    [SerializeField] private TextMeshProUGUI iconText;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI collectButtonText;
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private Sprite readyIcon;
    [SerializeField] private Sprite emptyIcon;

    [Header("Grant Target")]
    [SerializeField] private IdleAdventureRewardLedger rewardLedger;

    [Header("Events")]
    [SerializeField] private UnityEvent<string, string, int> rewardCollected = new UnityEvent<string, string, int>();

    private float localSecondsSinceServerSync;
    private bool serverStateRequestRunning;
    private int serverStateRequestGeneration;
    private int serverStateRetryAttempt;
    private Coroutine serverStateRetryCoroutine;
    private bool localClockInitialized;
    private bool wasBackendAuthoritative;
    private long rewardSnapshotServerUnixSeconds;
    private int rewardSnapshotRemainderSeconds;
    private bool rewardSnapshotUsesClaimableQuantity;
    private bool collectionRequestPending;
    private bool collectionAwaitingSnapshot;

    private const int MaxServerStateRetryAttempts = 3;
    private const float ServerStateRetryBaseSeconds = 2f;

    public string ItemKey => string.IsNullOrEmpty(itemKey) ? "reward" : itemKey;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? ItemKey : displayName;
    public int QuantityPerCycle => Mathf.Max(1, baseQuantityPerCycle + Mathf.Max(0, highestClearedStage) * Mathf.Max(0, quantityPerHighestStage));
    public int StoredQuantity => CalculateStoredQuantity(GetEstimatedServerUnixSeconds());
    public int MaxStoredQuantity => Mathf.Max(1, maxStoredQuantity);
    public float ProductionIntervalSeconds => Mathf.Max(1f, productionIntervalSeconds);
    public int HighestClearedStage => highestClearedStage;
    public bool HasServerSnapshot => hasServerSnapshot;
    public bool HasViewBindings => collectButton != null && iconImage != null && iconText != null && amountText != null && timerText != null && collectButtonText != null;
    public UnityEvent<string, string, int> RewardCollected => rewardCollected;
    public event Action<string, bool> FeedbackChanged;

    public void SetRewardLedger(IdleAdventureRewardLedger ledger)
    {
        rewardLedger = ledger;
    }

    public void SetHighestClearedStage(int value)
    {
        highestClearedStage = Mathf.Max(highestClearedStage, value);
        RefreshView();
    }

    public void SetHighestClearedStageFromProgress(int value)
    {
        highestClearedStage = Mathf.Max(0, value);
        RefreshView();
    }

    public void Initialize(IdleAdventureRewardLedger ledger, Button button, Image icon, Image fill, TextMeshProUGUI iconLabel, TextMeshProUGUI amountLabel, TextMeshProUGUI timerLabel, TextMeshProUGUI collectLabel, TextMeshProUGUI feedbackLabel = null)
    {
        if (collectButton != null) collectButton.onClick.RemoveListener(Collect);
        rewardLedger = ledger;
        collectButton = button;
        iconImage = icon;
        progressFill = fill;
        iconText = iconLabel;
        amountText = amountLabel;
        timerText = timerLabel;
        collectButtonText = collectLabel;
        feedbackText = feedbackLabel;
        EnsureInitialServerSnapshot();
        Bind();
        RefreshView();
    }

    public void SetIconSprites(Sprite original, Sprite grayscale)
    {
        readyIcon = original;
        emptyIcon = grayscale;
        RefreshView();
    }

    public void ApplyServerSnapshot(long serverUnixSeconds, long lastSettledUnixSeconds, int storedQuantityFromServer, int highestClearedStageFromServer)
    {
        ApplyServerSnapshot(
            serverUnixSeconds,
            lastSettledUnixSeconds,
            storedQuantityFromServer,
            highestClearedStageFromServer,
            0L,
            false);
    }

    private void ApplyServerSnapshot(
        long serverUnixSeconds,
        long lastSettledUnixSeconds,
        int storedQuantityFromServer,
        int highestClearedStageFromServer,
        long nextSettlementSeconds,
        bool usesClaimableQuantity)
    {
        collectionAwaitingSnapshot = false;
        syncedServerUnixSeconds = Math.Max(0L, serverUnixSeconds);
        lastSettledServerUnixSeconds = Math.Max(0L, lastSettledUnixSeconds);
        serverStoredQuantity = Mathf.Clamp(storedQuantityFromServer, 0, MaxStoredQuantity);
        highestClearedStage = Mathf.Max(0, highestClearedStageFromServer);
        localSecondsSinceServerSync = 0f;
        rewardSnapshotServerUnixSeconds = syncedServerUnixSeconds;
        rewardSnapshotUsesClaimableQuantity = usesClaimableQuantity && rewardSnapshotServerUnixSeconds > 0L;
        rewardSnapshotRemainderSeconds = ResolveSnapshotRemainder(nextSettlementSeconds);
        localClockInitialized = true;
        hasServerSnapshot = true;
        RefreshView();
    }

    public void ApplyServerCollection(long serverUnixSeconds, long nextLastSettledUnixSeconds, int nextStoredQuantityFromServer)
    {
        collectionAwaitingSnapshot = false;
        syncedServerUnixSeconds = Math.Max(0L, serverUnixSeconds);
        lastSettledServerUnixSeconds = Math.Max(0L, nextLastSettledUnixSeconds);
        serverStoredQuantity = Mathf.Clamp(nextStoredQuantityFromServer, 0, MaxStoredQuantity);
        localSecondsSinceServerSync = 0f;
        rewardSnapshotServerUnixSeconds = syncedServerUnixSeconds;
        rewardSnapshotRemainderSeconds = 0;
        rewardSnapshotUsesClaimableQuantity = rewardSnapshotServerUnixSeconds > 0L;
        localClockInitialized = true;
        hasServerSnapshot = true;
        RefreshView();
    }

    public void ApplyBackendStateFromService(IdleAdventureAfkRewardStateResponse state)
    {
        TryApplyBackendState(state);
    }

    private void Awake()
    {
        EnsureInitialServerSnapshot();
        Bind();
        RefreshView();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        serverStateRetryAttempt = 0;
        wasBackendAuthoritative = false;
        if (IsBackendAuthoritative())
        {
            ResetLocalPreviewForBackendSync();
        }
        TryLoadServerState();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused || !Application.isPlaying)
        {
            return;
        }

        serverStateRetryAttempt = 0;
        if (IsBackendAuthoritative())
        {
            ResetLocalPreviewForBackendSync();
        }
        TryLoadServerState();
    }

    private void OnDisable()
    {
        if (serverStateRetryCoroutine != null)
        {
            StopCoroutine(serverStateRetryCoroutine);
            serverStateRetryCoroutine = null;
        }
        serverStateRequestRunning = false;
    }

    private void OnValidate()
    {
        baseQuantityPerCycle = Mathf.Max(1, baseQuantityPerCycle);
        quantityPerHighestStage = Mathf.Max(0, quantityPerHighestStage);
        productionIntervalSeconds = Mathf.Max(1f, productionIntervalSeconds);
        maxStoredQuantity = Mathf.Max(1, maxStoredQuantity);
        highestClearedStage = Mathf.Max(0, highestClearedStage);
        serverStoredQuantity = Mathf.Clamp(serverStoredQuantity, 0, maxStoredQuantity);
        RefreshView();
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        bool backendAuthoritative = IsBackendAuthoritative();
        if (backendAuthoritative && !wasBackendAuthoritative)
        {
            ResetLocalPreviewForBackendSync();
            serverStateRetryAttempt = 0;
            TryLoadServerState();
        }
        wasBackendAuthoritative = backendAuthoritative;

        localSecondsSinceServerSync += Time.unscaledDeltaTime;
        RefreshView();
    }

    public void Collect()
    {
        if (TryCollectFromBackend()) return;

        SetFeedback("请先登录账号后领取奖励");
        Debug.LogWarning("Idle Adventure AFK reward collection requires an authenticated backend session.", this);
        RefreshView();
    }

    public void TryLoadServerState()
    {
        if (!useBackendWhenAuthenticated) return;

        BackendGameService service = BackendGameService.Instance;
        if (service == null || !service.IsAuthenticated || serverStateRequestRunning) return;

        serverStateRequestRunning = true;
        int requestGeneration = serverStateRequestGeneration;
        service.LoadIdleAdventureAfkReward(result =>
        {
            if (requestGeneration != serverStateRequestGeneration) return;

            serverStateRequestRunning = false;
            if (result != null && result.Success && TryApplyBackendState(result.Data))
            {
                serverStateRetryAttempt = 0;
                return;
            }

            ScheduleServerStateRetry();
            RefreshView();
        });
    }

    public void ClearServerStateForAccountChange()
    {
        serverStateRequestGeneration++;
        collectionRequestPending = false;
        collectionAwaitingSnapshot = false;
        SetFeedback(string.Empty);
        serverStateRequestRunning = false;
        serverStateRetryAttempt = 0;
        wasBackendAuthoritative = false;
        if (serverStateRetryCoroutine != null)
        {
            StopCoroutine(serverStateRetryCoroutine);
            serverStateRetryCoroutine = null;
        }

        highestClearedStage = 0;
        localClockInitialized = false;
        syncedServerUnixSeconds = 0L;
        lastSettledServerUnixSeconds = 0L;
        serverStoredQuantity = 0;
        localSecondsSinceServerSync = 0f;
        rewardSnapshotServerUnixSeconds = 0L;
        rewardSnapshotRemainderSeconds = 0;
        rewardSnapshotUsesClaimableQuantity = false;
        hasServerSnapshot = false;
        RefreshView();
    }

    private void ScheduleServerStateRetry()
    {
        if (!isActiveAndEnabled
            || !Application.isPlaying
            || serverStateRetryCoroutine != null
            || serverStateRetryAttempt >= MaxServerStateRetryAttempts)
        {
            return;
        }

        float delay = ServerStateRetryBaseSeconds * Mathf.Pow(2f, serverStateRetryAttempt);
        serverStateRetryAttempt++;
        serverStateRetryCoroutine = StartCoroutine(RetryServerStateAfter(delay));
    }

    private IEnumerator RetryServerStateAfter(float delay)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, delay));
        serverStateRetryCoroutine = null;
        TryLoadServerState();
    }

    private bool TryCollectFromBackend()
    {
        if (!useBackendWhenAuthenticated) return false;

        BackendGameService service = BackendGameService.Instance;
        if (service == null || !service.IsAuthenticated) return false;
        if (collectionRequestPending || collectionAwaitingSnapshot) return true;
        if (!hasServerSnapshot)
        {
            RefreshView();
            return true;
        }

        if (StoredQuantity <= 0) return true;

        collectionRequestPending = true;
        SetFeedback("正在领取奖励…");
        if (collectButton != null) collectButton.interactable = false;
        int requestGeneration = serverStateRequestGeneration;
        service.CollectIdleAdventureAfkReward(result =>
        {
            if (requestGeneration != serverStateRequestGeneration) return;

            collectionRequestPending = false;
            if (result != null && result.Success && result.Data != null)
            {
                collectionAwaitingSnapshot = true;
                ApplyBackendCollection(result.Data);
                SetFeedback(CollectionReceipt(result.Data));
                RefreshView();
                PlayerOnboardingGameplayBridge.NotifyBusinessSucceeded();
            }
            else
            {
                SetFeedback("领取失败，请稍后重试");
                RefreshView();
            }
        });

        return true;
    }

    private bool TryApplyBackendState(IdleAdventureAfkRewardStateResponse state)
    {
        if (!IsUsableBackendState(state))
        {
            if (IsBackendAuthoritative())
            {
                ResetLocalPreviewForBackendSync();
                ScheduleServerStateRetry();
            }
            return false;
        }

        string resolvedItemKey = state.ResolvedItemKey;
        string resolvedDisplayName = state.ResolvedDisplayName;
        int resolvedBaseQuantityPerCycle = state.ResolvedBaseQuantityPerCycle;
        int resolvedQuantityPerHighestStage = state.ResolvedQuantityPerHighestStage;
        int resolvedQuantityPerCycle = state.ResolvedQuantityPerCycle;
        int resolvedProductionIntervalSeconds = state.ResolvedProductionIntervalSeconds;
        int resolvedMaxStoredQuantity = state.ResolvedMaxStoredQuantity;
        int resolvedStoredQuantity = state.ResolvedStoredQuantity;
        int resolvedHighestClearedStage = state.ResolvedHighestClearedStage;
        long resolvedServerUnixSeconds = state.ResolvedServerUnixSeconds;
        long resolvedLastSettledServerUnixSeconds = state.ResolvedLastSettledServerUnixSeconds;
        long resolvedNextSettlementSeconds = state.ResolvedNextSettlementSeconds;

        if (!string.IsNullOrEmpty(resolvedItemKey)) itemKey = resolvedItemKey;
        if (!string.IsNullOrEmpty(resolvedDisplayName)) displayName = resolvedDisplayName;
        if (resolvedBaseQuantityPerCycle > 0) baseQuantityPerCycle = resolvedBaseQuantityPerCycle;
        if (resolvedQuantityPerHighestStage >= 0) quantityPerHighestStage = resolvedQuantityPerHighestStage;
        if (resolvedProductionIntervalSeconds > 0) productionIntervalSeconds = resolvedProductionIntervalSeconds;
        if (resolvedMaxStoredQuantity > 0) maxStoredQuantity = resolvedMaxStoredQuantity;

        if (resolvedQuantityPerCycle > 0 && resolvedBaseQuantityPerCycle <= 0)
        {
            baseQuantityPerCycle = resolvedQuantityPerCycle;
            quantityPerHighestStage = 0;
        }

        ApplyServerSnapshot(
            resolvedServerUnixSeconds,
            resolvedLastSettledServerUnixSeconds,
            resolvedStoredQuantity,
            resolvedHighestClearedStage,
            resolvedNextSettlementSeconds,
            true);
        serverStateRetryAttempt = 0;
        if (serverStateRetryCoroutine != null)
        {
            StopCoroutine(serverStateRetryCoroutine);
            serverStateRetryCoroutine = null;
        }
        return true;
    }

    public static bool IsUsableBackendStateResponse(IdleAdventureAfkRewardStateResponse state)
    {
        if (state == null) return false;

        string resolvedItemKey = state.ResolvedItemKey;
        int resolvedBaseQuantityPerCycle = state.ResolvedBaseQuantityPerCycle;
        int resolvedQuantityPerCycle = state.ResolvedQuantityPerCycle;
        int resolvedProductionIntervalSeconds = state.ResolvedProductionIntervalSeconds;
        int resolvedMaxStoredQuantity = state.ResolvedMaxStoredQuantity;
        int resolvedStoredQuantity = state.ResolvedStoredQuantity;
        long resolvedServerUnixSeconds = state.ResolvedServerUnixSeconds;

        if (string.IsNullOrWhiteSpace(resolvedItemKey)
            || resolvedServerUnixSeconds <= 0L
            || resolvedProductionIntervalSeconds <= 0
            || resolvedMaxStoredQuantity <= 0
            || (resolvedBaseQuantityPerCycle <= 0 && resolvedQuantityPerCycle <= 0)
            || resolvedStoredQuantity < 0
            || resolvedStoredQuantity > resolvedMaxStoredQuantity
            || state.base_quantity_per_cycle < 0
            || state.baseQuantityPerCycle < 0L
            || state.quantity_per_highest_stage < 0
            || state.quantityPerHighestStage < 0L
            || state.quantity_per_cycle < 0
            || state.quantityPerCycle < 0L
            || state.production_interval_seconds < 0
            || state.intervalSeconds < 0
            || state.max_stored_quantity < 0
            || state.maxStoredQuantity < 0L
            || state.stored_quantity < 0
            || state.claimableQuantity < 0L
            || state.highest_stage < 0
            || state.highestStage < 0
            || state.server_unix_seconds < 0L
            || state.serverUnixSeconds < 0L
            || state.last_settled_server_unix_seconds < 0L
            || state.nextSettlementSeconds < 0L
            || state.next_settlement_seconds < 0L)
        {
            return false;
        }

        return true;
    }

    private bool IsUsableBackendState(IdleAdventureAfkRewardStateResponse state)
    {
        return IsUsableBackendStateResponse(state);
    }

    private void ApplyBackendCollection(IdleAdventureAfkRewardCollectResponse response)
    {
        if (response == null) return;

        if (!response.ResolvedIdempotentReplay)
        {
            IdleAdventureCollectedItemResponse[] collectedRewards = response.ResolvedCollectedRewards;
            if (collectedRewards != null && collectedRewards.Length > 0)
            {
                for (int i = 0; i < collectedRewards.Length; i++)
                {
                    ApplyCollectedItem(collectedRewards[i]);
                }
            }
            else
            {
                ApplyCollectedItem(response.ResolvedCollectedReward ?? response.ResolvedCollectedItem);
            }
        }

        if (response.state != null)
        {
            TryApplyBackendState(response.state);
        }
        else
        {
            serverStateRetryAttempt = 0;
            TryLoadServerState();
        }
    }

    public void ClearStoredReward()
    {
        long serverNow = GetEstimatedServerUnixSeconds();
        syncedServerUnixSeconds = serverNow;
        lastSettledServerUnixSeconds = serverNow;
        serverStoredQuantity = 0;
        localSecondsSinceServerSync = 0f;
        rewardSnapshotServerUnixSeconds = serverNow;
        rewardSnapshotRemainderSeconds = 0;
        rewardSnapshotUsesClaimableQuantity = false;
        localClockInitialized = true;
        hasServerSnapshot = true;
        RefreshView();
    }

    private void ApplyCollectedItem(IdleAdventureCollectedItemResponse collectedItem)
    {
        if (collectedItem == null || collectedItem.quantity <= 0L) return;

        string collectedKey = string.IsNullOrEmpty(collectedItem.ResolvedItemKey) ? ItemKey : collectedItem.ResolvedItemKey;
        string collectedName = string.IsNullOrEmpty(collectedItem.ResolvedName) ? DisplayName : collectedItem.ResolvedName;
        int collectedQuantity = Mathf.Clamp((int)Math.Min(int.MaxValue, collectedItem.quantity), 0, int.MaxValue);

        Dictionary<string, int> rewards = new Dictionary<string, int>
        {
            { collectedKey, collectedQuantity }
        };

        Dictionary<string, string> displayNames = new Dictionary<string, string>
        {
            { collectedKey, collectedName }
        };

        if (rewardLedger != null) rewardLedger.GrantRewards(rewards, displayNames, $"{collectedName} ×{collectedQuantity}");
        rewardCollected.Invoke(collectedKey, collectedName, collectedQuantity);
    }

    private void EnsureInitialServerSnapshot()
    {
        if (hasServerSnapshot || localClockInitialized || IsBackendAuthoritative()) return;

        long serverNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        syncedServerUnixSeconds = serverNow;
        lastSettledServerUnixSeconds = serverNow;
        localSecondsSinceServerSync = 0f;
        localClockInitialized = true;
    }

    private long GetEstimatedServerUnixSeconds()
    {
        EnsureInitialServerSnapshot();
        return syncedServerUnixSeconds + Mathf.Max(0, Mathf.FloorToInt(localSecondsSinceServerSync));
    }

    private int CalculateStoredQuantity(long serverUnixSeconds)
    {
        int baseStored = Mathf.Clamp(serverStoredQuantity, 0, MaxStoredQuantity);
        if (rewardSnapshotUsesClaimableQuantity)
        {
            if (baseStored >= MaxStoredQuantity) return MaxStoredQuantity;

            long elapsedSinceSnapshot = Math.Max(
                0L,
                serverUnixSeconds - rewardSnapshotServerUnixSeconds);
            long accumulatedSeconds = elapsedSinceSnapshot + rewardSnapshotRemainderSeconds;
            long snapshotCycleCount = accumulatedSeconds / Math.Max(1L, (long)Mathf.Max(1f, productionIntervalSeconds));
            long snapshotProduced = snapshotCycleCount * Math.Max(1L, (long)QuantityPerCycle);
            return Mathf.Clamp(
                baseStored + (int)Math.Min(int.MaxValue, snapshotProduced),
                0,
                MaxStoredQuantity);
        }

        if (lastSettledServerUnixSeconds <= 0L) return baseStored;

        long elapsedSeconds = Math.Max(0L, serverUnixSeconds - lastSettledServerUnixSeconds);
        int cycleCount = Mathf.FloorToInt(elapsedSeconds / Mathf.Max(1f, productionIntervalSeconds));
        long produced = (long)cycleCount * QuantityPerCycle;
        return Mathf.Clamp(baseStored + (int)Math.Min(int.MaxValue, produced), 0, MaxStoredQuantity);
    }

    private float CalculateProgress(long serverUnixSeconds)
    {
        if (CalculateStoredQuantity(serverUnixSeconds) >= MaxStoredQuantity) return 1f;
        if (rewardSnapshotUsesClaimableQuantity)
        {
            long elapsedSinceSnapshot = Math.Max(
                0L,
                serverUnixSeconds - rewardSnapshotServerUnixSeconds);
            long snapshotInterval = Math.Max(1L, (long)Mathf.Max(1f, productionIntervalSeconds));
            long snapshotRemainder = (elapsedSinceSnapshot + rewardSnapshotRemainderSeconds) % snapshotInterval;
            return Mathf.Clamp01((float)snapshotRemainder / snapshotInterval);
        }

        if (lastSettledServerUnixSeconds <= 0L) return 0f;

        long elapsedSeconds = Math.Max(0L, serverUnixSeconds - lastSettledServerUnixSeconds);
        float interval = Mathf.Max(1f, productionIntervalSeconds);
        return Mathf.Clamp01((elapsedSeconds % interval) / interval);
    }

    private int CalculateRemainingSeconds(long serverUnixSeconds)
    {
        if (CalculateStoredQuantity(serverUnixSeconds) >= MaxStoredQuantity) return 0;
        if (rewardSnapshotUsesClaimableQuantity)
        {
            long elapsedSinceSnapshot = Math.Max(
                0L,
                serverUnixSeconds - rewardSnapshotServerUnixSeconds);
            long snapshotInterval = Math.Max(1L, (long)Mathf.Max(1f, productionIntervalSeconds));
            long snapshotRemainder = (elapsedSinceSnapshot + rewardSnapshotRemainderSeconds) % snapshotInterval;
            return (int)Math.Max(0L, snapshotInterval - snapshotRemainder);
        }

        if (lastSettledServerUnixSeconds <= 0L) return Mathf.CeilToInt(productionIntervalSeconds);

        long elapsedSeconds = Math.Max(0L, serverUnixSeconds - lastSettledServerUnixSeconds);
        int passedInCycle = Mathf.FloorToInt(elapsedSeconds % Mathf.Max(1f, productionIntervalSeconds));
        return Mathf.Max(0, Mathf.CeilToInt(productionIntervalSeconds) - passedInCycle);
    }

    private void Bind()
    {
        if (collectButton == null) return;

        collectButton.onClick.RemoveListener(Collect);
        collectButton.onClick.AddListener(Collect);
    }

    private string CollectionReceipt(IdleAdventureAfkRewardCollectResponse response)
    {
        IdleAdventureCollectedItemResponse[] rewards = response.ResolvedCollectedRewards;
        if (rewards == null || rewards.Length == 0)
            rewards = new[] { response.ResolvedCollectedReward ?? response.ResolvedCollectedItem };
        List<string> entries = new List<string>();
        foreach (IdleAdventureCollectedItemResponse reward in rewards)
        {
            if (reward == null || reward.quantity <= 0L) continue;
            string name = reward.ResolvedName;
            if (string.IsNullOrEmpty(name)) name = reward.ResolvedItemKey == ItemKey ? DisplayName : reward.ResolvedItemKey;
            if (string.IsNullOrEmpty(name)) name = "奖励";
            entries.Add($"{name} × {reward.quantity:N0}");
        }
        string prefix = response.ResolvedIdempotentReplay ? "已到账：" : "已获得 ";
        return entries.Count > 0 ? prefix + string.Join("、", entries) : "奖励已到账";
    }

    private void RefreshView()
    {
        bool waitingForServer = (IsBackendAuthoritative() && !hasServerSnapshot) || collectionAwaitingSnapshot;
        long serverNow = GetEstimatedServerUnixSeconds();
        int amount = waitingForServer ? 0 : CalculateStoredQuantity(serverNow);
        bool ready = !waitingForServer && amount > 0;
        bool full = amount >= MaxStoredQuantity;

        if (collectButton != null) collectButton.interactable = ready && !collectionRequestPending;
        if (iconImage != null)
        {
            iconImage.color = Color.white;
            Sprite stateIcon = ready ? readyIcon : emptyIcon;
            if (stateIcon != null) iconImage.sprite = stateIcon;
        }
        if (progressFill != null) progressFill.fillAmount = waitingForServer ? 0f : Mathf.Clamp01((float)amount / MaxStoredQuantity);
        if (iconText != null) iconText.text = FrontendTextLocalization.Normalize(ResolveIconText());
        if (amountText != null) amountText.text = waitingForServer ? "同步中" : $"{amount:N0} / {MaxStoredQuantity:N0}";
        if (timerText != null) timerText.text = waitingForServer ? string.Empty : full ? "已存满，请及时领取" : ready ? string.Empty : "奖励积累中";
    }

    private void SetFeedback(string value)
    {
        if (feedbackText != null) feedbackText.text = value;
        FeedbackChanged?.Invoke(value, collectionRequestPending);
    }

    private bool IsBackendAuthoritative()
    {
        if (!useBackendWhenAuthenticated) return false;

        BackendGameService service = BackendGameService.Instance;
        return service != null && service.IsAuthenticated;
    }

    private void ResetLocalPreviewForBackendSync()
    {
        localClockInitialized = false;
        syncedServerUnixSeconds = 0L;
        lastSettledServerUnixSeconds = 0L;
        serverStoredQuantity = 0;
        localSecondsSinceServerSync = 0f;
        rewardSnapshotServerUnixSeconds = 0L;
        rewardSnapshotRemainderSeconds = 0;
        rewardSnapshotUsesClaimableQuantity = false;
        hasServerSnapshot = false;
        RefreshView();
    }

    private int ResolveSnapshotRemainder(long nextSettlementSeconds)
    {
        if (nextSettlementSeconds <= 0L || serverStoredQuantity >= MaxStoredQuantity)
        {
            return 0;
        }

        long interval = Math.Max(1L, (long)Mathf.Max(1f, productionIntervalSeconds));
        long clampedNextSettlement = Math.Min(interval, nextSettlementSeconds);
        return (int)Math.Max(0L, interval - clampedNextSettlement);
    }

    private string ResolveIconText()
    {
        string source = DisplayName;
        return string.IsNullOrEmpty(source) ? "奖" : source.Substring(0, 1);
    }

    private string FormatRemainingTime(int remainingSeconds)
    {
        int minutes = remainingSeconds / 60;
        int seconds = remainingSeconds % 60;
        return minutes > 0 ? $"{minutes}:{seconds:00}" : $"{seconds}s";
    }
}
