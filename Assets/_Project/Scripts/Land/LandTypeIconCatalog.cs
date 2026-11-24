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

    public static Vector2 GetUiVisualScale(string landTypeKey)
    {
        switch (Normalize(landTypeKey))
        {
            case "MOUNTAIN": return new Vector2(1.24f, 1.26f);
            case "FOREST": return new Vector2(1.28f, 1.28f);
            case "VOLCANO": return new Vector2(1.34f, 1.35f);
            case "OCEAN": return new Vector2(1.31f, 1.37f);
            case "GRASSLAND": return new Vector2(1.28f, 1.29f);
            case "SNOWFIELD": return new Vector2(1.29f, 1.29f);
            case "WATER": return new Vector2(1.31f, 1.37f);
            case "SNOW": return new Vector2(1.29f, 1.29f);
            default: return Vector2.one;
        }
    }

    public static Vector2 GetUiVisualOffset(string landTypeKey)
    {
        switch (Normalize(landTypeKey))
        {
            case "MOUNTAIN": return new Vector2(0f, 1.4f);
            case "FOREST": return new Vector2(0f, 0.5f);
            case "VOLCANO": return new Vector2(0f, 2.2f);
            case "OCEAN": return new Vector2(0f, -2.1f);
            case "GRASSLAND": return new Vector2(0f, 0.6f);
            case "SNOWFIELD": return new Vector2(0f, 2.5f);
            case "WATER": return new Vector2(0f, -2.1f);
            case "SNOW": return new Vector2(0f, 2.5f);
            default: return Vector2.zero;
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
