param([switch]$Isolated,[int]$Detail=2000000,[string]$PlayerFolder='player-final')
$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
$validationRoot=if($Isolated){Join-Path $repoRoot 'artifacts/unity-v2'}else{$repoRoot}
$player=Join-Path $validationRoot ('artifacts/'+$PlayerFolder+'/WorldGen.exe')
$output=Join-Path $repoRoot 'artifacts/unity-ui-v3'
New-Item -ItemType Directory -Force $output | Out-Null
$report=Join-Path $output ('detail-'+$Detail+'.json')
$log=Join-Path $output ('detail-'+$Detail+'.log')
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WorldGenWindowProbe {
 public delegate bool Callback(IntPtr window,IntPtr value);
 [DllImport("user32.dll")] public static extern bool EnumWindows(Callback callback,IntPtr value);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
 [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr w,IntPtr l,uint flags,uint timeout,out IntPtr result);
 public struct Rect {public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window,out Rect rect);
 public static IntPtr Find(uint pid) { IntPtr found=IntPtr.Zero;EnumWindows((w,v)=>{uint p;GetWindowThreadProcessId(w,out p);Rect r;GetWindowRect(w,out r);if(p==pid && r.Right-r.Left>300 && r.Bottom-r.Top>300){found=w;return false;}return true;},IntPtr.Zero);return found; }
}
'@
$run=Start-Process -FilePath $player -ArgumentList @('-force-d3d11','-screen-fullscreen','0','--worldgen-detail',$Detail,'--worldgen-ui-check',('"'+$report+'"'),'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
$misses=0;$samples=0;[long]$maxMs=0;[long]$peakMemory=0
while(!$run.HasExited){
    $run.Refresh();$peakMemory=[Math]::Max($peakMemory,$run.WorkingSet64)
    $handle=[WorldGenWindowProbe]::Find($run.Id)
    if($handle -ne [IntPtr]::Zero){
        $result=[IntPtr]::Zero;$timer=[Diagnostics.Stopwatch]::StartNew()
        $answer=[WorldGenWindowProbe]::SendMessageTimeout($handle,0,[IntPtr]::Zero,[IntPtr]::Zero,2,1500,[ref]$result)
        $maxMs=[Math]::Max($maxMs,$timer.ElapsedMilliseconds);$samples++
        if($answer -eq [IntPtr]::Zero -and !$run.HasExited){$misses++}
    }
    Start-Sleep -Milliseconds 500
}
$run.WaitForExit()
@{Detail=$Detail;Samples=$samples;Timeouts=$misses;MaxMessageMilliseconds=$maxMs;PeakMemoryBytes=$peakMemory;ExitCode=$run.ExitCode} | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $output ('window-'+$Detail+'.json'))
Get-Content -LiteralPath $report
if($run.ExitCode -ne 0 -or $misses -gt 0){throw "Interaction validation failed; inspect $log"}
Write-Host "PASS $Detail-cell UI cancellation, frame timing, map scrolling, resizing and Windows message handling."
