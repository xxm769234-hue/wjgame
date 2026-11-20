using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class StarterCharacterSelectionView : MonoBehaviour
{
    private const string AssetRoot = "UI/StarterSelection/";
    private static readonly Color Paper = new Color(0.965f, 0.937f, 0.863f, 1f);
    private static readonly Color Ink = new Color(0.29f, 0.23f, 0.15f, 1f);
    private static readonly Color SecondaryInk = new Color(0.48f, 0.40f, 0.27f, 1f);
    private readonly Side[] sides = new Side[2];
    private RectTransform root;
    private Transform owner;
    private Action closeAction;
    private TMP_FontAsset font;
    private RectTransform header;
    private CanvasGroup headerGroup;
    private TMP_Text title;
    private TMP_Text subtitle;
    private RectTransform seam;
    private Image seamImage;
    private Image compass;
    private TMP_Text availability;
    private Button closeButton;
    private Button returnButton;
    private TMP_Text previewCaption;
    private Button confirmButton;
    private Button refreshButton;
    private Action<string> confirmAction;
    private Action refreshAction;
    private bool liveMode;
    private bool liveBusy;
    private bool liveSelectable;
    private bool liveConfirmed;
    private string livePendingGender;
    private string liveMessage;
    private Texture2D gradientTexture;
    private Sprite gradientSprite;
    private Texture2D edgeGradientTexture;
    private Sprite edgeGradientSprite;
    private float expansion;
    private int hoveredIndex = -1;
    private Vector2 lastSize;
    private Coroutine transition;

    public int SelectedIndex { get; private set; } = -1;
    public bool IsPreviewExpanded { get; private set; }
    public bool IsTransitioning => transition != null;

    private sealed class Side
    {
        public RectTransform root;
        public CanvasGroup group;
        public Button hitArea;
        public Image scene;
        public Image mutedScene;
        public Image leftPaperEdge;
        public Image rightPaperEdge;
        public Image fog;
        public Image fogFill;
        public CanvasGroup information;
        public TMP_Text role;
        public Image namePlate;
        public TMP_Text name;
        public TMP_Text description;
        public Button selectButton;
        public TMP_Text selectLabel;
        public Image chibi;
        public Image innerGlow;
        public float mute;
        public float jumpStart = -10f;
        public float sceneScale = 1f;
        public float sourceAspect = 1322f / 1190f;
        public float chibiTop;
    }

    public static StarterCharacterSelectionView Create(Transform owner, Action onClose)
    {
        StarterCharacterSelectionView existing = FindObjectOfType<StarterCharacterSelectionView>(true);
        if (existing != null && existing.owner == owner)
        {
            existing.closeAction = onClose;
            return existing;
        }
        GameObject screen = new GameObject("OnboardingStarterSelection", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(StarterCharacterSelectionView));
        Canvas canvas = screen.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32750;
        CanvasScaler scaler = screen.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        StarterCharacterSelectionView view = screen.GetComponent<StarterCharacterSelectionView>();
        view.owner = owner;
        view.closeAction = onClose;
        view.Build();
        return view;
    }

    private void Build()
    {
        root = transform as RectTransform;
        font = RuntimeFontAssetProvider.Resolve();
        Image background = Image("PaperBackground", root, null, Paper);
        Stretch(background.rectTransform);
        background.raycastTarget = true;
        CreateGradient();
        sides[0] = CreateSide(0, "Male", "见习编纂官", "执笔者", "写下的文字，终会成为现实。");
        sides[1] = CreateSide(1, "Female", "见习绘卷师", "绘梦者", "画出的风景，终会成为现实。");
        seamImage = Image("CenterDivider", root, "divider", new Color(.78f, .64f, .35f, 1f));
        seam = seamImage.rectTransform;
        compass = Image("CenterCompass", root, "compass", Color.white);
        header = Rect("StarterHeader", root);
        headerGroup = header.gameObject.AddComponent<CanvasGroup>();
        Image plate = Image("TitlePlate", header, "title_plate", Color.white);
        plate.preserveAspect = true;
        Stretch(plate.rectTransform);
        title = Text("StarterTitle", header, "选择执笔之人", 38f, Ink, true);
        subtitle = Text("StarterSubtitle", header, "这本书的故事，将由谁写下？", 18f, SecondaryInk);
        availability = Text("StarterAvailability", root, "初始伙伴选择尚未开放 · 当前可预览角色与启程效果", 19f, Ink);
        closeButton = Button("StarterClose", root, "稍后再选", Close, false);
        returnButton = Button("PreviewReturn", root, "返回选择", ReturnToSelection, false);
        returnButton.gameObject.SetActive(false);
        previewCaption = Text("PreviewCaption", root, string.Empty, 30f, Ink, true);
        previewCaption.gameObject.SetActive(false);
        confirmButton = Button("StarterConfirm", root, "确认启程", ConfirmSelection, true);
        refreshButton = Button("StarterRefresh", root, "重新查询", () => refreshAction?.Invoke(), false);
        confirmButton.gameObject.SetActive(false);
        refreshButton.gameObject.SetActive(false);
        Canvas.ForceUpdateCanvases();
        Layout();
    }

    private Side CreateSide(int index, string prefix, string roleLabel, string nameLabel, string descriptionLabel)
    {
        Side side = new Side();
        side.root = Rect("Starter" + prefix + "Side", root);
        side.group = side.root.gameObject.AddComponent<CanvasGroup>();
        Image backdrop = Image(prefix + "HitArea", side.root, null, Paper);
        Stretch(backdrop.rectTransform);
        backdrop.raycastTarget = true;
        side.hitArea = backdrop.gameObject.AddComponent<Button>();
        side.hitArea.transition = Selectable.Transition.None;
        side.hitArea.onClick.AddListener(() => SelectSide(index));
        EventTrigger pointer = backdrop.gameObject.AddComponent<EventTrigger>();
        AddPointerEvent(pointer, EventTriggerType.PointerEnter, () => hoveredIndex = index);
        AddPointerEvent(pointer, EventTriggerType.PointerExit, () => hoveredIndex = -1);
        RectTransform artClip = Rect(prefix + "ArtClip", side.root);
        Stretch(artClip);
        artClip.gameObject.AddComponent<RectMask2D>();
        string assetName = index == 0 ? "male" : "female";
        side.scene = Image(prefix + "Scene", artClip, assetName + "_scene", Color.white);
        side.mutedScene = Image(prefix + "MutedScene", artClip, assetName + "_scene_muted", new Color(1f, 1f, 1f, 0f));
        if (side.scene.sprite != null) side.sourceAspect = side.scene.sprite.rect.width / side.scene.sprite.rect.height;
        if (side.mutedScene.sprite == null)
        {
            side.mutedScene.sprite = side.scene.sprite;
            side.mutedScene.color = new Color(.65f, .68f, .73f, 0f);
        }
        side.leftPaperEdge = Image(prefix + "LeftPaperEdge", artClip, null, Color.clear);
        side.rightPaperEdge = Image(prefix + "RightPaperEdge", artClip, null, Color.clear);
        side.leftPaperEdge.sprite = side.rightPaperEdge.sprite = edgeGradientSprite;
        side.rightPaperEdge.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
        side.fog = Image(prefix + "PaperMist", side.root, null, Color.white);
        side.fog.sprite = gradientSprite;
        side.fogFill = Image(prefix + "PaperBase", side.root, null, Paper);
        RectTransform information = Rect(prefix + "Information", side.root);
        Stretch(information);
        side.information = information.gameObject.AddComponent<CanvasGroup>();
        side.role = Text(prefix + "Role", information, roleLabel, 20f, SecondaryInk);
        side.namePlate = Image(prefix + "NamePlate", information, "name_plate", Color.white);
        side.namePlate.preserveAspect = true;
        side.name = Text(prefix + "Name", information, nameLabel, 40f, Ink, true);
        side.description = Text(prefix + "Description", information, descriptionLabel, 21f, Ink);
        side.selectButton = Button(prefix + "SelectButton", information, index == 0 ? "选择他" : "选择她", () =>
        {
            if (SelectedIndex == index) PreviewSelection();
            else SelectSide(index);
        }, true);
        side.selectLabel = side.selectButton.GetComponentInChildren<TMP_Text>();
        side.chibi = Image(prefix + "Chibi", information, assetName + "_chibi", Color.white);
        side.chibi.preserveAspect = true;
        side.innerGlow = Image(prefix + "SelectedEdge", side.root, null, new Color(.83f, .65f, .29f, 0f));
        return side;
    }

    public void Show()
    {
        liveMode = false;
        gameObject.SetActive(true);
        ResetSelection();
    }

    public void ShowLive(Action<string> onConfirm, Action onRefresh)
    {
        liveMode = true;
        confirmAction = onConfirm;
        refreshAction = onRefresh;
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            ResetSelection();
        }
        UpdateMode();
    }

    public void SetLiveState(bool selectable, bool busy, bool confirmed, string pendingGender, string message)
    {
        liveSelectable = selectable;
        liveBusy = busy;
        liveConfirmed = confirmed;
        livePendingGender = pendingGender;
        liveMessage = message;
        if (!string.IsNullOrEmpty(pendingGender))
        {
            SelectedIndex = pendingGender == "MALE" ? 0 : 1;
            for (int index = 0; index < sides.Length; index++)
                sides[index].selectLabel.text = index == SelectedIndex
                    ? (index == 0 ? "预览 · 以文字启程" : "预览 · 以色彩启程") : (index == 0 ? "选择他" : "选择她");
        }
        UpdateMode();
        Layout();
    }

    public void ConfirmSelection()
    {
        if (!liveMode || !liveSelectable || liveBusy || liveConfirmed || !IsPreviewExpanded || IsTransitioning || SelectedIndex < 0) return;
        string gender = SelectedIndex == 0 ? "MALE" : "FEMALE";
        if (!string.IsNullOrEmpty(livePendingGender) && livePendingGender != gender) return;
        confirmAction?.Invoke(gender);
    }

    public void SelectSide(int index)
    {
        if (index < 0 || index > 1 || IsTransitioning || IsPreviewExpanded || liveMode && (liveBusy || liveConfirmed)) return;
        if (liveMode && !string.IsNullOrEmpty(livePendingGender) && (index == 0 ? "MALE" : "FEMALE") != livePendingGender) return;
        SelectedIndex = index;
        sides[index].jumpStart = Time.unscaledTime;
        for (int sideIndex = 0; sideIndex < sides.Length; sideIndex++)
        {
            sides[sideIndex].selectLabel.text = sideIndex == index
                ? (sideIndex == 0 ? "预览 · 以文字启程" : "预览 · 以色彩启程")
                : (sideIndex == 0 ? "选择他" : "选择她");
        }
    }

    public void PreviewSelection()
    {
        if (SelectedIndex < 0 || IsTransitioning || IsPreviewExpanded || liveMode && (liveBusy || liveConfirmed)) return;
        transition = StartCoroutine(AnimateExpansion(1f, .6f));
    }

    public void ReturnToSelection()
    {
        if (IsTransitioning || !IsPreviewExpanded || liveMode && (liveBusy || liveConfirmed)) return;
        transition = StartCoroutine(AnimateExpansion(0f, .4f));
    }

    public void Close()
    {
        gameObject.SetActive(false);
        closeAction?.Invoke();
    }

    private void ResetSelection()
    {
        StopAllCoroutines();
        transition = null;
        expansion = 0f;
        SelectedIndex = -1;
        IsPreviewExpanded = false;
        hoveredIndex = -1;
        for (int index = 0; index < sides.Length; index++)
        {
            sides[index].mute = 0f;
            sides[index].jumpStart = -10f;
            sides[index].selectLabel.text = index == 0 ? "选择他" : "选择她";
        }
        UpdateMode();
        Layout();
    }

    private IEnumerator AnimateExpansion(float target, float duration)
    {
        float start = expansion;
        float elapsed = 0f;
        returnButton.gameObject.SetActive(false);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float fraction = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - fraction, 3f);
            expansion = Mathf.Lerp(start, target, eased);
            Layout();
            yield return null;
        }
        expansion = target;
        IsPreviewExpanded = target > .5f;
        transition = null;
        UpdateMode();
        Layout();
    }

    private void UpdateMode()
    {
        returnButton.gameObject.SetActive(IsPreviewExpanded && (!liveMode || !liveConfirmed));
        returnButton.interactable = !liveMode || !liveBusy;
        previewCaption.gameObject.SetActive(IsPreviewExpanded);
        previewCaption.text = SelectedIndex == 0 ? "以文字启程" : "以色彩启程";
        availability.text = liveMode ? liveMessage : IsPreviewExpanded
            ? "角色预览 · 尚未确认，可返回重选" : "角色选择尚未开放 · 可先预览";
        confirmButton.gameObject.SetActive(liveMode && IsPreviewExpanded && !liveConfirmed);
        confirmButton.interactable = liveSelectable && !liveBusy && !IsTransitioning;
        confirmButton.GetComponentInChildren<TMP_Text>().text = liveBusy ? "正在确认…"
            : string.IsNullOrEmpty(livePendingGender) ? "确认启程" : "重试确认";
        refreshButton.gameObject.SetActive(liveMode && !liveBusy && (!liveSelectable || !string.IsNullOrEmpty(livePendingGender)));
        for (int index = 0; index < sides.Length; index++)
        {
            bool allowed = !liveMode || !liveBusy && !liveConfirmed
                && (string.IsNullOrEmpty(livePendingGender) || livePendingGender == (index == 0 ? "MALE" : "FEMALE"));
            sides[index].hitArea.interactable = allowed;
            sides[index].selectButton.interactable = allowed;
        }
    }

    private void Update()
    {
        if (owner == null)
        {
            Destroy(gameObject);
            return;
        }
        if (root == null || sides[0] == null) return;
        if (root.rect.size != lastSize) Layout();
        for (int index = 0; index < sides.Length; index++)
        {
            Side side = sides[index];
            float targetMute = SelectedIndex >= 0 && SelectedIndex != index ? 1f : 0f;
            side.mute = Mathf.MoveTowards(side.mute, targetMute, Time.unscaledDeltaTime / .3f);
            Color mutedColor = side.mutedScene.color;
            mutedColor.a = side.mute;
            side.mutedScene.color = mutedColor;
            side.chibi.color = Color.Lerp(Color.white, new Color(.65f, .67f, .71f, 1f), side.mute);
            float targetScale = hoveredIndex == index && !IsTransitioning && !IsPreviewExpanded ? 1.006f : 1f;
            side.sceneScale = Mathf.MoveTowards(side.sceneScale, targetScale, Time.unscaledDeltaTime * .04f);
            side.scene.transform.localScale = side.mutedScene.transform.localScale = Vector3.one * side.sceneScale;
            float edgeAlpha = SelectedIndex == index ? (.64f + Mathf.Sin(Time.unscaledTime * 2.2f) * .12f) * (1f - expansion) : 0f;
            side.innerGlow.color = new Color(.84f, .67f, .34f, edgeAlpha);
            float elapsed = Time.unscaledTime - side.jumpStart;
            float jump = elapsed >= 0f && elapsed < .5f ? Mathf.Sin(elapsed / .5f * Mathf.PI) * 24f : 0f;
            Vector2 position = side.chibi.rectTransform.anchoredPosition;
            position.y = -side.chibiTop + jump;
            side.chibi.rectTransform.anchoredPosition = position;
        }
    }

    private void Layout()
    {
        if (root == null || header == null) return;
        float width = root.rect.width;
        float height = root.rect.height;
        if (width <= 0f || height <= 0f) return;
        lastSize = root.rect.size;
        float halfWidth = width * .5f;
        for (int index = 0; index < sides.Length; index++)
        {
            Side side = sides[index];
            bool chosen = SelectedIndex == index;
            float sideWidth = chosen ? Mathf.Lerp(halfWidth, width, expansion) : halfWidth;
            float position = chosen ? Mathf.Lerp(index * halfWidth, 0f, expansion)
                : index * halfWidth + (index == 0 ? -halfWidth : halfWidth) * expansion;
            Place(side.root, position, 0f, sideWidth, height);
            side.group.alpha = chosen ? 1f : 1f - expansion;
            side.group.blocksRaycasts = expansion < .01f && !IsPreviewExpanded && !IsTransitioning;
            float sceneWidth = chosen ? Mathf.Lerp(halfWidth, Mathf.Min(width, height * side.sourceAspect), expansion) : halfWidth;
            float sceneHeight = sceneWidth / side.sourceAspect;
            float artTop = Mathf.Max(0f, height - sceneHeight - 240f) * (1f - expansion);
            Place(side.scene.rectTransform, (sideWidth - sceneWidth) * .5f, artTop, sceneWidth, sceneHeight);
            Place(side.mutedScene.rectTransform, (sideWidth - sceneWidth) * .5f, artTop, sceneWidth, sceneHeight);
            float edgeWidth = Mathf.Min(150f, sceneWidth * .12f);
            float artLeft = (sideWidth - sceneWidth) * .5f;
            Place(side.leftPaperEdge.rectTransform, artLeft, artTop, edgeWidth, sceneHeight);
            Place(side.rightPaperEdge.rectTransform, artLeft + sceneWidth, artTop, edgeWidth, sceneHeight);
            Color edgeColor = new Color(1f, 1f, 1f, chosen && artLeft > 1f ? expansion : 0f);
            side.leftPaperEdge.color = side.rightPaperEdge.color = edgeColor;
            float mistStart = Mathf.Min(680f, artTop + sceneHeight - 240f);
            float mistEnd = Mathf.Min(850f, artTop + sceneHeight - 8f);
            Place(side.fog.rectTransform, 0f, mistStart, sideWidth, mistEnd - mistStart);
            Place(side.fogFill.rectTransform, 0f, mistEnd, sideWidth, height - mistEnd);
            side.information.alpha = 1f - expansion;
            side.information.blocksRaycasts = expansion < .01f;
            float cardWidth = Mathf.Min(430f, sideWidth - 100f);
            Place(side.role.rectTransform, 40f, 842f, sideWidth - 80f, 30f);
            Place(side.namePlate.rectTransform, (sideWidth - cardWidth) * .5f, 756f, cardWidth, 84f);
            Place(side.name.rectTransform, (sideWidth - cardWidth) * .5f + 36f, 764f, cardWidth - 72f, 58f);
            Place(side.description.rectTransform, 38f, 878f, sideWidth - 76f, 30f);
            float buttonWidth = Mathf.Min(432f, sideWidth * .49f);
            Place(side.selectButton.transform as RectTransform, (sideWidth - buttonWidth) * .5f, 928f, buttonWidth, 88f);
            float chibiWidth = halfWidth < 850f ? 148f : 195f;
            float chibiCenter = halfWidth < 850f ? 91f : 175f;
            if (index == 1) chibiCenter = sideWidth - chibiCenter;
            side.chibiTop = halfWidth < 850f ? 850f : 812f;
            Place(side.chibi.rectTransform, chibiCenter - chibiWidth * .5f, side.chibiTop, chibiWidth, 1050f - side.chibiTop);
            Place(side.innerGlow.rectTransform, index == 0 ? sideWidth - 5f : 0f, 150f, 5f, height - 200f);
        }
        float headerWidth = Mathf.Min(620f, width * .44f);
        Place(header, (width - headerWidth) * .5f, 10f, headerWidth, 162f);
        Place(title.rectTransform, 42f, 42f, headerWidth - 84f, 50f);
        Place(subtitle.rectTransform, 52f, 96f, headerWidth - 104f, 28f);
        headerGroup.alpha = 1f - expansion;
        Place(seam, halfWidth - 5f, 148f, 10f, height - 174f);
        seamImage.color = new Color(1f, 1f, 1f, 1f - expansion);
        if (seamImage.sprite != null)
        {
            Place(seam, halfWidth - (height - 174f) * .5f, (height + 148f - 26f) * .5f - 5f, height - 174f, 10f);
            seam.pivot = new Vector2(.5f, .5f);
            seam.anchoredPosition = new Vector2(halfWidth, -(height + 148f - 26f) * .5f);
            seam.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }
        Place(compass.rectTransform, halfWidth - 32f, 488f, 64f, 64f);
        compass.color = new Color(1f, 1f, 1f, 1f - expansion);
        Place(availability.rectTransform, Mathf.Max(240f, width * .16f), height - (liveMode ? 70f : 42f), width - Mathf.Max(480f, width * .32f), liveMode ? 60f : 32f);
        availability.enableWordWrapping = liveMode;
        Place(closeButton.transform as RectTransform, 24f, 22f, 188f, 52f);
        Place(returnButton.transform as RectTransform, 24f, 88f, 188f, 52f);
        Place(previewCaption.rectTransform, width * .2f, height - (liveMode ? 238f : 130f), width * .6f, 58f);
        Place(confirmButton.transform as RectTransform, (width - 432f) * .5f, height - 172f, 432f, 88f);
        Place(refreshButton.transform as RectTransform, width - 220f, 22f, 196f, 52f);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        transition = null;
    }

    private void OnDestroy()
    {
        if (gradientSprite != null) Destroy(gradientSprite);
        if (gradientTexture != null) Destroy(gradientTexture);
        if (edgeGradientSprite != null) Destroy(edgeGradientSprite);
        if (edgeGradientTexture != null) Destroy(edgeGradientTexture);
    }

    private void CreateGradient()
    {
        gradientTexture = new Texture2D(1, 128, TextureFormat.RGBA32, false);
        gradientTexture.name = "StarterPaperMist";
        gradientTexture.wrapMode = TextureWrapMode.Clamp;
        gradientTexture.filterMode = FilterMode.Bilinear;
        for (int pixel = 0; pixel < 128; pixel++)
        {
            float fraction = 1f - pixel / 127f;
            Color color = Paper;
            color.a = Mathf.SmoothStep(0f, 1f, fraction);
            gradientTexture.SetPixel(0, pixel, color);
        }
        gradientTexture.Apply(false, true);
        gradientSprite = Sprite.Create(gradientTexture, new Rect(0f, 0f, 1f, 128f), new Vector2(.5f, .5f), 100f);
        edgeGradientTexture = new Texture2D(128, 1, TextureFormat.RGBA32, false);
        edgeGradientTexture.name = "StarterPaperEdge";
        edgeGradientTexture.wrapMode = TextureWrapMode.Clamp;
        edgeGradientTexture.filterMode = FilterMode.Bilinear;
        for (int pixel = 0; pixel < 128; pixel++)
        {
            Color color = Paper;
            color.a = Mathf.SmoothStep(1f, 0f, pixel / 127f);
            edgeGradientTexture.SetPixel(pixel, 0, color);
        }
        edgeGradientTexture.Apply(false, true);
        edgeGradientSprite = Sprite.Create(edgeGradientTexture, new Rect(0f, 0f, 128f, 1f), new Vector2(.5f, .5f), 100f);
    }

    private static RectTransform Rect(string objectName, Transform parent)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child.transform as RectTransform;
    }

    private static Image Image(string objectName, Transform parent, string asset, Color color)
    {
        RectTransform rect = Rect(objectName, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = string.IsNullOrEmpty(asset) ? null : Resources.Load<Sprite>(AssetRoot + asset);
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private TMP_Text Text(string objectName, Transform parent, string value, float size, Color color, bool bold = false)
    {
        RectTransform rect = Rect(objectName, parent);
        TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMax = size;
        text.fontSizeMin = size * .78f;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.richText = false;
        text.raycastTarget = false;
        return text;
    }

    private Button Button(string objectName, Transform parent, string label, Action action, bool artwork)
    {
        Image image = Image(objectName, parent, "select_button", Color.white);
        image.preserveAspect = true;
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, .94f, .77f, 1f);
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(.83f, .83f, .83f, 1f);
        button.colors = colors;
        button.onClick.AddListener(() => action());
        TMP_Text text = Text("Label", image.transform, label, artwork ? 27f : 21f, new Color(1f, .965f, .87f, 1f), true);
        Stretch(text.rectTransform);
        text.margin = new Vector4(28f, 5f, 28f, 5f);
        return button;
    }

    private static void AddPointerEvent(EventTrigger trigger, EventTriggerType type, Action action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(eventData => action());
        trigger.triggers.Add(entry);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rect, float left, float top, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(width, height);
    }
}
