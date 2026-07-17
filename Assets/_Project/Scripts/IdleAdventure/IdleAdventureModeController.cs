using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class IdleAdventureModeController : MonoBehaviour
{
    private const int MaxPartySize = 6;

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

    private readonly Queue<string> battleLog = new Queue<string>();
    private readonly List<Combatant> friendlies = new List<Combatant>(MaxPartySize);
    private readonly List<Combatant> enemies = new List<Combatant>(MaxPartySize);
    private readonly ActorView[] friendlyViews = new ActorView[MaxPartySize];
    private readonly ActorView[] enemyViews = new ActorView[MaxPartySize];

    private Canvas canvas;
    private TextMeshProUGUI stageText;
    private TextMeshProUGUI turnText;
    private TextMeshProUGUI friendlyCountText;
    private TextMeshProUGUI enemyCountText;
    private TextMeshProUGUI logText;
    private TextMeshProUGUI resultText;
    private Button pauseButton;
    private TextMeshProUGUI pauseButtonText;

    private int stage = 1;
    private int round = 1;
    private bool paused;
    private Coroutine battleRoutine;

    private void Awake()
    {
        EnsureEventSystem();
        ConfigureCamera();
        BuildInterface();
        StartStage(1);
    }

    private void OnEnable()
    {
        battleRoutine = StartCoroutine(BattleLoop());
    }

    private void OnDisable()
    {
        if (battleRoutine != null)
        {
            StopCoroutine(battleRoutine);
            battleRoutine = null;
        }
    }

    public void SetFriendlyPartySize(int count)
    {
        BuildFriendlyParty(Mathf.Clamp(count, 1, MaxPartySize));
        round = 1;
        AddLog($"Friendly party size changed to {friendlies.Count}.");
        RefreshBattleView();
    }

    public void SetEnemyPartySize(int count)
    {
        BuildEnemyParty(Mathf.Clamp(count, 1, MaxPartySize));
        round = 1;
        AddLog($"Enemy party size changed to {enemies.Count}.");
        RefreshBattleView();
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

    private void BuildInterface()
    {
        canvas = CreateCanvas();
        RectTransform root = canvas.GetComponent<RectTransform>();

        Image background = CreateImage("Background", root, new Color(0.09f, 0.1f, 0.12f));
        Stretch(background.rectTransform);

        RectTransform header = CreatePanel("Header", root, new Color(0.13f, 0.15f, 0.18f, 1f));
        Anchor(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -74f), new Vector2(0f, 0f));

        stageText = CreateText("Stage Text", header, "Idle Adventure mode", 28, FontStyles.Bold, TextAlignmentOptions.Left);
        Anchor(stageText.rectTransform, new Vector2(0f, 0f), new Vector2(0.58f, 1f), new Vector2(28f, 0f), new Vector2(-8f, 0f));

        turnText = CreateText("Turn Text", header, string.Empty, 20, FontStyles.Bold, TextAlignmentOptions.Right);
        Anchor(turnText.rectTransform, new Vector2(0.58f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-28f, 0f));

        RectTransform battlefield = CreatePanel("Battlefield", root, new Color(0f, 0f, 0f, 0f));
        Anchor(battlefield, new Vector2(0f, 0.23f), new Vector2(1f, 1f), new Vector2(28f, 20f), new Vector2(-28f, -92f));

        RectTransform playerPanel = CreatePanel("Friendly Party Area", battlefield, new Color(0.12f, 0.18f, 0.22f, 1f));
        Anchor(playerPanel, new Vector2(0f, 0f), new Vector2(0.48f, 1f), Vector2.zero, Vector2.zero);

        RectTransform enemyPanel = CreatePanel("Enemy Party Area", battlefield, new Color(0.22f, 0.13f, 0.14f, 1f));
        Anchor(enemyPanel, new Vector2(0.52f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

        BuildPartyPanel(playerPanel, true);
        BuildPartyPanel(enemyPanel, false);

        RectTransform footer = CreatePanel("Battle Log Panel", root, new Color(0.12f, 0.12f, 0.14f, 1f));
        Anchor(footer, new Vector2(0f, 0f), new Vector2(1f, 0.23f), new Vector2(28f, 24f), new Vector2(-28f, -14f));

        logText = CreateText("Battle Log", footer, string.Empty, 18, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        Anchor(logText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(20f, 18f), new Vector2(-180f, -18f));

        pauseButton = CreateButton("Pause Button", footer, "Pause");
        Anchor(pauseButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-152f, -28f), new Vector2(-20f, 28f));
        pauseButton.onClick.AddListener(TogglePause);
        pauseButtonText = pauseButton.GetComponentInChildren<TextMeshProUGUI>();

        resultText = CreateText("Result Text", footer, string.Empty, 18, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(resultText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-152f, 16f), new Vector2(-20f, 54f));
    }

    private void BuildPartyPanel(RectTransform panel, bool friendlySide)
    {
        TextMeshProUGUI title = CreateText(friendlySide ? "Friendly Party Title" : "Enemy Party Title", panel, friendlySide ? "Friendly Party" : "Enemy Party", 28, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -62f), new Vector2(-20f, -18f));

        TextMeshProUGUI countText = CreateText(friendlySide ? "Friendly Count" : "Enemy Count", panel, string.Empty, 18, FontStyles.Normal, TextAlignmentOptions.Center);
        Anchor(countText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -96f), new Vector2(-20f, -64f));
        if (friendlySide) friendlyCountText = countText;
        else enemyCountText = countText;

        RectTransform grid = CreatePanel(friendlySide ? "Friendly Slots" : "Enemy Slots", panel, new Color(0f, 0f, 0f, 0f));
        Anchor(grid, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(24f, 28f), new Vector2(-24f, -112f));

        for (int i = 0; i < MaxPartySize; i++)
        {
            bool frontRow = i < 3;
            int row = frontRow ? i : i - 3;
            int column = frontRow == friendlySide ? 1 : 0;
            float left = column == 0 ? 0f : 0.52f;
            float right = column == 0 ? 0.48f : 1f;
            float top = 1f - row / 3f;
            float bottom = 1f - (row + 1f) / 3f;

            ActorView view = CreateActorSlot(grid, friendlySide, i);
            Anchor(view.Root, new Vector2(left, bottom), new Vector2(right, top), new Vector2(6f, 8f), new Vector2(-6f, -8f));
            if (friendlySide) friendlyViews[i] = view;
            else enemyViews[i] = view;
        }
    }

    private ActorView CreateActorSlot(RectTransform parent, bool friendlySide, int index)
    {
        string rowName = index < 3 ? "Front" : "Back";
        RectTransform root = CreatePanel($"{(friendlySide ? "Friendly" : "Enemy")} {rowName} Slot {index + 1}", parent, new Color(0.07f, 0.08f, 0.1f, 0.68f));

        TextMeshProUGUI nameText = CreateText("Name", root, string.Empty, 17, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -30f), new Vector2(-8f, -6f));

        RectTransform healthBar = CreatePanel("Health Bar", root, new Color(0.03f, 0.035f, 0.04f, 1f));
        Anchor(healthBar, new Vector2(0.12f, 1f), new Vector2(0.88f, 1f), new Vector2(0f, -58f), new Vector2(0f, -40f));

        Image healthFill = CreateImage("Health Fill", healthBar, friendlySide ? new Color(0.23f, 0.74f, 0.43f, 1f) : new Color(0.88f, 0.25f, 0.22f, 1f));
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Horizontal;
        healthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        Stretch(healthFill.rectTransform);

        TextMeshProUGUI healthText = CreateText("Health Text", healthBar, string.Empty, 12, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(healthText.rectTransform);

        RectTransform actorRoot = CreatePanel("Actor", root, new Color(0f, 0f, 0f, 0f));
        Anchor(actorRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-42f, -42f), new Vector2(42f, 42f));

        Image body = CreateImage("Body", actorRoot, friendlySide ? new Color(0.18f, 0.62f, 0.93f, 1f) : new Color(0.9f, 0.32f, 0.28f, 1f));
        Anchor(body.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-28f, 8f), new Vector2(28f, 58f));

        Image head = CreateImage("Head", actorRoot, friendlySide ? new Color(0.93f, 0.78f, 0.55f, 1f) : new Color(0.55f, 0.24f, 0.24f, 1f));
        Anchor(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-20f, -26f), new Vector2(20f, 14f));

        TextMeshProUGUI statsText = CreateText("Stats", root, string.Empty, 13, FontStyles.Normal, TextAlignmentOptions.Center);
        Anchor(statsText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(8f, 6f), new Vector2(-8f, 28f));

        TextMeshProUGUI emptyText = CreateText("Empty Text", root, "Empty", 18, FontStyles.Normal, TextAlignmentOptions.Center);
        Stretch(emptyText.rectTransform);

        return new ActorView(root, actorRoot, body, healthFill, nameText, healthText, statsText, emptyText);
    }

    private IEnumerator BattleLoop()
    {
        yield return null;

        while (enabled)
        {
            while (paused) yield return null;

            turnText.text = $"Round {round}: speed order";
            yield return ResolveInitiativeRound();
            if (AllDefeated(enemies))
            {
                AddLog($"Enemy party defeated. Stage {stage} cleared.");
                resultText.text = "Victory";
                yield return new WaitForSeconds(turnInterval);
                StartStage(stage + 1);
                continue;
            }

            if (AllDefeated(friendlies))
            {
                AddLog("Friendly party was defeated. Recovering and retrying this stage.");
                resultText.text = "Recovering";
                yield return new WaitForSeconds(turnInterval * 1.5f);
                ReviveFriendlyParty();
                continue;
            }

            round++;
            RefreshBattleView();
            yield return new WaitForSeconds(turnInterval);
        }
    }

    private IEnumerator ResolveInitiativeRound()
    {
        List<Combatant> turnOrder = BuildInitiativeOrder();
        AddLog($"Round {round} order: {FormatInitiativeOrder(turnOrder)}");

        for (int i = 0; i < turnOrder.Count; i++)
        {
            while (paused) yield return null;

            Combatant attacker = turnOrder[i];
            if (attacker == null || attacker.IsDefeated) continue;

            List<Combatant> defenders = attacker.IsFriendly ? enemies : friendlies;
            Combatant target = FirstTargetByFormation(defenders, attacker.SlotIndex);
            if (target == null) yield break;

            int damage = CalculateDamage(attacker.Attack, target.Defense);
            AddLog($"{attacker.Name} (SPD {attacker.Speed}) attacks {target.Name} for {damage}.");
            yield return AnimateAttack(GetView(attacker), attacker.IsFriendly ? 1f : -1f);
            target.TakeDamage(damage);
            yield return AnimateHit(GetView(target));
            RefreshBattleView();

            if (target.IsDefeated)
            {
                AddLog($"{target.Name} is defeated.");
                RefreshBattleView();
            }

            if (AllDefeated(defenders)) yield break;
            yield return new WaitForSeconds(turnInterval * 0.35f);
        }
    }

    private List<Combatant> BuildInitiativeOrder()
    {
        List<Combatant> turnOrder = new List<Combatant>(friendlies.Count + enemies.Count);
        AddLivingCombatants(friendlies, turnOrder);
        AddLivingCombatants(enemies, turnOrder);
        turnOrder.Sort(CompareInitiative);
        return turnOrder;
    }

    private static void AddLivingCombatants(List<Combatant> source, List<Combatant> destination)
    {
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null && !source[i].IsDefeated) destination.Add(source[i]);
        }
    }

    private static int CompareInitiative(Combatant a, Combatant b)
    {
        int speedComparison = b.Speed.CompareTo(a.Speed);
        if (speedComparison != 0) return speedComparison;
        if (a.IsFriendly != b.IsFriendly) return a.IsFriendly ? -1 : 1;
        return a.SlotIndex.CompareTo(b.SlotIndex);
    }

    private static string FormatInitiativeOrder(List<Combatant> turnOrder)
    {
        if (turnOrder == null || turnOrder.Count == 0) return "none";

        List<string> names = new List<string>();
        int count = Mathf.Min(6, turnOrder.Count);
        for (int i = 0; i < count; i++)
        {
            names.Add($"{turnOrder[i].Name}({turnOrder[i].Speed})");
        }
        return string.Join(" > ", names);
    }

    private void StartStage(int nextStage)
    {
        stage = nextStage;
        round = 1;
        resultText.text = string.Empty;

        BuildFriendlyParty(Mathf.Clamp(startingFriendlyCount, 1, MaxPartySize));
        BuildEnemyParty(CalculateEnemyCountForStage(stage));

        AddLog($"Stage {stage} begins. {friendlies.Count} friendly unit(s) vs {enemies.Count} enemy unit(s).");
        RefreshBattleView();
    }

    private void BuildFriendlyParty(int count)
    {
        friendlies.Clear();
        for (int i = 0; i < count; i++)
        {
            int health = friendlyBaseHealth + i * 14;
            int attack = friendlyBaseAttack + i * 3;
            int defense = friendlyBaseDefense + i;
            int speed = friendlyBaseSpeed + (MaxPartySize - i);
            friendlies.Add(new Combatant($"Hero {i + 1}", health, attack, defense, speed, true, i));
        }
    }

    private void BuildEnemyParty(int count)
    {
        enemies.Clear();
        for (int i = 0; i < count; i++)
        {
            int health = enemyBaseHealth + (stage - 1) * enemyHealthPerStage + i * 12;
            int attack = enemyBaseAttack + (stage - 1) * enemyAttackPerStage + i * 2;
            int defense = enemyBaseDefense + Mathf.Max(0, stage / 3) + i / 2;
            int speed = enemyBaseSpeed + (stage - 1) * enemySpeedPerStage + (MaxPartySize - i);
            enemies.Add(new Combatant($"Enemy {stage}-{i + 1}", health, attack, defense, speed, false, i));
        }
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
        resultText.text = string.Empty;
        AddLog($"Friendly party recovered. Retrying Stage {stage}.");
        RefreshBattleView();
    }

    private void RefreshBattleView()
    {
        stageText.text = $"Idle Adventure mode  |  Stage {stage}";
        turnText.text = $"Round {round}  |  Allies {LivingCount(friendlies)}/{friendlies.Count}  Enemies {LivingCount(enemies)}/{enemies.Count}";
        if (friendlyCountText != null) friendlyCountText.text = $"{friendlies.Count}/{MaxPartySize} deployed";
        if (enemyCountText != null) enemyCountText.text = $"{enemies.Count}/{MaxPartySize} present";

        RefreshPartyViews(friendlies, friendlyViews);
        RefreshPartyViews(enemies, enemyViews);
        if (logText != null) logText.text = string.Join("\n", battleLog.ToArray());
    }

    private void RefreshPartyViews(List<Combatant> party, ActorView[] views)
    {
        for (int i = 0; i < MaxPartySize; i++)
        {
            Combatant combatant = i < party.Count ? party[i] : null;
            views[i].Apply(combatant);
        }
    }

    private ActorView GetView(Combatant combatant)
    {
        if (combatant == null) return null;
        ActorView[] views = combatant.IsFriendly ? friendlyViews : enemyViews;
        return combatant.SlotIndex >= 0 && combatant.SlotIndex < views.Length ? views[combatant.SlotIndex] : null;
    }

    private static Combatant FirstLiving(List<Combatant> party)
    {
        for (int i = 0; i < party.Count; i++)
        {
            if (party[i] != null && !party[i].IsDefeated) return party[i];
        }
        return null;
    }

    private static Combatant FirstTargetByFormation(List<Combatant> party, int attackerSlotIndex)
    {
        Combatant frontTarget = FirstLivingInRange(party, 0, 3);
        int start = frontTarget != null ? 0 : 3;
        int end = frontTarget != null ? 3 : MaxPartySize;
        return FirstLivingByRowPriority(party, start, end, attackerSlotIndex % 3);
    }

    private static Combatant FirstLivingInRange(List<Combatant> party, int startInclusive, int endExclusive)
    {
        int end = Mathf.Min(endExclusive, party.Count);
        for (int i = startInclusive; i < end; i++)
        {
            if (party[i] != null && !party[i].IsDefeated) return party[i];
        }
        return null;
    }

    private static Combatant FirstLivingByRowPriority(List<Combatant> party, int startInclusive, int endExclusive, int preferredRow)
    {
        Combatant sameRow = LivingAtSlot(party, startInclusive + preferredRow);
        if (sameRow != null) return sameRow;

        for (int row = preferredRow - 1; row >= 0; row--)
        {
            Combatant upperRow = LivingAtSlot(party, startInclusive + row);
            if (upperRow != null) return upperRow;
        }

        int rowCount = endExclusive - startInclusive;
        for (int row = preferredRow + 1; row < rowCount; row++)
        {
            Combatant lowerRow = LivingAtSlot(party, startInclusive + row);
            if (lowerRow != null) return lowerRow;
        }

        return null;
    }

    private static Combatant LivingAtSlot(List<Combatant> party, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= party.Count) return null;
        return party[slotIndex] != null && !party[slotIndex].IsDefeated ? party[slotIndex] : null;
    }

    private static bool AllDefeated(List<Combatant> party)
    {
        return FirstLiving(party) == null;
    }

    private static int LivingCount(List<Combatant> party)
    {
        int count = 0;
        for (int i = 0; i < party.Count; i++)
        {
            if (party[i] != null && !party[i].IsDefeated) count++;
        }
        return count;
    }

    private int CalculateDamage(int attack, int defense)
    {
        return Mathf.Max(1, attack - defense);
    }

    private IEnumerator AnimateAttack(ActorView attacker, float direction)
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

    private IEnumerator AnimateHit(ActorView target)
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
        paused = !paused;
        pauseButtonText.text = paused ? "Resume" : "Pause";
        AddLog(paused ? "Battle paused." : "Battle resumed.");
    }

    private Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Idle Adventure Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas newCanvas = canvasObject.GetComponent<Canvas>();
        newCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

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

    private sealed class ActorView
    {
        public ActorView(RectTransform root, RectTransform actorRoot, Image body, Image healthFill, TextMeshProUGUI nameText, TextMeshProUGUI healthText, TextMeshProUGUI statsText, TextMeshProUGUI emptyText)
        {
            Root = root;
            ActorRoot = actorRoot;
            Body = body;
            this.healthFill = healthFill;
            this.nameText = nameText;
            this.healthText = healthText;
            this.statsText = statsText;
            this.emptyText = emptyText;
        }

        public RectTransform Root { get; }
        public RectTransform ActorRoot { get; }
        public Image Body { get; }
        private readonly Image healthFill;
        private readonly TextMeshProUGUI nameText;
        private readonly TextMeshProUGUI healthText;
        private readonly TextMeshProUGUI statsText;
        private readonly TextMeshProUGUI emptyText;

        public void Apply(Combatant combatant)
        {
            bool occupied = combatant != null;
            ActorRoot.gameObject.SetActive(occupied);
            healthFill.transform.parent.gameObject.SetActive(occupied);
            nameText.gameObject.SetActive(occupied);
            healthText.gameObject.SetActive(occupied);
            statsText.gameObject.SetActive(occupied);
            emptyText.gameObject.SetActive(!occupied);

            Image rootImage = Root.GetComponent<Image>();
            if (rootImage != null) rootImage.color = occupied ? new Color(0.07f, 0.08f, 0.1f, 0.68f) : new Color(0.05f, 0.055f, 0.065f, 0.35f);
            if (!occupied) return;

            nameText.text = combatant.Name;
            healthFill.fillAmount = combatant.HealthRatio;
            healthText.text = $"{combatant.CurrentHealth}/{combatant.MaxHealth}";
            statsText.text = $"ATK {combatant.Attack}  DEF {combatant.Defense}  SPD {combatant.Speed}";
            Body.color = combatant.IsDefeated ? new Color(0.28f, 0.28f, 0.28f, 0.65f) : combatant.BaseColor;
        }
    }

    private sealed class Combatant
    {
        public Combatant(string name, int maxHealth, int attack, int defense, int speed, bool isFriendly, int slotIndex)
        {
            Name = name;
            MaxHealth = Mathf.Max(1, maxHealth);
            CurrentHealth = MaxHealth;
            Attack = Mathf.Max(0, attack);
            Defense = Mathf.Max(0, defense);
            Speed = Mathf.Max(1, speed);
            IsFriendly = isFriendly;
            SlotIndex = slotIndex;
            BaseColor = isFriendly ? new Color(0.18f, 0.62f, 0.93f, 1f) : new Color(0.9f, 0.32f, 0.28f, 1f);
        }

        public string Name { get; }
        public int MaxHealth { get; }
        public int CurrentHealth { get; private set; }
        public int Attack { get; }
        public int Defense { get; }
        public int Speed { get; }
        public bool IsFriendly { get; }
        public int SlotIndex { get; }
        public Color BaseColor { get; }
        public float HealthRatio => Mathf.Clamp01((float)CurrentHealth / MaxHealth);
        public bool IsDefeated => CurrentHealth <= 0;

        public void TakeDamage(int damage)
        {
            CurrentHealth = Mathf.Max(0, CurrentHealth - Mathf.Max(0, damage));
        }

        public void HealToFull()
        {
            CurrentHealth = MaxHealth;
        }
    }
}
