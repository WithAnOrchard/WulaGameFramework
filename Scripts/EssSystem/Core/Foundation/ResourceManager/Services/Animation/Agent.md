# AnimationClipService & ModelAnimationService

**Services** (`Service<AnimationClipService>`, `Service<ModelAnimationService>`)

## 职责

- **AnimationClipService**：异步加载 AnimationClip 资源，缓存 AnimationClip 对象
- **ModelAnimationService**：管理 FBX 模型和其内部 AnimationClip，提供模型动画查询

## Event API

### AnimationClipService

| 常量 | 字符串值 | 用途 |
|---|---|---|
| `EVT_GET_ANIMATION_CLIP_ASYNC` | `GetAnimationClipAsync` | 异步获取 AnimationClip，参数 `[string path, string clipName]` → `Ok(AnimationClip)` |
| `EVT_LOAD_ANIMATION_CLIP_ASYNC` | `LoadAnimationClipAsync` | 异步加载 AnimationClip，参数 `[string path]` → `Ok(AnimationClip)` |

### ModelAnimationService

| 常量 | 字符串值 | 用途 |
|---|---|---|
| `EVT_GET_MODEL_CLIPS` | `GetModelClips` | 取 FBX/Model 内全部 AnimationClip，参数 `[string modelPath]` → `Ok(List<AnimationClip>)` |
| `EVT_GET_ALL_MODEL_PATHS` | `GetAllModelPaths` | 枚举已索引的所有 FBX/Model 路径，参数 `[]` → `Ok(List<string>)` |

## 文件结构

```
Services/Animation/
├── AnimationClipService.cs
├── ModelAnimationService.cs
├── Agent.md
```
