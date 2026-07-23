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
    private bool equipmentServerBacked;
    private bool equipmentRefreshRunning;
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
            "正在读取服务器背包...");
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
        int playerId = PlayerPrefs.GetInt(PlayerIdKey, 0);
        UnityWebRequest request = CreateAuthorizedGet($"/inventory/list/{playerId}");
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

            if (equipmentPanelObject == null)
            {
                equipmentRefreshRunning = false;
                yield break;
            }

            if (HandleUnauthorizedResponse(request))
            {
                equipmentRefreshRunning = false;
                yield break;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                equipmentRefreshRunning = false;
                equipmentStatusMessage = $"请求失败：{ApiRequestUtility.GetErrorMessage(request, "服务器连接失败")}";
                RenderEquipmentScenePanel(GetEquipmentPanelRoot());
                yield break;
            }

            InventoryListApiResponse response;
            try
            {
                response = JsonUtility.FromJson<InventoryListApiResponse>(request.downloadHandler.text);
            }
            catch (Exception)
            {
                equipmentRefreshRunning = false;
                equipmentStatusMessage = "服务器背包格式无法解析";
                RenderEquipmentScenePanel(GetEquipmentPanelRoot());
                yield break;
            }

            if (response == null || response.code != 200)
            {
                equipmentRefreshRunning = false;
                equipmentStatusMessage = GetApiError(response);
                RenderEquipmentScenePanel(GetEquipmentPanelRoot());
                yield break;
            }

            SyncInventoryResourceValues(response.data);
            currentEquipmentItems = response.data ?? new InventoryItemResponse[0];
            equipmentRefreshRunning = false;
            equipmentStatusMessage = "服务器装备数据已刷新";
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
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

        List<InventoryItemResponse> visibleItems = GetFilteredEquipmentItems();
        InventoryItemResponse selected = FindEquipmentItem(selectedEquipmentItemKey);
        if (selected == null || !visibleItems.Contains(selected))
        {
            selected = visibleItems.Count > 0 ? visibleItems[0] : null;
            selectedEquipmentItemKey = selected == null ? string.Empty : GetInventoryItemKey(selected);
        }

        EquipmentStats totalStats = CalculateEquippedStats();
        SetSceneText(root, "EquipmentTitle", "装备大厅");
        SetSceneText(root, "EquipmentModeText", equipmentServerBacked ? "服务器背包 · 本地预览" : "游客存档 · 可穿戴");
        SetSceneText(root, "EquipmentPowerText", $"总战力  {totalStats.Power}");
        SetSceneText(root, "EquipmentCount", $"装备 {visibleItems.Count} 件");
        SetSceneText(root, "EquipmentStatus", equipmentStatusMessage);
        SetSceneText(root, "EquipmentHint", equipmentServerBacked
            ? "穿戴状态只保存在本地预览，服务端装备接口开放后可替换为真实同步"
            : "点击装备栏可卸下，选择物品后可穿戴");
        SetSceneText(root, "EquipmentAttackText", $"攻击  {totalStats.Attack}");
        SetSceneText(root, "EquipmentDefenseText", $"防御  {totalStats.Defense}");
        SetSceneText(root, "EquipmentHealthText", $"生命  {totalStats.Health}");
        SetSceneText(root, "EquipmentPowerDetailText", $"战力  {totalStats.Power}");

        BindSceneButton(FindSceneComponent<Button>(root, "EquipmentCloseButton"), HideEquipmentPanel);
        Button refresh = FindSceneComponent<Button>(root, "EquipmentRefreshButton");
        if (refresh != null)
        {
            refresh.interactable = HasAuthSession() && !equipmentRefreshRunning;
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
            BindSceneButton(categoryButton, () =>
            {
                activeEquipmentCategory = categoryId;
                selectedEquipmentItemKey = string.Empty;
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
            SetSceneText(row, "EquipmentRowMeta" + i, $"{GetEquipmentTypeLabel(item)}  x{item.quantity}");
            SetSceneText(row, "EquipmentRowEquipped" + i, equipped ? "已装备" : "可穿戴");
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
        SetSceneText(root, "EquipmentDetailType", $"{GetEquipmentTypeLabel(item)}  ·  数量 x{item.quantity}");
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
        SetSceneButtonText(action, equipped ? "卸下装备" : (equipmentServerBacked ? "预览穿戴" : "穿戴装备"));
        BindSceneButton(action, () => ToggleEquipment(root, item));
        SetSceneText(root, "EquipmentDetailStatus", equipped
            ? (equipmentServerBacked ? "当前为本地预览状态" : "当前已穿戴")
            : "选择穿戴后会替换同一栏位的装备");
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
            if (item == null || item.quantity <= 0 || (!IsEquipmentItem(item) && !IsPetItem(item)))
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
        string slotId = GetEquipmentSlot(item);
        return !string.IsNullOrEmpty(slotId) && GetEquippedItemKey(slotId) == GetInventoryItemKey(item);
    }

    private EquipmentStats CalculateEquippedStats()
    {
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

    private struct EquipmentStats
    {
        public int Attack;
        public int Defense;
        public int Health;
        public int Power;
    }
}
