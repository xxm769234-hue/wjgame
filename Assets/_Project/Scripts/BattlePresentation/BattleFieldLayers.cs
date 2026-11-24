using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(500)]
[DisallowMultipleComponent]
public sealed class BattleFieldLayers : MonoBehaviour
{
    internal sealed class DepthItem
    {
        public RectTransform rect;
        public IdleAdventureActorView view;
        public BattleActorAnimation origin;
        public BattleActorAnimation target;
        public float travel = 1f;
        public float? formationGroundY;
        public int priority;
        public int order;
        public int depth;
    }

    private readonly List<DepthItem> items = new List<DepthItem>(32);
    private int nextOrder;
    internal RectTransform Ground { get; private set; }
    internal RectTransform Shadows { get; private set; }
    internal RectTransform Figures { get; private set; }
    internal RectTransform Labels { get; private set; }

    internal static BattleFieldLayers For(Transform node)
    {
        if (!Application.isPlaying) return null;
        while (node != null && node.name != "Battlefield") node = node.parent;
        if (node == null) return null;
        if (!node.TryGetComponent(out BattleFieldLayers layers))
            layers = node.gameObject.AddComponent<BattleFieldLayers>();
        if (layers.Figures == null)
        {
            layers.Ground = layers.Plane("Battle Ground Effects");
            layers.Shadows = layers.Plane("Ground Shadows");
            layers.Figures = layers.Plane("Battle Depth");
            layers.Labels = layers.Plane("Battle Labels");
        }
        return layers;
    }

    private RectTransform Plane(string label)
    {
        var plane = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
        plane.SetParent(transform, false);
        plane.pivot = ((RectTransform)transform).pivot;
        plane.anchorMin = Vector2.zero;
        plane.anchorMax = Vector2.one;
        plane.offsetMin = plane.offsetMax = Vector2.zero;
        return plane;
    }

    internal void PlaceActor(IdleAdventureActorView view)
    {
        if (view.Root.parent == Figures) return;
        view.Root.SetParent(Figures, false);
        items.Add(new DepthItem { rect = view.Root, view = view, order = nextOrder++ });
    }

    internal DepthItem PlaceEffect(RectTransform visual, BattleActorAnimation recipient, int priority)
    {
        visual.SetParent(Figures, false);
        var item = new DepthItem { rect = visual, target = recipient, priority = priority, order = nextOrder++ };
        items.Add(item);
        return item;
    }

    private float GroundY(BattleActorAnimation unit)
    {
        return Figures.InverseTransformPoint(unit.GroundShadowCenter()).y;
    }

    private void LateUpdate()
    {
        float band = Mathf.Max(.25f, Figures.rect.height / 540f);
        for (int index = items.Count - 1; index >= 0; index--)
        {
            var item = items[index];
            if (item.rect == null)
            {
                items.RemoveAt(index);
                continue;
            }
            float ground;
            if (item.view != null)
            {
                var actor = item.view.Animation;
                ground = actor != null && actor.ActorRoot != null && item.view.Body.sprite != null
                    ? GroundY(actor) : Figures.InverseTransformPoint(item.rect.position).y;
                item.priority = actor != null && actor.InMeleeContact ? 1 : 0;
            }
            else if (item.formationGroundY.HasValue) ground = item.formationGroundY.Value;
            else if (item.target != null)
            {
                ground = GroundY(item.target);
                if (item.origin != null && item.travel < 1f)
                    ground = Mathf.Lerp(GroundY(item.origin), ground, item.travel);
            }
            else ground = Figures.InverseTransformPoint(item.rect.position).y;
            item.depth = Mathf.RoundToInt(ground / band);
        }
        items.Sort(CompareDepth);
        for (int index = 0; index < items.Count; index++)
            if (items[index].rect.GetSiblingIndex() != index) items[index].rect.SetSiblingIndex(index);
    }

    private static int CompareDepth(DepthItem left, DepthItem right)
    {
        int order = right.depth.CompareTo(left.depth);
        if (order != 0) return order;
        order = left.priority.CompareTo(right.priority);
        return order != 0 ? order : left.order.CompareTo(right.order);
    }
}
