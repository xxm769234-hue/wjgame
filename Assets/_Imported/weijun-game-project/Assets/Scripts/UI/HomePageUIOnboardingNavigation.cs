using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class HomePageUI
{
    private string onboardingNavigationSignature;
    private bool onboardingRoleGrowthOpened;
    private bool onboardingSignPlanSelected;
    private bool onboardingTaskCategorySelected;
    private bool onboardingWaitingForReward;
    private RectTransform starterSelectionPanel;

    public RectTransform ResolveOnboardingRewardConfirmation()
    {
        return FindActiveOnboardingChild(GetRegisteredScenePanel(HomePageScenePanelType.Lottery), "LotteryRewardConfirmPanel");
    }

    public RectTransform ResolveOnboardingNavigationTarget(string stepJson, bool activate)
    {
        if (!HasAuthSession() || !isActiveAndEnabled) return null;
        OnboardingNavigationStep step = ParseJson<OnboardingNavigationStep>(stepJson);
        if (step != null && step.actionKey != "OPEN_GENDER_SELECTION") HideStarterSelection();
        if (step == null || !TryGetOnboardingPanel(step.actionKey, out HomePageScenePanelType panelType))
            return step != null && step.actionKey == "OPEN_GENDER_SELECTION" ? ResolveStarterSelectionTarget(activate) : null;
        string signature = GameSession.PlayerId + ":" + step.stepCode + ":" + step.actionKey;
        if (onboardingNavigationSignature != signature)
        {
            onboardingNavigationSignature = signature;
            onboardingRoleGrowthOpened = false;
            onboardingSignPlanSelected = false;
            onboardingTaskCategorySelected = false;
        }
        RectTransform rewardDialog = FindActiveOnboardingChild(GetRegisteredScenePanel(HomePageScenePanelType.Lottery), "LotteryRewardConfirmPanel");
        if (rewardDialog != null)
        {
            onboardingWaitingForReward = true;
            return rewardDialog;
        }
        if (onboardingWaitingForReward)
        {
            activate = true;
            onboardingWaitingForReward = false;
        }
        HomePageScenePanel panel = GetRegisteredScenePanel(panelType);
        if (step.actionKey == "OPEN_SIGN_IN" && signPanelObject != null && signPanelObject.activeInHierarchy)
            panel = signPanelObject.GetComponent<HomePageScenePanel>();
        if (activate && (panel == null || !panel.gameObject.activeInHierarchy))
        {
            switch (step.actionKey)
            {
                case "OPEN_CARD_DRAW": OpenServerLottery(); break;
                case "OPEN_NPC_UPGRADE": ShowRolePanel(); break;
                case "OPEN_EQUIPMENT": ShowEquipmentPanel(); break;
                case "OPEN_TASKS": OpenServerTasks("daily"); break;
                case "OPEN_SIGN_IN": OpenServerSignIn("MONTHLY"); break;
                case "OPEN_SHOP": ShowShopContent(); break;
                case "OPEN_MAIL": ShowMailContent(); break;
                case "OPEN_FRIENDS": ShowFriendsPanel(); break;
            }
            panel = GetRegisteredScenePanel(panelType);
        }
        if (panel == null || !panel.gameObject.activeInHierarchy) return null;
        RectTransform target = panel.PanelRoot;
        switch (step.actionKey)
        {
            case "OPEN_CARD_DRAW":
                RectTransform poolDialog = FindActiveOnboardingChild(panel, "LotteryPoolPanel");
                string drawButton = !string.IsNullOrEmpty(pendingCardDrawKey) && pendingCardDrawOption != "SINGLE"
                    ? "LotteryTenButton" : "LotterySingleButton";
                target = poolDialog != null ? poolDialog : FindActiveOnboardingChild(panel, drawButton);
                break;
            case "OPEN_NPC_UPGRADE":
                RoleDisplayEntry role = FindRoleDisplayEntry(roleSelectedNpcId);
                if (!rolePanelLoading && role != null && role.owned && role.player != null && !onboardingRoleGrowthOpened)
                {
                    OpenRoleActionPanel(panel.PanelRoot, true);
                    onboardingRoleGrowthOpened = true;
                }
                RectTransform growthPanel = FindActiveOnboardingChild(panel, "RoleGrowthPanel");
                if (growthPanel == null)
                {
                    RectTransform growthOverlay = FindActiveOnboardingChild(panel, "RoleGrowthOverlay");
                    Button upgrade = FindSceneComponent<Button>(panel.PanelRoot, "RoleDetailUpgradeButton");
                    if (growthOverlay != null && upgrade != null && upgrade.gameObject.activeInHierarchy)
                        growthPanel = upgrade.transform.parent as RectTransform;
                }
                if (growthPanel != null) target = growthPanel;
                break;
            case "OPEN_TASKS":
                if (!serverTaskListRunning && !onboardingTaskCategorySelected)
                {
                    onboardingTaskCategorySelected = true;
                    foreach (ServerTaskResponse task in currentServerTasks)
                    {
                        if (task == null || !task.canClaim) continue;
                        string category = GetServerTaskDisplayCategory(task);
                        if (category != activeTaskCategory) ShowTaskPanel(category, "领取已完成的任务奖励");
                        break;
                    }
                }
                break;
            case "OPEN_SIGN_IN":
                if (serverSignOverviewReady && serverSignInOverview != null && !onboardingSignPlanSelected)
                {
                    onboardingSignPlanSelected = true;
                    SignInPlanResponse plan = SelectOnboardingSignPlan(serverSignInOverview.plans);
                    if (plan != null)
                    {
                        serverSignPlanCode = plan.planCode;
                        ShowCurrentServerSignPanel();
                        if (signPanelObject != null) panel = signPanelObject.GetComponent<HomePageScenePanel>();
                        target = panel != null ? panel.PanelRoot : null;
                    }
                }
                break;
        }
        if (target != null && target.gameObject.activeInHierarchy)
            RegisterOnboardingTarget(step.uiTargetKey, target);
        return target;
    }

    public bool IsOnboardingNavigationReady(string stepJson)
    {
        OnboardingNavigationStep step = ParseJson<OnboardingNavigationStep>(stepJson);
        if (step == null || !HasAuthSession()) return false;
        if (step.actionKey == "OPEN_GENDER_SELECTION") return false;
        if (!TryGetOnboardingPanel(step.actionKey, out HomePageScenePanelType panelType)) return false;
        HomePageScenePanel panel = GetRegisteredScenePanel(panelType);
        if (step.actionKey == "OPEN_SIGN_IN" && signPanelObject != null)
            panel = signPanelObject.GetComponent<HomePageScenePanel>();
        if (panel == null || !panel.gameObject.activeInHierarchy) return false;
        switch (step.actionKey)
        {
            case "OPEN_CARD_DRAW": return serverCardDrawPool != null && !serverCardDrawPoolRunning && FindCardDrawOption("SINGLE") != null;
            case "OPEN_NPC_UPGRADE": return !rolePanelLoading && rolePlayerEntries.Length > 0;
            case "OPEN_EQUIPMENT": return !equipmentRefreshRunning;
            case "OPEN_TASKS": return !serverTaskListRunning;
            case "OPEN_SIGN_IN": return serverSignOverviewReady;
            case "OPEN_SHOP": return coinShopCatalog != null && !coinShopCatalogLoading;
            case "OPEN_FRIENDS": return !v37Loading && !v37LastLoadFailed;
            case "OPEN_MAIL": return currentServerMails != null && !mailMutationRunning;
            default: return false;
        }
    }

    private static bool TryGetOnboardingPanel(string actionKey, out HomePageScenePanelType panelType)
    {
        switch (actionKey)
        {
            case "OPEN_CARD_DRAW": panelType = HomePageScenePanelType.Lottery; return true;
            case "OPEN_NPC_UPGRADE": panelType = HomePageScenePanelType.Role; return true;
            case "OPEN_EQUIPMENT": panelType = HomePageScenePanelType.Equipment; return true;
            case "OPEN_TASKS": panelType = HomePageScenePanelType.Task; return true;
            case "OPEN_SIGN_IN": panelType = HomePageScenePanelType.DailySign; return true;
            case "OPEN_SHOP": panelType = HomePageScenePanelType.Shop; return true;
            case "OPEN_MAIL": panelType = HomePageScenePanelType.Mail; return true;
            case "OPEN_FRIENDS": panelType = HomePageScenePanelType.Friends; return true;
            default: panelType = HomePageScenePanelType.Content; return false;
        }
    }

    private static SignInPlanResponse SelectOnboardingSignPlan(SignInPlanResponse[] plans)
    {
        SignInPlanResponse fallback = null;
        foreach (SignInPlanResponse plan in plans ?? new SignInPlanResponse[0])
        {
            if (plan == null || !IsSignInPlanVersionSupported(plan) || plan.days == null || plan.days.Length == 0) continue;
            if (fallback == null) fallback = plan;
            if (plan.canClaim || Array.Exists(plan.days, day => day != null && day.canClaim)) return plan;
        }
        return fallback;
    }

    private static RectTransform FindActiveOnboardingChild(HomePageScenePanel panel, string objectName)
    {
        Transform target = panel != null && panel.gameObject.activeInHierarchy ? panel.FindTransform(objectName) : null;
        return target != null && target.gameObject.activeInHierarchy ? target as RectTransform : null;
    }

    [Serializable]
    private sealed class OnboardingNavigationStep
    {
        public string stepCode;
        public string actionKey;
        public string uiTargetKey;
    }
}
