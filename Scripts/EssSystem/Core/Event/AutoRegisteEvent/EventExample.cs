using System.Collections.Generic;
using UnityEngine;

namespace EssSystem.Core.Event
{
    /// <summary>
    /// Event标注机制使用示例
    /// </summary>
    public class EventExample : MonoBehaviour
    {
        void Start()
        {
            // 确保EventProcessor初始化
            EventProcessor.Instance;
            
            Log("Event示例初始化完成", Color.green);
        }

        /// <summary>
        /// Event方法示例 - 使用标注自动注册为Event
        /// </summary>
        [Event("PlayerDeath")]
        public List<object> OnPlayerDeath(List<object> data)
        {
            string playerName = data.Count > 0 ? data[0] as string : "Unknown";
            int score = data.Count > 1 ? (int)data[1] : 0;
            
            Log($"玩家 {playerName} 死亡，得分: {score}", Color.red);
            
            // 返回处理结果
            return new List<object> { $"处理了玩家 {playerName} 的死亡事件", score * 2 };
        }

        /// <summary>
        /// EventListener方法示例 - 自动监听指定事件
        /// </summary>
        [EventListener("PlayerDeath")]
        public List<object> HandlePlayerDeath(string eventName, List<object> data)
        {
            Log($"监听到玩家死亡事件: {eventName}", Color.yellow);
            
            // 可以修改数据或添加新的处理结果
            return new List<object> { "额外处理: 记录死亡统计", data.Count };
        }

        /// <summary>
        /// 另一个EventListener示例 - 监听同一个事件
        /// </summary>
        [EventListener("PlayerDeath")]
        [EventListener("PlayerRespawn")] // 可以监听多个事件
        public List<object> HandleGameEvents(string eventName, List<object> data)
        {
            Log($"处理游戏事件: {eventName}, 数据量: {data?.Count ?? 0}", Color.cyan);
            
            return new List<object> { $"事件 {eventName} 已处理" };
        }

        /// <summary>
        /// 自定义事件方法
        /// </summary>
        [Event("CustomEvent")]
        public List<object> OnCustomEvent(List<object> data)
        {
            string message = data.Count > 0 ? data[0] as string : "默认消息";
            
            Log($"自定义事件触发: {message}", Color.magenta);
            
            return new List<object> { $"自定义事件已处理: {message}" };
        }

        /// <summary>
        /// 测试触发Event方法
        /// </summary>
        [Event("TestTrigger")]
        public List<object> OnTestTrigger(List<object> data)
        {
            Log("测试事件被触发！", Color.green);
            
            return new List<object> { "测试成功" };
        }

        void Update()
        {
            // 按键测试
            if (Input.GetKeyDown(KeyCode.P))
            {
                // 通过EventManager触发事件
                var results = EventManager.Instance.TriggerEvent("PlayerDeath", 
                    new List<object> { "TestPlayer", 100 });
                
                Log($"事件触发结果数量: {results.Count}", Color.blue);
            }
            
            if (Input.GetKeyDown(KeyCode.C))
            {
                // 通过EventProcessor触发Event方法
                var results = EventProcessor.Instance.TriggerEventMethod("CustomEvent", 
                    new List<object> { "这是一个测试消息" });
                
                Log($"自定义事件结果数量: {results.Count}", Color.blue);
            }
            
            if (Input.GetKeyDown(KeyCode.T))
            {
                // 触发测试事件
                var results = EventProcessor.Instance.TriggerEventMethod("TestTrigger");
                
                Log($"测试事件结果数量: {results.Count}", Color.blue);
            }
        }
    }
}
