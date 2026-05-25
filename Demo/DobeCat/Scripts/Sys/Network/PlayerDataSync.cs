using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Application.MultiManagers.FarmManager;
using EssSystem.Core.Application.MultiManagers.FarmManager.Dao;
using Demo.DobeCat.Game.Farm;
using Demo.DobeCat.Sys.Auth;

namespace Demo.DobeCat.Sys.Network
{
    /// <summary>
    /// 玩家数据定时同步组件。
    /// 使用 data_exchange_server 的 /collections/{collection}/items API：
    /// <list type="bullet">
    /// <item>上传：POST /collections/{collection}/items，带 TTL=永久（大数字）。</item>
    /// <item>下载：GET /collections/{collection}，按 id 过滤找到玩家条目。</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerDataSync : MonoBehaviour
    {
        [Tooltip("数据收发器 Base URL（与 RoomDiscovery 共享）。")]
        public string ServerBaseUrl = "http://154.12.90.249:8765";

        [Tooltip("数据上传间隔（秒）。")]
        [Min(1f)] public float UploadIntervalSeconds = 1f;

        [Tooltip("数据集合名称。")]
        public string Collection = "player_data";

        [Tooltip("HTTP 超时（秒）。")]
        [Min(1f)] public float HttpTimeout = 8f;

        public static PlayerDataSync Instance { get; private set; }

        private float _uploadTimer;
        private bool  _uploading;

        private void OnEnable()  => Instance = this;
        private void OnDisable() { if (Instance == this) Instance = null; }

        private void Update()
        {
            if (_uploading) return;
            _uploadTimer += Time.deltaTime;
            if (_uploadTimer >= UploadIntervalSeconds)
            {
                _uploadTimer = 0f;
                StartCoroutine(UploadPlayerData());
            }
        }

        // ── 测试工具 ──────────────────────────────────────────────

        /// <summary>【测试用】删除服务器上当前玩家的存档条目（不影响本地状态）。
        /// Inspector 右键菜单 → "Clear My Server Data" 可直接触发。</summary>
        [ContextMenu("Clear My Server Data")]
        public void ClearMyServerData() => StartCoroutine(ClearMyServerDataRoutine());

        private IEnumerator ClearMyServerDataRoutine()
        {
            var key = GetPlayerKey();
            if (string.IsNullOrEmpty(key))
            { Debug.LogWarning("[PlayerDataSync] 未登录，无法清除服务器数据"); yield break; }
            if (string.IsNullOrEmpty(ServerBaseUrl))
            { Debug.LogWarning("[PlayerDataSync] ServerBaseUrl 为空"); yield break; }

            var url = $"{ServerBaseUrl.TrimEnd('/')}/collections/{Collection}/items/{key}";
            using var req = UnityWebRequest.Delete(url);
            var bearer = DataExchangeSession.BuildAuthorizationHeader();
            if (bearer != null) req.SetRequestHeader("Authorization", bearer);
            req.timeout = (int)HttpTimeout;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success || req.responseCode == 404)
                Debug.Log($"[PlayerDataSync] 服务器存档已清除 key={key}（responseCode={req.responseCode}）");
            else
                Debug.LogWarning($"[PlayerDataSync] 清除失败({req.responseCode}): {req.error}");
        }

        // ── 上传 ──────────────────────────────────────────────────

        public void UploadNow() => StartCoroutine(UploadPlayerData());

        private IEnumerator UploadPlayerData()
        {
            if (_uploading) yield break;
            if (!EventProcessor.HasInstance) yield break;
            var key = GetPlayerKey();
            if (string.IsNullOrEmpty(key)) yield break;
            if (string.IsNullOrEmpty(ServerBaseUrl)) yield break;

            _uploading = true;

            // data_exchange_server 格式：{ id, ttl, data: { <自定义字段> } }
            var innerData = CollectPlayerData(EventProcessor.Instance);
            var payload = new Dictionary<string, object>
            {
                ["id"]   = key,
                ["ttl"]  = 99999999,
                ["data"] = innerData
            };

            var json = MiniJson.Serialize(payload);
            var url  = $"{ServerBaseUrl.TrimEnd('/')}/collections/{Collection}/items";

            using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            var bearer = DataExchangeSession.BuildAuthorizationHeader();
            if (bearer != null) req.SetRequestHeader("Authorization", bearer);
            req.timeout = (int)HttpTimeout;

            yield return req.SendWebRequest();

            _uploading = false;

            if (req.result == UnityWebRequest.Result.Success)
                Debug.Log($"[PlayerDataSync] 上传成功 key={key}");
            else
                Debug.LogWarning($"[PlayerDataSync] 上传失败({req.responseCode}): {req.error}");
        }

        // ── 下载还原 ──────────────────────────────────────────────

        /// <summary>登录后调用，等待 2 帧确保 item 模板注册完毕再还原。</summary>
        public Coroutine FetchAndRestore() => StartCoroutine(FetchAndRestoreRoutine());

        private IEnumerator FetchAndRestoreRoutine()
        {
            // 等 2 帧，确保 FarmWorldController.Start() → EnsureSetup() 完成
            yield return null;
            yield return null;

            if (!EventProcessor.HasInstance) yield break;
            var key = GetPlayerKey();
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning("[PlayerDataSync] 未登录或 Mid=0，跳过拉取");
                yield break;
            }
            if (string.IsNullOrEmpty(ServerBaseUrl)) yield break;

            var url = $"{ServerBaseUrl.TrimEnd('/')}/collections/{Collection}";
            const int maxRetries = 3;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                using var req = UnityWebRequest.Get(url);
                var bearer = DataExchangeSession.BuildAuthorizationHeader();
                if (bearer != null) req.SetRequestHeader("Authorization", bearer);
                req.timeout = (int)HttpTimeout;

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[PlayerDataSync] 拉取失败({req.responseCode}) 第{attempt}次: {req.error}");
                    if (attempt < maxRetries)
                    {
                        yield return new UnityEngine.WaitForSeconds(3f);
                        continue;
                    }
                    yield break;
                }

                // 打印原始响应（前500字符），用于诊断字段是否被服务器过滤
                var rawJson = req.downloadHandler.text;
                Debug.Log($"[PlayerDataSync] 原始响应(前500字符): {rawJson.Substring(0, Mathf.Min(500, rawJson.Length))}");

                try
                {
                    var root = MiniJson.Deserialize(rawJson) as Dictionary<string, object>;
                    if (root == null) { Debug.LogWarning("[PlayerDataSync] 响应不是 JSON 对象"); yield break; }

                    object listObj;
                    if (!root.TryGetValue("items", out listObj)) root.TryGetValue("data", out listObj);
                    if (!(listObj is List<object> items)) { Debug.LogWarning("[PlayerDataSync] 找不到 items/data 数组"); yield break; }

                    Dictionary<string, object> entry = null;
                    foreach (var i in items)
                    {
                        if (!(i is Dictionary<string, object> d)) continue;
                        if (d.TryGetValue("id", out var idV) && idV?.ToString() == key)
                        { entry = d; break; }
                    }

                    if (entry == null)
                    {
                        Debug.Log($"[PlayerDataSync] 服务器上无此玩家数据 key={key}");
                        yield break;
                    }

                    // 服务器把自定义字段包在 entry["data"] 里，需先解包
                    var payload = entry;
                    if (entry.TryGetValue("data", out var dataObj) && dataObj is Dictionary<string, object> nested)
                        payload = nested;

                    RestorePlayerData(EventProcessor.Instance, payload);
                    Debug.Log($"[PlayerDataSync] 从服务器同步成功 key={key}（第{attempt}次尝试）");
                    yield break;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PlayerDataSync] 解析失败: {ex.Message}");
                    yield break;
                }
            }
        }

        // ── 序列化 / 反序列化 ────────────────────────────────────

        private static string GetPlayerKey()
        {
            if (AuthSession.IsAuthenticated && AuthSession.Mid > 0)
                return $"player_{AuthSession.Mid}";
            return null;
        }

        private static Dictionary<string, object> CollectPlayerData(EventProcessor ep)
        {
            var result = new Dictionary<string, object>();

            var invRes = ep.TriggerEventMethod("InventoryQuery", new List<object> { "player" });
            if (ResultCode.IsOk(invRes) && invRes.Count >= 2
                && invRes[1] is EssSystem.Core.Application.SingleManagers.InventoryManager.Dao.Inventory inv)
            {
                var slots = new List<object>();
                foreach (var slot in inv.GetOccupiedSlots())
                    if (slot.Item != null)
                        slots.Add(new Dictionary<string, object>
                            { ["id"] = slot.Item.Id, ["stack"] = slot.Item.CurrentStack });
                result["inventory"] = slots;
            }

            var wId = EssSystem.Core.Application.MultiManagers.ShopManager.ShopService.WalletId("player");
            var walRes = ep.TriggerEventMethod("InventoryQuery", new List<object> { wId });
            if (ResultCode.IsOk(walRes) && walRes.Count >= 2
                && walRes[1] is EssSystem.Core.Application.SingleManagers.InventoryManager.Dao.Inventory wallet)
            {
                var slots = new List<object>();
                foreach (var slot in wallet.GetOccupiedSlots())
                    if (slot.Item != null)
                        slots.Add(new Dictionary<string, object>
                            { ["id"] = slot.Item.Id, ["stack"] = slot.Item.CurrentStack });
                result["wallet"] = slots;
            }

            // 农场槽位
            var farmSvc = FarmService.Instance;
            var farmInst = farmSvc?.GetFarm(FarmWorldController.FarmInstId);
            if (farmInst?.Slots != null)
            {
                var farmData = new List<object>();
                foreach (var slot in farmInst.Slots)
                {
                    if (slot.Stage == CropGrowthStage.Empty || string.IsNullOrEmpty(slot.CropConfigId)) continue;
                    farmData.Add(new Dictionary<string, object>
                    {
                        ["row"]        = slot.Row,
                        ["col"]        = slot.Col,
                        ["crop"]       = slot.CropConfigId,
                        ["stage"]      = (int)slot.Stage,
                        ["watered"]    = slot.Watered,
                        ["pest"]       = slot.HasPest,
                        ["planted"]    = slot.PlantedAtUnixSeconds,
                        ["stageStart"] = slot.StageStartUnixSeconds,
                        ["fertBoost"]  = slot.FertilizeBoostUntilUnix,
                        ["pestTime"]   = slot.ScheduledPestUnixSeconds,
                    });
                }
                result["farm"] = farmData;
            }

            result["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return result;
        }

        private static void RestorePlayerData(EventProcessor ep, Dictionary<string, object> data)
        {
            // 还原背包（关闭 UI → 删除 → 重建 → 写入 → UI 下次开时自动重建）
            if (data.TryGetValue("inventory", out var invObj) && invObj is List<object> invSlots)
            {
                ep.TriggerEventMethod("CloseInventoryUI", new List<object> { "player" });
                ep.TriggerEventMethod("InventoryDelete",  new List<object> { "player" });
                ep.TriggerEventMethod("InventoryCreate",  new List<object> { "player", "玩家背包", 30 });
                int added = 0;
                foreach (var s in invSlots)
                {
                    if (!(s is Dictionary<string, object> slot)) continue;
                    var id    = slot.TryGetValue("id",    out var idV)    ? idV?.ToString()         : null;
                    var stack = slot.TryGetValue("stack", out var stackV) ? Convert.ToInt32(stackV) : 1;
                    if (string.IsNullOrEmpty(id)) continue;
                    ep.TriggerEventMethod("InventoryAdd", new List<object> { "player", id, stack });
                    Debug.Log($"[PlayerDataSync] 背包 ← {id} x{stack}");
                    added++;
                }
                Debug.Log($"[PlayerDataSync] 背包还原完毕，共 {added} 条（服务器 {invSlots.Count} 条）");
            }
            else
            {
                Debug.LogWarning("[PlayerDataSync] 服务器数据中没有 inventory 字段，背包保持不变");
            }

            // 还原钱包
            if (data.TryGetValue("wallet", out var walObj) && walObj is List<object> walSlots)
            {
                var walletId = EssSystem.Core.Application.MultiManagers.ShopManager.ShopService.WalletId("player");
                ep.TriggerEventMethod("InventoryDelete", new List<object> { walletId });
                ep.TriggerEventMethod("InventoryCreate", new List<object> { walletId, "player钱包", 5 });
                int added = 0;
                foreach (var s in walSlots)
                {
                    if (!(s is Dictionary<string, object> slot)) continue;
                    var id    = slot.TryGetValue("id",    out var idV)    ? idV?.ToString()         : null;
                    var stack = slot.TryGetValue("stack", out var stackV) ? Convert.ToInt32(stackV) : 1;
                    if (string.IsNullOrEmpty(id)) continue;
                    ep.TriggerEventMethod("InventoryAdd", new List<object> { walletId, id, stack });
                    Debug.Log($"[PlayerDataSync] 钱包 ← {id} x{stack}");
                    added++;
                }
                Debug.Log($"[PlayerDataSync] 钱包还原完毕，共 {added} 条（服务器 {walSlots.Count} 条）");
            }
            else
            {
                Debug.LogWarning("[PlayerDataSync] 服务器数据中没有 wallet 字段，钱包保持不变");
            }

            // 还原农场槽位
            if (data.TryGetValue("farm", out var farmObj) && farmObj is List<object> savedSlots)
            {
                var farmSvc = FarmService.Instance;
                var farmInst = farmSvc?.GetFarm(FarmWorldController.FarmInstId);
                if (farmInst?.Slots != null)
                {
                    // 先清空所有槽位
                    foreach (var s in farmInst.Slots)
                    {
                        s.CropConfigId            = null;
                        s.Stage                   = CropGrowthStage.Empty;
                        s.Watered                 = false;
                        s.HasPest                 = false;
                        s.PlantedAtUnixSeconds    = 0;
                        s.StageStartUnixSeconds   = 0;
                        s.FertilizeBoostUntilUnix = 0;
                        s.ScheduledPestUnixSeconds = 0;
                    }
                    // 写入服务器数据
                    foreach (var item in savedSlots)
                    {
                        if (!(item is Dictionary<string, object> sd)) continue;
                        var row  = Convert.ToInt32(sd["row"]);
                        var col  = Convert.ToInt32(sd["col"]);
                        var slot = farmInst.Slots.Find(x => x.Row == row && x.Col == col);
                        if (slot == null) continue;
                        slot.CropConfigId             = sd["crop"]?.ToString();
                        slot.Stage                    = (CropGrowthStage)Convert.ToInt32(sd["stage"]);
                        slot.Watered                  = sd.TryGetValue("watered",    out var wv) && Convert.ToBoolean(wv);
                        slot.HasPest                  = sd.TryGetValue("pest",       out var pv) && Convert.ToBoolean(pv);
                        slot.PlantedAtUnixSeconds     = sd.TryGetValue("planted",    out var plv)   ? Convert.ToInt64(plv)   : 0;
                        slot.StageStartUnixSeconds    = sd.TryGetValue("stageStart", out var ssv)   ? Convert.ToInt64(ssv)   : 0;
                        slot.FertilizeBoostUntilUnix  = sd.TryGetValue("fertBoost",  out var fbv)   ? Convert.ToInt64(fbv)   : 0;
                        slot.ScheduledPestUnixSeconds = sd.TryGetValue("pestTime",   out var ptv)   ? Convert.ToInt64(ptv)   : 0;
                    }
                    Debug.Log($"[PlayerDataSync] 农场还原 {savedSlots.Count} 个作物");
                    // 通知 FarmWorldController 刷新视觉（1秒内自动触发）
                    FarmWorldController.Instance?.RefreshAllPublic();
                }
            }
        }
    }
}
