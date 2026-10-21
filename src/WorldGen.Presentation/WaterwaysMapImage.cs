using System;
using System.Threading;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>Optional cartographic estimates, not a water-geometry authority.
    /// Symbol widths are exaggerated; zoom exposes smaller source channels.</summary>
    public static class WaterwaysMapImage
    {
        public static double MinimumHalfWidth(double metresPerPixel)=>Math.Max(.6,.004*Math.Max(0,metresPerPixel));
        public static TerrainMapImage Build(World world,LocalTerrain terrain,TerrainMapViewport view,int width=512,int height=256,CancellationToken token=default)
        {
            if(width<1||height<1||width>1024||height>512)throw new ArgumentOutOfRangeException(nameof(width));
            var image=new TerrainMapImage{Width=width,Height=height,View=view.Clone(),Rgba=new byte[width*height*4]};
            double radius=terrain.RadiusM*terrain.HorizontalScale,metresPerPixel=radius*Math.PI/180*view.LongitudeSpan/width*Math.Max(.08,Math.Cos(view.Latitude*Math.PI/180));
            double minimum=MinimumHalfWidth(metresPerPixel),period=width*360/view.LongitudeSpan;
            void Pixel(int x,int y,byte alpha){if(x<0||y<0||x>=width||y>=height)return;int i=(y*width+x)*4;image.Rgba[i]=35;image.Rgba[i+1]=185;image.Rgba[i+2]=255;image.Rgba[i+3]=Math.Max(image.Rgba[i+3],alpha);}
            void Project(V p,out double x,out double y){TerrainMapViewport.Degrees(p,out double lat,out double lon);x=width*(.5+TerrainMapViewport.Wrap(lon-view.Longitude)/view.LongitudeSpan);y=height*(.5+(lat-view.Latitude)/view.LatitudeSpan);}
            void Line(double ax,double ay,double bx,double by,double half)
            {
                // Pixel-bounded rasterization also handles near-polar projections.
                if(Math.Max(ax,bx)+half<0||Math.Min(ax,bx)-half>width||Math.Max(ay,by)+half<0||Math.Min(ay,by)-half>height)return;
                int x0=(int)Math.Max(0,Math.Floor(Math.Min(ax,bx)-half)),x1=(int)Math.Min(width-1,Math.Ceiling(Math.Max(ax,bx)+half));
                int y0=(int)Math.Max(0,Math.Floor(Math.Min(ay,by)-half)),y1=(int)Math.Min(height-1,Math.Ceiling(Math.Max(ay,by)+half));
                double dx=bx-ax,dy=by-ay,dd=dx*dx+dy*dy;
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                {double t=Math.Max(0,Math.Min(1,((x+.5-ax)*dx+(y+.5-ay)*dy)/Math.Max(1e-20,dd)));double ex=x+.5-ax-t*dx,ey=y+.5-ay-t*dy;if(ex*ex+ey*ey<=half*half)Pixel(x,y,235);}
            }
            // Coarse fractional-body coverage is an estimate, deliberately not
            // a claim that the local shoreline matches a whole simulation cell.
            for(int y=0;y<height;y++)
            {
                token.ThrowIfCancellationRequested();
                for(int x=0;x<width;x++)
                {int cell=terrain.NearestCell(view.Position((x+.5)/width,1-(y+.5)/height));if(!world.Hydrology.OceanConnected[cell]&&world.Hydrology.WaterBody[cell]>=0&&world.Hydrology.LakeCoverageFraction[cell]>=.5)Pixel(x,y,125);}
            }
            foreach(var c in terrain.Hydrology.Channels)
            {
                token.ThrowIfCancellationRequested();double half=RiverscapeKernel.HalfWidth(c.WidthM*c.WidthM/(4.8*4.8),terrain.HorizontalScale);
                if(half<minimum)continue;
                Project(c.A,out double ax,out double ay);Project(c.B,out double bx,out double by);
                while(bx-ax>period*.5)bx-=period;while(bx-ax< -period*.5)bx+=period;
                double pad=Math.Max(3,Math.Abs(bx-ax)*.3+Math.Abs(by-ay)*.3);
                if(Math.Max(ay,by)+pad<0||Math.Min(ay,by)-pad>height)continue;
                if(Math.Max(ax,bx)+pad<0&&Math.Max(ax,bx)+period+pad<0||Math.Min(ax,bx)-pad>width&&Math.Min(ax,bx)-period-pad>width)continue;
                int count=(int)Math.Max(4,Math.Min(96,Math.Ceiling(Math.Sqrt((bx-ax)*(bx-ax)+(by-ay)*(by-ay))/5)));
                double stroke=Math.Max(.8,Math.Min(12,half/Math.Max(.001,metresPerPixel)));
                for(int k=1;k<=count;k++)
                {
                    V p=terrain.Amplification!=null&&view.LongitudeSpan<1?terrain.Amplification.RiverPoint(c.From,k/(double)count):c.Position(k/(double)count);
                    Project(p,out double x,out double y);while(x-ax>period*.5)x-=period;while(x-ax< -period*.5)x+=period;
                    for(int wrap=-1;wrap<=1;wrap++)Line(ax+wrap*period,ay,x+wrap*period,y,stroke);
                    ax=x;ay=y;
                }
            }
            return image;
        }
    }
}
