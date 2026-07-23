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
| 编队槽位越界 | `code=400`，`message=slot_index 必须在 0-5 范围内` |
| 角色不可用 | `code=400`，`message=该角色不可用于当前模式` |
| 战斗会话不存在 | `code=404`，`message=战斗会话不存在` |
| 战斗会话状态错误 | `code=409`，`message=当前战斗状态不允许此操作` |
| 系统异常 | HTTP 500，`code=500`，`message=系统异常，请联系管理员` |

### 1.2 登录鉴权

除登录、注册、健康检查等公共接口外，Idle Adventure 模式接口都需要携带：

```http
Authorization: Bearer <token>
```

`user.id` 就是游戏里的 `player_id`。例如用户 id 为 3，就调用：

```http
GET /api/idle-adventure/players/3/home
Authorization: Bearer <token>
```

后端必须校验路径或请求体中的 `player_id` 是否等于 token 对应用户 ID，不能用 A 用户 token 请求或修改 B 用户数据。

### 1.3 模式固定规则

当前版本 Idle Adventure 模式固定规则如下：

| 规则项 | 当前值 | 说明 |
| --- | --- | --- |
| 最大友方数量 | 6 | 玩家最多上阵 6 名友方角色 |
| 最大敌方数量 | 6 | 单关最多存在 6 名敌方角色 |
| 阵型布局 | 2 列 3 行 | 两列三行，前列 3 个，后列 3 个 |
| 槽位编号 | 0-5 | `0,1,2` 为前排；`3,4,5` 为后排 |
| 行号计算 | `slot_index % 3` | 行号为 0-2 |
| 友方技能槽 | 2 | 每名友方角色最多 2 个技能槽 |
| 敌方技能槽 | 1 | 每名敌方角色最多 1 个技能槽 |
| buff 图标显示 | 6 | 客户端每个角色最多显示 6 个 buff 图标 |
| 战斗开始条件 | 至少 1 名友方角色 | 编队为空时开始按钮不可用 |
| 关卡结束条件 | 当前槽位中的敌人全部死亡 | 弹出结束面板 |
| 结束面板行为 | 继续下一关 / 结束模式 | 当前版本“结束模式”先设计为暂停当前战斗 |

槽位布局：

| slot_index | 列 | 行 | 定位 |
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
2. 按 `effective_speed` 从高到低排序，速度高者先行动。
3. 速度相同情况下，友方优先；仍相同时按 `slot_index` 从小到大。
4. 行动角色优先使用第一个可用技能；无可用技能时使用普通攻击。
5. 目标选择先判断对方前排 `slot_index=0,1,2` 是否仍有存活角色。
6. 如果前排有存活角色，只能攻击前排；前排全部死亡后，才能攻击后排 `slot_index=3,4,5`。
7. 在可攻击列中，优先攻击与自己同一行的敌人。
8. 同一行无敌人时，优先攻击上一行的敌人。
9. 上方也无敌人时，再攻击下方行的敌人。
10. 每个角色行动前再次确认自身仍然存活；如果该角色已经死亡，则跳过本次行动。
11. 每个行动后立即结算伤害、死亡、技能冷却、技能附加 buff。
12. 每回合结束后，双方所有存活角色的 buff 持续回合减少 1，技能冷却减少 1。
13. 任一方全部死亡时，本关结束。

伤害计算：

```text
基础伤害 = max(0, attacker.effective_attack - target.effective_defense)
增益倍率 = 普通buff倍率 + 对应属性buff倍率 + 场地buff倍率 + 属性克制倍率 + 1
普通攻击伤害 = floor(基础伤害 * 增益倍率)
技能伤害 = floor(基础伤害 * 技能倍率 * 增益倍率)
```

说明：倍率字段使用小数表达，`0.25` 表示增加 25%，`-0.2` 表示降低 20%。如果最终伤害需要保底 1 点，后端可在最后一步执行 `max(1, final_damage)`；如果允许完全被防御抵消，则使用上面的 `max(0, ...)` 规则。属性克制倍率不从 buff 表读取，只要攻击者角色属性克制目标角色属性，就固定取 `0.2`。

倍率来源：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| normal_buff_multiplier | number | 普通伤害增益/减益 buff 倍率，直接参与所有伤害计算 |
| attribute_buff_multiplier | number | 属性增强/减益 buff 倍率，只在行动角色 `attribute_type` 与 buff 的 `affected_attribute` 匹配时生效 |
| field_buff_multiplier | number | 场地 buff 倍率，只对符合场地 buff `affected_attribute` 的角色生效 |
| attribute_restraint_multiplier | number | 属性克制倍率，攻击者角色属性克制目标角色属性时固定为 `0.2`，否则为 `0` |
| bonus_multiplier | number | 最终增益倍率，即上述四项相加后再加 1 |

buff 倍率分类：

| buff 类型 | effect_type | category | 生效范围 |
| --- | --- | --- | --- |
| 伤害增益/减益 buff（普通） | NORMAL_DAMAGE | `BENEFIT` / `DETRIMENT` | 对所有属性角色的伤害计算生效 |
| 属性增强/减益 buff | ATTRIBUTE_DAMAGE | `BENEFIT` / `DETRIMENT` | 只对角色 `attribute_type` 与 `affected_attribute` 匹配的角色生效 |
| 场地 buff | FIELD | `BENEFIT` / `DETRIMENT` | 只对 `affected_attribute` 匹配的角色生效，通常来自关卡或战场环境 |
| 属性克制倍率 | 无需 buff | 无需 category | 攻击者角色属性克制目标角色属性时固定增加 20% |

属性计算：

```text
effective_attack = max(0, base_attack + sum(active_buff.attack_modifier))
effective_defense = max(0, base_defense + sum(active_buff.defense_modifier))
effective_speed = max(1, base_speed + sum(active_buff.speed_modifier))
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
| ICE | 冰属性 |
| THUNDER | 雷属性 |

克制关系：水 > 火 > 木 > 土 > 水；物理 > 冰 > 雷 > 物理。

说明：当前没有细分属性时，普通攻击和技能都可以先使用 `PHYSICAL`。属性克制倍率固定为 20%，后端根据攻击者角色 `attribute_type` 与目标角色 `attribute_type` 判断是否命中克制关系；命中时 `attribute_restraint_multiplier=0.2`，未命中时为 `0`。

## 3. 通用数据结构

### 3.1 IdleAdventureModeConfigResponse

```json
{
  "max_party_size": 6,
  "formation_rows": 3,
  "formation_columns": 2,
  "friendly_skill_slot_count": 2,
  "enemy_skill_slot_count": 1,
  "max_buff_icon_slots": 6,
  "starting_friendly_count": 3,
  "base_enemy_count": 2,
  "add_enemy_every_stages": 2,
  "turn_interval": 0.75,
  "attack_animation_duration": 0.28,
  "hit_flash_duration": 0.18,
  "attribute_restraint_multiplier": 0.2,
  "reward_settlement": "ON_EXIT",
  "combat_rules": {
    "initiative": "SPEED_DESC",
    "front_column_first": true,
    "prefer_same_row": true,
    "prefer_upper_row_when_same_row_empty": true
  }
}
```

字段说明：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| max_party_size | number | 单方最大角色数，当前为 6 |
| formation_rows | number | 阵型行数，当前为 3 |
| formation_columns | number | 阵型列数，当前为 2 |
| friendly_skill_slot_count | number | 友方技能槽数量，当前为 2 |
| enemy_skill_slot_count | number | 敌方技能槽数量，当前为 1 |
| max_buff_icon_slots | number | 客户端显示 buff 图标数量 |
| starting_friendly_count | number | 默认友方数量 |
| base_enemy_count | number | 未配置关卡敌人时的第 1 关默认敌人数 |
| add_enemy_every_stages | number | 未配置关卡敌人时，每多少关自动增加 1 名敌人 |
| turn_interval | number | 客户端每回合间隔，单位秒 |
| attack_animation_duration | number | 攻击动画时长，单位秒 |
| hit_flash_duration | number | 受击闪烁时长，单位秒 |
| attribute_restraint_multiplier | number | 属性克制固定倍率，当前为 0.2 |
| reward_settlement | string | 奖励结算时机，当前为退出模式时统一结算 |
| combat_rules | object | 战斗规则说明 |

### 3.2 CombatantDefinition

```json
{
  "definition_id": 101,
  "combatant_key": "hero_1",
  "combatant_name": "Hero 1",
  "side": "FRIENDLY",
  "max_health": 160,
  "attack": 28,
  "defense": 5,
  "speed": 18,
  "attribute_type": "PHYSICAL",
  "skill_slots": [
    {
      "slot_index": 0,
      "skill": {
        "skill_id": 1001,
        "skill_key": "power_strike",
        "skill_name": "Power Strike",
        "cooldown_rounds": 2,
        "skill_multiplier": 1.35,
        "damage_attribute": "PHYSICAL",
        "self_buff_preset_id": null,
        "target_buff_preset_id": null
      }
    },
    {
      "slot_index": 1,
      "skill": {
        "skill_id": 1002,
        "skill_key": "battle_focus",
        "skill_name": "Battle Focus",
        "cooldown_rounds": 3,
        "skill_multiplier": 1,
        "damage_attribute": "PHYSICAL",
        "self_buff_preset_id": 1,
        "target_buff_preset_id": null
      }
    }
  ]
}
```

字段说明：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| definition_id | number | 角色配置 ID；`side=FRIENDLY` 时对应 `Player_npc.definition_id`，`side=ENEMY` 时对应 `idle_adventure_enemy_config.definition_id` |
| combatant_key | string | 角色配置 key |
| combatant_name | string | 显示名称 |
| side | string | `FRIENDLY` 或 `ENEMY` |
| max_health | number | 最大生命 |
| attack | number | 攻击力 |
| defense | number | 防御力 |
| speed | number | 速度 |
| attribute_type | string | 角色自身属性，用于属性克制计算 |
| skill_slots | array | 技能槽配置 |

### 3.3 SkillDefinition

```json
{
  "skill_id": 1001,
  "skill_key": "power_strike",
  "skill_name": "Power Strike",
  "cooldown_rounds": 2,
  "skill_multiplier": 1.35,
  "damage_attribute": "PHYSICAL",
  "self_buff_preset_id": null,
  "target_buff_preset_id": null
}
```

字段说明：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| skill_id | number | 技能 ID |
| skill_key | string | 技能 key |
| skill_name | string | 技能显示名 |
| cooldown_rounds | number | 技能冷却回合数 |
| skill_multiplier | number | 技能倍率，1 表示 100%，1.35 表示 135% |
| damage_attribute | string / null | 技能攻击属性；为空时使用角色自身 `attribute_type` |
| self_buff_preset_id | number / null | 对自己施加的 buff 预设 ID |
| target_buff_preset_id | number / null | 对目标施加的 buff 预设 ID |

### 3.4 BuffDefinition

```json
{
  "buff_preset_id": 1,
  "buff_key": "normal_damage_up",
  "display_name": "Damage Up",
  "category": "BENEFIT",
  "source": "CHARACTER_SKILL",
  "effect_type": "NORMAL_DAMAGE",
  "duration_rounds": 3,
  "attack_modifier": 8,
  "defense_modifier": 0,
  "speed_modifier": 0,
  "normal_damage_multiplier": 0.15,
  "attribute_damage_multiplier": 0,
  "affected_attribute": null
}
```

字段说明：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| buff_preset_id | number | buff 预设 ID |
| buff_key | string | buff key，同一角色同 key 同 source 时刷新持续时间 |
| display_name | string | 显示名 |
| category | string | `BENEFIT` 增益，`DETRIMENT` 减益 |
| source | string | `CHARACTER_SKILL` 或 `OTHER` |
| effect_type | string | `NORMAL_DAMAGE`、`ATTRIBUTE_DAMAGE` 或 `FIELD` |
| duration_rounds | number | 持续回合数，最小为 1 |
| attack_modifier | number | 攻击修正，可为负数 |
| defense_modifier | number | 防御修正，可为负数 |
| speed_modifier | number | 速度修正，可为负数 |
| normal_damage_multiplier | number | 普通 buff 倍率，参与 `普通buff倍率` 计算 |
| attribute_damage_multiplier | number | 属性 buff 倍率，只有角色 `attribute_type` 与 `affected_attribute` 匹配时参与 `对应属性buff倍率` 计算 |
| affected_attribute | string / null | 该 buff 生效的角色属性；普通伤害 buff 可为 null |

### 3.4.1 RewardItemDefinition

```json
{
  "item_key": "gold",
  "display_name": "Gold",
  "quantity": 120
}
```

字段说明：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| item_key | string | 奖励物品 key，应与背包或资源表中的物品 key 对齐 |
| display_name | string | 奖励显示名 |
| quantity | number | 奖励数量 |

### 3.4.2 StageRewardDefinition

```json
{
  "stage": 1,
  "rewards": [
    {
      "item_key": "gold",
      "display_name": "Gold",
      "quantity": 120
    },
    {
      "item_key": "adventure_token",
      "display_name": "Adventure Token",
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
  "slot_index": 0,
  "column": 0,
  "row": 0,
  "front_column": true,
  "definition_id": 101,
  "combatant_name": "Hero 1"
}
```

字段说明：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| slot_index | number | 槽位编号，范围 0-5 |
| column | number | 0 为前排，1 为后排 |
| row | number | 行号，范围 0-2 |
| front_column | boolean | 是否前排 |
| definition_id | number / null | 上阵友方角色配置 ID，对应 `Player_npc.definition_id`；空槽为 null |
| combatant_name | string / null | 上阵角色名称，空槽为 null |

### 3.6 CombatantState

```json
{
  "runtime_id": "F-0",
  "definition_id": 101,
  "combatant_name": "Hero 1",
  "side": "FRIENDLY",
  "slot_index": 0,
  "max_health": 160,
  "current_health": 128,
  "attack": 28,
  "defense": 5,
  "speed": 18,
  "attribute_type": "PHYSICAL",
  "effective_attack": 28,
  "effective_defense": 5,
  "effective_speed": 18,
  "defeated": false,
  "active_buffs": [
    {
      "buff_key": "normal_damage_up",
      "display_name": "Damage Up",
      "category": "BENEFIT",
      "source": "CHARACTER_SKILL",
      "effect_type": "NORMAL_DAMAGE",
      "remaining_rounds": 2,
      "attack_modifier": 0,
      "defense_modifier": 0,
      "speed_modifier": 0,
      "normal_damage_multiplier": 0.15,
      "attribute_damage_multiplier": 0,
      "affected_attribute": null
    }
  ],
  "skill_states": [
    {
      "slot_index": 0,
      "skill_id": 1001,
      "skill_name": "Power Strike",
      "cooldown_remaining": 1,
      "can_use": false
    }
  ]
}
```

说明：`friendlies` / `enemies` 数组在战斗状态接口中固定返回 6 个槽位。空槽返回 `null`；角色生命值归零并完成本次 `DEFEATED` 动作播放后，该槽位后续也返回 `null`，客户端不再显示对应模型、血条和信息。死亡发生的那一次回合由 `BattleAction.defeated_runtime_ids` 告诉客户端播放死亡表现。

### 3.7 BattleAction

```json
{
  "action_order": 1,
  "round": 1,
  "action_type": "SKILL",
  "actor_runtime_id": "F-0",
  "actor_name": "Hero 1",
  "target_runtime_id": "E-0",
  "target_name": "Enemy 1",
  "skill_id": 1001,
  "skill_name": "Power Strike",
  "damage": 36,
  "target_health_before": 95,
  "target_health_after": 59,
  "damage_formula": {
    "base_damage": 27,
    "skill_multiplier": 1.35,
    "normal_buff_multiplier": 0,
    "attribute_buff_multiplier": 0,
    "field_buff_multiplier": 0,
    "attribute_restraint_multiplier": 0,
    "bonus_multiplier": 1,
    "attacker_attribute": "FIRE",
    "defender_attribute": "WOOD"
  },
  "applied_buffs": [],
  "defeated_runtime_ids": [],
  "animation": {
    "attack_direction": 1,
    "attack_duration": 0.28,
    "hit_flash_duration": 0.18
  },
  "log": "Hero 1 uses Power Strike on Enemy 1 for 36."
}
```

字段说明：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| action_order | number | 本回合动作顺序 |
| round | number | 当前回合 |
| action_type | string | 动作类型 |
| actor_runtime_id | string | 行动者运行时 ID |
| target_runtime_id | string / null | 目标运行时 ID |
| skill_id | number / null | 技能 ID，普通攻击为 null |
| damage | number | 造成伤害 |
| damage_formula | object | 本次伤害计算明细，包含基础伤害、技能倍率和四类增益倍率 |
| applied_buffs | array | 本动作附加的 buff |
| defeated_runtime_ids | array | 本动作导致死亡的角色 |
| animation | object | 客户端播放动画所需参数 |
| log | string | 战斗日志 |

## 4. 模式首页接口

### 4.1 获取 Idle Adventure 首页数据

```http
GET /api/idle-adventure/players/{player_id}/home
Authorization: Bearer <token>
```

是否需要 token：是

前端传参：无请求体

路径参数：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| player_id | number | 是 | 玩家 ID，必须等于 token 对应用户 ID |

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
  "player_id": 1,
  "highest_stage": 5,
  "next_stage": 6,
  "has_running_session": false,
  "running_session_key": null,
  "mode_config": {
    "max_party_size": 6,
    "formation_rows": 3,
    "formation_columns": 2,
    "friendly_skill_slot_count": 2,
    "enemy_skill_slot_count": 1,
    "max_buff_icon_slots": 6
  },
  "formation": {
    "deployed_count": 3,
    "slots": [
      {
        "slot_index": 0,
        "column": 0,
        "row": 0,
        "front_column": true,
        "definition_id": 101,
        "combatant_name": "Hero 1"
      },
      {
        "slot_index": 1,
        "column": 0,
        "row": 1,
        "front_column": true,
        "definition_id": 102,
        "combatant_name": "Hero 2"
      },
      {
        "slot_index": 2,
        "column": 0,
        "row": 2,
        "front_column": true,
        "definition_id": 103,
        "combatant_name": "Hero 3"
      },
      {
        "slot_index": 3,
        "column": 1,
        "row": 0,
        "front_column": false,
        "definition_id": null,
        "combatant_name": null
      },
      {
        "slot_index": 4,
        "column": 1,
        "row": 1,
        "front_column": false,
        "definition_id": null,
        "combatant_name": null
      },
      {
        "slot_index": 5,
        "column": 1,
        "row": 2,
        "front_column": false,
        "definition_id": null,
        "combatant_name": null
      }
    ]
  },
  "available_npc_list": [
    {
      "definition_id": 101,
      "combatant_key": "hero_1",
      "combatant_name": "Hero 1",
      "max_health": 160,
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
2. 按 `buff_preset_id` 升序返回。

返回 data：

```json
[
  {
    "buff_preset_id": 1,
    "buff_key": "normal_damage_up",
    "display_name": "Damage Up",
    "category": "BENEFIT",
    "source": "CHARACTER_SKILL",
    "effect_type": "NORMAL_DAMAGE",
    "duration_rounds": 3,
    "attack_modifier": 0,
    "defense_modifier": 0,
    "speed_modifier": 0,
    "normal_damage_multiplier": 0.15,
    "attribute_damage_multiplier": 0,
    "affected_attribute": null
  },
  {
    "buff_preset_id": 2,
    "buff_key": "fire_damage_down",
    "display_name": "Fire Damage Down",
    "category": "DETRIMENT",
    "source": "CHARACTER_SKILL",
    "effect_type": "ATTRIBUTE_DAMAGE",
    "duration_rounds": 3,
    "attack_modifier": 0,
    "defense_modifier": 0,
    "speed_modifier": 0,
    "normal_damage_multiplier": 0,
    "attribute_damage_multiplier": -0.15,
    "affected_attribute": "FIRE"
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
    "skill_id": 1001,
    "skill_key": "power_strike",
    "skill_name": "Power Strike",
    "cooldown_rounds": 2,
    "skill_multiplier": 1.35,
    "damage_attribute": "PHYSICAL",
    "self_buff_preset_id": null,
    "target_buff_preset_id": null
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
3. 场地 buff 只对 `affected_attribute` 匹配的角色生效。

返回 data：

```json
[
  {
    "field_buff_id": 1,
    "stage": 1,
    "field_buff_key": "fire_field_up",
    "display_name": "Fire Field Up",
    "category": "BENEFIT",
    "effect_type": "FIELD",
    "target_side": null,
    "affected_attribute": "FIRE",
    "field_damage_multiplier": 0.15
  }
]
```

字段说明：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| field_buff_id | number | 场地 buff ID |
| stage | number / null | 生效关卡；null 表示全局生效 |
| field_buff_key | string | 场地 buff key |
| display_name | string | 显示名 |
| category | string | `BENEFIT` 增益，`DETRIMENT` 减益 |
| effect_type | string | 固定为 `FIELD` |
| target_side | string / null | 限定阵营；null 表示双方都可生效 |
| affected_attribute | string | 生效角色属性 |
| field_damage_multiplier | number | 场地伤害倍率，可正可负 |

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
3. 命中克制关系时，`attribute_restraint_multiplier` 固定为 `0.2`。

返回 data：

```json
{
  "restraint_multiplier": 0.2,
  "relations": [
    {
      "attacker_attribute": "FIRE",
      "defender_attribute": "WOOD"
    },
    {
      "attacker_attribute": "WATER",
      "defender_attribute": "FIRE"
    },
    {
      "attacker_attribute": "WOOD",
      "defender_attribute": "EARTH"
    },
    {
      "attacker_attribute": "EARTH",
      "defender_attribute": "WATER"
    },
    {
      "attacker_attribute": "PHYSICAL",
      "defender_attribute": "ICE"
    },
    {
      "attacker_attribute": "ICE",
      "defender_attribute": "THUNDER"
    },
    {
      "attacker_attribute": "THUNDER",
      "defender_attribute": "PHYSICAL"
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
        "item_key": "gold",
        "display_name": "Gold",
        "quantity": 120
      },
      {
        "item_key": "adventure_token",
        "display_name": "Adventure Token",
        "quantity": 1
      }
    ]
  },
  {
    "stage": 2,
    "rewards": [
      {
        "item_key": "gold",
        "display_name": "Gold",
        "quantity": 180
      },
      {
        "item_key": "wood",
        "display_name": "Wood",
        "quantity": 60
      }
    ]
  }
]
```

## 6. 友方角色与敌人接口

### 6.1 获取玩家可用于编队的 NPC 列表

```http
GET /api/idle-adventure/players/{player_id}/npcs
Authorization: Bearer <token>
```

是否需要 token：是

路径参数：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| player_id | number | 是 | 玩家 ID |

后端处理：

1. 校验 token 与 `player_id`。
2. 查询玩家已拥有或系统默认可用的 Idle Adventure 友方角色，角色基础配置来源为 `Player_npc`。
3. 查询每个角色的基础属性与技能槽。
4. 标记该角色当前是否已在编队中。

返回 data：

```json
[
  {
    "definition_id": 101,
    "combatant_key": "hero_1",
    "combatant_name": "Hero 1",
    "side": "FRIENDLY",
    "max_health": 160,
    "attack": 28,
    "defense": 5,
    "speed": 18,
    "attribute_type": "PHYSICAL",
    "assigned": true,
    "skill_slots": [
      {
        "slot_index": 0,
        "skill": {
          "skill_id": 1001,
          "skill_key": "power_strike",
          "skill_name": "Power Strike",
          "cooldown_rounds": 2,
          "skill_multiplier": 1.35,
          "damage_attribute": "PHYSICAL",
          "self_buff_preset_id": null,
          "target_buff_preset_id": null
        }
      },
      {
        "slot_index": 1,
        "skill": {
          "skill_id": 1002,
          "skill_key": "battle_focus",
          "skill_name": "Battle Focus",
          "cooldown_rounds": 3,
          "skill_multiplier": 1,
          "damage_attribute": "PHYSICAL",
          "self_buff_preset_id": 1,
          "target_buff_preset_id": null
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

1. 查询 `idle_adventure_stage_enemy_config` 中第 `stage` 关启用的敌方槽位配置。
2. 通过 `definition_id` 关联 `idle_adventure_enemy_config`，读取敌人基础属性和技能槽。
3. `enemy_count` 等于当前关卡启用的敌方槽位数量。
4. 可根据 `idle_adventure_stage_enemy_config.level` 和关卡成长规则计算敌人最终属性。
5. 返回固定 6 个槽位，未配置或未启用的槽位为 `null`，方便客户端按两列三行布局渲染。

敌人数计算：

```text
enemy_count = count(enabled idle_adventure_stage_enemy_config where stage = path.stage)
```

敌人成长（可选，具体数值可由后端配置）：

```text
health = enemy_config.max_health + (level - 1) * enemy_health_per_level
attack = enemy_config.attack + (level - 1) * enemy_attack_per_level
speed = enemy_config.speed + (level - 1) * enemy_speed_per_level
```

返回 data：

```json
{
  "stage": 1,
  "enemy_count": 2,
  "slots": [
    {
      "slot_index": 0,
      "enemy": {
        "definition_id": 201,
        "combatant_key": "enemy_1",
        "combatant_name": "Enemy 1",
        "side": "ENEMY",
        "max_health": 95,
        "attack": 15,
        "defense": 1,
        "speed": 15,
        "attribute_type": "PHYSICAL",
        "skill_slots": [
          {
            "slot_index": 0,
            "skill": {
              "skill_id": 2001,
              "skill_key": "claw",
              "skill_name": "Claw",
              "cooldown_rounds": 2,
              "skill_multiplier": 1.2,
              "damage_attribute": "PHYSICAL",
              "self_buff_preset_id": null,
              "target_buff_preset_id": null
            }
          }
        ]
      }
    },
    {
      "slot_index": 1,
      "enemy": {
        "definition_id": 202,
        "combatant_key": "enemy_2",
        "combatant_name": "Enemy 2",
        "side": "ENEMY",
        "max_health": 107,
        "attack": 17,
        "defense": 1,
        "speed": 14,
        "attribute_type": "PHYSICAL",
        "skill_slots": []
      }
    },
    {
      "slot_index": 2,
      "enemy": null
    },
    {
      "slot_index": 3,
      "enemy": null
    },
    {
      "slot_index": 4,
      "enemy": null
    },
    {
      "slot_index": 5,
      "enemy": null
    }
  ]
}
```

## 7. 编队接口

### 7.1 获取玩家当前编队

```http
GET /api/idle-adventure/players/{player_id}/formation
Authorization: Bearer <token>
```

是否需要 token：是

后端处理：

1. 校验 token 与 `player_id`。
2. 查询玩家已保存的 Idle Adventure 编队。
3. 如果玩家没有编队记录，生成默认编队：按可用 NPC 顺序填入前几个槽位。
4. 返回固定 6 个槽位。

返回 data：

```json
{
  "player_id": 1,
  "deployed_count": 3,
  "slots": [
    {
      "slot_index": 0,
      "column": 0,
      "row": 0,
      "front_column": true,
      "definition_id": 101,
      "combatant_name": "Hero 1"
    },
    {
      "slot_index": 1,
      "column": 0,
      "row": 1,
      "front_column": true,
      "definition_id": 102,
      "combatant_name": "Hero 2"
    },
    {
      "slot_index": 2,
      "column": 0,
      "row": 2,
      "front_column": true,
      "definition_id": 103,
      "combatant_name": "Hero 3"
    },
    {
      "slot_index": 3,
      "column": 1,
      "row": 0,
      "front_column": false,
      "definition_id": null,
      "combatant_name": null
    },
    {
      "slot_index": 4,
      "column": 1,
      "row": 1,
      "front_column": false,
      "definition_id": null,
      "combatant_name": null
    },
    {
      "slot_index": 5,
      "column": 1,
      "row": 2,
      "front_column": false,
      "definition_id": null,
      "combatant_name": null
    }
  ]
}
```

### 7.2 保存玩家编队

```http
PUT /api/idle-adventure/players/{player_id}/formation
Authorization: Bearer <token>
Content-Type: application/json
```

是否需要 token：是

前端传参：

```json
{
  "slots": [
    {
      "slot_index": 0,
      "definition_id": 101
    },
    {
      "slot_index": 1,
      "definition_id": 102
    },
    {
      "slot_index": 2,
      "definition_id": 103
    },
    {
      "slot_index": 3,
      "definition_id": null
    },
    {
      "slot_index": 4,
      "definition_id": null
    },
    {
      "slot_index": 5,
      "definition_id": null
    }
  ]
}
```

参数说明：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| slots | array | 是 | 固定 6 个槽位，也允许只传发生变化的槽位 |
| slots[].slot_index | number | 是 | 槽位编号，0-5 |
| slots[].definition_id | number / null | 是 | 友方角色配置 ID，对应 `Player_npc.definition_id`；null 表示空槽 |

后端处理：

1. 校验 token 与 `player_id`。
2. 校验 `slot_index` 范围必须为 0-5。
3. 校验 `definition_id` 必须属于该玩家可用 NPC。
4. 校验同一名角色不能重复上阵。
5. 校验保存后至少存在 1 名上阵角色。
6. 保存玩家编队。
7. 返回保存后的完整 6 槽位编队。

返回 data：同 `7.1 获取玩家当前编队`

失败情况：

| 场景 | message |
| --- | --- |
| 编队为空 | 至少需要上阵 1 名友方角色 |
| 槽位越界 | slot_index 必须在 0-5 范围内 |
| 角色重复上阵 | 同一角色不能重复上阵 |
| 角色不可用 | 该角色不可用于当前模式 |

### 7.3 自动填充默认编队

```http
POST /api/idle-adventure/players/{player_id}/formation/auto-fill
Authorization: Bearer <token>
```

是否需要 token：是

前端传参：无请求体

后端处理：

1. 查询玩家可用 NPC。
2. 根据默认友方数量 `starting_friendly_count`，从可用 NPC 中按配置顺序填充。
3. 优先填充前排 `slot_index=0,1,2`，数量超过 3 时再填充后排。
4. 保存并返回完整编队。

返回 data：同 `7.1 获取玩家当前编队`

### 7.4 清空编队

```http
POST /api/idle-adventure/players/{player_id}/formation/clear
Authorization: Bearer <token>
```

是否需要 token：是

说明：当前客户端“开始战斗”要求至少 1 名角色，因此清空编队后开始按钮应置为不可用。后端允许临时保存空编队，或也可以按业务要求拒绝清空。推荐允许清空，保存时返回 `deployed_count=0`。

后端处理：

1. 校验 token 与 `player_id`。
2. 删除或置空玩家 6 个编队槽位。
3. 返回空编队。

返回 data：

```json
{
  "player_id": 1,
  "deployed_count": 0,
  "slots": [
    {
      "slot_index": 0,
      "column": 0,
      "row": 0,
      "front_column": true,
      "definition_id": null,
      "combatant_name": null
    },
    {
      "slot_index": 1,
      "column": 0,
      "row": 1,
      "front_column": true,
      "definition_id": null,
      "combatant_name": null
    },
    {
      "slot_index": 2,
      "column": 0,
      "row": 2,
      "front_column": true,
      "definition_id": null,
      "combatant_name": null
    },
    {
      "slot_index": 3,
      "column": 1,
      "row": 0,
      "front_column": false,
      "definition_id": null,
      "combatant_name": null
    },
    {
      "slot_index": 4,
      "column": 1,
      "row": 1,
      "front_column": false,
      "definition_id": null,
      "combatant_name": null
    },
    {
      "slot_index": 5,
      "column": 1,
      "row": 2,
      "front_column": false,
      "definition_id": null,
      "combatant_name": null
    }
  ]
}
```

## 8. 战斗会话接口

### 8.1 创建并开始战斗

```http
POST /api/idle-adventure/players/{player_id}/sessions/start
Authorization: Bearer <token>
Content-Type: application/json
```

是否需要 token：是

前端传参：

```json
{
  "use_saved_formation": true,
  "formation_slots": null
}
```

也可以传入临时编队：

```json
{
  "stage": 6,
  "use_saved_formation": false,
  "formation_slots": [
    {
      "slot_index": 0,
      "definition_id": 101
    },
    {
      "slot_index": 3,
      "definition_id": 102
    }
  ]
}
```

参数说明：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| stage | number | 否 | 开始关卡；不传时默认 `highest_stage + 1`，即玩家下一关未通关关卡 |
| use_saved_formation | boolean | 否 | 是否使用后端保存的编队，默认 true |
| formation_slots | array / null | 否 | 临时编队，仅 `use_saved_formation=false` 时使用 |

后端处理：

1. 校验 token 与 `player_id`。
2. 查询玩家最高通关关卡 `highest_stage`。
3. 如果请求未传 `stage`，从 `highest_stage + 1` 开始；如果传入 `stage`，需校验该关卡允许挑战。
4. 读取保存编队或请求中的临时编队。
5. 校验至少 1 名友方角色上阵。
6. 生成友方运行时状态，空槽保留为 null。
7. 根据关卡生成敌方运行时状态，空槽保留为 null。
8. 创建战斗会话，状态设为 `RUNNING`。
9. 返回初始战斗状态。

返回 data：

```json
{
  "session_key": "ia-20260720-000001",
  "player_id": 1,
  "stage": 6,
  "round": 1,
  "status": "RUNNING",
  "paused": false,
  "stage_choice_pending": false,
  "pending_next_stage": null,
  "friendlies": [
    {
      "runtime_id": "F-0",
      "definition_id": 101,
      "combatant_name": "Hero 1",
      "side": "FRIENDLY",
      "slot_index": 0,
      "max_health": 160,
      "current_health": 160,
      "attack": 28,
      "defense": 5,
      "speed": 18,
      "attribute_type": "PHYSICAL",
      "effective_attack": 28,
      "effective_defense": 5,
      "effective_speed": 18,
      "defeated": false,
      "active_buffs": [],
      "skill_states": [
        {
          "slot_index": 0,
          "skill_id": 1001,
          "skill_name": "Power Strike",
          "cooldown_remaining": 0,
          "can_use": true
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
      "runtime_id": "E-0",
      "definition_id": 201,
      "combatant_name": "Enemy 1",
      "side": "ENEMY",
      "slot_index": 0,
      "max_health": 95,
      "current_health": 95,
      "attack": 15,
      "defense": 1,
      "speed": 15,
      "attribute_type": "PHYSICAL",
      "effective_attack": 15,
      "effective_defense": 1,
      "effective_speed": 15,
      "defeated": false,
      "active_buffs": [],
      "skill_states": []
    },
    null,
    null,
    null,
    null,
    null
  ],
  "logs": [
    "Stage 6 begins. 1 friendly unit(s) vs 1 enemy unit(s)."
  ]
}
```

失败情况：

| 场景 | message |
| --- | --- |
| 编队为空 | 至少需要上阵 1 名友方角色 |
| 指定关卡非法 | stage 必须大于等于 1，且不能跳过未通关关卡 |
| 已存在运行中的会话 | 已存在进行中的战斗会话 |

### 8.2 获取战斗会话状态

```http
GET /api/idle-adventure/players/{player_id}/sessions/{session_key}
Authorization: Bearer <token>
```

是否需要 token：是

后端处理：

1. 校验 token 与 `player_id`。
2. 查询战斗会话。
3. 返回当前关卡、回合、暂停状态、双方角色状态、日志和待选择的下一关。

返回 data：同 `8.1 创建并开始战斗`

### 8.3 暂停战斗

```http
POST /api/idle-adventure/players/{player_id}/sessions/{session_key}/pause
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
  "session_key": "ia-20260720-000001",
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
POST /api/idle-adventure/players/{player_id}/sessions/{session_key}/resume
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
  "session_key": "ia-20260720-000001",
  "status": "RUNNING",
  "paused": false,
  "message": "Battle resumed."
}
```

### 8.5 结束模式

```http
POST /api/idle-adventure/players/{player_id}/sessions/{session_key}/end
Authorization: Bearer <token>
```

是否需要 token：是

说明：玩家选择结束/退出模式时，后端应结算本次模式中所有已通关关卡的待发放奖励，并将奖励发放到玩家背包或资源账户。未通关的当前关卡不产生奖励。

奖励计算规则：收取待结算奖励池中的所有奖励（来自10.1）

后端处理：

1. 校验会话属于该玩家。
2. 查询本次会话中已通关但尚未结算的关卡。
3. 若当前会话正停留在 `STAGE_COMPLETE`，且该关奖励尚未写入待结算奖励池，则先将该关奖励加入待结算奖励池。
4. 将待结算奖励池中的奖励写入玩家背包/资源账户。
5. 标记这些关卡奖励已结算，避免重复领取。
6. 将会话状态更新为 `ENDED`。
7. 返回本次发放奖励明细和最新背包快照。

返回 data：

```json
{
  "session_key": "ia-20260720-000001",
  "status": "ENDED",
  "settled_stages": [1, 2],
  "granted_rewards": [
    {
      "item_key": "gold",
      "display_name": "Gold",
      "quantity": 300
    },
    {
      "item_key": "adventure_token",
      "display_name": "Adventure Token",
      "quantity": 2
    }
  ],
  "items": [
    {
      "item_id": 1,
      "item_key": "gold",
      "name": "金币",
      "item_type": "CURRENCY",
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
POST /api/idle-adventure/players/{player_id}/sessions/{session_key}/rounds/resolve
Authorization: Bearer <token>
Content-Type: application/json
```

是否需要 token：是

前端传参：

```json
{
  "client_round": 1
}
```

参数说明：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| client_round | number | 是 | 客户端认为正在结算的回合，用于防止重复请求 |

后端处理：

1. 校验 token 与 `player_id`。
2. 校验会话属于该玩家，且状态为 `RUNNING`。
3. 校验 `client_round` 等于服务端当前回合。
4. 构建行动顺序：所有存活角色按 `effective_speed` 降序。
5. 按行动顺序逐个结算攻击、技能、buff、死亡；行动前如果角色已死亡，则跳过该角色。
6. 如果当前关敌人全部死亡，状态设为 `STAGE_COMPLETE`，设置 `pending_next_stage=stage+1`，并更新玩家最高通关关卡。
7. 如果友方全部死亡，状态设为 `FAILED`。
8. 如果双方都未全部死亡，回合数加 1。
9. 返回动作列表和最新战斗状态。

返回 data：

```json
{
  "session_key": "ia-20260720-000001",
  "stage": 1,
  "round_before": 1,
  "round_after": 2,
  "status": "RUNNING",
  "initiative_order": [
    {
      "runtime_id": "F-0",
      "combatant_name": "Hero 1",
      "side": "FRIENDLY",
      "slot_index": 0,
      "effective_speed": 18
    },
    {
      "runtime_id": "E-0",
      "combatant_name": "Enemy 1",
      "side": "ENEMY",
      "slot_index": 0,
      "effective_speed": 15
    }
  ],
  "actions": [
    {
      "action_order": 1,
      "round": 1,
      "action_type": "SKILL",
      "actor_runtime_id": "F-0",
      "actor_name": "Hero 1",
      "target_runtime_id": "E-0",
      "target_name": "Enemy 1",
      "skill_id": 1001,
      "skill_name": "Power Strike",
      "damage": 36,
      "target_health_before": 95,
      "target_health_after": 59,
      "damage_formula": {
        "base_damage": 27,
        "skill_multiplier": 1.35,
        "normal_buff_multiplier": 0,
        "attribute_buff_multiplier": 0,
        "field_buff_multiplier": 0,
        "attribute_restraint_multiplier": 0,
        "bonus_multiplier": 1,
        "attacker_attribute": "FIRE",
        "defender_attribute": "WOOD"
      },
      "applied_buffs": [],
      "defeated_runtime_ids": [],
      "animation": {
        "attack_direction": 1,
        "attack_duration": 0.28,
        "hit_flash_duration": 0.18
      },
      "log": "Hero 1 uses Power Strike on Enemy 1 for 36."
    },
    {
      "action_order": 2,
      "round": 1,
      "action_type": "BASIC_ATTACK",
      "actor_runtime_id": "E-0",
      "actor_name": "Enemy 1",
      "target_runtime_id": "F-0",
      "target_name": "Hero 1",
      "skill_id": null,
      "skill_name": null,
      "damage": 10,
      "target_health_before": 160,
      "target_health_after": 150,
      "damage_formula": {
        "base_damage": 10,
        "skill_multiplier": 1,
        "normal_buff_multiplier": 0,
        "attribute_buff_multiplier": 0,
        "field_buff_multiplier": 0,
        "attribute_restraint_multiplier": 0,
        "bonus_multiplier": 1,
        "attacker_attribute": "WOOD",
        "defender_attribute": "FIRE"
      },
      "applied_buffs": [],
      "defeated_runtime_ids": [],
      "animation": {
        "attack_direction": -1,
        "attack_duration": 0.28,
        "hit_flash_duration": 0.18
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

说明：`friendlies` 和 `enemies` 应返回完整最新状态，结构同固定 6 槽位的 `CombatantState[]`，空槽与已死亡槽位返回 `null`。示例中省略是为了减少篇幅，实际接口不得省略。

失败情况：

| 场景 | message |
| --- | --- |
| 会话不存在 | 战斗会话不存在 |
| 会话未运行 | 当前战斗状态不允许此操作 |
| 回合号不一致 | 客户端回合号与服务端不一致，请刷新战斗状态 |

### 9.2 手动施加 buff

```http
POST /api/idle-adventure/players/{player_id}/sessions/{session_key}/buffs/apply
Authorization: Bearer <token>
Content-Type: application/json
```

是否需要 token：是

说明：此接口用于后续道具、事件、关卡机制等“其他方式增加的 buff”。当前 Unity 脚本中已区分 `CHARACTER_SKILL` 和 `OTHER` 来源；技能自带 buff 不需要调用此接口，由回合结算自动处理。

前端传参：

```json
{
  "target_side": "FRIENDLY",
  "slot_index": 0,
  "buff_preset_id": 3,
  "source": "OTHER"
}
```

参数说明：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| target_side | string | 是 | `FRIENDLY` 或 `ENEMY` |
| slot_index | number | 是 | 目标槽位，0-5 |
| buff_preset_id | number | 是 | buff 预设 ID |
| source | string | 否 | 推荐固定为 `OTHER` |

后端处理：

1. 校验战斗会话属于该玩家。
2. 校验目标槽位存在存活角色。
3. 查询 buff 预设。
4. 如果同一角色已有相同 `buff_key + source`，刷新剩余回合为两者较大值。
5. 否则追加新 buff。
6. 返回目标角色最新状态。

返回 data：

```json
{
  "target": {
    "runtime_id": "F-0",
    "combatant_name": "Hero 1",
    "side": "FRIENDLY",
    "slot_index": 0,
    "active_buffs": [
      {
        "buff_key": "normal_damage_up",
        "display_name": "Damage Up",
        "category": "BENEFIT",
        "source": "OTHER",
        "effect_type": "NORMAL_DAMAGE",
        "remaining_rounds": 3,
        "attack_modifier": 0,
        "defense_modifier": 0,
        "speed_modifier": 0,
        "normal_damage_multiplier": 0.15,
        "attribute_damage_multiplier": 0,
        "affected_attribute": null
      }
    ]
  },
  "log": "Hero 1 gained Damage Up from Other."
}
```

## 10. 关卡结算接口

### 10.1 继续下一关

```http
POST /api/idle-adventure/players/{player_id}/sessions/{session_key}/stages/continue
Authorization: Bearer <token>
```

是否需要 token：是

调用时机：当前关敌方全部死亡，服务端会话状态为 `STAGE_COMPLETE`，客户端显示结束面板后，玩家点击“继续下一关”。

后端处理：

1. 校验会话属于该玩家。
2. 校验会话状态为 `STAGE_COMPLETE`。
3. 将 `stage` 更新为 `pending_next_stage`。
4. 友方角色恢复满血。
5. 根据新关卡生成敌方阵容。
6. 将刚通关关卡的奖励加入本次会话待结算池，但不立即发放。

奖励计算规则：在待结算奖励池中加入当前通过层的奖励

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
POST /api/idle-adventure/players/{player_id}/sessions/{session_key}/stages/retry
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
GET /api/idle-adventure/players/{player_id}/progress
Authorization: Bearer <token>
```

是否需要 token：是

后端处理：

1. 校验 token 与 `player_id`。
2. 查询玩家最高通关关卡、最近会话、累计胜利次数、累计失败次数。

返回 data：

```json
{
  "player_id": 1,
  "highest_stage": 5,
  "last_stage": 3,
  "running_session_key": null,
  "total_wins": 12,
  "total_failures": 2,
  "updated_at": "2026-07-20T12:30:00+08:00"
}
```

### 11.2 保存关卡胜利进度

```http
POST /api/idle-adventure/players/{player_id}/progress/stage-clear
Authorization: Bearer <token>
Content-Type: application/json
```

是否需要 token：是

前端传参：

```json
{
  "session_key": "ia-20260720-000001",
  "cleared_stage": 5
}
```

说明：如果后端已经在 `rounds/resolve` 中权威判断胜利，则此接口可以不开放给客户端，避免客户端伪造通关。推荐由后端在关卡胜利时自动更新进度。

后端处理：

1. 校验会话属于该玩家。
2. 校验该会话确实已通关 `cleared_stage`。
3. 更新玩家最高通关关卡。
4. 返回最新进度。

返回 data：同 `11.1 获取玩家闯关进度`

### 11.3 挂机奖励接口

说明：挂机奖励由服务端按时间结算，客户端仅负责展示状态和发起领取。
奖励计算规则：由初始奖励数*层数*每层提升数

### 11.3.1 获取挂机奖励状态

```http
GET /api/idle-adventure/players/{player_id}/afk-reward
Authorization: Bearer <token>
```

是否需要 token：是

前端传参：无请求体

后端处理：

1. 校验 token 与 `player_id`。
2. 读取玩家挂机奖励的服务端结算状态。
3. 奖励计算规则：由初始奖励数*层数*每层提升数
4. 返回当前挂机奖励状态快照。

返回 data：

```json
{
  "player_id": 1,
  "item_key": "gold",
  "display_name": "Gold",
  "base_quantity_per_cycle": 10,
  "quantity_per_highest_stage": 2,
  "quantity_per_cycle": 60,
  "production_interval_seconds": 30,
  "max_stored_quantity": 1000,
  "stored_quantity": 600,
  "highest_stage": 3,
  "server_unix_seconds": 1784536691,
  "last_settled_server_unix_seconds": 1784536391
}
```

### 11.3.2 领取挂机奖励

```http
POST /api/idle-adventure/players/{player_id}/afk-reward/collect
Authorization: Bearer <token>
```

是否需要 token：是

前端传参：无请求体

后端处理：

1. 校验 token 与 `player_id`。
2. 按服务端时间计算当前可领取挂机奖励。
3. 奖励计算规则：奖励数=基础奖励数*层数*每层提升数
4. 将奖励写入玩家背包或资源账户。
5. 刷新挂机奖励状态并返回最新快照。

返回 data：

```json
{
  "player_id": 1,
  "collected_item": {
    "item_id": 1,
    "item_key": "gold",
    "display_name": "金币",
    "quantity": 600
  },
  "items": [
    {
      "item_id": 1,
      "item_key": "gold",
      "display_name": "金币",
      "item_type": "CURRENCY",
      "icon": "gold",
      "quantity": 10600
    }
  ],
  "state": {
    "player_id": 1,
    "item_key": "gold",
    "display_name": "Gold",
    "base_quantity_per_cycle": 10,
    "quantity_per_highest_stage": 2,
    "quantity_per_cycle": 60,
    "production_interval_seconds": 30,
    "max_stored_quantity": 1000,
    "stored_quantity": 0,
    "highest_stage": 3,
    "server_unix_seconds": 1784536691,
    "last_settled_server_unix_seconds": 1784536691
  }
}
```

## 12. 数据库表

### 12.0 接口字段与数据库字段对应规则

说明：

1. 接口请求参数、路径占位符和返回字段统一使用 snake_case，例如 `definition_id`、`slot_index`、`player_id`。
2. 数据库字段也统一使用 snake_case，例如 `definition_id`、`slot_index`、`player_id`。
3. 直接暴露给接口的配置字段名与数据库字段名保持一致，例如 `definition_id`、`skill_id`、`buff_preset_id`、`combatant_key`、`combatant_name`。
4. 接口中的 `definition_id` 根据阵营对应不同配置表：友方对应 `Player_npc.definition_id`，敌方对应 `idle_adventure_enemy_config.definition_id`。
5. 接口中的 `skill_id` 对应 `idle_adventure_skill_config.skill_id`。
6. 接口中的 `buff_preset_id` 对应 `idle_adventure_buff_preset.buff_preset_id`。
7. 接口中的 `field_buff_id` 对应 `idle_adventure_field_buff_config.field_buff_id`。
8. 接口中的 `session_key` 对应 `idle_adventure_session.session_key`；数据库内部关联字段可使用 `session_id` 指向 `idle_adventure_session.id`，但不直接作为接口字段返回。
9. 接口中的 `runtime_id` 对应 `idle_adventure_session_combatant.runtime_id`；数据库内部关联字段可使用 `session_combatant_id` 指向 `idle_adventure_session_combatant.id`，但不直接作为接口字段返回。
10. `effective_attack`、`effective_defense`、`effective_speed`、`can_use`、`quantity_per_cycle`、`server_unix_seconds` 等为后端计算字段，不直接作为独立数据库字段存储。

### 12.1 Player_npc

友方角色配置表。

> 友方角色配置表位置标注：本节 `12.1 Player_npc`。你后续手动调整友方角色属性、技能归属、可用状态时，优先改这张表及 `player_npc_skill`。

说明：

1. `Player_npc` 只存友方角色 / 玩家可上阵 NPC 的基础配置。
2. 接口返回中的友方 `definition_id` 对应 `Player_npc.definition_id`。
3. 友方阵营固定为 `FRIENDLY`，不再与敌人共用配置表，也不再通过 `side` 区分。

```sql
CREATE TABLE Player_npc (
  definition_id BIGINT PRIMARY KEY AUTO_INCREMENT,
  combatant_key VARCHAR(64) NOT NULL UNIQUE,
  combatant_name VARCHAR(64) NOT NULL,
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

字段说明：

| 字段 | 对应接口字段 | 说明 |
| --- | --- | --- |
| definition_id | definition_id | 友方角色配置 ID |
| combatant_key | combatant_key | 友方角色配置 key |
| combatant_name | combatant_name | 友方角色显示名称 |
| max_health | max_health | 最大生命值 |
| attack | attack | 攻击力 |
| defense | defense | 防御力 |
| speed | speed | 速度，参与行动顺序计算 |
| attribute_type | attribute_type | 角色属性，参与属性 buff 和属性克制计算 |
| enabled | 无 | 是否启用该角色配置 |
| sort_order | 无 | 默认排序，用于 NPC 列表和自动编队 |
| created_at | 无 | 创建时间 |
| updated_at | 无 | 更新时间 |

### 12.1.1 idle_adventure_enemy_config

敌方角色配置表。

说明：

1. `idle_adventure_enemy_config` 只存敌方角色基础配置。
2. 接口返回中的敌方 `definition_id` 对应 `idle_adventure_enemy_config.definition_id`。
3. 敌方阵营固定为 `ENEMY`。

```sql
CREATE TABLE idle_adventure_enemy_config (
  definition_id BIGINT PRIMARY KEY AUTO_INCREMENT,
  combatant_key VARCHAR(64) NOT NULL UNIQUE,
  combatant_name VARCHAR(64) NOT NULL,
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

字段说明：

| 字段 | 对应接口字段 | 说明 |
| --- | --- | --- |
| definition_id | definition_id | 敌方角色配置 ID |
| combatant_key | combatant_key | 敌方角色配置 key |
| combatant_name | combatant_name | 敌方角色显示名称 |
| max_health | max_health | 最大生命值 |
| attack | attack | 攻击力 |
| defense | defense | 防御力 |
| speed | speed | 速度，参与行动顺序计算 |
| attribute_type | attribute_type | 敌方角色属性 |
| enabled | 无 | 是否启用该敌人配置 |
| sort_order | 无 | 默认排序 |
| created_at | 无 | 创建时间 |
| updated_at | 无 | 更新时间 |

### 12.1.2 idle_adventure_stage_enemy_config

关卡敌人配置表。

说明：

1. 用于支持 `GET /api/idle-adventure/stages/{stage}/enemies`。
2. 每个关卡最多配置 6 个敌方槽位。
3. `definition_id` 对应 `idle_adventure_enemy_config.definition_id`。
4. 如果某个槽位未配置敌人，接口返回该槽位 `enemy=null`。

```sql
CREATE TABLE idle_adventure_stage_enemy_config (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  stage INT NOT NULL,
  slot_index INT NOT NULL,
  definition_id BIGINT NOT NULL,
  level INT NOT NULL DEFAULT 1,
  enabled TINYINT NOT NULL DEFAULT 1,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uk_idle_stage_enemy_slot (stage, slot_index)
);
```

字段说明：

| 字段 | 对应接口字段 | 说明 |
| --- | --- | --- |
| id | 无 | 关卡敌人配置记录 ID |
| stage | stage | 关卡编号 |
| slot_index | slot_index | 敌人所在槽位，范围 0-5 |
| definition_id | enemy.definition_id | 敌人配置 ID，对应 `idle_adventure_enemy_config.definition_id` |
| level | 无 | 敌人等级或成长系数，后端可用它计算关卡内属性成长 |
| enabled | 无 | 是否启用该槽位配置 |
| created_at | 无 | 创建时间 |
| updated_at | 无 | 更新时间 |
| uk_idle_stage_enemy_slot | 无 | 保证同一关卡同一槽位只有一条配置 |

### 12.2 idle_adventure_skill_config

```sql
CREATE TABLE idle_adventure_skill_config (
  skill_id BIGINT PRIMARY KEY AUTO_INCREMENT,
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

字段说明：

| 字段 | 对应接口字段 | 说明 |
| --- | --- | --- |
| skill_id | skill_id | 技能配置 ID |
| skill_key | skill_key | 技能 key |
| skill_name | skill_name | 技能显示名称 |
| cooldown_rounds | cooldown_rounds | 技能冷却回合数 |
| skill_multiplier | skill_multiplier | 技能倍率 |
| damage_attribute | damage_attribute | 技能攻击属性；为空时使用角色自身 `attribute_type` |
| self_buff_preset_id | self_buff_preset_id | 技能命中或释放时对自己施加的 buff 预设 |
| target_buff_preset_id | target_buff_preset_id | 技能命中或释放时对目标施加的 buff 预设 |
| enabled | 无 | 是否启用该技能 |
| created_at | 无 | 创建时间 |
| updated_at | 无 | 更新时间 |

### 12.3 player_npc_skill

友方角色技能槽配置表。友方最多 2 个技能槽。

说明：`definition_id` 对应 `Player_npc.definition_id`。

```sql
CREATE TABLE player_npc_skill (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  definition_id BIGINT NOT NULL,
  slot_index INT NOT NULL,
  skill_id BIGINT NOT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY uk_player_npc_skill_slot (definition_id, slot_index)
);
```

字段说明：

| 字段 | 对应接口字段 | 说明 |
| --- | --- | --- |
| id | 无 | 友方技能槽配置记录 ID |
| definition_id | definition_id | 友方角色配置 ID，对应 `Player_npc.definition_id` |
| slot_index | skill_slots[].slot_index | 技能槽位，友方范围 0-1 |
| skill_id | skill_slots[].skill.skill_id | 技能配置 ID，对应 `idle_adventure_skill_config.skill_id` |
| created_at | 无 | 创建时间 |
| uk_player_npc_skill_slot | 无 | 保证同一友方角色同一技能槽只有一个技能 |

### 12.3.1 idle_adventure_enemy_skill

敌方角色技能槽配置表。敌方最多 1 个技能槽。

说明：`definition_id` 对应 `idle_adventure_enemy_config.definition_id`。

```sql
CREATE TABLE idle_adventure_enemy_skill (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  definition_id BIGINT NOT NULL,
  slot_index INT NOT NULL,
  skill_id BIGINT NOT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY uk_idle_enemy_skill_slot (definition_id, slot_index)
);
```

字段说明：

| 字段 | 对应接口字段 | 说明 |
| --- | --- | --- |
| id | 无 | 敌方技能槽配置记录 ID |
| definition_id | definition_id | 敌方角色配置 ID，对应 `idle_adventure_enemy_config.definition_id` |
| slot_index | skill_slots[].slot_index | 技能槽位，敌方当前只使用 0 |
| skill_id | skill_slots[].skill.skill_id | 技能配置 ID，对应 `idle_adventure_skill_config.skill_id` |
| created_at | 无 | 创建时间 |
| uk_idle_enemy_skill_slot | 无 | 保证同一敌方角色同一技能槽只有一个技能 |

### 12.4 idle_adventure_buff_preset

```sql
CREATE TABLE idle_adventure_buff_preset (
  buff_preset_id BIGINT PRIMARY KEY AUTO_INCREMENT,
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

字段说明：

| 字段 | 对应接口字段 | 说明 |
| --- | --- | --- |
| buff_preset_id | buff_preset_id | buff 预设 ID |
| buff_key | buff_key | buff key |
| display_name | display_name | buff 显示名称 |
| category | category | `BENEFIT` 增益或 `DETRIMENT` 减益 |
| source | source | `CHARACTER_SKILL` 或 `OTHER` |
| effect_type | effect_type | `NORMAL_DAMAGE`、`ATTRIBUTE_DAMAGE` 或 `FIELD` |
| duration_rounds | duration_rounds | 持续回合数 |
| attack_modifier | attack_modifier | 攻击力修正值 |
| defense_modifier | defense_modifier | 防御力修正值 |
| speed_modifier | speed_modifier | 速度修正值 |
| normal_damage_multiplier | normal_damage_multiplier | 普通伤害 buff 倍率 |
| attribute_damage_multiplier | attribute_damage_multiplier | 属性伤害 buff 倍率 |
| affected_attribute | affected_attribute | 生效属性；普通伤害 buff 可为空 |
| enabled | 无 | 是否启用该 buff 预设 |
| created_at | 无 | 创建时间 |
| updated_at | 无 | 更新时间 |
| uk_buff_key_source | 无 | 保证同一来源下 buff key 唯一 |

### 12.4.1 idle_adventure_field_buff_config

场地 buff 配置表，用于计算 `场地buff倍率`。后续可以按关卡、地图、天气或特殊事件决定是否启用。

```sql
CREATE TABLE idle_adventure_field_buff_config (
  field_buff_id BIGINT PRIMARY KEY AUTO_INCREMENT,
  stage INT NULL,
  field_buff_key VARCHAR(64) NOT NULL,
  display_name VARCHAR(64) NOT NULL,
  category VARCHAR(16) NOT NULL DEFAULT 'BENEFIT',
  target_side VARCHAR(16) NULL,
  affected_attribute VARCHAR(32) NOT NULL,
  field_damage_multiplier DECIMAL(8,4) NOT NULL DEFAULT 0.0000,
  enabled TINYINT NOT NULL DEFAULT 1,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  KEY idx_idle_field_buff_stage (stage)
);
```

字段说明：

| 字段 | 对应接口字段 | 说明 |
| --- | --- | --- |
| field_buff_id | field_buff_id | 场地 buff 配置 ID |
| stage | stage | 生效关卡；为空表示全局生效 |
| field_buff_key | field_buff_key | 场地 buff key |
| display_name | display_name | 场地 buff 显示名称 |
| category | category | `BENEFIT` 增益或 `DETRIMENT` 减益 |
| target_side | target_side | 限定阵营；为空表示双方生效 |
| affected_attribute | affected_attribute | 生效角色属性 |
| field_damage_multiplier | field_damage_multiplier | 场地伤害倍率 |
| enabled | 无 | 是否启用该场地 buff |
| created_at | 无 | 创建时间 |
| updated_at | 无 | 更新时间 |
| idx_idle_field_buff_stage | 无 | 按关卡查询场地 buff 的索引 |

说明：接口字段 `effect_type` 固定返回 `FIELD`，可由后端派生，不需要单独存储。

### 12.4.2 idle_adventure_attribute_restraint_config

属性克制配置表，用于判断属性克制关系。克制关系命中时，`attribute_restraint_multiplier` 固定为 `0.2`，不在数据库中配置可变倍率。

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

字段说明：

| 字段 | 对应接口字段 | 说明 |
| --- | --- | --- |
| id | 无 | 属性克制配置记录 ID |
| attacker_attribute | relations[].attacker_attribute | 攻击者属性 |
| defender_attribute | relations[].defender_attribute | 被攻击者属性 |
| enabled | 无 | 是否启用该克制关系 |
| created_at | 无 | 创建时间 |
| updated_at | 无 | 更新时间 |
| uk_idle_attribute_restraint | 无 | 保证同一克制关系不重复 |

说明：接口字段 `restraint_multiplier` 固定为 `0.2`，不从数据库读取。

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

字段说明：

| 字段 | 对应接口字段 | 说明 |
| --- | --- | --- |
| id | 无 | 关卡奖励配置记录 ID |
| stage | stage | 关卡编号 |
| item_key | rewards[].item_key | 奖励物品 key |
| display_name | rewards[].display_name | 奖励显示名称 |
| quantity | rewards[].quantity | 奖励数量 |
| enabled | 无 | 是否启用该奖励 |
| created_at | 无 | 创建时间 |
| updated_at | 无 | 更新时间 |
| uk_idle_stage_reward_item | 无 | 保证同一关卡同一物品奖励只配置一条 |

### 12.5 player_idle_adventure_npc

玩家已解锁友方 NPC 关系表。

说明：`definition_id` 对应 `Player_npc.definition_id`。若当前版本所有玩家默认拥有同一批 NPC，也可以暂时不建该表，直接从 `Player_npc` 返回启用状态的友方角色。

```sql
CREATE TABLE player_idle_adventure_npc (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  player_id BIGINT NOT NULL,
  definition_id BIGINT NOT NULL,
  unlocked TINYINT NOT NULL DEFAULT 1,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uk_player_idle_npc (player_id, definition_id)
);
```

字段说明：

| 字段 | 对应接口字段 | 说明 |
| --- | --- | --- |
| id | 无 | 玩家 NPC 解锁记录 ID |
| player_id | player_id | 玩家 ID |
| definition_id | definition_id | 友方角色配置 ID，对应 `Player_npc.definition_id` |
| unlocked | 无 | 是否已解锁；未解锁的角色不应出现在可编队列表中 |
| created_at | 无 | 创建时间 |
| updated_at | 无 | 更新时间 |
| uk_player_idle_npc | 无 | 保证同一玩家同一 NPC 只存在一条解锁记录 |

### 12.6 player_idle_adventure_formation

玩家编队表。每名玩家最多保存 6 个槽位。

```sql
CREATE TABLE player_idle_adventure_formation (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  player_id BIGINT NOT NULL,
  slot_index INT NOT NULL,
  definition_id BIGINT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uk_player_idle_formation_slot (player_id, slot_index)
);
```

字段说明：

| 字段 | 对应接口字段 | 说明 |
| --- | --- | --- |
| id | 无 | 编队槽位记录 ID |
| player_id | player_id | 玩家 ID |
| slot_index | slots[].slot_index | 编队槽位，范围 0-5 |
| definition_id | slots[].definition_id | 上阵友方角色配置 ID；为空表示空槽 |
| created_at | 无 | 创建时间 |
| updated_at | 无 | 更新时间 |
| uk_player_idle_formation_slot | 无 | 保证同一玩家同一槽位只有一条编队记录 |

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

字段说明：

| 字段 | 对应接口字段 | 说明 |
| --- | --- | --- |
| id | 无 | 战斗会话内部 ID，供其他表关联 |
| session_key | session_key | 对外返回的战斗会话 ID |
| player_id | player_id | 玩家 ID |
| stage | stage | 当前挑战关卡 |
| round | round | 当前回合数 |
| status | status | 会话状态，对应 `BattleSessionStatus` |
| paused | paused | 是否暂停 |
| mode_paused_by_choice | 无 | 是否由关卡选择、结束面板等玩家选择导致暂停 |
| pending_next_stage | pending_next_stage | 当前关卡完成后可进入的下一关 |
| created_at | 无 | 创建时间 |
| updated_at | 无 | 更新时间 |

### 12.8 idle_adventure_session_combatant

战斗会话角色状态表。

说明：`definition_type=PLAYER_NPC` 时，`definition_id` 对应 `Player_npc.definition_id`；`definition_type=ENEMY` 时，`definition_id` 对应 `idle_adventure_enemy_config.definition_id`。

```sql
CREATE TABLE idle_adventure_session_combatant (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  session_id BIGINT NOT NULL,
  runtime_id VARCHAR(32) NOT NULL,
  definition_type VARCHAR(16) NOT NULL,
  definition_id BIGINT NOT NULL,
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

字段说明：

| 字段 | 对应接口字段 | 说明 |
| --- | --- | --- |
| id | 无 | 会话角色内部 ID |
| session_id | 无 | 战斗会话内部关联 ID，对应 `idle_adventure_session.id`，不直接作为接口字段返回 |
| runtime_id | runtime_id | 本次会话中的角色运行时 ID |
| definition_type | 无 | 配置来源类型，`PLAYER_NPC` 或 `ENEMY` |
| definition_id | definition_id | 配置 ID；根据 `definition_type` 对应 `Player_npc.definition_id` 或 `idle_adventure_enemy_config.definition_id` |
| side | side | `FRIENDLY` 或 `ENEMY` |
| slot_index | slot_index | 当前阵营槽位，范围 0-5 |
| combatant_name | combatant_name | 战斗中显示名称 |
| max_health | max_health | 最大生命值 |
| current_health | current_health | 当前生命值 |
| attack | attack | 基础攻击力快照 |
| defense | defense | 基础防御力快照 |
| speed | speed | 基础速度快照 |
| attribute_type | attribute_type | 属性快照 |
| defeated | defeated | 是否已死亡 |
| created_at | 无 | 创建时间 |
| updated_at | 无 | 更新时间 |
| uk_session_runtime | 无 | 保证同一会话 runtime_id 唯一 |
| uk_session_side_slot | 无 | 保证同一会话同一阵营同一槽位只有一个角色 |

说明：接口字段 `effective_attack`、`effective_defense`、`effective_speed` 由本表基础属性加上 `idle_adventure_session_buff` 中仍生效的修正值计算得出，不单独存储。

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

字段说明：

| 字段 | 对应接口字段 | 说明 |
| --- | --- | --- |
| id | 无 | 待结算奖励记录 ID |
| session_id | 无 | 战斗会话内部关联 ID，对应 `idle_adventure_session.id`，不直接作为接口字段返回 |
| player_id | player_id | 玩家 ID |
| cleared_stage | settled_stages[] / cleared_stage | 已通关并进入待结算池的关卡 |
| item_key | granted_rewards[].item_key | 奖励物品 key |
| display_name | granted_rewards[].display_name | 奖励显示名称 |
| quantity | granted_rewards[].quantity | 奖励数量 |
| settled | 无 | 是否已发放 |
| settled_at | 无 | 发放时间 |
| created_at | 无 | 创建时间 |
| updated_at | 无 | 更新时间 |
| uk_idle_session_stage_reward | 无 | 防止同一会话同一关卡同一物品重复进入待结算池 |

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

字段说明：

| 字段 | 对应接口字段 | 说明 |
| --- | --- | --- |
| id | 无 | 运行时 buff 记录 ID |
| session_combatant_id | 无 | 会话角色内部关联 ID，对应 `idle_adventure_session_combatant.id`，不直接作为接口字段返回 |
| buff_key | active_buffs[].buff_key | buff key |
| display_name | active_buffs[].display_name | buff 显示名称 |
| category | active_buffs[].category | `BENEFIT` 增益或 `DETRIMENT` 减益 |
| source | active_buffs[].source | `CHARACTER_SKILL` 或 `OTHER` |
| effect_type | active_buffs[].effect_type | buff 效果类型 |
| remaining_rounds | active_buffs[].remaining_rounds | 剩余回合数 |
| attack_modifier | active_buffs[].attack_modifier | 攻击力修正 |
| defense_modifier | active_buffs[].defense_modifier | 防御力修正 |
| speed_modifier | active_buffs[].speed_modifier | 速度修正 |
| normal_damage_multiplier | active_buffs[].normal_damage_multiplier | 普通伤害 buff 倍率 |
| attribute_damage_multiplier | active_buffs[].attribute_damage_multiplier | 属性伤害 buff 倍率 |
| affected_attribute | active_buffs[].affected_attribute | 生效属性 |
| created_at | 无 | 创建时间 |
| updated_at | 无 | 更新时间 |
| uk_combatant_buff_source | 无 | 同一角色同一 `buff_key + source` 只保留一条，重复施加时刷新持续时间 |

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

字段说明：

| 字段 | 对应接口字段 | 说明 |
| --- | --- | --- |
| id | 无 | 运行时技能状态记录 ID |
| session_combatant_id | 无 | 会话角色内部关联 ID，对应 `idle_adventure_session_combatant.id`，不直接作为接口字段返回 |
| slot_index | skill_states[].slot_index | 技能槽位 |
| skill_id | skill_states[].skill_id | 技能配置 ID，对应 `idle_adventure_skill_config.skill_id`；为空表示该技能槽为空 |
| cooldown_remaining | skill_states[].cooldown_remaining | 当前剩余冷却回合数 |
| created_at | 无 | 创建时间 |
| updated_at | 无 | 更新时间 |
| uk_combatant_skill_state_slot | 无 | 保证同一会话角色同一技能槽只有一条冷却状态 |

说明：接口字段 `can_use` 由 `skill_id IS NOT NULL` 且 `cooldown_remaining <= 0` 计算得出，不单独存储。

### 12.11 player_idle_adventure_progress

```sql
CREATE TABLE player_idle_adventure_progress (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  player_id BIGINT NOT NULL UNIQUE,
  highest_stage INT NOT NULL DEFAULT 0,
  last_stage INT NOT NULL DEFAULT 1,
  total_wins INT NOT NULL DEFAULT 0,
  total_failures INT NOT NULL DEFAULT 0,
  running_session_key VARCHAR(64) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);
```

字段说明：

| 字段 | 对应接口字段 | 说明 |
| --- | --- | --- |
| id | 无 | 玩家闯关进度记录 ID |
| player_id | player_id | 玩家 ID |
| highest_stage | highest_stage | 玩家历史最高通关关卡 |
| last_stage | last_stage | 最近挑战或停留的关卡 |
| total_wins | total_wins | 累计通关次数 |
| total_failures | total_failures | 累计失败次数 |
| running_session_key | running_session_key | 当前未结束会话的对外会话 key；为空表示没有运行中会话 |
| created_at | 无 | 创建时间 |
| updated_at | updated_at | 更新时间 |

说明：首页接口中的 `next_stage` 由 `highest_stage + 1` 计算得出。

### 12.12 player_idle_adventure_afk_reward

玩家挂机奖励状态表。

说明：

1. 用于支持 `GET /api/idle-adventure/players/{player_id}/afk-reward` 和 `POST /api/idle-adventure/players/{player_id}/afk-reward/collect`。
2. 结算时间以服务端时间为准，客户端只展示服务端快照。
3. 奖励计算规则：由初始奖励数*层数*每层提升数。

```sql
CREATE TABLE player_idle_adventure_afk_reward (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  player_id BIGINT NOT NULL UNIQUE,
  item_key VARCHAR(64) NOT NULL DEFAULT 'gold',
  display_name VARCHAR(64) NOT NULL DEFAULT 'Gold',
  base_quantity_per_cycle INT NOT NULL DEFAULT 10,
  quantity_per_highest_stage INT NOT NULL DEFAULT 2,
  production_interval_seconds INT NOT NULL DEFAULT 30,
  max_stored_quantity INT NOT NULL DEFAULT 1000,
  stored_quantity INT NOT NULL DEFAULT 0,
  last_settled_server_unix_seconds BIGINT NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);
```

字段说明：

| 字段 | 对应接口字段 | 说明 |
| --- | --- | --- |
| id | 无 | 玩家挂机奖励状态记录 ID |
| player_id | player_id | 玩家 ID |
| item_key | item_key / collected_item.item_key | 挂机产出的物品 key |
| display_name | display_name | 挂机奖励显示名称 |
| base_quantity_per_cycle | base_quantity_per_cycle | 每个单位周期的基础奖励数 |
| quantity_per_highest_stage | quantity_per_highest_stage | 最高通关层数对单位周期奖励的提升参数 |
| production_interval_seconds | production_interval_seconds | 单位产出周期，单位秒 |
| max_stored_quantity | max_stored_quantity | 最大存储上限 |
| stored_quantity | stored_quantity | 当前已结算但未领取数量 |
| last_settled_server_unix_seconds | last_settled_server_unix_seconds | 上次服务端结算时间戳 |
| created_at | 无 | 创建时间 |
| updated_at | 无 | 更新时间 |

说明：接口字段 `quantity_per_cycle`、`server_unix_seconds` 为计算字段。`quantity_per_cycle` 按当前文档规则由 `base_quantity_per_cycle`、`quantity_per_highest_stage` 和玩家 `highest_stage` 计算；`server_unix_seconds` 使用服务端当前时间。

## 13. Unity 前端联调边界

### 13.1 开始界面

开始界面只负责展示模式入口，不应自动进入战斗。流程：

1. 进入 `Idle Adventure mode` 场景。
2. Unity 调用 `GET /api/idle-adventure/players/{player_id}/home`。
3. 渲染开始界面。
4. 玩家点击 `Formation` 时打开编队界面。
5. 玩家点击 `Start Battle` 时调用 `POST /api/idle-adventure/players/{player_id}/sessions/start`。
6. 后端返回 `RUNNING` 后，Unity 隐藏开始界面，显示战斗界面。

### 13.2 编队界面

编队界面交互：

1. 下方显示 `available_npc_list`。
2. 中间显示 6 个 `FormationSlot`。
3. 拖动 NPC 到槽位时，Unity 本地可以先预览。
4. 点击确认时调用 `PUT /api/idle-adventure/players/{player_id}/formation` 保存。
5. 后端返回保存后的完整编队。
6. 如果 `deployed_count=0`，开始按钮置为不可点击。

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

客户端只保留角色图标下方一排 buff 图标，不显示额外文字信息。每个 buff 图标颜色或样式可按 `effect_type + category + source` 区分：

| effect_type | category | source | 建议显示 |
| --- | --- | --- | --- |
| NORMAL_DAMAGE | BENEFIT | CHARACTER_SKILL / OTHER | 普通伤害增益图标 |
| NORMAL_DAMAGE | DETRIMENT | CHARACTER_SKILL / OTHER | 普通伤害减益图标 |
| ATTRIBUTE_DAMAGE | BENEFIT | CHARACTER_SKILL / OTHER | 属性增强图标，并标记 `affected_attribute` |
| ATTRIBUTE_DAMAGE | DETRIMENT | CHARACTER_SKILL / OTHER | 属性减益图标，并标记 `affected_attribute` |
| FIELD | BENEFIT | OTHER | 场地增益图标，并标记 `affected_attribute` |
| FIELD | DETRIMENT | OTHER | 场地减益图标，并标记 `affected_attribute` |

## 14. 最小联调接口清单

如果后端想先支持当前 Unity 模式进入可用状态，建议优先实现以下接口：

| 优先级 | 接口 | 用途 |
| --- | --- | --- |
| P0 | `GET /api/idle-adventure/players/{player_id}/home` | 进入模式时加载首页、可用 NPC、保存编队 |
| P0 | `GET /api/idle-adventure/players/{player_id}/npcs` | 编队界面下方 NPC 列表 |
| P0 | `GET /api/idle-adventure/players/{player_id}/formation` | 获取保存编队 |
| P0 | `PUT /api/idle-adventure/players/{player_id}/formation` | 保存拖拽后的编队 |
| P0 | `GET /api/idle-adventure/stages/{stage}/enemies` | 获取当前关卡敌人槽位 |
| P0 | `GET /api/idle-adventure/players/{player_id}/afk-reward` | 开始界面挂机奖励状态 |
| P0 | `POST /api/idle-adventure/players/{player_id}/sessions/start` | 点击开始后创建战斗 |
| P1 | `POST /api/idle-adventure/players/{player_id}/sessions/{session_key}/rounds/resolve` | 后端权威回合结算 |
| P1 | `GET /api/idle-adventure/players/{player_id}/sessions/{session_key}` | 刷新当前战斗会话状态 |
| P1 | `POST /api/idle-adventure/players/{player_id}/sessions/{session_key}/stages/continue` | 继续下一关 |
| P1 | `POST /api/idle-adventure/players/{player_id}/sessions/{session_key}/stages/retry` | 失败后重新挑战当前关 |
| P1 | `POST /api/idle-adventure/players/{player_id}/sessions/{session_key}/pause` | 暂停战斗 |
| P1 | `POST /api/idle-adventure/players/{player_id}/sessions/{session_key}/resume` | 恢复战斗 |
| P1 | `POST /api/idle-adventure/players/{player_id}/sessions/{session_key}/end` | 结束模式并结算发放本次通关奖励 |
| P1 | `POST /api/idle-adventure/players/{player_id}/afk-reward/collect` | 领取挂机奖励 |
| P2 | `GET /api/idle-adventure/config` | 独立获取玩法配置 |
| P2 | `GET /api/idle-adventure/buff-presets` | 独立获取 buff 预设 |
| P2 | `GET /api/idle-adventure/skills` | 独立获取技能配置 |
| P2 | `GET /api/idle-adventure/field-buffs?stage=1` | 独立获取场地 buff 配置 |
| P2 | `GET /api/idle-adventure/attribute-restraints` | 独立获取属性克制关系，克制倍率固定 20% |
| P2 | `GET /api/idle-adventure/stage-rewards` | 独立获取每关奖励配置 |
| P2 | `POST /api/idle-adventure/players/{player_id}/sessions/{session_key}/buffs/apply` | 道具、事件、关卡机制等额外 buff |
| P2 | `GET /api/idle-adventure/players/{player_id}/progress` | 获取闯关进度 |
| P2 | `POST /api/idle-adventure/players/{player_id}/progress/stage-clear` | 客户端本地结算时保存通关进度 |
| P2 | `POST /api/idle-adventure/players/{player_id}/formation/auto-fill` | 自动填充默认编队 |
| P2 | `POST /api/idle-adventure/players/{player_id}/formation/clear` | 清空编队 |


