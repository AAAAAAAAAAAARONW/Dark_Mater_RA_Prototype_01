# Scene Flow — Tutorial → Journey

`SceneHandoff_NEW` carries the piece from the end of the tutorial into the journey, through black.

> **Switch:** `SceneHandoff_NEW.handoffEnabled`. Off = the tutorial ends and returns to its title card, as before.

## Sequence

| Step | What happens |
|---|---|
| 1 | Outro's title is fully up → journey starts loading in the background. Outro is told not to return to attract |
| 2 | Title holds (`titleHoldSeconds`, 3 s), or longer if the load isn't ready |
| 3 | Fade to black over the title (1 s) |
| 4 | Journey scene activates under black |
| 5 | Black holds (`settleSeconds`, 0.75 s) while cameras settle |
| 6 | Fade up onto the journey (2.5 s) |

The journey starts in the Quasar layer **facing forward**. `LP_Quasar.useLookBackSequence` is now `0`, so there is no look back at the quasar.

## Setup

1. **Tutorial scene:** create an empty **root** GameObject named `SceneHandoff` and add `SceneHandoff_NEW`. It finds the outro and subscribes by itself; no event wiring needed. It must be a root object, because it moves itself to `DontDestroyOnLoad`.
2. **Build Settings:** add `Assets/_Core_NEW/Tutorial.unity` (index 0) and `Assets/_Core_NEW/Gameplay_Scene_Ana.unity`.
   - In the Editor the handoff works without this, and logs a warning.
   - **In a build it will not.** It logs an error and stays in the tutorial.

## The journey's first shot — `JourneyIntroCamera_NEW`

The tutorial ends pulled up and back (wide, high, light small in frame). The journey opens on **the same framing and pushes in** to its normal orbit, so the black reads as one continuous move: out, then back in.

- **Setup:** add `JourneyIntroCamera_NEW` to any object in `Gameplay_Scene_Ana`. It finds the player and the Cinemachine brain itself.
- **How:** it creates a temporary virtual camera at the tutorial's end framing, scaled to the journey's world. On the reveal it drops out and the brain blends to the zone camera; that blend is the push-in. Then it deletes itself. `CameraDirector_NEW` is untouched.
- **Switch:** `introEnabled`.

| Setting | Default | Why |
|---|---|---|
| `startDistance` / `startHeight` | 3.4 / 3.0 | Outro ends at 34 / 30, and the tutorial world is ~10× the journey's scale |
| `startFieldOfView` | 72 | The outro's end FOV. The journey runs at 40, so most of the push-in is this narrowing |
| `holdSeconds` | 0.75 | Time on the wide shot after the reveal, so it registers as the tutorial's last frame |
| `pushSeconds` | 3.5 | The push-in |

**Tuning the seam:** these defaults are derived, not judged by eye. Compare the last frame before black with the first frame after; they should read as the same shot. Adjust `startDistance`, `startHeight` and `startFieldOfView` until they do.

## Not yet built

- **Journey → tutorial.** After the Earth ending, nothing returns to the tutorial's attract screen yet. `EarthCutscene_NEW` has no completion event to hook.
- **Journey idle timeout.** Deliberately not added. The journey needs no input, so "no input for 90 s" is normal viewing there, not an absent visitor.
