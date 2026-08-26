| 曾经的依赖 | 处理方式 | 代价 |
|---|---|---|| `PhotonSpectrumTrail` | **删掉兜底查找** —— `targetTrail` 场景里已显式赋值，为找一个组件而引用旧类型是唯一的牵连 | 5 行 |
| `LayerDefinitionTest` | 漂移检查改用 **`SerializedObject` 按字段名读**，靠 schema 识别旧资产（有 `layerId` + `nebulaColorDark`）。旧资产删掉后工具自动找不到，而不是编译失败 | 重写一个 editor 文件 |
| `DarkMatter` | 复制成 `DarkMatter_NEW`，偏转数学逐行照抄 | 147 行 |
| `UIImageLineGraphEffect` | 复制成 `LineGraphRenderer_NEW`，顶点布局不变 | 139 行 |
| `UniverseJourneyTracker` + `UniverseJourneyHUD` | **整体搬进 `Journey/`，GUID 不变、一行未改** | 见下 |# _Core_NEW — 新架构

**状态：已接入 `Assets/Scenes/PlaytestBuild_NEW.unity`（原场景的副本）。**

`PlaytestBuild.unity` 一字未改 —— `git diff` 已确认。新场景里旧组件全部 `m_Enabled: 0`（禁用不删除，随时可切回对照）。

## 首次打开必读

Unity 从未导入过这些文件，`.meta` 是手工生成的。**第一次打开务必按顺序做**：

1. 打开 Unity，等待导入完成
2. 看 Console —— 应当零 error
3. 打开 `PlaytestBuild_NEW`，检查 Hierarchy 里没有 `Missing (Mono Script)`
4. 抽查 Inspector：`GameManager` 上应有 5 个 `_NEW` 组件且字段已填好
5. 进 Play

**如果出现 Missing Script**：说明手写的 meta GUID 与 Unity 生成的不一致。删掉 `Assets/_Core_NEW/**/*.cs.meta` 让 Unity 重新生成，然后手动重连场景引用 —— 或者直接删掉 `PlaytestBuild_NEW` 重来，原场景不受影响。

---

## asmdef：阻碍已经消除，但还没加

原本 `_Core_NEW` 需要引用默认程序集里的 `UniverseJourneyTracker` 和 `DarkMatter`，而 asmdef 无法反向引用默认程序集 —— 加了就编译失败。

**这个阻碍现在没有了**：`DarkMatter` 已复制成 `DarkMatter_NEW`，`UniverseJourneyTracker` 连同 `UniverseJourneyHUD` 已经搬进 `Journey/`。当前 `_Core_NEW` **对外零编译依赖**（除引擎与包）。

加 asmdef 现在是可行的，因为预定义程序集（`Assembly-CSharp`）会自动引用所有 asmdef —— 留在 `Assets/Scripts/` 里那几个仍然引用 `UniverseJourneyTracker` 的旧脚本不会因此中断。

还没加，是因为它需要显式列出包引用（Cinemachine、UGUI、TextMeshPro、Video），配错就是一屏编译错误。**建议等新场景验收之后作为独立一次改动来做**，那时可以顺便验证「除了这些包，真的不依赖别的」。

---

## 目录

```
_Core_NEW/
├─ Data/          ScriptableObject 与纯数据（无逻辑）
│   ├─ LayerProfile_NEW        一层的参数（54 字段 → 16）
│   ├─ NebulaProfile_NEW       16 个 nebula shader 参数
│   ├─ SpectrumProfile_NEW     LAF 森林与连续谱参数
│   ├─ LayerCatalog_NEW        layerId 查找表
│   ├─ MilestoneSet_NEW        里程碑清单，每条自带 sprite
│   ├─ TransitionTiming_NEW    锚点 + 偏移（枚举与数据结构）
│   ├─ HierarchyBadge_NEW      标签属性（运行时，仅两个字符串）
│   ├─ BadgeRegistry_NEW       标签覆盖表
│   └─ DebugView_NEW           调试可视化总开关（三个静态 bool）
├─ Core/          状态核心（不碰相机 / 渲染 / 速度）
│   ├─ LayerGate_NEW           门板触发器
│   └─ LayerState_NEW          唯一权威 + 唯一事件源
├─ Responders/    响应层（订阅锚点，各管一件事）
│   ├─ LayerResponder_NEW      基类：订阅、退订、偏移排期
│   ├─ CameraDirector_NEW      唯一持有时间的脚本
│   ├─ WorldSwitcher_NEW       渲染层开关
│   ├─ NebulaResponder_NEW     nebula 材质
│   ├─ SpeedResponder_NEW      速度倍率
│   ├─ SpectrumResponder_NEW   LAF 参数分流
│   ├─ JourneyResponder_NEW    相位钉住
│   └─ EarthCutscene_NEW       结局视频
├─ Player/
│   ├─ PlayerRig_NEW           玩家控制器
│   ├─ OrbitCameraRig_NEW      FreeLook 输入与回正（去三倍重复）
│   ├─ DarkMatterBend_NEW      弯曲计算（普通类，不是组件）
│   ├─ DarkMatterRegistry_NEW  DarkMatter 缓存
│   └─ DarkMatter_NEW          暗物质球体（偏转数学照抄）
├─ Spectrum/
│   ├─ AbsorptionField_NEW     吸收缓冲区（分辨率可调）
│   ├─ SpectrumHUD_NEW         光谱折线图（模板烘焙成 LUT）
│   └─ LineGraphRenderer_NEW   UI 网格折线渲染器
├─ Journey/
│   ├─ UniverseJourneyTracker  距离 / 相位 / 尺度模型（整体搬入，未改写）
│   ├─ UniverseJourneyHUD      纵向进度条（整体搬入，未改写）
│   ├─ MilestoneRelay_NEW      读距离、发里程碑
│   └─ MilestoneCardHUD_NEW    卡片显示（队列 + 淡入淡出）
├─ Debug/
│   ├─ LayerDebugJump_NEW      传送 + F1 屏显
│   └─ CameraAimGizmo_NEW      相机朝向（受 DebugView 控制）
├─ Editor/        编辑器工具，不进包体
│   ├─ HierarchyBadges_NEW     Hierarchy 标签绘制
│   ├─ SystemMapWindow_NEW     全场景组件清单
│   ├─ ProfileDriftCheck_NEW   新旧资产数值比对
│   └─ DebugViewMenu_NEW       调试开关菜单 + EditorPrefs 持久化
└─ Assets/        生成好的资产（7 LP + 7 NP + 7 SP + Catalog + MilestoneSet + BadgeRegistry）
```

---

## 数据流

```
LayerGate_NEW ──RequestEnter──▶ LayerState_NEW
                                     │ OnLayerChanged
                                     ▼
                              CameraDirector_NEW
                                     │ 闭环等待 Cinemachine 混合权重
                                     │ OnAnchor(SequenceStart / CoverReached / LookUpStart)
                    ┌────────────────┼────────────────┐
                    ▼                ▼                ▼
            WorldSwitcher_NEW  NebulaResponder_NEW  SpeedResponder_NEW
```

`JourneyResponder_NEW` 直接订阅 `LayerState_NEW`，**不等锚点** —— 相位只驱动 HUD 文字，等遮罩会让标签滞后好几秒。

---

## 场景层级

新组件**不跟着旧场景的历史布局走**。旧版把渲染控制器挂在 Main Camera、把调试工具混在 GameManager 里、把 Earth 过场挂在一扇门上 —— 那是长年累积的结果，不是设计。新场景按职责归位：

```
[Systems]                        ← 新增根节点，整个系统层的入口
├── LayerCore
│     LayerState_NEW             状态权威
│     CameraDirector_NEW         时序总导演
├── LayerResponders
│     WorldSwitcher_NEW          渲染层开关
│     NebulaResponder_NEW        天空
│     SpeedResponder_NEW         速度
│     JourneyResponder_NEW       相位
│     EarthCutscene_NEW          结局视频
├── DataSources
│     UniverseJourneyTracker     距离 / 相位（旧脚本，仍是最佳实现）
│     LymanAlphaAbsorptionCtrl   吸收场（旧脚本）
└── DebugTools
      LayerDebugJump_NEW         传送
```

`DataSources` 里那两个是**旧脚本**，但它们仍在工作、也是新架构的依赖，所以按职责归到这里而不是留在 `GameManager`。`GameManager` 现在只剩四个已禁用的旧 Responder，可以整体删除。

**只有两处例外，因为它们被 Unity 的组件依赖钉死**：

| 组件 | 必须挂在 | 原因 |
|---|---|---|
| `PlayerRig_NEW` | `Player` | `[RequireComponent(CharacterController)]` |
| `LayerGate_NEW` ×6 | 各自的门 | `[RequireComponent(Collider)]`，触发事件只发给自己的碰撞体 |

这样分组的三个好处：把 `LayerResponders` 整个物体禁掉就能一次关掉全部视觉响应做 A/B；`DebugTools` 出包前整体剔除；Inspector 里不会出现十个组件堆在一个物体上的墙。

`[Systems]` 的 RootOrder 是 21，排在现有 16 个根节点之后。

已禁用的旧组件共 17 个：旧状态机、四个旧 Responder、旧渲染控制器、旧玩家控制器、六个旧触发器、旧调试传送、两份 `EarthLayerResponderTest`、`LAFLayerResponder`。全部保留在原位，勾回去即可对照。

> **仍留在 `GameManager` 上的是旧脚本**：`UniverseJourneyTracker`、`LymanAlphaAbsorptionController`。它们还在工作、新架构也依赖前者，所以这次没动。等旧组件整体清理时可以一并收进 `[Systems]`。

**手感相关的数值是从场景里抄的，不是代码默认值** —— `xSensitivity: 100`、`ySensitivity: 0.2`、`resetXSpeed: 50`（代码默认分别是 300 / 2 / 180，用默认值会明显变手感）。

调试传送键位已按关卡顺序排好：`1`=Macro `2`=Micro `3`=CosmicWeb `4`=MilkyWay `5`=SolarSystem `6`=Earth。

---

## 手工接线步骤（重来或新建场景时用）

### 1 · 建资产（已生成在 `_Core_NEW/Assets/`）

| 菜单 | 建几个 | 说明 |
|---|---|---|
| `Journey NEW / Nebula Profile` | 7 | 从对应的 `LD_*.asset` 抄 16 个 nebula 数值 |
| `Journey NEW / Layer Profile` | 7 | 见下方字段对照表 |
| `Journey NEW / Layer Catalog` | 1 | **entries 按关卡顺序排**：Quasar, Macro, Micro, CosmicWeb, MilkyWay, SolarSystem, Earth |
| `Journey NEW / Milestone Set` | 1 | 阈值降序；每条挂自己的 sprite |

**LayerProfile_NEW 字段从哪来**

| 新字段 | 旧来源 |
|---|---|
| `layerId` | 同名 |
| `isStartLayer` | 旧 `isDefaultLayer`（只有 `LD_Quasar` 为 true） |
| `zoneSpeedMultiplier` / `speedBlendDuration` | 同名 |
| `lockLookInput` | 旧 `lockLookInputDuringLookDown` |
| `lookDownHoldDuration` / `lookUpStartDelay` | 同名 |
| `useLookBackSequence` / `lookBackOrbitCountdown` / `lookBackOrbitDuration` | 同名 |
| `phaseOnEnter` | 同名 |
| `renderSwapDuration` | 旧 `transitionDuration` |
| `nebula` | 指向新建的 NebulaProfile |
| `timing` | 保持默认（三个通道全在 `CoverReached`） |

旧资产里的 `flare*` / `filament*` / `laf*` / `returnSpeedMultiplier` / `phaseOnExit` / `universeLayer` **不需要迁移** —— 审查确认它们在场景里没有任何消费者。

### 2 · 玩家

在 Player 上加 `PlayerRig_NEW`，禁用旧的 `DarkMatterPlayerControllerTest`（**禁用，不要删**，方便回滚）。

- `orbit.orbitCameras` 填 `VCam_Macro_Freelook`、`VCam_Micro_Freelook`、`VCam_SolarSystem`
- 其余数值保持默认 —— 已按原值填好（speed 2.5 / min 0.1 / max 3.5 / xSens 300 / ySens 2 / deadband 0.1）

### 3 · GameManager

加这五个组件，禁用对应的旧组件：

| 新组件 | 关键字段 |
|---|---|
| `LayerState_NEW` | catalog |
| `CameraDirector_NEW` | brain、coverVcam = `VCam_TopDown`、lookBackVcam = `VCam_Lookback_Freelook`、defaultZoneVcam = `VCam_Micro_Freelook`；`zoneCameras` 加两行：`SolarSystem → VCam_SolarSystem`、`Earth → VCam_SolarSystem` |
| `SpeedResponder_NEW` | player |
| `JourneyResponder_NEW` | tracker |
| `NebulaResponder_NEW` | 默认即可 |

### 4 · 渲染层

在 Main Camera（或搬到 GameManager）上加 `WorldSwitcher_NEW`，照旧的六组抄：

```
Quasar      → Quasar Layer          visibleOnStart ✔
Macro       → EarlyCosmicWebLayer
Micro       → GalaxyLayer
CosmicWeb   → CosmicWeb
MilkyWay    → MilkyWayLayer
SolarSystem → SolarSystem
```

Earth 没有组是**有意的**（结局是视频过场）。新版遇到找不到的 layerId 会保持现状并打一条 warning，不会像旧版那样把整个世界隐藏。

### 5 · 六扇门

每扇门加 `LayerGate_NEW`（layerId 照抄），禁用旧的 `LayerTriggerVolumeTest`。

### 6 · 里程碑（**尚未接线，需要你手动做**）

`MilestoneSet_New.asset` 已经生成好，里面是从代码默认值提取的 **24 条**完整里程碑（含描述与配色）—— 场景里那份只有 7 条的旧数据从此不用管了。

要接上：

1. 在 `GameManager` 加 `MilestoneRelay_NEW`，`milestones` 指向 `MilestoneSet_New`
2. 在 `MilestoneHUD` 那个物体上加 `MilestoneCardHUD_NEW`，填 `Image` 和 `CanvasGroup`
3. 给 24 条各挂一张 `card` sprite（现有 18 张按 label 对上去，缺 6 张）
4. **禁用旧的 `MilestoneHUD`**，否则卡片会放两遍

没有自动接是因为卡片 sprite 必须人工按 label 对应，机器对不了。

---

## 验收清单

| 检查 | 期望 |
|---|---|
| 开场 | Quasar 世界可见，倒计时后自动转身 |
| 第一扇门 | 转身完成后混合到 Macro，世界已经换好 |
| 第 2~6 扇门 | 相机俯视到位的**同一帧**，渲染层 + 天空一起换；速度同时开始变 |
| 抬头 | 看到的是完整换好的世界，天空不再"补间到一半" |
| 手感 | W/S 加减速、鼠标/右摇杆转视角、空格回正 —— 与旧版一致 |
| Console | 无 error；`debugLog` 默认全关 |
| 退出 Play | Scene 视图里材质与 skybox 参数没有被改动 |

---

## 依赖审计

新架构对外**零编译依赖**（剥掉注释与字符串后统计的实际代码引用）。曾经的 5 个依赖已全部处理：

| 曾经的依赖 | 处理方式 | 代价 |
|---|---|---|
| `PhotonSpectrumTrail` | **删掉兜底查找** —— `targetTrail` 场景里已显式赋值，为找一个组件而引用旧类型是唯一的牵连 | 5 行 |
| `LayerDefinitionTest` | 漂移检查改用 **`SerializedObject` 按字段名读**，靠 schema 识别旧资产（有 `layerId` + `nebulaColorDark` 就是）。旧资产删掉后工具自动"找不到"，而不是编译失败 | 重写一个 editor 文件 |
| `DarkMatter` | 复制成 `DarkMatter_NEW`，偏转数学逐行照抄 | 147 行 |
| `UIImageLineGraphEffect` | 复制成 `LineGraphRenderer_NEW`，顶点布局不变 | 139 行 |
| `UniverseJourneyTracker` + `UniverseJourneyHUD` | **整体搬进 `Journey/`，GUID 不变、一行未改** | 见下 |

**为什么 `UniverseJourneyTracker` 是搬而不是重写**：`UniverseJourneyHUD` 用了它 7 个成员且字段强类型，两者必须一起动 —— 合计 1,880 行，其中 943 行是运行时 UI 构建（节点布局、渐变纹理、自动隐藏协程），而它就是屏幕上那条可见的进度条。重写没有收益，只有风险。

搬动保留了 GUID，所以引用它们的 **15 个场景**全部照常工作；`_Core_NEW` 在文件层面自足；等旧场景和 Playground 归档删除后，命名也可以再统一。

其余全部是引擎与包内类型（Cinemachine / UGUI / TMP / VideoPlayer）。

### 新场景里仍然 enabled 的非 _NEW 脚本

全部是有意保留的，没有遗留：

```
UniverseJourneyTracker     距离 / 相位
UniverseJourneyHUD         纵向进度条
PhotonSpectrumTrail        拖尾光谱纹理
PhotonTrailController      拖尾吸收联动
UIImageLineGraphEffect     光谱图表渲染器
WorldSpaceUIPopup ×9       世界空间弹窗
BuiltInFinalUIOverlay      UIOverlay 层合成
AudioManager               BGM
RingMesh                   土星环网格
MilestoneHUD               ← 唯一待办：新的 MilestoneRelay/CardHUD 尚未接线
```

已禁用的遗留组件累计 **32 个**：旧 layer 栈 17 个 + 旧光谱 2 个 + 本轮清掉的 13 个
（`GraphFeeder`、`DarkMatterVal` ×5、`ForestHUDLine`、`UILineGraph`、`LymanAlphaModeTrigger`、`CameraForwardGizmo` ×6）。

其中 `GraphFeeder` 是最值得清掉的一个 —— 它挂在 **Player** 上，每秒 30 次往一个 GameObject 已禁用的图表写常量，并对 5 个 `DarkMatterVal` 做碰撞检测。

---

## 看清「什么挂在哪」

组件在 Hierarchy 里是不可见的 —— 要知道什么挂在哪，只能逐个点开看 Inspector。旧场景之所以会演变成「渲染控制器在 Main Camera、结局过场挂在一扇门上」，正是因为布局看不见，就会慢慢漂移。

五个工具解决这件事：

### 1 · Hierarchy 标签 —— 覆盖**每一个**脚本

`Editor/HierarchyBadges_NEW.cs` 在 Hierarchy 每行右侧画出该物体持有的组件：

```
[Systems]
├── LayerCore              [STATE] [CAMERA]
├── LayerResponders        [WORLD] [NEBULA] [SPEED] [JOURNEY] [SPECTRUM] [EARTH]
├── DataSources            [TRACKER] [ABSORB]
└── DebugTools             [DEBUG]
Player                     [PLAYER] [legacy]
Image                      [SPEC HUD] [GRAPH] [legacy] [orphan]
Layer_QuasartoMacro        [GATE] [legacy]
```

**它不认任何硬编码名单。** 标签按这个顺序解析：

| 顺序 | 来源 | 用在哪 |
|---|---|---|
| 1 | `BadgeRegistry_New.asset` | 你不想改的脚本：包内代码、旧系统、第三方 |
| 2 | `[HierarchyBadge_NEW("SPAWN", "#4A94F2")]` | **你自己写的脚本** —— 标签跟着类走，改名删除都不会留下孤儿条目 |
| 3 | 自动 | 从类名缩短出标签，颜色从类名哈希取色相 |

第 3 条意味着**明年写的新脚本不做任何配置也会显示**。前两条只是为了让它比类名更好读。

三种模式（`Tools > Journey NEW > Hierarchy Tags`）：

- **All scripts** —— 全部显示，未标注的以自动标签+变暗呈现（默认）
- **Annotated only** —— 只显示注册表和属性标注过的
- **Off**

其他特性：组件禁用时标签变暗；一行超过 4 个标签时收成 `+N`（悬停看全名）；**`Missing (Mono Script)` 会用红色 `MISSING` 标出**；注册表可以 `hide` 掉噪音类型（`CameraForwardGizmo` 挂在 6 个物体上，已隐藏）。

### 未来怎么自己标

**自己写的脚本 —— 加一行属性：**

```csharp
[HierarchyBadge_NEW("SPAWN", "#4A94F2")]
public class EnemySpawner : MonoBehaviour { }
```

颜色可省略，省略就按类名自动取一个稳定的颜色：

```csharp
[HierarchyBadge_NEW("SPAWN")]
```

**别人的脚本 / 不想改的文件 —— 在 `BadgeRegistry_New.asset` 加一行：**

| 字段 | 作用 |
|---|---|
| `typeName` | 类名，不含命名空间 |
| `label` | 留空则用自动标签 |
| `color` | 标签颜色 |
| `hide` | 永不显示（给挂在几十个物体上的噪音组件用） |
| `dim` | 画成暗色（标记「正在退役」） |

注册表**优先于属性**，所以可以在不动源码的前提下覆盖任何标签。资产由 `AssetDatabase` 自动发现，不需要在场景里挂引用。

### 2 · System Map 窗口

`Tools > Journey NEW > System Map` 打开一张**全场景**表：每个组件 → 它的完整层级路径 → 是否启用。**点一行就选中并高亮那个物体。**

分四段：架构分组（Core / Responders / Player / Data / HUD / Debug）、**Other scripts in scene**（其余全部脚本，不遗漏）、Legacy（已退役的旧栈）。

它还会主动报两类错：

- 本该唯一的组件出现了多次（`EarthLayerResponderTest` 就曾挂了两份驱动同一个 VideoPlayer）
- 旧组件里还有启用的（会和新架构打架）

Legacy 段顶部会给出结论，例如「19 个 legacy 组件全部已禁用，可安全删除」。

### 3 · Profile Drift Check —— 防止新旧资产悄悄分家

迁移时把 `LayerDefinitionTest` 的数值逐字段抄进了新资产。这意味着**同一组数字现在存在两个地方**，而且它们之间没有任何链接：去调 `LD_Micro.asset`，`NP_Micro.asset` 不会跟着变，两个场景就此分家，且**不会有任何报错**。

**约定**：旧资产从现在起冻结，只改新的；新场景签收后连同旧场景一起删掉，重复自然消失。

约定会被忘记，所以有 `Tools > Journey NEW > Profile Drift Check`：逐字段比对 25 个迁移过来的值，不一致就列出来，点一下跳到对应资产。

按 `layerId` 配对，不按文件名 —— 改名不会让比对失效，配不上的会被单独报出来而不是静默跳过。

比对范围：

| 来源 | 字段数 |
|---|---|
| `LayerProfile_NEW` ↔ `LayerDefinitionTest` | 11 |
| `NebulaProfile_NEW` ↔ `LayerDefinitionTest` 的 `nebula*` | 14 |

**唯一一处刻意的差异会被单独归类**（不算漂移）：`blendDuration` 0 vs 旧 `nebulaBlendDuration` 3 —— 切换发生在俯视遮罩下，补间玩家看不到，而抬头时还没跑完的补间会和已经换好的世界打架。

> `SpectrumProfile_NEW` 不在比对范围内。它的数值来自旧的 `LymanAlphaAbsorptionController` **组件**（不是资产），而且做过速度耦合的等效换算 —— 直接比对只会报出一堆假阳性。

### 4 · 一个开关管住所有调试可视化

gizmo、屏显、日志以前是散落在各组件上的三类布尔值，而且**默认全开** —— 结果是 Scene 视图每次重绘被 138 次 gizmo 调用埋掉，Console 被刷得看不见真东西。

现在全部先问 `DebugView_NEW`：

```csharp
public static class DebugView_NEW
{
    public static bool Gizmos;    // 场景标注：门体积、相机朝向
    public static bool Overlay;   // 屏显读数
    public static bool Verbose;   // 逐次切换的日志
}
```

两处控制：

- **编辑器**：`Tools > Journey NEW > Debug View`，三个可勾选项 + 一个 `All Off`。用 EditorPrefs 持久化，进 Play、重编译、重启编辑器都记得。
- **运行时**：`LayerDebugJump_NEW` 按 **F1** 切换 `Overlay`，并且（`driveGizmos` 默认开）同步切换 `Gizmos` —— 调试工具和它的可视化一起开关，而不是六个独立开关。

> Unity 的固有行为：`OnDrawGizmos` **只在 Scene 视图画**，Game 视图要另外打开它自己的 Gizmos 开关，build 里根本不会被调用。在这里打开 `Gizmos` 不会让任何东西出现在出包后的游戏里。

**`CameraForwardGizmo` → `CameraAimGizmo_NEW`**（6 个实例已替换）：

| | 旧 | 新 |
|---|---|---|
| 开关 | 每个组件自己两个 bool | 统一读 `DebugView_NEW.Gizmos` |
| 距离刻度球 | 默认开，6×20=120 个 | **默认关**（它们就落在已画出的射线上，只贡献刻度） |
| 每次重绘 gizmo 调用 | **138** | **18** |
| 出包 | 无 `#if UNITY_EDITOR`，gizmo 代码编进包体 | 绘制代码被排除；**类本身保留**，所以 build 里不会变 Missing Script |
| `[ExecuteAlways]` | 有，但此类只有 gizmo 方法，完全无作用 | 去掉 |

保留它而不是直接删，是因为 Cinemachine 只为 **live 相机和选中的相机**画视锥（`CinemachineBrainEditor:80/130`），**未选中的非 live 相机它不画** —— 摆场景时想一眼看到六个相机各自朝哪，这是它唯一还有的价值。

`LayerGate_NEW` 的门体积 gizmo 也接进了同一个开关。

### 5 · 运行时屏显

`LayerDebugJump_NEW` 现在有 OnGUI 面板，**F1 开关**：

```
LAYER DEBUG   F1 to hide
current   CosmicWeb
player Z  389.4
phase     CosmicWeb2
remaining 4.21 B ly

  1  Macro
  2  Micro
> 3  CosmicWeb
  4  MilkyWay
  5  SolarSystem
  6  Earth

· jumped to CosmicWeb
```

当前所在层用 `>` 标出，跳转反馈显示 2.5 秒后淡出。数字键按**关卡顺序**排列（旧工具按 catalog 顺序，键 3 指向没有门的 Quasar，按了没反应）。

> 两个 Editor 脚本在 `Editor/` 文件夹里，Unity 自动排除出包体，零运行时成本。`LayerDebugJump_NEW` 的 `enabledInBuild` 默认关闭，出包时自己禁用。

---

## LAF 光谱系统

两条链路共用一个吸收缓冲区，所以拖尾和 HUD 永远同步：

```
AbsorptionField_NEW  ── _final[2048]
      │                      │
      ├─ Texture2D ─────▶ PhotonTrailRainbow.shader   拖尾上的吸收暗线
      └─ SampleAbsorption ─▶ SpectrumHUD_NEW          HUD 折线图
```

### 修掉的六件事

| 问题 | 修法 |
|---|---|
| `TexWidth` 是 `const 256`，`lafDipWidthMin/Max`(0.0004/0.004) 在这个精度下都塌缩成 1 像素，所有吸收线粗细完全一样 | `resolution` 改成序列化字段，默认 **2048**。宽度区间恢复成 0.8–8.2 texel |
| 线宽以**像素**计，提分辨率会让线变细 | 改成**归一化**宽度（占光谱的比例），任何分辨率下观感一致，高分辨率只是让边缘更平滑 |
| HUD 每帧重算 1024 点 × 约 7 个 `Mathf.Exp` ≈ **7000 次指数运算**，只为重画一条形状不变、仅横向平移的曲线 | 模板烘焙成 4096 项 LUT，每帧变成查表 |
| `proceduralSampleCount: 1024`，而图表只有几百像素宽 | 降到 **512**，网格顶点减半 |
| `drawHighlightLine` 画第二遍完全相同的几何（`thickness` 和 `highlightThickness` 都是 0.1），顶点多 1/3 | 关掉，主线颜色改成两层的合成色 `(0.8575, 0.9525, 1)` —— 肉眼无差别 |
| 10 个 LAF 参数只有 1 个生效，其余都被送给了不管吸收线的 HUD | 拆成 `SpectrumProfile_NEW`，按归属分流：continuum → HUD，dip/seed/drift → AbsorptionField |

顶点数：**12,284 → 约 4,100**。

### 一处需要你知道的取舍

`referencePlayerSpeed` 原本是 **20**，而玩家 `maxSpeed` 只有 3.5 —— 速度耦合实际上是关闭的（乘数恒在 0.125 附近）。修成 3.5 之后耦合真的生效了，但同样的速度下漂移会快 2.6 倍。

为了不改变现有观感，`driftPerSecond` 和 `spawnRatePerSecond` 做了等效换算：

```
旧：ref=20  → idleScale=0.300 → drift = 0.02  × 0.300 = 0.00600
新：ref=3.5 → idleScale=0.771 → drift = 0.00778 × 0.771 = 0.00600   ← 巡航速度下完全一致
```

所以**巡航时看起来和以前一样，但现在加减速真的会影响光谱漂移**。如果想要更明显的速度反馈，调大 `SP_*.asset` 里的 `driftPerSecond` 即可。

### SpectrumProfile 的数值来源

**不是**从 `LD_*.asset` 的 `laf*` 字段抄的 —— 那些是死数据（`lafDipCount` 全为 0，照抄会让森林变空）。真实数值在旧 `LymanAlphaAbsorptionController` 组件上，7 个 profile 都是从那里换算来的，目前 7 层完全相同。**现在可以逐层调出不同的森林密度、深度和随机种子了** —— 这在旧结构下做不到。

### 剩下两件建议手工做的

1. **删掉禁用的 `Absorbtion Trail` 物体**。它用 `PhotonTrail.mat`（那个 shader 没有 `_AbsorptionLineTex`），`m_IsActive: 0`，是早期版本残留。Unity 里右键删除一秒的事 —— 用 YAML 删要同时改父节点的 children 列表，不值得冒险。
2. **注意场景里内嵌了一份 `Custom_PhotonTrail (Instance)` 材质**。这是有人在非 Play 模式下访问 `trail.material` 留下的，Unity 把实例化材质序列化进了场景。它现在能正常工作（两个 shader keyword 都对），但**改 `Assets/Shaders/Custom_PhotonTrail.mat` 资产不会影响场景里的拖尾** —— 两者已经脱钩。要重新挂钩就在 Inspector 里把材质槽重新指向那个资产，但要先确认两者数值一致。

---

## 相机优先级：一个必须记住的坑

`CameraDirector_NEW` 必须管到场景里**每一个** Priority 可能超过 `priorityHigh`(50) 的虚拟相机。漏掉一个，整套序列会静默失效。

第一次接线就踩了：`VCam_Macro_Freelook` 在场景里的初始 Priority 是 **100**，而它既不是 cover、不是 lookBack、也不在 `zoneCameras` 里 —— 于是它一直压过 look-back 相机(50)，**开场向后看的镜头完全没有出现，而且没有任何报错**。旧代码是靠 `SetPriority(vcamMacro, PriorityNormal)` 手动压到 10 才成立的。

修法有两条，都做了：

1. `zoneCameras` 补上 `Macro → VCam_Macro_Freelook`。这同时修好第二个问题 —— 第一扇门之后应该切到 `VCam_Macro`（旧 `QuasarToMacroSequence` 的终点），而不是 `defaultZoneVcam`。
2. `CameraDirector_NEW.Awake()` 增加 `WarnAboutUnmanagedCameras()`：扫描场景里所有独立虚拟相机（FreeLook 的隐藏子 rig 通过 `ParentCamera` 过滤掉），凡是 Priority ≥ `priorityHigh` 又不在管理范围内的，直接报 warning 并指出物体。

**以后往场景加任何 vcam，Console 会主动提醒。** 优先级冲突在运行时完全不可见，只能靠这种主动检查。

当前五个相机的接管状态：

| 相机 | 场景 Priority | 归属 |
|---|---|---|
| `VCam_Macro_Freelook` | 100 | `zoneCameras[Macro]` |
| `VCam_Lookback_Freelook` | 30 | `lookBackVcam` |
| `VCam_Micro_Freelook` | 20 | `defaultZoneVcam` |
| `VCam_SolarSystem` | 20 | `zoneCameras[SolarSystem]` + `[Earth]` |
| `VCam_TopDown` | 0 | `coverVcam` |

---

## 与旧版的行为差异（都是刻意的）

| 项 | 旧 | 新 |
|---|---|---|
| 遮罩等待 | 写死 3 秒（5 次里 4 次算错） | 闭环等真实混合权重 |
| Nebula 时机 | 门触发瞬间就开始变 | 遮罩到位时换 |
| Speed 时机 | 写死延迟 3 秒 | 遮罩到位时开始 |
| skybox 材质 | 直接写磁盘资产 | 运行时克隆，退出还原 |
| 找不到 layerId 的渲染组 | 静默隐藏全世界 | 保持现状 + warning |
| 打断序列时的输入锁 | 无条件解锁（可能误解 orbit 的锁） | 只解开自己上的锁 |

**手感相关的三项刻意保持原样**：空格回正不受相机锁约束（`blockResetWhileLocked` 默认 false）、暗物质弯曲数学逐行照抄、`Update` 六步顺序不变。

---

## 已知未覆盖

| 项 | 原因 |
|---|---|
| LAF 光谱参数分流 | `LAFSpectrumHUD.Configure()` 只接受旧的 `LayerDefinitionTest` 类型，改它就得动旧文件 |
| 吸收场分辨率 | `TexWidth` 是 `const int = 256`，`lafDipWidthMin/Max` 在这个精度下都塌缩成 1 像素。要真正调细必须改旧文件里的这个常量 |
| Flare | 场景里没有 `CameraFlareFlash`。`timing.flare` 通道已经留好，接一个响应器即可 |
| Earth 视频过场 | 现有 `EarthLayerResponderTest` 仍可用，但场景里挂了两份，接线时只保留 `Layer_SolartoEarth` 上那一份 |
