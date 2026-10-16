# Unity 新手引导接入与验收

## 依据和边界

- 依据根目录接口文档第 56 章的 MAIN_FLOW 七步协议。
- 引导分配、步骤完成、版本和跳过权限均由服务器决定。客户端不发奖励、不保存本地完成标记。
- MAIN_FLOW 在文档中的默认状态是关闭。后台须发布正式版本、开启配置并允许当前玩家分配；返回 available=false 时不显示引导属于正常行为。
- 后端详细锚点白名单文档 docs/player-onboarding-api.md 未包含在当前工作区；当前对接文档列出的七个步骤和 HOME 经营地图。

## 场景入口

HomeScene 中已有的节点继续使用：

    PlayerOnboardingManager
      PlayerOnboardingCanvas
        Overlay
          Panel
            Title / Step / Content / Target / State
            AcknowledgeButton / Label
            SkipButton / Label
        TargetHighlight
        TargetArrow

- 管理器脚本为 PlayerOnboardingManager.cs，沿用原脚本 GUID，修复原文件名不匹配导致的 Missing Script。
- 运行时优先绑定场景面板，不再创建第二套同名 Canvas；缺少场景面板时才使用备用 UI。
- 文本使用 TMP，备用 UI 使用现有 RuntimeFontAssetProvider；场景字体、图片和布局继续使用已保存组件。
- 步骤面板仅在 available=true、status=IN_PROGRESS 且 currentStep 不为空时显示；PAUSED 隐藏步骤并稍后重新查询。
- 即使编辑时关闭 PlayerOnboardingCanvas，收到可展示步骤后也会启用父 Canvas 和 Panel。Canvas 的零缩放会恢复，非零缩放和面板手动布局不覆盖。
- WELCOME/FINISH 的 CLIENT_ACK 只显示说明和确认按钮，不要求地图目标，避免空 uiTargetKey 阻塞欢迎步骤。
- Console 中搜索 [Onboarding]，可以看到 available、status、step 摘要，不包含登录 Token。如果状态符合但仍不显示，检查是否另有脚本禁用 Canvas 或将 CanvasGroup.alpha 设为 0。
- 四块目标遮罩及地图地块命中区域按目标运行时生成，不改变首页按钮位置和功能。
- 当前 Unity 如果还有未保存的场景修改，先保存；重新加载 HomeScene 后核对管理器脚本和面板。不要用重建整个首页来刷新。

## 七步检查

| 步骤 | 玩家操作 | 通过条件 |
| --- | --- | --- |
| WELCOME | 确认欢迎说明 | POST 当前步骤 ACK，收到服务器下一步 |
| OPEN_HOME_MAP | 点击前往经营地图，地图加载后确认 | 真实加载经营场景且服务器选中 HOME 后才能 ACK |
| FOCUS_FREE_LAND | 查看高亮地块后确认 | 可放置地块真实可见后才能 ACK |
| PLACE_FARM_LAND | 点击高亮地块 | 使用服务器 landConfigId 和原有放置事务，服务端 LAND_PLACED 推进 |
| START_FARM_BUILD | 点击高亮建筑卡片 | 使用步骤绑定的 playerLandId，原建造检查和建造接口成功 |
| COMPLETE_FARM_BUILD | 等待建造完成；可完成时使用原完成按钮 | 使用同一个 playerBuildingId，服务端确认 BUILDING_COMPLETED |
| FINISH | 确认完成说明 | 服务端返回 COMPLETED，遮罩消失，操作解锁 |

## 异常与恢复检查

1. 引导关闭、不接受新分配、已完成、已跳过：不会自行重启流程。
2. 仅 allowSkip=true 显示跳过按钮；跳过成功不领取额外奖励。
3. ACK/跳过断网或 5xx：PlayerPrefs 按玩家保存 UUID、路径、原始请求体；重试不修改版本或请求号。
4. 请求成功但响应丢失，重新登录：先查当前快照，再重放原请求；旧写响应不回退当前页面。
5. 8002/8003/8004/8006：停止旧意图、刷新快照，不用新版本偷偷重发；8006 隐藏跳过。
6. 8005：保留冲突请求，停止自动提交并提示处理，不自动生成另一组参数覆盖原请求。
7. PAUSED：隐藏步骤并定时重新查询；8001：显示暂不可用提示并重试。两者均不自动跳过。
8. 版本不支持：停止操作，点击检查更新返回 Launch 的启动策略检查流程。
9. 断线重连、回到前台：重新 GET 当前快照；重复/旧版本实时消息不倒退步骤。
10. 切换账号：中止旧协程、清理旧快照和输入锁；未完成请求仅属于原玩家。
11. 建造步骤只能操作绑定实例；不能用另一个同配置地块或建筑替代。取消/拆除保护由原业务接口返回 1008。
12. 目标未加载时不放行全部底层点击；等待地图同步，或使用服务器允许的跳过入口。

## 本次本地验证范围

- 使用 Unity 自带 C# 编译器及现有 Bee 引用编译两套程序集，输出仅写 Temp，不启动另一份 Unity 项目。
- 临时反射检查覆盖版本回退、终态、请求模型、账号隔离、桥接方法、显示状态和欢迎步骤等 25 项。
- 编译不等于 Play Mode 和真实后端验收；以上七步、后台开启/暂停、断网恢复及移动端触控仍需使用实际服务器测试。
