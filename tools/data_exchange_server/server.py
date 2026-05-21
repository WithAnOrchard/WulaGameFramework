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
import json
import threading
import time
from collections import defaultdict
from typing import Any

from flask import Flask, jsonify, request

# ─── 内存存储 ────────────────────────────────────────────────────

_LOCK = threading.RLock()
# {collection_name: {item_id: {"data": {...}, "expires_at": float, "updated_at": float}}}
_STORE: dict[str, dict[str, dict[str, Any]]] = defaultdict(dict)

DEFAULT_TTL_SECONDS = 30.0
GC_INTERVAL_SECONDS = 5.0


def _now() -> float:
    return time.time()


def _is_alive(entry: dict[str, Any], now: float | None = None) -> bool:
    exp = entry.get("expires_at", 0.0)
    if exp <= 0:  # 永久
        return True
    return (now if now is not None else _now()) < exp


def _gc_loop() -> None:
    while True:
        time.sleep(GC_INTERVAL_SECONDS)
        try:
            now = _now()
            removed = 0
            with _LOCK:
                for col_name, items in list(_STORE.items()):
                    for item_id, entry in list(items.items()):
                        if not _is_alive(entry, now):
                            del items[item_id]
                            removed += 1
                    if not items:
                        del _STORE[col_name]
            if removed:
                print(f"[gc] 清理 {removed} 条过期记录")
        except Exception as ex:  # pragma: no cover
            print(f"[gc] 错误: {ex}")


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
    return _ok({"collections": n_collections, "items": n_items, "uptime": _now()})


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

    return _ok({"id": item_id, "ttl_remaining": ttl_remaining})


@app.get("/collections/<name>")
def list_collection(name: str):
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
    # 按 updated_at 倒序，最近上报的在前
    alive.sort(key=lambda e: e["updated_at"], reverse=True)
    return _ok(alive, count=len(alive))


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
    with _LOCK:
        items = _STORE.get(name)
        if items is None or item_id not in items:
            return _fail("not found", 404)
        del items[item_id]
        if not items:
            del _STORE[name]
    return _ok({"id": item_id, "deleted": True})


# ─── Entry Point ─────────────────────────────────────────────────

def main() -> None:
    parser = argparse.ArgumentParser(description="通用数据收发器")
    parser.add_argument("--host", default="0.0.0.0")
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--debug", action="store_true")
    args = parser.parse_args()

    gc_thread = threading.Thread(target=_gc_loop, daemon=True, name="DataExchangeGC")
    gc_thread.start()

    print(f"[data-exchange] 启动 http://{args.host}:{args.port}")
    print(f"[data-exchange] GC 间隔 {GC_INTERVAL_SECONDS}s, 默认 TTL {DEFAULT_TTL_SECONDS}s")
    app.run(host=args.host, port=args.port, debug=args.debug, threaded=True)


if __name__ == "__main__":
    main()
