using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LandConfig", menuName = "Config/LandConfig")]
public class LandConfig : BaseConfig
{
    public Sprite tileSprite;
    // 铺设消耗资源 key=资源id value=数量
    public Dictionary<string, int> buildCost;
    // 允许放置的建筑类型
    public List<string> matchBuildTypes;
    public List<string> matchBuildIds;
}
