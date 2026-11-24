using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Full illustrations for comic cells; independent from small building icons.</summary>
public static class SimulationBuildingSceneCatalog
{
    private static readonly Dictionary<string, Sprite> Scenes = new Dictionary<string, Sprite>();

    public static Sprite GetScene(BuildingConfig building)
    {
        if (building == null) return null;
        if (building.comicSceneSprite != null) return building.comicSceneSprite;
        if (!string.IsNullOrEmpty(building.buildingCode))
        {
            string exactKey = "Buildings/" + building.buildingCode;
            if (!Scenes.TryGetValue(exactKey, out Sprite exact))
                exact = Resources.Load<Sprite>("SimulationBuildingScenes/" + exactKey);
            if (exact != null)
            {
                Scenes[exactKey] = exact;
                return exact;
            }
        }
        string key = ((building.buildingCode ?? "") + " " + building.buildingName + " " + building.buildingType).ToLowerInvariant();
        string scene = Matches(key, "hot_spring", "hotspring", "温泉") ? "HotSpring"
            : Matches(key, "smelt", "refin", "metal", "forge", "加工", "冶炼", "锻造") ? "Smelter"
            : Matches(key, "quarry", "stoneworks", "采石", "石场") ? "Quarry"
            : Matches(key, "lumber", "logging", "sawmill", "wood", "伐木", "木材") ? "Lumbermill"
            : Matches(key, "fish", "渔", "鱼场", "鱼塘") ? "Fishery"
            : Matches(key, "ranch", "pasture", "livestock", "牧场", "畜牧") ? "Pasture"
            : Matches(key, "farm", "field", "农", "麦田") ? "Farm"
            : Matches(key, "mine", "矿") ? "Mine"
            : Matches(key, "house", "home", "residen", "dwelling", "住房", "住宅", "民居") ? "Residence"
            : "Workshop";
        if (!Scenes.TryGetValue(scene, out Sprite sprite) || sprite == null)
        {
            sprite = Resources.Load<Sprite>("SimulationBuildingScenes/" + scene);
            if (sprite == null) sprite = Resources.Load<Sprite>("SimulationBuildingScenes/Placeholder");
            if (sprite != null) Scenes[scene] = sprite;
        }
        return sprite;
    }

    private static bool Matches(string value, params string[] candidates)
    {
        foreach (string candidate in candidates)
            if (value.IndexOf(candidate, StringComparison.Ordinal) >= 0) return true;
        return false;
    }
}
