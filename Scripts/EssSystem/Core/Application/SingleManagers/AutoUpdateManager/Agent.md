# AutoUpdateManager 更新模块

## 职责
- 负责版本检查、更新包下载/安装流程，以及构建生成的更新产物。
- 模块路径：`Scripts/EssSystem/Core/Application/SingleManagers/AutoUpdateManager`。
- 本文档只记录模块契约，具体实现细节以代码为准。

## 结构
- `Dao/`
- `Editor/`
- `Runtime/`
- `AutoUpdateManager.cs`
- `AutoUpdateService.cs`

## 边界
- 本模块只拥有“职责”中描述的行为，不隐式接管兄弟模块职责。
- 跨模块协作优先使用 EventProcessor 字符串协议，或目标模块明确暴露的窄接口。
- Demo 专属逻辑留在 Demo 目录，除非已经确认可复用为框架能力。

## Event API
- `AutoUpdateManager.EVT_BEGIN_DOWNLOAD` = `"BeginUpdateDownload"`
- `AutoUpdateManager.EVT_BEGIN_INSTALL` = `"BeginUpdateInstall"`
- `AutoUpdateManager.EVT_CHECK_UPDATE` = `"CheckForUpdate"`
- `AutoUpdateManager.EVT_SKIP_VERSION` = `"SkipUpdateVersion"`
- `AutoUpdateService.EVT_AVAILABLE` = `"OnUpdateAvailable"`
- `AutoUpdateService.EVT_CHECK_STARTED` = `"OnUpdateCheckStarted"`
- `AutoUpdateService.EVT_DOWNLOAD_PROGRESS` = `"OnUpdateDownloadProgress"`
- `AutoUpdateService.EVT_DOWNLOADED` = `"OnUpdateDownloaded"`
- `AutoUpdateService.EVT_FAILED` = `"OnUpdateFailed"`
- `AutoUpdateService.EVT_INSTALLING` = `"OnUpdateInstalling"`
- `AutoUpdateService.EVT_SKIPPED` = `"OnUpdateSkipped"`
- `AutoUpdateService.EVT_UP_TO_DATE` = `"OnUpdateUpToDate"`

## 维护注意
- 新增、改名或删除事件常量时，同步更新本节和根目录 Events.md。
- 示例保持最小化；实现细节写在代码注释里，模块契约写在本文档里。
- 已完成的 TODO 从本文档移除，必要时移动到 TODO.md。
