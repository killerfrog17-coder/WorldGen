"""Open the new review without closing or modifying any existing user apps."""
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
        default=repo/'artifacts/deer-mesh2motion-20261023')
    ap.add_argument('--own-rig',action='store_true')
    ap.add_argument('--trellis',action='store_true',help='Open the separate native-material comparison')
    ap.add_argument('--coat',action='store_true',help='Open the live anatomical material review')
    ap.add_argument('--coat-final',action='store_true',help='Open the finished material trial')
    ap.add_argument('--stage1',action='store_true',help='Open the unrigged animal selection review')
    ap.add_argument('--stage2',action='store_true',help='Open the approved model with fitted authored motion')
    ap.add_argument('--rightmost',action='store_true',help='Place the review on the rightmost monitor')
    ap.add_argument('--blend-name',help='Separate review filename inside --root')
    a=ap.parse_args();root=a.root.resolve()
    blend=root/('Deer - Original Rig Motion Review.blend' if a.own_rig else 'Deer - Mesh2Motion Review.blend')
    if a.trellis:blend=root/'Deer - TRELLIS2 Material Review.blend'
    if a.coat:blend=root/'Deer - Masked Coat Working Review.blend'
    if a.coat_final:blend=root/'Deer - Masked Coat Review.blend'
    if a.stage1:blend=root/'Deer - Stage1 Model Review.blend'
    if a.stage2:blend=root/'Deer - Stage2 Animation Review.blend'
    if a.blend_name:
        assert Path(a.blend_name).name==a.blend_name and a.blend_name.endswith('.blend')
        blend=root/a.blend_name
    viewer='deer-own-rig-viewer.py' if a.own_rig else 'deer-mesh2motion-viewer.py'
    ready='Original-rig deer viewer ready.' if a.own_rig else 'Mesh2Motion deer viewer ready.'
    if a.trellis:
        viewer='deer-trellis2-viewer.py';ready='TRELLIS2 viewer controls ready.'
    if a.coat or a.coat_final:
        viewer='deer-masked-coat-viewer.py';ready='Masked-coat live viewer ready.'
    if a.stage1:
        viewer='fauna-stage1-viewer.py';ready='Fauna stage1 viewer ready.'
    if a.stage2:
        viewer='fauna-stage2-viewer.py';ready='Fauna stage2 viewer ready.'
    blender=repo/'artifacts/tools/blender-4.5.15/blender-4.5.15-windows-x64/blender.exe'
    assert blend.is_file()
    running=subprocess.check_output(['tasklist','/FO','CSV','/NH'],text=True,errors='replace').lower()
    playing='theforest.exe' in running or 'sonsoftheforest.exe' in running
    startup=subprocess.STARTUPINFO()
    if playing:
        startup.dwFlags|=subprocess.STARTF_USESHOWWINDOW;startup.wShowWindow=4
    log=root/'viewer.log'
    with log.open('w') as f:
        process=subprocess.Popen([str(blender),'--factory-startup','--disable-autoexec',str(blend),
            '--python',str(repo/'scripts'/viewer)],
            cwd=repo,stdout=f,stderr=subprocess.STDOUT,startupinfo=startup)
    (root/'viewer-process.json').write_text(json.dumps({'pid':process.pid,'blend':str(blend)}))
    user=ctypes.windll.user32;windows=[]
    user.GetWindowThreadProcessId.argtypes=[wintypes.HWND,ctypes.POINTER(wintypes.DWORD)]
    user.IsWindowVisible.argtypes=[wintypes.HWND]
    user.MoveWindow.argtypes=[wintypes.HWND,ctypes.c_int,ctypes.c_int,ctypes.c_int,ctypes.c_int,wintypes.BOOL]
    user.ShowWindow.argtypes=[wintypes.HWND,ctypes.c_int];user.SetForegroundWindow.argtypes=[wintypes.HWND]
    callback=ctypes.WINFUNCTYPE(wintypes.BOOL,wintypes.HWND,wintypes.LPARAM)
    @callback
    def find(hwnd,param):
        pid=wintypes.DWORD();user.GetWindowThreadProcessId(hwnd,ctypes.byref(pid))
        if pid.value==process.pid and user.IsWindowVisible(hwnd):windows.append(hwnd)
        return True
    end=time.monotonic()+90
    while time.monotonic()<end and process.poll() is None:
        windows.clear();user.EnumWindows(find,0)
        if windows and ready in log.read_text(errors='replace'):
            rect=wintypes.RECT();user.SystemParametersInfoW(48,0,ctypes.byref(rect),0)
            if a.rightmost:
                class MonitorInfo(ctypes.Structure):
                    _fields_=[('cbSize',wintypes.DWORD),('rcMonitor',wintypes.RECT),('rcWork',wintypes.RECT),('flags',wintypes.DWORD)]
                monitors=[]
                monitor_callback=ctypes.WINFUNCTYPE(wintypes.BOOL,wintypes.HANDLE,wintypes.HDC,ctypes.POINTER(wintypes.RECT),wintypes.LPARAM)
                @monitor_callback
                def collect_monitor(handle,dc,bounds,param):
                    mi=MonitorInfo();mi.cbSize=ctypes.sizeof(mi)
                    if user.GetMonitorInfoW(handle,ctypes.byref(mi)):monitors.append(mi.rcWork)
                    return True
                user.EnumDisplayMonitors(0,0,collect_monitor,0)
                if monitors:rect=max(monitors,key=lambda r:r.left)
            hwnd=windows[0];user.ShowWindow(hwnd,4 if playing else 9)
            user.MoveWindow(hwnd,rect.left,rect.top,rect.right-rect.left,rect.bottom-rect.top,True)
            # Keep input with the game; position only the newly launched review.
            # This launcher creates no Desktop item.
            if not playing:
                user.ShowWindow(hwnd,3)
                user.SetForegroundWindow(hwnd)
            print('Deer review opened on '+('rightmost' if a.rightmost else 'primary')+' display.');return
        time.sleep(.3)
    raise RuntimeError('Viewer not ready; inspect '+str(log))


if __name__=='__main__':main()
