# UIManager UI 模块

## 职责
- 负责 Canvas 访问、UI 实体创建、查找、显示隐藏和层级。
- 模块路径：`Scripts/EssSystem/Core/Presentation/UIManager`。
- 本文档只记录模块契约，具体实现细节以代码为准。

## 结构
- `Dao/`
- `Editor/`
- `Entity/`
- `Theme/`
- `OverlayCanvasProvider.cs`
- `UICursorManager.cs`
- `UIDraggable.cs`
- `UIManager.cs`
- `UIService.cs`
- `UIWorldFollower.cs`

## 边界
- 本模块只拥有“职责”中描述的行为，不隐式接管兄弟模块职责。
- 跨模块协作优先使用 EventProcessor 字符串协议，或目标模块明确暴露的窄接口。
- Demo 专属逻辑留在 Demo 目录，除非已经确认可复用为框架能力。

## Event API
- `UIManager.EVT_ADD_WINDOW_BEHAVIOR` = `"AddUIWindowBehavior"`
- `UIManager.EVT_DAO_PROPERTY_CHANGED` = `"UIDaoPropertyChanged"`
- `UIManager.EVT_GET_CANVAS_TRANSFORM` = `"GetUICanvasTransform"`
- `UIManager.EVT_GET_ENTITY` = `"GetUIEntity"`
- `UIManager.EVT_GET_UI_GAMEOBJECT` = `"GetUIGameObject"`
- `UIManager.EVT_HOT_RELOAD` = `"HotReloadUIConfigs"`
- `UIManager.EVT_REGISTER_ENTITY` = `"RegisterUIEntity"`
- `UIManager.EVT_UNREGISTER_ENTITY` = `"UnregisterUIEntity"`

## 维护注意
- 新增、改名或删除事件常量时，同步更新本节和根目录 Events.md。
- 示例保持最小化；实现细节写在代码注释里，模块契约写在本文档里。
- 已完成的 TODO 从本文档移除，必要时移动到 TODO.md。
