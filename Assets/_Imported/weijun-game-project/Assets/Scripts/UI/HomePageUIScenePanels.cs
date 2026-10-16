using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TMP_Text;

public partial class HomePageUI
{
    private int lotterySceneFlipPhase;
    private readonly HashSet<string> _missingMailRewardIcons = new HashSet<string>(StringComparer.Ordinal);

    private const int TaskVisibleRows = 4;
    [Header("Task list layout")]
    [Tooltip("Spacing between runtime task rows. TaskRow0 controls the row size and child layout.")]
    [SerializeField] private float taskRowSpacing = 17.182f;
    private RectTransform _taskRowsContent;
    private RectTransform _taskRowTemplate;
    private readonly List<RectTransform> _taskRows = new List<RectTransform>();
    private float _taskRowsMinimumHeight;

    private HomePageScenePanel GetRegisteredScenePanel(HomePageScenePanelType type)
    {
        if (scenePanels == null)
        {
            scenePanels = FindObjectOfType<HomePageScenePanels>(true);
        }

        HomePageScenePanel panel = scenePanels != null ? scenePanels.Get(type) : null;
        if (panel == null && type == HomePageScenePanelType.Recharge)
        {
            HomePageScenePanel[] candidates = FindObjectsOfType<HomePageScenePanel>(true);
            for (int panelIndex = 0; panelIndex < candidates.Length; panelIndex++)
            {
                HomePageScenePanel candidate = candidates[panelIndex];
                if (candidate.PanelType == type && candidate.gameObject.scene == gameObject.scene)
                {
                    return candidate;
                }
            }
        }

        return panel;
    }

    private HomePageScenePanel OpenRegisteredScenePanel(HomePageScenePanelType type)
    {
        return OpenRegisteredScenePanel(type, true);
    }

    private HomePageScenePanel OpenRegisteredScenePanel(HomePageScenePanelType type, bool bringToFront)
    {
        HomePageScenePanel panel = GetRegisteredScenePanel(type);
        if (panel == null)
        {
            return null;
        }

        if (type != HomePageScenePanelType.Role)
        {
            HomePageScenePanel rolePanel = GetRegisteredScenePanel(HomePageScenePanelType.Role);
            if (rolePanel != null)
            {
                rolePanel.SetVisible(false);
            }

            rolePanelObject = null;
        }

        panel.SetVisible(true);
        if (bringToFront)
        {
            panel.transform.SetAsLastSibling();
        }
        return panel;
    }

    private bool HideRegisteredScenePanel(HomePageScenePanelType type, ref GameObject trackedObject)
    {
        HomePageScenePanel panel = GetRegisteredScenePanel(type);
        if (panel == null)
        {
            return false;
        }

        panel.SetVisible(false);
        if (trackedObject != panel.gameObject)
        {
            return false;
        }

        trackedObject = null;
        return true;
    }

    private bool TryShowTaskScenePanel(string category, string message, TaskDisplayItem[] tasks, int completedCount)
    {
        tasks = tasks ?? new TaskDisplayItem[0];
        HomePageScenePanel panel = OpenRegisteredScenePanel(HomePageScenePanelType.Task);
        if (panel == null)
        {
            return false;
        }

        taskPanelObject = panel.gameObject;
        Transform root = panel.PanelRoot;
        SetSceneText(root, "TaskTitle", "任务");
        SetSceneText(root, "TaskTotalProgress", $"本页进度  {completedCount}/{tasks.Length}");
        SetSceneText(root, "TaskFooterText", tasks.Length == 0 ? "\u6682\u65e0\u4efb\u52a1" : message);
        if (tasks.Length == 0) SetSceneText(root, "TaskFooterText", string.Empty);
        SetTaskEmptyMessage(root, tasks.Length == 0);
        BindSceneButton(FindSceneComponent<Button>(root, "TaskCloseButton"), HideTaskPanel);

        string[] categoryIds = { "daily", "main", "growth", "activity" };
        string[] categoryLabels = HasAuthSession()
            ? new[] { "每日任务", "每周任务", "成长任务", "活动任务" }
            : new[] { "每日任务", "主线任务", "成长任务", "活动任务" };
        for (int i = 0; i < categoryIds.Length; i++)
        {
            string categoryId = categoryIds[i];
            string categoryLabel = categoryLabels[i];
            Button button = FindSceneComponent<Button>(root, "TaskCategory_" + categoryId);
            if (button == null)
            {
                continue;
            }

            bool selected = categoryId == category;
            bool supported = true;
            Image background = button.GetComponent<Image>();
            if (background != null)
            {
                background.color = Color.white;
            }

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = categoryLabel;
                label.color = selected ? Color.white : new Color(0.38f, 0.18f, 0.08f, 1f);
            }

            Transform marker = FindSceneTransform(button.transform, "SelectedMarker");
            if (marker != null)
            {
                marker.gameObject.SetActive(false);
            }

            Transform selectedImage = FindSceneTransform(button.transform, "TaskCategorySelectedImage");
            if (selectedImage != null)
            {
                selectedImage.gameObject.SetActive(selected);
            }

            button.interactable = supported;
            BindSceneButton(button, supported ? (Action)(() => ShowTaskPanel(categoryId, $"已切换到{categoryLabel}")) : null);
        }

        RenderTaskSceneRows(root, tasks);

        SetStatus(message);
        return true;
    }

    private void RenderTaskSceneRows(Transform root, TaskDisplayItem[] tasks)
    {
        RectTransform content = FindSceneTransform(root, "TaskRowsContent") as RectTransform;
        if (content == null)
        {
            Debug.LogWarning("TaskRowsContent is missing; task rows cannot be generated.", this);
            return;
        }

        if (_taskRowsContent != content || _taskRowTemplate == null)
        {
            _taskRowsContent = content;
            _taskRows.Clear();
            _taskRowTemplate = content.Find("TaskRow0") as RectTransform;
            if (_taskRowTemplate == null)
            {
                Debug.LogWarning("TaskRow0 is missing; task rows cannot be generated.", this);
                return;
            }

            ScrollRect scroll = FindSceneComponent<ScrollRect>(root, "TaskRowsViewport");
            _taskRowsMinimumHeight = scroll != null && scroll.viewport != null
                ? scroll.viewport.rect.height : content.rect.height;
        }

        _taskRowTemplate.gameObject.SetActive(false);
        HideTaskRowsFrom(tasks.Length);
        while (_taskRows.Count < tasks.Length)
        {
            RectTransform row = Instantiate(_taskRowTemplate, content, false);
            row.name = "TaskRowRuntime" + (_taskRows.Count + 1);
            _taskRows.Add(row);
        }

        float rowStride = Mathf.Max(1f, _taskRowTemplate.rect.height + taskRowSpacing);
        float requiredHeight = tasks.Length == 0
            ? 0f
            : _taskRowTemplate.rect.height + Mathf.Max(0, tasks.Length - 1) * rowStride;
        content.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            Mathf.Max(_taskRowsMinimumHeight, requiredHeight));

        for (int index = 0; index < _taskRows.Count; index++)
        {
            RectTransform row = _taskRows[index];
            if (row == null) continue;

            bool visible = index < tasks.Length;
            row.gameObject.SetActive(visible);
            if (!visible) continue;

            row.anchoredPosition = _taskRowTemplate.anchoredPosition
                - new Vector2(0f, index * rowStride);
            ConfigureTaskSceneRow(row, tasks[index], index);
        }

        ScrollRect taskScroll = FindSceneComponent<ScrollRect>(root, "TaskRowsViewport");
        if (taskScroll != null)
        {
            taskScroll.StopMovement();
            taskScroll.verticalNormalizedPosition = 1f;
        }
    }

    private void HideTaskRowsFrom(int firstHiddenIndex)
    {
        for (int index = firstHiddenIndex; index < _taskRows.Count; index++)
        {
            RectTransform row = _taskRows[index];
            if (row != null) row.gameObject.SetActive(false);
        }
    }

    private void SetTaskEmptyMessage(Transform root, bool visible)
    {
        Transform empty = FindSceneTransform(root, "TaskEmpty");
        if (empty == null)
        {
            Debug.LogWarning("TaskEmpty is missing; add a TMP node named TaskEmpty to position the empty state.", this);
            return;
        }

        empty.gameObject.SetActive(visible);
        if (visible) SetSceneText(root, "TaskEmpty", "暂无任务");
    }

    private void ConfigureTaskSceneRow(Transform row, TaskDisplayItem task, int index)
    {
        bool claimed = IsTaskClaimed(task);
        SetSceneText(row, "TaskItemTitle", task.title);
        SetSceneText(row, "TaskItemDescription", task.description);
        SetSceneText(row, "TaskProgressTitle", task.IsComplete ? "任务完成" : "任务进度");
        SetSceneText(row, "TaskProgressValue", $"{Mathf.Min(task.current, task.target)}/{task.target}");
        SetSceneText(
            row,
            "TaskRewardText",
            string.IsNullOrEmpty(task.rewardSummary)
                ? $"{task.rewardName} x{task.rewardAmount}"
                : task.rewardSummary);

        Image progressFill = FindSceneComponent<Image>(row, "TaskProgressFill");
        if (progressFill != null)
        {
            float ratio = Mathf.Clamp01(task.current / (float)Mathf.Max(1, task.target));
            progressFill.fillAmount = ratio;
        }

        SetSceneText(row, "TaskRewardIconText", string.IsNullOrEmpty(task.rewardName) ? "奖" : task.rewardName.Substring(0, 1));

        Button actionButton = FindSceneComponent<Button>(row, "TaskActionButton");
        if (actionButton == null)
        {
            return;
        }

        string buttonLabel = claimed ? "已领取" : task.IsComplete ? "领取" : "未完成";
        SetSceneButtonText(actionButton, buttonLabel);
        actionButton.interactable = !claimed && task.IsComplete;
        if (claimed)
        {
            BindSceneButton(actionButton, null);
        }
        else if (task.IsComplete)
        {
            BindSceneButton(actionButton, () => ClaimTaskReward(task));
        }
        else
        {
            BindSceneButton(actionButton, null);
        }
    }

    private static Color GetTaskSceneRewardColor(string rewardKey)
    {
        if (rewardKey == "Wood") return new Color(0.30f, 0.64f, 0.28f, 1f);
        if (rewardKey == "Stone") return new Color(0.55f, 0.58f, 0.62f, 1f);
        if (rewardKey == "Crystal" || rewardKey == "SoulCrystal") return new Color(0.28f, 0.67f, 0.95f, 1f);
        if (rewardKey == "EquipmentChest") return new Color(0.72f, 0.38f, 0.14f, 1f);
        if (rewardKey == "LotteryTicket") return new Color(0.84f, 0.24f, 0.20f, 1f);
        return new Color(1f, 0.71f, 0.12f, 1f);
    }

    private bool TryShowInventoryScenePanel(
        List<InventoryItemResponse> visibleItems,
        int selectedIndex,
        int ownedCount,
        int unlockedSlots)
    {
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Inventory);
        if (panel == null)
        {
            return false;
        }

        InventoryPanelView view = panel.GetComponentInChildren<InventoryPanelView>(true);
        if (view == null)
        {
            return false;
        }

        EnsureInventoryInputBlocker(panel.transform.parent != null ? panel.transform.parent : panel.transform);
        panel.SetVisible(true);
        view.gameObject.SetActive(true);
        panel.transform.SetAsLastSibling();
        inventoryPanelObject = panel.gameObject;
        inventoryPanelView = view;
        ConfigureInventoryPrefab(view, visibleItems, selectedIndex, ownedCount, unlockedSlots);
        return true;
    }

    private bool TryShowRechargeScenePanel(PaymentProductResponse[] products, string status, bool disabled)
    {
        HomePageScenePanel panel = OpenRegisteredScenePanel(HomePageScenePanelType.Recharge, false);
        if (panel == null)
        {
            return false;
        }

        rechargePanelObject = panel.gameObject;
        Transform root = panel.PanelRoot;
        BindSceneButton(FindSceneComponent<Button>(root, "RechargeCloseButton"), HideRechargePanel);
        BindSceneButton(
            FindSceneComponent<Button>(root, "RechargeGuideButton"),
            ShowRefundCenter);
        SetSceneButtonText(
            FindSceneComponent<Button>(root, "RechargeGuideButton"),
            HasAuthSession() ? "退款/进度" : "账号功能");

        SetSceneText(root, "RechargeTitle", "充值");
        SetSceneText(root, "RechargeGoldText", $"金币  {PlayerPrefs.GetInt("Gold", 1000)}");
        SetSceneText(root, "RechargeCrystalText", $"钻石  {PlayerPrefs.GetInt("Crystal", 0)}");
        SetSceneText(root, "RechargeStatus", status);

        if (!rechargeSceneAuthoredStateCaptured)
        {
            rechargeSceneAuthoredActiveStates.Clear();
            for (int i = 0; i < 8; i++)
            {
                Transform authoredCard = FindSceneTransform(root, "RechargeProductCard" + i);
                if (authoredCard != null)
                {
                    rechargeSceneAuthoredActiveStates[authoredCard.name] = authoredCard.gameObject.activeSelf;
                }
            }

            rechargeSceneAuthoredStateCaptured = true;
        }

        int count = Mathf.Min(products != null ? products.Length : 0, 8);
        for (int i = 0; i < 8; i++)
        {
            Transform card = FindSceneTransform(root, "RechargeProductCard" + i);
            if (card == null)
            {
                continue;
            }

            bool authoredActive = !rechargeSceneAuthoredActiveStates.TryGetValue(card.name, out bool capturedActive)
                || capturedActive;
            card.gameObject.SetActive(authoredActive);
            if (!authoredActive || i >= count)
            {
                Button unavailablePriceButton = FindSceneComponent<Button>(card, "RechargePriceButton");
                if (unavailablePriceButton != null)
                {
                    unavailablePriceButton.interactable = false;
                    BindSceneButton(unavailablePriceButton, null);
                }
                continue;
            }

            PaymentProductResponse product = products[i];
            string productName = GetRechargeProductName(product);
            string rewardName = GetRechargeRewardName(product);
            int apiRewardAmount = GetShopRewardAmount(product);
            int rewardQuantity = Mathf.Max(0, apiRewardAmount);
            int bonus = 0;

            SetSceneText(card, "ProductName", productName);
            SetSceneText(card, "HeaderText", $"{rewardName}x{rewardQuantity}");
            SetSceneText(card, "RewardAmount", rewardQuantity.ToString());
            SetSceneText(card, "BonusText", string.IsNullOrWhiteSpace(product.description) ? "后端定价商品" : product.description);

            Button priceButton = FindSceneComponent<Button>(card, "RechargePriceButton");
            SetSceneButtonText(priceButton, FormatRechargePrice(product));
            BindSceneButton(
                priceButton,
                () => HandleRechargeProductSelected(product, productName, rewardName, rewardQuantity, bonus, disabled));
            if (priceButton != null)
            {
                priceButton.interactable = !disabled;
            }
        }

        SetStatus(status);
        return true;
    }

    private bool TryShowLotteryScenePanel(string status, string rewardName, int rewardValue, bool hasResult, bool isRolling)
    {
        HomePageScenePanel panel = OpenRegisteredScenePanel(HomePageScenePanelType.Lottery);
        if (panel == null)
        {
            return false;
        }

        lotteryPanelObject = panel.gameObject;
        Transform root = panel.PanelRoot;
        if (HasAuthSession())
        {
            ConfigureServerLotteryScenePanel(root, status, rewardName, rewardValue, hasResult, isRolling);
            return true;
        }

        CardDrawCardResponse[] localCards =
        {
            new CardDrawCardResponse { slotNo = 1, rewardType = "ITEM", rewardName = "金币", rewardAmount = 80, probability = 0.35f },
            new CardDrawCardResponse { slotNo = 2, rewardType = "ITEM", rewardName = "木材", rewardAmount = 150, probability = 0.25f },
            new CardDrawCardResponse { slotNo = 3, rewardType = "ITEM", rewardName = "石材", rewardAmount = 150, probability = 0.25f },
            new CardDrawCardResponse { slotNo = 4, rewardType = "ITEM", rewardName = "铁矿", rewardAmount = 120, probability = 0.15f }
        };
        ConfigureLotteryPrizeRail(root, localCards);
        SetSceneText(root, "LotteryDiamondText", $"钻石 {PlayerPrefs.GetInt("Crystal", 0)}");
        SetSceneText(root, "LotteryPrizeHint", "游客模式使用本地演示奖品");
        SetSceneText(root, "LotteryStatus", status);

        Button closeButton = FindSceneComponent<Button>(root, "LotteryCloseButton");
        if (closeButton != null)
        {
            closeButton.interactable = !isRolling;
        }
        BindSceneButton(closeButton, isRolling ? null : (Action)HideLotteryPanel);

        Button singleButton = FindSceneComponent<Button>(root, "LotterySingleButton");
        if (singleButton != null)
        {
            singleButton.interactable = !isRolling;
        }
        SetSceneButtonText(singleButton, isRolling ? "抽取中..." : "单抽（本地演示）");
        BindSceneButton(singleButton, isRolling ? null : (Action)PerformLotteryDraw);

        Button multiButton = FindSceneComponent<Button>(root, "LotteryTenButton");
        if (multiButton != null)
        {
            multiButton.interactable = false;
        }
        SetSceneButtonText(multiButton, "登录后连抽");
        BindSceneButton(multiButton, null);

        Button poolButton = FindSceneComponent<Button>(root, "LotteryPoolButton");
        if (poolButton != null)
        {
            poolButton.interactable = false;
        }
        SetSceneButtonText(poolButton, "服务器奖池");
        BindSceneButton(poolButton, null);

        SetStatus(status);
        return true;
    }

    private bool TryShowDailySignScenePanel(int signedDays, bool todaySigned, string status)
    {
        HomePageScenePanel panel = OpenRegisteredScenePanel(HomePageScenePanelType.DailySign);
        if (panel == null)
        {
            return false;
        }

        signPanelObject = panel.gameObject;
        Transform root = panel.PanelRoot;
        BindSceneButton(FindSceneComponent<Button>(root, "DailySignCloseButton"), HideSignPanel);
        if (HasAuthSession())
        {
            ConfigureServerSignScenePanel(root, "MONTHLY", status);
            return true;
        }

        PrepareDailySignSceneCards(root, 0);
        SetSceneText(root, "DailySignStatus", "请登录后查看服务器签到奖励");
        SetStatus(status);
        return true;
    }

    private bool TryShowNewbieSignScenePanel(int signedDays, string status)
    {
        HomePageScenePanel panel = OpenRegisteredScenePanel(HomePageScenePanelType.NewbieSign);
        if (panel == null)
        {
            return false;
        }

        signPanelObject = panel.gameObject;
        Transform root = panel.PanelRoot;
        BindSceneButton(FindSceneComponent<Button>(root, "NewbieSignCloseButton"), HideSignPanel);
        if (HasAuthSession())
        {
            ConfigureServerSignScenePanel(root, "NEWBIE", status);
            return true;
        }

        string[] rewardNames = { "金币", "木材 石材", "装备宝箱" };
        string[] rewardAmounts = { "x300", "x300", "x1" };
        for (int i = 0; i < 3; i++)
        {
            Transform card = FindSceneTransform(root, "NewbieSignCard" + (i + 1));
            bool claimed = i + 1 <= signedDays;
            bool highlighted = i + 1 == signedDays && signedDays <= 3;
            ConfigureSignSceneCard(card, i + 1, rewardNames[i], rewardAmounts[i], claimed, highlighted);
        }

        SetSceneText(root, "NewbieSignStatus", $"进度 {Mathf.Clamp(signedDays, 0, 3)}/3    {status}");
        SetStatus(status);
        return true;
    }

    private void ConfigureSignSceneCard(
        Transform card,
        int day,
        string rewardName,
        string rewardAmount,
        bool claimed,
        bool highlighted)
    {
        if (card == null)
        {
            return;
        }

        SetSceneText(card, "DayText", $"第{day}天");
        SetSceneText(card, "RewardText", string.IsNullOrEmpty(rewardAmount)
            ? rewardName
            : $"{rewardName}\n{rewardAmount}");
        SetSceneText(card, "RewardIconText", string.IsNullOrEmpty(rewardName) ? "奖" : rewardName.Substring(0, 1));
        SetSceneObjectActive(card, "ClaimedCheck", claimed);
        SetSceneObjectActive(card, "ClaimedChip", claimed);
        SetSceneObjectActive(card, "ClaimedText", claimed);
    }

    private bool TryShowSystemMailScenePanel(MailResponse[] mails, string message)
    {
        HomePageScenePanel panel = OpenRegisteredScenePanel(HomePageScenePanelType.Mail);
        if (panel == null)
        {
            return false;
        }

        mailPanelObject = panel.gameObject;
        Transform root = panel.PanelRoot;
        mailShowingNotifications = false;
        SetSceneText(root, "MailTitle", "系统邮件");
        SetSceneText(root, "MailListTitle", "邮件列表");
        SetSceneText(root, "MailEmptyText", "暂无邮件");
        SetSceneText(root, "MailRewardTitle", "奖励");
        SetSceneObjectActive(root, "DeleteMailButton", true);
        SetSceneObjectActive(root, "NotificationPreviousButton", false);
        SetSceneObjectActive(root, "NotificationNextButton", false);
        BindSceneButton(FindSceneComponent<Button>(root, "SystemMailCloseButton"), HideSystemMailPanel);
        Button claimAllButton = FindSceneComponent<Button>(root, "ClaimAllButton");
        Button claimButton = FindSceneComponent<Button>(root, "ClaimMailButton");
        SetSceneButtonText(claimAllButton, "一键领取");
        SetSceneButtonText(claimButton, "领取");
        BindSceneButton(claimAllButton, ClaimAllMailAttachments);
        BindSceneButton(claimButton, ClaimSelectedMailAttachment);
        BindSceneButton(FindSceneComponent<Button>(root, "DeleteMailButton"), DeleteSelectedMail);

        List<MailResponse> validMails = new List<MailResponse>();
        if (mails != null)
        {
            for (int i = 0; i < mails.Length; i++)
            {
                if (mails[i] != null)
                {
                    validMails.Add(mails[i]);
                }
            }
        }

        bool hasClaimableAttachment = false;
        for (int i = 0; i < validMails.Count; i++)
        {
            MailResponse mail = validMails[i];
            if (mail.hasAttachment && !mail.attachmentClaimed && !mail.expired)
            {
                hasClaimableAttachment = true;
                break;
            }
        }
        if (claimAllButton != null)
        {
            claimAllButton.interactable = hasClaimableAttachment && !mailMutationRunning;
        }

        Transform emptyCard = FindSceneTransform(root, "MailEmptyCard");
        if (emptyCard != null)
        {
            emptyCard.gameObject.SetActive(validMails.Count == 0);
        }

        List<Button> mailItemButtons = EnsureSceneMailListRows(root, validMails.Count);
        for (int i = 0; i < mailItemButtons.Count; i++)
        {
            Button itemButton = mailItemButtons[i];
            bool visible = i < validMails.Count;
            itemButton.gameObject.SetActive(visible);
            if (!visible)
            {
                continue;
            }

            MailResponse mail = validMails[i];
            string title = string.IsNullOrEmpty(mail.title) ? "系统邮件" : mail.title;
            string sender = string.IsNullOrEmpty(mail.senderName) ? "系统" : mail.senderName;
            string attachment = mail.hasAttachment ? (mail.attachmentClaimed ? "附件已领" : "可领取") : "无附件";
            SetSceneText(itemButton.transform, "MailItemTitle", title);
            string type = string.IsNullOrEmpty(mail.mailType) ? "SYSTEM" : mail.mailType;
            string expiry = string.IsNullOrEmpty(mail.expireTime) ? string.Empty : $"  Expires: {mail.expireTime}";
            SetSceneText(itemButton.transform, "MailItemMeta", $"{sender}  {type}  {attachment}{expiry}");
            SetSceneMailUnreadMark(itemButton.transform, mail);

            int selectedIndex = i;
            BindSceneButton(itemButton, () => SelectSceneMail(root, validMails, selectedIndex));
        }

        if (validMails.Count == 0)
        {
            RefreshSceneMailRewardSlots(root, null);
            if (claimButton != null)
            {
                claimButton.interactable = false;
            }
            SetSceneButtonText(claimButton, "领取");
            SetSceneText(root, "MailDetailTitle", "系统提示");
            SetSceneText(
                root,
                "MailDetailBody",
                string.IsNullOrEmpty(message)
                    ? "当前没有可展示的系统邮件。\n\n收到活动奖励、补偿奖励或系统通知后，会在这里显示。"
                    : message);
        }
        else
        {
            int selectedIndex = 0;
            for (int i = 0; i < validMails.Count; i++)
            {
                if (validMails[i].mailId == selectedMailId)
                {
                    selectedIndex = i;
                    break;
                }
            }
            SelectSceneMail(root, validMails, selectedIndex);
        }

        SetSceneText(root, "MailStatus", message ?? "已读取系统邮件");
        SetStatus("已打开系统邮件");
        return true;
    }

    private void SelectSceneMail(Transform root, List<MailResponse> mails, int selectedIndex)
    {
        if (mails == null || selectedIndex < 0 || selectedIndex >= mails.Count)
        {
            return;
        }

        Button selectedButton = FindSceneComponent<Button>(root, "MailItem" + selectedIndex);
        if (selectedButton != null && selectedButton.gameObject.activeSelf)
        {
            selectedButton.Select();
        }

        MailResponse mail = mails[selectedIndex];
        SelectMailForActions(mail);
        SetSceneMailUnreadMark(selectedButton == null ? null : selectedButton.transform, mail);
        string title = string.IsNullOrEmpty(mail.title) ? "系统邮件" : mail.title;
        string sender = string.IsNullOrEmpty(mail.senderName) ? "系统" : mail.senderName;
        string content = string.IsNullOrEmpty(mail.content) ? "暂无详细内容，正在读取邮件详情..." : mail.content;
        string attachment = mail.hasAttachment ? (mail.attachmentClaimed ? "附件状态：已领取" : "附件状态：可领取") : "附件状态：无附件";
        string time = string.IsNullOrEmpty(mail.createTime) ? "时间：未知" : $"时间：{mail.createTime}";

        SetSceneText(root, "MailDetailTitle", title);
        SetSceneText(root, "MailDetailBody", $"{content}\n\n发件人：{sender}\n{time}\n{attachment}");
        RefreshSceneMailRewardSlots(root, mail);
        SetSceneText(root, "MailStatus", mailDetailLoadingId == mail.mailId ? "正在读取邮件详情..." : attachment);

        Button claimButton = FindSceneComponent<Button>(root, "ClaimMailButton");
        if (claimButton != null)
        {
            claimButton.interactable = mail.hasAttachment && !mail.attachmentClaimed && !mail.expired && !mailMutationRunning;
        }
        SetSceneButtonText(claimButton, mail.attachmentClaimed ? "已领取" : "领取");
        BindSceneButton(claimButton, ClaimSelectedMailAttachment);

        Button deleteButton = FindSceneComponent<Button>(root, "DeleteMailButton");
        if (deleteButton != null)
        {
            deleteButton.interactable = (!mail.hasAttachment || mail.attachmentClaimed) && !mailMutationRunning;
        }
        BindSceneButton(deleteButton, DeleteSelectedMail);
    }

    private void RefreshSystemMailSceneUnreadMarks()
    {
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Mail);
        if (panel == null || !panel.gameObject.activeInHierarchy)
        {
            return;
        }

        int visibleIndex = 0;
        if (currentServerMails != null)
        {
            for (int i = 0; i < currentServerMails.Length; i++)
            {
                MailResponse mail = currentServerMails[i];
                if (mail == null)
                {
                    continue;
                }

                Button itemButton = FindSceneComponent<Button>(panel.PanelRoot, "MailItem" + visibleIndex);
                SetSceneMailUnreadMark(itemButton == null ? null : itemButton.transform, mail);
                visibleIndex++;
            }
        }
    }

    private static void SetSceneMailUnreadMark(Transform itemRoot, MailResponse mail)
    {
        Transform unreadMark = FindSceneTransform(itemRoot, "UnreadMark");
        if (unreadMark == null)
        {
            return;
        }

        bool hasUnclaimedAttachment = mail != null && mail.hasAttachment && !mail.attachmentClaimed;
        unreadMark.gameObject.SetActive(mail != null && (!mail.readFlag || hasUnclaimedAttachment));
    }

    private static List<Button> EnsureSceneMailListRows(Transform root, int requiredCount)
    {
        List<Button> rows = new List<Button>();
        Transform viewportTransform = FindSceneTransform(root, "MailItemP");
        if (viewportTransform == null)
        {
            viewportTransform = FindSceneTransform(root, "MailListArea");
        }

        RectTransform viewport = viewportTransform as RectTransform;
        if (viewport == null)
        {
            return rows;
        }

        RectTransform content = EnsureSceneMailListContent(viewport);
        if (content == null)
        {
            return rows;
        }

        Button[] existingButtons = viewport.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < existingButtons.Length; i++)
        {
            Button button = existingButtons[i];
            if (button == null || !TryGetSceneMailItemIndex(button.name, out _))
            {
                continue;
            }

            if (button.transform.parent != content)
            {
                RectTransform buttonRect = button.transform as RectTransform;
                Vector2 anchoredPosition = buttonRect == null ? Vector2.zero : buttonRect.anchoredPosition;
                Vector2 sizeDelta = buttonRect == null ? Vector2.zero : buttonRect.sizeDelta;
                Vector2 anchorMin = buttonRect == null ? Vector2.zero : buttonRect.anchorMin;
                Vector2 anchorMax = buttonRect == null ? Vector2.zero : buttonRect.anchorMax;
                Vector2 pivot = buttonRect == null ? Vector2.zero : buttonRect.pivot;
                button.transform.SetParent(content, false);
                if (buttonRect != null)
                {
                    buttonRect.anchorMin = anchorMin;
                    buttonRect.anchorMax = anchorMax;
                    buttonRect.pivot = pivot;
                    buttonRect.sizeDelta = sizeDelta;
                    buttonRect.anchoredPosition = anchoredPosition;
                }
            }
            rows.Add(button);
        }

        rows.Sort(CompareSceneMailItemIndex);
        if (rows.Count == 0)
        {
            ConfigureSceneMailListScroll(viewport, content, rows, requiredCount);
            return rows;
        }

        Button template = FindSceneMailItemButton(rows, 0) ?? rows[0];
        RectTransform templateRect = template.transform as RectTransform;
        float rowStep = GetSceneMailItemStep(rows);
        for (int index = 0; index < requiredCount; index++)
        {
            if (FindSceneMailItemButton(rows, index) != null)
            {
                continue;
            }

            GameObject clone = Instantiate(template.gameObject, content, false);
            clone.name = "MailItem" + index;
            RectTransform cloneRect = clone.transform as RectTransform;
            if (cloneRect != null && templateRect != null)
            {
                cloneRect.anchoredPosition = new Vector2(
                    templateRect.anchoredPosition.x,
                    templateRect.anchoredPosition.y - rowStep * index);
            }
            rows.Add(clone.GetComponent<Button>());
        }

        rows.RemoveAll(button => button == null);
        rows.Sort(CompareSceneMailItemIndex);
        ConfigureSceneMailListScroll(viewport, content, rows, requiredCount);
        return rows;
    }

    private static RectTransform EnsureSceneMailListContent(RectTransform viewport)
    {
        Transform existing = viewport.Find("MailListContent");
        RectTransform content = existing as RectTransform;
        bool created = content == null;
        if (content == null)
        {
            GameObject contentObject = new GameObject("MailListContent", typeof(RectTransform));
            content = contentObject.GetComponent<RectTransform>();
            content.SetParent(viewport, false);
        }

        if (created)
        {
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.localRotation = Quaternion.identity;
            content.localScale = Vector3.one;
            content.sizeDelta = new Vector2(
                Mathf.Max(1f, viewport.rect.width),
                Mathf.Max(1f, viewport.rect.height));
        }

        if (viewport.GetComponent<RectMask2D>() == null)
        {
            viewport.gameObject.AddComponent<RectMask2D>();
        }

        return content;
    }

    private static void ConfigureSceneMailListScroll(
        RectTransform viewport,
        RectTransform content,
        List<Button> rows,
        int visibleCount)
    {
        ScrollRect scroll = viewport.GetComponent<ScrollRect>();
        bool wasScrollable = scroll != null && scroll.vertical;
        float previousNormalizedPosition = scroll == null ? 1f : scroll.verticalNormalizedPosition;
        if (scroll == null)
        {
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
        }

        float contentHeight = Mathf.Max(1f, viewport.rect.height);
        for (int i = 0; i < visibleCount && i < rows.Count; i++)
        {
            RectTransform rowRect = rows[i].transform as RectTransform;
            if (rowRect == null)
            {
                continue;
            }

            float rowBottom = -rowRect.anchoredPosition.y + rowRect.rect.height * rowRect.pivot.y;
            contentHeight = Mathf.Max(contentHeight, rowBottom + 16f);
        }

        content.sizeDelta = new Vector2(Mathf.Max(1f, viewport.rect.width), contentHeight);
        bool needsScroll = visibleCount > 5;
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = needsScroll;
        scroll.inertia = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 32f;
        scroll.verticalNormalizedPosition = wasScrollable && needsScroll
            ? Mathf.Clamp01(previousNormalizedPosition)
            : 1f;

        Scrollbar scrollbar = FindSceneComponent<Scrollbar>(viewport, "MailListScrollbar");
        if (scrollbar != null)
        {
            scroll.verticalScrollbar = scrollbar;
            scrollbar.gameObject.SetActive(needsScroll);
        }
    }

    private static float GetSceneMailItemStep(List<Button> rows)
    {
        float totalStep = 0f;
        int stepCount = 0;
        for (int i = 1; i < rows.Count; i++)
        {
            RectTransform previous = rows[i - 1].transform as RectTransform;
            RectTransform current = rows[i].transform as RectTransform;
            if (previous == null || current == null)
            {
                continue;
            }

            float step = previous.anchoredPosition.y - current.anchoredPosition.y;
            if (step > 0f)
            {
                totalStep += step;
                stepCount++;
            }
        }

        if (stepCount > 0)
        {
            return totalStep / stepCount;
        }

        RectTransform template = rows[0].transform as RectTransform;
        return template == null ? 96f : Mathf.Max(1f, template.rect.height + 16f);
    }

    private static Button FindSceneMailItemButton(List<Button> rows, int index)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i] != null
                && TryGetSceneMailItemIndex(rows[i].name, out int rowIndex)
                && rowIndex == index)
            {
                return rows[i];
            }
        }

        return null;
    }

    private static int CompareSceneMailItemIndex(Button left, Button right)
    {
        int leftIndex = left != null && TryGetSceneMailItemIndex(left.name, out int parsedLeft)
            ? parsedLeft
            : int.MaxValue;
        int rightIndex = right != null && TryGetSceneMailItemIndex(right.name, out int parsedRight)
            ? parsedRight
            : int.MaxValue;
        return leftIndex.CompareTo(rightIndex);
    }

    private static bool TryGetSceneMailItemIndex(string objectName, out int index)
    {
        const string prefix = "MailItem";
        index = -1;
        return !string.IsNullOrEmpty(objectName)
            && objectName.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(objectName.Substring(prefix.Length), out index);
    }

    private void RefreshSceneMailRewardSlots(Transform root, MailResponse mail)
    {
        Transform detailArea = FindSceneTransform(root, "MailDetailArea");
        if (detailArea == null)
        {
            return;
        }

        Transform viewport = FindSceneTransform(detailArea, "MailRewardSlotP");
        if (viewport == null)
        {
            return;
        }

        RectTransform viewportRect = viewport as RectTransform;
        if (viewportRect == null)
        {
            return;
        }

        RectTransform content = EnsureMailRewardContent(viewportRect);
        if (content == null)
        {
            return;
        }

        List<Transform> slots = GetMailRewardSlots(viewport);
        slots.Sort(CompareSceneMailRewardSlotPosition);
        if (slots.Count == 0)
        {
            return;
        }

        List<MailAttachmentResponse> attachments = new List<MailAttachmentResponse>();
        if (mail != null && mail.attachments != null)
        {
            for (int i = 0; i < mail.attachments.Length; i++)
            {
                if (mail.attachments[i] != null)
                {
                    attachments.Add(mail.attachments[i]);
                }
            }
        }

        Transform template = slots[0];
        float slotStep = GetMailRewardSlotStep(slots);
        while (slots.Count < attachments.Count)
        {
            Transform previous = slots[slots.Count - 1];
            GameObject clone = Instantiate(template.gameObject, content, false);
            clone.name = "MailRewardSlotRuntime" + slots.Count;
            RectTransform cloneRect = clone.transform as RectTransform;
            RectTransform previousRect = previous as RectTransform;
            if (cloneRect != null && previousRect != null)
            {
                cloneRect.anchoredPosition = new Vector2(
                    previousRect.anchoredPosition.x + slotStep,
                    previousRect.anchoredPosition.y);
            }

            slots.Add(clone.transform);
        }

        for (int i = 0; i < slots.Count; i++)
        {
            Transform slot = slots[i];
            bool visible = i < attachments.Count;
            slot.gameObject.SetActive(visible);
            if (!visible)
            {
                continue;
            }

            MailAttachmentResponse attachment = attachments[i];
            bool hasGrantedSnapshot = HasGrantedMailRewardSnapshot(attachment);
            string rewardQuality = hasGrantedSnapshot ? attachment.grantedRewardQuality : attachment.rewardQuality;
            ApplyRewardQualityBackground(slot, string.IsNullOrWhiteSpace(rewardQuality) ? "COMMON" : rewardQuality);
            string rewardName = hasGrantedSnapshot && !string.IsNullOrEmpty(attachment.grantedRewardName)
                ? attachment.grantedRewardName
                : (!string.IsNullOrEmpty(attachment.rewardName)
                    ? attachment.rewardName
                    : (!string.IsNullOrEmpty(attachment.itemName)
                        ? attachment.itemName
                        : (!string.IsNullOrEmpty(attachment.name) ? attachment.name : attachment.itemKey)));
            long quantity = hasGrantedSnapshot && attachment.grantedQuantity > 0
                ? attachment.grantedQuantity
                : attachment.quantity;
            SetSceneText(slot, "MailRewardText", $"x{quantity}");

            Image icon = FindSceneComponent<Image>(slot, "Item");
            if (icon != null)
            {
                Sprite sprite = ResolveMailAttachmentSprite(attachment, rewardName);
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }
        }

        UpdateMailRewardScroll(viewportRect, content, slots, attachments.Count, root);
    }

    private static RectTransform EnsureMailRewardContent(RectTransform viewport)
    {
        Transform contentTransform = viewport.Find("MailRewardContent");
        RectTransform content = contentTransform as RectTransform;
        if (content == null)
        {
            GameObject contentObject = new GameObject("MailRewardContent", typeof(RectTransform));
            content = contentObject.GetComponent<RectTransform>();
            content.SetParent(viewport, false);
        }

        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.localRotation = Quaternion.identity;
        content.localScale = Vector3.one;
        content.sizeDelta = new Vector2(Mathf.Max(1f, viewport.rect.width), Mathf.Max(1f, viewport.rect.height));

        Transform[] children = viewport.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null
                || child == viewport
                || child == content
                || child.parent == content
                || child.name != "MailRewardSlot")
            {
                continue;
            }

            child.SetParent(content, false);
        }

        RectMask2D mask = viewport.GetComponent<RectMask2D>();
        if (mask == null)
        {
            viewport.gameObject.AddComponent<RectMask2D>();
        }

        ScrollRect scroll = viewport.GetComponent<ScrollRect>();
        if (scroll == null)
        {
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
        }

        if (viewport.GetComponent<Graphic>() == null)
        {
            Image hitArea = viewport.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;
        }

        scroll.viewport = viewport;
        scroll.content = content;
        scroll.vertical = false;
        scroll.inertia = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        return content;
    }

    private static List<Transform> GetMailRewardSlots(Transform viewport)
    {
        List<Transform> slots = new List<Transform>();
        Transform[] children = viewport.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null
                && child != viewport
                && child.name.StartsWith("MailRewardSlot", StringComparison.Ordinal))
            {
                slots.Add(child);
            }
        }

        return slots;
    }

    private static float GetMailRewardSlotStep(List<Transform> slots)
    {
        if (slots.Count > 1)
        {
            float totalStep = 0f;
            int stepCount = 0;
            for (int i = 1; i < slots.Count; i++)
            {
                RectTransform previous = slots[i - 1] as RectTransform;
                RectTransform current = slots[i] as RectTransform;
                if (previous == null || current == null)
                {
                    continue;
                }

                float step = current.anchoredPosition.x - previous.anchoredPosition.x;
                if (step > 0f)
                {
                    totalStep += step;
                    stepCount++;
                }
            }

            if (stepCount > 0)
            {
                return totalStep / stepCount;
            }
        }

        RectTransform first = slots[0] as RectTransform;
        return first == null ? 100f : Mathf.Max(1f, first.rect.width + 16f);
    }

    private static void UpdateMailRewardScroll(
        RectTransform viewport,
        RectTransform content,
        List<Transform> slots,
        int visibleSlotCount,
        Transform root)
    {
        float contentWidth = viewport.rect.width;
        for (int i = 0; i < visibleSlotCount && i < slots.Count; i++)
        {
            RectTransform slot = slots[i] as RectTransform;
            if (slot == null)
            {
                continue;
            }

            float right = slot.anchoredPosition.x + slot.rect.width * (1f - slot.pivot.x) + 16f;
            contentWidth = Mathf.Max(contentWidth, right);
        }

        content.sizeDelta = new Vector2(contentWidth, Mathf.Max(1f, viewport.rect.height));
        ScrollRect scroll = viewport.GetComponent<ScrollRect>();
        if (scroll == null)
        {
            return;
        }

        bool needsScroll = visibleSlotCount > 5 || contentWidth > viewport.rect.width + 0.5f;
        scroll.horizontal = needsScroll;
        scroll.vertical = false;
        scroll.horizontalNormalizedPosition = 0f;

        Scrollbar scrollbar = FindSceneComponent<Scrollbar>(root, "MailRewardScrollbar");
        if (scrollbar != null)
        {
            scroll.horizontalScrollbar = scrollbar;
            scrollbar.gameObject.SetActive(needsScroll);
        }
    }

    private Sprite ResolveMailAttachmentSprite(MailAttachmentResponse attachment, string rewardName)
    {
        if (attachment == null)
        {
            return null;
        }

        bool hasGrantedSnapshot = HasGrantedMailRewardSnapshot(attachment);
        string rewardType = hasGrantedSnapshot && !string.IsNullOrEmpty(attachment.grantedRewardType)
            ? attachment.grantedRewardType
            : attachment.attachmentType;
        rewardType = string.IsNullOrWhiteSpace(rewardType) ? "ITEM" : rewardType.Trim().ToUpperInvariant();
        string rewardCategory = hasGrantedSnapshot ? attachment.grantedRewardCategory : attachment.rewardCategory;
        string rewardQuality = hasGrantedSnapshot ? attachment.grantedRewardQuality : attachment.rewardQuality;
        string[] candidates = hasGrantedSnapshot
            ? new[]
            {
                attachment.grantedRewardIcon,
                attachment.grantedRewardKey,
                attachment.grantedRewardName
            }
            : new[]
            {
                attachment.rewardIcon,
                attachment.rewardKey,
                attachment.icon,
                attachment.itemKey,
                attachment.itemName,
                rewardName
            };
        for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
        {
            string candidate = candidates[candidateIndex];
            if (string.IsNullOrWhiteSpace(candidate)) continue;

            Sprite resourceSprite = LoadMailAttachmentResourceSprite(candidate);
            if (resourceSprite == null)
            {
                if (rewardType == "EQUIPMENT")
                {
                    resourceSprite = ResolveEquipmentSprite(candidate);
                }
                else if (rewardType == "NPC")
                {
                    resourceSprite = ResolveRoleSprite(new[] { candidate }, false);
                }
                else
                {
                    resourceSprite = ResolveMailInventorySprite(candidate);
                    if (resourceSprite == null
                        && string.Equals(rewardCategory, "NPC_FRAGMENT", StringComparison.OrdinalIgnoreCase))
                    {
                        resourceSprite = FindBoundRoleSprite(new[] { candidate }, false);
                    }
                }
            }
            if (resourceSprite != null)
            {
                return resourceSprite;
            }

            if (rewardType == "EQUIPMENT" || rewardType == "NPC") continue;

            string assetPath = GetMailAttachmentAssetPath(candidate, rewardType, rewardCategory);
            if (string.IsNullOrEmpty(assetPath))
            {
                continue;
            }

            if (assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                assetPath = assetPath.Substring("Assets/".Length);
            }

            if (assetPath.StartsWith("_Imported/weijun-game-project/Assets/", StringComparison.OrdinalIgnoreCase))
            {
                assetPath = assetPath.Substring("_Imported/weijun-game-project/Assets/".Length);
            }

            Sprite sprite = LoadSpriteFromAsset(assetPath);
            if (sprite != null)
            {
                return sprite;
            }
        }

        if (rewardType == "EQUIPMENT" && !string.IsNullOrWhiteSpace(rewardQuality))
        {
            string bindingKey = GetEquipmentIconBindingKey(rewardQuality, rewardCategory);
            Sprite equipmentSprite = ResolveEquipmentSprite(bindingKey);
            if (equipmentSprite == null)
            {
                equipmentSprite = LoadEquipmentSpriteFromProject(rewardQuality, rewardCategory);
            }
            if (equipmentSprite != null) return equipmentSprite;
        }

        if (rewardType == "NPC")
        {
            long npcId = hasGrantedSnapshot ? attachment.grantedAssetId : attachment.assetId;
            if (npcId > 0)
            {
                Sprite npcSprite = FindBoundRoleSprite(new[] { npcId.ToString(System.Globalization.CultureInfo.InvariantCulture) }, false);
                if (npcSprite != null) return npcSprite;
            }
        }

        if (Debug.isDebugBuild)
        {
            string snapshot = JsonUtility.ToJson(attachment);
            if (_missingMailRewardIcons.Add(snapshot))
            {
                Debug.LogWarning("[MailRewardIcon] 未找到邮件奖励图标映射，已跳过错误占位图。奖励快照: " + snapshot, this);
            }
        }

        return null;
    }

    private Sprite ResolveMailInventorySprite(string iconKey)
    {
        InventoryPanelView sceneView = inventoryPanelView;
        if (sceneView == null)
        {
            HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Inventory);
            if (panel != null) sceneView = panel.GetComponentInChildren<InventoryPanelView>(true);
        }

        Sprite sprite = sceneView != null ? sceneView.ResolveIcon(null, null, iconKey) : null;
        if (sprite != null) return sprite;

        InventoryPanelView prefab = Resources.Load<InventoryPanelView>("UI/InventoryPanel");
        return prefab != null ? prefab.ResolveIcon(null, null, iconKey) : null;
    }

    private static bool HasGrantedMailRewardSnapshot(MailAttachmentResponse attachment)
    {
        return attachment != null
            && (!string.IsNullOrEmpty(attachment.grantedRewardType)
                || attachment.grantedAssetId > 0
                || !string.IsNullOrEmpty(attachment.grantedRewardKey)
                || !string.IsNullOrEmpty(attachment.grantedRewardName)
                || !string.IsNullOrEmpty(attachment.grantedRewardIcon)
                || attachment.grantedQuantity > 0);
    }

    private static string GetMailAttachmentAssetPath(string value, string rewardType, string rewardCategory)
    {
        string key = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        if (key.IndexOf('/') >= 0 || key.IndexOf('\\') >= 0)
        {
            return GetShopIconAssetPath(key);
        }

        string normalizedType = (rewardType ?? string.Empty).Trim().ToUpperInvariant();
        if (normalizedType == "NPC" || normalizedType == "EQUIPMENT")
        {
            return string.Empty;
        }

        if (string.Equals(rewardCategory, "NPC_FRAGMENT", StringComparison.OrdinalIgnoreCase)
            || key.IndexOf("fragment", StringComparison.OrdinalIgnoreCase) >= 0
            || key.Contains("碎片"))
        {
            return string.Empty;
        }

        string assetPath = GetShopIconAssetPath(key);
        return string.Equals(assetPath, "UI/图标/主图标/装备.png", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : assetPath;
    }

    private static Sprite LoadMailAttachmentResourceSprite(string candidate)
    {
        string resourcePath = (candidate ?? string.Empty).Trim().Replace("\\", "/");
        if (string.IsNullOrEmpty(resourcePath)
            || resourcePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || resourcePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        int resourcesIndex = resourcePath.IndexOf("Resources/", StringComparison.OrdinalIgnoreCase);
        if (resourcesIndex >= 0)
        {
            resourcePath = resourcePath.Substring(resourcesIndex + "Resources/".Length);
        }
        resourcePath = resourcePath.TrimStart('/');

        string[] extensions = { ".png", ".jpg", ".jpeg" };
        for (int i = 0; i < extensions.Length; i++)
        {
            if (resourcePath.EndsWith(extensions[i], StringComparison.OrdinalIgnoreCase))
            {
                resourcePath = resourcePath.Substring(0, resourcePath.Length - extensions[i].Length);
                break;
            }
        }

        return string.IsNullOrEmpty(resourcePath) ? null : Resources.Load<Sprite>(resourcePath);
    }

    private static int CompareSceneMailRewardSlotPosition(Transform left, Transform right)
    {
        RectTransform leftRect = left as RectTransform;
        RectTransform rightRect = right as RectTransform;
        if (leftRect == null || rightRect == null)
        {
            return 0;
        }

        return leftRect.anchoredPosition.x.CompareTo(rightRect.anchoredPosition.x);
    }

    private bool TryShowSettingsScenePanel(string status)
    {
        HomePageScenePanel panel = OpenRegisteredScenePanel(HomePageScenePanelType.Settings);
        if (panel == null)
        {
            return false;
        }

        settingsPanelObject = panel.gameObject;
        GameSettingsRuntime.ApplyAll();
        RefreshSettingsScenePanel(panel.PanelRoot, status);
        SetStatus("已打开游戏设置");
        return true;
    }

    private void RefreshSettingsScenePanel(Transform root, string status)
    {
        if (root == null)
        {
            return;
        }

        Toggle musicToggle = FindSceneComponent<Toggle>(root, "SettingsMusicToggle");
        ConfigureSettingsToggle(musicToggle, GameSettingsRuntime.MusicEnabled);
        BindSceneToggle(musicToggle, value =>
        {
            GameSettingsRuntime.SetMusicEnabled(value);
            RefreshSettingsScenePanel(root, value ? "背景音乐已开启" : "背景音乐已关闭");
        });

        Slider musicSlider = FindSceneComponent<Slider>(root, "SettingsMusicSlider");
        if (musicSlider != null)
        {
            musicSlider.SetValueWithoutNotify(GameSettingsRuntime.MusicVolume);
        }
        BindSceneSlider(musicSlider, value =>
        {
            GameSettingsRuntime.SetMusicVolume(Mathf.RoundToInt(value));
            SetSceneText(root, "SettingsMusicValue", $"{GameSettingsRuntime.MusicVolume}%");
            SetSceneText(root, "SettingsStatus", "背景音乐音量已调整");
        });
        SetSceneText(root, "SettingsMusicValue", $"{GameSettingsRuntime.MusicVolume}%");

        Toggle soundToggle = FindSceneComponent<Toggle>(root, "SettingsSoundToggle");
        ConfigureSettingsToggle(soundToggle, GameSettingsRuntime.SoundEnabled);
        BindSceneToggle(soundToggle, value =>
        {
            GameSettingsRuntime.SetSoundEnabled(value);
            RefreshSettingsScenePanel(root, value ? "游戏音效已开启" : "游戏音效已关闭");
        });

        Slider soundSlider = FindSceneComponent<Slider>(root, "SettingsSoundSlider");
        if (soundSlider != null)
        {
            soundSlider.SetValueWithoutNotify(GameSettingsRuntime.SoundVolume);
        }
        BindSceneSlider(soundSlider, value =>
        {
            GameSettingsRuntime.SetSoundVolume(Mathf.RoundToInt(value));
            SetSceneText(root, "SettingsSoundValue", $"{GameSettingsRuntime.SoundVolume}%");
            SetSceneText(root, "SettingsStatus", "游戏音效音量已调整");
        });
        SetSceneText(root, "SettingsSoundValue", $"{GameSettingsRuntime.SoundVolume}%");

        BindSceneButton(FindSceneComponent<Button>(root, "SettingsCloseButton"), HideSettingsPanel);
        SetSceneText(root, "SettingsStatus", status);
    }

    private static void ConfigureSettingsToggle(Toggle toggle, bool isOn)
    {
        if (toggle == null)
        {
            return;
        }

        toggle.SetIsOnWithoutNotify(isOn);

        Text label = FindSceneComponent<Text>(toggle.transform, "ToggleLabel");
        if (label != null)
        {
            label.text = isOn ? "开启" : "关闭";
        }
    }

    private static void ConfigureSettingsSegmentButton(Button button, bool selected)
    {
        if (button == null)
        {
            return;
        }

        Image background = button.GetComponent<Image>();
        if (background != null)
        {
            background.color = selected
                ? new Color(0.20f, 0.56f, 0.66f, 1f)
                : new Color(0.90f, 0.80f, 0.62f, 1f);
        }

        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.color = selected ? Color.white : new Color(0.34f, 0.20f, 0.10f, 1f);
        }
    }

    private void HideSettingsPanel()
    {
        GameSettingsRuntime.Save();
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Settings);
        if (panel != null)
        {
            panel.SetVisible(false);
        }

        settingsPanelObject = null;
    }

    private static T FindSceneComponent<T>(Transform root, string objectName) where T : Component
    {
        if (root == null)
        {
            return null;
        }

        T[] components = root.GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i].name == objectName)
            {
                return components[i];
            }
        }

        return null;
    }

    private static Transform FindSceneTransform(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name == objectName)
            {
                return transforms[i];
            }
        }

        return null;
    }

    private static void SetSceneText(Transform root, string objectName, string value)
    {
        Text label = FindSceneComponent<Text>(root, objectName);
        if (label != null)
        {
            label.text = value ?? string.Empty;
            return;
        }

        TMP_Text tmpLabel = FindSceneComponent<TMP_Text>(root, objectName);
        if (tmpLabel != null)
        {
            tmpLabel.text = value ?? string.Empty;
        }
    }

    private static void SetSceneButtonText(Button button, string value)
    {
        if (button == null)
        {
            return;
        }

        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = value ?? string.Empty;
            return;
        }

        TMP_Text tmpLabel = button.GetComponentInChildren<TMP_Text>(true);
        if (tmpLabel != null)
        {
            tmpLabel.text = value ?? string.Empty;
        }
    }

    private static void BindSceneButton(Button button, Action action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        if (action != null)
        {
            button.onClick.AddListener(() => action());
        }
    }

    private static void BindSceneToggle(Toggle toggle, Action<bool> action)
    {
        if (toggle == null)
        {
            return;
        }

        toggle.onValueChanged.RemoveAllListeners();
        if (action != null)
        {
            toggle.onValueChanged.AddListener(value => action(value));
        }
    }

    private static void BindSceneSlider(Slider slider, Action<float> action)
    {
        if (slider == null)
        {
            return;
        }

        slider.onValueChanged.RemoveAllListeners();
        if (action != null)
        {
            slider.onValueChanged.AddListener(value => action(value));
        }
    }

    private static void SetSceneObjectActive(Transform root, string objectName, bool active)
    {
        Transform target = FindSceneTransform(root, objectName);
        if (target != null)
        {
            target.gameObject.SetActive(active);
        }
    }
}
