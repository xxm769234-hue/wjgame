using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>UI input adapter. It neither places land nor owns build state.</summary>
public sealed class LandDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("对应地块配置")]
    [SerializeField] private LandConfig config;
    private LandPreview preview;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (BuildManager.Instance == null)
        {
            Debug.LogError("BuildManager is missing from the scene.", this);
            return;
        }
        preview = BuildManager.Instance.BeginDrag(config);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (preview != null) preview.FollowPointer();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (preview == null) return;
        preview.RequestPlacement();
        preview = null;
    }
}
