using UnityEngine;

namespace Demo.DobeCat.Sys.Platform
{
    /// <summary>
    /// 桌宠帧率控制器 - 存根版本
    /// 
    /// 注意：完整的帧率控制功能已迁移到 EssSystem.Core.Platform.FrameRateController
    /// 此类为保持兼容性而保留。建议使用 EssSystem 版本：
    /// using EssSystem.Core.Platform;
    /// var controller = FrameRateController.Instance;
    /// </summary>
    public class PetFrameRateController : MonoBehaviour
    {
        public void SetFrameRate(int fps) { }
        public int GetCurrentFrameRate() => 60;
    }
}
