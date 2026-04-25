# ResourceManager 机制 Agent 指南

## 概述

ResourceManager 机制是 EssSystem 的资源管理系统，提供统一的资源加载、缓存和管理功能。本指南面向 AI Agent，说明如何使用 ResourceManager 和 ResourceService 进行资源管理。

## 核心组件

### 1. ResourceManager
```csharp
[Manager(0)]
public class ResourceManager : Manager<ResourceManager>
```

**用途**: Unity MonoBehaviour 资源管理器，提供对外的 Event 接口

**特性**:
- 继承自 Manager<ResourceManager>
- 所有公开方法标记 `[Event]` 特性
- 通过 Event 调用本地 ResourceService
- 支持同步和异步资源加载
- 支持外部文件加载

### 2. ResourceService
```csharp
public class ResourceService : Service<ResourceService>
```

**用途**: 资源服务，实现具体的资源加载和缓存逻辑

**特性**:
- 继承自 Service<ResourceService>
- 所有公开方法标记 `[Event]` 特性
- 内置资源缓存机制
- 支持预加载配置
- 自动数据持久化

### 3. ResourceType
```csharp
public enum ResourceType
{
    Prefab,      // 预制体
    Sprite,      // 精灵图片
    AudioClip,   // 音频剪辑
    Texture      // 纹理
}
```

## 使用方法

### 1. 获取资源（同步）

```csharp
// 获取 Sprite
var result = EventProcessor.Instance.TriggerEventMethod("GetSprite", 
    new List<object> { "Sprites/UI/Button" });
if (result != null && result.Count >= 2 && result[0].ToString() == "成功")
{
    Sprite sprite = result[1] as Sprite;
}

// 获取 Prefab
var prefabResult = EventProcessor.Instance.TriggerEventMethod("GetPrefab", 
    new List<object> { "Prefabs/Player" });
if (prefabResult != null && prefabResult.Count >= 2 && prefabResult[0].ToString() == "成功")
{
    GameObject prefab = prefabResult[1] as GameObject;
}

// 获取 AudioClip
var audioResult = EventProcessor.Instance.TriggerEventMethod("GetAudioClip", 
    new List<object> { "Audio/Music/Background" });
if (audioResult != null && audioResult.Count >= 2 && audioResult[0].ToString() == "成功")
{
    AudioClip clip = audioResult[1] as AudioClip;
}

// 获取 Texture
var textureResult = EventProcessor.Instance.TriggerEventMethod("GetTexture", 
    new List<object> { "Textures/Character" });
if (textureResult != null && textureResult.Count >= 2 && textureResult[0].ToString() == "成功")
{
    Texture2D texture = textureResult[1] as Texture2D;
}
```

### 2. 获取外部资源

```csharp
// 获取外部 Sprite
var result = EventProcessor.Instance.TriggerEventMethod("GetExternalSprite", 
    new List<object> { "C:/Images/CustomSprite.png" });
if (result != null && result.Count >= 2 && result[0].ToString() == "成功")
{
    Sprite sprite = result[1] as Sprite;
}
```

### 3. 异步加载资源

```csharp
// 异步加载 Prefab
var result = EventProcessor.Instance.TriggerEventMethod("LoadPrefabAsync", 
    new List<object> { "Prefabs/Enemy" });
// 返回: ["加载中"] 或 ["成功", resource]

// 异步加载 Sprite
var result = EventProcessor.Instance.TriggerEventMethod("LoadSpriteAsync", 
    new List<object> { "Sprites/UI/Icon" });
// 返回: ["加载中"] 或 ["成功", resource]

// 异步加载外部 Sprite
var result = EventProcessor.Instance.TriggerEventMethod("LoadExternalSpriteAsync", 
    new List<object> { "C:/Images/External.png" });
// 返回: ["加载中"] 或 ["成功", resource]
```

### 4. 添加预加载配置

```csharp
// 添加 Prefab 预加载配置
var result = EventProcessor.Instance.TriggerEventMethod("AddPreloadConfig", 
    new List<object> { "player_prefab", "Prefabs/Player", ResourceType.Prefab, false });

// 添加 Sprite 预加载配置
var result = EventProcessor.Instance.TriggerEventMethod("AddPreloadConfig", 
    new List<object> { "ui_button", "Sprites/UI/Button", ResourceType.Sprite, false });

// 添加外部 Sprite 预加载配置
var result = EventProcessor.Instance.TriggerEventMethod("AddPreloadConfig", 
    new List<object> { "custom_sprite", "C:/Images/Custom.png", ResourceType.Sprite, true });
```

### 5. 卸载资源

```csharp
// 卸载指定资源
var result = EventProcessor.Instance.TriggerEventMethod("UnloadResource", 
    new List<object> { "Prefabs/Player", false });

// 卸载外部资源
var result = EventProcessor.Instance.TriggerEventMethod("UnloadResource", 
    new List<object> { "C:/Images/Custom.png", true });

// 卸载所有资源
var result = EventProcessor.Instance.TriggerEventMethod("UnloadAllResources", 
    new List<object>());
```

## 内部 Event 方法

### ResourceManager Event 方法
- `GetPrefab(path)` - 获取 Prefab
- `GetSprite(path)` - 获取 Sprite
- `GetAudioClip(path)` - 获取 AudioClip
- `GetTexture(path)` - 获取 Texture
- `GetExternalSprite(filePath)` - 获取外部 Sprite
- `LoadPrefabAsync(path)` - 异步加载 Prefab
- `LoadSpriteAsync(path)` - 异步加载 Sprite
- `LoadExternalSpriteAsync(filePath)` - 异步加载外部 Sprite
- `AddPreloadConfig(id, path, type, isExternal)` - 添加预加载配置
- `UnloadResource(path, isExternal)` - 卸载资源
- `UnloadAllResources()` - 卸载所有资源

### ResourceService Event 方法
- `OnResourceDataLoaded()` - 数据加载完成触发预加载
- `GetResource(path, typeStr, isExternal)` - 获取资源
- `AddResourceConfig(id, path, type, isExternal)` - 添加资源配置
- `LoadResourceAsync(path, typeStr, isExternal)` - 异步加载资源
- `LoadExternalImageAsync(filePath)` - 异步加载外部图片
- `UnloadResource(path, isExternal)` - 卸载资源
- `UnloadAllResources()` - 卸载所有资源

## 使用示例

### 示例 1: 在 UIManager 中加载 Sprite

```csharp
[Manager(5)]
public class UIManager : Manager<UIManager>
{
    private ResourceManager _resourceManager;

    protected override void Initialize()
    {
        base.Initialize();
        _resourceManager = ResourceManager.Instance;
    }

    [Event("LoadAndSetSprite")]
    public List<object> LoadAndSetSprite(List<object> data)
    {
        string spritePath = data[0] as string;
        Image targetImage = data[1] as Image;

        // 通过 Event 调用 ResourceManager 获取 Sprite
        var result = EventProcessor.Instance.TriggerEventMethod("GetSprite", 
            new List<object> { spritePath });

        if (result != null && result.Count >= 2 && result[0].ToString() == "成功")
        {
            Sprite sprite = result[1] as Sprite;
            if (sprite != null && targetImage != null)
            {
                targetImage.sprite = sprite;
                return new List<object> { "成功" };
            }
        }

        return new List<object> { "加载失败" };
    }
}
```

### 示例 2: 在 GameplayManager 中实例化 Prefab

```csharp
[Manager(10)]
public class GameplayManager : Manager<GameplayManager>
{
    private ResourceManager _resourceManager;

    protected override void Initialize()
    {
        base.Initialize();
        _resourceManager = ResourceManager.Instance;
    }

    [Event("SpawnEnemy")]
    public List<object> SpawnEnemy(List<object> data)
    {
        string prefabPath = data[0] as string;
        Vector3 position = (Vector3)data[1];

        // 通过 Event 调用 ResourceManager 获取 Prefab
        var result = EventProcessor.Instance.TriggerEventMethod("GetPrefab", 
            new List<object> { prefabPath });

        if (result != null && result.Count >= 2 && result[0].ToString() == "成功")
        {
            GameObject prefab = result[1] as GameObject;
            if (prefab != null)
            {
                GameObject enemy = Instantiate(prefab, position, Quaternion.identity);
                return new List<object> { "成功", enemy };
            }
        }

        return new List<object> { "加载失败" };
    }
}
```

### 示例 3: 在 AudioManager 中加载音频

```csharp
[Manager(6)]
public class AudioManager : Manager<AudioManager>
{
    private ResourceManager _resourceManager;

    protected override void Initialize()
    {
        base.Initialize();
        _resourceManager = ResourceManager.Instance;
    }

    [Event("PlayBackgroundMusic")]
    public List<object> PlayBackgroundMusic(List<object> data)
    {
        string clipPath = data[0] as string;

        // 通过 Event 调用 ResourceManager 获取 AudioClip
        var result = EventProcessor.Instance.TriggerEventMethod("GetAudioClip", 
            new List<object> { clipPath });

        if (result != null && result.Count >= 2 && result[0].ToString() == "成功")
        {
            AudioClip clip = result[1] as AudioClip;
            if (clip != null)
            {
                // 播放音频
                AudioSource.PlayClipAtPoint(clip, Vector3.zero);
                return new List<object> { "成功" };
            }
        }

        return new List<object> { "加载失败" };
    }
}
```

### 示例 4: 添加预加载配置

```csharp
[Manager(5)]
public class LoadingManager : Manager<LoadingManager>
{
    private ResourceManager _resourceManager;

    protected override void Initialize()
    {
        base.Initialize();
        _resourceManager = ResourceManager.Instance;
        SetupPreloadConfig();
    }

    private void SetupPreloadConfig()
    {
        // 添加 UI 资源预加载
        EventProcessor.Instance.TriggerEventMethod("AddPreloadConfig", 
            new List<object> { "main_menu_bg", "Sprites/UI/MainMenuBG", ResourceType.Sprite, false });
        
        EventProcessor.Instance.TriggerEventMethod("AddPreloadConfig", 
            new List<object> { "button_normal", "Sprites/UI/ButtonNormal", ResourceType.Sprite, false });

        // 添加 Prefab 预加载
        EventProcessor.Instance.TriggerEventMethod("AddPreloadConfig", 
            new List<object> { "player_prefab", "Prefabs/Player", ResourceType.Prefab, false });

        // 添加音频预加载
        EventProcessor.Instance.TriggerEventMethod("AddPreloadConfig", 
            new List<object> { "bgm_title", "Audio/Music/Title", ResourceType.AudioClip, false });
    }
}
```

## 资源缓存机制

### 缓存键优化（性能提升）
使用 `ResourceKey` 结构体替代字符串拼接作为缓存键：

```csharp
public struct ResourceKey : IEquatable<ResourceKey>
{
    public readonly string Path;
    public readonly bool IsExternal;

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (Path?.GetHashCode() ?? 0);
            hash = hash * 31 + IsExternal.GetHashCode();
            return hash;
        }
    }
}
```

**优势**:
- 减少字符串分配和 GC
- 提升字典查找速度
- 类型安全的缓存键
- 兼容性优化: 使用传统 hash 算法替代 HashCode.Combine，确保 Unity 版本兼容
- 机制不变: 对外 API 完全兼容

### 缓存键格式
```
ResourceKey(path, false)  - Unity 内部资源
ResourceKey(path, true)   - 外部文件资源
```

### 缓存行为
- 首次加载时缓存资源
- 再次请求时从缓存返回
- 卸载资源时从缓存移除
- 外部文件支持异步加载

## 数据持久化

ResourceService 继承自 Service，支持数据持久化：

**存储结构**:
```
ResourceService._dataStorage:
{
    "Prefab": {
        "player_prefab": { id: "player_prefab", path: "Prefabs/Player", isExternal: false, type: Prefab }
    },
    "Sprite": {
        "ui_button": { id: "ui_button", path: "Sprites/UI/Button", isExternal: false, type: Sprite }
    },
    "AudioClip": {
        "bgm_title": { id: "bgm_title", path: "Audio/Music/Title", isExternal: false, type: AudioClip }
    },
    "Texture": {
        "character_tex": { id: "character_tex", path: "Textures/Character", isExternal: false, type: Texture }
    }
}
```

## 最佳实践

### 1. 资源路径管理
```csharp
public class ResourcePaths
{
    public const string UI_BUTTON = "Sprites/UI/Button";
    public const string PLAYER_PREFAB = "Prefabs/Player";
    public const string BGM_TITLE = "Audio/Music/Title";
}

// 使用常量
var result = EventProcessor.Instance.TriggerEventMethod("GetSprite", 
    new List<object> { ResourcePaths.UI_BUTTON });
```

### 2. 异步加载检查
```csharp
var result = EventProcessor.Instance.TriggerEventMethod("LoadSpriteAsync", 
    new List<object> { spritePath });

if (result != null && result[0].ToString() == "加载中")
{
    // 显示加载动画
    ShowLoadingIndicator();
}
else if (result != null && result[0].ToString() == "成功")
{
    // 隐藏加载动画
    HideLoadingIndicator();
    Sprite sprite = result[1] as Sprite;
}
```

### 3. 资源卸载
```csharp
// 场景切换时卸载不需要的资源
[Event("OnSceneUnload")]
public List<object> OnSceneUnload(List<object> data)
{
    string sceneName = data[0] as string;
    
    if (sceneName == "Gameplay")
    {
        // 卸载 UI 资源
        EventProcessor.Instance.TriggerEventMethod("UnloadResource", 
            new List<object> { "Sprites/UI/MainMenu", false });
    }
    
    return new List<object> { "成功" };
}
```

### 4. 错误处理
```csharp
var result = EventProcessor.Instance.TriggerEventMethod("GetSprite", 
    new List<object> { spritePath });

if (result == null || result.Count == 0)
{
    LogError("获取资源失败：返回结果为空");
}
else if (result[0].ToString() != "成功")
{
    LogWarning($"获取资源失败：{result[0]}");
}
else if (result.Count < 2)
{
    LogError("获取资源失败：返回数据不完整");
}
else
{
    Sprite sprite = result[1] as Sprite;
    if (sprite == null)
    {
        LogError("获取资源失败：资源为 null");
    }
}
```

## 注意事项

1. **架构规范**: ResourceManager 只能通过 Event 调用本地 ResourceService，不能直接访问
2. **资源路径**: Unity 资源路径相对于 Resources 文件夹，不需要包含 "Resources/" 前缀
3. **外部文件**: 外部文件需要完整路径，确保文件存在且有读取权限
4. **异步加载**: 异步加载返回 "加载中" 状态，需要检查资源是否真正加载完成
5. **内存管理**: 不再使用的资源应该及时卸载，避免内存泄漏
6. **预加载**: 启动时添加预加载配置可以提升游戏加载体验
7. **数据持久化**: 预加载配置会自动保存，下次启动时自动加载
8. **线程安全**: 外部文件加载使用 Task.Run，通过 MainThreadDispatcher 回到主线程

## 常见问题

### Q: 如何获取 Sprite？
A: 使用 Event 调用 GetSprite 方法：
```csharp
var result = EventProcessor.Instance.TriggerEventMethod("GetSprite", 
    new List<object> { "Sprites/UI/Button" });
```

### Q: 如何异步加载资源？
A: 使用 LoadXxxAsync 方法：
```csharp
var result = EventProcessor.Instance.TriggerEventMethod("LoadSpriteAsync", 
    new List<object> { spritePath });
```

### Q: 如何加载外部文件？
A: 使用 GetExternalSprite 或 LoadExternalSpriteAsync：
```csharp
var result = EventProcessor.Instance.TriggerEventMethod("GetExternalSprite", 
    new List<object> { "C:/Images/Custom.png" });
```

### Q: 如何添加预加载配置？
A: 使用 AddPreloadConfig 方法：
```csharp
var result = EventProcessor.Instance.TriggerEventMethod("AddPreloadConfig", 
    new List<object> { "id", "path", ResourceType.Sprite, false });
```

### Q: 资源路径格式是什么？
A: Unity 资源路径相对于 Resources 文件夹，如 "Prefabs/Player" 对应 "Resources/Prefabs/Player.prefab"

### Q: 如何卸载资源？
A: 使用 UnloadResource 或 UnloadAllResources：
```csharp
var result = EventProcessor.Instance.TriggerEventMethod("UnloadResource", 
    new List<object> { path, isExternal });
```

### Q: ResourceManager 和 ResourceService 有什么区别？
A:
- **ResourceManager**: 对外的 Event 接口，符合架构规范
- **ResourceService**: 内部实现，处理具体的资源加载逻辑
- ResourceManager 通过 Event 调用 ResourceService

### Q: 如何检查资源是否加载成功？
A: 检查返回结果：
```csharp
if (result != null && result.Count >= 2 && result[0].ToString() == "成功")
{
    var resource = result[1];
}
```
