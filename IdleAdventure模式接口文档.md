# Idle Adventure 模式接口文档

本文档记录 Idle Adventure 的目标接口契约，并对应 Flyway V16、V17、V51、V52、V67、V68、V69、V70。版本边界：远端 `origin/sck@08b4f0c` 已包含本文记录的敌人预览字段与 Buff 403/6515 拒绝逻辑；本地后端已恢复为 `sck@6502a67`，这两处尚未闭环，差异在 4.3 和 6.4 单独标注。Idle Adventure 对外提供 28 条 `snake_case` 接口；原 10 条无 `player_id` 的精简接口已经下线并返回 HTTP 404。28 条接口共用一套服务端权威战斗、进度、体力、固定奖励、随机掉落、挂机加速和幂等逻辑，不存在客户端结算分支。好友体力赠礼的发送接口属于好友模块，见 [`friend-stamina-gift-api.md`](friend-stamina-gift-api.md)；V70 复用同一体力聚合服务支持钻石商城购买，商城契约见 [`diamond-shop-api.md`](diamond-shop-api.md)。

基础地址：`http://localhost:8080/api`

## 一、接口总览

| 序号 | 方法 | 路径 | 用途 |
| ---: | --- | --- | --- |
| 1 | `GET` | `/idle-adventure/players/{player_id}/home` | 获取首页聚合数据 |
| 2 | `GET` | `/idle-adventure/config` | 获取玩法固定规则 |
| 3 | `GET` | `/idle-adventure/buff-presets` | 获取非场地 buff 配置 |
| 4 | `GET` | `/idle-adventure/skills` | 获取技能配置 |
| 5 | `GET` | `/idle-adventure/field-buffs` | 获取场地 buff 配置 |
| 6 | `GET` | `/idle-adventure/attribute-restraints` | 获取属性克制关系 |
| 7 | `GET` | `/idle-adventure/stage-rewards` | 获取全部关卡奖励配置 |
| 8 | `GET` | `/idle-adventure/players/{player_id}/npcs` | 获取玩家可参战 NPC |
| 9 | `GET` | `/idle-adventure/stages/{stage}/enemies` | 获取关卡敌人六槽位及完整属性预览 |
| 10 | `GET` | `/idle-adventure/players/{player_id}/formation` | 获取保存编队 |
| 11 | `PUT` | `/idle-adventure/players/{player_id}/formation` | 保存完整或局部编队变更 |
| 12 | `POST` | `/idle-adventure/players/{player_id}/formation/auto-fill` | 自动填充默认编队 |
| 13 | `POST` | `/idle-adventure/players/{player_id}/formation/clear` | 主动清空编队 |
| 14 | `POST` | `/idle-adventure/players/{player_id}/sessions/start` | 创建战斗会话 |
| 15 | `GET` | `/idle-adventure/players/{player_id}/sessions/{session_key}` | 查询或恢复会话 |
| 16 | `POST` | `/idle-adventure/players/{player_id}/sessions/{session_key}/pause` | 暂停会话 |
| 17 | `POST` | `/idle-adventure/players/{player_id}/sessions/{session_key}/resume` | 恢复会话 |
| 18 | `POST` | `/idle-adventure/players/{player_id}/sessions/{session_key}/end` | 结束并结算奖励 |
| 19 | `POST` | `/idle-adventure/players/{player_id}/sessions/{session_key}/rounds/resolve` | 权威结算一回合 |
| 20 | `POST` | `/idle-adventure/players/{player_id}/sessions/{session_key}/buffs/apply` | 行为与后端版本相关；见 6.4 |
| 21 | `POST` | `/idle-adventure/players/{player_id}/sessions/{session_key}/stages/continue` | 继续下一关 |
| 22 | `POST` | `/idle-adventure/players/{player_id}/sessions/{session_key}/stages/retry` | 重试当前关 |
| 23 | `GET` | `/idle-adventure/players/{player_id}/progress` | 获取权威进度 |
| 24 | `POST` | `/idle-adventure/players/{player_id}/progress/stage-clear` | 核实服务端通关结果 |
| 25 | `GET` | `/idle-adventure/players/{player_id}/afk-reward` | 查询挂机奖励 |
| 26 | `POST` | `/idle-adventure/players/{player_id}/afk-reward/collect` | 领取挂机奖励 |
| 27 | `GET` | `/idle-adventure/players/{player_id}/stamina` | 刷新并获取当前体力与恢复倒计时 |
| 28 | `POST` | `/idle-adventure/players/{player_id}/afk-reward/accelerate` | 消耗体力领取一份完整挂机上限奖励 |

## 二、通用约定

### 2.1 登录与玩家边界

所有接口必须携带：

```http
Authorization: Bearer <token>
```

包含 `{player_id}` 的路径必须传 token 对应玩家 ID。认证拦截器和业务服务会分别校验一次，不允许客户端通过路径访问其他玩家。没有 `{player_id}` 的配置接口仍然需要登录。

### 2.2 请求与响应格式

请求体和响应 `data` 统一使用 `snake_case`，例如 `session_key`、`formation_slots`、`client_round`。统一响应外层保持：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {}
}
```

业务错误主要通过响应体 `code` 表达，前端不能只判断 HTTP 状态。HTTP 401 表示未登录，HTTP 403 表示路径玩家越权，HTTP 404 表示接口不存在。

### 2.3 幂等请求头

以下写接口支持 `Idempotency-Key`：创建、暂停、恢复、结束、回合结算、应用 buff、继续、重试、挂机领取和挂机加速。

```http
Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
```

旧 Unity 为兼容可以省略，服务端会按会话、回合或领取周期生成受控键；新代码应主动传入。每次按钮操作生成一个 1 至 64 字符的新值，同一次网络超时重试必须复用原值。相同键和相同请求返回首次响应；相同键用于不同操作或不同请求内容返回 `6507`。

### 2.4 推荐调用流程

1. 登录后调用 `GET /players/{player_id}/home`，保存其中的 `stamina`。
2. 根据 `available_npc_list` 和 `formation` 展示编队。
3. 需要调整时调用保存、自动填充或清空接口。
4. 调用 `/sessions/start`，保存返回的 `session_key`；成功后刷新体力接口。
5. `status=RUNNING` 时按返回的 `round` 提交 `client_round`，按 `actions.action_order` 播放。
6. `STAGE_COMPLETE` 时先展示 `pending_rewards` 和 `random_drops`，再继续下一关或结束；`FAILED` 时重试或结束。
7. 断线后使用玩家 ID 和 `session_key` 查询会话，不在客户端恢复战斗数值。
8. 开始、继续和失败重试成功后都重新查询体力；暂停、恢复、回合结算和结束不扣体力。
9. 离开玩法前调用 `/end`，由后端原子发放本会话待结算奖励；需要快速结算挂机收益时调用 `/afk-reward/accelerate`，网络超时必须复用同一幂等键取得首次完整响应。

## 三、编队与槽位

六个槽位固定为两列三行：

| `slot_index` | `column` | `row` | `front_column` |
| ---: | ---: | ---: | --- |
| 0 | 0 | 0 | `true` |
| 1 | 0 | 1 | `true` |
| 2 | 0 | 2 | `true` |
| 3 | 1 | 0 | `false` |
| 4 | 1 | 1 | `false` |
| 5 | 1 | 2 | `false` |

一个玩家 NPC 实例不能占用多个槽位。首次读取尚未初始化的空编队时，后端按配置人数自动保存默认前排编队。玩家调用清空接口后，V17 的 `formation_initialized` 标记会保留主动空编队，后续首页、编队查询和开战不会偷偷填回。

## 四、配置与预览接口

### 4.1 首页

```http
GET /api/idle-adventure/players/{player_id}/home
```

返回 `player_id`、`highest_stage`、`next_stage`、`has_running_session`、`running_session_key`、`mode_config`、`stamina`、`formation` 和 `available_npc_list`。首页会在同一玩家事务中惰性恢复体力并聚合当前服务端时间点的数据，但不会执行任何战斗回合或发奖。首次进入玩法且尚无体力记录时，`current_stamina` 直接初始化为当前配置的 `max_stamina`。

### 4.2 独立配置

以下接口无请求体：

```http
GET /api/idle-adventure/config
GET /api/idle-adventure/buff-presets
GET /api/idle-adventure/skills
GET /api/idle-adventure/field-buffs?stage=1
GET /api/idle-adventure/attribute-restraints
GET /api/idle-adventure/stage-rewards
```

- `config` 返回阵型、技能槽、动画时间、克制倍率和服务端战斗规则。
- `buff-presets` 只返回当前玩法版本已启用的非场地 buff。
- `skills` 返回技能倍率、冷却、攻击属性及关联 buff ID。
- `field-buffs` 的 `stage` 可省略；传入时必须是存在且启用的关卡。
- `attribute-restraints` 返回有方向的克制关系和倍率。
- `stage-rewards` 按关卡及奖励顺序返回当前版本的固定物品奖励预览，不代表已经发放；随机池候选概率不通过该接口公开，实际命中结果在通关会话响应的 `random_drops` 中返回。

### 4.3 玩家 NPC 与关卡敌人

```http
GET /api/idle-adventure/players/{player_id}/npcs
GET /api/idle-adventure/stages/{stage}/enemies
```

玩家 NPC 的 `definition_id` 对应 NPC 配置 ID；`quality` 返回 `COMMON / FINE / EXCELLENT / EPIC / LEGENDARY` 五档稳定编码。保存或临时编队时后端会转换为当前玩家拥有的实例 ID并再次校验归属。

关卡敌人接口返回当前已发布玩法版本的关卡名称、实际敌人数和固定六槽位预览。例如：

```json
{
  "stage": 3,
  "stage_name": "林地深处",
  "enemy_count": 1,
  "slots": [
    {
      "slot_index": 0,
      "column": 0,
      "row": 0,
      "front_column": true,
      "enemy": {
        "definition_id": 101,
        "combatant_key": "forest_guardian",
        "combatant_name": "森林守卫",
        "icon": "enemy/forest-guardian.png",
        "model_key": "forest_guardian",
        "level": 12,
        "side": "ENEMY",
        "max_health": 2400,
        "attack": 180,
        "defense": 95,
        "speed": 36,
        "attribute_type": "WOOD",
        "skill_slots": []
      }
    },
    {
      "slot_index": 1,
      "column": 0,
      "row": 1,
      "front_column": true,
      "enemy": null
    }
  ]
}
```

远端 `origin/sck@08b4f0c` 的目标响应中，`slots` 始终按 `slot_index=0..5` 返回六项，示例只展开前两项；空槽的 `enemy` 为 `null`。每项同时返回 `column`、`row`、`front_column`，槽位 `0..2` 为前排第 `0` 列，槽位 `3..5` 为后排第 `1` 列，便于 Unity 直接布局而不重复硬编码换算。`level`、生命、攻击、防御和速度均为该敌人在此关卡最终生效的配置，不能用敌人模板或客户端公式重新推算。`icon` 供列表与头像展示，`model_key` 供 Unity 选择模型资源；资源缺失时客户端应使用默认占位图或通用模型，不能阻断战斗。`skill_slots` 继续返回完整技能槽定义。

目标响应只新增字段，不改变 URL、请求方式、鉴权和既有字段。当前 master 已在响应模型中增加对应字段，并在进入玩法、登录重连和回到前台时读取独立配置、玩家 NPC、当前关卡敌人预览及权威进度。本地已恢复的 `sck@6502a67` 不返回 `stage_name`、槽位布局和敌人的 `icon/model_key/level`，因此客户端会降级为关卡编号、敌人名称和数量；远端 `origin/sck@08b4f0c` 已返回这些字段。客户端不能自行推算战斗属性。敌人配置当前没有品质字段，仓库也没有统一战力计算口径，因此接口不会返回推测的 `quality` 或 `combat_power`。

### 4.4 Unity 独立配置与进度刷新

Unity 登录或重连后先调用首页和 `/progress`，以服务端 `highest_stage`、`last_stage` 更新本地进度及挂机收益显示；进入玩法页面、重连或回到前台时再调用本节 4.2/4.3 的独立 GET。独立 GET 仅用于展示、编队候选和版本核对，不会改变玩家状态；任何战斗、奖励、体力和属性结果仍以服务端会话响应为准。独立配置请求失败时可保留上一次快照并提示重试，但不得阻止已有会话恢复，也不得使用本地配置覆盖服务端快照。

## 五、编队接口

### 5.1 获取编队

```http
GET /api/idle-adventure/players/{player_id}/formation
```

返回 `player_id`、`deployed_count` 和固定六个 `slots`。每个槽位包含 `slot_index`、`column`、`row`、`front_column`、`definition_id`、`combatant_name` 和 `quality`；空槽后三个字段为 `null`。

### 5.2 保存编队变更

```http
PUT /api/idle-adventure/players/{player_id}/formation
Content-Type: application/json

{
  "slots": [
    {"slot_index": 0, "definition_id": 3001},
    {"slot_index": 1, "definition_id": null}
  ]
}
```

`slots` 最多六项，同一 `slot_index` 不能重复。传入 `definition_id` 表示设置该槽位，传 `null` 表示清空该槽位，未提交的槽位保持原值。服务端校验 NPC 归属、可用状态、重复上阵和人数上限，并把结果作为完整六槽位返回。

### 5.3 自动填充与清空

```http
POST /api/idle-adventure/players/{player_id}/formation/auto-fill
POST /api/idle-adventure/players/{player_id}/formation/clear
```

自动填充按服务端 NPC 顺序和 `starting_friendly_count` 覆盖当前编队；清空会保存主动空编队。空编队不能使用保存编队开始战斗，但仍可提交合法临时编队。

### 5.4 编队变化 WebSocket 事件

编队成功保存后，服务端向该玩家本人的全部在线连接发送：

```json
{
  "type": "IDLE_ADVENTURE_FORMATION_CHANGED",
  "requestId": null,
  "eventId": "33fddf13-598e-4f48-a1d9-0fb4ec011547",
  "playerId": 7,
  "data": {
    "player_id": 7,
    "deployed_count": 1,
    "slots": [
      {
        "slot_index": 0,
        "column": 0,
        "row": 0,
        "front_column": true,
        "definition_id": 400001,
        "combatant_name": "守林人",
        "quality": "FINE"
      }
    ]
  },
  "serverTime": "2026-08-26T10:30:00.123456"
}
```

事件覆盖局部/完整保存、自动填充、主动清空、首次默认编队初始化，以及开战前触发的默认初始化。`data` 与独立编队查询接口保持同一结构并始终补齐六个槽位；示例只展开一个槽位。

- 消息只在数据库事务提交成功后发送；事务回滚不发送，推送失败也不会回滚已经保存的编队。
- 这是本人状态变化消息，不是全服广播、公屏轮播或公告，不会发送给其他玩家。
- WebSocket 是刷新提示而不是唯一事实来源。消息可能延迟、重复、乱序或在离线期间漏收；收到事件、重连、回到前台或状态不确定时，Unity 应重新调用 `GET /api/idle-adventure/players/{player_id}/formation`。
- Unity 不应让较晚到达的旧事件覆盖刚完成的 HTTP 保存响应。最稳妥的处理是合并短时间内的刷新请求，再以最后一次 HTTP 查询结果覆盖本地编队。
- 多 API 实例部署必须启用 `redis_pubsub` 并保证 Redis 可用；单实例 `local_only` 只推送本节点连接。

### 5.5 管理端查看任意玩家编队

```http
GET /api/ops/players/{playerId}/idle-adventure/formation
Authorization: Bearer <operator-token>
```

该接口复用玩家中心只读权限，供管理端查看指定玩家的基础信息、挂机冒险进度和当前保存编队。它严格只读：未进入过玩法的玩家不会因为运营查看而创建进度，也不会自动填充默认编队。

管理端响应使用运营接口既有的 `camelCase` 字段，固定返回六个槽位。非空槽位包含玩家 NPC 实例 ID、NPC 配置 ID、名称、品质、等级、状态、战斗属性，以及基础成长和当前穿戴装备合并后的生命、攻击、防御、速度。`combatProfileAvailable=false` 表示对应 NPC 或当前等级战斗配置已停用、缺失或不可用于当前战斗，便于运营发现异常保存数据。玩家不存在返回业务码 `2001`。该接口不授予编辑、代替玩家换阵或绕过 NPC 归属校验的能力。

## 六、战斗会话接口

### 6.1 创建会话

```http
POST /api/idle-adventure/players/{player_id}/sessions/start
Content-Type: application/json
Idempotency-Key: start-20260722-001

{
  "stage": 1,
  "use_saved_formation": true,
  "formation_slots": null
}
```

`stage` 可省略，默认挑战 `highest_stage + 1`；允许重打已开放关卡，不允许跳过未通关关卡。`use_saved_formation` 默认 `true`。使用临时编队时传：

```json
{
  "stage": 1,
  "use_saved_formation": false,
  "formation_slots": [
    {"slot_index": 0, "definition_id": 3001}
  ]
}
```

临时编队只用于本次会话，不覆盖保存编队。后端锁定玩家，读取一次数据库业务时间，固化玩法版本、NPC 属性、装备加成、技能、敌人、场地 buff、克制、固定奖励、随机池和 NPC 重复碎片规则快照。

创建会话会在同一事务内按该关 `stamina_cost` 扣除体力。体力关闭时仍保留恢复数据但不扣除；体力不足时返回 HTTP 409、业务码 `6511`，会话创建及其他写入全部回滚。相同 `Idempotency-Key` 的成功重试只重放首次响应，不会再次扣体力。会话响应不内嵌扣除后的体力，Unity 应在成功后调用独立体力接口刷新显示。

### 6.2 查询、暂停和恢复

```http
GET  /api/idle-adventure/players/{player_id}/sessions/{session_key}
POST /api/idle-adventure/players/{player_id}/sessions/{session_key}/pause
POST /api/idle-adventure/players/{player_id}/sessions/{session_key}/resume
```

暂停只把 `RUNNING` 改为 `PAUSED`，不释放活动会话、不改变回合和战斗快照。暂停时禁止回合结算和额外 buff。恢复只允许 `PAUSED -> RUNNING`。已经处于目标状态的暂停或恢复请求也会写入幂等账本，之后使用同一请求号严格重放首次响应。

### 6.3 权威结算回合

```http
POST /api/idle-adventure/players/{player_id}/sessions/{session_key}/rounds/resolve
Content-Type: application/json
Idempotency-Key: round-ia-session-1-1

{"client_round":1}
```

客户端只能提交期望回合号。行动顺序、目标、技能、伤害、buff、死亡、胜负、进度和奖励均由后端计算。`initiative_order` 是回合开始时的不可变顺序，即使某角色在行动前被击败，也仍保留在顺序中但不会产生动作。

产生 `STAGE_COMPLETE` 时，后端先根据更新前的 `highest_stage` 判断是否首通，再持久化符合 `grant_scope` 的固定奖励和随机抽取结果。每个启用随机池独立执行 `draw_count` 次，每次必定命中一个候选奖励，不存在空奖励；同一回合幂等重放不会重复抽取。

### 6.4 应用配置化额外 buff

```http
POST /api/idle-adventure/players/{player_id}/sessions/{session_key}/buffs/apply
Content-Type: application/json
Idempotency-Key: buff-ia-session-1-001

{
  "target_side": "FRIENDLY",
  "slot_index": 0,
  "buff_preset_id": 16001,
  "source": "OTHER"
}
```

该路径的行为取决于后端版本。远端 `origin/sck@08b4f0c` 统一返回 HTTP 403、业务码 `6515`，不会读取或修改战斗会话；本地已恢复的 `sck@6502a67` 会读取会话锁定版本的配置并实际施加来源为 `OTHER` 的 Buff。由于现有请求没有服务端签发的广告、活动、道具消耗或次数凭证，本地旧行为允许客户端在缺少业务资格证明时请求增益；正式开放按钮前仍需补齐授权凭证、消费流水和防重放校验。

### 6.5 继续与重试

```http
POST /api/idle-adventure/players/{player_id}/sessions/{session_key}/stages/continue
POST /api/idle-adventure/players/{player_id}/sessions/{session_key}/stages/retry
```

继续只允许 `STAGE_COMPLETE`，进入下一关后回到 `RUNNING`、`stage_attempt=1`、`round=1`。重试只允许 `FAILED`，关卡不变、尝试次数加一、回合重置。两者都重新生成运行时状态，但继续使用会话创建时固化的友方属性、技能和玩法版本。

继续按下一关的 `stamina_cost` 扣体力，失败重试按当前关的 `stamina_cost` 再扣一次；两种操作都在会话状态更新前完成同事务校验，体力不足时全部回滚。暂停、恢复和重复查询不扣体力。成功后 Unity 应刷新独立体力接口。

### 6.6 结束并发奖

```http
POST /api/idle-adventure/players/{player_id}/sessions/{session_key}/end
Idempotency-Key: end-ia-session-1
```

允许从 `RUNNING`、`PAUSED`、`STAGE_COMPLETE` 或 `FAILED` 结束。后端在同一事务中锁定会话和未结算奖励，发放物品、创建独立装备实例、创建 NPC 或执行 NPC 重复碎片转换，然后标记奖励已结算并把会话改为 `ENDED`。任一资产发放失败都会整体回滚；当前未通关关卡不产生奖励，相同请求号不会重复发放。

`granted_rewards` 是本次实际入账结果，`items` 是发奖后的完整物品背包。装备和 NPC 不在 `items` 中，应依据 `granted_rewards` 刷新对应仓库。随机抽中一个玩家已经拥有的 NPC 时，`random_drops` 仍显示抽中的 NPC，`granted_rewards` 则返回实际碎片物品并标记 `fallback_applied=true`。网络超时后必须用原 `Idempotency-Key` 重试 `/end`，服务端会返回第一次的完整发奖结构。

## 七、进度与挂机接口

### 7.1 获取和确认进度

```http
GET  /api/idle-adventure/players/{player_id}/progress
POST /api/idle-adventure/players/{player_id}/progress/stage-clear
Content-Type: application/json

{
  "session_key": "ia-session-1",
  "cleared_stage": 1
}
```

进度查询返回 `highest_stage`、`last_stage`、`running_session_key`、`total_wins`、`total_failures` 和 `updated_at`。`stage-clear` 只核实指定会话已经处于对应 `STAGE_COMPLETE` 且权威进度已写入，不接受客户端推进关卡、伪造胜利或触发发奖。

### 7.2 查询挂机奖励

```http
GET /api/idle-adventure/players/{player_id}/afk-reward
```

返回奖励物品、基础单周期数量、最高关卡增量、最终单周期数量、周期秒数、最大存储量、当前存储量及服务端 Unix 时间。时间和数量由数据库业务时间及玩家挂机规则快照计算。

### 7.3 领取挂机奖励

```http
POST /api/idle-adventure/players/{player_id}/afk-reward/collect
Idempotency-Key: afk-collect-20260722-001
```

无请求体。后端锁定玩家和挂机状态后计算可领取增量，原子写入背包并保存完整幂等响应。玩法版本切换时先按旧快照结算到同一业务时间，再启用新规则，不用新配置追溯旧挂机时间。

响应中的 `collected_rewards` 是本次实际入账的完整奖励列表；配置切换恰好跨越两种挂机产物时会返回两项。旧字段 `collected_reward` 继续保留，指向列表第一项；没有可领取数量时返回旧产物的零数量兼容项。新 Unity 必须优先遍历 `collected_rewards`，不能只读取旧字段，否则可能漏展示切换后的第二种产物。

### 7.4 查询体力

```http
GET /api/idle-adventure/players/{player_id}/stamina
```

无请求体。接口会使用数据库统一北京时间惰性结算已经完成的恢复周期；客户端不得自行增加体力，只能用倒计时做平滑展示。示例：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "enabled": true,
    "current_stamina": 97,
    "max_stamina": 100,
    "over_cap": false,
    "overflow_amount": 0,
    "recovery_interval_seconds": 300,
    "next_recovery_at": "2026-08-03T14:05:00+08:00",
    "seconds_to_next_recovery": 180,
    "seconds_to_full": 780,
    "server_time": "2026-08-03T14:02:00+08:00",
    "config_version": 12
  }
}
```

- `current_stamina` 使用 64 位整数；Unity C# 必须用 `long`，不要用 `int` 承接。
- `seconds_to_next_recovery` 和 `seconds_to_full` 为服务端快照。Unity 可用 `server_time` 校正本地倒计时，到零后重新请求，不能在本地直接确认恢复成功。
- 满体力或超上限时 `next_recovery_at=null`，两个秒数字段均为 `0`。
- 运营降低上限时不会截断玩家已有体力。此时 `over_cap=true`、`overflow_amount=current_stamina-max_stamina`，恢复暂停，消费到低于新上限后重新开始计时。
- 已有活动会话继续使用会话创建时的体力上限和恢复周期；结束会话后下一次查询才切换到当前活动版本，避免进行中的会话被中途改规则。
- 发布新恢复周期时，服务端先按旧周期结算完整恢复次数，再从同一数据库业务时间按新周期重新建立锚点，旧周期剩余秒数不折算。
- 好友赠礼、钻石商城购买等外部发放先结算自然恢复，再原子增加体力；允许超过当前运营上限，超额期间 `over_cap=true` 且暂停自然恢复。好友赠礼不扣发送方体力，完整规则见 [`friend-stamina-gift-api.md`](friend-stamina-gift-api.md)；钻石购买与钻石扣减、订单和每日额度在同一事务中提交，成功后 Unity 应重新调用本接口刷新完整体力快照。

### 7.5 挂机加速

```http
POST /api/idle-adventure/players/{player_id}/afk-reward/accelerate
Authorization: Bearer <token>
Idempotency-Key: afk-accelerate-20260806-001
```

请求体为空。`Idempotency-Key` 必须由客户端生成并在同一次网络超时重试中保持不变，长度为 1 至 64 个字符。一次成功请求只消耗一次管理端配置的体力，并立即发放一份按当前最高关卡计算的完整挂机累计上限奖励。

响应的核心结构如下：

```json
{
  "code": 200,
  "message": "挂机加速成功",
  "data": {
    "player_id": 7,
    "mode_config_id": 18,
    "mode_config_version": 3,
    "highest_stage": 5,
    "stamina_before": 50,
    "stamina_cost": 10,
    "stamina_after": 40,
    "acceleration_cycles": 720,
    "natural_rewards": [],
    "acceleration_rewards": [],
    "total_rewards": [],
    "stamina": {},
    "state": {},
    "items": [],
    "idempotent_replay": false
  }
}
```

`natural_rewards` 是本次先结算的自然挂机增量，`acceleration_rewards` 是本次完整上限奖励，`total_rewards` 是按物品主键合并后的实际入账明细。三组奖励项均使用 `AssetReward` 字段：`reward_source` 分别为 `AFK_NATURAL`、`AFK_ACCELERATION`、`AFK_TOTAL`，并包含 `reward_type`、`asset_id`、`reward_key`、`reward_name`、`reward_icon`、`reward_category`、`item_id`、`item_key`、`item_name`、`quantity` 和 `fallback_applied`。同一物品会合并数量，不同物品仍分别返回。

结算规则：

1. 先按挂机状态保存的旧物品、类型、产率和余数结算自然收益；配置切换时不会把旧物品 ID 与新物品类型混用，未满周期余数会按新规则继续折算。
2. 再按本次采用的玩法版本和最新最高关卡计算 `afk_max_seconds / afk_interval_seconds` 对应的完整奖励，`acceleration_cycles` 是完整周期数。
3. 体力、自然奖励、加速奖励、挂机状态和加速幂等流水在同一事务中提交；背包入账按物品主键快照执行，不依赖物品当前名称或启停状态。
4. 体力不足、奖励超过经济护栏、背包余额可能溢出或任一写入失败时，整笔事务回滚，不推进挂机时钟、不扣体力、不写成功幂等流水。
5. 存在进行中的战斗会话时沿用该会话的 `mode_config_id`；没有进行中会话时使用当前 `ACTIVE` 玩法版本。
6. 相同玩家和相同幂等键再次请求只返回第一次完整响应，并将 `idempotent_replay` 设为 `true`，不会再次扣体力或发奖。

Unity 端点击后应立即禁用重复点击并生成新幂等键；超时、断线或 5xx 使用同一个键重试，成功或业务错误后再恢复按钮。成功后直接使用 `total_rewards` 和 `items` 刷新显示，并以响应中的 `stamina` 更新体力。`6511` 表示体力不足，`6513` 表示加速未开放，`6514` 表示奖励超过物理上限；客户端不得自行扣除或补发。

## 八、战斗响应模型

会话响应主要字段：

| 字段 | 说明 |
| --- | --- |
| `session_key`、`player_id` | 会话业务标识和 token 玩家 |
| `stage`、`stage_name`、`stage_attempt` | 当前关卡及尝试次数 |
| `round`、`round_before`、`round_after` | 下一次期望回合及本次回合变化 |
| `status`、`paused` | `RUNNING/PAUSED/STAGE_COMPLETE/FAILED/ENDED` |
| `stage_choice_pending`、`pending_next_stage` | 胜利后是否等待继续或结束；已到最终关时下一关为 `null` |
| `initiative_order` | 回合开始时服务端确定的完整行动顺序 |
| `actions` | 本回合按 `action_order` 播放的权威动作 |
| `friendlies`、`enemies` | 固定六槽位；空槽或已经死亡的槽位为 `null` |
| `logs` | 行动顺序和战斗日志 |
| `pending_rewards` | 已通关但尚未结束领取的固定或随机资产快照 |
| `random_drops` | 本会话已产生的逐抽命中结果和审计区间 |
| `settled_stages`、`granted_rewards` | 已结算关卡和本次实际入账的通用资产增量 |
| `items` | 发奖后的完整物品背包；装备和 NPC 需刷新各自接口 |
| `message`、`idempotent_replay` | 操作提示和是否重放首次响应 |

`actions.damage_formula` 包含基础伤害、技能倍率、普通 buff、属性 buff、场地 buff、克制倍率、最终倍率及攻防属性。Unity 只负责播放和展示，不得重新计算并覆盖结果。

### 8.1 通用奖励字段

`pending_rewards` 与 `granted_rewards` 都使用 `AssetReward`：

| 字段 | 说明 |
| --- | --- |
| `reward_source` | 待结算时为 `FIXED` 或 `RANDOM`；实际入账汇总项可以为 `null` |
| `reward_type` | `ITEM`、`EQUIPMENT` 或 `NPC` |
| `asset_id`、`reward_key` | 资产主键和稳定编码；新 Unity 以这两个字段识别奖励 |
| `reward_name`、`reward_icon`、`reward_category` | 通关时冻结的展示快照，不受之后改名、停用影响 |
| `item_id`、`item_key`、`display_name` | 旧 Unity 兼容字段；非物品奖励为 `null` |
| `quantity` | 奖励数量；NPC 固定为 `1`，装备按数量创建独立实例 |
| `grant_scope` | 待结算时为 `FIRST_CLEAR` 或 `EVERY_CLEAR`；实际入账项可以为 `null` |
| `fallback_applied` | 实际入账时是否因 NPC 已拥有而转换为碎片；待结算项可以为 `null` |

### 8.2 随机掉落字段

`random_drops` 每一项对应一次真实抽取：

| 字段 | 说明 |
| --- | --- |
| `drop_result_id` | 服务端逐抽审计主键 |
| `pool_key`、`pool_name` | 发布时冻结的掉落池编码和名称 |
| `draw_index` | 当前池内从 `1` 开始的抽取序号 |
| `random_value` | `[0, 100000000)` 内的服务端安全随机值 |
| `interval_start`、`interval_end` | 左闭右开命中区间，满足 `start <= value < end` |
| `reward_type`、`asset_id`、`reward_key` | 抽中的原始资产，不代表 NPC 重复转换后的最终资产 |
| `reward_name`、`reward_icon`、`reward_category`、`quantity` | 通关时冻结的展示结果 |

客户端可以直接用 `random_drops` 播放掉落动画，但资产到账提示必须以 `/end` 返回的 `granted_rewards` 为准。

## 九、状态机与错误码

```text
创建 -> RUNNING
RUNNING --pause--> PAUSED
PAUSED --resume--> RUNNING
RUNNING --回合胜利--> STAGE_COMPLETE
RUNNING --全灭或达到最大回合--> FAILED
STAGE_COMPLETE --continue--> RUNNING
FAILED --retry--> RUNNING
RUNNING/PAUSED/STAGE_COMPLETE/FAILED --end--> ENDED
```

| code | 含义 |
| ---: | --- |
| 6501 | 玩法未开放或当前配置不存在 |
| 6502 | 编队为空、越权、重复或配置不完整 |
| 6503 | 关卡不存在、配置不完整或不允许跳关 |
| 6504 | 会话不存在或不属于当前玩家 |
| 6505 | 当前会话状态不允许该操作 |
| 6506 | 客户端回合号与服务端不一致 |
| 6507 | 幂等键被用于不同操作或不同内容 |
| 6508 | 玩法配置或会话快照无效 |
| 6509 | 会话被并发修改 |
| 6510 | 挂机配置或算术无效 |
| 6511 | 体力不足；HTTP 状态为 `409 Conflict`，刷新体力后再挑战 |
| 6512 | 外部体力发放将超过数据库物理上限；HTTP 409，本次事务整体回滚 |
| 6513 | 挂机加速未开放；HTTP 状态为 `409 Conflict`，不扣体力且不发奖 |
| 6514 | 挂机奖励超过系统物理上限；HTTP 状态为 `409 Conflict`，本次事务整体回滚 |
| 6515 | 远端 `origin/sck@08b4f0c`：额外战斗增益没有服务端授权；HTTP 状态为 `403 Forbidden`，不读取或修改会话。本地 `sck@6502a67` 尚无此业务码 |

## 十、数据库与版本边界

V16 新增版本化玩法、buff、技能、敌人、关卡、克制、奖励、编队、进度、会话、幂等命令、待结算奖励和挂机状态。V17 增加 `PAUSED`、`PAUSE/RESUME/BUFF_APPLY` 命令类型和 `formation_initialized` 编队初始化标记。V18 增加独立配置草稿、正式版本、配置锁和追加审计；V19 把 Buff、技能和敌人主键改为数据库自增，供完整版本发布安全分配全局 ID。

已开放玩法版本不得原地修改或删除。运营配置中心的 `IDLE_ADVENTURE` 模块每次发布都会创建完整新版本，校验全部引用后在同一事务中切换唯一 `ACTIVE`；历史会话继续引用旧 `mode_config_id` 和 JSON 快照，不会被新配置追溯修改。直接 SQL 修改会被实时指纹识别并阻止旧草稿覆盖，但不会自动产生页面发布审计，因此生产配置优先通过管理端发布。

V20 增加装备逐级速度加成；新建战斗会话会把 NPC 基础速度与已穿戴装备速度相加后固化到战斗快照，已有会话不受后续配置或换装影响。V21 至 V50 继续增加运营安全、成长、好友、商城、统一资产、运行策略和数据库约束加固。

V51 新增：

- `idle_adventure_mode_config.stamina_enabled/max_stamina/recovery_interval_seconds`。
- `idle_adventure_stage_config.stamina_cost` 和固定奖励 `grant_scope`。
- `player_idle_adventure_stamina` 玩家体力、策略快照、恢复锚点和乐观版本。
- 版本化随机池、随机候选奖励、通关抽取批次和逐抽结果表。
- 待结算奖励对 `ITEM / EQUIPMENT / NPC` 的通用资产快照及 NPC 重复碎片快照。

V52 不修改已经执行的 V51，而是独立加固默认值、CHECK 约束、复合索引和级联外键。历史固定奖励在 V51 回填为 `EVERY_CLEAR` 保持原行为；V52 以后新建固定奖励的数据库默认值和管理端默认值为 `FIRST_CLEAR`。V61 为玩家 NPC 和编队响应增加统一品质字段，详细编码见 [`asset-quality-api.md`](asset-quality-api.md)。V67 为游戏好友体力赠礼增加体力配置字段和幂等流水，固定 CRC32 为 `459381446`。V68 增加挂机加速开关、体力成本和加速幂等审计流水，固定 CRC32 为 `-1616972206`；V69 在不修改已执行 V68 的前提下补齐挂机物品类型快照、历史回填和非空约束，固定 CRC32 为 `423149015`。V70 扩展钻石商城商品和订单 CHECK，使 `STAMINA / stamina` 可以通过同一体力聚合服务原子发放，固定 CRC32 为 `1183825380`。V71 增加独立建筑生产能力，不改变 Idle Adventure 接口。V72-V73 增加并加固实名与防沉迷能力，同样不改变 Idle Adventure 玩法契约。当前仓库 V1 至 V80 以及 V81、V81.1、V82、V83、V84、V85、V86、V87、V88、V89、V90、V91、V92、V93 一旦成功执行均禁止修改；已有 V92 数据库正常执行 V93，后续变化必须从 V94 新增迁移，禁止用 `repair` 掩盖已执行脚本漂移。

### 10.1 管理端配置

运营进入管理端“配置中心 -> 挂机冒险”，读取模块 `IDLE_ADVENTURE`、配置键 `IDLE_ADVENTURE`。操作仍遵循“编辑 -> 校验 -> 保存草稿 -> 发布”，保存草稿不影响玩家，发布时才创建完整新玩法版本并原子切换 `ACTIVE`。

#### 10.1.1 开关含义与误操作恢复

管理端基础规则页只提供两个可变玩法开关和一个只读状态标识，三者不能混用：

| 页面字段 | 对应配置 | 实际影响 | 关闭后的行为 |
| --- | --- | --- | --- |
| 闯关体力扣除 | `staminaEnabled` | 开始关卡、继续下一关、失败重试是否扣 `staminaCost` | 不再扣关卡体力；体力仍按恢复规则增长，挂机自然产出不变 |
| 挂机加速按钮 | `afkAccelerationEnabled` | 是否允许调用挂机加速接口并消耗一次体力 | 加速接口返回 `6513 / HTTP 409`；普通挂机和已产生的待领取奖励不受影响 |
| 玩法版本状态 | `enabled`、`status`、`active_marker` | 当前版本是否可被运行时选中 | 正式发布要求始终为启用；页面显示“必须启用”，不能用它停运玩法 |

启用挂机加速前必须先启用闯关体力扣除；页面会阻止不满足前置条件的操作。关闭任一可变开关会弹出二次确认，取消确认不会修改当前草稿。关卡、敌人槽位、场地 Buff、固定奖励、随机池和属性克制的 `enabled` 由发布规则强制为启用，不需要的条目应删除；Buff、技能和敌人模板可以停用，但仍被引用时发布会被拒绝。

误操作按是否发布处理：

1. **只改了页面、尚未保存**：点击底部“撤销页面修改”，工作副本恢复为最近一次服务端文档。
2. **已经保存草稿、尚未发布**：点击底部“丢弃”，输入原因后确认；正式配置和玩家运行规则不变。
3. **已经发布**：打开“版本历史”，查看需要恢复的历史版本，点击“回滚”。系统只创建回滚草稿，不会直接覆盖线上版本；有回滚权限的管理员必须检查校验和发布预览后再发布。
4. **玩法版本状态显示“配置异常”**：点击页面内“恢复为必须启用”，保存草稿并重新校验。服务端不会接受 `enabled=false` 的正式发布。

`CLIENT_RUNTIME` 的维护字段会影响全局客户端登录/版本策略，不是挂机冒险专用开关，不能用于替代挂机冒险停运。当前如果确实需要只停运挂机冒险，应先增加独立的玩法维护策略和对应 Unity 提示契约，再进行配置发布。

关卡展开后，敌方阵容编辑表下方提供“阵容属性预览”。预览固定展示六个槽位并标明前排/后排、敌人名称、等级、战斗属性、技能、图标、模型标识以及每个槽位的生命、攻击、防御、速度；同时展示生命、攻击、防御合计和速度总和（仅用于配置核对）。预览只读计算，不会回写草稿，也不代表独立的战斗力评分。空槽、重复槽位或未完成填写的行会安全降级为预览状态，正式保存和发布仍由后端执行连续关卡、槽位、引用和数值校验。

敌人配置当前没有品质字段，服务端也没有统一的战斗力公式，因此管理端和 Unity 均不显示推测的“品质/战斗力”。如未来需要该能力，应先定义版本化计算口径，再新增配置和接口，不得用生命、攻击等字段临时相加替代。

体力配置：

- `staminaEnabled`：是否实际扣体力；关闭后开始、继续、重试均不扣除。
- `maxStamina`：`1..1000000`，新玩家首次进入直接获得该上限。
- `recoveryIntervalSeconds`：`1..2678400`，表示恢复 1 点需要的秒数。
- 每关 `staminaCost`：`1..1000000`；启用体力时不得超过 `maxStamina`，否则禁止发布。

挂机加速配置：

- `afkAccelerationEnabled`：是否开放消耗一次体力、领取一份完整挂机上限奖励，默认关闭。
- `afkAccelerationStaminaCost`：每次加速消耗的体力，范围为 `1..maxStamina`；启用加速但未启用体力系统，或成本超过体力上限时禁止发布。
- 管理端按最高已配置关卡、基础产量、关卡增量、结算周期和累计上限实时预览单次加速的周期产量、完整周期数和奖励数量；发布时服务端使用同一整数公式再次校验经济保护上限。
- 加速奖励始终按 `afkMaxSeconds` 计算完整累计上限，不受玩家当前已积累秒数影响；本次自然挂机收益仍会先结算。

固定奖励：

- 每关最多 100 项，只能从已启用物品字典选择，数量为 `1..1000000000`。
- `grantScope=FIRST_CLEAR` 仅首次通关发放；`EVERY_CLEAR` 每次通关发放。

随机掉落：

- 每关最多 20 个池，每池 `drawCount` 为 `1..100`，每次抽取必定命中，不能配置空奖励。
- 每池最多 100 个候选项，类型为 `ITEM / EQUIPMENT / NPC`；NPC 数量必须为 `1`。
- `probabilityUnits` 使用一亿分比，池内启用项必须严格合计 `100000000`。管理端按百分比显示到 6 位小数，`1` 单位等于 `0.000001%`。
- 池也支持 `FIRST_CLEAR / EVERY_CLEAR`。选择 NPC 时，发布过程自动读取该 NPC 的专属碎片和重复转换数量并冻结快照，运营不手填碎片 ID。
- 发布会重新校验资产存在、已启用、NPC 可获取、候选不重复、序号连续、概率精确合计和引用类型；任一错误均不创建半成品版本。

管理端正式接口、角色权限、草稿冲突和发布审计见 [`operator-config-center.md`](operator-config-center.md)。

## 十一、验证范围

自动化测试固定以下边界：

1. Idle Adventure 包只允许存在 `IdleAdventureController` 一个 REST 控制器；Unity 字段转换由 API DTO、VO 和 `IdleAdventureFacade` 统一完成。
2. 28 条方法和路径必须精确匹配本文档，所有请求/响应字段保持 `snake_case`。
3. MockMvc 逐条请求 28 条路径必须命中，原 10 条精简路径必须全部返回 404。
4. 路径玩家越权、临时编队归属、主动清空、暂停/恢复幂等和通关防伪均有单元测试；额外 Buff 测试随版本不同分别覆盖服务端配置来源或未授权拒绝。
5. 战斗测试覆盖行动顺序、死亡跳过、目标选择、技能冷却、倍率、状态机、固定奖励和随机池快照。
6. 体力测试覆盖首次满体力、惰性恢复、恢复余数、满值消费锚点、策略切换、超上限保留、余额不足和乐观锁冲突。
7. 随机奖励测试覆盖概率边界、首通范围、非法概率、逐抽审计，以及 `ITEM / EQUIPMENT / NPC` 发放和 NPC 重复碎片转换。
8. V52 阶段验收时，MySQL 集成测试覆盖 V1 至 V52、V51/V52 校验和、CHECK 约束、复合外键和级联关系；当时后端全量测试 587 项通过、0 failure、0 error，63 项外部环境用例按条件跳过，管理端生产构建及真实 Spring Boot 启动均通过。
9. 2026-08-10 当前验收的后端测试报告共 847 项，0 failure、0 error，其中 63 项外部环境用例按条件跳过；MySQL 8 临时空库 V1 至 V73 顺序迁移和第二次零变更通过；管理端生产构建转换 3562 个模块。
10. 编队补强测试覆盖运营只读权限与六槽位聚合、Mapper 只读 SQL、局部更新在玩家锁内原子合并、失效槽清理、事务提交后推送、回滚不推送和推送故障隔离；管理端生产构建同时校验类型契约。

## 十二、上线前必须确认

1. **运营必须复核经济配置**：体力和 `FIRST_CLEAR / EVERY_CLEAR` 已实现，但高价值物品、装备、NPC、随机概率及重复刷取范围仍需在发布前人工复核；付费货币原则上只配置为首通奖励。
2. **回合没有服务端时间限频**：体力只在开始、继续和失败重试时扣除，动画间隔仍不阻止玩家在单场内用不同请求号快速提交回合。需要真实回合节奏时应增加服务端冷却，而不是信任 Unity 动画时长。
3. **挂机最高关卡倍率会追溯生效**：当前领取时按最新最高关卡计算未领取时间；更严格方案需要在通关事务中先结算旧倍率。
4. **入驻 NPC 当前仍可参战**：`IDLE` 和 `SETTLED` NPC 都能进入编队。如产品要求互斥，查询与开战校验必须同时限制。
5. **buff 展示上限需统一**：快照安全上限高于旧 Unity 六个图标槽，运营配置应控制同时生效数量。
6. **历史流水需要清理策略**：会话命令、挂机幂等、随机批次和逐抽审计会持续增长，只能分批清理终态、已结算且超过审计保留期的数据；不得先删仍被待结算奖励引用的逐抽记录。
7. **额外 Buff 授权仍未闭环**：本地 `sck@6502a67` 会实际施加配置 Buff，远端 `origin/sck@08b4f0c` 则固定返回 6515。正式开放按钮前必须增加服务端授权凭证、消耗流水、次数/过期校验和防重放规则；幂等键不能替代资格校验。
