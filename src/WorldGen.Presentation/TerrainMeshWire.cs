using System;
using System.IO;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>Local, bounded binary transport. Geometry is produced by the same
    /// Core/Presentation assemblies, never an approximate second generator.</summary>
    public static class TerrainMeshWire
    {
        public const int Version=8;
        [ThreadStatic] static byte[]? scratch;
        static byte[] Scratch=>scratch??(scratch=new byte[65536]);
        static void ArrayOut(BinaryWriter writer,Array data)
        {
            writer.Write(data.Length);int bytes=checked(data.Length*4);var buffer=Scratch;
            for(int at=0;at<bytes;){int n=Math.Min(buffer.Length,bytes-at);Buffer.BlockCopy(data,at,buffer,0,n);writer.Write(buffer,0,n);at+=n;}
        }
        static void ArrayIn(BinaryReader reader,Array target)
        {
            int bytes=checked(target.Length*4);var buffer=Scratch;
            for(int at=0;at<bytes;){int n=Math.Min(buffer.Length,bytes-at),got=0;while(got<n){int r=reader.Read(buffer,got,n-got);if(r==0)throw new EndOfStreamException();got+=r;}Buffer.BlockCopy(buffer,0,target,at,n);at+=n;}
        }
        static int Length(BinaryReader reader){int n=reader.ReadInt32();if(n<0||n>8000000)throw new InvalidDataException("Invalid terrain mesh array length.");return n;}
        static float[] Floats(BinaryReader reader){var data=new float[Length(reader)];ArrayIn(reader,data);return data;}
        static int[] Ints(BinaryReader reader){var data=new int[Length(reader)];ArrayIn(reader,data);return data;}
        static void Vector(BinaryWriter w,V v){w.Write(v.X);w.Write(v.Y);w.Write(v.Z);}
        static V Vector(BinaryReader r)=>new V{X=r.ReadDouble(),Y=r.ReadDouble(),Z=r.ReadDouble()};
        public static void WriteRequest(BinaryWriter w,LocalTerrainFrame frame,double east,double north,double size,int segments,double[] edges)
        {
            w.Write(Version);Vector(w,frame.Origin);Vector(w,frame.East);Vector(w,frame.North);w.Write(frame.RadiusM);w.Write(frame.AltitudeM);w.Write(frame.HorizontalScale);
            var intervals=edges??new[]{size/segments,size/segments,size/segments,size/segments};TerrainEdges.Validate(intervals);
            w.Write(east);w.Write(north);w.Write(size);w.Write(segments);w.Write(intervals.Length);foreach(double value in intervals)w.Write(value);w.Flush();
        }
        public static LocalTerrainFrame ReadRequest(BinaryReader r,out double east,out double north,out double size,out int segments,out double[] edges)
        {
            if(r.ReadInt32()!=Version)throw new InvalidDataException("Terrain transport version mismatch.");
            var origin=Vector(r);var e=Vector(r);var n=Vector(r);double radius=r.ReadDouble(),altitude=r.ReadDouble(),scale=r.ReadDouble();
            east=r.ReadDouble();north=r.ReadDouble();size=r.ReadDouble();segments=r.ReadInt32();int count=r.ReadInt32();if(count<4||count>16388||(count-4)%4!=0)throw new InvalidDataException("Invalid terrain edge interval count.");edges=new double[count];for(int i=0;i<count;i++)edges[i]=r.ReadDouble();TerrainEdges.Validate(edges);
            return LocalTerrainFrame.FromExactBasis(origin,e,n,radius,altitude,scale);
        }
        public static void WriteMesh(BinaryWriter w,LocalTerrainMesh m)
        {
            w.Write(m.EastM);w.Write(m.NorthM);w.Write(m.SizeM);w.Write(m.MinHeightM);w.Write(m.MaxHeightM);w.Write(m.BuildMilliseconds);w.Write(m.Segments);w.Write(m.GroundVertices);
            foreach(var a in new[]{m.Positions,m.Normals,m.Colors,m.SurfaceWeights,m.GroundWeightsB,m.Lithology,m.WaterPositions,m.WaterColors})ArrayOut(w,a);
            ArrayOut(w,m.Triangles);ArrayOut(w,m.WaterTriangles);ArrayOut(w,m.GroundCover);ArrayOut(w,m.MidCover);ArrayOut(w,m.ForestCover);w.Flush();
        }
        public static LocalTerrainMesh ReadMesh(BinaryReader r)
        {
            var m=new LocalTerrainMesh{EastM=r.ReadDouble(),NorthM=r.ReadDouble(),SizeM=r.ReadDouble(),MinHeightM=r.ReadDouble(),MaxHeightM=r.ReadDouble(),BuildMilliseconds=r.ReadDouble(),Segments=r.ReadInt32(),GroundVertices=r.ReadInt32()};
            if(m.Segments<4||m.Segments>128||m.GroundVertices<0||m.GroundVertices>16641)throw new InvalidDataException("Invalid terrain mesh dimensions.");
            m.Positions=Floats(r);m.Normals=Floats(r);m.Colors=Floats(r);m.SurfaceWeights=Floats(r);m.GroundWeightsB=Floats(r);m.Lithology=Floats(r);m.WaterPositions=Floats(r);m.WaterColors=Floats(r);m.Triangles=Ints(r);m.WaterTriangles=Ints(r);
            m.GroundCover=Floats(r);m.MidCover=Floats(r);m.ForestCover=Floats(r);int vertices=m.Positions.Length/3;
            if(m.ForestCover.Length!=0&&m.ForestCover.Length!=m.GroundVertices*8)throw new InvalidDataException("Incomplete forest cover grid.");
            if(m.MidCover.Length!=0&&m.MidCover.Length!=m.GroundVertices*6)throw new InvalidDataException("Incomplete mid-height cover grid.");
            if(m.GroundCover.Length!=0&&m.GroundCover.Length!=m.GroundVertices*16)throw new InvalidDataException("Incomplete ground-cover grid.");
            if(m.Positions.Length%3!=0||m.Normals.Length!=m.Positions.Length||m.Colors.Length!=vertices*4||m.SurfaceWeights.Length!=vertices*4||m.GroundWeightsB.Length!=vertices*4||m.Lithology.Length!=vertices*6||m.WaterColors.Length!=m.WaterPositions.Length/3*4)
                throw new InvalidDataException("Incomplete terrain mesh attributes.");
            return m;
        }
        public static void WriteScatterKey(BinaryWriter w,ScatterKey key)
        {w.Write(Version);w.Write(key.Face);w.Write(key.X);w.Write(key.Y);w.Write(key.Detail);w.Flush();}
        public static ScatterKey ReadScatterKey(BinaryReader r)
        {
            if(r.ReadInt32()!=Version)throw new InvalidDataException("Scatter transport version mismatch.");
            var key=new ScatterKey(r.ReadInt32(),r.ReadInt32(),r.ReadInt32(),r.ReadBoolean());
            if(key.Face<0||key.Face>5||key.X<0||key.Y<0||key.X>1048576||key.Y>1048576)throw new InvalidDataException("Invalid scatter address.");return key;
        }
        public static void WriteScatter(BinaryWriter w,ScatterPatch patch)
        {
            Vector(w,patch.Center);w.Write(patch.Instances.Length);
            foreach(var p in patch.Instances){w.Write(p.Id);w.Write((int)p.Kind);w.Write(p.Variant);Vector(w,p.Position);Vector(w,p.Normal);Vector(w,p.Color);w.Write(p.GroundM);w.Write(p.HeightM);w.Write(p.WidthM);w.Write(p.YawDegrees);}w.Flush();
        }
        public static ScatterPatch ReadScatter(BinaryReader r,ScatterKey key)
        {
            V center=Vector(r);int count=r.ReadInt32();if(count<0||count>60000)throw new InvalidDataException("Invalid scatter count.");
            var instances=new ScatterInstance[count];for(int i=0;i<count;i++)instances[i]=new ScatterInstance{Id=r.ReadUInt64(),Kind=(ScatterKind)r.ReadInt32(),Variant=r.ReadInt32(),Position=Vector(r),Normal=Vector(r),Color=Vector(r),GroundM=r.ReadDouble(),HeightM=r.ReadDouble(),WidthM=r.ReadDouble(),YawDegrees=r.ReadDouble()};
            return new ScatterPatch{Key=key,Center=center,Instances=instances};
        }
        public static void WriteForestKey(BinaryWriter w,ForestPageKey k)
        {w.Write(Version);w.Write(k.Face);w.Write(k.X);w.Write(k.Y);w.Write(k.Level);w.Flush();}
        public static ForestPageKey ReadForestKey(BinaryReader r)
        {if(r.ReadInt32()!=Version)throw new InvalidDataException("Forest wire version mismatch");var k=new ForestPageKey(r.ReadInt32(),r.ReadInt32(),r.ReadInt32(),r.ReadInt32());if(k.Face<0||k.Face>5||k.X<0||k.Y<0||k.Level<0||k.Level>15)throw new InvalidDataException("Invalid forest key");return k;}
        public static void WriteForest(BinaryWriter w,ForestPage page)
        {
            var trees=new ScatterInstance[page.Roots.Length];for(int i=0;i<trees.Length;i++)trees[i]=page.Roots[i].Tree;
            WriteScatter(w,new ScatterPatch{Instances=trees});w.Write(page.Probes);
            foreach(var p in page.Roots){w.Write(p.CellX);w.Write(p.CellY);w.Write(p.CrownScale);w.Write(p.Population);}w.Flush();
        }
        public static ForestPage ReadForest(BinaryReader r,ForestPageKey key)
        {
            var trees=ReadScatter(r,default).Instances;if(trees.Length>LocalScatter.ForestPageSide*LocalScatter.ForestPageSide)throw new InvalidDataException("Oversized forest page");
            var page=new ForestPage{Key=key,Probes=r.ReadInt32(),Roots=new ForestRepresentative[trees.Length]};
            for(int i=0;i<trees.Length;i++)page.Roots[i]=new ForestRepresentative{Tree=trees[i],CellX=r.ReadInt32(),CellY=r.ReadInt32(),CrownScale=r.ReadDouble(),Population=r.ReadInt32()};return page;
        }
    }
}
