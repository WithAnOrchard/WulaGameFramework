# LightManager 光照模块

## 职责
- 负责 2D/3D 光源、环境、雾、后处理和灯光预设。
- 模块路径：`Scripts/EssSystem/Core/Presentation/LightManager`。
- 本文档只记录模块契约，具体实现细节以代码为准。

## 结构
- `Dao/`
- `Editor/`
- `LightManager.cs`
- `LightService.cs`

## 边界
- 本模块只拥有“职责”中描述的行为，不隐式接管兄弟模块职责。
- 跨模块协作优先使用 EventProcessor 字符串协议，或目标模块明确暴露的窄接口。
- Demo 专属逻辑留在 Demo 目录，除非已经确认可复用为框架能力。

## Event API
- `LightManager.EVT_APPLY_PRESET` = `"ApplyLightPreset"`
- `LightManager.EVT_REGISTER_LIGHT` = `"RegisterLight"`
- `LightManager.EVT_REGISTER_LIGHT_2D` = `"RegisterLight2D"`
- `LightManager.EVT_REGISTER_PRESET` = `"RegisterLightPreset"`
- `LightManager.EVT_SET_AMBIENT` = `"SetAmbientLight"`
- `LightManager.EVT_SET_BLOOM` = `"SetBloom"`
- `LightManager.EVT_SET_CHROMATIC_ABERRATION` = `"SetChromaticAberration"`
- `LightManager.EVT_SET_COLOR_ADJUSTMENTS` = `"SetColorAdjustments"`
- `LightManager.EVT_SET_FOG` = `"SetFog"`
- `LightManager.EVT_SET_LIGHT_2D_COLOR` = `"SetLight2DColor"`
- `LightManager.EVT_SET_LIGHT_2D_INTENSITY` = `"SetLight2DIntensity"`
- `LightManager.EVT_SET_LIGHT_COLOR` = `"SetLightColor"`
- `LightManager.EVT_SET_LIGHT_INTENSITY` = `"SetLightIntensity"`
- `LightManager.EVT_SET_LIGHT_RANGE` = `"SetLightRange"`
- `LightManager.EVT_SET_LIGHT_SPOT_ANGLE` = `"SetLightSpotAngle"`
- `LightManager.EVT_SET_SKYBOX` = `"SetSkybox"`
- `LightManager.EVT_SET_SUN_COLOR` = `"SetSunColor"`
- `LightManager.EVT_SET_SUN_INTENSITY` = `"SetSunIntensity"`
- `LightManager.EVT_SET_SUN_ROTATION` = `"SetSunRotation"`
- `LightManager.EVT_SET_VIGNETTE` = `"SetVignette"`
- `LightManager.EVT_UNREGISTER_LIGHT` = `"UnregisterLight"`

## 维护注意
- 新增、改名或删除事件常量时，同步更新本节和根目录 Events.md。
- 示例保持最小化；实现细节写在代码注释里，模块契约写在本文档里。
- 已完成的 TODO 从本文档移除，必要时移动到 TODO.md。
