"""Record/compare exact BC7 slice payloads around an offline art publication."""
import argparse
import hashlib
import json
from pathlib import Path
import re

root = Path(__file__).resolve().parent.parent
parser = argparse.ArgumentParser()
parser.add_argument("output", type=Path)
parser.add_argument("--baseline", type=Path)
args = parser.parse_args()
records = {}
for name in ("albedo", "normal", "mask"):
    path = root / ("unity/WorldGen.Unity/Assets/WorldGen/Resources/TerrainSurfaceArtV1/" + name + ".asset")
    text = path.read_text(encoding="utf-8")
    field = lambda key: int(re.search(r"^\s*" + key + r": (\d+)", text, re.M).group(1))
    width, depth, mips = field("m_Width"), field("m_Depth"), field("m_MipCount")
    if field("m_Format") not in (108, 109):
        raise ValueError("Expected BC7")
    payload = bytes.fromhex(re.search(r"_typelessdata: ([0-9a-fA-F]+)", text).group(1))
    stride = sum(max(1, ((width >> mip) + 3) // 4) ** 2 * 16 for mip in range(mips))
    if len(payload) != stride * depth:
        raise ValueError("Unexpected array storage layout")
    records[name] = {"width": width, "depth": depth, "mips": mips, "bytes": len(payload),
        "slice_sha256": [hashlib.sha256(payload[i*stride:(i+1)*stride]).hexdigest() for i in range(depth)]}
if args.baseline:
    prior = json.loads(args.baseline.read_text())
    for name, data in prior.items():
        if records[name]["slice_sha256"][:data["depth"]] != data["slice_sha256"]:
            raise ValueError("Prior terrain material changed: " + name)
    print("PASS every pre-existing material slice is byte-identical")
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(json.dumps(records, indent=2) + "\n")
print({name: data["depth"] for name, data in records.items()})
