using System;

namespace EssSystem.Core.Application.SingleManagers.AutoUpdateManager.Dao
{
    /// <summary>
    /// 远端 manifest JSON 反序列化目标。
    ///
    /// 部署约定（业务侧自建静态文件服务即可，例如 GitHub Releases / Nginx）：
    /// <code>
    /// https://your-cdn.example.com/updates/latest.json      ← AutoUpdate 启动时 GET
    /// https://your-cdn.example.com/updates/v1.2.0.zip        ← manifest.downloadUrl
    /// </code>
    /// </summary>
    [Serializable]
    public class UpdateManifest
    {
        /// <summary>"1.2.0" —— 与 PlayerSettings.bundleVersion 比较</summary>
        public string version;
        /// <summary>ISO 日期 "2026-06-01"，仅展示用</summary>
        public string releaseDate;
        /// <summary>完整包 ZIP 下载地址</summary>
        public string downloadUrl;
        /// <summary>SHA256 校验（hex 小写 64 字符）—— 暂时只校验不强制</summary>
        public string checksumSha256;
        /// <summary>更新日志（多行字符串，业务 UI 直接展示）</summary>
        public string changelog;
        /// <summary>最低允许版本（低于此版本强制更新；留空 = 不限）</summary>
        public string minVersion;
        /// <summary>true = 强制更新，UI 不允许 Skip</summary>
        public bool mandatory;
        /// <summary>包字节数（UI 显示"下载 234 MB"用）</summary>
        public long packageSize;

        /// <summary>业务侧可塞额外字段（CDN 镜像、回滚地址、灰度比例等）</summary>
        public string extra;
    }

    /// <summary>
    /// AutoUpdate 状态机阶段。订阅 Service.Stage 即可知道当前在哪一步。
    /// </summary>
    public enum UpdateStage
    {
        /// <summary>空闲 / 尚未检查</summary>
        Idle            = 0,
        /// <summary>正在 GET manifest</summary>
        Checking        = 1,
        /// <summary>发现新版，等待用户决策</summary>
        Available       = 2,
        /// <summary>用户同意，下载中</summary>
        Downloading     = 3,
        /// <summary>下载完成，等用户点"立即安装"</summary>
        Downloaded      = 4,
        /// <summary>启动外部 updater，自身即将退出</summary>
        Installing      = 5,
        /// <summary>已是最新版本</summary>
        UpToDate        = 6,
        /// <summary>用户拒绝 / 已跳过该版本</summary>
        Skipped         = 7,
        /// <summary>网络 / 解析 / 下载 / 安装失败</summary>
        Failed          = 8,
    }
}
