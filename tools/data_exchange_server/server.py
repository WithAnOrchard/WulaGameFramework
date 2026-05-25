"""
通用数据收发器 (Data Exchange Server)
====================================

设计目标
--------
一个**与业务无关**的轻量 HTTP REST 服务，提供：
- 任意命名的"集合 (collection)"
- 集合内可放任意 JSON 条目，每条带 TTL（心跳保活）
- 超时未续约的条目自动 GC 清除

典型用例（多人房间列表）
-----------------------
- Mirror Host 每 10s 心跳：
  ``POST /collections/rooms/items``
  body: ``{"id": "room_abc", "ttl": 30, "data": {
    "name": "我的房间", "host": "1.2.3.4", "port": 7777,
    "players": ["Alice", "Bob"], "max": 4
  }}``
- Mirror Client 拉列表：
  ``GET /collections/rooms``
  返回所有未过期房间。

接口
----
``POST /collections/{name}/items``        upsert 一条；body 见下
``GET  /collections/{name}``              列出未过期条目
``GET  /collections/{name}/items/{id}``   单条
``DELETE /collections/{name}/items/{id}`` 主动下线
``GET  /health``                          健康检查

upsert body schema
------------------
``{"id": "string (必填，自定义稳定 ID)",
   "ttl": 30,                  # 秒，默认 30；0 = 永久
   "data": {...}}``            # 任意 JSON 数据

返回统一形如：``{"ok": true, "data": ...}`` 或 ``{"ok": false, "error": "msg"}``。

运行
----
``pip install flask``
``python server.py --host 0.0.0.0 --port 8765``

线程安全
--------
后台 GC 线程每 5s 扫一次清理过期项。所有读写都加 ``RLock``。
"""

from __future__ import annotations

import argparse
import hashlib
import json
import queue
import secrets
import sqlite3
import threading
import time
import urllib.error
import urllib.request
from collections import defaultdict
from typing import Any, Callable

from flask import Flask, Response, jsonify, request, stream_with_context

# ─── 内存存储 ────────────────────────────────────────────────────

_LOCK = threading.RLock()
# {collection_name: {item_id: {"data": {...}, "expires_at": float, "updated_at": float}}}
_STORE: dict[str, dict[str, dict[str, Any]]] = defaultdict(dict)

# SSE 订阅者：{collection_name: set[queue.Queue]}
_SUBSCRIBERS: dict[str, set[queue.Queue]] = defaultdict(set)
_SUB_LOCK = threading.RLock()

DEFAULT_TTL_SECONDS = 30.0
GC_INTERVAL_SECONDS = 5.0
SSE_KEEPALIVE_SECONDS = 15.0

# ─── 会话 / 鉴权 ─────────────────────────────────────────────────
# {token: {"mid": int, "uname": str, "expires_at": float, "sessdata_hash": str, ...}}
_SESSIONS: dict[str, dict[str, Any]] = {}
# 需要鉴权才能写的集合名（CLI --auth-collections 配置）。读永远开放。
_AUTH_REQUIRED_COLLECTIONS: set[str] = set()
# 是否真的对接 Bilibili API 验证 SESSDATA。开发期可用 --no-bilibili-check 关掉，信任客户端自报 mid。
_REQUIRE_BILIBILI_VALIDATION = True
SESSION_TTL_SECONDS = 3600.0  # 滑动过期：每次使用都续到 now+TTL
# 防重放：每个 mid 已处理过的最大 client_seq
_LAST_SEQ: dict[int, int] = {}


def _now() -> float:
    return time.time()


def _is_alive(entry: dict[str, Any], now: float | None = None) -> bool:
    exp = entry.get("expires_at", 0.0)
    if exp <= 0:  # 永久
        return True
    return (now if now is not None else _now()) < exp


def _snapshot(name: str, now: float | None = None) -> list[dict[str, Any]]:
    """返回某集合当前所有未过期条目（与 GET /collections/<name> 同结构）。"""
    if now is None:
        now = _now()
    with _LOCK:
        items = _STORE.get(name, {})
        alive = [
            {
                "id": entry["id"],
                "data": entry["data"],
                "ttl_remaining": -1.0 if entry["expires_at"] <= 0 else max(0.0, entry["expires_at"] - now),
                "updated_at": entry["updated_at"],
            }
            for entry in items.values()
            if _is_alive(entry, now)
        ]
    alive.sort(key=lambda e: e["updated_at"], reverse=True)
    return alive


def _publish(name: str, event_type: str) -> None:
    """向集合的所有 SSE 订阅者推送一次完整快照。失败的队列被自动清理。"""
    payload = {"type": event_type, "items": _snapshot(name), "ts": _now()}
    msg = f"event: change\ndata: {json.dumps(payload, ensure_ascii=False)}\n\n"
    with _SUB_LOCK:
        subs = list(_SUBSCRIBERS.get(name, ()))
    for q in subs:
        try:
            q.put_nowait(msg)
        except Exception:
            with _SUB_LOCK:
                _SUBSCRIBERS.get(name, set()).discard(q)


def _gc_loop() -> None:
    while True:
        time.sleep(GC_INTERVAL_SECONDS)
        try:
            now = _now()
            changed_collections: set[str] = set()
            removed = 0
            with _LOCK:
                for col_name, items in list(_STORE.items()):
                    for item_id, entry in list(items.items()):
                        if not _is_alive(entry, now):
                            del items[item_id]
                            removed += 1
                            changed_collections.add(col_name)
                    if not items:
                        del _STORE[col_name]
            for col in changed_collections:
                _publish(col, "expire")
            sess_removed = _gc_sessions(now)
            if removed or sess_removed:
                print(f"[gc] 清理 {removed} 条过期记录 / {sess_removed} 个过期会话")
        except Exception as ex:  # pragma: no cover
            print(f"[gc] 错误: {ex}")


# ─── 会话辅助 ────────────────────────────────────────────────────

def _hash_sessdata(s: str) -> str:
    return hashlib.sha256(s.encode("utf-8")).hexdigest()[:16]


def _validate_sessdata_via_bilibili(sessdata: str, timeout: float = 5.0) -> dict[str, Any] | None:
    """通过 Bilibili nav 接口校验 SESSDATA。成功返回 {mid, uname}，失败返回 None。"""
    url = "https://api.bilibili.com/x/web-interface/nav"
    req = urllib.request.Request(url, headers={
        "Cookie": f"SESSDATA={sessdata}",
        "User-Agent": "Mozilla/5.0 DobeCatDataExchange",
    })
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            payload = json.loads(resp.read().decode("utf-8"))
    except (urllib.error.URLError, TimeoutError, json.JSONDecodeError, OSError) as ex:
        print(f"[auth] bilibili validate failed: {ex}")
        return None
    if payload.get("code") != 0:
        return None
    data = payload.get("data") or {}
    if not data.get("isLogin"):
        return None
    mid = data.get("mid")
    if not mid:
        return None
    return {"mid": int(mid), "uname": str(data.get("uname") or "")}


def _issue_session(mid: int, uname: str, sessdata: str) -> dict[str, Any]:
    """签发一个新 token，并撤销同一 mid 的旧 token（单点登录）。"""
    token = secrets.token_hex(24)
    now = _now()
    sess = {
        "token": token,
        "mid": int(mid),
        "uname": uname,
        "expires_at": now + SESSION_TTL_SECONDS,
        "sessdata_hash": _hash_sessdata(sessdata),
        "created_at": now,
        "last_seen": now,
    }
    with _LOCK:
        for tk, s in list(_SESSIONS.items()):
            if s["mid"] == mid:
                del _SESSIONS[tk]
        _SESSIONS[token] = sess
    return sess


def _get_session_from_request() -> dict[str, Any] | None:
    """从 Authorization: Bearer <token> 解析当前会话；滑动续期；失效返回 None。"""
    auth = request.headers.get("Authorization", "")
    if not auth.startswith("Bearer "):
        return None
    token = auth[7:].strip()
    if not token:
        return None
    now = _now()
    with _LOCK:
        sess = _SESSIONS.get(token)
        if sess is None:
            return None
        if sess["expires_at"] < now:
            del _SESSIONS[token]
            return None
        sess["last_seen"] = now
        sess["expires_at"] = now + SESSION_TTL_SECONDS
    return sess


def _gc_sessions(now: float) -> int:
    removed = 0
    with _LOCK:
        for tk, s in list(_SESSIONS.items()):
            if s["expires_at"] < now:
                del _SESSIONS[tk]
                removed += 1
    return removed


# ─── Action 处理器注册 ───────────────────────────────────────────

_ACTION_HANDLERS: dict[str, Callable[[dict[str, Any], dict[str, Any]], Any]] = {}


class ActionError(Exception):
    """Action 处理器抛此异常以返回明确的非 200 状态码（如 cooldown→429、余额不足→402）。"""

    def __init__(self, msg: str, status: int = 400):
        super().__init__(msg)
        self.status = status


def action_handler(action_type: str):
    """注册一个 action 处理器：fn(session, body) -> result_dict。
    业务校验失败时 raise ActionError(msg, status)。"""
    def deco(fn):
        _ACTION_HANDLERS[action_type] = fn
        return fn
    return deco


@action_handler("ping")
def _act_ping(sess, body):
    return {"pong": True, "mid": sess["mid"], "ts": _now()}


# ─── 持久化层（SQLite） ──────────────────────────────────────────
# 关键资源（金币、领取冷却等）必须落盘，否则服务器重启 = 大家钱清零。
# 房间 / presence 这类短期状态留在内存即可。

_DB_PATH = "data_exchange.db"
_DB_LOCK = threading.RLock()
_db_conn: sqlite3.Connection | None = None


def _db_init(path: str) -> None:
    """初始化 SQLite 连接 + 建表。autocommit (isolation_level=None) + WAL 提高并发读。"""
    global _db_conn, _DB_PATH
    _DB_PATH = path
    _db_conn = sqlite3.connect(path, check_same_thread=False, isolation_level=None)
    _db_conn.execute("PRAGMA journal_mode=WAL")
    _db_conn.execute("PRAGMA synchronous=NORMAL")
    _db_conn.execute(
        """
        CREATE TABLE IF NOT EXISTS player_resources (
            mid                 INTEGER PRIMARY KEY,
            coins               INTEGER NOT NULL DEFAULT 0,
            last_daily_claim_ts REAL    NOT NULL DEFAULT 0,
            updated_at          REAL    NOT NULL DEFAULT 0
        )
        """
    )
    print(f"[db] SQLite 已就绪: {path}")


def _db_get_resources(mid: int) -> dict[str, Any]:
    """读玩家资源行；不存在则懒创建。返回 dict 形式的快照。"""
    assert _db_conn is not None, "DB 未初始化"
    with _DB_LOCK:
        cur = _db_conn.execute(
            "SELECT mid, coins, last_daily_claim_ts FROM player_resources WHERE mid=?",
            (mid,),
        )
        row = cur.fetchone()
        if row is None:
            now = _now()
            _db_conn.execute(
                "INSERT INTO player_resources(mid, coins, last_daily_claim_ts, updated_at) VALUES(?,0,0,?)",
                (mid, now),
            )
            return {"mid": mid, "coins": 0, "last_daily_claim_ts": 0.0}
        return {"mid": row[0], "coins": row[1], "last_daily_claim_ts": row[2]}


def _db_apply_coins_delta(mid: int, delta: int, *, allow_negative: bool = False,
                          set_claim_ts: float | None = None) -> dict[str, Any]:
    """原子地修改 coins。allow_negative=False 时余额不够会 raise ActionError(402)。"""
    assert _db_conn is not None, "DB 未初始化"
    with _DB_LOCK:
        # 确保行存在
        _db_get_resources(mid)
        cur = _db_conn.execute("SELECT coins FROM player_resources WHERE mid=?", (mid,))
        coins = int(cur.fetchone()[0])
        new_coins = coins + int(delta)
        if not allow_negative and new_coins < 0:
            raise ActionError(f"余额不足: 当前 {coins} 不足 {-delta}", 402)
        now = _now()
        if set_claim_ts is not None:
            _db_conn.execute(
                "UPDATE player_resources SET coins=?, last_daily_claim_ts=?, updated_at=? WHERE mid=?",
                (new_coins, set_claim_ts, now, mid),
            )
        else:
            _db_conn.execute(
                "UPDATE player_resources SET coins=?, updated_at=? WHERE mid=?",
                (new_coins, now, mid),
            )
        return _db_get_resources(mid)


# ─── 示例 actions：玩家资源 ─────────────────────────────────────
# 这些处理器演示"客户端报意图、服务端裁决"的反作弊范式，按需扩展。

DAILY_BONUS_AMOUNT = 100
DAILY_BONUS_COOLDOWN_SECONDS = 24 * 3600.0


@action_handler("get_resources")
def _act_get_resources(sess, body):
    return _db_get_resources(sess["mid"])


@action_handler("claim_daily_bonus")
def _act_claim_daily_bonus(sess, body):
    """每 24h 领一次金币。冷却由服务端时钟判定，客户端 ts 完全不参与。"""
    mid = sess["mid"]
    now = _now()
    res = _db_get_resources(mid)
    elapsed = now - float(res.get("last_daily_claim_ts") or 0.0)
    if elapsed < DAILY_BONUS_COOLDOWN_SECONDS:
        wait = int(DAILY_BONUS_COOLDOWN_SECONDS - elapsed)
        raise ActionError(f"daily bonus cooldown: 还需 {wait}s", 429)
    updated = _db_apply_coins_delta(mid, DAILY_BONUS_AMOUNT, set_claim_ts=now)
    return {"granted": DAILY_BONUS_AMOUNT, "resources": updated}


@action_handler("spend_coins")
def _act_spend_coins(sess, body):
    """扣金币（购买 / 抽卡 / 喂养扣费的通用入口）。
    body: {"amount": int>0, "reason": "feed_pet" | ...}
    服务端只信 amount 是非负整数，至于"扣完之后给什么"由扩展 handler 接手。
    """
    amount = body.get("amount")
    if not isinstance(amount, int) or amount <= 0:
        raise ActionError("amount 必须是正整数", 400)
    reason = str(body.get("reason") or "")
    updated = _db_apply_coins_delta(sess["mid"], -amount)
    print(f"[action] mid={sess['mid']} 扣 {amount} 金币 (reason={reason}) → 余额 {updated['coins']}")
    return {"spent": amount, "resources": updated}


# ─── Flask App ───────────────────────────────────────────────────

app = Flask(__name__)


def _ok(data: Any = None, **extra: Any):
    payload = {"ok": True, "data": data}
    payload.update(extra)
    return jsonify(payload)


def _fail(msg: str, status: int = 400):
    return jsonify({"ok": False, "error": msg}), status


@app.get("/health")
def health():
    with _LOCK:
        n_collections = len(_STORE)
        n_items = sum(len(v) for v in _STORE.values())
        n_sessions = len(_SESSIONS)
    return _ok({
        "collections": n_collections,
        "items": n_items,
        "sessions": n_sessions,
        "uptime": _now(),
        "auth_collections": sorted(_AUTH_REQUIRED_COLLECTIONS),
        "bilibili_check": _REQUIRE_BILIBILI_VALIDATION,
    })


# ─── 会话路由 ────────────────────────────────────────────────────

@app.post("/sessions")
def create_session():
    """登录：用 SESSDATA 换 token。
    body: {"sessdata": "...", "mid"?: int, "uname"?: str}
    mid/uname 仅在服务器以 --no-bilibili-check 启动时生效（dev 信任模式）。
    """
    body = request.get_json(silent=True) or {}
    sessdata = body.get("sessdata")
    if not isinstance(sessdata, str) or not sessdata:
        return _fail("缺少 sessdata", 400)

    if _REQUIRE_BILIBILI_VALIDATION:
        info = _validate_sessdata_via_bilibili(sessdata)
        if info is None:
            return _fail("SESSDATA 验证失败（无效或网络异常）", 401)
    else:
        mid = body.get("mid")
        if not isinstance(mid, int) or mid <= 0:
            return _fail("trust 模式需要 mid (int>0)", 400)
        info = {"mid": int(mid), "uname": str(body.get("uname") or "anon")}

    sess = _issue_session(info["mid"], info["uname"], sessdata)
    return _ok({
        "token": sess["token"],
        "mid": sess["mid"],
        "uname": sess["uname"],
        "expires_at": sess["expires_at"],
    })


@app.get("/sessions/me")
def whoami():
    sess = _get_session_from_request()
    if sess is None:
        return _fail("no session", 401)
    return _ok({"mid": sess["mid"], "uname": sess["uname"], "expires_at": sess["expires_at"]})


@app.delete("/sessions")
def revoke_session():
    sess = _get_session_from_request()
    if sess is None:
        return _fail("no session", 401)
    with _LOCK:
        _SESSIONS.pop(sess["token"], None)
    return _ok({"revoked": True})


# ─── Actions 路由（统一上行入口） ────────────────────────────────

@app.post("/actions")
def post_action():
    """所有"修改权威状态"的操作走这里。
    body: {"type": "<handler-name>", "client_seq"?: int, ...其它字段}
    需 Authorization: Bearer <token>。
    """
    sess = _get_session_from_request()
    if sess is None:
        return _fail("require session", 401)
    body = request.get_json(silent=True) or {}
    atype = body.get("type")
    if not isinstance(atype, str) or atype not in _ACTION_HANDLERS:
        return _fail(f"unknown action type: {atype}", 400)

    seq = body.get("client_seq")
    if isinstance(seq, int):
        with _LOCK:
            last = _LAST_SEQ.get(sess["mid"], 0)
            if seq <= last:
                return _fail("stale client_seq（重放或乱序）", 409)
            _LAST_SEQ[sess["mid"]] = seq

    try:
        result = _ACTION_HANDLERS[atype](sess, body)
    except ActionError as ex:
        return _fail(str(ex), ex.status)
    except Exception as ex:
        print(f"[action] handler '{atype}' unexpected error: {ex}")
        return _fail(f"action error: {ex}", 500)
    return _ok(result)


@app.post("/collections/<name>/items")
def upsert_item(name: str):
    body = request.get_json(silent=True) or {}
    item_id = body.get("id")
    if not item_id or not isinstance(item_id, str):
        return _fail("缺少必填字段 id (string)", 400)

    ttl = body.get("ttl", DEFAULT_TTL_SECONDS)
    try:
        ttl = float(ttl)
    except (TypeError, ValueError):
        return _fail("ttl 必须是数字（秒）", 400)

    data = body.get("data", {})
    if not isinstance(data, dict):
        data = {}

    # 鉴权：受保护集合必须带合法 token；并保证调用方只能改自己的 owner item。
    sess = _get_session_from_request()
    auth_required = name in _AUTH_REQUIRED_COLLECTIONS
    if auth_required and sess is None:
        return _fail("collection requires session", 401)
    if sess is not None:
        # 服务端单方面打 owner 戳，客户端伪造的 owner_mid 字段会被覆盖。
        data = dict(data)
        data["owner_mid"] = sess["mid"]
        data["owner_uname"] = sess["uname"]
        with _LOCK:
            existing = _STORE.get(name, {}).get(item_id)
            if existing is not None:
                ex_owner = (existing.get("data") or {}).get("owner_mid")
                if ex_owner is not None and ex_owner != sess["mid"]:
                    return _fail("not item owner", 403)

    now = _now()
    expires_at = 0.0 if ttl <= 0 else now + ttl

    with _LOCK:
        _STORE[name][item_id] = {
            "id": item_id,
            "data": data,
            "expires_at": expires_at,
            "updated_at": now,
        }
        # 计算"剩余 ttl"返回，方便客户端验证
        ttl_remaining = -1.0 if expires_at <= 0 else expires_at - now

    _publish(name, "upsert")
    return _ok({"id": item_id, "ttl_remaining": ttl_remaining})


@app.get("/collections/<name>")
def list_collection(name: str):
    alive = _snapshot(name)
    return _ok(alive, count=len(alive))


@app.get("/collections/<name>/stream")
def stream_collection(name: str):
    """SSE 推流：连接即发一次 snapshot，之后每次 upsert/delete/expire 推一帧。"""
    q: queue.Queue = queue.Queue(maxsize=64)
    with _SUB_LOCK:
        _SUBSCRIBERS[name].add(q)

    def gen():
        try:
            # 初始 snapshot
            initial = {"type": "snapshot", "items": _snapshot(name), "ts": _now()}
            yield f"event: change\ndata: {json.dumps(initial, ensure_ascii=False)}\n\n"
            while True:
                try:
                    msg = q.get(timeout=SSE_KEEPALIVE_SECONDS)
                    yield msg
                except queue.Empty:
                    # 保活心跳：注释行不会触发客户端 onmessage
                    yield ": keepalive\n\n"
        except GeneratorExit:
            pass
        finally:
            with _SUB_LOCK:
                _SUBSCRIBERS.get(name, set()).discard(q)

    headers = {
        "Content-Type": "text/event-stream; charset=utf-8",
        "Cache-Control": "no-cache, no-transform",
        "X-Accel-Buffering": "no",
        "Connection": "keep-alive",
    }
    return Response(stream_with_context(gen()), headers=headers)


@app.get("/collections/<name>/items/<item_id>")
def get_item(name: str, item_id: str):
    now = _now()
    with _LOCK:
        entry = _STORE.get(name, {}).get(item_id)
        if entry is None or not _is_alive(entry, now):
            return _fail("not found", 404)
        return _ok({
            "id": entry["id"],
            "data": entry["data"],
            "ttl_remaining": -1.0 if entry["expires_at"] <= 0 else max(0.0, entry["expires_at"] - now),
            "updated_at": entry["updated_at"],
        })


@app.delete("/collections/<name>/items/<item_id>")
def delete_item(name: str, item_id: str):
    sess = _get_session_from_request()
    auth_required = name in _AUTH_REQUIRED_COLLECTIONS
    if auth_required and sess is None:
        return _fail("collection requires session", 401)

    with _LOCK:
        items = _STORE.get(name)
        if items is None or item_id not in items:
            return _fail("not found", 404)
        if sess is not None:
            ex_owner = (items[item_id].get("data") or {}).get("owner_mid")
            if ex_owner is not None and ex_owner != sess["mid"]:
                return _fail("not item owner", 403)
        del items[item_id]
        if not items:
            del _STORE[name]
    _publish(name, "delete")
    return _ok({"id": item_id, "deleted": True})


# ─── Entry Point ─────────────────────────────────────────────────

def main() -> None:
    global _AUTH_REQUIRED_COLLECTIONS, _REQUIRE_BILIBILI_VALIDATION

    parser = argparse.ArgumentParser(description="通用数据收发器")
    parser.add_argument("--host", default="0.0.0.0")
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--debug", action="store_true")
    parser.add_argument(
        "--auth-collections",
        default="",
        help="逗号分隔，列出需要会话鉴权才能写的 collection；读永远开放。例：rooms,players",
    )
    parser.add_argument(
        "--no-bilibili-check",
        action="store_true",
        help="跳过对 SESSDATA 的 Bilibili nav 接口验证，仅用于本地开发（信任客户端自报 mid）",
    )
    parser.add_argument(
        "--db",
        default="data_exchange.db",
        help="SQLite 数据库文件路径（持久化玩家金币 / 资源 / 冷却）。默认 ./data_exchange.db",
    )
    args = parser.parse_args()

    _AUTH_REQUIRED_COLLECTIONS = {s.strip() for s in args.auth_collections.split(",") if s.strip()}
    _REQUIRE_BILIBILI_VALIDATION = not args.no_bilibili_check

    _db_init(args.db)

    gc_thread = threading.Thread(target=_gc_loop, daemon=True, name="DataExchangeGC")
    gc_thread.start()

    print(f"[data-exchange] 启动 http://{args.host}:{args.port}")
    print(f"[data-exchange] GC 间隔 {GC_INTERVAL_SECONDS}s, 默认 TTL {DEFAULT_TTL_SECONDS}s")
    print(f"[data-exchange] 受保护集合: {sorted(_AUTH_REQUIRED_COLLECTIONS) or '(无)'}; "
          f"Bilibili 校验: {'开' if _REQUIRE_BILIBILI_VALIDATION else '关（信任模式）'}")
    print(f"[data-exchange] 已注册 actions: {sorted(_ACTION_HANDLERS.keys())}")
    app.run(host=args.host, port=args.port, debug=args.debug, threaded=True)


if __name__ == "__main__":
    main()
