# DobeCat 内存优化指南

> 当前运行时内存 ~200MB，目标优化至 50-80MB（桌面宠物合理范围）

---

## 📊 内存分析

### 当前问题（200MB）

| 组件 | 估计占用 | 问题 |
|---|---|---|
| **UI 面板** | ~40-60MB | 多个 Panel 同时加载，未及时卸载 |
| **Sprite 缓存** | ~30-50MB | Plants sheet 全量加载，未按需卸载 |
| **弹幕/礼物数据** | ~20-30MB | 历史消息缓存未清理 |
| **农场 GameObject** | ~15-20MB | 9 个农田格子 + 背景 + 标签 GO |
| **框架 Manager** | ~20-30MB | 多个 Manager 单例 + 事件系统 |
| **其他** | ~20-40MB | 音频、资源、临时对象 |

---

## 🎯 优化方案

### 优先级 1：高收益（预计节省 60-80MB）

#### 1.1 **UI 面板延迟加载 + 卸载**
**问题**：所有 Panel 在启动时一次性创建，即使不使用也占用内存

**方案**：
```csharp
// 修改 DobeCatGameManager.cs
// 之前：启动时创建所有 Panel
// 现在：首次打开时创建，关闭时卸载

public class DobeCatSettingsPanelView : MonoBehaviour
{
    private static DobeCatSettingsPanelView _instance;
    private bool _initialized;

    public static void Show()
    {
        if (_instance == null)
            _instance = CreateInstance(); // 首次创建
        _instance._Show();
    }

    public static void Hide()
    {
        if (_instance != null)
            _instance._Hide();
    }

    public static void Unload()
    {
        if (_instance != null)
        {
            Destroy(_instance.gameObject);
            _instance = null;
            _initialized = false;
        }
    }
}
```

**预期节省**：30-40MB

---

#### 1.2 **Sprite Sheet 按需加载 + 卸载**
**问题**：`FarmTileObject.cs` 中 `_plantsSheet` 一次性加载所有植物 sprite，即使只用 10%

**方案**：
```csharp
// 修改 FarmTileObject.cs
private static Sprite[] _plantsSheet;
private static int _plantsSheetRefCount = 0;

private static Sprite LoadPlantSprite(string spriteName)
{
    if (string.IsNullOrEmpty(spriteName)) return null;
    if (_spriteCache.TryGetValue(spriteName, out var cached) && cached != null) 
        return cached;

    // 延迟加载：只在需要时加载
    if (_plantsSheet == null)
    {
        _plantsSheet = Resources.LoadAll<Sprite>("Sprites/Plants/Plants");
        _plantsSheetRefCount++;
        Debug.Log($"[FarmTile] 加载 Plants sheet，引用计数={_plantsSheetRefCount}");
    }

    if (_plantsSheet != null)
        foreach (var s in _plantsSheet)
            if (s.name == spriteName) 
            { 
                _spriteCache[spriteName] = s; 
                return s; 
            }
    return null;
}

// 在 FarmWorldController.OnDestroy() 中调用
public void UnloadPlantSprites()
{
    if (_plantsSheetRefCount > 0)
    {
        _plantsSheetRefCount--;
        if (_plantsSheetRefCount == 0)
        {
            Resources.UnloadAsset(_plantsSheet);
            _plantsSheet = null;
            _spriteCache.Clear();
            Debug.Log("[FarmTile] 卸载 Plants sheet");
        }
    }
}
```

**预期节省**：20-30MB

---

#### 1.3 **弹幕消息缓存限制**
**问题**：`DobeCatTestPanel` 中弹幕消息无限累积

**方案**：
```csharp
// 修改 DobeCatTestPanel.cs
private const int MAX_DANMU_HISTORY = 100; // 只保留最近 100 条
private Queue<string> _danmuHistory = new Queue<string>();

private void AppendLine(string text)
{
    _danmuHistory.Enqueue(text);
    if (_danmuHistory.Count > MAX_DANMU_HISTORY)
        _danmuHistory.Dequeue();
    
    // 重新构建显示文本
    UpdateDetailText();
}

private void UpdateDetailText()
{
    var sb = new StringBuilder();
    foreach (var line in _danmuHistory)
        sb.AppendLine(line);
    
    if (DanmuTestPanelView.Instance != null)
        DanmuTestPanelView.Instance.DetailText = sb.ToString();
}
```

**预期节省**：10-15MB

---

### 优先级 2：中等收益（预计节省 20-30MB）

#### 2.1 **农场 GameObject 池化**
**问题**：每次进入农场都创建 9 个 GO，退出时销毁，重复创建销毁

**方案**：
```csharp
// 创建 FarmGameObjectPool.cs
public class FarmGameObjectPool
{
    private static List<GameObject> _tilePool = new List<GameObject>();
    private static List<GameObject> _plantPool = new List<GameObject>();
    private const int POOL_SIZE = 9;

    public static void Initialize()
    {
        for (int i = 0; i < POOL_SIZE; i++)
        {
            _tilePool.Add(new GameObject($"PooledTile_{i}"));
            _plantPool.Add(new GameObject($"PooledPlant_{i}"));
        }
    }

    public static GameObject GetTile()
    {
        if (_tilePool.Count > 0)
        {
            var go = _tilePool[0];
            _tilePool.RemoveAt(0);
            go.SetActive(true);
            return go;
        }
        return new GameObject("Tile");
    }

    public static void ReturnTile(GameObject go)
    {
        go.SetActive(false);
        _tilePool.Add(go);
    }

    public static void Cleanup()
    {
        foreach (var go in _tilePool) Object.Destroy(go);
        foreach (var go in _plantPool) Object.Destroy(go);
        _tilePool.Clear();
        _plantPool.Clear();
    }
}
```

**预期节省**：5-10MB

---

#### 2.2 **UI 图标缓存**
**问题**：`DobeCatTestPanelView` 和 `DobeCatGiftStatsPanelView` 中重复加载相同图标

**方案**：
```csharp
// 创建 UIIconCache.cs
public static class UIIconCache
{
    private static Dictionary<string, Sprite> _cache = new();

    public static Sprite GetIcon(string path)
    {
        if (_cache.TryGetValue(path, out var spr) && spr != null)
            return spr;

        spr = Resources.Load<Sprite>(path);
        if (spr == null)
        {
            var all = Resources.LoadAll<Sprite>(path);
            spr = all?.Length > 0 ? all[0] : null;
        }

        if (spr != null)
            _cache[path] = spr;
        return spr;
    }

    public static void Clear()
    {
        _cache.Clear();
    }
}

// 使用
var spr = UIIconCache.GetIcon("UI/icon");
```

**预期节省**：2-5MB

---

### 优先级 3：长期优化（预计节省 10-20MB）

#### 3.1 **Addressable Assets 替代 Resources**
**问题**：`Resources` 文件夹中的资源无法精细控制加载/卸载

**方案**：
- 迁移关键资源到 Addressable Assets
- 支持异步加载 + 引用计数卸载
- 支持资源版本管理

#### 3.2 **音频流式加载**
**问题**：所有音频文件一次性加载到内存

**方案**：
- 背景音乐：使用 `AudioClip.loadType = StreamFromDisk`
- 音效：保持 `LoadInMemory`（文件小）

#### 3.3 **事件系统优化**
**问题**：EventProcessor 中事件监听器未及时移除

**方案**：
- 确保所有 `AddListener` 都有对应 `RemoveListener`
- 在 `OnDestroy()` 中清理监听器

---

## 📋 优化检查清单

### 立即可做（本周）

- [ ] **UI 面板延迟加载**
  - [ ] 修改 DobeCatSettingsPanelView
  - [ ] 修改 DobeCatAlarmPanelView
  - [ ] 修改 DobeCatPomodoroPanelView
  - [ ] 修改 DobeCatGiftStatsPanelView
  - [ ] 测试内存变化

- [ ] **Sprite 缓存优化**
  - [ ] 添加引用计数机制
  - [ ] 在农场关闭时卸载
  - [ ] 测试内存变化

- [ ] **弹幕消息限制**
  - [ ] 限制历史消息数量
  - [ ] 测试内存变化

### 后续优化（1-2 周）

- [ ] **GameObject 池化**
  - [ ] 创建 FarmGameObjectPool
  - [ ] 集成到 FarmWorldController
  - [ ] 性能测试

- [ ] **UI 图标缓存**
  - [ ] 创建 UIIconCache
  - [ ] 替换所有图标加载
  - [ ] 测试内存变化

### 长期规划（1 个月+）

- [ ] **迁移到 Addressable Assets**
- [ ] **音频流式加载**
- [ ] **事件系统审计**

---

## 🧪 内存测试方法

### 方法 1：Unity Profiler

```
Window → Analysis → Profiler
→ Memory 标签
→ Take Sample
→ 查看各组件内存占用
```

### 方法 2：内存快照对比

```csharp
// 在 DobeCatGameManager.cs 中添加
[ContextMenu("Print Memory")]
private void PrintMemory()
{
    var totalMemory = System.GC.GetTotalMemory(false);
    Debug.Log($"[Memory] 总内存: {totalMemory / 1024 / 1024}MB");
}
```

### 方法 3：逐步优化验证

```
1. 记录基线：200MB
2. 应用优化 1.1（UI 延迟加载）→ 测试 → 记录
3. 应用优化 1.2（Sprite 卸载）→ 测试 → 记录
4. 应用优化 1.3（消息限制）→ 测试 → 记录
5. 累计对比
```

---

## 📈 优化目标

| 阶段 | 目标 | 预期时间 |
|---|---|---|
| **现状** | 200MB | - |
| **Phase 1** | 120-140MB | 本周 |
| **Phase 2** | 80-100MB | 1 周 |
| **Phase 3** | 50-70MB | 1 个月 |

---

## 💡 最佳实践

### 1. **资源加载原则**
- ✅ 按需加载（延迟初始化）
- ✅ 及时卸载（使用完毕立即释放）
- ✅ 引用计数（多个模块共享资源时）
- ❌ 一次性加载所有资源
- ❌ 资源无限累积

### 2. **GameObject 管理**
- ✅ 使用对象池（频繁创建销毁）
- ✅ 及时销毁不用的 GO
- ✅ 使用 SetActive(false) 而非 Destroy
- ❌ 创建过多临时 GO

### 3. **事件系统**
- ✅ 在 OnDisable 中移除监听器
- ✅ 使用弱引用避免循环引用
- ❌ 监听器泄漏

### 4. **缓存策略**
- ✅ 设置缓存上限
- ✅ LRU 淘汰策略
- ✅ 定期清理过期缓存
- ❌ 无限增长的缓存

---

## 🔗 相关文档

- `DESIGN_STATUS.md` - 功能完成度
- `DobeCatGameManager.cs` - 启动流程
- `PetReactionController.cs` - 事件系统

---

## 📞 问题排查

### 问题：优化后内存仍未下降
**排查步骤**：
1. 确认 `Resources.UnloadUnusedAssets()` 已调用
2. 检查是否有事件监听器泄漏
3. 使用 Profiler 定位具体占用

### 问题：优化后出现卡顿
**排查步骤**：
1. 检查是否在主线程加载大资源
2. 考虑使用异步加载
3. 使用 Profiler 检查 GC 压力

### 问题：优化后功能异常
**排查步骤**：
1. 检查卸载时机是否正确
2. 确认缓存失效逻辑
3. 添加日志追踪资源生命周期

