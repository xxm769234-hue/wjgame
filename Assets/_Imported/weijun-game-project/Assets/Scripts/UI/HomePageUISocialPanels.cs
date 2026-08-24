using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public partial class HomePageUI
{
    private const int FriendsPerPage = 5;

    private GameObject accountProfilePanelObject;
    private GameObject friendsPanelObject;
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

    private void ShowAccountProfilePanel()
    {
        HideFriendsPanel();
        HidePanelsBehindSocialOverlay();

        HomePageScenePanel panel = OpenRegisteredScenePanel(HomePageScenePanelType.AccountProfile);
        if (panel == null)
        {
            SetLeftContent("个人信息", BuildAccountProfileFallbackText(), "个人档案场景组件未找到");
            return;
        }

        accountProfilePanelObject = panel.gameObject;
        profileSwitchLoginConfirmation = false;
        RefreshAccountProfileScenePanel(panel.PanelRoot, "账号资料来自当前登录存档");
        SetStatus("已打开个人档案");
    }

    private void RefreshAccountProfileScenePanel(Transform root, string status)
    {
        if (root == null)
        {
            return;
        }

        string nickname = PlayerPrefs.GetString("PlayerNickname", "玩家");
        string identifier = GetCurrentProfileIdentifier();
        string username = PlayerPrefs.GetString("LoginUsername", string.Empty);

        SetSceneText(root, "ProfileTitle", "个人信息");
        SetSceneText(root, "ProfileModeBadge", "账号登录");
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
        SetSceneText(root, "ProfileFriendStat", $"好友数量\n{v37Friends.Count} 人");
        SetSceneText(root, "ProfileExploreStat", $"领地探索\n{PlayerPrefs.GetInt("TaskMapEntered", 0)} 次");
        SetSceneText(root, "ProfileSyncText", GetProfileSyncText(isAuthenticated));
        SetSceneText(root, "ProfileStatus", status);
        SetSceneText(root, "ProfileNicknameHint", "账号昵称由服务器资料管理");
        ConfigureProfileAvatarControls(root, true, nickname);

        InputField nicknameInput = FindSceneComponent<InputField>(root, "ProfileNicknameInput");
        if (nicknameInput != null)
        {
            nicknameInput.SetTextWithoutNotify(nickname);
            nicknameInput.characterLimit = 12;
            nicknameInput.readOnly = true;
            nicknameInput.interactable = !profileRefreshRunning;
        }

        Button saveButton = FindSceneComponent<Button>(root, "ProfileSaveNicknameButton");
        saveButton.interactable = false;
        SetSceneButtonText(saveButton, "账号昵称");
        BindSceneButton(saveButton, null);

        Button refreshButton = FindSceneComponent<Button>(root, "ProfileRefreshButton");
        refreshButton.interactable = !profileRefreshRunning;
        SetSceneButtonText(refreshButton, profileRefreshRunning ? "同步中..." : "刷新资料");
        BindSceneButton(refreshButton, () => StartCoroutine(RefreshCurrentUserProfile(root)));

        Button switchButton = FindSceneComponent<Button>(root, "ProfileSwitchLoginButton");
        SetSceneButtonText(switchButton, profileSwitchLoginConfirmation ? "确认切换账号" : "切换账号");
        BindSceneButton(switchButton, () => HandleProfileSwitchLogin(root));
        BindSceneButton(FindSceneComponent<Button>(root, "ProfileCopyIdButton"), () => CopyProfileIdentifier(root));
        BindSceneButton(FindSceneComponent<Button>(root, "ProfileCloseButton"), HideAccountProfilePanel);
    }

    private void CopyProfileIdentifier(Transform root)
    {
        GUIUtility.systemCopyBuffer = GetCurrentProfileIdentifier();
        profileSwitchLoginConfirmation = false;
        RefreshAccountProfileScenePanel(root, "玩家编号已复制");
    }

    private void HandleProfileSwitchLogin(Transform root)
    {
        if (!profileSwitchLoginConfirmation)
        {
            profileSwitchLoginConfirmation = true;
            RefreshAccountProfileScenePanel(root, "再次点击“确认切换账号”返回登录界面");
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
        RefreshAccountProfileScenePanel(root, "正在同步服务器资料...");

        UnityWebRequest request = CreateAuthorizedGet("/user/me");
        if (request == null)
        {
            profileRefreshRunning = false;
            RefreshAccountProfileScenePanel(root, requestCreationError);
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
                RefreshAccountProfileScenePanel(root, $"资料同步失败：{ApiRequestUtility.GetErrorMessage(request, "请求失败")}");
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
                RefreshAccountProfileScenePanel(root, "服务器资料格式无法解析");
                yield break;
            }

            if (response == null || response.code != 200 || response.data == null)
            {
                profileRefreshRunning = false;
                RefreshAccountProfileScenePanel(root, response == null ? "服务器未返回资料" : GetApiError(response));
                yield break;
            }

            SaveUserProfile(response.data);
            profileRefreshRunning = false;
            RefreshAccountProfileScenePanel(root, "服务器资料同步完成");
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
        SaveCurrentAvatarState(profile.avatarUrl, profile.avatarSource, profile.avatarVersion, profile.avatarUpdatedTime);
        PlayerPrefs.SetString("ProfileLastSync", DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        PlayerPrefs.Save();
        WeChatPlatformBridge.SyncFriendLevel(profile.level);
        SyncInventoryResourceValues(profile.items);
        RefreshHomeResourceMetrics();
        UpdateHomeAvatarNickname(profile.nickname);
    }

    private string GetCurrentProfileIdentifier()
    {
        return GameSession.PlayerId.ToString();
    }

    private static string GetNicknameInitial(string nickname)
    {
        return string.IsNullOrEmpty(nickname) ? "游" : nickname.Substring(0, 1);
    }

    private static string GetProfileSyncText(bool isAuthenticated)
    {
        string lastSync = PlayerPrefs.GetString("ProfileLastSync", string.Empty);
        return string.IsNullOrEmpty(lastSync) ? "资料来源  当前登录缓存" : $"最近同步  {lastSync}";
    }

    private string BuildAccountProfileFallbackText()
    {
        return
            $"昵称：{PlayerPrefs.GetString("PlayerNickname", "玩家")}\n" +
            $"账号：{PlayerPrefs.GetString("LoginUsername", string.Empty)}\n" +
            "状态：账号登录\n" +
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
            if (labels[i].name == "AvatarName" || labels[i].name == "BookAvatarName")
            {
                labels[i].text = nickname;
            }
        }

        RefreshBookAvatarSummary();
    }

    private void HideAccountProfilePanel()
    {
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.AccountProfile);
        if (panel != null)
        {
            panel.SetVisible(false);
        }

        accountProfilePanelObject = null;
        profileSwitchLoginConfirmation = false;
    }

    private void ShowFriendsPanel()
    {
        HideAccountProfilePanel();
        HidePanelsBehindSocialOverlay();

        HomePageScenePanel panel = OpenRegisteredScenePanel(HomePageScenePanelType.Friends);
        if (panel == null)
        {
            SetLeftContent("好友列表", "好友面板场景组件未找到。", "无法打开好友列表");
            return;
        }

        friendsPanelObject = panel.gameObject;
        friendDeleteConfirmation = false;
        if (!HasAuthSession())
        {
            SetStatus("请先登录账号");
            return;
        }

        ResetServerFriendRequestPanel();
        RefreshFriendsScenePanel(panel.PanelRoot, "正在同步服务器好友和申请摘要...");
        StartCoroutine(RefreshServerFriendRequestState(panel.PanelRoot, true, "服务器好友同步完成"));
        SetStatus("已打开好友列表");
    }

    private void RefreshFriendsScenePanel(Transform root, string status)
    {
        if (root == null)
        {
            return;
        }

        if (!HasAuthSession())
        {
            return;
        }

        RefreshServerFriendRequestPanel(root, status);
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
        InvalidateServerFriendRequestPanel();
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
        public string avatarUrl;
        public string avatarSource;
        public long avatarVersion;
        public string avatarUpdatedTime;
        public InventoryItemResponse[] items;
    }

    [Serializable]
    private class ServerFriendResponse
    {
        public int playerId;
        public string nickname;
        public int level;
        public string friendsSince;
        public string avatarUrl;
        public long avatarVersion;
        public bool dailyStaminaGiftSent;
        public bool dailyStaminaGiftReceived;
        public string onlineStatus;
        public long presenceVersion;
        public string lastActiveTime;
        public string lastOfflineTime;
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
        public int quantity;
        public int perFriendDailyLimit;
        public int sentCount;
        public int receivedCount;
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
        public int quantity;
        public int receiverStaminaBefore;
        public int receiverStaminaAfter;
        public int maxStamina;
        public bool overCap;
        public int policyModeConfigId;
        public int policyVersion;
        public int configVersion;
        public string sentTime;
        public bool replayed;
    }
#pragma warning restore 0649
}
