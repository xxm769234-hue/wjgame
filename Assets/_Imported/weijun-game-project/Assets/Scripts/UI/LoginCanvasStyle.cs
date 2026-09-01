using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Font = TMPro.TMP_FontAsset;

[DisallowMultipleComponent]
public sealed class LoginCanvasStyle : MonoBehaviour
{
    [Header("Canvas")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField, Range(0f, 1f)] private float matchWidthOrHeight;
    [SerializeField] private int sortingOrder = 100;

    [Header("Fonts")]
    [SerializeField] private Font font;
    [SerializeField, Range(0.5f, 2f)] private float fontSizeMultiplier = 1f;
    [SerializeField] private bool overrideFontStyles;
    [SerializeField] private FontStyle titleFontStyle = FontStyle.Normal;
    [SerializeField] private FontStyle bodyFontStyle = FontStyle.Normal;

    [Header("Sprites")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Sprite paperSprite;
    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Sprite inputSprite;
    [SerializeField] private Sprite primaryButtonSprite;
    [SerializeField] private Sprite secondaryButtonSprite;
    [SerializeField] private Sprite weChatButtonSprite;

    [Header("Surface Colors")]
    [SerializeField] private Color backgroundColor = new Color(0.48f, 0.30f, 0.16f, 1f);
    [SerializeField] private Color paperColor = new Color(0.98f, 0.95f, 0.86f, 1f);
    [SerializeField] private Color panelColor = new Color(0.42f, 0.25f, 0.12f, 0.42f);
    [SerializeField] private Color shadowColor = new Color(0.18f, 0.10f, 0.05f, 0.28f);
    [SerializeField] private Color buttonShadowColor = new Color(0.18f, 0.10f, 0.05f, 0.24f);
    [SerializeField] private Color decorLineColor = new Color(0.77f, 0.45f, 0.22f, 0.72f);
    [SerializeField] private Color inputColor = new Color(0.99f, 0.97f, 0.90f, 1f);
    [SerializeField] private Color inputHoverColor = new Color(0.92f, 0.95f, 1f, 1f);

    [Header("Button Colors")]
    [SerializeField] private Color primaryButtonColor = new Color(0.09f, 0.28f, 0.58f, 1f);
    [SerializeField] private Color secondaryButtonColor = new Color(0.76f, 0.83f, 0.92f, 1f);
    [SerializeField] private Color weChatButtonColor = new Color(0.16f, 0.70f, 0.32f, 1f);

    [Header("Text Colors")]
    [SerializeField] private Color titleTextColor = new Color(0.74f, 0.20f, 0.15f, 1f);
    [SerializeField] private Color labelTextColor = new Color(0.82f, 0.24f, 0.18f, 1f);
    [SerializeField] private Color paperTextColor = new Color(0.28f, 0.16f, 0.09f, 1f);

    public Color PaperColor => paperColor;
    public Color PanelColor => panelColor;
    public Color ShadowColor => shadowColor;
    public Color ButtonShadowColor => buttonShadowColor;
    public Color DecorLineColor => decorLineColor;
    public Color InputColor => inputColor;
    public Color InputHoverColor => inputHoverColor;
    public Color LabelTextColor => labelTextColor;
    public Color PaperTextColor => paperTextColor;

    public void ConfigureCanvas(Canvas canvas, CanvasScaler scaler)
    {
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
        }

        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.matchWidthOrHeight = matchWidthOrHeight;
        }
    }

    public Font ResolveFont(Font fallbackFont)
    {
        return font != null ? font : fallbackFont;
    }

    public int ResolveFontSize(int defaultSize)
    {
        return Mathf.Max(8, Mathf.RoundToInt(defaultSize * fontSizeMultiplier));
    }

    public FontStyles ResolveFontStyle(string objectName, FontStyle fallbackStyle)
    {
        if (font != null)
        {
            return FontStyles.Normal;
        }

        if (!overrideFontStyles)
        {
            return ConvertFontStyle(fallbackStyle);
        }

        return ConvertFontStyle(IsTitle(objectName) ? titleFontStyle : bodyFontStyle);
    }

    public Color ResolveTextColor(string objectName, Color fallbackColor)
    {
        if (objectName == "LogoText")
        {
            return titleTextColor;
        }

        if (objectName.EndsWith("InputLabel", System.StringComparison.Ordinal))
        {
            return labelTextColor;
        }

        return fallbackColor;
    }

    public void ApplyBackground(Image image)
    {
        ApplyImage(image, backgroundSprite, backgroundColor);
    }

    public void ApplyPaper(Image image)
    {
        ApplyImage(image, paperSprite, paperColor);
    }

    public void ApplyPanel(Image image)
    {
        ApplyImage(image, panelSprite, panelColor);
    }

    public void ApplyInput(Image image)
    {
        ApplyImage(image, inputSprite, inputColor);
    }

    public void ApplyButton(Image image, string objectName, Color fallbackColor)
    {
        ApplyImage(image, ResolveButtonSprite(objectName), ResolveButtonColor(objectName, fallbackColor));
    }

    public Color ResolveButtonColor(string objectName, Color fallbackColor)
    {
        switch (objectName)
        {
            case "LoginButton":
                return primaryButtonColor;
            case "RegisterButton":
                return secondaryButtonColor;
            case "WeChatLoginButton":
            case "WeChatButton":
                return weChatButtonColor;
            default:
                return fallbackColor;
        }
    }

    private Sprite ResolveButtonSprite(string objectName)
    {
        switch (objectName)
        {
            case "LoginButton":
                return primaryButtonSprite;
            case "RegisterButton":
                return secondaryButtonSprite;
            case "WeChatLoginButton":
            case "WeChatButton":
                return weChatButtonSprite;
            default:
                return secondaryButtonSprite;
        }
    }

    private static bool IsTitle(string objectName)
    {
        return objectName == "LogoText"
            || objectName == "LogoSubtitle"
            || objectName == "BrandTitle"
            || objectName == "FormTitle"
            || objectName == "FormSubtitle"
            || objectName == "Title";
    }

    private static FontStyles ConvertFontStyle(FontStyle style)
    {
        switch (style)
        {
            case FontStyle.Bold:
                return FontStyles.Bold;
            case FontStyle.Italic:
                return FontStyles.Italic;
            case FontStyle.BoldAndItalic:
                return FontStyles.Bold | FontStyles.Italic;
            default:
                return FontStyles.Normal;
        }
    }

    private static void ApplyImage(Image image, Sprite sprite, Color fallbackColor)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = sprite;
        image.color = sprite != null ? Color.white : fallbackColor;
        image.type = sprite != null && HasBorder(sprite) ? Image.Type.Sliced : Image.Type.Simple;
    }

    private static bool HasBorder(Sprite sprite)
    {
        Vector4 border = sprite.border;
        return border.x > 0f || border.y > 0f || border.z > 0f || border.w > 0f;
    }

#if UNITY_EDITOR
    public void EditorSetFont(Font value)
    {
        font = value;
    }
#endif
}
