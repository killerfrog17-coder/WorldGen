using System;
using System.Threading;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>Cheap source-scale preview only. Progressive tiles replace every
    /// pixel with the actual terrain evaluator; this never generates terrain.</summary>
    static class TerrainMapPreview
    {
        public static TerrainMapImage Build(LocalTerrain terrain,World? world,TerrainMapViewport view,int width,Layer layer,CancellationToken token)
        {
            int height=width/2;var result=new TerrainMapImage{View=view.Clone(),Width=width,Height=height,Rgba=new byte[width*height*4]};
            var ids=new int[width*height];var points=new V[ids.Length];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++){token.ThrowIfCancellationRequested();int i=y*width+x;points[i]=view.Position((x+.5)/width,1-(y+.5)/height);ids[i]=terrain.NearestCell(points[i]);}
            float[]? cells=world!=null&&!TerrainMapSampling.Continuous(layer)?GlobeView.CellValues(world,layer,token:token,selectedCells:ids).Colors:null;
            for(int i=0;i<ids.Length;i++)
            {
                token.ThrowIfCancellationRequested();var f=terrain.Profile(ids[i]);V color;double elevation=world==null?f.ElevationM:terrain.SourceField(points[i],layer==Layer.TectonicElevation?world.TectonicM:layer==Layer.PreErosionElevation?world.PreErosionM:world.ElevationM);
                if(cells!=null)color=new V(cells[i*3],cells[i*3+1],cells[i*3+2]);
                else if(layer==Layer.Heightmap){double v=D.Clamp((elevation+5000)/11000);color=new V(v,v,v);}
                else if(layer==Layer.Bathymetry)color=elevation<0?GlobeView.Elevation((int)elevation):new V(.32,.4,.35);
                else if(layer==Layer.ErosionDelta&&world!=null){double delta=elevation-terrain.SourceField(points[i],world.PreErosionM);color=Mix(new V(.16,.19,.21),delta>=0?new V(.93,.65,.23):new V(.2,.68,.93),Math.Abs(delta)/500);}
                else if(layer==Layer.Satellite)
                {
                    color=elevation<0?GlobeView.Elevation((int)elevation):Mix(Mix(f.Ground.SoilColor,f.Cover.Color*.9,f.Cover.Coverage/.45),f.Forest.Color,f.Forest.Coverage*.8);
                }
                else if(layer==Layer.TreeDensity)color=elevation<0?new V(.05,.15,.23):Mix(new V(.72,.68,.53),new V(.025,.32,.1),Math.Sqrt(D.Clamp(f.Forest.Coverage)));
                else color=GlobeView.Elevation((int)elevation);
                result.Rgba[i*4]=(byte)(255*D.Clamp(color.X));result.Rgba[i*4+1]=(byte)(255*D.Clamp(color.Y));result.Rgba[i*4+2]=(byte)(255*D.Clamp(color.Z));result.Rgba[i*4+3]=255;
            }
            return result;
        }
        static V Mix(V a,V b,double t){t=D.Clamp(t);return a*(1-t)+b*t;}
    }
}
