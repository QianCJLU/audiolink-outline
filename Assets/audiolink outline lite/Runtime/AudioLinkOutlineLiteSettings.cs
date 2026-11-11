using UnityEngine;
using VRC.SDKBase;
namespace AudioLinkOutlineLite {
 [DisallowMultipleComponent,AddComponentMenu("audiolink outline lite/Settings")]
 public sealed class AudioLinkOutlineLiteSettings:MonoBehaviour,IEditorOnly {
  public const int MinimumTriangleBudget=1000,MaximumTriangleBudget=200000,DefaultTriangleBudget=70000;
  public bool defaultOn=true;
  public bool defaultBreathing=false;
  [Range(0,1)] public float brightness=1,width=1,waveWidth=1,hue=0,breathLength=.5f,breathFrequency=.5f;
  public Vector2 offset=Vector2.zero;
  [Range(MinimumTriangleBudget,MaximumTriangleBudget)] public int shellTriangleBudget=DefaultTriangleBudget;
  public SkinnedMeshRenderer[] renderers=new SkinnedMeshRenderer[0];
  [Range(1,128)] public int stencilBit=64;
  [HideInInspector] public bool installationConfirmed;
  [HideInInspector] public Material outline;
 }
}
