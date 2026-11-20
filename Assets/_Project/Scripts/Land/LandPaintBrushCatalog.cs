using System;
using System.Collections.Generic;
using UnityEngine;

public static class LandPaintBrushCatalog
{
    private const float PreviewWidthAt1080 = 192f;
    private const string ResourceRoot = "LandPaintBrushes/";
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
    private static readonly Dictionary<string, Sprite> LoadedBrushes = new Dictionary<string, Sprite>();

    public static Sprite GetBrush(LandConfig config)
    {
        if (config == null) return null;
        string key = config.LandTypeKey.Trim().Replace("_", string.Empty).ToUpperInvariant();
        if (!ResourceNames.TryGetValue(key, out string resourceName)) return null;
        if (LoadedBrushes.TryGetValue(resourceName, out Sprite brush) && brush != null) return brush;
        brush = Resources.Load<Sprite>(ResourceRoot + resourceName);
        if (brush != null) LoadedBrushes[resourceName] = brush;
        return brush;
    }

    public static float GetPreviewScale(Sprite brush, Camera camera)
    {
        if (brush == null) return 1f;
        float worldWidth = camera != null && camera.orthographic
            ? camera.orthographicSize * 2f * PreviewWidthAt1080 / 1080f
            : 1.2f;
        return worldWidth / Mathf.Max(0.001f, brush.bounds.size.x);
    }
}
