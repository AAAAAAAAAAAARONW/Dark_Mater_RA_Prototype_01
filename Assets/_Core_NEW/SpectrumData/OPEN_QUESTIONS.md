# Open Questions — Redshift & the Lyman-α Forest

**For the team meeting.** The baked journey spectrum is built on real cosmology and measured absorption (see `SOURCES.md`).

## Decisions so far (2026-09-27)

| Decision | Detail | Supersedes |
|---|---|---|
| ~~HUD shows transmission, Lyα only~~ → **HUD shows the quasar's spectrum with only Lyα absorption** | The quasar's own shape (Lyα peak, emission lines, continuum) is drawn from the start. The peak walks from the left edge to the right as the light redshifts, and the forest grows on its left. Only Lyα absorption is drawn; photoionisation, Lyβ and helium are not. This makes the journey continuous with the final reference frame. **Trade-off:** the red continuum sits at ~20% of the peak because the quasar emits less there, not because of absorption; it is smooth, unlike the narrow absorption dips | §7 |
| **Quasar at z ≈ 3.6** (like Q1422+2309), **VO changes** | Arrival shows a readable forest (older lines ~65% transmission, younger ~95%) instead of a black trough. VO edits and the one tracker field: `VO_CHANGES.md` | **Option B of §1**. §2, §3 and §6 described the z = 6.7 quasar and no longer apply to the build |
| **Sliding view + zoom into the reference at arrival** | During the journey the bar spans 1216 → 9,000 Å, so no part is permanently empty. At Earth it zooms continuously into the classic figure's framing (emitted 1000–1350 Å) and the real spectrum rises in, peak included. Final frame ≈ Q1422 (forest-to-continuum level 0.646 vs ~0.7, with the measured Selsing 2016 composite). See README §4b | §8 points 2–3. The final frame shows **brightness, not transmission**; only there does the absorption-only rule give way |

**Still open:** §1's physics caveat now applies from **z ≈ 2.46**, the first 7.7% of the journey. After that, lines are drawn as the gas's Lyα fingerprint, but they fall on light already removed below 912 Å. §4 (verify recalled numbers), §5 (real spectrum from the client) and the timeline cards in `VO_CHANGES.md` are also still open.

The sections below are kept as the record of how we got here. Numbers in §1–§3 and §6 are for the old z = 6.7 quasar.

---

## 1. The forest only forms in the first 3% of the journey

**What the script says (Phase 8):**
> "Every cloud of hydrogen gas you cross along the way leaves a fresh absorption line."
> "By reading this forest like tree rings, we can map the hidden, invisible web of the universe."

**What the data says:** for this quasar, that stops being true very early on.

| | Value | Where it comes from |
|---|---|---|
| Quasar redshift | **z = 6.70** | 13.0 Gyr lookback (the tracker's start), Planck 2018 cosmology |
| Universe age at emission | **0.81 Gyr** | ✓ matches VO "less than a billion years" |
| Total stretch at Earth | **×7.70** | ✓ matches VO "more than seven times" |
| **Forest freezes at** | **z = 4.78**, 0.44 Gyr after emission | = first **3.3%** of the journey's light-travel time |

**Why it freezes, in three steps:**

1. A hydrogen cloud only absorbs light that is at **1216 Å where the cloud is**.
2. Light is stretched as it travels. At the second cosmic web (z ≈ 0.8), the light that is at 1216 Å left the quasar at about **270 Å**.
3. Light that left shorter than **912 Å** ionises hydrogen and is absorbed within the first few hundred million years. The bake applies this using the measured mean free path of ionising photons.

So after z ≈ 4.8, every cloud the light crosses would absorb at a wavelength where there is no light left. **No new line can appear.** The barcode is written early and then only travels.

**What this affects:**
- Phase 8 VO ("every cloud you cross", "fresh absorption line")
- Phase 6–7 implication that the cosmic web and the intermediate galaxy leave marks on this light. **They leave none.**
- The CosmicWeb redshift demo: it now shows *reading what was already written*, not *writing*.

**What still holds, and arguably holds better:**
- The tree-ring metaphor. Tree rings are laid down early and carried for life.
- Redshift itself: the whole spectrum keeps stretching the entire way.
- "UV set out, IR arrives" happens visibly on the bar. The quasar's Lyα peak crosses the visible band (4000–7000 Å) between z ≈ 1.3 and z ≈ 0.3, roughly 9 to 4 billion years ago. **At the second cosmic web (z ≈ 0.8) the peak is at about 5,200 Å, which is green.** That's a strong moment for the demo: *"right now, your light would look green"*. *(Old z = 6.7 quasar. With the chosen z = 3.62 the peak is ~3,120 Å, near-UV, at the second cosmic web and turns visible only below z ≈ 0.4.)*

**Options:**

| | Approach | Cost |
|---|---|---|
| **A** *(current)* | Faithful. Forest forms fast and goes nearly black, then freezes. VO Phase 8 is rewritten as "reading the record". | VO rewrite. The demo becomes a reveal, not a live process |
| B | Use a lower-redshift quasar (z ≈ 3), where the forest stays transparent for longer | Breaks "under a billion years", "seven times" and "13 billion years" |
| C | Keep "every cloud leaves a line" as explicit artistic licence | The client's own researchers work on exactly this physics |

**To decide:** A / B / C. If A, who rewrites the Phase 8 VO?

---

## 2. At arrival, the forest is almost black

A real z ≈ 7 quasar spectrum shows almost no light blueward of Lyα. The universe before z ≈ 6 still held enough neutral hydrogen to absorb nearly everything (the Gunn–Peterson trough). Only thin "transmission spikes" get through.

The bake reproduces this:

| Region at Earth | Mean transmission |
|---|---|
| The whole forest (7021–9362 Å) | **~2%** |
| Below the Lyman limit (< 7021 Å) | **~0.01%** |

**On screen:** at arrival, the bar is dark everywhere left of the bright Lyα peak, with faint spikes. It is not the evenly spaced "barcode" a z ≈ 3 forest would look like.

**To decide:** is a nearly black forest acceptable as the final image? If not, options include:
- a display gain on the forest region only, labelled as such
- drawing the forest as its absorption depth, not its remaining light

---

## 3. "The first line" is real, and it sits in the proximity zone

The first line the light acquires (from the tutorial) is cut about **0.25 proper Mpc** from the quasar, **inside the quasar's proximity zone**. That is the ~1.5 Mpc bubble where the quasar's own light has ionised the hydrogen, so a line there can actually be seen. The bake picks it out automatically (`AbsorberZ[0]`, z = 6.696).

At arrival it sits just blueward of the Lyα peak, at 0.88 of the bar.

This is good news for the demo: the tutorial's line is a real, identifiable feature.

**To decide:** nothing blocking. Worth confirming the framing ("your first line was cut in the quasar's own neighbourhood") with a scientist.

---

## 4. Several input numbers are recalled, not checked

The model's **structure** is standard. Several **input values** were entered from memory of the papers and must be checked against the published tables before this is presented as data. They are flagged `approx` in the CSVs and listed in `SOURCES.md`:

- **Emission line strengths:** Vanden Berk et al. 2001, Table 2
- **Mean transmission at z ≥ 5:** Bosman et al. 2022
- **Mean free path values:** Worseck et al. 2014, Becker et al. 2021

None of these would change the conclusions in sections 1–2. Those follow from 912 Å physics and the Gunn–Peterson trough, which are not in doubt. They would change the exact shading.

**To decide:** who verifies the tables, and when?

---

## 5. Flag: a real spectrum could come from the client

Carnegie Observatories astronomers have worked on exactly these objects. The z = 7.54 quasar J1342+0928 was discovered by a team led by Eduardo Bañados, then at Carnegie.

A reduced, public spectrum of one of the known z > 7 quasars (J1120+0641, J1342+0928, J0313−1806) would let us:
- validate the bake's final row against a real observation
- or show the real spectrum at the arrival moment

**Not requested yet** (per decision). Raise at the meeting.

---

## 6. Most of the bar is black for most of the journey

This follows from §1–2 and is **physically correct**. It is still a display problem. Measured from the bake, in the forest band (light that left the quasar between 912 and 1216 Å):

| Light is at | Moment | Mean transmission | Columns > 10% | Separate spikes |
|---|---|---|---|---|
| z = 6.65 | just after the tutorial | 98% | 103 / 104 | — (nothing crossed yet) |
| z = 6.0 | forest forming | 38% | 48 / 103 | 5 |
| z = 5.5 | forest forming | 6% | 15 / 103 | 7 |
| z = 4.78 | **freeze** | 1.5% | 1 / 103 | 1 |
| z = 0.8 | 2nd cosmic web (demo) | 1.6% | 2 / 104 | 2 |
| z = 0 | Earth | 1.3% | 2 / 103 | 2 |

Left of the forest band, everything is at zero: that light was below 912 Å and was absorbed. At the second cosmic web that is **the left ~57% of the bar**.

Part of the darkness is resolution. One HUD column is ~800 km/s wide, and real transmission spikes are ~100 km/s wide, so averaging over a column dims them further.

**Display options (none change the physics):**

| | Idea | Effect |
|---|---|---|
| **W** | **Moving window + scrolling wavelength ruler.** After the freeze, the bar frames the forest band and the peak, and the wavelength labels slide underneath | Redshift reads as "the numbers under your forest keep rising". About 7× more pixels per km/s, so spikes resolve |
| **L** | **Label the darkness.** "Absorbed by the neutral hydrogen of the early universe." | The trough becomes the story. It is how the end of reionization was discovered (Becker et al. 2001) |
| **M** | **Show the barcode as marks.** `RedshiftMarks_NEW` ticks over the dark band, at the real absorber positions | The lines are visible as *where they are*, even where no light remains |
| **S** | **Non-linear vertical scale** (square root) | Spikes become visible; must be labelled |

**To decide:** which combination. The suggestion is **W + L + M**.

---

## 7. Decided: the display shows Lyman-α absorption only

The piece draws **only Lyα lines**: born at 1216 Å, carried redward. Photoionisation below 912 Å, Lyβ and helium stay in the physics (the flux grid) but are **not drawn**. Setting: `display_lya_only` in `bake_settings.csv`.

**What changes on screen:**
- The large black block left of the forest (photoionisation) is gone.
- New lines keep appearing at the birth tick **for the whole journey**, faint when young and darker when old. The step up to fully clear light is the redshift edge.
- The only dark part left is the **oldest end of the barcode** (lines cut at z > 6). That is Lyα itself, the Gunn–Peterson trough, so it stays.

| At | Older lines (cut at z > 4.8) | Younger lines |
|---|---|---|
| Earth | ~6% transmission | ~83% transmission, a readable forest |

**How this relates to §1:** on screen, "every cloud you cross leaves a fresh line" is now true. Physically, for *this* quasar, lines cut after z ≈ 4.8 fall on light that photoionisation had already removed. The display shows the gas's Lyα fingerprint along the path, not what a telescope at Earth would record. This is a legitimate teaching representation, but **it should be a conscious choice, and the VO should not claim the lines are "in the light that reaches Earth".** §6's table and "mostly black" problem describe the previous all-absorption display.

---

## 8. Target image: the classic Lyα-forest comparison

The team's reference for "what it should look like at the end" is the classic two-panel figure: 3C 273 (z = 0.158) above Q1422+2309 (z = 3.62). It plots **intensity against emitted wavelength**, 1000–1350 Å. The dense lines left of the 1216 Å peak in the lower panel are the Lyα forest.

**The model reproduces that figure** when baked for the same redshifts, on the same axes. The z = 3.62 bake shows the same dense forest left of the peak, at about 74% of the red-side continuum. This is a useful validation of the model.

Forest depth, measured as forest level (1050–1180 Å) ÷ red continuum (1270–1350 Å):

| Quasar z | Forest ÷ continuum | Universe age at emission | Stretch | Lookback |
|---|---|---|---|---|
| 0.158 (3C 273) | 1.30 (few lines; the ratio is above 1 because of the emission-line wings) | 11.8 Gyr | ×1.16 | 2.0 Gyr |
| **3.62 (Q1422)** | **0.74, a dense, readable forest** | **1.74 Gyr** | **×4.62** | **12.1 Gyr** |
| 6.70 (ours) | 0.018, nearly black except the proximity zone | 0.81 Gyr | ×7.70 | 13.0 Gyr |

**What it takes to end on that image:**

1. **The quasar's redshift.** Only a z ≈ 3–4 quasar ends like the reference. Ours (z = 6.7) ends black, as real z ≈ 7 quasars do. Choosing z ≈ 3.6 changes the VO's numbers: "less than a billion years" becomes ~1.7 billion, and "seven times" becomes ~4.6. The tracker's 13 Gyr start would also need changing. This is Option B from §1.
2. **Flux, not transmission.** The reference includes the quasar's own emission peak. The forest reads as "the lines to the left of the peak". Showing it means `hudDisplay = Flux` at least for the final view. That departs from §7's absorption-only rule.
3. **The astronomer's frame.** The reference is in *emitted* wavelength, so the peak is fixed at 1216 Å. The journey HUD uses the light's current frame (lines slide red). Suggestion: keep the sliding view for the journey. **At arrival, switch the HUD to the emitted-wavelength view** as the reveal: "this is what the telescope records, and how astronomers read it".
4. **Resolution.** The reference resolves individual lines (~6 km/s). The model image uses ~100 km/s, and the current HUD ~800 km/s. The final view needs its own high-resolution bake of the arrival spectrum only. That is cheap: one row.

**To decide:** quasar redshift (6.7 as scripted, or ~3.6 to match the reference), and whether the arrival view switches to the astronomer's frame.

---

## 9. Smaller items

- **The HUD shows transmission, not the quasar's spectrum** (decided). The piece treats the light as constant, like the rainbow trail, and shows only what was absorbed. The quasar's own emission shape (Lyα peak, emission lines, red fall-off) is divided out. This is also standard practice for forest analysis (continuum normalisation). `BakedSpectrumSource_NEW.hudDisplay = Flux` shows the physical spectrum when needed.
  - **Consequence:** the Lyα emission *peak* no longer appears on the bar. Redshift reads instead as the **edge between absorbed and clear light** moving redward.

- **The HUD bar is now a log axis from 1216 to 9,000 Å**, whenever `BakedSpectrumSource_NEW` is on. The procedural fallback still uses the old linear window.
- **Per-layer `continuumPeakPosition`** (the 0.32 / 0 / 0.42 jumps, including the blank `SP_CosmicWeb`) no longer matters while baked data is on. It is still broken in the fallback. Remove it, or fix `SP_CosmicWeb`?
- **The trail's colours don't know about wavelength.** Absorption lines appear on the trail in the right order but not under the matching colours. This was already true before.

## 10. Tutorial on the real data (2026-10-04): what changed in meaning

These follow from putting the tutorial on the same physics as the journey (README §4c). Each one needs a decision, or a line of VO changed.

1. **D7's description says "the curve and the bands hold still".** That is no longer true in Flux mode, which is the default and matches the journey. Redshift moves *everything* the light carries, including the quasar's own Lyα emission peak, which slides right along with the lines. The bands do hold still. Options:
   - **(a) Keep Flux and reword D7.** Recommended: "everything the light carries moves toward red together".
   - **(b) Use `curve = Transmission` in the tutorial.** A flat line with only the dips, so the curve does hold still, but the bar changes look at the cut into the journey.
2. **D9 folds UV and IR away, and every line is in the UV.** At departure, hydrogen's colour (1216 Å) is ultraviolet, and the tutorial's lines stay UV, reaching 4000 Å only near Earth. So after D9's fold the trail shows the visible band with **no lines on it**. Its description, "leaving the visible light and the lines carried in it", is now false. Options:
   - **(a) Don't fold at D9;** keep UV open into the journey.
   - **(b) Keep the fold and reword:** "the lines are in light you cannot see".
3. **Atoms show part of a real line.** The real forest at z ≈ 3.5 is dense: half the light is absorbed on average, and many absorbers are saturated troughs 10–20 cells wide. Each atom reveals the deepest absorber the light has just crossed, capped at ±5 cells (`maxLineHalfCells`) so D6's three lines read as three. The rest of each trough, and every other line, appears at D9's forest reveal. Positions and depths are real; the *selection* is the teaching device.
4. **The proximity zone is effectively off at z = 3.62.** The bake report says R_ion = 0.00. The zone is defined the way papers measure it (where transmission falls to 10%), but at z = 3.6 the *mean* transmission is ~0.54, so that definition gives no zone. In reality the quasar still thins the gas over a few Mpc. The first line therefore sits right at the quasar (z 3.6152, saturated). It is harmless for the piece. A z-appropriate definition (flux-ratio based) would fix it; flag for the science meeting.
5. **UV on the trail.** The tutorial's trail colours now follow real wavelengths, on a piecewise axis (`trailAxis = AllBands`): UV 55% of the ribbon (20% far UV as an edge margin, 25% the 1202–1400 Å line region, 10% the rest), visible 30%, IR 15%. The ribbon does not follow the bar's zoom. UV is drawn at a constant `uvBrightness`, not faded towards the edge, so the black line stripes stay visible. **Check how the ribbon reads in the room.** If it is too dim, raise `uvBrightness` on the tutorial's `PhotonSpectrumTrail`, or switch `driveTrailColours` off to keep the old artistic ribbon (lines then sit under colours that are not their wavelength).
6. **Pace.** The tutorial covers ~1.5% of the journey in ~60 s; the journey's quasar phase covers the next ~7% in ~8–14 s. The black hides the change of pace, and the HUD holds for ~2 s at the start of the journey until the tracker catches up.
