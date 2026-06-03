using System.Collections;
using System.Collections.Generic;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Util;
using UnityEngine;

namespace EssSystem.Core.Application.SingleManagers.AutoUpdateManager
{
    /// <summary>
    /// 自动更新门面。<para>
    /// 优先级 <b>-25</b>：在 EventProcessor(-30) 之后、DataManager(-20) 之前启动，
    /// 确保 GET manifest 时 EventProcessor 已就绪（要广播 EVT_*），
    /// 晚于 EventProcessor 才能订阅它的 TriggerEvent。</para>
    /// <para>对外接口全部走 §4.1 bare-string（见下方 [Event] 方法）；C# API 直接用 <see cref="AutoUpdateService"/>。</para>
    /// </summary>
    [Manager(-25)]
    public class AutoUpdateManager : Manager<AutoUpdateManager>
    {
        // ─── Event 名常量（命令 —— 业务侧调） ──────────────
        /// <summary>立刻 GET manifest 检一次。data: []</summary>
        public const string EVT_CHECK_UPDATE      = "CheckForUpdate";
        /// <summary>用户点了"立即更新"，开始下载。data: []</summary>
        public const string EVT_BEGIN_DOWNLOAD    = "BeginUpdateDownload";
        /// <summary>下载完成，用户点了"立即安装"，启动外部 updater + 退出自身。data: []</summary>
        public const string EVT_BEGIN_INSTALL     = "BeginUpdateInstall";
        /// <summary>用户选了"跳过此版本"，写入持久化。data: [string version]</summary>
        public const string EVT_SKIP_VERSION      = "SkipUpdateVersion";

        // ─── Inspector 配置 ────────────────────────────────
        [Header("Update Source")]
        [Tooltip("远端 manifest URL（latest.json 的地址）\n例如：https://cdn.example.com/updates/latest.json")]
        [SerializeField] private string _manifestUrl = "https://your-cdn.example.com/updates/latest.json";

        [Tooltip("本地版本号（留空 = 读 PlayerSettings.bundleVersion / Application.version）")]
        [SerializeField] private string _localVersionOverride = "";

        [Header("Behavior")]
        [Tooltip("启动时自动检查一次（不阻塞 —— 异步 GET）")]
        [SerializeField] private bool _autoCheckOnStart = true;

        [Tooltip("Editor 下跳过检查（仅在 Build 后生效）")]
        [SerializeField] private bool _skipInEditor = true;

        [Tooltip("启动后延迟多少秒再检查（等首场景资源加载完，避免和 AssetBundle/Addressables 抢带宽）")]
        [SerializeField] private float _startCheckDelay = 1.0f;

        public AutoUpdateService Service => AutoUpdateService.Instance;

        // ─── 生命周期 ──────────────────────────────────────
        protected override void Initialize()
        {
            base.Initialize();
            if (Service == null) { LogError("AutoUpdateService 未初始化"); return; }

            // 把 Inspector 配置喂给 Service
            Service.ManifestUrl = _manifestUrl;
            if (!string.IsNullOrEmpty(_localVersionOverride))
                Service.SetLocalVersion(_localVersionOverride);

            if (_autoCheckOnStart && ShouldCheckNow())
                StartCoroutine(DelayedCheck());
        }

        protected override void UpdateServiceInspectorInfo()
        {
            if (Service == null) return;
            Service.UpdateInspectorInfo();
            _serviceInspectorInfo = Service.InspectorInfo;
        }

        private IEnumerator DelayedCheck()
        {
            if (_startCheckDelay > 0f) yield return new WaitForSecondsRealtime(_startCheckDelay);
            Log("启动自动检查更新……", Color.cyan);
            Service.CheckForUpdate();
        }

        private bool ShouldCheckNow()
        {
#if UNITY_EDITOR
            if (_skipInEditor) return false;
#endif
            return true;
        }

        // ─── Event Methods（业务侧裸字符串调用） ────────────

        [Event(EVT_CHECK_UPDATE)]
        public List<object> CheckForUpdate(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("AutoUpdateService 未初始化");
            Service.CheckForUpdate();
            return ResultCode.Ok();
        }

        [Event(EVT_BEGIN_DOWNLOAD)]
        public List<object> BeginUpdateDownload(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("AutoUpdateService 未初始化");
            Service.BeginDownload();
            return ResultCode.Ok();
        }

        [Event(EVT_BEGIN_INSTALL)]
        public List<object> BeginUpdateInstall(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("AutoUpdateService 未初始化");
            Service.BeginInstall();
            return ResultCode.Ok();
        }

        [Event(EVT_SKIP_VERSION)]
        public List<object> SkipUpdateVersion(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("AutoUpdateService 未初始化");
            var v = data != null && data.Count > 0 ? data[0] as string : null;
            if (string.IsNullOrEmpty(v)) return ResultCode.Fail("参数无效：需要 [string version]");
            Service.SkipVersion(v);
            return ResultCode.Ok();
        }
    }
}
