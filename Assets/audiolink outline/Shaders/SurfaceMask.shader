Shader "Hidden/audiolink outline Surface Mask" {
 Properties { _ALOStencil ("Stencil",Int)=64

        [HideInInspector] _ALOMainTex ("Source Alpha",2D)="white" {}
        [HideInInspector] _ALOAlphaMask ("Alpha Mask",2D)="white" {}
        [HideInInspector] _ALOUseAlpha ("Use alpha",Float)=0
        [HideInInspector] _ALOColorAlpha ("Source opacity",Float)=1
        [HideInInspector] _ALOCutoff ("Alpha cutoff",Float)=0.5
        [HideInInspector] _ALOAlphaMode ("Alpha mode",Float)=0
        [HideInInspector] _ALOAlphaScale ("Alpha scale",Float)=1
        [HideInInspector] _ALOAlphaOffset ("Alpha offset",Float)=0

 }
 SubShader {
 Tags {"Queue"="Overlay+997" "RenderType"="Transparent" "VRCFallback"="Hidden"}
 Pass {
 // This is a projected silhouette exclusion mask, not a visible surface.
 Cull Off ZWrite Off ZTest Always ColorMask 0
 Stencil {Ref [_ALOStencil] ReadMask [_ALOStencil] WriteMask [_ALOStencil] Comp Always Pass Replace}
 CGPROGRAM
 #pragma target 3.0
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "UnityCG.cginc"
 #include "SurfaceAlpha.cginc"
 struct input {float4 vertex:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct output {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
 output vert(input v){UNITY_SETUP_INSTANCE_ID(v);output o;UNITY_INITIALIZE_OUTPUT(output,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
 float4 frag(output i):SV_Target {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);ALOClipAlpha(i.uv);return 0;}
 ENDCG
 }
 }
}

