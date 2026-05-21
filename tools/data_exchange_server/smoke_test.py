"""
冒烟测试：完整跑一遍 upsert / list / get / 心跳续约 / TTL 过期 / delete。

用法：
    1. 先启动服务: python server.py
    2. 再跑:      python smoke_test.py
"""

from __future__ import annotations

import json
import sys
import time
import urllib.error
import urllib.request

BASE = "http://127.0.0.1:8765"


def req(method: str, path: str, body: dict | None = None) -> dict:
    url = BASE + path
    data = None
    headers = {}
    if body is not None:
        data = json.dumps(body).encode("utf-8")
        headers["Content-Type"] = "application/json"
    r = urllib.request.Request(url, data=data, method=method, headers=headers)
    try:
        with urllib.request.urlopen(r, timeout=5) as resp:
            return json.loads(resp.read())
    except urllib.error.HTTPError as e:
        return json.loads(e.read())


def expect(name: str, cond: bool, detail: str = "") -> None:
    flag = "PASS" if cond else "FAIL"
    print(f"  [{flag}] {name}{(' :: ' + detail) if detail else ''}")
    if not cond:
        sys.exit(1)


def main() -> None:
    print("[1] /health")
    h = req("GET", "/health")
    expect("health ok", h.get("ok") is True, str(h))

    print("[2] upsert room1 (ttl=3s)")
    u1 = req("POST", "/collections/rooms/items", {
        "id": "room1", "ttl": 3,
        "data": {"name": "测试房 1", "host": "127.0.0.1", "port": 7777, "players": ["A"]},
    })
    expect("upsert ok", u1.get("ok") is True, str(u1))
    expect("ttl_remaining ≈ 3", abs(u1["data"]["ttl_remaining"] - 3) < 0.5)

    print("[3] upsert room2 (ttl=10s)")
    req("POST", "/collections/rooms/items", {
        "id": "room2", "ttl": 10,
        "data": {"name": "测试房 2", "host": "127.0.0.1", "port": 7778, "players": ["B", "C"]},
    })

    print("[4] list rooms (should have 2)")
    lst = req("GET", "/collections/rooms")
    expect("count == 2", lst["count"] == 2, str(lst))

    print("[5] get single room1")
    g = req("GET", "/collections/rooms/items/room1")
    expect("get ok", g["ok"] is True and g["data"]["id"] == "room1")
    expect("data passthrough", g["data"]["data"]["name"] == "测试房 1")

    print("[6] sleep 4s, room1 应该过期，room2 还在")
    time.sleep(4)
    lst2 = req("GET", "/collections/rooms")
    ids = {x["id"] for x in lst2["data"]}
    expect("room1 过期", "room1" not in ids, str(ids))
    expect("room2 仍在", "room2" in ids, str(ids))

    print("[7] 心跳续约 room2（再 upsert 一次）")
    u3 = req("POST", "/collections/rooms/items", {
        "id": "room2", "ttl": 10,
        "data": {"name": "测试房 2 续约", "host": "127.0.0.1", "port": 7778, "players": ["B", "C", "D"]},
    })
    expect("续约 ok", u3.get("ok") is True)

    print("[8] delete room2")
    d = req("DELETE", "/collections/rooms/items/room2")
    expect("delete ok", d.get("ok") is True)

    print("[9] 再 list 应该空")
    lst3 = req("GET", "/collections/rooms")
    expect("count == 0", lst3["count"] == 0, str(lst3))

    print("\n[OK] 全部通过")


if __name__ == "__main__":
    main()
