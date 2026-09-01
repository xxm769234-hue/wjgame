using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Text = TMPro.TMP_Text;
using Font = TMPro.TMP_FontAsset;

public static class AnnouncementPrefabBuilder
{
    private const string PrefabPath = "Assets/Resources/UI/AnnouncementPanel.prefab";
    private const string PaperSpritePath = "Assets/Sprites/Humble Gift - Paper UI System/Humble Gift - Paper UI System v1.1/Sprites/Paper UI Pack/Plain/8 Shop/1.png";
    private const string HeaderSpritePath = "Assets/Sprites/Humble Gift - Paper UI System/Humble Gift - Paper UI System v1.1/Sprites/Paper UI Pack/Plain/2 Headers/4.png";
    private const string ButtonSpritePath = "Assets/Sprites/Humble Gift - Paper UI System/Humble Gift - Paper UI System v1.1/Sprites/Paper UI Pack/Plain/4 Notification/2.png";
    private const string FontPath = "Assets/_Imported/weijun-game-project/Assets/Sprites/font/SourceHanSansSC-Normal SDF.asset";

    private static Font defaultFont;

    [InitializeOnLoadMethod]
    private static void BuildMissingPrefabAfterReload()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(PrefabPath))
            {
                BuildPrefab();
            }
        };
    }

    [MenuItem("Tools/WeiJun UI/Rebuild Announcement Prefab")]
    public static void BuildPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        defaultFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath)
            ?? RuntimeFontAssetProvider.Resolve();
        Sprite paperSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PaperSpritePath);
        Sprite headerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(HeaderSpritePath);
        Sprite buttonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonSpritePath);

        GameObject root = new GameObject("AnnouncementPanelView", typeof(RectTransform));
        try
        {
            Stretch(root.GetComponent<RectTransform>());
            AnnouncementPanelView view = root.AddComponent<AnnouncementPanelView>();

            Image dimmer = CreateImage("Dimmer", root.transform, new Color(0.05f, 0.04f, 0.03f, 0.72f));
            Stretch(dimmer.rectTransform);
            dimmer.raycastTarget = true;

            Image shadow = CreateImage("PaperShadow", root.transform, new Color(0.04f, 0.025f, 0.015f, 0.50f), paperSprite);
            SetCentered(shadow.rectTransform, new Vector2(12f, -18f), new Vector2(810f, 900f));
            shadow.raycastTarget = false;

            Image paper = CreateImage("AnnouncementPaper", root.transform, Color.white, paperSprite);
            SetCentered(paper.rectTransform, new Vector2(0f, -4f), new Vector2(810f, 900f));
            paper.raycastTarget = true;

            Image titleBanner = CreateImage("TitleBanner", paper.transform, new Color(0.43f, 0.25f, 0.14f, 1f), headerSprite);
            SetTopLeft(titleBanner.rectTransform, new Vector2(170f, -26f), new Vector2(470f, 100f));
            titleBanner.raycastTarget = false;

            Text titleText = CreateText(
                "Title",
                titleBanner.transform,
                "游戏公告",
                34,
                FontStyle.Bold,
                new Color(1f, 0.86f, 0.50f, 1f),
                TextAnchor.MiddleCenter);
            Stretch(titleText.rectTransform);

            Button closeButton = CreateButton(
                paper.transform,
                "CloseButton",
                "X",
                new Color(0.42f, 0.23f, 0.12f, 0.96f),
                new Color(1f, 0.86f, 0.52f, 1f),
                20,
                null,
                out _);
            SetTopLeft(closeButton.GetComponent<RectTransform>(), new Vector2(738f, -48f), new Vector2(42f, 42f));

            Text versionText = CreateText(
                "Version",
                paper.transform,
                "版本 1.0",
                18,
                FontStyle.Bold,
                new Color(0.36f, 0.20f, 0.10f, 1f),
                TextAnchor.MiddleLeft);
            SetTopLeft(versionText.rectTransform, new Vector2(92f, -120f), new Vector2(280f, 34f));

            Text publishDateText = CreateText(
                "PublishDate",
                paper.transform,
                "2026年7月21日",
                18,
                FontStyle.Normal,
                new Color(0.46f, 0.30f, 0.18f, 0.95f),
                TextAnchor.MiddleRight);
            SetTopLeft(publishDateText.rectTransform, new Vector2(438f, -120f), new Vector2(278f, 34f));

            Image divider = CreateImage("HeaderDivider", paper.transform, new Color(0.50f, 0.31f, 0.16f, 0.48f));
            SetTopLeft(divider.rectTransform, new Vector2(90f, -158f), new Vector2(628f, 2f));
            divider.raycastTarget = false;

            ScrollReferences scroll = BuildScrollView(paper.transform);

            Text scrollHintText = CreateText(
                "ScrollHint",
                paper.transform,
                "滚动查看完整公告",
                15,
                FontStyle.Normal,
                new Color(0.48f, 0.33f, 0.20f, 0.78f),
                TextAnchor.MiddleCenter);
            SetTopLeft(scrollHintText.rectTransform, new Vector2(260f, -756f), new Vector2(290f, 28f));

            Image confirmShadow = CreateImage("ConfirmShadow", paper.transform, new Color(0.20f, 0.10f, 0.04f, 0.35f), buttonSprite);
            SetTopLeft(confirmShadow.rectTransform, new Vector2(289f, -808f), new Vector2(232f, 68f));
            confirmShadow.raycastTarget = false;

            Button confirmButton = CreateButton(
                paper.transform,
                "ConfirmButton",
                "确定",
                new Color(0.98f, 0.72f, 0.22f, 1f),
                new Color(0.35f, 0.18f, 0.06f, 1f),
                24,
                buttonSprite,
                out Image confirmButtonImage);
            SetTopLeft(confirmButton.GetComponent<RectTransform>(), new Vector2(289f, -800f), new Vector2(232f, 68f));

            view.EditorSetReferences(
                dimmer,
                paper,
                titleBanner,
                confirmButtonImage,
                titleText,
                versionText,
                publishDateText,
                scroll.BodyText,
                scrollHintText,
                scroll.ScrollRect,
                scroll.ContentRoot,
                closeButton,
                confirmButton);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
            if (!saved)
            {
                throw new IOException("Failed to save announcement prefab at " + PrefabPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Announcement prefab rebuilt: " + PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static ScrollReferences BuildScrollView(Transform parent)
    {
        GameObject scrollObject = new GameObject("AnnouncementScrollView", typeof(RectTransform), typeof(ScrollRect));
        scrollObject.transform.SetParent(parent, false);
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        SetTopLeft(scrollRectTransform, new Vector2(82f, -176f), new Vector2(646f, 570f));

        Image viewportImage = CreateImage("Viewport", scrollObject.transform, new Color(1f, 0.96f, 0.82f, 0.03f));
        RectTransform viewportRect = viewportImage.rectTransform;
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = new Vector2(-24f, 0f);
        Mask viewportMask = viewportImage.gameObject.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        GameObject contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.transform.SetParent(viewportImage.transform, false);
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup contentLayout = contentObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(8, 14, 8, 28);
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter contentFitter = contentObject.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Text bodyText = CreateText(
            "Body",
            contentObject.transform,
            "公告正文",
            20,
            FontStyle.Normal,
            new Color(0.30f, 0.18f, 0.10f, 1f),
            TextAnchor.UpperLeft);
        bodyText.enableAutoSizing = false;
        bodyText.enableWordWrapping = true;
        bodyText.overflowMode = TextOverflowModes.Overflow;
        bodyText.lineSpacing = 1.18f;

        Image scrollbarTrack = CreateImage("Scrollbar", scrollObject.transform, new Color(0.45f, 0.28f, 0.15f, 0.20f));
        RectTransform scrollbarRect = scrollbarTrack.rectTransform;
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.anchoredPosition = Vector2.zero;
        scrollbarRect.sizeDelta = new Vector2(14f, 0f);

        Scrollbar scrollbar = scrollbarTrack.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.numberOfSteps = 0;

        GameObject slidingArea = new GameObject("SlidingArea", typeof(RectTransform));
        slidingArea.transform.SetParent(scrollbarTrack.transform, false);
        RectTransform slidingRect = slidingArea.GetComponent<RectTransform>();
        Stretch(slidingRect);
        slidingRect.offsetMin = new Vector2(2f, 4f);
        slidingRect.offsetMax = new Vector2(-2f, -4f);

        Image handle = CreateImage("Handle", slidingArea.transform, new Color(0.50f, 0.29f, 0.13f, 0.88f));
        Stretch(handle.rectTransform);
        handle.raycastTarget = true;
        scrollbar.handleRect = handle.rectTransform;
        scrollbar.targetGraphic = handle;

        ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>();
        scrollRect.content = contentRect;
        scrollRect.viewport = viewportRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.decelerationRate = 0.12f;
        scrollRect.scrollSensitivity = 36f;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        scrollRect.verticalScrollbarSpacing = 8f;

        return new ScrollReferences
        {
            ScrollRect = scrollRect,
            ContentRoot = contentRect,
            BodyText = bodyText
        };
    }

    private static Image CreateImage(string name, Transform parent, Color color, Sprite sprite = null)
    {
        GameObject target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        target.transform.SetParent(parent, false);
        Image image = target.GetComponent<Image>();
        image.color = color;
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        return image;
    }

    private static Button CreateButton(
        Transform parent,
        string name,
        string text,
        Color backgroundColor,
        Color textColor,
        int fontSize,
        Sprite sprite,
        out Image background)
    {
        background = CreateImage(name, parent, backgroundColor, sprite);
        background.raycastTarget = true;

        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.95f, 0.84f, 1f);
        colors.pressedColor = new Color(0.82f, 0.76f, 0.66f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.70f, 0.70f, 0.70f, 0.60f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        Text label = CreateText("Text", background.transform, text, fontSize, FontStyle.Bold, textColor, TextAnchor.MiddleCenter);
        Stretch(label.rectTransform);
        return button;
    }

    private static Text CreateText(
        string name,
        Transform parent,
        string value,
        int fontSize,
        FontStyle fontStyle,
        Color color,
        TextAnchor alignment)
    {
        GameObject target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        target.transform.SetParent(parent, false);
        TMP_Text label = target.GetComponent<TMP_Text>();
        label.font = defaultFont;
        label.text = value;
        label.fontSize = fontSize;
        label.fontStyle = ConvertFontStyle(fontStyle);
        label.color = color;
        label.alignment = ConvertAlignment(alignment);
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Min(12, fontSize);
        label.fontSizeMax = fontSize;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Truncate;
        label.raycastTarget = false;
        return label;
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

    private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetCentered(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private sealed class ScrollReferences
    {
        public ScrollRect ScrollRect;
        public RectTransform ContentRoot;
        public Text BodyText;
    }
}
