using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public sealed class IdleAdventureBookPagePerspective : BaseMeshEffect, ICanvasRaycastFilter
{
    private const float SourceBottom = 0.10f;
    private const float SourceTop = 0.93f;
    private const float LeftSourceLeft = 0.16f;
    private const float LeftSourceRight = 0.50f;
    private const float RightSourceLeft = 0.50f;
    private const float RightSourceRight = 0.84f;

    [SerializeField] private RectTransform canvasRoot;
    [SerializeField] private bool rightPage;
    [SerializeField] private RectTransform projectionReference;
    [SerializeField, Range(0f, 1f)] private float deformationStrength = 0.4f;
    private TextMeshProUGUI textGraphic;
    private readonly List<UIVertex> sourceTriangles = new List<UIVertex>();
    private readonly Vector3[] corners = new Vector3[4];

    private struct ProjectionContext
    {
        public Matrix4x4 LocalToRoot;
        public Matrix4x4 RootToLocal;
        public Rect RootRect;
        public Vector3 Center;
        public Vector3 MappedCenter;
        public float UniformScale;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        textGraphic = GetComponent<TextMeshProUGUI>();
        if (textGraphic != null) textGraphic.OnPreRenderText += ProjectText;
        Canvas.willRenderCanvases += UpdateRaycastBounds;
    }

    protected override void OnDisable()
    {
        if (textGraphic != null) textGraphic.OnPreRenderText -= ProjectText;
        Canvas.willRenderCanvases -= UpdateRaycastBounds;
        if (graphic != null) graphic.raycastPadding = Vector4.zero;
        base.OnDisable();
    }

    private void ProjectText(TMP_TextInfo textInfo)
    {
        if (!IsActive() || !TryGetProjectionContext(out ProjectionContext context)) return;
        for (int materialIndex = 0; materialIndex < textInfo.meshInfo.Length; materialIndex++)
        {
            TMP_MeshInfo mesh = textInfo.meshInfo[materialIndex];
            for (int vertexIndex = 0; vertexIndex < mesh.vertexCount; vertexIndex++)
            {
                mesh.vertices[vertexIndex] = ProjectVertex(mesh.vertices[vertexIndex], context);
            }
        }
    }

    public static void ApplyToHierarchy(Transform hierarchyRoot, RectTransform root, bool useRightPage, RectTransform reference = null)
    {
        if (hierarchyRoot == null || root == null) return;

        Graphic[] graphics = hierarchyRoot.GetComponentsInChildren<Graphic>(true);
        for (int graphicIndex = 0; graphicIndex < graphics.Length; graphicIndex++)
        {
            Graphic targetGraphic = graphics[graphicIndex];
            if (targetGraphic == null) continue;

            IdleAdventureBookPagePerspective effect = targetGraphic.GetComponent<IdleAdventureBookPagePerspective>();
            if (effect == null) effect = targetGraphic.gameObject.AddComponent<IdleAdventureBookPagePerspective>();
            float strength = targetGraphic is TMP_Text || (targetGraphic is Image image && image.preserveAspect)
                ? 0f
                : targetGraphic.GetComponent<Button>() != null ? 0.15f : 0.4f;
            effect.Configure(root, useRightPage, strength, reference != null ? reference : hierarchyRoot as RectTransform);
        }
    }

    public void Configure(RectTransform root, bool useRightPage, float strength = 0.4f, RectTransform reference = null)
    {
        canvasRoot = root;
        rightPage = useRightPage;
        deformationStrength = Mathf.Clamp01(strength);
        projectionReference = reference != null ? reference : transform as RectTransform;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vertexHelper)
    {
        if (!IsActive() || canvasRoot == null || vertexHelper == null) return;

        if (!TryGetProjectionContext(out ProjectionContext context)) return;

        if (graphic is TextMeshProUGUI) return;
        if (deformationStrength > 0f && graphic is Image image && image.type == Image.Type.Simple && image.sprite != null)
        {
            Subdivide(vertexHelper);
        }
        UIVertex vertex = new UIVertex();
        for (int vertexIndex = 0; vertexIndex < vertexHelper.currentVertCount; vertexIndex++)
        {
            vertexHelper.PopulateUIVertex(ref vertex, vertexIndex);
            vertex.position = ProjectVertex(vertex.position, context);
            vertexHelper.SetUIVertex(vertex, vertexIndex);
        }
    }

    public Vector3 ProjectVertex(Vector3 position)
    {
        return TryGetProjectionContext(out ProjectionContext context) ? ProjectVertex(position, context) : position;
    }

    private bool TryGetProjectionContext(out ProjectionContext context)
    {
        context = default;
        if (canvasRoot == null) return false;
        Rect rootRect = canvasRoot.rect;
        if (rootRect.width <= 0f || rootRect.height <= 0f) return false;
        RectTransform reference = projectionReference != null ? projectionReference : transform as RectTransform;
        if (reference == null) return false;
        Vector3 center = canvasRoot.InverseTransformPoint(reference.TransformPoint(reference.rect.center));
        Vector3 mappedCenter = MapRootPosition(center, rootRect);
        Vector3 horizontal = MapRootPosition(center + Vector3.right, rootRect) - mappedCenter;
        Vector3 vertical = MapRootPosition(center + Vector3.up, rootRect) - mappedCenter;
        context = new ProjectionContext
        {
            LocalToRoot = canvasRoot.worldToLocalMatrix * transform.localToWorldMatrix,
            RootToLocal = transform.worldToLocalMatrix * canvasRoot.localToWorldMatrix,
            RootRect = rootRect,
            Center = center,
            MappedCenter = mappedCenter,
            UniformScale = Mathf.Sqrt(horizontal.magnitude * vertical.magnitude)
        };
        return true;
    }

    private Vector3 ProjectVertex(Vector3 position, ProjectionContext context)
    {
        Vector3 rootPosition = context.LocalToRoot.MultiplyPoint3x4(position);
        Vector3 upright = context.MappedCenter + (rootPosition - context.Center) * context.UniformScale;
        upright.z = rootPosition.z;
        Vector3 mapped = deformationStrength <= 0f
            ? upright
            : Vector3.Lerp(upright, MapRootPosition(rootPosition, context.RootRect), deformationStrength);
        return context.RootToLocal.MultiplyPoint3x4(mapped);
    }

    private Vector3 MapRootPosition(Vector3 rootPosition, Rect rootRect)
    {
        Vector2 normalized = new Vector2((rootPosition.x - rootRect.xMin) / rootRect.width, (rootPosition.y - rootRect.yMin) / rootRect.height);
        Vector2 mapped = MapToBookPage(normalized);
        return new Vector3(rootRect.xMin + mapped.x * rootRect.width, rootRect.yMin + mapped.y * rootRect.height, rootPosition.z);
    }

    private void UpdateRaycastBounds()
    {
        if (!IsActive() || canvasRoot == null || graphic == null || !graphic.raycastTarget) return;
        if (!TryGetProjectionContext(out ProjectionContext context)) return;
        RectTransform target = graphic.rectTransform;
        Rect bounds = target.rect;
        target.GetLocalCorners(corners);
        Vector2 minimum = bounds.min;
        Vector2 maximum = bounds.max;
        foreach (Vector3 corner in corners)
        {
            Vector3 projected = ProjectVertex(corner, context);
            minimum = Vector2.Min(minimum, projected);
            maximum = Vector2.Max(maximum, projected);
        }
        graphic.raycastPadding = new Vector4(minimum.x - bounds.xMin, minimum.y - bounds.yMin, bounds.xMax - maximum.x, bounds.yMax - maximum.y);
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!IsActive() || canvasRoot == null || graphic == null) return true;
        if (!TryGetProjectionContext(out ProjectionContext context)) return true;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(graphic.rectTransform, screenPoint, eventCamera, out Vector2 localPoint)) return false;
        graphic.rectTransform.GetLocalCorners(corners);
        float previousCross = 0f;
        for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
        {
            Vector2 start = ProjectVertex(corners[cornerIndex], context);
            Vector2 end = ProjectVertex(corners[(cornerIndex + 1) % corners.Length], context);
            Vector2 edge = end - start;
            Vector2 offset = localPoint - start;
            float cross = edge.x * offset.y - edge.y * offset.x;
            if (Mathf.Abs(cross) < 0.001f) continue;
            if (previousCross != 0f && Mathf.Sign(cross) != Mathf.Sign(previousCross)) return false;
            previousCross = cross;
        }
        return true;
    }

    private void Subdivide(VertexHelper vertexHelper)
    {
        sourceTriangles.Clear();
        vertexHelper.GetUIVertexStream(sourceTriangles);
        vertexHelper.Clear();
        const int subdivisions = 8;
        for (int triangleIndex = 0; triangleIndex < sourceTriangles.Count; triangleIndex += 3)
        {
            UIVertex first = sourceTriangles[triangleIndex];
            UIVertex second = sourceTriangles[triangleIndex + 1];
            UIVertex third = sourceTriangles[triangleIndex + 2];
            int triangleStart = vertexHelper.currentVertCount;
            for (int row = 0; row <= subdivisions; row++)
            {
                for (int column = 0; column <= subdivisions - row; column++)
                {
                    float secondWeight = row / (float)subdivisions;
                    float thirdWeight = column / (float)subdivisions;
                    float firstWeight = 1f - secondWeight - thirdWeight;
                    UIVertex interpolated = first;
                    interpolated.position = first.position * firstWeight + second.position * secondWeight + third.position * thirdWeight;
                    interpolated.uv0 = first.uv0 * firstWeight + second.uv0 * secondWeight + third.uv0 * thirdWeight;
                    interpolated.color = (Color)first.color * firstWeight + (Color)second.color * secondWeight + (Color)third.color * thirdWeight;
                    vertexHelper.AddVert(interpolated);
                }
            }
            for (int row = 0; row < subdivisions; row++)
            {
                int rowStart = triangleStart + row * (subdivisions + 1) - row * (row - 1) / 2;
                int nextRowStart = rowStart + subdivisions + 1 - row;
                for (int column = 0; column < subdivisions - row; column++)
                {
                    vertexHelper.AddTriangle(rowStart + column, nextRowStart + column, rowStart + column + 1);
                    if (column < subdivisions - row - 1)
                        vertexHelper.AddTriangle(rowStart + column + 1, nextRowStart + column, nextRowStart + column + 1);
                }
            }
        }
    }

    private Vector2 MapToBookPage(Vector2 sourcePosition)
    {
        float sourceLeft = rightPage ? RightSourceLeft : LeftSourceLeft;
        float sourceRight = rightPage ? RightSourceRight : LeftSourceRight;
        float horizontal = (sourcePosition.x - sourceLeft) / (sourceRight - sourceLeft);
        float vertical = (sourcePosition.y - SourceBottom) / (SourceTop - SourceBottom);

        Vector2 bottomLeft = rightPage ? new Vector2(0.5059f, 0.0911f) : new Vector2(0.1309f, 0.1137f);
        Vector2 bottomRight = rightPage ? new Vector2(0.8818f, 0.1137f) : new Vector2(0.5039f, 0.0911f);
        Vector2 topLeft = rightPage ? new Vector2(0.5059f, 0.9280f) : new Vector2(0.1899f, 0.9280f);
        Vector2 topRight = rightPage ? new Vector2(0.8223f, 0.9280f) : new Vector2(0.5039f, 0.9280f);
        return MapProjective(horizontal, vertical, bottomLeft, bottomRight, topRight, topLeft);
    }

    private static Vector2 MapProjective(float horizontal, float vertical, Vector2 bottomLeft, Vector2 bottomRight, Vector2 topRight, Vector2 topLeft)
    {
        float deltaX1 = bottomRight.x - topRight.x;
        float deltaX2 = topLeft.x - topRight.x;
        float deltaX3 = bottomLeft.x - bottomRight.x + topRight.x - topLeft.x;
        float deltaY1 = bottomRight.y - topRight.y;
        float deltaY2 = topLeft.y - topRight.y;
        float deltaY3 = bottomLeft.y - bottomRight.y + topRight.y - topLeft.y;
        float determinant = deltaX1 * deltaY2 - deltaX2 * deltaY1;

        if (Mathf.Approximately(determinant, 0f))
        {
            return bottomLeft
                + horizontal * (bottomRight - bottomLeft)
                + vertical * (topLeft - bottomLeft);
        }

        float projectiveX = (deltaX3 * deltaY2 - deltaX2 * deltaY3) / determinant;
        float projectiveY = (deltaX1 * deltaY3 - deltaX3 * deltaY1) / determinant;
        float coefficientX1 = bottomRight.x - bottomLeft.x + projectiveX * bottomRight.x;
        float coefficientX2 = topLeft.x - bottomLeft.x + projectiveY * topLeft.x;
        float coefficientY1 = bottomRight.y - bottomLeft.y + projectiveX * bottomRight.y;
        float coefficientY2 = topLeft.y - bottomLeft.y + projectiveY * topLeft.y;
        float denominator = projectiveX * horizontal + projectiveY * vertical + 1f;

        if (Mathf.Approximately(denominator, 0f)) return bottomLeft;
        return new Vector2(
            (coefficientX1 * horizontal + coefficientX2 * vertical + bottomLeft.x) / denominator,
            (coefficientY1 * horizontal + coefficientY2 * vertical + bottomLeft.y) / denominator);
    }
}
