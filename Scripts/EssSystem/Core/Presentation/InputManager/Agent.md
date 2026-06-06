# InputManager 指南
## 概述
`Presentation/InputManager`（`[Manager(2)]`）—— 输入抽象层：Action 事件 + 鼠标 + 轴向查询，统一封装 `UnityEngine.Input`。
| | 类 | 角色 |
|---|---|---|
| Manager | `InputManager` | MonoBehaviour 单例：每帧 Update 检测 Action 状态变化 + 广播 `EVT_INPUT_DOWN/UP` |
| Service | `InputService` | 纯 C# 单例：玩家自定义键位持久化（actionName → KeyCodes） |
**设计目标**：业务模块写 `IsPressed("Jump")` 而非 `Input.GetKey(KeyCode.Space)`，让玩家可重绑键位、未来无痛切换 New Input System。
## 文件结构
```
Presentation/InputManager/
├── InputManager.cs   Manager（每帧轮询 + 广播 + 11 个 [Event]）
├── InputService.cs   Service（玩家自定义键位持久化）
└── Agent.md          本文档
```
## 默认 Action 绑定
| Action 名 | 默认 KeyCodes | 用途 |
|---|---|---|
| `MoveLeft` | `A`, `LeftArrow` | 移动 X- |
| `MoveRight` | `D`, `RightArrow` | 移动 X+ |
| `MoveUp` | `W`, `UpArrow` | 移动 Y+ |
| `MoveDown` | `S`, `DownArrow` | 移动 Y- |
| `Jump` | `Space` | 跳跃 |
| `Attack` | `Mouse0` | 主动作 / 攻击 |
| `AltAction` | `Mouse1` | 副动作 / 防御 |
| `Interact` | `E` | 交互 |
| `Cancel` | `Escape` | 取消 / 关闭 UI |
| `Pause` | `P`, `Pause` | 暂停 |
> 玩家通过 `EVT_BIND_ACTION` 自定义绑定后写入 `InputService`；启动时 Service 中的覆盖项会"压"在默认绑定之上。
## Event API

> Full Event definitions (params / return / side effects / usage) live in root Events.md -> section: **InputManager Event**.

- `InputManager.EVT_BIND_ACTION`
- `InputManager.EVT_GET_AXIS`
- `InputManager.EVT_GET_MOUSE_POS`
- `InputManager.EVT_GET_MOUSE_SCROLL`
- `InputManager.EVT_GET_MOVE_AXIS`
- `InputManager.EVT_INPUT_DOWN`
- `InputManager.EVT_INPUT_UP`
- `InputManager.EVT_IS_DOWN`
- `InputManager.EVT_IS_PRESSED`
- `InputManager.EVT_IS_UP`
- `InputManager.EVT_UNBIND_ACTION`

## Inspector 字段
| 字段 | 默认 | 说明 |
|---|---|---|
| `_broadcastEvents` | `true` | 是否每帧广播 `OnInputDown` / `OnInputUp`；关闭后只能用 `EVT_IS_*` 主动查询（性能敏感场景） |
## InputService 持久化
| 分类 | 键 | 类型 | 说明 |
|---|---|---|---|
| `Bindings` | `{actionName}` | `string[]` | KeyCode 名数组（如 `["Space", "Joystick1Button0"]`）；空 = 用代码内默认 |
## 跨模块调用示例
## 注意事项
- **跨模块只走 bare-string**（§4.1）；不要 `using EssSystem.Core.Presentation.InputManager` 仅为读 `EVT_*` 常量
- **每帧广播开销**：默认 10 个 Action × Update 调用 `Input.GetKey` 是常数级；广播只在 Down/Up 时触发，无变化时零事件分发
- **业务模块禁止** `Input.GetKeyDown` / `Input.GetButtonDown` ——会绕过键位重绑系统，玩家自定义键位失效
- **未来切 New Input System**：仅需替换 `IsAnyKeyHeld` / `Update` 内部为 `InputAction.ReadValue<>` 等；事件协议（`EVT_*` + 参数）保持不变，所有调用方零改动
- **UI 模块**：`UIManager.EnsureEventSystem` 仍硬编码 `StandaloneInputModule`；若要切 New Input System，UIManager 也需配合升级到 `InputSystemUIInputModule`
