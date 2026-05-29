using System;
using System.IO;
using UnityEngine;

namespace EssSystem.Core.Base.FileLogger
{
    /// <summary>
    /// 通用文件日志组件：将 Unity 日志镜像写入 <c>%AppData%/{AppName}/log.txt</c>。
    /// <para>超过 <see cref="MaxFileSizeBytes"/> 时自动轮转（保留最近 3 个日志文件）。</para>
    /// <para>支持日志级别过滤和自动清理过期日志。</para>
    /// <para>挂在场景根 GameObject 上；通过 <see cref="AppName"/> Inspector 字段指定应用目录名。</para>
    /// </summary>
    public class FileLogger : MonoBehaviour
    {
        [Tooltip("日志目录名（写入 %AppData%/{AppName}/log.txt）。")]
        [SerializeField] protected string _appName = "App";

        [Tooltip("单文件最大字节数，超出后轮转。默认 2 MB。")]
        [SerializeField] private long _maxFileSizeBytes = 2 * 1024 * 1024;

        [Tooltip("最小日志级别（低于此级别的日志不会被记录）。")]
        [SerializeField] private LogType _minLogLevel = LogType.Warning;

        [Tooltip("保留日志的最大天数（超过此天数的日志会被自动删除）。")]
        [SerializeField] private int _maxLogAgeDays = 7;

        private const int MAX_LOG_FILES = 3;  // 最多保留 3 个日志文件

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
                CleanupOldLogs();  // 清理过期日志
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
            
            // 日志级别过滤
            if (type < _minLogLevel) return;
            
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
                // 多文件轮转：删除最旧的日志，其他日志依次递进
                for (int i = MAX_LOG_FILES - 1; i >= 1; i--)
                {
                    var oldPath = Path.Combine(_logDir, $"log.{i}.txt");
                    var newPath = Path.Combine(_logDir, $"log.{i + 1}.txt");
                    if (File.Exists(oldPath))
                    {
                        if (File.Exists(newPath)) File.Delete(newPath);
                        File.Move(oldPath, newPath);
                    }
                }
                
                // 当前日志重命名为 log.1.txt
                var log1Path = Path.Combine(_logDir, "log.1.txt");
                if (File.Exists(log1Path)) File.Delete(log1Path);
                File.Move(_logPath, log1Path);
                Debug.Log($"[FileLogger] 日志文件已轮转，保留最近 {MAX_LOG_FILES} 个文件");
            }
            catch { }
        }

        private void CleanupOldLogs()
        {
            try
            {
                var now = DateTime.Now;
                var files = Directory.GetFiles(_logDir, "log*.txt");
                
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if ((now - fileInfo.LastWriteTime).TotalDays > _maxLogAgeDays)
                    {
                        File.Delete(file);
                        Debug.Log($"[FileLogger] 删除过期日志: {Path.GetFileName(file)}");
                    }
                }
            }
            catch { }
        }
    }
}
