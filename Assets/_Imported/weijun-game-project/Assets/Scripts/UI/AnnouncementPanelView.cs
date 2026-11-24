using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public sealed class AnnouncementPanelView : MonoBehaviour
{
    [Header("Editable Artwork")]
    [SerializeField] private Image dimmerImage;
    [SerializeField] private Image paperImage;
    [SerializeField] private Image titleBannerImage;
    [SerializeField] private Image confirmButtonImage;

    [Header("Announcement Text")]
    [SerializeField] private RectTransform announcementContentRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text versionText;
    [SerializeField] private TMP_Text publishDateText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text scrollHintText;

    [Header("Scrolling")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform contentRoot;

    [Header("Actions")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button confirmButton;

    [Header("Announcement Types")]
    [SerializeField] private RectTransform announcementTypeRoot;
    [SerializeField] private Button announcementTypeTemplate;
    [SerializeField] private RectTransform announcementTypeContent;
    [SerializeField] private ScrollRect announcementTypeScrollRect;

    private bool missingReferenceWarningLogged;
    private bool announcementTypeListInitialized;
    private Vector2 announcementTypeTemplatePosition;
    private Vector2 announcementTypeTemplateSize;
    private Vector2 announcementTypeTemplatePivot;
    private float announcementTypeStep;
    private string selectedCategoryKey;
    private readonly List<Button> announcementTypeButtons = new List<Button>();
    private readonly List<AnnouncementTypeGroup> announcementTypeGroups = new List<AnnouncementTypeGroup>();

    [Serializable]
    public sealed class AnnouncementItem
    {
        public string categoryKey;
        public string categoryName;
        public string title;
        public string version;
        public string publishDate;
        public string body;
    }

    private sealed class AnnouncementTypeGroup
    {
        public string key;
        public string name;
        public readonly List<AnnouncementItem> items = new List<AnnouncementItem>();
    }

    private void Awake()
    {
        ResolveMissingReferences();
    }

    public void SetAnnouncement(string title, string version, string publishDate, string body)
    {
        SetAnnouncements(new[]
        {
            new AnnouncementItem
            {
                categoryKey = "SYSTEM",
                categoryName = "系统公告",
                title = title,
                version = version,
                publishDate = publishDate,
                body = body
            }
        });
    }

    public void SetAnnouncements(AnnouncementItem[] items)
    {
        ResolveMissingReferences();
        BuildAnnouncementTypeGroups(items);
        RebuildAnnouncementTypeButtons();

        if (announcementTypeGroups.Count == 0)
        {
            selectedCategoryKey = null;
            SetText(titleText, "游戏公告");
            SetText(versionText, string.Empty);
            SetText(publishDateText, string.Empty);
            SetText(bodyText, "暂无公告内容");
            RefreshBodyScroll();
            return;
        }

        int selectedIndex = announcementTypeGroups.FindIndex(group =>
            string.Equals(group.key, selectedCategoryKey, StringComparison.OrdinalIgnoreCase));
        SelectAnnouncementType(Mathf.Max(0, selectedIndex));
    }

    public void SetStatus(string status, string message)
    {
        SetAnnouncements(null);
        SetText(versionText, FrontendTextLocalization.DisplayStatus(status));
        SetText(bodyText, FrontendTextLocalization.NormalizePlayerMessage(message, "公告暂时无法查看，请稍后重试。"));
        RefreshBodyScroll();
    }

    public bool HasAnnouncementTypeList
    {
        get
        {
            ResolveMissingReferences();
            return announcementTypeRoot != null && announcementTypeTemplate != null;
        }
    }

    public void BindClose(UnityAction closeAction)
    {
        ResolveMissingReferences();
        Bind(closeButton, closeAction);
        Bind(confirmButton, closeAction);
    }

    private void ResolveMissingReferences()
    {
        announcementContentRoot = announcementContentRoot != null
            ? announcementContentRoot : FindNamedComponent<RectTransform>("AnnouncementContent");
        Transform textRoot = announcementContentRoot != null ? announcementContentRoot : transform;
        titleText = titleText != null ? titleText : FindNamedComponent<TMP_Text>("Title");
        versionText = versionText != null ? versionText : FindNamedComponent<TMP_Text>(textRoot, "Version");
        publishDateText = publishDateText != null ? publishDateText : FindNamedComponent<TMP_Text>(textRoot, "PublishDate");
        bodyText = bodyText != null ? bodyText : FindNamedComponent<TMP_Text>(textRoot, "Body");
        scrollHintText = scrollHintText != null ? scrollHintText : FindNamedComponent<TMP_Text>(textRoot, "ScrollHint");
        scrollRect = scrollRect != null ? scrollRect : FindNamedComponent<ScrollRect>(textRoot, "AnnouncementScrollView");
        contentRoot = contentRoot != null ? contentRoot : scrollRect != null ? scrollRect.content : null;
        closeButton = closeButton != null ? closeButton : FindNamedComponent<Button>("CloseButton");
        confirmButton = confirmButton != null ? confirmButton : FindNamedComponent<Button>("ConfirmButton");
        announcementTypeRoot = announcementTypeRoot != null
            ? announcementTypeRoot
            : FindNamedComponent<RectTransform>("AnnouncementType");
        announcementTypeTemplate = announcementTypeTemplate != null
            ? announcementTypeTemplate
            : FindNamedComponent<Button>(announcementTypeRoot, "SystemNotice");

        if (!missingReferenceWarningLogged && bodyText == null)
        {
            missingReferenceWarningLogged = true;
            Debug.LogWarning("AnnouncementPanelView 缺少公告正文，请检查 AnnouncementContent 下的 Body TMP 节点。", this);
        }
        ConfigureSingleLine(titleText, 28f, 34f);
        if (titleText != null)
        {
            titleText.enableWordWrapping = false;
            titleText.maxVisibleLines = 1;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.margin = new Vector4(16f, 4f, 16f, 4f);
        }
        ConfigureSingleLine(versionText, 14f, 18f);
        ConfigureSingleLine(publishDateText, 14f, 18f);
        ConfigureBodyBounds();
        if (bodyText != null)
        {
            bodyText.enableAutoSizing = false;
            bodyText.enableWordWrapping = true;
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.lineSpacing = 4f;
            bodyText.paragraphSpacing = 8f;
            bodyText.raycastTarget = false;
        }
        ApplyPaperPresentation();
    }

    private void ApplyPaperPresentation()
    {
        Transform paper = transform.Find("AnnouncementPaper");
        if (paper == null || announcementContentRoot != null) return;
        Sprite paperSprite = Resources.Load<Sprite>("UI/ApprovedUi/Panels/AnnouncementPaper");
        if (paperSprite == null) return;
        paperImage = paper.GetComponent<Image>();
        if (paperImage != null)
        {
            paperImage.sprite = paperSprite;
            paperImage.overrideSprite = null;
            paperImage.type = Image.Type.Simple;
            paperImage.preserveAspect = false;
            paperImage.color = Color.white;
        }
        Image shadow = FindNamedComponent<Image>("PaperShadow");
        if (shadow != null)
        {
            shadow.sprite = paperSprite;
            shadow.type = Image.Type.Simple;
            shadow.color = new Color(0f, 0f, 0f, 0.33f);
        }
        titleBannerImage = paper.Find("TitleBanner")?.GetComponent<Image>();
        if (titleBannerImage != null) titleBannerImage.color = Color.clear;
        Image divider = paper.Find("HeaderDivider")?.GetComponent<Image>();
        if (divider != null) divider.color = Color.clear;
        Color ink = new Color(0.286f, 0.220f, 0.161f, 1f);
        Color muted = new Color(0.42f, 0.345f, 0.267f, 1f);
        if (titleText != null) titleText.color = ink;
        if (versionText != null) versionText.color = muted;
        if (publishDateText != null) publishDateText.color = muted;
        if (bodyText != null)
        {
            bodyText.fontSize = 22f;
            bodyText.color = ink;
            bodyText.margin = new Vector4(16f, 0f, 16f, 0f);
        }
        if (scrollHintText != null)
        {
            scrollHintText.fontSize = 17f;
            scrollHintText.color = muted;
        }
        if (scrollRect != null && scrollRect.viewport != null)
        {
            Image viewportImage = scrollRect.viewport.GetComponent<Image>();
            Mask viewportMask = scrollRect.viewport.GetComponent<Mask>();
            if (viewportMask != null)
            {
                viewportMask.showMaskGraphic = false;
                if (viewportImage != null) viewportImage.color = Color.white;
            }
            else if (viewportImage != null) viewportImage.color = Color.clear;
        }
        ApplyPaperButton(confirmButton, "UI/ApprovedUi/Panels/PrimaryButton", false);
        ApplyPaperButton(closeButton, "UI/ApprovedUi/Panels/CloseButton", true);
        Image confirmShadow = paper.Find("ConfirmShadow")?.GetComponent<Image>();
        if (confirmShadow != null) confirmShadow.color = Color.clear;
    }

    private static void ApplyPaperButton(Button button, string resourcePath, bool iconOnly)
    {
        if (button == null) return;
        Image image = button.GetComponent<Image>();
        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (image == null || sprite == null) return;
        image.sprite = sprite;
        image.overrideSprite = null;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.96f, 0.88f, 1f);
        colors.pressedColor = new Color(0.82f, 0.72f, 0.60f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.66f, 0.64f, 0.59f, 1f);
        button.colors = colors;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null) return;
        label.color = new Color(1f, 0.96f, 0.86f, 1f);
        if (iconOnly) label.text = string.Empty;
    }

    private static void ConfigureSingleLine(TMP_Text text, float minimum, float maximum)
    {
        if (text == null) return;
        text.enableAutoSizing = true;
        text.fontSizeMin = minimum;
        text.fontSizeMax = maximum;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
    }

    private void ConfigureBodyBounds()
    {
        if (scrollRect == null || scrollRect.viewport == null) return;
        RectTransform scrollArea = scrollRect.transform as RectTransform;
        RectTransform parent = scrollArea.parent as RectTransform;
        if (parent == null) return;
        Vector3[] corners = new Vector3[4];
        scrollArea.GetWorldCorners(corners);
        float top = parent.InverseTransformPoint(corners[1]).y;
        float bottom = parent.InverseTransformPoint(corners[0]).y;
        foreach (TMP_Text heading in new[] { versionText, publishDateText })
        {
            if (heading == null) continue;
            heading.rectTransform.GetWorldCorners(corners);
            top = Mathf.Min(top, parent.InverseTransformPoint(corners[0]).y - 12f);
        }
        if (scrollHintText != null)
        {
            scrollHintText.rectTransform.GetWorldCorners(corners);
            bottom = Mathf.Max(bottom, parent.InverseTransformPoint(corners[1]).y + 12f);
        }
        if (top <= bottom + 80f) return;
        float anchorY = Mathf.Lerp(parent.rect.yMin, parent.rect.yMax, scrollArea.anchorMax.y);
        scrollArea.anchorMin = new Vector2(scrollArea.anchorMin.x, scrollArea.anchorMax.y);
        scrollArea.pivot = new Vector2(scrollArea.pivot.x, 1f);
        scrollArea.anchoredPosition = new Vector2(scrollArea.anchoredPosition.x, top - anchorY);
        scrollArea.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, top - bottom);
    }

    private void BuildAnnouncementTypeGroups(AnnouncementItem[] items)
    {
        announcementTypeGroups.Clear();
        if (items == null)
        {
            return;
        }

        for (int index = 0; index < items.Length; index++)
        {
            AnnouncementItem item = items[index];
            if (item == null)
            {
                continue;
            }

            string key = string.IsNullOrWhiteSpace(item.categoryKey) ? "SYSTEM" : item.categoryKey.Trim();
            string name = string.IsNullOrWhiteSpace(item.categoryName) ? "游戏公告" : item.categoryName.Trim();
            AnnouncementTypeGroup group = null;
            for (int groupIndex = 0; groupIndex < announcementTypeGroups.Count; groupIndex++)
            {
                if (string.Equals(announcementTypeGroups[groupIndex].key, key, StringComparison.OrdinalIgnoreCase))
                {
                    group = announcementTypeGroups[groupIndex];
                    break;
                }
            }

            if (group == null)
            {
                group = new AnnouncementTypeGroup
                {
                    key = key,
                    name = name
                };
                announcementTypeGroups.Add(group);
            }

            group.items.Add(item);
        }
    }

    private void InitializeAnnouncementTypeList()
    {
        if (announcementTypeListInitialized)
        {
            return;
        }

        if (announcementTypeRoot == null || announcementTypeTemplate == null)
        {
            Debug.LogWarning("AnnouncementPanelView 缺少 AnnouncementType 或 SystemNotice 节点，无法生成公告类型按钮。", this);
            return;
        }

        RectTransform templateRect = announcementTypeTemplate.GetComponent<RectTransform>();
        if (templateRect == null)
        {
            Debug.LogWarning("AnnouncementPanelView 的 SystemNotice 缺少 RectTransform。", this);
            return;
        }

        Vector3 templatePosition = announcementTypeRoot.InverseTransformPoint(templateRect.position);
        announcementTypeTemplatePosition = new Vector2(templatePosition.x - announcementTypeRoot.rect.center.x,
            templatePosition.y - announcementTypeRoot.rect.yMax);
        announcementTypeTemplateSize = templateRect.rect.size;
        announcementTypeTemplatePivot = templateRect.pivot;
        announcementTypeStep = ResolveAnnouncementTypeStep(templateRect);

        Transform existingContent = announcementTypeRoot.Find("AnnouncementTypeContent");
        if (existingContent != null)
        {
            announcementTypeContent = existingContent as RectTransform;
        }

        if (announcementTypeContent == null)
        {
            GameObject contentObject = new GameObject("AnnouncementTypeContent", typeof(RectTransform));
            announcementTypeContent = contentObject.GetComponent<RectTransform>();
            announcementTypeContent.SetParent(announcementTypeRoot, false);
        }

        announcementTypeTemplate.transform.SetParent(announcementTypeContent, false);

        if (announcementTypeRoot.GetComponent<RectMask2D>() == null)
        {
            announcementTypeRoot.gameObject.AddComponent<RectMask2D>();
        }

        announcementTypeScrollRect = announcementTypeRoot.GetComponent<ScrollRect>();
        if (announcementTypeScrollRect == null)
        {
            announcementTypeScrollRect = announcementTypeRoot.gameObject.AddComponent<ScrollRect>();
        }

        announcementTypeScrollRect.viewport = announcementTypeRoot;
        announcementTypeScrollRect.content = announcementTypeContent;
        announcementTypeScrollRect.horizontal = false;
        announcementTypeScrollRect.vertical = true;
        announcementTypeScrollRect.horizontalScrollbar = null;
        announcementTypeScrollRect.verticalScrollbar = null;
        announcementTypeScrollRect.movementType = ScrollRect.MovementType.Clamped;
        announcementTypeScrollRect.inertia = true;
        announcementTypeScrollRect.scrollSensitivity = 30f;

        announcementTypeButtons.Add(announcementTypeTemplate);
        announcementTypeListInitialized = true;
    }

    private float ResolveAnnouncementTypeStep(RectTransform templateRect)
    {
        Transform secondTemplate = announcementTypeRoot.Find("GameNotice");
        RectTransform secondRect = secondTemplate == null ? null : secondTemplate as RectTransform;
        if (secondRect != null)
        {
            float sceneStep = Mathf.Abs(templateRect.anchoredPosition.y - secondRect.anchoredPosition.y);
            if (sceneStep > 1f)
            {
                return sceneStep;
            }
        }

        return Mathf.Max(1f, templateRect.rect.height * Mathf.Abs(templateRect.localScale.y) + 10f);
    }

    private void RebuildAnnouncementTypeButtons()
    {
        InitializeAnnouncementTypeList();
        if (announcementTypeContent == null || announcementTypeTemplate == null)
        {
            return;
        }

        while (announcementTypeButtons.Count < announcementTypeGroups.Count)
        {
            Button clone = Instantiate(announcementTypeTemplate, announcementTypeContent, false);
            clone.gameObject.name = "AnnouncementTypeButton_" + announcementTypeButtons.Count;
            announcementTypeButtons.Add(clone);
        }

        for (int index = 0; index < announcementTypeButtons.Count; index++)
        {
            Button button = announcementTypeButtons[index];
            bool visible = index < announcementTypeGroups.Count;
            button.gameObject.SetActive(visible);
            button.onClick = new Button.ButtonClickedEvent();
            if (!visible) continue;
            int buttonIndex = index;
            button.onClick.AddListener(delegate { SelectAnnouncementType(buttonIndex); });
            SetAnnouncementTypeButtonText(button, announcementTypeGroups[index].name);
            PositionAnnouncementTypeButton(button.GetComponent<RectTransform>(), index);
        }

        SetAnnouncementTypeContentHeight(announcementTypeGroups.Count);
        if (announcementTypeScrollRect != null)
        {
            announcementTypeScrollRect.StopMovement();
            announcementTypeScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private void PositionAnnouncementTypeButton(RectTransform buttonRect, int index)
    {
        if (buttonRect == null || announcementTypeRoot == null)
        {
            return;
        }

        buttonRect.anchorMin = new Vector2(0.5f, 1f);
        buttonRect.anchorMax = new Vector2(0.5f, 1f);
        buttonRect.pivot = announcementTypeTemplatePivot;
        buttonRect.anchoredPosition = new Vector2(
            announcementTypeTemplatePosition.x,
            announcementTypeTemplatePosition.y - announcementTypeStep * index);
        buttonRect.sizeDelta = announcementTypeTemplateSize;
    }

    private void SetAnnouncementTypeContentHeight(int itemCount)
    {
        if (announcementTypeContent == null || announcementTypeRoot == null)
        {
            return;
        }

        float rootHeight = announcementTypeRoot.rect.height;
        if (rootHeight <= 0f)
        {
            rootHeight = announcementTypeRoot.sizeDelta.y;
        }

        float firstCenterDistance = -announcementTypeTemplatePosition.y;
        float lastCenterDistance = firstCenterDistance + Mathf.Max(0, itemCount - 1) * announcementTypeStep;
        float bottomDistance = lastCenterDistance
            + announcementTypeTemplateSize.y * announcementTypeTemplatePivot.y
                * Mathf.Abs(announcementTypeTemplate.transform.localScale.y);
        float contentHeight = itemCount == 0 ? rootHeight : Mathf.Max(rootHeight, bottomDistance);

        announcementTypeContent.anchorMin = new Vector2(0f, 1f);
        announcementTypeContent.anchorMax = new Vector2(1f, 1f);
        announcementTypeContent.pivot = new Vector2(0.5f, 1f);
        announcementTypeContent.anchoredPosition = Vector2.zero;
        announcementTypeContent.sizeDelta = new Vector2(0f, contentHeight);
        announcementTypeScrollRect.vertical = contentHeight > rootHeight + 0.5f;
    }

    private void SetAnnouncementTypeButtonText(Button button, string value)
    {
        if (button == null)
        {
            return;
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
        {
            Debug.LogWarning("公告类型按钮缺少 TMP 文本节点。", button);
            return;
        }

        label.text = value ?? string.Empty;
        ConfigureSingleLine(label, 16f, Mathf.Max(16f, label.fontSizeMax));
    }

    private void SelectAnnouncementType(int index)
    {
        if (index < 0 || index >= announcementTypeGroups.Count)
        {
            return;
        }

        for (int buttonIndex = 0; buttonIndex < announcementTypeButtons.Count; buttonIndex++)
        {
            Button button = announcementTypeButtons[buttonIndex];
            bool selected = buttonIndex == index;
            button.transition = Selectable.Transition.None;
            if (button.targetGraphic is Image image)
                image.overrideSprite = selected ? button.spriteState.selectedSprite : null;
            if (button.targetGraphic != null)
                button.targetGraphic.CrossFadeColor(selected ? button.colors.selectedColor : button.colors.normalColor, 0f, true, true);
        }

        AnnouncementTypeGroup group = announcementTypeGroups[index];
        selectedCategoryKey = group.key;
        AnnouncementItem firstItem = group.items.Count > 0 ? group.items[0] : null;
        SetText(titleText, "游戏公告");
        SetText(versionText, firstItem == null ? string.Empty : firstItem.version);
        SetText(publishDateText, firstItem == null ? string.Empty : firstItem.publishDate);
        SetText(bodyText, BuildAnnouncementTypeBody(group));
        RefreshBodyScroll();
    }

    private void RefreshBodyScroll()
    {
        if (!isActiveAndEnabled || scrollRect == null || contentRoot == null || scrollRect.viewport == null) return;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        scrollRect.StopMovement();
        scrollRect.vertical = contentRoot.rect.height > scrollRect.viewport.rect.height + 0.5f;
        if (scrollHintText != null) scrollHintText.gameObject.SetActive(scrollRect.vertical);
        if (scrollRect.verticalScrollbar != null)
        {
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            scrollRect.verticalScrollbar.gameObject.SetActive(scrollRect.vertical);
        }
        scrollRect.verticalNormalizedPosition = 1f;
        contentRoot.anchoredPosition = new Vector2(contentRoot.anchoredPosition.x, 0f);
    }

    private string BuildAnnouncementTypeBody(AnnouncementTypeGroup group)
    {
        if (group == null || group.items.Count == 0)
        {
            return "暂无公告内容";
        }

        StringBuilder builder = new StringBuilder();
        for (int index = 0; index < group.items.Count; index++)
        {
            AnnouncementItem item = group.items[index];
            if (item == null)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append("\n\n------------------------------\n\n");
            }

            if (!string.IsNullOrEmpty(item.title))
            {
                builder.Append(item.title);
            }

            if (group.items.Count > 1 && !string.IsNullOrEmpty(item.publishDate))
            {
                builder.Append("\n").Append(item.publishDate);
            }

            if (!string.IsNullOrEmpty(item.body))
            {
                builder.Append("\n\n").Append(item.body);
            }
        }

        return builder.Length == 0 ? "暂无公告内容" : builder.ToString();
    }

    private T FindNamedComponent<T>(string objectName) where T : Component
    {
        return FindNamedComponent<T>(transform, objectName);
    }

    private static T FindNamedComponent<T>(Transform root, string objectName) where T : Component
    {
        if (root == null) return null;
        T[] components = root.GetComponentsInChildren<T>(true);
        for (int index = 0; index < components.Length; index++)
        {
            T component = components[index];
            if (component != null && component.gameObject.name == objectName)
            {
                return component;
            }
        }

        return null;
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

    private static void SetText(TMP_Text label, string value)
    {
        if (label != null)
        {
            label.text = value ?? string.Empty;
        }
    }

#if UNITY_EDITOR
    public void EditorSetReferences(
        Image dimmer,
        Image paper,
        Image titleBanner,
        Image confirmBackground,
        TMP_Text title,
        TMP_Text version,
        TMP_Text publishDate,
        TMP_Text body,
        TMP_Text scrollHint,
        ScrollRect announcementScrollRect,
        RectTransform announcementContent,
        Button close,
        Button confirm)
    {
        dimmerImage = dimmer;
        paperImage = paper;
        titleBannerImage = titleBanner;
        confirmButtonImage = confirmBackground;
        titleText = title;
        versionText = version;
        publishDateText = publishDate;
        bodyText = body;
        scrollHintText = scrollHint;
        scrollRect = announcementScrollRect;
        contentRoot = announcementContent;
        closeButton = close;
        confirmButton = confirm;
    }
#endif
}
