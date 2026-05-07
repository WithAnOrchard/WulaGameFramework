# BaseDefense（昼夜求生 Demo · 据点防御）

玩家核心据点 HP / 击毁状态管理。命令由 `BaseDefenseManager`（façade）入口，状态/广播在 `BaseDefenseService`。

## 关键文件

- `BaseDefenseManager.cs` — façade，挂场景；命令事件入口
- `BaseDefenseService.cs` — Service 单例；维护 HP，广播变更

## Event API

| 常量 | 字符串 | 类型 | 参数 / 返回 | 说明 |
|---|---|---|---|---|
| `BaseDefenseManager.EVT_DAMAGE_BASE` | `DamageBase` | 命令 | `[int amount]` → `ResultCode` | 对据点造成 amount 点伤害 |
| `BaseDefenseManager.EVT_RESET_BASE` | `ResetBase` | 命令 | 无参 → `ResultCode` | 重置 HP 到 `MaxHp` |
| `BaseDefenseService.EVT_HP_CHANGED` | `OnBaseHpChanged` | 广播 | `[int currentHp, int maxHp, int delta]` | HP 变化时广播；HUD 用之刷新 |
| `BaseDefenseService.EVT_DESTROYED` | `OnBaseDestroyed` | 广播 | 无参 | HP 归零时广播一次；可触发 GameOver |
