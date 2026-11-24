using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LoginCanvasStyle : MonoBehaviour
{
    private static Sprite dialogPaperSprite;
    private static Sprite dialogPrimarySprite;
    private static Sprite dialogSecondarySprite;
    private static Sprite consentBoxSprite;
    private static Sprite checkmarkSprite;
    private static Sprite dialogCloseSprite;
    private static readonly Color DialogInk = new Color(0.28f, 0.17f, 0.10f, 1f);
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
    [SerializeField] private Sprite logoSprite;
    [SerializeField] private Sprite startGameButtonSprite;
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

    public static void ApplyCheckbox(Toggle toggle)
    {
        if (toggle == null) return;
        if (consentBoxSprite == null) consentBoxSprite = Resources.Load<Sprite>("UI/ApprovedUi/Controls/ConsentBox");
        if (checkmarkSprite == null) checkmarkSprite = Resources.Load<Sprite>("UI/ApprovedUi/Controls/Checkmark");
        if (toggle.targetGraphic is Image box && consentBoxSprite != null)
        {
            box.sprite = consentBoxSprite;
            box.overrideSprite = null;
            box.type = Image.Type.Simple;
            box.preserveAspect = true;
            box.color = Color.white;
        }
        if (toggle.graphic is Image checkmark && checkmarkSprite != null)
        {
            checkmark.sprite = checkmarkSprite;
            checkmark.overrideSprite = null;
            checkmark.type = Image.Type.Simple;
            checkmark.preserveAspect = true;
            checkmark.color = Color.white;
            checkmark.raycastTarget = false;
        }
    }

    public static void ApplyDialogPaper(Image image)
    {
        if (image == null) return;
        if (dialogPaperSprite == null)
            dialogPaperSprite = LoadSlicedSprite("IdleAdventureUI/Afk/Panel", new Vector4(200f, 160f, 200f, 160f));
        if (dialogPaperSprite == null) return;
        image.sprite = dialogPaperSprite;
        image.color = Color.white;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 2f;
        Outline outline = image.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
    }

    public static void ApplyDialogCloseButton(Button button)
    {
        if (button == null) return;
        if (dialogCloseSprite == null)
            dialogCloseSprite = Resources.Load<Sprite>("UI/ApprovedUi/Controls/05_close");
        Image hitArea = button.GetComponent<Image>();
        if (hitArea == null) hitArea = button.gameObject.AddComponent<Image>();
        hitArea.sprite = null;
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;
        Transform existingIcon = button.transform.Find("CloseIcon");
        Image icon = existingIcon != null ? existingIcon.GetComponent<Image>() : null;
        if (icon == null)
        {
            icon = new GameObject("CloseIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            icon.transform.SetParent(button.transform, false);
        }
        icon.sprite = dialogCloseSprite;
        icon.color = dialogCloseSprite != null ? Color.white : Color.clear;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        icon.rectTransform.anchoredPosition = new Vector2(0f, 12f);
        icon.rectTransform.sizeDelta = new Vector2(38f, 38f);
        button.targetGraphic = icon;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.pressedColor = new Color(0.78f, 0.72f, 0.62f, 1f);
        colors.disabledColor = new Color(0.65f, 0.65f, 0.65f, 0.6f);
        button.colors = colors;
        foreach (Text label in button.GetComponentsInChildren<Text>(true))
        {
            label.text = "关闭";
            label.fontSize = 18;
            label.fontStyle = FontStyle.Bold;
            label.color = DialogInk;
            label.resizeTextForBestFit = false;
            label.lineSpacing = 1f;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            PlaceCloseLabel(label.rectTransform);
        }
        foreach (TMPro.TMP_Text label in button.GetComponentsInChildren<TMPro.TMP_Text>(true))
        {
            label.text = "关闭";
            label.fontSize = 18f;
            label.fontStyle = TMPro.FontStyles.Bold;
            label.color = DialogInk;
            label.enableAutoSizing = false;
            label.enableWordWrapping = false;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.raycastTarget = false;
            PlaceCloseLabel(label.rectTransform);
        }
    }

    private static void PlaceCloseLabel(RectTransform label)
    {
        label.anchorMin = label.anchorMax = new Vector2(0.5f, 0.5f);
        label.pivot = new Vector2(0.5f, 0.5f);
        label.anchoredPosition = new Vector2(0f, -23f);
        label.sizeDelta = new Vector2(64f, 32f);
    }

    public static void ApplyDialogButton(Button button, bool primary, bool destructive = false)
    {
        if (button == null || !(button.targetGraphic is Image image)) return;
        if (dialogPrimarySprite == null)
            dialogPrimarySprite = LoadSlicedSprite("IdleAdventureUI/Afk/ButtonPrimary", new Vector4(38f, 38f, 38f, 38f));
        if (dialogSecondarySprite == null)
            dialogSecondarySprite = LoadSlicedSprite("IdleAdventureUI/Afk/ButtonSecondary", new Vector4(38f, 38f, 38f, 38f));
        Sprite sprite = primary ? dialogPrimarySprite : dialogSecondarySprite;
        if (sprite == null) return;
        image.sprite = sprite;
        image.overrideSprite = null;
        image.color = Color.white;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 2f;
        Color tint = destructive ? new Color(1f, 0.67f, 0.58f, 1f) : Color.white;
        ColorBlock colors = button.colors;
        colors.normalColor = tint;
        colors.highlightedColor = Color.Lerp(tint, Color.white, 0.12f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = tint * new Color(0.86f, 0.86f, 0.86f, 1f);
        colors.disabledColor = new Color(0.66f, 0.64f, 0.59f, 0.8f);
        colors.colorMultiplier = 1f;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = colors;
        Color buttonInk = new Color(0.22f, 0.12f, 0.07f, 1f);
        foreach (Text label in button.GetComponentsInChildren<Text>(true))
        {
            label.color = buttonInk;
            label.fontStyle = FontStyle.Bold;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 16;
            label.resizeTextMaxSize = Mathf.Max(20, label.fontSize);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
        }
        foreach (TMPro.TMP_Text label in button.GetComponentsInChildren<TMPro.TMP_Text>(true))
        {
            label.color = buttonInk;
            label.fontStyle = TMPro.FontStyles.Bold;
            label.enableWordWrapping = false;
            label.enableAutoSizing = true;
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = Mathf.Min(label.fontSize, Mathf.Max(14f, label.fontSize - 4f));
            label.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            label.margin = new Vector4(12f, 2f, 12f, 2f);
        }
        Transform oldShadow = button.transform.parent.Find(button.name + "Shadow");
        if (oldShadow != null) oldShadow.gameObject.SetActive(false);
    }

    public static void ApplyDialogInput(InputField input)
    {
        if (input == null || !(input.targetGraphic is Image image)) return;
        Sprite sprite = Resources.Load<Sprite>("IdleAdventureUI/RoundedPanel");
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
        }
        image.color = new Color(1f, 0.98f, 0.91f, 1f);
        Outline outline = image.GetComponent<Outline>();
        if (outline == null) outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.44f, 0.28f, 0.14f, 0.55f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        ColorBlock colors = input.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.97f, 0.94f, 0.85f, 1f);
        colors.disabledColor = new Color(0.80f, 0.77f, 0.69f, 1f);
        input.colors = colors;
        if (input.textComponent != null)
        {
            input.textComponent.color = DialogInk;
            input.textComponent.fontSize = 22;
            input.textComponent.resizeTextForBestFit = false;
        }
        if (input.placeholder is Text placeholder)
        {
            placeholder.color = new Color(0.49f, 0.38f, 0.27f, 1f);
            placeholder.fontSize = placeholder.resizeTextMaxSize = 20;
            placeholder.resizeTextMinSize = 16;
            placeholder.resizeTextForBestFit = true;
            placeholder.horizontalOverflow = HorizontalWrapMode.Overflow;
            placeholder.verticalOverflow = VerticalWrapMode.Truncate;
        }
        Transform oldShadow = input.transform.parent.Find(input.name + "Shadow");
        if (oldShadow != null) oldShadow.gameObject.SetActive(false);
    }

    private static Sprite LoadSlicedSprite(string resource, Vector4 border)
    {
        Sprite source = Resources.Load<Sprite>(resource);
        if (source == null) return null;
        return Sprite.Create(source.texture, source.rect, new Vector2(0.5f, 0.5f), source.pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
    }

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
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
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

    public FontStyle ResolveFontStyle(string objectName, FontStyle fallbackStyle)
    {
        if (font != null)
        {
            return FontStyle.Normal;
        }

        if (!overrideFontStyles)
        {
            return fallbackStyle;
        }

        return IsTitle(objectName) ? titleFontStyle : bodyFontStyle;
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

    public void ApplyLogo(Image image)
    {
        ApplyImage(image, logoSprite, Color.white);
    }

    public void ApplyStartGameButton(Image image)
    {
        ApplyImage(image, startGameButtonSprite, primaryButtonColor);
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

    public void EditorSetLoginSprites(Sprite newBackgroundSprite, Sprite newLogoSprite, Sprite newStartGameButtonSprite)
    {
        backgroundSprite = newBackgroundSprite;
        logoSprite = newLogoSprite;
        startGameButtonSprite = newStartGameButtonSprite;
    }
#endif
}
