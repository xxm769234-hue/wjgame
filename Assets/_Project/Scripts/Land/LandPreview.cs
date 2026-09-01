using System;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class LandPreview : MonoBehaviour
{
    public event Action<LandSlotController, LandConfig> PlacementRequested;
    public event Action Cancelled;

    [SerializeField, Range(0.05f, 1f)] private float alpha = 0.55f;
    [SerializeField] private string previewSortingLayer = "Preview";
    [SerializeField] private int sortingOrder = 32767;
    private SpriteRenderer spriteRenderer;
    private Camera worldCamera;
    private MapManager mapManager;
    private LandConfig config;
    private LandSlotController currentSlot;
    private LandSlotController hoveredSlot;
    private int landSlotMask;
    private bool completed;

    private void Awake() => spriteRenderer = GetComponent<SpriteRenderer>();

    public void Initialize(LandConfig land, Camera camera)
    {
        config = land;
        worldCamera = camera != null ? camera : Camera.main;
        mapManager = MapManager.EnsureInstance();
        landSlotMask = LayerMask.GetMask("LandSlot");
        int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
        if (ignoreRaycastLayer >= 0) gameObject.layer = ignoreRaycastLayer;

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D previewCollider in colliders)
        {
            previewCollider.enabled = false;
        }

        spriteRenderer.sprite = land.landSprite;
        spriteRenderer.maskInteraction = SpriteMaskInteraction.None;
        spriteRenderer.sortingLayerName = previewSortingLayer;
        spriteRenderer.sortingOrder = sortingOrder;
        SetValidVisual(false);
        FollowPointer();
    }

    public void FollowPointer()
    {
        if (completed || worldCamera == null
            || (!PlayerOnboardingInputLock.IsTargetAllowed("FREE_LAND")
                && !PlayerOnboardingInputLock.IsTargetAllowed("LAND_SLOT"))) return;
        Vector3 screen = Input.mousePosition;
        screen.z = Mathf.Abs(worldCamera.transform.position.z);
        Vector3 world = worldCamera.ScreenToWorldPoint(screen);
        UpdatePlacementPreview(new Vector3(world.x, world.y, 0f));
    }

    public void RequestPlacement()
    {
        if (completed
            || (!PlayerOnboardingInputLock.IsTargetAllowed("FREE_LAND")
                && !PlayerOnboardingInputLock.IsTargetAllowed("LAND_SLOT"))) return;
        completed = true;
        if (currentSlot != null && config != null) PlacementRequested?.Invoke(currentSlot, config);
        else Cancelled?.Invoke();
        Destroy(gameObject);
    }

    public void Cancel()
    {
        if (completed) return;
        completed = true;
        Cancelled?.Invoke();
        Destroy(gameObject);
    }

    private void UpdatePlacementPreview(Vector3 pointerWorld)
    {
        DetectSlot(pointerWorld);
        transform.position = new Vector3(pointerWorld.x, pointerWorld.y, 0f);

        bool valid = false;
        if (hoveredSlot != null && mapManager != null)
        {
            valid = mapManager.CanPlace(hoveredSlot, config, out _);
            currentSlot = valid ? hoveredSlot : null;
        }
        else
        {
            currentSlot = null;
        }

        SetValidVisual(valid);
    }

    private void DetectSlot(Vector3 pointerWorld)
    {
        hoveredSlot = null;
        // BuildPanelController moves MapRoot with Transform animation. Force the 2D
        // physics world to use the newest Transform positions before point testing.
        Physics2D.SyncTransforms();

        // Fast path: correctly configured LandSlot layer.
        Collider2D hit = Physics2D.OverlapPoint(pointerWorld, landSlotMask);
        if (hit != null) TrySelectSlot(hit.GetComponentInParent<LandSlotController>());

        // Safety net for legacy scenes whose Shape layer was not saved correctly.
        // The collider still provides the authoritative irregular-shape containment test.
        if (hoveredSlot == null)
        {
            PolygonCollider2D[] colliders = FindObjectsByType<PolygonCollider2D>(FindObjectsSortMode.None);
            foreach (PolygonCollider2D slotCollider in colliders)
            {
                LandSlotController slot = slotCollider.GetComponent<LandSlotController>();
                if (slot != null && slotCollider.OverlapPoint(pointerWorld))
                {
                    TrySelectSlot(slot);
                    if (hoveredSlot != null) break;
                }
            }
        }
    }

    private void TrySelectSlot(LandSlotController slot)
    {
        if (slot != null) hoveredSlot = slot;
    }

    private void SetValidVisual(bool valid)
    {
        spriteRenderer.color = valid ? new Color(0.25f, 1f, 0.35f, alpha) : new Color(1f, 0.25f, 0.25f, alpha);
    }
}
