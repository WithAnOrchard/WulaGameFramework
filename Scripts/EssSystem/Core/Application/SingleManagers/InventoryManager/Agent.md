# InventoryManager 背包模块

## 职责
- 负责物品配置、堆叠规则、背包数据、装备栏和默认背包 UI。
- 模块路径：`Scripts/EssSystem/Core/Application/SingleManagers/InventoryManager`。
- 本文档只记录模块契约，具体实现细节以代码为准。

## 结构
- `Dao/`
- `Editor/`
- `Runtime/`
- `UI/`
- `InventoryManager.cs`
- `InventoryService.cs`

## 边界
- 本模块只拥有“职责”中描述的行为，不隐式接管兄弟模块职责。
- 跨模块协作优先使用 EventProcessor 字符串协议，或目标模块明确暴露的窄接口。
- Demo 专属逻辑留在 Demo 目录，除非已经确认可复用为框架能力。

## Event API
- `InventoryManager.EVT_CLOSE_UI` = `"CloseInventoryUI"`
- `InventoryManager.EVT_HOTBAR_USE` = `"InventoryHotbarUse"`
- `InventoryManager.EVT_OPEN_UI` = `"OpenInventoryUI"`
- `InventoryManager.EVT_REGISTER_ITEM` = `"InventoryRegisterItem"`
- `InventoryManager.EVT_REGISTER_PICKABLE_ITEM` = `"InventoryRegisterPickableItem"`
- `InventoryManager.EVT_SPAWN_PICKABLE_ITEM` = `"InventorySpawnPickableItem"`
- `InventoryService.EVT_ADD` = `"InventoryAdd"`
- `InventoryService.EVT_CHANGED` = `"InventoryChanged"`
- `InventoryService.EVT_COUNT_ITEM` = `"InventoryCountItem"`
- `InventoryService.EVT_CREATE` = `"InventoryCreate"`
- `InventoryService.EVT_DELETE` = `"InventoryDelete"`
- `InventoryService.EVT_MOVE` = `"InventoryMove"`
- `InventoryService.EVT_QUERY` = `"InventoryQuery"`
- `InventoryService.EVT_REMOVE` = `"InventoryRemove"`

## 维护注意
- 新增、改名或删除事件常量时，同步更新本节和根目录 Events.md。
- 示例保持最小化；实现细节写在代码注释里，模块契约写在本文档里。
- 已完成的 TODO 从本文档移除，必要时移动到 TODO.md。
- 默认物品模板、堆叠、图标、描述、快捷使用效果等数据优先写入 `Assets/FrameworkResources/Config/Framework/Inventory/*.json`，不要在业务代码里注册同类默认配置；构建后外部可通过同路径 `FrameworkResources/Config` 覆盖。
- `InventoryUIBuilder` 只能实现通用 UI 构建能力，不能写入 Demo/项目专属实体 ID、装备部位、素材路径或固定业务语义；这类内容必须放入 Inventory JSON 配置或 Demo 自己的适配层。
- 背包、装备栏、快捷栏等通用 UI 框、按钮、空槽提示和装饰图资源优先放在 `Assets/FrameworkResources/Common/UI/Inventory`；Demo 专属物品或场景素材才放入对应 Demo 资源目录。
