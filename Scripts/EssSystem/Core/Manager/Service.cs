using System;
using System.Collections.Generic;

namespace EssSystem.Core.Manager
{
    /// <summary>
    /// Service抽象类，继承自Singleton，提供数据存储功能
    /// </summary>
    /// <typeparam name="T">Service类型</typeparam>
    public abstract class Service<T> : SingletonNormal<T> where T : class, new()
    {
        /// <summary>
        /// 数据存储字典，键为string，值为Dictionary<string, object>
        /// </summary>
        protected Dictionary<string, Dictionary<string, object>> _dataStorage;

        /// <summary>
        /// 初始化Service
        /// </summary>
        protected Service()
        {
            _dataStorage = new Dictionary<string, Dictionary<string, object>>();
            Initialize();
        }

        /// <summary>
        /// Service初始化方法，子类可重写
        /// </summary>
        protected virtual void Initialize()
        {
            // 子类可重写此方法进行初始化操作
        }

        /// <summary>
        /// 存储数据到指定分类
        /// </summary>
        /// <param name="category">数据分类</param>
        /// <param name="key">数据键</param>
        /// <param name="value">数据值</param>
        public virtual void SetData(string category, string key, object value)
        {
            if (!_dataStorage.ContainsKey(category))
            {
                _dataStorage[category] = new Dictionary<string, object>();
            }

            _dataStorage[category][key] = value;
        }

        /// <summary>
        /// 从指定分类获取数据
        /// </summary>
        /// <param name="category">数据分类</param>
        /// <param name="key">数据键</param>
        /// <returns>数据值，如果不存在返回null</returns>
        public virtual object GetData(string category, string key)
        {
            if (_dataStorage.ContainsKey(category) && _dataStorage[category].ContainsKey(key))
            {
                return _dataStorage[category][key];
            }
            return null;
        }

        /// <summary>
        /// 获取指定类型的数据
        /// </summary>
        /// <typeparam name="TValue">数据类型</typeparam>
        /// <param name="category">数据分类</param>
        /// <param name="key">数据键</param>
        /// <returns>指定类型的数据值，如果不存在或类型不匹配返回默认值</returns>
        public virtual TValue GetData<TValue>(string category, string key)
        {
            object value = GetData(category, key);
            if (value is TValue typedValue)
            {
                return typedValue;
            }
            return default(TValue);
        }

        /// <summary>
        /// 检查指定数据是否存在
        /// </summary>
        /// <param name="category">数据分类</param>
        /// <param name="key">数据键</param>
        /// <returns>是否存在</returns>
        public virtual bool HasData(string category, string key)
        {
            return _dataStorage.ContainsKey(category) && _dataStorage[category].ContainsKey(key);
        }

        /// <summary>
        /// 移除指定数据
        /// </summary>
        /// <param name="category">数据分类</param>
        /// <param name="key">数据键</param>
        /// <returns>是否成功移除</returns>
        public virtual bool RemoveData(string category, string key)
        {
            if (_dataStorage.ContainsKey(category))
            {
                return _dataStorage[category].Remove(key);
            }
            return false;
        }

        /// <summary>
        /// 清空指定分类的所有数据
        /// </summary>
        /// <param name="category">数据分类</param>
        public virtual void ClearCategory(string category)
        {
            if (_dataStorage.ContainsKey(category))
            {
                _dataStorage[category].Clear();
            }
        }

        /// <summary>
        /// 清空所有数据
        /// </summary>
        public virtual void ClearAll()
        {
            _dataStorage.Clear();
        }

        /// <summary>
        /// 获取所有分类
        /// </summary>
        /// <returns>分类列表</returns>
        public virtual IEnumerable<string> GetCategories()
        {
            return _dataStorage.Keys;
        }

        /// <summary>
        /// 获取指定分类的所有键
        /// </summary>
        /// <param name="category">数据分类</param>
        /// <returns>键列表</returns>
        public virtual IEnumerable<string> GetKeys(string category)
        {
            if (_dataStorage.ContainsKey(category))
            {
                return _dataStorage[category].Keys;
            }
            return new List<string>();
        }

        /// <summary>
        /// 获取指定分类的数据数量
        /// </summary>
        /// <param name="category">数据分类</param>
        /// <returns>数据数量</returns>
        public virtual int GetCategoryCount(string category)
        {
            return _dataStorage.ContainsKey(category) ? _dataStorage[category].Count : 0;
        }

        /// <summary>
        /// 获取所有数据数量
        /// </summary>
        /// <returns>总数据数量</returns>
        public virtual int GetAllDataCount()
        {
            int count = 0;
            foreach (var category in _dataStorage.Values)
            {
                count += category.Count;
            }
            return count;
        }
    }
}
