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

    private SignInOverviewData serverSignInOverview;
    private string serverSignPlanCode = "MONTHLY";
    private string serverSignStatus = string.Empty;
    private int serverSignPage;
    private bool serverSignOverviewRunning;
    private bool serverSignMutationRunning;

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

    private bool rewardAdRunning;
    private string pendingRewardAdWatchId = string.Empty;

    private void RefreshHomeResourceMetrics()
    {
        if (boundBookView != null)
        {
            SetBookMetricValue(boundBookView.MetricTexts, 0, GetHomePurificationAgent().ToString());
            SetBookMetricValue(boundBookView.MetricTexts, 1, GetHomeStamina().ToString());
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
        SetSceneText(root, "ProfileGold", PlayerPrefs.GetInt("Gold", 1000).ToString());
        SetSceneText(root, "ProfileWood", PlayerPrefs.GetInt("Wood", 500).ToString());
        SetSceneText(root, "ProfileStone", PlayerPrefs.GetInt("Stone", 500).ToString());
        SetSceneText(root, "ProfileIron", PlayerPrefs.GetInt("Iron", 300).ToString());
        SetSceneText(root, "ProfileVital", $"生命  {PlayerPrefs.GetInt("PlayerHp", 1000)}    体力  {GetHomeStamina()}");
    }

    private void OpenServerTasks(string category)
    {
        activeTaskCategory = NormalizeServerTaskCategory(category);
        string message = serverTaskListRunning ? "正在同步服务器任务..." : "正在读取服务器任务...";
        ShowTaskPanel(activeTaskCategory, message);
        if (!serverTaskListRunning)
        {
            StartCoroutine(GetServerTasks());
        }
    }

    private IEnumerator GetServerTasks()
    {
        serverTaskListRunning = true;
        UnityWebRequest request = CreateAuthorizedGet("/tasks");
        if (request == null)
        {
            serverTaskListRunning = false;
            ShowTaskPanel(activeTaskCategory, requestCreationError);
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
                ShowTaskPanel(activeTaskCategory, "任务同步失败：" + error);
                yield break;
            }

            currentServerTasks = response.data.tasks ?? new ServerTaskResponse[0];
            serverTaskListRunning = false;
            string period = activeTaskCategory == "main" ? response.data.weeklyPeriodKey : response.data.dailyPeriodKey;
            ShowTaskPanel(activeTaskCategory, $"服务器周期 {period} · {response.data.businessTime}");
        }
    }

    private TaskDisplayItem[] BuildServerTaskItems(string category)
    {
        List<TaskDisplayItem> result = new List<TaskDisplayItem>();
        for (int i = 0; i < currentServerTasks.Length; i++)
        {
            ServerTaskResponse task = currentServerTasks[i];
            if (task == null || !MatchesServerTaskCategory(task, category))
            {
                continue;
            }

            string rewardSummary = BuildServerTaskRewardSummary(task);

            result.Add(new TaskDisplayItem(
                string.IsNullOrEmpty(task.taskCode) ? task.id.ToString() : task.taskCode,
                string.IsNullOrEmpty(task.taskName) ? "服务器任务" : task.taskName,
                BuildServerTaskDescription(task),
                Mathf.Max(0, task.progressAmount),
                Mathf.Max(1, task.targetAmount),
                "Experience",
                "经验",
                Mathf.Max(0, task.experienceReward),
                string.Equals(task.periodType, "DAILY", StringComparison.OrdinalIgnoreCase),
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
                ShowTaskPanel(activeTaskCategory, "领取失败：" + message);
                yield break;
            }

            ApplyExperienceGrant(response.data.experienceGrant);
            ApplyServerTaskRewardGrants(response.data.rewardGrants);
            UpdateServerTask(response.data.task);
            serverTaskClaimRunning = false;
            int granted = response.data.experienceGrant != null ? response.data.experienceGrant.grantedExperience : 0;
            ShowTaskPanel(activeTaskCategory, granted > 0 ? $"领取成功：经验 +{granted}" : "任务奖励已领取");
            StartCoroutine(RefreshAccountSnapshot(string.Empty));
        }
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
        if (task == null) return false;
        string taskCategory = (task.taskCategory ?? string.Empty).Trim().ToUpperInvariant();
        string periodType = (task.periodType ?? string.Empty).Trim().ToUpperInvariant();
        switch (NormalizeServerTaskCategory(category))
        {
            case "main":
                return periodType == "WEEKLY" || taskCategory == "MAIN";
            case "growth":
                return taskCategory == "GROWTH" || taskCategory == "ACHIEVEMENT" || periodType == "LIFETIME";
            case "activity":
                return taskCategory == "ACTIVITY";
            default:
                return periodType == "DAILY" || taskCategory == "ROUTINE" && periodType != "WEEKLY" && periodType != "LIFETIME";
        }
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

        if (task != null && !string.IsNullOrEmpty(task.randomNpcPoolName))
        {
            rewards.Add(task.randomNpcPoolName + (task.randomNpcDrawCount > 0 ? $" x{task.randomNpcDrawCount}" : string.Empty));
        }

        if (task != null && task.experienceReward > 0) rewards.Add("经验 x" + task.experienceReward);
        return rewards.Count == 0 ? "无固定奖励" : string.Join("、", rewards.ToArray());
    }

    private void ApplyServerTaskRewardGrants(TaskRewardGrant[] grants)
    {
        if (grants == null || grants.Length == 0) return;

        bool needsInventory = false;
        bool needsNpc = false;
        bool needsEquipment = false;
        List<string> names = new List<string>();
        for (int i = 0; i < grants.Length; i++)
        {
            TaskRewardGrant grant = grants[i];
            if (grant == null) continue;
            string type = (grant.grantedType ?? string.Empty).Trim().ToUpperInvariant();
            if (type == "ITEM") needsInventory = true;
            if (type == "NPC") needsNpc = true;
            if (type == "EQUIPMENT") needsEquipment = true;
            string name = string.IsNullOrEmpty(grant.grantedAssetName) ? grant.selectedNpcName : grant.grantedAssetName;
            if (!string.IsNullOrEmpty(name)) names.Add(name + " x" + Math.Max(0, grant.grantedQuantity));
        }

        if (names.Count > 0) SetStatus("任务到账：" + string.Join("、", names.ToArray()));
        StartCoroutine(RefreshTaskGrantState(needsInventory, needsNpc, needsEquipment));
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
        PlayerPrefs.SetInt("PlayerCurrentLevelExperience", Mathf.Max(0, grant.currentLevelExperience));
        PlayerPrefs.SetInt("PlayerRequiredExperience", Mathf.Max(0, grant.requiredExperienceForNextLevel));
        PlayerPrefs.SetInt("PlayerExperienceProgressBps", Mathf.Clamp(grant.experienceProgressBps, 0, 10000));
        PlayerPrefs.SetInt("PlayerLevelCurveRevision", Mathf.Max(0, grant.curveRevision));
        PlayerPrefs.SetInt("PlayerMaxLevel", grant.maxLevel ? 1 : 0);
        PlayerPrefs.Save();
        WeChatPlatformBridge.SyncFriendLevel(grant.afterLevel);
        RefreshHomeResourceMetrics();
    }

    private void OpenServerSignIn(string planCode)
    {
        serverSignPlanCode = string.Equals(planCode, "NEWBIE", StringComparison.OrdinalIgnoreCase) ? "NEWBIE" : "MONTHLY";
        serverSignPage = 0;
        serverSignStatus = "正在读取服务器签到配置...";
        ShowCurrentServerSignPanel();
        if (!serverSignOverviewRunning)
        {
            StartCoroutine(GetServerSignInOverview());
        }
    }

    private void ShowCurrentServerSignPanel()
    {
        if (serverSignPlanCode == "NEWBIE")
        {
            ShowNewbieSignPanel(0, serverSignStatus);
        }
        else
        {
            ShowDailySignPanel(0, false, serverSignStatus);
        }
    }

    private IEnumerator GetServerSignInOverview()
    {
        serverSignOverviewRunning = true;
        UnityWebRequest request = CreateAuthorizedGet("/sign-in/overview");
        if (request == null)
        {
            serverSignOverviewRunning = false;
            serverSignStatus = requestCreationError;
            ShowCurrentServerSignPanel();
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                serverSignOverviewRunning = false;
                yield break;
            }

            SignInOverviewApiResponse response = ParseJson<SignInOverviewApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                serverSignOverviewRunning = false;
                serverSignStatus = "签到同步失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "返回数据格式错误")
                    : GetApiError(response));
                ShowCurrentServerSignPanel();
                yield break;
            }

            serverSignInOverview = response.data;
            serverSignOverviewRunning = false;
            SignInPlanResponse plan = FindServerSignPlan(serverSignPlanCode);
            serverSignStatus = plan == null ? "当前签到计划未开放" : "签到配置已同步";
            if (plan != null && serverSignPlanCode == "MONTHLY" && plan.currentDay > 0)
            {
                serverSignPage = Mathf.Max(0, (plan.currentDay - 1) / 14);
            }
            ClampServerSignPage(plan);
            ShowCurrentServerSignPanel();
        }
    }

    private void ConfigureServerSignScenePanel(Transform root, string planCode, string status)
    {
        serverSignPlanCode = planCode;
        SignInPlanResponse plan = FindServerSignPlan(planCode);
        int cardCount = planCode == "NEWBIE" ? 3 : 14;
        string prefix = planCode == "NEWBIE" ? "NewbieSignCard" : "DailySignCard";
        string statusName = planCode == "NEWBIE" ? "NewbieSignStatus" : "DailySignStatus";
        int pageSize = planCode == "NEWBIE" ? 3 : 14;
        ClampServerSignPage(plan);

        if (plan == null)
        {
            for (int i = 0; i < cardCount; i++)
            {
                Transform missingCard = FindSceneTransform(root, prefix + (i + 1));
                if (missingCard != null)
                {
                    missingCard.gameObject.SetActive(false);
                }
            }
            SetSceneText(root, statusName, string.IsNullOrEmpty(status) ? "当前签到计划未开放" : status);
            ConfigureServerSignPaging(root, 1);
            return;
        }

        int firstDayIndex = serverSignPage * pageSize;
        SignInDayResponse[] days = plan.days ?? new SignInDayResponse[0];
        for (int i = 0; i < cardCount; i++)
        {
            Transform card = FindSceneTransform(root, prefix + (i + 1));
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

            SignInDayResponse day = days[dayIndex];
            string rewardName;
            string rewardAmount;
            BuildSignRewardDisplay(day.rewards, out rewardName, out rewardAmount);
            bool highlighted = day.canClaim || day.canMakeup;
            ConfigureSignSceneCard(card, day.rewardDay, rewardName, rewardAmount, day.claimed, highlighted);

            Button cardButton = card.GetComponent<Button>();
            if (cardButton == null)
            {
                cardButton = card.gameObject.AddComponent<Button>();
                cardButton.targetGraphic = card.GetComponent<Image>();
            }

            SignInDayResponse capturedDay = day;
            cardButton.interactable = !serverSignMutationRunning && (capturedDay.canClaim || capturedDay.canMakeup);
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

        int pageCount = Mathf.Max(1, Mathf.CeilToInt(days.Length / (float)pageSize));
        ConfigureServerSignPaging(root, pageCount);
        string mutation = serverSignMutationRunning ? "正在提交..." : status;
        string cost = plan.makeupCost == null
            ? string.Empty
            : $" · 补签 {plan.makeupCost.itemName}x{plan.makeupCost.amount}";
        SetSceneText(
            root,
            statusName,
            $"{plan.planName}  已领 {claimedCount}/{days.Length}  第{serverSignPage + 1}/{pageCount}页{cost}  {mutation}");
    }

    private void ConfigureServerSignPaging(Transform root, int pageCount)
    {
        if (serverSignPlanCode != "MONTHLY")
        {
            SetSceneObjectActive(root, "ServerSignPreviousButton", false);
            SetSceneObjectActive(root, "ServerSignNextButton", false);
            return;
        }

        Transform panel = FindSceneTransform(root, "DailySignPanel");
        if (panel == null)
        {
            return;
        }

        Button previous = FindSceneComponent<Button>(panel, "ServerSignPreviousButton");
        if (previous == null)
        {
            previous = CreateButton(panel, "ServerSignPreviousButton", "<", new Color(0.24f, 0.43f, 0.74f, 1f), Color.white);
            SetRect(previous.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(390f, -91f), new Vector2(52f, 42f));
            ConfigureClearButtonLabel(previous, 24);
        }

        Button next = FindSceneComponent<Button>(panel, "ServerSignNextButton");
        if (next == null)
        {
            next = CreateButton(panel, "ServerSignNextButton", ">", new Color(0.24f, 0.43f, 0.74f, 1f), Color.white);
            SetRect(next.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(768f, -91f), new Vector2(52f, 42f));
            ConfigureClearButtonLabel(next, 24);
        }

        previous.gameObject.SetActive(pageCount > 1);
        next.gameObject.SetActive(pageCount > 1);
        previous.interactable = serverSignPage > 0 && !serverSignMutationRunning;
        next.interactable = serverSignPage + 1 < pageCount && !serverSignMutationRunning;
        BindSceneButton(previous, () => ChangeServerSignPage(-1));
        BindSceneButton(next, () => ChangeServerSignPage(1));
    }

    private void ChangeServerSignPage(int offset)
    {
        SignInPlanResponse plan = FindServerSignPlan(serverSignPlanCode);
        int count = plan != null && plan.days != null ? plan.days.Length : 0;
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(count / 14f));
        serverSignPage = Mathf.Clamp(serverSignPage + offset, 0, pageCount - 1);
        ShowCurrentServerSignPanel();
    }

    private void ClampServerSignPage(SignInPlanResponse plan)
    {
        int pageSize = serverSignPlanCode == "NEWBIE" ? 3 : 14;
        int count = plan != null && plan.days != null ? plan.days.Length : 0;
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(count / (float)pageSize));
        serverSignPage = Mathf.Clamp(serverSignPage, 0, pageCount - 1);
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

    private void BeginServerSignClaim(string planCode, SignInDayResponse day)
    {
        if (day == null || serverSignMutationRunning)
        {
            return;
        }

        if (day.canClaim)
        {
            StartCoroutine(ClaimServerSignIn(planCode));
        }
        else if (day.canMakeup)
        {
            StartCoroutine(MakeupServerSignIn(planCode, day.rewardDay));
        }
    }

    private IEnumerator ClaimServerSignIn(string planCode)
    {
        serverSignMutationRunning = true;
        serverSignStatus = "正在领取签到奖励...";
        ShowCurrentServerSignPanel();
        SignInClaimRequest payload = new SignInClaimRequest { planCode = planCode };
        yield return SendServerSignMutation("/sign-in/claim", JsonUtility.ToJson(payload));
    }

    private IEnumerator MakeupServerSignIn(string planCode, int rewardDay)
    {
        serverSignMutationRunning = true;
        serverSignStatus = $"正在补签第 {rewardDay} 天...";
        ShowCurrentServerSignPanel();
        SignInMakeupRequest payload = new SignInMakeupRequest { planCode = planCode, rewardDay = rewardDay };
        yield return SendServerSignMutation("/sign-in/makeup", JsonUtility.ToJson(payload));
    }

    private IEnumerator SendServerSignMutation(string path, string json)
    {
        if (!TryCreateAuthorizedPost(path, json, out UnityWebRequest request, out string error))
        {
            serverSignMutationRunning = false;
            serverSignStatus = error;
            ShowCurrentServerSignPanel();
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                serverSignMutationRunning = false;
                yield break;
            }

            SignInClaimApiResponse response = ParseJson<SignInClaimApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                serverSignMutationRunning = false;
                serverSignStatus = "签到操作失败：" + (response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "请求失败")
                    : GetApiError(response));
                ShowCurrentServerSignPanel();
                if (response != null && response.code == 6002)
                {
                    StartCoroutine(GetServerSignInOverview());
                }
                yield break;
            }

            if (response.data.items != null)
            {
                currentInventoryItems = response.data.items;
                SyncInventoryResourceValues(response.data.items);
            }

            serverSignMutationRunning = false;
            serverSignStatus = BuildSignClaimResult(response.data);
            yield return RefreshSignInRewardState(response.data);
            StartCoroutine(RefreshAccountSnapshot(string.Empty));
            yield return GetServerSignInOverview();
        }
    }

    private IEnumerator RefreshSignInRewardState(SignInClaimData data)
    {
        bool needsInventoryRefresh = false;
        bool needsEquipmentRefresh = false;
        bool needsNpcRefresh = false;
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
            yield return RefreshCardDrawEquipmentState(syncedCategories, failedCategories);
        }
        if (needsNpcRefresh)
        {
            yield return RefreshCardDrawNpcState(syncedCategories, failedCategories);
        }

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

    private string BuildSignClaimResult(SignInClaimData data)
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
        return rewards.Count == 0 ? prefix : prefix + "：" + string.Join("、", rewards.ToArray());
    }

    private void BuildSignRewardDisplay(SignInRewardPreview[] rewards, out string rewardName, out string rewardAmount)
    {
        if (rewards == null || rewards.Length == 0)
        {
            rewardName = "暂无奖励";
            rewardAmount = string.Empty;
            return;
        }

        List<string> names = new List<string>();
        int totalAmount = 0;
        for (int i = 0; i < rewards.Length; i++)
        {
            SignInRewardPreview reward = rewards[i];
            if (reward == null)
            {
                continue;
            }

            names.Add(string.IsNullOrWhiteSpace(reward.rewardName)
                ? GetRewardDisplayName(reward.rewardType, reward.rewardKey)
                : reward.rewardName);
            totalAmount += Mathf.Max(0, reward.rewardAmount);
        }

        rewardName = names.Count == 0 ? "暂无奖励" : string.Join("/", names.ToArray());
        rewardAmount = rewards.Length == 1 ? "x" + totalAmount : rewards.Length + "项奖励";
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
        serverLotteryStatus = "正在读取服务器奖池...";
        serverLotteryRewardName = string.Empty;
        serverLotteryRewardAmount = 0;
        serverLotteryHasResult = false;
        serverLotteryDisplayPage = 0;
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
            serverLotteryStatus = $"{response.data.name} · 奖池版本 {response.data.version}";
            ShowServerLotteryState();
        }
    }

    private void BeginServerCardDraw(string optionCode)
    {
        if (serverCardDrawRunning || serverCardDrawPoolRunning)
        {
            return;
        }

        CardDrawOptionResponse option = FindCardDrawOption(optionCode);
        if (option == null)
        {
            serverLotteryStatus = "当前奖池没有可用的" + (optionCode == "FIVE" ? "五连抽" : "单抽") + "档位";
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
        serverLotteryStatus = optionCode == "FIVE" ? "正在执行五连抽..." : "正在执行单抽...";
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
        string[] cardRoots = { "LotteryLeftPrize", "LotteryCenterPrize", "LotteryRightPrize" };
        string[] nameFields = { "LotteryLeftName", "LotteryCenterName", "LotteryRightName" };
        string[] amountFields = { "LotteryLeftAmount", "LotteryCenterAmount", "LotteryRightAmount" };
        CardDrawCardResponse[] cards = serverCardDrawPool != null && serverCardDrawPool.cards != null
            ? serverCardDrawPool.cards
            : new CardDrawCardResponse[0];
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(cards.Length / 3f));
        serverLotteryDisplayPage = Mathf.Clamp(serverLotteryDisplayPage, 0, pageCount - 1);

        for (int i = 0; i < 3; i++)
        {
            int index = serverLotteryDisplayPage * 3 + i;
            CardDrawCardResponse card = index < cards.Length ? cards[index] : null;
            SetSceneText(root, nameFields[i], card == null ? "-" : card.rewardName);
            SetSceneText(root, amountFields[i], card == null ? string.Empty : $"x{card.rewardAmount}  {card.probability:P1}");
            Transform cardRoot = FindSceneTransform(root, cardRoots[i]);
            if (cardRoot != null)
            {
                cardRoot.localScale = isRolling && i == 1 && lotterySceneFlipPhase++ % 2 == 0
                    ? new Vector3(0.72f, 1f, 1f)
                    : Vector3.one;
            }
        }

        CardDrawOptionResponse single = FindCardDrawOption("SINGLE");
        CardDrawOptionResponse five = FindCardDrawOption("FIVE");
        int diamond = PlayerPrefs.GetInt("Crystal", 0);
        SetSceneText(root, "LotteryChanceText", serverCardDrawPool == null
            ? "正在同步奖池"
            : $"钻石 {diamond} · 奖项 {cards.Length} 个");
        SetSceneText(root, "LotteryStatus", string.IsNullOrEmpty(status) ? serverLotteryStatus : status);

        Button closeButton = FindSceneComponent<Button>(root, "LotteryCloseButton");
        if (closeButton != null)
        {
            closeButton.interactable = !serverCardDrawRunning;
        }
        BindSceneButton(closeButton, serverCardDrawRunning ? null : (Action)HideLotteryPanel);

        Button startButton = FindSceneComponent<Button>(root, "LotteryStartButton");
        if (startButton != null)
        {
            startButton.interactable = single != null && !serverCardDrawRunning && !serverCardDrawPoolRunning;
        }
        SetSceneButtonText(startButton, serverCardDrawRunning ? "抽取中..." : FormatDrawOption(single, "单抽"));
        BindSceneButton(startButton, startButton != null && startButton.interactable ? (Action)(() => BeginServerCardDraw("SINGLE")) : null);

        Button fiveButton = FindSceneComponent<Button>(root, "LotteryRuleButton");
        if (fiveButton != null)
        {
            fiveButton.interactable = five != null && !serverCardDrawRunning && !serverCardDrawPoolRunning;
        }
        SetSceneButtonText(fiveButton, FormatDrawOption(five, "五连"));
        BindSceneButton(fiveButton, fiveButton != null && fiveButton.interactable ? (Action)(() => BeginServerCardDraw("FIVE")) : null);

        Button poolButton = FindSceneComponent<Button>(root, "LotteryPrizeButton");
        if (poolButton != null)
        {
            poolButton.interactable = cards.Length > 3 && !serverCardDrawRunning;
        }
        SetSceneButtonText(poolButton, cards.Length > 3 ? $"奖池 {serverLotteryDisplayPage + 1}/{pageCount}" : "奖池");
        BindSceneButton(poolButton, poolButton != null && poolButton.interactable ? (Action)ShowNextLotteryCardPage : null);
    }

    private static string FormatDrawOption(CardDrawOptionResponse option, string fallback)
    {
        if (option == null)
        {
            return fallback + "未开放";
        }

        return $"{fallback} {option.costAmount}{option.costItemName}";
    }

    private void ShowNextLotteryCardPage()
    {
        int count = serverCardDrawPool != null && serverCardDrawPool.cards != null ? serverCardDrawPool.cards.Length : 0;
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(count / 3f));
        serverLotteryDisplayPage = (serverLotteryDisplayPage + 1) % pageCount;
        ShowServerLotteryState();
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
                announcementPanelView.SetAnnouncement("游戏公告", "当前没有有效公告", string.Empty, "暂无公告内容");
                announcementPanelView.BindClose(HideAnnouncementPanel);
                SetStatus("当前没有有效公告");
                yield break;
            }

            AnnouncementResponse first = announcements[0];
            announcementPanelView.SetAnnouncement(
                first.title,
                $"共 {announcements.Length} 条 · 未读 {response.data.unreadCount}",
                string.IsNullOrEmpty(first.startTime) ? string.Empty : "发布时间 " + first.startTime,
                BuildServerAnnouncementBody(announcements));
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

    private string BuildServerAnnouncementBody(AnnouncementResponse[] announcements)
    {
        List<string> blocks = new List<string>();
        for (int i = 0; i < announcements.Length; i++)
        {
            AnnouncementResponse item = announcements[i];
            if (item == null)
            {
                continue;
            }

            string pinned = item.pinned ? "[置顶] " : string.Empty;
            string time = string.IsNullOrEmpty(item.startTime) ? string.Empty : "\n" + item.startTime;
            blocks.Add($"{pinned}{item.title}{time}\n\n{item.content}");
        }

        return string.Join("\n\n------------------------------\n\n", blocks.ToArray());
    }

    private void ShowAnnouncementFailure(string error)
    {
        if (announcementPanelView != null)
        {
            announcementPanelView.SetAnnouncement("游戏公告", "同步失败", string.Empty, error);
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
        public int targetSceneId;
        public string targetSceneKey;
        public string targetSceneName;
        public long targetItemId;
        public string targetItemKey;
        public string targetItemName;
        public string targetItemIcon;
        public string targetItemType;
        public int targetAmount;
        public int progressAmount;
        public int experienceReward;
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
        public int quantity;
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
        public int grantedQuantity;
        public bool duplicateNpcConverted;
        public long playerNpcInstanceId;
    }

    [Serializable]
    private class ExperienceGrantResponse
    {
        public int playerId;
        public string sourceType;
        public string sourceId;
        public int requestedExperience;
        public int grantedExperience;
        public long beforeExperience;
        public long afterExperience;
        public int beforeLevel;
        public int afterLevel;
        public bool leveledUp;
        public bool maxLevel;
        public int currentLevelExperience;
        public int requiredExperienceForNextLevel;
        public int experienceProgressBps;
        public int curveRevision;
    }

    [Serializable]
    private class SignInOverviewApiResponse : BasicApiResponse
    {
        public SignInOverviewData data;
    }

    [Serializable]
    private class SignInOverviewData
    {
        public string businessTime;
        public SignInPlanResponse[] plans;
    }

    [Serializable]
    private class SignInPlanResponse
    {
        public string planCode;
        public string planName;
        public string planType;
        public string periodKey;
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
        public SignInRewardPreview[] rewards;
    }

    [Serializable]
    private class SignInRewardPreview
    {
        public int rewardSeq;
        public string rewardType;
        public string rewardKey;
        public long rewardAssetId;
        public string rewardName;
        public string rewardIcon;
        public string rewardCategory;
        public int rewardAmount;
        public string fallbackCondition;
        public string fallbackRewardType;
        public string fallbackRewardKey;
        public long fallbackRewardAssetId;
        public string fallbackRewardName;
        public string fallbackRewardIcon;
        public string fallbackRewardCategory;
        public int fallbackRewardAmount;
    }

    [Serializable]
    private class SignInCostResponse
    {
        public long itemId;
        public string itemKey;
        public string itemName;
        public string icon;
        public string itemCategory;
        public int amount;
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
