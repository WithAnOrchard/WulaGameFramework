# 长期优化 TODO

记录在功能迭代过程中识别出、但**当前用最小修法绕过**的框架/架构问题。
按"价值 × 改动成本"排序，越靠前越值得优先处理。

---

## 模块解耦（重要）

> **目标**：实现模块间绝对解耦，各模块可独立使用、替换或移除。

### 当前状态：❌ 未完成

**背景**：
- 框架设计初衷是"绝对解耦"，模块间通过 EventProcessor 通信
- 当前存在以下紧密耦合：
  - `SkillManager` ↔ `EntityManager`（58个文件直接 using）
  - `BuildingManager` ↔ `EntityManager`（6个文件直接 using）
  - `ShopManager` ↔ `InventoryManager`（1个文件直接 using）

**需要做的事情**：

1. **EntityManager 新增事件**
   - `EVT_ENTITY_SET_HP` - 设置实体 HP
   - `EVT_ENTITY_GET_HP` - 获取实体 HP
   - `EVT_ENTITY_GET_POSITION` - 获取实体位置
   - `EVT_ENTITY_SET_POSITION` - 设置实体位置
   - `EVT_ENTITY_REGISTER_DEATH_CALLBACK` - 注册死亡回调
   - `EVT_ENTITY_HAS_CAPABILITY` - 检查能力
   - `EVT_ENTITY_GET_CHARACTER_ROOT` - 获取 CharacterRoot

2. **BuildingManager 重构**
   - 移除对 `EntityManager.Dao` 的 using
   - 改用事件与 EntityManager 通信
   - `Building` 类中 `Entity` 引用改为 `string EntityInstanceId`

3. **SkillManager 重构**
   - 移除对 `EntityManager.Dao` 的 using
   - 移除对 `EntityManager.Capabilities` 的 using
   - 移除对 `EntityManager.Runtime` 的 using
   - `CastSkill` 事件参数中 `Entity` 类型改为 `string entityId`
   - 所有 Effects 重构为使用事件

**风险评估**：
- 改动范围：~35 个文件
- 复杂度：极高
- 建议：分阶段执行，每阶段充分测试后再继续
