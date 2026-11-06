# 待办

做完一项就划掉或删掉。最新的放最上面。

---

## 🔵 Journey 精修计划（11 月 1 日定）

**范围：只改 `Vizlab_Journey.unity`，`Gameplay_Scene_Ana` 不再同步。** 所有改动都要能在墙上（getReal3D）跑，也能在一台电脑上测。一步做完、在 Unity 里确认了，再做下一步。

| # | 步骤 | 做完的样子 | 状态 |
|---|---|---|---|
| 0 | **桌面测试环境** | 在编辑器里 Play `Vizlab_Journey`：Game 窗口自动切到墙的比例（`Vizlab Surface 1:5`，1920 × 1512，1.27:1）；键盘鼠标能操作。7 行弧面投影的预览（Wall 7）试过两次没成，11 月 1 日决定先不做，已删掉 | 🟡 比例已确认，但画面被竖着拉长了，11 月 3 日修了，等你再看一眼（见第 0 步） |
| 1 | **Quasar 往后挪** | 光的出生点和第一道门不动（第一层的长度、J0/J1 的时间不变），只把 quasar 往后挪（现在离出生点约 31，气体伸到约 52，光是从气体里出发的）。跟着调出 quasar 的 dive 焦点、J1 回头看到的构图。在 1.27:1 的 Game 窗口里定距离（墙的视野更宽，上墙前要再看一次） | ✅ 11 月 1 日你调好并提交了 |
| 2 | **从地球的夜面进** | `SolarSystemPath_NEW` 改成太阳在地球前方更远处：光先到地球，看到的是背光的地球（黑色圆盘、一圈亮的大气、夜面城市灯光）。地球转到终点（Pasadena）正好在夜面中央 | 🟡 代码好了，等你跑菜单、看效果（见下） |
| 3 | **大气层换掉** | 去掉 `EarthClouds_NEW`（全屏 raymarch，墙上 4 个 copy 太贵，夜里的白雾也不真实）。换成球面上的解析大气（一个 pass）+ 球面上的云贴图（夜里是挡住城市灯光的暗影）；往下降时天空保持黑色，星星和银河逐渐清楚 | 🟡 代码好了（11 月 3 日，云端写的，Unity 外只做了语法检查），等你和第 2 步一起看（见下）。星星和银河留到第 4 步 |
| 4 | **终点改成实时场景，去掉 Google Earth 录屏** | 夜光贴图从整个地球交叉淡化到洛杉矶盆地的高分辨率贴图 → 落到 Pasadena 上方山上的天文台：山脊剪影、银河、开着缝的圆顶 → 光进入望远镜，屏幕上出现一路收集的光谱条形码；J9、Closing 在这里说完。`EarthCutscene_NEW` 和视频关掉 | 🟡 分四段做：4a 夜面、4b 落到洛杉矶、4c 山顶天文台（11 月 5 日在 Unity 里看到天文台了）；4d 进望远镜收尾代码好了（11 月 6 日），等你看（见下） |
| 5 | **VO 对时间** | 拿到 Notion 上最新的 v7，按 ID 重新生成、导入。用 Flight Path 录一遍完整旅程，每句台词标出能不能在下一道门前说完；装不下的，优先让那一层停留更久，而不是改台词 | 🟡 录音已换成 v7（11 月 3 日），等你在 Unity 里飞一遍、对时间（见第 5 步） |
| 6 | **墙上的声音和同步** | 4 个 copy 只让 master 出声；台词推进按 clip 长度和 master 的时钟计算，不读各 copy 自己的 `AudioSource`。用 local cluster + probe 测一遍完整旅程 | ⬜ |

**实时终点场景的成本预算**（第 4 步）：不比现在的 cosmic web 贵。不开实时阴影，不用体积效果，不加后处理以外的全屏 pass。夜景大部分是黑的：山脊是低模剪影，圆顶几千个三角形，天空用现有的星空，城市灯光是贴图。在编辑器里看帧时间，超出预算就砍。

**终点（11 月 3 日定）：Mount Wilson 的 100 英寸 Hooker 望远镜。** 你说是"Carnegie 自己的望远镜"，具体哪台不确定，先按它做；LARC / Carnegie 那边说是别的再改（山和圆顶本来就是低模剪影）。当时的几个候选：
- Pasadena 市区的 Carnegie Observatories 是总部，没有在用的大望远镜
- 城北山上的 **Mount Wilson** 是 Carnegie 建的：Hubble 在那里的 100 英寸 Hooker 望远镜上测出了星系红移和宇宙膨胀，可以和红移那条线连起来，夜里俯瞰的就是洛杉矶的灯光
- Carnegie 现在做研究用的望远镜（Magellan）在智利的 Las Campanas


### 第 5 步：v7 录音已换上，在 Unity 里看

**改了什么（11 月 3 日，云端做的，Unity 外只做了文本检查）：**
- **覆盖 41 个旧录音**（Tutorial 25、Journey 16）：只换了内容，文件名和 `.meta` 没动，GUID 不变，所以 `VO_Tutorial` 表、`Vizlab_Journey` 的接线一行都没改。`Gameplay_Scene_Ana`、`Test_Journey*`、`Tutorial.unity` 引用的是同一批文件，也会听到新录音（这些场景本身没改）。
- **D6/D7 改了名**：`D7_Redshift` → `D6_Redshift`，`D6_FourMoreAtoms` → `D7_Clouds`（连 `.meta` 一起改，GUID 跟着走）。10 月 26 日的对调就此理顺：文件名、内容、表里的 D6/D7 三者一致。
- **新加 7 个**（`Assets/VoiceOver/Journey/`）：暗物质 6 句 + `J6_4`。
  - `Vizlab_Journey` 的 `DarkMatterMoment_NEW`：A/B 两个版本的 6 个 clip 槽填上了，字幕文字换成 v7。
  - `J6_4`：挂在红移演示 `F6` 的 onEnter 上（排队，J6_3 说完接着说）；`F6` 时长 10.3 → 15.5。`JourneyVoiceOverBuilder_NEW` 同步改了，以后再跑 Wire Journey Voice Over 也会加上。
- **Tutorial 表的文字**换成 v7（25 句），`recordedText` 填成同样的文字，状态改成 Final，所以 Report 不会报"需要重录"或"未核对"。
- `VO_Script_v7.md` 的 J3_DMoff_After、J3_DMon_Before 改成 Notion 版。
- 上传的 `vo_out_v7-…` 文件夹删了（内容都已经放到位；要找回在 git 历史里）。

**注意 `ledger.json`：** 这批不是 `elevenlabs_vo.py` 生成的，ledger 不知道。**不要**直接跑 `generate --go`，`plan` 会把整套列成要生成、白花额度。以后只用 `--only` 补单句。

**做：**
1. 回到 Unity，等它重新导入音频、编译完，Console 里没有红色 error。提示场景被改过就点 **Reload**（有没保存的改动先别点，告诉我）。
2. `Tools > Journey NEW > Voice Over > Timeline` → **Report**：Tutorial 应该没有缺录音、需要重录的行（E1/E2 那几句不在流程里，可以忽略）。

**看：**
- `Vizlab_tut`：按 `2` 跳到 D4，看完 D5–D9。D6（10.8 s）和 D7（11.2 s）比 beat 长（10 / 10.5 s），beat 会撑到说完；看红移加速、四团云是不是在台词说完之前就演完了、画面空等。
- `Vizlab_Journey`：用 Flight Path 录一遍完整旅程，Console 里搜 `Dropped` 和 `[DarkMatterMoment_NEW] Ended early`。
  - 第一个 cosmic web：J3 → 暗物质 Before → 按 Y → After，真实录音比原来的估时短约 4 s；J3b 还要落在 After 说完之后。
  - 第二个 cosmic web：J6_3 说完接 J6_4，都在出这一层之前说完。
  - 结尾：Closing 从 8.4 s 变成 **16 s**，看结尾视频够不够长（第 4 步换成实时场景时再定）。
- 装不下的，按第 5 步原来的原则：先让那一层停留更久，不改台词。

### 第 4 步 · 4d 光进望远镜、光谱全屏：在 Unity 里看

**定了：** 最后光谱全屏展示。

**怎么做的**（新组件 `EarthEnding_NEW`，在 `SolarSystem` 上，紧挨 `Earth Summit_NEW`）：
- **光进圆顶**：镜头在山顶站定 1 秒后（`Light After`），一道光从镜头后上方的天空（缝上方 1.2 km、往后 0.6 km，`From Km`）划下来，越来越快，3.5 秒（`Flight Seconds`）钻进圆顶的缝。光是一条白色拖尾（`Light Colour`、`Light Width Metres` 10 米）。钻进去的一刻，缝里的红灯闪亮一下（`Flash` 5 倍，1.5 秒内暗回去）。
- **旁白**：Closing 在光钻进去的时候开始说（`Closing` 槽填的是 `Closing.mp3`）。为此把 Closing 从 `JourneyVoiceOver` 的 Earth 那一行拿掉了，那一行现在只剩 J9（到达地球时说）。`Wire Journey Voice Over` 菜单也改了：场景里有 `EarthEnding_NEW` 时不再把 Closing 加回 Earth 那一行。
- **光谱全屏**：光进去 1 秒后（`Spectrum After`），画面变暗（`Backdrop` 0.9），HUD 上那条光谱条从它原来的位置放大到屏幕中央（`Spectrum Rect`：左右各留 6%，上下占中间 40%），2.5 秒（`Grow Seconds`）。用的就是 HUD 自己的那条（一路切进去的吸收线都在上面），不是复制：它被临时从 Row_2 拿到同一个画布的根上，行定位组件暂停；`EarthEnding_NEW` 关掉时会放回去。
- **视频关掉**：`EarthCutscene_NEW` 停用了（画布本来就是关着的，所以不会再出现）。
- 时间线（过地球门后）：0 s J9；14 s 落到洛杉矶上空；约 27.6 s 镜头站在山顶；约 32 s 光进圆顶、开始说 Closing（16 秒）；约 33–36 s 光谱放大到全屏；约 48 s Closing 说完，停在全屏光谱上。

**看：** 正常飞到地球（不要用调试键跳），等镜头站定以后：
- 光从画面上方划进来，钻进圆顶的缝，红光闪一下。
- Closing 开始说，不会和 J9 重叠，也不会说两遍。
- 画面变暗，光谱条放大到屏幕中间，吸收线清楚。
- 没有视频弹出来。

**不合适就调**（`SolarSystem` 上的 `Earth Ending_NEW`）：光从哪来、多快（`From Km`、`Flight Seconds`），光多粗多亮（`Light Width Metres`、`Light Colour`、`Flash`），光谱多大、在哪（`Spectrum Rect`），背景多暗（`Backdrop`），节奏（`Light After`、`Spectrum After`、`Grow Seconds`）。

**墙上要确认：** 光谱条是 HUD 的一部分，放大是在同一个画布里做的，所以墙上应该也是跨整面墙放大；但墙上 HUD 是按行贴的，没在墙上试过，上墙时看一眼。

### 第 4 步 · 4c 山顶天文台：在 Unity 里看

**定了：** 银河程序生成；艺术处理（头顶的星星和银河清楚，靠地平线被洛杉矶的橙色光晕盖住）；4c 只做圆顶外观和打开的缝，光进缝、望远镜、条形码是 4d。

**怎么做的：**
- **再往下降**：4b 的下降（12 倍）停稳以后，`EarthZoom_NEW` 接着把整个太阳系再放大约 100 倍（`Summit Seconds` 12 秒，慢—快—慢），直到镜头离地面 `Summit Camera Km` 1.8 km，比 Mount Wilson 的山顶（1.74 km）高 60 米。
- **地面**：地球的球只有 128 × 64 个平面，站在上面差好几公里。`EarthSummit_NEW` 在落点周围铺了一块细的真球面（±2.5° 经度 × ±2° 纬度，一直铺到地平线以外），用地球自己的材质画，所以夜光图、洛杉矶高清图、晴天区都和原来一样，换的时候看不出来；原来的粗球和大气藏起来。
- **洛杉矶的灯光**：从山顶斜着看，高清图的一个像素有 460 米，市中心本来就饱和，会糊成一大片白。所以离地面近时，亮的地方拆成一颗颗灯点（每 150 米一格，图越亮点越密），远处的地面渐渐没入城市的橙色雾霭。参数在 `Earth` 的 `Celestial Body_NEW` 里：`City Lights Km`、`City Light Size`、`City Light Brightness`、`Region Exposure`、`Ground Haze`。
- **山**：程序生成，不是测绘数据：镜头下是圆的山顶，朝洛杉矶方向很陡地落下去，中间三道一道比一道低的山脊，后面还有群山。近处的山最黑，远处的被雾气染得发灰，所以能分出层次。光照只有两样：身后左上方的月光，加上从山下往上照的洛杉矶暖光。约 2.5 万个顶点，不透明，一个 pass，没有阴影。
- **圆顶**：程序生成：圆柱底座加半球顶，缝从顶上越过去开到另一侧，里面是黑的，还有地板（从缝里看不到外面的地面）。按真实大小（直径 30 米）放大 1.6 倍，放在镜头左前方 300 米处，缝 3/4 侧对镜头。约 3 千个三角形。
- **天空**（`Resources/SummitSky_NEW`）：在镜头还朝下看的时候换掉天空盒。头顶是黑的，地平线上是洛杉矶的橙色光晕（朝城市那边最亮）；星星和银河都是程序生成的：银河是一条带团块、中间有暗尘带的光带，核心低低地挂在城市右边，往左上方升起，穿过最终的画面。离地平线越近被光污染吃掉越多。每个像素一个 pass，用的是现有的噪声贴图；没有闪烁，所以墙上 4 个 copy 画出来一样。
- **镜头抬起来**：下降到 55% 时，`EarthSummit_NEW` 在当前镜头的位置放一个自己的虚拟镜头（Priority 90，比 `CameraDirector_NEW` 的 50 高），7 秒内从朝下看平滑地转到朝洛杉矶、略高于地平线（`View Yaw` -8°、`View Pitch` +2°）。同时光的拖尾变细消失，接着光也藏起来（4d 再请它回来）；星星和银河随着镜头抬起慢慢出现。
- **视频**：`Start Delay` 改成 45 秒（过门后 14 秒下降 + 12 秒到山顶 + 停一会儿），4d 去掉。
- 在云端用 numpy 按同样的山形函数、镜头和天空公式模拟了最终画面（不是 Unity 截图）：前景是黑的山顶和圆顶（月光下灰色，看得到缝），中间是一大片洛杉矶的灯点，地平线一道橙色光晕，上面是星空。银河在模拟里是一团，因为我用的噪声和 Unity 里不同，要在 Unity 里看。

**做：**
1. 拉下最新的分支，回到 Unity 等编译完，Console 里没有红色 error。新文件：`Journey/EarthSummit_NEW.cs`、`Resources/SummitSet_NEW.shader`、`Resources/SummitSky_NEW.shader`。
2. 场景被改过就点 **Reload**。`SolarSystem` 上多了 `Earth Summit_NEW` 组件。
3. 想看日志：勾上 `SolarSystem` 上 `Earth Zoom_NEW` 和 `Earth Summit_NEW` 的 `Debug Log`，Console 会打印落点的经纬度（应该在 34.2, -118.1 附近）、离洛杉矶多远、镜头最后多高。

**看**（按 `5` 跳到 Solar System，飞到地球，过门后约 30 秒）：
- 降到洛杉矶以后继续往下，过程中看不出换了地面（没有跳一下、没有方块边）。
- 镜头慢慢抬起来：先看到斜下方的灯海，然后是山脊、圆顶、地平线的橙色光晕，最后是星空和银河。
- 洛杉矶是一颗颗灯点，不是一片白；远处没入雾里。
- 圆顶是灰色的，能看到缝和缝里的黑。
- 光的拖尾变细消失，不会有一根竖着的光柱戳在旁边。

**最可能要调的**（都在 `SolarSystem` 上的 `Earth Summit_NEW`）：
- 构图：`View Yaw` / `View Pitch`（镜头最后的朝向），`Dome Yaw` / `Dome Km` / `Dome Scale`（圆顶在哪、多大）。
- 亮度：`Moonlight`（圆顶和山多亮）、`City Glow`（山朝城市那面的暖光）、`Haze` / `Haze Km`（山的层次），`Sky Glow` / `Milky Way` / `Stars`（天空）。
- 节奏：`Lift From`（下降到多少开始抬镜头）、`Lift Seconds`、`Reveal After`；下降本身在 `Earth Zoom_NEW` 的 `Summit Seconds`。
- 截图发我（Win+Shift+S），我对照着改。

**11 月 3 日第一次试的结果和修正：** 镜头抬起来了，天空和地平线的光晕也对，但地平线以下全黑：洛杉矶、山、圆顶都没出来；银河像几团灰云。
- 地面：原因应该是浮点精度。整个太阳系绕落点一起再放大约 100 倍以后，太阳系根节点的世界坐标到了几千万，在这个量级上 float 的误差有几个单位，换算成真实距离约 1 km，地面被推到了镜头上方（从下面看，背面被剔除，什么都不画）。现在最后这段只放大地球本身：先把 `Earth` 从 `SolarSystem` 下面拿出来，坐标只有几万，精度足够；太阳系其余的部分留在原处并藏起来（山顶的夜空里不该有行星）。
- 银河：噪声太粗，核心太大。换成更细的团块和颗粒，光带和核心收窄。
- 再试一次，如果地平线以下还是黑的，请勾上 `SolarSystem` 上 `Earth Zoom_NEW` 和 `Earth Summit_NEW` 的 `Debug Log`，把 Console 里这两个组件打印的几行发给我；再在 Scene 视图里（Play 中）对准镜头附近截个图。

**11 月 4 日第二次试：** 地面出来了，但整体太黑，天文台几乎看不见。调亮：月光 0.16 → 0.45，圆顶更白（0.75），山稍亮（0.06）并加了整片天空的光（`Sky Light`）；圆顶和山的边缘加了一圈城市的暖光（`Dome Rim`、`Mountain Rim`），让它们从黑背景里跳出来；圆顶里面加了暗红色的灯（`Dome Inside`，真实的天文台夜里就用红灯），从缝里看得到一道红光；圆顶放大到真实的 2.2 倍。还嫌暗/太亮就调这几个，都在 `SolarSystem` 上的 `Earth Summit_NEW`。

**11 月 4 日第三次试：** 还是看不到地面和天文台，视角和我的模拟图不一样：说明镜头最后不在我算的位置（我推断是在高空，往平看时地面在画面下方以外）。现在镜头不再靠放大倍数算出来的位置，而是在抬头的同时落到山顶坐标系里一个固定的"机位"上：山顶上方 40–300 米、落点附近。不管地球最后实际放大了多少，镜头都会在山顶上。Console 里会多两行（不用勾 Debug Log）：开始建山顶时 `Building the summit at …`、抬完镜头时 `On the summit …`。**如果还看不到，请把这两行发我**，里面有镜头实际的高度和地球的大小。

**11 月 5 日：** 黄字显示 `km = 0.0283 units`（上一次是 3.67）：这次最后那段地球几乎没放大，镜头离地只有 0.05 个单位，圆顶离镜头 0.013 个单位，都在近裁剪面（0.1）以内，被裁掉了。原因：放大倍数原来是按那一刻镜头离地多高算的，镜头位置一变（比如用调试键跳过来），倍数就接近 1。现在改成固定比例：最后 1 km = 3.6 个世界单位（`Earth Zoom_NEW` 的 `Summit Units Per Km`），镜头机位直接用 `Summit Camera Km`。

**11 月 5 日，找到原因：** 黄字显示地面和山都画了，圆顶在画面下方很远（`dome on screen … -1.28`），而且比镜头低了约 360 米。山的函数里误用了 Unity 的 `Mathf.SmoothStep(a, b, t)`：它是在 a 和 b 之间按 t（0–1）插值，不是 shader 里"低于 a 为 0、高于 b 为 1"的 smoothstep。所以山的形状完全不对：圆顶落在坡下面，抬起镜头后掉出画面；乱掉的山挡住了后面的洛杉矶。numpy 模拟用的是正确的 smoothstep，所以模拟图没暴露这个问题。现在山的函数用自己的 `Edge(a, b, x)`。诊断的两行黄字往上挪了，不再被底部那行提示挡住。

**可能出的问题，告诉我：** 抬镜头时画面跳一下（Cinemachine 的混合）；地面某处闪烁（深度精度）；山或圆顶整个看不见（三角形朝向反了）；银河看起来像一团雾而不是一条带。

### 第 4 步 · 4a 夜面、4b 落到洛杉矶：在 Unity 里看

**参考：** 你发的 NASA Black Marble 图（美洲的夜面：月光下暗蓝的陆地、暖黄的城市灯光、灰色的云、右边一条蓝色的大气亮边）。

**改了什么（只动了 `Vizlab_Journey`）：**
- **夜光贴图**：你上传的两张 NASA Black Marble 2016（公有领域）。原图挪到了 `Assets/_Core_NEW/Assets/Image~/`（文件夹名以 `~` 结尾，Unity 不导入，免得去导入 21600 × 21600 的 A1），从它们生成了两张：
  - `Celestial/Earth_Night_BlackMarble_NEW.jpg`：全球，4096 × 2048。
  - `Celestial/Earth_Night_SoCal_NEW.jpg`：从 A1 裁出的南加州，1024 × 768，以 Mount Wilson 为中心，约 4.3° × 3.2°（西经 120.19–115.93°，北纬 32.62–35.82°），分辨率是全球那张的 24 倍。
- **4a 夜面**：`CelestialBody_NEW` 新加 **Night (Earth)** 一组。填了 `Night Map` 以后，夜面直接显示 Black Marble：月光下暗蓝的陆地、真实位置的城市灯光；原来噪声生成的城市灯光和夜面补光不再用。夜里的云是月光下的灰色（`Night Clouds`），城市灯光从云缝里透出来。
- **4b 落到洛杉矶**：下降时，镜头离地面越近，南加州那张高清图越清楚地盖在全球图上（`Region Fade`：从光到大气时的约 2.8 开始算，4 → 10 之间渐入；边缘羽化，看不出接缝）。落点周围 3° 内没有云、6° 外云照常（`Clear Sky Degrees`），所以最后是晴朗的洛杉矶夜空。
- **下降加深**：`EarthZoom_NEW` 的默认值改成 14 秒内放大 12 倍（原来 10 秒、6 倍）；有了高清图，12 倍还看得清。视频的 `Start Delay` 改成 16 秒，下降完再淡入。
- 在云端用 numpy 按 shader 的公式模拟了一遍（不是 Unity 截图）：远看是美洲的夜面、灰色的云、城市灯光；降到 12 倍时画面是洛杉矶盆地的灯光，比你的参考图亮一些的地方是 Black Marble 原图本身就饱和了。

**做：**
1. 拉下最新的分支，回到 Unity 等编译完、导入新贴图（`Earth_Night_*` 两张），Console 里没有红色 error。如果 Unity 已经给原来的 `Assets/_Core_NEW/Assets/Image/` 生成过 `.meta`，拉取时可能提示冲突：把本地那两个 `BlackMarble_2016_*.jpg.meta` 删掉再拉。
2. 场景被改过就点 **Reload**。

**看**（按 `5` 跳到 Solar System，飞到地球）：
- 地球夜面是暗蓝的陆地加真实的城市灯光（美国东部、墨西哥城那些亮团），不再是随机的光点。
- 云在夜里是暗灰色的，能看出来。
- 过了门以后往下降，美国西海岸 → 南加州 → 洛杉矶盆地越来越清楚，换到高清图时看不出跳变或方框的边。
- 洛杉矶上空没有云。
- 第 2 步问过的镜像问题：现在看大陆形状最清楚，北美如果是左右反的告诉我。

**不合适就调**（`Earth` 上的 `Celestial Body_NEW`）：夜面太暗/太亮调 `Night Exposure`；云太亮/太暗调 `Night Clouds`；高清图来得太早/太晚调 `Region Fade`；降得太深/太浅调 `SolarSystem` 上 `Earth Zoom_NEW` 的 `Descent Growth`。


### 第 3 步：大气层换掉，在 Unity 里看

**改了什么（只动了 `Vizlab_Journey`，Ana 和其它场景没动）：**
- **去掉 `EarthClouds_NEW`**：从 `LayerResponders` 上删掉了这个组件（脚本还在，别的场景照用）。进 Earth 层不再有全屏的云层、白雾和变蓝的天空。
- **大气**：地球本来就有 `CelestialBody_NEW` 画的解析大气（`Earth Air`，一个 pass、8 步），这次没改。夜面没有阳光，所以天空是黑的。
- **云换成真实云图**：`CelestialBody_NEW` 新加了 `Cloud Map` 槽，地球上填了 `Assets/_Core_NEW/Assets/Celestial/Earth_Clouds_2k_NEW.png`（2048 × 1024，和日间贴图对齐；取自 three.js 仓库的示例贴图 `examples/textures/planets/earth_clouds_2048.png`（r160），只留了透明度通道。原始出处和授权还没核实，公开展示前要确认，不行就换 NASA 的公有领域云图）。云和云影都从这张图读，`Cloud Cover` 0.55 就是原图，调低更少、调高更多。夜里云是暗的，盖在城市灯光上面，灯光只从云的空隙里透出来。没填贴图的星球还是原来的噪声云。
- **下降**：去掉云层以后，光过了地球门会一路飞进地面，再从球里穿出去。现在 `EarthZoom_NEW` 收到 Earth 层开始，就把光停在大气顶上（`SetHeld`），以光正下方的地面点为中心，整个太阳系继续放大：地面迎上来，弧度变平，夜面的云和灯光在镜头下铺开，看起来就是在往下掉。10 秒内放大 6 倍，先快后慢，然后停住。参数在 `SolarSystem` 上 `Earth Zoom_NEW` 的 **Descent**：`Descend`（关掉就是原来的样子，光会飞进地面）、`Descent Seconds`、`Descent Growth`。贴图只有 2k，放大超过 6 倍左右会糊，更近的洛杉矶贴图是第 4 步的事。
- **视频**：`EarthCutscene_NEW` 的 `Start Delay` 从 5 秒改成 10 秒，先看完下降再淡入视频。视频本身第 4 步才去掉。

**做：**
1. 回到 Unity，等编译完，Console 里没有红色 error。提示场景被改过就点 **Reload**（有没保存的改动先别点，告诉我）。
2. 第一次打开会导入新的云图。在 Project 里点 `Earth_Clouds_2k_NEW`，确认 Inspector 里 **sRGB 没勾**、Wrap Mode 是 **Repeat / Clamp**（U 重复、V 夹住）。
3. 如果第 2 步的菜单还没跑，先按第 2 步做完。

**看**（按 `5` 跳到 Solar System，飞到地球）：
- 地球远看就有真实的云：赤道附近零碎、中纬度一条条旋涡；白天那侧是白的，夜面是暗的。
- 过了门之后光停在空中、地面一直往上来，**光不会钻进地球**，画面也不会突然变成白雾或蓝天。
- 夜面的城市灯光被云挡住的地方是暗的。（灯光现在还是随机生成的，第 4 步才换真实夜光图。）
- 10 秒后视频照旧淡入。

**不合适就调：** 云太多或太少调 Earth 上 `Celestial Body_NEW` 的 `Cloud Cover`；下降太快或太慢调 `Descent Seconds`；降得不够低调 `Descent Growth`（太大贴图会糊）。帧率掉了、看到一圈圈的纹或者云在经度 180° 的地方有一条线，截图发我（Win+Shift+S）。

**关于「星星和银河逐渐清楚」：** 下降时镜头是沿着光的方向往下看地面的，地球占满画面，看不到天空，所以这一条这次没做。第 4 步落到山上、镜头抬起来看天时再做。Earth 层的天空（`NP_Earth`）是紫色的星云，到时候也要换成黑色的星空。

### 第 2 步：从夜面进，在 Unity 里这样做

**改了什么：**
- `SolarSystemPath_NEW`：太阳从"地球前面（光先经过太阳）"改到"地球后面"。新开关 `Sun Beyond Earth`，关掉就是原来的样子。
- 同一个组件：摆放时把地球转到终点朝向光的来路，北朝上。终点填的是 Mount Wilson（34.22, -118.06，在 Pasadena 北边的山上）；在行星尺度上和 Pasadena 看不出差别，第 4 步定了望远镜再改。
- `EarthZoom_NEW`：放大时太阳不再跟着一起放大（这个太阳系里太阳半径有地球轨道的一半，否则会占掉三分之一的天空）。它在屏幕上收到半径约 3°（`Sun Seen Degrees`），地球变大时落到地球边缘后面。`Camera.main` 也换成了 `GetReal3DView_NEW.ScreenCamera`，墙上每个 copy 算出的结果才一致。
- `Vizlab_Journey`：`Sun Aside` 0.8 → 0.6；光经过的行星从火星换成木星（太阳挪走后火星离地球只剩约 1.5，放大时会大得糊住屏幕）；地球的 `Fill` 调暗，夜面才是黑的。

**做：**
1. 回到 Unity，等编译完。提示场景被改过就点 **Reload**（有没保存的改动先别点，告诉我）。
2. 选中 `SolarSystem`，在 Inspector 里 `Solar System Path_NEW` 组件右上角的 ⋮ 菜单 → **Lay Out Along the Photon's Path**。Console 会列出每颗行星的位置，留意有没有 "closer than" 的提示。
3. **Ctrl+S 保存场景。**

**看**（按 `5` 跳到 Solar System，飞到地球）：
- 太阳在前方偏一侧，木星、土星从旁边经过（它们现在也是背光的，比以前暗，看到的主要是月牙）
- 地球是黑的圆盘，太阳那一侧有一条亮月牙，大气的亮边绕过去一大半
- 地球变大时太阳滑到地球边缘后面，最后整个屏幕是夜面
- 夜面的灯光：**现在的灯光是噪声随机生成的，不是真实城市的位置**，第 4 步才换真实夜光图

**不合适就调：** 亮的部分太多或太少调 `Sun Aside`（改完要重跑第 2 步的菜单）；太阳大小调 `EarthZoom_NEW` 的 `Sun Seen Degrees`；夜面太黑或太亮调 `Earth` 上的 `Fill` 和 `City Lights`。

**请顺便确认一件事：** 按 `PlanetSphere_NEW` 的网格算，地图可能是左右镜像的（经度增加的方向在屏幕上朝左）。飞近时看一眼大陆的形状，如果北美是反的告诉我。终点的位置已经按同一张网格算，不受影响，但第 4 步接真实区域图之前要修。

### 第 0 步：在 Unity 里确认

**11 月 3 日修：画面竖着拉长了。** 原因：在编辑器里，getReal3D 用的是 `editor_config`，它只有一块 530 × 300 的屏幕（16:9），投影按这个算；第 0 步把 Game 窗口改成墙的 1.27:1 以后，16:9 的画面被横着挤进去，所有东西都高了 1.39 倍（太阳变成竖的椭圆）。现在 `GetReal3DLens_NEW` 在桌面上（不是墙上的多个 copy 时）把投影按窗口的实际比例重新算：竖直视角保持 getReal3D 给的，水平跟着窗口走。所以 Game 窗口是什么比例都不会再变形；墙上不改。**看：** Play `Vizlab_Journey`，太阳、行星是圆的；把 Game 窗口换成 Free Aspect 再看一次，也应该是圆的。


1. 回到 Unity，等编译完，Console 里没有红色 error。（之前加的 **Tools > Vizlab > Desk Preview** 菜单和 `vizlab_desk.gr3d` 已经删了，getReal3D 用回原来的 `editor_config`。）
2. 打开 `Vizlab_Journey`，Play。Game 窗口应该在 Play 开始前自动切到 `Vizlab Surface 1:5`；Wall Test 面板上 game view 那一行显示 1.270:1。
3. 鼠标能看，W/S 缩放，Space、P、Y、Left Shift 有反应。

---

## 🟡 LARC 10 月 26 日反馈：红移提前加速、光谱带标注、Earth 相机翻转

**代码已改好，Unity 外编译通过。** 下面在 Unity 里做：

### Tutorial（`Test_Tutorial` / `Tutorial.unity`）

1. `Tools > Journey NEW > Tutorial > Build or Update`，保存场景。Console 里会出现几条 repair log（D7 时长 9.6 → 10.5、D6/D7 描述、波段标签 12 → 14 号字），都是正常的。
2. **新顺序**：D5 第一团云 → **D6 讲红移**（线开始滑，并在约 1.5 秒内加速）→ **D7 四团云**（每团切出一条分开的线，形成条形码）→ D8 再来一团。这和 VO v7 的剧本顺序一致。
3. **红移加速、不再放大 UV**：`TutorialBakedSpectrum_NEW` 上的 **Redshift Speed Up**（默认 10×）和 **Boost Ramp Seconds**（1.5）。光谱条从头到尾都是完整的 1216–9000 Å，D4 不再放大（**Magnify** 默认关，勾上就恢复原来的放大）。10× 时云之间的线在条上相隔约 10 px，到 D9 第一条线大约走到 1850 Å（仍在 UV）。条形码太挤就调大，线太细、看不清就告诉我。左上角调试行会显示 `boost ×10`。
   - **代价**：tutorial 多走了约 1 Gyr，journey 开头的光谱条会停在交接点上，等光追上（大约十几秒）。
   - trail 上放线的那一段延长到了 2200 Å（**Line Region End Full Bar A**），不然线会挤成一团。
4. **光谱带标注**：D3 光谱条出现时，ULTRAVIOLET → VISIBLE → INFRARED 依次亮起、标签放大，**trail 上同一条带同时变亮**（时钟由光谱条的 `Bands` 统一控制，trail 的亮度在 `TutorialTrailBands_NEW` 的 **Highlight Peak** 里调）。带太窄时标签自动换成 UV / VIS / IR。可调参数在 `Bands`（`TutorialSpectrumBands_NEW`）的 **Band highlight** 和 **Band names** 里。
5. **Trail 回头看不再左右颠倒**：不管从哪个方向看，UV 都在屏幕左边、IR 在右边。转身时，trail 在相机转到正侧面（带子在屏幕上竖着）的那一刻镜像过来，所以不会明显地跳一下。参数在 `TutorialTrailBands_NEW` 的 **Orient Every Seconds**（0.05）和 **Orient Switch Pixels**（8）。转身时如果在正侧面附近来回闪，就把 Switch Pixels 调大。
6. **VO**：`VO_Tutorial_Phase3` 里 D6 和 D7 的录音已对调（D6 = 红移那句，D7 = 「每个氢原子…」那句），所以现有录音和新顺序对得上。之后按 v7 重新录音、导入时，会按 ID 写回去。
6. 试玩：按 `2` 跳到 D4，看完 D5–D8。

### Journey（`Test_Journey_WebGas`）

7. **Earth 相机翻转**：之前到 Earth 门时，CameraDirector 仍然走普通换层流程，3.5 秒内从贴在光后面的低机位甩到俯视的 `VCam_TopDown`（俯角 76°），停 3 秒后再甩回来。Earth 层没有自己的世界，而且用的是同一个相机，所以这一甩什么都没遮住，只是翻了一下。现在 `CameraDirector_NEW` 的 **Stay On Camera Layers** 默认填了 `Earth`：进 Earth 层时不换相机、不甩，其余流程照旧（锁视角、事件时间都不变）。
8. 试玩：进 Play，飞到 Earth（或用调试键跳到 Solar System），看到达时相机不再翻。如果还是翻，说明不是这个原因，把录屏发我，我再查 `EarthZoom_NEW` 的相机部分。

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

剧本 `VO_Script_v7.md` 已更新（J3 缩短 + 暗物质 6 句）。在 `Assets/VoiceOver/Tools~`：
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
  - **再修正：引擎里还是发白、像一层奶白色的雾（10 月 25 日，照片 IMG_1311）**：根本原因是混合方式。紫色光斑只做相加，叠得越多越亮，蓝通道先溢出、接着红、最后绿，于是紫 → 洋红 → 白，Bloom 再把白色扩散成雾。中间试过完全"叠盖"（不透明地一层层盖上去），颜色不会白了，但太像糖果、没有半透明和发光的感觉，已经改掉。现在是两者之间：每个粒子按密度从色带取色（暗紫 → 紫 → 淡紫粉 → 桃色），加上自己的光，同时只遮住后面一小部分。稀的地方是半透明的辉光，密的地方颜色稳定下来、稍微泛 Bloom，不会叠到白。天空是深灰紫；细丝材质排到气体之后画。
    - 调法（三个 `CosmicWebGas*_NEW.mat`）：`Cover` 是每个粒子给多少光（大了气体更亮更厚）；`Occlude` 是遮住后面多少（0 只发光、越叠越亮直到发白，1 不透明像糖果，现在 0.45）；`Brightness` 整体亮度，密处的颜色大约是色带颜色 × `Brightness / Occlude`；`Color A–D` 是色带四个颜色（稀 → 密），`Ramp` 的 x/y/z 是到达紫、淡紫粉、桃色的密度位置；`Size` 粒子大小。
    - 手机拍屏幕会自动白平衡，紫色会被拍成灰白，下次请直接截图（Win+Shift+S）再发给我，颜色才准。
  - **按后处理反算颜色（10 月 25 日）**：相机的后处理（`Main Camera Profile` 的 ColorGrading，色温 -24.4）会把所有颜色往蓝推：比如材质里的紫色 (0.42, 0.24, 0.7) 实际显示成 (99, 63, 208)，桃色显示成米灰。现在这个场景里 web 的颜色都是从参考图量出来的目标色，再按这个白平衡反算回材质里填的值（所以材质里看起来偏暖、偏粉，经过后处理才是对的）：气体色带（目标：暗紫 (88,64,150)、紫 (125,72,185)、粉紫 (205,105,170)、桃 (250,150,145)）、星团色带、天空（目标灰紫 (52~66)）、BIG 细丝的 `Rim/Mid/Lit`、Galaxies 星点（目标桃粉 (245,150,140)）。后处理本身没改，其它场景不受影响。
    - 以后要改颜色：想要显示成某个颜色 X，材质里要填比 X 更暖的值（红多一点、蓝少一点）。改了后处理的色温，这些颜色要重新算。
  - **纤维内部像气体、和空白区域的边界清楚（10 月 30 日）**：以前气体粒子会溢出纤维的轮廓，靠近镜头时粒子很大，纤维像一根根模糊的软管子。现在纤维网格（`BIG`，材质 `CosmicWebNebulaFine_NEW`）先于气体画（queue 2949），在模板缓冲（stencil）的第 64 位标出它覆盖的像素；纤维气体（`CosmicWebGasThreads_NEW`）只在这些像素里画（`Stencil Comp` = Equal）。结果：纤维内部还是柔和、不均匀的气体，边界就是网格轮廓那样清楚，空白区域干净。`Test_Journey_WebGas` 和 `Vizlab_Journey` 一起生效。
    - 细丝材质同时把 `Absorb` 和 `Dust` 调成 0，避免先画的网格把背景压出暗色块。
    - 想恢复成软边：把 `CosmicWebGasThreads_NEW` 的 `Stencil Comp` 改回 Always。
  - **不要那么"粒子化"、网络边界再清楚些（10 月 30 日）**：纤维和中层的粒子放大一倍（`Size` 0.0018）、噪声打散减弱（`Breakup` 0.3）：粒子在裁好的轮廓里互相融成连续的气体，边界不会因此变糊。中层气体（星团周围的片）也只画在纤维的轮廓里（`Stencil Comp` = Equal），不再在星团周围散成一颗颗光斑。星团气体不裁边（它的网格是薄片，裁出来是刀片形），但零散碎块上的小亮点去掉了（新参数 `Thinnest` 0.4：比这个更稀的粒子不画），星团读起来是一团光而不是一把点。
  - **星团中心不够亮、不够发光（10 月 30 日）**：星团气体亮度 0.495 → 0.8；新加 `Web Gas (core glow)`（每个宇宙网物体下一个，`CosmicWebGasCoreGlow_NEW.mat`）：在 6 个最密的核心各放一层光晕 + 中等光 + 亮芯，相加混合、HDR，经 Bloom 泛光。太亮/发白调这个材质的 `Brightness`（现在 1.5，试过 3 就过曝了）；光晕大小调 `Size`；离镜头多近开始淡出调 `Camera Fade`。核心位置是从星团粒子算出来的：`python Tools~/bake_core_glow.py WebGas_Core_NEW.bytes WebGas_CoreGlow_NEW.bytes`。
  - **纤维靠近核心更亮更粉（10 月 30 日，参考图）**：纤维和中层的气体离星团核心越近，颜色在色带上越往粉/桃色走、也越亮，往外逐渐回到紫色。核心位置就是发光核那 6 个（材质里的 `Knot0–5`：xyz 是位置，w 是影响范围，由 `bake_core_glow.py` 一起算出并打印）。调法（`CosmicWebGasThreads_NEW`、`CosmicWebGasMiddle_NEW`）：`Near Pink` 是靠近核心时往粉色走多少（现在 0.8），`Near Bright` 是亮多少（现在 1，即最近处亮一倍）；粉色铺得太开/太窄，改 `Knot` 的 w（重新烘焙时用 `--knot-reach`，现在是核心大小的 4 倍）。
    - 要检查：Vizlab 大屏的渲染目标要有模板缓冲（24 位深度缓冲一般都带）。如果纤维气体在大屏上整个不见了，就是没有模板缓冲，先把 `Stencil Comp` 改回 Always，再告诉我。
  - **改成体积气体（Pyro 那样）+ 发光粒子，两个东西（10 月 31 日，新参考图）**：粒子版看起来扁平、奇怪，不像气云。现在纤维和中层是真正的体积气体（`CosmicWebPyro_NEW`，每个 web 物体下的 `Web Gas (pyro threads)` / `(pyro middle)`）：一团团翻滚、内部有明暗、边缘自然变稀、空白区域是暗的；颜色只走紫 → 洋红 → 粉，不再往黄走。橙黄的亮点是另外一层发光粒子（`Web Gas (stars)`）：沿纤维稀稀落落、节点处挤成一团，亮的会泛 Bloom。旧的粒子气体（threads / middle / clusters）和 `BIG` 的细丝表面关掉了但还在场景里；core glow 保留。详细的做法和调法写在 README 的「试验场景」那节。`Test_Journey_WebGas` 和 `Vizlab_Journey` 一起改了，Ana 和 Test_Journey 没动。
    - 便宜：每个像素 8 步、每步两次 3D 贴图读取；外壳 15 万 + 7 万个三角形，比原来画 `BIG` 细丝表面的 330 万还少。没有额外的 Pass 和 RenderTexture。镜头在气体里面时多几步。
    - 要检查：飞一遍 CosmicWeb（按 3），特别是穿过中间星团那段（镜头在气体里面）：有没有一块块的碎片、一圈圈的环纹或者突然出现的黑洞；按 Y 看暗物质切换时气体和亮点一起淡入淡出。帧率掉了告诉我（可以把 `Steps` 从 8 降到 6）。截图用 Win+Shift+S。
    - 太亮/太暗调 `CosmicWebPyro*_NEW.mat` 的 `Glow`；太碎调小 `Erode`；亮点太多/太亮调 `CosmicWebGasStars_NEW.mat` 的 `Brightness`，或者重新生成（`bake_web_stars.py --counts`）。
  - **再改：要体积感，但网状结构不能破坏（10 月 31 日）**：上一版是一朵朵翻滚的云，纤维被吹得很粗，网络没了。原因：纤维平均只有约 1 个世界单位粗，模糊的 3D 场分辨不了。现在纤维本身用原来的网格画（`BIG` + `CosmicWebNebulaFine_NEW`、`Coreyellow` + 新的 `CosmicWebNebulaFineMiddle_NEW`，重新打开、调亮、换成气体的紫/洋红/粉），体积气体只做每根纤维外面一圈柔和的光晕（场换成「离网格表面多近」的距离场，外壳也跟着换，贴着纤维）。结果：远看是一张完整的细纤维网，裹在柔和的气体里，有纵深；近看一根根纤维，外面有光晕。
    - 成本：纤维网格和原来画 `BIG` 细丝时一样；光晕外壳 39 万 + 35 万个三角形，每像素 8 步、每步两次 3D 贴图读取。
    - 要检查：同上（按 3 飞一遍，特别是穿过中间星团；按 Y；帧率）。纤维太暗/太亮调 `CosmicWebNebulaFine_NEW` 的 `Brightness`；光晕太宽/太窄调 `CosmicWebPyro*_NEW` 的 `Shape`（x 越小越宽）。
  - **星团那里不要翻滚的云，体积感只在穿过纤维时（10 月 31 日）**：`Web Gas (pyro middle)` 关掉了（星团周围那团粉色的云，也是它在进入 Milky Way 的转场里铺满画面）。纤维的光晕平时离镜头远一点就淡掉，只有镜头穿过一根纤维时，周围才出现一层能看透的薄纱（星团核心附近不出），噪声漂移也放慢了。纤维本身（`CosmicWebNebulaFine_NEW` / `FineMiddle`）一点没动。
    - 要检查：飞过 z 636、661、848 附近时（镜头在纤维里）有一层淡紫的体积；过星团和进 Milky Way 的转场时没有云。纱太浓/太淡调 `CosmicWebPyroThreads_NEW` 的 `Inside Density`（现在 0.5）；在星团附近也想要就把 `Cluster Clear` 调大（比如 0.9, 1）。
- [ ] **所有场景天空盒里固定的星星关掉了（10 月 31 日）**：天空的 shader（`Assets/Shaders/Nebula.shader`，`Custom/Nebula`）加了一个开关 `Fixed Stars`，默认关，所以用它的 4 个天空材质（`Custom_Nebula`、`II`、`II 1`、`III`，约 30 个场景）全部没有星星了；其它场景用的是 Unity 自带的天空，本来就没有星星。层的天空配置（`NP_*.asset`）里的星星参数还在，只是不画。想让某个天空的星星回来：在那个材质上勾上 `Fixed Stars`。
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
