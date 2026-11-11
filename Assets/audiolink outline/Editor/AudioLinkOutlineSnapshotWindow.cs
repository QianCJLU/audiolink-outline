using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using VRC.SDK3.Avatars.Components;
namespace AudioLinkOutline.Editor {
 public sealed class AudioLinkOutlineSnapshotWindow:EditorWindow {
  AudioLinkOutlineSettings settings;Camera sourceCamera;Texture2D[] images;string error="";int imageIndex,rangeChoice;string installStatus="";string peakLabel="波峰最大处";
  public static void Open(AudioLinkOutlineSettings settings){var window=GetWindow<AudioLinkOutlineSnapshotWindow>(true,"预览效果",true);window.settings=settings;window.sourceCamera=SceneView.lastActiveSceneView?SceneView.lastActiveSceneView.camera:null;window.minSize=new Vector2(440,430);window.Show();EditorApplication.delayCall+=window.Refresh;}
  void OnGUI(){
   if(!settings){EditorGUILayout.HelpBox("请选择角色上的轮廓配置。",MessageType.Info);return;}
   EditorGUILayout.LabelField("摄像头", "当前 Scene 视图");
   rangeChoice=EditorGUILayout.Popup("范围取值",rangeChoice,new[]{"游戏默认值","范围下限","范围上限"});
   if(GUILayout.Button("刷新预览"))Refresh();
   EditorGUILayout.HelpBox("按摄像头视角和场景灯光渲染当前角色，不包含场景其他物体遮挡和相机后处理。基础图不叠加波动；峰值图将整圈外扩到当前模式峰值；外观图使用固定时刻。快照强制显示轮廓。",MessageType.Info);
   if(!string.IsNullOrEmpty(error))EditorGUILayout.HelpBox(error,MessageType.Error);
   imageIndex=GUILayout.Toolbar(imageIndex,new[]{"未响应轮廓",peakLabel,"颜色与亮度"});
   if(images!=null&&images[imageIndex]){
    var texture=images[imageIndex];var area=GUILayoutUtility.GetRect(1,10000,100,10000,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));EditorGUI.DrawRect(area,new Color(.12f,.12f,.12f));GUI.DrawTexture(area,texture,ScaleMode.ScaleToFit,false);

   }

   if(!string.IsNullOrEmpty(installStatus))EditorGUILayout.HelpBox(installStatus,MessageType.Info);
   using(new EditorGUI.DisabledScope(images==null||!string.IsNullOrEmpty(error)))if(GUILayout.Button("确认一键安装")){
    try{AudioLinkOutlineInstaller.Repair(settings);EditorSceneManager.MarkSceneDirty(settings.gameObject.scene);installStatus="安装已应用，构建预览或上传角色时生成轮廓。";}
    catch(Exception e){error=e.Message;Debug.LogException(e);}
   }

  }
  void Refresh(){if(!this||!settings)return;sourceCamera=SceneView.lastActiveSceneView?SceneView.lastActiveSceneView.camera:null;installStatus="";try{
    var frames=Capture(settings,sourceCamera,rangeChoice,false);DisposeImages();images=frames;error="";
    bool manual=false;var audio=Shader.GetGlobalTexture("_AudioTexture");peakLabel=manual||!audio||audio.width<128||audio.height<4?"呼吸峰值":"音频峰值";
   }catch(Exception e){error=e.Message;Debug.LogException(e);}Repaint();}
  void DisposeImages(){if(images!=null)foreach(var texture in images)if(texture)DestroyImmediate(texture);images=null;}
  void OnDisable(){DisposeImages();}
  public static Texture2D[] Capture(AudioLinkOutlineSettings settings,Camera source,int range,bool framed){
   var avatar=settings.GetComponentInParent<VRCAvatarDescriptor>();if(!avatar)throw new Exception("请把轮廓预制件放在角色根对象下。");if(!source)throw new Exception("请打开 Scene 视图并调整到需要预览的角度。");
   var scene=EditorSceneManager.NewPreviewScene();GameObject clone=null;var frames=new Texture2D[3];var owned=new HashSet<UnityEngine.Object>();var originals=new HashSet<UnityEngine.Object>(avatar.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m).Cast<UnityEngine.Object>());
   foreach(var r in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(r.sharedMesh)originals.Add(r.sharedMesh);
   var previous=RenderTexture.active;RenderTexture rt=null;Camera cameraCopy=null;string generated="";bool success=false;
   try{
    EditorUtility.DisplayProgressBar("轮廓快照","构建独立角色预览…",.15f);
    clone=Instantiate(avatar.gameObject);clone.name="OutlineSnapshot_"+Guid.NewGuid().ToString("N");generated="Packages/nadena.dev.ndmf/__Generated/"+clone.name;UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(clone,scene);clone.transform.localScale=avatar.transform.lossyScale;
    clone.SetActive(true);
    var settingsPath=AnimationUtility.CalculateTransformPath(settings.transform,avatar.transform);
    var configuration=(string.IsNullOrEmpty(settingsPath)?clone.transform:clone.transform.Find(settingsPath)).GetComponent<AudioLinkOutlineSettings>();
    EditorUtility.CopySerializedManagedFieldsOnly(settings,configuration);configuration.enabled=true;configuration.gameObject.SetActive(true);
    configuration.renderers=settings.renderers.Where(r=>r).Select(r=>(r.transform==avatar.transform?clone.transform:clone.transform.Find(AnimationUtility.CalculateTransformPath(r.transform,avatar.transform)))?.GetComponent<SkinnedMeshRenderer>()).Where(r=>r).ToArray();
    var materialCopies=new Dictionary<Material,Material>();var meshCopies=new Dictionary<Mesh,Mesh>();
    foreach(var renderer in clone.GetComponentsInChildren<Renderer>(true)){
        renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>{if(!m)return m;if(!materialCopies.TryGetValue(m,out var copy)){copy=Instantiate(m);materialCopies[m]=copy;owned.Add(copy);}return copy;}).ToArray();
        if(renderer is SkinnedMeshRenderer skin&&skin.sharedMesh){var original=skin.sharedMesh;if(!meshCopies.TryGetValue(original,out var copy)){copy=Instantiate(original);meshCopies[original]=copy;owned.Add(copy);}skin.sharedMesh=copy;}
    }
    foreach(var camera in clone.GetComponentsInChildren<Camera>(true))camera.enabled=false;foreach(var listener in clone.GetComponentsInChildren<AudioListener>(true))listener.enabled=false;foreach(var animator in clone.GetComponentsInChildren<Animator>(true))animator.enabled=false;
    foreach(var light in Resources.FindObjectsOfTypeAll<Light>())if(light.gameObject.scene.IsValid()&&!EditorUtility.IsPersistent(light)&&light.isActiveAndEnabled&&light.gameObject.scene!=scene&&(!EditorSceneManager.IsPreviewScene(light.gameObject.scene)||light.gameObject.scene==avatar.gameObject.scene)&&!light.transform.IsChildOf(avatar.transform)){var go=new GameObject("Snapshot Light");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);go.transform.SetPositionAndRotation(light.transform.position,light.transform.rotation);EditorUtility.CopySerialized(light,go.AddComponent<Light>());}
    AudioLinkOutlinePreviewBuilder.Generate(configuration,owned);
    foreach(var animator in clone.GetComponentsInChildren<Animator>(true))animator.enabled=false;
    EditorUtility.DisplayProgressBar("轮廓快照","设置材质与场景灯光…",.6f);
    var shells=clone.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.sharedMaterials.Any(m=>m&&m.shader&&m.shader.name=="audiolink outline")).ToArray();if(shells.Length==0)throw new Exception("没有生成轮廓，请检查目标网格选择。");
    foreach(var renderer in clone.GetComponentsInChildren<Renderer>(true)){foreach(var material in renderer.sharedMaterials)if(material&&!EditorUtility.IsPersistent(material)&&!originals.Contains(material))owned.Add(material);if(renderer is SkinnedMeshRenderer skin&&skin.sharedMesh&&!EditorUtility.IsPersistent(skin.sharedMesh)&&!originals.Contains(skin.sharedMesh))owned.Add(skin.sharedMesh);}
    var materials=shells.SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct().ToArray();
    var normalized=Enumerable.Range(0,AudioLinkOutlineBuild.Parameters.Length).Select(AudioLinkOutlineBuild.DefaultValue).ToArray();if(range==1)for(int i=0;i<normalized.Length;i++)normalized[i]=0;if(range==2)for(int i=0;i<normalized.Length;i++)normalized[i]=1;
    foreach(var material in materials){foreach(int c in new[]{1,2,3,9,10,11})material.SetFloat(AudioLinkOutlineBuild.Properties[c],Mathf.Lerp(settings.ControlRange(c).x,settings.ControlRange(c).y,normalized[c]));material.SetFloat("_ALOBreath",0);material.SetFloat("_ALOOffsetHorizontal",range==0?0:range==1?settings.OffsetRange(0).x:settings.OffsetRange(0).y);material.SetFloat("_ALOOffsetVertical",range==0?0:range==1?settings.OffsetRange(1).x:settings.OffsetRange(1).y);}
    var effects=clone.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.sharedMaterials.Any(m=>m&&m.shader&&(m.shader.name=="audiolink outline"||m.shader.name=="Hidden/audiolink outline Surface Mask"))).ToArray();
    foreach(var renderer in effects){
     renderer.gameObject.SetActive(true);renderer.enabled=true;renderer.forceRenderingOff=false;renderer.updateWhenOffscreen=true;
     if(renderer.transform.parent)renderer.gameObject.layer=renderer.transform.parent.gameObject.layer;
     renderer.SetPropertyBlock(null);for(int slot=0;slot<renderer.sharedMaterials.Length;slot++)renderer.SetPropertyBlock(null,slot);
    }
    foreach(var renderer in clone.GetComponentsInChildren<Renderer>(true))if(renderer.lightProbeUsage!=UnityEngine.Rendering.LightProbeUsage.Off){UnityEngine.Rendering.SphericalHarmonicsL2 probe;LightProbes.GetInterpolatedProbe(renderer.bounds.center,null,out probe);var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);block.CopySHCoefficientArraysFrom(new[]{probe});renderer.SetPropertyBlock(block);renderer.lightProbeUsage=UnityEngine.Rendering.LightProbeUsage.CustomProvided;}
    var sourceView=SceneView.lastActiveSceneView;
    if(sourceView&&source==sourceView.camera&&!sourceView.sceneLighting){
        foreach(var light in Resources.FindObjectsOfTypeAll<Light>())if(light.gameObject.scene==scene)light.enabled=false;
        var headlight=new GameObject("Scene view preview light");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(headlight,scene);headlight.transform.rotation=source.transform.rotation;var previewLight=headlight.AddComponent<Light>();previewLight.type=LightType.Directional;previewLight.intensity=1;
    }
    var cameraGo=new GameObject("Snapshot Camera");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraGo,scene);cameraCopy=cameraGo.AddComponent<Camera>();cameraCopy.CopyFrom(source);cameraCopy.enabled=false;cameraCopy.renderingPath=RenderingPath.Forward;cameraCopy.useOcclusionCulling=false;foreach(var renderer in effects)cameraCopy.cullingMask|=1<<renderer.gameObject.layer;cameraCopy.scene=scene;cameraCopy.stereoTargetEye=StereoTargetEyeMask.None;cameraCopy.clearFlags=CameraClearFlags.SolidColor;cameraCopy.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
    if(framed){var bounds=shells[0].bounds;foreach(var renderer in shells.Skip(1))bounds.Encapsulate(renderer.bounds);float radius=Mathf.Max(.05f,bounds.extents.magnitude);cameraCopy.transform.position=bounds.center-source.transform.forward*radius*3;cameraCopy.transform.rotation=source.transform.rotation;if(cameraCopy.orthographic)cameraCopy.orthographicSize=radius*1.1f;cameraCopy.nearClipPlane=.01f;cameraCopy.farClipPlane=Mathf.Max(cameraCopy.farClipPlane,radius*8);}
    int width=1024,height=Mathf.Clamp(Mathf.RoundToInt(width/Mathf.Max(.2f,source.aspect)),256,1024);rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);rt.Create();cameraCopy.targetTexture=rt;cameraCopy.aspect=source.aspect;
    var audio=Shader.GetGlobalTexture("_AudioTexture");bool manual=false;float peak=manual||!audio||audio.width<128||audio.height<4?.5f:1;float clock=(float)(EditorApplication.timeSinceStartup%120);
    for(int mode=0;mode<3;mode++){
     foreach(var material in materials){material.SetFloat("_ALOOn",1);material.SetFloat("_ALOAvatarScale",1);material.SetFloat("_ALODebugHeight",0);material.SetFloat("_ALOTestClock",1);material.SetFloat("_ALOTime",clock);material.SetFloat("_ALOPreviewWave",mode==0?0:mode==1?peak:-1);}
     EditorUtility.DisplayProgressBar("轮廓快照","渲染快照 "+(mode+1)+" / 3",.7f+mode*.1f);
     cameraCopy.Render();RenderTexture.active=rt;frames[mode]=new Texture2D(width,height,TextureFormat.RGB24,false){hideFlags=HideFlags.HideAndDontSave};frames[mode].ReadPixels(new Rect(0,0,width,height),0,0);frames[mode].Apply();
    }
    success=true;return frames;
   }finally{EditorUtility.ClearProgressBar();RenderTexture.active=previous;if(cameraCopy)cameraCopy.targetTexture=null;if(rt){rt.Release();DestroyImmediate(rt);}EditorSceneManager.ClosePreviewScene(scene);foreach(var obj in owned)if(obj&&!EditorUtility.IsPersistent(obj))DestroyImmediate(obj);if(!string.IsNullOrEmpty(generated)&&AssetDatabase.IsValidFolder(generated))AssetDatabase.DeleteAsset(generated);if(!success)foreach(var texture in frames)if(texture)DestroyImmediate(texture);}
  }
 }
}
