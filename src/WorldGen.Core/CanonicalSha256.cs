using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace WorldGen.Core
{
    // Provider selection only: canonical bytes and SHA-256 stay identical.
    // Unity's Windows Mono and IL2CPP facades can use a managed backend.
    // Windows uses its platform implementation; unavailable CNG falls back.
    internal static class CanonicalSha256
    {
        static readonly bool WindowsNative=Environment.OSVersion.Platform==PlatformID.Win32NT;
        // Provider configuration is immutable. The process shares this one
        // handle, never a writable hash context. Lazy also memoizes an absent
        // platform provider instead of repeatedly probing an unavailable DLL.
        static readonly Lazy<AlgorithmHandle> Provider=new Lazy<AlgorithmHandle>(OpenProvider,LazyThreadSafetyMode.ExecutionAndPublication);
        static int providerOpenAttempts,nativeHashUnavailable,reusableHashUnavailable;
        internal static int ProviderOpenAttempts=>Volatile.Read(ref providerOpenAttempts);
        internal static string Backend(SHA256 hash)=>hash is WindowsHash native?"Windows CNG (cached provider; "+(native.Reusable?"reusable hash":"legacy hash")+")":hash.GetType().FullName+" (fallback)";
        static AlgorithmHandle OpenProvider()
        {
            Interlocked.Increment(ref providerOpenAttempts);
            try{Check(BCryptOpenAlgorithmProvider(out var handle,"SHA256",null,0));return new AlgorithmHandle(handle);}
            catch(DllNotFoundException){}
            catch(EntryPointNotFoundException){}
            catch(CryptographicException){}
            catch(PlatformNotSupportedException){}
            catch(MarshalDirectiveException){}
            return null;
        }
        public static SHA256 Create()
        {
            if(WindowsNative&&Volatile.Read(ref nativeHashUnavailable)==0)
            {
                try{var provider=Provider.Value;if(provider!=null)return new WindowsHash(provider);}
                catch(DllNotFoundException){}
                catch(EntryPointNotFoundException){}
                catch(CryptographicException){}
                catch(PlatformNotSupportedException){}
                catch(MarshalDirectiveException){}
                // Interop unsupported by a runtime is a backend capability,
                // not a reason to pay its exception cost for every digest.
                Interlocked.Exchange(ref nativeHashUnavailable,1);
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
            readonly AlgorithmHandle provider;bool providerLease;
            public HashHandle(IntPtr value,AlgorithmHandle provider):base(true)
            {this.provider=provider;provider.DangerousAddRef(ref providerLease);SetHandle(value);}
            protected override bool ReleaseHandle()
            {try{return BCryptDestroyHash(handle)>=0;}finally{if(providerLease){providerLease=false;provider.DangerousRelease();}}}
        }
        sealed class WindowsHash:SHA256
        {
            readonly AlgorithmHandle algorithm;HashHandle hash;bool disposed,reusable,finished;
            internal bool Reusable=>reusable;
            public WindowsHash(AlgorithmHandle provider)
            {
                HashSizeValue=256;algorithm=provider;Initialize();
            }
            public override void Initialize()
            {
                if(disposed)throw new ObjectDisposedException(nameof(WindowsHash));
                // A reusable CNG hash is already empty after FinishHash. Keep
                // its independent handle across ComputeHash resets, including
                // thousands of dirty Merkle nodes on one SHA256 instance.
                if(reusable&&finished&&hash!=null&&!hash.IsClosed){finished=false;return;}
                // Explicit Initialize during an unfinished message must still
                // discard that message, rather than accidentally retaining it.
                hash?.Dispose();hash=null;int flags=Volatile.Read(ref reusableHashUnavailable)==0?0x20:0;
                int status=BCryptCreateHash(algorithm,out var handle,IntPtr.Zero,0,IntPtr.Zero,0,flags);
                if(flags!=0&&(status==unchecked((int)0xC00000BB)||status==unchecked((int)0xC000000D)))
                {
                    // Vista/Windows7 and providers without reusable hashes keep
                    // the preceding exact native implementation, not a failure
                    // probe on every digest or an unnecessary managed fallback.
                    Interlocked.Exchange(ref reusableHashUnavailable,1);flags=0;
                    status=BCryptCreateHash(algorithm,out handle,IntPtr.Zero,0,IntPtr.Zero,0,0);
                }
                Check(status);hash=new HashHandle(handle,algorithm);reusable=flags!=0;finished=false;
            }
            protected override void HashCore(byte[] array,int offset,int count)
            {
                if(count==0)return;
                finished=false;
                var pinned=GCHandle.Alloc(array,GCHandleType.Pinned);
                try{Check(BCryptHashData(hash,IntPtr.Add(pinned.AddrOfPinnedObject(),offset),count,0));}finally{pinned.Free();}
            }
            protected override byte[] HashFinal(){var bytes=new byte[32];Check(BCryptFinishHash(hash,bytes,bytes.Length,0));finished=true;return bytes;}
            protected override void Dispose(bool disposing){disposed=true;if(disposing)hash?.Dispose();base.Dispose(disposing);}
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
