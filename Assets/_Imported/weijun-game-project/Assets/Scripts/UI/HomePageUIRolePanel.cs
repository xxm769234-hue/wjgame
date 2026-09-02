using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public partial class HomePageUI
{
    private const string PendingRoleAcquisitionRouteKey = "weijun.npcAcquisition.routeKey";
    private const string PendingRoleAcquisitionRoutePlayerIdKey = "weijun.npcAcquisition.playerId";
    private const string PendingRoleAcquisitionRouteExpiresAtKey = "weijun.npcAcquisition.expiresAt";
    private const string PendingRoleAcquisitionRoutePoolCodeKey = "weijun.npcAcquisition.poolCode";
    private const string PendingRoleAcquisitionRouteHighlightNpcIdKey = "weijun.npcAcquisition.highlightNpcId";
    private const string PendingRoleAcquisitionRouteTaskCodeKey = "weijun.npcAcquisition.taskCode";
    private const string PendingRoleAcquisitionRoutePlanCodeKey = "weijun.npcAcquisition.planCode";
    private const string PendingRoleAcquisitionRouteModeCodeKey = "weijun.npcAcquisition.modeCode";
    private const string PendingRoleAcquisitionRouteStageKey = "weijun.npcAcquisition.stage";
    private const string PendingRoleAcquisitionRoutePoolKeyKey = "weijun.npcAcquisition.poolKey";
    private const string PendingRoleAcquisitionRouteMailIdKey = "weijun.npcAcquisition.mailId";
    private const int MaxRoleAcquisitionRouteTokenLength = 128;

    private GameObject rolePanelObject;
    private RoleCatalogEntry[] roleCatalogEntries = new RoleCatalogEntry[0];
    private RolePlayerEntry[] rolePlayerEntries = new RolePlayerEntry[0];
    private readonly HashSet<string> roleAcquisitionEventIds = new HashSet<string>();
    private bool rolePanelLoading;
    private bool roleUpgradeRunning;
    private bool roleCatalogMode;
    private int roleSelectedNpcId;
    private string roleStatusMessage = string.Empty;
    private RoleAutoUpgradePreviewData roleAutoUpgradePreview;
    private long roleAutoUpgradePreviewNpcId;
    private bool roleAutoUpgradePreviewRunning;
    private string roleAutoUpgradePreviewError = string.Empty;
    private string roleAutoUpgradeIdempotencyKey = string.Empty;
    private string roleAutoUpgradeRequestSignature = string.Empty;
    private readonly Dictionary<string, string> roleAutoUpgradePendingKeys = new Dictionary<string, string>();

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
            SetLeftContent("角色中心", "角色面板场景节点不存在，请重新生成首页面板。", "角色面板未找到");
            return;
        }

        rolePanelObject = panel.gameObject;
        roleCatalogMode = false;
        roleSelectedNpcId = 0;
        rolePanelLoading = true;
        roleUpgradeRunning = false;
        ResetRoleAutoUpgradePreview();
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
        SetSceneText(row, "RoleListRowLevel" + GetRoleRowIndex(row), entry.player == null ? "待获得" : $"等级 {Mathf.Max(1, entry.player.level)}");
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
            : $"等级  {Mathf.Max(1, entry.player.level)} / {Mathf.Max(1, entry.catalog.maxLevel)}");
        SetSceneText(root, "RoleDetailAttribute", "战斗属性  " + (string.IsNullOrEmpty(entry.catalog.combatAttribute) ? "暂无" : entry.catalog.combatAttribute));
        SetSceneText(root, "RoleDetailProduction", $"产出加成  {entry.catalog.produceBonusRate * 100f:0.##}%");
        SetSceneText(root, "RoleDetailDescription", string.IsNullOrEmpty(entry.catalog.description) ? "暂无角色描述" : entry.catalog.description);
        SetSceneText(root, "RoleDetailAcquisition", BuildRoleAcquisitionText(entry));

        EnsureRoleAutoUpgradePreview(entry);

        Image icon = FindSceneComponent<Image>(root, "RoleDetailIcon");
        TMP_Text fallback = FindSceneComponent<TMP_Text>(root, "RoleDetailIconText");
        ApplyRoleIcon(icon, fallback, entry.catalog.icon, entry.catalog.name);

        if (upgrade != null)
        {
            upgrade.gameObject.SetActive(entry.player != null);
            bool previewMatches = entry.player != null && roleAutoUpgradePreviewNpcId == entry.player.playerNpcId;
            bool previewLoading = previewMatches && roleAutoUpgradePreviewRunning;
            bool maxLevelReached = previewMatches && roleAutoUpgradePreview != null
                && roleAutoUpgradePreview.payableTargetLevel <= roleAutoUpgradePreview.currentLevel;
            bool useAutoUpgrade = previewMatches && roleAutoUpgradePreview != null;
            upgrade.interactable = entry.player != null && !roleUpgradeRunning && !previewLoading && !maxLevelReached;
            SetSceneButtonText(upgrade, entry.player == null || maxLevelReached
                ? "已达上限"
                : previewLoading
                    ? "读取中..."
                    : useAutoUpgrade
                        ? $"一键升至等级 {roleAutoUpgradePreview.payableTargetLevel}"
                        : "升级");
            Action upgradeAction = null;
            if (entry.player != null && !roleUpgradeRunning && !previewLoading)
            {
                upgradeAction = useAutoUpgrade
                    ? (Action)(() => StartCoroutine(AutoUpgradeRole(entry.player, entry.catalog.maxLevel)))
                    : () => StartCoroutine(UpgradeRole(entry.player.playerNpcId));
            }
            BindSceneButton(upgrade, upgradeAction);
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
            Action acquireAction = canAcquire
                ? (Action)(() => StartCoroutine(GetRoleAcquisitionGuide(entry.catalog.npcId)))
                : null;
            BindSceneButton(acquire, acquireAction);
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
            if (roleAutoUpgradePreviewNpcId != playerNpcId)
            {
                yield break;
            }
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

            roleStatusMessage = $"{response.data.name} 已升级到等级 {response.data.level}，资源已由服务器结算";
        }

        roleUpgradeRunning = false;
        yield return RefreshGeneralInventoryAfterRoleMutation();
        yield return LoadRolePanelData();
    }

    private IEnumerator RefreshGeneralInventoryAfterRoleMutation()
    {
        List<string> syncedCategories = new List<string>();
        List<string> failedCategories = new List<string>();
        yield return RefreshCardDrawInventoryState(syncedCategories, failedCategories);
        if (failedCategories.Count > 0)
        {
            roleStatusMessage = "升级成功，但背包刷新失败："
                + string.Join(", ", failedCategories.ToArray());
        }
    }

    private void ResetRoleAutoUpgradePreview()
    {
        roleAutoUpgradePreview = null;
        roleAutoUpgradePreviewNpcId = 0L;
        roleAutoUpgradePreviewRunning = false;
        roleAutoUpgradePreviewError = string.Empty;
        roleAutoUpgradeIdempotencyKey = string.Empty;
        roleAutoUpgradeRequestSignature = string.Empty;
    }

    private void EnsureRoleAutoUpgradePreview(RoleDisplayEntry entry)
    {
        if (!HasAuthSession() || entry == null || entry.player == null || entry.player.playerNpcId <= 0L)
        {
            return;
        }

        long playerNpcId = entry.player.playerNpcId;
        if (roleAutoUpgradePreviewNpcId == playerNpcId
            && (roleAutoUpgradePreviewRunning || roleAutoUpgradePreview != null || !string.IsNullOrEmpty(roleAutoUpgradePreviewError)))
        {
            return;
        }

        if (roleAutoUpgradePreviewNpcId != playerNpcId)
        {
            roleAutoUpgradeIdempotencyKey = string.Empty;
            roleAutoUpgradeRequestSignature = string.Empty;
        }

        roleAutoUpgradePreview = null;
        roleAutoUpgradePreviewNpcId = playerNpcId;
        roleAutoUpgradePreviewRunning = true;
        roleAutoUpgradePreviewError = string.Empty;
        int targetLevel = entry.catalog.maxLevel > 0 ? entry.catalog.maxLevel : entry.player.level;
        StartCoroutine(LoadRoleAutoUpgradePreview(playerNpcId, Mathf.Max(1, targetLevel)));
    }

    private IEnumerator LoadRoleAutoUpgradePreview(long playerNpcId, int targetLevel)
    {
        UnityWebRequest request = CreateAuthorizedGet($"/npc/{playerNpcId}/auto-upgrade-preview?targetLevel={Mathf.Clamp(targetLevel, 1, 100)}");
        if (request == null)
        {
            roleAutoUpgradePreviewRunning = false;
            roleAutoUpgradePreviewError = requestCreationError;
            RenderRolePanel();
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (roleAutoUpgradePreviewNpcId != playerNpcId)
            {
                yield break;
            }
            if (HandleUnauthorizedResponse(request))
            {
                roleAutoUpgradePreviewRunning = false;
                yield break;
            }

            RoleAutoUpgradePreviewApiResponse response = ParseJson<RoleAutoUpgradePreviewApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                roleAutoUpgradePreviewError = response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response);
            }
            else
            {
                roleAutoUpgradePreview = response.data;
                roleAutoUpgradePreviewError = string.Empty;
            }
        }

        roleAutoUpgradePreviewRunning = false;
        RenderRolePanel();
    }

    private IEnumerator AutoUpgradeRole(RolePlayerEntry player, int fallbackTargetLevel)
    {
        if (player == null || player.playerNpcId <= 0L || roleUpgradeRunning) yield break;

        bool hasMatchingPreview = roleAutoUpgradePreview != null
            && roleAutoUpgradePreviewNpcId == player.playerNpcId;
        int currentLevel = hasMatchingPreview
            ? Mathf.Max(1, roleAutoUpgradePreview.currentLevel)
            : Mathf.Max(1, player.level);
        int payableTargetLevel = hasMatchingPreview
            ? roleAutoUpgradePreview.payableTargetLevel
            : Mathf.Max(currentLevel, fallbackTargetLevel);
        if (payableTargetLevel <= currentLevel)
        {
            roleStatusMessage = string.IsNullOrEmpty(roleAutoUpgradePreviewError) ? "角色已经无法继续升级" : roleAutoUpgradePreviewError;
            RenderRolePanel();
            yield break;
        }
        int targetLevel = hasMatchingPreview && roleAutoUpgradePreview.requestedTargetLevel > currentLevel
            ? roleAutoUpgradePreview.requestedTargetLevel
            : payableTargetLevel;

        string planFingerprint = roleAutoUpgradePreview == null ? string.Empty : roleAutoUpgradePreview.planFingerprint;
        if (string.IsNullOrEmpty(planFingerprint))
        {
            roleStatusMessage = "升级方案已失效，请重新读取后再试";
            ResetRoleAutoUpgradePreview();
            RenderRolePanel();
            yield break;
        }

        string requestSignature = GameSession.PlayerId
            + ":"
            + player.playerNpcId
            + ":"
            + currentLevel
            + ":"
            + targetLevel
            + ":"
            + planFingerprint;
        roleUpgradeRunning = true;
        if (!roleAutoUpgradePendingKeys.TryGetValue(requestSignature, out string pendingIdempotencyKey)
            || !Guid.TryParse(pendingIdempotencyKey, out _))
        {
            pendingIdempotencyKey = Guid.NewGuid().ToString("D");
            roleAutoUpgradePendingKeys[requestSignature] = pendingIdempotencyKey;
        }
        roleAutoUpgradeIdempotencyKey = pendingIdempotencyKey;
        roleAutoUpgradeRequestSignature = requestSignature;
        roleStatusMessage = "正在执行角色一键升级...";
        RenderRolePanel();

        string json = JsonUtility.ToJson(new RoleAutoUpgradeRequest
        {
            playerNpcId = player.playerNpcId,
            expectedCurrentLevel = currentLevel,
            targetLevel = targetLevel,
            planFingerprint = planFingerprint
        });
        if (!ApiRequestUtility.TryCreateJsonRequest(
            "/npc/auto-upgrade",
            UnityWebRequest.kHttpVerbPOST,
            json,
            true,
            out UnityWebRequest request,
            out string error))
        {
            roleUpgradeRunning = false;
            roleStatusMessage = error;
            RenderRolePanel();
            yield break;
        }

        ApiRequestUtility.SetIdempotencyKey(request, roleAutoUpgradeIdempotencyKey);
        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                roleUpgradeRunning = false;
                yield break;
            }

            RoleAutoUpgradeApiResponse response = ParseJson<RoleAutoUpgradeApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                bool definitiveRejection = response != null
                    && response.code != 200
                    && request.responseCode > 0
                    && request.responseCode < 500;
                roleUpgradeRunning = false;
                roleStatusMessage = "角色一键升级失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response));
                if (definitiveRejection)
                {
                    roleAutoUpgradePendingKeys.Remove(requestSignature);
                    roleAutoUpgradePreview = null;
                    roleAutoUpgradePreviewNpcId = 0L;
                    roleAutoUpgradePreviewError = string.Empty;
                    roleAutoUpgradeIdempotencyKey = string.Empty;
                    roleAutoUpgradeRequestSignature = string.Empty;
                }
                RenderRolePanel();
                yield break;
            }

            roleAutoUpgradePendingKeys.Remove(requestSignature);
            roleStatusMessage = $"角色已升级到等级 {response.data.reachedLevel}，服务器已同步资源";
        }

        roleUpgradeRunning = false;
        ResetRoleAutoUpgradePreview();
        yield return RefreshGeneralInventoryAfterRoleMutation();
        yield return LoadRolePanelData();
    }

    private IEnumerator GetRoleAcquisitionGuide(int npcId)
    {
        ClearPendingRoleAcquisitionRoute();
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
            ClearPendingRoleAcquisitionRoute();
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
        ClearPendingRoleAcquisitionRoute();
        if (guide == null)
        {
            roleStatusMessage = "获取途径不可用。";
            RenderRolePanel();
            return;
        }

        if (!string.IsNullOrEmpty(guide.status)
            && !string.Equals(guide.status, "AVAILABLE", StringComparison.OrdinalIgnoreCase))
        {
            roleStatusMessage = string.IsNullOrEmpty(guide.message)
                ? "该获取途径已失效。"
                : guide.message;
            RenderRolePanel();
            return;
        }

        string routeKey = guide.routeKey ?? string.Empty;
        switch (routeKey.ToUpperInvariant())
        {
            case "CARD_DRAW":
                if (!TryValidateCardDrawRoute(guide))
                {
                    roleStatusMessage = "抽奖入口参数无效";
                    RenderRolePanel();
                    break;
                }
                SavePendingRoleAcquisitionRoute("CARD_DRAW", guide.routeParams, guide.instructionExpiresAt);
                ClearPendingRoleAcquisitionRouteStorage();
                HideRolePanel();
                ShowLotteryContent(guide.routeParams.poolCode, guide.routeParams.highlightNpcId);
                break;
            case "PLAYER_TASK":
                if (!TryValidatePlayerTaskRoute(guide))
                {
                    roleStatusMessage = "任务入口参数无效";
                    RenderRolePanel();
                    break;
                }
                SavePendingRoleAcquisitionRoute("PLAYER_TASK", guide.routeParams, guide.instructionExpiresAt);
                ClearPendingRoleAcquisitionRouteStorage();
                HideRolePanel();
                OpenServerTasks("daily", guide.routeParams.taskCode);
                break;
            case "SIGN_IN":
                if (!TryValidateSignInRoute(guide, out string normalizedPlanCode))
                {
                    roleStatusMessage = "签到入口参数无效";
                    RenderRolePanel();
                    break;
                }
                SavePendingRoleAcquisitionRoute("SIGN_IN", guide.routeParams, guide.instructionExpiresAt);
                ClearPendingRoleAcquisitionRouteStorage();
                HideRolePanel();
                OpenServerSignIn(normalizedPlanCode);
                break;
            case "MAIL":
                if (!TryValidateMailRoute(guide))
                {
                    roleStatusMessage = "邮件入口参数无效";
                    RenderRolePanel();
                    break;
                }
                SavePendingRoleAcquisitionRoute("MAIL", guide.routeParams, guide.instructionExpiresAt);
                ClearPendingRoleAcquisitionRouteStorage();
                HideRolePanel();
                ShowMailContent(guide.routeParams.mailId);
                break;
            case "IDLE_ADVENTURE":
                if (!TryValidateIdleAdventureRoute(guide, out string normalizedModeCode))
                {
                    roleStatusMessage = "挂机入口参数无效";
                    RenderRolePanel();
                    break;
                }
                guide.routeParams.modeCode = normalizedModeCode;
                SavePendingRoleAcquisitionRoute("IDLE_ADVENTURE", guide.routeParams, guide.instructionExpiresAt);
                HideRolePanel();
                SceneManager.LoadScene(IdleAdventureSceneName);
                break;
            default:
                roleStatusMessage = string.IsNullOrEmpty(guide.message) ? "暂无可用获取入口" : guide.message;
                RenderRolePanel();
                break;
        }
    }

    private bool TryValidateCardDrawRoute(RoleAcquisitionGuideData guide)
    {
        RoleAcquisitionRouteParams route = guide == null ? null : guide.routeParams;
        if (route == null
            || !IsValidRoleRouteToken(route.poolCode)
            || route.highlightNpcId <= 0)
        {
            return false;
        }

        return guide.npcId <= 0 || route.highlightNpcId == guide.npcId;
    }

    private bool TryValidatePlayerTaskRoute(RoleAcquisitionGuideData guide)
    {
        return guide != null
            && guide.routeParams != null
            && IsValidRoleRouteToken(guide.routeParams.taskCode);
    }

    private bool TryValidateSignInRoute(RoleAcquisitionGuideData guide, out string planCode)
    {
        planCode = string.Empty;
        if (guide == null || guide.routeParams == null || !IsValidRoleRouteToken(guide.routeParams.planCode))
        {
            return false;
        }

        string normalized = NormalizeRoleRouteToken(guide.routeParams.planCode).ToUpperInvariant();
        if (normalized != "NEWBIE" && normalized != "MONTHLY")
        {
            return false;
        }

        planCode = normalized;
        return true;
    }

    private bool TryValidateMailRoute(RoleAcquisitionGuideData guide)
    {
        return guide != null
            && guide.routeParams != null
            && guide.routeParams.mailId > 0L;
    }

    private bool TryValidateIdleAdventureRoute(RoleAcquisitionGuideData guide, out string modeCode)
    {
        modeCode = string.Empty;
        RoleAcquisitionRouteParams route = guide == null ? null : guide.routeParams;
        if (route == null
            || !IsValidRoleRouteToken(route.modeCode)
            || !IsValidRoleRouteToken(route.poolKey)
            || route.stage < 1
            || route.stage > 10000)
        {
            return false;
        }

        string normalized = NormalizeRoleRouteToken(route.modeCode).ToUpperInvariant();
        if (normalized != "IDLE_ADVENTURE"
            && normalized != "IDLE_ADVENTURE_MODE"
            && normalized != "BATTLE")
        {
            return false;
        }

        modeCode = normalized;
        return true;
    }

    private static string NormalizeRoleRouteToken(string value)
    {
        return (value ?? string.Empty).Trim();
    }

    private static bool IsValidRoleRouteToken(string value)
    {
        string token = NormalizeRoleRouteToken(value);
        if (string.IsNullOrEmpty(token) || token.Length > MaxRoleAcquisitionRouteTokenLength)
        {
            return false;
        }

        for (int i = 0; i < token.Length; i++)
        {
            if (char.IsControl(token[i]))
            {
                return false;
            }
        }

        return true;
    }

    private void SavePendingRoleAcquisitionRoute(
        string routeKey,
        RoleAcquisitionRouteParams route,
        string instructionExpiresAt)
    {
        ClearPendingRoleAcquisitionRouteStorage();
        if (route == null || string.IsNullOrEmpty(routeKey))
        {
            return;
        }

        PlayerPrefs.SetString(PendingRoleAcquisitionRouteKey, routeKey);
        PlayerPrefs.SetInt(PendingRoleAcquisitionRoutePlayerIdKey, GameSession.PlayerId);
        PlayerPrefs.SetString(PendingRoleAcquisitionRouteExpiresAtKey, instructionExpiresAt ?? string.Empty);
        PlayerPrefs.SetString(PendingRoleAcquisitionRoutePoolCodeKey, NormalizeRoleRouteToken(route.poolCode));
        PlayerPrefs.SetInt(PendingRoleAcquisitionRouteHighlightNpcIdKey, Mathf.Max(0, route.highlightNpcId));
        PlayerPrefs.SetString(PendingRoleAcquisitionRouteTaskCodeKey, NormalizeRoleRouteToken(route.taskCode));
        PlayerPrefs.SetString(PendingRoleAcquisitionRoutePlanCodeKey, NormalizeRoleRouteToken(route.planCode));
        PlayerPrefs.SetString(PendingRoleAcquisitionRouteModeCodeKey, NormalizeRoleRouteToken(route.modeCode));
        PlayerPrefs.SetInt(PendingRoleAcquisitionRouteStageKey, Mathf.Max(0, route.stage));
        PlayerPrefs.SetString(PendingRoleAcquisitionRoutePoolKeyKey, NormalizeRoleRouteToken(route.poolKey));
        PlayerPrefs.SetString(
            PendingRoleAcquisitionRouteMailIdKey,
            route.mailId > 0L ? route.mailId.ToString(CultureInfo.InvariantCulture) : string.Empty);
        PlayerPrefs.Save();
    }

    private void ClearPendingRoleAcquisitionRoute()
    {
        ClearPendingRoleAcquisitionRouteStorage();
        pendingCardDrawPoolCode = string.Empty;
        pendingCardDrawHighlightNpcId = 0;
        cardDrawRoutePoolMismatch = false;
        pendingServerTaskCode = string.Empty;
        pendingMailRouteId = 0L;
    }

    private static void ClearPendingRoleAcquisitionRouteStorage()
    {
        PlayerPrefs.DeleteKey(PendingRoleAcquisitionRouteKey);
        PlayerPrefs.DeleteKey(PendingRoleAcquisitionRoutePlayerIdKey);
        PlayerPrefs.DeleteKey(PendingRoleAcquisitionRouteExpiresAtKey);
        PlayerPrefs.DeleteKey(PendingRoleAcquisitionRoutePoolCodeKey);
        PlayerPrefs.DeleteKey(PendingRoleAcquisitionRouteHighlightNpcIdKey);
        PlayerPrefs.DeleteKey(PendingRoleAcquisitionRouteTaskCodeKey);
        PlayerPrefs.DeleteKey(PendingRoleAcquisitionRoutePlanCodeKey);
        PlayerPrefs.DeleteKey(PendingRoleAcquisitionRouteModeCodeKey);
        PlayerPrefs.DeleteKey(PendingRoleAcquisitionRouteStageKey);
        PlayerPrefs.DeleteKey(PendingRoleAcquisitionRoutePoolKeyKey);
        PlayerPrefs.DeleteKey(PendingRoleAcquisitionRouteMailIdKey);
        PlayerPrefs.Save();
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
            fallback.text = FrontendTextLocalization.Normalize(string.IsNullOrEmpty(roleName) ? "角" : roleName.Substring(0, 1));
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
    private sealed class RoleAutoUpgradePreviewApiResponse : BasicApiResponse
    {
        public RoleAutoUpgradePreviewData data;
    }

    [Serializable]
    private sealed class RoleAutoUpgradePreviewData
    {
        public long playerNpcId;
        public int currentLevel;
        public int requestedTargetLevel;
        public int payableTargetLevel;
        public string planFingerprint;
    }

    [Serializable]
    private sealed class RoleAutoUpgradeRequest
    {
        public long playerNpcId;
        public int expectedCurrentLevel;
        public int targetLevel;
        public string planFingerprint;
    }

    [Serializable]
    private sealed class RoleAutoUpgradeApiResponse : BasicApiResponse
    {
        public RoleAutoUpgradeData data;
    }

    [Serializable]
    private sealed class RoleAutoUpgradeData
    {
        public long playerNpcId;
        public int reachedLevel;
        public int level;
        public string outcome;
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
