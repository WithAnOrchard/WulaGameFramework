using System;
using System.IO;
using UnityEngine;

namespace Demo.DobeCat.Sys
{
    /// <summary>
    /// 将 Unity 日志镜像写入 %AppData%/DobeCat/log.txt，方便线上排查。
    /// 超过 <see cref="MaxFileSizeBytes"/> 时自动轮转（旧文件重命名为 log.old.txt）。
    /// DESIGN.md §11 日志写入文件
    /// </summary>
    public class DobeCatLogger : MonoBehaviour
    {
        [Tooltip("单文件最大字节数，超出后轮转。默认 2 MB。")]
        [SerializeField] private long _maxFileSizeBytes = 2 * 1024 * 1024;

        private static readonly string LogDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DobeCat");

        private static readonly string LogPath    = Path.Combine(LogDir, "log.txt");
        private static readonly string LogOldPath = Path.Combine(LogDir, "log.old.txt");

        private StreamWriter _writer;

        private void OnEnable()
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                RotateIfNeeded();
                _writer = new StreamWriter(LogPath, append: true, encoding: System.Text.Encoding.UTF8)
                    { AutoFlush = true };
                _writer.WriteLine($"--- DobeCat log opened {DateTime.Now:yyyy-MM-dd HH:mm:ss} ---");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DobeCatLogger] Failed to open log file: {e.Message}");
            }
            Application.logMessageReceived += HandleLog;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= HandleLog;
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
            if (!File.Exists(LogPath)) return;
            if (new FileInfo(LogPath).Length < _maxFileSizeBytes) return;
            try
            {
                if (File.Exists(LogOldPath)) File.Delete(LogOldPath);
                File.Move(LogPath, LogOldPath);
            }
            catch { }
        }
    }
}
