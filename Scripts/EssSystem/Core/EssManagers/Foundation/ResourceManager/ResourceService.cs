using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.Util;
using EssSystem.Core.Event;
using EssSystem.Core;

namespace EssSystem.Core.EssManagers.Foundation.ResourceManager
{
    /// <summary>
    /// 资源缓存键（性能优化）
    /// </summary>
    public struct ResourceKey : IEquatable<ResourceKey>
    {
        public readonly string FileName;   // 文件名（不带扩展名）
        public readonly bool IsExternal;
        public readonly string TypeTag;    // 资源类型标签（"Prefab"/"Sprite"/"RuleTile"/...），避免同名不同类碰撞

        public ResourceKey(string path, bool isExternal, string typeTag = null)
        {
            FileName = ExtractFileNameWithoutExtension(path);
            IsExternal = isExternal;
            TypeTag = string.IsNullOrEmpty(typeTag) ? "" : typeTag;
        }

        private static string ExtractFileNameWithoutExtension(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;

            var fileName = System.IO.Path.GetFileName(path);
            if (string.IsNullOrEmpty(fileName)) return path;

            var fileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(fileName);
            return fileNameWithoutExt ?? path;
        }

        public bool Equals(ResourceKey other)
        {
            return FileName == other.FileName
                && IsExternal == other.IsExternal
                && TypeTag == other.TypeTag;
        }

        public override bool Equals(object obj)
        {
            return obj is ResourceKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (FileName?.GetHashCode() ?? 0);
                hash = hash * 31 + IsExternal.GetHashCode();
                hash = hash * 31 + (TypeTag?.GetHashCode() ?? 0);
                return hash;
            }
        }

        public override string ToString()
        {
            var prefix = IsExternal ? "external" : "unity";
            return string.IsNullOrEmpty(TypeTag) ? $"{prefix}:{FileName}" : $"{prefix}:{TypeTag}:{FileName}";
        }
    }

    /// <summary>
    /// 资源类型
    /// </summary>
    public enum ResourceType
    {
        Prefab,
        Sprite,
        AudioClip,
        Texture,
        RuleTile,
        AnimationClip,
    }

    /// <summary>
    /// 资源配置项
    /// </summary>
    [Serializable]
    public class ResourceConfigItem
    {
        public string id;
        public string path;
        public bool isExternal;
        public ResourceType type;
    }

    /// <summary>
    /// 资源服务 - 符合架构规范
    /// </summary>
    public class ResourceService : Service<ResourceService>
    {
        // ===== Event Constants (Service 内部 / 低层 API) =====
        public const string EVT_DATA_LOADED               = "OnResourceDataLoaded";
        public const string EVT_GET_RESOURCE              = "GetResource";
        public const string EVT_ADD_RESOURCE_CONFIG       = "AddResourceConfig";
        public const string EVT_LOAD_RESOURCE_ASYNC       = "LoadResourceAsync";
        public const string EVT_LOAD_EXTERNAL_IMAGE_ASYNC = "LoadExternalImageAsync";
        /// <summary>取出某个 FBX 模型内含的所有 AnimationClip。
        /// <para>Editor 路径：启动时 <see cref="_modelClipNames"/> 已预烘 → O(1)。</para>
        /// <para>Build 路径：优先读资源预生成的 manifest（<c>Resources/CharacterFBXManifest.json</c>），其中记录的 clip 名由 manifest提供 → 反取全局缓存里的 AnimationClip。</para>
        /// <para>data: [string modelPath]; 返回 Ok(List&lt;AnimationClip&gt;)。</para></summary>
        public const string EVT_GET_MODEL_CLIPS           = "GetModelClips";
        /// <summary>枚举已索引的所有 FBX/Model 路径（Resources 相对路径、不含扩展名）。返回 Ok(List&lt;string&gt;)。</summary>
        public const string EVT_GET_ALL_MODEL_PATHS       = "GetAllModelPaths";
        /// <summary>资源全部预加载 / 索引完成后一次性广播。订阅者按 <c>[EventListener(EVT_RESOURCES_LOADED)]</c> 接。</summary>
        public const string EVT_RESOURCES_LOADED          = "OnResourcesLoaded";
        // 外部图片加载广播事件（广播 / 订阅用）
        public const string EVT_EXTERNAL_IMAGE_LOADED      = "OnExternalImageLoaded";
        public const string EVT_EXTERNAL_IMAGE_LOAD_FAILED = "OnExternalImageLoadFailed";
        // 以下两个与 ResourceManager façade 同名（同一个字符串 key）。
        public const string EVT_UNLOAD_RESOURCE           = ResourceManager.EVT_UNLOAD_RESOURCE;
        public const string EVT_UNLOAD_ALL_RESOURCES      = ResourceManager.EVT_UNLOAD_ALL_RESOURCES;

        private Dictionary<ResourceKey, UnityEngine.Object> _loadedResources = new Dictionary<ResourceKey, UnityEngine.Object>();
        private bool _dataLoaded = false;

        /// <summary>FBX/Model 路径（不含扩展名、Resources 相对）→ 其内含的 clip 名列表。Editor 启动时 + Build 读 manifest 时填充。</summary>
        private Dictionary<string, List<string>> _modelClipNames = new Dictionary<string, List<string>>(System.StringComparer.OrdinalIgnoreCase);

        /// <summary>FBX manifest 资源路径（Resources 相对，不含扩展名）。</summary>
        private const string FBXManifestResourcePath = "CharacterFBXManifest";

        protected override void Initialize()
        {
            base.Initialize();
            Log("ResourceService 初始化完成", Color.green);
        }

        /// <summary>
        /// 获取已加载的资源（用于调试）
        /// </summary>
        public Dictionary<ResourceKey, UnityEngine.Object> GetLoadedResources()
        {
            return _loadedResources;
        }

        /// <summary>
        /// 当 DataManager 完成数据加载后调用此方法
        /// </summary>
        [Event(EVT_DATA_LOADED)]
        public List<object> OnDataLoaded(List<object> data)
        {
            // R5: 统一走 ResultCode。以前返 ["已加载"] 不是合法 ResultCode，调用方会误认失败；
            // 改返 Ok(true) 表示已加载，Ok() 表示本次刚加载完。
            if (_dataLoaded) return ResultCode.Ok(true);
            _dataLoaded = true;
            LoadAndPreloadResources();
            return ResultCode.Ok();
        }

        /// <summary>
        /// 从存储加载配置并预加载所有资源
        /// </summary>
        private void LoadAndPreloadResources()
        {
            // 预加载配置文件中指定的资源
            PreloadCategory<GameObject>("Prefab");
            PreloadCategory<AudioClip>("AudioClip");
            PreloadCategory<Texture2D>("Texture");

            // Sprite 特殊：外部文件走 LoadExternalImageAsync
            foreach (var key in GetKeys("Sprite"))
            {
                var config = GetData<ResourceConfigItem>("Sprite", key);
                if (config == null) continue;

                if (config.isExternal)
                    LoadExternalImageAsync(config.path, s => { if (s != null) Log($"预加载外部Sprite: {config.path}"); });
                else
                    LoadAsync<Sprite>(config.path, s => { if (s != null) Log($"预加载Sprite: {config.path}"); });
            }

            // 自动加载Resources文件夹下的所有资源
            AutoLoadAllResources();
        }

        /// <summary>
        /// 自动加载Resources文件夹下的所有资源
        /// </summary>
        private void AutoLoadAllResources()
        {
#if UNITY_EDITOR
            // Editor 下用 AssetDatabase 兜底，按【真实文件名】索引而非 m_Name；
            // 这样无论 Unity 的 m_Name 缓存是否落后于文件名（外部改名/移动后未刷新），
            // 都能用文件名为 ID 找到资产。
            UnityEditor.AssetDatabase.Refresh();
            EditorIndexResources<GameObject>();
            EditorIndexResources<Sprite>();
            EditorIndexResources<AudioClip>();
            EditorIndexResources<Texture2D>();
            EditorIndexResources<RuleTile>();
            EditorIndexAnimationClips();
            EditorIndexModelClipNames();
#endif
            // Build / Editor 都会试读一下 manifest（预生成的）；Editor 上面已索引的不会被覆盖。
            LoadFBXManifestIfPresent();

            // Build / 也作为 Editor 第二道兜底：Resources.LoadAll 用 m_Name 作 key
            LoadAllResourcesAsync<GameObject>();
            LoadAllResourcesAsync<Sprite>();
            LoadAllResourcesAsync<AudioClip>();
            LoadAllResourcesAsync<Texture2D>();
            LoadAllResourcesAsync<RuleTile>();
            LoadAllResourcesAsync<AnimationClip>();
            // 同时尝试通过 FBX 容器把内部 AnimationClip 子资产索引出来
            IndexAnimationClipsFromModelContainers();

            // 一次性广播【资源全部加载完成】 —— 用 TriggerEvent（走 _eventListeners 分发，给 [EventListener] 用）
            try
            {
                if (EventProcessor.Instance.HasListener(EVT_RESOURCES_LOADED))
                    EventProcessor.Instance.TriggerEvent(EVT_RESOURCES_LOADED, new List<object>());
            }
            catch (System.Exception ex) { Log($"广播 EVT_RESOURCES_LOADED 异常：{ex.Message}", Color.yellow); }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor 专用：用 AssetDatabase 找出所有 Resources 下的指定类型资产，
        /// 按 <b>真实文件名</b>（不依赖 m_Name）写入缓存。仅在 Editor 编译。
        /// </summary>
        private void EditorIndexResources<T>() where T : UnityEngine.Object
        {
            var typeName = typeof(T).Name;
            var guids = UnityEditor.AssetDatabase.FindAssets($"t:{typeName}");
            Log($"[Editor] AssetDatabase.FindAssets t:{typeName} 命中 {guids?.Length ?? 0} 个 GUID");
            if (guids == null) return;

            var added = 0;
            var inResources = 0;
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                // 仅处理 Resources/ 下的资产
                if (path.IndexOf("/Resources/", System.StringComparison.Ordinal) < 0) continue;
                inResources++;

                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset == null)
                {
                    Log($"[Editor] LoadAssetAtPath 返回 null: {path}", Color.yellow);
                    continue;
                }

                var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                var key = new ResourceKey(fileName, false, NormalizeTypeTag(typeName));
                if (!_loadedResources.ContainsKey(key))
                {
                    _loadedResources[key] = asset;
                    added++;
                    Log($"[Editor] 索引 {typeName}: '{fileName}' ← {path}");
                }
            }
            Log($"[Editor] {typeName} 在 Resources/ 下 {inResources} 个，新增缓存 {added} 条");
        }

        /// <summary>
        /// Editor 专用：扫描所有 Resources/ 下的资产（含 FBX 容器），
        /// 收集其中的 AnimationClip 子资产，按 <b>clip.name</b>（FBX 内 take 名）入缓存。
        /// 对应 ResourceKey.TypeTag = "AnimationClip"，可通过 EVT_GET_RESOURCE 直接取。
        /// </summary>
        private void EditorIndexAnimationClips()
        {
            // 1) 直接的 AnimationClip 文件（.anim）
            var clipGuids = UnityEditor.AssetDatabase.FindAssets("t:AnimationClip");
            // 2) Model 容器（FBX/OBJ/etc.）—— 内含 AnimationClip 子资产
            var modelGuids = UnityEditor.AssetDatabase.FindAssets("t:Model");
            var allGuids = new HashSet<string>();
            if (clipGuids != null)  foreach (var g in clipGuids)  allGuids.Add(g);
            if (modelGuids != null) foreach (var g in modelGuids) allGuids.Add(g);

            int scanned = 0, added = 0;
            foreach (var guid in allGuids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                if (path.IndexOf("/Resources/", System.StringComparison.Ordinal) < 0) continue;
                scanned++;
                var subAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
                if (subAssets == null) continue;
                foreach (var asset in subAssets)
                {
                    if (!(asset is AnimationClip clip)) continue;
                    // 跳过 FBX 自动生成的 __preview__ 占位 clip
                    if (clip.name.StartsWith("__preview__", System.StringComparison.Ordinal)) continue;
                    var key = new ResourceKey(clip.name, false, "AnimationClip");
                    if (!_loadedResources.ContainsKey(key))
                    {
                        _loadedResources[key] = clip;
                        added++;
                    }
                }
            }
            Log($"[Editor] AnimationClip 扫描容器 {scanned} 个，新增缓存 {added} 条");
        }
#endif

        /// <summary>
        /// Build / 通用：通过 <see cref="Resources.LoadAll"/> 全量扫描 GameObject（FBX 根），
        /// 用 <see cref="UnityEngine.Object.GetComponentsInChildren"/> 探不到 clip 子资产，
        /// 因此这里直接信赖 <see cref="LoadAllResourcesAsync{T}"/>(AnimationClip) 的成果 ——
        /// 该 API 已经把 Resources/ 下所有 AnimationClip（包括 FBX 子资产）按 m_Name 入缓存。
        /// 这里仅做一次"按 clip.name 重写 ResourceKey 以确保 TypeTag = AnimationClip"的兜底，
        /// 避免与 GameObject TypeTag 混淆。
        /// </summary>
        private void IndexAnimationClipsFromModelContainers()
        {
            // Resources.LoadAll<AnimationClip>("") 会递归 Resources/ 内所有匹配资产（含 FBX 子资产）
            var clips = Resources.LoadAll<AnimationClip>("");
            if (clips == null) return;
            int added = 0;
            foreach (var clip in clips)
            {
                if (clip == null) continue;
                if (clip.name.StartsWith("__preview__", System.StringComparison.Ordinal)) continue;
                var key = new ResourceKey(clip.name, false, "AnimationClip");
                if (!_loadedResources.ContainsKey(key))
                {
                    _loadedResources[key] = clip;
                    added++;
                }
            }
            if (added > 0) Log($"AnimationClip 全量索引（含 FBX 子资产）+{added}");
        }

        /// <summary>
        /// 异步加载指定类型的所有资源
        /// </summary>
        private void LoadAllResourcesAsync<T>() where T : UnityEngine.Object
        {
            // 先同步获取资源列表
            var resources = Resources.LoadAll<T>("");
            if (resources != null)
            {
                foreach (var resource in resources)
                {
                    if (resource != null)
                    {
                        // 使用资源名称作为路径的一部分
                        var resourceKey = new ResourceKey($"Resources/{resource.name}", false, NormalizeTypeTag(typeof(T).Name));
                        if (!_loadedResources.ContainsKey(resourceKey))
                        {
                            _loadedResources[resourceKey] = resource;
                            Log($"自动加载资源: {resource.name} ({typeof(T).Name})");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 通用预加载 helper — 按类别 key 异步加载 Unity 资源
        /// </summary>
        private void PreloadCategory<T>(string category) where T : UnityEngine.Object
        {
            foreach (var key in GetKeys(category))
            {
                var config = GetData<ResourceConfigItem>(category, key);
                if (config == null) continue;
                LoadAsync<T>(config.path, asset => { if (asset != null) Log($"预加载{category}: {config.path}"); });
            }
        }

        /// <summary>
        /// 获取已加载的资源（同步）。
        /// <para>
        /// 缓存命中直接返回；cache miss 时对非外部资源 fallback 到 <see cref="Resources.Load{T}(string)"/>
        /// 同步加载（按 path 走 Resources/ 实际目录），命中后写回缓存。
        /// 这样：
        /// <list type="bullet">
        /// <item>预加载未跑完时调用方仍能拿到资源</item>
        /// <item><c>Resources.LoadAll</c> 用 m_Name 当 key 而 m_Name 落后于文件名时也能找到</item>
        /// <item>调用方传 <c>"Tiles/GrasslandsGround"</c> 这种带子目录的路径即可</item>
        /// </list>
        /// </para>
        /// </summary>
        [Event(EVT_GET_RESOURCE)]
        public List<object> Get(List<object> data)
        {
            string path = data[0] as string;
            string typeStr = data[1] as string;
            bool isExternal = data.Count > 2 ? (bool)data[2] : false;

            var key = new ResourceKey(path, isExternal, NormalizeTypeTag(typeStr));

            // R4: TryGetValue 一次查找代替 ContainsKey + indexer 双查找。
            if (_loadedResources.TryGetValue(key, out var cached))
            {
                return ResultCode.Ok(cached);
            }

            // Fallback：非外部资源，按 path 直接 Resources.Load（同步）。
            // 调用方按"文件名为 ID"约定传入裸文件名时，会依次尝试若干常见子目录，
            // 命中即缓存（用同一 ResourceKey，命中后续再调用就走缓存）。
            if (!isExternal && !string.IsNullOrEmpty(path))
            {
                foreach (var candidate in EnumerateLoadCandidates(path))
                {
                    UnityEngine.Object loaded = typeStr switch
                    {
                        "Prefab"        => Resources.Load<GameObject>(candidate),
                        "Sprite"        => Resources.Load<Sprite>(candidate),
                        "AudioClip"     => Resources.Load<AudioClip>(candidate),
                        "Texture"       => Resources.Load<Texture2D>(candidate),
                        "RuleTile"      => Resources.Load<RuleTile>(candidate),
                        "AnimationClip" => Resources.Load<AnimationClip>(candidate),
                        _               => null,
                    };
                    if (loaded != null)
                    {
                        _loadedResources[key] = loaded;
                        Log($"Fallback 同步加载: {candidate} ({typeStr})");
                        return ResultCode.Ok(loaded);   // R5
                    }
                }
            }

            return ResultCode.Fail("资源未加载");   // R5
        }

        /// <summary>
        /// 已知的 Resources 子目录候选前缀（按命中概率排序），用于 fallback 时
        /// 把"文件名为 ID"映射到实际子目录文件。
        /// </summary>
        private static readonly string[] _resourceSubfolderHints =
        {
            "",                 // 根目录直接命中
            "Tiles",
            "Sprites",
            "Sprites/Tiles",
            "Sprites/UI",
            "Sprites/Characters",
            "Prefabs",
            "Audio",
            "Models",
            "Models/Characters3D",
        };

        /// <summary>
        /// 归一化资源类型标签 —— façade 字符串（"Prefab"/"Texture"）与
        /// <c>typeof(T).Name</c>（"GameObject"/"Texture2D"）互通，保证 ResourceKey.TypeTag 一致。
        /// </summary>
        private static string NormalizeTypeTag(string typeStrOrTypeName)
        {
            if (string.IsNullOrEmpty(typeStrOrTypeName)) return "";
            return typeStrOrTypeName switch
            {
                "Prefab"     => "Prefab",
                "GameObject" => "Prefab",
                "Texture"    => "Texture",
                "Texture2D"  => "Texture",
                _            => typeStrOrTypeName,   // "Sprite" / "AudioClip" / "RuleTile" / ... 原样
            };
        }

        /// <summary>枚举给定 path 在常见子目录下的 Resources.Load 候选路径。</summary>
        private static IEnumerable<string> EnumerateLoadCandidates(string path)
        {
            // 调用方已传 "Tiles/X" 这种带子目录路径时，原样优先尝试一次
            yield return path;

            // 已经含 '/'，说明调用方自带子目录，不再二次拼接，避免变成 "Tiles/Tiles/X"
            if (path.Contains('/') || path.Contains('\\')) yield break;

            foreach (var hint in _resourceSubfolderHints)
            {
                if (string.IsNullOrEmpty(hint)) continue;   // "" 等同于原 path，已 yield 过
                yield return $"{hint}/{path}";
            }
        }

        /// <summary>
        /// 通过ResourceKey获取资源ID
        /// </summary>
        public string GetResourceId(ResourceKey key)
        {
            // ResourceKey已经存储了文件名（不带扩展名），直接返回
            return key.FileName;
        }

        /// <summary>
        /// 添加预加载配置
        /// </summary>
        [Event(EVT_ADD_RESOURCE_CONFIG)]
        public List<object> AddPreloadConfig(List<object> data)
        {
            string id = data[0] as string;
            string path = data[1] as string;
            ResourceType type = (ResourceType)data[2];
            bool isExternal = data.Count > 3 ? (bool)data[3] : false;

            // 如果未提供ID，自动从路径中提取文件名（不带扩展名）
            if (string.IsNullOrEmpty(id))
            {
                // 仅用于提取 FileName，不需 TypeTag
                var key = new ResourceKey(path, isExternal);
                id = key.FileName;
            }

            string category = type.ToString();
            var config = new ResourceConfigItem { id = id, path = path, isExternal = isExternal, type = type };
            SetData(category, id, config);

            return ResultCode.Ok();   // R5
        }

        /// <summary>
        /// 异步加载 Unity 内部资源
        /// </summary>
        [Event(EVT_LOAD_RESOURCE_ASYNC)]
        public List<object> LoadAsync(List<object> data)
        {
            string path = data[0] as string;
            string typeStr = data[1] as string;
            bool isExternal = data.Count > 2 ? (bool)data[2] : false;

            if (isExternal) return ResultCode.Fail("外部资源请使用 LoadExternalImageAsync");   // R5

            var key = new ResourceKey(path, false, NormalizeTypeTag(typeStr));

            // R4: TryGetValue 一次查。
            if (_loadedResources.TryGetValue(key, out var cached)) return ResultCode.Ok(cached);   // R5

            // 根据类型加载
            switch (typeStr)
            {
                case "Prefab":        LoadAsync<GameObject>(path, _ => { }); break;
                case "Sprite":        LoadAsync<Sprite>(path, _ => { }); break;
                case "AudioClip":     LoadAsync<AudioClip>(path, _ => { }); break;
                case "Texture":       LoadAsync<Texture2D>(path, _ => { }); break;
                case "RuleTile":      LoadAsync<RuleTile>(path, _ => { }); break;
                case "AnimationClip": LoadAsync<AnimationClip>(path, _ => { }); break;
                default: return ResultCode.Fail("不支持的资源类型");   // R5
            }

            // 异步加载，返回“加载中”的 sentinel。调用方 IsOk 为 false，result[1] 为 提示文本。
            return ResultCode.Fail("加载中");   // R5：严格说不是失败，但走 Fail 格式调用方能一致处理
        }

        /// <summary>
        /// 取出 FBX/Model 容器内含的所有 AnimationClip。
        /// 缓存命中走 O(1) 反查；Editor 兜底用 <c>AssetDatabase.LoadAllAssetsAtPath</c>。
        /// </summary>
        [Event(EVT_GET_MODEL_CLIPS)]
        public List<object> GetModelClips(List<object> data)
        {
            string modelPath = data != null && data.Count > 0 ? data[0] as string : null;
            var result = new List<AnimationClip>();
            if (string.IsNullOrEmpty(modelPath)) return ResultCode.Ok(result);

            var key = NormalizeModelKey(modelPath);

            // 1) 命中 _modelClipNames（Editor 启动 + Build manifest 都填充） → 反查全局 AnimationClip 缓存
            if (_modelClipNames.TryGetValue(key, out var clipNames) && clipNames != null)
            {
                foreach (var name in clipNames)
                {
                    var clip = TryGetCachedClip(name);
                    if (clip != null) result.Add(clip);
                }
                if (result.Count > 0) return ResultCode.Ok(result);
            }

#if UNITY_EDITOR
            // 2) Editor fallback：直接 AssetDatabase 解析（用户运行时改了 FBX 后未重启的容错）
            var clips = LoadAllClipsAtModelPath(modelPath);
            if (clips != null) result.AddRange(clips);
            if (result.Count == 0)
            {
                var fileName = System.IO.Path.GetFileNameWithoutExtension(modelPath);
                var guids = UnityEditor.AssetDatabase.FindAssets($"{fileName} t:Model");
                if (guids != null)
                {
                    foreach (var g in guids)
                    {
                        var p = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                        if (string.IsNullOrEmpty(p)) continue;
                        if (System.IO.Path.GetFileNameWithoutExtension(p) != fileName) continue;
                        var more = LoadAllClipsAtModelPath(p);
                        if (more != null) result.AddRange(more);
                    }
                }
            }
#endif
            return ResultCode.Ok(result);
        }

        /// <summary>枚举已索引的所有 FBX/Model 路径。</summary>
        [Event(EVT_GET_ALL_MODEL_PATHS)]
        public List<object> GetAllModelPaths(List<object> data)
        {
            var list = new List<string>(_modelClipNames.Count);
            foreach (var kv in _modelClipNames) list.Add(kv.Key);
            return ResultCode.Ok(list);
        }

        /// <summary>把 modelPath 归一化为缓存 key（去扩展名 + Assets/Resources/ 前缀去除 + 反斜杠转正斜杠）。</summary>
        private static string NormalizeModelKey(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            var p = path.Replace('\\', '/');
            const string prefix = "Assets/Resources/";
            if (p.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                p = p.Substring(prefix.Length);
            var ext = System.IO.Path.GetExtension(p);
            if (!string.IsNullOrEmpty(ext)) p = p.Substring(0, p.Length - ext.Length);
            return p;
        }

        private AnimationClip TryGetCachedClip(string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return null;
            var key = new ResourceKey(clipName, false, "AnimationClip");
            if (_loadedResources.TryGetValue(key, out var obj) && obj is AnimationClip c) return c;
            return null;
        }

#if UNITY_EDITOR
        /// <summary>Editor 启动时遍历 Resources/ 下所有 FBX/Model，按归一化 key 缓存其内含 clip 名列表。</summary>
        private void EditorIndexModelClipNames()
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("t:Model");
            if (guids == null) return;
            int indexed = 0;
            foreach (var guid in guids)
            {
                var assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath)) continue;
                if (assetPath.IndexOf("/Resources/", System.StringComparison.Ordinal) < 0) continue;

                var key = NormalizeModelKey(assetPath);
                if (_modelClipNames.ContainsKey(key)) continue;

                var clips = LoadAllClipsAtModelPath(assetPath);
                if (clips == null || clips.Count == 0)
                {
                    _modelClipNames[key] = new List<string>();   // 仍登记 key，用于 EVT_GET_ALL_MODEL_PATHS（带空 clip 列表的会被 Factory 跳过）
                    continue;
                }
                var names = new List<string>(clips.Count);
                foreach (var c in clips) if (c != null) names.Add(c.name);
                _modelClipNames[key] = names;
                indexed++;
            }
            Log($"[Editor] 模型 clip 名单缓存：登记 {_modelClipNames.Count} 条（{indexed} 个含 clip）");
        }

        private static List<AnimationClip> LoadAllClipsAtModelPath(string path)
        {
            var list = new List<AnimationClip>();
            if (string.IsNullOrEmpty(path)) return list;
            var candidates = new List<string> { path };
            if (!path.StartsWith("Assets/", System.StringComparison.Ordinal))
            {
                foreach (var ext in new[] { ".fbx", ".FBX", ".obj", ".OBJ", ".dae", ".blend" })
                {
                    candidates.Add($"Assets/Resources/{path}{ext}");
                }
            }
            foreach (var c in candidates)
            {
                var subAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(c);
                if (subAssets == null || subAssets.Length == 0) continue;
                foreach (var a in subAssets)
                {
                    if (a is AnimationClip clip
                        && !clip.name.StartsWith("__preview__", System.StringComparison.Ordinal))
                        list.Add(clip);
                }
                if (list.Count > 0) break;
            }
            return list;
        }
#endif

        /// <summary>
        /// 读取 <c>Resources/CharacterFBXManifest.json</c>（由 Editor 工具 / Build 预处理生成），
        /// 把其中记录的 modelPath → clip 名列表填进 <see cref="_modelClipNames"/>。
        /// 已存在的 key 不覆盖（Editor 索引优先于 manifest）。
        /// </summary>
        private void LoadFBXManifestIfPresent()
        {
            var asset = Resources.Load<TextAsset>(FBXManifestResourcePath);
            if (asset == null) return;

            try
            {
                // 简单 JSON：{ "entries": [ { "path": "...", "clips": ["..","..."] } ] }
                var wrapper = JsonUtility.FromJson<FBXManifest>(asset.text);
                if (wrapper?.entries == null) return;
                int added = 0;
                foreach (var e in wrapper.entries)
                {
                    if (e == null || string.IsNullOrEmpty(e.path)) continue;
                    var key = NormalizeModelKey(e.path);
                    if (_modelClipNames.ContainsKey(key)) continue;
                    _modelClipNames[key] = e.clips != null ? new List<string>(e.clips) : new List<string>();
                    added++;
                }
                Log($"FBX manifest 加载：+{added} 条记录（总 {_modelClipNames.Count}）");
            }
            catch (System.Exception ex)
            {
                Log($"FBX manifest 解析失败：{ex.Message}", Color.yellow);
            }
        }

        [System.Serializable]
        private class FBXManifest
        {
            public FBXManifestEntry[] entries;
        }

        [System.Serializable]
        private class FBXManifestEntry
        {
            public string path;     // Resources 相对路径，无扩展名
            public string[] clips;  // 该 FBX 内 clip 名列表
        }

        /// <summary>
        /// 内部异步加载方法
        /// </summary>
        private void LoadAsync<T>(string path, System.Action<T> callback) where T : UnityEngine.Object
        {
            var key = new ResourceKey(path, false, NormalizeTypeTag(typeof(T).Name));

            // R4: TryGetValue 一次查。
            if (_loadedResources.TryGetValue(key, out var cached))
            {
                callback?.Invoke(cached as T);
                return;
            }

            ResourceRequest request = Resources.LoadAsync<T>(path);
            request.completed += (operation) =>
            {
                T resource = request.asset as T;
                if (resource != null)
                {
                    _loadedResources[key] = resource;
                }
                callback?.Invoke(resource);
            };
        }

        /// <summary>
        /// 异步加载外部文件图片
        /// </summary>
        [Event(EVT_LOAD_EXTERNAL_IMAGE_ASYNC)]
        public List<object> LoadExternalImageAsync(List<object> data)
        {
            string filePath = data[0] as string;
            var key = new ResourceKey(filePath, true, "Sprite");

            // R4: TryGetValue 一次查。
            if (_loadedResources.TryGetValue(key, out var cached)) return ResultCode.Ok(cached);   // R5

            if (!System.IO.File.Exists(filePath)) return ResultCode.Fail("文件不存在");   // R5

            LoadExternalImageAsync(filePath, null);
            return ResultCode.Fail("加载中");   // R5：同上 sentinel
        }

        /// <summary>
        /// 内部异步加载外部文件图片（带回调）
        /// </summary>
        private void LoadExternalImageAsync(string filePath, System.Action<Sprite> callback)
        {
            var key = new ResourceKey(filePath, true, "Sprite");

            // R4: TryGetValue 一次查。
            if (_loadedResources.TryGetValue(key, out var cached))
            {
                callback?.Invoke(cached as Sprite);
                return;
            }

            if (!System.IO.File.Exists(filePath))
            {
                callback?.Invoke(null);
                return;
            }

            System.Threading.Tasks.Task.Run(() =>
            {
                byte[] bytes = System.IO.File.ReadAllBytes(filePath);

                MainThreadDispatcher.Enqueue(() =>
                {
                    Texture2D texture = new Texture2D(2, 2);
                    if (texture.LoadImage(bytes))
                    {
                        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
                        _loadedResources[key] = sprite;
                        Log($"外部 Sprite 加载成功: {filePath}");

                        callback?.Invoke(sprite);

                        // 触发加载完成事件
                        EventProcessor.Instance.TriggerEventMethod(EVT_EXTERNAL_IMAGE_LOADED,
                            new List<object> { new Dictionary<string, object> { ["path"] = filePath, ["sprite"] = sprite } });
                    }
                    else
                    {
                        UnityEngine.Object.Destroy(texture);

                        callback?.Invoke(null);

                        // 触发加载失败事件
                        EventProcessor.Instance.TriggerEventMethod(EVT_EXTERNAL_IMAGE_LOAD_FAILED,
                            new List<object> { new Dictionary<string, object> { ["path"] = filePath, ["error"] = "加载失败" } });
                    }
                });
            });
        }

        /// <summary>
        /// 卸载资源
        /// </summary>
        [Event(EVT_UNLOAD_RESOURCE)]
        public List<object> Unload(List<object> data)
        {
            string path = data[0] as string;
            bool isExternal = data.Count > 1 ? (bool)data[1] : false;
            // 可选第 3 参数指定类型（"Sprite"/"RuleTile"/...），不传则按 FileName 全清
            string typeStr = data.Count > 2 ? data[2] as string : null;

            // 提取目标 FileName
            var probe = new ResourceKey(path, isExternal);
            var targetName = probe.FileName;

            // 收集匹配键（避免边遍历边删）
            var toRemove = new List<ResourceKey>();
            foreach (var k in _loadedResources.Keys)
            {
                if (k.FileName != targetName || k.IsExternal != isExternal) continue;
                if (!string.IsNullOrEmpty(typeStr) && k.TypeTag != NormalizeTypeTag(typeStr)) continue;
                toRemove.Add(k);
            }

            if (toRemove.Count == 0) return ResultCode.Fail("资源未加载");   // R5

            foreach (var k in toRemove)
            {
                Resources.UnloadAsset(_loadedResources[k]);
                _loadedResources.Remove(k);
            }
            return ResultCode.Ok();   // R5
        }

        /// <summary>
        /// 卸载所有资源
        /// </summary>
        [Event(EVT_UNLOAD_ALL_RESOURCES)]
        public List<object> UnloadAll(List<object> data)
        {
            foreach (var resource in _loadedResources.Values)
            {
                Resources.UnloadAsset(resource);
            }
            _loadedResources.Clear();
            return ResultCode.Ok();   // R5
        }

        /// <summary>
        /// 更新 Inspector 信息
        /// </summary>
        public override void UpdateInspectorInfo()
        {
            base.UpdateInspectorInfo();

            // 添加已加载资源信息
            if (InspectorInfo == null)
            {
                InspectorInfo = new ServiceDataInspectorInfo();
            }

            var resourceCategory = new ServiceDataInspectorInfo.CategoryInfo
            {
                CategoryName = "已加载资源",
                DataCount = _loadedResources.Count,
                DataItems = new List<ServiceDataInspectorInfo.DataInfo>()
            };

            foreach (var kvp in _loadedResources)
            {
                var resourceKey = kvp.Key;
                var resource = kvp.Value;
                var dataInfo = new ServiceDataInspectorInfo.DataInfo
                {
                    Key = resourceKey.ToString(),
                    TypeName = resource?.GetType().Name ?? "null",
                    ValueSummary = resource?.name ?? "null"
                };
                resourceCategory.DataItems.Add(dataInfo);
            }

            InspectorInfo.Categories.Add(resourceCategory);
        }
    }
}
