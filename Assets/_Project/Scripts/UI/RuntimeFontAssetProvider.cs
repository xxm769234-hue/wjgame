using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Resolves the shared Source Han Sans TMP font and applies it to loaded UI text.
/// </summary>
public sealed class RuntimeFontAssetProvider : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset fontAsset;
    [SerializeField] private Font legacyFont;
    private static TMP_FontAsset cachedFontAsset;
    private static Font cachedLegacyFont;
    private static RuntimeFontAssetProvider runtimeWatcher;
    private float nextTextScanTime;

    /// <summary>Returns the font assigned to this provider component.</summary>
    public TMP_FontAsset FontAsset => fontAsset;
    public Font LegacyFont => legacyFont;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplyFontToLoadedTexts()
    {
        ScanLoadedTexts();

        if (runtimeWatcher != null)
        {
            return;
        }

        GameObject watcherObject = new GameObject("FrontendTextLocalizationRuntime");
        Object.DontDestroyOnLoad(watcherObject);
        runtimeWatcher = watcherObject.AddComponent<RuntimeFontAssetProvider>();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextTextScanTime)
        {
            return;
        }

        nextTextScanTime = Time.unscaledTime + 0.25f;
        ScanLoadedTexts();
    }

    private static void ScanLoadedTexts()
    {
        TMP_FontAsset resolvedFont = Resolve();
        TMP_Text[] texts = FindObjectsOfType<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null)
            {
                if (IsEditableInputText(texts[i]))
                {
                    continue;
                }

                if (resolvedFont != null && texts[i].font != resolvedFont)
                {
                    texts[i].font = resolvedFont;
                }

                string normalized = FrontendTextLocalization.Normalize(texts[i].text);
                if (texts[i].text != normalized)
                {
                    texts[i].text = normalized;
                }
            }
        }

        Font resolvedLegacyFont = ResolveLegacy();
        Text[] legacyTexts = FindObjectsOfType<Text>(true);
        for (int i = 0; i < legacyTexts.Length; i++)
        {
            if (legacyTexts[i] != null)
            {
                if (IsEditableInputText(legacyTexts[i]))
                {
                    continue;
                }

                if (resolvedLegacyFont != null && legacyTexts[i].font != resolvedLegacyFont)
                {
                    legacyTexts[i].font = resolvedLegacyFont;
                }

                string normalized = FrontendTextLocalization.Normalize(legacyTexts[i].text);
                if (legacyTexts[i].text != normalized)
                {
                    legacyTexts[i].text = normalized;
                }
            }
        }
    }

    private static bool IsEditableInputText(TMP_Text text)
    {
        TMP_InputField input = text.GetComponentInParent<TMP_InputField>(true);
        return input != null && input.textComponent == text;
    }

    private static bool IsEditableInputText(Text text)
    {
        InputField input = text.GetComponentInParent<InputField>(true);
        return input != null && input.textComponent == text;
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

    public static Font ResolveLegacy()
    {
        if (cachedLegacyFont != null)
        {
            return cachedLegacyFont;
        }

        RuntimeFontAssetProvider provider = Resources.Load<RuntimeFontAssetProvider>("UI/SourceHanSansSC-Normal SDF");
        cachedLegacyFont = provider != null ? provider.legacyFont : null;
        return cachedLegacyFont;
    }
}
