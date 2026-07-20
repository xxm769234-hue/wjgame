using UnityEngine;
using UnityEngine.EventSystems;

public sealed class IdleAdventureFormationDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private int definitionIndex = -1;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Canvas canvas;
    private Transform originalParent;
    private Vector2 originalAnchoredPosition;

    public int DefinitionIndex => definitionIndex;

    public void Initialize(int index, Canvas ownerCanvas)
    {
        definitionIndex = index;
        canvas = ownerCanvas;
        rectTransform = transform as RectTransform;
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (rectTransform == null || canvas == null) return;

        originalParent = rectTransform.parent;
        originalAnchoredPosition = rectTransform.anchoredPosition;
        rectTransform.SetParent(canvas.transform, true);
        rectTransform.SetAsLastSibling();
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (rectTransform == null) return;
        rectTransform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (rectTransform == null || originalParent == null) return;

        rectTransform.SetParent(originalParent, false);
        rectTransform.anchoredPosition = originalAnchoredPosition;
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
    }
}
