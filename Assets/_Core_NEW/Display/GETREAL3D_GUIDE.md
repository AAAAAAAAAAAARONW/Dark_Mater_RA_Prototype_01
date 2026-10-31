# getReal3D —— 上墙的规矩、现状和测试方法

完整的中文说明（机制、改了什么、要做什么、怎么测）：[Vizlab Toolkit 文档](https://claude.ai/artifact/PZJLW5RDrMyGNdx26tR1NX)。这份是放在代码旁边的速查。

**先看这一段。** 墙上出问题（画面挤在中间、几块屏接不上、操作没反应）：

1. **画面全在墙的最底下** → getReal3D 以为观众的眼睛在地板上（第 4.5 节）。用 **Tools > Vizlab > Pin the Viewer's Head in a getReal3D Config** 改 Vizlab 的 `.gr3d`
2. 先确认用的是 **Tools > Vizlab > Build for the Wall (getReal3D)** 出的包，不是 Build Settings 出的 PC 包
3. launcher 的 Unity 版本警告（2019.4.41f2）：先点"仍要打开"，再 Launch；原因和长期办法见第 4 节
4. 跑一遍，把每个 copy 的 `ClusterProbe` 日志拷出来，用 **Tools > Vizlab > Compare Cluster Probe Logs** 看哪一项先对不上（见第 5 节）
5. 报告里写着 "Unity's Time differs between copies while getReal3D's agrees" → 在 launcher 里给游戏加启动参数 `-cluster-clock`，不用重新出包

---

## 1. 墙上到底在跑什么

Vizlab 的墙不是"一个游戏开一个大窗口"。一台机器、4 块 Quadro 显卡，getReal3D launcher 按配置文件（`.gr3d`）启动 **4 个游戏 copy**，每个画墙的一部分：

| copy | 画哪几行 | 窗口大小（按每行 5 × 1920 算） |
|---|---|---|
| 1 | 第 1 行 | 9600 × 1080 |
| 2 | 第 2–3 行 | 9600 × 2160 |
| 3 | 第 4–5 行 | 9600 × 2160 |
| 4 | 第 6–7 行 | 9600 × 2160 |

getReal3D 负责：

| getReal3D 自动同步的 | 不会自动同步的（要我们自己保证） |
|---|---|
| `getReal3D.Input`（手柄、按键，按 logical name 读） | `UnityEngine.Input`：键盘、鼠标、手柄**只有 master 能读到** |
| `Random` 的种子 | 窗口大小：谁调 `Screen.SetResolution` 谁就把自己那块拉坏 |
| 每帧一起换帧（swap lock） | 异步的东西：`LoadSceneAsync`、视频、线程，完成的帧数每个 copy 不一样 |
| `Cluster.time / deltaTime`（master 的时钟） | `Time.time / deltaTime`：**是否共享未确认**，看 probe |
| | 每个 copy 自己的相机投影：`Camera.main`、`WorldToScreenPoint` 的结果每个 copy 都不同 |
| | 写文件：4 个 copy 在同一台机器、同一个桌面、同一个 persistentDataPath |
| | 按窗口算的东西：窗口高度、窗口中心（暗角、镜头畸变）每个 copy 不同 |

**一句话：只要有一个 copy 和别的 copy 算出了不一样的东西，墙就会在接缝处裂开。** 而这种错误从单机上永远看不出来。

## 2. 总原则：不影响 PC

PC（编辑器、桌面 build、playtest）必须和接 getReal3D 之前一模一样。所以所有"墙上专用"的行为都只挂在两个开关上，别的什么都不认：

| 开关 | 什么时候为真 | 管什么 |
|---|---|---|
| `GetReal3DRuntime_NEW.LaunchedByGetReal3D` | 进程是 getReal3D launcher 启动的（环境变量 `GETREAL_CONFIG`） | 窗口、文件、日志、启动、场景加载 |
| 场景里有启用的 `GetReal3DView_NEW` | 只有 `Vizlab_tut`、`Vizlab_Journey` 里有 | 相机、输入、HUD |

PC 场景（`Tutorial.unity`、`Gameplay_Scene_Ana.unity` 等）里没有 `GetReal3DView_NEW`，PC build 也不会被 launcher 启动，所以这两个开关在 PC 上永远是假的。**新写的墙上专用代码，必须挂在这两个开关之一后面。**

场景分工：

| | PC | 墙 |
|---|---|---|
| 教程 | `Tutorial.unity` | `Vizlab_tut.unity` |
| 旅程 | `Gameplay_Scene_Ana.unity` | `Vizlab_Journey.unity` |
| 出包 | File > Build Settings（不变） | **Tools > Vizlab > Build for the Wall (getReal3D)** |

墙的包用自己的 product name（`… Vizlab`），所以它记住的窗口大小、PlayerPrefs、Player.log、persistentDataPath 都和 PC 包分开，PC 包在同一台机器上跑过也不会把墙"带歪"。

## 3. 各部分现状

### 相机

| 什么 | 怎么做的 | 在哪 |
|---|---|---|
| 教程相机 | `FirstPersonLookRig_NEW` 下挂 `[getReal3D]/Eye` | GameObject > Vizlab > Adapt Tutorial Camera to getReal3D |
| 旅程相机 | CinemachineBrain 的相机下挂 `[getReal3D]/Eye`，Cinemachine 照常移动它 | GameObject > Vizlab > Adapt Journey Camera to getReal3D |
| 缩放、dive、各镜头的 FOV | 原相机继续当"镜头"，`GetReal3DLens_NEW` 把 FOV 换算成墙上的放大 | `GetReal3DLens_NEW` |
| 世界里的标签（UIOverlay 层） | Eye 上也挂 `BuiltInFinalUIOverlay`，跟 getReal3D 的投影 | 旅程菜单自动做 |
| "这个东西在不在画面里" | 不用 `Camera.main`（墙上是每个 copy 各不相同的 eye），用 `GetReal3DView_NEW.ScreenCamera` | `QuasarVFX_NEW` |

### 操作

所有操作走 `InputScheme_NEW`；墙上它读 `getReal3D.Input`（`Assets/Resources/mappings.xml`）：

| 按键 | logical name | 用途 |
|---|---|---|
| A / Space | `WandButton` | 确认、回正、发射 |
| B / P | `Jump` | 暂停 |
| Y / Y | `Reset` | 暗物质开关 |
| X / Left Shift | `ChangeWand` | 显示旅程 tracker HUD |
| 右摇杆 | `Yaw` / `Pitch` | 看 |
| 左摇杆 Y | `Forward` | 缩放 |

墙上**不响应**的（只有 master 能读到，响应了就会只动一块屏）：鼠标、D-pad、`VizlabRowShifter_NEW` 的 LB/RB。调试键（数字键跳层、F 键）也只有 master 能收到 —— **现场不要按**，只在单机上用。

### UI

| 什么 | 墙上 |
|---|---|
| Screen Space Overlay 的 HUD | `GetReal3DHud_NEW` 把它画到七行屏上，每个 copy 画自己那部分 |
| 轨迹上的箭头（教程、旅程） | 用 `GetReal3DHud_NEW.TryWorldToCanvas`，按视线和屏幕的交点放 |
| IMGUI 调试面板 | 墙上启动时默认隐藏（`DebugView_NEW.Overlay = false`）；F1 只在 master 那块屏上显示 |

### 窗口和启动

| 什么 | 墙上 |
|---|---|
| `VizlabWallTest_NEW` | **不再改分辨率**，只记日志。之前它在每个 copy 里把整面墙的 1.27:1 塞进那个 copy 的窗口 → 画面挤在中间、两边黑边 |
| 教程 → 旅程 | 不用 `LoadSceneAsync`，在黑屏下同步 `LoadScene`；等待和淡入淡出按 master 的帧时间计，所有 copy 在同一帧切场景 |
| `Vizlab_tut` 的 hand-off 目标 | `Vizlab_Journey`（之前错指到 PC 的 `Gameplay_Scene_Ana`） |
| 调试 CSV（`BuildDebugOverlay`、`SessionInputMetricsLogger`） | 文件名带 `_master_<pid>` / `_node_<pid>`，4 个 copy 不再写同一个文件 |
| 粒子的 Auto Random Seed（Vizlab 两个场景里共 75 个） | `ClusterParticleSeeds_NEW`：场景加载时按层级路径给固定种子，每个 copy 一样（星系、尘埃在各屏排布一致） |
| 调试跳转（数字键跳层、教程跳 phase、跳 beat） | 墙上关掉：只有 master 收得到键盘，跳了就永远对不上 |
| 暗角、色差、镜头畸变（主 profile 的 Vignette，dive 时的三种） | `ClusterScreenEffects_NEW` 墙上关掉：每个 copy 都按自己窗口的中心画，会在第 1/2、3/4、5/6 行交界处出现暗带，dive 的"漏斗"重复 4 次。Bloom、运动模糊、调色不受影响 |
| 世界按屏幕上的像素大小淡入（`WorldSwitcher_NEW`） | 墙上按固定 1080 px 算：第 1 行的 copy 高 1080、其他 2160，否则各 copy 在不同帧淡入 |

### 还没处理、取决于 probe 结果的风险

2026-10 对两个 Vizlab 场景用到的全部脚本扫了一遍"各 copy 会算出不同结果"的地方。最大的一类是**时钟**：几乎所有东西都按 Unity 的 `Time` 走 —— 光点飞行（`PlayerRig_NEW`、`LayerPace_NEW`）、相机（`FirstPersonLookRig_NEW`、`CameraDirector_NEW`、两个 Zoom）、教程整个世界时钟（`TutorialClock_NEW`，用的是 unscaled time 的绝对值）、相机抖动和漂浮的相位（`TutorialCameraShake_NEW`、`GuideMote_NEW`）、约 20 个 shader 的 `_Time`。

**getReal3D 是否让每个 copy 的 `Time` 一致，现在没法从代码上确定**（插件 DLL 只读 `Time`，但它的配置里有"interceptors"，可能在底层接管了计时）。所以分两种情况：

| probe 报告 | 意味着 | 做法 |
|---|---|---|
| `Time.time` agrees | getReal3D 共享了时钟，上面这一大类都没问题 | 不用动 |
| `Time.time` 对不上，`Cluster.time` agrees | 每个 copy 自己计时 | ① 加 `-cluster-clock`（每帧按 master 的帧时间走，解决"越走越偏"）；② 若还剩一个固定的差（启动差了几秒造成的），说明需要"cluster 组好后所有 copy 同一帧重载第一个场景"，告诉开发 |
| 连 `Cluster.time` 都对不上 | 根本没组成一个 cluster | 先解决启动（第 4 节） |

其他扫出来、和时钟无关的：

| 风险 | 在哪 | 影响 | 状态 |
|---|---|---|---|
| 配音结束才推进流程 | `TutorialVoiceOver_NEW`、`JourneyVoiceOver_NEW` → `DarkMatterMoment_NEW` | 推进取决于每个 copy 自己的音频时钟；不出声的 copy 上 `AudioSource` 是否照常走未确认 | 第二层测试里看 probe 的 layer 列 |
| 地球视频 | `EarthCutscene_NEW`；`EarthVideoCanvas` 横跨好几行 | 每个 copy 各自解码，帧可能错开，接缝处看得出 | 未改 |
| 开发 build 里的调试键 | `JourneySequence_NEW`、`DarkMatterMoment_NEW`、`BakedSpectrumSource_NEW` 等 | 只在 DEVELOPMENT_BUILD 里有；墙的 build 是 release，不含 | 墙上不出开发 build |

## 4. 启动弹窗和"挤到中间、接不上"

**"挤到中间"的主因不是弹窗，是我们自己的 `VizlabWallTest_NEW`**（已修）：它在每个 copy 里把整面墙 1.27:1 的比例塞进那个 copy 的窗口。copy 的窗口是 9600 × 1080 或 9600 × 2160，于是每个 copy 只在中间画一块约 2743 px 宽的画面，两边黑。

**弹窗**是 getReal3D launcher 的 Unity 版本检查：项目用的是 2019.4.41**f2**，不在它的支持列表里。Chris（8/25 会议）和 LARC 会议都确认过：f2 会报这个警告，点掉照样能跑。按 8/25 会议记录，f2 是带安全更新的版本，f1 是 getReal3D 认的那一版。

它有没有造成"接不上"取决于弹窗在什么时候出现：

| 如果 | 那么 |
|---|---|
| launcher 只在启动前弹一次，点掉后 4 个 copy 一起起 | 弹窗本身不造成 copy 之间的延迟 |
| 每个 copy 各弹一次，要一个个点 | copy 依次启动，相差好几秒。getReal3D 在启动后 `injectionDelay`（样例 1000 ms）接管窗口、接入 cluster，晚的那个可能错过 |

probe 报告的 STARTUP 一段直接给出 4 个 copy 进程启动相差几秒：一看就知道是哪种。

办法，从稳到快：
1. **用 Unity 2019.4.41f1 出墙的包**（只换出包用的编辑器版本，项目不用改）→ launcher 不再弹。代价是没有 f2 的安全修补；Vizlab 那台机器不对外，风险可接受，但要和实验室说一声
2. 问实验室 launcher 能不能对这个游戏关掉版本检查
3. 继续点"仍要打开"：确认只弹一次、且点完才一起启动；如果是一个个弹，在 `.gr3d` 里把 `<injectionDelay>` 调大（毫秒）

## 4.5 画面全在墙的最底下

getReal3D 按"观众眼睛在哪"给每块屏算投影，眼睛的位置来自配置里的 **Head sensor**。没有头部追踪在跑、配置里也没把头固定住时，它读默认值：**地板，(0, 0, 0)**。getReal3D 自带的三个样例配置全都是这样，它的文档也写着 sensor 默认在地板上。

我们的相机永远把光点和跟随的东西放在画面正中，也就是"眼睛的正前方"。按 Vizlab 的几何（`VizlabDisplay_NEW`）：

| 眼睛的高度 | 正前方落在 |
|---|---|
| 地板 0 m | 第 7 行**下沿以下** → 所有东西挤在最底下 |
| 曲面圆心 1.24 m（设计点） | 第 5 行下部 |
| 站立 1.6 m | 第 5 行中间 |

单机上看不出来：编辑器的样例屏幕中心就在高度 0，正好和"地板上的眼睛"平齐。

**改法在配置里，不在游戏里**（getReal3D 没有从脚本移动头部的接口）：**Tools > Vizlab > Pin the Viewer's Head in a getReal3D Config**，选 Vizlab 的 `.gr3d`：

1. 它先在 Console 里列出配置里的每块屏、现在的头在哪、正前方落在哪块屏的百分之几高度 —— **不写任何东西就能确认是不是这个原因**
2. 选"曲面圆心 1.24 m"或"站立 1.6 m"，另存一份把 Head 固定（static）在那里的配置
3. launcher 改用这份配置

注意：
- 前提是配置的坐标和 `VizlabDisplay_NEW` 一样（毫米、原点在地面、墙在 -Z 方向）。Console 里列出的屏幕中心应该接近 `VizlabDisplay_NEW.Rows`，对不上就先别用
- 如果 Vizlab 真的有观众戴的头部追踪，就不是改配置，而是让追踪跑起来 —— 问实验室
- 游戏启动时如果眼睛低于 0.5 m，Player.log 里会有 `[GetReal3DView_NEW] getReal3D puts the viewer's eyes … above the floor` 的警告

## 5. 测试环境：不上墙怎么测

三层，从便宜到真实：

### 第一层：编辑器里（不需要 launcher）

打开 `Vizlab_tut` / `Vizlab_Journey` 直接 Play。getReal3D 用 `editor_config.gr3d`（一块 53 cm 的屏）出图，输入走 `getReal3D.Input`，能测：相机挂得对不对、缩放、操作映射。看墙上 HUD 的七行排布：把 `[getReal3D]` 上 `GetReal3DHud_NEW` 的 Mode 设成 Always。

测不了：多个 copy、启动、同步。

### 第二层：一台电脑上跑完整的 cluster（真正的模拟环境）

需要：一台 Windows 电脑装 getReal3D for Unity（developer 安装：plugin + launcher + daemon + developer license）。

1. 从 Vizlab 那台机器上拷出 launcher 用的 `.gr3d` 配置（建议也放一份进仓库，大家用同一份）
2. **Tools > Vizlab > Make Local Test Copy of a getReal3D Config**，选这个文件 → 生成 `xxx_local.gr3d`：所有电脑改成 localhost，窗口按同一个比例缩小摆在一个显示器上，去掉全屏；屏幕、投影、输入都和墙上一模一样
3. **Tools > Vizlab > Build for the Wall (getReal3D)** 出包
4. 在 launcher 里用 `xxx_local.gr3d` 启动这个包 → 本机会起 4 个 copy，就是墙上的那 4 个，只是窗口小

这一层能复现墙上的启动过程（包括弹窗造成的延迟）、窗口摆放、接缝和同步。getReal3D 自带的 `cave_config.gr3d`（"CAVE simulator"：一台 localhost、4 个窗口）就是同一个思路。

### 第三层：用日志判断同步

`ClusterSyncProbe_NEW` 只在 launcher 启动时（或命令行加 `-cluster-probe`）自动出现，每个 copy 写一个 CSV 到 `persistentDataPath/ClusterProbe`（Windows：`%USERPROFILE%\AppData\LocalLow\DefaultCompany\<product> Vizlab\ClusterProbe`）。记录：

- 启动：进程什么时候起来、多久到第一帧
- 每 30 个 cluster 帧：Unity 的时钟、getReal3D 的时钟、光点位置、相机位置/朝向/FOV、当前层、场景、Random 状态、窗口大小

把一次运行的所有 CSV 放进一个文件夹，**Tools > Vizlab > Compare Cluster Probe Logs** → 报告写到 Console 和 `probe_report.txt`：哪一项、从哪一帧开始对不上，以及这意味着什么。

## 6. 现场检查表

- [ ] 用 Build for the Wall 出包，检查没有报错（它会拦住没跑过 Adapt 菜单的场景）
- [ ] launcher 的版本警告只出现一次，点完后 4 个 copy 一起启动（probe STARTUP 相差 < 1 s）
- [ ] 每个 copy 的 Player.log / probe 里有 `launched by getReal3D` 和 `the window is left as getReal3D placed it`
- [ ] 光点在墙的中部（第 5 行附近），不在最底下；Player.log 里没有 "eyes … above the floor" 的警告
- [ ] 画面横跨所有屏，接缝处连续，没有黑边，第 1/2、3/4、5/6 行交界没有暗带
- [ ] Compare Cluster Probe Logs：STARTUP 各 copy 相差 < 1 s，FRAME BY FRAME 全部 agrees
- [ ] 如果 Time 那一项对不上 → 加 `-cluster-clock` 再测
- [ ] 画面不动（卡住）→ CinemachineBrain 在 Main Camera 被关掉后没有继续更新它，告诉开发
- [ ] A/B/Y/X、摇杆在每块屏上的反应一致

## 7. 还没确认的

| 问题 | 怎么确认 |
|---|---|
| getReal3D 是否同步 Unity 的 `Time` | probe 报告 |
| Vizlab 配置里 Head 是不是在地板上 | Pin the Viewer's Head 菜单的 Console 输出（不保存也会列出） |
| 4 个 copy 的窗口实际像素大小 | Vizlab 的 `.gr3d`，或 probe 的 WINDOWS 一段 |
| launcher 的版本警告是弹一次还是每个 copy 一次 | 现场看；probe STARTUP |
| `getReal3D_Settings` 的 `autoInitVR` 在 PC build 里做了什么 | 在**没装 getReal3D 的电脑**上跑一次 PC build，看有没有报错或弹窗 |
| 同一台机器上 4 个 copy 抢同一个 Player.log | launcher 是否给每个 copy 传了 `-logFile`；probe 文件本身是分开的 |
| 视频（`EarthVideoCanvas`）在各 copy 上是否同帧 | 第二层测试里看接缝 |
