using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class IdleAdventureStartPreviewLayout : MonoBehaviour
{
    private const int FormationSlotCount = 6;
    private const int EnemyGroupCount = 6;
    private const int RewardSlotCount = 4;
    private const float RewardIconSize = 30f;
    private const string LayoutRootName = "Start Preview Layout";
    private const string FormationTitleResource = "IdleAdventureUI/FormationTitle";
    private const string StageTitleResource = "IdleAdventureUI/StageTitleNavigation";
    private const string RewardFrameResource = "IdleAdventureUI/RewardPreviewFrame";
    private const string PreviewFrameResource = "IdleAdventureUI/PreviewFrame";
    private const string FormationBackgroundResource = "IdleAdventureUI/FormationPreviewBackground";
    private const string ForestSlimeResource = "IdleAdventureEnemyForestSlime";
    private const string FlameRaiderResource = "IdleAdventureEnemyFlameRaider";

    [SerializeField] private RectTransform layoutRoot;
    [SerializeField] private TextMeshProUGUI formationCountText;
    [SerializeField] private Image[] formationSlots = new Image[0];
    [SerializeField] private Image[] formationPortraits = new Image[0];
    [SerializeField] private TextMeshProUGUI[] formationSlotLabels = new TextMeshProUGUI[0];
    [SerializeField] private TextMeshProUGUI stageTitleText;
    [SerializeField] private TextMeshProUGUI enemySummaryText;
    [SerializeField] private Image[] enemyImages = new Image[0];
    [SerializeField] private TextMeshProUGUI[] enemyLabels = new TextMeshProUGUI[0];
    [SerializeField] private Image[] rewardSlots = new Image[0];
    [SerializeField] private Image[] rewardIcons = new Image[0];
    [SerializeField] private TextMeshProUGUI[] rewardLabels = new TextMeshProUGUI[0];
    [SerializeField] private Button quickAutoFillButton;
    [SerializeField] private Button quickClearButton;

    private readonly List<Sprite> runtimeSprites = new List<Sprite>();
    private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
    private readonly bool[] formationOccupied = new bool[FormationSlotCount];
    private IdleAdventurePreviewIconCatalog previewIcons;
    private IdleAdventureFormationRoster formationRoster;
    private int[] deployedCharacters = new int[0];
    private bool formationInteractable = true;
    private Action<int, int> assignCharacter;

    public void BindFormationDrag(Action<int, int> onAssign) { assignCharacter = onAssign; }

    public RectTransform ResolveOnboardingFormationTarget()
    {
        if (layoutRoot == null || !layoutRoot.gameObject.activeInHierarchy) return null;
        RectTransform roster = layoutRoot.Find("Formation Roster") as RectTransform;
        RectTransform preview = layoutRoot.Find("Formation Preview Content") as RectTransform;
        if (roster == null || preview == null || !roster.gameObject.activeInHierarchy || !preview.gameObject.activeInHierarchy) return null;
        RectTransform region = layoutRoot.Find("Onboarding Formation Region") as RectTransform;
        if (region == null) region = CreateRect("Onboarding Formation Region", layoutRoot);
        Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        Vector3[] corners = new Vector3[4];
        foreach (RectTransform area in new[] { roster, preview })
        {
            area.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector2 local = layoutRoot.InverseTransformPoint(corner);
                minimum = Vector2.Min(minimum, local);
                maximum = Vector2.Max(maximum, local);
            }
        }
        region.anchorMin = region.anchorMax = new Vector2(.5f, .5f);
        region.pivot = new Vector2(.5f, .5f);
        region.localPosition = (minimum + maximum) * .5f;
        region.sizeDelta = maximum - minimum;
        return region;
    }

    private void BindFormationSlotInteraction(RectTransform target, int index, Image portrait)
    {
        Image hit = target.GetComponent<Image>();
        if (hit == null) hit = target.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        hit.raycastTarget = portrait == null || formationOccupied[index];
        IdleAdventurePreviewDropTarget drop = target.GetComponent<IdleAdventurePreviewDropTarget>();
        if (drop == null) drop = target.gameObject.AddComponent<IdleAdventurePreviewDropTarget>();
        drop.Bind(index, () => formationInteractable, (slot, character) => assignCharacter?.Invoke(slot, character));
        if (portrait == null) return;
        IdleAdventurePreviewDragItem drag = target.GetComponent<IdleAdventurePreviewDragItem>();
        if (drag == null) drag = target.gameObject.AddComponent<IdleAdventurePreviewDragItem>();
        drag.Bind(() => index < deployedCharacters.Length ? deployedCharacters[index] : -1,
            () => formationInteractable && formationOccupied[index], portrait);
    }

    public void BindRosterAction(Action<int> onSelectCharacter)
    {
        if (formationRoster != null) formationRoster.Bind(onSelectCharacter);
    }

    public void SetRoster(IdleAdventurePlayerNpcResponse[] characters, int[] deployed, bool loaded)
    {
        deployedCharacters = deployed != null ? (int[])deployed.Clone() : new int[0];
        if (formationRoster != null) formationRoster.SetCharacters(characters, deployed, loaded);
    }

    public void ShowRosterMessage(string message)
    {
        if (formationRoster != null) formationRoster.ShowMessage(message);
    }

    public void EnsureLayout(RectTransform startPanel)
    {
        if (startPanel == null) return;

        if (!ResolveExistingBindings(startPanel))
        {
            RemoveIncompleteLayout(startPanel);
            BuildLayout(startPanel);
        }

        RemoveDuplicateLayouts(startPanel);
        EnsurePreviewIcons();
        if (!TryApplyBookSkin(startPanel))
        {
            RefreshResourceSprites();
            ConfigurePreviewSurfaces();
            ConfigureExistingControls(startPanel);
            ApplyBookPagePerspective(startPanel);
        }
        ConfigureRewardRows();
        MoveLayoutBelowFormationWindow(startPanel);
    }

    public void BindFormationActions(Action onOpenFormation, Action onAutoFill, Action onClear)
    {
        BindButton(FindButton(transform, "Open Formation Button"), onOpenFormation);
        BindButton(quickAutoFillButton, onAutoFill);
        BindButton(quickClearButton, onClear);
    }

    public void SetFormation(string[] names, int deployedCount, string[] iconKeys = null)
    {
        if (!HasFormationBindings()) return;

        formationCountText.text = FrontendTextLocalization.Normalize($"{Mathf.Clamp(deployedCount, 0, FormationSlotCount)}/{FormationSlotCount}");
        for (int i = 0; i < FormationSlotCount; i++)
        {
            bool occupied = names != null && i < names.Length && !string.IsNullOrWhiteSpace(names[i]);
            formationOccupied[i] = occupied;
            formationSlotLabels[i].text = string.Empty;
            formationSlotLabels[i].gameObject.SetActive(false);
            formationSlots[i].color = Color.clear;
            string iconKey = iconKeys != null && i < iconKeys.Length ? iconKeys[i] : null;
            bool matched = occupied && CharacterIdleImage.TryApply(formationPortraits[i], iconKey, names[i]);
            if (!occupied) CharacterIdleImage.Stop(formationPortraits[i]);
            Sprite portrait = matched ? formationPortraits[i].sprite
                : occupied ? LoadSprite(iconKey) ?? LoadSprite("IdleAdventureCharacter") : null;
            SetPreviewIcon(formationPortraits[i], portrait);
        }
        if (bookSkinReady) ConfigureBookFormation();
    }

    public void SetStage(int stage, string stageName, bool replay)
    {
        if (stageTitleText == null) return;

        int resolvedStage = Mathf.Max(1, stage);
        string subtitle = string.IsNullOrWhiteSpace(stageName)
            ? (replay ? "再次挑战" : "挑战准备")
            : stageName.Trim();
        stageTitleText.text = FrontendTextLocalization.Normalize($"关卡 {resolvedStage}\n{subtitle}");
    }

    public void SetEnemyPreview(IdleAdventureStageEnemiesResponse preview)
    {
        if (!HasEnemyBindings()) return;

        List<EnemyDisplay> groups = BuildEnemyGroups(preview);
        int enemyCount = 0;
        foreach (EnemyDisplay group in groups) enemyCount += group.Count;
        enemySummaryText.text = FrontendTextLocalization.Normalize(preview == null
            ? "关卡敌人预览  ·  数据读取中"
            : $"关卡敌人预览  ·  共 {enemyCount} 名");
        LayoutEnemyGroups(Mathf.Min(groups.Count, EnemyGroupCount));

        for (int i = 0; i < EnemyGroupCount; i++)
        {
            bool visible = i < groups.Count;
            enemyImages[i].transform.parent.gameObject.SetActive(visible);
            if (!visible) continue;

            EnemyDisplay group = groups[i];
            enemyImages[i].sprite = LoadSprite(group.IconKey) ?? ResolveEnemySprite(group.SearchKey);
            enemyImages[i].color = enemyImages[i].sprite != null ? Color.white : new Color(0.36f, 0.28f, 0.19f, 0.35f);
            enemyImages[i].preserveAspect = true;
            bool unknownAppearance = enemyImages[i].sprite == LoadSprite("UI/ApprovedUi/Controls/11_unknown_enemy_candidate");
            enemyImages[i].rectTransform.localScale = new Vector3(unknownAppearance ? 1f : -1f, 1f, 1f);

            string levelText = group.Level > 0 ? $"Lv.{group.Level}  " : string.Empty;
            enemyLabels[i].text = FrontendTextLocalization.Normalize(bookSkinReady
                ? $"{group.Name}\n{levelText}×{Mathf.Max(1, group.Count)}"
                : $"{levelText}{group.Name}\n×{Mathf.Max(1, group.Count)}");
        }
    }

    public void SetRewards(IdleAdventureRewardItemResponse[] backendRewards, IdleAdventureRewardItemDefinition[] fallbackRewards)
    {
        if (!HasRewardBindings()) return;

        List<RewardDisplay> rewards = new List<RewardDisplay>();
        if (backendRewards != null)
        {
            for (int i = 0; i < backendRewards.Length; i++)
            {
                IdleAdventureRewardItemResponse reward = backendRewards[i];
                if (reward == null || reward.quantity <= 0L) continue;
                string name = reward.ResolvedDisplayName;
                if (string.IsNullOrWhiteSpace(name)) name = "奖励";
                string iconKey = string.IsNullOrEmpty(reward.rewardIcon) ? reward.reward_icon : reward.rewardIcon;
                rewards.Add(new RewardDisplay(name, reward.quantity, reward.ResolvedItemKey, iconKey));
            }
        }

        if (rewards.Count == 0 && fallbackRewards != null)
        {
            for (int i = 0; i < fallbackRewards.Length; i++)
            {
                IdleAdventureRewardItemDefinition reward = fallbackRewards[i];
                if (reward == null) continue;
                rewards.Add(new RewardDisplay(reward.DisplayName, reward.Quantity, reward.ItemKey, null));
            }
        }

        for (int i = 0; i < RewardSlotCount; i++)
        {
            bool visible = i < rewards.Count;
            rewardSlots[i].gameObject.SetActive(visible || (i == 0 && rewards.Count == 0));
            if (!visible)
            {
                SetPreviewIcon(rewardIcons[i], null);
                if (i == 0 && rewards.Count == 0)
                {
                    rewardLabels[i].text = FrontendTextLocalization.Normalize("奖励数据读取中");
                    ConfigureRewardLabel(rewardLabels[i], false);
                }
                continue;
            }

            RewardDisplay reward = rewards[i];
            Sprite icon = ResolveRewardIcon(reward);
            rewardLabels[i].text = FrontendTextLocalization.Normalize(bookSkinReady && icon != null
                ? $"×{reward.Quantity}"
                : $"{reward.Name} ×{reward.Quantity}");
            SetPreviewIcon(rewardIcons[i], icon);
            ConfigureRewardLabel(rewardLabels[i], icon != null);
        }
        ConfigureRewardRows();
    }

    public void SetFormationActionsInteractable(bool interactable)
    {
        formationInteractable = interactable;
        if (!interactable) foreach (IdleAdventurePreviewDragItem drag in GetComponentsInChildren<IdleAdventurePreviewDragItem>(true)) drag.CancelDrag();
        Button formationButton = FindButton(transform, "Open Formation Button");
        if (formationButton != null) formationButton.interactable = interactable;
        if (quickAutoFillButton != null) quickAutoFillButton.interactable = interactable;
        if (quickClearButton != null) quickClearButton.interactable = interactable;
        if (formationRoster != null) formationRoster.SetInteractable(interactable);
    }

    private bool ResolveExistingBindings(RectTransform startPanel)
    {
        Transform existingRoot = startPanel.Find(LayoutRootName);
        if (existingRoot == null) return false;

        layoutRoot = existingRoot as RectTransform;
        formationCountText = FindText(layoutRoot, "Formation Count Badge/Label");
        stageTitleText = FindText(layoutRoot, "Stage Title Navigation/Stage Title Text");
        enemySummaryText = FindText(layoutRoot, "Enemy Preview Content/Enemy Summary");
        quickAutoFillButton = FindButton(layoutRoot, "Quick Auto Fill Button");
        quickClearButton = FindButton(layoutRoot, "Quick Clear Button");

        formationSlots = new Image[FormationSlotCount];
        formationSlotLabels = new TextMeshProUGUI[FormationSlotCount];
        for (int i = 0; i < FormationSlotCount; i++)
        {
            Transform slot = layoutRoot.Find($"Formation Preview Content/Formation Preview Slot {i + 1}");
            formationSlots[i] = slot != null ? slot.GetComponent<Image>() : null;
            formationSlotLabels[i] = FindText(slot, "Label");
        }

        enemyImages = new Image[EnemyGroupCount];
        enemyLabels = new TextMeshProUGUI[EnemyGroupCount];
        for (int i = 0; i < EnemyGroupCount; i++)
        {
            Transform group = layoutRoot.Find($"Enemy Preview Content/Enemy Group {i + 1}");
            Transform imageTransform = group != null ? group.Find("Enemy Image") : null;
            enemyImages[i] = imageTransform != null ? imageTransform.GetComponent<Image>() : null;
            enemyLabels[i] = FindText(group, "Enemy Label");
        }

        rewardSlots = new Image[RewardSlotCount];
        rewardLabels = new TextMeshProUGUI[RewardSlotCount];
        for (int i = 0; i < RewardSlotCount; i++)
        {
            Transform slot = layoutRoot.Find($"Reward Preview Content/Reward Slot {i + 1}");
            rewardSlots[i] = slot != null ? slot.GetComponent<Image>() : null;
            rewardLabels[i] = FindText(slot, "Label");
        }

        return layoutRoot != null
            && formationCountText != null
            && stageTitleText != null
            && enemySummaryText != null
            && quickAutoFillButton != null
            && quickClearButton != null
            && HasFormationBindings()
            && HasEnemyBindings()
            && HasRewardBindings();
    }

    private void BuildLayout(RectTransform startPanel)
    {
        layoutRoot = CreateRect(LayoutRootName, startPanel);
        Stretch(layoutRoot);

        Image formationTitle = CreateImage("Formation Title", layoutRoot, Color.white);
        Anchor(formationTitle.rectTransform, new Vector2(0.18f, 0.825f), new Vector2(0.415f, 0.925f));
        ApplyResourceSprite(formationTitle, FormationTitleResource, false);

        Image countBadge = CreateImage("Formation Count Badge", layoutRoot, new Color(0.47f, 0.31f, 0.15f, 0.96f));
        Anchor(countBadge.rectTransform, new Vector2(0.415f, 0.845f), new Vector2(0.49f, 0.905f));
        AddOutline(countBadge, new Color(0.19f, 0.105f, 0.04f, 1f), new Vector2(2f, -2f));
        formationCountText = CreateText("Label", countBadge.transform, "0/6 已上阵", 20f, FontStyles.Bold);
        Stretch(formationCountText.rectTransform);
        formationCountText.color = new Color(1f, 0.93f, 0.75f, 1f);

        RectTransform formationContent = CreateRect("Formation Preview Content", layoutRoot);
        Anchor(formationContent, new Vector2(0.168f, 0.225f), new Vector2(0.495f, 0.805f));

        Image formationBackground = CreateImage("Formation Preview Background", formationContent, Color.white);
        Stretch(formationBackground.rectTransform);
        ApplyResourceSprite(formationBackground, FormationBackgroundResource, false);

        formationSlots = new Image[FormationSlotCount];
        formationSlotLabels = new TextMeshProUGUI[FormationSlotCount];
        for (int i = 0; i < FormationSlotCount; i++)
        {
            bool frontColumn = i < 3;
            int row = i % 3;
            int column = frontColumn ? 1 : 0;
            float left = column == 0 ? 0.08f : 0.535f;
            float right = column == 0 ? 0.465f : 0.92f;
            float top = 0.93f - row * 0.30f;
            float bottom = top - 0.245f;

            Image slot = CreateImage($"Formation Preview Slot {i + 1}", formationContent, Color.clear);
            Anchor(slot.rectTransform, new Vector2(left, bottom), new Vector2(right, top));

            TextMeshProUGUI label = CreateText("Label", slot.transform, "空位", 18f, FontStyles.Bold);
            Anchor(label.rectTransform, new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.34f));
            label.color = new Color(0.30f, 0.25f, 0.19f, 0.78f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = 18f;

            formationSlots[i] = slot;
            formationSlotLabels[i] = label;
        }

        Image formationFrame = CreateImage("Formation Preview Frame", layoutRoot, Color.white);
        Anchor(formationFrame.rectTransform, new Vector2(0.162f, 0.215f), new Vector2(0.501f, 0.815f));
        ApplyResourceSprite(formationFrame, PreviewFrameResource, false);

        Image stageNavigation = CreateImage("Stage Title Navigation", layoutRoot, Color.white);
        Anchor(stageNavigation.rectTransform, new Vector2(0.52f, 0.81f), new Vector2(0.835f, 0.93f));
        ApplyResourceSprite(stageNavigation, StageTitleResource, false);

        stageTitleText = CreateText("Stage Title Text", stageNavigation.transform, "关卡 1\n挑战准备", 30f, FontStyles.Bold);
        Anchor(stageTitleText.rectTransform, new Vector2(0.15f, 0.10f), new Vector2(0.85f, 0.90f));
        stageTitleText.color = new Color(0.19f, 0.105f, 0.04f, 1f);
        stageTitleText.enableAutoSizing = true;
        stageTitleText.fontSizeMin = 20f;
        stageTitleText.fontSizeMax = 31f;

        RectTransform enemyContent = CreateRect("Enemy Preview Content", layoutRoot);
        Anchor(enemyContent, new Vector2(0.525f, 0.3475f), new Vector2(0.83f, 0.785f));

        Image combatBackground = CreateImage("Enemy Preview Background", enemyContent, Color.white);
        Stretch(combatBackground.rectTransform);
        combatBackground.color = new Color(0.88f, 0.82f, 0.74f, 0.32f);

        Image enemyShade = CreateImage("Enemy Preview Shade", enemyContent, new Color(0.20f, 0.11f, 0.045f, 0.12f));
        Stretch(enemyShade.rectTransform);

        enemySummaryText = CreateText("Enemy Summary", enemyContent, "关卡敌人预览", 19f, FontStyles.Bold);
        Anchor(enemySummaryText.rectTransform, new Vector2(0.06f, 0.86f), new Vector2(0.94f, 0.98f));
        enemySummaryText.color = new Color(0.19f, 0.105f, 0.04f, 1f);

        enemyImages = new Image[EnemyGroupCount];
        enemyLabels = new TextMeshProUGUI[EnemyGroupCount];
        for (int i = 0; i < EnemyGroupCount; i++)
        {
            RectTransform group = CreateRect($"Enemy Group {i + 1}", enemyContent);
            Image enemyImage = CreateImage("Enemy Image", group, Color.white);
            Anchor(enemyImage.rectTransform, new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.93f));
            enemyImage.preserveAspect = true;

            TextMeshProUGUI label = CreateText("Enemy Label", group, "敌人", 17f, FontStyles.Bold);
            Anchor(label.rectTransform, new Vector2(0.01f, 0.01f), new Vector2(0.99f, 0.31f));
            label.color = new Color(0.19f, 0.105f, 0.04f, 1f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 11f;
            label.fontSizeMax = 17f;

            enemyImages[i] = enemyImage;
            enemyLabels[i] = label;
        }
        LayoutEnemyGroups(EnemyGroupCount);

        Image enemyFrame = CreateImage("Enemy Preview Frame", layoutRoot, Color.white);
        Anchor(enemyFrame.rectTransform, new Vector2(0.515f, 0.3325f), new Vector2(0.84f, 0.795f));
        ApplyResourceSprite(enemyFrame, PreviewFrameResource, false);

        RectTransform rewardContent = CreateRect("Reward Preview Content", layoutRoot);
        Anchor(rewardContent, new Vector2(0.535f, 0.2125f), new Vector2(0.82f, 0.3025f));

        rewardSlots = new Image[RewardSlotCount];
        rewardLabels = new TextMeshProUGUI[RewardSlotCount];
        for (int i = 0; i < RewardSlotCount; i++)
        {
            float left = 0.025f + i * 0.245f;
            float right = left + 0.215f;
            Image rewardSlot = CreateImage($"Reward Slot {i + 1}", rewardContent, Color.clear);
            Anchor(rewardSlot.rectTransform, new Vector2(left, 0f), new Vector2(right, 1f));

            TextMeshProUGUI label = CreateText("Label", rewardSlot.transform, "奖励", 17f, FontStyles.Bold);
            Anchor(label.rectTransform, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.92f));
            label.color = new Color(0.19f, 0.105f, 0.04f, 1f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 11f;
            label.fontSizeMax = 17f;

            rewardSlots[i] = rewardSlot;
            rewardLabels[i] = label;
        }

        Image rewardFrame = CreateImage("Reward Preview Frame", layoutRoot, Color.white);
        Anchor(rewardFrame.rectTransform, new Vector2(0.515f, 0.20f), new Vector2(0.84f, 0.3225f));
        ApplyResourceSprite(rewardFrame, RewardFrameResource, false);

        quickAutoFillButton = CreateParchmentButton("Quick Auto Fill Button", layoutRoot, "自动编队");
        Anchor(quickAutoFillButton.GetComponent<RectTransform>(), new Vector2(0.29f, 0.135f), new Vector2(0.39f, 0.195f));

        quickClearButton = CreateParchmentButton("Quick Clear Button", layoutRoot, "清空编队");
        Anchor(quickClearButton.GetComponent<RectTransform>(), new Vector2(0.395f, 0.135f), new Vector2(0.495f, 0.195f));
    }

    private void ConfigureExistingControls(RectTransform startPanel)
    {
        string[] legacyPanels = { "Start Left Page", "Start Right Page" };
        foreach (string panelName in legacyPanels)
        {
            Transform legacyPanel = startPanel.Find(panelName);
            if (legacyPanel != null) legacyPanel.gameObject.SetActive(false);
        }

        Transform titleBlock = startPanel.Find("Start Title Block");
        if (titleBlock != null)
        {
            RectTransform titleRect = titleBlock as RectTransform;
            if (titleRect != null) Anchor(titleRect, new Vector2(0.20f, 0.025f), new Vector2(0.80f, 0.07f));
            Transform title = titleBlock.Find("Start Panel Title");
            if (title != null) title.gameObject.SetActive(false);
            TextMeshProUGUI body = FindText(titleBlock, "Start Panel Body Text");
            if (body != null)
            {
                Stretch(body.rectTransform);
                body.fontSize = 16f;
                body.color = new Color(0.25f, 0.15f, 0.08f, 0.92f);
                body.enableWordWrapping = false;
                body.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        Transform statusStrip = startPanel.Find("Start Status Strip");
        if (statusStrip != null) statusStrip.gameObject.SetActive(false);

        ConfigureStageSelector(startPanel.Find("Stage Selector") as RectTransform);
        Anchor(layoutRoot != null ? layoutRoot.Find("Formation Preview Content") as RectTransform : null, new Vector2(0.180f, 0.225f), new Vector2(0.495f, 0.805f));
        Anchor(layoutRoot != null ? layoutRoot.Find("Formation Preview Frame") as RectTransform : null, new Vector2(0.174f, 0.215f), new Vector2(0.501f, 0.815f));
        Anchor(layoutRoot != null ? layoutRoot.Find("Enemy Preview Content") as RectTransform : null, new Vector2(0.525f, 0.3475f), new Vector2(0.83f, 0.785f));
        Anchor(layoutRoot != null ? layoutRoot.Find("Enemy Preview Frame") as RectTransform : null, new Vector2(0.515f, 0.3325f), new Vector2(0.84f, 0.795f));
        Anchor(layoutRoot != null ? layoutRoot.Find("Reward Preview Content") as RectTransform : null, new Vector2(0.535f, 0.2125f), new Vector2(0.82f, 0.3025f));
        Anchor(layoutRoot != null ? layoutRoot.Find("Reward Preview Frame") as RectTransform : null, new Vector2(0.515f, 0.20f), new Vector2(0.84f, 0.3225f));
        ConfigureRewardFrame();
        Anchor(quickAutoFillButton != null ? quickAutoFillButton.GetComponent<RectTransform>() : null, new Vector2(0.29f, 0.135f), new Vector2(0.39f, 0.195f));
        Anchor(quickClearButton != null ? quickClearButton.GetComponent<RectTransform>() : null, new Vector2(0.395f, 0.135f), new Vector2(0.495f, 0.195f));
        StyleMainButton(FindButton(startPanel, "Open Formation Button"), "编队调整", new Vector2(0.18f, 0.135f), new Vector2(0.285f, 0.195f), new Color(0.94f, 0.80f, 0.58f, 0.96f), "编队");
        StyleMainButton(FindButton(startPanel, "Start Battle Button"), "开始战斗", new Vector2(0.58f, 0.125f), new Vector2(0.715f, 0.19f), new Color(0.88f, 0.43f, 0.14f, 0.98f));
    }

    private void ApplyBookPagePerspective(RectTransform startPanel)
    {
        if (layoutRoot == null || startPanel == null) return;
        RectTransform formationButtonRow = quickAutoFillButton != null ? quickAutoFillButton.transform as RectTransform : null;

        string[] leftPagePaths =
        {
            "Formation Title",
            "Formation Count Badge",
            "Formation Preview Content",
            "Formation Preview Frame",
            "Quick Auto Fill Button",
            "Quick Clear Button"
        };
        for (int i = 0; i < leftPagePaths.Length; i++)
        {
            Transform target = layoutRoot.Find(leftPagePaths[i]);
            RectTransform reference = target != null && target.GetComponent<Button>() != null ? formationButtonRow : null;
            IdleAdventureBookPagePerspective.ApplyToHierarchy(target, startPanel, false, reference);
        }

        string[] rightPagePaths =
        {
            "Stage Title Navigation",
            "Enemy Preview Content",
            "Enemy Preview Frame",
            "Reward Preview Content",
            "Reward Preview Frame"
        };
        for (int i = 0; i < rightPagePaths.Length; i++)
        {
            IdleAdventureBookPagePerspective.ApplyToHierarchy(layoutRoot.Find(rightPagePaths[i]), startPanel, true);
        }

        Button formationButton = FindButton(startPanel, "Open Formation Button");
        IdleAdventureBookPagePerspective.ApplyToHierarchy(formationButton != null ? formationButton.transform : null, startPanel, false, formationButtonRow);

        Button startButton = FindButton(startPanel, "Start Battle Button");
        IdleAdventureBookPagePerspective.ApplyToHierarchy(startButton != null ? startButton.transform : null, startPanel, true);

        Transform stageSelector = startPanel.Find("Stage Selector");
        IdleAdventureBookPagePerspective.ApplyToHierarchy(stageSelector, startPanel, true);
        if (stageSelector != null)
        {
            RectTransform navigation = layoutRoot.Find("Stage Title Navigation") as RectTransform;
            foreach (IdleAdventureBookPagePerspective effect in stageSelector.GetComponentsInChildren<IdleAdventureBookPagePerspective>(true))
                effect.Configure(startPanel, true, 0.4f, navigation);
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        ConfigureRewardFrame();
        ConfigureRewardRows();
    }

    private void OnEnable()
    {
        Canvas.preWillRenderCanvases += ConfigureRewardFrame;
    }

    private void OnDisable()
    {
        Canvas.preWillRenderCanvases -= ConfigureRewardFrame;
    }

    private void ConfigureRewardFrame()
    {
        Image frame = FindImage(layoutRoot, "Reward Preview Frame");
        if (frame != null) frame.gameObject.SetActive(false);
    }

    private void ConfigureRewardRows()
    {
        if (bookSkinReady)
        {
            ConfigureBookRewards();
            return;
        }
        RectTransform content = layoutRoot != null ? layoutRoot.Find("Reward Preview Content") as RectTransform : null;
        if (content == null || !HasRewardBindings()) return;

        int visibleCount = 0;
        foreach (Image slot in rewardSlots)
            if (slot.gameObject.activeSelf) visibleCount++;
        int rowCount = Mathf.Max(1, Mathf.CeilToInt(visibleCount / 2f));
        content.anchorMin = new Vector2(0.52f, 0.26f);
        content.anchorMax = new Vector2(0.84f, 0.26f);
        content.pivot = new Vector2(0.5f, 0.5f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 24f + rowCount * 36f + (rowCount - 1) * 6f);

        Image background = content.GetComponent<Image>();
        if (background == null) background = content.gameObject.AddComponent<Image>();
        StyleRewardSurface(background, new Color(0.84f, 0.69f, 0.44f, 0.12f));

        Image category = FindImage(content, "Reward Category");
        if (category == null) category = CreateImage("Reward Category", content, Color.white);
        StyleRewardSurface(category, new Color(0.55f, 0.42f, 0.23f, 0.94f));
        RectTransform categoryRect = category.rectTransform;
        Anchor(categoryRect, new Vector2(0.018f, 1f), new Vector2(0.18f, 1f));
        categoryRect.anchoredPosition = new Vector2(0f, -30f);
        categoryRect.sizeDelta = new Vector2(0f, 32f);
        TextMeshProUGUI categoryLabel = FindText(category.transform, "Label");
        if (categoryLabel == null) categoryLabel = CreateText("Label", category.transform, "通关奖励", 18f, FontStyles.Bold);
        Stretch(categoryLabel.rectTransform);
        categoryLabel.color = new Color(1f, 0.96f, 0.85f, 1f);
        categoryLabel.enableWordWrapping = false;
        categoryLabel.enableAutoSizing = true;
        categoryLabel.fontSizeMin = 14f;
        categoryLabel.fontSizeMax = 18f;
        categoryLabel.margin = new Vector4(5f, 0f, 5f, 0f);

        int visibleIndex = 0;
        float columnOffset = 0f;
        float availableWidth = Mathf.Max(1f, content.rect.width * 0.778f);
        float maximumChipWidth = visibleCount == 1 ? availableWidth : Mathf.Max(1f, (availableWidth - 8f) * 0.5f);
        foreach (Image slot in rewardSlots)
        {
            StyleRewardSurface(slot, new Color(1f, 0.97f, 0.89f, 0.82f));
            if (!slot.gameObject.activeSelf) continue;
            int column = visibleIndex % 2;
            int row = visibleIndex / 2;
            if (column == 0) columnOffset = 0f;
            TextMeshProUGUI label = FindText(slot.transform, "Label");
            float preferredWidth = label.GetPreferredValues(label.text, float.PositiveInfinity, 36f).x + label.rectTransform.offsetMin.x + 12f;
            float chipWidth = Mathf.Min(maximumChipWidth, Mathf.Max(126f, preferredWidth));
            Anchor(slot.rectTransform, new Vector2(0.20f, 1f), new Vector2(0.20f, 1f));
            slot.rectTransform.pivot = new Vector2(0f, 0.5f);
            slot.rectTransform.anchoredPosition = new Vector2(columnOffset, -30f - row * 42f);
            slot.rectTransform.sizeDelta = new Vector2(chipWidth, 36f);
            columnOffset += chipWidth + 8f;
            visibleIndex++;
        }
    }

    private static void StyleRewardSurface(Image image, Color color)
    {
        StyleCard(image);
        image.pixelsPerUnitMultiplier = 3f;
        image.color = color;
        image.raycastTarget = false;
        AddOutline(image, new Color(0.66f, 0.47f, 0.22f, 0.48f), new Vector2(0.75f, -0.75f));
    }

    private void RefreshResourceSprites()
    {
        if (layoutRoot == null) return;
        ApplyResourceSprite(FindImage(layoutRoot, "Formation Title"), FormationTitleResource, false);
        ApplyResourceSprite(FindImage(layoutRoot, "Formation Preview Content/Formation Preview Background"), FormationBackgroundResource, false);
        ApplyResourceSprite(FindImage(layoutRoot, "Formation Preview Frame"), PreviewFrameResource, false);
        ApplyResourceSprite(FindImage(layoutRoot, "Stage Title Navigation"), StageTitleResource, false);
        ApplyResourceSprite(FindImage(layoutRoot, "Enemy Preview Frame"), PreviewFrameResource, false);
        ApplyResourceSprite(FindImage(layoutRoot, "Reward Preview Frame"), RewardFrameResource, false);
    }

    private void ConfigurePreviewSurfaces()
    {
        foreach (RectMask2D mask in layoutRoot.GetComponentsInChildren<RectMask2D>(true)) mask.enabled = false;
        Image enemyBackground = FindImage(layoutRoot, "Enemy Preview Content/Enemy Preview Background");
        if (enemyBackground != null)
        {
            enemyBackground.sprite = null;
            enemyBackground.color = new Color(0.88f, 0.82f, 0.74f, 0.32f);
        }
        Image enemyShade = FindImage(layoutRoot, "Enemy Preview Content/Enemy Preview Shade");
        if (enemyShade != null) enemyShade.color = Color.clear;
        foreach (Image slot in formationSlots) ClearPreviewCardBackground(slot);
        foreach (TextMeshProUGUI label in formationSlotLabels) label.gameObject.SetActive(false);
        StyleCard(FindImage(layoutRoot, "Formation Count Badge"));
        StyleMainButton(quickAutoFillButton, "自动编队", new Vector2(0.29f, 0.135f), new Vector2(0.39f, 0.195f), new Color(0.94f, 0.80f, 0.58f, 1f));
        StyleMainButton(quickClearButton, "清空编队", new Vector2(0.395f, 0.135f), new Vector2(0.495f, 0.195f), new Color(0.94f, 0.80f, 0.58f, 1f), "一键清除");
    }

    private void EnsurePreviewIcons()
    {
        formationPortraits = new Image[FormationSlotCount];
        for (int slotIndex = 0; slotIndex < FormationSlotCount; slotIndex++)
        {
            Image portrait = FindImage(formationSlots[slotIndex].transform, "Portrait");
            if (portrait == null) portrait = CreateImage("Portrait", formationSlots[slotIndex].transform, Color.white);
            Anchor(portrait.rectTransform, new Vector2(0.08f, 0.35f), new Vector2(0.92f, 0.97f));
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            formationPortraits[slotIndex] = portrait;
            SetPreviewIcon(portrait, portrait.sprite);
        }
        rewardIcons = new Image[RewardSlotCount];
        for (int slotIndex = 0; slotIndex < RewardSlotCount; slotIndex++)
        {
            Image icon = FindImage(rewardSlots[slotIndex].transform, "Reward Icon");
            if (icon == null) icon = CreateImage("Reward Icon", rewardSlots[slotIndex].transform, Color.white);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            icon.rectTransform.pivot = new Vector2(0f, 0.5f);
            icon.rectTransform.anchoredPosition = new Vector2(6f, 0f);
            icon.rectTransform.sizeDelta = new Vector2(RewardIconSize, RewardIconSize);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            rewardIcons[slotIndex] = icon;
            SetPreviewIcon(icon, icon.sprite);
            ConfigureRewardLabel(rewardLabels[slotIndex], icon.sprite != null);
        }
    }

    private void ConfigureRewardLabel(TextMeshProUGUI label, bool hasIcon)
    {
        if (bookSkinReady)
        {
            StyleBookRewardLabel(label, hasIcon);
            return;
        }
        RectTransform rect = label.rectTransform;
        Stretch(rect);
        rect.offsetMin = new Vector2(hasIcon ? RewardIconSize + 12f : 8f, 0f);
        rect.offsetMax = new Vector2(-8f, 0f);
        label.alignment = TextAlignmentOptions.Left;
        label.fontSize = 18f;
        label.fontSizeMin = 13f;
        label.fontSizeMax = 18f;
        label.fontStyle = FontStyles.Normal;
        label.color = new Color(0.23f, 0.165f, 0.094f, 1f);
        label.enableAutoSizing = true;
        label.enableWordWrapping = false;
        label.richText = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
    }

    private static void SetPreviewIcon(Image image, Sprite sprite)
    {
        image.sprite = sprite;
        image.color = Color.white;
        image.gameObject.SetActive(sprite != null);
    }

    private Sprite ResolveRewardIcon(RewardDisplay reward)
    {
        if (bookSkinReady)
        {
            Sprite currency = ResolveBookCurrency(reward.ItemKey) ?? ResolveBookCurrency(reward.Name);
            if (currency != null) return currency;
        }
        Sprite sprite = LoadSprite(reward.IconKey) ?? LoadSprite(reward.ItemKey);
        if (sprite != null) return sprite;
        if (previewIcons == null) previewIcons = Resources.Load<IdleAdventurePreviewIconCatalog>("IdleAdventureUI/PreviewIcons");
        return previewIcons != null ? previewIcons.Find(reward.IconKey) ?? previewIcons.Find(reward.ItemKey) ?? previewIcons.Find(reward.Name) : null;
    }

    private static void ClearPreviewCardBackground(Image card)
    {
        if (card == null) return;
        card.color = Color.clear;
        Outline outline = card.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
    }

    private static void StyleCard(Image card)
    {
        if (card == null) return;
        card.sprite = Resources.Load<Sprite>("IdleAdventureUI/RoundedPanel");
        card.type = Image.Type.Sliced;
        card.pixelsPerUnitMultiplier = 1f;
    }

    private void ConfigureStageSelector(RectTransform selector)
    {
        if (selector == null) return;

        Anchor(selector, new Vector2(0.52f, 0.81f), new Vector2(0.835f, 0.93f));
        Image selectorImage = selector.GetComponent<Image>();
        if (selectorImage != null)
        {
            selectorImage.sprite = null;
            selectorImage.color = Color.clear;
            selectorImage.raycastTarget = false;
        }

        TextMeshProUGUI selectionText = FindText(selector, "Stage Selection Text");
        if (selectionText != null) selectionText.gameObject.SetActive(false);

        ConfigureArrowButton(FindButton(selector, "Previous Stage Button"), new Vector2(0f, 0f), new Vector2(0.15f, 1f));
        ConfigureArrowButton(FindButton(selector, "Next Stage Button"), new Vector2(0.85f, 0f), new Vector2(1f, 1f));
    }

    private static void ConfigureArrowButton(Button button, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (button == null) return;
        RectTransform rectTransform = button.GetComponent<RectTransform>();
        if (rectTransform != null) Anchor(rectTransform, anchorMin, anchorMax);

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = null;
            image.color = new Color(1f, 1f, 1f, 0.001f);
            image.raycastTarget = true;
            button.targetGraphic = image;
        }

        TMP_Text[] labels = button.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++) labels[i].gameObject.SetActive(false);
        Transform icon = button.transform.Find("SmallIcon");
        if (icon != null) icon.gameObject.SetActive(false);
    }

    private static void StyleMainButton(Button button, string labelText, Vector2 anchorMin, Vector2 anchorMax, Color backgroundColor, string iconResource = null)
    {
        if (button == null) return;
        RectTransform rectTransform = button.GetComponent<RectTransform>();
        if (rectTransform != null) Anchor(rectTransform, anchorMin, anchorMax);

        Image background = button.GetComponent<Image>();
        if (background != null)
        {
            background.sprite = null;
            StyleCard(background);
            background.color = backgroundColor;
            background.preserveAspect = false;
            background.raycastTarget = true;
            button.targetGraphic = background;
            AddOutline(background, new Color(0.30f, 0.15f, 0.055f, 1f), new Vector2(2f, -2f));
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.97f, 0.88f, 1f);
            colors.pressedColor = new Color(0.82f, 0.76f, 0.66f, 1f);
            colors.disabledColor = new Color(0.70f, 0.66f, 0.60f, 0.9f);
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;
        }

        Transform iconTransform = button.transform.Find("SmallIcon");
        if (iconTransform != null)
        {
            iconTransform.gameObject.SetActive(false);
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.gameObject.SetActive(true);
            label.text = FrontendTextLocalization.Normalize(labelText);
            label.font = RuntimeFontAssetProvider.Resolve();
            label.fontSize = 21f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.20f, 0.105f, 0.04f, 1f);
            label.raycastTarget = false;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label.enableAutoSizing = true;
            label.fontSizeMin = 16f;
            label.fontSizeMax = 23f;
            label.margin = new Vector4(8f, 2f, 8f, 2f);
        }

        if (!string.IsNullOrEmpty(iconResource))
        {
            Image icon = SmallButtonIconUtility.ApplyToButton(button, iconResource, 42f, 10f, 8f);
            if (icon != null) icon.gameObject.SetActive(true);
        }
    }

    private List<EnemyDisplay> BuildEnemyGroups(IdleAdventureStageEnemiesResponse preview)
    {
        List<EnemyDisplay> groups = new List<EnemyDisplay>();
        if (preview != null && preview.slots != null)
        {
            for (int i = 0; i < preview.slots.Length; i++)
            {
                IdleAdventureEnemySlotResponse slot = preview.slots[i];
                if (slot == null || slot.enemy == null) continue;

                string name = slot.enemy.ResolvedCombatantName;
                string key = slot.enemy.ResolvedCombatantKey;
                if (slot.enemy.ResolvedDefinitionId <= 0L && string.IsNullOrWhiteSpace(key) && string.IsNullOrWhiteSpace(name)) continue;
                if (string.IsNullOrWhiteSpace(name)) name = "敌人";
                string searchKey = (key + " " + name).Trim();
                string identity = slot.enemy.ResolvedDefinitionId > 0L ? slot.enemy.ResolvedDefinitionId.ToString() : searchKey;
                int level = Mathf.Max(0, slot.enemy.level);

                EnemyDisplay existing = null;
                for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
                {
                    if (groups[groupIndex].Level == level && string.Equals(groups[groupIndex].Identity, identity, StringComparison.OrdinalIgnoreCase))
                    {
                        existing = groups[groupIndex];
                        break;
                    }
                }

                if (existing != null) existing.Count++;
                else groups.Add(new EnemyDisplay(name, searchKey, identity, slot.enemy.icon, level, 1));
            }
        }

        return groups;
    }

    private void LayoutEnemyGroups(int count)
    {
        if (enemyImages == null) return;
        int visibleCount = Mathf.Clamp(count, 1, EnemyGroupCount);
        for (int i = 0; i < enemyImages.Length; i++)
        {
            if (enemyImages[i] == null || enemyImages[i].transform.parent == null) continue;
            RectTransform group = enemyImages[i].transform.parent as RectTransform;
            if (group == null) continue;

            if (visibleCount == 1)
            {
                Anchor(group, new Vector2(0.32f, 0.06f), new Vector2(0.68f, 0.86f));
            }
            else if (visibleCount == 2)
            {
                float left = i == 0 ? 0.14f : 0.54f;
                Anchor(group, new Vector2(left, 0.06f), new Vector2(left + 0.32f, 0.86f));
            }
            else
            {
                float left = 0.035f + (i % 3) * 0.325f;
                float top = visibleCount <= 3 ? 0.86f : 0.86f - (i / 3) * 0.41f;
                float bottom = visibleCount <= 3 ? 0.06f : top - 0.39f;
                Anchor(group, new Vector2(left, bottom), new Vector2(left + 0.28f, top));
            }
        }
    }

    private Sprite ResolveEnemySprite(string searchKey)
    {
        string normalized = (searchKey ?? string.Empty).ToLowerInvariant();
        if (normalized.Contains("forest_slime") || normalized.Contains("森林史莱姆"))
        {
            return LoadSprite(ForestSlimeResource);
        }
        if (normalized.Contains("flame_raider") || normalized.Contains("fire_raider") || normalized.Contains("烈焰掠夺者"))
        {
            return LoadSprite(FlameRaiderResource);
        }
        return LoadSprite("UI/ApprovedUi/Controls/11_unknown_enemy_candidate");
    }

    private void MoveLayoutBelowFormationWindow(RectTransform startPanel)
    {
        if (layoutRoot == null || startPanel == null) return;
        Transform formationWindow = startPanel.Find("Formation Window");
        if (formationWindow != null && layoutRoot.GetSiblingIndex() > formationWindow.GetSiblingIndex())
        {
            layoutRoot.SetSiblingIndex(formationWindow.GetSiblingIndex());
        }
    }

    private void RemoveIncompleteLayout(RectTransform startPanel)
    {
        Transform existing = startPanel.Find(LayoutRootName);
        if (existing == null) return;
        RemoveLayout(existing);
    }

    private void RemoveDuplicateLayouts(RectTransform startPanel)
    {
        for (int childIndex = startPanel.childCount - 1; childIndex >= 0; childIndex--)
        {
            Transform child = startPanel.GetChild(childIndex);
            if (child != layoutRoot && child.name == LayoutRootName) RemoveLayout(child);
        }
    }

    private static void RemoveLayout(Transform existing)
    {
        existing.gameObject.SetActive(false);
        existing.SetParent(null, false);
        if (Application.isPlaying) Destroy(existing.gameObject);
        else DestroyImmediate(existing.gameObject);
    }

    private bool HasFormationBindings()
    {
        return formationCountText != null
            && HasBindings(formationSlots, FormationSlotCount)
            && HasBindings(formationSlotLabels, FormationSlotCount);
    }

    private bool HasEnemyBindings()
    {
        return enemySummaryText != null
            && HasBindings(enemyImages, EnemyGroupCount)
            && HasBindings(enemyLabels, EnemyGroupCount);
    }

    private bool HasRewardBindings()
    {
        return HasBindings(rewardSlots, RewardSlotCount)
            && HasBindings(rewardLabels, RewardSlotCount);
    }

    private static bool HasBindings<T>(T[] values, int count) where T : UnityEngine.Object
    {
        if (values == null || values.Length < count) return false;
        for (int i = 0; i < count; i++)
        {
            if (values[i] == null) return false;
        }
        return true;
    }

    private Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject target = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        target.transform.SetParent(parent, false);
        Image image = target.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject target = new GameObject(objectName, typeof(RectTransform));
        target.transform.SetParent(parent, false);
        return target.GetComponent<RectTransform>();
    }

    private TextMeshProUGUI CreateText(string objectName, Transform parent, string content, float fontSize, FontStyles fontStyle)
    {
        GameObject target = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        target.transform.SetParent(parent, false);
        TextMeshProUGUI text = target.GetComponent<TextMeshProUGUI>();
        text.font = RuntimeFontAssetProvider.Resolve();
        text.text = FrontendTextLocalization.Normalize(content);
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.19f, 0.105f, 0.04f, 1f);
        text.raycastTarget = false;
        text.enableWordWrapping = true;
        return text;
    }

    private Button CreateParchmentButton(string objectName, Transform parent, string labelText)
    {
        Image background = CreateImage(objectName, parent, new Color(0.94f, 0.80f, 0.58f, 0.96f));
        background.raycastTarget = true;
        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        AddOutline(background, new Color(0.30f, 0.15f, 0.055f, 1f), new Vector2(2f, -2f));

        TextMeshProUGUI label = CreateText("Label", background.transform, labelText, 20f, FontStyles.Bold);
        Stretch(label.rectTransform);
        return button;
    }

    private void ApplyResourceSprite(Image image, string resourcePath, bool preserveAspect)
    {
        if (image == null) return;
        image.sprite = LoadSprite(resourcePath);
        image.color = image.sprite != null ? Color.white : Color.clear;
        image.type = Image.Type.Simple;
        image.preserveAspect = preserveAspect;
        image.raycastTarget = false;
    }

    private Sprite LoadSprite(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath)) return null;
        resourcePath = resourcePath.Trim().Replace('\\', '/');
        int resourcesIndex = resourcePath.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase);
        if (resourcesIndex >= 0) resourcePath = resourcePath.Substring(resourcesIndex + 11);
        if (resourcePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) resourcePath = resourcePath.Substring(0, resourcePath.Length - 4);
        if (spriteCache.TryGetValue(resourcePath, out Sprite cachedSprite) && cachedSprite != null) return cachedSprite;

        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null)
        {
            spriteCache[resourcePath] = sprite;
            return sprite;
        }

        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null) return null;
        Sprite runtimeSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        runtimeSprite.name = texture.name + " Runtime Sprite";
        runtimeSprites.Add(runtimeSprite);
        spriteCache[resourcePath] = runtimeSprite;
        return runtimeSprite;
    }

    private static Image FindImage(Transform root, string childPath)
    {
        Transform target = root != null ? root.Find(childPath) : null;
        return target != null ? target.GetComponent<Image>() : null;
    }

    private static TextMeshProUGUI FindText(Transform root, string childPath)
    {
        Transform target = root != null ? root.Find(childPath) : null;
        return target != null ? target.GetComponent<TextMeshProUGUI>() : null;
    }

    private static Button FindButton(Transform root, string childPath)
    {
        Transform target = root != null ? root.Find(childPath) : null;
        return target != null ? target.GetComponent<Button>() : null;
    }

    private static void BindButton(Button button, Action action)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        if (action != null) button.onClick.AddListener(() => action());
    }

    private static void AddOutline(Graphic graphic, Color color, Vector2 distance)
    {
        if (graphic == null) return;
        Outline outline = graphic.GetComponent<Outline>();
        if (outline == null) outline = graphic.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    private static void Stretch(RectTransform rectTransform)
    {
        if (rectTransform == null) return;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void Anchor(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (rectTransform == null) return;
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private sealed class EnemyDisplay
    {
        public readonly string Name;
        public readonly string SearchKey;
        public readonly string Identity;
        public readonly string IconKey;
        public readonly int Level;
        public int Count;

        public EnemyDisplay(string name, string searchKey, string identity, string iconKey, int level, int count)
        {
            Name = name;
            SearchKey = searchKey;
            Identity = identity;
            IconKey = iconKey;
            Level = level;
            Count = count;
        }
    }

    private sealed class RewardDisplay
    {
        public readonly string Name;
        public readonly long Quantity;
        public readonly string ItemKey;
        public readonly string IconKey;

        public RewardDisplay(string name, long quantity, string itemKey, string iconKey)
        {
            Name = name;
            Quantity = quantity;
            ItemKey = itemKey;
            IconKey = iconKey;
        }
    }
}
