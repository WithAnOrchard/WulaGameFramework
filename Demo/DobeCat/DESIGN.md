# DobeCat 桌面宠物 — 功能设计文档

> **项目定位**：Windows 桌面宠物（常驻透明窗口）+ 可扩展小游戏集合，基于 EssSystem 框架开发。
> 角色：DobeCat（猫咪），陪伴用户工作 / 娱乐，治愈 + 萌 + 偶尔搞怪。
>
> **状态图例**
> ✅ 已实现　　🔧 部分实现（骨架在，需完善）　　🔲 待开发

---

## 一、功能总览

| 功能模块 | 核心功能 | 状态 |
|---|---|:---:|
| 窗口系统 | 透明无边框 / 置顶 / 点击穿透 | ✅ |
| 桌宠展示 | Sheet 动画 / 方向翻转 / 多套动作 | 🔧 |
| AI 行为 | Utility AI 游荡 / 玩家控制 / Needs 驱动行为 | 🔧 |
| 用户互动 | 拖拽 / 右键菜单 / 撸猫 / 投喂 / 对话气泡 | 🔧 |
| 陪伴功能 | 番茄钟 / 久坐提醒 / 整点报时 / 天气播报 | 🔲 |
| 内容系统 | 背包 / 商店 / 农场 / 装扮 / 好感度 | 🔧 |
| 联网功能 | 多人房间 / 桌宠幽灵同步 / 云存档 | ✅ |
| 直播互动 | B 站弹幕 / 礼物 / 开播检测 | ✅ |
| 动态 / 投稿提醒 | 主播发动态或上传视频时全员桌宠提醒 | 🔲 |
| 系统集成 | 托盘菜单 / 登录验证 / 开机自启 | 🔧 |
| OBS 捕捉模式 | 窗口化切换 / 可被直播软件采集 | 🔲 |
| 分层显示 | 背景层 / 猫咪层独立控制 / 窗口缩放 | 🔲 |
| 直播经济系统 | 银币（陪伴+弹幕）/ 金币（电池礼物）/ 双商店 | 🔲 |

---

## 二、OBS 捕捉模式与分层显示

### 2.1 OBS / 直播姬捕捉模式

**目标**：桌宠窗口可被 OBS、直播姬等推流软件捕捉，方便主播将桌宠叠加到直播画面。

**两种显示模式**

| 模式 | 窗口状态 | 适用场景 |
|---|---|---|
| 桌面叠加（默认）| 透明无边框全屏，不在任务栏 | 日常桌面使用 |
| 窗口捕捉模式 | 普通有边框窗口，可被 OBS「窗口捕捉」识别 | 直播推流、录制 |

**切换方式**：右键托盘菜单 → **「显示模式」** → 选择「桌面叠加」或「窗口捕捉」。

**技术实现**

- 桌面叠加：当前已实现（`WS_POPUP + WS_EX_LAYERED + WS_EX_TOOLWINDOW`）。
- 窗口捕捉模式：恢复 `WS_CAPTION | WS_THICKFRAME`，去掉 `WS_EX_TOOLWINDOW`，使窗口出现在任务栏并可被 OBS「窗口捕捉」选中；同时保留透明背景选项（Game Capture 模式下可透出绿幕 / 透明）。
- 切换时调用 `SetWindowLong` + `SetWindowPos` 重新应用样式，不需重启。

| 子功能 | 状态 |
|---|:---:|
| 桌面叠加模式 | ✅ |
| 窗口捕捉模式（托盘切换）| 🔲 |
| OBS Game Capture 透明背景 | 🔲 |

---

### 2.2 分层显示与缩放

**目标**：背景层与猫咪层可独立控制，支持整体窗口缩放，方便不同使用场景（直播叠加 / 桌面摆放）。

**层级划分**

| 层 | 内容 | Camera / 渲染排序 |
|---|---|---|
| 背景层（Layer 0）| 可选装饰背景（房间 / 窗台 / 草地等图片）| SortingLayer: Background |
| 猫咪层（Layer 1）| DobeCat 桌宠本体 + 动画 | SortingLayer: Pet |
| UI 层（Layer 2）| 对话气泡 / 状态图标 | SortingLayer: UI |

**背景层开关**

- 右键设置面板 → **「显示背景」** 勾选框。
- 关闭后仅渲染猫咪层，透明区域完全穿透到桌面 / 直播画面，适合「只要猫」的场景。
- 技术上：切换 Background SortingLayer 对应 Camera 的 Culling Mask，或直接 `backgroundGo.SetActive(false)`。

**窗口 / 桌宠缩放**

- 右键设置面板 → **「桌宠大小」** 滑动条（50% ~ 300%）。
- 实现方式：调整 Camera 正交 Size（整体缩小视野 = 桌宠变大）或直接缩放桌宠根节点 `localScale`。
- 缩放值持久化到 DataManager。

| 子功能 | 状态 |
|---|:---:|
| 背景层 / 猫咪层分离渲染 | 🔲 |
| 右键设置关闭背景 | 🔲 |
| 窗口 / 桌宠整体缩放 | 🔲 |
| 缩放值持久化 | 🔲 |

---

## 三、窗口系统

### 2.1 透明无边框窗口

**目标**：桌宠悬浮在桌面上，背景完全透明，无标题栏和边框。

**实现原理**

- 启动时窗口为普通 460×400 登录窗口，登录通过后由 `DesktopOverlay.Enter()` 一次性切换为全屏透明叠加层。
- Win32 调用链：`SetWindowLong(WS_POPUP)` 去边框 → `SetWindowLong(WS_EX_LAYERED)` 启用分层合成 → `DwmExtendFrameIntoClientArea(MARGINS{-1})` 真透明 → `SetWindowPos(HWND_TOPMOST)` 全屏置顶。
- Camera 的 `backgroundColor = Color.clear`，透明像素直接透出桌面。

| 子功能 | 状态 | 备注 |
|---|:---:|---|
| 透明无边框窗口 | ✅ | Built-in 管线 + D3D11 + DwmExtend |
| 窗口置顶 | ✅ | `WS_EX_TOPMOST` |
| 隐藏任务栏 / Alt+Tab | ✅ | `WS_EX_TOOLWINDOW` |
| 全屏叠加层（桌宠在窗口内移动）| ✅ | 登录后切换到主屏 WorkArea 大小 |
| 鼠标点击穿透（矩形包围盒）| ✅ | `WS_EX_TRANSPARENT` 动态开关 |
| 鼠标点击穿透（像素级 alpha）| 🔲 | Sprite.texture.GetPixel 精确检测 |
| 多显示器边界感知 | 🔲 | `SPI_GETWORKAREA` 获取实际工作区 |

### 2.2 帧率策略

**目标**：桌宠静止/无互动时降低帧率，减少 CPU 占用（目标静止 < 2%）。

| 状态 | 目标帧率 | 触发条件 |
|---|---|---|
| 空闲（无互动）| 10 fps | 无鼠标输入 + AI 处于 Idle 状态 |
| 活跃（有互动）| 60 fps | 鼠标悬停 / WASD / 拖拽 / 动画播放 |

- **状态** 🔲 待开发：调用 `Application.targetFrameRate` 动态切换。

---

## 四、桌宠展示与动画

### 3.1 动画体系

桌宠视觉由 **CharacterManager** 驱动，采用 sprite sheet 模式（多行方向式 sheet）。

**已注册配置（✅）**

| 角色 | ConfigId | 说明 |
|---|---|---|
| Warrior（测试）| `Warrior` | 临时占位，4 方向行走 |
| Mage（幽灵测试）| `Mage` | 联网幽灵桌宠 |

**待设计动作集（🔲）**

| 动作 ID | 触发条件 | 优先级 |
|---|---|---|
| `idle` | 静止 | 基础 |
| `walk` | 移动中 | 基础 |
| `sit` | 长时间静止 | 中 |
| `sleep` | Energy 需求低 | 中 |
| `eat` | 走向食盆 | 中 |
| `lick` | 随机 Idle 变体 | 低 |
| `stretch` | 随机 Idle 变体 | 低 |
| `yawn` | 随机 Idle 变体 | 低 |
| `play` | 追逐玩具 | 中 |
| `react_happy` | 撸猫 / 投喂后 | 中 |
| `react_hit` | 受到点击 | 高 |

### 3.2 装扮叠加（🔲 待开发）

- 装扮分为帽子 / 围巾 / 衣服三个槽位，每个槽位独立 sprite sheet，与 Body 层叠加渲染。
- 通过 `CharacterManager` 的多部件机制（`WithSheet`）注册额外部件，运行时动态切换。
- 装扮来源：商店购买 / 好感度解锁。

---

## 五、AI 行为系统

### 4.1 架构

基于 **EntityManager BrainComponent（Utility AI）**，每帧各 Consideration 竞争评分，最高分的 Action 获得执行权。

```
Consideration(评分函数) → BrainComponent(调度) → IBrainAction(执行)
                                    ↕
                            BrainContext(黑板)
                                    ↕
                            NeedsComponent(需求值)
```

### 4.2 已实现 Consideration

| Consideration | 评分逻辑 | 对应 Action | 状态 |
|---|---|---|:---:|
| `PlayerControl` | WASD 有输入 → 1.0，否则 0 | `PetPlayerControlAction` | ✅ |
| `Wander` | 固定基线 0.2 | `PetWanderAction` | ✅ |

### 4.3 待实现 Consideration（🔲）

| Consideration | 评分逻辑 | 对应 Action |
|---|---|---|
| `Sleep` | Energy < 0.2 → 评分随 Energy 降低升高 | `SleepAction` |
| `Eat` | Hunger > 0.7 → 走向食盆 | `EatAction` |
| `Play` | Boredom > 0.6 → 追逐玩具 | `PlayAction` |
| `ReactToCursor` | 鼠标在附近 → 盯着看 / 扑过去 | `CursorReactAction` |
| `Idle_Variant` | 静止超过 N 秒 → 随机变体动作 | `IdleVariantAction` |

### 4.4 Needs 系统（🔲 待开发）

桌宠有 4 个需求值（0~1），随时间自动增长，由对应 Action 消耗。

| 需求 ID | 增长速率 | 超过阈值行为 | 消耗方式 |
|---|---|---|---|
| `Hunger`（饥饿）| 0.01 / 分钟 | 自动走向食盆 | 触发 Eat Action |
| `Energy`（精力）| 0.005 / 分钟 | 趴下睡觉 | 触发 Sleep Action |
| `Mood`（心情）| 由互动降低 | 无明显行为变化 | 撸猫 / 投喂 恢复 |
| `Boredom`（无聊）| 0.008 / 分钟 | 随机玩耍行为 | 触发 Play Action |

### 4.5 传感器（🔲 待开发）

| Sensor | 感知内容 | 实现方式 |
|---|---|---|
| `MouseSensor` | 鼠标世界坐标 | `Win32Native.GetCursorPos` → Camera.ScreenToWorldPoint |
| `BoundsSensor` | 屏幕边界碰撞 | 世界坐标 vs 屏幕边缘换算 |
| `IdleTimeSensor` | 用户键鼠空闲时长 | `GetLastInputInfo` Win32 API |
| `ForegroundSensor` | 前台窗口标题 | `GetForegroundWindow` + `GetWindowText` |

---

## 六、用户互动

| 互动类型 | 触发方式 | 效果 | 状态 |
|---|:---:|---|:---:|
| 拖拽移动 | 鼠标左键按住 + 拖动 | 桌宠跟随鼠标移动，AI 暂停 | ✅ |
| 右键菜单 | 鼠标右键点击桌宠 | 弹出托盘菜单 | ✅ |
| 点击互动 | 鼠标单击桌宠 | 播放表情动画 + 触发叫声音效 | 🔲 |
| 撸猫（长按）| 鼠标按住 > 1s | 好感度 +1/s，播放呼噜音效 | 🔲 |
| 投喂 | 从背包拖拽食物到桌宠 | 消耗食物，Hunger 降低，好感度增加 | 🔲 |
| 对话气泡 | 桌宠主动 / 用户触发 | 头顶弹出文字气泡（3s 自动消失）| 🔲 |

### 5.1 好感度系统（🔲 待开发）

- 好感度（0~100），通过撸猫、投喂、每日签到、完成陪伴任务等方式积累。
- **等级解锁内容**：

| 好感度等级 | 解锁内容 |
|---|---|
| 0–20 | 基础动作、基础对话 |
| 21–40 | 新 Idle 变体动画 |
| 41–60 | 专属装扮道具 |
| 61–80 | 特殊彩蛋动作（打哈欠、伸懒腰）|
| 81–100 | 隐藏对话线、特效 |

---

## 七、陪伴功能

所有陪伴功能均以"桌宠主动提醒"为载体，通过对话气泡 + 动画表达，不打扰用户为原则。

| 功能 | 触发条件 | 桌宠行为 | 状态 |
|---|---|---|:---:|
| 番茄钟 | 用户在托盘菜单启动 | 倒计时结束后撒花 + 气泡庆祝 | 🔲 |
| 久坐提醒 | 键鼠活跃时长 > 45 分钟 | 气泡："该起来活动了！" | 🔲 |
| 喝水提醒 | 每 60 分钟一次 | 气泡："记得喝水～" | 🔲 |
| 整点报时 | 每小时整点 | 气泡播报当前时间 | 🔲 |
| 自定义闹钟 | 用户在托盘菜单设置 | 时间到后气泡提醒 | 🔲 |
| 天气播报 | 每日启动 / 用户请求 | 气泡播报今日天气（接外部 API）| 🔲 |
| 深夜劝睡 | 当前时间 > 23:00 | 气泡："主人早点休息哦～" | 🔲 |
| 专注鼓励 | 用户连续工作 > 30 分钟 | 气泡："你好棒，继续加油！" | 🔲 |

---

## 八、内容系统

### 8.1 背包（✅ 已实现）

- 由 **InventoryManager** 管理，B 键切换显示（仅游戏上下文活跃时）。
- 支持物品注册（种子、食物、装扮道具）。

### 8.2 种子商店（✅ 已实现）

- 由 `ShopWindow` + `ShopManager` 驱动，托盘菜单"商店"入口打开。
- 商品列表动态拉取，支持金币购买，购买失败显示原因。
- 当前商品：作物种子（`DobeCatShopSetup.SHOP_SEED_STORE`）。

### 8.3 农场小游戏（✅ 已实现）

- `FarmWorldController` 管理农场地块，支持播种、浇水、收割。
- WASD 控制桌宠走进农场触发游戏上下文，Hotbar 自动显示。
- 农作物配置由 `DobeCatCropSetup` 注册（生长时间、收益等）。

### 8.4 对话库（🔲 待开发）

对话内容由 **DialogueManager** 驱动，每条对话包含文本 + 触发条件。

**对话分类**

| 分类 | 示例 | 触发条件 |
|---|---|---|
| 状态吐槽 | "好饿……主人能给我吃点东西吗" | Hunger > 0.8 |
| 时间感知 | "早安～今天也要加油哦" | 当日首次启动，时间 < 10:00 |
| 天气感知 | "今天好热，多喝水呀" | 天气 API 返回晴天 + 高温 |
| 好感度对话 | "主人，你今天摸了我好多次～" | 累计撸猫 > 100 下 |
| 彩蛋 | "喵……（打了个大哈欠）" | 随机低概率触发 |

---

## 九、直播经济系统

**核心理念**：用观看 / 参与行为（免费）赚银币，用真实礼物充值（电池礼物）赚金币，两种货币对应两个商店，价值体系分离、互不混淆。

### 9.1 货币体系

| 货币 | 获取方式 | 特点 |
|---|---|---|
| 🪙 **银币** | 陪伴时长 + 发送弹幕数量（免费）| 产出稳定，价值较低，适合日消耗品 |
| 💎 **金币** | B 站电池礼物兑换（付费）| 稀缺，价值较高，适合高价值内容 |

**银币产出规则**

| 行为 | 银币产出 | 备注 |
|---|---|---|
| 陪伴时长 | +1 银币 / 10 分钟 | 程序在后台运行即计算 |
| 发送弹幕 | +1 银币 / 条 | 同一直播间内去重（防刷）|
| 每日首次登录 | +5 银币 | 签到奖励 |

**金币兑换规则（电池礼物）**

B 站电池礼物以「电池」为单位（1 电池 = 0.1 元）。

| 礼物等级 | 电池数范围 | 兑换金币 | 示例礼物 |
|---|---|---|---|
| 普通 | 1–9 电池 | 电池数 × 1 金币 | 小心心（1电池）|
| 中级 | 10–99 电池 | 电池数 × 1.2 金币 | 辣条（10电池）|
| 高级 | 100–999 电池 | 电池数 × 1.5 金币 | 爱心（52电池）|
| 豪华 | 1000+ 电池 | 电池数 × 2 金币 | 舰长 / 总督 |

> 注：电池数从 B 站弹幕协议的礼物事件中直接读取（`gift.coin_type == 'gold'` 时为电池礼物）。

### 9.2 双商店设计

**银币商店（日常消耗）**

| 商品类型 | 示例 | 银币价格 |
|---|---|---|
| 农场种子 | 萝卜种子 / 玉米种子 | 10–30 银币 |
| 普通食物 | 猫粮 / 小鱼干 | 20–50 银币 |
| 普通装扮 | 普通帽子 / 蝴蝶结 | 100–300 银币 |
| 对话气泡皮肤 | 简约款 / 可爱款 | 200 银币 |

**金币商店（高价值内容）**

| 商品类型 | 示例 | 金币价格 |
|---|---|---|
| 稀有装扮 | 节日限定皮肤 / 特效 | 50–200 金币 |
| 特殊食物 | 寿司 / 蛋糕（好感度加成）| 30–80 金币 |
| 动作包 | 新 Idle 变体动作组 | 100 金币 |
| 限定彩蛋 | 专属庆祝特效 | 150 金币 |

### 9.3 实现方案

- **银币 / 金币** 作为两种独立货币注册到 `ShopManager`，`CURRENCY_SILVER` / `CURRENCY_GOLD`（当前已有 `CURRENCY_GOLD`，需新增 `CURRENCY_SILVER`）。
- **陪伴计时**：独立 `CompanionTimeTracker` 组件，每 10 分钟通过 `ShopManager.EVT_ADD_WALLET` 给 `player` 账户增加银币。
- **弹幕银币**：订阅 `DanmuManager` 弹幕事件，每条弹幕触发 +1 银币（同 UID 同场次去重）。
- **礼物金币**：订阅礼物事件，读取 `coin_type == 'gold'` 的电池数，按上表比例换算后充值金币。
- **银币商店**：新增 `ShopWindow_Silver`，或在现有 `ShopWindow` 中以 Tab 区分双货币。
- **金币商店**：`ShopWindow_Gold`，商品独立注册（`DobeCatShopSetup.SHOP_GOLD_STORE`）。

| 子功能 | 状态 |
|---|:---:|
| 货币基础（ShopManager CURRENCY_GOLD）| ✅ |
| CURRENCY_SILVER 注册 | 🔲 |
| 陪伴计时 → 银币产出 | 🔲 |
| 弹幕 → 银币产出 | 🔲 |
| 电池礼物 → 金币兑换 | 🔲 |
| 银币商店 UI | 🔲 |
| 金币商店 UI | 🔲 |
| 货币余额持久化 | 🔲 |

---

## 十、联网功能

### 10.1 多人桌宠同步（✅ 已实现）

- 基于 **Mirror** 网络框架。
- 每个客户端以 Host 模式启动，向 `data_exchange_server` 上报房间信息（IP、Port、显示名）。
- 用户在系统托盘"房间列表"选择加入他人桌面，PetNetworkSync 自动在本机生成对方的"幽灵桌宠"跟随移动。

| 子功能 | 状态 |
|---|:---:|
| Mirror Host 自动启动 | ✅ |
| 房间上报 / 心跳（RoomDiscoveryClient）| ✅ |
| 幽灵桌宠生成 + 位置同步 | ✅ |
| 数据交换 Session / Token | ✅ |
| 玩家数据云同步（PlayerDataSync）| ✅ |
| 农场状态多人同步 | 🔧 |

### 10.2 B 站直播互动（✅ 已实现）

| 模式 | 认证方式 | 功能范围 |
|---|---|---|
| Polling | 无需登录 | 纯文字弹幕，约 3s 延迟 |
| Token | SESSDATA Cookie | 实时弹幕 + 礼物 + SC |
| OpenLive | 主播身份码 | 实时弹幕 + 礼物 + SC（仅自己直播间）|

**已接入事件**：弹幕消息、礼物、SC（Super Chat）、开播状态轮询（`LiveStatusManager`）。

**待设计：直播互动效果（🔲）**

| 事件 | 桌宠反应 |
|---|---|
| 普通弹幕 | 气泡显示弹幕内容 |
| 礼物 | 播放开心动画 + 气泡感谢 |
| SC | 特殊庆祝动画 + 大气泡 |
| 进入直播间 | 气泡打招呼 |
| 开播检测 | 开播时播放特殊提醒动画 |

### 10.3 主播动态 / 投稿提醒（🔲 待开发）

**目标**：当关注的主播发布新动态或上传新视频时，所有在线桌宠同步弹出提醒气泡，桌宠播放专属动画。

#### 触发来源

| 内容类型 | B 站 API | 轮询间隔 |
|---|---|---|
| 空间动态（图文 / 转发 / 投票）| `/x/polymer/web-dynamic/v1/feed/space?host_mid={uid}` | 60 秒 |
| 投稿视频（新 av / BV 号）| `/x/space/arc/search?mid={uid}&ps=1` | 120 秒 |

> 轮询时对比本地缓存的最新 `dynamic_id` / `bvid`，有新内容则触发提醒；首次拉取仅建立基线，不触发。

#### 广播机制

```
轮询发现新内容
    ↓
本机桌宠立即反应
    ↓
通过 data_exchange_server（ActionsClient）推送广播消息
    ↓
所有在线客户端收到消息 → 各自桌宠反应
```

- 广播通过现有 `ActionsClient` 上行，其他客户端通过 `RoomDiscoveryClient` 心跳轮询或 Mirror 消息接收。
- 消息结构：`{ type: "space_notify", kind: "dynamic"|"video", title: "...", url: "..." }`。

#### 桌宠反应设计

| 内容类型 | 动画 | 气泡文字 | 气泡持续 |
|---|---|---|---|
| 新动态 | 竖耳朵 + 尾巴翘起 | 「主播发新动态啦！」+ 动态摘要（≤20字）| 8 秒 |
| 新视频投稿 | 兴奋跳跃 + 特效粒子 | 「主播出新视频了！🎬 {标题}」| 10 秒 |

- 气泡点击后调用系统默认浏览器打开对应链接（`Application.OpenURL`）。
- 同一内容 24 小时内只提醒一次（本地去重，存 DataManager）。

#### 配置项（右键设置面板）

| 配置项 | 默认值 | 说明 |
|---|---|---|
| 订阅 UID 列表 | 空 | 可填多个 B 站 UID，逗号分隔 |
| 开启动态提醒 | ✓ | 独立开关 |
| 开启投稿提醒 | ✓ | 独立开关 |
| 提醒音效 | ✓ | 弹出气泡时播放提示音 |

#### 子功能状态

| 子功能 | 状态 |
|---|:---:|
| 动态轮询（Space Dynamic API）| 🔲 |
| 投稿轮询（Space Archive API）| 🔲 |
| 本地新内容去重（DataManager）| 🔲 |
| 本机桌宠气泡提醒 | 🔲 |
| 全员广播（ActionsClient）| 🔲 |
| 气泡点击打开链接 | 🔲 |
| 设置面板 UID 配置 | 🔲 |

---

## 十一、系统集成

| 功能 | 实现方式 | 状态 |
|---|---|:---:|
| 系统托盘图标 | `System.Windows.Forms.NotifyIcon` | ✅ |
| 托盘右键菜单 | 重置位置 / 商店 / 房间列表 / 退出 | ✅ |
| B 站登录验证 | `BilibiliAuthValidator`（SESSDATA 校验）| ✅ |
| 快捷键退出（Ctrl+Shift+Q）| `Win32Native.GetAsyncKeyState`（穿透时也生效）| ✅ |
| 调试测试面板 | `DobeCatTestPanel`（F1 / 自动弹出）| ✅ |
| 开机自启 | 注册表 `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` | 🔲 |
| 日志写入文件 | `%AppData%/DobeCat/log.txt` | 🔲 |

---

## 十二、技术架构

### 12.1 框架 Manager 使用情况

| Manager | 优先级 | DobeCat 用途 | 状态 |
|---|:---:|---|:---:|
| EventProcessor | -30 | 全局事件总线 | ✅ |
| DataManager | -20 | 好感度 / 需求值 / 设置持久化 | 🔧 |
| ResourceManager | 0 | 资源加载 | ✅ |
| AudioManager | 3 | 喵叫 / 呼噜 / 互动音效 | 🔲 接入中 |
| UIManager | 5 | 商店 / 设置面板 / 气泡 | ✅ |
| InventoryManager | 10 | 食物 / 装扮 / 背包 | ✅ |
| CharacterManager | — | Sheet 动画驱动桌宠视觉 | ✅ |
| EntityManager | 13 | BrainComponent / Needs / Capabilities | ✅ |
| FarmManager | — | 农场小游戏后端 | ✅ |
| ShopManager | — | 种子商店交易 | ✅ |
| DialogueManager | 15 | 对话气泡内容驱动 | 🔲 待接入 |
| NetworkManager | — | Mirror 多人联网 | ✅ |
| DanmuManager | 50 | B 站弹幕 | ✅ |
| LiveStatusManager | — | B 站开播状态 | ✅ |

### 12.2 关键文件索引

| 职责 | 文件 |
|---|---|
| 总控入口 | `Scripts/DobeCatGameManager.cs` |
| 桌宠 AI 控制器 | `Scripts/Game/Pet/PetAiController.cs` |
| 透明叠加层 | `Scripts/Sys/Platform/Windows/DesktopOverlay.cs` |
| Win32 P/Invoke | `Scripts/Sys/Platform/Windows/Win32Native.cs` |
| 游戏上下文 | `Scripts/Game/DobeCatGameContext.cs` |
| 系统托盘 | `Scripts/Sys/Platform/Windows/SystemTray.cs` |
| 房间发现 | `Scripts/Sys/Network/RoomDiscoveryClient.cs` |
| 桌宠网络同步 | `Scripts/Game/Pet/PetNetworkSync.cs` |
| 种子商店 UI | `Scripts/Game/Shop/ShopWindow.cs` |
| 农场控制器 | `Scripts/Game/Farm/FarmWorldController.cs` |
| B 站验证 | `Scripts/Sys/Auth/BilibiliAuthValidator.cs` |

### 12.3 已知限制

| 问题 | 影响 | 解决方案 |
|---|---|---|
| Built-in 管线专用 | URP / HDRP 下 `TransparentRenderBlit` 无效 | URP 改用 Renderer Feature |
| 多显示器 DPI 缩放 | `Display.main.systemWidth/Height` 可能不等于实际像素 | `SystemParametersInfo SPI_GETWORKAREA` |
| .NET Framework 依赖 | `System.Windows.Forms` 要求 `.NET Framework` 兼容级别 | 不可改为 .NET Standard 2.1 |
| 全屏游戏遮挡 | D3D 全屏独占模式会覆盖桌宠窗口 | 暂无，M5 阶段处理 |
| 矩形包围盒穿透 | 透明边缘区域无法穿透点击 | 升级为 sprite alpha 像素检测 |

---

## 十三、开发里程碑

| 里程碑 | 核心交付 | 状态 |
|---|---|:---:|
| M0 立项 | 玩法文档 / 美术风格稿 | 🔧 |
| M1 窗口原型 | 透明窗口 + 拖拽 + 走动猫 + 点击穿透 | ✅ |
| M2 行为系统 | Brain Needs + 完整动作集 + 鼠标传感器 | 🔧 |
| M2.5 窗口增强 | OBS 捕捉模式 + 分层显示 + 桌宠缩放设置 | 🔲 |
| M3 互动内容 | 撸猫 / 投喂 / 对话气泡 / 装扮 / 好感度 | 🔲 |
| M3.5 直播经济 | 银币/金币货币体系 + 双商店 + 礼物兑换 | 🔲 |
| M3.6 动态提醒 | 主播动态 / 投稿轮询 + 全员广播 + 气泡提醒 | 🔲 |
| M4 陪伴功能 | 番茄钟 + 久坐提醒 + 整点报时 + 天气 | 🔲 |
| M5 打磨发布 | 设置面板 / 开机自启 / 像素穿透 / 安装包 | 🔲 |

---

## 十四、待决问题

- 是否支持**多只桌宠**同屏（同一进程多个实例 vs. 多进程）？
- 是否做**云存档 / 账号体系**（当前仅本地 + 局域网）？
- 是否开放 **MOD 支持**（用户自定义猫咪皮肤 / 装扮）？
- 是否需要接入**天气 / 公告 API**（需备案或三方 Key）？
- **银币防刷机制**：同 UID 弹幕去重窗口为一场直播还是 24 小时滚动窗口？
- **金币兑换比例**是否随活动动态调整，还是写死常量？
- **分层背景资源**：背景图集中提供还是用户自定义导入？
- **Mac 端**透明窗口替代方案调研（`NSWindow.setOpaque: NO`）？
