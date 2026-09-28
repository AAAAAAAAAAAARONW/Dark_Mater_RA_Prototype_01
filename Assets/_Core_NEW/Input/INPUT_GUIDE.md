# Input Framework — Guide

One control scheme for the **tutorial and the journey**, across **three controllers**.

> **The one rule:** gameplay code never calls `Input.GetAxis` / `Input.GetButton` / `Input.GetKey` for controls. It calls `InputScheme_NEW`.

---

## 1. Why this exists

Unity 2019.4's legacy Input Manager numbers axes and buttons by **hardware index**, not by controller model. The Vizlab Logitech at Carnegie Observatories reports differently from Xbox on almost every axis:

| Unity axis name | Hardware axis | Xbox | **Vizlab Logitech** |
|---|---|---|---|
| `Horizontal` / `Vertical` | 1 & 2 | Left stick | **D-pad** |
| `RightStickX` | 4 | Right stick X | **Right stick Y** |
| `RightStickY` | 5 | Right stick Y | **Left stick X** |

Code that hard-codes axis names doesn't break cleanly on the Logitech — it **scrambles**. Visitors assume they're holding it wrong.

---

## 2. Architecture

```
PadProfile_NEW              Data. One immutable profile per controller.
      ▲
      │ looks up
InputScheme_NEW             The only reader. Static. Everything goes through here.
      ▲                 ▲
      │                 │
TutorialInput_NEW     OrbitCameraRig_NEW (look)
(forwarder — keeps    PlayerRig_NEW      (recentre, speed)
 the old API alive)
```

| File | Kind | Attach? | Purpose |
|---|---|---|---|
| `PadProfile_NEW` | Static data | No | Axis names, button codes, printed letters, deadband — per controller |
| `InputScheme_NEW` | Static class | No | Reads input through the active profile |
| `InputProfilePinner_NEW` | MonoBehaviour | **Yes — scene root** | Forces a specific profile at startup |
| `InputDiagnostics_NEW` | MonoBehaviour | **Yes — scene root** | F4 live readout for on-site checks |
| `TutorialInput_NEW` | Static class | No | Legacy API for tutorial scripts; forwards to `InputScheme_NEW` |

---

## 3. The control scheme

| Control | Meaning | Where it's decided |
|---|---|---|
| Right stick | Look | Rig (`OrbitCameraRig_NEW`, `FirstPersonLookRig_NEW`) |
| Left stick | Zoom (tutorial only, for now) | `TutorialZoom_NEW` |
| **A** | Confirm / recentre / emit — one meaning everywhere | `InputScheme_NEW.ConfirmDown()` |
| **B** | Pause | `InputScheme_NEW.PauseDown()` |
| D-pad | Attendant only. Never in a prompt | `PlayerRig_NEW` when `speedInput = Attendant` |

Keyboard fallbacks: **Space** = A, **P** = B, **mouse** = look, **WASD** = left stick (Xbox/PS profiles only).

---

## 4. API cheat sheet

```csharp
using S = InputScheme_NEW;

// Sticks (deadbanded)
float x = S.StickX(S.Stick.Right, S.Deadband());
float y = S.StickY(S.Stick.Right, S.Deadband());   // positive = pushed away = up

// Look, in degrees this frame. Stick OR mouse, whichever is larger.
float dx = S.LookX(S.Stick.Right, sensitivity, deadband, Time.deltaTime);
float dy = S.LookY(S.Stick.Right, sensitivity, deadband, Time.deltaTime);

// Buttons (pressed this frame)
if (S.ConfirmDown()) { ... }
if (S.PauseDown())   { ... }

// Prompts — ALWAYS draw the glyph from here, never a hard-coded "A"
label.text = S.ConfirmGlyph() + "  TO RECENTRE";

// Attendant-only D-pad
float dpad = S.DPadY();

// Idle detection (any stick past deadband, any key, any mouse move)
if (S.AnyInput(S.Deadband())) { ... }

// Profile management
S.Pin("Vizlab");        // force a profile
S.Cycle();              // next profile, pinned
PadProfile_NEW p = S.Pad;
```

`Stick.Either` exists **only** for idle detection. Never use it for a control.

---

## 5. The three profiles

| | Xbox | PlayStation | **Vizlab** (exhibition) |
|---|---|---|---|
| Right stick | `RightStickX/Y` | `PadRightStickX/Y` | `Joy3` / `Joy4` |
| Left stick | `Horizontal/Vertical` | `Horizontal/Vertical` | `Joy5` / `Joy6` |
| D-pad | `Joy6` / `Joy7` | — | `Joy1` / `Joy2` |
| Y invert (L / R) | false / false | false / false | **true / true** ⚠ unverified |
| Confirm button | 0 | 1 (+ 0 as alt) | **1** |
| Pause button | 1 | 3 | **2** |
| Glyphs | A / B | ✕ / △ | A / B |
| Deadband | 0.10 | 0.10 | 0.15 |

**Buttons are mapped by the letter printed on the pad**, not by the number it sends. On the Logitech, the button printed **A** sends button 1 — so confirm listens to 1 and the HUD still draws "A".

---

## 6. Scene setup

On the systems root of **every scene** that takes input:

1. Add `InputProfilePinner_NEW` → `profileId = Vizlab` for exhibition builds. **Leave empty** at a desk to auto-detect.
2. Add `InputDiagnostics_NEW` (hidden by default).

The pinner runs at execution order **−10000**, before anything reads input.

> **Exhibition builds must pin.** Detection failing on site looks like visitor error, and nobody present can fix it.

---

## 7. How to change things

### Fix a wrong axis, sign, or button on site

1. Press **F4** → read the raw `Joy1`–`Joy10` rows while moving each stick
2. Edit the **Vizlab** entry at the bottom of `PadProfile_NEW.cs`
3. Rebuild

Common fixes:

| Symptom | Fix |
|---|---|
| Looking up/down is inverted | Flip `rightStickYInvert` (the second `true` on the `"Joy3", "Joy4", true` line) |
| Pressing A does nothing | Check F4's "BUTTONS HELD" when pressing A; set `confirmButton` to that number |
| Wrong stick turns the view | Swap the axis names on the right-stick line |

### Change sensitivity, recentre speed, smoothing

**Not here.** Those are feel, and live on the rig (`OrbitCameraRig_NEW`, `FirstPersonLookRig_NEW`). They must not vary per controller.

### Add a new controller

1. Add a `public static readonly PadProfile_NEW` in `PadProfile_NEW.cs` (copy an existing one)
2. Add it to `All` — **order matters**: first name-match wins in detection
3. If it needs new axes: use existing `Joy1`–`Joy10`. Don't add new named axes to `InputManager.asset`
4. Optional: add a value to `TutorialInput_NEW.PadLayout` + the two converters (`ToLayout`/`ToProfile`) if tutorial debug tools need to show it

### Add a new action (e.g. *Inspect* on Y)

1. **`PadProfile_NEW`** — add fields `inspectButton` + `inspectGlyph`, add constructor params
2. Fill them in on **all three** profiles (the positional constructor won't compile until you do — that's intentional)
3. **`InputScheme_NEW`** — add `InspectDown()` and `InspectGlyph()`, copying `PauseDown()` / `PauseGlyph()`
4. **`InputDiagnostics_NEW`** — add a row next to CONFIRM / PAUSE so it's checkable on site
5. Add it to the table in §3

### Change what an existing button *means*

Don't. "One button, one meaning, tutorial and journey alike" is a design rule. If a single beat needs A to mean something else (e.g. tutorial C2's *emit*), that beat temporarily disables the default behaviour and restores it on exit — see `FirstPersonLookRig_NEW.SetConfirmRecentres()`.

---

## 8. Gotchas

| Gotcha | Why it bites |
|---|---|
| **Double inversion** | `RightStickY` and `PadRightStickY` already have `invert: 1` in `InputManager.asset`, so their profiles use `false`. `Joy*` axes are raw, so Vizlab uses `true`. Invert twice = upside down |
| **`Horizontal` / `Vertical` are the D-pad on Vizlab** | Any code reading those names directly is wired to the D-pad on the exhibition pad. Use `InputScheme_NEW` |
| **`TutorialInput_NEW.LookStick` is serialized** | Stored as an int on `FirstPersonLookRig_NEW`. Don't renumber `Left=0, Right=1, Either=2` |
| **F5 pins** | Cycling on the diagnostic page pins the result; auto-detect won't run again this session |
| **`Joy*` axes have `dead: 0.19`** in the asset | Values below 0.19 already read as 0 before the profile's deadband applies. Kept to match the existing axes |
| **`PlayerRig_NEW.speedInput` defaults to `Off`** | Applies to every scene using `PlayerRig_NEW`, including `PlaytestBuild_NEW`. Set `Visitor` to restore the old left-stick speed control |

---

## 9. On-site checklist (no Unity needed)

1. **F4** → first line reads `Vizlab Logitech (pinned)`
2. Wrong? **F5** until it is
3. Push the right stick **away** from you → `LOOK Y` reads **positive**
4. Press the button printed **A** → `CONFIRM ... <- DOWN`
5. Press the button printed **B** → `PAUSE ... <- DOWN`

Anything wrong → §7, *Fix a wrong axis, sign, or button on site*.

---

## 10. Debug keys

| Key | Action |
|---|---|
| F4 | Show / hide input diagnostics |
| F5 | Cycle pad profile (pins it) |

*Other F-keys in the project: F1 layer overlay, F2/F3 tutorial director, F6/F7 journey sequence, F8 redshift advance.*
