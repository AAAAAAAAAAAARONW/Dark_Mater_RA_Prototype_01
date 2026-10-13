# 待办

做完一项就划掉或删掉。最新的放最上面。

---

## 🟡 摆放黄色纤维触发框（J3b）

**状态：** 占位框已由工具生成，位置还没调。不调也不会报错，但 J3b 可能在看不见纤维的地方触发，或者光根本没穿过它，J3b 就不说了。

**它是干什么的：** 光穿过这个框时，会在光谱条和光尾上加一条吸收线，闪烁并用箭头指着它，第一次穿过时说 J3b（*"These yellow threads are hydrogen gas…"*）。所以框要放在**一条看得见的黄色纤维上**，让玩家看到「穿过黄色的东西 → 冒出一条线」。

**前提：** 已经在 `Gameplay_Scene_Ana` 跑过 `Tools > Journey NEW > Voice Over > Wire Journey Voice Over (open scene)`，Hierarchy 里有 `Filament_BeforeGalaxy`。

**步骤：**

1. 打开 `Gameplay_Scene_Ana`，在 Hierarchy 搜索框里输入 `Filament_BeforeGalaxy`，选中它。
2. 打开 gizmo：`Tools > Journey NEW > Debug View` 里把 Gizmos 打开，Scene 视图右上角的 **Gizmos** 按钮也要开。这个框会显示成**黄色线框**。
3. **找一条合适的黄色纤维：**
   - 在 Macro 层（第一个 cosmic web），进入 galaxy 的门 `Layer_MacrotoMicro`（z ≈ 265）**之前**；
   - 光的路径会穿过它；
   - 最好在 J3 说完之后才碰到。Macro 层速度约 5 单位/秒，J3 约 9 秒，所以离 Macro 入口门 `Layer_QuasartoMacro`（z ≈ 135）**至少 50 个单位**，大约 z 在 190～250 之间。
   - 省事的找法：进 Play，按 **2** 跳到 Macro，看光飞过哪条黄色纤维时画面最好看，记下那时 debug 左上角显示的 `player Z`。
4. 退出 Play，选中 `Filament_BeforeGalaxy`，把 **Position 的 z** 改成刚才记下的值。
5. 调 **Box Collider 的 Size**：x、y 要盖住光的路径（看黄色线框有没有罩住飞行路线），z 保持 3 左右就行（相当于纤维的厚度）。
6. **Ctrl+S 保存场景。**
7. 再进 Play、按 2 验证：光穿过那条纤维时，光谱条上冒出一条新线，闪三下，有箭头跟着它往红端滑，同时说 J3b。

**可调参数**（选中物体后，在 `FilamentAbsorber_NEW` 组件上）：

| 参数 | 默认 | 作用 |
|---|---|---|
| Depth | 0.9 | 线有多黑，0～1 |
| Width Km S | 700 | 线多宽。约 600 = 光谱条一格，太细可能看不见 |
| Highlight Line | ✔ | 闪烁并加箭头 |
| First Time Line | J3b | 第一次穿过时说的话。以后加别的纤维，这一栏留空 |

**以后想加更多纤维：** 复制这个物体挪到别处，把 First Time Line 清空。J3b 每次游玩只说一次，不管先穿过哪个。

**为什么要手动放：** 烘焙数据里吸收线森林在 z 2.46 就冻结了，正好在第一个 cosmic web 之前，所以那里不会自然出现新线。这条线是有意加的「教学表达」，详见 `BakedSpectrumSource_NEW.AddAbsorber` 的注释。

---

## 做完了，等你在 Unity 里跑一遍确认

- [ ] **重新生成 3 句**：J5（第二个 cosmic web 的「70 亿年前」那句，上次没生成成功）和 E2 的两条 Y 键操作提示。共 283 字符。在 `Assets/VoiceOver/Tools~` 跑 `plan` → `generate --go` → `install`。
- [ ] **Ana 场景加上「光撞进地球」**：`Tools > Journey NEW > Solar System > Aim the Light at Earth`，保存场景。
- [ ] **Tutorial 场景跑一次 Build or Update**：光谱条上跟着吸收线走的标记从箭头换成了方框（`LineOutline`）。
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

- E2 暗物质开关用 **Y** 键（剧本已改）。Phase 4 还没做，做的时候 E2 的门控、提示行和按键图都要用 Y。
