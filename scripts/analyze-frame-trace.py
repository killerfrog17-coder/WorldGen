"""Summarize completed native frame traces; never run during a timing window.

Counters may be sampled on the adjacent frame. Collection association is
reported as evidence, not as proof that collection caused the whole frame.
"""
import argparse
import csv
import json
from pathlib import Path


def summarize(path, top):
    with path.open(newline="", encoding="utf-8-sig") as source:
        rows = [{k: float(v) for k, v in row.items()} for row in csv.DictReader(source)]
    if not rows:
        return {"file": path.name, "frames": 0}
    durations = sorted(r["frame_ms"] for r in rows)
    changed = [False] + [r["gc0"] != p["gc0"] for p, r in zip(rows, rows[1:])]
    phases = ("planning_ms", "terrain_install_ms", "terrain_retirement_ms",
              "ground_publication_ms", "projection_ms", "forest_ground_ms",
              "inventory_ms", "scheduling_ms", "solids_ms", "humans_ms",
              "fauna_ms", "readout_ms", "weather_ms", "camera_render_ms")
    peaks = []
    for at in sorted(range(len(rows)), key=lambda i: rows[i]["frame_ms"], reverse=True)[:top]:
        row = rows[at]
        largest = sorted(((k, row.get(k, 0)) for k in phases), key=lambda kv: kv[1], reverse=True)[:3]
        peaks.append({"t_s": row["t_s"], "frame_ms": row["frame_ms"],
                      "gc_same_frame": changed[at],
                      "gc_adjacent_frame": any(changed[max(0, at-1):min(len(rows), at+2)]),
                      "explorer_ms": row.get("explorer_ms", 0), "largest_tracked_phases": dict(largest)})
    elapsed = rows[-1]["t_s"] - rows[0]["t_s"]
    allocated = rows[-1].get("allocated_main_total", 0) - rows[0].get("allocated_main_total", 0)
    return {"file": path.name, "frames": len(rows),
            "mean_ms": sum(durations)/len(rows),
            "p95_ms": durations[int((len(rows)-1)*.95)],
            "p99_ms": durations[int((len(rows)-1)*.99)], "max_ms": durations[-1],
            "over10ms": sum(v > 10 for v in durations),
            "over16ms": sum(v > 16 for v in durations),
            "collections": rows[-1]["gc0"] - rows[0]["gc0"],
            # Some release Mono players expose the API but return zero for
            # every sample. That is unavailable telemetry, not zero allocation.
            "main_allocated_bytes_per_second": allocated/max(.001, elapsed) if rows[-1].get("allocated_main_total", 0)>0 else None,
            "heap_min_mib": min(r["heap_bytes"] for r in rows)/1048576,
            "heap_max_mib": max(r["heap_bytes"] for r in rows)/1048576,
            "peaks": peaks}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--top", type=int, default=8)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    if not (args.directory / "complete.txt").is_file():
        parser.error("Wait for the run's complete.txt before analyzing its timing evidence")
    result = [summarize(p, args.top) for p in sorted(args.directory.glob("*-trace.csv"))]
    if not result:
        parser.error("No frame traces in this directory")
    payload = json.dumps(result, indent=2)
    if args.output:
        args.output.write_text(payload + "\n", encoding="utf-8")
    else:
        print(payload)


if __name__ == "__main__":
    main()
