using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class IdleAdventureFormationHallMask : MaskableGraphic
{
    [SerializeField] private Vector2[] corners = new Vector2[0];

    public void Configure(Vector2[] normalizedCorners)
    {
        corners = normalizedCorners;
        color = Color.white;
        raycastTarget = false;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (corners == null || corners.Length != 4) return;
        Rect bounds = rectTransform.rect;
        for (int index = 0; index < corners.Length; index++)
        {
            Vector2 corner = corners[index];
            Vector3 position = new Vector3(
                bounds.xMin + corner.x * bounds.width,
                bounds.yMin + corner.y * bounds.height,
                0f);
            vertexHelper.AddVert(position, color, corner);
        }
        vertexHelper.AddTriangle(0, 1, 2);
        vertexHelper.AddTriangle(0, 2, 3);
    }
}
