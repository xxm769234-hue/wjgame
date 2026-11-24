using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class IdleAdventureStartPreviewLayout
{
    private readonly BattleAnimationSequence[] enemyIdles = new BattleAnimationSequence[EnemyGroupCount];
    private readonly Image[] enemyShadows = new Image[EnemyGroupCount];
    private readonly BattleAnimationCatalog.Entry[] enemyLooks = new BattleAnimationCatalog.Entry[EnemyGroupCount];

    private void RefreshEnemyField(IdleAdventureStageEnemiesResponse preview)
    {
        RectTransform field = (RectTransform)enemySummaryText.transform.parent;
        Image scenery = FindImage(field, "Enemy Preview Background");
        if (bookSkinReady)
        {
            scenery.gameObject.SetActive(false);
            RectTransform viewport = layoutRoot.Find("Enemy Hall Viewport") as RectTransform;
            if (viewport == null)
            {
                viewport = CreateRect("Enemy Hall Viewport", layoutRoot);
                Anchor(viewport, new Vector2(1073f / 2048f, 1f - 650f / 1152f),
                    new Vector2(1708f / 2048f, 1f - 229f / 1152f));
                var outline = viewport.gameObject.AddComponent<IdleAdventureFormationHallMask>();
                outline.Configure(new[] { new Vector2(.005f,1f), new Vector2(.93f,1f), Vector2.right, Vector2.zero });
                viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

                Image background = CreateImage("Stage Scenery", viewport, Color.clear);
                Stretch(background.rectTransform);
            }
            viewport.SetAsFirstSibling();
            scenery = FindImage(viewport, "Stage Scenery");
        }
        scenery.color = Color.clear;
        scenery.raycastTarget = false;
        scenery.gameObject.SetActive(preview != null);
        if (preview != null)
            BattleSceneAnimation.ConfigurePreview(scenery, BattleSceneAnimation.StageResource(preview.stage_presentation, preview.ResolvedStageName));

        var occupied = new IdleAdventureEnemyResponse[EnemyGroupCount];
        int count = 0;
        if (preview?.slots != null)
            for (int j = 0; j < preview.slots.Length; j++)
            {
                var slot = preview.slots[j];
                if (slot?.enemy == null) continue;
                int index = slot.ResolvedSlotIndex;
                if (index == 0 && occupied[0] != null) index = j;
                if (index < 0 || index >= occupied.Length || occupied[index] != null) continue;
                if (slot.enemy.ResolvedDefinitionId <= 0 && string.IsNullOrWhiteSpace(slot.enemy.ResolvedCombatantKey)
                    && string.IsNullOrWhiteSpace(slot.enemy.ResolvedCombatantName)) continue;
                occupied[index] = slot.enemy;
                count++;
            }

        enemySummaryText.text = preview == null ? "正在查看敌情…" : $"关卡敌人  ·  {count} 名";
        Anchor(enemySummaryText.rectTransform, new Vector2(.24f,.89f), new Vector2(.76f,1f));
        StyleFieldCaption(enemySummaryText, 19f);
        Image headingPlate = FieldPlate(field, "Enemy Heading Plate");
        Anchor(headingPlate.rectTransform, new Vector2(.325f,.90f), new Vector2(.675f,.99f));
        headingPlate.transform.SetAsLastSibling();
        for (int row = 0; row < 3; row++)
            for (int col = 1; col >= 0; col--)
            {
                int i = row + col * 3;
                var enemy = occupied[i];
                var group = (RectTransform)enemyImages[i].transform.parent;
                group.gameObject.SetActive(enemy != null);
                enemyIdles[i] = null;
                enemyLooks[i] = null;
                if (enemy == null) continue;
                var look = BattleActorAnimation.FindEnemyEntry(enemy.ResolvedDefinitionId, enemy.ResolvedModelKey,
                    enemy.ResolvedCombatantKey, enemy.icon, enemy.ResolvedCombatantName);
                enemyLooks[i] = look;
                enemyIdles[i] = look?.Resolve(BattleAnimationAction.Idle);
                Vector2 foot = IdleAdventureModeController.BattleSlotAnchor(false, i, count == 1, look != null && look.visualScale > 1.5f);
                foot.x = (foot.x - .56f) / .44f;
                Anchor(group, foot, foot);
                group.SetAsLastSibling();
                if (enemyShadows[i] == null)
                {
                    var shadow = CreateImage("Foot Shadow", group, Color.black);
                    ApplyResourceSprite(shadow, "IdleAdventureUI/FormationFootShadow", false);
                    shadow.color = new Color(0,0,0,.44f);
                    shadow.raycastTarget = false;
                    shadow.transform.SetAsFirstSibling();
                    enemyShadows[i] = shadow;
                }
                var plate = FieldPlate(group, "Name Plate");
                var plateRect = plate.rectTransform;
                Anchor(plateRect, Vector2.zero, Vector2.one);
                plateRect.pivot = new Vector2(.5f,1f);
                plateRect.sizeDelta = new Vector2(131f,22f);
                plateRect.anchoredPosition = new Vector2(0,-2f);
                plate.transform.SetAsLastSibling();
                var label = enemyLabels[i];
                label.transform.SetAsLastSibling();
                string name = enemy.ResolvedCombatantName;
                label.text = FrontendTextLocalization.Normalize((string.IsNullOrWhiteSpace(name) ? "敌人" : name) +
                    (enemy.level > 0 ? $"  Lv.{enemy.level}" : ""));
                StyleFieldCaption(label, 14f);
                Anchor(label.rectTransform, Vector2.zero, Vector2.one);
                label.rectTransform.pivot = new Vector2(.5f,1f);
                label.rectTransform.sizeDelta = new Vector2(153f,21f);
                label.rectTransform.anchoredPosition = new Vector2(0,-2f);
                enemyImages[i].enabled = enemyIdles[i] != null && enemyIdles[i].IsValid;
                enemyShadows[i].enabled = enemyImages[i].enabled;
            }
        headingPlate.transform.SetAsLastSibling();
        enemySummaryText.transform.SetAsLastSibling();
        DrawEnemyIdles();
    }

    private Image FieldPlate(RectTransform parent, string objectName)
    {
        Image plate = FindImage(parent, objectName);
        if (plate == null)
        {
            plate = CreateImage(objectName, parent, new Color32(48,36,34,195));
            plate.sprite = Resources.Load<Sprite>("IdleAdventureUI/RoundedPanel");
            plate.type = Image.Type.Sliced;
            plate.raycastTarget = false;
        }
        return plate;
    }

    private static void StyleFieldCaption(TextMeshProUGUI label, float size)
    {
        label.color = new Color32(255,245,221,255);
        label.fontSize = size;
        label.fontSizeMax = size;
        label.fontSizeMin = size * .8f;
        label.enableAutoSizing = true;
        label.enableWordWrapping = false;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        var outline = label.GetComponent<Outline>();
        if (outline == null) outline = label.gameObject.AddComponent<Outline>();
        outline.enabled = true;
        outline.effectColor = new Color32(43,30,24,235);
        outline.effectDistance = new Vector2(1.2f,-1.2f);
    }

    private void Update() => DrawEnemyIdles();

    private void DrawEnemyIdles()
    {
        if (enemySummaryText == null || !enemySummaryText.isActiveAndEnabled) return;
        float fieldHeight = ((RectTransform)enemySummaryText.transform.parent).rect.height;
        for (int i = 0; i < enemyIdles.Length; i++)
        {
            var clip = enemyIdles[i];
            if (clip == null || !clip.IsValid) continue;
            var look = enemyLooks[i];
            int frame = Mathf.Clamp((int)BattleActorAnimation.SourceFrame(clip, Mathf.Repeat(Time.time + i * .143f, clip.Duration)),0,clip.FrameCount-1);
            Sprite sprite = clip.frames[frame];
            if (sprite == null) continue;
            Image body = enemyImages[i];
            float height = fieldHeight * .165f * look.visualScale;
            float mirror = look.sourceFacingRight ? -1f : 1f;
            var rect = body.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f);
            rect.pivot = sprite.pivot / sprite.rect.size;
            rect.sizeDelta = sprite.rect.size * (height / (sprite.pixelsPerUnit * look.referenceBodyHeight));
            rect.localScale = new Vector3(mirror,1f,1f);
            rect.anchoredPosition = Vector2.zero;
            body.sprite = sprite;
            body.color = Color.white;
            Vector2 center = look.groundShadowOffset;
            float width = .62f;
            if (clip.TryGetFootContacts(frame, out Vector2 left, out Vector2 right))
            {
                center = (left + right) * .5f;
                width = Mathf.Clamp((right-left).magnitude + .31f,.39f,1.28f);
            }
            var shadow = enemyShadows[i].rectTransform;
            shadow.anchorMin = shadow.anchorMax = new Vector2(.5f,.5f);
            shadow.anchoredPosition = Vector2.Scale(center,new Vector2(mirror,1f)) * height;
            shadow.sizeDelta = new Vector2(width * 1.12f,.15f) * height;
        }
    }
}
