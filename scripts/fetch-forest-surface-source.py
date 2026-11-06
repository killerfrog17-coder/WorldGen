"""Pin CC0 scanned ground sources for broadleaf, needle and damp litter.

Only downloads original source data. Texture compression/normalisation is an
offline Unity authoring operation; no extra runtime image processing occurs.
"""
import hashlib
import json
from pathlib import Path
import urllib.request

root = Path(__file__).resolve().parent.parent
base = root / "assets-source/terrain/surfaces/cc0-forest-floor"
materials = []

def read(url):
    request = urllib.request.Request(url, headers={"User-Agent": "WorldGen offline asset authoring github.com/killerfrog17-coder/WorldGen"})
    return urllib.request.urlopen(request, timeout=90).read()

for name, asset, layer in [("litter", "dry_decay_leaves", 14), ("needles", "forrest_ground_03", 15), ("wetlitter", "forest_leaves_02", 16)]:
    folder = base / asset
    folder.mkdir(parents=True, exist_ok=True)
    catalog = json.loads(read("https://api.polyhaven.com/files/" + asset))
    info = json.loads(read("https://api.polyhaven.com/info/" + asset))
    record = {"name": name, "asset": asset, "layer": layer, "source": "https://polyhaven.com/a/" + asset,
              "license": "CC0-1.0", "license_url": "https://polyhaven.com/license", "dimensions_mm": info.get("dimensions"), "files": {}}
    for channel, field in [("albedo", "Diffuse"), ("normal", "nor_gl"), ("mask", "arm")]:
        source = catalog[field]["2k"]["jpg"]
        path = folder / (channel + ".jpg")
        data = path.read_bytes() if path.exists() else read(source["url"])
        if hashlib.md5(data).hexdigest() != source["md5"]:
            raise ValueError("Source integrity mismatch: " + str(path))
        path.write_bytes(data)
        record["files"][channel] = {"path": path.relative_to(root).as_posix(), "url": source["url"], "sha256": hashlib.sha256(data).hexdigest(), "bytes": len(data)}
        print(asset, channel, len(data))
    materials.append(record)
(base / "sources.json").write_text(json.dumps(materials, indent=2) + "\n", encoding="utf-8")
(root / "assets/verification/forest-floor-sources-20261106.json").write_text(json.dumps({"materials": materials,
    "publication": "TerrainSurfaceArtBuilder.Build keeps seventeen BC7 slices. Damp floor now uses photographed moss, fallen leaves and twigs instead of living ground plants over mud. Original scanned normals and AO/roughness are offline inputs.",
    "budget": "1024/512/128-pixel mipmapped BC7 albedo/normal/mask arrays, about 28.7 MiB across all seventeen layers. Texture fetch count, layer count and ranges unchanged; about 21.25 MiB above the previous 512/256/128 arrays."}, indent=2) + "\n", encoding="utf-8")
