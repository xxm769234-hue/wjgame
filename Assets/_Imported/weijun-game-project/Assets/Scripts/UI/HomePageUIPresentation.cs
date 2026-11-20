using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class HomePageUI
{
    private static readonly Color PresentationInk = new Color(.24f, .16f, .11f, 1f);
    private static readonly Color PresentationSecondaryInk = new Color(.43f, .33f, .24f, 1f);
    private static Sprite presentationRowSprite;

    private void ConfigurePanelPresentation(HomePageScenePanel panel)
    {
        if (panel == null || panel.PanelRoot == null) return;
        Transform root = panel.PanelRoot;
        if (panel.PanelType == HomePageScenePanelType.Equipment)
            ConfigureEquipmentPresentation(root);
        else if (panel.PanelType == HomePageScenePanelType.Role)
            ConfigureRoleStatsPresentation(root);
        else if (panel.PanelType == HomePageScenePanelType.Shop)
            ConfigureShopPagingPresentation(root);
        else if (panel.PanelType == HomePageScenePanelType.Lottery)
            ConfigureLotteryPresentation(root);
        else if (panel.PanelType == HomePageScenePanelType.Task)
        {
            Transform template = FindSceneTransform(root, "TaskRow0");
            if (template != null) ConfigureTaskRowPresentation(template, null);
        }
        ApplyPresentationButton(FindSceneComponent<Button>(root, "FriendsGiftButton"), true, 23f);
        ApplyPresentationButton(FindSceneComponent<Button>(root, "FriendsDeleteButton"), false, 23f);
        ApplyPresentationButton(FindSceneComponent<Button>(root, "ClaimAllButton"), true, 22f);
        ApplyPresentationButton(FindSceneComponent<Button>(root, "ClaimMailButton"), true, 22f);
        ApplyPresentationButton(FindSceneComponent<Button>(root, "DeleteMailButton"), false, 22f);
        ApplyPresentationButton(FindSceneComponent<Button>(root, "ProfileSwitchLoginButton"), false, 22f);
        ApplyPresentationButton(FindSceneComponent<Button>(root, "ProfileCancelAccountButton"), false, 22f);
        ApplyPresentationButton(FindSceneComponent<Button>(root, "SignMakeupCancelButton"), false, 22f);
        ApplyPresentationButton(FindSceneComponent<Button>(root, "SignMakeupConfirmButton"), true, 22f);
        foreach (string name in new[] { "SignMakeupTitle", "SignMakeupDetails" })
        {
            TMP_Text label = FindSceneComponent<TMP_Text>(root, name);
            if (label == null) continue;
            ConfigurePresentationLabel(label, name == "SignMakeupTitle" ? 28f : 22f, name == "SignMakeupTitle");
            label.margin = Vector4.zero;
            label.outlineWidth = 0f;
            label.alignment = TextAlignmentOptions.Center;
        }
    }

    private static TMP_Text EnsurePresentationCaption(Transform parent, string objectName, string caption)
    {
        Transform existing = parent.Find(objectName);
        TMP_Text label = existing == null ? null : existing.GetComponent<TMP_Text>();
        if (label == null)
        {
            GameObject labelObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.layer = parent.gameObject.layer;
            labelObject.transform.SetParent(parent, false);
            label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = RuntimeFontAssetProvider.Resolve();
        }
        label.text = caption;
        label.raycastTarget = false;
        return label;
    }

    private static void ConfigureRoleStatsPresentation(Transform root)
    {
        RectTransform statistics = FindSceneComponent<RectTransform>(root, "RoleDetailBattleStats");
        if (statistics == null || statistics.rect.width < 200f) return;
        string[] names = { "Defense", "Attack", "Blood", "Speed" };
        string[] captions = { "防御", "攻击", "生命", "速度" };
        float columnWidth = statistics.rect.width * .5f;
        for (int index = 0; index < names.Length; index++)
        {
            Transform statistic = statistics.Find(names[index]);
            if (statistic == null) continue;
            PlaceReadableRect(statistics, names[index], (index % 2) * columnWidth, 3f + (index / 2) * 58f, 42f, 42f);
            Image icon = statistic.GetComponent<Image>();
            if (icon != null) { icon.preserveAspect = true; icon.raycastTarget = false; }
            TMP_Text caption = EnsurePresentationCaption(statistic, "StatCaption", captions[index]);
            PlaceReadableRect(statistic, "StatCaption", 50f, 0f, columnWidth - 54f, 20f);
            ConfigurePresentationLabel(caption, 15f, false);
            PlaceReadableRect(statistic, "Value", 50f, 23f, columnWidth - 54f, 27f);
            ConfigurePresentationLabel(FindSceneComponent<TMP_Text>(statistic, "Value"), 19f, true);
        }
    }

    private static void ConfigureRoleActionPresentation(Transform root)
    {
        foreach (string name in new[] { "RoleDetailUpgradeButton", "RoleDetailOneClickUpgradeButton", "RoleDetailAutoUpgradeButton", "BreakthroughButton" })
            ApplyPresentationButton(FindSceneComponent<Button>(root, name), name != "RoleDetailUpgradeButton", 25f);
        foreach (string name in new[] { "RoleDetailUpgradePreview", "RoleDetailEquipment", "RoleDetailAttribute", "RoleDetailProduction" })
        {
            TMP_Text label = FindSceneComponent<TMP_Text>(root, name);
            if (label == null) continue;
            ConfigurePresentationLabel(label, 22f, false);
            label.margin = Vector4.zero;
            if (name == "RoleDetailUpgradePreview") label.alignment = TextAlignmentOptions.Center;
        }
    }

    private static void ConfigureShopPagingPresentation(Transform root)
    {
        Button previous = FindSceneComponent<Button>(root, "ShopPreviousButton");
        Button next = FindSceneComponent<Button>(root, "ShopNextButton");
        ConfigureShopPageButton(previous, "上一页", -274f);
        ConfigureShopPageButton(next, "下一页", -100f);
        TMP_Text page = FindSceneComponent<TMP_Text>(root, "ShopPageText");
        if (page == null) return;
        PositionShopPageControl(page.rectTransform, -204f, 62f);
        ConfigurePresentationLabel(page, 19f, true);
        page.alignment = TextAlignmentOptions.Center;
    }

    private static void ConfigureShopPageButton(Button button, string caption, float right)
    {
        if (button == null) return;
        PositionShopPageControl(button.transform as RectTransform, right, 96f);
        SetSceneButtonText(button, caption);
        ApplyPresentationButton(button, false, 21f);
    }

    private static void PositionShopPageControl(RectTransform rectangle, float right, float width)
    {
        if (rectangle == null) return;
        rectangle.anchorMin = rectangle.anchorMax = new Vector2(1f, 0f);
        rectangle.pivot = new Vector2(1f, 0f);
        rectangle.anchoredPosition = new Vector2(right, 32f);
        rectangle.sizeDelta = new Vector2(width, 44f);
    }

    private static void ConfigureSettingsTogglePresentation(Toggle toggle, bool audible)
    {
        RectTransform rectangle = toggle.transform as RectTransform;
        if (rectangle != null)
        {
            float widthDifference = 160f - rectangle.rect.width;
            rectangle.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 160f);
            rectangle.anchoredPosition -= new Vector2(widthDifference * (1f - rectangle.pivot.x), 0f);
        }
        Image background = toggle.targetGraphic as Image;
        if (background != null)
        {
            Sprite paper = Resources.Load<Sprite>("UI/ApprovedUi/Controls/SecondaryButton");
            if (paper != null)
            {
                background.overrideSprite = null;
                background.sprite = paper;
                background.type = Image.Type.Simple;
                background.color = Color.white;
                background.preserveAspect = false;
                toggle.transition = Selectable.Transition.ColorTint;
                ColorBlock colors = toggle.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, .97f, .88f, 1f);
                colors.pressedColor = new Color(.86f, .80f, .67f, 1f);
                colors.selectedColor = Color.white;
                toggle.colors = colors;
            }
        }
        TMP_Text caption = EnsurePresentationCaption(toggle.transform, "AudioStateLabel", audible ? "开启" : "静音");
        caption.rectTransform.anchorMin = new Vector2(0f, .5f);
        caption.rectTransform.anchorMax = new Vector2(1f, .5f);
        caption.rectTransform.pivot = new Vector2(.5f, .5f);
        caption.rectTransform.offsetMin = new Vector2(64f, -22f);
        caption.rectTransform.offsetMax = new Vector2(-14f, 22f);
        ConfigurePresentationLabel(caption, 23f, true);
        caption.alignment = TextAlignmentOptions.Center;
    }

    private void ConfigureEquipmentPresentation(Transform root)
    {
        if (root == null) return;
        SetSceneObjectActive(root, "EquipmentSelection", false);
        RectTransform card = FindSceneComponent<RectTransform>(root, "EquipmentDetailCard");
        Transform content = FindSceneTransform(root, "EquipmentDetailContent");
        if (card == null || content == null || card.rect.width < 200f || card.rect.height < 300f) return;
        float width = card.rect.width;
        float height = card.rect.height;
        float innerWidth = width - 48f;
        float columnWidth = (innerWidth - 14f) * .5f;
        float actionTop = height - 148f;
        Image surface = card.GetComponent<Image>();
        if (surface != null)
        {
            Sprite paper = Resources.Load<Sprite>("IdleAdventureUI/RoundedPanel");
            if (paper != null)
            {
                surface.sprite = paper;
                surface.type = Image.Type.Sliced;
                surface.preserveAspect = false;
                surface.color = new Color(1f, .96f, .85f, .92f);
                Outline border = surface.GetComponent<Outline>();
                if (border == null) border = surface.gameObject.AddComponent<Outline>();
                border.effectColor = new Color(.43f, .29f, .17f, .48f);
                border.effectDistance = new Vector2(1.5f, -1.5f);
            }
        }

        PlaceReadableRect(content, "EquipmentDetailIconBackground", 24f, 24f, 88f, 88f);
        PlaceReadableRect(content, "EquipmentDetailName", 128f, 24f, width - 152f, 56f);
        PlaceReadableRect(content, "EquipmentDetailType", 128f, 86f, width - 152f, 28f);
        ConfigurePresentationLabel(FindSceneComponent<TMP_Text>(content, "EquipmentDetailName"), 26f, true, true);
        ConfigurePresentationLabel(FindSceneComponent<TMP_Text>(content, "EquipmentDetailType"), 18f, false);

        string[] statistics = { "Attack", "Defense", "Health", "Speed" };
        for (int index = 0; index < statistics.Length; index++)
        {
            string statistic = "EquipmentDetail" + statistics[index];
            Transform chip = FindSceneTransform(content, statistic + "Chip");
            if (chip == null) continue;
            PlaceReadableRect(content, statistic + "Chip", 24f + (index % 2) * (columnWidth + 14f),
                132f + (index / 2) * 44f, columnWidth, 38f);
            PlaceReadableRect(chip, statistic + "Mark", 0f, 3f, 28f, 30f);
            PlaceReadableRect(chip, statistic, 36f, 0f, columnWidth - 36f, 38f);
            ConfigurePresentationLabel(FindSceneComponent<TMP_Text>(chip, statistic), 20f, false);
        }

        PlaceReadableRect(content, "EquipmentDescriptionCard", 24f, 228f, innerWidth, 62f);
        PlaceReadableRect(content, "EquipmentDetailDescription", 0f, 0f, innerWidth, 62f);
        TMP_Text description = FindSceneComponent<TMP_Text>(content, "EquipmentDetailDescription");
        if (description != null)
        {
            description.fontSize = 17f;
            description.color = PresentationSecondaryInk;
            description.lineSpacing = 3f;
        }

        PlaceReadableRect(content, "EquipmentUpgradePreviewViewport", 24f, 306f, innerWidth, 60f);
        TMP_Text preview = FindSceneComponent<TMP_Text>(content, "EquipmentDetailUpgradePreview");
        if (preview != null)
        {
            preview.rectTransform.anchorMin = new Vector2(0f, 1f);
            preview.rectTransform.anchorMax = new Vector2(1f, 1f);
            preview.rectTransform.pivot = new Vector2(0f, 1f);
            preview.rectTransform.anchoredPosition = Vector2.zero;
            preview.rectTransform.sizeDelta = new Vector2(-8f, preview.rectTransform.sizeDelta.y);
            preview.enableAutoSizing = false;
            preview.fontSize = 18f;
            preview.enableWordWrapping = true;
            preview.color = PresentationInk;
            preview.raycastTarget = false;
        }
        PlaceReadableRect(content, "EquipmentDetailStatus", 24f, 378f, innerWidth, Mathf.Max(34f, actionTop - 390f));
        ConfigurePresentationLabel(FindSceneComponent<TMP_Text>(content, "EquipmentDetailStatus"), 17f, false, true);

        ConfigureEquipmentAction(content, "EquipmentActionButton", 24f, actionTop, columnWidth, false);
        ConfigureEquipmentAction(content, "EquipmentLockButton", 38f + columnWidth, actionTop, columnWidth, false);
        ConfigureEquipmentAction(content, "EquipmentUpgradeButton", 24f, actionTop + 62f, columnWidth, false);
        ConfigureEquipmentAction(content, "EquipmentAutoUpgradeButton", 38f + columnWidth, actionTop + 62f, columnWidth, true);
    }

    private static void ConfigureEquipmentAction(Transform root, string name, float left, float top, float width, bool primary)
    {
        Button button = FindSceneComponent<Button>(root, name);
        if (button == null) return;
        PlaceReadableRect(root, name, left, top, width, 52f);
        ApplyPresentationButton(button, primary, 23f);
    }

    private void ConfigureTaskRowPresentation(Transform row, TaskDisplayItem task)
    {
        RectTransform rectangle = row as RectTransform;
        if (rectangle == null || rectangle.rect.width < 300f) return;
        Image rowImage = row.GetComponent<Image>();
        if (presentationRowSprite == null)
            presentationRowSprite = Resources.Load<Sprite>("IdleAdventureUI/RoundedPanel");
        if (rowImage != null && presentationRowSprite != null)
        {
            rowImage.sprite = presentationRowSprite;
            rowImage.type = Image.Type.Sliced;
            rowImage.color = new Color(1f, .97f, .88f, .86f);
            Outline outline = rowImage.GetComponent<Outline>();
            if (outline == null) outline = rowImage.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.40f, .29f, .18f, .48f);
            outline.effectDistance = new Vector2(1f, -1f);
        }
        float width = rectangle.rect.width;
        float leftWidth = width * .57f;
        float rightLeft = width * .67f;
        float rightWidth = width - rightLeft - 32f;
        PlaceReadableRect(row, "TaskItemTitle", 28f, 14f, leftWidth, 32f);
        PlaceReadableRect(row, "TaskItemDescription", 28f, 51f, leftWidth, 26f);
        PlaceReadableRect(row, "TaskReward", 28f, 89f, leftWidth, 32f);
        Transform reward = FindSceneTransform(row, "TaskReward");
        Image rewardIcon = FindSceneComponent<Image>(row, "TaskRewardIcon");
        if (rewardIcon == null && reward != null)
            rewardIcon = CreateImage("TaskRewardIcon", reward, Color.clear);
        PlaceReadableRect(row, "TaskRewardIcon", 0f, 1f, 30f, 30f);
        PlaceReadableRect(row, "TaskRewardIconText", 0f, 1f, 30f, 30f);
        PlaceReadableRect(row, "TaskRewardText", 40f, 0f, leftWidth - 40f, 32f);
        TMP_Text rewardFallback = FindSceneComponent<TMP_Text>(row, "TaskRewardIconText");
        ConfigurePresentationLabel(rewardFallback, 18f, true);
        if (rewardFallback != null) rewardFallback.alignment = TextAlignmentOptions.Center;
        SetSceneObjectActive(row, "TaskProgressTitle", false);
        PlaceReadableRect(row, "TaskProgressValue", rightLeft, 14f, rightWidth, 28f);
        PlaceReadableRect(row, "TaskProgressBackground", rightLeft, 47f, rightWidth, 12f);
        PlaceReadableRect(row, "TaskProgressFill", rightLeft + 2f, 49f, rightWidth - 4f, 8f);
        PlaceReadableRect(row, "TaskActionButton", width - 220f, 73f, 188f, 48f);
        ConfigurePresentationLabel(FindSceneComponent<TMP_Text>(row, "TaskItemTitle"), 24f, true);
        ConfigurePresentationLabel(FindSceneComponent<TMP_Text>(row, "TaskItemDescription"), 18f, false);
        ConfigurePresentationLabel(FindSceneComponent<TMP_Text>(row, "TaskRewardText"), 20f, true);
        TMP_Text progress = FindSceneComponent<TMP_Text>(row, "TaskProgressValue");
        ConfigurePresentationLabel(progress, 20f, true);
        if (progress != null) progress.alignment = TextAlignmentOptions.Right;
        ApplyPresentationButton(FindSceneComponent<Button>(row, "TaskActionButton"), true, 23f);

        if (rewardIcon != null)
        {
            Sprite icon = task != null ? ResolveMailInventorySprite(task.rewardKey) : rewardIcon.sprite;
            rewardIcon.sprite = icon;
            rewardIcon.color = icon != null ? Color.white : Color.clear;
            rewardIcon.type = Image.Type.Simple;
            rewardIcon.preserveAspect = true;
            rewardIcon.raycastTarget = false;
            SetSceneObjectActive(row, "TaskRewardIconText", icon == null);
        }
    }

    private static void ConfigureLotteryPresentation(Transform root)
    {
        if (root == null) return;
        string[] actionNames = { "LotterySingleButton", "LotteryTenButton", "LotteryRewardConfirmButton" };
        foreach (string name in actionNames)
        {
            Button button = FindSceneComponent<Button>(root, name);
            if (button == null) continue;
            RectTransform rectangle = button.transform as RectTransform;
            if (rectangle != null) rectangle.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 104f);
            ApplyPresentationButton(button, name != "LotterySingleButton", 26f, name != "LotteryRewardConfirmButton");
        }
        Button pool = FindSceneComponent<Button>(root, "LotteryPoolButton");
        if (pool != null)
        {
            RectTransform rectangle = pool.transform as RectTransform;
            if (rectangle != null) rectangle.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 62f);
            ApplyPresentationButton(pool, false, 23f);
        }
    }

    private static void ApplyPresentationButton(Button button, bool primary, float fontSize, bool twoLines = false)
    {
        if (button == null) return;
        LoginCanvasStyle.ApplyDialogButton(button, primary);
        Transform shine = button.transform.Find("ButtonShine");
        if (shine != null) shine.gameObject.SetActive(false);
        Image image = button.targetGraphic as Image;
        if (image != null)
        {
            image.preserveAspect = false;
            image.pixelsPerUnitMultiplier = 2f;
        }
        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            RectTransform rectangle = label.rectTransform;
            rectangle.anchorMin = Vector2.zero;
            rectangle.anchorMax = Vector2.one;
            rectangle.offsetMin = new Vector2(12f, 4f);
            rectangle.offsetMax = new Vector2(-12f, -4f);
            ConfigurePresentationLabel(label, fontSize, true, twoLines);
            label.alignment = TextAlignmentOptions.Center;
            label.margin = Vector4.zero;
            label.fontSizeMin = fontSize - 3f;
        }
    }

    private static void ConfigurePresentationLabel(TMP_Text label, float fontSize, bool emphasized, bool wrap = false)
    {
        if (label == null) return;
        label.enableWordWrapping = wrap;
        label.enableAutoSizing = true;
        label.fontSizeMin = fontSize - 2f;
        label.fontSizeMax = fontSize;
        label.fontStyle = emphasized ? FontStyles.Bold : FontStyles.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.maxVisibleLines = wrap ? 2 : 1;
        label.color = emphasized ? PresentationInk : PresentationSecondaryInk;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;
    }
}
