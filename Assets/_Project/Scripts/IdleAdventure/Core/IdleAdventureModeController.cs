using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[ExecuteAlways]
public sealed class IdleAdventureModeController : MonoBehaviour
{
    private const int FormationRows = 3;
    private const int FormationColumns = 2;
    private const int MaxPartySize = FormationRows * FormationColumns;
    private const int FriendlySkillSlotCount = 2;
    private const int EnemySkillSlotCount = 1;
    private const int MaxBuffIconSlots = 6;
    private const float AttributeRestraintMultiplier = 0.2f;
    private const string BackgroundCanvasName = "Idle Adventure Background Canvas";
    private const string StartCanvasName = "Idle Adventure Start Canvas";
    private const string HudCanvasName = "Idle Adventure Canvas";
    private const string AfkRewardCanvasName = "Idle Adventure AFK Reward Canvas";

    [Header("Mode Config")]
    [SerializeField] private IdleAdventureModeConfig modeConfig;

    [Header("Party Size")]
    [SerializeField, Range(1, MaxPartySize)] private int startingFriendlyCount = 3;
    [SerializeField, Range(1, MaxPartySize)] private int baseEnemyCount = 2;
    [SerializeField, Min(0)] private int addEnemyEveryStages = 2;

    [Header("Friendly Stats")]
    [SerializeField, Min(1)] private int friendlyBaseHealth = 160;
    [SerializeField, Min(0)] private int friendlyBaseAttack = 28;
    [SerializeField, Min(0)] private int friendlyBaseDefense = 5;
    [SerializeField, Min(1)] private int friendlyBaseSpeed = 12;

    [Header("Enemy Stats")]
    [SerializeField, Min(1)] private int enemyBaseHealth = 95;
    [SerializeField, Min(0)] private int enemyBaseAttack = 15;
    [SerializeField, Min(0)] private int enemyBaseDefense = 1;
    [SerializeField, Min(1)] private int enemyBaseSpeed = 9;
    [SerializeField, Min(0)] private int enemyHealthPerStage = 22;
    [SerializeField, Min(0)] private int enemyAttackPerStage = 4;
    [SerializeField, Min(0)] private int enemySpeedPerStage = 1;

    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float turnInterval = 0.75f;
    [SerializeField, Min(0.05f)] private float attackAnimationDuration = 0.28f;
    [SerializeField, Min(0.05f)] private float hitFlashDuration = 0.18f;

    [Header("Combatant Prefabs")]
    [SerializeField] private IdleAdventureCombatant friendlyCombatantPrefab;
    [SerializeField] private IdleAdventureCombatant enemyCombatantPrefab;

    [Header("Runtime Parents")]
    [SerializeField] private Transform combatantRootOverride;

    [Header("Rewards")]
    [SerializeField] private IdleAdventureRewardLedger rewardLedger;
    [SerializeField] private IdleAdventureAfkRewardSource afkRewardSource;

    [Header("Progress")]
    [SerializeField] private IdleAdventureStageProgress stageProgress;

    private readonly Queue<string> battleLog = new Queue<string>();
    private readonly List<IdleAdventureCombatant> friendlies = new List<IdleAdventureCombatant>(MaxPartySize);
    private readonly List<IdleAdventureCombatant> enemies = new List<IdleAdventureCombatant>(MaxPartySize);
    private readonly IdleAdventureActorView[] friendlyViews = new IdleAdventureActorView[MaxPartySize];
    private readonly IdleAdventureActorView[] enemyViews = new IdleAdventureActorView[MaxPartySize];
    private readonly int[] friendlyFormation = new int[MaxPartySize];
    private readonly List<int> clearedRewardStages = new List<int>();
    private readonly Dictionary<string, int> pendingRunRewards = new Dictionary<string, int>();
    private readonly Dictionary<string, int> grantedRewards = new Dictionary<string, int>();
    private readonly Dictionary<string, string> rewardDisplayNames = new Dictionary<string, string>();

    private Canvas canvas;
    private TextMeshProUGUI stageText;
    private TextMeshProUGUI turnText;
    private TextMeshProUGUI friendlyCountText;
    private TextMeshProUGUI enemyCountText;
    private TextMeshProUGUI logText;
    private TextMeshProUGUI resultText;
    private Button pauseButton;
    private TextMeshProUGUI pauseButtonText;
    private Canvas backgroundCanvas;
    private Canvas startCanvas;
    private Canvas afkRewardCanvas;
    private Transform combatantRoot;
    private IdleAdventureBattleHud battleHudView;
    private IdleAdventureStartPanel startPanelView;
    private IdleAdventureStageEndPanel stageEndPanelView;
    private IdleAdventureFormationPanel formationPanelView;
    private bool interfaceBuildInProgress;

    private int stage = 1;
    private int round = 1;
    private int pendingNextStage = -1;
    private bool battleStarted;
    private bool stageChoicePending;
    private bool stageFailedPending;
    private bool modePausedByChoice;
    private bool paused;
    private Coroutine battleRoutine;

    public IReadOnlyDictionary<string, int> GrantedRewards => grantedRewards;
    public IReadOnlyDictionary<string, int> PendingRunRewards => pendingRunRewards;

    private void Awake()
    {
        ApplyModeConfig();
        ConfigureCamera();
        EnsureEventSystem();
        BuildInterface();
        EnsureCombatantRoot();
        EnsureRewardLedger();
        EnsureStageProgress();
        EnsureAfkRewardSource();
        ResetToStartScreen();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (Application.isPlaying) return;

        UnityEditor.EditorApplication.delayCall -= EnsurePersistentSceneComponentsInEditMode;
        UnityEditor.EditorApplication.delayCall += EnsurePersistentSceneComponentsInEditMode;
#endif
    }

#if UNITY_EDITOR
    private void EnsurePersistentSceneComponentsInEditMode()
    {
        if (this == null || Application.isPlaying) return;

        ApplyModeConfig();
        ConfigureCamera();
        EnsureEventSystem();
        BuildInterface();
        EnsureCombatantRoot();
        EnsureRewardLedger();
        EnsureStageProgress();
        EnsureAfkRewardSource();
        ResetToStartScreen();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif

    [ContextMenu("Rebuild Persistent Scene Components")]
    private void RebuildPersistentSceneComponents()
    {
        ClearGeneratedInterface();
        BuildInterface();
        EnsureCombatantRoot();
        EnsureRewardLedger();
        EnsureStageProgress();
        EnsureAfkRewardSource();
        ResetToStartScreen();
    }

    private void ApplyModeConfig()
    {
        if (modeConfig == null) return;

        startingFriendlyCount = Mathf.Clamp(modeConfig.StartingFriendlyCount, 1, MaxPartySize);
        baseEnemyCount = Mathf.Clamp(modeConfig.BaseEnemyCount, 1, MaxPartySize);
        addEnemyEveryStages = Mathf.Max(0, modeConfig.AddEnemyEveryStages);
        friendlyBaseHealth = Mathf.Max(1, modeConfig.FriendlyBaseHealth);
        friendlyBaseAttack = Mathf.Max(0, modeConfig.FriendlyBaseAttack);
        friendlyBaseDefense = Mathf.Max(0, modeConfig.FriendlyBaseDefense);
        friendlyBaseSpeed = Mathf.Max(1, modeConfig.FriendlyBaseSpeed);
        enemyBaseHealth = Mathf.Max(1, modeConfig.EnemyBaseHealth);
        enemyBaseAttack = Mathf.Max(0, modeConfig.EnemyBaseAttack);
        enemyBaseDefense = Mathf.Max(0, modeConfig.EnemyBaseDefense);
        enemyBaseSpeed = Mathf.Max(1, modeConfig.EnemyBaseSpeed);
        enemyHealthPerStage = Mathf.Max(0, modeConfig.EnemyHealthPerStage);
        enemyAttackPerStage = Mathf.Max(0, modeConfig.EnemyAttackPerStage);
        enemySpeedPerStage = Mathf.Max(0, modeConfig.EnemySpeedPerStage);
        turnInterval = Mathf.Max(0.1f, modeConfig.TurnInterval);
        attackAnimationDuration = Mathf.Max(0.05f, modeConfig.AttackAnimationDuration);
        hitFlashDuration = Mathf.Max(0.05f, modeConfig.HitFlashDuration);
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;

        ApplyModeConfig();
        ConfigureCamera();
        EnsureEventSystem();
        BuildInterface();
        EnsureCombatantRoot();
        EnsureRewardLedger();
        EnsureStageProgress();
        EnsureAfkRewardSource();
        ResetToStartScreen();

        if (battleRoutine != null)
        {
            StopCoroutine(battleRoutine);
            battleRoutine = null;
        }

        battleRoutine = StartCoroutine(BattleLoop());
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;

        if (battleRoutine != null)
        {
            StopCoroutine(battleRoutine);
            battleRoutine = null;
        }
    }

    public void SetFriendlyPartySize(int count)
    {
        startingFriendlyCount = Mathf.Clamp(count, 1, MaxPartySize);
        InitializeDefaultFormation();
        BuildFriendlyParty(startingFriendlyCount);
        round = 1;
        AddLog($"Friendly party size changed to {DeployedCount(friendlies)}.");
        RefreshFormationView();
        RefreshBattleView();
    }

    public void SetEnemyPartySize(int count)
    {
        BuildEnemyParty(Mathf.Clamp(count, 1, MaxPartySize));
        round = 1;
        AddLog($"Enemy party size changed to {DeployedCount(enemies)}.");
        RefreshBattleView();
    }

    public void AutoFillFormation()
    {
        InitializeDefaultFormation();
        RefreshFormationView();
        UpdateButtonStates();
    }

    public void ClearFormation()
    {
        for (int i = 0; i < MaxPartySize; i++)
        {
            friendlyFormation[i] = -1;
        }

        RefreshFormationView();
        UpdateButtonStates();
    }

    public void OpenFormationPanel()
    {
        if (formationPanelView == null) return;

        formationPanelView.ShowFormationWindow();
        RefreshFormationView();
    }

    public void CloseFormationPanel()
    {
        if (formationPanelView == null) return;

        formationPanelView.HideFormationWindow();
        RefreshFormationView();
    }

    public void AssignFormationSlot(int slotIndex, int definitionIndex)
    {
        if (slotIndex < 0 || slotIndex >= MaxPartySize) return;
        if (definitionIndex < 0 || definitionIndex >= AvailableFriendlyDefinitionCount()) return;

        for (int i = 0; i < MaxPartySize; i++)
        {
            if (friendlyFormation[i] == definitionIndex) friendlyFormation[i] = -1;
        }

        friendlyFormation[slotIndex] = definitionIndex;
        RefreshFormationView();
        UpdateButtonStates();
    }

    public void ClearFormationSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MaxPartySize) return;

        friendlyFormation[slotIndex] = -1;
        RefreshFormationView();
        UpdateButtonStates();
    }

    public void ApplyAttackBuffToFriendlySlot(int slotIndex)
    {
        ApplySkillNormalDamageBuffToFriendlySlot(slotIndex, "normal_damage_up", "Damage Up", true, 3, 0.15f);
    }

    public void ApplyDefenseDebuffToEnemySlot(int slotIndex)
    {
        ApplySkillStatBuffToEnemySlot(slotIndex, "defense_down", "Defense Down", false, 3, 0, -4, 0);
    }

    public bool ApplyBuffPresetToFriendlySlot(int slotIndex, int presetIndex)
    {
        return ApplyBuffPresetToParty(friendlies, slotIndex, presetIndex);
    }

    public bool ApplyBuffPresetToEnemySlot(int slotIndex, int presetIndex)
    {
        return ApplyBuffPresetToParty(enemies, slotIndex, presetIndex);
    }

    public bool ApplyStatBuffToFriendlySlot(int slotIndex, string buffId, string displayName, bool isBenefit, int durationRounds, int attackModifier, int defenseModifier, int speedModifier)
    {
        return ApplySkillStatBuffToFriendlySlot(slotIndex, buffId, displayName, isBenefit, durationRounds, attackModifier, defenseModifier, speedModifier);
    }

    public bool ApplyStatBuffToEnemySlot(int slotIndex, string buffId, string displayName, bool isBenefit, int durationRounds, int attackModifier, int defenseModifier, int speedModifier)
    {
        return ApplySkillStatBuffToEnemySlot(slotIndex, buffId, displayName, isBenefit, durationRounds, attackModifier, defenseModifier, speedModifier);
    }

    public bool ApplySkillStatBuffToFriendlySlot(int slotIndex, string buffId, string displayName, bool isBenefit, int durationRounds, int attackModifier, int defenseModifier, int speedModifier)
    {
        return ApplyStatBuffToParty(friendlies, slotIndex, buffId, displayName, isBenefit, IdleAdventureBuffSource.CharacterSkill, durationRounds, attackModifier, defenseModifier, speedModifier);
    }

    public bool ApplySkillStatBuffToEnemySlot(int slotIndex, string buffId, string displayName, bool isBenefit, int durationRounds, int attackModifier, int defenseModifier, int speedModifier)
    {
        return ApplyStatBuffToParty(enemies, slotIndex, buffId, displayName, isBenefit, IdleAdventureBuffSource.CharacterSkill, durationRounds, attackModifier, defenseModifier, speedModifier);
    }

    public bool ApplyOtherStatBuffToFriendlySlot(int slotIndex, string buffId, string displayName, bool isBenefit, int durationRounds, int attackModifier, int defenseModifier, int speedModifier)
    {
        return ApplyStatBuffToParty(friendlies, slotIndex, buffId, displayName, isBenefit, IdleAdventureBuffSource.Other, durationRounds, attackModifier, defenseModifier, speedModifier);
    }

    public bool ApplyOtherStatBuffToEnemySlot(int slotIndex, string buffId, string displayName, bool isBenefit, int durationRounds, int attackModifier, int defenseModifier, int speedModifier)
    {
        return ApplyStatBuffToParty(enemies, slotIndex, buffId, displayName, isBenefit, IdleAdventureBuffSource.Other, durationRounds, attackModifier, defenseModifier, speedModifier);
    }

    public bool ApplySkillNormalDamageBuffToFriendlySlot(int slotIndex, string buffId, string displayName, bool isBenefit, int durationRounds, float multiplier)
    {
        return ApplyDamageMultiplierBuffToParty(friendlies, slotIndex, buffId, displayName, isBenefit, IdleAdventureBuffSource.CharacterSkill, IdleAdventureBuffEffectType.NormalDamage, durationRounds, multiplier, 0f, IdleAdventureDamageAttribute.None);
    }

    public bool ApplySkillNormalDamageBuffToEnemySlot(int slotIndex, string buffId, string displayName, bool isBenefit, int durationRounds, float multiplier)
    {
        return ApplyDamageMultiplierBuffToParty(enemies, slotIndex, buffId, displayName, isBenefit, IdleAdventureBuffSource.CharacterSkill, IdleAdventureBuffEffectType.NormalDamage, durationRounds, multiplier, 0f, IdleAdventureDamageAttribute.None);
    }

    public bool ApplyOtherNormalDamageBuffToFriendlySlot(int slotIndex, string buffId, string displayName, bool isBenefit, int durationRounds, float multiplier)
    {
        return ApplyDamageMultiplierBuffToParty(friendlies, slotIndex, buffId, displayName, isBenefit, IdleAdventureBuffSource.Other, IdleAdventureBuffEffectType.NormalDamage, durationRounds, multiplier, 0f, IdleAdventureDamageAttribute.None);
    }

    public bool ApplyOtherNormalDamageBuffToEnemySlot(int slotIndex, string buffId, string displayName, bool isBenefit, int durationRounds, float multiplier)
    {
        return ApplyDamageMultiplierBuffToParty(enemies, slotIndex, buffId, displayName, isBenefit, IdleAdventureBuffSource.Other, IdleAdventureBuffEffectType.NormalDamage, durationRounds, multiplier, 0f, IdleAdventureDamageAttribute.None);
    }

    public bool ApplySkillAttributeDamageBuffToFriendlySlot(int slotIndex, string buffId, string displayName, bool isBenefit, int durationRounds, float multiplier, IdleAdventureDamageAttribute affectedAttribute)
    {
        return ApplyDamageMultiplierBuffToParty(friendlies, slotIndex, buffId, displayName, isBenefit, IdleAdventureBuffSource.CharacterSkill, IdleAdventureBuffEffectType.AttributeDamage, durationRounds, 0f, multiplier, affectedAttribute);
    }

    public bool ApplySkillAttributeDamageBuffToEnemySlot(int slotIndex, string buffId, string displayName, bool isBenefit, int durationRounds, float multiplier, IdleAdventureDamageAttribute affectedAttribute)
    {
        return ApplyDamageMultiplierBuffToParty(enemies, slotIndex, buffId, displayName, isBenefit, IdleAdventureBuffSource.CharacterSkill, IdleAdventureBuffEffectType.AttributeDamage, durationRounds, 0f, multiplier, affectedAttribute);
    }

    public bool ApplyOtherAttributeDamageBuffToFriendlySlot(int slotIndex, string buffId, string displayName, bool isBenefit, int durationRounds, float multiplier, IdleAdventureDamageAttribute affectedAttribute)
    {
        return ApplyDamageMultiplierBuffToParty(friendlies, slotIndex, buffId, displayName, isBenefit, IdleAdventureBuffSource.Other, IdleAdventureBuffEffectType.AttributeDamage, durationRounds, 0f, multiplier, affectedAttribute);
    }

    public bool ApplyOtherAttributeDamageBuffToEnemySlot(int slotIndex, string buffId, string displayName, bool isBenefit, int durationRounds, float multiplier, IdleAdventureDamageAttribute affectedAttribute)
    {
        return ApplyDamageMultiplierBuffToParty(enemies, slotIndex, buffId, displayName, isBenefit, IdleAdventureBuffSource.Other, IdleAdventureBuffEffectType.AttributeDamage, durationRounds, 0f, multiplier, affectedAttribute);
    }

    private bool ApplyStatBuffToParty(List<IdleAdventureCombatant> party, int slotIndex, string buffId, string displayName, bool isBenefit, IdleAdventureBuffSource source, int durationRounds, int attackModifier, int defenseModifier, int speedModifier)
    {
        IdleAdventureBuffCategory category = isBenefit ? IdleAdventureBuffCategory.Benefit : IdleAdventureBuffCategory.Detriment;
        IdleAdventureBattleBuff buff = IdleAdventureBattleBuff.StatBuff(buffId, displayName, category, source, durationRounds, attackModifier, defenseModifier, speedModifier);
        return ApplyRuntimeBuffToParty(party, slotIndex, buff);
    }

    private bool ApplyDamageMultiplierBuffToParty(List<IdleAdventureCombatant> party, int slotIndex, string buffId, string displayName, bool isBenefit, IdleAdventureBuffSource source, IdleAdventureBuffEffectType effectType, int durationRounds, float normalDamageMultiplier, float attributeDamageMultiplier, IdleAdventureDamageAttribute affectedAttribute)
    {
        IdleAdventureBuffCategory category = isBenefit ? IdleAdventureBuffCategory.Benefit : IdleAdventureBuffCategory.Detriment;
        float sign = isBenefit ? 1f : -1f;
        IdleAdventureBattleBuff buff = IdleAdventureBattleBuff.DamageMultiplierBuff(buffId, displayName, category, source, effectType, durationRounds, Mathf.Abs(normalDamageMultiplier) * sign, Mathf.Abs(attributeDamageMultiplier) * sign, affectedAttribute);
        return ApplyRuntimeBuffToParty(party, slotIndex, buff);
    }

    private bool ApplyBuffPresetToParty(List<IdleAdventureCombatant> party, int slotIndex, int presetIndex)
    {
        if (modeConfig == null || modeConfig.BuffPresets == null || presetIndex < 0 || presetIndex >= modeConfig.BuffPresets.Length) return false;
        IdleAdventureBuffDefinition preset = modeConfig.BuffPresets[presetIndex];
        if (preset == null) return false;

        return ApplyRuntimeBuffToParty(party, slotIndex, preset.CreateRuntimeBuff());
    }

    private bool ApplyRuntimeBuffToParty(List<IdleAdventureCombatant> party, int slotIndex, IdleAdventureBattleBuff buff)
    {
        if (buff == null) return false;

        if (TryApplyBuff(party, slotIndex, buff))
        {
            string verb = buff.Category == IdleAdventureBuffCategory.Benefit ? "gained" : "suffered";
            AddLog($"{party[slotIndex].CombatantName} {verb} {buff.DisplayName} from {buff.SourceLabel}.");
            RefreshBattleView();
            return true;
        }

        return false;
    }

    private void ConfigureCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            mainCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }

        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = new Color(0.08f, 0.09f, 0.11f);
        mainCamera.orthographic = true;
        mainCamera.orthographicSize = 5f;
        mainCamera.transform.position = new Vector3(0f, 0f, -10f);
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private void EnsureCombatantRoot()
    {
        if (combatantRoot != null) return;
        if (combatantRootOverride != null)
        {
            combatantRoot = combatantRootOverride;
            return;
        }

        Transform existingRoot = transform.Find("Combatants");
        if (existingRoot != null)
        {
            combatantRoot = existingRoot;
            return;
        }

        GameObject rootObject = new GameObject("Combatants");
        rootObject.transform.SetParent(transform, false);
        combatantRoot = rootObject.transform;
    }

    private void EnsureRewardLedger()
    {
        if (rewardLedger != null) return;

        Transform existingLedger = transform.Find("Idle Adventure Reward Ledger");
        if (existingLedger != null)
        {
            rewardLedger = existingLedger.GetComponent<IdleAdventureRewardLedger>();
            if (rewardLedger != null) return;
        }

        IdleAdventureRewardLedger existingComponent = GetComponentInChildren<IdleAdventureRewardLedger>(true);
        if (existingComponent != null)
        {
            rewardLedger = existingComponent;
            return;
        }

        GameObject ledgerObject = new GameObject("Idle Adventure Reward Ledger");
        ledgerObject.transform.SetParent(transform, false);
        rewardLedger = ledgerObject.AddComponent<IdleAdventureRewardLedger>();
    }

    private void EnsureStageProgress()
    {
        if (stageProgress != null) return;

        Transform existingProgress = transform.Find("Idle Adventure Stage Progress");
        if (existingProgress != null)
        {
            stageProgress = existingProgress.GetComponent<IdleAdventureStageProgress>();
            if (stageProgress != null) return;
        }

        IdleAdventureStageProgress existingComponent = GetComponentInChildren<IdleAdventureStageProgress>(true);
        if (existingComponent != null)
        {
            stageProgress = existingComponent;
            return;
        }

        GameObject progressObject = new GameObject("Idle Adventure Stage Progress");
        progressObject.transform.SetParent(transform, false);
        stageProgress = progressObject.AddComponent<IdleAdventureStageProgress>();
    }

    private void EnsureAfkRewardSource()
    {
        EnsureRewardLedger();
        EnsureStageProgress();

        Canvas rewardCanvas = EnsureAfkRewardCanvas();
        if (rewardCanvas == null) return;

        IdleAdventureAfkRewardSource existingSource = afkRewardSource != null ? afkRewardSource : rewardCanvas.GetComponentInChildren<IdleAdventureAfkRewardSource>(true);
        if (existingSource != null && existingSource.HasViewBindings)
        {
            afkRewardSource = existingSource;
            afkRewardSource.SetRewardLedger(rewardLedger);
            afkRewardSource.SetHighestClearedStage(HighestClearedStage());
            return;
        }

        if (existingSource != null) DestroyRuntimeObject(existingSource.gameObject);
        BuildAfkRewardWidget(rewardCanvas.GetComponent<RectTransform>());
    }

    private void BuildAfkRewardWidget(RectTransform root)
    {
        if (root == null) return;

        RectTransform panel = CreatePanel("AFK Reward Widget", root, new Color(0.10f, 0.12f, 0.14f, 0.94f));
        Anchor(panel, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 28f), new Vector2(198f, 168f));

        Image iconImage = CreateImage("Reward Icon Button", panel, new Color(0.32f, 0.36f, 0.40f, 1f));
        Anchor(iconImage.rectTransform, new Vector2(0f, 0.22f), new Vector2(0.58f, 1f), new Vector2(12f, -10f), new Vector2(-8f, -12f));
        Button collectButton = iconImage.gameObject.AddComponent<Button>();
        collectButton.targetGraphic = iconImage;

        Image progressFill = CreateImage("Progress Fill", iconImage.transform, new Color(0.42f, 0.78f, 0.46f, 0.92f));
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = 0;
        progressFill.fillAmount = 0f;
        progressFill.raycastTarget = false;
        Anchor(progressFill.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.16f), Vector2.zero, Vector2.zero);

        TextMeshProUGUI iconText = CreateText("Icon Text", iconImage.transform, "G", 34, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(iconText.rectTransform);

        TextMeshProUGUI amountText = CreateText("Amount Text", panel, "x0", 21, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(amountText.rectTransform, new Vector2(0.58f, 0.50f), new Vector2(1f, 0.88f), new Vector2(0f, 0f), new Vector2(-10f, 0f));

        TextMeshProUGUI timerText = CreateText("Timer Text", panel, "30s", 16, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(timerText.rectTransform, new Vector2(0.58f, 0.18f), new Vector2(1f, 0.50f), new Vector2(0f, 0f), new Vector2(-10f, 0f));

        afkRewardSource = panel.gameObject.AddComponent<IdleAdventureAfkRewardSource>();
        afkRewardSource.Initialize(rewardLedger, collectButton, iconImage, progressFill, iconText, amountText, timerText);
        afkRewardSource.SetHighestClearedStage(HighestClearedStage());
    }

    private void BuildInterface()
    {
        if (interfaceBuildInProgress) return;
        interfaceBuildInProgress = true;

        try
        {
            IdleAdventureBattleHud existingHud = ResolveExistingBattleHud();
            if (existingHud != null && existingHud.HasBindings)
            {
                EnsureBattleBackgroundCanvas();
                EnsureStartCanvas();
                BindBattleHud(existingHud);
                EnsurePersistentPanels(existingHud.Root);
                return;
            }

            ClearGeneratedInterface();
            EnsureBattleBackgroundCanvas();
            startCanvas = EnsureStartCanvas();

            canvas = CreateCanvas();
            RectTransform root = canvas.GetComponent<RectTransform>();
            ConfigureCombatScreenVisible(false);

            Image background = CreateImage("Background", root, new Color(0f, 0f, 0f, 0f));
            background.raycastTarget = false;
            Stretch(background.rectTransform);

            RectTransform header = CreatePanel("Header", root, new Color(0.13f, 0.15f, 0.18f, 0.94f));
            Anchor(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -74f), new Vector2(0f, 0f));

            stageText = CreateText("Stage Text", header, "Idle Adventure mode", 28, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(stageText.rectTransform, new Vector2(0f, 0f), new Vector2(0.58f, 1f), new Vector2(28f, 0f), new Vector2(-8f, 0f));

            turnText = CreateText("Turn Text", header, string.Empty, 20, FontStyles.Bold, TextAlignmentOptions.Right);
            Anchor(turnText.rectTransform, new Vector2(0.58f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-28f, 0f));

            RectTransform battlefield = CreatePanel("Battlefield", root, new Color(0f, 0f, 0f, 0f));
            Anchor(battlefield, new Vector2(0f, 0.23f), new Vector2(1f, 1f), new Vector2(28f, 20f), new Vector2(-28f, -92f));

            RectTransform playerPanel = CreatePanel("Friendly Party Area", battlefield, new Color(0.12f, 0.18f, 0.22f, 0.78f));
            Anchor(playerPanel, new Vector2(0f, 0f), new Vector2(0.48f, 1f), Vector2.zero, Vector2.zero);

            RectTransform enemyPanel = CreatePanel("Enemy Party Area", battlefield, new Color(0.22f, 0.13f, 0.14f, 0.78f));
            Anchor(enemyPanel, new Vector2(0.52f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            RectTransform friendlySlotsRoot = BuildPartyPanel(playerPanel, true);
            RectTransform enemySlotsRoot = BuildPartyPanel(enemyPanel, false);

            RectTransform footer = CreatePanel("Battle Log Panel", root, new Color(0.12f, 0.12f, 0.14f, 0.94f));
            Anchor(footer, new Vector2(0f, 0f), new Vector2(1f, 0.23f), new Vector2(28f, 24f), new Vector2(-28f, -14f));

            logText = CreateText("Battle Log", footer, string.Empty, 18, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Anchor(logText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(20f, 18f), new Vector2(-180f, -18f));

            pauseButton = CreateButton("Pause Button", footer, "Pause");
            Anchor(pauseButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-152f, -28f), new Vector2(-20f, 28f));
            pauseButton.onClick.AddListener(TogglePause);
            pauseButtonText = pauseButton.GetComponentInChildren<TextMeshProUGUI>();

            resultText = CreateText("Result Text", footer, string.Empty, 18, FontStyles.Bold, TextAlignmentOptions.Center);
            Anchor(resultText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-152f, 16f), new Vector2(-20f, 54f));

            battleHudView = canvas.gameObject.AddComponent<IdleAdventureBattleHud>();
            battleHudView.Initialize(canvas, root, stageText, turnText, friendlyCountText, enemyCountText, logText, resultText, pauseButton, pauseButtonText, friendlySlotsRoot, enemySlotsRoot);

            BuildStartPanel(startCanvas.GetComponent<RectTransform>());
            BuildEndPanel(root);
            HideEndPanel();
        }
        finally
        {
            interfaceBuildInProgress = false;
        }
    }

    private IdleAdventureBattleHud ResolveExistingBattleHud()
    {
        if (battleHudView != null && battleHudView.HasBindings) return battleHudView;

        IdleAdventureBattleHud[] huds = GetComponentsInChildren<IdleAdventureBattleHud>(true);
        for (int i = 0; i < huds.Length; i++)
        {
            if (huds[i] != null && huds[i].HasBindings) return huds[i];
        }

        return null;
    }

    private void BindBattleHud(IdleAdventureBattleHud hud)
    {
        battleHudView = hud;
        canvas = hud.Canvas;
        ConfigureHudCanvas(canvas);
        ConfigureHudSurfaceTransparency(hud.Root);
        stageText = hud.StageText;
        turnText = hud.TurnText;
        friendlyCountText = hud.FriendlyCountText;
        enemyCountText = hud.EnemyCountText;
        logText = hud.LogText;
        resultText = hud.ResultText;
        pauseButton = hud.PauseButton;
        pauseButtonText = hud.PauseButtonText;

        if (pauseButton != null)
        {
            pauseButton.onClick.RemoveAllListeners();
            pauseButton.onClick.AddListener(TogglePause);
        }

        BindActorViews(hud.FriendlySlotsRoot, true);
        BindActorViews(hud.EnemySlotsRoot, false);
    }

    private Canvas EnsureBattleBackgroundCanvas()
    {
        Transform existingCanvasTransform = transform.Find(BackgroundCanvasName);
        if (existingCanvasTransform == null)
        {
            GameObject canvasObject = new GameObject(BackgroundCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            backgroundCanvas = canvasObject.GetComponent<Canvas>();
        }
        else
        {
            backgroundCanvas = EnsureComponent<Canvas>(existingCanvasTransform.gameObject);
            EnsureComponent<CanvasScaler>(existingCanvasTransform.gameObject);
        }

        ConfigureBackgroundCanvas(backgroundCanvas);
        RectTransform root = backgroundCanvas.GetComponent<RectTransform>();

        Image baseImage = EnsureImage("Battle Background", root, new Color(0.055f, 0.06f, 0.075f, 1f));
        Stretch(baseImage.rectTransform);
        baseImage.rectTransform.SetSiblingIndex(0);

        Image distantLight = EnsureImage("Distant Light", root, new Color(0.15f, 0.20f, 0.24f, 0.86f));
        Anchor(distantLight.rectTransform, new Vector2(0f, 0.38f), new Vector2(1f, 0.72f), Vector2.zero, Vector2.zero);
        distantLight.rectTransform.SetSiblingIndex(1);

        Image battleGround = EnsureImage("Battle Ground", root, new Color(0.10f, 0.13f, 0.12f, 1f));
        Anchor(battleGround.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.34f), Vector2.zero, Vector2.zero);
        battleGround.rectTransform.SetSiblingIndex(2);

        Image centerLane = EnsureImage("Center Lane", root, new Color(0.18f, 0.17f, 0.14f, 0.62f));
        Anchor(centerLane.rectTransform, new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.28f), Vector2.zero, Vector2.zero);
        centerLane.rectTransform.SetSiblingIndex(3);

        return backgroundCanvas;
    }

    private Canvas EnsureStartCanvas()
    {
        Transform existingCanvasTransform = transform.Find(StartCanvasName);
        if (existingCanvasTransform == null)
        {
            GameObject canvasObject = new GameObject(StartCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            startCanvas = canvasObject.GetComponent<Canvas>();
        }
        else
        {
            startCanvas = EnsureComponent<Canvas>(existingCanvasTransform.gameObject);
            EnsureComponent<CanvasScaler>(existingCanvasTransform.gameObject);
            EnsureComponent<GraphicRaycaster>(existingCanvasTransform.gameObject);
        }

        ConfigureStartCanvas(startCanvas);
        return startCanvas;
    }

    private Canvas EnsureAfkRewardCanvas()
    {
        Transform existingCanvasTransform = transform.Find(AfkRewardCanvasName);
        if (existingCanvasTransform == null)
        {
            GameObject canvasObject = new GameObject(AfkRewardCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            afkRewardCanvas = canvasObject.GetComponent<Canvas>();
        }
        else
        {
            afkRewardCanvas = EnsureComponent<Canvas>(existingCanvasTransform.gameObject);
            EnsureComponent<CanvasScaler>(existingCanvasTransform.gameObject);
            EnsureComponent<GraphicRaycaster>(existingCanvasTransform.gameObject);
        }

        ConfigureAfkRewardCanvas(afkRewardCanvas);
        return afkRewardCanvas;
    }

    private void ConfigureBackgroundCanvas(Canvas targetCanvas)
    {
        if (targetCanvas == null) return;

        targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        targetCanvas.overrideSorting = true;
        targetCanvas.sortingOrder = -20;

        CanvasScaler scaler = targetCanvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }
    }

    private void ConfigureStartCanvas(Canvas targetCanvas)
    {
        if (targetCanvas == null) return;

        targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        targetCanvas.overrideSorting = true;
        targetCanvas.sortingOrder = 40;

        CanvasScaler scaler = targetCanvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }
    }

    private void ConfigureAfkRewardCanvas(Canvas targetCanvas)
    {
        if (targetCanvas == null) return;

        targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        targetCanvas.overrideSorting = true;
        targetCanvas.sortingOrder = 70;

        CanvasScaler scaler = targetCanvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }
    }

    private void ConfigureHudCanvas(Canvas targetCanvas)
    {
        if (targetCanvas == null) return;

        targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        targetCanvas.overrideSorting = true;
        targetCanvas.sortingOrder = 10;
    }

    private void ConfigureHudSurfaceTransparency(RectTransform root)
    {
        if (root == null) return;

        SetChildImageColor(root, "Background", new Color(0f, 0f, 0f, 0f), false);
        SetChildImageColor(root, "Header", new Color(0.13f, 0.15f, 0.18f, 0.94f), true);
        SetChildImageColor(root, "Battlefield/Friendly Party Area", new Color(0.12f, 0.18f, 0.22f, 0.78f), true);
        SetChildImageColor(root, "Battlefield/Enemy Party Area", new Color(0.22f, 0.13f, 0.14f, 0.78f), true);
        SetChildImageColor(root, "Battle Log Panel", new Color(0.12f, 0.12f, 0.14f, 0.94f), true);
    }

    private void ConfigureCombatScreenVisible(bool visible)
    {
        if (backgroundCanvas != null) backgroundCanvas.gameObject.SetActive(visible);
        if (canvas != null) canvas.gameObject.SetActive(visible);
    }

    private void ConfigureStartScreenVisible(bool visible)
    {
        if (startCanvas != null) startCanvas.gameObject.SetActive(visible);
    }

    private void BindActorViews(RectTransform slotsRoot, bool friendlySide)
    {
        IdleAdventureActorView[] targetViews = friendlySide ? friendlyViews : enemyViews;
        for (int i = 0; i < MaxPartySize; i++)
        {
            targetViews[i] = EnsureActorView(slotsRoot, friendlySide, i);
        }
    }

    private IdleAdventureActorView EnsureActorView(RectTransform slotsRoot, bool friendlySide, int index)
    {
        if (slotsRoot == null) return null;

        string columnName = IsFrontColumnSlot(index) ? "Front Column" : "Back Column";
        string slotName = $"{(friendlySide ? "Friendly" : "Enemy")} {columnName} Slot {index + 1}";
        Transform slotTransform = slotsRoot.Find(slotName);

        IdleAdventureActorView view = slotTransform != null ? slotTransform.GetComponent<IdleAdventureActorView>() : null;
        if (view == null || !view.HasViewBindings)
        {
            if (slotTransform != null) DestroyRuntimeObject(slotTransform.gameObject);
            view = CreateActorSlot(slotsRoot, friendlySide, index);
        }

        bool frontColumn = IsFrontColumnSlot(index);
        int row = GetFormationRow(index);
        int column = frontColumn == friendlySide ? 1 : 0;
        float left = column == 0 ? 0f : 0.52f;
        float right = column == 0 ? 0.48f : 1f;
        float top = 1f - row / (float)FormationRows;
        float bottom = 1f - (row + 1f) / FormationRows;
        Anchor(view.Root, new Vector2(left, bottom), new Vector2(right, top), new Vector2(6f, 8f), new Vector2(-6f, -8f));
        return view;
    }

    private void EnsurePersistentPanels(RectTransform root)
    {
        if (root == null) return;

        Canvas startScreenCanvas = EnsureStartCanvas();
        RectTransform startRoot = startScreenCanvas != null ? startScreenCanvas.GetComponent<RectTransform>() : root;
        RemoveLegacyStartPanels(root, startRoot);

        startPanelView = ResolveExistingStartPanel(startRoot);
        formationPanelView = ResolveExistingFormationPanel(startRoot);
        if (startPanelView == null || !startPanelView.HasBindings || formationPanelView == null || !formationPanelView.HasBindings)
        {
            if (startPanelView != null) DestroyRuntimeObject(startPanelView.gameObject);
            startPanelView = null;
            formationPanelView = null;
            BuildStartPanel(startRoot);
        }
        else
        {
            startPanelView.Bind(StartAdventure);
            formationPanelView.Bind(OpenFormationPanel, CloseFormationPanel, AutoFillFormation, ClearFormation, AssignFormationSlot, ClearFormationSlot);
            RefreshFormationView();
        }

        stageEndPanelView = ResolveExistingStageEndPanel(root);
        if (stageEndPanelView == null || !stageEndPanelView.HasBindings) BuildEndPanel(root);
        else stageEndPanelView.Bind(ContinueNextStage, EndMode);
        RemoveExtraStageEndPanels(root, stageEndPanelView);

        HideEndPanel();
    }

    private IdleAdventureStartPanel ResolveExistingStartPanel(RectTransform root)
    {
        if (startPanelView != null && startPanelView.HasBindings) return startPanelView;
        return root.GetComponentInChildren<IdleAdventureStartPanel>(true);
    }

    private IdleAdventureStageEndPanel ResolveExistingStageEndPanel(RectTransform root)
    {
        if (stageEndPanelView != null && stageEndPanelView.HasBindings) return stageEndPanelView;
        return root.GetComponentInChildren<IdleAdventureStageEndPanel>(true);
    }

    private IdleAdventureFormationPanel ResolveExistingFormationPanel(RectTransform root)
    {
        if (formationPanelView != null && formationPanelView.HasBindings) return formationPanelView;
        return root.GetComponentInChildren<IdleAdventureFormationPanel>(true);
    }

    private void RemoveLegacyStartPanels(RectTransform hudRoot, RectTransform validStartRoot)
    {
        if (hudRoot == null) return;

        IdleAdventureStartPanel[] panels = hudRoot.GetComponentsInChildren<IdleAdventureStartPanel>(true);
        for (int i = panels.Length - 1; i >= 0; i--)
        {
            IdleAdventureStartPanel panel = panels[i];
            if (panel == null) continue;
            if (validStartRoot != null && panel.transform.IsChildOf(validStartRoot)) continue;

            DestroyRuntimeObject(panel.gameObject);
        }
    }

    private void RemoveExtraStageEndPanels(RectTransform root, IdleAdventureStageEndPanel panelToKeep)
    {
        if (root == null) return;

        IdleAdventureStageEndPanel[] panels = root.GetComponentsInChildren<IdleAdventureStageEndPanel>(true);
        for (int i = panels.Length - 1; i >= 0; i--)
        {
            IdleAdventureStageEndPanel panel = panels[i];
            if (panel == null || panel == panelToKeep) continue;

            DestroyRuntimeObject(panel.gameObject);
        }
    }

    private void ClearGeneratedInterface()
    {
        Transform existingBackgroundCanvas = transform.Find(BackgroundCanvasName);
        if (existingBackgroundCanvas != null) DestroyRuntimeObject(existingBackgroundCanvas.gameObject);

        Transform existingStartCanvas = transform.Find(StartCanvasName);
        if (existingStartCanvas != null) DestroyRuntimeObject(existingStartCanvas.gameObject);

        Transform existingCanvas = transform.Find(HudCanvasName);
        if (existingCanvas != null) DestroyRuntimeObject(existingCanvas.gameObject);

        Transform existingAfkRewardCanvas = transform.Find(AfkRewardCanvasName);
        if (existingAfkRewardCanvas != null) DestroyRuntimeObject(existingAfkRewardCanvas.gameObject);

        backgroundCanvas = null;
        startCanvas = null;
        afkRewardCanvas = null;
        canvas = null;
        afkRewardSource = null;
        battleHudView = null;
        startPanelView = null;
        stageEndPanelView = null;
        formationPanelView = null;
        stageText = null;
        turnText = null;
        friendlyCountText = null;
        enemyCountText = null;
        logText = null;
        resultText = null;
        pauseButton = null;
        pauseButtonText = null;

        for (int i = 0; i < MaxPartySize; i++)
        {
            friendlyViews[i] = null;
            enemyViews[i] = null;
        }
    }

    private void BuildStartPanel(RectTransform root)
    {
        RectTransform startPanel = CreatePanel("Start Panel", root, new Color(0.045f, 0.052f, 0.065f, 1f));
        Stretch(startPanel);

        RectTransform skyBand = CreatePanel("Start Sky Band", startPanel, new Color(0.09f, 0.13f, 0.17f, 1f));
        Anchor(skyBand, new Vector2(0f, 0.48f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

        RectTransform groundBand = CreatePanel("Start Ground Band", startPanel, new Color(0.08f, 0.11f, 0.10f, 1f));
        Anchor(groundBand, new Vector2(0f, 0f), new Vector2(1f, 0.48f), Vector2.zero, Vector2.zero);

        RectTransform titleBlock = CreatePanel("Start Title Block", startPanel, new Color(0f, 0f, 0f, 0f));
        Anchor(titleBlock, new Vector2(0.08f, 0.54f), new Vector2(0.92f, 0.88f), Vector2.zero, Vector2.zero);

        TextMeshProUGUI startPanelTitleText = CreateText("Start Panel Title", titleBlock, "Idle Adventure mode", 54, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(startPanelTitleText.rectTransform, new Vector2(0f, 0.48f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

        TextMeshProUGUI startPanelBodyText = CreateText("Start Panel Body Text", titleBlock, "Expedition ready", 24, FontStyles.Normal, TextAlignmentOptions.Center);
        Anchor(startPanelBodyText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.42f), new Vector2(20f, 0f), new Vector2(-20f, 0f));

        RectTransform statusStrip = CreatePanel("Start Status Strip", startPanel, new Color(0.12f, 0.14f, 0.16f, 0.94f));
        Anchor(statusStrip, new Vector2(0.22f, 0.39f), new Vector2(0.78f, 0.48f), Vector2.zero, Vector2.zero);

        TextMeshProUGUI formationText = CreateText("Formation Status", statusStrip, "Camp prepared  |  Gate open  |  Team ready", 19, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(formationText.rectTransform);

        Button formationButton = CreateButton("Open Formation Button", startPanel, "Formation");
        Anchor(formationButton.GetComponent<RectTransform>(), new Vector2(0.28f, 0.13f), new Vector2(0.44f, 0.19f), Vector2.zero, Vector2.zero);

        Button startButton = CreateButton("Start Battle Button", startPanel, "Start Battle");
        Anchor(startButton.GetComponent<RectTransform>(), new Vector2(0.56f, 0.13f), new Vector2(0.72f, 0.19f), Vector2.zero, Vector2.zero);

        RectTransform formationWindow = CreatePanel("Formation Window", startPanel, new Color(0.035f, 0.04f, 0.05f, 0.98f));
        Stretch(formationWindow);
        formationWindow.gameObject.SetActive(false);

        TextMeshProUGUI formationTitle = CreateText("Formation Title", formationWindow, "Formation", 42, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(formationTitle.rectTransform, new Vector2(0f, 0.88f), new Vector2(1f, 0.98f), new Vector2(28f, 0f), new Vector2(-28f, 0f));

        RectTransform slotArea = CreatePanel("Formation Slot Area", formationWindow, new Color(0.08f, 0.09f, 0.11f, 0.92f));
        Anchor(slotArea, new Vector2(0.24f, 0.32f), new Vector2(0.76f, 0.82f), Vector2.zero, Vector2.zero);

        Image[] formationSlots = new Image[MaxPartySize];
        TextMeshProUGUI[] formationLabels = new TextMeshProUGUI[MaxPartySize];
        IdleAdventureFormationSlotDropTarget[] formationDropTargets = new IdleAdventureFormationSlotDropTarget[MaxPartySize];
        for (int i = 0; i < MaxPartySize; i++)
        {
            bool frontColumn = IsFrontColumnSlot(i);
            int row = GetFormationRow(i);
            int column = frontColumn ? 0 : 1;
            float left = column == 0 ? 0.05f : 0.53f;
            float right = column == 0 ? 0.47f : 0.95f;
            float top = 0.92f - row * 0.30f;
            float bottom = top - 0.22f;

            Image slotImage = CreateImage($"Formation Slot {i + 1}", slotArea, new Color(0.08f, 0.09f, 0.10f, 0.82f));
            Anchor(slotImage.rectTransform, new Vector2(left, bottom), new Vector2(right, top), Vector2.zero, Vector2.zero);
            IdleAdventureFormationSlotDropTarget dropTarget = slotImage.gameObject.AddComponent<IdleAdventureFormationSlotDropTarget>();
            dropTarget.Initialize(i, AssignFormationSlot, ClearFormationSlot);

            TextMeshProUGUI label = CreateText("Label", slotImage.transform, string.Empty, 15, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            if (label != null) label.fontSize = 15f;
            formationSlots[i] = slotImage;
            formationLabels[i] = label;
            formationDropTargets[i] = dropTarget;
        }

        RectTransform npcListArea = CreatePanel("Available NPC List", formationWindow, new Color(0.07f, 0.075f, 0.085f, 0.94f));
        Anchor(npcListArea, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.26f), Vector2.zero, Vector2.zero);

        Image[] npcItems = new Image[MaxPartySize];
        TextMeshProUGUI[] npcLabels = new TextMeshProUGUI[MaxPartySize];
        IdleAdventureFormationDragItem[] npcDragItems = new IdleAdventureFormationDragItem[MaxPartySize];
        Canvas ownerCanvas = startCanvas != null ? startCanvas : GetComponentInParent<Canvas>();
        for (int i = 0; i < MaxPartySize; i++)
        {
            Image npcItem = CreateImage($"Available NPC {i + 1}", npcListArea, new Color(0.18f, 0.28f, 0.34f, 0.94f));
            float left = 0.02f + i * 0.16f;
            float right = left + 0.14f;
            Anchor(npcItem.rectTransform, new Vector2(left, 0.16f), new Vector2(right, 0.84f), Vector2.zero, Vector2.zero);
            npcItem.gameObject.AddComponent<CanvasGroup>();
            IdleAdventureFormationDragItem dragItem = npcItem.gameObject.AddComponent<IdleAdventureFormationDragItem>();
            dragItem.Initialize(i, ownerCanvas);

            TextMeshProUGUI npcLabel = CreateText("Label", npcItem.transform, string.Empty, 15, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(npcLabel.rectTransform);
            npcItems[i] = npcItem;
            npcLabels[i] = npcLabel;
            npcDragItems[i] = dragItem;
        }

        Button autoFillButton = CreateButton("Formation Auto Fill Button", formationWindow, "Auto Fill");
        Anchor(autoFillButton.GetComponent<RectTransform>(), new Vector2(0.08f, 0.86f), new Vector2(0.20f, 0.93f), Vector2.zero, Vector2.zero);

        Button clearButton = CreateButton("Formation Clear Button", formationWindow, "Clear");
        Anchor(clearButton.GetComponent<RectTransform>(), new Vector2(0.80f, 0.86f), new Vector2(0.92f, 0.93f), Vector2.zero, Vector2.zero);

        Button closeButton = CreateButton("Formation Close Button", formationWindow, "Confirm");
        Anchor(closeButton.GetComponent<RectTransform>(), new Vector2(0.42f, 0.86f), new Vector2(0.58f, 0.93f), Vector2.zero, Vector2.zero);

        startPanelView = startPanel.gameObject.AddComponent<IdleAdventureStartPanel>();
        startPanelView.Initialize(startPanelTitleText, startPanelBodyText, startButton);
        startPanelView.Bind(StartAdventure);

        formationPanelView = startPanel.gameObject.AddComponent<IdleAdventureFormationPanel>();
        formationPanelView.Initialize(formationWindow, formationText, formationButton, closeButton, autoFillButton, clearButton, formationSlots, formationLabels, formationDropTargets, npcItems, npcLabels, npcDragItems, ownerCanvas);
        formationPanelView.Bind(OpenFormationPanel, CloseFormationPanel, AutoFillFormation, ClearFormation, AssignFormationSlot, ClearFormationSlot);
        RefreshFormationView();
    }

    private void BuildEndPanel(RectTransform root)
    {
        RectTransform endPanel = CreatePanel("End Panel", root, new Color(0f, 0f, 0f, 0.74f));
        Stretch(endPanel);

        RectTransform panelBody = CreatePanel("End Panel Body", endPanel, new Color(0.12f, 0.14f, 0.17f, 1f));
        Anchor(panelBody, new Vector2(0.30f, 0.32f), new Vector2(0.70f, 0.68f), Vector2.zero, Vector2.zero);

        TextMeshProUGUI endPanelTitleText = CreateText("End Panel Title", panelBody, "Stage Cleared", 30, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(endPanelTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -82f), new Vector2(-30f, -26f));

        TextMeshProUGUI endPanelBodyText = CreateText("End Panel Body Text", panelBody, string.Empty, 18, FontStyles.Normal, TextAlignmentOptions.Center);
        Anchor(endPanelBodyText.rectTransform, new Vector2(0f, 0.42f), new Vector2(1f, 0.74f), new Vector2(40f, 0f), new Vector2(-40f, 0f));

        Button continueButton = CreateButton("Continue Stage Button", panelBody, "Continue");
        Anchor(continueButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-256f, 44f), new Vector2(-24f, 98f));

        Button endModeButton = CreateButton("End Mode Button", panelBody, "End Mode");
        Anchor(endModeButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(24f, 44f), new Vector2(256f, 98f));

        stageEndPanelView = endPanel.gameObject.AddComponent<IdleAdventureStageEndPanel>();
        stageEndPanelView.Initialize(endPanelTitleText, endPanelBodyText, continueButton, endModeButton);
        stageEndPanelView.Bind(ContinueNextStage, EndMode);
    }

    private void ResetToStartScreen()
    {
        battleStarted = false;
        stageChoicePending = false;
        stageFailedPending = false;
        modePausedByChoice = false;
        paused = false;
        pendingNextStage = -1;
        stage = 1;
        round = 1;
        battleLog.Clear();
        LoadStageProgress();
        ClearRunRewards();
        grantedRewards.Clear();
        rewardDisplayNames.Clear();
        if (rewardLedger != null) rewardLedger.ClearGrantedRewards();
        InitializeDefaultFormation();

        ClearParty(friendlies);
        ClearParty(enemies);
        ClearCombatantRootChildren();

        if (pauseButtonText != null) pauseButtonText.text = "Pause";
        if (resultText != null) resultText.text = string.Empty;
        HideEndPanel();
        ShowStartPanel();
        SetWaitingText();
        if (startPanelView != null) startPanelView.SetContent("Idle Adventure mode", $"Next stage: {NextUnclearedStage()}");
        RefreshFormationView();
    }

    private void InitializeDefaultFormation()
    {
        for (int i = 0; i < MaxPartySize; i++)
        {
            friendlyFormation[i] = -1;
        }

        int availableCount = AvailableFriendlyDefinitionCount();
        int deployCount = Mathf.Clamp(startingFriendlyCount, 1, Mathf.Max(1, availableCount));
        deployCount = Mathf.Min(deployCount, MaxPartySize);
        for (int i = 0; i < deployCount; i++)
        {
            friendlyFormation[i] = i;
        }
    }

    private int AvailableFriendlyDefinitionCount()
    {
        int configuredCount = modeConfig != null && modeConfig.FriendlyLineup != null ? modeConfig.FriendlyLineup.Length : 0;
        return Mathf.Clamp(Mathf.Max(configuredCount, startingFriendlyCount), 1, MaxPartySize);
    }

    private int FormationDeployedCount()
    {
        int count = 0;
        for (int i = 0; i < MaxPartySize; i++)
        {
            if (friendlyFormation[i] >= 0) count++;
        }

        return count;
    }

    private bool IsFriendlyDefinitionAssigned(int definitionIndex, int ignoredSlot)
    {
        for (int i = 0; i < MaxPartySize; i++)
        {
            if (i == ignoredSlot) continue;
            if (friendlyFormation[i] == definitionIndex) return true;
        }

        return false;
    }

    private void RefreshFormationView()
    {
        if (formationPanelView == null || !formationPanelView.HasBindings) return;

        int deployedCount = FormationDeployedCount();
        formationPanelView.SetStatus($"{deployedCount}/{MaxPartySize} deployed");

        for (int i = 0; i < MaxPartySize; i++)
        {
            int definitionIndex = friendlyFormation[i];
            bool occupied = definitionIndex >= 0;
            string rowName = $"Row {GetFormationRow(i) + 1}";
            string columnName = IsFrontColumnSlot(i) ? "Front" : "Back";
            string unitName = occupied ? ResolveFriendlyFormationName(definitionIndex) : "Empty";
            formationPanelView.SetSlot(i, $"{columnName} {rowName}\n{unitName}", occupied, IsFrontColumnSlot(i));
        }

        int availableCount = AvailableFriendlyDefinitionCount();
        for (int i = 0; i < MaxPartySize; i++)
        {
            bool available = i < availableCount;
            bool assigned = available && IsFriendlyDefinitionAssigned(i, -1);
            string unitName = available ? ResolveFriendlyFormationName(i) : string.Empty;
            string label = assigned ? $"{unitName}\nAssigned" : unitName;
            formationPanelView.SetNpc(i, label, available, assigned);
        }
    }

    private string ResolveFriendlyFormationName(int definitionIndex)
    {
        string fallbackName = $"Hero {definitionIndex + 1}";
        IdleAdventureCombatantDefinition[] lineup = modeConfig != null ? modeConfig.FriendlyLineup : null;
        if (lineup == null || definitionIndex < 0 || definitionIndex >= lineup.Length || lineup[definitionIndex] == null) return fallbackName;
        return lineup[definitionIndex].ResolveName(fallbackName);
    }

    private void ShowStartPanel()
    {
        battleStarted = false;
        stageChoicePending = false;
        stageFailedPending = false;
        modePausedByChoice = false;
        paused = false;
        pendingNextStage = -1;
        ConfigureCombatScreenVisible(false);
        ConfigureStartScreenVisible(true);
        if (startPanelView != null) startPanelView.Show();
        if (formationPanelView != null) formationPanelView.HideFormationWindow();
        RefreshFormationView();
        HideEndPanel();
        UpdateButtonStates();
    }

    private void HideStartPanel()
    {
        if (startPanelView != null) startPanelView.Hide();
        ConfigureStartScreenVisible(false);
    }

    private void ShowEndPanel()
    {
        if (stageEndPanelView != null) stageEndPanelView.Show();
        UpdateButtonStates();
    }

    private void HideEndPanel()
    {
        if (stageEndPanelView != null) stageEndPanelView.Hide();
        UpdateButtonStates();
    }

    private void StartAdventure()
    {
        if (battleStarted) return;
        if (FormationDeployedCount() <= 0)
        {
            RefreshFormationView();
            return;
        }

        battleStarted = true;
        stageChoicePending = false;
        stageFailedPending = false;
        modePausedByChoice = false;
        pendingNextStage = -1;
        paused = false;
        ClearRunRewards();
        if (startPanelView != null) startPanelView.SetContent("Idle Adventure mode", "Expedition ready");
        if (pauseButtonText != null) pauseButtonText.text = "Pause";
        ConfigureCombatScreenVisible(true);
        HideStartPanel();
        HideEndPanel();
        int startStage = NextUnclearedStage();
        StartStage(startStage);
        AddLog($"Adventure started from Stage {startStage}.");
        UpdateButtonStates();
    }

    private void OpenStageCompletePanel()
    {
        RegisterStageClearRewards(stage);
        stageChoicePending = true;
        stageFailedPending = false;
        modePausedByChoice = false;
        pendingNextStage = stage + 1;
        paused = true;
        if (pauseButtonText != null) pauseButtonText.text = "Resume";
        if (resultText != null) resultText.text = "Victory";

        if (stageEndPanelView != null)
        {
            string pendingRewards = FormatRewardSummary(pendingRunRewards);
            stageEndPanelView.SetContent($"Stage {stage} Cleared", $"Pending rewards: {pendingRewards}\nChoose whether to continue to the next stage or exit this mode.");
            stageEndPanelView.SetButtonLabels("Continue", "Exit Mode");
        }
        AddLog($"Stage {stage} cleared. Waiting for player choice.");
        ShowEndPanel();
    }

    private void OpenStageFailedPanel()
    {
        stageChoicePending = false;
        stageFailedPending = true;
        modePausedByChoice = false;
        pendingNextStage = -1;
        paused = true;
        if (pauseButtonText != null) pauseButtonText.text = "Resume";
        if (resultText != null) resultText.text = "Defeated";

        if (stageEndPanelView != null)
        {
            string pendingRewards = FormatRewardSummary(pendingRunRewards);
            stageEndPanelView.SetContent("Party Defeated", $"Pending rewards: {pendingRewards}\nChoose whether to restart this stage or exit this mode.");
            stageEndPanelView.SetButtonLabels("Restart", "Exit Mode");
        }

        AddLog($"Friendly party defeated on Stage {stage}. Waiting for player choice.");
        ShowEndPanel();
    }

    private void ContinueNextStage()
    {
        if (!battleStarted) return;
        if (stageFailedPending)
        {
            RestartCurrentStage();
            return;
        }

        if (!stageChoicePending || pendingNextStage < 1) return;

        int nextStage = pendingNextStage;
        stageChoicePending = false;
        stageFailedPending = false;
        modePausedByChoice = false;
        pendingNextStage = -1;
        HideEndPanel();
        paused = false;
        StartStage(nextStage);
        UpdateButtonStates();
    }

    private void RestartCurrentStage()
    {
        if (!battleStarted || !stageFailedPending) return;

        int retryStage = Mathf.Max(1, stage);
        stageChoicePending = false;
        stageFailedPending = false;
        modePausedByChoice = false;
        pendingNextStage = -1;
        HideEndPanel();
        paused = false;
        StartStage(retryStage);
        AddLog($"Restarting Stage {retryStage}.");
        UpdateButtonStates();
    }

    private void EndMode()
    {
        if (!battleStarted) return;

        string rewardSummary = SettlePendingRewards();
        stageChoicePending = false;
        stageFailedPending = false;
        modePausedByChoice = true;
        pendingNextStage = -1;
        paused = true;
        battleStarted = false;
        if (pauseButtonText != null) pauseButtonText.text = "Pause";
        if (resultText != null) resultText.text = string.Empty;

        ClearParty(friendlies);
        ClearParty(enemies);
        ClearCombatantRootChildren();
        HideEndPanel();
        ConfigureCombatScreenVisible(false);
        ShowStartPanel();
        AddLog("Idle Adventure mode exited.");
        SetWaitingText();
        if (startPanelView != null) startPanelView.SetContent("Idle Adventure mode", $"Rewards granted: {rewardSummary}");
        UpdateButtonStates();
    }

    private void SetWaitingText()
    {
        if (stageText != null) stageText.text = "Idle Adventure mode";
        if (turnText != null) turnText.text = "Waiting to start";
        if (resultText != null) resultText.text = string.Empty;
        if (friendlyCountText != null) friendlyCountText.text = $"0/{MaxPartySize} deployed";
        if (enemyCountText != null) enemyCountText.text = $"0/{MaxPartySize} present";
    }

    private void ClearRunRewards()
    {
        clearedRewardStages.Clear();
        pendingRunRewards.Clear();
    }

    private int NextUnclearedStage()
    {
        EnsureStageProgress();
        return stageProgress != null ? stageProgress.NextUnclearedStage : 1;
    }

    private int HighestClearedStage()
    {
        EnsureStageProgress();
        return stageProgress != null ? stageProgress.HighestClearedStage : 0;
    }

    private void LoadStageProgress()
    {
        EnsureStageProgress();
        int loadedStage = stageProgress != null ? stageProgress.LoadProgress() : 0;
        if (afkRewardSource != null) afkRewardSource.SetHighestClearedStageFromProgress(loadedStage);
    }

    private int RecordStageProgress(int clearedStage)
    {
        EnsureStageProgress();
        return stageProgress != null ? stageProgress.RecordStageCleared(clearedStage) : Mathf.Max(0, clearedStage);
    }

    private void RegisterStageClearRewards(int clearedStage)
    {
        int rewardStage = Mathf.Max(1, clearedStage);
        if (clearedRewardStages.Contains(rewardStage)) return;

        clearedRewardStages.Add(rewardStage);
        int highestStage = RecordStageProgress(rewardStage);
        if (afkRewardSource != null) afkRewardSource.SetHighestClearedStage(highestStage);
        IdleAdventureRewardItemDefinition[] rewards = modeConfig != null ? modeConfig.RewardsForStage(rewardStage) : null;
        if (rewards == null || rewards.Length == 0)
        {
            AddLog($"Stage {rewardStage} cleared. No reward configured.");
            return;
        }

        for (int i = 0; i < rewards.Length; i++)
        {
            IdleAdventureRewardItemDefinition reward = rewards[i];
            if (reward == null) continue;
            AddRewardToLedger(pendingRunRewards, reward.ItemKey, reward.DisplayName, reward.Quantity);
        }

        AddLog($"Stage {rewardStage} rewards banked: {FormatRewardsForStage(rewards)}.");
    }

    private string SettlePendingRewards()
    {
        if (pendingRunRewards.Count == 0)
        {
            EnsureRewardLedger();
            if (rewardLedger != null) rewardLedger.GrantRewards(null, rewardDisplayNames, "None");
            ClearRunRewards();
            return "None";
        }

        Dictionary<string, int> settlementRewards = new Dictionary<string, int>(pendingRunRewards);
        string summary = FormatRewardSummary(pendingRunRewards);

        foreach (KeyValuePair<string, int> reward in settlementRewards)
        {
            string displayName = rewardDisplayNames.ContainsKey(reward.Key) ? rewardDisplayNames[reward.Key] : reward.Key;
            AddRewardToLedger(grantedRewards, reward.Key, displayName, reward.Value);
        }

        EnsureRewardLedger();
        if (rewardLedger != null) rewardLedger.GrantRewards(settlementRewards, rewardDisplayNames, summary);

        AddLog($"Rewards granted: {summary}.");
        ClearRunRewards();
        return summary;
    }

    private void AddRewardToLedger(Dictionary<string, int> ledger, string itemKey, string displayName, int quantity)
    {
        if (ledger == null || quantity <= 0) return;

        string key = string.IsNullOrEmpty(itemKey) ? "reward" : itemKey;
        string label = string.IsNullOrEmpty(displayName) ? key : displayName;
        if (!rewardDisplayNames.ContainsKey(key)) rewardDisplayNames.Add(key, label);

        if (ledger.ContainsKey(key)) ledger[key] += quantity;
        else ledger.Add(key, quantity);
    }

    private string FormatRewardsForStage(IdleAdventureRewardItemDefinition[] rewards)
    {
        if (rewards == null || rewards.Length == 0) return "None";

        List<string> parts = new List<string>();
        for (int i = 0; i < rewards.Length; i++)
        {
            IdleAdventureRewardItemDefinition reward = rewards[i];
            if (reward == null) continue;
            parts.Add($"{reward.DisplayName} x{reward.Quantity}");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "None";
    }

    private string FormatRewardSummary(Dictionary<string, int> rewards)
    {
        if (rewards == null || rewards.Count == 0) return "None";

        List<string> parts = new List<string>();
        foreach (KeyValuePair<string, int> reward in rewards)
        {
            string displayName = rewardDisplayNames.ContainsKey(reward.Key) ? rewardDisplayNames[reward.Key] : reward.Key;
            parts.Add($"{displayName} x{reward.Value}");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "None";
    }

    private void UpdateButtonStates()
    {
        if (pauseButton != null) pauseButton.interactable = battleStarted && !stageChoicePending && !stageFailedPending && !modePausedByChoice;
        if (startPanelView != null) startPanelView.SetInteractable(!battleStarted && FormationDeployedCount() > 0);
        if (stageEndPanelView != null)
        {
            stageEndPanelView.SetContinueInteractable(battleStarted && !modePausedByChoice && ((stageChoicePending && pendingNextStage > 0) || stageFailedPending));
            stageEndPanelView.SetEndModeInteractable(battleStarted && !modePausedByChoice && (stageChoicePending || stageFailedPending));
        }
    }

    private RectTransform BuildPartyPanel(RectTransform panel, bool friendlySide)
    {
        TextMeshProUGUI title = CreateText(friendlySide ? "Friendly Party Title" : "Enemy Party Title", panel, friendlySide ? "Friendly Party" : "Enemy Party", 28, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -62f), new Vector2(-20f, -18f));

        TextMeshProUGUI countText = CreateText(friendlySide ? "Friendly Count" : "Enemy Count", panel, string.Empty, 18, FontStyles.Normal, TextAlignmentOptions.Center);
        Anchor(countText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -96f), new Vector2(-20f, -64f));
        if (friendlySide) friendlyCountText = countText;
        else enemyCountText = countText;

        RectTransform grid = CreatePanel(friendlySide ? "Friendly Slots" : "Enemy Slots", panel, new Color(0f, 0f, 0f, 0f));
        Anchor(grid, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(24f, 28f), new Vector2(-24f, -112f));
        BuildPartySlots(grid, friendlySide);
        return grid;
    }

    private void BuildPartySlots(RectTransform grid, bool friendlySide)
    {
        if (grid == null) return;
        for (int i = 0; i < MaxPartySize; i++)
        {
            bool frontColumn = IsFrontColumnSlot(i);
            int row = GetFormationRow(i);
            int column = frontColumn == friendlySide ? 1 : 0;
            float left = column == 0 ? 0f : 0.52f;
            float right = column == 0 ? 0.48f : 1f;
            float top = 1f - row / (float)FormationRows;
            float bottom = 1f - (row + 1f) / FormationRows;

            IdleAdventureActorView view = CreateActorSlot(grid, friendlySide, i);
            Anchor(view.Root, new Vector2(left, bottom), new Vector2(right, top), new Vector2(6f, 8f), new Vector2(-6f, -8f));
            if (friendlySide) friendlyViews[i] = view;
            else enemyViews[i] = view;
        }
    }

    private IdleAdventureActorView CreateActorSlot(RectTransform parent, bool friendlySide, int index)
    {
        string columnName = IsFrontColumnSlot(index) ? "Front Column" : "Back Column";
        RectTransform root = CreatePanel($"{(friendlySide ? "Friendly" : "Enemy")} {columnName} Slot {index + 1}", parent, new Color(0.07f, 0.08f, 0.1f, 0.68f));

        TextMeshProUGUI nameText = CreateText("Name", root, string.Empty, 17, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -30f), new Vector2(-8f, -6f));

        RectTransform healthBar = CreatePanel("Health Bar", root, new Color(0.03f, 0.035f, 0.04f, 1f));
        Anchor(healthBar, new Vector2(0.12f, 1f), new Vector2(0.88f, 1f), new Vector2(0f, -52f), new Vector2(0f, -34f));

        Image healthFill = CreateImage("Health Fill", healthBar, friendlySide ? new Color(0.23f, 0.74f, 0.43f, 1f) : new Color(0.88f, 0.25f, 0.22f, 1f));
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Horizontal;
        healthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        Stretch(healthFill.rectTransform);

        TextMeshProUGUI healthText = CreateText("Health Text", healthBar, string.Empty, 12, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(healthText.rectTransform);

        RectTransform actorRoot = CreatePanel("Actor", root, new Color(0f, 0f, 0f, 0f));
        Anchor(actorRoot, new Vector2(0.5f, 0.46f), new Vector2(0.5f, 0.46f), new Vector2(-42f, -48f), new Vector2(42f, 36f));

        Image body = CreateImage("Body", actorRoot, friendlySide ? new Color(0.18f, 0.62f, 0.93f, 1f) : new Color(0.9f, 0.32f, 0.28f, 1f));
        Anchor(body.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-28f, 8f), new Vector2(28f, 58f));

        Image head = CreateImage("Head", actorRoot, friendlySide ? new Color(0.93f, 0.78f, 0.55f, 1f) : new Color(0.55f, 0.24f, 0.24f, 1f));
        Anchor(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-20f, -26f), new Vector2(20f, 14f));

        RectTransform buffIconRow = CreatePanel("Buff Icon Row", root, new Color(0f, 0f, 0f, 0f));
        Anchor(buffIconRow, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 12f), new Vector2(-10f, 38f));
        Image[] buffIconBackgrounds = new Image[MaxBuffIconSlots];
        TextMeshProUGUI[] buffIconLabels = new TextMeshProUGUI[MaxBuffIconSlots];
        for (int i = 0; i < MaxBuffIconSlots; i++)
        {
            Image buffIcon = CreateImage($"Buff Icon {i + 1}", buffIconRow, new Color(0.2f, 0.22f, 0.25f, 1f));
            float left = i / (float)MaxBuffIconSlots;
            float right = (i + 1f) / MaxBuffIconSlots;
            Anchor(buffIcon.rectTransform, new Vector2(left, 0f), new Vector2(right, 1f), new Vector2(3f, 0f), new Vector2(-3f, 0f));

            TextMeshProUGUI buffIconLabel = CreateText("Label", buffIcon.transform, string.Empty, 10, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(buffIconLabel.rectTransform);
            buffIconBackgrounds[i] = buffIcon;
            buffIconLabels[i] = buffIconLabel;
        }

        TextMeshProUGUI emptyText = CreateText("Empty Text", root, "Empty", 18, FontStyles.Normal, TextAlignmentOptions.Center);
        Stretch(emptyText.rectTransform);
        healthBar.SetAsLastSibling();

        IdleAdventureActorView view = root.gameObject.AddComponent<IdleAdventureActorView>();
        view.Initialize(root, actorRoot, body, healthFill, nameText, healthText, buffIconRow, buffIconBackgrounds, buffIconLabels, emptyText, head);
        return view;
    }

    private IEnumerator BattleLoop()
    {
        yield return null;

        while (enabled)
        {
            while (!battleStarted || paused || stageChoicePending || stageFailedPending || modePausedByChoice) yield return null;

            if (turnText != null) turnText.text = $"Round {round}: speed order";
            yield return ResolveInitiativeRound();
            if (AllDefeated(enemies))
            {
                AddLog($"Enemy party defeated. Stage {stage} cleared.");
                OpenStageCompletePanel();
                continue;
            }

            if (AllDefeated(friendlies))
            {
                AddLog("Friendly party was defeated.");
                OpenStageFailedPanel();
                continue;
            }

            round++;
            RefreshBattleView();
            yield return new WaitForSeconds(turnInterval);
        }
    }

    private IEnumerator ResolveInitiativeRound()
    {
        List<IdleAdventureCombatant> turnOrder = BuildInitiativeOrder();
        AddLog($"Round {round} order: {FormatInitiativeOrder(turnOrder)}");

        for (int i = 0; i < turnOrder.Count; i++)
        {
            while (paused) yield return null;

            IdleAdventureCombatant attacker = turnOrder[i];
            if (attacker == null || attacker.IsDefeated) continue;

            List<IdleAdventureCombatant> defenders = attacker.IsFriendly ? enemies : friendlies;
            IdleAdventureCombatant target = FirstTargetByFormation(defenders, attacker.SlotIndex);
            if (target == null) yield break;

            IdleAdventureBattleSkill skill = attacker.FirstReadySkill();
            int damage = CalculateDamage(attacker, target, skill != null ? skill.Definition : null);
            string actionName = skill != null ? skill.Definition.SkillName : "Attack";
            AddLog($"{attacker.CombatantName} (SPD {attacker.EffectiveSpeed}) uses {actionName} on {target.CombatantName} for {damage}.");
            yield return AnimateAttack(GetView(attacker), attacker.IsFriendly ? 1f : -1f);
            target.TakeDamage(damage);
            ApplySkillBuffs(attacker, target, skill);
            if (skill != null) skill.TriggerCooldown();
            yield return AnimateHit(GetView(target));
            RefreshBattleView();

            if (target.IsDefeated)
            {
                AddLog($"{target.CombatantName} is defeated.");
                RefreshBattleView();
            }

            if (AllDefeated(defenders)) yield break;
            yield return new WaitForSeconds(turnInterval * 0.35f);
        }

        TickBuffs(friendlies);
        TickBuffs(enemies);
        TickSkillCooldowns(friendlies);
        TickSkillCooldowns(enemies);
        RefreshBattleView();
    }

    private List<IdleAdventureCombatant> BuildInitiativeOrder()
    {
        List<IdleAdventureCombatant> turnOrder = new List<IdleAdventureCombatant>(friendlies.Count + enemies.Count);
        AddLivingCombatants(friendlies, turnOrder);
        AddLivingCombatants(enemies, turnOrder);
        turnOrder.Sort(CompareInitiative);
        return turnOrder;
    }

    private static void AddLivingCombatants(List<IdleAdventureCombatant> source, List<IdleAdventureCombatant> destination)
    {
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null && !source[i].IsDefeated) destination.Add(source[i]);
        }
    }

    private static int CompareInitiative(IdleAdventureCombatant a, IdleAdventureCombatant b)
    {
        int speedComparison = b.EffectiveSpeed.CompareTo(a.EffectiveSpeed);
        if (speedComparison != 0) return speedComparison;
        if (a.IsFriendly != b.IsFriendly) return a.IsFriendly ? -1 : 1;
        return a.SlotIndex.CompareTo(b.SlotIndex);
    }

    private static string FormatInitiativeOrder(List<IdleAdventureCombatant> turnOrder)
    {
        if (turnOrder == null || turnOrder.Count == 0) return "none";

        List<string> names = new List<string>();
        int count = Mathf.Min(6, turnOrder.Count);
        for (int i = 0; i < count; i++)
        {
            names.Add($"{turnOrder[i].CombatantName}({turnOrder[i].EffectiveSpeed})");
        }
        return string.Join(" > ", names);
    }

    private void StartStage(int nextStage)
    {
        stage = nextStage;
        round = 1;
        stageChoicePending = false;
        stageFailedPending = false;
        modePausedByChoice = false;
        pendingNextStage = -1;
        paused = false;
        if (pauseButtonText != null) pauseButtonText.text = "Pause";
        if (resultText != null) resultText.text = string.Empty;
        HideEndPanel();

        BuildFriendlyParty(Mathf.Clamp(startingFriendlyCount, 1, MaxPartySize));
        BuildEnemyParty(CalculateEnemyCountForStage(stage));

        AddLog($"Stage {stage} begins. {DeployedCount(friendlies)} friendly unit(s) vs {DeployedCount(enemies)} enemy unit(s).");
        RefreshBattleView();
        UpdateButtonStates();
    }

    private void BuildFriendlyParty(int count)
    {
        ClearParty(friendlies);
        if (FormationDeployedCount() <= 0) InitializeDefaultFormation();

        for (int slotIndex = 0; slotIndex < MaxPartySize; slotIndex++)
        {
            int definitionIndex = friendlyFormation[slotIndex];
            friendlies.Add(definitionIndex >= 0 ? CreateFriendlyCombatantForFormation(definitionIndex, slotIndex) : null);
        }
    }

    private void BuildEnemyParty(int count)
    {
        ClearParty(enemies);
        int enemyCount = Mathf.Clamp(count, 1, MaxPartySize);
        for (int slotIndex = 0; slotIndex < MaxPartySize; slotIndex++)
        {
            enemies.Add(slotIndex < enemyCount ? CreateEnemyCombatantForSlot(slotIndex) : null);
        }
    }

    private IdleAdventureCombatant CreateFriendlyCombatantForFormation(int definitionIndex, int slotIndex)
    {
        IdleAdventureCombatantDefinition[] lineup = modeConfig != null ? modeConfig.FriendlyLineup : null;
        IdleAdventureCombatantDefinition definition = lineup != null && definitionIndex >= 0 && definitionIndex < lineup.Length ? lineup[definitionIndex] : null;
        if (definition != null)
        {
            string combatantName = definition.ResolveName($"Hero {definitionIndex + 1}");
            return CreateCombatant(combatantName, Mathf.Max(1, definition.MaxHealth), Mathf.Max(0, definition.Attack), Mathf.Max(0, definition.Defense), Mathf.Max(1, definition.Speed), true, slotIndex, definition.SkillSlots, definition.AttributeType);
        }

        int health = friendlyBaseHealth + definitionIndex * 14;
        int attack = friendlyBaseAttack + definitionIndex * 3;
        int defense = friendlyBaseDefense + definitionIndex;
        int speed = friendlyBaseSpeed + (MaxPartySize - definitionIndex);
        return CreateCombatant($"Hero {definitionIndex + 1}", health, attack, defense, speed, true, slotIndex);
    }

    private IdleAdventureCombatant CreateEnemyCombatantForSlot(int slotIndex)
    {
        IdleAdventureCombatantDefinition[] lineup = modeConfig != null ? modeConfig.EnemyLineup : null;
        IdleAdventureCombatantDefinition definition = lineup != null && slotIndex >= 0 && slotIndex < lineup.Length ? lineup[slotIndex] : null;
        if (definition != null)
        {
            int stageOffset = stage - 1;
            string combatantName = definition.ResolveName($"Enemy {stage}-{slotIndex + 1}");
            int health = Mathf.Max(1, definition.MaxHealth + stageOffset * enemyHealthPerStage);
            int attack = Mathf.Max(0, definition.Attack + stageOffset * enemyAttackPerStage);
            int defense = Mathf.Max(0, definition.Defense + Mathf.Max(0, stage / 3));
            int speed = Mathf.Max(1, definition.Speed + stageOffset * enemySpeedPerStage);
            return CreateCombatant(combatantName, health, attack, defense, speed, false, slotIndex, definition.SkillSlots, definition.AttributeType);
        }

        int fallbackHealth = enemyBaseHealth + (stage - 1) * enemyHealthPerStage + slotIndex * 12;
        int fallbackAttack = enemyBaseAttack + (stage - 1) * enemyAttackPerStage + slotIndex * 2;
        int fallbackDefense = enemyBaseDefense + Mathf.Max(0, stage / 3) + slotIndex / 2;
        int fallbackSpeed = enemyBaseSpeed + (stage - 1) * enemySpeedPerStage + (MaxPartySize - slotIndex);
        return CreateCombatant($"Enemy {stage}-{slotIndex + 1}", fallbackHealth, fallbackAttack, fallbackDefense, fallbackSpeed, false, slotIndex);
    }

    private int AddConfiguredCombatants(List<IdleAdventureCombatant> party, IdleAdventureCombatantDefinition[] lineup, int requestedCount, bool isFriendly)
    {
        if (lineup == null || lineup.Length == 0) return 0;

        int definitionCount = Mathf.Min(lineup.Length, requestedCount, MaxPartySize);
        for (int i = 0; i < definitionCount; i++)
        {
            IdleAdventureCombatantDefinition definition = lineup[i];
            if (definition == null) continue;

            int slotIndex = party.Count;
            int stageOffset = isFriendly ? 0 : stage - 1;
            int health = Mathf.Max(1, definition.MaxHealth + stageOffset * enemyHealthPerStage);
            int attack = Mathf.Max(0, definition.Attack + stageOffset * enemyAttackPerStage);
            int defense = Mathf.Max(0, definition.Defense + (isFriendly ? 0 : Mathf.Max(0, stage / 3)));
            int speed = Mathf.Max(1, definition.Speed + stageOffset * enemySpeedPerStage);
            string fallbackName = isFriendly ? $"Hero {slotIndex + 1}" : $"Enemy {stage}-{slotIndex + 1}";
            party.Add(CreateCombatant(definition.ResolveName(fallbackName), health, attack, defense, speed, isFriendly, slotIndex, definition.SkillSlots, definition.AttributeType));
        }

        return party.Count;
    }

    private IdleAdventureCombatant CreateCombatant(string combatantName, int health, int attack, int defense, int speed, bool isFriendly, int slotIndex, IdleAdventureSkillDefinition[] skillDefinitions = null, IdleAdventureDamageAttribute attributeType = IdleAdventureDamageAttribute.Physical)
    {
        EnsureCombatantRoot();

        IdleAdventureCombatant combatantPrefab = isFriendly ? friendlyCombatantPrefab : enemyCombatantPrefab;
        IdleAdventureCombatant combatant;
        if (combatantPrefab != null)
        {
            combatant = Instantiate(combatantPrefab, combatantRoot, false);
            combatant.name = combatantName;
        }
        else
        {
            GameObject combatantObject = new GameObject(combatantName, typeof(IdleAdventureCombatant));
            combatantObject.transform.SetParent(combatantRoot, false);
            combatant = combatantObject.GetComponent<IdleAdventureCombatant>();
        }

        combatant.Initialize(combatantName, health, attack, defense, speed, isFriendly, slotIndex, skillDefinitions, isFriendly ? FriendlySkillSlotCount : EnemySkillSlotCount, attributeType);
        return combatant;
    }

    private void ClearParty(List<IdleAdventureCombatant> party)
    {
        for (int i = 0; i < party.Count; i++)
        {
            if (party[i] == null) continue;
            DestroyRuntimeObject(party[i].gameObject);
        }

        party.Clear();
    }

    private void ClearCombatantRootChildren()
    {
        EnsureCombatantRoot();
        if (combatantRoot == null) return;

        for (int i = combatantRoot.childCount - 1; i >= 0; i--)
        {
            DestroyRuntimeObject(combatantRoot.GetChild(i).gameObject);
        }
    }

    private void DestroyRuntimeObject(GameObject runtimeObject)
    {
        if (runtimeObject == null) return;

        if (Application.isPlaying) Destroy(runtimeObject);
        else DestroyImmediate(runtimeObject);
    }

    private int CalculateEnemyCountForStage(int currentStage)
    {
        int extra = addEnemyEveryStages <= 0 ? 0 : (currentStage - 1) / addEnemyEveryStages;
        return Mathf.Clamp(baseEnemyCount + extra, 1, MaxPartySize);
    }

    private void ReviveFriendlyParty()
    {
        for (int i = 0; i < friendlies.Count; i++)
        {
            if (friendlies[i] != null) friendlies[i].HealToFull();
        }

        BuildEnemyParty(CalculateEnemyCountForStage(stage));
        round = 1;
        paused = false;
        if (resultText != null) resultText.text = string.Empty;
        AddLog($"Friendly party recovered. Retrying Stage {stage}.");
        RefreshBattleView();
        UpdateButtonStates();
    }

    private void RefreshBattleView()
    {
        if (stageText != null) stageText.text = $"Idle Adventure mode  |  Stage {stage}";
        if (turnText != null) turnText.text = $"Round {round}  |  Allies {LivingCount(friendlies)}/{DeployedCount(friendlies)}  Enemies {LivingCount(enemies)}/{DeployedCount(enemies)}";
        if (friendlyCountText != null) friendlyCountText.text = $"{DeployedCount(friendlies)}/{MaxPartySize} deployed";
        if (enemyCountText != null) enemyCountText.text = $"{DeployedCount(enemies)}/{MaxPartySize} present";

        RefreshPartyViews(friendlies, friendlyViews);
        RefreshPartyViews(enemies, enemyViews);
        if (logText != null) logText.text = string.Join("\n", battleLog.ToArray());
    }

    private void RefreshPartyViews(List<IdleAdventureCombatant> party, IdleAdventureActorView[] views)
    {
        for (int i = 0; i < MaxPartySize; i++)
        {
            IdleAdventureCombatant combatant = i < party.Count ? party[i] : null;
            if (views[i] != null) views[i].Apply(combatant);
        }
    }

    private IdleAdventureActorView GetView(IdleAdventureCombatant combatant)
    {
        if (combatant == null) return null;
        IdleAdventureActorView[] views = combatant.IsFriendly ? friendlyViews : enemyViews;
        return combatant.SlotIndex >= 0 && combatant.SlotIndex < views.Length ? views[combatant.SlotIndex] : null;
    }

    private static bool IsFrontColumnSlot(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < FormationRows;
    }

    private static int GetFormationRow(int slotIndex)
    {
        return Mathf.Clamp(slotIndex % FormationRows, 0, FormationRows - 1);
    }

    private static IdleAdventureCombatant FirstLiving(List<IdleAdventureCombatant> party)
    {
        for (int i = 0; i < party.Count; i++)
        {
            if (party[i] != null && !party[i].IsDefeated) return party[i];
        }
        return null;
    }

    private static IdleAdventureCombatant FirstTargetByFormation(List<IdleAdventureCombatant> party, int attackerSlotIndex)
    {
        IdleAdventureCombatant frontTarget = FirstLivingInRange(party, 0, FormationRows);
        int start = frontTarget != null ? 0 : FormationRows;
        int end = frontTarget != null ? FormationRows : MaxPartySize;
        return FirstLivingByRowPriority(party, start, end, GetFormationRow(attackerSlotIndex));
    }

    private static IdleAdventureCombatant FirstLivingInRange(List<IdleAdventureCombatant> party, int startInclusive, int endExclusive)
    {
        int end = Mathf.Min(endExclusive, party.Count);
        for (int i = startInclusive; i < end; i++)
        {
            if (party[i] != null && !party[i].IsDefeated) return party[i];
        }
        return null;
    }

    private static IdleAdventureCombatant FirstLivingByRowPriority(List<IdleAdventureCombatant> party, int startInclusive, int endExclusive, int preferredRow)
    {
        IdleAdventureCombatant sameRow = LivingAtSlot(party, startInclusive + preferredRow);
        if (sameRow != null) return sameRow;

        for (int row = preferredRow - 1; row >= 0; row--)
        {
            IdleAdventureCombatant upperRow = LivingAtSlot(party, startInclusive + row);
            if (upperRow != null) return upperRow;
        }

        int rowCount = endExclusive - startInclusive;
        for (int row = preferredRow + 1; row < rowCount; row++)
        {
            IdleAdventureCombatant lowerRow = LivingAtSlot(party, startInclusive + row);
            if (lowerRow != null) return lowerRow;
        }

        return null;
    }

    private static IdleAdventureCombatant LivingAtSlot(List<IdleAdventureCombatant> party, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= party.Count) return null;
        return party[slotIndex] != null && !party[slotIndex].IsDefeated ? party[slotIndex] : null;
    }

    private static bool AllDefeated(List<IdleAdventureCombatant> party)
    {
        return FirstLiving(party) == null;
    }

    private static int LivingCount(List<IdleAdventureCombatant> party)
    {
        int count = 0;
        for (int i = 0; i < party.Count; i++)
        {
            if (party[i] != null && !party[i].IsDefeated) count++;
        }
        return count;
    }

    private static int DeployedCount(List<IdleAdventureCombatant> party)
    {
        int count = 0;
        for (int i = 0; i < party.Count; i++)
        {
            if (party[i] != null) count++;
        }

        return count;
    }

    private static bool TryApplyBuff(List<IdleAdventureCombatant> party, int slotIndex, IdleAdventureBattleBuff buff)
    {
        if (buff == null) return false;
        if (slotIndex < 0 || slotIndex >= party.Count) return false;
        IdleAdventureCombatant target = party[slotIndex];
        if (target == null || target.IsDefeated) return false;

        target.AddBuff(buff);
        return true;
    }

    private static void TickBuffs(List<IdleAdventureCombatant> party)
    {
        for (int i = 0; i < party.Count; i++)
        {
            if (party[i] != null) party[i].TickBuffs();
        }
    }

    private static void TickSkillCooldowns(List<IdleAdventureCombatant> party)
    {
        for (int i = 0; i < party.Count; i++)
        {
            if (party[i] != null) party[i].TickSkillCooldowns();
        }
    }

    private int CalculateDamage(IdleAdventureCombatant attacker, IdleAdventureCombatant target, IdleAdventureSkillDefinition skill)
    {
        if (attacker == null || target == null) return 0;

        int baseDamage = Mathf.Max(0, attacker.EffectiveAttack - target.EffectiveDefense);
        float skillMultiplier = skill != null ? skill.SkillMultiplier : 1f;
        float normalBuffMultiplier = attacker.SumNormalDamageBuffMultiplier();
        float attributeBuffMultiplier = attacker.SumAttributeDamageBuffMultiplier();
        float fieldBuffMultiplier = CalculateFieldBuffMultiplier(attacker);
        float restraintMultiplier = IsAttributeRestrained(attacker.AttributeType, target.AttributeType) ? AttributeRestraintMultiplier : 0f;
        float bonusMultiplier = 1f + normalBuffMultiplier + attributeBuffMultiplier + fieldBuffMultiplier + restraintMultiplier;
        float finalDamage = baseDamage * skillMultiplier * bonusMultiplier;
        return Mathf.Max(0, Mathf.FloorToInt(finalDamage));
    }

    private float CalculateFieldBuffMultiplier(IdleAdventureCombatant attacker)
    {
        if (attacker == null || modeConfig == null || modeConfig.FieldBuffs == null) return 0f;

        float value = 0f;
        IdleAdventureFieldBuffDefinition[] fieldBuffs = modeConfig.FieldBuffs;
        for (int i = 0; i < fieldBuffs.Length; i++)
        {
            IdleAdventureFieldBuffDefinition fieldBuff = fieldBuffs[i];
            if (fieldBuff == null) continue;
            if (attacker.IsFriendly && !fieldBuff.AffectFriendlies) continue;
            if (!attacker.IsFriendly && !fieldBuff.AffectEnemies) continue;
            if (!IdleAdventureCombatant.AttributeMatches(attacker.AttributeType, fieldBuff.AffectedAttribute)) continue;

            value += fieldBuff.FieldDamageMultiplier;
        }

        return value;
    }

    private bool IsAttributeRestrained(IdleAdventureDamageAttribute attackerAttribute, IdleAdventureDamageAttribute defenderAttribute)
    {
        if (attackerAttribute == IdleAdventureDamageAttribute.None || defenderAttribute == IdleAdventureDamageAttribute.None) return false;
        if (modeConfig == null || modeConfig.AttributeRestraints == null) return false;

        IdleAdventureAttributeRestraintDefinition[] restraints = modeConfig.AttributeRestraints;
        for (int i = 0; i < restraints.Length; i++)
        {
            IdleAdventureAttributeRestraintDefinition restraint = restraints[i];
            if (restraint == null) continue;
            if (restraint.AttackerAttribute == attackerAttribute && restraint.DefenderAttribute == defenderAttribute) return true;
        }

        return false;
    }

    private void ApplySkillBuffs(IdleAdventureCombatant attacker, IdleAdventureCombatant target, IdleAdventureBattleSkill skill)
    {
        if (skill == null || skill.Definition == null) return;

        if (skill.Definition.HasSelfBuff) ApplyBuffPresetToCombatant(attacker, skill.Definition.SelfBuffPresetIndex);
        if (skill.Definition.HasTargetBuff) ApplyBuffPresetToCombatant(target, skill.Definition.TargetBuffPresetIndex);
    }

    private bool ApplyBuffPresetToCombatant(IdleAdventureCombatant target, int presetIndex)
    {
        if (target == null || target.IsDefeated || modeConfig == null || modeConfig.BuffPresets == null || presetIndex < 0 || presetIndex >= modeConfig.BuffPresets.Length) return false;

        IdleAdventureBuffDefinition preset = modeConfig.BuffPresets[presetIndex];
        if (preset == null) return false;

        IdleAdventureBattleBuff buff = preset.CreateRuntimeBuff();
        target.AddBuff(buff);
        string verb = buff.Category == IdleAdventureBuffCategory.Benefit ? "gained" : "suffered";
        AddLog($"{target.CombatantName} {verb} {buff.DisplayName} from {buff.SourceLabel}.");
        return true;
    }

    private IEnumerator AnimateAttack(IdleAdventureActorView attacker, float direction)
    {
        if (attacker == null || attacker.ActorRoot == null) yield break;

        RectTransform rectTransform = attacker.ActorRoot;
        Vector2 start = rectTransform.anchoredPosition;
        Vector2 lunge = start + new Vector2(36f * direction, 8f);
        float halfDuration = attackAnimationDuration * 0.5f;

        yield return MoveRect(rectTransform, start, lunge, halfDuration);
        yield return MoveRect(rectTransform, lunge, start, halfDuration);
    }

    private IEnumerator MoveRect(RectTransform rectTransform, Vector2 from, Vector2 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = 1f - Mathf.Pow(1f - t, 3f);
            rectTransform.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
            yield return null;
        }

        rectTransform.anchoredPosition = to;
    }

    private IEnumerator AnimateHit(IdleAdventureActorView target)
    {
        if (target == null || target.Body == null) yield break;

        Color originalColor = target.Body.color;
        target.Body.color = Color.white;
        yield return new WaitForSeconds(hitFlashDuration);
        target.Body.color = originalColor;
    }

    private void AddLog(string message)
    {
        battleLog.Enqueue(message);
        while (battleLog.Count > 5) battleLog.Dequeue();
        if (logText != null) logText.text = string.Join("\n", battleLog.ToArray());
    }

    private void TogglePause()
    {
        if (!battleStarted || stageChoicePending || stageFailedPending || modePausedByChoice) return;

        paused = !paused;
        if (pauseButtonText != null) pauseButtonText.text = paused ? "Resume" : "Pause";
        AddLog(paused ? "Battle paused." : "Battle resumed.");
        UpdateButtonStates();
    }

    private Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject(HudCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas newCanvas = canvasObject.GetComponent<Canvas>();
        ConfigureHudCanvas(newCanvas);

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return newCanvas;
    }

    private RectTransform CreatePanel(string objectName, Transform parent, Color color)
    {
        Image image = CreateImage(objectName, parent, color);
        return image.rectTransform;
    }

    private Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private Image EnsureImage(string objectName, Transform parent, Color color)
    {
        Transform existing = parent != null ? parent.Find(objectName) : null;
        Image image;
        if (existing == null)
        {
            image = CreateImage(objectName, parent, color);
        }
        else
        {
            EnsureComponent<CanvasRenderer>(existing.gameObject);
            image = EnsureComponent<Image>(existing.gameObject);
            image.color = color;
        }

        image.raycastTarget = false;
        return image;
    }

    private void SetChildImageColor(RectTransform root, string childPath, Color color, bool raycastTarget)
    {
        Transform child = root != null ? root.Find(childPath) : null;
        if (child == null) return;

        Image image = child.GetComponent<Image>();
        if (image == null) return;

        image.color = color;
        image.raycastTarget = raycastTarget;
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        if (target == null) return null;

        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private TextMeshProUGUI CreateText(string objectName, Transform parent, string text, float fontSize, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = alignment;
        label.color = new Color(0.93f, 0.94f, 0.95f, 1f);
        label.raycastTarget = false;
        label.enableWordWrapping = true;
        return label;
    }

    private Button CreateButton(string objectName, Transform parent, string labelText)
    {
        Image image = CreateImage(objectName, parent, new Color(0.22f, 0.3f, 0.38f, 1f));
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI label = CreateText("Label", image.transform, labelText, 18, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        return button;
    }

    private static void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void Anchor(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
    }

}
