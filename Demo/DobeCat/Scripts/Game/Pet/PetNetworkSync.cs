using System;
using System.Collections.Generic;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Presentation.CharacterManager;
using EssSystem.Manager.NetworkManager;
using NetMgr = EssSystem.Manager.NetworkManager.NetworkManager;
using UnityEngine;

namespace Demo.DobeCat.Game.Pet
{
    /// <summary>
    /// 桌宠位置网络同步 —— 完全建立在 NetworkManager 的 EVT_BROADCAST + EVT_NET_MESSAGE 之上。
    /// <list type="bullet">
    /// <item>每帧节流广播自身位置 [selfId, x, y, z]，topic = <see cref="Topic"/>。</item>
    /// <item>收到陌生 selfId 时 spawn 一只“幽灵桌宠”跟随；收到则更新坐标。</item>
    /// <item>5 秒未收到该 peer 的位置则销毁其幽灵（兜底掉线）。</item>
    /// </list>
    /// </summary>
    public class PetNetworkSync : MonoBehaviour
    {
        public const string Topic = "DobePos";

        [Tooltip("本地桌宠 Transform —— 由 DobeCatGameManager 注入。")]
        public Transform LocalPet;

        [Tooltip("幽灵桌宠使用的 CharacterManager ConfigId（与本机区分，默认 Mage）。")]
        public string GhostCharacterConfigId = "Mage";

        // 兼容字段（DobeCatGameManager 旧赋值不再使用，保留避免编译错）
        [System.NonSerialized] public string GhostSpritePath;
        [System.NonSerialized] public Color GhostTint;

        [Tooltip("幽灵桌宠视觉缩放。")]
        public float GhostScale = 1f;

        [Tooltip("位置广播间隔（秒）。100ms = 0.1。")]
        [Min(0.05f)] public float BroadcastInterval = 0.1f;

        [Tooltip("超过该秒数未收到 peer 消息则销毁幽灵。")]
        public float GhostTimeoutSeconds = 5f;

        // 自身唯一 ID（每次进程随机；不持久化）
        private string _selfId;
        private float _nextBroadcastTime;

        private class GhostEntry
        {
            public GameObject Go;
            public Transform Tr;
            public float LastSeenTime;
            public Vector3 TargetPos;
        }

        private readonly Dictionary<string, GhostEntry> _ghosts = new();

        private EssSystem.Core.Base.Event.EventDelegate _msgDelegate;

        private void OnEnable()
        {
            _selfId ??= Guid.NewGuid().ToString("N").Substring(0, 8);
            _msgDelegate ??= OnNetMessageDelegate;
            EventProcessor.Instance.AddListener(NetworkService.EVT_NET_MESSAGE, _msgDelegate);
            Debug.Log($"[PetNetworkSync] selfId={_selfId} 已订阅 EVT_NET_MESSAGE");
        }

        private void OnDisable()
        {
            if (_msgDelegate != null && EventProcessor.HasInstance)
                EventProcessor.Instance.RemoveListener(NetworkService.EVT_NET_MESSAGE, _msgDelegate);
        }

        private void OnDestroy()
        {
            foreach (var g in _ghosts.Values)
                if (g.Go != null) Destroy(g.Go);
            _ghosts.Clear();
        }

        private void Update()
        {
            // 1) 节流广播自己位置
            if (LocalPet != null && Time.unscaledTime >= _nextBroadcastTime)
            {
                _nextBroadcastTime = Time.unscaledTime + Mathf.Max(0.05f, BroadcastInterval);
                var p = LocalPet.position;
                var payload = new Dictionary<string, object>
                {
                    {"id", _selfId},
                    {"x", (double)p.x},
                    {"y", (double)p.y},
                    {"z", (double)p.z},
                };
                EventProcessor.Instance.TriggerEventMethod(NetMgr.EVT_BROADCAST,
                    new List<object> { Topic, payload });
            }

            // 2) 平滑插值 + 超时清理
            var now = Time.unscaledTime;
            List<string> stale = null;
            foreach (var kv in _ghosts)
            {
                var g = kv.Value;
                if (g.Tr != null) g.Tr.position = Vector3.Lerp(g.Tr.position, g.TargetPos, 1f - Mathf.Exp(-12f * Time.deltaTime));
                if (now - g.LastSeenTime > GhostTimeoutSeconds)
                {
                    (stale ??= new List<string>()).Add(kv.Key);
                }
            }
            if (stale != null)
            {
                foreach (var id in stale)
                {
                    if (_ghosts.TryGetValue(id, out var g) && g.Go != null) Destroy(g.Go);
                    _ghosts.Remove(id);
                    Debug.Log($"[PetNetworkSync] 幽灵超时销毁 id={id}");
                }
            }
        }

        // ─── 收到广播位置 ──────────────────────────────────────
        private List<object> OnNetMessageDelegate(string eventName, List<object> data)
        {
            if (data == null || data.Count < 3) return null;
            var topic = data[1] as string;
            if (topic != Topic) return null;

            var payloadStr = data[2] as string;
            var decoded = NetworkService.DecodePayload(payloadStr);
            if (decoded is not Dictionary<string, object> dict) return null;

            if (!dict.TryGetValue("id", out var idObj) || idObj is not string peerId) return null;
            if (peerId == _selfId) return null; // 忽略自己的回声

            var x = AsFloat(dict, "x");
            var y = AsFloat(dict, "y");
            var z = AsFloat(dict, "z");
            var pos = new Vector3(x, y, z);

            if (!_ghosts.TryGetValue(peerId, out var entry))
            {
                entry = SpawnGhost(peerId, pos);
                _ghosts[peerId] = entry;
                Debug.Log($"[PetNetworkSync] 新 peer 出现 id={peerId} → spawn 幽灵");
            }
            entry.TargetPos = pos;
            entry.LastSeenTime = Time.unscaledTime;
            return null;
        }

        private GhostEntry SpawnGhost(string peerId, Vector3 pos)
        {
            var go = new GameObject($"Ghost_{peerId}");
            go.transform.SetParent(transform.parent, worldPositionStays: false);
            go.transform.position = pos;

            var view = go.AddComponent<PetView>();
            view.UseChildRenderers = true;
            view.VisualScale = GhostScale;

            // 用 CharacterManager 创建一只可视化角色，instanceId 用 peerId 保唯一
            if (CharacterService.HasInstance && !string.IsNullOrEmpty(GhostCharacterConfigId))
            {
                var ghostInstanceId = $"DobeCatGhost_{peerId}";
                CharacterService.Instance.CreateCharacter(
                    configId:      GhostCharacterConfigId,
                    instanceId:    ghostInstanceId,
                    parent:        go.transform,
                    worldPosition: pos);
                EventProcessor.Instance.TriggerEventMethod(
                    EssSystem.Core.Presentation.CharacterManager.CharacterManager.EVT_PLAY_LOCOMOTION,
                    new List<object> { ghostInstanceId, false, true });
            }
            else
            {
                Debug.LogWarning($"[PetNetworkSync] CharacterService 未就绪，无法创建幽灵角色 peer={peerId}");
            }

            return new GhostEntry { Go = go, Tr = go.transform, TargetPos = pos, LastSeenTime = Time.unscaledTime };
        }

        private static float AsFloat(Dictionary<string, object> dict, string key)
        {
            if (!dict.TryGetValue(key, out var v) || v == null) return 0f;
            if (v is double d) return (float)d;
            if (v is float f) return f;
            if (v is long l) return l;
            if (v is int i) return i;
            return 0f;
        }
    }
}
