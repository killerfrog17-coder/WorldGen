"""Open the separate comparison review on the primary display."""
import argparse
import ctypes
from ctypes import wintypes
import json
from pathlib import Path
import subprocess
import time


def main():
    repo=Path(__file__).resolve().parent.parent
    ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,
        default=repo/'artifacts/deer-motion-comparison-20261023')
    a=ap.parse_args();root=a.root.resolve()
    blend=root/'Deer - AI Motion Comparison.blend'
    blender=repo/'artifacts/tools/blender-4.5.15/blender-4.5.15-windows-x64/blender.exe'
    assert blend.is_file()
    log=root/'viewer.log'
    with log.open('w') as f:
        process=subprocess.Popen([str(blender),'--factory-startup','--disable-autoexec',str(blend),
            '--python',str(repo/'scripts/deer-motion-comparison-viewer.py')],
            cwd=repo,stdout=f,stderr=subprocess.STDOUT)
    (root/'viewer-process.json').write_text(json.dumps({'pid':process.pid,'blend':str(blend)}))
    user=ctypes.windll.user32;windows=[]
    callback=ctypes.WINFUNCTYPE(wintypes.BOOL,wintypes.HWND,wintypes.LPARAM)
    user.GetWindowThreadProcessId.argtypes=[wintypes.HWND,ctypes.POINTER(wintypes.DWORD)]
    @callback
    def find(hwnd,param):
        pid=wintypes.DWORD();user.GetWindowThreadProcessId(hwnd,ctypes.byref(pid))
        if pid.value==process.pid and user.IsWindowVisible(hwnd):windows.append(hwnd)
        return True
    end=time.monotonic()+90
    while time.monotonic()<end and process.poll() is None:
        windows.clear();user.EnumWindows(find,0)
        if windows and 'comparison viewer ready.' in log.read_text(errors='replace'):
            rect=wintypes.RECT();user.SystemParametersInfoW(48,0,ctypes.byref(rect),0)
            hwnd=windows[0];user.ShowWindow(hwnd,9)
            user.MoveWindow(hwnd,rect.left,rect.top,rect.right-rect.left,rect.bottom-rect.top,True)
            user.ShowWindow(hwnd,3);user.SetForegroundWindow(hwnd)
            print('Comparison opened on primary monitor.');return
        time.sleep(.3)
    raise RuntimeError('Viewer not ready; inspect '+str(log))


if __name__=='__main__':main()
