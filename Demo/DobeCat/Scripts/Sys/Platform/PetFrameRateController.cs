using Demo.DobeCat.Sys.Platform.Windows;
using EssSystem.Core.Platform;

namespace Demo.DobeCat.Sys.Platform
{
    /// <summary>
    /// 桌宠专用帧率控制器。
    /// 通用实现已迁移至 <see cref="FrameRateController"/>；
    /// 本类重写 <see cref="IsForegroundFullscreen"/> 使用 DobeCat 的
    /// <see cref="ForegroundSensor"/>（保留场景序列化引用）。
    /// DESIGN.md §2.2 帧率策略
    /// </summary>
    public class PetFrameRateController : FrameRateController
    {
        protected override bool IsForegroundFullscreen() =>
            ForegroundSensor.Instance != null && ForegroundSensor.Instance.IsFullscreen;
    }
}
