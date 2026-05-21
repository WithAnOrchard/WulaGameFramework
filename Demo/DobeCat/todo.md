# DobeCat 桌面宠物 — 策划 TODO

> 基于 Unity + EssSystem 框架开发的桌面宠物（Desktop Pet）项目。
> 角色：DobeCat（一只猫咪），常驻桌面，伴随用户工作/娱乐。

---

## 一、项目定位

- **形态**: Windows 桌面宠物（透明无边框窗口，常驻最前 / 可置底）
- **平台**: Windows 优先，后续考虑 macOS
- **风格**: 2D 像素 / 卡通猫咪（沿用 CharacterManager sheet 动画方案）
- **气质**: 治愈 + 萌 + 偶尔搞怪，陪伴感优先
- **参考**: 莫娜桌宠 / VPet / Shimeji / 猫宿

## 二、核心体验

### 玩家与桌宠的关系
- 桌宠"住"在桌面上，会自己活动（待机、走动、睡觉、玩耍）
- 用户可以撸它、投喂、穿戴装扮
- 桌宠会根据用户使用电脑的状态做出反应（专注 / 久坐 / 深夜）

### 核心循环（每日）
- 桌宠根据需求（饿/困/无聊）自主切换行为
- 用户偶尔互动（点击/拖拽/投喂）→ 提升好感度
- 好感度解锁新动作 / 装扮 / 对话

## 三、桌面宠物窗口系统（关键技术点）

- [ ] **透明无边框窗口**: Unity Player 启用 `Use DXGI Flip` + Win32 API 设置 `WS_EX_LAYERED | WS_EX_TRANSPARENT`
- [ ] **窗口置顶 / 置底切换**: `SetWindowPos` HWND_TOPMOST
- [ ] **点击穿透**: 非桌宠像素区域鼠标穿透到下层窗口（基于 alpha 检测 / 区域 hit-test）
- [ ] **拖拽移动**: 鼠标按住桌宠拖拽，整窗口跟随
- [ ] **多显示器支持**: 边界检测、跨屏移动
- [ ] **任务栏 / Dock 检测**: 不被遮挡，落地动画对齐底边
- [ ] **开机自启 + 系统托盘菜单**

## 四、桌宠行为系统（基于 EntityManager Brain）

### Needs（需求）
- [ ] 饥饿（Hunger）
- [ ] 精力（Energy）
- [ ] 心情（Mood）
- [ ] 无聊（Boredom）

### Brain Actions（自主行为）
- [ ] **Idle**: 站立 / 坐下 / 舔毛 / 甩尾巴
- [ ] **Wander**: 在桌面屏幕范围内随机走动
- [ ] **Sleep**: 精力低时趴下睡觉
- [ ] **Play**: 追逐玩具 / 抓蝴蝶
- [ ] **Eat**: 走向食盆吃东西
- [ ] **Reaction**: 对鼠标/光标做出反应（盯着、扑过去）
- [ ] **Special**: 随机彩蛋动作（打哈欠、伸懒腰、卡 BUG）

### Sensors（感知）
- [ ] 鼠标位置感知
- [ ] 屏幕边界感知
- [ ] 用户活动状态感知（前台窗口标题、键鼠空闲时长）

## 五、互动系统

- [ ] **点击**: 单击触发表情 / 叫声
- [ ] **长按 / 撸**: 撸猫累计好感度
- [ ] **拖拽**: 拎起放下，落地有反馈
- [ ] **右键菜单**: 投喂 / 装扮 / 设置 / 退出
- [ ] **投喂**: 拖拽食物到桌宠身上
- [ ] **对话气泡**: 偶尔冒出文字（吐槽 / 问候 / 提醒）

## 六、内容系统

### 装扮
- [ ] 帽子 / 围巾 / 衣服槽位
- [ ] 装扮通过 ResourceManager 加载 sprite sheet 叠加在 Body 上

### 表情 / 动作包
- [ ] 基础情绪：开心 / 生气 / 困倦 / 好奇
- [ ] 节日 / 活动主题动作

### 对话库
- [ ] 通用问候（早安 / 晚安）
- [ ] 状态吐槽（饿了 / 困了）
- [ ] 时间感知（深夜劝睡 / 整点报时）
- [ ] 用户感知（专注鼓励 / 久坐提醒）

## 七、陪伴功能（差异化卖点）

- [ ] **番茄钟 / 专注计时**: 桌宠帮你专注，结束撒花
- [ ] **久坐提醒**: 久坐后桌宠拉你起来活动
- [ ] **喝水 / 休息提醒**
- [ ] **整点报时 / 自定义闹钟**
- [ ] **天气播报**（API 拉取）
- [ ] **桌面便签 / 待办**（桌宠帮你举着小牌子）

## 八、系统对接（EssSystem 框架）

- [ ] **CharacterManager**: 注册 DobeCat sheet 动画（idle/walk/sleep/eat/play 多套）
- [ ] **EntityManager + Brain**: 桌宠的 Utility AI（参考 Tribe 动物方案）
- [ ] **InventoryManager**: 食物 / 装扮 / 玩具背包
- [ ] **UIManager**: 右键菜单 / 设置面板 / 装扮界面 / 对话气泡
- [ ] **AudioManager**: 喵叫 / 呼噜 / 互动音效
- [ ] **DialogueManager**: 对话气泡内容驱动
- [ ] **DataManager**: 好感度 / 需求值 / 装扮 / 设置持久化
- [ ] **MapManager / Voxel3DMapManager**: ❌ 不需要

### 需要新增的能力
- [ ] **DesktopWindowManager**（新模块）: 封装 Win32 窗口操作（透明 / 穿透 / 置顶 / 拖拽）
- [ ] **OSInputSensor**（新 Sensor）: 全局鼠标位置 / 前台窗口 / 空闲时长

## 九、技术调研待办

- [ ] Unity 实现透明窗口最佳方案（HDRP 不支持，确认用 URP / Built-in）
- [ ] Unity 渲染区域外鼠标点击穿透实现
- [ ] 多猫支持（同时多个桌宠实例）
- [ ] 性能预算：CPU < 2%，内存 < 200MB，GPU 静止时近 0
- [ ] 帧率策略：无互动时降帧（10fps），互动时升回 60fps

## 十、开发里程碑

- [ ] **M0 立项**: 玩法文档定稿、美术风格 demo 一张
- [ ] **M1 窗口原型**: 透明窗口 + 拖拽 + 一只会走的猫（详见 §M1 拆解）
- [ ] **M2 行为系统**: Brain 接入，需求/动作/反应跑通
- [ ] **M3 互动 & 内容**: 撸猫、投喂、对话气泡、基础装扮
- [ ] **M4 陪伴功能**: 番茄钟 + 久坐提醒 + 整点报时
- [ ] **M5 打磨发布**: 设置面板、自启、托盘、安装包

## 十一、内容清单（待填）

### 角色
- [ ] DobeCat 主角猫（待设计：品种 / 配色 / 性格）

### 动作 sheet
- [ ] idle / walk / run / sleep / eat / sit / lick / stretch / yawn / play …

### 食物
- [ ] 

### 装扮
- [ ] 

### 玩具
- [ ] 

### 对话
- [ ] 

## §M1 窗口原型 — 任务拆解

> 目标：在桌面上看到一只能拖拽、能左右走动的 DobeCat。无 AI，无互动，纯技术验证。
> 验收：启动 exe → 桌面出现猫 → 鼠标可拖拽 → 松手后猫自己走来走去 → 不挡鼠标点击桌面图标。

### M1.1 工程基础（0.5 天）

- [ ] **新建场景**: `Assets/Demo/DobeCat/Scenes/DobeCat.unity`
- [ ] **目录骨架**:
  - `Demo/DobeCat/Scripts/Window/` — 窗口与 Win32 封装
  - `Demo/DobeCat/Scripts/Pet/` — 桌宠运行时
  - `Demo/DobeCat/Scripts/Boot/` — 启动入口
  - `Demo/DobeCat/Resources/DobeCat/` — 临时美术资源
  - `Demo/DobeCat/Art/` — 原始 sheet
- [ ] **渲染管线确认**: 用 Built-in 或 URP（HDRP 不支持透明窗口），`Camera.clearFlags = SolidColor`，`backgroundColor.a = 0`
- [ ] **Player Settings**:
  - Resolution: Windowed, 默认 600x400
  - Use DXGI Flip Model Swapchain ✅
  - 禁用启动 Splash
  - 关闭 Resizable Window

### M1.2 透明无边框窗口（1 天）

- [ ] **`DesktopWindow.cs`**（Demo/DobeCat/Scripts/Window）
  - `[DllImport]`: `GetActiveWindow`, `SetWindowLong`, `GetWindowLong`, `SetLayeredWindowAttributes`, `SetWindowPos`, `DwmExtendFrameIntoClientArea`, `MARGINS`
  - `Awake()`：拿到 HWND，应用 `WS_POPUP`，去掉 `WS_CAPTION | WS_THICKFRAME`
  - 应用扩展样式 `WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TOOLWINDOW`（不在任务栏显示）
  - 调用 `DwmExtendFrameIntoClientArea(MARGINS{-1})` 让透明区域真正透明
- [ ] **Editor 模式跳过 Win32 调用**（`#if !UNITY_EDITOR`）
- [ ] **验证**: 打包 exe 启动，背景透明、无边框、置顶
- [ ] **风险记录**: 多显示器 DPI 缩放下 HWND 尺寸是否对齐

### M1.3 鼠标点击穿透（1 天，难点）

- [ ] **方案选型**:
  - A. 全窗口穿透 + 桌宠像素时关闭穿透（推荐）
  - B. 通过区域 `SetWindowRgn` 划定不透明区域（实时更新成本高）
- [ ] **像素 hit-test 实现**:
  - `MouseHitTester.cs`：每帧用 `Camera.ScreenToWorldPoint` + `SpriteRenderer.sprite.texture.GetPixel` 判断鼠标下是否有 alpha > 阈值
  - 仅在桌宠 sprite 边界框内才采样，避免每帧全屏扫描
  - 标记 dirty 帧，无变化跳过
- [ ] **穿透切换**:
  - hit = false → 加 `WS_EX_TRANSPARENT`
  - hit = true → 去掉 `WS_EX_TRANSPARENT`
- [ ] **验证**: 鼠标移到猫身上能点中、移开能点桌面图标
- [ ] **已知坑**: Unity 鼠标坐标在 `WS_EX_TRANSPARENT` 下不会触发 OnMouseXxx，需用 Win32 `GetCursorPos` + `ScreenToClient` 兜底

### M1.4 拖拽移动整窗口（0.5 天）

- [ ] **`PetDragger.cs`**:
  - 鼠标按下且 hit = true → 进入拖拽
  - 拖拽中用 `SetWindowPos` 移动 HWND 而不是改 Transform
  - 记录按下时鼠标屏幕坐标与窗口左上角偏移
- [ ] **落地反馈**: 松手后播放 "落地" 动画（M1 阶段先用 idle 替代）
- [ ] **屏幕边界限制**: 用 `SystemParameters.WorkArea` 等价物（`SystemParametersInfo SPI_GETWORKAREA`）防止拖出可见区域

### M1.5 一只会走的猫（0.5 天）

- [ ] **资源**: 用 Tribe 现成 sheet 或新画一只占位猫（idle 4 帧 + walk 4 帧）
- [ ] **复用 CharacterManager**:
  - `RegisterSheetCreature("DobeCat", ...)` 注册 sheet 配置
  - `CharacterViewBridge.CreateCharacter("DobeCat", ...)` 创建视图
- [ ] **`PetWanderController.cs`**（M1 临时版，M2 替换为 Brain）:
  - 状态机：Idle ↔ Walk
  - 每 N 秒随机选目标点，朝目标走，到达后切 Idle
  - 调 `CharacterService.SetDirection` 控制朝向
- [ ] **窗口跟随**: 桌宠世界坐标 → 屏幕坐标 → `SetWindowPos`（or 让窗口固定全屏，桌宠在窗口内自由移动；二选一，推荐后者更简单）

### M1.6 全屏画布方案（推荐路线）

> 初版用「全屏透明窗口 + 猫在窗口内移动」更稳，后期再升级到「窗口跟随猫」。

- [ ] 启动时窗口 resize 到主屏 WorkArea 大小
- [ ] 摄像机正交尺寸匹配屏幕分辨率（1 unit = 1 pixel）
- [ ] 桌宠在窗口坐标系内自由移动
- [ ] 拖拽 = 移动桌宠的 Transform，不是窗口

### M1.7 退出与调试（0.5 天）

- [ ] **快捷键退出**: `Esc` 或 `Ctrl+Shift+Q`
- [ ] **临时调试 UI**: F1 切换显示 FPS / 鼠标 hit / 当前状态
- [ ] **日志**: 输出到 `%AppData%/DobeCat/log.txt`

### M1 验收清单

- [ ] exe 双击启动后桌面出现猫，无任务栏图标
- [ ] 猫自己左右走动，朝向正确
- [ ] 鼠标移到猫身上 cursor 可点中，移开点击穿透到桌面
- [ ] 按住猫拖拽，松手后继续走
- [ ] Esc 可退出
- [ ] 启动 5 分钟 CPU 占用 < 5%

### M1 技术调研 TODO

- [ ] 调研：Unity 透明窗口在 Windows 11 + 多显示器 + DPI 缩放下的兼容性
- [ ] 调研：是否需要每帧调 `SetWindowPos(HWND_TOPMOST)` 防止被全屏程序覆盖
- [ ] 调研：游戏全屏时桌宠如何处理（隐藏 / 暂停）
- [ ] 调研：Mac 端透明窗口替代方案（NSWindow + setOpaque:NO），评估跨平台抽象层

### M1 输出物

- [ ] `DobeCat.exe` 可独立运行
- [ ] `Demo/DobeCat/Agent.md` 第一版（窗口模块说明）
- [ ] 一段录屏 demo（拖拽 + 走动 + 穿透）

---

## 十二、待决问题

- [ ] 是否支持多只桌宠同屏？
- [ ] 是否做云存档 / 账号体系？
- [ ] 是否开放 MOD（用户自定义猫咪 / 装扮）？
- [ ] 商业化路径：免费 / 装扮付费 / 一次性买断？
- [ ] 是否需要联网功能（天气 / 公告 / 活动）？
