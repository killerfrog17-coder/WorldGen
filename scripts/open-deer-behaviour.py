"""Open the completed Blender review on the primary display, without changing prefs."""
import argparse
import ctypes
from ctypes import wintypes
import json
from pathlib import Path
import subprocess
import time

repo=Path(__file__).resolve().parent.parent
parser=argparse.ArgumentParser()
parser.add_argument('--root',type=Path,default=repo/'artifacts/deer-behaviour-20261023')
args=parser.parse_args()
root=args.root.resolve()
blend=root/'Deer - Game Ready Prototype.blend'
blender=repo/'artifacts/tools/blender-4.5.15/blender-4.5.15-windows-x64/blender.exe'
viewer=repo/'scripts/deer-behaviour-viewer.py'
if not blend.is_file():
    raise FileNotFoundError(blend)
logpath=root/'viewer.log'
with logpath.open('w') as output:
    process=subprocess.Popen([str(blender),'--factory-startup','--disable-autoexec',str(blend),
        '--python',str(viewer)],cwd=repo,stdout=output,stderr=subprocess.STDOUT)
(root/'viewer-process.json').write_text(json.dumps({'pid':process.pid,'blend':str(blend)}))
user32=ctypes.windll.user32
callback_type=ctypes.WINFUNCTYPE(wintypes.BOOL,wintypes.HWND,wintypes.LPARAM)
user32.GetWindowThreadProcessId.argtypes=[wintypes.HWND,ctypes.POINTER(wintypes.DWORD)]
user32.MoveWindow.argtypes=[wintypes.HWND,ctypes.c_int,ctypes.c_int,ctypes.c_int,ctypes.c_int,wintypes.BOOL]
user32.ShowWindow.argtypes=[wintypes.HWND,ctypes.c_int]
user32.SetForegroundWindow.argtypes=[wintypes.HWND]
windows=[]
@callback_type
def find(hwnd,param):
    pid=wintypes.DWORD()
    user32.GetWindowThreadProcessId(hwnd,ctypes.byref(pid))
    if pid.value==process.pid and user32.IsWindowVisible(hwnd):
        windows.append(hwnd)
    return True
deadline=time.monotonic()+90
while time.monotonic()<deadline and process.poll() is None:
    windows.clear()
    user32.EnumWindows(find,0)
    ready='Deer behaviour viewer ready.' in logpath.read_text(errors='replace')
    if windows and ready:
        rect=wintypes.RECT()
        user32.SystemParametersInfoW(48,0,ctypes.byref(rect),0)
        hwnd=windows[0]
        user32.ShowWindow(hwnd,9)
        user32.MoveWindow(hwnd,rect.left,rect.top,rect.right-rect.left,rect.bottom-rect.top,True)
        user32.ShowWindow(hwnd,3)
        user32.SetForegroundWindow(hwnd)
        break
    time.sleep(.3)
