using TMPro;
using UnityEngine;

/// <summary>
/// Resolves the shared Source Han Sans TMP font and applies it to loaded UI text.
/// </summary>
public sealed class RuntimeFontAssetProvider : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset fontAsset;
    private static TMP_FontAsset cachedFontAsset;

    /// <summary>Returns the font assigned to this provider component.</summary>
    public TMP_FontAsset FontAsset => fontAsset;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplyFontToLoadedTexts()
    {
        TMP_FontAsset resolvedFont = Resolve();
        if (resolvedFont == null)
        {
            return;
        }

        TMP_Text[] texts = FindObjectsOfType<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null)
            {
                texts[i].font = resolvedFont;
            }
        }
    }

    /// <summary>Loads the project-wide Source Han Sans TMP font.</summary>
    public static TMP_FontAsset Resolve()
    {
        if (cachedFontAsset != null)
        {
            return cachedFontAsset;
        }

        RuntimeFontAssetProvider provider = Resources.Load<RuntimeFontAssetProvider>("UI/SourceHanSansSC-Normal SDF");
        cachedFontAsset = provider != null && provider.fontAsset != null
            ? provider.fontAsset
            : TMP_Settings.defaultFontAsset;
        return cachedFontAsset;
    }
}
