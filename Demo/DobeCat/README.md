# DobeCat — 桌面宠物 Demo

基于 EssSystem 框架的桌面宠物原型。当前阶段：**M1 窗口原型**。

## 目录结构

```
Demo/DobeCat/
├── todo.md                       # 策划与里程碑
├── README.md                     # 本文件
├── DobeCatGameManager.cs         # 总控（继承 AbstractGameManager）
└── Scripts/
    ├── Window/
    │   ├── Win32Native.cs        # Win32 P/Invoke（仅 Standalone Windows 编译）
    │   └── DesktopWindow.cs      # 透明 / 置顶 / 点击穿透
    └── Pet/
        ├── PetView.cs            # 占位视觉（程序生成猫咪 sprite）
        ├── PetWander.cs          # M1 临时漫游（Idle ↔ Walk）
        ├── PetDragger.cs         # 鼠标拖拽桌宠
        └── PetClickThroughDriver.cs  # 根据鼠标命中切换窗口穿透
```

## 在 Unity 内运行测试

### 1. 创建场景

1. 新建空场景：`Assets/Demo/DobeCat/Scenes/DobeCat.unity`
2. 场景里只放一个空 GameObject，挂 `DobeCatGameManager`
3. 删除自带的 Main Camera —— GameManager 启动时会自己确保一个透明背景的正交相机
4. 打开 `File → Build Settings`，把场景加到列表

### 2. Editor 内调试

直接 Play：
- Win32 调用被 `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR` 跳过 → 不会真的让编辑器透明
- 但能看到一只占位猫在游戏视图里漫游
- 鼠标按住可拖拽
- 按 `Esc` 退出 Play

**预期 Console 输出：**
```
[AbstractGameManager] 自动添加基础 Manager: EventProcessor (优先级: -30)
[AbstractGameManager] 自动添加基础 Manager: DataManager (优先级: -20)
[AbstractGameManager] 自动添加基础 Manager: ResourceManager (优先级: 0)
[AbstractGameManager] 自动添加基础 Manager: AudioManager (优先级: 3)
[AbstractGameManager] 自动添加基础 Manager: UIManager (优先级: 5)
[DobeCatGameManager] 框架 Manager 初始化完成
[DobeCatGameManager] 占位桌宠已生成
[DesktopWindow] 非 Standalone Windows，跳过窗口设置（Editor 调试模式）
```

### 3. 真机测试（看到桌面上的猫）

1. `File → Build Settings → Windows / Standalone / Windows / x86_64`
2. **Player Settings** 关键项（缺一不可）：
   - Resolution and Presentation:
     - Fullscreen Mode: `Windowed`
     - Default Window Width / Height: 任意（启动时会被覆盖为 WorkArea）
     - Resizable Window: `false`
     - Visible In Background: `true`
     - **❗ Use DXGI Flip Model Swapchain for D3D11: 关闭（取消勾选）**  
       开启时 backbuffer 不带 alpha，DWM 看到全部不透明 → 桌宠背景看不见桌面
   - Other Settings:
     - Color Space: `Linear` 或 `Gamma` 都行
     - Auto Graphics API: 关闭，手动只保留 `Direct3D11`（DWM 透明依赖 D3D11）
     - **❗ Api Compatibility Level: `.NET Framework`**（系统托盘依赖 `System.Windows.Forms`，`.NET Standard 2.1` 用不了）
   - Splash Image: 关闭 Splash（更干净的启动）

3. **渲染管线**：当前 `TransparentRenderBlit` 仅支持 **Built-in 管线**。  
   - URP / HDRP 工程需把后处理改成 Renderer Feature 或 Custom Pass  
   - 检查方法：`Edit → Project Settings → Graphics → Scriptable Render Pipeline Settings` 为空 = Built-in

4. Build → 运行 exe → 桌面上能看到只占位猫在屏内漫游 + 拖拽 + 移开后鼠标穿透

### 4. 替换占位贴图

把任意一张 PNG（透明背景）放到 `Assets/Demo/DobeCat/Resources/DobeCat/cat_idle.png`：
- 在 `DobeCatGameManager` Inspector 里把 `_petSpritePath` 设为 `DobeCat/cat_idle`
- 重新 Play 即可。Sprite 加载失败时自动退回程序生成占位图。

## M1 已实现 / 未实现

### 已实现
- 透明无边框窗口、Toolwindow（不在任务栏）、置顶
- 点击穿透动态切换（基于 sprite bounds 命中）
- 鼠标拖拽桌宠（拖拽时自动暂停 wander 与穿透）
- 占位漫游：屏幕内随机选点 Idle ↔ Walk + 自动翻转朝向
- Esc 退出
- 框架基础 Manager 自动接管

### 待办（已记录在 `todo.md`）
- M1.3 升级：alpha 像素级 hit-test（当前是矩形包围盒）
- M1.5 接入 `CharacterManager` 的 sheet 动画
- M1.7 落地动画 / 调试 UI / 日志写入 `%AppData%`
- M2 接入 EntityManager Brain / Needs

## 已知坑

- HDRP 不支持透明窗口；当前项目是 Built-in / URP 时才能正确透明
- 在多显示器 + DPI 缩放下 `Display.main.systemWidth/Height` 可能不等于实际像素，需后续接入 `SystemParametersInfo SPI_GETWORKAREA`
- 全屏游戏运行时桌宠会被遮挡（Win32 全屏独占模式特性）—— M5 阶段处理
