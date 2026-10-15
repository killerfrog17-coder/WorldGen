"""Download WorldClim 2.1 normals; aggregate 10-minute land rasters to 2-degree blocks.
Data preparation ONLY. Does not run WorldGen or fit parameters.
Fick & Hijmans (2017), doi:10.1002/joc.5086. Source: worldclim.org/data/worldclim21.html.
"""
import argparse
import csv
import hashlib
import json
import os
from pathlib import Path
import urllib.request
import zipfile

import numpy as np
import rasterio


def prepare(folder):
    folder.mkdir(parents=True, exist_ok=True)
    manifest = {"dataset": "WorldClim 2.1 / 1970-2000", "source": "https://worldclim.org/data/worldclim21.html",
                "citation": "Fick & Hijmans 2017, doi:10.1002/joc.5086", "units": "temperature Celsius; precipitation mm/month; height metres", "files": []}
    grids = {}
    for variable in ("tavg", "prec", "elev"):
        url = f"https://geodata.ucdavis.edu/climate/worldclim/2_1/base/wc2.1_10m_{variable}.zip"
        archive = folder / f"wc2.1_10m_{variable}.zip"
        if not archive.exists():
            print(f"Downloading {url}", flush=True)
            pending = archive.with_suffix(".download")
            with urllib.request.urlopen(url, timeout=120) as response, pending.open("wb") as target:
                while chunk := response.read(1024 * 1024):
                    target.write(chunk)
            os.replace(pending, archive)
        with zipfile.ZipFile(archive) as zipped:
            bad = zipped.testzip()
            if bad:
                raise ValueError(f"Corrupt download: {archive}: {bad}")
            names = sorted(n for n in zipped.namelist() if n.lower().endswith(".tif"))
            if len(names) != (1 if variable == "elev" else 12):
                raise ValueError(f"Unexpected raster count in {archive}")
            arrays = []
            for name in names:
                # Read within the zip, never extract untrusted paths.
                with rasterio.open(f"zip://{archive.resolve().as_posix()}!{name}") as dataset:
                    if (dataset.width, dataset.height) != (2160, 1080) or dataset.crs.to_epsg() != 4326:
                        raise ValueError("Unexpected WorldClim geometry")
                    if not np.allclose(tuple(dataset.transform)[:6], (1/6, 0, -180, 0, -1/6, 90)):
                        raise ValueError("Unexpected raster orientation")
                    arrays.append(dataset.read(1, masked=True).astype(float).filled(np.nan))
            grids[variable] = np.stack(arrays)
        manifest["files"].append({"url": url, "sha256": hashlib.sha256(archive.read_bytes()).hexdigest()})
    valid = np.all(np.isfinite(grids["tavg"]), axis=0) & np.all(np.isfinite(grids["prec"]), axis=0) & np.isfinite(grids["elev"][0])
    valid &= np.all((grids["tavg"] >= -90) & (grids["tavg"] <= 65), axis=0)
    valid &= np.all((grids["prec"] >= 0) & (grids["prec"] <= 5000), axis=0)
    rows = []
    for y in range(90):
        for x in range(180):
            tile = np.s_[y*12:(y+1)*12, x*12:(x+1)*12]
            mask = valid[tile]
            if mask.mean() < .7:
                continue  # Coastal mismatches are not targets to memorize.
            lat = 89 - y*2
            lon = -179 + x*2
            # Geographic folds, with a 2-degree excluded rim around each 20-degree block.
            if y % 10 in (0, 9) or x % 10 in (0, 9):
                continue
            block = (y // 10) * 18 + x // 10
            fold = int(hashlib.sha256(f"worldgen-climate-v1:{block}".encode()).hexdigest()[:8], 16) % 5
            split = "test" if fold == 0 else "validation" if fold == 1 else "train"
            pixel_lats = 90 - (np.arange(y*12, (y+1)*12) + .5) / 6
            weights = np.broadcast_to(np.cos(np.deg2rad(pixel_lats))[:, None], mask.shape)[mask]
            means = [np.average(a[tile][mask], weights=weights) for v in ("elev", "tavg", "prec") for a in grids[v]]
            rows.append([y*180+x, lat, lon, split, (y//15)*6+x//30, mask.mean(), *means])
    if len(rows) < 1000:
        raise ValueError("Too few valid observation blocks")
    output = folder / "observations.csv"
    with output.with_suffix(".tmp").open("w", newline="", encoding="utf-8") as stream:
        writer = csv.writer(stream)
        writer.writerow(["block", "latitude", "longitude", "split", "region", "land_fraction", "height_m"] + [f"t{m:02}" for m in range(1, 13)] + [f"p{m:02}" for m in range(1, 13)])
        writer.writerows(rows)
    os.replace(output.with_suffix(".tmp"), output)
    manifest["observations_sha256"] = hashlib.sha256(output.read_bytes()).hexdigest()
    manifest["blocks"] = len(rows)
    (folder / "source.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(f"Prepared {len(rows)} observed blocks: {output}", flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("folder", type=Path)
    prepare(parser.parse_args().folder)
