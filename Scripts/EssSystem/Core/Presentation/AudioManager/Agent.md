# AudioManager 音频模块

## 职责
- 负责 BGM、SFX、音量控制、位置循环音效和常用音效入口。
- 模块路径：`Scripts/EssSystem/Core/Presentation/AudioManager`。
- 本文档只记录模块契约，具体实现细节以代码为准。

## 结构
- `AudioManager.cs`
- `AudioService.cs`

## 边界
- 本模块只拥有“职责”中描述的行为，不隐式接管兄弟模块职责。
- 跨模块协作优先使用 EventProcessor 字符串协议，或目标模块明确暴露的窄接口。
- Demo 专属逻辑留在 Demo 目录，除非已经确认可复用为框架能力。

## Event API
- `AudioManager.EVT_PAUSE_BGM` = `"PauseBGM"`
- `AudioManager.EVT_PLAY_ATTACK_SFX` = `"PlayAttackSFX"`
- `AudioManager.EVT_PLAY_BGM` = `"PlayBGM"`
- `AudioManager.EVT_PLAY_DAMAGE_SFX` = `"PlayDamageSFX"`
- `AudioManager.EVT_PLAY_ITEM_USE_SFX` = `"PlayItemUseSFX"`
- `AudioManager.EVT_PLAY_POSITIONAL_LOOP_SFX` = `"PlayPositionalLoopSFX"`
- `AudioManager.EVT_PLAY_SFX` = `"PlaySFX"`
- `AudioManager.EVT_PLAY_UI_SFX` = `"PlayUISFX"`
- `AudioManager.EVT_RESUME_BGM` = `"ResumeBGM"`
- `AudioManager.EVT_SET_BGM_VOLUME` = `"SetBGMVolume"`
- `AudioManager.EVT_SET_MASTER_VOLUME` = `"SetMasterVolume"`
- `AudioManager.EVT_SET_SFX_VOLUME` = `"SetSFXVolume"`
- `AudioManager.EVT_STOP_BGM` = `"StopBGM"`
- `AudioManager.EVT_STOP_POSITIONAL_SFX` = `"StopPositionalSFX"`

## 维护注意
- 新增、改名或删除事件常量时，同步更新本节和根目录 Events.md。
- 示例保持最小化；实现细节写在代码注释里，模块契约写在本文档里。
- 已完成的 TODO 从本文档移除，必要时移动到 TODO.md。
