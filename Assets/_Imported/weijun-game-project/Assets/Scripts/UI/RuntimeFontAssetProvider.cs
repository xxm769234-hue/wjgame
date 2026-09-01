using System.Reflection;
using TMPro;
using UnityEngine;

/// <summary>
/// Resolves the shared TMP font for the imported UI assembly.
/// </summary>
public static class RuntimeFontAssetProvider
{
    private static TMP_FontAsset cachedFontAsset;
    private static Font cachedLegacyFont;

    /// <summary>Loads the Source Han Sans TMP font from its Resources preset.</summary>
    public static TMP_FontAsset Resolve()
    {
        if (cachedFontAsset != null)
        {
            return cachedFontAsset;
        }

        GameObject providerPrefab = Resources.Load<GameObject>("UI/SourceHanSansSC-Normal SDF");
        if (providerPrefab != null)
        {
            Component provider = providerPrefab.GetComponent("RuntimeFontAssetProvider");
            PropertyInfo fontProperty = provider == null
                ? null
                : provider.GetType().GetProperty("FontAsset", BindingFlags.Instance | BindingFlags.Public);
            if (fontProperty != null)
            {
                cachedFontAsset = fontProperty.GetValue(provider, null) as TMP_FontAsset;
            }
        }

        return cachedFontAsset != null ? cachedFontAsset : TMP_Settings.defaultFontAsset;
    }

    public static Font ResolveLegacy()
    {
        if (cachedLegacyFont != null)
        {
            return cachedLegacyFont;
        }

        GameObject providerPrefab = Resources.Load<GameObject>("UI/SourceHanSansSC-Normal SDF");
        if (providerPrefab != null)
        {
            Component provider = providerPrefab.GetComponent("RuntimeFontAssetProvider");
            PropertyInfo fontProperty = provider == null
                ? null
                : provider.GetType().GetProperty("LegacyFont", BindingFlags.Instance | BindingFlags.Public);
            if (fontProperty != null)
            {
                cachedLegacyFont = fontProperty.GetValue(provider, null) as Font;
            }
        }

        return cachedLegacyFont;
    }
}
