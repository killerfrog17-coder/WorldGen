using System;
using System.Collections.Generic;
using System.Threading;

namespace WorldGen.Core
{
    public enum VolcanoForm { Shield, Caldera, WeatheredShield }

    /// <summary>Derived edifices attached to existing geological hotspot trails.
    /// Only affected coarse faces have candidates. No extra simulated entities.</summary>
    public sealed class VolcanicTerrain
    {
        public sealed class Volcano
        {
            public int FeatureId,AgeMyr;
            public VolcanoForm Form;
            public V Center,Axis,Side;
            public double RadiusM,HeightM,BaseM,CraterFraction,CraterDepth,Phase;
            public double Coordinate(V p,double radius)=>(p-Center).Length*radius/RadiusM;
            public double Target(double r,double angle)
            {
                double cone=Math.Pow(Math.Max(0,1-r),1.35);
                double bowl=Math.Max(0,1-Math.Pow(r/CraterFraction,2));
                double erosion=(.018+.065*(1-Math.Exp(-AgeMyr/5d)))*Math.Sin(Math.PI*Math.Min(1,r));
                double flutes=erosion*(Math.Sin(angle*9+Phase)+.35*Math.Sin(angle*17-Phase));
                return BaseM+HeightM*(cone-CraterDepth*bowl*bowl-flutes);
            }
        }
        readonly int[][] faceCandidates;
        readonly double radius;
        public readonly Volcano[] Forms;
        public int IndexedFaces {get;}
        public VolcanicTerrain(World world,double scale,CellLocator locator,CancellationToken token=default)
        {
            radius=world.Recipe.RadiusKm*1000*scale;
            var forms=new List<Volcano>();var cells=new Dictionary<int,List<int>>();
            foreach(var feature in world.Features)
            {
                token.ThrowIfCancellationRequested();
                if(feature.Kind!=FeatureKind.Hotspot&&feature.Kind!=FeatureKind.RidgeHotspot&&feature.Kind!=FeatureKind.IslandChain)continue;
                if(feature.WidthM<=0||feature.AmplitudeM<=0)continue;
                int nearest=locator.Nearest(feature.Center);double age=Math.Max(0,feature.AgeMyr);
                var random=new Rng(world.Recipe.Seed,"volcanic-local-edifice-1",feature.Id);
                double width=Math.Max(600,feature.WidthM*scale*(.48+.15*random.Unit()));
                double height=Math.Min(1800,Math.Min(feature.AmplitudeM*.45,width*(.11+.045*random.Unit())))*Math.Exp(-age/12);
                // Do not resurrect old submerged seamounts as new islands.
                if(world.ElevationM[nearest]+height< -50||height<35)continue;
                V axis=(feature.Axis-feature.Center*V.Dot(feature.Axis,feature.Center)).Unit;
                if(axis.Length<.5)axis=V.Tangent(feature.Center,new V(0,0,1));
                if(axis.Length<.5)axis=V.Tangent(feature.Center,new V(1,0,0));
                bool caldera=feature.Kind==FeatureKind.Hotspot&&random.Unit()<.35;
                var form=new Volcano{FeatureId=feature.Id,AgeMyr=feature.AgeMyr,Center=feature.Center,Axis=axis,Side=V.Cross(feature.Center,axis).Unit,
                    RadiusM=width,HeightM=height,BaseM=world.ElevationM[nearest]-height*.30,
                    CraterFraction=caldera?.20+.10*random.Unit():.07+.055*random.Unit(),
                    CraterDepth=(caldera?.67:.34)*Math.Exp(-age/5),Phase=random.Unit()*Math.PI*2,
                    Form=age>6?VolcanoForm.WeatheredShield:caldera?VolcanoForm.Caldera:VolcanoForm.Shield};
                int index=forms.Count;forms.Add(form);
                var affected=new HashSet<int>(feature.Cells);affected.Add(nearest);
                foreach(int cell in feature.Cells)foreach(int adjacent in world.Surface.Cells[cell].Neighbors)affected.Add(adjacent);
                foreach(int adjacent in world.Surface.Cells[nearest].Neighbors)affected.Add(adjacent);
                foreach(int cell in affected)
                {if(!cells.TryGetValue(cell,out var list)){list=new List<int>();cells.Add(cell,list);}list.Add(index);}
            }
            Forms=forms.ToArray();faceCandidates=new int[world.Surface.Triangles.Length/3][];
            for(int face=0;face<faceCandidates.Length;face++)
            {
                if((face&8191)==0)token.ThrowIfCancellationRequested();List<int> selected=null;
                for(int k=0;k<3;k++)if(cells.TryGetValue(world.Surface.Triangles[face*3+k],out var list))
                    foreach(int id in list){if(selected==null)selected=new List<int>();if(!selected.Contains(id))selected.Add(id);}
                if(selected!=null){selected.Sort();faceCandidates[face]=selected.ToArray();IndexedFaces++;}
            }
        }
        public double Apply(V point,int face,double ground,out double exposure)
        {
            exposure=0;var candidates=faceCandidates[face];if(candidates==null)return ground;
            foreach(int id in candidates)
            {
                var form=Forms[id];double r=form.Coordinate(point,radius);if(r>=1)continue;
                double blend=1-RiverscapeKernel.Smooth((r-.62)/.38);
                double angle=Math.Atan2(Dot(point-form.Center,form.Side),Dot(point-form.Center,form.Axis));
                ground+=(form.Target(r,angle)-ground)*blend;
                exposure=Math.Max(exposure,Math.Exp(-Math.Max(0,form.AgeMyr)/.6)*(1-RiverscapeKernel.Smooth((r-.35)/.45)));
            }
            return ground;
        }
        static double Dot(V a,V b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
    }
}
