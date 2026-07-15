using System.Collections;
using UnityEngine;

/// <summary>Only animates the sidebar and map UI; it has no build-domain dependency.</summary>
public sealed class BuildPanelController : MonoBehaviour
{
    [SerializeField] private RectTransform buildPanel;
    [SerializeField] private RectTransform mapArea;
    [SerializeField] private Transform mapRoot;
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

    private void Awake()
    {
        if (buildPanel == null)
        {
            Debug.LogError("BuildPanelController requires a Build Panel.", this);
            enabled = false;
            return;
        }
        rootCanvas = buildPanel.GetComponentInParent<Canvas>();
        if (mapRoot == null)
        {
            GameObject root = GameObject.Find("MapRoot");
            if (root != null) mapRoot = root.transform;
        }
        if (mapRoot != null) mapRootClosedPosition = mapRoot.localPosition;
        IsOpen = startOpen;
        Apply(IsOpen);
    }

    public void TogglePanel() => SetOpen(!IsOpen);
    public void ShowPanel() => SetOpen(true);
    public void HidePanel() => SetOpen(false);

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
        animationRoutine = null;
    }

    private void Apply(bool open)
    {
        buildPanel.anchoredPosition = open ? panelOpenPosition : panelClosedPosition;
        if (mapArea != null) mapArea.anchoredPosition = open ? mapOpenPosition : mapClosedPosition;
        if (mapRoot != null) mapRoot.localPosition = mapRootClosedPosition + (open ? worldMapOpenOffset : Vector3.zero);
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
