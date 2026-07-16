using System;
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
        config.allowBuildingType = SplitCsv(response.allowBuildType);
        return config;
    }

    public static LandConfig ResolveLand(LandConfigResponse response)
    {
        if (response == null) return FindAnyLandTemplate();

        LandConfig existing = FindLandById(response.landId);
        LandConfig config = existing != null ? UnityEngine.Object.Instantiate(existing) : CreateLandFromTemplate();
        if (config == null) return null;

        config.landId = response.landId;
        config.landName = string.IsNullOrEmpty(response.name) ? $"Land {response.landId}" : response.name;
        config.description = response.cannotPlaceReason;
        config.unlockLevel = Mathf.Max(1, response.unlockLevel);
        config.landType = ParseLandType(response.landType);
        config.cost = response.goldCost + response.woodCost + response.stoneCost + response.ironCost;
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

        int id = response.id > 0 ? response.id : response.buildingId;
        BuildingConfig existing = FindBuildingById(id);
        BuildingConfig config = existing != null ? UnityEngine.Object.Instantiate(existing) : CreateBuildingFromTemplate();
        if (config == null) return null;

        config.buildingId = id;
        config.buildingName = string.IsNullOrEmpty(response.name) ? $"Building {id}" : response.name;
        config.description = response.description;
        config.cost = response.goldCost + response.woodCost + response.stoneCost + response.ironCost;
        config.buildSeconds = Mathf.Max(0, response.buildTime);
        config.buildingType = string.IsNullOrEmpty(response.buildType) ? response.type : response.buildType;
        config.canBuild = response.canBuild;
        config.disabledReason = response.cannotBuildReason;
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

    private static void ApplyProduce(BuildingConfig config, string produce)
    {
        if (config == null || string.IsNullOrEmpty(produce)) return;

        string[] first = produce.Split(',')[0].Split(':');
        if (first.Length == 0) return;

        config.canProduceResource = true;
        config.resourceName = first[0].Trim();
        if (first.Length > 1 && int.TryParse(first[1].Trim(), out int amount))
        {
            config.resourceAmount = Mathf.Max(1, amount);
        }
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
