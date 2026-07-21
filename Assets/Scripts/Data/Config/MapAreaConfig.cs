using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MapAreaConfig", menuName = "Config/MapAreaConfig")]
//使用说明：策划在 Inspector 手动添加任意坐标，实现漫画不规则地块布局，不用修改代码。
public class MapAreaConfig : BaseConfig
{
    // 对应地块类型 grass/forest/sea/volcano/snow
    public string landType;
    // 首页展示不规则背景图
    public Sprite homeIrregularSprite;
    // 解锁条件类型
    public UnlockConditionType unlockType;
    // 解锁目标数值（金币数量/玩家等级）
    public long unlockTargetValue;
    // 【不规则核心】当前区域所有网格坐标，自由填写，无需规整矩形
    public List<Vector2Int> areaAllGrids;
}