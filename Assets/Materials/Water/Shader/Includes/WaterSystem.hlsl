#ifndef WATER_SYSTEM_INCLUDED
#define WATER_SYSTEM_INCLUDED
#if !defined(SHADERGRAPH_PREVIEW)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#endif

// All functions are shared by the plane and closed-volume Shader Graph masters.
float WaterHash(float2 p) { return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453); }
float WaterNoise(float2 p)
{
    float2 i=floor(p), f=frac(p); f=f*f*(3.0-2.0*f);
    return lerp(lerp(WaterHash(i),WaterHash(i+float2(1,0)),f.x),lerp(WaterHash(i+float2(0,1)),WaterHash(i+1),f.x),f.y);
}
float WaterFbm(float2 p) { return WaterNoise(p)*.65+WaterNoise(p*2.07+17.3)*.25+WaterNoise(p*4.13-5.2)*.1; }
// Packed, fine bubbles: bright foam body with sparse rounded pinholes.
// Cell boundaries are deliberately absent; F2-F1 edges read as cracked paint.
float WaterFoamGrain(float2 p)
{
    p*=3.75;
    float2 base=floor(p), local=frac(p);float first=8;
    [unroll]for(int y=-1;y<=1;y++)[unroll]for(int x=-1;x<=1;x++)
    {
        float2 cell=float2(x,y);float2 jitter=float2(WaterHash(base+cell),WaterHash(base+cell+31.7));
        float2 d=cell+jitter-local;float distance=dot(d,d);
        first=min(first,distance);
    }
    float aa=max(.015,fwidth(first));
    float holes=1-smoothstep(.018-aa,.075+aa,first);
    return 1-holes*.16;
}

void WaterCrestDetail_float(float Crest, float2 UV, float Time, float Scale, float Strength, float Style, float Enabled, float UpFacing, float Amount, out float Mask)
{
    Mask=0;
    UNITY_BRANCH if(Enabled<.5||Crest<.015) return;
    float2 p=UV*max(Scale,1.0)+float2(Time*.17,-Time*.09);
    float detail=WaterFbm(p*1.6);
    float fine=WaterNoise(p*5.3);
    float edge=smoothstep(.06+(.42-detail*.34)*Strength,.70,Crest);
    float aa=max(.035,fwidth(fine)*.6);
    float holes=lerp(.78,1.0,smoothstep(.2-aa,.2+aa,fine));
    float grain=WaterFoamGrain(p*4.0);
    float realistic=edge*holes*lerp(1,grain,Strength)*lerp(.62,1.0,detail);
    float graphic=smoothstep(.36,.43,Crest)*smoothstep(.19,.24,detail)*lerp(1,grain,.35);
    Mask=saturate(lerp(realistic,graphic,Style)*Enabled*UpFacing*Amount);
}

void WaterPlaneDepth_float(float3 PositionWS, float SceneEyeDepth, float3 Shallow, float3 Deep, float Density, float Enabled, float Style, float OriginalAlpha, float Opacity, out float3 Color, out float Alpha, out float Thickness)
{
    #if defined(SHADERGRAPH_PREVIEW)
        Thickness=1.5;
    #else
        float waterEye=-TransformWorldToView(PositionWS).z;
        Thickness=max(0,SceneEyeDepth-waterEye);
    #endif
    Thickness=min(Thickness,40.0);
    float absorption=1.0-exp(-Thickness*max(Density,.001));
    float bands=floor(absorption*4.0+.5)/4.0;
    float t=lerp(absorption,bands,Style*.9);
    Color=lerp(Shallow,lerp(Shallow,Deep,t),Enabled);
    Alpha=lerp(OriginalAlpha,max(OriginalAlpha,lerp(.02,.97,absorption)*Opacity),Enabled);
}

#if !defined(SHADERGRAPH_PREVIEW)
    TEXTURE2D_X(_WaterVolumeBackDepth);
    SAMPLER(sampler_WaterVolumeBackDepth);
#endif
void WaterVolumeDepth_float(float3 PositionWS, float4 ScreenUV, float3 Shallow, float3 Deep, float Density, float Enabled, float Style, float OriginalAlpha, float FallbackThickness, float Opacity, out float3 Color, out float Alpha, out float Thickness)
{
    Thickness=FallbackThickness;
    #if !defined(SHADERGRAPH_PREVIEW)
        float waterEye=-TransformWorldToView(PositionWS).z;
        float backEye=SAMPLE_TEXTURE2D_X(_WaterVolumeBackDepth,sampler_WaterVolumeBackDepth,ScreenUV.xy).r;
        if(backEye>waterEye+.001) Thickness=backEye-waterEye;
    #endif
    float absorption=1.0-exp(-max(Thickness,0)*max(Density,.001));
    float bands=floor(absorption*4.0+.5)/4.0;
    Color=lerp(Shallow,lerp(Shallow,Deep,lerp(absorption,bands,Style*.9)),Enabled);
    Alpha=lerp(OriginalAlpha,max(OriginalAlpha,lerp(.02,.96,absorption)*Opacity),Enabled);
}

// World-space vertical separation makes shore/contact widths stable as the
// camera changes angle. Absorption above still uses optical ray thickness.
void WaterIntersectionDepth_float(float3 PositionWS, float4 ScreenUV, float RawDepth, out float Depth)
{
    Depth=2;
    #if !defined(SHADERGRAPH_PREVIEW)
        #if !UNITY_REVERSED_Z
            RawDepth=lerp(UNITY_NEAR_CLIP_VALUE,1,RawDepth);
        #endif
        float3 scenePosition=ComputeWorldSpacePosition(ScreenUV.xy,RawDepth,UNITY_MATRIX_I_VP);
        Depth=clamp(PositionWS.y-scenePosition.y,0,40);
    #endif
}
void WaterSurfaceFoam_float(float3 PositionWS, float Time, float Scale, float Speed, float2 Direction, float Amount, float Stretch, float Intensity, float GlobalIntensity, float Style, float Enabled, float UpFacing, out float Mask)
{
    Mask=0;UNITY_BRANCH if(Enabled<.5||Amount<.001||Intensity<.001)return;
    float2 direction=normalize(Direction+float2(.0001,.0002));
    float2 p=float2(dot(PositionWS.xz,direction),dot(PositionWS.xz,float2(-direction.y,direction.x)));
    p=p*max(Scale,.01)*float2(1/max(Stretch,.1),1)+float2(-Time*Speed,Time*Speed*.13);
    float warp=WaterFbm(p*.6+Time*.007);
    float patches=WaterFbm(p+float2(warp,-warp)*1.1);
    float threshold=lerp(.86,.44,saturate(Amount));
    float coverage=smoothstep(threshold,threshold+lerp(.11,.035,Style),patches);
    float detail=WaterFbm(p*3.7+float2(Time*.009,-Time*.013));
    float grain=WaterFoamGrain(p*14.0);
    float irregular=smoothstep(.22,.61,detail);
    float realistic=coverage*irregular*grain;
    float graphic=smoothstep(.28,.36,coverage)*smoothstep(.25,.33,detail)*lerp(1,grain,.35);
    Mask=saturate(lerp(realistic,graphic,Style)*Intensity*GlobalIntensity*UpFacing);
}
void WaterContactFoam_float(float3 PositionWS, float Depth, float Time, float Width, float Scale, float Speed, float Intensity, float GlobalIntensity, float Style, float Enabled, float UpFacing, out float Mask)
{
    Mask=0;UNITY_BRANCH if(Enabled<.5||Depth>Width*1.4)return;
    float2 p=PositionWS.xz*Scale+float2(Time*Speed*.07,-Time*Speed*.045);
    float noise=WaterFbm(p);
    float width=max(.001,Width*(.78+.22*sin(Time*Speed+noise*5)));
    float edge=1-smoothstep(width*.15,width,Depth);
    float breakup=lerp(.32,1,smoothstep(.2,.68,noise));
    float grain=WaterFoamGrain(p*8.0);
    float realistic=edge*breakup*grain;
    float graphic=smoothstep(.24,.34,edge*breakup)*lerp(1,grain,.35);
    Mask=saturate(lerp(realistic,graphic,Style)*Intensity*GlobalIntensity*UpFacing);
}
void WaterCoastalFoam_float(float3 PositionWS, float Depth, float Time, float Width, float Speed, float Frequency, float Scale, float Intensity, float GlobalIntensity, float Style, float Enabled, float UpFacing, out float Mask)
{
    Mask=0;UNITY_BRANCH if(Enabled<.5||Depth>Width)return;
    float2 p=PositionWS.xz*Scale;
    float warp=WaterFbm(p+Time*.016);
    // Increasing phase moves each breaker toward smaller depth (toward shore).
    float phase=Depth*Frequency+Time*Speed+warp*2.6;
    float wave=sin(phase)*.5+.5;
    float front=smoothstep(lerp(.88,.78,Style),.995,wave);
    float wake=pow(saturate(sin(phase-.65)*.5+.5),5)*.09;
    float zone=(1-smoothstep(Width*.55,max(Width,.001),Depth))*smoothstep(.015,.10,Depth);
    float breakup=smoothstep(.18,.68,WaterFbm(p*3.3-float2(Time*.035,Time*.018)));
    float grain=WaterFoamGrain(p*16.0);
    float realistic=max(front,wake)*zone*breakup*grain;
    float graphic=smoothstep(.20,.32,max(front,wake)*zone*breakup)*lerp(1,grain,.35);
    Mask=saturate(lerp(realistic,graphic,Style)*Intensity*GlobalIntensity*UpFacing);
}

// Foam - Bubble Normal
float2 WaterBubbleSlope(float2 p)
{
    float2 base=floor(p),local=frac(p),slope=0;
    [unroll]for(int y=-1;y<=1;y++)[unroll]for(int x=-1;x<=1;x++)
    {
        float2 cell=float2(x,y);
        float2 d=local-cell-float2(WaterHash(base+cell),WaterHash(base+cell+31.7));
        float radius=.42+.15*WaterHash(base+cell+13.4);
        float q=dot(d,d)/(radius*radius);
        slope+=-6*d/(radius*radius)*exp(-3*q);
    }
    float footprint=max(length(ddx(p)),length(ddy(p)));
    return slope*(1-smoothstep(.3,1.0,footprint));
}
void WaterSurfaceDetail_float(float3 BaseNormal, float2 UV, float Time, float Foam, float Style, float NormalStrength, float OriginalSmoothness, float FoamBumpStrength, float FoamBubbleScale, out float3 Normal, out float Smoothness)
{
    float strength=NormalStrength*lerp(1,.3,Style);
    float3 normal=normalize(float3(BaseNormal.xy*strength,max(.2,BaseNormal.z)));
    float2 bubbleUV=UV*max(FoamBubbleScale,1)+float2(Time*.17,-Time*.11);
    float2 bubbles=WaterBubbleSlope(bubbleUV)*FoamBumpStrength*pow(saturate(1-Style),1.3);
    float body=smoothstep(.025,.45,Foam);
    float2 legacyBubbles=float2(sin(UV.x*523+Time*.9)*cos(UV.y*417-Time*.7),cos(UV.y*557-Time*.8)*sin(UV.x*389+Time*.6))*.085;
    Normal=FoamBumpStrength>.001?normalize(float3(normal.xy*lerp(1,.35,body)-bubbles*body,normal.z)):normalize(lerp(normal,normalize(float3(legacyBubbles,1)),Foam));
    Smoothness=lerp(lerp(.84,.97,saturate(OriginalSmoothness)),.86,Style);
    float variance=max(dot(ddx(normal),ddx(normal)),dot(ddy(normal),ddy(normal)));
    Smoothness=clamp(Smoothness-variance*.45,.65,.95);
}
void WaterLayeredNormals_float(UnityTexture2D WaterNormal, float2 Wave1UV, float2 Wave2UV, out float3 Normal)
{
    float3 a=UnpackNormal(SAMPLE_TEXTURE2D(WaterNormal.tex,WaterNormal.samplerstate,Wave1UV));
    float3 b=UnpackNormal(SAMPLE_TEXTURE2D(WaterNormal.tex,WaterNormal.samplerstate,Wave2UV));
    Normal=normalize(float3(a.xy+b.xy,max(.15,a.z*b.z)));
}
void WaterStyle_float(float3 ColorIn, float3 EmissionIn, float SmoothnessIn, float SpecularIn, float3 NormalWS, float3 PositionWS, float Style, float AlphaIn, out float3 Color, out float3 Emission, out float Smoothness, out float Specular, out float Alpha)
{
    float s=saturate(Style);
    float light=dot(normalize(NormalWS),normalize(float3(-.5,.75,-.4)))*.5+.5;
    float bands=floor(light*3.0+.5)/3.0;
    float facing=1;
    #if !defined(SHADERGRAPH_PREVIEW)
        facing=saturate(dot(normalize(NormalWS),normalize(GetCameraPositionWS()-PositionWS)));
    #endif
    float rim=smoothstep(.55,.7,1-facing);
    float3 graphic=ColorIn*(.6+bands*.55)+float3(.035,.16,.21)*rim;
    Color=ColorIn*(1-s*.95);
    Emission=lerp(EmissionIn,graphic,s*.95);
    Smoothness=lerp(SmoothnessIn,.6,s);
    Specular=SpecularIn*(1-s*.95);
    Alpha=lerp(AlphaIn,1,s*.95);
}

// Refraction offsets are clamped to the water/background depth separation, reducing
// foreground silhouettes leaking into the refracted image.
void WaterRefractionUV_float(float4 ScreenUV, float3 Normal, float Thickness, float Strength, float Enabled, out float4 UV)
{
    UV=ScreenUV;
    UV.xy=saturate(UV.xy+Normal.xy*Strength*saturate(Thickness*.7)*Enabled);
}
void WaterRefractionBlend_float(float3 Tint, float AlphaIn, float3 SceneColor, float3 Transmission, float4 EmissionIntensity, float Enabled, out float3 Color, out float3 Emission, out float Alpha)
{
    float a=saturate(AlphaIn);
    Color=Tint*lerp(1.0,a,Enabled);
    // Coloured transmission removes warm seabed light instead of letting the
    // refracted sand overwhelm the water's scattering colour.
    Emission=SceneColor*(1-a)*lerp(1,saturate(Transmission),a)*Enabled+Transmission*EmissionIntensity.xyz;
    Alpha=lerp(a,1.0,Enabled);
}

float WaterCausticFrame(UnityTexture2D Atlas,float2 uv,float frame)
{
    float2 tile=float2(fmod(frame,8),3-floor(frame/8));
    float2 atlasUV=(tile*264+4.5+frac(uv)*256)/float2(2112,1056);
    return SAMPLE_TEXTURE2D_LOD(Atlas.tex,Atlas.samplerstate,atlasUV,0).r;
}
void WaterCaustics_float(float3 SceneColor,float3 SurfaceWS,float4 ScreenUV,float RawDepth,float Time,float Scale,float Speed,float Strength,float FadeDepth,float Enabled,float IsVolume,UnityTexture2D Atlas,out float3 Color,out float Mask)
{
    Color=SceneColor;Mask=0;
    UNITY_BRANCH if(Enabled<.5||Strength<.001)return;
    #if !defined(SHADERGRAPH_PREVIEW)
        #if !UNITY_REVERSED_Z
            RawDepth=lerp(UNITY_NEAR_CLIP_VALUE,1,RawDepth);
        #endif
        float3 receiver=ComputeWorldSpacePosition(ScreenUV.xy,RawDepth,UNITY_MATRIX_I_VP);
        float waterEye=-TransformWorldToView(SurfaceWS).z;
        float receiverEye=-TransformWorldToView(receiver).z;
        float depth=SurfaceWS.y-receiver.y;
        float valid=step(waterEye+.025,receiverEye)*smoothstep(.025,.18,depth);
        if(IsVolume>.5)
        {
            float exitEye=SAMPLE_TEXTURE2D_X(_WaterVolumeBackDepth,sampler_WaterVolumeBackDepth,ScreenUV.xy).r;
            valid*=step(receiverEye,exitEye+.025);
        }
        float3 receiverNormal=normalize(cross(ddy(receiver),ddx(receiver))+float3(0,.00001,0));
        float orientation=saturate(abs(receiverNormal.y));
        float2 projected=(receiver.xz-receiver.y*float2(.35,.2))*max(Scale,.01);
        float t=Time*Speed;
        float frame=frac(t/8)*32;
        float focus=lerp(WaterCausticFrame(Atlas,projected,floor(frame)),WaterCausticFrame(Atlas,projected,fmod(floor(frame)+1,32)),frac(frame));
        float modulation=.65+.35*WaterFbm(projected*.7+float2(t*.07,-t*.09));
        float shadow=MainLightRealtimeShadow(TransformWorldToShadowCoord(receiver));
        Mask=focus*modulation*valid*orientation*exp(-max(depth,0)/max(FadeDepth,.05))*Strength*shadow;
        // Multiply the transmitted opaque colour; no white geometry or foam is
        // placed on the water surface, and dry ground receives no added light.
        Color=SceneColor*(1+float3(.82,1.0,.93)*Mask*3.2);
    #endif
}

void WaterPlaneSwell_float(float3 LegacyPosition, float Time, float Height, float Length, float Speed, out float3 Position, out float3 Normal)
{
    float k=6.2831853/max(Length,.1);
    float2 d1=normalize(float2(1,.35)),d2=normalize(float2(-.43,1));
    float a=dot(LegacyPosition.xz,d1)*k-Time*Speed;
    float b=dot(LegacyPosition.xz,d2)*k*1.63-Time*Speed*1.19;
    Position=LegacyPosition+float3(0,(sin(a)+sin(b)*.42)*Height,0);
    float2 slope=Height*k*(cos(a)*d1+cos(b)*.42*1.63*d2);
    Normal=normalize(float3(-slope.x,1,-slope.y));
}
void WaterVolumeSwell_float(float3 PositionOS, float3 NormalOS, float LegacyHeight, float Time, float Height, float Length, float Speed, out float3 Position, out float3 Normal)
{
    float k=6.2831853/max(Length,.1);
    float a=dot(PositionOS,float3(.83,.37,.54))*k-Time*Speed;
    float b=dot(PositionOS,float3(-.42,.72,.61))*k*1.6-Time*Speed*.8;
    // Position-only deformation remains continuous across hard normal/UV seams
    // of closed boxes. Normal displacement would pull those faces apart.
    float3 da=float3(.83,.37,.54)*k;
    float3 db=float3(-.42,.72,.61)*k*1.6;
    Position=PositionOS+Height*float3(sin(a)*.3,sin(b),cos(a)*.3);
    float3 dx=float3(1,0,0)+Height*float3(cos(a)*da.x*.3,cos(b)*db.x,-sin(a)*da.x*.3);
    float3 dy=float3(0,1,0)+Height*float3(cos(a)*da.y*.3,cos(b)*db.y,-sin(a)*da.y*.3);
    float3 dz=float3(0,0,1)+Height*float3(cos(a)*da.z*.3,cos(b)*db.z,-sin(a)*da.z*.3);
    Normal=normalize(cross(dy,dz)*NormalOS.x+cross(dz,dx)*NormalOS.y+cross(dx,dy)*NormalOS.z);
}
void WaterUpFacing_float(float3 NormalWS, float IsVolume, out float Mask) { Mask=lerp(1,smoothstep(.05,.6,NormalWS.y),IsVolume); }
void WaterVolumeAdvection_float(float3 PositionOS, float Time, out float3 Position) { Position=PositionOS+float3(Time*.013,Time*.007,-Time*.011); }
void WaterDebugView_float(float3 ColorIn, float3 EmissionIn, float AlphaIn, float SmoothnessIn, float SpecularIn, float Crest, float Surface, float Shoreline, float Coastal, float Thickness, float3 Normal, float View, float Caustics, out float3 Color, out float3 Emission, out float Alpha, out float Smoothness, out float Specular)
{
    Color=ColorIn;Emission=EmissionIn;Alpha=AlphaIn;Smoothness=SmoothnessIn;Specular=SpecularIn;
    if(View<.5)return;
    float3 debug=0;
    if(View<1.5)debug=Crest;
    else if(View<2.5)debug=Surface;
    else if(View<3.5)debug=Shoreline;
    else if(View<4.5)debug=Coastal;
    else if(View<5.5)debug=1-exp(-Thickness*.8);
    else if(View<6.5)debug=Normal*.5+.5;
    else if(View<7.5)debug=ColorIn;
    else if(View<8.5)debug=EmissionIn;
    else debug=Caustics;
    Color=0;Emission=debug;Alpha=1;Smoothness=0;Specular=0;
}
// Cached Shader Graph previews may retain the previous function signature.
void WaterDebug_float(float3 ColorIn,float3 EmissionIn,float AlphaIn,float SmoothnessIn,float SpecularIn,float Crest,float Surface,float Shoreline,float Coastal,float Thickness,float3 Normal,float View,out float3 Color,out float3 Emission,out float Alpha,out float Smoothness,out float Specular)
{
    WaterDebugView_float(ColorIn,EmissionIn,AlphaIn,SmoothnessIn,SpecularIn,Crest,Surface,Shoreline,Coastal,Thickness,Normal,View,0,Color,Emission,Alpha,Smoothness,Specular);
}
#endif
