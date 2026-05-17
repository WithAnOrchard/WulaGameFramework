using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Tilemaps;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;

namespace EssSystem.Core.Foundation.ResourceManager
{
    /// <summary>资源缓存键（FileName + IsExternal + TypeTag），避免同名不同类型碰撞。</summary>
    public struct ResourceKey : IEquatable<ResourceKey>
    {
        public readonly string FileName;
        public readonly bool   IsExternal;
        public readonly string TypeTag;

        public ResourceKey(string path, bool isExternal, string typeTag = null)
        {
            var name = string.IsNullOrEmpty(path) ? path : Path.GetFileNameWithoutExtension(path);
            FileName   = string.IsNullOrEmpty(name) ? path : name;
            IsExternal = isExternal;
            TypeTag    = typeTag ?? "";
        }

        public bool Equals(ResourceKey other) =>
            FileName == other.FileName && IsExternal == other.IsExternal && TypeTag == other.TypeTag;

        public override bool Equals(object obj) => obj is ResourceKey k && Equals(k);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + (FileName?.GetHashCode() ?? 0);
                h = h * 31 + IsExternal.GetHashCode();
                h = h * 31 + (TypeTag?.GetHashCode() ?? 0);
                return h;
            }
        }

        public override string ToString() =>
            string.IsNullOrEmpty(TypeTag)
                ? $"{(IsExternal ? "external" : "unity")}:{FileName}"
                : $"{(IsExternal ? "external" : "unity")}:{TypeTag}:{FileName}";
    }

    public enum ResourceType { Prefab, Sprite, AudioClip, Texture, RuleTile, AnimationClip }

    [Serializable]
    public class ResourceConfigItem
    {
        public string id;
        public string path;
        public bool   isExternal;
        public ResourceType type;
    }

    /// <summary>
    /// 资源服务 —— 配置预加载 + Resources/ 全量索引 + 同步 Get / 异步 Load / 外部图片 / 卸载。
    /// </summary>
    public class ResourceService : Service<ResourceService>
    {
        // ============================================================
        // Event 常量
        // ============================================================
        public const string EVT_DATA_LOADED                = "OnResourceDataLoaded";
        public const string EVT_GET_RESOURCE               = "GetResource";
        public const string EVT_ADD_RESOURCE_CONFIG        = "AddResourceConfig";
        public const string EVT_LOAD_RESOURCE_ASYNC        = "LoadResourceAsync";
        public const string EVT_LOAD_EXTERNAL_IMAGE_ASYNC  = "LoadExternalImageAsync";
        public const string EVT_GET_MODEL_CLIPS            = "GetModelClips";
        public const string EVT_GET_ALL_MODEL_PATHS        = "GetAllModelPaths";
        public const string EVT_RESOURCES_LOADED           = "OnResourcesLoaded";
        public const string EVT_EXTERNAL_IMAGE_LOADED      = "OnExternalImageLoaded";
        public const string EVT_EXTERNAL_IMAGE_LOAD_FAILED = "OnExternalImageLoadFailed";

        // ============================================================
        // 类型分发表
        // ============================================================
        private static readonly Dictionary<string, Type> _typeMap = new Dictionary<string, Type>
        {
            { "Prefab",        typeof(GameObject) },
            { "Sprite",        typeof(Sprite) },
            { "AudioClip",     typeof(AudioClip) },
            { "Texture",       typeof(Texture2D) },
            { "Material",      typeof(Material) },
            { "RuleTile",      typeof(RuleTile) },
            { "AnimationClip", typeof(AnimationClip) },
        };

        // 批量索引顺序（预加载 + Resources.LoadAll 都按此跑一遍）
        private static readonly (string tag, Type type)[] _bulkTypes =
        {
            ("Prefab",        typeof(GameObject)),
            ("Sprite",        typeof(Sprite)),
            ("AudioClip",     typeof(AudioClip)),
            ("Texture",       typeof(Texture2D)),
            ("Material",      typeof(Material)),
            ("RuleTile",      typeof(RuleTile)),
            ("AnimationClip", typeof(AnimationClip)),
        };

        // 调用方传裸文件名时，依次尝试的 Resources/ 子目录
        private static readonly string[] _subfolderHints =
        {
            "", "Tiles", "Sprites", "Sprites/Tiles", "Sprites/UI", "Sprites/Characters",
            "Prefabs", "Audio", "Sound", "Models", "Models/Characters3D",
        };

        private const string FBXManifestResourcePath = "CharacterFBXManifest";

        // ============================================================
        // 状态
        // ============================================================
        private readonly Dictionary<ResourceKey, UnityEngine.Object> _loadedResources
            = new Dictionary<ResourceKey, UnityEngine.Object>();

        /// <summary>FBX/Model 路径（Resources 相对、不含扩展名）→ 内含 clip 名列表。</summary>
        private readonly Dictionary<string, List<string>> _modelClipNames
            = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        private bool _dataLoaded;

        // ============================================================
        // 公开访问
        // ============================================================
        protected override void Initialize()
        {
            base.Initialize();
            Log("ResourceService 初始化完成", Color.green);
        }

        public Dictionary<ResourceKey, UnityEngine.Object> GetLoadedResources() => _loadedResources;
        public string GetResourceId(ResourceKey key) => key.FileName;

        // ============================================================
        // 数据加载完成 → 预加载 + 全量索引 + 广播
        // ============================================================
        [Event(EVT_DATA_LOADED)]
        public List<object> OnDataLoaded(List<object> data)
        {
            if (_dataLoaded) return ResultCode.Ok(true);
            _dataLoaded = true;

            PreloadConfiguredResources();
            IndexAllResources();

            try
            {
                if (EventProcessor.Instance.HasListener(EVT_RESOURCES_LOADED))
                    EventProcessor.Instance.TriggerEvent(EVT_RESOURCES_LOADED, new List<object>());
            }
            catch (Exception ex) { Log($"广播 EVT_RESOURCES_LOADED 异常：{ex.Message}", Color.yellow); }

            return ResultCode.Ok();
        }

        /// <summary>按 DataService 中的 ResourceConfigItem 配置异步预加载。Sprite 外部走文件读取。</summary>
        private void PreloadConfiguredResources()
        {
            foreach (var (tag, type) in _bulkTypes)
            {
                foreach (var key in GetKeys(tag))
                {
                    var config = GetData<ResourceConfigItem>(tag, key);
                    if (config == null) continue;
                    if (tag == "Sprite" && config.isExternal) LoadExternalImageAsync(config.path, null);
                    else                                       StartAsyncLoad(config.path, type);
                }
            }
        }

        /// <summary>把 Resources/ 下所有支持类型的资产入缓存。Editor 路径按文件名索引（容忍 m_Name 落后）；Build 路径用 Resources.LoadAll 兜底。</summary>
        private void IndexAllResources()
        {
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
            foreach (var (tag, type) in _bulkTypes) EditorIndexByFileName(type, tag);
            EditorIndexModelClipNames();
#endif
            LoadFBXManifestIfPresent();
            foreach (var (tag, type) in _bulkTypes) ResourcesLoadAllInto(type, tag);
        }

        // ============================================================
        // 同步取资源（缓存 / 子目录 fallback / Sprite 子图兜底）
        // ============================================================
        [Event(EVT_GET_RESOURCE)]
        public List<object> Get(List<object> data)
        {
            string path       = data[0] as string;
            string typeStr    = data[1] as string;
            bool   isExternal = data.Count > 2 && (bool)data[2];

            var key = new ResourceKey(path, isExternal, NormalizeTypeTag(typeStr));
            if (_loadedResources.TryGetValue(key, out var cached)) return ResultCode.Ok(cached);

            if (!isExternal && !string.IsNullOrEmpty(path) && _typeMap.TryGetValue(typeStr, out var type))
            {
                foreach (var candidate in EnumerateLoadCandidates(path))
                {
                    var loaded = Resources.Load(candidate, type);
                    if (loaded != null)
                    {
                        _loadedResources[key] = loaded;
                        Log($"Fallback 同步加载: {candidate} ({typeStr})");
                        return ResultCode.Ok(loaded);
                    }
                }

                // Sprite 还可按子图名兜底
                if (typeStr == "Sprite")
                {
                    var sprite = LoadSpriteByName(path);
                    if (sprite != null)
                    {
                        _loadedResources[key] = sprite;
                        Log($"Fallback 同步加载 Sprite 子图: {sprite.name}");
                        return ResultCode.Ok(sprite);
                    }
                }
            }

            return ResultCode.Fail("资源未加载");
        }

        // ============================================================
        // 异步加载（命中缓存直接返；否则触发并返回"加载中"sentinel）
        // ============================================================
        [Event(EVT_LOAD_RESOURCE_ASYNC)]
        public List<object> LoadAsync(List<object> data)
        {
            string path       = data[0] as string;
            string typeStr    = data[1] as string;
            bool   isExternal = data.Count > 2 && (bool)data[2];

            if (isExternal) return ResultCode.Fail("外部资源请使用 LoadExternalImageAsync");
            if (!_typeMap.TryGetValue(typeStr, out var type)) return ResultCode.Fail("不支持的资源类型");

            var key = new ResourceKey(path, false, NormalizeTypeTag(typeStr));
            if (_loadedResources.TryGetValue(key, out var cached)) return ResultCode.Ok(cached);

            StartAsyncLoad(path, type);
            return ResultCode.Fail("加载中");
        }

        /// <summary>触发 Resources.LoadAsync 并在完成时入缓存。命中缓存则不重复触发。</summary>
        private void StartAsyncLoad(string path, Type type)
        {
            var key = new ResourceKey(path, false, NormalizeTypeTag(type.Name));
            if (_loadedResources.ContainsKey(key)) return;

            var req = Resources.LoadAsync(path, type);
            req.completed += _ =>
            {
                if (req.asset != null) _loadedResources[key] = req.asset;
            };
        }

        // ============================================================
        // 添加预加载配置
        // ============================================================
        [Event(EVT_ADD_RESOURCE_CONFIG)]
        public List<object> AddPreloadConfig(List<object> data)
        {
            string       id         = data[0] as string;
            string       path       = data[1] as string;
            ResourceType type       = (ResourceType)data[2];
            bool         isExternal = data.Count > 3 && (bool)data[3];

            if (string.IsNullOrEmpty(id)) id = new ResourceKey(path, isExternal).FileName;

            SetData(type.ToString(), id,
                new ResourceConfigItem { id = id, path = path, isExternal = isExternal, type = type });
            return ResultCode.Ok();
        }

        // ============================================================
        // 外部图片加载（异步、走 MainThreadDispatcher）
        // ============================================================
        [Event(EVT_LOAD_EXTERNAL_IMAGE_ASYNC)]
        public List<object> LoadExternalImageAsync(List<object> data)
        {
            string filePath = data[0] as string;
            var key = new ResourceKey(filePath, true, "Sprite");

            if (_loadedResources.TryGetValue(key, out var cached)) return ResultCode.Ok(cached);
            if (!File.Exists(filePath))                            return ResultCode.Fail("文件不存在");

            LoadExternalImageAsync(filePath, null);
            return ResultCode.Fail("加载中");
        }

        private void LoadExternalImageAsync(string filePath, Action<Sprite> callback)
        {
            var key = new ResourceKey(filePath, true, "Sprite");
            if (_loadedResources.TryGetValue(key, out var cached)) { callback?.Invoke(cached as Sprite); return; }
            if (!File.Exists(filePath))                            { callback?.Invoke(null);             return; }

            System.Threading.Tasks.Task.Run(() =>
            {
                byte[] bytes = File.ReadAllBytes(filePath);
                MainThreadDispatcher.Enqueue(() =>
                {
                    var tex = new Texture2D(2, 2);
                    if (tex.LoadImage(bytes))
                    {
                        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero);
                        _loadedResources[key] = sprite;
                        Log($"外部 Sprite 加载成功: {filePath}");
                        callback?.Invoke(sprite);
                        EventProcessor.Instance.TriggerEventMethod(EVT_EXTERNAL_IMAGE_LOADED,
                            new List<object> { new Dictionary<string, object> { ["path"] = filePath, ["sprite"] = sprite } });
                    }
                    else
                    {
                        UnityEngine.Object.Destroy(tex);
                        callback?.Invoke(null);
                        EventProcessor.Instance.TriggerEventMethod(EVT_EXTERNAL_IMAGE_LOAD_FAILED,
                            new List<object> { new Dictionary<string, object> { ["path"] = filePath, ["error"] = "加载失败" } });
                    }
                });
            });
        }

        // ============================================================
        // 卸载
        // ============================================================
        [Event(ResourceManager.EVT_UNLOAD_RESOURCE)]
        public List<object> Unload(List<object> data)
        {
            string path       = data[0] as string;
            bool   isExternal = data.Count > 1 && (bool)data[1];
            string typeStr    = data.Count > 2 ? data[2] as string : null;
            string targetTag  = string.IsNullOrEmpty(typeStr) ? null : NormalizeTypeTag(typeStr);
            string targetName = new ResourceKey(path, isExternal).FileName;

            var toRemove = new List<ResourceKey>();
            foreach (var k in _loadedResources.Keys)
            {
                if (k.FileName != targetName || k.IsExternal != isExternal) continue;
                if (targetTag != null && k.TypeTag != targetTag)            continue;
                toRemove.Add(k);
            }
            if (toRemove.Count == 0) return ResultCode.Fail("资源未加载");

            foreach (var k in toRemove)
            {
                Resources.UnloadAsset(_loadedResources[k]);
                _loadedResources.Remove(k);
            }
            return ResultCode.Ok();
        }

        [Event(ResourceManager.EVT_UNLOAD_ALL_RESOURCES)]
        public List<object> UnloadAll(List<object> data)
        {
            foreach (var resource in _loadedResources.Values) Resources.UnloadAsset(resource);
            _loadedResources.Clear();
            return ResultCode.Ok();
        }

        // ============================================================
        // Model clips（FBX 容器内 AnimationClip 查询）
        // ============================================================
        [Event(EVT_GET_MODEL_CLIPS)]
        public List<object> GetModelClips(List<object> data)
        {
            string modelPath = data != null && data.Count > 0 ? data[0] as string : null;
            var result = new List<AnimationClip>();
            if (string.IsNullOrEmpty(modelPath)) return ResultCode.Ok(result);

            // 1) manifest / Editor 预烘命中 → 反查 AnimationClip 缓存
            if (_modelClipNames.TryGetValue(NormalizeModelKey(modelPath), out var names) && names != null)
            {
                foreach (var name in names)
                {
                    if (string.IsNullOrEmpty(name)) continue;
                    if (_loadedResources.TryGetValue(new ResourceKey(name, false, "AnimationClip"), out var o)
                        && o is AnimationClip clip)
                        result.Add(clip);
                }
                if (result.Count > 0) return ResultCode.Ok(result);
            }

#if UNITY_EDITOR
            // 2) Editor fallback：AssetDatabase 直读
            var direct = LoadAllClipsAtModelPath(modelPath);
            if (direct != null) result.AddRange(direct);
            if (result.Count == 0)
            {
                var fileName = Path.GetFileNameWithoutExtension(modelPath);
                var guids = UnityEditor.AssetDatabase.FindAssets($"{fileName} t:Model");
                if (guids != null)
                {
                    foreach (var g in guids)
                    {
                        var p = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                        if (string.IsNullOrEmpty(p) || Path.GetFileNameWithoutExtension(p) != fileName) continue;
                        var more = LoadAllClipsAtModelPath(p);
                        if (more != null) result.AddRange(more);
                    }
                }
            }
#endif
            return ResultCode.Ok(result);
        }

        [Event(EVT_GET_ALL_MODEL_PATHS)]
        public List<object> GetAllModelPaths(List<object> data)
        {
            var list = new List<string>(_modelClipNames.Count);
            foreach (var k in _modelClipNames.Keys) list.Add(k);
            return ResultCode.Ok(list);
        }

        // ============================================================
        // Inspector
        // ============================================================
        public override void UpdateInspectorInfo()
        {
            base.UpdateInspectorInfo();
            if (InspectorInfo == null) InspectorInfo = new ServiceDataInspectorInfo();

            var cat = new ServiceDataInspectorInfo.CategoryInfo
            {
                CategoryName = "已加载资源",
                DataCount    = _loadedResources.Count,
                DataItems    = new List<ServiceDataInspectorInfo.DataInfo>()
            };
            foreach (var kvp in _loadedResources)
            {
                cat.DataItems.Add(new ServiceDataInspectorInfo.DataInfo
                {
                    Key          = kvp.Key.ToString(),
                    TypeName     = kvp.Value?.GetType().Name ?? "null",
                    ValueSummary = kvp.Value?.name ?? "null",
                });
            }
            InspectorInfo.Categories.Add(cat);
        }

        // ============================================================
        // 索引辅助 / 静态工具
        // ============================================================
        private void ResourcesLoadAllInto(Type type, string typeTag)
        {
            var resources = Resources.LoadAll("", type);
            if (resources == null) return;
            bool filterPreview = type == typeof(AnimationClip);
            foreach (var r in resources)
            {
                if (r == null) continue;
                if (filterPreview && r.name.StartsWith("__preview__", StringComparison.Ordinal)) continue;
                var key = new ResourceKey(r.name, false, NormalizeTypeTag(typeTag));
                if (!_loadedResources.ContainsKey(key))
                {
                    _loadedResources[key] = r;
                    Log($"自动加载资源: {r.name} ({typeTag})");
                }
            }
        }

        /// <summary>读取 Resources/CharacterFBXManifest.json，把记录的 modelPath → clip 名列表写进 _modelClipNames。</summary>
        private void LoadFBXManifestIfPresent()
        {
            var asset = Resources.Load<TextAsset>(FBXManifestResourcePath);
            if (asset == null) return;
            try
            {
                var w = JsonUtility.FromJson<FBXManifest>(asset.text);
                if (w?.entries == null) return;
                int added = 0;
                foreach (var e in w.entries)
                {
                    if (e == null || string.IsNullOrEmpty(e.path)) continue;
                    var key = NormalizeModelKey(e.path);
                    if (_modelClipNames.ContainsKey(key)) continue;
                    _modelClipNames[key] = e.clips != null ? new List<string>(e.clips) : new List<string>();
                    added++;
                }
                Log($"FBX manifest 加载：+{added} 条记录（总 {_modelClipNames.Count}）");
            }
            catch (Exception ex) { Log($"FBX manifest 解析失败：{ex.Message}", Color.yellow); }
        }

        private static string NormalizeTypeTag(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            switch (s)
            {
                case "Prefab":  case "GameObject": return "Prefab";
                case "Texture": case "Texture2D":  return "Texture";
                default:                           return s;
            }
        }

        private static IEnumerable<string> EnumerateLoadCandidates(string path)
        {
            yield return path;
            if (path.Contains('/') || path.Contains('\\')) yield break;
            foreach (var hint in _subfolderHints)
            {
                if (string.IsNullOrEmpty(hint)) continue;
                yield return $"{hint}/{path}";
            }
        }

        private static Sprite LoadSpriteByName(string path)
        {
            var spriteName = Path.GetFileNameWithoutExtension(Path.GetFileName(path));
            if (string.IsNullOrEmpty(spriteName)) spriteName = path;
            foreach (var hint in _subfolderHints)
            {
                var sprites = Resources.LoadAll<Sprite>(hint);
                if (sprites == null) continue;
                foreach (var s in sprites) if (s != null && s.name == spriteName) return s;
            }
            return null;
        }

        private static string NormalizeModelKey(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            var p = path.Replace('\\', '/');
            const string prefix = "Assets/Resources/";
            if (p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) p = p.Substring(prefix.Length);
            var ext = Path.GetExtension(p);
            if (!string.IsNullOrEmpty(ext)) p = p.Substring(0, p.Length - ext.Length);
            return p;
        }

        // ============================================================
        // Editor 专用（按真实文件名索引；同时把 FBX 内 AnimationClip 入缓存）
        // ============================================================
#if UNITY_EDITOR
        private void EditorIndexByFileName(Type type, string typeTag)
        {
            var guids = UnityEditor.AssetDatabase.FindAssets($"t:{type.Name}");
            if (guids == null) return;
            int added = 0;
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                if (path.IndexOf("/Resources/", StringComparison.Ordinal) < 0) continue;

                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath(path, type);
                if (asset == null) continue;

                var key = new ResourceKey(Path.GetFileNameWithoutExtension(path), false, NormalizeTypeTag(typeTag));
                if (!_loadedResources.ContainsKey(key))
                {
                    _loadedResources[key] = asset;
                    added++;
                }
            }
            Log($"[Editor] 索引 {typeTag}: +{added} 条");
        }

        /// <summary>遍历 Resources/ 下所有 FBX，登记 path→clip 名单，并把 clip 本体入 _loadedResources（覆盖旧 EditorIndexAnimationClips 的职责）。</summary>
        private void EditorIndexModelClipNames()
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("t:Model");
            if (guids == null) return;
            int withClips = 0, clipsCached = 0;
            foreach (var guid in guids)
            {
                var assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath)) continue;
                if (assetPath.IndexOf("/Resources/", StringComparison.Ordinal) < 0) continue;

                var key = NormalizeModelKey(assetPath);
                if (_modelClipNames.ContainsKey(key)) continue;

                var clips = LoadAllClipsAtModelPath(assetPath);
                var names = new List<string>(clips?.Count ?? 0);
                if (clips != null)
                {
                    foreach (var c in clips)
                    {
                        if (c == null) continue;
                        names.Add(c.name);
                        var clipKey = new ResourceKey(c.name, false, "AnimationClip");
                        if (!_loadedResources.ContainsKey(clipKey))
                        {
                            _loadedResources[clipKey] = c;
                            clipsCached++;
                        }
                    }
                }
                _modelClipNames[key] = names;
                if (names.Count > 0) withClips++;
            }
            Log($"[Editor] 模型 clip 名单 {_modelClipNames.Count} 条（{withClips} 个含 clip）；AnimationClip 缓存 +{clipsCached}");
        }

        private static List<AnimationClip> LoadAllClipsAtModelPath(string path)
        {
            var list = new List<AnimationClip>();
            if (string.IsNullOrEmpty(path)) return list;

            var candidates = new List<string> { path };
            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                foreach (var ext in new[] { ".fbx", ".FBX", ".obj", ".OBJ", ".dae", ".blend" })
                    candidates.Add($"Assets/Resources/{path}{ext}");
            }
            foreach (var c in candidates)
            {
                var subs = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(c);
                if (subs == null || subs.Length == 0) continue;
                foreach (var a in subs)
                {
                    if (a is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                        list.Add(clip);
                }
                if (list.Count > 0) break;
            }
            return list;
        }
#endif

        // ============================================================
        // FBX manifest 数据载体
        // ============================================================
        [Serializable] private class FBXManifest      { public FBXManifestEntry[] entries; }
        [Serializable] private class FBXManifestEntry { public string path; public string[] clips; }
    }
}
