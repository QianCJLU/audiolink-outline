using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using nadena.dev.modular_avatar.core;
namespace AudioLinkOutlineLite.Editor {
 public static class AudioLinkOutlineLiteInstaller {
  public const string Version="0.1.6",Folder="Assets/audiolink outline lite",PrefabPath=Folder+"/audiolink outline lite.prefab";
  public static void Repair(AudioLinkOutlineLiteSettings settings){
   var menu=settings.GetComponent<ModularAvatarMenuInstaller>()??Undo.AddComponent<ModularAvatarMenuInstaller>(settings.gameObject);
   var parameters=settings.GetComponent<ModularAvatarParameters>()??Undo.AddComponent<ModularAvatarParameters>(settings.gameObject);
   var merge=settings.GetComponent<ModularAvatarMergeAnimator>()??Undo.AddComponent<ModularAvatarMergeAnimator>(settings.gameObject);
   Undo.RecordObjects(new UnityEngine.Object[]{settings,menu,parameters,merge},"配置轻量版安装组件");
   settings.installationConfirmed=true;
   settings.outline=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Resources/Outline.mat");
   menu.menuToAppend=AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(Folder+"/Resources/Menu.asset");
   ConfigureParameters(parameters,settings);
   merge.animator=AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder+"/Resources/Parameters.controller");merge.layerType=VRCAvatarDescriptor.AnimLayerType.FX;merge.pathMode=MergeAnimatorPathMode.Absolute;merge.matchAvatarWriteDefaults=true;
   parameters.hideFlags=HideFlags.HideInInspector;merge.hideFlags=HideFlags.HideInInspector;
   foreach(var obj in new UnityEngine.Object[]{settings,menu,parameters,merge}){EditorUtility.SetDirty(obj);if(PrefabUtility.IsPartOfPrefabInstance(obj))PrefabUtility.RecordPrefabInstancePropertyModifications(obj);}
  }
  public static void ConfigureParameters(ModularAvatarParameters parameters,AudioLinkOutlineLiteSettings settings){
   parameters.parameters=new System.Collections.Generic.List<ParameterConfig>{
    new ParameterConfig{nameOrPrefix=AudioLinkOutlineLiteBuild.ToggleParameter,syncType=ParameterSyncType.Bool,defaultValue=settings.defaultOn?1:0,saved=true,hasExplicitDefaultValue=true},
    new ParameterConfig{nameOrPrefix=AudioLinkOutlineLiteBuild.ModeParameter,syncType=ParameterSyncType.Bool,defaultValue=settings.defaultBreathing?1:0,saved=true,hasExplicitDefaultValue=true}};
  }
  public static void GenerateResources(){
   var shader=Shader.Find("audiolink outline lite");if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Lite shader compile failed");
   var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Resources/Outline.mat");if(!mat){mat=new Material(shader);AssetDatabase.CreateAsset(mat,Folder+"/Resources/Outline.mat");}mat.shader=shader;mat.name="audiolink outline lite";mat.renderQueue=4998;EditorUtility.SetDirty(mat);
   var menu=AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(Folder+"/Resources/Menu.asset");if(!menu){menu=ScriptableObject.CreateInstance<VRCExpressionsMenu>();AssetDatabase.CreateAsset(menu,Folder+"/Resources/Menu.asset");}
   foreach(var path in AssetDatabase.FindAssets("t:Texture2D",new[]{Folder+"/Resources/Icons"}).Select(AssetDatabase.GUIDToAssetPath)){
    var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.maxTextureSize=128;importer.isReadable=false;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
   }
   var controls=AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(Folder+"/Resources/Controls.asset");if(!controls){controls=ScriptableObject.CreateInstance<VRCExpressionsMenu>();AssetDatabase.CreateAsset(controls,Folder+"/Resources/Controls.asset");}
   var icon=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Resources/Icons/audiolink-outline.png");
   controls.controls=new System.Collections.Generic.List<VRCExpressionsMenu.Control>{
    new VRCExpressionsMenu.Control{name="开启描边",icon=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Resources/Icons/outline.png"),type=VRCExpressionsMenu.Control.ControlType.Toggle,parameter=new VRCExpressionsMenu.Control.Parameter{name=AudioLinkOutlineLiteBuild.ToggleParameter},value=1},
    new VRCExpressionsMenu.Control{name="固定呼吸",icon=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Resources/Icons/breath.png"),type=VRCExpressionsMenu.Control.ControlType.Toggle,parameter=new VRCExpressionsMenu.Control.Parameter{name=AudioLinkOutlineLiteBuild.ModeParameter},value=1},
    new VRCExpressionsMenu.Control{name="自动音频响应",icon=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Resources/Icons/audio-mode.png"),type=VRCExpressionsMenu.Control.ControlType.Toggle,parameter=new VRCExpressionsMenu.Control.Parameter{name=AudioLinkOutlineLiteBuild.ModeParameter},value=0}};
   menu.controls=new System.Collections.Generic.List<VRCExpressionsMenu.Control>{new VRCExpressionsMenu.Control{name="audiolink outline lite",icon=icon,type=VRCExpressionsMenu.Control.ControlType.SubMenu,subMenu=controls}};EditorUtility.SetDirty(menu);EditorUtility.SetDirty(controls);
   var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder+"/Resources/Parameters.controller");if(!controller){controller=AnimatorController.CreateAnimatorControllerAtPath(Folder+"/Resources/Parameters.controller");controller.AddParameter(new AnimatorControllerParameter{name=AudioLinkOutlineLiteBuild.ToggleParameter,type=AnimatorControllerParameterType.Bool,defaultBool=true});var idle=controller.layers[0].stateMachine.AddState("Ready");}
   if(!controller.parameters.Any(p=>p.name==AudioLinkOutlineLiteBuild.ModeParameter))controller.AddParameter(AudioLinkOutlineLiteBuild.ModeParameter,AnimatorControllerParameterType.Bool);
   EditorUtility.SetDirty(controller);
   var go=new GameObject("audiolink outline lite");try{var settings=go.AddComponent<AudioLinkOutlineLiteSettings>();Repair(settings);settings.installationConfirmed=false;PrefabUtility.SaveAsPrefabAsset(go,PrefabPath);}finally{UnityEngine.Object.DestroyImmediate(go);}AssetDatabase.SaveAssets();
  }
 }
 [CustomEditor(typeof(AudioLinkOutlineLiteSettings))]
 public sealed class AudioLinkOutlineLiteInspector:UnityEditor.Editor {
  [SerializeField] bool[] expanded={true,false,false,false,false};Vector2 meshScroll;string search="";
  void OnEnable(){serializedObject.FindProperty("renderers").isExpanded=true;}
  readonly string[] headings={"安装与目标","颜色与亮度","轮廓宽度","呼吸波形","剪影偏移"};
  public override void OnInspectorGUI(){
   serializedObject.Update();var settings=(AudioLinkOutlineLiteSettings)target;var avatar=settings.GetComponentInParent<VRCAvatarDescriptor>();
   using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox)){
    using(new EditorGUILayout.HorizontalScope()){
     EditorGUILayout.LabelField("audiolink outline lite",EditorStyles.boldLabel);
    }
    EditorGUILayout.LabelField("v"+AudioLinkOutlineLiteInstaller.Version+"  ·  游戏内开关与模式  ·  同步占用 2 bits",EditorStyles.miniLabel);
   }
   for(int section=0;section<headings.Length;section++){
    expanded[section]=EditorGUILayout.Foldout(expanded[section],headings[section],true,EditorStyles.foldoutHeader);
    if(!expanded[section])continue;
    using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox)){
     switch(section){
      case 0:
       Field("defaultOn","默认开启");
       var mode=serializedObject.FindProperty("defaultBreathing");mode.boolValue=EditorGUILayout.Popup("默认模式",mode.boolValue?1:0,new[]{"自动音频响应","固定呼吸"})==1;
       if(!avatar)EditorGUILayout.HelpBox("将此预制件拖到带 Avatar Descriptor 的角色根对象下，再选择目标网格。",MessageType.Info);
       else {if(avatar.GetComponentsInChildren<Component>(true).Any(c=>c&&c.GetType().FullName=="AudioLinkOutline.AudioLinkOutlineSettings"))EditorGUILayout.HelpBox("当前角色还有完整版配置。请移除完整版预制件，再使用轻量版。",MessageType.Warning);DrawTargets(avatar);}
       EditorGUILayout.LabelField("菜单位置：在下方 MA Menu Installer 中选择。",EditorStyles.wordWrappedMiniLabel);break;
      case 1:
       Field("brightness","亮度","0 使用对应材质的场景光照，1 输出 HDR 高亮。");Field("hue","色相","0 为白色，1 为向上流动的循环彩虹。");
       if(GUILayout.Button("恢复此组默认")){Set("brightness",1);Set("hue",0);}break;
      case 2:
       Field("width","基础轮廓宽度");Field("waveWidth","波峰外扩幅度");if(GUILayout.Button("恢复此组默认")){Set("width",1);Set("waveWidth",1);}break;
      case 3:
       EditorGUILayout.LabelField("有 AudioLink 时响应音频；没有时使用呼吸。",EditorStyles.wordWrappedMiniLabel);
       Field("breathLength","呼吸波长","50% 约为角色高度的四分之一。");Field("breathFrequency","呼吸频率","50% 为四秒周期，100% 为两秒周期，0 停止传播。");
       if(GUILayout.Button("恢复此组默认")){Set("breathLength",.5f);Set("breathFrequency",.5f);}break;
      case 4:
       var offset=serializedObject.FindProperty("offset");var value=offset.vector2Value;value.x=EditorGUILayout.Slider("水平",value.x,-1,1);value.y=EditorGUILayout.Slider("垂直",value.y,-1,1);offset.vector2Value=value;
       EditorGUILayout.LabelField("正值向右 / 向上，最大偏移约为角色高度的 5%。",EditorStyles.wordWrappedMiniLabel);
       if(GUILayout.Button("偏移归中"))offset.vector2Value=Vector2.zero;break;

     }
    }
   }
   using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox)){

       Field("shellTriangleBudget","新增三角面预算","轮廓和遮罩合计，默认 70,000，上限 200,000。");
       var bit=serializedObject.FindProperty("stencilBit");int[] bits={1,2,4,8,16,32,64,128};bit.intValue=EditorGUILayout.IntPopup("优先遮罩位",bit.intValue,bits.Select(v=>v.ToString()).ToArray(),bits);
       EditorGUILayout.LabelField("遮罩位冲突时自动避让。PC 蒙皮网格适用。",EditorStyles.wordWrappedMiniLabel);
       EditorGUILayout.LabelField(settings.installationConfirmed?"已确认安装":"等待确认安装，可先预览效果。",EditorStyles.wordWrappedMiniLabel);
     using(new EditorGUI.DisabledScope(!avatar||(settings.renderers?.Length??0)==0))if(GUILayout.Button("预览效果")){serializedObject.ApplyModifiedProperties();AudioLinkOutlineLiteSnapshotWindow.Open(settings);GUIUtility.ExitGUI();}
       if(GUILayout.Button("一键应用安装/修复")){serializedObject.ApplyModifiedProperties();AudioLinkOutlineLiteInstaller.Repair(settings);serializedObject.Update();}
   }
   if(serializedObject.ApplyModifiedProperties())SceneView.RepaintAll();
   EditorGUILayout.LabelField("属性在构建时生效；修改后重新构建预览。",EditorStyles.wordWrappedMiniLabel);
  }
  void Field(string name,string label,string tip=""){EditorGUILayout.PropertyField(serializedObject.FindProperty(name),new GUIContent(label,tip));}
  void Set(string name,float value){serializedObject.FindProperty(name).floatValue=value;}
  void DrawTargets(VRCAvatarDescriptor avatar){
   var list=serializedObject.FindProperty("renderers");
   using(new EditorGUI.IndentLevelScope())EditorGUILayout.PropertyField(list,new GUIContent("手动拖入目标"),true);
   search=EditorGUILayout.TextField("搜索网格",search);var selected=new System.Collections.Generic.HashSet<SkinnedMeshRenderer>();for(int i=0;i<list.arraySize;i++)if(list.GetArrayElementAtIndex(i).objectReferenceValue is SkinnedMeshRenderer r)selected.Add(r);
   EditorGUILayout.LabelField("已选 "+selected.Count+" 个网格",EditorStyles.miniLabel);
   var candidates=avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.sharedMesh&&r.name!="__ALOLiteOutline"&&r.name!="__ALOLiteMask").ToArray();
   using(var view=new EditorGUILayout.ScrollViewScope(meshScroll,GUILayout.Height(Mathf.Clamp(candidates.Length*21+6,45,180)))){
    meshScroll=view.scrollPosition;foreach(var r in candidates){string path=AnimationUtility.CalculateTransformPath(r.transform,avatar.transform);if(!string.IsNullOrWhiteSpace(search)&&path.IndexOf(search,StringComparison.OrdinalIgnoreCase)<0)continue;
     bool supported=!AudioLinkOutlineLiteMesh.HasUnverifiedSurface(r);{bool was=selected.Contains(r);bool next=EditorGUILayout.ToggleLeft(path+(supported?"":" [可能有渲染问题]"),was);if(was!=next){if(next)selected.Add(r);else selected.Remove(r);list.arraySize=selected.Count;int i=0;foreach(var item in selected)list.GetArrayElementAtIndex(i++).objectReferenceValue=item;}}
    }
   }
   if(selected.Any(AudioLinkOutlineLiteMesh.HasUnverifiedSurface))EditorGUILayout.HelpBox("所选网格包含未适配的着色器或缺失的材质，将尝试按通用表面生成轮廓，可能会有透明裁剪、遮挡或其他渲染问题。",MessageType.Warning);
   if(GUILayout.Button("清空选择"))list.arraySize=0;
  }
 }
}
