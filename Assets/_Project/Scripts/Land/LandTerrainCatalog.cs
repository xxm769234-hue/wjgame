using System;
using System.Collections.Generic;
using UnityEngine;

public static class LandTerrainCatalog
{
    private const string ResourceRoot = "LandTerrains/";

    private static readonly Dictionary<string, string> ResourceNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "GRASSLAND", "Grassland" },
        { "OCEAN", "Ocean" },
        { "WATER", "Ocean" },
        { "VOLCANO", "Volcano" },
        { "FOREST", "Forest" },
        { "MOUNTAIN", "Mountain" },
        { "SNOWFIELD", "Snowfield" },
        { "SNOW", "Snowfield" }
    };

    private static readonly Dictionary<string, Sprite> LoadedTerrains = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

    public static Sprite GetTerrain(LandConfig config)
    {
        return config == null ? null : GetTerrain(config.LandTypeKey);
    }

    public static Sprite GetTerrain(string landTypeKey)
    {
        string normalized = Normalize(landTypeKey);
        if (string.IsNullOrEmpty(normalized) || !ResourceNames.TryGetValue(normalized, out string resourceName)) return null;
        if (LoadedTerrains.TryGetValue(normalized, out Sprite loaded) && loaded != null) return loaded;

        Sprite terrain = Resources.Load<Sprite>(ResourceRoot + resourceName);
        if (terrain != null) LoadedTerrains[normalized] = terrain;
        return terrain;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return value.Trim().Replace("_", string.Empty).ToUpperInvariant();
    }
}
