sampler2D _ALOMainTex, _ALOAlphaMask;
float4 _ALOMainTex_ST, _ALOAlphaMask_ST;
float _ALOUseAlpha, _ALOColorAlpha, _ALOCutoff, _ALOAlphaMode, _ALOAlphaScale, _ALOAlphaOffset;
void ALOClipAlpha(float2 uv) {
 if(_ALOUseAlpha<0.5)return;
 float a=tex2D(_ALOMainTex,uv*_ALOMainTex_ST.xy+_ALOMainTex_ST.zw).a*_ALOColorAlpha;
 float mask=saturate(tex2D(_ALOAlphaMask,uv*_ALOAlphaMask_ST.xy+_ALOAlphaMask_ST.zw).r*_ALOAlphaScale+_ALOAlphaOffset);
 if(_ALOAlphaMode>0.5&&_ALOAlphaMode<1.5)a=mask;
 else if(_ALOAlphaMode<2.5&&_ALOAlphaMode>1.5)a*=mask;
 else if(_ALOAlphaMode<3.5&&_ALOAlphaMode>2.5)a=saturate(a+mask);
 else if(_ALOAlphaMode>3.5)a=saturate(a-mask);
 clip(a-max(_ALOCutoff,0.001));
}
