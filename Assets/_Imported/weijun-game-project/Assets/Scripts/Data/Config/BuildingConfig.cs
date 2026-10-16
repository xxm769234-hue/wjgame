using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BuildingConfig", menuName = "Config/BuildingConfig")]
public class BuildingConfig : BaseConfig
{
    // 仅可放置的地形
    public string limitLandType;
    public int width = 1;
    public int height = 1;
    public Sprite buildSprite;
    // 建造消耗
    public Dictionary<string, int> buildCost;
    // 各级升级消耗
    public List<Dictionary<string, int>> upgradeCostList;
    // 产出资源ID
    public string produceResId;
    // 单次生产秒数
    public int produceSecond = 60;
    // 是否允许拆除
    public bool canDelete = true;
}
