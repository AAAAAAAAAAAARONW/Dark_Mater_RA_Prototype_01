# ElevenLabs 配音生成

逐句生成 `VO_Script_Draft_v3.md`（Tutorial + Journey）的 AI 配音，文件名直接对上 Unity 里的配音表。这个文件夹名字以 `~` 结尾，Unity 不会导入它。

## 第一次

1. 设 API key（只放在环境变量里，不进任何文件）：
   ```
   setx ELEVENLABS_API_KEY "你的key"
   ```
   设完重开一次终端。
2. `copy config.example.json config.json`
3. `python elevenlabs_vo.py voices` 列出你账号里的声音（免费），把选中的 `voice_id` 填进 `config.json`。
4. **先用一句短的试**，不要直接跑全套：
   ```
   python elevenlabs_vo.py test "Ahead of you is twelve billion years of travel."
   ```
   听 `staging/_test.mp3`，不满意就调 `voice_settings` 再试。**定下来之后就别再改 config**：改任何一项，整套都会被视为需要重新生成。

## 生成

```
python elevenlabs_vo.py plan                  # 只看：要生成哪些、多少字符、账号还剩多少。不花钱
python elevenlabs_vo.py generate --go         # 真正生成，进 staging/
python elevenlabs_vo.py install               # 听完后拷进 Assets/VoiceOver，覆盖同名文件
```

然后在 Unity 里：**VO Timeline → Scan Recordings → Report**。

- 只生成某几句：`--only D5,N_A1,E2_Before_A`；只生成一半：`--part tutorial` / `--part journey`
- **改了剧本里某一句**：再跑 `plan`，只有改过的那句会出现在清单里
- 发音不对：在 `config.json` 的 `say_as` 里加一条替换（只影响发给 ElevenLabs 的文字，不改剧本），只有含这个词的句子会重新生成

## 语气 tag

`delivery.json` 给每句加一个语气 tag，比如 `[softly]`、`[in awe]`。`"*"` 是默认值，单句写了就用单句的（`E2_Before_A` → `E2_Before` → `*`，写 `""` 表示这句不加）。

- tag 只发给 ElevenLabs：剧本里没有，Unity 里也看不到
- 只有 `eleven_v3` 模型认 tag。换成别的模型时脚本会自动不加 tag，因为旧模型可能把方括号念出来
- 改一句的 tag，只重新生成那一句
- 先单句试：`python elevenlabs_vo.py test "Behind you, the quasar is already a single point of light." --as C4`（会带上 C4 的 tag）。也可以直接把 tag 写进引号里试
- v3 的 `stability` 只有三档：0.0（Creative，起伏大）、0.5（Natural）、1.0（Robust，最稳但 tag 效果弱）

## 省额度的机制

- `plan` 永远不花钱；`generate` 不加 `--go` 也不花钱
- `ledger.json` 记着每句生成时发出去的「文字 + 声音 + 模型 + 参数」。没变的句子永远不会再生成一次。**把 ledger.json 提交进 git**，队友就不会重复付钱
- 每生成一句就写一次 ledger，中途断了重跑只补没完成的
- 全套约 4,600 字符（Tutorial 约 2,600，Journey 约 2,000）

## 文件名

已有录音的句子沿用原文件名（如 `D5_Contact.mp3`），覆盖后 GUID 不变，Unity 里的接线和 cue 都不用动。新句子用行 id 命名（`E2_Before_A.mp3`、`J3b.mp3`），Scan Recordings 会自动对上。
