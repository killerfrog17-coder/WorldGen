param([Parameter(Mandatory=$true)][int]$ReviewProcessId,[ValidateSet('Leftmost','Rightmost')][string]$Monitor='Rightmost')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
if(-not ('WorldGenReviewWindowPlacement' -as [type])){
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WorldGenReviewWindowPlacement {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window,out Rect rect);
    [DllImport("user32.dll",SetLastError=true)] public static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
}
'@
}
$previousReviewDpi=[WorldGenReviewWindowPlacement]::SetThreadDpiAwarenessContext([IntPtr]::new(-4))
try{
    $reviewMonitor=[System.Windows.Forms.Screen]::AllScreens|Sort-Object {$_.Bounds.X} -Descending:($Monitor -eq 'Rightmost')|Select-Object -First 1
    $reviewDeadline=[DateTime]::UtcNow.AddSeconds(15)
    do{
        $reviewProcess=Get-Process -Id $ReviewProcessId -ErrorAction Stop
        $reviewProcess.Refresh()
        if($reviewProcess.MainWindowHandle -ne [IntPtr]::Zero){break}
        Start-Sleep -Milliseconds 100
    }while([DateTime]::UtcNow -lt $reviewDeadline)
    if($reviewProcess.MainWindowHandle -eq [IntPtr]::Zero){throw 'Review process did not create a window'}
    $reviewBounds=[WorldGenReviewWindowPlacement+Rect]::new()
    if(-not [WorldGenReviewWindowPlacement]::GetWindowRect($reviewProcess.MainWindowHandle,[ref]$reviewBounds)){throw 'Cannot read review window bounds'}
    $reviewX=$reviewMonitor.WorkingArea.X+[Math]::Max(0,[int](($reviewMonitor.WorkingArea.Width-($reviewBounds.Right-$reviewBounds.Left))/2))
    $reviewY=$reviewMonitor.WorkingArea.Y+[Math]::Max(0,[int](($reviewMonitor.WorkingArea.Height-($reviewBounds.Bottom-$reviewBounds.Top))/2))
    # Preserve resolution, focus and stacking; move only the caller's own window.
    if(-not [WorldGenReviewWindowPlacement]::SetWindowPos($reviewProcess.MainWindowHandle,[IntPtr]::Zero,$reviewX,$reviewY,0,0,0x0015)){throw 'Cannot move review window'}
    if(-not [WorldGenReviewWindowPlacement]::GetWindowRect($reviewProcess.MainWindowHandle,[ref]$reviewBounds)){throw 'Cannot verify review window placement'}
    if($reviewBounds.Left -lt $reviewMonitor.Bounds.Left -or $reviewBounds.Left -ge $reviewMonitor.Bounds.Right){throw 'Review window is not on the selected monitor'}
    Write-Output ('Review window '+$ReviewProcessId+' on '+$reviewMonitor.DeviceName+' at '+$reviewBounds.Left+','+$reviewBounds.Top)
}finally{[void][WorldGenReviewWindowPlacement]::SetThreadDpiAwarenessContext($previousReviewDpi)}
