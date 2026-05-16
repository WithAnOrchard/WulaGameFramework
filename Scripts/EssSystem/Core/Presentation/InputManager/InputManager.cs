using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Util;

namespace EssSystem.Core.Presentation.InputManager
{
    /// <summary>
    /// 输入管理器 —— 统一抽象 Action 事件 + 鼠标 + Axis 查询。
    /// <para>每帧检测 Action 状态变化广播 <see cref="EVT_INPUT_DOWN"/> / <see cref="EVT_INPUT_UP"/>，
    /// 业务模块通过 <c>[EventListener]</c> 订阅或主动 <see cref="EVT_IS_PRESSED"/> 查询。</para>
    /// <para>当前实现基于 UnityEngine.Input（Legacy Input Manager）；切 New Input System 仅需替换本类内部读取，事件协议不变。</para>
    /// </summary>
    [Manager(2)]
    public class InputManager : Manager<InputManager>
    {
        // ============================================================
        // Event 常量（对外 API）
        // ============================================================
        // —— 命令 / 查询 ——
        /// <summary>绑定 Action 到 KeyCode 列表（覆盖原绑定）。data: [string actionName, params KeyCode[] keys].</summary>
        public const string EVT_BIND_ACTION    = "BindInputAction";
        /// <summary>解绑 Action。data: [string actionName].</summary>
        public const string EVT_UNBIND_ACTION  = "UnbindInputAction";
        /// <summary>查询 Action 当前是否按住。data: [string actionName] → Ok(bool).</summary>
        public const string EVT_IS_PRESSED     = "IsInputPressed";
        /// <summary>查询 Action 是否本帧按下。data: [string actionName] → Ok(bool).</summary>
        public const string EVT_IS_DOWN        = "IsInputDown";
        /// <summary>查询 Action 是否本帧抬起。data: [string actionName] → Ok(bool).</summary>
        public const string EVT_IS_UP          = "IsInputUp";
        /// <summary>取轴向值（默认 Unity Axis；可指定 negativeAction/positiveAction 自动计算 -1/0/+1）。data: [string axisOrNegativeAction, string positiveAction?] → Ok(float).</summary>
        public const string EVT_GET_AXIS       = "GetInputAxis";
        /// <summary>取 2D 移动向量（来自 4 个方向 Action 或 Unity Horizontal/Vertical 轴）。data: 空 → Ok(Vector2).</summary>
        public const string EVT_GET_MOVE_AXIS  = "GetInputMoveAxis";
        /// <summary>鼠标屏幕坐标。data: 空 → Ok(Vector2).</summary>
        public const string EVT_GET_MOUSE_POS  = "GetMouseScreenPosition";
        /// <summary>鼠标本帧滚轮 delta。data: 空 → Ok(float).</summary>
        public const string EVT_GET_MOUSE_SCROLL = "GetMouseScroll";

        // —— 广播 ——（每帧检测 Action 状态变化）
        /// <summary>Action 本帧按下广播。data: [string actionName].</summary>
        public const string EVT_INPUT_DOWN = "OnInputDown";
        /// <summary>Action 本帧抬起广播。data: [string actionName].</summary>
        public const string EVT_INPUT_UP   = "OnInputUp";

        // ============================================================
        // 默认 Action 绑定
        // ============================================================
        private static readonly Dictionary<string, KeyCode[]> _defaultBindings = new()
        {
            { "MoveLeft",  new[] { KeyCode.A, KeyCode.LeftArrow  } },
            { "MoveRight", new[] { KeyCode.D, KeyCode.RightArrow } },
            { "MoveUp",    new[] { KeyCode.W, KeyCode.UpArrow    } },
            { "MoveDown",  new[] { KeyCode.S, KeyCode.DownArrow  } },
            { "Jump",      new[] { KeyCode.Space         } },
            { "Attack",    new[] { KeyCode.Mouse0        } },
            { "AltAction", new[] { KeyCode.Mouse1        } },
            { "Interact",  new[] { KeyCode.E             } },
            { "Cancel",    new[] { KeyCode.Escape        } },
            { "Pause",     new[] { KeyCode.P, KeyCode.Pause } },
        };

        // ============================================================
        // Inspector
        // ============================================================
        [Header("Behavior")]
        [Tooltip("是否每帧广播 OnInputDown / OnInputUp 事件（关闭后只能用 EVT_IS_* 主动查询）")]
        [SerializeField] private bool _broadcastEvents = true;

        public InputService Service => InputService.Instance;

        // ============================================================
        // 运行时缓存
        // ============================================================
        /// <summary>actionName → KeyCodes（运行时副本，从 Service 加载/覆盖）</summary>
        private readonly Dictionary<string, KeyCode[]> _bindings = new();

        /// <summary>本帧按住的 actions（避免重复 Input.GetKey 调用）。
        /// <b>不能 readonly</b>：每帧需与 _pressedLastFrame 交换引用，避免 Clear+Add 双开销。</summary>
        private HashSet<string> _pressedThisFrame = new();
        /// <summary>上一帧按住的 actions（用于比对生成 Down/Up 事件）。</summary>
        private HashSet<string> _pressedLastFrame = new();

        // ============================================================
        // 生命周期
        // ============================================================
        protected override void Initialize()
        {
            base.Initialize();
            LoadBindingsFromService();
            Log($"InputManager 初始化完成（{_bindings.Count} 个 Action）", Color.green);
        }

        protected override void SyncServiceLoggingSettings()
        {
            if (Service != null) Service.EnableLogging = _serviceEnableLogging;
        }

        protected override void UpdateServiceInspectorInfo()
        {
            if (Service == null) return;
            Service.UpdateInspectorInfo();
            _serviceInspectorInfo = Service.InspectorInfo;
        }

        private void LoadBindingsFromService()
        {
            _bindings.Clear();
            // 1) 默认绑定
            foreach (var kv in _defaultBindings) _bindings[kv.Key] = kv.Value;
            // 2) Service 中持久化的覆盖（玩家自定义键位）
            if (Service != null)
            {
                foreach (var (action, keys) in Service.GetAllBindings())
                    if (!string.IsNullOrEmpty(action) && keys != null && keys.Length > 0)
                        _bindings[action] = keys;
            }
        }

        // ============================================================
        // 每帧轮询
        // ============================================================
        protected override void Update()
        {
            base.Update();   // 让基类完成 Inspector 节流刷新

            // 交换两个 HashSet：上一帧 ← 本帧；旧的"上一帧"清空后变成新的"本帧"
            (_pressedLastFrame, _pressedThisFrame) = (_pressedThisFrame, _pressedLastFrame);
            _pressedThisFrame.Clear();

            foreach (var kv in _bindings)
            {
                if (IsAnyKeyHeld(kv.Value)) _pressedThisFrame.Add(kv.Key);
            }

            if (!_broadcastEvents || EventProcessor.Instance == null) return;

            // Down: this 有 last 无
            foreach (var a in _pressedThisFrame)
                if (!_pressedLastFrame.Contains(a))
                    EventProcessor.Instance.TriggerEventMethod(EVT_INPUT_DOWN, new List<object> { a });

            // Up: last 有 this 无
            foreach (var a in _pressedLastFrame)
                if (!_pressedThisFrame.Contains(a))
                    EventProcessor.Instance.TriggerEventMethod(EVT_INPUT_UP, new List<object> { a });
        }

        private static bool IsAnyKeyHeld(KeyCode[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
                if (Input.GetKey(keys[i])) return true;
            return false;
        }

        private static bool IsAnyKeyDown(KeyCode[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
                if (Input.GetKeyDown(keys[i])) return true;
            return false;
        }

        private static bool IsAnyKeyUp(KeyCode[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
                if (Input.GetKeyUp(keys[i])) return true;
            return false;
        }

        // ============================================================
        // C# API
        // ============================================================
        public bool IsPressed(string action) =>
            !string.IsNullOrEmpty(action) && _bindings.TryGetValue(action, out var keys) && IsAnyKeyHeld(keys);

        public bool IsDown(string action) =>
            !string.IsNullOrEmpty(action) && _bindings.TryGetValue(action, out var keys) && IsAnyKeyDown(keys);

        public bool IsUp(string action) =>
            !string.IsNullOrEmpty(action) && _bindings.TryGetValue(action, out var keys) && IsAnyKeyUp(keys);

        public Vector2 GetMoveAxis()
        {
            // 优先 4 个方向 Action；都没绑则回落 Unity Horizontal/Vertical
            float x = 0f, y = 0f;
            bool hasMove = _bindings.ContainsKey("MoveLeft") || _bindings.ContainsKey("MoveRight")
                        || _bindings.ContainsKey("MoveUp")   || _bindings.ContainsKey("MoveDown");
            if (hasMove)
            {
                if (IsPressed("MoveRight")) x += 1f;
                if (IsPressed("MoveLeft"))  x -= 1f;
                if (IsPressed("MoveUp"))    y += 1f;
                if (IsPressed("MoveDown"))  y -= 1f;
            }
            else
            {
                x = Input.GetAxis("Horizontal");
                y = Input.GetAxis("Vertical");
            }
            return new Vector2(x, y);
        }

        public void BindAction(string action, params KeyCode[] keys)
        {
            if (string.IsNullOrEmpty(action) || keys == null || keys.Length == 0) return;
            _bindings[action] = keys;
            Service?.SetBinding(action, keys);
        }

        public void UnbindAction(string action)
        {
            if (string.IsNullOrEmpty(action)) return;
            _bindings.Remove(action);
            Service?.RemoveBinding(action);
        }

        public Vector2 GetMouseScreenPosition() => (Vector2)Input.mousePosition;
        public float   GetMouseScroll()         => Input.GetAxis("Mouse ScrollWheel");

        // ============================================================
        // Event API
        // ============================================================
        [Event(EVT_BIND_ACTION)]
        public List<object> OnBindAction(List<object> data)
        {
            if (data == null || data.Count < 2 || !(data[0] is string action) || string.IsNullOrEmpty(action))
                return ResultCode.Fail("参数 [actionName, KeyCode...]");
            var keys = new List<KeyCode>();
            for (int i = 1; i < data.Count; i++)
                if (data[i] is KeyCode k) keys.Add(k);
            if (keys.Count == 0) return ResultCode.Fail("至少需 1 个 KeyCode");
            BindAction(action, keys.ToArray());
            return ResultCode.Ok(action);
        }

        [Event(EVT_UNBIND_ACTION)]
        public List<object> OnUnbindAction(List<object> data)
        {
            if (data == null || data.Count < 1 || !(data[0] is string action) || string.IsNullOrEmpty(action))
                return ResultCode.Fail("参数 [actionName]");
            UnbindAction(action);
            return ResultCode.Ok(action);
        }

        [Event(EVT_IS_PRESSED)]
        public List<object> OnIsPressed(List<object> data) =>
            data != null && data.Count >= 1 && data[0] is string a
                ? ResultCode.Ok(IsPressed(a)) : ResultCode.Fail("参数 [actionName]");

        [Event(EVT_IS_DOWN)]
        public List<object> OnIsDown(List<object> data) =>
            data != null && data.Count >= 1 && data[0] is string a
                ? ResultCode.Ok(IsDown(a)) : ResultCode.Fail("参数 [actionName]");

        [Event(EVT_IS_UP)]
        public List<object> OnIsUp(List<object> data) =>
            data != null && data.Count >= 1 && data[0] is string a
                ? ResultCode.Ok(IsUp(a)) : ResultCode.Fail("参数 [actionName]");

        [Event(EVT_GET_AXIS)]
        public List<object> OnGetAxis(List<object> data)
        {
            if (data == null || data.Count < 1 || !(data[0] is string a))
                return ResultCode.Fail("参数 [axisName] 或 [negativeAction, positiveAction]");

            // 双 Action 模式：返回 -1/0/+1
            if (data.Count >= 2 && data[1] is string positive)
            {
                float v = 0f;
                if (IsPressed(positive)) v += 1f;
                if (IsPressed(a))        v -= 1f;
                return ResultCode.Ok(v);
            }
            // 单参数：直接走 Unity Axis
            return ResultCode.Ok(Input.GetAxis(a));
        }

        [Event(EVT_GET_MOVE_AXIS)]
        public List<object> OnGetMoveAxis(List<object> data) => ResultCode.Ok(GetMoveAxis());

        [Event(EVT_GET_MOUSE_POS)]
        public List<object> OnGetMousePos(List<object> data) => ResultCode.Ok(GetMouseScreenPosition());

        [Event(EVT_GET_MOUSE_SCROLL)]
        public List<object> OnGetMouseScroll(List<object> data) => ResultCode.Ok(GetMouseScroll());
    }
}
