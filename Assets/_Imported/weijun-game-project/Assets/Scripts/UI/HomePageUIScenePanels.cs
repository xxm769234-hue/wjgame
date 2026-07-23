using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class HomePageUI
{
    private int lotterySceneFlipPhase;

    private HomePageScenePanel GetRegisteredScenePanel(HomePageScenePanelType type)
    {
        if (scenePanels == null)
        {
            scenePanels = FindObjectOfType<HomePageScenePanels>(true);
        }

        return scenePanels != null ? scenePanels.Get(type) : null;
    }

    private HomePageScenePanel OpenRegisteredScenePanel(HomePageScenePanelType type)
    {
        HomePageScenePanel panel = GetRegisteredScenePanel(type);
        if (panel == null)
        {
            return null;
        }

        panel.SetVisible(true);
        panel.transform.SetAsLastSibling();
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
        HomePageScenePanel panel = OpenRegisteredScenePanel(HomePageScenePanelType.Task);
        if (panel == null)
        {
            return false;
        }

        taskPanelObject = panel.gameObject;
        Transform root = panel.PanelRoot;
        SetSceneText(root, "TaskTotalProgress", $"本页进度  {completedCount}/{tasks.Length}");
        SetSceneText(root, "TaskCategoryTitle", GetTaskCategoryTitle(category));
        SetSceneText(root, "TaskCategoryNote", GetTaskCategoryNote(category));
        SetSceneText(root, "TaskFooterText", message);
        BindSceneButton(FindSceneComponent<Button>(root, "TaskCloseButton"), HideTaskPanel);

        string[] categoryIds = { "daily", "main", "growth", "activity" };
        string[] categoryLabels = { "每日任务", "主线任务", "成长任务", "活动任务" };
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
            Image background = button.GetComponent<Image>();
            if (background != null)
            {
                background.color = selected
                    ? new Color(0.88f, 0.35f, 0.18f, 1f)
                    : new Color(0.98f, 0.77f, 0.43f, 1f);
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
                marker.gameObject.SetActive(selected);
            }

            BindSceneButton(button, () => ShowTaskPanel(categoryId, $"已切换到{categoryLabel}"));
        }

        for (int i = 0; i < 4; i++)
        {
            Transform row = FindSceneTransform(root, "TaskRow" + i);
            if (row == null)
            {
                continue;
            }

            bool visible = i < tasks.Length;
            row.gameObject.SetActive(visible);
            if (visible)
            {
                ConfigureTaskSceneRow(row, tasks[i], i);
            }
        }

        SetStatus(message);
        return true;
    }

    private void ConfigureTaskSceneRow(Transform row, TaskDisplayItem task, int index)
    {
        bool claimed = IsTaskClaimed(task);
        Image rowImage = row.GetComponent<Image>();
        if (rowImage != null)
        {
            rowImage.color = claimed
                ? new Color(0.83f, 0.78f, 0.65f, 1f)
                : task.IsComplete
                    ? new Color(0.84f, 0.90f, 0.62f, 1f)
                    : index % 2 == 0
                        ? new Color(1f, 0.93f, 0.76f, 1f)
                        : new Color(0.96f, 0.86f, 0.66f, 1f);
        }

        Image accent = FindSceneComponent<Image>(row, "TaskRowAccent");
        if (accent != null)
        {
            accent.color = task.IsComplete
                ? new Color(0.35f, 0.58f, 0.23f, 1f)
                : new Color(0.82f, 0.35f, 0.16f, 1f);
        }

        Image numberBadge = FindSceneComponent<Image>(row, "TaskNumberBadge");
        if (numberBadge != null)
        {
            numberBadge.color = task.IsComplete
                ? new Color(0.42f, 0.65f, 0.24f, 1f)
                : new Color(0.58f, 0.30f, 0.13f, 1f);
        }

        SetSceneText(row, "TaskNumber", task.IsComplete ? "√" : (index + 1).ToString());
        SetSceneText(row, "TaskItemTitle", task.title);
        SetSceneText(row, "TaskItemDescription", task.description);
        SetSceneText(row, "TaskProgressTitle", task.IsComplete ? "任务完成" : "任务进度");
        SetSceneText(row, "TaskProgressValue", $"{Mathf.Min(task.current, task.target)}/{task.target}");
        SetSceneText(row, "TaskRewardText", $"{task.rewardName}\nx{task.rewardAmount}");

        Image progressFill = FindSceneComponent<Image>(row, "TaskProgressFill");
        if (progressFill != null)
        {
            float ratio = Mathf.Clamp01(task.current / (float)Mathf.Max(1, task.target));
            Vector2 size = progressFill.rectTransform.sizeDelta;
            progressFill.rectTransform.sizeDelta = new Vector2(190f * ratio, size.y);
            progressFill.color = task.IsComplete
                ? new Color(0.42f, 0.70f, 0.23f, 1f)
                : new Color(0.95f, 0.58f, 0.18f, 1f);
        }

        Image rewardIcon = FindSceneComponent<Image>(row, "TaskRewardIcon");
        if (rewardIcon != null)
        {
            rewardIcon.color = GetTaskSceneRewardColor(task.rewardKey);
        }

        SetSceneText(row, "TaskRewardIconText", string.IsNullOrEmpty(task.rewardName) ? "奖" : task.rewardName.Substring(0, 1));

        Button actionButton = FindSceneComponent<Button>(row, "TaskActionButton");
        if (actionButton == null)
        {
            return;
        }

        string buttonLabel = claimed ? "已领取" : task.IsComplete ? "领取" : task.goAction != null ? "前往" : "进行中";
        SetSceneButtonText(actionButton, buttonLabel);
        Image buttonImage = actionButton.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.color = claimed
                ? new Color(0.55f, 0.53f, 0.47f, 1f)
                : task.IsComplete
                    ? new Color(0.38f, 0.66f, 0.24f, 1f)
                    : new Color(0.83f, 0.34f, 0.16f, 1f);
        }

        actionButton.interactable = !claimed && (task.IsComplete || task.goAction != null);
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
            BindSceneButton(actionButton, task.goAction);
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

        panel.SetVisible(true);
        panel.transform.SetAsLastSibling();
        inventoryPanelObject = panel.gameObject;
        inventoryPanelView = view;
        ConfigureInventoryPrefab(view, visibleItems, selectedIndex, ownedCount, unlockedSlots);
        return true;
    }

    private bool TryShowRechargeScenePanel(PaymentProductResponse[] products, string status, bool disabled)
    {
        HomePageScenePanel panel = OpenRegisteredScenePanel(HomePageScenePanelType.Recharge);
        if (panel == null)
        {
            return false;
        }

        rechargePanelObject = panel.gameObject;
        Transform root = panel.PanelRoot;
        BindSceneButton(FindSceneComponent<Button>(root, "RechargeCloseButton"), HideRechargePanel);
        BindSceneButton(
            FindSceneComponent<Button>(root, "RechargeGuideButton"),
            () => SetStatus("贵族特权：充值获得贵族经验，特权接口待后续接入。"));

        SetSceneText(root, "RechargeGoldText", $"金币  {PlayerPrefs.GetInt("Gold", 1000)}");
        SetSceneText(root, "RechargeSoulCrystalText", $"魂晶  {PlayerPrefs.GetInt("SoulCrystal", 1200)}");
        SetSceneText(root, "RechargeCrystalText", $"蓝晶  {PlayerPrefs.GetInt("Crystal", 427)}");
        SetSceneText(root, "RechargePurpleCrystalText", $"紫晶  {PlayerPrefs.GetInt("PurpleCrystal", 0)}");
        SetSceneText(root, "RechargeStatus", status);

        PaymentProductResponse[] defaults = GetDefaultRechargeProducts();
        int count = Mathf.Min(products != null ? products.Length : 0, 8);
        for (int i = 0; i < 8; i++)
        {
            Transform card = FindSceneTransform(root, "RechargeProductCard" + i);
            if (card == null)
            {
                continue;
            }

            bool visible = i < count;
            card.gameObject.SetActive(visible);
            if (!visible)
            {
                continue;
            }

            PaymentProductResponse product = products[i] ?? defaults[i];
            string productName = string.IsNullOrEmpty(product.name) ? $"魂晶x{GetDefaultRechargeReward(i)}" : product.name;
            string rewardName = string.IsNullOrEmpty(product.rewardItemName) ? "魂晶" : product.rewardItemName;
            int apiRewardAmount = GetShopRewardAmount(product);
            int rewardQuantity = apiRewardAmount <= 0 ? GetDefaultRechargeReward(i) : apiRewardAmount;
            int bonus = Mathf.Max(0, Mathf.RoundToInt(rewardQuantity * GetFirstRechargeBonusRate(i)));

            SetSceneText(card, "ProductName", productName);
            SetSceneText(card, "HeaderText", $"{rewardName}x{rewardQuantity}");
            SetSceneText(card, "RewardAmount", rewardQuantity.ToString());
            SetSceneText(card, "BonusText", bonus > 0 ? $"首充赠 x{bonus}" : "首充赠礼");

            Button priceButton = FindSceneComponent<Button>(card, "RechargePriceButton");
            SetSceneButtonText(priceButton, FormatRechargePrice(product, i));
            BindSceneButton(
                priceButton,
                () => HandleRechargeProductSelected(product, productName, rewardName, rewardQuantity, bonus, disabled));
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
        string centerName = hasResult ? rewardName : "折扣券";
        string centerAmount = hasResult ? "x" + rewardValue : "10%";

        SetSceneText(root, "LotteryLeftName", hasResult ? "木材" : "宝石");
        SetSceneText(root, "LotteryLeftAmount", hasResult ? "x80" : "x?");
        SetSceneText(root, "LotteryCenterName", centerName);
        SetSceneText(root, "LotteryCenterAmount", centerAmount);
        SetSceneText(root, "LotteryRightName", hasResult ? "金币" : "闹钟");
        SetSceneText(root, "LotteryRightAmount", hasResult ? "x60" : "x1");
        SetSceneText(root, "LotteryChanceText", $"剩余 {GetLotteryChanceCount()} 次机会");
        SetSceneText(root, "LotteryStatus", status);

        Transform centerCard = FindSceneTransform(root, "LotteryCenterPrize");
        if (centerCard != null)
        {
            lotterySceneFlipPhase = isRolling ? lotterySceneFlipPhase + 1 : 0;
            float scaleX = isRolling && lotterySceneFlipPhase % 2 == 0 ? 0.72f : 1f;
            centerCard.localScale = new Vector3(scaleX, 1f, 1f);
        }

        Button closeButton = FindSceneComponent<Button>(root, "LotteryCloseButton");
        if (closeButton != null)
        {
            closeButton.interactable = !isRolling;
        }
        BindSceneButton(closeButton, isRolling ? null : (Action)HideLotteryPanel);

        Button startButton = FindSceneComponent<Button>(root, "LotteryStartButton");
        if (startButton != null)
        {
            startButton.interactable = !isRolling;
        }
        SetSceneButtonText(startButton, isRolling ? "翻动中..." : "点击开始");
        BindSceneButton(startButton, isRolling ? null : (Action)PerformLotteryDraw);

        BindSceneButton(
            FindSceneComponent<Button>(root, "LotteryPrizeButton"),
            () => ShowLotteryPanel("奖品池：金币、木材、石材、铁矿，后续可接入服务器奖池", rewardName, rewardValue, hasResult));
        BindSceneButton(
            FindSceneComponent<Button>(root, "LotteryRuleButton"),
            () => ShowLotteryPanel("规则：初始 100 次机会，每次抽奖消耗 1 次", rewardName, rewardValue, hasResult));

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

        string[] rewardNames = GetDailySignRewardNames();
        string[] rewardAmounts = GetDailySignRewardAmounts();
        for (int i = 0; i < 14; i++)
        {
            Transform card = FindSceneTransform(root, "DailySignCard" + (i + 1));
            bool claimed = i + 1 <= signedDays && signedDays > 0;
            bool highlighted = todaySigned && i + 1 == signedDays;
            ConfigureSignSceneCard(card, i + 1, rewardNames[i], rewardAmounts[i], claimed, highlighted);
        }

        SetSceneText(root, "DailySignStatus", $"进度 {Mathf.Clamp(signedDays, 0, 14)}/14    {status}");
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

        Image cardImage = card.GetComponent<Image>();
        if (cardImage != null)
        {
            cardImage.color = highlighted
                ? new Color(1f, 0.95f, 0.70f, 1f)
                : new Color(1f, 0.92f, 0.74f, 1f);
        }

        Image header = FindSceneComponent<Image>(card, "SignCardHeader");
        if (header != null)
        {
            header.color = claimed
                ? new Color(0.56f, 0.39f, 0.22f, 1f)
                : new Color(0.93f, 0.52f, 0.20f, 1f);
        }

        SetSceneText(card, "DayText", $"第{day}天");
        SetSceneText(card, "RewardText", $"{rewardName}\n{rewardAmount}");
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
        BindSceneButton(FindSceneComponent<Button>(root, "SystemMailCloseButton"), HideSystemMailPanel);
        BindSceneButton(
            FindSceneComponent<Button>(root, "ClaimAllButton"),
            ClaimAllMailAttachments);
        BindSceneButton(
            FindSceneComponent<Button>(root, "ClaimMailButton"),
            ClaimSelectedMailAttachment);
        BindSceneButton(FindSceneComponent<Button>(root, "DeleteMailButton"), DeleteSelectedMail);
        BindSceneButton(FindSceneComponent<Button>(root, "MailTabSystem"), () => SetStatus("已选择系统邮件分类"));
        BindSceneButton(FindSceneComponent<Button>(root, "MailTabReward"), () => SetStatus("已选择奖励邮件分类"));
        BindSceneButton(FindSceneComponent<Button>(root, "MailTabNotice"), () => SetStatus("已选择公告邮件分类"));

        List<MailResponse> validMails = new List<MailResponse>();
        if (mails != null)
        {
            for (int i = 0; i < mails.Length && validMails.Count < 5; i++)
            {
                if (mails[i] != null)
                {
                    validMails.Add(mails[i]);
                }
            }
        }

        Transform emptyCard = FindSceneTransform(root, "MailEmptyCard");
        if (emptyCard != null)
        {
            emptyCard.gameObject.SetActive(validMails.Count == 0);
        }

        for (int i = 0; i < 5; i++)
        {
            Button itemButton = FindSceneComponent<Button>(root, "MailItem" + i);
            if (itemButton == null)
            {
                continue;
            }

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
            SetSceneText(itemButton.transform, "MailItemMeta", $"{sender}  {attachment}");

            Image unreadMark = FindSceneComponent<Image>(itemButton.transform, "UnreadMark");
            if (unreadMark != null)
            {
                unreadMark.color = mail.readFlag
                    ? new Color(0.62f, 0.56f, 0.48f, 1f)
                    : new Color(0.90f, 0.22f, 0.16f, 1f);
            }

            int selectedIndex = i;
            BindSceneButton(itemButton, () => SelectSceneMail(root, validMails, selectedIndex));
        }

        if (validMails.Count == 0)
        {
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

        for (int i = 0; i < 5; i++)
        {
            Button itemButton = FindSceneComponent<Button>(root, "MailItem" + i);
            if (itemButton == null || !itemButton.gameObject.activeSelf)
            {
                continue;
            }

            Image background = itemButton.GetComponent<Image>();
            if (background != null)
            {
                background.color = i == selectedIndex
                    ? new Color(1f, 0.92f, 0.45f, 1f)
                    : new Color(1f, 0.96f, 0.78f, 1f);
            }
        }

        MailResponse mail = mails[selectedIndex];
        SelectMailForActions(mail);
        string title = string.IsNullOrEmpty(mail.title) ? "系统邮件" : mail.title;
        string sender = string.IsNullOrEmpty(mail.senderName) ? "系统" : mail.senderName;
        string content = string.IsNullOrEmpty(mail.content) ? "暂无详细内容，正在读取邮件详情..." : mail.content;
        string attachment = mail.hasAttachment ? (mail.attachmentClaimed ? "附件状态：已领取" : "附件状态：可领取") : "附件状态：无附件";
        string time = string.IsNullOrEmpty(mail.createTime) ? "时间：未知" : $"时间：{mail.createTime}";

        SetSceneText(root, "MailDetailTitle", title);
        SetSceneText(root, "MailDetailBody", $"{content}\n\n发件人：{sender}\n{time}\n{attachment}");
        SetSceneText(root, "MailRewardText", BuildMailAttachmentSummary(mail));
        SetSceneText(root, "MailStatus", mailDetailLoadingId == mail.mailId ? "正在读取邮件详情..." : attachment);

        Button claimButton = FindSceneComponent<Button>(root, "ClaimMailButton");
        if (claimButton != null)
        {
            claimButton.interactable = mail.hasAttachment && !mail.attachmentClaimed && !mail.expired && !mailMutationRunning;
        }
        BindSceneButton(claimButton, ClaimSelectedMailAttachment);

        Button deleteButton = FindSceneComponent<Button>(root, "DeleteMailButton");
        if (deleteButton != null)
        {
            deleteButton.interactable = (!mail.hasAttachment || mail.attachmentClaimed) && !mailMutationRunning;
        }
        BindSceneButton(deleteButton, DeleteSelectedMail);
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
            settingsLogoutConfirmation = false;
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
            settingsLogoutConfirmation = false;
            GameSettingsRuntime.SetMusicVolume(Mathf.RoundToInt(value));
            SetSceneText(root, "SettingsMusicValue", $"{GameSettingsRuntime.MusicVolume}%");
            SetSceneText(root, "SettingsStatus", "背景音乐音量已调整");
        });
        SetSceneText(root, "SettingsMusicValue", $"{GameSettingsRuntime.MusicVolume}%");

        Toggle soundToggle = FindSceneComponent<Toggle>(root, "SettingsSoundToggle");
        ConfigureSettingsToggle(soundToggle, GameSettingsRuntime.SoundEnabled);
        BindSceneToggle(soundToggle, value =>
        {
            settingsLogoutConfirmation = false;
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
            settingsLogoutConfirmation = false;
            GameSettingsRuntime.SetSoundVolume(Mathf.RoundToInt(value));
            SetSceneText(root, "SettingsSoundValue", $"{GameSettingsRuntime.SoundVolume}%");
            SetSceneText(root, "SettingsStatus", "游戏音效音量已调整");
        });
        SetSceneText(root, "SettingsSoundValue", $"{GameSettingsRuntime.SoundVolume}%");

        string[] qualityNames = { "流畅", "标准", "精细" };
        for (int i = 0; i < qualityNames.Length; i++)
        {
            int preset = i;
            Button button = FindSceneComponent<Button>(root, "SettingsQuality" + i);
            ConfigureSettingsSegmentButton(button, i == GameSettingsRuntime.QualityPreset);
            BindSceneButton(button, () =>
            {
                settingsLogoutConfirmation = false;
                GameSettingsRuntime.SetQualityPreset(preset);
                RefreshSettingsScenePanel(root, $"画质已切换为{qualityNames[preset]}");
            });
        }
        SetSceneText(root, "SettingsQualityValue", qualityNames[GameSettingsRuntime.QualityPreset]);

        Toggle fullScreenToggle = FindSceneComponent<Toggle>(root, "SettingsFullScreenToggle");
        ConfigureSettingsToggle(fullScreenToggle, GameSettingsRuntime.FullScreen);
        BindSceneToggle(fullScreenToggle, value =>
        {
            settingsLogoutConfirmation = false;
            GameSettingsRuntime.SetFullScreen(value);
            RefreshSettingsScenePanel(root, value ? "已切换为全屏显示" : "已切换为窗口显示");
        });

        int[] frameRates = { 30, 60 };
        for (int i = 0; i < frameRates.Length; i++)
        {
            int frameRate = frameRates[i];
            Button button = FindSceneComponent<Button>(root, "SettingsFrameRate" + frameRate);
            ConfigureSettingsSegmentButton(button, frameRate == GameSettingsRuntime.TargetFrameRate);
            BindSceneButton(button, () =>
            {
                settingsLogoutConfirmation = false;
                GameSettingsRuntime.SetTargetFrameRate(frameRate);
                RefreshSettingsScenePanel(root, $"帧率上限已设置为 {frameRate} FPS");
            });
        }

        BindSceneButton(FindSceneComponent<Button>(root, "SettingsCloseButton"), HideSettingsPanel);
        BindSceneButton(FindSceneComponent<Button>(root, "SettingsSaveButton"), SaveAndHideSettingsPanel);
        BindSceneButton(FindSceneComponent<Button>(root, "SettingsResetButton"), () =>
        {
            settingsLogoutConfirmation = false;
            GameSettingsRuntime.ResetDefaults();
            RefreshSettingsScenePanel(root, "已恢复默认设置");
        });
        BindSceneButton(FindSceneComponent<Button>(root, "SettingsLogoutButton"), () => HandleSettingsLogout(root));

        SetSceneButtonText(
            FindSceneComponent<Button>(root, "SettingsLogoutButton"),
            settingsLogoutConfirmation ? "确认返回登录" : "返回登录");
        SetSceneText(root, "SettingsAccount", $"当前账号：{PlayerPrefs.GetString("LoginUsername", "Guest")}");
        SetSceneText(root, "SettingsStatus", status);
    }

    private static void ConfigureSettingsToggle(Toggle toggle, bool isOn)
    {
        if (toggle == null)
        {
            return;
        }

        toggle.SetIsOnWithoutNotify(isOn);
        Image background = toggle.GetComponent<Image>();
        if (background != null)
        {
            background.color = isOn
                ? new Color(0.28f, 0.66f, 0.38f, 1f)
                : new Color(0.57f, 0.55f, 0.51f, 1f);
        }

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

    private void SaveAndHideSettingsPanel()
    {
        GameSettingsRuntime.Save();
        SetStatus("游戏设置已保存");
        HideSettingsPanel();
    }

    private void HideSettingsPanel()
    {
        GameSettingsRuntime.Save();
        settingsLogoutConfirmation = false;
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Settings);
        if (panel != null)
        {
            panel.SetVisible(false);
        }

        settingsPanelObject = null;
    }

    private void HandleSettingsLogout(Transform root)
    {
        if (!settingsLogoutConfirmation)
        {
            settingsLogoutConfirmation = true;
            RefreshSettingsScenePanel(root, "再次点击“确认返回登录”后退出当前账号");
            return;
        }

        GameSettingsRuntime.Save();
        ReturnToLogin();
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
