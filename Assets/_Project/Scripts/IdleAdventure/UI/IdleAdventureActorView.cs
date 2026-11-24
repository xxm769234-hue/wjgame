using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class IdleAdventureActorView : MonoBehaviour
{
    private const int MaxVisibleBuffIcons = 6;
    private static Sprite groundShadowSprite;
    private Image groundShadow;
    private Image leftContactShadow;
    private Image rightContactShadow;
    private const float EnemyFacingRightScaleX = 1f;
    private const float ForestSlimeHorizontalFlipScaleX = -1f;
    private const string CharacterSpriteResourceName = "IdleAdventureCharacter";
    private const string ForestSlimeSpriteResourceName = "IdleAdventureEnemyForestSlime";
    private const string FlameRaiderSpriteResourceName = "IdleAdventureEnemyFlameRaider";

    private static Sprite forestSlimeSprite;
    private static Sprite flameRaiderSprite;
    private static Sprite characterSprite;
    private static Sprite unknownEnemySprite;
    private static readonly Dictionary<string, Sprite> statusSprites = new Dictionary<string, Sprite>();

    [SerializeField] private RectTransform root;
    [SerializeField] private RectTransform actorRoot;
    [SerializeField] private Image body;
    [SerializeField] private Image head;
    [SerializeField] private Image healthFill;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private RectTransform buffIconRow;
    [SerializeField] private Image[] buffIconBackgrounds = new Image[0];
    [SerializeField] private TextMeshProUGUI[] buffIconLabels = new TextMeshProUGUI[0];
    [SerializeField] private TextMeshProUGUI emptyText;
    private BattleActorAnimation battleAnimation;
    private Func<bool> pauseQuery;
    private float headerBodyHeight = -1f;
    private Vector2 buffRowPosition;
    private readonly Image[] buffArtwork = new Image[MaxVisibleBuffIcons];
    private readonly TextMeshProUGUI[] buffFallbackLabels = new TextMeshProUGUI[MaxVisibleBuffIcons];
    private readonly TextMeshProUGUI[] buffDirectionLabels = new TextMeshProUGUI[MaxVisibleBuffIcons];
    private BattleHealthBar bladeBar;
    private Vector2 headerSoleOffset;
    private RectTransform labelsAnchor;
    private RectTransform LabelParent => labelsAnchor != null ? labelsAnchor : actorRoot;

    public RectTransform Root => root != null ? root : transform as RectTransform;
    public RectTransform ActorRoot => actorRoot;
    public Image Body => body;
    public BattleActorAnimation Animation => battleAnimation;
    public bool HasViewBindings => actorRoot != null && body != null && healthFill != null && nameText != null && healthText != null && HasBuffIconBindings() && emptyText != null;

    public void SetPauseQuery(Func<bool> query) { pauseQuery = query; }

    public void ConfigureBattleFootOrigin()
    {
        if (actorRoot == null) return;
        actorRoot.anchorMin = actorRoot.anchorMax = new Vector2(0.5f, 0f);
        actorRoot.pivot = new Vector2(0.5f, 0f);
        actorRoot.sizeDelta = new Vector2(84f, 84f);
        actorRoot.anchoredPosition = new Vector2(0f, 8f);
        var field = BattleFieldLayers.For(root);
        if (field != null)
        {
            field.PlaceActor(this);
            if (labelsAnchor == null)
            {
                labelsAnchor = new GameObject(name + " Labels", typeof(RectTransform)).GetComponent<RectTransform>();
                labelsAnchor.SetParent(field.Labels, false);
                labelsAnchor.sizeDelta = Vector2.zero;
                labelsAnchor.position = actorRoot.position;
                labelsAnchor.gameObject.SetActive(actorRoot.gameObject.activeInHierarchy);
            }
        }
        if (buffIconRow != null)
        {
            ConfigureBuffRow();
        }
    }

    private void LateUpdate()
    {
        if (labelsAnchor != null)
        {
            bool visible = actorRoot.gameObject.activeInHierarchy;
            if (labelsAnchor.gameObject.activeSelf != visible) labelsAnchor.gameObject.SetActive(visible);
            if (visible)
                labelsAnchor.position = battleAnimation != null && battleAnimation.InMeleeContact
                    ? battleAnimation.GroundShadowCenter() - actorRoot.TransformVector(headerSoleOffset)
                    : actorRoot.position;
        }
        if (battleAnimation != null && battleAnimation.IsBound && !Mathf.Approximately(headerBodyHeight, battleAnimation.BodyHeight))
            ConfigureActorHeader();
        if (groundShadow != null) UpdateGroundShadow();
    }

    private void EnsureGroundShadow()
    {
        if (groundShadow != null) return;
        if (groundShadowSprite == null) groundShadowSprite = Resources.Load<Sprite>("IdleAdventureUI/FormationFootShadow");
        if (groundShadowSprite == null) return;
        var field = BattleFieldLayers.For(transform);
        Transform layer = field != null ? field.Shadows : null;
        groundShadow = new GameObject(name + " Ground Shadow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        groundShadow.rectTransform.SetParent(layer != null ? layer : actorRoot, false);
        groundShadow.rectTransform.SetAsFirstSibling();
        groundShadow.sprite = groundShadowSprite;
        groundShadow.enabled = false;
        groundShadow.raycastTarget = false;
        leftContactShadow = CreateContactShadow("Left sole");
        rightContactShadow = CreateContactShadow("Right sole");
    }

    private Image CreateContactShadow(string label)
    {
        var contact = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        contact.rectTransform.SetParent(groundShadow.transform, false);
        contact.sprite = groundShadowSprite;
        contact.raycastTarget = false;
        contact.enabled = false;
        return contact;
    }

    private void UpdateGroundShadow()
    {
        groundShadow.enabled = actorRoot.gameObject.activeInHierarchy && body.isActiveAndEnabled && battleAnimation != null && !battleAnimation.IsDead;
        if (!groundShadow.enabled)
        {
            leftContactShadow.enabled = rightContactShadow.enabled = false;
            return;
        }
        var appearance = battleAnimation.Appearance;
        var clip = battleAnimation.CurrentSequence;
        RectTransform visual = body.rectTransform;
        float height = battleAnimation.IsBound ? battleAnimation.BodyHeight : visual.rect.height;
        Vector2 left, right;
        float lift = 0f;
        var pose = battleAnimation.DisplayedPoseSequence;
        if (pose != null && pose.TryGetFootContacts(battleAnimation.DisplayedPoseFrame, out left, out right))
        {
            if (pose.action != BattleAnimationAction.Idle)
                lift = BattleActorAnimation.GroundLift(left, right);
            left.y -= lift;
            right.y -= lift;
            left *= height;
            right *= height;
        }
        else if (battleAnimation.IsBound)
        {
            Vector2 center = appearance.groundShadowOffset * height;
            left = center - Vector2.right * height * .22f;
            right = center + Vector2.right * height * .22f;
        }
        else GetStaticSoles(out left, out right);

        Vector2 span = right - left;
        Vector2 size = appearance != null && appearance.groundShadowSize.x > 0f
            ? appearance.groundShadowSize * height
            : new Vector2(Mathf.Clamp(span.magnitude + height * .31f, height * .39f, height * 1.28f), height * .13f);
        RectTransform shadow = groundShadow.rectTransform;
        shadow.position = visual.TransformPoint((left + right) * .5f);
        Vector3 axis = shadow.parent.InverseTransformVector(visual.TransformVector(span));
        float angle = Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg;
        shadow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Clamp(Mathf.DeltaAngle(0f, angle * 2f) * .5f, -28f, 28f));
        shadow.sizeDelta = Vector2.Scale(size, new Vector2(1.12f, 1.16f)) * Mathf.Lerp(1f, .78f, Mathf.Clamp01(lift));
        float strength = appearance != null && appearance.groundShadowOpacity > 0f ? appearance.groundShadowOpacity : .51f;
        float fade = body.color.a * Mathf.Lerp(1f, .58f, Mathf.Clamp01(lift));
        if (battleAnimation.IsPlayingDeath && clip != null && clip.action == BattleAnimationAction.Death)
            fade *= 1f - Mathf.SmoothStep(0f, 1f, battleAnimation.CurrentSourceFrame / clip.FrameCount);
        groundShadow.color = new Color(0f, 0f, 0f, Mathf.Min(1f, strength * 1.12f) * fade);
        bool showContacts = (appearance == null || appearance.groundShadowSize.x <= 0f) &&
            body.sprite != null && body.sprite != unknownEnemySprite && body.sprite != forestSlimeSprite;
        float contactStrength = showContacts ? .59f * fade * (1f - Mathf.InverseLerp(.01f, .12f, lift)) : 0f;
        bool resting = pose == null || pose.action == BattleAnimationAction.Idle;
        float lowest = Mathf.Min(left.y, right.y);
        UpdateSoleShadow(leftContactShadow, visual, left, height, resting ? 0f : (left.y - lowest) / height, contactStrength);
        UpdateSoleShadow(rightContactShadow, visual, right, height, resting ? 0f : (right.y - lowest) / height, contactStrength);
    }

    private void UpdateSoleShadow(Image contact, RectTransform visual, Vector2 sole, float height, float raisedBy, float strength)
    {
        float opacity = strength * (1f - Mathf.InverseLerp(.09f, .23f, raisedBy));
        contact.enabled = opacity > .005f;
        if (!contact.enabled) return;
        RectTransform rect = contact.rectTransform;
        rect.position = visual.TransformPoint(sole);
        rect.rotation = groundShadow.rectTransform.parent.rotation;
        rect.sizeDelta = new Vector2(.224f, .071f) * height;
        contact.color = new Color(0f, 0f, 0f, opacity);
    }

    internal void GetStaticSoles(out Vector2 left, out Vector2 right)
    {
        Sprite sprite = body.sprite;
        Rect drawn = body.rectTransform.rect;
        left = new Vector2(.32f, 0f);
        right = new Vector2(.68f, 0f);
        if (sprite != null)
        {
            Vector2 fit = sprite.rect.size * Mathf.Min(drawn.width / sprite.rect.width, drawn.height / sprite.rect.height);
            drawn = new Rect(drawn.min + Vector2.Scale(drawn.size - fit, body.rectTransform.pivot), fit);
            if (sprite == flameRaiderSprite) { left = new Vector2(.385f, .166f); right = new Vector2(.765f, .013f); }
            else if (sprite == forestSlimeSprite) { left = new Vector2(.44f, .115f); right = new Vector2(.62f, .095f); }
            else if (sprite == characterSprite) { left = new Vector2(.47f, .043f); right = new Vector2(.65f, .01f); }
            else if (sprite.name == "Npc_1") { left = new Vector2(.39f, .037f); right = new Vector2(.59f, .04f); }
        }
        left = drawn.min + Vector2.Scale(drawn.size, left);
        right = drawn.min + Vector2.Scale(drawn.size, right);
    }

    private void OnDisable()
    {
        if (bladeBar != null) bladeBar.ResetPresentation();
        if (labelsAnchor != null) labelsAnchor.gameObject.SetActive(false);
        if (groundShadow != null) groundShadow.enabled = false;
        if (leftContactShadow != null) leftContactShadow.enabled = rightContactShadow.enabled = false;
    }

    private void OnDestroy()
    {
        if (labelsAnchor != null) Destroy(labelsAnchor.gameObject);
        if (groundShadow == null) return;
        if (Application.isPlaying) Destroy(groundShadow.gameObject);
        else DestroyImmediate(groundShadow.gameObject);
    }

    public void CancelAnimation()
    {
        if (battleAnimation != null) battleAnimation.Cancel();
        if (bladeBar != null) bladeBar.ResetPresentation();
    }

    public void Initialize(RectTransform rootTransform, RectTransform actorRootTransform, Image bodyImage, Image healthFillImage, TextMeshProUGUI nameLabel, TextMeshProUGUI healthLabel, RectTransform buffRow, Image[] buffBackgrounds, TextMeshProUGUI[] buffLabels, TextMeshProUGUI emptyLabel, Image headImage = null)
    {
        root = rootTransform;
        actorRoot = actorRootTransform;
        body = bodyImage;
        head = headImage;
        healthFill = healthFillImage;
        nameText = nameLabel;
        healthText = healthLabel;
        buffIconRow = buffRow;
        buffIconBackgrounds = buffBackgrounds ?? new Image[0];
        buffIconLabels = buffLabels ?? new TextMeshProUGUI[0];
        emptyText = emptyLabel;
    }

    public void Apply(IdleAdventureCombatant combatant)
    {
        if (!HasViewBindings) return;

        bool hasCombatant = combatant != null;
        bool visibleCombatant = hasCombatant && !combatant.IsDefeated;
        if (!hasCombatant) CancelAnimation();
        root.gameObject.SetActive(hasCombatant);
        actorRoot.gameObject.SetActive(visibleCombatant || battleAnimation != null && battleAnimation.IsPlayingDeath);
        healthFill.transform.parent.gameObject.SetActive(visibleCombatant);
        nameText.gameObject.SetActive(false);
        buffIconRow.gameObject.SetActive(visibleCombatant);
        emptyText.gameObject.SetActive(false);

        Image rootImage = root.GetComponent<Image>();
        if (rootImage != null)
        {
            rootImage.color = new Color(0f, 0f, 0f, 0f);
            rootImage.raycastTarget = false;
        }

        if (!visibleCombatant)
        {
            if (bladeBar != null) bladeBar.ResetPresentation();
            ClearBuffIcons();
            return;
        }

        if (!gameObject.activeInHierarchy)
        {
            CancelAnimation();
            return;
        }

        BattleHealthBar bar = EnsureBladeBar();
        bar.SetPauseQuery(pauseQuery);
        bar.Configure(BattleUnitRankTable.Resolve(combatant), combatant.IsFriendly, healthText);
        bar.SetHealth(combatant.GetInstanceID(), combatant.CurrentHealth, combatant.MaxHealth);
        ApplyBuffIcons(combatant);
        if (battleAnimation == null) battleAnimation = GetComponent<BattleActorAnimation>();
        if (battleAnimation == null) battleAnimation = gameObject.AddComponent<BattleActorAnimation>();
        if (battleAnimation.IsDead || battleAnimation.IsPlayingDeath) battleAnimation.Cancel();
        bool battleAnimated = battleAnimation.Bind(body, actorRoot, combatant.GetInstanceID().ToString(), combatant.IsFriendly, pauseQuery, combatant.AppearanceKey, combatant.CombatantName);
        EnsureGroundShadow();
        if (battleAnimated)
        {
            if (head != null) head.gameObject.SetActive(false);
            ConfigureActorHeader();
            return;
        }
        bool animated = combatant.IsFriendly && CharacterIdleImage.TryApply(body, combatant.AppearanceKey, combatant.CombatantName);
        if (animated && body.TryGetComponent(out CharacterIdleImage idlePlayer)) idlePlayer.BattlePlayback = battleAnimation;
        if (!combatant.IsFriendly) CharacterIdleImage.Stop(body);
        Sprite actorSprite = animated ? body.sprite : combatant.IsFriendly ? ResolveCharacterSprite() : ResolveEnemySprite(combatant);
        bool useActorSprite = actorSprite != null;
        body.sprite = actorSprite;
        body.type = Image.Type.Simple;
        body.preserveAspect = useActorSprite;
        body.color = useActorSprite ? Color.white : combatant.BaseColor;
        ConfigureBodyLayout(useActorSprite);
        ConfigureEnemyFacing(actorSprite != null && actorSprite == forestSlimeSprite);
        if (head != null)
        {
            head.gameObject.SetActive(!useActorSprite);
            if (!useActorSprite) head.color = combatant.IsFriendly ? new Color(0.93f, 0.78f, 0.55f, 1f) : new Color(0.55f, 0.24f, 0.24f, 1f);
        }
        ConfigureActorHeader();
    }

    private static Sprite ResolveCharacterSprite()
    {
        if (characterSprite == null) characterSprite = Resources.Load<Sprite>(CharacterSpriteResourceName);
        return characterSprite;
    }

    private void ConfigureActorHeader()
    {
        RectTransform healthBar = healthFill.transform.parent as RectTransform;
        if (healthBar == null || actorRoot == null) return;
        bool animated = battleAnimation.IsBound;
        float bodyHeight = animated ? battleAnimation.BodyHeight : body.rectTransform.rect.height;
        headerBodyHeight = bodyHeight;
        float scale = Mathf.Max(.01f, battleAnimation.PresentationScale);
        Vector2 sole;
        if (animated)
        {
            var appearance = battleAnimation.Appearance;
            var idle = appearance.Resolve(BattleAnimationAction.Idle);
            Vector2 contact = appearance.groundShadowOffset;
            if (idle != null && idle.TryGetFootContacts(0, out Vector2 left, out Vector2 right))
            {
                contact = new Vector2((left.x + right.x) * .5f, Mathf.Min(left.y, right.y));
            }
            // Resting soles stay stable through jumps, weapon swings and hit recoil.
            contact.x *= battleAnimation.SourceMirror;
            sole = battleAnimation.BaseBodyPosition + contact * bodyHeight;
        }
        else
        {
            GetStaticSoles(out Vector2 left, out Vector2 right);
            left = actorRoot.InverseTransformPoint(body.rectTransform.TransformPoint(left));
            right = actorRoot.InverseTransformPoint(body.rectTransform.TransformPoint(right));
            sole = new Vector2((left.x + right.x) * .5f, Mathf.Min(left.y, right.y)) - actorRoot.rect.min.y * Vector2.up;
        }

        BattleUnitRank rank = bladeBar != null ? bladeBar.Rank : BattleUnitRank.Friendly;
        Vector2 barSize = BattleHealthBar.SizeFor(rank) * scale;
        float width = barSize.x;
        float height = barSize.y;
        float gap = Mathf.Clamp(bodyHeight * .06f, 8f * scale, 16f * scale);
        Vector2 bottom = sole - Vector2.up * (gap + height);
        headerSoleOffset = sole;
        ConfigureHeaderRect(healthBar, width, height, bottom);
        buffRowPosition = bottom - Vector2.up * (4f * scale);
        if (buffIconRow != null) buffIconRow.anchoredPosition = buffRowPosition;
    }

    private BattleHealthBar EnsureBladeBar()
    {
        if (bladeBar != null) return bladeBar;
        RectTransform frame = (RectTransform)healthFill.transform.parent;
        Image oldFrame = frame.GetComponent<Image>();
        if (oldFrame != null) oldFrame.enabled = false;
        healthFill.enabled = false;
        var go = new GameObject("Blade Health Bar", typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleHealthBar));
        go.layer = frame.gameObject.layer;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(frame, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.SetAsFirstSibling();
        healthText.transform.SetAsLastSibling();
        bladeBar = go.GetComponent<BattleHealthBar>();
        return bladeBar;
    }

    /// <summary>多段攻击每段命中时由 BattleDamagePlan 调用，血条先逐段下降。</summary>
    public void PreviewHealthDrop(long amount, bool finalSegment)
    {
        if (bladeBar != null && bladeBar.isActiveAndEnabled) bladeBar.PreviewDamage(amount, finalSegment);
    }

    private void ConfigureHeaderRect(RectTransform rect, float width, float height, Vector2 bottom)
    {
        if (rect.parent != LabelParent) rect.SetParent(LabelParent, false);
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = bottom;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        rect.SetAsLastSibling();
    }

    private static void ConfigureLabel(TextMeshProUGUI label, float minimum, float maximum)
    {
        if (label == null) return;
        label.enableWordWrapping = false;
        label.enableAutoSizing = true;
        label.fontSizeMin = minimum;
        label.fontSizeMax = maximum;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.margin = new Vector4(2f, 0f, 2f, 0f);
        label.richText = false;
        label.outlineColor = new Color32(60, 41, 26, 230);
        label.outlineWidth = 0.14f;
    }

    private static Sprite ResolveEnemySprite(IdleAdventureCombatant combatant)
    {
        if (combatant == null || combatant.IsFriendly) return null;

        EnsureEnemySpritesLoaded();
        string combatantName = (combatant.CombatantName ?? string.Empty) + " " + (combatant.AppearanceKey ?? string.Empty);
        if (ContainsIdentity(combatantName, "森林史莱姆", "forest_slime", "enemy_forest_slime"))
        {
            return forestSlimeSprite;
        }

        if (ContainsIdentity(combatantName, "烈焰掠夺者", "fire_raider", "enemy_fire_raider", "flame_raider"))
        {
            return flameRaiderSprite;
        }

        if (unknownEnemySprite == null) unknownEnemySprite = Resources.Load<Sprite>("UI/ApprovedUi/Controls/11_unknown_enemy_candidate");
        return unknownEnemySprite;
    }

    private static bool ContainsIdentity(string value, params string[] identities)
    {
        for (int i = 0; i < identities.Length; i++)
        {
            if (value.IndexOf(identities[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }

        return false;
    }

    private static void EnsureEnemySpritesLoaded()
    {
        if (forestSlimeSprite == null) forestSlimeSprite = Resources.Load<Sprite>(ForestSlimeSpriteResourceName);
        if (flameRaiderSprite == null) flameRaiderSprite = Resources.Load<Sprite>(FlameRaiderSpriteResourceName);
    }

    private void ConfigureBodyLayout(bool useActorSprite)
    {
        if (body == null) return;

        RectTransform bodyRect = body.rectTransform;
        bodyRect.anchorMin = new Vector2(0.5f, 0f);
        bodyRect.anchorMax = new Vector2(0.5f, 0f);
        bodyRect.pivot = new Vector2(.5f, .5f);
        float scale = battleAnimation.SideSizeMultiplier;
        float halfWidth = (useActorSprite ? 62f : 34f) * scale;
        float bottom = useActorSprite ? -8f : 4f;
        float height = (useActorSprite ? 124f : 62f) * scale;
        bodyRect.offsetMin = new Vector2(-halfWidth, bottom);
        bodyRect.offsetMax = new Vector2(halfWidth, bottom + height);

        if (head != null && !useActorSprite)
        {
            RectTransform headRect = head.rectTransform;
            headRect.offsetMin = new Vector2(-24f, -31f) * scale;
            headRect.offsetMax = new Vector2(24f, 17f) * scale;
        }
    }

    private void ConfigureEnemyFacing(bool flipForestSlime)
    {
        if (body == null) return;

        if (actorRoot != null)
        {
            actorRoot.localRotation = Quaternion.identity;
            actorRoot.localScale = Vector3.one;
        }

        RectTransform bodyRect = body.rectTransform;
        bodyRect.localRotation = Quaternion.identity;
        float scaleX = flipForestSlime ? ForestSlimeHorizontalFlipScaleX : EnemyFacingRightScaleX;
        bodyRect.localScale = new Vector3(scaleX, 1f, 1f);
    }

    private void ApplyBuffIcons(IdleAdventureCombatant combatant)
    {
        ClearBuffIcons();
        if (combatant == null || combatant.ActiveBuffs == null) return;

        int visibleCount = Mathf.Min(MaxVisibleBuffIcons, combatant.ActiveBuffs.Count);
        ConfigureBuffRow(visibleCount);
        for (int i = 0; i < visibleCount; i++)
        {
            IdleAdventureBattleBuff buff = combatant.ActiveBuffs[i];
            if (buff == null) continue;

            Image background = buffIconBackgrounds[i];
            TextMeshProUGUI label = buffIconLabels[i];
            if (background == null || label == null) continue;

            background.gameObject.SetActive(true);
            background.color = ResolveBuffColor(buff);
            EnsureBuffArtwork(i);
            string iconKey = ResolveBuffIconKey(buff);
            Sprite icon = LoadStatusSprite(iconKey);
            buffArtwork[i].sprite = icon;
            buffArtwork[i].enabled = icon != null;
            buffFallbackLabels[i].text = icon == null ? buff.EffectShortLabel : string.Empty;
            bool bakedDirection = iconKey == "01_fire_damage_down_candidate" || iconKey == "02_fire_field_up_candidate";
            buffDirectionLabels[i].text = bakedDirection ? string.Empty : ResolveBuffDirection(buff);
            label.text = buff.RemainingRounds.ToString(CultureInfo.InvariantCulture);
            RectTransform badge = (RectTransform)label.transform.parent;
            badge.sizeDelta = new Vector2(Mathf.Max(13f, 5f + label.text.Length * 6f), 14f);
        }
    }

    private void ConfigureBuffRow(int visibleCount = MaxVisibleBuffIcons)
    {
        if (buffIconRow == null) return;
        if (LabelParent != null && buffIconRow.parent != LabelParent) buffIconRow.SetParent(LabelParent, false);
        buffIconRow.anchorMin = buffIconRow.anchorMax = new Vector2(.5f, 0f);
        buffIconRow.pivot = new Vector2(.5f, 1f);
        buffIconRow.sizeDelta = new Vector2(Mathf.Max(1, visibleCount) * 32f - 4f, 32f);
        buffIconRow.anchoredPosition = buffRowPosition;
        for (int index = 0; index < Mathf.Min(MaxVisibleBuffIcons, buffIconBackgrounds.Length); index++)
        {
            Image background = buffIconBackgrounds[index];
            if (background == null) continue;
            RectTransform iconRect = background.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0f, 1f);
            iconRect.sizeDelta = new Vector2(28f, 28f);
            iconRect.anchoredPosition = new Vector2(index * 32f, 0f);
            background.raycastTarget = false;
        }
    }

    private void EnsureBuffArtwork(int index)
    {
        if (buffArtwork[index] != null) return;
        Image background = buffIconBackgrounds[index];
        TextMeshProUGUI rounds = buffIconLabels[index];
        GameObject artworkObject = new GameObject("StatusArtwork", typeof(RectTransform), typeof(Image));
        artworkObject.transform.SetParent(background.transform, false);
        Image artwork = artworkObject.GetComponent<Image>();
        artwork.raycastTarget = false;
        artwork.preserveAspect = true;
        artwork.color = Color.white;
        artwork.rectTransform.anchorMin = Vector2.zero;
        artwork.rectTransform.anchorMax = Vector2.one;
        artwork.rectTransform.offsetMin = new Vector2(1f, 1f);
        artwork.rectTransform.offsetMax = new Vector2(-1f, -1f);
        buffArtwork[index] = artwork;
        buffFallbackLabels[index] = CreateBuffLabel("StatusCategory", background.transform, rounds, 15f);
        buffDirectionLabels[index] = CreateBuffLabel("StatusDirection", background.transform, rounds, 13f);
        TextMeshProUGUI direction = buffDirectionLabels[index];
        direction.alignment = TextAlignmentOptions.TopLeft;
        direction.margin = Vector4.zero;
        GameObject badgeObject = new GameObject("RemainingRoundsBadge", typeof(RectTransform), typeof(Image));
        badgeObject.transform.SetParent(background.transform, false);
        Image badge = badgeObject.GetComponent<Image>();
        badge.color = new Color(.12f, .1f, .08f, .94f);
        badge.raycastTarget = false;
        RectTransform badgeRect = badge.rectTransform;
        badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(1f, 0f);
        badgeRect.pivot = new Vector2(1f, 0.65f);
        badgeRect.anchoredPosition = new Vector2(2f, 0f);
        rounds.rectTransform.SetParent(badgeRect, false);
        rounds.rectTransform.anchorMin = Vector2.zero;
        rounds.rectTransform.anchorMax = Vector2.one;
        rounds.rectTransform.offsetMin = rounds.rectTransform.offsetMax = Vector2.zero;
        ConfigureLabel(rounds, 9f, 11f);
        rounds.alignment = TextAlignmentOptions.Center;
        rounds.margin = Vector4.zero;
        rounds.color = new Color(1f, .97f, .88f, 1f);
        rounds.raycastTarget = false;
    }

    private static TextMeshProUGUI CreateBuffLabel(string objectName, Transform parent, TextMeshProUGUI template, float size)
    {
        GameObject labelObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.font = template.font;
        label.fontSize = size;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(1f, .97f, .88f, 1f);
        label.outlineColor = new Color32(36, 30, 22, 255);
        label.outlineWidth = .18f;
        label.enableWordWrapping = false;
        label.richText = false;
        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        return label;
    }

    private static string ResolveBuffIconKey(IdleAdventureBattleBuff buff)
    {
        int modifiedStats = (buff.AttackModifier != 0 ? 1 : 0) + (buff.DefenseModifier != 0 ? 1 : 0) + (buff.SpeedModifier != 0 ? 1 : 0);
        if (modifiedStats == 0 && Mathf.Approximately(buff.NormalDamageMultiplier, 0f) && buff.AffectedAttribute == IdleAdventureDamageAttribute.Fire)
        {
            if (buff.EffectType == IdleAdventureBuffEffectType.AttributeDamage && buff.AttributeDamageMultiplier < 0f)
                return "01_fire_damage_down_candidate";
            if (buff.EffectType == IdleAdventureBuffEffectType.Field && buff.AttributeDamageMultiplier > 0f)
                return "02_fire_field_up_candidate";
        }
        if (buff.EffectType == IdleAdventureBuffEffectType.NormalDamage && modifiedStats == 1
            && Mathf.Approximately(buff.NormalDamageMultiplier, 0f) && Mathf.Approximately(buff.AttributeDamageMultiplier, 0f))
        {
            if (buff.AttackModifier != 0) return "Attack";
            if (buff.DefenseModifier != 0) return "Defense";
            if (buff.SpeedModifier != 0) return "Speed";
        }
        if (modifiedStats == 0 && buff.EffectType == IdleAdventureBuffEffectType.NormalDamage
            && Mathf.Approximately(buff.AttributeDamageMultiplier, 0f) && !Mathf.Approximately(buff.NormalDamageMultiplier, 0f))
            return "Attack";
        return string.Empty;
    }

    private static string ResolveBuffDirection(IdleAdventureBattleBuff buff)
    {
        bool positive = buff.AttackModifier > 0 || buff.DefenseModifier > 0 || buff.SpeedModifier > 0
            || buff.NormalDamageMultiplier > 0f || buff.AttributeDamageMultiplier > 0f;
        bool negative = buff.AttackModifier < 0 || buff.DefenseModifier < 0 || buff.SpeedModifier < 0
            || buff.NormalDamageMultiplier < 0f || buff.AttributeDamageMultiplier < 0f;
        return positive == negative ? string.Empty : positive ? "↑" : "↓";
    }

    private static Sprite LoadStatusSprite(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (!statusSprites.TryGetValue(key, out Sprite sprite) || sprite == null)
        {
            sprite = Resources.Load<Sprite>("UI/ApprovedUi/States/" + key);
            statusSprites[key] = sprite;
        }
        return sprite;
    }

    private void ClearBuffIcons()
    {
        int count = Mathf.Min(buffIconBackgrounds.Length, buffIconLabels.Length);
        for (int i = 0; i < count; i++)
        {
            if (buffIconBackgrounds[i] != null) buffIconBackgrounds[i].gameObject.SetActive(false);
            if (buffIconLabels[i] != null) buffIconLabels[i].text = string.Empty;
        }
    }

    private static Color ResolveBuffColor(IdleAdventureBattleBuff buff)
    {
        if (buff == null) return new Color(0.2f, 0.22f, 0.25f, 1f);

        if (buff.EffectType == IdleAdventureBuffEffectType.Field)
        {
            return buff.Category == IdleAdventureBuffCategory.Benefit
                ? new Color(0.24f, 0.55f, 0.78f, 1f)
                : new Color(0.64f, 0.38f, 0.18f, 1f);
        }

        if (buff.EffectType == IdleAdventureBuffEffectType.AttributeDamage)
        {
            return buff.Category == IdleAdventureBuffCategory.Benefit
                ? new Color(0.36f, 0.66f, 0.28f, 1f)
                : new Color(0.78f, 0.26f, 0.34f, 1f);
        }

        if (buff.Category == IdleAdventureBuffCategory.Benefit)
        {
            return buff.Source == IdleAdventureBuffSource.CharacterSkill
                ? new Color(0.18f, 0.62f, 0.36f, 1f)
                : new Color(0.22f, 0.48f, 0.72f, 1f);
        }

        return buff.Source == IdleAdventureBuffSource.CharacterSkill
            ? new Color(0.76f, 0.22f, 0.26f, 1f)
            : new Color(0.66f, 0.42f, 0.18f, 1f);
    }

    private bool HasBuffIconBindings()
    {
        if (buffIconRow == null || buffIconBackgrounds == null || buffIconLabels == null) return false;
        if (buffIconBackgrounds.Length < MaxVisibleBuffIcons || buffIconLabels.Length < MaxVisibleBuffIcons) return false;

        for (int i = 0; i < MaxVisibleBuffIcons; i++)
        {
            if (buffIconBackgrounds[i] == null || buffIconLabels[i] == null) return false;
        }

        return true;
    }
}
