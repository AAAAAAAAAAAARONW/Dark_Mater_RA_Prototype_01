# Tutorial — Journey of Light

实现 [Tutorial GDD — Journey of Light](https://bird-tune-e6c.notion.site/Tutorial-GDD-Journey-of-Light-3d3abeb8074b817b8f93e22777f5888f)。

**本轮范围：Phase 0（A1–A3）+ Phase 1（B1–B4），加上它们前面的 attract 状态。**
Phase 2–4 尚未实现，但拍子框架、控制方案和 HUD 都是按整套 20 帧设计的 —— 见文末「下一步」。

---

## 五分钟跑起来

1. 打开 `Assets/_Core_NEW/Tutorial.unity`
2. 菜单 `Tools > Journey NEW > Build Tutorial Scene (Phase 0-1)`
3. Play

看到的顺序：标题卡 →（按 A）→ 20 秒黑暗，第 15 秒一颗光点从右边缘飘出 → 转右抓住它 → 抬头 → 转身看到吸积盘 → A 提示出现 → 按 A 视角回正。

调试：`F2` 跳过当前拍子，左上角有当前 beat / 门控状态 / 空闲计时。两者都受 `DebugView_NEW.Overlay` 控制（`Tools > Journey NEW > Debug View`），出包时一起关掉。

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
├─ TutorialSky_NEW            把 NP_Quasar 写进 Custom/Nebula 天空盒
├─ TutorialDrift_NEW          随吸积盘漂移（光子拖尾靠它才画得出来）
├─ Beats/
│   ├─ Beat_Cinematic_NEW     A1 A2 A3（无动作，按分镜时长走）
│   ├─ Beat_LookAt_NEW        B1 B2（把目标带进准星）
│   ├─ Beat_TurnAround_NEW    B3（转过 150° 且盘在画面里）
│   └─ Beat_Confirm_NEW       B4（按 A）
└─ Editor/
    ├─ TutorialSceneBuilder_NEW   一键搭场景（布局）
    └─ TutorialWorldAssets_NEW    资产查找与生成（取材）
```

运行时依赖：引擎、UGUI、`_Core_NEW` 的 `DebugView_NEW` / `HierarchyBadge_NEW` / `NebulaProfile_NEW`，以及 **`Assets/Scripts/PhotonSpectrumTrail.cs`**（旧脚本，光子拖尾的光谱生成，刻意复用而不是重写）。**不依赖 Cinemachine** —— 教程是第一人称，不走 FreeLook。

---

## 场景里看得见的东西是从哪来的

第一版 builder 只生成灰色白球、天空盒还留在 Unity 默认值上，结果整个场景渲染出来是一片蓝灰虚空。现在屏幕上的每一样东西都来自工程里已有的资产：

| 元素 | 资产 | 负责哪几帧 |
|---|---|---|
| 天空 | `Materials/Custom_Nebula.mat` + `_Core_NEW/Assets/NP_Quasar.asset` | 全程。和主旅程的类星体层**共用同一份数值**，不会分家 |
| 类星体 | `Materials/BlazingQuasar.mat` | A1 的流、A2 的暗斑、**B2 的 jet channel**、B3 的盘与黑洞剪影 —— 四样都是这一个物体 |
| 光束（玩家自己） | `Shaders/Custom_PhotonTrail.mat` + `Scripts/PhotonSpectrumTrail.cs` | 全程。玩家就是光 |
| 吸积盘尘埃 | 生成的 `TutorialDiscDust.mat` + `TutorialSoftDot.png` | A1「深处缓慢旋转的暗红物质」 |
| 光点 | 生成的 `TutorialMote.mat` | A3 B1 B2 |

`BlazingQuasar.shader` 值得单独说一句：它的 Properties 里直接有 `_AccretionDisk`、`_JetColor / _JetWidth / _JetLength`、`_SpinSpeed`、`_CoreRadius`，**吸积盘、双极喷流、核心亮斑在一个 shader 里**。喷流沿物体局部 Y 轴，盘在垂直于 Y 的平面上 —— builder 靠一个 `QuasarEuler` 把它转到需要的朝向。

### 三个渲染上的坑，都已经处理

1. **天空盒必须在编辑期写进场景。** `RenderSettings` 是场景数据。只在运行时设置的话 Scene 视图仍是默认天空，截图全是错的。builder 在搭场景时就写。
2. **`NP_Quasar` 是克隆后再写。** `NebulaResponder_NEW` 的注释记着这个教训：曾经直接写 `RenderSettings.skybox`，那是磁盘上的 .mat，一次 playtest 永久改掉了材质、场景不再可复现。`TutorialSky_NEW` 克隆一份运行时实例，销毁时还原。
3. **拖尾材质用的是副本，不是共享资产。** `PhotonSpectrumTrail` 是 `[ExecuteAlways]`，编辑期就往 `sharedMaterial` 写生成的光谱贴图 —— 直接挂 `Custom_PhotonTrail.mat` 的话，光是**打开场景**就会改写主旅程在用的材质。builder 复制出 `TutorialPhotonTrail.mat` 再挂。PlaytestBuild 用的是场景内嵌实例，同一个规避方式。

### 为什么玩家会漂移

`TutorialDrift_NEW` 让玩家绕类星体自转轴缓慢公转。这不违反「玩家不能操控位移」—— GDD §4 的原文是 *never translates **under their own control***，被吸积盘的流带着走不是操控。它换来三样东西：

- **拖尾画得出来。** `TrailRenderer` 不动就不吐顶点。完全静止的玩家 = 一条看不见的光，而整个前提是玩家就是光。
- **视差。** 没有地平线、没有已知尺度参照物的场景里，近处尘埃相对远处盘的运动是唯一的深度线索。
- **A3 按原文成立。** 「光点从右边缘飘出画面」需要玩家和光点之间有相对运动。

速度取在「刚好能读出是在动」的下限。观众如果把它注意成「移动」，就是太快了。

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

### 3 · B1 之前屏幕上什么都没有

`TutorialHUD_NEW` 里每个元素的默认状态都是关，由具名拍子打开：

| 顺序 | 元素 | 首次出现 | 之后 |
|---|---|---|---|
| 1 | 控制图例 | B1 | **永不消失**，代码里没有隐藏它的路径 |
| 2 | 准星 | B1 | 跟图例一起 |
| 3 | A 提示 | B4 | 按下即隐 |

图例文案取自 GDD §4，一字不改：

```
STICK = LOOK      A = CONFIRM / RECENTRE
```

准星不在 GDD §7 的 UI 清单里，因为它不是玩家需要学的界面元素。但 B1 的门控是「光点保持在准星内」，准星不可见这条门就没法玩，所以它跟图例一起到场。

---

## 拍子与门控对照

| Frame | 组件 | 门控 | 实现要点 |
|---|---|---|---|
| A1 | `Beat_Cinematic_NEW` | 无，8 秒 | `onEnter` 挂 VO |
| A2 | `Beat_Cinematic_NEW` | 无，7 秒 | `onHalfway` 用于「亮度升起来」这种帧内变化 |
| A3 | `Beat_Cinematic_NEW` | 无，5 秒 | `onEnter` 激活光点；`onRumble` 见下方「已知缺口」 |
| B1 | `Beat_LookAt_NEW` | 光点进入准星 | `holdSeconds = 0`。GDD 明写 not a dwell timer |
| B2 | `Beat_LookAt_NEW` | 第二颗光点进入画面 | 同上，目标在上方 |
| B3 | `Beat_TurnAround_NEW` | 转过 150° **且**盘在画面里 | 两个条件都要 —— 见下 |
| B4 | `Beat_Confirm_NEW` | 按下 A | 按下即满足，回正 lerp 继续跑进下一拍 |

**B3 为什么两个条件都要**：只判角度的话，一个低头发呆、摇杆漂移的玩家也能过。而 B3 是空间定位落地的地方，GDD 给它的批注是 “Protect it.”。一条能在没看见盘的情况下通过的门，什么都没保护到。

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
| **A1/A2 与 B3 的取景冲突** | **需要你按 Figma 分镜定夺** | 见下 |

### A1/A2 与 B3 在第一人称自由视角下不能同时字面成立

A1/A2 写的是画面里**已经有**流动的物质、以及「中心一块比黑更黑的斑」；B3 写的是黑洞剪影与完整吸积盘**第一次被看见**。开场画面里有的东西，就已经被看见了 —— 这两条在一个不锁相机的第一人称场景里互斥。

**当前默认按 B3 处理**，因为 GDD 给 B3 的批注是 “Protect it.”，而 A1 的要求靠尘埃就能满足：类星体放在**后方偏左偏下**，近到玩家处在外盘之内，所以开场正前方满是漂移的物质，但核心本身在画面外。

分镜（Figma）才是取景的权威。所有位置都是 `TutorialSceneBuilder_NEW` 顶部的常量、也是场景里的 Transform，改一下拖一下都行。

---

## 为什么有一个搭场景的编辑器工具

`Tutorial.unity` 原本是空场景。Phase 0–1 是 7 个拍子、一套第一人称 rig、3 个 HUD 元素、一张标题卡和 2 颗光点，彼此之间全是引用 —— 手接大约 40 次拖拽，也就是 40 次把 B2 的门指到 B1 光点上、然后花一下午找原因的机会。

`_Core_NEW/README_NEW.md` 已经记录过手工累积布局的结果：它会漂移，而且没人看得见它在漂移。

所以布局写成代码，放在能读、能重跑的地方。重复运行会被拒绝而不是叠加 —— 删掉 `[Tutorial]` 根节点再跑一次就得到干净的一套。

生成的层级：

```
[Tutorial]
├── Environment                    TutorialSky_NEW（天空盒 + 环境光；顺手关掉方向光）
├── Photon                         TutorialDrift_NEW（玩家本体）
│     ├── Camera                   FirstPersonLookRig_NEW（复用场景里已有的 Main Camera）
│     └── Trail                    TrailRenderer + PhotonSpectrumTrail ← 这就是光束
├── World
│     ├── Quasar                   BlazingQuasar.mat，后方偏左偏下，带倾角
│     ├── DiscDust                 单个 ParticleSystem，圆环形，缓慢公转
│     ├── Mote_A3_B1               右前方约 28°，A3 出现，B1 抓住
│     └── Mote_B2                  上方约 55°，在喷流里，B2 出现
├── HUD                            Canvas + TutorialHUD_NEW + TutorialAttract_NEW
│     ├── Reticle / Legend / ConfirmPrompt / AttractCard
└── Director                       TutorialDirector_NEW
      └── Beats
            A1 A2 A3 B1 B2 B3 B4
```

尘埃用的是**一个** ParticleSystem，不是现成的 VFX prefab。工程里有 `RFX_Nebula` 系列，但每个是二十多个粒子系统堆出来的星系外观，而 GDD §10 item 3 已经把 Carnegie 硬件上的帧数列为待议项。一个圆环发射器 + 轨道速度就是吸积盘的形状，代价是一个 draw call。要更华丽的观感，把 `DiscDust` 换成 RFX prefab 即可。

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
