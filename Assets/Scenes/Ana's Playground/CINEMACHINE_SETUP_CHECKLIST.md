# Cinemachine Setup Checklist for Unity 2019.4.41f2
## Scene: "Ana's Playground I"

This checklist provides exact Inspector field values for setting up Cinemachine cameras to work with the migrated `MacroMicroTransitionControllerTest` and `MicroToMacroTriggerSequenceTest` scripts.

---

## Prerequisites

- Cinemachine package installed (via Package Manager)
- Main Camera has `CinemachineBrain` component
- Three Virtual Cameras exist in scene:
  - `VCam_Macro`
  - `VCam_Micro`
  - `VCam_TopDown`
- Custom blend asset exists: `Assets/Scenes/Ana's Playground/Ana's Assets/Main Camera Blends.asset`

---

## 1. Main Camera (Cinemachine Brain)

**Component**: `CinemachineBrain` (should already exist)

### Inspector Settings:

| Field | Value | Notes |
|-------|-------|-------|
| **Default Blend** | | |
| └─ Style | `Ease In Out` | |
| └─ Time | `1.0` | Default blend time (overridden by custom blends) |
| **Custom Blends** | `Main Camera Blends` | Assign the existing blend asset |
| **Update Method** | `Smart Update` | |
| **Blend Update Method** | `Smart Update` | |
| **Default World Up** | `Y Up` | |
| **Show Debug Text** | `false` | (Optional, for debugging) |
| **Show Camera Frustum** | `false` | (Optional, for debugging) |
| **Show Camera Collider** | `false` | (Optional, for debugging) |

---

## 2. VCam_Macro (Normal Gameplay Camera)

**Purpose**: Default camera for macro/cosmic web view. Active when player is outside trigger volume.

### Inspector Settings:

#### General
| Field | Value | Notes |
|-------|-------|-------|
| **Priority** | `10` | Default active camera |
| **Follow** | `[Player Transform]` | Assign player GameObject or CameraRigRoot |
| **Look At** | `[Player Transform]` | Assign player GameObject or CameraAimPivot |

#### Body (CinemachineFramingTransposer)
| Field | Value | Notes |
|-------|-------|-------|
| **Camera Distance** | `4.0` | Match `DarkMatterPlayerController.orbitDistance` |
| **Camera Side** | `0.0` | Centered horizontally |
| **Camera Height** | `0.0` | At player level |
| **Dead Zone Width** | `0.0` | No dead zone for orbit camera |
| **Dead Zone Height** | `0.0` | |
| **Soft Zone Width** | `1.0` | Full soft zone |
| **Soft Zone Height** | `1.0` | |
| **Bias X** | `0.0` | |
| **Bias Y** | `0.0` | |
| **Tracked Object Offset** | `(0, 0, 0)` | |

#### Aim (CinemachineComposer or CinemachinePOV)
**Option A: Composer (if camera should always look at player)**
| Field | Value | Notes |
|-------|-------|-------|
| **Tracked Object Offset** | `(0, 0, 0)` | |
| **Lookahead Time** | `0.0` | |
| **Lookahead Smoothing** | `0.0` | |
| **Horizontal Damping** | `0.0` | |
| **Vertical Damping** | `0.0` | |
| **Screen Position** | `(0.5, 0.5)` | Centered |

**Option B: POV (if player input drives camera rotation)**
| Field | Value | Notes |
|-------|-------|-------|
| **Horizontal Axis** | | Connect to `DarkMatterPlayerController.cameraYaw` |
| └─ Value | `0` | |
| └─ Speed | `0` | (Input-driven) |
| **Vertical Axis** | | Connect to `DarkMatterPlayerController.cameraPitch` |
| └─ Value | `0` | |
| └─ Speed | `0` | (Input-driven) |
| **Recentering** | | |
| └─ Enabled | `false` | |

#### Lens
| Field | Value | Notes |
|-------|-------|-------|
| **Field of View** | `70` | Match `macroFovRange.x` from transition controller |
| **Orthographic** | `false` | |
| **Orthographic Size** | `5` | (Not used if Orthographic = false) |
| **Near Clip Plane** | `0.3` | |
| **Far Clip Plane** | `1000` | |
| **Dutch** | `0` | |

---

## 3. VCam_Micro (Micro/Galaxy View Camera)

**Purpose**: Camera for micro/galaxy view. Activated when player enters trigger volume.

### Inspector Settings:

#### General
| Field | Value | Notes |
|-------|-------|-------|
| **Priority** | `0` | Inactive by default (activated by script) |
| **Follow** | `[Same as VCam_Macro]` | Same target as VCam_Macro |
| **Look At** | `[Same as VCam_Macro]` | Same target as VCam_Macro |

#### Body (CinemachineFramingTransposer)
| Field | Value | Notes |
|-------|-------|-------|
| **Camera Distance** | `3.0` | `orbitDistance * enterZoomMultiplier` (4.0 * 0.75) |
| **Camera Side** | `0.0` | Same as VCam_Macro |
| **Camera Height** | `0.0` | Same as VCam_Macro |
| **Dead Zone Width** | `0.0` | Same as VCam_Macro |
| **Dead Zone Height** | `0.0` | Same as VCam_Macro |
| **Soft Zone Width** | `1.0` | Same as VCam_Macro |
| **Soft Zone Height** | `1.0` | Same as VCam_Macro |
| **Bias X** | `0.0` | Same as VCam_Macro |
| **Bias Y** | `0.0` | Same as VCam_Macro |
| **Tracked Object Offset** | `(0, 0, 0)` | Same as VCam_Macro |

#### Aim
**Same settings as VCam_Macro** (use same component type and settings)

#### Lens
| Field | Value | Notes |
|-------|-------|-------|
| **Field of View** | `58` | Match `galaxyFovRange.y` from transition controller |
| **Orthographic** | `false` | |
| **Orthographic Size** | `5` | (Not used) |
| **Near Clip Plane** | `0.3` | Same as VCam_Macro |
| **Far Clip Plane** | `1000` | Same as VCam_Macro |
| **Dutch** | `0` | Same as VCam_Macro |

---

## 4. VCam_TopDown (Look-Down Sequence Camera)

**Purpose**: Top-down camera for look-down sequences during trigger enter/exit.

### Inspector Settings:

#### General
| Field | Value | Notes |
|-------|-------|-------|
| **Priority** | `0` | Inactive by default (activated by script) |
| **Follow** | `[Same as VCam_Macro]` | Same target as VCam_Macro |
| **Look At** | `[Same as VCam_Macro]` | Same target as VCam_Macro |

#### Body (CinemachineFramingTransposer)
| Field | Value | Notes |
|-------|-------|-------|
| **Camera Distance** | `4.0` | Same as VCam_Macro (normal distance) |
| **Camera Side** | `0.0` | Same as VCam_Macro |
| **Camera Height** | `0.0` | Same as VCam_Macro |
| **Dead Zone Width** | `0.0` | Same as VCam_Macro |
| **Dead Zone Height** | `0.0` | Same as VCam_Macro |
| **Soft Zone Width** | `1.0` | Same as VCam_Macro |
| **Soft Zone Height** | `1.0` | Same as VCam_Macro |
| **Bias X** | `0.0` | Same as VCam_Macro |
| **Bias Y** | `0.0` | Same as VCam_Macro |
| **Tracked Object Offset** | `(0, 0, 0)` | Same as VCam_Macro |

#### Aim (CinemachineComposer)
**Use Composer for top-down view:**
| Field | Value | Notes |
|-------|-------|-------|
| **Tracked Object Offset** | `(0, 0, 0)` | |
| **Lookahead Time** | `0.0` | |
| **Lookahead Smoothing** | `0.0` | |
| **Horizontal Damping** | `0.0` | |
| **Vertical Damping** | `0.0` | |
| **Screen Position** | `(0.5, 0.5)` | Centered |

**Alternative: Use POV with fixed pitch:**
| Field | Value | Notes |
|-------|-------|-------|
| **Horizontal Axis** | | |
| └─ Value | `0` | |
| └─ Speed | `0` | |
| **Vertical Axis** | | |
| └─ Value | `85` | Match `enterTopDownPitchOffset` (85-90°) |
| └─ Speed | `0` | Fixed angle |
| **Recentering** | | |
| └─ Enabled | `false` | |

#### Lens
| Field | Value | Notes |
|-------|-------|-------|
| **Field of View** | `70` | Same as VCam_Macro (or adjust as needed) |
| **Orthographic** | `false` | |
| **Orthographic Size** | `5` | (Not used) |
| **Near Clip Plane** | `0.3` | Same as VCam_Macro |
| **Far Clip Plane** | `1000` | Same as VCam_Macro |
| **Dutch** | `0` | Same as VCam_Macro |

---

## 5. Custom Blends (Main Camera Blends.asset)

**Location**: `Assets/Scenes/Ana's Playground/Ana's Assets/Main Camera Blends.asset`

Update blend times to match sequence requirements from `MicroToMacroTriggerSequenceTest`:

### Blend Settings:

| From Camera | To Camera | Style | Time (seconds) | Notes |
|-------------|-----------|-------|---------------|-------|
| `VCam_Macro` | `VCam_TopDown` | `Ease In Out` | `1.5` | Match `enterLookDownDuration` |
| `VCam_TopDown` | `VCam_Micro` | `Ease In Out` | `1.2` | Match `lookUpBlendDuration` |
| `VCam_Micro` | `VCam_TopDown` | `Ease In Out` | `0.35` | Match `lookDownBlendDuration` |
| `VCam_TopDown` | `VCam_Macro` | `Ease In Out` | `1.2` | Match `lookUpBlendDuration` |

### How to Edit:

1. Select `Main Camera Blends.asset` in Project window
2. In Inspector, find the blend entry (e.g., "VCam_Macro → VCam_TopDown")
3. Set **Time** field to the value from the table above
4. Repeat for all four blend entries

---

## 6. Script References Setup

### MicroToMacroTriggerSequenceTest Component

On the trigger GameObject, assign the following references in Inspector:

| Field | Value | Notes |
|-------|-------|-------|
| **Cinemachine Brain** | `[Main Camera]` | Auto-finds if left empty, but assign explicitly |
| **Vcam Macro** | `[VCam_Macro GameObject]` | Drag VCam_Macro from Hierarchy |
| **Vcam Micro** | `[VCam_Micro GameObject]` | Drag VCam_Micro from Hierarchy |
| **Vcam Top Down** | `[VCam_TopDown GameObject]` | Drag VCam_TopDown from Hierarchy |
| **Transition Controller** | `[MacroMicroTransitionControllerTest]` | Usually on Main Camera |
| **Player Controller** | `[DarkMatterPlayerController]` | Usually on Player GameObject |
| **Flare Flash** | `[CameraFlareFlash]` | Usually on Main Camera |

---

## 7. Validation Steps

After setup, verify:

1. **Default State**: VCam_Macro should be active (Priority 10), others inactive (Priority 0)
2. **Enter Trigger**: 
   - VCam_Macro → VCam_TopDown (1.5s blend)
   - Visuals switch to Micro
   - VCam_TopDown → VCam_Micro (1.2s blend)
3. **Exit Trigger**:
   - VCam_Micro → VCam_TopDown (0.35s blend)
   - Visuals transition to Macro
   - VCam_TopDown → VCam_Macro (1.2s blend)
4. **Camera Follow**: All cameras should follow player transform
5. **Camera Look At**: All cameras should look at player (or aim pivot)
6. **FOV**: VCam_Macro = 70°, VCam_Micro = 58°, VCam_TopDown = 70°
7. **Distance**: VCam_Macro = 4.0, VCam_Micro = 3.0, VCam_TopDown = 4.0

---

## 8. Troubleshooting

### Camera not switching
- Check Virtual Camera priorities (Macro=10, others=0 by default)
- Verify CinemachineBrain is on Main Camera
- Check that blend times match script expectations

### Camera not following player
- Verify Follow target is assigned on all Virtual Cameras
- Check that player GameObject exists and is active

### Blend times don't match
- Update Custom Blends asset with correct times (see section 5)
- Verify blend asset is assigned to CinemachineBrain

### FOV not matching
- Check Lens settings on each Virtual Camera
- Verify `allowFovOverride` is `false` in MacroMicroTransitionControllerTest

### Zoom not working
- VCam_Micro should have Camera Distance = 3.0 (75% of Macro's 4.0)
- If using playerController orbit multiplier, verify it's still being called (though Cinemachine should handle this)

---

## Summary

**VCam_Macro** (Priority 10):
- Distance: 4.0, FOV: 70°, Active by default

**VCam_Micro** (Priority 0):
- Distance: 3.0, FOV: 58°, Activated on trigger enter

**VCam_TopDown** (Priority 0):
- Distance: 4.0, FOV: 70°, Used for look-down sequences

**Blend Times**:
- Macro→TopDown: 1.5s
- TopDown→Micro: 1.2s
- Micro→TopDown: 0.35s
- TopDown→Macro: 1.2s
