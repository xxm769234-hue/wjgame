using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using Text = TMPro.TMP_Text;

public partial class HomePageUI
{
    private const int EquipmentColumns = 3;

    [Header("Equipment quality backgrounds")]
    [SerializeField] private Sprite equipmentCommonBackground;
    [SerializeField] private Sprite equipmentFineBackground;
    [SerializeField] private Sprite equipmentExcellentBackground;
    [SerializeField] private Sprite equipmentEpicBackground;
    [SerializeField] private Sprite equipmentLegendaryBackground;
    [SerializeField] private EquipmentIconBinding[] equipmentIconBindings = new EquipmentIconBinding[0];

    [Header("Equipment list layout")]
    [Tooltip("Spacing between generated equipment cells. Cell size and child styles come from EquipmentItemRow0.")]
    [SerializeField] private Vector2 equipmentGridSpacing = new Vector2(20.232f, 37.209f);

    private RectTransform _equipmentItemParent;
    private RectTransform _equipmentRowTemplate;
    private readonly List<RectTransform> _equipmentRows = new List<RectTransform>();
    private float _equipmentGridMinimumHeight;
    private int _equipmentReadVersion;
    private bool _equipmentDataReadSucceeded;
    private readonly Dictionary<string, Sprite> _equipmentIconCache = new Dictionary<string, Sprite>();
    private readonly Dictionary<long, EquipmentLoadoutData> _equipmentLoadouts = new Dictionary<long, EquipmentLoadoutData>();
    private readonly Dictionary<long, PlayerNpcResponse> _equipmentWearers = new Dictionary<long, PlayerNpcResponse>();
    private readonly HashSet<string> _equipmentUnknownQualities = new HashSet<string>();
    private readonly HashSet<string> _equipmentMissingIconKeys = new HashSet<string>();

    [Serializable]
    private sealed class EquipmentIconBinding
    {
        public string iconKey;
        public Sprite sprite;
    }

    private static readonly string[] EquipmentSlotIds = { "weapon", "helmet", "armor", "boots", "gloves" };
    private static readonly string[] EquipmentSlotLabels = { "武器", "头盔", "护甲", "靴子", "手套" };
    private Transform _equipmentCategoryRoot;
    private readonly List<Button> _equipmentCategoryButtons = new List<Button>();
    private int _equipmentSinglePreviewVersion;
    private int _equipmentAutoPreviewVersion;

    private GameObject equipmentPanelObject;
    private InventoryItemResponse[] currentEquipmentItems = new InventoryItemResponse[0];
    private InventoryItemResponse[] serverEquipmentWarehouse = new InventoryItemResponse[0];
    private PlayerNpcResponse[] serverPlayerNpcs = new PlayerNpcResponse[0];
    private EquipmentLoadoutData serverEquipmentLoadout;
    private bool equipmentServerBacked;
    private bool equipmentRefreshRunning;
    private bool equipmentMutationRunning;
    private int selectedEquipmentNpcIndex;
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
    private string equipmentUpgradeConfirmation = string.Empty;
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

        if (equipmentMutationRunning)
        {
            ShowEquipmentScenePanel(currentEquipmentItems, true, "正在处理装备操作，请稍候...");
            return;
        }

        activeEquipmentCategory = "all";
        selectedEquipmentItemKey = string.Empty;
        equipmentStatusMessage = string.Empty;
        serverPlayerNpcs = new PlayerNpcResponse[0];
        serverEquipmentWarehouse = new InventoryItemResponse[0];
        serverEquipmentLoadout = null;
        _equipmentLoadouts.Clear();
        _equipmentWearers.Clear();
        ResetEquipmentUpgradePreview();
        ResetEquipmentAutoUpgradePreviews();

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
        ResetEquipmentScroll(panel.PanelRoot);
        SetStatus(equipmentStatusMessage);
    }

    private IEnumerator GetEquipmentInventoryList(bool allowHidden = false)
    {
        int readVersion = ++_equipmentReadVersion;
        string sessionToken = GameSession.Token;
        _equipmentDataReadSucceeded = false;
        serverEquipmentLoadout = null;
        ResetEquipmentUpgradePreview();
        ResetEquipmentAutoUpgradePreviews();
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
            if (readVersion != _equipmentReadVersion || playerId != GameSession.PlayerId || sessionToken != GameSession.Token) yield break;

            if (equipmentPanelObject == null && !allowHidden)
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

        yield return RefreshServerEquipmentWarehouse(allowHidden);
        if (readVersion != _equipmentReadVersion || playerId != GameSession.PlayerId || sessionToken != GameSession.Token) yield break;
        if (!equipmentRefreshRunning || (equipmentPanelObject == null && !allowHidden))
        {
            yield break;
        }

        _equipmentLoadouts.Clear();
        for (int npcIndex = 0; npcIndex < serverPlayerNpcs.Length; npcIndex++)
        {
            PlayerNpcResponse npc = serverPlayerNpcs[npcIndex];
            if (npc == null) continue;
            yield return RefreshEquipmentNpcLoadout(npc, allowHidden);
            if (readVersion != _equipmentReadVersion || playerId != GameSession.PlayerId || sessionToken != GameSession.Token) yield break;
            if (!equipmentRefreshRunning || (equipmentPanelObject == null && !allowHidden))
            {
                RebuildServerEquipmentItems();
                RenderEquipmentScenePanel(GetEquipmentPanelRoot());
                yield break;
            }
        }
        PlayerNpcResponse selectedNpc = GetCurrentEquipmentNpc();
        serverEquipmentLoadout = selectedNpc != null && _equipmentLoadouts.ContainsKey(selectedNpc.playerNpcId)
            ? _equipmentLoadouts[selectedNpc.playerNpcId] : null;

        RebuildServerEquipmentItems();
        _equipmentDataReadSucceeded = true;
        equipmentRefreshRunning = false;
        equipmentStatusMessage = serverPlayerNpcs.Length == 0
            ? "装备仓库已刷新；当前账号还没有可穿戴装备的 NPC"
            : "服务器 NPC、装备仓库与穿戴数据已刷新";
        RenderEquipmentScenePanel(GetEquipmentPanelRoot());
    }

    private IEnumerator RefreshServerEquipmentWarehouse(bool allowHidden = false)
    {
        int readVersion = _equipmentReadVersion;
        int playerId = GameSession.PlayerId;
        string sessionToken = GameSession.Token;
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
            if (readVersion != _equipmentReadVersion || playerId != GameSession.PlayerId || sessionToken != GameSession.Token
                || (equipmentPanelObject == null && !allowHidden)) yield break;
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
        if (npc == null) yield break;
        int readVersion = _equipmentReadVersion;
        yield return RefreshEquipmentNpcLoadout(npc);
        if (readVersion != _equipmentReadVersion) yield break;
        EquipmentLoadoutData loadout;
        serverEquipmentLoadout = _equipmentLoadouts.TryGetValue(npc.playerNpcId, out loadout) ? loadout : null;
    }

    private IEnumerator RefreshEquipmentNpcLoadout(PlayerNpcResponse npc, bool allowHidden = false)
    {
        int readVersion = _equipmentReadVersion;
        int playerId = GameSession.PlayerId;
        string sessionToken = GameSession.Token;
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
            if (readVersion != _equipmentReadVersion || playerId != GameSession.PlayerId || sessionToken != GameSession.Token
                || (equipmentPanelObject == null && !allowHidden)) yield break;
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

            _equipmentLoadouts[npc.playerNpcId] = response.data;
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

        _equipmentWearers.Clear();
        for (int npcIndex = 0; npcIndex < serverPlayerNpcs.Length; npcIndex++)
        {
            PlayerNpcResponse npc = serverPlayerNpcs[npcIndex];
            EquipmentLoadoutData loadout;
            if (npc == null || !_equipmentLoadouts.TryGetValue(npc.playerNpcId, out loadout)
                || loadout == null || loadout.equipment == null) continue;
            for (int itemIndex = 0; itemIndex < loadout.equipment.Length; itemIndex++)
            {
                EquipmentLoadoutItem equipped = loadout.equipment[itemIndex];
                if (equipped == null || equipped.playerEquipmentId <= 0) continue;
                _equipmentWearers[equipped.playerEquipmentId] = npc;
                InventoryItemResponse item = items.Find(candidate => candidate.playerEquipmentId == equipped.playerEquipmentId);
                if (item == null)
                {
                    item = new InventoryItemResponse { playerEquipmentId = equipped.playerEquipmentId };
                    items.Add(item);
                }
                item.equipmentId = equipped.equipmentId;
                item.equipmentKey = equipped.equipmentKey;
                item.name = equipped.name;
                item.equipmentType = equipped.equipmentType;
                item.quality = equipped.quality;
                item.icon = equipped.icon;
                item.modelKey = equipped.modelKey;
                item.level = equipped.level;
                item.locked = equipped.locked;
                item.healthBonus = equipped.healthBonus;
                item.attackBonus = equipped.attackBonus;
                item.defenseBonus = equipped.defenseBonus;
                item.speedBonus = equipped.speedBonus;
                item.status = "EQUIPPED";
                NormalizeServerEquipmentItem(item, "EQUIPPED");
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

    private void RenderEquipmentNpcSummary(Transform root, PlayerNpcResponse npc)
    {
        Image avatar = FindSceneComponent<Image>(root, "EquipmentNpcAvatar");
        Text avatarFallback = FindSceneComponent<Text>(root, "EquipmentNpcAvatarText");
        Text name = FindSceneComponent<Text>(root, "EquipmentNpcName");
        Text level = FindSceneComponent<Text>(root, "EquipmentNpcLevel");
        Text attribute = FindSceneComponent<Text>(root, "EquipmentNpcAttribute");
        Text description = FindSceneComponent<Text>(root, "EquipmentNpcDescription");
        if (npc == null)
        {
            if (avatar != null) avatar.enabled = false;
            if (avatarFallback != null)
            {
                avatarFallback.gameObject.SetActive(true);
                avatarFallback.text = "NPC";
            }
            SetSceneText(root, "EquipmentNpcName", "未选择角色");
            SetSceneText(root, "EquipmentNpcLevel", "等级 -");
            SetSceneText(root, "EquipmentNpcAttribute", "战斗属性 -");
            SetSceneText(root, "EquipmentNpcDescription", "请选择角色查看穿戴装备");
            SetSceneText(root, "EquipmentNpcStats", "基础属性  -\n装备加成  -\n最终属性  -");
            return;
        }

        ApplyNpcIcon(avatar, avatarFallback, npc);
        SetSceneText(root, "EquipmentNpcName", npc.name);
        SetSceneText(root, "EquipmentNpcLevel", $"等级 Lv.{npc.level}/{Mathf.Max(npc.maxLevel, npc.level)}");
        SetSceneText(root, "EquipmentNpcAttribute", string.IsNullOrEmpty(npc.combatAttribute) ? "战斗属性 -" : $"战斗属性 {npc.combatAttribute}");
        SetSceneText(root, "EquipmentNpcDescription", string.IsNullOrEmpty(npc.description) ? "" : npc.description);
        SetSceneText(root, "EquipmentNpcStats", BuildEquipmentLoadoutStatsText());
    }

    private string BuildEquipmentLoadoutStatsText()
    {
        if (!equipmentServerBacked || serverEquipmentLoadout == null)
        {
            return "基础属性  等待服务器\n装备加成  等待服务器\n最终属性  等待服务器";
        }

        string baseText = $"生命 {serverEquipmentLoadout.baseHealth}  攻击 {serverEquipmentLoadout.baseAttack}  防御 {serverEquipmentLoadout.baseDefense}";
        string bonusText = $"生命 +{serverEquipmentLoadout.equipmentHealthBonus}  攻击 +{serverEquipmentLoadout.equipmentAttackBonus}  防御 +{serverEquipmentLoadout.equipmentDefenseBonus}";
        string totalText = $"生命 {serverEquipmentLoadout.totalHealth}  攻击 {serverEquipmentLoadout.totalAttack}  防御 {serverEquipmentLoadout.totalDefense}";
        if (serverEquipmentLoadout.baseSpeed != 0
            || serverEquipmentLoadout.equipmentSpeedBonus != 0
            || serverEquipmentLoadout.totalSpeed != 0)
        {
            baseText += $"  速度 {serverEquipmentLoadout.baseSpeed}";
            bonusText += $"  速度 +{serverEquipmentLoadout.equipmentSpeedBonus}";
            totalText += $"  速度 {serverEquipmentLoadout.totalSpeed}";
        }

        return $"基础属性  {baseText}\n装备加成  {bonusText}\n最终属性  {totalText}";
    }

    private void ApplyEquipmentIcon(Image image, Text fallback, InventoryItemResponse item, string fallbackName = null)
    {
        if (image == null) return;
        string displayName = item == null ? fallbackName : item.name;
        Sprite sprite = ResolveEquipmentSprite(item);
        image.sprite = sprite;
        image.enabled = sprite != null;
        if (fallback != null)
        {
            fallback.gameObject.SetActive(sprite == null);
            fallback.text = string.IsNullOrEmpty(displayName) ? string.Empty
                : System.Globalization.StringInfo.GetNextTextElement(displayName);
        }

        if (item != null && sprite == null)
        {
            string missingKey = string.Join("|", item.equipmentKey, item.icon, item.quality, item.equipmentType);
            if (_equipmentMissingIconKeys.Add(missingKey))
            {
                Debug.LogWarning("Equipment sprite was not found: " + missingKey, image);
            }
        }
    }

    private void ApplyNpcIcon(Image image, Text fallback, PlayerNpcResponse npc)
    {
        if (image == null) return;

        Sprite sprite = ResolveNpcSprite(npc);
        image.sprite = sprite;
        image.enabled = sprite != null;
        if (fallback != null)
        {
            fallback.gameObject.SetActive(sprite == null);
            fallback.text = npc == null || string.IsNullOrEmpty(npc.name)
                ? "NPC"
                : System.Globalization.StringInfo.GetNextTextElement(npc.name);
        }
        if (sprite == null && npc != null && !string.IsNullOrEmpty(npc.icon))
        {
            Debug.LogWarning("NPC portrait sprite was not found for icon: " + npc.icon, image);
        }
    }

    private Sprite ResolveNpcSprite(PlayerNpcResponse npc)
    {
        if (npc == null) return null;

        string npcId = npc.npcId > 0 ? npc.npcId.ToString() : null;
        string[] keys = { npc.icon, npc.npcCode, npc.modelKey, npcId, npc.npcId > 0 ? "Npc_" + npcId : null };
        Sprite boundSprite = FindBoundRoleSprite(keys, true);
        if (boundSprite != null) return boundSprite;

        for (int index = 0; index < keys.Length; index++)
        {
            string key = keys[index];
            if (string.IsNullOrWhiteSpace(key)) continue;

            key = key.Trim().Replace('\\', '/');
            int separator = key.LastIndexOf('/');
            if (separator >= 0) key = key.Substring(separator + 1);
            int extension = key.LastIndexOf('.');
            if (extension > 0) key = key.Substring(0, extension);

            string[] candidates =
            {
                "_Project/Art/UI/角色/Q版/" + key + ".png",
                "_Project/Art/UI/角色/角色立绘/" + key + ".png",
                "_Project/Art/UI/角色/" + key + ".png"
            };
            for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
            {
                Sprite sprite = LoadSpriteFromAsset(candidates[candidateIndex]);
                if (sprite != null) return sprite;
            }
        }

        return null;
    }

    private Sprite ResolveEquipmentSprite(InventoryItemResponse item)
    {
        if (item == null) return null;

        string[] candidates = { item.icon, item.equipmentKey, item.name };
        for (int index = 0; index < candidates.Length; index++)
        {
            Sprite sprite = ResolveEquipmentSprite(candidates[index]);
            if (sprite != null) return sprite;
        }

        string slotId = GetEquipmentSlot(item);
        Sprite boundSprite = ResolveEquipmentSprite(GetEquipmentIconBindingKey(item.quality, slotId));
        return boundSprite != null ? boundSprite : LoadEquipmentSpriteFromProject(item.quality, slotId);
    }

    private Sprite LoadEquipmentSpriteFromProject(string quality, string slotId)
    {
        string qualityDirectory;
        switch ((quality ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "FINE": qualityDirectory = "优秀级"; break;
            case "EXCELLENT": qualityDirectory = "稀有级"; break;
            case "EPIC": qualityDirectory = "史诗级"; break;
            case "LEGENDARY": qualityDirectory = "传说级/橙金"; break;
            default: qualityDirectory = "普通级"; break;
        }

        string fileName;
        switch (NormalizeEquipmentType(slotId))
        {
            case "weapon": fileName = qualityDirectory == "优秀级" || qualityDirectory == "传说级/橙金" ? "武器1" : "武器"; break;
            case "helmet": fileName = "头盔"; break;
            case "armor": fileName = qualityDirectory == "普通级" || qualityDirectory == "传说级/橙金" ? "胸甲" : "护甲"; break;
            case "boots": fileName = "鞋子"; break;
            case "gloves": fileName = "手套"; break;
            default: return null;
        }

        return LoadSpriteFromAsset("_Project/Art/UI/装备图标/" + qualityDirectory + "/" + fileName + ".png");
    }

    private static string GetEquipmentIconBindingKey(string quality, string slotId)
    {
        string normalizedQuality = (quality ?? string.Empty).Trim().ToUpperInvariant();
        switch (normalizedQuality)
        {
            case "FINE":
            case "EXCELLENT":
            case "EPIC":
            case "LEGENDARY":
                break;
            default:
                normalizedQuality = "COMMON";
                break;
        }

        string normalizedSlot = NormalizeEquipmentType(slotId);
        return string.IsNullOrEmpty(normalizedSlot)
            ? string.Empty
            : normalizedQuality + ":" + normalizedSlot;
    }

    private Sprite ResolveEquipmentSprite(string iconKey)
    {
        if (string.IsNullOrWhiteSpace(iconKey)) return null;
        Sprite cached;
        if (_equipmentIconCache.TryGetValue(iconKey, out cached) && cached != null) return cached;
        string resourceName = iconKey.Trim().Replace('\\', '/');
        int separator = resourceName.LastIndexOf('/');
        if (separator >= 0) resourceName = resourceName.Substring(separator + 1);
        int extension = resourceName.LastIndexOf('.');
        if (extension > 0) resourceName = resourceName.Substring(0, extension);
        Sprite sprite = null;
        for (int index = 0; equipmentIconBindings != null && index < equipmentIconBindings.Length; index++)
        {
            EquipmentIconBinding binding = equipmentIconBindings[index];
            if (binding != null && binding.sprite != null && (string.Equals(binding.iconKey, iconKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(binding.iconKey, resourceName, StringComparison.OrdinalIgnoreCase)))
            {
                sprite = binding.sprite;
                break;
            }
        }
        if (sprite == null) sprite = LoadMailAttachmentResourceSprite(iconKey);
#if UNITY_EDITOR
        if (sprite == null)
        {
            string path = iconKey.Replace('\\', '/');
            if (path.StartsWith("Assets/", StringComparison.Ordinal)) path = path.Substring(7);
            if (!System.IO.Path.HasExtension(path)) path += ".png";
            string[] candidates = { path, "Sprites/" + path, "Sprites/Arts/" + path, "Sprites/Arts/png/" + path };
            for (int index = 0; index < candidates.Length && sprite == null; index++)
            {
                sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/" + candidates[index]);
                if (sprite == null)
                    sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                        "Assets/_Imported/weijun-game-project/Assets/" + candidates[index]);
            }
        }
#endif
        if (sprite != null) _equipmentIconCache[iconKey] = sprite;
        return sprite;
    }

    private void RenderEquipmentScenePanel(Transform root)
    {
        if (root == null)
        {
            return;
        }

        List<string> categoryIds = GetBackendEquipmentCategoryIds(currentEquipmentItems);
        if (!categoryIds.Contains(activeEquipmentCategory)) activeEquipmentCategory = "all";
        List<InventoryItemResponse> allVisibleItems = GetFilteredEquipmentItems();
        InventoryItemResponse selected = FindEquipmentItem(selectedEquipmentItemKey);
        if (selected == null || !allVisibleItems.Contains(selected))
        {
            selected = allVisibleItems.Count > 0 ? allVisibleItems[0] : null;
            selectedEquipmentItemKey = selected == null ? string.Empty : GetInventoryItemKey(selected);
        }

        EquipmentStats totalStats = CalculateEquippedStats();
        PlayerNpcResponse selectedNpc = GetCurrentEquipmentNpc();
        RenderEquipmentNpcSummary(root, selectedNpc);
        SetSceneText(root, "EquipmentTitle", "装备大厅");
        SetSceneText(
            root,
            "EquipmentModeText",
            equipmentServerBacked
                ? (selectedNpc == null ? "服务器装备 · 暂无 NPC" : $"{selectedNpc.name}  Lv.{selectedNpc.level}  {selectedEquipmentNpcIndex + 1}/{serverPlayerNpcs.Length}")
                : "本地奖励库存 · 可穿戴");
        SetSceneText(root, "EquipmentCount", $"装备 {allVisibleItems.Count} 件");
        SetSceneText(root, "EquipmentStatus", equipmentStatusMessage);
        SetSceneText(root, "EquipmentAttackText", $"攻击  {totalStats.Attack}");
        SetSceneText(root, "EquipmentDefenseText", $"防御  {totalStats.Defense}");
        SetSceneText(root, "EquipmentHealthText", $"生命  {totalStats.Health}");
        SetSceneText(root, "EquipmentSpeedText", $"速度  {totalStats.Speed}");
        ConfigureServerNpcControls(root);

        BindSceneButton(FindSceneComponent<Button>(root, "EquipmentCloseButton"), HideEquipmentPanel);
        Button refresh = FindSceneComponent<Button>(root, "EquipmentRefreshButton");
        if (refresh != null)
        {
            refresh.interactable = HasAuthSession() && !equipmentRefreshRunning && !equipmentMutationRunning;
        }
        SetSceneButtonText(refresh, equipmentRefreshRunning ? "同步中..." : HasPendingEquipmentGrowthRequest() ? "核对上次结果" : (HasAuthSession() ? "刷新背包" : "本地已同步"));
        BindSceneButton(refresh, () =>
        {
            if (HasAuthSession() && !equipmentRefreshRunning && !equipmentMutationRunning)
            {
                if (HasPendingEquipmentGrowthRequest())
                {
                    ResumePendingEquipmentGrowthRequest();
                    return;
                }
                equipmentRefreshRunning = true;
                equipmentStatusMessage = "正在同步服务器背包...";
                RenderEquipmentScenePanel(root);
                StartCoroutine(GetEquipmentInventoryList());
            }
        });

        RenderEquipmentCategories(root, categoryIds);

        RenderEquipmentSlots(root);
        RenderEquipmentRows(root, allVisibleItems);
        RenderEquipmentDetail(root, selected);
    }

    private static string NormalizeEquipmentType(string equipmentType)
    {
        string value = (equipmentType ?? string.Empty).Trim().ToLowerInvariant();
        switch (value)
        {
            case "weapon":
            case "weapons":
            case "sword":
            case "bow":
            case "武器":
                return "weapon";
            case "helmet":
            case "head":
            case "headgear":
            case "hat":
            case "头盔":
            case "头部":
                return "helmet";
            case "armor":
            case "armour":
            case "chest":
            case "body":
            case "护甲":
            case "胸甲":
                return "armor";
            case "boots":
            case "boot":
            case "shoes":
            case "footwear":
            case "鞋子":
            case "靴子":
                return "boots";
            case "gloves":
            case "glove":
            case "hands":
            case "hand":
            case "手套":
                return "gloves";
            default:
                return value;
        }
    }

    private static List<string> GetBackendEquipmentCategoryIds(InventoryItemResponse[] items)
    {
        List<string> categories = new List<string>();
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal) { "all" };
        if (items != null)
        {
            for (int index = 0; index < items.Length; index++)
            {
                InventoryItemResponse item = items[index];
                if (item == null || item.playerEquipmentId <= 0) continue;
                string equipmentType = NormalizeEquipmentType(item.equipmentType);
                if (!string.IsNullOrEmpty(equipmentType) && seen.Add(equipmentType)) categories.Add(equipmentType);
            }
        }
        categories.Sort(StringComparer.Ordinal);
        categories.Insert(0, "all");
        return categories;
    }

    private void RenderEquipmentCategories(Transform root, List<string> categoryIds)
    {
        Transform categoryRoot = FindSceneTransform(root, "EquipmentCategory");
        if (categoryRoot == null) return;
        if (_equipmentCategoryRoot != categoryRoot)
        {
            _equipmentCategoryRoot = categoryRoot;
            _equipmentCategoryButtons.Clear();
            for (int index = 0; index < categoryRoot.childCount; index++)
            {
                Button button = categoryRoot.GetChild(index).GetComponent<Button>();
                if (button != null && button.name.StartsWith("EquipmentCategory", StringComparison.Ordinal))
                    _equipmentCategoryButtons.Add(button);
            }
            _equipmentCategoryButtons.Sort((left, right) =>
                ((RectTransform)right.transform).anchoredPosition.y.CompareTo(((RectTransform)left.transform).anchoredPosition.y));
        }
        if (_equipmentCategoryButtons.Count == 0) return;
        while (_equipmentCategoryButtons.Count < categoryIds.Count)
        {
            Button template = _equipmentCategoryButtons.Count > 1 ? _equipmentCategoryButtons[1] : _equipmentCategoryButtons[0];
            RectTransform last = _equipmentCategoryButtons[_equipmentCategoryButtons.Count - 1].transform as RectTransform;
            float spacing = _equipmentCategoryButtons.Count > 1
                ? ((RectTransform)_equipmentCategoryButtons[0].transform).anchoredPosition.y
                    - ((RectTransform)_equipmentCategoryButtons[1].transform).anchoredPosition.y
                : last.rect.height + 12f;
            Button clone = Instantiate(template, categoryRoot, false);
            clone.name = "EquipmentCategory_" + categoryIds[_equipmentCategoryButtons.Count];
            ((RectTransform)clone.transform).anchoredPosition = last.anchoredPosition
                - new Vector2(0f, Mathf.Max(last.rect.height, spacing));
            _equipmentCategoryButtons.Add(clone);
        }
        for (int index = 0; index < _equipmentCategoryButtons.Count; index++)
        {
            Button button = _equipmentCategoryButtons[index];
            if (button == null) continue;
            bool visible = index < categoryIds.Count;
            button.gameObject.SetActive(visible);
            if (!visible) continue;
            string categoryId = categoryIds[index];
            SetSceneButtonText(button, categoryId == "all" ? "全部" : GetEquipmentSlotLabel(categoryId));
            ConfigureEquipmentCategoryButton(button, activeEquipmentCategory == categoryId);
            button.interactable = !equipmentRefreshRunning && !equipmentMutationRunning;
            BindSceneButton(button, () =>
            {
                activeEquipmentCategory = categoryId;
                selectedEquipmentItemKey = string.Empty;
                equipmentUpgradeConfirmation = string.Empty;
                equipmentStatusMessage = "已切换装备分类";
                RenderEquipmentScenePanel(root);
                ResetEquipmentScroll(root);
            });
        }
    }

    private static Button FindEquipmentSlotButton(Transform root, string prefix, string slotId)
    {
        Button button = FindSceneComponent<Button>(root, prefix + ToPascalCase(slotId));
        if (button == null && slotId == "helmet") button = FindSceneComponent<Button>(root, prefix + "Accessory");
        if (button == null && slotId == "boots") button = FindSceneComponent<Button>(root, prefix + "Pet");
        return button;
    }

    private static T FindEquipmentCellComponent<T>(Transform root, string prefix) where T : Component
    {
        if (root == null) return null;
        T[] components = root.GetComponentsInChildren<T>(true);
        for (int index = 0; index < components.Length; index++)
        {
            if (components[index].name.StartsWith(prefix, StringComparison.Ordinal)) return components[index];
        }
        return null;
    }

    private static void SetEquipmentCellText(Transform root, string prefix, string value)
    {
        Text label = FindEquipmentCellComponent<Text>(root, prefix);
        if (label != null) label.text = value;
    }

    private static void ConfigureEquipmentCategoryButton(Button button, bool selected)
    {
        if (button == null) return;
        Transform marker = button.transform.Find("Image");
        if (marker != null) marker.gameObject.SetActive(selected);
        Image background = button.targetGraphic as Image;
        if (background != null && button.transition == Selectable.Transition.SpriteSwap)
        {
            background.overrideSprite = selected && button.spriteState.selectedSprite != null
                ? button.spriteState.selectedSprite : null;
        }
    }

    private void RenderEquipmentSlots(Transform root)
    {
        for (int index = 0; index < EquipmentSlotIds.Length; index++)
        {
            string slotId = EquipmentSlotIds[index];
            InventoryItemResponse item = FindEquipmentItem(GetEquippedItemKey(slotId));
            Button slot = FindEquipmentSlotButton(root, "EquipmentSlot", slotId);
            if (slot == null) continue;
            Image icon = FindEquipmentCellComponent<Image>(slot.transform, "EquipmentSlotIconBackground");
            Text fallback = FindEquipmentCellComponent<Text>(slot.transform, "EquipmentSlotIcon");
            ApplyEquipmentIcon(icon, fallback, item, EquipmentSlotLabels[index]);
            SetEquipmentCellText(slot.transform, "EquipmentSlotName", item == null ? "空装备位" : item.name);
            SetEquipmentCellText(slot.transform, "EquipmentSlotState", EquipmentSlotLabels[index]);
            slot.interactable = item != null && !equipmentRefreshRunning && !equipmentMutationRunning;
            BindSceneButton(slot, () => SelectEquipmentItem(root, GetEquippedItemKey(slotId)));
        }
    }

    private void RenderEquipmentRows(Transform root, List<InventoryItemResponse> items)
    {
        RectTransform content = FindSceneTransform(root, "EquipmentItemP") as RectTransform;
        if (content == null) return;
        if (_equipmentItemParent != content || _equipmentRowTemplate == null)
        {
            RectTransform template = content.Find("EquipmentItemRow0") as RectTransform;
            if (template == null)
            {
                Debug.LogWarning("EquipmentItemRow0 is missing; equipment cells cannot be generated.", this);
                return;
            }
            HideEquipmentRowsFrom(0);
            _equipmentRows.Clear();
            _equipmentItemParent = content;
            _equipmentRowTemplate = template;
            ScrollRect scroll = FindSceneComponent<ScrollRect>(root, "EquipmentItemsViewport");
            _equipmentGridMinimumHeight = scroll != null && scroll.viewport != null
                ? scroll.viewport.rect.height : content.rect.height;
        }
        _equipmentRowTemplate.gameObject.SetActive(false);
        // Keep generated rows in a small runtime pool. Scene-authored children are
        // never removed, and inactive rows are reused when the list grows again.
        HideEquipmentRowsFrom(items.Count);
        while (_equipmentRows.Count < items.Count)
        {
            RectTransform row = Instantiate(_equipmentRowTemplate, content, false);
            row.name = "EquipmentItemRow" + (_equipmentRows.Count + 1);
            _equipmentRows.Add(row);
        }

        float columnStride = Mathf.Max(1f, _equipmentRowTemplate.rect.width + equipmentGridSpacing.x);
        float rowStride = Mathf.Max(1f, _equipmentRowTemplate.rect.height + equipmentGridSpacing.y);
        int rowCount = Mathf.CeilToInt(items.Count / (float)EquipmentColumns);
        float requiredHeight = rowCount == 0 ? 0f
            : GetEquipmentTemplateBottom(content) + (rowCount - 1) * rowStride;
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(_equipmentGridMinimumHeight, requiredHeight));
        for (int index = 0; index < _equipmentRows.Count; index++)
        {
            RectTransform row = _equipmentRows[index];
            if (row == null) continue;
            bool visible = index < items.Count;
            row.gameObject.SetActive(visible);
            if (!visible) continue;
            row.anchoredPosition = _equipmentRowTemplate.anchoredPosition
                + new Vector2(index % EquipmentColumns * columnStride, -index / EquipmentColumns * rowStride);
            InventoryItemResponse item = items[index];
            string itemKey = GetInventoryItemKey(item);
            Transform selection = row.Find("Select");
            if (selection != null) selection.gameObject.SetActive(itemKey == selectedEquipmentItemKey);
            Image background = row.GetComponent<Image>();
            Sprite qualitySprite = GetEquipmentQualityBackground(item.quality);
            if (background != null && qualitySprite != null)
            {
                background.sprite = qualitySprite;
                background.overrideSprite = null;
            }
            Image icon = FindEquipmentCellComponent<Image>(row, "EquipmentRowIconBackground");
            Text fallback = FindEquipmentCellComponent<Text>(row, "EquipmentRowIcon");
            ApplyEquipmentIcon(icon, fallback, item);
            SetEquipmentCellText(row, "EquipmentRowName", item.name);
            SetEquipmentCellText(row, "EquipmentRowMeta", "Lv." + item.level);
            ConfigureEquipmentWearerBadge(row, item);
            Button button = row.GetComponent<Button>();
            if (button != null) button.interactable = !equipmentRefreshRunning && !equipmentMutationRunning;
            BindSceneButton(button, () => SelectEquipmentItem(root, itemKey));
        }
    }

    private void HideEquipmentRowsFrom(int firstHiddenIndex)
    {
        for (int index = firstHiddenIndex; index < _equipmentRows.Count; index++)
        {
            RectTransform row = _equipmentRows[index];
            if (row != null) row.gameObject.SetActive(false);
        }
    }

    private void ConfigureEquipmentWearerBadge(Transform row, InventoryItemResponse item)
    {
        Transform badge = FindSceneTransform(row, "EquipmentRowNPC");
        if (badge == null) return;

        PlayerNpcResponse wearer;
        bool worn = _equipmentWearers.TryGetValue(item.playerEquipmentId, out wearer) && wearer != null;
        Sprite portrait = null;
        if (worn)
        {
            string npcId = wearer.npcId > 0 ? wearer.npcId.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
            string[] keys = { wearer.icon, wearer.npcCode, npcId, npcId != null ? "Npc_" + npcId : null, wearer.modelKey };
            portrait = ResolveRoleSprite(keys, false);
        }

        Image head = FindSceneComponent<Image>(badge, "NPCHead");
        if (head != null)
        {
            head.overrideSprite = null;
            head.sprite = portrait;
            head.enabled = portrait != null;
        }

        Image background = badge.GetComponent<Image>();
        if (background != null)
        {
            background.color = worn ? GetEquipmentWearerQualityColor(wearer.quality) : Color.white;
        }

        Text label = badge.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = worn && (head == null || portrait == null) && !string.IsNullOrEmpty(wearer.name)
                ? System.Globalization.StringInfo.GetNextTextElement(wearer.name) : string.Empty;
        }
        badge.gameObject.SetActive(worn);
    }

    private static Color GetEquipmentWearerQualityColor(string quality)
    {
        switch (GetEquipmentQualityRank(quality))
        {
            case 4: return new Color(1f, 0.75f, 0.2f, 1f);
            case 3: return new Color(0.65f, 0.32f, 0.86f, 1f);
            case 2: return new Color(0.15f, 0.55f, 0.82f, 1f);
            case 1: return new Color(0.22f, 0.68f, 0.38f, 1f);
            default: return Color.white;
        }
    }

    private float GetEquipmentTemplateBottom(RectTransform content)
    {
        float bottom = 0f;
        Vector3[] corners = new Vector3[4];
        RectTransform[] children = _equipmentRowTemplate.GetComponentsInChildren<RectTransform>(true);
        for (int index = 0; index < children.Length; index++)
        {
            children[index].GetWorldCorners(corners);
            for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
                bottom = Mathf.Max(bottom, content.rect.yMax - content.InverseTransformPoint(corners[cornerIndex]).y);
        }
        return bottom;
    }

    private Sprite GetEquipmentQualityBackground(string quality)
    {
        switch ((quality ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "COMMON": return equipmentCommonBackground;
            case "FINE": return equipmentFineBackground != null ? equipmentFineBackground : equipmentCommonBackground;
            case "EXCELLENT": return equipmentExcellentBackground;
            case "EPIC": return equipmentEpicBackground;
            case "LEGENDARY": return equipmentLegendaryBackground;
            default:
                if (_equipmentUnknownQualities.Add(quality ?? string.Empty))
                    Debug.LogWarning("Unknown equipment quality: " + quality + "; using COMMON.", this);
                return equipmentCommonBackground;
        }
    }

    private void ResetEquipmentScroll(Transform root)
    {
        ScrollRect scroll = FindSceneComponent<ScrollRect>(root, "EquipmentItemsViewport");
        if (scroll == null) return;
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1f;
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
        Image detailIconBackground = FindSceneComponent<Image>(root, "EquipmentDetailIconBackground");
        Text detailIconFallback = FindSceneComponent<Text>(root, "EquipmentDetailIcon");
        ApplyEquipmentIcon(detailIconBackground, detailIconFallback, item);
        SetSceneText(root, "EquipmentDetailName", item.name);
        SetSceneText(
            root,
            "EquipmentDetailType",
            equipmentServerBacked
                ? $"{GetEquipmentTypeLabel(item)} · Lv.{item.level} · {GetEquipmentQualityLabel(item.quality)}"
                : $"{GetEquipmentTypeLabel(item)}  ·  数量 x{item.quantity}");
        SetSceneText(root, "EquipmentDetailDescription", GetEquipmentDescriptionWithUpgradePreview(item));
        SetSceneText(root, "EquipmentDetailUpgradePreview", BuildEquipmentAutoUpgradePreviewText(item));
        SetSceneText(root, "EquipmentDetailAttack", $"攻击  +{stats.Attack}");
        SetSceneText(root, "EquipmentDetailDefense", $"防御  +{stats.Defense}");
        SetSceneText(root, "EquipmentDetailHealth", $"生命  +{stats.Health}");
        SetSceneText(root, "EquipmentDetailSpeed", $"速度  +{stats.Speed}");

        bool equipped = IsItemEquipped(item);
        PlayerNpcResponse wearer;
        bool wornByOther = _equipmentWearers.TryGetValue(item.playerEquipmentId, out wearer) && !equipped;
        Button action = FindSceneComponent<Button>(root, "EquipmentActionButton");
        if (action != null)
        {
            action.interactable = !equipmentMutationRunning && !equipmentRefreshRunning
                && !HasPendingEquipmentGrowthRequest()
                && (!equipmentServerBacked || (GetCurrentEquipmentNpc() != null && serverEquipmentLoadout != null))
                && !wornByOther
                && (equipped || string.IsNullOrEmpty(GetEquippedItemKey(GetEquipmentSlot(item))));
        }
        bool slotOccupied = !equipped && !string.IsNullOrEmpty(GetEquippedItemKey(GetEquipmentSlot(item)));
        SetSceneButtonText(action, equipped ? "卸下装备" : wornByOther ? "其他角色穿戴中" : slotOccupied ? "请先卸下" : "穿戴装备");
        BindSceneButton(action, () => ToggleEquipment(root, item));
        ConfigureServerEquipmentDetailActions(root, item);
        SetSceneText(
            root,
            "EquipmentDetailStatus",
            equipmentServerBacked
                ? $"实例 ID {item.playerEquipmentId} · {(wearer != null ? wearer.name + " 已穿戴" : "当前位于仓库")} · {(item.locked ? "已锁定" : "未锁定")}"
                : (equipped ? "当前已穿戴" : "选择穿戴后会替换同一栏位的装备"));
    }

    private void SelectEquipmentItem(Transform root, string itemKey)
    {
        InventoryItemResponse item = FindEquipmentItem(itemKey);
        if (item == null)
        {
            return;
        }

        if (selectedEquipmentItemKey != itemKey)
        {
            CloseEquipmentBreakthroughPanel();
            equipmentUpgradeConfirmation = string.Empty;
        }
        if (activeEquipmentCategory != "all" && activeEquipmentCategory != GetEquipmentSlot(item))
        {
            activeEquipmentCategory = "all";
        }
        selectedEquipmentItemKey = itemKey;
        equipmentStatusMessage = $"已选择：{item.name}";
        RenderEquipmentScenePanel(root);
    }

    private void ToggleEquipment(Transform root, InventoryItemResponse item)
    {
        if (item == null || equipmentMutationRunning || equipmentRefreshRunning || HasPendingEquipmentGrowthRequest())
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
            if (npc == null || serverEquipmentLoadout == null)
            {
                equipmentStatusMessage = "当前没有可穿戴装备的 NPC";
                RenderEquipmentScenePanel(root);
                return;
            }

            if (_equipmentWearers.TryGetValue(item.playerEquipmentId, out PlayerNpcResponse wearer)
                && wearer.playerNpcId != npc.playerNpcId)
            {
                equipmentStatusMessage = "请切换到 " + wearer.name + " 后卸下该装备";
                RenderEquipmentScenePanel(root);
                return;
            }
            if (IsItemEquipped(item))
            {
                StartCoroutine(UnequipServerEquipment(npc.playerNpcId, item.playerEquipmentId));
            }
            else
            {
                if (!string.IsNullOrEmpty(GetEquippedItemKey(slotId)))
                {
                    equipmentStatusMessage = "请先卸下该角色同部位的装备";
                    RenderEquipmentScenePanel(root);
                    return;
                }
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
                || (equipmentServerBacked && item.playerEquipmentId <= 0)
                || (!equipmentServerBacked && (item.quantity <= 0 || (!IsEquipmentItem(item) && !IsPetItem(item)))))
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

        result.Sort(CompareEquipmentItems);
        return result;
    }

    private static int GetEquipmentQualityRank(string quality)
    {
        switch ((quality ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "LEGENDARY": return 4;
            case "EPIC": return 3;
            case "EXCELLENT": return 2;
            case "FINE": return 1;
            default: return 0;
        }
    }

    private static int CompareEquipmentItems(InventoryItemResponse left, InventoryItemResponse right)
    {
        int qualityOrder = GetEquipmentQualityRank(right.quality).CompareTo(GetEquipmentQualityRank(left.quality));
        if (qualityOrder != 0) return qualityOrder;
        int levelOrder = right.level.CompareTo(left.level);
        if (levelOrder != 0) return levelOrder;
        int nameOrder = string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase);
        return nameOrder != 0 ? nameOrder : left.playerEquipmentId.CompareTo(right.playerEquipmentId);
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

        if (equipmentServerBacked) return NormalizeEquipmentType(item.equipmentType);

        string equipmentType = (!string.IsNullOrEmpty(item.equipmentType) ? item.equipmentType : item.itemType ?? string.Empty)
            .Trim()
            .ToUpperInvariant();
        if (equipmentType == "WEAPON") return "weapon";
        if (equipmentType == "HELMET") return "helmet";
        if (equipmentType == "ARMOR") return "armor";
        if (equipmentType == "BOOTS") return "boots";
        if (equipmentType == "GLOVES") return "gloves";

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
                if (item != null && IsItemEquipped(item) && GetEquipmentSlot(item) == slotId)
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
            PlayerNpcResponse wearer;
            PlayerNpcResponse selectedNpc = GetCurrentEquipmentNpc();
            return item != null && selectedNpc != null
                && _equipmentWearers.TryGetValue(item.playerEquipmentId, out wearer)
                && wearer.playerNpcId == selectedNpc.playerNpcId;
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
            return serverStats;
        }

        if (equipmentServerBacked) return new EquipmentStats();

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
            return description + $"\n已达到最高等级 Lv.{equipmentUpgradePreview.maxLevel}";
        }

        List<string> costs = new List<string>
        {
            $"金币 x{equipmentUpgradePreview.goldCost}（持有 {equipmentUpgradePreview.availableGold}）"
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
            costs.Add($"{displayName} x{resourceCost.amount}（持有 {resourceCost.availableAmount}）");
        }
        if (equipmentUpgradePreview.isBreakthrough)
        {
            costs.Add($"同款任意等级装备 x{equipmentUpgradePreview.requiredMaterialCount}（手动选择，永久消耗）");
        }
        return description
            + $"\n{(equipmentUpgradePreview.isBreakthrough ? "突破" : "升级")}至 Lv.{equipmentUpgradePreview.targetLevel}：{string.Join("，", costs)}";
    }

    private string BuildEquipmentAutoUpgradePreviewText(InventoryItemResponse item)
    {
        if (!equipmentServerBacked || item == null)
        {
            return string.Empty;
        }
        if (equipmentUpgradePreviewRequestId == item.playerEquipmentId && equipmentUpgradePreview != null
            && equipmentUpgradePreview.maxLevelReached)
        {
            return $"已达到最高等级 Lv.{equipmentUpgradePreview.maxLevel}";
        }
        if (equipmentUpgradePreviewRequestId == item.playerEquipmentId && equipmentUpgradePreview != null
            && equipmentUpgradePreview.isBreakthrough)
        {
            return "已到达突破门槛，请先选择材料手动突破，再继续一键升级";
        }
        if (equipmentAutoUpgradePreviewId != item.playerEquipmentId || equipmentAutoUpgradePreviewRunning)
        {
            return "一键升级预览：正在读取服务器配置...";
        }
        if (!string.IsNullOrEmpty(equipmentAutoUpgradePreviewError))
        {
            return "一键升级预览：" + equipmentAutoUpgradePreviewError;
        }
        if (equipmentAutoUpgradePreview == null)
        {
            return "一键升级预览：暂无数据";
        }

        EquipmentAutoUpgradePreviewData preview = equipmentAutoUpgradePreview;
        string result = $"一键升级 Lv.{preview.currentLevel} → Lv.{preview.payableTargetLevel}/{preview.requestedTargetLevel}"
            + $"  共 {preview.upgradeLevelCount} 级  {FormatAutoUpgradeOutcome(preview.outcome)}";
        if (!string.IsNullOrEmpty(preview.stopReason))
        {
            result += "\n停止原因：" + preview.stopReason;
        }
        if (preview.blockedLevel > 0 && preview.blockedRequirements != null && preview.blockedRequirements.Length > 0)
        {
            for (int requirementIndex = 0; requirementIndex < preview.blockedRequirements.Length; requirementIndex++)
            {
                EquipmentAutoUpgradeBlockedRequirementData blocked = preview.blockedRequirements[requirementIndex];
                if (blocked != null)
                {
                    result += $"\nLv.{preview.blockedLevel} 需要 {GetAutoUpgradeRequirementText(blocked)}";
                }
            }
        }
        if (preview.upgradeSteps != null && preview.upgradeSteps.Length > 0)
        {
            result += "\n逐级：";
            for (int i = 0; i < preview.upgradeSteps.Length; i++)
            {
                EquipmentAutoUpgradeStepData step = preview.upgradeSteps[i];
                if (step == null)
                {
                    continue;
                }
                long deltaAttack = GetEquipmentStepDeltaAttack(step);
                long deltaDefense = GetEquipmentStepDeltaDefense(step);
                long deltaHealth = GetEquipmentStepDeltaHealth(step);
                long deltaSpeed = GetEquipmentStepDeltaSpeed(step);
                result += $"\nLv.{step.targetLevel} 攻{deltaAttack:+#;-#;0} 防{deltaDefense:+#;-#;0} 生{deltaHealth:+#;-#;0} 速{deltaSpeed:+#;-#;0}，金币 {step.goldCost}";
                if (step.requirements != null)
                {
                    for (int requirementIndex = 0; requirementIndex < step.requirements.Length; requirementIndex++)
                    {
                        if (step.requirements[requirementIndex] != null)
                        {
                            result += "，" + GetAutoUpgradeRequirementText(step.requirements[requirementIndex]);
                        }
                    }
                }
            }
        }
        return result;
    }

    private static long GetEquipmentStepDeltaHealth(EquipmentAutoUpgradeStepData step)
    {
        return step.deltaHealth != 0 ? step.deltaHealth : step.healthBonusDelta != 0
            ? step.healthBonusDelta : step.afterHealthBonus - step.beforeHealthBonus;
    }

    private static long GetEquipmentStepDeltaAttack(EquipmentAutoUpgradeStepData step)
    {
        return step.deltaAttack != 0 ? step.deltaAttack : step.attackBonusDelta != 0
            ? step.attackBonusDelta : step.afterAttackBonus - step.beforeAttackBonus;
    }

    private static long GetEquipmentStepDeltaDefense(EquipmentAutoUpgradeStepData step)
    {
        return step.deltaDefense != 0 ? step.deltaDefense : step.defenseBonusDelta != 0
            ? step.defenseBonusDelta : step.afterDefenseBonus - step.beforeDefenseBonus;
    }

    private static long GetEquipmentStepDeltaSpeed(EquipmentAutoUpgradeStepData step)
    {
        return step.deltaSpeed != 0 ? step.deltaSpeed : step.speedBonusDelta != 0
            ? step.speedBonusDelta : step.afterSpeedBonus - step.beforeSpeedBonus;
    }

    private static string FormatAutoUpgradeOutcome(string outcome)
    {
        if (string.Equals(outcome, "TARGET_REACHED", StringComparison.OrdinalIgnoreCase))
        {
            return "可达到目标";
        }
        if (string.Equals(outcome, "PARTIAL", StringComparison.OrdinalIgnoreCase))
        {
            return "资源不足，部分升级";
        }
        if (string.Equals(outcome, "BREAKTHROUGH_REQUIRED", StringComparison.OrdinalIgnoreCase))
        {
            return "已到突破门槛，需要手动突破";
        }
        return string.IsNullOrEmpty(outcome) ? "待确认" : outcome;
    }

    private static string GetAutoUpgradeRequirementText(EquipmentAutoUpgradeBlockedRequirementData requirement)
    {
        string name = string.IsNullOrEmpty(requirement.itemKey) ? requirement.requirementType : requirement.itemKey;
        return $"{name} {requirement.availableAmount}/{requirement.requiredAmount}";
    }

    private void ResetEquipmentUpgradePreview()
    {
        CloseEquipmentBreakthroughPanel();
        _equipmentSinglePreviewVersion++;
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
            || !_equipmentDataReadSucceeded
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
        StartCoroutine(RefreshEquipmentUpgradePreview(item.playerEquipmentId, item.level));
    }

    private IEnumerator RefreshEquipmentUpgradePreview(long playerEquipmentId, int expectedLevel)
    {
        int previewVersion = ++_equipmentSinglePreviewVersion;
        int readVersion = _equipmentReadVersion;
        int playerId = GameSession.PlayerId;
        string sessionToken = GameSession.Token;
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
                || previewVersion != _equipmentSinglePreviewVersion || readVersion != _equipmentReadVersion
                || playerId != GameSession.PlayerId || sessionToken != GameSession.Token
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
                || response.data == null)
            {
                equipmentUpgradePreviewError = response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response);
            }
            else if (!IsValidEquipmentSinglePreview(response.data, playerEquipmentId, expectedLevel))
            {
                equipmentUpgradePreview = null;
                equipmentUpgradePreviewError = "装备等级或配置已变化，请重新同步后预览";
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
        switch (GetEquipmentQualityRank(quality))
        {
            case 4: return "传说";
            case 3: return "史诗";
            case 2: return "优秀";
            case 1: return "精良";
            default: return "普通";
        }
    }

    private static string GetEquipmentSlotLabel(string slotId)
    {
        if (slotId == "weapon") return "武器";
        if (slotId == "helmet") return "头盔";
        if (slotId == "armor") return "护甲";
        if (slotId == "boots") return "靴子";
        if (slotId == "gloves") return "手套";
        return string.IsNullOrEmpty(slotId) ? "未分类" : slotId.ToUpperInvariant();
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
        Transform panel = FindSceneTransform(root, "EquipmentSlotsCard");
        if (panel == null)
        {
            panel = FindSceneTransform(root, "EquipmentPanel");
        }
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

        PlayerNpcResponse selectedNpc = GetCurrentEquipmentNpc();
        if (upgrade != null) EnsureNpcAutoUpgradePreview(selectedNpc);
        bool npcMaxLevelReached = selectedNpc != null
            && selectedNpc.maxLevel > 0
            && selectedNpc.level >= selectedNpc.maxLevel;
        bool canSwitch = serverPlayerNpcs.Length > 1 && !equipmentRefreshRunning && !equipmentMutationRunning;
        if (previous != null) previous.interactable = canSwitch;
        if (next != null) next.interactable = canSwitch;
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
        if (upgrade != null) upgrade.interactable = selectedNpc != null
            && !equipmentRefreshRunning
            && !equipmentMutationRunning
            && !autoLoading
            && !autoUnavailable
            && !npcMaxLevelReached;
        SetSceneButtonText(upgrade, npcMaxLevelReached ? "已满级" : autoLoading ? "读取中..." : autoUnavailable ? "无法升级" : autoReady ? "一键升级" : "NPC升级");
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
        ResetEquipmentUpgradePreview();
        ResetEquipmentAutoUpgradePreviews();
        equipmentRefreshRunning = true;
        serverEquipmentLoadout = null;
        RebuildServerEquipmentItems();
        equipmentStatusMessage = "正在读取所选 NPC 的装备...";
        RenderEquipmentScenePanel(GetEquipmentPanelRoot());
        StartCoroutine(ChangeSelectedNpcLoadout());
    }

    private IEnumerator ChangeSelectedNpcLoadout()
    {
        int readVersion = ++_equipmentReadVersion;
        serverEquipmentLoadout = null;
        yield return RefreshSelectedNpcLoadout();
        if (readVersion != _equipmentReadVersion) yield break;
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

    private void ConfigureServerEquipmentDetailActions(Transform root, InventoryItemResponse item)
    {
        Button upgrade = FindSceneComponent<Button>(root, "EquipmentUpgradeButton");
        Button autoUpgrade = FindSceneComponent<Button>(root, "EquipmentAutoUpgradeButton");
        Button lockButton = FindSceneComponent<Button>(root, "EquipmentLockButton");
        if (!equipmentServerBacked)
        {
            if (upgrade != null) upgrade.interactable = false;
            if (autoUpgrade != null) autoUpgrade.interactable = false;
            if (lockButton != null) lockButton.interactable = false;
            return;
        }

        EnsureEquipmentAutoUpgradePreview(item);
        bool busy = equipmentMutationRunning || equipmentRefreshRunning;
        bool previewMatches = item != null && equipmentUpgradePreviewRequestId == item.playerEquipmentId;
        bool singleReady = previewMatches && equipmentUpgradePreview != null
            && equipmentUpgradePreview.currentLevel == item.level && !equipmentUpgradePreviewRunning;
        bool maxLevelReached = singleReady && (equipmentUpgradePreview.maxLevelReached
            || item.level >= equipmentUpgradePreview.maxLevel);
        bool breakthrough = singleReady && equipmentUpgradePreview.isBreakthrough;
        bool singleInsufficient = singleReady && !HasEquipmentUpgradeFunds(equipmentUpgradePreview);
        bool pending = HasPendingEquipmentGrowthRequest();
        bool singleError = !equipmentRefreshRunning && (!_equipmentDataReadSucceeded || (previewMatches && !equipmentUpgradePreviewRunning
            && !string.IsNullOrEmpty(equipmentUpgradePreviewError)));
        bool autoMatches = item != null && equipmentAutoUpgradePreviewId == item.playerEquipmentId;
        bool autoError = autoMatches && !equipmentAutoUpgradePreviewRunning
            && !string.IsNullOrEmpty(equipmentAutoUpgradePreviewError);
        bool autoReady = HasCurrentEquipmentAutoUpgradePreview(item)
            && !equipmentAutoUpgradePreviewRunning;
        bool canAutoUpgrade = autoReady && equipmentAutoUpgradePreview.payableTargetLevel > item.level;
        bool insufficient = autoReady && !canAutoUpgrade && !maxLevelReached && !breakthrough;
        string singleKey = item == null ? string.Empty : "single:" + item.playerEquipmentId + ":" + item.level
            + ":" + (equipmentUpgradePreview != null ? equipmentUpgradePreview.planFingerprint : string.Empty);
        string autoKey = autoReady ? "auto:" + item.playerEquipmentId + ":" + item.level + ":"
            + equipmentAutoUpgradePreview.requestedTargetLevel + ":" + equipmentAutoUpgradePreview.planFingerprint : string.Empty;

        if (upgrade != null) upgrade.interactable = !busy && (pending || (item != null && !maxLevelReached
            && !singleInsufficient && (singleReady || singleError)));
        if (autoUpgrade != null) autoUpgrade.interactable = !busy && !pending && !breakthrough && item != null && !maxLevelReached
            && (canAutoUpgrade || singleError || autoError);
        if (lockButton != null) lockButton.interactable = item != null && !busy && !pending;

        SetSceneButtonText(upgrade, pending ? "核对上次结果" : maxLevelReached ? "已满级" : singleInsufficient ? "资源不足"
            : singleError ? "重试预览" : !singleReady ? "读取中..."
            : breakthrough ? "突破" : equipmentUpgradeConfirmation == singleKey ? "确认升级" : "升级");
        SetSceneButtonText(autoUpgrade, pending ? "等待核对" : maxLevelReached ? "已满级" : breakthrough ? "请先突破" : insufficient ? "资源不足"
            : singleError || autoError ? "重试预览" : !autoReady ? "读取中..."
            : equipmentUpgradeConfirmation == autoKey ? "确认升级" : "一键升级");
        SetSceneButtonText(lockButton, item != null && item.locked ? "解锁" : "锁定");

        BindSceneButton(upgrade, () =>
        {
            if (busy || equipmentMutationRunning || equipmentRefreshRunning) return;
            if (HasPendingEquipmentGrowthRequest())
            {
                ResumePendingEquipmentGrowthRequest();
                return;
            }
            if (item == null) return;
            if (singleError)
            {
                RetryEquipmentUpgradePreview(root);
                return;
            }
            if (!singleReady || maxLevelReached || singleInsufficient) return;
            if (breakthrough)
            {
                ShowEquipmentBreakthroughPanel(item);
                return;
            }
            if (equipmentUpgradeConfirmation != singleKey)
            {
                equipmentUpgradeConfirmation = singleKey;
                equipmentStatusMessage = "请核对单级升级费用，再点击确认升级";
                RenderEquipmentScenePanel(root);
                return;
            }
            equipmentUpgradeConfirmation = string.Empty;
            UpgradeServerEquipment(item);
        });
        BindSceneButton(autoUpgrade, () =>
        {
            if (busy || item == null || equipmentMutationRunning || equipmentRefreshRunning
                || HasPendingEquipmentGrowthRequest() || breakthrough) return;
            if (singleError || autoError)
            {
                RetryEquipmentUpgradePreview(root);
                return;
            }
            if (!canAutoUpgrade) return;
            if (equipmentUpgradeConfirmation != autoKey)
            {
                equipmentUpgradeConfirmation = autoKey;
                equipmentStatusMessage = "请核对一键升级目标、逐级属性及消耗，再点击确认升级";
                RenderEquipmentScenePanel(root);
                return;
            }
            equipmentUpgradeConfirmation = string.Empty;
            StartCoroutine(AutoUpgradeServerEquipment(item));
        });
        BindSceneButton(lockButton, () => ToggleServerEquipmentLock(item));
    }

    private void RetryEquipmentUpgradePreview(Transform root)
    {
        if (equipmentMutationRunning || equipmentRefreshRunning || equipmentPanelObject == null) return;
        ResetEquipmentUpgradePreview();
        _equipmentAutoPreviewVersion++;
        equipmentAutoUpgradePreview = null;
        equipmentAutoUpgradePreviewId = 0L;
        equipmentAutoUpgradePreviewRunning = false;
        equipmentAutoUpgradePreviewError = string.Empty;
        equipmentUpgradeConfirmation = string.Empty;
        equipmentRefreshRunning = true;
        equipmentStatusMessage = "正在同步装备并重新读取升级预览...";
        RenderEquipmentScenePanel(root);
        StartCoroutine(GetEquipmentInventoryList());
    }

    private bool HasCurrentEquipmentAutoUpgradePreview(InventoryItemResponse item)
    {
        return _equipmentDataReadSucceeded && item != null && equipmentAutoUpgradePreviewId == item.playerEquipmentId
            && IsValidEquipmentAutoUpgradePreview(equipmentAutoUpgradePreview, item.playerEquipmentId,
                item.level, equipmentUpgradePreview != null ? equipmentUpgradePreview.maxLevel : 0);
    }

    private static bool IsValidEquipmentAutoUpgradePreview(EquipmentAutoUpgradePreviewData preview,
        long playerEquipmentId, int currentLevel, int maxLevel)
    {
        if (preview == null || preview.playerEquipmentId != playerEquipmentId
            || preview.currentLevel != currentLevel || currentLevel < 1
            || preview.requestedTargetLevel <= currentLevel
            || preview.requestedTargetLevel > Math.Min(maxLevel, 100)
            || preview.payableTargetLevel < currentLevel || preview.payableTargetLevel > preview.requestedTargetLevel
            || preview.upgradeLevelCount != preview.payableTargetLevel - currentLevel
            || !IsValidUpgradePlanFingerprint(preview.planFingerprint)) return false;
        return true;
    }

    private static bool IsValidNpcAutoUpgradePreview(
        NpcAutoUpgradePreviewData preview,
        long playerNpcId,
        int requestedTargetLevel)
    {
        return preview != null
            && preview.playerNpcId == playerNpcId
            && preview.currentLevel >= 1
            && preview.requestedTargetLevel == requestedTargetLevel
            && preview.requestedTargetLevel > preview.currentLevel
            && preview.payableTargetLevel >= preview.currentLevel
            && preview.payableTargetLevel <= preview.requestedTargetLevel
            && preview.upgradeLevelCount == preview.payableTargetLevel - preview.currentLevel
            && IsValidUpgradePlanFingerprint(preview.planFingerprint);
    }

    private void UpgradeServerEquipment(InventoryItemResponse item)
    {
        if (item != null && !equipmentMutationRunning && !equipmentRefreshRunning
            && !HasPendingEquipmentGrowthRequest())
        {
            StartCoroutine(UpgradeServerEquipmentRequest(item.playerEquipmentId));
        }
    }

    private void ToggleServerEquipmentLock(InventoryItemResponse item)
    {
        if (item != null && !equipmentMutationRunning && !equipmentRefreshRunning && !HasPendingEquipmentGrowthRequest())
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
        _equipmentAutoPreviewVersion++;
        equipmentUpgradeConfirmation = string.Empty;
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
            npcAutoUpgradePreviewError = "NPC 已满级";
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
            else if (!IsValidNpcAutoUpgradePreview(response.data, playerNpcId, targetLevel))
            {
                npcAutoUpgradePreview = null;
                npcAutoUpgradePreviewError = "NPC 升级预览与当前角色不一致，请重新预览";
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
            equipmentStatusMessage = "NPC 一键升级预览已失效，请重新读取升级方案";
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
            yield break;
        }
        if (npcAutoUpgradePreview.payableTargetLevel <= npcAutoUpgradePreview.currentLevel)
        {
            equipmentStatusMessage = "当前资源不足或 NPC 已无法继续升级";
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
            yield break;
        }

        equipmentMutationRunning = true;
        equipmentStatusMessage = "正在执行 NPC 一键升级...";
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
        if (!equipmentServerBacked || !_equipmentDataReadSucceeded || item == null || item.playerEquipmentId <= 0L || equipmentRefreshRunning || equipmentMutationRunning)
        {
            return;
        }

        if (equipmentUpgradePreviewRequestId != item.playerEquipmentId || equipmentUpgradePreview == null
            || equipmentUpgradePreviewRunning || equipmentUpgradePreview.maxLevelReached || equipmentUpgradePreview.isBreakthrough)
        {
            return;
        }
        int targetLevel = Mathf.Min(equipmentUpgradePreview.maxLevel, 100);
        if (targetLevel <= item.level)
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
        StartCoroutine(LoadEquipmentAutoUpgradePreview(item.playerEquipmentId, targetLevel));
    }

    private IEnumerator LoadEquipmentAutoUpgradePreview(long playerEquipmentId, int targetLevel)
    {
        int previewVersion = ++_equipmentAutoPreviewVersion;
        int readVersion = _equipmentReadVersion;
        int playerId = GameSession.PlayerId;
        string sessionToken = GameSession.Token;
        int currentLevel = equipmentUpgradePreview != null ? equipmentUpgradePreview.currentLevel : 0;
        UnityWebRequest request = CreateAuthorizedGet($"/equipment/{playerEquipmentId}/auto-upgrade-preview?targetLevel={targetLevel}");
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
            if (equipmentAutoUpgradePreviewId != playerEquipmentId || equipmentPanelObject == null
                || playerId != GameSession.PlayerId || sessionToken != GameSession.Token
                || previewVersion != _equipmentAutoPreviewVersion || readVersion != _equipmentReadVersion)
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
            else if (!IsValidEquipmentAutoUpgradePreview(response.data, playerEquipmentId, currentLevel, targetLevel)
                || response.data.requestedTargetLevel != targetLevel)
            {
                equipmentAutoUpgradePreview = null;
                equipmentAutoUpgradePreviewError = "升级预览与当前装备不一致，请重新预览";
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
        if (equipmentMutationRunning || equipmentRefreshRunning || HasPendingEquipmentGrowthRequest()
            || (equipmentUpgradePreview != null && equipmentUpgradePreview.isBreakthrough))
        {
            yield break;
        }
        if (!HasCurrentEquipmentAutoUpgradePreview(item))
        {
            equipmentStatusMessage = "一键升级预览已失效，请重新预览";
            RetryEquipmentUpgradePreview(GetEquipmentPanelRoot());
            yield break;
        }
        if (equipmentAutoUpgradePreview.payableTargetLevel <= equipmentAutoUpgradePreview.currentLevel)
        {
            equipmentStatusMessage = "当前资源不足或装备已无法继续升级";
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
            yield break;
        }

        EquipmentAutoUpgradeRequestPayload payload = new EquipmentAutoUpgradeRequestPayload
        {
            playerEquipmentId = item.playerEquipmentId,
            expectedCurrentLevel = equipmentAutoUpgradePreview.currentLevel,
            targetLevel = equipmentAutoUpgradePreview.requestedTargetLevel,
            planFingerprint = equipmentAutoUpgradePreview.planFingerprint
        };
        yield return SendEquipmentGrowthRequest(new EquipmentGrowthRequest
        {
            playerId = GameSession.PlayerId,
            playerEquipmentId = item.playerEquipmentId,
            expectedLevel = item.level,
            targetLevel = payload.targetLevel,
            path = "/equipment/auto-upgrade",
            json = JsonUtility.ToJson(payload),
            idempotencyKey = Guid.NewGuid().ToString("D")
        });
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
        string completionMessage = string.Empty;
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
                equipmentStatusMessage = (npc ? "NPC" : "装备") + "一键升级失败：" + (response == null
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
                        ResetEquipmentUpgradePreview();
                        _equipmentAutoPreviewVersion++;
                        equipmentUpgradeConfirmation = string.Empty;
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
            completionMessage = (npc ? "NPC" : "装备") + $"已升级到 Lv.{response.data.reachedLevel}"
                + (response.data.targetReached ? "，已达到目标等级" : "，本次仅完成部分升级")
                + "，资源已由服务器结算";
        }

        equipmentMutationRunning = false;
        ResetEquipmentAutoUpgradePreviews();
        equipmentRefreshRunning = true;
        yield return RefreshGeneralInventoryAfterEquipmentMutation();
        yield return GetEquipmentInventoryList();
        equipmentStatusMessage = completionMessage;
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
        if (equipmentMutationRunning || HasPendingEquipmentGrowthRequest())
        {
            yield break;
        }

        int readVersion = _equipmentReadVersion;
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
            if (readVersion != _equipmentReadVersion)
            {
                equipmentMutationRunning = false;
                yield break;
            }
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
            _equipmentLoadouts[payload.playerNpcId] = response.data;
        }

        equipmentRefreshRunning = true;
        yield return RefreshServerEquipmentWarehouse();
        if (readVersion != _equipmentReadVersion)
        {
            equipmentMutationRunning = false;
            yield break;
        }
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
        EquipmentUpgradePreviewData preview = equipmentUpgradePreview;
        if (!_equipmentDataReadSucceeded || preview == null || preview.playerEquipmentId != playerEquipmentId || preview.isBreakthrough
            || preview.maxLevelReached || !HasEquipmentUpgradeFunds(preview)) yield break;
        EquipmentInstanceRequest payload = new EquipmentInstanceRequest { playerEquipmentId = playerEquipmentId };
        yield return SendEquipmentGrowthRequest(new EquipmentGrowthRequest
        {
            playerId = GameSession.PlayerId,
            playerEquipmentId = playerEquipmentId,
            expectedLevel = preview.currentLevel,
            targetLevel = preview.targetLevel,
            path = "/equipment/upgrade",
            json = JsonUtility.ToJson(payload)
        });
    }

    private void SyncEquipmentUpgradeResourceBalances(EquipmentUpgradeData result)
    {
        EquipmentUpgradeResourceCostData[] resourceCosts = result.resourceCosts
            ?? new EquipmentUpgradeResourceCostData[0];
        InventoryItemResponse[] balances = new InventoryItemResponse[resourceCosts.Length + 1];
        balances[0] = new InventoryItemResponse
        {
            itemKey = "gold",
            quantity = ClampEquipmentInventoryQuantity(result.remainingGold)
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
            descriptions.Add($"{displayName} x{resourceCost.amount}");
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
        if (equipmentMutationRunning || HasPendingEquipmentGrowthRequest()) yield break;
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

        PlayerPrefs.SetInt("Gold", ClampEquipmentInventoryQuantity(result.remainingGold));
        PlayerPrefs.Save();
        RefreshHomeResourceMetrics();
        equipmentMutationRunning = false;
        ResetEquipmentAutoUpgradePreviews();
        equipmentRefreshRunning = true;
        yield return RefreshGeneralInventoryAfterEquipmentMutation();
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
        CloseEquipmentBreakthroughPanel();
        if (!equipmentMutationRunning) _equipmentReadVersion++;
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Equipment);
        if (panel != null)
        {
            panel.SetVisible(false);
        }

        equipmentPanelObject = null;
        if (!equipmentMutationRunning) equipmentRefreshRunning = false;
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
        public string description;
        public string combatAttribute;
        public string quality;
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
        public bool isBreakthrough;
        public int requiredMaterialCount;
        public int materialCandidateCount;
        public EquipmentBreakthroughMaterial[] materialCandidates;
        public long[] safeRecommendedMaterialIds;
        public long[] completeRecommendedMaterialIds;
        public bool recommendationContainsHighLevel;
        public string materialPolicy;
        public string refundPolicy;
        public string planFingerprint;
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
        public long goldCost;
        public int sameEquipmentCount;
        public long[] consumedPlayerEquipmentIds;
        public long remainingGold;
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
    private class EquipmentAutoUpgradeRequestPayload
    {
        public long playerEquipmentId;
        public int expectedCurrentLevel;
        public int targetLevel;
        public string planFingerprint;
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
        public int contractVersion;
        public int currentLevel;
        public int requestedTargetLevel;
        public int payableTargetLevel;
        public int upgradeLevelCount;
        public bool targetReached;
        public string outcome;
        public string stopReason;
        public int blockedLevel;
        public EquipmentAutoUpgradeBlockedRequirementData[] blockedRequirements;
        public string planFingerprint;
        public EquipmentAutoUpgradeStepData[] upgradeSteps;
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
        public int contractVersion;
        public int currentLevel;
        public int requestedTargetLevel;
        public int payableTargetLevel;
        public int upgradeLevelCount;
        public bool targetReached;
        public string outcome;
        public string stopReason;
        public int blockedLevel;
        public int maxLevel;
        public bool resourceSufficient;
        public EquipmentAutoUpgradeBlockedRequirementData[] blockedRequirements;
        public string planFingerprint;
        public EquipmentAutoUpgradeStepData[] upgradeSteps;
    }

    [Serializable]
    private class EquipmentAutoUpgradeBlockedRequirementData
    {
        public string requirementType;
        public string itemKey;
        public long itemId;
        public long requiredAmount;
        public long availableAmount;
        public string message;
    }

    [Serializable]
    private class EquipmentAutoUpgradeStepData
    {
        public int fromLevel;
        public int targetLevel;
        public long beforeHealth;
        public long afterHealth;
        public long beforeAttack;
        public long afterAttack;
        public long beforeDefense;
        public long afterDefense;
        public long beforeSpeed;
        public long afterSpeed;
        public long health;
        public long attack;
        public long defense;
        public long speed;
        public long deltaHealth;
        public long deltaAttack;
        public long deltaDefense;
        public long deltaSpeed;
        public long goldCost;
        public long beforeHealthBonus;
        public long afterHealthBonus;
        public long healthBonusDelta;
        public long beforeAttackBonus;
        public long afterAttackBonus;
        public long attackBonusDelta;
        public long beforeDefenseBonus;
        public long afterDefenseBonus;
        public long defenseBonusDelta;
        public long beforeSpeedBonus;
        public long afterSpeedBonus;
        public long speedBonusDelta;
        public long goldItemId;
        public string goldItemKey;
        public bool payable;
        public long[] consumedPlayerEquipmentIds;
        public EquipmentAutoUpgradeResourceCostData[] resourceCosts;
        public EquipmentAutoUpgradeBlockedRequirementData[] requirements;
    }

    [Serializable]
    private class EquipmentAutoUpgradeResourceCostData
    {
        public long itemId;
        public string itemKey;
        public string itemName;
        public long amount;
        public long availableAmount;
        public long remainingAmount;
    }

    [Serializable]
    private class AutoUpgradeApiResponse : BasicApiResponse
    {
        public AutoUpgradeData data;
    }

    [Serializable]
    private class AutoUpgradeData
    {
        public int contractVersion;
        public string requestNo;
        public long playerNpcId;
        public long playerEquipmentId;
        public int fromLevel;
        public int requestedTargetLevel;
        public int reachedLevel;
        public int upgradedLevelCount;
        public int level;
        public bool targetReached;
        public string outcome;
        public long totalGoldCost;
        public long remainingGold;
        public EquipmentAutoUpgradeResourceCostData[] totalResourceCosts;
        public string planFingerprint;
        public string businessTime;
        public EquipmentAutoUpgradeStepData[] upgradeSteps;
    }
#pragma warning restore 0649

    private struct EquipmentStats
    {
        public int Attack;
        public int Defense;
        public int Health;
        public int Speed;
    }
}
