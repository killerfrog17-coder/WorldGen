using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>Disposable, checksummed river fitting data. The player and its
    /// worker share this cache; neither rewrites the saved world. A file lease
    /// prevents both processes doing the same cold fitting simultaneously.</summary>
    public static class LocalTerrainDerivationCache
    {
        // Bump when fitting coefficients/algorithm change, as well as the model
        // version when the public terrain geometry contract changes.
        public const string Version="river-bank-fit-20261029-1";
        const int Magic=0x57475244;
        public static string DefaultDirectory=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WorldGen","DerivedTerrain");
        public static string FileFor(World world,double scale,string directory)
        {
            string? source=world.Stages.LastOrDefault()?.Hash;if(string.IsNullOrWhiteSpace(source))source=StateHash.Compute(world);
            // An exact bit representation is stable across Unity Mono, IL2CPP
            // and CoreCLR; shortest round-trip decimal formatting is not a
            // cross-runtime cache identity contract.
            string scaleKey=BitConverter.DoubleToInt64Bits(scale).ToString("X16",CultureInfo.InvariantCulture);
            using var hash=SHA256.Create();var key=hash.ComputeHash(Encoding.UTF8.GetBytes(LocalTerrain.Version+"|"+Version+"|"+scaleKey+"|"+source));
            return Path.Combine(directory,BitConverter.ToString(key).Replace("-","").ToLowerInvariant()+".river");
        }
        public static LocalTerrain Create(World world,CancellationToken token=default,double horizontalScale=1,string? directory=null)
        {
            token.ThrowIfCancellationRequested();directory=directory??DefaultDirectory;string file=FileFor(world,horizontalScale,directory);int count=world.Surface.Cells.Length;
            double[]? cached=Read(file,count);if(cached!=null)return new LocalTerrain(world,token,horizontalScale,cached);
            FileStream? lease=null;
            try
            {
                Directory.CreateDirectory(directory);var watch=Stopwatch.StartNew();
                while(lease==null&&watch.Elapsed.TotalSeconds<110)
                {
                    token.ThrowIfCancellationRequested();
                    try{lease=new FileStream(file+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
                    catch(IOException){token.WaitHandle.WaitOne(100);}
                }
                if(lease==null)return new LocalTerrain(world,token,horizontalScale);
                cached=Read(file,count);
                if(cached!=null){lease.Dispose();lease=null;return new LocalTerrain(world,token,horizontalScale,cached);}
                var terrain=new LocalTerrain(world,token,horizontalScale);
                if(terrain.Amplification!=null)Write(file,terrain.Amplification.ExportRiverDatums());return terrain;
            }
            catch(UnauthorizedAccessException){return new LocalTerrain(world,token,horizontalScale);}
            catch(IOException){return new LocalTerrain(world,token,horizontalScale);}
            finally{lease?.Dispose();}
        }
        static double[]? Read(string file,int count)
        {
            try
            {
                using var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read);using var reader=new BinaryReader(stream);
                if(reader.ReadInt32()!=Magic||reader.ReadInt32()!=count||stream.Length!=40L+count*8L)return null;
                byte[] digest=reader.ReadBytes(32),bytes=reader.ReadBytes(checked(count*8));using var sha=SHA256.Create();if(!sha.ComputeHash(bytes).SequenceEqual(digest))return null;
                var result=new double[count];Buffer.BlockCopy(bytes,0,result,0,bytes.Length);return result.All(double.IsFinite)?result:null;
            }
            catch(IOException){return null;}catch(UnauthorizedAccessException){return null;}catch(OverflowException){return null;}
        }
        static void Write(string file,double[] values)
        {
            string temporary=file+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                var bytes=new byte[checked(values.Length*8)];Buffer.BlockCopy(values,0,bytes,0,bytes.Length);using var sha=SHA256.Create();
                using(var writer=new BinaryWriter(File.Create(temporary))){writer.Write(Magic);writer.Write(values.Length);writer.Write(sha.ComputeHash(bytes));writer.Write(bytes);}
                if(File.Exists(file))File.Replace(temporary,file,null);else File.Move(temporary,file);
            }
            catch(IOException){}catch(UnauthorizedAccessException){}
            finally{try{if(File.Exists(temporary))File.Delete(temporary);}catch(IOException){}catch(UnauthorizedAccessException){}}
        }
    }
}
