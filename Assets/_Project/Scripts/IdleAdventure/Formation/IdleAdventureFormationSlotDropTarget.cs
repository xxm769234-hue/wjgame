using System;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class IdleAdventureFormationSlotDropTarget : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    [SerializeField] private int slotIndex = -1;

    private Action<int, int> onDropped;
    private Action<int> onClicked;

    public void Initialize(int index, Action<int, int> dropAction, Action<int> clickAction)
    {
        slotIndex = index;
        onDropped = dropAction;
        onClicked = clickAction;
    }

    public void OnDrop(PointerEventData eventData)
    {
        IdleAdventureFormationDragItem dragItem = null;
        if (eventData.pointerDrag != null)
        {
            dragItem = eventData.pointerDrag.GetComponent<IdleAdventureFormationDragItem>();
            if (dragItem == null) dragItem = eventData.pointerDrag.GetComponentInParent<IdleAdventureFormationDragItem>();
            if (dragItem == null) dragItem = eventData.pointerDrag.GetComponentInChildren<IdleAdventureFormationDragItem>();
        }

        if (dragItem == null)
        {
            Debug.LogWarning($"Formation drop ignored on slot {slotIndex}: no drag item found.", this);
            return;
        }

        Debug.Log($"Formation drop accepted: slot={slotIndex}, npcIndex={dragItem.DefinitionIndex}", this);
        onDropped?.Invoke(slotIndex, dragItem.DefinitionIndex);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right) return;
        onClicked?.Invoke(slotIndex);
    }
}
