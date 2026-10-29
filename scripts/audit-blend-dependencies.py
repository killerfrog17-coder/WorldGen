"""Read approved authoring files without running embedded scripts or saving them."""
import bpy
import json
import sys
from pathlib import Path

arguments=sys.argv[sys.argv.index('--')+1:]
root=Path(arguments[0]).resolve()
output=Path(arguments[1]).resolve()
inventory=json.loads((root/'artifacts/fauna-runtime-20261026/native-inventory.json').read_text())
sources=[root/a['source'] for a in inventory['animals']]
sources.append(root/'artifacts/human-animation-preview/Human - Animation Comparison.blend')
records=[]
for index,source in enumerate(sources):
    bpy.ops.wm.open_mainfile(filepath=str(source),load_ui=False,use_scripts=False)
    external=[]
    for image in bpy.data.images:
        if image.source!='FILE' or image.packed_file or len(image.packed_files)>0 or not image.filepath:
            continue
        path=Path(bpy.path.abspath(image.filepath,library=image.library)).resolve()
        external.append({'kind':'image','path':str(path),'exists':path.is_file()})
    for library in bpy.data.libraries:
        path=Path(bpy.path.abspath(library.filepath)).resolve()
        external.append({'kind':'library','path':str(path),'exists':path.is_file()})
    records.append({'source':source.relative_to(root).as_posix(),'external':external})
    output.write_text(json.dumps(records,indent=2))
    print('AUTHORING DEPENDENCIES',index+1,'/',len(sources),source.name,len(external),flush=True)
