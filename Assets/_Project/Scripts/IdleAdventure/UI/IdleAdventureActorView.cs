using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class IdleAdventureActorView : MonoBehaviour
{
    private const int MaxVisibleBuffIcons = 6;
    private const float EnemyFacingRightScaleX = 1f;
    private const float ForestSlimeHorizontalFlipScaleX = -1f;
    private const string CharacterSpriteResourceName = "IdleAdventureCharacter";
    private const string ForestSlimeSpriteResourceName = "IdleAdventureEnemyForestSlime";
    private const string FlameRaiderSpriteResourceName = "IdleAdventureEnemyFlameRaider";

    private static Sprite forestSlimeSprite;
    private static Sprite flameRaiderSprite;
    private static Sprite characterSprite;

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

    public RectTransform Root => root != null ? root : transform as RectTransform;
    public RectTransform ActorRoot => actorRoot;
    public Image Body => body;
    public bool HasViewBindings => actorRoot != null && body != null && healthFill != null && nameText != null && healthText != null && HasBuffIconBindings() && emptyText != null;

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
        root.gameObject.SetActive(hasCombatant);
        actorRoot.gameObject.SetActive(visibleCombatant);
        healthFill.transform.parent.gameObject.SetActive(visibleCombatant);
        nameText.gameObject.SetActive(visibleCombatant);
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

        nameText.text = FrontendTextLocalization.Normalize(combatant.CombatantName);
        healthFill.fillAmount = combatant.HealthRatio;
        healthFill.color = combatant.IsFriendly ? new Color(0.23f, 0.74f, 0.43f, 1f) : new Color(0.88f, 0.25f, 0.22f, 1f);
        healthText.text = $"{combatant.CurrentHealth}/{combatant.MaxHealth}";
        ApplyBuffIcons(combatant);
        Sprite actorSprite = combatant.IsFriendly ? ResolveCharacterSprite() : ResolveEnemySprite(combatant);
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
    }

    private static Sprite ResolveCharacterSprite()
    {
        if (characterSprite == null) characterSprite = Resources.Load<Sprite>(CharacterSpriteResourceName);
        return characterSprite;
    }

    private static Sprite ResolveEnemySprite(IdleAdventureCombatant combatant)
    {
        if (combatant == null || combatant.IsFriendly) return null;

        EnsureEnemySpritesLoaded();
        string combatantName = combatant.CombatantName ?? string.Empty;
        if (ContainsIdentity(combatantName, "森林史莱姆", "forest_slime", "enemy_forest_slime") || combatant.AttributeType == IdleAdventureDamageAttribute.Wood)
        {
            return forestSlimeSprite;
        }

        if (ContainsIdentity(combatantName, "烈焰掠夺者", "fire_raider", "enemy_fire_raider") || combatant.AttributeType == IdleAdventureDamageAttribute.Fire)
        {
            return flameRaiderSprite;
        }

        return null;
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
        for (int i = 0; i < visibleCount; i++)
        {
            IdleAdventureBattleBuff buff = combatant.ActiveBuffs[i];
            if (buff == null) continue;

            Image background = buffIconBackgrounds[i];
            TextMeshProUGUI label = buffIconLabels[i];
            if (background == null || label == null) continue;

            background.gameObject.SetActive(true);
            background.color = ResolveBuffColor(buff);
            label.text = FrontendTextLocalization.Normalize($"{buff.EffectShortLabel}{buff.RemainingRounds}");
        }
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
