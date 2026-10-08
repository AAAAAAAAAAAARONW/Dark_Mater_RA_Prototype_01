#ifndef JOURNEY_MILKY_WAY_DENSITY_INCLUDED
#define JOURNEY_MILKY_WAY_DENSITY_INCLUDED
sampler2D _Noise;
float4 _ArmColor, _CoreColor, _HIIColor;
float _Brightness, _Pitch, _ArmCount, _ArmWidth, _Dust, _Phase, _Thickness;

            float lane(float phi,float r,float width)
            {
                float count=clamp(floor(_ArmCount+0.5),2,6);
                float d=abs(atan2(sin(phi*count),cos(phi*count)))/count*r;
                return exp(-d*d/max(width*width,0.00001));
            }
            void galaxy(float3 p,out float3 emission,out float extinction)
            {
                float cs=cos(_Phase), sn=sin(_Phase);
                float2 q=float2(cs*p.x+sn*p.z,-sn*p.x+cs*p.z)*2.1;
                float r=length(q);
                float edge=1-smoothstep(0.78,1.0,r);
                float phi=atan2(q.y,q.x)-_Pitch*log(max(r,0.22)/0.22);
                float4 gas=tex2Dlod(_Noise,float4(q*0.72+float2(p.y*2,0),0,0));
                float4 fine=tex2Dlod(_Noise,float4(q*3.3+float2(0.27,p.y*3),0,0));
                float4 micro=tex2Dlod(_Noise,float4(q*19.7+float2(p.y*1.2,0.41),0,0));
                // Keep noise subordinate to the continuous spiral skeleton.
                float width=_ArmWidth*(0.65+0.65*r);
                float bent=phi+(gas.r-0.5)*0.9+(fine.r-0.5)*0.16;
                float arm=lane(bent,r,width);
                float spur=lane(phi+0.7+(gas.g-0.5)*0.45,r,width*0.50)
                    *smoothstep(0.35,0.5,r)*(1-smoothstep(0.7,0.96,r))*0.09;
                float shoulder=lane(bent,r,width*2.5)*0.20;
                float arms=(arm+spur+shoulder)*smoothstep(0.16,0.29,r)*edge;
                float cloud=saturate((gas.b*0.25+fine.r*0.5+micro.a*0.25-0.27)*2.7);
                float clumps=0.03+2.7*cloud*cloud;
                clumps*=lerp(0.35,1.5,smoothstep(0.22,0.75,micro.b));
                float z=p.y/_Thickness;
                float discHeight=exp(-z*z*0.5);
                float bar=exp(-pow(length(q/float2(0.25,0.065)),1.6)*1.6)
                    *exp(-pow(p.y/0.018,2));
                float bulge=exp(-r*r/0.0075-p.y*p.y/0.0005);
                float haze=exp(-r*1.8)*0.10*edge*exp(-z*z*0.10);
                float starLight=arms*clumps*discHeight;
                float3 armTint=lerp(float3(0.78,0.81,0.87),_ArmColor.rgb,smoothstep(0.18,0.8,r));
                float hii=smoothstep(0.58,0.73,micro.g)*smoothstep(0.50,0.66,fine.g)
                    *arm*edge*smoothstep(0.3,0.5,r)*discHeight;
                float dustLane=lane(bent+0.14+(gas.g-0.5)*0.26,r,width*0.40)
                    +lane(bent-0.08,r,width*0.16)*0.55
                    +lane(bent+0.08,r,width*1.5)*smoothstep(0.40,0.7,fine.r)*0.45;
                float filaments=lerp(0.1,2.0,smoothstep(0.23,0.76,fine.b*0.5+micro.r*0.5));
                float dust=dustLane*filaments*edge*smoothstep(0.17,0.32,r)
                    *exp(-z*z*2.0)*_Dust;
                emission=(armTint*starLight*0.8 + _CoreColor.rgb*(bar*1.2+bulge*0.9)*(0.7+fine.g*0.6)
                    +_HIIColor.rgb*hii*1.25 + float3(0.34,0.40,0.54)*haze)*_Brightness;
                // Dust attenuates both the near-side starlight and whatever is behind it.
                // Blue is absorbed more strongly, leaving warm brown dust filaments.
                emission*=exp(-dust*float3(1.3,1.9,2.8));
                extinction=starLight*0.32+bar*0.85+bulge*0.6+haze*0.25+dust*2.4;
            }

#endif
