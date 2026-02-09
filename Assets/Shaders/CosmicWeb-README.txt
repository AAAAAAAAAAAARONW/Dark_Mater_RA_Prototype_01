CosmicWeb Volumetric Shader (3D)
================================

Usage:
------
1. Create a Material and set its Shader to "Custom/CosmicWeb".
2. Create a Cube (or large Quad) in the scene and assign this material.
3. Place the camera inside or near the volume. The shader raymarches from camera through the volume.

Recommended:
- Scale the cube to (4, 4, 4) or larger so the camera is inside the volume.
- Use a Transparent queue so it composites over the background.

Logic (purple nebula + yellow only on the network):
--------------------------------------------------
- Base = purple nebula (星云): void -> purple -> magenta along the filament network. Grain only on the network.
- Yellow = only on the ridge (脉络): vein color and star points are gated by an on-ridge mask, so they appear only along the web, not randomly in the void. Yellow veins = coherent streaks on the strands; yellow stars = additive bright dots on the network. Node/core glow stays at convergence points.
- Parameters: Star Point Brightness, Vein Amount, Vein Coherence make yellow obvious and tied to the network.
