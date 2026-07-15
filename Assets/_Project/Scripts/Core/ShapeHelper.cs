using UnityEngine;

/// <summary>Pure helpers for polygon shape math, centroid calculation, and grid snapping.</summary>
public static class ShapeHelper
{
    public static Vector2 GetLocalCentroid(PolygonCollider2D polygon)
    {
        if (polygon == null || polygon.pathCount == 0) return Vector2.zero;

        Vector2[] path = GetLargestPath(polygon);
        if (path.Length == 0) return polygon.offset;
        return CalculateCentroid(path);
    }

    public static Vector3 GetWorldCentroid(PolygonCollider2D polygon)
    {
        if (polygon == null) return Vector3.zero;
        Vector2 local = GetLocalCentroid(polygon);
        return polygon.transform.TransformPoint(local);
    }

    public static Vector2Int WorldToGrid(Vector3 worldPosition, Vector2 gridOriginWorld, float cellSize)
    {
        float safeCellSize = Mathf.Max(0.0001f, cellSize);
        int x = Mathf.RoundToInt((worldPosition.x - gridOriginWorld.x) / safeCellSize);
        int y = Mathf.RoundToInt((worldPosition.y - gridOriginWorld.y) / safeCellSize);
        return new Vector2Int(x, y);
    }

    public static Vector3 GridToWorld(Vector2Int gridPosition, Vector2 gridOriginWorld, float cellSize, float z = 0f)
    {
        float safeCellSize = Mathf.Max(0.0001f, cellSize);
        return new Vector3(
            gridOriginWorld.x + gridPosition.x * safeCellSize,
            gridOriginWorld.y + gridPosition.y * safeCellSize,
            z);
    }

    public static Vector3 SnapWorldToGrid(Vector3 worldPosition, Vector2 gridOriginWorld, float cellSize, float z = 0f)
    {
        return GridToWorld(WorldToGrid(worldPosition, gridOriginWorld, cellSize), gridOriginWorld, cellSize, z);
    }

    public static bool ContainsPoint(PolygonCollider2D polygon, Vector2 worldPoint)
    {
        return polygon != null && polygon.OverlapPoint(worldPoint);
    }

    public static Vector2[] GetLargestPath(PolygonCollider2D polygon)
    {
        if (polygon == null || polygon.pathCount == 0) return System.Array.Empty<Vector2>();

        int bestPathIndex = 0;
        float bestArea = 0f;
        for (int i = 0; i < polygon.pathCount; i++)
        {
            Vector2[] path = polygon.GetPath(i);
            float area = Mathf.Abs(GetSignedArea(path));
            if (area > bestArea)
            {
                bestArea = area;
                bestPathIndex = i;
            }
        }

        Vector2[] bestPath = polygon.GetPath(bestPathIndex);
        Vector2[] result = new Vector2[bestPath.Length];
        for (int i = 0; i < bestPath.Length; i++)
        {
            result[i] = bestPath[i] + polygon.offset;
        }
        return result;
    }

    private static Vector2 CalculateCentroid(Vector2[] path)
    {
        float signedArea = GetSignedArea(path);
        if (Mathf.Abs(signedArea) < 0.0001f) return Average(path);

        float cx = 0f;
        float cy = 0f;
        for (int i = 0; i < path.Length; i++)
        {
            Vector2 current = path[i];
            Vector2 next = path[(i + 1) % path.Length];
            float cross = current.x * next.y - next.x * current.y;
            cx += (current.x + next.x) * cross;
            cy += (current.y + next.y) * cross;
        }

        float factor = 1f / (6f * signedArea);
        return new Vector2(cx * factor, cy * factor);
    }

    private static float GetSignedArea(Vector2[] path)
    {
        if (path == null || path.Length < 3) return 0f;

        float area = 0f;
        for (int i = 0; i < path.Length; i++)
        {
            Vector2 current = path[i];
            Vector2 next = path[(i + 1) % path.Length];
            area += current.x * next.y - next.x * current.y;
        }
        return area * 0.5f;
    }

    private static Vector2 Average(Vector2[] path)
    {
        if (path == null || path.Length == 0) return Vector2.zero;

        Vector2 sum = Vector2.zero;
        for (int i = 0; i < path.Length; i++) sum += path[i];
        return sum / path.Length;
    }
}
