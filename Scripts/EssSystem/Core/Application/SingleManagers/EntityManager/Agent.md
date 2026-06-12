# EntityManager 实体模块

## 职责
- 负责实体配置、运行时注册、生命值、控制状态、能力组件和表现桥接。
- 模块路径：`Scripts/EssSystem/Core/Application/SingleManagers/EntityManager`。
- 本文档只记录模块契约，具体实现细节以代码为准。

## 结构
- `Brain/`
- `Capabilities/`
- `Dao/`
- `Runtime/`
- `CharacterViewBridge.cs`
- `EntityManager.cs`
- `EntityPhysicsConfig.cs`
- `EntityService.cs`

## 边界
- 本模块只拥有“职责”中描述的行为，不隐式接管兄弟模块职责。
- 跨模块协作优先使用 EventProcessor 字符串协议，或目标模块明确暴露的窄接口。
- Demo 专属逻辑留在 Demo 目录，除非已经确认可复用为框架能力。

## Event API
- `EntityManager.EVT_ADD_ENTITY_CAPABILITY` = `"AddEntityCapability"`
- `EntityManager.EVT_APPLY_COLLIDER` = `"ApplyCollider"`
- `EntityManager.EVT_ATTACH_ENTITY_HANDLE` = `"AttachEntityHandle"`
- `EntityManager.EVT_CONSUME_ENTITY_RESOURCE` = `"ConsumeEntityResource"`
- `EntityManager.EVT_CREATE_ENTITY` = `"CreateEntity"`
- `EntityManager.EVT_DAMAGE_ENTITY` = `"DamageEntity"`
- `EntityManager.EVT_DESTROY_ENTITY` = `"DestroyEntity"`
- `EntityManager.EVT_GET_CHARACTER_ROOT` = `"GetCharacterRoot"`
- `EntityManager.EVT_GET_CONTROL_STATE` = `"GetControlState"`
- `EntityManager.EVT_GET_DAMAGE_REDUCTION` = `"GetDamageReduction"`
- `EntityManager.EVT_GET_ENTITY` = `"GetEntity"`
- `EntityManager.EVT_GET_ENTITY_HP` = `"GetEntityHp"`
- `EntityManager.EVT_GET_ENTITY_ID_FROM_OBJECT` = `"GetEntityIdFromObject"`
- `EntityManager.EVT_GET_ENTITY_POSITION` = `"GetEntityPosition"`
- `EntityManager.EVT_GET_ENTITY_RESOURCE` = `"GetEntityResource"`
- `EntityManager.EVT_GET_SPEED_MULTIPLIER` = `"GetSpeedMultiplier"`
- `EntityManager.EVT_HEAL_ENTITY` = `"HealEntity"`
- `EntityManager.EVT_IS_ENTITY_DEAD` = `"IsEntityDead"`
- `EntityManager.EVT_POP_CONTROL_STATE` = `"PopControlState"`
- `EntityManager.EVT_PUSH_CONTROL_STATE` = `"PushControlState"`
- `EntityManager.EVT_REGISTER_DAMAGED_CALLBACK` = `"RegisterDamagedCallback"`
- `EntityManager.EVT_REGISTER_DEATH_CALLBACK` = `"RegisterDeathCallback"`
- `EntityManager.EVT_REGISTER_ENTITY_CONFIG` = `"RegisterEntityConfig"`
- `EntityManager.EVT_REGISTER_SCENE_ENTITY` = `"RegisterSceneEntity"`
- `EntityManager.EVT_REGISTER_SIMPLE_ENTITY_CONFIG` = `"RegisterSimpleEntityConfig"`
- `EntityManager.EVT_RESTORE_ENTITY_RESOURCE` = `"RestoreEntityResource"`
- `EntityManager.EVT_SET_DAMAGE_REDUCTION` = `"SetDamageReduction"`
- `EntityManager.EVT_SET_ENTITY_MAX_HP` = `"SetEntityMaxHp"`
- `EntityManager.EVT_SET_ENTITY_RESOURCE` = `"SetEntityResource"`
- `EntityManager.EVT_SET_ENTITY_POSITION` = `"SetEntityPosition"`
- `EntityManager.EVT_SET_SPEED_MULTIPLIER` = `"SetSpeedMultiplier"`

## 维护注意
- Demo 专属实体行为不要写进 EntityManager。
- 表现层通过 CharacterManager 事件或窄桥接对象访问。
- 技能效果应通过实体能力接口访问运行时状态，不依赖具体 Demo 实体类。
- 新增、改名或删除事件常量时，同步更新本节和根目录 Events.md。
- 示例保持最小化；实现细节写在代码注释里，模块契约写在本文档里。
- 已完成的 TODO 从本文档移除，必要时移动到 TODO.md。
