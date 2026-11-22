// The Sun's one colour ramp (SunSurface_NEW, SunGlow_NEW): a brightness in, a colour out, as
// NASA colours SDO's 304 Å pictures — the data is one channel, and the colour comes from
// how bright it is, the same anywhere on the disc or off it.
//
// Measured on 21 Nov from NASA's picture, by brightness (the red channel, which is the
// brightest everywhere):
//
//   brightness    0.05     0.33     0.51     0.72     0.88     0.97     1.0
//   colour        #0d0101  #550301  #830e03  #b72304  #e14206  #f7830a  #fda516
//   green / red   0.1      0.03     0.11     0.19     0.29     0.53     0.65
//   blue / red    ~0.02 to 0.09 throughout
//
// So red is the brightness; green comes in slowly, then fast at the top; blue hardly at all.
// It never goes white: the brightest is amber. Past 1 (active regions, flares, for the bloom)
// it goes on along that amber, so the bloom spreads amber rather than white.

#ifndef SUN_RAMP_NEW_INCLUDED
#define SUN_RAMP_NEW_INCLUDED

float3 SunRamp(float brightness)
{
    float i = saturate(brightness);
    float green = 0.03 + 0.26 * smoothstep(0.3, 0.9, i) + 0.36 * smoothstep(0.85, 1.0, i);
    float blue = 0.02 + 0.07 * smoothstep(0.9, 1.0, i);
    float3 c = float3(1.0, green, blue) * i;
    c += float3(1.0, 0.68, 0.12) * max(brightness - 1.0, 0.0);
    return c;
}

#endif
