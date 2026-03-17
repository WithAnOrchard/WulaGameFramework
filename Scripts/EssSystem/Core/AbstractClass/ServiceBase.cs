using System;
using System.Collections.Generic;
using EssSystem.Core.Dao;
using EssSystem.Core.Singleton;
namespace EssSystem.Core.AbstractClass
{
    //服务类的基类 此类所有的内容为实际逻辑 为单例模式
    public abstract class ServiceBase
    {
        // 简单封装
        protected static T Instance<T>() where T : class, new()
        {
            //在实例化之前 需要给DataService中创建命名空间这样 可以往里面塞所有的需要存储的数据
            DataService.Instance.DataSpaces.Add(typeof(T).Name,new Dictionary<string, object>());
            
            return Singleton<T>.Instance;
        }
    
        // 添加初始化
        protected static T InstanceWithInit<T>(Action<T> onCreated = null) where T : class, new()
        {
            //在实例化之前 需要给DataService中创建命名空间这样 可以往里面塞所有的需要存储的数据
            DataService.Instance.DataSpaces.Add(typeof(T).Name,new Dictionary<string, object>());
            var instance = Singleton<T>.Instance;
            onCreated?.Invoke(instance);
            return instance;
        }
    }
}