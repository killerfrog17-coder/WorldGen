param([Parameter(Mandatory=$true)][string]$Path)
$ErrorActionPreference='Stop'
$pdfFile=(Resolve-Path -LiteralPath $Path).Path
if([IO.Path]::GetExtension($pdfFile) -ne '.pdf'){throw 'Expected a PDF report'}
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class PdfReportWindows {
 public struct Rect {public int Left,Top,Right,Bottom;}
 public delegate bool Callback(IntPtr h,IntPtr p);
 [DllImport("user32.dll")] static extern bool EnumWindows(Callback c,IntPtr p);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h,StringBuilder s,int n);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h,StringBuilder s,int n);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint p);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a,uint b,bool on);
 [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
 [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int hgt,uint flags);
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int command);
 public static IntPtr[] Chrome() {
  var windows=new List<IntPtr>();EnumWindows((h,p)=>{var s=new StringBuilder(256);GetClassName(h,s,256);if(IsWindowVisible(h)&&s.ToString()=="Chrome_WidgetWin_1"){var title=new StringBuilder(1024);GetWindowText(h,title,1024);uint id;GetWindowThreadProcessId(h,out id);try{if(System.Diagnostics.Process.GetProcessById((int)id).ProcessName=="chrome"&&title.ToString().Contains("Google Chrome"))windows.Add(h);}catch{}}return true;},IntPtr.Zero);return windows.ToArray();
 }
 public static bool Activate(IntPtr h) {
  uint p;uint foreground=GetWindowThreadProcessId(GetForegroundWindow(),out p),current=GetCurrentThreadId();
  bool attached=foreground!=current&&AttachThreadInput(current,foreground,true);
  try{SetForegroundWindow(h);return GetForegroundWindow()==h;}finally{if(attached)AttachThreadInput(current,foreground,false);}
 }
}
'@
$left=[System.Windows.Forms.Screen]::AllScreens|Sort-Object {$_.Bounds.X}|Select-Object -First 1
$target=$null
foreach($handle in [PdfReportWindows]::Chrome()){
    $bounds=New-Object PdfReportWindows+Rect
    [void][PdfReportWindows]::GetWindowRect($handle,[ref]$bounds)
    $centerX=($bounds.Left+$bounds.Right)/2
    if($centerX -ge $left.Bounds.Left -and $centerX -lt $left.Bounds.Right -and $bounds.Bottom -gt $left.Bounds.Top){$target=$handle;break}
}
$uri=([Uri]$pdfFile).AbsoluteUri
if($target -and [PdfReportWindows]::Activate($target)){
    [System.Windows.Forms.SendKeys]::SendWait('^t')
    if([PdfReportWindows]::GetForegroundWindow() -ne $target){throw 'Focus changed; report URL was not typed'}
    $escaped=[regex]::Replace($uri,'[+^%~(){}\[\]]',{param($m)'{'+$m.Value+'}'})
    [System.Windows.Forms.SendKeys]::SendWait($escaped)
    [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
    Write-Output 'Opened PDF in a new tab in the existing leftmost Chrome window.'
} else {
    $chrome=@('C:/Program Files/Google/Chrome/Application/chrome.exe','C:/Program Files (x86)/Google/Chrome/Application/chrome.exe',"$env:LOCALAPPDATA/Google/Chrome/Application/chrome.exe")|Where-Object {Test-Path -LiteralPath $_}|Select-Object -First 1
    if(!$chrome){throw 'Chrome was not found'}
    $known=@([PdfReportWindows]::Chrome());$x=$left.Bounds.X+24;$y=$left.Bounds.Y+24;$w=$left.WorkingArea.Width-48;$h=$left.WorkingArea.Height-48
    Start-Process -FilePath $chrome -ArgumentList @('--new-window','--new-tab',('--window-position='+$x+','+$y),('--window-size='+$w+','+$h),('"'+$uri+'"')) -WindowStyle Normal | Out-Null
    for($attempt=0;$attempt -lt 50;$attempt++){
        $new=[PdfReportWindows]::Chrome()|Where-Object {$_ -notin $known}|Select-Object -First 1
        if($new){
            # Chrome can initially expose a transient window and then restore saved
            # maximized bounds. Position the settled report window after navigation.
            Start-Sleep -Seconds 2
            $new=[PdfReportWindows]::Chrome()|Where-Object {$_ -notin $known}|Select-Object -First 1
            if(!$new){continue}
            [void][PdfReportWindows]::ShowWindow($new,9)
            [void][PdfReportWindows]::SetWindowPos($new,[IntPtr]::Zero,$x,$y,$w,$h,0x0040)
            $placed=New-Object PdfReportWindows+Rect
            [void][PdfReportWindows]::GetWindowRect($new,[ref]$placed)
            $center=($placed.Left+$placed.Right)/2
            if($center -lt $left.Bounds.Left -or $center -ge $left.Bounds.Right){throw 'Report window did not remain on the leftmost monitor'}
            Write-Output 'Opened PDF in a new Chrome window/tab on the leftmost monitor.';break
        }
        Start-Sleep -Milliseconds 100
    }
    if(!$new){throw 'Could not verify the new report window'}
}
