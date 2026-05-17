using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;
// §4.1 跨模块 InventoryManager / AudioManager / DialogueManager 走 bare-string，不 using。

namespace Demo.Tribe.Player
{
    /// <summary>
    /// 玩家交互模块 —— B 键切换背包、I 键切换调试对话。
    /// 所有跨模块调用走 §4.1 bare-string 协议。
    /// </summary>
    [DisallowMultipleComponent]
    public class TribePlayerInteraction : MonoBehaviour
    {
        [Header("Inventory (B 键)")]
        [SerializeField] private bool   _enableInventoryToggle = true;
        [SerializeField] private string _inventoryId           = "player";
        [SerializeField] private string _inventoryConfigId     = "PlayerBackPack";
        [SerializeField] private KeyCode _inventoryToggleKey   = KeyCode.B;

        [Header("Dialogue (I 键)")]
        [SerializeField] private bool    _enableDialogueTest = true;
        [SerializeField] private string  _dialogueId         = "DebugDialogue";
        [SerializeField] private KeyCode _dialogueToggleKey  = KeyCode.I;

        public void Tick()
        {
            if (_enableInventoryToggle && Input.GetKeyDown(_inventoryToggleKey)) ToggleInventory();
            if (_enableDialogueTest && Input.GetKeyDown(_dialogueToggleKey)) ToggleDialogue();
        }

        private void ToggleInventory()
        {
            if (!EventProcessor.HasInstance || string.IsNullOrEmpty(_inventoryId)) return;
            var visible = IsUIVisible(_inventoryId);
            EventProcessor.Instance.TriggerEventMethod(
                visible ? "CloseInventoryUI" : "OpenInventoryUI",
                visible ? new List<object> { _inventoryId }
                        : new List<object> { _inventoryId, _inventoryConfigId });
            
            // 播放 UI 操作音效
            EventProcessor.Instance.TriggerEventMethod("PlayUISFX", null);
        }

        private void ToggleDialogue()
        {
            if (!EventProcessor.HasInstance) return;
            var current = EventProcessor.Instance.TriggerEventMethod("QueryDialogueCurrent", new List<object>());
            if (ResultCode.IsOk(current))
            {
                EventProcessor.Instance.TriggerEventMethod("CloseDialogueUI", new List<object>());
                return;
            }
            var result = EventProcessor.Instance.TriggerEventMethod(
                "OpenDialogueUI", new List<object> { _dialogueId });
            if (!ResultCode.IsOk(result))
            {
                var msg = result != null && result.Count >= 2 ? result[1] : "unknown";
                Debug.LogWarning($"[TribePlayerInteraction] 打开对话失败: {msg}（请确认 DialogueManager 已挂载且对话 Id `{_dialogueId}` 已注册）");
            }
        }

        private static bool IsUIVisible(string daoId)
        {
            var r = EventProcessor.Instance.TriggerEventMethod(
                "GetUIGameObject", new List<object> { daoId });
            return ResultCode.IsOk(r) && r.Count >= 2 && r[1] is GameObject go && go != null && go.activeInHierarchy;
        }
    }
}
