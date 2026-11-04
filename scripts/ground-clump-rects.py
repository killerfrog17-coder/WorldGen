"""Publish exact sprite bounds from visible alpha in the Blender bake.

The short wide grass occupies only part of its square atlas cell. Sampling that
whole cell shortened it and made the full-height herb disproportionately visible.
This changes registration only, not the source image or plant root placement.
"""
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
receipt = json.loads((root / 'assets-source/vegetation/ground-clumps/bake.json').read_text())
atlas = root / receipt['output']
try:
    from PIL import Image
    image = Image.open(atlas).getchannel('A')
    def bounds(index):
        x, y = index % 2 * 512, (1-index // 2) * 512
        box = image.crop((x, y, x+512, y+512)).point(lambda a: 255 if a > 40 else 0).getbbox()
        if box is None:
            raise ValueError('Empty clump atlas tile')
        return x+box[0], 1024-y-box[3], x+box[2], 1024-y-box[1]
except ImportError:
    # Blender's embedded Python can recreate the same registration without an
    # extra dependency. Its image rows already start at the bottom.
    import bpy
    pixels = list(bpy.data.images.load(str(atlas)).pixels)
    def bounds(index):
        x0, y0 = index % 2 * 512, index // 2 * 512
        used = [(x, y) for y in range(y0, y0+512) for x in range(x0, x0+512)
                if pixels[(y*1024+x)*4+3] > 40/255]
        return min(p[0] for p in used), min(p[1] for p in used), max(p[0] for p in used)+1, max(p[1] for p in used)+1
rectangles = []
aspects = []
for tile in receipt['tiles']:
    index = tile['tile']
    x0, y0, x1, y1 = bounds(index)
    x0, y0 = max(index % 2 * 512, x0-2), max(index // 2 * 512, y0-2)
    x1, y1 = min((index % 2+1)*512, x1+2), min((index // 2+1)*512, y1+2)
    rectangles.append(dict(x=x0/1024, y=y0/1024, z=(x1-x0)/1024, w=(y1-y0)/1024))
    aspects.append((x1-x0)/(y1-y0))
output = root / 'unity/WorldGen.Unity/Assets/WorldGen/Resources/GroundClumpRectsV1.json'
output.write_text(json.dumps(dict(rectangles=rectangles, aspects=aspects), indent=2) + '\n')
print(output.relative_to(root), aspects)
