using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
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
    private bool profileNicknamePolicyLoading;
    private bool profileNicknameSaving;
    private bool profileSwitchLoginConfirmation;
    private bool profileCancellationStatusLoading;
    private bool profileCancellationRequestRunning;
    private bool profileCancellationConfirmation;
    private bool profileCancellationStatusLoaded;
    private bool friendDeleteConfirmation;
    private bool friendsOnlineOnly;
    private int friendsPageIndex;
    private string friendsSearchQuery = string.Empty;
    private string selectedFriendId = string.Empty;
    private string friendsStatusMessage = string.Empty;
    private bool serverFriendsLoading;
    private bool serverFriendMutationRunning;
    private NicknamePolicyData profileNicknamePolicy;
    private string profileNicknamePolicyStatus = string.Empty;
    private ProfileCancellationData profileCancellationStatus;
    private string profileCancellationRequestId = string.Empty;
    private string profileCancellationRecoveryRequestId = string.Empty;

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
        profileCancellationConfirmation = false;
        profileCancellationStatusLoading = false;
        profileCancellationRequestRunning = false;
        profileCancellationStatusLoaded = false;
        profileCancellationStatus = null;
        RefreshAccountProfileScenePanel(panel.PanelRoot, "账号资料来自当前登录存档");
        if (HasAuthSession())
        {
            StartCoroutine(RefreshProfileNicknamePolicy(panel.PanelRoot));
            StartCoroutine(RefreshProfileCancellationStatus(panel.PanelRoot, "正在读取账号注销状态..."));
        }
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
        bool isAuthenticated = HasAuthSession();

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
        SetSceneText(root, "ProfileVital", $"生命  {PlayerPrefs.GetInt("PlayerHp", 1000)}    体力  {GetHomeStamina()}");
        SetSceneText(root, "ProfileSignStat", $"签到天数\n{PlayerPrefs.GetInt("DailySignDays", 0)} 天");
        SetSceneText(root, "ProfileFriendStat", $"好友数量\n{v37Friends.Count} 人");
        SetSceneText(root, "ProfileExploreStat", $"领地探索\n{PlayerPrefs.GetInt("TaskMapEntered", 0)} 次");
        SetSceneText(root, "ProfileSyncText", GetProfileSyncText(isAuthenticated));
        SetSceneText(root, "ProfileStatus", status);
        SetSceneText(root, "ProfileCancellationStatus", BuildProfileCancellationStatus(isAuthenticated));
        SetSceneText(root, "ProfileNicknameHint", BuildProfileNicknameHint());
        ConfigureProfileAvatarControls(root, true, nickname);

        InputField nicknameInput = FindSceneComponent<InputField>(root, "ProfileNicknameInput");
        bool canEditNickname = isAuthenticated
            && profileNicknamePolicy != null
            && profileNicknamePolicy.enabled
            && profileNicknamePolicy.canChangeNow
            && !profileNicknameSaving
            && !profileNicknamePolicyLoading;
        if (nicknameInput != null)
        {
            nicknameInput.SetTextWithoutNotify(nickname);
            nicknameInput.characterLimit = 12;
            nicknameInput.readOnly = !canEditNickname;
            nicknameInput.interactable = isAuthenticated && !profileRefreshRunning && !profileNicknameSaving && !profileNicknamePolicyLoading;
        }

        Button saveButton = FindSceneComponent<Button>(root, "ProfileSaveNicknameButton");
        if (saveButton != null)
        {
            saveButton.interactable = canEditNickname;
            SetSceneButtonText(saveButton, profileNicknameSaving ? "保存中..." : "保存昵称");
            BindSceneButton(saveButton, () => TrySaveProfileNickname(root));
        }

        Button refreshButton = FindSceneComponent<Button>(root, "ProfileRefreshButton");
        refreshButton.interactable = !profileRefreshRunning;
        SetSceneButtonText(refreshButton, profileRefreshRunning ? "同步中..." : "刷新资料");
        BindSceneButton(refreshButton, () => StartCoroutine(RefreshCurrentUserProfile(root)));

        Button switchButton = FindSceneComponent<Button>(root, "ProfileSwitchLoginButton");
        SetSceneButtonText(switchButton, profileSwitchLoginConfirmation ? "确认切换账号" : "切换账号");
        BindSceneButton(switchButton, () => HandleProfileSwitchLogin(root));

        Button cancellationButton = FindSceneComponent<Button>(root, "ProfileCancelAccountButton");
        bool cancellationStateReady = isAuthenticated
            && profileCancellationStatusLoaded
            && !profileCancellationStatusLoading
            && !profileCancellationRequestRunning
            && !profileRefreshRunning;
        SetSceneButtonText(cancellationButton, GetProfileCancellationButtonText(isAuthenticated));
        if (cancellationButton != null)
        {
            cancellationButton.interactable = cancellationStateReady
                && !IsProfileCancellationCompleted()
                && (HasCancellableProfileCancellation() || CanApplyProfileCancellation());
        }
        BindSceneButton(cancellationButton, () => HandleProfileCancellation(root));
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

    private IEnumerator RefreshProfileCancellationStatus(Transform root, string status)
    {
        if (root == null || !HasAuthSession() || profileCancellationStatusLoading)
        {
            yield break;
        }

        profileCancellationStatusLoading = true;
        profileCancellationStatusLoaded = false;
        RefreshAccountProfileScenePanel(root, status);

        UnityWebRequest request = CreateAuthorizedGet("/user/privacy/account-cancellation");
        if (request == null)
        {
            profileCancellationStatusLoading = false;
            RefreshAccountProfileScenePanel(root, requestCreationError);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();

            if (HandleUnauthorizedResponse(request))
            {
                profileCancellationStatusLoading = false;
                yield break;
            }

            ProfileCancellationApiResponse response = ParseJson<ProfileCancellationApiResponse>(request.downloadHandler.text);
            if (HandleClosedAccountCancellationResponse(response))
            {
                yield break;
            }

            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                profileCancellationStatusLoading = false;
                RefreshAccountProfileScenePanel(root, response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "注销状态读取失败")
                    : GetApiError(response));
                yield break;
            }

            profileCancellationStatus = response.data;
            profileCancellationStatusLoaded = true;
            profileCancellationStatusLoading = false;
            profileCancellationConfirmation = false;
            RefreshAccountProfileScenePanel(root, status);
        }
    }

    private void HandleProfileCancellation(Transform root)
    {
        if (!HasAuthSession() || profileCancellationStatusLoading || profileCancellationRequestRunning)
        {
            return;
        }

        if (!profileCancellationStatusLoaded)
        {
            RefreshAccountProfileScenePanel(root, "注销状态尚未同步，请稍候...");
            StartCoroutine(RefreshProfileCancellationStatus(root, "正在读取账号注销状态..."));
            return;
        }

        if (IsProfileCancellationCompleted())
        {
            RefreshAccountProfileScenePanel(root, "账号已经完成注销，无法恢复");
            return;
        }

        if (HasCancellableProfileCancellation())
        {
            if (!profileCancellationConfirmation)
            {
                profileCancellationConfirmation = true;
                RefreshAccountProfileScenePanel(root, "再次点击“确认撤销注销”以恢复账号");
                return;
            }

            StartCoroutine(CancelProfileCancellation(root));
            return;
        }

        if (!CanApplyProfileCancellation())
        {
            RefreshAccountProfileScenePanel(root, "当前注销状态不可提交新申请，请刷新状态");
            return;
        }

        if (!profileCancellationConfirmation)
        {
            profileCancellationConfirmation = true;
            RefreshAccountProfileScenePanel(root, "再次点击“确认注销账号”提交申请");
            return;
        }

        StartCoroutine(ApplyProfileCancellation(root));
    }

    private IEnumerator ApplyProfileCancellation(Transform root)
    {
        profileCancellationRequestRunning = true;
        profileCancellationConfirmation = false;
        if (string.IsNullOrEmpty(profileCancellationRequestId))
        {
            profileCancellationRequestId = Guid.NewGuid().ToString("D").ToLowerInvariant();
        }

        RefreshAccountProfileScenePanel(root, "正在提交注销申请...");
        ProfileCancellationRequest payload = new ProfileCancellationRequest
        {
            requestId = profileCancellationRequestId
        };

        if (!TryCreateAuthorizedPost(
                "/user/privacy/account-cancellation",
                JsonUtility.ToJson(payload),
                out UnityWebRequest request,
                out string error))
        {
            profileCancellationRequestRunning = false;
            RefreshAccountProfileScenePanel(root, error);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();

            if (HandleUnauthorizedResponse(request))
            {
                profileCancellationRequestRunning = false;
                yield break;
            }

            ProfileCancellationApiResponse response = ParseJson<ProfileCancellationApiResponse>(request.downloadHandler.text);
            if (HandleClosedAccountCancellationResponse(response))
            {
                yield break;
            }

            if (request.result == UnityWebRequest.Result.Success && response != null && response.code == 200 && response.data != null)
            {
                profileCancellationStatus = response.data;
                profileCancellationStatusLoaded = true;
                profileCancellationRequestRunning = false;
                RefreshAccountProfileScenePanel(root, string.IsNullOrEmpty(response.message)
                    ? "注销申请已提交，账号进入冷静期"
                    : response.message);
                SetStatus("注销申请已提交，冷静期内可以撤销");
                yield break;
            }

            profileCancellationRequestRunning = false;
            profileCancellationStatusLoaded = false;
            string message = response == null
                ? ApiRequestUtility.GetErrorMessage(request, "申请账号注销失败")
                : GetApiError(response);
            RefreshAccountProfileScenePanel(root, message);
            yield return RefreshProfileCancellationStatus(root, message);
        }
    }

    private IEnumerator CancelProfileCancellation(Transform root)
    {
        profileCancellationRequestRunning = true;
        profileCancellationConfirmation = false;
        if (string.IsNullOrEmpty(profileCancellationRecoveryRequestId))
        {
            profileCancellationRecoveryRequestId = Guid.NewGuid().ToString("D").ToLowerInvariant();
        }

        RefreshAccountProfileScenePanel(root, "正在撤销注销申请...");
        ProfileCancellationCancelRequest payload = new ProfileCancellationCancelRequest
        {
            requestId = profileCancellationRecoveryRequestId
        };

        if (!TryCreateAuthorizedPost(
                "/user/privacy/account-cancellation/cancel",
                JsonUtility.ToJson(payload),
                out UnityWebRequest request,
                out string error))
        {
            profileCancellationRequestRunning = false;
            RefreshAccountProfileScenePanel(root, error);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();

            if (HandleUnauthorizedResponse(request))
            {
                profileCancellationRequestRunning = false;
                yield break;
            }

            ProfileCancellationApiResponse response = ParseJson<ProfileCancellationApiResponse>(request.downloadHandler.text);
            if (HandleClosedAccountCancellationResponse(response))
            {
                yield break;
            }

            if (request.result == UnityWebRequest.Result.Success && response != null && response.code == 200 && response.data != null)
            {
                profileCancellationStatus = response.data;
                profileCancellationStatusLoaded = true;
                profileCancellationRequestRunning = false;
                profileCancellationRequestId = string.Empty;
                profileCancellationRecoveryRequestId = string.Empty;
                RefreshAccountProfileScenePanel(root, string.IsNullOrEmpty(response.message)
                    ? "注销申请已撤销，账号已恢复"
                    : response.message);
                SetStatus("注销申请已撤销，账号已恢复");
                yield break;
            }

            profileCancellationRequestRunning = false;
            profileCancellationStatusLoaded = false;
            string message = response == null
                ? ApiRequestUtility.GetErrorMessage(request, "撤销账号注销失败")
                : GetApiError(response);
            RefreshAccountProfileScenePanel(root, message);
            yield return RefreshProfileCancellationStatus(root, message);
        }
    }

    private bool HasCancellableProfileCancellation()
    {
        return profileCancellationStatus != null && profileCancellationStatus.cancellable;
    }

    private bool CanApplyProfileCancellation()
    {
        if (profileCancellationStatus == null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(profileCancellationStatus.accountStatus)
            && !string.Equals(profileCancellationStatus.accountStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrEmpty(profileCancellationStatus.cancellationStatus)
            || string.Equals(profileCancellationStatus.cancellationStatus, "CANCELLED", StringComparison.OrdinalIgnoreCase);
    }

    private bool HandleClosedAccountCancellationResponse(ProfileCancellationApiResponse response)
    {
        if (response == null || response.code != 7708)
        {
            return false;
        }

        profileCancellationStatusLoading = false;
        profileCancellationRequestRunning = false;
        profileCancellationStatusLoaded = false;
        profileCancellationStatus = null;
        ClearClosedAccountProfileState();
        ReturnToLogin();
        return true;
    }

    private void ClearClosedAccountProfileState()
    {
        GameSession.Clear();
        string[] accountStateKeys =
        {
            "LoginUsername",
            "PlayerNickname",
            "PlayerAvatarUrl",
            "PlayerAvatarSource",
            "PlayerAvatarVersion",
            "PlayerAvatarUpdatedTime",
            "PlayerTotalExperience",
            "PlayerLevelCurveRevision",
            "PlayerCurrentLevelExperience",
            "PlayerRequiredExperience",
            "PlayerExperienceProgressBps",
            "PlayerMaxLevel",
            "PlayerEconomyFrozen",
            "PlayerLv",
            "Gold",
            "Wood",
            "Stone",
            "Iron",
            "Crystal",
            "SoulCrystal"
        };

        for (int index = 0; index < accountStateKeys.Length; index++)
        {
            PlayerPrefs.DeleteKey(accountStateKeys[index]);
        }

        PlayerPrefs.Save();
    }

    private bool IsProfileCancellationCompleted()
    {
        return profileCancellationStatus != null
            && (string.Equals(profileCancellationStatus.cancellationStatus, "COMPLETED", StringComparison.OrdinalIgnoreCase)
                || string.Equals(profileCancellationStatus.accountStatus, "CLOSED", StringComparison.OrdinalIgnoreCase));
    }

    private string GetProfileCancellationButtonText(bool isAuthenticated)
    {
        if (!isAuthenticated)
        {
            return "账号注销";
        }

        if (profileCancellationStatusLoading)
        {
            return "读取中...";
        }

        if (profileCancellationRequestRunning)
        {
            return "处理中...";
        }

        if (IsProfileCancellationCompleted())
        {
            return "账号已注销";
        }

        if (HasCancellableProfileCancellation())
        {
            return profileCancellationConfirmation ? "确认撤销注销" : "撤销注销";
        }

        if (CanApplyProfileCancellation())
        {
            return profileCancellationConfirmation ? "确认注销账号" : "注销账号";
        }

        return "注销状态处理中";
    }

    private string BuildProfileCancellationStatus(bool isAuthenticated)
    {
        if (!isAuthenticated)
        {
            return "账号注销仅适用于已登录账号";
        }

        if (profileCancellationStatusLoading)
        {
            return "账号注销状态：读取中...";
        }

        if (!profileCancellationStatusLoaded || profileCancellationStatus == null)
        {
            return "账号注销状态：暂未同步";
        }

        if (string.IsNullOrEmpty(profileCancellationStatus.cancellationStatus))
        {
            return "账号注销状态：未申请";
        }

        string state = profileCancellationStatus.cancellationStatus;
        if (string.Equals(state, "PENDING", StringComparison.OrdinalIgnoreCase)
            || string.Equals(state, "PROCESSING", StringComparison.OrdinalIgnoreCase))
        {
            string scheduledTime = string.IsNullOrEmpty(profileCancellationStatus.scheduledTime)
                ? string.Empty
                : "，冷静期截止：" + profileCancellationStatus.scheduledTime;
            return "账号注销状态：" + state + scheduledTime;
        }

        if (string.Equals(state, "CANCELLED", StringComparison.OrdinalIgnoreCase))
        {
            return "账号注销状态：已撤销，可重新申请";
        }

        if (string.Equals(state, "COMPLETED", StringComparison.OrdinalIgnoreCase))
        {
            return "账号注销状态：已完成，账号不可恢复";
        }

        return "账号注销状态：" + state;
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
            yield return RefreshProfileNicknamePolicy(root);
            yield return RefreshProfileCancellationStatus(root, "正在同步账号注销状态...");
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
        PlayerPrefs.SetString("PlayerNicknameVersion", Math.Max(0L, profile.nicknameVersion).ToString());
        PlayerPrefs.SetString("PlayerNicknameUpdatedTime", profile.nicknameUpdatedTime ?? string.Empty);
        PlayerPrefs.SetInt("PlayerNicknameChangeCount", Math.Max(0, profile.nicknameChangeCount));
        PlayerPrefs.SetInt("PlayerNicknameChangeRequired", profile.nicknameChangeRequired ? 1 : 0);
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

        TMP_Text[] tmpLabels = mainPageTransform.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < tmpLabels.Length; i++)
        {
            if (tmpLabels[i].name == "AvatarName" || tmpLabels[i].name == "BookAvatarName")
            {
                tmpLabels[i].text = nickname;
            }
        }

        RefreshBookAvatarSummary();
    }

    private IEnumerator RefreshProfileNicknamePolicy(Transform root)
    {
        if (profileNicknamePolicyLoading || !HasAuthSession())
        {
            yield break;
        }

        profileNicknamePolicyLoading = true;
        RefreshAccountProfileScenePanel(root, "正在读取昵称规则...");
        UnityWebRequest request = CreateAuthorizedGet("/user/nickname/policy");
        if (request == null)
        {
            profileNicknamePolicyLoading = false;
            profileNicknamePolicyStatus = requestCreationError;
            RefreshAccountProfileScenePanel(root, "昵称规则读取失败");
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                profileNicknamePolicyLoading = false;
                yield break;
            }

            NicknamePolicyApiResponse response = ParseJson<NicknamePolicyApiResponse>(request.downloadHandler.text);
            profileNicknamePolicyLoading = false;
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                profileNicknamePolicyStatus = response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response);
                RefreshAccountProfileScenePanel(root, "昵称规则读取失败");
                yield break;
            }

            profileNicknamePolicy = response.data;
            profileNicknamePolicyStatus = string.Empty;
            SaveProfileNicknamePolicyState(response.data);
            RefreshAccountProfileScenePanel(root, "昵称规则已同步");
        }
    }

    private void TrySaveProfileNickname(Transform root)
    {
        if (profileNicknameSaving || profileNicknamePolicyLoading || !HasAuthSession())
        {
            return;
        }

        if (profileNicknamePolicy == null)
        {
            StartCoroutine(RefreshProfileNicknamePolicy(root));
            return;
        }

        if (!profileNicknamePolicy.enabled || !profileNicknamePolicy.canChangeNow)
        {
            RefreshAccountProfileScenePanel(root, "当前昵称暂不可修改");
            return;
        }

        InputField nicknameInput = FindSceneComponent<InputField>(root, "ProfileNicknameInput");
        string requestedNickname = nicknameInput == null ? string.Empty : nicknameInput.text.Trim();
        if (string.IsNullOrEmpty(requestedNickname))
        {
            RefreshAccountProfileScenePanel(root, "请输入昵称后再保存");
            return;
        }

        string currentNickname = PlayerPrefs.GetString("PlayerNickname", string.Empty);
        if (string.Equals(requestedNickname, currentNickname, StringComparison.Ordinal))
        {
            RefreshAccountProfileScenePanel(root, "昵称未变化");
            return;
        }

        StartCoroutine(SaveProfileNickname(root, requestedNickname, profileNicknamePolicy.nicknameVersion));
    }

    private IEnumerator SaveProfileNickname(Transform root, string requestedNickname, long expectedNicknameVersion)
    {
        profileNicknameSaving = true;
        RefreshAccountProfileScenePanel(root, "正在保存昵称...");
        NicknameUpdateRequest payload = new NicknameUpdateRequest
        {
            nickname = requestedNickname,
            expectedNicknameVersion = expectedNicknameVersion
        };

        if (!ApiRequestUtility.TryCreateJsonRequest(
                "/user/nickname",
                UnityWebRequest.kHttpVerbPUT,
                JsonUtility.ToJson(payload),
                true,
                out UnityWebRequest request,
                out string error))
        {
            profileNicknameSaving = false;
            RefreshAccountProfileScenePanel(root, error);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                profileNicknameSaving = false;
                yield break;
            }

            NicknameUpdateApiResponse response = ParseJson<NicknameUpdateApiResponse>(request.downloadHandler.text);
            if (request.result == UnityWebRequest.Result.Success && response != null && response.code == 200 && response.data != null)
            {
                ApplyProfileNicknameUpdate(response.data);
                profileNicknameSaving = false;
                profileNicknamePolicy = null;
                yield return RefreshProfileNicknamePolicy(root);
                RefreshAccountProfileScenePanel(root, "昵称已修改并同步到服务器");
                SetStatus("昵称已更新");
                yield break;
            }

            profileNicknameSaving = false;
            int code = response == null ? 0 : response.code;
            string message = response == null
                ? ApiRequestUtility.GetErrorMessage(request, "昵称修改失败")
                : GetApiError(response);
            if (code == 2009 || code == 2010 || code == 2011)
            {
                yield return RefreshProfileNicknamePolicy(root);
            }

            RefreshAccountProfileScenePanel(root, message);
            InputField nicknameInput = FindSceneComponent<InputField>(root, "ProfileNicknameInput");
            if (nicknameInput != null)
            {
                nicknameInput.SetTextWithoutNotify(requestedNickname);
            }
        }
    }

    private string BuildProfileNicknameHint()
    {
        if (profileNicknamePolicyLoading)
        {
            return "正在读取服务器昵称规则...";
        }

        if (profileNicknamePolicy == null)
        {
            return string.IsNullOrEmpty(profileNicknamePolicyStatus)
                ? "昵称规则尚未读取"
                : "昵称规则读取失败：" + profileNicknamePolicyStatus;
        }

        if (!profileNicknamePolicy.enabled)
        {
            return "服务器暂未开放昵称修改";
        }

        if (!profileNicknamePolicy.canChangeNow)
        {
            return string.IsNullOrEmpty(profileNicknamePolicy.nextChangeAvailableTime)
                ? "昵称修改仍在冷却中"
                : "下次可修改：" + profileNicknamePolicy.nextChangeAvailableTime;
        }

        return "昵称长度 " + profileNicknamePolicy.minLength + "-" + profileNicknamePolicy.maxLength + "，仅支持中文、英文和数字";
    }

    private static void SaveProfileNicknamePolicyState(NicknamePolicyData policy)
    {
        if (policy == null)
        {
            return;
        }

        PlayerPrefs.SetString("PlayerNicknameVersion", Math.Max(0L, policy.nicknameVersion).ToString());
        PlayerPrefs.SetString("PlayerNicknameUpdatedTime", policy.nicknameUpdatedTime ?? string.Empty);
        PlayerPrefs.SetInt("PlayerNicknameChangeCount", Math.Max(0, policy.nicknameChangeCount));
        PlayerPrefs.SetInt("PlayerNicknameChangeRequired", policy.nicknameChangeRequired ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void ApplyProfileNicknameUpdate(NicknameUpdateData update)
    {
        PlayerPrefs.SetString("PlayerNickname", update.nickname ?? string.Empty);
        PlayerPrefs.SetString("PlayerNicknameVersion", Math.Max(0L, update.nicknameVersion).ToString());
        PlayerPrefs.SetString("PlayerNicknameUpdatedTime", update.nicknameUpdatedTime ?? string.Empty);
        PlayerPrefs.SetInt("PlayerNicknameChangeCount", Math.Max(0, update.nicknameChangeCount));
        PlayerPrefs.SetInt("PlayerNicknameChangeRequired", update.nicknameChangeRequired ? 1 : 0);
        PlayerPrefs.Save();
        UpdateHomeAvatarNickname(update.nickname);
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
        public long nicknameVersion;
        public string nicknameUpdatedTime;
        public int nicknameChangeCount;
        public bool nicknameChangeRequired;
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
    private class NicknamePolicyApiResponse : BasicApiResponse
    {
        public NicknamePolicyData data;
    }

    [Serializable]
    private class NicknamePolicyData
    {
        public bool enabled;
        public int minLength;
        public int maxLength;
        public string allowedCharacterType;
        public bool pureNumberAllowed;
        public int cooldownHours;
        public string[] reservedWords;
        public long configVersion;
        public long nicknameVersion;
        public string nicknameUpdatedTime;
        public int nicknameChangeCount;
        public bool nicknameChangeRequired;
        public bool canChangeNow;
        public string nextChangeAvailableTime;
    }

    [Serializable]
    private class NicknameUpdateRequest
    {
        public string nickname;
        public long expectedNicknameVersion;
    }

    [Serializable]
    private class NicknameUpdateApiResponse : BasicApiResponse
    {
        public NicknameUpdateData data;
    }

    [Serializable]
    private class NicknameUpdateData
    {
        public string nickname;
        public long nicknameVersion;
        public string nicknameUpdatedTime;
        public int nicknameChangeCount;
        public bool nicknameChangeRequired;
        public string nextChangeAvailableTime;
    }

    [Serializable]
    private class ProfileCancellationApiResponse : BasicApiResponse
    {
        public ProfileCancellationData data;
    }

    [Serializable]
    private class ProfileCancellationData
    {
        public long cancellationId;
        public string accountStatus;
        public string cancellationStatus;
        public string requestedTime;
        public string scheduledTime;
        public string cancelledTime;
        public string completedTime;
        public bool cancellable;
    }

    [Serializable]
    private class ProfileCancellationRequest
    {
        public string requestId;
    }

    [Serializable]
    private class ProfileCancellationCancelRequest
    {
        public string requestId;
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
