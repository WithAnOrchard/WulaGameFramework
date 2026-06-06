# Presentation 层指南
## 概述
`Core/Presentation/` —— 表现层管理器集合，负责 UI、角色、音频、相机、灯光、特效等视觉/听觉表现。
| Manager | 优先级 | 职责 | 文档 |
|---|---|---|---|
| **InputManager** | 2 | 输入抽象（Action + Axis + 鼠标） | `InputManager/Agent.md` |
| **AudioManager** | 3 | 音频加载/播放（BGM / SFX） | `AudioManager/Agent.md` |
| **CameraManager** | 4 | 相机控制（跟随/缩放/震动） | `CameraManager/Agent.md` |
| **UIManager** | 5 | UI 实体管理中心（Canvas + UGUI 组件树） | `UIManager/Agent.md` |
| **EffectsManager** | 6 | 特效播放（粒子系统） | `EffectsManager/Agent.md` |
| **LightManager** | 7 | 光照管理（天空盒/动态光） | `LightManager/Agent.md` |
| **CharacterManager** | 11 | 角色外观工厂 + 动作运行时（2D Sprite / 3D Prefab） | `CharacterManager/Agent.md` |
## 核心优化（Phase 2）
### 1. CharacterManager — Sprite 预加载架构
**问题**：DobeCat 宠物 Sprite 不显示，身体部分消失。
**根本原因**：
- Sprite Sheet 路径硬编码在框架中，与业务方的资源结构不匹配
- 预加载逻辑在 CharacterManager 初始化时自动运行，无法传入自定义路径
**解决方案**：
- ✅ 删除 CharacterManager 中的自动预加载逻辑
- ✅ 将 `PreloadCharacterSprites(basePath)` 改为 public 方法，接受路径参数
- ✅ 由业务方（DobeCat）显式调用，传入正确的资源路径 `"Characters/PixArt"`
- ✅ 预加载流程：扫描 CharacterConfig → 加载 Sprite Sheet → 注册子图到 SpriteService 缓存
**文件修改**：
- `CharacterManager.cs`：移除自动预加载，改为 public 方法
- `DobeCatGameManager.cs`：在 `RunAfterLogin()` 中显式调用预加载
**Sprite ID 格式规范**：
```
{category}_{variant}_{action}_{frameIndex}
例：Skin_warrior_1_Idle_0
    ↓
Sprite Sheet 路径：Characters/PixArt/Skin/warrior_1
```
### 2. UIManager — 无需更新
UIManager 架构已完善，满足当前需求：
- ✅ UIComponent DAO + UIEntity 运行时分离
- ✅ 事件驱动的属性变更通知
- ✅ Canvas / EventSystem 自动建立
- ✅ 窗口行为（拖拽/缩放）
## 文档导航
| 文件 | 内容 | 更新状态 |
|---|---|---|
| `Presentation/Agent.md` | 本文件 —— Presentation 层总体指南 | ✅ 已更新 |
| `InputManager/Agent.md` | 输入抽象、Action 绑定、鼠标/轴向查询 | ✅ 完善 |
| `AudioManager/Agent.md` | BGM/SFX 管理、音量持久化、对象池 | ✅ 完善 |
| `UIManager/Agent.md` | UI 实体管理、Canvas 自动建立、DAO 分离 | ✅ 完善 |
| `CameraManager/Agent.md` | 相机跟随、缩放、震屏、坐标转换 | ✅ 完善 |
| `LightManager/Agent.md` | URP 灯光、预设、昼夜切换、后处理 | ✅ 完善 |
| `EffectsManager/Agent.md` | 特效池化、屏幕闪光、自动回收 | ✅ 完善 |
| `CharacterManager/Agent.md` | 角色工厂、2D/3D 统一、**Sprite 预加载** | ✅ 已更新 |
## 关联文档
- **Foundation 层**：`Foundation/ResourceManager/Agent.md` —— Sprite 加载基础设施
- **Base 层**：`Base/Agent.md` —— Manager/Service 架构、事件系统
- **项目级**：`Assets/Agent.md` —— 项目总览、快速上手
## 跨模块调用示例
### CharacterManager — 创建角色 + 预加载 Sprite
```csharp
// 业务方（DobeCat）
public class DobeCatGameManager : AbstractGameManager
{
    private void RunAfterLogin()
    {
        EnsureFrameworkManagers();
        // 预加载 Sprite Sheet（业务方负责路径）
        if (CharMgr.HasInstance)
        {
            CharMgr.Instance.PreloadCharacterSprites("Characters/PixArt");
        }
        // 创建角色
        var r = EventProcessor.Instance.TriggerEventMethod(
            "CreateCharacter",
            new List<object> { "Warrior", "player_001", transform, Vector3.zero });
        if (ResultCode.IsOk(r))
        {
            var characterRoot = r[1] as Transform;
            // 角色已创建，Sprite 已从缓存加载
        }
    }
}
```
### UIManager — 注册 UI 面板
```csharp
// 业务方
var panel = new UIPanelComponent("inventory", "背包")
    .SetSize(680, 560);
panel.AddChild(new UIButtonComponent("inv_close", "关闭", "×")
    .SetSize(36, 36));
EventProcessor.Instance.TriggerEventMethod(
    "RegisterUIEntity",
    new List<object> { panel.Id, panel });
```
## 架构约束
### CharacterManager
- ✅ **职责分离**：框架提供预加载方法，业务方负责调用和路径配置
- ✅ **无硬编码路径**：资源路径由业务方通过参数传入
- ✅ **2D/3D 统一**：按 `RenderMode` 派遣，同一 API 支持两种渲染模式
### UIManager
- ✅ **DAO 与 Entity 分离**：业务方只构造 UIComponent，UIManager 负责实例化
- ✅ **事件驱动**：属性变更通过事件通知，解耦 DAO 与 Entity
- ✅ **中立返回类型**：跨模块返回 `GameObject` / `Transform`，不返 `UIEntity`
## 注意事项
- **跨模块只走 bare-string**（§4.1）；不要为读 `EVT_*` 常量而 `using` 本模块
- **CharacterManager 预加载**：不应硬编码，由业务方根据资源结构调用
- **UIManager 禁止直接创建 UI**：禁止 `gameObject.AddComponent<Canvas/Button/Text/...>`，只构造 DAO
- **Sprite 路径规范**：必须遵循 `{category}_{variant}_{action}_{frameIndex}` 格式
## 各模块快速参考
### InputManager（优先级 2）
- **职责**：统一输入抽象，支持 Action 绑定、Axis 查询、鼠标输入
- **核心 Event**：`BindInputAction` / `IsInputPressed` / `GetInputAxis` / `GetMouseScreenPosition`
- **当前实现**：基于 Legacy Input Manager；支持切换到 New Input System
- **详见**：`InputManager/Agent.md`
### AudioManager（优先级 3）
- **职责**：BGM / SFX 管理，音量持久化，SFX 对象池
- **核心 Event**：`PlayBGM` / `PlaySFX` / `SetMasterVolume` / `SetBGMVolume` / `SetSFXVolume`
- **资源加载**：通过 ResourceManager（bare-string `"GetAudioClip"`）
- **详见**：`AudioManager/Agent.md`
### UIManager（优先级 5）
- **职责**：UI 实体管理中心，Canvas 自动建立，UGUI 组件树
- **核心 Event**：`RegisterUIEntity` / `UnregisterUIEntity` / `GetUIGameObject` / `HotReloadUIConfigs`
- **架构**：UIComponent DAO + UIEntity 运行时分离
- **约束**：禁止业务方直接创建 UI，只构造 DAO
- **详见**：`UIManager/Agent.md`
### CameraManager（优先级 6）
- **职责**：相机控制，跟随/缩放/震动等效果
- **核心 Event**：`SetCameraTarget` / `SetCameraZoom` / `ShakeCamera`
- **详见**：`CameraManager/Agent.md`
### LightManager（优先级 7）
- **职责**：光照管理，天空盒、动态光控制
- **核心 Event**：`SetSkybox` / `SetAmbientLight` / `AddDynamicLight`
- **详见**：`LightManager/Agent.md`
### EffectsManager（优先级 8）
- **职责**：特效播放，粒子系统管理
- **核心 Event**：`PlayEffect` / `StopEffect` / `ClearAllEffects`
- **详见**：`EffectsManager/Agent.md`
### CharacterManager（优先级 11）
- **职责**：角色外观工厂，2D Sprite / 3D Prefab 统一管理
- **核心 Event**：`CreateCharacter` / `DestroyCharacter` / `PlayCharacterAction` / `SetCharacterScale`
- **新增方法**：`PreloadCharacterSprites(basePath)` —— 业务方负责调用
- **详见**：`CharacterManager/Agent.md`
## 最佳实践
### 1. 输入处理
```csharp
// ✅ 推荐：通过 InputManager 查询
var isJumping = EventProcessor.Instance.TriggerEventMethod(
    "IsInputPressed", new List<object> { "Jump" });
// ❌ 禁止：直接使用 UnityEngine.Input
if (Input.GetKey(KeyCode.Space)) { }
```
### 2. 音频播放
```csharp
// ✅ 推荐：通过 AudioManager 播放
EventProcessor.Instance.TriggerEventMethod(
    "PlayBGM", new List<object> { "Sound/BGM_Forest", true });
// ❌ 禁止：直接加载 AudioClip 并播放
var clip = Resources.Load<AudioClip>("Sound/BGM_Forest");
audioSource.PlayOneShot(clip);
```
### 3. UI 创建
```csharp
// ✅ 推荐：构造 UIComponent DAO，让 UIManager 实例化
var panel = new UIPanelComponent("inventory", "背包")
    .SetSize(680, 560);
EventProcessor.Instance.TriggerEventMethod(
    "RegisterUIEntity", new List<object> { panel.Id, panel });
// ❌ 禁止：直接创建 UI 组件
var canvas = new GameObject("Canvas");
canvas.AddComponent<Canvas>();
```
### 4. 角色创建 + Sprite 预加载
```csharp
// ✅ 推荐：业务方负责预加载，传入正确路径
if (CharMgr.HasInstance)
{
    CharMgr.Instance.PreloadCharacterSprites("Characters/PixArt");
}
// 创建角色
var r = EventProcessor.Instance.TriggerEventMethod(
    "CreateCharacter",
    new List<object> { "Warrior", "player_001", transform });
// ❌ 禁止：在 CharacterManager 中硬编码路径
// 不应该这样做
```
### 5. 相机控制
```csharp
// ✅ 推荐：通过 CameraManager 控制
EventProcessor.Instance.TriggerEventMethod(
    "FollowTarget", new List<object> { playerTransform, 0.5f });
// ❌ 禁止：直接操作 Camera.main
Camera.main.transform.position = playerTransform.position;
```
## 后续优化方向
1. **InputManager 优化**：完全支持 New Input System，提供兼容层
2. **AudioManager 优化**：音频混音器集成，动态压缩
3. **CameraManager 优化**：相机动画曲线库，视锥剔除优化
4. **EffectsManager 优化**：特效对象池 + 自动清理机制
5. **LightManager 优化**：动态光阴影优化，烘焙集成
