using System;
using System.Collections.Generic;
using System.IO;
using EssSystem.Core.Application.SingleManagers.AutoUpdateManager.Dao;
using EssSystem.Core.Application.SingleManagers.AutoUpdateManager.Runtime;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Util;
using UnityEngine;

namespace EssSystem.Core.Application.SingleManagers.AutoUpdateManager
{
    /// <summary>
    /// 自动更新业务服务 —— 状态机 + 持久化（跳过版本 / 上次检查时间）。
    ///
    /// 状态机：
    ///   Idle → Checking → (Available | UpToDate)
    ///   Available → Downloading → Downloaded → Installing → [进程退出]
    ///   任意阶段 → Failed / Skipped
    ///
    /// 不直接做 I/O —— 真正的下载 / 安装分别走 <see cref="UpdateDownloader"/> 和
    /// <see cref="UpdateInstaller"/>，本类只编排状态和广播事件。
    /// </summary>
    public class AutoUpdateService : Service<AutoUpdateService>
    {
        // ─── 分类 ────────────────────────────────────────────
        public const string CAT_STATE = "UpdateState";

        // ─── 广播（Service 命名） ───────────────────────────
        public const string EVT_CHECK_STARTED     = "OnUpdateCheckStarted";
        public const string EVT_AVAILABLE         = "OnUpdateAvailable";
        public const string EVT_UP_TO_DATE        = "OnUpdateUpToDate";
        public const string EVT_DOWNLOAD_PROGRESS = "OnUpdateDownloadProgress";
        public const string EVT_DOWNLOADED        = "OnUpdateDownloaded";
        public const string EVT_INSTALLING        = "OnUpdateInstalling";
        public const string EVT_FAILED            = "OnUpdateFailed";
        public const string EVT_SKIPPED           = "OnUpdateSkipped";

        /// <summary>当前 manifest URL（运行时可改 —— 比如从设置面板切换测试服）</summary>
        public string ManifestUrl { get; set; }
        /// <summary>当前客户端版本（默认从 Application.version 读，调试时可被 SetLocalVersion 覆盖）</summary>
        public string LocalVersion { get; private set; } = UnityEngine.Application.version;
        /// <summary>当前阶段（业务 UI 直接绑这个就行）</summary>
        public UpdateStage Stage { get; private set; } = UpdateStage.Idle;
        /// <summary>远端 manifest（Available 阶段才有值）</summary>
        public UpdateManifest AvailableManifest { get; private set; }
        /// <summary>下载进度 0..1（Downloading 阶段才有值）</summary>
        public float DownloadProgress { get; private set; }
        /// <summary>已下载的本地 ZIP 完整路径</summary>
        public string DownloadedFilePath { get; private set; }
        /// <summary>最后一次错误信息（Failed 阶段才有值）</summary>
        public string LastError { get; private set; }

        // 业务侧直接订阅这些 C# event 比订阅 Service 命名的 bare-string 更省事
        public event Action<UpdateManifest> UpdateAvailable;
        public event Action                 UpToDate;
        public event Action<float>          DownloadProgressChanged;
        public event Action<string>         UpdateDownloaded;
        public event Action<string>         UpdateFailed;
        public event Action                 UpdateSkipped;

        protected override void Initialize()
        {
            base.Initialize();
            Log("AutoUpdateService 初始化完成（LocalVersion=" + LocalVersion + "）", Color.green);
        }

        public void SetLocalVersion(string v)
        {
            if (!string.IsNullOrEmpty(v)) LocalVersion = v;
        }

        // ─── 1. 检查 ─────────────────────────────────────────

        public async void CheckForUpdate()
        {
            if (string.IsNullOrEmpty(ManifestUrl))
            {
                Fail("ManifestUrl 未配置（请在 AutoUpdateManager Inspector 设置）");
                return;
            }
            Stage = UpdateStage.Checking;
            EventProcessor.Instance.TriggerEvent(EVT_CHECK_STARTED);

            try
            {
                var json = await UpdateDownloader.DownloadStringAsync(ManifestUrl);
                var manifest = JsonUtility.FromJson<UpdateManifest>(json);
                if (manifest == null || string.IsNullOrEmpty(manifest.version))
                {
                    Stage = UpdateStage.UpToDate;
                    EventProcessor.Instance.TriggerEvent(EVT_UP_TO_DATE);
                    UpToDate?.Invoke();
                    Log("manifest 解析为空或缺 version，按 UpToDate 处理", Color.yellow);
                    return;
                }

                // 用户已跳过该版本 → 视为 UpToDate
                if (IsSkipped(manifest.version))
                {
                    Stage = UpdateStage.Skipped;
                    EventProcessor.Instance.TriggerEvent(EVT_SKIPPED);
                    UpdateSkipped?.Invoke();
                    Log($"版本 {manifest.version} 已被用户跳过", Color.gray);
                    return;
                }

                if (IsNewerVersion(manifest.version, LocalVersion))
                {
                    AvailableManifest = manifest;
                    Stage = UpdateStage.Available;
                    EventProcessor.Instance.TriggerEvent(EVT_AVAILABLE, new List<object> { manifest });
                    UpdateAvailable?.Invoke(manifest);
                    Log($"发现新版本 {manifest.version}（当前 {LocalVersion}）", Color.cyan);
                }
                else
                {
                    Stage = UpdateStage.UpToDate;
                    EventProcessor.Instance.TriggerEvent(EVT_UP_TO_DATE);
                    UpToDate?.Invoke();
                    Log($"当前 {LocalVersion} 已是最新（远端 {manifest.version}）", Color.gray);
                }
            }
            catch (Exception e)
            {
                Fail("检查更新失败: " + e.Message);
            }
        }

        // ─── 2. 跳过某版本（写入持久化） ───────────────────

        public void SkipVersion(string version)
        {
            if (string.IsNullOrEmpty(version)) return;
            SetData(CAT_STATE, "skipped_version", version);
            SetData(CAT_STATE, "skipped_at", DateTime.UtcNow.ToString("o"));
            Stage = UpdateStage.Skipped;
            EventProcessor.Instance.TriggerEvent(EVT_SKIPPED);
            UpdateSkipped?.Invoke();
            Log($"已跳过版本 {version}", Color.gray);
        }

        private bool IsSkipped(string remoteVersion)
        {
            var saved = GetData<string>(CAT_STATE, "skipped_version");
            return !string.IsNullOrEmpty(saved) && saved == remoteVersion;
        }

        // ─── 3. 下载 ─────────────────────────────────────────

        public async void BeginDownload()
        {
            if (AvailableManifest == null || string.IsNullOrEmpty(AvailableManifest.downloadUrl))
            {
                Fail("没有可用更新（AvailableManifest 为空）");
                return;
            }
            Stage = UpdateStage.Downloading;
            DownloadProgress = 0;
            try
            {
                var tempDir  = Path.Combine(UnityEngine.Application.temporaryCachePath, "AutoUpdate");
                Directory.CreateDirectory(tempDir);
                var savePath = Path.Combine(tempDir, $"{AvailableManifest.version}.zip");

                await UpdateDownloader.DownloadFileAsync(
                    AvailableManifest.downloadUrl,
                    savePath,
                    onProgress: p =>
                    {
                        DownloadProgress = p;
                        EventProcessor.Instance.TriggerEvent(EVT_DOWNLOAD_PROGRESS, new List<object> { p });
                        DownloadProgressChanged?.Invoke(p);
                    });

                DownloadedFilePath = savePath;
                Stage = UpdateStage.Downloaded;
                EventProcessor.Instance.TriggerEvent(EVT_DOWNLOADED, new List<object> { savePath });
                UpdateDownloaded?.Invoke(savePath);
                Log($"下载完成 → {savePath}", Color.green);
            }
            catch (Exception e)
            {
                Fail("下载失败: " + e.Message);
            }
        }

        // ─── 4. 安装（启动外部 updater + 退出当前进程） ─────

        public void BeginInstall()
        {
            if (string.IsNullOrEmpty(DownloadedFilePath))
            {
                Fail("尚未下载（DownloadedFilePath 为空）");
                return;
            }
            Stage = UpdateStage.Installing;
            EventProcessor.Instance.TriggerEvent(EVT_INSTALLING);
            try
            {
                UpdateInstaller.InstallFromZip(DownloadedFilePath);
                // UpdateInstaller.InstallFromZip 在 Windows 上是同步启动 powershell stub 然后返回
                // stub 会等我们退出再解压；我们这里立刻退场
                Log("外部 updater 已启动，3 秒后退出当前进程……", Color.yellow);
                UnityEngine.Application.Quit(0);
            }
            catch (Exception e)
            {
                Fail("安装失败: " + e.Message);
            }
        }

        // ─── 内部 ───────────────────────────────────────────

        private void Fail(string msg)
        {
            LastError = msg;
            Stage = UpdateStage.Failed;
            EventProcessor.Instance.TriggerEvent(EVT_FAILED, new List<object> { msg });
            UpdateFailed?.Invoke(msg);
            LogError(msg);
        }

        /// <summary>SemVer-ish 比较 —— "1.2.3" &gt; "1.2.0"。解析失败按"不变"处理。</summary>
        private static bool IsNewerVersion(string remote, string local)
        {
            if (string.IsNullOrEmpty(remote)) return false;
            if (string.IsNullOrEmpty(local))   return true;
            if (!Version.TryParse(remote, out var vRemote)) return false;
            if (!Version.TryParse(local,   out var vLocal))  return true;  // local 不可解析就放行
            return vRemote > vLocal;
        }
    }
}
