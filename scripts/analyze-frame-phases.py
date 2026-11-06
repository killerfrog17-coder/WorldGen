"""Inspect source-timestamped engine timings; observation time is NOT frame time."""
import argparse
import csv
import json
from pathlib import Path


def rows(path):
    with path.open(encoding="utf-8-sig", newline="") as source:
        return list(csv.DictReader(source))


def analyze(root):
    if not (root / "complete.txt").exists():
        raise ValueError("Wait for the completed capture")
    output = []
    for summary in rows(root / "summary.csv"):
        prefix = summary["site"] + "-" + summary["case"]
        bridge = rows(root / (prefix + "-clock-bridge.csv"))[0]
        hz = float(bridge["system_hz"])
        engine_hz = float(bridge["engine_hz"])
        if hz != engine_hz:
            raise ValueError("Cannot align different engine/system clock domains")
        midpoint = (int(bridge["managed_before"]) + int(bridge["managed_after"])) / 2
        start = int(bridge["system_qpc"]) + (int(summary["start_qpc"]) - midpoint) * hz / float(bridge["managed_hz"])
        timings = []
        for row in rows(root / (prefix + "-engine-timings.csv")):
            t = (int(row["frame_start"]) - start) / hz
            if not 0 <= t <= float(summary["seconds"]):
                continue
            timings.append(dict(t_s=t, **{k: float(row[k]) for k in
                ("cpu_ms", "main_ms", "render_ms", "present_wait_ms", "gpu_ms")}))
        trace = rows(root / (prefix + "-trace.csv"))
        # These scopes overlap (e.g. scatter is inside Explorer.Update).
        # Report them separately rather than summing inclusive durations.
        scope_names = [name for name in trace[0] if name.endswith("_ms") and
                       name not in ("t_s", "frame_ms")] if trace else []
        scope_stats = {}
        for name in scope_names:
            values = sorted(float(row[name]) for row in trace)
            scope_stats[name] = dict(mean=sum(values) / len(values),
                                    p99=values[min(len(values) - 1, int(len(values) * .99))],
                                    maximum=values[-1])
        worst_updates = []
        for frame in sorted(trace, key=lambda f: float(f["explorer_ms"]), reverse=True)[:10]:
            worst_updates.append(dict(t_s=float(frame["t_s"]), frame_ms=float(frame["frame_ms"]),
                                      scopes={name: float(frame[name]) for name in scope_names
                                              if float(frame[name]) >= .2}))
        peaks = []
        for frame in sorted(trace, key=lambda f: float(f["frame_ms"]), reverse=True)[:20]:
            end = float(frame["t_s"])
            begin = end - float(frame["frame_ms"]) / 1000
            # Include neighbouring source frames: update-row sampling and engine
            # frame starts are different boundaries. Do not call this exact pairing.
            nearby = [r for r in timings if begin - .02 <= r["t_s"] <= end + .005]
            peaks.append(dict(t_s=end, frame_ms=float(frame["frame_ms"]),
                              explorer_ms=float(frame["explorer_ms"]), nearby_engine_frames=nearby))
        output.append(dict(site=summary["site"], case=summary["case"], samples=len(timings),
                           bridge_uncertainty_ms=(int(bridge["managed_after"]) - int(bridge["managed_before"])) * 500 / float(bridge["managed_hz"]),
                           largest_cpu=sorted(timings, key=lambda r: r["cpu_ms"], reverse=True)[:20],
                           largest_render=sorted(timings, key=lambda r: r["render_ms"], reverse=True)[:10],
                           largest_present_wait=sorted(timings, key=lambda r: r["present_wait_ms"], reverse=True)[:10],
                           scope_statistics=scope_stats, worst_explorer_updates=worst_updates,
                           worst_observed_frames=peaks))
    return output


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    result = json.dumps(analyze(args.directory), indent=2) + "\n"
    if args.output:
        args.output.write_text(result, encoding="utf-8")
    else:
        print(result)
