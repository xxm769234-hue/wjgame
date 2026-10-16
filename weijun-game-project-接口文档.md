# Weijun Game Project 后端接口文档

源码迁移边界已推进至 V157；此前业务库已确认执行 V149（`-292257608`）、V150（`601341822`）、V151（`-1451868567`）和 V152（`571652493`），当时失败迁移为 0。V153 充值档位管理、V154 约束加固和 V155 系统奖励排序修复的 checksum 分别为 `-1761361088`、`125577309`、`-618690687`；本机 `game_db` 已执行 V156 属性调整（checksum `-656179002`）和 V157 历史玩法恢复（checksum `349671882`），v1/v2 各恢复七条旧克制关系。管理端已发布活动玩法 v3（配置中心正式发布第 2 版），含 13 条图中关系、倍率 `0.20`，整版使用 Unity 默认背景。其他目标库以各自的 `flyway_schema_history` 和活动玩法版本为准。V151 已按数据库实际执行原文恢复并冻结；禁止修改已执行迁移或使用 `flyway repair`。后续数据库变化从 V158 新增迁移。已删数据仅能从迁移前备份恢复，详见末尾“V149/V150：未上线通用食物清理”。

物品示例中的小麦为 `wheat / RESOURCE`；`itemId=200000` 仅是标准初始化参考，实际编号由服务端返回，Unity 不得硬编码。

V148 NPC 首次专属碎片合成合同（checksum `-1250904303`）保持冻结；V149 已清理未上线的通用食物（checksum `-292257608`），V150 根据残余快照和配置完整性补强（checksum `601341822`），V151 已建立 NPC 手动突破请求（checksum `-1451868567`），V152 仅前向补强约束和一键升级停止结果；V153/V154 新增充值档位管理并前向加固约束；V155 按原有顺序修复系统奖励模板资产排序断号；V156 引入八种属性与 13 条有向克制关系，V157 前向恢复被覆盖的历史玩法版本，管理端再发布新规则为活动玩法 v3。阅读迁移与上线说明时，以顶部当前边界、第 18.1 节和仓库真实迁移文件为准。

重新生成时间：2026-09-29
更新日期：2026-10-16
历史同步说明（V147 阶段）：当时按玩家端、微信公共回调和 `/api/ops/**` 管理端路由复核并同步至 Flyway V147。V134 新增挂机冒险技能效果、初始冷却、目标阵营/模式、有序目标组、随机目标和单体治疗配置；V135 单独增加友方全体技能的 `include_self` 字段，避免修改已执行的 V134；V136 为 NPC 增加可空的 `portrait_key` 角色立绘绑定键；V137 清理尚未上线环境的戒指、饰品、项链装备及其开发期引用，并将新装备部位收敛为五项正式白名单；V138 增加装备手选任意等级材料和不可变等级快照并冻结；V139 前向增加装备一键升级 V2 的分级材料、全路径原子执行和幂等审计；V140 再前向加固单次突破请求生命周期和材料培养快照一致性；V141 显式拒绝缺少材料分组快照、计划指纹或材料选择指纹的 V2/手选审计记录；V142 将统一五档品质扩展到货币、基础资源、产物、NPC 碎片和活动抽奖券，并增加存量物品上线前一次性人工确认及永久锁定；V143 将活动抽奖券改为跨活动永久通用并统一钻石单价；V144 增加玩家首次男女选择及对应基础 NPC 发放、NPC/碎片品质一致性校验；V145 增加 NPC 碎片出售结算资源与数量的不可变合同；V146 冻结每批来源数量；V147 在启动阶段预检启用活动券商品的统一钻石单价（checksum=`1724824817`）。旧技能按 `DAMAGE + OPPONENT + SINGLE` 兼容，战斗会话创建时冻结技能规则，Unity 只播放服务端返回的逐目标结果。其余 V97～V133 的历史迁移、签到、活动券、地块和建筑合同保持不变；V134～V147 成功执行后均禁止修改，不得执行 `repair` 绕过。以上内容为 V147 历史记录，当前边界以本文顶部、第 18.1 节和文末“近期配置与接口补充”为准。
V148 新增 NPC 首次专属碎片合成：接口、管理端配置、幂等、订单审计和 Unity 接入见本文末尾“V148：NPC 首次专属碎片合成与图鉴自动入口”章节；V148 checksum=`-1250904303`，生产部署前需执行 Flyway 并完成迁移合同测试。
装备与品质最新规则：新装备部位只能是 `WEAPON / HELMET / ARMOR / BOOTS / GLOVES`；`RING / ACCESSORY / NECKLACE` 已由 V137 清理，管理端不再显示或允许创建。NPC 与装备品质英文编码保持 `COMMON / FINE / EXCELLENT / EPIC / LEGENDARY` 不变，中文统一显示为“普通、优秀、稀有、史诗、传说”，Unity 只需更新本地名称/颜色映射，不需修改 DTO 或接口字段。
Unity 本次需要修改：保留既有 NPC 获取渠道改造，并新增 `GET /api/customer-service/entry` 预取；玩家点击客服按钮的 touch/click 回调中调用微信官方 `wx.openCustomerServiceConversation`，支持 `sessionFrom` 过期重拉、最低基础库 `2.0.3`、关闭状态和 SDK 失败提示。管理端工单、微信回调和 Outbox 不需要 Unity 实现。
经营地图本次需要 Unity 修改：所有场景与地块请求携带 `X-Client-Version-Code`；场景列表读取 `minimumClientVersionCode`；从 `landSlots[]` 读取坐标级 `requiredLevel / unlockMode / tierCode / tierName / costAmount / purchaseCost / ruleFingerprint / sceneConfigVersion`；购买时回传包含 `expectedCurrencyItemId` 在内的完整 `expected*` 报价字段；区分金币和钻石图标、余额与确认文案；购买成功后刷新完整 32 格。首个完整能力版本号为 `10003`，低版本按 `426/7603` 进入更新引导。
沙漠生产建筑本次不新增 Unity 接口：继续使用可建造建筑、建造条件、地图、生产状态和配方接口，按服务端返回的 `buildType / needLand / modelKey / icon / produce` 展示。Unity 包内需提供 `sandstone_quarry_lv1/lv2/lv3` 与 `ore_smelting_kiln_lv1/lv2/lv3` 模型/图标资源，或由管理员在正式启用前把 Key 改为包内实际值；客户端不得假定建筑类型只有旧 13 种，资源缺失时应回退通用占位资源并记录错误。
本次头像改造：Unity 删除微信头像授权、临时文件读取、上传和远程下载逻辑；玩家目录新增可直接传给 `Resources.Load<Sprite>` 的 `resourceKey`，旧 Unity 仍可继续按 `avatarCode` 从包内资源显式映射加载。服务端仅保存编号和资源合同，`avatarUrl` 始终为 `null`。V103 后 `AVATAR_001` 也必须重新通过真实 SHA-256 清单验收；后续头像可在管理端登记，但必须经过“待客户端发布 → Unity Editor/可信 CI 自动清单验收 → 管理员显式启用”。管理端不提供手工 JSON 导入；图片必须随对应 Unity 包发布，清单验收不会上传或下发图片。旧 Unity 不带版本头时统一使用包内默认头像，不会获得未验收目录项。
当前文档以源码 V157 边界为准：V143～V148 的活动券、碎片出售与 NPC 首次合成合同保持冻结，V149～V152 已有此前业务库执行记录；V153～V157 的各目标库迁移结果须核对 `flyway_schema_history`。已执行脚本禁止修改或使用 `flyway repair`；后续数据库变化从 V158 开始。下文旧章节中“最高 V152、下一 V153”和“最高 V155、下一 V156”等语句只记录对应历史阶段，不代表当前边界。

客户端：Unity / 微信小游戏前端
后端基础地址：`http://localhost:8080/api`

签到本次 Unity 接入：三个签到接口继续携带 `X-Client-Version-Code`；读取 `rewardMode`、`rewardWindowDays`、`windowDays` 和 `expiresAt`，按服务端返回的 `days[]` 动态渲染。`CLAIM_SEQUENCE` 下只显示奖励档位，不生成参与窗口的空日期；30 天有效期、3 档奖励时只渲染 3 项，玩家可在第 1、6、7 天领取。领取请求仍只提交 `planCode`，客户端不得提交奖励序号或自行计算日期。旧客户端在版本门禁下必须进入升级引导。
统一 Content-Type：`application/json; charset=UTF-8`

## 当前版本补充（V143～V147）

### V147 启动预检与当时版本边界

- V147 迁移文件为 `V147__validate_active_activity_ticket_prices.sql`，checksum=`1724824817`。
- 启动时仅校验启用且未归档的 `CARD_DRAW_TICKET` 商品：每批券数和钻石价格必须为正、价格必须可被券数整除、每张券价格必须等于 `diamond_shop_policy.activity_ticket_diamond_price_per_ticket`；发现脏数据会明确阻断迁移，不会自动修正或静默回退默认价格。
- V147 不改变玩家券余额、不修改商城商品、不影响已完成订单；迁移成功后重复启动应为零变更。V152 阶段已完成 NPC 手动突破请求及约束补强，当时下一份迁移为 V153。

### 活动抽奖券

- `CARD_DRAW_TICKET` 是永久有效的活动抽奖券，可用于任何一个当前开放的限时卡池，不受券的来源活动约束，也不限于下一期卡池；券不会因活动结束失效、自动转换、自动出售或被删除。
- 限时活动只决定卡池开放时间以及签到、任务和商城购券等获取渠道的窗口，不决定已获得券的有效期。管理端不再提供“到期策略”入口，也不支持写入或发布 `ACTIVITY_TICKET_POLICY`；历史策略表和出售订单保留用于审计。
- 抽奖时按物品类型汇总并扣除可用券，券的历史发放、使用和出售记录只用于审计；新的活动券出售报价和成交被禁止。
- 普通钻石商品规则不变。活动券钻石商城商品统一使用 `activityTicketDiamondPricePerTicket`，商品总价必须等于“每张券单价 × 每批券数”；单价由管理端统一配置。

### 物品品质

- 货币、基础资源、产物、NPC 碎片和活动抽奖券统一使用 `COMMON / FINE / EXCELLENT / EPIC / LEGENDARY`，中文为“普通、优秀、稀有、史诗、传说”。
- 存量物品首次发布必须由 `CONFIG_ADMIN` 明确配置品质；品质发布后永久冻结，不能通过编辑、回滚或停用修改。
- NPC 碎片品质必须与其绑定 NPC 品质一致；不一致时管理端发布阻断，运行时也拒绝发放或兑换。

### 男女角色与基础 NPC

- `GET /api/player/gender` 查询选择状态，`POST /api/player/gender` 首次选择 `MALE` 或 `FEMALE`；选择成功后事务内发放管理端配置的对应基础 NPC。
- 玩家只能成功选择一次；相同性别重复请求幂等，改选另一性别拒绝。选择记录保存 NPC 编码、品质、头像、立绘和模型资源快照。
- 管理端模块为 `PLAYER_GENDER_STARTER / STARTER_NPCS`。男女 NPC 必须品质、等级上限、成长曲线、战斗属性和产出属性完全一致；基础 NPC 即使 `obtainable=false` 也可在 starterNpcs 字典中选择。

### NPC 碎片出售

- 管理端在“配置中心 → 资源出售 → 资源兑换规则”配置每个 NPC 专属碎片的来源数量、结算资源和结算数量；结算目标仅允许货币或资源。
- NPC 碎片规则首次正式发布后，来源/目标快照和兑换数量形成不可变合同；管理端显示“已冻结”并禁用相关字段，不能原地修改或删除已发布合同。
- 发布前强制校验碎片绑定启用 NPC、碎片与 NPC 品质一致、来源/目标物品启用且编码与 ID 匹配。玩家出售仍使用幂等、玩家锁、背包锁、每日额度和审计快照。

### 装备升级与突破

- 普通升级只升一级；到达启用突破门槛时返回 `6210`，不自动消耗突破材料。
- 普通升级、手动突破和一键升级目标允许保持 `WAREHOUSE` 或 `EQUIPPED`，无需卸下，也不会改变穿戴关系；只有被消耗的突破材料必须为同款、仓库、未锁定、未穿戴、未消耗且不得重复或等于目标装备。`6215` 仅保留旧码兼容，不再因目标穿戴返回；直接提交已穿戴材料仍会被 `6212` 拒绝。
- V1 一键升级只执行到突破点之前；V2 路径包含任一启用突破点时整体拒绝 `6210`，不自动截断，非突破路径可全路径原子执行。玩家到门槛后必须使用单次手动突破，同款任意等级材料每件占一个槽，高等级材料需要确认且培养投入永久消耗。执行事务失败整体回滚。
- Unity 不再因目标已穿戴禁用升级、突破或一键升级；材料未穿戴限制不变。成功后刷新装备、余额，以及 `GET /api/equipment/loadout/{playerNpcId}` 的穿戴 NPC 总属性。新战斗会话读取新属性；已经开始的战斗会话（含该会话后续关卡及重试）继续使用原快照，不追溯改变战斗。

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
| 业务异常 | `code` 为业务码或 500，`message` 为业务提示 |
| 系统异常 | HTTP 500，`code=500`，`message=系统异常，请联系管理员` |

### 1.2 登录鉴权

登录、注册、微信登录成功后，后端返回：

```json
{
  "token": "服务端token",
  "expiresIn": 604800,
  "user": {
    "id": 1,
    "username": "player1",
    "nickname": "Player1",
    "nicknameVersion": 0,
    "nicknameUpdatedTime": null,
    "nicknameChangeCount": 0,
    "nicknameChangeRequired": false,
    "level": 1,
    "totalExperience": 0,
    "levelCurveRevision": 1,
    "currentLevelExperience": 0,
    "requiredExperienceForNextLevel": 100,
    "experienceProgressBps": 0,
    "maxLevel": false,
    "economyFrozen": false,
    "gold": 10000,
    "wood": 5000,
    "stone": 3000,
    "iron": 2000,
    "diamond": 0,
    "items": [
      {
        "itemId": 1,
        "itemKey": "gold",
        "name": "金币",
        "itemType": "CURRENCY",
        "icon": "gold",
        "quantity": 10000
      }
    ]
  },
  "accountStatus": "ACTIVE",
  "accountManagementOnly": false,
  "privacyConsentRequired": false,
  "privacyPolicyVersion": "2026.08.05"
}
```

Unity 后续调用业务接口必须带：

```http
Authorization: Bearer <token>
```

`user.id` 就是游戏里的 `playerId`。例如用户 id 为 3，就调用：

```http
GET /api/land/map/3
Authorization: Bearer <token>
```

后端会校验路径或请求体里的 `playerId` 是否等于 token 对应用户 ID，不能用 A 用户 token 请求 B 用户数据。返回中的 `gold/wood/stone/iron/diamond` 是给资源栏快捷读取的字段，真实数量来源是 `player_item`；完整背包列表看 `items`，装备实例看装备仓库接口。

### 1.3 不需要 token 的接口

| 接口 | 说明 |
| --- | --- |
| `GET /api` 或 `GET /api/` | 服务根路径 |
| `GET /api/health` | 健康检查 |
| `GET /api/health/ping` | 心跳检查 |
| `POST /api/user/login` | 账号密码登录 |
| `POST /api/user/register` | 账号注册 |
| `POST /api/user/wx-login` | 微信小游戏登录 |
| `GET /api/privacy/policy/current` | 登录前读取当前正式隐私政策元数据 |
| `GET /api/privacy/policy/current/content` | 登录前读取当前正式隐私政策纯文本正文 |
| `GET /api/client/runtime-policy` | 客户端启动时查询版本、强制更新和维护状态 |
| `POST /api/payment/notify/wechat` | 微信支付服务器回调，不使用业务 token |
| `POST /api/ops/auth/login` | 独立运营账号登录，不使用玩家 token |
| `GET /api/ops/auth/context` | 运营登录页的系统、版本和环境信息，不使用玩家 token |
| `POST /api/notification/operation/send` | 内部运营提醒接口，使用 `X-Notification-Admin-Key` 后台密钥 |
| `POST /api/mail/admin/send` | 内部运营邮件接口，使用 `X-Notification-Admin-Key` 后台密钥 |

其他玩家业务接口都需要玩家 `Authorization`。除登录外的 `/api/ops/**` 接口必须携带独立的 `Authorization: Bearer <operator-token>`，玩家 token 不能访问运营接口。

## 2. 认证接口

### 2.1 账号密码登录

```http
POST /api/user/login
Content-Type: application/json
```

是否需要 token：否

前端传参：

```json
{
  "username": "player1",
  "password": "123456"
}
```

参数说明：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| username | string | 是 | 登录账号，不能为空 |
| password | string | 是 | 登录密码，不能为空 |

后端处理：

1. 去除 `username` 前后空格。
2. 查询 `users` 表：`username + password` 是否匹配。
3. 登录成功后，禁用该用户旧 token。
4. 插入新记录到 `auth_token` 表。
5. 按当前等级曲线惰性对账玩家等级和曲线版本，只升不降。
6. 返回 token、成长进度、用户基础信息和背包资源信息。

返回 data：`LoginResponse`

```json
{
  "token": "xxx",
  "expiresIn": 604800,
  "user": {
    "id": 1,
    "username": "player1",
    "nickname": "Player 1",
    "level": 1,
    "totalExperience": 0,
    "levelCurveRevision": 1,
    "currentLevelExperience": 0,
    "requiredExperienceForNextLevel": 100,
    "experienceProgressBps": 0,
    "maxLevel": false,
    "economyFrozen": false,
    "gold": 10000,
    "wood": 5000,
    "stone": 3000,
    "iron": 2000,
    "diamond": 0,
    "items": [
      {
        "itemId": 1,
        "itemKey": "gold",
        "name": "金币",
        "itemType": "CURRENCY",
        "icon": "gold",
        "quantity": 10000
      }
    ]
  },
  "accountStatus": "ACTIVE",
  "accountManagementOnly": false,
  "privacyConsentRequired": false,
  "privacyPolicyVersion": "2026.08.05"
}
```

失败情况：

| 场景 | message |
| --- | --- |
| 用户名或密码错误 | 用户名或密码错误 |
| username/password 为空 | 参数校验提示 |

### 2.2 账号注册

```http
POST /api/user/register
Content-Type: application/json
```

是否需要 token：否

前端传参：

```json
{
  "username": "test01",
  "password": "123456",
  "nickname": "测试玩家"
}
```

参数说明：

| 字段 | 类型 | 必填 | 限制 | 说明 |
| --- | --- | --- | --- | --- |
| username | string | 是 | 最大 50 字符 | 注册账号，唯一 |
| password | string | 是 | 最大 255 字符 | 注册密码 |
| nickname | string | 是 | NFKC 后 2 至 12 个码点 | 仅中文、ASCII 字母和数字，禁止纯数字，全服唯一 |

后端处理：

1. 检查 `users.username` 是否已存在。
2. 对昵称执行 NFKC、长度、字符白名单、纯数字和正式保留词校验。
3. 通过规范化昵称唯一索引裁决全服唯一性，再插入新用户到 `users` 表。
4. 初始化玩家默认资源到 `player_item`：金币 1000、木材 500、石材 500、铁矿 300。
5. 自动初始化玩家经营区域：
   - 插入 `player_map`，默认 `map_size=10`。
   - 插入默认初始地块到 `player_land`：`1001(0,0)`、`1002(1,0)`、`1004(0,1)`。
6. 签发 token，写入 `auth_token`。
7. 返回登录响应。

返回 data：`LoginResponse`

失败情况：

| 场景 | message |
| --- | --- |
| 账号已存在 | 该账号已经注册过 |
| 昵称已存在 | 该昵称已被使用 |
| 昵称格式无效 | 昵称只能使用2至12位中文、英文字母或数字 |
| 昵称包含保留词 | 昵称包含系统保留词 |
| 参数不合法 | 参数校验提示 |

### 2.3 微信小游戏登录

```http
POST /api/user/wx-login
Content-Type: application/json
```

是否需要 token：否

推荐请求：

```json
{
  "code": "wx.login 返回的一次性 code",
  "nickname": "测试玩家",
  "privacyAuthorized": true,
  "privacyPolicyVersion": "2026.08.05",
  "privacyConsentRequestId": "2257b487-e1bc-4d02-8e56-552e6da734d4",
  "clientVersion": "1.0.0",
  "recoverCancellation": false,
  "cancellationRecoveryRequestId": null
}
```

`code` 必填且只能使用一次。新玩家必须先主动同意并提交 `privacyAuthorized=true`、当前 `privacyPolicyVersion` 和授权 UUID。已有但授权失效的玩家可以不重新同意，后端会返回受限管理令牌，使其仍可进入授权或注销管理页；只有进入游戏才需要重新授权。冷静期账号只有在玩家明确点击恢复后才提交 `recoverCancellation=true` 和独立的 `cancellationRecoveryRequestId`。

后端使用 `code` 调用微信 `jscode2session`，按 `openid` 原子查找或创建玩家；新建玩家、初始资源、地图、微信绑定与首次授权在同一事务中提交。`session_key` 不持久化。并发首次登录的唯一键失败请求会整体回滚后读取已提交绑定，不产生孤立初始化数据。

响应 `LoginResponse` 在 V65 新增 `accountStatus/accountManagementOnly/privacyConsentRequired/privacyPolicyVersion`。`accountManagementOnly=true` 时 `user=null`，Unity 必须进入授权或注销管理页，不能进入主场景。完整首登、重签、撤回和冷静期恢复时序见第 43 节。

失败情况：

| 场景 | HTTP / code | 处理 |
| --- | --- | --- |
| AppID/AppSecret 未配置 | 业务失败 | 检查服务端部署密钥 |
| code 无效或重复使用 | 业务失败 | 重新调用 `wx.login` |
| 未发布正式隐私政策 | HTTP 503 / `7701` | 阻止登录并通知运营 |
| 新玩家或当前授权无效 | HTTP 403 / `7702` | 展示并确认当前政策 |
| 提交期间政策版本变化 | HTTP 409 / `7703` | 重新拉取政策并重新确认 |
| 账号已完成注销 | HTTP 410 / `7708` | 清除旧 token 和进度缓存 |


### 2.4 获取当前登录用户和最新资源

```http
GET /api/user/me
Authorization: Bearer <token>
```

是否需要 token：是

前端传参：无请求体。

使用场景：

Unity 在放置地块、开始建造、取消建造、升级建筑等会改变资源数量的操作后，可以调用该接口刷新资源栏。

后端处理：

1. 从 `Authorization` 请求头解析 token。
2. 查询 `auth_token` 表，确认 token 有效且未过期。
3. 刷新当前等级曲线，并在 `users` 行锁内惰性对账累计经验对应的等级和曲线版本；只允许升级，不会降低已有等级。
4. 从同一份不可变等级曲线快照计算等级内经验、下一级需求、进度基点和满级标记。
5. 从 `player_item` 查询背包资源，返回当前玩家最新成长数据、资源快捷字段和 `items` 背包列表。

返回 data：`UserResponse`

```json
{
  "id": 1,
  "username": "player1",
  "nickname": "Player1",
  "nicknameVersion": 0,
  "nicknameUpdatedTime": null,
  "nicknameChangeCount": 0,
  "nicknameChangeRequired": false,
  "level": 1,
  "totalExperience": 0,
  "levelCurveRevision": 1,
  "currentLevelExperience": 0,
  "requiredExperienceForNextLevel": 100,
  "experienceProgressBps": 0,
  "maxLevel": false,
  "economyFrozen": false,
  "gold": 9900,
  "wood": 4950,
  "stone": 3000,
  "iron": 2000,
  "diamond": 0,
  "items": [
    {
      "itemId": 1,
      "itemKey": "gold",
      "name": "金币",
      "itemType": "CURRENCY",
      "icon": "gold",
      "quantity": 9900
    },
    {
      "itemId": 200000,
      "itemKey": "wheat",
      "name": "小麦",
      "itemType": "RESOURCE",
      "icon": "wheat",
      "quantity": 0
    }
  ]
}
```

失败情况：

| 场景 | message |
| --- | --- |
| 未带 token 或 token 过期 | 未登录或登录已过期 |

### 2.5 查询玩家背包/仓库物品列表

```http
GET /api/inventory/list/{playerId}
Authorization: Bearer <token>
```

是否需要 token：是

前端传参：路径中的 `playerId` 必须等于当前 token 对应用户 ID。

后端处理：

1. 校验 token 和 `playerId` 归属。
2. 查询 `item_config` 中启用的物品配置。
3. 左连接 `player_item`，玩家未拥有的物品数量按 0 返回。
4. Unity 可按 `itemKey/itemType/icon` 自行展示资源栏、背包或仓库。

返回 data：`InventoryItemResponse[]`

```json
[
  {
    "itemId": 1,
    "itemKey": "gold",
    "name": "金币",
    "itemType": "CURRENCY",
    "icon": "gold",
    "quantity": 9900
  },
  {
    "itemId": 200000,
    "itemKey": "wheat",
    "name": "小麦",
    "itemType": "RESOURCE",
    "icon": "wheat",
    "quantity": 0
  }
]
```

### 2.6 退出登录

```http
POST /api/user/logout
Authorization: Bearer <token>
```

是否需要 token：是

前端传参：无请求体。

后端处理：

1. 从 `Authorization` 请求头解析 token。
2. 查询 `auth_token` 表中启用状态的 token。
3. 将该 token 的 `enabled` 改为 `false`。

返回 data：`null`

```json
{
  "code": 200,
  "message": "操作成功",
  "data": null
}
```

## 3. 地图和地块接口

### 3.1 获取玩家完整地图数据

```http
GET /api/land/map/{playerId}
Authorization: Bearer <token>
```

是否需要 token：是

路径参数：

| 参数 | 类型 | 说明 |
| --- | --- | --- |
| playerId | number | 玩家 ID，必须等于登录返回的 `user.id` |

后端处理：

1. 查询 `users` 表，确认玩家存在。
2. 查询 `player_map`，如果不存在则创建默认地图 `map_size=10`。
3. 查询 `player_land` 获取玩家已放置地块。
4. 根据每个地块的 `land_id` 查询 `land_config`，组装地块展示信息。
5. 查询 `player_building` 获取玩家建筑列表。
6. 根据 `building_config` 和 `building_level_config` 组装建筑展示信息。

返回 data：`MapDataResponse`

```json
{
  "playerId": 1,
  "width": 10,
  "height": 10,
  "lands": [
    {
      "playerLandId": 1,
      "landId": 1001,
      "landType": "GRASSLAND",
      "landName": "Grassland Farm Plot",
      "allowBuildType": "FARM",
      "level": 1,
      "positionX": 0,
      "positionY": 0
    }
  ],
  "buildings": [
    {
      "playerBuildingId": 1,
      "buildingId": 2001,
      "landId": 1,
      "buildingCode": "farm_2001",
      "buildingName": "Farm",
      "buildingType": "RESOURCE",
      "level": 1,
      "produce": "wheat:60",
      "modelKey": "farm_lv1",
      "icon": "farm_lv1",
      "status": "COMPLETED",
      "positionX": 0,
      "positionY": 0,
      "buildStartTime": null,
      "buildEndTime": null,
      "remainingTime": null,
      "progress": null
    }
  ]
}
```

### 3.2 获取当前可放置地块列表

```http
GET /api/land/available/{playerId}
Authorization: Bearer <token>
```

是否需要 token：是

路径参数：

| 参数 | 类型 | 说明 |
| --- | --- | --- |
| playerId | number | 玩家 ID |

后端处理：

1. 查询 `users` 表获取玩家等级，并从 `player_item` 查询玩家资源。
2. 查询 `land_config` 中 `enabled=true` 的配置。
3. 使用 `gold_cost/wood_cost/stone_cost/iron_cost` 数字字段计算消耗；`cost` 仅作为前端展示文本，例如 `gold:100,wood:50`。
4. 根据玩家等级和背包资源计算每种地块是否可放置；停用地块不会出现在列表中。

返回 data：`LandConfigResponse[]`

```json
[
  {
    "landId": 1001,
    "landType": "GRASSLAND",
    "name": "Grassland Farm Plot",
    "cost": "gold:100,wood:50",
    "unlockLevel": 1,
    "allowBuildType": "FARM",
    "goldCost": 100,
    "woodCost": 50,
    "stoneCost": 0,
    "ironCost": 0,
    "canPlace": true,
    "cannotPlaceReason": ""
  }
]
```

说明：地块类型使用稳定编码，管理端显示中文，例如 `DESERT` 显示为“沙漠”。V110 预置的沙漠模板 `landId=1010` 默认停用；V130 只向该模板追加 `SANDSTONE_QUARRY` 与 `ORE_SMELTING_KILN` 准入，不覆盖运营已经填写的名称、解锁等级、金币/木材/石材/铁矿费用或开放状态。两座新建筑自身也默认停用，因此升级数据库不会自动开放零费用沙漠。运营需在配置中心 `LAND_BUILDING / WORLD` 中核对完整规则并显式开启。停用只阻止新的查询和放置，不删除玩家已经放置的地块及其建筑；地图接口仍会返回存量地块。

### 3.3 放置地块

```http
POST /api/land/place
Content-Type: application/json
Authorization: Bearer <token>
```

是否需要 token：是

前端传参：

```json
{
  "playerId": 1,
  "landId": 1001,
  "positionX": 2,
  "positionY": 3
}
```

参数说明：

| 字段 | 类型 | 必填 | 限制 | 说明 |
| --- | --- | --- | --- | --- |
| playerId | number | 是 | 必须等于当前登录用户 ID | 玩家 ID |
| landId | number | 是 | 必须存在且 `enabled=true` | 地块配置 ID |
| positionX | number | 是 | 非负整数，且小于地图大小 | X 坐标 |
| positionY | number | 是 | 非负整数，且小于地图大小 | Y 坐标 |

后端处理：

1. 校验请求体 `playerId` 是否等于当前 token 用户。
2. 查询 `users` 表确认玩家存在。
3. 查询或创建 `player_map`。
4. 查询 `land_config` 并校验地块处于开放状态；服务端会重新读取最新配置，旧列表中的停用地块不能绕过校验。
5. 校验坐标是否在地图范围内。
6. 校验玩家等级是否满足 `unlock_level`。
7. 查询 `player_land`，校验该坐标没有已有地块。
8. 原子扣除 `player_item` 中的金币、木材、石材、铁矿。
9. 插入 `player_land`。
10. 返回新地块信息。
11. 如前端需要刷新资源栏，可继续调用 `GET /api/user/me`。

返回 data：`LandTileResponse`

失败情况：

| 场景 | message |
| --- | --- |
| 坐标超出地图 | 坐标超出地图范围 |
| 位置已有地块 | 该位置已有地块 |
| 等级不足 | 玩家等级需要达到 X |
| 资源不足 | 资源不足 |

## 4. 建筑接口

建筑状态枚举：

| 状态 | 说明 |
| --- | --- |
| BUILDING | 建造中 |
| COMPLETED | 已完成 |
| UPGRADING | 升级中 |
| DEMOLISHED | 已拆除，目前删除操作直接删除记录，不返回该状态 |

建筑类型枚举：

| 类型 | 说明 |
| --- | --- |
| RESOURCE | 资源建筑 |
| MILITARY | 军事建筑 |
| DEFENSE | 防御建筑 |
| FUNCTIONAL | 功能建筑 |
| DECORATION | 装饰建筑 |

当前数据库建筑目录共 15 种，其中 V130 新增的砂石场和矿冶窑默认停用。解锁等级来自 `building_config.unlock_level`，管理员可在 `LAND_BUILDING / WORLD` 配置；以下是 V130 完成后的系统默认值，玩家实际可见内容以启用状态、地块准入和接口响应为准。

| buildingId | buildType | 默认名称 | 默认解锁等级 | 前置建筑 | 数量限制 |
| ---: | --- | --- | ---: | --- | --- |
| 2001 | `FARM` | 农场 | 1 | 无 | 不限 |
| 2002 | `RANCH` | 牧场 | 1 | 无 | 不限 |
| 2003 | `MINE` | 矿场 | 2 | 无 | 不限 |
| 2004 | `LUMBER` | 伐木场 | 1 | 无 | 不限 |
| 2013 | `CEDAR_LUMBER` | 雪松林场 | 1 | 无 | 不限；默认仅开放到原本允许 `LUMBER` 的森林地块 |
| 2005 | `WOOD_PROCESSING` | 木材加工厂 | 3 | 伐木场 | 不限 |
| 2006 | `FISHERY` | 渔场 | 2 | 无 | 不限 |
| 2007 | `METAL_PROCESSING` | 金属加工厂 | 4 | 矿场 | 不限 |
| 2008 | `HOT_SPRING` | 温泉 | 4 | 无 | 不限 |
| 2009 | `BAKERY` | 面包坊 | 2 | 农场 | 不限 |
| 2010 | `BARBECUE_STALL` | 烧烤摊 | 2 | 牧场 | 不限 |
| 2011 | `DIAMOND_MINE` | 钻石矿场 | 6 | 矿场 | 玩家跨全部场景最多 1 座 |
| 2012 | `PURIFICATION_LAB` | 净化剂工坊 | 5 | 无 | 玩家跨全部场景最多 1 座 |
| 2014 | `SANDSTONE_QUARRY` | 砂石场 | 2 | 无 | 不限；默认停用且默认只允许沙漠地块 |
| 2015 | `ORE_SMELTING_KILN` | 矿冶窑 | 4 | 矿场（2003） | 不限；默认停用且默认只允许沙漠地块 |

V128 历史初版曾写入 90/150/250 木材、三级经验全 0，并临时开放到全部森林地块；V129 已前向修正，不修改 V128 历史。当前默认三级木材产量为 84、144、240，升级经验为 0、100、200，默认模型/图标 Key 为 `cedar_lumber_lv1`、`cedar_lumber_lv2`、`cedar_lumber_lv3`，默认只开放到原本允许 `LUMBER` 的森林地块；它按普通资源建筑处理。管理员可在 `LAND_BUILDING / WORLD`（配置中心 → 地块与建筑 → 等级与生产）修改所需地块类型、森林/雪地等地块的允许建造类型、解锁等级、逐级成本、建造时间、产量、生产周期、暂存容量、模型 Key 和图标 Key，也可在 `BUILDING_PRODUCTION / GLOBAL`（配置中心 → 建筑生产）修改产量、生产周期和暂存容量。系统固定 `CEDAR_LUMBER` 建筑类型、唯一默认配方和 `wood` 木材产物，禁止改成其他产物、增加生产投入或额外配方；启用等级仍必须连续。钻石矿场固定产出 `diamond`，净化剂工坊固定产出 `purification_agent`。两种单例特殊建筑同样禁止投入物品、额外配方、修改产物、停用系统默认配方、NPC 入驻和 NPC 产量加成；管理员仍可修改每周期产量、生产周期和最大暂存量。

V130 新增的砂石场和矿冶窑都按普通资源建筑处理，没有玩家维度单例数量限制。砂石场三级默认石材产量为 `60/108/180`，即按历史矿场石材档位 `50/90/150` 计算出的初始 20% 增量；它没有生产投入，后续修改矿场不会自动重算砂石场。矿冶窑沿用金属加工厂的初始成本、耗时、周期、容量与 `iron -> metal` 规则，三级默认消耗铁矿 `100/180/300`、产出金属 `50/90/150`。系统固定 `SANDSTONE_QUARRY` 只能保留无投入、唯一 `stone` 产出的启用 `DEFAULT` 配方，固定 `ORE_SMELTING_KILN` 只能保留唯一 `iron` 投入和唯一 `metal` 产出的启用 `DEFAULT` 配方；两个配置入口均禁止换物品、增删投入/产出、增加额外配方或停用默认配方，但允许调整数量、成本、经验、耗时、周期、容量、解锁等级、所需地块、模型/图标 Key 和启停状态。

V130 默认逐级规则：

| 建筑 | 等级 | 建造/升级成本（金币/木材/石材/铁矿） | 经验 | 建造耗时 | 每周期投入 → 产出 | 周期 | 容量 | 默认模型/图标 Key |
| --- | ---: | --- | ---: | ---: | --- | ---: | ---: | --- |
| 砂石场 | 1 | `200/100/50/0` | 0 | 90 秒 | 无 → 石材 60 | 3600 秒 | 500 | `sandstone_quarry_lv1` |
| 砂石场 | 2 | `360/180/120/30` | 100 | 150 秒 | 无 → 石材 108 | 3600 秒 | 900 | `sandstone_quarry_lv2` |
| 砂石场 | 3 | `680/320/240/100` | 200 | 240 秒 | 无 → 石材 180 | 3600 秒 | 1500 | `sandstone_quarry_lv3` |
| 矿冶窑 | 1 | `600/0/300/200` | 0 | 180 秒 | 铁矿 100 → 金属 50 | 3600 秒 | 800 | `ore_smelting_kiln_lv1` |
| 矿冶窑 | 2 | `1100/0/560/360` | 100 | 300 秒 | 铁矿 180 → 金属 90 | 3600 秒 | 1400 | `ore_smelting_kiln_lv2` |
| 矿冶窑 | 3 | `2000/0/1000/700` | 200 | 480 秒 | 铁矿 300 → 金属 150 | 3600 秒 | 2200 | `ore_smelting_kiln_lv3` |

### 4.1 获取可建造建筑列表

```http
GET /api/building/available/{playerId}
Authorization: Bearer <token>
```

是否需要 token：是

后端处理：

1. 查询 `users` 表获取玩家等级，并从 `player_item` 查询玩家资源。
2. 查询 `building_config` 中 `enabled=true` 的建筑。
3. 对每个建筑查询 `building_level_config` 的 1 级配置。
4. 检查玩家等级、资源、前置建筑是否满足。
5. 对特殊建筑统计玩家跨全部场景已拥有数量。
6. 返回所有启用建筑，并带 `canBuild/cannotBuildReason` 和单例数量字段。

返回 data：`BuildingConfigResponse[]`

```json
[
  {
    "id": 2001,
    "code": "farm_2001",
    "name": "Farm",
    "description": "wheat:60",
    "needLand": "GRASSLAND",
    "cost": "gold:100,wood:50",
    "produce": "wheat:60",
    "buildType": "FARM",
    "type": "RESOURCE",
    "level": 1,
    "goldCost": 100,
    "woodCost": 50,
    "stoneCost": 0,
    "ironCost": 0,
    "buildTime": 30,
    "modelKey": "farm_lv1",
    "icon": "farm_lv1",
    "requiredLevel": 1,
    "prerequisiteBuildingId": null,
    "singletonGroupCode": null,
    "ownedCount": 0,
    "maxOwnedCount": null,
    "ownershipLimitMet": true,
    "canBuild": true,
    "cannotBuildReason": ""
  }
]
```

特殊建筑字段语义：

| 字段 | 说明 |
| --- | --- |
| `singletonGroupCode` | 玩家维度单例组编码；普通建筑为 `null` |
| `ownedCount` | 玩家跨全部经营场景已拥有该单例组的建筑数量 |
| `maxOwnedCount` | 特殊建筑固定为 `1`；普通建筑为 `null` |
| `ownershipLimitMet` | 是否仍满足数量限制；会合并到 `canBuild` |

取消尚未完成的建造或拆除已完成的特殊建筑后，实例会被删除，玩家可以重新建造。并发重复建造由业务检查和数据库唯一索引双重拦截，达到上限返回业务码 `1009`。

### 4.2 检查建筑通用建造条件

```http
GET /api/building/check/{playerId}/{buildingId}
Authorization: Bearer <token>
```

是否需要 token：是

路径参数：

| 参数 | 类型 | 说明 |
| --- | --- | --- |
| playerId | number | 玩家 ID |
| buildingId | number | 建筑配置 ID，对应 `building_config.building_id` |

后端处理：

1. 查询 `users`。
2. 查询 `building_config`，校验启用。
3. 查询 `building_level_config` 的 1 级配置。
4. 检查等级、资源、前置建筑。
5. 不检查具体地块类型。

返回 data：`BuildingConditionResponse`

```json
{
  "buildingId": 2001,
  "canBuild": true,
  "levelMet": true,
  "resourceMet": true,
  "prerequisiteMet": true,
  "landMet": true,
  "ownershipLimitMet": true,
  "ownedCount": 0,
  "maxOwnedCount": null,
  "message": "",
  "goldNeeded": null,
  "woodNeeded": null,
  "stoneNeeded": null,
  "ironNeeded": null,
  "requiredLevel": null,
  "missingPrerequisite": null
}
```

### 4.3 检查指定地块是否可建造建筑

```http
GET /api/building/check/{playerId}/{buildingId}/{playerLandId}
Authorization: Bearer <token>
```

是否需要 token：是

路径参数：

| 参数 | 类型 | 说明 |
| --- | --- | --- |
| playerId | number | 玩家 ID |
| buildingId | number | 建筑配置 ID |
| playerLandId | number | 玩家地块实例 ID，对应 `player_land.id` |

后端处理：

除 4.2 的检查外，还会：

1. 查询 `player_land`，确认地块属于该玩家。
2. 查询 `land_config`。
3. 根据 `land_config.allow_build_type` 判断是否允许该建筑的 `build_type`。

返回 data：`BuildingConditionResponse`

### 4.4 开始建造建筑

```http
POST /api/building/build
Content-Type: application/json
Authorization: Bearer <token>
```

是否需要 token：是

前端传参：

```json
{
  "playerId": 1,
  "buildingId": 2001,
  "landId": 1
}
```

参数说明：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| playerId | number | 是 | 玩家 ID，必须等于 token 用户 ID |
| buildingId | number | 是 | 建筑配置 ID |
| landId | number | 是 | 玩家地块实例 ID，即 `player_land.id`，不是 `land_config.land_id`；建筑坐标由服务端根据该地块自动确定 |

后端处理：

1. 校验当前 token 用户。
2. 查询 `users`。
3. 查询 `building_config`，校验启用。
4. 查询 `building_level_config` 的 1 级配置。
5. 查询 `player_land`，确认地块属于玩家。
6. 查询 `land_config`，确认地块允许该建筑类型。
7. 检查等级、资源、前置建筑。
8. 服务端读取该玩家地块的 `position_x/position_y` 作为建筑坐标。
9. 查询 `player_building`，确认目标地块坐标没有已有建筑。
10. 原子扣除 `player_item` 背包资源。
11. 插入不可变 `building_task`，冻结本次实际消耗、退款比例、目标等级、展示字段和产出规则。
12. 插入 `player_building`，状态为 `BUILDING`，并关联 `active_task_id`。
13. 设置 `buildStartTime/buildEndTime/buildDuration`。
14. 如前端需要刷新资源栏，可继续调用 `GET /api/user/me`。

返回 data：`BuildingResponse`

```json
{
  "playerBuildingId": 10,
  "buildingId": 2001,
  "landId": 1,
  "buildingCode": "farm_2001",
  "buildingName": "Farm",
  "buildingType": "RESOURCE",
  "level": 1,
  "produce": "wheat:60",
  "modelKey": "farm_lv1",
  "icon": "farm_lv1",
  "status": "BUILDING",
  "positionX": 0,
  "positionY": 0,
  "buildStartTime": "2026-07-08 16:00:00",
  "buildEndTime": "2026-07-08 16:00:30",
  "remainingTime": 30,
  "progress": 0
}
```

失败情况：

| 场景 | message |
| --- | --- |
| 地块不属于玩家 | 玩家地块不存在 |
| 地块不允许该建筑 | 地块 xxx 不允许建造 xxx |
| 位置已有建筑 | 该位置已有建筑 |
| 资源/等级/前置建筑不足 | 返回具体原因 |
### 4.5 取消建造或取消升级

```http
POST /api/building/cancel
Content-Type: application/json
Authorization: Bearer <token>
```

是否需要 token：是

前端传参：

```json
{
  "playerId": 1,
  "playerBuildingId": 10
}
```

后端处理：

1. 校验 token 用户。
2. 锁定 `player_building` 及其当前 `building_task`。
3. 校验建筑、任务都属于该玩家且关联一致。
4. 只允许状态为 `BUILDING` 或 `UPGRADING`、任务为 `IN_PROGRESS` 的建筑取消。
5. 如果数据库业务时间已经达到或超过任务 `end_time`，拒绝取消；应调用完成接口或同步接口。
6. 按任务快照中的实际扣除和退款比例返还资源，不读取后来修改的等级价格。
7. 将任务标记为 `CANCELLED`，清除活动标记。
8. 如果是 `UPGRADING`：建筑恢复为 `COMPLETED`，继续使用升级前已应用的等级和产出快照。
9. 如果是 `BUILDING`：删除该 `player_building` 记录，但保留任务审计记录。
10. 如前端需要刷新资源栏，可继续调用 `GET /api/user/me`。

返回 data：`BuildingResponse`

注意：取消建造时数据库记录已删除，返回的是取消前的建筑信息，前端应从本地建筑列表移除该建筑，或重新调用同步/列表接口刷新。

### 4.6 完成建造或完成升级

```http
POST /api/building/complete
Content-Type: application/json
Authorization: Bearer <token>
```

是否需要 token：是

前端传参：

```json
{
  "playerId": 1,
  "playerBuildingId": 10
}
```

后端处理：

1. 校验 token 用户。
2. 查询 `player_building` 并校验归属。
3. 只允许 `BUILDING` 或 `UPGRADING` 状态完成。
4. 如果当前时间还没到 `buildEndTime`，拒绝完成。
5. 如果是升级中，`level + 1`。
6. 状态改为 `COMPLETED`，清空建造时间字段。
7. 重置该建筑的生产计时。
8. 创建 `BUILDING_COMPLETED` 站内提醒；玩家 WebSocket 在线时同时推送 `NOTIFICATION_CREATED`。

返回 data：`BuildingResponse`

注意：该接口不是“立即完成”接口。当前时间必须大于等于 `buildEndTime` 才能调用成功。开启 `BUILDING_COMPLETION_REMINDER_ENABLED=true` 后，定时任务会自动完成已经到期的建造/升级任务；未开启时，前端在时间到达后调用本接口或 `POST /api/building/sync/{playerId}` 完成状态同步。

### 4.7 升级建筑

```http
POST /api/building/upgrade
Content-Type: application/json
Authorization: Bearer <token>
```

是否需要 token：是

前端传参：

```json
{
  "playerId": 1,
  "playerBuildingId": 10
}
```

后端处理：

1. 校验 token 用户。
2. 查询 `player_building` 并校验归属。
3. 只有 `COMPLETED` 状态可以升级。
4. 查询下一等级 `building_level_config`。
5. 如果没有下一等级配置，表示已达到最高等级。
6. 原子扣除 `player_item` 背包资源。
7. 状态改为 `UPGRADING`。
8. 设置升级开始时间、结束时间和耗时。

返回 data：`BuildingResponse`

注意：升级开始后返回的 `level` 仍是当前等级；等完成升级或同步到完成后，等级才会加 1。

### 4.8 移动建筑

```http
POST /api/building/move
Content-Type: application/json
Authorization: Bearer <token>
```

是否需要 token：是

前端传参：

```json
{
  "playerId": 1,
  "playerBuildingId": 10,
  "targetLandId": 2
}
```

参数说明：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| playerId | number | 是 | 玩家 ID |
| playerBuildingId | number | 是 | 玩家建筑实例 ID，正整数 |
| targetLandId | number | 是 | 目标玩家地块实例 ID，即 `player_land.id`；目标坐标由服务端根据目标地块自动确定；不能传当前建筑所在地块 |

后端处理：

1. 校验 token 用户。
2. 查询 `player_building` 并校验归属。
3. 只有 `COMPLETED` 状态可以移动。
4. 查询目标 `player_land` 和 `land_config`。
5. 校验目标地块不能和当前建筑所在地块相同。
6. 校验目标地块类型满足建筑 `need_land`，并且 `allow_build_type` 允许该建筑类型。
7. 服务端读取目标地块的 `position_x/position_y` 作为建筑新坐标。
8. 查询目标位置是否已有其他建筑。
9. 更新 `player_building.land_id/position_x/position_y`。

返回 data：`BuildingResponse`
### 4.9 拆除建筑

```http
POST /api/building/demolish
Content-Type: application/json
Authorization: Bearer <token>
```

是否需要 token：是

前端传参：

```json
{
  "playerId": 1,
  "playerBuildingId": 10
}
```

后端处理：

1. 校验 token 用户。
2. 查询 `player_building` 并校验归属。
3. 建造中或升级中的建筑不能拆除。
4. 删除 `player_building` 记录。

返回 data：`null`

注意：当前拆除不返还资源。

### 4.10 获取玩家建筑列表

```http
GET /api/building/list/{playerId}
Authorization: Bearer <token>
```

是否需要 token：是

后端处理：

1. 查询 `users` 表确认玩家存在。
2. 查询 `player_building` 获取玩家全部建筑。
3. 查询 `building_config` 和当前等级 `building_level_config` 组装展示字段。

返回 data：`BuildingResponse[]`

### 4.11 获取建筑详情

```http
GET /api/building/detail/{playerId}/{playerBuildingId}
Authorization: Bearer <token>
```

是否需要 token：是

路径参数：

| 参数 | 类型 | 说明 |
| --- | --- | --- |
| playerId | number | 玩家 ID |
| playerBuildingId | number | 玩家建筑实例 ID |

后端处理：

1. 查询 `player_building`。
2. 校验该建筑属于当前 `playerId`。
3. 查询 `building_config` 和当前等级 `building_level_config`。
4. 返回建筑详情。

返回 data：`BuildingResponse`

### 4.12 同步建筑状态

```http
POST /api/building/sync/{playerId}
Authorization: Bearer <token>
```

是否需要 token：是

后端处理：

1. 查询 `users` 表确认玩家存在。
2. 查询 `player_building` 中状态为 `BUILDING` 或 `UPGRADING` 的记录。
3. 如果当前时间已经达到 `buildEndTime`：
   - `BUILDING` 改为 `COMPLETED`。
   - `UPGRADING` 改为 `COMPLETED`，并且 `level + 1`。
   - 清空建造时间字段。
4. 返回玩家全部建筑列表。

返回 data：`BuildingResponse[]`

## 5. 经营产出接口

经营产出接口用于建筑按服务器时间生产资源，并把收取结果写入玩家背包 `player_item`。当前实现遵循以下规则：

1. `building_level_config` 只负责下一次任务开始时提供候选规则；建筑当前生效规则已冻结在 `player_building` 和 `player_building_production` 快照中。
2. `produce_interval_seconds` 表示生产周期，单位秒；当前初始化数据默认 3600 秒。
3. `storage_capacity` 表示单个建筑每种产物最多暂存数量。
4. 玩家当前待收取数量存放在 `player_building_production.stored_amount`。
5. 建造中建筑不产出；升级中建筑不新增产出，但已结算的待收取数量仍保留。
6. 查询接口会按服务器时间实时计算返回值，但不会把资源直接加到背包，也不会清空待收取数量。
7. 收取会持久化结算结果；升级完成、NPC 入驻或移除会先按旧快照结算，再原子切换新规则版本。
8. Unity 可以本地显示倒计时，但最终产出数量以后端服务器时间计算为准。
9. NPC 入驻建筑只影响资源产出，不影响建造/升级时间；入驻和移除前后端会先结算当前产出并重置生产时钟。
10. 结算会先按快照容量判断是否已满，再计算实际增量；修改或新增等级产出配置不会追溯生成历史资源。

### 5.1 查询全部建筑产出状态

```http
GET /api/production/list/{playerId}
Authorization: Bearer <token>
```

是否需要 token：是

请求体：无。GET 接口不要传 JSON 请求体。

路径参数：

| 参数 | 类型 | 说明 |
| --- | --- | --- |
| playerId | number | 玩家 ID，必须等于登录返回的 `user.id` |

后端处理：

1. 校验 token 用户和 `playerId` 归属。
2. 查询玩家全部建筑。
3. 读取建筑已应用的 `produce_snapshot`、周期、容量和 NPC 加成快照，不查询当前等级配置。
4. 缺少状态行时，只按建筑已应用快照补齐，不从建筑历史更新时间为后来新增的产物追算资源。
5. 按服务器当前时间结算可收取数量，但查询接口不会把资源加到背包。
6. 返回每个建筑、每种产物的产出状态。多产物建筑会返回多条。

返回 data：`ProductionResponse[]`

```json
[
  {
    "playerBuildingId": 10,
    "landId": 1,
    "buildingId": 2001,
    "buildingName": "Farm",
    "level": 1,
    "status": "COMPLETED",
    "itemId": 200000,
    "itemKey": "wheat",
    "itemName": "小麦",
    "icon": "wheat",
    "baseProduceAmount": 60,
    "produceAmount": 72,
    "npcProduceBonusRate": 0.2,
    "settledNpc": {
      "playerNpcId": 2,
      "npcId": 3001,
      "npcCode": "field_steward",
      "name": "田园管家",
      "icon": "Npc_1",
      "portraitKey": "field_steward_portrait",
      "modelKey": "npc_field_steward",
      "description": "擅长农田管理的生产型 NPC",
      "combatAttribute": "WIND",
      "produceBonusRate": 0.2
    },
    "produceIntervalSeconds": 3600,
    "storedAmount": 120,
    "storageCapacity": 500,
    "canCollect": true,
    "lastSettleTime": "2026-07-10 10:00:00",
    "nextProduceTime": "2026-07-10 11:00:00",
    "remainingSeconds": 1800
  }
]
```

字段说明：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| playerBuildingId | number | 玩家建筑实例 ID |
| landId | number | 建筑所在玩家地块实例 ID |
| buildingId | number | 建筑配置 ID |
| status | string | 建筑状态，`COMPLETED` 才会继续新增产出 |
| itemKey | string | 产物标识，对应 `item_config.item_key` |
| baseProduceAmount | number | 建筑等级配置的基础周期产出数量，未计算 NPC 加成 |
| produceAmount | number | 实际每周期产出数量，已计算 NPC 加成 |
| npcProduceBonusRate | number | 当前入驻 NPC 的产出加成比例；无 NPC 时为 0 |
| settledNpc | object / null | 当前入驻 NPC 信息；无 NPC 时为 `null` |
| produceIntervalSeconds | number | 生产周期，单位秒 |
| storedAmount | number | 当前待收取数量 |
| storageCapacity | number | 当前建筑该产物最多暂存数量 |
| canCollect | boolean | 是否有可收取数量 |
| remainingSeconds | number / null | 距离下一次产出的剩余秒数；容量已满或暂停产出时可能为 `null` 或 0 |

### 5.2 查询单个建筑产出状态

```http
GET /api/production/detail/{playerId}/{playerBuildingId}
Authorization: Bearer <token>
```

是否需要 token：是

请求体：无。

路径参数：

| 参数 | 类型 | 说明 |
| --- | --- | --- |
| playerId | number | 玩家 ID，必须等于登录返回的 `user.id` |
| playerBuildingId | number | 玩家建筑实例 ID，对应 `player_building.id` |

后端处理：

1. 校验 token 用户和建筑归属。
2. 查询建筑当前等级产出配置。
3. 初始化或读取该建筑的产出状态。
4. 按服务器当前时间计算当前待收取数量。

返回 data：`ProductionResponse[]`

说明：返回数组是为了兼容一个建筑产出多种物品，例如矿场同时产出 `stone` 和 `iron`。

### 5.3 收取单个建筑资源

```http
POST /api/production/collect
Content-Type: application/json
Authorization: Bearer <token>
```

是否需要 token：是

前端传参：

```json
{
  "playerId": 1,
  "playerBuildingId": 10
}
```

参数说明：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| playerId | number | 是 | 玩家 ID，正整数，必须等于当前 token 用户 ID |
| playerBuildingId | number | 是 | 玩家建筑实例 ID，正整数 |

后端处理：

1. 校验 token 用户和建筑归属。
2. 锁定该建筑产出状态，防止重复点击导致重复收取。
3. 按服务器时间结算最新待收取数量。
4. 将本次收取数量增加到 `player_item`。
5. 将已收取产物的 `storedAmount` 清零。
6. 返回本次收取物品、该建筑最新产出状态和最新背包列表。

返回 data：`ProductionCollectResponse`

```json
{
  "playerId": 1,
  "collectedItems": [
    {
      "itemId": 200000,
      "itemKey": "wheat",
      "name": "小麦",
      "quantity": 120
    }
  ],
  "productions": [
    {
      "playerBuildingId": 10,
      "itemKey": "wheat",
      "storedAmount": 0,
      "storageCapacity": 500,
      "canCollect": false
    }
  ],
  "items": [
    {
      "itemId": 200000,
      "itemKey": "wheat",
      "name": "小麦",
      "itemType": "RESOURCE",
      "icon": "wheat",
      "quantity": 120
    }
  ]
}
```

### 5.4 一键收取全部建筑资源

```http
POST /api/production/collect-all
Content-Type: application/json
Authorization: Bearer <token>
```

是否需要 token：是

前端传参：

```json
{
  "playerId": 1
}
```

参数说明：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| playerId | number | 是 | 玩家 ID，正整数，必须等于当前 token 用户 ID |

后端处理：

1. 校验 token 用户。
2. 锁定玩家全部建筑产出状态。
3. 按服务器时间逐个建筑结算可收取数量。
4. 按 `itemKey` 汇总收取数量，不按 `buildingType` 汇总。
5. 将汇总数量增加到 `player_item`。
6. 清零已收取产物的 `storedAmount`。
7. 返回本次汇总收取结果、所有建筑产出状态和最新背包列表。

返回 data：`ProductionCollectResponse`

### 5.5 查询 NPC 图鉴配置列表

```http
GET /api/npc/config/list
Authorization: Bearer <token>
```

是否需要 token：是

请求体：无。GET 接口不要传 JSON 请求体。

后端处理：

1. 校验 token。
2. 查询 `npc_config` 中 `enabled = true` 的 NPC 配置；`obtainable=false` 的已停获 NPC 仍可展示并服务已有玩家。
3. 按 `sortOrder`、`npcId` 升序返回 NPC 图鉴展示、战斗属性、等级上限和实时产出加成配置。

返回 data：`NpcConfigResponse[]`

```json
[
  {
    "npcId": 3001,
    "npcCode": "field_steward",
    "name": "田园管家",
    "icon": "Npc_1",
    "portraitKey": "field_steward_portrait",
    "modelKey": "npc_field_steward",
    "description": "擅长农田管理的生产型 NPC",
    "combatAttribute": "WIND",
    "maxLevel": 100,
    "produceBonusRate": 0.2,
    "obtainable": true
  }
]
```

字段说明：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| npcId | number | NPC 配置 ID，对应 `npc_config.npc_id` |
| npcCode | string | NPC 配置编码 |
| name | string | NPC 名称 |
| icon | string | Unity 图标资源 Key |
| portraitKey | string / null | Unity 角色立绘绑定键；存量未配置时允许为空 |
| modelKey | string | Unity 模型资源 Key |
| description | string / null | NPC 展示说明 |
| combatAttribute | string | `PHYSICAL/FIRE/ICE/ROCK/WIND/POISON/STAR/DARK`；分别为物理、炎、冰、岩、风、毒、星、暗 |
| maxLevel | number | 当前 NPC 等级上限，Unity 升级按钮必须以该值为准 |
| produceBonusRate | number | 资源产出加成比例，例如 `0.2` 表示 +20% |
| obtainable | boolean | 是否允许抽卡、签到等新渠道继续发放；不代表玩家已拥有 NPC 是否可用 |

### 5.6 查询玩家拥有的全部 NPC

```http
GET /api/npc/list/{playerId}
Authorization: Bearer <token>
```

是否需要 token：是

请求体：无。GET 接口不要传 JSON 请求体。

路径参数：

| 参数 | 类型 | 说明 |
| --- | --- | --- |
| playerId | number | 玩家 ID，必须等于当前 token 用户 ID |

后端处理：

1. 校验 token 用户和 `playerId` 归属。
2. 查询 `users` 表确认玩家存在。
3. 查询 `player_npc`，关联 `npc_config` 返回玩家拥有的全部已启用 NPC。
4. 返回 NPC 实例 ID、配置 ID、状态、当前入驻建筑和实时产出加成。

返回 data：`NpcResponse[]`

```json
[
  {
    "playerNpcId": 2,
    "npcId": 3001,
    "npcCode": "field_steward",
    "name": "田园管家",
    "icon": "Npc_1",
    "portraitKey": "field_steward_portrait",
    "modelKey": "npc_field_steward",
    "description": "擅长农田管理的生产型 NPC",
    "combatAttribute": "WIND",
    "maxLevel": 100,
    "level": 1,
    "state": "IDLE",
    "settledPlayerBuildingId": null,
    "produceBonusRate": 0.2
  }
]
```

`NpcResponse` 新增字段与图鉴接口含义一致：`description` 用于详情展示，`combatAttribute` 用于战斗属性图标，`maxLevel` 用于升级 UI 上限。玩家拥有列表不返回 `obtainable`；停获只阻止新发放，不影响已经拥有的 NPC。

说明：前端安排 NPC 入驻时传的是 `playerNpcId`，也就是 `player_npc.id`；不是 `npc_config.npc_id`。

### 5.7 查询建筑可入驻 NPC

```http
GET /api/npc/available/{playerId}?playerBuildingId=10
Authorization: Bearer <token>
```

是否需要 token：是

请求体：无。GET 接口不要传 JSON 请求体。

路径和查询参数：

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| playerId | number | 是 | 玩家 ID，正整数，必须等于当前 token 用户 ID |
| playerBuildingId | number | 是 | 玩家建筑实例 ID，正整数，对应 `player_building.id` |

后端处理：

1. 校验 token 用户和建筑归属。
2. 建筑必须是 `COMPLETED`。
3. 如果建筑已有 NPC 入驻，返回业务错误。
4. 查询 `player_npc` 中当前玩家 `IDLE` 且 `settled_player_building_id IS NULL` 的 NPC，并关联 `npc_config` 返回当前实时加成配置。

返回 data：`NpcResponse[]`

```json
[
  {
    "playerNpcId": 2,
    "npcId": 3001,
    "npcCode": "field_steward",
    "name": "田园管家",
    "icon": "Npc_1",
    "portraitKey": "field_steward_portrait",
    "modelKey": "npc_field_steward",
    "description": "擅长农田管理的生产型 NPC",
    "combatAttribute": "WIND",
    "maxLevel": 100,
    "level": 1,
    "state": "IDLE",
    "settledPlayerBuildingId": null,
    "produceBonusRate": 0.2
  }
]
```

### 5.8 NPC 入驻建筑

```http
POST /api/production/npc/settle
Content-Type: application/json
Authorization: Bearer <token>
```

是否需要 token：是

前端传参：

```json
{
  "playerId": 1,
  "playerBuildingId": 10,
  "playerNpcId": 2
}
```

参数说明：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| playerId | number | 是 | 玩家 ID，正整数，必须等于当前 token 用户 ID |
| playerBuildingId | number | 是 | 玩家建筑实例 ID，正整数 |
| playerNpcId | number | 是 | 玩家 NPC 实例 ID，正整数，对应 `player_npc.id`，不是 `npc_config.npc_id` |

后端处理：

1. 校验 token 用户、建筑归属和 NPC 归属。
2. 建筑必须是 `COMPLETED`，且当前没有 NPC 入驻。
3. NPC 必须是 `IDLE`，且对应 `npc_config.enabled = true`。
4. 先按入驻前规则结算当前建筑已有产出到 `player_building_production.stored_amount`。
5. 重置该建筑生产时钟为当前服务器时间，避免半个生产周期被错误套用新加成。
6. 写入 `player_building_npc` 当前生效关系，不保存加成快照。
7. 更新 `player_npc.state = SETTLED` 和 `settled_player_building_id`。
8. 返回该建筑最新产出状态。

返回 data：`ProductionResponse[]`

说明：NPC 加成实时读取 `npc_config.produce_bonus_rate`，`produceAmount = baseProduceAmount * (1 + produceBonusRate)`，向下取整。

### 5.9 移除建筑 NPC

```http
POST /api/production/npc/remove
Content-Type: application/json
Authorization: Bearer <token>
```

是否需要 token：是

前端传参：

```json
{
  "playerId": 1,
  "playerBuildingId": 10
}
```

参数说明：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| playerId | number | 是 | 玩家 ID，正整数，必须等于当前 token 用户 ID |
| playerBuildingId | number | 是 | 玩家建筑实例 ID，正整数 |

后端处理：

1. 校验 token 用户和建筑归属。
2. 建筑必须是 `COMPLETED`，且当前已有 NPC 入驻。
3. 先按当前 NPC 加成结算已有产出。
4. 重置该建筑生产时钟为当前服务器时间。
5. 关闭 `player_building_npc.active`，记录 `remove_time`。
6. 更新 `player_npc.state = IDLE`，清空 `settled_player_building_id`。
7. 返回该建筑最新产出状态。

返回 data：`ProductionResponse[]`

### 5.10 失败情况

| 场景 | message |
| --- | --- |
| 未登录或 token 过期 | 未登录或登录已过期 |
| playerId 与 token 不一致 | 不能访问其他玩家的数据 / 不能操作其他玩家的数据 |
| 建筑不存在 | 建筑不存在 |
| 建筑不属于该玩家 | 不能操作其他玩家的数据 |
| 产物配置缺失 | 产物配置不存在：xxx |
| 产出配置格式错误 | 建筑产出配置格式错误：xxx |
| 建筑已有 NPC 入驻 | 该建筑已有NPC入驻 |
| 建筑没有 NPC 入驻 | 该建筑没有NPC入驻 |
| NPC 不属于该玩家或不存在 | 玩家NPC不存在 |
| NPC 不是空闲状态 | NPC当前不是空闲状态 |
| 建筑不是已完成状态 | 只有已完成的建筑可以入驻NPC |
| 有 NPC 入驻时升级建筑 | 建筑有NPC入驻，请先移除NPC再升级 |
## 6. 激励视频广告接口

激励视频广告接口用于“资源不足时看广告得资源”。当前后端采用三步流程：`prepare` 创建观看记录，前端完整观看后调用 `complete` 获取一次性 `claimToken`，最后调用 `claim` 发放奖励。开发环境默认不强制可信完成凭证；生产环境应开启可信凭证校验，没有可信广告回调服务时不要开启生产广告奖励。

统一规则：

1. 所有广告接口都需要 `Authorization: Bearer <token>`。
2. 广告奖励总开关由 `app.ad.reward-enabled` 控制，对应环境变量 `AD_REWARD_ENABLED`；当前默认关闭。
3. `prepare` 只允许前端传 `playerId/scene`，不能传奖励物品和数量，也不会返回 `claimToken`。
4. 前端只能在微信激励视频 `onClose` 且 `isEnded=true` 后调用 `complete`，并传 `completed=true`。
5. `complete` 会校验观看记录属于当前玩家、记录未过期、仍为待领取状态，并且从 `prepare` 到 `complete` 达到 `minWatchSeconds`。
6. `claim` 只接收 `playerId/watchId/claimToken`；`claimToken` 必须来自 `complete`，不再接收客户端的 `completed` 字段。
7. 奖励物品、数量、每日次数、冷却时间、有效期、最短观看时长和广告位 ID 全部来自 `ad_reward_config`。
8. 生产环境设置 `AD_COMPLETION_PROOF_REQUIRED=true` 和 `AD_COMPLETION_PROOF_SECRET` 后，`complete` 还必须携带可信广告服务生成的 `serverProof`。
9. `serverProof` 的签名原文是 `playerId:watchId`，使用 `AD_COMPLETION_PROOF_SECRET` 计算 HMAC-SHA256 并编码为小写十六进制；Unity / 微信小游戏客户端不能自行生成。
10. 三步流程能防重复领取、超时领取、跨玩家领取、跳过 `complete` 直接领取和过快确认；可信凭证用于补充广告服务端验真，不能用客户端回调替代。

默认资源不足场景：

| scene | 奖励 | 默认每日次数 | 默认冷却 | 默认有效期 | 默认最短观看 |
| --- | --- | --- | --- | --- | --- |
| `GOLD_SHORTAGE` | `gold +200` | 5 | 300 秒 | 300 秒 | 25 秒 |
| `WOOD_SHORTAGE` | `wood +100` | 5 | 300 秒 | 300 秒 | 25 秒 |
| `STONE_SHORTAGE` | `stone +80` | 5 | 300 秒 | 300 秒 | 25 秒 |
| `IRON_SHORTAGE` | `iron +50` | 5 | 300 秒 | 300 秒 | 25 秒 |

### 6.1 查询激励视频奖励配置

```http
GET /api/ad/reward/config
Authorization: Bearer <token>
```

返回 data：`AdRewardConfigResponse[]`

```json
[
  {
    "scene": "WOOD_SHORTAGE",
    "adUnitId": "rewarded_video_wood_shortage",
    "rewardItemKey": "wood",
    "rewardAmount": 100,
    "dailyLimit": 5,
    "cooldownSeconds": 300,
    "expireSeconds": 300,
    "minWatchSeconds": 25
  }
]
```

### 6.2 准备观看激励视频

```http
POST /api/ad/reward/prepare
Content-Type: application/json
Authorization: Bearer <token>
```

前端传参：

```json
{
  "playerId": 1,
  "scene": "WOOD_SHORTAGE"
}
```

返回 data：`AdRewardPrepareResponse`

```json
{
  "watchId": "AD20260713103000A1B2C3D4",
  "scene": "WOOD_SHORTAGE",
  "adUnitId": "rewarded_video_wood_shortage",
  "rewardItemKey": "wood",
  "rewardAmount": 100,
  "dailyLimit": 5,
  "cooldownSeconds": 300,
  "expireSeconds": 300,
  "minWatchSeconds": 25,
  "remainingSeconds": 300,
  "expireTime": "2026-07-14 10:35:00"
}
```

### 6.3 确认完整观看

```http
POST /api/ad/reward/complete
Content-Type: application/json
Authorization: Bearer <token>
```

前端传参：

```json
{
  "playerId": 1,
  "watchId": "AD20260713103000A1B2C3D4",
  "completed": true,
  "serverProof": "生产环境由可信广告服务生成的HMAC-SHA256十六进制值"
}
```

开发环境关闭强制校验时可以省略 `serverProof`；生产环境必须由可信广告回调服务生成，不能由前端根据密钥计算。

返回 data：`AdRewardCompleteResponse`

```json
{
  "watchId": "AD20260713103000A1B2C3D4",
  "claimToken": "7F0C2E8D4B6A4D8C9E0A1B2C3D4E5F6A",
  "scene": "WOOD_SHORTAGE",
  "rewardItemKey": "wood",
  "rewardAmount": 100,
  "minWatchSeconds": 25,
  "completeTime": "2026-07-14 10:30:28",
  "expireTime": "2026-07-14 10:35:00",
  "remainingSeconds": 272
}
```

### 6.4 领取激励视频奖励

```http
POST /api/ad/reward/claim
Content-Type: application/json
Authorization: Bearer <token>
```

前端传参：

```json
{
  "playerId": 1,
  "watchId": "AD20260713103000A1B2C3D4",
  "claimToken": "7F0C2E8D4B6A4D8C9E0A1B2C3D4E5F6A"
}
```

返回 data：`AdRewardClaimResponse`

```json
{
  "playerId": 1,
  "watchId": "AD20260713103000A1B2C3D4",
  "scene": "WOOD_SHORTAGE",
  "rewardItemKey": "wood",
  "rewardAmount": 100,
  "items": [
    {
      "itemId": 2,
      "itemKey": "wood",
      "name": "木材",
      "itemType": "RESOURCE",
      "icon": "wood",
      "quantity": 5100
    }
  ]
}
```

### 6.5 失败情况

| 场景 | message |
| --- | --- |
| 广告总开关关闭 | 广告奖励功能暂未开启 |
| scene 未配置或未启用 | 广告奖励配置不存在或未启用：xxx |
| 今日次数已用完 | 今日广告奖励次数已用完 |
| 冷却中 | 广告奖励冷却中，剩余xx秒 |
| watchId 不存在 | 广告观看记录不存在 |
| 未完整观看 | 激励视频未完整观看，不能领取奖励 |
| 观看时间过短 | 激励视频观看时间不足 |
| 生产环境缺少凭证 | 广告完成凭证服务未配置，暂不能领取奖励 |
| 凭证为空 | 缺少可信广告完成凭证 |
| 凭证无效 | 广告完成凭证无效 |
| claimToken 为空 | 广告领取令牌不能为空 |
| claimToken 不匹配 | 广告领取令牌无效，请重新观看广告 |
| 重复领取 | 广告奖励已领取 |
| 记录过期 | 广告观看记录已过期 |
| 并发或重复提交导致状态变化 | 广告奖励状态已变化，请刷新后重试 |
| 奖励物品未配置或禁用 | 广告奖励物品配置不存在：xxx |
## 7. 支付接口

支付接口用于微信小游戏内购买商品。当前实现包含：支付商品列表、本地订单幂等创建、事务外并发预支付租约、微信支付 API v3 JSAPI 下单、支付及退款通知验签解密、商户主动查单和过期关单、服务端发货、退款奖励追回与经济冻结审计。

渠道边界：当前实现只适用于普通微信支付 API v3 JSAPI，使用微信支付平台证书模式验签；不实现 `wx.requestMidasPayment` 米大师虚拟支付或微信支付公钥 `PUB_KEY_ID_...` 验签模式。正式启用收款或退款前必须确认小游戏获准使用的支付渠道和验签模式；如果使用米大师，不能复用本模块的普通商户支付和退款 API。

统一规则：

1. 商品列表、创建订单、查询订单需要 `Authorization: Bearer <token>`。
2. 微信支付回调 `POST /api/payment/notify/wechat` 不需要业务 token，由微信服务器调用。
3. 支付总开关由 `PAYMENT_ENABLED` 控制；关闭时商品列表返回空数组，创建订单拒绝。
4. `WECHAT_PAY_ENABLED=false` 是本地订单测试模式，只创建本地订单，`payReady=false`，不会发货；即使微信支付开关已开启，已支付、已关闭或过期的订单重放也可能返回 `payReady=false`，客户端不能仅凭该值推断为测试模式。
5. `WECHAT_PAY_ENABLED=true` 是真实微信支付模式，必须配置 `WECHAT_PAY_APP_ID`、`WECHAT_PAY_MCH_ID`、`WECHAT_PAY_MERCHANT_SERIAL_NO`、`WECHAT_PAY_MERCHANT_PRIVATE_KEY_PATH`、`WECHAT_PAY_API_V3_KEY`、`WECHAT_PAY_NOTIFY_URL`。
6. 前端创建订单只能传 `playerId/clientOrderId/productCode`，不能传价格、奖励物品或奖励数量。
7. `clientOrderId` 是客户端生成且最多 64 字符的幂等 ID，同一玩家同一 ID 的网络重试必须保持相同商品，只返回同一笔订单；新的一次购买才生成新 ID，不得把旧 ID 用于其他商品。
8. 商品价格、币种、奖励物品和奖励数量全部来自 `payment_product_config`，创建订单时会写入 `payment_order` 快照。
9. V14 起只启用钻石充值商品；V153 起管理员可新增、调整或停用档位，金额为大于零的 10 分整数倍，固定 `CNY` 和 `diamond`，到账钻石由服务端按 `priceFen / 10` 计算。调整生成新商品编码并停用旧版本；历史金币、资源和旧档位商品仅停用、不删除，已有订单仍按订单快照完成支付生命周期。Unity 必须从最新商品列表读取编码，不能硬编码初始化的 6 档。
10. 订单有效期由 `PAYMENT_ORDER_EXPIRE_MINUTES` 控制，默认 30 分钟；查询或创建订单时会自动关闭已过期未支付订单。
11. 同一订单的并发请求先用短事务领取 `CREATING` 租约，在事务外调用微信，再用短事务提交 `READY` 或 `UNKNOWN`；远程调用不占用数据库连接和订单行锁。
12. 发货只在微信回调验签解密成功，或商户主动查单的微信支付 API v3 响应验签成功且确认 `SUCCESS` 后，由同一个可信交易事务完成；缺少或错误的响应签名不能触发发货，客户端不能直接加资源。
13. `PAYMENT_RECONCILIATION_ENABLED=true` 时，回调丢失会由 `payment_reconciliation_task` 按有界批次和数据库租约主动查单；到期 `NOTPAY` 订单先调用微信关单，再关闭本地订单。
14. `PAYMENT_DELIVERY_MODE=sync` 时在当前进程内同步发货；`PAYMENT_DELIVERY_MODE=redis-stream` 时写入 `payment_delivery_task` 并发布 `payment:events`，由消费者幂等发货和补偿。
15. 如果微信确认支付时间晚于订单 `expireTime`，订单标记为 `MANUAL_REVIEW`，不会自动发货；需要运营核对支付并按实际情况退款或人工补发。
16. 支付成功后对账任务默认继续监控 365 天；发现 `REFUND.SUCCESS` 时，未发货订单取消任务，已发货订单追回奖励，余额不足则记录欠款并冻结资源消耗。
17. `PAYMENT_REFUND_ENABLED=true` 开启玩家申请和审核通过后的主动全额退款任务；运营审核接口另要求 `OPERATOR_API_ENABLED=true` 并配置独立运营账号。正式环境必须同时准备好审核能力后再开放玩家申请。
18. V15 主动退款只接受 `PAID/DELIVERED` 的钻石订单。退款金额、币种和应追回钻石数量全部来自锁定的 `payment_order` 快照，客户端和运营端都不能提交。
19. 审核通过后订单进入 `REFUNDING`，所有背包资源消费通过统一经济守卫拒绝；微信远程调用在数据库事务外执行，数据库租约防止多实例重复调用。
20. 微信全额退款成功后，待发货任务进入不可逆 `CANCELLED`；发布器、消费者和失败恢复任务都不能把它恢复为可发货状态。

V153/V154 的管理端充值档位接口和发布规则见文末“V153/V154：充值档位管理与 Unity 接入”章节；管理端列表每页 9 档只是前端展示分页，玩家商品目录接口仍返回全部当前在售档位。

### 7.1 查询支付商品列表

```http
GET /api/payment/product/list
Authorization: Bearer <token>
```

是否需要 token：是

前端传参：无

后端处理：

1. 校验 token。
2. 如果 `PAYMENT_ENABLED=false`，直接返回空数组，前端应隐藏充值入口。
3. 查询 `payment_product_config.enabled = true` 的商品配置。
4. 关联 `item_config`，只返回奖励物品仍启用的商品。
5. 按 `sort_order` 和 `id` 排序返回。

返回 data：`PaymentProductResponse[]`

```json
[
  {
    "productCode": "DIAMOND_60",
    "name": "60钻石",
    "priceFen": 600,
    "currency": "CNY",
    "rewardItemKey": "diamond",
    "rewardItemName": "钻石",
    "rewardIcon": "diamond",
    "rewardAmount": 60
  }
]
```

`DIAMOND_60` 仅是 V14 初始化数据的示例。运营调整档位后商品编码会变化；Unity 每次打开充值页都应使用本接口当前返回的 `productCode`、价格和到账钻石。

### 7.2 创建支付订单

```http
POST /api/payment/order/create
Content-Type: application/json
Authorization: Bearer <token>
```

是否需要 token：是

前端传参：

```json
{
  "playerId": 1,
  "clientOrderId": "unity-20260713-000001",
  "productCode": "<本次商品列表选中的 productCode>"
}
```

参数说明：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| playerId | number | 是 | 玩家 ID，必须等于 token 用户 ID |
| clientOrderId | string | 是 | 客户端订单幂等 ID，最大 64 字符；同一玩家内唯一 |
| productCode | string | 是 | 本次商品列表返回的在售编码，最多 50 字符；不可把初始 `DIAMOND_60` 写死在客户端 |

后端处理：

1. 校验支付总开关 `PAYMENT_ENABLED`。
2. 校验 token 用户和 `playerId`。
3. 查询玩家是否存在。
4. 标记该玩家已过期的未支付订单为 `CLOSED`。
5. 根据 `playerId + clientOrderId` 查询已有订单；如果存在，校验商品一致后直接返回已有订单。
6. 查询启用的支付商品配置，并校验奖励物品在 `item_config` 中仍启用。
7. 创建本地订单 `payment_order`，状态为 `CREATED`，写入价格和奖励快照、`clientOrderId`、`expireTime`。
8. 并发重复创建撞唯一键时，重新按 `clientOrderId` 查询并返回已有订单。
9. 如果 `WECHAT_PAY_ENABLED=false`，返回 `payReady=false` 和空 `payParams`。
10. 如果 `WECHAT_PAY_ENABLED=true`，根据玩家 `user_wechat_account.openid` 调用微信支付 JSAPI 下单，写入 `wx_prepay_id`，订单状态改为 `PAYING`，返回前端调起支付参数。

本地订单测试模式返回 data 示例：

```json
{
  "order": {
    "orderNo": "PAY20260713103000A1B2C3D4",
    "playerId": 1,
    "clientOrderId": "unity-20260713-000001",
    "productCode": "DIAMOND_60",
    "productName": "60钻石",
    "priceFen": 600,
    "currency": "CNY",
    "rewardItemKey": "diamond",
    "rewardAmount": 60,
    "status": "CREATED",
    "paidTime": null,
    "deliveredTime": null,
    "expireTime": "2026-07-13 11:00:00",
    "closeTime": null,
    "createTime": "2026-07-13 10:30:00"
  },
  "paymentProvider": "WECHAT_MINIGAME",
  "payReady": false,
  "payParams": {},
  "message": "订单已创建"
}
```

真实微信支付模式返回 data 示例：

```json
{
  "order": {
    "orderNo": "PAY20260713103000A1B2C3D4",
    "playerId": 1,
    "clientOrderId": "unity-20260713-000001",
    "productCode": "DIAMOND_60",
    "productName": "60钻石",
    "priceFen": 600,
    "currency": "CNY",
    "rewardItemKey": "diamond",
    "rewardAmount": 60,
    "status": "PAYING",
    "paidTime": null,
    "deliveredTime": null,
    "expireTime": "2026-07-13 11:00:00",
    "closeTime": null,
    "createTime": "2026-07-13 10:30:00"
  },
  "paymentProvider": "WECHAT_MINIGAME",
  "payReady": true,
  "payParams": {
    "appId": "wx123456",
    "timeStamp": "1783917600",
    "nonceStr": "85f1d7a4e1e24d8bb85a638a3b0f49aa",
    "package": "prepay_id=wx201410272009395522657a690389285100",
    "signType": "RSA",
    "paySign": "base64-signature"
  },
  "message": "订单已创建，请调起微信支付"
}
```

前端说明：只有 `payReady=true` 且 `payParams` 完整时才能调起微信支付；`paymentProvider=WECHAT_MINIGAME` 单独不能证明可付款。上面的 `DIAMOND_60` 订单响应仅展示初始档位示例，真实请求和响应以当前目录及订单快照为准。微信 SDK 成功、失败或取消回调均不代表钻石最终到账；客户端保存 `orderNo` 并查单，只有 `DELIVERED` 才刷新服务端权威余额，不在本地加钻石。

### 7.3 查询支付订单状态

```http
GET /api/payment/order/{playerId}/{orderNo}
Authorization: Bearer <token>
```

是否需要 token：是

路径参数：

| 参数 | 类型 | 说明 |
| --- | --- | --- |
| playerId | number | 玩家 ID，必须等于 token 用户 ID |
| orderNo | string | 本地订单号，对应 `payment_order.order_no` |

后端处理：

1. 校验 token 用户和 `playerId`。
2. 标记该玩家已过期的未支付订单为 `CLOSED`。
3. 查询该玩家自己的订单。
4. 返回订单状态和商品快照。

返回 data：`PaymentOrderResponse`

```json
{
  "orderNo": "PAY20260713103000A1B2C3D4",
  "playerId": 1,
  "clientOrderId": "unity-20260713-000001",
  "productCode": "DIAMOND_60",
  "productName": "60钻石",
  "priceFen": 600,
  "currency": "CNY",
  "rewardItemKey": "diamond",
  "rewardAmount": 60,
  "status": "DELIVERED",
  "paidTime": "2026-07-13 10:31:00",
  "deliveredTime": "2026-07-13 10:31:02",
  "expireTime": "2026-07-13 11:00:00",
  "closeTime": null,
  "createTime": "2026-07-13 10:30:00"
}
```

### 7.4 微信支付回调

```http
POST /api/payment/notify/wechat
Content-Type: application/json
```

是否需要 token：否

调用方：微信支付服务器。

后端处理：

1. 读取微信支付回调请求头 `Wechatpay-Timestamp/Wechatpay-Nonce/Wechatpay-Signature/Wechatpay-Serial`。
2. 拉取并缓存微信支付平台证书，使用平台证书验证回调签名；未知序列号触发远程刷新的频率由 `WECHAT_PAY_CERT_REFRESH_MIN_INTERVAL_SECONDS` 限制。
3. 使用 `WECHAT_PAY_API_V3_KEY` 对 `resource` 做 AES-GCM 解密。
4. 接受 `TRANSACTION.SUCCESS`、`REFUND.SUCCESS`、`REFUND.CLOSED` 和 `REFUND.ABNORMAL`，事件类型必须与解密后的交易/退款状态一致。
5. 支付成功事件按 `out_trade_no` 锁定订单，校验 AppID、商户号、交易号、金额、币种、payer openid 和 `success_time`。
6. 正常支付写入 `PAID` 并通过唯一发货记录幂等发奖；超时支付进入 `MANUAL_REVIEW`，不自动发货。
7. 退款成功事件锁定订单和退款申请；未发货时写入 `CANCELLED` 发货墓碑，已发货时先核对发货记录与订单奖励快照，再原子扣回订单奖励。
8. 退款奖励余额不足时写入 `payment_refund_recovery` 的未追回金额，并设置 `users.economy_frozen=true`，后续资源消耗被拒绝。
9. 部分退款、金额/币种/商户/订单/交易号不一致、异常状态或发货证据不完整时进入 `MANUAL_REVIEW`，不会自动按全额追回。
10. 重复通知幂等返回；`REFUNDED` 是不可回退终态，晚到支付成功事件不会恢复订单。
11. Nginx 部署模板为该公开回调单独设置限流、30 秒代理超时和 256KB 请求体上限。

主动对账不提供给前端调用。服务端使用商户签名请求 `GET /v3/pay/transactions/out-trade-no/{orderNo}`；`SUCCESS` 与回调共用交易处理器，`REFUND` 进入同一退款追回闭环，过期 `NOTPAY` 先调用微信关单。支付成功后的任务按 `PAYMENT_REFUND_MONITOR_DAYS` 和 `PAYMENT_REFUND_MONITOR_INTERVAL_SECONDS` 持续查单，查询异常按上限退避重试。

Redis Streams 发货任务达到 `PAYMENT_STREAM_MAX_RETRY_COUNT` 后会进入 `FAILED`。如果订单仍为 `PAID`，服务端会在 `PAYMENT_STREAM_FAILED_RECOVERY_DELAY_SECONDS`（默认 21600 秒）后分批恢复为 `PENDING`；退款、人工复核或其他非 `PAID` 订单不会自动恢复。恢复批次由 `PAYMENT_STREAM_FAILED_RECOVERY_BATCH_SIZE` 控制。

成功返回给微信：

```json
{
  "code": "SUCCESS",
  "message": "成功"
}
```

失败返回给微信：

```json
{
  "code": "FAIL",
  "message": "失败"
}
```

### 7.5 玩家申请钻石订单全额退款

```http
POST /api/payment/refund/request
Content-Type: application/json
Authorization: Bearer <player-token>
Idempotency-Key: refund-click-20260721-000001
```

是否需要 token：是，必须使用玩家 token。

前端传参：

```json
{
  "orderNo": "PAY20260721103000A1B2C3D4",
  "reason": "误充值申请退款"
}
```

参数规则：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `Idempotency-Key` | header string | 是 | 最长 64 字符；同一次点击重试必须复用，下一次申请使用新值 |
| orderNo | string | 是 | 最长 64 字符，只能申请 token 玩家自己的订单 |
| reason | string | 是 | 去除和压缩空白后最长 80 字符，符合微信普通商户退款原因限制 |

禁止提交 `playerId/refundAmount/currency/rewardItemKey/rewardAmount`。后端先锁定订单，只接受 `PAID` 或 `DELIVERED` 的钻石订单，再复制价格、币种、奖励和微信交易号快照。申请状态先写为 `PENDING_REVIEW`，此时尚未调用微信退款，也不会冻结玩家消费。

返回 data 示例：

```json
{
  "requestNo": "RF0123456789ABCDEF",
  "orderNo": "PAY20260721103000A1B2C3D4",
  "status": "PENDING_REVIEW",
  "statusMessage": "退款申请等待审核",
  "refundAmountFen": 600,
  "currency": "CNY",
  "rewardItemKey": "diamond",
  "rewardAmount": 60,
  "reason": "误充值申请退款",
  "requestedTime": "2026-07-21 10:35:00",
  "reviewTime": null,
  "successTime": null,
  "updateTime": "2026-07-21 10:35:00"
}
```

### 7.6 玩家查询退款进度

查询单笔：

```http
GET /api/payment/refund/{requestNo}
Authorization: Bearer <player-token>
```

查询当前玩家列表：

```http
GET /api/payment/refund/list?limit=20&beforeId=123
Authorization: Bearer <player-token>
```

`limit` 范围 1 至 100；`beforeId` 是上一页返回的 `nextBeforeId`，第一页不传。玩家只能看到自己的退款申请，响应不暴露微信退款号、内部错误或运营审计内容。

列表返回：

```json
{
  "items": [
    {
      "requestNo": "RF0123456789ABCDEF",
      "orderNo": "PAY20260721103000A1B2C3D4",
      "status": "PROCESSING",
      "statusMessage": "退款处理中，暂时不能消耗资源",
      "refundAmountFen": 600,
      "currency": "CNY",
      "rewardItemKey": "diamond",
      "rewardAmount": 60,
      "reason": "误充值申请退款",
      "requestedTime": "2026-07-21 10:35:00",
      "reviewTime": "2026-07-21 10:40:00",
      "successTime": null,
      "updateTime": "2026-07-21 10:40:01"
    }
  ],
  "nextBeforeId": null
}
```

### 7.7 首次创建运营账号和运营登录

当前已提供 `admin-web` Vue 运营配置台；退款接口仍可用 Apifox/Postman 调用。首次部署只启动一个实例，并临时配置：

```text
PAYMENT_REFUND_ENABLED=true
OPERATOR_API_ENABLED=true
OPERATOR_BOOTSTRAP_ENABLED=true
OPERATOR_BOOTSTRAP_USERNAME=refund-admin
OPERATOR_BOOTSTRAP_PASSWORD=至少12位且不超过72字节的独立高强度密码
OPERATOR_BOOTSTRAP_DISPLAY_NAME=Refund Administrator
OPERATOR_BOOTSTRAP_ROLE=REFUND_ADMIN
```

账号创建成功并验证登录后，必须永久设置 `OPERATOR_BOOTSTRAP_ENABLED=false` 并重启。bootstrap 不覆盖已存在账号，运营密码、运营 token、微信商户密钥都不能交给 Unity。

登录：

```http
POST /api/ops/auth/login
Content-Type: application/json
```

```json
{
  "username": "refund-admin",
  "password": "首次部署配置的独立密码",
  "deviceId": "browser-generated-id-20260723",
  "securityAcknowledged": true
}
```

`deviceId` 是浏览器生成并保存在 `localStorage` 的 16 至 64 字符非秘密标识，只用于审计和新设备提示，不能当作认证因子。`securityAcknowledged` 必须由本次登录明确勾选。

成功返回一次原始运营 token：

```json
{
  "token": "独立运营token",
  "expiresIn": 28800,
  "operator": {
    "id": 1,
    "username": "refund-admin",
    "displayName": "Refund Administrator",
    "role": "REFUND_ADMIN"
  },
  "security": {
    "currentLogin": {
      "loginTime": "2026-07-23 18:30:00",
      "clientIp": "192.168.1.20",
      "clientDescription": "Chrome / Windows"
    },
    "previousLogin": null,
    "newDevice": false
  }
}
```

连续失败默认 5 次后锁定 15 分钟；同一来源 IP 的失败窗口和封禁状态由数据库跨实例共享。每次成功登录轮换该账号唯一 token，数据库只保存 token 的 SHA-256 摘要。运营会话同时受绝对有效期和空闲有效期约束，每次有效请求原子刷新最后活动时间。

查询登录页环境、当前运营账号、修改密码或退出：

```http
GET  /api/ops/auth/context
GET  /api/ops/auth/me
PUT  /api/ops/auth/password
POST /api/ops/auth/logout
Authorization: Bearer <operator-token>
```

`GET /api/ops/auth/context` 不需要 token，返回 `consoleName/consoleVersion/environmentCode/environmentName/production/warning/loginEnabled`。修改密码请求为 `{"currentPassword":"...","newPassword":"..."}`；新密码至少 12 个 Unicode 字符且 UTF-8/BCrypt 输入不超过 72 字节。修改成功会注销该账号全部运营 token，前端必须重新登录。

### 7.8 运营查询退款列表和详情

```http
GET /api/ops/refunds?status=PENDING_REVIEW&limit=20&beforeId=123
GET /api/ops/refunds/{requestNo}
Authorization: Bearer <operator-token>
```

`status` 可不传，也可传 `PENDING_REVIEW/REJECTED/APPROVED/PROCESSING/SUCCESS/FAILED/MANUAL_REVIEW/CLOSED`。详情返回三部分：

1. `refund`：订单金额、币种、奖励、微信交易号/退款号、远程状态、重试次数和审核信息等不可变快照及执行状态。
2. `recovery`：已发货奖励的应追回、已追回、未追回数量；未发货或尚未成功退款时可能为 `null`。
3. `auditLogs`：按 ID 顺序返回玩家、运营、系统和微信产生的每次状态动作。

运营审核前必须对照微信商户平台确认商户号、订单号、交易号、金额、币种和退款状态，不能直接修改支付、退款、发货任务或玩家钻石表。

### 7.9 运营审核通过或拒绝

```http
POST /api/ops/refunds/{requestNo}/approve
POST /api/ops/refunds/{requestNo}/reject
Content-Type: application/json
Authorization: Bearer <operator-token>
```

```json
{
  "reviewNote": "已核对订单、金额、玩家申请和微信交易"
}
```

`reviewNote` 必填，最长 500 字符。`REFUND_REVIEWER` 和 `REFUND_ADMIN` 都能审核。

- 通过：只允许 `PENDING_REVIEW`。同一事务把申请改为 `APPROVED`、订单改为 `REFUNDING`，随后调度器在事务外调用微信；从这时起资源消费返回 `code=6401`。
- 拒绝：只允许 `PENDING_REVIEW`，不调用微信、不修改玩家余额，也不冻结消费。

### 7.10 管理员重试和关闭人工复核

重试仅允许 `REFUND_ADMIN`：

```http
POST /api/ops/refunds/{requestNo}/retry
Content-Type: application/json
Authorization: Bearer <operator-token>
```

```json
{
  "reviewNote": "已核对微信商户平台，重新查询该退款"
}
```

本系统发起且状态为 `FAILED/MANUAL_REVIEW` 的退款才可重试。远程结果不明时复用原商户退款单号先查询；微信已明确 `CLOSED` 时使用新商户退款单号重提。商户侧外部退款不允许自动重试。

关闭仅允许 `REFUND_ADMIN` 且申请必须是 `MANUAL_REVIEW`：

```http
POST /api/ops/refunds/{requestNo}/close
Content-Type: application/json
Authorization: Bearer <operator-token>
```

确认从未退款时：

```json
{
  "resolution": "NO_REFUND_RESTORE_ORDER",
  "reviewNote": "微信商户平台确认没有成功退款，恢复原订单"
}
```

证据仍无法闭环时：

```json
{
  "resolution": "UNRESOLVED_KEEP_FROZEN",
  "reviewNote": "退款证据异常，转线下审计并永久保留经济冻结"
}
```

`NO_REFUND_RESTORE_ORDER` 仅在没有 `refund_id`、成功状态、实际退款金额或外部退款来源证据时可用；恢复原订单后，原 `PAID` 发货任务才能重新排队。`UNRESOLVED_KEEP_FROZEN` 把在线流程改为 `CLOSED`，保留 `users.economy_frozen=true`，并把尚未完成的发货任务写成不可复活的 `CANCELLED` 墓碑；没有无审计的直接解冻接口。

### 7.11 退款状态和并发闭环

| 状态 | 含义 | 玩家消费 |
| --- | --- | --- |
| `PENDING_REVIEW` | 等待运营审核，尚未调用微信 | 允许 |
| `REJECTED` | 运营拒绝 | 允许 |
| `APPROVED` | 已审核，等待远程任务领取 | 拒绝 |
| `PROCESSING` | 微信退款创建/查询中 | 拒绝 |
| `SUCCESS` | 微信全额退款和本地奖励追回/欠款记录均完成 | 余额足够时允许；欠款时永久冻结 |
| `FAILED` | 微信明确 `CLOSED`，原订单状态已恢复 | 允许 |
| `MANUAL_REVIEW` | 部分退款、证据不一致、异常状态或自动结果不明 | 拒绝 |
| `CLOSED` | 管理员按明确结论关闭 | 取决于关闭结论 |

严格规则：

1. 退款金额取 `payment_order.price_fen`，奖励追回取订单 `reward_item_key/reward_amount`，不信任客户端金额，也不直接用回调字段决定应追回钻石。
2. 微信 HTTP 创建和查询都在数据库事务外；事务只领取/提交短租约，同一 `out_refund_no` 支持幂等重试。
3. 所有背包资源消费入口（建造/地块、NPC 升级、装备升级、抽卡等）最终统一经过背包扣除守卫；守卫锁 `users` 并检查活动退款或永久冻结。
4. 外部全额钻石退款在签名、商户、订单、交易号、金额和币种全部匹配时自动闭环；如果支付成功回调先前丢失，可信微信交易号会保存为外部退款证据。
5. 主动查单只看到 `trade_state=REFUND` 时无法证明具体退款金额，因此记录外部申请并先进入 `MANUAL_REVIEW`，不猜测为全额；后续验签通过且商户、订单、交易号、金额和币种全部匹配的明细回调可以补全证据并自动完成该外部全额钻石退款。
6. 发货记录缺失或与订单奖励快照不一致时不自动扣钻石；进入人工复核。
7. `CANCELLED` 发货任务是墓碑状态。发布期间产生的旧 Redis 消息由消费者重新读取数据库后直接 ACK，不能发货。

主要运行配置：

```text
PAYMENT_REFUND_ENABLED=false
PAYMENT_REFUND_BATCH_SIZE=20
PAYMENT_REFUND_LEASE_SECONDS=30
PAYMENT_REFUND_PROCESSING_QUERY_DELAY_SECONDS=30
PAYMENT_REFUND_ERROR_RETRY_DELAY_SECONDS=60
PAYMENT_REFUND_RETRY_BACKOFF_MULTIPLIER=2.0
PAYMENT_REFUND_MAX_RETRY_DELAY_SECONDS=3600
PAYMENT_REFUND_MAX_ATTEMPTS=20
PAYMENT_REFUND_SCHEDULER_INITIAL_DELAY_MS=20000
PAYMENT_REFUND_SCHEDULER_FIXED_DELAY_MS=10000
OPERATOR_API_ENABLED=false
OPERATOR_TOKEN_EXPIRES_IN_SECONDS=28800
OPERATOR_MAX_FAILED_ATTEMPTS=5
OPERATOR_LOCK_MINUTES=15
```

退款租约应长于微信 HTTP 超时；关闭 `PAYMENT_REFUND_ENABLED` 会停止新申请、审核通过和退款调度，但查询历史申请及运营人工拒绝/关闭能力仍按各接口规则工作。

### 7.12 失败情况

| 场景 | message |
| --- | --- |
| 支付总开关关闭 | 支付功能暂未开启 |
| 访问其他玩家订单 | 不能访问其他玩家的数据 / 不能操作其他玩家的支付订单 |
| 商品编码为空 | 商品编码不能为空 |
| clientOrderId 为空 | 客户端订单ID不能为空 |
| clientOrderId 已用于其他商品 | 客户端订单ID已用于其他商品，请重新生成客户端订单ID |
| 玩家未微信登录绑定 | 当前玩家未绑定微信openid，不能发起微信支付 |
| 微信支付配置缺失 | 微信支付AppID/商户号/证书/APIv3密钥/回调地址未配置 |
| 微信支付下单失败 | 微信支付接口请求失败 / 微信支付下单失败：未返回prepay_id |
| 回调签名错误 | 微信支付通知签名验证失败 |
| 回调解密错误 | 微信支付资源解密失败 |
| 回调金额不一致 | 微信支付金额与本地订单不一致 |
| 回调 openid 不一致 | 微信支付openid与本地玩家不一致 |
| 商品不存在或未启用 | 支付商品不存在或未启用：xxx |
| 商品价格配置错误 | 支付商品价格配置错误：xxx |
| 商品奖励数量配置错误 | 支付商品奖励配置错误：xxx |
| 奖励物品未配置或禁用 | 支付商品奖励物品配置不存在：xxx |
| 已有订单已关闭 | 订单已关闭，请重新下单 |
| 支付成功但晚于订单有效期 | Payment requires manual review |
| 订单不存在 | 支付订单不存在 |
| 主动退款未开启 | 微信全额退款功能未启用 |
| 订单不是自己的 | 不能申请其他玩家订单的退款 |
| 订单不是钻石或状态不允许 | V15主动退款只支持钻石充值订单 / 当前支付订单状态不允许退款 |
| 幂等 key 用于另一订单 | 同一幂等请求号不能用于不同支付订单 |
| 运营接口未开启或 token 失效 | 运营接口未启用 / 运营登录已过期 |
| 审核角色不足 | 没有退款审核权限 / 该操作仅退款管理员可执行 |
| 退款期间发起资源消费 | `code=6401`，退款处理中，暂时不能消耗资源 |
| 微信返回部分退款或证据不一致 | 状态进入 `MANUAL_REVIEW`，不自动按全额追回 |

## 8. 通知和微信订阅消息接口

通知模块包含三层：站内提醒用于持久化和历史查询，WebSocket 用于在线即时推送，微信订阅消息用于玩家离线时的外部触达。前端必须先调用微信 `wx.requestSubscribeMessage` 获取玩家授权，再把授权结果保存到后端；WebSocket 不需要微信授权，只需要有效登录 token。

### 8.1 查询站内提醒

```http
GET /api/notification/list/{playerId}?limit=50
Authorization: Bearer <token>
```

路径参数：

| 参数 | 类型 | 说明 |
| --- | --- | --- |
| playerId | number | 玩家 ID，必须等于登录用户 ID |

返回 data：`NotificationResponse[]`

```json
[
  {
    "id": 1001,
    "playerId": 1,
    "type": "RESOURCE_READY",
    "title": "资源可以收取了",
    "content": "你的建筑已经产出资源，回来收取可以继续建设。",
    "payload": "{\"scene\":\"RESOURCE_READY\"}",
    "readFlag": false,
    "readTime": null,
    "createTime": "2026-07-13 12:00:00"
  }
]
```

当前站内提醒类型：

| type | 触发场景 |
| --- | --- |
| `RESOURCE_READY` | 建筑资源达到可收取条件 |
| `OPERATION_NOTICE` | 内部运营接口发送提醒 |
| `PAYMENT_DELIVERED` | 支付订单完成服务端发货 |
| `AD_REWARD_CLAIMED` | 激励视频奖励领取成功 |
| `BUILDING_COMPLETED` | 建筑建造或升级完成 |
| `MAIL_RECEIVED` | 玩家收到新邮件 |

### 8.2 标记单条提醒已读

```http
POST /api/notification/read
Content-Type: application/json
Authorization: Bearer <token>
```

请求体：

```json
{
  "playerId": 1,
  "notificationId": 1001
}
```

### 8.3 标记全部提醒已读

```http
POST /api/notification/read_all
Content-Type: application/json
Authorization: Bearer <token>
```

请求体：

```json
{
  "playerId": 1
}
```

返回 data：本次标记已读的数量。

### 8.4 查询可授权订阅模板

```http
GET /api/notification/subscribe/templates/{playerId}
Authorization: Bearer <token>
```

返回 data：`SubscribeTemplateResponse[]`

```json
[
  {
    "templateType": "RESOURCE_READY",
    "templateId": "微信资源可领取订阅消息模板ID",
    "page": "pages/index/index",
    "enabled": true
  },
  {
    "templateType": "OPERATION_NOTICE",
    "templateId": "微信运营订阅消息模板ID",
    "page": "pages/index/index",
    "enabled": false
  }
]
```

前端拿到 `templateId` 后调用微信 `wx.requestSubscribeMessage({ tmplIds: [templateId] })`。

`RESOURCE_READY` 的 `templateId` 对应管理端“微信资源可领取订阅消息模板 ID”，`OPERATION_NOTICE` 对应“微信运营订阅消息模板 ID”。两者都需要从微信公众平台对应小程序的“功能 → 订阅消息 → 我的模板”中复制，系统不能自行生成。模板关键词默认要求标题、内容和时间分别对应 `thing1`、`thing2`、`time3`；若服务器修改过关键词映射，以部署配置为准。暂不发送对应微信提醒时可以留空，站内提醒不受影响；模板 ID 与 `X-Notification-Admin-Key` 内部接口密钥无关。

### 8.5 保存订阅授权结果

```http
POST /api/notification/subscribe/save
Content-Type: application/json
Authorization: Bearer <token>
```

请求体：

```json
{
  "playerId": 1,
  "templateType": "RESOURCE_READY",
  "templateId": "资源可收取模板ID",
  "accepted": true
}
```

字段说明：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| templateType | string | `RESOURCE_READY` 或 `OPERATION_NOTICE` |
| templateId | string | 微信订阅消息模板 ID |
| accepted | boolean | 用户是否同意授权；拒绝时传 `false` |

### 8.6 内部发送运营提醒

```http
POST /api/notification/operation/send
Content-Type: application/json
X-Notification-Admin-Key: your-random-admin-key
```

该接口不使用玩家 token，只允许 ApiPost、后端管理工具或服务器脚本调用。当前没有默认内部密钥，开发和生产环境都必须配置 `NOTIFICATION_OPERATION_ADMIN_KEY` 真实随机密钥，否则接口不可用。

请求体：

```json
{
  "playerId": 1,
  "title": "限时活动开启",
  "content": "今日登录可领取活动奖励",
  "sendWechat": true
}
```

后端处理：

1. 校验内部运营密钥。
2. 写入 `player_notification` 站内提醒。
3. 如果该玩家保持 WebSocket 在线，立即推送 `NOTIFICATION_CREATED`；`data.id` 与站内提醒记录 ID 相同。
4. 如果 `sendWechat=true` 且玩家已授权 `OPERATION_NOTICE` 模板，则调用微信订阅消息发送接口。
5. 写入 `wechat_subscribe_send_log`，记录成功、失败或跳过原因。

说明：运营提醒接口返回成功表示站内提醒已经写入。WebSocket 是否在线不影响写库，因此即使玩家离线，之后调用 `GET /api/notification/list/{playerId}` 仍然可以查询到该提醒。

### 8.7 离线资源可收取提醒

离线资源提醒没有前端主动调用接口，由后端定时任务自动扫描。触发条件：

1. `RESOURCE_REMINDER_ENABLED=true`。
2. 玩家已授权 `RESOURCE_READY` 模板。
3. 玩家最近活跃时间超过 `RESOURCE_REMINDER_OFFLINE_MINUTES`。
4. 建筑有资源可收取或已达到一个产出周期。
5. 冷却期 `RESOURCE_REMINDER_COOLDOWN_MINUTES` 内没有提醒过。

满足条件后，后端会创建站内提醒，并在 `WECHAT_SUBSCRIBE_ENABLED=true` 时尝试发送微信订阅消息。

多实例部署时，后端通过 `notification_reminder_claim` 的玩家、提醒类型和时间窗口唯一键领取提醒任务，保证同一提醒窗口只由一个实例创建，避免重复发送。

### 8.8 建筑完成自动处理和提醒

建筑到达 `buildEndTime` 后，有三种完成方式：

1. 前端调用 `POST /api/building/complete`。
2. 前端调用 `POST /api/building/sync/{playerId}`，由后端同步所有已到期建筑。
3. 开启建筑完成扫描，由服务端定时自动完成。

自动扫描配置：

```text
BUILDING_COMPLETION_REMINDER_ENABLED=true
BUILDING_COMPLETION_REMINDER_INITIAL_DELAY_MS=30000
BUILDING_COMPLETION_REMINDER_FIXED_DELAY_MS=60000
BUILDING_COMPLETION_REMINDER_BATCH_SIZE=50
```

默认 `BUILDING_COMPLETION_REMINDER_ENABLED=false`。开启后，服务启动 30 秒后进行第一次扫描，之后每 60 秒扫描一次，每批最多处理 50 条已到期任务。扫描只处理 `BUILDING` 或 `UPGRADING` 且 `buildEndTime <= 当前服务器时间` 的建筑，不会提前完成。

完成后会生成如下站内提醒：

```json
{
  "id": 1002,
  "playerId": 1,
  "type": "BUILDING_COMPLETED",
  "title": "建筑建造完成",
  "content": "Farm已建造完成，可以继续经营了。",
  "payload": "{\"scene\":\"BUILDING_COMPLETED\",\"playerBuildingId\":10,\"buildingId\":2001,\"buildingName\":\"Farm\",\"level\":1,\"upgraded\":false}",
  "readFlag": false,
  "readTime": null,
  "createTime": "2026-07-15 10:30:00"
}
```

升级完成时 `title` 为 `建筑升级完成`，`payload.upgraded=true`，`level` 为升级后的等级。玩家 WebSocket 在线时，该对象会作为 `NOTIFICATION_CREATED.data` 同步推送。

### 8.9 WebSocket 在线实时推送

WebSocket 负责在线即时消息。`NOTIFICATION_CREATED` 等持久化提醒仍通过提醒或邮件列表补查；`CARD_DRAW_RARE_BROADCAST` 是临时公屏氛围消息，断线或离线期间不补推，也不产生未读数。

连接地址：

```text
ws://localhost:8080/api/ws/realtime
wss://你的正式域名/api/ws/realtime
```

握手只接受请求头中的登录凭证：

```http
Authorization: Bearer <token>
```

禁止把 token 放进 URL 的 `token` 或 `access_token` 查询参数，避免代理、浏览器历史和访问日志泄露凭证。Unity 或支持自定义握手请求头的调试工具可直接连接；不支持自定义请求头的浏览器原生 WebSocket 需要前端网关提供安全的短期票据方案，不能退回 URL 长期 token。

握手时服务端只把令牌记录 ID 保存到会话，不保存原始 token。每条客户端消息和每次玩家私有推送前都会重新查询令牌状态；退出登录、重新登录、令牌过期或后台禁用后，连接会在下一次心跳或推送前关闭。单条文本消息默认最大 8192 字节，同一连接消息间隔默认至少 200 毫秒，过快会关闭当前连接。

连接成功后，服务端直接发送 WebSocket JSON 消息，不使用 HTTP 的 `code/message/data` 外层结构：

```json
{
  "type": "CONNECTED",
  "requestId": null,
  "eventId": "c8a501a0-65bd-4ee0-9337-a2d3f6f93c9d",
  "playerId": 1,
  "data": {
    "sessionId": "21a737de-06e7-44f7-a049-73b0508d9945"
  },
  "serverTime": "2026-07-15 10:20:00"
}
```

客户端心跳请求：

```json
{
  "type": "PING",
  "requestId": "ping-1"
}
```

`type` 也可以传 `HEARTBEAT`。服务端返回：

```json
{
  "type": "PONG",
  "requestId": "ping-1",
  "eventId": "24b63947-d119-44cb-9d15-28b0885a29e1",
  "playerId": 1,
  "data": {
    "sessionId": "21a737de-06e7-44f7-a049-73b0508d9945"
  },
  "serverTime": "2026-07-15 10:20:05"
}
```

创建任意站内提醒后，在线玩家收到：

```json
{
  "type": "NOTIFICATION_CREATED",
  "requestId": null,
  "eventId": "30b8cdd3-d736-48cf-b64b-bef19a858e8d",
  "playerId": 1,
  "data": {
    "id": 1003,
    "playerId": 1,
    "type": "OPERATION_NOTICE",
    "title": "WebSocket测试",
    "content": "玩家在线时应立即收到此消息",
    "payload": "{\"scene\":\"OPERATION_NOTICE\"}",
    "readFlag": false,
    "readTime": null,
    "createTime": "2026-07-15 10:21:00"
  },
  "serverTime": "2026-07-15 10:21:00"
}
```

Apifox 联调顺序：

1. 登录并取得玩家 token，确认 token 对应的 `playerId`。
2. 新建 WebSocket 请求，连接 `ws://localhost:8080/api/ws/realtime`，并在握手请求头设置 `Authorization: Bearer <登录token>`。
3. 必须先看到 `CONNECTED`，再发送 `{"type":"PING","requestId":"ping-1"}` 并确认收到 `PONG`。
4. 保持该 WebSocket 页面处于连接状态。
5. 新建 HTTP 请求调用 `POST /api/notification/operation/send`，请求头传 `X-Notification-Admin-Key`，请求体中的 `playerId` 必须与 WebSocket token 对应玩家一致。
6. HTTP 返回成功后，WebSocket 应立即出现 `NOTIFICATION_CREATED`。
7. 再调用 `GET /api/notification/list/{playerId}?limit=50`，应能查询到同一个 `data.id`。

测试请求体：

```json
{
  "playerId": 1,
  "title": "WebSocket测试",
  "content": "玩家在线时应立即收到此消息",
  "sendWechat": false
}
```

常见排查：

| 现象 | 检查项 |
| --- | --- |
| 握手失败 | token 是否有效、地址是否包含 `/api/ws/realtime`、服务是否启用 `REALTIME_ENABLED` |
| 收不到 `CONNECTED` | 请求是否真正进入“已连接”状态，是否超过单玩家连接数限制 |
| 能查到站内提醒但 WS 没消息 | WebSocket token 与请求体 `playerId` 是否为同一玩家，连接是否仍打开 |
| 本地单实例收不到 | HTTP 和 WebSocket 是否连接同一个正在运行的后端进程，应用是否已重启到最新代码 |
| 多实例部署收不到 | 设置 `REALTIME_PUSH_MODE=redis_pubsub`，并确认各实例连接同一 Redis 和同一频道 |

实时推送配置：

```text
REALTIME_ENABLED=true
REALTIME_ALLOWED_ORIGIN_PATTERNS=*
REALTIME_MAX_SESSIONS_PER_PLAYER=3
REALTIME_MAX_TEXT_MESSAGE_BYTES=8192
REALTIME_MIN_CLIENT_MESSAGE_INTERVAL_MS=200
REALTIME_PUSH_MODE=local_only
REALTIME_REDIS_PUBSUB_CHANNEL=realtime:push
REALTIME_INSTANCE_ID=
REALTIME_REDIS_PUBSUB_DEDUP_TTL_SECONDS=300
REALTIME_PRESENCE_KEY_PREFIX=realtime:presence
REALTIME_PRESENCE_TTL_SECONDS=90
REALTIME_PRESENCE_REFRESH_INTERVAL_MS=30000
```

本地开发和单实例部署使用 `local_only`。多实例部署使用 `redis_pubsub`：Redis Pub/Sub 负责跨实例消息，Redis `TIME`、ZSET 租约和 Lua 原子脚本负责全局连接上限、续租和释放。Redis 不可用时拒绝新连接，避免突破全局上限；登录、退出和令牌撤销会广播重新校验，只关闭令牌已失效的会话。生产环境必须收紧来源域名。

## 9. 健康检查接口

### 9.1 服务根路径

```http
GET /api
GET /api/
```

是否需要 token：否

返回 data：

```json
{
  "service": "weijun-game-project",
  "status": "UP",
  "health": "/api/health",
  "ping": "/api/health/ping"
}
```

### 9.2 健康检查

```http
GET /api/health
```

是否需要 token：否

返回 data：

```json
{
  "status": "UP",
  "service": "weijun-game-project",
  "timestamp": "2026-07-09 16:00:00",
  "version": "1.0.0"
}
```

### 9.3 心跳检查

```http
GET /api/health/ping
```

是否需要 token：否

返回 data：

```json
"pong"
```

## 10. Unity 调用流程

### 10.1 微信小游戏登录流程

1. 调用 `GET /api/client/runtime-policy`，先处理维护和强制更新。
2. 调用两个 `/api/privacy/policy/current*` 接口读取当前政策元数据和正文。
3. Unity 微信桥接调用微信官方隐私 API；只有玩家主动同意后才生成授权 UUID。
4. 调用 `wx.login` 获取一次性 `code`，再调用 `POST /api/user/wx-login`。
5. HTTP 超时重试时重新获取微信 code，但同一次授权动作复用原 `privacyConsentRequestId`。
6. 后端返回 token 后先判断 `accountManagementOnly`；为 `true` 时进入隐私授权或注销管理页。
7. 仅当 `accountManagementOnly=false` 且 `user != null` 时保存 `playerId=user.id` 并进入游戏。
8. 建立 WebSocket `/api/ws/realtime`，握手请求头设置 `Authorization: Bearer <token>`。
9. 加载地图并在后续全部普通业务请求中携带玩家 token。
### 10.2 进入游戏加载数据

调用：

```http
GET /api/land/map/{playerId}
Authorization: Bearer <token>
```

这个接口一次返回地图大小、已放置地块、已有建筑，适合进入经营区域时初始化客户端场景。

### 10.3 建造地块流程

1. `GET /api/land/available/{playerId}` 获取可放置地块和消耗。
2. Unity 本地预览并检查越界/重叠。
3. 玩家确认后调用 `POST /api/land/place`。
4. 成功后把返回的地块加入本地地图，或重新调用 `GET /api/land/map/{playerId}`。
5. 调用 `GET /api/user/me` 刷新金币、木材、石材、铁矿。

### 10.4 建造建筑流程

1. `GET /api/building/available/{playerId}` 获取建筑列表。
2. 玩家选择地块后调用 `GET /api/building/check/{playerId}/{buildingId}/{playerLandId}`。
3. 条件满足后调用 `POST /api/building/build`。
4. 客户端根据返回的 `buildEndTime/remainingTime/progress` 显示建造进度。
5. 建造、升级、取消等资源变化操作成功后，调用 `GET /api/user/me` 刷新资源栏。
6. 时间到后调用 `POST /api/building/complete`，或者调用 `POST /api/building/sync/{playerId}` 自动同步完成状态。
7. 开启服务端建筑完成扫描时，前端也可以等待 `BUILDING_COMPLETED` 对应的 `NOTIFICATION_CREATED`，收到后刷新建筑列表或地图。


### 10.5 资源栏刷新流程

以下接口会改变玩家资源：

| 操作 | 接口 | 资源变化 |
| --- | --- | --- |
| 放置地块 | `POST /api/land/place` | 扣除地块配置消耗 |
| 开始建造 | `POST /api/building/build` | 扣除 1 级建筑消耗 |
| 取消建造/升级 | `POST /api/building/cancel` | 返还对应消耗的 50% |
| 升级建筑 | `POST /api/building/upgrade` | 扣除下一等级消耗 |
| 收取单个建筑产出 | `POST /api/production/collect` | 增加对应产物到 `player_item` |
| 一键收取建筑产出 | `POST /api/production/collect-all` | 增加汇总产物到 `player_item` |

扣除/返还类接口成功后，Unity 可以统一调用：

```http
GET /api/user/me
Authorization: Bearer <token>
```

用返回的 `gold/wood/stone/iron/diamond` 快捷字段刷新顶部资源栏，或用 `items` 背包列表刷新仓库/背包界面。经营收取接口会直接返回最新 `items`，前端可以优先使用返回值刷新背包。

### 10.6 经营产出流程

1. 进入经营区域后，调用 `GET /api/production/list/{playerId}` 获取所有建筑的待收取数量和倒计时。
2. 玩家点击单个建筑时，调用 `GET /api/production/detail/{playerId}/{playerBuildingId}` 刷新该建筑面板。
3. 玩家收取单个建筑时，调用 `POST /api/production/collect`。
4. 玩家一键收取时，调用 `POST /api/production/collect-all`。
5. 收取成功后，用返回的 `items` 刷新背包/资源栏，用返回的 `productions` 刷新建筑产出状态。
6. Unity 不需要上传本地时间，最终产出数量由后端服务器时间计算。

### 10.7 NPC 图鉴与入驻流程

1. 打开 NPC 图鉴时，调用 `GET /api/npc/config/list` 获取全部启用 NPC 配置。
2. 打开我的 NPC 面板时，调用 `GET /api/npc/list/{playerId}` 获取玩家拥有的 NPC 实例。
3. 打开建筑生产面板时，调用 GET /api/production/detail/{playerId}/{playerBuildingId} 查看当前入驻 NPC 和产出加成。
4. 点击安排 NPC 入驻时，调用 `GET /api/npc/available/{playerId}?playerBuildingId=10` 获取当前建筑可入驻 NPC。
5. 玩家选择 NPC 后，调用 `POST /api/production/npc/settle`。
6. 移除 NPC 时，调用 `POST /api/production/npc/remove`。
7. 入驻或移除成功后，用返回的 ProductionResponse[] 刷新建筑生产面板。

### 10.8 资源不足看广告得资源流程

1. 建造、升级或放置地块接口返回资源不足时，Unity 根据缺少资源类型选择 `scene`，例如木材不足用 `WOOD_SHORTAGE`。
2. 调用 `POST /api/ad/reward/prepare` 获取 `watchId`、`adUnitId`、`minWatchSeconds` 和奖励预览；`prepare` 不返回 `claimToken`。
3. Unity / 微信小游戏端使用返回的 `adUnitId` 创建或复用全局激励视频广告实例，并先 `load()` 再 `show()`。
4. 只有微信广告 `onClose` 回调确认 `isEnded=true` 后，才调用 `POST /api/ad/reward/complete`，传 `playerId/watchId/completed=true`；生产环境还要携带可信广告服务生成的 `serverProof`。
5. `complete` 成功后会返回一次性 `claimToken`；前端再调用 `POST /api/ad/reward/claim`，传 `playerId/watchId/claimToken` 领取奖励。
6. 领取成功后，用返回的 `items` 刷新资源栏/背包，再让玩家重新发起原来的建造、升级或放置操作。
7. 如果广告未完整观看，不调用 `complete` 和 `claim`；记录过期或领取失败时，需要重新调用 `prepare` 获取新的 `watchId`。

### 10.9 支付下单流程

1. 玩家先通过微信登录或绑定微信账号，保存 `data.token` 和 `data.user.id`（支付接口的 `playerId`）。商品列表、建单、查单均带玩家 Bearer token，并同时检查 HTTP 状态和统一响应的 `code == 200`；微信回调由微信服务器调用，Unity 不调用。
2. 每次打开充值页调用 `GET /api/payment/product/list`，按服务端返回的 `name`、`priceFen`（分）、`rewardAmount` 展示在售档位，提交玩家当前选中项的 `productCode`；V153 起调整档位会换编码，不能把 `DIAMOND_60` 等初始化编码写死。旧编码建单被拒绝时刷新目录，让玩家重新确认金额和钻石数量。
3. 玩家点击充值时为本次购买生成并持久保存一个不超过 64 字符的 `clientOrderId`，将 `playerId/clientOrderId/productCode` 提交到 `POST /api/payment/order/create`。网络超时或响应丢失时复用同一 ID 和同一商品重试；新的一次购买才换 ID。拿到响应后持久保存 `data.order.orderNo`，以便重进游戏后恢复查单。订单金额和奖励在建单时冻结，客户端不提交金额或钻石数量。
4. 只有 `data.payReady=true` 且 `data.payParams` 完整时才调起收银台；`payReady=false` 只表示当前不能调起，可能是本地测试、订单已支付、已关闭或已过期，不能只看 `paymentProvider` 猜测原因。Unity C# 通过实际微信小游戏 Unity SDK 的 JavaScript 桥调用宿主 `wx.requestPayment`，传入 `timeStamp/nonceStr/package/signType/paySign`，并核对返回的 `appId` 与当前小游戏一致。仓库不提供通用 Unity 支付桥；商户私钥与签名生成只留在后端。
5. `wx.requestPayment` 的成功、失败、取消回调只用于关闭收银台和提示，不直接修改本地钻石。支付页面返回后及下次进游戏时，用保存的 `orderNo` 对 `GET /api/payment/order/{playerId}/{orderNo}` 做有界查询：`CREATED/PAYING` 未完成，`PAID` 已支付但待发货，只有 `DELIVERED` 表示服务端到账完成；随后调用 `GET /api/user/me` 刷新 `diamond` 和背包。`CLOSED/FAILED/REFUNDED` 不显示为到账，`REFUNDING/MANUAL_REVIEW` 进入退款或人工核对流程。
6. 上述桥接仅适用于已获批普通微信支付 API v3 JSAPI 的小游戏。若实际渠道要求 `wx.requestMidasPayment`，须另建米大师下单、支付和退款链路，不复用本节的 `payParams`。

### 10.10 玩家退款申请流程

1. 玩家在订单详情选择申请退款时，Unity 为本次点击生成新的 `Idempotency-Key`，只提交 `orderNo/reason`；同一次点击重试必须复用原 key，即使订单或申请状态已经推进，服务端仍返回最初创建的申请，不会重复建单或退款。
2. 请求超时或断网重试必须复用相同 key；新一次申请不能复用旧 key。
3. 创建成功后保存 `requestNo`，轮询 `GET /api/payment/refund/{requestNo}`，或进入退款列表时调用 `/list`。
4. `PENDING_REVIEW` 只表示等待审核；`APPROVED/PROCESSING` 时资源消费会被后端拒绝，前端应禁用抽卡、升级等消费按钮并展示状态。
5. `SUCCESS` 后刷新订单和背包；后端可能已追回钻石，余额不足时账号保持经济冻结，前端不能自行扣除或恢复余额。
6. Unity 不调用 `/api/ops/**`；运营人员在可信内网环境使用 Apifox/Postman 或后续管理系统，以独立运营账号审核。

### 10.11 WebSocket 和站内提醒流程

1. 登录成功后使用同一个 token 建立 `/api/ws/realtime` 连接。
2. 收到 `CONNECTED` 后，每隔一段时间发送 `PING`，收到 `PONG` 表示连接正常。
3. 收到 `NOTIFICATION_CREATED` 时，根据 `data.type` 刷新对应模块，例如 `BUILDING_COMPLETED` 刷新建筑、`MAIL_RECEIVED` 刷新邮件。
4. WebSocket 断开后自动重连；重连成功后调用 `GET /api/notification/list/{playerId}` 和邮件列表补查离线期间的数据。
5. App 切到后台、退出登录或 token 失效时关闭连接；重新登录后使用新 token 建立新连接。

## 11. 主要数据表对应关系

| 表 | 用途 | 相关接口 |
| --- | --- | --- |
| users | 用户账号、规范化唯一昵称、昵称版本、等级、累计经验和已对账曲线版本，不保存资源余额 | 登录、注册、改名、玩家基础信息、成长对账 |
| player_nickname_business_policy | 玩家主动改名开关、冷却小时数、保留词和配置版本 | 昵称规则查询、管理端草稿发布 |
| player_nickname_change_record | 每次成功改名的前后昵称、来源、版本和业务时间不可变快照 | 改名审计、问题排查 |
| player_level_curve_state / player_level_config | 当前等级曲线 revision 和只追加的累计经验门槛 | 等级计算、配置发布、缓存刷新 |
| player_experience_record | 建筑、任务、回填和运营调整产生的不可变幂等经验流水 | 经验发放、重放、运营审计 |
| player_progression_reconciliation_task | 冻结曲线版本、玩家 ID 上界、游标、租约、汇总计数和任务状态 | 手动/自动成长对账、页面恢复、失败重试 |
| player_progression_reconciliation_failure | 每个失败玩家的首次/最近错误、尝试次数和解决状态 | 失败明细分页、公平重试、问题排查 |
| item_config | 物品配置，例如金币、木材、小麦 | 背包查询、资源扣除、经营产出 |
| player_item | 玩家背包/仓库物品数量 | 背包查询、资源扣除、资源返还、产出收取 |
| equipment_config | 装备编码、类型、品质、图标、模型、成长曲线和等级上限 | 奖励校验、仓库、升级和穿戴 |
| player_equipment | 玩家逐件装备实例、等级、来源、仓库/穿戴/消耗状态和锁定标记 | 装备发放、升级、穿戴、卸下 |
| upgrade_gold_curve / upgrade_gold_curve_level | NPC/装备每个目标等级的独立金币成本 | NPC 和装备升级 |
| npc_level_attribute_config / npc_breakthrough_config | NPC 每级最终属性和管理端配置的突破点 | NPC 升级、战斗属性 |
| equipment_level_attribute_config / equipment_breakthrough_config | 装备每级属性和管理端配置的同款装备突破数量 | 装备升级、战斗属性 |
| player_npc_equipment | NPC 当前穿戴关系和卸下历史 | 穿戴、卸下、装备面板 |
| npc_upgrade_record / equipment_upgrade_record | 每次升级消耗和等级变化快照 | 升级审计 |
| equipment_upgrade_material_record | 突破实际消耗的装备实例 | 材料审计、防重复消费 |
| equipment_upgrade_resource_cost_record | 装备升级实际扣除的额外仓库物品快照 | 资源消耗审计、物品改名追溯 |
| card_draw_pool / card_draw_option_config / card_draw_reward_config | 版本化可配置卡池、单抽/五连价格和动态奖项权重 | 卡池查询、抽奖 |
| card_draw_batch / card_draw_card_snapshot / card_draw_result | 幂等批次、完整卡池快照和实际发放结果 | 抽奖重试、扣费和奖励审计 |
| sign_in_plan | 签到计划、窗口天数、补签开关/费用和装备领取上限 | 签到概览、普通领取、补签校验 |
| sign_in_reward_config | 每个计划、周期、日期的配置化奖励和 fallback | 签到预览、奖励发放 |
| player_sign_in_record | 普通/补签主记录、权威业务时间和补签费用快照 | 防重复领取、领取审计 |
| player_sign_in_reward_record | 每项配置奖励与实际发放结果快照 | fallback 审计、问题排查 |
| user_wechat_account | 微信 openid 与本地用户绑定 | 微信登录 |
| auth_token | 登录 token | 登录、注册、微信登录、退出、鉴权 |
| player_map | 玩家地图大小 | 地图加载、地块放置、新玩家初始化 |
| land_config | 地块配置、地块消耗、允许建筑类型 | 地块列表、地块放置、建筑地块校验 |
| player_land | 玩家已放置地块 | 地图加载、建筑建造、建筑移动 |
| building_config | 建筑基础配置 | 建筑列表、条件检查、建造/升级/移动 |
| building_level_config | 下一次建造/升级使用的等级候选配置和目标等级经验奖励 | 建筑列表、建造和升级前校验 |
| building_task | 本次实际消耗、退款比例、经验奖励、目标等级、展示和产出规则不可变快照 | 取消、完成、自动扫描和任务审计 |
| player_building | 玩家建筑实例 | 建筑建造、取消、完成、升级、移动、拆除、列表、同步 |
| player_building_production | 待收取数量、结算时间、产量/周期/容量/NPC 加成和规则版本快照 | 产出查询、收取和规则切换 |
| npc_config | NPC 基础配置和实时产出加成比例 | NPC 图鉴列表、NPC 可入驻列表、产出加成计算 |
| player_npc | 玩家拥有的 NPC 实例、等级、状态、当前入驻建筑 | 玩家 NPC 列表、NPC 可入驻列表、NPC 入驻、NPC 移除 |
| player_building_npc | 建筑和 NPC 的当前入驻关系及历史记录 | NPC 入驻、NPC 移除、产出加成计算 |
| ad_reward_config | 激励视频奖励配置，包含场景、广告位、奖励物品、次数和冷却 | 广告配置、准备观看 |
| ad_watch_record | 激励视频观看记录和奖励领取状态 | 准备观看、领取奖励、防重复领取 |
| payment_product_config | 支付商品配置，包含价格、币种、奖励物品和奖励数量 | 支付商品列表、创建支付订单 |
| payment_order | 支付订单和价格/奖励快照 | 创建支付订单、查询订单、回调与主动对账 |
| payment_delivery_record | 支付发货记录，防止重复发货 | 回调或主动查单确认后的发货 |
| payment_delivery_task | 支付发货任务表，Redis Streams 可靠投递和补偿 | 支付成功、异步发货、补偿任务 |
| payment_reconciliation_task | 微信支付主动查单、支付后退款监控、租约和重试状态 | 回调丢失补偿、多实例查单、退款发现、过期关单 |
| payment_refund_recovery | 已发货退款的奖励追回、未追回欠款和处理状态 | 退款审计、经济冻结和人工恢复 |
| payment_refund_request | 一单一退款申请、订单/奖励/微信证据快照、远程租约和状态机 | 玩家申请/查询、运营审核/重试/关闭 |
| payment_refund_audit_log | 玩家、运营、系统和微信产生的追加式状态审计 | 运营退款详情、责任追踪 |
| operator_account | 独立运营账号、BCrypt 密码、角色、登录锁定和密码修改时间 | 运营登录、退款及配置权限控制 |
| operator_token | 单账号独立 token 摘要、绝对有效期和最后活动时间 | `/api/ops/**` 鉴权、登录轮换、空闲失效和退出 |
| operator_login_guard / operator_login_audit_log | 跨实例登录限流和不可变登录审计 | 运营登录安全、设备变化提示和清理 |
| operator_security_audit_log | 密码修改等安全事件的追加审计 | 修改密码和责任追踪 |
| operator_config_lock / operator_config_release | 配置键串行锁、唯一草稿和不可变正式版本 | 配置读取、保存、发布和回滚 |
| operator_config_audit_log | 配置创建草稿、保存、发布、丢弃和回滚审计 | 配置操作审计查询 |
| config_refresh_outbox | 配置事务内写入、事务外通过 Redis 发布的可靠刷新事件 | 多节点等级曲线缓存刷新 |
| player_task_config / player_task | 日常/周常任务及永久场景成就定义，保存目标、奖励与场景/物品快照 | 任务概览、累计收获、场景开辟里程碑和奖励领取 |
| player_task_progress_event | 可信业务来源的幂等任务进度流水；场景成就改用当前地块权威状态对账 | 建筑完成、建筑升级和指定资源收获进度 |
| player_friend | 以较小玩家 ID 在前保存的一条无向好友关系，并记录建立关系的申请来源 | 申请同意、好友删除和等级排序列表 |
| player_friend_request_policy | 好友容量、待处理容量、北京时间每日额度、冷却和有效期策略 | 管理端好友申请策略发布、申请摘要 |
| player_friend_request | 好友申请、处理终态、过期状态和关系终止时间 | 收到/发出列表、同意、拒绝、取消、过期扫描 |
| player_friend_gift_record | 游戏好友每日赠礼的奖励及余额不可变快照 | 每日赠礼幂等、发送和接收额度 |
| wechat_friend_gift_config | 微信 MP 礼包 ID、类型、每日领取上限、发送开关和配置版本 | 微信赠礼目录、运营配置、回调发货 |
| wechat_friend_gift_reward | 微信道具 ID/数量到游戏内物品 ID/数量的不可变映射 | 回调道具校验、背包资源发放 |
| wechat_gift_delivery_record | 微信 `OrderId`、平台道具、游戏奖励和接收玩家的幂等快照 | 官方好友赠礼与送礼任务礼包发货审计 |
| idle_adventure_mode_config 及子配置表 | 版本化玩法、技能、状态、敌人、关卡、体力、固定奖励、随机池和克制 | Idle Adventure 28 条 Unity 接口与管理端完整版本发布 |
| player_idle_adventure_stamina | 玩家体力余额、恢复策略快照、惰性恢复锚点和乐观版本 | 首次满体力、查询倒计时、开始/继续/重试原子扣除 |
| idle_adventure_stage_drop_pool_config / idle_adventure_stage_drop_reward_config | 版本化关卡随机池、抽取次数、发放范围和类型化候选概率 | 管理端配置 `ITEM / EQUIPMENT / NPC` 必中奖掉落 |
| idle_adventure_drop_batch / idle_adventure_drop_result | 每次通关抽取批次、随机数、命中区间和资产快照 | 随机结果审计、幂等重放和问题对账 |
| player_idle_adventure_* / idle_adventure_session* | 玩家编队、进度、会话、命令、通用待结算奖励和挂机快照 | Idle Adventure 战斗、类型化发奖和挂机流程 |
| player_idle_adventure_afk_acceleration_claim | 挂机加速请求哈希、体力前后值、自然/加速/合并奖励和玩法版本快照 | 加速幂等重放、奖励审计和失败对账 |
| player_notification | 玩家站内提醒 | 提醒列表、已读、运营提醒、资源提醒 |
| player_subscribe_setting | 玩家微信订阅消息授权设置 | 查询模板、保存授权结果、发送前校验 |
| wechat_subscribe_send_log | 微信订阅消息发送日志 | 防重复提醒、排查发送失败 |
| player_online_state | 玩家最近登录和活跃时间 | 离线资源提醒判断 |
| notification_reminder_claim | 多实例提醒任务领取去重占位 | 离线资源提醒跨实例幂等 |
| player_mail | 玩家邮件主体、已读、领取、删除和过期状态 | 邮件列表、详情、已读、领取、删除 |
| player_mail_attachment | 邮件附件物品和数量 | 邮件详情、附件领取、一键领取 |
| resource_sale_policy / resource_sale_option | 资源出售全局开关、正式版本和整数批次兑换规则 | 管理端兑换配置、玩家正式报价 |
| player_resource_sale_daily_usage | 每种来源资源的北京时间每日出售用量 | 每日额度并发控制 |
| player_resource_sale_order | 资源扣减、金币发放和前后余额不可变快照 | 成交幂等重放、历史查询和对账 |
| card_draw_broadcast_outbox | 稀有抽卡批次、玩家昵称、奖池、奖励及正文快照 | 5 分钟内向 Redis 提交前的租约接管、重试和过期审计；不代表节点或会话端到端送达 |

### 11.1 数据库外键约束

V1 至 V80 数据库迁移已补充外键约束、安全清理、令牌摘要、支付对账与审核退款、签到/成长/抽卡、建筑快照、Idle Adventure 体力与随机掉落、运营配置中心、登录与密码安全、补签费用快照、默认中文显示名、玩家经验等级、任务、好友与在线状态、运营消息、资源出售、两类商城、统一资产、旧邮件回填、运行策略管理、地块购买净化、加工输入消耗、装备升级额外资源、玩家昵称管理、32 格经营地图策略、统一品质、多场景进度、生产收取幂等、历史场景快照回填、场景成就宝箱和随机 NPC 发放、挂机加速、挂机物品类型快照、钻石商城体力奖励、建筑全局工作时间和多配方、每名玩家跨全部经营场景唯一的特殊建筑、净化剂消耗快照、钻石产出审计及抽卡稀有奖励公屏 Outbox；`init.sql` 只包含 V1 手工初始化结构：

| 外键 | 说明 |
| --- | --- |
| `user_wechat_account.user_id -> users.id` | 微信绑定必须对应本地用户 |
| `auth_token.user_id -> users.id` | token 必须对应本地用户 |
| `player_item.player_id -> users.id` | 玩家背包必须对应本地用户 |
| `player_item.item_id -> item_config.item_id` | 玩家背包物品必须对应有效物品配置 |
| `ad_reward_config.reward_item_key -> item_config.item_key` | 广告奖励物品必须对应有效物品配置 |
| `ad_watch_record.player_id -> users.id` | 广告观看记录必须对应本地用户 |
| `ad_watch_record.reward_item_key -> item_config.item_key` | 广告奖励记录中的物品必须对应有效物品配置 |
| `payment_product_config.reward_item_key -> item_config.item_key` | 支付商品奖励物品必须对应有效物品配置 |
| `payment_order.player_id -> users.id` | 支付订单必须对应本地用户 |
| `payment_order.reward_item_key -> item_config.item_key` | 支付订单奖励物品必须对应有效物品配置 |
| `payment_delivery_record.player_id -> users.id` | 支付发货记录必须对应本地用户 |
| `payment_delivery_record.order_no -> payment_order.order_no` | 支付发货记录必须对应有效订单 |
| `payment_delivery_record.item_key -> item_config.item_key` | 支付发货物品必须对应有效物品配置 |
| `payment_delivery_task.player_id -> users.id` | 支付发货任务必须对应本地用户 |
| `payment_delivery_task.order_no -> payment_order.order_no` | 支付发货任务必须对应有效订单 |
| `payment_refund_request.order_no -> payment_order.order_no` | 退款申请必须对应有效订单且一单最多一条 |
| `payment_refund_request.player_id -> users.id` | 退款申请必须对应有效玩家 |
| `payment_refund_request.reviewer_id -> operator_account.id` | 审核人必须对应独立运营账号 |
| `payment_refund_audit_log.refund_request_id -> payment_refund_request.id` | 每条退款审计必须对应退款申请 |
| `operator_token.operator_id -> operator_account.id` | 运营 token 必须对应运营账号 |
| `player_notification.player_id -> users.id` | 站内提醒必须对应本地用户 |
| `player_subscribe_setting.player_id -> users.id` | 订阅设置必须对应本地用户 |
| `wechat_subscribe_send_log.player_id -> users.id` | 微信订阅消息日志必须对应本地用户 |
| `wechat_subscribe_send_log.notification_id -> player_notification.id` | 微信订阅消息日志可关联站内提醒 |
| `player_online_state.player_id -> users.id` | 玩家活跃状态必须对应本地用户 |
| `notification_reminder_claim.player_id -> users.id` | 提醒领取占位必须对应本地用户 |
| `player_mail.player_id -> users.id` | 玩家邮件必须对应本地用户 |
| `player_mail_attachment.mail_id -> player_mail.id` | 邮件附件必须对应有效邮件 |
| `player_mail_attachment.player_id -> users.id` | 邮件附件必须对应本地用户 |
| `player_mail_attachment.item_id -> item_config.item_id` | 邮件附件物品必须对应有效物品配置 |
| `player_friend_request.requester_player_id -> users.id` | 好友申请发起方必须对应有效玩家，账号物理删除时级联清理 |
| `player_friend_request.recipient_player_id -> users.id` | 好友申请接收方必须对应有效玩家，账号物理删除时级联清理 |
| `player_friend.accepted_request_id -> player_friend_request.id` | V37 后建立的正式关系可追踪原始同意申请，申请删除时置空 |
| `wechat_friend_gift_reward.gift_config_id -> wechat_friend_gift_config.id` | 微信道具奖励必须对应已发布礼包映射 |
| `wechat_friend_gift_reward.item_id -> item_config.item_id` | 微信赠礼只能发放有效的游戏内物品 |
| `wechat_gift_delivery_record.player_id -> users.id` | 微信赠礼发货流水必须对应本地玩家 |
| `wechat_gift_delivery_record.gift_config_id -> wechat_friend_gift_config.id` | 微信赠礼发货流水必须保留原礼包映射 |
| `player_map.player_id -> users.id` | 玩家地图必须对应本地用户 |
| `player_map.current_scene_id -> land_scene_config.scene_id` | 玩家最近进入的经营场景必须对应正式场景配置 |
| `player_land.player_id -> users.id` | 玩家地块必须对应本地用户 |
| `player_land.scene_id -> land_scene_config.scene_id` | 玩家地块必须归属有效经营场景 |
| `player_land.land_id -> land_config.land_id` | 玩家地块必须对应有效地块配置 |
| `player_land_slot_unlock.scene_id -> land_scene_config.scene_id` | 付费格购买记录必须归属有效经营场景 |
| `player_land_slot_purification.scene_id -> land_scene_config.scene_id` | 格子净化记录必须归属有效经营场景 |
| `player_scene.player_id -> users.id` | 玩家场景进度必须对应本地用户 |
| `player_scene.scene_id -> land_scene_config.scene_id` | 玩家场景进度必须对应正式场景配置 |
| `player_production_collect_record.player_id -> users.id` | 生产收取幂等记录必须对应本地用户 |
| `player_production_collect_record.scene_id -> land_scene_config.scene_id` | 单建筑或单场景收取记录必须关联有效场景 |
| `land_map_business_policy.purification_item_id -> item_config.item_id` | 净化策略必须引用有效物品 |
| `building_level_config.building_id -> building_config.building_id` | 建筑等级配置必须对应建筑基础配置 |
| `player_building.player_id -> users.id` | 玩家建筑必须对应本地用户 |
| `player_building.building_id -> building_config.building_id` | 玩家建筑必须对应有效建筑配置 |
| `player_building.land_id -> player_land.id` | 玩家建筑必须放在有效玩家地块上 |
| `player_building_production.player_id -> users.id` | 建筑产出状态必须对应本地用户 |
| `player_building_production.player_building_id -> player_building.id` | 建筑产出状态必须对应有效玩家建筑 |
| `player_building_production.item_id -> item_config.item_id` | 建筑产物必须对应有效物品配置 |
| `player_diamond_production_audit.player_id -> users.id` | 钻石建筑产出审计必须对应有效玩家 |
| `player_diamond_production_audit.collect_record_id -> player_production_collect_record.id` | 钻石审计必须关联生产收取幂等流水 |
| `card_draw_broadcast_outbox.batch_id -> card_draw_batch.id` | 公屏通报必须对应已落库的抽奖批次 |
| `card_draw_broadcast_outbox.player_id -> users.id` | 公屏通报的玩家昵称与奖励快照必须对应有效玩家 |
| `player_npc.player_id -> users.id` | 玩家 NPC 必须对应本地用户 |
| `player_npc.npc_id -> npc_config.npc_id` | 玩家 NPC 必须对应有效 NPC 配置 |
| `player_npc.settled_player_building_id -> player_building.id` | NPC 当前入驻建筑必须对应有效玩家建筑 |
| `player_building_npc.player_id -> users.id` | 建筑 NPC 入驻关系必须对应本地用户 |
| `player_building_npc.player_building_id -> player_building.id` | 建筑 NPC 入驻关系必须对应有效玩家建筑 |
| `player_building_npc.player_npc_id -> player_npc.id` | 建筑 NPC 入驻关系必须对应有效玩家 NPC |
| `player_equipment.player_id -> users.id` | 玩家装备必须对应本地用户 |
| `player_equipment.equipment_id -> equipment_config.equipment_id` | 玩家装备必须对应有效装备配置 |
| `sign_in_reward_config.plan_code -> sign_in_plan.plan_code` | 签到奖励必须对应有效签到计划 |
| `player_sign_in_record.player_id -> users.id` | 签到领取记录必须对应本地用户 |
| `player_sign_in_record.plan_code -> sign_in_plan.plan_code` | 签到领取记录必须对应有效计划 |
| `player_sign_in_reward_record.sign_in_record_id -> player_sign_in_record.id` | 实际奖励流水必须对应领取主记录 |
| `player_sign_in_reward_record.reward_config_id -> sign_in_reward_config.id` | 实际奖励流水必须关联配置行 |

如果旧数据库里已经存在脏数据，执行外键 SQL 时 MySQL 会报错，需要先清理不合法记录。

### 11.2 Flyway 版本迁移和测试数据边界

先创建空数据库，应用启动时 Flyway 自动执行 `src/main/resources/db/migration` 下的版本脚本。当前新库依次执行 V1 至 V130（其中包含 V81.1；V117 未占用，V130 为当前最新版本）。`FLYWAY_BASELINE_ON_MIGRATE` 默认是 `false`。

只有“已有业务表但没有 `flyway_schema_history`”的旧库首次接入时，才在完整备份后临时设置 `FLYWAY_BASELINE_ON_MIGRATE=true`。当前 `baseline-version=0`，会先记录版本 0，再执行 V1 到最新版本。迁移成功并核对记录后必须立即恢复为 `false`，防止误连其他非空数据库时被自动基线化。已经由 Flyway 管理的环境升级时始终保持 `false`。

当前版本文件：

- `V1__baseline_schema_and_config.sql`：基线表结构、约束和公共配置。
- `V2__harden_building_schedule_and_land_ownership.sql`：建筑时间精度及玩家地块归属约束。
- `V3__secure_wechat_session_and_reminder_claims.sql`：清理历史微信 `session_key`，创建多实例提醒领取占位表。
- `V4__hash_auth_tokens.sql`：把历史原始登录令牌转换为 SHA-256 摘要，并把令牌列收紧为 64 字符。
- `V5__add_payment_reconciliation_tasks.sql`：创建支付主动查单租约任务、回填已有预支付订单，并增加登录令牌清理索引。
- `V6__add_sign_in_and_equipment_system.sql`：增加首次登录时间和钻石，创建装备仓库、签到计划/配置/领取审计表，并写入新手和月度默认奖励。
- `V7__add_npc_growth_system.sql`：增加 NPC 专属碎片、逐级金币、1 至 100 级属性、突破配置和升级流水。
- `V8__add_equipment_growth_and_equip.sql`：增加装备逐级属性、同款材料突破、穿戴/卸下关系和升级流水。
- `V9__add_card_draw_system.sql`：增加初始版本化卡池、单抽/五连价格、权重、批次快照和幂等结果。
- `V10__make_card_draw_pool_size_configurable.sql`：增加卡池和批次 `card_count`，允许每个卡池版本配置 1 至 100 个连续展示奖项。
- `V11__snapshot_building_tasks_and_production_rules.sql`：增加建筑任务实际消耗、退款和产出规则快照。
- `V12__harden_payment_prepay_and_refund_lifecycle.sql`：增加预支付租约、长期退款监控、奖励追回和经济冻结审计。
- `V13__add_reminder_claim_cleanup_index.sql`：增加通知领取占位有界清理索引。
- `V14__enable_diamond_only_payment_products.sql`：停用非钻石支付商品，启用 6 个钻石充值档位并保留历史配置和订单。
- `V15__add_reviewed_payment_refunds.sql`：创建独立运营账号/token、退款申请和追加审计，增加 `REFUNDING` 订单状态与不可复活的 `CANCELLED` 发货任务状态。
- `V16__add_idle_adventure_mode.sql`：增加服务端权威 Idle Adventure 配置、会话、幂等命令、奖励和挂机快照。
- `V17__restore_legacy_idle_adventure_contract.sql`：恢复 Unity 已使用的 26 条 `snake_case` 契约，增加暂停/恢复和编队初始化语义。
- `V18__add_operator_config_center.sql`：增加配置键锁、草稿/正式版本、乐观锁和不可变配置审计。
- `V19__enable_idle_adventure_config_publishing.sql`：将玩法子配置主键改为数据库安全分配，支持完整版本发布。
- `V20__add_equipment_speed_bonus.sql`：增加装备逐级速度加成。
- `V21__harden_operator_login_security.sql`：增加运营登录限流、空闲会话、设备摘要和登录审计。
- `V22__add_operator_password_security.sql`：增加本人密码修改、全会话注销和安全事件审计。
- `V23__add_configurable_sign_in_makeup.sql`：增加补签开关/费用、普通与补签唯一性和费用快照。
- `V24__localize_default_config_names.sql`：仅把未被自定义的系统英文地块、建筑和默认卡池显示名转换为中文，不改变稳定编码。
- `V25__add_player_progression.sql`：增加累计经验、等级曲线 revision、只追加等级配置、幂等经验流水，并以 `SYSTEM_BACKFILL` 回填历史玩家当前等级最低累计经验。
- `V26__add_building_upgrade_experience.sql`：增加逐目标等级建筑升级经验和任务奖励快照；历史任务保持 0，不追溯补发。
- `V27__add_config_refresh_outbox.sql`：增加等级曲线发布后的可靠 Outbox、租约和重试状态。
- `V28__add_player_task_system.sql`：增加日常/周常任务配置、玩家周期快照和可信进度幂等流水。
- `V29__add_player_friend_system.sql`：增加规范化无向好友关系及双向查询索引。
- `V30__add_wechat_friend_gift_system.sql`：增加微信官方好友赠礼礼包、不可变道具映射和按 `OrderId` 幂等的发货流水。
- `V31__add_operator_announcement_and_system_reward.sql`：增加运营公告、系统奖励模板、发送活动、目标快照、租约和审计。
- `V32__add_operator_system_mail_campaign.sql`：增加管理端普通无附件系统邮件活动。
- `V33__add_operator_mail_campaign_creation_lock.sql`：增加普通邮件活动创建串行锁，防止并发重复创建。
- `V34__add_daily_friend_gift_system.sql`：增加游戏好友每日赠礼策略和不可变发放流水。
- `V35__add_resource_sale_system.sql`：增加资源出售策略、整数兑换规则、北京时间每日额度和不可变成交订单。
- `V36__harden_resource_sale_constraints.sql`：在不修改 V35 校验和的前提下加固安全整数、成交乘积和余额恒等式约束。
- `V37__add_player_friend_request_system.sql`：增加好友申请策略、完整申请历史、单一待处理约束和正式关系来源追踪。
- `V38__add_friend_presence_state.sql`：增加好友在线状态、最后活跃时间、单调版本和对账索引。
- `V39__add_player_avatar_system.sql`：增加头像对象键、来源、版本、每日修改次数和旧对象清理 Outbox。
- `V40__add_coin_shop_system.sql`：增加金币商城策略、商品、北京时间每日限购和不可变订单。
- `V41__add_diamond_shop_system.sql`：增加钻石商城策略、商品、北京时间每日限购和不可变订单。
- `V42__add_base_asset_config_management.sql`：开放物品基础资料管理和稳定 ID 分配。
- `V43__add_equipment_catalog_management.sql`：开放装备基础资料及初始化成长配置管理。
- `V44__add_npc_catalog_management.sql`：开放 NPC 基础资料、专属碎片和初始化成长配置管理。
- `V45__unify_asset_reference_snapshots.sql`：统一抽卡、签到、商城和资源出售的资产 ID 与展示快照。
- `V46__add_typed_reward_mail_attachments.sql`：让系统奖励和邮件附件支持 `ITEM / EQUIPMENT / NPC`。
- `V47__backfill_claimed_mail_grant_snapshots.sql`：回填 V46 前已领取物品邮件的实际发放快照。
- `V48__add_operator_runtime_content_management.sql`：增加广告、客户端运行、通知、经济、头像和内容模板运营配置。
- `V49__harden_client_runtime_policy_and_schema.sql`：增加客户端版本上报模式并补齐 JSON、状态和表注释约束。
- `V50__remove_verified_duplicate_indexes.sql`：删除两组经过 Mapper、EXPLAIN 和索引统计确认的重复索引。
- `V51__add_idle_adventure_stamina_and_random_drops.sql`：增加体力、固定奖励范围、版本化随机池和逐抽审计。
- `V52__harden_idle_adventure_stamina_and_random_drops.sql`：独立加固 V51 的默认值、CHECK、复合索引及级联外键。
- `V53__add_processing_production_input_costs.sql`：增加加工建筑每周期输入物品消耗。
- `V54__add_player_land_slot_purchases.sql`：增加玩家付费格永久购买记录。
- `V55__move_free_land_units_to_bottom_right.sql`：把固定免费区域调整至经营地图右下区域。
- `V56__add_player_land_slot_purification.sql`：增加按玩家稳定生成的格子净化记录。
- V57__add_equipment_upgrade_resource_costs.sql：增加装备升级额外物品消耗。
- `V75__make_growth_breakthroughs_configurable.sql`：放开 NPC/装备突破点为 2 到 100 级内可配置，并增加装备升级额外资源消耗快照流水。
- `V58__backfill_processing_production_cost_snapshots.sql`：精确回填存量加工规则和进行中任务快照。
- `V59__add_player_nickname_management.sql`：增加昵称规范化唯一性、乐观版本、策略和审计流水。
- `V60__add_land_map_business_policy.sql`：固定每个经营场景 8×4 共 32 格的布局规则。
- `V61__add_unified_npc_and_equipment_quality.sql`：统一 NPC 与装备五档品质。
- `V62__add_multi_scene_land_system.sql`：增加场景目录、玩家场景进度、场景隔离和生产收取幂等快照。
- `V63__harden_production_collect_scene_scope.sql`：回填历史生产收取场景快照并收紧收取范围约束。
- `V64__add_scene_achievement_chest_rewards.sql`：增加场景建成成就宝箱、奖励权益快照和随机 NPC 发放流水。
- `V65__add_privacy_compliance_and_account_cancellation.sql`：增加隐私政策、授权事件和账号注销状态。
- `V66__harden_privacy_compliance_constraints.sql`：补齐存量注销指纹、HTTPS、UUID 和头像清理约束。
- `V67__add_daily_friend_stamina_gifts.sql`：增加好友每日体力赠礼配置和幂等流水。
- `V68__add_idle_adventure_afk_acceleration.sql`：增加挂机加速开关、体力成本和加速幂等审计流水，固定 CRC32 为 `-1616972206`。
- `V69__add_idle_adventure_afk_item_type_snapshot.sql`：在不修改 V68 的前提下补齐挂机物品类型快照、历史回填和非空 CHECK 约束，固定 CRC32 为 `423149015`。
- `V70__add_diamond_shop_stamina_reward.sql`：扩展钻石商城商品和订单 CHECK，支持固定 `STAMINA / stamina` 体力奖励，固定 CRC32 为 `1183825380`。
- `V71__add_building_fuel_clock_and_recipes.sql`：增加全局建筑工作时间、补充物品、多配方和生产快照，固定 CRC32 为 `1008907161`。
- `V72__add_real_name_and_anti_addiction_compliance.sql`：增加实名、防沉迷、未成年人支付额度和平台上报 Outbox。
- `V73__harden_anti_addiction_session_and_calendar_publish.sql`：增加会话代次与日历发布事务锁，固定 CRC32 为 `1652737654`。
- `V74__add_player_bootstrap_config.sql`：增加配置化新玩家初始化物资。
- `V75__make_growth_breakthroughs_configurable.sql`：开放 NPC/装备突破等级并增加装备升级资源消耗快照。
- `V76__add_content_safety_management.sql`：增加内容安全词库、微信检查和摘要审计。
- `V77__add_operator_operations_p0.sql`：增加 P0 运营账号、财务、玩家、异常和指标中心。
- `V78__add_operator_operations_p1.sql`：增加 P1 审计、分析、标签分群、客服备注和定时发布。
- `V79__add_unique_special_buildings_and_purification_agent.sql`：增加两种每名玩家跨全部经营场景唯一的特殊建筑、净化剂消耗快照和钻石产出审计，固定 CRC32 为 `1556615827`。
- `V80__add_card_draw_rare_broadcast_outbox.sql`：增加卡池总开关、奖项命中开关、通报模板和批次级可靠公屏广播 Outbox。
- `V81__add_player_progression_reconciliation_task.sql`：增加冻结范围的成长对账任务、数据库租约、汇总计数和逐玩家失败明细，固定 CRC32 为 `1658821740`。
- `V81_1__extend_player_progression_reconciliation_task.sql`：为历史任务回填唯一请求键和 `MANUAL` 来源，允许系统自动任务不绑定运营员，并增加公平失败重试索引，固定 CRC32 为 `-840177369`。
- `V82__harden_player_progression_contract.sql`：拒绝脏成长快照和超限经验来源，增加等级/经验/流水算术约束、经验分页索引及公平自动任务扫描索引，并修复面包坊和烧烤摊升级经验，固定 CRC32 为 `-1431918849`。
- `V83__add_player_onboarding.sql`：创建六张新手引导表和默认关闭的冻结 v1，固定 CRC32 为 `1070104245`。
- `V84__harden_player_onboarding_contract.sql`：保留 v1、归一化终态、加固约束并将未来分配切换到兼容 v2，固定 CRC32 为 `1114405186`；V83 环境启动前必须先独立执行只读预检。
- `V85__add_batch_npc_and_equipment_upgrade.sql`：增加 NPC/装备一键升级完成态幂等批次、逐级流水批次关联、初始整批同款材料上限 1000 和材料候选索引，固定 CRC32 为 `-1472509603`。
- `V86__raise_equipment_batch_material_limit.sql`：不改写 V85，前向把装备完成态批次累计同款材料上限放宽到 5000，固定 CRC32 为 `-179532443`。
- `V87__add_resource_collect_task_objective.sql`：增加累计收获指定资源次数任务、目标物品快照和可靠进度事件，固定 CRC32 为 `-1481082424`。
- `V88__harden_resource_collect_task_contract.sql`：前向修复历史空快照、重建显式 NULL 安全约束并增加当前资源任务依赖索引，固定 CRC32 为 `-892889675`。
- `V89__add_scene_slot_unlock_task_objective.sql`：增加按场景开辟 1～32 格的永久宝箱里程碑，并允许同场景配置多个不同档位，固定 CRC32 为 `-1964148448`。
- `V90__add_lifetime_cumulative_task_achievements.sql`：增加三种事件驱动永久累计成就、永久累计统计表和统一成就唯一身份，固定 CRC32 为 `-319452238`。
- `V91__cap_lifetime_task_progress.sql`：封顶历史永久累计进度并收紧永久累计进度范围约束，固定 CRC32 为 `-613092297`。
- `V92__add_npc_acquisition_channels.sql`：增加空配置起步的 NPC 获取渠道、独立发布版本状态、路由参数约束和运行时查询索引，原始固定 CRC32 为 `1615105684`，执行后不可改写。
- `V93__harden_npc_acquisition_channel_contract.sql`：不改写 V92，前向收紧按钮长度、优先级和配置版本，并补齐非空白、渠道类型/路由一致和启用值约束；列变窄期间临时强制 `STRICT_ALL_TABLES`，脏数据会阻断而不会静默截断，成功后恢复原会话模式；固定 CRC32 为 `-2034435388`。
- `V94__separate_operator_business_numbers_and_idempotency.sql`：分离运营展示编号与创建幂等键，固定 CRC32 为 `-246164165`。
- `V95__add_wechat_customer_service_workbench.sql`：增加微信小游戏官方客服联系人、工单、消息和可靠发送 Outbox，固定 CRC32 为 `-1236124090`。
- `V96__harden_wechat_customer_service_runtime_columns.sql`：以前向迁移完善客服临时会话与多节点租约字段，固定 CRC32 为 `292469054`。
- `V97__add_preset_avatar_code.sql`：增加固定头像编号字段及查询索引，固定 CRC32 为 `151659432`。
- `V98__harden_preset_avatar_contract.sql`：建立固定头像来源与编号的一致性约束，固定 CRC32 为 `-2063702854`。
- `V99__disable_remote_avatar_sources.sql`：下线远程头像来源、归一历史数据并保留可选清理 Outbox，固定 CRC32 为 `67421201`。
- `V100__harden_system_avatar_contract.sql`：补齐注销清理原因、规范历史固定头像编号并以二进制精确 CHECK 收紧系统头像合同，固定 CRC32 为 `-880935665`。
- `V101__add_avatar_preset_catalog.sql`：增加数据库驱动的固定头像目录、Unity 清单验收证据、稳定编号分配和生命周期状态，原始固定 CRC32 为 `1155793846`，执行成功后禁止修改或通过 `repair` 接受漂移。
- `V102__forward_harden_avatar_preset_catalog.sql`：不改写 V101，前向加固目录身份、资源验收、清单结构版本、规范化请求指纹、唯一索引和头像序列保护，固定 CRC32 为 `-707871511`；执行前两张头像清单审计表必须均为空，任一表非空会安全阻断并保留原数据。
- `V103__remove_legacy_avatar_resource_exception.sql`：不改写 V101/V102，以固定 CRC32 `-172308135` 取消 `AVATAR_001` 的无摘要历史兼容例外并无条件降为 `STAGED`（`minimum_client_version_code=0`）；迁移后必须重新验收真实 Unity SHA-256 清单，不能直接启用。
- `V104__complete_avatar_client_version_contract.sql`：不改写 V103，将 V103 产生的未验收 `AVATAR_001` 最低客户端版本号从 0 提升为 1，并将目录最低客户端版本约束收紧为正整数，固定 CRC32 为 `-37724955`；保留清单字段和后续手动 `BLOCKED` 状态。
- `V131__add_sign_in_plan_versioning.sql`：增加不可变签到计划/奖励版本、玩家新手签到实例、存量迁移审计和客户端版本观测；存量绑定标记为 `LEGACY_BASELINE`，缺少首次登录时间时回退账号创建时间并记录审计，新玩家在首次成功登录时绑定当时生效版本。
- `V132__separate_activity_sign_in_reward_window.sql`：历史已执行迁移，只为活动签到增加独立奖励窗口和 `activity_reward_mode`，固定 checksum 为 `1930570088`；必须保持原文，禁止修改或 `repair`。
- `V133__separate_sign_in_reward_window.sql`：以前向迁移把奖励窗口能力扩展到新手签到，将历史活动字段统一为 `rewardMode/rewardWindowDays`，并补齐玩家实例冻结快照；已执行后不得修改，后续结构变化必须继续使用更高版本迁移。
- `V134__extend_idle_adventure_skill_target_effects.sql`：为挂机冒险技能增加初始冷却、伤害/回血/状态效果、目标阵营、单格/固定格/有序目标组/全体/随机目标规则，以及单体治疗参数；旧技能由数据库默认值兼容为单体伤害；已执行 checksum 为 `501186811`，不得修改或使用 `flyway repair`。
- `V135__add_idle_adventure_skill_include_self.sql`：在不改写 V134 的前提下，为 `ALLY + ALL` 技能增加 `include_self` 字段及约束；默认不包含施法者。
- `V136__add_npc_portrait_resource_key.sql`：为 NPC 增加可空的 `portrait_key` 立绘绑定键，不回填猜测资源路径；已执行后不得修改。
- `V137__remove_retired_equipment_slots.sql`：因项目尚未上线，清理 `RING`、`ACCESSORY`、`NECKLACE` 及其开发期装备、成长、库存、穿戴、奖励和商城引用；固定 checksum 为 `911080598`，已执行后不得修改或使用 `repair`。
- `V138__add_manual_equipment_breakthrough_materials.sql`：增加单次装备手选突破幂等请求、任意等级材料候选索引、材料等级/培养投入快照和不可退款审计；固定 checksum 为 `1435232508`，已冻结，后续变化只能使用前向迁移。
- `V139__add_manual_material_auto_upgrade_v2.sql`：扩展装备一键升级批次以兼容 `contract_version=1/2`，保存 V2 分级材料、选择指纹和确认事实，并允许逐级审计归属批次；固定 checksum 为 `1413213846`，已执行后不得修改。
- `V140__harden_equipment_breakthrough_request_lifecycle.sql`：不改写 V138/V139，前向增加单次突破请求 `PROCESSING/COMPLETED` 生命周期，并收紧材料等级与培养投入标记一致性；固定 checksum 为 `1806305011`，已执行后不得修改。
- `V141__harden_equipment_manual_material_audit_nullability.sql`：不改写 V139/V140，显式要求 V2 材料分组 JSON、材料选择指纹和手选流水双指纹非空；固定 checksum 为 `-1149433050`，已执行后不得修改。

```bash
mysql -u root -p -e "CREATE DATABASE IF NOT EXISTS game_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci"
```

V1 至 V147 一旦成功执行均禁止修改；V134～V147 保持冻结。下一次数据库变化必须新增 V148。以下脚本仅作为无法启动 Spring Boot 时的 V1 手工恢复兼容入口：

```bash
mysql -u root -p < src/main/resources/db/init.sql
```

`init.sql` 不创建测试玩家，正常启动不需要重复手工执行。仅在本地开发或接口联调环境继续执行：

```bash
mysql -u root -p < src/main/resources/db/dev-seed.sql
```

`dev-seed.sql` 会创建使用 BCrypt 密码散列的测试账号，以及对应背包、NPC、地图、地块和建筑数据。生产环境禁止执行该文件。

 已有数据库升级时不要手工执行任何版本迁移；保持 `FLYWAY_ENABLED=true`，停止旧版本 API/Worker 写入并备份数据库，只启动一个新版本实例让 Flyway 自动执行尚未应用的迁移至 V147，确认 `flyway_schema_history` 的 V131～V147 均为 `success=1`、V132 checksum 为 `1930570088`、V134 checksum 为 `501186811`、V137 checksum 为 `911080598`、V138 checksum 为 `1435232508`、V139 checksum 为 `1413213846`、V140 checksum 为 `1806305011`、V141 checksum 为 `-1149433050`、V142 checksum 为 `2110921250`、V143 checksum 为 `1356587650`、V144 checksum 为 `107305189`、V145 checksum 为 `-716050190`、V146 checksum 为 `1808794590`、V147 checksum 为 `1724824817`、`npc_config.portrait_key` 和 `resource_sale_option_freeze` 已存在、技能字段和签到版本审计数量正确、装备手选材料快照、请求生命周期和显式非空约束存在且重复迁移零变更后，再启动其他实例。V131～V147 已执行后不得修改脚本或使用 `repair`，后续正式变化从 V148 开始。V131～V147 的真实 MySQL 8 空库升级和重复迁移由对应集成测试覆盖；新 Mapper 使用 MySQL 8 别名语法，历史迁移中的 `VALUES()` 弃用警告不通过修改旧脚本消除。

## 12. 当前实现注意事项

1. 当前账号密码登录用于开发测试；新密码使用 BCrypt，旧明文密码首次成功登录后自动升级。生产环境默认关闭账号密码登录并使用微信登录。
2. 建筑当前按单格处理；建造和移动建筑时，前端只传玩家地块 ID，建筑坐标由服务端根据地块自动确定。
3. 拆除建筑当前不返还资源。
4. 取消建造或升级按 `building_task` 中冻结的实际消耗和退款比例返还；默认比例是 50%，后续配置变化不影响已开始任务。
5. 资源真实数量存放在 `player_item`；`users` 不再保存金币、木材、石材、铁矿余额。
6. 建筑当前产出规则和待收取数量都保存在快照行；修改等级配置不会追溯生效，升级完成或 NPC 入驻/移除会先结算旧规则再切换。
7. NPC 入驻建筑只做资源产出加成，不影响建造/升级时间；入驻或移除前后端会先结算并重置生产时钟。
8. 有 NPC 入驻的建筑暂不允许升级，前端需先调用移除 NPC 接口。
9. 激励视频广告默认关闭，需设置 `AD_REWARD_ENABLED=true` 才允许 `prepare/complete/claim`；生产环境还需设置 `AD_COMPLETION_PROOF_REQUIRED=true` 和 `AD_COMPLETION_PROOF_SECRET`，前端不能自行生成 `serverProof`。
10. 支付接口默认关闭；`PAYMENT_ENABLED=true` 才允许创建订单，`WECHAT_PAY_ENABLED=true` 且商户配置完整才会返回真实微信支付参数；支付晚于订单有效期会进入 `MANUAL_REVIEW`，不会自动发货。
11. 正式支付发货必须等待微信回调验签成功，或商户主动查单确认 `SUCCESS`；两条路径使用同一服务端事务，客户端不能直接发放资源。
12. 微信 `code` 只能使用一次，重复使用会返回 `invalid code`。
13. `Content-Type` 必须写 `application/json`，不要写成 `application/joson`。
14. 数据库结构由 Flyway 管理；`init.sql` 仅保留为手工恢复兼容脚本，测试玩家数据位于 `dev-seed.sql`，生产环境禁止执行该文件。自动 baseline 默认关闭，旧库仅首次接入时临时开启。
15. 新手签到窗口由管理端配置为 1～31 天。玩家首次成功登录时绑定不可变 `planVersion/windowDays` 和奖励快照；已绑定玩家不受后续配置发布影响，新玩家使用新版本。月度签到按服务端当月实际天数运行。普通领取只认服务端当天；是否允许补领取决于计划的 `allow_makeup` 和费用配置，默认新手/月度计划都关闭补签。
16. 微信订阅消息发送和离线资源提醒默认关闭；必须先完成前端 `wx.requestSubscribeMessage` 授权、配置真实模板 ID，再开启 `WECHAT_SUBSCRIBE_ENABLED` 和 `RESOURCE_REMINDER_ENABLED`。
17. 内部运营提醒和邮件接口不走玩家 token。生产环境必须分别配置至少 32 字符且互不相同的 `NOTIFICATION_OPERATION_ADMIN_KEY` 与 `NOTIFICATION_MAIL_ADMIN_KEY`；轮换时可短暂配置对应 `*_PREVIOUS_KEY`，禁止给客户端。
18. WebSocket 单实例使用 `local_only`；多实例使用 `redis_pubsub`，全局连接数由 Redis 租约原子控制。每次消息和私有推送仍复检数据库令牌，不能使用 URL 查询参数传 token。
19. 建筑完成自动扫描默认关闭。设置 `BUILDING_COMPLETION_REMINDER_ENABLED=true` 后，服务端只自动完成已经到达 `buildEndTime` 的任务，并创建 `BUILDING_COMPLETED` 站内提醒和在线 WebSocket 推送；不会提前完成建造或升级。
20. `application.yml` 默认使用 `${SPRING_PROFILES_ACTIVE:dev}`，本地直接启动会加载 `dev + payment`；生产环境必须配置 `SPRING_PROFILES_ACTIVE=prod`，并将 `CORS_ALLOWED_ORIGIN_PATTERNS`、`REALTIME_ALLOWED_ORIGIN_PATTERNS` 收紧为真实前端域名，避免使用 `*`。
21. 登录用户名最大 50 字符、密码最大 72 字符且 BCrypt 前按 UTF-8 最大 72 字节校验；微信临时 code 最大 128 字符。
22. GitHub CI 会在 MySQL 8 和 Redis 7 上运行测试；Pull Request 额外执行 Dependency Review，Dependabot 每周检查 Maven 和 GitHub Actions 更新。
23. V15 退款接口默认关闭，只适用于普通微信支付 API v3 JSAPI。米大师虚拟支付不能复用；运营账号/token 与玩家 token、提醒密钥和邮件密钥完全独立。
24. 退款 `MANUAL_REVIEW` 不代表可以手工改库。必须从运营详情核对订单快照、微信证据、追回结果和审计日志，再调用重试或带明确 `resolution` 的关闭接口。
25. 抽卡 `card_count` 为每个卡池版本独立配置的 1 至 100；`DEFAULT_CARD_9` 只是 V9 历史编码，前端不得固定渲染九格。
26. 运营配置台保存草稿不会改变游戏数据，只有发布事务才写正式业务表。页面草稿基于正式数据指纹并使用修订号防止并发覆盖；直接 SQL 修改后旧草稿会因指纹冲突被拒绝。

## 13. 邮件接口

邮件系统用于系统通知、活动奖励、补偿奖励和运营邮件。玩家邮件接口都需要 token；内部运营发送接口必须带 `X-Notification-Admin-Key`。`NOTIFICATION_MAIL_ADMIN_KEY` 必须独立配置且不能回退复用提醒密钥；生产启动会拒绝弱密钥或重复密钥。邮件列表使用单条 SQL 返回附件标记。

### 13.1 查询邮件列表

```http
GET /api/mail/list/{playerId}?limit=50
Authorization: Bearer <token>
```

返回 data：`MailResponse[]`。列表默认不返回正文和附件明细，前端可用 `readFlag=false` 或 `hasAttachment=true && attachmentClaimed=false` 计算红点。

```json
[
  {
    "mailId": 1001,
    "playerId": 1,
    "mailType": "ACTIVITY",
    "title": "限时活动奖励",
    "content": null,
    "senderName": "系统",
    "readFlag": false,
    "hasAttachment": true,
    "attachmentClaimed": false,
    "expired": false,
    "expireTime": "2026-07-21 10:00:00",
    "createTime": "2026-07-14 10:00:00",
    "attachments": []
  }
]
```

### 13.2 查询邮件详情

```http
GET /api/mail/detail/{playerId}/{mailId}
Authorization: Bearer <token>
```

详情会返回正文和附件明细。

### 13.3 标记邮件已读

```http
POST /api/mail/read
Content-Type: application/json
Authorization: Bearer <token>
```

```json
{
  "playerId": 1,
  "mailId": 1001
}
```

### 13.4 领取单封邮件附件

```http
POST /api/mail/claim
Content-Type: application/json
Authorization: Bearer <token>
```

```json
{
  "playerId": 1,
  "mailId": 1001
}
```

返回 data：`MailClaimResponse`，包含已领取附件和最新 `items` 背包列表。

### 13.5 一键领取邮件附件

```http
POST /api/mail/claim_all
Content-Type: application/json
Authorization: Bearer <token>
```

```json
{
  "playerId": 1,
  "limit": 50
}
```

返回 data：`MailClaimAllResponse`，包含 `claimedCount/skippedCount/mails/items`。

### 13.6 删除邮件

```http
POST /api/mail/delete
Content-Type: application/json
Authorization: Bearer <token>
```

```json
{
  "playerId": 1,
  "mailId": 1001
}
```

带未领取附件的邮件不允许删除。

### 13.7 内部发送运营邮件

```http
POST /api/mail/admin/send
Content-Type: application/json
X-Notification-Admin-Key: your-random-admin-key
```

```json
{
  "playerId": 1,
  "mailType": "ACTIVITY",
  "title": "限时活动奖励",
  "content": "感谢参与活动，请领取附件奖励。",
  "senderName": "系统",
  "expireTime": "2026-07-21 10:00:00",
  "sourceType": "ACTIVITY",
  "sourceId": "summer-2026-day1",
  "attachments": [
    {
      "itemKey": "gold",
      "quantity": 100
    }
  ]
}
```

所有运营邮件，包括单人、批量和全服，都必须填写 `sourceType/sourceId`，用于唯一键幂等去重，防止接口超时重试时重复发送或重复发奖。创建邮件后，后端会创建站内提醒；玩家在线时会通过 WebSocket 推送 `NOTIFICATION_CREATED`。

安全和容量规则：

1. 一键领取按单封邮件使用独立事务；某封失败时，该封背包变更和领取状态一起回滚，不影响其他邮件。
2. 单人和批量发送都必须使用稳定且唯一的 `sourceType/sourceId`；批量发送会在写入前校验全部目标玩家，再按单玩家使用独立事务。数据库故障等中途失败后使用相同来源键重试，已经创建的邮件不会重复。
3. 单次目标玩家默认最多 1000 名，由 `MAIL_MAX_ADMIN_TARGETS` 控制，服务端硬上限 10000；超过时必须拆分批次。
4. 单封邮件默认最多 20 种附件，单附件数量最多 1000000000，分别由 `MAIL_MAX_ATTACHMENTS` 和 `MAIL_MAX_ATTACHMENT_QUANTITY` 控制；重复 `itemKey` 合并后的总量仍不能超过单附件上限。
5. `playerIds` 每项必须为正数，重复玩家 ID 会在服务端去重。

## 14. 前端联调边界

后端已经提供 HTTP 接口、微信支付服务端回调、服务端发货、WebSocket 推送和邮件/通知数据协议。以下 SDK 调用必须由 Unity / 微信小游戏前端完成，后端不能直接替客户端调用：

| 前端能力 | 前端要做 | 后端要做 |
| --- | --- | --- |
| `wx.login` | 获取一次性 `code`，调用 `POST /api/user/wx-login` | 校验 code，绑定 openid，签发 token |
| `wx.createRewardedVideoAd` | 全局维护广告实例，load/show，监听 `onClose`，只有 `isEnded=true` 才调用 `complete` | 返回 `adUnitId/watchId/minWatchSeconds`，校验完整观看确认，发放奖励 |
| `wx.requestPayment` | 使用 `payParams` 调起微信支付，支付成功后刷新订单或背包 | 并发幂等预支付、验签回调、主动查单补偿、服务端发货 |
| `wx.requestSubscribeMessage` | 使用后端返回的模板 ID 请求授权，把授权结果保存回后端 | 保存授权状态，离线资源/运营提醒时发送订阅消息 |
| WebSocket | 连接 `/api/ws/realtime`，在握手请求头设置 `Authorization: Bearer <token>`，处理 `CONNECTED/PONG/NOTIFICATION_CREATED` | 仅接受请求头鉴权；消息和私有推送前复检令牌，单实例本地推送，多实例可通过 Redis Pub/Sub 转发 |
| 邮件红点/领取/删除 | 调用邮件列表/详情/领取/删除接口，根据字段刷新 UI 和背包 | 保存邮件状态，附件领取原子入库，返回最新背包 |
| 签到和装备仓库 | 查询签到概览；普通领取只提交 `planCode`，补签提交 `planCode/rewardDay`；装备到账后刷新仓库 | 使用数据库北京时间和用户行锁防重复，按配置扣除补签费用、原子发奖并保存费用与奖励快照 |
| NPC/装备成长和卡池抽奖 | 只展示卡池并传抽奖档位，生成并复用幂等请求号，按返回结果播放动画和刷新 UI | 校验配置与归属，服务端随机、串行扣费、发奖和写审计流水，返回权威结果 |

作为后端开发需要继续配合的事项：维护接口文档和示例、提供测试账号和测试数据、保证错误信息稳定、查看服务端日志、排查 token/签名/回调/跨域/WebSocket 握手问题。Unity 界面、微信 SDK 生命周期、按钮防连点、广告实例全局唯一、支付弹窗和订阅授权弹窗属于前端实现范围。

## 15. 签到和装备仓库接口

签到接口不接收 `playerId`，玩家身份始终从 `Authorization` 对应的 token 获取。一次领取事务只查询一次数据库北京时间，得到不可变的 `businessTime`；即使请求发生在跨日边界，也以该 SQL 实际执行时刻所属日期为准，不认客户端点击时间或设备时间。

### 15.1 查询签到概览

```http
GET /api/sign-in/overview
Authorization: Bearer <token>
X-Client-Version-Code: 10005
```

请求体：无。

成功示例（为控制篇幅，`days` 和 `rewards` 数组只展示部分元素；实际接口返回完整数组）：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "contractVersion": 2,
    "businessTime": "2026-07-17 17:30:00",
    "plans": [
      {
        "planCode": "NEWBIE",
        "planVersion": 2,
        "minimumClientVersionCode": 10005,
        "planName": "新手签到",
        "planType": "NEWBIE_WINDOW",
        "periodKey": "LIFETIME",
        "rewardMode": "CLAIM_SEQUENCE",
        "rewardWindowDays": 3,
        "currentDay": 2,
        "windowDays": 30,
        "expiresAt": "2026-08-15 00:00:00",
        "canClaim": true,
        "allowMakeup": false,
        "canMakeup": false,
        "makeupCost": null,
        "days": [
          {
            "rewardDay": 1,
            "status": "CLAIMED",
            "claimed": true,
            "canClaim": false,
            "canMakeup": false,
            "claimedAt": "2026-07-16 10:00:00",
            "rewards": []
          },
          {
            "rewardDay": 2,
            "status": "AVAILABLE",
            "claimed": false,
            "canClaim": true,
            "canMakeup": false,
            "claimedAt": null,
            "rewards": [
              {
                "rewardSeq": 1,
                "rewardType": "EQUIPMENT",
                "rewardKey": "starter_sword",
                "rewardAmount": 1,
                "fallbackCondition": null,
                "fallbackRewardType": null,
                "fallbackRewardKey": null,
                "fallbackRewardAmount": null
              }
            ]
          }
        ]
      }
    ]
  },
  "accountStatus": "ACTIVE",
  "accountManagementOnly": false,
  "privacyConsentRequired": false,
  "privacyPolicyVersion": "2026.08.05"
}
```

日期状态：`CLAIMED` 已领取、`MISSED` 已错过、`AVAILABLE` 当前可领取、`LOCKED` 尚未到达的日历日期、`UNAVAILABLE` 当前档位没有启用奖励配置。是否允许补领必须读取当天的 `canMakeup`，不能只根据 `MISSED` 推断。计划级 `allowMakeup` 表示开关，`canMakeup` 表示当前至少有一天可补，`makeupCost` 返回单次费用的 `itemKey/itemName/icon/amount`；关闭时费用为 `null`。

`rewardMode` 决定 `days[]` 的语义：

- `CLAIM_SEQUENCE`：`rewardWindowDays` 是可领取奖励档位数，`days[]` 长度等于该值；`rewardDay=1/2/3` 表示第 1/2/3 次成功领取，不代表活动自然日。参与窗口由 `windowDays`（新手）或活动快照起止时间（活动签到）决定。玩家可在参与窗口内的任意自然日领取下一档，断签不丢档；同一服务端自然日最多成功领取一次。全部档位领取后，`currentDay` 为 `rewardWindowDays + 1`，`canClaim=false`。
- `CALENDAR_DAY`：`rewardDay` 表示参与窗口中的自然日序号，`days[]` 按日历奖励周期返回；没有启用奖励的日期可返回 `UNAVAILABLE`。

新手示例：参与窗口 30 天、奖励只配置 3 档时使用 `windowDays=30`、`rewardMode=CLAIM_SEQUENCE`、`rewardWindowDays=3`，Unity 只渲染 3 个 `days[]` 档位，不生成第 4～30 天的空格。活动签到使用相同语义，活动起止时间只决定参与期限，不要求为活动的每个自然日配置奖励。月度计划的 `rewardMode` 和 `rewardWindowDays` 为 `null`，仍按当月实际天数返回 28 至 31 个日历日期。`expiresAt` 始终采用左闭右开语义，参与窗口终点到达即失效。

#### 15.1.1 月度每日签到接入示例

以下示例保留完整响应包裹、计划字段和一项完整奖励字段，**仅截取月度 `days[]` 中的第 28 日**，不是实际只返回一天。2026 年 9 月真实响应应包含第 1～30 日；其他月份按当月天数返回 28～31 项。名称、资源 ID、数量、品质和版本值仅作展示，必须以本环境接口返回为准。示例只有月度计划时 `data.contractVersion=1`；同时有协议版本 2 的有效计划时，概览取所有返回计划所需协议版本的最大值。

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "contractVersion": 1,
    "businessTime": "2026-09-28 17:30:00",
    "plans": [
      {
        "planVersion": null,
        "minimumClientVersionCode": 0,
        "planCode": "MONTHLY",
        "planName": "月度签到",
        "planType": "MONTHLY_CALENDAR",
        "periodKey": "2026-09",
        "currentDay": 28,
        "windowDays": null,
        "rewardMode": null,
        "rewardWindowDays": null,
        "expiresAt": "2026-10-01 00:00:00",
        "canClaim": true,
        "allowMakeup": false,
        "canMakeup": false,
        "makeupCost": null,
        "days": [
          {
            "rewardDay": 28,
            "status": "AVAILABLE",
            "claimed": false,
            "canClaim": true,
            "canMakeup": false,
            "claimedAt": null,
            "rewards": [
              {
                "rewardSeq": 1,
                "rewardType": "ITEM",
                "rewardKey": "gold",
                "rewardAssetId": 1001,
                "rewardName": "金币",
                "rewardIcon": "gold",
                "rewardCategory": "CURRENCY",
                "rewardAmount": 100,
                "rewardQuality": "COMMON",
                "fallbackCondition": null,
                "fallbackRewardType": null,
                "fallbackRewardKey": null,
                "fallbackAssetId": null,
                "fallbackRewardName": null,
                "fallbackRewardIcon": null,
                "fallbackRewardCategory": null,
                "fallbackRewardAmount": null,
                "fallbackRewardQuality": null
              }
            ]
          }
        ]
      }
    ]
  }
}
```

| Unity 读取位置 | 正确处理 |
| --- | --- |
| `data.contractVersion` | 概览级协议版本；不要从响应根节点或某个计划内读取 |
| `data.plans[].planCode` | 月度计划为 `MONTHLY`；领取时回传此编码 |
| `data.plans[].planType` | 月度类型为 `MONTHLY_CALENDAR`，不是 `MONTHLY` |
| `planVersion` | 未绑定不可变版本的月度计划可能为 `null`，不是配置缺失，也不是管理端正式配置版本号 |
| `windowDays/rewardMode/rewardWindowDays` | 月度均可为 `null`；C# DTO 使用可空数字/字符串字段，不用这些字段决定月历是否显示 |
| `days[]` / `currentDay` | 遍历返回数组生成格子；`currentDay` 是服务端当前月度日期，不是已领取次数 |
| `canClaim/canMakeup` | 控制领取按钮、补签按钮和红点，不控制整个日历是否显示；当天已领仍展示已领及后续日期 |
| `days[].rewards[]` | 每天可有多个奖励；使用 `rewardName/rewardIcon/rewardAmount/rewardQuality` 展示，不额外查资源配置 |
| `days[].claimedAt` | 已领为服务端领取时间，未领为 `null`；`rewardQuality` 为 `null` 的历史资产使用通用品质样式 |
| `expiresAt` | 月度为下月 1 日 00:00:00，跨月后重新查询概览，不永久隐藏月度签到 |

月度普通领取请求为 `POST /api/sign-in/claim`，正文 `{"planCode":"MONTHLY"}`；同样携带 token 和 `X-Client-Version-Code`。不能向普通领取接口提交 `rewardDay`。成功后重新请求概览；补签使用第 15.3 节的独立接口。

#### 15.1.2 空白页面与错误响应排查

1. **成功判定需要两层检查**：HTTP 成功不等于业务成功。只有 HTTP 成功、正文 `code == 200` 且概览 `data != null`，才能解析计划；当前异常映射也可能出现 HTTP 200 但正文 `code=500/data=null`。失败时显示错误和重试入口，记录接口、HTTP 状态、业务码及 message，不记录 token。
2. **版本门禁作用于整份概览**：三个签到接口均携带真实构建号 `X-Client-Version-Code`。有效计划中任意一个最低版本不满足时，概览整体返回 HTTP 426、业务码 `6009`，不会单独返回其余月度计划。即使月度最低版本为 `0`，也不能忽略同时生效的新手或活动计划的版本要求。
3. **只在成功响应中判断计划缺失**：检查 `data.plans[]` 是否包含 `planCode=MONTHLY`。缺失时核对后端正式月度计划是否启用、配置是否已发布；管理端未保存/未发布的开关不等于运行状态。
4. **计划存在仍空白时核对解析**：不要用 `planType=="MONTHLY"`、`rewardWindowDays>0`、`rewardMode!=null` 或 `canClaim==true` 作为月历可见条件。先渲染返回的 `days[]`，再决定各按钮状态；空数组要有诊断提示。
5. **配置覆盖与日期由服务端决定**：指定月份某天只要有启用奖励，就覆盖那天的 DEFAULT 奖励；不存在覆盖时使用默认配置。Unity 不自行合并配置、不用设备时区推算奖励，也不写死 31 天。

### 15.2 领取当天签到奖励

```http
POST /api/sign-in/claim
Content-Type: application/json
Authorization: Bearer <token>
X-Client-Version-Code: 10005
```

```json
{
  "planCode": "NEWBIE"
}
```

`planCode` 与 `planType` 是不同字段，领取时应原样使用概览中当前计划的 `planCode`：

| `planCode` | `planType` | 规则 |
| --- | --- | --- |
| `NEWBIE` | `NEWBIE_WINDOW` | 首次成功登录时冻结参与窗口 `windowDays`；`CLAIM_SEQUENCE` 下按下一奖励档位领取，参与窗口内可断签，不要求每天连续；`CALENDAR_DAY` 下按窗口自然日领取 |
| 概览返回的活动计划编码 | `ACTIVITY_CALENDAR` | 使用已发布活动的起止时间作为参与窗口快照；奖励档位和领取方式由 `rewardWindowDays/rewardMode` 决定，不能跨活动或跨窗口领取；不要把类型名 `ACTIVITY_CALENDAR` 当作固定计划编码 |
| `MONTHLY` | `MONTHLY_CALENDAR` | `periodKey=yyyy-MM`，只领取北京时间当天对应的日期；不能用 `planType == "MONTHLY"` 判断月度计划 |

前端点击签到按钮时，普通领取请求体只有当前计划的 `planCode`：新手礼包按钮传 `NEWBIE`，活动签到传活动计划编码，月度签到传 `MONTHLY`。`playerId`、领取日期、奖励档位、奖励数量和是否补领都不能由客户端提交；玩家身份来自请求头 token，服务端按冻结快照、`rewardMode` 和数据库业务时间计算下一档或当天奖励。`CLAIM_SEQUENCE` 不接受客户端提交 `rewardDay` 来跳档。

成功返回中的 `rewards` 同时保留配置奖励和实际奖励。NPC 未重复时二者相同；NPC 已拥有时，`configuredRewardType=NPC`、`grantedRewardType=ITEM`、`fallbackApplied=true`。`items` 是发奖后的最新背包列表；装备实例通过仓库接口查询。

```json
{
  "code": 200,
  "message": "领取成功",
  "data": {
    "signInRecordId": 1001,
    "planCode": "NEWBIE",
    "periodKey": "LIFETIME",
    "claimType": "NORMAL",
    "rewardDay": 3,
    "claimDate": "2026-07-17",
    "businessTime": "2026-07-17 17:30:00",
    "rewards": [
      {
        "rewardSeq": 1,
        "configuredRewardType": "NPC",
        "configuredRewardKey": "field_steward",
        "configuredRewardAmount": 1,
        "grantedRewardType": "ITEM",
        "grantedRewardKey": "diamond",
        "grantedRewardAmount": 100,
        "fallbackApplied": true
      }
    ],
    "cost": null,
    "items": []
  },
  "accountStatus": "ACTIVE",
  "accountManagementOnly": false,
  "privacyConsentRequired": false,
  "privacyPolicyVersion": "2026.08.05"
}
```

失败业务码：

| code | message | 含义 |
| --- | --- | --- |
| 6001 | 签到计划不存在或未启用 | `planCode` 错误或计划关闭 |
| 6002 | 当天奖励已经领取 | 重复点击或并发请求，奖励不会重复发放 |
| 6003 | 签到活动已结束 | 新手活动已超过配置窗口 |
| 6004 | 当天没有可领取奖励 | 当天没有启用的有效奖励行 |
| 6005 | 签到奖励配置错误 | 奖励编码、数量、顺序、fallback 或装备上限配置无效 |
| 6006 | 补签功能未开启 | 当前计划没有开启补签 |
| 6007 | 只能补签当前周期内早于今天的未领取日期 | 日期不是当前周期的已错过未领取日 |
| 6008 | 补签所需资源不足 | 玩家背包中的配置资源不足 |
| 6009 | 当前客户端版本不支持该签到计划 | 缺少、非法或低于 `minimumClientVersionCode`；HTTP 状态为 426，Unity 必须进入升级流程 |

### 15.3 补签指定日期

```http
POST /api/sign-in/makeup
Content-Type: application/json
Authorization: Bearer <token>
X-Client-Version-Code: 10005
```

```json
{
  "planCode": "MONTHLY",
  "rewardDay": 5
}
```

`rewardDay` 只能是当前活动周期内早于今天、存在有效奖励且尚未领取的日期。客户端不能提交费用、奖励或玩家 ID；服务端从 `sign_in_plan` 读取补签开关和费用，先锁定玩家，再原子扣费、写领取记录和发放奖励。成功响应结构与普通领取相同，但 `claimType="MAKEUP"`，`cost` 是本次真实扣除的不可变费用快照：

```json
{
  "code": 200,
  "message": "补签成功",
  "data": {
    "signInRecordId": 1002,
    "planCode": "MONTHLY",
    "periodKey": "2026-07",
    "claimType": "MAKEUP",
    "rewardDay": 5,
    "claimDate": "2026-07-23",
    "businessTime": "2026-07-23 18:30:00",
    "rewards": [],
    "cost": {
      "itemKey": "diamond",
      "itemName": "钻石",
      "icon": "diamond",
      "amount": 20
    },
    "items": []
  }
}
```

同一天的普通签到和补签使用不同的领取语义：普通签到每天最多一次；补签按“玩家 + 计划 + 周期 + 奖励日”唯一。配置日后变化不会改写历史记录中的 `costItemKeySnapshot/costItemAmountSnapshot`。

### 15.4 查询装备仓库

```http
GET /api/equipment/warehouse
Authorization: Bearer <token>
```

请求体和查询参数：无。该接口只能查询 token 所属玩家，不能指定其他玩家 ID。

`equipment_config.enabled` 只控制该装备能否继续用于新奖励发放。配置后来被停用时，玩家此前已经获得的装备实例仍会由本接口返回，不会从仓库列表中消失。

```json
{
  "code": 200,
  "message": "操作成功",
  "data": [
    {
      "playerEquipmentId": 501,
      "equipmentId": 4001,
      "equipmentKey": "starter_sword",
      "name": "新手短剑",
      "equipmentType": "WEAPON",
      "quality": "COMMON",
      "icon": "starter_sword",
      "modelKey": "starter_sword",
      "description": "新手装备，可通过签到奖励获得。",
      "attributesJson": null,
      "level": 1,
      "status": "WAREHOUSE",
      "locked": false,
      "healthBonus": 0,
      "attackBonus": 10,
      "defenseBonus": 0,
      "acquireTime": "2026-07-17 17:30:00"
    }
  ]
}
```

### 15.5 配置和并发规则

1. 第 2 天不是 Java 写死的五件装备。`sign_in_reward_config` 可配置任意已启用 `equipment_config.equipment_key` 和任意正数量，装备类型也可扩展；同一奖励档位的装备数量总和不得超过 `sign_in_plan.max_equipment_per_claim`。
2. 同一奖励档位启用的 `reward_seq` 必须是 `1..N` 连续序号。服务端在写领取记录前一次性校验全部物品、装备、NPC、fallback 和总量，任何配置无效都不会先发部分奖励。
3. 月度查询用一条 SQL 同时读取 `DEFAULT` 和当前 `yyyy-MM` 候选配置。指定月份当天只要存在启用配置，就整日覆盖默认配置，不进行合并。
4. 玩家 `users` 行使用 `SELECT ... FOR UPDATE` 锁定，数据库还有“玩家+计划+周期+奖励档位”和“玩家+计划+领取自然日”两个唯一键。`CLAIM_SEQUENCE` 在参与窗口内不要求连续日期，但同一自然日仍最多成功领取一次；6 个并发领取请求的真实 MySQL 测试结果是 1 个成功、其余返回业务异常，资源只入账一次。
5. 主领取记录、背包物品、装备实例、NPC、fallback 和实际奖励流水都在同一个事务内。任一写入失败会整体回滚。
6. `users.first_login_time` 在首次成功签发 token 的事务中通过 `WHERE first_login_time IS NULL` 原子初始化，之后不修改。V6 升级时已有账号使用原 `create_time` 回填，不会被当成当天新玩家。
7. V1 至 V37 成功执行后禁止直接修改迁移文件。以后正式调整奖励、装备、补偿或上限，应从 V38 开始新增迁移；本地临时联调才可直接更新测试数据库。详细 SQL 示例见 `docs/database-script-guide.md`。
8. V6 默认共有 42 条奖励配置记录，因为一条记录表示一项奖励而不是一天：新手第 1 天 5 条资源、第 2 天 5 条装备、第 3 天 1 条 NPC，加上月度 1 至 31 日各 1 条，共 `5+5+1+31=42` 条。
9. `ITEM` 和 NPC 的 ITEM fallback 数量可配置为正数，但当前没有计划级物品数量上限。不要配置接近 Java/MySQL 有符号 `BIGINT` 上限的数值；玩家现有数量与奖励相加溢出时，数据库会拒绝写入并使整次领取事务回滚。
10. 新手、活动和月度计划彼此独立。`NEWBIE` 的参与期限由玩家冻结实例的 `windowDays` 决定，奖励档位由 `rewardWindowDays/rewardMode` 决定；活动计划的参与期限来自活动起止时间快照，奖励档位同样独立。`CLAIM_SEQUENCE` 允许参与窗口内断签领取下一档；`expiresAt` 到达后新手/活动计划不再显示或领取。月度计划仍可在每个北京时间自然日领取一次。
11. 补签开启时，费用物品和正数量必须同时配置；发布配置会验证物品已启用。每次补签把费用编码和数量写入领取快照，后续调价不会追溯修改历史扣费。

### 15.6 Unity 推荐流程

1. 登录成功进入主界面后调用 `GET /api/sign-in/overview`，携带 `X-Client-Version-Code`；检查 HTTP 状态及响应正文 `code == 200`、`data != null` 后，再读取 `data.contractVersion` 和 `data.plans`。
2. 按 `data.plans[].days[]` 和每项 `rewards[]` 动态渲染，月度类型为 `MONTHLY_CALENDAR`。根据 `plans[].canClaim/canMakeup` 显示红点，根据 `days[].status/canClaim/canMakeup` 控制状态及按钮；不能把 `canClaim=false` 当成隐藏整个计划的条件，也不能因月度 `windowDays/rewardMode/rewardWindowDays` 为空而隐藏日历。`CLAIM_SEQUENCE` 以 `rewardDay` 作为领取档位，不把未领取档位误显示为活动日期。
3. 玩家点击领取时只提交 `planCode`，按钮在请求期间禁用，不能自行提交日期、奖励或玩家 ID。
4. 成功后使用响应 `items` 刷新背包；如果 `rewards` 中存在 `grantedRewardType=EQUIPMENT`，再调用装备仓库接口刷新装备列表；NPC 奖励成功时刷新玩家 NPC 列表。
5. `6002` 按已领取处理并重新查询概览；`6005` 属于服务端配置错误，前端只提示暂时无法领取并上报日志。
6. 玩家点击可补日期时调用 `POST /api/sign-in/makeup`，只提交该计划和 `rewardDay`；成功后使用 `cost` 展示真实消耗并重新查询概览。
7. HTTP 426、业务码 `6009` 时进入升级流程；其他业务失败显示错误并保留重试入口，不要把失败响应中的 `data=null` 当作“没有签到配置”。普通领取成功后同样重新请求概览，刷新已领取状态和 `claimedAt`。

本次签到奖励 SQL 的表别名修复不改变上述接口地址、请求参数或响应字段，也不涉及数据库结构变化或新增 Flyway 迁移。部署后需重启后端进程以重新加载 Mapper；Unity 已按本节契约接入时，无需因本次后端修复改代码。尚未按正确字段层级、月度类型或可空字段接入的客户端，仍需按本节修正；无 Unity 工程时不能代替客户端完成运行验收。

## 16. NPC 成长、装备成长和可配置卡池抽奖接口

本章接口都需要 token，玩家身份只从 `Authorization` 获取，前端不能传 `playerId`。原 NPC/装备单级接口和 V1 一键升级接口继续保留；装备单级升级不能跨突破点，V1 一键升级停在突破点前，V2 含突破点的路径整体拒绝 `6210`，只有非突破路径可原子执行。单次手选突破接口接受同款任意等级材料，每件固定占一个材料槽，高于 1 级时必须二次确认不可逆损失。三类操作的目标均允许保持穿戴，只有被消耗的材料必须先卸下。无论哪条接口，目标等级、金币、碎片、同款装备材料、额外物品和属性均由服务端读取数据库正式配置，客户端不能自行计算或修改。

NPC 和装备最高等级当前均为 100。突破等级不再由 Unity 或 Java 固定判断，而是由管理端正式配置决定：目标等级在 `npc_breakthrough_config` 或 `equipment_breakthrough_config` 中存在启用配置时才属于突破。NPC 突破必须通过独立手动接口并额外消耗该 NPC 的专属碎片；装备必须先手动突破，允许同款任意等级材料，每件只占一个材料槽。普通 NPC/装备升级和 V1/V2 一键升级不自动消耗突破材料。没有启用突破配置的目标等级按普通升级处理。

NPC 战斗等级和穿戴装备只影响 `health/attack/defense` 战斗属性。建筑增产始终只读取 `npc_config.produce_bonus_rate`，不会随 NPC 升级或装备变化而增加。

### 16.1 NPC 升级

```http
POST /api/npc/upgrade
Content-Type: application/json
Authorization: Bearer <token>
```

```json
{
  "playerNpcId": 2
}
```

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| playerNpcId | number | 是 | 当前玩家拥有的 NPC 实例 ID，对应 `player_npc.id` |

普通升级请求到达突破点时返回以下阻断结果（不会自动突破）：

```json
{
  "code": 6109,
  "message": "当前NPC已达到突破门槛，请先完成突破",
  "data": null
}
```

规则：

1. `playerNpcId` 必须属于 token 玩家，不能升级其他玩家的 NPC。
2. 每次请求只从当前等级提升到下一等级，不能由客户端指定目标等级或连续升级次数。
3. 服务端先锁玩家行，再锁 NPC 实例行；普通等级只扣金币。目标等级命中启用的 NPC 突破配置时返回 `6109`，不扣金币、不扣碎片、不更新等级。
4. 到达突破点必须先调用 NPC 突破预览和执行接口；突破事务会同时扣除该级金币和专属碎片，并写入 `npc_upgrade_record` 及幂等审计快照。
5. NPC 正在建筑中入驻时也可以升级。升级不会结算、重置或改变建筑生产时钟，也不会改变 `produceBonusRate`。

### 16.1A NPC 手动突破

`GET /api/npc/{playerNpcId}/breakthrough-preview` 返回当前等级的下一突破等级、金币、专属碎片、余额、目标属性和 `planFingerprint`。随后调用 `POST /api/npc/breakthrough`，必须携带标准 UUID `Idempotency-Key`，请求体如下：

```json
{
  "playerNpcId": 2,
  "expectedCurrentLevel": 19,
  "targetLevel": 20,
  "planFingerprint": "<预览返回的64位指纹>"
}
```

服务端重新锁定玩家、NPC、配置和背包余额后原子扣除并升级；同一幂等键只会成功一次。突破成功后 Unity 刷新 NPC 等级、属性、金币和碎片余额，之后才能再次调用普通升级或一键升级。NPC 一键升级遇到突破点时只提交突破前的普通升级前缀，返回 `outcome=BREAKTHROUGH_REQUIRED`，不得自动扣除突破碎片。

### 16.2 装备升级

升级前调用 `GET /api/equipment/{playerEquipmentId}/upgrade-preview` 获取金币、同款装备和额外仓库资源费用；预览不锁定或预留资源，提交时仍以升级事务为准。

```http
POST /api/equipment/upgrade
Content-Type: application/json
Authorization: Bearer <token>
```

```json
{
  "playerEquipmentId": 501
}
```

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| playerEquipmentId | number | 是 | 当前玩家拥有的装备实例 ID，对应 `player_equipment.id` |

成功示例，以下展示新手短剑从 19 级突破到 20 级：

```json
{
  "code": 200,
  "message": "装备升级成功",
  "data": {
    "upgradeRecordId": 8001,
    "playerEquipmentId": 501,
    "equipmentId": 4001,
    "equipmentKey": "starter_sword",
    "name": "新手短剑",
    "fromLevel": 19,
    "level": 20,
    "status": "EQUIPPED",
    "goldCost": 3120,
    "sameEquipmentCount": 1,
    "consumedPlayerEquipmentIds": [508],
    "remainingGold": 2980,
    "resourceCosts": [
      {
        "itemKey": "stone",
        "itemName": "石材",
        "amount": 50,
        "remainingAmount": 70
      }
    ],
    "healthBonus": 0,
    "attackBonus": 67,
    "defenseBonus": 0
  }
}
```

规则：

1. 升级目标可以处于 `WAREHOUSE` 或 `EQUIPPED`，已穿戴装备升级成功后，其 NPC 装备面板会立即按新等级属性重新计算。
2. 这是兼容旧客户端的 V1 单级接口，只升一级；下一等级是启用突破点时返回 `6210`，不自动消耗材料。必须改用 16.2C 的手选突破接口，材料与目标 `equipment_id` 相同、允许任意等级、状态为 `WAREHOUSE`、未锁定、未穿戴，且不能是目标自身。目标已穿戴不受此材料限制。
3. 服务端按实例 ID 升序选择并使用 `FOR UPDATE` 锁定材料。锁顺序固定为玩家、升级目标、材料，防止并发升级或穿戴重复使用材料。
4. 材料会更新为 `CONSUMED`，并记录在 `equipment_upgrade_material_record`；额外仓库物品消耗会记录在 `equipment_upgrade_resource_cost_record`；消耗后不会出现在仓库中。
5. 普通等级返回 `sameEquipmentCount=0` 和空的 `consumedPlayerEquipmentIds`；未配置额外仓库资源时 `resourceCosts` 为空数组。

### 16.2C 装备手选突破（任意等级材料）

手选突破用于一次只提升一级、但需要玩家明确指定材料的场景。它与旧的 `POST /api/equipment/upgrade` 相互独立：旧接口仍由服务端自动选择 1 级材料，本接口允许同款任意等级材料。

#### 16.2C.1 查询材料候选

```http
GET /api/equipment/{playerEquipmentId}/breakthrough-materials?page=1&pageSize=50
Authorization: Bearer <token>
```

响应中的 `items` 按“材料等级升序 → 获得时间升序 → 实例 ID 升序”排列。候选只是查询时快照，执行时仍会重新锁定并校验；`level>1` 的条目会返回 `hasCultivationInvestment=true`。

```json
{
  "code": 200,
  "data": {
    "contractVersion": 2,
    "total": 3,
    "page": 1,
    "pageSize": 50,
    "items": [
      {"playerEquipmentId": 508, "equipmentKey": "starter_sword", "level": 1, "selectable": true, "hasCultivationInvestment": false},
      {"playerEquipmentId": 509, "equipmentKey": "starter_sword", "level": 5, "selectable": true, "hasCultivationInvestment": true}
    ]
  }
}
```

#### 16.2C.2 预览材料选择

```http
POST /api/equipment/breakthrough-preview
Content-Type: application/json
Authorization: Bearer <token>
```

```json
{
  "playerEquipmentId": 501,
  "expectedCurrentLevel": 19,
  "targetLevel": 20,
  "planFingerprint": "预览返回的64位小写SHA-256",
  "materialPlayerEquipmentIds": [508, 509]
}
```

服务端严格校验材料数量必须等于当前突破配置；每个 ID 必须属于当前玩家、与目标同款、在仓库、未锁定、未穿戴、未被消耗且不能是目标装备。响应返回 `materialSelectionFingerprint` 和每件材料的等级快照。只要材料等级、状态、目标等级或正式配置发生变化，执行时指纹就会失效。

选择预览成功时，`data` 完整字段如下：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `contractVersion` | integer | 合同版本，固定为 `2`。 |
| `playerEquipmentId` | integer(int64) | 目标玩家装备实例 ID。 |
| `currentLevel` | integer | 服务端已校验的当前等级。 |
| `targetLevel` | integer | 本次必须到达的下一突破等级。 |
| `requiredMaterialCount` | integer | 本次必须提交的材料件数，也是 Unity 应动态生成的材料框数量。 |
| `highLevelMaterialCount` | integer | 已选材料中 `level > 1` 的件数。 |
| `requiresHighLevelConfirmation` | boolean | 存在任意 `level > 1` 材料时为 `true`。 |
| `materialPolicy` | string | 固定为 `SAME_EQUIPMENT_ANY_LEVEL_ONE_SLOT_EACH`。 |
| `refundPolicy` | string | 固定为 `NONE`。 |
| `warning` | string | 服务端返回的永久消耗警告文案。 |
| `planFingerprint` | string | 已重新校验的升级计划指纹。 |
| `materialSelectionFingerprint` | string | 包含目标状态和材料等级、状态、锁定快照的 64 位小写 SHA-256；执行时必须原样回传。 |
| `selectedMaterials` | array | 已校验材料快照，按实例 ID 升序；字段结构见下表。 |

`selectedMaterials[]`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `targetLevel` | integer | 该材料用于到达的突破目标等级。 |
| `selectionOrder` | integer | 从 `1` 开始的稳定顺序。 |
| `playerEquipmentId` | integer(int64) | 材料玩家装备实例 ID。 |
| `equipmentId` | integer(int64) | 材料装备配置 ID，必须与目标装备相同。 |
| `equipmentKey` | string | 装备稳定编码。 |
| `name` | string | 装备名称快照。 |
| `icon` | string \| null | 装备图标资源键。 |
| `quality` | string | 品质编码。 |
| `level` | integer | 预览时的材料等级快照。 |
| `hasCultivationInvestment` | boolean | `level > 1` 时为 `true`。 |

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "contractVersion": 2,
    "playerEquipmentId": 501,
    "currentLevel": 19,
    "targetLevel": 20,
    "requiredMaterialCount": 2,
    "highLevelMaterialCount": 1,
    "requiresHighLevelConfirmation": true,
    "materialPolicy": "SAME_EQUIPMENT_ANY_LEVEL_ONE_SLOT_EACH",
    "refundPolicy": "NONE",
    "warning": "选中的装备及其已投入金币、资源和突破材料将在成功后永久消耗，且不返还",
    "planFingerprint": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    "materialSelectionFingerprint": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
    "selectedMaterials": [
      {
        "targetLevel": 20,
        "selectionOrder": 1,
        "playerEquipmentId": 508,
        "equipmentId": 4001,
        "equipmentKey": "starter_sword",
        "name": "新手剑",
        "icon": "equipment/starter_sword",
        "quality": "FINE",
        "level": 1,
        "hasCultivationInvestment": false
      },
      {
        "targetLevel": 20,
        "selectionOrder": 2,
        "playerEquipmentId": 509,
        "equipmentId": 4001,
        "equipmentKey": "starter_sword",
        "name": "新手剑",
        "icon": "equipment/starter_sword",
        "quality": "FINE",
        "level": 5,
        "hasCultivationInvestment": true
      }
    ]
  }
}
```

#### 16.2C.3 执行手选突破

```http
POST /api/equipment/breakthrough
Authorization: Bearer <token>
Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
Content-Type: application/json
```

请求体与预览相同，并增加两个确认字段：

```json
{
  "playerEquipmentId": 501,
  "expectedCurrentLevel": 19,
  "targetLevel": 20,
  "planFingerprint": "预览返回值",
  "materialSelectionFingerprint": "预览返回值",
  "materialPlayerEquipmentIds": [508, 509],
  "confirmIrreversibleMaterialConsumption": true,
  "confirmHighLevelMaterialLoss": true
}
```

`confirmIrreversibleMaterialConsumption` 始终必须为 `true`；选中任何 `level>1` 材料时，`confirmHighLevelMaterialLoss` 也必须为 `true`。成功后材料统一变为 `CONSUMED`，材料自身及其已经投入的金币、资源和突破材料永不返还。整个扣费、材料消费、目标升级和审计在一个事务中完成；失败全部回滚。相同玩家复用相同 UUID 幂等键和完全相同请求会重放首次响应，同键异载荷返回 `6208`。

执行成功或幂等重放时，`message="装备突破成功"`，`data` 完整字段如下：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `contractVersion` | integer | 合同版本，固定为 `2`。 |
| `requestNo` | string(UUID) | 服务端生成的突破请求号；幂等重放保持首次值。 |
| `upgradeRecordId` | integer(int64) | 本次装备升级审计流水 ID。 |
| `playerEquipmentId` | integer(int64) | 已突破的目标装备实例 ID。 |
| `equipmentId` | integer(int64) | 装备配置 ID。 |
| `equipmentKey` | string | 装备稳定编码。 |
| `name` | string | 装备名称快照。 |
| `quality` | string | 品质编码。 |
| `fromLevel` | integer | 执行前等级。 |
| `level` | integer | 执行后的等级，单次突破成功时等于请求的 `targetLevel`。 |
| `status` | string | 目标装备当前状态，保持原 `WAREHOUSE` 或 `EQUIPPED`。 |
| `goldCost` | integer(int64) | 本次实际扣除金币。 |
| `materialCount` | integer | 本次实际消耗材料件数。 |
| `highLevelMaterialCount` | integer | 实际消耗且 `level > 1` 的材料件数。 |
| `refundPolicy` | string | 固定为 `NONE`。 |
| `consumedMaterials` | array | 实际消耗的不可变材料快照；结构与本节 `selectedMaterials[]` 相同。 |
| `remainingGold` | integer(int64) | 扣除后的金币余额。 |
| `resourceCosts` | array | 实际额外物品扣除结果；字段结构见下表。 |
| `healthBonus` | integer(int64) | 到达目标等级后的生命加成。 |
| `attackBonus` | integer(int64) | 到达目标等级后的攻击加成。 |
| `defenseBonus` | integer(int64) | 到达目标等级后的防御加成。 |
| `speedBonus` | integer(int64) | 到达目标等级后的速度加成。 |
| `businessTime` | string(datetime) | 数据库业务时间；幂等重放保持首次成功值。 |

`resourceCosts[]`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `itemKey` | string | 实际扣除物品的稳定编码。 |
| `itemName` | string | 物品名称快照。 |
| `amount` | integer(int64) | 本次实际扣除数量。 |
| `remainingAmount` | integer(int64) | 扣除后的玩家持有数量。 |

```json
{
  "code": 200,
  "message": "装备突破成功",
  "data": {
    "contractVersion": 2,
    "requestNo": "6de9766e-3a55-4d7f-bb7b-7d92e3a6c608",
    "upgradeRecordId": 8002,
    "playerEquipmentId": 501,
    "equipmentId": 4001,
    "equipmentKey": "starter_sword",
    "name": "新手剑",
    "quality": "FINE",
    "fromLevel": 19,
    "level": 20,
    "status": "WAREHOUSE",
    "goldCost": 1000,
    "materialCount": 2,
    "highLevelMaterialCount": 1,
    "refundPolicy": "NONE",
    "consumedMaterials": [
      {
        "targetLevel": 20,
        "selectionOrder": 1,
        "playerEquipmentId": 508,
        "equipmentId": 4001,
        "equipmentKey": "starter_sword",
        "name": "新手剑",
        "icon": "equipment/starter_sword",
        "quality": "FINE",
        "level": 1,
        "hasCultivationInvestment": false
      },
      {
        "targetLevel": 20,
        "selectionOrder": 2,
        "playerEquipmentId": 509,
        "equipmentId": 4001,
        "equipmentKey": "starter_sword",
        "name": "新手剑",
        "icon": "equipment/starter_sword",
        "quality": "FINE",
        "level": 5,
        "hasCultivationInvestment": true
      }
    ],
    "remainingGold": 4000,
    "resourceCosts": [
      {
        "itemKey": "stone",
        "itemName": "石材",
        "amount": 50,
        "remainingAmount": 70
      }
    ],
    "healthBonus": 2000,
    "attackBonus": 800,
    "defenseBonus": 400,
    "speedBonus": 100,
    "businessTime": "2026-09-23 15:30:00"
  }
}
```

### 16.2A NPC/装备一键升级预览

```http
GET /api/npc/{playerNpcId}/auto-upgrade-preview?targetLevel=50
GET /api/equipment/{playerEquipmentId}/auto-upgrade-preview?targetLevel=50
Authorization: Bearer <token>
```

`targetLevel` 必须大于实例当前等级，且不能超过模板 `maxLevel` 和服务端硬上限 100。预览会完整加载当前等级到目标等级的属性、金币、启用突破点和启用额外费用，再按当前余额计算连续可支付前缀；预览不锁定、不扣除、也不预留任何资源。

两个响应都会返回 `currentLevel`、`requestedTargetLevel`、`payableTargetLevel`、`upgradeLevelCount`、`targetReached`、`outcome`、`stopReason`、`blockedLevel`、`blockedRequirements`、`planFingerprint` 和 `upgradeSteps`。`planFingerprint` 是服务端根据实际正式配置生成的 64 位小写 SHA-256，确认执行时必须原样提交。

`upgradeSteps` 逐级返回属性 `before/after/delta`，Unity 可以直接展示每一级提高的属性或逐级播放动画。NPC 步骤返回生命、攻击、防御、速度；装备步骤返回对应四项加成。装备预览不会提前选择材料实例，因此步骤中的 `consumedPlayerEquipmentIds` 固定为空数组。

### 16.2B NPC/装备一键升级执行

```http
POST /api/npc/auto-upgrade
POST /api/equipment/auto-upgrade
Authorization: Bearer <token>
Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
Content-Type: application/json
```

NPC 请求：

```json
{
  "playerNpcId": 2,
  "expectedCurrentLevel": 1,
  "targetLevel": 50,
  "planFingerprint": "预览返回的64位小写SHA-256"
}
```

装备请求把 `playerNpcId` 替换为 `playerEquipmentId`。`Idempotency-Key` 必须是标准 UUID；同一玩家、同一 Key、同一请求会永久重放第一次完整或部分成功响应，不重新扣费。同键异载荷返回 `6107 / 6208`。

执行事务固定遵守现有全局锁序：玩家聚合锁 → 幂等历史 → 经济守卫 → 目标实例 → 配置共享锁 → 同款装备材料 ID 升序 → 背包物品 ID 升序。材料先于背包的顺序与原单级装备升级保持一致，避免新增接口形成反向锁序；同一玩家的全部经济写事务都先持有玩家聚合锁。服务端先验证整个目标区间配置，再计算可支付前缀；所有费用汇总后扣除一次，实例等级只更新一次，但会为每个实际完成等级写一条带 `batch_request_id` 的逐级流水。

1. 资源足够：到达请求目标，`targetReached=true`、`outcome=TARGET_REACHED`。
2. 中途不足：提交此前已经可支付的连续等级，`targetReached=false`，`reachedLevel=blockedLevel-1`，已完成等级消耗不退回。
3. 连第一级都无法支付：返回 `6104 / 6204`，不扣资源、不改等级、不写批次，也不占用幂等键。
4. 目标区间的必需属性/金币缺失或非法、启用费用引用的物品停用或损坏：整单失败并回滚；已停用的可选突破点按运营删除处理，不参与当前计划。
5. 预览后等级变化返回 `6106 / 6207`，计划变化返回 `6108 / 6209`，HTTP 均为 409；Unity 必须丢弃旧确认页并重新预览。

执行响应的 `upgradeSteps` 只包含实际完成的步骤。当前装备 V1 到启用突破点前停止，不会出现实际消耗突破材料的步骤，`consumedPlayerEquipmentIds` 为空；历史消耗实例字段保留用于读取旧响应。V2 不允许部分成功，包含突破点的请求整体返回 `6210`，不能用 V2 代替单次手动突破。

### 16.2D 装备一键升级 V2（非突破路径全路径原子，保留历史分组合同）

当前 V2 只执行不含启用突破点的路径，目标可处于 `WAREHOUSE` 或 `EQUIPPED`；成功保留穿戴关系。它只接受“全路径成功或全部回滚”，不会像 V1 那样提交可支付前缀。路径包含任何突破点时整体拒绝 `6210`，不截断、不扣费；先手动突破后重新请求计划。可执行的非突破路径提交 `materialGroups=[]`，仍需按预览返回值提交指纹及既有确认字段。下方分级材料字段和含突破示例记录 V139 引入的历史序列化合同，保留用于旧批次审计/幂等重放；不得作为当前可成功跨突破的请求示例。

以下四个接口都使用统一响应包裹：`code` 为业务码，成功固定为 `200`；`message` 为结果说明；`data` 为下文列出的接口数据对象。plan、materials、preview 成功时 `message="操作成功"`，execute 成功或幂等重放时 `message="装备一键升级完成"`。示例中的实例 ID、余额和指纹仅用于说明字段关系，客户端必须原样使用服务端本次返回值，不能自行生成指纹。

#### 16.2D.1 查询完整升级计划

```http
GET /api/equipment/{playerEquipmentId}/auto-upgrade-v2-plan?targetLevel=20
Authorization: Bearer <token>
```

`playerEquipmentId` 必须为正整数；`targetLevel` 必填，范围为 `2～100`，并且必须大于装备当前等级且不超过该装备模板的 `maxLevel`。计划必须至少经过一个启用的突破点，否则返回 `6210`。资源不足只作为预览提示，本接口不锁定、不预留也不扣除资源。

`data` 字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `contractVersion` | integer | 合同版本，固定为 `2`。 |
| `executionMode` | string | 执行模式，固定为 `ALL_OR_NOTHING`。 |
| `playerEquipmentId` | integer(int64) | 目标玩家装备实例 ID。 |
| `equipmentId` | integer(int64) | 装备配置 ID。 |
| `equipmentKey` | string | 装备稳定编码。 |
| `name` | string | 装备名称快照。 |
| `quality` | string | 品质编码：`COMMON / FINE / EXCELLENT / EPIC / LEGENDARY`。 |
| `status` | string | 目标装备当前状态，正常目标为 `WAREHOUSE` 或 `EQUIPPED`。 |
| `currentLevel` | integer | 计划生成时的目标装备等级。 |
| `requestedTargetLevel` | integer | 本次请求目标等级。 |
| `maxLevel` | integer | 装备模板允许的最高等级。 |
| `resourceSufficient` | boolean | 当前金币和额外背包物品是否足以支付整条路径；不代表手选材料已经校验。 |
| `blockedRequirements` | array | 全路径资源缺口；充足时为空数组，元素结构见下表。V2 当前只返回 `GOLD` 或 `ITEM` 缺口。 |
| `totalGoldCost` | integer(int64) | 从 `currentLevel` 到 `requestedTargetLevel` 的累计金币费用。 |
| `goldBalance` | integer(int64) | 计划查询时的金币余额快照。 |
| `totalRequiredMaterialCount` | integer | 所有突破点合计需要的同款装备材料件数。 |
| `materialRequirements` | array | 需要手选材料的突破等级及材料框数量，按 `targetLevel` 升序；普通等级不出现。 |
| `totalResourceCosts` | array | 除金币和同款装备外，额外背包物品的全路径汇总，按 `itemId` 升序。 |
| `materialPolicy` | string | 固定为 `SAME_EQUIPMENT_ANY_LEVEL_ONE_SLOT_EACH`，即同款任意等级、每件占一个材料框。 |
| `refundPolicy` | string | 固定为 `NONE`，材料及其历史培养投入不返还。 |
| `planFingerprint` | string | 服务端按正式配置生成的 64 位小写 SHA-256；preview 和 execute 必须原样回传。 |
| `upgradeSteps` | array | 请求区间内逐级费用和属性变化，包含全部等级步骤，结构见下表。 |

`blockedRequirements[]`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `resourceType` | string | 缺口类型；V2 计划当前为 `GOLD` 或 `ITEM`。 |
| `itemId` | integer(int64) | 金币或额外物品配置 ID。 |
| `resourceKey` | string | 资源稳定编码。 |
| `resourceName` | string | 资源名称快照。 |
| `requiredAmount` | integer(int64) | 完成整条路径累计需要的数量。 |
| `availableAmount` | integer(int64) | 计划查询时可用数量。 |
| `missingAmount` | integer(int64) | 缺少数量，即 `requiredAmount - availableAmount`。 |

`materialRequirements[]`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `targetLevel` | integer | 使用该组材料后实际到达的突破等级。 |
| `requiredMaterialCount` | integer | 该突破等级必须提交的材料件数。 |

`totalResourceCosts[]`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `itemId` | integer(int64) | 额外物品配置 ID。 |
| `itemKey` | string | 额外物品稳定编码。 |
| `itemName` | string | 额外物品名称快照。 |
| `amount` | integer(int64) | 全路径累计需要数量。 |
| `availableAmount` | integer(int64) | 计划开始时的余额快照。 |
| `remainingAmount` | integer(int64) | 支付后的预计余额；资源不足时最低返回 `0`。 |

`upgradeSteps[]`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `fromLevel` | integer | 本步骤开始前等级。 |
| `targetLevel` | integer | 本步骤完成后等级。 |
| `beforeHealthBonus` / `afterHealthBonus` / `healthBonusDelta` | integer(int64) | 本步骤前、后生命加成及变化量。 |
| `beforeAttackBonus` / `afterAttackBonus` / `attackBonusDelta` | integer(int64) | 本步骤前、后攻击加成及变化量；差值可为负数。 |
| `beforeDefenseBonus` / `afterDefenseBonus` / `defenseBonusDelta` | integer(int64) | 本步骤前、后防御加成及变化量。 |
| `beforeSpeedBonus` / `afterSpeedBonus` / `speedBonusDelta` | integer(int64) | 本步骤前、后速度加成及变化量。 |
| `goldItemId` | integer(int64) | 金币物品配置 ID。 |
| `goldItemKey` | string | 金币稳定编码。 |
| `goldCost` | integer(int64) | 本步骤金币费用。 |
| `sameEquipmentCount` | integer | 本步骤需要的同款装备材料件数；普通等级为 `0`。 |
| `resourceCosts` | array | 本步骤额外背包物品费用，按物品 ID 升序。 |
| `payable` | boolean | V2 为全路径语义：plan 中整条路径资源充足时所有步骤均为 `true`，否则均为 `false`；成功执行响应中固定为 `true`。 |
| `consumedPlayerEquipmentIds` | array<integer(int64)> | plan 中固定为空数组；成功执行时只在对应突破步骤返回该组实际消耗的实例 ID。 |

`upgradeSteps[].resourceCosts[]`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `itemId` | integer(int64) \| null | 额外物品配置 ID；当前 V2 逐级响应未填充时为 `null`。 |
| `itemKey` | string | 额外物品稳定编码。 |
| `itemName` | string | 额外物品名称快照。 |
| `amount` | integer(int64) | 本步骤需要数量。 |

成功响应示例（示例路径只有 `19 → 20` 一个步骤，且未配置额外物品费用）：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "contractVersion": 2,
    "executionMode": "ALL_OR_NOTHING",
    "playerEquipmentId": 501,
    "equipmentId": 4001,
    "equipmentKey": "starter_sword",
    "name": "新手剑",
    "quality": "FINE",
    "status": "WAREHOUSE",
    "currentLevel": 19,
    "requestedTargetLevel": 20,
    "maxLevel": 100,
    "resourceSufficient": true,
    "blockedRequirements": [],
    "totalGoldCost": 1000,
    "goldBalance": 5000,
    "totalRequiredMaterialCount": 2,
    "materialRequirements": [
      {
        "targetLevel": 20,
        "requiredMaterialCount": 2
      }
    ],
    "totalResourceCosts": [],
    "materialPolicy": "SAME_EQUIPMENT_ANY_LEVEL_ONE_SLOT_EACH",
    "refundPolicy": "NONE",
    "planFingerprint": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    "upgradeSteps": [
      {
        "fromLevel": 19,
        "targetLevel": 20,
        "beforeHealthBonus": 1900,
        "afterHealthBonus": 2000,
        "healthBonusDelta": 100,
        "beforeAttackBonus": 760,
        "afterAttackBonus": 800,
        "attackBonusDelta": 40,
        "beforeDefenseBonus": 380,
        "afterDefenseBonus": 400,
        "defenseBonusDelta": 20,
        "beforeSpeedBonus": 95,
        "afterSpeedBonus": 100,
        "speedBonusDelta": 5,
        "goldItemId": 1001,
        "goldItemKey": "gold",
        "goldCost": 1000,
        "sameEquipmentCount": 2,
        "resourceCosts": [],
        "payable": true,
        "consumedPlayerEquipmentIds": []
      }
    ]
  }
}
```

#### 16.2D.2 查询任意等级材料候选

```http
GET /api/equipment/{playerEquipmentId}/auto-upgrade-v2-materials?page=1&pageSize=50
Authorization: Bearer <token>
```

`playerEquipmentId` 必须为正整数；`page` 默认 `1` 且不得小于 `1`；`pageSize` 默认 `50`，范围为 `1～100`。本接口返回目标装备从当前等级至该模板 `maxLevel` 的突破路径上可复用的同款任意等级候选，不绑定某一个 `targetLevel`，也不表示候选已经分配给某个材料组。实际目标路径以随后请求的 V2 plan 为准，材料归组和最终有效性以后续 preview 校验为准。

候选按“材料等级升序 → 获得时间升序 → 实例 ID 升序”分页。客户端可以按同一顺序执行“一键添加”；若自动填充包含高等级材料，必须明确提示玩家并要求确认，不能静默吞掉已培养装备。

`data` 字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `contractVersion` | integer | 合同版本，固定为 `2`。 |
| `total` | integer(int64) | 当前查询条件下的候选总数。 |
| `page` | integer | 当前页码。 |
| `pageSize` | integer | 当前每页数量。 |
| `items` | array | 当前页候选装备快照。 |

`items[]`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `playerEquipmentId` | integer(int64) | 候选玩家装备实例 ID。 |
| `equipmentId` | integer(int64) | 装备配置 ID，与目标装备相同。 |
| `equipmentKey` | string | 装备稳定编码。 |
| `name` | string | 装备名称快照。 |
| `icon` | string \| null | 装备图标资源键；正式配置未填写时可为 `null`。 |
| `quality` | string | 品质编码。 |
| `level` | integer | 候选装备当前等级，允许 `1～100`。 |
| `acquireTime` | string(datetime) \| null | 获得时间，按服务端 `yyyy-MM-dd HH:mm:ss` 格式输出；历史数据缺失时可为 `null`。 |
| `selectable` | boolean | 当前候选接口返回的合格条目固定为 `true`。 |
| `hasCultivationInvestment` | boolean | `level > 1` 时为 `true`。 |
| `unavailableReason` | string \| null | 当前候选接口合格条目固定为 `null`；执行时仍会重新校验状态。 |

成功响应示例：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "contractVersion": 2,
    "total": 2,
    "page": 1,
    "pageSize": 50,
    "items": [
      {
        "playerEquipmentId": 508,
        "equipmentId": 4001,
        "equipmentKey": "starter_sword",
        "name": "新手剑",
        "icon": "equipment/starter_sword",
        "quality": "FINE",
        "level": 1,
        "acquireTime": "2026-09-20 10:15:30",
        "selectable": true,
        "hasCultivationInvestment": false,
        "unavailableReason": null
      },
      {
        "playerEquipmentId": 509,
        "equipmentId": 4001,
        "equipmentKey": "starter_sword",
        "name": "新手剑",
        "icon": "equipment/starter_sword",
        "quality": "FINE",
        "level": 5,
        "acquireTime": "2026-09-21 08:00:00",
        "selectable": true,
        "hasCultivationInvestment": true,
        "unavailableReason": null
      }
    ]
  }
}
```

#### 16.2D.3 预览分级材料选择

```http
POST /api/equipment/auto-upgrade-v2-preview
Content-Type: application/json
Authorization: Bearer <token>
```

```json
{
  "playerEquipmentId": 501,
  "expectedCurrentLevel": 19,
  "targetLevel": 20,
  "planFingerprint": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "materialGroups": [
    {
      "targetLevel": 20,
      "materialPlayerEquipmentIds": [508, 509]
    }
  ]
}
```

请求字段：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `playerEquipmentId` | integer(int64) | 是 | 目标装备实例 ID，必须为正整数。 |
| `expectedCurrentLevel` | integer | 是 | plan 返回的当前等级，范围 `1～99`，并且必须小于 `targetLevel`。 |
| `targetLevel` | integer | 是 | 目标等级，范围 `2～100`，必须等于本次 plan 的目标等级。 |
| `planFingerprint` | string | 是 | plan 返回的 64 位小写 SHA-256。 |
| `materialGroups` | array | 是 | 非空，最多 `99` 组；必须与 plan 的 `materialRequirements` 完全一致。 |
| `materialGroups[].targetLevel` | integer | 是 | 突破目标等级，范围 `2～100`；组间不得重复，普通等级不得建组。 |
| `materialGroups[].materialPlayerEquipmentIds` | array<integer(int64)> | 是 | 非空且每组最多 `1000` 个正整数 ID；组内和跨组都不得重复，也不得包含目标装备。每组数量必须精确等于对应 `requiredMaterialCount`，整批最多 `5000` 件。 |

服务端会按 `targetLevel` 升序规范化分组，并在每组内按实例 ID 升序生成快照和指纹。因此 preview 成功后，execute 必须提交同一批实例；客户端不得根据提交顺序推断 `selectionOrder`。预览响应中的 `requiresIrreversibleConfirmation` 始终为 `true`；存在任意高于 1 级材料时 `requiresHighLevelConfirmation=true`。

`data` 字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `contractVersion` | integer | 合同版本，固定为 `2`。 |
| `executionMode` | string | 固定为 `ALL_OR_NOTHING`。 |
| `playerEquipmentId` | integer(int64) | 目标装备实例 ID。 |
| `currentLevel` | integer | 已校验的当前等级。 |
| `targetLevel` | integer | 已校验的目标等级。 |
| `totalMaterialCount` | integer | 所有材料组的材料总件数。 |
| `highLevelMaterialCount` | integer | 所有组中 `level > 1` 的材料总件数。 |
| `requiresIrreversibleConfirmation` | boolean | 固定为 `true`，执行前必须确认永久消耗。 |
| `requiresHighLevelConfirmation` | boolean | 只要存在 `level > 1` 的材料即为 `true`。 |
| `materialPolicy` | string | 固定为 `SAME_EQUIPMENT_ANY_LEVEL_ONE_SLOT_EACH`。 |
| `refundPolicy` | string | 固定为 `NONE`。 |
| `warning` | string | 服务端返回的永久消耗警告文案。 |
| `planFingerprint` | string | 经服务端重新计算并确认一致的计划指纹。 |
| `materialSelectionFingerprint` | string | 包含目标状态、计划和每件材料等级/状态快照的 64 位小写 SHA-256；execute 必须原样回传。 |
| `materialGroups` | array | 规范化后的分级材料快照，按 `targetLevel` 升序。 |

`materialGroups[]`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `targetLevel` | integer | 本组材料实际用于到达的突破等级。 |
| `requiredMaterialCount` | integer | 本组必须消耗的材料件数。 |
| `highLevelMaterialCount` | integer | 本组 `level > 1` 的材料件数。 |
| `selectedMaterials` | array | 本组已校验的不可变材料快照，按实例 ID 升序。 |

`selectedMaterials[]`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `targetLevel` | integer | 该材料所属突破目标等级。 |
| `selectionOrder` | integer | 该组内从 `1` 开始的稳定顺序。 |
| `playerEquipmentId` | integer(int64) | 材料玩家装备实例 ID。 |
| `equipmentId` | integer(int64) | 材料装备配置 ID，与目标装备相同。 |
| `equipmentKey` | string | 装备稳定编码。 |
| `name` | string | 装备名称快照。 |
| `icon` | string \| null | 装备图标资源键。 |
| `quality` | string | 品质编码。 |
| `level` | integer | 预览时读取的材料等级快照。 |
| `hasCultivationInvestment` | boolean | `level > 1` 时为 `true`。 |

成功响应示例：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "contractVersion": 2,
    "executionMode": "ALL_OR_NOTHING",
    "playerEquipmentId": 501,
    "currentLevel": 19,
    "targetLevel": 20,
    "totalMaterialCount": 2,
    "highLevelMaterialCount": 1,
    "requiresIrreversibleConfirmation": true,
    "requiresHighLevelConfirmation": true,
    "materialPolicy": "SAME_EQUIPMENT_ANY_LEVEL_ONE_SLOT_EACH",
    "refundPolicy": "NONE",
    "warning": "选中的装备及其已投入金币、资源和突破材料将在成功后永久消耗，且不返还",
    "planFingerprint": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    "materialSelectionFingerprint": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
    "materialGroups": [
      {
        "targetLevel": 20,
        "requiredMaterialCount": 2,
        "highLevelMaterialCount": 1,
        "selectedMaterials": [
          {
            "targetLevel": 20,
            "selectionOrder": 1,
            "playerEquipmentId": 508,
            "equipmentId": 4001,
            "equipmentKey": "starter_sword",
            "name": "新手剑",
            "icon": "equipment/starter_sword",
            "quality": "FINE",
            "level": 1,
            "hasCultivationInvestment": false
          },
          {
            "targetLevel": 20,
            "selectionOrder": 2,
            "playerEquipmentId": 509,
            "equipmentId": 4001,
            "equipmentKey": "starter_sword",
            "name": "新手剑",
            "icon": "equipment/starter_sword",
            "quality": "FINE",
            "level": 5,
            "hasCultivationInvestment": true
          }
        ]
      }
    ]
  }
}
```

#### 16.2D.4 原子执行 V2

```http
POST /api/equipment/auto-upgrade-v2
Authorization: Bearer <token>
Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
Content-Type: application/json
```

请求体在 16.2D.3 基础上增加两个确认字段和预览指纹：

```json
{
  "playerEquipmentId": 501,
  "expectedCurrentLevel": 19,
  "targetLevel": 20,
  "planFingerprint": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "materialSelectionFingerprint": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
  "materialGroups": [
    {
      "targetLevel": 20,
      "materialPlayerEquipmentIds": [508, 509]
    }
  ],
  "confirmIrreversibleMaterialConsumption": true,
  "confirmHighLevelMaterialLoss": true
}
```

请求体字段约束与 16.2D.3 相同，并新增：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `materialSelectionFingerprint` | string | 是 | preview 返回的 64 位小写 SHA-256，必须原样回传。 |
| `confirmIrreversibleMaterialConsumption` | boolean | 是 | 必须显式为 `true`；确认所有材料及历史投入永久消耗。 |
| `confirmHighLevelMaterialLoss` | boolean | 是 | 选择中存在任意 `level > 1` 材料时必须为 `true`；全部材料均为 1 级时可为 `false`。 |

`Idempotency-Key` 必须是标准 UUID。同一玩家、同一个 key 只能绑定同一份完整请求事实，请求事实包括目标、两个指纹、规范化后的全部分组与材料 ID，以及两个确认值。

执行时固定按“玩家 → 幂等记录 → 目标装备 → 正式配置 → 全部材料 ID 升序 → 背包物品 ID 升序”加锁，并重新校验玩家归属、同款、仓库、未锁定、未穿戴、未消耗、材料数量和材料等级指纹。任一金币、额外资源或材料不足都整体回滚：不扣资源、不消耗材料、不提升等级、不写批次和审计。成功后目标装备一次性到达 `targetLevel`，材料全部变为 `CONSUMED`，历史培养投入不返还；响应中的 `materialGroups`、`consumedPlayerEquipmentIds` 和 `upgradeSteps` 是不可变快照。

V2 批次 `contractVersion=2`，唯一幂等范围为“玩家 + Idempotency-Key”。相同 key 和完全相同请求永久重放首次成功响应；修改目标等级、任意材料 ID、材料等级快照、计划指纹或确认字段返回 `6208`。计划配置变化返回 `6209`，材料状态/等级变化返回 `6214`，材料数量或分组错误返回 `6211`，材料不可用返回 `6212`，确认缺失返回 `6213`。

`data` 字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `contractVersion` | integer | 合同版本，固定为 `2`。 |
| `executionMode` | string | 固定为 `ALL_OR_NOTHING`。 |
| `requestNo` | string(UUID) | 首次成功生成的服务端批次号；幂等重放保持不变，它不是请求头中的 `Idempotency-Key`。 |
| `playerEquipmentId` | integer(int64) | 已升级的目标装备实例 ID。 |
| `equipmentId` | integer(int64) | 装备配置 ID。 |
| `equipmentKey` | string | 装备稳定编码。 |
| `name` | string | 装备名称快照。 |
| `quality` | string | 品质编码。 |
| `status` | string | 目标装备升级后的生命周期状态，仍为原来的 `WAREHOUSE` 或 `EQUIPPED`。 |
| `fromLevel` | integer | 执行前等级。 |
| `requestedTargetLevel` | integer | 请求目标等级。 |
| `reachedLevel` | integer | 实际到达等级；V2 成功时等于 `requestedTargetLevel`。 |
| `upgradedLevelCount` | integer | 实际完成的逐级步骤数，即 `reachedLevel - fromLevel`。 |
| `outcome` | string | V2 只持久化完整成功批次，固定为 `TARGET_REACHED`。 |
| `totalGoldCost` | integer(int64) | 实际扣除的累计金币。 |
| `remainingGold` | integer(int64) | 扣除后的金币余额。 |
| `totalMaterialCount` | integer | 实际消耗的材料总件数。 |
| `highLevelMaterialCount` | integer | 实际消耗且 `level > 1` 的材料件数。 |
| `consumedPlayerEquipmentIds` | array<integer(int64)> | 全部已消耗材料实例 ID，按 ID 升序。 |
| `materialGroups` | array | 执行时冻结的分级材料快照；结构与 16.2D.3 相同。 |
| `totalResourceCosts` | array | 实际额外物品汇总及扣除后余额；结构与 16.2D.1 相同。 |
| `refundPolicy` | string | 固定为 `NONE`。 |
| `planFingerprint` | string | 本批次使用的计划指纹。 |
| `materialSelectionFingerprint` | string | 本批次使用的材料选择指纹。 |
| `healthBonus` | integer(int64) | 到达目标等级后的生命加成。 |
| `attackBonus` | integer(int64) | 到达目标等级后的攻击加成。 |
| `defenseBonus` | integer(int64) | 到达目标等级后的防御加成。 |
| `speedBonus` | integer(int64) | 到达目标等级后的速度加成。 |
| `businessTime` | string(datetime) | 数据库业务时间，按服务端 `yyyy-MM-dd HH:mm:ss` 格式输出；幂等重放保持首次成功值。 |
| `upgradeSteps` | array | 实际完成的全部逐级快照；结构与 16.2D.1 相同，所有 `payable=true`，突破步骤包含实际材料 ID。 |

成功响应示例：

```json
{
  "code": 200,
  "message": "装备一键升级完成",
  "data": {
    "contractVersion": 2,
    "executionMode": "ALL_OR_NOTHING",
    "requestNo": "6de9766e-3a55-4d7f-bb7b-7d92e3a6c608",
    "playerEquipmentId": 501,
    "equipmentId": 4001,
    "equipmentKey": "starter_sword",
    "name": "新手剑",
    "quality": "FINE",
    "status": "WAREHOUSE",
    "fromLevel": 19,
    "requestedTargetLevel": 20,
    "reachedLevel": 20,
    "upgradedLevelCount": 1,
    "outcome": "TARGET_REACHED",
    "totalGoldCost": 1000,
    "remainingGold": 4000,
    "totalMaterialCount": 2,
    "highLevelMaterialCount": 1,
    "consumedPlayerEquipmentIds": [508, 509],
    "materialGroups": [
      {
        "targetLevel": 20,
        "requiredMaterialCount": 2,
        "highLevelMaterialCount": 1,
        "selectedMaterials": [
          {
            "targetLevel": 20,
            "selectionOrder": 1,
            "playerEquipmentId": 508,
            "equipmentId": 4001,
            "equipmentKey": "starter_sword",
            "name": "新手剑",
            "icon": "equipment/starter_sword",
            "quality": "FINE",
            "level": 1,
            "hasCultivationInvestment": false
          },
          {
            "targetLevel": 20,
            "selectionOrder": 2,
            "playerEquipmentId": 509,
            "equipmentId": 4001,
            "equipmentKey": "starter_sword",
            "name": "新手剑",
            "icon": "equipment/starter_sword",
            "quality": "FINE",
            "level": 5,
            "hasCultivationInvestment": true
          }
        ]
      }
    ],
    "totalResourceCosts": [],
    "refundPolicy": "NONE",
    "planFingerprint": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    "materialSelectionFingerprint": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
    "healthBonus": 2000,
    "attackBonus": 800,
    "defenseBonus": 400,
    "speedBonus": 100,
    "businessTime": "2026-09-23 15:30:00",
    "upgradeSteps": [
      {
        "fromLevel": 19,
        "targetLevel": 20,
        "beforeHealthBonus": 1900,
        "afterHealthBonus": 2000,
        "healthBonusDelta": 100,
        "beforeAttackBonus": 760,
        "afterAttackBonus": 800,
        "attackBonusDelta": 40,
        "beforeDefenseBonus": 380,
        "afterDefenseBonus": 400,
        "defenseBonusDelta": 20,
        "beforeSpeedBonus": 95,
        "afterSpeedBonus": 100,
        "speedBonusDelta": 5,
        "goldItemId": 1001,
        "goldItemKey": "gold",
        "goldCost": 1000,
        "sameEquipmentCount": 2,
        "resourceCosts": [],
        "payable": true,
        "consumedPlayerEquipmentIds": [508, 509]
      }
    ]
  }
}
```

#### 16.2D.5 Unity 接入顺序

1. 打开装备突破页先请求 V2 plan，再请求 V2 materials；按服务端返回的 `materialRequirements` 动态创建材料组和材料槽，不写死突破等级或槽位数量。
2. 用户选择或一键添加后调用 V2 preview，保存 `planFingerprint`、`materialSelectionFingerprint` 以及每组实例 ID；预览成功后再展示不可逆损失确认框。
3. 用户确认后调用 V2 execute。每次新操作生成新的标准 UUID；网络重试必须复用原 UUID 和原请求体。
4. 成功后以响应的 `reachedLevel`、`materialGroups` 和 `upgradeSteps` 刷新目标装备与仓库；不要自行推算剩余资源或假设所有材料都是 1 级。
5. `6211`～`6214` 或 `6209` 出现时丢弃旧确认页并重新请求 plan/materials；不能自动重试旧指纹。

### 16.3 穿戴装备

```http
POST /api/equipment/equip
Content-Type: application/json
Authorization: Bearer <token>
```

```json
{
  "playerNpcId": 2,
  "playerEquipmentId": 501
}
```

两个实例都必须属于 token 玩家，装备必须处于 `WAREHOUSE`。同一 NPC 的同一 `equipmentType` 只能穿戴一件，例如已有 `WEAPON` 时不能再穿另一件武器。成功后装备状态改为 `EQUIPPED`，返回该 NPC 的完整装备和战斗属性。

### 16.4 卸下装备

```http
POST /api/equipment/unequip
Content-Type: application/json
Authorization: Bearer <token>
```

```json
{
  "playerNpcId": 2,
  "playerEquipmentId": 501
}
```

装备必须当前穿戴在指定 NPC 身上。成功后关闭活动穿戴关系，将装备状态恢复为 `WAREHOUSE`，并返回卸下后的完整装备和战斗属性。穿戴历史不会物理删除。

### 16.5 锁定或解锁装备

```http
POST /api/equipment/lock
Content-Type: application/json
Authorization: Bearer <token>
```

锁定：

```json
{
  "playerEquipmentId": 501,
  "locked": true
}
```

解锁时传 `false`。仓库或已穿戴装备都可以锁定；`CONSUMED` 装备不能修改。锁定只表示该实例不能被自动选为装备突破材料，不限制它作为升级目标，也不限制穿戴或卸下。成功返回 `code=200`，`data=null`。

### 16.6 查询 NPC 装备和战斗属性

```http
GET /api/equipment/loadout/{playerNpcId}
Authorization: Bearer <token>
```

请求体：无。`playerNpcId` 必须属于 token 玩家。

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "playerNpcId": 2,
    "npcId": 3001,
    "npcLevel": 20,
    "baseHealth": 290,
    "baseAttack": 58,
    "baseDefense": 48,
    "equipmentHealthBonus": 0,
    "equipmentAttackBonus": 67,
    "equipmentDefenseBonus": 0,
    "totalHealth": 290,
    "totalAttack": 125,
    "totalDefense": 48,
    "equipment": [
      {
        "relationId": 9001,
        "playerEquipmentId": 501,
        "equipmentId": 4001,
        "equipmentKey": "starter_sword",
        "name": "新手短剑",
        "equipmentType": "WEAPON",
        "quality": "COMMON",
        "icon": "starter_sword",
        "modelKey": "starter_sword",
        "level": 20,
        "locked": true,
        "healthBonus": 0,
        "attackBonus": 67,
        "defenseBonus": 0
      }
    ]
  }
}
```

不同装备实例可以有不同等级。服务端分别读取每件装备当前等级对应的 `equipment_level_attribute_config`，再与 NPC 当前等级的 `npc_level_attribute_config` 相加，使用返回的 `totalHealth/totalAttack/totalDefense` 作为最终战斗属性。

### 16.7 查询当前可配置卡池

```http
GET /api/card-draw/current
Authorization: Bearer <token>
```

请求体和查询参数：无。返回当前生效的普通卡池、完整展示奖项和服务端可用抽奖档位，不返回限定活动卡池。限定池通过 `/api/card-draw/pools/{poolCode}` 查询。奖项数量由 `card_draw_pool.card_count` 配置，范围为 1 至 100，绝不是固定九宫格。下面使用 3 项卡池演示动态响应；真实接口的 `cards.length` 必须等于当前 `cardCount`：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "poolCode": "SUMMER_2026",
    "version": 2,
    "name": "夏日活动卡池",
    "cardCount": 3,
    "businessTime": "2026-07-23 18:30:00",
    "cards": [
      {"slotNo": 1, "rewardType": "ITEM", "rewardKey": "gold", "rewardName": "金币", "icon": "gold", "rewardAmount": 500, "weight": 5000, "probability": 0.5},
      {"slotNo": 2, "rewardType": "EQUIPMENT", "rewardKey": "starter_sword", "rewardName": "新手短剑", "icon": "starter_sword", "rewardAmount": 1, "weight": 3000, "probability": 0.3},
      {"slotNo": 3, "rewardType": "NPC", "rewardKey": "field_steward", "rewardName": "田园管家", "icon": "Npc_1", "rewardAmount": 1, "weight": 2000, "probability": 0.2}
    ],
    "options": [
      {"optionCode": "SINGLE", "drawCount": 1, "costItemKey": "diamond", "costItemName": "钻石", "costAmount": 100, "savingAmount": 0},
      {"optionCode": "FIVE", "drawCount": 5, "costItemKey": "diamond", "costItemName": "钻石", "costAmount": 450, "savingAmount": 50}
    ]
  }
}
```

`probability = weight / 当前卡池全部启用奖项的总权重`。前端只按 `cardCount/cards` 动态展示数量、图标和概率，不能假定 9 项，不能选择中奖位置、不能自行随机，也不能把奖励或中奖位置提交给后端。V9 的 `DEFAULT_CARD_9` 仅是历史稳定编码；V24 当前默认显示名为“默认可配置卡池”，编码不会限制新版本数量。

### 16.8 执行单抽或五连抽

```http
POST /api/card-draw/draw
Content-Type: application/json
Authorization: Bearer <token>
Idempotency-Key: 8ec0abef-2863-4f36-a681-6ff06e3f38b2
```

单抽请求：

```json
{
  "optionCode": "SINGLE"
}
```

五连抽请求：

```json
{
  "optionCode": "FIVE"
}
```

请求只允许选择服务端返回的 `optionCode`。次数、价格、优惠、奖励数量、权重和结果都不能由客户端提交。

下面是五连抽关键字段示例。为控制篇幅，`cards` 仅展示 1 个元素；真实响应返回数量等于 `cardCount` 的本次完整卡池快照。前两次结果展示了同一批次首次获得 NPC 后，再次抽到该 NPC 转成 20 个专属碎片：

```json
{
  "code": 200,
  "message": "抽奖成功",
  "data": {
    "drawBatchId": 10001,
    "poolCode": "SUMMER_2026",
    "poolVersion": 2,
    "optionCode": "FIVE",
    "drawCount": 5,
    "cardCount": 3,
    "costItemKey": "diamond",
    "costAmount": 450,
    "remainingDiamond": 1550,
    "businessTime": "2026-07-20 15:35:00",
    "cards": [
      {"slotNo": 3, "rewardType": "NPC", "rewardKey": "field_steward", "rewardName": "田园管家", "icon": "Npc_1", "rewardAmount": 1, "weight": 2000, "probability": 0.2}
    ],
    "results": [
      {"drawResultId": 11001, "drawSequence": 1, "winningSlot": 3, "configuredRewardType": "NPC", "configuredRewardKey": "field_steward", "configuredRewardAmount": 1, "grantedRewardType": "NPC", "grantedRewardKey": "field_steward", "grantedRewardAmount": 1, "duplicateNpcConverted": false},
      {"drawResultId": 11002, "drawSequence": 2, "winningSlot": 3, "configuredRewardType": "NPC", "configuredRewardKey": "field_steward", "configuredRewardAmount": 1, "grantedRewardType": "ITEM", "grantedRewardKey": "npc_fragment_field_steward", "grantedRewardAmount": 20, "duplicateNpcConverted": true},
      {"drawResultId": 11003, "drawSequence": 3, "winningSlot": 1, "configuredRewardType": "ITEM", "configuredRewardKey": "gold", "configuredRewardAmount": 500, "grantedRewardType": "ITEM", "grantedRewardKey": "gold", "grantedRewardAmount": 500, "duplicateNpcConverted": false},
      {"drawResultId": 11004, "drawSequence": 4, "winningSlot": 2, "configuredRewardType": "EQUIPMENT", "configuredRewardKey": "starter_sword", "configuredRewardAmount": 1, "grantedRewardType": "EQUIPMENT", "grantedRewardKey": "starter_sword", "grantedRewardAmount": 1, "duplicateNpcConverted": false},
      {"drawResultId": 11005, "drawSequence": 5, "winningSlot": 1, "configuredRewardType": "ITEM", "configuredRewardKey": "gold", "configuredRewardAmount": 500, "grantedRewardType": "ITEM", "grantedRewardKey": "gold", "grantedRewardAmount": 500, "duplicateNpcConverted": false}
    ]
  }
}
```

`Idempotency-Key` 规则：

1. Unity 每次真实点击生成一个随机 UUID，去掉首尾空格后最长 64 字符；不要使用固定值、玩家 ID 或当前秒数。
2. 同一次点击因超时、断网或未收到响应而重试时，必须复用原 `Idempotency-Key` 和原 `optionCode`。
3. 同一玩家以相同 key 和相同档位重试，服务端返回原批次、原 `cardCount` 和完整卡池快照、原结果及原抽奖后钻石余额，不重复扣费或发奖。
4. 相同 key 改传其他档位会返回参数错误。玩家主动开始下一次抽奖时必须生成新 key。
5. 后端先锁玩家行，再扣钻石、写批次与快照、逐次计算并发奖、写结果和成功状态；任一步失败时整个批次回滚。
6. 五连抽在事务内每获得一个新 NPC 都立即更新内存所有权集合，因此同批后续相同 NPC 会转换为该 NPC 自己的碎片。事务回滚时集合和数据库写入一起失效。

### 16.9 成长和抽奖失败业务码

| code | message | 典型场景 |
| --- | --- | --- |
| 6101 | 玩家NPC不存在 | NPC 不属于当前玩家、实例不存在，或装备面板查询了他人 NPC |
| 6102 | NPC已达到最高等级 | NPC 当前已经是 100 级或配置上限 |
| 6103 | NPC升级配置错误 | 金币、等级属性或命中的突破配置缺失/无效 |
| 6104 | NPC升级资源不足 | 金币不足，或突破时对应专属碎片不足 |
| 6105 | NPC目标等级不合法 | 一键升级目标不大于当前等级、超过模板上限或超过 100 |
| 6106 | NPC等级状态已变化，请刷新后重试 | 一键升级执行时 `expectedCurrentLevel` 已过期，HTTP 409 |
| 6107 | NPC升级幂等请求与首次请求不一致 | 同一玩家复用幂等键但修改了请求字段，HTTP 409 |
| 6108 | NPC升级配置已变化，请重新预览 | 执行时重新计算的 `planFingerprint` 与预览不同，HTTP 409 |
| 6201 | 玩家装备不存在 | 装备不属于当前玩家、实例不存在或已经被消耗 |
| 6202 | 装备已达到最高等级 | 装备当前已经是 100 级或配置上限 |
| 6203 | 装备升级配置错误 | 金币、等级属性、上限、额外资源或命中的突破配置缺失/无效 |
| 6204 | 装备升级资源不足 | V1 单级/一键升级的金币、额外仓库资源或同款 1 级材料不足 |
| 6205 | NPC对应装备部位已被占用 | 指定 NPC 的同一装备部位已有活动装备 |
| 6206 | 装备目标等级不合法 | 一键升级目标不大于当前等级、超过模板上限或超过 100 |
| 6207 | 装备等级状态已变化，请刷新后重试 | 一键升级执行时实例等级或状态已变化，HTTP 409 |
| 6208 | 装备升级幂等请求与首次请求不一致 | 同一玩家复用幂等键但修改了请求字段，HTTP 409 |
| 6209 | 装备升级配置已变化，请重新预览 | 执行时重新计算的 `planFingerprint` 与预览不同，HTTP 409 |
| 6210 | 当前装备不满足手选突破条件 | 目标等级不是当前等级的下一启用突破等级，或当前没有突破材料配置 |
| 6211 | 突破材料数量或分组不一致 | V2 缺少/多提交突破等级分组，或某组材料数量不等于正式配置 |
| 6212 | 突破材料不可用 | 材料不属于玩家、不同款、已穿戴、已锁定、已消耗、重复或是目标装备 |
| 6213 | 需要确认不可逆材料损失 | 未确认材料及其历史培养投入永久消耗，或未确认高等级材料损失 |
| 6214 | 突破材料状态已变化，请重新确认 | 材料等级、状态或选择指纹在预览后发生变化，HTTP 409 |
| 6301 | 当前没有可用卡池 | 没有处于有效时间范围内的唯一活动卡池 |
| 6302 | 抽奖档位不存在或未启用 | `optionCode` 错误或对应档位已关闭 |
| 6303 | 抽奖配置错误 | 奖项数量/连续性、权重、引用、价格、NPC 碎片或卡池版本配置无效 |
| 6304 | 钻石不足 | 当前钻石少于服务端配置的最终价格 |

### 16.10 数据配置、版本和并发规则

1. NPC 每级金币配置在 `upgrade_gold_curve_level`，每级最终属性在 `npc_level_attribute_config`，突破碎片在 `npc_breakthrough_config`。突破目标等级由管理端配置，范围为 2 到最高等级；`npc_config.fragment_item_id` 绑定专属碎片，`duplicate_fragment_amount` 控制重复 NPC 转换数量。
2. 装备每级金币配置也在 `upgrade_gold_curve_level`，每级属性在 `equipment_level_attribute_config`，突破同款数量在 `equipment_breakthrough_config`。额外升级消耗只允许配置 RESOURCE/PRODUCT 类型物品，并记录实际扣除快照。每一级都能单独修改，不使用 Java 金币倍率。V1 单级到突破点拒绝，V1 一键升级到突破点前停止，V2 含突破路径整体拒绝；单次手选突破允许同款任意等级材料，并把材料等级写入不可变审计快照。目标可保持穿戴，被消耗材料必须先卸下。
3. 卡池使用 `card_draw_pool` 版本化；`card_count` 配置 1 至 100 个展示奖项，档位价格在 `card_draw_option_config`，奖励、数量和权重在 `card_draw_reward_config`。运行时要求唯一活动卡池、启用奖励数量等于 `card_count`、内部序号连续为 `1..card_count`、有效 `SINGLE` 和优惠后的 `FIVE`。
4. `ITEM` 奖项可以配置任意启用物品，`EQUIPMENT` 奖项可以配置任意启用装备；`NPC` 奖项数量固定为 1，重复后按该 NPC 的专属配置转为碎片。
5. 每个抽奖批次把卡池版本、`card_count`、价格、完整卡池展示和实际结果保存为不可变快照。以后切换新卡池不会改变历史抽奖结果。
6. 玩家经济操作统一先锁 `users` 主记录。NPC/装备单级与一键升级、抽奖的真实 MySQL 并发测试覆盖材料只消耗一次、等级只更新一次，以及相同幂等 key 只扣费一次并重放相同结果。
7. V7 至 V30 已属于正式迁移，执行成功后禁止修改。后续调整金币、属性、材料、卡池或概率时新建 V31 或更高 Flyway 迁移，先创建和校验新卡池版本，再原子切换活动版本。

### 16.10.1 V75/V138/V139/V140/V141 突破配置化与 Unity 影响

V75 只改变服务端配置判定方式，旧单级接口和 V1 一键升级的请求体保持兼容；Unity 不需要为旧接口提交突破等级、碎片、同款装备或额外资源明细。V138 新增手选突破合同，V139 新增 V2 分级手选材料合同，Unity 若使用这两组新接口必须按 16.2C/16.2D 增加材料候选、分组、指纹和确认字段。

Unity 需要继续遵守：升级前用 `GET /api/equipment/{playerEquipmentId}/upgrade-preview` 展示旧接口费用，NPC 升级以 `POST /api/npc/upgrade` 成功响应为准，装备旧接口升级以 `POST /api/equipment/upgrade` 成功响应为准。使用 V138/V139 时改用 16.2C/16.2D 的材料候选、分组、计划指纹和确认字段。是否突破、需要多少碎片/同款装备/额外资源全部由后端正式配置决定，客户端不要写死 20/40/60/80/100，也不要写死材料等级或材料槽数量。

V138/V139/V140/V141 的数据库迁移必须由 Flyway 按顺序执行且不可修改已执行文件。V138 已冻结，V139/V140/V141 只以前向迁移扩展批次、请求生命周期并收紧审计字段非空约束。V1 历史批次继续使用 `contract_version=1` 和数据库默认字段；V2 批次使用 `contract_version=2`，保存逐突破等级材料组、材料等级快照、选择指纹和确认事实。
### 16.11 Unity 推荐流程

1. 保留单级按钮时继续只提交 `playerNpcId`；提供一键升级时先调用 NPC/装备 `auto-upgrade-preview`，展示逐级 `before/after/delta` 和可支付终点，再提交 `expectedCurrentLevel + targetLevel + planFingerprint`。
2. 打开仓库时调用 `GET /api/equipment/warehouse`；穿戴后使用接口返回的 loadout 刷新 NPC，卸下后同时刷新 loadout 和仓库。
3. 旧单级装备确认框继续调用 `GET /api/equipment/{playerEquipmentId}/upgrade-preview` 并提交 `/api/equipment/upgrade`；旧 V1 一键确认框使用 `auto-upgrade-preview/auto-upgrade`，仍自动选择 1 级材料。需要玩家指定任意等级材料或跨多个突破点全路径升级时，改用 16.2C/16.2D 接口。网络重试复用同一个 UUID 幂等键；成功后按响应中的 `reachedLevel / targetReached / blockedRequirements / upgradeSteps` 或 V2 的 `materialGroups` 刷新，不要假定每次都到达请求目标。
4. 打开抽奖界面先调用 `GET /api/card-draw/current`，按 `cardCount/cards` 动态渲染后端返回的全部奖项，按 `options` 显示单抽和五连的最终钻石价格与优惠；前端不写死网格数量，也不提供选卡操作。
5. 点击单抽传 `SINGLE`，点击五连传 `FIVE`，每次点击生成一个 `Idempotency-Key`。请求期间禁用按钮，网络重试复用相同 key。
6. 前端只在收到成功响应后按 `results[].drawSequence` 播放结果；`winningSlot` 是后端选中的内部奖项序号，`grantedRewardType/grantedRewardKey/grantedRewardAmount` 是实际入账奖励。
7. 结果含 `EQUIPMENT` 时刷新仓库，含新 `NPC` 时刷新 NPC 列表，含 `duplicateNpcConverted=true` 时按碎片表现展示并刷新背包。

## 17. 运营配置中心接口

运营配置中心使用独立运营账号和 `operator-token`，不能使用玩家 token。运行时游戏表始终是正式数据源；草稿、发布版本和审计分别保存在 `operator_config_release` 与 `operator_config_audit_log`。管理端基础页面位于 `admin-web`。

### 17.1 接口清单

本节列出 21 个通用认证/配置/引用统计/审计接口和 7 个玩家成长运营接口；其余运营工作台接口见第 52 节完整路由表：

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| `GET` | `/api/ops/auth/context` | 登录页系统、版本、环境和开关；无需 token |
| `POST` | `/api/ops/auth/login` | 运营账号登录 |
| `GET` | `/api/ops/auth/me` | 当前运营账号 |
| `POST` | `/api/ops/auth/logout` | 注销当前运营 token |
| `PUT` | `/api/ops/auth/password` | 验证旧密码并修改本人密码；成功后注销全部会话 |
| `GET` | `/api/ops/config/modules` | 可管理模块和配置键目录；每项返回后端真实的 `adminOnlyWrite` 与 `genericRollbackSupported` 权限/回滚能力 |
| `GET` | `/api/ops/config/dictionaries` | 已启用物品、装备和 NPC 选择字典 |
| `GET` | `/api/ops/config/asset-references/npcs` | 批量查询 NPC 本体和专属碎片引用统计 |
| `GET` | `/api/ops/config/{moduleCode}/{configKey}` | 正式配置、草稿、版本和实时指纹 |
| `POST` | `/api/ops/config/{moduleCode}/{configKey}/validate` | 校验完整配置，不写数据库 |
| `GET` | `/api/ops/config/{moduleCode}/{configKey}/preview` | 预览结构化差异、影响范围和高风险提示，不写数据库 |
| `PUT` | `/api/ops/config/{moduleCode}/{configKey}/draft` | 新建或更新唯一草稿 |
| `POST` | `/api/ops/config/{moduleCode}/{configKey}/publish` | 原子发布当前草稿 |
| `POST` | `/api/ops/config/{moduleCode}/{configKey}/schedule` | 按计划时间创建或更新定时发布任务 |
| `POST` | `/api/ops/config/{moduleCode}/{configKey}/schedule/cancel` | 取消尚未执行的定时发布任务 |
| `DELETE` | `/api/ops/config/{moduleCode}/{configKey}/draft` | 丢弃草稿，不改变正式配置 |
| `GET` | `/api/ops/config/{moduleCode}/{configKey}/history` | 查询历史版本摘要，`limit=1..100` |
| `GET` | `/api/ops/config/{moduleCode}/{configKey}/history/{versionNo}` | 按需读取一个完整历史快照 |
| `POST` | `/api/ops/config/{moduleCode}/{configKey}/rollback/{versionNo}` | 根据历史版本创建回滚草稿 |
| `GET` | `/api/ops/config-audits` | 使用 ID 游标分页查询配置审计 |
| `GET` | `/api/ops/config-audits/{auditId}` | 查询一条审计的前后快照 |
| `POST` | `/api/ops/player-experience/adjustments` | 使用运营请求号幂等增加玩家经验；仅 `CONFIG_ADMIN` |
| `GET` | `/api/ops/player-experience/adjustments` | 按玩家或记录 ID 游标查询经验调整历史；仅 `CONFIG_ADMIN` |
| `POST` | `/api/ops/player-experience/reconciliation` | 创建或继续一份持久化、只升不降的成长对账任务；仅 `CONFIG_ADMIN` |
| `GET` | `/api/ops/player-experience/reconciliation` | 按状态、来源和 ID 游标查询成长对账任务；仅 `CONFIG_ADMIN` |
| `GET` | `/api/ops/player-experience/reconciliation/{taskId}` | 查询一份成长对账任务的权威状态；仅 `CONFIG_ADMIN` |
| `POST` | `/api/ops/player-experience/reconciliation/{taskId}/retry` | 显式重试一批未解决失败玩家；仅 `CONFIG_ADMIN` |
| `GET` | `/api/ops/player-experience/reconciliation/{taskId}/failures` | 分页查询逐玩家失败明细；仅 `CONFIG_ADMIN` |

除 `/ops/auth/context` 和 `/ops/auth/login` 外，都必须携带：

```http
Authorization: Bearer <operator-token>
```

### 17.2 当前配置模块

| `moduleCode` | 可管理内容 | 典型 `configKey` |
| --- | --- | --- |
| `SIGN_IN` | 新手、活动和月度签到奖励、参与窗口、奖励领取模式、补签开关与费用、NPC 重复补偿 | `NEWBIE:DEFAULT`、系统派生的 `ACTIVITY_<hash>:DEFAULT`、`MONTHLY:yyyy-MM` |
| `CARD_DRAW` | 普通/限定卡池、1 至 100 个奖项、百分比概率、抽奖费用和自动编码 | `STANDARD_POOL`、`LIMITED_POOL`；普通池兼容 `ACTIVE_POOL` |
| `ITEM_CATALOG` | 物品基础资料、新增物品、启停和引用保护 | `ITEMS` |
| `EQUIPMENT_CATALOG` | 装备基础资料、新增装备、启停和引用保护 | `EQUIPMENTS` |
| `NPC_CATALOG` | NPC 基础资料、新增 NPC、专属碎片、运行/获取状态和引用保护 | `NPCS` |
| `NPC_GROWTH` | NPC 1 至 100 级战斗属性和可配置专属碎片突破消耗 | NPC ID |
| `EQUIPMENT_GROWTH` | 装备展示、1 至 100 级生命/攻击/防御/速度加成、突破和金币曲线 | 装备 ID |
| `UPGRADE_CURVE` | NPC/装备每个目标等级的明确金币成本 | 曲线编码 |
| `IDLE_ADVENTURE` | 完整玩法版本、状态、技能、敌人、关卡、奖励、克制和挂机规则 | `IDLE_ADVENTURE` |
| `LAND_BUILDING` | 地块准入、建筑类型/前置、逐级成本、升级经验、耗时、产物、周期和容量 | `WORLD` |
| `PLAYER_PROGRESSION` | 只追加的玩家等级和累计经验门槛 | `LEVEL_CURVE` |
| `PLAYER_TASK` | 日常/周常任务目标、经验奖励、排序和启用状态 | `TASKS` |
| `WECHAT_FRIEND_GIFT` | 微信 MP 礼包、微信道具到游戏资源的固化映射、领取上限和发送开关 | `GIFT_PACKAGES` |
| `SYSTEM_REWARD` | 系统奖励包名称、启用状态和多项附件资源 | 奖励包编码 |
| `PLAYER_FRIEND_GIFT` | 游戏好友每日赠礼资源、发送上限和接收上限 | `DAILY_GIFT` |
| `PLAYER_FRIEND_REQUEST` | 好友申请开关、好友容量、待处理容量、每日额度、冷却和有效期 | `POLICY` |
| `RESOURCE_SALE` | 资源到金币的整数兑换比例、单笔批次、北京时间每日额度和全局开关 | `EXCHANGE_OPTIONS` |

界面将地块/建筑类型、装备部位/品质、曲线适用类型和字典名称显示为中文，但请求和数据库仍使用稳定编码。V24 只转换仍等于系统英文默认值的名称，不覆盖已由运营自定义的名称。`LAND_BUILDING / WORLD` 支持新增地块、解锁等级、金币/木材/石材/铁矿放置费用、允许建造类型和 `enabled` 开关；新增地块编号由服务端在发布事务中自动生成，默认停用。停用只影响新的可放置列表和放置校验，不删除玩家已有地块。

模块目录响应中的 `adminOnlyWrite=true` 是写权限唯一真源：`CONFIG_VIEWER` 与 `CONFIG_EDITOR` 只能读取，只有 `CONFIG_ADMIN` 可以保存、发布或丢弃草稿；`adminOnlyWrite=false` 的模块允许 `CONFIG_EDITOR` 按通用流程编辑。`genericRollbackSupported=false` 表示历史版本仅供查看，不能创建整份回滚草稿（当前固定头像目录即如此）。页面只把这些字段作为界面提示，后端仍会在每个写接口再次校验权限。

`GET /api/ops/config/modules` 的单项响应示例：

```json
{
  "moduleCode": "AVATAR_CATALOG",
  "displayName": "固定头像目录",
  "description": "登记Unity固定头像合同；编号和资源路径由系统生成，验收后才允许启用",
  "adminOnlyWrite": true,
  "genericRollbackSupported": false,
  "keys": [{ "configKey": "PRESETS", "displayName": "固定头像目录" }]
}
```

### 17.3 草稿、发布和并发参数

校验或保存草稿使用完整配置快照：

```json
{
  "expectedRevision": 3,
  "baseFingerprint": "64位SHA-256正式数据指纹",
  "payload": {},
  "reason": "调整活动奖励"
}
```

首次创建草稿时 `expectedRevision` 可以为 `null`；更新现有草稿时必须等于服务端当前 `draftRevision`。发布或丢弃请求为：

```json
{
  "expectedRevision": 4,
  "reason": "活动配置复核通过"
}
```

发布事务依次锁定配置键、重新读取正式数据并核对 `baseFingerprint`、校验业务引用、写正式业务表、切换版本和追加审计。指纹或修订号不一致时返回 HTTP/业务 `409`，页面必须重新载入，不能覆盖他人发布或直接 SQL 修改。保存草稿不会影响线上；回滚只创建新草稿，仍需再次发布。

回滚请求使用页面读取到的当前正式版本号：

```json
{
  "expectedPublishedVersion": 7,
  "reason": "恢复上一版活动参数"
}
```

### 17.4 审计与安全边界

审计列表支持 `moduleCode/configKey/action/operatorId/beforeId/limit`，其中 `action` 为 `CREATE_DRAFT/SAVE_DRAFT/PUBLISH/DISCARD/ROLLBACK`。审计记录只读，不提供更新或删除接口。运营接口统一返回 `Cache-Control: no-store`、`X-Frame-Options: DENY` 和 `X-Content-Type-Options: nosniff` 等安全头。

当前实现是真实本地运营账号安全链路，不包含伪造的 SSO 或 WebAuthn 入口；TOTP 多因素认证已通过“账号安全与多因素认证”弹窗提供。运营人员先调用 `POST /api/ops/auth/mfa/setup` 生成一次性设置密钥，管理端在浏览器本地将返回的 `provisioningUri` 渲染为二维码，也保留手动密钥和导入地址；再使用认证器生成的 6 位验证码调用 `POST /api/ops/auth/mfa/confirm` 完成启用。二维码和明文密钥只保留在当前页面内存，弹窗关闭后清理；二维码生成失败时可使用手动方式。生产环境必须通过 HTTPS、可信反向代理和网络访问控制暴露管理端；`OPERATOR_TRUSTED_PROXY_ADDRESSES` 只填写真实直接代理地址。

## 18. Idle Adventure 接口文档

Idle Adventure 对 Unity 保留原 26 条 `snake_case` 接口，并新增体力查询和挂机加速接口，当前共 28 条路径，其中额外 Buff 路径暂不开放；原 10 条精简路径已下线并返回 HTTP 404。首次进入直接获得管理端配置的满体力，开始、继续下一关和失败重试扣除目标关卡体力，暂停、恢复、回合和结束不扣。挂机加速必须携带幂等键，先结算自然挂机收益，再扣除一次体力并发放一份完整挂机上限奖励。本文件末尾的“V134/V135 挂机冒险技能、范围目标与治疗规则”是技能、DTO、逐回合播放、重连和错误处理的主契约；同桌面 `IdleAdventure模式接口文档.md` 与项目内 `docs/idle-adventure-api.md` 是补充参考，若内容不一致，以本文件当前章节和后端 DTO 为准。

主文档同步保留完整路由索引，路径变量名称与当前 Controller 一致：

| 序号 | 方法 | 路径 | 用途 |
| ---: | --- | --- | --- |
| 1 | `GET` | `/api/idle-adventure/players/{playerId}/home` | 获取首页聚合数据 |
| 2 | `GET` | `/api/idle-adventure/config` | 获取玩法固定规则 |
| 3 | `GET` | `/api/idle-adventure/buff-presets` | 获取非场地 buff 配置 |
| 4 | `GET` | `/api/idle-adventure/skills` | 获取技能配置 |
| 5 | `GET` | `/api/idle-adventure/field-buffs` | 获取场地 buff 配置 |
| 6 | `GET` | `/api/idle-adventure/attribute-restraints` | 获取属性克制关系 |
| 7 | `GET` | `/api/idle-adventure/stage-rewards` | 获取全部关卡奖励配置 |
| 8 | `GET` | `/api/idle-adventure/players/{playerId}/npcs` | 获取玩家可参战 NPC |
| 9 | `GET` | `/api/idle-adventure/stages/{stage}/enemies` | 获取关卡敌人六槽位预览 |
| 10 | `GET` | `/api/idle-adventure/players/{playerId}/formation` | 获取保存编队 |
| 11 | `PUT` | `/api/idle-adventure/players/{playerId}/formation` | 保存完整或局部编队变更 |
| 12 | `POST` | `/api/idle-adventure/players/{playerId}/formation/auto-fill` | 自动填充默认编队 |
| 13 | `POST` | `/api/idle-adventure/players/{playerId}/formation/clear` | 主动清空编队 |
| 14 | `POST` | `/api/idle-adventure/players/{playerId}/sessions/start` | 创建战斗会话 |
| 15 | `GET` | `/api/idle-adventure/players/{playerId}/sessions/{sessionKey}` | 查询或恢复会话 |
| 16 | `POST` | `/api/idle-adventure/players/{playerId}/sessions/{sessionKey}/pause` | 暂停会话 |
| 17 | `POST` | `/api/idle-adventure/players/{playerId}/sessions/{sessionKey}/resume` | 恢复会话 |
| 18 | `POST` | `/api/idle-adventure/players/{playerId}/sessions/{sessionKey}/end` | 结束并结算奖励 |
| 19 | `POST` | `/api/idle-adventure/players/{playerId}/sessions/{sessionKey}/rounds/resolve` | 权威结算一回合 |
| 20 | `POST` | `/api/idle-adventure/players/{playerId}/sessions/{sessionKey}/buffs/apply` | 仅保留兼容路径，当前固定返回 HTTP 403/`6515`，Unity 不开放入口 |
| 21 | `POST` | `/api/idle-adventure/players/{playerId}/sessions/{sessionKey}/stages/continue` | 继续下一关 |
| 22 | `POST` | `/api/idle-adventure/players/{playerId}/sessions/{sessionKey}/stages/retry` | 重试当前关 |
| 23 | `GET` | `/api/idle-adventure/players/{playerId}/progress` | 获取权威进度 |
| 24 | `POST` | `/api/idle-adventure/players/{playerId}/progress/stage-clear` | 核实服务端通关结果 |
| 25 | `GET` | `/api/idle-adventure/players/{playerId}/afk-reward` | 查询挂机奖励 |
| 26 | `POST` | `/api/idle-adventure/players/{playerId}/afk-reward/collect` | 领取挂机奖励 |
| 27 | `GET` | `/api/idle-adventure/players/{playerId}/stamina` | 刷新并获取当前体力与恢复倒计时 |
| 28 | `POST` | `/api/idle-adventure/players/{playerId}/afk-reward/accelerate` | 消耗体力领取一份完整挂机上限奖励 |

### 18.1 战斗属性与克制方向（V156/V157）

新发布的战斗配置使用八种属性：`PHYSICAL`（物理）、`FIRE`（炎）、`ICE`（冰）、`ROCK`（岩）、`WIND`（风）、`POISON`（毒）、`STAR`（星）、`DARK`（暗）。`GET /api/idle-adventure/attribute-restraints` 始终返回当时激活的正式玩法版本；`attacker_attribute` 为攻击属性，`defender_attribute` 为被克制属性。图中箭头从攻击属性指向被克制属性，新规则完整关系如下：

| 攻击属性 | 克制的防守属性 |
| --- | --- |
| 物理 `PHYSICAL` | 岩 `ROCK` |
| 炎 `FIRE` | 冰 `ICE`、毒 `POISON` |
| 冰 `ICE` | 炎 `FIRE`、毒 `POISON` |
| 岩 `ROCK` | 炎 `FIRE`、风 `WIND` |
| 风 `WIND` | 冰 `ICE`、物理 `PHYSICAL` |
| 毒 `POISON` | 岩 `ROCK`、风 `WIND` |
| 星 `STAR` | 暗 `DARK` |
| 暗 `DARK` | 星 `STAR` |

新规则仅以上 13 个方向触发克制；炎与冰、星与暗分别双向克制，星、暗与其余六种属性没有克制关系。其他组合（含同属性）按正常伤害结算，不加克制倍率，也不因对方克制自己而自动减伤。命中时，服务端把玩法版本的 `attribute_restraint_multiplier` 加入伤害最终倍率的加法项；本机原配置值为 `0.20`。每个范围目标按自己的防守属性独立判断。战斗会话继续使用锁定版本及固化快照，Unity 只显示属性和服务端伤害结果，不自行计算克制。

V156 将存量编码 `WATER→ICE`、`WOOD→WIND`、`EARTH→ROCK`、`THUNDER→STAR` 迁移为新属性，并曾把历史玩法版本的克制行也替换为上表 13 个方向。V157 不修改已执行的 V156，而是恢复 v1 初始规则及 v2 发布快照中的敌人、技能、Buff 和各七条旧克制关系，历史玩法表同时允许旧、新属性编码；新配置仍只允许上述八种属性。本机 `game_db` 已执行 V157（checksum `349671882`），v1/v2 各七条；管理端随后发布活动玩法 v3（配置中心正式发布第 2 版），现有图中 13 条、倍率 `0.20`，整版使用 Unity 默认背景。旧会话仍沿用锁定的历史版本。其他环境按 `flyway_schema_history`、活动版本和接口响应逐项核对。Unity 的属性显示表应同时认识新八码和旧会话可能返回的 `WATER / WOOD / EARTH / THUNDER`，合计 12 种编码；旧码只用于历史版本显示，不能在客户端强行映射成新码推算克制。属性箭头和伤害以服务端当前配置或会话锁定结果为准，不把当前 v3 的 13 条规则套在旧 v1/v2 会话上。

本次战斗技能契约已在本文件末尾完整展开：技能由管理端配置 `effect_type`、`target_side`、`target_mode`、`target_groups`、`target_count`、`include_self`、初始冷却和治疗公式；服务端在会话创建时冻结规则。六个槽位使用列优先布局（前列 `0/1/2`、后列 `3/4/5`）。`ORDERED_GROUPS` 只选择第一个仍有存活目标的组，不跨组补足；示例 `[0,1] -> [2] -> [3,4] -> [5]` 严格按序切换。单体回血按当前生命绝对值（不按生命百分比）、施法者优先、后列优先、槽位顺序选择一个己方未满血存活目标；只有 `HEAL + ALLY` 强制把施法者纳入候选，普通 `ALLY` 是否包含施法者由 `ALLY + ALL + include_self` 决定，死亡角色不复活，治疗封顶。回合动作新增 `effect_type`、`selected_group_seq`、`selected_slots`、`total_damage`、`total_healed` 和 `targets[]`，旧单目标字段只代表第一目标。Unity 只播放服务端返回的最终目标与数值，不自行选目标或计算；先发布能解析新增字段的客户端，再在管理端启用范围/回血技能。技能接口现已返回 `skill_icon` 逻辑资源键，Unity 应通过资源绑定表加载，不得把它当作 `Assets/...` 物理路径。

### 18.2 Unity 接入顺序与状态处理

所有玩家请求使用 `Authorization: Bearer <player-token>`；路径中的 `playerId` 必须与 token 玩家一致。Idle Adventure 请求体和 `data` 使用 `snake_case`，外层响应为 `code/message/data`。先检查 HTTP 状态，再检查业务 `code`；业务成功后才使用 `data`。客户端保存的是服务端会话键和待确认操作的幂等键，不保存自行推算的战斗结果。

1. 登录后请求 `GET /api/idle-adventure/players/{playerId}/home`，读取 `stamina`、`formation.slots[]`、`available_npc_list[]`、`highest_stage`、`next_stage` 和 `running_session_key`。若 `has_running_session=true`，先用该键调用 `GET /sessions/{sessionKey}` 恢复现有会话；不要再次创建。配置、技能、属性克制和关卡敌人接口可供入口预览，但当前正式配置不得覆盖已有会话冻结的规则和背景。
2. 编队采用固定六槽位，按 `slot_index=0..5` 映射，空槽保留。需要修改保存编队时调用 `PUT /formation`，请求体如 `{"slots":[{"slot_index":0,"definition_id":3001},{"slot_index":1,"definition_id":null}]}`；未提交的槽位保持原值。`POST /formation/auto-fill` 和 `/formation/clear` 分别自动填充、主动清空。编队 WebSocket 事件仅提示刷新，收到事件、重连或返回前台时重新 GET 编队，避免旧事件覆盖较新的 HTTP 结果。
3. 可用 `GET /stages/{stage}/enemies` 预览关卡六个敌方槽位及可空 `stage_presentation`。无活动会话时调用 `POST /sessions/start`：保存编队传 `{"stage":1,"use_saved_formation":true}`；仅本次使用临时编队传 `{"stage":1,"use_saved_formation":false,"formation_slots":[{"slot_index":0,"definition_id":3001}]}`，临时编队不覆盖保存结果。`stage` 可省略，默认挑战 `highest_stage+1`，不能跳关。成功后保存 `session_key`，用本次会话响应初始化角色、属性、技能和背景，再调用 `GET /stamina` 刷新已扣体力。
4. `status=RUNNING` 时仅提交 `{"client_round":<响应中的 round>}` 到 `/rounds/resolve`，按 `actions[].action_order` 和 `targets[]` 播放，结束后以同一响应的阵容快照校正；不要本地结算。`PAUSED` 时按需调用 `/resume`；切后台或暂时离开可保留会话，返回时 GET 会话恢复。`STAGE_COMPLETE` 时先展示 `pending_rewards` 与 `random_drops`，可调用 `/stages/continue` 进入下一关或由玩家确认结束；`FAILED` 时可 `/stages/retry` 或确认结束。继续和失败重试成功后刷新体力；`POST /progress/stage-clear` 仅核实已完成关卡，不能用于推进关卡或发奖。
5. 玩家明确退出并结算时调用 `POST /sessions/{sessionKey}/end`；它可从 `RUNNING`、`PAUSED`、`STAGE_COMPLETE`、`FAILED` 结束，但未通关的当前关卡不生成奖励。以 `/end` 首次响应或相同幂等键重放的 `granted_rewards` 为实际入账结果，用 `items` 更新物品背包，装备与 NPC 依据奖励结果刷新各自仓库。`random_drops` 只用于掉落表现。普通 GET 会话不重放已播放的 `actions`，也不返回那次 `/end` 的发奖明细；结束请求结果不确定时先用原键重试 `/end`。
6. 挂机页用 `GET /afk-reward` 显示服务端累计量；`POST /afk-reward/collect` 成功后遍历 `collected_rewards[]` 展示全部实际入账项，不能只读兼容字段 `collected_item`。`POST /afk-reward/accelerate` 无请求体，必须带 `Idempotency-Key`，成功后用 `total_rewards`、`items` 和内嵌 `stamina` 更新显示。体力倒计时仅用于展示，到零后重新 GET `/stamina`，不得本地确认恢复或扣费。

`start`、`pause`、`resume`、`rounds/resolve`、`continue`、`retry`、`end`、挂机领取和加速，每次新操作使用 1 至 64 字符的新 `Idempotency-Key`；同一次超时、断网或 5xx 重试必须保留原键与原请求体，直到确认首次结果。尤其 `/end` 的键应持久化到结果确认，不能改键重试或用 GET 会话代替发奖回执。`6506` 先 GET 会话取得最新 `round`；`6507` 表示同键改操作或载荷，停止重试；`6509` 先 GET 后再判断是否发起新操作；`6511` 刷新体力并提示不足；`6513` 关闭加速入口；`6512` 是好友或商城等外部体力发放的物理上限冲突，`6514` 是挂机奖励物理上限冲突，两者本次操作均未成功，不得自行补发。完整错误码、奖励结构和回合播放字段见本文件后续 Idle Adventure 专节。

Unity 联调至少覆盖：新 v3 会话及旧 v1/v2 会话恢复；六槽位空槽和缺失的头像、立绘、模型、技能图标；有背景、无背景及无效背景 Key；继续、重试和重连后按会话切换背景；单体、范围、治疗及旧动作缺失 `targets` 的播放；体力不足、重复请求和 `/end` 超时重试；挂机跨版本一次领取两种奖励。当前机器没有 Unity 工程，以上是客户端实施与验收清单，不表示 Unity 代码已完成。

## 19. 玩家经验、任务和好友接口（V25-V29、V81/V81.1/V82）

### 19.1 最终客户端效果

玩家登录、注册、微信登录以及 `GET /api/user/me` 都返回同一套成长字段。登录和 `/user/me` 会先刷新等级曲线并执行惰性对账，再组装用户响应；客户端不需要单独调用“刷新等级”接口。

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `level` | number | 当前持久化等级；对账只升不降 |
| `totalExperience` | number | 单调递增的累计经验 |
| `levelCurveRevision` | number | 本次响应采用的已发布等级曲线版本 |
| `currentLevelExperience` | number | `totalExperience - 当前等级累计经验门槛` |
| `requiredExperienceForNextLevel` | number / null | 从当前等级升到下一级所需的等级内经验；满级为 `null` |
| `experienceProgressBps` | number | 当前等级进度基点，范围 `0..10000` |
| `maxLevel` | boolean | 是否达到当前已发布曲线的最高等级 |

前端进度条使用：

```text
experienceProgressBps / 10000
```

不要使用累计经验直接除以下一级累计门槛。`requiredExperienceForNextLevel` 是相邻两个累计门槛的差值。达到当前最高等级时，服务端继续保存后续经验，返回 `maxLevel=true`、`requiredExperienceForNextLevel=null` 和 `experienceProgressBps=10000`；前端必须在 `maxLevel=true` 时隐藏普通进度分母。以后运营追加等级后，玩家会在登录、获取经验或批量对账时按已保存经验自动升级。

经验发放或成长对账提交成功后，在线本人会收到 WebSocket 定向消息。实时发送失败不回滚已经提交的经验事务；客户端漏收后可通过登录或 `GET /api/user/me` 恢复权威状态。

```json
{
  "type": "PLAYER_PROGRESSION_CHANGED",
  "requestId": null,
  "eventId": "6f0b48d8-7349-45a6-b7f0-d55d116e4c0f",
  "playerId": 1,
  "data": {
    "playerId": 1,
    "changeType": "EXPERIENCE_GRANTED",
    "sourceType": "TASK_REWARD",
    "sourceId": "901",
    "grantedExperience": 100,
    "totalExperience": 200,
    "beforeLevel": 2,
    "level": 3,
    "leveledUp": true,
    "maxLevel": false,
    "currentLevelExperience": 0,
    "requiredExperienceForNextLevel": 300,
    "experienceProgressBps": 0,
    "curveRevision": 3
  },
  "serverTime": "2026-08-14T12:00:00"
}
```

`changeType` 为 `EXPERIENCE_GRANTED` 或 `RECONCILED`。对账消息的 `sourceType`、`sourceId` 为 `null`，`grantedExperience=0`；客户端仍应覆盖全部权威成长字段，不应把它当作新增经验动画。

### 19.2 建筑升级经验

建筑升级目标等级的经验配置保存在 `building_level_config.experience_reward`。开始升级时，奖励被复制到 `building_task.experience_reward`，以后运营修改配置不会改变已经开始的任务。

规则：

1. 初次建造不发升级经验，1 级默认奖励为 0。
2. 只有 `UPGRADE` 类型任务完成时发放经验。
3. V26 上线前已经存在的建造、升级或已完成任务奖励快照保持 0，永远不做历史扫描补发。
4. 完成、同步完成和自动完成共用同一业务事务与同一经验幂等键，重复触发不会重复发放。
5. `BuildingResponse.experienceGrant` 只在本次调用实际对应升级经验结果时返回；普通建造或 0 奖励返回 `null`。

升级完成响应中的经验结果示例：

```json
{
  "playerBuildingId": 10,
  "level": 2,
  "status": "COMPLETED",
  "experienceGrant": {
    "playerId": 1,
    "sourceType": "BUILDING_UPGRADE",
    "sourceId": "30001",
    "requestedExperience": 200,
    "grantedExperience": 200,
    "beforeExperience": 80,
    "afterExperience": 280,
    "beforeLevel": 1,
    "afterLevel": 2,
    "leveledUp": true,
    "maxLevel": false,
    "currentLevelExperience": 180,
    "requiredExperienceForNextLevel": 200,
    "experienceProgressBps": 9000,
    "curveRevision": 1,
    "replayed": false
  }
}
```

`sourceId` 是不可变 `building_task.id`。首次执行返回 `replayed=false`；完全相同的幂等重放返回第一次的不可变快照并标记 `replayed=true`，不会把重试伪装成“本次未升级”。重放快照不一定等于玩家此刻状态，Unity 不能用它覆盖更新的经验和等级。

### 19.3 日常、周常和永久场景成就

#### 查询当前任务

```http
GET /api/tasks
Authorization: Bearer <player-token>
```

服务端使用注入的 `Asia/Shanghai` 业务时钟生成周期：

- 日任务：`yyyyMMdd`，例如 `20260724`。
- 周任务：ISO week-based year 加两位周数，例如 `202630`。

响应示例：

```json
{
  "businessTime": "2026-07-24 18:00:00",
  "businessDate": "2026-07-24",
  "dailyPeriodKey": "20260724",
  "weeklyPeriodKey": "202630",
  "tasks": [
    {
      "id": 81001,
      "taskCode": "DAILY_BUILDING_UPGRADE",
      "taskName": "每日升级",
      "description": "每日完成1次建筑升级",
      "taskCategory": "ROUTINE",
      "periodType": "DAILY",
      "periodKey": "20260724",
      "objectiveType": "BUILDING_UPGRADE",
      "targetSceneId": null,
      "targetSceneKey": null,
      "targetSceneName": null,
      "targetItemId": null,
      "targetItemKey": null,
      "targetItemName": null,
      "targetItemIcon": null,
      "targetItemType": null,
      "targetAmount": 1,
      "progressAmount": 1,
      "experienceReward": 100,
      "randomNpcPoolName": null,
      "randomNpcDrawCount": 0,
      "fixedRewards": [],
      "randomNpcCandidates": [],
      "status": "COMPLETED",
      "canClaim": true,
      "completedTime": "2026-07-24 17:55:00",
      "claimedTime": null
    }
  ]
}
```

当前可信任务目标类型：

| `objectiveType` | 触发时机 |
| --- | --- |
| `BUILDING_COMPLETION` | 初次建造任务完成 |
| `BUILDING_UPGRADE` | 建筑升级任务完成 |
| `RESOURCE_COLLECT_COUNT` | 从建筑实际收获指定目标物品；按产出该物品的不同玩家建筑数累计 |
| `SCENE_SLOT_UNLOCK_COUNT` | 指定场景累计取得 `targetAmount` 格永久放置权限，目标允许 1～32 |
| `SCENE_FULLY_BUILT` | 指定场景 32 格全部取得放置权限，且每格均有已完成建筑 |

任务进度只由服务端业务事件或服务端权威状态对账写入，客户端不能直接提交进度。资源收获目标会按 `targetItemId` 严格匹配，木材事件不会推进铁矿任务；建筑任务 ID 或生产收取记录 ID 与目标物品 ID 是稳定来源。场景解锁里程碑直接统计格子响应中的 `placementUnlocked=true`，不要求该格已经建成建筑；购买或净化事务会即时核对，同一操作跨过多个档位时会完成全部已达到且尚未完成的宝箱。查询任务列表还会为老玩家按当前数据库状态补完成，无需 Unity 新增进度上报接口。

#### 领取任务经验

```http
POST /api/tasks/{playerTaskId}/claim
Authorization: Bearer <player-token>
```

请求体：无。

```json
{
  "task": {
    "id": 81001,
    "taskCode": "DAILY_BUILDING_UPGRADE",
    "periodType": "DAILY",
    "periodKey": "20260724",
    "targetAmount": 1,
    "progressAmount": 1,
    "experienceReward": 100,
    "status": "CLAIMED",
    "canClaim": false,
    "completedTime": "2026-07-24 17:55:00",
    "claimedTime": "2026-07-24 18:01:00"
  },
  "experienceGrant": {
    "playerId": 1,
    "sourceType": "TASK_REWARD",
    "sourceId": "81001",
    "requestedExperience": 100,
    "grantedExperience": 100,
    "beforeExperience": 280,
    "afterExperience": 380,
    "beforeLevel": 2,
    "afterLevel": 3,
    "leveledUp": true,
    "maxLevel": false,
    "currentLevelExperience": 80,
    "requiredExperienceForNextLevel": 300,
    "experienceProgressBps": 2666,
    "curveRevision": 1,
    "replayed": false
  },
  "rewardGrants": []
}
```

任务实例冻结任务名称、描述、目标、排序、配置版本、经验和资产权益。运营修改任务只影响以后物化的新周期实例。奖励为 0 时 `experienceGrant=null`。`rewardGrants` 返回固定资产和随机 NPC 的实际发放结果；对已领取任务重试会返回第一次的任务状态、原经验流水和原随机结果，`experienceGrant.replayed=true`，不重复发放。该历史快照不能覆盖客户端更晚的成长状态。V64 场景宝箱的完整字段和 Unity 接入见第 42 章。

### 19.4 游戏内好友列表（非微信好友）

当前实现按本项目玩家 ID 发起申请，必须由接收方明确同意后才建立双向好友关系。它不是微信好友关系链，不会读取微信昵称、头像、openid 或云存储，也不能替代微信开放数据域的好友列表。微信官方好友和赠礼能力见第 20 章；完整搜索、申请列表、同意、拒绝、取消和过期规则见第 26 章。

#### 发起好友申请（旧客户端兼容入口）

```http
POST /api/friends/{friendPlayerId}
Authorization: Bearer <player-token>
```

V37 起该接口只创建或返回一条 `PENDING` 好友申请，不再立即写入 `player_friend`。推荐新客户端改用 `POST /api/friends/requests`；不能申请自己，目标玩家必须存在，双方已经是正式好友时返回 `6708`。

#### 删除好友

```http
DELETE /api/friends/{friendPlayerId}
Authorization: Bearer <player-token>
```

重复删除不会产生额外副作用。

#### 按等级查询好友

```http
GET /api/friends?limit=20
Authorization: Bearer <player-token>
```

V38 起，每个好友对象同时返回 `onlineStatus`、`presenceVersion`、`lastActiveTime` 和 `lastOfflineTime`。客户端合并在线增量时只能接受版本更大的状态，完整契约见第 27 章。

固定排序：`level DESC, playerId ASC`。后续页必须把上一页 `nextCursor.level` 和 `nextCursor.playerId` 同时传回：

```http
GET /api/friends?cursorLevel=12&cursorPlayerId=205&limit=20
```

```json
{
  "items": [
    {
      "playerId": 101,
      "nickname": "高等级好友",
      "level": 15,
      "friendsSince": "2026-07-24 12:00:00"
    },
    {
      "playerId": 205,
      "nickname": "同级好友",
      "level": 12,
      "friendsSince": "2026-07-24 12:05:00"
    }
  ],
  "nextCursor": {
    "level": 12,
    "playerId": 205
  },
  "hasMore": true
}
```

`cursorLevel` 和 `cursorPlayerId` 必须同时为空或同时存在，`limit` 范围为 1 至 100。数据库只保存一条规范化关系：`lower_player_id < higher_player_id`，并以唯一键防止并发重复建立。V37 后由申请建立的关系还保存 `accepted_request_id`，删除后会在原申请写入 `relationshipEndedTime`，旧同意请求不能复活关系。

### 19.5 运营增加玩家经验

仅 `CONFIG_ADMIN` 可调用。当前接口只允许增加正经验，不允许直接设置、扣减经验或强制降级。

```http
POST /api/ops/player-experience/adjustments
Authorization: Bearer <operator-token>
Content-Type: application/json
```

```json
{
  "playerId": 1,
  "adjustmentRequestId": "TICKET_20260724_001",
  "experienceAmount": 500,
  "reason": "客服补偿"
}
```

约束：

- `adjustmentRequestId` 为 1 至 128 字符，只允许字母、数字、点、下划线、冒号和短横线，并作为工单幂等键。
- 管理端页面默认自动生成并只读展示该请求号；提交失败或网络超时时必须沿用原号重试，成功后才生成下一笔新号。直接调用接口的客户端可自行生成符合规则的请求号。
- `experienceAmount` 为正整数，单次管理端安全上限是 `9,000,000,000,000,000`。
- `reason` 必填，最长 255 字符，并写入不可变经验流水。
- 同一玩家、同一请求号、同一金额且去除首尾空格后的 `reason` 相同时返回第一次完整结果，`replayed=true`；修改金额或规范化后的原因都会返回幂等冲突。

响应外层结构：

```json
{
  "adjustment": {
    "id": 90001,
    "playerId": 1,
    "playerNickname": "Player 1",
    "adjustmentRequestId": "TICKET_20260724_001",
    "requestedExperience": 500,
    "grantedExperience": 500,
    "beforeExperience": 380,
    "afterExperience": 880,
    "beforeLevel": 3,
    "afterLevel": 4,
    "leveledUp": true,
    "maxLevel": false,
    "currentLevelExperience": 280,
    "requiredExperienceForNextLevel": 400,
    "experienceProgressBps": 7000,
    "curveRevision": 1,
    "operatorId": 11,
    "operatorUsername": "config_admin",
    "operatorDisplayName": "配置管理员",
    "referenceNo": "TICKET_20260724_001",
    "reason": "客服补偿",
    "createTime": "2026-07-24 18:10:00"
  },
  "replayed": false
}
```

查询调整历史：

```http
GET /api/ops/player-experience/adjustments?playerId=1&beforeId=90001&limit=30
Authorization: Bearer <operator-token>
```

结果按记录 ID 倒序游标分页，`limit` 范围为 1 至 100。管理端页面路由为 `/experience-adjustments`。

### 19.6 运营批量成长对账

仅 `CONFIG_ADMIN` 可调用。对账已经从“调用方自己维护游标的临时批处理”升级为可恢复的持久任务：任务创建时冻结目标曲线 revision、玩家 ID 上界和批量数，浏览器刷新、节点重启或请求超时后都能继续同一任务。

#### 19.6.1 创建任务并推进第一批

```http
POST /api/ops/player-experience/reconciliation
Authorization: Bearer <operator-token>
Content-Type: application/json
```

```json
{
  "startAfterPlayerId": 100,
  "batchSize": 500
}
```

两个字段都可以省略。`startAfterPlayerId` 只用于新建任务，表示跳过该 ID 及之前的玩家；`batchSize` 默认值、管理端默认值和硬上限统一为 500，合法范围为 1 至 500。

#### 19.6.2 继续已有任务

```http
POST /api/ops/player-experience/reconciliation
Authorization: Bearer <operator-token>
Content-Type: application/json
```

```json
{
  "taskId": 1201,
  "afterPlayerId": 200
}
```

`taskId` 必填；`afterPlayerId` 可省略，传入时必须等于服务端当前 `nextAfterPlayerId`，用于阻止旧页面重复推进已经变化的游标。续跑时禁止传 `startAfterPlayerId`，批量数已经冻结；如果回传 `batchSize`，必须与任务原值一致。只传 `afterPlayerId` 而不传 `taskId` 会返回参数错误，避免静默创建无法恢复的孤儿任务。

创建、继续、查询详情和失败重试均返回同一任务响应：

```json
{
  "scanned": 500,
  "reconciled": 17,
  "skipped": 482,
  "failed": 1,
  "nextAfterPlayerId": 600,
  "hasMore": true,
  "curveRevision": 3,
  "failedPlayerIds": [156],
  "taskId": 1201,
  "taskStatus": "SCANNING",
  "operation": "SCAN",
  "targetCurveRevision": 3,
  "currentRevision": 4,
  "superseded": true,
  "maxPlayerId": 9800,
  "scanCompleted": false,
  "unresolvedFailureCount": 1,
  "totalScanned": 500,
  "totalReconciled": 17,
  "totalSkipped": 482,
  "totalFailureAttempts": 1,
  "createTime": "2026-08-14T12:00:00",
  "updateTime": "2026-08-14T12:00:03",
  "completedTime": null
}
```

前八个字段继续保留原响应顺序和含义；新增字段是持久任务的权威状态。

| 字段 | 说明 |
| --- | --- |
| `scanned/reconciled/skipped/failed` | 本次请求处理的批次计数，不是任务累计值 |
| `nextAfterPlayerId/hasMore` | 仅扫描阶段使用；扫描结束后分别为 `null/false` |
| `curveRevision` | 兼容字段，等于 `targetCurveRevision` |
| `failedPlayerIds` | 本次批次最多返回 50 个失败玩家 ID；完整明细走 failures 接口 |
| `taskStatus` | `SCANNING`、`RETRY_REQUIRED` 或 `COMPLETED` |
| `operation` | `SCAN`、`RETRY` 或只读详情 `DETAIL` |
| `targetCurveRevision` | 创建任务时冻结的目标曲线；历史任务永远按该版本处理 |
| `currentRevision` | 当前数据库已发布曲线版本 |
| `superseded` | 当前版本是否高于任务冻结版本；为 `true` 时完成旧任务不代表最新曲线已全服完成 |
| `maxPlayerId` | 创建任务时冻结的玩家 ID 上界；任务期间新注册玩家不进入该任务 |
| `unresolvedFailureCount` | 当前仍未解决的失败玩家数量；必须降为 0 才能完成 |
| `total*` | 整份任务的累计扫描、修复、跳过和失败尝试次数 |

管理端“玩家成长对账”页面会把内部曲线 revision 转换为运维可读的中文：`targetCurveRevision=1` 显示为“目标等级曲线：第 1 版”，`currentRevision=1` 显示为“当前等级曲线：第 1 版”，不再使用容易误解的 `r1 / r1` 写法。目标等级曲线是任务创建时冻结的版本，当前等级曲线是数据库最新正式版本；两者不一致时页面会明确提示任务仍按目标版本执行。接口字段和数字值保持不变，便于 Unity、脚本和审计继续使用稳定契约。

#### 19.6.3 查询任务列表和详情

```http
GET /api/ops/player-experience/reconciliation?status=RETRY_REQUIRED&triggerType=CURVE_PUBLISHED&beforeId=1201&limit=50
Authorization: Bearer <operator-token>
```

`status` 可取 `SCANNING / RETRY_REQUIRED / COMPLETED`；`triggerType` 可取 `MANUAL / CURVE_PUBLISHED`。列表按任务 ID 倒序游标分页，`limit` 默认 50、范围 1 至 500。

```json
{
  "items": [
    {
      "taskId": 1201,
      "triggerType": "CURVE_PUBLISHED",
      "taskStatus": "RETRY_REQUIRED",
      "targetCurveRevision": 3,
      "currentRevision": 4,
      "superseded": true,
      "maxPlayerId": 9800,
      "nextAfterPlayerId": 9800,
      "batchSize": 500,
      "scanCompleted": true,
      "totalScanned": 9800,
      "totalReconciled": 317,
      "totalSkipped": 9482,
      "unresolvedFailureCount": 1,
      "totalFailureAttempts": 2,
      "lastError": "玩家等级数据与曲线不兼容",
      "createTime": "2026-08-14T12:00:00",
      "updateTime": "2026-08-14T12:02:00",
      "completedTime": null
    }
  ],
  "nextBeforeId": 1201,
  "hasMore": true
}
```

查询单份任务：

```http
GET /api/ops/player-experience/reconciliation/1201
Authorization: Bearer <operator-token>
```

详情使用前述统一任务响应，`operation=DETAIL`，本批计数均为 0。

#### 19.6.4 查询和重试失败玩家

```http
GET /api/ops/player-experience/reconciliation/1201/failures?limit=100&unresolvedOnly=true
Authorization: Bearer <operator-token>
```

`unresolvedOnly` 默认 `true`；失败明细按玩家 ID 正序游标分页，`limit` 默认 100、范围 1 至 500。

```json
{
  "items": [
    {
      "playerId": 156,
      "status": "UNRESOLVED",
      "attemptCount": 2,
      "firstErrorCode": 1035,
      "lastErrorCode": 1035,
      "lastErrorMessage": "玩家等级数据与曲线不兼容",
      "firstFailedTime": "2026-08-14T12:00:02",
      "lastAttemptTime": "2026-08-14T12:03:10",
      "resolvedTime": null
    }
  ],
  "nextAfterPlayerId": null,
  "hasMore": false
}
```

扫描完成且状态为 `RETRY_REQUIRED` 后，由运营修复数据，再显式执行：

```http
POST /api/ops/player-experience/reconciliation/1201/retry
Authorization: Bearer <operator-token>
```

每次最多重试任务冻结的 `batchSize` 条失败记录。选择顺序为 `lastAttemptTime` 最早优先，同一时间再按玩家 ID，持续失败的前一批不会饿死后续玩家。已解决记录保留审计，不会再次进入未解决集合。

#### 19.6.5 状态机、事务和自动任务规则

1. 状态固定为 `SCANNING -> RETRY_REQUIRED -> COMPLETED`，无失败时可从 `SCANNING` 直接完成；只有 `scanCompleted=true` 且 `unresolvedFailureCount=0` 才能进入 `COMPLETED`。
2. 扫描使用 `id > nextAfterPlayerId AND id <= maxPlayerId ORDER BY id`，不使用大偏移量 `OFFSET`。
3. 每个玩家使用独立 `REQUIRES_NEW` 短事务和 `users ... FOR UPDATE`；玩家成长更新与任务游标/计数在同一事务提交，任务游标不会越过未持久化的失败。
4. 失败明细和任务失败计数在同一独立短事务提交；单点失败不会回滚同批其他玩家，也不会因进程退出而永久丢失。
5. 对账只提升 `level` 和 `level_curve_revision`，绝不增加、扣减或伪造经验，也绝不降级。玩家已经处于更高曲线版本时，旧任务只跳过。
6. 数据库条件租约是多节点最终互斥边界；每处理一名玩家都会续租。旧租约持有者无法继续推进游标或重复累计。
7. 发布新等级曲线时，曲线版本、刷新 Outbox 和 `CURVE_PUBLISHED` 自动对账任务在同一个发布事务内原子写入，请求键 `CURVE:{revision}` 唯一幂等。
8. 后台每轮按最久未推进时间轮转多份自动任务；单份任务级异常会记录最后错误，保留当前 `owner` 作为 fencing token，并把 `lease_until` 重设为一个租约周期后的时间。到期前所有节点均跳过该任务，到期后任一节点可原子替换 `owner` 并继续处理；该数据库退避既避免每 5 秒热循环，也不会永久饿死后续曲线任务。扫描中的逐玩家失败会被持久化，扫描结束后不会无限自动重试，必须由运营显式触发失败重试。
9. 登录、玩家资料、建筑、地块和地图等等级准入路径会按最新曲线执行惰性对账；经验发放前也会主动检查最新 revision，避免配置发布后的短暂旧缓存窗口。

管理端页面路由为 `/progression-reconciliation`，支持页面刷新恢复、状态/来源筛选、任务列表加载更多、冻结版本过期提示、逐批扫描、累计计数、未解决失败明细刷新/加载更多以及显式失败重试。

### 19.7 管理端经验配置

#### 玩家等级曲线

通用配置中心模块：

```text
moduleCode = PLAYER_PROGRESSION
configKey  = LEVEL_CURVE
```

配置是完整快照：

```json
{
  "revision": 1,
  "levels": [
    {"level": 1, "requiredTotalExperience": 0, "publishedRevision": 1},
    {"level": 2, "requiredTotalExperience": 100, "publishedRevision": 1},
    {"level": 3, "requiredTotalExperience": 300, "publishedRevision": 1},
    {"level": 4, "requiredTotalExperience": 600, "publishedRevision": null}
  ]
}
```

已发布等级、门槛和 `publishedRevision` 禁止修改或删除；只能在尾部连续追加等级。新增行的 `publishedRevision` 必须为 `null`，发布后由服务端写入新 revision。Lv.1 门槛必须为 0，所有累计门槛严格递增，最多 10000 级。

当前实现比“有玩家达到后才阻断回滚”更保守：任何会删除或修改已发布等级的历史回滚都直接校验失败，不开放自动降级或强制降级入口。最高等级门槛只表示当前等级上限，不是累计经验软上限；系统没有需要与等级同步维护的第二个 `MAX_TOTAL_EXPERIENCE` 配置。

#### 玩家任务

```text
moduleCode = PLAYER_TASK
configKey  = TASKS
```

运营可以配置日常/周常任务，以及永久场景宝箱的场景、名称、说明、经验、固定物品/资源、固定装备、随机 NPC 池、抽数、一亿分比概率、排序和启停。已发布任务不能删除，`taskCode` 不能修改；下线任务应设置 `enabled=false`。服务端在发布时重新解析资产名称、图标、品质和重复碎片，客户端提交的展示字段不可信。已有玩家任务实例继续使用创建时的完整权益快照。

#### 建筑升级经验

`LAND_BUILDING / WORLD` 的每个建筑等级配置包含 `experienceReward`。该值必须非负，开始升级时冻结到 `building_task`。管理端发布只影响以后开始的升级。

### 19.8 缓存、事务和幂等实现

等级计算：

1. 应用启动时从 `player_level_config` 加载完整曲线，构建不可变 `TreeMap` 快照。
2. `totalExperience -> level` 使用 `floorEntry`，时间复杂度为 `O(log N)`；经验发放只查询当前 revision，版本变化时才重建完整曲线。
3. 快照保存在 `AtomicReference`，只原子替换完整且已校验的新对象，不发布半成品。
4. 快照加载使用 `SUPPORTS`：无外部事务时以只读连接加载，已经持有玩家事务时复用同一连接，避免锁内再借连接造成连接池饥饿。
5. Redis 通知关闭或丢失时，默认每 30 秒从数据库检查 revision；登录、`/user/me`、等级准入和经验发放都会主动检查最新 revision。

配置发布：

1. 等级配置、revision、配置版本、审计、`config_refresh_outbox` 和 `CURVE_PUBLISHED` 自动对账任务在同一个数据库事务提交。
2. Redis 网络发送不在配置事务内；定时发布器在提交后以短租约领取 Outbox，再发送消息。
3. 发送失败写回退避重试时间；多节点竞争由租约和条件更新防重。
4. 节点收到 `PLAYER_LEVEL_CURVE_REFRESH` 后重新加载完整曲线；30 秒心跳继续兜底漏消息。

玩家写事务统一锁序：

```text
users
  -> player_building / 当前 player_task
  -> building_task
  -> users 成长条件更新
  -> player_experience_record
```

`PlayerAggregateLockService` 必须是每个跨模块玩家写事务的第一把数据库锁。`PlayerProgressionService.grantExperience` 使用 `Propagation.MANDATORY`，只能加入调用方现有事务，不能独立提交。项目使用显式 MyBatis SQL 和条件更新，不依赖 JPA/Hibernate 的延迟 flush 顺序。

经验发放使用：

```text
UNIQUE(player_id, source_type, source_id)
```

来源类型：

| `sourceType` | `sourceId` |
| --- | --- |
| `BUILDING_UPGRADE` | `building_task.id` |
| `TASK_REWARD` | `player_task.id` |
| `SYSTEM_BACKFILL` | 固定迁移批次号 |
| `ADMIN_ADJUSTMENT` | 运营 `adjustmentRequestId` |

命中已有流水时校验请求经验数量，并返回流水中保存的完整第一次结果。累计经验使用 `Math.addExact` 检测 `BIGINT/long` 上溢；溢出返回 `2002` 并回滚整个建筑、任务或运营事务，不做截断或静默丢失经验。

### 19.9 上线迁移和历史数据

V25 执行前建议在维护窗口先做只读预检：

```sql
SELECT COUNT(*) AS invalid_players
FROM users
WHERE level < 1 OR level > 100;

SELECT id,
       level,
       0 AS old_total_experience,
       50 * (level - 1) * level AS backfill_total_experience
FROM users
ORDER BY id
LIMIT 100;
```

上线规则：

1. 完整备份数据库，停写或进入维护窗口；若当前停在 V83，先执行仓库 `docs/database-script-guide.md` 的 V84 只读预检，结果符合要求后只启动一个应用实例执行 Flyway 至 V104。
2. V25 自带等级范围断言；存在 1 至 100 之外的历史等级时迁移直接失败，不静默降级。
3. V25 先为每名历史玩家写入一条 `SYSTEM_BACKFILL / V25_PLAYER_LEVEL_BACKFILL` 流水，再把累计经验回填为其原等级最低门槛。
4. BI 的“正常经验产出”必须过滤 `SYSTEM_BACKFILL`，避免把迁移创建的基准经验计入运营产出。
5. V26 的 `building_task.experience_reward` 默认值为 0；历史进行中和已完成任务不补发。
6. V81 创建任务与失败明细表；V81.1 回填请求键和触发来源、允许系统任务不绑定运营员并增加公平失败重试索引；V82 在改结构前检查所有玩家快照能否由当前只追加曲线解释，并拒绝超过 JavaScript 安全整数上限的玩家经验、曲线门槛、经验流水及奖励来源；V83 创建默认关闭的冻结 v1；V84 保留 v1、归一化终态、加固约束并切换未来分配到兼容 v2；V85 增加 NPC/装备一键升级批次、逐级流水关联和初始装备材料约束；V86 前向把整批同款材料上限放宽到 5000；V87 增加指定资源收获次数任务；V88 前向加固其历史快照、约束和依赖查询索引；V89 增加按场景开辟指定格数的永久宝箱里程碑；V90 增加永久累计成就；V91 加固永久累计进度上限；V92 增加 NPC 获取渠道和独立发布版本状态；V93 不改写 V92，前向加固渠道数据库契约并防止列变窄时静默截断；V94 分离运营编号与幂等键；V95/V96 完善微信客服工作台；V97/V98 建立固定头像编号与来源约束；V99 归一历史远程头像并下线远程来源；V100 补齐清理原因并收紧系统头像合同；V101 交付固定头像正式目录与 Unity 清单证据；V102 不改写 V101，前向加固目录与清单合同；V103 取消 `AVATAR_001` 无摘要历史例外并将其无条件降为 `STAGED`；V104 将未验收版本号提升为 1并收紧正整数约束。
7. 升级 V102 前必须停止旧版本写入、备份数据库，并确认 `avatar_resource_manifest_import` 和 `avatar_resource_manifest_item` 均为空；任一表非空时迁移会安全阻断并保留原数据，必须停止发布且不得 `repair`。迁移完成后确认 `flyway_schema_history` 中 V81、V81.1、V82、V83、V84、V85、V86、V87、V88、V89、V90、V91、V92、V93、V94、V95、V96、V97、V98、V99、V100、V101、V102、V103、V104 均为 `success=1`，其中 V85-V104 校验和必须分别为 `-1472509603`、`-179532443`、`-1481082424`、`-892889675`、`-1964148448`、`-319452238`、`-613092297`、`1615105684`、`-2034435388`、`-246164165`、`-1236124090`、`292469054`、`151659432`、`-2063702854`、`67421201`、`-880935665`、`1155793846`、`-707871511`、`-172308135`、`-37724955`；再核对任务/失败表、成长 CHECK、新手引导 v1/v2、批量升级表、材料索引、整批上限 5000、资源任务目标快照与 NULL 安全约束、场景成就三元唯一键及 1～32 边界、永久累计统计表与唯一身份索引、`player_task_lifetime_stat.progress_amount` 的 `0..9000000000000000` 约束，以及 V93 后 NPC 获取渠道、V102 后头像目录与清单约束、V104 后 `AVATAR_001` 的 `STAGED` 状态，依次启动其他节点并观察曲线 revision 与自动任务状态。
8. V1-V104 已冻结，发布后禁止修改或用 `repair` 掩盖漂移，下一次正式数据库变化从 V105 开始。

### 19.10 新增业务错误码

| code | message | 典型场景 |
| --- | --- | --- |
| 2002 | 累计经验已达系统上限 | `Math.addExact` 检测到累计经验上溢 |
| 2003 | 经验发放幂等请求与原请求不一致 | 同一来源 ID 改传其他经验数量 |
| 2004 | 玩家等级配置不可用 | 曲线缺失、断层、回退或玩家数据不兼容 |
| 2005 | 玩家成长状态已变化，请重试 | 条件更新未命中并发前值 |
| 6601 | 玩家任务不存在 | 任务不存在或不属于当前玩家 |
| 6602 | 任务尚未完成 | `IN_PROGRESS` 状态尝试领取 |
| 6603 | 玩家任务配置错误 | 任务快照或正式配置无效 |
| 6604 | 玩家任务状态已变化，请刷新后重试 | 并发领取或状态条件更新失败 |
| 6605 | 任务进度幂等请求与原请求不一致 | 同一可信来源重复提交不同进度 |
| 6606 | 任务奖励快照损坏 | 权益快照或已持久化发放流水不完整、不一致 |
| 6607 | 任务奖励发放失败 | 物品、装备、NPC 或发放流水写入失败，事务整体回滚 |
| 6701 | 不能将自己添加为好友 | `friendPlayerId` 等于当前玩家 |
| 6708 | 双方已经是游戏好友 | 对正式好友重复发起申请 |
| 6709 | 游戏内好友申请当前未开放 | 正式策略禁止新建申请 |
| 6710 | 好友申请不存在 | 申请 ID 不存在或已被级联清理 |
| 6711 | 该好友申请已过期，请重新发起 | 实时到期检查或过期终态 |
| 6712 | 好友申请状态已变化，请刷新后重试 | 使用不匹配动作处理其他终态 |
| 6713 | 你无权处理该好友申请 | 非接收方同意/拒绝或非发起方取消 |
| 6714 | 你或对方的好友数量已达上限 | 同意时任一方达到正式策略上限 |
| 6715 | 待处理的发出申请数量已达上限 | 发起方有效待处理数量超限 |
| 6716 | 对方待处理的收到申请数量已达上限 | 接收方有效待处理数量超限 |
| 6717 | 今日发起好友申请次数已达上限 | 北京时间自然日创建总数超限 |
| 6718 | 好友申请仍在重新发送冷却期内 | 同方向拒绝后未过冷却时间 |
| 6719 | 好友申请正式配置不可用 | 策略缺失、损坏或版本无效 |
| 6720 | 该好友申请已不能用于建立好友关系 | 旧关系已删除后重放原同意申请 |
| 6801 | 微信好友赠礼配置错误 | 礼包、类型、奖励映射或正式配置不完整 |
| 6802 | 微信礼包道具与已发布配置不一致 | 回调 `GoodsList` 与固化的微信道具 ID/数量不一致 |
| 6803 | 微信礼包订单与首次请求不一致 | 同一 `OrderId` 被重放为其他玩家或其他请求内容 |

### 19.11 本次验证结果

2026-07-27 已执行并通过：

```powershell
mvn.cmd -q "-Dproject.build.directory=target-codex" test

$env:RUN_MYSQL_INTEGRATION_TESTS='true'
mvn.cmd -q "-Dproject.build.directory=target-codex" "-Dtest=FlywayMigrationIntegrationTest,BusinessConcurrencyIntegrationTest" test

$env:RUN_MYSQL_INTEGRATION_TESTS='true'
mvn.cmd -q "-Dproject.build.directory=target-codex" "-Dtest=OperatorConfigPersistenceIntegrationTest" test

npm.cmd --prefix admin-web run build
```

验证范围包括：后端全量单元/契约测试、微信官方 AES 样例、JSON/XML 明文与安全模式回包、MySQL 8.0 上 V1-V30 完整 Flyway 迁移、建筑/任务/经验/游戏内好友并发路径、6 路相同微信 `OrderId` 并发只发一次、微信礼包运营发布持久化、登录和批量成长对账，以及管理端 Vue TypeScript 类型检查和 Vite 生产构建。四组命令退出码均为 0。

## 20. 微信官方好友列表与好友赠礼（V30）

### 20.1 最终效果与系统边界

玩家在微信小游戏内打开好友赠礼界面后，最终效果是：

1. 好友数据来自微信开放数据域，展示真实微信好友的昵称、头像和云存储等级，不使用后端 `/api/friends` 的本地玩家关系。
2. 好友按云存储中的 `player_level DESC` 排序；同等级保持微信原始返回顺序，列表不会因刷新随机跳动。
3. 每个好友的赠送按钮由 `wx.getFriendSendGiftStatus` 返回的 `blockSend/blockCode/blockReason` 决定，不能只靠客户端本地计数。
4. 点击可赠送好友后，由开放数据域调用 `wx.sendGiftToFriend({ giftId, openid })`，不是调用自建“加资源”接口。
5. 微信平台把 `minigame_deliver_goods` 事件推送到后端；后端验签、解密、校验礼包映射并按 `OrderId` 幂等发放资源。
6. 资源内容、平台道具映射、每日领取上限和当前发送礼包都由管理端配置。客户端只展示目录，不能提交或覆盖实际奖励。
7. 官方好友赠礼有效期按微信规则为 48 小时；类型 18 礼包每日领取上限必须配置为 1 至 10，并与微信 MP 后台保持一致。

隐私与职责边界：服务端不能调用 `wx.getFriendCloudStorage` 获取微信好友关系，也不把微信好友列表落库。`/api/friends` 继续服务于游戏内玩家 ID 好友；微信好友列表、送礼状态和发起赠送全部在开放数据域执行。

### 20.2 微信官方能力与最低版本

当前接入对照以下微信小游戏官方 API：

| 官方能力 | 运行位置 | 用途 |
| --- | --- | --- |
| `wx.setUserCloudStorage` | 主域 | 把当前玩家等级写入微信云存储 |
| `wx.getFriendCloudStorage` | 仅开放数据域 | 读取微信好友昵称、头像、openid 和等级 KV |
| `wx.getFriendSendGiftStatus` | 仅开放数据域 | 批量查询指定好友当前能否接收该礼包 |
| `wx.sendGiftToFriend` | 仅开放数据域 | 按 `giftId + openid` 发起官方好友赠礼 |
| `wx.createPageManager` / `PageManager` | 小游戏客户端 | 加载和展示微信 OPENLINK 原生页面 |

`wx.createPageManager` 从基础库 3.6.7 开始支持；好友送礼状态和发送接口从基础库 3.11.2 开始支持，因此本功能统一最低基础库版本为 `3.11.2`。低版本必须隐藏赠礼入口或显示升级微信提示，不能降级为客户端自发奖励。

官方参考：

- `https://developers.weixin.qq.com/minigame/dev/api/open-api/data/wx.getFriendCloudStorage.html`
- `https://developers.weixin.qq.com/minigame/dev/api/open-api/data/wx.getFriendSendGiftStatus.html`
- `https://developers.weixin.qq.com/minigame/dev/api/open-api/data/FriendSendGiftStatus.html`
- `https://developers.weixin.qq.com/minigame/dev/api/open-api/data/wx.sendGiftToFriend.html`
- `https://developers.weixin.qq.com/minigame/dev/api/open-api/openlink/wx.createPageManager.html`
- `https://developers.weixin.qq.com/minigame/dev/api/open-api/openlink/PageManager.load.html`
- `https://developers.weixin.qq.com/minigame/dev/api/open-api/openlink/PageManager.show.html`

### 20.3 客户端礼包目录

```http
GET /api/wechat/friend-gifts/catalog
Authorization: Bearer <player-token>
```

成功响应：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "provider": "WECHAT_OFFICIAL",
    "friendListSource": "WECHAT_OPEN_DATA_CONTEXT",
    "minimumBaseLibraryVersion": "3.11.2",
    "pageManagerOpenlink": "ZmNux0GwB1lH3lpraFJCGrHX5ex5aYVqrqi0Z8870fK-mygTZ2y5kuPlXMyf37Stc78m3CtMvHJlO5WTsJnIkoVfMW8GrUDd2W5Mb81f23ydxCJzcSi_wf8YtFE-WME3",
    "giftValidityHours": 48,
    "giftPackages": [
      {
        "giftId": "gift_friend_001",
        "giftName": "每日互助资源包",
        "dailyReceiveLimit": 3,
        "configVersion": 1,
        "rewards": [
          {"itemId": 1, "itemKey": "WOOD", "quantity": 100},
          {"itemId": 2, "itemKey": "FOOD", "quantity": 50}
        ]
      }
    ]
  }
}
```

目录只返回当前 `sendEnabled=true` 的类型 18 礼包，最多一个；没有启用礼包时 `giftPackages=[]`，客户端应隐藏赠送入口。响应不暴露微信道具 ID、回调密钥或服务端发奖参数。`pageManagerOpenlink` 是微信官方固定 OPENLINK，不由运营自行填写。

### 20.4 等级同步与开放数据域排序

主域在微信登录完成、`/api/user/me` 返回以及任何升级响应使 `level` 变化后，同步一次等级：

```javascript
const FRIEND_LEVEL_KEY = 'player_level'

function syncWechatFriendLevel(level) {
  wx.setUserCloudStorage({
    KVDataList: [
      { key: FRIEND_LEVEL_KEY, value: String(level) }
    ],
    fail(error) {
      console.error('同步微信好友等级失败', error)
    }
  })
}
```

主域先请求礼包目录，再把 `giftId` 发送给开放数据域；不要把服务端 token、奖励数量或微信消息密钥传进去：

```javascript
const openDataContext = wx.getOpenDataContext()
openDataContext.postMessage({
  type: 'WECHAT_FRIEND_GIFT_RENDER',
  giftId: catalog.giftPackages[0].giftId
})
```

开放数据域读取好友、按等级排序并合并平台送礼状态：

```javascript
const FRIEND_LEVEL_KEY = 'player_level'

function readLevel(friend) {
  const entry = (friend.KVDataList || []).find(item => item.key === FRIEND_LEVEL_KEY)
  const level = entry ? Number(entry.value) : 0
  return Number.isSafeInteger(level) && level >= 0 ? level : 0
}

function loadWechatFriends(giftId, render) {
  wx.getFriendCloudStorage({
    keyList: [FRIEND_LEVEL_KEY],
    success(cloudResult) {
      const friends = (cloudResult.data || [])
        .map((friend, originalIndex) => ({ ...friend, level: readLevel(friend), originalIndex }))
        .sort((left, right) => right.level - left.level || left.originalIndex - right.originalIndex)

      wx.getFriendSendGiftStatus({
        giftId,
        openidList: friends.map(friend => friend.openid),
        success(statusResult) {
          const statusByOpenid = new Map(
            (statusResult.data || []).map(status => [status.openid, status])
          )
          render(friends.map(friend => {
            const giftStatus = statusByOpenid.get(friend.openid)
            return {
              ...friend,
              giftStatus,
              canSendGift: Boolean(giftStatus) && giftStatus.blockSend === false
            }
          }))
        },
        fail(error) {
          console.error('查询微信好友送礼状态失败', error)
          render(friends.map(friend => ({ ...friend, canSendGift: false })))
        }
      })
    },
    fail(error) {
      console.error('读取微信好友列表失败', error)
      render([])
    }
  })
}
```

`FriendSendGiftStatus` 的字段为 `openid/blockSend/blockCode/blockReason`。只有明确返回 `blockSend=false` 才启用按钮；失败或缺失状态默认禁用，客户端可以展示平台返回的 `blockReason`，但不能自行绕过限制。

### 20.5 发起赠送与 PageManager

自定义好友列表中的赠送按钮必须在开放数据域调用：

```javascript
function sendGiftToWechatFriend(giftId, friend, reloadStatus) {
  wx.sendGiftToFriend({
    giftId,
    openid: friend.openid,
    success() {
      reloadStatus()
    },
    fail(error) {
      console.error('微信好友赠礼失败', error)
      reloadStatus()
    }
  })
}
```

`success` 只表示微信平台受理了赠送，不代表客户端可以给接收方加资源。实际资源到账只认服务端消息推送和 `wechat_gift_delivery_record`。

需要使用微信原生 OPENLINK 页面时，可在客户端复用一个 `PageManager`：

```javascript
const pageManager = wx.createPageManager()
const pageOptions = { openlink: catalog.pageManagerOpenlink }

await pageManager.load(pageOptions)
await pageManager.show(pageOptions)

// 场景销毁或不再使用时调用
pageManager.destroy()
```

不要把 `PageManager` 放进每个好友条目重复创建。`load/show` 失败时按微信返回错误处理，并保留开放数据域自定义列表作为产品选定的唯一入口，避免同一页面出现两套状态不一致的赠送按钮。

### 20.6 管理端礼包与资源配置

通用配置中心模块：

```text
moduleCode = WECHAT_FRIEND_GIFT
configKey  = GIFT_PACKAGES
```

完整配置快照示例：

```json
{
  "giftPackages": [
    {
      "id": null,
      "giftId": "gift_friend_001",
      "giftName": "每日互助资源包",
      "giftTypeId": 18,
      "dailyReceiveLimit": 3,
      "sendEnabled": true,
      "configVersion": null,
      "rewards": [
        {
          "id": null,
          "wechatGoodsId": "id_100001",
          "wechatGoodsQuantity": 1,
          "itemId": 1,
          "itemKey": "WOOD",
          "grantQuantity": 100
        }
      ]
    }
  ]
}
```

管理规则：

1. `giftId/wechatGoodsId/wechatGoodsQuantity` 必须与微信 MP 后台已发布礼包完全一致。
2. 类型 18 表示“给朋友送道具”，`dailyReceiveLimit` 为 1 至 10，可被选为当前发送礼包。
3. 类型 19 表示“送道具任务奖励”，`dailyReceiveLimit=null` 且 `sendEnabled=false`，仍可接收平台发货回调。
4. 同一时间最多一个类型 18 礼包启用发送；页面开启一个时会关闭其他发送开关，服务端发布时再次强校验。
5. 新礼包最多 100 项奖励，只能引用已启用 `item_config`，`itemId` 与 `itemKey` 必须匹配。
6. 已发布礼包不能删除，`giftId/giftTypeId` 不能修改；已发布微信道具与游戏物品映射不能增删改。
7. 已发布礼包只允许修改 `giftName/dailyReceiveLimit/sendEnabled`，每次更新使用 `configVersion` 乐观锁。
8. 需要调整实际资源时，在微信 MP 新建礼包和道具后，在管理端追加一个新礼包，先发布并验证，再切换发送开关；旧礼包保留以承接 48 小时在途回调。
9. 单项微信数量和游戏发放数量最大为 `9,000,000,000,000,000`；总礼包数上限 1000。运营仍应使用符合游戏经济规模的小数值。

管理端页面使用 `WechatFriendGiftEditor`：微信礼包类型使用选择器、发送状态使用开关、游戏内物品使用启用物品字典，已发布不可变字段自动禁用，避免运营误改在途订单语义。

### 20.7 微信消息推送回调

微信 MP 后台配置公网 HTTPS 地址：

```text
https://<正式域名>/api/wechat/message-push
```

该路径不使用玩家 token；GET 用于保存配置时的 URL 验证，POST 接收明文或安全模式 JSON/XML。其他业务路径仍按原鉴权规则执行。

官方 JSON 发货事件示例：

```json
{
  "CreateTime": 1583202606,
  "MsgType": "event",
  "Event": "minigame_deliver_goods",
  "MiniGame": {
    "OrderId": "r_123",
    "IsPreview": 0,
    "ToUserOpenid": "to_user_openid",
    "Zone": 1001,
    "GiftTypeId": 18,
    "GiftId": "gift_friend_001",
    "SendTime": 1583202600,
    "GoodsList": [
      {"Id": "id_100001", "Num": 1}
    ]
  }
}
```

发货成功或同一请求幂等重放：

```json
{"ErrCode":0,"ErrMsg":"Success"}
```

openid 尚未绑定本地玩家：

```json
{"ErrCode":1,"ErrMsg":"Unregistered user","SubErrCode":172935494}
```

格式错误、礼包不匹配、发奖失败等返回 `ErrCode=1`，让微信按平台策略处理或重试。非 `minigame_deliver_goods` 消息在完成验签后直接返回文本 `success`。

### 20.8 回调安全、事务与幂等

生产环境变量：

| 环境变量 | 说明 |
| --- | --- |
| `WECHAT_MESSAGE_PUSH_ENABLED` | 消息推送总开关，正式联调通过后设为 `true` |
| `WECHAT_MESSAGE_PUSH_TOKEN` | 与微信 MP 后台完全一致的消息 Token |
| `WECHAT_MESSAGE_PUSH_ENCODING_AES_KEY` | 微信后台 43 字符 EncodingAESKey，解码后 32 字节 |
| `WECHAT_MESSAGE_PUSH_MAX_BODY_BYTES` | 回调正文上限，默认 262144，代码硬上限 1MB |
| `WECHAT_MINI_GAME_APP_ID` | AES 明文尾部 AppId 校验使用的小游戏 AppID |

生产环境推荐微信 MP 选择安全模式。当前实现包含：

1. 明文请求按 `token/timestamp/nonce` 字典序计算 SHA-1 并校验 `signature`。
2. 安全模式按 `token/timestamp/nonce/Encrypt` 校验 `msg_signature`。
3. AES-256-CBC 使用 EncodingAESKey 前 16 字节作为 IV，采用微信 PKCS#7 32 字节块规则。
4. 解密后严格校验 `random + msg_len + msg + appid`，AppId 不一致拒绝处理；安全模式响应也重新加密和签名。
5. JSON/XML 都禁用外部实体和 DTD，正文先做字节上限检查，控制器错误不回显内部堆栈。
6. Token、EncodingAESKey、AppSecret 只放环境变量或密钥管理系统，不能写入管理端礼包配置、客户端包或 Git。

发货事务固定顺序：

```text
openid -> user_id
  -> SELECT users FOR UPDATE
  -> SELECT wechat_gift_delivery_record BY OrderId FOR UPDATE
  -> 校验 giftId / GiftTypeId / 完整 GoodsList
  -> 按固化 itemId 写 player_item
  -> 写 wechat_gift_delivery_record
  -> 提交
```

`order_id` 有全局唯一键。同一 `OrderId`、同一接收玩家、同一规范化请求重试时直接返回第一次成功；同一 `OrderId` 改传其他玩家或内容时返回幂等冲突，不重复发资源。背包增加和发货流水处于同一事务，任一步失败都会整体回滚。

### 20.9 上线与验收清单

1. 完整备份数据库，单节点启动并确认 `flyway_schema_history` 已执行 `V30__add_wechat_friend_gift_system.sql`。
2. 在微信 MP 后台创建类型 18 礼包和道具，记录真实 `giftId/wechatGoodsId/quantity`，确认有效期和每日领取上限。
3. 管理端创建 `WECHAT_FRIEND_GIFT / GIFT_PACKAGES` 草稿，先保持 `sendEnabled=false`，校验后发布。
4. 配置正式 HTTPS 回调地址、Token、EncodingAESKey 和 AppId，开启安全模式，先完成 GET URL 验证。
5. 使用已微信登录并绑定 `user_wechat_account` 的专用账号发送预览事件，核对背包只增加配置数量且生成一条 `wechat_gift_delivery_record`。
6. 重放同一 `OrderId`，确认仍返回成功但背包和流水都不增加；改动相同订单内容时必须失败。
7. 发布客户端最低基础库 3.11.2 判断；登录和等级变化后调用 `wx.setUserCloudStorage`，在开放数据域验证真实微信好友、等级降序和送礼状态。
8. 验证 `wx.sendGiftToFriend` 成功后接收方最终由服务端到账，客户端不直接加资源；未注册 openid 返回 `SubErrCode=172935494`。
9. 联调完成后在管理端只启用一个类型 18 礼包，观察回调失败率、6801/6802/6803 日志和发货流水。
10. 调整奖励时追加新礼包并保留旧映射至少覆盖全部 48 小时在途礼物；V30 迁移和已发布奖励映射禁止原地修改。

## 21. 运营公告与系统奖励投递（V31）

### 21.1 最终效果与权限边界

V31 完成后，管理端新增“公告中心”和“系统奖励”两个页面：

1. 运营可以创建全服或指定玩家公告，保存草稿、修改、立即发布、定时发布、撤销并查看不可变审计记录。
2. 玩家可以查询当前有效公告、全部有效公告的未读数量，并幂等标记单条公告已读。
3. 配置管理员可以在配置中心维护可复用的系统奖励模板，配置模板名称、启用状态、游戏物品和数量。
4. 配置管理员可以按单人、多个指定玩家或全服快照创建奖励活动；奖励通过现有玩家邮件发送和领取，不由管理端直接改背包。
5. 全服奖励按活动创建时的最大玩家 ID 冻结受众边界，新注册玩家不会加入已经创建的活动。
6. 奖励模板版本和物品主键在活动创建时冻结，后续修改或停用模板不会改变在途活动和历史邮件。
7. 多节点使用数据库租约领取活动，邮件使用稳定来源键幂等，节点崩溃、批次重跑和人工重试都不会重复发奖。

权限矩阵：

| 角色/身份 | 公告查看 | 公告草稿创建和修改 | 公告发布和撤销 | 奖励模板查看 | 奖励模板修改 | 奖励活动创建、撤销和重试 |
| --- | --- | --- | --- | --- | --- | --- |
| 玩家 token | 仅当前有效玩家公告 | 否 | 否 | 否 | 否 | 否 |
| `CONFIG_VIEWER` | 是 | 否 | 否 | 是 | 否 | 否 |
| `CONFIG_EDITOR` | 是 | 是 | 否 | 是 | 否 | 否 |
| `CONFIG_ADMIN` | 是 | 是 | 是 | 是 | 是 | 是 |

前端按钮权限只用于改善操作体验，最终权限始终由后端 `OperatorConfigPermissionService` 校验。玩家接口由玩家 token 鉴权，`/api/ops/**` 由独立运营 token 鉴权。

### 21.2 玩家公告接口

#### 查询当前有效公告

```http
GET /api/announcements?limit=20
Authorization: Bearer <player-token>
```

`limit` 允许 1 至 100，默认 30。成功响应：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "items": [
      {
        "id": 9,
        "announcementCode": "NOTICE-20260727-1",
        "title": "维护完成公告",
        "content": "服务器维护已经完成，感谢您的等待。",
        "priority": 100,
        "pinned": true,
        "startTime": "2026-07-27 20:00:00",
        "endTime": "2026-07-30 20:00:00",
        "read": false,
        "readTime": null
      }
    ],
    "unreadCount": 1
  }
}
```

查询规则：

1. 只返回 `startTime <= 当前北京时间 < endTime` 的有效公告；`endTime=null` 表示长期有效。
2. `ALL` 公告对所有玩家可见，`PLAYER_IDS` 公告只对创建草稿时保存的目标玩家可见。
3. 排序固定为 `pinned DESC, priority DESC, start_time DESC, id DESC`。
4. `items` 受 `limit` 限制，`unreadCount` 始终统计当前玩家全部有效未读公告，不只统计本页。
5. 查询同时校验状态和时间窗口，即使定时状态推进任务短暂延迟，也不会提前展示或继续展示过期公告。

#### 标记公告已读

```http
POST /api/announcements/{announcementId}/read
Authorization: Bearer <player-token>
```

响应返回标记后的完整玩家公告，`read=true` 且包含首次 `readTime`。数据库主键为 `(announcement_id, player_id)`，重复调用不会新增记录，也不会覆盖首次阅读时间。

只能标记当前玩家仍然可见且仍在有效期内的公告。公告不存在、受众不匹配、尚未开始、已过期或已撤销时统一返回业务码 `6901`。

### 21.3 管理端公告接口和状态机

接口清单：

| 方法 | 路径 | 最低权限 | 用途 |
| --- | --- | --- | --- |
| `POST` | `/api/ops/announcements` | `CONFIG_EDITOR` | 创建公告草稿；新版使用 `Idempotency-Key`，公告编号由服务端生成 |
| `PUT` | `/api/ops/announcements/{id}` | `CONFIG_EDITOR` | 按 `expectedVersion` 修改草稿 |
| `POST` | `/api/ops/announcements/{id}/publish` | `CONFIG_ADMIN` | 立即发布或进入定时发布 |
| `POST` | `/api/ops/announcements/{id}/cancel` | `CONFIG_ADMIN` | 撤销草稿、待发布或线上公告 |
| `GET` | `/api/ops/announcements` | `CONFIG_VIEWER` | 按状态和主键游标分页 |
| `GET` | `/api/ops/announcements/{id}` | `CONFIG_VIEWER` | 查询目标玩家和完整审计 |

创建全服公告示例：

```http
POST /api/ops/announcements
Authorization: Bearer <operator-token>
Idempotency-Key: announcement:550e8400-e29b-41d4-a716-446655440000
Content-Type: application/json
```

```json
{
  "title": "维护公告",
  "content": "服务器将在今晚维护。",
  "priority": 100,
  "pinned": true,
  "audienceType": "ALL",
  "playerIds": [],
  "startTime": "2026-07-28 20:00:00",
  "endTime": "2026-07-29 02:00:00",
  "reason": "例行维护"
}
```

指定玩家公告把 `audienceType` 改为 `PLAYER_IDS`，并传入最多 1000 个正整数玩家 ID。服务端会排序、去重并验证玩家存在；全服公告禁止同时填写目标玩家。

新版调用方必须为一次创建操作生成 `Idempotency-Key`，网络超时或提交结果未知时使用原请求头和原正文重试。服务端生成 `ANN-yyyyMMdd-随机串` 格式的 `announcementCode` 作为展示、检索和运维排障编号；该编号不再承担幂等职责。创建响应中的 `data.replayed` 表示是否为幂等重放：相同请求头和相同规范化内容返回第一次创建的公告及原 `announcementCode`；相同请求头改传其他内容时返回 `6903`，不会覆盖原公告。

兼容旧调用方时，可不传请求头并继续在正文传 `announcementCode`，服务端会暂时把该旧字段同时作为业务编号和幂等键。`announcementCode` 已废弃，新调用方不得同时提交 `Idempotency-Key` 和正文 `announcementCode`；两者同时存在或两者都缺失都会拒绝创建。幂等键最长 128 字符，只允许字母、数字、点、下划线、冒号和短横线。

修改、发布和撤销都必须携带当前版本：

```json
{
  "expectedVersion": 2,
  "reason": "审核通过"
}
```

公告状态机：

```text
DRAFT
  -> SCHEDULED   未来开始时间发布
  -> PUBLISHED   已到开始时间时立即发布
  -> CANCELLED   草稿撤销

SCHEDULED
  -> PUBLISHED   到达开始时间
  -> EXPIRED     到达结束时间且尚未生效
  -> CANCELLED   运营撤销

PUBLISHED
  -> EXPIRED     到达结束时间
  -> CANCELLED   运营撤销
```

只有 `DRAFT` 可以修改或发布。修改、发布和撤销均以 `version` 乐观锁防止两个运营互相覆盖；状态或版本变化返回 `6902`。定时任务每 5 秒推进一次状态，查询接口仍以北京时间窗口作为最终兜底。

审计操作固定为 `CREATE / UPDATE / PUBLISH / CANCEL`。每条审计保存运营账号快照、原因、操作时间和操作后完整公告 JSON，不允许更新或删除。

### 21.4 管理端系统奖励模板配置

奖励模板接入通用配置中心：

```text
moduleCode = SYSTEM_REWARD
configKey  = PACKAGES
```

完整配置快照示例：

```json
{
  "rewardPackages": [
    {
      "id": null,
      "packageCode": "MAINTENANCE_REWARD",
      "packageName": "维护补偿礼包",
      "description": "维护结束后使用",
      "enabled": true,
      "configVersion": null,
      "items": [
        {
          "itemId": 1,
          "itemKey": "gold",
          "quantity": 100,
          "sortOrder": 1
        }
      ]
    }
  ]
}
```

管理规则：

1. 该模块属于高风险经济配置，只有 `CONFIG_ADMIN` 可以保存草稿、丢弃草稿和发布；其他配置角色只读。
2. 最多 500 个模板，每个模板 1 至 20 项物品；单项数量为 1 至 1,000,000,000。
3. `packageCode` 必须以大写字母开头，只能包含大写字母、数字和下划线，发布后禁止修改。
4. 物品必须来自已启用的 `item_config`，`itemId` 与 `itemKey` 必须完全匹配，同一模板不能重复物品。
5. `sortOrder` 必须从 1 连续递增；管理端删除物品后会自动重新排序，服务端发布时再次校验。
6. 已发布模板不能物理删除，只能设置 `enabled=false`；只有启用模板会出现在新建奖励活动页面。
7. 修改模板名称、说明、启用状态或物品时，使用 `configVersion` 乐观锁并自动递增版本。
8. 活动创建会复制 `packageCode/packageName/configVersion/itemId/itemKey/quantity/sortOrder`。模板后续修改只影响新活动，不改变已创建活动。

V31 会创建默认关闭的 `SYSTEM_COMPENSATION_BASIC` 基础补偿礼包，默认包含 100 个 `gold`。必须由配置管理员确认经济数值后再启用，禁止把迁移默认值视为正式运营方案。

### 21.5 系统奖励活动接口

接口清单：

| 方法 | 路径 | 最低权限 | 用途 |
| --- | --- | --- | --- |
| `GET` | `/api/ops/system-rewards/packages` | `CONFIG_VIEWER` | 查询当前已启用奖励模板 |
| `POST` | `/api/ops/system-rewards/campaigns` | `CONFIG_ADMIN` | 创建立即或定时奖励活动；新版使用 `Idempotency-Key`，投递单号由服务端生成 |
| `GET` | `/api/ops/system-rewards/campaigns` | `CONFIG_VIEWER` | 按状态和主键游标分页 |
| `GET` | `/api/ops/system-rewards/campaigns/{id}` | `CONFIG_VIEWER` | 查询冻结奖励、受众和审计 |
| `POST` | `/api/ops/system-rewards/campaigns/{id}/cancel` | `CONFIG_ADMIN` | 撤销或请求批次边界停止 |
| `POST` | `/api/ops/system-rewards/campaigns/{id}/retry` | `CONFIG_ADMIN` | 从失败活动原游标继续投递 |

创建全服奖励示例：

```http
POST /api/ops/system-rewards/campaigns
Authorization: Bearer <operator-token>
Idempotency-Key: reward:550e8400-e29b-41d4-a716-446655440000
Content-Type: application/json
```

```json
{
  "packageCode": "MAINTENANCE_REWARD",
  "title": "维护补偿",
  "content": "感谢您的耐心等待，请领取本次维护补偿。",
  "senderName": "系统",
  "targetType": "ALL",
  "playerIds": [],
  "scheduledTime": "2026-07-27 22:00:00",
  "mailExpireTime": "2026-08-03 22:00:00",
  "reason": "2026-07-27 维护补偿"
}
```

指定玩家奖励把 `targetType` 改为 `PLAYER_IDS` 并填写玩家 ID。配置默认最多 1000 人，代码硬上限 10000 人；超过限制应拆分为不同工单，不能提高到无界数组。

创建规则：

1. 新版调用方使用 `Idempotency-Key` 作为创建幂等键；允许字母、数字、点、下划线、冒号和短横线，最长 128。提交结果未知时必须沿用原请求头和原正文重试。
2. 服务端生成 `REWARD-yyyyMMdd-随机串` 格式的 `requestNo`，仅用于展示、检索和运维排障。同一幂等键和同一规范化请求重试时返回原活动及原单号，`data.replayed=true`；内容不同返回 `7004`。
3. 创建时锁定模板主记录和物品记录，确保模板版本与物品快照来自同一个已提交版本。
4. `ALL` 在创建事务中冻结当时最大的 `users.id` 和目标人数；投递只扫描 `id <= targetMaxPlayerId`。
5. `PLAYER_IDS` 在创建事务中排序、去重、验证存在并写入不可变目标表。
6. 已过去的 `scheduledTime` 会归一为数据库当前北京时间；`mailExpireTime` 如非空，必须晚于实际发送时间。
7. 活动创建、物品快照、目标快照和 `CREATE` 审计处于同一事务，任一步失败都会整体回滚。

创建成功响应核心字段：

```json
{
  "code": 200,
  "message": "系统奖励活动创建成功",
  "data": {
    "campaign": {
      "id": 44,
      "requestNo": "REWARD-20260727-5E9F4A8D90B54BAA82D249ACF1478E31",
      "packageCode": "MAINTENANCE_REWARD",
      "packageName": "维护补偿礼包",
      "packageVersion": 3,
      "targetType": "ALL",
      "targetMaxPlayerId": 90001,
      "targetCount": 86000,
      "processedCount": 0,
      "cursorPlayerId": 0,
      "status": "SCHEDULED",
      "scheduledTime": "2026-07-27 22:00:00",
      "cancelRequested": false,
      "attempts": 0,
      "lastError": null
    },
    "replayed": false
  }
}
```

列表使用 `beforeId` 主键游标，`limit` 为 1 至 100，默认 30。可用状态筛选为 `SCHEDULED / PROCESSING / COMPLETED / FAILED / CANCELLED`。

管理端页面将上述状态、受众类型、奖励类型和审计动作统一翻译为中文（例如“已完成”“全服快照”“物品”“创建投递”）。`packageCode` 等稳定配置编码和服务端生成的 `requestNo` 仍由接口原样返回；`requestNo` 用于工单检索和故障排查，创建幂等由不展示的 `Idempotency-Key` 承担。页面主视图默认显示中文名称，悬停提示保留内部编码，不改变数据库值。

兼容旧调用方时，可不传请求头并继续在正文传 `requestNo`，服务端会暂时把该旧字段同时作为业务单号和幂等键。正文 `requestNo` 已废弃，新调用方不得同时提交 `Idempotency-Key` 和正文 `requestNo`；两者同时存在或两者都缺失都会拒绝创建。

### 21.6 投递、租约、重试和撤销语义

奖励活动状态机：

```text
SCHEDULED
  -> PROCESSING  到达计划时间并取得数据库租约
  -> CANCELLED   未开始时由运营撤销

PROCESSING
  -> PROCESSING  一批成功后释放租约并等待下一批，或失败后指数退避
  -> COMPLETED   全部目标玩家的邮件已幂等落库
  -> FAILED      连续失败达到最大次数
  -> CANCELLED   收到撤销请求后在批次边界停止

FAILED
  -> PROCESSING  管理员人工重试，从原游标继续
  -> CANCELLED   管理员撤销
```

投递实现：

1. 调度器只在短事务中使用 `SELECT ... FOR UPDATE` 领取一个到期活动，写入唯一 `leaseOwner` 和 `leaseUntil` 后立即提交。
2. 默认每批读取 100 个玩家 ID，按主键升序发送；每个玩家邮件调用拥有独立事务，单个坏玩家不会回滚已经提交的其他玩家邮件。
3. 邮件来源固定为：

```text
sourceType = SYSTEM_REWARD
sourceId   = CAMPAIGN:<campaignId>
```

4. 现有 `player_mail` 唯一键 `UNIQUE(player_id, source_type, source_id)` 保证同一活动对同一玩家最多一封邮件。
5. 命中重复来源时会比较标题、正文、发件人、过期时间和全部冻结附件；完全一致时返回第一次邮件 ID，内容不同返回 `7004`。
6. 只有整批邮件都成功后才推进 `cursorPlayerId` 和 `processedCount`。中途失败时游标不动，重试会重放整批，但已经创建的邮件会幂等复用。
7. 租约过期导致其他节点接管时，旧节点即使继续执行也只能产生幂等邮件；游标更新还必须匹配当前租约拥有者和预期旧游标。
8. 连续失败采用指数退避，默认最多 8 次，间隔从 10 秒增长到最多 600 秒；任一批成功后连续失败计数清零。

撤销不是“追回奖励”：

- `SCHEDULED` 或 `FAILED` 可以直接进入 `CANCELLED`。
- `PROCESSING` 只设置 `cancelRequested=true`，当前批次完成后停止；节点崩溃时由过期租约收口任务完成撤销。
- 已经生成的邮件和已经领取的附件不会删除或扣回，因此页面会保留 `processedCount`，用于说明撤销前实际覆盖人数。
- `COMPLETED` 活动不能撤销。需要修正运营误发时必须走新的审计工单和独立补偿方案，不能直接改历史活动或邮件附件。

失败人工重试使用原活动、原模板快照、原受众和原游标，不允许运营在重试时更换奖励内容。`CANCEL_REQUEST` 和 `RETRY` 都写入不可变审计记录。

### 21.7 历史附件和领取安全

系统奖励活动按 `itemId` 写入 `player_mail_attachment`。领取时也按冻结 `itemId` 调用背包服务，不重新按 `itemKey` 查找当前启用配置。因此：

1. 奖励活动创建后，即使模板被停用，已经创建的活动仍能继续发送冻结奖励。
2. 邮件创建后，即使对应 `item_config.enabled` 后来变为 `false`，历史邮件仍可按原物品主键领取。
3. `item_config` 仍通过外键限制物理删除，防止历史附件变成孤儿记录。
4. 邮件领取锁定 `player_mail`，背包增加和 `attachment_claimed=true` 在同一事务中提交；并发领取最多成功一次。

### 21.8 V31 数据表和约束

V31 新增 10 张表：

| 表 | 用途 | 关键约束 |
| --- | --- | --- |
| `game_announcement` | 公告正文、受众类型和生命周期 | `announcement_code` 与 `idempotency_key` 分别唯一、状态/时间窗/版本检查 |
| `game_announcement_target` | 指定玩家公告受众 | `(announcement_id, player_id)` 主键 |
| `player_announcement_read` | 玩家公告首次已读时间 | `(announcement_id, player_id)` 主键 |
| `operator_announcement_audit_log` | 公告操作审计 | 只追加，操作限 `CREATE/UPDATE/PUBLISH/CANCEL` |
| `system_reward_package` | 奖励模板主记录 | `package_code` 唯一、`config_version > 0` |
| `system_reward_package_item` | 模板物品 | 同模板物品和排序唯一、数量大于 0 |
| `operator_reward_campaign` | 奖励/邮件活动、游标、租约和状态 | `request_no` 与 `idempotency_key` 分别唯一、目标/计数/租约成对检查 |
| `operator_reward_campaign_item` | 活动不可变物品快照 | 同活动物品和排序唯一 |
| `operator_reward_campaign_target` | 指定玩家活动受众快照 | `(campaign_id, player_id)` 主键 |
| `operator_reward_campaign_audit_log` | 奖励活动审计 | 只追加，操作限 `CREATE/CANCEL_REQUEST/RETRY` |

全服奖励不复制全服目标行，只保存 `targetMaxPlayerId + targetCount`；指定玩家奖励才写 `operator_reward_campaign_target`。公告也不为全服玩家复制公告或已读初始行，只有玩家实际阅读后才写一条已读记录。

V94 为 `game_announcement` 和 `operator_reward_campaign` 增加独立 `idempotency_key`，并用历史 `announcement_code/request_no` 回填旧数据；历史重放语义保持不变。新记录由服务端生成展示业务编号，并按独立幂等键去重，禁止把业务编号重新用于新版创建请求的主动重试。

`operator_reward_campaign` 的数据库检查约束确保：

- `ALL` 必须有 `target_max_player_id`，`PLAYER_IDS` 必须没有该字段。
- `target_count > 0` 且 `processed_count <= target_count`。
- `lease_owner` 与 `lease_until` 必须同时为空或同时非空。
- 邮件过期时间必须晚于计划发送时间。

### 21.9 运行配置和监控

生产环境可配置：

| 环境变量 | 默认值 | 说明 |
| --- | ---: | --- |
| `ANNOUNCEMENT_INITIAL_DELAY_MS` | 5000 | 公告生命周期任务首次延迟 |
| `ANNOUNCEMENT_FIXED_DELAY_MS` | 5000 | 公告生命周期任务固定间隔 |
| `SYSTEM_REWARD_DISPATCH_ENABLED` | `true` | 系统奖励调度总开关 |
| `SYSTEM_REWARD_CLAIM_BATCH_SIZE` | 1 | 每轮最多领取并处理的活动数，代码硬上限 20 |
| `SYSTEM_REWARD_DELIVERY_BATCH_SIZE` | 100 | 单活动单批玩家数，代码硬上限 500 |
| `SYSTEM_REWARD_LEASE_SECONDS` | 120 | 活动租约秒数，代码最小值 10 |
| `SYSTEM_REWARD_MAX_ATTEMPTS` | 8 | 单批最大连续失败次数 |
| `SYSTEM_REWARD_RETRY_BASE_SECONDS` | 10 | 指数退避基础秒数 |
| `SYSTEM_REWARD_RETRY_MAX_SECONDS` | 600 | 指数退避最大秒数 |
| `SYSTEM_REWARD_MAX_TARGETED_PLAYERS` | 1000 | 单次指定玩家上限，代码硬上限 10000 |

建议监控：

1. `PROCESSING` 且 `lease_until` 长时间早于当前时间的活动数量。
2. `FAILED` 活动数量、`last_error` 分布和连续失败次数。
3. `SCHEDULED` 且 `scheduled_time` 已过去较长时间的积压数量。
4. `processed_count / target_count` 长时间不变化的活动。
5. `player_mail` 中 `SYSTEM_REWARD` 来源的创建速率和唯一键冲突率；幂等重放可以出现，内容冲突必须告警。

应用、数据库和 JSON 统一使用北京时间业务口径：数据库通过 `CONVERT_TZ(UTC_TIMESTAMP(6), '+00:00', '+08:00')` 获取权威业务时间，JVM 和 Jackson 时区均为 `Asia/Shanghai`，运营时间格式固定为 `yyyy-MM-dd HH:mm:ss`。

### 21.10 新增业务错误码

| code | message | 典型场景 |
| --- | --- | --- |
| 6901 | 公告不存在 | 玩家不可见、公告失效或管理端 ID 不存在 |
| 6902 | 公告状态或版本已变化 | 草稿被其他运营修改、发布或撤销 |
| 6903 | 公告创建请求与首次内容不一致 | 同一 `Idempotency-Key` 重放不同创建请求；旧调用方仍按 `announcementCode` 判断 |
| 7001 | 系统奖励模板不可用 | 模板不存在、未启用、无物品或冻结物品失效 |
| 7002 | 系统奖励活动不存在 | 管理端活动 ID 不存在 |
| 7003 | 系统奖励活动状态已变化 | 非法撤销/重试或租约、游标条件更新未命中 |
| 7004 | 系统奖励创建请求与首次内容不一致 | 同一 `Idempotency-Key` 或邮件来源重放不同内容；旧调用方仍按 `requestNo` 判断 |

参数格式、长度和数量错误仍返回通用参数错误；权限不足返回 403。接口不向客户端回显数据库 SQL、堆栈或内部租约实现细节。

### 21.11 上线、停用和恢复清单

1. 完整备份数据库，在维护窗口停止管理端写入和系统奖励调度，只保留一个应用实例执行 Flyway。
2. 确认 V1-V30 均成功后执行 `V31__add_operator_announcement_and_system_reward.sql`，检查 `flyway_schema_history` 已到 V31。
3. 检查 10 张新表、`uk_game_announcement_code`、`uk_operator_reward_campaign_request` 和活动租约检查约束存在。
4. 首次启动建议临时设置 `SYSTEM_REWARD_DISPATCH_ENABLED=false`，先验证管理端页面、权限、模板草稿和发布审计。
5. 确认默认 `SYSTEM_COMPENSATION_BASIC` 为关闭状态；根据正式经济方案修改数量、发布并显式启用。
6. 使用测试玩家创建一条指定玩家公告，验证定时显示、未读数、重复已读和撤销后不可见。
7. 使用专用测试玩家创建一条指定玩家奖励，确认只生成一封 `SYSTEM_REWARD / CAMPAIGN:<id>` 邮件，领取后背包只增加一次。
8. 手动重跑同一活动批次，确认邮件和附件数量不增加；使用同一 `Idempotency-Key` 重复创建应返回原活动及原 `requestNo`。
9. 开启 `SYSTEM_REWARD_DISPATCH_ENABLED=true`，逐步发送小受众活动，再开放全服活动；观察积压、失败、租约和邮件速率。
10. 全服奖励发送前必须二次确认冻结人数、模板版本、邮件有效期和工单号。创建后不能修改受众或奖励内容，只能在批次边界撤销未发送部分。

紧急停用不删除 V31 表，也不修改已经执行的迁移文件：先设置 `SYSTEM_REWARD_DISPATCH_ENABLED=false` 并滚动重启节点，再在管理端撤销未开始活动。旧版本应用可以忽略新表，但已经发送的邮件和已领取资源不能通过回滚应用版本自动追回。

### 21.12 本次验证结果

2026-07-27 已执行：

```powershell
mvn.cmd -q "-Dproject.build.directory=target-codex" test

$env:RUN_MYSQL_INTEGRATION_TESTS='true'
mvn.cmd -q "-Dproject.build.directory=target-codex" "-Dtest=FlywayMigrationIntegrationTest,OperatorConfigPersistenceIntegrationTest,BusinessConcurrencyIntegrationTest" test

npm.cmd --prefix admin-web run build

git diff --check
```

验证范围包括：

1. 后端全量单元和控制器契约测试。
2. MySQL 8.0 上 V1-V31 完整 Flyway 迁移、V31 表/索引/约束和默认关闭奖励模板。
3. 系统奖励模板配置中心持久化、版本递增、管理员专属写权限和不可删除规则。
4. 6 路并发使用同一 `Idempotency-Key` 创建活动只落一条活动记录并返回同一活动 ID 和原 `requestNo`。
5. 强制重跑同一奖励批次后仍只有两封玩家邮件和两条附件，活动最终为 `COMPLETED`。
6. 冻结 `itemId` 的历史邮件在物品后来停用后仍可领取，领取事务不会重复增加背包。
7. 公告玩家和运营路由、请求校验、版本参数、未读与幂等已读契约。
8. 管理端 Vue TypeScript 检查和 Vite 生产构建，公告、系统奖励、配置中心路由与权限按钮均可编译。

最终命令均应以退出码 0 为上线门槛；任何 MySQL 集成测试或管理端构建失败都必须先修复，禁止只跳过失败测试后发布。

## 22. 普通无附件系统邮件（V32-V33）

### 22.1 最终效果

管理端新增“系统邮件”页面，运营可以创建普通无附件邮件并发送给全服快照或指定玩家。该能力使用运营账号 token 和现有 `CONFIG_*` 角色体系，不向浏览器暴露 `/api/mail/admin/**` 的内部后台密钥。

普通系统邮件具备以下完整能力：

1. 支持立即发送和定时发送，邮件有效期可以为空。
2. 支持 `ALL` 全服快照和 `PLAYER_IDS` 指定玩家。
3. 支持状态筛选、主键游标分页、活动详情和不可变操作审计。
4. 支持发送前撤销、发送中批次边界撤销、失败后从原游标重试。
5. 支持稳定 `Idempotency-Key` 创建幂等，同一请求重复提交返回原活动及服务端生成的原 `requestNo`。
6. 每个玩家独立事务投递，批次重放不会重复创建邮件。
7. 玩家收到 `MailType.SYSTEM` 邮件，`hasAttachment=false`，附件列表为空，不出现领取按钮。
8. 普通邮件不会读取奖励模板，也不会写入 `player_mail_attachment`。

“系统邮件”和“系统奖励”在管理端是两个独立入口；底层只复用可靠投递骨架。奖励邮件仍必须冻结已发布奖励模板并至少包含一个附件，普通邮件不能包含任何附件，二者不会因为复用调度器而混淆业务语义。

### 22.2 管理端接口

接口清单：

| 方法 | 路径 | 最低权限 | 用途 |
| --- | --- | --- | --- |
| `POST` | `/api/ops/system-mails/campaigns` | `CONFIG_ADMIN` | 创建立即或定时普通系统邮件；新版使用 `Idempotency-Key`，投递单号由服务端生成 |
| `GET` | `/api/ops/system-mails/campaigns` | `CONFIG_VIEWER` | 按状态和主键游标分页 |
| `GET` | `/api/ops/system-mails/campaigns/{id}` | `CONFIG_VIEWER` | 查询正文、受众和审计 |
| `POST` | `/api/ops/system-mails/campaigns/{id}/cancel` | `CONFIG_ADMIN` | 撤销或请求批次边界停止 |
| `POST` | `/api/ops/system-mails/campaigns/{id}/retry` | `CONFIG_ADMIN` | 从失败活动原游标继续发送 |

`CONFIG_EDITOR` 可以查看但不能发送；只有 `CONFIG_ADMIN` 可以创建、撤销或重试。后端始终再次校验权限，不能把管理端按钮是否显示当作安全边界。

创建全服普通邮件示例：

```http
POST /api/ops/system-mails/campaigns
Authorization: Bearer <operator-token>
Idempotency-Key: mail:550e8400-e29b-41d4-a716-446655440000
Content-Type: application/json
```

```json
{
  "title": "维护通知",
  "content": "服务器将于今晚 22:00 进行例行维护。",
  "senderName": "系统",
  "targetType": "ALL",
  "playerIds": [],
  "scheduledTime": "2026-07-27 21:30:00",
  "mailExpireTime": null,
  "reason": "2026-07-27 例行维护通知"
}
```

指定玩家发送时：

```json
{
  "title": "账号处理结果",
  "content": "您的工单已经处理完成。",
  "senderName": "客服中心",
  "targetType": "PLAYER_IDS",
  "playerIds": [10001, 10002],
  "scheduledTime": "2026-07-27 14:30:00",
  "mailExpireTime": "2026-08-27 14:30:00",
  "reason": "客服工单 TICKET-20260727-002"
}
```

创建成功响应核心字段：

```json
{
  "code": 200,
  "message": "系统邮件活动创建成功",
  "data": {
    "campaign": {
      "id": 51,
      "requestNo": "MAIL-20260727-4BE2E81E59DC46D6A5F720A63A77CF58",
      "title": "维护通知",
      "content": "服务器将于今晚 22:00 进行例行维护。",
      "senderName": "系统",
      "targetType": "ALL",
      "targetMaxPlayerId": 90001,
      "targetCount": 86000,
      "processedCount": 0,
      "cursorPlayerId": 0,
      "status": "SCHEDULED",
      "scheduledTime": "2026-07-27 21:30:00",
      "mailExpireTime": null,
      "cancelRequested": false,
      "attempts": 0,
      "lastError": null
    },
    "replayed": false
  }
}
```

列表参数与奖励活动一致：`beforeId` 为可选主键游标，`limit` 为 1 至 100且默认 30，`status` 可选值为 `SCHEDULED / PROCESSING / COMPLETED / FAILED / CANCELLED`。

### 22.3 创建幂等与受众冻结

创建事务按以下顺序执行：

```text
规范化请求并生成 SHA-256 摘要
  -> 快速查询已有 Idempotency-Key
  -> SELECT 创建锁分片 FOR UPDATE
  -> 当前读再次查询 Idempotency-Key
  -> 验证并冻结受众
  -> 写活动和指定玩家目标快照
  -> 写 CREATE 不可变审计
  -> 提交事务并释放分片锁
```

规则如下：

1. 新版调用方使用 `Idempotency-Key`，最长 128，只允许字母、数字、点、下划线、冒号和短横线；提交结果未知时必须沿用原请求头和原正文重试。
2. 服务端生成 `MAIL-yyyyMMdd-随机串` 格式的 `requestNo`，仅用于展示、检索和运维排障；数据库仍保证业务单号全局唯一。
3. 相同幂等键和相同规范化内容重试返回原活动、原 `requestNo` 并设置 `replayed=true`。
4. 相同幂等键但标题、正文、受众、时间或原因不同，返回 `7103`，不会覆盖第一次活动。
5. `ALL` 在创建事务中冻结当时最大的 `users.id` 和目标人数；之后注册的新玩家不属于该活动。
6. `PLAYER_IDS` 会排序、去重、校验全部玩家存在，再写入不可变目标快照。
7. 已过去的计划时间按数据库当前北京时间归一；有效期如非空，必须晚于实际计划时间。

V33 创建 64 行固定分片锁。新版请求按 `Idempotency-Key` 稳定落到同一行，并以 `SELECT ... FOR UPDATE` 在所有服务节点间串行创建判定。分片锁随数据库事务自动释放，不依赖单机 `synchronized`、Redis 可用性或连接级命名锁；不同请求只在哈希碰撞时短暂串行。

兼容旧调用方时，可不传请求头并继续在正文传 `requestNo`，服务端会暂时把该旧字段同时作为业务单号和幂等键。正文 `requestNo` 已废弃，新调用方不得同时提交 `Idempotency-Key` 和正文 `requestNo`；两者同时存在或两者都缺失都会拒绝创建。

上述公告、系统邮件和系统奖励均为运营管理接口，Unity 玩家客户端不调用这些创建接口。本次业务编号自动生成和幂等请求头改造不要求 Unity 修改、重新打包或提审。

### 22.4 投递、幂等和玩家表现

普通系统邮件投递固定使用：

```text
mailType   = SYSTEM
sourceType = OPERATOR_MAIL
sourceId   = CAMPAIGN:<campaignId>
attachments = []
```

投递规则：

1. 普通邮件和奖励邮件由同一个多节点数据库租约调度器领取，但根据 `campaign_type` 选择不同邮件类型和附件规则。
2. 普通邮件活动如果意外存在冻结物品，投递会失败并进入重试/告警，不会把它当作奖励邮件发送。
3. 奖励邮件如果没有附件，可信邮件服务直接拒绝；放宽普通邮件附件数量不会削弱奖励安全约束。
4. `UNIQUE(player_id, source_type, source_id)` 保证同一活动对同一玩家最多一封邮件。
5. 命中重复来源时会核对邮件类型、标题、正文、发件人、有效期和完整附件列表；完全一致返回第一次邮件 ID，不一致返回 `7104`。
6. 一批玩家全部投递成功后才推进游标。任一玩家失败时整批游标不动，已成功玩家在重试时通过来源唯一键幂等复用。
7. 玩家邮件列表显示 `hasAttachment=false`；详情返回 `attachments=[]`。无附件邮件不允许领取，但可以正常标记已读和删除。
8. 撤销只停止尚未投递的玩家，不删除已经生成的邮件；`COMPLETED` 活动不能撤销。

共享调度器暂时继续使用 `SYSTEM_REWARD_*` 环境变量名称，以保持现有部署配置兼容。该开关现在同时控制普通邮件和奖励邮件；紧急停用时二者都会停止领取新批次。

### 22.5 管理端页面

管理端地址：

```text
http://localhost:5173/system-mails
```

页面支持：

1. 自动生成可编辑的 `MAIL-<时间>-<后缀>` 请求号。
2. 全服/指定玩家分段选择，指定玩家 ID 自动排序和去重。
3. 标题、正文、发件人、计划时间、可选过期时间和发送原因。
4. 提交前二次确认目标范围和邮件标题。
5. 状态筛选、发送进度、详情抽屉、完整正文、冻结受众和审计时间线。
6. 未开始/发送中/失败活动撤销，失败活动人工重试。

“系统邮件”和“系统奖励”页面复用 `OperatorMailCampaignConsole.vue`。奖励模式额外显示模板选择和冻结物品，普通邮件模式不渲染模板或附件控件，避免两套高度相似页面长期漂移。

### 22.6 V32 与 V33 数据变更

V32 对现有 `operator_reward_campaign` 做兼容扩展：

1. 新增 `campaign_type`，已有记录和奖励活动固定为 `REWARD`，普通邮件为 `SYSTEM_MAIL`。
2. 奖励模板快照列调整为可空，但数据库 `chk_operator_reward_campaign_payload` 强制：
   - `REWARD` 必须有完整模板编码、名称和正版本号；
   - `SYSTEM_MAIL` 三个模板字段必须全部为空。
3. 新增 `(campaign_type, status, id)` 管理端分页索引。
4. 原活动、目标、审计和租约字段继续复用，不迁移或复制 V31 历史数据。

V33 新增：

| 表 | 用途 | 关键约束 |
| --- | --- | --- |
| `operator_mail_campaign_lock_shard` | 多节点创建幂等分片锁 | 64 个固定主键，`shard_id < 64` |

V32 已在本地库执行后保持原文件不变；并发锁表通过新的 V33 发布。禁止为了消除 Flyway checksum 差异修改已应用迁移或执行无依据的 `flyway repair`。

### 22.7 新增错误码

| code | message | 典型场景 |
| --- | --- | --- |
| 7101 | 系统邮件活动不存在 | ID 不存在，或用普通邮件接口访问奖励活动 |
| 7102 | 系统邮件活动状态已变化 | 非法撤销/重试，或并发状态条件更新未命中 |
| 7103 | 系统邮件创建请求与首次内容不一致 | 同一 `Idempotency-Key` 重放不同普通邮件请求；旧调用方仍按 `requestNo` 判断 |
| 7104 | 邮件业务来源与首次投递内容不一致 | 同一玩家和来源键出现不同邮件正文或附件 |

参数格式和目标数量错误返回 400，权限不足返回 403，未登录运营接口返回 401。内部 `/api/mail/admin/**` 仍保留给受信后台工具，不能在管理端页面使用或暴露其 `X-Notification-Admin-Key`。

### 22.8 上线和恢复清单

1. 备份数据库，确认 V31 已成功，再由单节点顺序执行 V32、V33。
2. 检查 `flyway_schema_history` 中 32、33 均为成功，禁止修改二者 checksum。
3. 检查 `campaign_type`、`chk_operator_reward_campaign_payload` 和分页索引存在。
4. 检查 `operator_mail_campaign_lock_shard` 恰好 64 行，缺行时禁止开放创建接口。
5. 使用配置管理员创建一条未来时间的指定玩家普通邮件，确认活动详情没有模板和附件。
6. 撤销测试活动，确认写入 `CANCEL_REQUEST` 审计。
7. 创建一条立即发送邮件，确认玩家只收到一封 `OPERATOR_MAIL / CAMPAIGN:<id>` 邮件且附件数为 0。
8. 重放同一批次和同一创建请求，确认活动、玩家邮件和审计不会重复。
9. 先小范围验证，再开放全服发送；全服操作必须二次确认目标人数、正文、时间和工单号。
10. 紧急停用设置 `SYSTEM_REWARD_DISPATCH_ENABLED=false` 并滚动重启；不要删除活动、邮件或迁移表。

### 22.9 本次验证结果

2026-07-27 已完成：

```powershell
mvn.cmd -q "-Dmaven.resources.skip=true" test

$env:RUN_MYSQL_INTEGRATION_TESTS='true'
mvn.cmd -q "-Dmaven.resources.skip=true" "-Dtest=FlywayMigrationIntegrationTest,BusinessConcurrencyIntegrationTest" test

npm.cmd --prefix admin-web run build

git diff --check
```

验证结果：

1. Surefire 汇总 397 项测试，0 失败、0 错误，11 项外部条件测试跳过。
2. MySQL 8.0 从空库完整执行 V1-V33，并从 V16 旧库连续升级到 V33；重复 migrate 为 0 项。
3. 主开发库保持 V32 checksum 不变并独立应用 V33，服务健康状态为 `UP`。
4. 6 路并发使用同一普通邮件 `Idempotency-Key` 只创建一条活动，并返回同一活动 ID 和原 `requestNo`。
5. 普通邮件批次强制重放后仍只有两封玩家邮件、零条附件，活动最终为 `COMPLETED`。
6. 奖励邮件并发创建和批次重放继续通过，仍要求冻结附件且不会退化为普通邮件。
7. 管理端 Vue TypeScript 检查和 Vite 生产构建通过；`/system-mails` 路由返回 200。
8. 后端 `http://localhost:8080/api/health` 返回 `UP`；未登录访问 `/api/ops/system-mails/campaigns` 返回 401，证明路由存在且受运营鉴权保护。

本次测试使用 `maven.resources.skip=true` 是因为本地运行实例已提前完成资源复制，验证阶段避免 Windows 对 `target/classes` 中运行态资源的覆盖冲突；正式 CI 或停服构建应直接执行标准 `mvn.cmd -q test`。

---

## 23. 游戏好友每日赠礼（V34）

### 23.1 功能语义

游戏内好友每日赠礼采用“系统赞助”模型：

1. 发送者不会扣除自己的背包资源。
2. 系统直接向接收者背包发放管理端当前配置的物品和数量，默认配置为 `gold × 100`。
3. A 向 B、B 向 A 是两个独立方向，每个方向在北京时间自然日内最多成功一次。
4. 除好友间单方向每日一次外，还同时限制每名玩家每日发送总次数和接收总次数。
5. 重复点击、客户端超时重试或多节点并发请求返回第一次发放的完整结果，不会再次增加余额。
6. 每次成功发放固化物品 ID、物品编码、数量、配置版本和发放后余额，后续运营改配置不影响历史流水。
7. V34 默认配置为停用，配置管理员审核经济参数并发布启用后，玩家接口才允许发放。

该功能属于游戏内 `player_friend` 好友关系，不读取微信好友关系，也不替代微信官方 `wechatgift` 礼包回调。

### 23.2 玩家接口

所有接口均要求玩家登录令牌。

#### 23.2.1 查询好友与当日赠礼状态

```http
GET /api/friends?limit=20&cursorLevel=12&cursorPlayerId=10008
Authorization: Bearer <player-token>
```

首次请求不传 `cursorLevel` 和 `cursorPlayerId`；后续请求必须同时传入上一页的 `nextCursor.level` 和 `nextCursor.playerId`。

响应示例：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "items": [
      {
        "playerId": 10002,
        "nickname": "好友甲",
        "level": 18,
        "friendsSince": "2026-07-27 10:30:00",
        "dailyGiftSent": true,
        "dailyGiftReceived": false
      }
    ],
    "nextCursor": {
      "level": 18,
      "playerId": 10002
    },
    "hasMore": true,
    "dailyGift": {
      "enabled": true,
      "periodKey": "20260727",
      "itemId": 1,
      "itemKey": "gold",
      "quantity": 100,
      "dailySendLimit": 20,
      "sentCount": 3,
      "remainingSendCount": 17,
      "dailyReceiveLimit": 20,
      "receivedCount": 2,
      "remainingReceiveCount": 18,
      "configVersion": 2
    }
  }
}
```

好友排序仍为 `level DESC, playerId ASC`。`dailyGiftSent` 表示当前玩家今天是否已经向该好友发送；`dailyGiftReceived` 表示今天是否已经收到该好友发送的赠礼。

#### 23.2.2 发送每日好友赠礼

```http
POST /api/friends/{friendPlayerId}/gifts/daily
Authorization: Bearer <player-token>
```

请求不接收物品、数量、日期或发送者 ID，所有经济参数和当前玩家身份均由服务端决定。

首次发放响应：

```json
{
  "code": 200,
  "message": "好友赠礼发送成功",
  "data": {
    "recordId": 90001,
    "senderPlayerId": 10001,
    "receiverPlayerId": 10002,
    "periodKey": "20260727",
    "itemId": 1,
    "itemKey": "gold",
    "quantity": 100,
    "receiverBalanceAfter": 1600,
    "configVersion": 2,
    "sentTime": "2026-07-27 15:30:00",
    "replayed": false
  }
}
```

同一天重复调用返回相同 `recordId`、奖励快照、发放后余额和发送时间，仅 `replayed=true`，不会再次发放。

### 23.3 管理端配置

入口：

```text
配置中心 -> 游戏好友赠礼 -> 每日好友赠礼
```

模块和配置键：

```text
moduleCode = PLAYER_FRIEND_GIFT
configKey  = DAILY_GIFT
```

可配置字段：

| 字段 | 含义 | 约束 |
| --- | --- | --- |
| `itemId` / `itemKey` | 系统实际发放的背包物品 | 必须指向同一项已启用物品 |
| `quantity` | 每次发放数量 | 1～1,000,000,000 |
| `dailySendLimit` | 每名玩家每日发送总次数 | 1～1,000 |
| `dailyReceiveLimit` | 每名玩家每日接收总次数 | 1～1,000 |
| `enabled` | 玩家赠礼总开关 | V34 初始为 `false` |
| `configVersion` | 正式配置乐观锁版本 | 后端维护，只读 |

该模块标记为 `adminOnlyWrite=true`：配置查看员可以读取和校验，只有配置管理员可以保存草稿、发布、丢弃或创建回滚草稿。配置发布继续复用配置中心的完整快照、版本历史、不可变审计和发布事务。

### 23.4 事务、锁顺序与幂等

首次发放事务固定执行：

```text
读取当前登录玩家
  -> 按玩家 ID 升序 SELECT users FOR UPDATE
  -> 获取数据库北京时间并生成 yyyyMMdd
  -> 查询已有有向赠礼流水 FOR UPDATE
  -> 锁定 player_friend 好友关系
  -> 读取并校验正式配置和已启用物品
  -> 统计发送者、接收者当天成功流水
  -> 增加接收者背包物品
  -> 查询发放后余额
  -> 写入不可变赠礼流水
  -> 提交事务
```

并发约束：

1. 所有添加好友、删除好友和每日赠礼都按较小玩家 ID、较大玩家 ID 的顺序锁 `users`，A→B 与 B→A 不会形成反向玩家锁。
2. `UNIQUE(sender_player_id, receiver_player_id, period_key)` 是数据库最终防重护栏。
3. 同一发送者向不同好友会被发送者玩家行串行，发送总上限不会并发超发。
4. 不同发送者向同一接收者会被接收者玩家行串行，接收总上限不会并发超发。
5. 好友关系使用 `SELECT ... FOR UPDATE`，删除好友与发放不会出现“校验通过后关系已删除”的并发穿透。
6. 背包发放、余额快照和赠礼流水位于同一事务；任何一步失败全部回滚。
7. 已存在流水优先于当前好友关系和当前配置返回，因此首次成功后的网络重试不会因后来删好友或停用配置而失去原始结果。

北京时间来自数据库 `CONVERT_TZ(UTC_TIMESTAMP(6), '+00:00', '+08:00')`，客户端不能提交日期，应用服务器系统时区不会影响每日边界。

### 23.5 V34 数据表

| 表 | 用途 | 关键约束 |
| --- | --- | --- |
| `player_friend_gift_config` | 当前正式赠礼经济配置 | 单例 `id=1`、物品外键、数量/限额/版本检查 |
| `player_friend_gift_record` | 每次成功发放的不可变流水 | 有向好友每日唯一、双方玩家外键、奖励和余额快照 |

主要索引：

```text
uk_player_friend_gift_daily_pair(sender_player_id, receiver_player_id, period_key)
idx_player_friend_gift_sender_period(sender_player_id, period_key, id)
idx_player_friend_gift_receiver_period(receiver_player_id, period_key, id)
```

### 23.6 新增错误码

| code | message | 典型场景 |
| --- | --- | --- |
| 6702 | 双方当前不是游戏好友 | 向非好友发送，或好友已删除 |
| 6703 | 好友每日赠礼当前未开放 | 正式配置 `enabled=false` |
| 6704 | 今日好友赠礼发送次数已达上限 | 发送者当天成功流水达到总上限 |
| 6705 | 该好友今日接收赠礼次数已达上限 | 接收者当天成功流水达到总上限 |
| 6706 | 好友每日赠礼配置错误 | 正式配置缺失、物品停用或物品 ID/编码不匹配 |
| 6707 | 好友赠礼发放后余额超出系统上限 | 背包 BIGINT 加法越界并回滚 |

重复发送不是错误，返回第一次结果并设置 `replayed=true`。向自己发送继续返回 `6701`。

### 23.7 上线检查

1. 停止旧节点后备份数据库，由单节点执行 V34。
2. 确认两张 V34 表、三个核心索引和默认单例配置存在。
3. 保持 `enabled=false`，先在管理端保存草稿并校验物品、数量和双向限额。
4. 使用配置管理员发布启用，确认 `config_version` 从 1 增加到 2。
5. 使用两个测试好友并发执行 A→B、B→A，各自余额只增加一次。
6. 重复请求确认返回相同 `recordId` 且 `replayed=true`。
7. 删除好友后新发送返回 6702；已经成功的同日请求重放仍返回第一次结果。
8. 检查好友列表 `dailyGiftSent`、`dailyGiftReceived` 和顶层余量与数据库流水一致。

### 23.8 本次验证结果

验证日期：2026-07-27（Asia/Shanghai）。

| 验证项 | 结果 |
| --- | --- |
| 后端全量测试 | 411 个测试，0 failure，0 error；49 个需要外部环境的用例按条件跳过 |
| MySQL 8 集成测试 | Flyway 空库/旧库升级、配置真实发布、A→B/B→A 六路并发全部通过，退出码 0 |
| Flyway | 成功校验并执行至 V34，共 34 个迁移版本 |
| 好友赠礼专项单测 | `FriendServiceImplTest`、`FriendGiftServiceImplTest`、`FriendGiftOperatorConfigHandlerTest` 通过 |
| 管理端生产构建 | `vue-tsc --noEmit && vite build` 通过，3496 个模块完成转换 |
| 补丁格式检查 | `git diff --check` 通过，仅存在工作树原有的行尾转换提示 |
| 主开发库运行态 | Flyway 确认 `game_db` 当前版本为 34，应用使用当前代码重启成功 |
| 后端 HTTP 验收 | `GET /api/health` 返回 200；未登录调用赠礼接口返回 401 |
| 管理端页面验收 | `GET http://localhost:5173/configs` 返回 200 |

执行命令：

```powershell
mvn -q "-Dmaven.resources.skip=true" test
$env:RUN_MYSQL_INTEGRATION_TESTS='true'
mvn -q "-Dmaven.resources.skip=true" "-Dtest=FlywayMigrationIntegrationTest,BusinessConcurrencyIntegrationTest,OperatorConfigPersistenceIntegrationTest" test
npm.cmd --prefix admin-web run build
git diff --check
```

## 24. 中文注释与维护契约审计

审计日期：2026-07-27（Asia/Shanghai）。本轮只调整注释和开发文档，不改变接口、数据库结构或业务行为。

### 24.1 覆盖范围与注释准则

本轮检查了以下范围：

1. `src/main/java/com/game/server` 下 666 个生产 Java 文件。
2. `src/test/java` 下 103 个 Java 测试文件。
3. `admin-web/src` 下的 Vue、TypeScript 和 JavaScript 源码。
4. `src/main/resources` 下的 MyBatis XML、YAML 和 properties 配置。
5. `scripts` 下的 PowerShell 运维脚本。

统一准则如下：

1. 生产代码顶层 `class`、`interface`、`record`、`enum` 必须有准确的中文职责说明。
2. Service、Controller、Scheduler、Configuration、协议与安全组件的非覆写公共方法，必须说明真实业务契约；涉及事务、幂等、锁顺序、缓存、租约、鉴权或失败降级时必须明确边界。
3. `@Override` 方法继承父接口契约，不重复粘贴同义注释；Mapper 的直观增删改查、DTO/VO/Entity 的简单访问器也不添加流水账注释。
4. Redis、WebSocket、HTTP、SQL、ID、API、NPC、JSON 等技术标识可以保留；完整英文业务句子必须翻译为中文。
5. 注释必须解释职责和约束，不能只把方法名机械翻译一遍，也不能描述显而易见的赋值步骤。

### 24.2 审计与补全结果

| 审计项 | 结果 |
| --- | --- |
| 生产 Java 顶层类型缺少类级说明 | 0 |
| 业务层非覆写公共方法缺少契约说明 | 0 |
| 生产/测试 Java 中纯英文业务注释 | 0 |
| Vue/TypeScript/XML/YAML/PowerShell 中纯英文业务注释 | 0 |
| 前端保留项 | `admin-web/src/env.d.ts` 的 `/// <reference types="vite/client" />`，这是 TypeScript 编译指令，不是业务注释 |

本轮重点补全了玩家成长与任务、好友赠礼、运营认证、经验调整与对账、配置 Outbox、邮件与通知、公告调度、支付退款、Redis Streams 发货、WebSocket 多实例会话、签到、微信消息安全等模块。关键说明已经覆盖玩家聚合锁顺序、经验发放幂等回放、失败计数提交、令牌轮换、退款租约、配置快照原子替换和直接会话发送的鉴权前提。

### 24.3 Flyway 历史迁移例外

`src/main/resources/db/migration/V*.sql` 中仍有少量已发布英文注释，本轮有意不修改。Flyway 校验和包含脚本文本和注释，翻译已执行迁移中的注释会使现有数据库在启动时产生 checksum mismatch。

处理规则：

1. 已发布迁移保持字节级不变，不能为了统一注释执行无依据的 `flyway repair`。
2. 新迁移从创建时开始使用中文业务注释。
3. 如需修正历史迁移表达，写入开发文档或新增后续迁移说明，不覆盖生产库已经确认的校验和。

### 24.4 验证结果

| 验证项 | 结果 |
| --- | --- |
| 后端全量测试 | 411 个测试，0 failure，0 error，49 个外部环境用例按条件跳过 |
| 管理端生产构建 | `vue-tsc --noEmit && vite build` 通过，3496 个模块完成转换 |
| 补丁格式检查 | `git diff --check` 通过，仅有工作树原有行尾转换提示 |
| Java 英文注释扫描 | 无命中 |
| 业务公共方法契约扫描 | `missingCount=0` |

验证命令：

```powershell
rg -n -P --glob '*.java' '^\s*(?://|/\*\*?|\*)(?!.*\p{Han}).*[A-Za-z]' src/main/java/com/game/server src/test/java
mvn -q "-Dmaven.resources.skip=true" test
npm.cmd --prefix admin-web run build
git diff --check
```

## 25. 玩家资源出售（V35）

### 25.1 功能语义与边界

玩家可以把管理端正式配置允许的 `RESOURCE` 或 `PRODUCT` 类型物品按整数批次出售给系统，第一期只结算金币 `gold`。

1. 接口只操作当前登录玩家，不接受客户端提交 `playerId`。
2. 玩家先读取正式报价和当日额度，再携带配置版本确认出售。
3. 每种来源资源分别按北京时间 UTC+8 自然日统计额度，客户端不能提交日期。
4. 资源扣减、金币增加、每日额度和不可变订单在同一事务中提交。
5. 相同幂等键和相同请求完整重放第一次成交，不会重复扣资源或发金币。
6. V35 部署后默认关闭，且不预置任何兑换比例；配置管理员发布后才可开放。
7. V35 保持已发布校验和不变，V36 单独加强客户端安全整数、成交乘积和余额恒等式约束。

当前 P0 由客户端生成并持久化标准 UUID `Idempotency-Key`。尚未提供服务端预生成幂等键接口，也没有向小游戏端广播 `RESOURCE_SALE_CONFIG_UPDATED`；客户端收到 HTTP 409 后主动刷新报价。

### 25.2 查询可出售资源

```http
GET /api/resource-sales/options
Authorization: Bearer <player-token>
```

响应示例：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "enabled": true,
    "configRevision": 3,
    "usageDate": "2026-07-28",
    "options": [
      {
        "sourceItemId": 2,
        "sourceItemKey": "wood",
        "sourceItemName": "木材",
        "sourceItemType": "RESOURCE",
        "sourceItemIcon": "wood",
        "targetItemId": 1,
        "targetItemKey": "gold",
        "targetItemName": "金币",
        "targetItemIcon": "gold",
        "sourceQuantityPerLot": 10,
        "targetQuantityPerLot": 3,
        "minLotsPerOrder": 1,
        "maxLotsPerOrder": 100,
        "dailySourceQuantityLimit": 10000,
        "usedSourceQuantity": 120,
        "remainingDailySourceQuantity": 9880,
        "sourceBalance": 560,
        "maxSellableLots": 56
      }
    ]
  }
}
```

`maxSellableLots` 已综合全局开关、来源余额、北京时间当日剩余额度和单笔最大批次。若不足单笔最小批次则返回 `0`。

### 25.3 确认出售

```http
POST /api/resource-sales
Authorization: Bearer <player-token>
Idempotency-Key: c40f79ec-820d-4f30-a120-f9ec2d8fd21f
Content-Type: application/json
```

```json
{
  "sourceItemId": 2,
  "lotCount": 20,
  "expectedConfigRevision": 3
}
```

成功响应：

```json
{
  "code": 200,
  "message": "资源出售成功",
  "data": {
    "orderId": 9001,
    "orderNo": "4077e190-d445-40fd-94bd-17a03e9eb94c",
    "usageDate": "2026-07-28",
    "sourceItemId": 2,
    "sourceItemKey": "wood",
    "targetItemId": 1,
    "targetItemKey": "gold",
    "sourceQuantityPerLot": 10,
    "targetQuantityPerLot": 3,
    "lotCount": 20,
    "soldQuantity": 200,
    "grantedQuantity": 60,
    "sourceBalanceBefore": 560,
    "sourceBalanceAfter": 360,
    "targetBalanceBefore": 1000,
    "targetBalanceAfter": 1060,
    "configRevision": 3,
    "createTime": "2026-07-28T16:30:00.123456",
    "replayed": false
  }
}
```

幂等键使用规则：

1. 玩家进入确认动作时生成一个 UUID，并持久化到该待提交动作。
2. 网络超时或响应丢失时复用相同 UUID 和完全相同的请求体。
3. 玩家修改来源资源、批次数或重新确认最新报价时生成新 UUID。
4. 相同键、相同请求返回第一次订单的完整快照并设置 `replayed=true`。
5. 相同键、不同请求返回 `7208`，客户端不得继续复用该键。

配置版本发生变化时返回结构化 HTTP 409：

```json
{
  "code": 7203,
  "message": "资源出售配置已更新，请刷新后重新确认",
  "data": {
    "latestRevision": 4,
    "refreshRequired": true
  }
}
```

客户端必须重新读取 `/resource-sales/options` 并让玩家确认最新比例，不能静默按新报价成交。

### 25.4 查询出售历史

```http
GET /api/resource-sales/history?beforeId=9001&limit=20
Authorization: Bearer <player-token>
```

`beforeId` 可选，使用上一页 `nextCursor`；`limit` 默认 20，范围 1～100。订单按主键倒序返回，仅当 `hasMore=true` 时继续请求下一页。

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "items": [
      {
        "orderId": 9000,
        "orderNo": "e23ed902-ce9d-4f19-aee0-7fa1bf7052d6",
        "usageDate": "2026-07-28",
        "sourceItemId": 2,
        "sourceItemKey": "wood",
        "targetItemId": 1,
        "targetItemKey": "gold",
        "lotCount": 20,
        "soldQuantity": 200,
        "grantedQuantity": 60,
        "sourceBalanceAfter": 360,
        "targetBalanceAfter": 1060,
        "configRevision": 3,
        "createTime": "2026-07-28T16:30:00.123456"
      }
    ],
    "nextCursor": 9000,
    "hasMore": true
  }
}
```

### 25.5 管理端配置

入口：

```text
配置中心 -> 资源出售 -> 资源兑换规则
moduleCode = RESOURCE_SALE
configKey  = EXCHANGE_OPTIONS
```

配置快照：

```json
{
  "enabled": false,
  "configRevision": 1,
  "options": [
    {
      "sourceItemId": 2,
      "sourceItemKey": "wood",
      "targetItemId": 1,
      "targetItemKey": "gold",
      "sourceQuantityPerLot": 10,
      "targetQuantityPerLot": 3,
      "minLotsPerOrder": 1,
      "maxLotsPerOrder": 100,
      "dailySourceQuantityLimit": 10000,
      "sortOrder": 10,
      "enabled": true
    }
  ]
}
```

发布约束：

1. 来源必须是已启用的 `RESOURCE` 或 `PRODUCT`，同一来源只能配置一条规则。
2. 目标必须是已启用的 `CURRENCY` 类型 `gold`。
3. 每批数量和单笔批次范围均为 1～1,000,000,000。
4. 每日来源数量上限不超过 9,000,000,000,000,000，且不能小于单笔最大来源数量。
5. 单笔最大来源和目标成交数量不能超过客户端安全整数上限。
6. 全局开放时至少有一条启用规则。
7. `configRevision` 由后端维护，实际配置变化后递增，页面只读。

该模块标记为 `adminOnlyWrite=true`。查看员和普通配置编辑员可以读取；只有 `CONFIG_ADMIN` 可以保存草稿、发布、丢弃和创建回滚草稿。

### 25.6 事务、锁顺序和幂等

新成交事务固定执行：

```text
锁定 users 玩家聚合行
  -> 查询 player_resource_sale_order 幂等订单 FOR UPDATE
  -> 校验经济冻结和退款活动状态
  -> 共享锁定 resource_sale_policy
  -> 共享锁定 resource_sale_option
  -> 按 item_id 升序锁定 player_item
  -> 锁定 player_resource_sale_daily_usage
  -> 扣资源
  -> 加金币
  -> 增加每日额度
  -> 写入不可变订单
  -> 提交事务
```

幂等订单在当前经济冻结和当前配置之前查询；玩家首次成功后即使后来被冻结、规则停用或价格变化，重试仍能拿到第一次的完整结果。配置发布按策略、规则的同向顺序加锁，不与成交形成反向等待。所有写入使用同一个默认 `REQUIRED` 事务，不使用 `REQUIRES_NEW`。

### 25.7 V35 数据表

| 表 | 用途 | 关键约束 |
| --- | --- | --- |
| `resource_sale_policy` | 全局开关与正式配置版本 | 单例 `id=1`、默认关闭、版本大于 0 |
| `resource_sale_option` | 整数批次兑换规则 | 来源物品唯一、物品外键、数量和批次检查 |
| `player_resource_sale_daily_usage` | 每种资源的北京时间每日用量 | `(player_id, usage_date, source_item_id)` 唯一 |
| `player_resource_sale_order` | 不可变成功成交快照 | `(player_id, idempotency_key)` 唯一、兑换乘积和前后余额检查 |

### 25.8 新增业务错误码

| code | message | 客户端处理 |
| --- | --- | --- |
| 7201 | 资源出售当前未开放 | 关闭入口并刷新配置 |
| 7202 | 资源出售规则不存在或未启用 | 重新查询可出售资源 |
| 7203 | 资源出售配置已更新，请刷新后重新确认 | HTTP 409，刷新报价 |
| 7204 | 出售批次数量不符合当前规则 | 修正批次数 |
| 7205 | 可出售资源不足 | 刷新余额 |
| 7206 | 该资源今日出售额度不足 | 刷新当日额度 |
| 7207 | 出售结算后余额超出系统上限 | 停止重试并告警 |
| 7208 | 出售幂等请求与首次请求不一致 | 为新意图生成新 UUID |
| 7209 | 资源出售正式配置不可用 | 关闭入口并告警运营 |
| 7210 | 资源余额已变化，请刷新后重试 | 刷新余额并使用新 UUID |

玩家处于退款经济冻结状态时返回 `6401`；请求字段或 UUID 格式错误返回 `400`。

### 25.9 上线检查

1. 备份数据库并由单节点执行 Flyway 至 V36，确认四张表、外键、唯一索引和 V36 加固约束存在。
2. 确认默认单例 `enabled=false`、`config_revision=1`，并确认没有预置价格。
3. 配置管理员创建草稿，复核兑换比例、单笔范围和每日额度，再发布开放。
4. 验证正常成交、相同键重放、同键异参、余额不足、额度不足和旧报价 HTTP 409。
5. 对账资源、金币、每日额度和订单快照，任何失败不得留下部分写入。
6. 监控数据库死锁、`7209` 配置损坏和 `7207` 余额溢出后再逐步放量。

若启动时出现 V35 checksum mismatch，禁止直接修改 `flyway_schema_history` 或盲目执行 `repair`。当前 V35 已发布校验和为 `-1093160282`，约束增强必须由 V36 追加执行。

仓库中的独立维护文档：`docs/resource-sale-api.md`。

### 25.10 本次验证结果

验证日期：2026-07-28（Asia/Shanghai）。

| 验证项 | 结果 |
| --- | --- |
| 后端全量测试 | 430 项，0 failure，0 error；50 项外部环境用例按条件跳过 |
| 资源出售定向测试 | 服务、配置、经济冻结与 HTTP 409 契约共 21 项通过 |
| MySQL 8 迁移测试 | 旧库从 V16 升级至 V36，20 个待执行迁移、新表、唯一键、检查约束和默认策略全部通过 |
| MySQL 8 并发测试 | 37 项全部通过；六个同键出售请求仅扣资源、发金币、写订单和累计额度一次，其余五次完整重放 |
| 本机开发库启动 | V35 校验和 `-1093160282` 验证通过，V36 成功执行，应用在随机验证端口完整启动后已关闭 |
| 管理端生产构建 | `vue-tsc --noEmit && vite build` 通过，3499 个模块完成转换 |
| 补丁格式检查 | `git diff --check` 通过，仅有工作树原有行尾转换提示 |

验证命令：

```powershell
mvn -q test
$env:RUN_MYSQL_INTEGRATION_TESTS='true'
mvn -q "-Dtest=FlywayMigrationIntegrationTest" test
mvn -q "-Dtest=BusinessConcurrencyIntegrationTest" test
npm.cmd --prefix admin-web run build
git diff --check
```

## 26. 游戏内好友申请（V37）

### 26.1 最终效果与边界

玩家现在通过“精确输入玩家 ID、发送申请、对方同意”建立游戏好友，不再由任意一方单方面添加成功。

1. 搜索只支持精确 `users.id`，不提供昵称模糊搜索或全服玩家枚举。
2. 收到和发出的多条申请分别使用游标分页展示，第一版逐条同意、拒绝或取消，不提供批量处理。
3. 只有申请接收方可以同意或拒绝，只有申请发起方可以取消。
4. 只有同意成功后才写入 `player_friend`；好友列表继续按 `level DESC, playerId ASC` 排序。
5. 成为正式好友后可以继续使用第 23 章的每日好友金币赠礼，申请本身不携带资源。
6. 本模块不是微信官方好友关系链；真实微信好友昵称、头像和平台赠礼仍由第 20 章的开放数据域负责。
7. 自然日申请额度统一使用数据库北京时间，即 UTC+8 的 `00:00:00` 重置。

所有玩家接口都必须携带：

```http
Authorization: Bearer <player-token>
```

### 26.2 精确搜索与发起申请

#### 精确搜索玩家

```http
GET /api/friends/search?playerId=20002
Authorization: Bearer <player-token>
```

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "playerId": 20002,
    "nickname": "玩家乙",
    "level": 18,
    "relationshipStatus": "NONE",
    "pendingRequestId": null
  }
}
```

`relationshipStatus` 取值：

| 值 | 说明 |
| --- | --- |
| `SELF` | 查询的是当前玩家自己 |
| `FRIEND` | 双方已经是正式游戏好友 |
| `NONE` | 当前没有正式关系或有效待处理申请 |
| `OUTGOING_PENDING` | 当前玩家是待处理申请发起方 |
| `INCOMING_PENDING` | 当前玩家是待处理申请接收方 |

搜索结果只暴露 `playerId/nickname/level`，不会返回用户名、openid、背包、金币或钻石。目标不存在返回 `2001`。

#### 发起申请

推荐接口：

```http
POST /api/friends/requests
Authorization: Bearer <player-token>
Content-Type: application/json
```

```json
{
  "recipientPlayerId": 20002
}
```

旧客户端兼容入口：

```http
POST /api/friends/20002
Authorization: Bearer <player-token>
```

两个入口都只创建或返回申请。成功响应中的核心结构：

```json
{
  "code": 200,
  "message": "好友申请已提交或已存在",
  "data": {
    "requestId": 91001,
    "direction": "OUTGOING",
    "requesterPlayerId": 10001,
    "requesterNickname": "玩家甲",
    "requesterLevel": 12,
    "recipientPlayerId": 20002,
    "recipientNickname": "玩家乙",
    "recipientLevel": 18,
    "status": "PENDING",
    "createTime": "2026-07-28 16:00:00",
    "expireTime": "2026-08-04 16:00:00",
    "resolvedTime": null,
    "resolutionReason": null,
    "relationshipEndedTime": null,
    "canAccept": false,
    "canReject": false,
    "canCancel": true
  }
}
```

同一对玩家任意方向最多只有一条有效 `PENDING` 申请。A 已向 B 申请时，B 再向 A 发起不会自动同意，而是返回原申请并标记 `direction=INCOMING`。申请有效期在创建时按正式策略冻结；后续调整策略不会追溯修改已有申请。

### 26.3 申请列表与摘要

收到的申请：

```http
GET /api/friends/requests/incoming?status=PENDING&limit=20
Authorization: Bearer <player-token>
```

发出的申请：

```http
GET /api/friends/requests/outgoing?status=PENDING&limit=20
Authorization: Bearer <player-token>
```

查询参数：

| 参数 | 必填 | 规则 |
| --- | --- | --- |
| `status` | 否 | 默认 `PENDING`；支持六种持久化状态和 `ALL`，不区分大小写 |
| `beforeId` | 否 | 上一页 `nextCursor`，必须为正整数 |
| `limit` | 否 | 默认 20，范围 1 至 100 |

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "items": [],
    "nextCursor": null,
    "hasMore": false
  }
}
```

分页按申请主键倒序。仅当 `hasMore=true` 时继续携带 `nextCursor`。默认 `PENDING` 会过滤实时已到期申请；查询 `EXPIRED` 会同时返回已固化和等待扫描固化的到期记录。

申请摘要：

```http
GET /api/friends/requests/summary
Authorization: Bearer <player-token>
```

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "enabled": true,
    "friendCount": 28,
    "maxFriendCount": 200,
    "pendingOutgoingCount": 2,
    "maxPendingOutgoing": 50,
    "pendingIncomingCount": 5,
    "maxPendingIncoming": 100,
    "usageDate": "2026-07-28",
    "dailyRequestCount": 3,
    "dailyRequestLimit": 30,
    "resendCooldownHours": 24,
    "requestExpirationDays": 7,
    "configVersion": 1
  }
}
```

客户端可用摘要显示申请角标、容量和当日余量；写接口仍会在事务内重新校验，不能把摘要当作授权凭证。

### 26.4 同意、拒绝和取消

```http
POST   /api/friends/requests/{requestId}/accept
POST   /api/friends/requests/{requestId}/reject
DELETE /api/friends/requests/{requestId}
Authorization: Bearer <player-token>
```

三个接口均不需要请求体，并返回第一次形成的完整申请终态：

| 状态 | 形成方式 | 后续语义 |
| --- | --- | --- |
| `PENDING` | 发起申请 | 接收方可同意/拒绝，发起方可取消 |
| `ACCEPTED` | 接收方同意 | 正式关系存在时，同意可完整幂等重放 |
| `REJECTED` | 接收方拒绝 | 相同拒绝动作完整幂等重放 |
| `CANCELLED` | 发起方取消 | 相同取消动作完整幂等重放 |
| `EXPIRED` | 超过有效期 | 不可处理，必须重新申请 |
| `INVALIDATED` | 账号生命周期或一致性修复 | 不可处理 |

删除由该申请建立的好友关系后，原申请会记录 `relationshipEndedTime`。此后重放旧同意操作返回 `6720`，不会恢复关系，双方必须创建并同意一条新申请。删除好友不会自动取消双方另一条仍为 `PENDING` 的申请；该申请仍可按正常规则处理。玩家账号物理删除时，关联申请通过外键级联清理，列表不会返回缺失玩家资料。

### 26.5 正式好友与每日赠礼衔接

同意成功后查询正式好友：

```http
GET /api/friends?limit=20
Authorization: Bearer <player-token>
```

删除正式好友：

```http
DELETE /api/friends/{friendPlayerId}
Authorization: Bearer <player-token>
```

向正式好友发送北京时间当日赠礼：

```http
POST /api/friends/{friendPlayerId}/gifts/daily
Authorization: Bearer <player-token>
```

赠礼资源、发送上限和接收上限继续由 `PLAYER_FRIEND_GIFT / DAILY_GIFT` 管理，与好友申请策略独立。非正式好友发送返回 `6702`。

### 26.6 管理端好友申请策略

入口：

```text
配置中心 -> 游戏好友申请 -> 好友申请策略
moduleCode = PLAYER_FRIEND_REQUEST
configKey  = POLICY
```

配置快照：

```json
{
  "enabled": true,
  "maxFriendCount": 200,
  "maxPendingOutgoing": 50,
  "maxPendingIncoming": 100,
  "dailyRequestLimit": 30,
  "resendCooldownHours": 24,
  "requestExpirationDays": 7,
  "configVersion": 1
}
```

| 字段 | 范围 | 说明 |
| --- | ---: | --- |
| `enabled` | `true/false` | 关闭后阻止新建申请，已有申请仍可查询和处理 |
| `maxFriendCount` | 1 至 1000 | 同意时双方都必须未达到上限 |
| `maxPendingOutgoing` | 1 至 1000 | 每名发起方有效待处理上限 |
| `maxPendingIncoming` | 1 至 1000 | 每名接收方有效待处理上限 |
| `dailyRequestLimit` | 1 至 1000 | 北京时间自然日创建总数上限 |
| `resendCooldownHours` | 0 至 720 | 同方向被拒绝后的再次申请冷却 |
| `requestExpirationDays` | 1 至 365 | 只冻结到新申请的有效期 |
| `configVersion` | 正整数 | 后端乐观版本，页面只读 |

该模块 `adminOnlyWrite=true`。`CONFIG_VIEWER` 和 `CONFIG_EDITOR` 可以读取；只有 `CONFIG_ADMIN` 可以保存、发布、丢弃或创建回滚草稿。发布复用 `/api/ops/config/**` 通用接口、数据库锁、草稿修订、正式指纹、版本历史和追加审计。

### 26.7 数据库、锁顺序与过期扫描

V37 新增或调整：

| 表或字段 | 用途 | 关键约束 |
| --- | --- | --- |
| `player_friend_request_policy` | 单例正式策略 | `id=1`、字段范围检查、配置版本 |
| `player_friend_request` | 申请和完整处理历史 | 规范化玩家对、单一待处理、状态/原因/时间检查、双方外键 |
| `player_friend.accepted_request_id` | 当前关系的来源申请 | 唯一外键，迁移前好友允许为 `NULL` |

所有申请写事务固定锁序：

```text
较小 users.id
  -> 较大 users.id
  -> player_friend 规范化关系
  -> player_friend_request 申请记录
```

同意申请在同一个默认 `REQUIRED` 事务中推进状态并写入正式关系，任何一步失败都整体回滚。玩家主行锁同时保护好友容量、待处理数量和当日创建额度，A→B 与 B→A 并发申请不会产生两条待处理记录。

后台默认每 60 秒最多固化 500 条到期记录：

```text
FRIEND_REQUEST_EXPIRATION_ENABLED
FRIEND_REQUEST_EXPIRATION_BATCH_SIZE
FRIEND_REQUEST_EXPIRATION_INITIAL_DELAY_MS
FRIEND_REQUEST_EXPIRATION_FIXED_DELAY_MS
```

接口本身始终按数据库北京时间实时判断是否到期，因此扫描延迟或短暂停止不会让过期申请被接受。

### 26.8 业务错误码

| code | message | 客户端处理 |
| --- | --- | --- |
| 2001 | 玩家不存在 | 检查输入 ID 或刷新目标资料 |
| 6701 | 不能将自己添加为好友 | 禁止向自己提交 |
| 6708 | 双方已经是游戏好友 | 刷新正式好友状态 |
| 6709 | 游戏内好友申请当前未开放 | 关闭新增入口，已有申请仍可展示 |
| 6710 | 好友申请不存在 | 从列表移除该记录 |
| 6711 | 该好友申请已过期，请重新发起 | 刷新列表并允许重新申请 |
| 6712 | 好友申请状态已变化，请刷新后重试 | 重新读取申请终态 |
| 6713 | 你无权处理该好友申请 | 校验当前玩家和动作按钮 |
| 6714 | 你或对方的好友数量已达上限 | 清理好友后重试 |
| 6715 | 待处理的发出申请数量已达上限 | 取消或等待已有发出申请 |
| 6716 | 对方待处理的收到申请数量已达上限 | 稍后重试 |
| 6717 | 今日发起好友申请次数已达上限 | 等待北京时间次日重置 |
| 6718 | 好友申请仍在重新发送冷却期内 | 按服务端提示时间等待 |
| 6719 | 好友申请正式配置不可用 | 关闭入口并告警运营 |
| 6720 | 该好友申请已不能用于建立好友关系 | 创建并处理一条新申请 |

请求参数错误返回 `400`，未登录返回 HTTP 401。好友申请业务错误使用统一响应体 `code`。

### 26.9 上线与验收

1. 备份数据库并由单节点执行 Flyway 至 V38，确认申请结构和好友在线状态快照字段存在。
2. 由配置管理员复核默认容量、每日额度、冷却和有效期，再决定是否调整并发布。
3. 客户端把旧“添加成功”交互改为“申请等待”，实现收到/发出多条申请和逐条操作。
4. 验证 A→B 与 B→A 并发申请只产生一条待处理记录，重复同意只产生一条正式关系。
5. 验证拒绝冷却、北京时间额度、实时过期、扫描固化、删除后重新申请和账号物理删除级联。
6. 验证正式好友列表仍按等级排序，每日好友金币赠礼不受申请流程升级影响。

仓库中的独立维护文档：`docs/friend-request-api.md`。

验证命令：

```powershell
mvn -q test
$env:RUN_MYSQL_INTEGRATION_TESTS='true'
mvn -q test
npm.cmd --prefix admin-web run build
git diff --check
```

本次验证结果（2026-07-28，Asia/Shanghai）：

| 验证项 | 结果 |
| --- | --- |
| 后端全量测试 | 458 项，0 failure，0 error；58 项外部环境用例按条件跳过 |
| 好友申请专项单测 | 服务 6 项、正式好友服务 5 项、管理端策略 5 项，全部通过 |
| Flyway、Redis 与业务并发 | MySQL 8 的 V1 至 V38、Redis 多节点 Presence 集成和业务并发测试通过 |
| 管理端生产构建 | `vue-tsc --noEmit && vite build` 通过，3502 个模块完成转换 |
| 补丁格式检查 | `git diff --check` 通过，仅有现有行尾转换提示 |
| 进程复核 | Spring Boot 与 Vite 开发服务均已关闭，验证端口无监听 |

V1 至 V38 已发布迁移禁止修改；后续变化必须新增 V39 或更高版本。

## 27. 游戏好友在线状态（V38）

### 27.1 最终效果与边界

1. 玩家建立有效 WebSocket 连接后，正式游戏好友显示 `ONLINE`。
2. 同一玩家多个设备同时连接时，首个连接触发上线，最后一个连接消失才触发离线。
3. HTTP 登录和普通接口请求只更新最近活跃时间，不会把玩家误判为 WebSocket 在线。
4. 本功能只服务 `player_friend` 正式游戏好友，不读取微信官方好友关系或开放数据域。
5. 多实例模式以 Redis 会话租约为实时真相；`player_online_state.online` 只用于审计和对账。
6. Redis 不可用时状态返回 `UNKNOWN`，好友列表接口仍成功，不影响好友申请、删除或赠礼。

### 27.2 正式好友列表字段

```http
GET /api/friends?limit=20
Authorization: Bearer <player-token>
```

固定排序仍为 `level DESC, playerId ASC`，分页继续使用 `cursorLevel + cursorPlayerId` 复合游标。好友对象示例：

```json
{
  "playerId": 20002,
  "nickname": "玩家乙",
  "level": 18,
  "friendsSince": "2026-07-20 12:00:00",
  "dailyGiftSent": false,
  "dailyGiftReceived": true,
  "onlineStatus": "ONLINE",
  "presenceVersion": 1785232800123,
  "lastActiveTime": "2026-07-28 18:00:00",
  "lastOfflineTime": "2026-07-28 10:00:00"
}
```

| 字段 | 说明 |
| --- | --- |
| `onlineStatus` | `ONLINE`、`OFFLINE` 或 `UNKNOWN` |
| `presenceVersion` | 状态单调版本；客户端增量合并的唯一顺序依据 |
| `lastActiveTime` | 最近登录、HTTP 活动或 WebSocket 心跳的北京时间，可为空 |
| `lastOfflineTime` | 数据库最后一次确认离线的北京时间，可为空 |

客户端不得根据最近活跃时间自行覆盖 `onlineStatus`。服务端对一页好友执行一次公开资料 SQL 和一次 Redis Pipeline，不存在按好友逐条查询的 N+1 问题。

### 27.3 WebSocket 状态推送

仍使用现有连接：

```text
ws://localhost:8080/api/ws/realtime
wss://你的域名/api/ws/realtime
Authorization: Bearer <player-token>
```

状态切换在默认 500 毫秒窗口内合并，只向当前在线的正式好友推送：

```json
{
  "type": "FRIEND_PRESENCE_CHANGED",
  "eventId": "0f230407-5dd8-4da3-8b16-12cb4cc2ca81",
  "playerId": 10001,
  "data": {
    "playerId": 20002,
    "onlineStatus": "OFFLINE",
    "presenceVersion": 1785232805123,
    "lastActiveTime": "2026-07-28 18:00:00",
    "lastOfflineTime": "2026-07-28 18:00:05"
  },
  "serverTime": "2026-07-28 18:00:05"
}
```

外层 `playerId` 是消息接收玩家，`data.playerId` 是状态发生变化的好友。

客户端必须按以下规则实现：

1. 首次读取好友列表时保存每名好友的 `presenceVersion`。
2. 推送版本小于等于本地版本时直接丢弃。
3. 只有更大版本可以覆盖状态和时间字段。
4. WebSocket 重连成功后重新读取好友列表，恢复可能漏掉的增量。
5. 在线状态推送不离线补发，核心好友和赠礼业务不得依赖该消息成功。

### 27.4 Redis、版本与对账

默认 Redis 键：

```text
realtime:presence:{playerId}:sessions   ZSet，会话和租约到期毫秒
realtime:presence:{playerId}:meta       Hash，状态、版本和切换时间
realtime:presence:{publish}:versions    Hash，永久事件版本水位
realtime:presence:{publish}:payloads    Hash，最新待推送载荷
realtime:presence:{publish}:due         ZSet，合并窗口到期时间
realtime:presence:reconcile:lock        String，对账任务所有者令牌
```

建连、续租、断连、对账和事件领取均由 Lua 原子执行。租约使用 Redis `TIME`，ZSet TTL 精确等于最晚会话剩余时间，空集合立即删除。已领取版本水位不会删除，旧事件不能在推送后倒灌；消费前再次核对当前状态，已被新版本替代的事件直接丢弃。

后台默认每 30 秒使用 `(last_active_time, player_id)` 复合游标扫描数据库在线快照，每批 200 条，单轮预算 15 秒。多实例使用带随机所有者令牌的 Redis 锁和 Lua 比较续租/删除。Redis 不可用时立即停止本轮，不会批量写成离线。

### 27.5 数据库迁移

V38 对 `player_online_state` 增加：

| 字段或索引 | 用途 |
| --- | --- |
| `last_offline_time` | 最后一次确认离线时间 |
| `status_version` | 只允许更高版本覆盖的数据库快照版本 |
| `idx_online_active_player (online, last_active_time, player_id)` | 对账候选过滤与稳定游标分页 |

`online` 列保留，但只作为审计和对账快照。实时好友状态必须通过 Presence 服务读取 Redis 租约，禁止 Mapper 或业务模块直接以数据库列作为在线依据。

### 27.6 生产配置

单实例可使用 `REALTIME_PUSH_MODE=local_only`。多实例生产必须让所有节点连接同一 Redis 并配置：

```text
REALTIME_PUSH_MODE=redis_pubsub
REALTIME_PRESENCE_KEY_PREFIX=realtime:presence
REALTIME_PRESENCE_TTL_SECONDS=90
REALTIME_PRESENCE_REFRESH_INTERVAL_MS=30000
PRESENCE_RECONCILIATION_ENABLED=true
PRESENCE_RECONCILIATION_FIXED_DELAY_MS=30000
PRESENCE_RECONCILIATION_BATCH_SIZE=200
PRESENCE_RECONCILIATION_MAX_RUN_MS=15000
PRESENCE_RECONCILIATION_STALE_SECONDS=120
PRESENCE_RECONCILIATION_LOCK_TTL_MS=24000
PRESENCE_RECONCILIATION_LOCK_RENEW_INTERVAL_MS=8000
PRESENCE_EVENT_COALESCE_DELAY_MS=500
PRESENCE_EVENT_PUBLISH_BATCH_SIZE=100
PRESENCE_EVENT_PUBLISH_FIXED_DELAY_MS=100
```

续租间隔必须小于有效租约 TTL 的一半，否则集群模式启动失败。Presence 属于基础设施配置，不开放给普通管理端运营人员修改。

### 27.7 验收结果

本轮已验证：

- Maven 全量测试：458 项，0 failure，0 error，58 项外部环境测试按条件跳过。
- 真实 Redis：全局连接上限、精确 TTL、多设备首连/末连、批量查询、自然过期版本和已发布事件防重通过。
- MySQL 8：V1 至 V38 全量迁移、字段和复合索引验证通过，临时测试库已自动删除。
- 补丁格式：`git diff --check` 通过后再交付。

仓库中的独立维护文档：`docs/friend-presence-api.md`。


## 28. 玩家系统资源头像（V39/V97/V98/V99/V100/V101/V102/V103/V104）

本章是当前头像功能的唯一生效协议。头像图片随 Unity 客户端发布，服务端只保存固定编号；不提供微信头像授权、头像同步、图片上传或远程头像下载。

基础地址：`http://localhost:8080/api`（生产环境使用实际 HTTPS 域名）。

### 28.1 当前业务效果

1. Unity 登录后调用 `GET /api/user/avatar/presets` 获取固定头像目录；响应包含 `enabled` 状态，客户端只把启用项作为可选项。头像业务总开关关闭时目录仍返回，但所有项的 `enabled=false`；已停用或已拥有的头像仍可按编号在本地展示。
2. 玩家选择编号后调用 `PUT /api/user/avatar/preset`，服务端在事务内校验并保存 `avatarSource=PRESET`、`avatarCode`。
3. 登录、个人资料、好友列表、玩家搜索和好友申请响应返回头像编号与版本。
4. `avatarUrl` 是协议兼容字段，当前始终为 `null`；Unity 只能从包内资源加载。
5. `DELETE /api/user/avatar` 恢复 Unity 包内默认头像。
6. 同编号重复选择幂等，不增加版本和每日次数；实际切换才消耗一次每日额度。
7. V99 将历史旧来源归一为默认头像；V100 再规范固定头像编号、补齐注销清理原因并收紧精确 CHECK；历史对象只进入一次性清理 Outbox。

> 旧版客户端调用 `PUT /api/user/avatar` 上传接口会收到 404/405。该接口已下线，不能继续实现或调用。

### 28.2 头像目录

```http
GET /api/user/avatar/presets
Authorization: Bearer <player-token>
```

```json
{
  "code": 200,
  "message": "操作成功",
  "data": [
    {"avatarCode": "AVATAR_001", "displayName": "默认头像一", "resourceKey": "Avatars/avatar_001", "enabled": true}
  ]
}
```

固定映射（路径不含扩展名）：

| avatarCode | 服务端正式路径 | 玩家目录 `resourceKey` |
| --- | --- | --- |
| `AVATAR_001` | `Resources/Avatars/avatar_001` | `Avatars/avatar_001` |

`DEFAULT` 不携带 `avatarCode`；Unity 使用客户端内置的默认 Sprite 常量（默认资源路径由 Unity 项目维护，服务端不下发、不拼接）。

`resourceKey` 是服务端从不可变 `unityResourcePath` 派生的 Resources 相对键，不含 `Resources/` 前缀、文件扩展名或前导斜杠。新 Unity 直接调用 `Resources.Load<Sprite>(resourceKey)`，禁止自行拼接路径；该字段是向后兼容的新增 JSON 字段，旧 Unity 忽略它后可继续使用大小写敏感的 `avatarCode → Sprite` 映射。当前正式目录只有 `AVATAR_001`。后端固定目录是玩家接口和管理端的唯一数据源，但服务端不能读取或实时扫描 Unity 安装包；新增、下架、改名或换图必须同步更新后端目录和 Unity 包，联合验收后随对应版本窗口发布。

### 28.3 选择固定头像

```http
PUT /api/user/avatar/preset
Authorization: Bearer <player-token>
Content-Type: application/json
```

```json
{"avatarCode":"AVATAR_001"}
```

成功响应中的 `avatarUrl` 始终为 `null`：

```json
{
  "code": 200,
  "message": "头像设置成功",
  "data": {
    "avatarUrl": null,
    "avatarSource": "PRESET",
    "avatarCode": "AVATAR_001",
    "avatarVersion": 9,
    "avatarUpdatedTime": "2026-08-27 14:30:00"
  }
}
```

服务端只接受目录中的启用编号。默认每名玩家每天（北京时间）最多切换 5 次，管理端可配置为 1～100 次；恢复默认不占额度。已停用编号不会再被新选择，但已拥有玩家仍可按编号展示本地资源。

### 28.4 恢复默认头像

```http
DELETE /api/user/avatar
Authorization: Bearer <player-token>
```

成功状态为 `avatarSource=DEFAULT`、`avatarCode=null`、`avatarUrl=null`；如果头像状态确实发生变化，`avatarVersion` 在当前值基础上递增 1。重复恢复默认头像仍成功且不无意义递增版本。

### 28.5 好友及公开资料

登录、`GET /api/user/me`、`GET /api/friends`、`GET /api/friends/search` 和好友申请响应直接携带头像编号与版本；`avatarUrl` 始终为 `null`。

需要主动刷新当前好友页时，可批量调用：

```http
POST /api/friends/avatars/batch
Authorization: Bearer <player-token>
Content-Type: application/json
```

`playerIds` 必须包含 1～100 名正式好友；重复 ID 去重并保持首次顺序，禁止按好友逐个请求。

### 28.6 Unity 接入要求

1. 登录后读取目录；请求失败不阻塞进入游戏，使用包内默认图片。
2. 新 Unity 使用目录下发的大小写敏感 `resourceKey` 直接调用 `Resources.Load<Sprite>(resourceKey)`，不自行截取或拼接路径；旧 Unity 可保留已有 `avatarCode` 固定字典。
3. 只有 HTTP 2xx 且业务 `code=200` 才更新本地状态；收到更小的 `avatarVersion` 时丢弃。
4. `PRESET` 按编号加载固定资源，`DEFAULT` 或编号为空加载默认资源。
5. 编号不存在、资源加载失败、头像业务关闭或目录请求失败时回退默认图片。
6. 不实现微信授权按钮、临时文件读取、multipart 上传、远程 URL 下载和对象存储逻辑。
7. 头像设置页可从个人中心再次打开；首次选择引导由 Unity 控制显示频率。
8. `resourceKey` 只是包内定位键，不是图片下载地址。新头像图片必须位于 `Assets/**/Resources/Avatars/` 并随对应 Unity 包发布；后端配置或清单验收不能向旧包补发图片。

### 28.7 错误码

| HTTP | code | 含义 | 客户端处理 |
| ---: | ---: | --- | --- |
| 400 | `400` | 编号为空、格式错误或不存在 | 刷新目录并保留当前头像 |
| 429 | `7306` | 今日切换次数已达上限 | 提示北京时间次日再试 |
| 409 | `7307` | 头像状态并发变化 | 刷新 `/user/me` 后重试 |
| 503 | `7308` | 头像业务已关闭 | 隐藏选择入口并显示默认头像 |
| 401 | `401` | 未登录或令牌过期 | 重新登录 |

当前不再提供旧上传接口对应的 `7301`～`7305` 业务路径。

### 28.8 管理端配置

管理端模块：`AVATAR_POLICY / BUSINESS`，仅 `CONFIG_ADMIN` 可发布。

| 字段 | 说明 |
| --- | --- |
| `enabled` | 头像业务总开关；关闭后不能选择固定头像，但仍可读取目录和恢复默认 |
| `dailyUpdateLimit` | 北京时间每日成功切换次数，范围 1～100 |
| `configVersion` | 系统维护的乐观锁版本，不允许手工修改 |

固定头像目录由管理端打开编辑器时调用以下只读接口动态加载：

```http
GET /api/ops/config/avatar-presets
Authorization: Bearer <operator-token>
```

```json
{
  "code": 200,
  "message": "操作成功",
  "data": [
    {
      "avatarCode": "AVATAR_001",
      "displayName": "默认头像一",
      "unityResourcePath": "Resources/Avatars/avatar_001",
      "resourceKey": "Avatars/avatar_001",
      "enabled": true
    }
  ]
}
```

本节保留 V39/V97～V100 的兼容字段说明；当前固定头像登记、生命周期和资源验收以第 65 章及 V103/V104 规则为准。管理端可通过 `AVATAR_CATALOG / PRESETS` 新增登记和调整状态，但不提供 JSON 文本框、文件上传或手工清单导入。页面只读展示 Unity Editor/可信 CI 自动验收结果；读取失败时显示明确错误，不得用前端旧清单冒充实时结果。“动态加载”不代表服务端能够扫描 Unity 包，资源是否存在仍须在客户端构建中验收。
策略发布成功后由各 API 节点定时刷新本地快照，通常在 30 秒内生效；确认新开关或每日额度生效前不要重复发布同一配置。

### 28.9 数据库迁移与部署

- V97 新增 `users.avatar_code` 和索引。
- V98 建立 `PRESET + avatarCode` 的过渡一致性约束。
- V99 将历史旧来源归一为 `DEFAULT`，清空旧对象键和远程策略字段，并把 `users` 约束收紧为只允许 `DEFAULT/PRESET`；固定 CRC32 为 `67421201`。V100 补齐 `ACCOUNT_CLOSURE` 清理原因、规范固定头像编号并以二进制精确 CHECK 收紧来源/编号合同；固定 CRC32 为 `-880935665`。
- `users.avatar_object_key`、策略表旧字段和清理 Outbox 仅为历史数据兼容保留，当前业务不读取、不写入。

正常固定头像业务完全不需要对象存储、S3 或 CDN。历史对象如需删除，可临时设置 `AVATAR_CLEANUP_ENABLED=true` 并启动清理 Worker；清理完成后恢复 `false`。API 进程默认不加载对象存储。

### 28.10 上线验收

- [ ] Flyway 顺序执行到 V104；V97～V104 在 `flyway_schema_history` 中均为 `success=1`，V103 checksum 为 `-172308135`，V104 checksum 为 `-37724955`。
- [ ] V103 无条件将 `AVATAR_001` 置为 `STAGED/resourceVerified=false/minimumClientVersionCode=0`；V104 后最低客户端版本号为 1。完成目标 Unity 清单验收并由管理员发布 `ACTIVE` 后，`GET /api/user/avatar/presets` 和 `GET /api/ops/config/avatar-presets` 才返回可选项，且编号能在目标 Unity 包中加载。
- [ ] 未登记的 `AVATAR_002` 不能被选择，管理端目录读取失败时不会展示前端静态兜底数据。
- [ ] 选择固定头像返回正确 `avatarCode`、递增版本和 `avatarUrl=null`。
- [ ] 同编号重试幂等，超过每日额度返回 `7306`，关闭业务返回 `7308`。
- [ ] 登录、好友、搜索和申请响应均不返回远程头像地址或对象键。
- [ ] Unity 找不到编号或资源加载失败时显示默认图片且不发起网络下载。
- [ ] 若存在历史对象，清理完成后关闭 `AVATAR_CLEANUP_ENABLED`。

详细字段、测试命令和后端实现见仓库 `docs/avatar-api.md`。

## 29. 金币商城（V40）

### 29.1 最终效果与边界

玩家可以使用金币 `gold` 购买管理端正式发布的以下内容：

- `RESOURCE`：木材、石材、铁等普通资源。
- `PRODUCT`：现有物品目录中的产品。
- `NPC_FRAGMENT`：指定 NPC 的专属碎片。
- `EQUIPMENT`：每件创建一个独立的 1 级、未锁定、仓库状态装备实例。

禁止购买金币、钻石及其他所有 `CURRENCY` 类型物品。支付货币也固定为已启用的 `CURRENCY:gold`，管理端不能改为钻石。每个商品按实际发放数量执行北京时间自然日上限，日界为 UTC+8 的 `00:00:00`。

客户端不能提交 `playerId`、价格或奖励内容。服务端根据登录 token、正式商品和正式配置版本结算。金币扣除、奖励发放、每日用量和不可变订单在同一事务中提交。

### 29.2 查询商城目录

```http
GET /api/coin-shop/catalog
Authorization: Bearer <player-token>
```

响应示例：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "enabled": true,
    "configRevision": 2,
    "usageDate": "2026-07-30",
    "goldItemId": 1,
    "goldItemKey": "gold",
    "goldItemName": "金币",
    "goldItemIcon": "gold",
    "goldBalance": 12000,
    "products": [
      {
        "productId": 1,
        "productCode": "WOOD_PACK",
        "displayName": "木材包",
        "description": "使用金币购买木材",
        "productIcon": "shop_wood_pack",
        "rewardType": "ITEM",
        "rewardItemId": 2,
        "rewardEquipmentId": null,
        "rewardKey": "wood",
        "rewardName": "木材",
        "rewardCategory": "RESOURCE",
        "rewardIcon": "wood",
        "rewardQuantityPerLot": 100,
        "goldPricePerLot": 30,
        "minLotsPerOrder": 1,
        "maxLotsPerOrder": 20,
        "dailyRewardQuantityLimit": 2000,
        "usedRewardQuantity": 300,
        "remainingDailyRewardQuantity": 1700,
        "maxPurchasableLots": 17
      }
    ]
  }
}
```

字段规则：

| 字段 | Unity 用途 |
| --- | --- |
| `enabled` | 全局关闭时禁用整个商城 |
| `configRevision` | 确认购买时原样提交，不能自行加减 |
| `usageDate` | 服务端统一生成的北京时间日期 |
| `goldBalance` | 当前登录玩家金币余额 |
| `rewardType` | `ITEM` 或 `EQUIPMENT` |
| `rewardItemId` | 物品奖励时有值，装备奖励时为 `null` |
| `rewardEquipmentId` | 装备奖励时有值，物品奖励时为 `null` |
| `dailyRewardQuantityLimit` | 每日实际奖励数量上限，不是购买份数 |
| `maxPurchasableLots` | 已综合金币、当日额度、单笔上限和全局开关；为 0 时禁止提交 |

目录只返回已启用且未归档的商品。`productIcon=null` 时 Unity 使用 `rewardIcon` 回退展示。

管理端和玩家界面统一使用“份”：购买 1 份会消耗 `goldPricePerLot` 金币，并获得 `rewardQuantityPerLot` 数量的同一种奖励。JSON 中的 `PerLot`、`Lots` 和 `lotCount` 是为兼容既有接口保留的技术字段名，不表示一次购买多种奖励。

### 29.3 确认购买与幂等重试

```http
POST /api/coin-shop/purchases
Authorization: Bearer <player-token>
Idempotency-Key: 83fa2b54-4b76-42a5-9579-d665b8a3dfa4
Content-Type: application/json
```

```json
{
  "productId": 1,
  "lotCount": 2,
  "expectedConfigRevision": 2
}
```

成功响应：

```json
{
  "code": 200,
  "message": "购买成功",
  "data": {
    "orderId": 9001,
    "orderNo": "f50dd60a-cda5-4a15-b761-bfbc76b4f3af",
    "usageDate": "2026-07-30",
    "productId": 1,
    "productCode": "WOOD_PACK",
    "productName": "木材包",
    "productIcon": "shop_wood_pack",
    "rewardType": "ITEM",
    "rewardItemId": 2,
    "rewardEquipmentId": null,
    "rewardKey": "wood",
    "rewardName": "木材",
    "rewardIcon": "wood",
    "rewardQuantityPerLot": 100,
    "goldPricePerLot": 30,
    "lotCount": 2,
    "chargedGold": 60,
    "grantedQuantity": 200,
    "goldItemId": 1,
    "goldItemKey": "gold",
    "goldBalanceBefore": 12000,
    "goldBalanceAfter": 11940,
    "rewardItemBalanceBefore": 5000,
    "rewardItemBalanceAfter": 5200,
    "configRevision": 2,
    "createTime": "2026-07-30T16:30:00.123456",
    "replayed": false
  }
}
```

装备订单的 `rewardItemBalanceBefore/rewardItemBalanceAfter` 均为 `null`，`grantedQuantity` 是创建的独立装备实例数。

Unity 必须在第一次发请求前持久化：

```text
Idempotency-Key + productId + lotCount + expectedConfigRevision
```

网络超时或没有收到明确结果时，必须复用同一个 UUID 和完全相同的请求体。相同请求重试会返回第一次订单，只有 `replayed=true`；不会再次扣金币、发奖励或占用额度。同一个 UUID 改变任一请求字段会返回 `7408`。

UUID 使用：

```csharp
string idempotencyKey = Guid.NewGuid().ToString("D");
```

不要让 HTTP 重试器每次自动生成新 UUID，也不要把一个固定 UUID 用于多次不同购买。

配置发布后旧请求固定返回 HTTP 409：

```json
{
  "code": 7403,
  "message": "金币商城配置已更新，请刷新后重新确认",
  "data": {
    "latestRevision": 3,
    "refreshRequired": true
  }
}
```

收到 409 后重新拉目录、展示新价格、让玩家重新确认，并为新的业务意图生成新 UUID。服务端不会静默按新价格成交。

### 29.4 查询购买历史

```http
GET /api/coin-shop/purchases/history?beforeId=9001&limit=20
Authorization: Bearer <player-token>
```

`beforeId` 可省略；`limit` 默认 20，范围 1 至 100。响应按订单 ID 倒序：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "items": [
      {
        "orderId": 9000,
        "orderNo": "e6be59a8-aa68-4327-ab93-e6a0e26de6f4",
        "usageDate": "2026-07-30",
        "productId": 1,
        "productCode": "WOOD_PACK",
        "productName": "木材包",
        "productIcon": "shop_wood_pack",
        "rewardType": "ITEM",
        "rewardItemId": 2,
        "rewardEquipmentId": null,
        "rewardKey": "wood",
        "rewardName": "木材",
        "rewardIcon": "wood",
        "lotCount": 2,
        "chargedGold": 60,
        "grantedQuantity": 200,
        "goldBalanceAfter": 11940,
        "rewardItemBalanceAfter": 5200,
        "configRevision": 2,
        "createTime": "2026-07-30T16:30:00.123456"
      }
    ],
    "nextCursor": 9000,
    "hasMore": true
  }
}
```

仅当 `hasMore=true` 时继续携带 `nextCursor` 查询下一页。

### 29.5 Unity DTO 和调用顺序

金额、数量、ID 和版本都必须使用 C# `long`；可能为 JSON `null` 的物品/装备 ID 和物品余额使用 `long?`。不要使用 `int`、`float` 或 `double`。推荐使用 Newtonsoft Json，避免 `JsonUtility` 对可空值和泛型响应的限制。

```csharp
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

[Serializable]
public sealed class ApiResult<T>
{
    [JsonProperty("code")] public int Code;
    [JsonProperty("message")] public string Message;
    [JsonProperty("data")] public T Data;
}

[Serializable]
public sealed class CoinShopPurchaseRequest
{
    [JsonProperty("productId")] public long ProductId;
    [JsonProperty("lotCount")] public long LotCount;
    [JsonProperty("expectedConfigRevision")] public long ExpectedConfigRevision;
}

[Serializable]
public sealed class CoinShopProduct
{
    [JsonProperty("productId")] public long ProductId;
    [JsonProperty("productCode")] public string ProductCode;
    [JsonProperty("displayName")] public string DisplayName;
    [JsonProperty("description")] public string Description;
    [JsonProperty("productIcon")] public string ProductIcon;
    [JsonProperty("rewardType")] public string RewardType;
    [JsonProperty("rewardItemId")] public long? RewardItemId;
    [JsonProperty("rewardEquipmentId")] public long? RewardEquipmentId;
    [JsonProperty("rewardKey")] public string RewardKey;
    [JsonProperty("rewardName")] public string RewardName;
    [JsonProperty("rewardCategory")] public string RewardCategory;
    [JsonProperty("rewardIcon")] public string RewardIcon;
    [JsonProperty("rewardQuantityPerLot")] public long RewardQuantityPerLot;
    [JsonProperty("goldPricePerLot")] public long GoldPricePerLot;
    [JsonProperty("minLotsPerOrder")] public long MinLotsPerOrder;
    [JsonProperty("maxLotsPerOrder")] public long MaxLotsPerOrder;
    [JsonProperty("dailyRewardQuantityLimit")] public long DailyRewardQuantityLimit;
    [JsonProperty("usedRewardQuantity")] public long UsedRewardQuantity;
    [JsonProperty("remainingDailyRewardQuantity")] public long RemainingDailyRewardQuantity;
    [JsonProperty("maxPurchasableLots")] public long MaxPurchasableLots;
}

[Serializable]
public sealed class CoinShopPurchaseResult
{
    [JsonProperty("orderId")] public long OrderId;
    [JsonProperty("orderNo")] public string OrderNo;
    [JsonProperty("productId")] public long ProductId;
    [JsonProperty("rewardType")] public string RewardType;
    [JsonProperty("rewardItemId")] public long? RewardItemId;
    [JsonProperty("rewardEquipmentId")] public long? RewardEquipmentId;
    [JsonProperty("lotCount")] public long LotCount;
    [JsonProperty("chargedGold")] public long ChargedGold;
    [JsonProperty("grantedQuantity")] public long GrantedQuantity;
    [JsonProperty("goldBalanceBefore")] public long GoldBalanceBefore;
    [JsonProperty("goldBalanceAfter")] public long GoldBalanceAfter;
    [JsonProperty("rewardItemBalanceBefore")] public long? RewardItemBalanceBefore;
    [JsonProperty("rewardItemBalanceAfter")] public long? RewardItemBalanceAfter;
    [JsonProperty("configRevision")] public long ConfigRevision;
    [JsonProperty("createTime")] public string CreateTime;
    [JsonProperty("replayed")] public bool Replayed;
}
```

推荐流程：

1. `GET /coin-shop/catalog`，保存目录和 `configRevision`。
2. 限制玩家输入在 `minLotsPerOrder` 至 `maxPurchasableLots` 之间。
3. 确认后生成 UUID，先持久化 UUID 与请求体，再调用购买接口。
4. 同时检查 HTTP 状态和响应体 `code`；`HTTP 200 + code=200` 才是成功。
5. 网络结果不确定时原样重试；`replayed=true` 仍按成功处理。
6. HTTP 409 重新加载并重新确认；余额或额度错误也重新拉目录。
7. 成功后用响应里的余额立即更新界面，并按需刷新背包或装备仓库。

### 29.6 WebSocket 购买通知

首次成交事务提交后，玩家在线会话收到：

```json
{
  "type": "COIN_SHOP_PURCHASED",
  "requestId": null,
  "eventId": "5dac9e88-2ee4-4c0d-8f57-169624208f86",
  "playerId": 1,
  "data": {
    "orderId": 9001,
    "orderNo": "f50dd60a-cda5-4a15-b761-bfbc76b4f3af",
    "goldBalanceAfter": 11940,
    "rewardItemBalanceAfter": 5200,
    "replayed": false
  },
  "serverTime": "2026-07-30T16:30:00.130"
}
```

实际 `data` 是完整 `CoinShopPurchaseResult`。幂等重放不再次推送。该消息用于同账号其他在线界面刷新，不是唯一成交凭证；断线恢复以 HTTP 响应、购买历史和背包接口为准。客户端可按 `eventId` 和 `orderId` 去重。

### 29.7 管理端商品配置

配置中心标识：

```text
moduleCode = COIN_SHOP
configKey  = PRODUCT_CATALOG
```

管理端支持全局启停、商品新增/启停/归档、名称说明、图标、物品或装备奖励、每份获得数量、单份金币价格、单次最少/最多购买份数、北京时间每日实际奖励上限和排序。

只有 `CONFIG_ADMIN` 可以保存草稿、发布、丢弃和创建回滚草稿。发布校验包括：

- 支付固定为 `gold`，所有 `CURRENCY` 奖励都禁止。
- 物品奖励只允许 `RESOURCE`、`PRODUCT`、`NPC_FRAGMENT`。
- 装备固定发 1 级、未锁定、仓库实例。
- 同一种奖励同时只能有一条已启用商品。
- 商品编码及奖励身份发布后不可修改；换奖励必须创建新商品编码。
- 删除正式商品只归档，历史订单不删除。
- 每日上限按实际奖励数量计算，不能小于单笔最大奖励数。
- 无业务变化的重复发布不递增 `configRevision`；真实变化发布后加一。

### 29.8 业务错误码

| code | message | Unity 处理 |
| --- | --- | --- |
| `7401` | 金币商城当前未开放 | 关闭入口并刷新目录 |
| `7402` | 金币商城商品不存在或未启用 | 刷新目录 |
| `7403` | 金币商城配置已更新，请刷新后重新确认 | HTTP 409，刷新并重新确认 |
| `7404` | 购买份数不符合当前规则 | 修正购买份数 |
| `7405` | 金币余额不足 | 刷新余额 |
| `7406` | 该商品今日可购买数量不足 | 刷新当日额度 |
| `7407` | 购买结算后物品余额超出系统上限 | 停止重试并上报 |
| `7408` | 购买幂等请求与首次请求不一致 | 检查本地请求状态，新意图使用新 UUID |
| `7409` | 金币商城正式配置不可用 | 关闭入口并告警 |
| `7410` | 金币或背包余额已变化，请刷新后重试 | 刷新后使用新 UUID 重试 |

退款经济冻结还会返回 `6401`；参数或 UUID 格式错误返回 `400`；未登录返回 HTTP 401。

### 29.9 数据库、事务与并发

V40 新增：

| 表 | 用途 |
| --- | --- |
| `coin_shop_policy` | 全局开关、固定金币物品和正式版本 |
| `coin_shop_product` | 正式商品、启用/归档状态和价格数量规则 |
| `player_coin_shop_daily_usage` | `(player_id, usage_date, product_id)` 唯一的北京时间每日用量 |
| `player_coin_shop_order` | `(player_id, idempotency_key)` 唯一的不可变成功订单快照 |

新购买固定顺序：

```text
users 玩家聚合行锁
  -> 幂等订单查询锁
  -> 活动退款状态 NOWAIT 共享锁
  -> 商城正式配置共享锁
  -> player_item 按 item_id 升序锁定金币行及物品奖励行
  -> 当日用量行锁
  -> 扣金币、写订单、发奖励、增加用量
  -> 提交后推送
```

六个相同 UUID 的并发请求只允许一笔真实成交，其余完整重放；六个不同 UUID 争抢同一每日额度时，只允许额度内订单成功。数据库唯一键、条件更新和事务回滚共同防止重复扣款、超发和部分提交。

### 29.10 上线检查

1. 单节点执行 Flyway 至 V40，确认四张表和约束建立成功。
2. 确认初始 `coin_shop_policy.enabled=false`、版本为 1、支付 Key 为 `gold`。
3. 由 `CONFIG_ADMIN` 创建并发布审核后的商品草稿，不直接在线修改正式表。
4. 验证普通物品、NPC 碎片和装备实例购买，确认钻石与金币奖励无法发布。
5. 验证同键重试、同键异参、配置 409、余额不足、每日额度和并发争抢。
6. 对账金币、奖励、每日用量、订单和 WebSocket 刷新，确认失败事务无残留。

仓库内更完整的字段说明、Unity DTO 和运维约束见 `docs/coin-shop-api.md`。

### 29.11 本次实现验证

验收时间：2026-07-30，时区：Asia/Shanghai。

| 验证项 | 结果 |
| --- | --- |
| 后端全量测试 | 507 项，0 failure，0 error；56 项外部环境用例按条件跳过 |
| 真实 MySQL 配置中心 | 6 项全部通过，空库完成 V1 至 V40 共 40 个迁移 |
| 真实 MySQL 并发与迁移 | 44 项业务并发测试和 1 项 Flyway 升级测试全部通过 |
| 管理端生产构建 | `vue-tsc --noEmit && vite build` 通过，共转换 3505 个模块 |
| 补丁格式 | `git diff --check` 通过，仅存在仓库既有的行尾转换提示 |

---

## 30. 钻石商城（V41/V70/V118/V121）

### 30.1 功能效果

钻石商城是“高级货币购买已发布商品”的白名单商城。普通商品与限时活动抽奖券商品使用同一套玩家接口；玩家只能购买管理端 `DIAMOND_SHOP / PRODUCT_CATALOG` 已发布且启用的商品，后端同时校验配置版本、活动时间、活动专属券和玩家额度。

当前支持：

- 钻石购买金币 `gold`。
- 钻石购买已启用的非货币物品，例如资源、产品、NPC 碎片。
- 钻石购买装备，后端创建独立 1 级仓库装备实例。
- 钻石购买体力，体力不进入背包，按统一体力事务服务直接增加玩家余额。
- 钻石购买限时活动专属抽奖券；同一活动可配置多个价格档位，但跨商品共享每名玩家的活动累计购券上限。
- 管理端配置每份获得数量、单份钻石价格、单次最少/最多购买份数、北京时间每日奖励上限、排序、图标、说明、启停。
- 禁止奖励钻石本身，禁止奖励除金币以外的其他货币。
- 幂等订单、余额快照、每日额度、配置版本冲突 HTTP 409、购买成功 WebSocket 推送。

### 30.2 Unity 玩家接口

```http
GET /api/diamond-shop/catalog
Authorization: Bearer <token>
```

返回字段重点：

- `enabled`：钻石商城是否开放。
- `configRevision`：正式目录版本，购买时必须原样传回。
- `usageDate`：北京时间自然日。
- `diamondBalance`：玩家当前钻石余额。
- `products[]`：已发布且启用的商品。
- `products[].maxPurchasableLots`：当前最多可购买份数，普通商品综合钻石余额和每日额度；活动券商品还会综合活动开放状态及活动累计剩余额度。
- 活动券商品额外返回 `activityCode`、`activityName`、`activityStartTime`、`activityEndTime`、`activityOpen`、`activityRewardQuantityLimit`、`usedActivityRewardQuantity`、`remainingActivityRewardQuantity`；普通商品这些字段全部为 `null`。
- 未开始或已结束的活动商品仍可返回供页面展示，但 `activityOpen=false`、`maxPurchasableLots=0`，Unity 不得自行绕过该状态发起购买。

管理端和玩家界面统一使用“份”：购买 1 份会消耗 `diamondPricePerLot` 钻石，并获得 `rewardQuantityPerLot` 数量的同一种奖励。JSON 中的 `PerLot`、`Lots` 和 `lotCount` 是为兼容既有接口保留的技术字段名，不表示一次购买多种奖励。

```http
POST /api/diamond-shop/purchases
Authorization: Bearer <token>
Idempotency-Key: <标准UUID>
Content-Type: application/json
```

```json
{
  "productId": 1,
  "lotCount": 2,
  "expectedConfigRevision": 3
}
```

成功返回不可变订单快照，包含：

- `chargedDiamond`：实际扣除钻石。
- `grantedQuantity`：实际发放奖励数量。
- `diamondBalanceBefore / diamondBalanceAfter`：扣钻石前后余额。
- `rewardItemBalanceBefore / rewardItemBalanceAfter`：物品奖励前后余额，装备和体力奖励时为 `null`。
- 活动券订单额外返回 `activityCode`、活动名称及起止时间、`activityRewardQuantityLimit`、`remainingActivityRewardQuantityAfter`；历史接口返回同一份不可变成交快照。
- `replayed`：同一幂等键重放时为 `true`。

```http
GET /api/diamond-shop/purchases/history?beforeId=1001&limit=20
Authorization: Bearer <token>
```

购买成功后，在线会话会收到 WebSocket 类型：`DIAMOND_SHOP_PURCHASED`。Unity 仍应以 HTTP 返回、背包查询和历史订单作为恢复依据。若 `rewardType=STAMINA`，不要刷新背包或在本地加体力；应立即调用 `GET /api/idle-adventure/players/{playerId}/stamina` 获取权威余额、超上限状态和恢复倒计时。

### 30.3 配置变化

Unity 如果使用旧目录提交购买，返回 HTTP 409：

```json
{
  "code": 7503,
  "message": "钻石商城配置已更新，请刷新后重新确认",
  "data": {
    "latestRevision": 4,
    "refreshRequired": true
  }
}
```

处理方式：重新调用 `GET /api/diamond-shop/catalog`，刷新 UI 后让玩家重新确认购买。

### 30.4 管理端配置

模块：`DIAMOND_SHOP`

配置键：`PRODUCT_CATALOG`

只有 `CONFIG_ADMIN` 可以保存、发布、回滚。管理端编辑器分为“普通商品”和“活动抽奖券”两栏：普通商品继续选择物品、装备或体力；活动券商品只选择已发布、尚未开始且已绑定专属券的活动，奖励物品由活动自动带入。管理员配置每份券数、单份钻石价格、单次购买份数、每日上限和活动累计上限，不需要手写活动编码或券编码。同一活动多个商品必须使用相同累计上限，活动开始后禁止改绑或修改活动券商品。

示例商品：

```json
{
  "enabled": true,
  "configRevision": 1,
  "diamondItemId": 6,
  "diamondItemKey": "diamond",
  "products": [
    {
      "productId": null,
      "productCode": "DIAMOND_GOLD_10000",
      "activityCode": null,
      "activityTicketItemId": null,
      "activityTicketItemKey": null,
      "displayName": "10000金币",
      "description": "钻石兑换金币",
      "icon": "gold",
      "rewardType": "ITEM",
      "rewardItemId": 1,
      "rewardItemKey": "gold",
      "rewardEquipmentId": null,
      "rewardEquipmentKey": null,
      "rewardQuantityPerLot": 10000,
      "diamondPricePerLot": 10,
      "minLotsPerOrder": 1,
      "maxLotsPerOrder": 10,
      "dailyRewardQuantityLimit": 100000,
      "activityRewardQuantityLimit": null,
      "sortOrder": 10,
      "enabled": true
    }
  ]
}
```

已发布商品的 `productCode` 和奖励身份不能原地替换；需要换奖励对象时，新建商品编码，旧商品停用或归档。

### 30.5 错误码

| code | 含义 |
| --- | --- |
| 7501 | 钻石商城当前未开放 |
| 7502 | 商品不存在或未启用 |
| 7503 | 配置版本变化，Unity 需要重新拉目录 |
| 7504 | 购买份数不符合当前规则 |
| 7505 | 钻石余额不足 |
| 7506 | 当日额度不足 |
| 7507 | 结算后物品余额超上限 |
| 7508 | 幂等请求与首次请求不一致 |
| 7509 | 正式配置不可用 |
| 7510 | 钻石或背包余额并发变化 |
| 7511 | 本活动跨商品共享的剩余可购抽奖券数量不足 |
| 6512 | 购买后体力将超过物理上限，体力、钻石、订单和每日额度全部回滚 |

### 30.6 本次验证

- `mvn -q test`：通过。
- `RUN_MYSQL_INTEGRATION_TESTS=true` 的 `FlywayMigrationIntegrationTest`：通过，空库可从 V1 顺序执行至 V70，第二次迁移为 0。
- V70 固定 CRC32 为 `1183825380`；V71 固定 CRC32 为 `1008907161`。V1-V73 已冻结，下一份迁移使用 V74。


## 31. 物品基础资料配置（V42）

### 31.1 功能效果

管理端新增 `ITEM_CATALOG / ITEMS` 配置模块，用于维护 `item_config` 物品基础资料。运营可以新增货币、资源、产物、NPC 碎片、活动抽奖券等基础物品，也可以修改展示名称、图标、说明、排序和启停状态。已发布物品不能删除，只能停用。活动抽奖券选择 `CARD_DRAW_TICKET` 类型，启用并发布后，返回限定卡池重新读取配置，即可在“活动抽奖券（通用）”下拉框中选择；此绑定用于展示和发放快照，消费不受来源活动限制。

新增物品的 `itemId` 不由运营填写，后端通过 `config_asset_id_sequence` 在发布事务内串行分配，默认从 `200000` 以上开始，避免和历史迁移写死的物品 ID 冲突。

### 31.2 管理端配置

配置中心标识：

```text
moduleCode = ITEM_CATALOG
configKey  = ITEMS
```

该模块属于高风险经济配置，`adminOnlyWrite=true`：`CONFIG_VIEWER` 和 `CONFIG_EDITOR` 可以读取，只有 `CONFIG_ADMIN` 可以保存草稿、发布、丢弃或创建回滚草稿。

管理端提供表格化编辑器：

- 新增物品：填写 `itemKey`、名称、类型、图标、说明、排序和启停。
- 修改展示：名称、图标、说明、排序、启停可改。
- 删除规则：新草稿里尚未发布的物品可以删除；已发布物品只能停用。
- 引用展示：展开物品行可查看被玩家背包、商城、资源出售、签到、抽卡、系统奖励、邮件附件、好友赠礼、广告奖励、充值商品、NPC 碎片和放置冒险等模块引用的数量。

### 31.3 字段规则

| 字段 | 说明 | 修改规则 |
| --- | --- | --- |
| `itemId` | 数据库主键 | 后端分配，运营不填写 |
| `itemKey` | 稳定业务编码，如 `wood`、`magic_dust`、`npc_fragment_mage` | 被引用后禁止修改 |
| `name` | 展示名称 | 可修改 |
| `itemType` | `CURRENCY`、`RESOURCE`、`PRODUCT`、`NPC_FRAGMENT`、`CARD_DRAW_TICKET` | 对应货币、资源、产物、NPC 碎片、活动抽奖券；被引用后禁止修改 |
| `icon` | Unity 图标资源 Key | 可修改 |
| `description` | 说明 | 可修改 |
| `sortOrder` | 管理端和字典排序 | 可修改 |
| `enabled` | 是否可被新配置引用 | 可修改，停用不影响历史数据 |
| `createdByType` | `MANUAL` 或 `SYSTEM` | 只读；运营新增默认为 `MANUAL` |

### 31.4 Unity 调用说明

Unity 不直接调用 `ITEM_CATALOG` 运营接口。客户端仍通过各业务接口获取可购买、可出售、可奖励的正式目录，例如金币商城、钻石商城、资源出售、签到和背包接口。

Unity 需要稳定使用：

- `itemId`：服务端结算和背包主键。
- `itemKey`：客户端资源映射和埋点识别。
- `name / icon / description`：展示文本和图标。
- `itemType`：区分货币、资源、产物、NPC 碎片。

当管理端修改名称或图标后，Unity 下次拉取对应业务目录或背包列表即可展示新内容；历史订单和历史邮件仍以创建时快照为准。

### 31.5 本次验证

- `mvn -q test`：通过。
- `npm.cmd run build`：通过。
- `git diff --check`：通过，仅有 Windows 行尾转换提示。
## 32. 装备基础资料配置（V43）

### 32.1 功能效果

管理端新增 `EQUIPMENT_CATALOG / EQUIPMENTS` 配置模块，用于维护 `equipment_config` 装备基础资料。运营可以新增自己想要的装备，也可以修改名称、图标、模型、说明、扩展属性、金币曲线、最高等级、排序和启停状态。已发布装备不能删除，只能停用。

新增装备的 `equipmentId` 不由运营填写，后端通过 `config_asset_id_sequence` 在发布事务内串行分配，装备 ID 固定从 `300000` 以上开始；物品为 `200000+`，后续 NPC 为 `400000+`，方便排查和避免跨类型冲突。

新增装备发布时会自动初始化成长数据：

- 自动绑定 `EQUIPMENT_COMMON_V1` 金币曲线；如果不存在，则使用第一个启用的 `EQUIPMENT` 曲线；没有任何可用曲线时阻断发布。
- 自动生成 1 到 `maxLevel` 的装备等级属性，默认生命 +10、攻击 +1、防御 +1、速度 +0。
- 默认生成 `maxLevel` 范围内 20/40/60/80/100 的突破模板，同款 1 级装备消耗默认随突破点递增；发布后可在 `EQUIPMENT_GROWTH` 中增删改任意 2 到最高等级内的突破点。后续修改装备等级上限不会恢复运营已删除的突破点。

### 32.2 管理端配置

配置中心标识：

```text
moduleCode = EQUIPMENT_CATALOG
configKey  = EQUIPMENTS
```

该模块属于高风险经济和战斗配置，`adminOnlyWrite=true`：`CONFIG_VIEWER` 和 `CONFIG_EDITOR` 可以读取，只有 `CONFIG_ADMIN` 可以保存草稿、发布、丢弃或创建回滚草稿。

管理端提供表格化编辑器：

- 新增装备：填写 `equipmentKey`、名称、部位、品质、图标、模型、说明、扩展属性、金币曲线、最高等级、排序和启停。
- 修改展示：名称、图标、模型、说明、扩展属性、排序、启停可改。
- 删除规则：新草稿里尚未发布的装备可以删除；已发布装备只能停用。
- 引用展示：展开装备行可查看被玩家装备实例、NPC 穿戴、装备升级、签到、抽卡、金币商城和钻石商城引用的数量。
- 锁定规则：装备被引用后，`equipmentKey`、`equipmentType`、`quality` 禁止修改；需要新装备时只能使用正式五部位 `WEAPON / HELMET / ARMOR / BOOTS / GLOVES`。`RING / ACCESSORY / NECKLACE` 已由 V137 清理，服务端和管理端均拒绝废弃或自定义部位。

### 32.3 字段规则

| 字段 | 说明 | 修改规则 |
| --- | --- | --- |
| `equipmentId` | 数据库主键 | 后端分配，运营不填写 |
| `equipmentKey` | 稳定业务编码，如 `moon_blade` | 被引用后禁止修改 |
| `name` | 展示名称 | 可修改 |
| `equipmentType` | 正式部位编码：`WEAPON`、`HELMET`、`ARMOR`、`BOOTS`、`GLOVES` | 被引用后禁止修改 |
| `quality` | 固定五档：`COMMON`、`FINE`、`EXCELLENT`、`EPIC`、`LEGENDARY`；中文为普通、优秀、稀有、史诗、传说 | 被引用后禁止修改 |
| `icon` | Unity 图标资源 Key | 可修改 |
| `modelKey` | Unity 模型资源 Key | 可修改 |
| `description` | 装备说明 | 可修改 |
| `attributesJson` | 扩展属性 JSON 对象 | 可修改，会影响实时读取配置的展示/战斗 |
| `goldCurveCode` | 装备升级金币曲线编码 | 可修改，曲线必须启用且覆盖 `maxLevel` |
| `maxLevel` | 装备最高等级，1 到 100 | 可修改，但不能低于玩家已拥有该装备的最高等级 |
| `sortOrder` | 管理端和字典排序 | 可修改 |
| `enabled` | 是否可被新配置引用和发放 | 可修改，停用不删除玩家已有装备 |
| `createdByType` | `MANUAL` 或 `SYSTEM` | 只读；运营新增默认为 `MANUAL` |

### 32.4 与装备成长页的关系

`EQUIPMENT_CATALOG` 负责新增装备和基础资料边界；`EQUIPMENT_GROWTH` 负责已有装备的逐级属性、突破材料和成长细节。成长页现在禁止修改装备品质和启停状态，这两个字段统一由基础资料页管理。

当前 `player_equipment` 没有创建时属性快照，仓库、穿戴和战斗计算会实时关联 `equipment_config` 与成长配置。因此修改 `attributesJson` 或成长属性会影响已有玩家装备；运营发布前应查看引用范围并确认影响。

### 32.5 Unity 调用说明

Unity 不直接调用 `EQUIPMENT_CATALOG` 运营接口。客户端仍通过业务接口获取装备信息，例如金币商城、钻石商城、抽卡、签到、背包仓库和 NPC 穿戴接口。

Unity 需要稳定使用：

- `equipmentId`：服务端装备模板主键。
- `equipmentKey`：客户端资源映射和埋点识别。
- `equipmentType`：部位和穿戴槽位判断。
- `quality`：品质展示和筛选。
- `name / icon / modelKey / description`：展示文本、图标和模型资源。
- `maxLevel`：升级 UI 上限。

当管理端修改名称、图标或模型后，Unity 下次拉取对应业务目录或仓库列表即可展示新内容。已创建的商城订单、邮件或奖励流水仍以创建时快照为准。

### 32.6 本次验证

- `mvn -q test`：通过。
- `npm.cmd run build`：通过。
- `git diff --check`：通过，仅有 Windows 行尾转换提示。

## 33. NPC 基础资料配置（V44）

### 33.1 功能效果

管理端新增 `NPC_CATALOG / NPCS` 配置模块。运营可以新增 NPC，维护名称、头像绑定键、角色立绘绑定键、Q版绑定键、说明、战斗属性、固定建筑增产比例、专属碎片、重复转换数量、金币曲线、最高等级、排序以及运行/获取状态，不再需要手写 NPC 新增 SQL。角色立绘字段已由 V136 前向补充，存量允许为空。

新增 NPC 的 `npcId` 由服务端通过 `config_asset_id_sequence` 串行分配，固定从 `400000` 段开始。专属碎片支持两种模式：

- `AUTO`：自动创建 `npc_fragment_{npcCode}`，物品 ID 从 `200000` 段分配，类型固定为 `NPC_FRAGMENT`，`createdByType=SYSTEM`。
- `EXISTING`：绑定物品基础资料中已经启用的 `NPC_FRAGMENT`，且同一种碎片只能归属一个 NPC。

新增 NPC 会自动生成成长数据：

- 1 到 `maxLevel` 的战斗属性，Lv.1 默认为生命 100、攻击 10、防御 5、速度 100，后续每级分别增加 10/1/1/1。
- 默认生成 `maxLevel` 范围内 20/40/60/80/100 的突破模板，消耗本 NPC 专属碎片 1/2/3/4/5 个；发布后可在 `NPC_GROWTH` 中增删改任意 2 到最高等级内的突破点。后续修改 NPC 等级上限或专属碎片只同步现有配置，不会恢复或重新启用运营已删除的突破点。
- 金币曲线优先使用填写的 NPC 曲线；留空时优先选择 `NPC_COMMON_V1`，没有可用 NPC 曲线时拒绝发布。

### 33.2 生命周期

`enabled` 和 `obtainable` 语义不同：

| 字段 | 含义 |
| --- | --- |
| `enabled` | 已有 NPC 是否允许运行、升级、入驻和出现在图鉴中 |
| `obtainable` | 抽卡、签到和管理端新奖励配置是否允许继续发放该 NPC |

新 NPC 首次发布必须为 `enabled=true, obtainable=false`。系统完成默认成长初始化后，运营应进入 `NPC_GROWTH` 检查逐级属性和突破消耗，再回到基础资料页开放获取。后端在 `false -> true` 时会再次校验 1 到最高等级的属性、已启用突破点、专属碎片和金币曲线，任一已启用突破配置无效都会拒绝发布。

停获推荐顺序：先从活动卡池和签到配置移除 NPC 并发布，再设置 `obtainable=false`。如果先停获，仍引用该 NPC 的活动配置会被运行时判定为无效，从而拒绝整次抽卡或领取，避免继续错误发放。

玩家已经拥有 NPC 后，`combatAttribute` 和 `produceBonusRate` 会被锁定，避免实时战斗与生产语义突然变化。`maxLevel` 不得低于玩家已拥有的最高等级；稳定 `npcCode` 永久不可修改；专属碎片被玩家背包、商城、奖励、签到、抽卡等外部模块引用后不能更换。已发布 NPC 不能删除，只能按引用保护规则停获或停用。

### 33.3 管理端接口

配置中心标识：

```text
moduleCode = NPC_CATALOG
configKey  = NPCS
```

读取、校验、草稿、发布、历史和回滚继续使用第 17 章通用配置接口。NPC 引用统计使用独立只读接口，不进入草稿指纹：

```http
GET /api/ops/config/asset-references/npcs
Authorization: Bearer <operator-token>
```

响应示例：

```json
[
  {
    "npcId": 3001,
    "npcCode": "field_steward",
    "npcReferenceCount": 8,
    "fragmentReferenceCount": 3,
    "references": [
      {
        "referenceScope": "NPC",
        "moduleCode": "PLAYER_NPC",
        "moduleName": "玩家拥有",
        "referenceCount": 8
      },
      {
        "referenceScope": "FRAGMENT",
        "moduleCode": "COIN_SHOP",
        "moduleName": "金币商城商品",
        "referenceCount": 3
      }
    ]
  }
]
```

发布锁顺序固定为：配置中心全局发布锁 -> `item_config` -> `npc_config` -> NPC 成长数据。自动碎片、NPC 主记录、逐级属性、突破配置、正式版本和审计在同一个事务中提交，任何写入失败都会整体回滚。

### 33.4 配置字段

| 字段 | 说明 | 规则 |
| --- | --- | --- |
| `npcId` | NPC 模板 ID | 新增时为 `null`，发布后由服务端分配 `400000+` |
| `npcCode` | 稳定业务编码 | 小写字母开头，只能含小写字母、数字、下划线；发布后不可改 |
| `name` | NPC 名称 | 可修改 |
| `icon` | Unity 图标资源 Key | 可修改 |
| `portraitKey` | Unity 角色立绘绑定键 | 可修改；存量允许为空，不填写 `Assets/...` 物理路径 |
| `modelKey` | Unity 模型资源 Key | 可修改 |
| `description` | NPC 展示说明 | 可修改 |
| `combatAttribute` | 战斗属性 | `PHYSICAL/FIRE/ICE/ROCK/WIND/POISON/STAR/DARK`；玩家拥有后不可改 |
| `fragmentMode` | 专属碎片模式 | `AUTO` 或 `EXISTING` |
| `fragmentItemKey` | 专属碎片编码 | 必须是启用的 `NPC_FRAGMENT`；外部引用后不可换 |
| `duplicateFragmentAmount` | 重复获得时转换数量 | 1 到 1,000,000,000 |
| `goldCurveCode` | NPC 金币曲线编码 | 必须启用、类型为 `NPC` 且覆盖 `maxLevel` |
| `maxLevel` | 等级上限 | 1 到 100，不得低于玩家现有最高等级 |
| `produceBonusRate` | 固定建筑增产比例 | 0 到 100，最多 4 位小数；玩家拥有后不可改 |
| `sortOrder` | 图鉴和管理端排序 | 0 到 1,000,000 |
| `enabled` | 已有 NPC 运行状态 | 有玩家或业务直接引用时不能关闭 |
| `obtainable` | 新发放状态 | 可独立关闭；开启时必须同时 `enabled=true` 且成长完整 |
| `createdByType` | 创建来源 | 只读，运营新增固定为 `MANUAL` |

### 33.5 Unity 调用说明

Unity 不调用 `/api/ops/**` 管理接口。NPC 基础资料发布后，客户端继续调用玩家业务接口：

- `GET /api/npc/config/list`：返回图鉴配置，并在 V136 返回 `icon/portraitKey/modelKey` 三类表现键。
- `GET /api/npc/list/{playerId}`：返回玩家拥有 NPC，并在 V136 返回 `icon/portraitKey/modelKey` 三类表现键。
- `GET /api/npc/available/{playerId}`：可入驻列表同步返回三类表现键。
- `POST /api/npc/upgrade`：升级结果新增 `combatAttribute`。
- 建筑生产响应中的 `settledNpc`：同步返回 `icon/portraitKey/modelKey` 三类表现键。

Unity 应使用 `maxLevel` 控制升级按钮，使用 `combatAttribute` 显示属性图标，使用 `description` 展示详情。`obtainable=false` 只表示不能再从新渠道获得，不能据此隐藏或禁用玩家已经拥有的 NPC。

### 33.6 本次验证

- `mvn -q test`：全量测试通过。
- `RUN_MYSQL_INTEGRATION_TESTS=true`：MySQL 8.0 临时库从 V1 顺序迁移至 V44 通过，迁移后结构、约束、索引和 ID 分段断言通过。
- `npm.cmd run build`：Vue TypeScript 检查和 Vite 生产构建通过。

<!-- unified-asset-reward:start -->
## 34. 统一资产引用、奖励快照与管理端接入（V45-V46）
本文档说明 V45、V46 后商城、抽卡、签到、系统奖励、邮件和资源出售对物品、装备、NPC 的统一引用方式，并给出 Unity 与运营管理端的实际接入约定。

基础地址：`http://localhost:8080/api`

### 一、能力矩阵

| 模块 | ITEM | EQUIPMENT | NPC | 说明 |
| --- | ---: | ---: | ---: | --- |
| 抽卡 | 是 | 是 | 是 | 重复 NPC 自动转换为该 NPC 的专属碎片物品 |
| 签到 | 是 | 是 | 是 | NPC 可配置“已拥有时转物品”回退奖励 |
| 系统奖励模板与奖励邮件 | 是 | 是 | 是 | NPC 数量固定为 1，重复时按发布快照转专属碎片 |
| 玩家邮件附件 | 是 | 是 | 是 | 已投递权益不因基础资料后来停用而失效 |
| 金币商城 | 是 | 是 | 否 | NPC 碎片按 `ITEM / NPC_FRAGMENT` 配置 |
| 钻石商城 | 是 | 是 | 否 | NPC 碎片按 `ITEM / NPC_FRAGMENT` 配置 |
| 资源出售 | 是 | 否 | 否 | 来源支持 `RESOURCE / PRODUCT / NPC_FRAGMENT`，目标为货币或资源；NPC 碎片合同发布后冻结 |

没有配置到对应业务模块的资产不能被购买、抽取、签到领取或发送。服务端在发布时重新读取基础资料并校验，不能通过管理端请求伪造名称、图标、分类或 NPC 重复补偿规则。

### 二、统一资产字段

统一资产身份由以下字段共同表达：

| 字段 | 含义 |
| --- | --- |
| `rewardType` | `ITEM`、`EQUIPMENT` 或 `NPC` |
| `assetId` | 数据库稳定主键；必须按 `rewardType` 解释 |
| `rewardKey` | 稳定业务编码，如 `wood`、`iron_sword`、`sword_master` |
| `rewardName` | 发布或业务发生时冻结的展示名称 |
| `rewardIcon` | 发布或业务发生时冻结的 Unity 图标资源键；允许为 `null` |
| `rewardCategory` | 物品类型、装备部位或固定值 `NPC` |
| `rewardAmount` | 发放数量；NPC 奖励固定为 1 |

同一个数字 ID 在不同资产表中可能重复，因此 Unity 不能只按 `assetId` 判断类型。缓存键、去重键和埋点键应使用 `rewardType + ":" + assetId`。

不同接口为了表达业务语义会给字段加前缀：

| 业务语义 | 类型字段 | ID 字段 |
| --- | --- | --- |
| 抽卡/签到配置奖励 | `configuredRewardType` | `configuredAssetId` |
| 抽卡/签到实际奖励 | `grantedRewardType` | `grantedAssetId` |
| 抽卡当前卡位 | `rewardType` | `rewardAssetId` |
| 签到奖励预览 | `rewardType` | `rewardAssetId` |
| 邮件承诺附件 | `attachmentType` | `assetId` |
| 邮件实际领取 | `grantedRewardType` | `grantedAssetId` |

### 三、两层快照语义

系统保存两种不同快照，客户端必须区分：

1. **发布快照**：运营发布时冻结资产 ID、编码、名称、图标和分类。目录、卡池、签到预览和邮件未领取附件展示这一层。
2. **实际发放快照**：玩家发生购买、抽卡、签到或邮件领取时冻结最终到账内容。历史订单和已领取结果展示这一层。

当 NPC 已拥有而转为碎片时，配置快照仍是 NPC，实际发放快照是 `ITEM`。因此结算动画、背包刷新和到账埋点必须使用 `granted*` 字段，不能继续使用 `configured*` 字段。

基础资料后来改名、换图标或停用时：

- 历史订单、历史抽卡结果、签到流水和邮件附件继续显示原快照，不联表读取当前名称。
- 管理端编辑旧正式配置时保留只读快照，并标记“已停用或不可用”，不会把选项显示为空白。
- 新草稿不能重新选择已停用资产；发布时后端再次校验正式基础资料。
- 已经投递到玩家邮箱的附件属于已承诺权益，领取时允许使用承诺快照完成发放。
- 普通运行配置发现资产引用损坏或当前不可发放时会失败并告警，不会静默换成另一项资产。

### 四、玩家接口

所有玩家接口都必须携带：

```http
Authorization: Bearer <player-token>
```

统一响应外层为：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {}
}
```

#### 4.1 抽卡

查询当前卡池：

```http
GET /api/card-draw/current
```

每个 `cards[]` 现在返回：

```json
{
  "slotNo": 1,
  "rewardType": "NPC",
  "rewardKey": "sword_master",
  "rewardAssetId": 400001,
  "rewardName": "剑圣",
  "icon": "npc/sword_master",
  "rewardCategory": "NPC",
  "rewardAmount": 1,
  "weight": 1000000,
  "probability": 0.01
}
```

执行抽卡：

```http
POST /api/card-draw/draw
Idempotency-Key: 78f35dd7-1e7f-4275-963c-4ecbdfebda43
Content-Type: application/json

{
  "optionCode": "FIVE"
}
```

每个 `results[]` 同时返回配置奖励与实际奖励：

```json
{
  "drawResultId": 91001,
  "drawSequence": 2,
  "winningSlot": 1,
  "configuredRewardType": "NPC",
  "configuredRewardKey": "sword_master",
  "configuredAssetId": 400001,
  "configuredRewardName": "剑圣",
  "configuredRewardIcon": "npc/sword_master",
  "configuredRewardCategory": "NPC",
  "configuredRewardAmount": 1,
  "grantedRewardType": "ITEM",
  "grantedRewardKey": "npc_fragment_sword_master",
  "grantedAssetId": 200015,
  "grantedRewardName": "剑圣碎片",
  "grantedRewardIcon": "item/npc_fragment_sword_master",
  "grantedRewardCategory": "NPC_FRAGMENT",
  "grantedRewardAmount": 5,
  "duplicateNpcConverted": true
}
```

相同 `Idempotency-Key` 和相同档位重试会重放第一次完整批次，不重复扣钻石或发奖。

#### 4.2 签到

```http
GET /api/sign-in/overview
POST /api/sign-in/claim
POST /api/sign-in/makeup
```

普通领取请求：

```json
{
  "planCode": "DEFAULT"
}
```

补签请求额外携带 `rewardDay`。概览中的 `days[].rewards[]` 返回 `rewardType`、`rewardKey`、`rewardAssetId`、`rewardName`、`rewardIcon`、`rewardCategory`、`rewardAmount`，并在配置了 NPC 回退时返回同结构的 `fallback*` 字段。

领取结果中的 `rewards[]` 返回配置与实际两组字段：

```json
{
  "rewardSeq": 1,
  "configuredRewardType": "NPC",
  "configuredRewardKey": "sword_master",
  "configuredAssetId": 400001,
  "configuredRewardName": "剑圣",
  "configuredRewardIcon": "npc/sword_master",
  "configuredRewardCategory": "NPC",
  "configuredRewardAmount": 1,
  "grantedRewardType": "ITEM",
  "grantedRewardKey": "npc_fragment_sword_master",
  "grantedAssetId": 200015,
  "grantedRewardName": "剑圣碎片",
  "grantedRewardIcon": "item/npc_fragment_sword_master",
  "grantedRewardCategory": "NPC_FRAGMENT",
  "grantedRewardAmount": 5,
  "fallbackApplied": true
}
```

`cost` 返回补签实际消耗物品的 `itemId`、`itemKey`、`itemName`、`icon`、`itemCategory` 和 `amount` 快照。

#### 4.3 邮件

```http
GET /api/mail/list/{playerId}?limit=50
GET /api/mail/detail/{playerId}/{mailId}
POST /api/mail/claim
POST /api/mail/claim_all
```

未领取附件示例：

```json
{
  "attachmentId": 7001,
  "attachmentType": "NPC",
  "assetId": 400001,
  "rewardKey": "sword_master",
  "rewardName": "剑圣",
  "rewardCategory": "NPC",
  "rewardIcon": "npc/sword_master",
  "itemId": null,
  "itemKey": null,
  "name": "剑圣",
  "itemType": null,
  "icon": "npc/sword_master",
  "quantity": 1,
  "grantedRewardType": null,
  "grantedAssetId": null,
  "grantedQuantity": null,
  "fallbackApplied": false,
  "claimedTime": null
}
```

领取后，同一附件补齐 `grantedRewardType`、`grantedAssetId`、`grantedRewardKey`、`grantedRewardName`、`grantedRewardCategory`、`grantedRewardIcon`、`grantedQuantity`、`fallbackApplied` 和 `claimedTime`。重复领取已成功邮件时返回相同实际发放快照，不再次发放。

`itemId`、`itemKey`、`name`、`itemType`、`icon` 是旧 Unity 兼容字段。新客户端必须优先使用统一字段；非 ITEM 附件的 `itemId/itemKey/itemType` 为 `null`。

#### 4.4 商城与资源出售

- 金币商城完整契约：[`coin-shop-api.md`](coin-shop-api.md)
- 钻石商城完整契约：[`diamond-shop-api.md`](diamond-shop-api.md)
- 资源出售完整契约：[`resource-sale-api.md`](resource-sale-api.md)

三类历史接口都返回业务发生时的名称、图标和分类快照，不受后来基础资料修改影响。

### 五、管理端

管理端启动后访问 `http://localhost:5173`：

- `/configs`：物品、装备、NPC 基础资料，以及抽卡、签到、金币商城、钻石商城、资源出售和系统奖励模板。
- `/system-rewards`：选择已发布奖励模板，创建立即或定时发送的奖励邮件活动。
- `/system-mails`：发送普通无附件系统邮件；它与奖励邮件分开，避免误发资产。

#### 5.1 基础字典

```http
GET /api/ops/config/dictionaries
Authorization: Bearer <operator-token>
```

响应包含 `items`、`equipment`、`npcs` 三组可选项，每项结构为：

```json
{
  "id": 400001,
  "key": "sword_master",
  "name": "剑圣",
  "type": "NPC",
  "icon": "npc/sword_master"
}
```

编辑器统一提交“类型 + 资产 ID + 稳定编码”。名称、图标、分类快照只用于展示和冲突恢复；后端发布时会重新生成正式值。

#### 5.2 配置模块

| 页面模块 | 配置键 | 可选资产 |
| --- | --- | --- |
| `CARD_DRAW` | `STANDARD_POOL`、`LIMITED_POOL` | ITEM / EQUIPMENT / NPC |
| `SIGN_IN` | `NEWBIE:DEFAULT`、系统派生的 `ACTIVITY_<hash>:DEFAULT` 或月份键 | ITEM / EQUIPMENT / NPC |
| `SYSTEM_REWARD` | `PACKAGES` | ITEM / EQUIPMENT / NPC |
| `COIN_SHOP` | `PRODUCT_CATALOG` | ITEM / EQUIPMENT |
| `DIAMOND_SHOP` | `PRODUCT_CATALOG` | ITEM / EQUIPMENT |
| `RESOURCE_SALE` | `EXCHANGE_OPTIONS` | ITEM |

所有模块继续使用配置中心通用流程：读取正式配置、校验、保存草稿、发布、查看历史、创建回滚草稿。接口清单和权限见 [`operator-config-center.md`](operator-config-center.md)。

#### 5.3 系统奖励发送

查询可用模板：

```http
GET /api/ops/system-rewards/packages
Authorization: Bearer <operator-token>
```

每个 `items[]` 返回 `rewardType`、`assetId`、`rewardKey`、展示快照和 NPC 重复碎片快照。创建活动：

```http
POST /api/ops/system-rewards/campaigns
Authorization: Bearer <operator-token>
Idempotency-Key: reward:550e8400-e29b-41d4-a716-446655440000
Content-Type: application/json
```

```json
{
  "packageCode": "SUMMER_REWARD",
  "title": "夏日活动奖励",
  "content": "请在有效期内领取。",
  "senderName": "系统",
  "targetType": "PLAYER_IDS",
  "playerIds": [1, 2],
  "scheduledTime": "2026-07-30 18:00:00",
  "mailExpireTime": "2026-08-06 23:59:59",
  "reason": "夏日活动第一日奖励"
}
```

活动创建时再次冻结模板版本和全部资产快照；之后修改模板不会改变已经创建的活动或已投递邮件。

### 六、Unity DTO 建议

```csharp
using Newtonsoft.Json;

[System.Serializable]
public sealed class AssetSnapshotDto
{
    [JsonProperty("rewardType")] public string RewardType;
    [JsonProperty("assetId")] public long AssetId;
    [JsonProperty("rewardKey")] public string RewardKey;
    [JsonProperty("rewardName")] public string RewardName;
    [JsonProperty("rewardIcon")] public string RewardIcon;
    [JsonProperty("rewardCategory")] public string RewardCategory;
    [JsonProperty("rewardAmount")] public long RewardAmount;
}
```

具体接口中字段带有 `configured`、`granted`、`attachment` 等前缀，建议分别建立 DTO，不建议对原始 JSON 做字符串替换。客户端处理顺序：

1. 先按 `rewardType` 选择背包、装备仓库或 NPC 图鉴界面。
2. 结算结果优先读取 `granted*`；预览和未领取邮件读取配置/承诺字段。
3. 图标为空时使用对应类型的 Unity 默认图标。
4. 未识别的新 `rewardType` 不崩溃，展示通用奖励占位并上报日志。
5. 订单、抽卡批次和邮件领取恢复以 HTTP 结果及历史数据为准，不以动画或 WebSocket 是否播放为准。

### 七、迁移与兼容

- V45 为抽卡、签到、金币商城、钻石商城和资源出售补齐稳定资产 ID 与展示快照。
- V46 让系统奖励模板、投递活动和玩家邮件附件支持 ITEM / EQUIPMENT / NPC。
- V47 为 V46 发布前已经领取的旧 ITEM 邮件补齐实际发放快照，不覆盖新类型奖励。
- V48 增加客户端运行策略、通知、经济护栏、头像动态策略和内容模板。
- V49 增加客户端版本上报模式、稳定阻断原因及 JSON/状态/表注释数据库约束。
- V50 删除两组经 Mapper、EXPLAIN 和索引统计确认的完全重复索引，不改变接口。
- 当前 V1 至 V73 一旦由 Flyway 成功执行都不得修改；下一次结构变化从 V74 新建迁移。
- 旧 ITEM 邮件字段继续返回，给旧 Unity 留出升级窗口；新功能不应继续依赖兼容字段。

### 八、验收结果

验证日期：2026-07-31（Asia/Shanghai）。

| 验证项 | 结果 |
| --- | --- |
| 后端全量测试 | 562 项，0 failure，0 error；默认环境下 63 项外部依赖用例按条件跳过，MySQL 专项另行启用并通过 |
| MySQL 8 Flyway | 1 项通过，空库按顺序验证并执行 V1 至 V50 共 50 个迁移 |
| MySQL 8 配置中心 | 7 项全部通过，覆盖发布事务、审计回滚和基础资料联动 |
| 管理端生产构建 | `vue-tsc --noEmit && vite build` 通过，共转换 3540 个模块 |
| 文档示例 | 项目文档与桌面统一资产章节的全部 JSON 示例均通过解析校验 |
<!-- unified-asset-reward:end -->

## 35. 运行策略与运营内容管理（V48-V49）

### 35.1 最终效果

V48 完成七项运营能力，V49 补强客户端版本上报和阻断语义：

| 能力 | 最终效果 |
| --- | --- |
| 激励广告奖励 | 管理端配置场景、广告位、奖励物品、数量、次数、冷却和观看时间 |
| 客户端版本与维护 | Unity/微信小游戏启动查询版本策略，登录入口再次强制校验 |
| 通知提醒策略 | 管理站内通知、微信订阅消息、资源提醒和建筑完成提醒业务参数 |
| 经济安全阈值 | 限制系统奖励单次数量、活动人数和每日金币/钻石投放 |
| 头像业务策略 | 管理固定头像启停、每日切换次数和配置版本（目录只读） |
| 公告/邮件模板 | 公告、普通邮件和奖励邮件可选择模板填充表单 |
| 统一活动日历 | 汇总正式配置，展示生效状态、即将到期和时间冲突 |

六个可写模块统一走配置中心的“读取正式值 -> 校验 -> 保存草稿 -> 发布 -> 历史/回滚 -> 审计”流程。只有 `CONFIG_ADMIN` 可以写入，其他配置角色只读。

### 35.2 客户端启动策略

```http
GET /api/client/runtime-policy?platform=UNITY&channel=DEFAULT&versionCode=10001
```

是否需要 token：否。

参数：

| 字段 | 必填 | 说明 |
| --- | --- | --- |
| `platform` | 是 | `UNITY` 或 `WECHAT_MINIGAME` |
| `channel` | 否 | 默认 `DEFAULT`，使用大写稳定编码 |
| `versionCode` | 正式客户端必须传 | 随客户端版本单调递增的正整数 |

响应示例：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "configured": true,
    "platform": "UNITY",
    "channelCode": "DEFAULT",
    "currentVersionCode": 10001,
    "minimumVersionCode": 10000,
    "minimumVersionName": "1.0.0",
    "latestVersionCode": 10100,
    "latestVersionName": "1.1.0",
    "updateAvailable": true,
    "forceUpdate": false,
    "blockReason": "NONE",
    "updateUrl": "https://download.example.com/game",
    "maintenanceActive": false,
    "loginBlocked": false,
    "maintenanceStartTime": null,
    "maintenanceEndTime": null,
    "message": null,
    "serverTime": "2026-07-31T12:00:00.000000"
  }
}
```

判断顺序：

1. `versionReportMode=REQUIRED` 且未提供版本号时，`blockReason=VERSION_MISSING`。
2. `currentVersionCode < minimumVersionCode` 时，`blockReason=BELOW_MINIMUM` 并始终强制更新。
3. 低于最新版本且管理端开启“低于最新版本强更”时，`blockReason=FORCE_UPDATE`。
4. 维护窗口生效且“维护期间禁止登录”开启时，`blockReason=MAINTENANCE`。
5. 只发现可选更新或只展示维护提示时返回 `blockReason=NONE` 并允许登录。
6. 指定渠道不存在时回退同平台 `DEFAULT`；平台也没有策略时 `configured=false`。

稳定枚举为 `NONE / VERSION_MISSING / BELOW_MINIMUM / FORCE_UPDATE / MAINTENANCE`。三个版本原因优先于维护并返回 HTTP 426，维护阻断返回 HTTP 503。`serverTime` 和维护窗口均为数据库北京时间。

账号登录、注册和微信登录必须继续携带：

```http
X-Client-Channel: DEFAULT
X-Client-Version-Code: 10001
```

启动查询用于页面展示，登录请求头用于服务端兜底，二者不能互相替代。V49 默认 `OPTIONAL` 兼容旧客户端；管理端切换为 `REQUIRED` 后，缺少版本号会返回 HTTP 426。

### 35.3 Unity DTO 与调用顺序

```csharp
using Newtonsoft.Json;

public sealed class ClientRuntimePolicyDto
{
    [JsonProperty("configured")] public bool Configured;
    [JsonProperty("platform")] public string Platform;
    [JsonProperty("channelCode")] public string ChannelCode;
    [JsonProperty("currentVersionCode")] public long? CurrentVersionCode;
    [JsonProperty("minimumVersionCode")] public long? MinimumVersionCode;
    [JsonProperty("minimumVersionName")] public string MinimumVersionName;
    [JsonProperty("latestVersionCode")] public long? LatestVersionCode;
    [JsonProperty("latestVersionName")] public string LatestVersionName;
    [JsonProperty("updateAvailable")] public bool UpdateAvailable;
    [JsonProperty("forceUpdate")] public bool ForceUpdate;
    [JsonProperty("blockReason")] public string BlockReason;
    [JsonProperty("updateUrl")] public string UpdateUrl;
    [JsonProperty("maintenanceActive")] public bool MaintenanceActive;
    [JsonProperty("loginBlocked")] public bool LoginBlocked;
    [JsonProperty("maintenanceStartTime")] public string MaintenanceStartTime;
    [JsonProperty("maintenanceEndTime")] public string MaintenanceEndTime;
    [JsonProperty("message")] public string Message;
    [JsonProperty("serverTime")] public string ServerTime;
}
```

Unity 推荐流程：

1. 启动后请求 `/client/runtime-policy`。
2. `VERSION_MISSING / BELOW_MINIMUM / FORCE_UPDATE` 时展示不可关闭更新页，并使用 `updateUrl`。
3. `MAINTENANCE` 时展示维护页。
4. `NONE` 时进入登录，登录请求补齐两个 `X-Client-*` 请求头。
5. HTTP 426/503 时解析统一响应体中的 `message`，不要只展示通用网络错误。

安全上线顺序：先保持 `OPTIONAL` 发布后端，再让 Unity/微信版本完成请求头接入并确认正常上报，最后才切换 `REQUIRED`。如发生误拦截，切回 `OPTIONAL` 即可恢复旧客户端登录。

### 35.4 管理端配置模块

管理端入口：`http://localhost:5173/configs`

| 模块编码 | 配置键 | 主要字段 |
| --- | --- | --- |
| `AD_REWARD` | `SCENES` | `scene/adUnitId/rewardItemKey/rewardAmount/dailyLimit/cooldownSeconds/expireSeconds/minWatchSeconds/enabled` |
| `CLIENT_RUNTIME` | `POLICIES` | `platform/channelCode/versionReportMode/minimumVersionCode/latestVersionCode/forceUpdate/updateUrl/maintenance*` |
| `NOTIFICATION_POLICY` | `BUSINESS` | 通知开关、离线分钟数、冷却、模板 ID、跳转页面 |
| `ECONOMY_SAFETY` | `LIMITS` | 单次奖励、活动人数、每日金币/钻石阈值和阻断方式 |
| `AVATAR_POLICY` | `BUSINESS` | `enabled/dailyUpdateLimit/configVersion`（固定头像目录从后端只读接口动态展示） |
| `MESSAGE_TEMPLATE` | `TEMPLATES` | 模板编码、类型、标题、正文、发件人和默认有效期 |

客户端策略中的 `OPTIONAL` 表示缺少版本号仍兼容登录，`REQUIRED` 表示缺少版本号立即按 HTTP 426 阻断。Unity 启用版本更新、强更或强制上报时必须先配置完整 HTTPS 更新地址；微信小游戏可使用微信原生更新能力。维护开启且起止时间都为空表示立即进入无限期维护，管理端会在发布前明确提示。

通用读取示例：

```http
GET /api/ops/config/CLIENT_RUNTIME/POLICIES
Authorization: Bearer <operator-token>
```

所有模块继续使用配置中心通用接口：

```text
POST   /api/ops/config/{moduleCode}/{configKey}/validate
PUT    /api/ops/config/{moduleCode}/{configKey}/draft
POST   /api/ops/config/{moduleCode}/{configKey}/publish
DELETE /api/ops/config/{moduleCode}/{configKey}/draft
GET    /api/ops/config/{moduleCode}/{configKey}/history
POST   /api/ops/config/{moduleCode}/{configKey}/rollback/{versionNo}
```

V48 迁移写入的默认业务配置会立即生效，但不会伪造运营发布人与审计历史。因此新模块首次进入页面时可能显示“正式版本：未纳入版本管理”；这不是 Git 状态或配置失效。管理员完成一次“保存草稿 -> 发布草稿”后会生成 `operator_config_release` 正式记录并显示 `v1`。

已发布的场景编码、策略编码、平台、渠道、模板编码和模板类型不可修改；已发布记录不能删除，只能停用。

### 35.5 各模块关键边界

激励广告：

- 奖励只能从当前启用的物品字典选择。
- `scene` 发布后不可修改。
- 广告验签密钥不进入页面；`AD_COMPLETION_PROOF_SECRET` 只能由服务端或可信回调服务持有。
- 玩家仍使用既有 `prepare -> complete -> claim` 三步接口。

通知提醒：

- 只把业务开关、分钟数、模板 ID、跳转页等参数放入管理端。
- 扫描批次、调度频率、后台密钥、微信 AppSecret 和 HTTP 超时仍放服务端配置。
- 发布后各节点最多 30 秒刷新完整不可变快照；刷新失败继续使用上一版。

头像策略：

- 默认头像来自 Unity 包内资源，不配置 HTTPS 地址、站内 URL 或对象存储路径。
- 头像图片随 Unity 客户端发布，服务端不接收图片、微信头像或远程地址；管理端通过 `/api/ops/config/avatar-presets` 动态只读展示后端目录，服务端不会扫描 Unity 包。
- 恢复默认头像始终允许；固定头像选择受头像业务总开关控制。
- 发布后各节点最多 30 秒刷新完整不可变快照。

内容模板：

- 类型为 `ANNOUNCEMENT/SYSTEM_MAIL/REWARD_MAIL`。
- 模板只填充创建表单；实际提交仍保存最终内容快照。
- 修改模板不会追溯修改已创建公告或邮件。

### 35.6 经济安全接口与错误码

查询北京时间指定自然日已承诺投放：

```http
GET /api/ops/economy-safety/daily-usage?date=2026-07-31
Authorization: Bearer <operator-token>
```

响应中的 `projectedGoldAmount` 和 `projectedDiamondAmount` 是字符串，避免 JavaScript 大整数失真。预算按奖励活动的计划发送日期统计；撤销活动不计入，未撤销失败活动仍计入已承诺预算。

| code | HTTP | 客户端/管理端处理 |
| --- | ---: | --- |
| `6402` | 200 | 超过硬性数量、人数或每日投放上限，不能绕过 |
| `6403` | 409 | 弹出风险确认；确认后以 `economyRiskConfirmed=true` 重提一次 |

并发创建奖励活动会锁定经济策略单例行，避免两个请求分别通过后共同超出每日预算。

### 35.7 公告与邮件模板接口

```http
GET /api/ops/message-templates?type=ANNOUNCEMENT
GET /api/ops/message-templates?type=SYSTEM_MAIL
GET /api/ops/message-templates?type=REWARD_MAIL
Authorization: Bearer <operator-token>
```

接口只返回已启用正式模板。管理端公告、普通邮件和奖励邮件创建页可选择模板；用户仍可在提交前修改标题、正文、发件人和有效期。

### 35.8 统一活动日历

管理端页面：`http://localhost:5173/activity-calendar`

```http
GET /api/ops/activity-calendar?from=2026-07-01T00:00:00&to=2026-10-31T23:59:59&expiringWithinHours=72
Authorization: Bearer <operator-token>
```

可按 `sourceType` 和统一 `status` 筛选。状态值：

```text
UPCOMING / ACTIVE / EXPIRING / EXPIRED / PERMANENT / DISABLED
```

当前聚合抽卡、公告、系统奖励、普通邮件、维护、金币商城、钻石商城、签到、任务和激励广告。`issues` 返回到期或配置异常提示，`conflictWithCodes` 返回冲突的稳定编码；冲突检测覆盖同平台维护窗口和卡池窗口。

管理端将平台和各业务原始状态按模块显示为中文；“业务状态”保留英文内部状态码悬停提示，稳定编码与后端枚举本身不做翻译或改写。

活动日历只读正式表，不是玩家活动接口，也不改写各业务状态。

### 35.9 数据库与安全边界

V48 新增：

- `client_runtime_policy`
- `notification_business_policy`
- `economy_safety_policy`
- `avatar_business_policy`
- `operator_message_template`
- `ad_reward_config.config_version`

V49 新增或补强：

- `client_runtime_policy.version_report_mode`，默认 `OPTIONAL`。
- `client_runtime_policy` 的版本上报模式和维护登录组合约束。
- 配置发布/审计 JSON、广告观看状态和微信订阅发送状态约束。
- `users`、地块和建筑核心表的中文表注释。

以下内容永远不能进入管理端配置：

- 微信 AppSecret、支付商户密钥和证书。
- 广告验签密钥。
- 数据库、Redis、对象存储账号和密钥。
- 内部公告/邮件后台密钥。
- 调度线程、扫描批次、租约和清理任务基础设施参数。

该段记录 V64 发布时的历史迁移边界；V1 至 V104 仍保持冻结。当前最高迁移为 V137，下一次数据库变化必须新增 V138。

### 35.10 本次验收

验证日期：2026-07-31（Asia/Shanghai）。

| 验证项 | 结果 |
| --- | --- |
| 后端编译 | 通过 |
| 后端全量测试 | 562 项，0 failure，0 error，63 项条件用例跳过 |
| 启动回归保护 | Mapper 扫描覆盖测试与 V48、V49、V50 已发布校验和防篡改测试通过 |
| 管理端生产构建 | `vue-tsc --noEmit && vite build` 通过 |
| MySQL 8 Flyway | 临时库从 V1 顺序迁移到 V50，重复迁移为 0 |
| 活动日历 SQL | 在真实 V50 MySQL 临时库执行并校验 10 个返回字段别名 |
| 真实应用启动 | 连接 `game_db` 完整启动并迁移到 V50；`GET /api/client/runtime-policy` 返回 `blockReason=NONE`，临时验证进程已关闭 |

仓库详细说明：`docs/runtime-operations-api.md`、`docs/operator-config-center.md`、`docs/database-script-guide.md`。

## 36. V50 数据库重复索引审计

V50 只删除两项经过完整证据链确认的普通重复索引：

| 表 | 删除索引 | 保留索引 | 索引列 |
| --- | --- | --- | --- |
| `player_friend` | `idx_player_friend_lower` | `uk_player_friend_pair` | `(lower_player_id, higher_player_id)` |
| `player_sign_in_record` | `idx_player_sign_in_overview` | `uk_player_sign_in_period_day` | `(player_id, plan_code, period_key, reward_day)` |

确认依据：

1. 两组索引的列、顺序和排序方向完全相同，保留索引还承担唯一性约束。
2. `PlayerFriendMapper.xml` 和 `PlayerSignInRecordMapper.xml` 的对应查询都能由保留索引完整覆盖。
3. 迁移前后 `EXPLAIN FORMAT=TREE` 均选择保留的唯一索引；好友反向查询继续使用 `idx_player_friend_higher`，签到日期查询继续使用 `idx_player_sign_in_claim_date`。
4. 本地代表性读取中两个保留唯一索引有读取计数，两个重复普通索引读取计数为 0。
5. 仅满足左前缀关系的其他短索引全部保留，等待生产周期统计后再单独评估。

验收结果：本地 `game_db` 已应用 V50，Flyway 校验和为 `-659852229`；数据库仍为 128 张表；后端全量测试 562 项，0 failure、0 error，63 项条件用例跳过；MySQL 8 临时库 V1 至 V50 迁移及重复执行检查通过。

V50 不改变任何 HTTP/WebSocket 字段、路径或业务语义，Unity、微信小游戏和管理端无需调整。该段记录的是 V50 当时的验收事实；当前 V51 至 V104 已执行且同样禁止修改，下一次数据库变化从 V105 开始。详细证据见仓库 `docs/database-index-audit.md`。

## 37. V51/V52 挂机冒险体力与随机奖励

### 37.1 最终功能效果

- 管理端“配置中心 -> 挂机冒险”可配置体力开关、体力上限、恢复 1 点所需秒数和每关体力消耗。
- 玩家第一次进入玩法时直接获得当前版本的最大体力；恢复由服务端按数据库北京时间惰性结算，不依赖定时任务。
- 运营降低上限不会删除玩家已有体力；超出上限时暂停恢复，消费回到上限以下后继续计时。
- 开始战斗、继续下一关和失败重试在同一玩家事务中扣体力；体力不足返回 HTTP 409、业务码 `6511`，会话和其他写入全部回滚。
- 固定奖励支持 `FIRST_CLEAR` 和 `EVERY_CLEAR`。历史奖励保持 V51 回填时的 `EVERY_CLEAR`，V52 后新奖励默认 `FIRST_CLEAR`。
- 每关可配置多个必中奖随机池。每个池可抽多次，候选支持 `ITEM / EQUIPMENT / NPC` 和独立数量，启用候选概率必须严格合计 `100000000`。
- 随机抽中重复 NPC 时，原始抽取仍显示 NPC，实际结算按发布快照转换为其专属碎片，不受后续配置修改影响。
- `/end` 在一个事务中发放固定与随机奖励；相同 `Idempotency-Key` 重试返回第一次完整结果，不会重复发放。

### 37.2 Unity 必须调整

新增调用：

```http
GET /api/idle-adventure/players/{player_id}/stamina
Authorization: Bearer <token>
```

体力对象字段：

```text
enabled
current_stamina          // C# 使用 long
max_stamina
over_cap
overflow_amount
recovery_interval_seconds
next_recovery_at
seconds_to_next_recovery
seconds_to_full
server_time
config_version
```

首页响应新增 `stamina`。战斗会话响应新增：

```text
pending_rewards          // 已通关、尚未结束结算的固定/随机奖励
random_drops             // 原始逐抽命中结果和随机区间
granted_rewards          // /end 实际入账结果
```

Unity 应在首页保存体力快照，并在开始、继续、失败重试成功后调用体力接口刷新。倒计时只用于展示，到零后必须重新查询；不能在本地增加权威余额。`STAGE_COMPLETE` 可用 `random_drops` 播放掉落动画，最终到账提示必须以 `/end` 的 `granted_rewards` 为准。抽中重复 NPC 时，`random_drops` 是 NPC，`granted_rewards` 是碎片且 `fallback_applied=true`。结束请求网络超时必须复用原 `Idempotency-Key`。

### 37.3 管理端发布约束

- `maxStamina` 范围 `1..1000000`，`recoveryIntervalSeconds` 范围 `1..2678400`。
- 启用体力时，每关 `staminaCost` 必须为 `1..maxStamina`。
- 每关最多 100 项固定物品奖励；发放范围只能为 `FIRST_CLEAR / EVERY_CLEAR`。
- 每关最多 20 个随机池，每池最多 100 个候选，每池 `drawCount` 为 `1..100`。
- 候选必须从已发布且启用的物品、装备、可获取 NPC 字典选择；NPC 数量固定为 1。
- `probabilityUnits` 是一亿分比，页面按百分比显示 6 位小数；每池启用项必须精确合计 100%。
- NPC 重复补偿由后端读取专属碎片并冻结发布快照，管理端不能手填内部 ID。
- 保存草稿不影响玩家；发布时创建完整新玩法版本并原子切换唯一 `ACTIVE`，任何校验失败都不产生半成品。

### 37.4 数据库与验证

V51 新增 5 张表并扩展玩法、关卡、固定奖励和待结算奖励结构；V52 不回改 V51，而是补齐首通默认值、CHECK 约束、复合索引和级联外键。该阶段数据库共有 139 张业务表，V51 校验和为 `-675021607`，V52 校验和为 `-1330169956`。当前 V1 至 V73 禁止修改，下一迁移使用 V74。

后端全量测试 587 项通过，0 failure、0 error，63 项条件用例跳过；其中 V51/V52 已发布迁移校验和测试均通过。管理端 TypeScript 检查和生产构建通过；真实 Spring Boot 启动、Flyway V52、MyBatis Mapper 和定时任务装配通过。完整契约见桌面 `IdleAdventure模式接口文档.md`。

## 38. V53-V58/V60 地块、加工生产与装备升级扩展

> **历史合同提示：** 本节保留 V53-V60 阶段的统一金币价接口，用于理解旧客户端兼容，不得作为当前 Unity 接入依据。当前 32 格独立等级、免费/金币/钻石解锁、完整报价回传和版本守卫统一以第 66 节为准。

更新日期：2026-08-10
适用迁移：V53-V58、V60-V61
客户端：Unity / 微信小游戏
玩家接口基础地址：`/api`

### 一、功能范围

本次扩展包含三组能力：

1. 经营地图返回固定 8×4 共 32 格的等级、净化、购买和最终放置状态，支持永久净化、金币购买和管理端统一调价。
2. 建筑等级可以配置每个生产周期的输入物品；服务端按当前可支付的完整周期真实扣除输入并累计产物。
3. 装备升级可以在金币和同款突破装备之外配置额外仓库物品，并提供升级前费用预览。

所有玩家写操作先锁定 `users` 主行，再锁业务记录并修改背包，避免不同模块形成反向锁序。客户端提交的玩家 ID 必须与登录令牌一致。

### 二、经营地图格子

#### 2.1 查询完整地图

```http
GET /api/land/map/{playerId}
Authorization: Bearer <token>
```

`MapDataResponse` 新增 `landSlots`。数组固定覆盖当前经营地图 `x=0..7`、`y=0..3` 的 32 个格子；`lands` 和 `buildings` 仍分别表示已经放置的地块与建筑。

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "playerId": 5,
    "width": 10,
    "height": 10,
    "landPolicyRevision": 7,
    "landGridWidth": 8,
    "landGridHeight": 4,
    "landSlotCount": 32,
    "landSlots": [
      {
        "positionX": 4,
        "positionY": 0,
        "unitIndex": 1,
        "requiredLevel": 5,
        "levelUnlocked": true,
        "requiresPurification": true,
        "purified": false,
        "purificationAvailable": true,
        "purificationItemId": 200000,
        "purificationItemKey": "purification_agent",
        "purificationItemAmount": 1,
        "requiresPurchase": false,
        "purchased": true,
        "purchaseAvailable": false,
        "placementUnlocked": false,
        "goldCost": 0
      }
    ],
    "lands": [],
    "buildings": []
  }
}
```

字段语义：

| 字段 | 说明 |
| --- | --- |
| `width` / `height` | 旧地图坐标兼容字段，当前仍为 `10×10`；不能据此生成 32 格经营区域 |
| `landPolicyRevision` | 本次响应使用的经营地图策略版本；新购买时原样提交 |
| `landGridWidth` / `landGridHeight` | 32 格经营地图的权威宽高，当前固定为 `8×4` |
| `landSlotCount` | 权威格子总数，当前固定为 `32` |
| `unitIndex` | 所属 2×2 地块单元，范围 `0..7` |
| `requiredLevel` | 单元等级门槛 |
| `levelUnlocked` | 当前玩家是否达到等级门槛 |
| `requiresPurification` | 该玩家的该格是否需要净化；同一玩家永久稳定 |
| `purified` | 无需净化或已经永久净化 |
| `purificationAvailable` | 当前是否可以调用净化接口 |
| `purificationItemId / purificationItemKey` | 当前正式策略要求的净化物品身份 |
| `purificationItemAmount` | 首次净化该格实际需要的物品数量 |
| `requiresPurchase` | 是否需要金币购买 |
| `purchased` | 免费格或已经永久购买 |
| `purchaseAvailable` | 是否满足等级、净化和上下左右相邻条件，可以购买 |
| `placementUnlocked` | Unity 是否可以在该坐标调用地块放置接口 |
| `goldCost` | 当前付费格价格；免费格为 `0` |

Unity 必须使用 `landGridWidth`、`landGridHeight` 和 `landSlotCount` 生成 32 格经营区域；旧 `width`、`height` 暂时保留，是为了兼容数据库中既有地图坐标和旧客户端。Unity 应直接使用 `placementUnlocked` 控制放置，不要自行组合多个布尔字段推导最终权限。使用 `purificationAvailable` 和 `purchaseAvailable` 控制相应按钮，操作成功后用响应对象替换该坐标状态，随后可按需重新拉取完整地图。

当前固定规则：

- 单元等级门槛依次为 `1, 5, 10, 10, 15, 15, 15, 15`。
- 免费区域是 `x=4..7, y=0..1`；其余区域按格购买。
- 单元 0 无需净化，单元 1 需要净化 1 格，单元 2、3 各净化 2 格，单元 4 至 7 全部需要净化。
- 需要净化的具体格子由 `playerId + unitIndex` 稳定计算，不是每次请求重新随机。
- 付费格只能从免费或已购买的可访问格子向上下左右相邻位置逐格扩展。

#### 2.2 净化格子

```http
POST /api/land/slot/purify
Content-Type: application/json
Authorization: Bearer <token>
```

```json
{
  "playerId": 5,
  "positionX": 4,
  "positionY": 0
}
```

返回 `LandSlotStateResponse`。首次净化按 `LAND_MAP_POLICY / WORLD` 的正式配置扣除 `purificationItemKey × purificationItemAmount`，默认是 `purification_agent × 1`；余额不足时不写净化记录。物品扣除、净化记录和实际物品 ID、编码、数量快照位于同一事务，任一步失败整体回滚。无需净化或已经净化时重复调用仍返回成功，不会重复扣费或重复写记录；后续管理端改价不追溯历史消耗。节点尚未加载到正版本正式策略时仍可展示固定布局，但净化接口会返回“净化消耗配置暂不可用”，不会使用兼容占位物品扣费。

#### 2.3 购买格子

```http
POST /api/land/slot/purchase
Content-Type: application/json
Authorization: Bearer <token>
```

```json
{
  "playerId": 5,
  "positionX": 3,
  "positionY": 1,
  "expectedPolicyRevision": 7
}
```

`expectedPolicyRevision` 是查询地图时收到的 `landPolicyRevision`。旧客户端可以暂时省略，但新 Unity 必须携带，避免管理端调价后仍按旧界面价格提交。

首次购买会在同一事务中写入永久解锁记录并扣除金币；任一步失败会整体回滚。相同坐标重复提交会优先返回已经购买的最终状态，不会再次扣费，也不会因为策略后来变更而失败。购买前必须先净化，并满足等级与相邻格条件。

Unity 提交流程：

1. 查询地图并显示目标格的 `goldCost`，保存同一响应中的 `landPolicyRevision`。
2. 玩家确认后，将该版本作为 `expectedPolicyRevision` 提交。
3. HTTP `409`、业务码 `7601` 表示价格已变化；重新查询地图、刷新显示价格并要求玩家再次确认，不能自动按新价格扣费。
4. HTTP `503`、业务码 `7602` 表示节点尚未加载正式策略；临时禁用购买按钮并稍后重查地图。
5. 网络超时重试使用同一坐标和原请求。若第一次已经成功，服务端仍幂等返回已购买状态。

#### 2.4 管理端经营地图策略

配置中心使用：

```text
moduleCode = LAND_MAP_POLICY
configKey  = WORLD
```

正式 payload：

```json
{
  "mapWidth": 8,
  "mapHeight": 4,
  "unitSideLength": 2,
  "freeSlotCount": 8,
  "paidSlotCount": 24,
  "paidSlotGoldCost": 1000,
  "purificationItemId": 200000,
  "purificationItemKey": "purification_agent",
  "purificationItemAmount": 1,
  "configVersion": 7
}
```

抽卡命中管理端勾选的稀有奖项时，所有令牌仍有效的在线会话会收到 `CARD_DRAW_RARE_BROADCAST`。这是一条游戏顶部公屏轮播，不是公告、邮件或站内提醒；完整字段、去重和过期规则见第 55 节。

- 页面完整展示 8×4 地图，但布局字段只读；管理员可修改 24 个付费格的统一单格价格、净化物品和单格净化数量。
- `paidSlotGoldCost` 范围为 `1..1000000000`；页面同时展示 24 格全部购买的总价。
- 净化物品必须存在、启用且类型为 `PRODUCT`，`purificationItemAmount` 范围为 `1..1000000`；页面选择物品时同步其真实 ID 和编码。
- 保存草稿不影响玩家，发布时执行指纹、草稿修订号、正式版本和数据库约束校验。
- 正式表、配置版本、审计和刷新 Outbox 在同一事务提交；提交后本机事件与 Redis 消息刷新节点，30 秒心跳负责漏通知兜底。
- 运行时通过不可变快照读取，不在地图查询或购买事务中反复查询策略表。
- 历史购买记录的 `gold_cost` 保存实际成交价格，不随以后调价变化。
- 不再使用 `LAND_SLOT_PURCHASE_GOLD_COST`。部署参数只保留可选的 `LAND_MAP_POLICY_REFRESH_MS=30000` 心跳间隔。

### 三、装备升级额外资源

#### 3.1 查询升级预览

```http
GET /api/equipment/{playerEquipmentId}/upgrade-preview
Authorization: Bearer <token>
```

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "playerEquipmentId": 501,
    "equipmentId": 4001,
    "equipmentKey": "starter_sword",
    "name": "新手短剑",
    "quality": "COMMON",
    "currentLevel": 19,
    "targetLevel": 20,
    "maxLevel": 100,
    "maxLevelReached": false,
    "goldCost": 2000,
    "availableGold": 5000,
    "sameEquipmentCount": 1,
    "resourceCosts": [
      {
        "itemKey": "stone",
        "itemName": "石材",
        "amount": 50,
        "availableAmount": 120
      }
    ]
  }
}
```

预览只读取当前快照，不锁定资源，也不预留材料。`quality` 使用统一五档稳定编码。Unity 可据此展示升级确认框，但提交升级时仍以服务端事务校验为准。满级时 `maxLevelReached=true`、`targetLevel=currentLevel`、费用为 0，两个材料列表为空。

#### 3.2 执行升级

原接口路径与请求不变：

```http
POST /api/equipment/upgrade
Content-Type: application/json
Authorization: Bearer <token>

{
  "playerEquipmentId": 501
}
```

成功响应新增 `resourceCosts`：

```json
{
  "quality": "COMMON",
  "goldCost": 2000,
  "sameEquipmentCount": 1,
  "consumedPlayerEquipmentIds": [601],
  "remainingGold": 3000,
  "resourceCosts": [
    {
      "itemKey": "stone",
      "itemName": "石材",
      "amount": 50,
      "remainingAmount": 70
    }
  ]
}
```

金币、额外仓库物品、配置命中的同款突破装备、等级更新和升级审计位于同一事务。任一种资源不足时返回业务码 `6204`，不会发生部分扣除。额外仓库物品实际消耗会冻结到 `equipment_upgrade_resource_cost_record`。Unity 升级成功后使用 `remainingGold` 和各项 `remainingAmount` 更新资源栏，并刷新目标装备；失败后重新查询预览。

### 四、加工建筑输入消耗

#### 4.1 运营配置结构

管理端“配置中心 -> 地块与建筑 -> 等级与生产”中，每个等级包含：

```json
{
  "level": 1,
  "produceIntervalSeconds": 60,
  "storageCapacity": 500,
  "productions": [
    { "itemKey": "iron", "amount": 100 }
  ],
  "productionCosts": [
    { "itemKey": "stone", "amount": 50 }
  ]
}
```

`productions` 是每个完整周期的产物，`productionCosts` 是每个完整周期先扣除的仓库物品。配置生产消耗时必须且只能配置一种产物；产物和消耗各最多 100 行，同一列表不能重复物品，数量范围为 `1..1000000000000000`，引用物品必须存在且启用。

正式表继续使用兼容快照字符串：

```text
iron:100|stone:50
```

`|` 前是产物，`|` 后是周期消耗。玩家接口中的 `BuildingConfigResponse.produce` 和 `BuildingResponse.produce` 仍返回该完整兼容字符串；Unity 若展示生产规则，应先按第一个 `|` 分段，再分别按逗号和冒号解析，不能把右侧内容当成产物。生产状态接口 `ProductionResponse` 只返回产物状态，输入消耗以建筑 `produce` 快照为准。

#### 4.2 服务端结算语义

- 没有 `productionCosts` 的建筑保持原有纯产出逻辑。
- 有输入消耗时，服务端按最短缺料项计算本次能支付的完整周期数，只扣除实际结算周期对应的输入。
- 例如到期 3 周期但材料只够 2 周期，只生产 2 周期并扣 2 周期材料。
- 无法支付的历史周期不会形成无限欠账；结算时钟从当前服务器时间重新开始。
- 产物仓库已满时不再扣输入；所有乘法和加法都有 `long` 溢出保护。
- 查询生产列表、查询详情、主动收取和后台扫描都可能触发真实结算。Unity 打开生产面板后应刷新背包，不能假定查询接口一定只读资源。
- 后台任务只把产物累计到建筑暂存区，不直接发到玩家背包；玩家仍需调用原收取接口。

#### 4.3 多实例调度配置

单实例默认不访问 Redis 分布式锁。部署多个后端实例时必须配置：

```text
PRODUCTION_SETTLEMENT_ENABLED=true
PRODUCTION_SETTLEMENT_CLUSTER_LOCK_ENABLED=true
PRODUCTION_SETTLEMENT_LOCK_KEY=game:production:settlement:lock
PRODUCTION_SETTLEMENT_LOCK_TTL_MS=60000
PRODUCTION_SETTLEMENT_BATCH_SIZE=200
PRODUCTION_SETTLEMENT_FIXED_DELAY_MS=30000
```

锁使用随机所有者令牌、Lua 比较续租和比较释放。Redis 不可用或续租失败时停止领取本轮后续建筑；玩家主动查询和收取仍可通过惰性结算继续工作。

### 五、装备成长管理配置

`EQUIPMENT_GROWTH` payload 新增：

```json
{
  "resourceCosts": [
    { "targetLevel": 20, "itemKey": "stone", "amount": 50 },
    { "targetLevel": 21, "itemKey": "wood", "amount": 100 }
  ]
}
```

规则：

- `targetLevel` 必须为 `2..maxLevel`。
- 金币由金币曲线管理，`itemKey=gold` 禁止重复配置。
- 同一目标等级和物品只能出现一次。
- 单件装备最多 1000 条额外资源消耗。
- 发布时引用物品必须存在且启用；发布会按目标等级、物品编码稳定排序后整体替换正式配置。
- 管理端页面只能从物品字典选择，保存草稿不会影响玩家，发布后新升级事务立即使用正式配置。

配置中心通用接口仍为：

```http
GET  /api/ops/config/{moduleCode}/{configKey}
POST /api/ops/config/{moduleCode}/{configKey}/validate
PUT  /api/ops/config/{moduleCode}/{configKey}/draft
POST /api/ops/config/{moduleCode}/{configKey}/publish
Authorization: Bearer <operator-token>
```

本次涉及 `moduleCode=LAND_BUILDING, configKey=WORLD`，以及 `moduleCode=EQUIPMENT_GROWTH, configKey={equipmentId}`。

### 六、数据库迁移

| 版本 | 作用 | 固定校验和 |
| --- | --- | ---: |
| V53 | 加工建筑默认等级增加周期输入消耗 | `-848927068` |
| V54 | 创建玩家地块购买表并兼容存量占用 | `-869353727` |
| V55 | 将免费区域调整到右下两块并兼容存量占用 | `947265086` |
| V56 | 创建玩家地块净化表并回填存量占用 | `954492890` |
| V57 | 创建装备升级额外资源配置表 | 399150688 |
| V75 | 突破点改为 2-100 级内可配置，并新增装备升级额外资源消耗快照流水 | 新迁移，随本次发布执行 |
| V58 | 精确回填历史加工建筑和进行中任务快照 | `1726757000` |
| V60 | 创建固定 32 格经营地图策略及可版本化付费格价格 | `-1617276541` |
| V61 | 统一 NPC 与装备五档品质并增加数据库约束 | `89572006` |
| V62 | 创建多场景进度、逐场景价格及生产收取幂等快照 | `-1155339352` |
| V63 | 回填单建筑收取场景快照并严格约束收取范围与场景字段 | `-2076174813` |

V1-V104 禁止修改，下一次数据库变化使用 V105。V59 是玩家昵称管理迁移，接口见 [`nickname-api.md`](nickname-api.md)。

### 七、验证记录

- `mvn -q test`：后端全量测试通过。
- 经营地图策略、购买报价、地图响应、Redis 刷新、HTTP 错误码和管理端 Handler 共 53 项定向测试通过。
- 真实 MySQL 8 从 V57 顺序升级到 V61，正式策略、旧装备品质映射及 V60/V61 CHECK 约束验证通过。
- `npm.cmd run build`：Vue TypeScript 检查与 Vite 生产构建通过。
- `git diff --check`：补丁格式检查通过。

## 39. 玩家昵称修改与 Unity 接入

适用迁移：V59  
玩家接口基础地址：`http://localhost:8080/api`

### 39.1 最终规则

1. 服务端先执行 Unicode NFKC 规范化，再保存规范化后的显示昵称。
2. 长度为 2 至 12 个 Unicode 码点，只允许中文汉字、ASCII 英文字母和数字。
3. 禁止纯数字、空格、下划线、标点、emoji 和其他特殊符号。
4. 英文大小写不影响唯一性，`Alice88` 与 `alice88` 不能被两个玩家分别占用。
5. 正式保留词按 NFKC 和英文小写做包含匹配，由管理端发布。
6. 改名使用玩家行锁、昵称版本和数据库唯一索引；失败事务不会改变玩家或审计流水。
7. 相同规范化目标的超时重试是幂等操作，不会重复增加版本、修改次数或审计流水。

Unity 可以提前限制常见非法输入，但必须以服务端返回为准。C# 的 `string.Length` 不是 Unicode 码点数，不要只依赖它复刻服务端长度判断。

### 39.2 用户响应新增字段

登录响应中的 `user` 和 `GET /api/user/me` 新增：

| 字段 | 类型 | 可空 | 说明 |
| --- | --- | --- | --- |
| `nicknameVersion` | long | 否 | 昵称状态版本，首次为 0 |
| `nicknameUpdatedTime` | datetime | 是 | 最近一次主动改名的北京时间，首次主动改名前为 `null` |
| `nicknameChangeCount` | int | 否 | 成功主动改名累计次数 |
| `nicknameChangeRequired` | boolean | 否 | 是否正在使用微信建号时分配的临时昵称 |

Unity 打开改名页面时必须使用服务端最新 `nicknameVersion`，不要长期复用本地缓存版本。

### 39.3 查询昵称策略和当前状态

```http
GET /api/user/nickname/policy
Authorization: Bearer <player-token>
```

成功响应：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "enabled": true,
    "minLength": 2,
    "maxLength": 12,
    "allowedCharacterType": "HAN_LATIN_DIGIT",
    "pureNumberAllowed": false,
    "cooldownHours": 168,
    "reservedWords": ["系统", "官方", "客服", "管理员", "gm"],
    "configVersion": 1,
    "nicknameVersion": 0,
    "nicknameUpdatedTime": null,
    "nicknameChangeCount": 0,
    "nicknameChangeRequired": false,
    "canChangeNow": true,
    "nextChangeAvailableTime": null
  }
}
```

| 字段 | Unity 用法 |
| --- | --- |
| `enabled` | 为 `false` 时禁用主动改名提交 |
| `allowedCharacterType` | 固定为 `HAN_LATIN_DIGIT` |
| `reservedWords` | 用于输入提示，不得硬编码为客户端最终裁决规则 |
| `configVersion` | 策略版本，仅用于刷新和诊断，不是改名并发版本 |
| `nicknameVersion` | PUT 时原样提交为 `expectedNicknameVersion` |
| `canChangeNow` | 已综合开关和冷却，不代表待提交昵称一定可用 |
| `nextChangeAvailableTime` | 北京时间；无冷却或从未主动改名时为 `null` |

### 39.4 修改当前玩家昵称

```http
PUT /api/user/nickname
Authorization: Bearer <player-token>
Content-Type: application/json
```

请求：

```json
{
  "nickname": "青山客",
  "expectedNicknameVersion": 0
}
```

成功响应：

```json
{
  "code": 200,
  "message": "昵称修改成功",
  "data": {
    "nickname": "青山客",
    "nicknameVersion": 1,
    "nicknameUpdatedTime": "2026-08-04T12:00:00",
    "nicknameChangeCount": 1,
    "nicknameChangeRequired": false,
    "nextChangeAvailableTime": "2026-08-11T12:00:00"
  }
}
```

Unity 处理顺序：

1. 仅在 HTTP 2xx 且 `code=200` 时替换本地昵称状态。
2. 展示响应中的 `nickname`，不要继续使用未经规范化的原始输入。
3. 使用响应中的 `nicknameVersion` 覆盖本地版本。
4. HTTP 409 时重新读取 `/api/user/me` 或昵称 policy，再让玩家确认。
5. HTTP 429 时刷新 policy，并展示服务端返回的可用时间。

第一次请求已提交但客户端未收到响应时，使用相同昵称和原 `expectedNicknameVersion` 重试。只要当前规范化昵称仍等于目标，服务端返回第一次成功后的当前状态；如果该玩家随后又改成其他昵称，旧请求返回并发修改错误。

### 39.5 错误码

| HTTP | 业务码 | 含义 | Unity 处理 |
| ---: | ---: | --- | --- |
| 400 | `400` | 请求字段缺失或类型错误 | 检查请求结构 |
| 400 | `2006` | 昵称格式不符合固定规则 | 保留输入并显示服务端提示 |
| 409 | `2007` | 昵称已被其他玩家使用 | 让玩家更换昵称 |
| 400 | `2008` | 昵称包含正式保留词 | 让玩家更换昵称 |
| 429 | `2009` | 改名仍在冷却中 | 刷新 policy 并展示可用时间 |
| 409 | `2010` | 昵称版本已变化 | 刷新用户和 policy 后重试 |
| 403 | `2011` | 管理端已关闭主动改名 | 禁用提交并刷新 policy |
| 401 | `401` | 未登录或 token 过期 | 重新登录 |

HTTP 非 2xx 时仍需解析统一 JSON 中的 `code` 和 `message`。

### 39.6 注册和微信首次建号

`POST /api/user/register` 的 `nickname` 使用同一套固定规则、正式保留词和全服唯一性校验。非法昵称返回对应业务错误，不会留下半成品用户、资源或地图。

`POST /api/user/wx-login` 的 `nickname` 是可选建议值。首次建号时该值为空、非法、包含保留词或已占用，服务端会改用形如 `玩家A1B2C3D4E5` 的唯一临时昵称：

- 微信登录仍成功；
- `nicknameChangeRequired=true`；
- 临时昵称不消耗主动改名次数；
- 第一次主动改名成功后自动变为 `false`。

Unity 应在 `nicknameChangeRequired=true` 且 `enabled=true` 时突出显示改名入口。

### 39.7 管理端配置

配置中心模块：

```text
moduleCode = PLAYER_NICKNAME
configKey  = POLICY
```

正式 payload：

```json
{
  "enabled": true,
  "cooldownHours": 168,
  "reservedWords": ["系统", "官方", "客服", "管理员", "GM"],
  "configVersion": 1
}
```

- 只有 `CONFIG_ADMIN` 可以保存、发布、丢弃或创建回滚草稿。
- `cooldownHours` 为 0 至 8760，0 表示无冷却。
- 保留词最多 200 个，每个词为 1 至 12 位中文、英文字母或数字。
- 规范化后重复的保留词会阻止保存或发布。
- 发布使用 `configVersion` 乐观校验，并保留配置发布历史和操作审计。
- 新策略只影响后续昵称分配和主动修改，不追溯强制重命名历史玩家。

### 39.8 数据库和上线

V59 新增：

- `users.nickname_normalized` 生成列和唯一索引；
- 昵称版本、最近修改时间、修改次数及临时昵称标志；
- `player_nickname_business_policy` 正式策略表；
- `player_nickname_change_record` 不可变审计流水。

迁移前先执行只读检查：

```sql
SELECT LOWER(TRIM(nickname)) AS normalized_nickname, COUNT(*) AS duplicate_count
FROM users
GROUP BY LOWER(TRIM(nickname))
HAVING COUNT(*) > 1;
```

结果必须为空。生产发布时停止旧版本写流量、备份数据库，按第 65.7 节确认两张头像清单审计表为空后，启动一个当前版本节点并由 Flyway 迁移至仓库当前最高版本 V104，验证注册、微信登录、policy、改名和玩家任务接口后再恢复全部节点。V1-V104 已发布迁移禁止修改，下一次数据库变化使用 V105。

验证记录：真实 MySQL 8 已从 V1 完整迁移到 V61；后端全量测试、昵称定向测试、管理端 TypeScript 检查和生产构建均通过。

## 40. V61 NPC/装备品质与 V142 物品品质统一协议

### 40.1 稳定编码

NPC、装备和物品共用以下五档品质。后端、数据库和 Unity 只传稳定英文编码，中文仅用于显示。

| 编码 | 中文 | Unity 建议排序值 |
| --- | --- | ---: |
| `COMMON` | 普通 | 0 |
| `FINE` | 优秀 | 1 |
| `EXCELLENT` | 稀有 | 2 |
| `EPIC` | 史诗 | 3 |
| `LEGENDARY` | 传说 | 4 |

旧编码 `UNCOMMON`、`RARE`、`MYTHIC` 已停用。Unity 遇到未知值时按普通样式兜底并记录日志，不要因为枚举反序列化失败阻断整个列表。

### 40.2 管理端配置

入口：`配置中心 -> 物品基础资料`、`配置中心 -> 装备基础资料` 和 `配置中心 -> NPC 基础资料`。

- 新增装备或 NPC 默认 `COMMON`，运营从固定五档下拉框选择；新增物品必须显式选择品质，不允许默认代填。
- 页面不允许自由创建品质编码。
- 装备被玩家、穿戴、升级、签到、抽卡或商城引用后，编码、部位和品质锁定。
- NPC 被玩家或业务配置引用后，品质锁定；名称、图标和说明仍按原规则维护。
- 草稿不影响游戏，只有校验并发布后才更新正式配置。
- `GET /api/ops/config/dictionaries` 的物品、装备和 NPC 条目统一返回 `quality`；迁移期尚未完成首次确认的存量物品才可能暂时为 `null`。

NPC 配置 payload 示例：

```json
{
  "npcId": 3001,
  "npcCode": "field_steward",
  "name": "田园管家",
  "quality": "FINE",
  "combatAttribute": "PHYSICAL"
}
```

### 40.3 Unity 新增字段

| 接口或响应 | 品质字段 |
| --- | --- |
| `GET /api/npc/config/list` | `data[].quality` |
| `GET /api/npc/list/{playerId}` | `data[].quality` |
| `GET /api/npc/available/{playerId}` | `data[].quality` |
| `POST /api/npc/upgrade` | `data.quality` |
| 建筑生产详情 | `data[].settledNpc.quality` |
| `GET /api/equipment/warehouse` | `data[].quality` |
| `GET /api/equipment/{playerEquipmentId}/upgrade-preview` | `data.quality` |
| `POST /api/equipment/upgrade` | `data.quality` |
| `GET /api/equipment/loadout/{playerNpcId}` | `data.equipment[].quality` |
| `GET /api/idle-adventure/players/{player_id}/npcs` | `data[].quality` |
| 挂机冒险编队响应 | `data.slots[].quality` |

NPC 图鉴响应示例：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": [
    {
      "npcId": 3001,
      "npcCode": "field_steward",
      "name": "田园管家",
      "quality": "FINE",
      "icon": "npc/field_steward",
      "portraitKey": "field_steward_portrait",
      "modelKey": "model/field_steward",
      "combatAttribute": "PHYSICAL",
      "maxLevel": 100
    }
  ]
}
```

Unity 建议保留服务端字符串，并集中转换排序或边框样式：

```csharp
public static int QualityOrder(string quality) => quality switch
{
    "FINE" => 1,
    "EXCELLENT" => 2,
    "EPIC" => 3,
    "LEGENDARY" => 4,
    _ => 0
};
```

品质属于 NPC/装备模板，不重复写入 `player_npc` 或玩家装备实例。玩家 NPC、建筑入驻、挂机冒险、装备仓库和升级接口都会读取正式模板的当前品质。

### 40.4 数据库与上线

V61 新增：

- `npc_config.quality`，已有 NPC 默认回填 `COMMON`；
- 旧装备 `UNCOMMON -> FINE`、`RARE -> EXCELLENT`、`MYTHIC -> LEGENDARY`；
- 无法识别的旧自定义品质回填 `COMMON`；
- `chk_npc_config_quality` 和 `chk_equipment_config_quality` 固定五值约束；
- 固定 Flyway 校验和 `89572006`。

生产上线顺序（V61 发布时）：备份数据库，只启动一个节点执行 V61，确认 `flyway_schema_history.success=1` 以及品质约束存在，再启动其他节点。当前 V1-V73 禁止修改，下一次迁移使用 V74。

验证记录：后端全量 683 项测试为 0 failure、0 error；管理端 TypeScript 与 Vite 生产构建通过；真实 MySQL 8 已验证 V1 到 V61 顺序升级、旧品质映射和两张表 CHECK 约束。

## 41. V62 多经营场景、32 格进度与生产收取

> **历史合同提示：** 本节保留 V62 阶段的多场景初版合同，其中统一金币价、旧报价字段和示例响应已经被后续版本扩展。当前 Unity 必须按第 66 节携带 `X-Client-Version-Code`，读取完整 `landSlots` 字段并回传全部 `expected*` 报价快照。

### 41.1 最终游戏效果

- 每个经营场景固定为 `8×4` 共 32 格，地块、建筑、NPC 入驻、生产状态、购买和净化记录均按 `sceneId` 隔离。
- 玩家把当前场景 32 格全部推进到 `placementUnlocked=true` 后，服务端幂等标记场景完成并解锁下一个启用场景；不要求实际放满地块或建筑。
- 金币、背包、装备、NPC、等级等玩家资产全局共享。切换场景只改变最近查看场景，不暂停其他场景生产。
- 新玩家自动进入第一个启用场景。默认目录包含 `HOME` 和 `FOREST`，运营可通过管理端继续向末尾追加场景。

### 41.2 Unity 推荐调用顺序

1. 登录成功后调用 `GET /api/land/scenes/list/{playerId}`，构建场景选择页并记录每项 `configVersion`。
2. 选择 `canEnter=true` 的场景后调用 `POST /api/land/scenes/switch`；重复切换当前场景会幂等成功。
3. 调用 `GET /api/land/map/{playerId}?sceneId={sceneId}`，使用 `landGridWidth=8`、`landGridHeight=4` 和 32 条 `landSlots` 渲染经营区域。
4. 只根据服务端 `purificationAvailable`、`purchaseAvailable`、`placementUnlocked` 控制按钮，不在客户端重复推导最终权限。
5. 净化、购买、放地块成功后，用返回状态替换该坐标并重新查询场景列表；当场景完成时可展示下一场景解锁效果。
6. 建筑和生产列表始终显式传 `sceneId`；单建筑建造、移动通过 `playerLandId` 自动确定所属场景。

### 41.3 场景列表与切换

```http
GET /api/land/scenes/list/{playerId}
Authorization: Bearer <token>
```

`data` 为数组，单项示例：

```json
{
  "sceneId": 1,
  "sceneKey": "HOME",
  "sceneName": "家园",
  "sortOrder": 1,
  "requiredLevels": [1, 5, 10, 10, 15, 15, 15, 15],
  "paidSlotGoldCost": 1000,
  "backgroundKey": "scene/home/background",
  "terrainKey": "scene/home/terrain",
  "bgmKey": "scene/home/bgm",
  "icon": "scene/home/icon",
  "status": "UNLOCKED",
  "current": true,
  "canEnter": true,
  "unlockedSlotCount": 8,
  "totalSlotCount": 32,
  "completed": false,
  "nextSceneId": 2,
  "unlockedTime": "2026-08-04T18:00:00",
  "completedTime": null,
  "lastEnteredTime": "2026-08-04T18:00:00",
  "configVersion": 7
}
```

`status` 稳定值为 `LOCKED / UNLOCKED / COMPLETED`。锁定场景仍返回预览资源，但 `canEnter=false`。

```http
POST /api/land/scenes/switch
Authorization: Bearer <token>
Content-Type: application/json

{
  "playerId": 5,
  "sceneId": 2
}
```

### 41.4 场景地图、净化和购买

```http
GET /api/land/map/{playerId}?sceneId=1
Authorization: Bearer <token>
```

地图响应新增或明确以下字段：

| 字段 | Unity 用途 |
| --- | --- |
| `sceneId / sceneKey / sceneName` | 标识本次地图所属场景 |
| `backgroundKey / terrainKey / bgmKey` | 加载对应 Unity Addressables 资源 |
| `sceneConfigVersion` | 购买付费格时提交的报价版本 |
| `landPolicyRevision` | 旧客户端兼容字段，V62 起等于 `sceneConfigVersion` |
| `landGridWidth / landGridHeight / landSlotCount` | 权威经营区域尺寸，固定 `8 / 4 / 32` |
| `landSlots[].sceneId` | 格子所属场景 |
| `lands[].sceneId / buildings[].sceneId` | 已放置地块和建筑所属场景 |

净化请求：

```http
POST /api/land/slot/purify
Authorization: Bearer <token>
Content-Type: application/json

{
  "playerId": 5,
  "sceneId": 1,
  "positionX": 4,
  "positionY": 0
}
```

购买请求：

```http
POST /api/land/slot/purchase
Authorization: Bearer <token>
Content-Type: application/json

{
  "playerId": 5,
  "sceneId": 1,
  "positionX": 3,
  "positionY": 1,
  "expectedSceneVersion": 7
}
```

- 新 Unity 必须提交 `expectedSceneVersion`。旧字段 `expectedPolicyRevision` 暂时兼容；两者同时存在时使用新字段。
- 业务码 `7601` 表示价格已变化：重新拉取地图、展示新价格并要求玩家再次确认，不能静默按新价格扣费。
- 业务码 `7602` 表示场景报价策略暂不可用：暂时禁用购买并稍后刷新。
- 同一已购买坐标重复提交不会再次扣金币，历史成交价保存在购买记录中，不受后续调价影响。

放置地块 `POST /api/land/place` 的请求新增可选 `sceneId`，新 Unity 应显式传入。建造和移动建筑继续提交 `playerLandId / targetPlayerLandId`，服务端从地块实例确定场景，不能跨场景移动建筑。

建筑列表和同步状态支持场景参数：

```http
GET  /api/building/list/{playerId}?sceneId=1
POST /api/building/sync/{playerId}?sceneId=1
```

### 41.5 三种生产收取范围

查询指定场景生产状态：

```http
GET /api/production/list/{playerId}?sceneId=1
Authorization: Bearer <token>
```

每条生产状态新增 `sceneId` 和 `playerLandId`；兼容字段 `landId` 与 `playerLandId` 相同。

收取单个建筑：

```http
POST /api/production/collect
Authorization: Bearer <token>
Idempotency-Key: <客户端生成的UUID>
Content-Type: application/json

{
  "playerId": 5,
  "playerBuildingId": 801
}
```

收取单个场景：

```http
POST /api/production/collect-all
Authorization: Bearer <token>
Idempotency-Key: <客户端生成的UUID>
Content-Type: application/json

{
  "playerId": 5,
  "sceneId": 1
}
```

收取全部场景时只提交 `playerId`。响应 `collectScope` 稳定值：

| 值 | 语义 | 响应 `sceneId` |
| --- | --- | --- |
| `BUILDING` | 单建筑收取 | 建筑所属场景 |
| `SCENE` | 单场景全部建筑 | 对应场景 |
| `ALL_SCENES` | 玩家所有场景 | `null` |

`Idempotency-Key` 最长 64 位，只允许字母、数字、点、下划线、冒号和短横线。一次点击生成一次 UUID，网络超时重试必须复用同一 Key；服务端会保存并原样重放首次成功的完整响应。相同 Key 改成其他建筑、场景或范围会被拒绝。旧客户端省略 Header 时仍可调用，但无法安全处理“结果未知后的主动重试”。

### 41.6 WebSocket 增量事件

连接 `/api/ws/realtime` 后可收到：

| `type` | 核心字段 | Unity 处理 |
| --- | --- | --- |
| `LAND_SCENE_UNLOCKED` | `sourceSceneId, sceneId, configVersion, minimumClientVersionCode, refreshRequired` | `refreshRequired=true` 时刷新场景列表并播放解锁效果；版本不足时显示更新提示，不直接进入新场景 |
| `LAND_SCENE_SWITCHED` | `sceneId, sceneKey, sceneName, configVersion, minimumClientVersionCode, refreshRequired` | `refreshRequired=true` 时重新查询当前场景地图；版本不足时进入统一更新引导 |
| `LAND_SLOT_STATE_CHANGED` | 完整 `LandSlotStateResponse` 字段，并额外包含 `refreshRequired=true` | 兼容旧 Unity 继续读取原格子字段；格子购买或净化可能同时改变邻格的可购买状态，新 Unity 收到后必须重新 `GET` 当前场景整图，不能只替换目标格 |
| `PRODUCTION_COLLECTED` | `sceneId, collectScope, idempotencyKey, collectedItems` | 刷新对应生产列表和背包 |

WebSocket 只用于及时刷新，HTTP 响应和重新查询始终是权威状态。事件均在数据库事务提交后发送。

`LAND_SLOT_STATE_CHANGED` 不会缩减为三个字段。其顶层仍保留坐标、等级、净化、购买、解锁方式、价格、规则指纹和 `sceneConfigVersion` 等完整格子状态，并新增 `refreshRequired=true`。这样旧 Unity 不会因字段被删除而失效，新 Unity 则应在收到事件后重新拉取整图，以同步相邻格的 `purchaseAvailable` 等联动状态。

### 41.7 管理端配置

当前入口：`配置中心 → 基础资料 → 经营场景目录`。

```text
moduleCode = LAND_SCENE_CATALOG
configKey  = WORLD
```

每个场景可配置：显示名称、8 个不递减等级门槛、单个付费格金币价格、背景 Key、地形 Key、BGM Key、图标和启停。规则如下：

- 场景固定 8×4 共 32 格，不允许运营改变格子尺寸。
- 已发布场景不能删除，不能修改 `sceneId / sceneKey / sortOrder`，新场景只能使用递增 ID 追加到末尾。
- 已有玩家进入的场景不能停用，也不能提高等级门槛；允许降低门槛、调整价格和 Unity 资源。
- `paidSlotGoldCost` 范围 `1..1000000000`；保存草稿不生效，只有校验并发布后才原子替换正式目录。
- 旧 `LAND_MAP_POLICY / WORLD` 只读保留固定布局，不能继续修改价格。

### 41.8 数据库与验收

V62 新增 `land_scene_config`、`player_scene`、`player_production_collect_record`，并为地图、地块、购买和净化记录增加 `scene_id`，固定校验和为 `-1155339352`。V63 不修改 V62，而是回填旧单建筑收取记录的场景快照、清理全场景收取的冗余场景值，并收紧数据库约束，固定校验和为 `-2076174813`。V64 增加场景永久成就宝箱、奖励权益快照和随机 NPC 发放流水，固定校验和为 `51817526`。以上是 V62-V64 发布时的历史边界；当前 V1-V73 禁止修改，下一迁移必须使用 V74。

验收日期：2026-08-04（Asia/Shanghai）。

- 后端 `mvn -q test`：712 项，0 failure、0 error；63 项外部环境用例按条件跳过。
- 真实 MySQL 8 临时库：先执行到 V62、写入旧格式收取记录，再升级到 V63，回填和严格约束验证通过，二次迁移为 0。
- 管理端 `npm.cmd run build`：TypeScript 与 Vite 构建通过，共转换 3549 个模块。
- V62/V63 CRC32 防篡改测试与 `git diff --check` 通过。

## 42. 场景成就与开辟里程碑宝箱（V64/V89）

### 42.1 最终规则

当前支持两种互不替代的永久场景成就：

- `SCENE_SLOT_UNLOCK_COUNT`：运营可按同一场景配置多个 1～32 格里程碑，例如 10、20、32；服务端只统计 `placementUnlocked=true` 的格子，不要求格子上已有建筑。
- `SCENE_FULLY_BUILT`：每个场景只能发布一个，目标固定 32；服务端同时满足以下两项后才把任务置为 `COMPLETED`：

1. 该场景固定 32 格全部返回 `placementUnlocked=true`。
2. 32 个不同地块上均存在状态为 `COMPLETED` 的建筑。

`SCENE_FULLY_BUILT` 的权威进度取“已解锁格数”和“已完成建筑覆盖格数”的较小值；`SCENE_SLOT_UNLOCK_COUNT` 直接取已解锁格数。Unity 不提交进度；购买、净化或建筑完成事务会实时核对，打开任务页时还会按当前数据库状态惰性修复上线前已经满足条件的玩家。一次操作跨过多个档位时，所有 `targetAmount <= 当前已开辟格数` 的未完成里程碑都会完成。

宝箱支持非负经验、多项固定 `ITEM`、多项固定 `EQUIPMENT`，以及 1 至 10 抽的必中奖随机 NPC 池。`ITEM` 可选择物品字典中启用的货币、资源、产品和 NPC 碎片；固定装备会创建独立 1 级仓库实例。完整 NPC 只能进入随机池，不能作为固定奖励。

### 42.2 Unity 查询与预览

```http
GET /api/tasks
Authorization: Bearer <player-token>
```

场景成就任务的关键数据（以下仍以 32 格全部建成为例）：

```json
{
  "id": 91001,
  "taskCode": "SCENE_HOME_FULLY_BUILT",
  "taskName": "家园建成宝箱",
  "description": "解锁家园全部地块并完成每格建筑",
  "taskCategory": "ACHIEVEMENT",
  "periodType": "LIFETIME",
  "periodKey": "LIFETIME",
  "objectiveType": "SCENE_FULLY_BUILT",
  "targetSceneId": 1,
  "targetSceneKey": "HOME",
  "targetSceneName": "家园",
  "targetAmount": 32,
  "progressAmount": 32,
  "experienceReward": 500,
  "randomNpcPoolName": "家园建成NPC池",
  "randomNpcDrawCount": 1,
  "fixedRewards": [
    {
      "rewardSeq": 1,
      "rewardType": "ITEM",
      "assetId": 2,
      "assetKey": "wood",
      "assetName": "木材",
      "assetIcon": "wood",
      "assetCategory": "RESOURCE",
      "assetQuality": null,
      "quantity": 5000,
      "probabilityUnits": null,
      "probabilityPercent": null,
      "duplicateCompensation": null
    }
  ],
  "randomNpcCandidates": [
    {
      "rewardSeq": 1,
      "rewardType": "NPC",
      "assetId": 400001,
      "assetKey": "sword_master",
      "assetName": "剑圣",
      "assetIcon": "npc/sword_master",
      "assetCategory": "NPC",
      "assetQuality": "EPIC",
      "quantity": 1,
      "probabilityUnits": 100000000,
      "probabilityPercent": 100,
      "duplicateCompensation": {
        "itemId": 200101,
        "itemKey": "npc_fragment_sword_master",
        "itemName": "剑圣碎片",
        "itemIcon": "item/npc_fragment_sword_master",
        "quantity": 8
      }
    }
  ],
  "status": "COMPLETED",
  "canClaim": true,
  "completedTime": "2026-08-04 18:29:58",
  "claimedTime": null
}
```

`probabilityUnits` 是服务端用于判定的一亿分比整数，所有启用候选必须精确合计 `100000000`；`probabilityPercent` 只用于 UI 展示。品质固定为 `COMMON/FINE/EXCELLENT/EPIC/LEGENDARY`，统一显示为普通/优秀/稀有/史诗/传说；英文编码保持稳定，不因展示名称调整而迁移历史快照。`fixedRewards` 和 `randomNpcCandidates` 均为玩家任务创建时冻结的权益快照。

若运营配置“开辟 10 格宝箱”，接口路径和字段完全相同，只需识别：

```json
{
  "objectiveType": "SCENE_SLOT_UNLOCK_COUNT",
  "targetSceneId": 1,
  "targetAmount": 10,
  "progressAmount": 7,
  "status": "IN_PROGRESS",
  "canClaim": false
}
```

Unity 按 `objectiveType + targetSceneId + targetAmount` 区分同一场景的多个宝箱；不新增开地进度上报接口，也不能根据本地动画次数自行累加进度。

### 42.3 领取和实际抽取结果

```http
POST /api/tasks/{playerTaskId}/claim
Authorization: Bearer <player-token>
```

请求体为空，`playerTaskId` 必须来自任务列表。随机数由后端 `SecureRandom` 生成，Unity 不能提交 NPC、品质、概率、数量或随机结果。`rewardGrants` 中每项都是实际到账结果：

```json
{
  "task": {
    "id": 91001,
    "status": "CLAIMED",
    "canClaim": false,
    "claimedTime": "2026-08-04 18:31:00"
  },
  "experienceGrant": {
    "playerId": 1,
    "sourceType": "TASK_REWARD",
    "sourceId": "91001",
    "requestedExperience": 500,
    "grantedExperience": 500,
    "beforeExperience": 1000,
    "afterExperience": 1500,
    "beforeLevel": 3,
    "afterLevel": 4,
    "leveledUp": true,
    "maxLevel": false,
    "currentLevelExperience": 200,
    "requiredExperienceForNextLevel": 800,
    "experienceProgressBps": 2500,
    "curveRevision": 1
  },
  "rewardGrants": [
    {
      "grantSeq": 2,
      "rewardSource": "RANDOM_NPC",
      "drawIndex": 1,
      "selectedNpcId": 400001,
      "selectedNpcCode": "sword_master",
      "selectedNpcName": "剑圣",
      "selectedNpcIcon": "npc/sword_master",
      "selectedNpcQuality": "EPIC",
      "grantedType": "NPC",
      "grantedAssetId": 400001,
      "grantedAssetKey": "sword_master",
      "grantedAssetName": "剑圣",
      "grantedAssetIcon": "npc/sword_master",
      "grantedAssetCategory": "NPC",
      "grantedAssetQuality": "EPIC",
      "grantedQuantity": 1,
      "duplicateNpcConverted": false,
      "playerNpcInstanceId": 52001
    }
  ]
}
```

若玩家已拥有抽中的 NPC，或同次多抽再次抽中刚获得的 NPC，则 `selectedNpc*` 仍表示抽中的角色，`grantedType=ITEM`、`duplicateNpcConverted=true`，`grantedAsset*` 和 `grantedQuantity` 表示实际到账的专属碎片。Unity 应按 `rewardGrants` 播放到账动画，不能按候选概率自行推导结果。

`playerTaskId` 本身是稳定幂等依据。首次领取会在同一数据库事务中保存随机决策、发经验和资产、创建 NPC/装备实例并标记任务已领取；任一步失败全部回滚。网络超时后可使用同一 ID 直接重试，服务端返回首次经验和随机结果，不会再次抽取或发奖。

### 42.4 管理端配置

入口：`配置中心 -> 玩家任务`。

```text
moduleCode = PLAYER_TASK
configKey  = TASKS
```

页面点击“新增场景宝箱”后，可先选择“开辟指定格数”或“32 格全部建成”，再配置目标场景、名称、说明、经验、固定奖励、随机 NPC 池、抽数、概率、排序和启停。资源、装备及 NPC 都必须从正式基础资料字典选择；名称、图标、品质和重复碎片规则由服务端发布时重新解析，页面不可伪造。

发布护栏：

1. 场景成就周期固定 `LIFETIME`；“开辟指定格数”允许 1～32，“32 格全部建成”固定 32 且不可编辑。
2. 唯一身份是 `objectiveType + targetSceneId + targetAmount`：同一场景可配置 10、20、32 等多个开辟档位，也可让“开辟32格”和“全部建成32格”并存，但完全相同的三元组会被拒绝。
3. 旧 `SCENE_FULLY_BUILT` 仍至少包含一项固定 `ITEM` 且必须配置随机 NPC 池；`SCENE_SLOT_UNLOCK_COUNT` 可按活动设计选择经验、固定资产或随机 NPC 池，完整 NPC 仍只能放入随机池。
4. 随机池 1 至 100 个候选、1 至 10 抽；每项概率大于 0，启用项精确合计 100%。
5. NPC 必须启用、允许获取并绑定有效专属碎片；装备和 NPC 品质只允许五档固定编码。
6. 已发布任务不能删除或修改 `taskCode`，停止使用应设置 `enabled=false`。
7. 发布新版本只影响以后物化的任务，已有玩家继续保留原场景、目标、奖励、概率、品质及重复补偿快照。

NPC 品质可在宝箱正式版本发布前通过 `NPC_CATALOG` 修改。正式宝箱或玩家权益快照已经引用 NPC 后，引用检查会阻断品质修改；需要改变语义时应创建新的 NPC 版本，不能静默改写玩家已看到的奖励。

### 42.5 错误码和客户端处理

| code | 说明 | Unity 处理 |
| ---: | --- | --- |
| `6601` | 玩家任务不存在 | 刷新任务列表 |
| `6602` | 任务尚未完成 | 保持未领取并刷新进度 |
| `6603` | 玩家任务配置错误 | 不重试并记录服务端排障信息 |
| `6604` | 玩家任务状态已变化 | 重新查询任务列表 |
| `6605` | 任务进度幂等冲突 | 正常客户端不会触发 |
| `6606` | 任务奖励快照损坏 | 不重试，提示稍后再试 |
| `6607` | 任务奖励发放失败 | 保留同一任务 ID，稍后重试 |

推荐流程：登录或进入任务页调用 `GET /api/tasks`，按 `taskCategory` 分页签，详情直接展示两个奖励数组；领取时锁定按钮，成功后按 `rewardGrants` 播放动画，再刷新玩家资料、背包、装备和 NPC 列表。仓库中的完整独立说明位于 `docs/player-task-achievement-api.md`。

### 42.6 数据库边界

V64 新增正式固定奖励、正式随机 NPC 池、玩家权益快照和实际发放流水四张表，并扩展任务配置/实例为永久成就。V89 不修改 V64/V87/V88，而是新增 `SCENE_SLOT_UNLOCK_COUNT`，把场景成就唯一键扩展为 `(objective_type, target_scene_id, target_amount)`，并同步加固配置、实例和进度事件的 CHECK；固定 CRC32 为 `-1964148448`。V64 固定 CRC32 为 `51817526`；当前 V1-V104 已冻结，下一份数据库迁移必须从 V105 开始，禁止执行 `flyway repair` 掩盖漂移。

### 42.7 实施验收

验收日期：2026-08-04（Asia/Shanghai）。

- 默认环境执行 `mvn -q test`：共 712 项，0 failure、0 error；63 项依赖真实 MySQL 等外部环境的用例按条件跳过。
- 启用真实 MySQL 集成测试执行 `mvn -q test`：共 712 项，0 failure、0 error、9 skipped；V1-V64 空库迁移成功（V64 阶段历史验收记录）。
- 定向执行并发、配置持久化和 Flyway 集成测试：共 52 项，0 failure、0 error。
- 管理端执行 `npm.cmd run build`：TypeScript 与 Vite 构建通过，共转换 3549 个模块。
- `git diff --check` 与 V64 固定校验和测试通过；V64 CRC32 为 `51817526`（V64 阶段历史验收记录）。当前 V1-V73 已冻结，下一迁移必须从 V74 开始。

## 43. 隐私政策、授权撤回与账号注销（V65-V66）

本文档对应 Flyway V65-V66 和 `modules/privacy`。后端提供自有隐私政策版本、授权凭证、授权撤回、注销冷静期及最终匿名化闭环；Unity/微信小游戏端仍须调用微信官方隐私授权 API。两者缺一不可。

基础地址：`http://localhost:8080/api`

时间格式：`yyyy-MM-dd HH:mm:ss`，业务时间统一为北京时间。

### 一、职责边界

#### Java 后端已经实现

- 保存不可变的正式隐私政策版本和正文 SHA-256。
- 登录前公开返回当前政策元数据和纯文本正文。
- 记录玩家 `AGREED / WITHDRAWN` 不可变授权事件及客户端 UUID 幂等键。
- 新微信玩家没有明确同意当前政策时拒绝创建账号。
- 授权失效后只签发账号管理令牌，普通游戏接口返回 `7702`。
- 提供可撤销的账号注销申请、管理端冷静期、支付阻断、后台租约和失败重试。
- 注销完成后删除微信绑定、令牌、直接身份和社交数据，随机匿名化用户主行，并异步清理头像对象。
- 账号进入冷静期、撤回授权或完成注销后，跨节点断开现有实时连接。

#### Unity/微信桥接仍须实现

- 按当前微信小游戏基础库调用官方隐私能力，例如查询隐私设置、打开隐私协议、触发隐私授权和处理授权回调。
- 只有玩家主动点击同意后，才把 `privacyAuthorized=true` 发送给 Java 后端。
- Java 后端不能替代微信平台的官方隐私弹窗，也不能代替开发者在微信公众平台填写《用户隐私保护指引》。
- 隐私政策正文必须由实际运营主体和法律顾问审核；管理端不会自动生成可直接上线的法律文本。

V65 不内置虚假的默认政策。首次部署必须先在管理端发布正式政策，否则政策查询和登录返回 `7701`。

V65 只建立基础状态与表结构，固定校验和为 `-1144973114`；V66 在不修改 V65 的前提下回填注销请求指纹，并增加 HTTPS、UUID 和头像注销清理约束，固定校验和为 `-1142994506`。V66 不改变任何 Unity 请求或响应字段。V67-V104 已分别交付后续功能且均已冻结；下一次数据库变化必须从 V105 新建迁移。

### 二、公开政策接口

以下接口不需要 token。

#### 2.1 查询当前政策元数据

```http
GET /api/privacy/policy/current
```

成功响应：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "policyVersion": "2026.08.05",
    "title": "游戏隐私保护指引",
    "summary": "首次正式发布",
    "contentUrl": "https://game.example.com/privacy/2026.08.05",
    "contentSha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
    "operatorName": "示例科技有限公司",
    "contactEmail": "privacy@example.com",
    "effectiveTime": "2026-08-05 12:00:00",
    "requiresReconsent": true
  }
}
```

`contentUrl` 可为 `null`。Unity 不应自行猜测政策版本，也不能把客户端硬编码版本当作提交依据。

#### 2.2 查询当前政策正文

```http
GET /api/privacy/policy/current/content
```

成功响应：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "policyVersion": "2026.08.05",
    "content": "正式隐私政策纯文本正文……",
    "contentSha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
  }
}
```

客户端至少核对元数据和正文中的 `policyVersion` 相同。需要完整性校验时，对 UTF-8 正文计算 SHA-256 并与 `contentSha256` 比较。

### 三、微信登录改动

#### 3.1 请求

```http
POST /api/user/wx-login
Content-Type: application/json
```

```json
{
  "code": "wx.login 返回的一次性 code",
  "nickname": "玩家主动提供的可选昵称",
  "privacyAuthorized": true,
  "privacyPolicyVersion": "2026.08.05",
  "privacyConsentRequestId": "2257b487-e1bc-4d02-8e56-552e6da734d4",
  "clientVersion": "1.0.0",
  "recoverCancellation": false,
  "cancellationRecoveryRequestId": null
}
```

| 字段 | 必填 | 说明 |
| --- | --- | --- |
| `code` | 是 | 每次调用后端登录前重新调用 `wx.login` 获取；最长 128 字符 |
| `nickname` | 否 | 只用于首次创建玩家，后端仍执行昵称格式和唯一性校验 |
| `privacyAuthorized` | 条件必填 | 新玩家必须为 `true`；已有未授权玩家可为 `false` 并只进入账号管理页 |
| `privacyPolicyVersion` | 条件必填 | 新建账号或本次主动同意时，提交玩家实际阅读的当前版本 |
| `privacyConsentRequestId` | 条件必填 | 新建账号或本次主动同意时提交；网络重试必须复用 |
| `clientVersion` | 否 | 最长 64 字符的 Unity/小游戏版本快照 |
| `recoverCancellation` | 否 | 玩家在冷静期登录页明确选择恢复账号时才为 `true` |
| `cancellationRecoveryRequestId` | 条件必填 | 恢复账号动作的独立 UUID，不能复用授权或注销请求号 |

对于已有且授权仍有效的玩家，隐私字段可以不传；推荐客户端仍先读取当前政策，以便及时处理重签。已有但授权无效的玩家可以不重新同意，后端会签发受限管理令牌，使其仍可查询授权或申请注销；只有进入游戏才需要重新同意。新玩家若未授权，后端返回 HTTP 403、`code=7702`，不会创建半成品玩家。

#### 3.2 正常登录响应

```json
{
  "code": 200,
  "message": "微信登录成功",
  "data": {
    "token": "player-token",
    "expiresIn": 604800,
    "user": {
      "id": 10001,
      "nickname": "玩家昵称"
    },
    "accountStatus": "ACTIVE",
    "accountManagementOnly": false,
    "privacyConsentRequired": false,
    "privacyPolicyVersion": "2026.08.05"
  }
}
```

`user` 仍包含原登录响应的完整玩家字段，此处仅省略展示。

#### 3.3 受限账号管理响应

授权已撤回、政策要求重新授权或账号处于注销冷静期时，登录成功只代表身份确认成功：

```json
{
  "code": 200,
  "message": "微信登录成功",
  "data": {
    "token": "management-token",
    "expiresIn": 604800,
    "user": null,
    "accountStatus": "CANCELLATION_PENDING",
    "accountManagementOnly": true,
    "privacyConsentRequired": false,
    "privacyPolicyVersion": "2026.08.05"
  }
}
```

Unity 必须先判断 `accountManagementOnly`，不能在 `user=null` 时继续进入主场景。该 token 只能用于 `/user/privacy/**`；调用普通游戏接口会返回 `7702` 或 `7705`。

#### 3.4 推荐首次登录时序

1. 查询 `/client/runtime-policy`，先处理维护和强制更新。
2. 查询 `/privacy/policy/current` 和 `/privacy/policy/current/content`。
3. 通过微信桥接读取并处理微信官方隐私授权状态。
4. 玩家主动同意后生成 UUID v4，并保存为本次授权动作请求号。
5. 调用 `wx.login` 获取新的临时 code。
6. 调用 `/user/wx-login`，同时提交政策版本和授权请求号。
7. HTTP 超时且无法确认结果时，重新获取微信 code，但复用同一个 `privacyConsentRequestId`。
8. 收到 `7703` 时重新读取政策、重新展示并生成新的授权动作 UUID。
9. 根据 `accountManagementOnly` 决定进入主场景还是账号管理页。

### 四、授权管理接口

这些接口都使用普通或受限账号管理 token：

```http
Authorization: Bearer <player-or-management-token>
```

#### 4.1 查询授权状态

```http
GET /api/user/privacy/consent
```

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "accountStatus": "ACTIVE",
    "currentPolicyVersion": "2026.08.05",
    "consentValid": true,
    "latestAction": "AGREED",
    "latestPolicyVersion": "2026.08.05",
    "latestOccurredTime": "2026-08-05 12:10:00"
  }
}
```

从未产生授权事件时，`latestAction/latestPolicyVersion/latestOccurredTime` 均为 `null`。

#### 4.2 同意当前政策

```http
POST /api/user/privacy/consent
Authorization: Bearer <token>
Content-Type: application/json
```

```json
{
  "policyVersion": "2026.08.05",
  "requestId": "2257b487-e1bc-4d02-8e56-552e6da734d4",
  "clientVersion": "1.0.0"
}
```

成功后原 token 可重新访问普通接口，但实时连接已被安全断开时必须重新连接。相同 `requestId` 和相同内容重复提交返回第一次操作结果；同一 UUID 改政策、来源或客户端版本返回 `7704`。

注销冷静期账号不能单独调用同意接口，须先撤销注销或通过登录恢复账号。

#### 4.3 撤回授权

```http
POST /api/user/privacy/consent/withdraw
Authorization: Bearer <token>
Content-Type: application/json
```

```json
{
  "requestId": "f680bd6c-e320-4a38-906d-172666526646",
  "clientVersion": "1.0.0"
}
```

成功后：

- 当前普通业务权限立即失效，跨节点实时连接会关闭。
- 同一个 token 仍可查询隐私状态、重新同意政策或申请注销。
- 撤回授权不会自动删除账号；删除账号必须另行提交注销申请。

### 五、账号注销接口

#### 5.1 查询最近一次注销状态

```http
GET /api/user/privacy/account-cancellation
Authorization: Bearer <token>
```

没有历史申请时：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "cancellationId": null,
    "accountStatus": "ACTIVE",
    "cancellationStatus": null,
    "requestedTime": null,
    "scheduledTime": null,
    "cancelledTime": null,
    "completedTime": null,
    "cancellable": false
  }
}
```

#### 5.2 申请注销

```http
POST /api/user/privacy/account-cancellation
Authorization: Bearer <token>
Content-Type: application/json
```

```json
{
  "requestId": "0f857b11-4daf-4a84-b5ec-fda5d210f38f",
  "reason": "不再使用"
}
```

`requestId` 必须是 UUID v1-v5，推荐 UUID v4。`reason` 可不传，最多 500 字符。相同请求号和规范化后的相同原因可安全重放；相同请求号改原因返回 `7704`。

成功响应：

```json
{
  "code": 200,
  "message": "账号已进入注销冷静期",
  "data": {
    "cancellationId": 88,
    "accountStatus": "CANCELLATION_PENDING",
    "cancellationStatus": "PENDING",
    "requestedTime": "2026-08-05 12:00:00",
    "scheduledTime": "2026-08-08 12:00:00",
    "cancelledTime": null,
    "completedTime": null,
    "cancellable": true
  }
}
```

申请前后端会锁定玩家聚合并检查未支付订单、待发货任务和退款流程。存在未闭环支付时返回 `7707`，不会创建注销任务。申请成功后普通游戏和新支付建单立即被禁止。

#### 5.3 撤销注销

```http
POST /api/user/privacy/account-cancellation/cancel
Authorization: Bearer <management-token>
Content-Type: application/json
```

```json
{
  "requestId": "e34e28ed-106a-40b7-bc82-cbca3ea4e799"
}
```

相同撤销 UUID 重试会返回第一次结果。成功后 `accountStatus=ACTIVE`、`cancellationStatus=CANCELLED`，原 token 可恢复普通接口访问；Unity 应重新建立实时连接并刷新 `/user/me`。

#### 5.4 在微信登录页恢复冷静期账号

玩家冷静期重新登录时，后端默认只返回受限账号管理响应，不会因一次登录自动恢复。只有玩家明确点击“恢复账号”后才提交：

```json
{
  "code": "新的 wx.login code",
  "privacyAuthorized": false,
  "privacyPolicyVersion": null,
  "privacyConsentRequestId": null,
  "clientVersion": "1.0.0",
  "recoverCancellation": true,
  "cancellationRecoveryRequestId": "9c5790c5-e3a8-4f9a-ae53-eb09183de3a8"
}
```

若当前授权同时失效且玩家希望恢复后直接进入游戏，可在同一请求中提交新确认的政策版本和独立 `privacyConsentRequestId`，恢复注销和新增授权会处于同一数据库事务。若玩家不重新授权，账号仍可恢复，但响应保持 `accountManagementOnly=true`；玩家可以继续查看隐私状态、再次申请注销或稍后单独同意，不能进入普通游戏业务。

### 六、状态枚举

账号状态：

| 值 | 含义 |
| --- | --- |
| `ACTIVE` | 正常账号；仍须拥有有效授权才能访问普通业务 |
| `CANCELLATION_PENDING` | 注销冷静期或后台处理中，只允许账号管理接口 |
| `CLOSED` | 已完成不可逆匿名化，旧 token 和旧微信绑定均不可用 |

注销任务状态：

| 值 | 含义 |
| --- | --- |
| `PENDING` | 冷静期中或等待下一次后台重试 |
| `PROCESSING` | 某个服务节点持有短租约并正在处理 |
| `CANCELLED` | 玩家已撤销，账号恢复正常 |
| `COMPLETED` | 直接身份已擦除，用户主行已匿名化 |
| `MANUAL_REVIEW` | 达到自动重试上限，需要客服或运维处理；尚未做部分匿名化 |

### 七、最终注销效果

冷静期结束后，后台任务会再次锁定玩家并复核支付状态。单次处理在一个事务中完成：

- 删除微信 `openid/unionid` 绑定和全部登录 token。
- 删除好友关系、好友申请、昵称修改历史、站内提醒、订阅设置、提醒占位、在线状态和头像每日用量。
- 将头像对象键写入清理 Outbox，事务提交后由对象存储任务异步删除。
- 用安全随机值替换用户名、密码和昵称，清空头像及经济冻结原因，账号状态改为 `CLOSED`。
- 清空注销原因并把任务改为 `COMPLETED`。

支付订单、退款、经济流水、游戏资产流水、授权事件和配置审计等依法或为安全审计需要保留的记录继续使用匿名化后的内部 `player_id`，不再保留微信绑定和公开昵称。具体保存期限必须在正式隐私政策和企业数据保留制度中明确。

注销完成后无法恢复旧进度。玩家以后用同一微信账号重新登录时，会在重新授权后创建一个新的游戏账号。

### 八、错误码和 HTTP 状态

| HTTP | code | 含义 | Unity 处理 |
| ---: | ---: | --- | --- |
| 503 | `7701` | 未发布正式政策或政策不可用 | 阻止登录，提示稍后重试并通知运营 |
| 403 | `7702` | 当前授权无效 | 进入政策确认页，不要进入主场景 |
| 409 | `7703` | 提交期间政策版本变化 | 重新拉取并重新征得同意 |
| 409 | `7704` | UUID 被用于不同内容 | 停止自动重试，为新用户动作生成新 UUID |
| 403 | `7705` | 账号在注销冷静期 | 进入账号管理页，查询或撤销注销 |
| 409 | `7706` | 没有可撤销的申请 | 刷新注销状态 |
| 409 | `7707` | 存在未结支付/发货/退款 | 提示待流程结束后再申请 |
| 410 | `7708` | 账号已注销 | 清除本地 token 和旧进度缓存 |
| 409 | `7709` | 注销状态并发变化 | 延迟后查询状态，不盲目重复新请求 |
| 503 | `7710` | 自动处理达到上限 | 展示客服入口，不继续高频重试 |

无论 HTTP 状态如何，Unity 都必须解析统一 JSON 并以 `code == 200` 判断业务成功。

### 九、Unity 数据模型与关键判断

```csharp
using System;

[Serializable]
public sealed class PrivacyPolicyDto
{
    public string policyVersion;
    public string title;
    public string summary;
    public string contentUrl;
    public string contentSha256;
    public string operatorName;
    public string contactEmail;
    public string effectiveTime;
    public bool requiresReconsent;
}

[Serializable]
public sealed class WechatLoginRequestDto
{
    public string code;
    public string nickname;
    public bool privacyAuthorized;
    public string privacyPolicyVersion;
    public string privacyConsentRequestId;
    public string clientVersion;
    public bool recoverCancellation;
    public string cancellationRecoveryRequestId;
}

[Serializable]
public sealed class LoginDataDto
{
    public string token;
    public long expiresIn;
    public UserDto user;
    public string accountStatus;
    public bool accountManagementOnly;
    public bool privacyConsentRequired;
    public string privacyPolicyVersion;
}

[Serializable]
public sealed class AccountCancellationDto
{
    public long cancellationId;
    public string accountStatus;
    public string cancellationStatus;
    public string requestedTime;
    public string scheduledTime;
    public string cancelledTime;
    public string completedTime;
    public bool cancellable;
}
```

`cancellationId` 在后端可为 `null`。若继续使用 Unity `JsonUtility`，建议给统一响应的 `data` 使用能表达可空值的 JSON 库，或同时根据 `cancellationStatus == null` 判断从未申请，不要依赖默认的 `long=0`。

UUID 生成：

```csharp
string requestId = Guid.NewGuid().ToString("D").ToLowerInvariant();
```

登录成功后的第一处分支必须是：

```csharp
if (loginData.accountManagementOnly)
{
    OpenAccountManagementScreen(loginData);
    return;
}

if (loginData.user == null)
{
    ShowFatalProtocolError();
    return;
}

EnterGame(loginData.user);
```

### 十、管理端配置

模块编码：`PRIVACY_COMPLIANCE`

配置键：`POLICY`

只有 `CONFIG_ADMIN` 可保存、丢弃和发布，其他运营角色只读。页面字段：

| 字段 | 规则 |
| --- | --- |
| `policyVersion` | 1 至 64 位稳定版本，只允许字母、数字、点、横线和下划线；使用后不可复用 |
| `title` | 必填，最多 200 字符 |
| `summary` | 必填，最多 1000 字符 |
| `content` | 必填纯文本，最多 200000 字符；服务端统一换行并计算 SHA-256 |
| `contentUrl` | 可空；填写时必须是无用户信息的 HTTPS 地址 |
| `operatorName` | 实际个人信息处理者名称，最多 200 字符 |
| `contactEmail` | 实际隐私事务联系邮箱 |
| `effectiveTime` | 立即发布模式下不能晚于发布时间 |
| `requiresReconsent` | 正文变化时强制为 `true` |
| `cancellationCoolingHours` | 24 至 720 小时，发布后只影响新申请 |
| `policyId/contentSha256/configVersion` | 服务端只读字段，不应由运营手工修改 |

正式政策不可原地覆盖。需要恢复旧正文时也必须填写一个从未使用的新 `policyVersion` 并重新发布，不能直接回滚到已使用版本。这样可以保证历史同意凭证始终指向真实、不可变的正文。

首次上线操作顺序：

1. 备份数据库，停止旧版本写入并按第 65.7 节确认两张头像清单审计表为空，只启动一个当前版本后端节点并让 Flyway 从数据库现有版本迁移至当前最高版本 V104；已有 V92 数据库会先校验原始 CRC32 `1615105684`，再自动执行 V93 至 V104，禁止使用 `repair`。
2. 登录管理端，进入“客户端与通知 > 隐私与账号注销”。
3. 填写经过审核的真实主体、邮箱和正文，保存草稿并校验。
4. 发布后验证两个公开政策接口均返回 200，SHA-256 一致。
5. 在微信公众平台完成隐私指引声明和审核，Unity 完成官方隐私 API 桥接。
6. 最后开放正式微信登录流量。

### 十一、后台任务配置与监控

业务冷静期只能在管理端维护。部署环境变量只控制后台执行器：

```text
PRIVACY_CANCELLATION_ENABLED=true
PRIVACY_CANCELLATION_BATCH_SIZE=20
PRIVACY_CANCELLATION_LEASE_SECONDS=120
PRIVACY_CANCELLATION_MAX_ATTEMPTS=100
PRIVACY_CANCELLATION_RETRY_BASE_SECONDS=30
PRIVACY_CANCELLATION_RETRY_MAX_SECONDS=3600
PRIVACY_CANCELLATION_INITIAL_DELAY_MS=30000
PRIVACY_CANCELLATION_FIXED_DELAY_MS=30000
```

多节点不需要额外分布式锁。任务通过 MySQL `FOR UPDATE SKIP LOCKED` 领取，以租约所有者条件完成或重试；单个玩家匿名化失败会回滚，不会留下半删数据。运维应监控：

- `player_account_cancellation.status='MANUAL_REVIEW'` 的数量。
- 租约过期后仍长期处于 `PROCESSING` 的任务。
- `last_error` 重复出现的数据库、对象存储或支付锁错误。
- `player_avatar_cleanup_outbox` 中 `ACCOUNT_CLOSURE` 清理任务的积压。
- 当前正式政策指针、配置版本和政策正文 SHA-256。

### 十二、验收清单

1. 未发布政策时公开政策和登录均返回 `7701`，不会自动使用示例文本。
2. 新玩家不授权返回 `7702`，数据库不残留用户、地图或微信绑定。
3. 玩家同意后可正常登录；相同 UUID 重试不重复写授权事件。
4. 发布要求重签的新政策后，旧 token 调普通接口返回 `7702`。
5. 撤回授权后实时连接断开，但同 token 可重新同意或申请注销。
6. 有未结支付、发货或退款时注销返回 `7707`。
7. 申请注销后普通接口和新支付建单均返回 `7705`。
8. 冷静期内撤销成功并恢复普通接口；登录不会未经确认自动恢复。
9. 多节点同时扫描同一任务时只允许一个租约持有者完成。
10. 注销事务故障时所有删除和匿名化一起回滚，重试后最终完成。
11. 完成后旧 token、旧微信绑定和旧头像不可用，账务审计仍可按匿名内部 ID 对账。
12. `mvn -q test`、MySQL 8 Flyway 集成测试、管理端构建和 `git diff --check` 全部通过。
13. V65 校验和为 `-1144973114`、V66 校验和为 `-1142994506`，并验证带旧注销记录的 V65 数据库可直接升级到 V66。

## 44. 游戏好友每日体力赠礼（V67）

### 44.1 功能结果

1. 只有 `player_friend` 中的正式游戏好友可以赠送，不读取微信官方好友关系。
2. 按北京时间自然日统计；A→B 每日一次，B→A 也每日一次，两个方向独立。
3. 体力由系统发放，不扣发送方体力，也不占原金币/物品赠礼额度。
4. 同方向同日重复调用幂等返回第一次结果，`replayed=true`，不会重复增加体力。
5. 接收方满体力时仍完整接收，可暂时超过运营上限；超额期间暂停自然恢复。
6. 数据库物理上限为 `1000000`，整笔礼物会越界时返回 HTTP 409、业务码 `6512`，体力和流水全部回滚。

V67 初始体力赠礼数值为 `5`，开关默认为关闭。必须先在管理端发布启用，玩家接口才允许发送。

### 44.2 Unity 好友列表改动

```http
GET /api/friends?limit=20
Authorization: Bearer <player-token>
```

每个好友新增：

```json
{
  "playerId": 20002,
  "nickname": "玩家乙",
  "dailyStaminaGiftSent": false,
  "dailyStaminaGiftReceived": true
}
```

| 字段 | 类型 | Unity 用途 |
| --- | --- | --- |
| `dailyStaminaGiftSent` | `bool` | 为 `true` 时今天不能再向该好友发送 |
| `dailyStaminaGiftReceived` | `bool` | 展示今天是否收到该好友体力 |

好友列表顶层新增：

```json
{
  "dailyStaminaGift": {
    "enabled": true,
    "periodKey": "20260806",
    "quantity": 5,
    "perFriendDailyLimit": 1,
    "sentCount": 3,
    "receivedCount": 2,
    "configVersion": 9
  }
}
```

`sentCount/receivedCount` 只用于统计展示，不是全局限额。第一版固定每位好友、每个方向、每天一次。Unity 必须以服务端 `periodKey` 和布尔字段为准，不按设备本地时区自行重置。

### 44.3 发送接口

```http
POST /api/friends/{friendPlayerId}/gifts/stamina
Authorization: Bearer <player-token>
```

无请求体，不传数量，也不传 `Idempotency-Key`。服务端按照发送者、接收者和北京时间日期自动幂等。

成功响应：

```json
{
  "code": 200,
  "message": "好友体力赠礼发送成功",
  "data": {
    "recordId": 88001,
    "senderPlayerId": 10001,
    "receiverPlayerId": 20002,
    "periodKey": "20260806",
    "quantity": 5,
    "receiverStaminaBefore": 98,
    "receiverStaminaAfter": 103,
    "maxStamina": 100,
    "overCap": true,
    "policyModeConfigId": 16001,
    "policyVersion": 12,
    "configVersion": 9,
    "sentTime": "2026-08-06T12:30:00",
    "replayed": false
  }
}
```

网络超时可以直接重试同一路径。同日已成功时返回同一个 `recordId` 和首次余额快照，只把 `replayed` 设为 `true`。首次成功后即使运营关闭赠礼或双方删除好友，原请求重放仍返回第一次结果，不重复发放。

推荐 Unity 流程：

1. 进入好友页读取 `dailyStaminaGift` 和每个好友的两个状态字段。
2. `enabled=false` 或 `dailyStaminaGiftSent=true` 时禁用发送按钮。
3. 点击后调用发送接口；成功立即把该好友的发送状态设为 `true`。
4. 展示“已为好友赠送 X 点体力”；接收方余额以其本人后续体力查询为准。
5. 进入挂机冒险页时重新调用 `GET /api/idle-adventure/players/{playerId}/stamina`，不要在客户端自行增加权威体力。

### 44.4 管理端配置

入口：

```text
配置中心 -> 游戏好友赠礼 -> 每日好友赠礼
moduleCode = PLAYER_FRIEND_GIFT
configKey  = DAILY_GIFT
```

完整配置新增两个字段：

```json
{
  "staminaGiftQuantity": 5,
  "staminaGiftEnabled": true
}
```

| 字段 | 范围 | 说明 |
| --- | ---: | --- |
| `staminaGiftQuantity` | 1 至 1000000 | 每个好友每次系统发放的体力 |
| `staminaGiftEnabled` | `true/false` | 只控制体力赠礼，不影响原物品赠礼 |

原 `enabled` 仍只控制金币/物品赠礼。两个开关可以分别启停，但共用完整配置的 `configVersion` 乐观锁。V67 之前保存的旧草稿没有体力字段，必须丢弃或从当前正式配置重新创建后再发布。

### 44.5 错误码

| code | HTTP | 说明 | Unity 处理 |
| ---: | ---: | --- | --- |
| `400` | 400 | 好友玩家 ID 无效 | 修正参数，不重试 |
| `401` | 401 | 未登录或令牌过期 | 重新登录 |
| `6701` | 200 | 不能给自己发送 | 隐藏自己的发送入口 |
| `6702` | 200 | 双方不是正式游戏好友 | 刷新好友列表 |
| `6721` | 200 | 好友每日体力赠礼未开放 | 刷新配置并隐藏入口 |
| `6722` | 200 | 体力赠礼正式配置错误 | 停止重试并提示稍后再试 |
| `6512` | 409 | 发放将超过物理上限 | 本次未发放，刷新体力并上报运营 |

Unity 必须同时判断 HTTP 状态和统一响应体 `code`。

### 44.6 数据库与验证

V67 增加：

- `player_friend_gift_config.stamina_gift_quantity`
- `player_friend_gift_config.stamina_gift_enabled`
- `player_friend_stamina_gift_record`

流水唯一键为 `(sender_player_id, receiver_player_id, period_key)`，并保存赠送数量、赠送前后体力、玩法版本和好友配置版本快照。发送事务按较小玩家 ID、较大玩家 ID、当日有向幂等流水、好友关系、挂机进度、活动会话和体力行的固定顺序加锁，体力更新与流水写入同事务提交。

V67 固定 CRC32 为 `459381446`。V68 固定 CRC32 为 `-1616972206`，V69 固定 CRC32 为 `423149015`。MySQL 8 空库已顺序升级至 V69，挂机加速表、物品类型快照、JSON 合法性、状态检查和玩家幂等唯一键均通过验证；完整 Maven 测试、Idle Adventure HTTP 契约测试、Flyway 集成测试、迁移校验和管理端生产构建均已通过。

## 45. 挂机冒险加速（V68）

本节对应 Flyway V68、V69 和当前 Java 实现。玩家可以消耗一次管理端配置的体力，立即领取一份按当前最高关卡和挂机上限计算的完整奖励；该操作与自然挂机结算、背包入账、状态更新和幂等审计在同一事务中完成。

### 45.1 Unity 接口

```http
POST /api/idle-adventure/players/{player_id}/afk-reward/accelerate
Authorization: Bearer <token>
Idempotency-Key: afk-accelerate-20260806-001
Content-Type: application/json
```

请求体为空。`Idempotency-Key` 必须为 1 至 64 个字符；网络超时、断线或 5xx 重试必须复用原值，不得生成新键。

成功响应的核心字段：

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

三组奖励均为服务端 `AssetReward` 快照；`natural_rewards` 是先结算的自然增量，`acceleration_rewards` 是完整挂机上限奖励，`total_rewards` 是按物品主键合并后的实际入账结果。Unity 以 `total_rewards`、`items` 和返回的 `stamina` 刷新界面，不在本地扣体力或发奖。

### 45.2 结算与失败语义

1. 服务端先按挂机状态保存的旧物品类型、产率和余数结算自然收益，再按当前玩法版本和最高关卡计算完整上限奖励。
2. 一次成功请求只扣一次 `afk_acceleration_stamina_cost`，并将自然奖励、加速奖励、体力、挂机状态和幂等流水一起提交。
3. 体力不足、玩法未开放、奖励物理上限不足、背包余额溢出或任一入账失败时整笔事务回滚，不推进挂机时间。
4. 相同玩家和幂等键重试只返回第一次完整响应，并将 `idempotent_replay` 设为 `true`；请求内容不一致返回 `6507`。
5. `6511` 表示体力不足，`6513` 表示挂机加速未开放，`6514` 表示奖励超过系统物理上限；业务错误必须同时按 HTTP 状态和响应 `code` 处理。

### 45.3 管理端配置

入口为“配置中心 -> 挂机冒险”，模块和配置键均为 `IDLE_ADVENTURE`。仍按“编辑 -> 校验 -> 保存草稿 -> 发布”执行，草稿不影响线上版本。

| 字段 | 规则 |
| --- | --- |
| `afkAccelerationEnabled` | 是否开放加速，默认关闭 |
| `afkAccelerationStaminaCost` | 每次成本 `1..maxStamina`；启用加速时必须启用体力且不得超过体力上限 |
| 加速预览 | 按最高关卡、产率、周期和挂机上限使用与服务端相同的整数公式实时计算周期数和奖励数量 |
| 发布校验 | 再次校验经济护栏、奖励物品类型和完整版本引用；失败不创建半成品 |

运营修改物品名称、图标或启停状态不会改变已落账奖励的快照。随机掉落和挂机奖励只能引用正式资产；服务端保护 `BIGINT` 数量上限并拒绝溢出。

### 45.4 数据库与验收

V68 新增 `player_idle_adventure_afk_acceleration_claim`，保存请求哈希、玩法版本、体力前后值、自然/加速/合并奖励快照、完整响应和业务时间；唯一键为 `(player_id, idempotency_key)`。V69 在 `player_idle_adventure_afk_state` 增加 `item_type_snapshot`，回填历史挂机状态并设置非空默认值和 CHECK 约束，防止配置切换后历史物品类型错配。JSON、状态、余额恒等式和外键约束由数据库与服务端双重校验。

V68 固定 CRC32 为 `-1616972206`，V69 固定 CRC32 为 `423149015`。完整 Maven 测试、28 条 Idle Adventure HTTP 契约测试、V65 至 V70 Flyway 集成测试、迁移校验和及管理端生产构建均已通过。

---

## 46. 钻石购买体力与 V70 上线说明

### 46.1 Unity 接入

不新增接口，继续使用：

```http
GET /api/diamond-shop/catalog
POST /api/diamond-shop/purchases
GET /api/diamond-shop/purchases/history
```

目录或订单中的体力商品固定返回：

```json
{
  "rewardType": "STAMINA",
  "rewardItemId": null,
  "rewardEquipmentId": null,
  "rewardKey": "stamina",
  "rewardName": "体力",
  "rewardIcon": null,
  "rewardCategory": "STAMINA"
}
```

Unity 需要给奖励类型枚举增加 `STAMINA` 分支。购买仍按 `rewardQuantityPerLot * lotCount` 计算实际体力，仍使用标准 UUID `Idempotency-Key`；网络超时必须复用原键。同键重放只返回首次订单，不会重复扣钻石或增加体力。成功后调用 `GET /api/idle-adventure/players/{playerId}/stamina`，不要使用订单的背包余额字段推算体力。

### 46.2 管理端配置

进入“配置中心 -> 钻石商城”，创建草稿并新增商品：

1. 奖励类型选择“体力”。
2. 配置每份体力、每份钻石价格、单笔最小/最大份数和北京时间每日体力上限。
3. 保存草稿、校验并发布；未发布或未启用的商品不能购买。
4. 单笔实际体力不得超过 `1000000`，每日上限不得小于单笔最大值；同一时间只能启用一条体力商品。

体力允许暂时超过当前运营上限，超额期间自然恢复暂停。玩家当前体力加本次奖励超过物理上限 `1000000` 时返回 `6512`，整笔事务回滚，不扣钻石、不写订单、不占每日额度。

### 46.3 Flyway 防错

V70 文件为 `V70__add_diamond_shop_stamina_reward.sql`，固定 CRC32 为 `1183825380`。该文件已经由校验和测试冻结，不得继续编辑；V71-V104 已由后续独立迁移交付，下一次数据库变化新建 V105。

上线顺序：备份数据库，停止旧版本写流量，只启动一个新版本节点执行 Flyway；确认 `flyway_schema_history` 中 `version=70`、`success=1`、`checksum=1183825380` 后，再启动其他节点。不要提前手工执行 V70 SQL，也不要用 `repair` 掩盖历史文件不一致。完整预检 SQL 见仓库 `docs/database-script-guide.md`。

---

## 47. 建筑工作时间与多配方生产（V71）

本节是 Unity 接入 V71 建筑生产功能的权威契约。所有接口都要求玩家令牌：

```http
Authorization: Bearer <player-token>
Content-Type: application/json
```

### 47.1 最终游戏效果

1. 每个玩家只有一份全局建筑工作时间；有剩余时间时，所有已完成且存在生效生产规则的建筑同时推进。
2. 工作时间耗尽时逻辑工作时钟冻结。已完成但未收取的产物和未完成周期进度都保留。
3. 玩家补充工作时间后从原半周期继续。例如 60 秒周期已经运行 30 秒，恢复后再运行 30 秒即可完成。
4. 玩家可消耗管理端正式配置的 `PRODUCT` 物品补充时间；V71 默认提供面包、烤肉和烤鱼，但 Unity 必须读取接口返回，不得写死物品 ID。
5. 每个建筑等级可有多条配方，每条配方支持多项周期投入和多项固定产出。首版固定产出概率为 `10000`，即 100%。
6. 主动切换配方前结算旧配方已经完成的完整周期，旧半周期清除；暂存区中的旧产物继续保留并可以收取。
7. 运营发布新规则不会追溯覆盖现有建筑或进行中的建造/升级任务；玩家主动切换时才取得目标配方的最新正式版本。

### 47.2 新增接口总览

| 方法 | 路径 | 用途 | 幂等键 |
| --- | --- | --- | --- |
| `GET` | `/api/production/work-state` | 查询全局工作时间、每日额度和可消耗物品 | 不需要 |
| `POST` | `/api/production/work-time/preview` | 不扣物品地预览本次补充 | 不需要 |
| `POST` | `/api/production/work-time/consume` | 扣物品并增加全局工作时间 | 必须为标准 UUID |
| `GET` | `/api/production/recipes/{playerBuildingId}` | 查询建筑当前配方和可选配方 | 不需要 |
| `POST` | `/api/production/recipes/switch` | 结算旧配方并切换目标配方 | 必须为标准 UUID |

原有生产查询与收取接口保持路径兼容：

```http
GET  /api/production/list/{playerId}?sceneId={sceneId}
GET  /api/production/detail/{playerId}/{playerBuildingId}
POST /api/production/collect
POST /api/production/collect-all
```

### 47.3 查询工作状态

```http
GET /api/production/work-state
Authorization: Bearer <player-token>
```

响应核心结构：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "currentWorkClockSeconds": 839203245,
    "remainingWorkSeconds": 21600,
    "maxWorkSeconds": 86400,
    "workUntil": "2026-08-07T23:30:00",
    "anchorTime": "2026-08-07T17:30:00",
    "serverTime": "2026-08-07T17:30:00",
    "usageDate": "2026-08-07",
    "dailyGainedWorkSeconds": 7200,
    "dailyRemainingGainSeconds": 79200,
    "activeBuildingCount": 6,
    "productionRunning": true,
    "configVersion": 3,
    "fuels": [
      {
        "itemId": 200003,
        "itemKey": "bread",
        "itemName": "面包",
        "itemType": "PRODUCT",
        "icon": "bread",
        "workSecondsPerItem": 3600,
        "ownedQuantity": 12,
        "maxConsumableQuantity": 12,
        "configVersion": 3
      }
    ]
  }
}
```

| 字段 | Unity 处理 |
| --- | --- |
| `currentWorkClockSeconds` | 服务端内部单调逻辑时钟，不是 Unix 时间戳 |
| `remainingWorkSeconds` | 当前剩余工作秒数，可按 `serverTime` 在本地每秒递减展示 |
| `workUntil` | 按当前供给预计耗尽的北京时间 |
| `usageDate` | 北京时间自然日，统一在 UTC+8 的 00:00 重置 |
| `activeBuildingCount` | 已完成且至少有一条生效生产规则的建筑数 |
| `productionRunning` | 只表示有剩余工作时间，不保证每栋建筑都有材料或容量 |
| `configVersion` | 预览和确认补充必须回传的正式聚合版本 |
| `maxConsumableQuantity` | 综合库存、单次限制、容量和当日额度后的当前最大可消耗数量 |

Unity 切回前台、跨日、收到资源变化或请求失败后应重新查询。不要根据设备本地日期自行重置额度。示例中的所有 `itemId`、`recipeId` 都不是固定常量，必须使用接口实际返回值。

### 47.4 预览与确认补充

两个接口使用相同请求体：

```json
{
  "itemId": 200003,
  "quantity": 2,
  "expectedConfigVersion": 3
}
```

预览：

```http
POST /api/production/work-time/preview
```

响应包含 `requestedWorkSeconds`、`currentRemainingWorkSeconds`、`afterRemainingWorkSeconds`、`dailyRemainingGainSeconds`、`maxConsumableQuantity`、`canConsume` 和 `unavailableReason`。预览不扣物品，也不预留库存或额度。

确认：

```http
POST /api/production/work-time/consume
Idempotency-Key: 8b5eaaac-5db6-49df-8f60-31c7383ef805
```

成功响应：

```json
{
  "code": 200,
  "message": "建筑工作时间增加成功",
  "data": {
    "idempotencyKey": "8b5eaaac-5db6-49df-8f60-31c7383ef805",
    "itemId": 200003,
    "itemKey": "bread",
    "itemName": "面包",
    "consumedQuantity": 2,
    "grantedWorkSeconds": 7200,
    "itemRemainingQuantity": 10,
    "workState": {}
  }
}
```

`workState` 是 47.3 的完整状态。扣物品、增加时间、增加北京时间当日用量和保存幂等响应在同一事务中提交。Unity 在玩家点击确认时生成一次标准 UUID；网络超时、断线或 5xx 后必须复用原键和完全相同的请求体。相同键更换物品、数量或配置版本会返回 `7808`。

### 47.5 查询和切换配方

查询：

```http
GET /api/production/recipes/{playerBuildingId}
```

响应核心结构：

```json
{
  "playerBuildingId": 801,
  "activeRecipeId": 3001,
  "activeRecipeVersion": 2,
  "recipes": [
    {
      "recipeId": 3001,
      "recipeCode": "ROAST_MEAT",
      "displayName": "烤肉配方",
      "cycleSeconds": 60,
      "storageCapacity": 500,
      "defaultRecipe": true,
      "active": true,
      "configVersion": 2,
      "inputs": [],
      "outputs": []
    }
  ]
}
```

`activeRecipeVersion` 是玩家建筑当前快照版本，配方的 `configVersion` 是最新正式版本；不同表示运营已发布新版，但当前建筑仍按旧快照生产。

切换：

```http
POST /api/production/recipes/switch
Idempotency-Key: 033d73c7-f145-4aa9-9cd2-53c44f27f70f

{
  "playerBuildingId": 801,
  "recipeId": 3002,
  "expectedRecipeVersion": 1
}
```

成功返回 `idempotencyKey`、`playerBuildingId`、`previousRecipeId`、完整 `activeRecipe` 和该建筑的 `productions`。建造或升级中的建筑不能切换。返回 `7811` 时重新读取配方列表并让玩家再次确认；超时重试必须复用原 UUID。

### 47.6 生产状态展示

`ProductionResponse` 中的关键字段：

| 字段 | 语义 |
| --- | --- |
| `storedAmount` | 已完成并保存在建筑暂存区的数量 |
| `canCollect` | `storedAmount > 0` |
| `remainingSeconds` | 当前周期还需的有效工作秒；满仓时为 `0`，非生效历史产物可能为 `null` |
| `nextProduceTime` | 当前工作时间足以完成该周期时的预计北京时间 |

`nextProduceTime=null` 可能表示工作时间耗尽、剩余时间不足一个周期、建筑满仓、规则停用或建筑当前不能生产。Unity 不得在该状态下继续本地累计产物。恢复工作时间后重新查询，服务端会从保存的逻辑工作秒继续计算。

### 47.7 错误码

| code | HTTP | 说明 | Unity 处理 |
| ---: | ---: | --- | --- |
| `7801` | 403 | 建筑工作供给未开放 | 隐藏或禁用补充入口 |
| `7802` | 404 | 供给物品不存在或已停用 | 刷新工作状态 |
| `7803` | 409 | 全局配置版本变化 | 刷新并重新确认 |
| `7804` | 400 | 消耗数量不符合规则 | 修正数量，不原样重试 |
| `7805` | 409 | 供给物品不足 | 刷新背包和状态 |
| `7806` | 409 | 工作时间容量不足 | 使用 `maxConsumableQuantity` |
| `7807` | 429 | 北京时间当日额度不足 | 提示次日重置 |
| `7808` | 409 | 同一幂等键用于不同请求 | 停止重试，为新操作生成新键 |
| `7809` | 409 | 玩家生产状态并发变化 | 刷新后重试 |
| `7810` | 404 | 配方不存在或停用 | 刷新配方列表 |
| `7811` | 409 | 配方版本变化 | 刷新并重新确认 |
| `7812` | 409 | 配方切换状态冲突 | 刷新建筑和配方 |
| `7813` | 503 | 正式配置或历史快照异常 | 停止自动重试并提示稍后再试 |

Unity 必须同时判断 HTTP 状态和统一响应体 `code`。GET 可以短间隔重试；扣资源和切换只能复用原幂等键重试，不能因为结果未知而生成新键。

### 47.8 管理端和 V71

管理入口：

```text
配置中心 -> 玩法与成长 -> 建筑生产 -> 全局建筑生产规则
moduleCode = BUILDING_PRODUCTION
configKey  = GLOBAL
```

管理端可配置工作时间策略、补充物品和每个建筑等级的多条配方。每个有启用配方的等级必须且只能有一个启用默认配方。`LAND_BUILDING / WORLD` 仍可编辑默认生产规则，发布时与规范化默认配方双向同步；非默认配方只在建筑生产页面管理。草稿不影响玩家，只有校验并发布成功才生效。

### 47.9 Unity 动态配置同步

- 管理端保存草稿不会影响 Unity；只有校验并发布成功后，正式配置才生效。
- Unity 在进入生产页面、重新登录或应用回到前台时重新调用 `GET /api/production/work-state`，并在打开建筑配方面板时调用 `GET /api/production/recipes/{playerBuildingId}`，不得写死补充秒数、限额、物品 ID 或配方内容。
- Unity 保存响应中的全局 `configVersion` 和配方 `configVersion`。消耗返回 `7803`、切换返回 `7811` 时，必须重新查询并让玩家再次确认，不能静默按新配置重试。
- 当前后端没有向 Unity 主动推送 `BUILDING_PRODUCTION_CONFIG_UPDATED` WebSocket 事件。页面长时间保持打开时可每 30 至 60 秒刷新一次；未来增加推送时也只发送版本失效通知，完整配置仍通过上述 GET 接口获取。
- 工作策略和补充物品发布后影响后续查询与消耗，但不会截断玩家已有剩余工作时间；默认配方发布后不追溯覆盖已有建筑快照。

V71 新增 9 张表，并为建筑等级、玩家建筑、建造任务和生产行增加配方或逻辑工作时钟快照。迁移文件为 `V71__add_building_fuel_clock_and_recipes.sql`，固定 CRC32 为 `1008907161`。V71 已冻结，禁止修改或使用 Flyway `repair` 掩盖差异；V72-V104 已由后续独立迁移交付，下一次数据库变化必须新建 V105。

### 47.10 最终验收

- 后端测试报告共 847 项，0 failure、0 error；其中 63 项外部环境用例按条件跳过。
- V71 固定 CRC32 测试通过；MySQL 8 临时空库 V1 至 V73 顺序迁移和第二次零变更验证通过。
- 管理端 TypeScript 检查与 Vue 生产构建通过，共转换 3562 个模块。

## 48. Nginx 多实例负载均衡与扩缩容

### 48.1 当前部署状态

当前 Windows Nginx 已改为统一上游组：

```nginx
upstream weijun_backend {
    least_conn;
    keepalive 32;

    server 127.0.0.1:8080 max_fails=2 fail_timeout=10s;
    server 127.0.0.1:8081 max_fails=2 fail_timeout=10s down;
}
```

- `8080` 是当前活动 Java 节点。
- `8081` 已预登记但保持 `down`，未启动时不会收到请求，也不会增加接口延迟。
- 普通 HTTP API、登录、支付回调、运营接口和 WebSocket 都统一使用 `weijun_backend`，新增节点不需要逐个修改代理入口。
- Nginx access log 增加 `upstream`、上游状态和耗时，便于确认请求实际进入哪个节点。

### 48.2 Unity 和管理端影响

Unity、微信小游戏和管理端继续使用原 HTTPS 地址：

```text
https://服务端域名/api/...
wss://服务端域名/api/ws/...
```

本次没有新增、删除或修改任何业务接口、请求字段、响应字段或错误码。客户端不得直接保存 `8080/8081`，也不需要感知请求实际落到哪个 Java 实例。

### 48.3 启用新节点

新节点至少需要独立配置：

```text
SERVER_PORT=8081
REALTIME_PUSH_MODE=redis_pubsub
REALTIME_INSTANCE_ID=game-node-02
PAYMENT_STREAM_CONSUMER_NAME=payment-node-02
```

原节点也必须切换为 `REALTIME_PUSH_MODE=redis_pubsub`，并使用不同的实例 ID 和支付消费者名称。所有节点共享 MySQL、Redis、对象存储和相同业务密钥。

严格启用顺序：

1. 启动新 Java 节点。
2. 直接请求 `http://节点地址:端口/api/health/ping`，确认返回 `200/pong`。
3. 删除对应 Nginx `server` 行末尾的 `down`。
4. 执行 `nginx -t`。
5. 执行 `nginx -s reload`。
6. 通过 access log 的 `upstream=` 确认新节点开始接流量。

停用时先增加 `down` 并重载 Nginx，再停止 Java；节点停止导致的现有 WebSocket 断线由客户端按退避策略重连。

### 48.4 Flyway 与连接池

- 多实例发布时只允许一个迁移节点使用 `FLYWAY_ENABLED=true`；迁移成功后再启动 `FLYWAY_ENABLED=false` 的业务节点。
- 已发布迁移禁止修改，后续变化继续新建更高版本迁移。
- 数据库连接预算必须满足：`节点数 * DB_POOL_MAX_SIZE + 管理/迁移预留 < MySQL max_connections`。
- 一台服务器上的两个进程只能防进程故障；正式高可用应把两个 Java 节点部署在不同服务器，并使用云负载均衡或双 Nginx。

### 48.5 当前验收结果

- 仓库本地配置和 Linux 生产模板均通过 Nginx 1.30.4 语法检查。
- 当前实际 Nginx 配置与仓库本地配置 SHA-256 一致，旧配置已备份。
- `8081` 当前未监听且保持 `down`；通过 HTTPS 请求 `/api/health/ping` 返回 `200/pong`。
- 新格式 access log 显示本次请求由 `127.0.0.1:8080` 成功处理。

完整运维说明见仓库 `docs/nginx-load-balancing.md`。

## 49. 实名认证与防沉迷（V72-V73）及上线手册

本文对应 Flyway V72-V73、`modules/antiaddiction`、登录令牌范围、未成年人支付额度和管理端防沉迷日历。

基础地址：`http://localhost:8080/api`

业务时区：`Asia/Shanghai`。接口时间统一使用服务端北京时间，Unity 不得用设备时间自行决定准入。

## 一、完成范围与上线边界

### 1.1 Java 后端已经实现

- 登录、HTTP 游戏接口和 WebSocket 共用同一套实名及防沉迷准入判断。
- 未实名、实名拒绝、实名快照过期、非允许日期、非 20:00 至 21:00、当日 1 小时耗尽均由后端阻断。
- 未通过准入时签发 `COMPLIANCE_ONLY` 受限令牌，只允许实名、隐私、注销和退出登录相关接口。
- Redis Lua 使用 Redis `TIME` 原子累计玩家自然在线时间；同一玩家多设备、多节点同时在线不会重复计时。
- Redis 不可用时成年人仍按有效实名快照准入，未成年人一律拒绝进入或继续游戏。
- 每 30 秒左右把时长和会话快照写回 MySQL；服务重启时采用保守恢复，避免少计时。
- WebSocket 定期续租；到期会话分批标记 `EXPIRED`，开始、结束和强制下线事件通过数据库 Outbox 可靠上报。
- 20:55 和 20:59 分别推送 5 分钟、1 分钟提醒，达到限制后撤销令牌并跨节点关闭连接。
- HTTP/WebSocket 触碰被准入拒绝时不会新建或保留 `ACTIVE` 会话，已有同键审计会话会标记为强制下线。
- 玩家主动退出会先撤销令牌，再立即结算并关闭该玩家全部防沉迷会话，最后跨节点关闭实时连接；会话清理异常不会恢复已撤销令牌。
- 未成年人支付按单次和北京时间自然月执行额度预占；支付成功后转实付，超时释放，晚到支付进入人工复核而不直接发货。
- 管理端使用独立 `COMPLIANCE_ADMIN` 角色导入、预览、发布和查看官方节假日日历历史。
- 数据库不保存姓名和证件号码明文，只保存 HMAC-SHA256 指纹、平台流水及最小化认证快照。

### 1.2 正式上线前仍需外部完成

仓库中的 `UnconfiguredOfficialAntiAddictionProvider` 是安全占位实现，不伪造官方协议。取得以下资料后，必须按实际技术手册实现并联调官方 provider；在此之前，生产环境开启正式阻断会拒绝启动：

1. 游戏对应的实名认证/防沉迷官方接入资格及最新技术手册。
2. ISBN 版号、`bizId` 或平台实际要求的游戏标识。
3. 接口密钥、签名证书、回调证书和证书轮换规则。
4. 生产与测试地址、IP 白名单、回调白名单和官方测试账号。
5. 实名认证、会话上报、查询/补报、错误码和限流规范。
6. Unity/微信小游戏项目，用于完成实名页、倒计时、强制下线和支付限制交互。

`MOCK` 只能用于本地联调。生产 profile 即使尚未开启阻断，也会拒绝使用 `MOCK`。

当前尚未取得中宣部正式接入资料，因此正式 provider 保持未配置状态，`ANTI_ADDICTION_ENABLED` 和 `ANTI_ADDICTION_ENFORCEMENT_ENABLED` 在生产环境都必须保持 `false`。其余本地规则、接口、支付额度、会话计时和管理端日历可以先完成开发验收，但不能据此宣称已经通过监管平台联调。

## 二、准入模型

### 2.1 实名状态

| 枚举 | 含义 |
| --- | --- |
| `UNVERIFIED` | 尚未完成实名 |
| `VERIFIED` | 平台确认通过且快照仍有效 |
| `REJECTED` | 平台拒绝 |
| `MANUAL_REVIEW` | 需要人工处理 |

年龄档位：`UNKNOWN / UNDER_8 / AGE_8_TO_15 / AGE_16_TO_17 / ADULT`。

### 2.2 游戏准入

- 成年玩家：实名快照有效即可进入，不依赖 Redis 在线时钟。
- 未成年玩家：必须同时满足官方日历、北京时间窗口、每日剩余时长和 Redis 时钟可用。
- 允许日期基线：周五、周六、周日；管理端发布的 `HOLIDAY` 可增加法定节假日。
- `WORKDAY` 用于保存官方调休信息和审计，不会扩大准入范围。
- 配置只允许收紧监管边界，不能把每日时长调到 3600 秒以上、把窗口扩到 20:00 至 21:00 之外，或提高支付限额；违规配置会导致启动失败。

### 2.3 受限令牌

登录响应中的 `accountManagementOnly=true` 或 `compliance.restrictedToken=true` 表示令牌范围为 `COMPLIANCE_ONLY`。

受限令牌可以访问：

- `GET /api/anti-addiction/status`
- `POST /api/anti-addiction/real-name`
- `/api/user/privacy/**`
- `POST /api/user/logout`

背包、建筑、生产、商城、邮件、好友、签到、任务、挂机和奖励领取等游戏接口默认拒绝。实名成功后必须重新登录取得 `FULL` 令牌。

## 三、登录响应改动

账号密码登录仅供开发测试；微信登录和账号密码登录都返回相同的 `compliance` 结构。

```json
{
  "code": 200,
  "message": "微信登录成功",
  "data": {
    "token": "player-or-restricted-token",
    "expiresIn": 604800,
    "user": null,
    "accountStatus": "ACTIVE",
    "accountManagementOnly": true,
    "privacyConsentRequired": false,
    "privacyPolicyVersion": "2026.08.05",
    "compliance": {
      "enforcementEnabled": true,
      "verificationStatus": "UNVERIFIED",
      "ageGroup": "UNKNOWN",
      "gameAccessAllowed": false,
      "restrictedToken": true,
      "blockReason": "REAL_NAME_REQUIRED",
      "message": "请先完成实名认证后进入游戏",
      "usedSeconds": 0,
      "remainingSeconds": 0,
      "nextAllowedTime": null,
      "profileValidUntil": null,
      "serverTime": "2026-08-10 20:00:00"
    }
  }
}
```

Unity 判断顺序：

1. 先处理客户端版本、维护和隐私政策。
2. 再判断 `compliance`；字段为可选以兼容旧客户端，但启用正式阻断前必须强制升级到支持该字段的版本。
3. `gameAccessAllowed=false` 时不能加载主场景。
4. `REAL_NAME_REQUIRED / REAL_NAME_REJECTED / PROFILE_EXPIRED` 进入实名页。
5. 时间或时长限制进入防沉迷提示页，展示 `message` 和 `nextAllowedTime`。
6. `SERVICE_UNAVAILABLE / CALENDAR_UNAVAILABLE` 展示服务不可用，不允许未成年人离线放行。

## 四、玩家接口

请求头：

```http
Authorization: Bearer <FULL或COMPLIANCE_ONLY令牌>
```

### 4.1 查询实名及准入状态

```http
GET /api/anti-addiction/status
```

响应 `data` 与登录响应中的 `compliance` 完全相同。客户端回到前台、实名页刷新或收到网络恢复事件时可以调用，但不应高频轮询。

`blockReason`：

| 值 | Unity 行为 |
| --- | --- |
| `NONE` | 允许进入 |
| `REAL_NAME_REQUIRED` | 打开实名页 |
| `REAL_NAME_REJECTED` | 展示认证失败并允许重新提交 |
| `PROFILE_EXPIRED` | 要求重新认证 |
| `CALENDAR_UNAVAILABLE` | 展示服务维护提示 |
| `OUTSIDE_ALLOWED_WINDOW` | 展示下次可进入时间 |
| `DAILY_TIME_EXHAUSTED` | 展示今日时长已用完 |
| `SERVICE_UNAVAILABLE` | 未成年人禁止进入，稍后重试 |

### 4.2 提交实名认证

```http
POST /api/anti-addiction/real-name
Content-Type: application/json
Authorization: Bearer <COMPLIANCE_ONLY令牌>
```

```json
{
  "requestId": "8d33b6d4-a51e-4f3e-b5da-04db00e105cb",
  "realName": "用户实际姓名",
  "identityNumber": "18位身份证号码"
}
```

规则：

- `requestId` 接受符合 RFC 4122 变体的标准 UUID v1-v5；客户端推荐生成随机 UUID v4。同一次玩家动作和网络重试必须复用同一个值。
- 姓名最多 64 字符，证件号码最多 32 字符。
- 客户端与服务端日志、埋点、崩溃报告、代理访问日志不得记录请求正文。
- 后端只在本次调用内短暂使用原文，数据库和缓存不保存原文。

成功响应：

```json
{
  "code": 200,
  "message": "实名认证已通过，请重新登录",
  "data": {
    "verified": true,
    "reloginRequired": true,
    "compliance": {
      "verificationStatus": "VERIFIED",
      "gameAccessAllowed": true
    }
  }
}
```

响应中的 `compliance` 还包含第三节完整字段。Unity 收到 `reloginRequired=true` 后清除旧令牌并重新执行登录，不能继续使用受限令牌。

## 五、WebSocket 提醒与强制下线

连接方式仍为 `/api/ws/realtime`，握手携带 `Authorization: Bearer <FULL令牌>`。

### 5.1 倒计时提醒

```json
{
  "type": "ANTI_ADDICTION_WARNING",
  "eventId": "uuid",
  "playerId": 10001,
  "data": {
    "warningMinutes": 5,
    "remainingSeconds": 300,
    "serverTime": "2026-08-14 20:55:00"
  },
  "serverTime": "2026-08-14 20:55:00"
}
```

`warningMinutes` 为 `5` 或 `1`。客户端倒计时只用于展示，最终准入以服务端为准。

### 5.2 强制下线

```json
{
  "type": "ANTI_ADDICTION_FORCED_OFFLINE",
  "eventId": "uuid",
  "playerId": 10001,
  "data": {
    "reason": "DAILY_TIME_EXHAUSTED",
    "message": "今日游戏时长已用完",
    "serverTime": "2026-08-14 21:00:00"
  }
}
```

收到后 Unity 必须立即：停止操作、关闭战斗/经营交互、清除令牌、断开 WebSocket、展示原因并返回登录页。即使消息丢失，后续 HTTP 请求也会被后端拒绝。

### 5.3 主动退出

```http
POST /api/user/logout
Authorization: Bearer <FULL或COMPLIANCE_ONLY令牌>
```

退出成功后令牌立即失效，服务端同步关闭该玩家全部防沉迷 HTTP/WebSocket 会话并广播连接重新校验。Unity 不需要额外调用“结束计时”接口；即使实时连接已经断开，也应调用退出接口完成令牌和会话闭环。

## 六、未成年人支付

支付商品和创建订单接口路径不变。防沉迷启用后，服务端在原支付事务中增加：

- 未实名或实名过期禁止下单。
- 8 周岁以下禁止支付。
- 8 至 15 周岁：单次最多 5000 分、每月最多 20000 分。
- 16 至 17 周岁：单次最多 10000 分、每月最多 40000 分。
- 月额度判断为 `paid_fen + reserved_fen + 本次金额`。
- 订单创建时预占，未支付关闭或超时后释放，成功回调按微信实际支付时间转实付。
- 已释放预占后才到达的成功回调进入 `MANUAL_REVIEW`，不会自动发货；后续按支付退款流程处理。

所有额度使用北京时间月份。Unity 只展示后端错误，不得在本地自行放宽。

## 七、管理端官方日历

访问地址：`/compliance-calendar`。账号角色必须是独立的 `COMPLIANCE_ADMIN`。

普通 `CONFIG_ADMIN` 无权发布防沉迷日历；`COMPLIANCE_ADMIN` 登录后只展示合规入口。

### 7.1 CSV 格式

```csv
date,dayType,name
2027-01-01,HOLIDAY,元旦
2027-02-06,WORKDAY,春节调休工作日
2027-02-07,HOLIDAY,春节
```

### 7.2 JSON 格式

```json
{
  "entries": [
    {"date": "2027-01-01", "dayType": "HOLIDAY", "name": "元旦"},
    {"date": "2027-02-06", "dayType": "WORKDAY", "name": "春节调休工作日"}
  ]
}
```

也支持直接传数组。日期必须属于请求中的 `calendarYear`，重复日期、无效日期或无效 `dayType` 会阻断发布。单次内容最大 1 MB。

### 7.3 差异预览

```http
POST /api/ops/compliance/calendar/preview
Authorization: Bearer <operator-token>
Content-Type: application/json
```

```json
{
  "calendarYear": 2027,
  "sourceName": "国务院办公厅2027年节假日安排",
  "format": "CSV",
  "content": "date,dayType,name\n2027-01-01,HOLIDAY,元旦"
}
```

预览响应字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `calendarYear` | integer | 本次预览年份 |
| `currentVersion` / `nextVersion` | long | 当前已发布版本与本次候选版本 |
| `sourceSha256` | string | 规范化导入内容的 SHA-256 |
| `holidayCount` / `workdayCount` | integer | 候选内容中的节假日与调休工作日数量 |
| `addedCount` / `changedCount` / `removedCount` | integer | 相对当前发布版本的新增、变更和移除数量 |
| `blockingErrors` / `warnings` | string[] | 阻断错误与非阻断警告；`blockingErrors` 非空时禁止发布 |
| `entries` | object[] | 日期级差异明细 |
| `valid` | boolean | 由服务端根据 `blockingErrors` 计算 |

`entries[]` 返回 `date`、`dayType`、`displayName`、`changeType`；`changeType` 用于区分新增、变更、移除或未变化日期。

### 7.4 发布

```http
POST /api/ops/compliance/calendar/publish
```

请求在预览内容基础上增加：

```json
{
  "expectedCurrentVersion": 0,
  "confirmed": true
}
```

`expectedCurrentVersion` 必须等于预览返回值。并发管理员已经发布新版本时返回 `409`，必须重新预览。发布使用数据库全局锁；历史版本只归档，不删除。

发布成功返回版本摘要：`id`、`calendarYear`、`calendarVersion`、`sourceName`、`sourceSha256`、`status`、`operatorId`、`publishedTime`、`retiredTime`。

### 7.5 历史

```http
GET /api/ops/compliance/calendar/history?calendarYear=2027
Authorization: Bearer <operator-token>
```

历史接口按版本返回与发布接口相同的版本摘要字段；已被新版本替换的记录通过 `status` 和 `retiredTime` 表示，不会物理删除。

## 八、错误码

| code | HTTP | 含义 |
| --- | --- | --- |
| `7901` | 403 | 需要实名或当前受限令牌需要重新登录 |
| `7902` | 400 | 实名认证未通过 |
| `7903` | 403 | 实名快照或年龄档位已过期 |
| `7904` | 403 | 不在允许日期/时段 |
| `7905` | 403 | 今日游戏时长耗尽 |
| `7906` | 503 | Redis 防沉迷时钟不可用 |
| `7907` | 503 | 官方实名平台不可用或尚未接入 |
| `7908` | 403 | 未成年人单笔支付超限 |
| `7909` | 403 | 未成年人月额度不足 |
| `7910` | 403 | 当前年龄档位禁止支付 |
| `7911` | 409 | 支付预占已失效，需要人工处理 |
| `7912` | 503 | 当前年份官方日历不可用 |
| `7913` | 503 | 防沉迷运行配置无效 |

## 九、Redis 与数据库

### 9.1 Redis 键

- `anti-addiction:sessions:{playerId}`：ZSet，会话成员及租约截止时间。
- `anti-addiction:clock:{playerId}`：自然时间结算锚点。
- `anti-addiction:usage:{playerId}:yyyy-MM-dd`：当日毫秒用量。
- `anti-addiction:profile:{playerId}`：不含身份原文的实名快照缓存。
- `anti-addiction:warning:{playerId}:date:minutes`：跨节点提醒去重。
- `anti-addiction:enforcement:scan-lock`：强制下线扫描所有者锁；每批续租，完成后比较令牌释放。

同一玩家的计时键使用相同 Redis Cluster hash tag，Lua 可以单槽原子执行。
强制下线任务按 `player_id` 游标分批扫完所有活跃未成年人，不会只处理最新一批。多节点正常情况下只有锁持有者执行；锁服务异常时各节点按故障安全策略继续扫描并拒绝未成年人，而不是跳过保护。

### 9.2 V72-V73 表与加固字段

- `player_compliance_profile`
- `anti_addiction_daily_usage`
- `anti_addiction_game_session`
- `anti_addiction_calendar_release`
- `anti_addiction_calendar_date`
- `anti_addiction_calendar_publish_lock`
- `minor_payment_monthly_usage`
- `minor_payment_reservation`
- `anti_addiction_provider_outbox`
- `anti_addiction_audit_log`

V72 还为 `auth_token` 增加 `access_scope`，并为 `operator_account.role` 增加 `COMPLIANCE_ADMIN`。V73 为 `anti_addiction_game_session` 增加 `session_generation`，并增加日历发布事务锁；当前 V1 至 V104 已发布迁移全部冻结。已有数据库按第 65.7 节完成 V102 前置空表检查后，启动当前应用时由 Flyway 迁移至仓库当前最高版本 V104，下一次数据库变化从 V105 新建迁移；禁止用 `repair` 掩盖已执行脚本漂移。

## 十、生产配置与启用顺序

生产环境变量模板见 `deploy/systemd/weijun-game-project.env.template`。最少需要：

```text
ANTI_ADDICTION_ENABLED=true
ANTI_ADDICTION_ENFORCEMENT_ENABLED=false
ANTI_ADDICTION_PROVIDER_MODE=official
ANTI_ADDICTION_IDENTITY_FINGERPRINT_SECRET=<独立的至少32字符随机密钥>
ANTI_ADDICTION_REDIS_CACHE_ENABLED=true
ANTI_ADDICTION_ENFORCEMENT_LOCK_KEY=anti-addiction:enforcement:scan-lock
ANTI_ADDICTION_ENFORCEMENT_LOCK_TTL_MS=60000
```

推荐顺序：

1. 取得官方资料，实现并通过 provider 沙箱测试。
2. 部署 V72-V73，保持两个开关为 `false`。
3. Unity 发布支持 `compliance`、实名页、WebSocket 提醒和强制下线的版本。
4. 后端设置 `ANTI_ADDICTION_ENABLED=true`、正式阻断仍为 `false`，完成实名接口灰度。
5. 创建 `COMPLIANCE_ADMIN`，导入并发布当前年份和下一年份官方日历。
6. 用成年、各未成年档位、双设备、Redis 故障和支付回调账号完成测试报告。
7. 通过客户端版本策略提高最低版本，阻断旧客户端。
8. 最后设置 `ANTI_ADDICTION_ENFORCEMENT_ENABLED=true` 并滚动发布。

禁止把身份指纹密钥写入管理端、Git、Unity、日志或普通配置草稿。多节点必须使用同一密钥、MySQL、Redis 和北京时间。

## 十一、Unity 验收清单

- 登录 DTO 支持可选 `compliance` 对象和 `user=null`。
- 支持所有实名状态、年龄档位和阻断原因枚举的未知值降级显示。
- 实名请求正文不进入日志或埋点，提交后立即清空输入框内存引用。
- 实名成功后重新登录，不复用受限令牌。
- 进入主场景前同时检查隐私、账号状态和防沉迷准入。
- 处理 5 分钟、1 分钟提醒和强制下线消息。
- 每次受保护接口收到 7901 至 7906/7912 时统一退出游戏流程，不只弹 Toast。
- 支付按钮以后端下单结果为准，不缓存月额度自行判断。
- 客户端倒计时使用 `serverTime + remainingSeconds` 校准，回前台后重新查询状态。

## 十二、关键测试场景

当前自动化执行结果和仍需官方/Unity 验收的边界见 [`anti-addiction-test-report.md`](anti-addiction-test-report.md)。

- 周五 19:59:59 禁止，20:00:00 允许，20:59:00 提醒，21:00:00 强制下线。
- 同一未成年人两台设备在线只累计一份自然时间；新登录令牌使旧连接失效。
- Redis 故障时未成年人拒绝，成年有效快照仍可进入。
- 服务重启后从数据库快照保守恢复，不少计时。
- 官方日历重复日期、格式错误、版本冲突均阻断发布。
- 并发订单接近月额度时只有额度内订单成功预占。
- 预占释放与支付回调并发时不直接发货，晚到成功进入人工复核。
- Outbox 消费节点崩溃后，过期处理租约可由其他节点重新领取。

正式验收还必须使用官方平台提供的测试账号和测试环境，仓库内 Mock 测试不能替代监管平台联调报告。

## 50. 实名认证与防沉迷测试报告

报告日期：2026-08-10  
代码基线：Flyway V1-V73，Java 21，Spring Boot 3.x  
测试时区：`Asia/Shanghai`

## 一、结论

除中宣部正式 provider 及 Unity/微信小游戏端到端联调外，后端准入、自然时间计时、支付额度、提醒、强制下线、退出结算、日历发布和安全降级已经通过自动化验证。

当前不能开启生产正式阻断，也不能把本报告作为监管平台验收报告。生产环境必须保持：

```text
ANTI_ADDICTION_ENABLED=false
ANTI_ADDICTION_ENFORCEMENT_ENABLED=false
ANTI_ADDICTION_PROVIDER_MODE=official
```

## 二、执行结果

| 验证项 | 命令 | 结果 |
| --- | --- | --- |
| 全部 Maven 测试 | `mvn -q test` | 通过 |
| 防沉迷专项测试 | `mvn -q "-Dtest=ComplianceInterceptorTest,AntiAddictionSessionServiceTest,AntiAddictionAccessServiceTest,AntiAddictionEnforcementSchedulerTest,AntiAddictionStartupValidatorTest,SensitiveVerificationModelTest,AuthServiceImplTest" test` | 通过 |
| Redis 自然时间集成测试 | 设置 `RUN_REDIS_INTEGRATION_TESTS=true` 后运行 `AntiAddictionRedisClockIntegrationTest` | 2 项通过 |
| MySQL 迁移集成测试 | 设置 `RUN_MYSQL_INTEGRATION_TESTS=true` 后运行 `FlywayMigrationIntegrationTest` | V1-V73 首次迁移和二次零变更通过 |
| 最终 Surefire 汇总 | 全部报告聚合 | 847 项，0 failure，0 error，63 skipped |

MySQL 8 运行历史迁移时出现的 `VALUES() is deprecated` 是已知警告，不是迁移失败；禁止修改已发布迁移消除该警告。

## 三、已验证场景

| 场景 | 自动化结果 |
| --- | --- |
| 未实名登录 | 返回受限准入结果，不进入主游戏 |
| 成年有效快照 | 不依赖 Redis 游戏时钟即可准入 |
| 未成年人 Redis 不可用 | 故障关闭，拒绝准入 |
| 周五 19:59:59 | 拒绝 |
| 周五 20:00:00 | 允许 |
| 周五 21:00:00 | 拒绝 |
| 当日自然时间达到 3600 秒 | 拒绝且剩余时间为 0 |
| 当前年度日历未发布的工作日 | 返回 `CALENDAR_UNAVAILABLE` |
| 同一玩家两个在线会话 | 保留两个租约，只累计一份自然时间 |
| 被拒绝的 HTTP 触碰 | 不写入 `ACTIVE`，已有同键会话强制关闭 |
| 5 分钟、1 分钟提醒 | 分别推送正确事件和剩余秒数 |
| 达到限制强制下线 | 推送事件、撤销全部令牌、关闭会话及跨节点连接并记录审计 |
| 玩家主动退出 | 立即关闭全部防沉迷会话和实时连接 |
| 退出时会话清理异常 | 令牌撤销结果不回滚，仍关闭实时连接 |
| 受限令牌访问普通游戏接口 | HTTP 403；显式合规豁免接口仍可访问 |
| 8 周岁以下支付 | 禁止 |
| 单笔/月额度及已支付加预占额度 | 严格校验 |
| 预占有效期内的晚到回调 | 可按实际支付时间转实付 |
| 预占失效后的成功回调 | 转人工处理，不自动发货 |
| 正式 provider 未就绪时误开生产阻断 | 应用拒绝启动 |
| 实名敏感模型字符串输出 | 姓名、证件号和平台主体值不出现在 `toString()` |

## 四、申请后必须补做

1. 按官方最新技术手册实现实名查询、会话上报、查询补报、签名、证书轮换、限流和错误码映射。
2. 使用官方测试环境及成年、各未成年年龄档位测试账号完成认证和行为上报验收。
3. 在 Unity/微信小游戏真机验证受限登录、实名页、5/1 分钟提醒、21:00 强制下线、前后台切换和支付限制。
4. 验证官方平台超时、熔断、证书失效、签名失败、回调重放和 Outbox 重试恢复。
5. 导入并发布当前年度和下一年度官方节假日日历，验证版本、差异预览和到期告警。
6. 将支持 `compliance` 的客户端提升为最低版本，确认旧客户端已阻断后再开启正式 enforcement。

正式验收完成后，应在本文件追加官方平台环境、用例编号、执行人、时间、证据链接和最终审批结论，不覆盖本次后端自动化记录。

## 51. V76 内容安全与不良信息过滤

更新时间：2026-08-11

### 51.1 修改前后

| 项目 | 修改前 | 修改后 |
| --- | --- | --- |
| 玩家昵称 | 只校验格式、唯一性和昵称保留词 | 增加统一本地词库；主动改名和微信首次建议昵称可叠加微信 `msg_sec_check` |
| 微信首次登录 | 建议昵称格式无效或重复时使用临时昵称 | 不合规或默认 fail-closed 审核不可用时也改用临时昵称，登录仍成功 |
| 公告与邮件 | 只校验非空和长度 | 标题、正文、发件人发布前统一检查本地词库 |
| 内容模板 | 只校验结构和长度 | 公告、普通邮件、奖励邮件模板发布前统一检查 |
| 词库维护 | 无统一入口 | 管理端 `CONTENT_SAFETY / WORDS` 表格化新增、分类、启停和版本发布 |
| 审计 | 无内容安全审计 | 记录决策、命中分类、微信结果和 SHA-256，不保存送检原文 |

### 51.2 已接入入口

- `POST /api/user/register`：注册昵称执行本地词库检查。
- `POST /api/user/wx-login`：首次建议昵称执行本地词库，并可使用已换取的 `openid` 调用微信检查。
- `PUT /api/user/me/nickname`：主动改名执行本地词库和可选微信检查；同昵称幂等重试不会重复送检。
- 管理端公告、普通系统邮件、奖励邮件和内容模板：执行本地词库检查。

现有请求与成功响应 DTO 均未增加或删除字段。

### 51.3 管理端配置

```text
moduleCode = CONTENT_SAFETY
configKey  = WORDS
```

只有 `CONFIG_ADMIN` 可以写入。词条匹配会执行 NFKC、英文小写并移除标点和空白；已发布词不能删除或改写，只能停用后新增。V76 默认初始化少量高风险词，运营可在配置中心继续扩充和分类。

### 51.4 新增错误码

| HTTP | 业务码 | message | Unity 建议 |
| ---: | ---: | --- | --- |
| 400 | `2012` | 内容包含不合规信息 | 保留输入并提示玩家更换 |
| 503 | `2013` | 内容安全服务暂不可用，请稍后重试 | 保留输入并允许稍后重试 |

Unity 如果统一显示服务端 `message`，不需要修改。若客户端使用业务码白名单或本地化映射，需要新增 `2012`、`2013`。微信首次登录返回结构不变，临时昵称仍通过 `nicknameChangeRequired=true` 表示。

### 51.5 微信启用条件

生产开启前必须配置真实 `WECHAT_MINI_GAME_APP_ID`、`WECHAT_MINI_GAME_APP_SECRET`，确认微信公众平台当前账号具有 `msg_sec_check` 权限、服务器网络和必要的 IP 白名单可用，并确保 `openid` 属于同一 AppID。AppSecret 只保存在服务端。

```text
CONTENT_SAFETY_ENABLED=true
CONTENT_SAFETY_LOCAL_FILTER_ENABLED=true
CONTENT_SAFETY_WECHAT_CHECK_ENABLED=false
CONTENT_SAFETY_FAIL_OPEN=false
```

微信联调完成前保持微信检查关闭；正式启用时建议继续保持 fail-closed。

### 51.6 验证结果

- `mvn -q test`：通过。
- MySQL 8 `FlywayMigrationIntegrationTest`：V1-V76 顺序迁移、V76 表检查和二次零变更通过。
- `admin-web npm run build`：TypeScript 检查和 Vite 生产构建通过。
- 微信真实网络调用未在自动化测试中执行，正式上线前仍需使用真实小游戏账号联调。

## 52. V77-V78 P0/P1 运营工作台

更新时间：2026-08-11

### 52.1 修改前后

| 项目 | 修改前 | 修改后 |
| --- | --- | --- |
| 管理端 | 以配置发布为主，退款主要使用后端接口 | 增加财务、玩家、治理、分析和运营账号完整工作区 |
| 退款角色路由 | 可能进入配置中心 | `REFUND_REVIEWER`、`REFUND_ADMIN` 登录后固定进入 `/refunds` |
| 玩家处置 | 缺少统一安全入口 | 补偿、踢线、冻结/解冻统一要求原因、二次确认、幂等和审计 |
| 运营监控 | 在线、异常、合规和经济数据分散 | 首页、异常中心、统一审计和运营分析集中展示 |
| 配置发布 | 草稿、发布和回滚 | 增加结构化差异、影响范围、高风险提示和定时发布 |

### 52.2 页面与接口

管理端 Axios 已配置 `/api` 基础路径，页面实际调用 `/api/ops/**`：

| 页面 | 主要接口 |
| --- | --- |
| `/dashboard` | `GET /api/ops/dashboard` |
| `/refunds` | `/api/ops/refunds` 下的列表、详情、批准、拒绝、重试和关闭 |
| `/payments/orders` | `GET /api/ops/payments/orders`、订单详情 |
| `/payments/revenue` | `GET /api/ops/payments/revenue` |
| `/payments/reconciliation` | 微信账单导入、批次、明细、差异和关闭接口 |
| `/players` | 玩家查询、详情、补偿、踢线、冻结/解冻、备注和标签接口 |
| `/audiences` | `/api/ops/player-tags`、`/api/ops/player-segments` |
| `/exceptions` | 异常列表和人工关闭接口 |
| `/operators` | 账号创建、修改、启停、强退、密码/MFA 重置和登录记录 |
| `/unified-audits` | `GET /api/ops/audits/unified` |
| `/analytics` | 内容安全、合规、经济和活动效果接口 |
| `/configs` | 配置预览、定时发布和取消定时接口 |

当前代码包含 25 个管理端 Controller、102 条 `/api/ops/**` 方法路由。下表补齐页面摘要中省略的正式路径；相同路径但 HTTP 方法不同的接口是不同操作：

| 模块 | 方法与路径 |
| --- | --- |
| 运营分析 | `GET /api/ops/analytics/content-safety`；`GET /api/ops/analytics/compliance`；`GET /api/ops/analytics/economy`；`GET /api/ops/analytics/activities` |
| 本人 MFA | `POST /api/ops/auth/mfa/setup`；`POST /api/ops/auth/mfa/confirm`；`POST /api/ops/auth/mfa/disable` |
| 配置增强 | `GET /api/ops/config/{moduleCode}/{configKey}/preview`；`POST /api/ops/config/{moduleCode}/{configKey}/schedule`；`POST /api/ops/config/{moduleCode}/{configKey}/schedule/cancel` |
| 异常中心 | `GET /api/ops/exceptions`；`POST /api/ops/exceptions/{taskType}/{taskKey}/close` |
| 运营账号 | `GET /api/ops/operators`；`POST /api/ops/operators`；`PUT /api/ops/operators/{operatorId}`；`POST /api/ops/operators/{operatorId}/enable`；`POST /api/ops/operators/{operatorId}/disable`；`POST /api/ops/operators/{operatorId}/force-logout`；`POST /api/ops/operators/{operatorId}/reset-password`；`POST /api/ops/operators/{operatorId}/reset-mfa`；`GET /api/ops/operators/{operatorId}/login-records` |
| 支付运维 | `GET /api/ops/payments/orders/{orderNo}`；`GET /api/ops/payments/reconciliation/bills`；`GET /api/ops/payments/reconciliation/bills/{importId}/entries`；`GET /api/ops/payments/reconciliation/bills/{importId}/differences`；`POST /api/ops/payments/reconciliation/bills/differences/{differenceId}/resolve` |
| 标签与分群 | `GET /api/ops/player-tags`；`POST /api/ops/player-tags`；`PUT /api/ops/player-tags/{tagId}`；`GET /api/ops/player-segments`；`POST /api/ops/player-segments`；`GET /api/ops/player-segments/{segmentId}`；`POST /api/ops/player-segments/{segmentId}/archive` |
| 玩家运营 | `GET /api/ops/players`；`GET /api/ops/players/{playerId}`；`POST /api/ops/players/{playerId}/kick-offline`；`POST /api/ops/players/{playerId}/economy/freeze`；`POST /api/ops/players/{playerId}/economy/unfreeze`；`GET /api/ops/players/{playerId}/notes`；`POST /api/ops/players/{playerId}/notes`；`POST /api/ops/players/{playerId}/tags/{tagId}`；`DELETE /api/ops/players/{playerId}/tags/{tagId}` |

#### 玩家中心在线状态

玩家列表和玩家详情响应新增 `onlineStatus`：`ONLINE` 表示存在有效的实时 WebSocket 会话租约，`OFFLINE` 表示已确认没有有效租约，`UNKNOWN` 表示 Presence/Redis 暂时不可用。`online` 仅作为旧管理端兼容布尔值，新管理端优先使用 `onlineStatus`。仅 HTTP 登录或普通请求不会判定为在线，玩家必须建立并保持 `/api/ws/realtime` 连接；多实例部署必须将 `REALTIME_PUSH_MODE` 设置为 `redis_pubsub` 并确保 Redis 可用。

金额单位为分。收入接口的 `paymentRate` 和 `paymentSuccessRate` 已经是百分数，例如 `12.50` 表示 `12.50%`。

### 52.3 玩家安全操作

```http
POST /api/ops/players/{playerId}/compensations
Authorization: Bearer <operator-token>
Content-Type: application/json
```

```json
{
  "idempotencyKey": "compensation:20260811:player-1001:case-88",
  "reason": "客服工单 CASE-88 已核对",
  "confirmed": true,
  "items": [
    { "itemKey": "bread", "quantity": 20 },
    { "itemKey": "gold", "quantity": 1000 }
  ]
}
```

踢线、冻结和解冻分别使用 `/kick-offline`、`/economy/freeze`、`/economy/unfreeze`，请求同样必须携带 `idempotencyKey`、`reason`、`confirmed=true`。同键同内容返回原结果，同键不同内容返回冲突。补偿只接受已启用物品，单次最多 20 种。

### 52.4 微信账单

```http
POST /api/ops/payments/reconciliation/bills?billType=PAYMENT&billDate=2026-08-10
Authorization: Bearer <operator-token>
Content-Type: multipart/form-data
file=<微信CSV账单>
```

`billType` 只允许 `PAYMENT` 或 `REFUND`，文件最大 10 MB、最多 10 万条明细。同一类型、日期和文件 SHA-256 重复导入不会重复写入。系统生成金额、状态、交易标识、手续费和缺失订单差异，人工关闭差异必须提交原因及 `confirmed=true`。

### 52.5 角色与安全

- `REFUND_REVIEWER`：退款查询、批准和拒绝，默认进入 `/refunds`。
- `REFUND_ADMIN`：增加退款重试/关闭、微信对账、客服记录和异常处置。
- `CONFIG_VIEWER`：配置、财务、玩家和非合规分析只读。
- `CONFIG_EDITOR`：配置编辑、预览和直接发布，不获得退款审核或玩家高风险权限。
- `CONFIG_ADMIN`：配置、运营账号、玩家安全操作、微信对账和合规监控，不继承退款审核权限。
- `COMPLIANCE_ADMIN`：防沉迷配置、合规监控和相关玩家支持，不查看财务。

管理员不能停用自己、修改自己的角色、重置自己的密码或从账号页重置自己的 MFA；本人关闭 MFA 必须输入当前密码和动态验证码。系统始终至少保留一个启用的 `CONFIG_ADMIN`。

### 52.6 上线与 Unity

```text
OPERATOR_API_ENABLED=true
OPERATOR_MFA_ISSUER=维钧游戏运营中台
OPERATOR_MFA_ENCRYPTION_KEY_BASE64=32字节随机密钥的Base64文本
OPERATOR_MFA_ALLOWED_CLOCK_SKEW_WINDOWS=1
```

V77-V78 是该工作台首次交付时的历史迁移边界。当前上线应先停止旧版本写入、备份数据库并按第 65.7 节完成 V102 前置空表检查，只启动一个当前版本实例并由 Flyway 迁移至仓库当前最高版本 V104，再启动其余实例；后续数据库变化从 V105 开始。Redis 用于实时在线人数和 DAU；生产必须正确配置 `OPERATOR_TRUSTED_PROXY_ADDRESSES`。MFA 加密密钥上线后不得随意更换。

本节全部是 `/api/ops/**`、后台指标任务和 `admin-web` 管理端能力。Unity 使用的玩家接口路径、请求体和成功响应没有变化，Unity 不需要修改。

### 52.7 验证结果

- `mvn -q test`：通过。
- MyBatis Mapper XML 全量解析：通过。
- MySQL 8 `FlywayMigrationIntegrationTest`：V1-V78 和二次零变更通过。
- `admin-web npm run build`：TypeScript 与 Vite 正式构建通过。

## 53. 服务端并发与在线容量优化

更新时间：2026-08-12

### 53.1 修改前后

| 热路径 | 修改前 | 修改后 |
| --- | --- | --- |
| 普通 HTTP 鉴权 | 分别查询令牌、账号状态和隐私授权 | 单条数据库授权快照；生产 Redis 命中后为零次 MySQL 查询 |
| WebSocket 定期鉴权 | 每条连接分别查询，同玩家多连接重复读取 | 同玩家相同 Token 在一轮巡检或推送中只鉴权一次 |
| 玩家活跃时间 | 鉴权请求可能持续写 MySQL | 本地原子限频，默认每玩家每分钟最多一条活跃 SQL |
| 在线审计快照 | 每次连接续租都可能刷新 MySQL | 实时状态仍以本机会话或 Redis 为准，MySQL 默认每玩家每分钟最多刷新一次 |
| 运营在线索引 | 每条 WebSocket 连接分别续期 | 同一玩家每轮只续期一次 |
| 防沉迷关闭或观察模式 | HTTP/WebSocket 热路径仍可能读取实名和数据库时间 | 直接跳过实名、数据库时间和会话计时热路径 |
| 防沉迷正式执行 | 每次判定查询一次数据库时间 | 每节点默认每秒用数据库校准一次，其间用单调时钟推进 |

### 53.2 授权缓存一致性与故障策略

- Redis 只保存 Token 的 SHA-256 摘要、Token 记录 ID、玩家 ID、到期时间、权限范围、账号状态和隐私授权布尔值，不保存原始 Token、身份证、昵称或其他个人资料。
- 玩家令牌、账号状态或隐私授权变化时使用玩家版本失效；发布新隐私政策时使用全局政策版本失效，不扫描 Redis Key。
- 安全状态写事务开始时建立 pending 占位并递增版本，事务结束后再次递增版本；数据库查询前后还会校验共享变更纪元，阻止并发旧快照回填复活。
- Redis 读取或普通缓存写入失败时，本次请求回源 MySQL 继续服务。
- 登录签发新 Token、退出、强制撤销、隐私同意/撤回、注销申请/恢复或最终注销时，如果 Redis 失效操作失败，服务端会保护性终止并回滚数据库事务，避免已修改安全状态仍被旧缓存接受。
- 生产开启授权缓存后应把 Redis 视为核心依赖，启用健康检查并部署监控、高可用与备份策略。

### 53.3 新增生产配置

```text
AUTH_AUTHORIZATION_CACHE_ENABLED=true
AUTH_AUTHORIZATION_CACHE_KEY_PREFIX=weijun:prod:auth:authorization
AUTH_AUTHORIZATION_CACHE_TTL_SECONDS=300
AUTH_AUTHORIZATION_CACHE_REVISION_TTL_SECONDS=86400
AUTH_AUTHORIZATION_CACHE_PENDING_TTL_SECONDS=900
PRESENCE_SNAPSHOT_REFRESH_SECONDS=60
ANTI_ADDICTION_AUTHORITY_TIME_REFRESH_MILLIS=1000
ANTI_ADDICTION_AUTHORITY_TIME_MAX_STALE_MILLIS=5000
REDIS_HEALTH_ENABLED=true
```

不同环境的 `AUTH_AUTHORIZATION_CACHE_KEY_PREFIX` 必须不同，同一环境全部节点必须一致，配置值中不要使用大括号。开发环境默认关闭授权缓存，避免本地没有 Redis 时影响登录联调；生产环境默认开启。

### 53.4 容量基线

没有目标服务器实测前不能承诺固定在线人数。对常见 `4 核 8 GB` 单 Java 节点，建议首发先按约 `3000 CCU` 配置容量；使用真实 TLS、WebSocket 心跳、登录鉴权、好友推送和典型业务混合流量连续压测至少 60 分钟，CPU、内存、GC、HTTP P95/P99、Hikari、MySQL 与 Redis 指标均满足阈值后，再把单节点目标提高到约 `5000 CCU`。

部署基线同步调整为：Nginx `worker_processes auto`、每 worker `worker_connections 16384`、上游 keepalive `256`，Java systemd `LimitNOFILE=131072`。目标超过单节点实测上限时应横向增加 Java 节点并使用 `REALTIME_PUSH_MODE=redis_pubsub`，不能只放大线程池或数据库连接池。

### 53.5 Unity 结论

本轮只修改后端内部查询、缓存、限频和部署参数：

- HTTP 路径、方法、请求头、请求体和统一返回结构不变。
- 登录 Token 的生成、传递和 `Authorization: Bearer <token>` 用法不变。
- WebSocket 地址、握手 Token、客户端消息和服务端消息类型不变。
- 账号状态、隐私授权、防沉迷和错误码语义不变。

因此已按 V72-V73 完成防沉迷适配的 Unity 不需要修改，也不需要重新生成数据模型。

### 53.6 验证结果

- `mvn -q -DskipTests test-compile`：通过。
- 授权缓存版本、事务占位、旧回填拦截、Redis 降级、防沉迷时间缓存、多会话鉴权和在线限频专项测试：通过。
- `mvn -q test`：全量通过。
- 本轮未新增 Flyway 文件，数据库结构仍为 V78。

## 54. V79 玩家跨场景唯一特殊建筑与净化剂

更新时间：2026-08-13

### 54.1 最终效果

- 新增 `2011 / DIAMOND_MINE / 钻石矿场`，默认山地、玩家 6 级解锁、前置矿场，固定产出 `diamond`。
- 新增 `2012 / PURIFICATION_LAB / 净化剂工坊`，默认草原、玩家 5 级解锁，固定产出 `purification_agent`。
- 每位玩家跨全部经营场景，每种特殊建筑最多拥有 1 座；取消建造或拆除后可重新建造。
- 两种特殊建筑禁止 NPC 入驻和 NPC 增产，禁止投入物品、额外配方、修改固定产物和停用系统默认配方。
- 管理端可修改建筑解锁等级，以及特殊建筑每周期产量、生产周期和最大暂存量。
- 净化首次消耗管理端配置的 `PRODUCT` 物品和数量；默认 `purification_agent × 1`，历史净化保存实际消耗快照。
- 钻石建筑产出收取写入 `player_diamond_production_audit`，并绑定生产收取幂等流水；重放不会重复发钻石或重复审计。

### 54.2 Unity 需要处理的新增响应字段

`GET /api/building/available/{playerId}`、`GET /api/building/check/{playerId}/{buildingId}` 和带地块参数的条件接口新增：

| 字段 | 说明 |
| --- | --- |
| `singletonGroupCode` | 可建造列表中的特殊建筑单例组；普通建筑为 `null` |
| `ownedCount` | 玩家跨全部场景已拥有数量 |
| `maxOwnedCount` | 特殊建筑为 `1`；普通建筑为 `null` |
| `ownershipLimitMet` | 是否仍可继续建造该单例组 |

地图格子响应新增 `purificationItemId`、`purificationItemKey`、`purificationItemAmount`。Unity 应直接展示服务端返回的净化成本；净化成功后刷新该格状态和背包资源。达到特殊建筑上限时，服务端返回业务码 `1009`。

### 54.3 管理端边界

- `LAND_BUILDING / WORLD`：管理员可配置建筑解锁等级和等级生产参数；特殊建筑的类型、单例组、固定产物和系统配方结构只读。
- `BUILDING_PRODUCTION / GLOBAL`：特殊建筑只能编辑周期产量、周期秒数和容量，不能新增/删除配方、修改产物、增加投入或停用配方。
- `LAND_MAP_POLICY / WORLD`：管理员可配置净化物品和数量；物品必须存在、启用且为 `PRODUCT`，数量范围 `1..1000000`。

### 54.4 数据库与上线

V79 文件为 `V79__add_unique_special_buildings_and_purification_agent.sql`，固定 CRC32 为 `1556615827`。迁移增加单例组配置与建造快照、`(player_id, singleton_group_snapshot)` 唯一索引、净化策略与历史消耗快照、`purification_agent` 物品、两座建筑及默认配方、地块准入和钻石产出审计表。

以下是 V79 首次交付时的历史验收边界：确认 `version=79`、`success=1`、`checksum=1556615827`，并核对两座建筑、净化剂、唯一索引、净化策略字段和审计表。当前上线应先停止旧版本写入、备份数据库并按第 65.7 节完成 V102 前置空表检查，只启动一个当前版本节点由 Flyway 迁移至仓库当前最高版本 V104，再启动其余节点；V1-V104 已发布迁移禁止修改，下一次数据库变化从 V105 开始。

### 54.5 验证结果

- 后端专项测试和全量测试通过。
- MySQL 8 临时空库 V1-V79 顺序迁移及第二次零变更通过。
- 管理端 TypeScript 检查和 Vite 生产构建通过。
- `git diff --check` 和 V79 固定校验和检查通过。

## 55. V80 抽卡稀有奖励公屏轮播

更新时间：2026-08-13

### 55.1 最终效果与边界

- 管理端可对当前卡池设置总开关、纯文本模板，并逐个勾选命中后需要通报的奖励。
- 单抽或五连抽命中已勾选奖励时，一次抽奖批次最多生成一条 `CARD_DRAW_RARE_BROADCAST`；多个稀有奖励按首次命中顺序合并。
- 该消息在游戏顶部公屏中一条条轮播，不是运营公告、系统邮件或站内提醒，不产生未读数或红点。
- 只推送给消息有效期内仍在线且令牌仍有效的会话；Redis 尚未接收广播前由 Outbox 在有效期内重试，broker 接收后的节点订阅和会话发送为实时 best-effort。离线或漏收不补发，创建 5 分钟后不再展示。
- 抽奖 HTTP 成功响应与历史流水始终是扣费、中奖和到账的权威结果，公屏消息只能用于氛围展示，不能据此发奖。

### 55.2 管理端配置

配置入口为“配置中心 -> 抽卡奖池”，下拉框仅展示两个真实可选入口：

- 普通卡池（钻石）：配置键 `STANDARD_POOL`，仅对应 `poolType=STANDARD`。
- 限定活动卡池（活动抽奖券）：配置键 `LIMITED_POOL`，仅对应 `poolType=LIMITED_ACTIVITY`，可配置活动名称和起止时间；“暂无配置”时仍可进入空白配置。

历史配置键 `ACTIVE_POOL` 继续兼容普通卡池；配置键不是 Unity 使用的 `poolCode`。卡池类型由入口决定且只读。

普通与限定卡池的奖池编码、限定卡池的活动编码都由后端首次发布时自动生成，无需管理员填写。读取空配置、校验或保存草稿时不生成编码，页面显示“首次发布时由系统自动生成”；发布成功后显示只读、可复制的正式编码。格式为 `POOL_yyyyMMdd_<32位大写UUID>`、`ACT_yyyyMMdd_<32位大写UUID>`，日期使用服务端业务时间，长度不超过 64。

已有编码（含历史手填编码）原样保留。后续修改名称、奖励、时间或保底规则不改变编码，不重置原有保底进度；后端禁止篡改。旧未发布草稿如含手填编码，需丢弃旧草稿后从空配置重新填写业务字段。新建活动应先发布卡池，再在任务等配置中选择已发布活动。

Unity 不自行生成或解析这些编码。普通池继续使用 `/api/card-draw/current`、`/api/card-draw/draw`；限定池继续使用实际 `poolCode` 调用指定卡池接口。若 Unity 已按服务端编码配置接入，无需改协议；新限定池首次发布后需将生成的实际 `poolCode` 接入对应活动入口，不能使用 `LIMITED_POOL` 代替。本次自动编码不新增 Flyway 迁移。

当前已交付：物品目录登记活动抽奖券，限定卡池按 `CARD_DRAW_TICKET` 类型汇总扣券，普通每日任务活动加送和活动签到由玩家手动领取奖励，钻石商城按统一每张券单价出售。旧的独立活动收获任务已由 V125 下线。活动券永久有效，可用于任何当前开放的限时卡池，不受来源活动约束，也不限于下一期；限时的是卡池和获取渠道，而不是券。管理端不再提供到期策略入口或支持 `ACTIVITY_TICKET_POLICY` 写入/发布，历史策略表和出售数据仅保留审计。

普通卡池通用配置键示例（限定池将键替换为 `LIMITED_POOL`）：

```text
moduleCode = CARD_DRAW
configKey  = STANDARD_POOL
```

```http
GET  /api/ops/config/CARD_DRAW/STANDARD_POOL
POST /api/ops/config/CARD_DRAW/STANDARD_POOL/validate
PUT  /api/ops/config/CARD_DRAW/STANDARD_POOL/draft
POST /api/ops/config/CARD_DRAW/STANDARD_POOL/publish
Authorization: Bearer <operator-token>
```

新增配置字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `broadcastEnabled` | boolean | 卡池总开关；历史配置缺省为 `false` |
| `broadcastTemplate` | string/null | 纯文本模板，最多 500 个字符 |
| `rewards[].broadcastOnWin` | boolean | 命中该奖项时是否纳入本批通报；历史配置缺省为 `false` |

模板只允许 `{playerName}`、`{poolName}`、`{rewardSummary}`。发布时校验模板长度、占位符语法、内容安全和至少一个已勾选奖项。新增奖励、切换奖励类型或更换资产时默认关闭该奖励的通报，避免误播；“按品质勾选”只作用于当前已有的装备/NPC 奖项，不形成未来自动继承规则。

### 55.3 WebSocket 消息

连接仍使用统一实时通道：

```text
ws://localhost:8080/api/ws/realtime
wss://你的正式域名/api/ws/realtime
Authorization: Bearer <player-token>
```

消息示例：

```json
{
  "type": "CARD_DRAW_RARE_BROADCAST",
  "requestId": null,
  "eventId": "card-draw-broadcast:550e8400-e29b-41d4-a716-446655440000",
  "playerId": null,
  "data": {
    "playerName": "林间旅人",
    "poolName": "限定伙伴池",
    "content": "恭喜林间旅人在限定伙伴池抽中剑圣×2！",
    "rewards": [
      {
        "rewardName": "剑圣",
        "rewardCategory": "NPC",
        "hitCount": 2,
        "configuredAmount": 2,
        "duplicateNpcConverted": true,
        "grants": [
          {
            "rewardType": "NPC",
            "rewardName": "剑圣",
            "rewardCategory": "NPC",
            "hitCount": 1,
            "amount": 1,
            "duplicateNpcConverted": false
          },
          {
            "rewardType": "ITEM",
            "rewardName": "剑圣碎片",
            "rewardCategory": "NPC_FRAGMENT",
            "hitCount": 1,
            "amount": 5,
            "duplicateNpcConverted": true
          }
        ]
      }
    ],
    "omittedRewardTypeCount": 0,
    "occurredAt": "2026-08-13 14:30:00",
    "expiresAt": "2026-08-13 14:35:00"
  },
  "serverTime": "2026-08-13 14:30:01"
}
```

外层 `playerId=null` 表示公共广播。外层 `eventId` 格式为 `card-draw-broadcast:{UUIDv4}`，是必须使用的不透明去重主键，不要解析其中内容。`data.content` 已由服务端安全渲染，Unity 直接展示。公开 `data` 不包含玩家 ID、抽奖批次 ID、资产 ID、卡池编码或奖励稳定编码，也不承诺图标、品质或资产定位字段；UTF-8 JSON 序列化最多 6500 字节。

`data.rewards` 最多 5 类，每项仅有 `rewardName/rewardCategory/hitCount/configuredAmount/duplicateNpcConverted/grants`；`grants` 每项仅有 `rewardType/rewardName/rewardCategory/hitCount/amount/duplicateNpcConverted`。`omittedRewardTypeCount` 表示因 5 类或 6500 字节上限而省略的聚合项数量。重复 NPC 的正文仍显示抽中的 NPC，`grants` 保留碎片实际到账事实，最终到账仍以 HTTP 抽奖响应为准。

### 55.4 Unity 接入

1. 收到 `CARD_DRAW_RARE_BROADCAST` 后校验外层 `eventId`、`data.content` 和 `data.expiresAt`。
2. 使用有界 LRU/TTL 集合按外层 `eventId` 去重；这是协议硬要求。Redis 已接收广播后若 Outbox 完成更新失败，事件仍可能被再次领取并重发。
3. 已过期消息直接丢弃；未过期且未处理的消息进入 FIFO 队列，一次只播放一条，建议单条停留 4 至 6 秒。
4. 在游戏顶部公屏或跑马灯区域展示 `data.content`；`data.rewards` 只用于可选文字摘要、埋点或详情展示，不提供图标、品质或资产定位能力，也不能重复发奖。
5. 场景切换时保留内存队列，退出登录时清空队列与该账号临时去重状态；断线重连后不请求历史通报。

当前仓库不包含 Unity 工程，因此本次交付的是后端、管理端和完整协议；Unity 顶部轮播 UI、动画及队列需要客户端按本节接入。

### 55.5 Outbox 重试边界、数据库与上线

抽奖扣费、随机结果、奖励发放、批次成功和 Outbox 写入处于同一数据库事务；Outbox 写入失败时整次抽奖回滚，相同 `Idempotency-Key` 重放成功批次不会重复创建事件。玩家昵称使用抽奖锁定玩家时的快照。

V80 新增 `card_draw_pool.broadcast_enabled`、`card_draw_pool.broadcast_template`、`card_draw_reward_config.broadcast_on_win` 和 `card_draw_broadcast_outbox`。Outbox 使用数据库时间、`FOR UPDATE SKIP LOCKED`、短租约和每轮唯一 owner fencing；Redis 尚未接收广播且调用失败时按 `1/2/4/8/16/30` 秒退避，5 分钟到期后标记 `EXPIRED`。

单机可使用 `REALTIME_PUSH_MODE=local_only`；多实例生产必须使用 `REALTIME_PUSH_MODE=redis_pubsub`。公共广播通过 Redis 显式 `BROADCAST` 回环所有节点，每个节点只发送本机在线会话，并在发送前逐会话复验令牌。

该链路不是端到端至少一次投递。Outbox 只保证数据库事件在过期前持续尝试交给 Redis；Redis Pub/Sub 被 broker 接收后不保存可重放消息，各节点订阅、逐会话鉴权和 WebSocket 发送均为实时 best-effort。节点短暂断连、订阅异常、会话并发关闭或网络丢包均可能导致漏收，且不会历史补发。客户端仍必须按 `eventId` 去重，因为 broker 接收成功后数据库完成更新失败可能触发重发。

“发布成功”指标只表示当前推送层接受广播，不表示所有在线会话已展示。可直接采集：

- `game.card.draw.broadcast.outbox.events{outcome=published|retry_scheduled|expired}`：发布、重试和过期计数。
- `game.card.draw.broadcast.outbox.pending`：有效期内待处理及处理中数量。
- `game.card.draw.broadcast.outbox.oldest.age.seconds`：最老有效积压事件年龄秒数。

还应监控订阅节点与会话侧健康度；不能仅凭 Outbox 终态判断端到端送达。

上线前保持全部卡池 `broadcastEnabled=false`。先停止旧版本写入并备份数据库，确认 `avatar_resource_manifest_import` 和 `avatar_resource_manifest_item` 均为空；任一表非空时 V102 会安全阻断并保留原数据，必须停止发布且不得 `repair`。只启动一个节点执行 Flyway 至 V104，核对 V80、V81、V81.1、V82 至 V104 均为 `success=1`、抽卡 Outbox 索引、成长对账任务/失败表、V82 成长约束、V85 批量升级表和材料索引、V86 最终材料约束、V87 资源收获任务基础结构、V88 前向加固、V89 场景里程碑约束、V90 永久累计统计与唯一身份约束、V91 永久累计进度封顶约束、V92 NPC 获取渠道首发结构与独立发布版本状态、V93 最终渠道约束、V94 运营编号与幂等键、V95/V96 客服结构、V97/V98 固定头像约束、V99 历史远程头像归一、V100 系统头像合同、V101 固定头像正式目录与 Unity 清单证据、V102 前向加固合同、V103 默认头像重新验收前置状态和 V104 最低客户端版本约束，以及 V83/V84 默认关闭且 v1/v2 兼容的新手引导；随后把包含公共 payload、Redis `BROADCAST`、成长持久任务、批量升级、固定头像和逐会话鉴权逻辑的应用版本滚动升级到全部实时节点。确认所有节点版本一致、Redis 订阅及 Worker 指标正常后，才可由管理端开启 `broadcastEnabled`。只要仍有旧节点就不得提前开启。V1-V104 发布后均冻结，下一次数据库变化从 V105 开始。

### 55.6 验收清单

- 关闭总开关或只命中未勾选奖项：无公屏消息。
- 单抽命中勾选奖项：在线客户端收到一条消息。
- 五连命中多个勾选奖项：只收到一条合并消息，顺序与首次命中一致。
- 重复 NPC：正文显示 NPC，`grants` 显示碎片实际到账。
- 同一抽奖请求重试：不重复扣费、发奖或创建 Outbox；实时重复到达时 Unity 只播放一次。
- 玩家退出或 token 过期但连接未及时断开：该连接不接收广播并被服务端关闭。
- Redis 停止后恢复：未过期事件继续重试，超过 5 分钟的事件不补播。
- Redis 已接收后让某订阅节点断连：允许该节点本地会话错过消息，不进行历史补推。
- 滚动发布：全部实时节点完成包含公屏通报能力的当前应用版本升级并验证前保持 `broadcastEnabled=false`，完成后再开启并验证首条通报。

## 56. 玩家新手引导

新手引导是独立的服务端权威状态机。Unity 负责展示和发起允许的操作，不能自行完成业务步骤或发奖。玩家首次分配后永久绑定当时的冻结版本；运营发布新版本只影响之后首次分配的玩家。

首版 `MAIN_FLOW` 默认关闭，预置流程为：

| 顺序 | 步骤 | 完成方式 | 服务端权威条件 |
| ---: | --- | --- | --- |
| 1 | `WELCOME` | `CLIENT_ACK` | 玩家确认欢迎说明 |
| 2 | `OPEN_HOME_MAP` | `CLIENT_ACK` | 玩家确认已打开经营地图 |
| 3 | `FOCUS_FREE_LAND` | `CLIENT_ACK` | 玩家确认已看到目标地块 |
| 4 | `PLACE_FARM_LAND` | `BUSINESS_EVENT` | 原地块接口真实完成目标地块操作 |
| 5 | `START_FARM_BUILD` | `BUSINESS_EVENT` | 在步骤 4 绑定的同一玩家地块实例开始目标建筑 |
| 6 | `COMPLETE_FARM_BUILD` | `BUSINESS_EVENT` | 步骤 5 绑定的同一玩家建筑实例真实完成 |
| 7 | `FINISH` | `CLIENT_ACK` | 玩家确认完成说明后进入 `COMPLETED` |

### 56.1 玩家接口

三个接口都使用当前登录玩家身份，必须传 `Authorization: Bearer <player-token>`，不得提交 `playerId`。

```http
GET  /api/onboarding/current
POST /api/onboarding/steps/{stepCode}/ack
POST /api/onboarding/skip
```

查询接口用于登录、重连、回到前台、ACK 超时和本地状态不确定时恢复，不需要幂等键。符合当前分配策略但尚未绑定的玩家，会在查询事务内原子绑定正式版本。

进行中快照示例：

```json
{
  "code": 200,
  "message": "查询新手引导成功",
  "data": {
    "available": true,
    "contractVersion": 1,
    "minimumClientVersionCode": 10001,
    "guideCode": "MAIN_FLOW",
    "guideVersion": 3,
    "instanceId": 1024,
    "stateVersion": 6,
    "status": "IN_PROGRESS",
    "allowSkip": true,
    "currentStep": {
      "stepCode": "START_FARM_BUILD",
      "sequence": 5,
      "sceneCode": "HOME",
      "presentationType": "SPOTLIGHT",
      "uiTargetKey": "BUILDING_FARM_CARD",
      "inputMode": "ALLOW_TARGET_ONLY",
      "actionKey": "OPEN_BUILDING_PANEL",
      "completionType": "BUSINESS_EVENT",
      "title": "建造第一座农场",
      "content": "选择农场并开始建造。",
      "presentationParams": {},
      "businessContext": {
        "eventType": "BUILDING_STARTED",
        "landConfigId": null,
        "buildingConfigId": 2001,
        "playerLandId": 501,
        "playerBuildingId": null
      }
    },
    "serverTime": "2026-08-14T10:00:00"
  }
}
```

关键字段：

| 字段 | 说明 |
| --- | --- |
| `contractVersion` | Unity 展示协议版本，不支持时停止自动操作并进入统一升级流程 |
| `minimumClientVersionCode` | 冻结引导要求的最低客户端数字版本 |
| `guideVersion` | 玩家永久绑定的内容版本，写请求必须回传 |
| `stateVersion` | 玩家进度乐观并发版本，写请求必须回传 |
| `status` | `NOT_STARTED / IN_PROGRESS / PAUSED / COMPLETED / SKIPPED` |
| `allowSkip` | 本次快照是否允许跳过，客户端不能只看本地配置 |
| `currentStep` | 当前展示步骤；终态或无可用引导时为 `null` |
| `businessContext` | 业务步骤的只读定位上下文；`CLIENT_ACK` 步骤为 `null` |

`businessContext` 只允许 `eventType/landConfigId/buildingConfigId/playerLandId/playerBuildingId`。地块放置步骤返回目标 `landConfigId`；建筑开始步骤返回目标 `buildingConfigId` 和来源 `playerLandId`；建筑完成步骤同时返回绑定的 `playerBuildingId`、`playerLandId` 和目标 `buildingConfigId`。这些 ID 只用于原业务接口和页面定位，不能作为完成证明，也不能替换成同配置的其他实例。

没有正式版本、总开关关闭且玩家尚未绑定、灰度未命中或暂不接受新分配时，查询仍返回 HTTP 200，但 `available=false`、`status=NOT_STARTED`、`currentStep=null`。已完成或已跳过时 `available=true`，保留版本和实例信息，`currentStep=null`。

### 56.2 确认展示步骤

```http
POST /api/onboarding/steps/{stepCode}/ack
Authorization: Bearer <player-token>
Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
Content-Type: application/json

{
  "expectedGuideVersion": 3,
  "expectedStateVersion": 6
}
```

`stepCode` 必须等于最新快照的当前步骤，当前步骤必须是 `CLIENT_ACK`。`Idempotency-Key` 必须是标准 UUID；一次确认意图先持久化 UUID、路径和请求体，超时或 5xx 重试必须复用原值。相同 UUID 和相同请求重放首次完整响应，不重复推进或推送；相同 UUID 改步骤或版本返回 `8005`。

幂等重放会原样返回首次响应，因此可能低于客户端后来取得的 `stateVersion`。Unity 只能应用相同 `guideVersion` 且 `stateVersion` 不低于本地的写响应；旧响应直接丢弃并调用 `GET /api/onboarding/current`，不能回退页面或 `businessContext`。

### 56.3 跳过引导

```http
POST /api/onboarding/skip
Authorization: Bearer <player-token>
Idempotency-Key: 733e30be-1551-4a1b-9e31-5bf0f43a1741
Content-Type: application/json

{
  "expectedGuideVersion": 3,
  "expectedStateVersion": 6
}
```

只有最新快照 `allowSkip=true` 时可调用。成功后进入终态 `SKIPPED`，不发额外奖励、不完成剩余步骤，重登也不会重开。跳过使用与 ACK 相同的 UUID 幂等和双版本规则；首次结果只能用原 UUID 重放，已经跳过后换新 UUID 再提交返回 `8006`。

### 56.4 可信业务推进与建筑保护

`LAND_PLACED / BUILDING_STARTED / BUILDING_COMPLETED` 不提供通用“完成引导”接口。Unity 必须调用原有地块或建筑接口；原业务成功事务内才写入引导证据，事务回滚时引导推进一并回滚，实时消息只在提交后发送。

建筑开始必须发生在步骤 4 已冻结的同一 `playerLandId`，建筑完成必须对应步骤 5 冻结的同一 `playerBuildingId`。开始证据写入后、完成证据写入前，取消建造或拆除该实例返回：

```json
{
  "code": 1008,
  "message": "该建筑正在用于新手引导，完成对应引导步骤前不能取消或拆除"
}
```

HTTP 状态为 409。其他同配置地块或建筑不能代替绑定实例推进。首版发布规则还要求建筑完成步骤紧跟对应的建筑开始步骤，并拒绝带前置建筑依赖的引导目标，避免产生不可恢复的死链。

### 56.5 WebSocket 刷新提示

连接继续使用 `/api/ws/realtime` 和请求头 Bearer Token。首次分配或状态真实变化并提交后，仅向本人发送：

```json
{
  "type": "PLAYER_ONBOARDING_UPDATED",
  "requestId": null,
  "eventId": "7bb4c171-1a82-4d37-a7a3-89f32196e860",
  "playerId": 42,
  "data": {
    "playerId": 42,
    "instanceId": 1024,
    "guideCode": "MAIN_FLOW",
    "guideVersion": 3,
    "stateVersion": 7,
    "status": "IN_PROGRESS",
    "currentStepCode": "COMPLETE_FARM_BUILD",
    "changedAt": "2026-08-14T10:00:01"
  },
  "serverTime": "2026-08-14T10:00:01"
}
```

该消息只提示刷新，不是完整快照。Unity 收到更高版本后调用查询接口；重复、乱序或漏收不影响权威状态。WebSocket 发送失败不回滚事务，断线期间不补发，重连后必须主动查询。

### 56.6 错误码

| HTTP | 业务码 | 含义 | Unity 处理 |
| ---: | ---: | --- | --- |
| 503 | `8001` | 绑定版本、步骤配置或冻结证据暂不可用/损坏 | 暂停引导、上报并稍后查询，不自动跳过 |
| 409 | `8002` | 请求步骤不是当前步骤 | 丢弃本地步骤并查询 |
| 409 | `8003` | 当前步骤必须由可信业务事件完成 | 停止 ACK，执行原业务后查询 |
| 409 | `8004` | 引导版本或状态版本冲突 | 查询并重新渲染，不自动替换参数重放 |
| 409 | `8005` | 同一 UUID 被用于不同请求 | 保留原请求，新意图使用新 UUID |
| 403 | `8006` | 当前策略或状态不允许跳过 | 隐藏跳过按钮并查询 |
| 409 | `1008` | 引导绑定建筑不能取消或拆除 | 保留实例，完成或加速对应建造 |

请求体缺失、UUID 格式错误、版本非正数或步骤编码格式错误返回 HTTP 400；未登录或 Token 失效返回 401；频率过高返回 429。

### 56.7 运营配置与 V83-V89 上线

配置中心固定使用 `moduleCode=PLAYER_ONBOARDING`、`configKey=MAIN_FLOW`。管理端支持编辑步骤、展示锚点、完成方式、灰度、跳过、最低客户端版本和新分配开关。发布时会校验线性流程、白名单、纯文本安全、整数配置 ID、初始等级、地块类型/允许建筑类型、目标建筑一级成本和新玩家初始资源。

`LAND_BUILDING / PLAYER_BOOTSTRAP / LAND_SCENE_CATALOG / ITEM_CATALOG` 的即时与定时发布统一执行反向依赖保护。接受新分配期间，候选配置必须保持完整流程、HOME 首场景、1级首格和开局资源可达；存在在途玩家时，只保护各冻结版本最早未完成步骤及之后的依赖，禁止停用、提级、涨价或破坏兼容性，名称、图标、降价及已全部通过的历史步骤仍可正常调整。关闭“接受新分配”可以解除未来开局资源约束，但不会绕过在途玩家剩余地块、建筑和场景保护；总开关关闭也只是暂停展示。已绑定版本和历史步骤不得删除。

V83 创建 `onboarding_policy`、`onboarding_plan_version`、`onboarding_step_config`、`player_onboarding_progress`、`player_onboarding_step_record`、`player_onboarding_request` 六张表，预置默认关闭的七步冻结 v1，固定 CRC32 为 `1070104245`。V84 不改写 V83：它保留已绑定玩家的 v1，归一化终态暂停标记，增加稳定锚点/动作、`ARROW`、同地块/同建筑实例引用和更严格约束，最后才把未来分配切换到兼容 v2；固定 CRC32 为 `1114405186`。V85 创建 NPC/装备一键升级完成态批次、逐级流水关联和初始材料约束，固定 CRC32 为 `-1472509603`；V86 不改写 V85，前向把整批同款材料上限放宽到 5000，固定 CRC32 为 `-179532443`；V87 增加资源收获次数任务，固定 CRC32 为 `-1481082424`；V88 不改写 V87，前向修复历史空快照、重建显式 NULL 安全约束并增加当前资源任务依赖索引，固定 CRC32 为 `-892889675`；V89 增加场景开辟里程碑并扩展三元唯一键，固定 CRC32 为 `-1964148448`；V90 增加三种事件驱动永久累计成就、永久累计统计表和统一成就唯一身份，固定 CRC32 为 `-319452238`；V91 不改写 V90，先封顶历史永久累计进度，再将 `player_task_lifetime_stat.progress_amount` 约束收紧为 `0..9000000000000000`，固定 CRC32 为 `-613092297`。仍停在 V83 的环境必须在启动前独立执行只读预检，并在升级 V102 前停止写入、备份且确认两张头像清单审计表为空，再由 Flyway 顺序升级至 V104；V103 成功后 `AVATAR_001` 处于 `STAGED`，V104 成功后其最低客户端版本号为 1，需重新导入 Unity 清单并显式发布。V83 至 V104 一旦执行成功都不得修改，后续数据库变化从 V105 开始，禁止用 Flyway `repair` 掩盖校验和漂移。

完整 Unity 锚点白名单、管理端发布细则和联调验收见仓库 `docs/player-onboarding-api.md`。

## 57. V87/V88 累计收获指定资源次数任务

V87 在现有日常、周常任务中增加 `RESOURCE_COLLECT_COUNT`；V88 在不改写 V87 的前提下加固历史快照、数据库约束和当前任务依赖查询。该目标表示“从建筑实际收获指定物品的建筑次数”，不按资源数量计数，也不增加客户端上报进度接口。

### 57.1 最终计数规则

1. 单个建筑一次实际收获目标物品正数数量，任务增加 `1`；一次收获 1 木材和一次收获 1000 木材的计数相同。
2. 场景一键收取或全场景一键收取时，有 `N` 个不同玩家建筑实际产出目标物品，任务增加 `N`。
3. 同一建筑一次同时收获木材和铁矿时，木材任务与铁矿任务分别增加 `1`；目标物品 ID 严格隔离，不会串进度。
4. 空收取、产物数量为 0、只进行查询结算、幂等重放均不计数。
5. 历史生产收取不回补；V87 上线并由运营发布对应任务后的新收取才开始累计。
6. 没有对应进行中任务时不写无意义的资源任务永久事件；一次多产物收取只物化一次当前周期任务。
7. 任务推进、建筑暂存量清零、背包入账、收取记录和钻石审计位于同一数据库事务；任一步失败全部回滚，提交后才发送生产收取实时消息。

木材、铁矿、钻石和净化剂都可以作为目标，只要它们仍属于“启用物品 + 启用建筑 + 启用等级 + 启用配方”的实际产物。

### 57.2 Unity 查询接口

接口没有新增路径，继续使用：

```http
GET /api/tasks
Authorization: Bearer <player-token>
```

资源收获任务示例：

```json
{
  "id": 92001,
  "taskCode": "DAILY_WOOD_COLLECTION",
  "taskName": "每日收获木材",
  "description": "累计从建筑收获木材5次",
  "taskCategory": "ROUTINE",
  "periodType": "DAILY",
  "periodKey": "20260818",
  "objectiveType": "RESOURCE_COLLECT_COUNT",
  "targetSceneId": null,
  "targetSceneKey": null,
  "targetSceneName": null,
  "targetItemId": 2,
  "targetItemKey": "wood",
  "targetItemName": "木材",
  "targetItemIcon": "wood",
  "targetItemType": "RESOURCE",
  "targetAmount": 5,
  "progressAmount": 3,
  "experienceReward": 100,
  "randomNpcPoolName": null,
  "randomNpcDrawCount": 0,
  "fixedRewards": [],
  "randomNpcCandidates": [],
  "status": "IN_PROGRESS",
  "canClaim": false,
  "completedTime": null,
  "claimedTime": null
}
```

`targetItemId/targetItemKey/targetItemName/targetItemIcon/targetItemType` 是任务发布并物化给玩家时冻结的目标物品快照。Unity 只负责展示；不要用名称匹配任务，也不要本地增加 `progressAmount`。

玩家执行以下任一正式收取接口成功后，Unity 使用 HTTP 收取响应更新背包和生产状态，再调用 `GET /api/tasks` 刷新权威任务进度：

```http
POST /api/production/collect
POST /api/production/collect-all
```

单建筑、单场景和全场景范围及 `Idempotency-Key` 规则保持第 41.5 节不变。网络超时重试必须复用原 Key；服务端重放第一次响应且不重复推进任务。

任务完成后的领取接口也不变：

```http
POST /api/tasks/{playerTaskId}/claim
Authorization: Bearer <player-token>
```

经验、固定资产奖励、随机 NPC、领取幂等和错误码继续遵循第 19.3 节与第 42 章。

### 57.3 管理端配置

入口：`配置中心 -> 玩家任务`。

```text
moduleCode = PLAYER_TASK
configKey  = TASKS
```

新增或编辑日常/周常任务时，目标类型选择“累计收获指定资源次数”，再从“目标产物”搜索框选择物品并填写目标次数。页面顶部会展示统一计数规则。

字典接口：

```http
GET /api/ops/config/dictionaries
Authorization: Bearer <operator-token>
```

响应新增 `productionItems`，其条目结构与 `items` 相同，但只包含当前实际可生产的启用物品。任务配置提交包含：

```json
{
  "objectiveType": "RESOURCE_COLLECT_COUNT",
  "targetItemId": 2,
  "targetItemKey": "wood",
  "targetItemName": "木材",
  "targetItemIcon": "wood",
  "targetItemType": "RESOURCE",
  "targetAmount": 5
}
```

服务端发布时会根据 `targetItemId + targetItemKey` 重新解析正式资料并覆盖名称、图标和类型，客户端或运营请求不能伪造快照。允许的目标类型为 `CURRENCY / RESOURCE / PRODUCT`，NPC 碎片不能作为收获次数目标。

建筑生产配置发布、“从土地建筑同步”和物品基础资料启停都会执行反向依赖保护：如果变更会让启用资源任务，或当前业务日/周仍进行中的玩家资源任务，失去最后一个实际生产来源，则整次发布返回 HTTP 409 并回滚。运营必须先保留另一条生产来源，或等待/妥善处理当前周期任务后再调整，不能静默制造不可完成任务。

新建、保持启用、重新启用或更换目标物品的资源任务，目标必须存在于当前 `productionItems`。仅当任务已经发布、本次保持或调整为 `enabled=false`，并且 `targetItemId + targetItemKey` 均未改变时，才允许保留已失去生产来源的历史停用快照；该例外只用于避免历史停用任务阻断后续全量发布，不会让它重新对玩家生效。

玩家任务物化固定按“玩家聚合锁 → 待创建资源任务配置 ID 升序共享锁”执行。共享锁后会复核 `enabled + objectiveType + configVersion`：若运营发布先提交，本次物化跳过已停用或已变更配置；若玩家事务先取得共享锁，运营发布等待玩家实例和奖励快照提交。该锁序与运营“模块配置键锁 → 全局发布锁 → 业务行锁”配合，关闭停用任务与移除最后生产来源之间的竞态窗口。

### 57.4 数据库与幂等边界

V87 新增并由固定校验和 `-1481082424` 冻结：

- `player_task_config.target_item_*`：正式任务目标物品及发布快照。
- `player_task.target_item_*_snapshot`：玩家任务实例的冻结目标。
- `player_task_progress_event.target_item_id`：可信进度事件的目标维度。
- 三个按目标物品查询/审计的复合索引，以及三个到 `item_config(item_id)` 的限制删除外键。
- 三张任务表的目标类型约束，以及配置、实例、事件的目标物品语义约束。

V88 由固定校验和 `-892889675` 冻结：先根据仍受外键保护的场景/物品正式配置，补齐 V64 遗留的空场景快照和可能存在的空资源目标快照；再重建带显式 `IS NOT NULL` 的严格 CHECK，最后增加 `idx_player_task_resource_target_current` 当前周期依赖保护索引。V88 是独立前向迁移，绝不通过修改已执行的 V87 或执行 `flyway repair` 处理历史库。

资源收取事件来源格式：

```text
PRODUCTION_COLLECT:{collectRecordId}:{targetItemId}
```

同一收取记录、同一目标物品只会推进一次。事件重放必须同时匹配目标物品 ID 和进度增量，否则返回任务进度幂等冲突。

### 57.5 上线与验收

用户手动启动后端时保持：

```text
FLYWAY_ENABLED=true
FLYWAY_BASELINE_ON_MIGRATE=false
```

停止旧版本写入、备份数据库并确认 `avatar_resource_manifest_import` 和 `avatar_resource_manifest_item` 均为空后，应用会自动从数据库当前版本顺序升级到 V104；已经应用 V87 的数据库顺序执行 V88 至 V104，已经应用 V88 的数据库顺序执行 V89 至 V104，已经应用 V89 的数据库顺序执行 V90 至 V104，已经应用 V90 的数据库顺序执行 V91 至 V104，已经应用 V91 的数据库顺序执行 V92 至 V104，已经应用 V92 的数据库顺序执行 V93 至 V104。任一清单表非空时 V102 会安全失败并保留原数据，必须停止发布且不得执行 `repair`。不要提前手工执行迁移，也不要使用 `flyway repair` 掩盖校验和问题。多节点环境先启动一个新版本节点，确认 `flyway_schema_history` 中 V87/V88/V89/V90/V91/V92/V93/V94/V95/V96/V97/V98/V99/V100/V101/V102/V103/V104 均为 `success=1`，且校验和分别为 `-1481082424`、`-892889675`、`-1964148448`、`-319452238`、`-613092297`、`1615105684`、`-2034435388`、`-246164165`、`-1236124090`、`292469054`、`151659432`、`-2063702854`、`67421201`、`-880935665`、`1155793846`、`-707871511`、`-172308135`、`-37724955`，再滚动启动其余节点。V103 完成后 `AVATAR_001` 为 `STAGED/minimumClientVersionCode=0`；V104 完成后版本号为 1，必须重新导入真实 Unity 清单并由管理员发布 `ACTIVE`。

本次实现已覆盖并验证：

- 单建筑大量资源只计 `1`，多建筑一键收取按建筑数累计，多产物按物品 ID 稳定顺序推进。
- 木材/铁矿目标隔离、空收取、无匹配任务、幂等重放和事件冲突。
- 权威目标快照、非生产产物拒绝、玩家实例冻结与 Unity 返回字段。
- 管理端 `productionItems` 包含木材、铁矿、钻石、净化剂，并排除 NPC 碎片。
- V86 旧库可连续迁移到 V104；已应用正确 V87 且存在历史 NULL 场景/资源快照的测试库会先由 V88 前向修复，再由 V89 增加场景里程碑、V90 增加永久累计统计与唯一身份约束、V91 封顶并约束永久累计进度、V92/V93 增加并加固 NPC 获取渠道、V94 分离运营编号、V95/V96 完善客服结构、V97～V102 加固头像合同，最后由 V103 取消默认头像历史兼容例外；错误 CHECK/FK/唯一键及超限进度写入会被拒绝。
- V87 校验和保持数据库已应用值 `-1481082424`；用户手动启动当前后端时由 Flyway 按现有数据库版本顺序执行尚未应用的 V88 至 V104，无需也不得执行 `repair`。
- 真实 MySQL 双事务并发测试应在发布前启用并确认：物化事务持有配置 `FOR SHARE` 时运营停用更新会等待；运营停用先提交时，后续锁定当前读不会返回已停用配置。本地未启用外部环境时，该项按条件跳过，不能记为通过。
- 本次可重复验证的管理端 TypeScript/Vite 生产构建、头像专项测试和 Flyway 脚本合同测试通过；真实 MySQL 顺序迁移、双事务并发和目标 Unity LTS 构建仍需在对应环境执行，最终发布不能用条件跳过替代。

仓库详细说明见 `docs/player-task-achievement-api.md`、`docs/building-production-api.md`、`docs/operator-config-center.md` 和 `docs/database-script-guide.md`。V104 是本段写作时的历史边界；当前最高迁移为 V137，下一次数据库结构变化使用 V138。V87/V88/V89/V90/V91/V92/V93/V94/V95/V96/V97/V98/V99/V100/V101/V102/V103/V104 固定 CRC32 分别为 `-1481082424`、`-892889675`、`-1964148448`、`-319452238`、`-613092297`、`1615105684`、`-2034435388`、`-246164165`、`-1236124090`、`292469054`、`151659432`、`-2063702854`、`67421201`、`-880935665`、`1155793846`、`-707871511`、`-172308135`、`-37724955`。V101 至 V104 均保持冻结且不得通过 `repair` 接受漂移。

## 58. V89 按场景开辟格数宝箱

V89 新增永久成就目标 `SCENE_SLOT_UNLOCK_COUNT`，用于配置“在指定场景累计开辟 N 格”的宝箱。这里的“开辟”以服务端格子状态 `placementUnlocked=true` 为唯一准则：等级条件、净化条件和付费购买条件必须全部满足，但不要求格子上已经存在或完成建筑。

### 58.1 运营配置

入口仍为 `配置中心 -> 玩家任务 -> 新增场景宝箱`。运营选择“开辟指定格数”后：

1. 目标场景必须来自启用的正式场景字典。
2. `targetAmount` 允许 1～32；可在同一场景配置 10、20、32 等多个档位。
3. 完全相同的 `objectiveType + targetSceneId + targetAmount` 禁止重复；不同档位或不同目标类型可以并存。
4. “32 格全部建成”仍对应 `SCENE_FULLY_BUILT`，目标固定 32 且不可编辑，不会被新目标替代。
5. 奖励继续复用任务宝箱能力：非负经验、固定物品/资源、固定装备、随机完整 NPC 池及重复 NPC 转碎片；完整 NPC 不能作为固定奖励。

### 58.2 玩家端效果

Unity 继续调用 `GET /api/tasks` 查询，继续调用 `POST /api/tasks/{playerTaskId}/claim` 领取，不新增请求字段、接口或进度上报。任务响应中：

```json
{
  "taskCategory": "ACHIEVEMENT",
  "periodType": "LIFETIME",
  "objectiveType": "SCENE_SLOT_UNLOCK_COUNT",
  "targetSceneId": 1,
  "targetSceneKey": "HOME",
  "targetSceneName": "家园",
  "targetAmount": 10,
  "progressAmount": 10,
  "status": "COMPLETED",
  "canClaim": true
}
```

购买或净化格子时，服务端固定按“玩家聚合锁 → 物化并锁定场景成就 → 地块/库存/资源写入 → 权威状态对账”执行；任一步失败整单回滚。一次操作若让当前开辟数跨过多个档位，全部已达到的未完成宝箱会一起变为 `COMPLETED`。老玩家首次打开任务列表时，服务端会根据当前格子数据库状态补完成，不依赖历史事件。

### 58.3 数据库与上线

`V89__add_scene_slot_unlock_task_objective.sql` 只做前向变更，不修改 V64、V87 或 V88：

- 任务配置唯一键扩展为 `(objective_type, target_scene_id, target_amount)`。
- 配置和实例 CHECK 允许 `SCENE_SLOT_UNLOCK_COUNT` 的 1～32 边界，并保留 `SCENE_FULLY_BUILT=32`。
- 两种场景成就都必须冻结完整场景快照，且不能夹带资源收获目标物品快照。
- 进度事件目标枚举同步识别新类型；客户端仍不能直接写入进度。

V89 固定 CRC32 为 `-1964148448`。用户手动启动后端前保持 `FLYWAY_ENABLED=true`、`FLYWAY_BASELINE_ON_MIGRATE=false`；按第 65.7 节完成 V102 前置空表检查后，正常启动会由 Flyway 自动顺序升级，不得手工执行脚本，也不得使用 `flyway repair`。V89 的历史能力和校验和保持不变；当前仓库最高迁移为 V137，下一次数据库变化必须新增 V138。

## 59. V90 三类永久累计成就

V90 将现有可信任务进度事件扩展为可同时驱动周期任务与永久成就的统一入口。运营可配置“累计完成建筑建造”“累计完成建筑升级”“累计收获指定资源次数”三类永久累计宝箱；原有按场景开辟格数、32 格全部建成、每日任务和每周任务保持不变。

### 59.1 最终效果与进度语义

- `BUILDING_COMPLETION`：全局累计完成建筑建造次数，不绑定场景或物品。
- `BUILDING_UPGRADE`：全局累计完成建筑升级次数，不绑定场景或物品。
- `RESOURCE_COLLECT_COUNT`：按指定产出物品累计成功收获次数，绑定 `targetItemId` 及其键值、名称快照。
- 一次受信任的建造完成、升级完成或资源收获只写入一个幂等进度事件；同一事务内，该事件会推进所有匹配的 `DAILY`、`WEEKLY`、`LIFETIME` 任务实例以及永久累计统计，避免重复计数和跨周期不一致。
- 永久成就允许配置多个档位。例如玩家当前累计建造 8 次，一次可信事件将累计值推进到 10 时，目标为 9 次和 10 次的未完成永久成就可同时变为 `COMPLETED`，各档奖励仍需分别领取。
- 完全相同的事件重放不会重复推进；相同来源标识携带不同目标或增量属于幂等冲突，整单失败并保留原始结果。

### 59.2 玩家端与 Unity 接入

Unity 不新增接口，也不得上报任务进度：

1. 打开任务或成就页面时继续调用 `GET /api/tasks`。
2. 领取奖励时继续调用 `POST /api/tasks/{playerTaskId}/claim`。
3. 在建造完成、建筑升级成功或资源收获成功后重新拉取任务列表，或按现有页面刷新策略在再次进入页面时刷新。
4. 使用 `taskCategory` 与 `periodType=LIFETIME` 识别永久成就，使用 `objectiveType` 区分三种累计目标。
5. 进度统一展示 `progressAmount/targetAmount`；资源目标展示 `targetItemId/targetItemKey/targetItemName`，两种全局建筑目标的 `targetItem*` 与 `targetScene*` 均为空。

示例：

```json
{
  "taskCategory": "ACHIEVEMENT",
  "periodType": "LIFETIME",
  "objectiveType": "BUILDING_COMPLETION",
  "targetSceneId": null,
  "targetItemId": null,
  "targetItemKey": null,
  "targetItemName": null,
  "targetAmount": 50,
  "progressAmount": 18,
  "status": "IN_PROGRESS",
  "canClaim": false
}
```

### 59.3 管理端配置与唯一性

管理端入口仍为 `PLAYER_TASK / TASKS`，页面标题为“玩家任务与永久成就”，通过“新增永久成就”配置。运营必须遵守以下规则：

- 成就维度固定为：场景成就 `SCENE(sceneId)`，指定资源成就 `ITEM(targetItemId)`，建筑建造/升级成就 `GLOBAL`。
- 永久成就档位唯一身份为 `objectiveType + dimensionType/dimensionId + targetAmount`。完全相同的档位禁止重复，已停用配置也占用该身份；不同目标值可作为多档成就并存。已发布档位的目标类型、统计维度和目标值不可原地修改，调整时新增任务代码并停用旧档；名称、描述、奖励、排序和启停仍可维护。
- 相同 `targetAmount` 只有在目标类型或维度不同时才可并存。例如不同资源可各配置“累计收获 100 次”，但同一资源的同一档位只能有一条。
- `BUILDING_COMPLETION`、`BUILDING_UPGRADE` 不允许绑定场景或物品；`RESOURCE_COLLECT_COUNT` 不允许绑定场景，且必须选择当前启用的建筑生产产出物品。
- 只有 `SCENE_FULLY_BUILT` 固定要求物品奖励和随机完整 NPC 池；其余永久成就继续使用现有灵活奖励组合。

### 59.4 历史数据的可信下界

V90 只迁移可审计的历史证据，不猜测玩家真实历史总次数：

- 已完成的 `building_task` 记录以 `INSERT IGNORE` 补入可信事件账本，并区分 `BUILD` 与 `UPGRADE`。
- 永久累计统计从 `player_task_progress_event` 中聚合；资源历史只采用此前已经可靠记录的资源收获进度事件。
- 不从 `response_json`、当前背包余额或当前建筑数量反推历史次数，因为这些状态无法证明发生过多少次操作。
- 因此，V90 上线前缺少可信证据的活动不会被虚构补齐，迁移后的历史值是可审计的“可信下界”，可能低于玩家真实历史累计；V90 上线后的新事件按统一可信入口精确累计。
- 晚发布的永久成就档位会从永久累计统计初始化进度，并最多封顶到该档 `targetAmount`；按场景开辟和 32 格全部建成仍按当前权威场景状态对账，不使用永久事件统计。

### 59.5 数据库与上线边界

`V90__add_lifetime_cumulative_task_achievements.sql` 为前向迁移，主要边界如下：

- `player_task_config.achievement_identity` 是生成列，唯一索引 `uk_player_task_achievement_identity` 从数据库层阻止同一永久成就档位重复。
- `player_task_lifetime_stat` 永久保存玩家按目标维度累计的可信总量；`uk_player_task_lifetime_stat_dimension` 保证同一玩家、目标类型和维度只有一行累计值，`idx_player_task_lifetime_stat_objective` 支持目标查询。
- 可信事件账本、永久累计统计和日常/周常/永久任务实例在同一业务事务内更新，任何一步失败均整体回滚。
- V90 固定 CRC32 为 `-319452238`，已发布迁移禁止修改，也不得用 `flyway repair` 掩盖校验和漂移。
- V90 是本节能力首次交付时的历史迁移边界；当前仓库最高迁移为 V137，下一次数据库变化必须新增 V138。

## 60. V91 永久累计进度上限加固

V91 不修改 V90 已发布脚本，也不改变任何 HTTP/WebSocket 路径、请求字段、响应字段或任务业务语义。`V91__cap_lifetime_task_progress.sql` 只对永久累计统计的数据库边界做前向加固：

- 先将 `player_task_lifetime_stat.progress_amount > 9000000000000000` 的历史值封顶为 `9000000000000000`，确保后续约束可以在已有数据库安全建立。
- 重建 `chk_player_task_lifetime_stat_progress`，要求 `progress_amount` 始终位于 `0..9000000000000000`，与 JavaScript 安全整数上限及运行时封顶累加保持一致。
- V91 固定 CRC32 为 `-613092297`；V90 固定 CRC32 保持 `-319452238`，两份迁移执行成功后均禁止修改，也不得使用 `flyway repair` 掩盖漂移。
- 用户手动启动后端前保持 `FLYWAY_ENABLED=true`、`FLYWAY_BASELINE_ON_MIGRATE=false`；按第 65.7 节完成 V102 前置空表检查后，由 Flyway 顺序执行全部尚未应用迁移。当前仓库最高迁移为 V137，下一次数据库变化必须新增 V138。

## 61. V92 NPC 图鉴与服务端获取指引

V92 增加服务端驱动的 NPC 获取渠道、独立配置发布版本、玩家图鉴聚合接口、短效获取指令和奖励到账后的实时刷新事件。旧 NPC 配置及玩家拥有列表接口继续保留，旧 Unity 不会因后端先上线而失效；新 Unity 接入本节接口后，可以由运营调整 NPC 获取入口、优先级、开放时间和等级条件，无需为了改入口重新发版。

### 61.1 改造前后对比

| 对比项 | 改造前 | V92 完成后 |
| --- | --- | --- |
| 图鉴数据 | Unity 分别读取 NPC 配置和玩家拥有列表并自行合并 | `GET /api/npc/catalog` 一次返回配置、拥有状态和当前最佳获取渠道 |
| 获取按钮 | Unity 自己判断是否展示，并容易硬编码活动或页面 ID | 服务端返回 `OWNED/AVAILABLE/LOCKED/UPCOMING/ENDED/UNAVAILABLE` 和展示文案 |
| 点击获取 | Unity 持有活动 ID 后直接跳页，运营改入口通常需要客户端发版 | Unity 先请求短效指令，再按白名单 `routeKey` 打开页面 |
| 多渠道 | 没有统一优先级和自动降级 | 可为同一 NPC 配置多条渠道；可用渠道优先，高优先级失效后自动切换到下一个可用渠道 |
| 配置安全 | 跳转参数缺少统一服务端校验 | 发布时校验 NPC、真实业务来源、时间、等级、优先级和强类型路由参数 |
| 多端刷新 | 主要依赖页面重新打开或手动刷新 | 奖励事务提交后推送 `PLAYER_NPC_ACQUISITION_RESOLVED`，HTTP 图鉴仍是最终权威 |
| 重复 NPC | 客户端难以区分首次获得与转碎片 | `changeType` 明确区分 `NEW_ACQUIRE` 和 `DUPLICATE_CONVERT` |

### 61.2 图鉴接口

```http
GET /api/npc/catalog
Authorization: Bearer <player-token>
```

成功响应中的 `data` 是 NPC 数组：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": [
    {
      "npcId": 1001,
      "npcCode": "legendary_blacksmith",
      "name": "传奇铁匠",
      "quality": "LEGENDARY",
      "icon": "npc/legendary_blacksmith",
      "portraitKey": "legendary_blacksmith_portrait",
      "modelKey": "legendary_blacksmith",
      "description": "擅长锻造的传奇工匠",
      "combatAttribute": "FIRE",
      "maxLevel": 100,
      "produceBonusRate": 0.15,
      "owned": false,
      "acquisition": {
        "status": "AVAILABLE",
        "channelType": "CARD_DRAW",
        "displayName": "限定卡池",
        "buttonText": "前往抽取",
        "message": "前往抽卡获取"
      },
      "skills": [
        {
          "slotIndex": 0,
          "skillId": 901,
          "skillKey": "HEAL_LOWEST",
          "skillName": "急救",
          "skillIcon": "skill/heal_lowest",
          "cooldownRounds": 3,
          "initialCooldownRounds": 1,
          "effectType": "HEAL",
          "targetSide": "ALLY",
          "targetMode": "SINGLE",
          "targetGroups": [],
          "targetCount": null,
          "includeSelf": false,
          "healFormula": "TARGET_MAX_HEALTH_RATE",
          "healBaseValue": 0,
          "healMultiplier": 0.2
        }
      ],
      "configVersion": 7,
      "contractVersion": 1
    }
  ]
}
```

Unity 展示规则：

| `status` | 建议展示 |
| --- | --- |
| `OWNED` | 显示“已拥有”，隐藏获取按钮 |
| `AVAILABLE` | 高亮服务端下发的 `buttonText` |
| `LOCKED` | 按钮置灰，显示服务端 `message` 中的解锁条件 |
| `UPCOMING` | 按钮置灰，显示“即将开放”或服务端文案 |
| `ENDED` | 按钮置灰，显示“活动已结束” |
| `UNAVAILABLE` | 隐藏按钮或显示“暂无获取途径” |

`owned=true` 时服务端始终返回拥有状态，不再让渠道覆盖。服务端先从全部 `AVAILABLE` 渠道中选择数值最小的 `priority`；没有可用渠道时，按 `LOCKED → UPCOMING → ENDED → UNAVAILABLE` 选择最有意义的说明，同状态再按优先级排序。渠道被停用时完全不参与选择。

NPC 被运营设为不可获取后，普通静态渠道不再产生新的获取机会；但已经物化到玩家任务实例中的未领取 NPC 奖励快照，以及玩家真实存在的有效 NPC 邮件，都属于既得权益，仍可能返回 `AVAILABLE`。`OWNED` 或没有任何渠道时，`channelType/displayName/buttonText` 等渠道字段为 `null`；首次尚未发布渠道配置时 `configVersion=0`，Unity 必须允许这些字段为空。

`skills` 始终返回数组，是当前已发布 `IDLE_ADVENTURE` 玩法版本中该 NPC 的技能槽配置，按 `slotIndex` 升序排列。没有生效玩法版本或 NPC 未配置技能时返回空数组。图鉴页一次调用 `/api/npc/catalog` 即可获得全部 NPC 技能，禁止按 NPC 逐个请求；战斗实时冷却以会话响应的 `skill_states` 为准，Unity 不得自行结算冷却。

技能展示字段包括 `skillId/skillKey/skillName/skillIcon/cooldownRounds/initialCooldownRounds/skillMultiplier/damageAttribute/selfBuffPresetId/targetBuffPresetId/effectType/targetSide/targetMode/targetGroups/targetCount/includeSelf/healFormula/healBaseValue/healMultiplier`。其中 `effectType` 支持 `DAMAGE/HEAL/BUFF`，`targetMode` 支持 `SINGLE/FIXED_SLOTS/ORDERED_GROUPS/ALL/RANDOM_N`。

### 61.3 点击获取与短效指令

```http
GET /api/npc/{npcId}/acquisition-guide
Authorization: Bearer <player-token>
```

可跳转示例：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "npcId": 1001,
    "status": "AVAILABLE",
    "action": "REDIRECT",
    "channelType": "CARD_DRAW",
    "sourceRefKey": "limited_202608",
    "routeKey": "CARD_DRAW",
    "routeParams": {
      "poolCode": "limited_202608",
      "highlightNpcId": 1001
    },
    "message": "前往抽卡获取",
    "contractVersion": 1,
    "configVersion": 7,
    "instructionExpiresAt": "2026-08-20T10:31:00"
  }
}
```

不可跳转时仍返回 HTTP 成功响应，但 `action=SHOW_TOAST`，Unity 只展示 `message`，不得继续使用之前缓存的路由参数：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "npcId": 1001,
    "status": "ENDED",
    "action": "SHOW_TOAST",
    "channelType": "CARD_DRAW",
    "sourceRefKey": "limited_202608",
    "routeKey": null,
    "routeParams": null,
    "message": "卡池活动已结束",
    "contractVersion": 1,
    "configVersion": 7,
    "instructionExpiresAt": "2026-08-20T10:31:00"
  }
}
```

`instructionExpiresAt` 最长为响应生成后 60 秒；渠道若更早开始、结束或改变状态，则使用更早的变化时间。指令过期后必须重新请求。`REDIRECT` 只表示导航到获取入口，绝不表示 NPC 已发放；抽卡、任务、签到、挂机和邮件仍由各自业务接口重新校验并结算，Unity 不得因收到跳转指令而在本地增加 NPC。

`OWNED` 或无渠道时，获取指令中的渠道字段为 `null`；任何非 `AVAILABLE` 状态都必须返回 `routeKey=null`、`routeParams=null`。Unity 不能沿用上一次成功指令中的路由参数。

### 61.4 Unity 路由白名单

`routeKey` 是稳定业务路由，不是 Unity Scene 名称，禁止直接执行 `SceneManager.LoadScene(routeKey)`。Unity 需要维护显式白名单：

| `routeKey` | 必需 `routeParams` | Unity 行为 |
| --- | --- | --- |
| `CARD_DRAW` | `poolCode`, `highlightNpcId` | 打开抽卡页并定位 NPC |
| `PLAYER_TASK` | `taskCode` | 打开任务总览并定位任务 |
| `SIGN_IN` | `planCode` | 打开签到总览并定位计划 |
| `IDLE_ADVENTURE` | `modeCode`, `stage`, `poolKey` | 打开挂机冒险并定位模式、关卡和掉落池 |
| `MAIL` | `mailId` | 打开该玩家真实存在的邮件；管理端不能静态配置 MAIL |

推荐处理顺序：

```text
1. contractVersion 不受支持：提示客户端升级。
2. instructionExpiresAt 已过期：重新请求 acquisition-guide。
3. action=SHOW_TOAST：只显示 message。
4. action=REDIRECT 且 routeKey 在本地白名单：解析强类型参数并打开页面。
5. 未知 action/status/routeKey：安全降级为提示或刷新，不执行任意跳转。
```

### 61.5 实时刷新事件

连接地址为 `/api/ws/realtime`，握手只接受请求头 `Authorization: Bearer <player-token>`。首次获得 NPC 或重复 NPC 转补偿资产后，服务端只在奖励事务成功提交后推送：

```json
{
  "type": "PLAYER_NPC_ACQUISITION_RESOLVED",
  "requestId": null,
  "eventId": "b0e5961f-a61c-4bf1-ac71-9f00ea4bcf08",
  "playerId": 42,
  "data": {
    "acquisitionId": "CARD_DRAW:90001",
    "playerId": 42,
    "npcId": 1001,
    "playerNpcId": 55001,
    "changeType": "NEW_ACQUIRE",
    "sourceType": "CARD_DRAW",
    "sourceRefKey": "limited_202608:70001",
    "occurredTime": "2026-08-20T10:30:00"
  },
  "serverTime": "2026-08-20T10:30:00.120"
}
```

- `NEW_ACQUIRE`：`playerNpcId` 必为正整数，可播放一次新 NPC 动画，然后刷新图鉴。
- `DUPLICATE_CONVERT`：`playerNpcId` 必为 `null`，不得播放新 NPC 动画，只刷新图鉴和背包资产。
- `sourceType` 当前为 `CARD_DRAW / PLAYER_TASK / SIGN_IN / IDLE_ADVENTURE / MAIL_ATTACHMENT`；邮件奖励事件使用 `MAIL_ATTACHMENT`，与渠道路由 `MAIL` 不同。
- 信封 `eventId` 是传输 ID；Unity 必须按 `data.acquisitionId` 做业务去重。事件可能重复、延迟或丢失，登录、重连、恢复前台和打开图鉴时都必须重新查询 `/api/npc/catalog`。
- HTTP 图鉴是 NPC 所有权的唯一权威，WebSocket 只用于提醒刷新，不能作为发奖依据。

### 61.6 管理端配置

配置中心入口为 `NPC_ACQUISITION / CHANNELS`。通用配置接口：

```http
GET  /api/ops/config/NPC_ACQUISITION/CHANNELS
POST /api/ops/config/NPC_ACQUISITION/CHANNELS/validate
GET  /api/ops/config/NPC_ACQUISITION/CHANNELS/preview
PUT  /api/ops/config/NPC_ACQUISITION/CHANNELS/draft
POST /api/ops/config/NPC_ACQUISITION/CHANNELS/publish
Authorization: Bearer <operator-token>
```

为指定 NPC 查询可以引用的真实正式来源：

```http
GET /api/ops/config/npc-acquisition/sources?npcId=1001&channelType=CARD_DRAW
Authorization: Bearer <operator-token>
```

首版静态渠道为 `CARD_DRAW / PLAYER_TASK / SIGN_IN / IDLE_ADVENTURE`。管理端使用结构化来源下拉框和路由参数，不允许运营直接输入 Unity 场景名或任意 JSON。玩家真实存在、未领取且未过期的 NPC 邮件由运行时动态生成 `MAIL` 渠道，不写入正式配置。

先通过查询接口取得 `currentFingerprint`、`draftRevision` 和现有草稿。`validate` 与 `draft` 使用完整草稿请求；首次创建草稿时 `expectedRevision=null`，更新已有草稿时填写当前 `draftRevision`：

```json
{
  "expectedRevision": null,
  "baseFingerprint": "GET接口返回的currentFingerprint",
  "payload": {
    "channels": [
      {
        "channelCode": "legendary_blacksmith_card_draw",
        "npcId": 1001,
        "channelType": "CARD_DRAW",
        "sourceRefKey": "limited_202608",
        "priority": 100,
        "routeKey": "CARD_DRAW",
        "routeParams": {
          "poolCode": "limited_202608",
          "highlightNpcId": 1001
        },
        "startTime": "2026-08-20T00:00:00",
        "endTime": "2026-08-31T23:59:59",
        "minPlayerLevel": 1,
        "displayName": "限定卡池",
        "buttonText": "前往抽取",
        "enabled": true
      }
    ]
  },
  "reason": "配置传奇铁匠的限时卡池入口"
}
```

正式发布只发布已经保存的当前草稿，不直接接收渠道数组：

```json
{
  "expectedRevision": 3,
  "reason": "NPC 获取渠道验收通过，正式发布"
}
```

`expectedRevision` 必须等于当前 `draftRevision`。草稿保存和发布都会校验渠道编码、同一 NPC 的优先级、NPC 是否允许获取、来源对象是否存在且确实包含目标 NPC、时间窗口、等级和强类型路由参数。内层 `payload` 不接受数据库行 `id`、行版本或其他未知字段。正式发布按完整渠道集合替换并递增独立 `configVersion`；高优先级渠道结束后，运行时自动降级到下一个可用渠道。

### 61.7 Unity 必须修改的内容

1. 图鉴页调用 `GET /api/npc/catalog`，直接使用 `owned`、`acquisition`、`skills`、`configVersion` 和 `contractVersion`；技能详情不再逐 NPC 请求。
2. 获取按钮点击时调用 `GET /api/npc/{npcId}/acquisition-guide`，不再拿列表中的活动 ID 直接跳转。
3. 增加五个 `routeKey` 的本地白名单和强类型参数解析；未知路由必须安全降级。
4. 支持六种 `status`、两种 `action`、指令过期重拉和不支持协议版本提示。
5. 监听 `PLAYER_NPC_ACQUISITION_RESOLVED`，按 `acquisitionId` 去重，并区分首次获得与重复转资产。
6. 登录、重连、回到前台及重新进入图鉴时主动拉取 HTTP 最新状态，不能只依赖 WebSocket。

旧 Unity 暂时不修改也能继续使用 `/api/npc/config/list` 和 `/api/npc/list/{playerId}`；只是无法使用服务端获取按钮、自动渠道降级和实时刷新新协议。

### 61.8 Flyway 与手动启动

`V92__add_npc_acquisition_channels.sql` 空配置起步，只创建 NPC 获取渠道表和独立发布版本状态，不预置任何业务渠道。原始固定 CRC32 为 `1615105684`，执行成功后禁止修改，也不得使用 Flyway `repair` 掩盖漂移。

`V93__harden_npc_acquisition_channel_contract.sql` 不改写 V92，只以前向迁移将按钮文案收紧为 32 字符、移除渠道 `config_version` 默认值、限定优先级与发布版本范围，并补齐文本非空白、渠道类型/路由一致和启用值 CHECK。迁移保存当前会话 `sql_mode`，在列变窄期间临时强制 `STRICT_ALL_TABLES`，因此不合规历史数据会阻断迁移而不会被静默截断；成功后恢复原会话模式。V93 固定 CRC32 为 `-2034435388`；这是 NPC 渠道章节的历史边界，当前仓库最高迁移为 V137，下一次数据库变化必须新增 V138。

用户手动启动后端前保持：

```text
FLYWAY_ENABLED=true
FLYWAY_BASELINE_ON_MIGRATE=false
```

已有 `flyway_schema_history` 的数据库应先停止旧版本写入并备份，升级 V102 前确认 `avatar_resource_manifest_import` 和 `avatar_resource_manifest_item` 均为空，再直接启动当前版本，由 Flyway 先验证历史脚本并按顺序执行到仓库当前最高版本 V104；不要手工执行 V92-V104。任一清单表非空时 V102 会安全阻断并保留原数据，必须停止发布，不得执行 `repair`。迁移完成后确认版本 `92` 的 `success=1`、脚本名为 `V92__add_npc_acquisition_channels.sql`、校验和为 `1615105684`，并确认版本 `93` 的 `success=1`、脚本名为 `V93__harden_npc_acquisition_channel_contract.sql`、校验和为 `-2034435388`。V92 初次迁移后渠道表为空、发布版本为 `0` 属于正常状态，运营发布第一份正式配置后玩家图鉴才会出现静态获取入口。已执行 V92 的旧库无需 `repair`，满足 V102 空表前置条件时正常启动会自动继续执行 V93-V104；V103 固定 CRC32 为 `-172308135`，执行后 `AVATAR_001` 为 `STAGED`，必须重新验收 Unity 清单并显式发布。V94-V104 的具体校验值和客服、固定头像结构核对见后续章节。

完整字段与异常边界见仓库 `docs/npc-acquisition-api.md`；数据库核对 SQL 见 `docs/database-script-guide.md`。

## 62. 微信小游戏官方客服

### 62.1 最终效果

该功能打开的是微信小游戏官方客服会话，不是游戏公屏轮播、公告或 Unity 自建聊天窗口。玩家文字、图片和小游戏卡片由微信回调到后端形成工单；运营人员在“玩家运维 -> 客服工单”中认领、分类、流转和回复，回复通过微信官方 `sendcustommessage` 异步发送。

### 62.2 Unity 玩家入口

```http
GET /api/customer-service/entry?scene=PLAYER_SUPPORT&page=pages/index/index
Authorization: Bearer <player-token>
```

响应：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "enabled": true,
    "sessionFrom": "v1.eyJ...signature",
    "sessionFromExpiresAt": "2026-08-24T04:15:00Z",
    "showMessageCard": true,
    "sendMessageTitle": "联系客服",
    "sendMessagePath": "pages/index/index",
    "sendMessageImg": null,
    "minimumBaseLibraryVersion": "2.0.3"
  }
}
```

页面加载时预取该接口；距 `sessionFromExpiresAt` 不足 30 秒时重新获取。只允许在玩家实际 touch/click 回调中调用：

```javascript
wx.openCustomerServiceConversation({
  sessionFrom: entry.sessionFrom,
  showMessageCard: entry.showMessageCard,
  sendMessageTitle: entry.sendMessageTitle,
  sendMessagePath: entry.sendMessagePath,
  sendMessageImg: entry.sendMessageImg,
  success() {},
  fail(error) {
    // 显示“客服会话打开失败，请稍后重试”。
  }
})
```

`enabled=false` 时 Unity 隐藏入口或提示暂未开放，不调用微信 API。该接口要求玩家登录，但豁免游戏时长拦截；未绑定微信账号返回 409。旧 Unity 不调用该接口时，其他功能不受影响，但不会显示新客服入口。

### 62.3 微信回调

继续复用：

```http
GET  /api/wechat/message-push
POST /api/wechat/message-push
```

支持 AES 安全模式下的 JSON/XML；生产必须使用安全模式。GET 验证和所有 POST 验签都会严格校验纯数字 `timestamp` 处于服务端时间前后 5 分钟内。POST 明文因官方 signature 不覆盖正文，dev/prod 均默认拒绝；仅本地联调可短时显式设置 `WECHAT_MESSAGE_PUSH_ALLOW_PLAINTEXT=true`。客服类型为 `text`、`image`、`miniprogrampage` 和 `event/user_enter_tempsession`；礼包事件继续走原发货逻辑。入站消息按 `MsgId` 或事件摘要去重，OpenID 不返回管理端、不写日志。

官方限制：玩家发消息后 48 小时最多回复 5 条；仅进入会话时 1 分钟最多回复 2 条；已有有效 48 小时窗口时不叠加进入会话额度。`sessionFrom` 最长 1000 UTF-8 字节，客户端基础库最低 `2.0.3`。

### 62.4 管理端接口

```http
GET  /api/ops/customer-service/tickets
GET  /api/ops/customer-service/tickets/{ticketId}
POST /api/ops/customer-service/tickets/{ticketId}/read
POST /api/ops/customer-service/tickets/{ticketId}/claim
PUT  /api/ops/customer-service/tickets/{ticketId}/classification
PUT  /api/ops/customer-service/tickets/{ticketId}/status
POST /api/ops/customer-service/tickets/{ticketId}/reply
Authorization: Bearer <operator-token>
```

查看沿用玩家读取权限；认领、分类、流转和回复允许 `REFUND_ADMIN / CONFIG_ADMIN / COMPLIANCE_ADMIN`。写操作携带详情中的 `expectedVersion`，409 后刷新详情。分类、流转和回复只允许 `assignedOperatorId` 等于当前操作员；`CONFIG_ADMIN` 也必须先通过认领接口填写原因并显式接管，接管审计成功后才能处置，不能静默覆盖原负责人。

列表和详情共用 `replyAllowed`、`replyUnavailableReason` 契约：未分配负责人返回 `CLAIM_REQUIRED` 并显示“需先认领”；`CONFIG_ADMIN` 查看其他客服负责的工单时返回 `TAKEOVER_REQUIRED` 并显示“需先接管”；普通客服查看他人负责的工单返回 `ASSIGNED_TO_OTHER`。以上状态下 `replyAllowed=false`，未先完成认领或接管时调用分类、流转或回复接口均返回 409。回复原因按权限、全局开关、工单关闭、负责人、官方窗口、剩余额度依次判定，全局关闭时统一返回 `FEATURE_DISABLED`。

状态流转：

```text
PENDING        -> PROCESSING / RESOLVED / CLOSED
PROCESSING     -> WAITING_PLAYER / RESOLVED / CLOSED
WAITING_PLAYER -> PROCESSING / RESOLVED / CLOSED
RESOLVED       -> PROCESSING / CLOSED
CLOSED         -> 无
```

回复请求：

```http
POST /api/ops/customer-service/tickets/1001/reply
Idempotency-Key: 95000000-0000-4000-8000-000000000001

{
  "content": "您好，请提供微信支付订单号，我们会尽快核查。",
  "expectedVersion": 9
}
```

网络结果不明确时必须复用相同 UUID 和正文；后端直接重放第一次结果，不重复扣额度或发送。正文默认最多 1000 个 Unicode 字符并执行本地内容安全检查。

### 62.5 Outbox 与运维

回复事务只预留额度并写 `PENDING` Outbox，微信网络调用在事务外执行。临时失败按 `2/5/15/30/60/120/300` 秒退避；最终状态为 `SENT/FAILED/EXPIRED`。发送完成按消息顺序和工单终态加栅栏，旧任务不会覆盖新玩家消息或已解决/已关闭状态。`FAILED/EXPIRED` 会以“微信客服回复”出现在异常任务中心。

指标：

```text
game.wechat.customer.service.outbox.events{outcome=sent|retry_scheduled|failed|expired}
game.wechat.customer.service.outbox.pending
```

### 62.6 配置与 V95/V96

默认关闭，至少配置：

```text
WECHAT_MESSAGE_PUSH_ENABLED=true
WECHAT_MESSAGE_PUSH_TOKEN=<Token>
WECHAT_MESSAGE_PUSH_ENCODING_AES_KEY=<EncodingAESKey>
WECHAT_MESSAGE_PUSH_ALLOW_PLAINTEXT=false
WECHAT_MINI_GAME_APP_ID=<AppID>
WECHAT_MINI_GAME_APP_SECRET=<AppSecret>
WECHAT_CUSTOMER_SERVICE_ENABLED=true
```

V95 新增 `wechat_customer_service_contact`、`wechat_customer_service_ticket`、`wechat_customer_service_message`、`wechat_customer_service_send_outbox`，原始固定 CRC32 为 `-1236124090`。V96 不修改接口契约，只以前向迁移将 `wechat_customer_service_ticket.last_player_message_time` 放宽为可空，并将 `claimed_by` 的字符集排序规则改为 `utf8mb4_bin`；V96 固定 CRC32 为 `292469054`。V97 新增 `users.avatar_code` 及查询索引，固定 CRC32 为 `151659432`；V98 重新建立头像来源与 `PRESET` 编号一致性约束，固定 CRC32 为 `-2063702854`；V99 归一历史远程头像并收紧为 `DEFAULT/PRESET`，固定 CRC32 为 `67421201`；V100 补齐注销清理原因并收紧系统头像编号，固定 CRC32 为 `-880935665`；V101 新增数据库驱动固定头像目录和 Unity 完整清单验收，原始固定 CRC32 为 `1155793846`，执行成功后已冻结，禁止修改或通过 `repair` 接受漂移。V102 不改写 V101，以固定 CRC32 `-707871511` 前向加固目录身份、资源验收和清单审计合同；升级时两张头像清单审计表必须均为空，任一表非空都会安全阻断并保留原数据，不得 `repair`。V103 不改写 V101/V102，以固定 CRC32 `-172308135` 取消 `AVATAR_001` 的无摘要历史兼容例外并要求重新验收；当前最高迁移为 V137，后续数据库变化必须新增 V138，不得修改已执行迁移。

保持 `FLYWAY_ENABLED=true`、`FLYWAY_BASELINE_ON_MIGRATE=false`，先停止旧版本写入、备份数据库并确认 `avatar_resource_manifest_import` 和 `avatar_resource_manifest_item` 均为空，用户手动启动后由 Flyway 自动升级到 V104；已经执行 V95/V96 的数据库只会继续执行后续版本。任一清单表非空时 V102 会在持久化结构变更前安全失败并保留原数据，必须停止发布并准备专门兼容迁移，无需且不得 `flyway repair`。不要手工执行迁移，也不要修改已执行的 V95～V104。客服开启且明文关闭时，启动会校验 EncodingAESKey 恰好 43 字符且 Base64 解码为 32 字节。多节点环境先启动一个新节点，确认 V95～V104 均为 `success=1`，并确认 V97、V98、V99、V100、V101、V102、V103、V104 校验和依次为 `151659432`、`-2063702854`、`67421201`、`-880935665`、`1155793846`、`-707871511`、`-172308135`、`-37724955`，再完成 AES 回调验收；确认服务器 NTP 正常、明文开关为 `false` 后，才滚动启动其他节点。

完整官方约束、字段、错误处理、环境变量和验收清单见仓库 `docs/wechat-customer-service-api.md`。
头像协议已统一收录在第 28 章“玩家系统资源头像（V39/V97/V98/V99/V100/V101/V102/V103/V104）”，本处不再重复维护旧版头像示例。

## 63. 单 JAR API / Worker 运行角色部署

### 63.1 改造后的效果

后端仍然只构建一个 JAR，不拆数据库、不拆仓库，也不增加服务之间的 HTTP 调用。正式环境把同一份、同一 SHA-256 的 JAR 启动为两个 Java 进程：

```text
Unity / 微信小游戏 / 管理端 / 微信回调
                    |
               HTTPS Nginx
                    |
             APP_ROLE=api
      HTTP、支付/微信回调、WebSocket
                    |
             MySQL + Redis
                    |
             APP_ROLE=worker
   异步消费、定时扫描、重试、对账和清理
```

Nginx 只代理 API 业务端口。Worker 的业务端口和 API/Worker 的 Actuator 管理端口只绑定 `127.0.0.1`，不得加入 Nginx 上游或开放到公网。

### 63.2 三种运行角色

| `APP_ROLE` | 使用场景 | 加载职责 |
| --- | --- | --- |
| `all` | 本地开发、旧单进程兼容 | 同时加载 API、WebSocket 和全部 Worker 调度器 |
| `api` | 正式在线进程 | 玩家/管理端 HTTP、支付和微信回调、WebSocket、本机会话续租 |
| `worker` | 正式后台进程 | 支付发货、客服 Outbox、广播 Outbox、生产结算、系统奖励、对账和清理 |

未配置 `APP_ROLE` 时默认 `all`，原有 IDE 或 `mvn spring-boot:run` 本地启动方式保持兼容。正式环境只运行 `api + worker`，不要再额外启动第三个 `all` 进程。

生产分角色进程有强制启动保护：

1. Worker 的 `FLYWAY_ENABLED` 必须为 `false`；只有指定 API 或独立迁移节点可以执行迁移。
2. 生产 API 与 Worker 的 `REALTIME_PUSH_MODE` 都必须为 `redis_pubsub`。
3. 生产 Worker 的 `SERVER_ADDRESS` 和 `MANAGEMENT_SERVER_ADDRESS` 必须明确为回环地址。
4. 生产 Worker 的 `MANAGEMENT_SERVER_PORT` 必须显式配置为 `1..65535` 的独立端口。
5. Worker 主端口由最高优先级 Filter 在认证和 CORS 前统一返回 404，WebSocket 和 Redis 实时订阅器只在 API 角色注册。
6. 上述角色、Flyway、实时模式和 Worker 网络边界会在数据源、迁移和端口初始化前完成校验。

### 63.3 客户端与外部接口兼容

本次改造不改变任何玩家或运营业务接口：

- Unity HTTPS 基础地址仍为 `https://你的域名/api`。
- Unity WebSocket 地址仍为 `wss://你的域名/api/ws/realtime`。
- 管理端仍通过原域名访问 `/api/ops/**`。
- 微信登录、微信支付回调、消息推送和客服回调路径不变。
- 请求参数、响应字段、业务错误码、Bearer Token 和运营权限均不变。

Unity 和管理端不需要知道请求由哪个 API 进程处理，也不能直接访问 Worker。已有客户端无需因本次部署改造重新发版。

### 63.4 正式端口与关键变量

| 配置 | API | Worker |
| --- | --- | --- |
| `APP_ROLE` | `api` | `worker` |
| `SERVER_ADDRESS` | `127.0.0.1` | `127.0.0.1` |
| `SERVER_PORT` | `8080` | `18081` |
| `MANAGEMENT_SERVER_ADDRESS` | `127.0.0.1` | `127.0.0.1` |
| `MANAGEMENT_SERVER_PORT` | `18080` | `18082` |
| `FLYWAY_ENABLED` | 首个迁移节点可为 `true` | 固定 `false` |
| `REALTIME_PUSH_MODE` | `redis_pubsub` | `redis_pubsub` |
| `DB_POOL_MAX_SIZE` | 模板为 `30` | 模板为 `10` |

`MANAGEMENT_SERVER_ADDRESS=127.0.0.1` 是监听网卡地址，不是 HTTPS 域名，不要改成证书域名。HTTPS 仍只在 Nginx 终止。

每个进程必须使用不同的 `APP_INSTANCE_ID` 和 `LOG_INSTANCE_ID`。API 与 Worker 必须连接同一 MySQL、Redis 和实时频道，并使用同一版本化 JAR；若部署历史头像清理或其他上传业务，相关 Worker 还需访问同一对象存储。固定头像正常链路不要求对象存储。不能让两个进程长期混跑不同代码版本。

### 63.5 健康检查与优雅停机

生产 readiness 同时检查：

```text
readinessState + runtimeRole + db + redis
```

本机检查地址：

```http
GET http://127.0.0.1:18080/actuator/health/readiness
GET http://127.0.0.1:18082/actuator/health/readiness
```

两者都必须返回 HTTP 200 且 `status=UP`。`runtimeRole` 记录角色、实例 ID 和排空状态；应用开始关闭后转为 `OUT_OF_SERVICE`。readiness 不通过 Nginx 对外暴露，也不能替代任务积压、微信平台和支付渠道监控。

应用已启用：

- Web 容器 graceful shutdown；
- Spring 调度器停止领取新一轮任务并等待当前轮结束；
- 每个生命周期阶段默认最多等待 60 秒；
- systemd 使用 `SIGTERM`；API、Worker 和 Windows 停止脚本均预留 180 秒外层排空预算。

正常停止顺序固定为：先 Worker、后 API。正常启动顺序固定为：先 API 完成 Flyway 和 readiness、后 Worker。Windows 脚本通过仅绑定本机的 Actuator shutdown 触发优雅关闭；任务管理器结束进程或 `Stop-Process -Force` 只用于明确接受租约恢复和重试的故障处置。

### 63.6 Flyway 与版本化 JAR

本节记录运行角色改造交付时的历史边界：当时没有新增 Flyway 文件且最高版本为 V100。当前仓库已新增并冻结 V101/V102/V103/V104：V101 原始固定 CRC32 为 `1155793846`，禁止修改或通过 `repair` 接受漂移；V102 不改写 V101，以固定 CRC32 `-707871511` 前向加固头像目录与清单合同；V103 以固定 CRC32 `-172308135` 取消默认头像无摘要历史例外并要求重新验收。后续数据库变化从 V105 开始。

正式发布必须：

1. 生成不可覆盖的新 JAR 文件名和 `.sha256` 校验文件。
2. API 与 Worker 同时指向该同一绝对 JAR 路径和校验文件。
3. 启动前再次执行 SHA-256 校验，校验失败禁止启动。
4. Worker 永远保持 `FLYWAY_ENABLED=false`。
5. 只让一个指定 API 或独立迁移节点执行 Flyway；确认迁移成功和 API readiness 后再启动 Worker。
6. 禁止覆盖 Windows 正在运行的 JAR，禁止修改已执行迁移或使用 `flyway repair` 掩盖漂移。

标准发布顺序：构建与测试 → 复制版本化 JAR 并校验 → 备份数据库 → 停 Worker → API 摘流并停止 → 切换两个角色的 JAR 路径 → 启动唯一 API 迁移节点 → 恢复 Nginx → 启动 Worker → 核对角色、实例 ID、JAR SHA 和任务积压。

### 63.7 异步消息和恢复边界

支付 Redis Stream 新消息携带 `contractVersion=1`，历史未携带版本的消息按 V1 读取。Worker 遇到无法识别的显式版本或事件类型时会记录错误并保留 pending，不会直接 ACK 丢弃；部署支持该契约的新 Worker 后再接管。

数据库状态机、事务、唯一键、幂等键、租约和重试共同降低重复副作用风险，但不能把 MySQL、Redis 和微信外部接口组合成一个跨系统原子事务。Worker 异常退出后，先确认旧进程已结束，再使用与 API 相同 SHA 的 JAR 恢复；长时间积压应逐步观察和消化，不要直接放大连接池与批次。

仓库完整部署说明：`docs/runtime-role-deployment.md`。systemd 模板位于 `deploy/systemd`，Windows 启停脚本位于 `deploy/windows`。

## 64. 挂机冒险编队运营查询与实时刷新

### 64.1 管理端查看任意玩家编队

```http
GET /api/ops/players/{playerId}/idle-adventure/formation
Authorization: Bearer <operator-token>
```

该接口供管理端“玩家中心 -> 挂机编队”页签查询指定玩家当前保存的编队，沿用玩家中心只读权限。接口严格只读：运营查看从未进入过挂机冒险的玩家时，不会创建玩家进度、不会初始化编队，也不会自动填充 NPC。

响应固定返回六个槽位，并包含：

- 玩家基础信息和挂机冒险进度；
- 玩家 NPC 实例 ID、NPC 配置 ID、名称、品质、等级和实例状态；
- 槽位位置、前后排信息和是否为空；
- 当前生命、攻击、防御、速度等总属性；
- `combatProfileAvailable`，用于标识 NPC 或当前等级战斗配置是否完整、启用且可用于战斗。

管理端只提供查询，不提供代替玩家换阵、绕过 NPC 归属校验或修改战斗属性的能力。玩家不存在时返回业务码 `2001`。

### 64.2 玩家编队变化 WebSocket 消息

玩家保存局部或完整编队、自动填充、主动清空、首次默认初始化，以及开战前触发默认初始化成功后，服务端向该玩家本人的全部在线连接发送：

```json
{
  "type": "IDLE_ADVENTURE_FORMATION_CHANGED",
  "requestId": null,
  "eventId": "33fddf13-598e-4f48-a1d9-0fb4ec011547",
  "playerId": 7,
  "data": {
    "player_id": 7,
    "deployed_count": 1,
    "slots": []
  },
  "serverTime": "2026-08-26T10:30:00.123456"
}
```

`data` 与玩家编队查询接口保持同一结构，实际始终返回完整六槽位。该事件是玩家本人状态变化通知，不是全服广播、公屏轮播或公告，也不会发送给其他玩家。

- 消息只在数据库事务提交成功后发送，事务回滚不发送；
- 推送失败只记录日志和指标，不回滚已经提交的编队；
- 多 API 实例正式部署时必须使用 `REALTIME_PUSH_MODE=redis_pubsub` 并保证 Redis 可用；
- 离线、断线和 WebSocket 丢包不会影响数据库中的权威编队。

### 64.3 Unity 正确处理方式

WebSocket 事件只能作为“需要刷新”的提示。Unity 收到事件、WebSocket 重连、应用回到前台或本地状态不确定时，应重新调用：

```http
GET /api/idle-adventure/players/{player_id}/formation
```

当前响应没有 `formationVersion`，因此 Unity 不能直接使用事件中的快照覆盖刚完成的 HTTP 保存结果，否则乱序消息可能让界面回退到旧阵容。建议合并短时间内重复的刷新信号，并以最后一次 HTTP 查询结果覆盖本地状态。

不接入新 WebSocket 事件时，原有 HTTP 编队功能仍可正常使用；接入后可改善同账号多设备和跨页面的刷新及时性。管理端新增的运营查询接口不需要 Unity 调用。

### 64.4 并发保存与失效 NPC 防护

局部编队保存已经调整为在同一玩家锁事务内读取当前六槽位、合并本次局部修改并一次性持久化，避免两个并发请求各自基于旧快照保存后互相覆盖。完整保存、自动填充、清空和开战读取继续复用统一编队校验。

读取编队时会清理已经删除、停用或不再属于玩家的 NPC 实例所形成的“幽灵槽位”。开战前再次进行权威校验；编队为空或没有有效可参战 NPC 时返回业务码 `6502`，不会创建非法战斗会话。

### 64.5 相关接口契约补充

挂机领取接口：

```http
POST /api/idle-adventure/players/{player_id}/afk-reward/collect
```

新 Unity 必须优先遍历响应中的 `collected_rewards` 完整奖励列表。旧字段 `collected_item` 仅作为兼容字段保留，配置版本切换导致一次领取包含两种产物时，只读取旧字段会漏展示第二项奖励。

额外 Buff 接口：

```http
POST /api/idle-adventure/players/{player_id}/sessions/{session_key}/buffs/apply
```

在尚未具备服务端授权凭证、消费流水和防重放校验前，该旧路径统一返回 HTTP 403、业务码 `6515`，不会修改战斗会话。客户端幂等键不能替代玩家获得 Buff 的业务资格。

### 64.6 数据库与兼容性

本节记录挂机冒险改造交付时的历史边界：当时没有新增 Flyway 迁移且最高版本为 V100。当前仓库已新增并冻结 V101/V102/V103/V104：V101 原始固定 CRC32 为 `1155793846`，禁止修改或通过 `repair` 接受漂移；V102 不改写 V101，以固定 CRC32 `-707871511` 前向加固头像目录与清单合同；V103 以固定 CRC32 `-172308135` 取消默认头像无摘要历史例外并要求重新验收。后续数据库变化从 V105 开始。既有 Unity 挂机冒险 HTTP 路径、请求字段和主要响应结构保持不变；新增内容仅包括运营只读接口、本人 WebSocket 刷新事件，以及服务端并发与失效数据防护。

完整的 28 条玩家端接口、字段示例、错误码、体力、奖励和战斗规则见仓库 `docs/idle-adventure-api.md`，桌面专项文档 `IdleAdventure模式接口文档.md` 已同步同一版本。

### 64.7 关卡敌人属性与管理端预览

玩家端关卡敌人查询接口保持原路径：

```http
GET /api/idle-adventure/stages/{stage}/enemies
Authorization: Bearer <player-token>
```

响应继续返回固定六个槽位，并新增以下只读字段：

- `stage_name`：当前已发布关卡名称；
- `column`、`row`、`front_column`：槽位布局信息；
- `enemy.icon`、`enemy.model_key`：展示资源和模型资源标识；
- `enemy.level`：该关卡槽位最终生效等级；
- `enemy.max_health`、`attack`、`defense`、`speed`：该槽位最终生效属性。

空槽位仍返回 `enemy: null`。新增字段不改变 URL、鉴权、请求方式或既有字段，旧 Unity 可兼容；新 Unity 若需要展示这些信息，需在敌人响应模型中增加字段并在 UI 中使用 `stage_name`、等级、属性和资源标识。客户端不得根据等级自行推算属性。

管理端路径为“配置中心 -> 挂机冒险 -> 关卡配置”。展开关卡后，敌方阵容编辑表下方显示“阵容属性预览”，固定展示六槽位、前后排、等级、战斗属性、技能、图标、模型以及生命/攻击/防御合计和速度总和（仅用于配置核对）。该预览只读计算，不写回配置；正式发布仍由后端严格校验。

当前敌人配置没有品质字段，项目也没有统一战斗力公式，因此接口和管理端不返回推测的 `quality` 或 `combat_power`。本次没有新增 Flyway 迁移，也不需要修改数据库结构。

## 65. V101～V104 固定头像管理端登记与 Unity 资源验收

V101 把固定头像从 Java 静态清单迁移到数据库正式目录。V102 补齐清单指纹和数据库合同；V103 取消 `AVATAR_001` 的无摘要历史例外并将其降为 `STAGED`，等待重新验收；V104 将该未验收条目的最低客户端版本号提升为 1，并收紧目录正整数约束。图片仍只存在于 Unity 包内，后端和管理端不上传图片、不保存图片文件、不使用对象存储，也不恢复微信头像能力。

### 65.1 最终效果

- 管理员可在配置中心 `AVATAR_CATALOG / PRESETS` 新增头像登记，只填写显示名称、目标最低 Unity 数字版本和排序。
- 首次发布后端自动生成稳定编号和路径，例如 `AVATAR_002`、`Resources/Avatars/avatar_002`；管理员不能手工填写或修改。
- 新条目固定先进入 `STAGED`，玩家看不到也不能选择。
- Unity 按生成路径加入图片并生成目标包；Unity Editor 或可信 CI 自动上报完整资源清单，只完成路径/SHA-256 验收，不自动启用。管理端不提供手工 JSON 导入。
- 管理员重新创建草稿，把验收通过的条目改为 `ACTIVE` 并发布，满足版本门槛的玩家才会看到。
- `RETIRED` 禁止新选择但保留历史玩家展示；Unity 后续版本不得删除对应资源。
- `BLOCKED` 用于紧急屏蔽，玩家公开资料会回退默认头像；解除屏蔽必须重新完成同摘要清单验收。
- 已发布编号、路径和图片摘要永久不可原地替换；换图必须登记新编号，因此不会发生“一换图片所有玩家一起变化”。

生命周期：

| 状态 | 玩家目录 | 可新选择 | 历史玩家展示 |
| --- | --- | --- | --- |
| `STAGED` | 不返回 | 否 | 否 |
| `ACTIVE` | 版本满足时返回 | 是 | 是 |
| `RETIRED` | 版本满足时返回 | 否 | 是 |
| `BLOCKED` | 不返回 | 否 | 回退默认头像 |

### 65.2 玩家目录

```http
GET /api/user/avatar/presets
Authorization: Bearer <player-token>
X-Client-Version-Code: 10002
```

`X-Client-Version-Code` 使用与客户端运行策略相同的单调递增数字版本。旧 Unity 不带该请求头时按版本 `0` 处理；V104 后正式头像最低版本必须为正整数，因此旧客户端不会收到目录项，应使用包内默认头像。

```json
{
  "code": 200,
  "message": "操作成功",
  "data": [
    {
      "avatarCode": "AVATAR_001",
      "displayName": "默认头像一",
      "resourceKey": "Avatars/avatar_001",
      "enabled": true,
      "minimumClientVersionCode": 10002
    },
    {
      "avatarCode": "AVATAR_002",
      "displayName": "田园少年",
      "resourceKey": "Avatars/avatar_002",
      "enabled": true,
      "minimumClientVersionCode": 10002
    }
  ]
}
```

业务总开关关闭时仍返回当前版本可渲染目录，但全部 `enabled=false`。`resourceKey` 是从正式 `unityResourcePath` 派生的 Resources 相对键，不含 `Resources/` 前缀、扩展名或前导斜杠；新 Unity 直接传给 `Resources.Load<Sprite>(resourceKey)`。这是向后兼容的新增字段，旧 Unity 忽略它后可继续使用 `avatarCode → Sprite` 映射。Unity 只展示 `enabled=true` 的可选择项；未知编号、本地 Sprite 缺失或业务失败时必须回退包内默认头像。`resourceKey` 不是下载地址，图片必须位于 `Assets/**/Resources/Avatars/` 并随对应 Unity 包发布。

### 65.3 选择头像

```http
PUT /api/user/avatar/preset
Authorization: Bearer <player-token>
X-Client-Version-Code: 10002
Content-Type: application/json

{"avatarCode":"AVATAR_002"}
```

服务端先检查本地只读目录，再在玩家事务中按“锁玩家 → 锁正式头像行”重新校验 `ACTIVE`、资源验收状态和最低客户端版本，避免紧急屏蔽后的缓存窗口继续写入。相同编号重复选择幂等。

```json
{
  "code": 200,
  "message": "头像设置成功",
  "data": {
    "avatarUrl": null,
    "avatarSource": "PRESET",
    "avatarCode": "AVATAR_002",
    "avatarVersion": 3,
    "avatarUpdatedTime": "2026-08-28 18:00:00"
  }
}
```

错误处理：

| HTTP | 业务码 | Unity 处理 |
| ---: | ---: | --- |
| 400 | `400` | 编号未知或不可选，刷新目录并保留当前头像 |
| 426 | `7310` | 当前客户端版本不支持目标头像，进入统一更新提示 |
| 429 | `7306` | 当日切换次数已达上限 |
| 409 | `7307` | 头像状态并发变化，刷新本人资料 |
| 503 | `7308` | 头像业务关闭，隐藏选择入口 |

恢复默认接口保持不变：

```http
DELETE /api/user/avatar
Authorization: Bearer <player-token>
```

### 65.4 管理端目录配置

管理端进入“配置中心 → 社交与玩家 → 固定头像目录”。只有 `CONFIG_ADMIN` 可以保存、发布或丢弃草稿；历史版本仅供查看，不支持整份回滚。需要恢复某个历史状态时，必须在当前目录上逐项修改，重新完成 Unity 清单验收后再发布。其他配置角色只读。页面不提供 JSON 文本框、文件上传或“导入 Unity 清单”按钮，只读展示 Editor/CI 自动验收状态、版本、摘要和审计结果。

目录条目主要字段：

| 字段 | 来源 |
| --- | --- |
| `displayName`、`sortOrder` | 运营填写；已发布后仍可按规则调整 |
| `minimumClientVersionCode` | 登记时填写；仅 `STAGED` 且未验收时可修正，Editor/CI 验收后冻结 |
| `status` | 已发布条目按生命周期调整 |
| `avatarId`、`avatarCode`、`unityResourcePath` | 首次发布由服务端生成 |
| `resourceSha256`、`manifestVersion`、`resourceVerified` | Unity Editor/可信 CI 自动清单验收写入；运营不能手工勾选、修改或粘贴 JSON |
| `configVersion`、`catalogVersion` | 服务端并发控制和多节点刷新版本 |

新增完整流程：

1. 新增登记，填写名称、目标版本、唯一排序，状态保持“待客户端发布”。
2. 保存草稿、校验、预览、发布，重新读取生成的编号和路径。
3. Unity 按路径把图片加入 `Assets/**/Resources/Avatars/`，生成目标包和完整清单；不得覆盖已发布编号的图片。
4. Unity Editor 或可信 CI 使用受控的 `CONFIG_ADMIN` 构建凭证自动调用清单验收接口。有配置草稿时接口会拒绝验收；不得通过人工粘贴 JSON 绕过。
5. 自动验收成功后，管理员在页面确认验收结果、重新读取目录，另建草稿将目标改为“已启用”，再预览发布。只有图片已随对应 Unity 包发布后才能启用。

`AVATAR_POLICY / BUSINESS` 继续只管理头像总开关和每日切换次数，不再承载头像目录。

### 65.5 Unity Editor/CI 自动清单验收

构建工具先读取正式目录，禁止根据本地约定猜测编号或使用管理端草稿：

```http
GET /api/ops/config/AVATAR_CATALOG/PRESETS
Authorization: Bearer <CONFIG_ADMIN-editor-or-ci-token>
```

使用响应中的 `currentPayload.entries` 生成清单。目标客户端版本必须包含所有
`minimumClientVersionCode <= clientVersionCode` 的 `ACTIVE/RETIRED` 头像，以及本次已经
放入包内、准备验收的 `STAGED` 头像；排除 `BLOCKED` 和未进入当前包的 `STAGED` 头像。
如果响应中存在 `draftPayload`，构建工具必须停止验收并提示管理员先发布或丢弃草稿。

```http
POST /api/ops/config/avatar-presets/manifests/report
Authorization: Bearer <CONFIG_ADMIN-editor-or-ci-token>
Content-Type: application/json
```

该 JSON 是 Unity Editor 或可信 CI 生成并直接提交的机器合同，不是管理端表单。管理员不需要也不应手工编辑、粘贴或上传该 JSON。自动化凭证必须受控、可轮换并只供 Editor/CI 使用，禁止放入 Unity 运行时代码、游戏包、玩家设备或公开仓库。

`POST /api/ops/config/avatar-presets/manifests/report` 是 Editor/CI 新工具的推荐地址。旧工具使用的 `POST /api/ops/config/avatar-presets/manifests/import` 仅作兼容别名保留，请求体、权限、幂等、冲突校验和审计语义与 `/report` 完全一致；管理端不会调用两个地址中的任何一个。

```json
{
  "schemaVersion": 1,
  "platform": "WECHAT_MINIGAME",
  "channelCode": "DEFAULT",
  "clientVersionCode": 10002,
  "clientVersionName": "1.0.2",
  "manifestVersion": "wechat-default-10002",
  "manifestSha256": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
  "items": [
    {
      "avatarCode": "AVATAR_001",
      "unityResourcePath": "Resources/Avatars/avatar_001",
      "resourceSha256": "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"
    },
    {
      "avatarCode": "AVATAR_002",
      "unityResourcePath": "Resources/Avatars/avatar_002",
      "resourceSha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
    }
  ]
}
```

每个 `resourceSha256` 是 Unity 项目中对应头像源图片文件原始字节的 SHA-256。建议 Editor/CI
按 `avatarCode` 排序，把客户端版本和每项 `avatarCode/unityResourcePath/resourceSha256`
写入独立构建证据文件，再把该文件的 SHA-256 填入 `manifestSha256` 并归档；不要对包含
`manifestSha256` 自身的请求 JSON 做循环自哈希。

成功响应：

```json
{
  "code": 200,
  "message": "Unity头像资源清单自动验收成功",
  "data": {
    "catalogVersion": 6,
    "manifestVersion": "wechat-default-10002",
    "matchedCount": 2,
    "verifiedCount": 1,
    "skippedLegacyCount": 0,
    "replayed": false,
    "entries": [
      {
        "avatarId": 2,
        "avatarCode": "AVATAR_002",
        "displayName": "田园少年",
        "unityResourcePath": "Resources/Avatars/avatar_002",
        "resourceSha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "minimumClientVersionCode": 10002,
        "manifestVersion": "wechat-default-10002",
        "resourceVerified": true,
        "legacyCompatible": false,
        "status": "STAGED",
        "sortOrder": 20,
        "configVersion": 6
      }
    ]
  }
}
```

Editor/CI 必须同时检查 HTTP 状态和外层 `code`。网络超时或 HTTP 5xx 可使用完全相同的
清单身份和请求体重试，成功重放返回 `replayed=true`。HTTP 400 表示字段、路径或摘要格式
错误，HTTP 401/403 表示构建凭证不可用，HTTP 409 表示目录草稿、版本或资源身份冲突；
这些错误不能无限重试，应重新读取正式目录并提示开发人员处理。

清单是目标客户端构建的完整快照，必须包含该版本应继续渲染的全部 `ACTIVE/RETIRED` 头像和本次准备验收的 `STAGED` 头像。遗漏历史可渲染资源、路径不一致或已验证图片摘要变化都会整单拒绝。V103 后 `AVATAR_001` 也必须提供真实 SHA-256 和正整数最低客户端版本；不再接受无摘要历史例外。清单严格校验编号、路径、SHA-256 三元组，且不得包含 `BLOCKED` 头像。

首版只接受 `WECHAT_MINIGAME / DEFAULT`，同一客户端数字版本号只能对应一份清单。同一 `(platform, channelCode, manifestVersion)`、同内容重放幂等，不推进目录版本；同身份异内容返回 HTTP 409。自动验收只绑定 `STAGED` 资源证据，不会自动改为 `ACTIVE`，并同时写入头像清单审计和配置中心统一审计。

`manifestSha256` 是 Unity Editor 或可信 CI 输出的源资源清单/构建证据文件摘要，不是对包含 `manifestSha256` 字段的这段 JSON 再做自哈希；运营人员不得手工编造。服务端另行保存规范化请求指纹，用于严格判断幂等重放。

目录事务提交并完成本机缓存刷新后，在线客户端会收到只含版本提示的实时消息：

```json
{
  "type": "AVATAR_CATALOG_CHANGED",
  "eventId": "avatar-catalog:7",
  "data": { "catalogVersion": 7 }
}
```

Unity 收到后重新调用 `GET /api/user/avatar/presets`，不能把 WebSocket 消息当作目录正文。消息丢失不影响最终正确性；登录、重连、回到前台和进入头像页时仍应主动通过 HTTP 查询权威目录。

### 65.6 Unity 必须修改的内容

首次只使用 `AVATAR_001` 的旧客户端仍可运行，但 V104 后未携带版本头的客户端只使用包内默认头像；真正新增或重新启用头像前，Unity 必须完成：

1. 所有头像目录和选择请求携带 `X-Client-Version-Code`。
2. 新 Unity 在玩家目录 DTO 中增加 `resourceKey`，并直接调用 `Resources.Load<Sprite>(resourceKey)`；禁止根据编号自行拼路径。旧 Unity 可继续使用大小写敏感的 `avatarCode → Sprite` 显式字典。
3. 把新图片放到后端生成的精确 Resources 路径，不得覆盖旧编号文件。
4. 未知编号、缺少 Sprite、`BLOCKED` 回退或接口失败时统一显示包内默认头像。
5. Unity Editor/CI 导出包含平台、渠道、客户端版本、清单摘要和每项资源摘要的完整 JSON 清单，并使用受控构建凭证自动上报；该凭证不得进入玩家包。
6. 已发布资源只追加、不删除、不覆盖；`RETIRED` 资源仍需保留，以便历史玩家展示。

Unity 目录 DTO 和本地加载建议直接按以下方式实现，加载失败时不得留下空头像：

```csharp
using System;
using UnityEngine;

[Serializable]
public sealed class PresetAvatarDto
{
    public string avatarCode;
    public string displayName;
    public string resourceKey;
    public bool enabled;
    public long minimumClientVersionCode;
}

public static Sprite LoadPresetOrDefault(PresetAvatarDto item, Sprite defaultSprite)
{
    if (item == null || string.IsNullOrWhiteSpace(item.resourceKey))
    {
        return defaultSprite;
    }

    Sprite loaded = Resources.Load<Sprite>(item.resourceKey);
    return loaded != null ? loaded : defaultSprite;
}
```

Unity 运行时不需要实现管理端草稿、发布、清单验收或数据库版本刷新；清单上报只存在于 Editor 工具或 CI 流水线，不得进入玩家运行时代码。图片必须随 Unity 包发布，对象存储仍不需要配置。

### 65.7 数据库与部署

V101 原始迁移新增：

- `avatar_preset_catalog_state`：目录聚合版本；
- `avatar_preset_catalog`：稳定资源合同和生命周期；
- `avatar_resource_manifest_import`：每次 Unity Editor/CI 自动清单验收的摘要与调用身份审计；
- `avatar_resource_manifest_item`：逐项验证证据；
- `config_asset_id_sequence.AVATAR`：服务端编号序列，至少从 2 开始，并自动跳过历史玩家已经使用的纯数字头像后缀，避免未来复用旧编号。

V101 原始固定 CRC32 为 `1155793846`，执行成功后已经冻结，禁止修改脚本或通过 `flyway repair` 接受漂移。V102 不改写 V101，以固定 CRC32 `-707871511` 前向补齐目录身份、资源验收、清单结构版本、规范化请求指纹、唯一索引和头像序列保护。V103 不改写前述迁移，以固定 CRC32 `-172308135` 移除 `AVATAR_001` 无摘要历史例外：保留编号和路径，清除旧验收字段并无条件降为 `STAGED`，必须重新验收真实 Unity 资源后才能启用。V104 不改写 V103，以固定 CRC32 `-37724955` 将该未验收条目的最低客户端版本号提升为 1，并收紧目录最低客户端版本正整数约束；V103/V104 执行成功后均禁止修改，下一次数据库变化从 V105 开始。

部署时保持 `FLYWAY_ENABLED=true`，停止旧版本 API/Worker 写流量并备份数据库。若数据库最后成功版本低于 V99，先按仓库 `docs/database-script-guide.md` 执行 `ACCOUNT_CLOSURE` 头像清理记录只读预检；计数大于 0 时必须停止启动并完成受控桥接，不能修改历史迁移或执行 `repair`。执行 V102 前必须确认 `avatar_resource_manifest_import` 和 `avatar_resource_manifest_item` 均为空；原始 V101 无法为既有清单记录真实反推规范化请求指纹，因此任一表非空时 V102 会在持久化结构变更前安全失败并保留全部原数据。此时必须停止发布并准备专门兼容迁移，不得删除记录、修改 `flyway_schema_history` 或执行 `repair` 绕过。V103 仅在 V102 成功后执行，并将 `AVATAR_001` 安全降为 `STAGED`，不能跳过重新验收。

全部预检通过后只启动一个新版本节点，让 Flyway 从当前版本迁移至 V104；确认 `version=101`、`success=1`、`checksum=1155793846`，`version=102`、`success=1`、`checksum=-707871511`，`version=103`、`success=1`、`checksum=-172308135`，以及 `version=104`、`success=1`、`checksum=-37724955`，第二次 migrate 为 0 后，再启动其他 API/Worker 节点。V1～V104 执行成功后禁止修改，下一次数据库变化使用 V105，禁止用 `flyway repair` 掩盖校验和漂移。迁移完成后必须重新执行 Unity 清单验收，再由管理员将 `AVATAR_001` 从 `STAGED` 显式发布为 `ACTIVE`。

目录发布或清单验收提交后，本节点使用事务提交事件刷新，其他节点通过配置刷新 Outbox 和 Redis 通知；每个节点另有 30 秒数据库版本轮询。启动首次加载失败会阻止应用启动，后续刷新失败保留最近有效快照。

## 挂机冒险 V105 战斗背景资源

管理端在“配置中心 -> 挂机冒险 -> 战斗背景”登记背景编码和中文名称，系统默认生成 `IdleAdventure/Backgrounds/<编码小写>` 资源 Key，也支持历史或特殊路径手动调整并点击“恢复默认”；最终 Key 必须与已打入 Unity 包的资源完全一致，再在关卡中选择背景。若整个待发布版本没有背景资源，且所有关卡都未选择背景，可以沿用 Unity 包内默认背景；只要登记背景资源，就必须为每关选择已启用背景，不能混用空关卡背景。关卡敌人预览和所有会话响应新增 `stage_presentation`：`background_code`、`background_name`、`background_resource_key`。Unity 在开始、继续、重试和恢复时按该 Key 加载背景；字段为空、缺失或加载失败时回退包内默认背景。当前会话背景已写入服务端快照，配置发布不会改变进行中的战斗；继续下一关使用下一关配置。新增未打包图片仍需随 Unity 新版本发布。


# 经济流水来源分析接口

## 说明

运营端可通过来源明细接口区分资源来自系统奖励、任务、签到、广告、建筑生产、
挂机冒险、支付发货、商城交易、管理端补偿等业务。该接口只供管理端使用，Unity
游戏接口无需修改，也不会调用此接口。

## 接口

`GET /api/ops/analytics/economy/source-breakdown`

请求头使用现有运营账号认证方式。查询参数均可省略：

| 参数 | 类型 | 说明 |
| --- | --- | --- |
| `fromDate` | `yyyy-MM-dd` | 起始日期，默认最近 30 天 |
| `toDate` | `yyyy-MM-dd` | 结束日期，默认数据库业务当天 |
| `itemKey` | 字符串 | 物品标识，可选 |
| `sourceType` | 字符串 | 来源编码，可选 |

日期范围最多 366 天，单次最多返回 5000 条汇总记录。来源编码包括：

| 编码 | 中文名称 |
| --- | --- |
| `SYSTEM_REWARD` | 系统奖励 |
| `TASK_REWARD` | 任务奖励 |
| `SIGN_IN_REWARD` | 签到奖励 |
| `AD_REWARD` | 激励广告 |
| `CARD_DRAW_REWARD` | 抽卡奖励 |
| `BUILDING_PRODUCTION` | 建筑生产 |
| `IDLE_ADVENTURE` | 挂机冒险 |
| `PAYMENT_DELIVERY` | 支付发货 |
| `FRIEND_GIFT` | 好友赠礼 |
| `WECHAT_GIFT` | 微信赠礼 |
| `OPERATOR_COMPENSATION` | 管理端补偿 |
| `SHOP_PURCHASE` | 商城交易 |
| `RESOURCE_SALE` | 资源出售 |
| `PRODUCTION_FUEL` | 生产加速消耗 |
| `REFUND_RECOVERY` | 退款追回 |
| `GAMEPLAY_CONSUMPTION` | 玩法消耗 |
| `LAND_SLOT_PURCHASE` | 地块格子购买 |
| `LEGACY_UNCLASSIFIED` | 历史未分类 |
| `UNKNOWN` | 未识别来源 |

## 返回示例

```json
{
  "code": 0,
  "data": {
    "generatedTime": "2026-09-03T10:00:00",
    "fromDate": "2026-08-05",
    "toDate": "2026-09-03",
    "itemKey": null,
    "sourceType": null,
    "breakdown": [
      {
        "sourceType": "BUILDING_PRODUCTION",
        "itemId": 1001,
        "itemKey": "wood",
        "itemName": "木材",
        "producedAmount": 12000,
        "consumedAmount": 300,
        "netAmount": 11700,
        "changeCount": 420,
        "lastChangeTime": "2026-09-03T09:58:00"
      }
    ]
  }
}
```

## 数据与兼容性

`V106__add_economy_change_sources.sql` 为流水表增加 `source_type`、
`source_reference` 和来源索引。迁移不会删除历史流水；迁移前已存在且无法追溯
来源的记录标记为 `LEGACY_UNCLASSIFIED`。原 `/api/ops/analytics/economy` 接口、
所有 Unity 接口和现有产出/消耗总量字段保持兼容。

## 统计口径

- `producedAmount` 只汇总该来源下大于零的余额变化。
- `consumedAmount` 只汇总该来源下小于零变化的绝对值。
- 建筑正常收获标记为 `BUILDING_PRODUCTION`；邮件、运营奖励活动等后台发放标记为 `SYSTEM_REWARD`，两者不会混算。
- 金币商城和钻石商城的扣款及物品到账均标记为 `SHOP_PURCHASE`；资源出售的卖出扣除和目标资源到账均标记为 `RESOURCE_SALE`。
- `sourceReference` 仅用于服务端审计追踪，首版汇总接口不向管理端返回单笔业务引用，防止运营列表暴露过多订单标识。

## 上线与回滚

1. 先备份数据库，再使用 Flyway 正常执行 `V106__add_economy_change_sources.sql`。
2. 部署后端和管理端后，在“运营分析 → 经济分析 → 来源明细”验证新流水来源。
3. 不要手工修改 `flyway_schema_history`。如部署失败，优先回滚应用版本；数据库新增列对旧版本读取无影响，保留数据后再排查。
4. 历史记录保留为“历史未分类”，不进行猜测性回填。

# 单玩家资源来源明细（运营端）

## 接口

GET /api/ops/analytics/economy/player-source-details

用于玩家中心查看指定玩家的资源来源汇总及逐笔资源余额变化。该接口仅供管理端调用，Unity 游戏接口不需要修改。

请求参数：

- playerId：必填，玩家 ID，正整数。
- fromDate、toDate：可选，格式 yyyy-MM-dd，默认最近 30 天至数据库业务当天，最多查询 366 天。
- itemKey：可选，物品标识，例如 wood、gold。
- sourceType：可选，来源编码，例如 BUILDING_PRODUCTION、TASK_REWARD、OPERATOR_COMPENSATION。
- beforeId：可选，上一页最后一条流水 ID，使用 ID 游标分页。
- limit：可选，默认 50，最大 200。

返回 data 字段：

- playerId、playerName：玩家标识和展示名称。
- breakdown：按来源和物品汇总的 producedAmount（产出）、consumedAmount（消耗）、netAmount（净变化）、changeCount（变更次数）。
- changes：逐笔流水，包含流水 ID、物品、来源、来源引用、数量变化、变化后余额和变更时间。
- hasMore、nextBeforeId：分页标记；hasMore=true 时，将 nextBeforeId 作为下一次请求的 beforeId。

来源编码包括：系统奖励、任务奖励、签到奖励、激励广告、抽卡奖励、建筑生产、挂机冒险、支付发货、好友赠礼、微信赠礼、管理端补偿、商城交易、资源出售、生产加速消耗、退款追回、玩法消耗、历史未分类和未识别来源。

数据说明：V106 迁移前无法可靠还原来源的历史流水显示为“历史未分类”。V107 仅新增玩家、物品、时间联合索引，不删除或修改历史数据。管理端查询使用 MySQL 权威流水和 ID 游标分页，Unity 端无需改动。

# 建筑供给按数量计费与持续生产（V108/V109，2026-09-08）

本节更新建筑供给的计算规则；前文若仍按所有建筑共用固定兑换秒数描述，以本节为准。当前迁移最高版本为 V110；前文 V104/V105 边界属于历史说明。既有玩家剩余工作时间不回写，历史扣费记录不重算。所有玩家接口使用 `/api` 前缀及 `Authorization: Bearer <player-token>`，身份从令牌取得，不接收其他玩家 ID。V109 仅修复迁移兼容性并加固数据库约束，V110 增加可配置沙漠地块和地块启停状态；接口路径、请求和响应均不变；已按本节接入的 Unity 无需因本次修复再次修改。

## 一、计费与运行规则

- 有效建筑为已完成 `COMPLETED` 且至少有一条生效生产规则 `rule_active=true` 的建筑；一栋多个产出只计一次，建造中、升级中、无有效配方的建筑不计。满仓、配方原料不足仍计入有效建筑，供给时间不保证每栋都能实际产出。
- 手动补充：`floor(quantity × workSecondsPerItem ÷ activeBuildingCount)` 得到增加的全局工作秒数。`quantity` 是实际消耗物品总数；`workSecondsPerItem` 是单栋、每个物品的基准秒数。默认面包 3600 秒时，1 栋用 1 个、5 栋用 5 个均增加 1 小时；5 栋用 1 个增加 720 秒。
- 没有有效建筑或本次供给不足以增加 1 秒时不扣费。容量、每日手动额度、背包数量和单次上限由服务端统一检查。
- 自动续费：`nextRenewalCost = activeBuildingCount × fuelPerBuilding`，一次续购 `renewalCycleSeconds` 秒；默认每栋 1 个、周期 3600 秒。它是独立的固定周期费用，不使用手动供给的 `workSecondsPerItem` 再次换算。
- 玩家开启后先用完已付时间，到期扣下一个周期费用。库存不足进入等待，默认 60 秒后重试；恢复时从当前服务器时间续一个周期，不补扣停机或缺料期间的历史费用。自动续费不占每日手动补充额度。
- 后续新建建筑可使用本周期剩余全局工作时间；建筑数量增减不重新计算已付时间，下一次补充或续费按新数量收费。关闭持续生产也保留已付时间。

## 二、手动补充接入调整

原接口继续使用：`GET /api/production/work-state`、`POST /api/production/work-time/preview`、`POST /api/production/work-time/consume`。

预览/消耗请求示例（物品 ID 必须来自 `work-state.fuels`，不要写死示例值）：

```json
{
  "itemId": 200101,
  "quantity": 5,
  "expectedConfigVersion": 8,
  "expectedActiveBuildingCount": 5
}
```

`expectedActiveBuildingCount` 为新增可选字段。新 Unity 在确认消耗时传预览的建筑数；数量变化返回业务码 `7809`（HTTP 409），重新预览确认。旧三字段请求仍可调用。配置版本变化返回 `7803`，同样重新预览。

预览新增 `activeBuildingCount`（计费建筑数）和 `minConsumableQuantity`（最少能增加 1 秒的物品总数）；供给列表也增加 `minConsumableQuantity`，`maxConsumableQuantity` 按新公式返回。Unity 显示服务端 `requestedWorkSeconds`、确认后使用 `grantedWorkSeconds/workState`，不可自行按原固定公式计算。`consume` 继续要求 UUID `Idempotency-Key`，超时重试保持原键、原请求体；历史成功响应原样重放。

## 三、持续生产设置查询

```http
GET /api/production/continuous-policy
Authorization: Bearer <player-token>
```

成功响应：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "featureAvailable": true,
    "enabled": true,
    "fuelItemId": 200101,
    "fuelItemKey": "bread",
    "fuelItemName": "面包",
    "status": "RUNNING",
    "activeBuildingCount": 5,
    "renewalCycleSeconds": 3600,
    "fuelPerBuilding": 1,
    "nextRenewalCost": 5,
    "ownedFuelQuantity": 20,
    "nextAttemptAt": "2026-09-08T14:00:00",
    "lastRenewAt": "2026-09-08T13:00:00",
    "lastFailureReason": null,
    "version": 4,
    "configVersion": 8,
    "serverTime": "2026-09-08T13:00:00",
    "remainingWorkSeconds": 3600,
    "workUntil": "2026-09-08T14:00:00"
  }
}
```

`featureAvailable` 综合全局配置和当前账号可扣费状态；`enabled` 是玩家开关。配置停用或账号冻结时玩家选择会保留，但不能扣费。`version` 是玩家策略版本，`configVersion` 是正式配置版本，二者不可混用。`nextAttemptAt` 是计划检查时间，不代表保证续费成功。GET 在到期且达到重试时间时会兜底执行一次续费，因此需要刷新背包并使用返回的工作状态。

| status | 中文显示 | 含义 |
| --- | --- | --- |
| DISABLED | 持续生产已关闭 | 不自动扣费，已有时间保留 |
| RUNNING | 持续生产已开启 | 已付时间可运行，后续到期再校验资源 |
| WAITING_RESOURCE | 供给物品不足 | 本次未扣费，等待重试 |
| NO_ACTIVE_BUILDING | 暂无有效生产建筑 | 本次未扣费，等待建筑可用 |
| CONFIG_UNAVAILABLE | 持续生产暂不可用 | 配置、所选物品或账号扣费条件不可用 |

## 四、开启、关闭、更换供给

```http
PUT /api/production/continuous-policy
Authorization: Bearer <player-token>
Content-Type: application/json
```

```json
{
  "enabled": true,
  "fuelItemId": 200101,
  "expectedVersion": 4
}
```

供给选项使用 `work-state.fuels`。关闭时传 `enabled=false`、`fuelItemId=null`、最新 `expectedVersion`。返回与查询相同的完整状态。开启时若工作时间已耗尽，立即尝试一个周期；资源不足则成功保存开关，返回 `WAITING_RESOURCE`，不负扣资源。

错误处理：`7814` 表示功能未开放（HTTP 403）；`7815` 表示策略版本已变（HTTP 409）；`7818` 表示并发更新冲突（HTTP 409）；`7802` 表示所选供给不存在或不可用。冲突或超时后先 GET 确认结果，刷新版本，再由玩家确认提交，不自动重复点击或盲目覆盖其他设备设置。

## 五、Unity 页面操作顺序

1. 进入生产页面、重新登录、切回前台时查询 `work-state` 和 `continuous-policy`。
2. 展示服务端剩余时间、有效建筑数、每周期总费用、库存、玩家开关与状态。选定物品来自后端列表。
3. 手动加时先调用 preview，显示服务端费用和秒数，确认时复用预览建筑数、配置版本并提交 consume。
4. 开关或更换供给时使用最新策略 `version` 调用 PUT；返回后更新面板及背包。原持续生产开关若只保存在 Unity 本地，需要改接这个接口。
5. 页面保持打开可每 30～60 秒刷新；资源变化后重新查询。后台默认每 5 秒扫描一次、每批最多 200 人，繁忙时可能延后，不应承诺精确零延迟扣费。当前没有新增持续生产 WebSocket 事件。

## 六、管理端与运维

位置：配置中心 → 建筑生产（`BUILDING_PRODUCTION / GLOBAL`）。工作策略中配置“自动续费生产”开关、周期（60～86400 秒，且不超过工作时间容量）、每栋每周期供给数量（1～1000）。补充物品中的“单栋每单位工作秒数”用于手动供给；要让面包满足每栋每小时 1 个，配置为 3600 秒，自动续费配置为 3600 秒/1 个。

Worker 默认 `app.production.continuous-renewal.enabled=true`、初始/扫描间隔 5000 毫秒、每批 200 人、独立 Redis 扫描锁开启、租期 60000 毫秒。`all` 或 `worker` 角色执行调度，纯 `api` 角色只提供接口兜底。Redis 扫描锁只负责避免重复扫描，扣费正确性由数据库玩家行锁、策略版本条件更新、成功流水唯一键和事务保证。失败玩家的重试时间会单独后移，避免持续占据扫描队首。

新成功流水表：`player_production_auto_renew_record`；新玩家设置表：`player_production_continuous_policy`。经济流水来源 `PRODUCTION_AUTO_RENEWAL` 显示“持续生产续费”，业务引用为 `AUTO_RENEW:玩家ID:续费前策略版本`。正式发布仍走草稿、校验、发布流程。

V108 仅新增结构和默认配置字段，不重写现有工作时钟、余额或建筑；其已执行原文按 CRC32 `-143402381` 冻结，不再修改。应用在首次查询或设置持续生产时可按需创建关闭状态的策略行，不会替玩家自动开启。

V109 使用 `V109__harden_continuous_production_contract.sql` 前向补齐两项约束：

1. 成功流水必须满足“实际消耗量 = 有效建筑数 × 每栋每周期费用”。先检查历史流水，异常时直接阻断迁移并保留记录，不伪造或重写扣费审计。
2. 关闭策略必须没有调度时间，开启策略必须有调度时间。只归一不符合此规则的策略：关闭时清空 `next_attempt_at`，开启但时间为空时设为数据库当前时间；对应 `version` 递增、`update_time` 更新。余额、已付时钟、玩家开关、物品选择和其余策略均不变，应用恢复后仍先校验工作时间和资源，不补扣历史费用。

上线前备份数据库并停止旧 API/Worker 写入，保持 Flyway 与验证开启，由部署人员手动启动一个新版节点完成尚未执行的迁移；确认 V108 的 `success=1`、`checksum=-143402381` 和 V109 的 `success=1` 后，再启动其余同版本节点。禁止修改 `flyway_schema_history`、关闭校验或使用 `repair`。若出现失败记录，先保留日志并检查 DDL 实际状态，不直接重跑或假定 MySQL 已完整回滚。

本次迁移验收流程：独立临时 MySQL 库执行 V107→原始 V108→V109，检查重复迁移零变更、异常历史审计阻断且保留、策略时间归一和版本变化、余额/已付时钟/玩家选择不变，并继续验证并发只扣一次、审计失败时业务事务回滚和资源不足等待。实际测试结果以交付说明为准，不以自动启动游戏后端替代测试。部署预检和只读核对 SQL 见项目 `docs/database-script-guide.md` 顶部“当前迁移边界”。

2026-09-08 验证结果：上述独立 MySQL 集成测试共 7 项全部通过，临时测试库已清理；后端全量测试共 1571 项，实际执行 1482 项、条件跳过 89 项，失败和错误均为 0。V108 固定校验和为 `-143402381`，V109 为 `-804663872`，均已补齐回归保护。实际游戏库的只读检查显示 V109 已于 16:41:14 成功执行，110 条 SQL 迁移校验和与源码和编译资源完全一致，应用日志已记录启动成功。本次修复不改变 Unity 接口；已完成持续生产接入的客户端无需再次修改。

## 限时活动抽奖（V113-V114）

- 查询：`GET /api/card-draw/pools/{poolCode}`；抽奖：`POST /api/card-draw/pools/{poolCode}/draw`，请求体仅传 `optionCode`，请求头必须传每次操作唯一的 `Idempotency-Key`。
- 普通卡池仍使用旧的 `/api/card-draw/current`、`/api/card-draw/draw`，费用固定为钻石；限时卡池使用独立 `poolCode`，档位配置选择有效的 `CARD_DRAW_TICKET` 展示券，实际消费按玩家全部 `CARD_DRAW_TICKET` 余额汇总。活动券永久有效，可用于任何当前开放的限时卡池，不受来源活动约束，也不限于下一期。
- 活动时间使用服务端数据库时间，规则为开始包含、结束不包含。服务端负责时间、随机、保底、重复 NPC 转碎片、扣券和发奖，Unity 只展示接口返回内容。
- 配置中心 → 抽卡奖池的限定卡池入口为 `LIMITED_POOL`，填写活动名称、开始/结束时间，启用档位统一选择同一种活动券作为展示和发放绑定。奖池编码与活动编码首次发布时由系统生成并只读展示，历史编码不变；普通卡池使用 `STANDARD_POOL` 单独维护。发布会校验券绑定、引用资产、概率、保底传说候选和碎片配置。
- V113 增加活动表、卡池生效范围、按卡池隔离的幂等键和首次接纳时间；V114 增加活动专属券绑定。后端启动时 Flyway 自动执行，禁止修改 V111/V112 或使用 `repair`。

### 旧独立活动收获任务下线（V125）

- V115/V116 曾为 `periodType=ACTIVITY` 的独立活动收获任务增加活动绑定、时间快照和数据库约束；这两份已执行迁移保持原文不变，仅作为历史记录保留。
- V125 以前向迁移清理历史 `ACTIVITY` 任务配置、玩家任务实例、奖励快照、奖励领取流水及随机 NPC/固定奖励子配置，然后移除 `activity_code`、活动时间快照字段、旧索引/外键/检查约束，并把任务周期收紧为 `DAILY`、`WEEKLY`、`LIFETIME`。
- V125 不删除或改写普通每日任务的 `activityBonus` 快照，也不影响活动签到、钻石购买活动券和限定卡池；历史活动券到期策略及出售订单仅保留审计，V143 起不再创建新的出售成交。当前活动抽奖券不得通过独立活动任务或普通固定奖励发放。
- V125 由 Flyway 在备份后自动执行；不得手工重复执行 SQL、修改 V115/V116 原文或使用 `flyway repair` 掩盖校验和不一致。先只启动一个新版本节点，确认 V125 为 `success=1` 后再启动其他 API/Worker 节点。

### 活动签到、钻石购券与历史出售审计（V118-V122）

- V118 建立钻石商城活动券基础结构，已执行原文 checksum 固定为 `-1164195219`；V119 增加绑定限时活动周期的签到计划；V120 增加活动券到期策略，已执行原文 checksum 固定为 `1978746044`。
- V121 只做前向加固，不重复创建 V118 已有字段：一张券只能绑定一个活动，活动商品必须绑定该活动的专属券，同一活动多个商品共享玩家累计购券上限，订单保留完整活动快照。
- V122 增加活动结束后的玩家主动出售订单结构。V143 以前的 `RETAIN`/`SELL` 仅作为历史数据语义保留；当前所有活动券永久有效，并可用于任何当前开放的限时卡池。管理端不再提供到期策略入口，也不支持写入或发布 `ACTIVITY_TICKET_POLICY`；系统不自动删除、转换或出售券，不创建新的出售成交。历史策略表、订单和已执行迁移保留不变。

#### 活动签到专属券绑定校验（发布时）

活动签到只能绑定已经发布且启用的限时活动。管理端保存或发布时，服务端会按 `activityCode` 重新读取活动权威记录，并在同一校验事务中核对以下内容；浏览器提交的名称、时间、版本和券快照只用于比对，不能作为信任来源：

1. 签到计划编码必须由活动编码自动派生，且活动名称、开始时间、结束时间、配置版本快照与已发布活动完全一致；活动不存在、未发布、已停用或快照过期时阻止发布。
2. 活动必须恰好关联一条已发布并启用的 `LIMITED_ACTIVITY` 卡池。卡池的活动编码、起止时间、专属券物品 ID、物品编码和类型必须分别与活动登记值一致；缺少卡池、重复卡池或绑定不一致均阻止发布。
3. 抽奖券必须存在、启用且物品类型为 `CARD_DRAW_TICKET`，ID 与编码必须是一对一的有效组合。发放来源保留活动快照用于审计，但运行时扣券不按活动编码隔离，所有启用的 `CARD_DRAW_TICKET` 均可用于当前限定卡池。
4. 活动签到不允许补签；活动开始后活动身份、时间和专属券绑定冻结。普通 `NEWBIE`/`MONTHLY` 签到不进入上述活动券校验，原有奖励规则保持不变。

发布验收至少覆盖：正确绑定通过；其他活动券、券 ID/编码/类型不匹配、无效或重复限定卡池、活动快照被篡改、NPC 重复补偿引用其他活动券及 fallback 快照 ID 错配均被拒绝。对应回归用例位于 `SignInOperatorConfigHandlerTest`，包括 `acceptsCorrectlyBoundActivitySignInTicket`、`rejectsActivitySignInRewardUsingAnotherActivityTicket`、`rejectsActivitySignInTicketWhenIdAndKeyDoNotMatch`、`rejectsActivitySignInWhenRegisteredTicketTypeIsNotCardDrawTicket`、`rejectsActivitySignInWhenLimitedPoolBindingIsInvalid`、`rejectsActivitySignInWhenActivityPoolRowsAreDuplicated`、`rejectsActivitySignInWhenSnapshotDoesNotMatchPublishedActivity`、`rejectsActivitySignInWhenFallbackReferencesAnotherActivityTicket` 和 `rejectsActivitySignInWhenFallbackTicketSnapshotIdDoesNotMatch`。

- 活动券商品继续使用 `GET /api/diamond-shop/catalog`、`POST /api/diamond-shop/purchases`、`GET /api/diamond-shop/purchases/history`。目录活动字段为 `activityCode`、`activityName`、`activityStartTime`、`activityEndTime`、`activityOpen`、`activityRewardQuantityLimit`、`usedActivityRewardQuantity`、`remainingActivityRewardQuantity`；普通商品这些字段为 `null`。
- 未开始或已结束时活动商品返回 `activityOpen=false`、`maxPurchasableLots=0`。成交及历史新增 `remainingActivityRewardQuantityAfter` 等活动快照；同一幂等键重试只返回首次成交结果。

```http
GET /api/activity-ticket-sales/quotes
Authorization: Bearer <玩家令牌>

POST /api/activity-ticket-sales
Authorization: Bearer <玩家令牌>
Idempotency-Key: <标准UUID>
Content-Type: application/json

GET /api/activity-ticket-sales/history?beforeId=<订单ID>&limit=20
Authorization: Bearer <玩家令牌>
```

```json
{
  "activityCode": "ACT_20260915_0123456789ABCDEF0123456789ABCDEF",
  "quantity": 10
}
```

出售接口不接受券物品 ID、目标币种或单价。该接口仅为历史订单兼容保留；V143 起查询报价为空，新的出售请求统一拒绝，不能扣券或增加货币。历史订单仍可按原快照审计查询。

Flyway 上线时保持校验开启并先执行 `mvn clean`，避免 `target/classes` 残留旧迁移。V123 的原始校验和为 `1737150770`，V124 的定稿校验和为 `-1941686628`；V125 是新的前向清理迁移，首次执行前应备份数据库并确认旧 `ACTIVITY` 任务数据范围。V125 会按外键顺序清理旧独立活动任务及其奖励流水，再移除旧活动字段和约束，保留每日 `activityBonus`、活动签到、钻石购券和历史活动券出售数据。V143 起活动券永久通用，历史出售结构仅用于审计，不能新建出售成交。临时 MySQL 应验证空库顺序迁移、已有 V124 库升级、重复迁移为零以及 V125 清理后任务周期约束只允许 `DAILY/WEEKLY/LIFETIME`。禁止删除或改写已执行迁移，也禁止用 `flyway repair` 掩盖校验和不一致。

### 普通每日任务活动期间加送专属券（V123-V124，V125 后仍保留）

V123 实现的是“普通每日任务在某一个限时活动期间额外送一张该活动展示券”，不是自动发券。发放仍绑定所选活动并由玩家在活动时间内主动领取；抽奖消费则按 `CARD_DRAW_TICKET` 类型汇总，任意活动券都可用于当前生效的限时卡池。

#### 管理端配置

入口：配置中心 → 玩家任务（`PLAYER_TASK / TASKS`）。展开一条普通每日任务，在“活动期间加送专属券”中选择活动。页面会自动展示活动名称、活动阶段、对应限定卡池、专属券名称与图标、固定数量 `×1` 及北京时间窗口；管理员不填写券 ID、券编码、券数量或卡池编码。

配置中心单条任务的新增字段：

```json
{
  "taskCategory": "ROUTINE",
  "periodType": "DAILY",
  "activityBonus": {
    "activityCode": "ACT_20260920_0123456789ABCDEF0123456789ABCDEF"
  }
}
```

未配置活动加送时提交：

```json
{
  "activityBonus": null
}
```

配置约束如下：

1. `activityBonus` 只允许 `taskCategory=ROUTINE` 且 `periodType=DAILY` 的普通每日任务使用；周常和永久成就必须为 `null`。V125 后不存在可新建或继续物化的独立 `ACTIVITY` 任务。
2. 选择活动后，后端自动派生该活动唯一专属 `CARD_DRAW_TICKET`，数量固定为 `1`，不能由请求覆盖。
3. 活动必须已发布并启用，且必须绑定唯一处于 `ACTIVE` 状态的 `LIMITED_ACTIVITY` 限定卡池；卡池范围、活动编码和起止时间必须完全对应。
4. 专属券必须存在、启用且类型为 `CARD_DRAW_TICKET`。限定卡池至少有一个启用抽奖档位，并且所有启用档位都只能消耗该活动绑定的专属券。
5. 活动 A 的券可以用于活动 B 的限定卡池；普通固定奖励仍需按业务模块的活动绑定校验发放，避免无活动上下文的错误发券。
6. 新增或切换活动绑定必须在活动开始日北京时间 `00:00` 前发布。例如活动在 `2026-09-20 10:00` 开始，发布截止仍为 `2026-09-20 00:00`。
7. 从活动开始日 `00:00` 起至活动结束，已绑定任务的整份配置被冻结，名称、描述、启停状态、目标、数量、经验、固定奖励、随机奖励、排序和活动绑定均不能修改；活动结束后才可在后续配置版本中调整。这样可以保证当天零点已经生成任务的玩家和稍后登录的玩家获得一致快照。
8. 限定卡池抽奖会校验档位配置中的展示券为有效 `CARD_DRAW_TICKET` 物品，但实际扣券按玩家全部 `CARD_DRAW_TICKET` 余额汇总；任意活动券都可用于当前生效的限定卡池，普通池仍只消耗钻石，普通池与限定池的相同幂等请求号也不会跨卡池重放。

管理端活动字典 `GET /api/ops/config/dictionaries` 的 `activities` 会同时返回 `activityCode/activityName`、`poolCode/poolName`、`startTime/endTime`、专属券摘要，以及 `phase`、`dailyTaskBonusBindingValid`、`dailyTaskBonusSelectable`、`dailyTaskBonusBindingFrozen`、`dailyTaskBonusUnavailableReason`。页面以这些服务端字段显示可选状态和中文原因，不使用浏览器时间自行判断；活动、券或限定卡池链路异常时显示红色并阻止发布，不会把“不可配置”误显示为正常。

#### Unity 查询与展示

接口不变：

```http
GET /api/tasks
Authorization: Bearer <player-token>
```

活动开放且任务存在有效加送快照时，任务元素新增 `activityBonus`：

```json
{
  "id": 92001,
  "taskCode": "DAILY_WOOD_COLLECTION",
  "taskName": "每日收获木材",
  "taskCategory": "ROUTINE",
  "periodType": "DAILY",
  "progressAmount": 5,
  "targetAmount": 5,
  "activityBonus": {
    "activityCode": "ACT_20260920_0123456789ABCDEF0123456789ABCDEF",
    "activityName": "金秋收获季",
    "startTime": "2026-09-20T10:00:00",
    "endTime": "2026-09-27T10:00:00",
    "ticketItemId": 8001,
    "ticketItemKey": "TICKET_AUTUMN_2026",
    "ticketItemName": "金秋活动抽奖券",
    "ticketItemIcon": "item/ticket_autumn_2026",
    "ticketQuantity": 1,
    "status": "ELIGIBLE",
    "canClaim": true
  },
  "status": "COMPLETED",
  "canClaim": true
}
```

当前任务响应不再提供独立活动任务归属字段；普通 `DAILY` 任务的加送只看 `activityBonus`，Unity 不能把活动编码当作独立任务入口。`activityBonus.status` 可见值为 `PENDING / ELIGIBLE / GRANTED`，其中只有 `ELIGIBLE` 且 `activityBonus.canClaim=true` 时显示“随任务领取专属券”。

以下情况 `activityBonus=null`：未配置加送、任务没有形成有效快照、任务在活动窗口外完成、加送已经逾期，或数据库业务时间已经到达活动结束时刻。Unity 收到空值必须隐藏加送区域并清除本地缓存，不能使用手机时间恢复入口。

#### 玩家主动领取

仍使用原接口，不新增请求字段：

```http
POST /api/tasks/{playerTaskId}/claim
Authorization: Bearer <player-token>
```

活动期内成功发放时，领取响应新增：

```json
{
  "activityBonusGrant": {
    "activityCode": "ACT_20260920_0123456789ABCDEF0123456789ABCDEF",
    "activityName": "金秋收获季",
    "startTime": "2026-09-20T10:00:00",
    "endTime": "2026-09-27T10:00:00",
    "ticketItemId": 8001,
    "ticketItemKey": "TICKET_AUTUMN_2026",
    "ticketItemName": "金秋活动抽奖券",
    "ticketItemIcon": "item/ticket_autumn_2026",
    "ticketQuantity": 1,
    "status": "GRANTED",
    "canClaim": false
  }
}
```

实际完整响应仍包含原有 `task`、`experienceGrant` 和 `rewardGrants`。未配置加送、完成时间不合格，或首次领取已经到达活动结束时刻时，`activityBonusGrant=null`，但基础经验、固定奖励和随机奖励照常领取。任务完成本身不发券；Unity 也不能在完成回调中直接增加背包数量。

任务实例会冻结活动编码、名称、配置版本、活动时间以及券的 ID、编码、名称、图标、类型和数量。完成与领取均使用数据库北京时间，并分别满足 `startTime <= businessTime < endTime`。临界规则如下：

| 场景 | 加送结果 | 基础任务奖励 |
| --- | --- | --- |
| 未配置 `activityBonus` | 无加送 | 正常领取 |
| 活动开始前完成，活动期间领取 | 无加送 | 正常领取 |
| 活动开始时刻完成并在窗口内领取 | 加送 1 张专属券 | 正常领取 |
| 活动期内完成，结束时刻或之后首次领取 | 加送过期，不发券 | 正常领取 |
| 活动结束时刻完成 | 不符合窗口，不发券 | 正常领取 |
| 活动期内已成功领取，网络重试发生在活动结束后 | 只重放首次 `GRANTED` 快照，不重复发券 | 只重放首次结果 |

#### Unity 接入和向后兼容

1. 旧 Unity 不认识 `activityBonus` 和 `activityBonusGrant` 时可以忽略新增 JSON 字段，原任务列表、进度与基础领奖流程不受影响。
2. 新 Unity 在任务卡片或奖励预览中读取非空 `task.activityBonus`，展示服务端返回的券图标、名称和数量；不要根据活动编码本地映射任意券。
3. 点击原任务领取按钮后，以非空 `activityBonusGrant` 播放专属券到账效果，以 `rewardGrants` 播放基础实际奖励；返回空值时不要补发。
4. 任务完成、领取成功、应用回到前台以及活动倒计时到点后重新调用 `GET /api/tasks`。活动结束后服务端不再返回加送，客户端应立即移除旧展示。
5. 网络超时使用同一个 `playerTaskId` 重试。任务 ID 是稳定幂等键；已经成功发放的专属券不会重复增加。
6. V123 不新增任务进度上报接口，不改变生产收取接口，也不要求 Unity 提交活动、卡池、券或数量。

V125 执行后不再支持 `ROUTINE + ACTIVITY` 独立活动收获任务；历史实例及其奖励流水由迁移清理。V123/V124 的普通每日任务加送继续使用独立快照和状态，活动签到、钻石购券和限定卡池合同不变；活动券到期出售仅保留历史审计，V143 起不能新建或成交。

V123 已按数据库记录的原始 CRC32 `1737150770` 冻结，V124 定稿 CRC32 为 `-1941686628`；二者均不得改写。V125 使用新的前向迁移清理旧独立活动任务及其字段/约束，不改变 V123/V124 的文件内容。V125 执行后任务周期数据库约束只允许 `DAILY/WEEKLY/LIFETIME`，后续任务规则变更必须继续新建更高版本迁移。

## 66. V126-V130 经营场景、历史成交与生产建筑扩展

本节是经营地图与当前生产建筑目录的最新权威合同；前文仍描述“8 个 2×2 单元统一等级、8 格免费、24 格统一金币价”的内容仅用于理解历史兼容。V126 首次建立 8×4 共 32 个坐标级正式规则，已执行原文以 checksum `1602461842` 冻结；V127 只前向修正历史成交分类和约束，checksum 为 `-376022642`；V128/V129 完成雪松林场初版和前向修正；V130 以 checksum `-1808775742` 新增默认停用的砂石场与矿冶窑。V130 不改变以下坐标级 Unity/HTTP 合同：

- 每格独立 `requiredLevel`。
- `unlockMode` 支持 `FREE / GOLD / DIAMOND`。
- `tierCode / tierName / costAmount` 表达运营价格阶梯；相同编码必须同名称、同币种、同价格。
- 首个完整支持新版规则的 Unity 数字版本为 `10003`。
- 旧 `requiredLevels / paidSlotGoldCost / goldCost` 只做兼容摘要，新 Unity 不得用它们自行计算报价或权限。

Unity 完整接入顺序如下；场景切换、净化、放置的请求体和完整响应结构可继续参考第 41.1～41.6 节，但凡价格、等级、版本或购买字段与本节冲突，一律以本节为准：

1. 调用 `GET /api/land/scenes/list/{playerId}` 获取场景列表。
2. 玩家选择场景时调用 `POST /api/land/scenes/switch`。
3. 调用 `GET /api/land/map/{playerId}?sceneId={sceneId}` 获取完整 32 格地图及权威报价。
4. 需要净化时调用 `POST /api/land/slot/purify`，成功后重新读取整图。
5. 付费解锁时调用 `POST /api/land/slot/purchase`，必须原样回传本节规定的全部 `expected*` 报价字段。
6. 放置地块时调用 `POST /api/land/place` 并显式携带 `sceneId`。
7. 接入 `LAND_SCENE_UNLOCKED / LAND_SCENE_SWITCHED / LAND_SLOT_STATE_CHANGED`；收到刷新提示、WebSocket 重连或应用回到前台时重新查询场景列表或整图，不能把实时消息当作最终权威状态。

### 66.1 Unity 请求头

场景列表、场景切换、地图、购买、净化和放置请求统一携带：

```http
X-Client-Version-Code: 10003
Authorization: Bearer <player-token>
```

场景仍完全兼容旧模型时，管理端可设置 `minimumClientVersionCode=0`；一旦使用钻石、每格差异、多个阶梯或不同免费布局，最低版本不得低于 `10003`。场景列表只按玩家当前场景校验，列表中其他高版本场景不会让整份列表失败；每个列表项都会返回自己的 `minimumClientVersionCode`，Unity 应在场景卡片上显示“需要更新”。场景切换按目标场景校验，地图和写操作按实际场景校验。请求头缺失、为 0、低于对应门槛或格式非法时，服务端返回 HTTP `426 Upgrade Required`、业务码 `7603`。Unity 必须进入更新引导，不能按旧金币界面继续操作。

### 66.2 场景与地图接口

```http
GET /api/land/scenes/list/{playerId}
GET /api/land/map/{playerId}?sceneId={sceneId}
```

场景列表项和地图顶层都会返回 `minimumClientVersionCode`；地图固定返回 32 条 `landSlots`。单格核心结构：

```json
{
  "sceneId": 1,
  "positionX": 2,
  "positionY": 3,
  "unitIndex": 6,
  "requiredLevel": 20,
  "levelUnlocked": true,
  "requiresPurification": true,
  "purified": true,
  "purificationAvailable": false,
  "purificationItemId": 200000,
  "purificationItemKey": "purification_agent",
  "purificationItemAmount": 1,
  "unlockMode": "DIAMOND",
  "tierCode": "DIAMOND_PREMIUM",
  "tierName": "高级钻石格",
  "costAmount": 30,
  "requiresPurchase": true,
  "purchased": false,
  "purchaseAvailable": true,
  "placementUnlocked": false,
  "purchaseCost": {
    "itemId": 2,
    "itemKey": "diamond",
    "itemName": "钻石",
    "icon": "diamond",
    "amount": 30
  },
  "ruleFingerprint": "64位小写SHA-256",
  "sceneConfigVersion": 12,
  "goldCost": 0
}
```

Unity 处理规则：

1. 使用 `landGridWidth=8 / landGridHeight=4 / landSlotCount=32` 创建网格。
2. 直接使用 `placementUnlocked` 控制放置，不在本地重新组合权限。
3. `FREE` 不显示购买；`GOLD` 使用 `purchaseCost` 的金币信息；`DIAMOND` 使用钻石信息。
4. `goldCost` 仅兼容旧 Unity：金币格等于金额，免费格和钻石格为 0，不能用 `goldCost=0` 判断免费。
5. 购买成功后重新查询完整地图，因为相邻格的 `purchaseAvailable` 可能一起变化。

### 66.3 购买接口与确认报价

```http
POST /api/land/slot/purchase
Content-Type: application/json
Authorization: Bearer <player-token>
X-Client-Version-Code: 10003
```

```json
{
  "playerId": 5,
  "sceneId": 1,
  "positionX": 2,
  "positionY": 3,
  "expectedSceneVersion": 12,
  "expectedUnlockMode": "DIAMOND",
  "expectedCurrencyItemId": 2,
  "expectedCurrencyItemKey": "diamond",
  "expectedCostAmount": 30,
  "expectedRuleFingerprint": "64位小写SHA-256"
}
```

客户端把地图确认框实际展示的版本、方式、货币物品 ID、货币编码、金额和指纹原样回传。服务端在玩家锁后重新共享锁定数据库场景和坐标规则，实际扣款只使用数据库中的货币 ID 与金额；客户端字段只用于阻止旧报价误扣。规则变化返回 HTTP `409`、业务码 `7601`，必须刷新地图并重新确认；HTTP `503`、业务码 `7602` 是兼容保留的策略不可用处理，客户端应防御性处理但不能依赖当前版本一定返回；客户端版本不足返回 HTTP `426`、业务码 `7603`。

相同玩家、场景和坐标重复提交已完成购买时，服务端先返回既有永久权益，不重复扣款，也不因后来调价失败。首次成功后刷新对应金币或钻石余额、完整 32 格地图及场景里程碑。

### 66.4 成交快照

`player_land_slot_unlock` 保存成交审计字段；V126 后的新成交完整冻结。V127 以前向迁移归一 V126 执行前的历史记录，只保存旧数据能够证明的事实：

- `acquisition_type`：`LEGACY_GRANT / LEGACY_PAID_PURCHASE / PAID_PURCHASE`。
- `unlock_mode_snapshot`。
- `tier_code_snapshot / tier_name_snapshot`。
- `currency_item_id / currency_item_key_snapshot / currency_item_name_snapshot / currency_item_icon_snapshot`。
- `actual_cost`。
- `required_level_snapshot`。
- `scene_config_version_snapshot`。
- `rule_fingerprint`。
- `unlocked_at`。

历史 `gold_cost=0` 的权益标记为 `LEGACY_GRANT`；历史非零金币成交标记为 `LEGACY_PAID_PURCHASE`，只回填 `GOLD`、金币物品身份和原实付金额。旧表无法证明的阶梯、等级门槛、场景版本、规则指纹、货币名称和图标必须保持 `null`，不能套用迁移时的当前规则伪造历史。只有 V126 后的新 `PAID_PURCHASE` 才要求上述字段全部完整。金币交易继续让兼容列 `gold_cost=actual_cost`，钻石新交易的兼容列为 0；管理端经济来源将本次扣款显示为“地块格子购买”（编码 `LAND_SLOT_PURCHASE`）。

### 66.5 管理端配置

入口：`配置中心 → 基础资料 → 经营场景目录`，对应：

```text
moduleCode = LAND_SCENE_CATALOG
configKey  = WORLD
```

页面提供 8×4 地图、点击整张地块卡片多选或取消选择、全选、按阶梯选择、批量设置等级与价格、单格覆盖、独立阶梯、免费/金币/钻石统计、总成本和最低 Unity 版本。标准操作：先确认 Unity 已支持版本 10003，再配置场景资源和版本门槛，批量绑定阶梯，处理全部校验错误，保存草稿、校验、发布，最后使用正式 Unity 验证。

发布强制校验：

- 正好 32 格且坐标完整唯一。
- 免费格固定 `FREE / 免费 / 0`；付费金额 `1..1000000000`。
- 至少一个免费入口；每个付费格在其要求等级下可从免费格正交到达。
- 首个启用场景 `(6,0)、(7,0)、(6,1)、(7,1)` 必须全部为 1 级免费，保护新手引导。
- 已有玩家进入的场景不能停用、提高任何坐标等级或把免费格变为付费。
- 已购买和已放置权益永久有效；后续调价、金币/钻石切换只影响尚未购买的格子。

### 66.6 V126～V130 数据库上线顺序

1. 停止旧版本 API 和 Worker，完整备份数据库；预检历史地块记录的场景、坐标及 `gold_cost`，金额必须位于 `0..1000000000`。
2. 保持 Flyway 开启，只启动一个新版本实例。新库会依次执行 V126、V127、V128、V129、V130；已有数据库先确认既有迁移 checksum，再让 Flyway 自动执行尚未应用的 V130。不要手工运行脚本。
3. 确认 `flyway_schema_history` 中 V126/V127/V128/V129/V130 均为 `success=1`，checksum 分别为 `1602461842`、`-376022642`、`-576670290`、`752861842`、`-1808775742`；每个场景在 `land_scene_slot_config` 恰好 32 行且版本与场景一致；全部记录都有 `acquisition_type/actual_cost`，新 `PAID_PURCHASE` 具有完整快照，历史未知字段保持 `NULL`。
4. 核对建筑终态：2013 号 `CEDAR_LUMBER` 保持 V129 最终规则；2014 号 `SANDSTONE_QUARRY` 和 2015 号 `ORE_SMELTING_KILN` 均存在且默认停用，共有六条启用等级和六条唯一 `DEFAULT` 配方；砂石场只有 `stone` 产出且无投入，矿冶窑只有 `iron -> metal`；`1010/DESERT` 的原名称、等级、费用和启停状态未被覆盖，准入同时包含两种新建筑。
5. 再启动其他同版本 API/Worker。不要执行 `repair`，不要修改已经执行的 V126～V130；后续变化必须使用 V131 或更高版本前向迁移。
6. 先发布并验证 Unity 10003 或更高版本及两座建筑资源，再在管理端发布钻石、差异化地块或沙漠建筑配置。

### 66.7 V130 沙漠生产建筑管理端与 Unity 接入

管理端使用 `CONFIG_ADMIN` 进入“配置中心 → 玩法与成长 → 地块与建筑”，在同一 `LAND_BUILDING / WORLD` 草稿中按以下顺序操作：

1. “地块准入”：找到 `landId=1010` 的沙漠地块，核对解锁等级、金币/木材/石材/铁矿放置费用和两种允许建筑；确认经济规则后再切换为“已开放”。
2. “建筑规则”：找到 2014 号砂石场与 2015 号矿冶窑，确认所需地块均为 `DESERT`，矿冶窑前置为 2003 号矿场且矿场保持启用，然后开启需要发布的建筑。
3. “等级与生产”：核对逐级成本、经验、耗时、固定配方数量、周期、容量及 Unity 模型/图标 Key。固定物品身份不能修改。
4. 完成“编辑 → 校验 → 保存草稿 → 发布”。草稿不影响玩家；发布会原子校验地块准入、建筑所需地块、前置建筑和固定配方，任一不一致都会整体拒绝。

“配置中心 → 玩法与成长 → 建筑生产 → 全局建筑生产规则”（`BUILDING_PRODUCTION / GLOBAL`）也会显示这六条规范化默认配方，可调整投入/产出数量、周期和容量，但不能开启建筑、修改地块准入、建造成本、模型/图标 Key、固定物品身份或默认配方结构。两个入口共用同一后端固定配方校验。

Unity 不新增接口路径，继续通过 `GET /api/building/available/{playerId}`、建造条件、地图、生产状态和 `GET /api/production/recipes/{playerBuildingId}` 获取权威配置。客户端需识别 `SANDSTONE_QUARRY` 与 `ORE_SMELTING_KILN`，并准备以下大小写敏感资源 Key：

- `sandstone_quarry_lv1`、`sandstone_quarry_lv2`、`sandstone_quarry_lv3`
- `ore_smelting_kiln_lv1`、`ore_smelting_kiln_lv2`、`ore_smelting_kiln_lv3`

模型和图标默认使用同一组 Key；管理端只登记 Key，不上传 Unity 资源。正式启用前必须确认当前 Unity 包可加载这些资源，或先在管理端改成包内实际 Key。Unity 应以接口响应为准，不写死默认产量、成本或建筑总数；资源加载失败时回退通用占位资源并记录错误，不能阻断玩家进入地图。

详细可维护说明同步在项目文档 `docs/land-production-equipment-api.md`、`docs/operator-config-center.md` 和 `docs/database-script-guide.md`。

## V131 新手签到动态窗口与 Unity 接入补充（历史冻结）

V131 的不可变版本、玩家快照、存量迁移审计和客户端版本观测保持冻结。历史 V132 只拆分活动签到奖励窗口；当前统一字段和新手/活动参与窗口语义以 V133 章节为准。不得修改 V131/V132 迁移或用 `flyway repair` 接受漂移。

三个签到请求都必须携带客户端构建号：

```http
GET /api/sign-in/overview
POST /api/sign-in/claim
POST /api/sign-in/makeup
X-Client-Version-Code: 10005
```

概览协议版本的 JSON 路径是 `data.contractVersion`，不在响应根节点，也不在 `data.plans[]` 内；计划返回 `planVersion`、`minimumClientVersionCode`、`rewardMode`、`rewardWindowDays`、`windowDays`（仅新手）、`expiresAt`，日期数组必须按服务端返回的 `days[]` 动态遍历，每一项的 `rewards[]` 也必须动态遍历。`CLAIM_SEQUENCE` 下 `days[]` 长度等于 `rewardWindowDays`，每个 `rewardDay` 表示领取档位；`CALENDAR_DAY` 下 `days[]` 表示参与窗口的自然日。月度计划为 `planCode=MONTHLY`、`planType=MONTHLY_CALENDAR`，`windowDays/rewardMode/rewardWindowDays` 为 `null` 时仍应显示完整月历。日期状态为 `LOCKED/AVAILABLE/CLAIMED/MISSED/UNAVAILABLE`，已领取日期提供 `claimedAt`。`businessTime` 和 `expiresAt` 使用服务端数据库时间，窗口是左闭右开；客户端不得写死 3 个格子、30 个活动日、奖励项数量或日期。领取成功后重新请求概览。

当 `minimumClientVersionCode > 0` 时，版本头缺失、非法或过低返回 HTTP 426、业务码 `6009`，Unity 必须进入强制升级流程。管理端发布 `windowDays > 3`、`rewardWindowDays > 3` 或 `rewardMode=CLAIM_SEQUENCE` 的新手配置时，合同版本至少为 2、最低 Unity 构建号必须为正数，且最近 7 天动态客户端覆盖率达到 95%；活动配置同样必须使用支持 `days[]` 动态长度的客户端。先发布支持动态数组和奖励模式的 Unity 版本并等待覆盖率达标，再发布长参与窗口或序列奖励配置。管理端就绪度接口为 `GET /api/ops/config/sign-in/client-readiness?minimumClientVersionCode=...`。

版本门禁会中止整次概览请求，不是仅从 `plans[]` 隐藏不支持的计划。因此，即使月度计划最低版本为 `0`，同一玩家仍在进行中的新手或活动计划要求更高版本时，概览也可能整体返回 `6009`；不得把它诊断为“月度没有配置奖励”。构建号必须如实对应客户端能力，不能只提高请求头数值绕过门禁。

V131 上线顺序：备份数据库并停止旧版本写入；保持 `FLYWAY_ENABLED=true`，只启动一个新版本实例自动迁移；确认 `flyway_schema_history` 的 V131 `success=1`、真实 MySQL 集成测试通过且重复迁移零变更；再启动其他节点。V131 已执行后不得修改脚本或使用 `flyway repair`。

## V133 新手/活动签到参与窗口与奖励档位分离（当前权威）

V132 已按 checksum `1930570088` 冻结为活动签到专用迁移；V133 不改写 V131/V132，而是将“玩家可以参与签到多久”和“最多配置多少个奖励档位”统一拆成两个明确概念，并将历史活动字段前向归一为通用合同：

- `windowDays`：仅新手计划使用，表示首次成功登录后冻结的参与有效期；例如 `windowDays=30` 表示玩家可在 30 天内参与签到。
- 活动签到使用已发布活动的 `activityStartTimeSnapshot`、`activityEndTimeSnapshot` 作为参与有效期，采用 `[start, end)` 半开区间；活动结束时间到达后不可领取。
- `rewardWindowDays`：新手和活动计划的奖励档位数量，它决定 `days[]` 的长度，不等于参与窗口天数。新手范围为 1～`windowDays`（`windowDays` 最大 31）；活动范围为 1～活动半开区间的自然日数，服务端硬上限 366。
- `rewardMode=CLAIM_SEQUENCE`：按累计成功领取次数领取。30 天参与窗口只配置 3 档时，`rewardWindowDays=3`，奖励 `rewardDay=1/2/3` 分别表示第 1/2/3 次领取；玩家可以在第 1、6、7 天领取，断签不丢档，同一服务端自然日最多领取一次。
- `rewardMode=CALENDAR_DAY`：按参与窗口自然日序号领取；奖励覆盖范围由 `rewardWindowDays` 决定，缺失日期按发布校验规则处理。
- `planCode=MONTHLY`、`planType=MONTHLY_CALENDAR` 的计划继续按月份自然日运行，`windowDays/rewardMode/rewardWindowDays` 返回 `null`，不受新手/活动奖励档位规则影响；应按 `days[]` 显示当月 28～31 天。

管理端配置示例（30 天有效期、3 档奖励、不要求第 4～30 天奖励）：

```json
{
  "planType": "NEWBIE_WINDOW",
  "windowDays": 30,
  "rewardMode": "CLAIM_SEQUENCE",
  "rewardWindowDays": 3,
  "rewards": [
    { "rewardDay": 1, "rewardSeq": 1, "enabled": true },
    { "rewardDay": 2, "rewardSeq": 1, "enabled": true },
    { "rewardDay": 3, "rewardSeq": 1, "enabled": true }
  ]
}
```

活动签到使用同样的 `rewardMode/rewardWindowDays` 字段，活动起止快照只控制开放时间；`ACTIVITY_CALENDAR + CLAIM_SEQUENCE + rewardWindowDays=3` 时，`days[]` 只返回 3 个奖励档位，不生成 30 个空日期。客户端只提交 `planCode`，不能提交奖励序号、活动日期、奖励内容或数量；服务端按玩家冻结快照和数据库业务时间决定下一档。

Unity 必须：

1. 遍历 `data.plans[].days[]`，不要固定生成 3 格、30 格或其他数量；新手/活动根据 `rewardMode` 显示“第 N 次领取”或“第 N 天”，`MONTHLY_CALENDAR` 按 `rewardDay` 显示当月日期，不依赖可空的 `rewardMode`。
2. 使用 `currentDay`：`CLAIM_SEQUENCE` 表示下一次可领取档位（已领完 3 档时为 4），`CALENDAR_DAY` 表示当前参与窗口自然日序号；`MONTHLY` 仍表示月度日期。
3. 在参与窗口内按 `canClaim/status` 控制按钮；`CLAIM_SEQUENCE` 断签后仍可领取下一档，不要把缺席日期标记为奖励丢失。
4. 领取成功后重新请求概览；活动结束、`expiresAt` 到达或收到 `6003` 时立即停止领取并刷新页面。

V133 上线顺序：备份数据库并停止旧版本写入；保持 `FLYWAY_ENABLED=true`，只启动一个新版本实例自动执行尚未应用的迁移；确认 V131、V132、V133 在 `flyway_schema_history` 中均为 `success=1`，V132 checksum 为 `1930570088`、V133 checksum 为 `-976225678`，并运行后端和管理端签到回归测试、真实 MySQL 迁移测试及重复迁移零变更后，再启动其他节点。V131～V133 均禁止修改或使用 `flyway repair`，后续结构变化从 V134 开始。

## V134/V135 挂机冒险技能、范围目标与治疗规则（当前权威）

本节是 Unity、后端和管理端共同遵循的最终技能契约。它覆盖技能冷却、技能优先级、单体攻击、范围攻击（AOE）、单体回血、动作响应、断线恢复和发布顺序。前文的旧技能说明只用于兼容背景；与本节冲突时以本节为准。

### 1. 版本与数据库边界

V134 不改写 V131～V133。升级时由 Flyway 自动执行
`V134__extend_idle_adventure_skill_target_effects.sql`，为技能表增加初始冷却、效果、目标阵营/模式、目标组和治疗字段；旧数据按 `DAMAGE + OPPONENT + SINGLE + initial_cooldown_rounds=0` 兼容运行。

V135 保持 V134 不可变，由 Flyway 自动执行
`V135__add_idle_adventure_skill_include_self.sql`，为 `ALLY + ALL` 增加 `include_self`，默认不包含施法者。V134 已执行 checksum 必须保持 `501186811`，不得修改历史脚本或使用 `flyway repair` 掩盖漂移。V136 仅增加 `npc_config.portrait_key`，不改动技能结构；V137 清理未上线开发期的废弃装备部位；后续数据库变更从 V138 开始。

上线前确认 V131～V137 在 `flyway_schema_history` 中均为 `success=1`，V134 checksum 为 `501186811`，V135 成功增加 `include_self`，V136 成功增加 `portrait_key`，V137 checksum 为 `911080598` 且废弃部位清理完成，第二次 migrate 不产生变更，并通过 `flyway validate`、真实 MySQL 迁移测试和技能回归测试。

### 1.1 `/config` 基础玩法 DTO

`GET /api/idle-adventure/config` 返回固定玩法参数和服务端行动规则。`turn_interval`、`attack_animation_duration`、`hit_flash_duration` 是秒数（小数），不是毫秒；Unity 只能用于表现和节奏提示，不能用它们推导回合结果。该响应当前不包含玩法版本 ID，版本冻结只存在于服务端会话快照。

| 字段 | 说明 |
| --- | --- |
| `max_party_size`、`formation_rows`、`formation_columns` | 编队上限和布局维度，当前为六槽位 |
| `friendly_skill_slot_count`、`enemy_skill_slot_count` | 友方/敌方技能槽上限 |
| `max_buff_icon_slots` | UI 可展示的 Buff 图标上限 |
| `starting_friendly_count`、`base_enemy_count`、`add_enemy_every_stages` | 初始人数和关卡人数规则 |
| `turn_interval`、`attack_animation_duration`、`hit_flash_duration` | 服务端返回的秒数表现参数 |
| `attribute_restraint_multiplier`、`reward_settlement` | 克制倍率和奖励结算策略 |
| `combat_rules` | `initiative`、`front_column_first`、`prefer_same_row`、`prefer_upper_row_when_same_row_empty` |

### 2. 管理端配置入口与字段

入口：`配置中心 → 挂机冒险 → 技能模板`。管理端保存草稿、校验并发布新的玩法版本；已发布版本不可原地修改。每次创建战斗会话时，服务端把技能规则、NPC 属性、敌人和 Buff 写入会话 `state_json` 快照，并绑定不可变的玩法版本 ID；动画参数按该会话版本读取。会话中途修改管理端配置只影响新会话，重连和回合结算仍使用原版本和快照。

`GET /api/idle-adventure/skills` 的 `data[]` 直接返回技能定义列表；玩家 NPC、敌人预览等角色对象则在 `skill_slots[].skill` 中复用相同定义。技能字段如下：

| 字段 | 允许值/范围 | 用途 |
| --- | --- | --- |
| `skill_id`、`skill_key`、`skill_name` | 服务端生成的 ID、稳定编码、名称 | 唯一识别和展示 |
| `skill_icon` | 逻辑资源键或 `null` | Unity 通过资源绑定表加载技能图标 |
| `cooldown_rounds` | 非负整数 | 技能释放后的冷却回合数 |
| `initial_cooldown_rounds` | `0..1000` | 战斗开始时剩余冷却；双方均适用 |
| `skill_multiplier`、`damage_attribute` | 非负倍率、已配置属性 | 仅服务端计算伤害 |
| `effect_type` | `DAMAGE` / `HEAL` / `BUFF` | 一次技能只能有一个主效果 |
| `target_side` | `OPPONENT` / `ALLY` / `SELF` | 目标阵营，服务端阻止跨阵营 |
| `target_mode` | `SINGLE` / `ORDERED_GROUPS` / `FIXED_SLOTS` / `ALL` / `RANDOM_N` | 目标选择算法 |
| `target_groups` | `number[][]`，最多 12 组，每组最多 6 个 `0..5` 槽位 | `ORDERED_GROUPS` 或 `FIXED_SLOTS` 使用；管理端通过阵型卡片生成，不要求手写 JSON |
| `target_count` | `1..6` | 仅 `RANDOM_N` 使用，表示最多命中数量 |
| `include_self` | `true` / `false` | 仅 `ALLY + ALL` 生效，是否包含施法者 |
| `heal_formula` | `FLAT` / `ATTACK_RATE` / `TARGET_MAX_HEALTH_RATE` | 仅 `HEAL` 使用 |
| `heal_base_value` | `0..1,000,000,000,000`，最多 4 位小数 | 固定治疗量或比例公式的基础值 |
| `heal_multiplier` | 非负小数 | 比例治疗倍率；服务端取整并封顶 |
| `self_buff_preset_id`、`target_buff_preset_id` | 已启用 Buff ID 或 `null` | 动作结算后施加 Buff |

当前发布校验上限为友方最多 2 个技能槽、敌方最多 1 个技能槽；具体数量仍以 `/config` 的 `friendly_skill_slot_count`、`enemy_skill_slot_count` 为准。友方和敌方都按技能槽位升序、冷却和合法目标规则执行。

发布时阻断：目标槽位越界或重复、空目标组、固定格配置多于一组、非范围模式携带目标组、`RANDOM_N` 缺少或超出数量、`HEAL` 指向敌方/范围模式、`SELF` 使用非 `SINGLE/ALL`、`include_self` 用在非 `ALLY + ALL`、非治疗技能填写治疗字段、Buff 无模板或引用停用模板。

### 3. 六槽位和目标选择语义

六槽位是现有战斗系统的**列优先 3 行 2 列**布局，不得在 Unity 中改成行优先：

```text
                         第 1 列（前列）  第 2 列（后列）
第 1 行（row=0）                0              3
第 2 行（row=1）                1              4
第 3 行（row=2）                2              5
```

`ORDERED_GROUPS` 的规则是：从左到右只选**第一个仍有至少一个存活目标的组**；组内删除已死亡槽位并按槽位升序返回，但不从后续组补足。全部组无目标时本次技能不释放、不扣冷却，行动者继续尝试下一个技能，全部技能均不适用才普通攻击。

运营人员点击技能行的“目标规则”按钮打开独立编辑弹窗；弹窗内使用三行两列的可视化阵型卡片，点击槽位大格即可选择或取消，不需要手写 JSON。
`ORDERED_GROUPS` 可以添加、删除和上下移动目标组，组序就是回退优先级；已被其他组使用的槽位
会禁用并显示所属组，避免发布时出现重复。`FIXED_SLOTS` 始终只有一个目标组。保存时仍提交
原有的 `target_groups: number[][]`，不会增加 UI 临时字段。

推荐的两目标前列优先配置为 `[0,1] -> [2] -> [3,4] -> [5]`：

1. `0、1` 都存活：只打 `0、1`；
2. 只剩 `1`：只打 `1`，不补 `2`；
3. `0、1` 都死亡：尝试 `[2]`；`2` 死亡后才尝试 `[3,4]`；
4. `[3,4]` 都死亡后才尝试 `[5]`；
5. 这意味着第 1 行有 1 个、第 3 行有 2 个时只取第 1 行的 1 个，不跨行拼接；“完全没有目标才进入下一组”。

其他可直接在管理端配置的预设：

```text
前列三格 → 后列三格： [0,1,2] -> [3,4,5]
后列三格 → 前列三格： [3,4,5] -> [0,1,2]
逐行两格：           [0,3] -> [1,4] -> [2,5]
固定三格：           [0,4,5]（FIXED_SLOTS，整组无效也不转移）
```

`SINGLE` 不走目标组，沿用普通攻击的服务端顺序：先检查对方前列 `0/1/2`，前列仍有存活目标时只在前列选择；前列全灭后才检查后列 `3/4/5`。同一列优先与施法者同一行，再按行距离升序，最后按行号较大者和槽位升序稳定排序（施法者行 0 为 `0,1,2`，行 1 为 `1,0,2`，行 2 为 `2,1,0`）。技能改成单格不需要 Unity 改目标算法。`FIXED_SLOTS` 只使用唯一一组，指定格死亡就跳过死亡格，绝不自动换到其他格。`ALL` 选择指定阵营全部存活目标；`RANDOM_N` 从当前所有合法存活目标中按服务端可重放排序取前 N 个，Unity 不得自行随机。

每个 AOE 目标都独立按自己的防御、属性和 Buff 计算伤害；`total_damage` 是 `targets[]` 中各项实际伤害之和。旧的 `target_runtime_id`、`target_name`、`damage`、`target_health_before`、`target_health_after` 和 `damage_formula` 只代表第一目标，不能用来替代 AOE 的完整结果。

普通 `ALLY` 范围技能只有在 `target_mode=ALL` 且 `include_self=true` 时才包含施法者；`ALLY + SINGLE/FIXED_SLOTS/ORDERED_GROUPS/RANDOM_N` 不因 `include_self` 自动改变目标规则。只有首版单体 `HEAL + ALLY` 按回血专用规则始终把施法者纳入候选。

### 4. 冷却、技能优先级和释放边界

1. 战斗开始时，友方和敌方每个技能的 `cooldown_remaining` 等于 `initial_cooldown_rounds`；管理端可分别配置，不以 Unity 本地时间倒计时为准。
2. 同一角色技能按 `slot_index` 升序尝试，槽位 0 优先于槽位 1。冷却完成且存在合法目标立即释放；无合法目标只跳过，不扣冷却，再尝试下一个技能。
3. 技能释放后服务端写入冷却；普通攻击只在所有技能不可用或无合法目标时执行。
4. 一次动作结算过程中目标死亡不会追加或转移到其他目标；下一次行动重新依据当前快照选择。
5. 目标全部死亡或行动计划为空时，不再追加该行动；服务端保留已经生成的 `initiative_order`，并按最终 `status` 返回，Unity 不得等待不存在的后续动作。
6. Buff 在本次动作的伤害/治疗全部结算完成后生效，不影响本次动作的攻击力、伤害或 `ATTACK_RATE` 治疗量。

冷却是按回合由服务端推进的：技能释放后先写入该技能的 `cooldown_rounds`，本回合结算结束统一减 1；暂停不会推进。比如 `cooldown_rounds=2`，本回合释放后响应为 1，下一回合结束后为 0，再下一回合才可释放；`initial_cooldown_rounds=2` 时前两次行动不可用，第三次行动才可用。`skill_states[].can_use` 只表示冷却已归零，不保证目标合法；Unity 不得仅凭 `can_use` 播放技能，最终是否释放以本回合 `actions[]` 为准。

`selected_group_seq` 从 1 开始，仅 `ORDERED_GROUPS` 和 `FIXED_SLOTS` 返回；其他模式为 `null`。`selected_slots` 是本次实际命中的存活槽位，始终按槽位升序；`FIXED_SLOTS` 的唯一组命中时序号固定为 1。

### 5. 单体回血规则（首版）

首版只支持 `effect_type=HEAL`、`target_mode=SINGLE`，`target_side` 为 `ALLY` 或 `SELF`。不支持范围回血、随机回血、固定格回血、治疗死亡角色或复活；过量治疗不转护盾。

`ALLY` 候选为己方存活且未满血角色（包含施法者），`SELF` 候选仅施法者。候选排序严格为：

1. 当前生命绝对值从低到高（不是生命百分比）；
2. 生命值相同时施法者优先；
3. 仍相同时后列 `3、4、5` 优先于前列 `0、1、2`；
4. 仍相同时槽位升序。

因此施法者与最低血量角色血量相同，治疗施法者；前列和后列最低血量相同，治疗后列。`ALLY` 的候选始终包含施法者，即使 `include_self=false`；`include_self` 只控制 `ALLY + ALL` 的全体范围技能，不改变首版单体回血规则。治疗量按服务端公式计算并限制为不超过目标最大生命，响应中的 `healed_amount` 是实际恢复量。没有可治疗目标时不释放、不扣冷却。

治疗公式为：`FLAT = floor(heal_base_value)`；`ATTACK_RATE = floor(heal_base_value + 施法者 effective_attack × heal_multiplier)`；`TARGET_MAX_HEALTH_RATE = floor(heal_base_value + 目标 max_health × heal_multiplier)`。结果取非负值并受服务端上限保护，再与目标缺失生命取最小值；使用的是施法前的有效攻击，治疗不会复活、不会转护盾。

### 6. Unity 使用的接口和请求约束

基础地址为部署环境的 `/api`。相关接口如下，所有玩家 ID 必须与 Bearer Token 一致：

| 方法 | 路径 | Unity 用途 |
| --- | --- | --- |
| `GET` | `/idle-adventure/skills` | 技能图鉴、技能名称和配置预览；不用于客户端选目标 |
| `GET` | `/idle-adventure/stages/{stage}/enemies` | 进入关卡前读取六槽位敌人和技能展示 |
| `POST` | `/idle-adventure/players/{player_id}/sessions/start` | 创建战斗，会话快照在此冻结 |
| `GET` | `/idle-adventure/players/{player_id}/sessions/{session_key}` | 重连/切后台后恢复完整状态 |
| `POST` | `/idle-adventure/players/{player_id}/sessions/{session_key}/rounds/resolve` | 请求服务端结算一个回合 |
| `POST` | `/idle-adventure/players/{player_id}/sessions/{session_key}/pause` | 暂停 |
| `POST` | `/idle-adventure/players/{player_id}/sessions/{session_key}/resume` | 恢复 |
| `POST` | `/idle-adventure/players/{player_id}/sessions/{session_key}/stages/continue` | 通关后进入下一关 |
| `POST` | `/idle-adventure/players/{player_id}/sessions/{session_key}/stages/retry` | 失败后重试 |
| `POST` | `/idle-adventure/players/{player_id}/sessions/{session_key}/end` | 结束并结算奖励 |

`start`、`pause`、`resume`、`rounds/resolve`、`continue`、`retry`、`end` 必须带每次操作唯一的 `Idempotency-Key`；网络超时重试必须复用原键和原请求体。`rounds/resolve` 请求体只能发送服务端期望的回合号：

```json
{ "client_round": 3 }
```

客户端不得提交技能 ID、目标槽位、伤害、治疗、冷却、死亡状态或奖励数量；服务端只接受 `client_round`，其余值全部由服务端计算。

接口统一返回 `{ "code": 200, "message": "...", "data": { ... } }` 形式；HTTP 成功不代表本地可自行结算，必须以 `data` 中的服务端状态为准。

### 7. Unity DTO（Newtonsoft.Json 示例）

以下字段名与后端 JSON 一一对应，字段可为空时必须使用 nullable 类型。项目若使用其他 JSON 库，保持相同的 `snake_case` 映射，不得把 Idle Adventure 字段改成 camelCase：

```csharp
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

[Serializable]
public sealed class SkillDefinitionDto
{
    [JsonProperty("skill_id")] public long skillId;
    [JsonProperty("skill_key")] public string skillKey;
    [JsonProperty("skill_name")] public string skillName;
    [JsonProperty("cooldown_rounds")] public int cooldownRounds;
    [JsonProperty("skill_multiplier")] public decimal skillMultiplier;
    [JsonProperty("damage_attribute")] public string damageAttribute;
    [JsonProperty("self_buff_preset_id")] public long? selfBuffPresetId;
    [JsonProperty("target_buff_preset_id")] public long? targetBuffPresetId;
    [JsonProperty("initial_cooldown_rounds")] public int initialCooldownRounds;
    [JsonProperty("effect_type")] public string effectType;
    [JsonProperty("target_side")] public string targetSide;
    [JsonProperty("target_mode")] public string targetMode;
    [JsonProperty("target_groups")] public List<List<int>> targetGroups;
    [JsonProperty("target_count")] public int? targetCount;
    [JsonProperty("include_self")] public bool? includeSelf;
    [JsonProperty("heal_formula")] public string healFormula;
    [JsonProperty("heal_base_value")] public decimal? healBaseValue;
    [JsonProperty("heal_multiplier")] public decimal? healMultiplier;
    [JsonProperty("skill_icon")] public string skillIcon;
}

[Serializable]
public sealed class SkillSlotDto
{
    [JsonProperty("slot_index")] public int slotIndex;
    [JsonProperty("skill")] public SkillDefinitionDto skill;
}

[Serializable]
public sealed class RuntimeSkillDto
{
    [JsonProperty("slot_index")] public int slotIndex;
    [JsonProperty("skill_id")] public long skillId;
    [JsonProperty("skill_name")] public string skillName;
    [JsonProperty("cooldown_remaining")] public int? cooldownRemaining;
    [JsonProperty("can_use")] public bool? canUse;
    [JsonProperty("initial_cooldown_rounds")] public int? initialCooldownRounds;
    [JsonProperty("effect_type")] public string effectType;
    [JsonProperty("target_side")] public string targetSide;
    [JsonProperty("target_mode")] public string targetMode;
    [JsonProperty("target_groups")] public List<List<int>> targetGroups;
    [JsonProperty("target_count")] public int? targetCount;
    [JsonProperty("include_self")] public bool? includeSelf;
    [JsonProperty("heal_formula")] public string healFormula;
    [JsonProperty("heal_base_value")] public decimal? healBaseValue;
    [JsonProperty("heal_multiplier")] public decimal? healMultiplier;
}

[Serializable]
public sealed class TargetResultDto
{
    [JsonProperty("slot_index")] public int? slotIndex;
    [JsonProperty("runtime_id")] public string runtimeId;
    [JsonProperty("target_name")] public string targetName;
    [JsonProperty("health_before")] public long? healthBefore;
    [JsonProperty("health_after")] public long? healthAfter;
    [JsonProperty("damage")] public long? damage;
    [JsonProperty("healed_amount")] public long? healedAmount;
    [JsonProperty("defeated")] public bool? defeated;
    [JsonProperty("defender_attribute")] public string defenderAttribute;
}

[Serializable]
public sealed class AnimationDto
{
    [JsonProperty("attack_direction")] public int attackDirection;
    [JsonProperty("attack_duration")] public decimal attackDuration;
    [JsonProperty("hit_flash_duration")] public decimal hitFlashDuration;
}

[Serializable]
public sealed class DamageFormulaDto
{
    [JsonProperty("base_damage")] public long baseDamage;
    [JsonProperty("skill_multiplier")] public decimal skillMultiplier;
    [JsonProperty("normal_buff_multiplier")] public decimal normalBuffMultiplier;
    [JsonProperty("attribute_buff_multiplier")] public decimal attributeBuffMultiplier;
    [JsonProperty("field_buff_multiplier")] public decimal fieldBuffMultiplier;
    [JsonProperty("attribute_restraint_multiplier")] public decimal attributeRestraintMultiplier;
    [JsonProperty("bonus_multiplier")] public decimal bonusMultiplier;
    [JsonProperty("attacker_attribute")] public string attackerAttribute;
    [JsonProperty("defender_attribute")] public string defenderAttribute;
}

[Serializable]
public sealed class BattleActionDto
{
    [JsonProperty("action_order")] public int actionOrder;
    [JsonProperty("round")] public int round;
    [JsonProperty("action_type")] public string actionType;
    [JsonProperty("actor_runtime_id")] public string actorRuntimeId;
    [JsonProperty("actor_name")] public string actorName;
    [JsonProperty("target_runtime_id")] public string targetRuntimeId;
    [JsonProperty("target_name")] public string targetName;
    [JsonProperty("skill_id")] public long? skillId;
    [JsonProperty("skill_name")] public string skillName;
    [JsonProperty("effect_type")] public string effectType;
    [JsonProperty("selected_group_seq")] public int? selectedGroupSeq;
    [JsonProperty("selected_slots")] public List<int> selectedSlots;
    [JsonProperty("total_damage")] public long totalDamage;
    [JsonProperty("total_healed")] public long totalHealed;
    [JsonProperty("targets")] public List<TargetResultDto> targets;
    [JsonProperty("damage")] public long damage;
    [JsonProperty("target_health_before")] public long targetHealthBefore;
    [JsonProperty("target_health_after")] public long targetHealthAfter;
    [JsonProperty("damage_formula")] public DamageFormulaDto damageFormula;
    [JsonProperty("applied_buffs")] public List<ActiveBuffDto> appliedBuffs;
    [JsonProperty("defeated_runtime_ids")] public List<string> defeatedRuntimeIds;
    [JsonProperty("animation")] public AnimationDto animation;
    [JsonProperty("log")] public string log;
}
```

战斗角色和会话可以直接使用下面的 DTO（奖励、掉落和背景字段按主文档其他章节继续映射）：

```csharp
[Serializable]
public sealed class ActiveBuffDto
{
    [JsonProperty("buff_key")] public string buffKey;
    [JsonProperty("display_name")] public string displayName;
    [JsonProperty("category")] public string category;
    [JsonProperty("source")] public string source;
    [JsonProperty("effect_type")] public string effectType;
    [JsonProperty("remaining_rounds")] public int remainingRounds;
    [JsonProperty("attack_modifier")] public long attackModifier;
    [JsonProperty("defense_modifier")] public long defenseModifier;
    [JsonProperty("speed_modifier")] public long speedModifier;
    [JsonProperty("normal_damage_multiplier")] public decimal normalDamageMultiplier;
    [JsonProperty("attribute_damage_multiplier")] public decimal attributeDamageMultiplier;
    [JsonProperty("affected_attribute")] public string affectedAttribute;
}

[Serializable]
public sealed class CombatantDto
{
    [JsonProperty("runtime_id")] public string runtimeId;
    [JsonProperty("definition_id")] public long definitionId;
    [JsonProperty("combatant_key")] public string combatantKey;
    [JsonProperty("combatant_name")] public string combatantName;
    [JsonProperty("icon")] public string icon;
    [JsonProperty("portrait_key")] public string portraitKey;
    [JsonProperty("model_key")] public string modelKey;
    [JsonProperty("side")] public string side;
    [JsonProperty("slot_index")] public int slotIndex;
    [JsonProperty("max_health")] public long maxHealth;
    [JsonProperty("current_health")] public long currentHealth;
    [JsonProperty("attack")] public long attack;
    [JsonProperty("defense")] public long defense;
    [JsonProperty("speed")] public long speed;
    [JsonProperty("attribute_type")] public string attributeType;
    [JsonProperty("effective_attack")] public long effectiveAttack;
    [JsonProperty("effective_defense")] public long effectiveDefense;
    [JsonProperty("effective_speed")] public long effectiveSpeed;
    [JsonProperty("defeated")] public bool defeated;
    [JsonProperty("active_buffs")] public List<ActiveBuffDto> activeBuffs;
    [JsonProperty("skill_states")] public List<RuntimeSkillDto> skillStates;
}

[Serializable]
public sealed class SessionDto
{
    [JsonProperty("session_key")] public string sessionKey;
    [JsonProperty("player_id")] public long playerId;
    [JsonProperty("stage")] public int stage;
    [JsonProperty("stage_name")] public string stageName;
    [JsonProperty("stage_presentation")] public StagePresentationDto stagePresentation;
    [JsonProperty("stage_attempt")] public int stageAttempt;
    [JsonProperty("round")] public int round;
    [JsonProperty("round_before")] public int? roundBefore;
    [JsonProperty("round_after")] public int? roundAfter;
    [JsonProperty("status")] public string status;
    [JsonProperty("paused")] public bool paused;
    [JsonProperty("stage_choice_pending")] public bool stageChoicePending;
    [JsonProperty("pending_next_stage")] public int? pendingNextStage;
    [JsonProperty("initiative_order")] public List<InitiativeEntryDto> initiativeOrder;
    [JsonProperty("actions")] public List<BattleActionDto> actions;
    [JsonProperty("friendlies")] public List<CombatantDto> friendlies;
    [JsonProperty("enemies")] public List<CombatantDto> enemies;
    [JsonProperty("logs")] public List<string> logs;
    [JsonProperty("pending_rewards")] public List<AssetRewardDto> pendingRewards;
    [JsonProperty("random_drops")] public List<RandomDropDto> randomDrops;
    [JsonProperty("settled_stages")] public List<int> settledStages;
    [JsonProperty("granted_rewards")] public List<AssetRewardDto> grantedRewards;
    [JsonProperty("items")] public List<InventoryItemDto> items;
    [JsonProperty("message")] public string message;
    [JsonProperty("idempotent_replay")] public bool idempotentReplay;
}

[Serializable]
public sealed class InitiativeEntryDto
{
    [JsonProperty("runtime_id")] public string runtimeId;
    [JsonProperty("combatant_name")] public string combatantName;
    [JsonProperty("side")] public string side;
    [JsonProperty("slot_index")] public int slotIndex;
    [JsonProperty("effective_speed")] public long effectiveSpeed;
}

[Serializable]
public sealed class StagePresentationDto
{
    [JsonProperty("background_code")] public string backgroundCode;
    [JsonProperty("background_name")] public string backgroundName;
    [JsonProperty("background_resource_key")] public string backgroundResourceKey;
}

[Serializable]
public sealed class AssetRewardDto
{
    [JsonProperty("reward_source")] public string rewardSource;
    [JsonProperty("reward_type")] public string rewardType;
    [JsonProperty("asset_id")] public long? assetId;
    [JsonProperty("reward_key")] public string rewardKey;
    [JsonProperty("reward_name")] public string rewardName;
    [JsonProperty("reward_icon")] public string rewardIcon;
    [JsonProperty("reward_category")] public string rewardCategory;
    [JsonProperty("reward_quality")] public string rewardQuality;
    [JsonProperty("item_id")] public long? itemId;
    [JsonProperty("item_key")] public string itemKey;
    [JsonProperty("display_name")] public string displayName;
    [JsonProperty("quantity")] public long quantity;
    [JsonProperty("grant_scope")] public string grantScope;
    [JsonProperty("fallback_applied")] public bool? fallbackApplied;
}

[Serializable]
public sealed class RandomDropDto
{
    [JsonProperty("drop_result_id")] public long dropResultId;
    [JsonProperty("pool_key")] public string poolKey;
    [JsonProperty("pool_name")] public string poolName;
    [JsonProperty("draw_index")] public int drawIndex;
    [JsonProperty("random_value")] public int randomValue;
    [JsonProperty("interval_start")] public int intervalStart;
    [JsonProperty("interval_end")] public int intervalEnd;
    [JsonProperty("reward_type")] public string rewardType;
    [JsonProperty("asset_id")] public long? assetId;
    [JsonProperty("reward_key")] public string rewardKey;
    [JsonProperty("reward_name")] public string rewardName;
    [JsonProperty("reward_icon")] public string rewardIcon;
    [JsonProperty("reward_category")] public string rewardCategory;
    [JsonProperty("reward_quality")] public string rewardQuality;
    [JsonProperty("quantity")] public long quantity;
}

[Serializable]
public sealed class InventoryItemDto
{
    [JsonProperty("item_id")] public long itemId;
    [JsonProperty("item_key")] public string itemKey;
    [JsonProperty("name")] public string name;
    [JsonProperty("display_name")] public string displayName;
    [JsonProperty("item_type")] public string itemType;
    [JsonProperty("quality")] public string quality;
    [JsonProperty("icon")] public string icon;
    [JsonProperty("quantity")] public long quantity;
}
```

`SessionDto` 的 `friendlies/enemies` 始终返回六项固定槽位，空槽位或没有对应角色的项为 `null`；Unity 应按 `slot_index` 建立映射，不得因为某槽位死亡而压缩列表或改变站位。创建和 GET 会话时 `round_before=null`，`round_after` 为当前服务端回合；只在回合结算响应中用这两个字段校验本次回合推进。`Combatant` 的 `portrait_key` 可为空，资源降级规则见 V136；`reward_quality/quality` 缺失或为空时使用通用品质样式。未知字段必须忽略，以便后端向前扩展；旧快照或旧幂等响应可能缺少新增字段，Unity 应把缺失的技能效果按 `DAMAGE + OPPONENT + SINGLE`、初始冷却按 `0`、目标组按空列表兼容处理，并对可空数值使用 nullable 类型。

典型 AOE 和回血动作（省略与本次播放无关的字段）如下。`targets` 顺序是服务端最终顺序，`runtime_id` 只在当前会话内有效；`defeated_runtime_ids` 与 `targets[].defeated=true` 的项一致：

```json
{
  "action_type": "SKILL",
  "effect_type": "DAMAGE",
  "actor_runtime_id": "F-0",
  "selected_group_seq": 1,
  "selected_slots": [0, 1],
  "total_damage": 240,
  "total_healed": 0,
  "targets": [
    {"slot_index": 0, "runtime_id": "E-0", "health_before": 300, "health_after": 180, "damage": 120, "healed_amount": 0, "defeated": false},
    {"slot_index": 1, "runtime_id": "E-1", "health_before": 240, "health_after": 120, "damage": 120, "healed_amount": 0, "defeated": false}
  ]
}
```

```json
{
  "action_type": "SKILL",
  "effect_type": "HEAL",
  "actor_runtime_id": "F-0",
  "selected_group_seq": null,
  "selected_slots": [4],
  "total_damage": 0,
  "total_healed": 120,
  "targets": [
    {"slot_index": 4, "runtime_id": "F-4", "health_before": 300, "health_after": 420, "damage": 0, "healed_amount": 120, "defeated": false}
  ]
}
```

### 8. Unity 回合播放流程（不可省略）

1. 进入关卡先调用 `stages/{stage}/enemies` 预览，再调用 `sessions/start`；用返回的 `session_key` 和 `friendlies/enemies` 建立运行时角色映射。
2. 点击“下一回合”只发送 `{client_round}` 和新的幂等键，等待 `rounds/resolve` 完整响应；不要在动画结束前重复发同一回合。
3. 按 `actions` 的 `action_order` 升序播放。通过 `actor_runtime_id` 找到施法者；`target_runtime_id` 只为旧单体兼容，新的 AOE/治疗必须遍历 `targets[]`。
4. 每个 `targets[]` 元素依次执行：定位 `runtime_id`/`slot_index` → 播放伤害或治疗特效 → 使用 `health_before`、`health_after` 更新血条 → 用 `damage` 或 `healed_amount` 播放数字 → `defeated=true` 或 `defeated_runtime_ids` 隐藏/标记死亡。
5. `effect_type=HEAL` 播放治疗特效和绿色数字；`DAMAGE` 播放受击、伤害数字和死亡；`BUFF` 只播放 Buff 图标/状态变化。`total_damage`、`total_healed` 只用于汇总显示，不替代逐目标结果。`animation.attack_duration`、`animation.hit_flash_duration`、`/config` 中同名时长均为秒（服务端返回小数），不是毫秒。
6. 当前回合所有 `actions` 播放完毕后，再以响应中的 `friendlies/enemies[].current_health`、`skill_states[].cooldown_remaining` 和 `active_buffs` 整体校正本地状态；这些阵容字段是整回合结束快照，不能在每个动作后提前覆盖中间动画状态。客户端不得自行递减冷却或移动槽位。
7. `selected_slots` 仅用于范围提示和调试，实际受击对象以 `targets[]` 为准。死亡目标不会被 Unity 自动换成其他格；后端在下一动作响应中决定新的目标组。
8. 当前回合所有动作播放完成后，根据 `status` 显示继续、重试、结束或失败界面；不要根据本地存活数量判定胜负。

可直接转换为客户端代码的伪代码如下（名称仅示意，服务端字段不可改名）：

```text
clientRound = session.round
response = POST rounds/resolve({client_round: clientRound}, idempotencyKey)
if response.code != 200: handleBusinessCode(response.code); return
data = response.data
assert data.round_before == clientRound
for action in sort(data.actions, action_order):
    actor = findRuntime(action.actor_runtime_id)
    playActor(action.action_type, action.animation, action.skill_name)
    if action.targets != null:
        for result in action.targets:
            target = findRuntime(result.runtime_id)
            playEffect(action.effect_type, target, result)
            setHealth(target, result.health_after)
            if result.defeated: markDefeated(target)
    else if action.target_runtime_id != null:
        target = findRuntime(action.target_runtime_id)
        playLegacySingleTarget(action, target, action.damage,
                               action.target_health_before, action.target_health_after)
        if action.defeated_runtime_ids != null and
           action.target_runtime_id in action.defeated_runtime_ids: markDefeated(target)
    applyBuffs(action.applied_buffs)
replaceRoster(data.friendlies, data.enemies)
replaceCooldownsAndBuffs(data.friendlies, data.enemies)
if data.status == RUNNING: nextRound = data.round_after
else: stopResolveAndShowTerminalState(data.status, data.granted_rewards)
```

回合响应中的 `friendlies/enemies` 是整回合结束快照，不能代替逐动作动画；动作播放完毕后再用它校正生命、死亡、技能冷却和 Buff。`targets=null` 或字段缺失才回退旧单目标字段，已返回空数组时不应凭旧字段补造目标；旧动作缺失 `effect_type` 时按 `DAMAGE` 播放。`GET /sessions/{session_key}` 只返回当前快照，通常 `actions=[]`，不会重播已经错过的动画；不确定的 `rounds/resolve` 结果必须先用原幂等键重试，不能新建键造成重复回合。

### 9. 断线、重连和幂等

- 请求超时、断网或 5xx：使用同一 `Idempotency-Key`、同一请求体重试；成功响应中的 `idempotent_replay=true` 表示服务端重放首次结果。
- 重连、切后台回前台、收到重复 WebSocket/推送或本地状态不确定时，调用会话查询接口覆盖本地快照；不要重新发送一个新的回合号。
- 服务端快照是唯一事实来源，包含技能冷却、目标规则、NPC 属性、当前生命和已死亡状态；客户端缓存丢失不会改变战斗结果。
- 旧快照没有 `effect_type/target_mode/target_groups` 时按单体伤害兼容；Unity 对缺失 `targets` 时可读取旧的 `target_runtime_id`、`damage` 字段，但新版本应优先 `targets[]`。
- 会话创建、进入下一关和失败重试都沿用会话冻结的技能/属性版本，但会重建该关运行时状态并按快照重新设置友方满血、敌方初始状态和技能初始冷却；暂停/恢复不重置，也不推进冷却。

`BUFF` 动作的 `damage`、`healed_amount` 通常为 0，权威变化在 `applied_buffs[]`；Unity 应按其中的 `buff_key`、`remaining_rounds` 和属性修正刷新图标/数值。自身 Buff 在动作结算后生效；目标 Buff 仅施加给结算后仍存活的目标。相同来源和 Buff 键再次施加时服务端刷新持续时间，回合末非持久 Buff 扣减，归零后移除；客户端不要自行叠加或计时。

技能接口返回 `skill_icon` 逻辑资源键（允许为 `null`），Unity 应通过资源绑定表加载；字段缺失或为空时可按 `skill_key` 使用本地默认图标，不得拼接 `Assets/...` 物理路径。会话 `skill_states[]` 和 `actions[]` 不返回图标；恢复旧会话时使用已缓存的绑定或默认图标，不用当前正式技能配置覆盖该会话的技能快照。

业务错误必须始终检查响应体 `code`，不能只看 HTTP 200：`6501` 未开放、`6502` 编队无效、`6503` 关卡不可用、`6504` 会话不存在、`6505` 状态冲突、`6506` 回合号冲突（先 GET 取最新 `round`）、`6507` 幂等键复用不同操作或请求体（禁止改 payload 重试）、`6508` 快照/配置无效（提示维护）、`6509` 并发修改（GET 后判断是否需要新操作）；`6511` 体力不足应刷新体力，`6512/6514` 为物理上限冲突且本次事务未成功，`6513` 应关闭加速入口，未授权 Buff `6515` 不应开放客户端入口。参数校验是 HTTP 400，认证失败是 401/403，旧接口路径是 404。

### 10. 客户端发布顺序与验收

必须严格按以下顺序上线：

1. 先发布能解析上述新增字段、逐目标播放、忽略未知字段并支持重连恢复的 Unity 版本；此版本默认仍按服务端返回结果播放旧单体技能。
2. 观察客户端覆盖率和错误日志达到运营要求后，才在管理端发布首个 `ORDERED_GROUPS`、`FIXED_SLOTS`、`RANDOM_N` 或 `HEAL` 技能。
3. 首次启用前验收：冷却为 0/非 0、两个技能槽优先级、`[0,1] -> [2] -> [3,4] -> [5]` 的死亡边界、固定格不转移、随机结果重试一致、治疗最低血量/同血量施法者/后列优先、满血和死亡不治疗、治疗封顶、Buff 延后生效、断线重连和幂等重放。
4. 以上客户端合同完成后，日后调整目标组、范围、初始冷却、治疗公式、动画时长和 Buff 引用只需管理端发布新版本，不需要再次修改 Unity；如果新增字段或新增表现资源，才按版本化客户端流程发布。

V134/V135 的实现与测试文件以仓库 `docs/idle-adventure-api.md`、`IdleAdventureApiResponses.java`、`IdleAdventureApiRequests.java`、`IdleAdventureCombatEngine.java` 和 `IdleAdventureController.java` 为准。V134/V135/V136/V137 迁移均已冻结，后续结构变化从 V138 开始。

## V136 NPC 头像、角色立绘与 Q版资源接入（当前权威）

V136 不改写 V134/V135。`V136__add_npc_portrait_resource_key.sql` 在 `npc_config` 增加可空的 `portrait_key`，使 NPC 的三类表现资源能够分别配置；该迁移不回填猜测值，不修改 NPC 身份、数值、拥有关系或奖励记录：

| 玩家接口字段 | 管理端含义 | Unity 物理目录 |
| --- | --- | --- |
| `icon` | 头像绑定键 | `Assets/_Project/Art/UI/角色/头像` |
| `portraitKey` | 角色立绘绑定键 | `Assets/_Project/Art/UI/角色/角色立绘` |
| `modelKey` | Q版绑定键 | `Assets/_Project/Art/UI/角色/Q版` |

`Assets/...` 只是 Unity 编辑器中的物理目录，按接入规范不得原样填写到管理端。页面对 `Assets/`、`Resources/`、反斜杠和常见扩展名只显示非阻断警告，后端不会据此硬阻断保存或发布；管理端和后端实际保存的是逻辑绑定键，例如：

```json
{
  "npcCode": "chief_manager",
  "icon": "chief_manager_avatar",
  "portraitKey": "chief_manager_portrait",
  "modelKey": "chief_manager_chibi"
}
```

Unity 继续使用当前 Inspector、Prefab、Scene 或 ScriptableObject 的直接引用方案，不需要把图片迁入 `Resources`。在已有 `roleIconBindings`、`roleModelBindings` 旁增加 `rolePortraitBindings`，并严格按接口值查找：

```text
roleIconBindings      <- icon
rolePortraitBindings  <- portraitKey
roleModelBindings     <- modelKey
```

禁止使用 `npcCode`、NPC 中文名称或文件名再次拼接目录。三组绑定初始化时应检查空 Key、重复 Key 和空资源；找不到某一项时只回退该类型的默认资源并记录 `npcCode + 字段名 + Key`，不能让单个资源缺失造成整个 NPC 列表、详情、建筑或战斗页面加载失败。Q版资源继续沿用项目现有 `roleModelBindings` 的资源类型，可以是 Sprite、Prefab 或已有封装，不由后端字段强制改变。

以下玩家响应统一包含三类资源键：

- `GET /api/npc/catalog`：`icon / portraitKey / modelKey`；
- `GET /api/npc/config/list`：`icon / portraitKey / modelKey`；
- `GET /api/npc/list/{playerId}`：`icon / portraitKey / modelKey`；
- `GET /api/npc/available/{playerId}`：`icon / portraitKey / modelKey`；
- 建筑生产响应中的 `settledNpc`：`icon / portraitKey / modelKey`；
- `GET /api/idle-adventure/players/{player_id}/npcs` 和首页 `available_npc_list[]`：本模块使用 snake_case，字段为 `icon / portrait_key / model_key`。
- Idle Adventure 首页 `formation.slots[]`、独立编队 HTTP 响应和 `IDLE_ADVENTURE_FORMATION_CHANGED` 的 `data.slots[]`：字段为 `icon / portrait_key / model_key`，空槽从 `definition_id` 开始的角色与资源字段均为空。
- Idle Adventure 关卡预览 `GET /api/idle-adventure/stages/{stage}/enemies` 的 `slots[].enemy`：返回 `icon / portrait_key / model_key`；敌人模板没有独立立绘配置，所以当前 `portrait_key=null`。
- Idle Adventure 会话响应 `friendlies[]/enemies[]`：字段为 `icon / portrait_key / model_key`；友方 NPC 返回三类键，敌方 `portrait_key` 当前为 `null`。

存量数据和灰度发布期间三个字段都应按可空资源键处理。`portraitKey/portrait_key` 为空时，Unity 先用 `icon` 头像资源兼容显示，头像也不可用时再用默认立绘，不能覆盖已经成功解析的 Q版资源。Unity DTO 增加 `portraitKey`；Idle Adventure 的玩家 NPC、编队槽位、关卡敌人和会话角色 DTO 都映射 `portrait_key`。旧 Unity 通常会忽略未知字段，不会因后端先增加 `portraitKey` 而失效，但旧客户端不会显示独立立绘。

管理端入口为“配置中心 → 基础资料 → NPC 基础资料”。展开 NPC 后分别填写“头像绑定键”“角色立绘绑定键”“Q版绑定键”，保存草稿、校验并发布正式版本。管理端只保存 Key，不上传或扫描 Unity 资源，也无法判断客户端包内是否真实存在对应图片。

推荐发布顺序：

1. 后端正常执行 V136 并部署接口，存量立绘键允许为空；
2. Unity 增加 `rolePortraitBindings`、DTO 字段、默认立绘、重复/缺失 Key 检查；
3. 美术把三类资源放入上述现有目录，Unity 将逻辑 Key 与真实资源逐项绑定并发布客户端；
4. 管理端补齐正式 NPC 的三个 Key，保存、校验和发布；
5. 重新请求 NPC 接口，验收图鉴、拥有列表、可入驻列表、建筑入驻/生产、Idle Adventure 选人及战斗入口；
6. 在微信小游戏或其他实际目标平台构建验证，不能只验证 Unity Editor。

新增一个从未进入客户端包的资源时，仍需先发布包含资源和绑定的 Unity 包；只改管理端不能让旧客户端凭空获得图片。相反，资源已随客户端交付且绑定 Key 不变时，美术移动文件一般可由 Unity GUID 保持引用，不需要修改后端。完整契约同步在项目文档 `docs/npc-visual-resource-api.md`。

### V136.1 Unity 可直接复制的 DTO

使用 Unity `JsonUtility` 时字段名必须与 JSON 完全一致。普通 NPC 接口使用 camelCase，Idle Adventure 使用 snake_case，两类 DTO 不要混用：

```csharp
using System;

[Serializable]
public sealed class NpcVisualDto
{
    public long npcId;
    public string npcCode;
    public string name;
    public string icon;
    public string portraitKey;
    public string modelKey;
}

[Serializable]
public sealed class IdleAdventureNpcVisualDto
{
    public long definition_id;
    public string combatant_key;
    public string combatant_name;
    public string icon;
    public string portrait_key;
    public string model_key;
}

[Serializable]
public sealed class IdleAdventureCombatantVisualDto
{
    public string runtime_id;
    public long definition_id;
    public string combatant_key;
    public string combatant_name;
    public string icon;
    public string portrait_key;
    public string model_key;
}

[Serializable]
public sealed class IdleAdventureFormationSlotVisualDto
{
    public int slot_index;
    public int column;
    public int row;
    public bool front_column;
    // 服务端空槽返回 null；JsonUtility 会保留默认值 0，正数才表示有角色。
    public long definition_id;
    public string combatant_name;
    public string quality;
    public string icon;
    public string portrait_key;
    public string model_key;
}

[Serializable]
public sealed class IdleAdventureStageEnemyVisualDto
{
    public long definition_id;
    public string combatant_key;
    public string combatant_name;
    public string icon;
    public string portrait_key;
    public string model_key;
}
```

使用 Newtonsoft.Json 时可以把编队槽位 `definition_id` 声明为 `long?`，也可以将 C# 属性命名为 `PortraitKey` 并用 JSON 属性标注 `portraitKey` 或 `portrait_key`，但不能让两种接口字段互相误映射。

### V136.2 Unity 三组非 Resources 绑定表

以下方案通过 Inspector 直接引用 `Assets/_Project/Art/UI/角色` 下的资源，不调用 `Resources.Load`，也不要求迁移目录。如果项目现有绑定组件名称不同，应把 `rolePortraitBindings` 加到现有组件，不再新建第二套注册表。

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class RoleSpriteBinding
{
    public string key;
    public Sprite asset;
}

[Serializable]
public sealed class RoleObjectBinding
{
    public string key;
    public UnityEngine.Object asset;
}

public sealed class RoleVisualRegistry : MonoBehaviour
{
    [SerializeField] private RoleSpriteBinding[] roleIconBindings = new RoleSpriteBinding[0];
    [SerializeField] private RoleSpriteBinding[] rolePortraitBindings = new RoleSpriteBinding[0];
    [SerializeField] private RoleObjectBinding[] roleModelBindings = new RoleObjectBinding[0];
    [SerializeField] private Sprite defaultRoleIcon;
    [SerializeField] private Sprite defaultRolePortrait;
    [SerializeField] private UnityEngine.Object defaultRoleModel;

    private Dictionary<string, Sprite> iconByKey;
    private Dictionary<string, Sprite> portraitByKey;
    private Dictionary<string, UnityEngine.Object> modelByKey;
    private readonly HashSet<string> reportedMissingKeys = new HashSet<string>();

    private void Awake()
    {
        iconByKey = BuildSpriteMap(roleIconBindings, nameof(roleIconBindings));
        portraitByKey = BuildSpriteMap(rolePortraitBindings, nameof(rolePortraitBindings));
        modelByKey = BuildObjectMap(roleModelBindings, nameof(roleModelBindings));
    }

    public Sprite ResolveIcon(string npcCode, string iconKey)
    {
        Sprite asset;
        if (TryGet(iconByKey, iconKey, out asset)) return asset;
        ReportMissingOnce(npcCode, "icon", iconKey);
        return defaultRoleIcon;
    }

    public Sprite ResolvePortrait(string npcCode, string portraitKey, string iconKey)
    {
        Sprite asset;
        if (TryGet(portraitByKey, portraitKey, out asset)) return asset;
        if (TryGet(iconByKey, iconKey, out asset)) return asset;
        ReportMissingOnce(npcCode, "portraitKey", portraitKey);
        return defaultRolePortrait;
    }

    public UnityEngine.Object ResolveModel(string npcCode, string modelKey)
    {
        UnityEngine.Object asset;
        if (TryGet(modelByKey, modelKey, out asset)) return asset;
        ReportMissingOnce(npcCode, "modelKey", modelKey);
        return defaultRoleModel;
    }

    private static bool TryGet<T>(Dictionary<string, T> map, string key, out T asset)
    {
        asset = default(T);
        return !string.IsNullOrWhiteSpace(key)
            && map.TryGetValue(key.Trim(), out asset);
    }

    private static Dictionary<string, Sprite> BuildSpriteMap(
        RoleSpriteBinding[] bindings,
        string bindingName)
    {
        var result = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        foreach (RoleSpriteBinding binding in bindings ?? new RoleSpriteBinding[0])
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.key) || binding.asset == null)
            {
                Debug.LogError($"{bindingName} 存在空 Key 或空资源");
                continue;
            }

            string key = binding.key.Trim();
            if (result.ContainsKey(key))
            {
                Debug.LogError($"{bindingName} 存在重复 Key：{key}");
                continue;
            }
            result.Add(key, binding.asset);
        }
        return result;
    }

    private static Dictionary<string, UnityEngine.Object> BuildObjectMap(
        RoleObjectBinding[] bindings,
        string bindingName)
    {
        var result = new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
        foreach (RoleObjectBinding binding in bindings ?? new RoleObjectBinding[0])
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.key) || binding.asset == null)
            {
                Debug.LogError($"{bindingName} 存在空 Key 或空资源");
                continue;
            }

            string key = binding.key.Trim();
            if (result.ContainsKey(key))
            {
                Debug.LogError($"{bindingName} 存在重复 Key：{key}");
                continue;
            }
            result.Add(key, binding.asset);
        }
        return result;
    }

    private void ReportMissingOnce(string npcCode, string fieldName, string key)
    {
        string identity = $"{npcCode}|{fieldName}|{key}";
        if (!reportedMissingKeys.Add(identity)) return;
        Debug.LogError($"NPC 表现资源未绑定：npcCode={npcCode}, field={fieldName}, key={key}");
    }
}
```

Q版如果当前使用 `Sprite` 或 `GameObject`，将 `UnityEngine.Object` 换成现有强类型即可，不要改变原有实例化逻辑。立绘降级顺序固定为：命中 `portraitKey` → 使用 `icon` 头像兼容显示 → 默认立绘；头像和 Q版各自独立降级，不能互相覆盖。

### V136.3 Inspector 配置步骤与示例 NPC 清单

1. 资源继续放在 `Assets/_Project/Art/UI/角色/头像`、`角色立绘`、`Q版`，保留 `.meta`。
2. 打开当前持有 `roleIconBindings`、`roleModelBindings` 的 Scene、Prefab 或 ScriptableObject。
3. 加入 `rolePortraitBindings` 和三个默认资源字段。
4. 三组列表为每个 NPC 各加一项，Key 与管理端逐字符一致，Asset 拖入对应资源。
5. 保存并应用 Prefab Override；启动时清除空 Key、重复 Key、空资源报错。
6. 构建微信小游戏或实际目标平台验证，不能只验证 Editor。

示例登记表（用于说明命名方式，实际 NPC 数量和编码以管理端 `NPC_CATALOG / NPCS` 当前正式配置为准；`chief_manager` 可能是运营新增内容，本文不保证数据库一定存在）：

| `npcCode` | 头像 Key | 立绘 Key | Q版 Key |
| --- | --- | --- | --- |
| `field_steward` | `field_steward_avatar` | `field_steward_portrait` | `field_steward_chibi` |
| `workshop_foreman` | `workshop_foreman_avatar` | `workshop_foreman_portrait` | `workshop_foreman_chibi` |
| `master_manager` | `master_manager_avatar` | `master_manager_portrait` | `master_manager_chibi` |
| `chief_manager` | `chief_manager_avatar` | `chief_manager_portrait` | `chief_manager_chibi` |

这些 Key 是推荐统一值；如果 Unity 当前已有稳定 Key，可以保留旧 Key，并在管理端填写完全相同的实际值，不要为了符合示例无意义改名。

### V136.4 联调验收表

| 验收入口 | 必须验证的资源字段 | 通过标准 |
| --- | --- | --- |
| `/api/npc/catalog` | `icon/portraitKey/modelKey` | 图鉴头像、详情立绘、Q版分别正确 |
| `/api/npc/config/list` | `icon/portraitKey/modelKey` | 旧图鉴入口兼容，未知字段不崩溃 |
| `/api/npc/list/{playerId}` | `icon/portraitKey/modelKey` | 玩家角色页三类资源正确 |
| `/api/npc/available/{playerId}` | `icon/portraitKey/modelKey` | 建筑可入驻角色列表正确 |
| 生产响应 `settledNpc` | `icon/portraitKey/modelKey` | 已入驻 NPC 展示正确 |
| `/api/idle-adventure/players/{player_id}/npcs` | `icon/portrait_key/model_key` | 选人页正确映射 snake_case |
| 首页 `available_npc_list[]` | `icon/portrait_key/model_key` | 首页候选角色正确 |
| 首页/HTTP/WebSocket 编队 `slots[]` | `icon/portrait_key/model_key` | 编队槽位与空槽安全映射 |
| 关卡预览 `slots[].enemy` | `icon/portrait_key/model_key` | 当前敌方立绘为空并安全降级 |
| 战斗会话 `friendlies[]` | `icon/portrait_key/model_key` | 友方头像、立绘、Q版正确 |
| 战斗会话 `enemies[]` | `icon/portrait_key/model_key` | 当前敌方立绘为空并安全降级 |

联调还必须覆盖：空立绘键回退头像、三种 Key 不存在、重复绑定、空资源、大小写不一致、修改正式配置后重新拉取、旧客户端忽略新增字段、实际目标平台构建。任何一种缺失都只能影响相应表现资源，不能导致白屏、空引用、整页加载失败或错误替换其他 NPC 的资源。

---

## 最新接口补充：装备手选突破与一键升级 V2（V138/V139/V140/V141）

本节覆盖此前文档中“突破材料固定为 1 级”的旧说明。旧 V1 接口保持原行为；只有下列 V2/手选接口允许同款任意等级材料。

### 规则与管理端

管理端路径：`配置中心 → 玩法与成长 → 装备成长（EQUIPMENT_GROWTH）`。`equipment_breakthrough_config.sameEquipmentCount` 在界面显示为“突破材料框数量”，每件材料固定占一个框，不按等级折算。材料必须与目标同一 `equipmentId`，可为 1/5/20 等任意等级；成功后材料、已投入金币、资源和突破材料全部永久消耗，`refundPolicy=NONE`。发布校验要求等级属性、金币曲线、额外资源和突破点完整；配置变化会使 `planFingerprint` 失效。

服务端最终校验材料：当前玩家、同款、`WAREHOUSE`、未锁定、未穿戴、未消费、不是目标装备。客户端只传实例 ID，不能信任客户端传来的等级或费用。

### 单次突破（`contractVersion=2`）

```http
GET  /api/equipment/{playerEquipmentId}/upgrade-preview
GET  /api/equipment/{playerEquipmentId}/breakthrough-materials?page=1&pageSize=50
POST /api/equipment/breakthrough-preview
POST /api/equipment/breakthrough
```

`upgrade-preview` 返回 `isBreakthrough`、`requiredMaterialCount`、`materialCandidateCount`、`materialCandidates`、`safeRecommendedMaterialIds`、`completeRecommendedMaterialIds`、`recommendationContainsHighLevel`、`materialPolicy`、`refundPolicy` 和 `planFingerprint`。普通升级的材料框数量为 0；Unity 必须按该值动态生成框。安全推荐只含 1 级材料；完整推荐排序为等级低、获得时间早、实例 ID 小，高等级推荐必须二次确认。

选择预览请求：

```json
{
  "playerEquipmentId": 3001,
  "expectedCurrentLevel": 19,
  "targetLevel": 20,
  "materialPlayerEquipmentIds": [3101, 3102, 3108],
  "planFingerprint": "<预览返回的64位小写SHA-256>"
}
```

响应返回每件材料的 `level/hasCultivationInvestment` 快照、`highLevelMaterialCount`、`requiresHighLevelConfirmation`、`materialSelectionFingerprint` 和不可退款警告。执行请求必须携带：

```http
Idempotency-Key: <标准UUID>
```

并原样回传两个指纹、材料 ID、`confirmIrreversibleMaterialConsumption=true`；只要有 `level>1` 材料，还必须 `confirmHighLevelMaterialLoss=true`。事务锁顺序是玩家 → 幂等历史 → 目标 → 配置 → 材料 ID 全局升序 → 背包物品 ID 升序，任何失败全部回滚。同一玩家同一幂等键同载荷重放首次响应，改载荷返回冲突。

### 一键升级 V2（非突破路径全路径原子）

当前 V2 含突破点的路径整体拒绝 `6210`，不自动截断；非突破路径可以执行，`materialGroups=[]`。以下含 20/40 级材料组示例仅记录历史 V139 合同，当前不可据此跨突破，历史完成态批次仍可按原幂等键重放。目标允许保持穿戴；不要将“材料必须未穿戴”误用于目标。

```http
GET  /api/equipment/{playerEquipmentId}/auto-upgrade-v2-plan?targetLevel=50
GET  /api/equipment/{playerEquipmentId}/auto-upgrade-v2-materials?page=1&pageSize=50
POST /api/equipment/auto-upgrade-v2-preview
POST /api/equipment/auto-upgrade-v2
```

计划返回 `executionMode=ALL_OR_NOTHING`、全路径费用、`planFingerprint`、逐级 `upgradeSteps` 和 `materialRequirements[{targetLevel,requiredMaterialCount}]`。选择预览的 `materialGroups[]` 必须恰好覆盖每个突破目标等级，普通等级不能建组，每组数量必须精确匹配，实例 ID 不能跨组或组内重复：

```json
{
  "playerEquipmentId": 3001,
  "expectedCurrentLevel": 1,
  "targetLevel": 50,
  "planFingerprint": "<计划指纹>",
  "materialGroups": [
    {"targetLevel":20,"materialPlayerEquipmentIds":[3101,3102]},
    {"targetLevel":40,"materialPlayerEquipmentIds":[3108,3110,3111]}
  ]
}
```

执行时必须回传整体 `materialSelectionFingerprint`、分组材料和两个确认值，并带新的标准 UUID。全路径资源、目标等级、材料状态、批次和逐级审计要么全部成功要么全部回滚；成功 `outcome` 固定为 `TARGET_REACHED`，响应按 `targetLevel` 返回材料快照。V2 与旧 `/api/equipment/auto-upgrade` 分离，旧接口仍只自动消耗 1 级材料。

### V1/V2 兼容合同（Unity 必须遵守）

| 合同 | 固定调用链 | 材料规则 | 执行语义 |
| --- | --- | --- | --- |
| 一键升级 V1 | `GET /api/equipment/{playerEquipmentId}/auto-upgrade-preview` → `POST /api/equipment/auto-upgrade` | 当前不消耗突破材料 | 尽可能完成可支付普通等级前缀，遇突破点停止；第一步即突破时返回 `6210` |
| 一键升级 V2 | `GET /api/equipment/{playerEquipmentId}/auto-upgrade-v2-plan` → `POST /api/equipment/auto-upgrade-v2-preview` → `POST /api/equipment/auto-upgrade-v2` | 仅非突破路径可执行，`materialGroups=[]` | `ALL_OR_NOTHING`；包含突破点时整体拒绝 `6210`，不自动截断 |

两版 `planFingerprint` 使用不同协议域：相同装备、相同起止等级和相同配置下生成的 V1/V2 指纹仍不相同。V1 预览值只能提交给 V1 执行；V2 计划值只能提交给 V2 选择预览和执行。不得把任一版本的请求体、计划指纹或材料字段投递到另一版本路由；跨路由混用会以 `6209` 在扣费前拒绝，不会改变金币、材料或装备等级。Unity 不能依赖 JSON 解析器“忽略未知字段”来混用合同。

每次新的升级操作必须生成一个从未使用过的标准 UUID `Idempotency-Key`，V1/V2 之间也不得复用。同一路由、同一请求体发生网络超时时，才使用原 UUID 重试；收到 `6208` 后废弃原 UUID，重新执行该版本的完整预览流程。V1 或 V2 任一执行成功（含幂等重放确认首次成功）后，必须同时清除本地两版旧预览、`planFingerprint`、`materialSelectionFingerprint` 和材料选择，重新请求仓库、目标详情、余额及下一步准备使用的版本预览，不能继续提交成功前缓存的另一版计划。

本次协议域隔离版本部署前已经生成、但尚未成功执行的 V2 `planFingerprint` 会安全失效：新版提交返回 `6209`，不扣金币、不消耗材料、不改变装备等级。Unity 应重新拉取 V2 计划、候选和选择预览，并以新的 UUID 执行。V1 指纹算法保持不变；部署前已经成功的 V2 请求继续支持原 UUID 与完全相同载荷幂等重放首次响应，不受此次指纹域调整影响。

### 错误码与客户端刷新

| 业务码 | 含义 | 处理 |
| --- | --- | --- |
| `6210` | 单次突破条件不满足，或普通升级/V2 路径需要先手动突破 | 重新拉预览；到门槛后使用单次手动突破，不反复重试自动升级 |
| `6211` | 分组或材料框数量不匹配 | 清空选择重新预览 |
| `6212` | 材料归属/状态/同款校验失败 | 重新拉候选 |
| `6213` | 永久消耗或高等级确认缺失 | 弹确认框 |
| `6214` | 预览后材料快照变化 | 重新候选→预览 |
| `6208` | 同幂等键载荷变化 | 换新 UUID |
| `6209` | 计划指纹变化 | 重新拉计划 |
| `6204` | 资源不足 | 只展示缺口，不扣部分资源 |

`6210/6211/6213` 返回 HTTP 400；`6212/6214/6208/6209` 返回 HTTP 409。成功或重放后 Unity 必须重新请求仓库、目标详情和余额，不能只在本地改等级。

### Unity 接入顺序和可配置边界

Unity 使用手选接口时依次执行：预览 → 按 `requiredMaterialCount` 动态建框 → 分页候选 → 选择预览并保存两个指纹 → 高等级危险确认 → 使用本次操作的新 `Idempotency-Key` 执行 → 成功后刷新仓库/详情/余额。指纹不自行计算；同一路由同载荷网络超时不能换 UUID 重复提交。以后运营只调整突破等级、材料框数量和费用，Unity 不写死 3 个框、20/40/60 等等级或材料等级。

### Flyway 上线门禁（V152 阶段记录）

V138 创建单次突破幂等请求、任意等级候选索引和材料等级/培养状态快照，checksum=`1435232508`，已冻结；V139 以前向方式扩展一键升级批次并兼容 `contract_version=1/2`，checksum=`1413213846`；V140 再以前向方式加固单次突破请求 `PROCESSING/COMPLETED` 生命周期和材料培养快照一致性，checksum=`1806305011`；V141 显式要求 V2 材料分组 JSON、材料选择指纹和手选流水双指纹非空，checksum=`-1149433050`；V142 扩展全物品五档品质、存量一次性配置及 NPC/碎片一致性，checksum=`2110921250`；V143 活动券跨活动永久通用和统一钻石单价，checksum=`1356587650`；V144 男女选择、基础 NPC 发放和碎片出售基础合同，checksum=`107305189`；V145 NPC 碎片结算资源与数量冻结，checksum=`-716050190`；V146 追加来源数量冻结，checksum=`1808794590`；V147 预检活动券统一单价，checksum=`1724824817`；V151 建立 NPC 手动突破请求，checksum=`-1451868567`；V152 前向补强突破请求约束和一键升级停止结果。V138～V152 禁止修改或 `flyway repair`。本节形成时的上线门禁要求串行执行编译、单次/V1/V2 定向测试、V138～V152 合同测试及隔离 MySQL 迁移测试，并验证品质、性别、碎片合同、NPC 突破约束和券跨活动审计证据，再检查 `git diff --check`。当时最高迁移为 V152，下一份数据库迁移为 V153；当前源码边界见文首和文末 V153～V155 章节。

---

## 历史接口补充：V142 全物品品质与 Unity 接入

V142 将 NPC/装备使用的 `COMMON / FINE / EXCELLENT / EPIC / LEGENDARY` 五档稳定编码扩展到全部物品，包括货币、基础资源、产物、NPC 碎片和活动抽奖券。中文展示固定为普通、优秀、稀有、史诗、传说；Unity 不得按名称、物品类型或本地表推测品质。

### 1. 管理端一次性配置

- 入口：`配置中心 → 基础资料 → 物品基础资料`。
- V142 不自动猜测存量物品品质；升级后 `quality=null / qualityLocked=false` 表示“待运营确认”，不是默认普通。
- `CONFIG_ADMIN` 必须为所有存量物品选择品质，然后执行“校验 → 保存草稿 → 发布”。任一缺失或非法值会阻断整次发布。
- 保存草稿不会锁定；首次正式发布写入品质后立即永久锁定，后续 `qualityLocked=true`，不能改档或清空。需要新的品质语义时新建物品编码并停用旧编码。
- NPC 专属碎片必须与关联 NPC 品质一致。系统自动创建碎片时继承 NPC 品质；已有碎片不一致时阻断物品或 NPC 发布。

该配置在每个独立生产数据库只需做一次。品质写入正式库后，后端重启、扩容或日常配置发布都不需要重新配置；开发库或预发布库的配置不会自动进入生产库。也就是说，上线前确实需要你手动确认一次，但不是每次上线或启动都重复填写。

### 2. Unity 响应字段

```http
GET /api/inventory/list/{playerId}
```

```json
{
  "code": 200,
  "data": [
    {
      "itemId": 1,
      "itemKey": "gold",
      "name": "金币",
      "itemType": "CURRENCY",
      "quality": "COMMON",
      "icon": "gold",
      "quantity": 100
    }
  ]
}
```

登录/用户资料、签到领取、邮件领取、广告领取、建筑生产和挂机冒险等复用背包响应的接口，同样在 `data.items[].quality` 返回品质。金币商城、钻石商城商品/成交/历史响应返回 `rewardQuality`，资源出售返回 `sourceItemQuality` 与 `targetItemQuality`，历史活动抽奖券出售订单（如存在）返回 `ticketItemQuality` 与 `targetItemQuality`，抽卡档位返回 `costItemQuality`。系统奖励模板和奖励活动详情返回 `rewardQuality`、`duplicateFragmentQuality`。`GET /api/ops/config/dictionaries` 的 `items[]`、`productionItems[]`、`equipment[]` 和 `npcs[]` 也返回 `quality`。

旧 Unity 可以忽略新增 JSON 字段；新 Unity 应使用服务端品质编码决定边框、颜色和排序。预发布阶段若仍出现 `null`，按普通样式兜底并记录诊断日志，不能因未知枚举导致整个背包解析失败。

### 3. 快照、迁移与上线门禁

V142 为 `item_config` 增加可空 `quality` 和五值 CHECK，并前向放宽历史任务物品快照约束：旧 ITEM 空品质快照保持可读，V142 后新发布任务可保存物品品质。品质不写入 `player_item`，背包通过 `item_config` 读取不可变正式品质，无需批量改写历史商城、签到、邮件或任务记录；品质一旦首次正式发布即永久冻结。

V150 阶段上线顺序记录：停止旧节点写入 → 备份数据库 → 只启动一个新版本节点执行 Flyway 至 V150 → 由 `CONFIG_ADMIN` 完成全物品一次性品质配置并正式发布 → 复核所有物品 `qualityLocked=true` 及 NPC/碎片同品质 → 配置 NPC 碎片出售合同并确认冻结字段 → 验收 NPC 合成、背包字段和 Unity 五档样式 → 启动其余节点并开放流量。

V142 checksum 已由仓库固定测试锁定为 `2110921250`；V143 checksum=`1356587650`、V144 checksum=`107305189`、V145 checksum=`-716050190`、V146 checksum=`1808794590`、V147 checksum=`1724824817`、V148 checksum=`-1250904303`、V149 checksum=`-292257608`、V150 checksum=`601341822`、V151 checksum=`-1451868567`、V152 checksum=`571652493`，均执行后不得修改脚本或使用 `flyway repair` 接受漂移。V146 承接来源数量冻结，V147 预检活动券统一单价，V148 建立 NPC 首次碎片合成审计，V151 建立 NPC 手动突破请求，V152 完成前向约束补强；本节形成时最高迁移为 V152，下一份数据库迁移为 V153。

V147 checksum=`1724824817` 为上一版本活动券预检；V148 checksum=`-1250904303` 为 NPC 首次碎片合成历史迁移；V151 checksum=`-1451868567` 为 NPC 手动突破历史迁移，V152 checksum=`571652493` 为其前向约束补强；本节形成时仓库最高迁移为 V152。V148、V151、V152 的真实 MySQL 集成测试需要配置测试数据库环境变量，生产执行后必须验证重复启动零变更。

## V148：NPC 首次专属碎片合成与图鉴自动入口

### 玩家接口

```http
GET  /api/npc/{npcId}/synthesis-preview
POST /api/npc/{npcId}/synthesize
Authorization: Bearer <player-token>
Idempotency-Key: <UUID>          # 仅 POST，ASCII [A-Za-z0-9][A-Za-z0-9_-]{0,63}
```

POST 请求体只有 `{ "planFingerprint": "<预览返回值>" }`。服务端原子校验 NPC 合成配置、玩家所有权、碎片余额和指纹后，扣除专属碎片并创建 `level=1/state=IDLE` NPC；重复成功请求使用同一幂等键只返回首次事实，不会重复扣除或创建。错误码为 `6110` 未开放、`6111` 已拥有、`6112` 碎片不足、`6113` 预览计划变化、`6114` 幂等键载荷冲突、`6115` 配置异常。完整字段、锁顺序和重试规则见仓库 `docs/npc-synthesis-api.md`。

### 图鉴自动获取途径

`GET /api/npc/catalog` 和 `GET /api/npc/{npcId}/acquisition-guide` 会根据 `NPC_CATALOG / NPCS` 中的 `synthesisEnabled` 自动生成虚拟渠道，不需要在 `NPC_ACQUISITION / CHANNELS` 再配一行：`channelType=SYNTHESIS`、`routeKey=NPC_SYNTHESIS`、`routeParams={npcId}`。该开关独立于 `obtainable`，可关闭抽卡/签到新发放而保留合成；已拥有 NPC 永远显示 `OWNED`。碎片不足仍可进入预览，`canSynthesize=false` 由预览控制确认按钮。图鉴批量一次读取待合成 NPC 的碎片余额，不逐 NPC 请求预览。

### Unity 接入

Unity 将 `NPC_SYNTHESIS` 映射到 NPC 合成预览页，使用 `routeParams.npcId` 定位目标；不把 routeKey 当作场景名执行。进入页面后先请求预览，成功或幂等重放后刷新图鉴、NPC 列表和背包；网络超时仅用同一幂等键重试完全相同载荷，指纹变化或 `6113` 必须重新预览并换新 UUID。旧渠道快照不会被自动改写。

### Flyway 边界

V148 创建首次 NPC 碎片合成所需的配置字段、幂等/审计结构（以仓库迁移脚本及合同测试为准）；V147 及以前脚本和校验和冻结，禁止回改或 `flyway repair`。上线前先单节点执行 Flyway，再验证 `NpcSynthesisV148MigrationContractTest` 和合成定向测试，最后滚动部署 Unity 路由支持版本。

## V149/V150：未上线通用食物清理

V149 `V149__remove_retired_food_item.sql` 已在当前业务库成功执行，真实删除通用物品 `food`、命中的配置与库存以及开发期重放数据，不补偿，也不替换成小麦、金币或其他资源。已删数据仅能从迁移前备份恢复，本次不是尚未执行的方案。V149 checksum 固定为 `-292257608`；只读验证确认全部 150 份 SQL 与已执行历史 checksum 匹配，禁止改写 V149 或 `repair`。V1、V6、V9、V71 等历史迁移保持原文；V71 的历史说明不代表 V149 后仍保留通用 `food`。

抽卡删除 `food` 奖项后重排剩余槽位并校准 `card_count`，空池停用；单抽、五连抽次数保持既有规则。默认 `MONTHLY` 第 5、11、17、23、29 天删除后为空，计划已停用，须管理端补齐合法奖励并重新发布；默认 `NEWBIE` 首日仍有其他奖励，保留且不受影响。物品目录前后端禁止新增或恢复 `food`；其他物品的品质与首次发布永久冻结规则保持不变。

V150 `V150__harden_retired_food_cleanup.sql` 只做前向补强：停止生产快照已为空的活跃规则并同步等级默认配方指针，保留其他产物库存与非空冻结快照；停用无任何奖励渠道的任务、空礼包和空赠礼，只取消未完成的空 `REWARD` 投递活动，保留无附件 `SYSTEM_MAIL`；删除无奖励、无活动加送资格、无发放流水且尚未领取的开发期任务实例；定点净化挂机奖励数组，不删除会话、命令或掉落批次，保留非食物待领奖励及纯审计快照/哈希；停用候选总概率不等于一亿的随机池，等待运营重配；补拦 `food:12` 等生产字符串草稿并取消定时发布。V149 已删除关系行，V150 无法反推原食物关联，采用剩余快照/配置完整性兜底，可能同时命中由其他开发操作形成的同类空配置；不伪称精确恢复已删关联，不重分配概率、不替换奖励。

当前已执行 V150 仅净化已知奖励数组，不删除 session、command 或 dropBatch：处理 `state_json.stageRewards/randomDropPools` 及开始/命令响应中的 `pendingRewards/randomDrops/grantedRewards/items`，保留未命中元素的原顺序、会话状态及命令幂等键，其他 pending 奖励仍可退出结算；既有 `drop_batch.config_snapshot_json` 与哈希保持审计原文。重要限制：checksum `601341822` 版本按数组元素内任意精确 JSON 字符串 `food` 匹配；随机池整体命中即移除整池，不重分配概率。它尚未收窄到 `itemKey`、`ITEM + rewardKey` 或 `fallbackItemKey`，因此名称、图标或池名恰为 `food` 也可能误命中。身份字段收窄仅存在于未完成候选，不得宣称当前业务库已有该保护。

自定义新手冻结版本若缺档位，父计划停用后不进入 overview，claim/makeup 返回 `6001`；登录仍可保留或按首次登录时间绑定版本，绑定过程不读奖励明细。版本、有效区间、指针和实例不得删除。重新发布不会修复旧账号的冻结版本；重新启用后，旧账号当前空档位不可领取，直接领取返回 `6004`，在扣费、写领取记录和发奖之前结束，顺序模式也不自动跳档。此类开发期旧账号需保留审计后另行明确处置，不能擅自换绑、替换奖励或补偿。

Unity 无需修改接口或 DTO，按服务端返回的背包、奖励、卡面和生产列表展示即可；若硬编码了 `food` 图标、计数栏或物品 ID，应移除这些分支。本文当前示例使用 `wheat / 小麦 / RESOURCE`，`itemId=200000` 仅为标准初始化参考值；V71 从编号段动态分配，既有数据库可能不同，必须以服务端实际 `itemId` 为准，不能将旧 ID 5 当作小麦。

当前业务库 V149～V152 均已成功执行；V151 checksum 为 `-1451868567`，V152 checksum 为 `571652493`，失败迁移为 0。源文件已恢复为实际执行原文，V151/V152 与数据库历史 checksum 一致。

此前 checksum `1444213355` 的候选实现通过迁移/物品相关 115 项及签到 42 项测试，但这些结果不代表当前恢复版本最终验证通过。候选保存在 `tools/pending-migrations/retired-food-reviewed-candidate.sql`，位于 Flyway 扫描目录之外并带有未完成说明；严格身份判断用例仍代表待完成的改进。当前 V150 冻结值已补入迁移合同测试，但恢复后的测试未重跑。

另外 7 项业务集成场景中 5 项通过、2 项失败；两项失败已在清理前的 target 148 重现，分别涉及玩家等级曲线不兼容和建筑任务经济快照无效，不是本次 food 迁移引起。管理端 69 项测试、build 和类型检查通过，大 chunk 提示为警告。

V151 已由开发库执行并冻结，checksum=`-1451868567`；V152 已成功执行并冻结，checksum=`571652493`。此前本地脚本变更造成的 checksum mismatch 已通过恢复 V151 原文解决，禁止使用 `flyway repair`。

V152 阶段已执行迁移最高为 V152，当时下一份迁移为 V153。V148～V152 及其他历史脚本保持冻结，禁止修改或使用 `flyway repair` 掩盖漂移。

## V151：NPC 手动突破统一规则（历史补充）

V151 新增 `npc_breakthrough_request` 幂等请求/响应快照表，并给 `npc_upgrade_record` 增加可空的 `breakthrough_request_id` 审计关联；不修改 V7、V85 或其他已执行迁移。NPC 与装备现在统一遵守：普通升级每次只升 1 级，目标等级命中启用突破配置时必须停止；一键升级只能提交当前突破点之前的连续普通升级前缀，不能自动扣突破资源；突破成功后才允许继续升级。

NPC 突破使用该 NPC 专属 `NPC_FRAGMENT` 碎片，同时扣除突破目标等级的金币，接口为 `GET /api/npc/{playerNpcId}/breakthrough-preview` 与 `POST /api/npc/breakthrough`。预览和成功响应均返回 `contractVersion=1`；预览指纹、当前等级和目标等级必须原样提交，执行必须使用标准 UUID `Idempotency-Key`；请求、资源、等级、流水和响应快照在同一事务中完成，超时重试只重放首次成功结果。

Unity 不需要写死突破等级或碎片数量：普通升级收到 `6109` 时打开突破页；一键升级读取 `outcome=BREAKTHROUGH_REQUIRED` 和 `blockedLevel`；突破预览成功后按返回的 `fragmentCost/goldCost/planFingerprint` 展示确认，成功后刷新 NPC 详情、背包和升级按钮状态。

## V152：Flyway checksum 修复与 NPC 突破约束补强

V151 的已执行原文已恢复，固定 checksum=`-1451868567`；V152 `V152__harden_npc_manual_breakthrough.sql` 前向补强完成态响应、正数碎片成本、突破请求与一键批次互斥关联，并允许 `BREAKTHROUGH_REQUIRED` 停止结果。V152 先做只读预检，发现历史审计快照不合规会安全失败，不修改玩家资产。V1→V152 隔离 MySQL 迁移、校验和验证和 V152 重复迁移零变更已通过；MySQL 历史 `VALUES()` 弃用提示属于警告，不是失败。

本次还下线了活动券出售/到期策略玩家运行时路由，以及头像图片上传/对象上传遗留实现；历史表、Flyway 迁移和审计枚举保留，Unity 不再调用活动券出售接口，固定头像继续按原目录协议使用。

## V153/V154：充值档位管理与 Unity 接入（历史补充）

V153 新增人民币充值档位的版本、在售金额唯一约束和目录修订号；V154 不改写 V153，只以前向迁移收紧在售档位的非空、币种、金额、奖励和版本约束。两份迁移固定 checksum 分别为 `-1761361088`、`125577309`。本节形成时源码最高版本为 V155，当时下一份迁移从 V156 开始；当前已推进至 V157，下一份数据库迁移从 V158 开始。部署目标库时须核对 `flyway_schema_history` 的版本、成功状态和 checksum，禁止修改已执行脚本或使用 `flyway repair` 掩盖漂移。

### 管理端档位接口

以下接口属于运营端 `/api/ops/**`，使用运营 Bearer token，Unity 玩家端不调用：

```http
GET  /api/ops/payments/recharge-tiers
POST /api/ops/payments/recharge-tiers/preview
POST /api/ops/payments/recharge-tiers/publish
Authorization: Bearer <operator-token>
```

列表和预览需要财务读取权限，发布仅允许 `CONFIG_EDITOR` 或 `CONFIG_ADMIN`，不需要第二人复核。列表统一响应的 `data` 为 `{ "revision": 2, "tiers": [...] }`，同时包含在售和停用历史；每项有 `productCode/tierKey/tierVersion/name/priceFen/currency/rewardItemKey/rewardAmount/sortOrder/enabled/createTime/updateTime`。管理端默认只看在售，可在“在售”“已停用”“全部”之间切换；顶部“在售档位”和“已停用”统计可点击，分别切换到对应分类。超过 9 档时每页 9 档仅在前端分页，切换筛选会回到第一页，GET 接口仍一次返回完整目录。

预览和发布使用相同的变更字段，发布另须提供非空且最多 500 字符的 `reason`：

```json
{
  "action": "REPLACE",
  "productCode": "<当前在售商品编码>",
  "priceFen": 9800,
  "expectedRevision": 2,
  "reason": "调整充值金额"
}
```

`action=ADD` 不传 `productCode`，`REPLACE` 和 `DISABLE` 必须传当前在售的 `productCode`；`DISABLE` 不传 `priceFen`。新增或调整的金额须大于零、不超过 `2147483647` 分且为 10 分的整数倍，固定 `currency=CNY`、`rewardItemKey=diamond`，服务端生成 `rewardAmount=priceFen/10`，即 ¥1 = 10 钻石。相同金额不能同时有两个在售档位；调整金额不能与原金额相同。可选 `sortOrder` 为 `0..1000000`。预览返回 `data={revision,before,after}`，新增的 `before=null`；预览不修改目录，发布会在同一事务中核对 `expectedRevision`、切换版本、递增修订号并记录原因与审计。过期修订号需重新加载目录和预览。

调整会为同一 `tierKey` 创建新 `tierVersion` 和新 `productCode`，旧版停用；停用只阻止新购，不物理删除。旧订单即使尚未付款，也继续按创建时冻结的价格、币种与到账钻石履约。Unity 打开充值页必须重新调用 `GET /api/payment/product/list`，使用返回的当前 `productCode` 展示和建单；被停用的旧编码若新建订单被拒绝，应刷新目录并让玩家重新确认金额。完整的 Unity 登录、幂等建单、JavaScript 桥接 `wx.requestPayment`、查单和重进恢复顺序见本文件“10.9 支付下单流程”。

真实收款仍须先核准小游戏获批渠道及验签模式：本模块面向普通微信支付 API v3 JSAPI 和平台证书模式，不提供米大师虚拟支付或 `PUB_KEY_ID_...` 公钥模式。微信成功通知与主动查单的成功响应由服务端验签、校验交易后才推进发货；Unity 仅在订单 `DELIVERED` 后刷新权威余额，不自行发钻石。

## V155：系统奖励排序缺口修复

V137/V149 清理废弃资产时删除了部分 `system_reward_package_item`，可能使同一奖励模板的 `sortOrder` 出现断号；配置中心编辑或保存整份模板时曾因此提示“系统奖励排序必须从1开始连续递增”。V155 `V155__compact_system_reward_item_order.sql` 已在本机 `game_db` 执行，固定 checksum=`-618690687`，按模板原有顺序将剩余资产重新编号为 `1..N`，不改变资产、数量或已冻结的投递活动快照。其他环境以 `flyway_schema_history` 的 V155 `success` 和 checksum 为准；本节形成时后续迁移从 V156 开始，当前边界为 V157、下一迁移从 V158 开始。禁止修改已执行的 V155 或使用 `flyway repair`。

管理端“配置中心 → 活动与奖励 → 系统奖励 → 奖励模板”若仍显示排序断号，会指出模板并提供“修复奖励顺序”，点击后按页面当前顺序重排本次工作副本，再保存草稿、校验和发布。历史迁移还可能留下已停用的空模板；它可以保留，但启用前必须至少配置一项资产。V155 执行前创建的系统奖励草稿可能因正式配置指纹变化而失效，应丢弃旧草稿或以当前正式配置重建，再重新确认修改。Unity 接口与客户端接入流程不受此修复影响。

## 近期配置与接口补充（当前源码 V157）

以下变更不新增玩家接口或数据库迁移。两个资产引用接口只供运营后台使用；NPC 碎片命名、商城奖励展示快照和资源出售整数校验由现有发布链路执行。Unity 继续通过原有玩家接口取得正式目录和展示字段。

### 资产引用来源可用性与分页明细

基础地址仍为 `http://localhost:8080/api`。两个只读接口属于 `/api/ops/**`，必须携带运营 `Authorization: Bearer <operator-token>`；后端要求 `CONFIG_VIEWER`、`CONFIG_EDITOR` 或 `CONFIG_ADMIN` 的配置读取权限。Unity 玩家端不调用，也不能用玩家令牌访问。

```http
GET /api/ops/config/asset-references/availability?assetType=ITEM&assetId=123&scope=ASSET&sourceModuleCodes=COIN_SHOP,CARD_DRAW
GET /api/ops/config/asset-references/details?assetType=ITEM&assetId=123&scope=ASSET&sourceModuleCode=COIN_SHOP&offset=0&limit=20
```

| 参数 | 合同 |
| --- | --- |
| `assetType`、`assetId` | 必填；类型仅 `ITEM / EQUIPMENT / NPC`，ID 为正整数，服务端按正式基础资料解析身份。资产不存在返回 404。 |
| `assetKey` | 可选兼容参数；查询始终以 `assetType + assetId` 对应的服务端权威编码为准，不信任客户端传入值。 |
| `scope` | 可选，默认 `ASSET`；物品和装备只允许 `ASSET`，NPC 本体允许 `ASSET / NPC`，专属碎片用 `FRAGMENT`，此时按 NPC 所绑定的碎片物品查询。 |
| `sourceModuleCodes` | 仅可用性接口必填；逗号分隔或重复传参，共 1～30 个来源编码，去重后按请求顺序返回。来源必须属于当前资产类型与范围的白名单。 |
| `sourceModuleCode` | 仅明细接口必填；一次查询一个白名单来源。 |
| `offset`、`limit` | 仅明细接口使用；偏移量默认 0 且不可为负，单页数量默认 20、范围 1～50。 |

可用性响应的 `data[]` 每项包含 `sourceModuleCode`、`detailsSupported`、`locatableCount` 和 `unavailableReason`。`detailsSupported=false` 时 `locatableCount=null`；支持明细但当前没有可定位记录时为 `0`，不能把它当作该来源的总关联数。来源检查失败应显示重试，不应推断为“不支持”。

明细响应的 `data` 包含 `assetType/assetId/assetKey/assetName/scope/sourceModuleCode/sourceModuleName`、`summaryTotalCount`（来源关联总数）、`locatableCount`（当前可定位配置数）、`unavailableCount`（总数减可定位数的非负值）、`offset/limit/hasMore/unavailableReason` 和 `records[]`。每条记录包含 `referenceId/recordType/displayName/description/location`；`location` 包含 `kind/moduleCode/configKey/sectionKey/recordKey/parentRecordKey/routeName/query/available/unavailableReason`。`kind=CONFIG` 时按配置模块及结构化记录键定位；`kind=ROUTE` 时按路由名及查询参数定位；`location.available=false` 时显示原因，不执行跳转。分页只覆盖可定位记录，`summaryTotalCount` 可能大于 `locatableCount`。

玩家业务数据、历史流水与冻结快照，或尚未接入明细的来源仍计入引用统计，但可能不能逐条查看。抽卡奖励的定位键同时包含奖池编码、槽位、奖励类型和资产编码；编辑草稿删除、重排或替换奖励后，不得只凭槽位高亮另一项资产。上述接口不保存草稿、不发布配置，也不修改玩家记录。

### NPC 标准专属碎片名称

标准专属碎片编码为 `npc_fragment_{npcCode}`，且必须绑定对应 NPC。发布 NPC 名称或绑定时，服务端将其碎片展示名同步为“NPC 名称 + 碎片”；名称联动要求 NPC 名称不超过 98 个字符。发布物品基础资料时也会把已绑定的标准碎片名称校正为当前 NPC 名称。只有旧版默认说明会随名称更新；运营自定义说明保留原文。非标准编码的专属碎片仍独立命名，不套用该规则。Unity 从背包、图鉴、奖励等服务端响应读取碎片展示名，不拼接本地 NPC 名称，也不需要新增 DTO 字段。

首次合成页仅在预览响应 `fragment != null` 时使用 `fragment.name` 展示材料；`ALREADY_OWNED` 或 `NOT_AVAILABLE` 时 `fragment=null`，应按 `reasonCode/reasonMessage` 展示原因并禁用合成。普通 NPC 接口的表现资源字段为 `icon/portraitKey/modelKey`，挂机冒险接口为 `icon/portrait_key/model_key`；两种字段命名继续按各自 DTO 映射。

### 商城商品重新发布后的奖励快照

金币商城 `COIN_SHOP / PRODUCT_CATALOG` 和钻石商城 `DIAMOND_SHOP / PRODUCT_CATALOG` 在重新发布包含已有商品的配置时，会重新读取当前正式物品或装备资料，刷新商品的奖励名称、图标和分类展示快照；仅底层资产展示资料变化也会触发该快照差异，不能把它当作无变化发布。商品稳定编码与奖励身份仍按原有发布校验，不能借此原地替换奖励。钻石商城固定体力奖励仍使用“体力”、分类 `STAMINA` 和空图标。已成交订单保留成交时冻结的奖励展示快照，不追改历史。Unity 不需新增接口或字段；打开商城及购买配置变化后重新读取正式商品目录，历史订单继续展示订单返回的快照。

### 资源出售配置只接受整数数量

运营端 `RESOURCE_SALE / EXCHANGE_OPTIONS` 的每条 `options[]` 中，`sourceQuantityPerLot`、`targetQuantityPerLot`、`minLotsPerOrder`、`maxLotsPerOrder`、`dailySourceQuantityLimit` 和 `sortOrder` 必须是 JSON 整数。提交小数数值（包括 `1.0`）时，后端在配置解析阶段拒绝；前端应以整数输入展示，不应把金币或其他结算数量格式化成带小数的可提交值。已有的正数、额度、排序与客户端安全整数上限校验继续适用。Unity 按原玩家资源出售报价与成交响应显示整数数量；无需增加接口字段或自行计算汇率，成交前仍以最新服务端报价为准。

### 本轮 Unity 待同步与联调

当前机器没有 Unity 工程，下列客户端代码和资源仍需在实际 Unity 项目中实施与验收；本文件的接口说明不表示客户端已经修改。

| 位置 | Unity 需要同步 | 联调重点 |
| --- | --- | --- |
| Idle Adventure 角色、技能与背景 | 分开解析普通 NPC 的 `camelCase` 和玩法的 `snake_case` 表现资源字段；增加可空 `stage_presentation` 背景 DTO 和 `skill_icon` 绑定；按服务端 `targets[]` 播放范围伤害与治疗。 | 立绘、模型、图标或背景资源缺失时降级；当前 v3 没有背景资源，使用包内默认背景；旧动作缺少 `targets` 时回退单目标字段，断线恢复不使用当前技能配置覆盖旧会话。详见《IdleAdventure模式接口文档》第十三节。 |
| 属性与克制 | 新会话显示八种新属性，旧 v1/v2 会话还需兼容四种旧编码，合计识别 12 种编码。 | v3 的 13 条关系和旧版各七条由服务端按版本结算，Unity 不重算伤害或把旧码改写成新码。 |
| NPC 专属碎片与合成 | 碎片名称直接读服务端展示字段；合成预览仅在 `fragment != null` 时显示材料。 | `ALREADY_OWNED/NOT_AVAILABLE` 使用 `reasonCode/reasonMessage` 并禁用合成，不拼接本地碎片名或提交空指纹。 |
| 商城与资源出售 | 金币/钻石商城打开或配置变化后重拉目录，历史订单使用成交快照；价格、余额、批次及数量用 C# `long`/`long?`，按整数显示。 | 目录变化需重新确认购买；资源出售成交前以最新报价为准。运营资产引用接口只供后台使用，Unity 不调用，也无需为商城快照、碎片改名或出售整数校验增加玩家接口字段。 |
