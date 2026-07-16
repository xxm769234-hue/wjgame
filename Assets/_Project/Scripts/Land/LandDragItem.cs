using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>UI input adapter. It neither places land nor owns build state.</summary>
public sealed class LandDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("对应地块配置")]
    [SerializeField] private LandConfig config;
    [Header("Runtime Display")]
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private Image iconImage;
    private LandPreview preview;
    private bool hasBackendConfig;

    public void SetBackendConfig(LandConfig value)
    {
        config = value;
        hasBackendConfig = value != null;
        gameObject.SetActive(hasBackendConfig);
        if (!hasBackendConfig)
        {
            SetLabel(string.Empty);
            return;
        }

        gameObject.name = $"Land Drag Item - {DisplayName(config)}";
        SetLabel(FormatLabel(config));
        if (iconImage != null && config.landSprite != null) iconImage.sprite = config.landSprite;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!hasBackendConfig || config == null)
        {
            Debug.LogWarning("Land drag ignored because backend land config is not loaded.", this);
            return;
        }

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

    private void SetLabel(string value)
    {
        if (labelText == null) labelText = GetComponentInChildren<TMP_Text>(true);
        if (labelText != null) labelText.text = value;
    }

    private static string FormatLabel(LandConfig land)
    {
        string name = DisplayName(land);
        return land.cost > 0 ? $"{name}\nCost: {land.cost}" : name;
    }

    private static string DisplayName(LandConfig land)
    {
        if (land == null) return "Land";
        return string.IsNullOrEmpty(land.landName) ? $"Land {land.landId}" : land.landName;
    }
}
