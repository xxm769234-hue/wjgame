using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//解锁条件枚举
public enum UnlockConditionType
{
    Gold,       // 消耗金币解锁整片区域
    PlayerLevel // 等级达标直接解锁，无消耗
}
