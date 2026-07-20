using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class IdleAdventureAfkRewardSource : MonoBehaviour
{
    [Header("Production")]
    [SerializeField] private string itemKey = "gold";
    [SerializeField] private string displayName = "Gold";
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

    [Header("Grant Target")]
    [SerializeField] private IdleAdventureRewardLedger rewardLedger;

    [Header("Events")]
    [SerializeField] private UnityEvent<string, string, int> rewardCollected = new UnityEvent<string, string, int>();

    private float localSecondsSinceServerSync;

    public string ItemKey => string.IsNullOrEmpty(itemKey) ? "reward" : itemKey;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? ItemKey : displayName;
    public int QuantityPerCycle => Mathf.Max(1, baseQuantityPerCycle + Mathf.Max(0, highestClearedStage) * Mathf.Max(0, quantityPerHighestStage));
    public int StoredQuantity => CalculateStoredQuantity(GetEstimatedServerUnixSeconds());
    public int MaxStoredQuantity => Mathf.Max(1, maxStoredQuantity);
    public int HighestClearedStage => highestClearedStage;
    public bool HasServerSnapshot => hasServerSnapshot;
    public bool HasViewBindings => collectButton != null && iconImage != null && progressFill != null && iconText != null && amountText != null && timerText != null;
    public UnityEvent<string, string, int> RewardCollected => rewardCollected;

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

    public void Initialize(IdleAdventureRewardLedger ledger, Button button, Image icon, Image fill, TextMeshProUGUI iconLabel, TextMeshProUGUI amountLabel, TextMeshProUGUI timerLabel)
    {
        rewardLedger = ledger;
        collectButton = button;
        iconImage = icon;
        progressFill = fill;
        iconText = iconLabel;
        amountText = amountLabel;
        timerText = timerLabel;
        EnsureInitialServerSnapshot();
        Bind();
        RefreshView();
    }

    public void ApplyServerSnapshot(long serverUnixSeconds, long lastSettledUnixSeconds, int storedQuantityFromServer, int highestClearedStageFromServer)
    {
        syncedServerUnixSeconds = Math.Max(0L, serverUnixSeconds);
        lastSettledServerUnixSeconds = Math.Max(0L, lastSettledUnixSeconds);
        serverStoredQuantity = Mathf.Clamp(storedQuantityFromServer, 0, MaxStoredQuantity);
        highestClearedStage = Mathf.Max(0, highestClearedStageFromServer);
        localSecondsSinceServerSync = 0f;
        hasServerSnapshot = true;
        RefreshView();
    }

    public void ApplyServerCollection(long serverUnixSeconds, long nextLastSettledUnixSeconds, int nextStoredQuantityFromServer)
    {
        syncedServerUnixSeconds = Math.Max(0L, serverUnixSeconds);
        lastSettledServerUnixSeconds = Math.Max(0L, nextLastSettledUnixSeconds);
        serverStoredQuantity = Mathf.Clamp(nextStoredQuantityFromServer, 0, MaxStoredQuantity);
        localSecondsSinceServerSync = 0f;
        hasServerSnapshot = true;
        RefreshView();
    }

    private void Awake()
    {
        EnsureInitialServerSnapshot();
        Bind();
        RefreshView();
    }

    private void OnEnable()
    {
        if (Application.isPlaying) TryLoadServerState();
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

        localSecondsSinceServerSync += Time.unscaledDeltaTime;
        RefreshView();
    }

    public void Collect()
    {
        if (TryCollectFromBackend()) return;
        CollectLocalEstimate();
    }

    public void TryLoadServerState()
    {
        if (!useBackendWhenAuthenticated) return;

        BackendGameService service = BackendGameService.Instance;
        if (service == null || !service.IsAuthenticated) return;

        service.LoadIdleAdventureAfkReward(result =>
        {
            if (result != null && result.Success && result.Data != null) ApplyBackendState(result.Data);
        });
    }

    private bool TryCollectFromBackend()
    {
        if (!useBackendWhenAuthenticated) return false;

        BackendGameService service = BackendGameService.Instance;
        if (service == null || !service.IsAuthenticated) return false;

        if (collectButton != null) collectButton.interactable = false;
        service.CollectIdleAdventureAfkReward(result =>
        {
            if (result != null && result.Success && result.Data != null)
            {
                ApplyBackendCollection(result.Data);
            }
            else
            {
                RefreshView();
            }
        });

        return true;
    }

    private void ApplyBackendState(IdleAdventureAfkRewardStateResponse state)
    {
        if (state == null) return;

        if (!string.IsNullOrEmpty(state.itemKey)) itemKey = state.itemKey;
        if (!string.IsNullOrEmpty(state.displayName)) displayName = state.displayName;
        if (state.baseQuantityPerCycle > 0) baseQuantityPerCycle = state.baseQuantityPerCycle;
        if (state.quantityPerHighestStage >= 0) quantityPerHighestStage = state.quantityPerHighestStage;
        if (state.productionIntervalSeconds > 0) productionIntervalSeconds = state.productionIntervalSeconds;
        if (state.maxStoredQuantity > 0) maxStoredQuantity = state.maxStoredQuantity;

        ApplyServerSnapshot(state.serverUnixSeconds, state.lastSettledServerUnixSeconds, state.storedQuantity, state.highestClearedStage);
    }

    private void ApplyBackendCollection(IdleAdventureAfkRewardCollectResponse response)
    {
        if (response == null) return;

        if (response.collectedItem != null && response.collectedItem.quantity > 0)
        {
            string collectedKey = string.IsNullOrEmpty(response.collectedItem.itemKey) ? ItemKey : response.collectedItem.itemKey;
            string collectedName = string.IsNullOrEmpty(response.collectedItem.name) ? DisplayName : response.collectedItem.name;
            int collectedQuantity = response.collectedItem.quantity;

            Dictionary<string, int> rewards = new Dictionary<string, int>
            {
                { collectedKey, collectedQuantity }
            };

            Dictionary<string, string> displayNames = new Dictionary<string, string>
            {
                { collectedKey, collectedName }
            };

            if (rewardLedger != null) rewardLedger.GrantRewards(rewards, displayNames, $"{collectedName} x{collectedQuantity}");
            rewardCollected.Invoke(collectedKey, collectedName, collectedQuantity);
        }

        if (response.state != null) ApplyBackendState(response.state);
        else ClearStoredReward();
    }

    private void CollectLocalEstimate()
    {
        long serverNow = GetEstimatedServerUnixSeconds();
        int amount = CalculateStoredQuantity(serverNow);
        if (amount <= 0) return;

        Dictionary<string, int> rewards = new Dictionary<string, int>
        {
            { ItemKey, amount }
        };

        Dictionary<string, string> displayNames = new Dictionary<string, string>
        {
            { ItemKey, DisplayName }
        };

        if (rewardLedger != null) rewardLedger.GrantRewards(rewards, displayNames, $"{DisplayName} x{amount}");
        rewardCollected.Invoke(ItemKey, DisplayName, amount);

        syncedServerUnixSeconds = serverNow;
        lastSettledServerUnixSeconds = serverNow;
        serverStoredQuantity = 0;
        localSecondsSinceServerSync = 0f;
        hasServerSnapshot = true;
        RefreshView();
    }

    public void ClearStoredReward()
    {
        long serverNow = GetEstimatedServerUnixSeconds();
        syncedServerUnixSeconds = serverNow;
        lastSettledServerUnixSeconds = serverNow;
        serverStoredQuantity = 0;
        localSecondsSinceServerSync = 0f;
        hasServerSnapshot = true;
        RefreshView();
    }

    private void EnsureInitialServerSnapshot()
    {
        if (hasServerSnapshot) return;

        long serverNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        syncedServerUnixSeconds = serverNow;
        lastSettledServerUnixSeconds = serverNow;
        localSecondsSinceServerSync = 0f;
        hasServerSnapshot = true;
    }

    private long GetEstimatedServerUnixSeconds()
    {
        EnsureInitialServerSnapshot();
        return syncedServerUnixSeconds + Mathf.Max(0, Mathf.FloorToInt(localSecondsSinceServerSync));
    }

    private int CalculateStoredQuantity(long serverUnixSeconds)
    {
        int baseStored = Mathf.Clamp(serverStoredQuantity, 0, MaxStoredQuantity);
        if (lastSettledServerUnixSeconds <= 0L) return baseStored;

        long elapsedSeconds = Math.Max(0L, serverUnixSeconds - lastSettledServerUnixSeconds);
        int cycleCount = Mathf.FloorToInt(elapsedSeconds / Mathf.Max(1f, productionIntervalSeconds));
        long produced = (long)cycleCount * QuantityPerCycle;
        return Mathf.Clamp(baseStored + (int)Math.Min(int.MaxValue, produced), 0, MaxStoredQuantity);
    }

    private float CalculateProgress(long serverUnixSeconds)
    {
        if (CalculateStoredQuantity(serverUnixSeconds) >= MaxStoredQuantity) return 1f;
        if (lastSettledServerUnixSeconds <= 0L) return 0f;

        long elapsedSeconds = Math.Max(0L, serverUnixSeconds - lastSettledServerUnixSeconds);
        float interval = Mathf.Max(1f, productionIntervalSeconds);
        return Mathf.Clamp01((elapsedSeconds % interval) / interval);
    }

    private int CalculateRemainingSeconds(long serverUnixSeconds)
    {
        if (CalculateStoredQuantity(serverUnixSeconds) >= MaxStoredQuantity) return 0;
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

    private void RefreshView()
    {
        long serverNow = GetEstimatedServerUnixSeconds();
        int amount = CalculateStoredQuantity(serverNow);
        bool ready = amount > 0;
        bool full = amount >= MaxStoredQuantity;

        if (collectButton != null) collectButton.interactable = ready;
        if (iconImage != null) iconImage.color = ready ? new Color(0.95f, 0.72f, 0.24f, 1f) : new Color(0.32f, 0.36f, 0.40f, 1f);
        if (progressFill != null) progressFill.fillAmount = ready ? 1f : CalculateProgress(serverNow);
        if (iconText != null) iconText.text = ResolveIconText();
        if (amountText != null) amountText.text = $"x{amount}/{MaxStoredQuantity}";
        if (timerText != null) timerText.text = full ? "Full" : ready ? "Ready" : FormatRemainingTime(CalculateRemainingSeconds(serverNow));
    }

    private string ResolveIconText()
    {
        string source = DisplayName;
        return string.IsNullOrEmpty(source) ? "R" : source.Substring(0, 1).ToUpperInvariant();
    }

    private string FormatRemainingTime(int remainingSeconds)
    {
        int minutes = remainingSeconds / 60;
        int seconds = remainingSeconds % 60;
        return minutes > 0 ? $"{minutes}:{seconds:00}" : $"{seconds}s";
    }
}
