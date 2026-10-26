# VO notes

The working notes behind `VO_Script_v7.md`: the writing rules, how each moment works in the build, science checks, the feedback log, and what to re-record. The script holds only the lines; change words there, everything else here.

Contents: 1 Writing rules · 2 The script file · 3 How the moments work · 4 Science checks · 5 On-screen text to fix · 6 To re-record · 7 Status in Unity · 8 Open questions · 9 Feedback log

## 1 · Writing rules

- The voice tells the story. It never gives instructions. "Look right" and "press A" belong to the hint line and the `N_` prompts.
- One line per beat at most. Control beats (A3, B1 to B4, C2, D10) get no story line. A1 and A2 keep their story lines and also get a prompt. The dark matter moment is the one exception: one line before the press and one after it, because there the press itself is the point of the story.
- About 140 words a minute (2.3 words a second). Every line lists its estimated read time.
- Voice: calm, close, second person. Wonder, not lecture. **Female voice** (the scientists' request). The whole set is re-recorded once in the new voice, after the scientists' yes/no pass.
- Say "absorb" and "wavelength" for what hydrogen does to light, not "take". Absorption removes the light at one wavelength, and that is why a colour goes missing.
- Never name a colour of the art (the gas, the dark matter, the threads). The art changes; the recordings should not have to. Colours of the light itself (ultraviolet, purple, red) are fine.
- Do not call the light's ribbon a "trail" or a "streak". It is the light itself, spread out by colour.
- No tree-ring analogy anywhere: it was heard as the origin of "forest". (Cut on 25 October.)

## 2 · The script file

Both tools read `VO_Script_v7.md`: Unity's **Import Script** (`VoiceOverTableTools_NEW`, the tutorial half) and `Tools~/elevenlabs_vo.py` (both halves). The format they expect:

- `## Part 1 · Tutorial` and `## Part 2 · Journey` split the two halves. Anything after the next `## ` heading is ignored.
- Every line is a row of a four-column table: `| ID | When | Line | ≈ |`. Headings, prose and header rows are ignored, so sections and notes between tables are free.
- **ID** is the line id and the start of its file name. `N_` makes it a button prompt (a nudge in Unity). The Unity beat is the id without `N_` (`N_A1` → beat A1, `N_Pause` → the pause, `Ending` → the ending).
- **When** is free text. `Cue 0.5 s` in it sets the beat's narration delay in Unity, on a beat that is new to the table.
- **≈** is the read time in seconds.
- Change markers like `[v6]` are stripped if they turn up, but the script no longer uses them: changes are logged here instead.

Unity finds the script as the newest `VO_Script*.md` in `Assets/VoiceOver`, by name, so this file must not start with `VO_Script`.

## 3 · How the moments work

### Tutorial gating

Gated beats (A1, A2, C4, C5) hold until their line has finished. Cinematic beats run for their duration (below).

### Button prompts (`N_` lines)

**Why they exist.** GDD §5 keeps VO off control beats because hearing an instruction is slower than doing it. That holds for players who already get it. But the biggest failure in the May playtest was non-gamers not working out the camera, and text arrived faster than they could read it. A hint line alone may not reach exactly the players who need it. Each prompt names the exact control (Xbox pad): right stick = look, left stick = zoom, A = confirm / recentre, B = pause.

**How they play.**

- Trigger: the beat has been open for 5 s and its gate is not met. Tunable per beat.
- Plays once per beat. Never repeats, never loops.
- If the player completes the gate mid-line, it fades out over 0.3 s.
- The hint text stays on screen as now. The prompt adds sound; it does not replace the hint.
- A prompt timer, not a timeout. The beat still never advances on its own (§5, §12.3).
- The pause prompt (`N_Pause`) plays once, 2 s after any pause, if the visitor has not resumed. The pause card already says B TO RESUME; this says it out loud for the visitor who is not reading. A line cut off by the pause carries on after they resume.

**Cost in Unity.** One idle timer and one fade-out on gate complete, on the gated beat base class, so every control beat gets it at once. **If the team says no** to prompts, delete the `N_` rows; nothing else depends on them.

### Phase 3 · redshift as soon as the line moves (25 October)

In the build the bar drifts from D3 on, so D5's line starts sliding the moment it is cut; it only shows once D5's slow motion ends. Until now D6 said "the same line gets darker" over a line that was visibly moving, and redshift waited for D7. Redshift is now D6, and more gas is D7. Because the light keeps stretching, more gas cuts fresh lines rather than deepening the first, and D7 says so; "the more gas, the deeper the line" stays as a general rule.

**Build change needed:** swap D6's and D7's content in `TutorialSceneBuilder_NEW` (D6: `StartDrift`, `Redden`, hide the line arrows when it ends; D7: arm the cloud group). Rename the beats to match.

### Phase 3 durations

Each is the duration field on the beat. The builder never overwrites a tuned value. "Now" is the current build, not the GDD. D1, D3 and D7 were already lengthened to fit the recorded clips.

| Beat | D1 | D2 | D3 | D4 | D5 | D6 (redshift, was D7) | D7, D8 | D9 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Now (build) | 6.5 s | 12 s | 6.2 s | 10 s | 8 s | 9.6 s (on D7 today) | 10 s | 5 s |
| Suggested | 7 s | 7 s | 7 s | 10 s, ends on contact | 10 s | 10 s | 10 s | 7 s |

Lengthening D5 also lengthens its slow motion.

### Tutorial ending

The ending line fades to the title card, then hands off to the journey (J0). Phase 4 (E1 to E3, a dark matter toggle at the end of the tutorial) is cut: dark matter is taught in the journey instead. The old E1/E2 lines are in v5 and in git history; they were never built or recorded.

### Journey opening

The journey opens on 0.1 s of black; J0 starts as the picture fades up. J1 plays as the camera turns to look back.

### Dark matter moment (first cosmic web)

Right after J3, the narration introduces dark matter while the light keeps flying. The visitor presses **Y** to show or hide a drawing of it. Built as `DarkMatterMoment_NEW`.

**The two versions** (playtest both, keep one; one Inspector switch, `DarkMatterStop_NEW.version`):

| | Version B · hidden by default (the build's default) | Version A · shown by default |
| --- | --- | --- |
| Inspector value | `HiddenByDefault` | `ShownByDefault` |
| On arrival | Only the gas | Gas and the dark matter drawing |
| Y does | Shows the dark matter | Hides the dark matter |
| Hint line | `Y · SHOW DARK MATTER` | `Y · HIDE DARK MATTER` |
| Lines | `J3_DMoff_*`, `N_J3_DMoff` | `J3_DMon_*`, `N_J3_DMon` |

**How it plays** (the same in both versions):

1. J3 ends, and the **before** line starts. The hint line comes up with it.
2. The visitor presses Y while flying. The drawing eases in or out over 2.5 s, and the **after** line plays.
3. **Press during the before line:** the drawing changes at once, and the after line follows the before line.
4. **No press:** the prompt plays 1.5 s after the before line ends, once. At 6 s the drawing changes on its own and the after line plays, so a walk-up visitor still sees it and hears why.
5. Afterwards Y keeps working, silently, for the rest of the first cosmic web.

**Rules for these lines.**

- The light never stops or slows: stopping changes the trail, and the journey never waits. The first cosmic web is lengthened instead (same speed, 5 units/s), so J3, the moment and J3b fit in about a minute. The Flight Path window shows where each line falls.
- Y shows or hides a **drawing**. The gas never moves. Both versions say "drawn so you can see it", because dark matter gives off no light.
- Both versions use the scientists' two images: dark matter is the web's **scaffolding**, and the gas is the **beacon** that shows where it lies.
- The dark matter is called a "haze": the web gas glows too, so "glow" would be ambiguous.
- Y, not B: B is pause everywhere (GDD §4, one button one meaning). Y rather than X because X sits beside A, where the thumb has rested for the whole tutorial; Y is a deliberate reach, which suits a switch.

### J3b · first filament

Said once per run: the first time the photon passes through a filament and the absorption line appears on the spectrum. The game marks the same moment on screen, also once (the tutorial's arrows pointing at the new line on the bar). After that, filaments and lines go by without comment; J6 picks the idea up again. Place it with the Flight Path window's **J3b here**, on a filament after the dark matter moment's lines end.

### Galaxy

J4b plays straight after J4. The journey stays slow while it is said, then speeds up to leave. J2 (the HUD) follows J4b: the opening is only 14 seconds of flight, and by the galaxy the marker has visibly moved along the chain. J2b is optional.

### Redshift demo (second cosmic web, z ≈ 0.8)

Four lines over the 6 s replay and the 12 marked lines, about 42 s in all. J6_3 names the forest and says who Lyman was in the same breath, so nobody guesses where the name comes from. J6_4, the forest's value, is split out so J6_3 does not run to 19 s. **J6_4 needs a fourth slot** in `JourneyVoiceOverBuilder_NEW` (today F5 holds J6_3, lengthened across F6).

## 4 · Science checks

**Dates and numbers**

- **"Less than two billion years after the Big Bang" (A2, J0).** z = 3.62 → 1.74 Gyr after the Big Bang (Planck 2018). Correct.
- **"One small region of the early universe" (A2).** The age is the same everywhere at a given moment; what we see is one place. (A first try, "in this corner … two billion years have passed", wrongly implied the age differs from place to place.)
- **"Nearly five times" (J6_2).** 1 + z = 4.62. Correct.
- **"Five times heavier than ordinary matter" (dark matter moment).** Dark matter to ordinary matter ≈ 5.3 : 1 (Planck 2018). Correct.
- **"Eight minutes" (J8).** Sun–Earth light travel time is 8 min 20 s. Correct.
- **"Half its present age" at seven billion years ago (J5).** 13.8 − 7 = 6.8 Gyr. Close enough for the read. Moved from J4 to J5 in v4, because the galaxy and the second cosmic web were both at seven billion years ago, so the same date would have been said twice.
- **"Making stars faster every year" (J0).** At z ≈ 3.6 the universe's star-formation rate is still rising; it peaks around z ≈ 1.9 (Madau & Dickinson 2014). Correct.
- **"Faster than at any time before or since", ten billion years ago (J4).** The cosmic star-formation peak, "cosmic noon", is at z ≈ 1.9, about 10.4 Gyr ago. Requires the journey tracker's Galaxy phase at 10 billion.
- **"A hundred thousand years to cross a galaxy like this one" (J4b).** Light crosses a Milky Way–sized galaxy (about 100,000 light-years) in about 100,000 years. "Like this one" covers the art not stating a size. "Less than the blink of an eye": 100,000 years is 1/120,000 of the journey, so if the journey took an hour, the galaxy would take 0.03 s, and a blink is 0.1 to 0.4 s. The line never says light slows down; it does not.
- **"Dark energy is about to start speeding up the expansion" (J5).** The expansion starts accelerating at z ≈ 0.6 to 0.7, about 6 Gyr ago, just after the second cosmic web (z ≈ 0.8, 7 Gyr ago).

**Quasar and hydrogen**

- **"Outshines all the galaxy's stars" (A2).** A luminous quasar is typically 10 to 100 or more times brighter than its whole host galaxy. Correct.
- **"Hydrogen is the simplest element, and the most common in the universe" (D4).** About three quarters of ordinary matter by mass, about nine atoms in ten. Not "the most common material": dark matter outweighs all ordinary matter five to one, which the dark matter moment says.
- **"A cloud of hydrogen gas" (D4–D8).** The measurements reflect enormous numbers of atoms in a region of gas, not one atom; "cloud" is the scientists' preferred word.
- **"One wavelength" (D5).** A simplification: one narrow band, Lyman-alpha at 121.6 nm, which is ultraviolet; the build starts the line inside UV (§12.2 item 0b, still open).
- **"Each cloud cuts a fresh line" (D7, J3b).** It is the same Lyman-alpha line each time, landing at a different place on the spectrum because the light has been redshifted in between.

**Cosmic web and dark matter**

- **"The largest structure in the universe" (J3).** The cosmic web is the largest known structure. Its filaments are typically tens of megaparsecs long, a hundred million light-years and more, so "hundreds of millions of light-years" holds for its threads.
- **"Far too little gas here to pull itself into threads like these" (J3_DMoff_Before).** Ordinary matter is about one sixth of all matter, and until recombination it was held smooth by radiation. On its own it could not have collapsed into the web in the time available; dark matter's gravity had a head start, and the gas fell into it. A fair simplification.
- **"Drawn so you can see it" (both versions).** Dark matter gives off no light. The haze on screen is a drawing, and the lines say so.
- **"These threads are hydrogen gas" (J3b).** By mass, a filament is mostly dark matter. Its ordinary matter is gas, about three quarters hydrogen, plus the galaxies strung along it. Galaxy clusters sit at the nodes where filaments meet, not along the threads. Almost all of that hydrogen is ionized; the tiny neutral fraction (around one atom in 10⁴ to 10⁵ at z ≈ 2 to 3) is still enough to make the Lyman-alpha forest.
- **"No stars light them" (J3b).** The gas in intergalactic filaments is mostly far from any galaxy, so it is not lit by stars; it is seen only by what it absorbs. A fair simplification. (The art draws it glowing; that is a picture, like the dark matter haze.)

**The forest**

- **"After Theodore Lyman, the physicist who first measured this line of hydrogen" (J6_3).** Lyman found hydrogen's ultraviolet series at Harvard (1906 to 1914); Lyman-alpha is its first and strongest line, at 121.6 nm. If the scientists want "alpha" and "forest" explained too: "…: Lyman for the physicist who discovered this line of hydrogen, alpha because it is the strongest line in its family, and forest because, packed together, the lines stand like trees." About 15 s for that sentence.
- **"Maps of galaxies show only where stars shine" (J6_4).** A simplification of the scientists' point: galaxy surveys trace light from stars in the densest regions, while the forest traces diffuse gas through more of the volume.
- **"Cut lines into your light" (J6_3).** After z ≈ 2.46 the drawn Lyα lines are not in the light that reaches Earth (LAF & Redshift, open questions). J6 says "cut lines into your light", not "in the light that reaches Earth". Keep it that way.
- **"Each one marks a moment of your journey" (J9).** Each line comes from gas at a different distance along the path, so the lines are a record ordered in time.

## 5 · On-screen text to fix

Card art in `Assets/_Core_NEW/UI/Milestones`. The cards are PNGs, so these are art changes.

| Card | Now | Problem | Suggested |
| --- | --- | --- | --- |
| `JourneyMilestone_GalaxyCluster` | GALAXY CLUSTER · "Thousands of galaxies. Temperature: 100 million K." | Shown before the galaxy, ten billion years ago. Structures that early are not galaxy clusters yet. | **WHERE THREADS CROSS** · "Young galaxies crowd where the web's threads meet." Or drop the card. |
| `JourneyMilestone_LightAbsorption` | "H₂ cloud dims the signal — dark lines form in the spectrum" | H₂ is molecular hydrogen. The Lyman-alpha line comes from single hydrogen atoms in the gas. | "Hydrogen cloud dims the signal — dark lines form in the spectrum" |

The milestone text "infant star clusters" (Milky Way) means star clusters, not galaxy clusters. It is correct as it is. No VO line says "cluster".

**Timeline cards (`MilestoneSet_New.asset`), open since the quasar moved to z ≈ 3.6.** A card fires once the remaining distance drops below its threshold, so every card above the journey's start (12.07 Gyr) fires on the first frame:

| Card | Threshold | Fires |
| --- | --- | --- |
| Big Bang | 13.8 Gyr | first frame |
| Cosmic Microwave Background | 13.78 Gyr | first frame |
| First Stars Ignite | 12.5 Gyr | first frame |
| Cosmic Reionization | 12.0 Gyr | 0.07 Gyr in, seconds after the start |

- Three cards at once on the first frame: queue them, trim them, or move them to the tutorial.
- "Cosmic Reionization" at 12.0 Gyr ago (z ≈ 3.4) is late for **hydrogen** reionization, which completed about 12.6 to 12.8 Gyr ago (z ≈ 5.5 to 6). It matches **helium** reionization (z ≈ 3). The quasar shines after hydrogen reionization, so the card's wording matters.

## 6 · To re-record

The whole set is re-recorded in the female voice after the scientists' pass, so these can wait for that. Re-records keep their current file names, so clips already wired in Unity are replaced without rewiring. New lines get their ID as the file name, which is where `install` and the builders look.

| Line | File | Why |
| --- | --- | --- |
| A2 | `A2_ZoomIn` | "one small region of the early universe" |
| D1 | `D1_UVIRUnfold` | no "trail" |
| D3 | `D3_SpectrumBar` | "the same colors … purple toward red" |
| D4 | `D4_AtomApproaches` | "the most common element", not "material" |
| D6, D7, D8 | `D6_FourMoreAtoms`, `D7_Redshift`, `D8_OneMoreAtom` | Redshift moved to D6; D7 and D8 rewritten. D6 and D7 swap content, so the old file names no longer describe them. |
| J3 | `J3_CosmicWeb1` | shortened |
| J3_DMoff_Before, J3_DMoff_After, N_J3_DMoff | new | dark matter moment, Version B |
| J3_DMon_Before, J3_DMon_After, N_J3_DMon | new | dark matter moment, Version A |
| J3b | `J3b` | no "yellow" |
| J5 | `J5_CosmicWeb2` | last generation failed |
| J6_3 | `J6_3_Forest` | Lyman named; the forest's value moved out |
| J6_4 | new | the forest's value |
| J9 | `J9_Earth` | tree rings cut |
| Closing | `Closing` | observations, simulations, theory; around the world |

## 7 · Status in Unity

Done: every tutorial beat has a VO slot (narration plus prompt). One player queues lines and never overlaps two. Pause pauses the VO, and the 90-second return to attract stops it. The journey's dark matter moment (`DarkMatterMoment_NEW`, with the A/B switch) and the J3b trigger are built.

To do:

- Swap D6 and D7 in the tutorial builder (redshift first).
- Give the redshift demo a fourth slot for J6_4.
- Re-import the script in Unity (**Import Script**). The tutorial table still holds the v2 text for D4, D6, D8 ("a single atom"); nobody sees it (no subtitles), but its re-record flags depend on it.
- The two cards in §5.

## 8 · Open questions

1. **Which dark matter version?** A or B, after playtests.
2. **Explain "alpha" and "forest" too?** See the J6_3 science note for the longer sentence.
3. **Prompts at all?** The `N_` lines are for discussion with the team (§3).

## 9 · Feedback log

### 25 October · scientists' notes, second pass

| Note | What changed |
| --- | --- |
| A2: "the early universe" should be more precise | "This is one small region of the early universe, less than two billion years after the Big Bang." |
| D4: is hydrogen "the most common material"? | No: dark matter outweighs ordinary matter. Now "the most common element". |
| Introduce redshift when the first line starts to move | Redshift moved from D7 to D6; more gas is D7; D7 and D8 rewritten. Needs the builder swap. |
| Dark matter section was hard to follow | Tutorial Phase 4 removed; the journey moment described in one place (§3). Prompt B is "Press Y to see the dark matter." |
| D1: not "trail" | "Your light is made of many colors, spread out here from ultraviolet to infrared." It does not mention visible light, so D2 still reveals it. |
| D3: "from purple toward red", keep it light | "This bar is your spectrum: the same colors, laid out in a line from purple toward red." |
| D4–D8: a cloud of hydrogen, not a single atom | Done in v5. Unity's table still holds the old text until re-import. On screen: the H₂ card (§5). |
| J6_3: separate tree rings from "forest"; say who Lyman is | Tree rings cut altogether. J6_3 names Lyman. |
| The forest's value: gas reveals structure where there are no stars | Its own line, J6_4. |
| Opening: the quasar as the bright heart of a galaxy | Done in v5 (A2, C4). |
| J3: dark matter as scaffolding, gas as the beacon; keep "galaxies grow where the threads cross" | Both versions of the dark matter moment say "scaffolding" and "beacon". J4 unchanged. |
| Ending: observation + simulation + theory, and the research community | Closing: "astronomers around the world … Observations, simulations and theory together …" |
| Every visible "cluster" | No VO line uses it. The Galaxy Cluster card does and is wrong (§5). |
| The web gas is no longer yellow (25 October art) | J3b drops "yellow"; "glow" becomes "haze". Rule: never name an art colour. |
| The script mixed lines and working notes | Split into `VO_Script_v7.md` (lines, with when each plays) and this file. |

### 16 October · v6, dark matter moment

| Note | What changed |
| --- | --- |
| A dedicated place to explain dark matter, taught like the tutorial | New dark matter moment in the first cosmic web: before line with the Y hint, press, after line. |
| Do not stop: stopping changes the trail; keep flying | The light never stops; the press is offered while flying, and the first cosmic web is lengthened at the same speed. |
| Not B, which is pause | Y, as decided in v4. |
| Two versions, default on / default off | Version A (shown, Y hides) and Version B (hidden, Y shows), one Inspector switch. |
| Dark matter's gravity pulls gas into this structure | In both after lines. |
| Dark matter and the filament callout both in the first cosmic web; the second one is absorption and redshift | J3, the moment and J3b are all in the first cosmic web. |
| Y shows or hides the drawing; the gas does not move | The lines are about what is drawn, not a what-if. Tutorial E1/E2 superseded. |
| J3 | Shortened to name the web only. |

### 15 October · v5, scientists' feedback

| Feedback | What changed |
| --- | --- |
| Use a female voice | Voice direction. Re-record once, after the yes/no pass. |
| Quasar as the bright heart of a galaxy | A2 and C4. |
| Wavelength "from purple toward red" | D3. |
| A cloud of hydrogen gas, not a single atom | D4–D8. Absorption grows with more gas. |
| Dark matter as scaffolding, gas as the beacon | J3 rewritten. J4's "galaxies grow where the threads of the web cross" kept. |
| The forest's value | J3b "No stars light them"; J6_3 maps of galaxies vs the gas in between. |
| Separate tree rings from "forest" | Moved to J9 (cut altogether on 25 October). |
| Ending: what astronomers learn | J9 and Closing rewritten. |

### 14 October · v4, journey revision

The galaxy moved to ten billion years ago (cosmic noon); "half its present age" moved to J5; J5's dark energy line corrected to "about to start"; J2 moved to the galaxy; the toggle went on Y.

### 27 September · the quasar moves to z ≈ 3.6

The quasar moved from z ≈ 6.7 to z ≈ 3.6, the redshift of Q1422+2309 in the classic Lyman-alpha forest figure, so the arrival shows a readable forest instead of the near-black trough of a z ≈ 7 quasar (`SpectrumData/OPEN_QUESTIONS.md` §8). The journey became 12.07 Gyr (from 13.0), the universe 1.74 Gyr old at the start (from 0.81), the stretch at Earth ×4.62 (from ×7.70). Every line with those numbers was rewritten ("less than two billion years", "twelve billion years", "nearly five times", "more than seven billion years before our Sun was born"). One scene field goes with it: `UniverseJourneyTracker` → Phases[0] → Remaining Distance At Start = `12070000000`.

### 13 October · v3, team notes

"Take" → "absorb" and "wavelength"; J8 "light from our Sun"; C5 wording; J2 "at the bottom of the screen"; J3b added as a once-per-run filament callout.
