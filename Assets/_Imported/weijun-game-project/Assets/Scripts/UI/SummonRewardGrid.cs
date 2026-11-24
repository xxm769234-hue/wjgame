using UnityEngine;
using UnityEngine.UI;

public sealed class SummonRewardGrid : GridLayoutGroup
{
    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
        int count = Mathf.Max(1, rectChildren.Count);
        Rect viewport = ((RectTransform)transform.parent).rect;
        float available = Mathf.Max(1f, viewport.width - padding.horizontal);
        int columns = Mathf.Clamp(Mathf.FloorToInt((available + spacing.x) / (148f + spacing.x)), 1, Mathf.Min(5, count));
        float width = (available - spacing.x * (columns - 1)) / columns;
        int rows = Mathf.CeilToInt((float)count / columns);
        float heightBudget = (viewport.height - padding.vertical - spacing.y * (rows - 1)) / rows;
        float height = Mathf.Min(count == 1 ? 336f : 272f, width * 1.34f, Mathf.Max(192f, heightBudget));
        m_CellSize = new Vector2(Mathf.Min(width, height * .80f), height);
        m_Constraint = Constraint.FixedColumnCount;
        m_ConstraintCount = columns;
    }
}
