# DanmuManager UI

## 职责
- 负责弹幕测试面板和调试入口。
- 模块路径：`Scripts/EssSystem/Manager/DanmuManager/UI`。
- 本文档只记录模块契约，具体实现细节以代码为准。

## 结构
- `DanmuTestPanel.cs`
- `DanmuTestPanelView.cs`

## 边界
- 本模块只拥有“职责”中描述的行为，不隐式接管兄弟模块职责。
- 跨模块协作优先使用 EventProcessor 字符串协议，或目标模块明确暴露的窄接口。
- Demo 专属逻辑留在 Demo 目录，除非已经确认可复用为框架能力。

## Event API
- 本目录没有声明本地 EVT_XXX 常量。

## 维护注意
- 新增、改名或删除事件常量时，同步更新本节和根目录 Events.md。
- 示例保持最小化；实现细节写在代码注释里，模块契约写在本文档里。
- 已完成的 TODO 从本文档移除，必要时移动到 TODO.md。
