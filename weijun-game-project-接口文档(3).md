# Weijun Game Project 后端接口文档

重新生成时间：2026-07-13
更新日期：2026-07-27
同步说明：已同步当前代码，包括 V15 审核全额退款、V16/V17 Idle Adventure 26 条 Unity 契约、V18/V19 运营配置中心、V20 装备速度、V21/V22 运营登录与密码安全、V23 配置化补签、V24 默认配置中文显示名、V25-V29 玩家经验等级、建筑升级经验、配置刷新 Outbox、日常/周常任务和游戏内好友，以及 V30 微信官方好友赠礼、消息安全回调和管理端礼包配置。当前数据库迁移为 Flyway V1 至 V30。
客户端：Unity / 微信小游戏前端
后端基础地址：`http://localhost:8080/api`
统一 Content-Type：`application/json; charset=UTF-8`

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
  }
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
  }
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
| nickname | string | 是 | 最大 50 字符 | 玩家昵称，唯一 |

后端处理：

1. 检查 `users.username` 是否已存在。
2. 检查 `users.nickname` 是否已存在。
3. 插入新用户到 `users` 表。
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
| 参数不合法 | 参数校验提示 |

### 2.3 微信小游戏登录

```http
POST /api/user/wx-login
Content-Type: application/json
```

是否需要 token：否

前端传参：

```json
{
  "code": "wx.login 返回的临时 code",
  "nickname": "测试玩家"
}
```

参数说明：

| 字段 | 类型 | 必填 | 限制 | 说明 |
| --- | --- | --- | --- | --- |
| code | string | 是 | 非空 | Unity 微信小游戏端调用 `wx.login` 后得到的临时 code，只能使用一次 |
| nickname | string | 否 | 最大 50 字符 | 首次微信登录创建用户时使用 |

后端处理：

1. 使用 `code` 请求微信接口 `jscode2session`。
2. 微信返回 `openid/session_key/unionid`；`session_key` 只在本次服务端请求内存中使用，不再写入数据库。
3. 查询 `user_wechat_account` 表是否存在该 `openid`。
4. 如果不存在：
   - 创建本地用户到 `users` 表，账号格式为 `wx_<openid>`。
   - 初始化默认资源到 `player_item`：金币 1000、木材 500、石材 500、铁矿 300。
   - 自动初始化 `player_map` 和默认 `player_land`。
   - 插入微信绑定到 `user_wechat_account`。
   - 上述写入在独立事务中完成；同一 `openid` 并发首次登录时，唯一键冲突的一方完整回滚，再读取另一方已经提交的绑定，不会残留孤立用户或初始数据。
5. 如果已存在：
   - 查询绑定的本地用户。
   - 微信本次返回非空 `unionid` 时才更新；本次未返回时保留历史值。历史 `session_key` 由 V3 迁移清理，当前实体和查询也不再读取或写入该字段。
6. 签发 token，写入 `auth_token`。
7. 返回登录响应。

返回 data：`LoginResponse`

失败情况：

| 场景 | message |
| --- | --- |
| AppID/AppSecret 未配置 | 微信小游戏AppID或AppSecret未配置 |
| code 无效或重复使用 | 微信登录失败：invalid code... |
| 无法请求微信服务 | 微信登录失败：无法连接微信服务，请查看后端控制台日志 |

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
  "nickname": "Player 1",
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
      "itemId": 5,
      "itemKey": "food",
      "name": "食物",
      "itemType": "PRODUCT",
      "icon": "food",
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
    "itemId": 5,
    "itemKey": "food",
    "name": "食物",
    "itemType": "PRODUCT",
    "icon": "food",
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
      "produce": "food:60",
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
2. 查询全部 `land_config` 配置。
3. 使用 `gold_cost/wood_cost/stone_cost/iron_cost` 数字字段计算消耗；`cost` 仅作为前端展示文本，例如 `gold:100,wood:50`。
4. 根据玩家等级和背包资源计算每种地块是否可放置。

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
| landId | number | 是 | 必须存在于 `land_config` | 地块配置 ID |
| positionX | number | 是 | 非负整数，且小于地图大小 | X 坐标 |
| positionY | number | 是 | 非负整数，且小于地图大小 | Y 坐标 |

后端处理：

1. 校验请求体 `playerId` 是否等于当前 token 用户。
2. 查询 `users` 表确认玩家存在。
3. 查询或创建 `player_map`。
4. 查询 `land_config`。
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
5. 返回所有启用建筑，并带 `canBuild/cannotBuildReason`。

返回 data：`BuildingConfigResponse[]`

```json
[
  {
    "id": 2001,
    "code": "farm_2001",
    "name": "Farm",
    "description": "food:60",
    "needLand": "GRASSLAND",
    "cost": "gold:100,wood:50",
    "produce": "food:60",
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
    "canBuild": true,
    "cannotBuildReason": ""
  }
]
```

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
  "produce": "food:60",
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
    "itemId": 5,
    "itemKey": "food",
    "itemName": "食物",
    "icon": "food",
    "baseProduceAmount": 60,
    "produceAmount": 72,
    "npcProduceBonusRate": 0.2,
    "settledNpc": {
      "playerNpcId": 2,
      "npcId": 3001,
      "npcCode": "field_steward",
      "name": "田园管家",
      "icon": "Npc_1",
      "modelKey": "npc_field_steward",
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
      "itemId": 5,
      "itemKey": "food",
      "name": "食物",
      "quantity": 120
    }
  ],
  "productions": [
    {
      "playerBuildingId": 10,
      "itemKey": "food",
      "storedAmount": 0,
      "storageCapacity": 500,
      "canCollect": false
    }
  ],
  "items": [
    {
      "itemId": 5,
      "itemKey": "food",
      "name": "食物",
      "itemType": "PRODUCT",
      "icon": "food",
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
2. 查询 `npc_config` 中 `enabled = true` 的 NPC 配置。
3. 按 `npc_id` 升序返回 NPC 图鉴展示字段和实时产出加成配置。

返回 data：`NpcConfigResponse[]`

```json
[
  {
    "npcId": 3001,
    "npcCode": "field_steward",
    "name": "田园管家",
    "icon": "Npc_1",
    "modelKey": "npc_field_steward",
    "produceBonusRate": 0.2
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
| modelKey | string | Unity 模型资源 Key |
| produceBonusRate | number | 资源产出加成比例，例如 `0.2` 表示 +20% |

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
    "modelKey": "npc_field_steward",
    "level": 1,
    "state": "IDLE",
    "settledPlayerBuildingId": null,
    "produceBonusRate": 0.2
  }
]
```

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
    "modelKey": "npc_field_steward",
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

渠道边界：当前实现只适用于普通微信支付 API v3 JSAPI，不实现 `wx.requestMidasPayment` 米大师虚拟支付。正式启用退款前必须确认小游戏获准使用的真实支付渠道；如果使用米大师，不能调用本模块的普通商户退款 API。

统一规则：

1. 商品列表、创建订单、查询订单需要 `Authorization: Bearer <token>`。
2. 微信支付回调 `POST /api/payment/notify/wechat` 不需要业务 token，由微信服务器调用。
3. 支付总开关由 `PAYMENT_ENABLED` 控制；关闭时商品列表返回空数组，创建订单拒绝。
4. `WECHAT_PAY_ENABLED=false` 是本地订单测试模式，只创建本地订单，`payReady=false`，不会发货。
5. `WECHAT_PAY_ENABLED=true` 是真实微信支付模式，必须配置 `WECHAT_PAY_APP_ID`、`WECHAT_PAY_MCH_ID`、`WECHAT_PAY_MERCHANT_SERIAL_NO`、`WECHAT_PAY_MERCHANT_PRIVATE_KEY_PATH`、`WECHAT_PAY_API_V3_KEY`、`WECHAT_PAY_NOTIFY_URL`。
6. 前端创建订单只能传 `playerId/clientOrderId/productCode`，不能传价格、奖励物品或奖励数量。
7. `clientOrderId` 是客户端生成的幂等 ID，同一玩家同一 `clientOrderId` 重试只返回同一笔订单；重新下单必须生成新的 `clientOrderId`。
8. 商品价格、币种、奖励物品和奖励数量全部来自 `payment_product_config`，创建订单时会写入 `payment_order` 快照。
9. V14 起只启用钻石充值商品；历史金币和资源商品仅停用、不删除，已有订单仍按订单快照完成支付生命周期。
10. 订单有效期由 `PAYMENT_ORDER_EXPIRE_MINUTES` 控制，默认 30 分钟；查询或创建订单时会自动关闭已过期未支付订单。
11. 同一订单的并发请求先用短事务领取 `CREATING` 租约，在事务外调用微信，再用短事务提交 `READY` 或 `UNKNOWN`；远程调用不占用数据库连接和订单行锁。
12. 发货只在微信回调验签解密成功，或商户主动查单确认 `SUCCESS` 后，由同一个可信交易事务完成；客户端不能直接加资源。
13. `PAYMENT_RECONCILIATION_ENABLED=true` 时，回调丢失会由 `payment_reconciliation_task` 按有界批次和数据库租约主动查单；到期 `NOTPAY` 订单先调用微信关单，再关闭本地订单。
14. `PAYMENT_DELIVERY_MODE=sync` 时在当前进程内同步发货；`PAYMENT_DELIVERY_MODE=redis-stream` 时写入 `payment_delivery_task` 并发布 `payment:events`，由消费者幂等发货和补偿。
15. 如果微信确认支付时间晚于订单 `expireTime`，订单标记为 `MANUAL_REVIEW`，不会自动发货；需要运营核对支付并按实际情况退款或人工补发。
16. 支付成功后对账任务默认继续监控 365 天；发现 `REFUND.SUCCESS` 时，未发货订单取消任务，已发货订单追回奖励，余额不足则记录欠款并冻结资源消耗。
17. `PAYMENT_REFUND_ENABLED=true` 开启玩家申请和审核通过后的主动全额退款任务；运营审核接口另要求 `OPERATOR_API_ENABLED=true` 并配置独立运营账号。正式环境必须同时准备好审核能力后再开放玩家申请。
18. V15 主动退款只接受 `PAID/DELIVERED` 的钻石订单。退款金额、币种和应追回钻石数量全部来自锁定的 `payment_order` 快照，客户端和运营端都不能提交。
19. 审核通过后订单进入 `REFUNDING`，所有背包资源消费通过统一经济守卫拒绝；微信远程调用在数据库事务外执行，数据库租约防止多实例重复调用。
20. 微信全额退款成功后，待发货任务进入不可逆 `CANCELLED`；发布器、消费者和失败恢复任务都不能把它恢复为可发货状态。

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
  "productCode": "DIAMOND_60"
}
```

参数说明：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| playerId | number | 是 | 玩家 ID，必须等于 token 用户 ID |
| clientOrderId | string | 是 | 客户端订单幂等 ID，最大 64 字符；同一玩家内唯一 |
| productCode | string | 是 | 支付商品编码，对应 `payment_product_config.product_code` |

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

前端说明：`payReady=true` 时用 `payParams` 调起微信支付；支付成功后不要本地加资源，只轮询订单状态或刷新背包，最终发货以后端回调或主动查单确认的服务端状态为准。

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
    "templateId": "资源可收取模板ID",
    "page": "pages/index/index",
    "enabled": true
  },
  {
    "templateType": "OPERATION_NOTICE",
    "templateId": "运营提醒模板ID",
    "page": "pages/index/index",
    "enabled": false
  }
]
```

前端拿到 `templateId` 后调用微信 `wx.requestSubscribeMessage({ tmplIds: [templateId] })`。

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

WebSocket 只负责在线即时通知，不替代站内提醒查询。连接断开期间产生的消息不会补推，重新连接后应调用站内提醒和邮件列表接口补齐数据。

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

1. Unity 调用微信小游戏 API：`wx.login`。
2. 拿到临时 `code`。
3. Unity 调用后端：`POST /api/user/wx-login`。
4. 后端返回 `token` 和 `user.id`。
5. Unity 保存：
   - `token`
   - `playerId = user.id`
6. Unity 建立 WebSocket：`/api/ws/realtime`，握手请求头设置 `Authorization: Bearer <token>`，收到 `CONNECTED` 后开始心跳。
7. Unity 加载地图：`GET /api/land/map/{playerId}`。
8. 后续所有业务接口都带：`Authorization: Bearer <token>`。

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

1. 打开充值/商城面板时，调用 `GET /api/payment/product/list` 获取可售商品。
2. 玩家选择商品后，生成本地唯一 `clientOrderId`，调用 `POST /api/payment/order/create` 创建订单。
3. `payReady=true` 时，前端用 `payParams` 调起微信支付；`payReady=false` 时只提示当前为本地订单测试模式，不发放资源。
4. 支付成功后前端只刷新订单/背包状态，最终发货以后端回调验签或主动查单确认后的服务端状态为准。
5. 前端可轮询 `GET /api/payment/order/{playerId}/{orderNo}` 查看状态，`DELIVERED` 表示已到账；`MANUAL_REVIEW` 表示支付晚于订单有效期，等待运营核对，不会自动发货。

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
| users | 用户账号、昵称、等级、累计经验和已对账曲线版本，不保存资源余额 | 登录、注册、玩家基础信息、成长对账 |
| player_level_curve_state / player_level_config | 当前等级曲线 revision 和只追加的累计经验门槛 | 等级计算、配置发布、缓存刷新 |
| player_experience_record | 建筑、任务、回填和运营调整产生的不可变幂等经验流水 | 经验发放、重放、运营审计 |
| item_config | 物品配置，例如金币、木材、食物 | 背包查询、资源扣除、经营产出 |
| player_item | 玩家背包/仓库物品数量 | 背包查询、资源扣除、资源返还、产出收取 |
| equipment_config | 装备编码、类型、品质、图标、模型、成长曲线和等级上限 | 奖励校验、仓库、升级和穿戴 |
| player_equipment | 玩家逐件装备实例、等级、来源、仓库/穿戴/消耗状态和锁定标记 | 装备发放、升级、穿戴、卸下 |
| upgrade_gold_curve / upgrade_gold_curve_level | NPC/装备每个目标等级的独立金币成本 | NPC 和装备升级 |
| npc_level_attribute_config / npc_breakthrough_config | NPC 每级最终属性和五个突破档位 | NPC 升级、战斗属性 |
| equipment_level_attribute_config / equipment_breakthrough_config | 装备每级属性和同款装备突破数量 | 装备升级、战斗属性 |
| player_npc_equipment | NPC 当前穿戴关系和卸下历史 | 穿戴、卸下、装备面板 |
| npc_upgrade_record / equipment_upgrade_record | 每次升级消耗和等级变化快照 | 升级审计 |
| equipment_upgrade_material_record | 突破实际消耗的装备实例 | 材料审计、防重复消费 |
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
| player_task_config / player_task | 日常/周常任务定义和带奖励快照的玩家周期实例 | 任务概览、进度和领取 |
| player_task_progress_event | 可信业务来源的幂等任务进度流水 | 建筑完成和升级任务进度 |
| player_friend | 以较小玩家 ID 在前保存的一条无向好友关系 | 好友添加、删除和等级排序列表 |
| wechat_friend_gift_config | 微信 MP 礼包 ID、类型、每日领取上限、发送开关和配置版本 | 微信赠礼目录、运营配置、回调发货 |
| wechat_friend_gift_reward | 微信道具 ID/数量到游戏内物品 ID/数量的不可变映射 | 回调道具校验、背包资源发放 |
| wechat_gift_delivery_record | 微信 `OrderId`、平台道具、游戏奖励和接收玩家的幂等快照 | 官方好友赠礼与送礼任务礼包发货审计 |
| idle_adventure_mode_config 及子配置表 | 版本化玩法、技能、状态、敌人、关卡、奖励和克制 | Idle Adventure 26 条 Unity 接口 |
| player_idle_adventure_* / idle_adventure_session* | 玩家编队、进度、会话、命令、待结算和挂机快照 | Idle Adventure 战斗和挂机流程 |
| player_notification | 玩家站内提醒 | 提醒列表、已读、运营提醒、资源提醒 |
| player_subscribe_setting | 玩家微信订阅消息授权设置 | 查询模板、保存授权结果、发送前校验 |
| wechat_subscribe_send_log | 微信订阅消息发送日志 | 防重复提醒、排查发送失败 |
| player_online_state | 玩家最近登录和活跃时间 | 离线资源提醒判断 |
| notification_reminder_claim | 多实例提醒任务领取去重占位 | 离线资源提醒跨实例幂等 |
| player_mail | 玩家邮件主体、已读、领取、删除和过期状态 | 邮件列表、详情、已读、领取、删除 |
| player_mail_attachment | 邮件附件物品和数量 | 邮件详情、附件领取、一键领取 |

### 11.1 数据库外键约束

V1 至 V30 数据库迁移已补充外键约束、安全清理、令牌摘要、支付对账与审核退款、签到/成长/抽卡、建筑快照、Idle Adventure、运营配置中心、登录与密码安全、补签费用快照、默认中文显示名、玩家经验等级、任务、游戏内好友和微信官方好友赠礼；`init.sql` 只包含 V1 手工初始化结构：

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
| `wechat_friend_gift_reward.gift_config_id -> wechat_friend_gift_config.id` | 微信道具奖励必须对应已发布礼包映射 |
| `wechat_friend_gift_reward.item_id -> item_config.item_id` | 微信赠礼只能发放有效的游戏内物品 |
| `wechat_gift_delivery_record.player_id -> users.id` | 微信赠礼发货流水必须对应本地玩家 |
| `wechat_gift_delivery_record.gift_config_id -> wechat_friend_gift_config.id` | 微信赠礼发货流水必须保留原礼包映射 |
| `player_map.player_id -> users.id` | 玩家地图必须对应本地用户 |
| `player_land.player_id -> users.id` | 玩家地块必须对应本地用户 |
| `player_land.land_id -> land_config.land_id` | 玩家地块必须对应有效地块配置 |
| `building_level_config.building_id -> building_config.building_id` | 建筑等级配置必须对应建筑基础配置 |
| `player_building.player_id -> users.id` | 玩家建筑必须对应本地用户 |
| `player_building.building_id -> building_config.building_id` | 玩家建筑必须对应有效建筑配置 |
| `player_building.land_id -> player_land.id` | 玩家建筑必须放在有效玩家地块上 |
| `player_building_production.player_id -> users.id` | 建筑产出状态必须对应本地用户 |
| `player_building_production.player_building_id -> player_building.id` | 建筑产出状态必须对应有效玩家建筑 |
| `player_building_production.item_id -> item_config.item_id` | 建筑产物必须对应有效物品配置 |
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

先创建空数据库，应用启动时 Flyway 自动执行 `src/main/resources/db/migration` 下的版本脚本。当前新库依次执行 V1 至 V30。`FLYWAY_BASELINE_ON_MIGRATE` 默认是 `false`。

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

```bash
mysql -u root -p -e "CREATE DATABASE IF NOT EXISTS game_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci"
```

Flyway 已成功执行的脚本禁止修改，后续数据库变化必须新增下一个版本文件。以下脚本仅作为无法启动 Spring Boot 时的 V1 手工恢复兼容入口：

```bash
mysql -u root -p < src/main/resources/db/init.sql
```

`init.sql` 不创建测试玩家，正常启动不需要重复手工执行。仅在本地开发或接口联调环境继续执行：

```bash
mysql -u root -p < src/main/resources/db/dev-seed.sql
```

`dev-seed.sql` 会创建使用 BCrypt 密码散列的测试账号，以及对应背包、NPC、地图、地块和建筑数据。生产环境禁止执行该文件。

已有数据库升级时不要手工执行任何版本迁移；保持 `FLYWAY_ENABLED=true`，启动一个应用实例完成迁移后再启动其他实例。手工执行 `init.sql` 后数据库已经非空，首次启动也必须临时开启一次 baseline，成功后再关闭。V1 至 V30 成功执行后禁止修改；后续正式变化从 V31 开始新增迁移。旧迁移中的 MySQL `VALUES()` 弃用警告不能通过修改历史文件消除，新迁移必须使用别名语法。

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
15. 新手礼包从首次成功登录的北京时间自然日开始，第 4 天活动入口消失；月度签到按服务端当月实际天数运行。普通领取只认服务端当天；是否允许补领取决于计划的 `allow_makeup` 和费用配置，默认新手/月度计划都关闭补签。
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
```

请求体：无。

成功示例（为控制篇幅，`days` 和 `rewards` 数组只展示部分元素；实际接口返回完整数组）：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": {
    "businessTime": "2026-07-17 17:30:00",
    "plans": [
      {
        "planCode": "NEWBIE",
        "planName": "新手三日礼包",
        "planType": "NEWBIE_WINDOW",
        "periodKey": "LIFETIME",
        "currentDay": 2,
        "windowDays": 3,
        "expiresAt": "2026-07-19 00:00:00",
        "canClaim": true,
        "allowMakeup": false,
        "canMakeup": false,
        "makeupCost": null,
        "days": [
          {
            "rewardDay": 1,
            "status": "MISSED",
            "claimed": false,
            "canClaim": false,
            "canMakeup": false,
            "rewards": []
          },
          {
            "rewardDay": 2,
            "status": "AVAILABLE",
            "claimed": false,
            "canClaim": true,
            "canMakeup": false,
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
  }
}
```

日期状态：`CLAIMED` 已领取、`MISSED` 已错过、`AVAILABLE` 当天可领取、`LOCKED` 未来日期、`UNAVAILABLE` 当天没有启用奖励配置。是否允许补领必须读取当天的 `canMakeup`，不能只根据 `MISSED` 推断。计划级 `allowMakeup` 表示开关，`canMakeup` 表示当前至少有一天可补，`makeupCost` 返回单次费用的 `itemKey/itemName/icon/amount`；关闭时费用为 `null`。新手第 4 个自然日起，`NEWBIE` 不再返回；月度计划按当月实际天数返回 28 至 31 天。

### 15.2 领取当天签到奖励

```http
POST /api/sign-in/claim
Content-Type: application/json
Authorization: Bearer <token>
```

```json
{
  "planCode": "NEWBIE"
}
```

`planCode` 当前支持：

| 值 | 规则 |
| --- | --- |
| `NEWBIE` | 首次成功登录当日为第 1 天，连续 3 个北京时间自然日；只领当天，不能补领 |
| `MONTHLY` | `periodKey=yyyy-MM`，只领取北京时间当天对应的日期 |

前端点击签到按钮时，实际请求体只有当前计划的 `planCode`：新手礼包按钮传 `NEWBIE`，每日签到按钮传 `MONTHLY`。`playerId`、领取日期、奖励编号、奖励数量和是否补领都不能由客户端提交；玩家身份来自请求头 token，日期和奖励由服务端计算及读取配置。

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
  }
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

### 15.3 补签指定日期

```http
POST /api/sign-in/makeup
Content-Type: application/json
Authorization: Bearer <token>
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

1. 第 2 天不是 Java 写死的五件装备。`sign_in_reward_config` 可配置任意已启用 `equipment_config.equipment_key` 和任意正数量，装备类型也可扩展；同一天装备数量总和不得超过 `sign_in_plan.max_equipment_per_claim`。
2. 同一天启用的 `reward_seq` 必须是 `1..N` 连续序号。服务端在写领取记录前一次性校验全部物品、装备、NPC、fallback 和总量，任何配置无效都不会先发部分奖励。
3. 月度查询用一条 SQL 同时读取 `DEFAULT` 和当前 `yyyy-MM` 候选配置。指定月份当天只要存在启用配置，就整日覆盖默认配置，不进行合并。
4. 玩家 `users` 行使用 `SELECT ... FOR UPDATE` 锁定，数据库还有“玩家+计划+周期+日期档位”和“玩家+计划+领取自然日”两个唯一键。6 个并发领取请求的真实 MySQL 测试结果是 1 个成功、其余返回业务异常，资源只入账一次。
5. 主领取记录、背包物品、装备实例、NPC、fallback 和实际奖励流水都在同一个事务内。任一写入失败会整体回滚。
6. `users.first_login_time` 在首次成功签发 token 的事务中通过 `WHERE first_login_time IS NULL` 原子初始化，之后不修改。V6 升级时已有账号使用原 `create_time` 回填，不会被当成当天新玩家。
7. V1 至 V30 成功执行后禁止直接修改迁移文件。以后正式调整奖励、装备、补偿或上限，应从 V31 开始新增迁移；本地临时联调才可直接更新测试数据库。详细 SQL 示例见 `docs/database-script-guide.md`。
8. V6 默认共有 42 条奖励配置记录，因为一条记录表示一项奖励而不是一天：新手第 1 天 5 条资源、第 2 天 5 条装备、第 3 天 1 条 NPC，加上月度 1 至 31 日各 1 条，共 `5+5+1+31=42` 条。
9. `ITEM` 和 NPC 的 ITEM fallback 数量可配置为正数，但当前没有计划级物品数量上限。不要配置接近 Java/MySQL 有符号 `BIGINT` 上限的数值；玩家现有数量与奖励相加溢出时，数据库会拒绝写入并使整次领取事务回滚。
10. 新手计划和月度计划彼此独立。新玩家前 3 个自然日可以每天分别领取一次 `NEWBIE` 和 `MONTHLY`；第 4 天起新手计划不再显示或领取，月度计划仍可在每个北京时间自然日领取一次。
11. 补签开启时，费用物品和正数量必须同时配置；发布配置会验证物品已启用。每次补签把费用编码和数量写入领取快照，后续调价不会追溯修改历史扣费。

### 15.6 Unity 推荐流程

1. 登录成功进入主界面后调用 `GET /api/sign-in/overview`。
2. 根据 `plans[].canClaim/canMakeup` 显示红点，根据 `days[].status/canMakeup` 渲染已领、错过、可领、可补和锁定状态，并用 `makeupCost` 展示后端费用。
3. 玩家点击领取时只提交 `planCode`，按钮在请求期间禁用，不能自行提交日期、奖励或玩家 ID。
4. 成功后使用响应 `items` 刷新背包；如果 `rewards` 中存在 `grantedRewardType=EQUIPMENT`，再调用装备仓库接口刷新装备列表；NPC 奖励成功时刷新玩家 NPC 列表。
5. `6002` 按已领取处理并重新查询概览；`6005` 属于服务端配置错误，前端只提示暂时无法领取并上报日志。
6. 玩家点击可补日期时调用 `POST /api/sign-in/makeup`，只提交该计划和 `rewardDay`；成功后使用 `cost` 展示真实消耗并重新查询概览。

## 16. NPC 成长、装备成长和可配置卡池抽奖接口

本章接口都需要 token，玩家身份只从 `Authorization` 获取，前端不能传 `playerId`。升级一次只提升 1 级，目标等级、金币、碎片、同款装备材料和属性均由服务端读取数据库配置，客户端不能提交或修改。

NPC 和装备最高等级当前均为 100。目标等级为 20、40、60、80、100 时属于突破等级：NPC 额外消耗该 NPC 的专属碎片，装备额外消耗配置数量的同款 1 级装备。普通等级只消耗该目标等级配置的金币。

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

成功示例，以下展示田园管家从 19 级突破到 20 级：

```json
{
  "code": 200,
  "message": "NPC升级成功",
  "data": {
    "upgradeRecordId": 7001,
    "playerNpcId": 2,
    "npcId": 3001,
    "npcCode": "field_steward",
    "name": "田园管家",
    "fromLevel": 19,
    "level": 20,
    "goldCost": 3900,
    "fragmentCost": 20,
    "fragmentItemKey": "npc_fragment_field_steward",
    "remainingGold": 6100,
    "remainingFragments": 35,
    "health": 290,
    "attack": 58,
    "defense": 48,
    "produceBonusRate": 0.2
  }
}
```

规则：

1. `playerNpcId` 必须属于 token 玩家，不能升级其他玩家的 NPC。
2. 每次请求只从当前等级提升到下一等级，不能由客户端指定目标等级或连续升级次数。
3. 服务端先锁玩家行，再锁 NPC 实例行；金币、碎片、等级更新和 `npc_upgrade_record` 流水位于同一事务。
4. 非突破等级返回 `fragmentCost=0`、`fragmentItemKey=null`、`remainingFragments=null`。
5. NPC 正在建筑中入驻时也可以升级。升级不会结算、重置或改变建筑生产时钟，也不会改变 `produceBonusRate`。

### 16.2 装备升级

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
    "healthBonus": 0,
    "attackBonus": 67,
    "defenseBonus": 0
  }
}
```

规则：

1. 升级目标可以处于 `WAREHOUSE` 或 `EQUIPPED`，已穿戴装备升级成功后，其 NPC 装备面板会立即按新等级属性重新计算。
2. 突破材料必须与目标 `equipment_id` 相同、等级为 1、状态为 `WAREHOUSE`、未锁定、未穿戴，并且不能是升级目标本身。
3. 服务端按实例 ID 升序选择并使用 `FOR UPDATE` 锁定材料。锁顺序固定为玩家、升级目标、材料，防止并发升级或穿戴重复使用材料。
4. 材料会更新为 `CONSUMED`，并记录在 `equipment_upgrade_material_record`；消耗后不会出现在仓库中。
5. 普通等级返回 `sameEquipmentCount=0` 和空的 `consumedPlayerEquipmentIds`。

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

请求体和查询参数：无。返回唯一生效卡池、完整展示奖项和服务端可用抽奖档位。奖项数量由 `card_draw_pool.card_count` 配置，范围为 1 至 100，绝不是固定九宫格。下面使用 3 项活动池演示动态响应；真实接口的 `cards.length` 必须等于当前 `cardCount`：

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
| 6103 | NPC升级配置错误 | 金币、等级属性、碎片或突破配置缺失或无效 |
| 6104 | NPC升级资源不足 | 金币不足，或突破时对应专属碎片不足 |
| 6201 | 玩家装备不存在 | 装备不属于当前玩家、实例不存在或已经被消耗 |
| 6202 | 装备已达到最高等级 | 装备当前已经是 100 级或配置上限 |
| 6203 | 装备升级配置错误 | 金币、等级属性、上限或突破配置缺失或无效 |
| 6204 | 装备升级资源不足 | 金币不足，或可用的同款 1 级未锁定材料不足 |
| 6205 | NPC对应装备部位已被占用 | 指定 NPC 的同一装备部位已有活动装备 |
| 6301 | 当前没有可用卡池 | 没有处于有效时间范围内的唯一活动卡池 |
| 6302 | 抽奖档位不存在或未启用 | `optionCode` 错误或对应档位已关闭 |
| 6303 | 抽奖配置错误 | 奖项数量/连续性、权重、引用、价格、NPC 碎片或卡池版本配置无效 |
| 6304 | 钻石不足 | 当前钻石少于服务端配置的最终价格 |

### 16.10 数据配置、版本和并发规则

1. NPC 每级金币配置在 `upgrade_gold_curve_level`，每级最终属性在 `npc_level_attribute_config`，突破碎片在 `npc_breakthrough_config`。`npc_config.fragment_item_id` 绑定专属碎片，`duplicate_fragment_amount` 控制重复 NPC 转换数量。
2. 装备每级金币配置也在 `upgrade_gold_curve_level`，每级属性在 `equipment_level_attribute_config`，突破同款数量在 `equipment_breakthrough_config`。每一级都能单独修改，不使用 Java 金币倍率。
3. 卡池使用 `card_draw_pool` 版本化；`card_count` 配置 1 至 100 个展示奖项，档位价格在 `card_draw_option_config`，奖励、数量和权重在 `card_draw_reward_config`。运行时要求唯一活动卡池、启用奖励数量等于 `card_count`、内部序号连续为 `1..card_count`、有效 `SINGLE` 和优惠后的 `FIVE`。
4. `ITEM` 奖项可以配置任意启用物品，`EQUIPMENT` 奖项可以配置任意启用装备；`NPC` 奖项数量固定为 1，重复后按该 NPC 的专属配置转为碎片。
5. 每个抽奖批次把卡池版本、`card_count`、价格、完整卡池展示和实际结果保存为不可变快照。以后切换新卡池不会改变历史抽奖结果。
6. 玩家经济操作统一先锁 `users` 主记录。NPC/装备升级和抽奖的真实 MySQL 并发测试已覆盖突破材料只消耗一次、等级只提升一次、相同抽奖 key 只扣费和发奖一次。
7. V7 至 V30 已属于正式迁移，执行成功后禁止修改。后续调整金币、属性、材料、卡池或概率时新建 V31 或更高 Flyway 迁移，先创建和校验新卡池版本，再原子切换活动版本。

### 16.11 Unity 推荐流程

1. 打开 NPC 面板时查询玩家 NPC 列表；升级按钮只提交 `playerNpcId`，成功后使用响应等级和属性刷新面板。
2. 打开仓库时调用 `GET /api/equipment/warehouse`；穿戴后使用接口返回的 loadout 刷新 NPC，卸下后同时刷新 loadout 和仓库。
3. 装备升级按钮只提交 `playerEquipmentId`。突破前可以根据服务端配置展示消耗预览；当前接口没有独立预览时，以升级响应或失败码作为最终权威结果。
4. 打开抽奖界面先调用 `GET /api/card-draw/current`，按 `cardCount/cards` 动态渲染后端返回的全部奖项，按 `options` 显示单抽和五连的最终钻石价格与优惠；前端不写死网格数量，也不提供选卡操作。
5. 点击单抽传 `SINGLE`，点击五连传 `FIVE`，每次点击生成一个 `Idempotency-Key`。请求期间禁用按钮，网络重试复用相同 key。
6. 前端只在收到成功响应后按 `results[].drawSequence` 播放结果；`winningSlot` 是后端选中的内部奖项序号，`grantedRewardType/grantedRewardKey/grantedRewardAmount` 是实际入账奖励。
7. 结果含 `EQUIPMENT` 时刷新仓库，含新 `NPC` 时刷新 NPC 列表，含 `duplicateNpcConverted=true` 时按碎片表现展示并刷新背包。

## 17. 运营配置中心接口

运营配置中心使用独立运营账号和 `operator-token`，不能使用玩家 token。运行时游戏表始终是正式数据源；草稿、发布版本和审计分别保存在 `operator_config_release` 与 `operator_config_audit_log`。管理端基础页面位于 `admin-web`。

### 17.1 接口清单

管理端当前使用 17 个通用认证/配置/审计接口，并增加 3 个玩家成长运营接口：

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| `GET` | `/api/ops/auth/context` | 登录页系统、版本、环境和开关；无需 token |
| `POST` | `/api/ops/auth/login` | 运营账号登录 |
| `GET` | `/api/ops/auth/me` | 当前运营账号 |
| `POST` | `/api/ops/auth/logout` | 注销当前运营 token |
| `PUT` | `/api/ops/auth/password` | 验证旧密码并修改本人密码；成功后注销全部会话 |
| `GET` | `/api/ops/config/modules` | 可管理模块和配置键目录 |
| `GET` | `/api/ops/config/dictionaries` | 已启用物品、装备和 NPC 选择字典 |
| `GET` | `/api/ops/config/{moduleCode}/{configKey}` | 正式配置、草稿、版本和实时指纹 |
| `POST` | `/api/ops/config/{moduleCode}/{configKey}/validate` | 校验完整配置，不写数据库 |
| `PUT` | `/api/ops/config/{moduleCode}/{configKey}/draft` | 新建或更新唯一草稿 |
| `POST` | `/api/ops/config/{moduleCode}/{configKey}/publish` | 原子发布当前草稿 |
| `DELETE` | `/api/ops/config/{moduleCode}/{configKey}/draft` | 丢弃草稿，不改变正式配置 |
| `GET` | `/api/ops/config/{moduleCode}/{configKey}/history` | 查询历史版本摘要，`limit=1..100` |
| `GET` | `/api/ops/config/{moduleCode}/{configKey}/history/{versionNo}` | 按需读取一个完整历史快照 |
| `POST` | `/api/ops/config/{moduleCode}/{configKey}/rollback/{versionNo}` | 根据历史版本创建回滚草稿 |
| `GET` | `/api/ops/config-audits` | 使用 ID 游标分页查询配置审计 |
| `GET` | `/api/ops/config-audits/{auditId}` | 查询一条审计的前后快照 |
| `POST` | `/api/ops/player-experience/adjustments` | 使用运营请求号幂等增加玩家经验；仅 `CONFIG_ADMIN` |
| `GET` | `/api/ops/player-experience/adjustments` | 按玩家或记录 ID 游标查询经验调整历史；仅 `CONFIG_ADMIN` |
| `POST` | `/api/ops/player-experience/reconciliation` | 按玩家 ID 游标执行一批只升不降的成长对账；仅 `CONFIG_ADMIN` |

除 `/ops/auth/context` 和 `/ops/auth/login` 外，都必须携带：

```http
Authorization: Bearer <operator-token>
```

### 17.2 当前配置模块

| `moduleCode` | 可管理内容 | 典型 `configKey` |
| --- | --- | --- |
| `SIGN_IN` | 新手/月度每日多项奖励、补签开关与费用、NPC 重复补偿 | `NEWBIE:DEFAULT`、`MONTHLY:yyyy-MM` |
| `CARD_DRAW` | 当前活动池、1 至 100 个奖项、百分比概率、单抽/五连钻石价格 | `ACTIVE_POOL` |
| `NPC_GROWTH` | NPC 展示、专属碎片、1 至 100 级属性、突破和金币曲线 | NPC ID |
| `EQUIPMENT_GROWTH` | 装备展示、1 至 100 级生命/攻击/防御/速度加成、突破和金币曲线 | 装备 ID |
| `UPGRADE_CURVE` | NPC/装备每个目标等级的明确金币成本 | 曲线编码 |
| `IDLE_ADVENTURE` | 完整玩法版本、状态、技能、敌人、关卡、奖励、克制和挂机规则 | `IDLE_ADVENTURE` |
| `LAND_BUILDING` | 地块准入、建筑类型/前置、逐级成本、升级经验、耗时、产物、周期和容量 | `WORLD` |
| `PLAYER_PROGRESSION` | 只追加的玩家等级和累计经验门槛 | `LEVEL_CURVE` |
| `PLAYER_TASK` | 日常/周常任务目标、经验奖励、排序和启用状态 | `TASKS` |
| `WECHAT_FRIEND_GIFT` | 微信 MP 礼包、微信道具到游戏资源的固化映射、领取上限和发送开关 | `GIFT_PACKAGES` |

界面将地块/建筑类型、装备部位/品质、曲线适用类型和字典名称显示为中文，但请求和数据库仍使用稳定编码。V24 只转换仍等于系统英文默认值的名称，不覆盖已由运营自定义的名称。

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

当前实现是真实本地运营账号安全链路，没有伪造 SSO、WebAuthn 或 TOTP 按钮。生产环境必须通过 HTTPS、可信反向代理和网络访问控制暴露管理端；`OPERATOR_TRUSTED_PROXY_ADDRESSES` 只填写真实直接代理地址。

## 18. Idle Adventure 接口文档

Idle Adventure 对 Unity 保留 26 条 `snake_case` 接口，原 10 条精简路径已下线并返回 HTTP 404。完整路径、请求体、会话状态机、幂等键和 26 条逐项契约见同桌面的 `IdleAdventure模式接口文档.md`；该文件由项目内 `docs/idle-adventure-api.md` 同步生成。

## 19. 玩家经验、任务和好友接口（V25-V29）

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
    "curveRevision": 1
  }
}
```

`sourceId` 是不可变 `building_task.id`。首次执行和幂等重放会返回同一份原始经验结果，不会把重试伪装成“本次未升级”。

### 19.3 日常和周常任务

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
      "periodType": "DAILY",
      "periodKey": "20260724",
      "objectiveType": "BUILDING_UPGRADE",
      "targetAmount": 1,
      "progressAmount": 1,
      "experienceReward": 100,
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

任务进度只由服务端业务事件写入，客户端不能直接提交进度。每个事件使用 `player_id + objective_type + source_id` 唯一键防重；建筑任务 ID 是稳定来源 ID。

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
    "curveRevision": 1
  }
}
```

任务实例冻结任务名称、描述、目标、排序、配置版本和经验奖励。运营修改任务只影响以后物化的新周期实例。奖励为 0 时 `experienceGrant=null`。对已领取任务重试会返回第一次的任务状态和原经验流水，不重复发放。

### 19.4 游戏内好友列表（非微信好友）

当前实现是按本项目玩家 ID 建立、立即生效的双向好友关系，不包含申请/审批状态。它不是微信好友关系链，不会读取微信昵称、头像、openid 或云存储，也不能替代微信开放数据域的好友列表。微信官方好友和赠礼能力见第 20 章；客户端不得把 `/api/friends` 返回结果冒充微信好友。

#### 添加好友

```http
POST /api/friends/{friendPlayerId}
Authorization: Bearer <player-token>
```

重复添加按成功处理并返回同一好友资料。不能添加自己，目标玩家必须存在。

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

`cursorLevel` 和 `cursorPlayerId` 必须同时为空或同时存在，`limit` 范围为 1 至 100。数据库只保存一条规范化关系：`lower_player_id < higher_player_id`，并以唯一键防止并发重复添加。

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
- `experienceAmount` 为正整数，单次管理端安全上限是 `9,000,000,000,000,000`。
- `reason` 必填，最长 255 字符，并写入不可变经验流水。
- 同一玩家、同一请求号、同一金额重试时返回第一次完整结果，`replayed=true`；改传其他金额返回幂等冲突。

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

仅 `CONFIG_ADMIN` 可调用：

```http
POST /api/ops/player-experience/reconciliation
Authorization: Bearer <operator-token>
Content-Type: application/json
```

```json
{
  "afterPlayerId": 100,
  "batchSize": 100
}
```

两个字段都可以省略。`afterPlayerId` 是不包含自身的玩家主键游标；默认批量数为 100，默认配置上限为 500，代码硬上限为 1000。

```json
{
  "scanned": 100,
  "reconciled": 17,
  "skipped": 82,
  "failed": 1,
  "nextAfterPlayerId": 200,
  "hasMore": true,
  "curveRevision": 3,
  "failedPlayerIds": [156]
}
```

规则：

1. 权限校验后先刷新并校验最新完整等级曲线，再读取玩家游标。
2. 使用 `id > afterPlayerId ORDER BY id`，不使用大偏移量 `OFFSET`。
3. 每个玩家使用独立 `REQUIRES_NEW` 短事务和 `users ... FOR UPDATE`，单点失败不会回滚同批其他玩家。
4. 对账只提升 `level` 和 `level_curve_revision`，绝不增加、扣减或伪造经验，也绝不降级。
5. `reconciled` 表示实际提升等级或 revision；`skipped` 表示无需修改或玩家已消失。
6. `failedPlayerIds` 最多返回 50 个失败 ID，完整失败数以 `failed` 为准。
7. `hasMore=false` 时 `nextAfterPlayerId=null`，表示已经处理到末尾。

管理端页面路由为 `/progression-reconciliation`，支持自定义起始游标和批量数、逐批继续、累计扫描/修复/跳过/失败统计、失败玩家列表和从头重新开始。

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

运营可以配置 `taskName/description/periodType/objectiveType/targetAmount/experienceReward/sortOrder/enabled`。已发布任务不能删除，`taskCode` 不能修改；下线任务应设置 `enabled=false`。已有玩家任务实例继续使用创建时快照。

#### 建筑升级经验

`LAND_BUILDING / WORLD` 的每个建筑等级配置包含 `experienceReward`。该值必须非负，开始升级时冻结到 `building_task`。管理端发布只影响以后开始的升级。

### 19.8 缓存、事务和幂等实现

等级计算：

1. 应用启动时从 `player_level_config` 加载完整曲线，构建不可变 `TreeMap` 快照。
2. `totalExperience -> level` 使用 `floorEntry`，时间复杂度为 `O(log N)`；高频经验发放不查等级配置表。
3. 快照保存在 `AtomicReference`，只原子替换完整且已校验的新对象，不发布半成品。
4. 快照加载使用独立 `READ_COMMITTED` 短事务，避免 MySQL `REPEATABLE_READ` 旧快照。
5. Redis 通知关闭或丢失时，默认每 30 秒从数据库检查 revision；登录、`/user/me` 和运营对账会在玩家行锁之前主动刷新。

配置发布：

1. 等级配置、revision、配置版本、审计和 `config_refresh_outbox` 在同一个数据库事务提交。
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

1. 完整备份数据库，停写或进入维护窗口，只启动一个应用实例执行 Flyway V25-V30。
2. V25 自带等级范围断言；存在 1 至 100 之外的历史等级时迁移直接失败，不静默降级。
3. V25 先为每名历史玩家写入一条 `SYSTEM_BACKFILL / V25_PLAYER_LEVEL_BACKFILL` 流水，再把累计经验回填为其原等级最低门槛。
4. BI 的“正常经验产出”必须过滤 `SYSTEM_BACKFILL`，避免把迁移创建的基准经验计入运营产出。
5. V26 的 `building_task.experience_reward` 默认值为 0；历史进行中和已完成任务不补发。
6. 迁移完成后确认 `flyway_schema_history` 到 V30，再依次启动其他节点并观察等级曲线缓存 revision。
7. 已执行的 V25-V30 文件禁止修改，下一次正式数据库变化从 V31 新增。

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
| 6701 | 不能将自己添加为好友 | `friendPlayerId` 等于当前玩家 |
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
POST /api/announcements/9/read
Authorization: Bearer <player-token>
```

响应返回标记后的完整玩家公告，`read=true` 且包含首次 `readTime`。数据库主键为 `(announcement_id, player_id)`，重复调用不会新增记录，也不会覆盖首次阅读时间。

只能标记当前玩家仍然可见且仍在有效期内的公告。公告不存在、受众不匹配、尚未开始、已过期或已撤销时统一返回业务码 `6901`。

### 21.3 管理端公告接口和状态机

接口清单：

| 方法 | 路径 | 最低权限 | 用途 |
| --- | --- | --- | --- |
| `POST` | `/api/ops/announcements` | `CONFIG_EDITOR` | 创建公告草稿，`announcementCode` 为幂等键 |
| `PUT` | `/api/ops/announcements/{id}` | `CONFIG_EDITOR` | 按 `expectedVersion` 修改草稿 |
| `POST` | `/api/ops/announcements/{id}/publish` | `CONFIG_ADMIN` | 立即发布或进入定时发布 |
| `POST` | `/api/ops/announcements/{id}/cancel` | `CONFIG_ADMIN` | 撤销草稿、待发布或线上公告 |
| `GET` | `/api/ops/announcements` | `CONFIG_VIEWER` | 按状态和主键游标分页 |
| `GET` | `/api/ops/announcements/{id}` | `CONFIG_VIEWER` | 查询目标玩家和完整审计 |

创建全服公告示例：

```json
{
  "announcementCode": "NOTICE-20260727-1",
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

创建响应中的 `data.replayed` 表示是否为幂等重放。相同 `announcementCode` 和相同规范化内容重复提交时返回同一公告；相同编码改传其他内容时返回 `6903`，不会覆盖原公告。

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
| `POST` | `/api/ops/system-rewards/campaigns` | `CONFIG_ADMIN` | 创建立即或定时奖励活动 |
| `GET` | `/api/ops/system-rewards/campaigns` | `CONFIG_VIEWER` | 按状态和主键游标分页 |
| `GET` | `/api/ops/system-rewards/campaigns/{id}` | `CONFIG_VIEWER` | 查询冻结奖励、受众和审计 |
| `POST` | `/api/ops/system-rewards/campaigns/{id}/cancel` | `CONFIG_ADMIN` | 撤销或请求批次边界停止 |
| `POST` | `/api/ops/system-rewards/campaigns/{id}/retry` | `CONFIG_ADMIN` | 从失败活动原游标继续投递 |

创建全服奖励示例：

```json
{
  "requestNo": "TICKET-20260727-001",
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

1. `requestNo` 是稳定工单号或活动号，也是创建幂等键；允许字母、数字、点、下划线、冒号和短横线，最长 128。
2. 同一 `requestNo` 和同一规范化请求重试时返回原活动，`data.replayed=true`；内容不同返回 `7004`。
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
      "requestNo": "TICKET-20260727-001",
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
| `game_announcement` | 公告正文、受众类型和生命周期 | `announcement_code` 唯一、状态/时间窗/版本检查 |
| `game_announcement_target` | 指定玩家公告受众 | `(announcement_id, player_id)` 主键 |
| `player_announcement_read` | 玩家公告首次已读时间 | `(announcement_id, player_id)` 主键 |
| `operator_announcement_audit_log` | 公告操作审计 | 只追加，操作限 `CREATE/UPDATE/PUBLISH/CANCEL` |
| `system_reward_package` | 奖励模板主记录 | `package_code` 唯一、`config_version > 0` |
| `system_reward_package_item` | 模板物品 | 同模板物品和排序唯一、数量大于 0 |
| `operator_reward_campaign` | 奖励活动、游标、租约和状态 | `request_no` 唯一、目标/计数/租约成对检查 |
| `operator_reward_campaign_item` | 活动不可变物品快照 | 同活动物品和排序唯一 |
| `operator_reward_campaign_target` | 指定玩家活动受众快照 | `(campaign_id, player_id)` 主键 |
| `operator_reward_campaign_audit_log` | 奖励活动审计 | 只追加，操作限 `CREATE/CANCEL_REQUEST/RETRY` |

全服奖励不复制全服目标行，只保存 `targetMaxPlayerId + targetCount`；指定玩家奖励才写 `operator_reward_campaign_target`。公告也不为全服玩家复制公告或已读初始行，只有玩家实际阅读后才写一条已读记录。

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
| 6903 | 公告编码与首次创建内容不一致 | 同一 `announcementCode` 重放不同创建请求 |
| 7001 | 系统奖励模板不可用 | 模板不存在、未启用、无物品或冻结物品失效 |
| 7002 | 系统奖励活动不存在 | 管理端活动 ID 不存在 |
| 7003 | 系统奖励活动状态已变化 | 非法撤销/重试或租约、游标条件更新未命中 |
| 7004 | 系统奖励请求号与首次创建内容不一致 | 同一 `requestNo` 或邮件来源重放不同内容 |

参数格式、长度和数量错误仍返回通用参数错误；权限不足返回 403。接口不向客户端回显数据库 SQL、堆栈或内部租约实现细节。

### 21.11 上线、停用和恢复清单

1. 完整备份数据库，在维护窗口停止管理端写入和系统奖励调度，只保留一个应用实例执行 Flyway。
2. 确认 V1-V30 均成功后执行 `V31__add_operator_announcement_and_system_reward.sql`，检查 `flyway_schema_history` 已到 V31。
3. 检查 10 张新表、`uk_game_announcement_code`、`uk_operator_reward_campaign_request` 和活动租约检查约束存在。
4. 首次启动建议临时设置 `SYSTEM_REWARD_DISPATCH_ENABLED=false`，先验证管理端页面、权限、模板草稿和发布审计。
5. 确认默认 `SYSTEM_COMPENSATION_BASIC` 为关闭状态；根据正式经济方案修改数量、发布并显式启用。
6. 使用测试玩家创建一条指定玩家公告，验证定时显示、未读数、重复已读和撤销后不可见。
7. 使用专用测试玩家创建一条指定玩家奖励，确认只生成一封 `SYSTEM_REWARD / CAMPAIGN:<id>` 邮件，领取后背包只增加一次。
8. 手动重跑同一活动批次，确认邮件和附件数量不增加；使用同一 `requestNo` 重复创建应返回原活动。
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
4. 6 路并发使用同一 `requestNo` 创建活动只落一条活动记录并返回同一活动 ID。
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
5. 支持稳定 `requestNo` 创建幂等，同一请求重复提交返回原活动。
6. 每个玩家独立事务投递，批次重放不会重复创建邮件。
7. 玩家收到 `MailType.SYSTEM` 邮件，`hasAttachment=false`，附件列表为空，不出现领取按钮。
8. 普通邮件不会读取奖励模板，也不会写入 `player_mail_attachment`。

“系统邮件”和“系统奖励”在管理端是两个独立入口；底层只复用可靠投递骨架。奖励邮件仍必须冻结已发布奖励模板并至少包含一个附件，普通邮件不能包含任何附件，二者不会因为复用调度器而混淆业务语义。

### 22.2 管理端接口

接口清单：

| 方法 | 路径 | 最低权限 | 用途 |
| --- | --- | --- | --- |
| `POST` | `/api/ops/system-mails/campaigns` | `CONFIG_ADMIN` | 创建立即或定时普通系统邮件 |
| `GET` | `/api/ops/system-mails/campaigns` | `CONFIG_VIEWER` | 按状态和主键游标分页 |
| `GET` | `/api/ops/system-mails/campaigns/{id}` | `CONFIG_VIEWER` | 查询正文、受众和审计 |
| `POST` | `/api/ops/system-mails/campaigns/{id}/cancel` | `CONFIG_ADMIN` | 撤销或请求批次边界停止 |
| `POST` | `/api/ops/system-mails/campaigns/{id}/retry` | `CONFIG_ADMIN` | 从失败活动原游标继续发送 |

`CONFIG_EDITOR` 可以查看但不能发送；只有 `CONFIG_ADMIN` 可以创建、撤销或重试。后端始终再次校验权限，不能把管理端按钮是否显示当作安全边界。

创建全服普通邮件示例：

```json
{
  "requestNo": "MAIL-20260727-001",
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
  "requestNo": "MAIL-TICKET-20260727-002",
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
      "requestNo": "MAIL-20260727-001",
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
  -> 快速查询已有 requestNo
  -> SELECT 创建锁分片 FOR UPDATE
  -> 当前读再次查询 requestNo
  -> 验证并冻结受众
  -> 写活动和指定玩家目标快照
  -> 写 CREATE 不可变审计
  -> 提交事务并释放分片锁
```

规则如下：

1. `requestNo` 最长 128，只允许字母、数字、点、下划线、冒号和短横线。
2. `operator_reward_campaign.request_no` 仍为全局唯一，因此普通邮件和奖励活动不能复用同一个请求号。
3. 相同 `requestNo` 和相同规范化内容重试返回原活动并设置 `replayed=true`。
4. 相同 `requestNo` 但标题、正文、受众、时间或原因不同，返回 `7103`，不会覆盖第一次活动。
5. `ALL` 在创建事务中冻结当时最大的 `users.id` 和目标人数；之后注册的新玩家不属于该活动。
6. `PLAYER_IDS` 会排序、去重、校验全部玩家存在，再写入不可变目标快照。
7. 已过去的计划时间按数据库当前北京时间归一；有效期如非空，必须晚于实际计划时间。

V33 创建 64 行固定分片锁。相同请求号通过 `Math.floorMod(requestNo.hashCode(), 64)` 稳定落到同一行，并以 `SELECT ... FOR UPDATE` 在所有服务节点间串行创建判定。分片锁随数据库事务自动释放，不依赖单机 `synchronized`、Redis 可用性或连接级命名锁；不同请求只在哈希碰撞时短暂串行。

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
| 7103 | 系统邮件请求号与首次创建内容不一致 | 同一 `requestNo` 重放不同普通邮件请求 |
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
4. 6 路并发使用同一普通邮件 `requestNo` 只创建一条活动，并返回同一活动 ID。
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
