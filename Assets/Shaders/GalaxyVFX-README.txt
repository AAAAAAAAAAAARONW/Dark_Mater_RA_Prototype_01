VFX/Galaxy Shader - 3D Volume (Raymarching)
============================================

This shader renders the galaxy as a 3D volume: it raymarches from the camera through a box and integrates density along each ray. So from any angle you get correct 3D: face view shows the spiral, edge view shows a bright band (integrated light through the disc).

1. Create material: Shader -> VFXGalaxy
2. Use on a CUBE mesh:
   - Create -> 3D Object -> Cube
   - Scale as needed (e.g. 1,1,1 or 2,2,2)
   - Set material "Volume Size" to match the cube extent in object space: 1 for default cube (-0.5 to 0.5), 2 if you scaled the cube by 2
3. Galaxy orientation in object space:
   - Disc plane = XY (spiral face in XY)
   - Thickness = Z (disc is thin along Z)
   - So: look along -Z to see the spiral face; look along X or Y to see the thin glowing edge
4. Rotate the cube in the scene to orient the galaxy (e.g. rotate so the spiral faces the camera).

Volume parameters (Inspector):
- Volume Size: must match your cube's object-space extent (default cube = 1)
- Raymarch Steps: 16-80, higher = better quality, slower
- Step Size: max step length, smaller = finer but slower
- Density Scale: overall brightness/density of the volume
- Disc Thickness: how fast density falls off along Z (smaller = thinner disc)

All other parameters (Core, Disk, Spiral, HII, Dust, Grain, Stars, Edge, Animation) work as before. The galaxy is sampled in 3D at each raymarch step, so the result is fully 3D from any view.
