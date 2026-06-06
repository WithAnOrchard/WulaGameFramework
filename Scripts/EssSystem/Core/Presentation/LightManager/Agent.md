# LightManager 指南（URP 专用）
## 概述
`Presentation/LightManager`（`[Manager(7)]`）—— **URP 专用**全局灯光与环境管理：主光、环境光、雾、天空盒、后处理 Volume、动态 3D Light、URP 2D Light，并通过预设（`LightPreset`）一键切换昼夜风格。
| | 类 | 角色 |
|---|---|---|
| Manager | `LightManager` | MonoBehaviour 单例：场景 RenderSettings + URP Volume + 17 个 [Event] 入口 |
| Service | `LightService` | 纯 C# 单例：预设持久化 + 玩家无障碍偏好（Bloom 倍率 / Vignette / CA 开关） |
> **依赖**：URP package（`UnityEngine.Rendering.Universal`）。Built-in Render Pipeline 项目请勿挂载本 Manager。
>
> **与 `VoxelLightingManager(14)` 正交**：本 Manager 处理 URP 场景级（sun + sky + post-FX）；VoxelLightingManager 处理体素地图内的光照传播算法。互不干扰，可并存。
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

> Full Event definitions (params / return / side effects / usage) live in root Events.md -> section: **LightManager Event**.

- `LightManager.EVT_APPLY_PRESET`
- `LightManager.EVT_REGISTER_LIGHT`
- `LightManager.EVT_REGISTER_LIGHT_2D`
- `LightManager.EVT_REGISTER_PRESET`
- `LightManager.EVT_SET_AMBIENT`
- `LightManager.EVT_SET_BLOOM`
- `LightManager.EVT_SET_CHROMATIC_ABERRATION`
- `LightManager.EVT_SET_COLOR_ADJUSTMENTS`
- `LightManager.EVT_SET_FOG`
- `LightManager.EVT_SET_LIGHT_2D_COLOR`
- `LightManager.EVT_SET_LIGHT_2D_INTENSITY`
- `LightManager.EVT_SET_LIGHT_COLOR`
- `LightManager.EVT_SET_LIGHT_INTENSITY`
- `LightManager.EVT_SET_LIGHT_RANGE`
- `LightManager.EVT_SET_LIGHT_SPOT_ANGLE`
- `LightManager.EVT_SET_SKYBOX`
- `LightManager.EVT_SET_SUN_COLOR`
- `LightManager.EVT_SET_SUN_INTENSITY`
- `LightManager.EVT_SET_SUN_ROTATION`
- `LightManager.EVT_SET_VIGNETTE`
- `LightManager.EVT_UNREGISTER_LIGHT`

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
## LightService 持久化
| 分类 | 键 | 类型 | 用途 |
|---|---|---|---|
| `Settings` | `BloomIntensityScale` | float | 玩家无障碍：Bloom 全局倍率（0~2，0 关） |
| `Settings` | `VignetteEnabled` | bool | 玩家偏好：暗角开关 |
| `Settings` | `ChromaticAberrationEnabled` | bool | 玩家偏好：色差开关 |
| `Settings` | `CurrentPresetName` | string | 上次应用的预设名（启动恢复） |
| `Presets` | `{name}` | `LightPreset` | 预设包（业务自注册） |
## 跨模块调用示例
## 注意事项
- **跨模块只走 bare-string**（§4.1）；预设走 `LightPreset` DAO 类（项目内中立类）；2D 灯需 `using UnityEngine.Rendering.Universal` 才能传 `Light2D`，这是 URP 包本身的约束
- **业务模块禁止**直接改 `RenderSettings.fog/skybox/...` —— 走事件让 LightManager 协调预设过渡
- **业务模块禁止**直接挂 `Volume` —— 用 `EVT_SET_*` 走全局 Volume；本地 Volume（trigger volume）属于游戏关卡设计范畴，不在框架管辖内
- **运行时创建的 VolumeProfile 不写盘** —— `ScriptableObject.CreateInstance` 创建，仅用于运行时；玩家偏好通过 `LightService` 持久化
- **预设过渡冲突**：同时多次 `ApplyPreset` 会取消上一个协程，按最新一次过渡
- **DayNight + LightManager 协作模式**：DayNight 系统只发 `EVT_PHASE_CHANGED`，LightManager 接听并 `ApplyPreset`；不要让 DayNight 直接改 `RenderSettings.fog`（§B 反模式：跨层耦合）
- **2D Light2D 与 3D Light 字典分开**：`_lights3D` / `_lights2D` 不互通；id 命名空间相同时按子系统区分调用
- **Anti-Patterns §B11（推测）**：禁止 `using UnityEngine.Rendering` / `UnityEngine.Rendering.Universal` 出现在非 LightManager 模块；外部模块走 Event 即可，无需直接 Volume API
