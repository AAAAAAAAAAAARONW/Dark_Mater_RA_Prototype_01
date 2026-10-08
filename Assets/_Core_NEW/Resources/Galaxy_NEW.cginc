// Shared by the Milky Way's shaders (GalaxyVFX_NEW): the disc's warp, so the gas sheet, the
// glow of the old stars and every star bend the same way.
//
// The Milky Way's outer disc is warped: beyond about half the radius it bends up on one side
// and down on the other, an S seen edge-on. The line of nodes (where it does not bend) runs
// close to the Sun, so the Sun's side is flat and the bend is greatest a quarter turn away.

#ifndef GALAXY_NEW_INCLUDED
#define GALAXY_NEW_INCLUDED

float _Warp;

float Sq(float x) { return x * x; }

// Height of the disc's middle above the plane at p (the plane's x and z, radius 1 at the edge).
float GalaxyWarp(float2 p)
{
    float r = max(length(p), 1e-4);
    return _Warp * Sq(smoothstep(0.45, 1.05, r)) * (p.y / r);
}

#endif
