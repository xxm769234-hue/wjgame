using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using LegacyText = UnityEngine.UI.Text;

[DisallowMultipleComponent]
public sealed class InventoryPanelView : MonoBehaviour
{
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
    [SerializeField] private Image detailIcon;
    [SerializeField] private TMP_Text detailNameText;
    [SerializeField] private TMP_Text detailTypeText;
    [SerializeField] private TMP_Text detailQuantityText;
    [SerializeField] private TMP_Text detailDescriptionText;
    [SerializeField] private TMP_FontAsset fontAsset;

    [Header("Editable Copy")]
    [SerializeField] private string inventoryTitle = "背包";
    [SerializeField] private string inventorySubtitle = "领地物资与道具管理";
    [SerializeField] private string inventoryGridTitle = "物品栏";
    [SerializeField] private string localInventoryHint = "本地奖励库存\n仅用于本地任务奖励";
    [SerializeField] private string serverInventoryHint = "服务器库存\n按正式报价出售";
    [SerializeField] private string emptyCategoryText = "当前物品暂无物品";
    [SerializeField] private string capacityFormat = "背包空间：{0:00}/{1}";
    [SerializeField] private string visibleCountFormat = "当前物品 {0} 件  已开放 {1} 格";

    [Header("Category Style")]
    [SerializeField] private Color categoryNormalColor = new Color(0.91f, 0.67f, 0.29f, 1f);
    [SerializeField] private Color categorySelectedColor = new Color(0.92f, 0.46f, 0.16f, 1f);
    [SerializeField] private Color categoryNormalTextColor = new Color(0.44f, 0.21f, 0.07f, 1f);
    [SerializeField] private Color categorySelectedTextColor = Color.white;

    [Header("Side Action Style")]
    [SerializeField] private Color sideNormalColor = new Color(0.69f, 0.38f, 0.14f, 1f);
    [SerializeField] private Color sideSelectedColor = new Color(0.91f, 0.49f, 0.16f, 1f);
    [SerializeField] private Color sideNormalTextColor = new Color(1f, 0.91f, 0.66f, 1f);
    [SerializeField] private Color sideSelectedTextColor = Color.white;

    public int SlotCount => slots == null ? 0 : slots.Length;
    public int SchemaVersion => schemaVersion;

    private void Awake()
    {
        if (fontAsset == null)
        {
            fontAsset = RuntimeFontAssetProvider.Resolve();
        }
        UpgradeLegacyTextComponents();
        ResolveReferences();
        SetCategoriesVisible(false);
        EnsureResourceDetailPanel();
    }

    public InventorySlotView GetSlot(int index)
    {
        return slots != null && index >= 0 && index < slots.Length ? slots[index] : null;
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
        string[] candidates = { itemKey, iconKey, itemName };
        for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
        {
            string candidate = NormalizeIconKey(candidates[candidateIndex]);
            if (string.IsNullOrEmpty(candidate))
            {
                continue;
            }

            if (icons == null)
            {
                continue;
            }

            for (int i = 0; i < icons.Length; i++)
            {
                IconControl icon = icons[i];
                if (icon != null && NormalizeKey(icon.Key) == candidate && icon.Icon != null)
                {
                    return icon.Icon;
                }
            }
        }

        return null;
    }

    private static string NormalizeIconKey(string value)
    {
        string normalized = NormalizeKey(value);
        if (string.IsNullOrEmpty(normalized))
        {
            return string.Empty;
        }

        if (normalized.Contains("gold") || normalized.Contains("金币")) return "gold";
        if (normalized.Contains("diamond") || normalized.Contains("crystal") || normalized.Contains("bluecrystal") || normalized.Contains("钻石") || normalized.Contains("万能彩墨")) return "diamond";
        if (normalized.Contains("wood") || normalized.Contains("木材")) return "wood";
        if (normalized.Contains("stone") || normalized.Contains("石材")) return "stone";
        if (normalized.Contains("iron") || normalized.Contains("ore") || normalized.Contains("矿石") || normalized.Contains("铁矿")) return "ore";
        if (normalized.Contains("wheat") || normalized.Contains("小麦")) return "wheat";
        if (normalized.Contains("fish") || normalized.Contains("鱼")) return "fish";
        if (normalized.Contains("meat") || normalized.Contains("肉")) return "meat";
        if (normalized.Contains("bread") || normalized.Contains("food") || normalized.Contains("面包") || normalized.Contains("食物")) return "bread";
        if (normalized.Contains("metal") || normalized.Contains("金属")) return "metal";
        if (normalized.Contains("stamina") || normalized.Contains("体力")) return "stamina";
        if (normalized.Contains("purification") || normalized.Contains("净化剂")) return "purificationagent";
        if (normalized.Contains("chest") || normalized.Contains("宝箱")) return "chest";
        return normalized;
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
        Sprite sprite,
        string itemName,
        string itemType,
        int quantity,
        string description)
    {
        EnsureResourceDetailPanel();
        SetActive(detailRoot, true);
        if (detailIcon != null)
        {
            detailIcon.sprite = sprite;
            detailIcon.enabled = sprite != null;
            detailIcon.preserveAspect = true;
        }

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

            bool selected = control.Id == "warehouse";
            if (control.Background != null)
            {
                control.Background.color = selected ? sideSelectedColor : sideNormalColor;
            }

            if (control.Label != null)
            {
                control.Label.color = selected ? sideSelectedTextColor : sideNormalTextColor;
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
        if (contextMenu == null)
        {
            return;
        }

        contextMenu.gameObject.SetActive(true);
        contextMenu.anchoredPosition = anchoredPosition;
        contextMenu.SetAsLastSibling();
        SetText(contextNameText, string.IsNullOrEmpty(itemName) ? "物品" : itemName);
        SetText(contextTypeText, itemType ?? string.Empty);
        SetText(contextHintText, hint ?? string.Empty);
        SetText(contextPrimaryLabel, primaryLabel ?? string.Empty);
        Bind(contextPrimaryButton, primaryAction);
        Bind(contextSellButton, sellAction);
        Bind(contextDismissButton, dismissAction);
    }

    public void HideContextMenu()
    {
        if (contextMenu != null)
        {
            contextMenu.gameObject.SetActive(false);
        }
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
        Image resourceDetailIcon,
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
        detailIcon = resourceDetailIcon;
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

        if (detailIcon == null)
        {
            detailIcon = CreateDetailImage("InventoryDetailIcon", new Vector2(35f, -32f), new Vector2(150f, 150f));
        }

        detailNameText = EnsureDetailText(detailNameText, "InventoryDetailName", "资源详情", 26, new Vector2(32f, -202f), new Vector2(286f, 42f), TextAlignmentOptions.Left);
        detailTypeText = EnsureDetailText(detailTypeText, "InventoryDetailType", "物品", 17, new Vector2(32f, -250f), new Vector2(286f, 30f), TextAlignmentOptions.Left);
        detailQuantityText = EnsureDetailText(detailQuantityText, "InventoryDetailQuantity", "数量  0", 19, new Vector2(32f, -294f), new Vector2(286f, 34f), TextAlignmentOptions.Left);
        detailDescriptionText = EnsureDetailText(detailDescriptionText, "InventoryDetailDescription", "点击物品查看详情", 16, new Vector2(32f, -350f), new Vector2(286f, 220f), TextAlignmentOptions.TopLeft);
        detailDescriptionText.enableWordWrapping = true;
    }

    private Image CreateDetailImage(string objectName, Vector2 position, Vector2 size)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        child.transform.SetParent(detailRoot, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = child.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private TMP_Text EnsureDetailText(TMP_Text current, string objectName, string value, float size, Vector2 position, Vector2 dimensions, TextAlignmentOptions alignment)
    {
        if (current != null)
        {
            return current;
        }

        GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        child.transform.SetParent(detailRoot, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
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
