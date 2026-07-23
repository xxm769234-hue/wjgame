using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//功能：统一坐标换算，所有地图逻辑复用，避免重复计算
public static class MapTool
{
    // 单格子像素尺寸
    public const int TileSize = 128;

    // 网格坐标转世界坐标（左下角原点）
    public static Vector2 GridToWorld(int x, int y)
    {
        float worldX = x * TileSize + TileSize / 2f;
        float worldY = y * TileSize + TileSize / 2f;
        return new Vector2(worldX, worldY);
    }

    // 鼠标屏幕坐标转网格坐标
    public static Vector2Int WorldToGrid(Vector2 worldPos)
    {
        int x = Mathf.FloorToInt(worldPos.x / TileSize);
        int y = Mathf.FloorToInt(worldPos.y / TileSize);
        return new Vector2Int(x, y);
    }

    // 拼接坐标key "x_y"
    public static string GetGridKey(int x, int y)
    {
        return $"{x}_{y}";
    }

    // 判断坐标是否越界
    public static bool IsOutBound(int x, int y, int width, int height)
    {
        return x < 0 || y < 0 || x >= width || y >= height;
    }
}
