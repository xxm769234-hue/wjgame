using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BattleActionEffects : MonoBehaviour
{
    private sealed class Instance
    {
        public BattleEffectBinding binding;
        public Image image;
        public BattleActorAnimation recipient;
        public bool launched;
        public Vector2 launchPosition;
    }

    private readonly List<Instance> instances = new List<Instance>();
    private BattleActorAnimation actor;
    private BattleActorAnimation target;
    private BattleActorAnimation[] allies;
    private BattleActorAnimation[] opponents;
    private RectTransform space;
    private RectTransform underlay;
    private RectTransform overlay;

    public void Begin(BattleActorAnimation source, BattleAnimationSequence sequence, BattleActorAnimation destination, BattleActorAnimation[] teammates, BattleActorAnimation[] enemies, int variantSeed, BattleActorAnimation[] affectedTargets = null)
    {
        Clear();
        actor = source;
        target = destination;
        BattleActorAnimation[] recipients = affectedTargets ?? (destination != null ? new[] { destination } : new BattleActorAnimation[0]);
        allies = FilterRecipients(recipients, teammates, source);
        opponents = FilterRecipients(recipients, enemies, null);
        if (sequence.effects == null || sequence.effects.Length == 0) return;
        Transform ancestor = actor.transform;
        while (ancestor.parent != null && ancestor.name != "Battlefield" && ancestor.GetComponent<Canvas>() == null) ancestor = ancestor.parent;
        space = ancestor as RectTransform;
        if (space == null) return;
        underlay = MakeLayer("Battle Ground Effects", space, true);
        overlay = MakeLayer("Battle Action Effects", space, false);
        foreach (BattleEffectBinding binding in sequence.effects)
        {
            if (binding == null || binding.effect == null || !binding.effect.IsValid) continue;
            if (binding.placement == BattleEffectPlacement.Ally)
                foreach (BattleActorAnimation recipient in allies) AddInstance(binding, recipient);
            else if (binding.placement == BattleEffectPlacement.Enemy || binding.placement == BattleEffectPlacement.EnemyGround || binding.placement == BattleEffectPlacement.Projectile)
                foreach (BattleActorAnimation recipient in opponents) AddInstance(binding, recipient);
            else if (binding.placement == BattleEffectPlacement.PartyGround)
            {
                if (allies.Length > 0) AddInstance(binding, allies[0]);
            }
            else if (binding.placement == BattleEffectPlacement.EnemyFormationGround)
            {
                if (opponents.Length > 0) AddInstance(binding, opponents[0]);
            }
            else AddInstance(binding, source);
        }
    }

    private static BattleActorAnimation[] FilterRecipients(BattleActorAnimation[] candidates, BattleActorAnimation[] party, BattleActorAnimation self)
    {
        List<BattleActorAnimation> matches = new List<BattleActorAnimation>();
        foreach (BattleActorAnimation candidate in candidates)
        {
            if (candidate == null || !candidate.isActiveAndEnabled || candidate.IsDead || matches.Contains(candidate)) continue;
            bool matchesSide = candidate == self;
            if (party != null)
                foreach (BattleActorAnimation member in party) if (member == candidate) { matchesSide = true; break; }
            if (matchesSide) matches.Add(candidate);
        }
        return matches.ToArray();
    }

    private void AddInstance(BattleEffectBinding binding, BattleActorAnimation recipient)
    {
        bool ground = binding.placement == BattleEffectPlacement.PartyGround || binding.placement == BattleEffectPlacement.CasterGround || binding.placement == BattleEffectPlacement.EnemyGround || binding.placement == BattleEffectPlacement.EnemyFormationGround;
        GameObject visual = new GameObject(binding.effect.name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        visual.transform.SetParent(ground ? underlay : overlay, false);
        Image image = visual.GetComponent<Image>();
        image.raycastTarget = false;
        image.enabled = false;
        image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(binding.opacity));
        ConfigureImageSpace(image);
        instances.Add(new Instance { binding = binding, image = image, recipient = recipient });
    }

    public void Sample(float sourceFrame)
    {
        if (actor == null || space == null) return;
        foreach (Instance instance in instances)
        {
            BattleEffectBinding binding = instance.binding;
            bool visible = sourceFrame >= binding.startFrame && sourceFrame < binding.endFrame && instance.recipient != null && instance.recipient.isActiveAndEnabled;
            instance.image.enabled = visible;
            if (!visible) continue;
            BattleEffectDefinition effect = binding.effect;
            float effectFrame;
            if (binding.placement == BattleEffectPlacement.Projectile && sourceFrame < binding.launchFrame)
                effectFrame = Interpolate(sourceFrame, binding.startFrame, binding.launchFrame, binding.firstEffectFrame, binding.launchEffectFrame);
            else if (sourceFrame < binding.contactFrame)
                effectFrame = Interpolate(sourceFrame, binding.placement == BattleEffectPlacement.Projectile ? binding.launchFrame : binding.startFrame, binding.contactFrame, binding.placement == BattleEffectPlacement.Projectile ? binding.launchEffectFrame : binding.firstEffectFrame, binding.contactEffectFrame);
            else
                effectFrame = Interpolate(sourceFrame, binding.contactFrame, binding.endFrame, binding.contactEffectFrame, effect.sequence.FrameCount - 1);
            bool frameAligned = binding.preserveSourceScale && actor.CurrentSequence != null
                && effect.sequence.FrameCount == actor.CurrentSequence.FrameCount
                && Mathf.Approximately(binding.startFrame, 0f)
                && Mathf.Approximately(binding.contactFrame, 0f)
                && binding.firstEffectFrame == 0
                && binding.contactEffectFrame == 0
                && Mathf.Approximately(binding.endFrame, effect.sequence.FrameCount);
            int frame = frameAligned
                ? Mathf.Clamp(Mathf.FloorToInt(sourceFrame), 0, effect.sequence.FrameCount - 1)
                : Mathf.Clamp(Mathf.FloorToInt(effectFrame), 0, effect.sequence.FrameCount - 1);
            Vector2 destination = Destination(instance);
            Vector2 anchor = destination;
            if (binding.placement == BattleEffectPlacement.Projectile)
            {
                if (!instance.launched && sourceFrame >= binding.launchFrame)
                {
                    instance.launchPosition = ProjectileOrigin(binding, destination, binding.launchFrame);
                    instance.launched = true;
                }
                Vector2 origin = instance.launched ? instance.launchPosition : ProjectileOrigin(binding, destination, sourceFrame);
                float progress = Mathf.InverseLerp(binding.launchFrame, binding.contactFrame, sourceFrame);
                anchor = Vector2.Lerp(origin, destination, progress) + Vector2.up * (Mathf.Sin(progress * Mathf.PI) * binding.arcHeight * actor.UnitsToPixels);
            }
            float scale = binding.preserveSourceScale ? actor.UnitsToPixels : binding.width * actor.UnitsToPixels / Mathf.Max(0.01f, effect.referenceWidth);
            float mirror = (binding.flipX ? -1f : 1f) * actor.SourceMirror;
            if (binding.placement == BattleEffectPlacement.PartyGround)
            {
                Bounds bounds = PartyBounds(allies, actor);
                scale = Mathf.Max((bounds.size.x + actor.BodyHeight * 1.6f) / Mathf.Max(0.01f, effect.referenceWidth), (bounds.size.y + actor.BodyHeight * 1.6f) / Mathf.Max(0.01f, effect.referenceHeight));
            }
            BattleActorAnimation.SetSprite(instance.image, effect.sequence.frames[frame], scale, mirror);
            ConfigureImageSpace(instance.image);
            Vector2 offset = effect.frameAnchors[frame] * scale;
            offset.x *= mirror;
            instance.image.rectTransform.anchoredPosition = anchor - offset;
        }
    }

    private Vector2 Destination(Instance instance)
    {
        BattleEffectBinding binding = instance.binding;
        Vector2 position;
        switch (binding.placement)
        {
            case BattleEffectPlacement.CasterGround:
                position = Local(actor.FootPosition);
                break;
            case BattleEffectPlacement.CasterAbove:
                position = Local(actor.FootPosition) + Vector2.up * actor.BodyHeight * 1.08f;
                break;
            case BattleEffectPlacement.Ally:
                position = Local(instance.recipient.FootPosition);
                break;
            case BattleEffectPlacement.PartyGround:
                position = PartyBounds(allies, actor).center;
                break;
            case BattleEffectPlacement.EnemyGround:
                position = Local(instance.recipient.FootPosition);
                break;
            case BattleEffectPlacement.EnemyFormationGround:
                Bounds bounds = PartyBounds(opponents, instance.recipient);
                position = (Vector2)bounds.center + Vector2.Scale(binding.formationOffset, bounds.extents);
                break;
            default:
                position = Local(instance.recipient.BodyCenter);
                break;
        }
        Vector2 offset = binding.targetOffset * actor.UnitsToPixels;
        if (binding.placement == BattleEffectPlacement.Enemy || binding.placement == BattleEffectPlacement.Projectile) offset += binding.targetBodyOffset * instance.recipient.BodyHeight;
        offset.x *= actor.SourceMirror;
        return position + offset;
    }

    private Vector2 ProjectileOrigin(BattleEffectBinding binding, Vector2 destination, float sourceFrame)
    {
        if (binding.projectileDirection.sqrMagnitude > 0.0001f && binding.projectileDistance > 0f)
        {
            Vector2 direction = binding.projectileDirection.normalized;
            direction.x *= (binding.flipX ? -1f : 1f) * actor.SourceMirror;
            return destination - direction * binding.projectileDistance * actor.UnitsToPixels;
        }
        Vector2 offset = binding.sourceOffset * actor.BodyHeight;
        if (binding.sourceKeys != null && binding.sourceKeys.Length > 0)
        {
            offset = binding.sourceKeys[0].localPosition;
            for (int index = 1; index < binding.sourceKeys.Length; index++)
            {
                BattleEffectSourceKey previous = binding.sourceKeys[index - 1];
                BattleEffectSourceKey current = binding.sourceKeys[index];
                offset = Vector2.Lerp(previous.localPosition, current.localPosition, Mathf.InverseLerp(previous.frame, current.frame, sourceFrame));
                if (sourceFrame <= current.frame) break;
            }
            offset *= actor.UnitsToPixels;
        }
        offset.x *= actor.SourceMirror;
        return Local(actor.FootPosition) + offset;
    }

    private Bounds PartyBounds(BattleActorAnimation[] party, BattleActorAnimation fallback)
    {
        Bounds bounds = new Bounds(fallback != null ? (Vector3)Local(fallback.FootPosition) : Vector3.zero, Vector3.zero);
        bool found = false;
        if (party != null)
            foreach (BattleActorAnimation member in party)
            {
                if (member == null || !member.isActiveAndEnabled || member.IsDead) continue;
                Vector2 position = Local(member.FootPosition);
                if (!found) bounds = new Bounds(position, Vector3.zero);
                else bounds.Encapsulate(position);
                found = true;
            }
        return bounds;
    }

    private Vector2 Local(Vector3 worldPosition) { return space.InverseTransformPoint(worldPosition); }

    private static RectTransform MakeLayer(string layerName, RectTransform parent, bool behind)
    {
        RectTransform layer = new GameObject(layerName, typeof(RectTransform)).GetComponent<RectTransform>();
        layer.SetParent(parent, false);
        layer.pivot = parent.pivot;
        layer.anchorMin = Vector2.zero;
        layer.anchorMax = Vector2.one;
        layer.offsetMin = Vector2.zero;
        layer.offsetMax = Vector2.zero;
        layer.anchoredPosition = Vector2.zero;
        layer.localRotation = Quaternion.identity;
        layer.localScale = Vector3.one;
        if (behind) layer.SetAsFirstSibling();
        else layer.SetAsLastSibling();
        return layer;
    }

    private void ConfigureImageSpace(Image image)
    {
        if (image == null || space == null) return;
        RectTransform imageRect = image.rectTransform;
        imageRect.anchorMin = space.pivot;
        imageRect.anchorMax = space.pivot;
        imageRect.localRotation = Quaternion.identity;
    }

    private static float Interpolate(float value, float start, float end, float first, float last)
    {
        return Mathf.Lerp(first, last, end > start ? Mathf.InverseLerp(start, end, value) : 1f);
    }

    public void Clear()
    {
        foreach (Instance instance in instances)
            if (instance.image != null) instance.image.sprite = null;
        if (underlay != null) { underlay.gameObject.SetActive(false); Destroy(underlay.gameObject); }
        if (overlay != null) { overlay.gameObject.SetActive(false); Destroy(overlay.gameObject); }
        instances.Clear();
        underlay = null;
        overlay = null;
        space = null;
        actor = null;
        target = null;
    }

    private void OnDisable() { Clear(); }
}
