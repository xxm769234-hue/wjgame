using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class InventoryPanelView : MonoBehaviour
{
    [Serializable]
    public sealed class ResourceControl
    {
        public string Key;
        public string Label;
        public Text ValueText;
        public Button AddButton;
    }

    [Serializable]
    public sealed class CategoryControl
    {
        public string Id;
        public Button Button;
        public Image Background;
        public Text Label;
        public GameObject SelectedMarker;
    }

    [Serializable]
    public sealed class SideActionControl
    {
        public string Id;
        public Button Button;
        public Image Background;
        public Text Label;
    }

    [Header("Panel Components")]
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private Button backButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button expandButton;
    [SerializeField] private Button organizeButton;
    [SerializeField] private Text titleText;
    [SerializeField] private Text subtitleText;
    [SerializeField] private Text sidebarHintText;
    [SerializeField] private Text gridTitleText;
    [SerializeField] private Text emptyText;
    [SerializeField] private Text capacityText;
    [SerializeField] private Text visibleCountText;
    [SerializeField] private Text statusText;

    [Header("Reusable Controls")]
    [SerializeField] private ResourceControl[] resources = new ResourceControl[0];
    [SerializeField] private CategoryControl[] categories = new CategoryControl[0];
    [SerializeField] private InventorySlotView[] slots = new InventorySlotView[0];
    [SerializeField] private SideActionControl[] sideActions = new SideActionControl[0];

    [Header("Item Action Menu")]
    [SerializeField] private RectTransform contextMenu;
    [SerializeField] private Text contextNameText;
    [SerializeField] private Text contextTypeText;
    [SerializeField] private Text contextHintText;
    [SerializeField] private Text contextPrimaryLabel;
    [SerializeField] private Button contextPrimaryButton;
    [SerializeField] private Button contextSellButton;
    [SerializeField] private Button contextDismissButton;

    [Header("Editable Copy")]
    [SerializeField] private string inventoryTitle = "储物";
    [SerializeField] private string stallTitle = "摆摊";
    [SerializeField] private string inventorySubtitle = "领地物资与道具管理";
    [SerializeField] private string stallSubtitle = "选择物品进行上架或出售";
    [SerializeField] private string inventoryGridTitle = "物品栏";
    [SerializeField] private string stallGridTitle = "选择要上架的物品";
    [SerializeField] private string localInventoryHint = "本地库存\n可使用与出售";
    [SerializeField] private string serverInventoryHint = "服务器库存\n仅支持查看";
    [SerializeField] private string emptyCategoryText = "当前分类暂无物品";
    [SerializeField] private string capacityFormat = "储物袋空间：{0:00}/{1}";
    [SerializeField] private string visibleCountFormat = "当前分类 {0} 件  已开放 {1} 格";

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

    public InventorySlotView GetSlot(int index)
    {
        return slots != null && index >= 0 && index < slots.Length ? slots[index] : null;
    }

    public void SetMode(bool stallMode, bool serverBacked)
    {
        SetText(titleText, stallMode ? stallTitle : inventoryTitle);
        SetText(subtitleText, stallMode ? stallSubtitle : inventorySubtitle);
        SetText(gridTitleText, stallMode ? stallGridTitle : inventoryGridTitle);
        SetText(sidebarHintText, serverBacked ? serverInventoryHint : localInventoryHint);
        SetText(emptyText, emptyCategoryText);
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
            if (resource != null && resource.Key == key)
            {
                SetText(resource.ValueText, $"{resource.Label}  {Mathf.Max(0, value)}");
                return;
            }
        }
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
        bool stallMode,
        UnityAction warehouseAction,
        UnityAction stallAction,
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

            bool selected = control.Id == "warehouse" && !stallMode || control.Id == "stall" && stallMode;
            if (control.Background != null)
            {
                control.Background.color = selected ? sideSelectedColor : sideNormalColor;
            }

            if (control.Label != null)
            {
                control.Label.color = selected ? sideSelectedTextColor : sideNormalTextColor;
            }

            UnityAction action = control.Id == "warehouse"
                ? warehouseAction
                : control.Id == "stall"
                    ? stallAction
                    : storeAction;
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

    private static void SetText(Text label, string value)
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
        Text title,
        Text subtitle,
        Text sidebarHint,
        Text gridTitle,
        Text empty,
        Text capacity,
        Text visibleCount,
        Text status,
        ResourceControl[] resourceControls,
        CategoryControl[] categoryControls,
        InventorySlotView[] inventorySlots,
        SideActionControl[] actions,
        RectTransform itemContextMenu,
        Text contextName,
        Text contextType,
        Text contextHint,
        Text primaryLabel,
        Button primaryButton,
        Button sellButton,
        Button dismissButton)
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
    }
#endif
}
