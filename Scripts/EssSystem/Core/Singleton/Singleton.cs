using System;
using UnityEngine;

namespace EssSystem.Core.Singleton
{
    //单例模式
    public abstract class Singleton<T> where T : class, new()
    {
        private static readonly Lazy<T> _instance = new(() =>
        {
            var instance = new T();
            (instance as Singleton<T>)?.Init(true); // 可选：如果需要初始化
            return instance;
        });

        public bool LogMessage { get; set; } = true;

        public static T Instance => _instance.Value;

        protected virtual void Init(bool logMessage)
        {
            LogMessage = logMessage;
        }

        public void Log(string message)
        {
            if (LogMessage) Debug.Log(message);
        }
    }
}