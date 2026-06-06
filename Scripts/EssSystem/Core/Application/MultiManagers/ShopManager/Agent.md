# ShopManager 指南
## 概述
`ShopManager`（`[Manager(19)]`）+ `ShopService`（业务服务 + 持久化）提供商店交易系统：
配置 / 货币 / 钱包 / Buy-Sell 事务原子性。
**与 NPC 解耦**：本 Manager 不知道 NPC 是谁；NpcConfig 通过 `ShopId` 字段反向指向某 ShopConfig，
触发 `OpenShop` 由业务侧粘合。**与 IStats 软依赖**：CHA 折扣公式可降级为固定 markup。
## 状态
Service 已提供商店注册、货币注册、钱包初始化 / 充值 / 查询、购买事务与失败回滚。
钱包底层复用 Inventory 容器，但 ShopManager 只通过 Inventory 事件通信，不直接依赖 Inventory DTO。
## 文件结构
```
ShopManager/
├── ShopManager.cs               薄门面（Manager 单例）
├── ShopService.cs               业务服务（CAT_CONFIGS / CAT_CURRENCIES / CAT_WALLETS）
├── Agent.md                     本文档
└── Dao/
    ├── ShopConfig.cs            Id / DisplayName / OwnerNpcConfigId / Type / Stock / Policy / CurrencyId
    ├── ShopStock.cs             ItemId / BasePrice / Stock / SellbackRatio / 补货
    ├── ShopPolicy.cs            BuyMarkupRatio / SellMarkdownRatio / AcceptedSellTypes / CHA 阈值
    ├── ShopType.cs              General / Weapon / Armor / Magic / BlackMarket
    └── CurrencyEntry.cs         Id / DisplayName / IconSpriteId
```
## 数据分类（持久化）
| 常量 | 用途 |
|---|---|
| `ShopService.CAT_CONFIGS`    = `"ShopConfigs"` | 已注册 `ShopConfig`（按 Id） |
| `ShopService.CAT_CURRENCIES` = `"Currencies"`  | 已注册 `CurrencyEntry`（按 Id） |
| `wallet_{playerId}` Inventory | 玩家钱包容器；余额通过 `InventoryCountItem` 查询 |
## 价格公式（实施时落到 ShopFormulas.cs）
```
基础买入价 = stock.BasePrice * Policy.BuyMarkupRatio
CHA 折扣  = max(0, (CHA - 10) * 0.01)        // 每点 CHA -1%
最终买入价 = round(基础买入价 * (1 - CHA折扣))
卖出价 = item.Value * stock.SellbackRatio * Policy.SellMarkdownRatio * (1 + (CHA-10)*0.005)
```
## Event API

> Full Event definitions (params / return / side effects / usage) live in root Events.md -> section: **ShopManager Event**.

- `ShopManager.EVT_ADD_WALLET`
- `ShopManager.EVT_BUY_ITEM`
- `ShopManager.EVT_GET_WALLET`
- `ShopManager.EVT_INIT_WALLET`
- `ShopManager.EVT_REGISTER_CURRENCY`
- `ShopManager.EVT_REGISTER_SHOP`
