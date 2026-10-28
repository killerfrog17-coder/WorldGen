using System;
using System.Collections.Generic;
using System.Threading;

namespace WorldGen.Core
{
    public sealed partial class LocalScatter
    {
        const int FallenBranchSide=8;
        bool FallenBranchSource(ScatterKey key,int slot,LocalTerrain.LocalTerrainPatch patch,out ScatterInstance tree,out Rng random)
        {
            if(!key.Detail||slot<0||slot>=FallenBranchSide*FallenBranchSide)throw new ArgumentOutOfRangeException();
            ulong id=key.Id(4,slot);random=new Rng(seed,"fallen-tree-branch-1",unchecked((int)Rng.Mix((uint)id^(uint)(id>>32))));
            double u=(slot%FallenBranchSide+.12+.76*random.Unit())/FallenBranchSide,v=(slot/FallenBranchSide+.12+.76*random.Unit())/FallenBranchSide;
            if(!TryFaunaTree(Position(key,u,v),patch,out tree))return false;
            var form=new TreeMorphology(tree.Variant).Form;
            // Palms, tree ferns and succulent stems do not shed these woody limbs.
            return form==TreeGrowthForm.BroadCrown||form==TreeGrowthForm.NeedleCrown;
        }
        public bool TryFallenBranchParent(ScatterKey key,int slot,LocalTerrain.LocalTerrainPatch patch,out ScatterInstance tree)
            =>FallenBranchSource(key,slot,patch,out tree,out _);
        void AddFallenBranches(ScatterKey key,LocalTerrain.LocalTerrainPatch patch,List<ScatterInstance> result,CancellationToken token)
        {
            int anchored=0;
            for(int slot=0;slot<FallenBranchSide*FallenBranchSide;slot++)
            {
                token.ThrowIfCancellationRequested();
                if(!FallenBranchSource(key,slot,patch,out var tree,out var random))continue;
                double branchYield=D.Clamp(tree.HeightM/8,.18,.85)*D.Clamp(tree.WidthM/3,.3,1);
                if(random.Unit()>branchYield)continue;
                V east=V.Cross(Math.Abs(tree.Position.Z)<.99?new V(0,0,1):new V(0,1,0),tree.Position).Unit,north=V.Cross(tree.Position,east).Unit;
                double angle=random.Unit()*Math.PI*2,radius=.25+Math.Sqrt(random.Unit())*Math.Min(6,Math.Max(1,tree.WidthM*.55));
                var at=(tree.Position+(east*Math.Cos(angle)+north*Math.Sin(angle))*(radius/(terrain.RadiusM*scale))).Unit;
                if(PlaceFallenBranch(key.Id(4,slot),at,patch,ref random,result))anchored++;
            }
            // Roughly one general woodland piece per nineteen tree-derived pieces.
            // A tiny residual allows an occasional stick in an actual forest gap.
            var background=new Rng(seed,"woodland-branch-background-1",unchecked((int)Rng.Mix((uint)key.Id(5,0)^(uint)(key.Id(5,0)>>32))));
            double expected=(anchored+1)/19.0;int count=(int)Math.Floor(expected);if(background.Unit()<expected-count)count++;
            for(int slot=0;slot<count;slot++)
            {
                token.ThrowIfCancellationRequested();V at=Position(key,background.Unit(),background.Unit());
                if(patch.ForestProfile(at).Coverage<.05)continue;
                PlaceFallenBranch(key.Id(5,slot),at,patch,ref background,result);
            }
        }
        bool PlaceFallenBranch(ulong id,V at,LocalTerrain.LocalTerrainPatch patch,ref Rng random,List<ScatterInstance> result)
        {
            var sample=patch.Sample(at);if(sample.Underwater||sample.ElevationM<2||sample.Biome==Biome.Ice)return false;
            V east=V.Cross(Math.Abs(at.Z)<.99?new V(0,0,1):new V(0,1,0),at).Unit,north=V.Cross(at,east).Unit;double delta=1.5/(terrain.RadiusM*scale);
            double sx=(patch.Elevation((at+east*delta).Unit)-patch.Elevation((at-east*delta).Unit))/3,sz=(patch.Elevation((at+north*delta).Unit)-patch.Elevation((at-north*delta).Unit))/3;
            if(sx*sx+sz*sz>.64)return false;
            result.Add(new ScatterInstance{Id=id,Kind=ScatterKind.Branch,Variant=(int)(random.Unit()*8),Position=at,Normal=(at-east*sx-north*sz).Unit,
                GroundM=sample.ElevationM,WidthM=D.Q(.35+1.95*random.Unit(),1000),HeightM=D.Q(.014+.035*random.Unit(),1000),YawDegrees=random.Unit()*360,Color=new V(1,1,1)});
            return true;
        }
    }
}
