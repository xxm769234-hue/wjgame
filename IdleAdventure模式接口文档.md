# Weijun Game Project Idle Adventure 模式后端接口文档

生成时间：2026-07-20  
适用模式：Idle Adventure mode / 放置闯关模式  
客户端：Unity  
后端基础地址：`http://localhost:8080/api`  
统一 Content-Type：`application/json; charset=UTF-8`

同步说明：本文档根据 Unity 当前 Idle Adventure 模式功能整理，用于后续后端接口开发。当前 Unity 玩法中，战斗、编队、技能、buff、开始界面、结束面板等逻辑主要在客户端脚本中实现；本文档将这些功能抽象为后端接口契约，方便后端保存玩家编队、下发角色配置、下发关卡敌人、创建战斗会话、结算战斗回合和记录闯关进度。

## 1. 通用规则

### 1.1 统一返回结构

所有接口统一返回：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {}
}
```

字段说明：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| code | number | 业务状态码，200 表示成功 |
| message | string | 提示信息 |
| data | object / array / null | 业务数据 |

常见失败：

| 场景 | 返回 |
| --- | --- |
| 参数缺失或格式错误 | `code=400`，`message` 为具体字段错误 |
| 未登录或 token 过期 | HTTP 401，`code=401`，`message=未登录或登录已过期` |
| 访问其他玩家数据 | HTTP 403，`code=403`，`message=不能访问其他玩家的数据` |
| 编队为空 | `code=400`，`message=至少需要上阵 1 名友方角色` |
| 编队槽位越界 | `code=400`，`message=slotIndex 必须在 0-5 范围内` |
| 角色不可用 | `code=400`，`message=该角色不可用于当前模式` |
| 战斗会话不存在 | `code=404`，`message=战斗会话不存在` |
| 战斗会话状态错误 | `code=409`，`message=当前战斗状态不允许此操作` |
| 系统异常 | HTTP 500，`code=500`，`message=系统异常，请联系管理员` |

### 1.2 登录鉴权

除登录、注册、健康检查等公共接口外，Idle Adventure 模式接口都需要携带：

```http
Authorization: Bearer <token>
```

`user.id` 就是游戏里的 `playerId`。例如用户 id 为 3，就调用：

```http
GET /api/idle-adventure/players/3/home
Authorization: Bearer <token>
```

后端必须校验路径或请求体中的 `playerId` 是否等于 token 对应用户 ID，不能用 A 用户 token 请求或修改 B 用户数据。

### 1.3 模式固定规则

当前版本 Idle Adventure 模式固定规则如下：

| 规则项 | 当前值 | 说明 |
| --- | --- | --- |
| 最大友方数量 | 6 | 玩家最多上阵 6 名友方角色 |
| 最大敌方数量 | 6 | 单关最多存在 6 名敌方角色 |
| 阵型布局 | 2 列 3 行 | 两列三行，前列 3 个，后列 3 个 |
| 槽位编号 | 0-5 | `0,1,2` 为前排；`3,4,5` 为后排 |
| 行号计算 | `slotIndex % 3` | 行号为 0-2 |
| 友方技能槽 | 2 | 每名友方角色最多 2 个技能槽 |
| 敌方技能槽 | 1 | 每名敌方角色最多 1 个技能槽 |
| buff 图标显示 | 6 | 客户端每个角色最多显示 6 个 buff 图标 |
| 战斗开始条件 | 至少 1 名友方角色 | 编队为空时开始按钮不可用 |
| 关卡结束条件 | 当前槽位中的敌人全部死亡 | 弹出结束面板 |
| 结束面板行为 | 继续下一关 / 结束模式 | 当前版本“结束模式”先设计为暂停当前战斗 |

槽位布局：

| slotIndex | 列 | 行 | 定位 |
| --- | --- | --- | --- |
| 0 | 前排 | 第 1 行 | front row 1 |
| 1 | 前排 | 第 2 行 | front row 2 |
| 2 | 前排 | 第 3 行 | front row 3 |
| 3 | 后排 | 第 1 行 | back row 1 |
| 4 | 后排 | 第 2 行 | back row 2 |
| 5 | 后排 | 第 3 行 | back row 3 |

### 1.4 战斗结算规则

后端如承担权威战斗结算，应按以下逻辑生成每回合动作结果：

1. 每回合开始时，收集双方所有存活角色。
2. 按 `effectiveSpeed` 从高到低排序，速度高者先行动。
3. 速度相同情况下，友方优先；仍相同时按 `slotIndex` 从小到大。
4. 行动角色优先使用第一个可用技能；无可用技能时使用普通攻击。
5. 目标选择先判断对方前排 `slotIndex=0,1,2` 是否仍有存活角色。
6. 如果前排有存活角色，只能攻击前排；前排全部死亡后，才能攻击后排 `slotIndex=3,4,5`。
7. 在可攻击列中，优先攻击与自己同一行的敌人。
8. 同一行无敌人时，优先攻击上一行的敌人。
9. 上方也无敌人时，再攻击下方行的敌人。
10. 每个行动后立即结算伤害、死亡、技能冷却、技能附加 buff。
11. 每回合结束后，双方所有存活角色的 buff 持续回合减少 1，技能冷却减少 1。
12. 任一方全部死亡时，本关结束。

伤害计算：

```text
基础伤害 = max(0, attacker.effectiveAttack - target.effectiveDefense)
增益倍率 = 普通buff倍率 + 对应属性buff倍率 + 场地buff倍率 + 属性克制倍率 + 1
普通攻击伤害 = floor(基础伤害 * 增益倍率)
技能伤害 = floor(基础伤害 * 技能倍率 * 增益倍率)
```

说明：倍率字段使用小数表达，`0.25` 表示增加 25%，`-0.2` 表示降低 20%。如果最终伤害需要保底 1 点，后端可在最后一步执行 `max(1, finalDamage)`；如果允许完全被防御抵消，则使用上面的 `max(0, ...)` 规则。属性克制倍率不从 buff 表读取，只要攻击者角色属性克制目标角色属性，就固定取 `0.2`。

倍率来源：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| normalBuffMultiplier | number | 普通伤害增益/减益 buff 倍率，直接参与所有伤害计算 |
| attributeBuffMultiplier | number | 属性增强/减益 buff 倍率，只在行动角色 `attributeType` 与 buff 的 `affectedAttribute` 匹配时生效 |
| fieldBuffMultiplier | number | 场地 buff 倍率，只对符合场地 buff `affectedAttribute` 的角色生效 |
| attributeRestraintMultiplier | number | 属性克制倍率，攻击者角色属性克制目标角色属性时固定为 `0.2`，否则为 `0` |
| bonusMultiplier | number | 最终增益倍率，即上述四项相加后再加 1 |

buff 倍率分类：

| buff 类型 | effectType | category | 生效范围 |
| --- | --- | --- | --- |
| 伤害增益/减益 buff（普通） | NORMAL_DAMAGE | `BENEFIT` / `DETRIMENT` | 对所有属性角色的伤害计算生效 |
| 属性增强/减益 buff | ATTRIBUTE_DAMAGE | `BENEFIT` / `DETRIMENT` | 只对角色 `attributeType` 与 `affectedAttribute` 匹配的角色生效 |
| 场地 buff | FIELD | `BENEFIT` / `DETRIMENT` | 只对 `affectedAttribute` 匹配的角色生效，通常来自关卡或战场环境 |
| 属性克制倍率 | 无需 buff | 无需 category | 攻击者角色属性克制目标角色属性时固定增加 20% |

属性计算：

```text
effectiveAttack = max(0, baseAttack + sum(activeBuff.attackModifier))
effectiveDefense = max(0, baseDefense + sum(activeBuff.defenseModifier))
effectiveSpeed = max(1, baseSpeed + sum(activeBuff.speedModifier))
```

## 2. 枚举定义

### 2.1 BattleSide

| 值 | 说明 |
| --- | --- |
| FRIENDLY | 友方 |
| ENEMY | 敌方 |

### 2.2 BattleSessionStatus

| 值 | 说明 |
| --- | --- |
| CREATED | 已创建，未开始 |
| RUNNING | 战斗中 |
| PAUSED | 已暂停 |
| STAGE_COMPLETE | 当前关卡胜利，等待玩家选择 |
| FAILED | 友方全部死亡 |
| ENDED | 模式结束 |

### 2.3 BuffCategory

| 值 | 说明 |
| --- | --- |
| BENEFIT | 增益 buff |
| DETRIMENT | 减益 buff |

### 2.4 BuffSource

| 值 | 说明 |
| --- | --- |
| CHARACTER_SKILL | 角色技能增加的 buff |
| OTHER | 其他方式增加的 buff |

### 2.5 BuffEffectType

| 值 | 说明 |
| --- | --- |
| NORMAL_DAMAGE | 伤害增益/减益 buff（普通），对所有属性生效 |
| ATTRIBUTE_DAMAGE | 属性增强/减益 buff，只对对应属性角色生效 |
| FIELD | 场地 buff，只对对应属性角色生效 |

### 2.6 BattleActionType

| 值 | 说明 |
| --- | --- |
| BASIC_ATTACK | 普通攻击 |
| SKILL | 技能 |
| BUFF_APPLIED | buff 生效 |
| BUFF_TICK | buff 回合减少 |
| BUFF_EXPIRED | buff 过期 |
| DEFEATED | 角色死亡 |
| ROUND_END | 回合结束 |
| STAGE_COMPLETE | 关卡胜利 |
| PARTY_FAILED | 友方失败 |

### 2.7 DamageAttribute

| 值 | 说明 |
| --- | --- |
| PHYSICAL | 物理 / 普通属性 |
| FIRE | 火属性 |
| WATER | 水属性 |
| WOOD | 木属性 |
| EARTH | 土属性 |
| LIGHT | 光属性 |
| DARK | 暗属性 |

说明：当前没有细分属性时，普通攻击和技能都可以先使用 `PHYSICAL`。属性克制倍率固定为 20%，后端根据攻击者角色 `attributeType` 与目标角色 `attributeType` 判断是否命中克制关系；命中时 `attributeRestraintMultiplier=0.2`，未命中时为 `0`。

## 3. 通用数据结构

### 3.1 IdleAdventureModeConfigResponse

```json
{
  "maxPartySize": 6,
  "formationRows": 3,
  "formationColumns": 2,
  "friendlySkillSlotCount": 2,
  "enemySkillSlotCount": 1,
  "maxBuffIconSlots": 6,
  "startingFriendlyCount": 3,
  "baseEnemyCount": 2,
  "addEnemyEveryStages": 2,
  "turnInterval": 0.75,
  "attackAnimationDuration": 0.28,
  "hitFlashDuration": 0.18,
  "attributeRestraintMultiplier": 0.2,
  "rewardSettlement": "ON_EXIT",
  "combatRules": {
    "initiative": "SPEED_DESC",
    "frontColumnFirst": true,
    "preferSameRow": true,
    "preferUpperRowWhenSameRowEmpty": true
  }
}
```

字段说明：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| maxPartySize | number | 单方最大角色数，当前为 6 |
| formationRows | number | 阵型行数，当前为 3 |
| formationColumns | number | 阵型列数，当前为 2 |
| friendlySkillSlotCount | number | 友方技能槽数量，当前为 2 |
| enemySkillSlotCount | number | 敌方技能槽数量，当前为 1 |
| maxBuffIconSlots | number | 客户端显示 buff 图标数量 |
| startingFriendlyCount | number | 默认友方数量 |
| baseEnemyCount | number | 第 1 关基础敌人数 |
| addEnemyEveryStages | number | 每多少关增加 1 名敌人 |
| turnInterval | number | 客户端每回合间隔，单位秒 |
| attackAnimationDuration | number | 攻击动画时长，单位秒 |
| hitFlashDuration | number | 受击闪烁时长，单位秒 |
| attributeRestraintMultiplier | number | 属性克制固定倍率，当前为 0.2 |
| rewardSettlement | string | 奖励结算时机，当前为退出模式时统一结算 |
| combatRules | object | 战斗规则说明 |

### 3.2 CombatantDefinition

```json
{
  "definitionId": 101,
  "combatantKey": "hero_1",
  "combatantName": "Hero 1",
  "side": "FRIENDLY",
  "maxHealth": 160,
  "attack": 28,
  "defense": 5,
  "speed": 18,
  "attributeType": "PHYSICAL",
  "skillSlots": [
    {
      "slotIndex": 0,
      "skill": {
        "skillId": 1001,
        "skillKey": "power_strike",
        "skillName": "Power Strike",
        "cooldownRounds": 2,
        "skillMultiplier": 1.35,
        "selfBuffPresetId": null,
        "targetBuffPresetId": null
      }
    },
    {
      "slotIndex": 1,
      "skill": {
        "skillId": 1002,
        "skillKey": "battle_focus",
        "skillName": "Battle Focus",
        "cooldownRounds": 3,
        "skillMultiplier": 1,
        "selfBuffPresetId": 1,
        "targetBuffPresetId": null
      }
    }
  ]
}
```

字段说明：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| definitionId | number | 角色配置 ID |
| combatantKey | string | 角色配置 key |
| combatantName | string | 显示名称 |
| side | string | `FRIENDLY` 或 `ENEMY` |
| maxHealth | number | 最大生命 |
| attack | number | 基础攻击 |
| defense | number | 基础防御 |
| speed | number | 基础速度 |
| attributeType | string | 角色自身属性，用于属性克制计算 |
| skillSlots | array | 技能槽配置 |

### 3.3 SkillDefinition

```json
{
  "skillId": 1001,
  "skillKey": "power_strike",
  "skillName": "Power Strike",
  "cooldownRounds": 2,
  "skillMultiplier": 1.35,
  "damageAttribute": "PHYSICAL",
  "selfBuffPresetId": null,
  "targetBuffPresetId": null
}
```

字段说明：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| skillId | number | 技能 ID |
| skillKey | string | 技能 key |
| skillName | string | 技能显示名 |
| cooldownRounds | number | 技能冷却回合数 |
| skillMultiplier | number | 技能倍率，1 表示 100%，1.35 表示 135% |
| damageAttribute | string | 技能攻击属性；为空时使用角色自身 `attributeType` |
| selfBuffPresetId | number / null | 对自己施加的 buff 预设 ID |
| targetBuffPresetId | number / null | 对目标施加的 buff 预设 ID |

### 3.4 BuffDefinition

```json
{
  "buffPresetId": 1,
  "buffKey": "normal_damage_up",
  "displayName": "Damage Up",
  "category": "BENEFIT",
  "source": "CHARACTER_SKILL",
  "effectType": "NORMAL_DAMAGE",
  "durationRounds": 3,
  "attackModifier": 8,
  "defenseModifier": 0,
  "speedModifier": 0,
  "normalDamageMultiplier": 0.15,
  "attributeDamageMultiplier": 0,
  "affectedAttribute": null
}
```

字段说明：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| buffPresetId | number | buff 预设 ID |
| buffKey | string | buff key，同一角色同 key 同 source 时刷新持续时间 |
| displayName | string | 显示名 |
| category | string | `BENEFIT` 增益，`DETRIMENT` 减益 |
| source | string | `CHARACTER_SKILL` 或 `OTHER` |
| effectType | string | `NORMAL_DAMAGE`、`ATTRIBUTE_DAMAGE` 或 `FIELD` |
| durationRounds | number | 持续回合数，最小为 1 |
| attackModifier | number | 攻击修正，可为负数 |
| defenseModifier | number | 防御修正，可为负数 |
| speedModifier | number | 速度修正，可为负数 |
| normalDamageMultiplier | number | 普通 buff 倍率，参与 `普通buff倍率` 计算 |
| attributeDamageMultiplier | number | 属性 buff 倍率，只有角色 `attributeType` 与 `affectedAttribute` 匹配时参与 `对应属性buff倍率` 计算 |
| affectedAttribute | string / null | 该 buff 生效的角色属性；普通伤害 buff 可为 null |

### 3.4.1 RewardItemDefinition

```json
{
  "itemKey": "gold",
  "displayName": "Gold",
  "quantity": 120
}
```

字段说明：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| itemKey | string | 奖励物品 key，应与背包或资源表中的物品 key 对齐 |
| displayName | string | 奖励显示名 |
| quantity | number | 奖励数量 |

### 3.4.2 StageRewardDefinition

```json
{
  "stage": 1,
  "rewards": [
    {
      "itemKey": "gold",
      "displayName": "Gold",
      "quantity": 120
    },
    {
      "itemKey": "adventure_token",
      "displayName": "Adventure Token",
      "quantity": 1
    }
  ]
}
```

字段说明：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| stage | number | 关卡编号 |
| rewards | array | 当前关卡通关后进入待结算池的奖励 |

### 3.5 FormationSlot

```json
{
  "slotIndex": 0,
  "column": 0,
  "row": 0,
  "frontColumn": true,
  "definitionId": 101,
  "combatantName": "Hero 1"
}
```

字段说明：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| slotIndex | number | 槽位编号，范围 0-5 |
| column | number | 0 为前排，1 为后排 |
| row | number | 行号，范围 0-2 |
| frontColumn | boolean | 是否前排 |
| definitionId | number / null | 上阵角色配置 ID，空槽为 null |
| combatantName | string / null | 上阵角色名称，空槽为 null |

### 3.6 CombatantState

```json
{
  "runtimeId": "F-0",
  "definitionId": 101,
  "combatantName": "Hero 1",
  "side": "FRIENDLY",
  "slotIndex": 0,
  "maxHealth": 160,
  "currentHealth": 128,
  "attack": 28,
  "defense": 5,
  "speed": 18,
  "attributeType": "PHYSICAL",
  "effectiveAttack": 28,
  "effectiveDefense": 5,
  "effectiveSpeed": 18,
  "defeated": false,
  "activeBuffs": [
    {
      "buffKey": "normal_damage_up",
      "displayName": "Damage Up",
      "category": "BENEFIT",
      "source": "CHARACTER_SKILL",
      "effectType": "NORMAL_DAMAGE",
      "remainingRounds": 2,
      "attackModifier": 0,
      "defenseModifier": 0,
      "speedModifier": 0,
      "normalDamageMultiplier": 0.15,
      "attributeDamageMultiplier": 0,
      "affectedAttribute": null
    }
  ],
  "skillStates": [
    {
      "slotIndex": 0,
      "skillId": 1001,
      "skillName": "Power Strike",
      "cooldownRemaining": 1,
      "canUse": false
    }
  ]
}
```

### 3.7 BattleAction

```json
{
  "actionOrder": 1,
  "round": 1,
  "actionType": "SKILL",
  "actorRuntimeId": "F-0",
  "actorName": "Hero 1",
  "targetRuntimeId": "E-0",
  "targetName": "Enemy 1",
  "skillId": 1001,
  "skillName": "Power Strike",
  "damage": 36,
  "targetHealthBefore": 95,
  "targetHealthAfter": 59,
  "damageFormula": {
    "baseDamage": 27,
    "skillMultiplier": 1.35,
    "normalBuffMultiplier": 0,
    "attributeBuffMultiplier": 0,
    "fieldBuffMultiplier": 0,
    "attributeRestraintMultiplier": 0,
    "bonusMultiplier": 1,
    "attackerAttribute": "FIRE",
    "defenderAttribute": "WOOD"
  },
  "appliedBuffs": [],
  "defeatedRuntimeIds": [],
  "animation": {
    "attackDirection": 1,
    "attackDuration": 0.28,
    "hitFlashDuration": 0.18
  },
  "log": "Hero 1 uses Power Strike on Enemy 1 for 36."
}
```

字段说明：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| actionOrder | number | 本回合动作顺序 |
| round | number | 当前回合 |
| actionType | string | 动作类型 |
| actorRuntimeId | string | 行动者运行时 ID |
| targetRuntimeId | string / null | 目标运行时 ID |
| skillId | number / null | 技能 ID，普通攻击为 null |
| damage | number | 造成伤害 |
| damageFormula | object | 本次伤害计算明细，包含基础伤害、技能倍率和四类增益倍率 |
| appliedBuffs | array | 本动作附加的 buff |
| defeatedRuntimeIds | array | 本动作导致死亡的角色 |
| animation | object | 客户端播放动画所需参数 |
| log | string | 战斗日志 |

## 4. 模式首页接口

### 4.1 获取 Idle Adventure 首页数据

```http
GET /api/idle-adventure/players/{playerId}/home
Authorization: Bearer <token>
```

是否需要 token：是

前端传参：无请求体

路径参数：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| playerId | number | 是 | 玩家 ID，必须等于 token 对应用户 ID |

后端处理：

1. 校验 token。
2. 查询玩家 Idle Adventure 进度。
3. 查询当前可用 NPC / 友方角色列表。
4. 查询玩家已保存编队。
5. 查询模式配置、buff 预设和技能配置。
6. 返回开始界面、编队界面和战斗开始所需数据。

返回 data：

```json
{
  "playerId": 1,
  "highestStage": 5,
  "currentStage": 1,
  "hasRunningSession": false,
  "runningSessionId": null,
  "modeConfig": {
    "maxPartySize": 6,
    "formationRows": 3,
    "formationColumns": 2,
    "friendlySkillSlotCount": 2,
    "enemySkillSlotCount": 1,
    "maxBuffIconSlots": 6
  },
  "formation": {
    "deployedCount": 3,
    "slots": [
      {
        "slotIndex": 0,
        "column": 0,
        "row": 0,
        "frontColumn": true,
        "definitionId": 101,
        "combatantName": "Hero 1"
      },
      {
        "slotIndex": 1,
        "column": 0,
        "row": 1,
        "frontColumn": true,
        "definitionId": 102,
        "combatantName": "Hero 2"
      },
      {
        "slotIndex": 2,
        "column": 0,
        "row": 2,
        "frontColumn": true,
        "definitionId": 103,
        "combatantName": "Hero 3"
      },
      {
        "slotIndex": 3,
        "column": 1,
        "row": 0,
        "frontColumn": false,
        "definitionId": null,
        "combatantName": null
      },
      {
        "slotIndex": 4,
        "column": 1,
        "row": 1,
        "frontColumn": false,
        "definitionId": null,
        "combatantName": null
      },
      {
        "slotIndex": 5,
        "column": 1,
        "row": 2,
        "frontColumn": false,
        "definitionId": null,
        "combatantName": null
      }
    ]
  },
  "availableNpcList": [
    {
      "definitionId": 101,
      "combatantKey": "hero_1",
      "combatantName": "Hero 1",
      "maxHealth": 160,
      "attack": 28,
      "defense": 5,
      "speed": 18,
      "assigned": true
    }
  ]
}
```

失败情况：

| 场景 | message |
| --- | --- |
| token 无效 | 未登录或登录已过期 |
| 访问其他玩家 | 不能访问其他玩家的数据 |

## 5. 配置接口

### 5.1 获取模式基础配置

```http
GET /api/idle-adventure/config
Authorization: Bearer <token>
```

是否需要 token：是

前端传参：无请求体

后端处理：

1. 查询 Idle Adventure 模式基础配置。
2. 返回阵型、技能槽、buff 图标、战斗动画时间和基础战斗规则。

返回 data：`IdleAdventureModeConfigResponse`

### 5.2 获取 buff 预设列表

```http
GET /api/idle-adventure/buff-presets
Authorization: Bearer <token>
```

是否需要 token：是

前端传参：无请求体

后端处理：

1. 查询 Idle Adventure 模式可用 buff 预设。
2. 按 `buffPresetId` 升序返回。

返回 data：

```json
[
  {
    "buffPresetId": 1,
    "buffKey": "normal_damage_up",
    "displayName": "Damage Up",
    "category": "BENEFIT",
    "source": "CHARACTER_SKILL",
    "effectType": "NORMAL_DAMAGE",
    "durationRounds": 3,
    "attackModifier": 0,
    "defenseModifier": 0,
    "speedModifier": 0,
    "normalDamageMultiplier": 0.15,
    "attributeDamageMultiplier": 0,
    "affectedAttribute": null
  },
  {
    "buffPresetId": 2,
    "buffKey": "fire_damage_down",
    "displayName": "Fire Damage Down",
    "category": "DETRIMENT",
    "source": "CHARACTER_SKILL",
    "effectType": "ATTRIBUTE_DAMAGE",
    "durationRounds": 3,
    "attackModifier": 0,
    "defenseModifier": 0,
    "speedModifier": 0,
    "normalDamageMultiplier": 0,
    "attributeDamageMultiplier": -0.15,
    "affectedAttribute": "FIRE"
  }
]
```

### 5.3 获取技能配置列表

```http
GET /api/idle-adventure/skills
Authorization: Bearer <token>
```

是否需要 token：是

前端传参：无请求体

后端处理：

1. 查询 Idle Adventure 模式技能配置。
2. 返回技能伤害、冷却和关联 buff 预设。

返回 data：

```json
[
  {
    "skillId": 1001,
    "skillKey": "power_strike",
    "skillName": "Power Strike",
    "cooldownRounds": 2,
    "skillMultiplier": 1.35,
    "damageAttribute": "PHYSICAL",
    "selfBuffPresetId": null,
    "targetBuffPresetId": null
  }
]
```

### 5.4 获取场地 buff 配置

```http
GET /api/idle-adventure/field-buffs?stage=1
Authorization: Bearer <token>
```

是否需要 token：是

前端传参：无请求体

查询参数：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| stage | number | 否 | 关卡编号；传入后只返回当前关卡生效的场地 buff |

后端处理：

1. 查询当前模式或指定关卡正在生效的场地 buff。
2. 只返回启用状态的配置。
3. 场地 buff 只对 `affectedAttribute` 匹配的角色生效。

返回 data：

```json
[
  {
    "fieldBuffId": 1,
    "fieldBuffKey": "fire_field_up",
    "displayName": "Fire Field Up",
    "category": "BENEFIT",
    "effectType": "FIELD",
    "targetSide": null,
    "affectedAttribute": "FIRE",
    "fieldDamageMultiplier": 0.15
  }
]
```

字段说明：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| fieldBuffId | number | 场地 buff ID |
| fieldBuffKey | string | 场地 buff key |
| displayName | string | 显示名 |
| category | string | `BENEFIT` 增益，`DETRIMENT` 减益 |
| effectType | string | 固定为 `FIELD` |
| targetSide | string / null | 限定阵营；null 表示双方都可生效 |
| affectedAttribute | string | 生效角色属性 |
| fieldDamageMultiplier | number | 场地伤害倍率，可正可负 |

### 5.5 获取属性克制关系

```http
GET /api/idle-adventure/attribute-restraints
Authorization: Bearer <token>
```

是否需要 token：是

前端传参：无请求体

后端处理：

1. 查询属性克制关系。
2. 属性克制关系只表示“是否克制”，不配置可变倍率。
3. 命中克制关系时，`attributeRestraintMultiplier` 固定为 `0.2`。

返回 data：

```json
{
  "restraintMultiplier": 0.2,
  "relations": [
    {
      "attackerAttribute": "FIRE",
      "defenderAttribute": "WOOD"
    },
    {
      "attackerAttribute": "WATER",
      "defenderAttribute": "FIRE"
    }
  ]
}
```

### 5.6 获取关卡奖励配置

```http
GET /api/idle-adventure/stage-rewards
Authorization: Bearer <token>
```

是否需要 token：是

前端传参：无请求体

后端处理：

1. 查询 Idle Adventure 模式每个关卡的奖励配置。
2. 每个关卡可以配置不同奖励。
3. 通关时奖励只进入待结算池，不立即发放。
4. 玩家选择退出模式时，统一结算并发放所有已通关关卡的待结算奖励。

返回 data：

```json
[
  {
    "stage": 1,
    "rewards": [
      {
        "itemKey": "gold",
        "displayName": "Gold",
        "quantity": 120
      },
      {
        "itemKey": "adventure_token",
        "displayName": "Adventure Token",
        "quantity": 1
      }
    ]
  },
  {
    "stage": 2,
    "rewards": [
      {
        "itemKey": "gold",
        "displayName": "Gold",
        "quantity": 180
      },
      {
        "itemKey": "wood",
        "displayName": "Wood",
        "quantity": 60
      }
    ]
  }
]
```

## 6. 友方角色与敌人接口

### 6.1 获取玩家可用于编队的 NPC 列表

```http
GET /api/idle-adventure/players/{playerId}/npcs
Authorization: Bearer <token>
```

是否需要 token：是

路径参数：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| playerId | number | 是 | 玩家 ID |

后端处理：

1. 校验 token 与 `playerId`。
2. 查询玩家已拥有或系统默认可用的 Idle Adventure 友方角色。
3. 查询每个角色的基础属性与技能槽。
4. 标记该角色当前是否已在编队中。

返回 data：

```json
[
  {
    "definitionId": 101,
    "combatantKey": "hero_1",
    "combatantName": "Hero 1",
    "side": "FRIENDLY",
    "maxHealth": 160,
    "attack": 28,
    "defense": 5,
    "speed": 18,
    "attributeType": "PHYSICAL",
    "assigned": true,
    "skillSlots": [
      {
        "slotIndex": 0,
        "skill": {
          "skillId": 1001,
          "skillKey": "power_strike",
          "skillName": "Power Strike",
          "cooldownRounds": 2,
          "skillMultiplier": 1.35,
          "damageAttribute": "PHYSICAL",
          "selfBuffPresetId": null,
          "targetBuffPresetId": null
        }
      },
      {
        "slotIndex": 1,
        "skill": {
          "skillId": 1002,
          "skillKey": "battle_focus",
          "skillName": "Battle Focus",
          "cooldownRounds": 3,
          "skillMultiplier": 1,
          "damageAttribute": "PHYSICAL",
          "selfBuffPresetId": 1,
          "targetBuffPresetId": null
        }
      }
    ]
  }
]
```

失败情况：

| 场景 | message |
| --- | --- |
| 访问其他玩家 | 不能访问其他玩家的数据 |

### 6.2 获取指定关卡敌人列表

```http
GET /api/idle-adventure/stages/{stage}/enemies
Authorization: Bearer <token>
```

是否需要 token：是

路径参数：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| stage | number | 是 | 关卡编号，从 1 开始 |

后端处理：

1. 读取第 `stage` 关的敌人配置。
2. 根据关卡成长规则计算敌人数量和属性。
3. 返回固定 6 个槽位，未使用槽位为 `null`，方便客户端按两列三行布局渲染。

敌人数计算：

```text
enemyCount = clamp(baseEnemyCount + floor((stage - 1) / addEnemyEveryStages), 1, 6)
```

敌人成长：

```text
health = enemyBaseHealth + (stage - 1) * enemyHealthPerStage + slotIndex * 12
attack = enemyBaseAttack + (stage - 1) * enemyAttackPerStage + slotIndex * 2
speed = enemyBaseSpeed + (stage - 1) * enemySpeedPerStage + (6 - slotIndex)
```

返回 data：

```json
{
  "stage": 1,
  "enemyCount": 2,
  "slots": [
    {
      "slotIndex": 0,
      "enemy": {
        "definitionId": 201,
        "combatantKey": "enemy_1",
        "combatantName": "Enemy 1",
        "side": "ENEMY",
        "maxHealth": 95,
        "attack": 15,
        "defense": 1,
        "speed": 15,
        "attributeType": "PHYSICAL",
        "skillSlots": [
          {
            "slotIndex": 0,
            "skill": {
              "skillId": 2001,
              "skillKey": "claw",
              "skillName": "Claw",
              "cooldownRounds": 2,
              "skillMultiplier": 1.2,
              "damageAttribute": "PHYSICAL",
              "selfBuffPresetId": null,
              "targetBuffPresetId": null
            }
          }
        ]
      }
    },
    {
      "slotIndex": 1,
      "enemy": {
        "definitionId": 202,
        "combatantKey": "enemy_2",
        "combatantName": "Enemy 2",
        "side": "ENEMY",
        "maxHealth": 107,
        "attack": 17,
        "defense": 1,
        "speed": 14,
        "attributeType": "PHYSICAL",
        "skillSlots": []
      }
    },
    {
      "slotIndex": 2,
      "enemy": null
    },
    {
      "slotIndex": 3,
      "enemy": null
    },
    {
      "slotIndex": 4,
      "enemy": null
    },
    {
      "slotIndex": 5,
      "enemy": null
    }
  ]
}
```

## 7. 编队接口

### 7.1 获取玩家当前编队

```http
GET /api/idle-adventure/players/{playerId}/formation
Authorization: Bearer <token>
```

是否需要 token：是

后端处理：

1. 校验 token 与 `playerId`。
2. 查询玩家已保存的 Idle Adventure 编队。
3. 如果玩家没有编队记录，生成默认编队：按可用 NPC 顺序填入前几个槽位。
4. 返回固定 6 个槽位。

返回 data：

```json
{
  "playerId": 1,
  "deployedCount": 3,
  "slots": [
    {
      "slotIndex": 0,
      "column": 0,
      "row": 0,
      "frontColumn": true,
      "definitionId": 101,
      "combatantName": "Hero 1"
    },
    {
      "slotIndex": 1,
      "column": 0,
      "row": 1,
      "frontColumn": true,
      "definitionId": 102,
      "combatantName": "Hero 2"
    },
    {
      "slotIndex": 2,
      "column": 0,
      "row": 2,
      "frontColumn": true,
      "definitionId": 103,
      "combatantName": "Hero 3"
    },
    {
      "slotIndex": 3,
      "column": 1,
      "row": 0,
      "frontColumn": false,
      "definitionId": null,
      "combatantName": null
    },
    {
      "slotIndex": 4,
      "column": 1,
      "row": 1,
      "frontColumn": false,
      "definitionId": null,
      "combatantName": null
    },
    {
      "slotIndex": 5,
      "column": 1,
      "row": 2,
      "frontColumn": false,
      "definitionId": null,
      "combatantName": null
    }
  ]
}
```

### 7.2 保存玩家编队

```http
PUT /api/idle-adventure/players/{playerId}/formation
Authorization: Bearer <token>
Content-Type: application/json
```

是否需要 token：是

前端传参：

```json
{
  "slots": [
    {
      "slotIndex": 0,
      "definitionId": 101
    },
    {
      "slotIndex": 1,
      "definitionId": 102
    },
    {
      "slotIndex": 2,
      "definitionId": 103
    },
    {
      "slotIndex": 3,
      "definitionId": null
    },
    {
      "slotIndex": 4,
      "definitionId": null
    },
    {
      "slotIndex": 5,
      "definitionId": null
    }
  ]
}
```

参数说明：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| slots | array | 是 | 固定 6 个槽位，也允许只传发生变化的槽位 |
| slots[].slotIndex | number | 是 | 槽位编号，0-5 |
| slots[].definitionId | number / null | 是 | 友方角色配置 ID，null 表示空槽 |

后端处理：

1. 校验 token 与 `playerId`。
2. 校验 `slotIndex` 范围必须为 0-5。
3. 校验 `definitionId` 必须属于该玩家可用 NPC。
4. 校验同一名角色不能重复上阵。
5. 校验保存后至少存在 1 名上阵角色。
6. 保存玩家编队。
7. 返回保存后的完整 6 槽位编队。

返回 data：同 `7.1 获取玩家当前编队`

失败情况：

| 场景 | message |
| --- | --- |
| 编队为空 | 至少需要上阵 1 名友方角色 |
| 槽位越界 | slotIndex 必须在 0-5 范围内 |
| 角色重复上阵 | 同一角色不能重复上阵 |
| 角色不可用 | 该角色不可用于当前模式 |

### 7.3 自动填充默认编队

```http
POST /api/idle-adventure/players/{playerId}/formation/auto-fill
Authorization: Bearer <token>
```

是否需要 token：是

前端传参：无请求体

后端处理：

1. 查询玩家可用 NPC。
2. 根据默认友方数量 `startingFriendlyCount`，从可用 NPC 中按配置顺序填充。
3. 优先填充前排 `slotIndex=0,1,2`，数量超过 3 时再填充后排。
4. 保存并返回完整编队。

返回 data：同 `7.1 获取玩家当前编队`

### 7.4 清空编队

```http
POST /api/idle-adventure/players/{playerId}/formation/clear
Authorization: Bearer <token>
```

是否需要 token：是

说明：当前客户端“开始战斗”要求至少 1 名角色，因此清空编队后开始按钮应置为不可用。后端允许临时保存空编队，或也可以按业务要求拒绝清空。推荐允许清空，保存时返回 `deployedCount=0`。

后端处理：

1. 校验 token 与 `playerId`。
2. 删除或置空玩家 6 个编队槽位。
3. 返回空编队。

返回 data：

```json
{
  "playerId": 1,
  "deployedCount": 0,
  "slots": [
    {
      "slotIndex": 0,
      "column": 0,
      "row": 0,
      "frontColumn": true,
      "definitionId": null,
      "combatantName": null
    },
    {
      "slotIndex": 1,
      "column": 0,
      "row": 1,
      "frontColumn": true,
      "definitionId": null,
      "combatantName": null
    },
    {
      "slotIndex": 2,
      "column": 0,
      "row": 2,
      "frontColumn": true,
      "definitionId": null,
      "combatantName": null
    },
    {
      "slotIndex": 3,
      "column": 1,
      "row": 0,
      "frontColumn": false,
      "definitionId": null,
      "combatantName": null
    },
    {
      "slotIndex": 4,
      "column": 1,
      "row": 1,
      "frontColumn": false,
      "definitionId": null,
      "combatantName": null
    },
    {
      "slotIndex": 5,
      "column": 1,
      "row": 2,
      "frontColumn": false,
      "definitionId": null,
      "combatantName": null
    }
  ]
}
```

## 8. 战斗会话接口

### 8.1 创建并开始战斗

```http
POST /api/idle-adventure/players/{playerId}/sessions/start
Authorization: Bearer <token>
Content-Type: application/json
```

是否需要 token：是

前端传参：

```json
{
  "stage": 1,
  "useSavedFormation": true,
  "formationSlots": null
}
```

也可以传入临时编队：

```json
{
  "stage": 1,
  "useSavedFormation": false,
  "formationSlots": [
    {
      "slotIndex": 0,
      "definitionId": 101
    },
    {
      "slotIndex": 3,
      "definitionId": 102
    }
  ]
}
```

参数说明：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| stage | number | 否 | 开始关卡，默认 1 |
| useSavedFormation | boolean | 否 | 是否使用后端保存的编队，默认 true |
| formationSlots | array / null | 否 | 临时编队，仅 `useSavedFormation=false` 时使用 |

后端处理：

1. 校验 token 与 `playerId`。
2. 读取保存编队或请求中的临时编队。
3. 校验至少 1 名友方角色上阵。
4. 生成友方运行时状态，空槽保留为 null。
5. 根据关卡生成敌方运行时状态，空槽保留为 null。
6. 创建战斗会话，状态设为 `RUNNING`。
7. 返回初始战斗状态。

返回 data：

```json
{
  "sessionId": "ia-20260720-000001",
  "playerId": 1,
  "stage": 1,
  "round": 1,
  "status": "RUNNING",
  "paused": false,
  "stageChoicePending": false,
  "pendingNextStage": null,
  "friendlies": [
    {
      "runtimeId": "F-0",
      "definitionId": 101,
      "combatantName": "Hero 1",
      "side": "FRIENDLY",
      "slotIndex": 0,
      "maxHealth": 160,
      "currentHealth": 160,
      "attack": 28,
      "defense": 5,
      "speed": 18,
      "attributeType": "PHYSICAL",
      "effectiveAttack": 28,
      "effectiveDefense": 5,
      "effectiveSpeed": 18,
      "defeated": false,
      "activeBuffs": [],
      "skillStates": [
        {
          "slotIndex": 0,
          "skillId": 1001,
          "skillName": "Power Strike",
          "cooldownRemaining": 0,
          "canUse": true
        }
      ]
    },
    null,
    null,
    null,
    null,
    null
  ],
  "enemies": [
    {
      "runtimeId": "E-0",
      "definitionId": 201,
      "combatantName": "Enemy 1",
      "side": "ENEMY",
      "slotIndex": 0,
      "maxHealth": 95,
      "currentHealth": 95,
      "attack": 15,
      "defense": 1,
      "speed": 15,
      "attributeType": "PHYSICAL",
      "effectiveAttack": 15,
      "effectiveDefense": 1,
      "effectiveSpeed": 15,
      "defeated": false,
      "activeBuffs": [],
      "skillStates": []
    },
    null,
    null,
    null,
    null,
    null
  ],
  "logs": [
    "Stage 1 begins. 1 friendly unit(s) vs 1 enemy unit(s)."
  ]
}
```

失败情况：

| 场景 | message |
| --- | --- |
| 编队为空 | 至少需要上阵 1 名友方角色 |
| 指定关卡非法 | stage 必须大于等于 1 |
| 已存在运行中的会话 | 已存在进行中的战斗会话 |

### 8.2 获取战斗会话状态

```http
GET /api/idle-adventure/players/{playerId}/sessions/{sessionId}
Authorization: Bearer <token>
```

是否需要 token：是

后端处理：

1. 校验 token 与 `playerId`。
2. 查询战斗会话。
3. 返回当前关卡、回合、暂停状态、双方角色状态、日志和待选择的下一关。

返回 data：同 `8.1 创建并开始战斗`

### 8.3 暂停战斗

```http
POST /api/idle-adventure/players/{playerId}/sessions/{sessionId}/pause
Authorization: Bearer <token>
```

是否需要 token：是

后端处理：

1. 校验会话属于该玩家。
2. 如果会话状态为 `RUNNING`，更新为 `PAUSED`。
3. 返回最新会话状态。

返回 data：

```json
{
  "sessionId": "ia-20260720-000001",
  "status": "PAUSED",
  "paused": true,
  "message": "Battle paused."
}
```

失败情况：

| 场景 | message |
| --- | --- |
| 会话不存在 | 战斗会话不存在 |
| 已在关卡选择中 | 当前战斗状态不允许此操作 |

### 8.4 恢复战斗

```http
POST /api/idle-adventure/players/{playerId}/sessions/{sessionId}/resume
Authorization: Bearer <token>
```

是否需要 token：是

后端处理：

1. 校验会话属于该玩家。
2. 如果会话状态为 `PAUSED`，更新为 `RUNNING`。
3. 返回最新会话状态。

返回 data：

```json
{
  "sessionId": "ia-20260720-000001",
  "status": "RUNNING",
  "paused": false,
  "message": "Battle resumed."
}
```

### 8.5 结束模式

```http
POST /api/idle-adventure/players/{playerId}/sessions/{sessionId}/end
Authorization: Bearer <token>
```

是否需要 token：是

说明：玩家选择结束/退出模式时，后端应结算本次模式中所有已通关关卡的待发放奖励，并将奖励发放到玩家背包或资源账户。未通关的当前关卡不产生奖励。

后端处理：

1. 校验会话属于该玩家。
2. 查询本次会话中已通关但尚未结算的关卡。
3. 汇总这些关卡的奖励配置。
4. 将奖励写入玩家背包/资源账户。
5. 标记这些关卡奖励已结算，避免重复领取。
6. 将会话状态更新为 `ENDED`。
7. 返回本次发放奖励明细和最新背包快照。

返回 data：

```json
{
  "sessionId": "ia-20260720-000001",
  "status": "ENDED",
  "settledStages": [1, 2],
  "grantedRewards": [
    {
      "itemKey": "gold",
      "displayName": "Gold",
      "quantity": 300
    },
    {
      "itemKey": "adventure_token",
      "displayName": "Adventure Token",
      "quantity": 2
    }
  ],
  "items": [
    {
      "itemId": 1,
      "itemKey": "gold",
      "name": "金币",
      "itemType": "CURRENCY",
      "icon": "gold",
      "quantity": 10300
    }
  ],
  "message": "Idle Adventure rewards granted."
}
```

## 9. 回合结算接口

### 9.1 结算一整个回合

```http
POST /api/idle-adventure/players/{playerId}/sessions/{sessionId}/rounds/resolve
Authorization: Bearer <token>
Content-Type: application/json
```

是否需要 token：是

前端传参：

```json
{
  "clientRound": 1
}
```

参数说明：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| clientRound | number | 是 | 客户端认为正在结算的回合，用于防止重复请求 |

后端处理：

1. 校验 token 与 `playerId`。
2. 校验会话属于该玩家，且状态为 `RUNNING`。
3. 校验 `clientRound` 等于服务端当前回合。
4. 构建行动顺序：所有存活角色按 `effectiveSpeed` 降序。
5. 按行动顺序逐个结算攻击、技能、buff、死亡。
6. 如果当前关敌人全部死亡，状态设为 `STAGE_COMPLETE`，设置 `pendingNextStage=stage+1`。
7. 如果友方全部死亡，状态设为 `FAILED`。
8. 如果双方都未全部死亡，回合数加 1。
9. 返回动作列表和最新战斗状态。

返回 data：

```json
{
  "sessionId": "ia-20260720-000001",
  "stage": 1,
  "roundBefore": 1,
  "roundAfter": 2,
  "status": "RUNNING",
  "initiativeOrder": [
    {
      "runtimeId": "F-0",
      "combatantName": "Hero 1",
      "side": "FRIENDLY",
      "slotIndex": 0,
      "effectiveSpeed": 18
    },
    {
      "runtimeId": "E-0",
      "combatantName": "Enemy 1",
      "side": "ENEMY",
      "slotIndex": 0,
      "effectiveSpeed": 15
    }
  ],
  "actions": [
    {
      "actionOrder": 1,
      "round": 1,
      "actionType": "SKILL",
      "actorRuntimeId": "F-0",
      "actorName": "Hero 1",
      "targetRuntimeId": "E-0",
      "targetName": "Enemy 1",
      "skillId": 1001,
      "skillName": "Power Strike",
      "damage": 36,
      "targetHealthBefore": 95,
      "targetHealthAfter": 59,
      "damageFormula": {
        "baseDamage": 27,
        "skillMultiplier": 1.35,
        "normalBuffMultiplier": 0,
        "attributeBuffMultiplier": 0,
        "fieldBuffMultiplier": 0,
        "attributeRestraintMultiplier": 0,
        "bonusMultiplier": 1,
        "attackerAttribute": "FIRE",
        "defenderAttribute": "WOOD"
      },
      "appliedBuffs": [],
      "defeatedRuntimeIds": [],
      "animation": {
        "attackDirection": 1,
        "attackDuration": 0.28,
        "hitFlashDuration": 0.18
      },
      "log": "Hero 1 uses Power Strike on Enemy 1 for 36."
    },
    {
      "actionOrder": 2,
      "round": 1,
      "actionType": "BASIC_ATTACK",
      "actorRuntimeId": "E-0",
      "actorName": "Enemy 1",
      "targetRuntimeId": "F-0",
      "targetName": "Hero 1",
      "skillId": null,
      "skillName": null,
      "damage": 10,
      "targetHealthBefore": 160,
      "targetHealthAfter": 150,
      "damageFormula": {
        "baseDamage": 10,
        "skillMultiplier": 1,
        "normalBuffMultiplier": 0,
        "attributeBuffMultiplier": 0,
        "fieldBuffMultiplier": 0,
        "attributeRestraintMultiplier": 0,
        "bonusMultiplier": 1,
        "attackerAttribute": "WOOD",
        "defenderAttribute": "FIRE"
      },
      "appliedBuffs": [],
      "defeatedRuntimeIds": [],
      "animation": {
        "attackDirection": -1,
        "attackDuration": 0.28,
        "hitFlashDuration": 0.18
      },
      "log": "Enemy 1 uses Attack on Hero 1 for 10."
    }
  ],
  "friendlies": [],
  "enemies": [],
  "logs": [
    "Initiative: Hero 1(18) > Enemy 1(15)",
    "Hero 1 uses Power Strike on Enemy 1 for 36.",
    "Enemy 1 uses Attack on Hero 1 for 10."
  ]
}
```

说明：`friendlies` 和 `enemies` 应返回完整最新状态，结构同 `CombatantState[]`。示例中省略是为了减少篇幅，实际接口不得省略。

失败情况：

| 场景 | message |
| --- | --- |
| 会话不存在 | 战斗会话不存在 |
| 会话未运行 | 当前战斗状态不允许此操作 |
| 回合号不一致 | 客户端回合号与服务端不一致，请刷新战斗状态 |

### 9.2 手动施加 buff

```http
POST /api/idle-adventure/players/{playerId}/sessions/{sessionId}/buffs/apply
Authorization: Bearer <token>
Content-Type: application/json
```

是否需要 token：是

说明：此接口用于后续道具、事件、关卡机制等“其他方式增加的 buff”。当前 Unity 脚本中已区分 `CHARACTER_SKILL` 和 `OTHER` 来源；技能自带 buff 不需要调用此接口，由回合结算自动处理。

前端传参：

```json
{
  "targetSide": "FRIENDLY",
  "slotIndex": 0,
  "buffPresetId": 3,
  "source": "OTHER"
}
```

参数说明：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| targetSide | string | 是 | `FRIENDLY` 或 `ENEMY` |
| slotIndex | number | 是 | 目标槽位，0-5 |
| buffPresetId | number | 是 | buff 预设 ID |
| source | string | 否 | 推荐固定为 `OTHER` |

后端处理：

1. 校验战斗会话属于该玩家。
2. 校验目标槽位存在存活角色。
3. 查询 buff 预设。
4. 如果同一角色已有相同 `buffKey + source`，刷新剩余回合为两者较大值。
5. 否则追加新 buff。
6. 返回目标角色最新状态。

返回 data：

```json
{
  "target": {
    "runtimeId": "F-0",
    "combatantName": "Hero 1",
    "side": "FRIENDLY",
    "slotIndex": 0,
    "activeBuffs": [
      {
        "buffKey": "normal_damage_up",
        "displayName": "Damage Up",
        "category": "BENEFIT",
        "source": "OTHER",
        "effectType": "NORMAL_DAMAGE",
        "remainingRounds": 3,
        "attackModifier": 0,
        "defenseModifier": 0,
        "speedModifier": 0,
        "normalDamageMultiplier": 0.15,
        "attributeDamageMultiplier": 0,
        "affectedAttribute": null
      }
    ]
  },
  "log": "Hero 1 gained Damage Up from Other."
}
```

## 10. 关卡结算接口

### 10.1 继续下一关

```http
POST /api/idle-adventure/players/{playerId}/sessions/{sessionId}/stages/continue
Authorization: Bearer <token>
```

是否需要 token：是

调用时机：当前关敌方全部死亡，服务端会话状态为 `STAGE_COMPLETE`，客户端显示结束面板后，玩家点击“继续下一关”。

后端处理：

1. 校验会话属于该玩家。
2. 校验会话状态为 `STAGE_COMPLETE`。
3. 将 `stage` 更新为 `pendingNextStage`。
4. 友方角色恢复满血。
5. 根据新关卡生成敌方阵容。
6. 将刚通关关卡的奖励加入本次会话待结算池，但不立即发放。
7. 清空或保留战斗日志，推荐保留最近 8 条。
8. 状态更新为 `RUNNING`。
9. 返回新关卡初始战斗状态和当前待结算奖励摘要。

返回 data：同 `8.1 创建并开始战斗`

失败情况：

| 场景 | message |
| --- | --- |
| 当前关尚未完成 | 当前战斗状态不允许此操作 |
| 会话不存在 | 战斗会话不存在 |

### 10.2 重新挑战当前关

```http
POST /api/idle-adventure/players/{playerId}/sessions/{sessionId}/stages/retry
Authorization: Bearer <token>
```

是否需要 token：是

调用时机：友方全部死亡，服务端会话状态为 `FAILED`。当前 Unity 脚本中存在友方恢复并重新生成敌人的逻辑，后端可用该接口承接。

后端处理：

1. 校验会话属于该玩家。
2. 校验会话状态为 `FAILED`。
3. 友方角色恢复满血。
4. 重新生成当前关敌方阵容。
5. 回合重置为 1。
6. 状态更新为 `RUNNING`。

返回 data：同 `8.1 创建并开始战斗`

## 11. 进度接口

### 11.1 获取玩家闯关进度

```http
GET /api/idle-adventure/players/{playerId}/progress
Authorization: Bearer <token>
```

是否需要 token：是

后端处理：

1. 校验 token 与 `playerId`。
2. 查询玩家最高通关关卡、最近会话、累计胜利次数、累计失败次数。

返回 data：

```json
{
  "playerId": 1,
  "highestStage": 5,
  "lastStage": 3,
  "runningSessionId": null,
  "totalWins": 12,
  "totalFailures": 2,
  "updatedAt": "2026-07-20T12:30:00+08:00"
}
```

### 11.2 保存关卡胜利进度

```http
POST /api/idle-adventure/players/{playerId}/progress/stage-clear
Authorization: Bearer <token>
Content-Type: application/json
```

是否需要 token：是

前端传参：

```json
{
  "sessionId": "ia-20260720-000001",
  "clearedStage": 5
}
```

说明：如果后端已经在 `rounds/resolve` 中权威判断胜利，则此接口可以不开放给客户端，避免客户端伪造通关。推荐由后端在关卡胜利时自动更新进度。

后端处理：

1. 校验会话属于该玩家。
2. 校验该会话确实已通关 `clearedStage`。
3. 更新玩家最高通关关卡。
4. 返回最新进度。

返回 data：同 `11.1 获取玩家闯关进度`

## 12. 推荐数据库表

### 12.1 idle_adventure_combatant_config

角色配置表，友方 NPC 和敌方都可放在同一张表中，用 `side` 区分。

```sql
CREATE TABLE idle_adventure_combatant_config (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  combatant_key VARCHAR(64) NOT NULL UNIQUE,
  combatant_name VARCHAR(64) NOT NULL,
  side VARCHAR(16) NOT NULL,
  max_health INT NOT NULL DEFAULT 100,
  attack INT NOT NULL DEFAULT 10,
  defense INT NOT NULL DEFAULT 0,
  speed INT NOT NULL DEFAULT 1,
  attribute_type VARCHAR(32) NOT NULL DEFAULT 'PHYSICAL',
  enabled TINYINT NOT NULL DEFAULT 1,
  sort_order INT NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);
```

### 12.2 idle_adventure_skill_config

```sql
CREATE TABLE idle_adventure_skill_config (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  skill_key VARCHAR(64) NOT NULL UNIQUE,
  skill_name VARCHAR(64) NOT NULL,
  cooldown_rounds INT NOT NULL DEFAULT 0,
  skill_multiplier DECIMAL(8,4) NOT NULL DEFAULT 1.0000,
  damage_attribute VARCHAR(32) NULL,
  self_buff_preset_id BIGINT NULL,
  target_buff_preset_id BIGINT NULL,
  enabled TINYINT NOT NULL DEFAULT 1,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);
```

### 12.3 idle_adventure_combatant_skill

角色技能槽配置表。友方最多 2 个技能槽，敌方最多 1 个技能槽。

```sql
CREATE TABLE idle_adventure_combatant_skill (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  combatant_config_id BIGINT NOT NULL,
  slot_index INT NOT NULL,
  skill_id BIGINT NOT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY uk_combatant_skill_slot (combatant_config_id, slot_index)
);
```

### 12.4 idle_adventure_buff_preset

```sql
CREATE TABLE idle_adventure_buff_preset (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  buff_key VARCHAR(64) NOT NULL,
  display_name VARCHAR(64) NOT NULL,
  category VARCHAR(16) NOT NULL,
  source VARCHAR(32) NOT NULL,
  effect_type VARCHAR(32) NOT NULL DEFAULT 'NORMAL_DAMAGE',
  duration_rounds INT NOT NULL DEFAULT 1,
  attack_modifier INT NOT NULL DEFAULT 0,
  defense_modifier INT NOT NULL DEFAULT 0,
  speed_modifier INT NOT NULL DEFAULT 0,
  normal_damage_multiplier DECIMAL(8,4) NOT NULL DEFAULT 0.0000,
  attribute_damage_multiplier DECIMAL(8,4) NOT NULL DEFAULT 0.0000,
  affected_attribute VARCHAR(32) NULL,
  enabled TINYINT NOT NULL DEFAULT 1,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uk_buff_key_source (buff_key, source)
);
```

### 12.4.1 idle_adventure_field_buff_config

场地 buff 配置表，用于计算 `场地buff倍率`。后续可以按关卡、地图、天气或特殊事件决定是否启用。

```sql
CREATE TABLE idle_adventure_field_buff_config (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  field_buff_key VARCHAR(64) NOT NULL UNIQUE,
  display_name VARCHAR(64) NOT NULL,
  target_side VARCHAR(16) NULL,
  affected_attribute VARCHAR(32) NOT NULL,
  field_damage_multiplier DECIMAL(8,4) NOT NULL DEFAULT 0.0000,
  enabled TINYINT NOT NULL DEFAULT 1,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);
```

### 12.4.2 idle_adventure_attribute_restraint_config

属性克制配置表，用于判断属性克制关系。克制关系命中时，`attributeRestraintMultiplier` 固定为 `0.2`，不在数据库中配置可变倍率。

```sql
CREATE TABLE idle_adventure_attribute_restraint_config (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  attacker_attribute VARCHAR(32) NOT NULL,
  defender_attribute VARCHAR(32) NOT NULL,
  enabled TINYINT NOT NULL DEFAULT 1,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uk_idle_attribute_restraint (attacker_attribute, defender_attribute)
);
```

### 12.4.3 idle_adventure_stage_reward_config

关卡奖励配置表。每个关卡可以配置多条奖励，通关时写入会话待结算池，玩家退出模式时统一发放。

```sql
CREATE TABLE idle_adventure_stage_reward_config (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  stage INT NOT NULL,
  item_key VARCHAR(64) NOT NULL,
  display_name VARCHAR(64) NOT NULL,
  quantity INT NOT NULL DEFAULT 1,
  enabled TINYINT NOT NULL DEFAULT 1,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uk_idle_stage_reward_item (stage, item_key)
);
```

### 12.5 player_idle_adventure_npc

玩家可用 NPC 表。若当前版本所有玩家默认拥有同一批 NPC，也可以暂时不建该表，直接从配置表返回友方角色。

```sql
CREATE TABLE player_idle_adventure_npc (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  player_id BIGINT NOT NULL,
  combatant_config_id BIGINT NOT NULL,
  unlocked TINYINT NOT NULL DEFAULT 1,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uk_player_idle_npc (player_id, combatant_config_id)
);
```

### 12.6 player_idle_adventure_formation

玩家编队表。每名玩家最多保存 6 个槽位。

```sql
CREATE TABLE player_idle_adventure_formation (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  player_id BIGINT NOT NULL,
  slot_index INT NOT NULL,
  combatant_config_id BIGINT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uk_player_idle_formation_slot (player_id, slot_index)
);
```

### 12.7 idle_adventure_session

战斗会话主表。

```sql
CREATE TABLE idle_adventure_session (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  session_key VARCHAR(64) NOT NULL UNIQUE,
  player_id BIGINT NOT NULL,
  stage INT NOT NULL DEFAULT 1,
  round INT NOT NULL DEFAULT 1,
  status VARCHAR(32) NOT NULL,
  paused TINYINT NOT NULL DEFAULT 0,
  mode_paused_by_choice TINYINT NOT NULL DEFAULT 0,
  pending_next_stage INT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);
```

### 12.8 idle_adventure_session_combatant

战斗会话角色状态表。

```sql
CREATE TABLE idle_adventure_session_combatant (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  session_id BIGINT NOT NULL,
  runtime_id VARCHAR(32) NOT NULL,
  combatant_config_id BIGINT NOT NULL,
  side VARCHAR(16) NOT NULL,
  slot_index INT NOT NULL,
  combatant_name VARCHAR(64) NOT NULL,
  max_health INT NOT NULL,
  current_health INT NOT NULL,
  attack INT NOT NULL,
  defense INT NOT NULL,
  speed INT NOT NULL,
  attribute_type VARCHAR(32) NOT NULL DEFAULT 'PHYSICAL',
  defeated TINYINT NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uk_session_runtime (session_id, runtime_id),
  UNIQUE KEY uk_session_side_slot (session_id, side, slot_index)
);
```

### 12.8.1 idle_adventure_session_pending_reward

战斗会话待结算奖励表。当前会话每通关一关，将该关奖励累计到此表；玩家选择退出模式时发放并标记为已结算。

```sql
CREATE TABLE idle_adventure_session_pending_reward (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  session_id BIGINT NOT NULL,
  player_id BIGINT NOT NULL,
  cleared_stage INT NOT NULL,
  item_key VARCHAR(64) NOT NULL,
  display_name VARCHAR(64) NOT NULL,
  quantity INT NOT NULL DEFAULT 1,
  settled TINYINT NOT NULL DEFAULT 0,
  settled_at DATETIME NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uk_idle_session_stage_reward (session_id, cleared_stage, item_key)
);
```

### 12.9 idle_adventure_session_buff

战斗会话角色身上的运行时 buff。

```sql
CREATE TABLE idle_adventure_session_buff (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  session_combatant_id BIGINT NOT NULL,
  buff_key VARCHAR(64) NOT NULL,
  display_name VARCHAR(64) NOT NULL,
  category VARCHAR(16) NOT NULL,
  source VARCHAR(32) NOT NULL,
  effect_type VARCHAR(32) NOT NULL DEFAULT 'NORMAL_DAMAGE',
  remaining_rounds INT NOT NULL,
  attack_modifier INT NOT NULL DEFAULT 0,
  defense_modifier INT NOT NULL DEFAULT 0,
  speed_modifier INT NOT NULL DEFAULT 0,
  normal_damage_multiplier DECIMAL(8,4) NOT NULL DEFAULT 0.0000,
  attribute_damage_multiplier DECIMAL(8,4) NOT NULL DEFAULT 0.0000,
  affected_attribute VARCHAR(32) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uk_combatant_buff_source (session_combatant_id, buff_key, source)
);
```

### 12.10 idle_adventure_session_skill_state

战斗会话技能冷却状态表。

```sql
CREATE TABLE idle_adventure_session_skill_state (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  session_combatant_id BIGINT NOT NULL,
  slot_index INT NOT NULL,
  skill_id BIGINT NULL,
  cooldown_remaining INT NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uk_combatant_skill_state_slot (session_combatant_id, slot_index)
);
```

### 12.11 player_idle_adventure_progress

```sql
CREATE TABLE player_idle_adventure_progress (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  player_id BIGINT NOT NULL UNIQUE,
  highest_stage INT NOT NULL DEFAULT 0,
  last_stage INT NOT NULL DEFAULT 1,
  total_wins INT NOT NULL DEFAULT 0,
  total_failures INT NOT NULL DEFAULT 0,
  running_session_id BIGINT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);
```

## 13. Unity 前端联调边界

### 13.1 开始界面

开始界面只负责展示模式入口，不应自动进入战斗。推荐流程：

1. 进入 `Idle Adventure mode` 场景。
2. Unity 调用 `GET /api/idle-adventure/players/{playerId}/home`。
3. 渲染开始界面。
4. 玩家点击 `Formation` 时打开编队界面。
5. 玩家点击 `Start Battle` 时调用 `POST /api/idle-adventure/players/{playerId}/sessions/start`。
6. 后端返回 `RUNNING` 后，Unity 隐藏开始界面，显示战斗界面。

### 13.2 编队界面

编队界面交互：

1. 下方显示 `availableNpcList`。
2. 中间显示 6 个 `FormationSlot`。
3. 拖动 NPC 到槽位时，Unity 本地可以先预览。
4. 点击确认时调用 `PUT /api/idle-adventure/players/{playerId}/formation` 保存。
5. 后端返回保存后的完整编队。
6. 如果 `deployedCount=0`，开始按钮置为不可点击。

### 13.3 战斗界面

如果后端权威结算：

1. Unity 调用 `POST /rounds/resolve` 请求结算一整个回合。
2. 后端返回 `actions`。
3. Unity 按 `actions` 顺序播放攻击动画、受击动画、血条变化、buff 图标变化、死亡表现。
4. 播放完毕后，如果 `status=RUNNING`，继续请求下一回合。
5. 如果 `status=STAGE_COMPLETE`，弹出结束面板。
6. 如果 `status=FAILED`，显示失败或重试界面。

如果客户端暂时继续本地结算：

1. 后端只提供配置、编队、敌人和进度保存。
2. Unity 按当前脚本本地结算战斗。
3. 关卡胜利后调用进度接口保存结果。
4. 后续正式上线前建议迁移到后端权威结算，避免客户端伪造战斗结果。

### 13.4 buff 显示

客户端只保留角色图标下方一排 buff 图标，不显示额外文字信息。每个 buff 图标颜色或样式可按 `effectType + category + source` 区分：

| effectType | category | source | 建议显示 |
| --- | --- | --- | --- |
| NORMAL_DAMAGE | BENEFIT | CHARACTER_SKILL / OTHER | 普通伤害增益图标 |
| NORMAL_DAMAGE | DETRIMENT | CHARACTER_SKILL / OTHER | 普通伤害减益图标 |
| ATTRIBUTE_DAMAGE | BENEFIT | CHARACTER_SKILL / OTHER | 属性增强图标，并标记 `affectedAttribute` |
| ATTRIBUTE_DAMAGE | DETRIMENT | CHARACTER_SKILL / OTHER | 属性减益图标，并标记 `affectedAttribute` |
| FIELD | BENEFIT | OTHER | 场地增益图标，并标记 `affectedAttribute` |
| FIELD | DETRIMENT | OTHER | 场地减益图标，并标记 `affectedAttribute` |

## 14. 最小联调接口清单

如果后端想先支持当前 Unity 模式进入可用状态，建议优先实现以下接口：

| 优先级 | 接口 | 用途 |
| --- | --- | --- |
| P0 | `GET /api/idle-adventure/players/{playerId}/home` | 进入模式时加载首页、可用 NPC、保存编队 |
| P0 | `GET /api/idle-adventure/players/{playerId}/npcs` | 编队界面下方 NPC 列表 |
| P0 | `GET /api/idle-adventure/players/{playerId}/formation` | 获取保存编队 |
| P0 | `PUT /api/idle-adventure/players/{playerId}/formation` | 保存拖拽后的编队 |
| P0 | `POST /api/idle-adventure/players/{playerId}/sessions/start` | 点击开始后创建战斗 |
| P1 | `POST /api/idle-adventure/players/{playerId}/sessions/{sessionId}/rounds/resolve` | 后端权威回合结算 |
| P1 | `POST /api/idle-adventure/players/{playerId}/sessions/{sessionId}/stages/continue` | 继续下一关 |
| P1 | `POST /api/idle-adventure/players/{playerId}/sessions/{sessionId}/pause` | 暂停战斗 |
| P1 | `POST /api/idle-adventure/players/{playerId}/sessions/{sessionId}/resume` | 恢复战斗 |
| P1 | `POST /api/idle-adventure/players/{playerId}/sessions/{sessionId}/end` | 结束模式并结算发放本次通关奖励 |
| P2 | `GET /api/idle-adventure/config` | 独立获取玩法配置 |
| P2 | `GET /api/idle-adventure/buff-presets` | 独立获取 buff 预设 |
| P2 | `GET /api/idle-adventure/skills` | 独立获取技能配置 |
| P2 | `GET /api/idle-adventure/field-buffs?stage=1` | 独立获取场地 buff 配置 |
| P2 | `GET /api/idle-adventure/attribute-restraints` | 独立获取属性克制关系，克制倍率固定 20% |
| P2 | `GET /api/idle-adventure/stage-rewards` | 独立获取每关奖励配置 |
| P2 | `GET /api/idle-adventure/players/{playerId}/progress` | 获取闯关进度 |
