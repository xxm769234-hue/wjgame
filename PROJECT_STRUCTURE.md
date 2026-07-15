# Unity Project Structure

本项目的自有资源集中放在 `Assets/_Project` 下，第三方资源保留在各自目录中。

## 顶层结构

```text
Assets/
  _Project/        项目自有资源
  TextMesh Pro/    Unity TextMesh Pro 第三方资源，保持独立
Packages/          Unity 包配置
ProjectSettings/   Unity 项目设置
```

## Assets/_Project

```text
Assets/_Project/
  Art/
    Backgrounds/       背景图
    Lands/
      Masks/           地块遮罩图
      Textures/        地块贴图
  Configs/
    Buildings/         建筑 ScriptableObject 配置
    Lands/             地块 ScriptableObject 配置
    NPCs/              NPC ScriptableObject 配置
  Prefabs/
    Land/              地块、拖拽项、预览相关预制体
  Scenes/              游戏场景
  Scripts/
    Buildings/         建筑放置、建筑配置与建筑管理
    Core/              通用工具
    Land/              地块、预览、拖拽与地块配置
    Map/               地图管理与摄像机控制
    NPCs/              NPC 配置与管理
    Resources/         资源气泡与一键收取
    UI/                面板与按钮 UI 控制
```

## 维护规则

- 新增自己的游戏资源优先放进 `Assets/_Project`。
- 不要把自己的业务脚本放进 `TextMesh Pro` 等第三方目录。
- 在文件管理器中移动 Unity 资源时，要连同同名 `.meta` 一起移动。
- 场景放到 `Assets/_Project/Scenes` 后，如果加入 Build Settings，确认路径指向新位置。
- 配置类资源建议使用 `Configs`，运行时代码建议放到对应功能的 `Scripts` 子目录。
