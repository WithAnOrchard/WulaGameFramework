# ShopManager 商店模块

## 职责
- 负责商店配置、价格、库存规则、购买和出售流程。
- 模块路径：`Scripts/EssSystem/Core/Application/MultiManagers/ShopManager`。
- 本文档只记录模块契约，具体实现细节以代码为准。

## 结构
- `Dao/`
- `ShopManager.cs`
- `ShopService.cs`

## 边界
- 本模块只拥有“职责”中描述的行为，不隐式接管兄弟模块职责。
- 跨模块协作优先使用 EventProcessor 字符串协议，或目标模块明确暴露的窄接口。
- Demo 专属逻辑留在 Demo 目录，除非已经确认可复用为框架能力。

## Event API
- `ShopManager.EVT_ADD_WALLET` -> `ShopService.EVT_ADD_WALLET`
- `ShopManager.EVT_BUY_ITEM` -> `ShopService.EVT_BUY_ITEM`
- `ShopManager.EVT_GET_WALLET` -> `ShopService.EVT_GET_WALLET`
- `ShopManager.EVT_INIT_WALLET` -> `ShopService.EVT_INIT_WALLET`
- `ShopManager.EVT_REGISTER_CURRENCY` -> `ShopService.EVT_REGISTER_CURRENCY`
- `ShopManager.EVT_REGISTER_SHOP` -> `ShopService.EVT_REGISTER_SHOP`
- `ShopService.EVT_ADD_WALLET` = `"ShopAddWallet"`
- `ShopService.EVT_BUY_ITEM` = `"ShopBuy"`
- `ShopService.EVT_GET_WALLET` = `"ShopGetWallet"`
- `ShopService.EVT_INIT_WALLET` = `"ShopInitWallet"`
- `ShopService.EVT_REGISTER_CURRENCY` = `"ShopRegisterCurrency"`
- `ShopService.EVT_REGISTER_SHOP` = `"ShopRegister"`

## 维护注意
- 新增、改名或删除事件常量时，同步更新本节和根目录 Events.md。
- 示例保持最小化；实现细节写在代码注释里，模块契约写在本文档里。
- 已完成的 TODO 从本文档移除，必要时移动到 TODO.md。
