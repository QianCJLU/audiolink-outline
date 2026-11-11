using UnityEngine;
using VRC.SDKBase;

namespace AudioLinkOutline
{
    [DisallowMultipleComponent]
    [AddComponentMenu("audiolink outline/Settings")]
    public sealed class AudioLinkOutlineSettings : MonoBehaviour,IEditorOnly
    {
        public const int MinimumTriangleBudget=1000, MaximumTriangleBudget=200000, DefaultTriangleBudget=70000;
        [Range(MinimumTriangleBudget,MaximumTriangleBudget)] public int shellTriangleBudget=DefaultTriangleBudget;
        [Tooltip("仅为用户明确选择的部件生成轮廓。空列表不自动选择任何部件。")]
        public SkinnedMeshRenderer[] renderers=new SkinnedMeshRenderer[0];
        [Tooltip("构建时自动避开角色现有遮罩位，无需手动猜测。")]
        [Range(1,128)] public int stencilBit=64;
        public Vector2 brightnessLimits=new Vector2(0,1),widthLimits=new Vector2(0,1),waveWidthLimits=new Vector2(0,1),hueLimits=new Vector2(0,1),breathLengthLimits=new Vector2(0,1),breathFrequencyLimits=new Vector2(0,1);
        public Vector2 horizontalLimits=new Vector2(-1,1),verticalLimits=new Vector2(-1,1);
        public Vector2 ControlRange(int index){
            Vector2 value=index==1?brightnessLimits:index==2?widthLimits:index==3?hueLimits:index==9?waveWidthLimits:index==10?breathLengthLimits:index==11?breathFrequencyLimits:new Vector2(0,1);
            return new Vector2(Mathf.Clamp01(Mathf.Min(value.x,value.y)),Mathf.Clamp01(Mathf.Max(value.x,value.y)));
        }
        public Vector2 OffsetRange(int axis){var value=axis==0?horizontalLimits:verticalLimits;return new Vector2(Mathf.Clamp(Mathf.Min(value.x,value.y),-1,0),Mathf.Clamp(Mathf.Max(value.x,value.y),0,1));}
        [HideInInspector] public Material outline;
    }
}
