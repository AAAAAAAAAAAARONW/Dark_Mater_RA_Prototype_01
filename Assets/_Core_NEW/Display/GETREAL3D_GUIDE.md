# getReal3D —— 上墙的规矩、现状和测试方法

**先看这一段。** 墙上出问题（画面挤在中间、几块屏接不上、操作没反应）：

1. 先确认用的是 **Tools > Vizlab > Build for the Wall (getReal3D)** 出的包，不是 Build Settings 出的 PC 包
2. 启动时**不能有任何弹窗**卡住其中一个 copy（见第 4 节）
3. 跑一遍，把每个 copy 的 `ClusterProbe` 日志拷出来，用 **Tools > Vizlab > Compare Cluster Probe Logs** 看哪一项先对不上（见第 5 节）
4. 报告里写着 "Unity's Time differs between copies while getReal3D's agrees" → 在 launcher 里给游戏加启动参数 `-cluster-clock`，不用重新出包

---

## 1. 墙上到底在跑什么

Vizlab 的墙不是"一个游戏开一个大窗口"。getReal3D launcher 按配置文件（`.gr3d`）给**每个窗口启动一个游戏 copy**：我们现场看到的是 3 个 instance。每个 copy 只画墙的一部分，getReal3D 负责：

| getReal3D 自动同步的 | 不会自动同步的（要我们自己保证） |
|---|---|
| `getReal3D.Input`（手柄、按键，按 logical name 读） | `UnityEngine.Input`：键盘、鼠标、手柄**只有 master 能读到** |
| `Random` 的种子 | 窗口大小：谁调 `Screen.SetResolution` 谁就把自己那块拉坏 |
| 每帧一起换帧（swap lock） | 异步的东西：`LoadSceneAsync`、视频、线程，完成的帧数每个 copy 不一样 |
| `Cluster.time / deltaTime`（master 的时钟） | `Time.time / deltaTime`：**是否共享未确认**，看 probe |
| | 每个 copy 自己的相机投影：`Camera.main`、`WorldToScreenPoint` 的结果每个 copy 都不同 |
| | 写文件：3 个 copy 在同一台机器、同一个桌面、同一个 persistentDataPath |

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
| 调试 CSV（`BuildDebugOverlay`、`SessionInputMetricsLogger`） | 文件名带 `_master_<pid>` / `_node_<pid>`，3 个 copy 不再写同一个文件 |
| 粒子的 Auto Random Seed（Vizlab 两个场景里共 75 个） | `ClusterParticleSeeds_NEW`：场景加载时按层级路径给固定种子，每个 copy 一样（星系、尘埃在各屏排布一致） |
| 调试跳转（数字键跳层、教程跳 phase、跳 beat） | 墙上关掉：只有 master 收得到键盘，跳了就永远对不上 |

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
| dive 时镜头畸变/暗角的中心按视口算 | `LayerDive_NEW` 设 LensDistortion / Vignette 的 center | 每个窗口各自以自己的中心畸变，"漏斗"在每块屏重复一次，而不是整面墙一个 | 未改；墙上可考虑关掉这两个效果 |
| 世界缩放淡出按像素高度 | `WorldSwitcher_NEW`（`pixelHeight`） | 只有各 copy 窗口高度一样时才一致 | 未改；看 Vizlab 配置 |
| 配音结束才推进流程 | `TutorialVoiceOver_NEW`、`JourneyVoiceOver_NEW` → `DarkMatterMoment_NEW` | 推进取决于每个 copy 自己的音频时钟；不出声的 copy 上 `AudioSource` 是否照常走未确认 | 第二层测试里看 probe 的 layer 列 |
| 地球视频 | `EarthCutscene_NEW`；`EarthVideoCanvas` 横跨好几行 | 每个 copy 各自解码，帧可能错开，接缝处看得出 | 未改 |
| 开发 build 里的调试键 | `JourneySequence_NEW`、`DarkMatterMoment_NEW`、`BakedSpectrumSource_NEW` 等 | 只在 DEVELOPMENT_BUILD 里有；墙的 build 是 release，不含 | 墙上不出开发 build |

## 4. 启动弹窗和"挤到中间、接不上"

getReal3D 启动一个 copy 后，会等配置里的 `injectionDelay`（样例配置是 1000 ms）再接管那个窗口：摆位置、定大小、接入 cluster。**如果某个 copy 那时被弹窗卡住**，它的窗口要么还不存在、要么还不是最终的那个，接管就会落空或落错；同时它比别的 copy 晚很多才进入第一帧。结果正是：

- 窗口大小/位置不对 → 画面挤在中间、比例不对
- 这个 copy 晚于其他 copy 开始 → 各屏的内容对不上

我们自己的游戏在启动时**没有**任何会等待用户点击的弹窗（Unity 2019.4 已经没有分辨率对话框了）。所以弹窗多半来自游戏外面，按出现的概率：

| 弹窗 | 特征 | 处理 |
|---|---|---|
| **Windows 防火墙**（"Windows Defender Firewall has blocked some features of this app"） | 游戏第一次开网络端口时出现；getReal3D 每次部署到新路径（launcher 的 CACHE 目录）都可能再出现 | 在 Vizlab 机器上给游戏 exe（或整个 CACHE 目录）加一条入站规则；或点一次"允许"后不要换部署路径 |
| **Windows SmartScreen**（"Windows protected your PC"） | 从网上/U 盘拷来的 exe 带"来自网络"标记 | 右键 exe → 属性 → 解除锁定（Unblock），或打包前在本机解压 |
| getReal3D / license 相关 | 标题里有 getReal3D / FlexLM | 交给实验室管理员 |
| Unity splash | 不是弹窗，但每个 copy 多花几秒；Personal 版关不掉 | 墙的 build 在许可允许时会关掉它 |

**要确认是哪一个：截图那个弹窗。** 另外 probe 的报告第一段（STARTUP）会直接给出每个 copy 进程启动差了几秒、各自用了多久才到第一帧：被弹窗卡住的那个一眼就能看出来。

如果确实没法消掉弹窗，可以在 Vizlab 的 `.gr3d` 里把 `<injectionDelay>` 调大（单位毫秒），给窗口出现留时间 —— 但这是绕开，不是解决。

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
4. 在 launcher 里用 `xxx_local.gr3d` 启动这个包 → 本机会起 3 个 copy，就是墙上的那 3 个，只是窗口小

这一层能复现墙上的启动过程（包括弹窗造成的延迟）、窗口摆放、接缝和同步。getReal3D 自带的 `cave_config.gr3d`（"CAVE simulator"：一台 localhost、4 个窗口）就是同一个思路。

### 第三层：用日志判断同步

`ClusterSyncProbe_NEW` 只在 launcher 启动时（或命令行加 `-cluster-probe`）自动出现，每个 copy 写一个 CSV 到 `persistentDataPath/ClusterProbe`（Windows：`%USERPROFILE%\AppData\LocalLow\DefaultCompany\<product> Vizlab\ClusterProbe`）。记录：

- 启动：进程什么时候起来、多久到第一帧
- 每 30 个 cluster 帧：Unity 的时钟、getReal3D 的时钟、光点位置、相机位置/朝向/FOV、当前层、场景、Random 状态、窗口大小

把一次运行的所有 CSV 放进一个文件夹，**Tools > Vizlab > Compare Cluster Probe Logs** → 报告写到 Console 和 `probe_report.txt`：哪一项、从哪一帧开始对不上，以及这意味着什么。

## 6. 现场检查表

- [ ] 用 Build for the Wall 出包，检查没有报错（它会拦住没跑过 Adapt 菜单的场景）
- [ ] 启动时 3 个 copy 都没有弹窗
- [ ] 每个 copy 的 Player.log / probe 里有 `launched by getReal3D` 和 `the window is left as getReal3D placed it`
- [ ] 画面横跨所有屏，接缝处连续，没有黑边
- [ ] Compare Cluster Probe Logs：STARTUP 各 copy 相差 < 1 s，FRAME BY FRAME 全部 agrees
- [ ] 如果 Time 那一项对不上 → 加 `-cluster-clock` 再测
- [ ] 画面不动（卡住）→ CinemachineBrain 在 Main Camera 被关掉后没有继续更新它，告诉开发
- [ ] A/B/Y/X、摇杆在每块屏上的反应一致

## 7. 还没确认的

| 问题 | 怎么确认 |
|---|---|
| getReal3D 是否同步 Unity 的 `Time` | probe 报告 |
| Vizlab 的 3 个 copy 各画哪几行 | Vizlab 的 `.gr3d` |
| `getReal3D_Settings` 的 `autoInitVR` 在 PC build 里做了什么 | 在**没装 getReal3D 的电脑**上跑一次 PC build，看有没有报错或弹窗 |
| 同一台机器上 3 个 copy 抢同一个 Player.log | launcher 是否给每个 copy 传了 `-logFile`；probe 文件本身是分开的 |
| 视频（`EarthVideoCanvas`）在各 copy 上是否同帧 | 第二层测试里看接缝 |
