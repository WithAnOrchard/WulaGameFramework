# DialogueManager 指南

## 概述

`DialogueManager`（`[Manager(15)]`，薄门面）+ `DialogueService`（业务/状态机 + 持久化）提供完整对话系统：

- 一段 `Dialogue` 由若干 `DialogueLine` 组成；每行可显式 `NextLineId` 或顺序推进。
- 行内可挂 `DialogueOption[]`，选中后可：广播事件 / 触发回调 / 跳到指定行 / 切换到另一段对话。
- UI 可配置：主面板尺寸/位置/背景、说话者文本、正文文本、立绘、"下一句"按钮、选项按钮列表、关闭按钮。
- 通过 Event 调用 UIManager（`RegisterUIEntity` / `UnregisterUIEntity` / `GetUIGameObject` 等），与 UI 模块**完全解耦**。
- 单实例会话设计：同时只允许一段活动对话；启动新对话会覆盖旧的。

## 文件结构

```
DialogueManager/
├── DialogueManager.cs          薄门面：默认注册、EVT_OPEN_UI/EVT_CLOSE_UI、按钮回调、广播订阅
├── DialogueService.cs          状态机 + 持久化 + 6 个 [Event] handler + 3 个广播
├── Agent.md                    本文档
├── UI/
│   ├── DialogueUIBuilder.cs    纯静态：BuildPanelTree / ApplyLine
│   └── DialogueUIRefs.cs       UI 子组件引用集合
└── Dao/
    ├── Dialogue.cs             一段对话（含 List<DialogueLine>）
    ├── DialogueLine.cs         一行（说话者+文本+选项+背景/立绘覆盖）
    ├── DialogueOption.cs       选项（事件名/回调/跳转）
    └── UIConfig/
        └── DialogueConfig.cs   UI 配置（含 PanelLayout / TextLayout / PortraitLayout / NextButtonLayout / OptionsLayout / CloseButtonLayout 子类）
```

## 数据分类（持久化）

| 常量 | 用途 |
|---|---|
| `DialogueService.CAT_DIALOGUES` = `"Dialogues"` | 全部已注册 `Dialogue` |
| `DialogueService.CAT_CONFIGS`   = `"Configs"`   | 全部已注册 `DialogueConfig` |

会话状态（`ActiveDialogueId` / `ActiveLineId` / `ActiveConfigId`）属运行时数据，**不写盘**。

## 推进规则速查

| 当前行状态 | UI 显示 | 用户操作 |
|---|---|---|
| `Options` 非空 | 选项按钮列表 | 点击某选项 → `SelectOption(index)` |
| `Options` 为空 + `NextLineId` 有值 | "下一句" 按钮 | 点击 → 跳到 `NextLineId` |
| `Options` 为空 + 末尾行 | "下一句" 按钮 | 点击 → 结束对话 |

`DialogueOption` 三类副作用按顺序触发：① 广播 `EventName(EventArgs...)` → ② `OnSelected` 回调 → ③ 跳转。

## Event API

> 共 11 个：2 个 Manager 命令 + 6 个 Service 命令/查询 + 3 个 Service 广播。

### 命令类（调用方主动触发）

#### `DialogueManager.EVT_OPEN_UI` — 打开对话 UI 并启动会话
- **常量**: `DialogueManager.EVT_OPEN_UI` = `"OpenDialogueUI"`
- **参数**: `[string dialogueId, string configId?]`（configId 缺省时优先用 `Dialogue.ConfigId`，再退到 `Default`）
- **返回**: `ResultCode.Ok(dialogueId)` / `ResultCode.Fail(msg)`
- **副作用**: 调 `UIManager.EVT_REGISTER_ENTITY` 创建 UI；调 `DialogueService.StartDialogue` 启动会话；广播 `DialogueService.EVT_STARTED` + `EVT_LINE_CHANGED`
- **示例**:
  ```csharp
  EventProcessor.Instance.TriggerEventMethod(
      DialogueManager.EVT_OPEN_UI,
      new List<object> { "DebugDialogue" });
  ```

#### `DialogueManager.EVT_CLOSE_UI` — 结束对话并隐藏 UI
- **常量**: `DialogueManager.EVT_CLOSE_UI` = `"CloseDialogueUI"`
- **参数**: 无（`new List<object>()`）
- **返回**: `ResultCode.Ok("closed")`
- **副作用**: 调 `DialogueService.EndDialogue` 清状态；广播 `DialogueService.EVT_ENDED`；UI 仅隐藏（缓存复用），下次 Open 不重建

#### `DialogueService.EVT_REGISTER_DIALOGUE` — 注册对话
- **常量**: `DialogueService.EVT_REGISTER_DIALOGUE` = `"RegisterDialogue"`
- **参数**: `[Dialogue]`
- **返回**: `ResultCode.Ok(dialogue.Id)` / `ResultCode.Fail`
- **副作用**: 写入 `CAT_DIALOGUES` 持久化分类（覆盖同 Id）
- **跨模块提醒**: 该事件参数是 `Dialogue` Dao 类型，跨模块调用需 using `EssSystem.Core.Application.SingleManagers.DialogueManager.Dao`。建议在业务 GameManager 直接调 `DialogueService.Instance.RegisterDialogue(...)`，事件版本主要供 Inspector / 工具脚本用。

#### `DialogueService.EVT_REGISTER_CONFIG` — 注册 UI 配置
- **常量**: `DialogueService.EVT_REGISTER_CONFIG` = `"RegisterDialogueConfig"`
- **参数**: `[DialogueConfig]`
- **返回**: `ResultCode.Ok(config.ConfigId)`
- **副作用**: 写入 `CAT_CONFIGS` 持久化分类

#### `DialogueService.EVT_ADVANCE` — 推进到下一行
- **常量**: `DialogueService.EVT_ADVANCE` = `"AdvanceDialogue"`
- **参数**: 无
- **返回**: `ResultCode.Ok(activeLineId)` / `ResultCode.Fail("没有活动对话")`
- **副作用**: 当前行无选项时按 `NextLineId` 或列表顺序推进；末尾自动 `EndDialogue`；广播 `EVT_LINE_CHANGED` 或 `EVT_ENDED`
- **行有选项时**: 仅打 warning，不推进（应改用 `EVT_SELECT_OPTION`）

#### `DialogueService.EVT_SELECT_OPTION` — 选择当前行第 N 个选项
- **常量**: `DialogueService.EVT_SELECT_OPTION` = `"SelectDialogueOption"`
- **参数**: `[int index]`
- **返回**: `ResultCode.Ok(activeLineId)` / `ResultCode.Fail`
- **副作用**: 顺序触发 ① 广播 `option.EventName(option.EventArgs...)` ② 执行 `option.OnSelected` ③ 跳转；广播 `EVT_LINE_CHANGED` 或 `EVT_ENDED`

#### `DialogueService.EVT_END` — 强制结束当前会话
- **常量**: `DialogueService.EVT_END` = `"EndDialogue"`
- **参数**: 无
- **返回**: `ResultCode.Ok("ended")`
- **副作用**: 清空活动状态；广播 `EVT_ENDED`

#### `DialogueService.EVT_QUERY_CURRENT` — 查询当前会话
- **常量**: `DialogueService.EVT_QUERY_CURRENT` = `"QueryDialogueCurrent"`
- **参数**: 无
- **返回**: `[OK, dialogueId, lineId, configId]` 无活动时 `Fail`

### 广播类（订阅方用 `[EventListener]`）

#### `DialogueService.EVT_STARTED` — 对话启动**广播**
- **常量**: `DialogueService.EVT_STARTED` = `"OnDialogueStarted"`
- **广播参数**: `[string dialogueId, string configId]`

#### `DialogueService.EVT_LINE_CHANGED` — 当前行切换**广播**
- **常量**: `DialogueService.EVT_LINE_CHANGED` = `"OnDialogueLineChanged"`
- **广播参数**: `[string dialogueId, string lineId]`
- **典型用途**: UI 层订阅刷新（`DialogueManager` 自己的内部刷新走这个）

#### `DialogueService.EVT_ENDED` — 对话结束**广播**
- **常量**: `DialogueService.EVT_ENDED` = `"OnDialogueEnded"`
- **广播参数**: `[string dialogueId]`

## 使用范例

### 1. 在游戏内代码动态构造对话并打开 UI

```csharp
using EssSystem.Core.Application.SingleManagers.DialogueManager;
using EssSystem.Core.Application.SingleManagers.DialogueManager.Dao;

var d = new Dialogue("Quest_01", "村长寒暄")
    .WithConfig(DialogueManager.DefaultConfigId)
    .AddLine(new DialogueLine("greet",  "村长", "欢迎来到我们的村庄！"))
    .AddLine(new DialogueLine("ask",    "村长", "你愿意接受任务吗？")
        .AddOption(new DialogueOption("接受")
            .WithEvent("OnQuestAccepted", "Quest_01")
            .WithNextLine("accepted"))
        .AddOption(new DialogueOption("拒绝")
            .WithCallback(() => Debug.Log("玩家拒绝了任务"))))
    .AddLine(new DialogueLine("accepted", "村长", "祝你好运！"));

DialogueService.Instance.RegisterDialogue(d);

EventProcessor.Instance.TriggerEventMethod(
    DialogueManager.EVT_OPEN_UI,
    new List<object> { "Quest_01" });
```

### 2. 监听选项广播事件（业务模块）

```csharp
[EventListener("OnQuestAccepted")]   // §4.1 跨模块用 bare-string
public List<object> OnQuestAccepted(string evt, List<object> args)
{
    var questId = args.Count > 0 ? args[0] as string : null;
    // 触发任务系统...
    return null;
}
```

### 3. 自定义对话框 UI（背景/位置）

```csharp
using EssSystem.Core.Application.SingleManagers.DialogueManager.Dao.UIConfig;

var cfg = new DialogueConfig("Romance", "情节对话")
    .WithPanel(new DialogueConfig.PanelLayout()
        .WithSize(1100f, 280f)
        .WithPosition(960f, 220f)
        .WithBackgroundId("dialog_box_pink")            // 用 ResourceManager 注册的 Sprite Id
        .WithBackgroundColor(new Color(1f, 0.92f, 0.95f, 0.95f)))
    .WithBody(new DialogueConfig.TextLayout(960f, 140f, 0f, 20f, 22, TextAnchor.UpperLeft))
    .WithSpeaker(new DialogueConfig.TextLayout(360f, 36f, -340f, 130f, 24, TextAnchor.MiddleLeft));

DialogueService.Instance.RegisterConfig(cfg);
```

## 与其他模块的协作

| 场景 | 路径 |
|---|---|
| 启动对话 | 业务 → `EVT_OPEN_UI`（命令） |
| 接收选项触发的事件 | 业务用 `[EventListener("XXX")]` 订阅 `option.EventName` |
| UI 刷新（DialogueManager 内部） | 订阅 `EVT_LINE_CHANGED` → `RefreshCurrentLine` |
| 对话结束清理 | 订阅 `EVT_ENDED`（外部业务清场景状态等） |

## 默认行为

`DialogueManager` Inspector 提供两个 toggle：

- `_registerDefaultConfig` — 启动注册一份 `Default` 配置（id=`DialogueManager.DefaultConfigId`）
- `_registerDebugDialogue` — 启动注册一段 `DebugDialogue` 用以开箱即用调试，可通过右键菜单"打开调试对话"立刻看到效果

线上发版前请把这两个开关关闭。
