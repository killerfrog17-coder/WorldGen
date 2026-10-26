"""Open only this review window on the rightmost monitor, preserving other apps."""
import ctypes
from ctypes import wintypes
import json
from pathlib import Path
import subprocess
import time

REPO=Path(__file__).resolve().parent.parent
ROOT=REPO/'artifacts/human-builder-demo'
blend=ROOT/'Human - Character Builder Comparison.blend'
assert blend.is_file()
with (ROOT/'viewer.log').open('w') as log:
    process=subprocess.Popen([str(REPO/'artifacts/tools/blender-4.5.15/blender-4.5.15-windows-x64/blender.exe'),'--factory-startup','--disable-autoexec',str(blend),'--python',str(REPO/'scripts/human-builder-demo-viewer.py')],stdout=log,stderr=subprocess.STDOUT,cwd=REPO)
(ROOT/'viewer-process.json').write_text(json.dumps({'pid':process.pid,'blend':str(blend)}))
user=ctypes.windll.user32
user.GetWindowThreadProcessId.argtypes=[wintypes.HWND,ctypes.POINTER(wintypes.DWORD)]
user.IsWindowVisible.argtypes=[wintypes.HWND]
user.MoveWindow.argtypes=[wintypes.HWND,ctypes.c_int,ctypes.c_int,ctypes.c_int,ctypes.c_int,wintypes.BOOL]
user.ShowWindow.argtypes=[wintypes.HWND,ctypes.c_int];user.SetForegroundWindow.argtypes=[wintypes.HWND]
windows=[]
@ctypes.WINFUNCTYPE(wintypes.BOOL,wintypes.HWND,wintypes.LPARAM)
def find_window(hwnd,param):
    pid=wintypes.DWORD();user.GetWindowThreadProcessId(hwnd,ctypes.byref(pid))
    if pid.value==process.pid and user.IsWindowVisible(hwnd):windows.append(hwnd)
    return True
end=time.monotonic()+90
while time.monotonic()<end and process.poll() is None:
    windows.clear();user.EnumWindows(find_window,0)
    if windows and 'Human builder comparison viewer ready.' in (ROOT/'viewer.log').read_text(errors='replace'):break
    time.sleep(.3)
else:raise RuntimeError('Human viewer did not become ready')
class MonitorInfo(ctypes.Structure):
    _fields_=[('cbSize',wintypes.DWORD),('rcMonitor',wintypes.RECT),('rcWork',wintypes.RECT),('flags',wintypes.DWORD)]
monitors=[]
@ctypes.WINFUNCTYPE(wintypes.BOOL,wintypes.HANDLE,wintypes.HDC,ctypes.POINTER(wintypes.RECT),wintypes.LPARAM)
def collect(handle,dc,bounds,param):
    info=MonitorInfo();info.cbSize=ctypes.sizeof(info)
    if user.GetMonitorInfoW(handle,ctypes.byref(info)):monitors.append(info.rcWork)
    return True
user.EnumDisplayMonitors(0,0,collect,0)
rect=max(monitors,key=lambda r:r.left);hwnd=windows[0]
user.ShowWindow(hwnd,9);user.MoveWindow(hwnd,rect.left,rect.top,rect.right-rect.left,rect.bottom-rect.top,True)
user.ShowWindow(hwnd,3);user.SetForegroundWindow(hwnd)
print('Human comparison opened RIGHTMOST, PID',process.pid)
