using System;
using System.Linq;
using System.Threading;

namespace WorldGen.Core
{
    /// <summary>Explicit scenario masks: whole-plate guidance, then a constrained shore before climate.</summary>
    public static class MapShapes
    {
        public static readonly string[] Names={"Donut-v1","Crater-v1","Inland-v1","Terra-v1","AllLand-v1"};
        public static double SignedDegrees(Recipe recipe,V point,out int identity)
        {
            identity=0;string shape=recipe.LandShape;double angle=D.Angle(point,new V(1,0,0))*180/Math.PI;
            if(shape=="AllLand-v1")return 90;
            if(shape=="Donut-v1")return Math.Min(angle-35,80-angle);
            if(shape=="Crater-v1"){if(angle<9){identity=1;return 9-angle;}return angle-55;}
            if(shape=="Inland-v1")return angle-48;
            double lon=Math.Atan2(point.Y,point.X)*180/Math.PI,lat=Math.Asin(D.Clamp(point.Z,-1,1))*180/Math.PI;
            double best=-180;int identityLocal=0;
            void Ellipse(double x,double y,double rx,double ry,int label)
            {
                double dx=lon-x;dx-=360*Math.Floor((dx+180)/360);double v=(1-Math.Sqrt(dx*dx/(rx*rx)+(lat-y)*(lat-y)/(ry*ry)))*Math.Min(rx,ry);
                if(v>best){best=v;identityLocal=label;}
            }
            Ellipse(-108,43,32,31,0);Ellipse(-63,-20,18,36,0);Ellipse(-83,10,8,16,0);
            Ellipse(60,47,100,25,2);Ellipse(20,0,25,37,1);Ellipse(34,29,12,9,1);
            Ellipse(135,-25,22,15,3);Ellipse(124,-5,26,7,3);Ellipse(172,-42,6,11,3);
            identity=identityLocal;return best+Terrain.Noise(point,recipe.Seed^47631,13)*2.5;
        }
        public static void AssignPlates(World w,CancellationToken token)
        {
            var r=w.Recipe;
            foreach(var plate in w.Microplates)
            {
                token.ThrowIfCancellationRequested();var counts=new int[Math.Max(4,r.ContinentCount)];int land=0;
                foreach(int i in plate.Cells)if(SignedDegrees(r,w.Surface.Cells[i].Position,out int id)>0){counts[id%r.ContinentCount]++;land++;}
                plate.ContinentId=land*2>=plate.Cells.Length?Array.IndexOf(counts,counts.Max()):-1;
                plate.DensityKgM3=(plate.ContinentId>=0?2400:3000)+D.M(500*Rng.Value(r.Seed,"plate-density",plate.Id));
            }
            if(r.LandShape=="Crater-v1" && r.ContinentCount>1 && !w.Microplates.Any(p=>p.ContinentId==1))
            {
                var center=w.Microplates.OrderByDescending(p=>p.Centroid.X).ThenBy(p=>p.Id).First();center.ContinentId=1;
                center.DensityKgM3=2400+D.M(500*Rng.Value(r.Seed,"plate-density",center.Id));
            }
            w.Continent=w.CellMicroplate.Select(i=>w.Microplates[i].ContinentId).ToArray();
            w.ContinentalCandidateScores=new long[r.Interconnectedness];w.ContinentalScore=0;w.ContinentalCandidate=0;
            w.Metric("continent_identity_count",w.Continent.Where(i=>i>=0).Distinct().Count());
            w.Metric("continental_crust_fraction",w.Microplates.Where(p=>p.ContinentId>=0).Sum(p=>(double)p.AreaM2)/w.Microplates.Sum(p=>(double)p.AreaM2));
            w.Metric("shape_guided_allocation",1);w.Warnings.Add("Shape template determines plate land/sea assignment; interconnectedness search is not used for this map type.");
        }
        public static void ApplyTerrain(World w,CancellationToken token)
        {
            if(w.Recipe.LandShape==null)return;var h=w.ElevationM;var ocean=new bool[h.Length];
            for(int i=0;i<h.Length;i++)
            {
                if((i&1023)==0)token.ThrowIfCancellationRequested();double distance=SignedDegrees(w.Recipe,w.Surface.Cells[i].Position,out _);bool land=distance>0;
                if(land){double interior=h[i]>0?h[i]:100+Math.Abs(h[i])*.08;h[i]=D.M(Math.Max(5,Math.Min(interior,15+distance*450)));}
                else h[i]=-D.M(Math.Max(5,Math.Min(Math.Max(300,Math.Abs(h[i])),20-distance*500)));
                ocean[i]=!land;
            }
            w.Hydrology.OceanConnected=ocean;w.Landmass=Graph.Components(w.Surface,i=>!ocean[i]);
            var shore=Enumerable.Range(0,h.Length).Where(i=>w.Surface.Cells[i].Neighbors.Any(j=>ocean[i]!=ocean[j]));
            w.CoastDistanceM=Graph.Distances(w.Surface,shore,out _,token);
            w.Metric("shape_constrained_terrain",1);w.Warnings.Add("Template shorelines override tectonic coastlines; plate provenance remains available. This is an authored map layout, not a geological reconstruction.");
        }
    }
}
