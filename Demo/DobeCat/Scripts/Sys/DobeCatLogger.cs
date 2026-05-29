using EssSystem.Core.Base.FileLogger;

namespace Demo.DobeCat.Sys
{
    /// <summary>
    /// DobeCat 专用文件日志（%AppData%/DobeCat/log.txt）。
    /// 仅设置应用名，其余由基类 FileLogger 实现。
    /// </summary>
    public class DobeCatLogger : FileLogger
    {
        private void Reset() => _appName = "DobeCat";
    }
}
