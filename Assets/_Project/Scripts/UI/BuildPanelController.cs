using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Only animates the sidebar and map UI; it has no build-domain dependency.</summary>
public sealed class BuildPanelController : MonoBehaviour
{
    [SerializeField] private RectTransform buildPanel;
    [SerializeField] private RectTransform mapArea;
    [SerializeField] private Transform mapRoot;
    [SerializeField] private LandDragItem landDragItemPrefab;
    [SerializeField] private Transform landDragItemContainer;
    [SerializeField, Min(0.01f)] private float animationDuration = 0.25f;
    [SerializeField] private Vector2 panelOpenPosition = new Vector2(43f, 0f);
    [SerializeField] private Vector2 panelClosedPosition = new Vector2(438f, 0f);
    [SerializeField] private Vector2 mapClosedPosition = Vector2.zero;
    [SerializeField] private Vector2 mapOpenPosition = new Vector2(-395f, 0f);
    [SerializeField] private Vector3 worldMapOpenOffset = new Vector3(-1.8f, 0f, 0f);
    [SerializeField] private bool closeWhenClickOutside = true;
    [SerializeField] private bool startOpen;

    public bool IsOpen { get; private set; }
    private Coroutine animationRoutine;
    private Vector3 mapRootClosedPosition;
    private Canvas rootCanvas;
    private int openedFrame = -1;
    private RectTransform runtimeLandItemRoot;
    private readonly List<LandDragItem> runtimeLandItems = new List<LandDragItem>();
    private const float LandListWidth = 228f;
    private const float LandItemHeight = 141f;
    private const float LandItemSpacing = 12f;
    private const float LandListRightPadding = 43f;
    private const float LandListTopPadding = 120f;
    private const float LandScrollViewWidth = 292f;
    private const float LandScrollViewHeight = 849.5f;
    private const int LandVisibleItemCount = 5;
    private const float LandViewportHeight = LandVisibleItemCount * LandItemHeight + (LandVisibleItemCount - 1) * LandItemSpacing + 4f;
    private const float LandViewportFallbackTopPadding = 66f;
    private const float LandViewportFallbackRightPadding = 50f;
    private const float BackgroundTextureWidthPixels = 868f;
    private const float BackgroundTextureHeightPixels = 2129f;
    private const float BackgroundInnerLeftPixels = 205f;
    private const float BackgroundInnerRightPixels = 685f;
    private const float BackgroundInnerTopPixels = 294f;
    private const float BackgroundInnerBottomPixels = 1946f;
    private const string SidebarWoodResourcePath = "UI/SidebarWoodBackdrop";
    private const float SidebarWoodWorldZ = 0f;
    private const int SidebarWoodSortingOrder = -32768;
    private SpriteRenderer sidebarWoodRenderer;
    private Camera sidebarWoodCamera;
    private readonly Vector3[] sidebarWoodPanelCorners = new Vector3[4];
    private readonly Vector3[] scrollbarWorldCorners = new Vector3[4];
    private static Sprite sidebarWoodSprite;

    private void Awake()
    {
        if (buildPanel == null)
        {
            Debug.LogError("BuildPanelController requires a Build Panel.", this);
            enabled = false;
            return;
        }
        rootCanvas = buildPanel.GetComponentInParent<Canvas>();
        EnsureSidebarWoodBackdrop();
        if (landDragItemContainer == null) landDragItemContainer = ResolveLandDragItemContainer();
        ResolveLandDragItemPrefab();
        HideSceneLandDragItemTemplates();
        if (mapRoot == null)
        {
            GameObject root = GameObject.Find("MapRoot");
            if (root != null) mapRoot = root.transform;
        }
        if (mapRoot != null) mapRootClosedPosition = mapRoot.localPosition;
        IsOpen = startOpen;
        Apply(IsOpen);
        SetRemoteAvailableLands(null);
    }

    private void EnsureSidebarWoodBackdrop()
    {
        if (buildPanel == null) return;

        if (sidebarWoodSprite == null)
        {
            sidebarWoodSprite = Resources.Load<Sprite>(SidebarWoodResourcePath);
        }

        if (sidebarWoodSprite == null)
        {
            if (sidebarWoodRenderer != null) sidebarWoodRenderer.gameObject.SetActive(false);
            return;
        }

        if (sidebarWoodRenderer == null)
        {
            GameObject woodObject = new GameObject("Sidebar Wood Backdrop", typeof(SpriteRenderer));
            sidebarWoodRenderer = woodObject.GetComponent<SpriteRenderer>();
        }

        sidebarWoodRenderer.sprite = sidebarWoodSprite;
        sidebarWoodRenderer.color = Color.white;
        ConfigureSidebarWoodBackdrop();
        sidebarWoodRenderer.gameObject.SetActive(true);
    }

    private void ConfigureSidebarWoodBackdrop()
    {
        if (sidebarWoodRenderer == null || sidebarWoodRenderer.sprite == null || buildPanel == null) return;

        if (sidebarWoodCamera == null) sidebarWoodCamera = Camera.main;
        if (sidebarWoodCamera == null)
        {
            sidebarWoodRenderer.gameObject.SetActive(false);
            return;
        }

        buildPanel.GetWorldCorners(sidebarWoodPanelCorners);
        Camera uiCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;
        Vector2 screenBottomLeft = RectTransformUtility.WorldToScreenPoint(uiCamera, sidebarWoodPanelCorners[0]);
        Vector2 screenTopRight = RectTransformUtility.WorldToScreenPoint(uiCamera, sidebarWoodPanelCorners[2]);
        float cameraDepth = Mathf.Abs(SidebarWoodWorldZ - sidebarWoodCamera.transform.position.z);
        Vector3 worldBottomLeft = sidebarWoodCamera.ScreenToWorldPoint(new Vector3(screenBottomLeft.x, screenBottomLeft.y, cameraDepth));
        Vector3 worldTopRight = sidebarWoodCamera.ScreenToWorldPoint(new Vector3(screenTopRight.x, screenTopRight.y, cameraDepth));
        float targetWidth = Mathf.Abs(worldTopRight.x - worldBottomLeft.x);
        float targetHeight = Mathf.Abs(worldTopRight.y - worldBottomLeft.y);
        Vector2 spriteSize = sidebarWoodRenderer.sprite.bounds.size;
        if (targetWidth <= 0f || targetHeight <= 0f || spriteSize.x <= 0f || spriteSize.y <= 0f)
        {
            sidebarWoodRenderer.gameObject.SetActive(false);
            return;
        }

        float uniformScale = Mathf.Max(targetWidth / spriteSize.x, targetHeight / spriteSize.y);
        sidebarWoodRenderer.transform.position = new Vector3(
            (worldBottomLeft.x + worldTopRight.x) * 0.5f,
            (worldBottomLeft.y + worldTopRight.y) * 0.5f,
            SidebarWoodWorldZ);
        sidebarWoodRenderer.transform.rotation = Quaternion.identity;
        sidebarWoodRenderer.transform.localScale = new Vector3(uniformScale, uniformScale, 1f);
        ConfigureSidebarWoodSorting();
        sidebarWoodRenderer.gameObject.SetActive(true);
    }

    private void ConfigureSidebarWoodSorting()
    {
        if (sidebarWoodRenderer == null) return;

        sidebarWoodRenderer.sortingLayerName = "Default";
        sidebarWoodRenderer.sortingOrder = SidebarWoodSortingOrder;
    }

    public void TogglePanel() => SetOpen(!IsOpen);
    public void ShowPanel() => SetOpen(true);
    public void HidePanel() => SetOpen(false);

    private Vector2 ResolvePanelOpenPosition()
    {
        RectTransform panelParent = buildPanel != null ? buildPanel.parent as RectTransform : null;
        ScrollRect scrollRect = buildPanel != null ? buildPanel.GetComponentInChildren<ScrollRect>(true) : null;
        RectTransform scrollbarRect = scrollRect != null && scrollRect.verticalScrollbar != null
            ? scrollRect.verticalScrollbar.transform as RectTransform
            : null;
        if (panelParent == null || scrollbarRect == null) return panelOpenPosition;

        Canvas.ForceUpdateCanvases();
        scrollbarRect.GetWorldCorners(scrollbarWorldCorners);
        float scrollbarRight = float.NegativeInfinity;
        for (int i = 0; i < scrollbarWorldCorners.Length; i++)
        {
            float cornerX = panelParent.InverseTransformPoint(scrollbarWorldCorners[i]).x;
            scrollbarRight = Mathf.Max(scrollbarRight, cornerX);
        }

        if (float.IsNegativeInfinity(scrollbarRight)) return panelOpenPosition;
        float alignmentOffset = panelParent.rect.xMax - scrollbarRight;
        return new Vector2(buildPanel.anchoredPosition.x + alignmentOffset, panelOpenPosition.y);
    }

    public void SetRemoteAvailableLands(IReadOnlyList<LandConfig> configs)
    {
        if (buildPanel == null) return;

        int count = configs != null ? configs.Count : 0;
        IReadOnlyList<LandDragItem> items = RebuildLandDragItems(count);
        for (int i = 0; i < items.Count; i++)
        {
            LandConfig config = i < count ? configs[i] : null;
            items[i].SetBackendConfig(config);
        }
    }

    private IReadOnlyList<LandDragItem> RebuildLandDragItems(int requiredCount)
    {
        ClearRuntimeLandItems();
        if (requiredCount <= 0) return runtimeLandItems;

        LandDragItem prefab = ResolveLandDragItemPrefab();
        RectTransform parent = ResolveRuntimeLandItemRoot();
        if (prefab == null)
        {
            Debug.LogWarning($"Backend returned {requiredCount} placeable land configs, but no LandDragItem prefab or template is assigned.", this);
            return runtimeLandItems;
        }

        ConfigureRuntimeLandList(parent, requiredCount);
        for (int i = 0; i < requiredCount; i++)
        {
            LandDragItem item = Instantiate(prefab, parent);
            item.name = $"Land Drag Item Runtime {i + 1}";
            item.gameObject.SetActive(true);
            NormalizeRuntimeLandItem(item, i);
            runtimeLandItems.Add(item);
        }

        Debug.Log($"Build panel generated {runtimeLandItems.Count} land drag item(s) from backend configs.", this);
        StartCoroutine(RefreshRuntimeLandListLayoutNextFrame());
        return runtimeLandItems;
    }

    private void ClearRuntimeLandItems()
    {
        for (int i = runtimeLandItems.Count - 1; i >= 0; i--)
        {
            if (runtimeLandItems[i] != null) Destroy(runtimeLandItems[i].gameObject);
        }
        runtimeLandItems.Clear();
    }

    private LandDragItem ResolveLandDragItemPrefab()
    {
        if (landDragItemPrefab != null)
        {
            HideSceneTemplate(landDragItemPrefab);
            return landDragItemPrefab;
        }

        LandDragItem template = buildPanel.GetComponentInChildren<LandDragItem>(true);
        if (template != null)
        {
            landDragItemPrefab = template;
            HideSceneTemplate(template);
        }
        return landDragItemPrefab;
    }

    private static void HideSceneTemplate(LandDragItem template)
    {
        if (template != null && template.gameObject.scene.IsValid()) template.gameObject.SetActive(false);
    }

    private void HideSceneLandDragItemTemplates()
    {
        LandDragItem[] templates = buildPanel.GetComponentsInChildren<LandDragItem>(true);
        for (int i = 0; i < templates.Length; i++)
        {
            HideSceneTemplate(templates[i]);
        }
    }

    private Transform ResolveLandDragItemContainer()
    {
        LandDragItem existing = buildPanel.GetComponentInChildren<LandDragItem>(true);
        if (existing != null && existing.transform.parent != null) return existing.transform.parent;

        return buildPanel;
    }

    private RectTransform ResolveRuntimeLandItemRoot()
    {
        if (runtimeLandItemRoot != null) return runtimeLandItemRoot;

        Transform content = landDragItemContainer != null ? landDragItemContainer : buildPanel;
        Transform existing = content.Find("Runtime Land Drag Items");
        if (existing != null) runtimeLandItemRoot = existing as RectTransform;
        if (runtimeLandItemRoot == null)
        {
            GameObject rootObject = new GameObject("Runtime Land Drag Items", typeof(RectTransform));
            runtimeLandItemRoot = rootObject.GetComponent<RectTransform>();
            runtimeLandItemRoot.SetParent(content, false);
        }

        ConfigureLandScrollView(content as RectTransform);
        return runtimeLandItemRoot;
    }

    private void ConfigureRuntimeLandList(RectTransform parent, int itemCount)
    {
        float height = itemCount > 0 ? itemCount * LandItemHeight + Mathf.Max(0, itemCount - 1) * LandItemSpacing : LandItemHeight;
        parent.anchorMin = new Vector2(0f, 1f);
        parent.anchorMax = new Vector2(1f, 1f);
        parent.pivot = new Vector2(0.5f, 1f);
        parent.anchoredPosition = Vector2.zero;
        parent.sizeDelta = new Vector2(0f, height);

        LayoutElement layoutElement = parent.GetComponent<LayoutElement>();
        if (layoutElement == null) layoutElement = parent.gameObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = height;
        layoutElement.preferredHeight = height;
        layoutElement.flexibleHeight = 0f;
    }

    private static void NormalizeRuntimeLandItem(LandDragItem item, int index)
    {
        RectTransform rect = item != null ? item.transform as RectTransform : null;
        if (rect == null) return;

        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -index * (LandItemHeight + LandItemSpacing));
        rect.sizeDelta = new Vector2(LandListWidth, LandItemHeight);

        if (rect.GetComponent<RectMask2D>() == null) rect.gameObject.AddComponent<RectMask2D>();
    }

    private IEnumerator RefreshRuntimeLandListLayoutNextFrame()
    {
        yield return null;
        RefreshRuntimeLandListLayout();
    }

    private void RefreshRuntimeLandListLayout()
    {
        if (runtimeLandItemRoot == null || runtimeLandItems.Count == 0) return;

        ConfigureRuntimeLandList(runtimeLandItemRoot, runtimeLandItems.Count);
        for (int i = 0; i < runtimeLandItems.Count; i++)
        {
            NormalizeRuntimeLandItem(runtimeLandItems[i], i);
        }
    }

    private void ConfigureLandScrollView(RectTransform content)
    {
        if (content == null) return;

        RectTransform viewport = content.parent as RectTransform;
        RectTransform scrollView = viewport != null ? viewport.parent as RectTransform : null;
        if (scrollView == null) return;

        LayoutElement scrollViewLayoutElement = scrollView.GetComponent<LayoutElement>();
        if (scrollViewLayoutElement == null) scrollViewLayoutElement = scrollView.gameObject.AddComponent<LayoutElement>();
        scrollViewLayoutElement.ignoreLayout = true;

        Canvas.ForceUpdateCanvases();

        scrollView.anchorMin = new Vector2(1f, 1f);
        scrollView.anchorMax = new Vector2(1f, 1f);
        scrollView.pivot = new Vector2(1f, 1f);
        scrollView.anchoredPosition = new Vector2(-LandListRightPadding, -LandListTopPadding);
        scrollView.sizeDelta = new Vector2(LandScrollViewWidth, LandScrollViewHeight);
        Canvas.ForceUpdateCanvases();

        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.pivot = new Vector2(0.5f, 0.5f);
        if (!TryGetBackgroundInnerViewportOffsets(scrollView, out Vector2 offsetMin, out Vector2 offsetMax))
        {
            float fallbackBottomPadding = Mathf.Max(0f, LandScrollViewHeight - LandViewportHeight - LandViewportFallbackTopPadding);
            offsetMin = new Vector2(0f, fallbackBottomPadding);
            offsetMax = new Vector2(-LandViewportFallbackRightPadding, -LandViewportFallbackTopPadding);
        }

        ScrollRect scrollRect = scrollView.GetComponent<ScrollRect>();
        if (scrollRect != null)
        {
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            if (scrollRect.horizontalScrollbar != null)
            {
                scrollRect.horizontalScrollbar.gameObject.SetActive(false);
                scrollRect.horizontalScrollbar = null;
            }
        }

        Canvas.ForceUpdateCanvases();
        viewport.offsetMin = offsetMin;
        viewport.offsetMax = offsetMax;
        ConfigureLandScrollbar(scrollRect, offsetMin.y, offsetMax.y);

        if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();

        VerticalLayoutGroup layoutGroup = content.GetComponent<VerticalLayoutGroup>();
        if (layoutGroup != null)
        {
            layoutGroup.spacing = 0f;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
        }

        ContentSizeFitter sizeFitter = content.GetComponent<ContentSizeFitter>();
        if (sizeFitter != null)
        {
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    private static void ConfigureLandScrollbar(ScrollRect scrollRect, float bottomInset, float topInset)
    {
        Scrollbar scrollbar = scrollRect != null ? scrollRect.verticalScrollbar : null;
        if (scrollbar == null) return;
        RectTransform scrollbarRect = scrollbar.transform as RectTransform;
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = Vector2.one;
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.offsetMin = new Vector2(-12f, bottomInset);
        scrollbarRect.offsetMax = new Vector2(0f, topInset);
        Image track = scrollbar.GetComponent<Image>();
        if (track != null) track.color = new Color(0.38f, 0.25f, 0.13f, 0.28f);
        RectTransform handleRect = scrollbar.handleRect;
        if (handleRect != null)
        {
            RectTransform slidingArea = handleRect.parent as RectTransform;
            if (slidingArea != null)
            {
                slidingArea.anchorMin = Vector2.zero;
                slidingArea.anchorMax = Vector2.one;
                slidingArea.offsetMin = Vector2.zero;
                slidingArea.offsetMax = Vector2.zero;
            }
            Image handle = handleRect.GetComponent<Image>();
            if (handle != null) handle.color = new Color(0.65f, 0.43f, 0.23f, 1f);
        }
        ColorBlock colors = scrollbar.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.94f, 0.81f, 1f);
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.84f, 0.73f, 0.57f, 1f);
        colors.colorMultiplier = 1f;
        scrollbar.colors = colors;
    }

    private bool TryGetBackgroundInnerViewportOffsets(RectTransform scrollView, out Vector2 offsetMin, out Vector2 offsetMax)
    {
        offsetMin = Vector2.zero;
        offsetMax = Vector2.zero;
        if (buildPanel == null || scrollView == null) return false;

        Image backgroundImage = buildPanel.GetComponent<Image>();
        Sprite backgroundSprite = backgroundImage != null ? backgroundImage.sprite : null;
        if (backgroundImage == null || backgroundSprite == null) return false;

        Rect drawnRect = GetImageDrawRect(backgroundImage, backgroundSprite);
        float innerWidth = BackgroundInnerRightPixels - BackgroundInnerLeftPixels;
        float innerHeight = BackgroundInnerBottomPixels - BackgroundInnerTopPixels;
        if (drawnRect.width <= 0f || drawnRect.height <= 0f || innerWidth <= 0f || innerHeight <= 0f) return false;

        Rect innerRect = new Rect(
            drawnRect.xMin + drawnRect.width * (BackgroundInnerLeftPixels / BackgroundTextureWidthPixels),
            drawnRect.yMax - drawnRect.height * (BackgroundInnerBottomPixels / BackgroundTextureHeightPixels),
            drawnRect.width * (innerWidth / BackgroundTextureWidthPixels),
            drawnRect.height * (innerHeight / BackgroundTextureHeightPixels));

        float viewportHeight = Mathf.Min(LandViewportHeight, innerRect.height);
        float viewportTop = innerRect.yMax - (innerRect.height - viewportHeight) * 0.5f;
        float viewportBottom = viewportTop - viewportHeight;

        Vector3 worldBottomLeft = buildPanel.TransformPoint(new Vector3(innerRect.xMin, viewportBottom, 0f));
        Vector3 worldTopRight = buildPanel.TransformPoint(new Vector3(innerRect.xMax, viewportTop, 0f));
        Vector3 localBottomLeft = scrollView.InverseTransformPoint(worldBottomLeft);
        Vector3 localTopRight = scrollView.InverseTransformPoint(worldTopRight);
        Rect scrollRect = scrollView.rect;

        offsetMin = new Vector2(localBottomLeft.x - scrollRect.xMin, localBottomLeft.y - scrollRect.yMin);
        offsetMax = new Vector2(localTopRight.x - scrollRect.xMax, localTopRight.y - scrollRect.yMax);
        return offsetMax.x <= 0f && offsetMin.x >= 0f && offsetMin.y >= 0f && offsetMax.y <= 0f;
    }

    private static Rect GetImageDrawRect(Image image, Sprite sprite)
    {
        Rect rect = image.rectTransform.rect;
        if (!image.preserveAspect || sprite == null || sprite.rect.height <= 0f) return rect;

        float spriteAspect = sprite.rect.width / sprite.rect.height;
        float rectAspect = rect.width / Mathf.Max(0.0001f, rect.height);
        float drawWidth = rect.width;
        float drawHeight = rect.height;
        if (rectAspect > spriteAspect)
        {
            drawWidth = drawHeight * spriteAspect;
        }
        else
        {
            drawHeight = drawWidth / spriteAspect;
        }

        return new Rect(
            rect.center.x - drawWidth * 0.5f,
            rect.center.y - drawHeight * 0.5f,
            drawWidth,
            drawHeight);
    }

    public void SetOpen(bool open)
    {
        if (!enabled || (open == IsOpen && animationRoutine == null)) return;
        IsOpen = open;
        if (open) openedFrame = Time.frameCount;
        if (animationRoutine != null) StopCoroutine(animationRoutine);
        animationRoutine = StartCoroutine(Animate(open));
    }

    private void Update()
    {
        if (IsOpen && animationRoutine == null)
        {
            Vector2 alignedOpenPosition = ResolvePanelOpenPosition();
            if ((buildPanel.anchoredPosition - alignedOpenPosition).sqrMagnitude > 0.01f)
            {
                buildPanel.anchoredPosition = alignedOpenPosition;
            }
        }
        ConfigureSidebarWoodBackdrop();
        if (PlayerOnboardingInputLock.IsLocked) return;
        if (!closeWhenClickOutside || !IsOpen || Time.frameCount == openedFrame) return;

        if (TryGetPointerDownPosition(out Vector2 screenPosition) && !IsPointerInsideBuildPanel(screenPosition))
        {
            HidePanel();
        }
    }

    private IEnumerator Animate(bool open)
    {
        Vector2 panelStart = buildPanel.anchoredPosition;
        Vector2 mapStart = mapArea != null ? mapArea.anchoredPosition : Vector2.zero;
        Vector2 panelTarget = open ? ResolvePanelOpenPosition() : panelClosedPosition;
        Vector2 mapTarget = open ? mapOpenPosition : mapClosedPosition;
        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / animationDuration));
            buildPanel.anchoredPosition = Vector2.LerpUnclamped(panelStart, panelTarget, t);
            if (mapArea != null) mapArea.anchoredPosition = Vector2.LerpUnclamped(mapStart, mapTarget, t);
            if (mapRoot != null) mapRoot.localPosition = Vector3.LerpUnclamped(mapRootClosedPosition, mapRootClosedPosition + (open ? worldMapOpenOffset : Vector3.zero), t);
            ConfigureSidebarWoodBackdrop();
            yield return null;
        }
        buildPanel.anchoredPosition = panelTarget;
        if (mapArea != null) mapArea.anchoredPosition = mapTarget;
        if (mapRoot != null) mapRoot.localPosition = mapRootClosedPosition + (open ? worldMapOpenOffset : Vector3.zero);
        ConfigureSidebarWoodBackdrop();
        RefreshRuntimeLandListLayout();
        animationRoutine = null;
    }

    private void Apply(bool open)
    {
        buildPanel.anchoredPosition = open ? ResolvePanelOpenPosition() : panelClosedPosition;
        if (mapArea != null) mapArea.anchoredPosition = open ? mapOpenPosition : mapClosedPosition;
        if (mapRoot != null) mapRoot.localPosition = mapRootClosedPosition + (open ? worldMapOpenOffset : Vector3.zero);
        ConfigureSidebarWoodBackdrop();
        RefreshRuntimeLandListLayout();
    }

    private static bool TryGetPointerDownPosition(out Vector2 screenPosition)
    {
        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                screenPosition = touch.position;
                return true;
            }
        }

        screenPosition = Vector2.zero;
        return false;
    }

    private bool IsPointerInsideBuildPanel(Vector2 screenPosition)
    {
        Camera uiCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(buildPanel, screenPosition, uiCamera);
    }
}
