using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class IdleAdventureDialogLayout : MonoBehaviour
{
    private const string Art = "IdleAdventureUI/Afk/";
    private RectTransform paper;
    private RectTransform availableArea;
    private TextMeshProUGUI body;
    private ScrollRect scroll;
    private Vector2 lastAreaSize;
    private string lastBody;
    private RectTransform structuredContent;
    private float lastContentHeight;

    public static void Apply(TextMeshProUGUI title, TextMeshProUGUI message, Button primary, Button secondary, RectTransform content = null)
    {
        if (title == null || message == null || primary == null || secondary == null) return;
        RectTransform panel = title.transform.parent as RectTransform;
        if (panel == null) return;
        IdleAdventureDialogLayout layout = panel.GetComponent<IdleAdventureDialogLayout>();
        if (layout == null) layout = panel.gameObject.AddComponent<IdleAdventureDialogLayout>();
        layout.structuredContent = content;
        layout.Configure(panel, title, message, primary, secondary);
    }

    private void Configure(RectTransform panel, TextMeshProUGUI title, TextMeshProUGUI message, Button primary, Button secondary)
    {
        paper = panel;
        availableArea = paper.parent as RectTransform;
        body = message;
        paper.anchorMin = paper.anchorMax = paper.pivot = new Vector2(0.5f, 0.5f);
        paper.anchoredPosition = Vector2.zero;
        paper.sizeDelta = new Vector2(850f, 520f);
        Image background = paper.GetComponent<Image>();
        if (background != null)
        {
            Sprite sprite = Resources.Load<Sprite>(Art + "Panel");
            if (sprite != null)
            {
                background.sprite = sprite;
                background.type = Image.Type.Simple;
                background.color = Color.white;
            }
        }
        Place(title.rectTransform, 160f, 31f, 530f, 52f);
        StyleText(title, 32f, true);
        title.alignment = TextAlignmentOptions.Center;
        title.enableAutoSizing = true;
        title.fontSizeMin = 24f;
        title.fontSizeMax = 32f;
        title.overflowMode = TextOverflowModes.Ellipsis;
        ConfigureButton(primary, "ButtonPrimary", 165f);
        ConfigureButton(secondary, "ButtonSecondary", 449f);
        EnsureBodyScroll();
        RefreshGeometry();
    }

    private void EnsureBodyScroll()
    {
        Transform existing = paper.Find("Dialog Text Viewport");
        RectTransform viewport;
        if (existing == null)
        {
            viewport = new GameObject("Dialog Text Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect)).GetComponent<RectTransform>();
            viewport.SetParent(paper, false);
        }
        else viewport = (RectTransform)existing;
        Place(viewport, 115f, 116f, 620f, structuredContent != null ? 250f : 232f);
        Image hit = viewport.GetComponent<Image>();
        hit.color = Color.clear;
        hit.raycastTarget = true;
        scroll = viewport.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;
        body.transform.SetParent(structuredContent != null ? structuredContent : viewport, false);
        body.rectTransform.anchorMin = new Vector2(0f, 1f);
        body.rectTransform.anchorMax = Vector2.one;
        body.rectTransform.pivot = new Vector2(0.5f, 1f);
        body.rectTransform.sizeDelta = new Vector2(-14f, 232f);
        body.rectTransform.anchoredPosition = Vector2.zero;
        StyleText(body, 22f, false);
        body.enableAutoSizing = false;
        body.enableWordWrapping = true;
        body.overflowMode = TextOverflowModes.Overflow;
        body.alignment = TextAlignmentOptions.TopLeft;
        body.lineSpacing = 5f;
        body.margin = new Vector4(4f, 4f, 4f, 6f);
        if (structuredContent != null)
        {
            structuredContent.SetParent(viewport, false);
            structuredContent.anchorMin = new Vector2(0f, 1f);
            structuredContent.anchorMax = Vector2.one;
            structuredContent.pivot = new Vector2(0.5f, 1f);
            structuredContent.sizeDelta = new Vector2(-14f, structuredContent.sizeDelta.y);
            scroll.content = structuredContent;
        }
        else scroll.content = body.rectTransform;
        Transform barTransform = paper.Find("Dialog Scrollbar");
        if (barTransform == null)
        {
            RectTransform barRect = new GameObject("Dialog Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar)).GetComponent<RectTransform>();
            barRect.SetParent(paper, false);
            Place(barRect, 740f, 120f, 7f, 224f);
            barRect.GetComponent<Image>().color = new Color(0.38f, 0.27f, 0.16f, 0.12f);
            RectTransform handle = new GameObject("Handle", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            handle.SetParent(barRect, false);
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.one;
            handle.offsetMin = handle.offsetMax = Vector2.zero;
            handle.GetComponent<Image>().color = new Color(0.52f, 0.35f, 0.19f, 0.7f);
            Scrollbar scrollbar = barRect.GetComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handle.GetComponent<Image>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
        }
        else scroll.verticalScrollbar = barTransform.GetComponent<Scrollbar>();
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    private void LateUpdate()
    {
        if (paper != null && availableArea != null && (lastAreaSize != availableArea.rect.size || lastBody != body.text
            || (structuredContent != null && !Mathf.Approximately(lastContentHeight, structuredContent.rect.height)))) RefreshGeometry();
    }

    private void RefreshGeometry()
    {
        if (paper == null || availableArea == null || body == null) return;
        lastAreaSize = availableArea.rect.size;
        paper.localScale = Vector3.one * Mathf.Clamp(Mathf.Min((lastAreaSize.x - 48f) / 850f, (lastAreaSize.y - 48f) / 520f), 0.1f, 1f);
        Canvas.ForceUpdateCanvases();
        float height = structuredContent != null ? structuredContent.rect.height : body.GetPreferredValues(body.text, 606f, Mathf.Infinity).y + 12f;
        if (structuredContent == null)
            body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(232f, height));
        bool hasOverflow = height > scroll.viewport.rect.height + .5f;
        body.alignment = structuredContent != null || !hasOverflow ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;
        if (scroll.verticalScrollbar != null) scroll.verticalScrollbar.gameObject.SetActive(hasOverflow);
        if (lastBody != body.text)
        {
            scroll.StopMovement();
            scroll.content.anchoredPosition = Vector2.zero;
        }
        lastBody = body.text;
        lastContentHeight = height;
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.GetComponent<RectTransform>());
    }

    private static void ConfigureButton(Button button, string spriteName, float left)
    {
        Place(button.GetComponent<RectTransform>(), left, 393f, 236f, 66f);
        Image image = button.targetGraphic as Image;
        if (image != null)
        {
            Sprite sprite = Resources.Load<Sprite>(Art + spriteName);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.color = Color.white;
                image.type = sprite.border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 2f;
            }
        }
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.97f, 0.90f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.82f, 0.78f, 0.71f, 1f);
        colors.disabledColor = new Color(0.65f, 0.64f, 0.60f, 0.82f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        button.transition = Selectable.Transition.ColorTint;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null) return;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(18f, 8f);
        label.rectTransform.offsetMax = new Vector2(-18f, -8f);
        label.fontSize = label.fontSizeMax = 25f;
        label.fontSizeMin = 20f;
        label.enableAutoSizing = true;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        SmallButtonIconUtility.ApplyOutlinedLabel(label);
    }

    private static void StyleText(TMP_Text label, float size, bool bold)
    {
        label.color = new Color(0.28f, 0.18f, 0.12f, 1f);
        label.fontSize = size;
        label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        label.outlineWidth = 0f;
        label.raycastTarget = false;
    }

    private static void Place(RectTransform rect, float left, float top, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(width, height);
    }
}
