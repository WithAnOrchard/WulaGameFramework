# LightManager 指南（URP 专用）

## 概述

`Presentation/LightManager`（`[Manager(7)]`）—— **URP 专用**全局灯光与环境管理：主光、环境光、雾、天空盒、后处理 Volume、动态 3D Light、URP 2D Light，并通过预设（`LightPreset`）一键切换昼夜风格。

| | 类 | 角色 |
|---|---|---|
| Manager | `LightManager` | MonoBehaviour 单例：场景 RenderSettings + URP Volume + 17 个 [Event] 入口 |
| Service | `LightService` | 纯 C# 单例：预设持久化 + 玩家无障碍偏好（Bloom 倍率 / Vignette / CA 开关） |

> **依赖**：URP package（`UnityEngine.Rendering.Universal`）。Built-in Render Pipeline 项目请勿挂载本 Manager。
>
> **与 `VoxelLightManager(14)` 正交**：本 Manager 处理 URP 场景级（sun + sky + post-FX）；VoxelLightManager 处理体素地图内的光照传播算法。互不干扰，可并存。

## 文件结构

```
Presentation/LightManager/
├── LightManager.cs               Manager（17 个 [Event]）
├── LightService.cs               Service（预设 + 偏好持久化）
├── Dao/
│   ├── LightPreset.cs            预设打包数据
│   └── DefaultLightPresets.cs    内置 4 预设：Dawn / Noon / Dusk / Night
└── Agent.md                      本文档
```

## 数据流

```
ApplyPreset("Night", 3.0f)
  │
  ▼
LightManager.ApplyPreset
  ├─ Service.GetPreset("Night")            ──→ Presets.json
  ├─ CaptureCurrentAsPreset (from)
  └─ BlendPresetRoutine(from, to, 3s)
       每帧 lerp:
         Sun.color/intensity/rotation       ──→ Light(Directional)
         RenderSettings.ambient/fog          ──→ RenderSettings
         Bloom/Vignette/CA/ColorAdj          ──→ VolumeProfile.effect.value
       中点切离散字段:
         RenderSettings.fog (enable)
         RenderSettings.skybox (Material)    ──→ ResourceManager "GetMaterial"
       完成 → Service.CurrentPresetName = "Night"  (持久化)
```

## Event API

> 共 17 个命令。跨模块走 bare-string（§4.1）。

### Sun（主光源 Directional Light）

#### `LightManager.EVT_SET_SUN_COLOR`
- **常量**: `"SetSunColor"`
- **参数**: `[Color color]`
- **返回**: `Ok()` / `Fail`
- **副作用**: `_sunLight.color = color`

#### `LightManager.EVT_SET_SUN_INTENSITY`
- **常量**: `"SetSunIntensity"`
- **参数**: `[float intensity]`
- **返回**: `Ok()` / `Fail`
- **副作用**: `_sunLight.intensity = max(0, intensity)`

#### `LightManager.EVT_SET_SUN_ROTATION`
- **常量**: `"SetSunRotation"`
- **参数**: `[Vector3 euler]`
- **返回**: `Ok()` / `Fail`
- **副作用**: `_sunLight.transform.rotation = Quaternion.Euler(euler)`
- **典型用途**: 配合 DayNight 系统，按时间角度 Lerp 太阳位置

### Environment（环境光 / 雾 / 天空盒）

#### `LightManager.EVT_SET_AMBIENT`
- **常量**: `"SetAmbientLight"`
- **参数**: `[Color color, float intensity?]`
- **返回**: `Ok()` / `Fail`
- **副作用**: `RenderSettings.ambientLight` + `ambientIntensity`

#### `LightManager.EVT_SET_FOG`
- **常量**: `"SetFog"`
- **参数**: `[bool enable, Color? color, float? density]`
- **返回**: `Ok()` / `Fail`
- **副作用**: `RenderSettings.fog/fogColor/fogDensity`

#### `LightManager.EVT_SET_SKYBOX`
- **常量**: `"SetSkybox"`
- **参数**: `[string resourcesPath]`
- **返回**: `Ok(path)` / `Fail("加载天空盒失败")`
- **副作用**: `ResourceManager`（bare-string `"GetMaterial"`）加载 → `RenderSettings.skybox = mat` → `DynamicGI.UpdateEnvironment()`（重算环境光）

### URP 后处理 Volume

> 启动时若 `_globalVolume` 为空且 `_autoCreateVolume = true`，自动建一个全局 Volume + 4 个 Effect（Bloom / Vignette / ChromaticAberration / ColorAdjustments）。

#### `LightManager.EVT_SET_BLOOM`
- **常量**: `"SetBloom"`
- **参数**: `[float intensity, float? threshold]`
- **返回**: `Ok()` / `Fail`
- **副作用**: `bloom.intensity = intensity × Service.BloomIntensityScale`（无障碍倍率，0 = 关）

#### `LightManager.EVT_SET_VIGNETTE`
- **常量**: `"SetVignette"`
- **参数**: `[float intensity, Color? color]`
- **返回**: `Ok()` / `Fail`
- **副作用**: 若 `Service.VignetteEnabled = false`，强制 intensity = 0（玩家关闭暗角时即时生效）

#### `LightManager.EVT_SET_CHROMATIC_ABERRATION`
- **常量**: `"SetChromaticAberration"`
- **参数**: `[float strength]`
- **返回**: `Ok()` / `Fail`
- **副作用**: 若 `Service.ChromaticAberrationEnabled = false`，强制 strength = 0

#### `LightManager.EVT_SET_COLOR_ADJUSTMENTS`
- **常量**: `"SetColorAdjustments"`
- **参数**: `[float postExposure, float? saturation, float? contrast]`
- **返回**: `Ok()` / `Fail`
- **副作用**: `ColorAdjustments.postExposure / saturation / contrast`（saturation/contrast clamp 到 [-100, 100]）

### Presets（昼夜预设）

#### `LightManager.EVT_REGISTER_PRESET`
- **常量**: `"RegisterLightPreset"`
- **参数**: `[LightPreset preset]`
- **返回**: `Ok(preset.Name)` / `Fail`
- **副作用**: 写入 `LightService.CATEGORY_PRESETS`（**持久化**），下次启动可直接 ApplyPreset

#### `LightManager.EVT_APPLY_PRESET`
- **常量**: `"ApplyLightPreset"`
- **参数**: `[string presetName, float duration?=0]`
- **返回**: `Ok(name)` / `Fail("未注册预设")`
- **副作用**:
  - `duration <= 0` → 立即应用所有字段
  - `duration > 0` → 启动协程逐帧 Lerp（Sun/Ambient/Fog/PostFX 数值），中点切离散字段（FogEnabled / Skybox）
  - 写入 `Service.CurrentPresetName`（启动时若 `_autoApplySavedPreset = true` 自动恢复）

### Dynamic 3D Lights

#### `LightManager.EVT_REGISTER_LIGHT`
- **常量**: `"RegisterLight"`
- **参数**: `[string lightId, Light light]`
- **返回**: `Ok(lightId)` / `Fail`
- **副作用**: 写入运行时字典；外部 GameObject 销毁后字典里会留 null，下次 SET 会忽略

#### `LightManager.EVT_SET_LIGHT_INTENSITY`
- **常量**: `"SetLightIntensity"`
- **参数**: `[string lightId, float intensity, float? duration]`
- **返回**: `Ok(lightId)` / `Fail`
- **副作用**: `duration > 0` 启动协程 Lerp；`= 0` 立即赋值

### Dynamic 2D Lights（URP 2D Light2D）

#### `LightManager.EVT_REGISTER_LIGHT_2D`
- **常量**: `"RegisterLight2D"`
- **参数**: `[string lightId, Light2D light]`
- **返回**: `Ok(lightId)` / `Fail`
- **依赖**: `using UnityEngine.Rendering.Universal;`（URP 2D Renderer 启用时该组件生效）

#### `LightManager.EVT_SET_LIGHT_2D_INTENSITY`
- **常量**: `"SetLight2DIntensity"`
- **参数**: `[string lightId, float intensity, float? duration]`
- **返回**: `Ok(lightId)` / `Fail`

#### `LightManager.EVT_SET_LIGHT_2D_COLOR`
- **常量**: `"SetLight2DColor"`
- **参数**: `[string lightId, Color color]`
- **返回**: `Ok(lightId)` / `Fail`

## Inspector 字段

| 字段 | 默认 | 说明 |
|---|---|---|
| `_sunLight` | （空） | 主光源；为空启动时自动找场景中第一盏 Directional Light |
| `_globalVolume` | （空） | 全局 Volume；为空且 `_autoCreateVolume = true` 时自动建立 |
| `_autoCreateVolume` | `true` | 启动时自动创建全局 Volume + 4 个核心 Effect |
| `_registerDefaultPresets` | `true` | 启动时注册 4 个内置预设（Dawn / Noon / Dusk / Night） |
| `_autoApplySavedPreset` | `true` | 启动时自动应用 `Service.CurrentPresetName`（断点续连体验） |

## 内置 4 预设

| Preset | 特征 |
|---|---|
| **Dawn** | 暖橙偏粉太阳，低角度（10°），晨雾；Bloom ×1.4，轻微暗角 |
| **Noon** | 纯白太阳，高角度（60°），无雾；Bloom ×0.8，中性 |
| **Dusk** | 暖红夕阳，相反方位（-120°），晚霞雾；Bloom ×1.6，中度暗角 + 暖色调 |
| **Night** | 蓝月光（-60°），低强度（0.25），蓝灰雾；Bloom ×1.2，强暗角 + 低饱和 + 高对比 |

业务侧可用同 Name 覆盖：
```csharp
EventProcessor.Instance.TriggerEventMethod("RegisterLightPreset",
    new List<object> { new LightPreset("Noon") { /* 自定义字段 */ } });
```

## LightService 持久化

| 分类 | 键 | 类型 | 用途 |
|---|---|---|---|
| `Settings` | `BloomIntensityScale` | float | 玩家无障碍：Bloom 全局倍率（0~2，0 关） |
| `Settings` | `VignetteEnabled` | bool | 玩家偏好：暗角开关 |
| `Settings` | `ChromaticAberrationEnabled` | bool | 玩家偏好：色差开关 |
| `Settings` | `CurrentPresetName` | string | 上次应用的预设名（启动恢复） |
| `Presets` | `{name}` | `LightPreset` | 预设包（业务自注册） |

## 跨模块调用示例

```csharp
// 1) 启动期：DayNightManager 注册自定义预设
EventProcessor.Instance.TriggerEventMethod("RegisterLightPreset",
    new List<object> { new LightPreset("Forest_Noon") {
        SunIntensity = 1.1f, AmbientColor = new Color(0.5f, 0.6f, 0.45f),
        FogEnabled = true, FogColor = new Color(0.45f, 0.55f, 0.4f), FogDensity = 0.018f,
        BloomIntensity = 1.0f } });

// 2) 昼夜系统切阶段（与 DayNightGameManager.EVT_PHASE_CHANGED 协作）
[EventListener("DayNightPhaseChanged")]
public List<object> OnPhase(List<object> data) {
    var isNight = (bool)data[0];
    EventProcessor.Instance.TriggerEventMethod("ApplyLightPreset",
        new List<object> { isNight ? "Night" : "Noon", 3.0f });
    return ResultCode.Ok();
}

// 3) 玩家进室内：减弱主光、亮室内灯
EventProcessor.Instance.TriggerEventMethod("SetSunIntensity",
    new List<object> { 0.3f });
EventProcessor.Instance.TriggerEventMethod("SetLightIntensity",
    new List<object> { "tavern_chandelier", 2.0f, 0.5f });

// 4) 战斗高光时刻：加 Bloom + 红 Vignette + 震屏（与 CameraManager 协作）
EventProcessor.Instance.TriggerEventMethod("SetBloom",
    new List<object> { 2.0f });
EventProcessor.Instance.TriggerEventMethod("SetVignette",
    new List<object> { 0.4f, new Color(0.6f, 0.05f, 0.05f) });
EventProcessor.Instance.TriggerEventMethod("ShakeCamera",
    new List<object> { 0.2f, 0.4f });

// 5) 2D URP 项目：火把灯光呼吸
EventProcessor.Instance.TriggerEventMethod("RegisterLight2D",
    new List<object> { "torch_01", torchLight });
EventProcessor.Instance.TriggerEventMethod("SetLight2DIntensity",
    new List<object> { "torch_01", 0.6f, 0.4f });   // 0.4s lerp 到 0.6
```

## 注意事项

- **跨模块只走 bare-string**（§4.1）；预设走 `LightPreset` DAO 类（项目内中立类）；2D 灯需 `using UnityEngine.Rendering.Universal` 才能传 `Light2D`，这是 URP 包本身的约束
- **业务模块禁止**直接改 `RenderSettings.fog/skybox/...` —— 走事件让 LightManager 协调预设过渡
- **业务模块禁止**直接挂 `Volume` —— 用 `EVT_SET_*` 走全局 Volume；本地 Volume（trigger volume）属于游戏关卡设计范畴，不在框架管辖内
- **运行时创建的 VolumeProfile 不写盘** —— `ScriptableObject.CreateInstance` 创建，仅用于运行时；玩家偏好通过 `LightService` 持久化
- **预设过渡冲突**：同时多次 `ApplyPreset` 会取消上一个协程，按最新一次过渡
- **DayNight + LightManager 协作模式**：DayNight 系统只发 `EVT_PHASE_CHANGED`，LightManager 接听并 `ApplyPreset`；不要让 DayNight 直接改 `RenderSettings.fog`（§B 反模式：跨层耦合）
- **2D Light2D 与 3D Light 字典分开**：`_lights3D` / `_lights2D` 不互通；id 命名空间相同时按子系统区分调用
- **Anti-Patterns §B11（推测）**：禁止 `using UnityEngine.Rendering` / `UnityEngine.Rendering.Universal` 出现在非 LightManager 模块；外部模块走 Event 即可，无需直接 Volume API
