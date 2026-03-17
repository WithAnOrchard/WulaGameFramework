using System;
using UnityEngine;

namespace EssSystem.Core.Singleton
{
    //Unity中的可部署实体的单例模式
    public abstract class SingletonMono<T> : MonoBehaviour where T : Component
    {
        
        private static T instance;

        public Boolean logMessage = false;
        
        public Boolean hasInit=false;

        public static T Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject singletonObject = new GameObject(typeof(T).Name + "_Singleton");

                    instance = singletonObject.AddComponent<T>();
                }

                return instance;
            }
            set
            {
                instance = value;
            }
        }


        private void Start()
        {
            // 确保在场景切换时不会销毁该实例
            DontDestroyOnLoad(gameObject);
        }

        public void LogMessage(string message)
        {
            if (logMessage)
            {
                Debug.Log(message);
            }
        }

        public abstract void Init(Boolean logMessage = true);
    }
}