# Tutorial — Journey of Light

实现 [Tutorial GDD — Journey of Light](https://bird-tune-e6c.notion.site/Tutorial-GDD-Journey-of-Light-3d3abeb8074b817b8f93e22777f5888f)。

**本轮范围：Phase 0（A1–A3）+ Phase 1（B1–B4），加上它们前面的 attract 状态。**
Phase 2–4 尚未实现，但拍子框架、控制方案和 HUD 都是按整套 20 帧设计的 —— 见文末「下一步」。

---

## 五分钟跑起来

1. 打开 `Assets/_Core_NEW/Tutorial.unity`
2. **在 Lighting 面板给场景 assign 天空盒** —— builder 不碰这个
3. 菜单 `Tools > Journey NEW > Build Tutorial Scene (Phase 0-1)`
4. Play

看到的顺序：纯黑 → 标题卡 →（按 A）→ 黑保持一会儿后渐显出天空，玩家匀速朝远处的类星体直线飞行，`LEFT STICK · LOOK` 提示已经在了 → 第 15 秒一颗光点从右边缘飘出，转右抓住它 → 抬头抓第二颗 → 转身抓第三颗 → A 图标 + `RECENTRE` 出现，按 A 视角平滑回到航向。

调试：`F2` 跳过当前拍子。左上角三行 —— 拍子与门控状态、相机状态（是否在回正、偏离航向多少度、**这一帧有没有收到 A**、摇杆推了多少）、航行状态（速度、已飞距离、距类星体、**航向偏差**）。全部受 `DebugView_NEW.Overlay` 控制（`Tools > Journey NEW > Debug View`），出包时一起关掉。

那三行是为了让「A 没反应」「是不是没往前飞」这类问题**看一眼就能定位**，而不是靠打断点。

**再跑一次不需要重开场景** —— 90 秒无输入自动回到标题卡，光点归位，HUD 清空，视角回正。这是展陈现场唯一重要的行为：下一位观众看到的必须是开头，而不是上一位停在半路的画面。

---

## 目录

```
Tutorial/
├─ TutorialInput_NEW          控制方案的唯一出处（GDD §4）
├─ FirstPersonLookRig_NEW     第一人称视角 + A 回正
├─ TutorialBeat_NEW           拍子基类：一个动作一个门
├─ TutorialDirector_NEW       按顺序跑拍子 + 90 秒空闲返回
├─ GuideMote_NEW              引导光点：漂移、趋近变亮、抓住绽放
├─ TutorialHUD_NEW            图例 / 准星 / A 提示，以及它们何时不在
├─ TutorialAttract_NEW        标题卡与整体重置
├─ TutorialSky_NEW            把 NP_Quasar 写进 Custom/Nebula 天空盒（**builder 不挂**，按需自己加）
├─ TutorialFadeIn_NEW         开场纯黑 → 渐显，时长与曲线全部暴露
├─ TutorialTravel_NEW         匀速直线飞向类星体，不接受任何输入
├─ Beats/
│   ├─ Beat_Cinematic_NEW     A1 A2 A3（无动作，按分镜时长走）
│   ├─ Beat_LookAt_NEW        B1 B2（把目标带进准星）
│   ├─ Beat_TurnAround_NEW    B3（转过 150° 且目标在画面里）
│   └─ Beat_Confirm_NEW       B4（按 A）
└─ Editor/
    ├─ TutorialSceneBuilder_NEW   一键搭场景（布局）
    └─ TutorialWorldAssets_NEW    资产查找与生成（取材）
```

运行时依赖：引擎、UGUI、`_Core_NEW` 的 `DebugView_NEW` / `HierarchyBadge_NEW` / `NebulaProfile_NEW`，以及 **`Assets/Scripts/PhotonSpectrumTrail.cs`**（旧脚本，光子拖尾的光谱生成，刻意复用而不是重写）。**不依赖 Cinemachine** —— 教程是第一人称，不走 FreeLook。

---

## 场景里看得见的东西是从哪来的

**天空盒不归 builder 管** —— `RenderSettings` 一行都不写，自己在 Lighting 面板里 assign。其余屏幕上的每一样东西都来自工程里已有的资产：

| 元素 | 资产 | 负责哪几帧 |
|---|---|---|
| 后处理 | `Scenes/Test Level_Profiles/Main Camera Profile.asset` | 全程。和 PlaytestBuild **共用同一份 profile**，不会分家 |
| 类星体 | `Materials/BlazingQuasar.mat` | 远处的目标。A1 的暗斑、**B2 的 jet channel** 都是这一个物体 |
| 光子拖尾 | `Shaders/Custom_PhotonTrail.mat` + `Scripts/PhotonSpectrumTrail.cs` | **Phase 0–1 关闭**，Phase 2 发射之后才亮 |
| 星际尘埃 | 生成的 `TutorialDiscDust.mat` + `TutorialSoftDot.png` | A1「缓慢旋转的暗红物质」，同时提供飞行的速度感 |
| 光点 | 生成的 `TutorialMote.mat` | A3 B1 B2 B3 |

`BlazingQuasar.shader` 值得单独说一句：它的 Properties 里直接有 `_AccretionDisk`、`_JetColor / _JetWidth / _JetLength`、`_SpinSpeed`、`_CoreRadius`，**吸积盘、双极喷流、核心亮斑在一个 shader 里**。喷流沿物体局部 Y 轴，所以类星体保持直立，B2「抬头看到喷流通道」就是字面发生的事。

### 后处理：和 PlaytestBuild 同一套接法

三样东西，位置和 PlaytestBuild 的 Main Camera 一模一样：相机放在 `PostProcessing` 层（11）、`PostProcessLayer` 的 volume mask 指向同一层、`PostProcessVolume` 挂在相机自己身上且 `isGlobal`。抗锯齿用 FXAA，对齐 PlaytestBuild 的 `antialiasingMode: 1`，也是三种里最便宜的。

**profile 是共享的，不是复制的。** bloom 和调色是类星体读起来「亮」而不是「灰」的主要原因，教程要是带自己的一份 grade，就会和主旅程慢慢分家，而且要等到两个画面并排看才会发现。

### 一个资产安全坑

**拖尾材质用的是副本，不是共享资产。** `PhotonSpectrumTrail` 是 `[ExecuteAlways]`，编辑期就往 `sharedMaterial` 写生成的光谱贴图 —— 直接挂 `Custom_PhotonTrail.mat` 的话，光是**打开场景**就会改写主旅程在用的材质。builder 复制出 `TutorialPhotonTrail.mat` 再挂。PlaytestBuild 用的是场景内嵌实例，同一个规避方式。

（`NebulaResponder_NEW` 的注释记着同类的教训：曾经直接写 `RenderSettings.skybox`，那是磁盘上的 .mat，一次 playtest 永久改掉了材质、场景不再可复现。`TutorialSky_NEW` 用的是克隆再写的做法，但 builder 现在不挂它 —— 天空盒归你。）

---

## 开场渐显

`TutorialFadeIn_NEW`：一张铺满屏幕的黑图，压在世界之上、其余 HUD 之下（所以标题卡是黑底白字，而不是被黑图盖住）。

时序全部暴露在 Inspector：

| 字段 | 作用 |
|---|---|
| `startBeatId` | 从哪一拍开始，默认 `A1` —— 所以纯黑一直持续到标题卡按下 A |
| `holdSeconds` | 纯黑保持多久 |
| `fadeSeconds` | 渐显多久 |
| `curve` | 渐显的形状，默认缓入缓出 |
| `onFadeStarted` / `onFadeComplete` | 给环境音床和第一句 VO 用 —— 它们要对着画面落，不是对着秒表 |

黑图全透明之后会把 `Image` 组件关掉：一张全屏透明图在 Carnegie 的曲面屏上仍然是一整屏的 overdraw。

---

## HUD

第一版是白色 Arial 浮在画面上，那是调试读数不是界面。现在：

| 元素 | 长什么样 |
|---|---|
| 控制提示 | 深色圆角底板（9-slice）+ 一行大字。**底板是 HUD 和调试读数的分界线** —— 纯白字在明亮的类星体和星场前会消失，深底板在房间后排也读得住 |
| A 提示 | 圆形按键图标里写 A，右边跟动词。做成圆盘而不是光秃秃一个字母，因为字母会被读成文字，而文字看起来不能按 |
| 准星 | 圆环，不是方块。方块在暗屏中央会被读成坏点；圆环读作瞄准框，而且光点能留在环内 —— B1 的门控正是「光点在环内」 |
| 图例 | 小字、低透明度。它要在整段体验里一直挂着，不能和当下的指令抢注意力 |

底板、圆环、圆盘三张贴图是 builder 生成的（`TutorialChip.png` 九宫格、`TutorialRing.png`、`TutorialDisc.png`），纯几何单色。美术直接替换 PNG，builder 一行都不用动。

**文案的归属没变**：形状和位置属于 HUD，词属于拍子。GDD §5 要求「继续」这个操作在每一处都是同样的形状、同样的位置，所以 `Beat_Confirm_NEW.promptText` 现在只写动词（`RECENTRE`），A 那个图标是 HUD 画的。

---

## 相机手感：数值全部抄自 PlaytestBuild

不是我选的，是从 `PlaytestBuild_NEW.unity` 的 Player 上逐个抄下来的 —— 教程要交接给一个手感一致的主体验。

| | PlaytestBuild | 教程 | 说明 |
|---|---|---|---|
| 水平灵敏度 | `xSensitivity: 100` | 100 °/s | 直接照抄 |
| **垂直灵敏度** | `ySensitivity: 0.2` | **26 °/s** | 需要换算，见下 |
| 死区 | `stickDeadband: 0.1` | 0.1 | 直接照抄 |
| 回正水平 | `resetXSpeed: 50` | 50 °/s | `MoveTowardsAngle` 匀速，不是定时缓动 |
| 回正垂直 | `resetYSpeed: 1.5` | 195 °/s | 同样换算 |
| FOV | FreeLook `FieldOfView: 40` | 40 | Unity 默认 60 会像另一个游戏 |

**垂直那一格是唯一需要动脑的地方。** PlaytestBuild 驱动的是 Cinemachine FreeLook，`m_YAxis.Value` 是 0→1 的归一化轴，跨越整个 orbit 弧。那个 rig 的 orbits 是 height 3 / 1 / −2、radius 1 / 3 / 1，大致 130° 的弧，所以 0.2 轴单位/秒 ≈ **26 °/s**。把 0.2 当成 0.2 °/s 抄过来会完全不能用；随手拍一个数字，就是教程和游戏手感对不上的经典来源。

PlaytestBuild 的柔和感来自 Cinemachine 的 damping，第一人称没有对应物，所以 `smoothTime`（默认 0.06s）把它显式加回来。设成 0 就是原始输入。

两根摇杆都读。分镜的 B1 提示写的是 `LEFT STICK · LOOK`，而工程里 `RightStickX/Y` 是手柄轴 4/5（右摇杆）、`Horizontal/Vertical` 是轴 1/2（左摇杆）。GDD §4 只说「只有一根摇杆，它负责看」，没说是哪根。两根都读意味着展陈现场的手柄不管驱动怎么报都能用，观众抓错摇杆也不会被惩罚。`Horizontal/Vertical` 同时带 WASD，所以在桌面上也能测。

---

## 为什么玩家一直在动

`TutorialTravel_NEW` 让玩家以恒定速度直线飞向类星体。**不接受任何输入** —— 没有改速度或改航向的公开方法，也没有一个「允许玩家控制」的开关等着以后被打开。GDD §3 把「你不能操纵方向，只能看」列为教程要装进玩家脑子里的第二件事，§4 直接把移动摇杆删掉，让这条靠控制的缺席来执行，而不是靠一行文字。

它同时换来三样东西：

- **速度感。** 尘埃粒子在世界空间模拟、发射器挂在玩家身上 —— 新粒子一直在玩家周围生成所以不会用完，已生成的留在世界里以航速掠过。两半缺一不可：世界空间 + 固定发射器六秒就被甩掉，本地空间则是一团跟着你走、看起来完全静止的云。
- **A3 按原文成立。** 「光点从右边缘飘出画面」需要玩家和光点之间有相对运动。
- **拖尾以后画得出来。** `TrailRenderer` 不动就不吐顶点。Phase 2 发射之后才打开拖尾，那时它需要有位移可拖。

光点是**挂在玩家身上**的。25 单位/秒的航速下，钉在世界空间的光点四秒就被甩到身后，B1 那条门就永远过不去了 —— 分镜说的「缓慢飘出画面右缘」是相对于光的运动，不是相对于宇宙的。

---

## 三条设计约束，写进了结构里

### 1 · 「不靠计时器推进」的准确范围

GDD §5 说 nothing advances on a timer，但 Phase 0 的每一帧都写着时长（A1 是 0:00–0:08）。这两句不矛盾，规则的范围比字面窄：

> **绝不用计时器代替玩家的动作。**

所以 `AdvanceMode` 是两个显式的枚举值：

| 模式 | 用在 | 时间能否推进 |
|---|---|---|
| `Duration` | 不要求玩家做任何事的帧（A1–A3，将来 C1 C3 D1–D4） | 能，这是唯一的用途 |
| `PlayerAction` | 有门控的帧（B1–B4） | **不能**，连超时字段都没有 |

`PlayerAction` 模式下 `duration` 字段被完全忽略，Inspector 里也加不进一个超时。规则不靠人记住。

### 2 · 相机永远不锁

`FirstPersonLookRig_NEW` **没有 SetLocked 方法**，是故意的。

GDD §2 记录了旧版 C3 锁相机被多名玩家读成 bug，§5 把「相机永不锁定，玩家视角输入在每次转场中保持有效，包括发射」列为逐拍适用的规则。一个没有锁的类无法退化出一个锁。

由此派生的一条：B4 的回正是可以被打断的。推摇杆就取消 lerp —— 一个跟摇杆对抗的回正就是换了件外套的锁。

### 3 · 每个 UI 元素由具名拍子打开

`TutorialHUD_NEW` 里每个元素的默认状态都是关：

| 顺序 | 元素 | 首次出现 | 之后 |
|---|---|---|---|
| 1 | 控制提示行 | **A1** | 分镜的 B2 B3 都写 No new prompt，所以空的 `hintText` 表示「不变」而不是「清空」 |
| 2 | 控制图例 | B1 | **永不消失**，代码里没有隐藏它的路径 |
| 3 | 准星 | B1 | 跟图例一起 |
| 4 | A 提示 | B4 | 按下即隐 |

提示行放在 A1 是**有意偏离 GDD §7** 的：文档把第一个提示放在 B1、要求 B1 之前屏幕全空。走上来的观众应该先被告知「你可以转视角」，再被要求去转 —— 这是你定的顺序，不是我的解读。

图例文案取自 GDD §4，一字不改：

```
STICK = LOOK      A = CONFIRM / RECENTRE
```

准星不在 GDD §7 的 UI 清单里，因为它不是玩家需要学的界面元素。但 B1 的门控是「光点保持在准星内」，准星不可见这条门就没法玩，所以它跟图例一起到场。

---

## 拍子与门控对照

| Frame | 组件 | 门控 | 实现要点 |
|---|---|---|---|
| A1 | `Beat_Cinematic_NEW` | 无，8 秒 | 提示 `LEFT STICK · LOOK` 在这里出现；渐显也在这里开始；`onEnter` 挂 VO |
| A2 | `Beat_Cinematic_NEW` | 无，7 秒 | `onHalfway` 用于「亮度升起来」这种帧内变化 |
| A3 | `Beat_Cinematic_NEW` | 无，5 秒 | `onEnter` 激活光点；`onRumble` 见下方「已知缺口」 |
| B1 | `Beat_LookAt_NEW` | 光点进入准星 | `holdSeconds = 0`。GDD 明写 not a dwell timer |
| B2 | `Beat_LookAt_NEW` | 第二颗光点进入画面 | 光点从头顶掠过。分镜写 No new prompt，所以 `hintText` 留空 |
| B3 | `Beat_TurnAround_NEW` | 转过 150° **且**第三颗光点在画面里 | 分镜的 B3 是「third mote, behind」。两个条件都要 —— 见下 |
| B4 | `Beat_Confirm_NEW` | 按下 A | A 图标 + `RECENTRE`。按下即满足，回正继续跑进下一拍 |

**B3 为什么两个条件都要**：只判角度的话，一个低头发呆、摇杆漂移的玩家也能过。而 B3 是空间定位落地的地方，GDD 给它的批注是 “Protect it.”。一条能在没看见东西的情况下通过的门，什么都没保护到。

**yaw 用最短角差**，所以转满 360° 读数是 0。这是对的：转回来的玩家又面朝前方了，他没有转身。

---

## 已知缺口与被阻塞项

| 项 | 状态 | 说明 |
|---|---|---|
| **A3 手柄震动** | **做不了** | 工程用 legacy Input Manager，`manifest.json` 里没有 `com.unity.inputsystem`。Unity 2019.4 在这套输入下**没有任何 rumble API**。已做成 `Beat_Cinematic_NEW.onRumble` 这个 UnityEvent 接缝并默认在 A3 触发，接上震动那天挂上去即可 —— 这样它是 Inspector 里看得见的一条线，而不是 GDD 里悄悄消失的一句话 |
| **三个面部按键** | 被阻塞（GDD §10 item 1） | `TutorialInput_NEW.InspectIsPlaceholder` 为 `true`，inspect 暂时绑在 `E`。D5 的提示文案在确认之前不该写。这条同时卡住 whitebox 和 Storyboard v1 的 Beat 3 |
| **VO** | 占位 | 每拍最多一句，多数没有。`Beat_Cinematic_NEW` 上有 AudioSource + AudioClip 两格，空着是正常状态 |
| **教程背景** | 未定（GDD §10 item 3） | live universe 还是 slow drift 没定，所以 attract 返回时**没有**做世界淡出 —— 现在写的两种方案下都要重写 |
| **美术资产** | 部分复用 | 天空、类星体、光子拖尾都是工程现有资产（见上一节）。尘埃与光点材质是生成的白盒，换掉 `.mat` 即可，builder 不用动 |
| **摆位与 GDD §6 的偏差** | **有意为之，见下** | |

### 当前摆位：接近类星体，而不是待在吸积盘里

GDD §6 把 Phase 0–1 写成「已经在吸积盘内部」。当前实现的是**接近**：玩家在远处，以恒定速度直线飞向类星体，B3 的「转身」看到的是第三颗光点和身后的空，不是完整的吸积盘。

这是按体验梳理直接定的，不是我的解读。它同时解决了原本无解的一处矛盾：A1/A2 要求开场画面里**已经有**流动的物质和「中心一块比黑更黑的斑」，B3 要求吸积盘与黑洞剪影是转身时**第一次被看见** —— 在一个不锁相机的第一人称场景里，开场画面里有的东西就已经被看见了，两条不能同时字面成立。类星体在远处正前方，A1/A2 就成立；B3 改成第三颗光点，转身这件事本身仍然被教到。

分镜（Figma）的 B3 原文就是 “Third mote, behind”，所以这一改是**向分镜靠拢**，不是偏离它。真正跟 GDD §6 有出入的是「在盘内 vs 接近盘」这一条。

所有位置都是 `TutorialSceneBuilder_NEW` 顶部的常量，也是场景里的 Transform —— 改常量重跑，或者直接在 Scene 视图里拖。

---

## 为什么有一个搭场景的编辑器工具

`Tutorial.unity` 原本是空场景。Phase 0–1 是 7 个拍子、一套第一人称 rig、3 个 HUD 元素、一张标题卡和 2 颗光点，彼此之间全是引用 —— 手接大约 40 次拖拽，也就是 40 次把 B2 的门指到 B1 光点上、然后花一下午找原因的机会。

`_Core_NEW/README_NEW.md` 已经记录过手工累积布局的结果：它会漂移，而且没人看得见它在漂移。

所以布局写成代码，放在能读、能重跑的地方。重复运行会被拒绝而不是叠加 —— 删掉 `[Tutorial]` 根节点再跑一次就得到干净的一套。

生成的层级：

```
[Tutorial]
├── Environment                    TutorialSky_NEW（天空盒 + 环境光；顺手关掉方向光）
├── Photon                         TutorialTravel_NEW（玩家本体，匀速直线）
│     ├── Camera                   FirstPersonLookRig_NEW，FOV 40（复用已有的 Main Camera）
│     ├── Dust                     ParticleSystem，球壳发射 + 世界空间模拟 = 速度感
│     ├── Trail                    TrailRenderer + PhotonSpectrumTrail ← 默认关闭
│     └── Motes                    跟着光走，不是钉在世界里
│           ├── Mote_A3_B1         右前方约 33°，A3 出现，B1 抓住
│           ├── Mote_B2            头顶约 52°，B2 出现
│           └── Mote_B3            正后方，B3 出现
├── World
│     └── Quasar                   BlazingQuasar.mat，正前方 6000 单位，直立
├── HUD                            Canvas + TutorialHUD_NEW + TutorialAttract_NEW
│     ├── Reticle / Legend / Hint / ConfirmPrompt / AttractCard
└── Director                       TutorialDirector_NEW
      └── Beats
            A1 A2 A3 B1 B2 B3 B4
```

尘埃用的是**一个** ParticleSystem，不是现成的 VFX prefab。工程里有 `RFX_Nebula` 系列，但每个是二十多个粒子系统堆出来的星系外观，而 GDD §10 item 3 已经把 Carnegie 硬件上的帧数列为待议项。一个球壳发射器就够了，代价是一个 draw call。要更华丽的观感，把 `Dust` 换成 RFX prefab 即可。

拍子是 Director 的子物体，按 Hierarchy 顺序自动收集 —— **调整分镜顺序是在 Hierarchy 里拖一下，不是改数组**。

---

## 下一步（Phase 2–4）

框架已经按整套 20 帧设计，多数后续拍子不需要新组件：

| Frame | 复用 | 需要新增 |
|---|---|---|
| C1 C3 | `Beat_Cinematic_NEW` | 加速 / 速度隧道 VFX |
| C2 | `Beat_Confirm_NEW`（`recentreOnPress = false`） | 发射音效与白帧 |
| C4 | `Beat_LookAt_NEW`，目标指向身后的类星体 | — |
| C5 | `Beat_Confirm_NEW` | 发射后调用 `SetForwardAxisToCurrent` 重设 jet 轴 |
| D1–D4 | `Beat_Cinematic_NEW` | 单个氢原子、原子团、光谱条的空态与单线态、`Time.timeScale = 0.2` 的吸收拍 |
| D5 | 新的 inspect 拍子 | **被三个面部按键阻塞** |
| E1 | `Beat_Cinematic_NEW` | 结构化纤维（复用现有 cosmic web） |
| E2 | 新的滑杆拍子 | 暗物质滑杆 UI，门控是「推到两端」 |
| E3 | `Beat_Confirm_NEW` | journey readout，直接进 Phase −1 |

两处需要注意的接口：

- **D2 的慢动作**：`Time.timeScale = 0.2` 时相机必须保持自由。`FirstPersonLookRig_NEW` 全程用 `Time.unscaledDeltaTime`，已经满足；`TutorialDirector_NEW` 同理，所以 `Duration` 拍子的时长读的是真实秒数而不是缩放后的。**D2 的 0.2× 需要按分镜秒数走的话，这是要确认的一处。**
- **E3 交给主体验**：`TutorialDirector_NEW.OnTutorialComplete` 是接口，目前没有订阅者。

---

## 仍未解决的上层问题

GDD §11：本文档与 Tutorial Storyboard v1 是两套不同的设计，团队还没选。这里实现的是**本文档**的版本（第一人称、活场景、无引导角色）。

如果最后选的是 v1（第三人称、Canvas 面板内、有观察者鸟），`TutorialDirector_NEW` / `TutorialBeat_NEW` / `TutorialInput_NEW` 这三个仍然适用 —— 控制方案与门控模型本来就来自 v1 且已对齐。会被丢掉的是 `FirstPersonLookRig_NEW` 和世界空间的摆位。

**这个决定应该在任何一版进 whitebox 之前做出。**
