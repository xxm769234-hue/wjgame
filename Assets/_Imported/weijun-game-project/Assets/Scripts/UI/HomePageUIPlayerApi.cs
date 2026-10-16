using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public partial class HomePageUI
{
    private ServerTaskResponse[] currentServerTasks = new ServerTaskResponse[0];
    private bool serverTaskListRunning;
    private bool serverTaskClaimRunning;
    private string pendingServerTaskCode = string.Empty;

    private SignInOverviewData serverSignInOverview;
    private const int SupportedSignInContractVersion = 2;
    private const int DailySignColumns = 5;
    private const int DailySignVisibleRows = 2;
    private string serverSignPlanCode = "MONTHLY";
    private string serverSignStatus = string.Empty;
    private readonly List<Transform> _dailySignCards = new List<Transform>();
    private RectTransform _dailySignContent;
    private RectTransform _dailySignCardTemplate;
    private int _dailySignFocusDay;
    private bool _dailySignResetScroll;
    private readonly List<Transform> _activitySignCards = new List<Transform>();
    private readonly List<Vector2> _activitySignCardPositions = new List<Vector2>();
    private RectTransform _activitySignCardParent;
    private RectTransform _activitySignCardTemplate;
    private int _activitySignPage;
    private readonly List<Button> _activityTypeButtons = new List<Button>();
    private Transform _activityTypeRoot;
    private bool serverSignOverviewRunning;
    private bool serverSignMutationRunning;
    private bool serverSignOverviewReady;
    private bool serverSignFocusCurrentDay;
    private GameObject signMakeupConfirmObject;
    private int signConfirmationVersion;

    private CardDrawPoolData serverCardDrawPool;
    private bool serverCardDrawPoolRunning;
    private bool serverCardDrawRunning;
    private int serverLotteryDisplayPage;
    private string serverLotteryStatus = string.Empty;
    private string serverLotteryRewardName = string.Empty;
    private int serverLotteryRewardAmount;
    private bool serverLotteryHasResult;
    private string pendingCardDrawKey = string.Empty;
    private string pendingCardDrawOption = string.Empty;
    private string pendingCardDrawPoolCode = string.Empty;
    private int pendingCardDrawHighlightNpcId;
    private bool cardDrawRoutePoolMismatch;

    private bool rewardAdRunning;
    private string pendingRewardAdWatchId = string.Empty;

    private void RefreshHomeResourceMetrics()
    {
        if (boundBookView != null)
        {
            SetBookMetricValue(boundBookView.MetricTexts, 0, GetHomePurificationAgent().ToString());
            SetBookMetricValue(boundBookView.MetricTexts, 1, GetHomeStaminaDisplay());
            SetBookMetricValue(boundBookView.MetricTexts, 2, PlayerPrefs.GetInt("Gold", 1000).ToString());
            SetBookMetricValue(boundBookView.MetricTexts, 3, PlayerPrefs.GetInt("Crystal", 0).ToString());
        }

        RefreshBookAvatarSummary();

        RefreshOpenProfileResourceCards();

        if (popupLevelText != null)
        {
            popupLevelText.text = $"等级：Lv.{PlayerPrefs.GetInt("PlayerLv", 1)}";
        }

        if (popupResourceText != null)
        {
            popupResourceText.text =
                $"金币：{PlayerPrefs.GetInt("Gold", 1000)}\n" +
                $"钻石：{PlayerPrefs.GetInt("Crystal", 0)}\n" +
                $"木材：{PlayerPrefs.GetInt("Wood", 500)}\n" +
                $"石材：{PlayerPrefs.GetInt("Stone", 500)}\n" +
                $"铁矿：{PlayerPrefs.GetInt("Iron", 300)}";
        }
    }

    private void RefreshOpenProfileResourceCards()
    {
        HomePageScenePanel profilePanel = GetRegisteredScenePanel(HomePageScenePanelType.AccountProfile);
        if (profilePanel == null || !profilePanel.gameObject.activeInHierarchy || profilePanel.PanelRoot == null)
        {
            return;
        }

        Transform root = profilePanel.PanelRoot;
        SetSceneText(root, "ProfileVital", $"生命  {PlayerPrefs.GetInt("PlayerHp", 1000)}    体力  {GetHomeStamina()}");
    }

    private void OpenServerTasks(string category)
    {
        OpenServerTasks(category, string.Empty);
    }

    private void OpenServerTasks(string category, string taskCode)
    {
        activeTaskCategory = NormalizeServerTaskCategory(category);
        pendingServerTaskCode = (taskCode ?? string.Empty).Trim();
        string message = serverTaskListRunning ? "正在同步服务器任务..." : "正在读取服务器任务...";
        ShowTaskPanel(activeTaskCategory, message);
        if (!serverTaskListRunning)
        {
            StartCoroutine(GetServerTasks());
        }
    }

    private IEnumerator GetServerTasks(string claimFailureMessage = null)
    {
        serverTaskListRunning = true;
        UnityWebRequest request = CreateAuthorizedGet("/tasks");
        if (request == null)
        {
            serverTaskListRunning = false;
            ShowTaskPanel(activeTaskCategory, BuildTaskRefreshStatus(claimFailureMessage, false, requestCreationError));
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                serverTaskListRunning = false;
                yield break;
            }

            ServerTaskListApiResponse response = ParseJson<ServerTaskListApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                serverTaskListRunning = false;
                string error = response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "任务返回数据格式错误")
                    : GetApiError(response);
                ShowTaskPanel(activeTaskCategory, BuildTaskRefreshStatus(claimFailureMessage, false, error));
                yield break;
            }

            currentServerTasks = response.data.tasks ?? new ServerTaskResponse[0];
            if (!string.IsNullOrEmpty(pendingServerTaskCode))
            {
                for (int i = 0; i < currentServerTasks.Length; i++)
                {
                    ServerTaskResponse target = currentServerTasks[i];
                    if (target != null && string.Equals(target.taskCode, pendingServerTaskCode, StringComparison.OrdinalIgnoreCase))
                    {
                        activeTaskCategory = GetServerTaskDisplayCategory(target);
                        break;
                    }
                }
            }
            serverTaskListRunning = false;
            string period = activeTaskCategory == "main" ? response.data.weeklyPeriodKey : response.data.dailyPeriodKey;
            ShowTaskPanel(activeTaskCategory, BuildTaskRefreshStatus(
                claimFailureMessage,
                true,
                $"服务器周期 {period} · {response.data.businessTime}"));
        }
    }

    private static bool ShouldRefreshServerTasksAfterClaimFailure(int code)
    {
        return code == 6601 || code == 6602 || code == 6604 || code == 6605;
    }

    private static string BuildTaskRefreshStatus(string claimFailureMessage, bool refreshed, string detail)
    {
        if (string.IsNullOrEmpty(claimFailureMessage))
        {
            return refreshed ? detail : "任务同步失败：" + detail;
        }

        return refreshed
            ? "领取失败：" + claimFailureMessage + "；任务列表已刷新"
            : "领取失败：" + claimFailureMessage + "；任务列表刷新失败：" + detail;
    }

    private TaskDisplayItem[] BuildServerTaskItems(string category)
    {
        List<TaskDisplayItem> result = new List<TaskDisplayItem>();
        HashSet<string> displayedTaskKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string displayCategory = NormalizeServerTaskCategory(category);
        List<ServerTaskResponse> orderedTasks = new List<ServerTaskResponse>();
        if (!string.IsNullOrEmpty(pendingServerTaskCode))
        {
            for (int i = 0; i < currentServerTasks.Length; i++)
            {
                ServerTaskResponse task = currentServerTasks[i];
                if (task != null
                    && string.Equals(task.taskCode, pendingServerTaskCode, StringComparison.OrdinalIgnoreCase)
                    && MatchesServerTaskCategory(task, displayCategory))
                {
                    orderedTasks.Add(task);
                    break;
                }
            }
        }
        for (int i = 0; i < currentServerTasks.Length; i++)
        {
            if (currentServerTasks[i] != null && !orderedTasks.Contains(currentServerTasks[i]))
            {
                orderedTasks.Add(currentServerTasks[i]);
            }
        }

        for (int i = 0; i < orderedTasks.Count; i++)
        {
            ServerTaskResponse task = orderedTasks[i];
            if (task == null || !MatchesServerTaskCategory(task, displayCategory))
            {
                continue;
            }

            string taskKey = task.id > 0L
                ? "id:" + task.id
                : "code:" + (task.taskCode ?? string.Empty).Trim();
            int separatorIndex = taskKey.IndexOf(':');
            if (separatorIndex >= 0 && string.IsNullOrEmpty(taskKey.Substring(separatorIndex + 1)))
            {
                taskKey = "index:" + i;
            }
            if (!displayedTaskKeys.Add(taskKey))
            {
                continue;
            }

            string rewardSummary = BuildServerTaskRewardSummary(task);

            string description = BuildServerTaskDescription(task);
            if (!string.IsNullOrEmpty(pendingServerTaskCode)
                && string.Equals(task.taskCode, pendingServerTaskCode, StringComparison.OrdinalIgnoreCase))
            {
                description = "【当前指引目标】\n" + description;
            }

            result.Add(new TaskDisplayItem(
                string.IsNullOrEmpty(task.taskCode) ? task.id.ToString() : task.taskCode,
                string.IsNullOrEmpty(task.taskName) ? "服务器任务" : task.taskName,
                description,
                ClampLongToInt(task.progressAmount, 0),
                ClampLongToInt(task.targetAmount, 1),
                "Experience",
                "经验",
                ClampLongToInt(task.experienceReward, 0),
                string.Equals(displayCategory, "daily", StringComparison.OrdinalIgnoreCase),
                null,
                true,
                task.id,
                task.status,
                rewardSummary));
        }

        return result.ToArray();
    }

    private void BeginClaimServerTask(TaskDisplayItem task)
    {
        if (task == null || task.serverTaskId <= 0 || serverTaskClaimRunning)
        {
            return;
        }

        StartCoroutine(ClaimServerTask(task.serverTaskId));
    }

    private IEnumerator ClaimServerTask(long playerTaskId)
    {
        serverTaskClaimRunning = true;
        ShowTaskPanel(activeTaskCategory, "正在领取任务经验...");
        if (!TryCreateAuthorizedPost($"/tasks/{playerTaskId}/claim", "{}", out UnityWebRequest request, out string error))
        {
            serverTaskClaimRunning = false;
            ShowTaskPanel(activeTaskCategory, error);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                serverTaskClaimRunning = false;
                yield break;
            }

            ServerTaskClaimApiResponse response = ParseJson<ServerTaskClaimApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                serverTaskClaimRunning = false;
                string message = response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "领取失败")
                    : GetApiError(response);
                if (response != null && ShouldRefreshServerTasksAfterClaimFailure(response.code))
                {
                    ShowTaskPanel(activeTaskCategory, "领取失败：" + message + "；正在刷新任务列表...");
                    yield return GetServerTasks(message);
                }
                else
                {
                    ShowTaskPanel(activeTaskCategory, "领取失败：" + message);
                }
                yield break;
            }

            if (response.data.experienceGrant == null || !response.data.experienceGrant.replayed)
            {
                ApplyExperienceGrant(response.data.experienceGrant);
            }
            UpdateServerTask(response.data.task);
            yield return RefreshAccountSnapshot(string.Empty);
            yield return ApplyServerTaskRewardGrants(response.data.rewardGrants);
            serverTaskClaimRunning = false;
            long granted = response.data.experienceGrant != null ? response.data.experienceGrant.grantedExperience : 0L;
            ShowTaskPanel(activeTaskCategory, granted > 0 ? $"领取成功：经验 +{granted}" : "任务奖励已领取");
        }
    }

    private static int ClampLongToInt(long value, int minimum)
    {
        long clamped = Math.Max((long)minimum, value);
        return clamped > int.MaxValue ? int.MaxValue : (int)clamped;
    }

    private static string NormalizeServerTaskCategory(string category)
    {
        if (string.Equals(category, "main", StringComparison.OrdinalIgnoreCase)) return "main";
        if (string.Equals(category, "growth", StringComparison.OrdinalIgnoreCase)) return "growth";
        if (string.Equals(category, "activity", StringComparison.OrdinalIgnoreCase)) return "activity";
        return "daily";
    }

    private static bool MatchesServerTaskCategory(ServerTaskResponse task, string category)
    {
        return task != null
            && string.Equals(
                GetServerTaskDisplayCategory(task),
                NormalizeServerTaskCategory(category),
                StringComparison.OrdinalIgnoreCase);
    }

    private static string GetServerTaskDisplayCategory(ServerTaskResponse task)
    {
        if (task == null)
        {
            return "daily";
        }

        string taskCategory = (task.taskCategory ?? string.Empty).Trim().ToUpperInvariant();
        string periodType = (task.periodType ?? string.Empty).Trim().ToUpperInvariant();
        switch (taskCategory)
        {
            case "MAIN":
                return "main";
            case "GROWTH":
            case "ACHIEVEMENT":
                return "growth";
            case "ACTIVITY":
                return "activity";
            case "ROUTINE":
                return periodType == "WEEKLY" ? "main"
                    : periodType == "LIFETIME" ? "growth"
                    : "daily";
        }

        if (periodType == "WEEKLY") return "main";
        if (periodType == "LIFETIME") return "growth";
        if (periodType == "DAILY") return "daily";
        return "daily";
    }

    private static string BuildServerTaskDescription(ServerTaskResponse task)
    {
        if (task == null) return string.Empty;
        string description = task.description ?? string.Empty;
        if (task.targetSceneId > 0 && !string.IsNullOrEmpty(task.targetSceneName))
        {
            description += $"\n场景：{task.targetSceneName}";
        }
        if (!string.IsNullOrEmpty(task.targetItemName))
        {
            description += $"\n目标：{task.targetItemName}";
        }
        return description;
    }

    private static string BuildServerTaskRewardSummary(ServerTaskResponse task)
    {
        List<string> rewards = new List<string>();
        if (task != null && task.fixedRewards != null)
        {
            for (int i = 0; i < task.fixedRewards.Length; i++)
            {
                TaskRewardSnapshot reward = task.fixedRewards[i];
                if (reward == null) continue;
                string name = string.IsNullOrEmpty(reward.assetName) ? reward.assetKey : reward.assetName;
                if (!string.IsNullOrEmpty(name)) rewards.Add(name + " x" + Math.Max(0, reward.quantity));
            }
        }

        if (task != null
            && (!string.IsNullOrEmpty(task.randomNpcPoolName)
                || (task.randomNpcCandidates != null && task.randomNpcCandidates.Length > 0)))
        {
            string randomNpcSummary = BuildRandomNpcCandidateSummary(task);
            if (!string.IsNullOrEmpty(randomNpcSummary))
            {
                rewards.Add(randomNpcSummary);
            }
            else if (!string.IsNullOrEmpty(task.randomNpcPoolName))
            {
                rewards.Add(task.randomNpcPoolName
                    + (task.randomNpcDrawCount > 0 ? $" x{task.randomNpcDrawCount}" : string.Empty));
            }
        }

        if (task != null && task.experienceReward > 0) rewards.Add("经验 x" + task.experienceReward);
        return rewards.Count == 0 ? "无固定奖励" : string.Join("、", rewards.ToArray());
    }

    private static string BuildRandomNpcCandidateSummary(ServerTaskResponse task)
    {
        if (task == null || task.randomNpcCandidates == null || task.randomNpcCandidates.Length == 0)
        {
            return string.Empty;
        }

        List<string> candidates = new List<string>();
        int shown = 0;
        for (int i = 0; i < task.randomNpcCandidates.Length; i++)
        {
            TaskRewardSnapshot candidate = task.randomNpcCandidates[i];
            if (candidate == null)
            {
                continue;
            }

            string name = string.IsNullOrEmpty(candidate.assetName)
                ? candidate.assetKey
                : candidate.assetName;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }
            if (shown >= 8)
            {
                break;
            }

            string description = name + " x" + Math.Max(0, candidate.quantity);
            if (!string.IsNullOrEmpty(candidate.assetQuality))
            {
                description += " [" + candidate.assetQuality + "]";
            }
            float probabilityPercent = candidate.probabilityPercent;
            if (probabilityPercent <= 0f && candidate.probabilityUnits > 0)
            {
                probabilityPercent = candidate.probabilityUnits / 1000000f;
            }
            if (probabilityPercent > 0f)
            {
                description += $" ({probabilityPercent:0.##}%)";
            }

            TaskDuplicateCompensationSnapshot compensation = candidate.duplicateCompensation;
            if (compensation != null && compensation.quantity > 0)
            {
                string compensationName = string.IsNullOrEmpty(compensation.itemName)
                    ? compensation.itemKey
                    : compensation.itemName;
                if (!string.IsNullOrEmpty(compensationName))
                {
                    description += " [重复→" + compensationName + " x"
                        + Math.Max(0, compensation.quantity) + "]";
                }
            }

            candidates.Add(description);
            shown++;
        }

        if (candidates.Count == 0)
        {
            return string.Empty;
        }

        string poolName = string.IsNullOrEmpty(task.randomNpcPoolName)
            ? "随机NPC"
            : task.randomNpcPoolName;
        string drawCount = task.randomNpcDrawCount > 0
            ? " x" + task.randomNpcDrawCount
            : string.Empty;
        string suffix = task.randomNpcCandidates.Length > shown ? " 等" : string.Empty;
        return poolName + drawCount + "：" + string.Join(" / ", candidates.ToArray()) + suffix;
    }

    private IEnumerator ApplyServerTaskRewardGrants(TaskRewardGrant[] grants)
    {
        bool needsInventory = grants == null;
        bool needsNpc = grants == null;
        bool needsEquipment = grants == null;
        List<string> names = new List<string>();
        if (grants != null)
        {
            for (int i = 0; i < grants.Length; i++)
            {
                TaskRewardGrant grant = grants[i];
                if (grant == null) continue;
                string type = string.IsNullOrEmpty(grant.grantedType)
                    ? (grant.grantedAssetCategory ?? string.Empty).Trim().ToUpperInvariant()
                    : grant.grantedType.Trim().ToUpperInvariant();
                if (type == "ITEM") needsInventory = true;
                if (type == "NPC") needsNpc = true;
                if (type == "EQUIPMENT") needsEquipment = true;
                if (grant.duplicateNpcConverted) needsInventory = true;
                string name = !string.IsNullOrEmpty(grant.grantedAssetName)
                    ? grant.grantedAssetName
                    : (!string.IsNullOrEmpty(grant.selectedNpcName)
                        ? grant.selectedNpcName
                        : grant.grantedAssetKey);
                if (!string.IsNullOrEmpty(name)) names.Add(name + " x" + Math.Max(0L, grant.grantedQuantity));
            }
        }

        if (names.Count > 0) SetStatus("任务到账：" + string.Join("、", names.ToArray()));
        yield return RefreshTaskGrantState(needsInventory, needsNpc, needsEquipment);
    }

    private IEnumerator RefreshTaskGrantState(bool needsInventory, bool needsNpc, bool needsEquipment)
    {
        List<string> synced = new List<string>();
        List<string> failed = new List<string>();
        if (needsInventory) yield return RefreshCardDrawInventoryState(synced, failed);
        if (needsNpc) yield return RefreshCardDrawNpcState(synced, failed);
        if (needsEquipment) yield return RefreshCardDrawEquipmentState(synced, failed);
        if (failed.Count > 0) SetStatus("任务奖励同步失败：" + string.Join("、", failed.ToArray()));
    }

    private void UpdateServerTask(ServerTaskResponse updated)
    {
        if (updated == null)
        {
            return;
        }

        for (int i = 0; i < currentServerTasks.Length; i++)
        {
            if (currentServerTasks[i] != null && currentServerTasks[i].id == updated.id)
            {
                currentServerTasks[i] = updated;
                return;
            }
        }
    }

    private void ApplyExperienceGrant(ExperienceGrantResponse grant)
    {
        if (grant == null)
        {
            return;
        }

        PlayerPrefs.SetInt("PlayerLv", Mathf.Max(1, grant.afterLevel));
        PlayerPrefs.SetString("PlayerTotalExperience", Math.Max(0L, grant.afterExperience).ToString());
        PlayerPrefs.SetInt("PlayerCurrentLevelExperience", ClampLongToInt(grant.currentLevelExperience, 0));
        PlayerPrefs.SetInt("PlayerRequiredExperience", ClampLongToInt(grant.requiredExperienceForNextLevel, 0));
        PlayerPrefs.SetInt("PlayerExperienceProgressBps", Mathf.Clamp(grant.experienceProgressBps, 0, 10000));
        PlayerPrefs.SetInt("PlayerLevelCurveRevision", ClampLongToInt(grant.curveRevision, 0));
        PlayerPrefs.SetInt("PlayerMaxLevel", grant.maxLevel ? 1 : 0);
        PlayerPrefs.Save();
        WeChatPlatformBridge.SyncFriendLevel(grant.afterLevel);
        RefreshHomeResourceMetrics();
    }

    private void OpenServerSignIn(string planCode)
    {
        string normalizedPlanCode = (planCode ?? string.Empty).Trim();
        serverSignPlanCode = string.IsNullOrEmpty(normalizedPlanCode) ? "MONTHLY" : normalizedPlanCode;
        _dailySignResetScroll = true;
        _dailySignFocusDay = 0;
        _activitySignPage = 0;
        serverSignInOverview = null;
        serverSignOverviewReady = false;
        serverSignFocusCurrentDay = true;
        serverSignStatus = "正在读取服务器签到配置...";
        ShowCurrentServerSignPanel();
        if (!serverSignOverviewRunning)
        {
            StartCoroutine(GetServerSignInOverview(null, true));
        }
    }

    private void ShowCurrentServerSignPanel()
    {
        if (!string.Equals(serverSignPlanCode, "MONTHLY", StringComparison.OrdinalIgnoreCase))
        {
            ShowNewbieSignPanel(0, serverSignStatus);
        }
        else
        {
            ShowDailySignPanel(0, false, serverSignStatus);
        }
    }

    private void RefreshVisibleServerSignPanel()
    {
        if (signPanelObject == null || !signPanelObject.activeInHierarchy)
        {
            return;
        }

        HomePageScenePanel panel = signPanelObject.GetComponent<HomePageScenePanel>();
        if (panel != null)
        {
            if (panel.PanelType == HomePageScenePanelType.NewbieSign)
            {
                ConfigureActivitySignPanel(panel.PanelRoot, serverSignStatus);
            }
            else
            {
                ConfigureServerSignScenePanel(panel.PanelRoot, serverSignPlanCode, serverSignStatus);
            }
        }
    }

    private void RefreshServerSignInStatus()
    {
        if (HasAuthSession() && isActiveAndEnabled && !serverSignOverviewRunning && !serverSignMutationRunning)
        {
            HideSignMakeupConfirmation();
            StartCoroutine(GetServerSignInOverview());
        }
    }

    private IEnumerator GetServerSignInOverview(string resultStatus = null, bool focusCurrentDay = false)
    {
        while (serverSignOverviewRunning)
        {
            yield return null;
        }
        if (!HasAuthSession())
        {
            yield break;
        }

        serverSignOverviewRunning = true;
        serverSignOverviewReady = false;
        RefreshVisibleServerSignPanel();
        string requestToken = GameSession.Token;
        try
        {
            UnityWebRequest request = CreateAuthorizedGet("/sign-in/overview");
            if (request == null)
            {
                serverSignOverviewRunning = false;
                serverSignStatus = string.IsNullOrEmpty(resultStatus) ? requestCreationError : resultStatus + "；概览刷新失败：" + requestCreationError;
                RefreshVisibleServerSignPanel();
                yield break;
            }

            using (request)
            {
                yield return request.SendWebRequest();
                if (!string.Equals(requestToken, GameSession.Token, StringComparison.Ordinal))
                {
                    yield break;
                }
                if (HandleUnauthorizedResponse(request))
                {
                    serverSignOverviewRunning = false;
                    yield break;
                }

                SignInOverviewApiResponse response = ParseJson<SignInOverviewApiResponse>(request.downloadHandler.text);
                if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
                {
                    serverSignOverviewRunning = false;
                    bool versionRejected = request.responseCode == 426 || response != null && response.code == 6009;
                    serverSignStatus = versionRejected
                        ? "当前客户端版本不支持签到配置，请更新客户端"
                        : "签到同步失败：" + (response == null
                            ? ApiRequestUtility.GetErrorMessage(request, "返回数据格式错误")
                            : GetApiError(response));
                    if (versionRejected)
                    {
                        Debug.LogWarning(
                            "签到接口拒绝当前客户端版本：HTTP=" + request.responseCode
                            + ", code=" + (response == null ? 0 : response.code)
                            + ", clientVersion=" + ApiRuntimeConfig.GetClientVersionCode(),
                            this);
                    }
                    if (!string.IsNullOrEmpty(resultStatus)) serverSignStatus = resultStatus + "；" + serverSignStatus;
                    RefreshVisibleServerSignPanel();
                    yield break;
                }

                serverSignInOverview = response.data;
                serverSignOverviewReady = true;
                serverSignOverviewRunning = false;
                SignInPlanResponse plan = FindServerSignPlan(serverSignPlanCode);
                if (plan == null)
                {
                    serverSignStatus = !string.IsNullOrEmpty(resultStatus)
                        ? resultStatus + "；当前签到计划未开放"
                        : "当前签到计划未开放：" + serverSignPlanCode;
                    Debug.LogWarning(
                        "签到接口未返回请求的计划：requested=" + serverSignPlanCode
                        + ", plans=" + FormatSignInPlanCodes(serverSignInOverview.plans),
                        this);
                }
                else if (plan.days == null || plan.days.Length == 0)
                {
                    serverSignStatus = !string.IsNullOrEmpty(resultStatus)
                        ? resultStatus + "；当前签到计划没有 days 数据"
                        : "当前签到计划没有可展示的签到数据";
                    Debug.LogWarning(
                        "签到接口返回空 days：plan=" + plan.planCode
                        + ", rewardMode=" + plan.rewardMode
                        + ", rewardWindowDays=" + plan.rewardWindowDays,
                        this);
                }
                else if (!IsSignInPlanVersionSupported(plan))
                {
                    serverSignStatus = !string.IsNullOrEmpty(resultStatus)
                        ? resultStatus + "；签到配置需要更新客户端"
                        : "签到配置需要更新客户端";
                    Debug.LogWarning(
                        "签到计划版本高于客户端：plan=" + plan.planCode
                        + ", contractVersion=" + plan.contractVersion
                        + ", minimumClientVersionCode=" + plan.minimumClientVersionCode
                        + ", clientVersion=" + ApiRuntimeConfig.GetClientVersionCode(),
                        this);
                }
                else
                {
                    serverSignStatus = !string.IsNullOrEmpty(resultStatus) ? resultStatus : "签到配置已同步";
                }
                if ((focusCurrentDay || serverSignFocusCurrentDay) && plan != null && IsMonthlySignPlan(serverSignPlanCode) && plan.currentDay > 0)
                {
                    _dailySignFocusDay = plan.currentDay;
                }
                serverSignFocusCurrentDay = false;
                RefreshVisibleServerSignPanel();
                RefreshSignInEntryBadges();
            }
        }
        finally
        {
            serverSignOverviewRunning = false;
            RefreshVisibleServerSignPanel();
            RefreshSignInEntryBadges();
        }
    }

    private void ConfigureServerSignScenePanel(Transform root, string planCode, string status)
    {
        serverSignPlanCode = planCode;
        SignInPlanResponse plan = FindServerSignPlan(planCode);
        SignInDayResponse[] days = plan == null || plan.days == null ? new SignInDayResponse[0] : plan.days;
        if (plan != null && !IsSignInPlanVersionSupported(plan))
        {
            days = new SignInDayResponse[0];
        }
        if (days.Length > 0)
        {
            List<SignInDayResponse> availableDays = new List<SignInDayResponse>();
            for (int index = 0; index < days.Length; index++)
            {
                if (days[index] != null) availableDays.Add(days[index]);
            }
            days = availableDays.ToArray();
        }
        int cardCount = days.Length;
        bool activityPlan = FindSceneTransform(root, "ActivityType") != null;
        string prefix = activityPlan ? "NewbieSignCard" : "DailySignCard";
        string statusName = activityPlan ? "NewbieSignStatus" : "DailySignStatus";
        List<Transform> activityCards = activityPlan ? PrepareActivitySignSceneCards(root, cardCount) : null;
        List<Transform> dailyCards = IsMonthlySignPlan(planCode) ? PrepareDailySignSceneCards(root, cardCount) : null;

        ConfigureServerSignRefreshButton(root, planCode);
        if (activityPlan)
        {
            ConfigureActivityTypeButtons(root);
            ConfigureActivityDetails(root, plan);
        }

        if (plan == null)
        {
            int missingCardCount = activityCards != null ? activityCards.Count : dailyCards != null ? dailyCards.Count : cardCount;
            for (int i = 0; i < missingCardCount; i++)
            {
                Transform missingCard = activityCards != null ? activityCards[i]
                    : dailyCards != null ? dailyCards[i] : FindSceneTransform(root, prefix + (i + 1));
                if (missingCard != null)
                {
                    missingCard.gameObject.SetActive(false);
                }
            }
            SetSceneText(root, statusName, string.IsNullOrEmpty(status) ? "当前签到计划未开放" : status);
            if (activityCards != null) ConfigureActivityPaging(root, 1);
            return;
        }

        if (dailyCards != null)
        {
            for (int index = cardCount; index < dailyCards.Count; index++)
            {
                dailyCards[index].gameObject.SetActive(false);
            }
        }

        int pageSize = activityCards != null ? Mathf.Max(1, _activitySignCardPositions.Count) : cardCount;
        int pageCount = activityCards == null ? 1 : Mathf.Max(1, Mathf.CeilToInt(days.Length / (float)pageSize));
        if (activityCards != null)
        {
            _activitySignPage = Mathf.Clamp(_activitySignPage, 0, pageCount - 1);
            ConfigureActivityPaging(root, pageCount);
        }

        int firstDayIndex = activityCards == null ? 0 : _activitySignPage * pageSize;
        int displayCount = activityCards == null ? cardCount : pageSize;
        for (int i = 0; i < displayCount; i++)
        {
            Transform card = activityCards != null
                ? (firstDayIndex + i < activityCards.Count ? activityCards[firstDayIndex + i] : null)
                : dailyCards != null ? (i < dailyCards.Count ? dailyCards[i] : null)
                : FindSceneTransform(root, prefix + (i + 1));
            if (card == null)
            {
                continue;
            }

            int dayIndex = firstDayIndex + i;
            bool visible = dayIndex < days.Length && days[dayIndex] != null;
            card.gameObject.SetActive(visible);
            if (!visible)
            {
                continue;
            }

            if (activityCards != null && card is RectTransform activityCardRect)
            {
                activityCardRect.anchoredPosition = _activitySignCardPositions[i];
            }

            SignInDayResponse day = days[dayIndex];
            string rewardName;
            string rewardAmount;
            BuildSignRewardDisplay(day.rewards, out rewardName, out rewardAmount);
            bool claimed = day.claimed || string.Equals(day.status, "CLAIMED", StringComparison.OrdinalIgnoreCase);
            bool highlighted = !claimed && (day.canClaim || CanMakeupServerSignDay(plan, day));
            ConfigureSignSceneCard(card, day.rewardDay, rewardName, rewardAmount, claimed, highlighted);
            ApplySignRewardIcon(card, day.rewards);
            if (IsMonthlySignPlan(planCode))
            {
                SetSceneObjectActive(card, "ClaimedChip", false);
            }
            else
            {
                SetSceneObjectActive(card, "ClaimedChip", true);
            }
            SetSceneObjectActive(card, "ClaimedText", true);
            SetSceneText(card, "ClaimedText", GetServerSignDayStatus(plan, day));

            Button cardButton = card.GetComponent<Button>();
            if (cardButton == null)
            {
                cardButton = card.gameObject.AddComponent<Button>();
                cardButton.targetGraphic = card.GetComponent<Image>();
                cardButton.transition = Selectable.Transition.None;
            }

            SignInDayResponse capturedDay = day;
            cardButton.interactable = serverSignOverviewReady && !serverSignOverviewRunning && !serverSignMutationRunning && highlighted;
            BindSceneButton(cardButton, () => BeginServerSignClaim(plan.planCode, capturedDay));
        }

        int claimedCount = 0;
        for (int i = 0; i < days.Length; i++)
        {
            if (days[i] != null && days[i].claimed)
            {
                claimedCount++;
            }
        }

        if (dailyCards != null) FocusDailySignDay(root, days);
        string mutation = serverSignMutationRunning ? "正在提交..." : status;
        string cost = !plan.allowMakeup ? " · 补签未开放" : plan.makeupCost == null
            ? " · 暂无补签费用配置"
            : " · 补签消耗 " + FormatSignInCost(plan.makeupCost);
        SetSceneText(
            root,
            statusName,
            $"{plan.planName}  已领 {claimedCount}/{days.Length}{cost}  {mutation}");
    }

    private List<Transform> PrepareDailySignSceneCards(Transform root, int count)
    {
        ScrollRect scroll = FindSceneComponent<ScrollRect>(root, "DailySignCardP");
        RectTransform content = scroll != null ? scroll.content : null;
        RectTransform viewport = scroll != null ? scroll.viewport : null;
        if (content == null || viewport == null)
        {
            Debug.LogWarning("DailySignCardP requires its scene ScrollRect, viewport and content.", this);
            return new List<Transform>();
        }

        if (_dailySignContent != content || _dailySignCardTemplate == null)
        {
            _dailySignContent = content;
            _dailySignCards.Clear();
            _dailySignCardTemplate = content.Find("DailySignCard1") as RectTransform;
            _dailySignResetScroll = true;
        }
        if (_dailySignCardTemplate == null)
        {
            Debug.LogWarning("DailySignCard1 is missing; sign-in cards cannot be generated.", this);
            return _dailySignCards;
        }

        for (int index = 0; index < content.childCount; index++)
        {
            Transform child = content.GetChild(index);
            const string prefix = "DailySignCard";
            if (child.name.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(child.name.Substring(prefix.Length), out int cardNumber)
                && cardNumber > 0)
            {
                child.gameObject.SetActive(false);
            }
        }

        while (_dailySignCards.Count < count)
        {
            RectTransform card = Instantiate(_dailySignCardTemplate, content, false);
            card.name = "DailySignRuntimeCard" + (_dailySignCards.Count + 1);
            _dailySignCards.Add(card);
        }

        Vector2 origin = _dailySignCardTemplate.anchoredPosition;
        GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
        bool useSceneGrid = grid != null && grid.enabled;
        float columnStride = 0f;
        float rowStride = 0f;
        int rows = Mathf.CeilToInt(count / (float)DailySignColumns);
        float height = 0f;
        if (!useSceneGrid)
        {
            float cardWidth = _dailySignCardTemplate.rect.width * Mathf.Abs(_dailySignCardTemplate.localScale.x);
            float cardHeight = _dailySignCardTemplate.rect.height * Mathf.Abs(_dailySignCardTemplate.localScale.y);
            RectTransform secondCard = content.Find("DailySignCard2") as RectTransform;
            float availableStride = (viewport.rect.width - Mathf.Max(0f, origin.x) * 2f - cardWidth) / (DailySignColumns - 1);
            columnStride = secondCard != null
                ? Mathf.Min(secondCard.anchoredPosition.x - origin.x, availableStride)
                : availableStride;
            columnStride = Mathf.Max(cardWidth, columnStride);
            float topPadding = Mathf.Max(0f, -origin.y);
            rowStride = Mathf.Max(cardHeight,
                (viewport.rect.height - topPadding * 2f - cardHeight) / (DailySignVisibleRows - 1));
            height = rows == 0 ? 0f : topPadding * 2f + cardHeight + (rows - 1) * rowStride;
        }
        float scrollOffset = _dailySignResetScroll ? 0f : content.anchoredPosition.y;

        for (int index = 0; index < _dailySignCards.Count; index++)
        {
            RectTransform card = _dailySignCards[index] as RectTransform;
            if (card == null) continue;
            bool visible = index < count;
            card.gameObject.SetActive(visible);
            if (visible && !useSceneGrid)
            {
                card.anchoredPosition = origin + new Vector2(
                    (index % DailySignColumns) * columnStride,
                    -(index / DailySignColumns) * rowStride);
            }
        }

        if (useSceneGrid)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            height = LayoutUtility.GetPreferredHeight(content);
        }
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(viewport.rect.height, height));
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        float maximumOffset = Mathf.Max(0f, content.rect.height - viewport.rect.height);
        scroll.StopMovement();
        scroll.horizontal = false;
        scroll.vertical = maximumOffset > 0.01f;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        content.anchoredPosition = new Vector2(content.anchoredPosition.x,
            Mathf.Clamp(scrollOffset, 0f, maximumOffset));
        _dailySignResetScroll = false;
        return _dailySignCards;
    }

    private static bool IsSignInPlanVersionSupported(SignInPlanResponse plan)
    {
        if (plan == null)
        {
            return false;
        }

        return (plan.contractVersion <= 0 || plan.contractVersion <= SupportedSignInContractVersion)
            && (plan.minimumClientVersionCode <= 0
                || plan.minimumClientVersionCode <= ApiRuntimeConfig.GetClientVersionCode());
    }

    private static bool IsMonthlySignPlan(string planCode)
    {
        return string.Equals(planCode, "MONTHLY", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatSignInPlanCodes(SignInPlanResponse[] plans)
    {
        if (plans == null || plans.Length == 0) return "<empty>";
        List<string> codes = new List<string>();
        for (int index = 0; index < plans.Length; index++)
        {
            if (plans[index] != null && !string.IsNullOrWhiteSpace(plans[index].planCode))
            {
                codes.Add(plans[index].planCode);
            }
        }
        return codes.Count == 0 ? "<empty>" : string.Join(",", codes.ToArray());
    }

    private static bool IsLimitedActivitySignPlan(SignInPlanResponse plan)
    {
        return plan != null && !string.IsNullOrWhiteSpace(plan.planCode)
            && !string.Equals(plan.planCode, "NEWBIE", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(plan.planCode, "MONTHLY", StringComparison.OrdinalIgnoreCase)
            && (!string.IsNullOrWhiteSpace(plan.activityCode)
                || string.Equals(plan.planType, "ACTIVITY_WINDOW", StringComparison.OrdinalIgnoreCase)
                || string.Equals(plan.planType, "ACTIVITY", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsActivitySignPlan(SignInPlanResponse plan)
    {
        return plan != null && (string.Equals(plan.planCode, "NEWBIE", StringComparison.OrdinalIgnoreCase)
            || IsLimitedActivitySignPlan(plan));
    }

    private void ConfigureActivitySignPanel(Transform root, string status)
    {
        SignInPlanResponse selected = FindServerSignPlan(serverSignPlanCode);
        if (serverSignOverviewReady && !IsActivitySignPlan(selected))
        {
            selected = GetActivityPlan(0);
            serverSignPlanCode = selected == null ? string.Empty : selected.planCode;
            _activitySignPage = 0;
        }
        ConfigureServerSignScenePanel(root, serverSignPlanCode, status);
    }

    private List<Transform> PrepareActivitySignSceneCards(Transform root, int count)
    {
        Transform parent = FindSceneTransform(root, "SignCardP");
        if (parent == null)
        {
            Debug.LogWarning("SignCardP is missing; activity sign-in cards cannot be generated.", this);
            return _activitySignCards;
        }

        RectTransform parentRect = parent as RectTransform;
        RectTransform template = parent.Find("NewbieSignCard1") as RectTransform;
        if (parentRect == null || template == null)
        {
            Debug.LogWarning("SignCardP/NewbieSignCard1 is missing; activity sign-in cards cannot be generated.", this);
            return _activitySignCards;
        }

        if (_activitySignCardParent != parentRect || _activitySignCardTemplate == null)
        {
            _activitySignCardParent = parentRect;
            _activitySignCardTemplate = template;
            _activitySignCards.Clear();
            _activitySignCardPositions.Clear();
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child.name.StartsWith("NewbieSignCard", StringComparison.Ordinal))
                {
                    _activitySignCards.Add(child);
                    _activitySignCardPositions.Add((child as RectTransform)?.anchoredPosition ?? Vector2.zero);
                }
            }
            if (_activitySignCardPositions.Count == 0)
            {
                _activitySignCardPositions.Add(template.anchoredPosition);
            }
        }

        while (_activitySignCards.Count < count)
        {
            RectTransform card = Instantiate(_activitySignCardTemplate, _activitySignCardParent, false);
            card.name = "ActivitySignRuntimeCard" + (_activitySignCards.Count + 1);
            _activitySignCards.Add(card);
        }

        for (int index = 0; index < _activitySignCards.Count; index++)
        {
            Transform card = _activitySignCards[index];
            card.gameObject.SetActive(false);
            RectTransform cardRect = card as RectTransform;
            if (cardRect != null)
            {
                int positionIndex = Mathf.Min(index, _activitySignCardPositions.Count - 1);
                Vector2 position = _activitySignCardPositions[positionIndex];
                if (index >= _activitySignCardPositions.Count && _activitySignCardPositions.Count > 1)
                {
                    Vector2 first = _activitySignCardPositions[0];
                    Vector2 second = _activitySignCardPositions[1];
                    position = first + (second - first) * index;
                }
                cardRect.anchoredPosition = position;
            }
        }

        return _activitySignCards;
    }

    private void ConfigureActivityTypeButtons(Transform root)
    {
        Transform typeRoot = FindSceneTransform(root, "ActivityType");
        if (typeRoot == null)
        {
            return;
        }

        for (int index = 0; index < typeRoot.childCount; index++)
        {
            Transform child = typeRoot.GetChild(index);
            if (child.name.StartsWith("ActivityBtn", StringComparison.Ordinal))
            {
                child.gameObject.SetActive(false);
            }
        }

        SignInPlanResponse[] plans = serverSignInOverview == null ? null : serverSignInOverview.plans;
        int activityCount = 0;
        if (plans != null)
        {
            for (int index = 0; index < plans.Length; index++)
            {
                if (plans[index] != null && (plans[index].planCode == "NEWBIE" || IsLimitedActivitySignPlan(plans[index]))) activityCount++;
            }
        }

        Transform template = typeRoot.Find("ActivityBtn");
        if (template == null)
        {
            Debug.LogWarning("ActivityType/ActivityBtn is missing; activity type buttons cannot be generated.", this);
            return;
        }

        for (int index = 0; index < activityCount; index++)
        {
            Transform buttonObject = index == 0 ? template : FindOrCreateActivityTypeButton(template, typeRoot, index + 1);
            SignInPlanResponse plan = GetActivityPlan(index);
            if (buttonObject == null || plan == null) continue;
            buttonObject.gameObject.SetActive(true);
            if (buttonObject is RectTransform buttonRect && index > 0 && template is RectTransform templateRect)
            {
                buttonRect.anchoredPosition = templateRect.anchoredPosition
                    - new Vector2(0f, index * templateRect.rect.height);
            }
            SetSceneText(buttonObject, "ActivityName", GetActivityName(plan));
            Button button = buttonObject.GetComponent<Button>();
            BindSceneButton(button, () => OpenServerSignIn(plan.planCode));
        }
    }

    private Transform FindOrCreateActivityTypeButton(Transform template, Transform parent, int number)
    {
        Transform existing = parent.Find("ActivityBtn" + number);
        if (existing != null) return existing;
        Transform created = Instantiate(template, parent, false);
        created.name = "ActivityBtn" + number;
        return created;
    }

    private SignInPlanResponse GetActivityPlan(int activityIndex)
    {
        if (serverSignInOverview == null || serverSignInOverview.plans == null) return null;
        int currentIndex = 0;
        for (int index = 0; index < serverSignInOverview.plans.Length; index++)
        {
            SignInPlanResponse plan = serverSignInOverview.plans[index];
            if (plan == null || !IsActivitySignPlan(plan)) continue;
            if (currentIndex++ == activityIndex) return plan;
        }
        return null;
    }

    private void ConfigureActivityDetails(Transform root, SignInPlanResponse plan)
    {
        if (plan == null) return;
        Transform activityPanel = FindSceneTransform(root, "ActivityPanel");
        Transform detailRoot = activityPanel == null ? root : FindSceneTransform(activityPanel, "NewbieSignPanel");
        if (detailRoot == null) detailRoot = root;
        SetSceneText(detailRoot, "ActivityName", GetActivityName(plan));
        SetSceneText(detailRoot, "Data", FormatActivityDate(plan));
        SetSceneText(detailRoot, "Explain", BuildActivityDescription(plan));
    }

    private static string GetActivityName(SignInPlanResponse plan)
    {
        if (plan == null) return string.Empty;
        return string.IsNullOrWhiteSpace(plan.activityName) ? plan.planName : plan.activityName;
    }

    private static string FormatActivityDate(SignInPlanResponse plan)
    {
        if (plan == null) return string.Empty;
        string start = string.IsNullOrWhiteSpace(plan.activityStartTime) ? plan.startTime : plan.activityStartTime;
        string end = string.IsNullOrWhiteSpace(plan.activityEndTime) ? plan.endTime : plan.activityEndTime;
        if (string.IsNullOrEmpty(start) && string.Equals(plan.planCode, "NEWBIE", StringComparison.OrdinalIgnoreCase)) start = "首次登录";
        if (string.IsNullOrEmpty(end)) end = plan.expiresAt;
        if (string.IsNullOrEmpty(start)) return end;
        if (string.IsNullOrEmpty(end)) return start;
        return start + " - " + end;
    }

    private static string BuildActivityDescription(SignInPlanResponse plan)
    {
        if (plan == null) return string.Empty;
        if (!string.IsNullOrWhiteSpace(plan.activityDescription)) return plan.activityDescription;
        if (!string.IsNullOrWhiteSpace(plan.description)) return plan.description;
        if (string.Equals(plan.planCode, "NEWBIE", StringComparison.OrdinalIgnoreCase)) return "首次登录后连续签到领取新手奖励";
        return "活动期间每日签到领取服务器配置的活动奖励";
    }

    private void ConfigureActivityPaging(Transform root, int pageCount)
    {
        Transform cardParent = FindSceneTransform(root, "SignCardP");
        if (cardParent == null) return;
        Button previous = FindSceneComponent<Button>(cardParent, "ActivityRewardPreviousButton");
        Button next = FindSceneComponent<Button>(cardParent, "ActivityRewardNextButton");
        if (previous == null || next == null)
        {
            Debug.LogWarning("Activity reward page buttons are missing; add ActivityRewardPreviousButton and ActivityRewardNextButton under SignCardP.", this);
            return;
        }
        previous.gameObject.SetActive(pageCount > 1);
        next.gameObject.SetActive(pageCount > 1);
        previous.interactable = _activitySignPage > 0 && !serverSignMutationRunning && !serverSignOverviewRunning;
        next.interactable = _activitySignPage + 1 < pageCount && !serverSignMutationRunning && !serverSignOverviewRunning;
        BindSceneButton(previous, () => ChangeActivitySignPage(-1));
        BindSceneButton(next, () => ChangeActivitySignPage(1));
    }

    private void ChangeActivitySignPage(int offset)
    {
        if (serverSignMutationRunning || serverSignOverviewRunning) return;
        SignInPlanResponse plan = FindServerSignPlan(serverSignPlanCode);
        int pageSize = Mathf.Max(1, _activitySignCardPositions.Count);
        int count = plan == null || plan.days == null ? 0 : plan.days.Length;
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(count / (float)pageSize));
        _activitySignPage = Mathf.Clamp(_activitySignPage + offset, 0, pageCount - 1);
        RefreshVisibleServerSignPanel();
    }

    private void FocusDailySignDay(Transform root, SignInDayResponse[] days)
    {
        if (_dailySignFocusDay <= 0 || _dailySignCardTemplate == null) return;
        ScrollRect scroll = FindSceneComponent<ScrollRect>(root, "DailySignCardP");
        if (scroll == null || scroll.content == null || scroll.viewport == null) return;

        for (int index = 0; index < days.Length && index < _dailySignCards.Count; index++)
        {
            if (days[index].rewardDay != _dailySignFocusDay) continue;
            RectTransform card = _dailySignCards[index] as RectTransform;
            if (card == null) break;
            float offset = _dailySignCardTemplate.anchoredPosition.y - card.anchoredPosition.y;
            scroll.StopMovement();
            scroll.content.anchoredPosition = new Vector2(scroll.content.anchoredPosition.x,
                Mathf.Clamp(offset, 0f, Mathf.Max(0f, scroll.content.rect.height - scroll.viewport.rect.height)));
            break;
        }
        _dailySignFocusDay = 0;
    }

    private void ConfigureServerSignRefreshButton(Transform root, string planCode)
    {
        Transform panel = FindSceneTransform(root, planCode == "NEWBIE" ? "NewbieSignPanel" : "DailySignPanel");
        if (panel == null) return;
        Button refresh = FindSceneComponent<Button>(panel, "ServerSignRefreshButton");
        if (refresh == null)
        {
            return;
        }
        refresh.interactable = !serverSignMutationRunning && !serverSignOverviewRunning;
        BindSceneButton(refresh, RefreshServerSignInStatus);
    }


    private SignInPlanResponse FindServerSignPlan(string planCode)
    {
        if (serverSignInOverview == null || serverSignInOverview.plans == null)
        {
            return null;
        }

        for (int i = 0; i < serverSignInOverview.plans.Length; i++)
        {
            SignInPlanResponse plan = serverSignInOverview.plans[i];
            if (plan != null && string.Equals(plan.planCode, planCode, StringComparison.OrdinalIgnoreCase))
            {
                return plan;
            }
        }

        return null;
    }

    private static SignInDayResponse FindServerSignDay(SignInPlanResponse plan, int rewardDay)
    {
        if (plan == null || plan.days == null) return null;
        for (int index = 0; index < plan.days.Length; index++)
        {
            SignInDayResponse day = plan.days[index];
            if (day != null && day.rewardDay == rewardDay) return day;
        }
        return null;
    }

    private static bool CanMakeupServerSignDay(SignInPlanResponse plan, SignInDayResponse day)
    {
        return plan != null && day != null && !IsLimitedActivitySignPlan(plan) && plan.allowMakeup && plan.canMakeup
            && day.canMakeup && !day.claimed
            && string.Equals(day.status, "MISSED", StringComparison.OrdinalIgnoreCase)
            && day.rewardDay > 0 && day.rewardDay < plan.currentDay
            && plan.makeupCost != null && plan.makeupCost.amount > 0
            && !string.IsNullOrWhiteSpace(plan.makeupCost.itemKey);
    }

    private static string GetServerSignDayStatus(SignInPlanResponse plan, SignInDayResponse day)
    {
        if (day == null) return "暂无奖励";
        if (day.claimed || string.Equals(day.status, "CLAIMED", StringComparison.OrdinalIgnoreCase)) return "已领取";
        if (day.canClaim) return "可领取";
        if (CanMakeupServerSignDay(plan, day)) return "可补签";
        switch ((day.status ?? string.Empty).ToUpperInvariant())
        {
            case "MISSED": return "已错过";
            case "LOCKED": return "未到日期";
            case "UNAVAILABLE": return "暂无奖励";
            default: return "不可领取";
        }
    }

    private static string FormatSignInCost(SignInCostResponse cost)
    {
        if (cost == null) return string.Empty;
        string itemName = string.IsNullOrWhiteSpace(cost.itemName)
            ? GetRewardDisplayName("ITEM", cost.itemKey) : cost.itemName;
        return itemName + " x" + cost.amount;
    }

    private string GetServerSignError(SignInClaimApiResponse response, UnityWebRequest request)
    {
        if (response == null)
        {
            return "请求结果未确认，正在刷新签到状态。" + ApiRequestUtility.GetErrorMessage(request, "网络请求失败");
        }
        switch (response.code)
        {
            case 6001: return "签到计划未开放，已重新查询配置";
            case 6002: return "该日期奖励已领取，不会重复扣费或发奖";
            case 6003: return "签到活动已结束";
            case 6004: return "该日期暂无可领取奖励";
            case 6005: return "奖励配置异常，暂时无法领取，请稍后再试";
            case 6006: return "当前计划未开启补签";
            case 6007: return "该日期当前不可补签，请按最新签到状态重新选择";
            case 6008: return "补签所需资源不足，本次未扣费，请获取资源后重试";
            default: return "签到操作失败：" + GetApiError(response);
        }
    }

    private void ShowSignMakeupConfirmation(SignInPlanResponse plan, SignInDayResponse day)
    {
        ShowSignConfirmation(plan, day, true);
    }

    private void ShowSignClaimConfirmation(SignInPlanResponse plan, SignInDayResponse day)
    {
        ShowSignConfirmation(plan, day, false);
    }

    private void ShowSignConfirmation(SignInPlanResponse plan, SignInDayResponse day, bool isMakeup)
    {
        if (plan == null || day == null || signPanelObject == null || !signPanelObject.activeInHierarchy) return;
        if (isMakeup ? !CanMakeupServerSignDay(plan, day) : !day.canClaim) return;
        HideSignMakeupConfirmation();
        Transform existing = FindSceneTransform(signPanelObject.transform, "SignMakeupConfirmation");
        Button cancelButton = FindSceneComponent<Button>(existing, "SignMakeupCancelButton");
        Button confirmButton = FindSceneComponent<Button>(existing, "SignMakeupConfirmButton");
        TMPro.TMP_Text details = FindSceneComponent<TMPro.TMP_Text>(existing, "SignMakeupDetails");
        if (existing == null || cancelButton == null || confirmButton == null || details == null)
        {
            serverSignStatus = "补签确认界面未配置完整，请检查场景中的 SignMakeupConfirmation 节点";
            Debug.LogWarning(serverSignStatus, signPanelObject);
            RefreshVisibleServerSignPanel();
            return;
        }

        signMakeupConfirmObject = existing.gameObject;
        existing.SetAsLastSibling();
        signMakeupConfirmObject.SetActive(true);
        if (isMakeup)
        {
            SignInCostResponse cost = plan.makeupCost;
            string costName = cost == null || string.IsNullOrWhiteSpace(cost.itemName)
                ? GetRewardDisplayName("ITEM", cost == null ? string.Empty : cost.itemKey)
                : cost.itemName;
            details.text = "消耗：" + costName + "*" + (cost == null ? 0 : cost.amount);
            ApplySignMakeupCostIcon(existing, cost);
        }
        else
        {
            details.text = "奖励：" + FormatSignInRewardSummary(day.rewards);
            ApplySignClaimRewardIcon(existing, day.rewards);
        }
        SetSceneText(existing, "SignMakeupTitle", isMakeup ? "补签确认" : "领取奖励");
        confirmButton.interactable = !isMakeup;
        SetSceneButtonText(confirmButton, isMakeup ? "正在查询..." : "确认领取");
        ConfigureSignMakeupNavigation(cancelButton, confirmButton);
        ConfigureSignMakeupNavigation(confirmButton, cancelButton);
        BindSceneButton(cancelButton, HideSignMakeupConfirmation);
        BindSceneButton(confirmButton, () =>
        {
            if (!confirmButton.interactable) return;
            if (!serverSignMutationRunning && !serverSignOverviewRunning && HasAuthSession())
            {
                if (isMakeup)
                {
                    StartCoroutine(ConfirmServerSignMakeup(plan, day.rewardDay));
                }
                else
                {
                    HideSignMakeupConfirmation();
                    StartCoroutine(ClaimServerSignIn(plan.planCode));
                }
            }
        });
        if (cancelButton != null && UnityEngine.EventSystems.EventSystem.current != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(cancelButton.gameObject);
        }
        if (isMakeup)
        {
            StartCoroutine(RefreshSignMakeupAffordability(plan.makeupCost, existing.gameObject, confirmButton, signConfirmationVersion));
        }
    }

    private IEnumerator RefreshSignMakeupAffordability(SignInCostResponse cost, GameObject dialog, Button confirmButton, int version)
    {
        string requestToken = GameSession.Token;
        int playerId = GameSession.PlayerId;
        UnityWebRequest request = CreateAuthorizedGet($"/inventory/list/{playerId}");
        if (request == null)
        {
            SetSceneButtonText(confirmButton, "查询失败");
            serverSignStatus = "补签资源查询失败：" + requestCreationError;
            RefreshVisibleServerSignPanel();
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            // A closed/reopened dialog or changed account must not receive an old balance result.
            if (version != signConfirmationVersion || dialog == null || !dialog.activeInHierarchy || confirmButton == null
                || playerId != GameSession.PlayerId || !string.Equals(requestToken, GameSession.Token, StringComparison.Ordinal))
            {
                yield break;
            }
            if (HandleUnauthorizedResponse(request)) yield break;

            InventoryListApiResponse response = ParseJson<InventoryListApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                SetSceneButtonText(confirmButton, "查询失败");
                serverSignStatus = "补签资源查询失败，请关闭后重试：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败") : GetApiError(response));
                RefreshVisibleServerSignPanel();
                yield break;
            }

            bool enough = HasEnoughSignMakeupCost(cost, response.data);
            confirmButton.interactable = enough;
            SetSceneButtonText(confirmButton, enough ? "确认" : "资源不足");
        }
    }

    private static bool HasEnoughSignMakeupCost(SignInCostResponse cost, InventoryItemResponse[] items)
    {
        if (cost == null || cost.amount <= 0 || string.IsNullOrWhiteSpace(cost.itemKey) || items == null) return false;

        long remaining = cost.amount;
        for (int index = 0; index < items.Length; index++)
        {
            InventoryItemResponse item = items[index];
            if (item == null || item.quantity <= 0) continue;
            // Match server identifiers, not display names or local currency aliases.
            bool matches = cost.itemId > 0 && item.itemId > 0
                ? cost.itemId == item.itemId
                : string.Equals(cost.itemKey.Trim(), (item.itemKey ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
            if (!matches) continue;
            remaining -= item.quantity;
            if (remaining <= 0) return true;
        }
        return false;
    }

    private static string FormatSignInRewardSummary(SignInRewardPreview[] rewards)
    {
        if (rewards == null || rewards.Length == 0) return "暂无奖励";
        List<string> entries = new List<string>();
        for (int index = 0; index < rewards.Length; index++)
        {
            SignInRewardPreview reward = rewards[index];
            if (reward == null) continue;
            string name = string.IsNullOrWhiteSpace(reward.rewardName)
                ? GetRewardDisplayName(reward.rewardType, reward.rewardKey)
                : reward.rewardName;
            entries.Add(name + "*" + Math.Max(0L, reward.rewardAmount));
        }
        return entries.Count == 0 ? "暂无奖励" : string.Join("、", entries.ToArray());
    }

    private void ApplySignMakeupCostIcon(Transform root, SignInCostResponse cost)
    {
        Transform viewport = FindSceneTransform(root, "SignMakeupDetailsViewport");
        Transform iconFrame = viewport != null ? viewport.Find("Icon") : null;
        Image icon = iconFrame != null ? FindSceneComponent<Image>(iconFrame, "Image") : null;
        if (icon == null)
        {
            Debug.LogWarning("补签费用图标未配置，请检查 SignMakeupDetailsViewport/Icon/Image", root);
            return;
        }

        Sprite sprite = null;
        if (cost != null)
        {
            string[] candidates = { cost.icon, cost.itemKey, cost.itemName };
            for (int index = 0; index < candidates.Length && sprite == null; index++)
            {
                sprite = LoadMailAttachmentResourceSprite(candidates[index]);
                if (sprite != null) continue;
                string assetPath = GetMailAttachmentAssetPath(candidates[index], "ITEM", cost.itemCategory);
                sprite = LoadNewbieSignResourceSprite(assetPath);
                if (sprite == null && !string.IsNullOrEmpty(assetPath)) sprite = LoadSpriteFromAsset(assetPath);
            }
        }

        icon.sprite = sprite;
        icon.enabled = sprite != null;
        if (sprite == null && cost != null) Debug.LogWarning("未找到补签费用物品图标：" + cost.itemKey, icon);
    }

    private void ApplySignClaimRewardIcon(Transform root, SignInRewardPreview[] rewards)
    {
        Transform viewport = FindSceneTransform(root, "SignMakeupDetailsViewport");
        Transform iconFrame = viewport != null ? viewport.Find("Icon") : null;
        Image icon = iconFrame != null ? FindSceneComponent<Image>(iconFrame, "Image") : null;
        if (icon == null)
        {
            Debug.LogWarning("签到奖励图标未配置，请检查 SignMakeupDetailsViewport/Icon/Image", root);
            return;
        }

        SignInRewardPreview reward = null;
        if (rewards != null)
        {
            for (int index = 0; index < rewards.Length; index++)
            {
                if (rewards[index] != null)
                {
                    reward = rewards[index];
                    break;
                }
            }
        }

        Sprite sprite = null;
        if (reward != null)
        {
            string[] candidates = { reward.rewardIcon, reward.rewardKey, reward.rewardName };
            for (int index = 0; index < candidates.Length && sprite == null; index++)
            {
                sprite = LoadMailAttachmentResourceSprite(candidates[index]);
                if (sprite != null) continue;
                string assetPath = GetMailAttachmentAssetPath(candidates[index], reward.rewardType, reward.rewardCategory);
                sprite = LoadNewbieSignResourceSprite(assetPath);
                if (sprite == null && !string.IsNullOrEmpty(assetPath)) sprite = LoadSpriteFromAsset(assetPath);
            }
        }

        icon.sprite = sprite;
        icon.enabled = sprite != null;
        if (sprite == null && reward != null) Debug.LogWarning("未找到签到奖励图标：" + reward.rewardKey, icon);
    }

    private static void ConfigureSignMakeupNavigation(Button button, Button other)
    {
        if (button == null) return;
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.Explicit;
        navigation.selectOnLeft = other;
        navigation.selectOnRight = other;
        navigation.selectOnUp = other;
        navigation.selectOnDown = other;
        button.navigation = navigation;
    }

    private void HideSignMakeupConfirmation()
    {
        signConfirmationVersion++;
        if (signMakeupConfirmObject != null && UnityEngine.EventSystems.EventSystem.current != null)
        {
            GameObject selected = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(signMakeupConfirmObject.transform))
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            }
        }
        if (signMakeupConfirmObject != null) signMakeupConfirmObject.SetActive(false);
        HideSceneSignMakeupConfirmation(HomePageScenePanelType.DailySign);
        HideSceneSignMakeupConfirmation(HomePageScenePanelType.NewbieSign);
    }

    private void HideSceneSignMakeupConfirmation(HomePageScenePanelType panelType)
    {
        HomePageScenePanel panel = GetRegisteredScenePanel(panelType);
        Transform confirmation = panel != null ? FindSceneTransform(panel.transform, "SignMakeupConfirmation") : null;
        if (confirmation != null) confirmation.gameObject.SetActive(false);
    }

    private IEnumerator ConfirmServerSignMakeup(SignInPlanResponse preview, int rewardDay)
    {
        serverSignMutationRunning = true;
        HideSignMakeupConfirmation();
        string requestToken = GameSession.Token;
        try
        {
            serverSignStatus = "正在核对补签日期和费用...";
            yield return GetServerSignInOverview(serverSignStatus);
            if (!serverSignOverviewReady || !string.Equals(requestToken, GameSession.Token, StringComparison.Ordinal)) yield break;
            SignInPlanResponse latest = FindServerSignPlan(preview.planCode);
            SignInDayResponse day = FindServerSignDay(latest, rewardDay);
            if (latest == null || !string.Equals(latest.periodKey, preview.periodKey, StringComparison.Ordinal)
                || !CanMakeupServerSignDay(latest, day))
            {
                serverSignStatus = "签到周期或日期状态已变化，请重新选择可补签日期";
                yield break;
            }
            if (latest.makeupCost.itemKey != preview.makeupCost.itemKey || latest.makeupCost.amount != preview.makeupCost.amount)
            {
                serverSignStatus = "补签费用已更新，请重新确认";
                ShowSignMakeupConfirmation(latest, day);
                yield break;
            }
            yield return MakeupServerSignIn(latest.planCode, rewardDay);
        }
        finally
        {
            serverSignMutationRunning = false;
            RefreshVisibleServerSignPanel();
        }
    }

    private void RefreshSignInEntryBadges()
    {
        if (boundBookView == null) return;
        SetSignInEntryBadge(boundBookView.DailySignButton, FindServerSignPlan("MONTHLY"));
        SetSignInEntryBadge(boundBookView.ActivityButton, FindServerSignPlan("NEWBIE"));
    }

    private void SetSignInEntryBadge(Button button, SignInPlanResponse plan)
    {
        if (button == null) return;
        bool available = serverSignOverviewReady && plan != null && (plan.canClaim || plan.canMakeup);
        Transform badge = button.transform.Find("SignAvailableBadge");
        if (badge == null && available)
        {
            Image image = CreateImage("SignAvailableBadge", button.transform, new Color(0.85f, 0.12f, 0.12f, 1f));
            image.raycastTarget = false;
            SetRect(image.rectTransform, Vector2.one, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(-8f, -8f), new Vector2(14f, 14f));
            badge = image.transform;
        }
        if (badge != null) badge.gameObject.SetActive(available);
    }

    private void BeginServerSignClaim(string planCode, SignInDayResponse day)
    {
        if (day == null || serverSignMutationRunning || serverSignOverviewRunning || !serverSignOverviewReady || !HasAuthSession())
        {
            return;
        }

        SignInPlanResponse plan = FindServerSignPlan(planCode);
        day = FindServerSignDay(plan, day.rewardDay);
        if (day == null || day.claimed || string.Equals(day.status, "CLAIMED", StringComparison.OrdinalIgnoreCase)) return;

        if (day.canClaim)
        {
            ShowSignClaimConfirmation(plan, day);
        }
        else if (CanMakeupServerSignDay(plan, day))
        {
            ShowSignMakeupConfirmation(plan, day);
        }
    }

    private IEnumerator ClaimServerSignIn(string planCode)
    {
        serverSignMutationRunning = true;
        serverSignStatus = "正在领取签到奖励...";
        RefreshVisibleServerSignPanel();
        SignInClaimRequest payload = new SignInClaimRequest { planCode = planCode };
        yield return SendServerSignMutation("/sign-in/claim", JsonUtility.ToJson(payload));
    }

    private IEnumerator MakeupServerSignIn(string planCode, int rewardDay)
    {
        serverSignMutationRunning = true;
        serverSignStatus = $"正在补签第 {rewardDay} 天...";
        RefreshVisibleServerSignPanel();
        SignInMakeupRequest payload = new SignInMakeupRequest { planCode = planCode, rewardDay = rewardDay };
        yield return SendServerSignMutation("/sign-in/makeup", JsonUtility.ToJson(payload));
    }

    private IEnumerator SendServerSignMutation(string path, string json)
    {
        string requestToken = GameSession.Token;
        try
        {
            if (!TryCreateAuthorizedPost(path, json, out UnityWebRequest request, out string error))
            {
                serverSignStatus = error;
                yield break;
            }

            using (request)
            {
                yield return request.SendWebRequest();
                if (!string.Equals(requestToken, GameSession.Token, StringComparison.Ordinal))
                {
                    yield break;
                }
                if (HandleUnauthorizedResponse(request))
                {
                    yield break;
                }

                SignInClaimApiResponse response = ParseJson<SignInClaimApiResponse>(request.downloadHandler.text);
                if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
                {
                    serverSignStatus = GetServerSignError(response, request);
                    if (response != null && response.code == 6005)
                    {
                        Debug.LogWarning("Sign-in reward configuration error (6005): " + response.message, this);
                    }
                    if (response == null || request.responseCode >= 500 || response.code == 6002 || response.code == 200)
                    {
                        yield return RefreshSignInRewardState(null);
                        if (!string.Equals(requestToken, GameSession.Token, StringComparison.Ordinal)) yield break;
                        yield return RefreshSignInAccountSnapshot();
                        if (!string.Equals(requestToken, GameSession.Token, StringComparison.Ordinal)) yield break;
                        RefreshHomeResourceMetrics();
                    }
                    yield return GetServerSignInOverview(serverSignStatus);
                    yield break;
                }

                if (response.data.items != null)
                {
                    currentInventoryItems = response.data.items;
                    SyncInventoryResourceValues(response.data.items);
                }

                serverSignStatus = BuildSignClaimResult(response.data);
                yield return RefreshSignInRewardState(response.data);
                if (!string.Equals(requestToken, GameSession.Token, StringComparison.Ordinal)) yield break;
                yield return RefreshSignInAccountSnapshot();
                if (!string.Equals(requestToken, GameSession.Token, StringComparison.Ordinal)) yield break;
                RefreshHomeResourceMetrics();
                yield return GetServerSignInOverview(serverSignStatus);
            }
        }
        finally
        {
            serverSignMutationRunning = false;
            if (string.Equals(requestToken, GameSession.Token, StringComparison.Ordinal))
            {
                RefreshVisibleServerSignPanel();
                SetStatus(serverSignStatus);
            }
        }
    }

    private IEnumerator RefreshSignInRewardState(SignInClaimData data)
    {
        string requestToken = GameSession.Token;
        bool needsInventoryRefresh = data == null || data.items != null || data.cost != null;
        bool needsEquipmentRefresh = data == null;
        bool needsNpcRefresh = data == null;
        if (data != null && data.rewards != null)
        {
            for (int i = 0; i < data.rewards.Length; i++)
            {
                SignInGrantedReward reward = data.rewards[i];
                if (reward == null)
                {
                    continue;
                }

                switch ((reward.grantedRewardType ?? string.Empty).Trim().ToUpperInvariant())
                {
                    case "ITEM":
                        needsInventoryRefresh = true;
                        break;
                    case "EQUIPMENT":
                        needsEquipmentRefresh = true;
                        break;
                    case "NPC":
                        needsNpcRefresh = true;
                        break;
                }
            }
        }

        List<string> syncedCategories = new List<string>();
        List<string> failedCategories = new List<string>();
        if (needsInventoryRefresh)
        {
            if (data != null && data.items != null)
            {
                syncedCategories.Add("背包");
            }
            else
            {
                yield return RefreshCardDrawInventoryState(syncedCategories, failedCategories);
            }
        }
        if (needsEquipmentRefresh)
        {
            if (!string.Equals(requestToken, GameSession.Token, StringComparison.Ordinal)) yield break;
            yield return RefreshCardDrawEquipmentState(syncedCategories, failedCategories);
        }
        if (needsNpcRefresh)
        {
            if (!string.Equals(requestToken, GameSession.Token, StringComparison.Ordinal)) yield break;
            yield return RefreshCardDrawNpcState(syncedCategories, failedCategories);
        }

        if (!string.Equals(requestToken, GameSession.Token, StringComparison.Ordinal)) yield break;
        if (needsNpcRefresh || needsEquipmentRefresh)
        {
            yield return RefreshVisibleRolePanelAfterSignIn(requestToken, failedCategories);
        }
        if (!string.Equals(requestToken, GameSession.Token, StringComparison.Ordinal)) yield break;

        if (inventoryPanelObject != null && inventoryPanelObject.activeInHierarchy && needsInventoryRefresh)
        {
            inventoryStatusMessage = "签到奖励已同步";
            RenderInventoryPanel();
        }
        if (equipmentPanelObject != null && equipmentPanelObject.activeInHierarchy && (needsEquipmentRefresh || needsNpcRefresh))
        {
            RebuildServerEquipmentItems();
            equipmentStatusMessage = "签到奖励已同步";
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
        }

        if (failedCategories.Count > 0)
        {
            serverSignStatus += "；同步失败：" + string.Join("；", failedCategories.ToArray());
        }
    }

    private IEnumerator RefreshSignInAccountSnapshot()
    {
        yield return RefreshAccountSnapshot(string.Empty, false,
            error => serverSignStatus += "；签到后资料同步失败：" + error);
    }

    private IEnumerator RefreshVisibleRolePanelAfterSignIn(string requestToken, List<string> failedCategories)
    {
        while (rolePanelObject != null && rolePanelObject.activeInHierarchy && (rolePanelLoading || roleUpgradeRunning))
        {
            if (!HasAuthSession() || !string.Equals(requestToken, GameSession.Token, StringComparison.Ordinal)) yield break;
            yield return null;
        }
        if (rolePanelObject == null || !rolePanelObject.activeInHierarchy || !HasAuthSession()
            || !string.Equals(requestToken, GameSession.Token, StringComparison.Ordinal)) yield break;

        int requestVersion = ++roleRequestVersion;
        int playerId = GameSession.PlayerId;
        rolePanelLoading = true;
        roleStatusMessage = "正在同步签到后的角色数据...";
        RenderRolePanel();
        bool loaded = false;
        yield return LoadRolePanelData(requestVersion, playerId, () => loaded = true);
        if (IsRoleRequestCurrent(requestVersion, playerId) && !loaded)
        {
            failedCategories.Add("角色中心（" + roleStatusMessage + "）");
        }
    }

    private static string BuildSignClaimResult(SignInClaimData data)
    {
        if (data == null)
        {
            return "签到成功";
        }

        List<string> rewards = new List<string>();
        if (data.rewards != null)
        {
            for (int i = 0; i < data.rewards.Length; i++)
            {
                SignInGrantedReward reward = data.rewards[i];
                if (reward != null)
                {
                    string rewardName = string.IsNullOrWhiteSpace(reward.grantedRewardName)
                        ? GetRewardDisplayName(reward.grantedRewardType, reward.grantedRewardKey)
                        : reward.grantedRewardName;
                    rewards.Add($"{rewardName}x{reward.grantedRewardAmount}");
                }
            }
        }

        string prefix = data.claimType == "MAKEUP" ? $"第{data.rewardDay}天补签成功" : $"第{data.rewardDay}天签到成功";
        string result = rewards.Count == 0 ? prefix : prefix + "：" + string.Join("、", rewards.ToArray());
        if (data.cost != null)
        {
            result += "；实际消耗 " + FormatSignInCost(data.cost);
        }
        return result;
    }

    private void BuildSignRewardDisplay(SignInRewardPreview[] rewards, out string rewardName, out string rewardAmount)
    {
        if (rewards == null || rewards.Length == 0)
        {
            rewardName = "暂无奖励";
            rewardAmount = string.Empty;
            return;
        }

        List<string> entries = new List<string>();
        for (int i = 0; i < rewards.Length; i++)
        {
            SignInRewardPreview reward = rewards[i];
            if (reward == null)
            {
                continue;
            }

            string name = string.IsNullOrWhiteSpace(reward.rewardName)
                ? GetRewardDisplayName(reward.rewardType, reward.rewardKey)
                : reward.rewardName;
            entries.Add(name + " x" + Math.Max(0L, reward.rewardAmount));
        }

        rewardName = entries.Count == 0 ? "暂无奖励" : string.Join("\n", entries.ToArray());
        rewardAmount = string.Empty;
    }

    private void ApplySignRewardIcon(Transform card, SignInRewardPreview[] rewards)
    {
        if (card != null && card.name.StartsWith("NewbieSignCard", StringComparison.Ordinal))
        {
            ApplyNewbieSignRewardIcon(card, rewards);
            return;
        }

        if (card == null)
        {
            return;
        }

        Transform iconFrame = FindSceneTransform(card, "RewardIcon");
        Image rewardIcon = iconFrame != null ? FindSceneComponent<Image>(iconFrame, "Icon") : null;
        if (rewardIcon == null)
        {
            rewardIcon = FindSceneComponent<Image>(card, "RewardIcon");
        }
        if (rewardIcon == null)
        {
            return;
        }

        SignInRewardPreview reward = null;
        if (rewards != null)
        {
            for (int index = 0; index < rewards.Length; index++)
            {
                if (rewards[index] != null)
                {
                    reward = rewards[index];
                    break;
                }
            }
        }

        rewardIcon.sprite = null;
        rewardIcon.enabled = false;
        if (reward == null)
        {
            SetSceneObjectActive(card, "RewardIconText", false);
            return;
        }

        ApplyRewardQualityBackground(iconFrame, reward.rewardQuality);

        string[] candidates = { reward.rewardIcon, reward.rewardKey, reward.rewardName };
        for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
        {
            Sprite sprite = LoadMailAttachmentResourceSprite(candidates[candidateIndex]);
            if (sprite == null)
            {
                string assetPath = GetMailAttachmentAssetPath(
                    candidates[candidateIndex],
                    reward.rewardType,
                    reward.rewardCategory);
                sprite = LoadNewbieSignResourceSprite(assetPath);
                if (sprite == null && !string.IsNullOrEmpty(assetPath))
                {
                    sprite = LoadSpriteFromAsset(assetPath);
                }
            }

            if (sprite == null)
            {
                continue;
            }

            rewardIcon.sprite = sprite;
            rewardIcon.enabled = true;
            SetSceneObjectActive(card, "RewardIconText", false);
            return;
        }

        SetSceneObjectActive(card, "RewardIconText", true);
    }

    private static void ApplyRewardQualityBackground(Transform iconFrame, string quality)
    {
        if (iconFrame == null || string.IsNullOrWhiteSpace(quality))
        {
            return;
        }

        Image qualityBackground = iconFrame.GetComponent<Image>();
        if (qualityBackground == null)
        {
            return;
        }

        InventoryPanelView inventoryView = Resources.Load<InventoryPanelView>("UI/InventoryPanel");
        Sprite sprite = inventoryView == null ? null : inventoryView.ResolveQualityBackground(quality);
        if (sprite == null)
        {
            return;
        }

        qualityBackground.sprite = sprite;
        qualityBackground.color = Color.white;
    }

    private void ApplyNewbieSignRewardIcon(Transform card, SignInRewardPreview[] rewards)
    {
        Image image = FindSceneComponent<Image>(card, "RewardResourceImage");
        if (image == null)
        {
            return;
        }

        Sprite sprite = null;
        SignInRewardPreview reward = null;
        if (rewards != null)
        {
            for (int i = 0; i < rewards.Length; i++)
            {
                if (rewards[i] != null)
                {
                    reward = rewards[i];
                    break;
                }
            }
        }

        if (reward != null)
        {
            string[] candidates = { reward.rewardIcon, reward.rewardKey, reward.rewardName };
            for (int i = 0; i < candidates.Length && sprite == null; i++)
            {
                sprite = LoadMailAttachmentResourceSprite(candidates[i]);
                if (sprite == null)
                {
                    string path = GetMailAttachmentAssetPath(candidates[i], reward.rewardType, reward.rewardCategory);
                    sprite = LoadNewbieSignResourceSprite(path);
                    if (sprite == null && !string.IsNullOrEmpty(path))
                    {
                        sprite = LoadSpriteFromAsset(path);
                    }
                }
            }
        }

        // Only reward data changes. The slot background and all authored layout/style stay untouched.
        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    private Sprite LoadNewbieSignResourceSprite(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)
            || assetPath.IndexOf("UI/资源图标/", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return null;
        }

        // Reuse serialized resource references so icons also work without loose Assets files in builds.
        string iconName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        HomePageScenePanel inventoryPanel = GetRegisteredScenePanel(HomePageScenePanelType.Inventory);
        InventoryPanelView icons = inventoryPanel != null
            ? inventoryPanel.GetComponentInChildren<InventoryPanelView>(true)
            : null;
        Sprite sprite = icons != null ? icons.ResolveIcon(iconName, null, null) : null;
        if (sprite != null)
        {
            return sprite;
        }

        InventoryPanelView prefab = Resources.Load<InventoryPanelView>("UI/InventoryPanel");
        return prefab != null ? prefab.ResolveIcon(iconName, null, null) : null;
    }

    private static string GetRewardDisplayName(string rewardType, string rewardKey)
    {
        string key = (rewardKey ?? string.Empty).ToLowerInvariant();
        if (key == "gold") return "金币";
        if (key == "diamond" || key == "crystal") return "钻石";
        if (key == "wood") return "木材";
        if (key == "stone") return "石材";
        if (key == "iron") return "铁矿";
        if (string.Equals(rewardType, "EQUIPMENT", StringComparison.OrdinalIgnoreCase)) return "装备";
        if (string.Equals(rewardType, "NPC", StringComparison.OrdinalIgnoreCase)) return "NPC";
        return string.IsNullOrEmpty(rewardKey) ? "奖励" : rewardKey;
    }

    private void OpenServerLottery()
    {
        OpenServerLottery(string.Empty, 0);
    }

    private void OpenServerLottery(string poolCode, int highlightNpcId)
    {
        pendingCardDrawPoolCode = (poolCode ?? string.Empty).Trim();
        pendingCardDrawHighlightNpcId = Math.Max(0, highlightNpcId);
        cardDrawRoutePoolMismatch = false;
        serverLotteryStatus = "正在读取服务器奖池...";
        serverLotteryRewardName = string.Empty;
        serverLotteryRewardAmount = 0;
        serverLotteryHasResult = false;
        serverLotteryDisplayPage = 0;
        ResetLotteryOverlayPanels();
        ShowLotteryPanel(serverLotteryStatus, string.Empty, 0, false);
        if (!serverCardDrawPoolRunning && !serverCardDrawRunning)
        {
            StartCoroutine(GetServerCardDrawPool());
        }
    }

    private IEnumerator GetServerCardDrawPool()
    {
        serverCardDrawPoolRunning = true;
        UnityWebRequest request = CreateAuthorizedGet("/card-draw/current");
        if (request == null)
        {
            serverCardDrawPoolRunning = false;
            serverLotteryStatus = requestCreationError;
            ShowServerLotteryState();
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                serverCardDrawPoolRunning = false;
                yield break;
            }

            CardDrawPoolApiResponse response = ParseJson<CardDrawPoolApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                serverCardDrawPoolRunning = false;
                serverLotteryStatus = "奖池同步失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "返回数据格式错误")
                    : GetApiError(response));
                ShowServerLotteryState();
                yield break;
            }

            serverCardDrawPool = response.data;
            serverCardDrawPoolRunning = false;
            serverLotteryDisplayPage = 0;
            cardDrawRoutePoolMismatch = !string.IsNullOrEmpty(pendingCardDrawPoolCode)
                && !string.Equals(pendingCardDrawPoolCode, response.data.poolCode, StringComparison.OrdinalIgnoreCase);
            serverLotteryStatus = cardDrawRoutePoolMismatch
                ? $"指引奖池 {pendingCardDrawPoolCode} 已失效，当前奖池为 {response.data.poolCode}"
                : $"{response.data.name} · 奖池版本 {response.data.version}";
            ShowServerLotteryState();
        }
    }

    private void BeginServerCardDraw(string optionCode)
    {
        if (serverCardDrawRunning || serverCardDrawPoolRunning)
        {
            return;
        }

        if (cardDrawRoutePoolMismatch)
        {
            serverLotteryStatus = "当前奖池已变化，请重新打开获取指引";
            ShowServerLotteryState();
            return;
        }

        CardDrawOptionResponse option = FindCardDrawOption(optionCode);
        if (option == null)
        {
            serverLotteryStatus = "当前奖池没有可用的" + (optionCode == "TEN" ? "十连抽" : optionCode == "FIVE" ? "五连抽" : "单抽") + "档位";
            ShowServerLotteryState();
            return;
        }

        if (string.IsNullOrEmpty(pendingCardDrawKey) || pendingCardDrawOption != optionCode)
        {
            pendingCardDrawKey = Guid.NewGuid().ToString();
            pendingCardDrawOption = optionCode;
        }

        StartCoroutine(DrawServerCards(optionCode, pendingCardDrawKey));
    }

    private IEnumerator DrawServerCards(string optionCode, string idempotencyKey)
    {
        serverCardDrawRunning = true;
        serverLotteryStatus = optionCode == "TEN"
            ? "正在执行十连抽..."
            : optionCode == "FIVE" ? "正在执行五连抽..." : "正在执行单抽...";
        ShowServerLotteryState(true);

        CardDrawRequest payload = new CardDrawRequest { optionCode = optionCode };
        if (!TryCreateAuthorizedPost("/card-draw/draw", JsonUtility.ToJson(payload), out UnityWebRequest request, out string error))
        {
            serverCardDrawRunning = false;
            serverLotteryStatus = error;
            ShowServerLotteryState();
            yield break;
        }

        request.SetRequestHeader("Idempotency-Key", idempotencyKey);
        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                serverCardDrawRunning = false;
                yield break;
            }

            CardDrawApiResponse response = ParseJson<CardDrawApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                bool serverResponded = request.responseCode > 0 && request.result != UnityWebRequest.Result.ConnectionError;
                if (serverResponded)
                {
                    pendingCardDrawKey = string.Empty;
                    pendingCardDrawOption = string.Empty;
                }

                serverCardDrawRunning = false;
                string failure = response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "抽奖请求失败")
                    : GetApiError(response);
                serverLotteryStatus = serverResponded
                    ? "抽奖失败：" + failure
                    : "网络状态未知，再次点击会使用同一请求号重试：" + failure;
                ShowServerLotteryState();
                yield break;
            }

            pendingCardDrawKey = string.Empty;
            pendingCardDrawOption = string.Empty;
            serverCardDrawRunning = false;
            if (response.data.cards != null && response.data.cards.Length > 0)
            {
                if (serverCardDrawPool == null)
                {
                    serverCardDrawPool = new CardDrawPoolData();
                }
                serverCardDrawPool.cards = response.data.cards;
                serverCardDrawPool.cardCount = response.data.cardCount;
            }

            PlayerPrefs.SetInt("Crystal", Mathf.Max(0, response.data.remainingDiamond));
            PlayerPrefs.Save();
            RefreshHomeResourceMetrics();

            serverLotteryStatus = BuildCardDrawResultText(response.data);
            serverLotteryHasResult = response.data.results != null && response.data.results.Length > 0;
            if (serverLotteryHasResult)
            {
                CardDrawResultResponse first = response.data.results[0];
                serverLotteryRewardName = GetCardDrawResultName(first);
                serverLotteryRewardAmount = first.grantedRewardAmount;
                serverLotteryDisplayPage = Mathf.Max(0, (first.winningSlot - 1) / 3);
            }

            ShowServerLotteryState();
            ShowLotteryRewardConfirm(response.data);
            StartCoroutine(RefreshCardDrawRewardState(response.data));
        }
    }

    private IEnumerator RefreshCardDrawRewardState(CardDrawData drawData)
    {
        bool needsInventoryRefresh = false;
        bool needsEquipmentRefresh = false;
        bool needsNpcRefresh = false;
        if (drawData != null && drawData.results != null)
        {
            for (int i = 0; i < drawData.results.Length; i++)
            {
                CardDrawResultResponse result = drawData.results[i];
                if (result == null)
                {
                    continue;
                }

                switch ((result.grantedRewardType ?? string.Empty).Trim().ToUpperInvariant())
                {
                    case "ITEM":
                        needsInventoryRefresh = true;
                        break;
                    case "EQUIPMENT":
                        needsEquipmentRefresh = true;
                        break;
                    case "NPC":
                        needsNpcRefresh = true;
                        break;
                }
            }
        }

        List<string> syncedCategories = new List<string>();
        List<string> failedCategories = new List<string>();
        if (needsInventoryRefresh)
        {
            yield return RefreshCardDrawInventoryState(syncedCategories, failedCategories);
        }

        if (needsNpcRefresh)
        {
            yield return RefreshCardDrawNpcState(syncedCategories, failedCategories);
        }

        if (needsEquipmentRefresh)
        {
            yield return RefreshCardDrawEquipmentState(syncedCategories, failedCategories);
        }

        if (inventoryPanelObject != null && inventoryPanelObject.activeInHierarchy && needsInventoryRefresh)
        {
            inventoryStatusMessage = "抽奖奖励已同步";
            RenderInventoryPanel();
        }

        if (equipmentPanelObject != null && equipmentPanelObject.activeInHierarchy && (needsNpcRefresh || needsEquipmentRefresh))
        {
            RebuildServerEquipmentItems();
            equipmentStatusMessage = "抽奖奖励已同步";
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
        }

        if (syncedCategories.Count == 0 && failedCategories.Count == 0)
        {
            yield break;
        }

        string syncMessage = syncedCategories.Count > 0
            ? "已同步：" + string.Join("、", syncedCategories.ToArray())
            : string.Empty;
        if (failedCategories.Count > 0)
        {
            string failureMessage = "同步失败：" + string.Join("；", failedCategories.ToArray());
            syncMessage = string.IsNullOrEmpty(syncMessage) ? failureMessage : syncMessage + "。" + failureMessage;
        }

        serverLotteryStatus = BuildCardDrawResultText(drawData) + "\n" + syncMessage;
        if (lotteryPanelObject != null && lotteryPanelObject.activeInHierarchy)
        {
            ShowServerLotteryState();
        }
        else
        {
            SetStatus(syncMessage);
        }
    }

    private IEnumerator RefreshCardDrawInventoryState(List<string> syncedCategories, List<string> failedCategories)
    {
        int playerId = GameSession.PlayerId;
        if (playerId <= 0)
        {
            failedCategories.Add("背包（玩家信息无效）");
            yield break;
        }

        UnityWebRequest request = CreateAuthorizedGet($"/inventory/list/{playerId}");
        if (request == null)
        {
            failedCategories.Add("背包（" + requestCreationError + "）");
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                failedCategories.Add("背包（登录已失效）");
                yield break;
            }

            InventoryListApiResponse response = ParseJson<InventoryListApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200)
            {
                string error = response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response);
                failedCategories.Add("背包（" + error + "）");
                yield break;
            }

            currentInventoryItems = response.data ?? new InventoryItemResponse[0];
            inventoryServerBacked = true;
            SyncInventoryResourceValues(currentInventoryItems);
            syncedCategories.Add("背包");
        }
    }

    private IEnumerator RefreshCardDrawNpcState(List<string> syncedCategories, List<string> failedCategories)
    {
        int playerId = GameSession.PlayerId;
        if (playerId <= 0)
        {
            failedCategories.Add("灵宠列表（玩家信息无效）");
            yield break;
        }

        long selectedNpcId = GetCurrentEquipmentNpc() != null ? GetCurrentEquipmentNpc().playerNpcId : 0;
        UnityWebRequest request = CreateAuthorizedGet($"/npc/list/{playerId}");
        if (request == null)
        {
            failedCategories.Add("灵宠列表（" + requestCreationError + "）");
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                failedCategories.Add("灵宠列表（登录已失效）");
                yield break;
            }

            PlayerNpcListApiResponse response = ParseJson<PlayerNpcListApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200)
            {
                string error = response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response);
                failedCategories.Add("灵宠列表（" + error + "）");
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

            syncedCategories.Add("灵宠列表");
        }
    }

    private IEnumerator RefreshCardDrawEquipmentState(List<string> syncedCategories, List<string> failedCategories)
    {
        UnityWebRequest request = CreateAuthorizedGet("/equipment/warehouse");
        if (request == null)
        {
            failedCategories.Add("装备仓库（" + requestCreationError + "）");
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                failedCategories.Add("装备仓库（登录已失效）");
                yield break;
            }

            EquipmentWarehouseApiResponse response = ParseJson<EquipmentWarehouseApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200)
            {
                string error = response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response);
                failedCategories.Add("装备仓库（" + error + "）");
                yield break;
            }

            serverEquipmentWarehouse = response.data ?? new InventoryItemResponse[0];
            for (int i = 0; i < serverEquipmentWarehouse.Length; i++)
            {
                NormalizeServerEquipmentItem(serverEquipmentWarehouse[i], "WAREHOUSE");
            }

            RebuildServerEquipmentItems();
            syncedCategories.Add("装备仓库");
        }
    }

    private string BuildCardDrawResultText(CardDrawData data)
    {
        if (data == null || data.results == null || data.results.Length == 0)
        {
            return "抽奖完成";
        }

        List<string> rewards = new List<string>();
        int shown = Mathf.Min(data.results.Length, 5);
        for (int i = 0; i < shown; i++)
        {
            CardDrawResultResponse result = data.results[i];
            string suffix = result.duplicateNpcConverted ? "(重复NPC转碎片)" : string.Empty;
            rewards.Add($"{GetCardDrawResultName(result)}x{result.grantedRewardAmount}{suffix}");
        }

        return "获得：" + string.Join("、", rewards.ToArray()) + $" · 剩余钻石 {data.remainingDiamond}";
    }

    private string GetCardDrawResultName(CardDrawResultResponse result)
    {
        if (result == null)
        {
            return "奖励";
        }

        if (!string.IsNullOrWhiteSpace(result.grantedRewardName))
        {
            return result.grantedRewardName;
        }

        CardDrawCardResponse card = FindCardDrawCard(result.winningSlot);
        if (card != null && !string.IsNullOrEmpty(card.rewardName)
            && string.Equals(card.rewardKey, result.grantedRewardKey, StringComparison.OrdinalIgnoreCase))
        {
            return card.rewardName;
        }

        return GetRewardDisplayName(result.grantedRewardType, result.grantedRewardKey);
    }

    private CardDrawCardResponse FindCardDrawCard(int slotNo)
    {
        if (serverCardDrawPool == null || serverCardDrawPool.cards == null)
        {
            return null;
        }

        for (int i = 0; i < serverCardDrawPool.cards.Length; i++)
        {
            if (serverCardDrawPool.cards[i] != null && serverCardDrawPool.cards[i].slotNo == slotNo)
            {
                return serverCardDrawPool.cards[i];
            }
        }

        return null;
    }

    private CardDrawOptionResponse FindCardDrawOption(string optionCode)
    {
        if (serverCardDrawPool == null || serverCardDrawPool.options == null)
        {
            return null;
        }

        for (int i = 0; i < serverCardDrawPool.options.Length; i++)
        {
            CardDrawOptionResponse option = serverCardDrawPool.options[i];
            if (option != null && string.Equals(option.optionCode, optionCode, StringComparison.OrdinalIgnoreCase))
            {
                return option;
            }
        }

        return null;
    }

    private void ConfigureServerLotteryScenePanel(
        Transform root,
        string status,
        string rewardName,
        int rewardValue,
        bool hasResult,
        bool isRolling)
    {
        CardDrawCardResponse[] cards = serverCardDrawPool != null && serverCardDrawPool.cards != null
            ? serverCardDrawPool.cards
            : new CardDrawCardResponse[0];
        EnsureRareCardDrawBroadcastView(root);
        StartRareCardDrawBroadcast();
        ConfigureLotteryPrizeRail(root, cards);

        CardDrawOptionResponse single = FindCardDrawOption("SINGLE");
        CardDrawOptionResponse multi = FindCardDrawOption("TEN");
        if (multi == null)
        {
            multi = FindCardDrawOption("FIVE");
        }
        string multiLabel = multi != null && string.Equals(multi.optionCode, "TEN", StringComparison.OrdinalIgnoreCase)
            ? "十连抽"
            : "五连抽";
        int diamond = PlayerPrefs.GetInt("Crystal", 0);
        SetSceneText(root, "LotteryDiamondText", $"钻石 {diamond}");
        SetSceneText(root, "LotteryPrizeHint", serverCardDrawPool == null
            ? "正在同步奖池"
            : $"奖项 {cards.Length} 个 · 服务器配置");
        SetSceneText(root, "LotteryStatus", string.IsNullOrEmpty(status) ? serverLotteryStatus : status);

        Transform poolPanel = FindSceneTransform(root, "LotteryPoolPanel");
        Transform rewardPanel = FindSceneTransform(root, "LotteryRewardConfirmPanel");
        bool poolVisible = poolPanel != null && poolPanel.gameObject.activeSelf;
        bool rewardVisible = rewardPanel != null && rewardPanel.gameObject.activeSelf;

        Button closeButton = FindSceneComponent<Button>(root, "LotteryCloseButton");
        if (closeButton != null)
        {
            closeButton.interactable = !serverCardDrawRunning && !poolVisible && !rewardVisible;
        }
        BindSceneButton(closeButton, closeButton != null && closeButton.interactable ? (Action)HideLotteryPanel : null);

        Button singleButton = FindSceneComponent<Button>(root, "LotterySingleButton");
        if (singleButton != null)
        {
            singleButton.interactable = single != null && !serverCardDrawRunning && !serverCardDrawPoolRunning && !poolVisible && !rewardVisible;
        }
        SetSceneButtonText(singleButton, serverCardDrawRunning ? "抽取中..." : FormatDrawOption(single, "单抽"));
        BindSceneButton(singleButton, singleButton != null && singleButton.interactable ? (Action)(() => BeginServerCardDraw("SINGLE")) : null);

        Button multiButton = FindSceneComponent<Button>(root, "LotteryTenButton");
        if (multiButton != null)
        {
            multiButton.interactable = multi != null && !serverCardDrawRunning && !serverCardDrawPoolRunning && !poolVisible && !rewardVisible;
        }
        SetSceneButtonText(multiButton, FormatDrawOption(multi, multiLabel));
        BindSceneButton(
            multiButton,
            multiButton != null && multiButton.interactable && multi != null
                ? (Action)(() => BeginServerCardDraw(multi.optionCode))
                : null);

        Button poolButton = FindSceneComponent<Button>(root, "LotteryPoolButton");
        if (poolButton != null)
        {
            poolButton.interactable = cards.Length > 0 && !serverCardDrawRunning && !rewardVisible;
        }
        SetSceneButtonText(poolButton, "奖池");
        BindSceneButton(poolButton, poolButton != null && poolButton.interactable ? (Action)ShowLotteryPool : null);
    }

    private static string FormatDrawOption(CardDrawOptionResponse option, string fallback)
    {
        if (option == null)
        {
            return fallback + "未开放";
        }

        return $"{fallback}\n{option.costAmount}{option.costItemName}";
    }

    private void ConfigureLotteryPrizeRail(Transform root, CardDrawCardResponse[] cards)
    {
        Transform content = FindSceneTransform(root, "LotteryPrizeContent");
        Transform template = FindSceneTransform(root, "LotteryPrizeCardTemplate");
        if (content == null || template == null)
        {
            Debug.LogWarning("LotteryPrizeContent or LotteryPrizeCardTemplate is missing from the scene.");
            return;
        }

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);
            if (child != template && child.name.StartsWith("RuntimeLotteryPrizeCard_", StringComparison.Ordinal))
            {
                Destroy(child.gameObject);
            }
        }

        CardDrawCardResponse[] visibleCards = cards ?? new CardDrawCardResponse[0];
        SetSceneObjectActive(root, "LotteryPrizeEmptyText", visibleCards.Length == 0);
        for (int i = 0; i < visibleCards.Length; i++)
        {
            CardDrawCardResponse cardData = visibleCards[i];
            if (cardData == null)
            {
                continue;
            }

            GameObject cardObject = Instantiate(template.gameObject, content, false);
            cardObject.name = "RuntimeLotteryPrizeCard_" + cardData.slotNo;
            cardObject.SetActive(true);
            SetSceneText(cardObject.transform, "LotteryPrizeCardName", string.IsNullOrEmpty(cardData.rewardName) ? "奖励" : cardData.rewardName);
            SetSceneText(cardObject.transform, "LotteryPrizeCardAmount", $"x{Mathf.Max(0, cardData.rewardAmount)}");
            SetSceneText(cardObject.transform, "LotteryPrizeCardProbability", $"概率 {cardData.probability:P1}");
            SetSceneText(cardObject.transform, "LotteryPrizeIconText", GetLotteryIconLabel(cardData.rewardName, cardData.rewardType));
            Image icon = FindSceneComponent<Image>(cardObject.transform, "LotteryPrizeRewardImage");
            if (icon != null)
            {
                icon.sprite = ResolveLotteryPrizeSprite(cardData);
                icon.enabled = icon.sprite != null;
                SetSceneObjectActive(cardObject.transform, "LotteryPrizeIconText", icon.sprite == null);
            }
            SetSceneObjectActive(cardObject.transform, "LotteryPrizeHighlight",
                pendingCardDrawHighlightNpcId > 0 && cardData.rewardAssetId == pendingCardDrawHighlightNpcId);
            if (pendingCardDrawHighlightNpcId > 0 && cardData.rewardAssetId == pendingCardDrawHighlightNpcId)
            {
                SetSceneText(cardObject.transform, "LotteryPrizeCardName", "★ " + (string.IsNullOrEmpty(cardData.rewardName) ? "目标 NPC" : cardData.rewardName));
            }
        }
    }

    private Sprite ResolveLotteryPrizeSprite(CardDrawCardResponse card)
    {
        if (card == null) return null;

        string[] keys = { card.icon, card.rewardKey, card.rewardName };
        if (string.Equals(card.rewardType, "NPC", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveRoleSprite(keys, false);
        }

        bool equipment = string.Equals(card.rewardType, "EQUIPMENT", StringComparison.OrdinalIgnoreCase);
        foreach (string key in keys)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            Sprite sprite = equipment ? ResolveEquipmentSprite(key) : ResolveMailInventorySprite(key);
            if (sprite != null) return sprite;
        }

        return null;
    }

    private void ShowLotteryPool()
    {
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Lottery);
        Transform root = panel != null ? panel.PanelRoot : null;
        if (root == null || serverCardDrawPool == null)
        {
            SetStatus("当前奖池还未同步完成");
            return;
        }

        Transform rewardPanel = FindSceneTransform(root, "LotteryRewardConfirmPanel");
        if (rewardPanel != null)
        {
            rewardPanel.gameObject.SetActive(false);
        }

        Transform poolPanel = FindSceneTransform(root, "LotteryPoolPanel");
        Transform content = FindSceneTransform(root, "LotteryPoolContent");
        Transform template = FindSceneTransform(root, "LotteryPoolRowTemplate");
        if (poolPanel == null || content == null || template == null)
        {
            SetStatus("奖池面板节点不完整，请在 Unity 中重新生成抽奖面板");
            return;
        }

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);
            if (child != template && child.name.StartsWith("RuntimeLotteryPoolRow_", StringComparison.Ordinal))
            {
                Destroy(child.gameObject);
            }
        }

        CardDrawCardResponse[] cards = serverCardDrawPool.cards ?? new CardDrawCardResponse[0];
        SetSceneText(poolPanel, "LotteryPoolTitle", string.IsNullOrEmpty(serverCardDrawPool.name) ? "当前奖池" : serverCardDrawPool.name);
        for (int i = 0; i < cards.Length; i++)
        {
            CardDrawCardResponse cardData = cards[i];
            if (cardData == null)
            {
                continue;
            }

            GameObject rowObject = Instantiate(template.gameObject, content, false);
            rowObject.name = "RuntimeLotteryPoolRow_" + cardData.slotNo;
            rowObject.SetActive(true);
            SetSceneText(rowObject.transform, "LotteryPoolRowSlot", "#" + cardData.slotNo);
            SetSceneText(rowObject.transform, "LotteryPoolRowName", string.IsNullOrEmpty(cardData.rewardName) ? "奖励" : cardData.rewardName);
            SetSceneText(rowObject.transform, "LotteryPoolRowAmount", "数量 x" + Mathf.Max(0, cardData.rewardAmount));
            SetSceneText(rowObject.transform, "LotteryPoolRowProbability", "概率 " + cardData.probability.ToString("P1"));
        }

        poolPanel.gameObject.SetActive(true);
        poolPanel.SetAsLastSibling();
        BindSceneButton(FindSceneComponent<Button>(poolPanel, "LotteryPoolCloseButton"), CloseLotteryPool);
    }

    private void CloseLotteryPool()
    {
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Lottery);
        Transform root = panel != null ? panel.PanelRoot : null;
        Transform poolPanel = FindSceneTransform(root, "LotteryPoolPanel");
        if (poolPanel != null)
        {
            poolPanel.gameObject.SetActive(false);
        }
        ShowServerLotteryState();
    }

    private void ShowLotteryRewardConfirm(CardDrawData data)
    {
        if (data == null || data.results == null || data.results.Length == 0)
        {
            return;
        }

        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Lottery);
        Transform root = panel != null ? panel.PanelRoot : null;
        Transform rewardPanel = FindSceneTransform(root, "LotteryRewardConfirmPanel");
        Transform content = FindSceneTransform(root, "LotteryRewardContent");
        Transform template = FindSceneTransform(root, "LotteryRewardRowTemplate");
        if (rewardPanel == null || content == null || template == null)
        {
            Debug.LogWarning("Lottery reward confirmation nodes are missing from the scene.");
            return;
        }

        Transform poolPanel = FindSceneTransform(root, "LotteryPoolPanel");
        if (poolPanel != null)
        {
            poolPanel.gameObject.SetActive(false);
        }

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);
            if (child != template && child.name.StartsWith("RuntimeLotteryRewardRow_", StringComparison.Ordinal))
            {
                Destroy(child.gameObject);
            }
        }

        CardDrawResultResponse[] results = OrderCardDrawResults(data.results);
        SetSceneText(rewardPanel, "LotteryRewardConfirmTitle", data.drawCount > 1 ? "连抽结果" : "抽奖结果");
        SetSceneText(rewardPanel, "LotteryRewardConfirmSummary", "奖励已按服务器抽奖结果发放，确认后关闭此提示");
        for (int i = 0; i < results.Length; i++)
        {
            CardDrawResultResponse result = results[i];
            if (result == null)
            {
                continue;
            }

            GameObject rowObject = Instantiate(template.gameObject, content, false);
            rowObject.name = "RuntimeLotteryRewardRow_" + result.drawSequence;
            rowObject.SetActive(true);
            SetSceneText(rowObject.transform, "LotteryRewardRowSequence", result.drawSequence.ToString());
            SetSceneText(rowObject.transform, "LotteryRewardRowName", GetCardDrawResultName(result));
            SetSceneText(rowObject.transform, "LotteryRewardRowAmount", "x" + Mathf.Max(0, result.grantedRewardAmount));
            SetSceneText(rowObject.transform, "LotteryRewardRowNote", result.duplicateNpcConverted ? "重复 NPC 已转碎片" : GetRewardCategoryLabel(result.grantedRewardType));
        }

        rewardPanel.gameObject.SetActive(true);
        rewardPanel.SetAsLastSibling();
        Button confirm = FindSceneComponent<Button>(rewardPanel, "LotteryRewardConfirmButton");
        if (confirm != null)
        {
            confirm.interactable = !serverCardDrawRunning;
        }
        BindSceneButton(confirm, !serverCardDrawRunning ? (Action)ConfirmLotteryReward : null);
    }

    private void ConfirmLotteryReward()
    {
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Lottery);
        Transform root = panel != null ? panel.PanelRoot : null;
        Transform rewardPanel = FindSceneTransform(root, "LotteryRewardConfirmPanel");
        if (rewardPanel != null)
        {
            rewardPanel.gameObject.SetActive(false);
        }
        SetStatus("抽奖奖励已确认");
        ShowServerLotteryState();
    }

    private void ResetLotteryOverlayPanels()
    {
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Lottery);
        Transform root = panel != null ? panel.PanelRoot : null;
        Transform poolPanel = FindSceneTransform(root, "LotteryPoolPanel");
        Transform rewardPanel = FindSceneTransform(root, "LotteryRewardConfirmPanel");
        if (poolPanel != null)
        {
            poolPanel.gameObject.SetActive(false);
        }
        if (rewardPanel != null)
        {
            rewardPanel.gameObject.SetActive(false);
        }
    }

    private static CardDrawResultResponse[] OrderCardDrawResults(CardDrawResultResponse[] source)
    {
        CardDrawResultResponse[] ordered = new CardDrawResultResponse[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            CardDrawResultResponse selected = null;
            int expectedSequence = i + 1;
            for (int j = 0; j < source.Length; j++)
            {
                if (source[j] != null && source[j].drawSequence == expectedSequence)
                {
                    selected = source[j];
                    break;
                }
            }
            ordered[i] = selected ?? source[i];
        }
        return ordered;
    }

    private static string GetLotteryIconLabel(string rewardName, string rewardType)
    {
        if (!string.IsNullOrEmpty(rewardName))
        {
            return rewardName.Substring(0, 1);
        }
        return string.IsNullOrEmpty(rewardType) ? "奖" : rewardType.Substring(0, 1);
    }

    private static Color GetLotteryRewardColor(string rewardType)
    {
        string normalized = (rewardType ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized == "NPC") return new Color(0.62f, 0.42f, 0.78f, 1f);
        if (normalized == "EQUIPMENT") return new Color(0.82f, 0.46f, 0.22f, 1f);
        return new Color(0.25f, 0.63f, 0.78f, 1f);
    }

    private static string GetRewardCategoryLabel(string rewardType)
    {
        string normalized = (rewardType ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized == "NPC") return "角色";
        if (normalized == "EQUIPMENT") return "装备";
        if (normalized == "ITEM") return "物品";
        return string.IsNullOrEmpty(rewardType) ? "奖励" : rewardType;
    }

    private void ShowServerLotteryState(bool rolling = false)
    {
        ShowLotteryPanel(
            serverLotteryStatus,
            serverLotteryRewardName,
            serverLotteryRewardAmount,
            serverLotteryHasResult,
            rolling);
    }

    private IEnumerator GetServerAnnouncements()
    {
        UnityWebRequest request = CreateAuthorizedGet("/announcements?limit=100");
        if (request == null)
        {
            ShowAnnouncementFailure(requestCreationError);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                yield break;
            }

            AnnouncementListApiResponse response = ParseJson<AnnouncementListApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                ShowAnnouncementFailure(response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "公告返回格式错误")
                    : GetApiError(response));
                yield break;
            }

            AnnouncementResponse[] announcements = response.data.items ?? new AnnouncementResponse[0];
            if (announcements.Length == 0)
            {
                if (announcementPanelView == null || !announcementPanelView.gameObject.activeInHierarchy)
                {
                    yield break;
                }

                announcementPanelView.SetAnnouncements(null);
                announcementPanelView.BindClose(HideAnnouncementPanel);
                SetStatus("当前没有有效公告");
                yield break;
            }

            if (announcementPanelView == null || !announcementPanelView.gameObject.activeInHierarchy)
            {
                yield break;
            }

            announcementPanelView.SetAnnouncements(BuildAnnouncementPanelItems(announcements));
            announcementPanelView.BindClose(HideAnnouncementPanel);
            SetStatus($"已读取 {announcements.Length} 条服务器公告");

            for (int i = 0; i < announcements.Length; i++)
            {
                if (announcements[i] != null && !announcements[i].read)
                {
                    yield return MarkServerAnnouncementRead(announcements[i]);
                }
            }
        }
    }

    private IEnumerator MarkServerAnnouncementRead(AnnouncementResponse announcement)
    {
        if (announcement == null)
        {
            yield break;
        }

        if (!TryCreateAuthorizedPost(
            $"/announcements/{announcement.id}/read",
            "{}",
            out UnityWebRequest request,
            out string error))
        {
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogWarning("公告已读状态提交失败：" + error);
            }
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                yield break;
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                announcement.read = true;
            }
            else
            {
                Debug.LogWarning("公告已读状态提交失败：" + ApiRequestUtility.GetErrorMessage(request, "请求失败"));
            }
        }
    }

    private AnnouncementPanelView.AnnouncementItem[] BuildAnnouncementPanelItems(AnnouncementResponse[] announcements)
    {
        List<AnnouncementPanelView.AnnouncementItem> items = new List<AnnouncementPanelView.AnnouncementItem>();
        if (announcements == null)
        {
            return items.ToArray();
        }

        for (int index = 0; index < announcements.Length; index++)
        {
            AnnouncementResponse announcement = announcements[index];
            if (announcement == null)
            {
                continue;
            }

            string categoryKey = ResolveAnnouncementCategoryKey(announcement);
            string categoryName = ResolveAnnouncementCategoryName(announcement, categoryKey);
            items.Add(new AnnouncementPanelView.AnnouncementItem
            {
                categoryKey = categoryKey,
                categoryName = categoryName,
                title = announcement.title,
                version = announcement.announcementCode,
                publishDate = string.IsNullOrEmpty(announcement.startTime)
                    ? string.Empty
                    : "发布时间 " + announcement.startTime,
                body = announcement.content
            });
        }

        return items.ToArray();
    }

    private string ResolveAnnouncementCategoryKey(AnnouncementResponse announcement)
    {
        if (announcement == null)
        {
            return "SYSTEM";
        }

        if (!string.IsNullOrWhiteSpace(announcement.announcementType)) return announcement.announcementType.Trim();
        if (!string.IsNullOrWhiteSpace(announcement.type)) return announcement.type.Trim();
        if (!string.IsNullOrWhiteSpace(announcement.category)) return announcement.category.Trim();
        // V31 lists announcements, not categories. Keep separate entries even when their titles match.
        if (announcement.id > 0) return "NOTICE:" + announcement.id;
        if (!string.IsNullOrWhiteSpace(announcement.announcementCode)) return "NOTICE:" + announcement.announcementCode.Trim();
        if (!string.IsNullOrWhiteSpace(announcement.title)) return announcement.title.Trim();
        return "SYSTEM";
    }

    private string ResolveAnnouncementCategoryName(AnnouncementResponse announcement, string categoryKey)
    {
        if (announcement == null)
        {
            return "系统公告";
        }

        if (!string.IsNullOrWhiteSpace(announcement.announcementTypeName)) return announcement.announcementTypeName.Trim();
        if (!string.IsNullOrWhiteSpace(announcement.typeName)) return announcement.typeName.Trim();
        if (!string.IsNullOrWhiteSpace(announcement.categoryName)) return announcement.categoryName.Trim();
        if (!string.IsNullOrWhiteSpace(announcement.announcementType)) return announcement.announcementType.Trim();
        if (!string.IsNullOrWhiteSpace(announcement.type)) return announcement.type.Trim();
        if (!string.IsNullOrWhiteSpace(announcement.category)) return announcement.category.Trim();
        if (!string.IsNullOrWhiteSpace(announcement.title)) return announcement.title.Trim();
        if (categoryKey != null && categoryKey.StartsWith("NOTICE:", StringComparison.Ordinal)) return "公告";
        return string.IsNullOrEmpty(categoryKey) ? "系统公告" : categoryKey;
    }

    private void ShowAnnouncementFailure(string error)
    {
        if (announcementPanelView != null && announcementPanelView.gameObject.activeInHierarchy)
        {
            announcementPanelView.SetStatus("同步失败", error);
            announcementPanelView.BindClose(HideAnnouncementPanel);
        }
        SetStatus("公告同步失败：" + error);
    }

    private void ShowGoldRewardAd()
    {
        if (!HasAuthSession())
        {
            SetStatus("请先登录账号后领取服务器广告奖励。 ");
            return;
        }

        if (rewardAdRunning)
        {
            SetStatus("广告奖励正在处理中，请稍候。 ");
            return;
        }

        StartCoroutine(GetRewardAdConfigAndPrepare("GOLD_SHORTAGE"));
    }

    private IEnumerator GetRewardAdConfigAndPrepare(string scene)
    {
        rewardAdRunning = true;
        SetStatus("正在读取激励广告配置...");
        UnityWebRequest configRequest = CreateAuthorizedGet("/ad/reward/config");
        if (configRequest == null)
        {
            rewardAdRunning = false;
            SetStatus(requestCreationError);
            yield break;
        }

        using (configRequest)
        {
            yield return configRequest.SendWebRequest();
            if (HandleUnauthorizedResponse(configRequest))
            {
                rewardAdRunning = false;
                yield break;
            }

            AdRewardConfigApiResponse response = ParseJson<AdRewardConfigApiResponse>(configRequest.downloadHandler.text);
            if (configRequest.result != UnityWebRequest.Result.Success || response == null || response.code != 200)
            {
                rewardAdRunning = false;
                SetStatus("广告配置读取失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(configRequest, "请求失败")
                    : GetApiError(response)));
                yield break;
            }

            bool configured = false;
            if (response.data != null)
            {
                for (int i = 0; i < response.data.Length; i++)
                {
                    if (response.data[i] != null && response.data[i].scene == scene)
                    {
                        configured = true;
                        break;
                    }
                }
            }

            if (!configured)
            {
                rewardAdRunning = false;
                SetStatus("服务器未开放金币激励广告。 ");
                yield break;
            }
        }

        AdRewardPrepareRequest payload = new AdRewardPrepareRequest
        {
            playerId = GameSession.PlayerId,
            scene = scene
        };
        if (!TryCreateAuthorizedPost("/ad/reward/prepare", JsonUtility.ToJson(payload), out UnityWebRequest prepareRequest, out string error))
        {
            rewardAdRunning = false;
            SetStatus(error);
            yield break;
        }

        using (prepareRequest)
        {
            yield return prepareRequest.SendWebRequest();
            if (HandleUnauthorizedResponse(prepareRequest))
            {
                rewardAdRunning = false;
                yield break;
            }

            AdRewardPrepareApiResponse response = ParseJson<AdRewardPrepareApiResponse>(prepareRequest.downloadHandler.text);
            if (prepareRequest.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                rewardAdRunning = false;
                SetStatus("广告准备失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(prepareRequest, "请求失败")
                    : GetApiError(response)));
                yield break;
            }

            pendingRewardAdWatchId = response.data.watchId;
            SetStatus($"观看完整广告可获得 {GetRewardDisplayName("ITEM", response.data.rewardItemKey)} x{response.data.rewardAmount}");
            WeChatPlatformBridge.RequestRewardedVideo(this, response.data.watchId, response.data.adUnitId);
        }
    }

    public void OnWeChatRewardedVideoCompleted(string watchId)
    {
        if (!rewardAdRunning || string.IsNullOrEmpty(watchId) || watchId != pendingRewardAdWatchId)
        {
            return;
        }

        StartCoroutine(CompleteAndClaimRewardAd(watchId));
    }

    public void OnWeChatRewardedVideoClosed(string watchId)
    {
        if (watchId != pendingRewardAdWatchId)
        {
            return;
        }

        rewardAdRunning = false;
        pendingRewardAdWatchId = string.Empty;
        SetStatus("广告未完整观看，本次不会提交或领取奖励。 ");
    }

    public void OnWeChatRewardedVideoFailed(string message)
    {
        rewardAdRunning = false;
        pendingRewardAdWatchId = string.Empty;
        SetStatus("激励广告打开失败：" + message);
    }

    private IEnumerator CompleteAndClaimRewardAd(string watchId)
    {
        SetStatus("正在确认广告观看结果...");
        AdRewardCompleteRequest completePayload = new AdRewardCompleteRequest
        {
            playerId = GameSession.PlayerId,
            watchId = watchId,
            completed = true
        };
        if (!TryCreateAuthorizedPost("/ad/reward/complete", JsonUtility.ToJson(completePayload), out UnityWebRequest completeRequest, out string error))
        {
            rewardAdRunning = false;
            pendingRewardAdWatchId = string.Empty;
            SetStatus(error);
            yield break;
        }

        string claimToken;
        using (completeRequest)
        {
            yield return completeRequest.SendWebRequest();
            if (HandleUnauthorizedResponse(completeRequest))
            {
                rewardAdRunning = false;
                yield break;
            }

            AdRewardCompleteApiResponse response = ParseJson<AdRewardCompleteApiResponse>(completeRequest.downloadHandler.text);
            if (completeRequest.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                rewardAdRunning = false;
                pendingRewardAdWatchId = string.Empty;
                SetStatus("广告确认失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(completeRequest, "请求失败")
                    : GetApiError(response)));
                yield break;
            }

            claimToken = response.data.claimToken;
        }

        AdRewardClaimRequest claimPayload = new AdRewardClaimRequest
        {
            playerId = GameSession.PlayerId,
            watchId = watchId,
            claimToken = claimToken
        };
        if (!TryCreateAuthorizedPost("/ad/reward/claim", JsonUtility.ToJson(claimPayload), out UnityWebRequest claimRequest, out error))
        {
            rewardAdRunning = false;
            pendingRewardAdWatchId = string.Empty;
            SetStatus(error);
            yield break;
        }

        using (claimRequest)
        {
            yield return claimRequest.SendWebRequest();
            if (HandleUnauthorizedResponse(claimRequest))
            {
                rewardAdRunning = false;
                yield break;
            }

            AdRewardClaimApiResponse response = ParseJson<AdRewardClaimApiResponse>(claimRequest.downloadHandler.text);
            rewardAdRunning = false;
            pendingRewardAdWatchId = string.Empty;
            if (claimRequest.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                SetStatus("广告奖励领取失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(claimRequest, "请求失败")
                    : GetApiError(response)));
                yield break;
            }

            if (response.data.items != null)
            {
                SyncInventoryResourceValues(response.data.items);
            }
            SetStatus($"广告奖励已领取：{GetRewardDisplayName("ITEM", response.data.rewardItemKey)} x{response.data.rewardAmount}");
            StartCoroutine(RefreshAccountSnapshot(string.Empty));
        }
    }

#pragma warning disable 0649
    [Serializable]
    private class ServerTaskListApiResponse : BasicApiResponse
    {
        public ServerTaskListData data;
    }

    [Serializable]
    private class ServerTaskListData
    {
        public string businessTime;
        public string businessDate;
        public string dailyPeriodKey;
        public string weeklyPeriodKey;
        public ServerTaskResponse[] tasks;
    }

    [Serializable]
    private class ServerTaskResponse
    {
        public long id;
        public string taskCode;
        public string taskCategory;
        public string taskName;
        public string description;
        public string periodType;
        public string periodKey;
        public string objectiveType;
        public long targetSceneId;
        public string targetSceneKey;
        public string targetSceneName;
        public long targetItemId;
        public string targetItemKey;
        public string targetItemName;
        public string targetItemIcon;
        public string targetItemType;
        public long targetAmount;
        public long progressAmount;
        public long experienceReward;
        public TaskRewardSnapshot[] fixedRewards;
        public string randomNpcPoolName;
        public int randomNpcDrawCount;
        public TaskRewardSnapshot[] randomNpcCandidates;
        public string status;
        public bool canClaim;
        public string completedTime;
        public string claimedTime;
    }

    [Serializable]
    private class ServerTaskClaimApiResponse : BasicApiResponse
    {
        public ServerTaskClaimData data;
    }

    [Serializable]
    private class ServerTaskClaimData
    {
        public ServerTaskResponse task;
        public ExperienceGrantResponse experienceGrant;
        public TaskRewardGrant[] rewardGrants;
    }

    [Serializable]
    private class TaskRewardSnapshot
    {
        public int rewardSeq;
        public string rewardType;
        public long assetId;
        public string assetKey;
        public string assetName;
        public string assetIcon;
        public string assetCategory;
        public string assetQuality;
        public long quantity;
        public int probabilityUnits;
        public float probabilityPercent;
        public TaskDuplicateCompensationSnapshot duplicateCompensation;
    }

    [Serializable]
    private class TaskDuplicateCompensationSnapshot
    {
        public long itemId;
        public string itemKey;
        public string itemName;
        public string itemIcon;
        public long quantity;
    }

    [Serializable]
    private class TaskRewardGrant
    {
        public int grantSeq;
        public string rewardSource;
        public int drawIndex;
        public long selectedNpcId;
        public string selectedNpcCode;
        public string selectedNpcName;
        public string selectedNpcIcon;
        public string selectedNpcQuality;
        public string grantedType;
        public long grantedAssetId;
        public string grantedAssetKey;
        public string grantedAssetName;
        public string grantedAssetIcon;
        public string grantedAssetCategory;
        public string grantedAssetQuality;
        public long grantedQuantity;
        public bool duplicateNpcConverted;
        public long playerNpcInstanceId;
    }

    [Serializable]
    private class ExperienceGrantResponse
    {
        public long playerId;
        public string sourceType;
        public string sourceId;
        public long requestedExperience;
        public long grantedExperience;
        public long beforeExperience;
        public long afterExperience;
        public int beforeLevel;
        public int afterLevel;
        public bool leveledUp;
        public bool maxLevel;
        public long currentLevelExperience;
        public long requiredExperienceForNextLevel;
        public int experienceProgressBps;
        public long curveRevision;
        public bool replayed;
    }

    [Serializable]
    private class SignInOverviewApiResponse : BasicApiResponse
    {
        public int contractVersion;
        public SignInOverviewData data;
    }

    [Serializable]
    private class SignInOverviewData
    {
        public int contractVersion;
        public string businessTime;
        public SignInPlanResponse[] plans;
    }

    [Serializable]
    private class SignInPlanResponse
    {
        public string planCode;
        public int contractVersion;
        public int planVersion;
        public long minimumClientVersionCode;
        public string planName;
        public string planType;
        public string activityCode;
        public string activityName;
        public string activityDescription;
        public string description;
        public string startTime;
        public string endTime;
        public string activityStartTime;
        public string activityEndTime;
        public string periodKey;
        public string rewardMode;
        public int rewardWindowDays;
        public int currentDay;
        public int windowDays;
        public string expiresAt;
        public bool canClaim;
        public bool allowMakeup;
        public bool canMakeup;
        public SignInCostResponse makeupCost;
        public SignInDayResponse[] days;
    }

    [Serializable]
    private class SignInDayResponse
    {
        public int rewardDay;
        public string status;
        public bool claimed;
        public bool canClaim;
        public bool canMakeup;
        public string claimedAt;
        public SignInRewardPreview[] rewards;
    }

    [Serializable]
    private class SignInRewardPreview
    {
        public int rewardSeq;
        public string rewardType;
        public string rewardKey;
        public string rewardQuality;
        public long rewardAssetId;
        public string rewardName;
        public string rewardIcon;
        public string rewardCategory;
        public long rewardAmount;
        public string fallbackCondition;
        public string fallbackRewardType;
        public string fallbackRewardKey;
        public string fallbackRewardQuality;
        public long fallbackRewardAssetId;
        public string fallbackRewardName;
        public string fallbackRewardIcon;
        public string fallbackRewardCategory;
        public long fallbackRewardAmount;
    }

    [Serializable]
    private class SignInCostResponse
    {
        public long itemId;
        public string itemKey;
        public string itemName;
        public string icon;
        public string itemCategory;
        public long amount;
    }

    [Serializable]
    private class SignInClaimRequest
    {
        public string planCode;
    }

    [Serializable]
    private class SignInMakeupRequest
    {
        public string planCode;
        public int rewardDay;
    }

    [Serializable]
    private class SignInClaimApiResponse : BasicApiResponse
    {
        public SignInClaimData data;
    }

    [Serializable]
    private class SignInClaimData
    {
        public long signInRecordId;
        public string planCode;
        public string periodKey;
        public string claimType;
        public int rewardDay;
        public string claimDate;
        public string businessTime;
        public SignInGrantedReward[] rewards;
        public SignInCostResponse cost;
        public InventoryItemResponse[] items;
    }

    [Serializable]
    private class SignInGrantedReward
    {
        public int rewardSeq;
        public string configuredRewardType;
        public string configuredRewardKey;
        public string configuredRewardQuality;
        public long configuredAssetId;
        public string configuredRewardName;
        public string configuredRewardIcon;
        public string configuredRewardCategory;
        public long configuredRewardAmount;
        public string grantedRewardType;
        public string grantedRewardKey;
        public string grantedRewardQuality;
        public long grantedAssetId;
        public string grantedRewardName;
        public string grantedRewardIcon;
        public string grantedRewardCategory;
        public long grantedRewardAmount;
        public bool fallbackApplied;
    }

    [Serializable]
    private class CardDrawPoolApiResponse : BasicApiResponse
    {
        public CardDrawPoolData data;
    }

    [Serializable]
    private class CardDrawPoolData
    {
        public string poolCode;
        public int version;
        public string name;
        public int cardCount;
        public string businessTime;
        public CardDrawCardResponse[] cards;
        public CardDrawOptionResponse[] options;
    }

    [Serializable]
    private class CardDrawCardResponse
    {
        public int slotNo;
        public string rewardType;
        public string rewardKey;
        public long rewardAssetId;
        public string rewardName;
        public string icon;
        public string rewardCategory;
        public int rewardAmount;
        public int weight;
        public float probability;
    }

    [Serializable]
    private class CardDrawOptionResponse
    {
        public string optionCode;
        public int drawCount;
        public string costItemKey;
        public string costItemName;
        public int costAmount;
        public int savingAmount;
    }

    [Serializable]
    private class CardDrawRequest
    {
        public string optionCode;
    }

    [Serializable]
    private class CardDrawApiResponse : BasicApiResponse
    {
        public CardDrawData data;
    }

    [Serializable]
    private class CardDrawData
    {
        public long drawBatchId;
        public string poolCode;
        public int poolVersion;
        public string optionCode;
        public int drawCount;
        public int cardCount;
        public string costItemKey;
        public int costAmount;
        public int remainingDiamond;
        public string businessTime;
        public CardDrawCardResponse[] cards;
        public CardDrawResultResponse[] results;
    }

    [Serializable]
    private class CardDrawResultResponse
    {
        public long drawResultId;
        public int drawSequence;
        public int winningSlot;
        public string configuredRewardType;
        public string configuredRewardKey;
        public long configuredAssetId;
        public string configuredRewardName;
        public string configuredRewardIcon;
        public string configuredRewardCategory;
        public int configuredRewardAmount;
        public string grantedRewardType;
        public string grantedRewardKey;
        public long grantedAssetId;
        public string grantedRewardName;
        public string grantedRewardIcon;
        public string grantedRewardCategory;
        public int grantedRewardAmount;
        public bool duplicateNpcConverted;
    }

    [Serializable]
    private class AnnouncementListApiResponse : BasicApiResponse
    {
        public AnnouncementListData data;
    }

    [Serializable]
    private class AnnouncementListData
    {
        public AnnouncementResponse[] items;
        public int unreadCount;
    }

    [Serializable]
    private class AnnouncementResponse
    {
        public long id;
        public string announcementCode;
        public string announcementType;
        public string announcementTypeName;
        public string type;
        public string typeName;
        public string category;
        public string categoryName;
        public string title;
        public string content;
        public int priority;
        public bool pinned;
        public string startTime;
        public string endTime;
        public bool read;
        public string readTime;
    }

    [Serializable]
    private class AdRewardConfigApiResponse : BasicApiResponse
    {
        public AdRewardConfigResponse[] data;
    }

    [Serializable]
    private class AdRewardConfigResponse
    {
        public string scene;
        public string adUnitId;
        public string rewardItemKey;
        public int rewardAmount;
        public int dailyLimit;
        public int cooldownSeconds;
        public int expireSeconds;
        public int minWatchSeconds;
    }

    [Serializable]
    private class AdRewardPrepareRequest
    {
        public int playerId;
        public string scene;
    }

    [Serializable]
    private class AdRewardPrepareApiResponse : BasicApiResponse
    {
        public AdRewardPrepareResponse data;
    }

    [Serializable]
    private class AdRewardPrepareResponse
    {
        public string watchId;
        public string scene;
        public string adUnitId;
        public string rewardItemKey;
        public int rewardAmount;
        public int dailyLimit;
        public int cooldownSeconds;
        public int expireSeconds;
        public int minWatchSeconds;
        public int remainingSeconds;
        public string expireTime;
    }

    [Serializable]
    private class AdRewardCompleteRequest
    {
        public int playerId;
        public string watchId;
        public bool completed;
    }

    [Serializable]
    private class AdRewardCompleteApiResponse : BasicApiResponse
    {
        public AdRewardCompleteResponse data;
    }

    [Serializable]
    private class AdRewardCompleteResponse
    {
        public string watchId;
        public string claimToken;
        public string scene;
        public string rewardItemKey;
        public int rewardAmount;
        public int minWatchSeconds;
        public string completeTime;
        public string expireTime;
        public int remainingSeconds;
    }

    [Serializable]
    private class AdRewardClaimRequest
    {
        public int playerId;
        public string watchId;
        public string claimToken;
    }

    [Serializable]
    private class AdRewardClaimApiResponse : BasicApiResponse
    {
        public AdRewardClaimResponse data;
    }

    [Serializable]
    private class AdRewardClaimResponse
    {
        public int playerId;
        public string watchId;
        public string scene;
        public string rewardItemKey;
        public int rewardAmount;
        public InventoryItemResponse[] items;
    }
#pragma warning restore 0649
}
