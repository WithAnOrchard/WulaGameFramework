using System;
using System.Collections.Generic;
using System.Linq;
using EssSystem.Core.EssManagers.Presentation.UIManager.Dao;
using EssSystem.Core.EssManagers.Presentation.UIManager.Dao.CommonComponents;
using EssSystem.Core.Event;
using UnityEngine;
// 本文件为中立 DAO 层——不 <c>using</c> UIManager 模块。广播属性变更走 EVT_DAO_PROPERTY_CHANGED 事件。

namespace EssSystem.Core.EssManagers.Presentation.UIManager.Dao
{
    /// <summary>
    ///     UI组件 - 所有UI元素的抽象基类
    /// </summary>
    public abstract class UIComponent : Adjustable
    {
        #region Constructor

        /// <summary>
        ///     构造函数
        /// </summary>
        /// <param name="id">组件唯一ID</param>
        /// <param name="type">组件类型</param>
        /// <param name="name">组件名称</param>
        protected UIComponent(string id, UIType type, string name = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Type = type;
            _name = name ?? id;
            _children = new Dictionary<string, UIComponent>();
        }

        #endregion

        #region Properties

        /// <summary>
        ///     组件类型
        /// </summary>
        public UIType Type { get; }

        /// <summary>
        ///     组件唯一ID
        /// </summary>
        public string Id { get; }

        /// <summary>
        ///     组件名称
        /// </summary>
        private string _name;

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnNameChanged(value);
                }
            }
        }

        /// <summary>
        ///     父组件
        /// </summary>
        [SerializeField] [HideInInspector] private UIComponent _parent;

        public UIComponent Parent
        {
            get => _parent;
            private set => _parent = value;
        }

        /// <summary>
        ///     子组件列表
        /// </summary>
        [NonSerialized] private readonly Dictionary<string, UIComponent> _children;

        /// <summary>
        ///     是否可见
        /// </summary>
        private bool _visible = true;

        public bool Visible
        {
            get => _visible;
            set
            {
                if (_visible != value)
                {
                    _visible = value;
                    OnVisibleChanged(value);
                }
            }
        }

        /// <summary>
        ///     是否可交互
        /// </summary>
        private bool _interactable = true;

        public bool Interactable
        {
            get => _interactable;
            set
            {
                if (_interactable != value)
                {
                    _interactable = value;
                    OnInteractableChanged(value);
                }
            }
        }

        #endregion

        #region Child Management

        /// <summary>
        ///     添加子组件
        /// </summary>
        /// <param name="child">子组件</param>
        /// <returns>当前组件，支持链式调用</returns>
        public UIComponent AddChild(UIComponent child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));

            if (child._parent != null)
                child._parent.RemoveChild(child.Id);

            child._parent = this;
            _children[child.Id] = child;
            return this;
        }

        /// <summary>
        ///     移除子组件
        /// </summary>
        /// <param name="childId">子组件ID</param>
        /// <returns>当前组件，支持链式调用</returns>
        public UIComponent RemoveChild(string childId)
        {
            if (_children.TryGetValue(childId, out var child))
            {
                _children.Remove(childId);
                child._parent = null;
            }

            return this;
        }

        /// <summary>
        ///     获取子组件
        /// </summary>
        /// <param name="childId">子组件ID</param>
        /// <returns>子组件，不存在返回null</returns>
        public UIComponent GetChild(string childId)
        {
            return _children.TryGetValue(childId, out var child) ? child : null;
        }

        /// <summary>
        ///     根据ID链获取组件
        /// </summary>
        /// <param name="idPath">ID路径，如 "parent/child/grandchild"</param>
        /// <returns>目标组件，不存在返回null</returns>
        public UIComponent GetComponent(string idPath)
        {
            if (string.IsNullOrEmpty(idPath))
                return null;

            var pathParts = idPath.Split('/');
            var current = this;

            foreach (var part in pathParts)
                if (part == "..")
                {
                    current = current._parent;
                    if (current == null)
                        return null;
                }
                else if (part == ".")
                {
                }
                else
                {
                    current = current.GetChild(part);
                    if (current == null)
                        return null;
                }

            return current;
        }

        /// <summary>
        ///     链式获取并修改组件
        /// </summary>
        /// <param name="idPath">ID路径</param>
        /// <param name="modifyAction">修改操作</param>
        /// <returns>当前组件，支持链式调用</returns>
        public UIComponent ModifyComponent(string idPath, Action<UIComponent> modifyAction)
        {
            var target = GetComponent(idPath);
            if (target != null && modifyAction != null) modifyAction(target);
            return this;
        }

        /// <summary>
        ///     获取所有子组件
        /// </summary>
        /// <returns>子组件集合</returns>
        public IEnumerable<UIComponent> GetChildren()
        {
            return _children.Values;
        }

        #endregion

        #region Type Conversion

        /// <summary>
        ///     转换为按钮组件
        /// </summary>
        /// <returns>按钮组件，如果不是按钮类型返回null</returns>
        public UIButtonComponent AsButton()
        {
            return Type == UIType.Button ? this as UIButtonComponent : null;
        }

        /// <summary>
        ///     转换为面板组件
        /// </summary>
        /// <returns>面板组件，如果不是面板类型返回null</returns>
        public UIPanelComponent AsPanel()
        {
            return Type == UIType.Panel ? this as UIPanelComponent : null;
        }

        /// <summary>
        ///     转换为文本组件
        /// </summary>
        /// <returns>文本组件，如果不是文本类型返回null</returns>
        public UITextComponent AsText()
        {
            return Type == UIType.Text ? this as UITextComponent : null;
        }

        public UIBarComponent AsBar()
        {
            return Type == UIType.Bar ? this as UIBarComponent : null;
        }

        /// <summary>
        ///     检查是否为按钮类型
        /// </summary>
        /// <returns>是否为按钮类型</returns>
        public bool IsButton()
        {
            return Type == UIType.Button;
        }

        /// <summary>
        ///     检查是否为面板类型
        /// </summary>
        /// <returns>是否为面板类型</returns>
        public bool IsPanel()
        {
            return Type == UIType.Panel;
        }

        /// <summary>
        ///     检查是否为文本类型
        /// </summary>
        /// <returns>是否为文本类型</returns>
        public bool IsText()
        {
            return Type == UIType.Text;
        }

        public bool IsBar()
        {
            return Type == UIType.Bar;
        }

        #endregion

        #region Chain Operations

        /// <summary>
        ///     设置位置
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <returns>当前组件，支持链式调用</returns>
        public T SetPosition<T>(float x, float y) where T : UIComponent
        {
            Position = new Vector2(x, y);
            return (T)this;
        }

        /// <summary>
        ///     设置大小
        /// </summary>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <returns>当前组件，支持链式调用</returns>
        public T SetSize<T>(float width, float height) where T : UIComponent
        {
            Size = new Vector2(width, height);
            return (T)this;
        }

        /// <summary>
        ///     设置缩放
        /// </summary>
        /// <param name="scaleX">X轴缩放</param>
        /// <param name="scaleY">Y轴缩放</param>
        /// <returns>当前组件，支持链式调用</returns>
        public T SetScale<T>(float scaleX, float scaleY) where T : UIComponent
        {
            Scale = new Vector2(scaleX, scaleY);
            return (T)this;
        }

        /// <summary>
        ///     设置可见性
        /// </summary>
        /// <param name="visible">是否可见</param>
        /// <returns>当前组件，支持链式调用</returns>
        public T SetVisible<T>(bool visible) where T : UIComponent
        {
            Visible = visible;
            return (T)this;
        }

        /// <summary>
        ///     设置交互性
        /// </summary>
        /// <param name="interactable">是否可交互</param>
        /// <returns>当前组件，支持链式调用</returns>
        public T SetInteractable<T>(bool interactable) where T : UIComponent
        {
            Interactable = interactable;
            return (T)this;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        ///     获取完整ID路径
        /// </summary>
        /// <returns>从根组件到当前组件的完整路径</returns>
        public string GetFullPath()
        {
            var path = new List<string>();
            var current = this;

            while (current != null)
            {
                path.Add(current.Id);
                current = current._parent;
            }

            path.Reverse();
            return string.Join("/", path);
        }

        /// <summary>
        ///     销毁组件
        /// </summary>
        public virtual void Destroy()
        {
            // 销毁所有子组件
            var children = _children.Values.ToList();
            foreach (var child in children) child.Destroy();

            // 从父组件中移除
            _parent?.RemoveChild(Id);
        }

        #endregion

        #region Property Change Notifications

        /// <summary>
        ///     通知实体属性变更——走事件广播，UIManager 会查到对应 UIEntity 并调 OnDaoPropertyChanged。
        /// </summary>
        protected virtual void NotifyEntityPropertyChanged(string propertyName, object value)
        {
            if (string.IsNullOrEmpty(Id) || !EventProcessor.HasInstance) return;
            EventProcessor.Instance.TriggerEventMethod(
                "UIDaoPropertyChanged",   // = UIManager.EVT_DAO_PROPERTY_CHANGED
                new List<object> { Id, propertyName, value });
        }

        protected virtual void OnNameChanged(string newName)
        {
            NotifyEntityPropertyChanged("Name", newName);
        }

        protected virtual void OnVisibleChanged(bool visible)
        {
            NotifyEntityPropertyChanged("Visible", visible);
        }

        protected virtual void OnInteractableChanged(bool interactable)
        {
            NotifyEntityPropertyChanged("Interactable", interactable);
        }

        protected override void OnPositionChanged(Vector2 newPosition)
        {
            base.OnPositionChanged(newPosition);
            NotifyEntityPropertyChanged("Position", newPosition);
        }

        protected override void OnSizeChanged(Vector2 newSize)
        {
            base.OnSizeChanged(newSize);
            NotifyEntityPropertyChanged("Size", newSize);
        }

        protected override void OnScaleChanged(Vector2 newScale)
        {
            base.OnScaleChanged(newScale);
            NotifyEntityPropertyChanged("Scale", newScale);
        }

        #endregion
    }
}