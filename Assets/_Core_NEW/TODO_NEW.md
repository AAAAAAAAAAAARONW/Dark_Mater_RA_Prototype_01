# 待办

做完一项就划掉或删掉。最新的放最上面。

---

## 🟡 第一个 cosmic web：暗物质时刻 + 拉长 + 摆纤维（10 月 16 日）

**改了什么：** 光**不再停下**。J3 说完后，旁白讲暗物质，提示 `Y · SHOW DARK MATTER` 和这句一起出现，玩家边飞边按 Y。没按的话，那句说完 1.5 秒提示一次，6 秒后自动切换。为了放得下这一分钟的台词，第一个 cosmic web 要**拉长**（速度不变）。新加了 **Flight Path** 窗口：在 Scene 里画出光的路径、秒数刻度、每句台词的位置，以及自动找到的黄色纤维。

**代码已写好、编译通过（Unity 外编译）。下面按顺序做：**

### 0 · 先按阶段整理场景（防止拖错）

1. `Tools > Journey NEW > Layout > Stages`，点 **Group scene by stage**。Hierarchy 里会多一个 `Journey Stages`，下面 `1 · Quasar` … `7 · Earth`，每个阶段放着：它的世界（render layer）、结束它的门、它里面的触发框（J3b 纤维、galaxy edge、Earth arrival…）。世界里的位置一点都不变，Ctrl+Z 可以撤销。**Ctrl+S 保存。**
2. 要调哪个阶段，就点那一行的 **Focus**：Scene 视图里只能点到这个阶段的门和触发框，web、其他阶段、相机都点不到。要挪 web 本身时点 **Whole stage**。做完点 **Unlock all**。
3. 每个阶段下面还列出了相关设置（暗物质时刻、红移演示、dive、旁白），点 **Select** 直接跳过去。
4. 以后新加了触发框，点 **Re-group** 会把它放进对应的阶段（按位置判断）。

### 1 · 更新暗物质时刻

1. 回到 Unity，等编译完，Console 里没有红色 error。
2. 打开 `Gameplay_Scene_Ana`。`DarkMatterStop` 物体上的组件现在叫 `DarkMatterMoment_NEW`（同一个组件改了名，接线都还在）。可以把物体改名为 `DarkMatterMoment`，不改也行。
3. 选中它，在 Inspector 里确认 **Version**、**Auto After Line = 6**、**Nudge After Line = 1.5**、**Hint With Before Line ✔**。

### 2 · 先看看现在的路径和时间

1. 打开 `Tools > Journey NEW > Flight Path`。Scene 视图里会出现光的路径：每层一个颜色，每秒一个小刻度，每 5 秒一个带 `+5 s` 的刻度；**黄色加粗的地方是光穿过黄色纤维**，旁边标 `F1 Macro +12.3 s`。
2. 窗口里 **Record in Play mode** 默认打开。进 Play，按 **2** 跳到 Macro，飞到 galaxy 门为止，然后停止 Play。窗口和 Scene 里会出现每句台词的蓝色条（从哪开始、到哪结束）和 Y、nudge、自动切换这些点。
3. 看第一个 cosmic web 的总时长和台词是不是被下一个门截断。如果 Console 里有 `[DarkMatterMoment_NEW] Ended early`，就说明太短了。

### 3 · 拉长第一个 cosmic web

1. `Tools > Journey NEW > Layout > Lengthen First Cosmic Web…`。
2. 窗口显示现在的长度和秒数（约 130 单位 ÷ 5 单位/秒 ≈ 26 秒）。在 **Seconds wanted** 填想要的秒数（默认 62），点 **Set extra**，会算出要加的长度（约 +175）。
3. **The web itself** 选一种：
   - **Scale**（默认）：web 整体等比放大，长得和原来一样，只是更大；光在里面显得更小、更慢。
   - **Stretch**：只沿路径方向拉长，纤维沿路径方向变长。
   - **Leave**：web 不动，新加的那段可能是空的，需要你手动补。
4. 看下面的列表（会被往后挪的物体），点 **Lengthen by …**。去 galaxy 的 dive 会自动跟着调整，仍然对准原来那团黄色星系团。
5. **Ctrl+S 保存场景。** 不满意就 Ctrl+Z。

### 4 · 把 J3b 放到一条纤维上

1. 回到 Flight Path 窗口。Macro 下面列着找到的纤维：`F3  +41.2 → +41.8 s  z 341.0  peak 0.90`。
2. 挑一条在**暗物质那句说完之后**的纤维（对照列表里 VO 那几行的结束时间），点 **Frame** 在 Scene 里看它，满意就点 **J3b here**，触发框会挪过去、贴合纤维厚度。
3. 找不到合适的：调 **Threshold**（越低找到的越多）和 **Probe radius**（离光中心多远的气体也算）。
4. Ctrl+S 保存。再进 Play、按 2 飞一遍：穿过那条纤维时光谱条上冒出新线，说 J3b。录下来之后，Scene 里那个点会和黄色标记对上。

### 录音

剧本 `VO_Script_Draft_v6.md` 已更新（J3 缩短 + 暗物质 6 句）。在 `Assets/VoiceOver/Tools~`：
```
python elevenlabs_vo.py generate --go --only J3,J3_DMon_Before,J3_DMon_After,N_J3_DMon,J3_DMoff_Before,J3_DMoff_After,N_J3_DMoff
python elevenlabs_vo.py install
```
约 860 字符（账号剩约 1,185）。装好后再跑一次 `Tools > Journey NEW > Dark Matter > Build Dark Matter Moment (open scene)`，录音会填进空槽；然后再飞一遍、录一次，看真实长度。

**可调参数：**

| 在哪 | 参数 | 默认 | 作用 |
|---|---|---|---|
| `DarkMatterMoment_NEW` | Version | HiddenByDefault | A/B：ShownByDefault = 一开始有暗物质，Y 隐藏 |
| | Hint With Before Line | ✔ | 提示和那句一起出现；关掉 = 那句说完才出现 |
| | Nudge After Line | 1.5 | 那句说完几秒说「按 Y」 |
| | Auto After Line | 6 | 那句说完几秒自动切换 |
| `DM_Scaffold.mat` | Colour / Opacity / Emission | 蓝 / 0.55 / 1.4 | 暗物质的样子 |
| `Dark Matter Scaffold` | Reveal Seconds | 2.5 | 显示/隐藏用几秒 |
| Flight Path 窗口 | Threshold / Probe radius / Voice lane offset | 0.35 / 0.4 / 1.6 | 纤维检测和画法 |

**注意：** 录音存在 `Library/JourneyFlightRecording.json`，只在你这台电脑上。挪 web 不影响录音；挪门或改速度后，要把那一层再飞一遍。

---

## 做完了，等你在 Unity 里跑一遍确认

- [ ] **重新生成 3 句**：J5（第二个 cosmic web 的「70 亿年前」那句，上次没生成成功）和 E2 的两条 Y 键操作提示。共 283 字符。在 `Assets/VoiceOver/Tools~` 跑 `plan` → `generate --go` → `install`。
- [ ] **Ana 场景加上「光撞进地球」**：`Tools > Journey NEW > Solar System > Aim the Light at Earth`，保存场景。
- [ ] **Tutorial 场景跑一次 Build or Update**：光谱条上跟着吸收线走的标记从箭头换成了方框（`LineOutline`）。
- [ ] **Tutorial 里的氢原子换成了气体云**（LARC 10 月 14 日）。场景已经改好：D4–D8 的每个 `Atom_*` 下多了一个 `Cloud` 子物体（`NebulaCloudVFX_NEW`），原来的小球渲染器关掉了。**它和旅程里的星云是两种东西，所以颜色不同**：旅程第二段 cosmic web 里的是恒星形成的星云（红紫色，里面有星）；Tutorial 的是一团冷的中性氢，只在光所在的中心发亮，边缘是暗蓝灰色，没有星，光晕和点光源也是同一种冷蓝（`TutorialAtomHalo.mat` 换到了 Legacy Particles/Additive，才能上色）。**形状**：一条不规则的「气带」，几团大小不一的块串在一条轴上、边缘撕成丝，每团按种子转到不同角度横在来路上；原来撞击时罩在外面的大圆光晕（3.2 倍）缩到了 0.6 倍，变成云中心的一个亮点，不然外面那个圆会把它又变回球。试玩 D4：远处先是一个冷蓝光点，靠近时长成一条不规则的气带，撞上时光在云里面、光谱上出现那条线。嫌云太大/太小改 `Cloud` 的 scale（现在 2），嫌形状不对换 `Seed` 或转 `Cloud` 的角度。
- [ ] **Tutorial 的看/转判定放宽了**：B1「向右看」、B2「向上看」、C4「转身」现在只要按提示的方向把视角转过去就算（从拍子打开时的视角算起，转到目标方向的 8 成；目标在身后的话转 110° 即可，左右都行），不再要求把光点套进准星；B3「转身」改成从拍子打开时算起转 120°（原来是离航向 150° 且光点在画面里）。想要回原来的严格门控：B3 把 `Require Disc In Frame` 勾上，B1/B2/C4 把 `Turn Satisfies` 关掉。
- [ ] **Tutorial 调试键**：开着 Debug Overlay（`Tools > Journey NEW > Debug View`）时，数字键 `1` 跳到 C2（光在黑洞边上等待 Emit），`2` 跳到 D4（第一团氢气云逼近），`3`–`6` 是 A1 / B1 / C1 / D1。
- [ ] **第一个星系换成了碰撞环星系（10 月 23 日）**（`RingGalaxyVFX_NEW`，特殊星系，照 Hubble 的 Arp 148 做），替换了原来 Winnie 的 `Galaxy2` 粒子旋涡星系（`Galaxy2` 已经关掉，没删，打开就能对比）。`Gameplay_Scene_Ana` 和 `Test_Journey` 两个场景都改了。按 2 跳到 Micro 看。
  - **位置（第三版，Aaron 手动摆的）**：半径 23.8，中心在 Micro 门后 90.5、光的路线下方 3.5、右边 4，朝光倾斜 18°。整段 Micro 星系都在光的前方，光在出 Micro 的门那里从它中间上方飞过。`Test_Journey` 已经同步成一样的位置，`Replace First Galaxy` 菜单也改成摆到这里。
  - **出 Micro 的转场（10 月 23 日修）**：转场原来往星系自己的中心缩。星系挪到出口门正下方以后，这个中心离光点只有 4 个单位：Micro 世界缩向镜头正下方，镜头回头时亮结叠在光点上，星系团的光晕把整个画面糊成黄色，看起来就是错位。现在加了一个空物体 `GalaxyLayer/Micro Cluster Centre`（出口门后方 13.3、下方 12.5，就是这个转场当初调的位置），设成 `LayerDive_NEW` 里 CosmicWeb 那一项的 `Focus Override`，两个场景都改了。以后再挪星系，这个转场不跟着变；要改转场缩向哪里，就挪这个空物体。进 Micro 的转场没动：白光之后光点落在星系中心，和潜入的那个点在屏幕上只差 2° 左右。
  - **动效**：环和里面的东西会转（`Turn` 0.02）；每隔几秒从核心扩散出一圈涟漪，经过环时环和亮结会闪亮一下（`Ripple Seconds`、`Ripples`）；亮结会亮起、跟着环转一阵、再熄灭，换到别处亮（`Knot Life`）；星星会闪（`Twinkle`）；桥上的星星往环里流，潮汐尾的星星往外流。都在 Inspector 的 Motion 里调，`Ripples` 和 `Twinkle` 设 0 就关掉。
  - **白点**：Micro 一路上那几百个白点是离开 Micro（去 CosmicWeb）转场用的星系群。它们原来整段都暗暗地显示（`Ambient Crowd` 0.3），现在只在门前最后 5 个单位才出现（0），数量 2000 → 1000。`Gameplay_Scene_Aaron` 里的同一个设置没动。
  - 以后重摆用 `Tools > Journey NEW > Replace First Galaxy with Ring Galaxy`。如果把星系的倾角改了，记得把 `Intruder Tilt` 改成大约 -(倾角 + 17)，光条才会保持侧对着光。
- [ ] **星云改成做在第二段 cosmic web 的纤维里（10 月 23 日）**：原来那 7 团单独的星云（`Render Layers/CosmicWeb/Nebulae`）删掉了，改成把紫色纤维 `BIG` 的材质换成 `CosmicWebNebula_NEW.mat`（`Assets/_Core_NEW/Assets/CosmicWebNebula/`）。颜色保持原来纤维的紫色，不是粉色；有一团一团的气、暗的尘带，会慢慢飘。它很便宜：不做光线步进，每个像素三次贴图读取、没有循环；`BIG (1)` 是同一个 170 万顶点的网格又画了一遍，关掉了，紫色部分的顶点也少了一半。黄色的 Core / Coreyellow 没动。`Gameplay_Scene_Ana` 和 `Test_Journey` 都改了，第一段 web（Macro）没动。按 3 跳到 CosmicWeb 飞一遍看看颜色和亮度；觉得亮/暗调材质的 `Brightness`，觉得太碎调 `Noise Scale`（越小团越大），尘带多少调 `Dust`。要换回原来的烘焙体积：`Tools > Journey NEW > Restore Second Cosmic Web Gas`。
  - **中间那层也换了（粉色）**：星团周围小一点的片和丝是 `Coreyellow`（名字叫 yellow，烘焙出来其实是暗粉色；真正黄色的星团是 `Core`，`Coreyellow2` 几乎看不见），换成了 `CosmicWebNebulaPink_NEW.mat`：同一个 shader，粉/洋红色，团更细，往另一个方向飘。亮度 4，嫌太粉太亮就调这个材质的 `Brightness`。
  - **边缘软化（10 月 23 日）**：片和丝的边缘原来很硬（网格很薄，边缘在一条极窄的边上翻过去）。现在每个网格配了一张从网格烘焙的 3D 贴图（`WebSoftEdges_BIG_NEW` / `WebSoftEdges_Coreyellow_NEW`），气体朝片的边缘、丝的末端、零碎小块逐渐变淡、用噪声打散；`BIG.fbx` 和 `Coreyellow.fbx` 的法线改成导入时计算、平滑角 180°（第一次打开项目会重新导入这两个大模型，要等一会儿）。软化的强弱在材质的 `Soft Edge`：x 到 y 是从完全透明到完全显示的范围（调大 y 更软、气体更少），z 是边缘的不规则程度；设成 (-1, 0, 0) 就关掉。紫色那层调得轻（0.06, 0.42, 0.3），粉色那层重一些（0.15, 0.55, 0.35）。
  - **气团粒子版本，放在复制的场景里试（10 月 25 日）**：`Test_Journey_WebGas.unity`（`Test_Journey` 的副本，Ana 和 Test_Journey 没动）。参考宇宙网气体密度图：紫色纤维加软的光晕、密的节点偏桃色；中间那层粉色的片（`Coreyellow`）和黄色星团（`Core`）都改成了粒子，边缘是软的。暗物质 Y 在这个场景里照常切换，新的部分会跟着淡入淡出。打开这个场景按 3 飞一遍 CosmicWeb，再按 Y 看切换；满意的话我再把它搬到 Ana。亮度在三个 `CosmicWebGas*_NEW.mat` 的 `Brightness` 调，粒子大小在 `Size`，离镜头多近开始淡出在 `Camera Fade`。
  - **修正：暗物质切换是突然的、颜色发淡、星星发白（10 月 25 日）**：
    - 突然：进 Play 时 Unity 会对每个组件调一次 `OnValidate`，`CosmicWebGas_NEW` 因此把自己的渲染物体删掉重建，新的那个用的是材质原件，暗物质开关淡入淡出用的那份拷贝和 WorldSwitcher 的列表里都没有它，所以气体跟着 web 物体一下出现、一下消失。现在只有换了粒子文件或材质才重建。这个场景里交叉淡化也从 1.5 秒改成 2.5 秒（和原来暗物质显现的时长一样）。
    - 颜色淡、星星白：我之前的预览是纯黑背景，没画天空和 Galaxies。引擎里 CosmicWeb 这层的天空（`NP_CosmicWeb`）是亮紫色渐变加很多淡黄白的硬边星点，紫色气体叠在紫色天空上就显得很淡，星点是白的；Galaxies 星点经过相机的冷色白平衡也偏灰白；星团中心叠加过亮成了白色。预览现在把天空、Galaxies、相机 FOV 30 和后处理都画上了，和引擎一致。
    - 只改这个场景：它的层目录换成 `CosmicWebGas/LayerCatalog_WebGas_NEW`（和 `LayerCatalog_New` 一样，只是 CosmicWeb 指向 `LP_CosmicWeb_WebGas_NEW` → 天空 `NP_CosmicWeb_WebGas_NEW`：参考图那样的深石板紫，星点少、暗）；CosmicWeb 的 Galaxies 用 `GalaxyFieldWarm_NEW.mat`（桃粉色）；`CosmicWebGasClusters_NEW.mat` 改成桃粉、亮度 0.36。其它场景的天空和 Galaxies 都没动。
    - 天空太暗/太亮调 `NP_CosmicWeb_WebGas_NEW` 的三个颜色；星点多少调 `Star Threshold`（越大越少），亮度 `Star Brightness`。
- [ ] **开场：黑 0.1 秒后淡入**（出生点 z 100，开场这层全速约 14 秒）。在 Ana 场景跑一次 Wire Journey Voice Over 并保存：会加上 `JourneyOpening_NEW`，把 J2、J2b 从开场挪到星系，开场延迟从 3 秒改成 1 秒。试玩时检查：黑一下就淡入、光开始飞；J0 说完接着 J1；Console 里没有 `Dropped J1…`。
- [ ] **Ana 场景再跑一次 Wire Journey Voice Over 并保存**：会撤掉上一轮写进场景的开场减速。
- [ ] **Solar System 镜头**：`Tools > Journey NEW > Solar System > Camera Preset` 把视角放宽并把相机拉远；`Tilt the System` 把太阳系平面倾斜 15°，让轨道看起来是椭圆而不是一条线。两个可以分别用 Ctrl+Z 撤销，满意就保存场景。
- [ ] **试玩检查清单：**
  - 开场：J0 → J1 一开口镜头转身往后看，再转回前方。如果没转，Console 里会有一条 `PlayLookBack ignored: …`，把它发给我。
  - 有句台词没说出来的话，Console 里会有 `Dropped …: the light left …`，说明那一层太短，台词来不及说完。
  - 按 2 跳到 Macro：穿过纤维时冒出一条新线，光谱条上用**方框**框住，光尾上有 outline 闪烁加箭头，同时说 J3b。
  - 光谱条上：固定的出生点（121.6 nm）是绿色箭头，跟着线走的是方框，两者不再长得一样。
  - Galaxy：J4 → J4b，说完停 2 秒后加速离开；Milky Way 同理。
  - 进入 Milky Way：红移演示的标记消失。

## 已定

- 暗物质开关用 **Y** 键（B 是暂停）。**10 月 16 日改：不在 tutorial 的 Phase 4 教，改在旅程第一个 cosmic web 里边飞边教**（`DarkMatterMoment_NEW`，光不停）。Y 显示/隐藏的是暗物质的「画出来的样子」，气体不动。Tutorial 的 E1/E2 不做了，录音不用。PlayStation 手柄上是 ○。
