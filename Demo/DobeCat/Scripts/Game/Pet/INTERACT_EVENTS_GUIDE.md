# DobeCat 互动事件系统指南

> 完整的 B 站直播互动事件钩子系统，支持进入、关注、特别关注、互相关注、分享、点赞等所有互动类型。

---

## 📋 支持的互动类型

| 互动类型 | 枚举值 | 说明 | 气泡文本示例 |
|---|---|---|---|
| **进入** | `InteractTypeEnum.Enter` | 用户进入直播间 | "欢迎 {用户} 进入直播间！" |
| **关注** | `InteractTypeEnum.Follow` | 用户关注主播 | "感谢 {用户} 的关注！" |
| **特别关注** | `InteractTypeEnum.SpecialFollow` | 用户特别关注主播 | "🌟 {用户} 特别关注了主播！" |
| **互相关注** | `InteractTypeEnum.MutualFollow` | 用户与主播互相关注 | "🤝 {用户} 和主播互相关注了！" |
| **分享** | `InteractTypeEnum.Share` | 用户分享直播间 | "感谢 {用户} 分享直播间！" |
| **点赞** | `InteractTypeEnum.Like` | 用户给主播点赞 | "👍 {用户} 给主播点赞了！" |

---

## 🔧 实现原理

### 事件流程

```
B 站弹幕协议
    ↓
INTERACT_WORD / OPEN_LIVEROOM_INTERACT_WORD / LIVE_OPEN_PLATFORM_LIVE_ROOM_ENTER
    ↓
DanmakuModel (MsgType=Interact, InteractType=具体类型)
    ↓
DanmuService.EVT_RAW 广播
    ↓
PetReactionController.HandleRawDanmu()
    ↓
HandleInteractEvent() 分发
    ↓
PetSpeechBubble.Show() 显示气泡
```

### 核心代码位置

- **事件监听**：`PetReactionController.OnEnable()` 注册 `DanmuService.EVT_RAW`
- **事件处理**：`PetReactionController.HandleRawDanmu()` 过滤互动事件
- **具体逻辑**：`PetReactionController.HandleInteractEvent()` 按类型分发
- **气泡显示**：`PetSpeechBubble.Show()` 渲染文字

---

## 🎨 自定义互动反应

### 方案 1：修改气泡文本

直接编辑 `HandleInteractEvent()` 中的 `message` 赋值：

```csharp
case InteractTypeEnum.Enter:
    // 修改为自定义文本
    message = $"🎉 欢迎 {userName} 来陪伴我们！";
    break;
```

### 方案 2：添加自定义动画

在 `HandleInteractEvent()` 中调用自定义方法：

```csharp
case InteractTypeEnum.Follow:
    message = $"感谢 {userName} 的关注！";
    // 触发特殊动画
    TriggerFollowAnimation();
    break;

private void TriggerFollowAnimation()
{
    // 调用 PetAiController 或其他动画控制器
    // 例如：播放"开心"动作、特效粒子等
}
```

### 方案 3：添加音效

```csharp
case InteractTypeEnum.SpecialFollow:
    message = $"🌟 {userName} 特别关注了主播！";
    PetSoundController.PlaySpecial(); // 播放特殊音效
    break;
```

### 方案 4：扩展新的互动类型

如果 B 站后续添加新的互动类型，只需在 `switch` 中添加新 `case`：

```csharp
case InteractTypeEnum.NewType:
    message = $"新互动类型：{userName}";
    break;
```

---

## 📊 配置参数

在 Inspector 中可配置的参数：

| 参数 | 默认值 | 说明 |
|---|---|---|
| `_interactDuration` | 4f | 互动气泡显示时长（秒） |
| `_danmuCooldown` | 5f | 弹幕冷却时间（秒） |
| `_danmuShowChance` | 0.3 | 弹幕显示概率（0-1） |

---

## 🔗 相关事件

### DanmuService 事件常量

```csharp
// 高级事件（已处理的特定类型）
DanmuService.EVT_DANMAKU      // 普通弹幕
DanmuService.EVT_GIFT         // 礼物
DanmuService.EVT_SC           // Super Chat
DanmuService.EVT_CONNECTED    // 连接成功
DanmuService.EVT_DISCONNECTED // 连接断开

// 原始事件（所有类型，含互动）
DanmuService.EVT_RAW          // 原始弹幕（包含互动事件）
```

### 直播状态事件

```csharp
LiveStatusService.EVT_LIVE_STARTED // 开播
LiveStatusService.EVT_LIVE_ENDED   // 下播
```

---

## 📝 扩展示例

### 示例 1：为每种互动类型添加不同的音效

```csharp
private void HandleInteractEvent(DanmakuModel dm)
{
    var userName = dm.UserName ?? "某位观众";
    string message = null;
    string soundType = null;

    switch (dm.InteractType.Value)
    {
        case InteractTypeEnum.Enter:
            message = $"欢迎 {userName} 进入直播间！";
            soundType = "enter";
            break;
        case InteractTypeEnum.Follow:
            message = $"感谢 {userName} 的关注！";
            soundType = "follow";
            break;
        // ... 其他类型
    }

    if (!string.IsNullOrEmpty(message))
    {
        PetSpeechBubble.Instance.Show(message, _interactDuration);
        if (!string.IsNullOrEmpty(soundType))
            PetSoundController.PlayInteractSound(soundType);
    }
}
```

### 示例 2：统计互动数据

```csharp
private Dictionary<InteractTypeEnum, int> _interactStats = new();

private void HandleInteractEvent(DanmakuModel dm)
{
    // 统计互动
    if (!_interactStats.ContainsKey(dm.InteractType.Value))
        _interactStats[dm.InteractType.Value] = 0;
    _interactStats[dm.InteractType.Value]++;

    // ... 显示气泡
}

public void PrintInteractStats()
{
    foreach (var kvp in _interactStats)
        Debug.Log($"{kvp.Key}: {kvp.Value} 次");
}
```

### 示例 3：互动触发特殊奖励

```csharp
private void HandleInteractEvent(DanmakuModel dm)
{
    var userName = dm.UserName ?? "某位观众";
    string message = null;

    switch (dm.InteractType.Value)
    {
        case InteractTypeEnum.SpecialFollow:
            message = $"🌟 {userName} 特别关注了主播！";
            // 触发特殊奖励
            AwardSpecialFollowBonus(dm.UserID_long);
            break;
        // ... 其他类型
    }

    if (!string.IsNullOrEmpty(message))
        PetSpeechBubble.Instance.Show(message, _interactDuration);
}

private void AwardSpecialFollowBonus(long uid)
{
    // 例如：给予额外银币、解锁特殊装扮等
    Debug.Log($"[互动奖励] UID {uid} 获得特别关注奖励");
}
```

---

## 🚀 快速开始

1. **确保 PetReactionController 已启用**
   - 挂在任意 MonoBehaviour 上（通常在 DobeCatGameManager）
   - 确保 `OnEnable()` 被调用

2. **配置 Inspector 参数**
   - 调整 `_interactDuration` 控制气泡显示时长
   - 调整 `_danmuCooldown` 控制弹幕频率

3. **测试互动事件**
   - 在 B 站直播间进行互动（进入/关注/分享等）
   - 观察桌宠头顶气泡反应

4. **自定义反应**
   - 编辑 `HandleInteractEvent()` 中的 `message` 文本
   - 或添加自定义动画/音效逻辑

---

## ⚙️ 技术细节

### 事件数据结构

```csharp
// DanmakuModel 中的互动相关字段
public MsgTypeEnum MsgType;           // 消息类型（Interact）
public InteractTypeEnum? InteractType; // 互动类型（Enter/Follow/等）
public string UserName;               // 用户名
public long UserID_long;              // 用户 UID
```

### 互动类型枚举

```csharp
public enum InteractTypeEnum
{
    Enter = 1,           // 进入
    Follow = 2,          // 关注
    Share = 3,           // 分享
    SpecialFollow = 4,   // 特别关注
    MutualFollow = 5,    // 互相关注
    Like = 6,            // 点赞
}
```

---

## 📚 相关文档

- `DESIGN.md` §10.2 - 直播互动设计
- `DESIGN_STATUS.md` - 功能完成度分析
- `PetReactionController.cs` - 实现代码
- `PetSpeechBubble.cs` - 气泡显示组件

---

## 💡 最佳实践

1. **保持气泡文本简洁** - 最多 30 字，避免刷屏
2. **使用 emoji 增强视觉** - 但不要过度
3. **合理设置冷却时间** - 避免频繁触发
4. **预留扩展空间** - 在 `default` case 中处理未知类型
5. **性能考虑** - 避免在互动处理中执行重操作

---

## 🔄 后续扩展方向

- [ ] 互动触发特殊动画
- [ ] 互动统计与排行榜
- [ ] 互动触发游戏内奖励
- [ ] 互动事件录制与回放
- [ ] 自定义互动文本配置文件
- [ ] 互动事件多人同步（Mirror）

