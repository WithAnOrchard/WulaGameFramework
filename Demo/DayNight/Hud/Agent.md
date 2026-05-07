# Hud（昼夜求生 Demo · 抬头显示）

订阅 GameManager / BaseDefense / WaveSpawn 的广播事件，把状态实时写到 UI 文本。

## 关键文件

- `DayNightHudManager.cs` — façade；构建 UI 面板 + 订阅广播

## Event API（仅监听，无对外发出的命令）

订阅以下广播：

| 常量 | 处理器 | 用途 |
|---|---|---|
| `DayNightGameManager.EVT_PHASE_CHANGED` | `OnPhase` | 切换昼夜显示 / 回合编号 / BOSS 夜样式 |
| `BaseDefenseService.EVT_HP_CHANGED` | `OnBaseHp` | 更新据点 HP 文本 |
| `BaseDefenseService.EVT_DESTROYED` | `OnBaseDestroyed` | 据点击毁后切换为红字提示 |
| `WaveSpawnService.EVT_WAVE_STARTED` | `OnWaveStarted` | 显示当前波次/敌人总数 |
| `WaveSpawnService.EVT_WAVE_CLEARED` | `OnWaveCleared` | 显示"波次：清完" |
