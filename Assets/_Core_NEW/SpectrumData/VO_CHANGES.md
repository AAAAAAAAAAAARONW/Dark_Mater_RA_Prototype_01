# VO & Timeline Changes — Quasar at z ≈ 3.6

**Decision (2026-09-27):** the quasar moves from z ≈ 6.7 to **z ≈ 3.6**, the redshift of Q1422+2309 in the classic Lyman-α forest figure. The journey's arrival then shows a readable forest instead of the near-black Gunn–Peterson trough of a z ≈ 7 quasar (see `OPEN_QUESTIONS.md` §8). The HUD keeps the sliding view to the end; no switch to the astronomer's frame.

Numbers below come from the bake (`Baked/SpectrumJourney_report.txt`), Planck 2018 cosmology.

| | Before (z = 6.70) | **After (z = 3.62)** |
|---|---|---|
| Journey start (lookback) | 13.0 Gyr | **12.07 Gyr** |
| Universe age at emission | 0.81 Gyr | **1.74 Gyr** |
| Stretch at Earth | ×7.70 | **×4.62** |
| Hydrogen's Lyα colour arrives at | 9,360 Å (infrared) | **5,610 Å (visible, yellow-green)** |
| Journey start vs. Sun's birth (4.6 Gyr ago) | ~8.4 Gyr before | **~7.5 Gyr before** |

---

## VO lines to change

| Beat | Current line | **Proposed** |
|---|---|---|
| **A2** | "…You are looking at the early universe, less than a billion years after the Big Bang." | "…You are looking at the early universe, **less than two billion years** after the Big Bang." |
| **C1** | "…Your journey across space will last thirteen billion years." | "…Your journey across space will last **twelve billion years**." |
| **Phase 8, ¶2** | "By the time you reach Earth, your light will be stretched to more than seven times its original wavelength. What set out as ultraviolet light will arrive as infrared." | "By the time you reach Earth, your light will be stretched to **more than four and a half times** its original wavelength. **The ultraviolet colour that hydrogen absorbs will arrive as visible, yellow-green light.**" |
| **Phase 9, Milky Way** | "…You set out on your journey about eight billion years before our Sun was born." | "…You set out on your journey about **seven and a half billion years** before our Sun was born." |
| **Phase 9, Solar System** | "…Your journey has taken thirteen billion years." | "…Your journey has taken **twelve billion years**." |
| **Phase 9, Earth** | "…a complete record of thirteen billion years of cosmic history." | "…a complete record of **twelve billion years** of cosmic history." |
| **Closing** *(optional)* | "…You end as a message from the dawn of time." | 1.7 Gyr after the Big Bang is early, but not the "dawn". Consider "…a message from **the universe's youth**." |

**Phase 8, ¶2 alternative, if "visible" is too subtle for the room:** "What set out as ultraviolet will arrive as light human eyes can see." This ties back to tutorial D2 ("Human eyes can only see this narrow band in the middle").

**Unchanged:** all tutorial lines (no numbers); Phase 6–7; Phase 8 ¶1, ¶3, ¶4.

---

## Scene change required

**One field.** `Gameplay_Scene_Ana` → `UniverseJourneyTracker` → Phases[0] → **Remaining Distance At Start**: `13000000000` → **`12070000000`**.

The tracker derives its distance-per-unit scale from this value, so phase 0 rescales by itself. The HUD clock follows. If this is left at 13 billion, `BakedSpectrumSource_NEW` logs a mismatch at startup.

---

## Timeline cards affected (`MilestoneSet_New.asset`)

A card fires once the remaining distance drops below its threshold. Every card whose threshold is **above the new start (12.07 Gyr)** therefore fires on the very first frame:

| Card | Threshold | Before | After |
|---|---|---|---|
| Big Bang | 13.8 Gyr | first frame | first frame |
| Cosmic Microwave Background | 13.78 Gyr | first frame | first frame |
| **First Stars Ignite** | 12.5 Gyr | 0.5 Gyr into the journey | **first frame** (new) |
| **Cosmic Reionization** | 12.0 Gyr | 1.0 Gyr in | **0.07 Gyr in**, seconds after the start |

**For the content owner:**
- Three cards at once on the first frame. Decide whether they should queue, be trimmed, or move to the tutorial.
- "Cosmic Reionization" at 12.0 Gyr ago (z ≈ 3.4) is late for **hydrogen** reionization, which completed ~12.6–12.8 Gyr ago (z ≈ 5.5–6). It matches **helium** reionization (z ≈ 3). Our quasar now shines *after* hydrogen reionization, so this card's wording matters more than before.
