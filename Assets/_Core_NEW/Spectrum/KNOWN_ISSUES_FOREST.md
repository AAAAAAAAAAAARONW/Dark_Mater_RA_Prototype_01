# Lyman-α forest —— 已知的科学性缺口

**状态：已知，暂不修。先按 fake 版本做 CosmicWeb 层的 redshift 演示，科学性作为下一步。**

写这份记录的原因：下面几条单看都像是「参数没调好」，实际上都是**数据来源不对**。
调参数永远修不好它们，而且每调一次都会让它看起来更像对的，从而更晚被发现。

---

## 1 · forest 的线和世界里的云没有关系

`AbsorptionField_NEW` 按 `SpectrumProfile_NEW` 的 `randomSeed` / `spawnRatePerSecond` /
`dipWidthMin..Max` / `dipDepthMin..Max` **随机**撒线，按时间推进。

世界里真实存在的气体云是另一套东西 —— `AbsorptionVolume`、`PurpleCloudMultiVolume`，
玩家真的会穿过它们。**两者之间没有任何连接。**

后果，按严重程度：

- **VO 会说谎。** 脚本 Phase 8 写的是「每一朵你穿过的氢云都留下一条新线」「线的间距
  记录了云之间的距离」。观众能数云，也能数线，两个数字对不上。
- **「第一条线」不存在。** 新演示要高亮「你在 tutorial 里割出的那条线现在在哪」，
  而现在没有任何一条线是可以被指名的 —— 它们生成完就只是缓冲区里的一段数值。
- **间距没有含义。** 现在的间距来自 `spawnRatePerSecond` 和随机数，不是距离。

**打算怎么修（下一步）**：一小批**被命名、被记录位置**的线（对应真实穿过的云），
叠在程序化的背景森林之上。背景没人数，前景要对得上。演示只给这批命名的线配锚。

---

## 2 · redshift 的量不是从旅程距离来的

`driftPerSecond` 是「参考速度下每秒的归一化位移」，由 `SpeedResponder_NEW` 的速度倍率
缩放，逐帧积分。它**不读** `UniverseJourneyTracker` 的距离或相位。

后果：

- 屏幕上如果同时出现 **13 Gyr 时钟**和 **×stretch 读数**，两个数字各算各的，会对不上。
  展陈墙上两个读数并排，迟早被发现。
- 任何一次 layer 速度调整，都会**悄悄改变**到达地球时的总红移量。
- 调试跳层（`LayerDebugJump_NEW`）会跳过积分，红移量直接失准。

**打算怎么修（下一步）**：红移量是距离/相位的函数，由 tracker 提供，drift 只是它的
显示速率。fake 阶段按「全程预算」定 drift（见下），这样替换成真值时观感不变。

---

## 3 · 现在的 drift 快了大约一个数量级，而且会绕回来

`SP_Macro` / `SP_CosmicWeb`：`driftPerSecond: 0.00778`。光谱归一化到 0–1，
所以 **128 秒滚完一整条**。`SpectrumHUD_NEW.wrapSpectrum` 默认开，所以线会 wrap。

一趟 8–12 分钟的 journey 就是 4–6 圈。**这让「锚 + 间距」这个设计不可能成立** ——
锚和线之间的距离会周期性归零。

**fake 版本的做法**：

- `wrapSpectrum` 关掉
- drift 按全程预算倒推，而不是调手感：
  全程 600 s、第一条线走 0.45 条 bar → `driftPerSecond ≈ 0.00075`（慢约 10 倍）

预算是「全程走多远」，所以第 2 条接上真值之后，画面观感不变。

---

## 4 · 线的深度只在它割到的地方看得见

`SpectrumHUD_NEW` 的吸收是**减高度**画出来的，所以一条线只能和它割进去的曲线一样明显。
tutorial 把线割在 Ly-α 峰的亮侧，并且靠 `driftContinuum = false` 把曲线钉住，
否则几秒之内那个波长就跑到森林区的平坦连续谱上，等于没有线。

journey 是 `driftContinuum = true`（曲线和线一起漂），所以这条暂时不咬人。
但**如果以后为了演示去关 journey 的 `driftContinuum`，这条会立刻咬人**，而且表现为
「线莫名其妙变淡了」，很难联想到是这里。

---

## 相关文件

- `Assets/_Core_NEW/Spectrum/AbsorptionField_NEW.cs` —— 线的生成与 drift
- `Assets/_Core_NEW/Spectrum/SpectrumHUD_NEW.cs` —— 曲线与 `driftContinuum` / `wrapSpectrum`
- `Assets/_Core_NEW/Data/SpectrumProfile_NEW.cs` —— 每层参数
- `Assets/_Core_NEW/Assets/SP_*.asset` —— 实际数值
- `Assets/Scripts/AbsorptionVolume.cs`、`PurpleCloudMultiVolume.cs` —— 世界里真实的云
- `Assets/_Core_NEW/Journey/UniverseJourneyTracker.cs` —— 距离/相位，第 2 条要接的来源
