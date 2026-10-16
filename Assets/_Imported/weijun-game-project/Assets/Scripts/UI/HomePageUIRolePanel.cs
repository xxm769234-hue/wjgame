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
    [SerializeField] private Sprite roleQualityWhiteBackground;
    [SerializeField] private Sprite roleQualityPurpleBackground;
    [SerializeField] private Sprite roleQualityRedBackground;
    [SerializeField] private Sprite roleQualityGreenBackground;
    [SerializeField] private Sprite roleQualityBlueBackground;
    [SerializeField] private Sprite roleQualityGoldBackground;
    [Header("Role sprites")]
    [Tooltip("Role list avatars are assigned in the scene from Assets/_Project/Art/UI/角色/头像.")]
    [SerializeField] private RoleSpriteBinding[] roleIconBindings = new RoleSpriteBinding[0];
    [Tooltip("Role detail models are assigned in the scene from Assets/_Project/Art/UI/角色/Q版.")]
    [SerializeField] private RoleSpriteBinding[] roleModelBindings = new RoleSpriteBinding[0];
    private RoleCatalogEntry[] roleCatalogEntries = new RoleCatalogEntry[0];
    private RolePlayerEntry[] rolePlayerEntries = new RolePlayerEntry[0];
    private readonly HashSet<string> roleAcquisitionEventIds = new HashSet<string>();
    private bool rolePanelLoading;
    private bool roleUpgradeRunning;
    private bool roleCatalogMode;
    private int roleSelectedNpcId;
    private int roleRequestVersion;
    private int roleSessionPlayerId;
    private string roleStatusMessage = string.Empty;
    private RoleAutoUpgradePreviewData roleAutoUpgradePreview;
    private long roleAutoUpgradePreviewNpcId;
    private bool roleAutoUpgradePreviewRunning;
    private string roleAutoUpgradePreviewError = string.Empty;
    private int roleAutoUpgradePreviewRequestVersion;
    private RoleLoadoutData roleLoadoutData;
    private long roleLoadoutNpcId;
    private bool roleLoadoutLoading;
    private string roleLoadoutError = string.Empty;
    private int roleLoadoutRequestVersion;
    private RoleCombatProfile[] roleCombatProfiles;
    private bool roleCombatProfilesLoading;
    private string roleCombatProfilesError = string.Empty;
    private int roleCombatProfilesRequestVersion;
    private RectTransform roleListContentRect;
    private float roleListSceneContentHeight;
    private Vector2 roleListSceneContentSizeDelta;
    private Vector2 roleListSceneContentPosition;
    private ScrollRect roleListScrollRect;
    private bool roleListSceneScrollEnabled;
    private bool roleListSceneScrollVertical;
    private int roleDescriptionNpcId;
    private int roleActionPanelNpcId;
    private TMP_Text roleDetailQualityText;
    private Color roleDetailQualitySceneColor;
    private RoleBreakthroughPreviewData roleBreakthroughPreview;
    private long roleBreakthroughRequiredNpcId;
    private long roleBreakthroughPreviewNpcId;
    private int roleBreakthroughPreviewVersion;
    private bool roleBreakthroughPreviewLoading;
    private string roleBreakthroughPreviewError = string.Empty;
    private readonly List<Transform> roleBreakthroughGeneratedSlots = new List<Transform>();
    private Transform roleBreakthroughTemplate;
    private RoleGrowthRequest rolePendingGrowthRequest;
    private RoleGrowthItem[] roleGrowthItems;
    private int roleGrowthItemsRequestVersion;
    private InventoryPanelView roleGrowthIconSource;

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
            SetLeftContent("\u89d2\u8272\u4e2d\u5fc3", "\u89d2\u8272\u9762\u677f\u573a\u666f\u8282\u70b9\u4e0d\u5b58\u5728\uff0c\u8bf7\u5728 Unity \u4e2d\u6267\u884c\u91cd\u5efa\u4e3b\u573a\u666f\u9762\u677f", "\u89d2\u8272\u9762\u677f\u672a\u627e\u5230");
            return;
        }

        rolePanelObject = panel.gameObject;
        CloseRoleActionPanels(panel.PanelRoot);
        int currentPlayerId = GameSession.PlayerId;
        if (roleSessionPlayerId != currentPlayerId)
        {
            roleAcquisitionEventIds.Clear();
            roleSessionPlayerId = currentPlayerId;
        }
        if (rolePendingGrowthRequest != null && rolePendingGrowthRequest.playerId != currentPlayerId)
        {
            rolePendingGrowthRequest = null;
        }

        roleRequestVersion++;
        roleCatalogMode = false;
        roleSelectedNpcId = 0;
        roleDescriptionNpcId = 0;
        roleCatalogEntries = new RoleCatalogEntry[0];
        rolePlayerEntries = new RolePlayerEntry[0];
        roleGrowthItems = null;
        rolePanelLoading = true;
        ResetRoleAutoUpgradePreview();
        ResetRoleBreakthroughPreview();
        ResetRoleLoadoutState();
        roleStatusMessage = HasAuthSession()
            ? "\u6b63\u5728\u540c\u6b65\u670d\u52a1\u5668\u89d2\u8272\u6570\u636e..."
            : "\u8bf7\u5148\u767b\u5f55\u8d26\u53f7\u540e\u67e5\u770b\u89d2\u8272\u6570\u636e";
        RenderRolePanel();

        if (HasAuthSession())
        {
            StartCoroutine(LoadRolePanelData(roleRequestVersion, currentPlayerId));
        }
    }

    private bool IsRoleRequestCurrent(int requestVersion, int playerId, string token = null)
    {
        return requestVersion == roleRequestVersion
            && rolePanelObject != null
            && rolePanelObject.activeInHierarchy
            && HasAuthSession()
            && GameSession.PlayerId == playerId
            && (token == null || GameSession.Token == token);
    }

    private IEnumerator LoadRolePanelData(int requestVersion, int playerId, Action onLoaded = null)
    {
        if (!IsRoleRequestCurrent(requestVersion, playerId))
        {
            yield break;
        }

        rolePanelLoading = true;
        RolePanelLoadState state = new RolePanelLoadState { token = GameSession.Token };
        ResetRoleLoadoutState();
        ResetRoleCombatProfiles();
        StartCoroutine(LoadRoleCombatProfiles(requestVersion, playerId, state.token, roleCombatProfilesRequestVersion));
        StartCoroutine(LoadRoleCatalogData(requestVersion, playerId, state));
        StartCoroutine(LoadRolePlayerData(requestVersion, playerId, state));
        StartCoroutine(LoadRoleGrowthItems(requestVersion, playerId, () =>
        {
            if (IsRoleRequestCurrent(requestVersion, playerId, state.token) && !rolePanelLoading)
            {
                RefreshRoleDetail();
            }
        }));

        while ((!state.catalogCompleted || !state.playerCompleted)
            && string.IsNullOrEmpty(state.catalogError) && string.IsNullOrEmpty(state.playerError)
            && IsRoleRequestCurrent(requestVersion, playerId, state.token))
        {
            yield return null;
        }

        if (!IsRoleRequestCurrent(requestVersion, playerId, state.token))
        {
            yield break;
        }

        if (!string.IsNullOrEmpty(state.catalogError) || (state.catalogCompleted && state.catalogEntries == null))
        {
            rolePanelLoading = false;
            roleStatusMessage = "角色图鉴读取失败：" + state.catalogError;
            RenderRolePanel();
            yield break;
        }

        if (!string.IsNullOrEmpty(state.playerError) || (state.playerCompleted && state.playerEntries == null))
        {
            rolePanelLoading = false;
            roleStatusMessage = "我的角色读取失败：" + state.playerError;
            RenderRolePanel();
            yield break;
        }

        roleCatalogEntries = state.catalogEntries ?? new RoleCatalogEntry[0];
        rolePlayerEntries = state.playerEntries ?? new RolePlayerEntry[0];

        rolePanelLoading = false;
        if (roleCatalogEntries.Length > 0 && roleSelectedNpcId == 0)
        {
            List<RoleDisplayEntry> ownedEntries = GetRoleDisplayEntries(false);
            if (ownedEntries.Count > 0)
            {
                roleSelectedNpcId = ownedEntries[0].catalog.npcId;
            }
            else
            {
                for (int i = 0; i < roleCatalogEntries.Length; i++)
                {
                    if (roleCatalogEntries[i] != null)
                    {
                        roleSelectedNpcId = roleCatalogEntries[i].npcId;
                        break;
                    }
                }
            }
        }

        roleStatusMessage = $"已同步 {roleCatalogEntries.Length} 个角色配置，拥有 {rolePlayerEntries.Length} 个角色";
        RenderRolePanel();
        onLoaded?.Invoke();
    }

    private IEnumerator LoadRoleCatalogData(int requestVersion, int playerId, RolePanelLoadState state)
    {
        UnityWebRequest request = CreateAuthorizedGet("/npc/catalog");
        if (request == null)
        {
            state.catalogError = requestCreationError;
            state.catalogCompleted = true;
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (!IsRoleRequestCurrent(requestVersion, playerId, state.token))
            {
                yield break;
            }

            if (HandleUnauthorizedResponse(request))
            {
                state.catalogError = "登录状态已失效";
            }
            else
            {
                RoleCatalogApiResponse response = ParseJson<RoleCatalogApiResponse>(request.downloadHandler.text);
                if (request.result != UnityWebRequest.Result.Success || response == null
                    || response.code != 200 || response.data == null)
                {
                    state.catalogError = response == null
                        ? ApiRequestUtility.GetErrorMessage(request, "服务器返回数据格式错误")
                        : GetApiError(response);
                }
                else
                {
                    state.catalogEntries = response.data;
                    for (int index = 0; index < state.catalogEntries.Length; index++)
                    {
                        if (state.catalogEntries[index] != null && state.catalogEntries[index].contractVersion > 1)
                        {
                            state.catalogError = "角色图鉴协议版本过高，请更新客户端";
                            break;
                        }
                    }
                }
            }
        }

        state.catalogCompleted = true;
    }

    private IEnumerator LoadRolePlayerData(int requestVersion, int playerId, RolePanelLoadState state)
    {
        UnityWebRequest request = CreateAuthorizedGet($"/npc/list/{playerId}");
        if (request == null)
        {
            state.playerError = requestCreationError;
            state.playerCompleted = true;
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (!IsRoleRequestCurrent(requestVersion, playerId, state.token))
            {
                yield break;
            }

            if (HandleUnauthorizedResponse(request))
            {
                state.playerError = "登录状态已失效";
            }
            else
            {
                RolePlayerApiResponse response = ParseJson<RolePlayerApiResponse>(request.downloadHandler.text);
                if (request.result != UnityWebRequest.Result.Success || response == null
                    || response.code != 200 || response.data == null)
                {
                    state.playerError = response == null
                        ? ApiRequestUtility.GetErrorMessage(request, "服务器返回数据格式错误")
                        : GetApiError(response);
                }
                else
                {
                    state.playerEntries = response.data;
                }
            }
        }

        state.playerCompleted = true;
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

        EnsureRoleListRows(root, visibleEntries.Count);
        List<Transform> rows = GetRoleListRows(root);
        for (int i = 0; i < rows.Count; i++)
        {
            Transform row = rows[i];
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

    private void RefreshRoleDetail()
    {
        if (rolePanelObject == null || !rolePanelObject.activeInHierarchy)
        {
            return;
        }

        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Role);
        if (panel != null && panel.PanelRoot != null)
        {
            ConfigureRoleDetail(panel.PanelRoot, FindRoleDisplayEntry(roleSelectedNpcId));
        }
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
        if (image == null)
        {
            return;
        }

        button.transition = Selectable.Transition.None;
        SpriteState spriteState = button.spriteState;
        Sprite targetSprite = selected ? spriteState.selectedSprite : spriteState.disabledSprite;
        if (targetSprite != null)
        {
            image.sprite = targetSprite;
        }

        if (selected)
        {
            button.Select();
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
            bool owned = entry.owned || player != null;
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

    /*
    private void ConfigureRoleListRow(Transform row, RoleDisplayEntry entry)
    {
        SetSceneText(row, "RoleListRowName" + GetRoleRowIndex(row), entry.catalog.name ?? "未命名角色");
        SetSceneText(row, "RoleListRowQuality" + GetRoleRowIndex(row), "品质  " + FormatRoleQuality(entry.catalog.quality));
        SetSceneText(row, "RoleListRowLevel" + GetRoleRowIndex(row), !entry.owned || entry.player == null
            ? "待获得"
            : $"Lv.{Mathf.Max(1, entry.player.level)}");
        SetSceneText(row, "RoleListRowStatus" + GetRoleRowIndex(row), GetRoleStatusLabel(entry));

        int rowIndex = GetRoleRowIndex(row);
        SetRoleRowText(row, rowIndex, "RoleListRowName", entry.catalog.name ?? "\u672a\u547d\u540d\u89d2\u8272");
        SetRoleRowText(row, rowIndex, "RoleListRowQuality", "\u54c1\u8d28  " + FormatRoleQualityForDisplay(entry.catalog.quality));
        SetRoleRowText(row, rowIndex, "RoleListRowLevel", !entry.owned || entry.player == null
            ? "\u5f85\u83b7\u5f97"
            : $"Lv.{Mathf.Max(1, entry.player.level)}");

        Transform iconRoot = FindRoleRowChild(row, rowIndex, "RoleListRowIcon");
        Image icon = FindRoleRowAvatarImage(iconRoot);
        TMP_Text fallback = FindRoleRowComponent<TMP_Text>(row, rowIndex, "RoleListRowIconText");
        ApplyRoleIcon(icon, fallback, entry.catalog.name, GetRoleIconKeys(entry));

        Image qualityImage = FindRoleRowComponent<Image>(row, rowIndex, "RoleListRowQuality");
        Sprite qualitySprite = GetRoleQualityBackground(entry.catalog.quality);
        if (qualityImage != null && qualitySprite != null)
        {
            qualityImage.sprite = qualitySprite;
            qualityImage.color = Color.white;
        }

        Transform select = FindSceneTransform(row, "Select");
        if (select != null)
        {
            select.gameObject.SetActive(entry.catalog.npcId == roleSelectedNpcId);
        }

        Transform rowStatus = FindSceneTransform(row, "RowStatus");
        if (rowStatus == null)
        {
            rowStatus = FindRoleRowChild(row, rowIndex, "RoleListRowStatus");
        }
        if (rowStatus != null)
        {
            rowStatus.gameObject.SetActive(!entry.owned);
            SetSceneText(row, rowStatus.name, "\u672a\u83b7\u53d6");
        }

        Button button = row.GetComponent<Button>();
        BindSceneButton(button, () =>
        {
            roleSelectedNpcId = entry.catalog.npcId;
            RenderRolePanel();
        });

    }
    */

    private void ConfigureRoleListRow(Transform row, RoleDisplayEntry entry)
    {
        if (row == null || entry == null || entry.catalog == null)
        {
            return;
        }

        int rowIndex = GetRoleRowIndex(row);
        SetRoleRowText(row, rowIndex, "RoleListRowName", entry.catalog.name ?? "\u672a\u547d\u540d\u89d2\u8272");
        SetRoleRowText(row, rowIndex, "RoleListRowLevel", !entry.owned || entry.player == null
            ? "\u5f85\u83b7\u5f97"
            : $"Lv.{Mathf.Max(1, entry.player.level)}");
        SetRoleRowText(row, rowIndex, "RoleListRowStatus", GetRoleStatusLabelForDisplay(entry));

        Transform iconRoot = FindRoleRowChild(row, rowIndex, "RoleListRowIcon");
        Image icon = FindRoleRowAvatarImage(iconRoot);
        TMP_Text fallback = FindRoleRowComponent<TMP_Text>(row, rowIndex, "RoleListRowIconText");
        ApplyRoleIcon(icon, fallback, entry.catalog.name, GetRoleIconKeys(entry));

        Image qualityImage = FindRoleRowComponent<Image>(row, rowIndex, "RoleListRowQuality");
        Sprite qualitySprite = GetRoleQualityBackground(entry.catalog.quality);
        if (qualityImage != null && qualitySprite != null)
        {
            qualityImage.sprite = qualitySprite;
        }

        Transform select = FindSceneTransform(row, "Select");
        if (select != null)
        {
            select.gameObject.SetActive(entry.catalog.npcId == roleSelectedNpcId);
        }

        Transform rowStatus = FindSceneTransform(row, "RowStatus");
        if (rowStatus == null)
        {
            rowStatus = FindRoleRowChild(row, rowIndex, "RoleListRowStatus");
        }
        if (rowStatus != null)
        {
            rowStatus.gameObject.SetActive(!entry.owned);
        }

        BindSceneButton(row.GetComponent<Button>(), () =>
        {
            roleSelectedNpcId = entry.catalog.npcId;
            RenderRolePanel();
        });
    }

    private static Transform FindRoleRowChild(Transform row, int rowIndex, string childPrefix)
    {
        if (row == null || string.IsNullOrEmpty(childPrefix))
        {
            return null;
        }

        Transform child = FindSceneTransform(row, childPrefix + rowIndex);
        if (child != null)
        {
            return child;
        }

        child = FindSceneTransform(row, childPrefix + "0");
        if (child != null)
        {
            return child;
        }

        return FindSceneTransform(row, childPrefix);
    }

    private static T FindRoleRowComponent<T>(Transform row, int rowIndex, string childPrefix) where T : Component
    {
        Transform child = FindRoleRowChild(row, rowIndex, childPrefix);
        return child == null ? null : child.GetComponent<T>();
    }

    private static Image FindRoleRowAvatarImage(Transform iconRoot)
    {
        if (iconRoot == null)
        {
            return null;
        }

        Transform modelMask = FindSceneTransform(iconRoot, "ModelMask");
        Transform model = modelMask == null ? null : FindSceneTransform(modelMask, "model");
        Image avatar = model == null ? null : model.GetComponent<Image>();
        if (avatar != null)
        {
            return avatar;
        }

        avatar = FindSceneComponent<Image>(iconRoot, "HeadImage");
        if (avatar != null)
        {
            return avatar;
        }

        return FindSceneComponent<Image>(iconRoot, "Image");
    }

    private static void SetRoleRowText(Transform row, int rowIndex, string childPrefix, string value)
    {
        Transform child = FindRoleRowChild(row, rowIndex, childPrefix);
        if (child != null)
        {
            SetSceneText(row, child.name, value);
        }
    }

    private static int GetRoleRowIndex(Transform row)
    {
        string value = row == null ? string.Empty : row.name.Replace("RoleListRow", string.Empty);
        return int.TryParse(value, out int index) ? index : 0;
    }

    /*
    private void ConfigureRoleDetail(Transform root, RoleDisplayEntry entry)
    {
        Button upgrade = FindSceneComponent<Button>(root, "RoleDetailUpgradeButton");
        Button autoUpgrade = EnsureRoleAutoUpgradeButton(root, upgrade);
        Button acquire = FindSceneComponent<Button>(root, "RoleDetailAcquireButton");
        if (entry == null)
        {
            SetSceneText(root, "RoleDetailName", "暂无角色数据");
            SetSceneText(root, "RoleDetailQuality", string.Empty);
            SetSceneText(root, "RoleDetailStatus", rolePanelLoading ? "正在加载..." : "暂无可显示角色");
            SetSceneText(root, "RoleDetailLevel", string.Empty);
            SetSceneText(root, "RoleDetailAttribute", string.Empty);
            SetSceneText(root, "RoleDetailBattleStats", string.Empty);
            SetSceneText(root, "RoleDetailEquipment", string.Empty);
            SetSceneText(root, "RoleDetailProduction", string.Empty);
            SetSceneText(root, "RoleDetailDescription", string.Empty);
            SetSceneText(root, "RoleDetailAcquisition", string.Empty);
            SetSceneText(root, "RoleDetailUpgradePreview", "所需资源");
            if (upgrade != null) upgrade.gameObject.SetActive(false);
            if (autoUpgrade != null) autoUpgrade.gameObject.SetActive(false);
            if (acquire != null) acquire.gameObject.SetActive(false);
            return;
        }

        SetSceneText(root, "RoleDetailName", entry.catalog.name ?? "未命名角色");
        SetSceneText(root, "RoleDetailQuality", "品质  " + FormatRoleQuality(entry.catalog.quality));
        SetSceneText(root, "RoleDetailStatus", "状态  " + GetRoleStatusLabel(entry));
        SetSceneText(root, "RoleDetailLevel", !entry.owned || entry.player == null
            ? "等级  待获得"
            : $"等级  Lv.{Mathf.Max(1, entry.player.level)} / {Mathf.Max(1, entry.catalog.maxLevel)}");
        SetSceneText(root, "RoleDetailAttribute", "战斗属性  " + (string.IsNullOrEmpty(entry.catalog.combatAttribute) ? "暂无" : entry.catalog.combatAttribute));
        EnsureRoleLoadout(entry);
        SetSceneText(root, "RoleDetailQuality", "\u54c1\u8d28  " + FormatRoleQualityForDisplay(entry.catalog.quality));
        SetSceneText(root, "RoleDetailStatus", "\u72b6\u6001  " + GetRoleStatusLabelForDisplay(entry));
        SetSceneText(root, "RoleDetailBattleStats", BuildRoleBattleStatsText(entry));
        SetSceneText(root, "RoleDetailEquipment", BuildRoleEquipmentText(entry));
        SetSceneText(root, "RoleDetailProduction", $"产出加成  {entry.catalog.produceBonusRate * 100f:0.##}%");
        SetSceneText(root, "RoleDetailDescription", string.IsNullOrEmpty(entry.catalog.description) ? "暂无角色描述" : entry.catalog.description);
        SetSceneText(root, "RoleDetailAcquisition", BuildRoleAcquisitionText(entry));

        EnsureRoleAutoUpgradePreview(entry);
        SetSceneText(root, "RoleDetailUpgradePreview", "所需资源");

        TMP_Text fallback = FindSceneComponent<TMP_Text>(root, "RoleDetailIconText");
        Image npcModel = FindSceneComponent<Image>(root, "NPCModel");
        ApplyRoleIcon(npcModel, fallback, entry.catalog.name, GetRoleModelKeys(entry), true);

        if (upgrade != null)
        {
            upgrade.gameObject.SetActive(entry.owned && entry.player != null);
            bool previewMatches = entry.owned && entry.player != null && roleAutoUpgradePreviewNpcId == entry.player.playerNpcId;
            bool previewLoading = previewMatches && roleAutoUpgradePreviewRunning;
            bool maxLevelReached = entry.player != null
                && entry.catalog.maxLevel > 0
                && entry.player.level >= entry.catalog.maxLevel;
            if (previewMatches && roleAutoUpgradePreview != null)
            {
                maxLevelReached = maxLevelReached
                    || roleAutoUpgradePreview.payableTargetLevel <= roleAutoUpgradePreview.currentLevel;
            }
            bool useAutoUpgrade = previewMatches && roleAutoUpgradePreview != null;
            upgrade.interactable = entry.owned && entry.player != null && !roleUpgradeRunning && !previewLoading && !maxLevelReached;
            SetSceneButtonText(upgrade, !entry.owned || entry.player == null || maxLevelReached
                ? "已达上限"
                : previewLoading
                    ? "读取中..."
                    : useAutoUpgrade
                        ? $"一键升至 Lv.{roleAutoUpgradePreview.payableTargetLevel}"
                        : "升级");
            Action upgradeAction = null;
            if (entry.owned && entry.player != null && !roleUpgradeRunning && !previewLoading)
            {
                upgradeAction = useAutoUpgrade
                    ? (Action)(() => StartCoroutine(AutoUpgradeRole(entry.player, entry.catalog.maxLevel)))
                    : () => StartCoroutine(UpgradeRole(entry.player.playerNpcId));
            }
            BindSceneButton(upgrade, upgradeAction);
        }

        ConfigureNormalRoleUpgradeButton(upgrade, entry);
        ConfigureAutoRoleUpgradeButton(autoUpgrade, entry);

        if (acquire != null)
        {
            bool canAcquire = !entry.owned && entry.catalog.acquisition != null
                && string.Equals(entry.catalog.acquisition.status, "AVAILABLE", StringComparison.OrdinalIgnoreCase);
            acquire.gameObject.SetActive(!entry.owned);
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
    */

    private void ConfigureRoleDetail(Transform root, RoleDisplayEntry entry)
    {
        ConfigureRoleActionPanels(root, entry);
        SetSceneText(root, "RoleProperty", "\u5c5e\u6027\u7c7b\u578b  "
            + (entry == null ? "--" : FormatRoleCombatAttributeForDisplay(entry)));
        Button upgrade = FindSceneComponent<Button>(root, "RoleDetailUpgradeButton");
        Button acquire = FindSceneComponent<Button>(root, "RoleDetailAcquireButton");
        Button breakthrough = FindSceneComponent<Button>(root, "BreakthroughButton");
        Button autoUpgrade = FindSceneComponent<Button>(root, "RoleDetailOneClickUpgradeButton")
            ?? FindSceneComponent<Button>(root, "RoleDetailAutoUpgradeButton");
        bool owned = entry != null && entry.owned && entry.player != null;
        SetSceneObjectActive(root, "RoleDetailAcquisition", entry != null && !owned);
        SetSceneObjectActive(root, "RoleDetailEquipment", owned);
        EnsureRoleAutoUpgradePreview(entry);
        ConfigureRoleBreakthroughVisibility(root, entry);

        if (entry == null)
        {
            ConfigureRoleUpgradeResourceDisplay(root, null);
            SetSceneText(root, "RoleDetailName", "\u6682\u65e0\u89d2\u8272\u6570\u636e");
            ConfigureRoleDetailQuality(root, null);
            SetSceneText(root, "RoleDetailStatus", rolePanelLoading ? "\u6b63\u5728\u52a0\u8f7d..." : "\u6682\u65e0\u53ef\u663e\u793a\u89d2\u8272");
            SetSceneText(root, "RoleDetailLevel", string.Empty);
            SetSceneText(root, "RoleDetailAttribute", string.Empty);
            ConfigureRoleBattleStats(root, null);
            ConfigureRoleEquipmentDisplay(root, null);
            SetSceneText(root, "RoleDetailProduction", string.Empty);
            ConfigureRoleDescription(root, null);
            SetSceneText(root, "RoleDetailAcquisition", string.Empty);
            SetSceneText(root, "RoleDetailUpgradePreview", "所需资源");
            if (upgrade != null) upgrade.gameObject.SetActive(false);
            if (autoUpgrade != null) autoUpgrade.gameObject.SetActive(false);
            if (acquire != null && acquire != autoUpgrade) acquire.gameObject.SetActive(false);
            ConfigureRoleBreakthrough(root, entry, breakthrough);
            Image emptyModel = FindSceneComponent<Image>(root, "NPCModel");
            if (emptyModel != null) emptyModel.enabled = false;
            return;
        }

        SetSceneText(root, "RoleDetailName", entry.catalog.name ?? "\u672a\u547d\u540d\u89d2\u8272");
        ConfigureRoleDetailQuality(root, entry);
        SetSceneText(root, "RoleDetailStatus", "\u72b6\u6001  " + GetRoleStatusLabelForDisplay(entry));
        SetSceneText(root, "RoleDetailLevel", !entry.owned || entry.player == null
            ? "\u7b49\u7ea7  \u5f85\u83b7\u5f97"
            : $"\u7b49\u7ea7  Lv.{Mathf.Max(1, entry.player.level)} / {Mathf.Max(1, entry.catalog.maxLevel)}");
        SetSceneText(root, "RoleDetailAttribute", "\u5c5e\u6027\u7c7b\u578b  " + FormatRoleCombatAttributeForDisplay(entry));
        EnsureRoleLoadout(entry);
        ConfigureRoleBattleStats(root, entry);
        ConfigureRoleEquipmentDisplay(root, entry);
        SetSceneText(root, "RoleDetailProduction", $"\u4ea7\u51fa\u52a0\u6210  {entry.catalog.produceBonusRate * 100f:0.##}%");
        ConfigureRoleDescription(root, entry);
        SetSceneText(root, "RoleDetailAcquisition", BuildRoleAcquisitionText(entry));

        SetSceneText(root, "RoleDetailUpgradePreview", "所需资源");

        ConfigureRoleUpgradeResourceDisplay(root, entry);

        TMP_Text fallback = FindSceneComponent<TMP_Text>(root, "RoleDetailIconText");
        Image npcModel = FindSceneComponent<Image>(root, "NPCModel");
        ApplyRoleIcon(npcModel, fallback, entry.catalog.name, GetRoleModelKeys(entry), true);

        ConfigureNormalRoleUpgradeButton(upgrade, entry);
        if (acquire != null && acquire != autoUpgrade)
        {
            ConfigureRoleAcquisitionButton(acquire, entry);
        }

        if (autoUpgrade != null)
        {
            if (entry.owned && entry.player != null)
            {
                ConfigureAutoRoleUpgradeButton(autoUpgrade, entry);
            }
            else if (autoUpgrade == acquire)
            {
                ConfigureRoleAcquisitionButton(autoUpgrade, entry);
            }
            else
            {
                autoUpgrade.gameObject.SetActive(false);
            }
        }
        ConfigureRoleBreakthrough(root, entry, breakthrough);
    }

    private void ConfigureRoleActionPanels(Transform root, RoleDisplayEntry entry)
    {
        int npcId = entry == null ? 0 : entry.catalog.npcId;
        bool owned = entry != null && entry.owned && entry.player != null;
        if (roleActionPanelNpcId != npcId || !owned)
        {
            CloseRoleActionPanels(root);
            roleActionPanelNpcId = npcId;
        }

        Button equipment = FindSceneComponent<Button>(root, "RoleViewEquipmentButton");
        Button growth = FindSceneComponent<Button>(root, "RoleOpenGrowthButton");
        if (equipment != null)
        {
            equipment.gameObject.SetActive(entry != null);
            equipment.interactable = owned;
            BindSceneButton(equipment, owned ? (Action)(() => OpenRoleActionPanel(root, false)) : null);
        }
        if (growth != null)
        {
            growth.gameObject.SetActive(owned);
            growth.interactable = owned && !rolePanelLoading;
            SetSceneButtonText(growth, IsRoleAtBreakthrough(entry) ? "突破" : "升级");
            BindSceneButton(growth, growth.interactable ? (Action)(() => OpenRoleActionPanel(root, true)) : null);
        }

        BindSceneButton(FindSceneComponent<Button>(root, "RoleEquipmentCloseButton"), () => CloseRoleActionPanels(root));
        BindSceneButton(FindSceneComponent<Button>(root, "RoleGrowthCloseButton"), () => CloseRoleActionPanels(root));
        string name = entry == null ? string.Empty : entry.catalog.name ?? "角色";
        SetSceneText(root, "RoleEquipmentTitle", name + " · 装备");
        SetSceneText(root, "RoleGrowthTitle", name + (IsRoleAtBreakthrough(entry) ? " · 突破" : " · 升级"));
        string growthStatus = roleStatusMessage;
        if (!roleUpgradeRunning && !rolePanelLoading)
        {
            if (roleAutoUpgradePreviewRunning || roleBreakthroughPreviewLoading) growthStatus = "正在读取升级资源...";
            else if (!string.IsNullOrEmpty(roleBreakthroughPreviewError)) growthStatus = roleBreakthroughPreviewError;
            else if (!string.IsNullOrEmpty(roleAutoUpgradePreviewError)) growthStatus = roleAutoUpgradePreviewError;
        }
        SetSceneText(root, "RoleGrowthStatus", growthStatus);
        SetSceneText(root, "RoleEquipmentStatus", roleLoadoutLoading ? "正在读取已穿戴装备..."
            : string.IsNullOrEmpty(roleLoadoutError) ? string.Empty : "装备读取失败：" + roleLoadoutError);
    }

    private void OpenRoleActionPanel(Transform root, bool growth)
    {
        RoleDisplayEntry entry = FindRoleDisplayEntry(roleSelectedNpcId);
        if (entry == null || !entry.owned || entry.player == null) return;
        CloseRoleActionPanels(root);
        SetSceneObjectActive(root, growth ? "RoleGrowthOverlay" : "RoleEquipmentOverlay", true);
        ConfigureRoleDetail(root, entry);
    }

    private void CloseRoleActionPanels(Transform root)
    {
        SetSceneObjectActive(root, "RoleEquipmentOverlay", false);
        SetSceneObjectActive(root, "RoleGrowthOverlay", false);
    }

    private Button EnsureRoleAutoUpgradeButton(Transform root, Button upgrade)
    {
        return root == null ? null : FindSceneComponent<Button>(root, "RoleDetailAutoUpgradeButton");
    }

    private void ConfigureNormalRoleUpgradeButton(Button button, RoleDisplayEntry entry)
    {
        if (button == null)
        {
            return;
        }

        bool owned = entry != null && entry.owned && entry.player != null;
        bool maxLevel = owned && entry.player.level >= GetRoleMaxLevel(entry);
        bool available = owned && !IsRoleAtBreakthrough(entry)
            && (maxLevel || GetCurrentRoleAutoUpgradePreview(entry) != null);
        button.gameObject.SetActive(available);
        button.interactable = available && !maxLevel && !roleUpgradeRunning && !rolePanelLoading && rolePendingGrowthRequest == null;
        SetSceneButtonText(button, maxLevel ? "\u5df2\u8fbe\u4e0a\u9650" : "\u5347\u7ea7");
        BindSceneButton(button, button.interactable
            ? () => StartCoroutine(UpgradeRole(entry.player.playerNpcId))
            : null);
    }

    private void ConfigureRoleAcquisitionButton(Button button, RoleDisplayEntry entry)
    {
        if (button == null)
        {
            return;
        }

        bool canAcquire = entry != null && !entry.owned && entry.catalog.acquisition != null
            && string.Equals(entry.catalog.acquisition.status, "AVAILABLE", StringComparison.OrdinalIgnoreCase);
        button.gameObject.SetActive(entry != null && !entry.owned);
        button.interactable = canAcquire && !rolePanelLoading;
        SetSceneButtonText(button, "\u524d\u5f80\u83b7\u53d6");
        BindSceneButton(button, canAcquire
            ? () => StartCoroutine(GetRoleAcquisitionGuide(entry.catalog.npcId))
            : null);
    }

    private void ConfigureAutoRoleUpgradeButton(Button button, RoleDisplayEntry entry)
    {
        if (button == null)
        {
            return;
        }

        bool available = entry != null && entry.owned && entry.player != null && !IsRoleAtBreakthrough(entry);
        bool previewMatches = available && roleAutoUpgradePreviewNpcId == entry.player.playerNpcId;
        bool previewReady = GetCurrentRoleAutoUpgradePreview(entry) != null;
        bool previewLoading = previewMatches && roleAutoUpgradePreviewRunning;
        bool maxLevel = available && entry.player.level >= GetRoleMaxLevel(entry);
        bool canAdvance = previewReady && roleAutoUpgradePreview.currentLevel == entry.player.level
            && roleAutoUpgradePreview.payableTargetLevel > roleAutoUpgradePreview.currentLevel;
        bool retry = available && IsPendingRoleGrowth(entry.player.playerNpcId, "/npc/auto-upgrade");
        bool previewFailed = previewMatches && !string.IsNullOrEmpty(roleAutoUpgradePreviewError);

        button.gameObject.SetActive(available && (maxLevel || previewReady || retry || previewFailed));
        button.interactable = available && !roleUpgradeRunning && !rolePanelLoading
            && (retry || (rolePendingGrowthRequest == null && !maxLevel && !previewLoading && (canAdvance || previewFailed)));
        SetSceneButtonText(button, retry ? "重试一键升级" : maxLevel ? "已达上限"
            : previewLoading ? "读取中..." : previewFailed ? "重新预览"
            : IsRoleAtBreakthrough(entry) ? "请先突破" : previewReady && !canAdvance ? "资源不足" : "一键升级");
        BindSceneButton(button, !button.interactable ? null : (Action)(() =>
        {
            if (retry) StartCoroutine(SendRoleGrowthRequest(rolePendingGrowthRequest));
            else if (previewFailed) { ResetRoleAutoUpgradePreview(); RenderRolePanel(); }
            else StartCoroutine(AutoUpgradeRole(entry.player, entry.catalog.maxLevel));
        }));
    }

    private IEnumerator UpgradeRole(long playerNpcId)
    {
        if (playerNpcId <= 0 || roleUpgradeRunning || rolePendingGrowthRequest != null || !HasAuthSession())
        {
            yield break;
        }

        int sessionPlayerId = GameSession.PlayerId;
        string sessionToken = GameSession.Token;
        roleUpgradeRunning = true;
        roleGrowthItemsRequestVersion++;
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
            if (!IsRoleGrowthSessionCurrent(sessionPlayerId, sessionToken))
            {
                roleUpgradeRunning = false;
                yield break;
            }
            if (HandleUnauthorizedResponse(request))
            {
                roleUpgradeRunning = false;
                yield break;
            }

            RoleUpgradeApiResponse response = ParseJson<RoleUpgradeApiResponse>(request.downloadHandler.text);
            if (response != null && response.code == 6109 && request.responseCode > 0 && request.responseCode < 500)
            {
                roleUpgradeRunning = false;
                ResetRoleBreakthroughPreview();
                roleBreakthroughRequiredNpcId = playerNpcId;
                roleStatusMessage = "已达到突破门槛，请选择所需资源后确认突破";
                ResetRoleAutoUpgradePreview();
                RenderRolePanel();
                yield break;
            }
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                roleUpgradeRunning = false;
                roleStatusMessage = "角色升级失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response));
                RenderRolePanel();
                yield break;
            }

            if (response.data.playerNpcId != playerNpcId)
            {
                roleStatusMessage = "服务器返回的角色不一致，正在重新同步";
            }
            else roleStatusMessage = $"{response.data.name} 已升级到 Lv.{response.data.level}，资源已由服务器结算";
        }

        yield return RefreshRoleGrowthState(sessionPlayerId, sessionToken);
    }

    private IEnumerator RefreshGeneralInventoryAfterRoleMutation()
    {
        List<string> syncedCategories = new List<string>();
        List<string> failedCategories = new List<string>();
        yield return RefreshCardDrawInventoryState(syncedCategories, failedCategories);
        if (failedCategories.Count > 0)
        {
            roleStatusMessage = "升级成功，但部分资源同步失败："
                + string.Join(", ", failedCategories.ToArray());
        }
    }

    private void ResetRoleAutoUpgradePreview()
    {
        roleAutoUpgradePreviewRequestVersion++;
        roleAutoUpgradePreview = null;
        roleAutoUpgradePreviewNpcId = 0L;
        roleAutoUpgradePreviewRunning = false;
        roleAutoUpgradePreviewError = string.Empty;
    }

    private static int GetRoleMaxLevel(RoleDisplayEntry entry)
    {
        return entry.catalog.maxLevel > 0 ? Math.Min(100, entry.catalog.maxLevel) : 100;
    }

    private IEnumerator LoadRoleGrowthItems(int requestVersion, int playerId, Action onCompleted = null)
    {
        int inventoryRequestVersion = ++roleGrowthItemsRequestVersion;
        roleGrowthItems = null;
        string token = GameSession.Token;
        UnityWebRequest request = CreateAuthorizedGet($"/inventory/list/{playerId}");
        if (request == null)
        {
            onCompleted?.Invoke();
            yield break;
        }
        using (request)
        {
            yield return request.SendWebRequest();
            if (inventoryRequestVersion != roleGrowthItemsRequestVersion
                || !IsRoleRequestCurrent(requestVersion, playerId, token)) yield break;
            if (HandleUnauthorizedResponse(request)) yield break;
            RoleGrowthInventoryResponse response = ParseJson<RoleGrowthInventoryResponse>(request.downloadHandler.text);
            if (request.result == UnityWebRequest.Result.Success && response != null && response.code == 200)
                roleGrowthItems = response.data;
        }

        onCompleted?.Invoke();
    }

    private RoleGrowthItem FindRoleGrowthItem(string itemKey)
    {
        if (roleGrowthItems == null || string.IsNullOrEmpty(itemKey)) return null;
        return Array.Find(roleGrowthItems, item => item != null
            && string.Equals(item.itemKey, itemKey, StringComparison.OrdinalIgnoreCase));
    }

    private RoleAutoUpgradePreviewData GetCurrentRoleAutoUpgradePreview(RoleDisplayEntry entry)
    {
        return entry != null && entry.owned && entry.player != null
            && roleAutoUpgradePreviewNpcId == entry.player.playerNpcId
            && roleAutoUpgradePreview != null && roleAutoUpgradePreview.playerNpcId == entry.player.playerNpcId
            && roleAutoUpgradePreview.currentLevel == entry.player.level
            && !roleAutoUpgradePreviewRunning && string.IsNullOrEmpty(roleAutoUpgradePreviewError)
                ? roleAutoUpgradePreview : null;
    }

    private void ConfigureRoleUpgradeResourceDisplay(Transform root, RoleDisplayEntry entry)
    {
        Transform detail = FindSceneTransform(root, "RoleGrowthCard")
            ?? FindSceneTransform(root, "RoleDetailCard");
        if (detail == null) return;
        RoleAutoUpgradePreviewData preview = GetCurrentRoleAutoUpgradePreview(entry);
        bool visible = preview != null
            && entry.player.level < GetRoleMaxLevel(entry) && !IsRoleAtBreakthrough(entry);
        Transform singleSlot = detail.Find("UpgradeBreak");
        Transform autoSlot = detail.Find("OneClickUpgradeBreak");
        if (!visible)
        {
            if (singleSlot != null) singleSlot.gameObject.SetActive(false);
            if (autoSlot != null) autoSlot.gameObject.SetActive(false);
            bool breakthroughReady = IsRoleAtBreakthrough(entry)
                && !roleBreakthroughPreviewLoading && string.IsNullOrEmpty(roleBreakthroughPreviewError)
                && IsValidRoleBreakthroughPreview(roleBreakthroughPreview, entry.player.playerNpcId, entry.player.level);
            SetSceneObjectActive(root, "RoleDetailUpgradePreview", breakthroughReady);
            return;
        }

        RoleGrowthItem gold = FindRoleGrowthItem("gold");
        long balance = roleGrowthItems == null ? -1 : gold == null ? 0 : gold.quantity;
        RoleAutoUpgradeBlockedRequirementResponse blockedGold = preview == null || preview.blockedRequirements == null
            ? null : Array.Find(preview.blockedRequirements, requirement => requirement != null
                && (string.Equals(requirement.itemKey, "gold", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(requirement.requirementType, "GOLD", StringComparison.OrdinalIgnoreCase)));
        if (blockedGold != null && preview.blockedLevel == preview.currentLevel + 1)
            balance = blockedGold.availableAmount;
        RoleBreakthroughCost singleCost = new RoleBreakthroughCost
        {
            key = "gold", name = "金币", required = GetRolePreviewGoldCost(preview, false), available = balance
        };
        RoleBreakthroughCost autoCost = new RoleBreakthroughCost
        {
            key = "gold", name = "金币", required = GetRolePreviewGoldCost(preview, true), available = balance
        };
        bool singleReady = singleCost.required >= 0 && singleCost.available >= 0;
        bool autoReady = autoCost.required >= 0 && autoCost.available >= 0;
        if (singleReady) ApplyRoleResourceCost(singleSlot, singleCost);
        if (autoReady) ApplyRoleResourceCost(autoSlot, autoCost);
        if (singleSlot != null) singleSlot.gameObject.SetActive(singleReady);
        if (autoSlot != null) autoSlot.gameObject.SetActive(autoReady);
        SetSceneObjectActive(root, "RoleDetailUpgradePreview", singleReady || autoReady);
    }

    private static long GetRolePreviewGoldCost(RoleAutoUpgradePreviewData preview, bool autoUpgrade)
    {
        if (preview == null) return -1;
        int targetLevel = autoUpgrade && preview.payableTargetLevel > preview.currentLevel
            ? preview.payableTargetLevel : preview.currentLevel + 1;
        long total = 0;
        for (int level = preview.currentLevel + 1; level <= targetLevel; level++)
        {
            RoleAutoUpgradeStepResponse step = preview.upgradeSteps == null ? null
                : Array.Find(preview.upgradeSteps, candidate => candidate != null && candidate.targetLevel == level);
            long cost = step == null ? -1 : step.goldCost;
            if (cost < 0 && preview.blockedLevel == level && preview.blockedRequirements != null)
            {
                RoleAutoUpgradeBlockedRequirementResponse requirement = Array.Find(preview.blockedRequirements,
                    candidate => candidate != null && (string.Equals(candidate.itemKey, "gold", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(candidate.requirementType, "GOLD", StringComparison.OrdinalIgnoreCase)));
                if (requirement != null) cost = requirement.requiredAmount;
            }
            if (cost < 0 || cost > long.MaxValue - total) return -1;
            total += cost;
        }
        return total;
    }

    private void ApplyRoleResourceCost(Transform slot, RoleBreakthroughCost cost)
    {
        if (slot == null || cost == null) return;
        RoleGrowthItem item = FindRoleGrowthItem(cost.key);
        if (roleGrowthIconSource == null) roleGrowthIconSource = inventoryPanelView != null
            ? inventoryPanelView : Resources.Load<InventoryPanelView>("UI/InventoryPanel");
        Image background = slot.GetComponent<Image>();
        if (background != null && roleGrowthIconSource != null)
        {
            Sprite qualityFrame = roleGrowthIconSource.ResolveQualityBackground(item == null ? null : item.quality);
            if (qualityFrame != null) background.sprite = qualityFrame;
        }
        Image icon = FindSceneComponent<Image>(slot, "BreakthroughImage")
            ?? FindSceneComponent<Image>(slot, "Icon") ?? FindSceneComponent<Image>(slot, "Image");
        if (icon != null)
        {
            string itemName = item != null && !string.IsNullOrEmpty(item.name) ? item.name : cost.name;
            string iconKey = item == null ? null : item.icon;
            Sprite sprite = roleGrowthIconSource == null ? null : roleGrowthIconSource.ResolveIcon(cost.key, itemName, iconKey);
            if (sprite == null) sprite = LoadMailAttachmentResourceSprite(iconKey)
                ?? LoadMailAttachmentResourceSprite(cost.key) ?? LoadMailAttachmentResourceSprite(itemName);
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }
        TMP_Text required = FindSceneComponent<TMP_Text>(slot, "所需");
        TMP_Text owned = FindSceneComponent<TMP_Text>(slot, "拥有");
        if (required != null) required.text = cost.required >= 0 ? cost.required.ToString(CultureInfo.InvariantCulture) : "--";
        if (owned != null)
        {
            owned.text = "/" + (cost.available >= 0 ? cost.available.ToString(CultureInfo.InvariantCulture) : "--");
            owned.color = cost.required >= 0 && cost.available >= 0 && cost.available < cost.required
                ? new Color32(0xC5, 0x3A, 0x3A, 0xFF) : new Color32(0x6A, 0x55, 0x3D, 0xFF);
        }
    }

    private bool IsRoleAtBreakthrough(RoleDisplayEntry entry)
    {
        if (entry == null || !entry.owned || entry.player == null) return false;
        long npcId = entry.player.playerNpcId;
        return IsPendingRoleGrowth(npcId, "/npc/breakthrough") || roleBreakthroughRequiredNpcId == npcId
            || (roleAutoUpgradePreviewNpcId == npcId && roleAutoUpgradePreview != null
                && roleAutoUpgradePreview.currentLevel == entry.player.level
                && roleAutoUpgradePreview.blockedLevel == entry.player.level + 1
                && (roleAutoUpgradePreview.outcome == "BREAKTHROUGH_REQUIRED"
                    || roleAutoUpgradePreview.stopReason == "BREAKTHROUGH_REQUIRED"));
    }

    private void ResetRoleBreakthroughPreview()
    {
        roleBreakthroughPreviewVersion++;
        roleBreakthroughPreview = null;
        roleBreakthroughPreviewNpcId = 0;
        roleBreakthroughRequiredNpcId = 0;
        roleBreakthroughPreviewLoading = false;
        roleBreakthroughPreviewError = string.Empty;
    }

    private void ConfigureRoleBreakthroughVisibility(Transform root, RoleDisplayEntry entry)
    {
        long npcId = entry != null && entry.owned && entry.player != null ? entry.player.playerNpcId : 0;
        if (roleBreakthroughPreviewNpcId != 0 && roleBreakthroughPreviewNpcId != npcId)
            ResetRoleBreakthroughPreview();
        bool showSelection = IsRoleAtBreakthrough(entry)
            && !roleBreakthroughPreviewLoading && string.IsNullOrEmpty(roleBreakthroughPreviewError)
            && roleBreakthroughPreview != null && roleBreakthroughPreview.playerNpcId == npcId
            && roleBreakthroughPreview.currentLevel == entry.player.level
            && GetRoleBreakthroughCosts(roleBreakthroughPreview).TrueForAll(cost => cost.available >= cost.required);
        SetSceneObjectActive(root, "BreakingThroughSelection", showSelection);
        SetSceneObjectActive(root, "Breakthrough", IsRoleAtBreakthrough(entry));
    }

    private void ConfigureRoleBreakthrough(Transform root, RoleDisplayEntry entry, Button button)
    {
        if (button == null) return;
        bool atBreakthrough = IsRoleAtBreakthrough(entry);
        button.gameObject.SetActive(atBreakthrough);
        if (!atBreakthrough)
        {
            button.interactable = false;
            BindSceneButton(button, null);
            return;
        }
        long npcId = entry.player.playerNpcId;
        bool retry = IsPendingRoleGrowth(npcId, "/npc/breakthrough");
        if (roleBreakthroughPreviewNpcId != npcId && !roleUpgradeRunning && rolePendingGrowthRequest == null)
        {
            roleBreakthroughPreviewNpcId = npcId;
            roleBreakthroughPreviewLoading = true;
            StartCoroutine(LoadRoleBreakthroughPreview(npcId, entry.player.level, ++roleBreakthroughPreviewVersion));
        }

        RoleBreakthroughPreviewData plan = roleBreakthroughPreview;
        bool ready = !roleBreakthroughPreviewLoading && string.IsNullOrEmpty(roleBreakthroughPreviewError)
            && IsValidRoleBreakthroughPreview(plan, npcId, entry.player.level);
        List<RoleBreakthroughCost> costs = ready ? GetRoleBreakthroughCosts(plan) : new List<RoleBreakthroughCost>();
        bool sufficient = ready && costs.TrueForAll(cost => cost.available >= cost.required);
        Transform panel = FindSceneTransform(root, "Breakthrough");
        bool slotsReady = ConfigureRoleBreakthroughSlots(panel, costs);

        bool previewFailed = !string.IsNullOrEmpty(roleBreakthroughPreviewError);
        button.interactable = !roleUpgradeRunning && !rolePanelLoading
            && (retry || (rolePendingGrowthRequest == null && (previewFailed || sufficient)));
        SetSceneButtonText(button, retry ? "重试突破" : roleBreakthroughPreviewLoading ? "读取中..."
            : previewFailed ? "重新预览" : "突破");
        BindSceneButton(button, !button.interactable ? null : (Action)(() =>
        {
            if (retry) StartCoroutine(SendRoleGrowthRequest(rolePendingGrowthRequest));
            else if (previewFailed)
            {
                ResetRoleBreakthroughPreview();
                roleBreakthroughRequiredNpcId = npcId;
                RenderRolePanel();
            }
            else ConfirmRoleBreakthrough(entry);
        }));

        if (previewFailed) SetSceneText(root, "RoleStatus", roleBreakthroughPreviewError);
        else if (ready && !slotsReady) SetSceneText(root, "RoleStatus", "突破资源框未配置");
    }

    private IEnumerator LoadRoleBreakthroughPreview(long npcId, int currentLevel, int version)
    {
        int playerId = GameSession.PlayerId;
        string token = GameSession.Token;
        UnityWebRequest request = CreateAuthorizedGet($"/npc/{npcId}/breakthrough-preview");
        if (request == null) roleBreakthroughPreviewError = requestCreationError;
        else using (request)
        {
            yield return request.SendWebRequest();
            if (version != roleBreakthroughPreviewVersion || roleBreakthroughPreviewNpcId != npcId
                || !IsRoleGrowthSessionCurrent(playerId, token)) yield break;
            if (HandleUnauthorizedResponse(request)) { ResetRoleBreakthroughPreview(); yield break; }
            RoleBreakthroughPreviewApiResponse response = ParseJson<RoleBreakthroughPreviewApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                roleBreakthroughPreviewError = response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "突破预览读取失败") : GetApiError(response);
            }
            else if (!IsValidRoleBreakthroughPreview(response.data, npcId, currentLevel))
            {
                roleBreakthroughPreviewError = "突破预览与当前角色或协议不一致，请重新预览";
            }
            else
            {
                roleBreakthroughPreview = response.data;
                roleBreakthroughPreviewError = string.Empty;
            }
        }
        roleBreakthroughPreviewLoading = false;
        RefreshRoleDetail();
    }

    private static bool IsValidRoleBreakthroughPreview(RoleBreakthroughPreviewData plan, long npcId, int currentLevel)
    {
        return plan != null && plan.contractVersion == 1 && plan.playerNpcId == npcId
            && currentLevel >= 1 && plan.currentLevel == currentLevel && plan.targetLevel == currentLevel + 1
            && plan.targetLevel <= 100 && plan.goldCost >= 0 && plan.fragmentCost > 0
            && !string.IsNullOrWhiteSpace(plan.fragmentItemKey) && plan.fragmentItemKey != "gold"
            && plan.availableGold >= 0 && plan.availableFragments >= 0
            && plan.goldBalance >= 0 && plan.fragmentBalance >= 0
            && IsValidUpgradePlanFingerprint(plan.planFingerprint);
    }

    private static List<RoleBreakthroughCost> GetRoleBreakthroughCosts(RoleBreakthroughPreviewData plan)
    {
        List<RoleBreakthroughCost> costs = new List<RoleBreakthroughCost>();
        if (plan.goldCost > 0) costs.Add(new RoleBreakthroughCost
        {
            key = "gold", name = "金币", required = plan.goldCost,
            available = Math.Max(plan.availableGold, plan.goldBalance)
        });
        costs.Add(new RoleBreakthroughCost
        {
            key = plan.fragmentItemKey,
            name = string.IsNullOrEmpty(plan.fragmentItemName) ? "角色专属碎片" : plan.fragmentItemName,
            required = plan.fragmentCost, available = Math.Max(plan.availableFragments, plan.fragmentBalance)
        });
        return costs;
    }

    private bool ConfigureRoleBreakthroughSlots(Transform panel, List<RoleBreakthroughCost> costs)
    {
        List<Transform> slots = new List<Transform>();
        if (panel == null) return false;
        Transform template = panel.Find("UpgradeBreak");
        if (template == null || template.GetComponent<Image>() == null
            || template.Find("所需") == null || template.Find("拥有") == null) return false;
        if (roleBreakthroughTemplate != template)
        {
            foreach (Transform generated in roleBreakthroughGeneratedSlots)
            {
                if (generated == null) continue;
                generated.gameObject.SetActive(false);
                Destroy(generated.gameObject);
            }
            roleBreakthroughGeneratedSlots.Clear();
            roleBreakthroughTemplate = template;
        }
        roleBreakthroughGeneratedSlots.RemoveAll(slot => slot == null);
        for (int index = 0; index < panel.childCount; index++)
        {
            Transform child = panel.GetChild(index);
            if (child.GetComponent<Image>() != null
                && (child.name == "UpgradeBreak" || child.name.StartsWith("UpgradeBreak (", StringComparison.Ordinal))
                && child.Find("所需") != null && child.Find("拥有") != null)
                slots.Add(child);
        }
        int authoredCount = slots.Count;
        while (roleBreakthroughGeneratedSlots.Count < Math.Max(0, costs.Count - authoredCount))
        {
            Transform clone = Instantiate(template.gameObject, template.parent, false).transform;
            clone.name = "RoleBreakthroughResource" + (roleBreakthroughGeneratedSlots.Count + 1);
            if (template is RectTransform templateRect && clone is RectTransform cloneRect
                && template.parent.GetComponent<LayoutGroup>() == null)
            {
                RectTransform lastSlot = slots[authoredCount - 1] as RectTransform;
                Vector2 offset = authoredCount > 1 && slots[authoredCount - 2] is RectTransform previousSlot
                    ? lastSlot.anchoredPosition - previousSlot.anchoredPosition
                    : new Vector2(templateRect.rect.width + 12f, 0f);
                cloneRect.anchoredPosition = lastSlot.anchoredPosition + offset * (roleBreakthroughGeneratedSlots.Count + 1);
            }
            roleBreakthroughGeneratedSlots.Add(clone);
        }
        slots.AddRange(roleBreakthroughGeneratedSlots);
        for (int index = 0; index < slots.Count; index++)
        {
            Transform slot = slots[index];
            if (slot == null) continue;
            slot.gameObject.SetActive(index < costs.Count);
            if (index >= costs.Count) continue;
            RoleBreakthroughCost cost = costs[index];
            ApplyRoleResourceCost(slot, cost);
            SetSceneObjectActive(slot, "Select", false);
            SetSceneText(slot, "Quantity", $"{cost.available}/{cost.required}");
            SetSceneText(slot, "ResourceName", cost.name);
            Button button = slot.GetComponent<Button>();
            if (button != null)
            {
                BindSceneButton(button, null);
                button.enabled = false;
            }
        }
        return true;
    }

    private void ConfirmRoleBreakthrough(RoleDisplayEntry entry)
    {
        RoleBreakthroughPreviewData plan = roleBreakthroughPreview;
        if (entry == null || entry.player == null || roleUpgradeRunning || rolePendingGrowthRequest != null
            || rolePanelLoading || roleBreakthroughPreviewLoading || !string.IsNullOrEmpty(roleBreakthroughPreviewError)
            || !IsValidRoleBreakthroughPreview(plan, entry.player.playerNpcId, entry.player.level)) return;
        List<RoleBreakthroughCost> costs = GetRoleBreakthroughCosts(plan);
        if (!costs.TrueForAll(cost => cost.available >= cost.required)) return;
        RoleAutoUpgradeRequest payload = new RoleAutoUpgradeRequest
        {
            playerNpcId = plan.playerNpcId, expectedCurrentLevel = plan.currentLevel,
            targetLevel = plan.targetLevel, planFingerprint = plan.planFingerprint
        };
        StartCoroutine(SendRoleGrowthRequest(CreateRoleGrowthRequest("/npc/breakthrough", payload)));
    }

    private static RoleGrowthRequest CreateRoleGrowthRequest(string path, RoleAutoUpgradeRequest payload)
    {
        return new RoleGrowthRequest
        {
            playerId = GameSession.PlayerId, playerNpcId = payload.playerNpcId,
            currentLevel = payload.expectedCurrentLevel, targetLevel = payload.targetLevel,
            path = path, json = JsonUtility.ToJson(payload), idempotencyKey = Guid.NewGuid().ToString("D")
        };
    }

    private bool IsRoleGrowthSessionCurrent(int playerId, string token)
    {
        return HasAuthSession() && GameSession.PlayerId == playerId && GameSession.Token == token;
    }

    private bool IsPendingRoleGrowth(long npcId, string path)
    {
        return rolePendingGrowthRequest != null && rolePendingGrowthRequest.playerId == GameSession.PlayerId
            && rolePendingGrowthRequest.playerNpcId == npcId && rolePendingGrowthRequest.path == path;
    }

    private IEnumerator SendRoleGrowthRequest(RoleGrowthRequest operation)
    {
        if (operation == null || roleUpgradeRunning || !HasAuthSession() || operation.playerId != GameSession.PlayerId
            || (rolePendingGrowthRequest != null && rolePendingGrowthRequest != operation)) yield break;
        string token = GameSession.Token;
        rolePendingGrowthRequest = operation;
        roleUpgradeRunning = true;
        roleGrowthItemsRequestVersion++;
        bool breakthrough = operation.path == "/npc/breakthrough";
        roleStatusMessage = breakthrough ? "正在提交角色突破..." : "正在执行角色一键升级...";
        RenderRolePanel();
        if (!TryCreateAuthorizedPost(operation.path, operation.json, out UnityWebRequest request, out string error))
        {
            roleUpgradeRunning = false;
            roleStatusMessage = error;
            RenderRolePanel();
            yield break;
        }
        ApiRequestUtility.SetIdempotencyKey(request, operation.idempotencyKey);
        using (request)
        {
            yield return request.SendWebRequest();
            if (!IsRoleGrowthSessionCurrent(operation.playerId, token))
            {
                roleUpgradeRunning = false;
                yield break;
            }
            if (HandleUnauthorizedResponse(request)) { roleUpgradeRunning = false; yield break; }
            RoleAutoUpgradeApiResponse response = ParseJson<RoleAutoUpgradeApiResponse>(request.downloadHandler.text);
            int reachedLevel = response != null && response.data != null
                ? (breakthrough ? response.data.level : response.data.reachedLevel) : 0;
            bool valid = request.result == UnityWebRequest.Result.Success && response != null && response.code == 200
                && response.data != null && response.data.playerNpcId == operation.playerNpcId
                && reachedLevel > operation.currentLevel && reachedLevel <= operation.targetLevel
                && (!breakthrough || (response.data.contractVersion == 1 && reachedLevel == operation.targetLevel));
            if (!valid)
            {
                bool rejected = response != null && response.code != 200 && request.responseCode > 0 && request.responseCode < 500;
                roleStatusMessage = response == null ? ApiRequestUtility.GetErrorMessage(request, "请求结果未确认，请重试")
                    : response.code == 200 ? "服务器升级结果不完整，请使用原请求重试确认" : GetApiError(response);
                if (rejected)
                {
                    rolePendingGrowthRequest = null;
                    ResetRoleAutoUpgradePreview();
                    ResetRoleBreakthroughPreview();
                    if (breakthrough || response.code == 6109) roleBreakthroughRequiredNpcId = operation.playerNpcId;
                    if (response.code == 6104) roleStatusMessage = "资源不足，请前往获取";
                    if (response.code == 6106 || response.code == 6108)
                    {
                        yield return RefreshRoleGrowthState(operation.playerId, token);
                        yield break;
                    }
                }
                else roleStatusMessage += "，请点击重试，勿重复发起新升级";
                roleUpgradeRunning = false;
                RenderRolePanel();
                yield break;
            }
            rolePendingGrowthRequest = null;
            roleStatusMessage = $"角色已升级到 Lv.{reachedLevel}"
                + (response.data.outcome == "BREAKTHROUGH_REQUIRED" ? "，请先手动突破" : "，资源已由服务器结算");
        }
        yield return RefreshRoleGrowthState(operation.playerId, token);
    }

    private IEnumerator RefreshRoleGrowthState(int playerId, string token)
    {
        ResetRoleAutoUpgradePreview();
        ResetRoleBreakthroughPreview();
        ResetRoleLoadoutState();
        if (IsRoleGrowthSessionCurrent(playerId, token)) yield return RefreshGeneralInventoryAfterRoleMutation();
        string resultMessage = roleStatusMessage;
        bool refreshed = false;
        if (IsRoleGrowthSessionCurrent(playerId, token))
            yield return LoadRolePanelData(roleRequestVersion, playerId, () => refreshed = true);
        roleUpgradeRunning = false;
        if (IsRoleGrowthSessionCurrent(playerId, token))
        {
            if (refreshed) roleStatusMessage = resultMessage;
            RenderRolePanel();
        }
    }

    private List<Transform> GetRoleListRows(Transform root)
    {
        List<Transform> rows = new List<Transform>();
        Transform content = FindSceneTransform(root, "RoleListContent");
        if (content == null)
        {
            return rows;
        }

        for (int i = 0; i < content.childCount; i++)
        {
            Transform child = content.GetChild(i);
            if (child != null
                && child.name.StartsWith("RoleListRow", StringComparison.Ordinal)
                && !child.name.StartsWith("RoleListRowTemplateOld", StringComparison.Ordinal))
            {
                rows.Add(child);
            }
        }

        return rows;
    }

    private void EnsureRoleListRows(Transform root, int requiredCount)
    {
        Transform content = FindSceneTransform(root, "RoleListContent");
        if (content == null)
        {
            return;
        }

        if (requiredCount <= 0)
        {
            UpdateRoleListScrollGeometry(root, content, 0);
            return;
        }

        Transform template = content.Find("RoleListRow1");
        if (template == null)
        {
            Debug.LogWarning("RoleListRow1 is missing; cannot create additional role rows.", root);
            return;
        }

        int existingCount = GetRoleListRows(root).Count;
        int nextIndex = 1;
        while (content.Find("RoleListRow" + nextIndex) != null)
        {
            nextIndex++;
        }

        for (int i = existingCount; i < requiredCount; i++)
        {
            GameObject clone = Instantiate(template.gameObject, content, false);
            clone.name = "RoleListRow" + nextIndex;
            RenameRoleRowTemplateChildren(clone.transform, nextIndex);
            nextIndex++;
        }

        UpdateRoleListScrollGeometry(root, content, requiredCount);
    }

    private void UpdateRoleListScrollGeometry(Transform root, Transform content, int visibleCount)
    {
        RectTransform contentRect = content == null ? null : content.GetComponent<RectTransform>();
        GridLayoutGroup grid = content == null ? null : content.GetComponent<GridLayoutGroup>();
        Transform viewport = FindSceneTransform(root, "RoleListViewport");
        RectTransform viewportRect = viewport == null ? null : viewport.GetComponent<RectTransform>();
        if (contentRect == null || grid == null || !grid.enabled || viewportRect == null)
        {
            return;
        }

        if (roleListContentRect != contentRect)
        {
            roleListContentRect = contentRect;
            roleListSceneContentHeight = contentRect.rect.height;
            roleListSceneContentSizeDelta = contentRect.sizeDelta;
            roleListSceneContentPosition = contentRect.anchoredPosition;
        }

        ScrollRect scroll = viewport.GetComponent<ScrollRect>() ?? content.GetComponentInParent<ScrollRect>();
        if (roleListScrollRect != scroll)
        {
            roleListScrollRect = scroll;
            roleListSceneScrollEnabled = scroll != null && scroll.enabled;
            roleListSceneScrollVertical = scroll != null && scroll.vertical;
        }

        float availableWidth = Mathf.Max(1f, contentRect.rect.width - grid.padding.horizontal);
        int columns = Mathf.Max(1, Mathf.FloorToInt(
            (availableWidth + grid.spacing.x) / Mathf.Max(1f, grid.cellSize.x + grid.spacing.x)));
        if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
        {
            columns = Mathf.Max(1, grid.constraintCount);
        }

        int rowCount = Mathf.CeilToInt(Mathf.Max(0, visibleCount) / (float)columns);
        if (grid.constraint == GridLayoutGroup.Constraint.FixedRowCount)
        {
            rowCount = Mathf.Min(Mathf.Max(0, visibleCount), Mathf.Max(1, grid.constraintCount));
        }
        float contentHeight = grid.padding.vertical
            + rowCount * grid.cellSize.y
            + Mathf.Max(0, rowCount - 1) * grid.spacing.y;
        bool canScroll = visibleCount > 0 && contentHeight > viewportRect.rect.height + 0.5f;
        // Only the overflow extent is data-driven; keep the authored layout when the list fits.
        if (canScroll)
        {
            contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Max(roleListSceneContentHeight, contentHeight));
        }
        else
        {
            contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, roleListSceneContentSizeDelta.y);
        }

        if (scroll != null && scroll.content == contentRect)
        {
            scroll.vertical = canScroll && roleListSceneScrollVertical;
            scroll.enabled = roleListSceneScrollEnabled && (scroll.horizontal || scroll.vertical);
            if (!canScroll)
            {
                scroll.StopMovement();
                contentRect.anchoredPosition = roleListSceneContentPosition;
            }
        }

    }

    private static void RenameRoleRowTemplateChildren(Transform row, int rowIndex)
    {
        Transform[] children = row.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] == row)
            {
                continue;
            }

            string childName = children[i].name;
            if (childName.EndsWith("0", System.StringComparison.Ordinal))
            {
                children[i].name = childName.Substring(0, childName.Length - 1) + rowIndex;
            }
        }
    }

    private void ResetRoleLoadoutState()
    {
        roleLoadoutRequestVersion++;
        roleLoadoutData = null;
        roleLoadoutNpcId = 0L;
        roleLoadoutLoading = false;
        roleLoadoutError = string.Empty;
    }

    private void ResetRoleCombatProfiles()
    {
        roleCombatProfilesRequestVersion++;
        roleCombatProfiles = null;
        roleCombatProfilesLoading = false;
        roleCombatProfilesError = string.Empty;
    }

    private IEnumerator LoadRoleCombatProfiles(int panelVersion, int playerId, string token, int requestVersion)
    {
        roleCombatProfilesLoading = true;
        UnityWebRequest request = CreateAuthorizedGet($"/idle-adventure/players/{playerId}/npcs");
        if (request == null)
        {
            roleCombatProfilesLoading = false;
            roleCombatProfilesError = requestCreationError;
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (requestVersion != roleCombatProfilesRequestVersion
                || !IsRoleRequestCurrent(panelVersion, playerId, token))
            {
                yield break;
            }
            if (HandleUnauthorizedResponse(request))
            {
                roleCombatProfilesLoading = false;
                yield break;
            }

            RoleCombatProfilesApiResponse response = ParseJson<RoleCombatProfilesApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null
                || response.code != 200 || response.data == null)
            {
                roleCombatProfilesError = response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败") : GetApiError(response);
            }
            else
            {
                roleCombatProfiles = response.data;
            }
        }

        roleCombatProfilesLoading = false;
        if (!rolePanelLoading) RefreshRoleDetail();
    }

    private void EnsureRoleLoadout(RoleDisplayEntry entry)
    {
        if (!HasAuthSession() || entry == null || !entry.owned || entry.player == null || entry.player.playerNpcId <= 0L
            || roleUpgradeRunning || rolePendingGrowthRequest != null)
        {
            ResetRoleLoadoutState();
            return;
        }

        long playerNpcId = entry.player.playerNpcId;
        if (roleLoadoutNpcId == playerNpcId
            && (roleLoadoutLoading || roleLoadoutData != null || !string.IsNullOrEmpty(roleLoadoutError)))
        {
            return;
        }

        roleLoadoutNpcId = playerNpcId;
        roleLoadoutData = null;
        roleLoadoutLoading = true;
        roleLoadoutError = string.Empty;
        int requestVersion = ++roleLoadoutRequestVersion;
        StartCoroutine(LoadRoleLoadout(playerNpcId, requestVersion));
    }

    private IEnumerator LoadRoleLoadout(long playerNpcId, int requestVersion)
    {
        int playerId = GameSession.PlayerId;
        string token = GameSession.Token;
        UnityWebRequest request = CreateAuthorizedGet($"/equipment/loadout/{playerNpcId}");
        if (request == null)
        {
            if (requestVersion == roleLoadoutRequestVersion && roleLoadoutNpcId == playerNpcId)
            {
                roleLoadoutLoading = false;
                roleLoadoutError = requestCreationError;
                RefreshRoleDetail();
            }
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (requestVersion != roleLoadoutRequestVersion || roleLoadoutNpcId != playerNpcId
                || !IsRoleGrowthSessionCurrent(playerId, token))
            {
                yield break;
            }

            if (HandleUnauthorizedResponse(request))
            {
                roleLoadoutLoading = false;
                yield break;
            }

            RoleLoadoutApiResponse response = ParseJson<RoleLoadoutApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success
                || response == null
                || response.code != 200
                || response.data == null)
            {
                roleLoadoutError = response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response);
            }
            else if (response.data.playerNpcId != playerNpcId)
            {
                roleLoadoutError = "属性数据与当前角色不一致";
            }
            else
            {
                roleLoadoutData = response.data;
                roleLoadoutError = string.Empty;
            }
        }

        roleLoadoutLoading = false;
        RefreshRoleDetail();
    }

    private void EnsureRoleAutoUpgradePreview(RoleDisplayEntry entry)
    {
        if (!HasAuthSession() || entry == null || !entry.owned || entry.player == null || entry.player.playerNpcId <= 0L
            || rolePanelLoading || roleUpgradeRunning || rolePendingGrowthRequest != null)
        {
            return;
        }

        long playerNpcId = entry.player.playerNpcId;
        int currentLevel = Mathf.Max(1, entry.player.level);
        if (roleAutoUpgradePreviewNpcId == playerNpcId
            && (roleAutoUpgradePreviewRunning || !string.IsNullOrEmpty(roleAutoUpgradePreviewError)
                || (roleAutoUpgradePreview != null && roleAutoUpgradePreview.currentLevel == currentLevel)))
        {
            return;
        }

        int maxLevel = GetRoleMaxLevel(entry);
        if (currentLevel >= maxLevel)
        {
            roleAutoUpgradePreview = null;
            roleAutoUpgradePreviewNpcId = playerNpcId;
            roleAutoUpgradePreviewRunning = false;
            roleAutoUpgradePreviewError = string.Empty;
            return;
        }

        roleAutoUpgradePreview = null;
        roleAutoUpgradePreviewNpcId = playerNpcId;
        roleAutoUpgradePreviewRunning = true;
        roleAutoUpgradePreviewError = string.Empty;
        int requestVersion = ++roleAutoUpgradePreviewRequestVersion;
        StartCoroutine(LoadRoleAutoUpgradePreview(playerNpcId, maxLevel, requestVersion));
    }

    private IEnumerator LoadRoleAutoUpgradePreview(long playerNpcId, int targetLevel, int requestVersion)
    {
        int sessionPlayerId = GameSession.PlayerId;
        string sessionToken = GameSession.Token;
        UnityWebRequest request = CreateAuthorizedGet($"/npc/{playerNpcId}/auto-upgrade-preview?targetLevel={Mathf.Clamp(targetLevel, 1, 100)}");
        if (request == null)
        {
            if (requestVersion == roleAutoUpgradePreviewRequestVersion && roleAutoUpgradePreviewNpcId == playerNpcId)
            {
                roleAutoUpgradePreviewRunning = false;
                roleAutoUpgradePreviewError = requestCreationError;
                RefreshRoleDetail();
            }
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (requestVersion != roleAutoUpgradePreviewRequestVersion
                || roleAutoUpgradePreviewNpcId != playerNpcId
                || !IsRoleGrowthSessionCurrent(sessionPlayerId, sessionToken)
                || rolePanelObject == null
                || !rolePanelObject.activeInHierarchy)
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
            else if (!IsValidRoleAutoUpgradePreview(response.data, playerNpcId, targetLevel))
            {
                roleAutoUpgradePreview = null;
                roleAutoUpgradePreviewError = "角色升级预览与当前角色不一致，请重新预览";
            }
            else
            {
                roleAutoUpgradePreview = response.data;
                roleAutoUpgradePreviewError = string.Empty;
            }
        }

        roleAutoUpgradePreviewRunning = false;
        RefreshRoleDetail();
    }

    private static bool IsValidRoleAutoUpgradePreview(
        RoleAutoUpgradePreviewData preview,
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

    private static bool IsValidUpgradePlanFingerprint(string fingerprint)
    {
        if (string.IsNullOrEmpty(fingerprint) || fingerprint.Length != 64)
        {
            return false;
        }

        for (int index = 0; index < fingerprint.Length; index++)
        {
            char character = fingerprint[index];
            if (!((character >= '0' && character <= '9')
                || (character >= 'a' && character <= 'f')))
            {
                return false;
            }
        }

        return true;
    }

    private IEnumerator AutoUpgradeRole(RolePlayerEntry player, int fallbackTargetLevel)
    {
        if (player == null || player.playerNpcId <= 0L || roleUpgradeRunning || rolePendingGrowthRequest != null) yield break;

        bool hasMatchingPreview = roleAutoUpgradePreview != null
            && roleAutoUpgradePreviewNpcId == player.playerNpcId;
        if (!hasMatchingPreview || roleAutoUpgradePreview.currentLevel != player.level)
        {
            ResetRoleAutoUpgradePreview();
            RenderRolePanel();
            yield break;
        }
        int currentLevel = roleAutoUpgradePreview.currentLevel;
        int payableTargetLevel = roleAutoUpgradePreview.payableTargetLevel;
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

        RoleAutoUpgradeRequest payload = new RoleAutoUpgradeRequest
        {
            playerNpcId = player.playerNpcId,
            expectedCurrentLevel = currentLevel,
            targetLevel = targetLevel,
            planFingerprint = planFingerprint
        };
        yield return SendRoleGrowthRequest(CreateRoleGrowthRequest("/npc/auto-upgrade", payload));
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
            DateTime expiresAt = default(DateTime);
            if (guide.contractVersion > 1)
            {
                roleStatusMessage = "角色获取协议版本过高，请更新客户端";
                RenderRolePanel();
                yield break;
            }

            if (!string.IsNullOrEmpty(guide.instructionExpiresAt)
                && !DateTime.TryParse(
                    guide.instructionExpiresAt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out expiresAt))
            {
                roleStatusMessage = "获取指引时间格式无效，请重新点击获取";
                RenderRolePanel();
                yield break;
            }

            if (!string.IsNullOrEmpty(guide.instructionExpiresAt)
                && DateTime.UtcNow > expiresAt)
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
            roleStatusMessage = "Acquisition route is unavailable.";
            RenderRolePanel();
            return;
        }

        if (!string.Equals(guide.status, "AVAILABLE", StringComparison.OrdinalIgnoreCase))
        {
            roleStatusMessage = string.IsNullOrEmpty(guide.message)
                ? "This acquisition route is no longer available."
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
        roleRequestVersion++;
        ResetRoleCombatProfiles();
        ResetRoleAutoUpgradePreview();
        ResetRoleBreakthroughPreview();
        ResetRoleLoadoutState();
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Role);
        if (panel != null)
        {
            CloseRoleActionPanels(panel.PanelRoot);
            panel.SetVisible(false);
        }

        rolePanelObject = null;
        rolePanelLoading = false;
    }

    private void RefreshRolePanelFromRealtime(string acquisitionId)
    {
        if (!string.IsNullOrEmpty(acquisitionId) && !roleAcquisitionEventIds.Add(acquisitionId))
        {
            return;
        }

        if (rolePanelObject != null && rolePanelObject.activeInHierarchy && HasAuthSession() && !rolePanelLoading)
        {
            roleRequestVersion++;
            StartCoroutine(LoadRolePanelData(roleRequestVersion, GameSession.PlayerId));
        }
    }

    private static string[] GetRoleIconKeys(RoleDisplayEntry entry)
    {
        if (entry == null)
        {
            return new string[0];
        }

        int catalogNpcId = entry.catalog == null ? 0 : entry.catalog.npcId;
        int playerNpcId = entry.player == null ? 0 : entry.player.npcId;
        return new[]
        {
            entry.catalog == null ? null : entry.catalog.icon,
            entry.player == null ? null : entry.player.icon,
            entry.catalog == null ? null : entry.catalog.npcCode,
            entry.player == null ? null : entry.player.npcCode,
            entry.catalog == null ? null : entry.catalog.modelKey,
            entry.player == null ? null : entry.player.modelKey,
            catalogNpcId > 0 ? catalogNpcId.ToString(CultureInfo.InvariantCulture) : null,
            playerNpcId > 0 ? playerNpcId.ToString(CultureInfo.InvariantCulture) : null,
            catalogNpcId > 0 ? "Npc_" + catalogNpcId.ToString(CultureInfo.InvariantCulture) : null,
            playerNpcId > 0 ? "Npc_" + playerNpcId.ToString(CultureInfo.InvariantCulture) : null
        };
    }

    private static string[] GetRoleModelKeys(RoleDisplayEntry entry)
    {
        if (entry == null)
        {
            return new string[0];
        }

        int catalogNpcId = entry.catalog == null ? 0 : entry.catalog.npcId;
        int playerNpcId = entry.player == null ? 0 : entry.player.npcId;
        return new[]
        {
            entry.catalog == null ? null : entry.catalog.modelKey,
            entry.player == null ? null : entry.player.modelKey,
            entry.catalog == null ? null : entry.catalog.icon,
            entry.player == null ? null : entry.player.icon,
            entry.catalog == null ? null : entry.catalog.npcCode,
            entry.player == null ? null : entry.player.npcCode,
            catalogNpcId > 0 ? catalogNpcId.ToString(CultureInfo.InvariantCulture) : null,
            playerNpcId > 0 ? playerNpcId.ToString(CultureInfo.InvariantCulture) : null,
            catalogNpcId > 0 ? "Npc_" + catalogNpcId.ToString(CultureInfo.InvariantCulture) : null,
            playerNpcId > 0 ? "Npc_" + playerNpcId.ToString(CultureInfo.InvariantCulture) : null
        };
    }

    private void ApplyRoleIcon(Image image, TMP_Text fallback, string roleName, string[] iconKeys, bool preferFullPortrait = false)
    {
        if (image == null)
        {
            return;
        }

        Sprite sprite = ResolveRoleSprite(iconKeys, preferFullPortrait);

        if (sprite != null)
        {
            image.sprite = sprite;
        }
        image.enabled = sprite != null;
        if (fallback != null)
        {
            fallback.gameObject.SetActive(sprite == null);
            fallback.text = string.IsNullOrEmpty(roleName) ? "NPC" : roleName.Substring(0, 1);
        }

        if (sprite == null)
        {
            Debug.LogWarning("Role sprite was not found for: " + (string.IsNullOrEmpty(roleName) ? "NPC" : roleName), image);
        }
    }

    private Sprite ResolveRoleSprite(string[] iconKeys, bool preferFullPortrait)
    {
        if (iconKeys == null)
        {
            return null;
        }

        Sprite boundSprite = FindBoundRoleSprite(iconKeys, preferFullPortrait);
        if (boundSprite != null)
        {
            return boundSprite;
        }

        for (int keyIndex = 0; keyIndex < iconKeys.Length; keyIndex++)
        {
            string normalizedKey = NormalizeRoleAssetKey(iconKeys[keyIndex]);
            if (string.IsNullOrEmpty(normalizedKey))
            {
                continue;
            }

            string[] candidates = preferFullPortrait
                ? new[]
                {
                    "_Project/Art/UI/角色/Q版/" + normalizedKey + ".png"
                }
                : new[]
                {
                    "_Project/Art/UI/角色/头像/" + normalizedKey + ".png"
                };

            for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
            {
                Sprite sprite = LoadSpriteFromAsset(candidates[candidateIndex]);
                if (sprite != null)
                {
                    return sprite;
                }
            }
        }

        return null;
    }

    private Sprite FindBoundRoleSprite(string[] iconKeys, bool preferFullPortrait)
    {
        RoleSpriteBinding[] bindings = preferFullPortrait ? roleModelBindings : roleIconBindings;
        if (bindings == null || iconKeys == null)
        {
            return null;
        }

        for (int keyIndex = 0; keyIndex < iconKeys.Length; keyIndex++)
        {
            string normalizedKey = NormalizeRoleAssetKey(iconKeys[keyIndex]);
            if (string.IsNullOrEmpty(normalizedKey))
            {
                continue;
            }

            for (int bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
            {
                RoleSpriteBinding binding = bindings[bindingIndex];
                if (binding != null && binding.sprite != null
                    && AreRoleAssetKeysEqual(binding.key, normalizedKey))
                {
                    return binding.sprite;
                }
            }
        }

        return null;
    }

    private static bool AreRoleAssetKeysEqual(string left, string right)
    {
        string normalizedLeft = NormalizeRoleAssetKey(left);
        string normalizedRight = NormalizeRoleAssetKey(right);
        if (string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        const string npcPrefix = "npc_";
        if (normalizedLeft.StartsWith(npcPrefix, StringComparison.OrdinalIgnoreCase))
        {
            normalizedLeft = normalizedLeft.Substring(npcPrefix.Length);
        }
        if (normalizedRight.StartsWith(npcPrefix, StringComparison.OrdinalIgnoreCase))
        {
            normalizedRight = normalizedRight.Substring(npcPrefix.Length);
        }

        return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRoleAssetKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        string normalizedKey = key.Trim().Replace('\\', '/');
        int separatorIndex = normalizedKey.LastIndexOf('/');
        if (separatorIndex >= 0)
        {
            normalizedKey = normalizedKey.Substring(separatorIndex + 1);
        }

        int extensionIndex = normalizedKey.LastIndexOf('.');
        if (extensionIndex > 0)
        {
            normalizedKey = normalizedKey.Substring(0, extensionIndex);
        }

        return normalizedKey;
    }

    private void ConfigureRoleDetailQuality(Transform root, RoleDisplayEntry entry)
    {
        TMP_Text label = FindSceneComponent<TMP_Text>(root, "RoleDetailQuality");
        if (label == null)
        {
            return;
        }

        if (roleDetailQualityText != label)
        {
            roleDetailQualityText = label;
            roleDetailQualitySceneColor = label.color;
        }

        if (entry == null || entry.catalog == null)
        {
            label.text = string.Empty;
            label.color = roleDetailQualitySceneColor;
            return;
        }

        string qualityLabel = FormatRoleQualityForDisplay(entry.catalog.quality);
        string qualityCode;
        switch (qualityLabel)
        {
            case "\u4f18\u79c0": qualityCode = "FINE"; break;
            case "\u7a00\u6709": qualityCode = "EXCELLENT"; break;
            case "\u53f2\u8bd7": qualityCode = "EPIC"; break;
            case "\u4f20\u8bf4": qualityCode = "LEGENDARY"; break;
            default: qualityCode = "COMMON"; break;
        }

        Color qualityColor = GetEquipmentWearerQualityColor(qualityCode);
        qualityColor.a = roleDetailQualitySceneColor.a;
        label.text = "\u54c1\u8d28  " + qualityLabel;
        label.color = qualityColor;
    }

    private static string FormatRoleQualityForDisplay(string quality)
    {
        string value = (quality ?? string.Empty).Trim().ToUpperInvariant();
        if (value == "COMMON" || value == "NORMAL" || value == "WHITE" || value == "\u666e\u901a" || value == "\u767d\u8272")
        {
            return "\u666e\u901a";
        }
        if (value == "FINE" || value == "UNCOMMON" || value == "GREEN" || value == "\u7cbe\u826f" || value == "\u4f18\u79c0" || value == "\u7eff\u8272")
        {
            return "\u4f18\u79c0";
        }
        if (value == "EXCELLENT" || value == "RARE" || value == "BLUE" || value == "\u7a00\u6709" || value == "\u84dd\u8272")
        {
            return "\u7a00\u6709";
        }
        if (value == "EPIC" || value == "PURPLE" || value == "\u53f2\u8bd7" || value == "\u7d2b\u8272")
        {
            return "\u53f2\u8bd7";
        }
        if (value == "LEGENDARY" || value == "MYTHIC" || value == "SSR" || value == "GOLD"
            || value == "RED" || value == "LUXURY" || value == "\u4f20\u8bf4" || value == "\u91d1\u8272" || value == "\u7ea2\u8272")
        {
            return "\u4f20\u8bf4";
        }

        return "\u666e\u901a";
    }

    private static string GetRoleStatusLabelForDisplay(RoleDisplayEntry entry)
    {
        if (entry == null)
        {
            return "\u672a\u77e5";
        }
        if (entry.owned)
        {
            return "\u5df2\u83b7\u53d6";
        }

        string status = entry.catalog.acquisition == null ? string.Empty : entry.catalog.acquisition.status;
        switch ((status ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "AVAILABLE": return "\u53ef\u83b7\u53d6";
            case "LOCKED": return "\u5f85\u89e3\u9501";
            case "UPCOMING": return "\u5373\u5c06\u5f00\u653e";
            case "ENDED": return "\u6d3b\u52a8\u7ed3\u675f";
            case "UNAVAILABLE": return "\u6682\u65e0\u9014\u5f84";
            default: return "\u672a\u83b7\u53d6";
        }
    }

    private static string FormatRoleQuality(string quality)
    {
        return string.IsNullOrEmpty(quality) ? "未知" : quality;
    }

    private Sprite GetRoleQualityBackground(string quality)
    {
        string value = (quality ?? string.Empty).Trim().ToUpperInvariant();
        if (value == "COMMON" || value == "NORMAL" || value == "WHITE" || value == "\u666e\u901a" || value == "\u767d\u8272")
        {
            return roleQualityWhiteBackground;
        }
        if (value == "FINE" || value == "UNCOMMON" || value == "GREEN" || value == "\u7cbe\u826f" || value == "\u7eff\u8272")
        {
            return roleQualityGreenBackground;
        }
        if (value == "EXCELLENT" || value == "RARE" || value == "BLUE" || value == "\u4f18\u79c0" || value == "\u84dd\u8272")
        {
            return roleQualityBlueBackground;
        }
        if (value == "EPIC" || value == "PURPLE" || value == "\u53f2\u8bd7" || value == "\u7d2b\u8272")
        {
            return roleQualityPurpleBackground;
        }
        if (value == "LEGENDARY" || value == "MYTHIC" || value == "SSR" || value == "GOLD"
            || value == "RED" || value == "LUXURY" || value == "\u4f20\u8bf4" || value == "\u91d1\u8272" || value == "\u7ea2\u8272")
        {
            return roleQualityGoldBackground;
        }

        return roleQualityWhiteBackground;
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

    private static string FormatRoleCombatAttributeForDisplay(RoleDisplayEntry entry)
    {
        string attribute = entry == null || entry.catalog == null
            ? string.Empty
            : entry.catalog.combatAttribute;
        if (string.IsNullOrWhiteSpace(attribute) && entry != null && entry.player != null)
        {
            attribute = entry.player.combatAttribute;
        }

        switch ((attribute ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "PHYSICAL": return "\u7269\u7406";
            case "FIRE": return "\u706b";
            case "WATER": return "\u6c34";
            case "WOOD": return "\u6728";
            case "EARTH": return "\u571f";
            case "ICE": return "\u51b0";
            case "THUNDER": return "\u96f7";
            default: return "\u672a\u77e5";
        }
    }

    private void ConfigureRoleBattleStats(Transform root, RoleDisplayEntry entry)
    {
        Transform statsRoot = FindSceneTransform(root, "RoleDetailBattleStats");
        if (statsRoot == null)
        {
            return;
        }

        bool hasStats = entry != null && entry.owned && entry.player != null
            && roleLoadoutData != null && !roleLoadoutLoading
            && string.IsNullOrEmpty(roleLoadoutError)
            && roleLoadoutNpcId == entry.player.playerNpcId
            && roleLoadoutData.playerNpcId == entry.player.playerNpcId
            && (roleLoadoutData.npcLevel <= 0 || roleLoadoutData.npcLevel == entry.player.level);

        RoleCombatProfile profile = FindRoleCombatProfile(entry);
        string unavailable = GetRoleBattleStatsUnavailableText(entry);
        SetRoleBattleStatText(statsRoot, "Blood", hasStats
            ? roleLoadoutData.totalHealth.ToString(CultureInfo.InvariantCulture)
            : profile != null ? profile.Health.ToString(CultureInfo.InvariantCulture) : unavailable);
        SetRoleBattleStatText(statsRoot, "Attack", hasStats
            ? roleLoadoutData.totalAttack.ToString(CultureInfo.InvariantCulture)
            : profile != null ? profile.attack.ToString(CultureInfo.InvariantCulture) : unavailable);
        SetRoleBattleStatText(statsRoot, "Defense", hasStats
            ? roleLoadoutData.totalDefense.ToString(CultureInfo.InvariantCulture)
            : profile != null ? profile.defense.ToString(CultureInfo.InvariantCulture) : unavailable);
        SetRoleBattleStatText(statsRoot, "Speed", ResolveRoleSpeedText(entry, hasStats));
    }

    private static void SetRoleBattleStatText(Transform root, string statName, string value)
    {
        SetSceneText(FindSceneTransform(root, statName), "Value", value);
    }

    private RoleCombatProfile FindRoleCombatProfile(RoleDisplayEntry entry)
    {
        if (entry == null || !entry.owned || entry.player == null || roleCombatProfiles == null
            || roleUpgradeRunning || rolePendingGrowthRequest != null)
        {
            return null;
        }

        foreach (RoleCombatProfile profile in roleCombatProfiles)
        {
            if (profile == null || profile.Health <= 0
                || (profile.level > 0 && profile.level != entry.player.level)) continue;
            // Older combat responses identify the NPC configuration, not the owned instance.
            bool matches = profile.PlayerNpcId > 0
                ? profile.PlayerNpcId == entry.player.playerNpcId
                : profile.DefinitionId > 0 && profile.DefinitionId == entry.player.npcId;
            if (matches) return profile;
        }

        return null;
    }

    private string GetRoleBattleStatsUnavailableText(RoleDisplayEntry entry)
    {
        if (entry == null) return rolePanelLoading ? "读取中" : "暂无数据";
        if (!entry.owned) return "未获得";
        if (rolePanelLoading || roleLoadoutLoading || roleCombatProfilesLoading
            || roleUpgradeRunning || rolePendingGrowthRequest != null) return "读取中";
        if (!string.IsNullOrEmpty(roleLoadoutError) || !string.IsNullOrEmpty(roleCombatProfilesError)) return "读取失败";
        return "未配置";
    }

    private string ResolveRoleSpeedText(RoleDisplayEntry entry, bool hasStats)
    {
        if (hasStats && roleLoadoutData != null && roleLoadoutData.totalSpeed > 0)
        {
            return roleLoadoutData.totalSpeed.ToString(CultureInfo.InvariantCulture);
        }
        RoleCombatProfile profile = FindRoleCombatProfile(entry);
        if (profile != null)
        {
            return profile.speed.ToString(CultureInfo.InvariantCulture);
        }
        if (hasStats && roleLoadoutData != null && roleLoadoutData.baseSpeed > 0)
        {
            return (roleLoadoutData.baseSpeed + roleLoadoutData.equipmentSpeedBonus)
                .ToString(CultureInfo.InvariantCulture);
        }
        return GetRoleBattleStatsUnavailableText(entry);
    }

    private void ConfigureRoleDescription(Transform root, RoleDisplayEntry entry)
    {
        TMP_Text description = FindSceneComponent<TMP_Text>(root, "RoleDetailDescription");
        if (description == null)
        {
            return;
        }

        int npcId = entry == null ? 0 : entry.catalog.npcId;
        string text = entry == null ? string.Empty
            : string.IsNullOrEmpty(entry.catalog.description) ? "\u6682\u65e0\u89d2\u8272\u63cf\u8ff0" : entry.catalog.description;
        bool resetScroll = roleDescriptionNpcId != npcId || description.text != text;
        roleDescriptionNpcId = npcId;
        description.text = text;

        ScrollRect scroll = FindSceneComponent<ScrollRect>(root, "RoleDetailDescriptionViewport");
        if (resetScroll && scroll != null && scroll.content == description.rectTransform)
        {
            scroll.StopMovement();
            LayoutRebuilder.ForceRebuildLayoutImmediate(description.rectTransform);
            scroll.verticalNormalizedPosition = 1f;
        }
    }

    private string BuildRoleEquipmentText(RoleDisplayEntry entry)
    {
        if (entry == null || entry.player == null)
        {
            return "已穿戴装备  未获得角色";
        }

        if (roleLoadoutLoading && roleLoadoutNpcId == entry.player.playerNpcId)
        {
            return "已穿戴装备  正在读取服务器数据...";
        }

        if (!string.IsNullOrEmpty(roleLoadoutError) && roleLoadoutNpcId == entry.player.playerNpcId)
        {
            return "已穿戴装备  读取失败：" + roleLoadoutError;
        }

        if (roleLoadoutData == null || roleLoadoutNpcId != entry.player.playerNpcId)
        {
            return "已穿戴装备  暂无服务器数据";
        }

        if (roleLoadoutData.equipment == null || roleLoadoutData.equipment.Length == 0)
        {
            return "已穿戴装备  暂无装备";
        }

        List<string> equipmentLabels = new List<string>();
        for (int i = 0; i < roleLoadoutData.equipment.Length; i++)
        {
            RoleLoadoutEquipmentData equipment = roleLoadoutData.equipment[i];
            if (equipment == null)
            {
                continue;
            }

            string equipmentName = string.IsNullOrEmpty(equipment.name) ? equipment.equipmentKey : equipment.name;
            if (string.IsNullOrEmpty(equipmentName))
            {
                equipmentName = "未命名装备";
            }

            string equipmentType = string.IsNullOrEmpty(equipment.equipmentType)
                ? "装备" : GetEquipmentSlotLabel(equipment.equipmentType.ToLowerInvariant());
            equipmentLabels.Add($"{equipmentType}：{equipmentName} Lv.{Mathf.Max(1, equipment.level)}");
        }

        return equipmentLabels.Count == 0
            ? "已穿戴装备  暂无装备"
            : "已穿戴装备  " + string.Join("  ", equipmentLabels.ToArray());
    }

    private void ConfigureRoleEquipmentDisplay(Transform root, RoleDisplayEntry entry)
    {
        Transform equipmentLabelRoot = FindSceneTransform(root, "RoleDetailEquipment");
        if (equipmentLabelRoot == null)
        {
            return;
        }

        Transform equipmentParent = equipmentLabelRoot.Find("EquipmentP");
        if (entry == null || entry.player == null || !entry.owned || equipmentParent == null)
        {
            SetSceneText(equipmentLabelRoot, "RoleDetailEquipment", "暂无装备");
            if (equipmentParent != null)
            {
                equipmentParent.gameObject.SetActive(false);
            }
            return;
        }

        if (roleLoadoutLoading && roleLoadoutNpcId == entry.player.playerNpcId)
        {
            SetSceneText(equipmentLabelRoot, "RoleDetailEquipment", "已穿戴装备");
            equipmentParent.gameObject.SetActive(true);
            SetRoleEquipmentSlots(equipmentParent, null);
            return;
        }

        bool validLoadout = roleLoadoutData != null
            && roleLoadoutNpcId == entry.player.playerNpcId
            && roleLoadoutData.playerNpcId == entry.player.playerNpcId
            && string.IsNullOrEmpty(roleLoadoutError);
        RoleLoadoutEquipmentData[] equipment = validLoadout && roleLoadoutData.equipment != null
            ? Array.FindAll(roleLoadoutData.equipment, item => item != null) : null;
        if (equipment == null || equipment.Length == 0)
        {
            SetSceneText(equipmentLabelRoot, "RoleDetailEquipment", "暂无装备");
            equipmentParent.gameObject.SetActive(false);
            SetRoleEquipmentSlots(equipmentParent, null);
            return;
        }

        SetSceneText(equipmentLabelRoot, "RoleDetailEquipment", "已穿戴装备");
        equipmentParent.gameObject.SetActive(true);
        SetRoleEquipmentSlots(equipmentParent, equipment);
    }

    private void SetRoleEquipmentSlots(Transform equipmentParent, RoleLoadoutEquipmentData[] equipment)
    {
        if (equipmentParent == null)
        {
            return;
        }

        Transform template = equipmentParent.Find("EquipmentQuality");
        if (template == null || template.GetComponent<Image>() == null
            || FindSceneComponent<Image>(template, "Equipment") == null)
        {
            return;
        }

        List<Transform> slots = new List<Transform>();
        for (int childIndex = 0; childIndex < equipmentParent.childCount; childIndex++)
        {
            Transform slot = equipmentParent.GetChild(childIndex);
            if (slot == template || (slot.name.StartsWith("EquipmentQuality_", StringComparison.Ordinal)
                && int.TryParse(slot.name.Substring("EquipmentQuality_".Length), out _)))
            {
                slots.Add(slot);
            }
        }

        int equipmentCount = equipment == null ? 0 : equipment.Length;
        while (slots.Count < equipmentCount)
        {
            Transform slot = Instantiate(template, equipmentParent, false);
            slot.name = "EquipmentQuality_" + slots.Count;
            slots.Add(slot);
        }

        for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
        {
            Transform slot = slots[slotIndex];
            RoleLoadoutEquipmentData equipped = slotIndex < equipmentCount ? equipment[slotIndex] : null;
            if (equipped == null)
            {
                slot.gameObject.SetActive(false);
                continue;
            }

            Image qualityFrame = slot.GetComponent<Image>();
            if (qualityFrame != null)
            {
                qualityFrame.sprite = GetEquipmentQualityBackground(equipped.quality);
                qualityFrame.enabled = qualityFrame.sprite != null;
            }

            Image icon = FindSceneComponent<Image>(slot, "Equipment");
            Sprite sprite = ResolveRoleLoadoutEquipmentSprite(equipped);
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }
            slot.gameObject.SetActive(true);
        }
    }

    private Sprite ResolveRoleLoadoutEquipmentSprite(RoleLoadoutEquipmentData equipment)
    {
        if (equipment == null)
        {
            return null;
        }

        string[] keys = { equipment.icon, equipment.equipmentKey, equipment.name, equipment.modelKey };
        for (int index = 0; index < keys.Length; index++)
        {
            Sprite sprite = ResolveEquipmentSprite(keys[index]);
            if (sprite != null)
            {
                return sprite;
            }
        }

        InventoryItemResponse item = new InventoryItemResponse
        {
            equipmentKey = equipment.equipmentKey,
            equipmentType = equipment.equipmentType,
            quality = equipment.quality,
            name = equipment.name,
            icon = equipment.icon,
            modelKey = equipment.modelKey,
            level = equipment.level,
            playerEquipmentId = equipment.playerEquipmentId
        };
        return ResolveEquipmentSprite(item);
    }

    private string BuildRoleUpgradePreviewText(RoleDisplayEntry entry)
    {
        if (entry == null || entry.player == null)
        {
            return "升级预览  未获得角色";
        }

        int currentLevel = Mathf.Max(1, entry.player.level);
        int maxLevel = entry.catalog.maxLevel > 0 ? entry.catalog.maxLevel : 100;
        if (currentLevel >= maxLevel)
        {
            return "升级预览  已达到服务器等级上限";
        }

        if (roleAutoUpgradePreviewNpcId != entry.player.playerNpcId)
        {
            return "升级预览  等待服务器返回...";
        }

        if (roleAutoUpgradePreviewRunning)
        {
            return "升级预览  正在读取服务器升级方案...";
        }

        if (!string.IsNullOrEmpty(roleAutoUpgradePreviewError))
        {
            return "升级预览  " + roleAutoUpgradePreviewError;
        }

        if (roleAutoUpgradePreview == null)
        {
            return "升级预览  暂无服务器升级方案";
        }

        string preview = $"升级预览  Lv.{roleAutoUpgradePreview.currentLevel} → Lv.{roleAutoUpgradePreview.payableTargetLevel} / 目标 Lv.{roleAutoUpgradePreview.requestedTargetLevel}";
        if (!roleAutoUpgradePreview.targetReached && !string.IsNullOrEmpty(roleAutoUpgradePreview.stopReason))
        {
            preview += "  " + roleAutoUpgradePreview.stopReason;
        }

        if (roleAutoUpgradePreview.blockedRequirements != null
            && roleAutoUpgradePreview.blockedRequirements.Length > 0)
        {
            RoleAutoUpgradeBlockedRequirementResponse requirement = roleAutoUpgradePreview.blockedRequirements[0];
            if (requirement != null)
            {
                string requirementMessage = string.IsNullOrEmpty(requirement.message)
                    ? requirement.requirementType
                    : requirement.message;
                preview += "  " + requirementMessage;
            }
        }

        if (roleAutoUpgradePreview.upgradeSteps != null
            && roleAutoUpgradePreview.upgradeSteps.Length > 0)
        {
            RoleAutoUpgradeStepResponse lastStep = roleAutoUpgradePreview.upgradeSteps[roleAutoUpgradePreview.upgradeSteps.Length - 1];
            if (lastStep != null)
            {
                preview += $"\n预计属性 生命 {GetRoleStepAfterHealth(lastStep)}  攻击 {GetRoleStepAfterAttack(lastStep)}  防御 {GetRoleStepAfterDefense(lastStep)}  速度 {GetRoleStepAfterSpeed(lastStep)}";
            }
        }

        return preview;
    }

    private static long GetRoleStepAfterHealth(RoleAutoUpgradeStepResponse step)
    {
        return step.afterHealth != 0 || step.afterHealthBonus == 0 ? step.afterHealth : step.afterHealthBonus;
    }

    private static long GetRoleStepAfterAttack(RoleAutoUpgradeStepResponse step)
    {
        return step.afterAttack != 0 || step.afterAttackBonus == 0 ? step.afterAttack : step.afterAttackBonus;
    }

    private static long GetRoleStepAfterDefense(RoleAutoUpgradeStepResponse step)
    {
        return step.afterDefense != 0 || step.afterDefenseBonus == 0 ? step.afterDefense : step.afterDefenseBonus;
    }

    private static long GetRoleStepAfterSpeed(RoleAutoUpgradeStepResponse step)
    {
        return step.afterSpeed != 0 || step.afterSpeedBonus == 0 ? step.afterSpeed : step.afterSpeedBonus;
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
    private sealed class RoleSpriteBinding
    {
        public string key;
        public Sprite sprite;
    }

    private sealed class RolePanelLoadState
    {
        public string token;
        public bool catalogCompleted;
        public bool playerCompleted;
        public string catalogError;
        public string playerError;
        public RoleCatalogEntry[] catalogEntries;
        public RolePlayerEntry[] playerEntries;
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
        public string quality;
        public string icon;
        public string modelKey;
        public string description;
        public string combatAttribute;
        public int maxLevel;
        public int level;
        public string state;
        public long settledPlayerBuildingId;
        public long settledSceneId;
        public long settledPlayerLandId;
        public float produceBonusRate;
    }

    [Serializable]
    private sealed class RoleUpgradeApiResponse : BasicApiResponse
    {
        public RoleUpgradeData data;
    }

    [Serializable]
    private sealed class RoleGrowthInventoryResponse : BasicApiResponse
    {
        public RoleGrowthItem[] data;
    }

    [Serializable]
    private sealed class RoleGrowthItem
    {
        public string itemKey;
        public string name;
        public string icon;
        public string quality;
        public long quantity;
    }

    [Serializable]
    private sealed class RoleBreakthroughPreviewApiResponse : BasicApiResponse
    {
        public RoleBreakthroughPreviewData data;
    }

    [Serializable]
    private sealed class RoleBreakthroughPreviewData
    {
        public int contractVersion;
        public long playerNpcId;
        public int currentLevel;
        public int targetLevel;
        public long goldCost;
        public long availableGold;
        public long goldBalance;
        public long fragmentCost;
        public string fragmentItemKey;
        public string fragmentItemName;
        public long availableFragments;
        public long fragmentBalance;
        public string planFingerprint;
    }

    private sealed class RoleBreakthroughCost
    {
        public string key;
        public string name;
        public long required;
        public long available;
    }

    private sealed class RoleGrowthRequest
    {
        public int playerId;
        public long playerNpcId;
        public int currentLevel;
        public int targetLevel;
        public string path;
        public string json;
        public string idempotencyKey;
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
        public int upgradeLevelCount;
        public bool targetReached;
        public string outcome;
        public string stopReason;
        public int blockedLevel;
        public RoleAutoUpgradeBlockedRequirementResponse[] blockedRequirements;
        public string planFingerprint;
        public RoleAutoUpgradeStepResponse[] upgradeSteps;
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
        public long npcId;
        public string npcCode;
        public string name;
        public int contractVersion;
        public string requestNo;
        public int fromLevel;
        public int reachedLevel;
        public int level;
        public int upgradedLevelCount;
        public string outcome;
        public bool targetReached;
        public long totalGoldCost;
        public long remainingGold;
        public long fragmentCost;
        public string fragmentItemKey;
        public long remainingFragments;
        public long health;
        public long attack;
        public long defense;
        public long speed;
        public string businessTime;
        public RoleAutoUpgradeStepResponse[] upgradeSteps;
    }

    [Serializable]
    private sealed class RoleUpgradeData
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
    private sealed class RoleAutoUpgradeBlockedRequirementResponse
    {
        public string requirementType;
        public string itemKey;
        public long itemId;
        public long requiredAmount;
        public long availableAmount;
        public string message;
    }

    [Serializable]
    private sealed class RoleAutoUpgradeStepResponse
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
        public long goldCost = -1;
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
        public RoleAutoUpgradeBlockedRequirementResponse[] requirements;
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

    [Serializable]
    private sealed class RoleCombatProfilesApiResponse : BasicApiResponse
    {
        public RoleCombatProfile[] data;
    }

    [Serializable]
    private sealed class RoleCombatProfile
    {
        public long player_npc_id;
        public long playerNpcId;
        public long definition_id;
        public long definitionId;
        public int level;
        public long max_health;
        public long maxHealth;
        public long attack;
        public long defense;
        public long speed;

        public long PlayerNpcId => playerNpcId > 0 ? playerNpcId : player_npc_id;
        public long DefinitionId => definitionId > 0 ? definitionId : definition_id;
        public long Health => maxHealth > 0 ? maxHealth : max_health;
    }

    [Serializable]
    private sealed class RoleLoadoutApiResponse : BasicApiResponse
    {
        public RoleLoadoutData data;
    }

    [Serializable]
    private sealed class RoleLoadoutData
    {
        public long playerNpcId;
        public long npcId;
        public int npcLevel;
        public long baseHealth;
        public long baseAttack;
        public long baseDefense;
        public long baseSpeed;
        public long equipmentHealthBonus;
        public long equipmentAttackBonus;
        public long equipmentDefenseBonus;
        public long equipmentSpeedBonus;
        public long totalHealth;
        public long totalAttack;
        public long totalDefense;
        public long totalSpeed;
        public RoleLoadoutEquipmentData[] equipment;
    }

    [Serializable]
    private sealed class RoleLoadoutEquipmentData
    {
        public long relationId;
        public long playerEquipmentId;
        public long equipmentId;
        public string equipmentKey;
        public string name;
        public string equipmentType;
        public string quality;
        public string icon;
        public string modelKey;
        public int level;
        public bool locked;
        public long healthBonus;
        public long attackBonus;
        public long defenseBonus;
        public long speedBonus;
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
