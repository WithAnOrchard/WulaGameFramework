# 通用数据收发器 (Data Exchange Server)

一个**与业务无关**的轻量 HTTP REST 服务，用于在分布式场景下做"心跳保活的发现表"。任何客户端都能向某个集合 upsert 一条带 TTL 的 JSON 数据，超时未续约自动 GC，其它客户端通过 GET 拉取活跃列表。

## 安装 / 启动

```bash
pip install -r requirements.txt
python server.py --host 0.0.0.0 --port 8765
```

## API

### `POST /collections/{name}/items`

upsert 一条记录到指定集合。

**Request body**:
```json
{
  "id": "room_abc",
  "ttl": 30,
  "data": { "name": "我的房间", "host": "1.2.3.4", "port": 7777 }
}
```

| 字段 | 类型 | 默认 | 说明 |
|---|---|---|---|
| `id` | string | 必填 | 自定义稳定 ID，重复 upsert 即续约 |
| `ttl` | number (秒) | 30 | ≤ 0 表示永久（不会被 GC） |
| `data` | any | `{}` | 任意 JSON 数据，原样存原样返 |

**Response**:
```json
{ "ok": true, "data": { "id": "room_abc", "ttl_remaining": 30.0 } }
```

### `GET /collections/{name}`

列出指定集合中所有未过期条目，按 `updated_at` 倒序。

**Response**:
```json
{
  "ok": true,
  "count": 2,
  "data": [
    { "id": "room_abc", "data": {...}, "ttl_remaining": 27.3, "updated_at": 1700000000.0 },
    { "id": "room_xyz", "data": {...}, "ttl_remaining": 18.0, "updated_at": 1699999990.0 }
  ]
}
```

### `GET /collections/{name}/items/{id}`

取单条；过期或不存在返回 404。

### `DELETE /collections/{name}/items/{id}`

主动下线（如 Mirror Host 正常关闭时调用，避免 30s 后才被 GC 清掉）。

### `GET /health`

健康检查，返回当前集合数 / 条目数。

## 典型用例：Mirror 多人房间发现

### Host 端（每 10s 心跳）

```bash
curl -X POST http://server:8765/collections/rooms/items \
  -H "Content-Type: application/json" \
  -d '{
    "id": "room_alice_001",
    "ttl": 30,
    "data": {
      "name": "Alice 的房间",
      "host": "1.2.3.4",
      "port": 7777,
      "players": ["Alice", "Bob"],
      "max": 4,
      "version": "1.0.0"
    }
  }'
```

### Client 端（拉房间列表）

```bash
curl http://server:8765/collections/rooms
```

### Host 关闭时

```bash
curl -X DELETE http://server:8765/collections/rooms/items/room_alice_001
```

## Unity 客户端集成示意（C#）

```csharp
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class RoomDiscovery : MonoBehaviour
{
    public string ServerUrl = "http://localhost:8765";
    public string SelfRoomId = "my_room";

    IEnumerator Heartbeat() {
        var url = $"{ServerUrl}/collections/rooms/items";
        var json = JsonUtility.ToJson(new RoomReq {
            id = SelfRoomId, ttl = 30,
            data = new RoomData { name = "我的房间", host = "...", port = 7777 }
        });
        while (true) {
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            yield return new WaitForSeconds(10f);
        }
    }
}
```

## 设计要点

- **完全无业务**：服务端对 `data` 字段不做任何 schema 校验，调用方约定即可
- **多个集合并存**：同一个服务可以同时管 `rooms` / `lobbies` / `presence` / `voice_channels` 等
- **GC 在后台 5s 一扫**：单进程内存存储；倒了之后所有数据丢失（重启即清空，符合"在线/离线"语义）
- **加锁全程 RLock**：可在 Flask `threaded=True` 下安全运行，几百 QPS 量级足矣

## 已知限制

- **单进程内存**：横向扩展需自行接 Redis（替换 `_STORE` 即可）
- **无鉴权**：内网部署或跑在 reverse proxy（带 Bearer Token）后面更安全
- **无回放**：`data` 是覆盖式的，没有历史快照

## 文件

- `server.py` — 全部实现（< 200 行）
- `requirements.txt` — Flask 依赖
