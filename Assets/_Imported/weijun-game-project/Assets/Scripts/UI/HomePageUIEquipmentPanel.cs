using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public partial class HomePageUI
{
    private const int EquipmentRows = 6;

    private static readonly string[] EquipmentSlotIds = { "weapon", "helmet", "armor", "boots", "gloves" };
    private static readonly string[] EquipmentSlotLabels = { "武器", "头盔", "护甲", "靴子", "手套" };
    private static readonly string[] EquipmentCategoryIds = { "all", "weapon", "helmet", "armor", "boots", "gloves" };
    private static readonly string[] EquipmentCategoryLabels = { "全部", "武器", "头盔", "护甲", "靴子", "手套" };

    private GameObject equipmentPanelObject;
    private InventoryItemResponse[] currentEquipmentItems = new InventoryItemResponse[0];
    private InventoryItemResponse[] serverEquipmentWarehouse = new InventoryItemResponse[0];
    private PlayerNpcResponse[] serverPlayerNpcs = new PlayerNpcResponse[0];
    private EquipmentLoadoutData serverEquipmentLoadout;
    private bool equipmentServerBacked;
    private bool equipmentRefreshRunning;
    private bool equipmentMutationRunning;
    private int selectedEquipmentNpcIndex;
    private int equipmentPageIndex;
    private string activeEquipmentCategory = "all";
    private string selectedEquipmentItemKey = string.Empty;
    private string equipmentStatusMessage = string.Empty;
    private EquipmentUpgradePreviewData equipmentUpgradePreview;
    private long equipmentUpgradePreviewRequestId;
    private bool equipmentUpgradePreviewRunning;
    private string equipmentUpgradePreviewError = string.Empty;
    private EquipmentAutoUpgradePreviewData equipmentAutoUpgradePreview;
    private long equipmentAutoUpgradePreviewId;
    private bool equipmentAutoUpgradePreviewRunning;
    private string equipmentAutoUpgradePreviewError = string.Empty;
    private NpcAutoUpgradePreviewData npcAutoUpgradePreview;
    private long npcAutoUpgradePreviewId;
    private bool npcAutoUpgradePreviewRunning;
    private string npcAutoUpgradePreviewError = string.Empty;
    private string npcAutoUpgradeIdempotencyKey = string.Empty;
    private string equipmentAutoUpgradeIdempotencyKey = string.Empty;
    private string npcAutoUpgradeRequestSignature = string.Empty;
    private string equipmentAutoUpgradeRequestSignature = string.Empty;
    private readonly Dictionary<string, string> autoUpgradePendingKeys = new Dictionary<string, string>();

    private void ShowEquipmentPanel()
    {
        HideEquipmentPanel();
        HideSystemMailPanel();
        HideSignPanel();
        HideLotteryPanel();
        HideRechargePanel();
        HideTaskPanel();
        HideInventoryPanel();
        HideShopPanel();
        HideAnnouncementPanel();
        HideAccountProfilePanel();
        HideFriendsPanel();
        HideSettingsPanel();

        activeEquipmentCategory = "all";
        selectedEquipmentItemKey = string.Empty;
        equipmentPageIndex = 0;
        equipmentStatusMessage = string.Empty;
        ResetEquipmentUpgradePreview();
        ResetEquipmentAutoUpgradePreviews();

        equipmentRefreshRunning = true;
        ShowEquipmentScenePanel(
            new InventoryItemResponse[0],
            true,
            "正在读取服务器居民与装备仓库...");
        StartCoroutine(GetEquipmentInventoryList());
    }

    private void ShowEquipmentScenePanel(InventoryItemResponse[] items, bool serverBacked, string status)
    {
        HomePageScenePanel panel = OpenRegisteredScenePanel(HomePageScenePanelType.Equipment);
        if (panel == null)
        {
            SetLeftContent(
                "装备",
                BuildEquipmentFallbackText(items),
                "装备场景组件未找到");
            return;
        }

        currentEquipmentItems = items ?? new InventoryItemResponse[0];
        equipmentServerBacked = serverBacked;
        equipmentStatusMessage = status ?? string.Empty;
        equipmentPanelObject = panel.gameObject;
        RenderEquipmentScenePanel(panel.PanelRoot);
        SetStatus(equipmentStatusMessage);
    }

    private IEnumerator GetEquipmentInventoryList()
    {
        ResetEquipmentUpgradePreview();
        long selectedNpcId = GetCurrentEquipmentNpc() != null ? GetCurrentEquipmentNpc().playerNpcId : 0;
        int playerId = GameSession.PlayerId;
        UnityWebRequest npcRequest = CreateAuthorizedGet($"/npc/list/{playerId}");
        if (npcRequest == null)
        {
            equipmentRefreshRunning = false;
            equipmentStatusMessage = requestCreationError;
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
            yield break;
        }

        using (npcRequest)
        {
            yield return npcRequest.SendWebRequest();

            if (equipmentPanelObject == null)
            {
                equipmentRefreshRunning = false;
                yield break;
            }

            if (HandleUnauthorizedResponse(npcRequest))
            {
                equipmentRefreshRunning = false;
                yield break;
            }

            PlayerNpcListApiResponse response = ParseJson<PlayerNpcListApiResponse>(npcRequest.downloadHandler.text);
            if (npcRequest.result != UnityWebRequest.Result.Success || response == null || response.code != 200)
            {
                equipmentRefreshRunning = false;
                equipmentStatusMessage = "居民列表读取失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(npcRequest, "服务器连接失败")
                    : GetApiError(response));
                RenderEquipmentScenePanel(GetEquipmentPanelRoot());
                yield break;
            }

            serverPlayerNpcs = response.data ?? new PlayerNpcResponse[0];
            selectedEquipmentNpcIndex = 0;
            if (selectedNpcId > 0)
            {
                for (int i = 0; i < serverPlayerNpcs.Length; i++)
                {
                    if (serverPlayerNpcs[i] != null && serverPlayerNpcs[i].playerNpcId == selectedNpcId)
                    {
                        selectedEquipmentNpcIndex = i;
                        break;
                    }
                }
            }
        }

        yield return RefreshServerEquipmentWarehouse();
        if (!equipmentRefreshRunning || equipmentPanelObject == null)
        {
            yield break;
        }

        serverEquipmentLoadout = null;
        if (GetCurrentEquipmentNpc() != null)
        {
            yield return RefreshSelectedNpcLoadout();
            if (!equipmentRefreshRunning)
            {
                RebuildServerEquipmentItems();
                RenderEquipmentScenePanel(GetEquipmentPanelRoot());
                yield break;
            }
        }

        RebuildServerEquipmentItems();
        equipmentRefreshRunning = false;
        equipmentStatusMessage = serverPlayerNpcs.Length == 0
            ? "装备仓库已刷新；当前账号还没有可穿戴装备的居民"
            : "服务器居民、装备仓库与穿戴数据已刷新";
        RenderEquipmentScenePanel(GetEquipmentPanelRoot());
    }

    private IEnumerator RefreshServerEquipmentWarehouse()
    {
        UnityWebRequest request = CreateAuthorizedGet("/equipment/warehouse");
        if (request == null)
        {
            equipmentRefreshRunning = false;
            equipmentStatusMessage = requestCreationError;
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                equipmentRefreshRunning = false;
                yield break;
            }

            EquipmentWarehouseApiResponse response = ParseJson<EquipmentWarehouseApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200)
            {
                equipmentRefreshRunning = false;
                equipmentStatusMessage = "装备仓库读取失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response));
                RenderEquipmentScenePanel(GetEquipmentPanelRoot());
                yield break;
            }

            serverEquipmentWarehouse = response.data ?? new InventoryItemResponse[0];
            for (int i = 0; i < serverEquipmentWarehouse.Length; i++)
            {
                NormalizeServerEquipmentItem(serverEquipmentWarehouse[i], "WAREHOUSE");
            }
        }
    }

    private IEnumerator RefreshSelectedNpcLoadout()
    {
        PlayerNpcResponse npc = GetCurrentEquipmentNpc();
        if (npc == null)
        {
            serverEquipmentLoadout = null;
            yield break;
        }

        UnityWebRequest request = CreateAuthorizedGet($"/equipment/loadout/{npc.playerNpcId}");
        if (request == null)
        {
            equipmentRefreshRunning = false;
            equipmentStatusMessage = requestCreationError;
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                equipmentRefreshRunning = false;
                yield break;
            }

            EquipmentLoadoutApiResponse response = ParseJson<EquipmentLoadoutApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                equipmentRefreshRunning = false;
                equipmentStatusMessage = "居民装备读取失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response));
                yield break;
            }

            serverEquipmentLoadout = response.data;
        }
    }

    private void RebuildServerEquipmentItems()
    {
        List<InventoryItemResponse> items = new List<InventoryItemResponse>();
        HashSet<long> instanceIds = new HashSet<long>();
        for (int i = 0; i < serverEquipmentWarehouse.Length; i++)
        {
            InventoryItemResponse item = serverEquipmentWarehouse[i];
            if (item != null && instanceIds.Add(item.playerEquipmentId))
            {
                items.Add(item);
            }
        }

        if (serverEquipmentLoadout != null && serverEquipmentLoadout.equipment != null)
        {
            for (int i = 0; i < serverEquipmentLoadout.equipment.Length; i++)
            {
                EquipmentLoadoutItem loadoutItem = serverEquipmentLoadout.equipment[i];
                if (loadoutItem == null || !instanceIds.Add(loadoutItem.playerEquipmentId))
                {
                    continue;
                }

                InventoryItemResponse item = new InventoryItemResponse
                {
                    playerEquipmentId = loadoutItem.playerEquipmentId,
                    equipmentId = loadoutItem.equipmentId,
                    equipmentKey = loadoutItem.equipmentKey,
                    name = loadoutItem.name,
                    equipmentType = loadoutItem.equipmentType,
                    quality = loadoutItem.quality,
                    icon = loadoutItem.icon,
                    modelKey = loadoutItem.modelKey,
                    level = loadoutItem.level,
                    locked = loadoutItem.locked,
                    healthBonus = loadoutItem.healthBonus,
                    attackBonus = loadoutItem.attackBonus,
                    defenseBonus = loadoutItem.defenseBonus,
                    speedBonus = loadoutItem.speedBonus
                };
                NormalizeServerEquipmentItem(item, "EQUIPPED");
                items.Add(item);
            }
        }

        currentEquipmentItems = items.ToArray();
    }

    private static void NormalizeServerEquipmentItem(InventoryItemResponse item, string fallbackStatus)
    {
        if (item == null)
        {
            return;
        }

        item.itemKey = "equipment-instance-" + item.playerEquipmentId;
        item.itemType = string.IsNullOrEmpty(item.equipmentType) ? "EQUIPMENT" : item.equipmentType;
        item.quantity = 1;
        if (string.IsNullOrEmpty(item.status))
        {
            item.status = fallbackStatus;
        }
    }

    private Transform GetEquipmentPanelRoot()
    {
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Equipment);
        return panel != null ? panel.PanelRoot : null;
    }

    private void EnsureBackendEquipmentUi(Transform root)
    {
        RenameLegacyEquipmentNode(root, "EquipmentSlot", "Accessory", "Helmet");
        RenameLegacyEquipmentNode(root, "EquipmentSlot", "Pet", "Boots");
        CloneEquipmentNode(root, "EquipmentSlotBoots", "EquipmentSlotGloves", "Boots", "Gloves");

        Vector2[] slotPositions =
        {
            new Vector2(62f, -222f),
            new Vector2(207f, -222f),
            new Vector2(352f, -222f),
            new Vector2(134f, -372f),
            new Vector2(279f, -372f)
        };
        for (int i = 0; i < EquipmentSlotIds.Length; i++)
        {
            Button slot = FindSceneComponent<Button>(root, "EquipmentSlot" + ToPascalCase(EquipmentSlotIds[i]));
            if (slot == null)
            {
                continue;
            }

            RectTransform rect = slot.GetComponent<RectTransform>();
            SetRect(rect, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), slotPositions[i], new Vector2(139f, 129f));
            rect.localScale = Vector3.one;

            string suffix = ToPascalCase(EquipmentSlotIds[i]);
            RectTransform iconBackground = FindSceneTransform(root, "EquipmentSlotIconBackground" + suffix) as RectTransform;
            if (iconBackground != null)
            {
                SetRect(iconBackground, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(42.5f, -8f), new Vector2(54f, 54f));
            }

            Text iconLabel = FindSceneComponent<Text>(root, "EquipmentSlotIcon" + suffix);
            if (iconLabel != null)
            {
                Stretch(iconLabel.rectTransform);
                iconLabel.fontSize = 24;
                iconLabel.resizeTextForBestFit = true;
                iconLabel.resizeTextMinSize = 16;
                iconLabel.resizeTextMaxSize = 24;
            }
            ConfigureCompactEquipmentSlotText(root, "EquipmentSlotName" + suffix, new Vector2(8f, -66f), new Vector2(123f, 24f), 16, 12);
            ConfigureCompactEquipmentSlotText(root, "EquipmentSlotState" + suffix, new Vector2(8f, -92f), new Vector2(123f, 20f), 13, 11);

            RectTransform empty = FindSceneTransform(root, "EquipmentSlotEmpty" + suffix) as RectTransform;
            if (empty != null)
            {
                SetRect(empty, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(8f, -113f), new Vector2(123f, 14f));
                Text emptyLabel = empty.GetComponentInChildren<Text>(true);
                if (emptyLabel != null)
                {
                    emptyLabel.fontSize = 11;
                    emptyLabel.resizeTextForBestFit = true;
                    emptyLabel.resizeTextMinSize = 9;
                    emptyLabel.resizeTextMaxSize = 11;
                }
            }
        }

        RenameLegacyEquipmentNode(root, "EquipmentCategory", "Accessory", "Helmet");
        RenameLegacyEquipmentNode(root, "EquipmentCategory", "Pet", "Boots");
        CloneEquipmentNode(root, "EquipmentCategoryBoots", "EquipmentCategoryGloves", "Boots", "Gloves");

        for (int i = 0; i < EquipmentCategoryIds.Length; i++)
        {
            Button category = FindSceneComponent<Button>(root, "EquipmentCategory" + ToPascalCase(EquipmentCategoryIds[i]));
            if (category == null)
            {
                continue;
            }

            RectTransform rect = category.GetComponent<RectTransform>();
            SetRect(
                rect,
                TopLeftAnchor(),
                TopLeftAnchor(),
                TopLeftAnchor(),
                new Vector2(548f + i * 70f, -216f),
                new Vector2(64f, 38f));
            rect.localScale = Vector3.one;
            SetSceneButtonText(category, EquipmentCategoryLabels[i]);
            ConfigureClearButtonLabel(category, 15);
        }

        EnsureEquipmentSpeedUi(root);
    }

    private void ConfigureCompactEquipmentSlotText(
        Transform root,
        string objectName,
        Vector2 position,
        Vector2 size,
        int fontSize,
        int minimumFontSize)
    {
        Text label = FindSceneComponent<Text>(root, objectName);
        if (label == null)
        {
            return;
        }

        SetRect(label.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, size);
        label.fontSize = fontSize;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = minimumFontSize;
        label.resizeTextMaxSize = fontSize;
    }

    private static void RenameLegacyEquipmentNode(
        Transform root,
        string prefix,
        string legacySuffix,
        string backendSuffix)
    {
        if (FindSceneTransform(root, prefix + backendSuffix) != null)
        {
            return;
        }

        Transform legacy = FindSceneTransform(root, prefix + legacySuffix);
        if (legacy != null)
        {
            RenameEquipmentNodeTree(legacy, legacySuffix, backendSuffix);
            legacy.name = prefix + backendSuffix;
        }
    }

    private static void CloneEquipmentNode(
        Transform root,
        string sourceName,
        string targetName,
        string sourceSuffix,
        string targetSuffix)
    {
        if (FindSceneTransform(root, targetName) != null)
        {
            return;
        }

        Transform source = FindSceneTransform(root, sourceName);
        if (source == null)
        {
            return;
        }

        GameObject clone = UnityEngine.Object.Instantiate(source.gameObject, source.parent, false);
        RenameEquipmentNodeTree(clone.transform, sourceSuffix, targetSuffix);
        clone.name = targetName;
    }

    private static void RenameEquipmentNodeTree(Transform node, string sourceSuffix, string targetSuffix)
    {
        Transform[] descendants = node.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            descendants[i].name = descendants[i].name.Replace(sourceSuffix, targetSuffix);
        }
    }

    private void EnsureEquipmentSpeedUi(Transform root)
    {
        Text speedSummary = FindSceneComponent<Text>(root, "EquipmentSpeedText");
        if (speedSummary == null)
        {
            Text healthSummary = FindSceneComponent<Text>(root, "EquipmentHealthText");
            if (healthSummary != null)
            {
                speedSummary = UnityEngine.Object.Instantiate(healthSummary, healthSummary.transform.parent, false);
                speedSummary.name = "EquipmentSpeedText";
            }
        }
        if (speedSummary != null)
        {
            SetRect(
                speedSummary.rectTransform,
                TopLeftAnchor(),
                TopLeftAnchor(),
                TopLeftAnchor(),
                new Vector2(1120f, -758f),
                new Vector2(130f, 28f));
        }

        Transform speedChip = FindSceneTransform(root, "EquipmentDetailSpeedChip");
        if (speedChip == null)
        {
            Transform healthChip = FindSceneTransform(root, "EquipmentDetailHealthChip");
            if (healthChip != null)
            {
                GameObject clone = UnityEngine.Object.Instantiate(healthChip.gameObject, healthChip.parent, false);
                RenameEquipmentNodeTree(clone.transform, "Health", "Speed");
                clone.name = "EquipmentDetailSpeedChip";
                speedChip = clone.transform;

                Image speedMark = FindSceneComponent<Image>(speedChip, "EquipmentDetailSpeedMark");
                if (speedMark != null)
                {
                    speedMark.color = new Color(0.32f, 0.62f, 0.75f, 1f);
                }
            }
        }

        string[] statIds = { "Attack", "Defense", "Health", "Speed", "Power" };
        Vector2[] statPositions =
        {
            new Vector2(28f, -312f),
            new Vector2(172f, -312f),
            new Vector2(316f, -312f),
            new Vector2(100f, -376f),
            new Vector2(244f, -376f)
        };
        for (int i = 0; i < statIds.Length; i++)
        {
            string statId = statIds[i];
            Transform chip = FindSceneTransform(root, "EquipmentDetail" + statId + "Chip");
            if (chip == null)
            {
                continue;
            }

            SetRect(
                chip.GetComponent<RectTransform>(),
                TopLeftAnchor(),
                TopLeftAnchor(),
                TopLeftAnchor(),
                statPositions[i],
                new Vector2(130f, 52f));

            RectTransform mark = FindSceneTransform(chip, "EquipmentDetail" + statId + "Mark") as RectTransform;
            if (mark != null)
            {
                SetRect(mark, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(10f, -12f), new Vector2(24f, 28f));
            }

            Text value = FindSceneComponent<Text>(chip, "EquipmentDetail" + statId);
            if (value != null)
            {
                SetRect(value.rectTransform, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(40f, -8f), new Vector2(82f, 32f));
                value.resizeTextForBestFit = true;
                value.resizeTextMinSize = 11;
                value.resizeTextMaxSize = Mathf.Max(16, value.fontSize);
            }
        }
    }

    private void RenderEquipmentScenePanel(Transform root)
    {
        if (root == null)
        {
            return;
        }

        EnsureBackendEquipmentUi(root);

        List<InventoryItemResponse> allVisibleItems = GetFilteredEquipmentItems();
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(allVisibleItems.Count / (float)EquipmentRows));
        equipmentPageIndex = Mathf.Clamp(equipmentPageIndex, 0, pageCount - 1);
        int firstItemIndex = equipmentPageIndex * EquipmentRows;
        List<InventoryItemResponse> visibleItems = allVisibleItems.GetRange(
            firstItemIndex,
            Mathf.Min(EquipmentRows, Mathf.Max(0, allVisibleItems.Count - firstItemIndex)));
        InventoryItemResponse selected = FindEquipmentItem(selectedEquipmentItemKey);
        if (selected == null || !visibleItems.Contains(selected))
        {
            selected = visibleItems.Count > 0 ? visibleItems[0] : null;
            selectedEquipmentItemKey = selected == null ? string.Empty : GetInventoryItemKey(selected);
        }

        EquipmentStats totalStats = CalculateEquippedStats();
        PlayerNpcResponse selectedNpc = GetCurrentEquipmentNpc();
        SetSceneText(root, "EquipmentTitle", "装备大厅");
        SetSceneText(
            root,
            "EquipmentModeText",
            equipmentServerBacked
                ? (selectedNpc == null ? "服务器装备 · 暂无居民" : $"{selectedNpc.name}  等级 {selectedNpc.level}  {selectedEquipmentNpcIndex + 1}/{serverPlayerNpcs.Length}")
                : "本地奖励库存 · 可穿戴");
        SetSceneText(root, "EquipmentPowerText", $"总战力  {totalStats.Power}");
        SetSceneText(root, "EquipmentCount", $"装备 {allVisibleItems.Count} 件");
        SetSceneText(root, "EquipmentStatus", equipmentStatusMessage);
        SetSceneText(root, "EquipmentHint", equipmentServerBacked
            ? "装备操作以服务器实例状态为准；锁定装备不会被自动选作突破材料"
            : "点击装备栏可卸下，选择物品后可穿戴");
        SetSceneText(root, "EquipmentAttackText", $"攻击  {totalStats.Attack}");
        SetSceneText(root, "EquipmentDefenseText", $"防御  {totalStats.Defense}");
        SetSceneText(root, "EquipmentHealthText", $"生命  {totalStats.Health}");
        SetSceneText(root, "EquipmentSpeedText", $"速度  {totalStats.Speed}");
        SetSceneText(root, "EquipmentPowerDetailText", $"战力  {totalStats.Power}");
        ConfigureServerNpcControls(root);
        ConfigureEquipmentPaging(root, pageCount);

        BindSceneButton(FindSceneComponent<Button>(root, "EquipmentCloseButton"), HideEquipmentPanel);
        Button refresh = FindSceneComponent<Button>(root, "EquipmentRefreshButton");
        if (refresh != null)
        {
            refresh.interactable = HasAuthSession() && !equipmentRefreshRunning && !equipmentMutationRunning;
        }
        SetSceneButtonText(refresh, equipmentRefreshRunning ? "同步中..." : (HasAuthSession() ? "刷新背包" : "本地已同步"));
        BindSceneButton(refresh, () =>
        {
            if (HasAuthSession() && !equipmentRefreshRunning)
            {
                equipmentRefreshRunning = true;
                equipmentStatusMessage = "正在同步服务器背包...";
                RenderEquipmentScenePanel(root);
                StartCoroutine(GetEquipmentInventoryList());
            }
        });

        for (int i = 0; i < EquipmentCategoryIds.Length; i++)
        {
            string categoryId = EquipmentCategoryIds[i];
            Button categoryButton = FindSceneComponent<Button>(root, "EquipmentCategory" + ToPascalCase(categoryId));
            ConfigureEquipmentCategoryButton(categoryButton, activeEquipmentCategory == categoryId);
            if (categoryButton != null)
            {
                categoryButton.interactable = !equipmentRefreshRunning && !equipmentMutationRunning;
            }
            BindSceneButton(categoryButton, () =>
            {
                activeEquipmentCategory = categoryId;
                selectedEquipmentItemKey = string.Empty;
                equipmentPageIndex = 0;
                equipmentStatusMessage = "已切换装备分类";
                RenderEquipmentScenePanel(root);
            });
        }

        RenderEquipmentSlots(root);
        RenderEquipmentRows(root, visibleItems);
        RenderEquipmentDetail(root, selected);
    }

    private static void ConfigureEquipmentCategoryButton(Button button, bool selected)
    {
        if (button == null)
        {
            return;
        }

        Image background = button.GetComponent<Image>();
        if (background != null)
        {
            background.color = selected
                ? new Color(0.42f, 0.24f, 0.50f, 1f)
                : new Color(0.90f, 0.79f, 0.59f, 1f);
        }

        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.color = selected ? Color.white : new Color(0.36f, 0.20f, 0.10f, 1f);
        }
    }

    private void RenderEquipmentSlots(Transform root)
    {
        for (int i = 0; i < EquipmentSlotIds.Length; i++)
        {
            string slotId = EquipmentSlotIds[i];
            InventoryItemResponse item = FindEquipmentItem(GetEquippedItemKey(slotId));
            string slotName = EquipmentSlotLabels[i];
            Button slotButton = FindSceneComponent<Button>(root, "EquipmentSlot" + ToPascalCase(slotId));
            Image slotBackground = slotButton != null ? slotButton.GetComponent<Image>() : null;
            if (slotBackground != null)
            {
                slotBackground.color = item == null
                    ? new Color(0.86f, 0.72f, 0.48f, 1f)
                    : GetEquipmentColor(item);
            }

            Image slotIconBackground = FindSceneComponent<Image>(root, "EquipmentSlotIconBackground" + ToPascalCase(slotId));
            if (slotIconBackground != null)
            {
                Color slotColor = GetEquipmentSlotColor(slotId);
                slotIconBackground.color = item == null ? Color.Lerp(slotColor, Color.white, 0.12f) : GetEquipmentColor(item);
            }

            SetSceneText(root, "EquipmentSlotIcon" + ToPascalCase(slotId), item == null ? GetSlotIcon(slotId) : GetNicknameInitial(item.name));
            SetSceneText(root, "EquipmentSlotName" + ToPascalCase(slotId), item == null ? "空装备位" : item.name);
            SetSceneText(root, "EquipmentSlotState" + ToPascalCase(slotId), item == null ? slotName : $"{slotName} · 已装备");
            SetSceneObjectActive(root, "EquipmentSlotEmpty" + ToPascalCase(slotId), item == null);

            string capturedSlotId = slotId;
            BindSceneButton(slotButton, () => UnequipEquipment(root, capturedSlotId));
        }
    }

    private void RenderEquipmentRows(Transform root, List<InventoryItemResponse> visibleItems)
    {
        for (int i = 0; i < EquipmentRows; i++)
        {
            Transform row = FindSceneTransform(root, "EquipmentItemRow" + i);
            if (row == null)
            {
                continue;
            }

            bool visible = i < visibleItems.Count;
            row.gameObject.SetActive(visible);
            if (!visible)
            {
                continue;
            }

            InventoryItemResponse item = visibleItems[i];
            string itemKey = GetInventoryItemKey(item);
            bool equipped = IsItemEquipped(item);
            Image background = row.GetComponent<Image>();
            if (background != null)
            {
                background.color = itemKey == selectedEquipmentItemKey
                    ? new Color(1f, 0.78f, 0.38f, 1f)
                    : new Color(1f, 0.94f, 0.74f, 1f);
            }

            Image iconBackground = FindSceneComponent<Image>(row, "EquipmentRowIconBackground" + i);
            if (iconBackground != null)
            {
                iconBackground.color = GetEquipmentColor(item);
            }

            SetSceneText(row, "EquipmentRowIcon" + i, GetNicknameInitial(item.name));
            SetSceneText(row, "EquipmentRowName" + i, item.name);
            SetSceneText(
                row,
                "EquipmentRowMeta" + i,
                equipmentServerBacked
                    ? $"{GetEquipmentTypeLabel(item)}  等级 {item.level}  {GetEquipmentQualityLabel(item.quality)}"
                    : $"{GetEquipmentTypeLabel(item)}  ×{item.quantity}");
            SetSceneText(
                row,
                "EquipmentRowEquipped" + i,
                equipmentServerBacked
                    ? (equipped ? "已装备" : (item.locked ? "已锁定" : "仓库"))
                    : (equipped ? "已装备" : "可穿戴"));
            Button rowButton = row.GetComponent<Button>();
            string capturedKey = itemKey;
            BindSceneButton(rowButton, () => SelectEquipmentItem(root, capturedKey));
        }
    }

    private void RenderEquipmentDetail(Transform root, InventoryItemResponse item)
    {
        bool hasItem = item != null;
        SetSceneObjectActive(root, "EquipmentDetailEmpty", !hasItem);
        SetSceneObjectActive(root, "EquipmentDetailContent", hasItem);
        if (!hasItem)
        {
            SetSceneText(root, "EquipmentDetailStatus", "暂无可选装备");
            return;
        }

        EnsureEquipmentUpgradePreview(item);
        EquipmentStats stats = GetEquipmentStats(item);
        SetSceneText(root, "EquipmentDetailIcon", GetNicknameInitial(item.name));
        SetSceneText(root, "EquipmentDetailName", item.name);
        SetSceneText(
            root,
            "EquipmentDetailType",
            equipmentServerBacked
                ? $"{GetEquipmentTypeLabel(item)} · 等级 {item.level} · {GetEquipmentQualityLabel(item.quality)}"
                : $"{GetEquipmentTypeLabel(item)}  ·  数量 ×{item.quantity}");
        Text descriptionText = FindSceneComponent<Text>(root, "EquipmentDetailDescription");
        if (descriptionText != null)
        {
            descriptionText.resizeTextForBestFit = true;
            descriptionText.resizeTextMinSize = 10;
            descriptionText.resizeTextMaxSize = 16;
            descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descriptionText.verticalOverflow = VerticalWrapMode.Truncate;
            descriptionText.lineSpacing = 0.9f;
            RectTransform descriptionCard = descriptionText.transform.parent as RectTransform;
            if (descriptionCard != null)
            {
                descriptionCard.sizeDelta = new Vector2(descriptionCard.sizeDelta.x, 72f);
            }
        }
        SetSceneText(root, "EquipmentDetailDescription", GetEquipmentDescriptionWithUpgradePreview(item));
        SetSceneText(root, "EquipmentDetailAttack", $"攻击  +{stats.Attack}");
        SetSceneText(root, "EquipmentDetailDefense", $"防御  +{stats.Defense}");
        SetSceneText(root, "EquipmentDetailHealth", $"生命  +{stats.Health}");
        SetSceneText(root, "EquipmentDetailSpeed", $"速度  +{stats.Speed}");
        SetSceneText(root, "EquipmentDetailPower", $"战力  +{stats.Power}");

        Image iconBackground = FindSceneComponent<Image>(root, "EquipmentDetailIconBackground");
        if (iconBackground != null)
        {
            iconBackground.color = GetEquipmentColor(item);
        }

        bool equipped = IsItemEquipped(item);
        Button action = FindSceneComponent<Button>(root, "EquipmentActionButton");
        if (action != null)
        {
            action.interactable = !equipmentMutationRunning && (!equipmentServerBacked || GetCurrentEquipmentNpc() != null);
        }
        SetSceneButtonText(action, equipped ? "卸下装备" : "穿戴装备");
        BindSceneButton(action, () => ToggleEquipment(root, item));
        ConfigureServerEquipmentDetailActions(root, item);
        SetSceneText(
            root,
            "EquipmentDetailStatus",
            equipmentServerBacked
                ? $"实例编号 {item.playerEquipmentId} · {(equipped ? "当前居民已穿戴" : "当前位于仓库")} · {(item.locked ? "已锁定" : "未锁定")}"
                : (equipped ? "当前已穿戴" : "选择穿戴后会替换同一栏位的装备"));
    }

    private void SelectEquipmentItem(Transform root, string itemKey)
    {
        InventoryItemResponse item = FindEquipmentItem(itemKey);
        if (item == null)
        {
            return;
        }

        selectedEquipmentItemKey = itemKey;
        equipmentStatusMessage = $"已选择：{item.name}";
        RenderEquipmentScenePanel(root);
    }

    private void ToggleEquipment(Transform root, InventoryItemResponse item)
    {
        if (item == null)
        {
            return;
        }

        string slotId = GetEquipmentSlot(item);
        if (string.IsNullOrEmpty(slotId))
        {
            equipmentStatusMessage = "该物品没有可用装备栏位";
            RenderEquipmentScenePanel(root);
            return;
        }

        if (equipmentServerBacked)
        {
            PlayerNpcResponse npc = GetCurrentEquipmentNpc();
            if (npc == null)
            {
                equipmentStatusMessage = "当前没有可穿戴装备的居民";
                RenderEquipmentScenePanel(root);
                return;
            }

            if (IsItemEquipped(item))
            {
                StartCoroutine(UnequipServerEquipment(npc.playerNpcId, item.playerEquipmentId));
            }
            else
            {
                StartCoroutine(EquipServerEquipment(npc.playerNpcId, item.playerEquipmentId));
            }
            return;
        }

        string currentKey = GetEquippedItemKey(slotId);
        if (currentKey == GetInventoryItemKey(item))
        {
            ClearEquippedItemKey(slotId);
            equipmentStatusMessage = equipmentServerBacked ? "已取消本地预览" : $"已卸下：{item.name}";
        }
        else
        {
            SetEquippedItemKey(slotId, GetInventoryItemKey(item));
            equipmentStatusMessage = equipmentServerBacked ? $"已预览穿戴：{item.name}" : $"已穿戴：{item.name}";
        }

        SaveEquipmentState();
        RenderEquipmentScenePanel(root);
    }

    private void UnequipEquipment(Transform root, string slotId)
    {
        string currentKey = GetEquippedItemKey(slotId);
        if (string.IsNullOrEmpty(currentKey))
        {
            equipmentStatusMessage = "该装备位当前为空";
            RenderEquipmentScenePanel(root);
            return;
        }

        if (equipmentServerBacked)
        {
            PlayerNpcResponse npc = GetCurrentEquipmentNpc();
            InventoryItemResponse item = FindEquipmentItem(currentKey);
            if (npc == null || item == null)
            {
                equipmentStatusMessage = "当前居民的装备状态已变化，请刷新";
                RenderEquipmentScenePanel(root);
                return;
            }

            StartCoroutine(UnequipServerEquipment(npc.playerNpcId, item.playerEquipmentId));
            return;
        }

        ClearEquippedItemKey(slotId);
        SaveEquipmentState();
        equipmentStatusMessage = equipmentServerBacked ? "已取消本地预览" : $"已卸下{GetEquipmentSlotLabel(slotId)}装备";
        RenderEquipmentScenePanel(root);
    }

    private List<InventoryItemResponse> GetFilteredEquipmentItems()
    {
        List<InventoryItemResponse> result = new List<InventoryItemResponse>();
        if (currentEquipmentItems == null)
        {
            return result;
        }

        for (int i = 0; i < currentEquipmentItems.Length; i++)
        {
            InventoryItemResponse item = currentEquipmentItems[i];
            if (item == null
                || (!equipmentServerBacked && item.quantity <= 0)
                || (!IsEquipmentItem(item) && !IsPetItem(item)))
            {
                continue;
            }

            string slotId = GetEquipmentSlot(item);
            if (string.IsNullOrEmpty(slotId))
            {
                continue;
            }
            if (activeEquipmentCategory != "all" && slotId != activeEquipmentCategory)
            {
                continue;
            }

            result.Add(item);
        }

        result.Sort((left, right) =>
        {
            int equippedOrder = IsItemEquipped(right).CompareTo(IsItemEquipped(left));
            if (equippedOrder != 0)
            {
                return equippedOrder;
            }
            return string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase);
        });
        return result;
    }

    private InventoryItemResponse FindEquipmentItem(string itemKey)
    {
        if (string.IsNullOrEmpty(itemKey) || currentEquipmentItems == null)
        {
            return null;
        }

        for (int i = 0; i < currentEquipmentItems.Length; i++)
        {
            if (currentEquipmentItems[i] != null && GetInventoryItemKey(currentEquipmentItems[i]) == itemKey)
            {
                return currentEquipmentItems[i];
            }
        }

        return null;
    }

    private string GetEquipmentSlot(InventoryItemResponse item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        string equipmentType = (!string.IsNullOrEmpty(item.equipmentType) ? item.equipmentType : item.itemType ?? string.Empty)
            .Trim()
            .ToUpperInvariant();
        if (equipmentType == "WEAPON") return "weapon";
        if (equipmentType == "HELMET") return "helmet";
        if (equipmentType == "ARMOR") return "armor";
        if (equipmentType == "BOOTS") return "boots";
        if (equipmentType == "GLOVES") return "gloves";

        if (equipmentServerBacked)
        {
            return string.Empty;
        }

        if (!IsEquipmentItem(item))
        {
            return string.Empty;
        }

        string key = NormalizeInventoryKey(
            string.IsNullOrEmpty(item.equipmentKey) ? GetInventoryItemKey(item) : item.equipmentKey);
        string name = item.name ?? string.Empty;
        if (IsWeaponEquipment(key, name))
        {
            return "weapon";
        }

        if (IsHelmetEquipment(key, name))
        {
            return "helmet";
        }

        if (IsArmorEquipment(key, name))
        {
            return "armor";
        }

        if (IsBootsEquipment(key, name))
        {
            return "boots";
        }

        if (IsGlovesEquipment(key, name))
        {
            return "gloves";
        }

        return string.Empty;
    }

    private static bool IsWeaponEquipment(string key, string name)
    {
        return key.Contains("weapon") || key.Contains("sword") || key.Contains("blade") || key.Contains("bow")
            || key.Contains("staff") || key.Contains("axe") || key.Contains("spear")
            || name.Contains("剑") || name.Contains("刀") || name.Contains("弓") || name.Contains("杖") || name.Contains("武器");
    }

    private static bool IsArmorEquipment(string key, string name)
    {
        return key.Contains("armor") || key.Contains("plate") || key.Contains("shield") || key.Contains("clothes")
            || name.Contains("甲") || name.Contains("铠") || name.Contains("盾") || name.Contains("护甲") || name.Contains("防具");
    }

    private static bool IsHelmetEquipment(string key, string name)
    {
        return key.Contains("helmet") || key.Contains("helm") || key.Contains("headgear")
            || name.Contains("头盔") || name.Contains("盔") || name.Contains("帽");
    }

    private static bool IsBootsEquipment(string key, string name)
    {
        return key.Contains("boots") || key.Contains("boot") || key.Contains("shoes") || key.Contains("shoe")
            || name.Contains("靴") || name.Contains("鞋");
    }

    private static bool IsGlovesEquipment(string key, string name)
    {
        return key.Contains("gloves") || key.Contains("glove") || key.Contains("gauntlet")
            || name.Contains("手套") || name.Contains("护手") || name.Contains("拳套");
    }

    private string GetEquippedItemKey(string slotId)
    {
        if (equipmentServerBacked)
        {
            if (currentEquipmentItems == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < currentEquipmentItems.Length; i++)
            {
                InventoryItemResponse item = currentEquipmentItems[i];
                if (item != null && IsItemEquippedByStatus(item) && GetEquipmentSlot(item) == slotId)
                {
                    return GetInventoryItemKey(item);
                }
            }

            return string.Empty;
        }

        string key = PlayerPrefs.GetString(GetEquipmentStateKey(slotId), string.Empty);
        if (!string.IsNullOrEmpty(key))
        {
            return key;
        }

        if (!equipmentServerBacked && slotId == "weapon")
        {
            return PlayerPrefs.GetString("EquippedItemKey", string.Empty);
        }

        return string.Empty;
    }

    private void SetEquippedItemKey(string slotId, string itemKey)
    {
        PlayerPrefs.SetString(GetEquipmentStateKey(slotId), itemKey ?? string.Empty);
        if (!equipmentServerBacked && slotId == "weapon")
        {
            PlayerPrefs.SetString("EquippedItemKey", itemKey ?? string.Empty);
        }
    }

    private void ClearEquippedItemKey(string slotId)
    {
        PlayerPrefs.DeleteKey(GetEquipmentStateKey(slotId));
        if (!equipmentServerBacked && slotId == "weapon")
        {
            PlayerPrefs.DeleteKey("EquippedItemKey");
        }
    }

    private string GetEquipmentStateKey(string slotId)
    {
        string prefix = equipmentServerBacked ? "EquipmentPreview" : "Equipped";
        string suffix = char.ToUpper(slotId[0]) + slotId.Substring(1);
        return prefix + suffix + "Key";
    }

    private void SaveEquipmentState()
    {
        PlayerPrefs.Save();
    }

    private bool IsItemEquipped(InventoryItemResponse item)
    {
        if (equipmentServerBacked)
        {
            return IsItemEquippedByStatus(item);
        }

        string slotId = GetEquipmentSlot(item);
        return !string.IsNullOrEmpty(slotId) && GetEquippedItemKey(slotId) == GetInventoryItemKey(item);
    }

    private static bool IsItemEquippedByStatus(InventoryItemResponse item)
    {
        return item != null && string.Equals(item.status, "EQUIPPED", StringComparison.OrdinalIgnoreCase);
    }

    private EquipmentStats CalculateEquippedStats()
    {
        if (equipmentServerBacked && serverEquipmentLoadout != null)
        {
            EquipmentStats serverStats = new EquipmentStats
            {
                Attack = serverEquipmentLoadout.totalAttack,
                Defense = serverEquipmentLoadout.totalDefense,
                Health = serverEquipmentLoadout.totalHealth,
                Speed = serverEquipmentLoadout.totalSpeed
            };
            serverStats.Power = CalculateEquipmentPower(serverStats);
            return serverStats;
        }

        EquipmentStats result = new EquipmentStats
        {
            Attack = 10,
            Defense = 8,
            Health = 100,
            Speed = 10
        };

        for (int i = 0; i < EquipmentSlotIds.Length; i++)
        {
            InventoryItemResponse item = FindEquipmentItem(GetEquippedItemKey(EquipmentSlotIds[i]));
            if (item == null)
            {
                continue;
            }

            EquipmentStats itemStats = GetEquipmentStats(item);
            result.Attack += itemStats.Attack;
            result.Defense += itemStats.Defense;
            result.Health += itemStats.Health;
            result.Speed += itemStats.Speed;
        }

        result.Power = CalculateEquipmentPower(result);
        return result;
    }

    private EquipmentStats GetEquipmentStats(InventoryItemResponse item)
    {
        if (equipmentServerBacked && item != null)
        {
            EquipmentStats serverStats = new EquipmentStats
            {
                Attack = item.attackBonus,
                Defense = item.defenseBonus,
                Health = item.healthBonus,
                Speed = item.speedBonus
            };
            serverStats.Power = CalculateEquipmentPower(serverStats);
            return serverStats;
        }

        string slotId = GetEquipmentSlot(item);
        string key = NormalizeInventoryKey(GetInventoryItemKey(item));
        int variant = StableInventoryHash(key) % 6;
        EquipmentStats result = new EquipmentStats();
        if (slotId == "weapon")
        {
            result.Attack = 12 + variant;
        }
        else if (slotId == "helmet")
        {
            result.Defense = 7 + variant;
            result.Health = 20 + variant * 2;
        }
        else if (slotId == "armor")
        {
            result.Defense = 12 + variant;
            result.Health = 35 + variant * 3;
        }
        else if (slotId == "boots")
        {
            result.Defense = 3 + variant / 2;
            result.Health = 8 + variant;
            result.Speed = 3 + variant / 2;
        }
        else if (slotId == "gloves")
        {
            result.Attack = 5 + variant;
            result.Defense = 2 + variant / 2;
            result.Speed = 1 + variant / 3;
        }

        result.Power = CalculateEquipmentPower(result);
        return result;
    }

    private static int CalculateEquipmentPower(EquipmentStats stats)
    {
        return stats.Attack + stats.Defense + stats.Health / 10 + stats.Speed;
    }

    private string GetEquipmentDescription(InventoryItemResponse item)
    {
        if (equipmentServerBacked && item != null && !string.IsNullOrEmpty(item.description))
        {
            return item.description;
        }

        string slot = GetEquipmentSlot(item);
        if (slot == "weapon") return "适合领主出征的攻击装备，提升基础攻击。";
        if (slot == "helmet") return "保护头部的防御装备，提升生命与防御。";
        if (slot == "armor") return "守护领地的护甲装备，提升防御与生命。";
        if (slot == "boots") return "兼顾防护与行动的靴子，可提升速度。";
        if (slot == "gloves") return "强化攻击动作的手套，补充战斗属性。";
        return "装备属性与穿戴部位由服务器配置决定。";
    }

    private string GetEquipmentDescriptionWithUpgradePreview(InventoryItemResponse item)
    {
        string description = GetEquipmentDescription(item);
        if (!equipmentServerBacked || item == null)
        {
            return description;
        }
        if (equipmentUpgradePreviewRequestId != item.playerEquipmentId
            || equipmentUpgradePreviewRunning)
        {
            return description + "\n升级消耗：正在读取...";
        }
        if (!string.IsNullOrEmpty(equipmentUpgradePreviewError))
        {
            return description + "\n升级消耗读取失败：" + equipmentUpgradePreviewError;
        }
        if (equipmentUpgradePreview == null)
        {
            return description;
        }
        if (equipmentUpgradePreview.maxLevelReached)
        {
            return description + $"\n已达到最高等级 {equipmentUpgradePreview.maxLevel}";
        }

        List<string> costs = new List<string>
        {
            $"金币 ×{equipmentUpgradePreview.goldCost}（持有 {equipmentUpgradePreview.availableGold}）"
        };
        EquipmentUpgradeResourceCostPreviewData[] resourceCosts = equipmentUpgradePreview.resourceCosts
            ?? new EquipmentUpgradeResourceCostPreviewData[0];
        for (int i = 0; i < resourceCosts.Length; i++)
        {
            EquipmentUpgradeResourceCostPreviewData resourceCost = resourceCosts[i];
            if (resourceCost == null || resourceCost.amount <= 0L)
            {
                continue;
            }
            string displayName = string.IsNullOrWhiteSpace(resourceCost.itemName)
                ? resourceCost.itemKey
                : resourceCost.itemName;
            costs.Add($"{displayName} ×{resourceCost.amount}（持有 {resourceCost.availableAmount}）");
        }
        if (equipmentUpgradePreview.sameEquipmentCount > 0)
        {
            costs.Add($"同款1级装备 ×{equipmentUpgradePreview.sameEquipmentCount}");
        }
        return description
            + $"\n升至等级 {equipmentUpgradePreview.targetLevel}：{string.Join("，", costs)}";
    }

    private void ResetEquipmentUpgradePreview()
    {
        equipmentUpgradePreview = null;
        equipmentUpgradePreviewRequestId = 0L;
        equipmentUpgradePreviewRunning = false;
        equipmentUpgradePreviewError = string.Empty;
    }

    private void EnsureEquipmentUpgradePreview(InventoryItemResponse item)
    {
        if (!equipmentServerBacked
            || item == null
            || item.playerEquipmentId <= 0L
            || equipmentRefreshRunning
            || equipmentMutationRunning)
        {
            return;
        }
        if (equipmentUpgradePreviewRequestId == item.playerEquipmentId
            && (equipmentUpgradePreviewRunning
                || equipmentUpgradePreview != null
                || !string.IsNullOrEmpty(equipmentUpgradePreviewError)))
        {
            return;
        }

        equipmentUpgradePreview = null;
        equipmentUpgradePreviewRequestId = item.playerEquipmentId;
        equipmentUpgradePreviewRunning = true;
        equipmentUpgradePreviewError = string.Empty;
        StartCoroutine(RefreshEquipmentUpgradePreview(item.playerEquipmentId));
    }

    private IEnumerator RefreshEquipmentUpgradePreview(long playerEquipmentId)
    {
        UnityWebRequest request = CreateAuthorizedGet($"/equipment/{playerEquipmentId}/upgrade-preview");
        if (request == null)
        {
            if (equipmentUpgradePreviewRequestId == playerEquipmentId)
            {
                equipmentUpgradePreviewRunning = false;
                equipmentUpgradePreviewError = requestCreationError;
                RenderEquipmentScenePanel(GetEquipmentPanelRoot());
            }
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (equipmentUpgradePreviewRequestId != playerEquipmentId
                || equipmentPanelObject == null)
            {
                yield break;
            }
            if (HandleUnauthorizedResponse(request))
            {
                equipmentUpgradePreviewRunning = false;
                yield break;
            }

            EquipmentUpgradePreviewApiResponse response = ParseJson<EquipmentUpgradePreviewApiResponse>(
                request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success
                || response == null
                || response.code != 200
                || response.data == null
                || response.data.playerEquipmentId != playerEquipmentId)
            {
                equipmentUpgradePreviewError = response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response);
            }
            else
            {
                equipmentUpgradePreview = response.data;
                equipmentUpgradePreviewError = string.Empty;
            }
        }

        equipmentUpgradePreviewRunning = false;
        RenderEquipmentScenePanel(GetEquipmentPanelRoot());
    }

    private string GetEquipmentTypeLabel(InventoryItemResponse item)
    {
        string slot = GetEquipmentSlot(item);
        return GetEquipmentSlotLabel(slot);
    }

    private static string GetEquipmentQualityLabel(string quality)
    {
        if (string.Equals(quality, "COMMON", StringComparison.OrdinalIgnoreCase)) return "普通";
        if (string.Equals(quality, "RARE", StringComparison.OrdinalIgnoreCase)) return "稀有";
        if (string.Equals(quality, "EPIC", StringComparison.OrdinalIgnoreCase)) return "史诗";
        if (string.Equals(quality, "LEGENDARY", StringComparison.OrdinalIgnoreCase)) return "传说";
        return string.IsNullOrEmpty(quality) ? "装备" : (FrontendTextLocalization.ContainsAsciiLetter(quality) ? "装备" : quality);
    }

    private static string GetEquipmentSlotLabel(string slotId)
    {
        if (slotId == "weapon") return "武器";
        if (slotId == "helmet") return "头盔";
        if (slotId == "armor") return "护甲";
        if (slotId == "boots") return "靴子";
        if (slotId == "gloves") return "手套";
        return "装备";
    }

    private static string GetSlotIcon(string slotId)
    {
        if (slotId == "weapon") return "剑";
        if (slotId == "helmet") return "盔";
        if (slotId == "armor") return "甲";
        if (slotId == "boots") return "靴";
        if (slotId == "gloves") return "套";
        return "装";
    }

    private Color GetEquipmentColor(InventoryItemResponse item)
    {
        return GetEquipmentSlotColor(GetEquipmentSlot(item));
    }

    private static Color GetEquipmentSlotColor(string slot)
    {
        if (slot == "weapon") return new Color(0.83f, 0.38f, 0.24f, 1f);
        if (slot == "helmet") return new Color(0.76f, 0.57f, 0.22f, 1f);
        if (slot == "armor") return new Color(0.28f, 0.55f, 0.76f, 1f);
        if (slot == "boots") return new Color(0.32f, 0.62f, 0.43f, 1f);
        if (slot == "gloves") return new Color(0.63f, 0.42f, 0.68f, 1f);
        return new Color(0.55f, 0.55f, 0.55f, 1f);
    }

    private static string ToPascalCase(string value)
    {
        return string.IsNullOrEmpty(value) ? string.Empty : char.ToUpper(value[0]) + value.Substring(1);
    }

    private PlayerNpcResponse GetCurrentEquipmentNpc()
    {
        if (serverPlayerNpcs == null || serverPlayerNpcs.Length == 0)
        {
            return null;
        }

        selectedEquipmentNpcIndex = Mathf.Clamp(selectedEquipmentNpcIndex, 0, serverPlayerNpcs.Length - 1);
        return serverPlayerNpcs[selectedEquipmentNpcIndex];
    }

    private void ConfigureServerNpcControls(Transform root)
    {
        Transform panel = FindSceneTransform(root, "EquipmentPanel");
        if (panel == null)
        {
            return;
        }

        Button previous = FindSceneComponent<Button>(panel, "EquipmentNpcPreviousButton");
        Button next = FindSceneComponent<Button>(panel, "EquipmentNpcNextButton");
        Button upgrade = FindSceneComponent<Button>(panel, "EquipmentNpcUpgradeButton");
        if (!equipmentServerBacked)
        {
            if (previous != null) previous.gameObject.SetActive(false);
            if (next != null) next.gameObject.SetActive(false);
            if (upgrade != null) upgrade.gameObject.SetActive(false);
            return;
        }

        if (previous == null)
        {
            previous = CreateButton(panel, "EquipmentNpcPreviousButton", "<", new Color(0.58f, 0.38f, 0.66f, 1f), Color.white);
            SetRect(previous.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(282f, -40f), new Vector2(40f, 42f));
            ConfigureClearButtonLabel(previous, 22);
        }
        if (next == null)
        {
            next = CreateButton(panel, "EquipmentNpcNextButton", ">", new Color(0.58f, 0.38f, 0.66f, 1f), Color.white);
            SetRect(next.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(616f, -40f), new Vector2(40f, 42f));
            ConfigureClearButtonLabel(next, 22);
        }
        if (upgrade == null)
        {
            upgrade = CreateButton(panel, "EquipmentNpcUpgradeButton", "居民升级", new Color(0.86f, 0.48f, 0.20f, 1f), Color.white);
            SetRect(upgrade.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(664f, -40f), new Vector2(104f, 42f));
            ConfigureClearButtonLabel(upgrade, 16);
        }

        previous.gameObject.SetActive(true);
        next.gameObject.SetActive(true);
        upgrade.gameObject.SetActive(true);
        PlayerNpcResponse selectedNpc = GetCurrentEquipmentNpc();
        EnsureNpcAutoUpgradePreview(selectedNpc);
        bool npcMaxLevelReached = selectedNpc != null
            && selectedNpc.maxLevel > 0
            && selectedNpc.level >= selectedNpc.maxLevel;
        bool canSwitch = serverPlayerNpcs.Length > 1 && !equipmentRefreshRunning && !equipmentMutationRunning;
        previous.interactable = canSwitch;
        next.interactable = canSwitch;
        bool autoReady = selectedNpc != null
            && npcAutoUpgradePreviewId == selectedNpc.playerNpcId
            && npcAutoUpgradePreview != null
            && !string.IsNullOrEmpty(npcAutoUpgradePreview.planFingerprint)
            && npcAutoUpgradePreview.payableTargetLevel > npcAutoUpgradePreview.currentLevel;
        bool autoUnavailable = selectedNpc != null
            && npcAutoUpgradePreviewId == selectedNpc.playerNpcId
            && npcAutoUpgradePreview != null
            && !string.IsNullOrEmpty(npcAutoUpgradePreview.planFingerprint)
            && npcAutoUpgradePreview.payableTargetLevel <= npcAutoUpgradePreview.currentLevel;
        bool autoLoading = selectedNpc != null
            && npcAutoUpgradePreviewId == selectedNpc.playerNpcId
            && npcAutoUpgradePreviewRunning;
        upgrade.interactable = selectedNpc != null
            && !equipmentRefreshRunning
            && !equipmentMutationRunning
            && !autoLoading
            && !autoUnavailable
            && !npcMaxLevelReached;
        SetSceneButtonText(upgrade, npcMaxLevelReached ? "已满级" : autoLoading ? "读取中..." : autoUnavailable ? "无法升级" : autoReady ? "一键升级" : "居民升级");
        BindSceneButton(previous, () => ChangeEquipmentNpc(-1));
        BindSceneButton(next, () => ChangeEquipmentNpc(1));
        Action upgradeAction = autoReady
            ? (Action)(() => StartCoroutine(AutoUpgradeCurrentServerNpc(selectedNpc)))
            : UpgradeCurrentServerNpc;
        BindSceneButton(upgrade, upgradeAction);
    }

    private void ChangeEquipmentNpc(int offset)
    {
        if (serverPlayerNpcs == null || serverPlayerNpcs.Length == 0 || equipmentRefreshRunning || equipmentMutationRunning)
        {
            return;
        }

        selectedEquipmentNpcIndex = (selectedEquipmentNpcIndex + offset + serverPlayerNpcs.Length) % serverPlayerNpcs.Length;
        selectedEquipmentItemKey = string.Empty;
        equipmentPageIndex = 0;
        equipmentRefreshRunning = true;
        equipmentStatusMessage = "正在读取所选居民的装备...";
        RenderEquipmentScenePanel(GetEquipmentPanelRoot());
        StartCoroutine(ChangeSelectedNpcLoadout());
    }

    private IEnumerator ChangeSelectedNpcLoadout()
    {
        serverEquipmentLoadout = null;
        yield return RefreshSelectedNpcLoadout();
        if (!equipmentRefreshRunning)
        {
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
            yield break;
        }

        RebuildServerEquipmentItems();
        equipmentRefreshRunning = false;
        PlayerNpcResponse npc = GetCurrentEquipmentNpc();
        equipmentStatusMessage = npc == null ? "当前没有居民" : $"已切换到 {npc.name}";
        RenderEquipmentScenePanel(GetEquipmentPanelRoot());
    }

    private void ConfigureEquipmentPaging(Transform root, int pageCount)
    {
        Transform panel = FindSceneTransform(root, "EquipmentPanel");
        if (panel == null)
        {
            return;
        }

        SetSceneText(root, "EquipmentListTitle", pageCount > 1 ? $"装备物品 {equipmentPageIndex + 1}/{pageCount}" : "装备物品");
        Button previous = FindSceneComponent<Button>(panel, "EquipmentPagePreviousButton");
        Button next = FindSceneComponent<Button>(panel, "EquipmentPageNextButton");
        if (previous == null)
        {
            previous = CreateButton(panel, "EquipmentPagePreviousButton", "<", new Color(0.67f, 0.46f, 0.24f, 1f), Color.white);
            SetRect(previous.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(850f, -178f), new Vector2(42f, 36f));
            ConfigureClearButtonLabel(previous, 20);
        }
        if (next == null)
        {
            next = CreateButton(panel, "EquipmentPageNextButton", ">", new Color(0.67f, 0.46f, 0.24f, 1f), Color.white);
            SetRect(next.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(902f, -178f), new Vector2(42f, 36f));
            ConfigureClearButtonLabel(next, 20);
        }

        previous.gameObject.SetActive(pageCount > 1);
        next.gameObject.SetActive(pageCount > 1);
        previous.interactable = equipmentPageIndex > 0 && !equipmentRefreshRunning && !equipmentMutationRunning;
        next.interactable = equipmentPageIndex + 1 < pageCount && !equipmentRefreshRunning && !equipmentMutationRunning;
        BindSceneButton(previous, () => ChangeEquipmentPage(-1));
        BindSceneButton(next, () => ChangeEquipmentPage(1));
    }

    private void ChangeEquipmentPage(int offset)
    {
        equipmentPageIndex += offset;
        selectedEquipmentItemKey = string.Empty;
        RenderEquipmentScenePanel(GetEquipmentPanelRoot());
    }

    private void ConfigureServerEquipmentDetailActions(Transform root, InventoryItemResponse item)
    {
        Transform detailContent = FindSceneTransform(root, "EquipmentDetailContent");
        Button action = FindSceneComponent<Button>(root, "EquipmentActionButton");
        Button upgrade = FindSceneComponent<Button>(root, "EquipmentUpgradeButton");
        Button lockButton = FindSceneComponent<Button>(root, "EquipmentLockButton");
        if (detailContent == null || action == null)
        {
            return;
        }

        if (!equipmentServerBacked)
        {
            SetRect(action.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(136f, -454f), new Vector2(202f, 50f));
            if (upgrade != null) upgrade.gameObject.SetActive(false);
            if (lockButton != null) lockButton.gameObject.SetActive(false);
            return;
        }

        SetRect(action.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(28f, -454f), new Vector2(128f, 50f));
        ConfigureClearButtonLabel(action, 18);
        if (upgrade == null)
        {
            upgrade = CreateButton(detailContent, "EquipmentUpgradeButton", "升级", new Color(0.86f, 0.49f, 0.18f, 1f), Color.white);
            SetRect(upgrade.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(173f, -454f), new Vector2(128f, 50f));
            ConfigureClearButtonLabel(upgrade, 18);
        }
        if (lockButton == null)
        {
            lockButton = CreateButton(detailContent, "EquipmentLockButton", "锁定", new Color(0.44f, 0.43f, 0.60f, 1f), Color.white);
            SetRect(lockButton.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(318f, -454f), new Vector2(128f, 50f));
            ConfigureClearButtonLabel(lockButton, 18);
        }

        upgrade.gameObject.SetActive(true);
        lockButton.gameObject.SetActive(true);
        EnsureEquipmentAutoUpgradePreview(item);
        bool previewMatches = item != null
            && equipmentUpgradePreviewRequestId == item.playerEquipmentId;
        bool previewLoading = previewMatches && equipmentUpgradePreviewRunning;
        bool maxLevelReached = previewMatches
            && equipmentUpgradePreview != null
            && equipmentUpgradePreview.maxLevelReached;
        bool autoPreviewMatches = item != null && equipmentAutoUpgradePreviewId == item.playerEquipmentId;
        bool autoPreviewLoading = autoPreviewMatches && equipmentAutoUpgradePreviewRunning;
        bool autoReady = autoPreviewMatches && equipmentAutoUpgradePreview != null
            && !string.IsNullOrEmpty(equipmentAutoUpgradePreview.planFingerprint)
            && equipmentAutoUpgradePreview.payableTargetLevel > equipmentAutoUpgradePreview.currentLevel;
        bool autoUnavailable = autoPreviewMatches && equipmentAutoUpgradePreview != null
            && !string.IsNullOrEmpty(equipmentAutoUpgradePreview.planFingerprint)
            && equipmentAutoUpgradePreview.payableTargetLevel <= equipmentAutoUpgradePreview.currentLevel;
        upgrade.interactable = item != null
            && !equipmentMutationRunning
            && !equipmentRefreshRunning
            && !previewLoading
            && !maxLevelReached
            && !autoPreviewLoading
            && !autoUnavailable;
        lockButton.interactable = item != null && !equipmentMutationRunning && !equipmentRefreshRunning;
        SetSceneButtonText(upgrade, maxLevelReached ? "已满级" : autoPreviewLoading ? "读取中..." : autoUnavailable ? "无法升级" : autoReady ? "一键升级" : (previewLoading ? "读取中..." : "升级"));
        SetSceneButtonText(lockButton, item != null && item.locked ? "解锁" : "锁定");
        Action upgradeAction = autoReady
            ? (Action)(() => StartCoroutine(AutoUpgradeServerEquipment(item)))
            : () => UpgradeServerEquipment(item);
        BindSceneButton(upgrade, upgradeAction);
        BindSceneButton(lockButton, () => ToggleServerEquipmentLock(item));
    }

    private void UpgradeServerEquipment(InventoryItemResponse item)
    {
        if (item != null && !equipmentMutationRunning && !equipmentRefreshRunning)
        {
            StartCoroutine(UpgradeServerEquipmentRequest(item.playerEquipmentId));
        }
    }

    private void ToggleServerEquipmentLock(InventoryItemResponse item)
    {
        if (item != null && !equipmentMutationRunning && !equipmentRefreshRunning)
        {
            StartCoroutine(SetServerEquipmentLock(item.playerEquipmentId, !item.locked));
        }
    }

    private void UpgradeCurrentServerNpc()
    {
        PlayerNpcResponse npc = GetCurrentEquipmentNpc();
        if (npc != null
            && (npc.maxLevel <= 0 || npc.level < npc.maxLevel)
            && !equipmentMutationRunning
            && !equipmentRefreshRunning)
        {
            StartCoroutine(UpgradeServerNpcRequest(npc.playerNpcId));
        }
    }

    private void ResetEquipmentAutoUpgradePreviews()
    {
        equipmentAutoUpgradePreview = null;
        equipmentAutoUpgradePreviewId = 0L;
        equipmentAutoUpgradePreviewRunning = false;
        equipmentAutoUpgradePreviewError = string.Empty;
        npcAutoUpgradePreview = null;
        npcAutoUpgradePreviewId = 0L;
        npcAutoUpgradePreviewRunning = false;
        npcAutoUpgradePreviewError = string.Empty;
        npcAutoUpgradeIdempotencyKey = string.Empty;
        equipmentAutoUpgradeIdempotencyKey = string.Empty;
        npcAutoUpgradeRequestSignature = string.Empty;
        equipmentAutoUpgradeRequestSignature = string.Empty;
    }

    private void EnsureNpcAutoUpgradePreview(PlayerNpcResponse npc)
    {
        if (!equipmentServerBacked || npc == null || npc.playerNpcId <= 0L || equipmentRefreshRunning || equipmentMutationRunning)
        {
            return;
        }
        if (npc.maxLevel > 0 && npc.level >= npc.maxLevel)
        {
            npcAutoUpgradePreview = null;
            npcAutoUpgradePreviewId = npc.playerNpcId;
            npcAutoUpgradePreviewRunning = false;
            npcAutoUpgradePreviewError = "居民已满级";
            return;
        }

        if (npcAutoUpgradePreviewId == npc.playerNpcId
            && (npcAutoUpgradePreviewRunning || npcAutoUpgradePreview != null || !string.IsNullOrEmpty(npcAutoUpgradePreviewError)))
        {
            return;
        }

        if (npcAutoUpgradePreviewId != npc.playerNpcId)
        {
            npcAutoUpgradeIdempotencyKey = string.Empty;
            npcAutoUpgradeRequestSignature = string.Empty;
        }

        npcAutoUpgradePreview = null;
        npcAutoUpgradePreviewId = npc.playerNpcId;
        npcAutoUpgradePreviewRunning = true;
        npcAutoUpgradePreviewError = string.Empty;
        int targetLevel = npc.maxLevel > npc.level ? npc.maxLevel : 100;
        StartCoroutine(LoadNpcAutoUpgradePreview(npc.playerNpcId, targetLevel));
    }

    private IEnumerator LoadNpcAutoUpgradePreview(long playerNpcId, int targetLevel)
    {
        UnityWebRequest request = CreateAuthorizedGet($"/npc/{playerNpcId}/auto-upgrade-preview?targetLevel={Mathf.Clamp(targetLevel, 1, 100)}");
        if (request == null)
        {
            npcAutoUpgradePreviewRunning = false;
            npcAutoUpgradePreviewError = requestCreationError;
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (npcAutoUpgradePreviewId != playerNpcId)
            {
                yield break;
            }
            if (HandleUnauthorizedResponse(request))
            {
                npcAutoUpgradePreviewRunning = false;
                yield break;
            }

            NpcAutoUpgradePreviewApiResponse response = ParseJson<NpcAutoUpgradePreviewApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                npcAutoUpgradePreviewError = response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response);
            }
            else
            {
                npcAutoUpgradePreview = response.data;
                npcAutoUpgradePreviewError = string.Empty;
            }
        }

        npcAutoUpgradePreviewRunning = false;
        if (equipmentPanelObject != null) RenderEquipmentScenePanel(GetEquipmentPanelRoot());
    }

    private IEnumerator AutoUpgradeCurrentServerNpc(PlayerNpcResponse npc)
    {
        if (npc == null || npcAutoUpgradePreview == null || string.IsNullOrEmpty(npcAutoUpgradePreview.planFingerprint))
        {
            UpgradeCurrentServerNpc();
            yield break;
        }
        if (npcAutoUpgradePreview.payableTargetLevel <= npcAutoUpgradePreview.currentLevel)
        {
            equipmentStatusMessage = "当前资源不足或居民已无法继续升级";
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
            yield break;
        }

        equipmentMutationRunning = true;
        equipmentStatusMessage = "正在执行居民一键升级...";
        RenderEquipmentScenePanel(GetEquipmentPanelRoot());
        AutoUpgradeRequestPayload payload = new AutoUpgradeRequestPayload
        {
            playerNpcId = npc.playerNpcId,
            expectedCurrentLevel = Mathf.Max(1, npcAutoUpgradePreview.currentLevel),
            targetLevel = npcAutoUpgradePreview.requestedTargetLevel > npcAutoUpgradePreview.currentLevel
                ? npcAutoUpgradePreview.requestedTargetLevel
                : npcAutoUpgradePreview.payableTargetLevel,
            planFingerprint = npcAutoUpgradePreview.planFingerprint
        };
        yield return SendAutoUpgradeRequest("/npc/auto-upgrade", JsonUtility.ToJson(payload), true, npc.playerNpcId);
    }

    private void EnsureEquipmentAutoUpgradePreview(InventoryItemResponse item)
    {
        if (!equipmentServerBacked || item == null || item.playerEquipmentId <= 0L || equipmentRefreshRunning || equipmentMutationRunning)
        {
            return;
        }

        if (equipmentAutoUpgradePreviewId == item.playerEquipmentId
            && (equipmentAutoUpgradePreviewRunning || equipmentAutoUpgradePreview != null || !string.IsNullOrEmpty(equipmentAutoUpgradePreviewError)))
        {
            return;
        }

        if (equipmentAutoUpgradePreviewId != item.playerEquipmentId)
        {
            equipmentAutoUpgradeIdempotencyKey = string.Empty;
            equipmentAutoUpgradeRequestSignature = string.Empty;
        }

        equipmentAutoUpgradePreview = null;
        equipmentAutoUpgradePreviewId = item.playerEquipmentId;
        equipmentAutoUpgradePreviewRunning = true;
        equipmentAutoUpgradePreviewError = string.Empty;
        StartCoroutine(LoadEquipmentAutoUpgradePreview(item.playerEquipmentId));
    }

    private IEnumerator LoadEquipmentAutoUpgradePreview(long playerEquipmentId)
    {
        UnityWebRequest request = CreateAuthorizedGet($"/equipment/{playerEquipmentId}/auto-upgrade-preview?targetLevel=100");
        if (request == null)
        {
            equipmentAutoUpgradePreviewRunning = false;
            equipmentAutoUpgradePreviewError = requestCreationError;
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (equipmentAutoUpgradePreviewId != playerEquipmentId)
            {
                yield break;
            }
            if (HandleUnauthorizedResponse(request))
            {
                equipmentAutoUpgradePreviewRunning = false;
                yield break;
            }

            EquipmentAutoUpgradePreviewApiResponse response = ParseJson<EquipmentAutoUpgradePreviewApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                equipmentAutoUpgradePreviewError = response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response);
            }
            else
            {
                equipmentAutoUpgradePreview = response.data;
                equipmentAutoUpgradePreviewError = string.Empty;
            }
        }

        equipmentAutoUpgradePreviewRunning = false;
        if (equipmentPanelObject != null) RenderEquipmentScenePanel(GetEquipmentPanelRoot());
    }

    private IEnumerator AutoUpgradeServerEquipment(InventoryItemResponse item)
    {
        if (item == null || equipmentAutoUpgradePreview == null || string.IsNullOrEmpty(equipmentAutoUpgradePreview.planFingerprint))
        {
            UpgradeServerEquipment(item);
            yield break;
        }
        if (equipmentAutoUpgradePreview.payableTargetLevel <= equipmentAutoUpgradePreview.currentLevel)
        {
            equipmentStatusMessage = "当前资源不足或装备已无法继续升级";
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
            yield break;
        }

        equipmentMutationRunning = true;
        equipmentStatusMessage = "正在执行装备一键升级...";
        RenderEquipmentScenePanel(GetEquipmentPanelRoot());
        AutoUpgradeRequestPayload payload = new AutoUpgradeRequestPayload
        {
            playerEquipmentId = item.playerEquipmentId,
            expectedCurrentLevel = Mathf.Max(1, equipmentAutoUpgradePreview.currentLevel),
            targetLevel = equipmentAutoUpgradePreview.requestedTargetLevel > equipmentAutoUpgradePreview.currentLevel
                ? equipmentAutoUpgradePreview.requestedTargetLevel
                : equipmentAutoUpgradePreview.payableTargetLevel,
            planFingerprint = equipmentAutoUpgradePreview.planFingerprint
        };
        yield return SendAutoUpgradeRequest("/equipment/auto-upgrade", JsonUtility.ToJson(payload), false, item.playerEquipmentId);
    }

    private IEnumerator SendAutoUpgradeRequest(string path, string json, bool npc, long targetId)
    {
        if (!ApiRequestUtility.TryCreateJsonRequest(path, UnityWebRequest.kHttpVerbPOST, json, true, out UnityWebRequest request, out string error))
        {
            equipmentMutationRunning = false;
            equipmentStatusMessage = error;
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
            yield break;
        }

        string requestSignature = GameSession.PlayerId + ":" + path + ":" + targetId + ":" + json;
        if (!autoUpgradePendingKeys.TryGetValue(requestSignature, out string idempotencyKey)
            || !Guid.TryParse(idempotencyKey, out _))
        {
            idempotencyKey = Guid.NewGuid().ToString("D");
            autoUpgradePendingKeys[requestSignature] = idempotencyKey;
        }
        if (npc)
        {
            npcAutoUpgradeIdempotencyKey = idempotencyKey;
            npcAutoUpgradeRequestSignature = requestSignature;
        }
        else
        {
            equipmentAutoUpgradeIdempotencyKey = idempotencyKey;
            equipmentAutoUpgradeRequestSignature = requestSignature;
        }
        ApiRequestUtility.SetIdempotencyKey(request, idempotencyKey);
        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                equipmentMutationRunning = false;
                yield break;
            }

            AutoUpgradeApiResponse response = ParseJson<AutoUpgradeApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                bool definitiveRejection = response != null
                    && response.code != 200
                    && request.responseCode > 0
                    && request.responseCode < 500;
                equipmentMutationRunning = false;
                equipmentStatusMessage = (npc ? "居民" : "装备") + "一键升级失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response));
                if (definitiveRejection)
                {
                    autoUpgradePendingKeys.Remove(requestSignature);
                    if (npc)
                    {
                        npcAutoUpgradePreview = null;
                        npcAutoUpgradePreviewId = 0L;
                        npcAutoUpgradePreviewRunning = false;
                        npcAutoUpgradePreviewError = string.Empty;
                        npcAutoUpgradeIdempotencyKey = string.Empty;
                        npcAutoUpgradeRequestSignature = string.Empty;
                    }
                    else
                    {
                        equipmentAutoUpgradePreview = null;
                        equipmentAutoUpgradePreviewId = 0L;
                        equipmentAutoUpgradePreviewRunning = false;
                        equipmentAutoUpgradePreviewError = string.Empty;
                        equipmentAutoUpgradeIdempotencyKey = string.Empty;
                        equipmentAutoUpgradeRequestSignature = string.Empty;
                    }
                }
                RenderEquipmentScenePanel(GetEquipmentPanelRoot());
                yield break;
            }

            autoUpgradePendingKeys.Remove(requestSignature);
            equipmentStatusMessage = (npc ? "居民" : "装备") + $"已升级到等级 {response.data.reachedLevel}，资源已由服务器结算";
        }

        equipmentMutationRunning = false;
        ResetEquipmentAutoUpgradePreviews();
        equipmentRefreshRunning = true;
        yield return RefreshGeneralInventoryAfterEquipmentMutation();
        yield return GetEquipmentInventoryList();
        RenderEquipmentScenePanel(GetEquipmentPanelRoot());
    }

    private IEnumerator EquipServerEquipment(long playerNpcId, long playerEquipmentId)
    {
        EquipmentRelationRequest payload = new EquipmentRelationRequest
        {
            playerNpcId = playerNpcId,
            playerEquipmentId = playerEquipmentId
        };
        yield return SendServerEquipmentRelation("/equipment/equip", payload, "正在穿戴装备...", "装备已穿戴");
    }

    private IEnumerator UnequipServerEquipment(long playerNpcId, long playerEquipmentId)
    {
        EquipmentRelationRequest payload = new EquipmentRelationRequest
        {
            playerNpcId = playerNpcId,
            playerEquipmentId = playerEquipmentId
        };
        yield return SendServerEquipmentRelation("/equipment/unequip", payload, "正在卸下装备...", "装备已卸下");
    }

    private IEnumerator SendServerEquipmentRelation(
        string path,
        EquipmentRelationRequest payload,
        string pendingMessage,
        string successMessage)
    {
        if (equipmentMutationRunning)
        {
            yield break;
        }

        equipmentMutationRunning = true;
        equipmentStatusMessage = pendingMessage;
        RenderEquipmentScenePanel(GetEquipmentPanelRoot());
        if (!TryCreateAuthorizedPost(path, JsonUtility.ToJson(payload), out UnityWebRequest request, out string error))
        {
            equipmentMutationRunning = false;
            equipmentStatusMessage = error;
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                equipmentMutationRunning = false;
                yield break;
            }

            EquipmentLoadoutApiResponse response = ParseJson<EquipmentLoadoutApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                equipmentMutationRunning = false;
                equipmentStatusMessage = "装备操作失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response));
                RenderEquipmentScenePanel(GetEquipmentPanelRoot());
                yield break;
            }

            serverEquipmentLoadout = response.data;
        }

        equipmentRefreshRunning = true;
        yield return RefreshServerEquipmentWarehouse();
        equipmentMutationRunning = false;
        if (equipmentRefreshRunning)
        {
            RebuildServerEquipmentItems();
            equipmentRefreshRunning = false;
            equipmentStatusMessage = successMessage;
        }
        RenderEquipmentScenePanel(GetEquipmentPanelRoot());
    }

    private IEnumerator UpgradeServerEquipmentRequest(long playerEquipmentId)
    {
        equipmentMutationRunning = true;
        equipmentStatusMessage = "正在升级装备...";
        RenderEquipmentScenePanel(GetEquipmentPanelRoot());
        EquipmentInstanceRequest payload = new EquipmentInstanceRequest { playerEquipmentId = playerEquipmentId };
        if (!TryCreateAuthorizedPost("/equipment/upgrade", JsonUtility.ToJson(payload), out UnityWebRequest request, out string error))
        {
            equipmentMutationRunning = false;
            equipmentStatusMessage = error;
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
            yield break;
        }

        EquipmentUpgradeData result = null;
        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                equipmentMutationRunning = false;
                yield break;
            }

            EquipmentUpgradeApiResponse response = ParseJson<EquipmentUpgradeApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                equipmentMutationRunning = false;
                equipmentStatusMessage = "装备升级失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response));
                RenderEquipmentScenePanel(GetEquipmentPanelRoot());
                yield break;
            }
            result = response.data;
        }

        SyncEquipmentUpgradeResourceBalances(result);
        equipmentMutationRunning = false;
        ResetEquipmentAutoUpgradePreviews();
        equipmentRefreshRunning = true;
        yield return RefreshGeneralInventoryAfterEquipmentMutation();
        yield return GetEquipmentInventoryList();
        string resourceCost = FormatEquipmentUpgradeResourceCosts(result.resourceCosts);
        string materialCost = result.sameEquipmentCount > 0
            ? $"，消耗同款装备 {result.sameEquipmentCount} 件"
            : string.Empty;
        equipmentStatusMessage = $"{result.name} 已升级到等级 {result.level}，消耗金币 {result.goldCost}{resourceCost}{materialCost}";
        RenderEquipmentScenePanel(GetEquipmentPanelRoot());
    }

    private void SyncEquipmentUpgradeResourceBalances(EquipmentUpgradeData result)
    {
        EquipmentUpgradeResourceCostData[] resourceCosts = result.resourceCosts
            ?? new EquipmentUpgradeResourceCostData[0];
        InventoryItemResponse[] balances = new InventoryItemResponse[resourceCosts.Length + 1];
        balances[0] = new InventoryItemResponse
        {
            itemKey = "gold",
            quantity = Mathf.Max(0, result.remainingGold)
        };
        for (int i = 0; i < resourceCosts.Length; i++)
        {
            EquipmentUpgradeResourceCostData resourceCost = resourceCosts[i];
            balances[i + 1] = new InventoryItemResponse
            {
                itemKey = resourceCost != null ? resourceCost.itemKey : string.Empty,
                quantity = resourceCost == null
                    ? 0
                    : ClampEquipmentInventoryQuantity(resourceCost.remainingAmount)
            };
        }
        SyncInventoryResourceValues(balances);
    }

    private static int ClampEquipmentInventoryQuantity(long quantity)
    {
        if (quantity <= 0L) return 0;
        return quantity >= int.MaxValue ? int.MaxValue : (int)quantity;
    }

    private static string FormatEquipmentUpgradeResourceCosts(
        EquipmentUpgradeResourceCostData[] resourceCosts)
    {
        if (resourceCosts == null || resourceCosts.Length == 0)
        {
            return string.Empty;
        }

        List<string> descriptions = new List<string>();
        for (int i = 0; i < resourceCosts.Length; i++)
        {
            EquipmentUpgradeResourceCostData resourceCost = resourceCosts[i];
            if (resourceCost == null || resourceCost.amount <= 0L)
            {
                continue;
            }
            string displayName = string.IsNullOrWhiteSpace(resourceCost.itemName)
                ? resourceCost.itemKey
                : resourceCost.itemName;
            descriptions.Add($"{displayName} ×{resourceCost.amount}");
        }
        return descriptions.Count == 0
            ? string.Empty
            : $"，消耗仓库资源 {string.Join("、", descriptions)}";
    }

    private IEnumerator RefreshGeneralInventoryAfterEquipmentMutation()
    {
        List<string> syncedCategories = new List<string>();
        List<string> failedCategories = new List<string>();
        yield return RefreshCardDrawInventoryState(syncedCategories, failedCategories);
        if (failedCategories.Count > 0)
        {
            SetStatus("升级成功，但通用背包同步失败："
                + string.Join("、", failedCategories.ToArray()));
        }
    }

    private IEnumerator SetServerEquipmentLock(long playerEquipmentId, bool locked)
    {
        equipmentMutationRunning = true;
        equipmentStatusMessage = locked ? "正在锁定装备..." : "正在解锁装备...";
        RenderEquipmentScenePanel(GetEquipmentPanelRoot());
        EquipmentLockRequest payload = new EquipmentLockRequest
        {
            playerEquipmentId = playerEquipmentId,
            locked = locked
        };
        if (!TryCreateAuthorizedPost("/equipment/lock", JsonUtility.ToJson(payload), out UnityWebRequest request, out string error))
        {
            equipmentMutationRunning = false;
            equipmentStatusMessage = error;
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                equipmentMutationRunning = false;
                yield break;
            }

            BasicApiResponse response = ParseJson<BasicApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200)
            {
                equipmentMutationRunning = false;
                equipmentStatusMessage = "装备锁定状态修改失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response));
                RenderEquipmentScenePanel(GetEquipmentPanelRoot());
                yield break;
            }
        }

        equipmentMutationRunning = false;
        equipmentRefreshRunning = true;
        yield return GetEquipmentInventoryList();
        equipmentStatusMessage = locked ? "装备已锁定" : "装备已解锁";
        RenderEquipmentScenePanel(GetEquipmentPanelRoot());
    }

    private IEnumerator UpgradeServerNpcRequest(long playerNpcId)
    {
        equipmentMutationRunning = true;
        equipmentStatusMessage = "正在升级居民...";
        RenderEquipmentScenePanel(GetEquipmentPanelRoot());
        PlayerNpcInstanceRequest payload = new PlayerNpcInstanceRequest { playerNpcId = playerNpcId };
        if (!TryCreateAuthorizedPost("/npc/upgrade", JsonUtility.ToJson(payload), out UnityWebRequest request, out string error))
        {
            equipmentMutationRunning = false;
            equipmentStatusMessage = error;
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
            yield break;
        }

        NpcUpgradeData result = null;
        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                equipmentMutationRunning = false;
                yield break;
            }

            NpcUpgradeApiResponse response = ParseJson<NpcUpgradeApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                equipmentMutationRunning = false;
            equipmentStatusMessage = "居民升级失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response));
                RenderEquipmentScenePanel(GetEquipmentPanelRoot());
                yield break;
            }
            result = response.data;
        }

        PlayerPrefs.SetInt("Gold", ClampEquipmentInventoryQuantity(result.remainingGold));
        PlayerPrefs.Save();
        RefreshHomeResourceMetrics();
        equipmentMutationRunning = false;
        ResetEquipmentAutoUpgradePreviews();
        equipmentRefreshRunning = true;
        yield return RefreshGeneralInventoryAfterEquipmentMutation();
        yield return GetEquipmentInventoryList();
        equipmentStatusMessage = $"{result.name} 已升级到等级 {result.level}，消耗金币 {result.goldCost}";
        RenderEquipmentScenePanel(GetEquipmentPanelRoot());
    }

    private string BuildEquipmentFallbackText(InventoryItemResponse[] items)
    {
        List<InventoryItemResponse> equipment = new List<InventoryItemResponse>();
        if (items != null)
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] != null && (IsEquipmentItem(items[i]) || IsPetItem(items[i])))
                {
                    equipment.Add(items[i]);
                }
            }
        }

        if (equipment.Count == 0)
        {
            return "暂无装备物品。";
        }

        string text = "装备列表\n\n";
        for (int i = 0; i < equipment.Count; i++)
        {
            text += $"{i + 1}. {equipment[i].name}  ×{equipment[i].quantity}\n";
        }
        return text;
    }

    private void HideEquipmentPanel()
    {
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Equipment);
        if (panel != null)
        {
            panel.SetVisible(false);
        }

        equipmentPanelObject = null;
        equipmentRefreshRunning = false;
    }

#pragma warning disable 0649
    [Serializable]
    private class PlayerNpcListApiResponse : BasicApiResponse
    {
        public PlayerNpcResponse[] data;
    }

    [Serializable]
    private class PlayerNpcResponse
    {
        public long playerNpcId;
        public int npcId;
        public string npcCode;
        public string name;
        public string icon;
        public string modelKey;
        public int level;
        public int maxLevel;
        public string state;
        public long settledPlayerBuildingId;
        public float produceBonusRate;
    }

    [Serializable]
    private class EquipmentWarehouseApiResponse : BasicApiResponse
    {
        public InventoryItemResponse[] data;
    }

    [Serializable]
    private class EquipmentLoadoutApiResponse : BasicApiResponse
    {
        public EquipmentLoadoutData data;
    }

    [Serializable]
    private class EquipmentLoadoutData
    {
        public long playerNpcId;
        public int npcId;
        public int npcLevel;
        public int baseHealth;
        public int baseAttack;
        public int baseDefense;
        public int baseSpeed;
        public int equipmentHealthBonus;
        public int equipmentAttackBonus;
        public int equipmentDefenseBonus;
        public int equipmentSpeedBonus;
        public int totalHealth;
        public int totalAttack;
        public int totalDefense;
        public int totalSpeed;
        public EquipmentLoadoutItem[] equipment;
    }

    [Serializable]
    private class EquipmentLoadoutItem
    {
        public long relationId;
        public long playerEquipmentId;
        public int equipmentId;
        public string equipmentKey;
        public string name;
        public string equipmentType;
        public string quality;
        public string icon;
        public string modelKey;
        public int level;
        public bool locked;
        public int healthBonus;
        public int attackBonus;
        public int defenseBonus;
        public int speedBonus;
    }

    [Serializable]
    private class EquipmentRelationRequest
    {
        public long playerNpcId;
        public long playerEquipmentId;
    }

    [Serializable]
    private class EquipmentInstanceRequest
    {
        public long playerEquipmentId;
    }

    [Serializable]
    private class EquipmentLockRequest
    {
        public long playerEquipmentId;
        public bool locked;
    }

    [Serializable]
    private class PlayerNpcInstanceRequest
    {
        public long playerNpcId;
    }

    [Serializable]
    private class EquipmentUpgradeApiResponse : BasicApiResponse
    {
        public EquipmentUpgradeData data;
    }

    [Serializable]
    private class EquipmentUpgradePreviewApiResponse : BasicApiResponse
    {
        public EquipmentUpgradePreviewData data;
    }

    [Serializable]
    private class EquipmentUpgradePreviewData
    {
        public long playerEquipmentId;
        public int equipmentId;
        public string equipmentKey;
        public string name;
        public int currentLevel;
        public int targetLevel;
        public int maxLevel;
        public bool maxLevelReached;
        public long goldCost;
        public long availableGold;
        public int sameEquipmentCount;
        public EquipmentUpgradeResourceCostPreviewData[] resourceCosts;
    }

    [Serializable]
    private class EquipmentUpgradeResourceCostPreviewData
    {
        public string itemKey;
        public string itemName;
        public long amount;
        public long availableAmount;
    }

    [Serializable]
    private class EquipmentUpgradeData
    {
        public long upgradeRecordId;
        public long playerEquipmentId;
        public int equipmentId;
        public string equipmentKey;
        public string name;
        public int fromLevel;
        public int level;
        public string status;
        public int goldCost;
        public int sameEquipmentCount;
        public long[] consumedPlayerEquipmentIds;
        public int remainingGold;
        public EquipmentUpgradeResourceCostData[] resourceCosts;
        public int healthBonus;
        public int attackBonus;
        public int defenseBonus;
        public int speedBonus;
    }

    [Serializable]
    private class EquipmentUpgradeResourceCostData
    {
        public string itemKey;
        public string itemName;
        public long amount;
        public long remainingAmount;
    }

    [Serializable]
    private class NpcUpgradeApiResponse : BasicApiResponse
    {
        public NpcUpgradeData data;
    }

    [Serializable]
    private class NpcUpgradeData
    {
        public long upgradeRecordId;
        public long playerNpcId;
        public long npcId;
        public string npcCode;
        public string name;
        public string quality;
        public string combatAttribute;
        public int fromLevel;
        public int level;
        public long goldCost;
        public long fragmentCost;
        public string fragmentItemKey;
        public long remainingGold;
        public long remainingFragments;
        public long health;
        public long attack;
        public long defense;
        public long speed;
        public double produceBonusRate;
    }

    [Serializable]
    private class AutoUpgradeRequestPayload
    {
        public long playerNpcId;
        public long playerEquipmentId;
        public int expectedCurrentLevel;
        public int targetLevel;
        public string planFingerprint;
    }

    [Serializable]
    private class NpcAutoUpgradePreviewApiResponse : BasicApiResponse
    {
        public NpcAutoUpgradePreviewData data;
    }

    [Serializable]
    private class NpcAutoUpgradePreviewData
    {
        public long playerNpcId;
        public int currentLevel;
        public int requestedTargetLevel;
        public int payableTargetLevel;
        public string planFingerprint;
    }

    [Serializable]
    private class EquipmentAutoUpgradePreviewApiResponse : BasicApiResponse
    {
        public EquipmentAutoUpgradePreviewData data;
    }

    [Serializable]
    private class EquipmentAutoUpgradePreviewData
    {
        public long playerEquipmentId;
        public int currentLevel;
        public int requestedTargetLevel;
        public int payableTargetLevel;
        public string planFingerprint;
    }

    [Serializable]
    private class AutoUpgradeApiResponse : BasicApiResponse
    {
        public AutoUpgradeData data;
    }

    [Serializable]
    private class AutoUpgradeData
    {
        public long playerNpcId;
        public long playerEquipmentId;
        public int reachedLevel;
        public int level;
        public string outcome;
    }
#pragma warning restore 0649

    private struct EquipmentStats
    {
        public int Attack;
        public int Defense;
        public int Health;
        public int Speed;
        public int Power;
    }
}
