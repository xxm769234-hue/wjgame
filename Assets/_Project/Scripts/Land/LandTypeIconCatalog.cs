using System;
using System.Collections.Generic;
using UnityEngine;

public static class LandTypeIconCatalog
{
    public const float DisplayIconPixels = 120f;
    private const string ResourceRoot = "LandTypeIcons/";
    private static readonly Dictionary<string, string> ResourceNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "GRASSLAND", "Grassland" },
        { "FOREST", "Forest" },
        { "MOUNTAIN", "Mountain" },
        { "OCEAN", "Ocean" },
        { "SNOWFIELD", "Snowfield" },
        { "VOLCANO", "Volcano" },
        { "WATER", "Ocean" },
        { "SNOW", "Snowfield" }
    };

    private static readonly Dictionary<string, Sprite> LoadedIcons = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

    public static Sprite GetIcon(LandConfig config)
    {
        return config == null ? null : GetIcon(config.LandTypeKey);
    }

    public static Sprite GetIcon(string landTypeKey)
    {
        string normalized = Normalize(landTypeKey);
        if (string.IsNullOrEmpty(normalized) || !ResourceNames.TryGetValue(normalized, out string resourceName)) return null;
        if (LoadedIcons.TryGetValue(normalized, out Sprite loaded) && loaded != null) return loaded;

        Sprite icon = Resources.Load<Sprite>(ResourceRoot + resourceName);
        if (icon != null) LoadedIcons[normalized] = icon;
        return icon;
    }

    public static float GetUiVisualScale(string landTypeKey)
    {
        switch (Normalize(landTypeKey))
        {
            case "MOUNTAIN": return 1.23f;
            case "FOREST": return 1.31f;
            case "VOLCANO": return 2.0f;
            case "OCEAN": return 1.68f;
            case "GRASSLAND": return 1.88f;
            case "SNOWFIELD": return 1.66f;
            case "WATER": return 1.68f;
            case "SNOW": return 1.66f;
            default: return 1f;
        }
    }

    public static float GetPreviewScale(LandConfig config, Sprite icon, float targetVisibleHeight)
    {
        if (config == null || icon == null || targetVisibleHeight <= 0f) return 1f;

        float coverage = GetVisibleHeightCoverage(config.LandTypeKey);
        float sourceHeight = Mathf.Max(0.001f, icon.bounds.size.y);
        return targetVisibleHeight / (sourceHeight * coverage);
    }

    public static float GetPreviewVisibleWorldHeight(Camera camera)
    {
        if (camera == null || !camera.orthographic || camera.pixelHeight <= 0) return 0.6f;
        return camera.orthographicSize * 2f * DisplayIconPixels / camera.pixelHeight;
    }

    private static float GetVisibleHeightCoverage(string landTypeKey)
    {
        switch (Normalize(landTypeKey))
        {
            case "MOUNTAIN": return 0.812f;
            case "FOREST": return 0.760f;
            case "VOLCANO": return 0.500f;
            case "OCEAN": return 0.596f;
            case "GRASSLAND": return 0.532f;
            case "SNOWFIELD": return 0.605f;
            case "WATER": return 0.596f;
            case "SNOW": return 0.605f;
            default: return 1f;
        }
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return value.Trim().Replace("_", string.Empty).ToUpperInvariant();
    }
}
