using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace AudioLinkOutline.Editor
{
public static class AudioLinkOutlineMesh
{
    sealed class Cell
    {
        public List<int> members=new List<int>();
        public Vector3 center,normal;
        public int representative;
    }
    sealed class Reduced { public Cell[] cells;public int[] triangles;public int[] slots; }
    static Reduced Cluster(Vector3[] vertices,Vector3[] normals,BoneWeight[] weights,int[] indices,int[] slots,int[] islands,float size)
    {
        var table=new Dictionary<(Vector3Int,int,int,int),int>();var cells=new List<Cell>();var map=new int[vertices.Length];
        foreach(int i in indices.Distinct())
        {
            var v=vertices[i];var n=normals[i];var abs=new Vector3(Mathf.Abs(n.x),Mathf.Abs(n.y),Mathf.Abs(n.z));
            int axis=abs.x>=abs.y&&abs.x>=abs.z?0:(abs.y>=abs.z?1:2);
            var w=weights[i];int bone=w.boneIndex0;float most=w.weight0;
            if(w.weight1>most){bone=w.boneIndex1;most=w.weight1;}if(w.weight2>most){bone=w.boneIndex2;most=w.weight2;}if(w.weight3>most)bone=w.boneIndex3;
            var key=(new Vector3Int(Mathf.FloorToInt(v.x/size),Mathf.FloorToInt(v.y/size),Mathf.FloorToInt(v.z/size)),axis*2+(n[axis]<0?1:0),bone,islands[i]);
            if(!table.TryGetValue(key,out int cell)){cell=cells.Count;table.Add(key,cell);cells.Add(new Cell());}
            map[i]=cell;cells[cell].members.Add(i);cells[cell].center+=v;cells[cell].normal+=n;
        }
        foreach(var c in cells)
        {
            c.center/=c.members.Count;c.normal.Normalize();
            c.representative=c.members.OrderByDescending(i=>Vector3.Dot(vertices[i]-c.center,c.normal)).First();
            c.center=vertices[c.representative];c.normal=normals[c.representative].normalized;
        }
        var triangles=new List<int>();var unique=new HashSet<(int,int,int,int)>();var outputSlots=new List<int>();
        for(int i=0;i<indices.Length;i+=3)
        {
            int a=map[indices[i]],b=map[indices[i+1]],c=map[indices[i+2]];
            if(a==b||b==c||a==c||Vector3.Cross(cells[b].center-cells[a].center,cells[c].center-cells[a].center).sqrMagnitude<1e-14f)continue;
            int lo=Math.Min(a,Math.Min(b,c)),hi=Math.Max(a,Math.Max(b,c));
            if(!unique.Add((lo,a+b+c-lo-hi,hi,slots[i/3])))continue;
            triangles.Add(a);triangles.Add(b);triangles.Add(c);outputSlots.Add(slots[i/3]);
        }
        var used=triangles.Distinct().OrderBy(i=>i).ToArray();var compact=used.Select((id,i)=>(id,i)).ToDictionary(p=>p.id,p=>p.i);
        return new Reduced{cells=used.Select(i=>cells[i]).ToArray(),triangles=triangles.Select(i=>compact[i]).ToArray(),slots=outputSlots.ToArray()};
    }
    public static bool SupportedSurface(Material material)=>material&&material.shader&&
        (material.HasProperty("_MainTex")||material.HasProperty("_BaseMap"));
    public static int[] Indices(SkinnedMeshRenderer source)=>Enumerable.Range(0,source.sharedMesh.subMeshCount)
        .Where(i=>i<source.sharedMaterials.Length&&SupportedSurface(source.sharedMaterials[i])&&source.sharedMesh.GetTopology(i)==UnityEngine.MeshTopology.Triangles)
        .SelectMany(i=>source.sharedMesh.GetTriangles(i)).ToArray();

    public static Mesh Create(SkinnedMeshRenderer source,int budget,HashSet<string> animated,out Transform[] bones)
    {
        var mesh=source.sharedMesh;var v=mesh.vertices;var normals=mesh.normals;var weights=mesh.boneWeights;var indices=Indices(source);
        if(indices.Length==0){bones=Array.Empty<Transform>();return null;}
        if(normals.Length!=v.Length||weights.Length!=v.Length)throw new Exception("Missing skin data: "+source.name);
        // Disconnected hair cards and UV seams must not collapse into one surface.
        var islands=Enumerable.Range(0,v.Length).ToArray();
        Func<int,int> find=null;find=i=>{while(islands[i]!=i){islands[i]=islands[islands[i]];i=islands[i];}return i;};
        for(int i=0;i<indices.Length;i+=3){int a=find(indices[i]);islands[find(indices[i+1])]=a;islands[find(indices[i+2])]=a;}
        for(int i=0;i<islands.Length;i++)islands[i]=find(i);
        var slots=Enumerable.Range(0,mesh.subMeshCount).Where(i=>i<source.sharedMaterials.Length&&SupportedSurface(source.sharedMaterials[i])&&mesh.GetTopology(i)==UnityEngine.MeshTopology.Triangles).SelectMany(i=>Enumerable.Repeat(i,mesh.GetTriangles(i).Length/3)).ToArray();
        var dv=new Vector3[v.Length];var dn=new Vector3[v.Length];var dt=new Vector3[v.Length];
        // Bake static customization but retain exactly the weights and morph deltas
        // associated with the selected representative vertex across bending joints.
        for(int shape=0;shape<mesh.blendShapeCount;shape++)
        {
            if(animated.Contains(mesh.GetBlendShapeName(shape)))continue;
            float value=source.GetBlendShapeWeight(shape);if(Mathf.Abs(value)<0.00001f)continue;
            int frames=mesh.GetBlendShapeFrameCount(shape);
            int upper=0;while(upper<frames-1&&mesh.GetBlendShapeFrameWeight(shape,upper)<value)upper++;
            int lower=upper-1;float lowerWeight=lower<0?0:mesh.GetBlendShapeFrameWeight(shape,lower);
            float upperWeight=mesh.GetBlendShapeFrameWeight(shape,upper);
            float t=(value-lowerWeight)/Mathf.Max(0.00001f,upperWeight-lowerWeight);
            var lowD=new Vector3[v.Length];var lowN=new Vector3[v.Length];
            if(lower>=0)mesh.GetBlendShapeFrameVertices(shape,lower,lowD,lowN,dt);
            mesh.GetBlendShapeFrameVertices(shape,upper,dv,dn,dt);
            for(int i=0;i<v.Length;i++){v[i]+=Vector3.LerpUnclamped(lowD[i],dv[i],t);normals[i]+=Vector3.LerpUnclamped(lowN[i],dn[i],t);}
        }
        var bounds=new Bounds(v[indices[0]],Vector3.zero);foreach(int i in indices)bounds.Encapsulate(v[i]);
        float low=0.000001f,high=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
        var best=Cluster(v,normals,weights,indices,slots,islands,low);
        if(best.triangles.Length/3>budget)
        {
            best=Cluster(v,normals,weights,indices,slots,islands,high);
            for(int iteration=0;iteration<22;iteration++)
            {
                float cell=(low+high)*0.5f;var candidate=Cluster(v,normals,weights,indices,slots,islands,cell);
                if(candidate.triangles.Length/3>budget)low=cell;
                else{high=cell;if(candidate.triangles.Length>best.triangles.Length)best=candidate;}
            }
        }
        if(best.triangles.Length==0||best.triangles.Length/3>budget)throw new Exception("Cannot meet shell budget on "+source.name);
        var result=new Mesh{name=source.name+" [audiolink outline Shell]",indexFormat=best.cells.Length>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16};
        result.vertices=best.cells.Select(c=>c.center).ToArray();result.normals=best.cells.Select(c=>c.normal).ToArray();
        var bw=best.cells.Select(c=>weights[c.representative]).ToArray();
        var boneIds=bw.SelectMany(w=>new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3}).Distinct().OrderBy(i=>i).ToArray();
        var boneMap=boneIds.Select((id,i)=>(id,i)).ToDictionary(p=>p.id,p=>p.i);
        for(int i=0;i<bw.Length;i++){var w=bw[i];w.boneIndex0=boneMap[w.boneIndex0];w.boneIndex1=boneMap[w.boneIndex1];w.boneIndex2=boneMap[w.boneIndex2];w.boneIndex3=boneMap[w.boneIndex3];bw[i]=w;}
        result.boneWeights=bw;result.bindposes=boneIds.Select(i=>mesh.bindposes[i]).ToArray();bones=boneIds.Select(i=>source.bones[i]).ToArray();
        var uv=mesh.uv;result.uv=best.cells.Select(c=>uv.Length==v.Length?uv[c.representative]:Vector2.zero).ToArray();
        result.subMeshCount=mesh.subMeshCount;
        for(int slot=0;slot<result.subMeshCount;slot++)result.SetTriangles(Enumerable.Range(0,best.slots.Length).Where(i=>best.slots[i]==slot).SelectMany(i=>best.triangles.Skip(i*3).Take(3)).ToArray(),slot,false);
        for(int shape=0;shape<mesh.blendShapeCount;shape++)
        {
            string name=mesh.GetBlendShapeName(shape);if(!animated.Contains(name))continue;
            for(int frame=0;frame<mesh.GetBlendShapeFrameCount(shape);frame++)
            {
                mesh.GetBlendShapeFrameVertices(shape,frame,dv,dn,dt);
                result.AddBlendShapeFrame(name,mesh.GetBlendShapeFrameWeight(shape,frame),best.cells.Select(c=>dv[c.representative]).ToArray(),best.cells.Select(c=>dn[c.representative]).ToArray(),null);
            }
        }
        result.RecalculateBounds();return result;
    }
}
}
