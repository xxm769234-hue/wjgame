using System;
using UnityEngine;

[Serializable]
public sealed class ProductionResourceCost
{
    public string itemKey = "iron";
    public string itemName = "铁矿";
    [Min(1)] public int amount = 1;

    public bool IsValid => !string.IsNullOrWhiteSpace(itemKey) && amount > 0;
}
