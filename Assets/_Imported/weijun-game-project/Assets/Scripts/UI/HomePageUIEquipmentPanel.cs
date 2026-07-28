using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public partial class HomePageUI
{
    private const int EquipmentRows = 6;

    private static readonly string[] EquipmentSlotIds = { "weapon", "armor", "accessory", "pet" };
    private static readonly string[] EquipmentSlotLabels = { "武器", "防具", "饰品", "灵宠" };

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
        HideGuestProfilePanel();
        HideFriendsPanel();
        HideSettingsPanel();

        activeEquipmentCategory = "all";
        selectedEquipmentItemKey = string.Empty;
        equipmentPageIndex = 0;
        equipmentStatusMessage = string.Empty;

        if (!HasAuthSession())
        {
            ShowEquipmentScenePanel(
                BuildLocalInventoryItems(),
                false,
                "游客装备已从本地存档读取");
            return;
        }

        equipmentRefreshRunning = true;
        ShowEquipmentScenePanel(
            new InventoryItemResponse[0],
            true,
            "正在读取服务器 NPC 与装备仓库...");
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
                equipmentStatusMessage = "NPC 列表读取失败：" + (response == null
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
            ? "装备仓库已刷新；当前账号还没有可穿戴装备的 NPC"
            : "服务器 NPC、装备仓库与穿戴数据已刷新";
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
                equipmentStatusMessage = "NPC 装备读取失败：" + (response == null
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
                    defenseBonus = loadoutItem.defenseBonus
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

    private void RenderEquipmentScenePanel(Transform root)
    {
        if (root == null)
        {
            return;
        }

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
                ? (selectedNpc == null ? "服务器装备 · 暂无 NPC" : $"{selectedNpc.name}  Lv.{selectedNpc.level}  {selectedEquipmentNpcIndex + 1}/{serverPlayerNpcs.Length}")
                : "游客存档 · 可穿戴");
        SetSceneText(root, "EquipmentPowerText", $"总战力  {totalStats.Power}");
        SetSceneText(root, "EquipmentCount", $"装备 {allVisibleItems.Count} 件");
        SetSceneText(root, "EquipmentStatus", equipmentStatusMessage);
        SetSceneText(root, "EquipmentHint", equipmentServerBacked
            ? "装备操作以服务器实例状态为准；锁定装备不会被自动选作突破材料"
            : "点击装备栏可卸下，选择物品后可穿戴");
        SetSceneText(root, "EquipmentAttackText", $"攻击  {totalStats.Attack}");
        SetSceneText(root, "EquipmentDefenseText", $"防御  {totalStats.Defense}");
        SetSceneText(root, "EquipmentHealthText", $"生命  {totalStats.Health}");
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

        string[] categoryIds = { "all", "weapon", "armor", "accessory", "pet" };
        for (int i = 0; i < categoryIds.Length; i++)
        {
            string categoryId = categoryIds[i];
            Button categoryButton = FindSceneComponent<Button>(root, "EquipmentCategory" + ToPascalCase(categoryId));
            ConfigureEquipmentCategoryButton(categoryButton, activeEquipmentCategory == categoryId);
            if (categoryButton != null)
            {
                categoryButton.interactable = !equipmentServerBacked || categoryId != "pet";
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
                    ? $"{GetEquipmentTypeLabel(item)}  Lv.{item.level}  {GetEquipmentQualityLabel(item.quality)}"
                    : $"{GetEquipmentTypeLabel(item)}  x{item.quantity}");
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

        EquipmentStats stats = GetEquipmentStats(item);
        SetSceneText(root, "EquipmentDetailIcon", GetNicknameInitial(item.name));
        SetSceneText(root, "EquipmentDetailName", item.name);
        SetSceneText(
            root,
            "EquipmentDetailType",
            equipmentServerBacked
                ? $"{GetEquipmentTypeLabel(item)} · Lv.{item.level} · {GetEquipmentQualityLabel(item.quality)}"
                : $"{GetEquipmentTypeLabel(item)}  ·  数量 x{item.quantity}");
        SetSceneText(root, "EquipmentDetailDescription", GetEquipmentDescription(item));
        SetSceneText(root, "EquipmentDetailAttack", $"攻击  +{stats.Attack}");
        SetSceneText(root, "EquipmentDetailDefense", $"防御  +{stats.Defense}");
        SetSceneText(root, "EquipmentDetailHealth", $"生命  +{stats.Health}");
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
                ? $"实例 ID {item.playerEquipmentId} · {(equipped ? "当前 NPC 已穿戴" : "当前位于仓库")} · {(item.locked ? "已锁定" : "未锁定")}"
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
                equipmentStatusMessage = "当前没有可穿戴装备的 NPC";
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
                equipmentStatusMessage = "当前 NPC 的装备状态已变化，请刷新";
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

        string equipmentType = (item.equipmentType ?? item.itemType ?? string.Empty).ToUpperInvariant();
        if (equipmentType == "WEAPON") return "weapon";
        if (equipmentType == "ARMOR") return "armor";
        if (equipmentType == "ACCESSORY" || equipmentType == "ORNAMENT") return "accessory";
        if (equipmentType == "PET") return "pet";

        if (IsPetItem(item))
        {
            return "pet";
        }

        if (!IsEquipmentItem(item))
        {
            return string.Empty;
        }

        string key = NormalizeInventoryKey(GetInventoryItemKey(item));
        string name = item.name ?? string.Empty;
        if (IsWeaponEquipment(key, name))
        {
            return "weapon";
        }

        if (IsArmorEquipment(key, name))
        {
            return "armor";
        }

        return "accessory";
    }

    private static bool IsWeaponEquipment(string key, string name)
    {
        return key.Contains("weapon") || key.Contains("sword") || key.Contains("blade") || key.Contains("bow")
            || key.Contains("staff") || key.Contains("axe") || key.Contains("spear")
            || name.Contains("剑") || name.Contains("刀") || name.Contains("弓") || name.Contains("杖") || name.Contains("武器");
    }

    private static bool IsArmorEquipment(string key, string name)
    {
        return key.Contains("armor") || key.Contains("helmet") || key.Contains("plate") || key.Contains("shield")
            || key.Contains("clothes") || key.Contains("boots")
            || name.Contains("甲") || name.Contains("盔") || name.Contains("盾") || name.Contains("鞋") || name.Contains("防具");
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
            return new EquipmentStats
            {
                Attack = serverEquipmentLoadout.totalAttack,
                Defense = serverEquipmentLoadout.totalDefense,
                Health = serverEquipmentLoadout.totalHealth,
                Power = serverEquipmentLoadout.totalAttack + serverEquipmentLoadout.totalDefense + serverEquipmentLoadout.totalHealth / 10
            };
        }

        EquipmentStats result = new EquipmentStats
        {
            Attack = 10,
            Defense = 8,
            Health = 100
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
        }

        result.Power = result.Attack + result.Defense + result.Health / 10;
        return result;
    }

    private EquipmentStats GetEquipmentStats(InventoryItemResponse item)
    {
        if (equipmentServerBacked && item != null)
        {
            return new EquipmentStats
            {
                Attack = item.attackBonus,
                Defense = item.defenseBonus,
                Health = item.healthBonus,
                Power = item.attackBonus + item.defenseBonus + item.healthBonus / 10
            };
        }

        string slotId = GetEquipmentSlot(item);
        string key = NormalizeInventoryKey(GetInventoryItemKey(item));
        int variant = StableInventoryHash(key) % 6;
        EquipmentStats result = new EquipmentStats();
        if (slotId == "weapon")
        {
            result.Attack = 12 + variant;
        }
        else if (slotId == "armor")
        {
            result.Defense = 12 + variant;
            result.Health = 35 + variant * 3;
        }
        else if (slotId == "pet")
        {
            result.Attack = 4 + variant;
            result.Health = 18 + variant * 2;
        }
        else
        {
            result.Attack = 3 + variant / 2;
            result.Defense = 3 + variant / 2;
            result.Health = 8 + variant * 2;
        }

        result.Power = result.Attack + result.Defense + result.Health / 10;
        return result;
    }

    private string GetEquipmentDescription(InventoryItemResponse item)
    {
        if (equipmentServerBacked && item != null && !string.IsNullOrEmpty(item.description))
        {
            return item.description;
        }

        string slot = GetEquipmentSlot(item);
        if (slot == "weapon") return "适合领主出征的攻击装备，提升基础攻击。";
        if (slot == "armor") return "守护领地的防御装备，提升防御与生命。";
        if (slot == "pet") return "陪伴领主作战的灵宠，提供额外属性。";
        return "拥有特殊加成的饰品，可补足战斗属性。";
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
        return string.IsNullOrEmpty(quality) ? "装备" : quality;
    }

    private static string GetEquipmentSlotLabel(string slotId)
    {
        if (slotId == "weapon") return "武器";
        if (slotId == "armor") return "防具";
        if (slotId == "pet") return "灵宠";
        if (slotId == "accessory") return "饰品";
        return "装备";
    }

    private static string GetSlotIcon(string slotId)
    {
        if (slotId == "weapon") return "剑";
        if (slotId == "armor") return "甲";
        if (slotId == "pet") return "宠";
        return "饰";
    }

    private Color GetEquipmentColor(InventoryItemResponse item)
    {
        string slot = GetEquipmentSlot(item);

        if (slot == "weapon") return new Color(0.83f, 0.38f, 0.24f, 1f);
        if (slot == "armor") return new Color(0.28f, 0.55f, 0.76f, 1f);
        if (slot == "pet") return new Color(0.68f, 0.38f, 0.72f, 1f);
        return new Color(0.38f, 0.65f, 0.40f, 1f);
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
            upgrade = CreateButton(panel, "EquipmentNpcUpgradeButton", "NPC升级", new Color(0.86f, 0.48f, 0.20f, 1f), Color.white);
            SetRect(upgrade.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(664f, -40f), new Vector2(104f, 42f));
            ConfigureClearButtonLabel(upgrade, 16);
        }

        previous.gameObject.SetActive(true);
        next.gameObject.SetActive(true);
        upgrade.gameObject.SetActive(true);
        bool canSwitch = serverPlayerNpcs.Length > 1 && !equipmentRefreshRunning && !equipmentMutationRunning;
        previous.interactable = canSwitch;
        next.interactable = canSwitch;
        upgrade.interactable = GetCurrentEquipmentNpc() != null && !equipmentRefreshRunning && !equipmentMutationRunning;
        BindSceneButton(previous, () => ChangeEquipmentNpc(-1));
        BindSceneButton(next, () => ChangeEquipmentNpc(1));
        BindSceneButton(upgrade, UpgradeCurrentServerNpc);
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
        equipmentStatusMessage = "正在读取所选 NPC 的装备...";
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
        equipmentStatusMessage = npc == null ? "当前没有 NPC" : $"已切换到 {npc.name}";
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
        upgrade.interactable = item != null && !equipmentMutationRunning && !equipmentRefreshRunning;
        lockButton.interactable = item != null && !equipmentMutationRunning && !equipmentRefreshRunning;
        SetSceneButtonText(lockButton, item != null && item.locked ? "解锁" : "锁定");
        BindSceneButton(upgrade, () => UpgradeServerEquipment(item));
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
        if (npc != null && !equipmentMutationRunning && !equipmentRefreshRunning)
        {
            StartCoroutine(UpgradeServerNpcRequest(npc.playerNpcId));
        }
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

        PlayerPrefs.SetInt("Gold", Mathf.Max(0, result.remainingGold));
        PlayerPrefs.Save();
        RefreshHomeResourceMetrics();
        equipmentMutationRunning = false;
        equipmentRefreshRunning = true;
        yield return GetEquipmentInventoryList();
        equipmentStatusMessage = $"{result.name} 已升级到 Lv.{result.level}，消耗金币 {result.goldCost}";
        RenderEquipmentScenePanel(GetEquipmentPanelRoot());
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
        equipmentStatusMessage = "正在升级 NPC...";
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
                equipmentStatusMessage = "NPC 升级失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response));
                RenderEquipmentScenePanel(GetEquipmentPanelRoot());
                yield break;
            }
            result = response.data;
        }

        PlayerPrefs.SetInt("Gold", Mathf.Max(0, result.remainingGold));
        PlayerPrefs.Save();
        RefreshHomeResourceMetrics();
        equipmentMutationRunning = false;
        equipmentRefreshRunning = true;
        yield return GetEquipmentInventoryList();
        equipmentStatusMessage = $"{result.name} 已升级到 Lv.{result.level}，消耗金币 {result.goldCost}";
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
            text += $"{i + 1}. {equipment[i].name}  x{equipment[i].quantity}\n";
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
        public int equipmentHealthBonus;
        public int equipmentAttackBonus;
        public int equipmentDefenseBonus;
        public int totalHealth;
        public int totalAttack;
        public int totalDefense;
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
        public int healthBonus;
        public int attackBonus;
        public int defenseBonus;
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
        public int npcId;
        public string npcCode;
        public string name;
        public int fromLevel;
        public int level;
        public int goldCost;
        public int fragmentCost;
        public string fragmentItemKey;
        public int remainingGold;
        public int remainingFragments;
        public int health;
        public int attack;
        public int defense;
        public float produceBonusRate;
    }
#pragma warning restore 0649

    private struct EquipmentStats
    {
        public int Attack;
        public int Defense;
        public int Health;
        public int Power;
    }
}
