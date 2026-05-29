using System;
using System.IO;
using UnityEngine;

namespace EssSystem.Core.Foundation.FileLogger
{
    /// <summary>
    /// 通用文件日志组件：将 Unity 日志镜像写入 <c>%AppData%/{AppName}/log.txt</c>。
    /// <para>超过 <see cref="MaxFileSizeBytes"/> 时自动轮转（旧文件重命名为 log.old.txt）。</para>
    /// <para>挂在场景根 GameObject 上；通过 <see cref="AppName"/> Inspector 字段指定应用目录名。</para>
    /// </summary>
    public class FileLogger : MonoBehaviour
    {
        [Tooltip("日志目录名（写入 %AppData%/{AppName}/log.txt）。")]
        [SerializeField] protected string _appName = "App";

        [Tooltip("单文件最大字节数，超出后轮转。默认 2 MB。")]
        [SerializeField] private long _maxFileSizeBytes = 2 * 1024 * 1024;

        private StreamWriter _writer;
        private string _logDir;
        private string _logPath;
        private string _logOldPath;

        protected virtual void OnEnable()
        {
            _logDir     = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), _appName);
            _logPath    = Path.Combine(_logDir, "log.txt");
            _logOldPath = Path.Combine(_logDir, "log.old.txt");
            try
            {
                Directory.CreateDirectory(_logDir);
                RotateIfNeeded();
                _writer = new StreamWriter(_logPath, append: true, encoding: System.Text.Encoding.UTF8)
                    { AutoFlush = true };
                _writer.WriteLine($"--- {_appName} log opened {DateTime.Now:yyyy-MM-dd HH:mm:ss} ---");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FileLogger] Failed to open log file: {e.Message}");
            }
            UnityEngine.Application.logMessageReceived += HandleLog;
        }

        protected virtual void OnDisable()
        {
            UnityEngine.Application.logMessageReceived -= HandleLog;
            try { _writer?.Close(); } catch { }
            _writer = null;
        }

        private void HandleLog(string condition, string stackTrace, LogType type)
        {
            if (_writer == null) return;
            try
            {
                var prefix = type switch
                {
                    LogType.Error     => "[ERR]",
                    LogType.Assert    => "[AST]",
                    LogType.Warning   => "[WRN]",
                    LogType.Exception => "[EXC]",
                    _                 => "[LOG]",
                };
                _writer.WriteLine($"{DateTime.Now:HH:mm:ss} {prefix} {condition}");
                if (type == LogType.Error || type == LogType.Exception)
                    _writer.WriteLine(stackTrace);
            }
            catch { }
        }

        private void RotateIfNeeded()
        {
            if (!File.Exists(_logPath)) return;
            if (new FileInfo(_logPath).Length < _maxFileSizeBytes) return;
            try
            {
                if (File.Exists(_logOldPath)) File.Delete(_logOldPath);
                File.Move(_logPath, _logOldPath);
            }
            catch { }
        }
    }
}
