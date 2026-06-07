# InputManager 输入模块

## 职责
- 负责输入绑定、动作状态和跨模块输入查询。
- 模块路径：`Scripts/EssSystem/Core/Presentation/InputManager`。
- 本文档只记录模块契约，具体实现细节以代码为准。

## 结构
- `InputManager.cs`
- `InputService.cs`

## 边界
- 本模块只拥有“职责”中描述的行为，不隐式接管兄弟模块职责。
- 跨模块协作优先使用 EventProcessor 字符串协议，或目标模块明确暴露的窄接口。
- Demo 专属逻辑留在 Demo 目录，除非已经确认可复用为框架能力。

## Event API
- `InputManager.EVT_BIND_ACTION` = `"BindInputAction"`
- `InputManager.EVT_GET_AXIS` = `"GetInputAxis"`
- `InputManager.EVT_GET_MOUSE_POS` = `"GetMouseScreenPosition"`
- `InputManager.EVT_GET_MOUSE_SCROLL` = `"GetMouseScroll"`
- `InputManager.EVT_GET_MOVE_AXIS` = `"GetInputMoveAxis"`
- `InputManager.EVT_INPUT_DOWN` = `"OnInputDown"`
- `InputManager.EVT_INPUT_UP` = `"OnInputUp"`
- `InputManager.EVT_IS_DOWN` = `"IsInputDown"`
- `InputManager.EVT_IS_PRESSED` = `"IsInputPressed"`
- `InputManager.EVT_IS_UP` = `"IsInputUp"`
- `InputManager.EVT_UNBIND_ACTION` = `"UnbindInputAction"`

## 维护注意
- 数字快捷栏使用 `HotbarUse1..9`，技能快捷栏使用 `SkillUse1..4`；业务侧只读 Action，不直接写 KeyCode。
- 新增、改名或删除事件常量时，同步更新本节和根目录 Events.md。
- 示例保持最小化；实现细节写在代码注释里，模块契约写在本文档里。
- 已完成的 TODO 从本文档移除，必要时移动到 TODO.md。
