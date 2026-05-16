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

> 共 8 个查询/命令 + 2 个广播。跨模块走 bare-string（§4.1）。

### 查询类（每帧可调）

#### `InputManager.EVT_IS_PRESSED` — Action 是否按住（持续）
- **常量**: `InputManager.EVT_IS_PRESSED` = `"IsInputPressed"`
- **参数**: `[string actionName]`
- **返回**: `Ok(bool)` / `Fail`

#### `InputManager.EVT_IS_DOWN` — Action 本帧是否按下（边沿）
- **常量**: `InputManager.EVT_IS_DOWN` = `"IsInputDown"`
- **参数**: `[string actionName]`
- **返回**: `Ok(bool)` / `Fail`

#### `InputManager.EVT_IS_UP` — Action 本帧是否抬起（边沿）
- **常量**: `InputManager.EVT_IS_UP` = `"IsInputUp"`
- **参数**: `[string actionName]`
- **返回**: `Ok(bool)` / `Fail`

#### `InputManager.EVT_GET_AXIS` — 取轴向值
- **常量**: `InputManager.EVT_GET_AXIS` = `"GetInputAxis"`
- **参数**:
  - 单参数 `[string axisName]` → 直接返 `Input.GetAxis(axisName)`（Unity 内置 Horizontal / Vertical 等）
  - 双参数 `[string negativeAction, string positiveAction]` → 返 -1/0/+1
- **返回**: `Ok(float)` / `Fail`
- **示例**:
  ```csharp
  // Unity 原生轴
  TriggerEventMethod("GetInputAxis", new List<object> { "Horizontal" });
  // 双 Action 模式（用 Action 系统）
  TriggerEventMethod("GetInputAxis", new List<object> { "MoveLeft", "MoveRight" });
  ```

#### `InputManager.EVT_GET_MOVE_AXIS` — 2D 移动向量（一步到位）
- **常量**: `InputManager.EVT_GET_MOVE_AXIS` = `"GetInputMoveAxis"`
- **参数**: `[]`
- **返回**: `Ok(Vector2)`
- **副作用**: 优先按 `MoveLeft/Right/Up/Down` 4 个 Action 计算（轴值 -1/0/+1）；都未绑定则回落 Unity `Horizontal` / `Vertical` 平滑轴
- **典型用途**: 玩家移动控制：
  ```csharp
  var r = TriggerEventMethod("GetInputMoveAxis", null);
  var dir = ResultCode.IsOk(r) ? (Vector2)r[1] : Vector2.zero;
  rb.velocity = dir * speed;
  ```

#### `InputManager.EVT_GET_MOUSE_POS` — 鼠标屏幕坐标
- **常量**: `InputManager.EVT_GET_MOUSE_POS` = `"GetMouseScreenPosition"`
- **参数**: `[]`
- **返回**: `Ok(Vector2)`

#### `InputManager.EVT_GET_MOUSE_SCROLL` — 鼠标滚轮 delta
- **常量**: `InputManager.EVT_GET_MOUSE_SCROLL` = `"GetMouseScroll"`
- **参数**: `[]`
- **返回**: `Ok(float)` —— 等价 `Input.GetAxis("Mouse ScrollWheel")`

### 命令类（键位绑定）

#### `InputManager.EVT_BIND_ACTION` — 覆盖 Action 绑定
- **常量**: `InputManager.EVT_BIND_ACTION` = `"BindInputAction"`
- **参数**: `[string actionName, params KeyCode[] keys]`
- **返回**: `Ok(actionName)` / `Fail`
- **副作用**: 写入运行时 `_bindings` + `InputService` 持久化分类
- **示例**:
  ```csharp
  EventProcessor.Instance.TriggerEventMethod("BindInputAction",
      new List<object> { "Jump", KeyCode.Space, KeyCode.Joystick1Button0 });
  ```

#### `InputManager.EVT_UNBIND_ACTION` — 解绑 Action（恢复默认或删除）
- **常量**: `InputManager.EVT_UNBIND_ACTION` = `"UnbindInputAction"`
- **参数**: `[string actionName]`
- **返回**: `Ok(actionName)` / `Fail`
- **副作用**: 移除运行时与持久化绑定。**注意**：默认 Action 解绑后会被下次启动时重新填充（默认绑定写在代码里）

### 广播类

#### `InputManager.EVT_INPUT_DOWN` — Action 本帧按下广播
- **常量**: `InputManager.EVT_INPUT_DOWN` = `"OnInputDown"`
- **data**: `[string actionName]`
- **触发**: Update 中检测到 last 无 / this 有时发送
- **订阅示例**:
  ```csharp
  [EventListener("OnInputDown")]
  public List<object> OnAnyInputDown(List<object> data)
  {
      if (data[0] is string action && action == "Jump") DoJump();
      return ResultCode.Ok();
  }
  ```

#### `InputManager.EVT_INPUT_UP` — Action 本帧抬起广播
- **常量**: `InputManager.EVT_INPUT_UP` = `"OnInputUp"`
- **data**: `[string actionName]`
- **触发**: Update 中检测到 last 有 / this 无时发送

## Inspector 字段

| 字段 | 默认 | 说明 |
|---|---|---|
| `_broadcastEvents` | `true` | 是否每帧广播 `OnInputDown` / `OnInputUp`；关闭后只能用 `EVT_IS_*` 主动查询（性能敏感场景） |

## InputService 持久化

| 分类 | 键 | 类型 | 说明 |
|---|---|---|---|
| `Bindings` | `{actionName}` | `string[]` | KeyCode 名数组（如 `["Space", "Joystick1Button0"]`）；空 = 用代码内默认 |

## 跨模块调用示例

```csharp
// 1) 玩家移动（每帧 Update）
var r = EventProcessor.Instance.TriggerEventMethod("GetInputMoveAxis", null);
if (ResultCode.IsOk(r)) {
    var dir = (Vector2)r[1];
    rb.velocity = dir.normalized * 5f;
}

// 2) 监听跳跃按下
[EventListener("OnInputDown")]
public List<object> OnInputDown(List<object> data) {
    if (data[0] is string a && a == "Jump") rb.AddForce(Vector2.up * 8f, ForceMode2D.Impulse);
    return ResultCode.Ok();
}

// 3) 设置选项页面允许玩家重绑跳跃键
EventProcessor.Instance.TriggerEventMethod("BindInputAction",
    new List<object> { "Jump", KeyCode.W });
```

## 注意事项

- **跨模块只走 bare-string**（§4.1）；不要 `using EssSystem.Core.Presentation.InputManager` 仅为读 `EVT_*` 常量
- **每帧广播开销**：默认 10 个 Action × Update 调用 `Input.GetKey` 是常数级；广播只在 Down/Up 时触发，无变化时零事件分发
- **业务模块禁止** `Input.GetKeyDown` / `Input.GetButtonDown` ——会绕过键位重绑系统，玩家自定义键位失效
- **未来切 New Input System**：仅需替换 `IsAnyKeyHeld` / `Update` 内部为 `InputAction.ReadValue<>` 等；事件协议（`EVT_*` + 参数）保持不变，所有调用方零改动
- **UI 模块**：`UIManager.EnsureEventSystem` 仍硬编码 `StandaloneInputModule`；若要切 New Input System，UIManager 也需配合升级到 `InputSystemUIInputModule`
