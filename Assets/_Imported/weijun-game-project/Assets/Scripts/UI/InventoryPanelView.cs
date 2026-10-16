using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using LegacyText = UnityEngine.UI.Text;

[DisallowMultipleComponent]
public sealed class InventoryPanelView : MonoBehaviour
{
    public const int GridColumns = 7;
    public const int GridVisibleRows = 4;

    [SerializeField, HideInInspector] private int schemaVersion;

    [Serializable]
    public sealed class ResourceControl
    {
        public string Key;
        public string Label;
        public Image Icon;
        public TMP_Text ValueText;
        public Button AddButton;
    }

    [Serializable]
    public sealed class CategoryControl
    {
        public string Id;
        public Button Button;
        public Image Background;
        public TMP_Text Label;
        public GameObject SelectedMarker;
    }

    [Serializable]
    public sealed class SideActionControl
    {
        public string Id;
        public Button Button;
        public Image Background;
        public TMP_Text Label;
    }

    [Serializable]
    public sealed class IconControl
    {
        public string Key;
        public Sprite Icon;
    }

    [Header("Panel Components")]
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private Button backButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button expandButton;
    [SerializeField] private Button organizeButton;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private TMP_Text sidebarHintText;
    [SerializeField] private TMP_Text gridTitleText;
    [SerializeField] private TMP_Text emptyText;
    [SerializeField] private TMP_Text capacityText;
    [SerializeField] private TMP_Text visibleCountText;
    [SerializeField] private TMP_Text statusText;

    [Header("Reusable Controls")]
    [SerializeField] private ResourceControl[] resources = new ResourceControl[0];
    [SerializeField] private CategoryControl[] categories = new CategoryControl[0];
    [SerializeField] private GameObject categoryRoot;
    [SerializeField] private IconControl[] icons = new IconControl[0];
    [SerializeField] private InventorySlotView[] slots = new InventorySlotView[0];
    [SerializeField] private SideActionControl[] sideActions = new SideActionControl[0];

    private InventorySlotView slotTemplate;
    private RectTransform slotContent;
    private GridLayoutGroup slotGrid;
    private ScrollRect slotScroll;
    private readonly List<InventorySlotView> runtimeSlots = new List<InventorySlotView>();

    [Header("Item Quality Backgrounds")]
    [Tooltip("COMMON / 普通：白色品质框")]
    [SerializeField] private Sprite _commonBackground;
    [Tooltip("FINE / 优秀：绿色品质框")]
    [SerializeField] private Sprite _fineBackground;
    [Tooltip("EXCELLENT / 稀有：蓝色品质框")]
    [SerializeField] private Sprite _excellentBackground;
    [Tooltip("EPIC / 史诗：紫色品质框")]
    [SerializeField] private Sprite _epicBackground;
    [Tooltip("LEGENDARY / 传说：金色品质框")]
    [SerializeField] private Sprite _legendaryBackground;

    private readonly HashSet<string> _qualityWarnings = new HashSet<string>();

    [Header("Item Action Menu")]
    [SerializeField] private RectTransform contextMenu;
    [SerializeField] private TMP_Text contextNameText;
    [SerializeField] private TMP_Text contextTypeText;
    [SerializeField] private TMP_Text contextHintText;
    [SerializeField] private TMP_Text contextPrimaryLabel;
    [SerializeField] private Button contextPrimaryButton;
    [SerializeField] private Button contextSellButton;
    [SerializeField] private Button contextDismissButton;

    [Header("Resource Detail")]
    [SerializeField] private RectTransform detailRoot;
    [SerializeField] private TMP_Text detailNameText;
    [SerializeField] private TMP_Text detailTypeText;
    [SerializeField] private TMP_Text detailQuantityText;
    [SerializeField] private TMP_Text detailDescriptionText;
    [SerializeField] private TMP_FontAsset fontAsset;

    [Header("Editable Copy")]
    [SerializeField] private string inventoryTitle = "仓库";
    [SerializeField] private string inventorySubtitle = "领地物资与道具管理";
    [SerializeField] private string inventoryGridTitle = "物品栏";
    [SerializeField] private string localInventoryHint = "本地奖励库存\n仅用于本地任务奖励";
    [SerializeField] private string serverInventoryHint = "服务器库存\n按正式报价出售";
    [SerializeField] private string emptyCategoryText = "暂无物品，请获取";
    [SerializeField] private string capacityFormat = "背包空间：{0:00}/{1}";
    [SerializeField] private string visibleCountFormat = "当前物品 {0} 件  已开放 {1} 格";

    [Header("Category Style")]
    [SerializeField] private Color categoryNormalColor = new Color(0.91f, 0.67f, 0.29f, 1f);
    [SerializeField] private Color categorySelectedColor = new Color(0.92f, 0.46f, 0.16f, 1f);
    [SerializeField] private Color categoryNormalTextColor = new Color(0.44f, 0.21f, 0.07f, 1f);
    [SerializeField] private Color categorySelectedTextColor = Color.white;

    public int SlotCount => slots == null ? 0 : slots.Length;
    public int SchemaVersion => schemaVersion;

    private void Awake()
    {
        RemoveInventoryFooter();
        RemoveInventoryResourceBadges();
        if (fontAsset == null)
        {
            fontAsset = RuntimeFontAssetProvider.Resolve();
        }
        UpgradeLegacyTextComponents();
        ResolveReferences();
        SetCategoriesVisible(false);
        EnsureResourceDetailPanel();
    }

    private void RemoveInventoryResourceBadges()
    {
        resources = new ResourceControl[0];
        string[] names =
        {
            "InventoryResourceBadge_Gold",
            "InventoryResourceBadge_Diamond",
            "InventoryResourceShadow_Gold",
            "InventoryResourceShadow_Diamond"
        };

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == transform || child == null)
            {
                continue;
            }

            for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
            {
                if (child.name == names[nameIndex])
                {
                    Destroy(child.gameObject);
                    break;
                }
            }
        }
    }

    private void RemoveInventoryFooter()
    {
        Transform footer = transform.Find("InventoryFooter");
        if (footer == null)
        {
            return;
        }

        Transform organizeButton = footer.Find("InventoryOrganizeButton");
        if (organizeButton != null)
        {
            organizeButton.SetParent(transform, false);
        }

        Destroy(footer.gameObject);
    }

    public InventorySlotView GetSlot(int index)
    {
        return slots != null && index >= 0 && index < slots.Length ? slots[index] : null;
    }

    public static int GetRequiredSlotCount(int itemCount)
    {
        int rows = Mathf.Max(GridVisibleRows, Mathf.CeilToInt(Mathf.Max(0, itemCount) / (float)GridColumns));
        return rows * GridColumns;
    }

    public int PrepareSlotGrid(int itemCount)
    {
        if (slotTemplate == null && !InitializeSlotGrid()) return 0;

        int requiredCount = GetRequiredSlotCount(itemCount);
        while (runtimeSlots.Count < requiredCount)
        {
            InventorySlotView slot = Instantiate(slotTemplate, slotContent, false);
            slot.name = "InventoryRuntimeSlot_" + runtimeSlots.Count.ToString("00");
            slot.ShowEmpty();
            runtimeSlots.Add(slot);
        }
        slots = runtimeSlots.ToArray();
        for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
        {
            slots[slotIndex].gameObject.SetActive(slotIndex < requiredCount);
        }

        slotGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        slotGrid.constraintCount = GridColumns;
        slotGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        slotGrid.startAxis = GridLayoutGroup.Axis.Horizontal;
        float rowStride = slotGrid.cellSize.y + slotGrid.spacing.y;
        float viewportHeight = slotGrid.padding.vertical + GridVisibleRows * rowStride - slotGrid.spacing.y;
        float contentHeight = slotGrid.padding.vertical + (requiredCount / GridColumns) * rowStride - slotGrid.spacing.y;
        float scrollOffset = slotContent.anchoredPosition.y;
        slotScroll.viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewportHeight);
        slotContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
        LayoutRebuilder.ForceRebuildLayoutImmediate(slotContent);

        bool canScroll = itemCount > GridColumns * GridVisibleRows;
        slotScroll.StopMovement();
        slotScroll.horizontal = false;
        slotScroll.vertical = canScroll;
        slotScroll.enabled = canScroll;
        slotContent.anchoredPosition = new Vector2(0f,
            canScroll ? Mathf.Clamp(scrollOffset, 0f, contentHeight - viewportHeight) : 0f);
        return requiredCount;
    }

    private bool InitializeSlotGrid()
    {
        GameObject parentObject = FindObject("ItemParent");
        slotContent = parentObject != null ? parentObject.transform as RectTransform : null;
        Transform templateTransform = slotContent != null ? slotContent.Find("InventorySlot_02") : null;
        InventorySlotView template = templateTransform != null ? templateTransform.GetComponent<InventorySlotView>() : null;
        RectTransform contentParent = slotContent != null ? slotContent.parent as RectTransform : null;
        if (template == null || contentParent == null)
        {
            Debug.LogWarning("Inventory requires ItemParent/InventorySlot_02 to create its item grid.", this);
            return false;
        }

        slotGrid = slotContent.GetComponent<GridLayoutGroup>();
        if (slotGrid == null)
        {
            slotGrid = slotContent.gameObject.AddComponent<GridLayoutGroup>();
            RectTransform templateRect = template.transform as RectTransform;
            slotGrid.cellSize = templateRect.rect.size;
            slotGrid.spacing = new Vector2(12f, 12f);
        }
        ContentSizeFitter fitter = slotContent.GetComponent<ContentSizeFitter>();
        if (fitter != null) fitter.enabled = false;

        Vector3 topLeft = contentParent.InverseTransformPoint(
            slotContent.TransformPoint(new Vector3(slotContent.rect.xMin, slotContent.rect.yMax, 0f)));
        GameObject viewportObject = new GameObject("InventoryItemsViewport", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewportObject.layer = parentObject.layer;
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        viewport.SetParent(contentParent, false);
        viewport.SetSiblingIndex(slotContent.GetSiblingIndex());
        viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(0f, 1f);
        viewport.anchoredPosition = new Vector2(topLeft.x - contentParent.rect.xMin, topLeft.y - contentParent.rect.yMax);
        viewport.sizeDelta = slotContent.rect.size;
        Image hitArea = viewportObject.GetComponent<Image>();
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;

        slotContent.SetParent(viewport, false);
        slotContent.anchorMin = new Vector2(0f, 1f);
        slotContent.anchorMax = new Vector2(1f, 1f);
        slotContent.pivot = new Vector2(0f, 1f);
        slotContent.anchoredPosition = Vector2.zero;
        slotContent.sizeDelta = new Vector2(0f, viewport.rect.height);
        slotScroll = viewportObject.GetComponent<ScrollRect>();
        slotScroll.viewport = viewport;
        slotScroll.content = slotContent;
        slotScroll.horizontal = false;
        slotScroll.movementType = ScrollRect.MovementType.Clamped;
        slotScroll.scrollSensitivity = Mathf.Max(1f, slotGrid.cellSize.y + slotGrid.spacing.y);

        for (int childIndex = 0; childIndex < slotContent.childCount; childIndex++)
        {
            Transform child = slotContent.GetChild(childIndex);
            const string prefix = "InventorySlot_";
            if (child.name.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(child.name.Substring(prefix.Length), out int slotNumber))
            {
                child.gameObject.SetActive(false);
            }
        }
        slotTemplate = template;
        return true;
    }

    /// <summary>
    /// Resolves the server quality code to an Inspector-assigned slot background.
    /// Missing or unknown quality uses the common style without changing item data.
    /// </summary>
    public Sprite ResolveQualityBackground(string quality)
    {
        string qualityCode = (quality ?? string.Empty).Trim().ToUpperInvariant();
        Sprite background;
        switch (qualityCode)
        {
            case "COMMON": background = _commonBackground; break;
            case "FINE": background = _fineBackground; break;
            case "EXCELLENT": background = _excellentBackground; break;
            case "EPIC": background = _epicBackground; break;
            case "LEGENDARY": background = _legendaryBackground; break;
            default:
                if (_qualityWarnings.Add("quality:" + qualityCode))
                {
                    Debug.LogWarning("Inventory quality is missing or unknown: '" + quality
                        + "'. Using the COMMON background; check the server item configuration.", this);
                }
                background = _commonBackground;
                break;
        }

        if (background == null)
        {
            if (_qualityWarnings.Add("sprite:" + qualityCode))
            {
                Debug.LogWarning("Inventory quality background is not assigned for '" + quality
                    + "'. Check Item Quality Backgrounds on InventoryPanelView.", this);
            }
            return _commonBackground;
        }

        return background;
    }

    public void SetSlotVisible(int index, bool visible)
    {
        InventorySlotView slot = GetSlot(index);
        if (slot != null)
        {
            slot.gameObject.SetActive(visible);
        }
    }

    public void ApplySlotImages(Sprite normalSprite, Sprite selectedSprite)
    {
        if (slots == null)
        {
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
            {
                slots[i].SetSlotImages(normalSprite, selectedSprite);
            }
        }
    }

    public void SetMode(bool serverBacked)
    {
        SetText(titleText, inventoryTitle);
        SetText(subtitleText, inventorySubtitle);
        SetText(gridTitleText, inventoryGridTitle);
        SetText(sidebarHintText, serverBacked ? serverInventoryHint : localInventoryHint);
        SetText(emptyText, emptyCategoryText);
        SetCategoriesVisible(false);
    }

    public void SetSidebarHint(string value)
    {
        SetText(sidebarHintText, value);
    }

    public void SetResourceValue(string key, int value)
    {
        if (resources == null)
        {
            return;
        }

        for (int i = 0; i < resources.Length; i++)
        {
            ResourceControl resource = resources[i];
            if (resource != null && NormalizeKey(resource.Key) == NormalizeKey(key))
            {
                SetText(resource.ValueText, $"{resource.Label}  {Mathf.Max(0, value)}");
                if (string.Equals(NormalizeKey(key), "gold", StringComparison.OrdinalIgnoreCase))
                {
                    SetActive(resource.AddButton, false);
                }
                return;
            }
        }
    }

    public void SetResourceIcon(string key, Sprite sprite)
    {
        if (resources == null)
        {
            return;
        }

        string normalizedKey = NormalizeKey(key);
        if (normalizedKey == "crystal")
        {
            normalizedKey = "diamond";
        }

        for (int i = 0; i < resources.Length; i++)
        {
            ResourceControl resource = resources[i];
            if (resource == null || NormalizeKey(resource.Key) != normalizedKey)
            {
                continue;
            }

            if (resource.Icon != null)
            {
                resource.Icon.sprite = sprite;
                resource.Icon.enabled = sprite != null;
                resource.Icon.preserveAspect = true;
            }
        }
    }

    public Sprite ResolveIcon(string itemKey, string itemName, string iconKey)
    {
        if (icons == null) return null;

        string[] candidates = { iconKey, itemKey, itemName };
        foreach (string value in candidates)
        {
            for (int matchPass = 0; matchPass < 2; matchPass++)
            {
                string candidate = matchPass == 0 ? NormalizeIconLookupKey(value) : NormalizeIconKey(value);
                if (string.IsNullOrEmpty(candidate)) continue;

                foreach (IconControl icon in icons)
                {
                    if (icon != null && icon.Icon != null && NormalizeIconLookupKey(icon.Key) == candidate)
                    {
                        return icon.Icon;
                    }
                }
            }
        }

        return null;
    }

    private static string NormalizeIconKey(string value)
    {
        string normalized = NormalizeIconLookupKey(value);
        switch (normalized)
        {
            case "金币": return "gold";
            case "crystal":
            case "bluecrystal":
            case "钻石":
            case "万能彩墨": return "diamond";
            case "木材": return "wood";
            case "woodplank":
            case "woodboard":
            case "木板": return "plank";
            case "石材": return "stone";
            case "iron":
            case "ironore":
            case "矿石":
            case "铁矿": return "ore";
            case "小麦": return "wheat";
            case "鱼": return "fish";
            case "cookedfish":
            case "roastfish":
            case "roastedfish":
            case "烤鱼": return "grilledfish";
            case "肉": return "meat";
            case "cookedmeat":
            case "roastmeat":
            case "roastedmeat":
            case "烤肉": return "grilledmeat";
            case "food":
            case "面包":
            case "食物": return "bread";
            case "金属": return "metal";
            case "体力": return "stamina";
            case "purification":
            case "净化剂": return "purificationagent";
            case "宝箱":
            case "宝箱（未打开）": return "chest";
            case "lotteryticket":
            case "抽奖券": return "carddrawticket";
            default: return normalized;
        }
    }

    private static string NormalizeIconLookupKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string fileName = System.IO.Path.GetFileNameWithoutExtension(value.Trim().Replace('\\', '/'));
        return NormalizeKey(fileName);
    }

    public void SetCounts(int ownedCount, int visibleCount, int capacity, int unlockedSlots)
    {
        SetText(capacityText, string.Format(capacityFormat, ownedCount, capacity));
        SetText(visibleCountText, string.Format(visibleCountFormat, visibleCount, unlockedSlots));
    }

    public void SetStatus(string message)
    {
        SetText(statusText, message ?? string.Empty);
    }

    public void SetEmptyVisible(bool visible)
    {
        SetActive(emptyText, visible);
    }

    public void ShowResourceDetail(
        string itemName,
        string itemType,
        int quantity,
        string description)
    {
        EnsureResourceDetailPanel();
        SetActive(detailRoot, true);

        SetText(detailNameText, string.IsNullOrEmpty(itemName) ? "资源详情" : itemName);
        SetText(detailTypeText, string.IsNullOrEmpty(itemType) ? "物品" : itemType);
        SetText(detailQuantityText, $"数量  {Mathf.Max(0, quantity)}");
        SetText(detailDescriptionText, string.IsNullOrEmpty(description) ? "服务器物品配置未提供额外说明" : description);
    }

    public void HideResourceDetail()
    {
        SetActive(detailRoot, false);
    }

    public void SetCategoriesVisible(bool visible)
    {
        if (categoryRoot != null)
        {
            categoryRoot.SetActive(visible);
        }
    }

    public void BindChromeActions(
        UnityAction closeAction,
        UnityAction expandAction,
        UnityAction organizeAction,
        UnityAction rechargeAction)
    {
        Bind(backButton, closeAction);
        Bind(closeButton, closeAction);
        Bind(expandButton, expandAction);
        Bind(organizeButton, organizeAction);

        if (resources == null)
        {
            return;
        }

        for (int i = 0; i < resources.Length; i++)
        {
            if (resources[i] != null)
            {
                Bind(resources[i].AddButton, rechargeAction);
            }
        }
    }

    public void BindCategoryActions(string selectedCategory, Action<string> onSelected)
    {
        if (categories == null)
        {
            return;
        }

        for (int i = 0; i < categories.Length; i++)
        {
            CategoryControl control = categories[i];
            if (control == null)
            {
                continue;
            }

            bool selected = control.Id == selectedCategory;
            if (control.Background != null)
            {
                control.Background.color = selected ? categorySelectedColor : categoryNormalColor;
            }

            if (control.Label != null)
            {
                control.Label.color = selected ? categorySelectedTextColor : categoryNormalTextColor;
            }

            SetActive(control.SelectedMarker, selected);
            if (control.Button != null)
            {
                control.Button.onClick.RemoveAllListeners();
                string categoryId = control.Id;
                if (onSelected != null)
                {
                    control.Button.onClick.AddListener(() => onSelected(categoryId));
                }
            }
        }
    }

    public void BindSideActions(
        UnityAction warehouseAction,
        UnityAction storeAction)
    {
        if (sideActions == null)
        {
            return;
        }

        for (int i = 0; i < sideActions.Length; i++)
        {
            SideActionControl control = sideActions[i];
            if (control == null)
            {
                continue;
            }

            UnityAction action = control.Id == "warehouse" ? warehouseAction : storeAction;
            Bind(control.Button, action);
        }
    }

    public void ShowContextMenu(
        Vector2 anchoredPosition,
        string itemName,
        string itemType,
        string primaryLabel,
        string hint,
        UnityAction primaryAction,
        UnityAction sellAction,
        UnityAction dismissAction)
    {
        // The former context menu is intentionally no longer shown. Keep this
        // method as a compatibility bridge for existing inventory callers and
        // bind only the sale action that now lives under the resource detail.
        Bind(contextSellButton, sellAction);
        SetActive(contextSellButton, true);
    }

    public void BindSellAction(UnityAction sellAction)
    {
        Bind(contextSellButton, sellAction);
        SetActive(contextSellButton, sellAction != null);
    }

    public void HideContextMenu()
    {
        if (contextMenu != null)
        {
            contextMenu.gameObject.SetActive(false);
        }

        Bind(contextSellButton, null);
        SetActive(contextSellButton, false);
    }

    private static void Bind(Button button, UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        if (action != null)
        {
            button.onClick.AddListener(action);
        }
    }

    private static void SetText(TMP_Text label, string value)
    {
        if (label != null)
        {
            label.text = value;
        }
    }

    private static void SetActive(Component component, bool active)
    {
        if (component != null)
        {
            component.gameObject.SetActive(active);
        }
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }

#if UNITY_EDITOR
    /// <summary>Assigns serialized quality sprites when creating an inventory prefab.</summary>
    public void EditorSetQualityBackgrounds(Sprite common, Sprite fine, Sprite excellent, Sprite epic, Sprite legendary)
    {
        _commonBackground = common;
        _fineBackground = fine;
        _excellentBackground = excellent;
        _epicBackground = epic;
        _legendaryBackground = legendary;
    }

    public void EditorSetReferences(
        RectTransform panel,
        Button back,
        Button close,
        Button expand,
        Button organize,
        TMP_Text title,
        TMP_Text subtitle,
        TMP_Text sidebarHint,
        TMP_Text gridTitle,
        TMP_Text empty,
        TMP_Text capacity,
        TMP_Text visibleCount,
        TMP_Text status,
        ResourceControl[] resourceControls,
        CategoryControl[] categoryControls,
        InventorySlotView[] inventorySlots,
        SideActionControl[] actions,
        RectTransform itemContextMenu,
        TMP_Text contextName,
        TMP_Text contextType,
        TMP_Text contextHint,
        TMP_Text primaryLabel,
        Button primaryButton,
        Button sellButton,
        Button dismissButton,
        GameObject categoriesRoot,
        IconControl[] iconControls,
        RectTransform resourceDetailRoot,
        TMP_Text resourceDetailName,
        TMP_Text resourceDetailType,
        TMP_Text resourceDetailQuantity,
        TMP_Text resourceDetailDescription,
        TMP_FontAsset panelFont)
    {
        panelRect = panel;
        backButton = back;
        closeButton = close;
        expandButton = expand;
        organizeButton = organize;
        titleText = title;
        subtitleText = subtitle;
        sidebarHintText = sidebarHint;
        gridTitleText = gridTitle;
        emptyText = empty;
        capacityText = capacity;
        visibleCountText = visibleCount;
        statusText = status;
        resources = resourceControls;
        categories = categoryControls;
        categoryRoot = categoriesRoot;
        icons = iconControls;
        slots = inventorySlots;
        sideActions = actions;
        contextMenu = itemContextMenu;
        contextNameText = contextName;
        contextTypeText = contextType;
        contextHintText = contextHint;
        contextPrimaryLabel = primaryLabel;
        contextPrimaryButton = primaryButton;
        contextSellButton = sellButton;
        contextDismissButton = dismissButton;
        detailRoot = resourceDetailRoot;
        detailNameText = resourceDetailName;
        detailTypeText = resourceDetailType;
        detailQuantityText = resourceDetailQuantity;
        detailDescriptionText = resourceDetailDescription;
        fontAsset = panelFont;
    }

    public void EditorSetSchemaVersion(int version)
    {
        schemaVersion = version;
    }
#endif

    private void UpgradeLegacyTextComponents()
    {
        LegacyText[] legacyTexts = GetComponentsInChildren<LegacyText>(true);
        for (int i = 0; i < legacyTexts.Length; i++)
        {
            LegacyText legacy = legacyTexts[i];
            if (legacy == null || legacy.GetComponent<TMP_Text>() != null)
            {
                continue;
            }

            TextMeshProUGUI replacement = legacy.gameObject.AddComponent<TextMeshProUGUI>();
            replacement.text = legacy.text;
            replacement.fontSize = legacy.fontSize;
            replacement.color = legacy.color;
            replacement.raycastTarget = legacy.raycastTarget;
            replacement.alignment = ConvertAlignment(legacy.alignment);
            replacement.font = fontAsset;
            legacy.enabled = false;
        }
    }

    private void ResolveReferences()
    {
        if (categoryRoot == null)
        {
            categoryRoot = FindObject("InventorySidebar");
        }

        titleText = ResolveText(titleText, "InventoryTitle");
        subtitleText = ResolveText(subtitleText, "InventorySubtitle");
        sidebarHintText = ResolveText(sidebarHintText, "InventorySidebarHint");
        gridTitleText = ResolveText(gridTitleText, "InventoryGridTitle");
        emptyText = ResolveText(emptyText, "InventoryEmptyText");
        capacityText = ResolveText(capacityText, "InventoryCapacity");
        visibleCountText = ResolveText(visibleCountText, "InventoryVisibleCount");
        statusText = ResolveText(statusText, "InventoryStatus");
        contextNameText = ResolveText(contextNameText, "Name");
        contextTypeText = ResolveText(contextTypeText, "Type");
        contextHintText = ResolveText(contextHintText, "Hint");
        contextPrimaryLabel = ResolveText(contextPrimaryLabel, "Label");
    }

    private TMP_Text ResolveText(TMP_Text current, string objectName)
    {
        return current != null ? current : FindText(objectName);
    }

    private TMP_Text FindText(string objectName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name != objectName)
            {
                continue;
            }

            TMP_Text text = children[i].GetComponent<TMP_Text>();
            if (text != null)
            {
                return text;
            }
        }

        return null;
    }

    private GameObject FindObject(string objectName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == objectName)
            {
                return children[i].gameObject;
            }
        }

        return null;
    }

    private void EnsureResourceDetailPanel()
    {
        if (detailRoot == null)
        {
            GameObject root = new GameObject("InventoryResourceDetail", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(transform, false);
            detailRoot = root.GetComponent<RectTransform>();
            detailRoot.anchorMin = new Vector2(0f, 1f);
            detailRoot.anchorMax = new Vector2(0f, 1f);
            detailRoot.pivot = new Vector2(0f, 1f);
            detailRoot.anchoredPosition = new Vector2(1340f, -108f);
            detailRoot.sizeDelta = new Vector2(350f, 672f);
            root.GetComponent<Image>().color = new Color(0.30f, 0.15f, 0.05f, 0.96f);
        }

        detailNameText = EnsureDetailText(detailNameText, "InventoryDetailName", "资源详情", 26, TextAlignmentOptions.Left);
        detailTypeText = EnsureDetailText(detailTypeText, "InventoryDetailType", "物品", 17, TextAlignmentOptions.Left);
        detailQuantityText = EnsureDetailText(detailQuantityText, "InventoryDetailQuantity", "数量  0", 19, TextAlignmentOptions.Left);
        detailDescriptionText = EnsureDetailText(detailDescriptionText, "InventoryDetailDescription", "点击物品查看详情", 16, TextAlignmentOptions.TopLeft);
        detailDescriptionText.enableWordWrapping = true;
    }

    private TMP_Text EnsureDetailText(TMP_Text current, string objectName, string value, float size, TextAlignmentOptions alignment)
    {
        if (current != null)
        {
            return current;
        }

        GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        child.transform.SetParent(detailRoot, false);
        TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = fontAsset;
        text.fontSize = size;
        text.color = new Color(1f, 0.90f, 0.66f, 1f);
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static string NormalizeKey(string value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant().Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty);
    }

    private static TextAlignmentOptions ConvertAlignment(TextAnchor alignment)
    {
        switch (alignment)
        {
            case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft: return TextAlignmentOptions.MidlineLeft;
            case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
            case TextAnchor.MiddleRight: return TextAlignmentOptions.MidlineRight;
            case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
            default: return TextAlignmentOptions.Center;
        }
    }
}
