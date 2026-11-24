using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Full illustrations for comic cells; independent from small building icons.</summary>
public static class SimulationBuildingSceneCatalog
{
    private static readonly Dictionary<string, Sprite> Scenes = new Dictionary<string, Sprite>();

    private static readonly Dictionary<int, (string code, int count)> Buildings = new Dictionary<int, (string, int)>
    {
        { 2001, ("farm_2001", 1) },
        { 2002, ("ranch_2002", 2) },
        { 2003, ("mine_2003", 2) },
        { 2004, ("lumber_2004", 2) },
        { 2005, ("wood_processing_2005", 3) },
        { 2006, ("fishery_2006", 7) },
        { 2007, ("metal_processing_2007", 4) },
        { 2008, ("hot_spring_2008", 5) },
        { 2009, ("bakery_2009", 1) },
        { 2010, ("barbecue_stall_2010", 1) },
        { 2011, ("diamond_mine_2011", 1) },
        { 2012, ("purification_lab_2012", 1) },
        { 2013, ("cedar_lumber_2013", 4) },
        { 2014, ("sandstone_quarry_2014", 2) },
        { 2015, ("ore_smelting_kiln_2015", 2) },
    };

    public static Sprite GetScene(BuildingConfig building, int slotKey = 0)
    {
        if (building == null) return null;
        if (building.comicSceneSprite != null) return building.comicSceneSprite;
        string code = building.buildingCode;
        int id = building.buildingId;
        if (id <= 0 && !string.IsNullOrEmpty(code))
            int.TryParse(code.Substring(code.LastIndexOf('_') + 1), out id);
        if (Buildings.TryGetValue(id, out var art))
        {
            if (!string.IsNullOrEmpty(code) && code != art.code && LoadBuilding(code) is Sprite custom)
                return custom;
            int variant = (int)((uint)slotKey % (uint)art.count) + 1;
            if (variant > 1 && LoadBuilding(art.code + "_v" + variant) is Sprite alternate)
                return alternate;
            code = art.code;
        }
        if (!string.IsNullOrEmpty(code) && LoadBuilding(code) is Sprite exact) return exact;
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

    private static Sprite LoadBuilding(string code)
    {
        string key = "Buildings/" + code;
        if (!Scenes.TryGetValue(key, out Sprite scene))
            Scenes[key] = scene = Resources.Load<Sprite>("SimulationBuildingScenes/" + key);
        return scene;
    }

    private static bool Matches(string value, params string[] candidates)
    {
        foreach (string candidate in candidates)
            if (value.IndexOf(candidate, StringComparison.Ordinal) >= 0) return true;
        return false;
    }
}
