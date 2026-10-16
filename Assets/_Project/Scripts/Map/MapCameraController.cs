using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Camera))]
public sealed class MapCameraController : MonoBehaviour
{
    [Header("Pan")]
    [SerializeField] private bool enableDrag = true;
    [SerializeField] private bool allowLeftButtonDrag = true;
    [SerializeField] private bool allowMiddleButtonDrag = true;
    [SerializeField] private bool allowRightButtonDrag = true;
    [SerializeField] private Vector2 boundsPadding = new Vector2(0.5f, 0.5f);

    [Header("Zoom")]
    [SerializeField] private bool enableZoom = true;
    [SerializeField, Min(0.01f)] private float zoomSpeed = 2f;
    [SerializeField, Min(0.01f)] private float zoomedOutUnitsAcross = 2.2f;
    [SerializeField, Min(0.01f)] private float zoomedInUnitsAcross = 0.5f;
    [SerializeField, Min(0.01f)] private float zoomedInMinimumAreaRatio = 0.9375f;

    private Camera controlledCamera;
    private Bounds mapBounds;
    private bool hasBounds;
    private bool dragging;
    private int dragPointerId = -1;
    private int dragMouseButton = -1;
    private Vector3 dragWorldStart;
    private float minOrthographicSize = 1f;
    private float maxOrthographicSize = 4f;

    private void Awake()
    {
        controlledCamera = GetComponent<Camera>();
        controlledCamera.orthographic = true;
    }

    public void Configure(Bounds bounds, float landslotWidth)
    {
        Configure(bounds, landslotWidth, landslotWidth);
    }

    public void Configure(Bounds bounds, float landslotWidth, float landslotHeight)
    {
        if (controlledCamera == null) controlledCamera = GetComponent<Camera>();
        controlledCamera.orthographic = true;

        mapBounds = bounds;
        mapBounds.Expand(new Vector3(boundsPadding.x * 2f, boundsPadding.y * 2f, 0f));
        hasBounds = bounds.size.x > 0.0001f && bounds.size.y > 0.0001f;

        float aspect = Mathf.Max(0.01f, controlledCamera.aspect);
        float safeUnitWidth = Mathf.Max(0.01f, landslotWidth);
        float safeUnitHeight = Mathf.Max(0.01f, landslotHeight);
        float minimumSizeByWidth = safeUnitWidth * zoomedInUnitsAcross / (2f * aspect);
        float minimumSizeByArea = Mathf.Sqrt(safeUnitWidth * safeUnitHeight * zoomedInMinimumAreaRatio / (4f * aspect));
        minOrthographicSize = Mathf.Max(0.5f, Mathf.Max(minimumSizeByWidth, minimumSizeByArea));
        float fallbackZoomedOutSize = safeUnitWidth * zoomedOutUnitsAcross / (2f * aspect);
        float fullMapSize = hasBounds
            ? Mathf.Max(mapBounds.extents.y, mapBounds.extents.x / aspect)
            : fallbackZoomedOutSize;
        maxOrthographicSize = Mathf.Max(minOrthographicSize, fullMapSize);

        controlledCamera.orthographicSize = Mathf.Clamp(controlledCamera.orthographicSize, minOrthographicSize, maxOrthographicSize);
        controlledCamera.orthographicSize = maxOrthographicSize;

        if (hasBounds)
        {
            Vector3 center = mapBounds.center;
            transform.position = new Vector3(center.x, center.y, transform.position.z);
            ClampToBounds();
        }
    }

    private void Update()
    {
        if (PlayerOnboardingInputLock.IsGameplayInputBlocked)
        {
            EndDrag();
            return;
        }

        HandleZoom();
        HandleDrag();
    }

    private void HandleZoom()
    {
        if (!enableZoom || controlledCamera == null) return;

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) <= 0.0001f) return;
        if (IsPointerOverUi(-1)) return;

        controlledCamera.orthographicSize = Mathf.Clamp(
            controlledCamera.orthographicSize - scroll * zoomSpeed,
            minOrthographicSize,
            maxOrthographicSize);
        ClampToBounds();
    }

    private void HandleDrag()
    {
        if (!enableDrag || controlledCamera == null) return;

        if (!dragging)
        {
            TryBeginMouseDrag();
            TryBeginTouchDrag();
            return;
        }

        if (dragPointerId >= 0) ContinueTouchDrag();
        else ContinueMouseDrag();
    }

    private void TryBeginMouseDrag()
    {
        for (int button = 0; button <= 2; button++)
        {
            if (!CanUseMouseButton(button) || !Input.GetMouseButtonDown(button)) continue;
            if (button == 0 && BuildManager.Instance != null && BuildManager.Instance.IsBuildMode) continue;
            if (IsPointerOverUi(-1)) continue;

            dragging = true;
            dragPointerId = -1;
            dragMouseButton = button;
            dragWorldStart = ScreenToWorld(Input.mousePosition);
            return;
        }
    }

    private void ContinueMouseDrag()
    {
        if (dragMouseButton < 0 || !Input.GetMouseButton(dragMouseButton))
        {
            EndDrag();
            return;
        }

        PanFromScreenPosition(Input.mousePosition);
    }

    private bool CanUseMouseButton(int button)
    {
        return button == 0 && allowLeftButtonDrag
            || button == 1 && allowRightButtonDrag
            || button == 2 && allowMiddleButtonDrag;
    }

    private void TryBeginTouchDrag()
    {
        if (Input.touchCount != 1) return;

        Touch touch = Input.GetTouch(0);
        if (touch.phase != TouchPhase.Began) return;
        if (BuildManager.Instance != null && BuildManager.Instance.IsBuildMode) return;
        if (IsPointerOverUi(touch.fingerId)) return;

        dragging = true;
        dragPointerId = touch.fingerId;
        dragMouseButton = -1;
        dragWorldStart = ScreenToWorld(touch.position);
    }

    private void ContinueTouchDrag()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId != dragPointerId) continue;

            if (touch.phase == TouchPhase.Canceled || touch.phase == TouchPhase.Ended)
            {
                EndDrag();
                return;
            }

            PanFromScreenPosition(touch.position);
            return;
        }

        EndDrag();
    }

    private void PanFromScreenPosition(Vector2 screenPosition)
    {
        Vector3 currentWorld = ScreenToWorld(screenPosition);
        Vector3 delta = dragWorldStart - currentWorld;
        transform.position += new Vector3(delta.x, delta.y, 0f);
        ClampToBounds();
    }

    private Vector3 ScreenToWorld(Vector2 screenPosition)
    {
        Vector3 screen = new Vector3(screenPosition.x, screenPosition.y, Mathf.Abs(transform.position.z));
        return controlledCamera.ScreenToWorldPoint(screen);
    }

    private void ClampToBounds()
    {
        if (!hasBounds || controlledCamera == null) return;

        float halfHeight = controlledCamera.orthographicSize;
        float halfWidth = halfHeight * controlledCamera.aspect;
        Vector3 position = transform.position;

        position.x = ClampAxis(position.x, mapBounds.min.x + halfWidth, mapBounds.max.x - halfWidth, mapBounds.center.x);
        position.y = ClampAxis(position.y, mapBounds.min.y + halfHeight, mapBounds.max.y - halfHeight, mapBounds.center.y);
        transform.position = position;
    }

    private static float ClampAxis(float value, float min, float max, float center)
    {
        return min > max ? center : Mathf.Clamp(value, min, max);
    }

    private void EndDrag()
    {
        dragging = false;
        dragPointerId = -1;
        dragMouseButton = -1;
    }

    private static bool IsPointerOverUi(int pointerId)
    {
        if (EventSystem.current == null) return false;
        return pointerId >= 0
            ? EventSystem.current.IsPointerOverGameObject(pointerId)
            : EventSystem.current.IsPointerOverGameObject();
    }
}
