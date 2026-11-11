Shader "audiolink outline lite"
{
    Properties
    {
        [HideInInspector] _ALOMainTex ("Source Alpha",2D)="white" {}
        [HideInInspector] _ALOAlphaMask ("Alpha Mask",2D)="white" {}
        [HideInInspector] _ALOUseAlpha ("Use alpha",Float)=0
        [HideInInspector] _ALOColorAlpha ("Source opacity",Float)=1
        [HideInInspector] _ALOCutoff ("Alpha cutoff",Float)=0.5
        [HideInInspector] _ALOAlphaMode ("Alpha mode",Float)=0
        [HideInInspector] _ALOAlphaScale ("Alpha scale",Float)=1
        [HideInInspector] _ALOAlphaOffset ("Alpha offset",Float)=0

        [HideInInspector] _ALOLightMinLimit ("Source minimum light",Float)=0.05
        [HideInInspector] _ALOLightMaxLimit ("Source maximum light",Float)=1
        [HideInInspector] _ALOMonochromeLighting ("Source monochrome lighting",Float)=0
        [HideInInspector] _ALOAsUnlit ("Source unlit blend",Float)=0
        [HideInInspector] _ALOVertexLightStrength ("Source vertex lights",Float)=1
        _ALOOn ("Enabled",Float)=1
        [HideInInspector] _ALOStencil ("Stencil bit",Int)=64
        _ALOBrightness ("Brightness: zero = ambient, half = 2, full = 16",Range(0,1))=1
        _ALOWidth ("Width",Range(0,1))=1
        _ALOWaveWidth ("Wave peak width: full = half of 1.2.1",Range(0,1))=1
        _ALOHue ("Hue: zero white, one upward rainbow",Range(0,1))=0
        _ALOBreathLength ("Breathing band length: half = quarter height",Range(0,1))=0.5
        _ALOBreathFrequency ("Breathing frequency: half = original, full = double",Range(0,1))=0.5
        _ALOBreath ("Breathing",Float)=0
        _ALOOffsetHorizontal ("Silhouette horizontal",Range(-1,1))=0
        _ALOOffsetVertical ("Silhouette vertical",Range(-1,1))=0
        _ALOMargin ("Silhouette margin",Float)=0.0005
        _ALOHeight ("Standing height",Float)=1.3
        _ALOBottom ("Bottom",Float)=0
        _ALOBaseScale ("Authored scale",Float)=1
        _ALOAvatarScale ("VRChat ScaleFactor",Float)=1
        [HideInInspector] _ALOPreviewWave ("Editor snapshot wave",Float)=-1
        [HideInInspector] _ALOTestClock ("Test clock",Float)=0
        [HideInInspector] _ALOTime ("Test time",Float)=0
        [HideInInspector] _ALODebugHeight ("Test height",Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Overlay+998" "RenderType"="Transparent" "VRCFallback"="Hidden" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Cull Off ZWrite Off ZTest LEqual Blend One Zero ColorMask RGB
            Stencil { Ref [_ALOStencil] ReadMask [_ALOStencil] WriteMask [_ALOStencil] Comp NotEqual Pass Replace }
            CGPROGRAM
            #pragma target 5.0
            #pragma vertex Control
            #pragma hull Hull
            #pragma domain Domain
            #pragma fragment ALOFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fwdbase
            #include "Outline.cginc"
            struct CP { float4 vertex:INTERNALTESSPOS;float3 normal:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID };
            CP Control(ALOInput v){CP o;o.vertex=v.vertex;o.normal=v.normal;o.uv=v.uv;UNITY_TRANSFER_INSTANCE_ID(v,o);return o;}
            struct TF { float edge[3]:SV_TessFactor;float inside:SV_InsideTessFactor; };
            float EdgeFactor(float4 a,float4 b)
            {
                float3 wa=mul(unity_ObjectToWorld,a).xyz,wb=mul(unity_ObjectToWorld,b).xyz;
                float distanceToViewer=distance((wa+wb)*0.5,_WorldSpaceCameraPos)/ALOScale();
                if(distanceToViewer>8||_ALOOn<0.5)return 1;
                float lo=(min(wa.y,wb.y)-ALOBottomWS())/(_ALOHeight*ALOScale());
                float hi=(max(wa.y,wb.y)-ALOBottomWS())/(_ALOHeight*ALOScale());
                if(_ALOTestClock>0.5&&_ALOPreviewWave>=0)return distanceToViewer<5?2:1;
                bool crest;
                if(ALOUseBreathing())
                {
                    crest=ALOBreathIntersects(lo,hi);
                    if(!crest)return 1;
                }
                else
                {
                    uint w,h;_AudioTexture.GetDimensions(w,h);if(w<128||h<4)return 1;
                    float phase=frac(ALOClock()/12);
                    crest=ceil(6*(lo+phase-0.015)+0.5)<=floor(6*(hi+phase+0.015)+0.5);
                }
                return crest?(distanceToViewer<5?8:4):(distanceToViewer<5?2:1);
            }
            TF Factors(InputPatch<CP,3> p)
            {
                UNITY_SETUP_INSTANCE_ID(p[0]);TF f;
                f.edge[0]=0;f.edge[1]=0;f.edge[2]=0;f.inside=0;
                // Zero tessellation factors discard an invisible patch before
                // domain shading and pixel work. Visible factors stay unchanged.
                if(_ALOOn<0.5)return f;
                if(!(_ALOTestClock>0.5&&_ALOPreviewWave>=0)&&_ALODebugHeight<0.5&&_ALOWidth<=0&&dot(ALOOffset(),ALOOffset())<0.000000000001)
                {
                    if(ALOUseBreathing())
                    {
                        float y0=mul(unity_ObjectToWorld,p[0].vertex).y;
                        float y1=mul(unity_ObjectToWorld,p[1].vertex).y;
                        float y2=mul(unity_ObjectToWorld,p[2].vertex).y;
                        float low=(min(y0,min(y1,y2))-ALOBottomWS())/(_ALOHeight*ALOScale());
                        float high=(max(y0,max(y1,y2))-ALOBottomWS())/(_ALOHeight*ALOScale());
                        // Match the full breathing band, including wrap-around.
                        if(!ALOBreathIntersects(low,high))return f;
                    }
                    else
                    {
                        uint w,h;_AudioTexture.GetDimensions(w,h);
                        if(w<128||h<4)return f;
                    }
                }
                f.edge[0]=EdgeFactor(p[1].vertex,p[2].vertex);f.edge[1]=EdgeFactor(p[2].vertex,p[0].vertex);f.edge[2]=EdgeFactor(p[0].vertex,p[1].vertex);
                f.inside=max(f.edge[0],max(f.edge[1],f.edge[2]));return f;
            }
            [domain("tri")][partitioning("integer")][outputtopology("triangle_cw")][outputcontrolpoints(3)][patchconstantfunc("Factors")]
            CP Hull(InputPatch<CP,3> p,uint id:SV_OutputControlPointID){return p[id];}
            [domain("tri")]
            ALOOutput Domain(TF f,OutputPatch<CP,3> p,float3 b:SV_DomainLocation)
            {
                ALOInput v;UNITY_INITIALIZE_OUTPUT(ALOInput,v);UNITY_TRANSFER_INSTANCE_ID(p[0],v);
                v.vertex=p[0].vertex*b.x+p[1].vertex*b.y+p[2].vertex*b.z;
                v.uv=p[0].uv*b.x+p[1].uv*b.y+p[2].uv*b.z;
                v.normal=normalize(p[0].normal*b.x+p[1].normal*b.y+p[2].normal*b.z);return ALOVertex(v);
            }
            ENDCG
        }
    }
    Fallback Off
}
