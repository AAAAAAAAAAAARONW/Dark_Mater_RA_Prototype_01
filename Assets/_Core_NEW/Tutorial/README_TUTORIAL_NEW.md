# Tutorial — Journey of Light

实现 [Tutorial GDD — Journey of Light](https://bird-tune-e6c.notion.site/Tutorial-GDD-Journey-of-Light-3d3abeb8074b817b8f93e22777f5888f)。

**已实现：Phase 0（A1–A3）、Phase 1（B1–B4）、Phase 2（C1–C5），加上前面的 attract 状态。**
Phase 3–4 尚未实现 —— 见文末「下一步」。

---

## 五分钟跑起来

1. 打开 `Assets/_Core_NEW/Tutorial.unity`
2. **在 Lighting 面板给场景 assign 天空盒** —— builder 不碰这个
3. 菜单 `Tools > Journey NEW > Tutorial > Build or Update`
4. Play

看到的顺序：纯黑 → 标题卡 →（按 A）→ 黑保持一会儿后渐显出天空，玩家匀速朝远处的类星体直线飞行，提示行已经写着 `LOOK AROUND` → 第 15 秒一颗光点从右边缘飘出，提示变 `LOOK RIGHT` → `LOOK UP` → `TURN AROUND` → `A TO RECENTRE` 且下方出现 A 图标，按 A 视角平滑回到航向。**提示行始终是当下要做的那件事。**

调试：`F2` 跳过当前拍子。左上角三行 —— 拍子与门控状态、相机状态（是否在回正、偏离航向多少度、**这一帧有没有收到 A**、摇杆推了多少）、航行状态（速度、已飞距离、距类星体、**航向偏差**）。全部受 `DebugView_NEW.Overlay` 控制（`Tools > Journey NEW > Debug View`），出包时一起关掉。

那三行是为了让「A 没反应」「是不是没往前飞」这类问题**看一眼就能定位**，而不是靠打断点。

**再跑一次不需要重开场景** —— 90 秒无输入自动回到标题卡，光点归位，HUD 清空，视角回正。这是展陈现场唯一重要的行为：下一位观众看到的必须是开头，而不是上一位停在半路的画面。

---

## 接手前先读这一节

### builder 不会毁掉你的改动

两个菜单项：

| 菜单 | 行为 |
|---|---|
| `Tools > Journey NEW > Tutorial > **Build or Update**` | 日常用这个。**增量**：缺什么补什么、空引用填上，已经存在的一律不碰 |
| `… > Rebuild From Scratch` | 先删整个 `[Tutorial]` 再重建，弹窗确认。只在真的想推倒重来时用 |

分工不是按字段，是按**种类**：

- **builder 拥有「存在性」和「接线」** —— 该有哪些物体、哪个门指向哪颗光点、HUD 订阅哪个 director。空引用是**缺口**不是决定（通常是因为后来的版本新增了它该指向的东西），所以会被填上，**并且每填一处都打一条 Console log**。
- **你拥有「数值」** —— Transform、调过的参数、换过的材质、自己加的子物体、UnityEvent 上挂的东西。数字、布尔、枚举**永远不会**被写进一个已经存在的组件，因为分不清「没人设过」和「有人就是设成了这个值」。
- **builder 拥有「文案」** —— 提示行、A 键提示、图例那串控制说明。这些是 builder 自己写的内容，不是谁调出来的数值，所以 `Build or Update` **会**改写它们，并把改前改后一起打进 Console。每个拍子（和 HUD）上有一个 `builderOwnsCopy`，默认开；取消勾选，那一处的文案就归你，builder 再也不碰。`beatId`、`legendFirstBeatId` 不算文案 —— 它们是标识符，走的还是「只填空」。

拉过一个新增阶段的改动之后，跑一次 `Build or Update` 就行。Console 会告诉你它加了几个东西、填了几个引用，或者「没什么可做的」。

**你自己往 builder 里加东西时，请守住这条线**：走 `FindOrCreate` / `AddIfMissing` / `Wire`，直接写属性的地方用 `IsFresh` 包住。

**builder 会删东西的地方只有两处**，都会在 Console 里大声说明，Undo 都能撤回：

- `RetypeBeat` —— 某一帧换了种类（A2 A3 从过场变成缩放课）。必须显式删，因为 `TutorialBeat_NEW` 是 `DisallowMultipleComponent`：往已经有 `Beat_Cinematic_NEW` 的物体上加 `Beat_Zoom_NEW` 不会叠加，而是失败，然后这一轮会继续往一个根本没在跑的组件里写值。挂在**旧组件自己**的 UnityEvent 上的东西会跟着走（builder 放的那些会重新接上）；同一个物体上其他组件不受影响。
- `RetireBeat` —— 某一拍的内容搬走了，它没工作了（A4）。留着它不是保留谁的改动，而是留一道玩家要满足两次的门 —— A4 会去要一次玩家在 A2 A3 已经做完的缩放，杵在那里看起来就是坏的。判定带类型保护，所以只是重名的物体不会被误删。

### 加一拍需要动几个地方

1. 在 `TutorialSceneBuilder_NEW` 里对应的 `BuildPhaseN` 方法里加一段 `Wire(...)`（照抄邻居即可）
2. 需要新组件的话在 `Tutorial/` 下新建一个，**一个组件一件事**
3. 跑 `Build or Update`

拍子是 Director 的子物体、**按 Hierarchy 顺序执行**。新建的拍子会被放到 builder 上一个走过的拍子之后 —— builder 是按分镜顺序走的，所以插在中间的一帧落在正确的位置，而不是被追加到列表末尾。已经存在的拍子永远不会被移动：在 Hierarchy 里重排是一个决定，builder 不撤销决定。

**拍子不认识具体的效果，效果也不认识拍子。** 拍子只管门控和提示；`TutorialEmission_NEW` 这类只管效果，靠拍子的 `onEnter` UnityEvent 连起来。所以整条 Phase 2 时序在 Inspector 里是看得见的：点开 C3，它的 onEnter 上写着 `TutorialEmission_NEW.Emit`。重排分镜不用改任何 C# 文件。

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
├─ TutorialSpeedStreaks_NEW   近景拉丝层，速度取自 TutorialTravel_NEW
├─ TutorialZoom_NEW           左摇杆缩放 FOV，并按视场缩放视角灵敏度
├─ TutorialRangeMap_NEW       右上角小地图：固定世界框，玩家在里面移动
├─ TutorialZoomGauge_NEW      右侧缩放刻度条 —— 让 FOV 这个控制变得看得见
├─ TutorialEmission_NEW       Phase 2 时序：加速 → 阈值 → 发射（反转航向、开拖尾）
├─ TutorialFlash_NEW          全屏闪白，颜色和时长由调用方给
├─ TutorialCameraShake_NEW    相机位移抖动，幅度由外部驱动
├─ Beats/
│   ├─ Beat_Cinematic_NEW     A1 C1 C3（无动作，按分镜时长走）
│   ├─ Beat_Zoom_NEW          A2 A3（推进去 / 拉回来 —— 开场那段的教学）
│   ├─ Beat_LookAt_NEW        B1 B2 C4（把目标带进准星）
│   ├─ Beat_TurnAround_NEW    B3（转过 150° 且目标在画面里）
│   └─ Beat_Confirm_NEW       B4 C2 C5（按 A）
└─ Editor/
    ├─ TutorialSceneBuilder_NEW   一键搭场景（布局）
    ├─ TutorialBeatEditor_NEW     拍子的 Inspector：藏掉当前模式不读的字段
    └─ TutorialWorldAssets_NEW    资产查找与生成（取材）
```

运行时依赖：引擎、UGUI、`_Core_NEW` 的 `DebugView_NEW` / `HierarchyBadge_NEW` / `NebulaProfile_NEW`，以及 **`Assets/Scripts/PhotonSpectrumTrail.cs`**（旧脚本，光子拖尾的光谱生成，刻意复用而不是重写）。**不依赖 Cinemachine** —— 教程是第一人称，不走 FreeLook。

---

## 场景里看得见的东西是从哪来的

**天空盒不归 builder 管** —— `RenderSettings` 一行都不写，自己在 Lighting 面板里 assign。其余屏幕上的每一样东西都来自工程里已有的资产：

| 元素 | 资产 | 负责哪几帧 |
|---|---|---|
| 后处理 | `Scenes/Test Level_Profiles/Main Camera Profile.asset` | 全程。和 PlaytestBuild **共用同一份 profile**，不会分家 |
| 类星体 | `Materials/BlazingQuasar.mat` | 远处的目标，也是 C1 到站、C3 发射的地方 |
| HUD 字体 | `Fonts/Gontserrat-Regular SDF.asset` | 全程。PlaytestBuild 的 UI 用的就是它 |
| 光子拖尾 | `Shaders/Custom_PhotonTrail.mat` + `Scripts/PhotonSpectrumTrail.cs` | **Phase 0–1 关闭**，C3 发射之后才亮。位置在眼睛前方 2.6 单位、略偏下 |
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

## 星星闪烁（Nebula shader）

原来的 `stars()` 是纯静态的 —— `step(_StarThreshold, valueNoise3D(dir * _StarScale))`，一个时间项都没有。现在能闪了，新增三个参数：

| 属性 | 作用 |
|---|---|
| `_StarTwinkle` | 0 / 1 开关，**默认 0** |
| `_StarTwinkleSpeed` | 每秒周期数，0–8 |
| `_StarTwinkleAmount` | 谷底掉到多暗，0 = 不闪，1 = 闪到全黑 |

**默认关，是刻意的。** 这个 shader 被 15 个场景引用，不该因为教程想要闪烁就让别人的画面全变了。已有材质渲染结果与之前逐像素一致。

两个实现上的选择：

- **相位来自星星所在的噪声格。** `hash(floor(dir * _StarScale))` 给每颗星一个稳定且与邻居无关的相位。不这么做的话整片天空会同步脉动，读起来是屏幕在闪，不是星星在闪。
- **只调亮度，不调阈值。** 让阈值随时间变会导致星星整颗出现和消失 —— 那是另一种效果，而且是更糟的一种：真实的星星不会眨没。

三个参数一路暴露到了数据层：`NebulaProfile_NEW` 加了 `starTwinkle / starTwinkleSpeed / starTwinkleAmount`，`NebulaResponder_NEW`（主旅程按层切换天空用的）和 `TutorialSky_NEW` 都会写。已有的 7 个 `NP_*.asset` 没有这三个字段，Unity 会填 C# 默认值（关闭），所以主旅程画面不变。

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

第一版是内置 Arial 的白字浮在画面上，那是调试读数不是界面。现在走 **TextMeshPro + 工程自己的 Gontserrat**（PlaytestBuild 的 UI 用的就是它 —— 教程和主旅程不该看起来像两个产品）。内置 Arial 在固定像素尺寸下一旦 Canvas 缩放就发虚，而在 Carnegie 那块又宽又弯的屏上它一直在缩放；SDF 字体任何尺寸都是锐的，还能调字距。

| 元素 | 长什么样 |
|---|---|
| 控制提示 | 深色圆角底板（9-slice）+ 一行大字。**底板是 HUD 和调试读数的分界线** —— 纯白字在明亮的类星体和星场前会消失，深底板在房间后排也读得住 |
| A 提示 | 圆形按键图标里写 A，右边跟动词。做成圆盘而不是光秃秃一个字母，因为字母会被读成文字，而文字看起来不能按 |
| 准星 | 圆环，不是方块。方块在暗屏中央会被读成坏点；圆环读作瞄准框，而且光点能留在环内 —— B1 的门控正是「光点在环内」 |
| 图例 | 小字、低透明度。它要在整段体验里一直挂着，不能和当下的指令抢注意力 |

底板、圆环、圆盘三张贴图是 builder 生成的（`TutorialChip.png` 九宫格、`TutorialRing.png`、`TutorialDisc.png`），纯几何单色。美术直接替换 PNG，builder 一行都不用动。字距（`characterSpacing`）拉开了一点 —— 全大写挤在一起在远处会读成一个色块。

**文案的归属没变**：形状和位置属于 HUD，词属于拍子。GDD §5 要求「继续」这个操作在每一处都是同样的形状、同样的位置，所以 `Beat_Confirm_NEW.promptText` 现在只写动词（`RECENTRE`），A 那个图标是 HUD 画的。

---

## 相机手感：数值全部抄自 PlaytestBuild

不是我选的，是从 `PlaytestBuild_NEW.unity` 的 Player 上逐个抄下来的 —— 教程要交接给一个手感一致的主体验。

| | PlaytestBuild | 教程 | 说明 |
|---|---|---|---|
| 水平灵敏度 | `xSensitivity: 100` | 100 °/s | 直接照抄 |
| **垂直灵敏度** | `ySensitivity: 0.2` | **26 °/s** | 需要换算，见下 |
| 死区 | `stickDeadband: 0.1` | 0.1 | 直接照抄 |
| 回正水平 | `resetXSpeed: 50` | **140 °/s** | `MoveTowardsAngle` 匀速。**唯一没照抄的一格**，见下 |
| 回正垂直 | `resetYSpeed: 1.5` | 195 °/s | 同样换算 |
| FOV | FreeLook `FieldOfView: 40` | 40 | Unity 默认 60 会像另一个游戏 |

**垂直那一格是唯一需要动脑的地方。** PlaytestBuild 驱动的是 Cinemachine FreeLook，`m_YAxis.Value` 是 0→1 的归一化轴，跨越整个 orbit 弧。那个 rig 的 orbits 是 height 3 / 1 / −2、radius 1 / 3 / 1，大致 130° 的弧，所以 0.2 轴单位/秒 ≈ **26 °/s**。把 0.2 当成 0.2 °/s 抄过来会完全不能用；随手拍一个数字，就是教程和游戏手感对不上的经典来源。

PlaytestBuild 的柔和感来自 Cinemachine 的 damping，第一人称没有对应物，所以 `smoothTime`（默认 0.06s）把它显式加回来。设成 0 就是原始输入。

### 只有一根摇杆能转视角

GDD §4：“Stick — Look. **The only stick.**” 两根都能转不是「只有一根」，那是两个碰巧做同一件事的控制，玩家哪个都学不会；而且不管提示怎么写，它对其中一根来说都是错的。

`lookStick` 默认 **Right**，和 PlaytestBuild 一致 —— 教程和主旅程对「哪根摇杆负责看」给同一个答案。分镜的提示图上写的是 LEFT STICK，实际的 build 是裁决者，所有提示文案已经跟着改成 `RIGHT STICK`。

工程里 `RightStickX/Y` 是手柄轴 4/5，`Horizontal/Vertical` 是轴 1/2（同时带 WASD）。换回左摇杆的话，改 `lookStick` 加改 `TutorialSceneBuilder_NEW` 里那几行文案，两处一起。

空闲检测（90 秒回 attract）故意仍然读两根：推了那根不负责转视角的摇杆的观众，也是在场的观众，不该被重置。

### A = 复位，绑在 rig 上，不绑在拍子上

GDD §4 的原话是 “A — confirm / recentre / emit. **One button, one meaning**, tutorial and journey alike.”

之前 A 只在 B4 那一拍触发复位，那不是「一个含义」，那是一个拍子形状的例外 —— 于是它就以最明显的方式失效：在其他任何时刻按 A，什么都不会发生。现在 `FirstPersonLookRig_NEW` 自己读 A，从玩家拿到相机的第一帧起 A 就复位。A 另有含义的拍子（C2 发射、E3 交接）在自己开着的期间用 `SetConfirmRecentres(false)` 关掉它，退出时交还。

**回正默认不可打断**，这是本教程唯一一处明知故犯地违背 GDD §5「相机永不锁定」。理由：玩家是按了按钮主动要求的，全程约一秒，而每一次试图让它可打断的结果都是 A 看起来是坏的 —— 磨损的摇杆静止时读数就能超过阈值，在画面还没明显动之前就把回正取消掉。`recentreCancellable` 这个开关留着，等展陈用的手柄确认没问题再打开。

**回正速度是唯一没照抄 PlaytestBuild 的数值。** 那边的 50 °/s 对一个很少偏离 90° 以上的 FreeLook 是合适的；而 B4 永远紧跟在一次 150° 以上的转身之后，50 °/s 就是三秒半的极慢漂移 —— 长到那一下按压读起来像什么都没发生。

---

## 为什么玩家一直在动

`TutorialTravel_NEW` 让玩家以恒定速度直线飞向类星体。**不接受任何输入** —— 没有改速度或改航向的公开方法，也没有一个「允许玩家控制」的开关等着以后被打开。GDD §3 把「你不能操纵方向，只能看」列为教程要装进玩家脑子里的第二件事，§4 直接把移动摇杆删掉，让这条靠控制的缺席来执行，而不是靠一行文字。

光点是**挂在玩家身上**的。25 单位/秒的航速下，钉在世界空间的光点四秒就被甩到身后，B1 那条门就永远过不去了 —— 分镜说的「缓慢飘出画面右缘」是相对于光的运动，不是相对于宇宙的。

### 速度感：两层近景，缺一不可

**问题**：25 单位/秒，最近的物体在 40 单位外，下一个在 6000 外 —— 屏幕上没有任何东西变化得足够快，玩家在飞但看起来像是停着的。速度不是移动者的属性，是**掠过物**的属性；巡航中的飞机看起来是静止的，直到地面近到糊掉。

所以是两层，做法不一样：

| 层 | 模拟空间 | 粒子自身速度 | 作用 |
|---|---|---|---|
| `Dust` | **世界** | 几乎为 0 | 视差。新尘埃一直在玩家周围生成，已生成的留在原地被甩到身后 |
| `Streaks` | **本地** | 沿 −Z **等于航速** | 拉丝。`Stretch` 渲染模式沿粒子自身速度方向拉长 |

**为什么拉丝层必须是本地空间**：`Stretch` 沿粒子**自己的**速度向量绘制，而一个待在世界里不动、被相机飞过的粒子，速度是 0 —— 画不出任何东西。所以拉丝层跟着玩家走，并被赋予一个真实的向后速度。

两层一起才有纵深，任何一层单独都像屏保。

尘埃这次也调近了：球壳半径从 160 收到 70，寿命从 24–40 秒收到 4–9 秒，发射率 90 → 260，粒子也变小。**视差是角速度，来自近处** —— 20 单位外的粒子在 25 u/s 下会明显扫过，160 单位外的几乎不动。上一版把壳放在 160，尘埃约等于画在天上。

拉丝速度取自 `TutorialTravel_NEW.Speed`，不是第二份手抄的数字（`TutorialSpeedStreaks_NEW`）。跟真实航速对不上的拉丝比没有拉丝更糟：那看起来像世界在一个静止的玩家底下滑动。

**还想更强的话**，按代价从低到高：调 `Dust` 的 `rateOverTime` 和球壳半径 → 调 `Streaks` 的 `velocityScale` / `lengthScale` → 提高 `TutorialTravel_NEW.speed`（但这会同时改变到达类星体的时间）→ 在中景加一层大而暗的气体团，补上近尘埃和 6000 单位外类星体之间那段空白。

---

## 三条设计约束，写进了结构里

### 1 · 「不靠计时器推进」的准确范围

GDD §5 说 nothing advances on a timer，但 Phase 0 的每一帧都写着时长（A1 是 0:00–0:08）。这两句不矛盾，规则的范围比字面窄：

> **绝不用计时器代替玩家的动作。**

所以 `AdvanceMode` 是两个显式的枚举值：

| 模式 | 用在 | 时间能否推进 |
|---|---|---|
| `Duration` | 不要求玩家做任何事的帧（A1、C1 C3，将来 D1–D4） | 能，这是唯一的用途 |
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
| 1 | 控制提示行 | **A1** | 每个有门控的拍子都会改写它，见下 |
| 2 | 控制图例 | B1 | **永不消失**，代码里没有隐藏它的路径 |
| 3 | 准星 | B1 | 跟图例一起 |
| 4 | A 提示 | B4 | 按下即隐 |

提示行由每个拍子上的 `hintMode` 控制，三个值：

| 值 | 含义 | 用在 |
|---|---|---|
| `Keep` | 保持现状 | 默认。分镜在 B2 B3 上写的 “No new prompt” 就是它 |
| `Show` | 换成本拍的 `hintText` | **每一个有门控的拍子** |
| `Clear` | 拿掉 | 目前没人用，留着以免有人再用空字符串去伪装 |

**提示行永远是「当下要玩家做的那件事」**：

| 拍子 | 提示行 |
|---|---|
| A1 | `RIGHT STICK  ·  LOOK AROUND` |
| A2 | `LEFT STICK  ·  ZOOM IN` |
| A3 | `LEFT STICK  ·  ZOOM OUT` |
| B1 | `RIGHT STICK  ·  LOOK RIGHT` |
| B2 | `RIGHT STICK  ·  LOOK UP` |
| B3 | `RIGHT STICK  ·  TURN AROUND` |
| B4 | `A  TO  RECENTRE` |
| C3 | `A  TO  EMIT` |
| C4 | `RIGHT STICK  ·  LOOK BACK` |
| C5 | `A  TO  RECENTRE` |

分镜在 B2 B3 上写的是 “No new prompt”，意思是**控制**已经教过、不用再教一遍 —— 这一点保留了，B 段里 `RIGHT STICK` 那半截自始至终不变（A2 A3 是另一课，教的是另一根摇杆）。但提示行不只在教控制，它还在说下一步做什么，而一条写着 LOOK RIGHT 却在等玩家抬头的提示，比没有提示更糟。所以**控制那半截固定，动作那半截跟着拍子走**。

底板说「做什么」，下方的圆形 A 图标说「用哪个键做」。两者不会同时说同一件事。

文案全部在 `TutorialSceneBuilder_NEW` 的 `BuildPhaseN` 里，一处一行 `.Copy("hintText", ...)`；场景搭好之后也能直接在拍子的 Inspector 里改 —— 但改之前先把那一拍的 `builderOwnsCopy` 取消勾选，否则下一次 `Build or Update` 会按 builder 里的文案把它改回来（并在 Console 里说明改了什么）。

**`hintText` 只在 `hintMode` 是 `Show` 的时候被读。** 模式是 `Keep` 或 `Clear` 时那一栏在 Inspector 里直接不显示 —— 一个改了不会生效、也不报错的输入框，是最贵的一种坑。同理，`duration` 只在 Duration 模式下显示，`inputGraceSeconds` 只在 PlayerAction 模式下显示。这些由 `Editor/TutorialBeatEditor_NEW.cs` 负责，它挂在基类上（`true` = 包含派生类），所以新写的拍子自动就有。

提示行放在 A1 是**有意偏离 GDD §7** 的：文档把第一个提示放在 B1、要求 B1 之前屏幕全空。走上来的观众应该先被告知「你可以转视角」，再被要求去转 —— 这是你定的顺序，不是我的解读。

图例文案取自 GDD §4，按拆成两根摇杆之后的控制方案展开：

```
RIGHT STICK = LOOK    LEFT STICK = ZOOM    A = CONFIRM / RECENTRE
```

准星不在 GDD §7 的 UI 清单里，因为它不是玩家需要学的界面元素。但 B1 的门控是「光点保持在准星内」，准星不可见这条门就没法玩，所以它跟图例一起到场。

---

## 拍子与门控对照

| Frame | 组件 | 门控 | 实现要点 |
|---|---|---|---|
| A1 | `Beat_Cinematic_NEW` | 无，8 秒 | 提示 `RIGHT STICK · LOOK AROUND` 在这里出现；渐显也在这里开始；`onEnter` 挂 VO |
| **A2** | `Beat_Zoom_NEW` | **视场收窄过 0.55** | 提示 `LEFT STICK · ZOOM IN`。分镜里这是一帧无动作的过场 —— 见下 |
| **A3** | `Beat_Zoom_NEW` | **视场回到 0.15 以下** | 提示 `LEFT STICK · ZOOM OUT`。`onEnter` 激活光点、触发 `onRumble`（见「已知缺口」）—— 光点是在视野变宽的那一刻从边缘进来的 |
| B1 | `Beat_LookAt_NEW` | 光点进入准星 | 提示 `LOOK RIGHT`。`holdSeconds = 0`，GDD 明写 not a dwell timer |
| B2 | `Beat_LookAt_NEW` | 第二颗光点进入画面 | 提示 `LOOK UP`。光点从头顶掠过 |
| B3 | `Beat_TurnAround_NEW` | 转过 150° **且**第三颗光点在画面里 | 提示 `TURN AROUND`。分镜的 B3 是「third mote, behind」，两个条件都要 —— 见下 |
| B4 | `Beat_Confirm_NEW` | 按下 A | 底板换成 `A TO RECENTRE`，下方是 A 图标 + `RECENTRE` |
| C1 | `Beat_Cinematic_NEW` | 无，12 秒 | 加速 + 抖动渐起。`onEnter` → `TutorialEmission_NEW.BeginSpinUp` |
| C2 | `Beat_Confirm_NEW` | 按下 A | **A 在这里是「发射」不是「复位」** —— `recentreOnPress = false`，拍子期间把 rig 的 A 绑定关掉，退出时交还 |
| C3 | `Beat_Cinematic_NEW` | 无，8 秒 | 闪白 + **反转航向** + 打开光子拖尾。**相机绝不锁** |
| C4 | `Beat_LookAt_NEW` | 类星体进入画面 | 目标就是那颗类星体。C3 之后玩家在远离它，所以「转身看到它已经只是一个亮点」是字面成立的 |
| C5 | `Beat_Confirm_NEW` | **回正完成** | 唯一一个 `waitForRecentre = true` 的拍子 —— 分镜的门控是「视角已回到前向轴」，不是「按下了 A」 |

**B3 为什么两个条件都要**：只判角度的话，一个低头发呆、摇杆漂移的玩家也能过。而 B3 是空间定位落地的地方，GDD 给它的批注是 “Protect it.”。一条能在没看见东西的情况下通过的门，什么都没保护到。

**yaw 用最短角差**，所以转满 360° 读数是 0。这是对的：转回来的玩家又面朝前方了，他没有转身。

---

## 已知缺口与被阻塞项

| 项 | 状态 | 说明 |
|---|---|---|
| **A3 手柄震动** | **做不了** | 工程用 legacy Input Manager，`manifest.json` 里没有 `com.unity.inputsystem`。Unity 2019.4 在这套输入下**没有任何 rumble API**。已做成 `TutorialBeat_NEW.onRumble` 这个 UnityEvent 接缝并默认在 A3 触发，接上震动那天挂上去即可 —— 这样它是 Inspector 里看得见的一条线，而不是 GDD 里悄悄消失的一句话 |
| **三个面部按键** | 被阻塞（GDD §10 item 1） | `TutorialInput_NEW.InspectIsPlaceholder` 为 `true`，inspect 暂时绑在 `E`。D5 的提示文案在确认之前不该写。这条同时卡住 whitebox 和 Storyboard v1 的 Beat 3 |
| **VO** | 占位 | 每拍最多一句，多数没有。`Beat_Cinematic_NEW` 上有 AudioSource + AudioClip 两格，空着是正常状态 |
| **教程背景** | 未定（GDD §10 item 3） | live universe 还是 slow drift 没定，所以 attract 返回时**没有**做世界淡出 —— 现在写的两种方案下都要重写 |
| **美术资产** | 部分复用 | 天空、类星体、光子拖尾都是工程现有资产（见上一节）。尘埃与光点材质是生成的白盒，换掉 `.mat` 即可，builder 不用动 |
| **摆位与 GDD §6 的偏差** | **有意为之，见下** | |

### 当前摆位：接近类星体，而不是待在吸积盘里

GDD §6 把 Phase 0–1 写成「已经在吸积盘内部」。当前实现的是**接近**：玩家在远处，以恒定速度直线飞向类星体，B3 的「转身」看到的是第三颗光点和身后的空，不是完整的吸积盘。

这是按体验梳理直接定的，不是我的解读。它同时解决了原本无解的一处矛盾：A1 要求开场画面里**已经有**流动的物质和「中心一块比黑更黑的斑」，B3 要求吸积盘与黑洞剪影是转身时**第一次被看见** —— 在一个不锁相机的第一人称场景里，开场画面里有的东西就已经被看见了，两条不能同时字面成立。类星体在远处正前方，A1 就成立；B3 改成第三颗光点，转身这件事本身仍然被教到。

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
│     ├── Dust                     ParticleSystem，球壳 + 世界空间模拟 = 视差
│     ├── Streaks                  ParticleSystem，本地空间 + Stretch = 拉丝
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

## 两根摇杆，各管各的

| 控制 | 作用 |
|---|---|
| **右摇杆** | 转视角。`FirstPersonLookRig_NEW`，数值抄自 PlaytestBuild |
| **左摇杆** | 缩放视场（FOV 40 → 12）。`TutorialZoom_NEW` |
| **A** | 确认 / 复位 / 发射 |

没有任何地方同时读两根摇杆，两个控制是可分离的。

### 一个踩过的坑：改默认值不会影响已有场景

视角从左摇杆挪到右摇杆时，我改的是字段的 **C# 默认值**。而 `lookStick` 是序列化字段 —— **已经存在的场景保留的是它自己存的那个值，新默认值对它完全不可见**。结果：场景里视角还在左摇杆上，缩放也用左摇杆，右摇杆彻底没反应，而所有提示都写着 RIGHT STICK。

这也是所有权规则第一次真正咬人：规则说 builder 不覆盖已有组件上的枚举，所以修复根本到不了那个场景。

现在有 `SeparateTheSticks`：**检测的是冲突，不是默认值**。两个控制绑在同一根摇杆上不可能是谁的有意决定 —— 它让另一根摇杆完全失效、让两条提示同时说谎，那是坏状态不是偏好。所以 builder 会修它并大声报出来。要是有人**故意**把视角放回左摇杆，那就该把缩放挪走，而不是让 builder 坚持自己的口味。

`TutorialZoom_NEW` 在运行时也查一遍并报 error —— 因为这个坑的形成过程对两边都是隐形的，场景可以在没人做错任何事的情况下变成错的。

**教训对以后一样有效**：改一个序列化字段的默认值，等于没改任何已有场景。要让它落地，得写迁移。

### 同一个坑的第二种形状：文案冻在了第一次写下的样子

上面那次修复把控制方案改对了，提示行却还写着 `LEFT STICK · LOOK AROUND`。原因是所有权规则的另一半：`Wire` 的 `Str()` 只填**空**字符串，非空一律当成「有人的决定」。

但文案不是数值。`hintText` 里的那句话是 builder 自己写的，场景里那份只是它某一次运行留下的拷贝 —— 把它当成人的决定来保护，等于让文案永远追不上代码。控制改了、提示不改，玩家看到的是一条**错的**指令，这比丢掉某人的措辞坏得多。

所以文案走 `Copy()`，不走 `Str()`：已有组件也照写，每改一处打一条 Console log 说明改前改后，`builderOwnsCopy` 取消勾选就完全免疫。区分标准是「这段字是谁写的」，不是「它现在空不空」。

顺带一提，当时之所以查了很久，是因为改 A2 A3 的 `hintText` 也没反应 —— 那是第三件事：它们的 `hintMode` 是 `Keep`，那一栏根本不会被读。现在它在那种模式下不显示了。

### 缩放是一处对 GDD 的有意偏离

GDD §4 写的是 “Stick — Look. **The only stick.**”。加左摇杆是偏离它的，理由是开场那段的实际问题：Phase 0 有二十秒在告诉玩家「你可以转视角」，而画面里唯一能看的东西是一个远处的亮点，转来转去没有回报。「把远处的东西看得更近」是天文展项能交给观众的、最不需要解释的动作。

要撤掉：删 `TutorialZoom_NEW`，把 A2 A3 改回 `Beat_Cinematic_NEW`。

### 它是被「教」的，不是被「提示」的

第一版只加了一行提示：屏幕上写 `LEFT STICK · LOOK CLOSER`，然后不管玩家碰没碰摇杆都往下走 —— **那不是教学**。这个教程里其他每一个控制都是同一种教法：拍子不满足就不推进。

所以 **A2 和 A3 是两个 `Beat_Zoom_NEW`**：A2 的门控是视场收窄过 `threshold = 0.55`（`direction = In`），A3 是回到 `0.15` 以下（`direction = Out`）。

**为什么是两拍而不是一拍。** 推进去和拉回来是两件不同的发现。推进去是奖励：类星体从一个亮点变成有喷流的盘。拉回来是没人会自己找到的那一半 —— 而一个被留在 12° 视场里走完全程的玩家，会错过 B2 的喷流、会在 B3 转身时迷失方向。所以这一课收两次费，Phase 0 交给 B1 的时候视场回到了原位。

门控只是一个阈值，不是 dwell、也不是要求按住。GDD §6 在 B1 上写的 “not a dwell timer” 是同一件事：一个刚搞懂某个控制的人，应该因为用了它而被放行，而不是被要求把它端稳。

有一处小保险：如果拍子打开时门控**已经**成立（重排了拍子、F2 跳过了上一拍、或者 attract 在缩放中途重启），那就要求玩家先离开阈值再穿回来。一个要求缩放却不花一次缩放的拍子，什么都没教。

**分镜里 A2 A3 是两帧无动作的 Silence。** GDD 的 Phase 0 三帧都没有任何东西可按；这里把后两帧换成了带门控的教学。改动记在这里。A3 自己的内容留着了：光点仍然在这一拍出现，而且是在玩家把视野拉宽的时候从边缘进来的 —— 视野变宽本来就是边缘上的东西第一次可见的那一刻。

### 还需要一个看得见的刻度条

只有门控还是不够。**一行文字和一张变大的画面，看不出是同一件事** —— 非玩家推动摇杆、看到整个画面缩放，没有任何理由推断出「存在一个有量程的控制」，那同样可以是场景在动。

所以有了 `TutorialZoomGauge_NEW`：屏幕右侧一条竖直刻度条，推杆时填充上升、松手时回落，旁边写着当前视场度数。它在画面上是**不动的**，而世界在它背后变化 —— 这才把「刚才发生了点什么」变成「这是我在做，而且还有更多可用」。

它在缩放拍子打开时淡入，然后像控制图例一样一直留着。教完就消失的读数，教给玩家的是「那个控制也一起没了」。

判定用的是**拍子的类型**（`Beat_Zoom_NEW`）而不是 id，所以 A2 A3 改名、或者以后再加一个缩放课，这里都不用动。

### 右上角的小地图

`TutorialRangeMap_NEW`：一个圆形俯视图 —— 中心是自己，一根短杆表示当前朝向，一个点是类星体，下面是实时距离。跟图例和准星一起在 B1 出现。

第一版我做成了一条进度轨 —— **那是时间轴不是地图**。它只能回答「我走了百分之多少」，回答不了玩家在 B3 被要求转身的那一刻真正产生的问题：「我不再面朝前方之后，东西都在哪」。

第二版又错了另一个方向：我把玩家钉在圆心、让类星体滑过来 —— 那画的是**类星体在靠近玩家**，跟实际发生的事、跟这段体验讲的事都相反。类星体是固定在空间里的，动的是光。

所以现在地图**框住的是世界不是玩家**：第一帧按「起点 ↔ 类星体」定好中心和比例，之后类星体在框里不动，**玩家的标记在框里走过去**。

**北朝上**，玩家标记上带一根朝向杆：转视角时地图不转，转的是杆；在 B3 转满一圈，杆会指回你来的方向。这个对应关系才是地图的意义所在，比一个百分比值钱。

**这根杆反了两次，第二次才找到根因。**

第一次：它被放在 Facing 矩形的**底部**，也就是标记的下方，于是指的是玩家**来的**方向。第二次：翻上去之后仍然不对 —— 因为方向是从 `-lookRig.Yaw` 算出来的，那是**两套约定**（世界 yaw 的正方向、UI 旋转的正方向）必须互相吻合、而且还要跟 `ToMap` 的投影吻合。三者里错一个就是错的，而且错法看起来都像「画反了」。

现在两件事都被拿掉了：

- **角度来自相机自己的 forward，走的是和标记完全相同的投影**（世界 X → 地图 X，世界 Z → 地图 Y），再 `Atan2` 出方位角。杆和两个标记是用同样两个数算出来的，不可能互相矛盾。
- **杆的位置归组件管，不归布局管。** `TutorialRangeMap_NEW.PlaceStem()` 在 Awake 里把它停在轴心正上方 —— 杆相对于旋转中心在哪，不是谁的审美，那就是「这根杆指哪」的定义。长度、粗细、颜色仍然是你的。builder 也调这同一个方法（不是自己复制一遍），所以不用进 Play 模式，Scene 视图里就是对的；已有场景里画在下方的杆也是这里被修好的。

还加了一次**自检**：开局玩家还朝着前方时，「我朝哪」和「我往哪走」在地图上是同一根箭头，所以 `CheckTheNeedleAgrees` 会点乘一下这两个方向，小于零就报 error 说明地图正在说反话。这个 bug 两次都是「看起来没问题，直到有人转身」，所以值得让它自己喊出来。

**取 XZ 平面**，因为行进是水平的，这段体验没有垂直结构可丢。B2 抬头看喷流时地图没有任何反应 —— 这是对的：它是「你在哪」的地图，不是「你在指哪」的地图。

比例**只定一次**。一个会随玩家移动重新缩放的框是变焦，而一张你横穿它时还在变焦的地图，说不出你走了多远。`displayScale` 和 `unitSuffix` 留着 —— 场景单位对观众没有意义，等叙事定下用什么单位计量再改。

**灵敏度必须跟着视场走，这一点最容易漏。** 「度/秒」是常数，但「屏幕/秒」不是：视场从 40° 收到 12° 时，同样的摇杆推量扫过的画面是三倍，玩家正想看仔细的时候相机反而变得暴躁。`TutorialZoom_NEW` 把 `FirstPersonLookRig_NEW.SensitivityScale` 设成 `当前视场 / 基准视场`，视觉速度就保持不变了。

---

## 到站：类星体的距离是算出来的，不是拍的

玩家在 C1 结束、C2 打开的那一刻**正好抵达类星体并停住**，然后一直静止到被 A 发射出去。

这件事不能靠「设一个速度、算一个距离」解决，因为 **B1–B4 是玩家门控的、时长无上限**：一个到处看两分钟的观众和一个一路冲过去的观众，到达 C1 时距离完全不同，但两个人都必须在 C2 打开时到达。

所以它被写成一个**距离问题**而不是速度问题 —— `TutorialTravel_NEW.ApproachTo(目标点, 秒数, 曲线)`：不管现在离多远，在这么多秒里覆盖掉，末速为零。C1 的 `onEnter` 调它，时长和 C1 的拍子时长一致。

时间线是这样倒推出来的：

| 阶段 | 覆盖 |
|---|---|
| Phase 0 | 20 秒 × 25 u/s = 500，类星体在第一个提示出现前已经明显变大 |
| Phase 1 | 玩家门控、无上限。`holdDistance = 900` 让巡航停在离核心 900 处 —— 磨蹭的观众停在那儿，而不是从他正朝着去的东西里穿过去 |
| C1 | 12 秒覆盖剩下的一切，停在 `arrivalStandoff = 560` |

于是类星体放在 **2600**：冲过去的观众从约 2100 外开始最后一段，慢慢逛的从 900 外开始，两个人都在 C2 打开时到站。

停住这件事是 `travel.Halt()` 做的 —— C2 全程速度为 0，直到 `Emit()` 反转航向并给出隧道速度。拉丝层读的是 `CurrentSpeed` 而不是 `Speed`，所以减速、静止、隧道它都跟得上；否则玩家会停在类星体前面而介质还在狂飙。

**停多远是算出来的**，不是写死的数字。半径 R 的球在距离 d 处张开的半角是 `asin(R/d)`，所以想让它张开某个半角 a，距离就是 `R / sin(a)`。`arrivalScreenFill = 2.2` 表示「张开 2.2 倍的垂直视场」—— 类星体从每个边缘溢出去，按下 A 的那一刻画面里没有别的东西。

R 从类星体的 Renderer bounds 读，视场从相机读，都不是手填的。**所以美术把类星体放大一倍，或者有人把 FOV 调掉，停靠距离会自己跟上** —— 写死 560 的话这两件事都会悄悄把玩家留在太远的地方。

---

## Phase 2：发射

没有新的拍子类型 —— C1 C3 是过场、C2 C5 是确认拍、C4 是 look-at，全是 Phase 1 用过的四个组件。新增的是三个只管效果的组件，靠拍子的 `onEnter` 连上去：

| 组件 | 职责 |
|---|---|
| `TutorialEmission_NEW` | 时序：加速 → 阈值 → 发射。速度斜坡、抖动幅度、航向反转、开拖尾 |
| `TutorialFlash_NEW` | 全屏闪色，颜色和时长由调用方给（Phase 3 的 D2 吸收还会用） |
| `TutorialCameraShake_NEW` | 相机**位移**抖动 |

**为什么抖动是位移不是旋转**：`FirstPersonLookRig_NEW` 每帧写 `transform.rotation`，任何别的东西写旋转都是在跟它抢 —— 而且会输，因为 rig 在 Update 里直接覆盖。位移没人占，所以可以在 LateUpdate 里独占，不用协调任何事。顺带它效果也更对：位移抖动读起来是「画面本身不稳」，正是分镜说的 “double outline = frame jitter, not a second UI layer”。

**航向反转是 Phase 2 的关键**。Phase 0–1 朝类星体飞；C4 要求玩家转身看到「类星体已经只是一个亮点」，这只有在光此刻正在**远离**它时才成立。所以发射会翻转航向，`TutorialTravel_NEW` 同时把 rig 的前向轴带过去 —— 否则 A 会一直把视角复位到玩家刚刚花八秒离开的那个方向。

`TutorialTravel_NEW` 因此开了一个口子：`SetCourse` 和 `SetSpeed`。GDD 在意的区分保住了 —— **没有任何输入路径通到它们**，唯一的调用者是 `TutorialEmission_NEW`。「发生在玩家身上的事」和「玩家操纵」是两回事。

C2 是全片唯一 A 不等于复位的拍子（它是发射）。`Beat_Confirm_NEW` 在拍子期间调 `lookRig.SetConfirmRecentres(false)`，退出时交还 —— 一处开关，不是一个到处扩散的特例。

---

## 下一步（Phase 3–4）

剩下的：

| Frame | 复用 | 需要新增 |
|---|---|---|
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
