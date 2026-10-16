using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public sealed class ResourceSaleDialogView : MonoBehaviour
{
    [Header("Colors")]
    [SerializeField] private Color overlayColor = new Color(0.08f, 0.04f, 0.01f, 0.72f);
    [SerializeField] private Color panelColor = new Color(0.96f, 0.78f, 0.43f, 1f);
    [SerializeField] private Color headerColor = new Color(0.43f, 0.20f, 0.07f, 1f);
    [SerializeField] private Color cardColor = new Color(1f, 0.90f, 0.66f, 1f);
    [SerializeField] private Color primaryColor = new Color(0.83f, 0.34f, 0.12f, 1f);
    [SerializeField] private Color secondaryColor = new Color(0.65f, 0.38f, 0.13f, 1f);
    [SerializeField] private Color inkColor = new Color(0.25f, 0.12f, 0.04f, 1f);

    private bool initialized;
    private GameObject quoteContent;
    private TMP_Text itemNameText;
    private TMP_Text ratioText;
    private TMP_Text limitsText;
    private TMP_Text lotCountText;
    private TMP_Text summaryText;
    private TMP_Text quoteStatusText;
    private Image sourceItemIconImage;
    private TMP_Text submitButtonText;
    private Button decreaseButton;
    private Button increaseButton;
    private Button maxButton;
    private Button submitButton;
    private Button closeButton;

    public bool EnsureBuilt(TMP_FontAsset fontAsset)
    {
        if (initialized)
        {
            return true;
        }

        if (TryBindSceneLayout())
        {
            initialized = true;
            return true;
        }

        Debug.LogError("ResourceSaleDialog is missing authored sale controls.", this);
        return false;

#if false

        RectTransform root = GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        Image blocker = GetComponent<Image>();
        blocker.color = overlayColor;
        blocker.raycastTarget = true;

        RectTransform panel = CreateImage("SaleDialogPanel", transform, panelColor);
        SetCentered(panel, new Vector2(760f, 620f));

        RectTransform header = CreateImage("Header", panel, headerColor);
        SetTopLeft(header, new Vector2(0f, 0f), new Vector2(760f, 62f));
        TMP_Text title = CreateText("Title", header, "资源出售", fontAsset, 28, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        SetTopLeft(title.rectTransform, new Vector2(24f, -8f), new Vector2(620f, 46f));

        closeButton = CreateButton("CloseButton", header, "X", fontAsset, 20, new Color(0.70f, 0.22f, 0.11f, 1f), Color.white);
        SetTopLeft(closeButton.GetComponent<RectTransform>(), new Vector2(698f, -10f), new Vector2(42f, 42f));

        quoteContent = CreateContainer("QuoteContent", panel);
        SetStretch(quoteContent.GetComponent<RectTransform>(), 0f, 0f, 74f, 0f);
        BuildQuoteContent(quoteContent.transform, fontAsset);

        gameObject.SetActive(false);
    }
#endif
    }

    public void ShowQuote(
        string itemName,
        string ratio,
        string limits,
        string lotCount,
        string summary,
        string status,
        string submitLabel,
        bool canDecrease,
        bool canIncrease,
        bool canSubmit,
        bool busy,
        UnityAction showQuoteAction,
        UnityAction showHistoryAction,
        UnityAction decreaseAction,
        UnityAction increaseAction,
        UnityAction maxAction,
        UnityAction refreshAction,
        UnityAction submitAction,
        UnityAction closeAction,
        string sourceIconKey)
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (quoteContent != null)
        {
            quoteContent.SetActive(true);
        }

        SetText(itemNameText, itemName);
        SetText(ratioText, ratio);
        SetText(limitsText, limits);
        SetText(lotCountText, lotCount);
        SetText(summaryText, summary);
        SetText(quoteStatusText, status);
        SetText(submitButtonText, busy ? "正在提交..." : submitLabel);
        SetResourceIcon(sourceIconKey, itemName);

        decreaseButton.interactable = !busy && canDecrease;
        increaseButton.interactable = !busy && canIncrease;
        maxButton.interactable = !busy && canIncrease;
        submitButton.interactable = !busy && canSubmit;

        Bind(decreaseButton, decreaseAction);
        Bind(increaseButton, increaseAction);
        Bind(maxButton, maxAction);
        Bind(submitButton, submitAction);
        Bind(closeButton, closeAction);
    }

    public void ShowHistory(
        string body,
        string status,
        bool hasMore,
        bool busy,
        UnityAction showQuoteAction,
        UnityAction showHistoryAction,
        UnityAction refreshAction,
        UnityAction loadMoreAction,
        UnityAction closeAction)
    {
        Debug.LogWarning("资源出售记录界面已移除。", this);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void BuildQuoteContent(Transform parent, TMP_FontAsset fontAsset)
    {
        itemNameText = CreateText("ItemName", parent, string.Empty, fontAsset, 24, FontStyle.Bold, inkColor, TextAnchor.MiddleLeft);
        SetTopLeft(itemNameText.rectTransform, new Vector2(30f, -6f), new Vector2(700f, 38f));

        RectTransform ratioCard = CreateImage("RatioCard", parent, cardColor);
        SetTopLeft(ratioCard, new Vector2(30f, -52f), new Vector2(700f, 68f));
        ratioText = CreateText("Ratio", ratioCard, string.Empty, fontAsset, 20, FontStyle.Bold, inkColor, TextAnchor.MiddleCenter);
        SetStretch(ratioText.rectTransform, 54f, 54f, 8f, 8f);
        sourceItemIconImage = CreateIcon("SourceItemIcon", ratioCard, new Vector2(12f, -18f));

        RectTransform limitsCard = CreateImage("LimitsCard", parent, new Color(0.91f, 0.67f, 0.31f, 1f));
        SetTopLeft(limitsCard, new Vector2(30f, -130f), new Vector2(700f, 64f));
        limitsText = CreateText("Limits", limitsCard, string.Empty, fontAsset, 16, FontStyle.Normal, inkColor, TextAnchor.MiddleCenter);
        limitsText.enableWordWrapping = true;
        SetStretch(limitsText.rectTransform, 12f, 12f, 6f, 6f);

        TMP_Text lotLabel = CreateText("LotLabel", parent, "出售批次", fontAsset, 18, FontStyle.Bold, inkColor, TextAnchor.MiddleLeft);
        SetTopLeft(lotLabel.rectTransform, new Vector2(30f, -208f), new Vector2(130f, 46f));

        decreaseButton = CreateButton("DecreaseButton", parent, "-", fontAsset, 26, secondaryColor, Color.white);
        SetTopLeft(decreaseButton.GetComponent<RectTransform>(), new Vector2(176f, -206f), new Vector2(48f, 46f));

        RectTransform lotCard = CreateImage("LotValue", parent, Color.white);
        SetTopLeft(lotCard, new Vector2(234f, -206f), new Vector2(170f, 46f));
        lotCountText = CreateText("LotCount", lotCard, string.Empty, fontAsset, 21, FontStyle.Bold, inkColor, TextAnchor.MiddleCenter);
        lotCountText.enableAutoSizing = true;
        lotCountText.fontSizeMin = 12;
        lotCountText.fontSizeMax = 21;
        SetStretch(lotCountText.rectTransform, 4f, 4f, 4f, 4f);

        increaseButton = CreateButton("IncreaseButton", parent, "+", fontAsset, 24, secondaryColor, Color.white);
        SetTopLeft(increaseButton.GetComponent<RectTransform>(), new Vector2(414f, -206f), new Vector2(48f, 46f));

        maxButton = CreateButton("MaxButton", parent, "最大", fontAsset, 17, secondaryColor, Color.white);
        SetTopLeft(maxButton.GetComponent<RectTransform>(), new Vector2(472f, -206f), new Vector2(86f, 46f));

        RectTransform summaryCard = CreateImage("SummaryCard", parent, new Color(1f, 0.94f, 0.78f, 1f));
        SetTopLeft(summaryCard, new Vector2(30f, -270f), new Vector2(700f, 66f));
        summaryText = CreateText("Summary", summaryCard, string.Empty, fontAsset, 18, FontStyle.Bold, primaryColor, TextAnchor.MiddleCenter);
        summaryText.enableWordWrapping = true;
        SetStretch(summaryText.rectTransform, 12f, 12f, 6f, 6f);

        quoteStatusText = CreateText("Status", parent, string.Empty, fontAsset, 14, FontStyle.Normal, inkColor, TextAnchor.MiddleLeft);
        quoteStatusText.enableWordWrapping = true;
        SetTopLeft(quoteStatusText.rectTransform, new Vector2(30f, -346f), new Vector2(700f, 54f));

        submitButton = CreateButton("SubmitButton", parent, "确认出售", fontAsset, 19, primaryColor, Color.white);
        SetTopLeft(submitButton.GetComponent<RectTransform>(), new Vector2(530f, -420f), new Vector2(200f, 46f));
        submitButtonText = submitButton.GetComponentInChildren<TMP_Text>(true);
    }

    private bool TryBindSceneLayout()
    {
        Transform panel = transform.Find("SaleDialogPanel");
        if (panel == null)
        {
            return false;
        }

        quoteContent = FindGameObject(panel, "QuoteContent");
        closeButton = FindButton(panel, "Header/CloseButton");

        itemNameText = FindText(panel, "QuoteContent/ItemName");
        ratioText = FindText(panel, "QuoteContent/RatioCard/Ratio");
        Transform ratioCard = panel.Find("QuoteContent/RatioCard");
        if (ratioCard != null)
        {
            sourceItemIconImage = FindComponent<Image>(ratioCard, "SourceItemIcon");
        }
        limitsText = FindText(panel, "QuoteContent/LimitsCard/Limits");
        lotCountText = FindText(panel, "QuoteContent/LotValue/LotCount");
        summaryText = FindText(panel, "QuoteContent/SummaryCard/Summary");
        quoteStatusText = FindText(panel, "QuoteContent/Status");
        submitButton = FindButton(panel, "QuoteContent/SubmitButton");
        submitButtonText = FindButtonText(submitButton);
        decreaseButton = FindButton(panel, "QuoteContent/DecreaseButton");
        increaseButton = FindButton(panel, "QuoteContent/IncreaseButton");
        maxButton = FindButton(panel, "QuoteContent/MaxButton");

        return quoteContent != null
            && closeButton != null
            && itemNameText != null
            && ratioText != null
            && limitsText != null
            && lotCountText != null
            && summaryText != null
            && sourceItemIconImage != null
            && submitButton != null
            && submitButtonText != null
            && decreaseButton != null
            && increaseButton != null
            && maxButton != null;
    }

    private void SetResourceIcon(string sourceIconKey, string sourceName)
    {
        SetIcon(sourceItemIconImage, ResolveResourceIcon(sourceIconKey, sourceName));
    }

    private static void SetIcon(Image image, Sprite sprite)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    private static Sprite ResolveResourceIcon(string iconKey, string fallbackName)
    {
        string value = string.IsNullOrWhiteSpace(iconKey) ? fallbackName : iconKey.Trim();
        string normalized = value.Replace("\\", "/");
        if (normalized.StartsWith("UI/", System.StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(3);
        }

        if (normalized.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(0, normalized.Length - 4);
        }

        Sprite sprite = LoadResourceIconAsset(normalized);
        if (sprite != null)
        {
            return sprite;
        }

        string lower = normalized.ToLowerInvariant();
        string fileName = lower.Contains("gold") || lower.Contains("coin") || value.Contains("金币")
            ? "金币"
            : lower.Contains("diamond") || lower.Contains("crystal") || value.Contains("钻石")
            ? "万能彩墨"
            : lower.Contains("wood") || value.Contains("木材")
            ? "木材"
            : lower.Contains("stone") || value.Contains("石材")
            ? "石材"
            : lower.Contains("iron") || lower.Contains("ore") || value.Contains("矿")
            ? "矿石"
            : lower.Contains("purification") || value.Contains("净化剂")
            ? "净化剂"
            : lower.Contains("stamina") || value.Contains("体力")
            ? "体力"
            : string.Empty;
        if (string.IsNullOrEmpty(fileName))
        {
            return null;
        }

        return LoadResourceIconAsset("UI/资源图标/" + fileName);
    }

    private static Sprite LoadResourceIconAsset(string resourcePath)
    {
        string normalizedPath = resourcePath.Replace("\\", "/");
        if (normalizedPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
        {
            normalizedPath = normalizedPath.Substring(0, normalizedPath.Length - 4);
        }

        string relativeAssetPath = normalizedPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase)
            ? normalizedPath
            : "Assets/_Imported/weijun-game-project/Assets/" + normalizedPath;
        if (normalizedPath.StartsWith("UI/资源图标/", System.StringComparison.OrdinalIgnoreCase))
        {
            relativeAssetPath = "Assets/_Project/Art/" + normalizedPath;
        }
        else if (normalizedPath.StartsWith("资源图标/", System.StringComparison.OrdinalIgnoreCase))
        {
            relativeAssetPath = "Assets/_Project/Art/UI/" + normalizedPath;
        }
#if UNITY_EDITOR
        Sprite editorSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(relativeAssetPath + ".png");
        if (editorSprite != null)
        {
            return editorSprite;
        }
#endif

        string absolutePath = Path.Combine(Application.dataPath, relativeAssetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar) + ".png");
        if (!File.Exists(absolutePath))
        {
            return null;
        }

        byte[] bytes = File.ReadAllBytes(absolutePath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(bytes))
        {
            Object.Destroy(texture);
            return null;
        }

        texture.name = Path.GetFileNameWithoutExtension(absolutePath);
        texture.filterMode = FilterMode.Point;
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }

    private static Image EnsureIcon(Transform parent, string name, Vector2 position)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            return existing.GetComponent<Image>();
        }

        RectTransform rect = CreateImage(name, parent, Color.white);
        SetTopLeft(rect, position, new Vector2(40f, 40f));
        return rect.GetComponent<Image>();
    }

    private static Image CreateIcon(string name, Transform parent, Vector2 position)
    {
        return EnsureIcon(parent, name, position);
    }

    private static GameObject FindGameObject(Transform root, string path)
    {
        Transform target = root.Find(path);
        return target == null ? null : target.gameObject;
    }

    private static Button FindButton(Transform root, string path)
    {
        Transform target = root.Find(path);
        return target == null ? null : target.GetComponent<Button>();
    }

    private static TMP_Text FindButtonText(Button button)
    {
        return button == null ? null : button.GetComponentInChildren<TMP_Text>(true);
    }

    private static TMP_Text FindText(Transform root, string path)
    {
        Transform target = root.Find(path);
        return target == null ? null : target.GetComponent<TMP_Text>();
    }

    private static T FindComponent<T>(Transform root, string path) where T : Component
    {
        Transform target = root.Find(path);
        return target == null ? null : target.GetComponent<T>();
    }

    private static GameObject CreateContainer(string name, Transform parent)
    {
        GameObject target = new GameObject(name, typeof(RectTransform));
        target.transform.SetParent(parent, false);
        return target;
    }

    private static RectTransform CreateImage(string name, Transform parent, Color color)
    {
        GameObject target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        target.transform.SetParent(parent, false);
        Image image = target.GetComponent<Image>();
        image.color = color;
        return target.GetComponent<RectTransform>();
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        string value,
        TMP_FontAsset fontAsset,
        int fontSize,
        FontStyle style,
        Color color,
        TextAnchor alignment)
    {
        GameObject target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        target.transform.SetParent(parent, false);
        TMP_Text text = target.GetComponent<TMP_Text>();
        text.font = fontAsset;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style == FontStyle.Bold ? FontStyles.Bold : style == FontStyle.Italic ? FontStyles.Italic : style == FontStyle.BoldAndItalic ? FontStyles.Bold | FontStyles.Italic : FontStyles.Normal;
        text.color = color;
        text.alignment = ConvertAlignment(alignment);
        text.raycastTarget = false;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Truncate;
        return text;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        string label,
        TMP_FontAsset fontAsset,
        int fontSize,
        Color background,
        Color foreground)
    {
        RectTransform rect = CreateImage(name, parent, background);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        TMP_Text text = CreateText("Label", rect, label, fontAsset, fontSize, FontStyle.Bold, foreground, TextAnchor.MiddleCenter);
        SetStretch(text.rectTransform, 4f, 4f, 2f, 2f);
        return button;
    }

    private static void Bind(Button button, UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        if (action != null)
        {
            button.onClick.AddListener(action);
        }
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
        {
            text.text = value ?? string.Empty;
        }
    }

    private static void SetButtonText(Button button, string value)
    {
        if (button == null)
        {
            return;
        }

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        SetText(text, value);
    }

    private static TextAlignmentOptions ConvertAlignment(TextAnchor alignment)
    {
        switch (alignment)
        {
            case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft: return TextAlignmentOptions.MidlineLeft;
            case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
            case TextAnchor.MiddleRight: return TextAlignmentOptions.MidlineRight;
            case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
            default: return TextAlignmentOptions.TopLeft;
        }
    }

    private static void SetCentered(RectTransform rect, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetStretch(RectTransform rect, float left, float right, float top, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }
}
