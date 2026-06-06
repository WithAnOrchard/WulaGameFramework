# AnimationClipService & ModelAnimationService
**Services** (`Service<AnimationClipService>`, `Service<ModelAnimationService>`)
## 职责
- **AnimationClipService**：异步加载 AnimationClip 资源，缓存 AnimationClip 对象
- **ModelAnimationService**：管理 FBX 模型和其内部 AnimationClip，提供模型动画查询
## Event API

> Full Event definitions (params / return / side effects / usage) live in root Events.md -> section: **Animation Event**.

- `EVT_GET_ALL_MODEL_PATHS`
- `EVT_GET_ANIMATION_CLIP_ASYNC`
- `EVT_GET_MODEL_CLIPS`
- `EVT_LOAD_ANIMATION_CLIP_ASYNC`

## 文件结构
```
Services/Animation/
├── AnimationClipService.cs
├── ModelAnimationService.cs
├── Agent.md
```
