using System;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class HomePageUI
{
    private const string BreakthroughArtworkPath = "UI/ApprovedUi/EquipmentBreakthrough/";
    private static readonly Color BreakthroughInk = new Color(.286f, .239f, .208f);
    private static readonly Color BreakthroughMutedInk = new Color(.47f, .37f, .26f);

    private void ConfigureEquipmentBreakthroughPresentation(Transform root)
    {
        Image overlay = root.GetComponent<Image>();
        if (overlay != null) overlay.color = new Color(.118f, .098f, .082f, .78f);
        Image body = FindSceneComponent<Image>(root, "BreakthroughBody");
        if (body == null) return;
        ApplyBreakthroughArtwork(body, "01_书屿工坊_面板底板", false);
        body.rectTransform.anchorMin = body.rectTransform.anchorMax = new Vector2(.5f, .5f);
        body.rectTransform.pivot = new Vector2(.5f, .5f);
        body.rectTransform.anchoredPosition = new Vector2(0f, 11f);
        body.rectTransform.sizeDelta = new Vector2(1280f, 850f);
        EquipmentBreakthroughPanelFit fit = body.GetComponent<EquipmentBreakthroughPanelFit>();
        if (fit == null) fit = body.gameObject.AddComponent<EquipmentBreakthroughPanelFit>();
        fit.Fit();
        RectTransform art = EnsureBreakthroughContainer(body.transform, "BreakthroughArtwork");
        Stretch(art);
        art.SetAsFirstSibling();
        BreakthroughDecoration(art, "TitlePlate", "02_铜橙标题铭牌", 390f, 0f, 500f, 100f);
        BreakthroughDecoration(art, "TargetMount", "03_突破装备展示托板", 60f, 128f, 340f, 540f);
        BreakthroughDecoration(art, "SelectedTray", "04_已选材料纸托", 454f, 172f, 750f, 190f);
        BreakthroughDecoration(art, "CandidatesTray", "05_候选材料四格底托", 454f, 402f, 750f, 220f);
        BreakthroughDecoration(art, "CostTray", "06_消耗横条底板", 454f, 624f, 750f, 72f);
        BreakthroughDecoration(art, "WorkshopBadge", "14_画笔与锤工坊徽章", 92f, 78f, 108f, 108f);
        BreakthroughLabel(body.transform, "BreakthroughTitle", "装备突破", 490f, 14f, 300f, 60f, 36f, true, TextAlignmentOptions.Center).color = new Color(1f, .953f, .839f);
        BreakthroughLabel(body.transform, "TargetCaption", "突破目标", 112f, 164f, 230f, 28f, 20f, false, TextAlignmentOptions.Center);
        BreakthroughLabel(body.transform, "BreakthroughTargetName", "", 88f, 193f, 280f, 48f, 28f, true, TextAlignmentOptions.Center, true);
        BreakthroughLabel(body.transform, "CurrentLevelCaption", "当前等级", 96f, 536f, 118f, 24f, 18f, false, TextAlignmentOptions.Center);
        BreakthroughLabel(body.transform, "TargetLevelCaption", "突破后", 239f, 536f, 118f, 24f, 18f, false, TextAlignmentOptions.Center);
        BreakthroughLabel(body.transform, "BreakthroughCurrentLevel", "", 96f, 560f, 118f, 40f, 29f, true, TextAlignmentOptions.Center);
        BreakthroughLabel(body.transform, "BreakthroughTargetLevel", "", 239f, 560f, 118f, 40f, 29f, true, TextAlignmentOptions.Center).color = new Color(.21f, .325f, .41f);
        BreakthroughLabel(body.transform, "BreakthroughLevelArrow", "→", 210f, 560f, 32f, 40f, 25f, false, TextAlignmentOptions.Center);
        BreakthroughLabel(body.transform, "BreakthroughTargetType", "", 100f, 608f, 260f, 36f, 20f, false, TextAlignmentOptions.Center);
        Image frame = EnsureBreakthroughImage(body.transform, "BreakthroughTargetFrame");
        PositionBreakthroughRect(frame.rectTransform, 135f, 262f, 184f, 184f);
        frame.raycastTarget = false;
        frame.preserveAspect = true;
        Image icon = EnsureBreakthroughImage(frame.transform, "BreakthroughTargetIcon");
        PositionBreakthroughRect(icon.rectTransform, 16.5f, 6.5f, 151f, 151f);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        BreakthroughLabel(body.transform, "MaterialSelectedTitle", "投入材料", 456f, 126f, 260f, 40f, 25f, true);
        BreakthroughLabel(body.transform, "BreakthroughMaterialCount", "", 1010f, 128f, 189f, 36f, 21f, false, TextAlignmentOptions.Right);
        BreakthroughLabel(body.transform, "MaterialCandidatesTitle", "可用材料", 456f, 359f, 260f, 40f, 25f, true);
        BreakthroughLabel(body.transform, "MaterialPage", "", 926f, 362f, 74f, 34f, 18f, false, TextAlignmentOptions.Center);
        BreakthroughLabel(body.transform, "BreakthroughCost", "本次消耗", 475f, 640f, 128f, 38f, 20f, true);
        BreakthroughLabel(body.transform, "BreakthroughSelectionHint", "", 456f, 694f, 746f, 38f, 20f, false);
        BreakthroughLabel(body.transform, "BreakthroughEmptyMaterials", "暂无可用的同款装备", 485f, 475f, 688f, 66f, 22f, false, TextAlignmentOptions.Center, true).gameObject.SetActive(false);
        Image slots = FindSceneComponent<Image>(root, "MaterialSlotsViewport");
        if (slots != null)
        {
            slots.color = Color.clear;
            slots.raycastTarget = true;
            PositionBreakthroughRect(slots.rectTransform, 466f, 196f, 728f, 154f);
            HorizontalLayoutGroup layout = FindSceneComponent<HorizontalLayoutGroup>(slots.transform, "MaterialSlots");
            if (layout != null)
            {
                layout.spacing = 0f;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childForceExpandWidth = false;
            }
            ScrollRect selectedMaterials = slots.GetComponent<ScrollRect>();
            selectedMaterials.StopMovement();
            selectedMaterials.content.anchoredPosition = new Vector2(0f, selectedMaterials.content.anchoredPosition.y);
        }
        for (int index = 0; index < EquipmentMaterialPageSize; index++)
            PlaceReadableRect(body.transform, "MaterialCandidate" + index, 477f + 181f * index, 425f, 164f, 170f);
        ConfigureBreakthroughButton(body.transform, "MaterialPrevious", "16_上一页箭头", "", 883f, 356f, 34f, 46f, 18f);
        ConfigureBreakthroughButton(body.transform, "MaterialNext", "17_下一页箭头", "", 1010f, 356f, 34f, 46f, 18f);
        Button reload = ConfigureBreakthroughButton(body.transform, "MaterialReload", null, "刷新", 1080f, 355f, 119f, 48f, 19f);
        Image refresh = EnsureBreakthroughImage(reload.transform, "RefreshIcon");
        ApplyBreakthroughArtwork(refresh, "15_刷新图标", false);
        PositionBreakthroughRect(refresh.rectTransform, 0f, 7f, 36f, 36f);
        reload.targetGraphic = refresh;
        TMP_Text reloadLabel = FindSceneComponent<TMP_Text>(reload.transform, "Text");
        if (reloadLabel != null) PositionBreakthroughRect(reloadLabel.rectTransform, 40f, 7f, 79f, 36f);
        ConfigureBreakthroughButton(body.transform, "BreakthroughPreview", "08_次操作按钮", "预览突破", 656f, 741f, 230f, 66f, 24f);
        ConfigureBreakthroughButton(body.transform, "BreakthroughConfirm", "07_主操作按钮", "确认突破", 905f, 737f, 300f, 72f, 27f);
        ConfigureBreakthroughButton(body.transform, "BreakthroughCancel", "09_轻按钮", "取消", 61f, 743f, 136f, 54f, 22f);
        ConfigureBreakthroughButton(body.transform, "BreakthroughClose", "10_关闭按钮", "", 1176f, 63f, 62f, 66f, 22f);
        ConfigureBreakthroughToggle(body.transform, "ConfirmIrreversible", "确认材料与培养投入不予返还", 455f, 696f, 484f);
        ConfigureBreakthroughToggle(body.transform, "ConfirmHighLevel", "确认消耗已培养的装备", 949f, 696f, 255f);
        ScrollRect costs = EnsureBreakthroughScroll(body.transform, "BreakthroughCostViewport", "BreakthroughCostItems", true);
        PositionBreakthroughRect(costs.viewport, 615f, 632f, 582f, 48f);
        ConfigureBreakthroughScrollbar(body.transform, costs, "BreakthroughCostScrollbar", 623f, 685f, 564f, 5f, true);
        ScrollRect selectedScroll = FindSceneComponent<ScrollRect>(root, "MaterialSlotsViewport");
        if (selectedScroll != null)
            ConfigureBreakthroughScrollbar(body.transform, selectedScroll, "BreakthroughMaterialsScrollbar", 480f, 346f, 702f, 5f, true);
        ScrollRect warning = EnsureBreakthroughScroll(body.transform, "BreakthroughWarningViewport", "BreakthroughWarningContent", false);
        PositionBreakthroughRect(warning.viewport, 80f, 670f, 320f, 64f);
        ConfigureBreakthroughScrollbar(body.transform, warning, "BreakthroughWarningScrollbar", 403f, 672f, 5f, 58f, false);
        TMP_Text warningText = FindSceneComponent<TMP_Text>(body.transform, "BreakthroughWarning");
        if (warningText != null)
        {
            warningText.transform.SetParent(warning.content, false);
            ConfigureBreakthroughLabel(warningText, 17f, false, TextAlignmentOptions.TopLeft, true);
            warningText.enableAutoSizing = false;
            warningText.color = new Color(.52f, .30f, .18f);
        }
    }

    private static void PositionBreakthroughRect(RectTransform rectangle, float left, float top, float width, float height)
    {
        rectangle.anchorMin = rectangle.anchorMax = new Vector2(0f, 1f);
        rectangle.pivot = new Vector2(0f, 1f);
        rectangle.anchoredPosition = new Vector2(left, -top);
        rectangle.sizeDelta = new Vector2(width, height);
    }

    private RectTransform EnsureBreakthroughContainer(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        return child != null ? child.GetComponent<RectTransform>() : CreateLayoutObject(name, parent).GetComponent<RectTransform>();
    }

    private Image EnsureBreakthroughImage(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        return child != null ? child.GetComponent<Image>() : CreateImage(name, parent, Color.white);
    }

    private static void ApplyBreakthroughArtwork(Image image, string asset, bool interactive)
    {
        image.overrideSprite = null;
        image.sprite = asset == null ? null : Resources.Load<Sprite>(BreakthroughArtworkPath + asset);
        image.type = Image.Type.Simple;
        image.color = asset == null ? Color.clear : Color.white;
        image.preserveAspect = false;
        image.raycastTarget = interactive;
    }

    private void BreakthroughDecoration(Transform parent, string name, string asset, float left, float top, float width, float height)
    {
        Image image = EnsureBreakthroughImage(parent, name);
        ApplyBreakthroughArtwork(image, asset, false);
        PositionBreakthroughRect(image.rectTransform, left, top, width, height);
    }

    private TMP_Text BreakthroughLabel(Transform parent, string name, string value, float left, float top, float width, float height,
        float size, bool bold, TextAlignmentOptions alignment = TextAlignmentOptions.Left, bool wrap = false)
    {
        TMP_Text label = EnsurePresentationCaption(parent, name, value);
        ConfigureBreakthroughLabel(label, size, bold, alignment, wrap);
        PositionBreakthroughRect(label.rectTransform, left, top, width, height);
        return label;
    }

    private static void ConfigureBreakthroughLabel(TMP_Text label, float size, bool bold, TextAlignmentOptions alignment, bool wrap = false)
    {
        label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        label.fontSize = label.fontSizeMax = size;
        label.fontSizeMin = Mathf.Min(size, 16f);
        label.enableAutoSizing = true;
        label.enableWordWrapping = wrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.alignment = alignment;
        label.color = bold ? BreakthroughInk : BreakthroughMutedInk;
        label.margin = Vector4.zero;
        label.raycastTarget = false;
        label.outlineWidth = 0f;
        foreach (Outline outline in label.GetComponents<Outline>()) outline.enabled = false;
        foreach (Shadow shadow in label.GetComponents<Shadow>()) shadow.enabled = false;
    }

    private Button ConfigureBreakthroughButton(Transform parent, string name, string asset, string caption,
        float left, float top, float width, float height, float fontSize)
    {
        Button button = FindSceneComponent<Button>(parent, name);
        if (button == null) button = CreateButton(parent, name, caption, Color.clear, BreakthroughInk);
        Image surface = button.GetComponent<Image>();
        ApplyBreakthroughArtwork(surface, asset, true);
        PositionBreakthroughRect(surface.rectTransform, left, top, width, height);
        button.targetGraphic = surface;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = Color.white;
        colors.highlightedColor = colors.selectedColor = new Color(1f, .97f, .90f);
        colors.pressedColor = new Color(.86f, .77f, .61f);
        colors.disabledColor = new Color(.67f, .64f, .58f, .65f);
        button.colors = colors;
        Transform shine = button.transform.Find("ButtonShine");
        if (shine != null) shine.gameObject.SetActive(false);
        TMP_Text label = EnsurePresentationCaption(button.transform, "Text", caption);
        ConfigureBreakthroughLabel(label, fontSize, name != "BreakthroughCancel" && name != "MaterialReload", TextAlignmentOptions.Center);
        PositionBreakthroughRect(label.rectTransform, 8f, 3f, width - 16f, height - 12f);
        label.gameObject.SetActive(!string.IsNullOrEmpty(caption));
        return button;
    }

    private void ConfigureBreakthroughToggle(Transform parent, string name, string caption, float left, float top, float width)
    {
        Toggle toggle = FindSceneComponent<Toggle>(parent, name);
        if (toggle == null) return;
        PositionBreakthroughRect((RectTransform)toggle.transform, left, top, width, 36f);
        Image box = FindSceneComponent<Image>(toggle.transform, "Box");
        Image check = FindSceneComponent<Image>(toggle.transform, "Check");
        ApplyBreakthroughArtwork(box, "18_未勾选框", true);
        PositionBreakthroughRect(box.rectTransform, 0f, 4f, 24f, 24f);
        ApplyBreakthroughArtwork(check, "13_确认勾选框", false);
        Stretch(check.rectTransform);
        TMP_Text label = FindSceneComponent<TMP_Text>(toggle.transform, "Text");
        label.text = caption;
        ConfigureBreakthroughLabel(label, 18f, false, TextAlignmentOptions.Left);
        PositionBreakthroughRect(label.rectTransform, 31f, 0f, width - 31f, 32f);
        toggle.colors = ColorBlock.defaultColorBlock;
    }

    private ScrollRect EnsureBreakthroughScroll(Transform parent, string name, string contentName, bool horizontal)
    {
        Image viewport = EnsureBreakthroughImage(parent, name);
        viewport.color = Color.clear;
        viewport.raycastTarget = true;
        if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect scroll = viewport.GetComponent<ScrollRect>();
        if (scroll == null) scroll = viewport.gameObject.AddComponent<ScrollRect>();
        RectTransform content = EnsureBreakthroughContainer(viewport.transform, contentName);
        PositionBreakthroughRect(content, 0f, 0f, 0f, 0f);
        scroll.viewport = viewport.rectTransform;
        scroll.content = content;
        scroll.horizontal = horizontal;
        scroll.vertical = !horizontal;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;
        return scroll;
    }

    private void ConfigureBreakthroughScrollbar(Transform parent, ScrollRect scroll, string name,
        float left, float top, float width, float height, bool horizontal)
    {
        Image track = EnsureBreakthroughImage(parent, name);
        track.color = new Color(.47f, .37f, .26f, .16f);
        track.raycastTarget = true;
        PositionBreakthroughRect(track.rectTransform, left, top, width, height);
        Scrollbar scrollbar = track.GetComponent<Scrollbar>();
        if (scrollbar == null) scrollbar = track.gameObject.AddComponent<Scrollbar>();
        Image handle = EnsureBreakthroughImage(track.transform, "Handle");
        handle.color = new Color(.47f, .37f, .26f, .6f);
        handle.raycastTarget = false;
        Stretch(handle.rectTransform);
        scrollbar.handleRect = handle.rectTransform;
        scrollbar.targetGraphic = handle;
        scrollbar.direction = horizontal ? Scrollbar.Direction.LeftToRight : Scrollbar.Direction.BottomToTop;
        scrollbar.transition = Selectable.Transition.None;
        if (horizontal)
        {
            scroll.horizontalScrollbar = scrollbar;
            scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }
        else
        {
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }
    }

    private void ConfigureEquipmentMaterialPresentation(Button button, EquipmentBreakthroughMaterial material, bool selected, bool slot)
    {
        ApplyBreakthroughArtwork(button.GetComponent<Image>(), null, true);
        Transform shine = button.transform.Find("ButtonShine");
        if (shine != null) shine.gameObject.SetActive(false);
        Transform oldLabel = button.transform.Find("Text");
        if (oldLabel != null) oldLabel.gameObject.SetActive(false);
        Image frame = EnsureBreakthroughImage(button.transform, "MaterialFrame");
        frame.sprite = GetEquipmentQualityBackground(material == null ? "COMMON" : material.quality);
        frame.color = Color.white;
        frame.preserveAspect = true;
        frame.raycastTarget = false;
        float frameSize = slot ? 100f : 104f;
        PositionBreakthroughRect(frame.rectTransform, slot ? 64f : 30f, slot ? 11f : 8f, frameSize, frameSize);
        button.targetGraphic = frame;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = colors.highlightedColor = colors.selectedColor = colors.pressedColor = colors.disabledColor = Color.white;
        button.colors = colors;
        Image icon = EnsureBreakthroughImage(frame.transform, "MaterialIcon");
        Sprite sprite = material == null ? null : ResolveEquipmentSprite(material.icon)
            ?? ResolveEquipmentSprite(material.equipmentKey) ?? ResolveEquipmentSprite(material.name);
        if (sprite == null && material != null)
        {
            InventoryItemResponse item = currentEquipmentItems == null ? null : currentEquipmentItems.FirstOrDefault(candidate => candidate != null
                && (candidate.playerEquipmentId == material.playerEquipmentId || candidate.equipmentId == material.equipmentId));
            if (item != null) sprite = ResolveEquipmentSprite(item);
        }
        icon.sprite = sprite;
        icon.gameObject.SetActive(sprite != null);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        PositionBreakthroughRect(icon.rectTransform, frameSize * .13f, frameSize * .035f, frameSize * .74f, frameSize * .74f);
        BreakthroughLabel(frame.transform, "MaterialLevel", material == null ? "待选择" : "Lv." + material.level,
            4f, frameSize * .73f, frameSize - 8f, frameSize * .26f, 17f, true, TextAlignmentOptions.Center);
        string state = material == null ? "选择下方装备" : slot ? (material.level > 1 ? "已培养 · 点击移除" : "点击移除")
            : selected ? (material.level > 1 ? "已选择 · 已培养" : "已选择")
            : !IsEquipmentMaterialSelectable(material, equipmentBreakthroughPlan) ? material.unavailableReason
            : equipmentBreakthroughMaterials.Count >= equipmentBreakthroughPlan.requiredMaterialCount ? "材料已选满" : "点击选择";
        BreakthroughLabel(button.transform, "MaterialState", state, 6f, slot ? 114f : 122f, slot ? 216f : 152f,
            slot ? 34f : 42f, 17f, selected && !slot, TextAlignmentOptions.Center, true);
        Image marker = EnsureBreakthroughImage(frame.transform, "MaterialSelectionMark");
        ApplyBreakthroughArtwork(marker, slot ? "12_移除角标" : "11_选中勾选角标", false);
        PositionBreakthroughRect(marker.rectTransform, frameSize - 22f, -9f, slot ? 30f : 36f, slot ? 32f : 38f);
        marker.gameObject.SetActive(selected);
        if (slot)
        {
            LayoutElement element = button.GetComponent<LayoutElement>();
            if (element != null) element.preferredWidth = element.minWidth = 228f;
        }
    }

    private void RenderEquipmentBreakthroughHeader(Transform root, EquipmentUpgradePreviewData plan)
    {
        SetSceneText(root, "BreakthroughTitle", "装备突破");
        SetSceneText(root, "BreakthroughTargetName", plan.name);
        SetSceneText(root, "BreakthroughCurrentLevel", "Lv." + plan.currentLevel);
        SetSceneText(root, "BreakthroughTargetLevel", "Lv." + plan.targetLevel);
        SetSceneText(root, "BreakthroughMaterialCount", $"已选 {equipmentBreakthroughMaterials.Count} / {plan.requiredMaterialCount}");
        InventoryItemResponse item = currentEquipmentItems == null ? null : currentEquipmentItems.FirstOrDefault(candidate => candidate != null && candidate.playerEquipmentId == plan.playerEquipmentId);
        Image frame = FindSceneComponent<Image>(root, "BreakthroughTargetFrame");
        Image icon = FindSceneComponent<Image>(root, "BreakthroughTargetIcon");
        frame.sprite = GetEquipmentQualityBackground(item == null ? "COMMON" : item.quality);
        icon.sprite = item != null ? ResolveEquipmentSprite(item) : ResolveEquipmentSprite(plan.equipmentKey) ?? ResolveEquipmentSprite(plan.name);
        icon.gameObject.SetActive(icon.sprite != null);
        SetSceneText(root, "BreakthroughTargetType", item == null ? "" : GetEquipmentQualityLabel(item.quality) + " · " + GetEquipmentTypeLabel(item));
        ScrollRect costs = FindSceneComponent<ScrollRect>(root, "BreakthroughCostViewport");
        EquipmentUpgradeResourceCostPreviewData[] resources = plan.resourceCosts == null
            ? new EquipmentUpgradeResourceCostPreviewData[0] : plan.resourceCosts.Where(cost => cost != null).ToArray();
        int count = resources.Length + 1;
        for (int index = 0; index < count; index++)
        {
            RectTransform entry = EnsureBreakthroughContainer(costs.content, "Cost" + index);
            entry.gameObject.SetActive(true);
            PositionBreakthroughRect(entry, index * 290f, 0f, 280f, 48f);
            string key = index == 0 ? "gold" : resources[index - 1].itemKey;
            string name = index == 0 ? "金币" : resources[index - 1].itemName;
            long amount = index == 0 ? plan.goldCost : resources[index - 1].amount;
            Image resourceIcon = EnsureBreakthroughImage(entry, "CostIcon");
            resourceIcon.sprite = ResolveMailInventorySprite(key) ?? LoadMailAttachmentResourceSprite(name);
            resourceIcon.preserveAspect = true;
            resourceIcon.raycastTarget = false;
            resourceIcon.gameObject.SetActive(resourceIcon.sprite != null);
            PositionBreakthroughRect(resourceIcon.rectTransform, 8f, 4f, 40f, 40f);
            string amountText = amount.ToString("N0", CultureInfo.InvariantCulture);
            if (resourceIcon.sprite == null) amountText = (string.IsNullOrWhiteSpace(name) ? "材料" : name) + " " + amountText;
            BreakthroughLabel(entry, "CostAmount", amountText, resourceIcon.sprite != null ? 62f : 8f, 0f,
                resourceIcon.sprite != null ? 214f : 268f, 48f, 27f, true);
        }
        for (int index = count; index < costs.content.childCount; index++) costs.content.GetChild(index).gameObject.SetActive(false);
        costs.content.sizeDelta = new Vector2(Mathf.Max(582f, count * 290f), 48f);
        costs.horizontal = count > 2;
        costs.horizontalScrollbar.gameObject.SetActive(costs.horizontal);
        if (!costs.horizontal) costs.horizontalNormalizedPosition = 0f;
        TMP_Text empty = FindSceneComponent<TMP_Text>(root, "BreakthroughEmptyMaterials");
        empty.gameObject.SetActive(equipmentMaterialPage.Length == 0);
        empty.text = equipmentBreakthroughBusy ? "正在读取材料…" : "暂无可用的同款装备";
        string warning = equipmentBreakthroughBusy ? "正在加载…" : equipmentBreakthroughMessage ?? string.Empty;
        string[] routineMessages = { "请选择同款装备作为材料；每件占一个材料框", "请选择材料，再预览突破", "材料选择已变化，请重新预览", "暂无可用的同款材料装备" };
        if (routineMessages.Contains(warning)) warning = string.Empty;
        const string consumptionNotice = "材料及其投入的金币、资源、突破材料永久消耗，不予返还。";
        if (warning.StartsWith(consumptionNotice, StringComparison.Ordinal)) warning = warning.Substring(consumptionNotice.Length).TrimStart('\n', '\r');
        ScrollRect warningScroll = FindSceneComponent<ScrollRect>(root, "BreakthroughWarningViewport");
        TMP_Text warningLabel = FindSceneComponent<TMP_Text>(root, "BreakthroughWarning");
        bool changed = warningLabel.text != warning;
        warningLabel.text = warning;
        warningLabel.overflowMode = TextOverflowModes.Overflow;
        float warningHeight = Mathf.Max(64f, warningLabel.GetPreferredValues(warning, 320f, Mathf.Infinity).y + 4f);
        PositionBreakthroughRect(warningLabel.rectTransform, 0f, 0f, 320f, warningHeight);
        warningScroll.content.sizeDelta = new Vector2(320f, warningHeight);
        warningScroll.vertical = warningHeight > 64f;
        warningScroll.verticalScrollbar.gameObject.SetActive(warningScroll.vertical);
        if (changed) warningScroll.verticalNormalizedPosition = 1f;
    }

    private void RefreshEquipmentBreakthroughLayout(Transform root, int requiredCount, bool previewed)
    {
        ScrollRect slots = FindSceneComponent<ScrollRect>(root, "MaterialSlotsViewport");
        HorizontalLayoutGroup layout = slots.content.GetComponent<HorizontalLayoutGroup>();
        int padding = Mathf.Max(0, Mathf.RoundToInt((728f - requiredCount * 228f) * .5f));
        layout.padding = new RectOffset(padding, padding, 0, 0);
        slots.horizontal = requiredCount * 228f > 728f;
        slots.horizontalScrollbar.gameObject.SetActive(slots.horizontal);
        if (!slots.horizontal) slots.horizontalNormalizedPosition = 0f;
        LayoutRebuilder.ForceRebuildLayoutImmediate(slots.content);
        TMP_Text hint = FindSceneComponent<TMP_Text>(root, "BreakthroughSelectionHint");
        hint.gameObject.SetActive(!previewed);
        hint.text = equipmentBreakthroughBusy ? "正在读取突破信息…" : equipmentBreakthroughMaterials.Count == requiredCount
            ? "材料已选齐，请先预览本次突破" : "请选择同款装备作为材料";
        EquipmentBreakthroughPanelFit fit = FindSceneComponent<EquipmentBreakthroughPanelFit>(root, "BreakthroughBody");
        if (fit != null) fit.Fit();
    }
}
