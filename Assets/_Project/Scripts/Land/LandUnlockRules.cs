using UnityEngine;

/// <summary>Central rules for unlocking complete four-slot land units.</summary>
public static class LandUnlockRules
{
    public const int UnitSideLength = 2;
    public const int SlotsPerUnit = UnitSideLength * UnitSideLength;
    public const int MapUnitColumns = 4;
    public const int MapUnitRows = 2;
    public const int TotalUnitCount = MapUnitColumns * MapUnitRows;
    public const int DefaultPaidSlotGoldCost = 1000;

    private static readonly int[] RequiredLevels = { 1, 5, 10, 10, 15, 15, 15, 15 };

    public static int GetUnlockedUnitCount(int playerLevel, int totalUnitCount)
    {
        if (totalUnitCount <= 0) return 0;

        int canonicalUnitCount = Mathf.Min(totalUnitCount, TotalUnitCount);
        int unlockedUnits = 0;
        for (int unitIndex = 0; unitIndex < canonicalUnitCount; unitIndex++)
        {
            if (IsLevelUnlocked(playerLevel, unitIndex)) unlockedUnits++;
        }
        return ApplyAdditionalRules(playerLevel, unlockedUnits, totalUnitCount);
    }

    public static int GetUnlockedSlotCount(int playerLevel, int totalUnitCount)
    {
        return GetUnlockedUnitCount(playerLevel, totalUnitCount) * SlotsPerUnit;
    }

    /// <summary>
    /// Canonical order: right page from bottom to top, then left page from bottom to top.
    /// Units within each row progress from right to left.
    /// </summary>
    public static int ResolveUnitIndex(int unitColumn, int unitRow)
    {
        if (unitColumn < 0 || unitColumn >= MapUnitColumns || unitRow < 0 || unitRow >= MapUnitRows)
        {
            return -1;
        }

        if (unitColumn >= 2)
        {
            return unitRow * 2 + (3 - unitColumn);
        }

        return 4 + unitRow * 2 + (1 - unitColumn);
    }

    public static int GetRequiredPlayerLevel(int unitIndex)
    {
        if (unitIndex < 0 || unitIndex >= RequiredLevels.Length) return int.MaxValue;
        return RequiredLevels[unitIndex];
    }

    public static bool IsLevelUnlocked(int playerLevel, int unitIndex)
    {
        return Mathf.Max(1, playerLevel) >= GetRequiredPlayerLevel(unitIndex);
    }

    public static bool RequiresGoldPurchase(int unitIndex)
    {
        return unitIndex >= 2 && unitIndex < TotalUnitCount;
    }

    private static int ApplyAdditionalRules(int playerLevel, int unlockedUnitCount, int totalUnitCount)
    {
        // Keep future task, resource, or story requirements isolated from map placement code.
        return Mathf.Clamp(unlockedUnitCount, 0, totalUnitCount);
    }
}
