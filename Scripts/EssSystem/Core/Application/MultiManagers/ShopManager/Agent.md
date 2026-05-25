# ShopManager 指南

## 概述

`ShopManager`（`[Manager(19)]`）+ `ShopService`（业务服务 + 持久化）提供商店交易系统：
配置 / 货币 / 钱包 / Buy-Sell 事务原子性。

**与 NPC 解耦**：本 Manager 不知道 NPC 是谁；NpcConfig 通过 `ShopId` 字段反向指向某 ShopConfig，
触发 `OpenShop` 由业务侧粘合。**与 IStats 软依赖**：CHA 折扣公式可降级为固定 markup。

## 状态

🚧 **骨架阶段**：Manager / Service 已挂入优先级链；Dao（`ShopConfig` / `ShopStock` /
`ShopPolicy` / `ShopType` / `CurrencyEntry`）已定义；事务流程、价格公式、ShopUI、Wallet
API 尚未实现。详见 `Demo/Tribe/ToDo.md` 条目 #4 后置（Shop）M4-M6。

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
| `ShopService.CAT_WALLETS`    = `"Wallets"`     | 玩家钱包余额（按 playerId+currencyId） |

## 价格公式（实施时落到 ShopFormulas.cs）

```
基础买入价 = stock.BasePrice * Policy.BuyMarkupRatio
CHA 折扣  = max(0, (CHA - 10) * 0.01)        // 每点 CHA -1%
最终买入价 = round(基础买入价 * (1 - CHA折扣))

卖出价 = item.Value * stock.SellbackRatio * Policy.SellMarkdownRatio * (1 + (CHA-10)*0.005)
```

## Event API

通过 `EventProcessor.Instance.TriggerEventMethod(EVT_*, args)` 调用。

| 常量 | 字符串 | 参数 | 返回 |
|---|---|---|---|
| `ShopManager.EVT_REGISTER_SHOP` | `"ShopRegister"` | `[ShopConfig]` | `Ok(id)` |
| `ShopManager.EVT_REGISTER_CURRENCY` | `"ShopRegisterCurrency"` | `[CurrencyEntry]` | `Ok(id)` |
| `ShopManager.EVT_BUY_ITEM` | `"ShopBuy"` | `[shopId, itemId, amount?, playerId?]` | `Ok("购买成功")` / `Fail(msg)` |
| `ShopManager.EVT_INIT_WALLET` | `"ShopInitWallet"` | `[playerId, currencyId, amount]` | `Ok()` / `Fail(msg)` |
| `ShopManager.EVT_GET_WALLET` | `"ShopGetWallet"` | `[playerId, currencyId]` | `Ok(amount)` / `Fail(msg)` |
| `ShopManager.EVT_ADD_WALLET` | `"ShopAddWallet"` | `[playerId, currencyId, amount]` | `Ok()` / `Fail(msg)` |

### 计划中（尚未实现）

`OpenShop` / `CloseShop` / `ShopSellItem` — M4 实施时新增。

货币常量：`ShopService.CURRENCY_GOLD = "gold"` / `ShopService.CURRENCY_SILVER = "silver"`
