# ArtV2 — the HUD art for the curved wall

Imported from `UI test1/` as delivered. The files there had names Unity cannot live with
(`journey tracker-1horizontal-80%.png.png.png`), so they were renamed on the way in. The
pixels are untouched.

| Here | Delivered as | Size |
|---|---|---|
| `JourneyTracker_H1_60/80.png` | `journey tracker-1horizontal-60/80%` | 5922 x 605 |
| `JourneyTracker_H2_60/80.png` | `journey tracker-2horizontal-60/80%` | 5925 x 605 |
| `JourneyTracker_V_60/80.png` | `journey tracker-verticle-60/80%` | 656 x 2292 |
| `LAF_60/80.png` | `LAF-60/80%` | 7080 x 624 |
| `Callout_CosmicWeb_60/80.png` | `cosmic web 60/80%` | 2244 x 1008 |
| `Title_JourneyOfLight.png` | `title1` | 2364 x 400 |
| `Title_EnteringCosmicWeb.png` | `title2` | 3444 x 400 |

**60 and 80 are the panel's opacity**, not two different drawings: the same artwork over a
more or less transparent background. Which one holds up over moving imagery is a decision
for the wall, which is what `UIArtVariant_NEW` and the art panel in
`PlaytestBuild_NEW_ArtPreview` are for.

The nine `tutorial*` files are not here: they are already in the project as
`_Core_NEW/Tutorial/Assets/Hints/TutorialHint_*.png`, imported with the tutorial work.

## Import settings, and why they are not the project default

- **Max size 8192.** The project's other UI sprites are capped at 2048. A display row is
  9600 px across, so a 5922 px asset capped at 2048 would be thrown away twice over: once
  by the cap, once by stretching it back up on the wall.
- **Uncompressed.** These are flat colours with long diagonal edges, which is what DXT is
  worst at. The point of putting them on the wall is to judge those edges, so the
  compression artefacts would be judged instead. If memory becomes a problem, this is the
  first setting to trade away.
- No mipmaps, alpha is transparency, sprite (2D and UI) — the same as the project's other
  UI sprites.

## Adding a version

Drop the file in here, then add a row to the element's `UIArtVariant_NEW` in the scene:
a label for the button and the sprite. The panel picks it up with no other wiring.
