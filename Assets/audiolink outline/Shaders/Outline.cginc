#include "UnityCG.cginc"
#include "Lighting.cginc"
#include "SurfaceAlpha.cginc"
Texture2D<float4> _AudioTexture;
float _ALOOn, _ALOBrightness, _ALOWidth, _ALOHue, _ALOBreath;
float _ALOWaveWidth, _ALOBreathLength, _ALOBreathFrequency;
float _ALOOffsetHorizontal, _ALOOffsetVertical;
float _ALOMargin, _ALOHeight, _ALOBottom, _ALOBaseScale, _ALOAvatarScale;
float _ALOLightMinLimit, _ALOLightMaxLimit, _ALOMonochromeLighting, _ALOAsUnlit, _ALOVertexLightStrength;
float _ALOTestClock, _ALOTime, _ALODebugHeight;
struct ALOInput { float4 vertex:POSITION; float3 normal:NORMAL;float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
struct ALOOutput { float4 position:SV_POSITION; float activity:TEXCOORD0; float height:TEXCOORD1; float3 worldNormal:TEXCOORD2; float2 uv:TEXCOORD3; float3 lightColor:TEXCOORD4; UNITY_VERTEX_OUTPUT_STEREO };
float ALOClock() { return _ALOTestClock>0.5 ? _ALOTime : _Time.y; }
float ALOScale() { return max(_ALOBaseScale*_ALOAvatarScale,0.0001); }
float2 ALOOffset()
{
    float2 offset=clamp(float2(_ALOOffsetHorizontal,_ALOOffsetVertical),-1,1);
    return offset/max(1,length(offset))*max(_ALOHeight,0.0001)*0.05;
}
float ALOBottomWS() { return mul(unity_ObjectToWorld,float4(0,0,0,1)).y+_ALOBottom*ALOScale(); }
// Height is normalized to the avatar: total breathing band = 0.25 * height.
#define ALO_BREATH_PHASE_RADIUS (0.125*saturate(_ALOBreathLength))
bool ALOBreathIntersects(float low,float high)
{
    if(_ALOBreathLength<=0)return false;
    float phase=0.06-ALOClock()*0.25*(2*saturate(_ALOBreathFrequency));
    return ceil(phase+low*0.5-ALO_BREATH_PHASE_RADIUS-0.00001)<=floor(phase+high*0.5+ALO_BREATH_PHASE_RADIUS+0.00001);
}
float ALOWave(float height)
{
    float clock=ALOClock();
    if(_ALOBreath>0.5)
    {
        // Four-second interval, two-second ascent. The waveform peak is 0.5.
        // Smooth breathing band spans one quarter of the normalized body height.
        if(_ALOBreathLength<=0)return 0;
        float phase=frac(0.06+height*0.5-clock*0.25*(2*saturate(_ALOBreathFrequency)));
        float spike=saturate(1-min(phase,1-phase)/max(ALO_BREATH_PHASE_RADIUS,0.000001));
        return 0.5*spike*spike*(3-2*spike);
    }
    uint width,rows;_AudioTexture.GetDimensions(width,rows);
    if(width<128||rows<4)return 0;
    float h=frac((0.5+height*255)/256+clock/12);
    float shape=pow(saturate(1-abs(sin(UNITY_PI*(6*h+0.5)))),1.3);
    shape=(shape+1.4*pow(shape,12))/2.4;
    float response=0;
    [unroll]for(int band=0;band<4;band++)
    {
        float warped=frac(h+0.008*sin(2*UNITY_PI*(0.5+h)));
        float d=abs(warped-(band+0.5)/4);d=min(d,1-d);
        float envelope=0.14+0.86*exp(-0.5*d*d/(0.13*0.13));
        float delay=0.5+2*(0.5+0.5*sin(2*UNITY_PI*(0.5-h)+band*1.71));
        int column=(int)floor(delay);
        float audio=lerp(_AudioTexture.Load(int3(column,band,0)).r,_AudioTexture.Load(int3(column+1,band,0)).r,frac(delay));
        response=max(response,saturate(audio)*envelope);
    }
    return response*shape;
}
// Toon light magnitude follows the source material instead of dark rim normals.
float3 ALOSceneLighting(float3 world,float3 normal)
{
    float luminance=dot(_LightColor0.rgb,float3(0.22,0.707,0.071));
    float3 direction=_WorldSpaceLightPos0.xyz*luminance+(unity_SHAr.xyz+unity_SHAg.xyz+unity_SHAb.xyz)/3;
    direction=dot(direction,direction)<0.000001?0:normalize(direction);
    float3 light=max(0,_LightColor0.rgb)+max(0,ShadeSH9(float4(direction*(2.0/3),1)));
    #ifdef VERTEXLIGHT_ON
    light+=Shade4PointLights(unity_4LightPosX0,unity_4LightPosY0,unity_4LightPosZ0,
        unity_LightColor[0].rgb,unity_LightColor[1].rgb,unity_LightColor[2].rgb,unity_LightColor[3].rgb,
        unity_4LightAtten0,world,normal)*_ALOVertexLightStrength;
    #endif
    light=clamp(light,_ALOLightMinLimit,max(_ALOLightMinLimit,_ALOLightMaxLimit));
    light=lerp(light,dot(light,float3(1.0/3,1.0/3,1.0/3)).xxx,saturate(_ALOMonochromeLighting));
    return lerp(light,1,saturate(_ALOAsUnlit));
}
ALOOutput ALOVertex(ALOInput v)
{
    UNITY_SETUP_INSTANCE_ID(v);
    ALOOutput o;UNITY_INITIALIZE_OUTPUT(ALOOutput,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;
    float height=(world.y-ALOBottomWS())/max(_ALOHeight*ALOScale(),0.0001);
    float thickness = 0.0015 * 3.0 * saturate(_ALOWidth) + 0.04 * 3.0 * 0.5 * saturate(_ALOWaveWidth) * ALOWave(height);
    float extrusion=thickness+_ALOMargin;
    if(_ALODebugHeight>0.5)extrusion=0;
    // Extrude in the viewing plane, retaining the actual surface's camera depth.
    // LEqual in the pass then gives the same wall occlusion as the character.
    float3 normalView=mul((float3x3)UNITY_MATRIX_V,UnityObjectToWorldNormal(v.normal));
    float2 direction=normalView.xy;
    if(dot(direction,direction)<0.000001)
    {
        float3 radial=world-mul(unity_ObjectToWorld,float4(0,0,0,1)).xyz;radial.y=0;
        direction=mul((float3x3)UNITY_MATRIX_V,radial).xy;
    }
    direction=normalize(direction+float2(0.00000001,0));
    float4 view=mul(UNITY_MATRIX_V,float4(world,1));
    view.xy+=direction*extrusion*ALOScale();
    float2 offset=ALOOffset();
    view.xy+=offset*ALOScale();
    o.uv=v.uv;
    o.worldNormal=UnityObjectToWorldNormal(v.normal);
    o.lightColor=ALOSceneLighting(world,normalize(o.worldNormal));
    o.position=mul(UNITY_MATRIX_P,view);o.activity=max(thickness,length(offset));o.height=height;
    if(_ALOOn<0.5)o.position=float4(0,0,0,1);
    return o;
}
float4 ALOFragment(ALOOutput i):SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    clip(_ALOOn-0.5);ALOClipAlpha(i.uv);
    if(_ALODebugHeight>0.5)return float4(saturate(i.height).xxx,1);
    clip(i.activity-0.000001);
    // One third of the hue ring spans the actual deformed body height.
    // Subtract time so each color travels upward; the palette repeats every 6s.
    float hue=_ALOHue>=0.99999?frac(i.height/3-ALOClock()/6):_ALOHue;
    float3 rgb=saturate(abs(frac(hue+float3(0,2.0/3,1.0/3))*6-3)-1);
    if(_ALOHue<=0)rgb=1;
    // Keep the menu parameter normalized, but output unclamped HDR radiance.
    // Cubic response gives 0.25 at 25%, 2 at 50%, and 16 at 100%.
    float brightness=saturate(_ALOBrightness);
    float intensity=16*brightness*brightness*brightness;
    float3 ambient=max(0,i.lightColor)*(1-saturate(brightness*4));
    if(_ALOHue>=0.99999)
    {
        // Multiplying every channel by 16 clips almost the entire hue transition
        // to secondary-color plateaus on LDR/strongly exposed cameras. Keep the
        // intermediate channels readable; concentrate HDR energy near channel peaks.
        float3 peak=rgb*rgb;peak=peak*peak;peak=peak*peak;
        peak=peak*peak*peak; // rgb^24; continuous at hue-sector boundaries.
        return float4(rgb*(ambient+min(intensity,1))+peak*max(intensity-1,0),1);
    }
    return float4(rgb*(ambient+intensity),1);
}
