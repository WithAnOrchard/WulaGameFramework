# DialogueManager 对话模块

## 职责
- 负责对话配置、对话状态、选项、头像和默认对话 UI 创建。
- 模块路径：`Scripts/EssSystem/Core/Application/SingleManagers/DialogueManager`。
- 本文档只记录模块契约，具体实现细节以代码为准。

## 结构
- `Dao/`
- `UI/`
- `DialogueManager.cs`
- `DialogueService.cs`

## 边界
- 本模块只拥有“职责”中描述的行为，不隐式接管兄弟模块职责。
- 跨模块协作优先使用 EventProcessor 字符串协议，或目标模块明确暴露的窄接口。
- Demo 专属逻辑留在 Demo 目录，除非已经确认可复用为框架能力。

## Event API
- `DialogueManager.EVT_CLOSE_UI` = `"CloseDialogueUI"`
- `DialogueManager.EVT_OPEN_UI` = `"OpenDialogueUI"`
- `DialogueManager.EVT_SET_PORTRAIT_SPRITE` = `"SetDialoguePortraitSprite"`
- `DialogueService.EVT_ADVANCE` = `"AdvanceDialogue"`
- `DialogueService.EVT_END` = `"EndDialogue"`
- `DialogueService.EVT_ENDED` = `"OnDialogueEnded"`
- `DialogueService.EVT_LINE_CHANGED` = `"OnDialogueLineChanged"`
- `DialogueService.EVT_QUERY_CURRENT` = `"QueryDialogueCurrent"`
- `DialogueService.EVT_REGISTER_CONFIG` = `"RegisterDialogueConfig"`
- `DialogueService.EVT_REGISTER_DIALOGUE` = `"RegisterDialogue"`
- `DialogueService.EVT_SELECT_OPTION` = `"SelectDialogueOption"`
- `DialogueService.EVT_STARTED` = `"OnDialogueStarted"`

## 维护注意
- 新增、改名或删除事件常量时，同步更新本节和根目录 Events.md。
- 示例保持最小化；实现细节写在代码注释里，模块契约写在本文档里。
- 已完成的 TODO 从本文档移除，必要时移动到 TODO.md。
