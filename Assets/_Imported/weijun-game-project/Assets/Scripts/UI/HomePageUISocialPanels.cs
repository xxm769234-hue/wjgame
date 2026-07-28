using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public partial class HomePageUI
{
    private const string GuestIdentifierKey = "GuestProfileIdentifier";
    private const string GuestNicknameKey = "GuestNickname";
    private const string LocalFriendStoreKey = "HomeLocalFriendStoreV1";
    private const int FriendsPerPage = 5;

    private GameObject guestProfilePanelObject;
    private GameObject friendsPanelObject;
    private LocalFriendStore localFriendStore;
    private LocalFriendStore serverFriendStore = new LocalFriendStore();
    private ServerDailyGiftStatus serverDailyGiftStatus;
    private bool profileRefreshRunning;
    private bool profileSwitchLoginConfirmation;
    private bool friendDeleteConfirmation;
    private bool friendsOnlineOnly;
    private int friendsPageIndex;
    private string friendsSearchQuery = string.Empty;
    private string selectedFriendId = string.Empty;
    private string friendsStatusMessage = string.Empty;
    private bool serverFriendsLoading;
    private bool serverFriendMutationRunning;

    private void ShowGuestProfilePanel()
    {
        HideFriendsPanel();
        HidePanelsBehindSocialOverlay();

        HomePageScenePanel panel = OpenRegisteredScenePanel(HomePageScenePanelType.GuestProfile);
        if (panel == null)
        {
            bool isGuest = !HasAuthSession();
            SetLeftContent(
                isGuest ? "游客信息" : "个人信息",
                BuildProfileFallbackText(isGuest),
                "个人档案场景组件未找到");
            return;
        }

        guestProfilePanelObject = panel.gameObject;
        profileSwitchLoginConfirmation = false;
        RefreshGuestProfileScenePanel(
            panel.PanelRoot,
            HasAuthSession() ? "账号资料来自当前登录存档" : "游客资料已从本地存档读取");
        SetStatus("已打开个人档案");
    }

    private void RefreshGuestProfileScenePanel(Transform root, string status)
    {
        if (root == null)
        {
            return;
        }

        bool isAuthenticated = HasAuthSession();
        string nickname = PlayerPrefs.GetString("PlayerNickname", isAuthenticated ? "玩家" : "游客");
        string identifier = GetCurrentProfileIdentifier();
        string username = PlayerPrefs.GetString("LoginUsername", isAuthenticated ? string.Empty : "Guest");

        SetSceneText(root, "ProfileTitle", isAuthenticated ? "个人信息" : "游客信息");
        SetSceneText(root, "ProfileModeBadge", isAuthenticated ? "账号登录" : "游客模式");
        SetSceneText(root, "ProfileAvatarInitial", GetNicknameInitial(nickname));
        SetSceneText(root, "ProfileDisplayName", nickname);
        SetSceneText(root, "ProfileIdentifier", $"玩家编号  {identifier}");
        SetSceneText(root, "ProfileAccount", $"登录账号  {(string.IsNullOrEmpty(username) ? "未设置" : username)}");
        SetSceneText(root, "ProfileLevel", $"领主等级  Lv.{PlayerPrefs.GetInt("PlayerLv", 1)}");
        SetSceneText(root, "ProfileGold", PlayerPrefs.GetInt("Gold", 1000).ToString());
        SetSceneText(root, "ProfileWood", PlayerPrefs.GetInt("Wood", 500).ToString());
        SetSceneText(root, "ProfileStone", PlayerPrefs.GetInt("Stone", 500).ToString());
        SetSceneText(root, "ProfileIron", PlayerPrefs.GetInt("Iron", 300).ToString());
        SetSceneText(root, "ProfileVital", $"生命  {PlayerPrefs.GetInt("PlayerHp", 1000)}    体力  {PlayerPrefs.GetInt("PlayerEnergy", 100)}");
        SetSceneText(root, "ProfileSignStat", $"签到天数\n{PlayerPrefs.GetInt("DailySignDays", 0)} 天");
        SetSceneText(root, "ProfileFriendStat", $"好友数量\n{LoadLocalFriendStore().friends.Count} 人");
        SetSceneText(root, "ProfileExploreStat", $"领地探索\n{PlayerPrefs.GetInt("TaskMapEntered", 0)} 次");
        SetSceneText(root, "ProfileSyncText", GetProfileSyncText(isAuthenticated));
        SetSceneText(root, "ProfileStatus", status);
        SetSceneText(root, "ProfileNicknameHint", isAuthenticated ? "账号昵称由服务器资料管理" : "昵称长度为 2-12 个字符");

        InputField nicknameInput = FindSceneComponent<InputField>(root, "ProfileNicknameInput");
        if (nicknameInput != null)
        {
            nicknameInput.SetTextWithoutNotify(nickname);
            nicknameInput.characterLimit = 12;
            nicknameInput.readOnly = isAuthenticated;
            nicknameInput.interactable = !profileRefreshRunning;
        }

        Button saveButton = FindSceneComponent<Button>(root, "ProfileSaveNicknameButton");
        saveButton.interactable = !isAuthenticated && !profileRefreshRunning;
        SetSceneButtonText(saveButton, isAuthenticated ? "账号昵称" : "保存昵称");
        BindSceneButton(saveButton, () => SaveGuestNickname(root));

        Button refreshButton = FindSceneComponent<Button>(root, "ProfileRefreshButton");
        refreshButton.interactable = isAuthenticated && !profileRefreshRunning;
        SetSceneButtonText(refreshButton, profileRefreshRunning ? "同步中..." : (isAuthenticated ? "刷新资料" : "本地存档"));
        BindSceneButton(refreshButton, () => StartCoroutine(RefreshCurrentUserProfile(root)));

        Button switchButton = FindSceneComponent<Button>(root, "ProfileSwitchLoginButton");
        SetSceneButtonText(switchButton, profileSwitchLoginConfirmation ? "确认切换账号" : "切换账号");
        BindSceneButton(switchButton, () => HandleProfileSwitchLogin(root));
        BindSceneButton(FindSceneComponent<Button>(root, "ProfileCopyIdButton"), () => CopyProfileIdentifier(root));
        BindSceneButton(FindSceneComponent<Button>(root, "ProfileCloseButton"), HideGuestProfilePanel);
    }

    private void SaveGuestNickname(Transform root)
    {
        if (HasAuthSession())
        {
            RefreshGuestProfileScenePanel(root, "账号昵称请通过服务端资料接口修改");
            return;
        }

        InputField input = FindSceneComponent<InputField>(root, "ProfileNicknameInput");
        string nickname = input != null ? input.text.Trim() : string.Empty;
        if (nickname.Length < 2 || nickname.Length > 12)
        {
            RefreshGuestProfileScenePanel(root, "昵称需要保持在 2-12 个字符之间");
            return;
        }

        if (nickname.IndexOfAny(new[] { '\r', '\n', '\t' }) >= 0)
        {
            RefreshGuestProfileScenePanel(root, "昵称不能包含换行或制表符");
            return;
        }

        PlayerPrefs.SetString(GuestNicknameKey, nickname);
        PlayerPrefs.SetString("PlayerNickname", nickname);
        PlayerPrefs.Save();
        profileSwitchLoginConfirmation = false;
        UpdateHomeAvatarNickname(nickname);
        RefreshGuestProfileScenePanel(root, "游客昵称已保存");
        SetStatus("游客昵称已更新");
    }

    private void CopyProfileIdentifier(Transform root)
    {
        GUIUtility.systemCopyBuffer = GetCurrentProfileIdentifier();
        profileSwitchLoginConfirmation = false;
        RefreshGuestProfileScenePanel(root, "玩家编号已复制");
    }

    private void HandleProfileSwitchLogin(Transform root)
    {
        if (!profileSwitchLoginConfirmation)
        {
            profileSwitchLoginConfirmation = true;
            RefreshGuestProfileScenePanel(root, "再次点击“确认切换账号”返回登录界面");
            return;
        }

        ReturnToLogin();
    }

    private IEnumerator RefreshCurrentUserProfile(Transform root)
    {
        if (profileRefreshRunning || !HasAuthSession())
        {
            yield break;
        }

        profileRefreshRunning = true;
        profileSwitchLoginConfirmation = false;
        RefreshGuestProfileScenePanel(root, "正在同步服务器资料...");

        UnityWebRequest request = CreateAuthorizedGet("/user/me");
        if (request == null)
        {
            profileRefreshRunning = false;
            RefreshGuestProfileScenePanel(root, requestCreationError);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();

            if (HandleUnauthorizedResponse(request))
            {
                profileRefreshRunning = false;
                yield break;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                profileRefreshRunning = false;
                RefreshGuestProfileScenePanel(root, $"资料同步失败：{ApiRequestUtility.GetErrorMessage(request, "请求失败")}");
                yield break;
            }

            UserProfileApiResponse response;
            try
            {
                response = JsonUtility.FromJson<UserProfileApiResponse>(request.downloadHandler.text);
            }
            catch (Exception)
            {
                profileRefreshRunning = false;
                RefreshGuestProfileScenePanel(root, "服务器资料格式无法解析");
                yield break;
            }

            if (response == null || response.code != 200 || response.data == null)
            {
                profileRefreshRunning = false;
                RefreshGuestProfileScenePanel(root, response == null ? "服务器未返回资料" : GetApiError(response));
                yield break;
            }

            SaveUserProfile(response.data);
            profileRefreshRunning = false;
            RefreshGuestProfileScenePanel(root, "服务器资料同步完成");
            SetStatus("个人资料已刷新");
        }
    }

    private void SaveUserProfile(UserProfileData profile)
    {
        PlayerPrefs.SetInt(PlayerIdKey, profile.id);
        PlayerPrefs.SetString("LoginUsername", profile.username ?? string.Empty);
        PlayerPrefs.SetString("PlayerNickname", profile.nickname ?? string.Empty);
        PlayerPrefs.SetInt("PlayerLv", profile.level);
        PlayerPrefs.SetString("PlayerTotalExperience", Math.Max(0L, profile.totalExperience).ToString());
        PlayerPrefs.SetInt("PlayerLevelCurveRevision", Mathf.Max(0, profile.levelCurveRevision));
        PlayerPrefs.SetInt("PlayerCurrentLevelExperience", Mathf.Max(0, profile.currentLevelExperience));
        PlayerPrefs.SetInt("PlayerRequiredExperience", Mathf.Max(0, profile.requiredExperienceForNextLevel));
        PlayerPrefs.SetInt("PlayerExperienceProgressBps", Mathf.Clamp(profile.experienceProgressBps, 0, 10000));
        PlayerPrefs.SetInt("PlayerMaxLevel", profile.maxLevel ? 1 : 0);
        PlayerPrefs.SetInt("PlayerEconomyFrozen", profile.economyFrozen ? 1 : 0);
        PlayerPrefs.SetInt("Gold", profile.gold);
        PlayerPrefs.SetInt("Wood", profile.wood);
        PlayerPrefs.SetInt("Stone", profile.stone);
        PlayerPrefs.SetInt("Iron", profile.iron);
        PlayerPrefs.SetInt("Crystal", Mathf.Max(0, profile.diamond));
        PlayerPrefs.SetString("ProfileLastSync", DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        PlayerPrefs.Save();
        WeChatPlatformBridge.SyncFriendLevel(profile.level);
        SyncInventoryResourceValues(profile.items);
        RefreshHomeResourceMetrics();
        UpdateHomeAvatarNickname(profile.nickname);
    }

    private string GetCurrentProfileIdentifier()
    {
        if (HasAuthSession())
        {
            return PlayerPrefs.GetInt(PlayerIdKey, 0).ToString();
        }

        string identifier = PlayerPrefs.GetString(GuestIdentifierKey, string.Empty);
        if (!string.IsNullOrEmpty(identifier))
        {
            return identifier;
        }

        identifier = "WJ-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
        PlayerPrefs.SetString(GuestIdentifierKey, identifier);
        PlayerPrefs.Save();
        return identifier;
    }

    private static string GetNicknameInitial(string nickname)
    {
        return string.IsNullOrEmpty(nickname) ? "游" : nickname.Substring(0, 1);
    }

    private static string GetProfileSyncText(bool isAuthenticated)
    {
        if (!isAuthenticated)
        {
            return "资料来源  本地游客存档";
        }

        string lastSync = PlayerPrefs.GetString("ProfileLastSync", string.Empty);
        return string.IsNullOrEmpty(lastSync) ? "资料来源  当前登录缓存" : $"最近同步  {lastSync}";
    }

    private string BuildProfileFallbackText(bool isGuest)
    {
        return
            $"昵称：{PlayerPrefs.GetString("PlayerNickname", isGuest ? "游客" : "玩家")}\n" +
            $"账号：{PlayerPrefs.GetString("LoginUsername", isGuest ? "Guest" : string.Empty)}\n" +
            $"状态：{(isGuest ? "游客模式" : "账号登录")}\n" +
            $"等级：Lv.{PlayerPrefs.GetInt("PlayerLv", 1)}\n\n" +
            $"金币：{PlayerPrefs.GetInt("Gold", 1000)}\n" +
            $"木材：{PlayerPrefs.GetInt("Wood", 500)}\n" +
            $"石材：{PlayerPrefs.GetInt("Stone", 500)}\n" +
            $"铁矿：{PlayerPrefs.GetInt("Iron", 300)}";
    }

    private void UpdateHomeAvatarNickname(string nickname)
    {
        if (mainPageTransform == null || string.IsNullOrEmpty(nickname))
        {
            return;
        }

        Text[] labels = mainPageTransform.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i].name == "AvatarName")
            {
                labels[i].text = nickname;
            }
        }
    }

    private void HideGuestProfilePanel()
    {
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.GuestProfile);
        if (panel != null)
        {
            panel.SetVisible(false);
        }

        guestProfilePanelObject = null;
        profileSwitchLoginConfirmation = false;
    }

    private void ShowFriendsPanel()
    {
        HideGuestProfilePanel();
        HidePanelsBehindSocialOverlay();

        HomePageScenePanel panel = OpenRegisteredScenePanel(HomePageScenePanelType.Friends);
        if (panel == null)
        {
            SetLeftContent("好友列表", "好友面板场景组件未找到。", "无法打开好友列表");
            return;
        }

        friendsPanelObject = panel.gameObject;
        friendDeleteConfirmation = false;
        ResetWeChatFriendGiftCatalog();
        if (HasAuthSession())
        {
            friendsOnlineOnly = false;
            serverFriendStore = new LocalFriendStore();
            serverDailyGiftStatus = null;
            RefreshFriendsScenePanel(panel.PanelRoot, "正在同步服务器好友...");
            StartCoroutine(GetServerFriends(panel.PanelRoot));
        }
        else
        {
            RefreshFriendsScenePanel(panel.PanelRoot, "游客好友名册已打开");
        }
        SetStatus("已打开好友列表");
    }

    private void RefreshFriendsScenePanel(Transform root, string status)
    {
        if (root == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(status))
        {
            friendsStatusMessage = status;
        }

        LocalFriendStore store = LoadLocalFriendStore();
        List<LocalFriendData> filtered = GetFilteredFriends(store.friends);
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(filtered.Count / (float)FriendsPerPage));
        friendsPageIndex = Mathf.Clamp(friendsPageIndex, 0, pageCount - 1);
        int firstIndex = friendsPageIndex * FriendsPerPage;
        int visibleCount = Mathf.Min(FriendsPerPage, Mathf.Max(0, filtered.Count - firstIndex));

        LocalFriendData selected = FindFriendById(selectedFriendId);
        int selectedIndex = selected != null ? filtered.IndexOf(selected) : -1;
        if (selectedIndex < firstIndex || selectedIndex >= firstIndex + visibleCount)
        {
            selected = visibleCount > 0 ? filtered[firstIndex] : null;
            selectedFriendId = selected != null ? selected.id : string.Empty;
            friendDeleteConfirmation = false;
        }

        bool serverBacked = HasAuthSession();
        int onlineCount = 0;
        for (int i = 0; i < store.friends.Count; i++)
        {
            if (store.friends[i].online)
            {
                onlineCount++;
            }
        }

        string countText = serverBacked
            ? BuildServerFriendCountText(filtered.Count, store.friends.Count)
            : $"好友 {filtered.Count}/{store.friends.Count}    在线 {onlineCount}";
        SetSceneText(root, "FriendsCount", countText);
        SetSceneText(root, "FriendsPageText", $"{friendsPageIndex + 1} / {pageCount}");
        SetSceneText(root, "FriendsStatus", friendsStatusMessage);

        InputField searchInput = FindSceneComponent<InputField>(root, "FriendsSearchInput");
        if (searchInput != null)
        {
            searchInput.SetTextWithoutNotify(friendsSearchQuery);
            searchInput.onValueChanged.RemoveAllListeners();
            searchInput.onValueChanged.AddListener(value =>
            {
                friendsSearchQuery = value.Trim();
                friendsPageIndex = 0;
                friendDeleteConfirmation = false;
                RefreshFriendsScenePanel(root, null);
            });
        }

        InputField addInput = FindSceneComponent<InputField>(root, "FriendsAddInput");
        if (addInput != null)
        {
            addInput.characterLimit = 12;
            if (addInput.placeholder is Text placeholder)
            {
                placeholder.text = serverBacked ? "输入玩家ID" : "输入好友昵称";
            }
            addInput.interactable = !serverFriendMutationRunning;
        }

        ConfigureFriendFilterButton(FindSceneComponent<Button>(root, "FriendsFilterAll"), !friendsOnlineOnly);
        ConfigureFriendFilterButton(FindSceneComponent<Button>(root, "FriendsFilterOnline"), friendsOnlineOnly);
        Button onlineFilter = FindSceneComponent<Button>(root, "FriendsFilterOnline");
        if (onlineFilter != null)
        {
            onlineFilter.interactable = !serverBacked;
            SetSceneButtonText(onlineFilter, serverBacked ? "服务器" : "在线");
        }
        BindSceneButton(FindSceneComponent<Button>(root, "FriendsFilterAll"), () => SetFriendFilter(root, false));
        BindSceneButton(onlineFilter, serverBacked ? null : (Action)(() => SetFriendFilter(root, true)));
        BindSceneButton(FindSceneComponent<Button>(root, "FriendsClearSearchButton"), () => ClearFriendSearch(root));
        BindSceneButton(FindSceneComponent<Button>(root, "FriendsAddButton"), () => AddLocalFriend(root));
        BindSceneButton(FindSceneComponent<Button>(root, "FriendsCloseButton"), HideFriendsPanel);
        ConfigureWeChatFriendGiftButton(root);

        Button previousButton = FindSceneComponent<Button>(root, "FriendsPreviousButton");
        Button nextButton = FindSceneComponent<Button>(root, "FriendsNextButton");
        if (previousButton != null)
        {
            previousButton.interactable = friendsPageIndex > 0 && !serverFriendsLoading;
        }
        if (nextButton != null)
        {
            nextButton.interactable = friendsPageIndex < pageCount - 1 && !serverFriendsLoading;
        }
        BindSceneButton(previousButton, () => ChangeFriendsPage(root, -1));
        BindSceneButton(nextButton, () => ChangeFriendsPage(root, 1));

        for (int rowIndex = 0; rowIndex < FriendsPerPage; rowIndex++)
        {
            Transform row = FindSceneTransform(root, "FriendRow" + rowIndex);
            bool visible = rowIndex < visibleCount;
            if (row == null)
            {
                continue;
            }

            row.gameObject.SetActive(visible);
            if (!visible)
            {
                continue;
            }

            LocalFriendData friend = filtered[firstIndex + rowIndex];
            bool isSelected = friend.id == selectedFriendId;
            Image rowBackground = row.GetComponent<Image>();
            if (rowBackground != null)
            {
                rowBackground.color = isSelected
                    ? new Color(1f, 0.80f, 0.42f, 1f)
                    : new Color(1f, 0.95f, 0.78f, 1f);
            }

            Image avatarBackground = FindSceneComponent<Image>(row, "FriendRowAvatarBackground" + rowIndex);
            if (avatarBackground != null)
            {
                avatarBackground.color = GetFriendAvatarColor(friend.id);
            }

            Image onlineMark = FindSceneComponent<Image>(row, "FriendRowOnlineMark" + rowIndex);
            if (onlineMark != null)
            {
                onlineMark.color = friend.online
                    ? new Color(0.22f, 0.70f, 0.34f, 1f)
                    : new Color(0.58f, 0.55f, 0.50f, 1f);
            }

            SetSceneText(row, "FriendRowAvatar" + rowIndex, GetNicknameInitial(friend.nickname));
            SetSceneText(row, "FriendRowName" + rowIndex, friend.nickname);
            SetSceneText(
                row,
                "FriendRowMeta" + rowIndex,
                friend.serverBacked ? $"Lv.{friend.level}    好友自 {friend.friendsSince}" : $"Lv.{friend.level}    亲密度 {friend.intimacy}");
            SetSceneText(
                row,
                "FriendRowState" + rowIndex,
                friend.serverBacked ? (friend.dailyGiftSent ? "今日已赠" : "今日可赠") : (friend.online ? "在线" : friend.lastSeen));
            Button rowButton = row.GetComponent<Button>();
            string friendId = friend.id;
            BindSceneButton(rowButton, () => SelectFriend(root, friendId));
        }

        RefreshFriendDetail(root, selected);
    }

    private void RefreshFriendDetail(Transform root, LocalFriendData friend)
    {
        bool hasFriend = friend != null;
        SetSceneObjectActive(root, "FriendDetailEmpty", !hasFriend);
        SetSceneObjectActive(root, "FriendDetailContent", hasFriend);
        if (!hasFriend)
        {
            return;
        }

        Image avatar = FindSceneComponent<Image>(root, "FriendDetailAvatarBackground");
        if (avatar != null)
        {
            avatar.color = GetFriendAvatarColor(friend.id);
        }

        SetSceneText(root, "FriendDetailAvatar", GetNicknameInitial(friend.nickname));
        SetSceneText(root, "FriendDetailName", friend.nickname);
        SetSceneText(root, "FriendDetailId", $"好友编号  {friend.id}");
        SetSceneText(root, "FriendDetailLevel", $"领主等级  Lv.{friend.level}");
        SetSceneText(
            root,
            "FriendDetailOnline",
            friend.serverBacked ? $"成为好友：{friend.friendsSince}" : (friend.online ? "当前在线" : $"离线 · {friend.lastSeen}"));
        SetSceneText(
            root,
            "FriendDetailIntimacy",
            friend.serverBacked ? (friend.dailyGiftReceived ? "今日已收到该好友赠礼" : "今日尚未收到该好友赠礼") : $"亲密度  {friend.intimacy}");

        bool giftedToday = friend.serverBacked ? friend.dailyGiftSent : friend.lastGiftDate == GetFriendDate();
        bool giftEnabled = !friend.serverBacked || (serverDailyGiftStatus != null && serverDailyGiftStatus.enabled);
        string giftHint = friend.serverBacked
            ? BuildServerFriendGiftHint(friend)
            : (giftedToday ? "今日体力已经赠送" : "今日可赠送 5 点体力");
        SetSceneText(root, "FriendGiftHint", giftHint);
        Button giftButton = FindSceneComponent<Button>(root, "FriendsGiftButton");
        if (giftButton != null)
        {
            giftButton.interactable = !giftedToday && giftEnabled && !serverFriendMutationRunning;
        }
        SetSceneButtonText(giftButton, giftedToday ? "今日已赠" : (friend.serverBacked ? "发送赠礼" : "赠送体力"));
        BindSceneButton(giftButton, () => GiftFriendStamina(root, friend.id));

        Button deleteButton = FindSceneComponent<Button>(root, "FriendsDeleteButton");
        if (deleteButton != null)
        {
            deleteButton.interactable = !serverFriendMutationRunning;
        }
        SetSceneButtonText(deleteButton, friendDeleteConfirmation ? "确认删除" : "删除好友");
        BindSceneButton(deleteButton, () => RequestDeleteFriend(root, friend.id));
    }

    private void SetFriendFilter(Transform root, bool onlineOnly)
    {
        friendsOnlineOnly = onlineOnly;
        friendsPageIndex = 0;
        friendDeleteConfirmation = false;
        RefreshFriendsScenePanel(root, onlineOnly ? "已筛选在线好友" : "已显示全部好友");
    }

    private void ClearFriendSearch(Transform root)
    {
        friendsSearchQuery = string.Empty;
        friendsPageIndex = 0;
        friendDeleteConfirmation = false;
        RefreshFriendsScenePanel(root, "搜索条件已清除");
    }

    private void ChangeFriendsPage(Transform root, int offset)
    {
        friendsPageIndex += offset;
        friendDeleteConfirmation = false;
        RefreshFriendsScenePanel(root, "已切换好友列表页");
    }

    private void SelectFriend(Transform root, string friendId)
    {
        selectedFriendId = friendId;
        friendDeleteConfirmation = false;
        RefreshFriendsScenePanel(root, "已选择好友");
    }

    private void AddLocalFriend(Transform root)
    {
        InputField input = FindSceneComponent<InputField>(root, "FriendsAddInput");
        string nickname = input != null ? input.text.Trim() : string.Empty;
        if (HasAuthSession())
        {
            if (!int.TryParse(nickname, out int friendPlayerId) || friendPlayerId <= 0)
            {
                RefreshFriendsScenePanel(root, "请输入正确的玩家 ID");
                return;
            }

            if (friendPlayerId == GameSession.PlayerId)
            {
                RefreshFriendsScenePanel(root, "不能添加自己为好友");
                return;
            }

            StartCoroutine(AddServerFriend(root, friendPlayerId));
            return;
        }

        if (nickname.Length < 2 || nickname.Length > 12)
        {
            RefreshFriendsScenePanel(root, "好友昵称需要保持在 2-12 个字符之间");
            return;
        }

        LocalFriendStore store = LoadLocalFriendStore();
        for (int i = 0; i < store.friends.Count; i++)
        {
            if (string.Equals(store.friends[i].nickname, nickname, StringComparison.OrdinalIgnoreCase))
            {
                selectedFriendId = store.friends[i].id;
                RefreshFriendsScenePanel(root, "该好友已经在列表中");
                return;
            }
        }

        LocalFriendData friend = new LocalFriendData
        {
            id = "WJ-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant(),
            nickname = nickname,
            level = 1,
            online = false,
            lastSeen = "刚刚添加",
            intimacy = 0,
            lastGiftDate = string.Empty
        };
        store.friends.Add(friend);
        SaveLocalFriendStore();

        selectedFriendId = friend.id;
        friendsSearchQuery = string.Empty;
        friendsOnlineOnly = false;
        friendsPageIndex = Mathf.Max(0, Mathf.CeilToInt(store.friends.Count / (float)FriendsPerPage) - 1);
        friendDeleteConfirmation = false;
        if (input != null)
        {
            input.SetTextWithoutNotify(string.Empty);
        }
        RefreshFriendsScenePanel(root, $"已添加好友：{nickname}");
    }

    private void GiftFriendStamina(Transform root, string friendId)
    {
        LocalFriendData friend = FindFriendById(friendId);
        if (friend == null)
        {
            RefreshFriendsScenePanel(root, "好友不存在，列表已刷新");
            return;
        }

        if (friend.serverBacked)
        {
            if (!int.TryParse(friend.id, out int friendPlayerId))
            {
                RefreshFriendsScenePanel(root, "好友玩家 ID 无效");
                return;
            }
            StartCoroutine(SendServerDailyFriendGift(root, friendPlayerId));
            return;
        }

        if (friend.lastGiftDate == GetFriendDate())
        {
            RefreshFriendsScenePanel(root, "今天已经向该好友赠送过体力");
            return;
        }

        friend.lastGiftDate = GetFriendDate();
        friend.intimacy += 3;
        SaveLocalFriendStore();
        PlayerPrefs.SetInt("FriendGiftCount", PlayerPrefs.GetInt("FriendGiftCount", 0) + 1);
        PlayerPrefs.Save();
        friendDeleteConfirmation = false;
        RefreshFriendsScenePanel(root, $"已向 {friend.nickname} 赠送 5 点体力");
    }

    private void RequestDeleteFriend(Transform root, string friendId)
    {
        LocalFriendData friend = FindFriendById(friendId);
        if (friend == null)
        {
            RefreshFriendsScenePanel(root, "好友不存在，列表已刷新");
            return;
        }

        if (!friendDeleteConfirmation)
        {
            friendDeleteConfirmation = true;
            RefreshFriendsScenePanel(root, $"再次点击“确认删除”移除 {friend.nickname}");
            return;
        }

        if (friend.serverBacked)
        {
            if (!int.TryParse(friend.id, out int friendPlayerId))
            {
                RefreshFriendsScenePanel(root, "好友玩家 ID 无效");
                return;
            }
            StartCoroutine(DeleteServerFriend(root, friendPlayerId, friend.nickname));
            return;
        }

        string nickname = friend.nickname;
        LoadLocalFriendStore().friends.Remove(friend);
        SaveLocalFriendStore();
        selectedFriendId = string.Empty;
        friendDeleteConfirmation = false;
        RefreshFriendsScenePanel(root, $"已删除好友：{nickname}");
    }

    private List<LocalFriendData> GetFilteredFriends(List<LocalFriendData> source)
    {
        List<LocalFriendData> filtered = new List<LocalFriendData>();
        for (int i = 0; i < source.Count; i++)
        {
            LocalFriendData friend = source[i];
            if (friendsOnlineOnly && !friend.online)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(friendsSearchQuery)
                && friend.nickname.IndexOf(friendsSearchQuery, StringComparison.OrdinalIgnoreCase) < 0
                && friend.id.IndexOf(friendsSearchQuery, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            filtered.Add(friend);
        }

        filtered.Sort((left, right) =>
        {
            if (HasAuthSession())
            {
                int levelOrder = right.level.CompareTo(left.level);
                if (levelOrder != 0)
                {
                    return levelOrder;
                }

                int.TryParse(left.id, out int leftId);
                int.TryParse(right.id, out int rightId);
                return leftId.CompareTo(rightId);
            }

            int onlineOrder = right.online.CompareTo(left.online);
            return onlineOrder != 0
                ? onlineOrder
                : string.Compare(left.nickname, right.nickname, StringComparison.OrdinalIgnoreCase);
        });
        return filtered;
    }

    private LocalFriendData FindFriendById(string friendId)
    {
        if (string.IsNullOrEmpty(friendId))
        {
            return null;
        }

        List<LocalFriendData> friends = LoadLocalFriendStore().friends;
        for (int i = 0; i < friends.Count; i++)
        {
            if (friends[i].id == friendId)
            {
                return friends[i];
            }
        }

        return null;
    }

    private LocalFriendStore LoadLocalFriendStore()
    {
        if (HasAuthSession())
        {
            return serverFriendStore ?? (serverFriendStore = new LocalFriendStore());
        }

        if (localFriendStore != null)
        {
            return localFriendStore;
        }

        string json = PlayerPrefs.GetString(LocalFriendStoreKey, string.Empty);
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                localFriendStore = JsonUtility.FromJson<LocalFriendStore>(json);
            }
            catch (Exception)
            {
                localFriendStore = null;
            }
        }

        if (localFriendStore == null || localFriendStore.friends == null)
        {
            localFriendStore = CreateDefaultFriendStore();
            SaveLocalFriendStore();
        }

        return localFriendStore;
    }

    private void SaveLocalFriendStore()
    {
        if (HasAuthSession())
        {
            return;
        }

        if (localFriendStore == null)
        {
            return;
        }

        PlayerPrefs.SetString(LocalFriendStoreKey, JsonUtility.ToJson(localFriendStore));
        PlayerPrefs.Save();
    }

    private static LocalFriendStore CreateDefaultFriendStore()
    {
        LocalFriendStore store = new LocalFriendStore();
        store.friends.Add(CreateDefaultFriend("WJ-1001", "林间小筑", 8, true, "在线", 36));
        store.friends.Add(CreateDefaultFriend("WJ-1002", "星火铁匠", 6, false, "12 分钟前", 28));
        store.friends.Add(CreateDefaultFriend("WJ-1003", "风铃商队", 11, true, "在线", 52));
        store.friends.Add(CreateDefaultFriend("WJ-1004", "青石守卫", 5, false, "1 小时前", 21));
        store.friends.Add(CreateDefaultFriend("WJ-1005", "麦穗", 3, true, "在线", 17));
        store.friends.Add(CreateDefaultFriend("WJ-1006", "雪原旅人", 9, false, "昨天", 43));
        store.friends.Add(CreateDefaultFriend("WJ-1007", "海港船长", 7, false, "2 天前", 31));
        return store;
    }

    private static LocalFriendData CreateDefaultFriend(string id, string nickname, int level, bool online, string lastSeen, int intimacy)
    {
        return new LocalFriendData
        {
            id = id,
            nickname = nickname,
            level = level,
            online = online,
            lastSeen = lastSeen,
            intimacy = intimacy,
            lastGiftDate = string.Empty
        };
    }

    private static string GetFriendDate()
    {
        return DateTime.Now.ToString("yyyy-MM-dd");
    }

    private static Color GetFriendAvatarColor(string id)
    {
        Color[] colors =
        {
            new Color(0.28f, 0.62f, 0.76f, 1f),
            new Color(0.88f, 0.48f, 0.30f, 1f),
            new Color(0.38f, 0.66f, 0.36f, 1f),
            new Color(0.64f, 0.42f, 0.72f, 1f),
            new Color(0.86f, 0.66f, 0.22f, 1f)
        };
        int hash = 0;
        if (!string.IsNullOrEmpty(id))
        {
            for (int i = 0; i < id.Length; i++)
            {
                hash = (hash * 31 + id[i]) & int.MaxValue;
            }
        }
        return colors[hash % colors.Length];
    }

    private static void ConfigureFriendFilterButton(Button button, bool selected)
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

        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.color = selected ? Color.white : new Color(0.36f, 0.20f, 0.10f, 1f);
        }
    }

    private void HideFriendsPanel()
    {
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Friends);
        if (panel != null)
        {
            panel.SetVisible(false);
        }

        friendsPanelObject = null;
        friendDeleteConfirmation = false;
    }

    private void HidePanelsBehindSocialOverlay()
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
        HideSettingsPanel();
    }

#pragma warning disable 0649
    [Serializable]
    private class UserProfileApiResponse : BasicApiResponse
    {
        public UserProfileData data;
    }

    [Serializable]
    private class UserProfileData
    {
        public int id;
        public string username;
        public string nickname;
        public int level;
        public long totalExperience;
        public int levelCurveRevision;
        public int currentLevelExperience;
        public int requiredExperienceForNextLevel;
        public int experienceProgressBps;
        public bool maxLevel;
        public bool economyFrozen;
        public int gold;
        public int wood;
        public int stone;
        public int iron;
        public int diamond;
        public InventoryItemResponse[] items;
    }

    private IEnumerator GetServerFriends(Transform root)
    {
        if (serverFriendsLoading)
        {
            yield break;
        }

        serverFriendsLoading = true;
        List<LocalFriendData> loadedFriends = new List<LocalFriendData>();
        int cursorLevel = 0;
        int cursorPlayerId = 0;
        bool hasCursor = false;
        bool hasMore = true;
        int pageGuard = 0;

        while (hasMore && pageGuard++ < 50)
        {
            string path = hasCursor
                ? $"/friends?cursorLevel={cursorLevel}&cursorPlayerId={cursorPlayerId}&limit=100"
                : "/friends?limit=100";
            UnityWebRequest request = CreateAuthorizedGet(path);
            if (request == null)
            {
                serverFriendsLoading = false;
                RefreshFriendsScenePanel(root, requestCreationError);
                yield break;
            }

            using (request)
            {
                yield return request.SendWebRequest();
                if (HandleUnauthorizedResponse(request))
                {
                    serverFriendsLoading = false;
                    yield break;
                }

                ServerFriendListApiResponse response = ParseJson<ServerFriendListApiResponse>(request.downloadHandler.text);
                if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
                {
                    serverFriendsLoading = false;
                    RefreshFriendsScenePanel(root, "好友同步失败：" + (response == null
                        ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                        : GetApiError(response)));
                    yield break;
                }

                if (response.data.items != null)
                {
                    for (int i = 0; i < response.data.items.Length; i++)
                    {
                        ServerFriendResponse item = response.data.items[i];
                        if (item != null)
                        {
                            loadedFriends.Add(ConvertServerFriend(item));
                        }
                    }
                }

                if (response.data.dailyGift != null)
                {
                    serverDailyGiftStatus = response.data.dailyGift;
                }

                hasMore = response.data.hasMore && response.data.nextCursor != null;
                if (hasMore)
                {
                    int nextLevel = response.data.nextCursor.level;
                    int nextPlayerId = response.data.nextCursor.playerId;
                    if (hasCursor && nextLevel == cursorLevel && nextPlayerId == cursorPlayerId)
                    {
                        hasMore = false;
                    }
                    else
                    {
                        cursorLevel = nextLevel;
                        cursorPlayerId = nextPlayerId;
                        hasCursor = true;
                    }
                }
            }
        }

        serverFriendStore = new LocalFriendStore { friends = loadedFriends };
        serverFriendsLoading = false;
        friendsPageIndex = Mathf.Clamp(friendsPageIndex, 0, Mathf.Max(0, Mathf.CeilToInt(loadedFriends.Count / (float)FriendsPerPage) - 1));
        RefreshFriendsScenePanel(root, $"服务器好友同步完成，共 {loadedFriends.Count} 人");
    }

    private IEnumerator AddServerFriend(Transform root, int friendPlayerId)
    {
        if (serverFriendMutationRunning)
        {
            yield break;
        }

        serverFriendMutationRunning = true;
        RefreshFriendsScenePanel(root, "正在添加服务器好友...");
        if (!TryCreateAuthorizedPost($"/friends/{friendPlayerId}", "{}", out UnityWebRequest request, out string error))
        {
            serverFriendMutationRunning = false;
            RefreshFriendsScenePanel(root, error);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                serverFriendMutationRunning = false;
                yield break;
            }

            ServerFriendMutationApiResponse response = ParseJson<ServerFriendMutationApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200)
            {
                serverFriendMutationRunning = false;
                RefreshFriendsScenePanel(root, "添加失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response)));
                yield break;
            }
        }

        serverFriendMutationRunning = false;
        selectedFriendId = friendPlayerId.ToString();
        InputField input = FindSceneComponent<InputField>(root, "FriendsAddInput");
        if (input != null)
        {
            input.SetTextWithoutNotify(string.Empty);
        }
        yield return GetServerFriends(root);
    }

    private IEnumerator DeleteServerFriend(Transform root, int friendPlayerId, string nickname)
    {
        if (serverFriendMutationRunning)
        {
            yield break;
        }

        serverFriendMutationRunning = true;
        RefreshFriendsScenePanel(root, "正在删除服务器好友...");
        if (!ApiRequestUtility.TryCreateJsonRequest(
            $"/friends/{friendPlayerId}",
            UnityWebRequest.kHttpVerbDELETE,
            string.Empty,
            true,
            out UnityWebRequest request,
            out string error))
        {
            serverFriendMutationRunning = false;
            RefreshFriendsScenePanel(root, error);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                serverFriendMutationRunning = false;
                yield break;
            }

            BasicApiResponse response = ParseJson<BasicApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200)
            {
                serverFriendMutationRunning = false;
                RefreshFriendsScenePanel(root, "删除失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response)));
                yield break;
            }
        }

        serverFriendMutationRunning = false;
        friendDeleteConfirmation = false;
        selectedFriendId = string.Empty;
        friendsPageIndex = 0;
        yield return GetServerFriends(root);
        RefreshFriendsScenePanel(root, $"已删除好友：{nickname}");
    }

    private IEnumerator SendServerDailyFriendGift(Transform root, int friendPlayerId)
    {
        if (serverFriendMutationRunning)
        {
            yield break;
        }

        serverFriendMutationRunning = true;
        RefreshFriendsScenePanel(root, "正在发送每日好友赠礼...");
        if (!TryCreateAuthorizedPost($"/friends/{friendPlayerId}/gifts/daily", "{}", out UnityWebRequest request, out string error))
        {
            serverFriendMutationRunning = false;
            RefreshFriendsScenePanel(root, error);
            yield break;
        }

        ServerDailyGiftSendResponse gift = null;
        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                serverFriendMutationRunning = false;
                yield break;
            }

            ServerDailyGiftApiResponse response = ParseJson<ServerDailyGiftApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                serverFriendMutationRunning = false;
                RefreshFriendsScenePanel(root, "赠礼发送失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response)));
                yield break;
            }
            gift = response.data;
        }

        serverFriendMutationRunning = false;
        friendDeleteConfirmation = false;
        yield return GetServerFriends(root);
        string replay = gift.replayed ? "（重复请求，未再次发放）" : string.Empty;
        RefreshFriendsScenePanel(root, $"赠礼已发送：{gift.itemKey} x{gift.quantity}{replay}");
    }

    private static LocalFriendData ConvertServerFriend(ServerFriendResponse item)
    {
        return new LocalFriendData
        {
            id = item.playerId.ToString(),
            nickname = string.IsNullOrEmpty(item.nickname) ? $"玩家 {item.playerId}" : item.nickname,
            level = Mathf.Max(1, item.level),
            online = false,
            lastSeen = string.Empty,
            intimacy = 0,
            lastGiftDate = string.Empty,
            serverBacked = true,
            friendsSince = item.friendsSince ?? string.Empty,
            dailyGiftSent = item.dailyGiftSent,
            dailyGiftReceived = item.dailyGiftReceived
        };
    }

    private string BuildServerFriendCountText(int filteredCount, int totalCount)
    {
        if (serverDailyGiftStatus == null)
        {
            return $"好友 {filteredCount}/{totalCount}    服务器同步";
        }

        return $"好友 {filteredCount}/{totalCount}    今日赠礼 {serverDailyGiftStatus.sentCount}/{serverDailyGiftStatus.dailySendLimit}";
    }

    private string BuildServerFriendGiftHint(LocalFriendData friend)
    {
        if (serverDailyGiftStatus == null || !serverDailyGiftStatus.enabled)
        {
            return "服务器每日好友赠礼当前未开启";
        }

        if (friend.dailyGiftSent)
        {
            return "今天已经向该好友发送过赠礼";
        }

        return $"系统赠送 {serverDailyGiftStatus.itemKey} x{serverDailyGiftStatus.quantity}，今日剩余 {serverDailyGiftStatus.remainingSendCount} 次";
    }

    [Serializable]
    private class LocalFriendStore
    {
        public List<LocalFriendData> friends = new List<LocalFriendData>();
    }

    [Serializable]
    private class LocalFriendData
    {
        public string id;
        public string nickname;
        public int level;
        public bool online;
        public string lastSeen;
        public int intimacy;
        public string lastGiftDate;
        public bool serverBacked;
        public string friendsSince;
        public bool dailyGiftSent;
        public bool dailyGiftReceived;
    }

    [Serializable]
    private class ServerFriendListApiResponse : BasicApiResponse
    {
        public ServerFriendListData data;
    }

    [Serializable]
    private class ServerFriendListData
    {
        public ServerFriendResponse[] items;
        public ServerFriendCursor nextCursor;
        public bool hasMore;
        public ServerDailyGiftStatus dailyGift;
    }

    [Serializable]
    private class ServerFriendResponse
    {
        public int playerId;
        public string nickname;
        public int level;
        public string friendsSince;
        public bool dailyGiftSent;
        public bool dailyGiftReceived;
    }

    [Serializable]
    private class ServerFriendCursor
    {
        public int level;
        public int playerId;
    }

    [Serializable]
    private class ServerDailyGiftStatus
    {
        public bool enabled;
        public string periodKey;
        public int itemId;
        public string itemKey;
        public int quantity;
        public int dailySendLimit;
        public int sentCount;
        public int remainingSendCount;
        public int dailyReceiveLimit;
        public int receivedCount;
        public int remainingReceiveCount;
        public int configVersion;
    }

    [Serializable]
    private class ServerFriendMutationApiResponse : BasicApiResponse
    {
        public ServerFriendResponse data;
    }

    [Serializable]
    private class ServerDailyGiftApiResponse : BasicApiResponse
    {
        public ServerDailyGiftSendResponse data;
    }

    [Serializable]
    private class ServerDailyGiftSendResponse
    {
        public long recordId;
        public int senderPlayerId;
        public int receiverPlayerId;
        public string periodKey;
        public int itemId;
        public string itemKey;
        public int quantity;
        public int receiverBalanceAfter;
        public int configVersion;
        public string sentTime;
        public bool replayed;
    }
#pragma warning restore 0649
}
