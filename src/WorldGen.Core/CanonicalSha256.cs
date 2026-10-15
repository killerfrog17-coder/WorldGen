using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace WorldGen.Core
{
    // Provider selection only: canonical bytes and SHA-256 stay identical.
    // Unity's Windows Mono SHA256/Cng/CSP facades all use a slow managed backend.
    // Other hosts retain their normal BCL implementation; unavailable CNG falls back.
    internal static class CanonicalSha256
    {
        static readonly bool WindowsMono=Environment.OSVersion.Platform==PlatformID.Win32NT&&Type.GetType("Mono.Runtime")!=null;
        public static SHA256 Create()
        {
            if(WindowsMono)
            {
                try{return new WindowsHash();}
                catch(DllNotFoundException){}
                catch(EntryPointNotFoundException){}
                catch(CryptographicException){}
                catch(PlatformNotSupportedException){}
            }
            return SHA256.Create();
        }
        sealed class AlgorithmHandle:SafeHandleZeroOrMinusOneIsInvalid
        {
            public AlgorithmHandle(IntPtr value):base(true){SetHandle(value);}
            protected override bool ReleaseHandle()=>BCryptCloseAlgorithmProvider(handle,0)>=0;
        }
        sealed class HashHandle:SafeHandleZeroOrMinusOneIsInvalid
        {
            public HashHandle(IntPtr value):base(true){SetHandle(value);}
            protected override bool ReleaseHandle()=>BCryptDestroyHash(handle)>=0;
        }
        sealed class WindowsHash:SHA256
        {
            AlgorithmHandle algorithm;HashHandle hash;
            public WindowsHash()
            {
                HashSizeValue=256;Check(BCryptOpenAlgorithmProvider(out var handle,"SHA256",null,0));algorithm=new AlgorithmHandle(handle);
                try{Initialize();}catch{algorithm.Dispose();throw;}
            }
            public override void Initialize()
            {
                hash?.Dispose();Check(BCryptCreateHash(algorithm,out var handle,IntPtr.Zero,0,IntPtr.Zero,0,0));hash=new HashHandle(handle);
            }
            protected override void HashCore(byte[] array,int offset,int count)
            {
                if(count==0)return;
                var pinned=GCHandle.Alloc(array,GCHandleType.Pinned);
                try{Check(BCryptHashData(hash,IntPtr.Add(pinned.AddrOfPinnedObject(),offset),count,0));}finally{pinned.Free();}
            }
            protected override byte[] HashFinal(){var bytes=new byte[32];Check(BCryptFinishHash(hash,bytes,bytes.Length,0));return bytes;}
            protected override void Dispose(bool disposing){if(disposing){hash?.Dispose();algorithm?.Dispose();}base.Dispose(disposing);}
        }
        static void Check(int status){if(status<0)throw new CryptographicException("Windows SHA-256 failed: 0x"+status.ToString("X8"));}
        [DllImport("bcrypt.dll",CharSet=CharSet.Unicode),DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        static extern int BCryptOpenAlgorithmProvider(out IntPtr algorithm,string name,string implementation,int flags);
        [DllImport("bcrypt.dll"),DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        static extern int BCryptCreateHash(AlgorithmHandle algorithm,out IntPtr hash,IntPtr storage,int storageLength,IntPtr secret,int secretLength,int flags);
        [DllImport("bcrypt.dll"),DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        static extern int BCryptHashData(HashHandle hash,IntPtr bytes,int count,int flags);
        [DllImport("bcrypt.dll"),DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        static extern int BCryptFinishHash(HashHandle hash,[Out]byte[] bytes,int count,int flags);
        [DllImport("bcrypt.dll"),DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        static extern int BCryptDestroyHash(IntPtr hash);
        [DllImport("bcrypt.dll"),DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        static extern int BCryptCloseAlgorithmProvider(IntPtr algorithm,int flags);
    }
}
