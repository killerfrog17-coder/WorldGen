"""Resume only this task's suspended coordinator after the shared PC benchmark."""
import ctypes,json,subprocess,time
from ctypes import wintypes
from pathlib import Path
root=Path(__file__).resolve().parent.parent/'artifacts/fauna-eye-closure-repair'
record=json.loads((root/'benchmark-pause.json').read_text());pid=int(record['pid'])
quiet=0
while quiet<3:
    command="Get-CimInstance Win32_Process | Where-Object {$_.Name -eq 'WorldGen.exe' -and $_.CommandLine -like '*--worldgen-performance*'} | Select-Object -ExpandProperty ProcessId"
    active=subprocess.check_output(['powershell','-NoProfile','-Command',command],text=True).strip()
    quiet=0 if active else quiet+1
    time.sleep(4)
command=f"Get-CimInstance Win32_Process -Filter 'ProcessId={pid}' | Select-Object -ExpandProperty CommandLine"
current=subprocess.check_output(['powershell','-NoProfile','-Command',command],text=True).strip()
assert current==record['command'],'Coordinator changed; refuse to resume another process'
kernel=ctypes.windll.kernel32;kernel.OpenProcess.argtypes=[wintypes.DWORD,wintypes.BOOL,wintypes.DWORD];kernel.OpenProcess.restype=wintypes.HANDLE
handle=kernel.OpenProcess(0x0800,False,pid);assert handle
nt=ctypes.windll.ntdll;nt.NtResumeProcess.argtypes=[wintypes.HANDLE];result=nt.NtResumeProcess(handle)
kernel.CloseHandle.argtypes=[wintypes.HANDLE];kernel.CloseHandle(handle);assert result==0,result
record['resumed']=True;(root/'benchmark-pause.json').write_text(json.dumps(record,indent=2))
print('RESUMED_OWN_FAUNA_EXPORT',pid,flush=True)
