using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Only animates the sidebar and map UI; it has no build-domain dependency.</summary>
public sealed class BuildPanelController : MonoBehaviour
{
    [SerializeField] private RectTransform buildPanel;
    [SerializeField] private RectTransform mapArea;
    [SerializeField] private Transform mapRoot;
    [SerializeField] private LandDragItem landDragItemPrefab;
    [SerializeField] private Transform landDragItemContainer;
    [SerializeField, Min(0.01f)] private float animationDuration = 0.25f;
    [SerializeField] private Vector2 panelOpenPosition = Vector2.zero;
    [SerializeField] private Vector2 panelClosedPosition = new Vector2(350f, 0f);
    [SerializeField] private Vector2 mapClosedPosition = Vector2.zero;
    [SerializeField] private Vector2 mapOpenPosition = new Vector2(-175f, 0f);
    [SerializeField] private Vector3 worldMapOpenOffset = new Vector3(-2f, 0f, 0f);
    [SerializeField] private bool closeWhenClickOutside = true;
    [SerializeField] private bool startOpen;

    public bool IsOpen { get; private set; }
    private Coroutine animationRoutine;
    private Vector3 mapRootClosedPosition;
    private Canvas rootCanvas;
    private int openedFrame = -1;
    private RectTransform runtimeLandItemRoot;
    private readonly List<LandDragItem> runtimeLandItems = new List<LandDragItem>();
    private const float LandListWidth = 300f;
    private const float LandItemHeight = 104f;
    private const float LandItemSpacing = 10f;
    private const float LandListRightPadding = 16f;
    private const float LandListTopPadding = 24f;

    private void Awake()
    {
        if (buildPanel == null)
        {
            Debug.LogError("BuildPanelController requires a Build Panel.", this);
            enabled = false;
            return;
        }
        rootCanvas = buildPanel.GetComponentInParent<Canvas>();
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

    public void TogglePanel() => SetOpen(!IsOpen);
    public void ShowPanel() => SetOpen(true);
    public void HidePanel() => SetOpen(false);

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

        Transform existing = buildPanel.Find("Runtime Land Drag Items");
        if (existing != null) runtimeLandItemRoot = existing as RectTransform;
        if (runtimeLandItemRoot == null)
        {
            GameObject rootObject = new GameObject("Runtime Land Drag Items", typeof(RectTransform));
            runtimeLandItemRoot = rootObject.GetComponent<RectTransform>();
            runtimeLandItemRoot.SetParent(buildPanel, false);
        }

        runtimeLandItemRoot.SetAsLastSibling();
        return runtimeLandItemRoot;
    }

    private static void ConfigureRuntimeLandList(RectTransform parent, int itemCount)
    {
        float height = itemCount > 0 ? itemCount * LandItemHeight + Mathf.Max(0, itemCount - 1) * LandItemSpacing : LandItemHeight;
        parent.anchorMin = new Vector2(1f, 1f);
        parent.anchorMax = new Vector2(1f, 1f);
        parent.pivot = new Vector2(1f, 1f);
        parent.anchoredPosition = new Vector2(-LandListRightPadding, -LandListTopPadding);
        parent.sizeDelta = new Vector2(LandListWidth, height);
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
        runtimeLandItemRoot.SetAsLastSibling();
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
        if (PlayerOnboardingInputLock.IsGameplayInputBlocked) return;
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
        Vector2 panelTarget = open ? panelOpenPosition : panelClosedPosition;
        Vector2 mapTarget = open ? mapOpenPosition : mapClosedPosition;
        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / animationDuration));
            buildPanel.anchoredPosition = Vector2.LerpUnclamped(panelStart, panelTarget, t);
            if (mapArea != null) mapArea.anchoredPosition = Vector2.LerpUnclamped(mapStart, mapTarget, t);
            if (mapRoot != null) mapRoot.localPosition = Vector3.LerpUnclamped(mapRootClosedPosition, mapRootClosedPosition + (open ? worldMapOpenOffset : Vector3.zero), t);
            yield return null;
        }
        buildPanel.anchoredPosition = panelTarget;
        if (mapArea != null) mapArea.anchoredPosition = mapTarget;
        if (mapRoot != null) mapRoot.localPosition = mapRootClosedPosition + (open ? worldMapOpenOffset : Vector3.zero);
        RefreshRuntimeLandListLayout();
        animationRoutine = null;
    }

    private void Apply(bool open)
    {
        buildPanel.anchoredPosition = open ? panelOpenPosition : panelClosedPosition;
        if (mapArea != null) mapArea.anchoredPosition = open ? mapOpenPosition : mapClosedPosition;
        if (mapRoot != null) mapRoot.localPosition = mapRootClosedPosition + (open ? worldMapOpenOffset : Vector3.zero);
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
