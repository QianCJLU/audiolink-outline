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
        public const string Version="0.0.2";

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
            var go=new GameObject("audiolink outline");try{var settings=go.AddComponent<AudioLinkOutlineSettings>();Repair(settings);PrefabUtility.SaveAsPrefabAsset(go,PrefabPath);}finally{UnityEngine.Object.DestroyImmediate(go);}
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
    public sealed class AudioLinkOutlineInspector:UnityEditor.Editor
    {
        [SerializeField] bool targetsExpanded;
        Vector2 targetScroll;

        public override void OnInspectorGUI()
        {
            var s=(AudioLinkOutlineSettings)target;var avatar=s.GetComponentInParent<VRCAvatarDescriptor>();
            EditorGUILayout.LabelField("audiolink outline "+AudioLinkOutlineInstaller.Version,EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("放在角色根节点下即可随构建生成；编辑模式不直接生成重复网格。支持有主纹理属性的常见角色材质（含透明材质）；静态网格和粒子暂不支持。新增面数包含轮廓和遮罩。",MessageType.Info);
            if(!avatar&&!EditorUtility.IsPersistent(s))EditorGUILayout.HelpBox("请把预制件拖到 Avatar Descriptor 根对象下面。",MessageType.Warning);
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("shellTriangleBudget"),new GUIContent("新增轮廓面数上限"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("renderers"),new GUIContent("需要描边的部件（手动选择）"),true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("stencilBit"),new GUIContent("优先遮罩位（冲突自动避让）"));
            serializedObject.ApplyModifiedProperties();
            if(s.renderers==null||s.renderers.Length==0)EditorGUILayout.HelpBox("尚未选择部件，不会自动生成轮廓。可拖入上方列表，或在下方直接勾选。",MessageType.Warning);
            if(avatar)
            {
                EditorGUILayout.Space();
                int selectedCount=(s.renderers??Array.Empty<SkinnedMeshRenderer>()).Where(r=>r).Distinct().Count();
                targetsExpanded=EditorGUILayout.Foldout(targetsExpanded,"勾选生成目标（已选 "+selectedCount+"）",true,EditorStyles.foldoutHeader);
                if(targetsExpanded)
                {
                var candidates=avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.sharedMesh&&r.name!=AudioLinkOutlineBuild.ChildName&&r.name!="__ALOMask").ToArray();
                float height=Mathf.Min(240f,Mathf.Max(30f,candidates.Length*(EditorGUIUtility.singleLineHeight+EditorGUIUtility.standardVerticalSpacing)+8f));
                using(var view=new EditorGUILayout.ScrollViewScope(targetScroll,GUILayout.Height(height)))
                {
                targetScroll=view.scrollPosition;
                var selected=new HashSet<SkinnedMeshRenderer>(s.renderers??Array.Empty<SkinnedMeshRenderer>());
                foreach(var r in candidates)
                {
                    bool supported=AudioLinkOutlineMesh.Indices(r).Length>0;
                    using(new EditorGUI.DisabledScope(!supported))
                    {
                        bool was=selected.Contains(r);
                        bool chosen=EditorGUILayout.ToggleLeft(AnimationUtility.CalculateTransformPath(r.transform,avatar.transform)+(supported?"":" [材质不支持]"),was);
                        if(chosen!=was)
                        {
                            Undo.RecordObject(s,"选择轮廓目标");
                            if(chosen)selected.Add(r);else selected.Remove(r);
                            s.renderers=selected.Where(x=>x).ToArray();EditorUtility.SetDirty(s);PrefabUtility.RecordPrefabInstancePropertyModifications(s);
                        }
                    }
                }
                }
                }
            }
            EditorGUILayout.HelpBox("在下方 MA Menu Installer 中选择菜单安装位置。亮度为 0 时接收环境光；使用开启描边开关完全关闭轮廓。",MessageType.Info);
            if(GUILayout.Button("修复安装组件（不修改原菜单）"))AudioLinkOutlineInstaller.Repair(s);
            if(avatar&&GUILayout.Button("检查将包含哪些网格"))
            {
                var list=avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.sharedMesh&&AudioLinkOutlineBuild.Selected(r,avatar.transform,s)&&AudioLinkOutlineMesh.Indices(r).Length>0).Select(r=>AnimationUtility.CalculateTransformPath(r.transform,avatar.transform)).ToArray();
                EditorUtility.DisplayDialog("将生成轮廓的部件（构建前预览）",list.Length==0?"没有支持的网格。":string.Join("\n",list),"确定");
            }
        }
    }
}
