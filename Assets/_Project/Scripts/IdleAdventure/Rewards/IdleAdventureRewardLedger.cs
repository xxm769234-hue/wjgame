using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public sealed class IdleAdventureRewardLedgerEntry
{
    [SerializeField] private string itemKey;
    [SerializeField] private string displayName;
    [SerializeField] private int quantity;

    public IdleAdventureRewardLedgerEntry(string itemKey, string displayName, int quantity)
    {
        this.itemKey = itemKey;
        this.displayName = displayName;
        this.quantity = quantity;
    }

    public string ItemKey => itemKey;
    public string DisplayName => displayName;
    public int Quantity => quantity;

    public void Add(int amount)
    {
        quantity = Mathf.Max(0, quantity + amount);
    }
}

[Serializable]
public sealed class IdleAdventureRewardGrantedEvent : UnityEvent<string, string, int>
{
}

[Serializable]
public sealed class IdleAdventureRewardSettlementEvent : UnityEvent<string>
{
}

public sealed class IdleAdventureRewardLedger : MonoBehaviour
{
    [SerializeField] private List<IdleAdventureRewardLedgerEntry> grantedRewards = new List<IdleAdventureRewardLedgerEntry>();
    [SerializeField] private IdleAdventureRewardGrantedEvent rewardGranted = new IdleAdventureRewardGrantedEvent();
    [SerializeField] private IdleAdventureRewardSettlementEvent settlementCompleted = new IdleAdventureRewardSettlementEvent();

    public IReadOnlyList<IdleAdventureRewardLedgerEntry> GrantedRewards => grantedRewards;
    public IdleAdventureRewardGrantedEvent RewardGranted => rewardGranted;
    public IdleAdventureRewardSettlementEvent SettlementCompleted => settlementCompleted;

    public void GrantRewards(Dictionary<string, int> rewards, Dictionary<string, string> displayNames, string summary)
    {
        if (rewards == null || rewards.Count == 0)
        {
            settlementCompleted.Invoke(string.IsNullOrEmpty(summary) ? "None" : summary);
            return;
        }

        foreach (KeyValuePair<string, int> reward in rewards)
        {
            if (reward.Value <= 0) continue;

            string key = string.IsNullOrEmpty(reward.Key) ? "reward" : reward.Key;
            string displayName = displayNames != null && displayNames.ContainsKey(key) ? displayNames[key] : key;
            AddOrIncrease(key, displayName, reward.Value);
            rewardGranted.Invoke(key, displayName, reward.Value);
        }

        settlementCompleted.Invoke(string.IsNullOrEmpty(summary) ? "None" : summary);
    }

    public int GetGrantedQuantity(string itemKey)
    {
        int index = FindEntryIndex(itemKey);
        return index >= 0 ? grantedRewards[index].Quantity : 0;
    }

    public void ClearGrantedRewards()
    {
        grantedRewards.Clear();
    }

    private void AddOrIncrease(string itemKey, string displayName, int quantity)
    {
        int index = FindEntryIndex(itemKey);
        if (index >= 0)
        {
            grantedRewards[index].Add(quantity);
            return;
        }

        grantedRewards.Add(new IdleAdventureRewardLedgerEntry(itemKey, displayName, quantity));
    }

    private int FindEntryIndex(string itemKey)
    {
        if (string.IsNullOrEmpty(itemKey)) itemKey = "reward";

        for (int i = 0; i < grantedRewards.Count; i++)
        {
            IdleAdventureRewardLedgerEntry entry = grantedRewards[i];
            if (entry != null && entry.ItemKey == itemKey) return i;
        }

        return -1;
    }
}
