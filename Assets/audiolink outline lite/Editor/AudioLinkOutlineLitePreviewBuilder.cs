using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using VRC.SDK3.Avatars.Components;
namespace AudioLinkOutlineLite.Editor {
 internal static class AudioLinkOutlineLitePreviewBuilder {
  public static void Generate(AudioLinkOutlineLiteSettings settings,HashSet<UnityEngine.Object> owned){
   var avatar=settings.GetComponentInParent<VRCAvatarDescriptor>();var root=avatar.transform;
   var targets=settings.renderers.Where(r=>r&&r.sharedMesh&&r.transform.IsChildOf(root)&&AudioLinkOutlineLiteMesh.Indices(r).Length>0).Distinct().ToArray();
   if(targets.Length==0)throw new Exception("请选择角色上的三角面蒙皮网格。");
   var template=AssetDatabase.LoadAssetAtPath<Material>(AudioLinkOutlineLiteInstaller.Folder+"/Resources/Outline.mat");if(!template)throw new Exception("缺少轮廓材质，请重新导入安装包。");
   int stencil=AudioLinkOutlineLiteStencil.Select(settings.stencilBit,avatar.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials),Array.Empty<AnimationClip>());
   float bottom=float.PositiveInfinity,top=float.NegativeInfinity;
   foreach(var renderer in targets){var baked=new Mesh();try{renderer.BakeMesh(baked);foreach(var v in baked.vertices){float y=root.InverseTransformPoint(renderer.transform.TransformPoint(v)).y;bottom=Mathf.Min(bottom,y);top=Mathf.Max(top,y);}}finally{UnityEngine.Object.DestroyImmediate(baked);}}
   int budget=Mathf.Clamp(settings.shellTriangleBudget,AudioLinkOutlineLiteSettings.MinimumTriangleBudget,AudioLinkOutlineLiteSettings.MaximumTriangleBudget);
   var capacities=targets.Select(r=>AudioLinkOutlineLiteMesh.Indices(r).Length/3).ToArray();var desired=targets.Select((r,i)=>Mathf.Sqrt(capacities[i])*(r==avatar.VisemeSkinnedMesh?1.3f:1)).ToArray();
   var budgets=new int[targets.Length];int remaining=budget/2;
   for(int round=0;round<targets.Length+2&&remaining>0;round++){
    var active=Enumerable.Range(0,targets.Length).Where(i=>budgets[i]<capacities[i]).ToArray();if(active.Length==0)break;float sum=active.Sum(i=>desired[i]);int available=remaining;
    foreach(int i in active){int add=Math.Min(remaining,Math.Min(capacities[i]-budgets[i],Math.Max(1,Mathf.FloorToInt(available*desired[i]/sum))));budgets[i]+=add;remaining-=add;}
   }
   for(int index=0;index<targets.Length;index++){
    if(budgets[index]==0)continue;var source=targets[index];var mesh=AudioLinkOutlineLiteMesh.Create(source,budgets[index],new HashSet<string>(),out var bones);if(!mesh)continue;owned.Add(mesh);
    for(int mask=0;mask<2;mask++){
     var go=new GameObject(mask==0?AudioLinkOutlineLiteBuild.ChildName:"__ALOLiteMask");go.transform.SetParent(source.transform,false);go.layer=source.gameObject.layer;
     var renderer=go.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;renderer.bones=bones;renderer.rootBone=root;renderer.updateWhenOffscreen=true;
     renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.BlendProbes;renderer.probeAnchor=source.probeAnchor;
     var bounds=new Bounds(root.InverseTransformPoint(source.bounds.center),Vector3.zero);
     for(int c=0;c<8;c++)bounds.Encapsulate(root.InverseTransformPoint(source.bounds.center+Vector3.Scale(source.bounds.extents,new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1))));bounds.Expand(.3f+2*(top-bottom)*.05f);renderer.localBounds=bounds;
     renderer.sharedMaterials=Enumerable.Range(0,mesh.subMeshCount).Select(slot=>{
      var original=slot<source.sharedMaterials.Length?source.sharedMaterials[slot]:null;var material=AudioLinkOutlineLiteSurface.Create(original,template,stencil,mask==1);owned.Add(material);
      if(mask==0){material.SetFloat("_ALOBottom",bottom);material.SetFloat("_ALOHeight",Mathf.Max(.0001f,top-bottom));material.SetFloat("_ALOBaseScale",Mathf.Abs(root.lossyScale.y));material.SetFloat("_ALOAvatarScale",1);}return material;
     }).ToArray();
    }
   }
  }
 }
}
