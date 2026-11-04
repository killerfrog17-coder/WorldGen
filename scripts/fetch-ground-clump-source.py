"""Fetch the free CC0 source model used by the offline clump atlas baker."""
import hashlib
import json
import sys
from pathlib import Path
import urllib.request

root = Path(__file__).resolve().parent.parent
base = root / "assets-source/vegetation/ground-clumps"

def read(url):
    request = urllib.request.Request(url, headers={"User-Agent": "WorldGen offline asset authoring github.com/killerfrog17-coder/WorldGen"})
    return urllib.request.urlopen(request, timeout=90).read()

for asset in sys.argv[1:] or ["grass_medium_01", "nettle_plant"]:
    if asset not in ("grass_medium_01", "nettle_plant"):
        raise ValueError("Unreviewed source asset")
    destination = base / asset
    catalog = json.loads(read("https://api.polyhaven.com/files/" + asset))
    model = catalog["blend"]["1k"]["blend"]
    files = {asset + "_1k.blend": model, **model["include"]}
    receipt = {"asset": asset, "source": "https://polyhaven.com/a/" + asset, "license": "CC0-1.0", "license_url": "https://polyhaven.com/license", "files": []}
    for name, info in files.items():
        path = destination / name
        path.parent.mkdir(parents=True, exist_ok=True)
        data = path.read_bytes() if path.exists() else read(info["url"])
        if hashlib.md5(data).hexdigest() != info["md5"]:
            raise ValueError(f"Source integrity mismatch: {name}")
        path.write_bytes(data)
        receipt["files"].append({"path": path.relative_to(root).as_posix(), "url": info["url"], "sha256": hashlib.sha256(data).hexdigest(), "bytes": len(data)})
        print(name, len(data))
    (destination / "source.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
