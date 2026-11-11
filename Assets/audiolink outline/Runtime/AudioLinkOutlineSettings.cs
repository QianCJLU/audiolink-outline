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
        [HideInInspector] public Material outline;
    }
}
