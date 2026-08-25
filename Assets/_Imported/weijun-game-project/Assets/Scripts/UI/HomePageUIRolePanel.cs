using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public partial class HomePageUI
{
    private GameObject rolePanelObject;
    private RoleCatalogEntry[] roleCatalogEntries = new RoleCatalogEntry[0];
    private RolePlayerEntry[] rolePlayerEntries = new RolePlayerEntry[0];
    private readonly HashSet<string> roleAcquisitionEventIds = new HashSet<string>();
    private bool rolePanelLoading;
    private bool roleUpgradeRunning;
    private bool roleCatalogMode;
    private int roleSelectedNpcId;
    private string roleStatusMessage = string.Empty;

    private void ShowRolePanel()
    {
        HideSystemMailPanel();
        HideSignPanel();
        HideLotteryPanel();
        HideRechargePanel();
        HideTaskPanel();
        HideInventoryPanel();
        HideEquipmentPanel();
        HideShopPanel();
        HideAnnouncementPanel();
        HideAccountProfilePanel();
        HideFriendsPanel();
        HideSettingsPanel();

        HomePageScenePanel panel = OpenRegisteredScenePanel(HomePageScenePanelType.Role);
        if (panel == null)
        {
            SetLeftContent("角色中心", "角色面板场景节点不存在，请在 Unity 中执行 Tools/WeiJun UI/Rebuild Home Scene Panels。", "角色面板未找到");
            return;
        }

        rolePanelObject = panel.gameObject;
        roleCatalogMode = false;
        roleSelectedNpcId = 0;
        rolePanelLoading = true;
        roleUpgradeRunning = false;
        roleStatusMessage = HasAuthSession() ? "正在同步服务器角色数据..." : "请先登录账号后查看角色数据";
        RenderRolePanel();

        if (HasAuthSession())
        {
            StartCoroutine(LoadRolePanelData());
        }
    }

    private IEnumerator LoadRolePanelData()
    {
        UnityWebRequest catalogRequest = CreateAuthorizedGet("/npc/catalog");
        if (catalogRequest == null)
        {
            rolePanelLoading = false;
            roleStatusMessage = requestCreationError;
            RenderRolePanel();
            yield break;
        }

        using (catalogRequest)
        {
            yield return catalogRequest.SendWebRequest();
            if (HandleUnauthorizedResponse(catalogRequest))
            {
                rolePanelLoading = false;
                yield break;
            }

            RoleCatalogApiResponse catalogResponse = ParseJson<RoleCatalogApiResponse>(catalogRequest.downloadHandler.text);
            if (catalogRequest.result != UnityWebRequest.Result.Success
                || catalogResponse == null
                || catalogResponse.code != 200
                || catalogResponse.data == null)
            {
                rolePanelLoading = false;
                roleStatusMessage = "角色图鉴读取失败：" + (catalogResponse == null
                    ? ApiRequestUtility.GetErrorMessage(catalogRequest, "服务器返回数据格式错误")
                    : GetApiError(catalogResponse));
                RenderRolePanel();
                yield break;
            }

            roleCatalogEntries = catalogResponse.data;
        }

        int playerId = GameSession.PlayerId;
        UnityWebRequest playerRequest = CreateAuthorizedGet($"/npc/list/{playerId}");
        if (playerRequest == null)
        {
            rolePanelLoading = false;
            roleStatusMessage = requestCreationError;
            RenderRolePanel();
            yield break;
        }

        using (playerRequest)
        {
            yield return playerRequest.SendWebRequest();
            if (HandleUnauthorizedResponse(playerRequest))
            {
                rolePanelLoading = false;
                yield break;
            }

            RolePlayerApiResponse playerResponse = ParseJson<RolePlayerApiResponse>(playerRequest.downloadHandler.text);
            if (playerRequest.result != UnityWebRequest.Result.Success
                || playerResponse == null
                || playerResponse.code != 200
                || playerResponse.data == null)
            {
                rolePanelLoading = false;
                roleStatusMessage = "我的角色读取失败：" + (playerResponse == null
                    ? ApiRequestUtility.GetErrorMessage(playerRequest, "服务器返回数据格式错误")
                    : GetApiError(playerResponse));
                RenderRolePanel();
                yield break;
            }

            rolePlayerEntries = playerResponse.data;
        }

        rolePanelLoading = false;
        if (roleCatalogEntries.Length > 0 && roleSelectedNpcId == 0)
        {
            RoleDisplayEntry first = GetRoleDisplayEntries(false).Count > 0
                ? GetRoleDisplayEntries(false)[0]
                : null;
            roleSelectedNpcId = first == null ? roleCatalogEntries[0].npcId : first.catalog.npcId;
        }

        roleStatusMessage = $"已同步 {roleCatalogEntries.Length} 个角色配置，拥有 {rolePlayerEntries.Length} 个角色";
        RenderRolePanel();
    }

    private void RenderRolePanel()
    {
        if (rolePanelObject == null)
        {
            return;
        }

        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Role);
        if (panel == null || panel.PanelRoot == null)
        {
            return;
        }

        Transform root = panel.PanelRoot;
        BindSceneButton(FindSceneComponent<Button>(root, "RoleCloseButton"), HideRolePanel);
        BindSceneButton(FindSceneComponent<Button>(root, "RoleOwnedTab"), () => SetRoleTab(false));
        BindSceneButton(FindSceneComponent<Button>(root, "RoleCatalogTab"), () => SetRoleTab(true));
        SetSceneText(root, "RoleStatus", roleStatusMessage);

        ConfigureRoleTabButton(FindSceneComponent<Button>(root, "RoleOwnedTab"), !roleCatalogMode);
        ConfigureRoleTabButton(FindSceneComponent<Button>(root, "RoleCatalogTab"), roleCatalogMode);

        List<RoleDisplayEntry> visibleEntries = GetRoleDisplayEntries(roleCatalogMode);
        SetSceneText(root, "RoleListTitle", roleCatalogMode
            ? $"角色图鉴  {visibleEntries.Count}"
            : $"我的角色  {visibleEntries.Count}");

        for (int i = 0; i < 20; i++)
        {
            Transform row = FindSceneTransform(root, "RoleListRow" + i);
            if (row == null)
            {
                continue;
            }

            bool visible = i < visibleEntries.Count;
            row.gameObject.SetActive(visible);
            if (visible)
            {
                ConfigureRoleListRow(row, visibleEntries[i]);
            }
        }

        ConfigureRoleDetail(root, FindRoleDisplayEntry(roleSelectedNpcId));
    }

    private void SetRoleTab(bool catalog)
    {
        roleCatalogMode = catalog;
        List<RoleDisplayEntry> entries = GetRoleDisplayEntries(catalog);
        if (entries.Count > 0)
        {
            roleSelectedNpcId = entries[0].catalog.npcId;
        }

        RenderRolePanel();
    }

    private void ConfigureRoleTabButton(Button button, bool selected)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = selected
                ? new Color(0.20f, 0.56f, 0.66f, 1f)
                : new Color(0.90f, 0.79f, 0.59f, 1f);
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.color = selected ? Color.white : new Color(0.36f, 0.20f, 0.10f, 1f);
        }
    }

    private List<RoleDisplayEntry> GetRoleDisplayEntries(bool catalog)
    {
        List<RoleDisplayEntry> result = new List<RoleDisplayEntry>();
        for (int i = 0; i < roleCatalogEntries.Length; i++)
        {
            RoleCatalogEntry entry = roleCatalogEntries[i];
            if (entry == null)
            {
                continue;
            }

            RolePlayerEntry player = FindPlayerRole(entry.npcId);
            bool owned = player != null || entry.owned;
            if (!catalog && !owned)
            {
                continue;
            }

            result.Add(new RoleDisplayEntry(entry, player, owned));
        }

        return result;
    }

    private RoleDisplayEntry FindRoleDisplayEntry(int npcId)
    {
        List<RoleDisplayEntry> entries = GetRoleDisplayEntries(roleCatalogMode);
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].catalog.npcId == npcId)
            {
                return entries[i];
            }
        }

        return entries.Count > 0 ? entries[0] : null;
    }

    private RolePlayerEntry FindPlayerRole(int npcId)
    {
        for (int i = 0; i < rolePlayerEntries.Length; i++)
        {
            if (rolePlayerEntries[i] != null && rolePlayerEntries[i].npcId == npcId)
            {
                return rolePlayerEntries[i];
            }
        }

        return null;
    }

    private void ConfigureRoleListRow(Transform row, RoleDisplayEntry entry)
    {
        SetSceneText(row, "RoleListRowName" + GetRoleRowIndex(row), entry.catalog.name ?? "未命名角色");
        SetSceneText(row, "RoleListRowQuality" + GetRoleRowIndex(row), "品质  " + FormatRoleQuality(entry.catalog.quality));
        SetSceneText(row, "RoleListRowLevel" + GetRoleRowIndex(row), entry.player == null ? "待获得" : $"Lv.{Mathf.Max(1, entry.player.level)}");
        SetSceneText(row, "RoleListRowStatus" + GetRoleRowIndex(row), GetRoleStatusLabel(entry));

        Image icon = FindSceneComponent<Image>(row, "RoleListRowIcon" + GetRoleRowIndex(row));
        TMP_Text fallback = FindSceneComponent<TMP_Text>(row, "RoleListRowIconText" + GetRoleRowIndex(row));
        ApplyRoleIcon(icon, fallback, entry.catalog.icon, entry.catalog.name);

        Button button = row.GetComponent<Button>();
        BindSceneButton(button, () =>
        {
            roleSelectedNpcId = entry.catalog.npcId;
            RenderRolePanel();
        });

        Image background = row.GetComponent<Image>();
        if (background != null)
        {
            background.color = entry.catalog.npcId == roleSelectedNpcId
                ? new Color(1f, 0.84f, 0.45f, 1f)
                : new Color(1f, 0.95f, 0.78f, 1f);
        }
    }

    private static int GetRoleRowIndex(Transform row)
    {
        string value = row == null ? string.Empty : row.name.Replace("RoleListRow", string.Empty);
        return int.TryParse(value, out int index) ? index : 0;
    }

    private void ConfigureRoleDetail(Transform root, RoleDisplayEntry entry)
    {
        Button upgrade = FindSceneComponent<Button>(root, "RoleDetailUpgradeButton");
        Button acquire = FindSceneComponent<Button>(root, "RoleDetailAcquireButton");
        if (entry == null)
        {
            SetSceneText(root, "RoleDetailName", "暂无角色数据");
            SetSceneText(root, "RoleDetailQuality", string.Empty);
            SetSceneText(root, "RoleDetailStatus", rolePanelLoading ? "正在加载..." : "暂无可显示角色");
            SetSceneText(root, "RoleDetailLevel", string.Empty);
            SetSceneText(root, "RoleDetailAttribute", string.Empty);
            SetSceneText(root, "RoleDetailProduction", string.Empty);
            SetSceneText(root, "RoleDetailDescription", string.Empty);
            SetSceneText(root, "RoleDetailAcquisition", string.Empty);
            if (upgrade != null) upgrade.gameObject.SetActive(false);
            if (acquire != null) acquire.gameObject.SetActive(false);
            return;
        }

        SetSceneText(root, "RoleDetailName", entry.catalog.name ?? "未命名角色");
        SetSceneText(root, "RoleDetailQuality", "品质  " + FormatRoleQuality(entry.catalog.quality));
        SetSceneText(root, "RoleDetailStatus", "状态  " + GetRoleStatusLabel(entry));
        SetSceneText(root, "RoleDetailLevel", entry.player == null
            ? "等级  待获得"
            : $"等级  Lv.{Mathf.Max(1, entry.player.level)} / {Mathf.Max(1, entry.catalog.maxLevel)}");
        SetSceneText(root, "RoleDetailAttribute", "战斗属性  " + (string.IsNullOrEmpty(entry.catalog.combatAttribute) ? "暂无" : entry.catalog.combatAttribute));
        SetSceneText(root, "RoleDetailProduction", $"产出加成  {entry.catalog.produceBonusRate * 100f:0.##}%");
        SetSceneText(root, "RoleDetailDescription", string.IsNullOrEmpty(entry.catalog.description) ? "暂无角色描述" : entry.catalog.description);
        SetSceneText(root, "RoleDetailAcquisition", BuildRoleAcquisitionText(entry));

        Image icon = FindSceneComponent<Image>(root, "RoleDetailIcon");
        TMP_Text fallback = FindSceneComponent<TMP_Text>(root, "RoleDetailIconText");
        ApplyRoleIcon(icon, fallback, entry.catalog.icon, entry.catalog.name);

        if (upgrade != null)
        {
            upgrade.gameObject.SetActive(entry.player != null);
            upgrade.interactable = entry.player != null && !roleUpgradeRunning && entry.player.level < entry.catalog.maxLevel;
            SetSceneButtonText(upgrade, entry.player != null && entry.player.level >= entry.catalog.maxLevel ? "已达上限" : "升级");
            BindSceneButton(upgrade, entry.player == null || roleUpgradeRunning ? null : () => StartCoroutine(UpgradeRole(entry.player.playerNpcId)));
        }

        if (acquire != null)
        {
            bool canAcquire = entry.player == null && entry.catalog.acquisition != null
                && string.Equals(entry.catalog.acquisition.status, "AVAILABLE", StringComparison.OrdinalIgnoreCase);
            acquire.gameObject.SetActive(entry.player == null);
            acquire.interactable = canAcquire && !rolePanelLoading;
            SetSceneButtonText(acquire, canAcquire && !string.IsNullOrEmpty(entry.catalog.acquisition.buttonText)
                ? entry.catalog.acquisition.buttonText
                : "前往获取");
            BindSceneButton(acquire, canAcquire ? () => StartCoroutine(GetRoleAcquisitionGuide(entry.catalog.npcId)) : null);
        }
    }

    private IEnumerator UpgradeRole(long playerNpcId)
    {
        if (playerNpcId <= 0 || roleUpgradeRunning)
        {
            yield break;
        }

        roleUpgradeRunning = true;
        roleStatusMessage = "正在请求服务器升级角色...";
        RenderRolePanel();
        if (!TryCreateAuthorizedPost("/npc/upgrade", "{\"playerNpcId\":" + playerNpcId + "}", out UnityWebRequest request, out string error))
        {
            roleUpgradeRunning = false;
            roleStatusMessage = error;
            RenderRolePanel();
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                roleUpgradeRunning = false;
                yield break;
            }

            RoleUpgradeApiResponse response = ParseJson<RoleUpgradeApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                roleUpgradeRunning = false;
                roleStatusMessage = "角色升级失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response));
                RenderRolePanel();
                yield break;
            }

            roleStatusMessage = $"{response.data.name} 已升级到 Lv.{response.data.level}，资源已由服务器结算";
        }

        roleUpgradeRunning = false;
        yield return LoadRolePanelData();
    }

    private IEnumerator GetRoleAcquisitionGuide(int npcId)
    {
        UnityWebRequest request = CreateAuthorizedGet($"/npc/{npcId}/acquisition-guide");
        if (request == null)
        {
            roleStatusMessage = requestCreationError;
            RenderRolePanel();
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                yield break;
            }

            RoleAcquisitionGuideApiResponse response = ParseJson<RoleAcquisitionGuideApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                roleStatusMessage = "获取指引读取失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response));
                RenderRolePanel();
                yield break;
            }

            RoleAcquisitionGuideData guide = response.data;
            if (guide.contractVersion > 1)
            {
                roleStatusMessage = "角色获取协议版本过高，请更新客户端";
                RenderRolePanel();
                yield break;
            }

            if (!string.IsNullOrEmpty(guide.instructionExpiresAt)
                && DateTime.TryParse(guide.instructionExpiresAt, out DateTime expiresAt)
                && DateTime.UtcNow > expiresAt.ToUniversalTime())
            {
                roleStatusMessage = "获取指引已过期，请重新点击获取";
                RenderRolePanel();
                yield break;
            }

            if (string.Equals(guide.action, "SHOW_TOAST", StringComparison.OrdinalIgnoreCase))
            {
                roleStatusMessage = string.IsNullOrEmpty(guide.message) ? "当前暂不可获取" : guide.message;
                RenderRolePanel();
                yield break;
            }

            if (!string.Equals(guide.action, "REDIRECT", StringComparison.OrdinalIgnoreCase))
            {
                roleStatusMessage = "服务器返回了不支持的获取动作";
                RenderRolePanel();
                yield break;
            }

            RouteRoleAcquisitionGuide(guide);
        }
    }

    private void RouteRoleAcquisitionGuide(RoleAcquisitionGuideData guide)
    {
        string routeKey = guide.routeKey ?? string.Empty;
        switch (routeKey.ToUpperInvariant())
        {
            case "CARD_DRAW":
                if (guide.routeParams == null || string.IsNullOrEmpty(guide.routeParams.poolCode))
                {
                    roleStatusMessage = "抽奖入口参数无效";
                    RenderRolePanel();
                    break;
                }
                HideRolePanel();
                ShowLotteryContent();
                break;
            case "PLAYER_TASK":
                if (guide.routeParams == null || string.IsNullOrEmpty(guide.routeParams.taskCode))
                {
                    roleStatusMessage = "任务入口参数无效";
                    RenderRolePanel();
                    break;
                }
                HideRolePanel();
                ShowTaskContent();
                break;
            case "SIGN_IN":
                if (guide.routeParams == null || string.IsNullOrEmpty(guide.routeParams.planCode))
                {
                    roleStatusMessage = "签到入口参数无效";
                    RenderRolePanel();
                    break;
                }
                HideRolePanel();
                ShowDailySignContent();
                break;
            case "MAIL":
                if (guide.routeParams == null || guide.routeParams.mailId <= 0)
                {
                    roleStatusMessage = "邮件入口参数无效";
                    RenderRolePanel();
                    break;
                }
                HideRolePanel();
                ShowMailContent();
                break;
            case "IDLE_ADVENTURE":
                if (guide.routeParams == null || string.IsNullOrEmpty(guide.routeParams.modeCode))
                {
                    roleStatusMessage = "挂机入口参数无效";
                    RenderRolePanel();
                    break;
                }
                HideRolePanel();
                SceneManager.LoadScene(IdleAdventureSceneName);
                break;
            default:
                roleStatusMessage = string.IsNullOrEmpty(guide.message) ? "暂无可用获取入口" : guide.message;
                RenderRolePanel();
                break;
        }
    }

    private void HideRolePanel()
    {
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Role);
        if (panel != null)
        {
            panel.SetVisible(false);
        }

        rolePanelObject = null;
        rolePanelLoading = false;
        roleUpgradeRunning = false;
    }

    private void RefreshRolePanelFromRealtime(string acquisitionId)
    {
        if (!string.IsNullOrEmpty(acquisitionId) && !roleAcquisitionEventIds.Add(acquisitionId))
        {
            return;
        }

        if (rolePanelObject != null && rolePanelObject.activeInHierarchy && HasAuthSession() && !rolePanelLoading)
        {
            StartCoroutine(LoadRolePanelData());
        }
    }

    private void ApplyRoleIcon(Image image, TMP_Text fallback, string iconKey, string roleName)
    {
        if (image == null)
        {
            return;
        }

        Sprite sprite = null;
        string[] candidates =
        {
            iconKey,
            "Sprites/" + iconKey,
            "Sprites/Arts/" + iconKey,
            "Sprites/Arts/png/" + iconKey
        };
        for (int i = 0; i < candidates.Length && sprite == null; i++)
        {
            string candidate = candidates[i];
            if (string.IsNullOrEmpty(candidate))
            {
                continue;
            }

            sprite = LoadSpriteFromAsset(candidate.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                ? candidate
                : candidate + ".png");
        }

        image.sprite = sprite;
        image.color = sprite == null ? new Color(0.28f, 0.62f, 0.76f, 1f) : Color.white;
        image.preserveAspect = true;
        if (fallback != null)
        {
            fallback.gameObject.SetActive(sprite == null);
            fallback.text = string.IsNullOrEmpty(roleName) ? "NPC" : roleName.Substring(0, 1);
        }
    }

    private static string FormatRoleQuality(string quality)
    {
        return string.IsNullOrEmpty(quality) ? "未知" : quality;
    }

    private static string GetRoleStatusLabel(RoleDisplayEntry entry)
    {
        if (entry == null)
        {
            return "未知";
        }

        if (entry.owned)
        {
            return "已获得";
        }

        string status = entry.catalog.acquisition == null ? string.Empty : entry.catalog.acquisition.status;
        switch ((status ?? string.Empty).ToUpperInvariant())
        {
            case "AVAILABLE": return "可获得";
            case "LOCKED": return "待解锁";
            case "UPCOMING": return "即将开放";
            case "ENDED": return "活动结束";
            case "UNAVAILABLE": return "暂无途径";
            default: return "待获得";
        }
    }

    private static string BuildRoleAcquisitionText(RoleDisplayEntry entry)
    {
        if (entry == null || entry.owned || entry.catalog.acquisition == null)
        {
            return entry != null && entry.owned ? "获取方式  已获得" : "获取方式  暂无服务器说明";
        }

        NpcAcquisitionEntry acquisition = entry.catalog.acquisition;
        string displayName = string.IsNullOrEmpty(acquisition.displayName) ? acquisition.channelType : acquisition.displayName;
        string message = string.IsNullOrEmpty(acquisition.message) ? string.Empty : "\n" + acquisition.message;
        return "获取方式  " + (string.IsNullOrEmpty(displayName) ? "暂无" : displayName) + message;
    }

    [Serializable]
    private sealed class RoleCatalogApiResponse : BasicApiResponse
    {
        public RoleCatalogEntry[] data;
    }

    [Serializable]
    private sealed class RoleCatalogEntry
    {
        public int npcId;
        public string npcCode;
        public string name;
        public string quality;
        public string icon;
        public string modelKey;
        public string description;
        public string combatAttribute;
        public int maxLevel;
        public float produceBonusRate;
        public bool owned;
        public NpcAcquisitionEntry acquisition;
        public int configVersion;
        public int contractVersion;
    }

    [Serializable]
    private sealed class NpcAcquisitionEntry
    {
        public string status;
        public string channelType;
        public string displayName;
        public string buttonText;
        public string message;
    }

    [Serializable]
    private sealed class RolePlayerApiResponse : BasicApiResponse
    {
        public RolePlayerEntry[] data;
    }

    [Serializable]
    private sealed class RolePlayerEntry
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
    private sealed class RoleUpgradeApiResponse : BasicApiResponse
    {
        public RoleUpgradeData data;
    }

    [Serializable]
    private sealed class RoleUpgradeData
    {
        public long playerNpcId;
        public string name;
        public int level;
    }

    [Serializable]
    private sealed class RoleAcquisitionGuideApiResponse : BasicApiResponse
    {
        public RoleAcquisitionGuideData data;
    }

    [Serializable]
    private sealed class RoleAcquisitionGuideData
    {
        public int npcId;
        public string status;
        public string action;
        public string channelType;
        public string sourceRefKey;
        public string routeKey;
        public string message;
        public int contractVersion;
        public string instructionExpiresAt;
        public RoleAcquisitionRouteParams routeParams;
    }

    [Serializable]
    private sealed class RoleAcquisitionRouteParams
    {
        public string poolCode;
        public int highlightNpcId;
        public string taskCode;
        public string planCode;
        public string modeCode;
        public int stage;
        public string poolKey;
        public long mailId;
    }

    private sealed class RoleDisplayEntry
    {
        public readonly RoleCatalogEntry catalog;
        public readonly RolePlayerEntry player;
        public readonly bool owned;

        public RoleDisplayEntry(RoleCatalogEntry catalog, RolePlayerEntry player, bool owned)
        {
            this.catalog = catalog;
            this.player = player;
            this.owned = owned;
        }
    }
}
