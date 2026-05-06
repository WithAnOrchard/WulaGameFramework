# WaveSpawn（昼夜求生 Demo · 波次刷怪）

夜晚分批生成敌人 → 全部清完 → 等下一波 / 进入白天。

## 关键文件

- `WaveSpawnService.cs` — Service 单例；维护当前波次状态、计时器、敌人计数；广播 START/CLEARED
- 不直接 `using EntityManager`，通过 `EXT_CREATE_ENTITY = "CreateEntity"` 字符串协议跨模块创建实体

## Event API

| 常量 | 字符串 | 类型 | 参数 | 说明 |
|---|---|---|---|---|
| `WaveSpawnService.EVT_WAVE_STARTED` | `OnWaveStarted` | 广播 | `[int round, int waveIndex, int totalEnemies]` | 一波敌人开始生成时 |
| `WaveSpawnService.EVT_WAVE_CLEARED` | `OnWaveCleared` | 广播 | `[int round, int waveIndex]` | 一波清完（场上 0 敌人）时 |
