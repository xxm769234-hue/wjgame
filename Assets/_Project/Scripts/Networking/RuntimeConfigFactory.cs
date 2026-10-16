using System;
using System.Collections.Generic;
using UnityEngine;

public static class RuntimeConfigFactory
{
    public static LandConfig ResolveLand(LandTileResponse response)
    {
        if (response == null) return FindAnyLandTemplate();

        LandConfig existing = FindLandById(response.landId);
        LandConfig config = existing != null ? UnityEngine.Object.Instantiate(existing) : CreateLandFromTemplate();
        if (config == null) return null;

        config.landId = response.landId;
        config.landName = string.IsNullOrEmpty(response.landName) ? $"Land {response.landId}" : response.landName;
        config.unlockLevel = Mathf.Max(1, response.level);
        config.landType = ParseLandType(response.landType);
        config.backendLandType = response.landType ?? string.Empty;
        config.allowBuildingType = SplitCsv(response.allowBuildType);
        return config;
    }

    public static LandConfig ResolveLand(LandConfigResponse response)
    {
        if (response == null) return FindAnyLandTemplate();

        int landId = ToRuntimeId(response.landId);
        LandConfig existing = FindLandById(landId);
        LandConfig config = existing != null ? UnityEngine.Object.Instantiate(existing) : CreateLandFromTemplate();
        if (config == null) return null;

        config.landId = landId;
        config.landName = string.IsNullOrEmpty(response.name) ? $"Land {landId}" : response.name;
        config.description = response.cannotPlaceReason;
        config.unlockLevel = Mathf.Max(1, response.unlockLevel);
        config.landType = ParseLandType(response.landType);
        config.backendLandType = response.landType ?? string.Empty;
        config.goldCost = response.goldCost;
        config.woodCost = response.woodCost;
        config.stoneCost = response.stoneCost;
        config.ironCost = response.ironCost;
        config.cost = SumCostForLegacy(response.goldCost, response.woodCost, response.stoneCost, response.ironCost);
        config.canPlace = response.canPlace;
        config.disabledReason = response.cannotPlaceReason;
        config.allowBuildingType = SplitCsv(response.allowBuildType);
        return config;
    }

    public static BuildingConfig ResolveBuilding(BuildingResponse response)
    {
        if (response == null) return FindAnyBuildingTemplate();

        BuildingConfig existing = FindBuildingById(response.buildingId);
        BuildingConfig config = existing != null ? UnityEngine.Object.Instantiate(existing) : CreateBuildingFromTemplate();
        if (config == null) return null;

        config.buildingId = response.buildingId;
        config.buildingName = string.IsNullOrEmpty(response.buildingName) ? $"Building {response.buildingId}" : response.buildingName;
        if (existing == null || string.IsNullOrEmpty(config.buildingType) || string.Equals(config.buildingType, "Default", StringComparison.OrdinalIgnoreCase))
        {
            config.buildingType = InferBuildType(response);
        }
        ApplyProduce(config, response.produce);
        return config;
    }

    public static BuildingConfig ResolveBuilding(BuildingConfigResponse response)
    {
        if (response == null) return FindAnyBuildingTemplate();

        int id = ToRuntimeId(response.id > 0L ? response.id : response.buildingId);
        BuildingConfig existing = FindBuildingById(id);
        BuildingConfig config = existing != null ? UnityEngine.Object.Instantiate(existing) : CreateBuildingFromTemplate();
        if (config == null) return null;

        config.buildingId = id;
        config.buildingName = string.IsNullOrEmpty(response.name) ? $"Building {id}" : response.name;
        config.description = response.description;
        config.goldCost = response.goldCost;
        config.woodCost = response.woodCost;
        config.stoneCost = response.stoneCost;
        config.ironCost = response.ironCost;
        config.cost = SumCostForLegacy(response.goldCost, response.woodCost, response.stoneCost, response.ironCost);
        config.buildSeconds = Mathf.Max(0, response.buildTime);
        config.buildingType = string.IsNullOrEmpty(response.buildType) ? response.type : response.buildType;
        config.requiredLandType = response.needLand ?? string.Empty;
        config.canBuild = response.canBuild;
        config.disabledReason = response.cannotBuildReason;
        config.singletonGroupCode = response.singletonGroupCode;
        config.ownedCount = Mathf.Max(0, response.ownedCount);
        config.maxOwnedCount = Mathf.Max(0, response.maxOwnedCount);
        config.hasOwnershipLimit = !string.IsNullOrEmpty(response.singletonGroupCode) && response.maxOwnedCount > 0;
        config.ownershipLimitMet = response.ownershipLimitMet;
        ApplyProduce(config, response.produce);
        return config;
    }

    public static BuildingRuntimeStatus ParseBuildingStatus(string value)
    {
        if (string.Equals(value, "UPGRADING", StringComparison.OrdinalIgnoreCase)) return BuildingRuntimeStatus.UPGRADING;
        if (string.Equals(value, "BUILDING", StringComparison.OrdinalIgnoreCase)) return BuildingRuntimeStatus.BUILDING;
        return BuildingRuntimeStatus.COMPLETED;
    }

    private static LandConfig FindLandById(int landId)
    {
        LandConfig[] configs = Resources.FindObjectsOfTypeAll<LandConfig>();
        for (int i = 0; i < configs.Length; i++)
        {
            if (configs[i] != null && configs[i].landId == landId) return configs[i];
        }
        return null;
    }

    private static LandConfig FindAnyLandTemplate()
    {
        LandConfig[] configs = Resources.FindObjectsOfTypeAll<LandConfig>();
        for (int i = 0; i < configs.Length; i++)
        {
            if (configs[i] != null) return configs[i];
        }
        return null;
    }

    private static LandConfig CreateLandFromTemplate()
    {
        LandConfig template = FindAnyLandTemplate();
        if (template != null) return UnityEngine.Object.Instantiate(template);

        LandConfig config = ScriptableObject.CreateInstance<LandConfig>();
        config.landName = "Remote Land";
        config.description = "Loaded from backend";
        return config;
    }

    private static BuildingConfig FindBuildingById(int buildingId)
    {
        BuildingConfig[] configs = Resources.FindObjectsOfTypeAll<BuildingConfig>();
        for (int i = 0; i < configs.Length; i++)
        {
            if (configs[i] != null && configs[i].buildingId == buildingId) return configs[i];
        }
        return null;
    }

    private static BuildingConfig FindAnyBuildingTemplate()
    {
        if (BuildingManager.Instance != null && BuildingManager.Instance.AvailableBuildings.Count > 0)
        {
            return BuildingManager.Instance.AvailableBuildings[0];
        }

        BuildingConfig[] configs = Resources.FindObjectsOfTypeAll<BuildingConfig>();
        for (int i = 0; i < configs.Length; i++)
        {
            if (configs[i] != null) return configs[i];
        }
        return null;
    }

    private static BuildingConfig CreateBuildingFromTemplate()
    {
        BuildingConfig template = FindAnyBuildingTemplate();
        if (template != null) return UnityEngine.Object.Instantiate(template);

        BuildingConfig config = ScriptableObject.CreateInstance<BuildingConfig>();
        config.buildingName = "Remote Building";
        config.canBuild = true;
        return config;
    }

    private static LandType ParseLandType(string value)
    {
        if (string.IsNullOrEmpty(value)) return LandType.Grassland;

        string normalized = value.Replace("_", string.Empty);
        LandType parsed;
        return Enum.TryParse(normalized, true, out parsed) ? parsed : LandType.Grassland;
    }

    private static string[] SplitCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return new string[0];
        string[] raw = value.Split(',');
        for (int i = 0; i < raw.Length; i++) raw[i] = raw[i].Trim();
        return raw;
    }

    private static int SumCostForLegacy(long gold, long wood, long stone, long iron)
    {
        long total = Math.Max(0L, gold) + Math.Max(0L, wood) + Math.Max(0L, stone) + Math.Max(0L, iron);
        return total > int.MaxValue ? int.MaxValue : (int)total;
    }

    private static int ToRuntimeId(long value)
    {
        if (value <= 0L) return 0;
        if (value > int.MaxValue) throw new OverflowException($"Backend identifier {value} exceeds the current Unity runtime identifier range.");
        return (int)value;
    }

    private static void ApplyProduce(BuildingConfig config, string produce)
    {
        if (config == null || string.IsNullOrEmpty(produce)) return;

        string outputPart = produce;
        string costPart = string.Empty;
        SplitProduceDefinition(produce, out outputPart, out costPart);

        string[] first = outputPart.Split(',')[0].Split(':');
        if (first.Length == 0) return;

        config.canProduceResource = true;
        config.resourceName = first[0].Trim();
        if (first.Length > 1 && int.TryParse(first[1].Trim(), out int amount))
        {
            config.resourceAmount = Mathf.Max(1, amount);
        }

        config.productionCosts = ParseProductionCosts(costPart);
    }

    private static void SplitProduceDefinition(string produce, out string outputPart, out string costPart)
    {
        outputPart = produce;
        costPart = string.Empty;

        int index = produce.IndexOf('|');
        if (index < 0) return;

        outputPart = produce.Substring(0, index).Trim();
        costPart = produce.Substring(index + 1).Trim();
    }

    private static ProductionResourceCost[] ParseProductionCosts(string costPart)
    {
        if (string.IsNullOrWhiteSpace(costPart)) return new ProductionResourceCost[0];

        List<ProductionResourceCost> costs = new List<ProductionResourceCost>();
        string[] tokens = costPart.Split(new[] { ',', ';', '+' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i].Trim();
            if (string.IsNullOrEmpty(token)) continue;

            int separator = token.IndexOf(':');
            if (separator < 0) separator = token.IndexOf('=');
            if (separator <= 0 || separator >= token.Length - 1) continue;

            string key = token.Substring(0, separator).Trim();
            string rawAmount = token.Substring(separator + 1).Trim();
            if (string.IsNullOrEmpty(key) || !int.TryParse(rawAmount, out int amount) || amount <= 0) continue;

            costs.Add(new ProductionResourceCost
            {
                itemKey = key,
                itemName = key,
                amount = amount
            });
        }

        return costs.ToArray();
    }

    private static string InferBuildType(BuildingResponse response)
    {
        if (response == null) return "Default";
        if (!string.IsNullOrEmpty(response.buildingCode))
        {
            int separator = response.buildingCode.IndexOf('_');
            if (separator > 0) return response.buildingCode.Substring(0, separator).ToUpperInvariant();
        }

        return string.IsNullOrEmpty(response.buildingType) ? "Default" : response.buildingType;
    }
}
