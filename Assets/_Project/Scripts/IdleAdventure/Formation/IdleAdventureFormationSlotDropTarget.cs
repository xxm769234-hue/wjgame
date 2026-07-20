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
        IdleAdventureFormationDragItem dragItem = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<IdleAdventureFormationDragItem>() : null;
        if (dragItem == null) return;

        onDropped?.Invoke(slotIndex, dragItem.DefinitionIndex);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right) return;
        onClicked?.Invoke(slotIndex);
    }
}
