"""Syntax/type check against this project's cached Unity compiler references.

Does not start Unity, import assets, compile shaders, build a player, or replace
any player DLL. This is additional source validation, never a substitute for a
normal licensed editor build and graphical checks.
"""
from pathlib import Path
import subprocess,sys

root=Path(__file__).resolve().parents[1];project=root/'unity/WorldGen.Unity'
source=next((project/'Library/Bee/artifacts').glob('*/Assembly-CSharp.rsp'),None)
if source is None:raise SystemExit('A prior successful Unity editor compilation is required.')
out=root/'artifacts/unity-source-check';out.mkdir(parents=True,exist_ok=True)
lines=source.read_text(encoding='utf-8-sig').splitlines();result=[]
known={line.strip('"').replace('\\','/') for line in lines if line.strip('"').endswith('.cs')}
for line in lines:
    if line.startswith('-out:'):line='-out:"'+str(out/'Assembly-CSharp.check.dll')+'"'
    if line.startswith('-refout:'):line='-refout:"'+str(out/'Assembly-CSharp.check.ref.dll')+'"'
    result.append(line)
for p in sorted((project/'Assets/WorldGen/Runtime').rglob('*.cs')):
    relative=p.relative_to(project).as_posix()
    if relative not in known:result.append('"'+relative+'"')
response=out/'runtime.rsp';response.write_text('\n'.join(result),encoding='utf-8')
editor=Path('C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Data')
compilers=sorted((editor/'DotNetSdk/sdk').glob('*/Roslyn/bincore/csc.dll'))
if not compilers:raise SystemExit('Bundled Unity C# compiler unavailable.')
run=subprocess.run([str(editor/'DotNetSdk/dotnet.exe'),str(compilers[-1]),'@'+str(response)],cwd=project,capture_output=True,text=True)
(out/'compiler.log').write_text(run.stdout+'\n'+run.stderr,encoding='utf-8')
errors=[line for line in (run.stdout+'\n'+run.stderr).splitlines() if 'error ' in line.lower()]
print('\n'.join(errors) if errors else 'Unity runtime source check '+('PASS' if run.returncode==0 else 'FAILED (inspect compiler.log)'))
sys.exit(run.returncode)
