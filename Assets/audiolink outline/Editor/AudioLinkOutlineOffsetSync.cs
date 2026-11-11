using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using nadena.dev.modular_avatar.core;
using VRC.SDK3.Avatars.Components;
namespace AudioLinkOutline.Editor {
public static class AudioLinkOutlineOffsetSync {
 public static void Configure(ModularAvatarParameters parameters){
  var inputs=AudioLinkOutlineBuild.Parameters.Skip(5).Take(4).ToArray();
  for(int i=0;i<parameters.parameters.Count;i++){var c=parameters.parameters[i];if(inputs.Contains(c.nameOrPrefix)){c.localOnly=true;c.saved=true;parameters.parameters[i]=c;}}
  foreach(string axis in AudioLinkOutlineBuild.OffsetAxes)if(!parameters.parameters.Any(p=>p.nameOrPrefix==axis))parameters.parameters.Add(new ParameterConfig{nameOrPrefix=axis,syncType=ParameterSyncType.Float,defaultValue=0,hasExplicitDefaultValue=true,localOnly=false,saved=false});
 }
 public static void Build(AnimatorController fx,AnimationClip[][] axisClips){
  foreach(string input in AudioLinkOutlineBuild.Parameters.Skip(5).Take(4))if(!fx.parameters.Any(p=>p.name==input))fx.AddParameter(input,AnimatorControllerParameterType.Float);
  if(!fx.parameters.Any(p=>p.name=="IsLocal"))fx.AddParameter("IsLocal",AnimatorControllerParameterType.Bool);
  const string tick="ALO/OffsetSyncClock";if(!fx.parameters.Any(p=>p.name==tick))fx.AddParameter(tick,AnimatorControllerParameterType.Float);
  var clock=new AnimationClip{name="Offset sync 20ms"};AnimationUtility.SetEditorCurve(clock,EditorCurveBinding.FloatCurve("",typeof(Animator),tick),AnimationCurve.Constant(0,.02f,0));
  for(int axis=0;axis<2;axis++){
   string target=AudioLinkOutlineBuild.OffsetAxes[axis];if(!fx.parameters.Any(p=>p.name==target))fx.AddParameter(target,AnimatorControllerParameterType.Float);
   var tree=new BlendTree{name="Offset axis signed",blendType=BlendTreeType.Simple1D,blendParameter=target,useAutomaticThresholds=false};for(int i=0;i<3;i++)tree.AddChild(axisClips[axis][i],i-1);
   var display=new AnimatorStateMachine{name="ALO Outline Offset Axis "+axis};var state=display.AddState("Value");state.motion=tree;display.defaultState=state;fx.AddLayer(new AnimatorControllerLayer{name=display.name,stateMachine=display,defaultWeight=1});
   string positive=AudioLinkOutlineBuild.Parameters[axis==0?6:5],negative=AudioLinkOutlineBuild.Parameters[axis==0?8:7];
   var machine=new AnimatorStateMachine{name="ALO Outline Offset Encoder "+axis};var remote=machine.AddState("Network observer");remote.motion=new AnimationClip{name="Offset encoder inactive"};machine.defaultState=remote;
   var pos=new[]{machine.AddState("Positive A"),machine.AddState("Positive B")};var neg=new[]{machine.AddState("Negative A"),machine.AddState("Negative B")};
   for(int i=0;i<2;i++){
    foreach(var pair in new[]{(pos[i],positive,1f),(neg[i],negative,-1f)}){
     pair.Item1.motion=clock;var driver=pair.Item1.AddStateMachineBehaviour<VRCAvatarParameterDriver>();driver.localOnly=true;
     driver.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter{name=target,source=pair.Item2,type=VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Copy,convertRange=true,sourceMin=0,sourceMax=1,destMin=0,destMax=pair.Item3});
    }
    Link(pos[i],neg[i],negative,true,false);Link(neg[i],pos[i],negative,false,false);
    Link(pos[i],pos[1-i],negative,false,true);Link(neg[i],neg[1-i],negative,true,true);
   }
   var enterPos=Link(remote,pos[0],negative,false,false);enterPos.AddCondition(AnimatorConditionMode.If,0,"IsLocal");
   var enterNeg=Link(remote,neg[0],negative,true,false);enterNeg.AddCondition(AnimatorConditionMode.If,0,"IsLocal");
   fx.AddLayer(new AnimatorControllerLayer{name=machine.name,stateMachine=machine,defaultWeight=1});
  }
 }
 static AnimatorStateTransition Link(AnimatorState from,AnimatorState to,string negative,bool active,bool timed){
  var transition=from.AddTransition(to);transition.duration=0;transition.hasExitTime=timed;transition.exitTime=1;transition.AddCondition(active?AnimatorConditionMode.Greater:AnimatorConditionMode.Less,0.000001f,negative);return transition;
 }
}}
