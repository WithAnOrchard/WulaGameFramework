using UnityEngine;

/// <summary>
/// Unity MonoBehaviour Singleton Pattern with Mono suffix
///  MonoBehaviour  singleton 
/// </summary>
/// <typeparam name="T">The type that will be singleton</typeparam>
public class SingletonMono<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static readonly object _lock = new object();
    private static bool _applicationIsQuitting = false;

    /// <summary>
    /// The singleton instance
    ///  singleton 
    /// </summary>
    public static T Instance
    {
        get
        {
            if (_applicationIsQuitting)
            {
                Debug.LogWarning($"[SingletonMono] 实例 '{typeof(T)}' 已在应用程序退出时销毁。不会再次创建 - 返回null。");
                return null;
            }

            lock (_lock)
            {
                if (_instance == null)
                {
                    _instance = (T)FindObjectOfType(typeof(T));

                    if (FindObjectsOfType(typeof(T)).Length > 1)
                    {
                        Debug.LogError($"[SingletonMono] 出现严重错误 - 不应该有超过1个单例！重新打开场景可能会修复此问题。");
                        return _instance;
                    }

                    if (_instance == null)
                    {
                        GameObject singleton = new GameObject();
                        _instance = singleton.AddComponent<T>();
                        singleton.name = $"(singleton_mono) {typeof(T)}";

                        DontDestroyOnLoad(singleton);

                        Debug.Log($"[SingletonMono] 场景中需要 {typeof(T)} 的实例，因此创建了 '{singleton}' 并设置了DontDestroyOnLoad。");
                    }
                    else
                    {
                        Debug.Log($"[SingletonMono] 使用已创建的实例: {_instance.gameObject.name}");
                    }
                }

                return _instance;
            }
        }
    }

    /// <summary>
    /// Called when the instance is created
     ///  
    /// </summary>
    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this as T;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Called when the application is quitting
     ///  
    /// </summary>
    private void OnApplicationQuit()
    {
        _applicationIsQuitting = true;
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
    /// 日志输出方法
    /// </summary>
    /// <param name="message">日志消息</param>
    /// <param name="color">日志颜色</param>
    protected virtual void Log(string message, Color color = default)
    {
        if (color == default) color = Color.white;
        Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>[{typeof(T).Name}] {message}</color>");
    }

    /// <summary>
    /// 警告日志输出方法
    /// </summary>
    /// <param name="message">警告消息</param>
    protected virtual void LogWarning(string message)
    {
        Debug.LogWarning($"[{typeof(T).Name}] {message}");
    }

    /// <summary>
    /// 错误日志输出方法
    /// </summary>
    /// <param name="message">错误消息</param>
    protected virtual void LogError(string message)
    {
        Debug.LogError($"[{typeof(T).Name}] {message}");
    }
}
