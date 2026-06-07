# Tribe Demo

## 职责
- 记录 Tribe Demo 的构建、资源、UI、启动日志和 Demo 内部边界。
- 模块路径：`Demo/Tribe`。
- 本文档只记录模块契约，具体实现细节以代码为准。

## 结构
- `Background/`
- `Editor/`
- `Entity/`
- `Interaction/`
- `Player/`
- `Resource/`
- `World/`
- `need.md`
- `ToDo.md`
- `Tribe.unity`
- `TribeCollisionLayers.cs`
- `TribeGameManager.cs`

## 边界
- 本模块只拥有“职责”中描述的行为，不隐式接管兄弟模块职责。
- 跨模块协作优先使用 EventProcessor 字符串协议，或目标模块明确暴露的窄接口。
- Demo 专属逻辑留在 Demo 目录，除非已经确认可复用为框架能力。

## Event API
- 本目录没有声明本地 EVT_XXX 常量。

## 维护注意
- Tribe 运行时资源优先来自 FrameworkResources/Tribe。
- 启动性能排查优先看项目根目录 Logs/Tribe/log.txt。
- Tribe UI 定制留在 Demo 内，除非已经抽象成可复用框架能力。
- 新增、改名或删除事件常量时，同步更新本节和根目录 Events.md。
- 示例保持最小化；实现细节写在代码注释里，模块契约写在本文档里。
- 已完成的 TODO 从本文档移除，必要时移动到 TODO.md。
