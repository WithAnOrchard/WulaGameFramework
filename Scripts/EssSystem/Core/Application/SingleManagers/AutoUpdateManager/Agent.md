# AutoUpdateManager

## 概述

`AutoUpdateManager`（`[Manager(-25)]` 门面）+ `AutoUpdateService`（业务状态机）提供**自动更新**。

> **设计范围**：启动时 GET 远端 `latest.json` → 与 `Application.version`（`PlayerSettings.bundleVersion`）比较 → 用户同意 → 下载完整包 ZIP → 写 PowerShell stub → 启动 stub → `Application.Quit()` → stub 等待旧进程退出 → 解压覆盖 → 重启新版本。
>
> **当前不做**：增量 / 差分更新、CDN 多镜像分流、断点续传、回滚、代码签名校验。

## 目录

```
AutoUpdateManager/
├── AutoUpdateManager.cs           门面 [Manager(-25)] + Event 入口
├── AutoUpdateService.cs           业务状态机：Idle→Checking→Available→Downloading→Downloaded→Installing
├── Agent.md                       本文档
├── Dao/
│   └── UpdateManifest.cs          远端 manifest DTO + UpdateStage 枚举
├── Runtime/
│   ├── UpdateDownloader.cs        UnityWebRequest 异步下载（带进度回调）
│   └── UpdateInstaller.cs         Windows: PowerShell stub 自替换；Mac/Linux 抛 NotSupported
└── Editor/
    └── AutoUpdateBuilder.cs       菜单 Build/EssSystem/Build Update Package
                                   — 一键 ZIP 构建产物 + 写 manifest
```

## 优先级

`AutoUpdateManager(-25)` 依赖：
- `EventProcessor(-30)` — 广播 `OnUpdate*` 事件
- `DataManager(-20)` — 持久化"已跳过版本"（晚于本模块无所谓，反正 service 启动时就读 SetData）

晚于本模块（>= -25）：
- 业务侧 GameBootstrap / 启动 UI：订阅 `OnUpdateAvailable` 弹窗、点"立即更新"调 `BeginUpdateDownload`、下载完调 `BeginUpdateInstall`

## 状态机

```
                  ┌──────┐
       (start) →  │ Idle │
                  └──┬───┘
        CheckForUpdate()│
                     ▼
                ┌────────┐  远端无新版 / 已跳过    ┌──────────┐
                │Checking├──────────────────────►│UpToDate  │
                └───┬────┘                       │/Skipped  │
                    │ 远端有新版本                 └──────────┘
                    ▼
              ┌──────────┐  用户 SkipVersion()   ┌────────┐
              │Available ├─────────────────────►│Skipped │
              └────┬─────┘                      └────────┘
                   │ BeginDownload()
                   ▼
            ┌────────────┐  网络/写盘失败        ┌────────┐
            │Downloading ├─────────────────────►│Failed  │
            └────┬───────┘                      └────────┘
                 │ 100% 落盘
                 ▼
            ┌────────────┐  BeginInstall()
            │ Downloaded ├─────────────────────► [进程退出]
            └────────────┘                          │
                                                   ▼
                                            ┌────────────┐
                                            │ Installing │
                                            └────────────┘
                                            (stub 解压 + 重启)
```

任意阶段出错都进 `Failed` 并保留 `LastError`。重置状态用 `Service.SetLocalVersion(...)` 或重新调 `CheckForUpdate()`。

## 远端 manifest 格式

```json
{
  "version": "1.2.0",
  "releaseDate": "2026-06-01",
  "downloadUrl": "https://your-cdn.example.com/updates/v1.2.0.zip",
  "checksumSha256": "abc123...64hex",
  "changelog": "- 新增 Z 轴光照\n- 修了 sprite 渲染",
  "minVersion": "1.0.0",
  "mandatory": false,
  "packageSize": 123456789,
  "extra": "any-string"
}
```

| 字段 | 必填 | 说明 |
|---|---|---|
| `version`        | ✅ | SemVer-ish，跟 `Application.version` 比，**大于**才提示 |
| `downloadUrl`    | ✅ | 完整 ZIP 包 URL（不做增量） |
| `releaseDate`    | ❌ | UI 展示用 |
| `checksumSha256` | ❌ | hex 小写 64 字符，**当前只计算不校验**（写在这供业务侧自行加） |
| `changelog`      | ❌ | 多行字符串，UI 直接显示 |
| `minVersion`     | ❌ | 低于此版本强制更新（**当前未实现**，UI 不展示也不阻止） |
| `mandatory`      | ❌ | true 时 UI 不允许 Skip（**当前未实现**，只是 DTO 字段） |
| `packageSize`    | ❌ | 字节数，UI 显示"下载 234 MB"用 |
| `extra`          | ❌ | 业务塞额外信息（CDN 镜像、灰度比例、灰度用户 ID 段等） |

> ⚠️ `minVersion` / `mandatory` / SHA256 校验目前**没接进状态机**，写进 manifest 是给后续扩展用。

## Event API

### 命令（业务侧 bare-string 调）

| 常量 | 字符串 | 参数 | 返回 |
|---|---|---|---|
| `AutoUpdateManager.EVT_CHECK_UPDATE`    | `CheckForUpdate`      | `[]` | `Ok` |
| `AutoUpdateManager.EVT_BEGIN_DOWNLOAD`  | `BeginUpdateDownload` | `[]` | `Ok` |
| `AutoUpdateManager.EVT_BEGIN_INSTALL`   | `BeginUpdateInstall`  | `[]` | `Ok` |
| `AutoUpdateManager.EVT_SKIP_VERSION`    | `SkipUpdateVersion`   | `[string version]` | `Ok` / `Fail` |

### 广播（订阅）

| 常量 | 字符串 | data |
|---|---|---|
| `AutoUpdateService.EVT_CHECK_STARTED`     | `OnUpdateCheckStarted`     | `[]` |
| `AutoUpdateService.EVT_AVAILABLE`         | `OnUpdateAvailable`        | `[UpdateManifest]` |
| `AutoUpdateService.EVT_UP_TO_DATE`        | `OnUpdateUpToDate`         | `[]` |
| `AutoUpdateService.EVT_SKIPPED`           | `OnUpdateSkipped`          | `[]` |
| `AutoUpdateService.EVT_DOWNLOAD_PROGRESS` | `OnUpdateDownloadProgress` | `[float 0..1]` |
| `AutoUpdateService.EVT_DOWNLOADED`        | `OnUpdateDownloaded`       | `[string localPath]` |
| `AutoUpdateService.EVT_INSTALLING`        | `OnUpdateInstalling`       | `[]` |
| `AutoUpdateService.EVT_FAILED`            | `OnUpdateFailed`           | `[string error]` |

广播也通过 C# event 暴露（`Service.UpdateAvailable` / `Service.DownloadProgressChanged` / ...），业务侧哪个顺手用哪个。

## 完整使用流程

### 1) 业务侧启动后，订阅 `OnUpdateAvailable` 弹窗

```csharp
[EventListener("OnUpdateAvailable")]
public List<object> OnUpdateAvailable(List<object> data)
{
    var manifest = (UpdateManifest)data[0];
    // 业务 UI 弹"发现新版本 v" + manifest.changelog + 两个按钮 [立即更新] [跳过]
    return ResultCode.Ok();
}
```

### 2) 用户点"立即更新" → 调 `BeginUpdateDownload`

```csharp
EventProcessor.Instance.TriggerEventMethod("BeginUpdateDownload");
```

### 3) 订阅 `OnUpdateDownloadProgress` 更新进度条

```csharp
[EventListener("OnUpdateDownloadProgress")]
public List<object> OnDownloadProgress(List<object> data)
{
    var p = (float)data[0];
    progressBar.value = p;
    return ResultCode.Ok();
}
```

### 4) 订阅 `OnUpdateDownloaded` → 弹"立即安装"按钮

```csharp
[EventListener("OnUpdateDownloaded")]
public List<object> OnDownloaded(List<object> data)
{
    // 显示 [立即安装] 按钮（点了调 BeginUpdateInstall）
    return ResultCode.Ok();
}
```

### 5) 用户点"立即安装" → 调 `BeginUpdateInstall`

```csharp
EventProcessor.Instance.TriggerEventMethod("BeginUpdateInstall");
```

→ Service.BeginInstall() 启动 PowerShell stub → `Application.Quit(0)`
→ 主进程退出
→ stub 等待主进程 PID 退出 → 解压覆盖 → 重启新 .exe
→ 用户看到新版本启动

## 部署侧流程（出新版）

1. **构建 Unity Player**：`File → Build Settings → Build` 选个目录（比如 `Builds/Win64/WulaGame/`）
2. **打成 ZIP + manifest**：`Build → EssSystem → Build Update Package` → 选刚才的目录 → 生成 `Build/Updates/v1.2.0.zip` + `Build/Updates/latest.json`
3. **改 baseUrl**：`Build → EssSystem → Set Update Base URL...`（或在 CI 用 sed 替换 `"downloadUrl": ".../v1.2.0.zip"` 里的 `your-cdn.example.com`）
4. **上传 CDN**：`rsync -av Build/Updates/ user@cdn:/var/www/updates/`
5. **下次玩家启动** → AutoUpdateManager.Initialize() 1 秒后 GET `latest.json` → 弹窗 → 用户更新

## 实现细节 / 注意事项

1. **运行时换皮**：Windows 锁住运行中的 `.exe` + `.dll`，所以下载完不能让游戏自己解压覆盖。改写 PowerShell stub：等 `Get-Process -Id` 拿到主进程退出再 `ZipFile.ExtractToDirectory` 覆盖 → `Start-Process` 重启。**第一次跑会被 Windows Defender / SmartScreen 弹"未知应用"警告**，需要给 powershell stub 加签名或换 .NET updater .exe。
2. **进度精度**：`UnityWebRequest.downloadProgress` 是 0..1 的 float，UpdateDownloader 每帧 `Task.Yield` 调一次进度回调，足够 UI 用；不写"已下载字节数 / 总字节数"（Unity 没暴露下载字节数 API）。
3. **失败重试**：当前**没有**自动重试。`Failed` 后只能用户点"重试"重新调 `CheckForUpdate()` / `BeginDownload()`。要自动重试在 Manager 里包一层指数退避。
4. **临时文件位置**：`Application.temporaryCachePath/AutoUpdate/` —— 每次启动会清掉（`removeFileOnAbort = true` 保证 abort 时也清）。解压成功后会主动 `Remove-Item` 删 ZIP。
5. **多语言 / 平台**：
   - 当前 stub 用 **PowerShell 5.1**（Windows 10+ 自带） + `System.IO.Compression.FileSystem` 库
   - Mac / Linux 抛 `PlatformNotSupportedException`，业务侧挂 .sh / .app updater 即可
   - 多语言只是 `changelog` 字段塞不同语言，UI 自己解析
6. **CDN 缓存**：把 `latest.json` 设为 `Cache-Control: no-cache`，ZIP 设 `Cache-Control: max-age=31536000, immutable`（带 version 哈希即可强缓存）
7. **版本号规范**：建议用 `major.minor.patch`（1.2.0），扩展到 `1.2.0-beta.1` 时 `System.Version.TryParse` 会失败，**当前会按"不更新"处理**。要支持 pre-release 改 `IsNewerVersion` 自己做 SemVer 比较。
8. **不要在 Editor 测**：Editor 模式默认 `SkipInEditor = true`，Build 完出 Player 才能验证全流程。
9. **首次运行**：`Application.temporaryCachePath` 在某些沙盒环境（macOS app bundle）可能是只读的，目前 stub 路径在 `tempPath/AutoUpdate/`，不在游戏安装目录，所以**应该**没问题。
10. **CI 集成**：`AutoUpdateBuilder.BuildUpdatePackage` 是 `public static`，可以在 build script（`build.py` / `Jenkinsfile`）里用 `-executeMethod AutoUpdateBuilder.BuildUpdatePackage` Unity 批处理模式调，然后脚本接管 baseUrl 替换 + 上传。

## 已知限制

1. **无 SHA256 校验**：`AutoUpdateService` 接收到 manifest 后**不**去对比 `checksumSha256` 和下载下来的真实哈希。要校验就自己加。
2. **无 minVersion 强制**：manifest 里 `minVersion` 字段当前**没用**。业务 UI 可以自己读 manifest.minVersion 决定是否允许"稍后再说"。
3. **无 mandatory 拦截**：同上，UI 自己处理。
4. **无断点续传**：`DownloadHandlerFile` 一次写完，断网就得重下。
5. **无并发下载**：单文件顺序下。
6. **stub 报警**：未签名的 PowerShell stub 在某些 Windows 会被 Defender / SmartScreen 拦截（虽然 `-ExecutionPolicy Bypass` 已经放行 PS 引擎本身，但第一次可能仍弹"未知发布者"）。生产环境建议：
   - 给 stub 路径加 Authenticode 签名
   - 或干脆做一个独立的 .NET updater .exe 替代 stub
7. **Mac/Linux 未实现**：抛 `PlatformNotSupportedException`。
