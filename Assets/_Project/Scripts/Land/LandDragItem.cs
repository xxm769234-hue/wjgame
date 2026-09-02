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
        if (!config.canPlace)
        {
            string reason = string.IsNullOrEmpty(config.disabledReason)
                ? "服务器当前不允许放置此地块。"
                : config.disabledReason;
            Debug.LogWarning($"Land drag rejected by backend availability: {reason}", this);
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
        if (labelText != null) labelText.text = FrontendTextLocalization.Normalize(value);
    }

    private static string FormatLabel(LandConfig land)
    {
        string name = DisplayName(land);
        string cost = FormatCost(land);
        if (land.canPlace) return string.IsNullOrEmpty(cost) ? name : $"{name}\n{cost}";

        string reason = string.IsNullOrEmpty(land.disabledReason) ? "不可用" : land.disabledReason;
        return string.IsNullOrEmpty(cost)
            ? $"{name}\n不可用：{reason}"
            : $"{name}\n{cost}\n不可用：{reason}";
    }

    private static string FormatCost(LandConfig land)
    {
        if (land == null) return string.Empty;

        System.Collections.Generic.List<string> costs = new System.Collections.Generic.List<string>();
        if (land.goldCost > 0L) costs.Add($"金币 {land.goldCost:N0}");
        if (land.woodCost > 0L) costs.Add($"木材 {land.woodCost:N0}");
        if (land.stoneCost > 0L) costs.Add($"石材 {land.stoneCost:N0}");
        if (land.ironCost > 0L) costs.Add($"铁矿 {land.ironCost:N0}");
        if (costs.Count > 0) return "消耗：" + string.Join("、", costs);
        return land.cost > 0 ? $"消耗：{land.cost:N0}" : string.Empty;
    }

    private static string DisplayName(LandConfig land)
    {
        if (land == null) return "地块";
        return string.IsNullOrEmpty(land.landName) ? $"地块 {land.landId}" : land.landName;
    }
}
