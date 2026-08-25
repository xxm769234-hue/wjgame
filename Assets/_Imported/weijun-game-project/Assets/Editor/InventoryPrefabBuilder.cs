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
    private const int PrefabSchemaVersion = 3;
    private const int InventoryCapacity = 40;
    private const int InventoryColumns = 8;
    private const string FontPath = "Assets/_Imported/weijun-game-project/Assets/Sprites/font/平方重(PingFang Heavy) SDF.asset";
    private const string ResourceIconDirectory = "Assets/_Imported/weijun-game-project/Assets/UI/资源图标/";

    private static Font defaultFont;
    private static TMP_FontAsset fontAsset;

    [InitializeOnLoadMethod]
    private static void BuildMissingPrefabAfterReload()
    {
        EditorApplication.delayCall += () =>
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            InventoryPanelView view = prefab != null ? prefab.GetComponent<InventoryPanelView>() : null;
            if (view == null || view.SchemaVersion != PrefabSchemaVersion)
            {
                BuildPrefab();
            }
        };
    }

    [MenuItem("Tools/WeiJun UI/Rebuild Inventory Prefab")]
    public static void BuildPrefab()
    {
        Directory.CreateDirectory(PrefabDirectory);
        fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        defaultFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Imported/weijun-game-project/Assets/Sprites/font/平方重(PingFang Heavy).ttf")
            ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
            SetTopLeft(panel.rectTransform, new Vector2(100f, -78f), new Vector2(1720f, 900f));
            panel.raycastTarget = true;

            BuildPanelBackground(panel.transform);

            Button backButton = CreateButton(
                panel.transform,
                "InventoryBackButton",
                "<",
                new Color(0.82f, 0.53f, 0.18f, 1f),
                new Color(1f, 0.93f, 0.70f, 1f),
                30,
                out _);
            SetTopLeft(backButton.GetComponent<RectTransform>(), new Vector2(32f, -27f), new Vector2(72f, 48f));

            TMP_Text titleText = CreateText(
                "InventoryTitle",
                panel.transform,
                "背包",
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

            InventoryPanelView.ResourceControl[] resources =
            {
                BuildResourceBadge(panel.transform, new Vector2(620f, -24f), "金币", "Gold", new Color(1f, 0.74f, 0.14f, 1f)),
                BuildResourceBadge(panel.transform, new Vector2(900f, -24f), "钻石", "Diamond", new Color(0.26f, 0.70f, 1f, 1f))
            };

            Button closeButton = CreateButton(
                panel.transform,
                "InventoryCloseButton",
                "X",
                new Color(0.72f, 0.22f, 0.12f, 1f),
                Color.white,
                23,
                out _);
            SetTopLeft(closeButton.GetComponent<RectTransform>(), new Vector2(1636f, -26f), new Vector2(48f, 48f));

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

            BuildGridFrame(panel.transform);

            TMP_Text gridTitleText = CreateText(
                "InventoryGridTitle",
                panel.transform,
                "物品栏",
                22,
                FontStyle.Bold,
                new Color(0.42f, 0.19f, 0.05f, 1f),
                TextAnchor.MiddleLeft);
            SetTopLeft(gridTitleText.rectTransform, new Vector2(286f, -120f), new Vector2(260f, 34f));

            InventorySlotView[] slots = BuildSlots(panel.transform);

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
                BuildSideAction(panel.transform, new Vector2(1450f, -234f), "warehouse", "仓库", "仓"),
                BuildSideAction(panel.transform, new Vector2(1450f, -414f), "store", "商店", "店")
            };

            ContextMenuReferences context = BuildContextMenu(panel.transform);
            DetailReferences detail = BuildDetailPanel(panel.transform);
            InventoryPanelView.IconControl[] icons = BuildIconControls();
            sidebar.gameObject.SetActive(false);

            view.EditorSetReferences(
                panel.rectTransform,
                backButton,
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
                context.Root,
                context.NameText,
                context.TypeText,
                context.HintText,
                context.PrimaryLabel,
                context.PrimaryButton,
                context.SellButton,
                context.DismissButton,
                sidebar.gameObject,
                icons,
                detail.Root,
                detail.Icon,
                detail.NameText,
                detail.TypeText,
                detail.QuantityText,
                detail.DescriptionText,
                fontAsset);
            view.EditorSetSchemaVersion(PrefabSchemaVersion);

            context.Root.gameObject.SetActive(false);
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
        Image paper = CreateImage("InventoryPaper", panel, new Color(0.92f, 0.67f, 0.31f, 1f));
        SetTopLeft(paper.rectTransform, new Vector2(16f, -86f), new Vector2(1688f, 792f));
        paper.raycastTarget = false;

        Image header = CreateImage("InventoryHeader", panel, new Color(0.48f, 0.23f, 0.08f, 1f));
        SetTopLeft(header.rectTransform, new Vector2(16f, -14f), new Vector2(1688f, 74f));
        header.raycastTarget = false;

        Image headerLight = CreateImage("InventoryHeaderLight", header.transform, new Color(1f, 0.71f, 0.22f, 0.22f));
        SetTopLeft(headerLight.rectTransform, Vector2.zero, new Vector2(1688f, 9f));
        headerLight.raycastTarget = false;
    }

    private static InventoryPanelView.ResourceControl BuildResourceBadge(
        Transform parent,
        Vector2 position,
        string label,
        string key,
        Color iconColor)
    {
        Image shadow = CreateImage("InventoryResourceShadow_" + key, parent, new Color(0.10f, 0.04f, 0.01f, 0.30f));
        SetTopLeft(shadow.rectTransform, position + new Vector2(4f, -4f), new Vector2(244f, 50f));
        shadow.raycastTarget = false;

        Image badge = CreateImage("InventoryResourceBadge_" + key, parent, new Color(0.28f, 0.13f, 0.05f, 0.94f));
        SetTopLeft(badge.rectTransform, position, new Vector2(244f, 50f));

        Image gem = CreateImage("Icon", badge.transform, Color.white);
        SetTopLeft(gem.rectTransform, new Vector2(14f, -13f), new Vector2(24f, 24f));
        gem.sprite = LoadResourceIcon(key == "Gold" ? "金币" : "万能彩墨");
        if (gem.sprite == null)
        {
            gem.color = iconColor;
            gem.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
        }
        gem.preserveAspect = true;
        gem.raycastTarget = false;

        TMP_Text valueText = CreateText(
            "InventoryResourceText_" + key,
            badge.transform,
            label + "  0",
            18,
            FontStyle.Bold,
            new Color(1f, 0.89f, 0.64f, 1f),
            TextAnchor.MiddleLeft);
        SetTopLeft(valueText.rectTransform, new Vector2(50f, -8f), new Vector2(144f, 34f));

        Button addButton = null;
        if (key != "Gold")
        {
            addButton = CreateButton(
                badge.transform,
                "InventoryResourcePlus_" + key,
                "+",
                new Color(0.78f, 0.52f, 0.16f, 1f),
                Color.white,
                22,
                out _);
            SetTopLeft(addButton.GetComponent<RectTransform>(), new Vector2(207f, -8f), new Vector2(34f, 34f));
        }

        return new InventoryPanelView.ResourceControl
        {
            Key = key,
            Label = label,
            Icon = gem,
            ValueText = valueText,
            AddButton = addButton
        };
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

    private static void BuildGridFrame(Transform panel)
    {
        Image gridFrame = CreateImage("InventoryGridFrame", panel, new Color(0.48f, 0.23f, 0.08f, 1f));
        SetTopLeft(gridFrame.rectTransform, new Vector2(258f, -108f), new Vector2(1060f, 678f));
        gridFrame.raycastTarget = false;

        Image gridPaper = CreateImage("InventoryGridPaper", gridFrame.transform, new Color(0.98f, 0.79f, 0.43f, 1f));
        SetTopLeft(gridPaper.rectTransform, new Vector2(12f, -12f), new Vector2(1036f, 654f));
        gridPaper.raycastTarget = false;
    }

    private static InventorySlotView[] BuildSlots(Transform panel)
    {
        InventorySlotView[] result = new InventorySlotView[InventoryCapacity];
        Vector2 slotStart = new Vector2(286f, -158f);
        Vector2 slotSize = new Vector2(106f, 106f);

        for (int slotIndex = 0; slotIndex < InventoryCapacity; slotIndex++)
        {
            int row = slotIndex / InventoryColumns;
            int column = slotIndex % InventoryColumns;
            Vector2 position = slotStart + new Vector2(column * 118f, -row * 118f);
            result[slotIndex] = BuildSlot(panel, slotIndex, position, slotSize);
        }

        return result;
    }

    private static InventorySlotView BuildSlot(Transform parent, int slotIndex, Vector2 position, Vector2 size)
    {
        Image shadow = CreateImage("InventorySlotShadow_" + slotIndex.ToString("00"), parent, new Color(0.22f, 0.10f, 0.03f, 0.35f));
        SetTopLeft(shadow.rectTransform, position + new Vector2(5f, -6f), size);
        shadow.raycastTarget = false;

        Button button = CreateButton(
            parent,
            "InventorySlot_" + slotIndex.ToString("00"),
            string.Empty,
            new Color(0.92f, 0.68f, 0.34f, 1f),
            Color.white,
            16,
            out _);
        SetTopLeft(button.GetComponent<RectTransform>(), position, size);
        Image background = button.GetComponent<Image>();

        Image inner = CreateImage("Inner", button.transform, new Color(1f, 0.86f, 0.54f, 0.68f));
        SetTopLeft(inner.rectTransform, new Vector2(6f, -6f), size - new Vector2(12f, 12f));
        inner.raycastTarget = false;

        Image itemIcon = CreateImage("ItemIcon", button.transform, Color.white);
        SetTopLeft(itemIcon.rectTransform, new Vector2(19f, -12f), new Vector2(68f, 68f));
        itemIcon.preserveAspect = true;
        itemIcon.raycastTarget = false;

        Image fallbackBackground = CreateImage("FallbackBackground", button.transform, new Color(0.80f, 0.38f, 0.14f, 1f));
        SetTopLeft(fallbackBackground.rectTransform, new Vector2(32f, -25f), new Vector2(42f, 42f));
        fallbackBackground.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
        fallbackBackground.raycastTarget = false;

        TMP_Text fallbackText = CreateText("FallbackText", button.transform, "物", 22, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        SetTopLeft(fallbackText.rectTransform, new Vector2(19f, -12f), new Vector2(68f, 68f));

        TMP_Text quantityText = CreateText(
            "Quantity",
            button.transform,
            "0",
            16,
            FontStyle.Bold,
            new Color(0.35f, 0.15f, 0.04f, 1f),
            TextAnchor.MiddleRight);
        SetTopLeft(quantityText.rectTransform, new Vector2(54f, -84f), new Vector2(42f, 22f));

        Image selectedFrame = CreateImage("SelectedFrame", button.transform, new Color(0.25f, 0.72f, 0.18f, 0.80f));
        SetTopLeft(selectedFrame.rectTransform, Vector2.zero, new Vector2(8f, size.y));
        selectedFrame.raycastTarget = false;

        GameObject lockRoot = CreateRectObject("Lock", button.transform);
        Stretch(lockRoot.GetComponent<RectTransform>());
        BuildLockMark(lockRoot.transform);

        InventorySlotView slotView = button.gameObject.AddComponent<InventorySlotView>();
        slotView.EditorSetReferences(
            button,
            background,
            inner,
            itemIcon,
            fallbackBackground,
            fallbackText,
            quantityText,
            selectedFrame.gameObject,
            lockRoot);
        slotView.ShowEmpty();
        return slotView;
    }

    private static void BuildLockMark(Transform parent)
    {
        Image body = CreateImage("Body", parent, new Color(0.42f, 0.43f, 0.42f, 0.95f));
        SetTopLeft(body.rectTransform, new Vector2(36f, -41f), new Vector2(34f, 27f));
        body.raycastTarget = false;

        Image shackle = CreateImage("Shackle", parent, new Color(0.52f, 0.53f, 0.52f, 0.95f));
        SetTopLeft(shackle.rectTransform, new Vector2(43f, -28f), new Vector2(20f, 21f));
        shackle.raycastTarget = false;

        TMP_Text label = CreateText("Label", parent, "锁", 15, FontStyle.Bold, new Color(0.86f, 0.87f, 0.84f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(label.rectTransform, new Vector2(36f, -48f), new Vector2(34f, 28f));
    }

    private static FooterReferences BuildFooter(Transform panel)
    {
        Image footer = CreateImage("InventoryFooter", panel, new Color(0.50f, 0.24f, 0.08f, 1f));
        SetTopLeft(footer.rectTransform, new Vector2(258f, -800f), new Vector2(1060f, 64f));

        TMP_Text capacityText = CreateText(
            "InventoryCapacity",
            footer.transform,
            "背包空间：00/40",
            18,
            FontStyle.Bold,
            new Color(1f, 0.86f, 0.48f, 1f),
            TextAnchor.MiddleLeft);
        SetTopLeft(capacityText.rectTransform, new Vector2(20f, -8f), new Vector2(290f, 30f));

        TMP_Text visibleCountText = CreateText(
            "InventoryVisibleCount",
            footer.transform,
            "当前分类 0 件  已开放 32 格",
            14,
            FontStyle.Normal,
            new Color(1f, 0.78f, 0.48f, 0.90f),
            TextAnchor.MiddleLeft);
        SetTopLeft(visibleCountText.rectTransform, new Vector2(20f, -36f), new Vector2(320f, 22f));

        TMP_Text statusText = CreateText(
            "InventoryStatus",
            footer.transform,
            string.Empty,
            14,
            FontStyle.Normal,
            new Color(1f, 0.88f, 0.62f, 0.94f),
            TextAnchor.MiddleCenter);
        SetTopLeft(statusText.rectTransform, new Vector2(340f, -9f), new Vector2(510f, 46f));

        Button organizeButton = CreateButton(
            footer.transform,
            "InventoryOrganizeButton",
            "一键整理",
            new Color(0.89f, 0.58f, 0.18f, 1f),
            new Color(0.36f, 0.16f, 0.04f, 1f),
            18,
            out _);
        SetTopLeft(organizeButton.GetComponent<RectTransform>(), new Vector2(874f, -11f), new Vector2(164f, 44f));

        return new FooterReferences
        {
            CapacityText = capacityText,
            VisibleCountText = visibleCountText,
            StatusText = statusText,
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
        Image shadow = CreateImage("InventorySideActionShadow_" + id, parent, new Color(0.12f, 0.05f, 0.01f, 0.36f));
        SetTopLeft(shadow.rectTransform, position + new Vector2(6f, -7f), new Vector2(154f, 126f));
        shadow.raycastTarget = false;

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

    private static ContextMenuReferences BuildContextMenu(Transform parent)
    {
        GameObject root = CreateRectObject("InventoryContextMenu", parent);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        SetTopLeft(rootRect, new Vector2(398f, -162f), new Vector2(184f, 150f));

        Image shadow = CreateImage("Shadow", root.transform, new Color(0.12f, 0.05f, 0.01f, 0.45f));
        SetTopLeft(shadow.rectTransform, new Vector2(5f, -6f), new Vector2(184f, 150f));
        shadow.raycastTarget = false;

        Image menu = CreateImage("Panel", root.transform, new Color(0.30f, 0.15f, 0.05f, 0.98f));
        SetTopLeft(menu.rectTransform, Vector2.zero, new Vector2(184f, 150f));

        TMP_Text nameText = CreateText(
            "Name",
            menu.transform,
            "物品",
            17,
            FontStyle.Bold,
            new Color(1f, 0.90f, 0.62f, 1f),
            TextAnchor.MiddleLeft);
        SetTopLeft(nameText.rectTransform, new Vector2(12f, -8f), new Vector2(136f, 28f));

        TMP_Text typeText = CreateText(
            "Type",
            menu.transform,
            "道具  x1",
            14,
            FontStyle.Normal,
            new Color(1f, 0.77f, 0.45f, 0.90f),
            TextAnchor.MiddleLeft);
        SetTopLeft(typeText.rectTransform, new Vector2(12f, -34f), new Vector2(160f, 24f));

        Button primaryButton = CreateButton(
            menu.transform,
            "PrimaryAction",
            "使用",
            new Color(0.30f, 0.68f, 0.14f, 1f),
            Color.white,
            16,
            out TMP_Text primaryLabel);
        SetTopLeft(primaryButton.GetComponent<RectTransform>(), new Vector2(10f, -64f), new Vector2(78f, 40f));

        Button sellButton = CreateButton(
            menu.transform,
            "SellAction",
            "出售",
            new Color(0.91f, 0.37f, 0.13f, 1f),
            Color.white,
            16,
            out _);
        SetTopLeft(sellButton.GetComponent<RectTransform>(), new Vector2(96f, -64f), new Vector2(78f, 40f));

        TMP_Text hintText = CreateText(
            "Hint",
            menu.transform,
            "操作 1 件物品",
            12,
            FontStyle.Normal,
            new Color(1f, 0.82f, 0.57f, 0.86f),
            TextAnchor.MiddleCenter);
        SetTopLeft(hintText.rectTransform, new Vector2(10f, -112f), new Vector2(164f, 22f));

        Button dismissButton = CreateButton(
            menu.transform,
            "Dismiss",
            "x",
            new Color(0.52f, 0.24f, 0.09f, 1f),
            Color.white,
            13,
            out _);
        SetTopLeft(dismissButton.GetComponent<RectTransform>(), new Vector2(158f, -8f), new Vector2(20f, 20f));

        return new ContextMenuReferences
        {
            Root = rootRect,
            NameText = nameText,
            TypeText = typeText,
            HintText = hintText,
            PrimaryLabel = primaryLabel,
            PrimaryButton = primaryButton,
            SellButton = sellButton,
            DismissButton = dismissButton
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
        SetTopLeft(background.rectTransform, new Vector2(1225f, -108f), new Vector2(300f, 672f));

        Image icon = CreateImage("InventoryDetailIcon", background.transform, Color.white);
        SetTopLeft(icon.rectTransform, new Vector2(32f, -32f), new Vector2(136f, 136f));
        icon.raycastTarget = false;
        icon.preserveAspect = true;

        TMP_Text name = CreateText("InventoryDetailName", background.transform, "资源详情", 25, FontStyle.Bold, new Color(1f, 0.90f, 0.66f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(name.rectTransform, new Vector2(28f, -190f), new Vector2(244f, 40f));
        TMP_Text type = CreateText("InventoryDetailType", background.transform, "物品", 16, FontStyle.Normal, new Color(1f, 0.78f, 0.45f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(type.rectTransform, new Vector2(28f, -238f), new Vector2(244f, 28f));
        TMP_Text quantity = CreateText("InventoryDetailQuantity", background.transform, "数量  0", 19, FontStyle.Bold, new Color(1f, 0.90f, 0.66f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(quantity.rectTransform, new Vector2(28f, -280f), new Vector2(244f, 32f));
        TMP_Text description = CreateText("InventoryDetailDescription", background.transform, "点击物品查看详情", 15, FontStyle.Normal, new Color(1f, 0.84f, 0.60f, 0.94f), TextAnchor.UpperLeft);
        SetTopLeft(description.rectTransform, new Vector2(28f, -332f), new Vector2(244f, 210f));
        description.enableWordWrapping = true;

        return new DetailReferences
        {
            Root = background.rectTransform,
            Icon = icon,
            NameText = name,
            TypeText = type,
            QuantityText = quantity,
            DescriptionText = description
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
            BuildIconControl("chest", "宝箱（未打开）")
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

    private sealed class ContextMenuReferences
    {
        public RectTransform Root;
        public TMP_Text NameText;
        public TMP_Text TypeText;
        public TMP_Text HintText;
        public TMP_Text PrimaryLabel;
        public Button PrimaryButton;
        public Button SellButton;
        public Button DismissButton;
    }

    private sealed class DetailReferences
    {
        public RectTransform Root;
        public Image Icon;
        public TMP_Text NameText;
        public TMP_Text TypeText;
        public TMP_Text QuantityText;
        public TMP_Text DescriptionText;
    }
}
