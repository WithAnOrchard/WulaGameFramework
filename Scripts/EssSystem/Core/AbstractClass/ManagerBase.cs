using System;
using EssSystem.Core.Singleton;
using UnityEngine;

namespace EssSystem.Core.AbstractClass
{
    //管理器的基类 此类所有的内容必须是使用事件来进行
    public class ManagerBase : MonoBehaviour
    {
        // 简单封装
        protected static T Instance<T>() where T : Component, new()
        {
            return SingletonMono<T>.Instance;
        }

        // 添加初始化
        protected static T InstanceWithInit<T>(Action<T> onCreated = null) where T : Component, new()
        {
            var instance = SingletonMono<T>.Instance;
            onCreated?.Invoke(instance);
            return instance;
        }
    }
}