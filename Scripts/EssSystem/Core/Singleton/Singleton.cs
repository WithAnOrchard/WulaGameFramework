using System;
using UnityEngine;

/// <summary>
/// Normal Class Singleton Pattern
///  singleton 
/// </summary>
/// <typeparam name="T">The type that will be singleton</typeparam>
public class SingletonNormal<T> where T : class, new()
{
    private static T _instance;
    private static readonly object _lock = new object();

    /// <summary>
    /// The singleton instance
    ///  singleton 
    /// </summary>
    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new T();
                    }
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Check if instance exists
    ///  instance 
    /// </summary>
    public static bool HasInstance
    {
        get { return _instance != null; }
    }

    /// <summary>
    /// Get instance without creating new one
    ///  instance
    /// </summary>
    public static T TryGetInstance()
    {
        return _instance;
    }

    /// <summary>
    /// Destroy the singleton instance
    ///  singleton 
    /// </summary>
    public static void DestroyInstance()
    {
        lock (_lock)
        {
            if (_instance is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _instance = null;
        }
    }

    /// <summary>
    /// 日志输出方法
    /// </summary>
    /// <param name="message">日志消息</param>
    public static void Log(string message)
    {
        Debug.Log($"[{typeof(T).Name}] {message}");
    }

    /// <summary>
    /// 警告日志输出方法
    /// </summary>
    /// <param name="message">警告消息</param>
    public static void LogWarning(string message)
    {
        Debug.LogWarning($"[{typeof(T).Name}] {message}");
    }

    /// <summary>
    /// 错误日志输出方法
    /// </summary>
    /// <param name="message">错误消息</param>
    public static void LogError(string message)
    {
        Debug.LogError($"[{typeof(T).Name}] {message}");
    }
}

/// <summary>
/// Lazy Initialization Singleton Pattern
///  singleton 
/// </summary>
/// <typeparam name="T">The type that will be singleton</typeparam>
public class SingletonLazy<T> where T : class, new()
{
    private static readonly Lazy<T> _lazyInstance = new Lazy<T>(() => new T());

    /// <summary>
    /// The singleton instance
    ///  singleton 
    /// </summary>
    public static T Instance => _lazyInstance.Value;

    /// <summary>
    /// Check if instance exists
    ///  instance 
    /// </summary>
    public static bool HasInstance => _lazyInstance.IsValueCreated;

    /// <summary>
    /// Get instance without creating new one
    ///  instance
    /// </summary>
    public static T TryGetInstance()
    {
        return HasInstance ? _lazyInstance.Value : null;
    }

    /// <summary>
    /// Destroy the singleton instance
    ///  singleton 
    /// </summary>
    public static void DestroyInstance()
    {
        if (_lazyInstance.IsValueCreated && _lazyInstance.Value is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    /// <summary>
    /// 日志输出方法
    /// </summary>
    /// <param name="message">日志消息</param>
    public static void Log(string message)
    {
        Debug.Log($"[{typeof(T).Name}] {message}");
    }

    /// <summary>
    /// 警告日志输出方法
    /// </summary>
    /// <param name="message">警告消息</param>
    public static void LogWarning(string message)
    {
        Debug.LogWarning($"[{typeof(T).Name}] {message}");
    }

    /// <summary>
    /// 错误日志输出方法
    /// </summary>
    /// <param name="message">错误消息</param>
    public static void LogError(string message)
    {
        Debug.LogError($"[{typeof(T).Name}] {message}");
    }
}
