using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>UI input adapter. It neither places land nor owns build state.</summary>
public sealed class LandDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private const float IconFrameSize = LandTypeIconCatalog.DisplayIconPixels;
    private const float IconDisplayScaleMultiplier = 0.82f;
    private const float IconBaselineCompensation = -12f;
    private const float ContactShadowOuterHorizontalOffset = 4.5f;
    private const float ContactShadowOuterVerticalOffset = -4.5f;
    private const float ContactShadowOuterScale = 1.12f;
    private const float ContactShadowOuterAlpha = 0.18f;
    private const float ContactShadowCoreHorizontalOffset = 2.5f;
    private const float ContactShadowCoreVerticalOffset = -3f;
    private const float ContactShadowCoreScale = 1.06f;
    private const float ContactShadowCoreAlpha = 0.30f;
    private const string TrayFrontResourcePath = "UI/BuildPanelSlotFront";
    [Header("对应地块配置")]
    [SerializeField] private LandConfig config;
    [Header("Runtime Display")]
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private Image iconImage;
    private Image cardImage;
    private RectTransform iconRect;
    private Image contactShadowImage;
    private RectTransform contactShadowRect;
    private Image contactShadowCoreImage;
    private RectTransform contactShadowCoreRect;
    private Image trayFrontImage;
    private RectTransform trayFrontImageRect;
    private LandPreview preview;
    private bool hasBackendConfig;
    private bool hasVisibleIcon;
    private static Sprite trayFrontSprite;

    private void Awake()
    {
        cardImage = GetComponent<Image>();
        if (labelText == null) labelText = GetComponentInChildren<TMP_Text>(true);
        EnsureIconImage();
        EnsureTrayLayers();
    }

    private void LateUpdate()
    {
        if (trayFrontImage != null && cardImage != null && trayFrontImage.color != cardImage.color)
        {
            trayFrontImage.color = cardImage.color;
        }
    }

    public void SetBackendConfig(LandConfig value)
    {
        config = value;
        hasBackendConfig = value != null;
        hasVisibleIcon = false;
        gameObject.SetActive(hasBackendConfig);
        if (!hasBackendConfig)
        {
            SetLabel(string.Empty);
            if (iconImage != null) iconImage.enabled = false;
            return;
        }

        gameObject.name = $"Land Drag Item - {DisplayName(config)}";
        string state = config.canPlace ? string.Empty : FrontendTextLocalization.NormalizePlayerMessage(config.disabledReason, "暂不可用");
        SetLabel(string.IsNullOrWhiteSpace(state) ? DisplayName(config) : DisplayName(config) + "｜" + state);
        ApplyTypeIcon(config);
        ConfigureLabelLayout();
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
                ? "这块地暂时无法放置。"
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
        if (labelText == null)
        {
            GameObject labelObject = new GameObject("Land Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.layer = gameObject.layer;
            labelObject.transform.SetParent(transform, false);
            labelText = labelObject.GetComponent<TextMeshProUGUI>();
        }
        if (labelText != null) labelText.text = FrontendTextLocalization.Normalize(value);
    }

    private void EnsureIconImage()
    {
        if (iconImage != null && iconImage != cardImage)
        {
            iconRect = iconImage.rectTransform;
            return;
        }

        GameObject iconObject = new GameObject("Land Type Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObject.transform.SetParent(transform, false);
        iconImage = iconObject.GetComponent<Image>();
        iconRect = iconImage.rectTransform;
        iconImage.raycastTarget = false;
    }

    private void ApplyTypeIcon(LandConfig land)
    {
        EnsureIconImage();
        if (iconImage == null) return;

        Sprite icon = LandTypeIconCatalog.GetIcon(land);
        hasVisibleIcon = icon != null;
        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
        iconImage.type = Image.Type.Simple;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        EnsureTrayLayers();
        if (contactShadowImage != null) contactShadowImage.enabled = icon != null;

        if (icon != null)
        {
            iconImage.color = land.canPlace ? Color.white : new Color(0.62f, 0.62f, 0.62f, 1f);
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = LandTypeIconCatalog.GetUiVisualOffset(land.LandTypeKey) + new Vector2(0f, IconBaselineCompensation);
            iconRect.sizeDelta = new Vector2(IconFrameSize, IconFrameSize);
            Vector2 visualScale = LandTypeIconCatalog.GetUiVisualScale(land.LandTypeKey);
            iconRect.localScale = new Vector3(
                visualScale.x * IconDisplayScaleMultiplier,
                visualScale.y * IconDisplayScaleMultiplier,
                1f);
            RefreshTrayLayers();
            ApplyTrayLayerOrder();
            ConfigureLabelLayout();
        }
        else
        {
            RefreshTrayLayers();
            ApplyTrayLayerOrder();
        }
    }

    private void EnsureTrayLayers()
    {
        if (cardImage == null) return;

        if (contactShadowImage == null)
        {
            GameObject shadowObject = new GameObject("Contact Shadow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            shadowObject.transform.SetParent(transform, false);
            contactShadowImage = shadowObject.GetComponent<Image>();
            contactShadowRect = contactShadowImage.rectTransform;
            contactShadowImage.type = Image.Type.Simple;
            contactShadowImage.preserveAspect = true;
            contactShadowImage.raycastTarget = false;
            contactShadowImage.maskable = true;
        }

        if (contactShadowCoreImage == null)
        {
            GameObject shadowCoreObject = new GameObject("Contact Shadow Core", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            shadowCoreObject.transform.SetParent(transform, false);
            contactShadowCoreImage = shadowCoreObject.GetComponent<Image>();
            contactShadowCoreRect = contactShadowCoreImage.rectTransform;
            contactShadowCoreImage.type = Image.Type.Simple;
            contactShadowCoreImage.preserveAspect = true;
            contactShadowCoreImage.raycastTarget = false;
            contactShadowCoreImage.maskable = true;
        }

        if (trayFrontImage == null)
        {
            GameObject overlayObject = new GameObject("Tray Front Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlayObject.transform.SetParent(transform, false);
            trayFrontImage = overlayObject.GetComponent<Image>();
            trayFrontImageRect = trayFrontImage.rectTransform;
            trayFrontImage.type = Image.Type.Simple;
            trayFrontImage.preserveAspect = true;
            trayFrontImage.raycastTarget = false;
            trayFrontImage.maskable = true;
        }

        RefreshTrayLayers();
    }

    private void RefreshTrayLayers()
    {
        RectTransform rootRect = transform as RectTransform;
        if (rootRect == null) return;

        bool hasSlotSprite = cardImage != null && cardImage.sprite != null;
        Sprite frontSprite = GetTrayFrontSprite();

        if (trayFrontImage != null && trayFrontImageRect != null)
        {
            trayFrontImage.sprite = hasSlotSprite && frontSprite != null ? frontSprite : null;
            trayFrontImage.color = cardImage != null ? cardImage.color : Color.white;
            trayFrontImageRect.anchorMin = Vector2.zero;
            trayFrontImageRect.anchorMax = Vector2.one;
            trayFrontImageRect.pivot = new Vector2(0.5f, 0.5f);
            trayFrontImageRect.anchoredPosition = Vector2.zero;
            trayFrontImageRect.sizeDelta = Vector2.zero;
            trayFrontImage.gameObject.SetActive(hasSlotSprite && frontSprite != null);
        }

        if (contactShadowImage != null && contactShadowRect != null)
        {
            Sprite iconSprite = iconImage != null ? iconImage.sprite : null;
            contactShadowImage.sprite = iconSprite;
            contactShadowImage.type = Image.Type.Simple;
            contactShadowImage.preserveAspect = true;
            contactShadowRect.anchorMin = new Vector2(0.5f, 0.5f);
            contactShadowRect.anchorMax = new Vector2(0.5f, 0.5f);
            contactShadowRect.pivot = new Vector2(0.5f, 0.5f);
            if (iconRect != null)
            {
                contactShadowRect.anchoredPosition = iconRect.anchoredPosition
                    + new Vector2(ContactShadowOuterHorizontalOffset, ContactShadowOuterVerticalOffset);
                contactShadowRect.sizeDelta = iconRect.sizeDelta;
                contactShadowRect.localScale = iconRect.localScale * ContactShadowOuterScale;
            }
            contactShadowImage.color = new Color(0.16f, 0.11f, 0.08f, ContactShadowOuterAlpha);
            contactShadowImage.enabled = hasVisibleIcon && hasSlotSprite && iconSprite != null;
        }

        if (contactShadowCoreImage != null && contactShadowCoreRect != null)
        {
            Sprite iconSprite = iconImage != null ? iconImage.sprite : null;
            contactShadowCoreImage.sprite = iconSprite;
            contactShadowCoreImage.type = Image.Type.Simple;
            contactShadowCoreImage.preserveAspect = true;
            contactShadowCoreRect.anchorMin = new Vector2(0.5f, 0.5f);
            contactShadowCoreRect.anchorMax = new Vector2(0.5f, 0.5f);
            contactShadowCoreRect.pivot = new Vector2(0.5f, 0.5f);
            if (iconRect != null)
            {
                contactShadowCoreRect.anchoredPosition = iconRect.anchoredPosition
                    + new Vector2(ContactShadowCoreHorizontalOffset, ContactShadowCoreVerticalOffset);
                contactShadowCoreRect.sizeDelta = iconRect.sizeDelta;
                contactShadowCoreRect.localScale = iconRect.localScale * ContactShadowCoreScale;
            }
            contactShadowCoreImage.color = new Color(0.16f, 0.11f, 0.08f, ContactShadowCoreAlpha);
            contactShadowCoreImage.enabled = hasVisibleIcon && hasSlotSprite && iconSprite != null;
        }
    }

    private void ApplyTrayLayerOrder()
    {
        if (contactShadowRect != null) contactShadowRect.SetAsLastSibling();
        if (contactShadowCoreRect != null) contactShadowCoreRect.SetAsLastSibling();
        if (iconRect != null) iconRect.SetAsLastSibling();
        if (trayFrontImage != null) trayFrontImage.rectTransform.SetAsLastSibling();
        if (labelText != null) labelText.rectTransform.SetAsLastSibling();
    }

    private static Sprite GetTrayFrontSprite()
    {
        if (trayFrontSprite == null) trayFrontSprite = Resources.Load<Sprite>(TrayFrontResourcePath);
        return trayFrontSprite;
    }

    private void ConfigureLabelLayout()
    {
        if (labelText == null) return;

        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = new Vector2(0.5f, 1f);
        labelRect.anchorMax = labelRect.anchorMin;
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = new Vector2(0f, -2f);
        labelRect.sizeDelta = new Vector2(204f, 32f);
        labelRect.localScale = Vector3.one;
        labelRect.localRotation = Quaternion.identity;
        labelText.gameObject.SetActive(true);
        labelText.font = RuntimeFontAssetProvider.Resolve();
        labelText.fontSize = 18f;
        labelText.fontSizeMin = 15f;
        labelText.fontSizeMax = 18f;
        labelText.enableAutoSizing = true;
        labelText.fontStyle = FontStyles.Bold;
        labelText.color = new Color(0.22f, 0.12f, 0.07f, 1f);
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.enableWordWrapping = false;
        labelText.maxVisibleLines = 1;
        labelText.overflowMode = TextOverflowModes.Ellipsis;
        labelText.raycastTarget = false;
        labelText.margin = Vector4.zero;
        labelText.lineSpacing = 0f;
    }

    private static string FormatLabel(LandConfig land)
    {
        string name = DisplayName(land);
        string cost = FormatCost(land);
        if (land.canPlace) return string.IsNullOrEmpty(cost) ? name : $"{name}\n{cost}";

        string reason = FrontendTextLocalization.NormalizePlayerMessage(land.disabledReason, "暂时无法放置");
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
