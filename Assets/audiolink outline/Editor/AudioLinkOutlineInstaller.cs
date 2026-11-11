using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using nadena.dev.modular_avatar.core;

namespace AudioLinkOutline.Editor
{
    public static class AudioLinkOutlineInstaller
    {
        public const string Folder="Assets/audiolink outline";
        public const string PrefabPath=Folder+"/audiolink outline.prefab";
        public const string Version="0.2.5";

        public static AudioLinkOutlineSettings Install(VRCAvatarDescriptor avatar,VRCExpressionsMenu target)
        {
            if(!avatar)throw new ArgumentException("请选择带 VRC Avatar Descriptor 的角色。");
            if(EditorUtility.IsPersistent(avatar))throw new ArgumentException("请先把角色放入场景，再选择角色安装。");
            if(avatar.GetComponentsInChildren<Component>(true).Any(c=>c&&c.GetType().Name=="PlumAudioOutline"))
                throw new InvalidOperationException("该角色已安装旧版 Plum 轮廓。本安装包不会覆盖旧效果，请在其他角色上安装，或先自行移除旧配置。");
            var existing=avatar.GetComponentsInChildren<AudioLinkOutlineSettings>(true);
            if(existing.Length>1)throw new InvalidOperationException("存在多份音律轮廓，请保留一份。");
            AudioLinkOutlineSettings settings;
            if(existing.Length==1)settings=existing[0];
            else
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if(!prefab)throw new InvalidOperationException("缺少 audiolink outline.prefab，请重新导入完整安装包。");
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,avatar.transform);
                Undo.RegisterCreatedObjectUndo(go,"安装音律轮廓");settings=go.GetComponent<AudioLinkOutlineSettings>();
            }
            Repair(settings);
            var installer=settings.GetComponent<ModularAvatarMenuInstaller>();Undo.RecordObject(installer,"选择轮廓菜单位置");installer.installTargetMenu=target;EditorUtility.SetDirty(installer);
            PrefabUtility.RecordPrefabInstancePropertyModifications(installer);
            EditorSceneManager.MarkSceneDirty(avatar.gameObject.scene);
            Selection.activeGameObject=settings.gameObject;
            return settings;
        }

        public static void Repair(AudioLinkOutlineSettings settings)
        {
            var go=settings.gameObject;
            var menu=go.GetComponent<ModularAvatarMenuInstaller>();if(!menu)menu=Undo.AddComponent<ModularAvatarMenuInstaller>(go);
            var parameters=go.GetComponent<ModularAvatarParameters>();if(!parameters)parameters=Undo.AddComponent<ModularAvatarParameters>(go);
            var merge=go.GetComponent<ModularAvatarMergeAnimator>();if(!merge)merge=Undo.AddComponent<ModularAvatarMergeAnimator>(go);
            Undo.RecordObjects(new UnityEngine.Object[]{settings,menu,parameters,merge},"修复音律轮廓组件");
            settings.installationConfirmed=true;
            settings.outline=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Resources/Outline.mat");
            menu.menuToAppend=AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(Folder+"/Resources/Menu Entry.asset");
            parameters.parameters=Enumerable.Range(0,AudioLinkOutlineBuild.Parameters.Length).Select(i=>new ParameterConfig{
                nameOrPrefix=AudioLinkOutlineBuild.Parameters[i],syncType=i==0||i==4?ParameterSyncType.Bool:ParameterSyncType.Float,
                defaultValue=AudioLinkOutlineBuild.DefaultValue(i),saved=true,hasExplicitDefaultValue=true,internalParameter=false,localOnly=false
            }).ToList();
            parameters.parameters.Add(new ParameterConfig{nameOrPrefix=AudioLinkOutlineBuild.ResetParameter,syncType=ParameterSyncType.Bool,defaultValue=0,saved=false,localOnly=true,hasExplicitDefaultValue=true});
            AudioLinkOutlineOffsetSync.Configure(parameters);
            merge.animator=AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder+"/Resources/Parameters.controller");
            merge.layerType=VRCAvatarDescriptor.AnimLayerType.FX;merge.pathMode=MergeAnimatorPathMode.Absolute;
            parameters.hideFlags=HideFlags.HideInInspector;merge.hideFlags=HideFlags.HideInInspector;
            merge.matchAvatarWriteDefaults=true;merge.deleteAttachedAnimator=false;
            foreach(var obj in new UnityEngine.Object[]{settings,menu,parameters,merge}){EditorUtility.SetDirty(obj);if(PrefabUtility.IsPartOfPrefabInstance(obj))PrefabUtility.RecordPrefabInstancePropertyModifications(obj);}
        }

        // Build distribution resources in the author's workspace. This never
        // installs on, saves, or otherwise changes a user's current avatar.
        public static void GenerateResources()
        {
            if(!AssetDatabase.IsValidFolder(Folder+"/Resources"))AssetDatabase.CreateFolder(Folder,"Resources");
            string materialPath=Folder+"/Resources/Outline.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            var shader=Shader.Find("audiolink outline");if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("音律轮廓着色器编译失败。");
            if(!material){material=new Material(shader){name="audiolink outline"};AssetDatabase.CreateAsset(material,materialPath);}
            material.name="audiolink outline";material.shader=shader;material.renderQueue=4998;EditorUtility.SetDirty(material);
            var controls=Menu(Folder+"/Resources/Controls.asset");controls.controls.Clear();
            string[] names={"开启描边","描边亮度","描边粗细","描边色相","呼吸波动"};
            string[] iconNames={"outline","brightness","width","hue","breath"};
            for(int i=0;i<5;i++)
            {
                if(i!=0)continue;
                bool toggle=i==0||i==4;var c=new VRCExpressionsMenu.Control{name=names[i],type=toggle?VRCExpressionsMenu.Control.ControlType.Toggle:VRCExpressionsMenu.Control.ControlType.RadialPuppet,value=1};
                c.icon=Icon(iconNames[i]);
                if(toggle)c.parameter=new VRCExpressionsMenu.Control.Parameter{name=AudioLinkOutlineBuild.Parameters[i]};else c.subParameters=new[]{new VRCExpressionsMenu.Control.Parameter{name=AudioLinkOutlineBuild.Parameters[i]}};
                controls.controls.Add(c);
            }
            var attributes=Menu(Folder+"/Resources/Attributes.asset");attributes.controls.Clear();
            string[] attributeNames={"波峰宽度","未响应轮廓宽度","亮度","描边色相","呼吸波动","自动音频响应","呼吸波长","呼吸频率"};
            int[] attributeParameters={9,2,1,3,4,4,10,11};string[] attributeIcons={"wave-width","width","brightness","hue","breath","audio-mode","breath-length","breath-frequency"};
            for(int i=0;i<8;i++) {
                bool toggle=i==4||i==5;
                var control=new VRCExpressionsMenu.Control{name=attributeNames[i],icon=Icon(attributeIcons[i]),type=toggle?VRCExpressionsMenu.Control.ControlType.Toggle:VRCExpressionsMenu.Control.ControlType.RadialPuppet,value=i==5?0:1};
                if(toggle)control.parameter=new VRCExpressionsMenu.Control.Parameter{name=AudioLinkOutlineBuild.Parameters[4]};
                else control.subParameters=new[]{new VRCExpressionsMenu.Control.Parameter{name=AudioLinkOutlineBuild.Parameters[attributeParameters[i]]}};
                attributes.controls.Add(control);
            }
            EditorUtility.SetDirty(attributes);
            controls.controls.Insert(1,new VRCExpressionsMenu.Control{name="描边属性",icon=Icon("attributes"),type=VRCExpressionsMenu.Control.ControlType.SubMenu,subMenu=attributes});
            controls.controls.Add(new VRCExpressionsMenu.Control{
                name="剪影偏移",icon=Icon("offset"),type=VRCExpressionsMenu.Control.ControlType.FourAxisPuppet,
                subParameters=AudioLinkOutlineBuild.Parameters.Skip(5).Take(4).Select(p=>new VRCExpressionsMenu.Control.Parameter{name=p}).ToArray(),
                labels=new[]{"上","右","下","左"}.Select((t,i)=>new VRCExpressionsMenu.Control.Label{name=t,icon=Icon(new[]{"arrow-up","arrow-right","arrow-down","arrow-left"}[i])}).ToArray()
            });
            controls.controls.Add(new VRCExpressionsMenu.Control{name="偏移归中",icon=Icon("reset"),type=VRCExpressionsMenu.Control.ControlType.Button,parameter=new VRCExpressionsMenu.Control.Parameter{name=AudioLinkOutlineBuild.ResetParameter},value=1});
            var entry=Menu(Folder+"/Resources/Menu Entry.asset");entry.controls.Clear();entry.controls.Add(new VRCExpressionsMenu.Control{name="audiolink outline",icon=Icon("audiolink-outline"),type=VRCExpressionsMenu.Control.ControlType.SubMenu,subMenu=controls});
            EditorUtility.SetDirty(controls);EditorUtility.SetDirty(entry);
            string controllerPath=Folder+"/Resources/Parameters.controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if(!controller)
            {
                controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                for(int i=0;i<5;i++)controller.AddParameter(new AnimatorControllerParameter{name=AudioLinkOutlineBuild.Parameters[i],type=i==0||i==4?AnimatorControllerParameterType.Bool:AnimatorControllerParameterType.Float,defaultBool=i==0,defaultFloat=i==1||i==2?1:0});
                var idle=controller.layers[0].stateMachine.AddState("Parameter seed");idle.writeDefaultValues=true;controller.layers[0].stateMachine.defaultState=idle;
            }
            foreach(string parameter in AudioLinkOutlineBuild.Parameters.Skip(5))
                if(!controller.parameters.Any(p=>p.name==parameter))controller.AddParameter(new AnimatorControllerParameter{name=parameter,type=AnimatorControllerParameterType.Float,defaultFloat=AudioLinkOutlineBuild.DefaultValue(Array.IndexOf(AudioLinkOutlineBuild.Parameters,parameter))});
            if(!controller.parameters.Any(p=>p.name==AudioLinkOutlineBuild.ResetParameter))controller.AddParameter(AudioLinkOutlineBuild.ResetParameter,AnimatorControllerParameterType.Bool);
            EditorUtility.SetDirty(controller);
            var go=new GameObject("audiolink outline");try{var settings=go.AddComponent<AudioLinkOutlineSettings>();Repair(settings);settings.installationConfirmed=false;PrefabUtility.SaveAsPrefabAsset(go,PrefabPath);}finally{UnityEngine.Object.DestroyImmediate(go);}
            AssetDatabase.SaveAssets();
        }
        static VRCExpressionsMenu Menu(string path){var menu=AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(path);if(!menu){menu=ScriptableObject.CreateInstance<VRCExpressionsMenu>();AssetDatabase.CreateAsset(menu,path);}return menu;}
        static Texture2D Icon(string name)
        {
            string path=Folder+"/Resources/Icons/"+name+".png";
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(!importer)throw new Exception("缺少菜单图标："+path);
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;
            importer.mipmapEnabled=false;importer.isReadable=false;importer.maxTextureSize=128;importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.CompressedHQ;
            // MA preserves DXT icons directly; BC7 is re-encoded with mipmaps.
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Standalone",overridden=true,maxTextureSize=128,format=TextureImporterFormat.DXT5,compressionQuality=100});importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }

 [CustomEditor(typeof(AudioLinkOutlineSettings))]
 public sealed class AudioLinkOutlineInspector:UnityEditor.Editor {
  [SerializeField] bool[] expanded={true,false,false,false,false};Vector2 meshScroll;string search="";
  void OnEnable(){serializedObject.FindProperty("renderers").isExpanded=true;}
  readonly string[] headings={"安装与目标","颜色与亮度","轮廓宽度","呼吸波形","剪影偏移"};
  public override void OnInspectorGUI(){
   serializedObject.Update();var settings=(AudioLinkOutlineSettings)target;var avatar=settings.GetComponentInParent<VRCAvatarDescriptor>();
   using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox)){
    using(new EditorGUILayout.HorizontalScope()){
     EditorGUILayout.LabelField("audiolink outline",EditorStyles.boldLabel);
    }
    EditorGUILayout.LabelField("v"+AudioLinkOutlineInstaller.Version+"  ·  游戏内调节范围  ·  同步占用 66 bits",EditorStyles.miniLabel);
   }
   for(int section=0;section<headings.Length;section++){
    expanded[section]=EditorGUILayout.Foldout(expanded[section],headings[section],true,EditorStyles.foldoutHeader);
    if(!expanded[section])continue;
    using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox)){
     switch(section){
      case 0:
       if(!avatar)EditorGUILayout.HelpBox("将此预制件拖到带 Avatar Descriptor 的角色根对象下，再选择目标网格。",MessageType.Info);
       else DrawTargets(avatar);
       EditorGUILayout.LabelField("菜单位置：在下方 MA Menu Installer 中选择。",EditorStyles.wordWrappedMiniLabel);break;
      case 1:
       Range("brightnessLimits","亮度");Range("hueLimits","色相");
       EditorGUILayout.LabelField("亮度下限 0 使用场景光照；色相上限 100% 可启用循环彩虹。",EditorStyles.wordWrappedMiniLabel);
       if(GUILayout.Button("恢复此组默认")){ResetRange("brightnessLimits");ResetRange("hueLimits");}break;
      case 2:
       Range("widthLimits","基础轮廓宽度");Range("waveWidthLimits","波峰外扩幅度");
       if(GUILayout.Button("恢复此组默认")){ResetRange("widthLimits");ResetRange("waveWidthLimits");}break;
      case 3:
       Range("breathLengthLimits","呼吸波长");Range("breathFrequencyLimits","呼吸频率");
       EditorGUILayout.LabelField("实际值 50%：波长约身高 25%、频率为四秒周期。100%：波长约身高 50%、频率为两秒周期。",EditorStyles.wordWrappedMiniLabel);
       if(GUILayout.Button("恢复此组默认")){ResetRange("breathLengthLimits");ResetRange("breathFrequencyLimits");}break;
      case 4:
       OffsetRange("horizontalLimits","水平偏移");OffsetRange("verticalLimits","垂直偏移");
       EditorGUILayout.LabelField("负向和正向极限分别对应轮盘左/下和右/上。中点与归中始终为 0；100% 偏移量约为角色高度的 5%。",EditorStyles.wordWrappedMiniLabel);
       if(GUILayout.Button("恢复此组默认")){ResetRange("horizontalLimits",true);ResetRange("verticalLimits",true);}break;

     }
    }
   }
   using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox)){

       Field("shellTriangleBudget","新增三角面预算","轮廓和遮罩合计，默认 70,000，上限 200,000。");
       var bit=serializedObject.FindProperty("stencilBit");int[] bits={1,2,4,8,16,32,64,128};bit.intValue=EditorGUILayout.IntPopup("优先遮罩位",bit.intValue,bits.Select(v=>v.ToString()).ToArray(),bits);
       EditorGUILayout.LabelField("遮罩位冲突时自动避让。PC 蒙皮网格适用。",EditorStyles.wordWrappedMiniLabel);
       EditorGUILayout.LabelField(settings.installationConfirmed?"已确认安装":"等待确认安装，可先预览效果。",EditorStyles.wordWrappedMiniLabel);
     using(new EditorGUI.DisabledScope(!avatar||(settings.renderers?.Length??0)==0))if(GUILayout.Button("预览效果")){serializedObject.ApplyModifiedProperties();AudioLinkOutlineSnapshotWindow.Open(settings);GUIUtility.ExitGUI();}
       if(GUILayout.Button("一键应用安装/修复")){serializedObject.ApplyModifiedProperties();AudioLinkOutlineInstaller.Repair(settings);serializedObject.Update();}
   }
   if(serializedObject.ApplyModifiedProperties())SceneView.RepaintAll();
   EditorGUILayout.LabelField("轮盘 0% 对应下限，100% 对应上限。范围在构建时生效。",EditorStyles.wordWrappedMiniLabel);
  }
  void Field(string name,string label,string tip=""){EditorGUILayout.PropertyField(serializedObject.FindProperty(name),new GUIContent(label,tip));}
  void Range(string property,string label){
   var p=serializedObject.FindProperty(property);Vector2 value=p.vector2Value;float min=Mathf.Clamp01(Mathf.Min(value.x,value.y))*100,max=Mathf.Clamp01(Mathf.Max(value.x,value.y))*100;
   EditorGUILayout.LabelField(label,EditorStyles.boldLabel);
   EditorGUILayout.MinMaxSlider(ref min,ref max,0,100);
   float previousLabelWidth=EditorGUIUtility.labelWidth;
   try {EditorGUIUtility.labelWidth=58;using(new EditorGUILayout.HorizontalScope()){
    min=EditorGUILayout.FloatField(new GUIContent("下限 (%)"),min);max=EditorGUILayout.FloatField(new GUIContent("上限 (%)"),max);
   }}finally{EditorGUIUtility.labelWidth=previousLabelWidth;}
   min=Mathf.Clamp(min,0,100);max=Mathf.Clamp(max,min,100);p.vector2Value=new Vector2(min/100,max/100);
  }
  void OffsetRange(string property,string label){
   var p=serializedObject.FindProperty(property);Vector2 value=p.vector2Value;EditorGUILayout.LabelField(label,EditorStyles.boldLabel);
   value.x=EditorGUILayout.Slider("负向下限 (%)",value.x*100,-100,0)/100;value.y=EditorGUILayout.Slider("正向上限 (%)",value.y*100,0,100)/100;p.vector2Value=value;
  }
  void ResetRange(string property,bool signed=false){serializedObject.FindProperty(property).vector2Value=new Vector2(signed?-1:0,1);}
  void DrawTargets(VRCAvatarDescriptor avatar){
   var list=serializedObject.FindProperty("renderers");
   using(new EditorGUI.IndentLevelScope())EditorGUILayout.PropertyField(list,new GUIContent("手动拖入目标"),true);
   search=EditorGUILayout.TextField("搜索网格",search);var selected=new System.Collections.Generic.HashSet<SkinnedMeshRenderer>();for(int i=0;i<list.arraySize;i++)if(list.GetArrayElementAtIndex(i).objectReferenceValue is SkinnedMeshRenderer r)selected.Add(r);
   EditorGUILayout.LabelField("已选 "+selected.Count+" 个网格",EditorStyles.miniLabel);
   var candidates=avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.sharedMesh&&r.name!="__ALOOutline"&&r.name!="__ALOMask").ToArray();
   using(var view=new EditorGUILayout.ScrollViewScope(meshScroll,GUILayout.Height(Mathf.Clamp(candidates.Length*21+6,45,180)))){
    meshScroll=view.scrollPosition;foreach(var r in candidates){string path=AnimationUtility.CalculateTransformPath(r.transform,avatar.transform);if(!string.IsNullOrWhiteSpace(search)&&path.IndexOf(search,StringComparison.OrdinalIgnoreCase)<0)continue;
     bool supported=!AudioLinkOutlineMesh.HasUnverifiedSurface(r);{bool was=selected.Contains(r);bool next=EditorGUILayout.ToggleLeft(path+(supported?"":" [可能有渲染问题]"),was);if(was!=next){if(next)selected.Add(r);else selected.Remove(r);list.arraySize=selected.Count;int i=0;foreach(var item in selected)list.GetArrayElementAtIndex(i++).objectReferenceValue=item;}}
    }
   }
   if(selected.Any(AudioLinkOutlineMesh.HasUnverifiedSurface))EditorGUILayout.HelpBox("所选网格包含未适配的着色器或缺失的材质，将尝试按通用表面生成轮廓，可能会有透明裁剪、遮挡或其他渲染问题。",MessageType.Warning);
   if(GUILayout.Button("清空选择"))list.arraySize=0;
  }
 }
}
