using System;
using System.IO;

namespace WorldGen.Presentation
{
    // Transient map tiles on the authenticated terrain-worker connection; never save data.
    public static class TerrainMapWire
    {
        public static void WriteRequest(BinaryWriter w,int revision,TerrainMapViewport view,Layer layer,int width)
        {w.Write(revision);w.Write((int)layer);w.Write(width);WriteView(w,view);w.Flush();}
        public static TerrainMapViewport ReadRequest(BinaryReader r,out int revision,out Layer layer,out int width)
        {
            revision=r.ReadInt32();layer=(Layer)r.ReadInt32();width=r.ReadInt32();var view=ReadView(r);
            if(!Enum.IsDefined(typeof(Layer),layer)||width<32||width>32768)throw new InvalidDataException("Invalid map request.");return view;
        }
        static void WriteView(BinaryWriter w,TerrainMapViewport v){w.Write(v.Latitude);w.Write(v.Longitude);w.Write(v.LongitudeSpan);}
        static TerrainMapViewport ReadView(BinaryReader r)
        {
            var v=new TerrainMapViewport{Latitude=r.ReadDouble(),Longitude=r.ReadDouble(),LongitudeSpan=r.ReadDouble()};
            if(double.IsNaN(v.Latitude)||double.IsNaN(v.Longitude)||double.IsNaN(v.LongitudeSpan)||Math.Abs(v.Latitude)>90||Math.Abs(v.Longitude)>180||v.LongitudeSpan<1e-9||v.LongitudeSpan>360)throw new InvalidDataException("Invalid map viewport.");return v;
        }
        public static void WriteUpdate(BinaryWriter w,ProgressiveTerrainMap.Update u)
        {
            w.Write(u.Revision);w.Write((int)u.Layer);w.Write(u.X);w.Write(u.Y);w.Write(u.CanvasWidth);w.Write(u.CanvasHeight);w.Write(u.Preview);w.Write(u.Complete);w.Write(u.Error);w.Write(u.TileHits);w.Write(u.TilesBuilt);w.Write(u.Image!=null);
            if(u.Image!=null){w.Write(u.Image.Width);w.Write(u.Image.Height);WriteView(w,u.Image.View);w.Write(u.Image.Rgba);}
            w.Flush();
        }
        public static ProgressiveTerrainMap.Update ReadUpdate(BinaryReader r)
        {
            var u=new ProgressiveTerrainMap.Update{Revision=r.ReadInt32(),Layer=(Layer)r.ReadInt32(),X=r.ReadInt32(),Y=r.ReadInt32(),CanvasWidth=r.ReadInt32(),CanvasHeight=r.ReadInt32(),Preview=r.ReadBoolean(),Complete=r.ReadBoolean(),Error=r.ReadString(),TileHits=r.ReadInt64(),TilesBuilt=r.ReadInt64()};
            if(r.ReadBoolean())
            {
                int w=r.ReadInt32(),h=r.ReadInt32();var view=ReadView(r);
                if(w<1||w>32||h<1||h>32||u.X<0||u.Y<0||u.CanvasWidth<1||u.CanvasWidth>32768||u.CanvasHeight<1||u.CanvasHeight>16384||u.X+w>u.CanvasWidth||u.Y+h>u.CanvasHeight)throw new InvalidDataException("Invalid map tile.");
                var bytes=r.ReadBytes(w*h*4);if(bytes.Length!=w*h*4)throw new EndOfStreamException();u.Image=new TerrainMapImage{Width=w,Height=h,View=view,Rgba=bytes};
            }
            return u;
        }
    }
}
