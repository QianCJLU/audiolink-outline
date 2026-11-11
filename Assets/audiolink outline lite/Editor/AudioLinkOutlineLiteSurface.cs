using UnityEngine;
namespace AudioLinkOutlineLite.Editor {
public static class AudioLinkOutlineLiteSurface {
 public static bool UsesAlpha(Material source) {
  string tag=source.GetTag("RenderType",false,"");
  return source.renderQueue>=2450||tag.Contains("Transparent")||source.IsKeywordEnabled("_ALPHATEST_ON")||source.IsKeywordEnabled("_ALPHABLEND_ON");
 }
 public static Material Create(Material source,Material template,int stencil,bool mask) {
  var result=mask?new Material(Shader.Find("Hidden/audiolink outline lite Surface Mask")):new Material(template);
  result.name=source.name+(mask?" [Contour Mask]":" [Contour Shell]");result.renderQueue=mask?4997:4998;result.SetInt("_ALOStencil",stencil);
  if(!mask)foreach(string property in new[]{"LightMinLimit","LightMaxLimit","MonochromeLighting","AsUnlit","VertexLightStrength"})
   if(source.HasProperty("_"+property))result.SetFloat("_ALO"+property,source.GetFloat("_"+property));
  string texture=source.HasProperty("_MainTex")?"_MainTex":"_BaseMap";
  result.SetTexture("_ALOMainTex",source.GetTexture(texture));result.SetTextureScale("_ALOMainTex",source.GetTextureScale(texture));result.SetTextureOffset("_ALOMainTex",source.GetTextureOffset(texture));
  result.SetFloat("_ALOUseAlpha",UsesAlpha(source)?1:0);
  result.SetFloat("_ALOColorAlpha",source.HasProperty("_Color")?source.GetColor("_Color").a:source.HasProperty("_BaseColor")?source.GetColor("_BaseColor").a:1);
  // Soft transparent surfaces need a silhouette threshold, rather than a solid card.
  float cutoff=source.HasProperty("_Cutoff")?source.GetFloat("_Cutoff"):0;
  result.SetFloat("_ALOCutoff",Mathf.Max(0.001f,cutoff));
  if(source.HasProperty("_AlphaMaskMode")) {
   result.SetFloat("_ALOAlphaMode",source.GetFloat("_AlphaMaskMode"));
   foreach(string suffix in new[]{"Scale","Value"})if(source.HasProperty("_AlphaMask"+suffix))result.SetFloat(suffix=="Scale"?"_ALOAlphaScale":"_ALOAlphaOffset",source.GetFloat("_AlphaMask"+suffix));
   if(source.HasProperty("_AlphaMask")){result.SetTexture("_ALOAlphaMask",source.GetTexture("_AlphaMask"));result.SetTextureScale("_ALOAlphaMask",source.GetTextureScale("_AlphaMask"));result.SetTextureOffset("_ALOAlphaMask",source.GetTextureOffset("_AlphaMask"));}
  }
  return result;
 }
}}

