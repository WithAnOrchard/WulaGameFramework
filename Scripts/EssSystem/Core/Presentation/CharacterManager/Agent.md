# CharacterManager 角色表现模块

## 职责
- 负责 2D/3D 角色创建、动作播放、朝向、移动和帧事件。
- 模块路径：`Scripts/EssSystem/Core/Presentation/CharacterManager`。
- 本文档只记录模块契约，具体实现细节以代码为准。

## 结构
- `Common/`
- `Prefab3D/`
- `Sprite2D/`
- `CharacterManager.cs`
- `CharacterService.cs`

## 边界
- 本模块只拥有“职责”中描述的行为，不隐式接管兄弟模块职责。
- 跨模块协作优先使用 EventProcessor 字符串协议，或目标模块明确暴露的窄接口。
- Demo 专属逻辑留在 Demo 目录，除非已经确认可复用为框架能力。

## Event API
- `CharacterManager.EVT_CREATE_CHARACTER` = `"CreateCharacter"`
- `CharacterManager.EVT_DESTROY_CHARACTER` = `"DestroyCharacter"`
- `CharacterManager.EVT_GET_PART_SPRITE_ID` = `"GetCharacterPartSpriteId"`
- `CharacterManager.EVT_MOVE_CHARACTER` = `"MoveCharacter"`
- `CharacterManager.EVT_PLAY_ACTION` = `"PlayCharacterAction"`
- `CharacterManager.EVT_PLAY_LOCOMOTION` = `"PlayCharacterLocomotion"`
- `CharacterManager.EVT_SET_CHARACTER_POSITION` = `"SetCharacterPosition"`
- `CharacterManager.EVT_SET_CHARACTER_SCALE` = `"SetCharacterScale"`
- `CharacterManager.EVT_SET_DIRECTION` = `"SetCharacterDirection"`
- `CharacterManager.EVT_SET_FACING` = `"SetCharacterFacing"`
- `CharacterManager.EVT_STOP_ACTION` = `"StopCharacterAction"`
- `CharacterManager.EVT_TRIGGER_ATTACK` = `"TriggerCharacterAttack"`
- `CharacterService.EVT_FRAME_EVENT` = `"OnCharacterFrameEvent"`

## 维护注意
- 新增、改名或删除事件常量时，同步更新本节和根目录 Events.md。
- 示例保持最小化；实现细节写在代码注释里，模块契约写在本文档里。
- 已完成的 TODO 从本文档移除，必要时移动到 TODO.md。
