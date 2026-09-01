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
    private GameObject historyContent;
    private TMP_Text itemNameText;
    private TMP_Text ratioText;
    private TMP_Text limitsText;
    private TMP_Text lotCountText;
    private TMP_Text summaryText;
    private TMP_Text quoteStatusText;
    private TMP_Text historyText;
    private TMP_Text historyStatusText;
    private TMP_Text submitButtonText;
    private Button quoteTabButton;
    private Button historyTabButton;
    private Button decreaseButton;
    private Button increaseButton;
    private Button maxButton;
    private Button refreshQuoteButton;
    private Button submitButton;
    private Button refreshHistoryButton;
    private Button loadMoreButton;
    private Button closeButton;
    private ScrollRect historyScroll;
    private RectTransform historyContentRect;

    public void EnsureBuilt(TMP_FontAsset fontAsset)
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        fontAsset = fontAsset != null
            ? fontAsset
            : RuntimeFontAssetProvider.Resolve();

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

        quoteTabButton = CreateButton("QuoteTab", panel, "出售确认", fontAsset, 18, secondaryColor, Color.white);
        SetTopLeft(quoteTabButton.GetComponent<RectTransform>(), new Vector2(24f, -74f), new Vector2(160f, 42f));
        historyTabButton = CreateButton("HistoryTab", panel, "出售记录", fontAsset, 18, secondaryColor, Color.white);
        SetTopLeft(historyTabButton.GetComponent<RectTransform>(), new Vector2(194f, -74f), new Vector2(160f, 42f));

        quoteContent = CreateContainer("QuoteContent", panel);
        SetStretch(quoteContent.GetComponent<RectTransform>(), 0f, 0f, 126f, 0f);
        BuildQuoteContent(quoteContent.transform, fontAsset);

        historyContent = CreateContainer("HistoryContent", panel);
        SetStretch(historyContent.GetComponent<RectTransform>(), 0f, 0f, 126f, 0f);
        BuildHistoryContent(historyContent.transform, fontAsset);

        gameObject.SetActive(false);
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
        UnityAction closeAction)
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        quoteContent.SetActive(true);
        historyContent.SetActive(false);
        SetTabState(true);

        SetText(itemNameText, itemName);
        SetText(ratioText, ratio);
        SetText(limitsText, limits);
        SetText(lotCountText, lotCount);
        SetText(summaryText, summary);
        SetText(quoteStatusText, status);
        SetText(submitButtonText, busy ? "正在提交..." : submitLabel);

        decreaseButton.interactable = !busy && canDecrease;
        increaseButton.interactable = !busy && canIncrease;
        maxButton.interactable = !busy && canIncrease;
        refreshQuoteButton.interactable = !busy;
        submitButton.interactable = !busy && canSubmit;

        Bind(quoteTabButton, showQuoteAction);
        Bind(historyTabButton, showHistoryAction);
        Bind(decreaseButton, decreaseAction);
        Bind(increaseButton, increaseAction);
        Bind(maxButton, maxAction);
        Bind(refreshQuoteButton, refreshAction);
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
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        quoteContent.SetActive(false);
        historyContent.SetActive(true);
        SetTabState(false);

        SetText(historyText, body);
        SetText(historyStatusText, status);
        refreshHistoryButton.interactable = !busy;
        loadMoreButton.gameObject.SetActive(hasMore || busy);
        loadMoreButton.interactable = !busy && hasMore;
        SetButtonText(loadMoreButton, busy ? "正在读取..." : "加载更多");

        Bind(quoteTabButton, showQuoteAction);
        Bind(historyTabButton, showHistoryAction);
        Bind(refreshHistoryButton, refreshAction);
        Bind(loadMoreButton, loadMoreAction);
        Bind(closeButton, closeAction);

        Canvas.ForceUpdateCanvases();
        if (historyContentRect != null && historyText != null)
        {
            float contentHeight = Mathf.Max(334f, historyText.preferredHeight + 20f);
            historyContentRect.sizeDelta = new Vector2(0f, contentHeight);
        }

        if (historyScroll != null && !busy)
        {
            historyScroll.verticalNormalizedPosition = 1f;
        }
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
        SetStretch(ratioText.rectTransform, 16f, 16f, 8f, 8f);

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

        refreshQuoteButton = CreateButton("RefreshQuoteButton", parent, "刷新报价", fontAsset, 17, secondaryColor, Color.white);
        SetTopLeft(refreshQuoteButton.GetComponent<RectTransform>(), new Vector2(30f, -420f), new Vector2(150f, 46f));

        submitButton = CreateButton("SubmitButton", parent, "确认出售", fontAsset, 19, primaryColor, Color.white);
        SetTopLeft(submitButton.GetComponent<RectTransform>(), new Vector2(530f, -420f), new Vector2(200f, 46f));
        submitButtonText = submitButton.GetComponentInChildren<TMP_Text>(true);
    }

    private void BuildHistoryContent(Transform parent, TMP_FontAsset fontAsset)
    {
        RectTransform viewport = CreateImage("HistoryViewport", parent, new Color(1f, 0.94f, 0.78f, 1f));
        SetTopLeft(viewport, new Vector2(30f, -8f), new Vector2(700f, 334f));
        Mask mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        GameObject content = CreateContainer("HistoryList", viewport);
        historyContentRect = content.GetComponent<RectTransform>();
        historyContentRect.anchorMin = new Vector2(0f, 1f);
        historyContentRect.anchorMax = new Vector2(1f, 1f);
        historyContentRect.pivot = new Vector2(0.5f, 1f);
        historyContentRect.anchoredPosition = Vector2.zero;
        historyContentRect.sizeDelta = new Vector2(0f, 334f);

        historyText = CreateText("HistoryText", content.transform, string.Empty, fontAsset, 16, FontStyle.Normal, inkColor, TextAnchor.UpperLeft);
        historyText.enableWordWrapping = true;
        historyText.overflowMode = TextOverflowModes.Overflow;
        RectTransform historyRect = historyText.rectTransform;
        historyRect.anchorMin = new Vector2(0f, 1f);
        historyRect.anchorMax = new Vector2(1f, 1f);
        historyRect.pivot = new Vector2(0.5f, 1f);
        historyRect.anchoredPosition = new Vector2(0f, -10f);
        historyRect.sizeDelta = new Vector2(-28f, 0f);
        ContentSizeFitter fitter = historyText.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        historyScroll = viewport.gameObject.AddComponent<ScrollRect>();
        historyScroll.viewport = viewport;
        historyScroll.content = historyContentRect;
        historyScroll.horizontal = false;
        historyScroll.vertical = true;
        historyScroll.movementType = ScrollRect.MovementType.Clamped;
        historyScroll.scrollSensitivity = 24f;

        historyStatusText = CreateText("HistoryStatus", parent, string.Empty, fontAsset, 14, FontStyle.Normal, inkColor, TextAnchor.MiddleLeft);
        historyStatusText.enableWordWrapping = true;
        SetTopLeft(historyStatusText.rectTransform, new Vector2(30f, -354f), new Vector2(700f, 50f));

        refreshHistoryButton = CreateButton("RefreshHistoryButton", parent, "刷新记录", fontAsset, 17, secondaryColor, Color.white);
        SetTopLeft(refreshHistoryButton.GetComponent<RectTransform>(), new Vector2(30f, -420f), new Vector2(150f, 46f));

        loadMoreButton = CreateButton("LoadMoreButton", parent, "加载更多", fontAsset, 17, primaryColor, Color.white);
        SetTopLeft(loadMoreButton.GetComponent<RectTransform>(), new Vector2(580f, -420f), new Vector2(150f, 46f));
    }

    private void SetTabState(bool quoteSelected)
    {
        SetButtonColor(quoteTabButton, quoteSelected ? primaryColor : secondaryColor);
        SetButtonColor(historyTabButton, quoteSelected ? secondaryColor : primaryColor);
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

    private static void SetButtonColor(Button button, Color color)
    {
        if (button != null && button.targetGraphic is Image image)
        {
            image.color = color;
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
