# SkillManager 技能模块

## 职责
- 负责技能注册、学习、释放、效果执行和运行时能力解耦。
- 模块路径：`Scripts/EssSystem/Core/Application/MultiManagers/SkillManager`。
- 本文档只记录模块契约，具体实现细节以代码为准。

## 结构
- `Dao/`
- `Runtime/`
- `SkillManager.cs`
- `SkillService.cs`

## 边界
- 本模块只拥有“职责”中描述的行为，不隐式接管兄弟模块职责。
- 跨模块协作优先使用 EventProcessor 字符串协议，或目标模块明确暴露的窄接口。
- Demo 专属逻辑留在 Demo 目录，除非已经确认可复用为框架能力。

## Event API
- `SkillManager.EVT_CAST_SKILL` = `"CastSkill"`
- `SkillManager.EVT_LEARN_SKILL` = `"LearnSkill"`
- `SkillManager.EVT_REGISTER_SKILL` = `"RegisterSkill"`

## 维护注意
- 技能效果保持数据配置与运行时执行分离。
- SkillManager 不直接依赖 Demo 实体类；需要实体状态时走 EntityManager 能力或事件协议。
- 新增、改名或删除事件常量时，同步更新本节和根目录 Events.md。
- 示例保持最小化；实现细节写在代码注释里，模块契约写在本文档里。
- 已完成的 TODO 从本文档移除，必要时移动到 TODO.md。
