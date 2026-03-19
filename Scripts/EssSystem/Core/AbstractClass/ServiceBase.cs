using System;
using System.Collections.Generic;
using EssSystem.Core.Dao;
using EssSystem.Core.Singleton;
namespace EssSystem.Core.AbstractClass
{
    //服务类的基类 此类所有的内容为实际逻辑 为单例模式
    public class ServiceBase:Singleton<ServiceBase>
    {
        //目标 根据service存取数据 
        public Dictionary<string,Dictionary<string,object>> DataSpaces=new();
        // 简单封装
        protected static T Instance<T>() where T : class, new()
        {
            return Singleton<T>.Instance;
        }
    
        // 添加初始化
        protected static T InstanceWithInit<T>(Action<T> onCreated = null) where T : class, new()
        {
            var instance = Singleton<T>.Instance;
            onCreated?.Invoke(instance);
            return instance;
        }
    }
}