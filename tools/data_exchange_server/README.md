# 通用数据收发器 (Data Exchange Server)

一个轻量 HTTP REST 服务。最初定位是"心跳保活的发现表"，现已扩展三层能力：

1. **集合层** —— 任意 collection + 任意 JSON + TTL，超时自动 GC。适合房间列表 / presence。
2. **推送层** —— SSE 长连接，集合内任何变更立刻广播全量快照，告别轮询延迟。
3. **会话 + Action 层** —— 用 B 站 SESSDATA 换短期 token；权威写操作（金币、冷却、物品）必须走 `/actions`，由服务端裁决。配 SQLite 持久化，重启不丢。

这三层是分级、可关可开的：纯做发现表时与原版完全兼容；要做反作弊只需打开 `--auth-collections` 并写 action handler。

## 安装 / 启动

```bash
pip install -r requirements.txt
# 最小启动（开放、无鉴权）：
python server.py --host 0.0.0.0 --port 8765
# 受保护启动（rooms/players 集合写入需 token + Bilibili 校验）：
python server.py --auth-collections rooms,players
# 本地联调（跳过 Bilibili 校验，信任客户端自报 mid）：
python server.py --auth-collections rooms,players --no-bilibili-check
```

### CLI 选项

| 选项 | 默认 | 说明 |
|---|---|---|
| `--host` | `0.0.0.0` | 绑定地址 |
| `--port` | `8765` | 端口 |
| `--auth-collections` | `""` | 逗号分隔，列出需要会话鉴权才能写的 collection；读永远开放。例：`rooms,players` |
| `--no-bilibili-check` | off | 跳过对 SESSDATA 的 Bilibili nav 接口验证。**仅本地开发用**，开启后服务端会信任客户端自报的 `mid`/`uname` |
| `--db` | `data_exchange.db` | SQLite 文件路径，存储玩家金币 / 资源 / 冷却 |
| `--debug` | off | Flask debug 模式 |

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

### `GET /collections/{name}/stream` *(SSE 推送)*

服务端推送：连接建立后立即发一帧 `snapshot`，此后每次该集合 upsert / delete / 过期都推一帧 `change`，附带完整未过期条目列表。客户端可以用此通道**完全替代轮询**。

**事件格式（标准 SSE）**：

```
event: change
data: {"type": "snapshot|upsert|delete|expire", "items": [...], "ts": 1700000000.0}

: keepalive       ← 每 15s 一次注释行保活，不会触发 onmessage
```

推荐与轮询并存：把 `GET /collections/{name}` 当兜底，SSE 断线重连期间仍能拿到列表。

### `GET /health`

健康检查。返回 `collections` / `items` / `sessions` 数；`auth_collections` 显示哪些集合已开启鉴权；`bilibili_check` 显示是否真的对接了 Bilibili 校验。

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

## 会话 / 鉴权 API

### `POST /sessions`

用 B 站 SESSDATA 换一个短期 token，后续上行写请求带 `Authorization: Bearer <token>` 就够了，不再需要每次发 SESSDATA。

**Request body**：

```json
{ "sessdata": "..." }            // 默认必须；服务端会调 Bilibili nav 接口校验
{ "sessdata": "...", "mid": 12345, "uname": "Alice" }   // 仅 --no-bilibili-check 时
```

**Response**：

```json
{ "ok": true, "data": { "token": "...", "mid": 12345, "uname": "Alice", "expires_at": 1700003600.0 } }
```

Token 默认 1h **滑动过期**：每次被使用都会续到 `now + 3600s`。同一 mid 重新签发会撤销旧 token（隐式单点登录）。

### `GET /sessions/me`

用当前 token 查身份。失败 401。

### `DELETE /sessions`

主动注销当前 token。

## 受保护集合

启动时通过 `--auth-collections rooms,players` 标记的 collection：

- `POST /collections/<name>/items` 必须带 `Authorization: Bearer <token>`，否则 401。
- 服务端会**单方面写入** `data.owner_mid` / `data.owner_uname`，客户端伪造无效。
- 同一 id 已存在时，校验当前调用方 mid 与已有 `owner_mid` 一致；否则 403。
- `DELETE` 同理：只有 owner 能删自己的 item。
- 读永远开放（`GET /collections/<name>` / `GET .../stream` 不需 token）。

非受保护集合保持原行为；如果调用方仍然带了 token，服务端依然会盖 owner 戳，方便客户端读取时做归属。

## Actions API（权威写入）

所有"会改服务端权威状态"的写入都走这里。

### `POST /actions`

**Headers**：`Authorization: Bearer <token>`

**Body**：

```json
{ "type": "<handler-name>", "client_seq": 42, "...": "..." }
```

- `type` 必填，对应服务端 `@action_handler("<name>")` 注册的处理器。
- `client_seq` 单调递增整数。服务端记录每个 mid 的最大 seq，**任何 ≤ 最大值的请求会被 409 拒绝**，做重放 / 乱序保护。
- 校验失败时返回 4xx + 错误信息（处理器可 raise `ActionError(msg, status)`）。

### 内置 actions

| type | 说明 | 失败码 |
|---|---|---|
| `ping` | 测试 token 是否生效 | — |
| `get_resources` | 读当前玩家金币 / 冷却（自动懒建行） | — |
| `claim_daily_bonus` | 每 24h 领 100 金币；冷却由**服务端时钟**判定 | 429 |
| `spend_coins` | 扣 `amount` 金币（body: `{amount, reason}`）。余额不足返回 402 | 400/402 |

### 扩展自己的 action

```python
from server import action_handler, ActionError, _db_apply_coins_delta

@action_handler("feed_pet")
def _feed_pet(sess, body):
    # 1) 服务端校验
    pet_id = body.get("pet_id")
    if not isinstance(pet_id, str):
        raise ActionError("pet_id 必须是字符串", 400)
    # 2) 扣资源（原子，余额不足自动 402）
    updated = _db_apply_coins_delta(sess["mid"], -10)
    # 3) 应用业务效果（写自己的表 / 推 SSE 等）
    return {"affinity_gain": 5, "resources": updated}
```

关键约束：

- **永远不接受客户端上报最终值**。客户端只发"意图"（`amount`, `pet_id`, `direction`），服务端按规则裁决。
- 涉及金币 / 物品的修改一律用 `_db_apply_coins_delta` 等持久化 helper，确保重启不丢。
- 速率敏感的（位移、攻击）自行加冷却（用 `last_*_ts` 字段或 `_LAST_SEQ`）。

## 持久化

- 表 `player_resources(mid, coins, last_daily_claim_ts, updated_at)` 自动建。
- WAL 模式 + autocommit，单 SQLite 连接 + RLock 串行写，单机几百 TPS 没压力。
- 备份：直接 `cp data_exchange.db data_exchange.db.bak`（WAL 模式安全）。

## 设计要点

- **三层分离**：collection 层无业务、推送层无业务、action 层才有业务。要新业务加 handler 即可，不动框架。
- **零信任客户端**：受保护集合的 owner 由服务端打戳；action 走 token + seq + 业务校验。
- **同步全程 RLock + 单 SQLite 连接**：吞吐够中小规模（几百 QPS / 几百活跃玩家），出瓶颈再换 Redis / Postgres。
- **SSE > 轮询**：服务端延迟 ms 级；客户端实现一份 SSE consumer + 轮询兜底就够。

## 已知限制

- **单进程**：横向扩展需把 `_STORE` / `_SESSIONS` 搬 Redis、`_publish` 走 Pub/Sub。
- **无回放 / 历史**：`data` 是覆盖式的，需要审计就额外写日志表。
- **token 仅内存**：服务进程重启 = 所有用户被踢，需要重新 `/sessions`。可接受的话就这样；要 SSO 体验就把 `_SESSIONS` 也落 SQLite。
- **Bilibili nav 限速未知**：高并发登录场景下需自行加 LRU 缓存（按 SESSDATA hash），减少对 b 站接口的调用。

## 文件

- `server.py` — 全部实现（约 600 行）
- `requirements.txt` — Flask 依赖
- `data_exchange.db` — SQLite 持久化文件（运行后自动生成，可 `.gitignore`）
