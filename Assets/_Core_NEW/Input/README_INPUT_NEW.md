# Input —— 一个控制方案，三个手柄

**先看这一段。** 展陈现场如果「控制不对」：

1. 按 **F4** 打开 `InputDiagnostics_NEW`
2. 看第一行 `PROFILE` —— 应该是 `Vizlab Logitech (pinned)`
3. 不对的话按 **F5** 换一个，直到对
4. 摇杆推一下，看 `RESOLVED BY PROFILE` 那几行动不动、方向对不对
5. 按印着 **A** 的键，`CONFIRM` 那行应该出现 `<- DOWN`

这五步不需要装 Unity，不需要重新出包。

---

## 为什么会有这一层

Unity 2019.4 的 legacy Input Manager **不认识手柄型号**，它只按硬件序号编号轴和按钮。两个手感一样的手柄，报上来的数字可以完全不同。

Carnegie Observatories 用的是一个 Logitech，它和 Xbox 的编号几乎处处不同：

| Unity 轴名 | 读的是 joystick axis | Xbox 上是 | **Logitech 上是** |
|---|---|---|---|
| `Horizontal` / `Vertical` | 1 & 2 | 左摇杆 | **D-Pad** |
| `RightStickX` | 4 | 右摇杆 X | **右摇杆 Y** |
| `RightStickY` | 5 | 右摇杆 Y | **左摇杆 X** |

所以在旧 build 里，那台机器上：左右看由**右摇杆的上下**驱动，上下看由**左**摇杆的左右驱动，D-Pad 会改航速，真正的左摇杆基本没反应。

**这是最坏的一种故障：不是死掉，是乱套。** 观众不会觉得设备坏了，会觉得自己不会用。

---

## 结构

```
Input/
├─ PadProfile_NEW          一个手柄 = 一份数据（轴名、按钮号、印在壳上的字母、死区）
├─ InputScheme_NEW         读输入的唯一入口。tutorial 和 journey 都走它
├─ InputProfilePinner_NEW  在场景里钉死用哪个 profile（出包时用）
└─ InputDiagnostics_NEW    F4 的实时读数页
```

`TutorialInput_NEW` 还在 `Tutorial/` 目录下，但已经**只是个转发器** —— 它保留原来的公开 API（包括序列化到场景里的 `LookStick` 枚举），实现全部转给 `InputScheme_NEW`。新代码直接调 `InputScheme_NEW`。

### profile 拥有什么，不拥有什么

| profile 拥有 | 谁拥有 |
|---|---|
| 轴名、按钮编号 | — |
| 印在塑料壳上的字母（HUD 画的就是这个） | — |
| 死区（它是硬件属性） | — |
| 灵敏度、回正速度、平滑 | **rig**，绝不能随手柄变 |

最后一行是有意的：如果灵敏度跟着手柄走，那么「今天插了哪个手柄」就会改变作品的手感。

---

## 按钮按「壳上印的字」映射，不按编号

Logitech 的面键相对 Xbox 转了一格：

| 壳上印的 | 实际发出的 button | Xbox 上那个编号是 | 我们给它的含义 |
|---|---|---|---|
| **A** | 1 | B | **confirm / recentre / emit** |
| **B** | 2 | X | **pause** |
| X | 0 | A | 空着 |
| Y | 3 | Y | 空着 |

设计是按 Xbox 写的（A = confirm，B = pause），而**观众读的是字母，不是编号**。所以 Vizlab profile 里 confirm 听 button 1、pause 听 button 2，HUD 照样写 A 和 B。

偏移刚好是 A 和 B 各 +1、Y 是 0 —— 所以这是一张**表**，不是一个加法。

---

## 出包时钉死，不要靠自动检测

`InputScheme_NEW` 能按手柄名字猜。桌面上这没问题：猜错两秒就看出来，再两秒就改掉。

**现场是反过来的**：手柄是已知的那一个，在场的人打不开 Unity，而猜错不长得像猜错 —— 长得像观众不会用。

所以：

- 场景根节点上挂 `InputProfilePinner_NEW`，`profileId = Vizlab`
- 留空才会走检测（桌面开发用）

Pinner 的 `DefaultExecutionOrder` 是 −10000，在任何东西读输入之前就跑完。

---

## InputManager.asset 加了什么

`Joy1` … `Joy10`，中性名字，一对一映射到 joystick axis 1–10，`dead: 0.19`、`invert: 0`（保持和已有轴一致的死区，符号交给 profile 决定）。

**加中性轴而不是加 `VizlabRightStickX` 这类名字**，是因为下一个手柄只需要改 profile 里的数据，不需要再动一次 project setting —— project setting 是唯一一处「改了要所有人重开工程」的地方。

已有的 `RightStickX/Y`、`PadRightStickX/Y` 一行未改，Xbox 和 PlayStation profile 继续用它们（它们的 `invert: 1` 已经在资产里，所以那两个 profile 的 `*YInvert` 是 false —— **反两次就是上下颠倒**）。

---

## 还没在真机上验证的一件事

`PadProfile_NEW.Vizlab` 的 `leftStickYInvert` / `rightStickYInvert` 是**猜的**（按和其它手柄一致的约定：推离身体 = 正）。`Joy*` 是原始轴，这台手柄的符号没人读过。

现场如果上下看反了：**改那一行的一个 bool**。F4 的诊断页直接显示符号，所以这是三十秒的事，不是一次重新出包。

---

## 顺带改掉的两件事

**1 · 航速不再归观众。** `PlayerRig_NEW.speedInput` 现在是三值枚举，默认 `Off`。

理由：tutorial 整个第一幕在教「你不能操纵光」，而且它是靠**删掉移动摇杆**来教的，不是靠一行字。journey 里观众能把光减速到停，那一课就是谎话；而且下一位观众会站在一张不动的画前面。

`Attendant` 把它放在 D-Pad 上（没有任何提示提到它，观众不会去按），并且**按 profile 映射** —— 直接读 `"Vertical"` 会在 Xbox 上把它放到左摇杆，在 Logitech 上放到 D-Pad，取决于那天插了哪个手柄。

`Visitor` 是旧行为，留给需要手动快进的 playtest。

**2 · `OrbitCameraRig_NEW` 的四个轴名字段删掉了。** 不是隐藏，是删掉 —— profile 现在回答这个问题。留一个「能改、不报错、不生效」的输入框是最贵的一种坑。

死区多了一个 `useProfileDeadband`（默认开）：死区是硬件属性，Vizlab 那个用旧了的摇杆静止时比 Xbox 离中心更远，而一个静止时就超过阈值的摇杆会自己漂一整天，顺带让 90 秒空闲返回永远不触发。
