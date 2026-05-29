using EssSystem.Core.Foundation.FileLogger;
using UnityEngine;

namespace Demo.DobeCat.Sys
{
    /// <summary>
    /// DobeCat 专用文件日志。基类实现地址 <c>%AppData%/DobeCat/log.txt</c>。
    /// 实现已迁移至 <see cref="EssSystem.Core.Foundation.FileLogger.FileLogger"/>。
    /// </summary>
    public class DobeCatLogger : FileLogger
    {
        private void Reset()
        {
            _appName = "DobeCat";
        }
    }
}
