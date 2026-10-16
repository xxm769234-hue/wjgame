using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class InventoryPrefabBuilder
{
    private const string PrefabDirectory = "Assets/Resources/UI";
    private const string PrefabPath = PrefabDirectory + "/InventoryPanel.prefab";
    private const string ImportedPrefabPath = "Assets/_Imported/weijun-game-project/Assets/Resources/UI/InventoryPanel.prefab";
    private const int PrefabSchemaVersion = 5;
    private const int InventoryColumns = InventoryPanelView.GridColumns;
    private const int InventoryCapacity = InventoryColumns * InventoryPanelView.GridVisibleRows;
    private const string InventoryArtDirectory = "Assets/_Project/Art/UI/二级界面/仓库二级界面/";
    private const string FontPath = "Assets/_Imported/weijun-game-project/Assets/Sprites/font/SourceHanSansSC-Normal SDF.asset";
    private const string ResourceIconDirectory = "Assets/_Project/Art/UI/资源图标/";

    private static TMP_FontAsset fontAsset;

    [MenuItem("Tools/WeiJun UI/Rebuild Inventory Prefab")]
    public static void BuildPrefab()
    {
        Directory.CreateDirectory(PrefabDirectory);
        fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        GameObject root = new GameObject("InventoryPanelView", typeof(RectTransform));
        try
        {
            RectTransform rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect);
            InventoryPanelView view = root.AddComponent<InventoryPanelView>();

            Image panelShadow = CreateImage("InventoryPanelShadow", root.transform, new Color(0.08f, 0.04f, 0.01f, 0.50f));
            SetTopLeft(panelShadow.rectTransform, new Vector2(114f, -94f), new Vector2(1730f, 914f));
            panelShadow.raycastTarget = false;

            Image panel = CreateImage("InventoryPanel", root.transform, new Color(0.39f, 0.19f, 0.07f, 1f));
            panel.sprite = LoadInventoryArtSprite("二级界面背景");
            panel.color = Color.white;
            panel.type = Image.Type.Simple;
            SetTopLeft(panel.rectTransform, new Vector2(100f, -78f), new Vector2(1720f, 900f));
            panel.raycastTarget = true;

            BuildPanelBackground(panel.transform);

            TMP_Text titleText = CreateText(
                "InventoryTitle",
                panel.transform,
                "仓库",
                36,
                FontStyle.Bold,
                new Color(1f, 0.88f, 0.47f, 1f),
                TextAnchor.MiddleLeft);
            SetTopLeft(titleText.rectTransform, new Vector2(120f, -28f), new Vector2(170f, 52f));

            TMP_Text subtitleText = CreateText(
                "InventorySubtitle",
                panel.transform,
                "领地物资与道具管理",
                17,
                FontStyle.Bold,
                new Color(1f, 0.89f, 0.67f, 0.88f),
                TextAnchor.MiddleLeft);
            SetTopLeft(subtitleText.rectTransform, new Vector2(298f, -30f), new Vector2(280f, 42f));

            InventoryPanelView.ResourceControl[] resources = new InventoryPanelView.ResourceControl[0];

            Button closeButton = CreateButton(
                panel.transform,
                "InventoryCloseButton",
                string.Empty,
                Color.white,
                Color.white,
                23,
                out _);
            Image closeImage = closeButton.GetComponent<Image>();
            closeImage.sprite = LoadInventoryArtSprite("关闭按钮");
            closeImage.color = Color.white;
            closeImage.type = Image.Type.Simple;
            closeImage.preserveAspect = true;
            SetTopLeft(closeButton.GetComponent<RectTransform>(), new Vector2(1537f, -48f), new Vector2(75f, 77f));

            Image sidebar = CreateImage("InventorySidebar", panel.transform, new Color(0.69f, 0.38f, 0.13f, 0.96f));
            SetTopLeft(sidebar.rectTransform, new Vector2(24f, -108f), new Vector2(218f, 672f));

            InventoryPanelView.CategoryControl[] categories =
            {
                BuildCategoryButton(sidebar.transform, "all", "全部", new Vector2(16f, -18f)),
                BuildCategoryButton(sidebar.transform, "equipment", "装备", new Vector2(16f, -106f)),
                BuildCategoryButton(sidebar.transform, "medicine", "药品", new Vector2(16f, -194f)),
                BuildCategoryButton(sidebar.transform, "other", "其它", new Vector2(16f, -282f))
            };

            Button expandButton = CreateButton(
                sidebar.transform,
                "InventoryExpandButton",
                "扩充格子",
                new Color(0.88f, 0.58f, 0.20f, 1f),
                new Color(0.35f, 0.15f, 0.04f, 1f),
                17,
                out _);
            SetTopLeft(expandButton.GetComponent<RectTransform>(), new Vector2(16f, -458f), new Vector2(178f, 54f));

            TMP_Text sidebarHintText = CreateText(
                "InventorySidebarHint",
                sidebar.transform,
                "账号仓库\n资源由服务器同步",
                16,
                FontStyle.Bold,
                new Color(1f, 0.87f, 0.60f, 0.94f),
                TextAnchor.MiddleCenter);
            SetTopLeft(sidebarHintText.rectTransform, new Vector2(18f, -540f), new Vector2(174f, 48f));

            GameObject itemParent = BuildGridFrame(panel.transform);

            TMP_Text gridTitleText = CreateText(
                "InventoryGridTitle",
                panel.transform,
                "物品栏",
                22,
                FontStyle.Bold,
                new Color(0.42f, 0.19f, 0.05f, 1f),
                TextAnchor.MiddleLeft);
            SetTopLeft(gridTitleText.rectTransform, new Vector2(286f, -120f), new Vector2(260f, 34f));

            InventorySlotView[] slots = BuildSlots(itemParent.transform);

            TMP_Text emptyText = CreateText(
                "InventoryEmptyText",
                panel.transform,
                "当前物品暂无物品",
                22,
                FontStyle.Bold,
                new Color(0.48f, 0.27f, 0.12f, 0.72f),
                TextAnchor.MiddleCenter);
            SetTopLeft(emptyText.rectTransform, new Vector2(510f, -390f), new Vector2(430f, 42f));
            emptyText.gameObject.SetActive(false);

            FooterReferences footer = BuildFooter(panel.transform);

            InventoryPanelView.SideActionControl[] sideActions =
            {
                BuildSideAction(panel.transform, new Vector2(24f, -234f), "warehouse", "仓库", "仓"),
                BuildSideAction(panel.transform, new Vector2(24f, -414f), "store", "商店", "店")
            };

            DetailReferences detail = BuildDetailPanel(panel.transform);
            InventoryPanelView.IconControl[] icons = BuildIconControls();
            sidebar.gameObject.SetActive(false);

            GameObject saleDialog = new GameObject(
                "ResourceSaleDialog",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(ResourceSaleDialogView));
            saleDialog.transform.SetParent(root.transform, false);
            saleDialog.GetComponent<ResourceSaleDialogView>().EnsureBuilt(fontAsset);

            view.EditorSetReferences(
                panel.rectTransform,
                null,
                closeButton,
                expandButton,
                footer.OrganizeButton,
                titleText,
                subtitleText,
                sidebarHintText,
                gridTitleText,
                emptyText,
                footer.CapacityText,
                footer.VisibleCountText,
                footer.StatusText,
                resources,
                categories,
                slots,
                sideActions,
                null,
                null,
                null,
                null,
                null,
                null,
                detail.SellButton,
                null,
                sidebar.gameObject,
                icons,
                detail.Root,
                detail.NameText,
                detail.TypeText,
                detail.QuantityText,
                detail.DescriptionText,
                fontAsset);
            view.EditorSetSchemaVersion(PrefabSchemaVersion);
            view.EditorSetQualityBackgrounds(
                LoadInventoryArtSprite("品质框/格子（白）"),
                LoadInventoryArtSprite("品质框/格子（绿）"),
                LoadInventoryArtSprite("品质框/格子（蓝）"),
                LoadInventoryArtSprite("品质框/格子（紫）"),
                LoadInventoryArtSprite("品质框/格子（金）"));

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
            if (!saved)
            {
                throw new IOException("Failed to save inventory prefab at " + PrefabPath);
            }

            PrefabUtility.SaveAsPrefabAsset(root, ImportedPrefabPath, out bool importedSaved);
            if (!importedSaved)
            {
                throw new IOException("Failed to save inventory prefab at " + ImportedPrefabPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Inventory prefab rebuilt: " + PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void BuildPanelBackground(Transform panel)
    {
        Image header = CreateImage("InventoryHeader", panel, new Color(0.48f, 0.23f, 0.08f, 1f));
        SetTopLeft(header.rectTransform, new Vector2(16f, -14f), new Vector2(1688f, 74f));
        header.raycastTarget = false;

        Image headerLight = CreateImage("InventoryHeaderLight", header.transform, new Color(1f, 0.71f, 0.22f, 0.22f));
        SetTopLeft(headerLight.rectTransform, Vector2.zero, new Vector2(1688f, 9f));
        headerLight.raycastTarget = false;
    }

    private static InventoryPanelView.CategoryControl BuildCategoryButton(
        Transform parent,
        string id,
        string label,
        Vector2 position)
    {
        Button button = CreateButton(
            parent,
            "InventoryCategory_" + id,
            label,
            new Color(0.91f, 0.67f, 0.29f, 1f),
            new Color(0.44f, 0.21f, 0.07f, 1f),
            22,
            out TMP_Text labelText);
        SetTopLeft(button.GetComponent<RectTransform>(), position, new Vector2(178f, 72f));

        Image marker = CreateImage("InventoryCategoryMarker", button.transform, new Color(1f, 0.88f, 0.42f, 1f));
        RectTransform markerRect = marker.rectTransform;
        markerRect.anchorMin = new Vector2(1f, 0.5f);
        markerRect.anchorMax = new Vector2(1f, 0.5f);
        markerRect.pivot = new Vector2(0f, 0.5f);
        markerRect.anchoredPosition = new Vector2(-8f, 0f);
        markerRect.sizeDelta = new Vector2(12f, 40f);
        marker.raycastTarget = false;
        marker.gameObject.SetActive(false);

        return new InventoryPanelView.CategoryControl
        {
            Id = id,
            Button = button,
            Background = button.GetComponent<Image>(),
            Label = labelText,
            SelectedMarker = marker.gameObject
        };
    }

    private static GameObject BuildGridFrame(Transform panel)
    {
        Image gridFrame = CreateImage("InventoryGridFrame", panel, new Color(0.48f, 0.23f, 0.08f, 1f));
        gridFrame.sprite = LoadInventoryArtSprite("资源栏");
        gridFrame.color = Color.white;
        gridFrame.type = Image.Type.Simple;
        SetTopLeft(gridFrame.rectTransform, new Vector2(258f, -108f), new Vector2(1060f, 678f));
        gridFrame.raycastTarget = false;

        Image gridPaper = CreateImage("InventoryGridPaper", gridFrame.transform, new Color(0.98f, 0.79f, 0.43f, 1f));
        SetTopLeft(gridPaper.rectTransform, new Vector2(12f, -12f), new Vector2(1036f, 654f));
        gridPaper.raycastTarget = false;

        GameObject itemParent = CreateRectObject("ItemParent", gridPaper.transform);
        Stretch(itemParent.GetComponent<RectTransform>());
        return itemParent;
    }

    private static InventorySlotView[] BuildSlots(Transform itemParent)
    {
        InventorySlotView[] result = new InventorySlotView[InventoryCapacity];
        Vector2 slotStart = new Vector2(16f, -38f);
        Vector2 slotSize = new Vector2(106f, 106f);

        for (int slotIndex = 0; slotIndex < InventoryCapacity; slotIndex++)
        {
            int row = slotIndex / InventoryColumns;
            int column = slotIndex % InventoryColumns;
            Vector2 position = slotStart + new Vector2(column * 118f, -row * 118f);
            result[slotIndex] = BuildSlot(itemParent, slotIndex, position, slotSize);
            result[slotIndex].gameObject.SetActive(false);
        }

        return result;
    }

    private static InventorySlotView BuildSlot(Transform parent, int slotIndex, Vector2 position, Vector2 size)
    {
        Button button = CreateButton(
            parent,
            "InventorySlot_" + slotIndex.ToString("00"),
            string.Empty,
            Color.white,
            Color.white,
            16,
            out _);
        SetTopLeft(button.GetComponent<RectTransform>(), position, size);
        Image background = button.GetComponent<Image>();
        background.sprite = LoadInventoryArtSprite("品质框/格子（无点击）");
        background.color = Color.white;
        background.type = Image.Type.Simple;

        Image selectedImage = CreateImage("SelectedImage", button.transform, Color.white);
        SetTopLeft(selectedImage.rectTransform, Vector2.zero, size);
        selectedImage.sprite = LoadInventoryArtSprite("格子（点击）");
        selectedImage.color = Color.white;
        selectedImage.type = Image.Type.Simple;
        selectedImage.raycastTarget = false;
        selectedImage.gameObject.SetActive(false);

        Image itemIcon = CreateImage("ItemIcon", button.transform, Color.white);
        SetTopLeft(itemIcon.rectTransform, new Vector2(19f, -12f), new Vector2(68f, 68f));
        itemIcon.preserveAspect = true;
        itemIcon.raycastTarget = false;

        TMP_Text quantityText = CreateText(
            "Quantity",
            button.transform,
            "0",
            16,
            FontStyle.Bold,
            new Color(0.35f, 0.15f, 0.04f, 1f),
            TextAnchor.MiddleRight);
        SetTopLeft(quantityText.rectTransform, new Vector2(54f, -84f), new Vector2(42f, 22f));

        InventorySlotView slotView = button.gameObject.AddComponent<InventorySlotView>();
        slotView.EditorSetReferences(
            button,
            background,
            null,
            itemIcon,
            null,
            null,
            quantityText,
            selectedImage,
            null);
        slotView.ShowEmpty();
        return slotView;
    }

    private static FooterReferences BuildFooter(Transform panel)
    {
        Button organizeButton = CreateButton(
            panel,
            "InventoryOrganizeButton",
            "一键整理",
            new Color(0.89f, 0.58f, 0.18f, 1f),
            new Color(0.36f, 0.16f, 0.04f, 1f),
            18,
            out _);
        SetTopLeft(organizeButton.GetComponent<RectTransform>(), new Vector2(1130f, -800f), new Vector2(164f, 44f));

        return new FooterReferences
        {
            CapacityText = null,
            VisibleCountText = null,
            StatusText = null,
            OrganizeButton = organizeButton
        };
    }

    private static InventoryPanelView.SideActionControl BuildSideAction(
        Transform parent,
        Vector2 position,
        string id,
        string label,
        string iconText)
    {
        Button button = CreateButton(
            parent,
            "InventorySideAction_" + id,
            string.Empty,
            new Color(0.69f, 0.38f, 0.14f, 1f),
            Color.white,
            20,
            out _);
        SetTopLeft(button.GetComponent<RectTransform>(), position, new Vector2(154f, 126f));

        TMP_Text icon = CreateText(
            "Icon",
            button.transform,
            iconText,
            34,
            FontStyle.Bold,
            new Color(1f, 0.91f, 0.66f, 1f),
            TextAnchor.MiddleCenter);
        SetTopLeft(icon.rectTransform, new Vector2(40f, -12f), new Vector2(74f, 54f));

        TMP_Text labelText = CreateText("Label", button.transform, label, 20, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        SetTopLeft(labelText.rectTransform, new Vector2(12f, -82f), new Vector2(130f, 30f));

        return new InventoryPanelView.SideActionControl
        {
            Id = id,
            Button = button,
            Background = button.GetComponent<Image>(),
            Label = labelText
        };
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        target.transform.SetParent(parent, false);
        Image image = target.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static Button CreateButton(
        Transform parent,
        string name,
        string text,
        Color backgroundColor,
        Color textColor,
        int fontSize,
        out TMP_Text label)
    {
        Image background = CreateImage(name, parent, backgroundColor);
        background.raycastTarget = true;

        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.94f, 0.82f, 1f);
        colors.pressedColor = new Color(0.82f, 0.78f, 0.70f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.70f, 0.70f, 0.70f, 0.65f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        label = null;
        if (!string.IsNullOrEmpty(text))
        {
            label = CreateText("Text", background.transform, text, fontSize, FontStyle.Bold, textColor, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
        }

        return button;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        string value,
        int fontSize,
        FontStyle style,
        Color color,
        TextAnchor alignment)
    {
        GameObject target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        target.transform.SetParent(parent, false);

        TextMeshProUGUI label = target.GetComponent<TextMeshProUGUI>();
        label.font = fontAsset;
        label.text = value;
        label.fontSize = fontSize;
        label.fontStyle = style == FontStyle.Bold ? FontStyles.Bold : FontStyles.Normal;
        label.color = color;
        label.alignment = ConvertAlignment(alignment);
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Min(12, fontSize);
        label.fontSizeMax = fontSize;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Truncate;
        label.raycastTarget = false;
        return label;
    }

    private static DetailReferences BuildDetailPanel(Transform parent)
    {
        Image background = CreateImage("InventoryResourceDetail", parent, new Color(0.30f, 0.15f, 0.05f, 0.96f));
        background.sprite = LoadInventoryArtSprite("资源栏");
        background.color = Color.white;
        background.type = Image.Type.Simple;
        SetTopLeft(background.rectTransform, new Vector2(1225f, -108f), new Vector2(300f, 672f));

        TMP_Text name = CreateText("InventoryDetailName", background.transform, "资源详情", 25, FontStyle.Bold, new Color(1f, 0.90f, 0.66f, 1f), TextAnchor.MiddleLeft);
        TMP_Text type = CreateText("InventoryDetailType", background.transform, "物品", 16, FontStyle.Normal, new Color(1f, 0.78f, 0.45f, 1f), TextAnchor.MiddleLeft);
        TMP_Text quantity = CreateText("InventoryDetailQuantity", background.transform, "数量  0", 19, FontStyle.Bold, new Color(1f, 0.90f, 0.66f, 1f), TextAnchor.MiddleLeft);
        TMP_Text description = CreateText("InventoryDetailDescription", background.transform, "点击物品查看详情", 15, FontStyle.Normal, new Color(1f, 0.84f, 0.60f, 0.94f), TextAnchor.UpperLeft);
        description.enableWordWrapping = true;
        Button sell = CreateButton(
            background.transform,
            "SellAction",
            "出售",
            new Color(0.91f, 0.37f, 0.13f, 1f),
            Color.white,
            16,
            out _);
        SetTopLeft(sell.GetComponent<RectTransform>(), new Vector2(32f, -540f), new Vector2(236f, 52f));

        return new DetailReferences
        {
            Root = background.rectTransform,
            NameText = name,
            TypeText = type,
            QuantityText = quantity,
            DescriptionText = description,
            SellButton = sell
        };
    }


    private static InventoryPanelView.IconControl[] BuildIconControls()
    {
        return new[]
        {
            BuildIconControl("gold", "金币"),
            BuildIconControl("diamond", "万能彩墨"),
            BuildIconControl("wood", "木材"),
            BuildIconControl("stone", "石材"),
            BuildIconControl("iron", "矿石"),
            BuildIconControl("ore", "矿石"),
            BuildIconControl("wheat", "小麦"),
            BuildIconControl("fish", "鱼"),
            BuildIconControl("meat", "肉"),
            BuildIconControl("bread", "面包"),
            BuildIconControl("food", "面包"),
            BuildIconControl("metal", "金属"),
            BuildIconControl("stamina", "体力"),
            BuildIconControl("purificationagent", "净化剂"),
            BuildIconControl("chest", "宝箱（未打开）"),
            BuildIconControl("plank", "木板"),
            BuildIconControl("grilledfish", "烤鱼"),
            BuildIconControl("grilledmeat", "烤肉"),
            BuildIconControl("carddrawticket", "抽奖券")
        };
    }

    private static InventoryPanelView.IconControl BuildIconControl(string key, string assetName)
    {
        return new InventoryPanelView.IconControl
        {
            Key = key,
            Icon = LoadResourceIcon(assetName)
        };
    }

    private static Sprite LoadResourceIcon(string assetName)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(ResourceIconDirectory + assetName + ".png");
    }

    private static Sprite LoadInventoryArtSprite(string assetName)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(InventoryArtDirectory + assetName + ".png");
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

    private static GameObject CreateRectObject(string name, Transform parent)
    {
        GameObject target = new GameObject(name, typeof(RectTransform));
        target.transform.SetParent(parent, false);
        return target;
    }

    private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private sealed class FooterReferences
    {
        public TMP_Text CapacityText;
        public TMP_Text VisibleCountText;
        public TMP_Text StatusText;
        public Button OrganizeButton;
    }

    private sealed class DetailReferences
    {
        public RectTransform Root;
        public TMP_Text NameText;
        public TMP_Text TypeText;
        public TMP_Text QuantityText;
        public TMP_Text DescriptionText;
        public Button SellButton;
    }
}
