using System;
using System.Collections.Generic;
using System.Threading;

namespace WorldGen.Core
{
    public sealed partial class LocalScatter
    {
        // One bounded authoring/settlement query, sharing the exact natural
        // placement predicates and IDs with rendering. Never a planet scan.
        public ScatterInstance[] SettlementSolids(V center,double radiusM,CancellationToken token=default)
        {
            if(radiusM<1||radiusM>1200)throw new ArgumentOutOfRangeException(nameof(radiusM));
            var east=V.Cross(Math.Abs(center.Z)<.99?new V(0,0,1):new V(0,1,0),center).Unit;var north=V.Cross(center,east).Unit;
            double physicalRadius=terrain.RadiusM*scale,limit=radiusM+32;var result=new List<ScatterInstance>();
            double Dot(V a,V b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
            foreach(var key in Around(center,limit))
            {
                token.ThrowIfCancellationRequested();double minX=double.PositiveInfinity,maxX=double.NegativeInfinity,minZ=double.PositiveInfinity,maxZ=double.NegativeInfinity;
                foreach(double u in new[]{0d,1})foreach(double v in new[]{0d,1})
                {
                    V delta=Position(key,u,v)-center;double x=Dot(delta,east)*physicalRadius,z=Dot(delta,north)*physicalRadius;
                    minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minZ=Math.Min(minZ,z);maxZ=Math.Max(maxZ,z);
                }
                double dx=Math.Max(minX,Math.Max(0,-maxX)),dz=Math.Max(minZ,Math.Max(0,-maxZ));
                if(dx*dx+dz*dz>(limit+1)*(limit+1))continue;
                foreach(var item in BuildContents(key,token,center,limit).Instances)
                {
                    bool tree=item.Kind==ScatterKind.GeneratedTree||item.Kind==ScatterKind.Broadleaf||item.Kind==ScatterKind.Conifer||item.Kind==ScatterKind.SavannaTree;
                    bool rock=item.Kind==ScatterKind.Boulder||item.Kind==ScatterKind.Outcrop||item.Kind==ScatterKind.Stone;
                    if(!tree&&!rock||tree&&item.HeightM<1.8||rock&&(item.HeightM<.35||item.WidthM<.4))continue;
                    double spread=tree?item.WidthM*(item.Kind==ScatterKind.GeneratedTree?new TreeMorphology(item.Variant).TrunkRadius:.035):item.WidthM*.6;
                    if((item.Position-center).Length*physicalRadius>radiusM+spread)continue;result.Add(item);
                }
            }
            result.Sort((a,b)=>a.Id.CompareTo(b.Id));return result.ToArray();
        }
        void AddSettlementTrees(ScatterKey key,LocalTerrain.LocalTerrainPatch patch,List<ScatterInstance> result,V center,double radius,CancellationToken token)
        {
            // Query only candidate slots that can meet the circle. The canonical
            // planting predicate still owns every identity/species/size decision;
            // a partial result MUST NOT be inserted into the complete tile cache.
            Basis(key.Face,out V normal,out V east,out V north);double d=V.Dot(center,normal),delta=radius/(terrain.RadiusM*scale);int divisions=Divisions(false);
            void Bounds(V axis,int cell,out int low,out int high)
            {
                double value=V.Dot(center,axis),min=double.PositiveInfinity,max=double.NegativeInfinity;
                foreach(double a in new[]{value-delta,value+delta})foreach(double b in new[]{d-delta,d+delta}){min=Math.Min(min,a/b);max=Math.Max(max,a/b);}
                low=Math.Max(0,(int)Math.Floor(((min+1)*.5*divisions-cell)*TreeCandidateSide)-1);
                high=Math.Min(TreeCandidateSide-1,(int)Math.Ceiling(((max+1)*.5*divisions-cell)*TreeCandidateSide)+1);
            }
            Bounds(east,key.X,out int x0,out int x1);Bounds(north,key.Y,out int y0,out int y1);
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
            {
                token.ThrowIfCancellationRequested();
                // The unjittered slot plus a whole slot diagonal is conservative
                // even at a cube corner and avoids expensive out-of-circle samples.
                var point=Position(key,(x+.5)/TreeCandidateSide,(y+.5)/TreeCandidateSide);
                double margin=2*terrain.RadiusM*scale/divisions/TreeCandidateSide*1.5;
                if((point-center).Length*terrain.RadiusM*scale>radius+margin)continue;
                if(TryTree(key,x,y,patch,out var tree))result.Add(tree);
            }
        }
    }
}
