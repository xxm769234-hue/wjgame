using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class IdleAdventurePreviewDragItem : MonoBehaviour, IInitializePotentialDragHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private Func<int> character;
    private Func<bool> canDrag;
    private Image portrait;
    private ScrollRect scroll;
    private RectTransform ghost;
    private Canvas canvas;
    private bool scrolling;
    private PointerEventData activePointer;
    public bool IsDeploying { get; private set; }
    public int DefinitionIndex { get; private set; } = -1;

    public void Bind(Func<int> source, Func<bool> allowed, Image image, ScrollRect rosterScroll = null)
    {
        character = source;
        canDrag = allowed;
        portrait = image;
        scroll = rosterScroll;
    }

    public void OnInitializePotentialDrag(PointerEventData data)
    {
        if (scroll != null) scroll.OnInitializePotentialDrag(data);
    }

    public void OnBeginDrag(PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left || canDrag == null || !canDrag()) return;
        DefinitionIndex = character();
        if (DefinitionIndex < 0) return;
        activePointer = data;
        data.eligibleForClick = false;
        Vector2 delta = data.position - data.pressPosition;
        scrolling = scroll != null && Mathf.Abs(delta.x) > Mathf.Abs(delta.y);
        if (scrolling) scroll.OnBeginDrag(data);
        else BeginDeployment(data);
    }

    public void OnDrag(PointerEventData data)
    {
        if (canDrag == null || !canDrag()) { CancelDrag(); return; }
        if (scrolling)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(scroll.viewport, data.position, data.pressEventCamera, out Vector2 local);
            if (local.y > scroll.viewport.rect.yMax + 12f)
            {
                scroll.OnEndDrag(data);
                scroll.StopMovement();
                scrolling = false;
                BeginDeployment(data);
            }
            else scroll.OnDrag(data);
        }
        MoveGhost(data);
    }

    private void BeginDeployment(PointerEventData data)
    {
        canvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (canvas == null || portrait == null || portrait.sprite == null) return;
        scroll?.StopMovement();
        IsDeploying = true;
        ghost = new GameObject("Formation Drag Preview", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
        ghost.gameObject.layer = canvas.gameObject.layer;
        ghost.SetParent(canvas.transform, false);
        Image image = ghost.GetComponent<Image>();
        image.sprite = portrait.sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = new Color(1f, 1f, 1f, 0.8f);
        ghost.sizeDelta = new Vector2(100f, 140f);
        ghost.SetAsLastSibling();
        MoveGhost(data);
    }

    private void MoveGhost(PointerEventData data)
    {
        if (ghost == null) return;
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)canvas.transform, data.position, camera, out Vector3 position))
            ghost.position = position;
    }

    public bool CanDrop => IsDeploying && canDrag != null && canDrag() && character() == DefinitionIndex;

    public void OnEndDrag(PointerEventData data)
    {
        if (scrolling && scroll != null) scroll.OnEndDrag(data);
        data.eligibleForClick = false;
        CancelDrag();
    }

    public void CancelDrag()
    {
        if (scrolling && scroll != null && activePointer != null) scroll.OnEndDrag(activePointer);
        activePointer = null;
        scrolling = false;
        IsDeploying = false;
        DefinitionIndex = -1;
        if (ghost != null)
        {
            ghost.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(ghost.gameObject);
            else DestroyImmediate(ghost.gameObject);
        }
        ghost = null;
    }

    private void OnDisable() { CancelDrag(); }
}
