using System;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class IdleAdventurePreviewDropTarget : MonoBehaviour, IDropHandler
{
    private int slot;
    private Func<bool> canDrop;
    private Action<int, int> assign;

    public void Bind(int slotIndex, Func<bool> allowed, Action<int, int> onAssign)
    {
        slot = slotIndex;
        canDrop = allowed;
        assign = onAssign;
    }

    public void OnDrop(PointerEventData data)
    {
        IdleAdventurePreviewDragItem source = data.pointerDrag != null
            ? data.pointerDrag.GetComponent<IdleAdventurePreviewDragItem>() : null;
        if (source != null && source.CanDrop && canDrop != null && canDrop())
            assign?.Invoke(slot, source.DefinitionIndex);
    }
}
