using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

// Closing the game, including its immediate Alt+F4 path, also closes the owned
// host. Never attaches to a user-supplied/external server.
sealed class NpcOwnedJob : IDisposable
{
    IntPtr handle;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [StructLayout(LayoutKind.Sequential)] struct Basic
    {public long ProcessTime,JobTime;public uint Flags;public UIntPtr Minimum,Maximum;public uint Active;public UIntPtr Affinity;public uint Priority,Scheduling;}
    [StructLayout(LayoutKind.Sequential)] struct Io {public ulong ReadOperations,WriteOperations,OtherOperations,ReadBytes,WriteBytes,OtherBytes;}
    [StructLayout(LayoutKind.Sequential)] struct Extended {public Basic Basic;public Io Io;public UIntPtr ProcessMemory,JobMemory,PeakProcess,PeakJob;}
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr CreateJobObject(IntPtr attributes,string name);
    [DllImport("kernel32.dll")] static extern bool SetInformationJobObject(IntPtr job,int kind,ref Extended limits,uint length);
    [DllImport("kernel32.dll")] static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
#endif
    public void Attach(Process process)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        handle=CreateJobObject(IntPtr.Zero,null);var limits=new Extended{Basic=new Basic{Flags=0x2000}};
        if(handle==IntPtr.Zero||!SetInformationJobObject(handle,9,ref limits,(uint)Marshal.SizeOf<Extended>())||!AssignProcessToJobObject(handle,process.Handle))
        {Dispose();throw new InvalidOperationException("Could not bind the NPC host lifetime to the game");}
#endif
    }
    public void Dispose()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if(handle!=IntPtr.Zero){CloseHandle(handle);handle=IntPtr.Zero;}
#endif
    }
}
