using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class IdleAdventureActorView : MonoBehaviour
{
    private const int MaxVisibleBuffIcons = 6;
    private const float HealthBarHeight = 18f;
    private const float NameHeight = 26f;
    private const float HealthFillInset = 1.5f;
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
    private readonly Image[] buffArtwork = new Image[MaxVisibleBuffIcons];
    private readonly TextMeshProUGUI[] buffFallbackLabels = new TextMeshProUGUI[MaxVisibleBuffIcons];
    private readonly TextMeshProUGUI[] buffDirectionLabels = new TextMeshProUGUI[MaxVisibleBuffIcons];
    private Image nameBackdrop;
    private float healthRatio = 1f;

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
        if (buffIconRow != null)
        {
            ConfigureBuffRow();
        }
    }

    private void LateUpdate()
    {
        if (battleAnimation != null && battleAnimation.IsBound && !Mathf.Approximately(headerBodyHeight, battleAnimation.BodyHeight))
            ConfigureActorHeader();
    }

    public void CancelAnimation()
    {
        if (battleAnimation != null) battleAnimation.Cancel();
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
        ConfigureTextLayout();
    }

    public void Apply(IdleAdventureCombatant combatant)
    {
        if (!HasViewBindings) return;
        ConfigureTextLayout();

        bool hasCombatant = combatant != null;
        bool visibleCombatant = hasCombatant && !combatant.IsDefeated;
        if (!hasCombatant) CancelAnimation();
        root.gameObject.SetActive(hasCombatant);
        actorRoot.gameObject.SetActive(visibleCombatant || battleAnimation != null && battleAnimation.IsPlayingDeath);
        healthFill.transform.parent.gameObject.SetActive(visibleCombatant);
        nameText.gameObject.SetActive(visibleCombatant);
        if (nameBackdrop != null) nameBackdrop.gameObject.SetActive(visibleCombatant);
        healthText.gameObject.SetActive(visibleCombatant);
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
            ClearBuffIcons();
            return;
        }

        if (!gameObject.activeInHierarchy)
        {
            CancelAnimation();
            return;
        }

        nameText.text = FrontendTextLocalization.Normalize(combatant.CombatantName);
        SetHealthRatio(combatant.HealthRatio);
        healthFill.color = combatant.IsFriendly ? new Color(0.23f, 0.74f, 0.43f, 1f) : new Color(0.88f, 0.25f, 0.22f, 1f);
        healthText.text = $"{combatant.CurrentHealth}/{combatant.MaxHealth}";
        ApplyBuffIcons(combatant);
        if (battleAnimation == null) battleAnimation = GetComponent<BattleActorAnimation>();
        if (battleAnimation == null) battleAnimation = gameObject.AddComponent<BattleActorAnimation>();
        if (battleAnimation.IsDead || battleAnimation.IsPlayingDeath) battleAnimation.Cancel();
        bool battleAnimated = battleAnimation.Bind(body, actorRoot, combatant.GetInstanceID().ToString(), combatant.IsFriendly, pauseQuery, combatant.AppearanceKey, combatant.CombatantName);
        if (battleAnimated)
        {
            if (head != null) head.gameObject.SetActive(false);
            ConfigureActorHeader();
            return;
        }
        bool animated = combatant.IsFriendly && CharacterIdleImage.TryApply(body, combatant.AppearanceKey, combatant.CombatantName);
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

    private void ConfigureTextLayout()
    {
        ConfigureLabel(nameText, 18f, 22f);
        ConfigureLabel(healthText, 13f, 15f);
        foreach (TextMeshProUGUI label in buffIconLabels)
        {
            ConfigureLabel(label, 12f, 14f);
            if (label != null) label.margin = Vector4.zero;
        }
    }

    private void ConfigureActorHeader()
    {
        RectTransform healthBar = healthFill.transform.parent as RectTransform;
        if (healthBar == null || actorRoot == null) return;
        float bodyHeight = battleAnimation != null && battleAnimation.IsBound ? battleAnimation.BodyHeight : 124f;
        headerBodyHeight = bodyHeight;
        float fit = battleAnimation != null && battleAnimation.IsBound ? battleAnimation.PresentationScale : 1f;
        float width = battleAnimation != null && battleAnimation.IsBound
            ? (bodyHeight / Mathf.Max(0.01f, fit) > BattleActorAnimation.ReferenceBodyHeight * 1.5f ? 200f : 128f) * fit : 128f;
        float barBottom = bodyHeight + 12f;
        ConfigureHeaderRect(healthBar, width, HealthBarHeight, barBottom);
        ConfigureHeaderRect(nameText.rectTransform, width + 12f, NameHeight, barBottom + HealthBarHeight + 4f);
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = new Color(0.98f, 0.96f, 0.9f, 1f);
        nameText.raycastTarget = false;
        ConfigureNameBackdrop(width + 12f, barBottom + HealthBarHeight + 4f);
        Image healthBackground = healthBar.GetComponent<Image>();
        if (healthBackground != null)
        {
            healthBackground.color = new Color(0.06f, 0.07f, 0.1f, 0.88f);
            healthBackground.raycastTarget = false;
        }
        ApplyHealthFillLayout();
        healthFill.raycastTarget = false;
        healthText.rectTransform.anchorMin = Vector2.zero;
        healthText.rectTransform.anchorMax = Vector2.one;
        healthText.rectTransform.offsetMin = new Vector2(2f, 0f);
        healthText.rectTransform.offsetMax = new Vector2(-2f, 0f);
        healthText.alignment = TextAlignmentOptions.Center;
        healthText.raycastTarget = false;
    }

    private void SetHealthRatio(float ratio)
    {
        healthRatio = Mathf.Clamp01(ratio);
        ApplyHealthFillLayout();
    }

    // The fill has no sprite, and a sprite-less UGUI Image ignores fillAmount (it always
    // draws the whole rect), so the bar is sized through its anchors instead.
    private void ApplyHealthFillLayout()
    {
        if (healthFill == null) return;
        RectTransform fill = healthFill.rectTransform;
        healthFill.type = Image.Type.Simple;
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(healthRatio, 1f);
        fill.offsetMin = new Vector2(HealthFillInset, HealthFillInset);
        fill.offsetMax = new Vector2(HealthFillInset - 2f * HealthFillInset * healthRatio, -HealthFillInset);
    }

    private void ConfigureHeaderRect(RectTransform rect, float width, float height, float bottom)
    {
        if (rect.parent != actorRoot) rect.SetParent(actorRoot, false);
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(0f, bottom);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        rect.SetAsLastSibling();
    }

    private void ConfigureNameBackdrop(float width, float bottom)
    {
        if (nameBackdrop == null)
        {
            Transform existing = actorRoot.Find("Name Backdrop");
            if (existing != null) nameBackdrop = existing.GetComponent<Image>();
            if (nameBackdrop == null)
            {
                GameObject backdrop = new GameObject("Name Backdrop", typeof(RectTransform), typeof(Image));
                backdrop.layer = nameText.gameObject.layer;
                backdrop.transform.SetParent(actorRoot, false);
                nameBackdrop = backdrop.GetComponent<Image>();
            }
            nameBackdrop.sprite = Resources.Load<Sprite>("IdleAdventureUI/RoundedPanel");
            nameBackdrop.type = Image.Type.Sliced;
            nameBackdrop.color = new Color(0.16f, 0.14f, 0.12f, 0.72f);
            nameBackdrop.raycastTarget = false;
        }
        ConfigureHeaderRect(nameBackdrop.rectTransform, width, NameHeight, bottom);
        nameBackdrop.gameObject.SetActive(nameText.gameObject.activeSelf);
        nameBackdrop.transform.SetSiblingIndex(nameText.transform.GetSiblingIndex());
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
        bodyRect.offsetMin = useActorSprite ? new Vector2(-62f, -8f) : new Vector2(-34f, 4f);
        bodyRect.offsetMax = useActorSprite ? new Vector2(62f, 116f) : new Vector2(34f, 66f);

        if (head != null && !useActorSprite)
        {
            RectTransform headRect = head.rectTransform;
            headRect.offsetMin = new Vector2(-24f, -31f);
            headRect.offsetMax = new Vector2(24f, 17f);
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
            label.text = Mathf.Max(0, buff.RemainingRounds).ToString();
        }
    }

    private void ConfigureBuffRow(int visibleCount = MaxVisibleBuffIcons)
    {
        if (buffIconRow == null) return;
        buffIconRow.anchorMin = buffIconRow.anchorMax = new Vector2(.5f, 0f);
        buffIconRow.pivot = new Vector2(.5f, 1f);
        buffIconRow.sizeDelta = new Vector2(Mathf.Max(1, visibleCount) * 32f - 4f, 32f);
        buffIconRow.anchoredPosition = new Vector2(0f, -6f);
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
        badgeRect.pivot = new Vector2(1f, 0f);
        badgeRect.anchoredPosition = new Vector2(2f, -2f);
        badgeRect.sizeDelta = new Vector2(24f, 17f);
        rounds.rectTransform.SetParent(badgeRect, false);
        rounds.rectTransform.anchorMin = Vector2.zero;
        rounds.rectTransform.anchorMax = Vector2.one;
        rounds.rectTransform.offsetMin = rounds.rectTransform.offsetMax = Vector2.zero;
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
