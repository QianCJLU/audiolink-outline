using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace AudioLinkOutline.Editor
{
    public static class AudioLinkOutlineStencil
    {
        static readonly string[] Prefixes={"_Stencil","_OutlineStencil","_PreStencil"};
        public static int ReservedBits(Material material)
        {
            if(!material)return 0;int reserved=0;
            foreach(string prefix in Prefixes)
            {
                if(!material.HasProperty(prefix+"Ref"))continue;
                int reference=material.GetInt(prefix+"Ref")&255;
                int read=material.HasProperty(prefix+"ReadMask")?material.GetInt(prefix+"ReadMask")&255:255;
                int write=material.HasProperty(prefix+"WriteMask")?material.GetInt(prefix+"WriteMask")&255:255;
                int comparison=material.HasProperty(prefix+"Comp")?material.GetInt(prefix+"Comp"):8;
                // Disabled/Never/Always do not consume their reference for comparisons.
                if(comparison>=2&&comparison<=7)reserved|=reference&read;
                foreach(string action in new[]{"Pass","Fail","ZFail"})
                {
                    int operation=material.HasProperty(prefix+action)?material.GetInt(prefix+action):0;
                    if(operation==2)reserved|=reference&write; // Replace
                    else if(operation>=3)reserved|=write; // Increment/decrement/invert can produce any masked bit.
                }
            }
            return reserved&255;
        }
        public static int Select(int preferred,IEnumerable<Material> materials,IEnumerable<AnimationClip> clips)
        {
            if(preferred<1||preferred>128||(preferred&(preferred-1))!=0)throw new Exception("Stencil 位必须是 1/2/4/8/16/32/64/128。");
            // Renderer-wide stencil animations bypass material values. They need
            // explicit remapping, so fail before mutating the build clone.
            foreach(var clip in clips)
                if(AnimationUtility.GetCurveBindings(clip).Any(b=>b.propertyName.StartsWith("material.")&&b.propertyName.IndexOf("Stencil",StringComparison.OrdinalIgnoreCase)>=0))
                    throw new Exception("音律轮廓发现运行时 Stencil 动画："+clip.name+"。无法安全自动预留遮罩位，请先移除该动画或使用独立遮罩方案。");
            var all=materials.Where(m=>m).Distinct().ToArray();int reserved=all.Aggregate(0,(bits,m)=>bits|ReservedBits(m));
            foreach(int bit in new[]{preferred,128,64,32,16,8,4,2,1}.Distinct())if((reserved&bit)==0)return bit;
            throw new Exception("角色材质已占用全部 8 个 Stencil 位，音律轮廓无法安全预留遮罩位；未改动原材质。占用材质："+string.Join(", ",all.Where(m=>ReservedBits(m)!=0).Select(m=>m.name).Take(8)));
        }
    }
}
