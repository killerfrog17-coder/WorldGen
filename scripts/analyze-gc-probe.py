"""Correlate native Mono stop/resume intervals with completed frame captures.

GC cycle start/end may span many frames; they are NOT themselves pause times.
Frame alignment uses the recorded native/managed QPC bridge, including its
uncertainty. Run only after the player and timing window have finished.
"""
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
    events = sorted(rows(root / "native-gc-events.csv"), key=lambda e: int(e["native_qpc"]))
    pending, pauses, unmatched = {}, [], 0
    for event in events:
        tid, kind, qpc = int(event["thread"]), int(event["event"]), int(event["native_qpc"])
        if kind == 6:
            if tid in pending:
                unmatched += 1
            pending[tid] = {6: qpc, "thread": tid}
        elif kind in (7, 8) and tid in pending:
            pending[tid][kind] = qpc
        elif kind == 9:
            pause = pending.pop(tid, None)
            if pause is None or 7 not in pause or 8 not in pause:
                unmatched += 1
                continue
            pause[9] = qpc
            if not pause[6] <= pause[7] <= pause[8] <= pause[9]:
                raise ValueError("Non-monotonic stop/resume sequence")
            pauses.append(pause)
    unmatched += len(pending)
    windows = []
    for summary in rows(root / "summary.csv"):
        prefix = f'{summary["site"]}-{summary["case"]}'
        bridge = rows(root / (prefix + "-clock-bridge.csv"))[0]
        hz = float(bridge["system_hz"])
        midpoint = (int(bridge["managed_before"]) + int(bridge["managed_after"])) / 2
        native_start = int(bridge["system_qpc"]) + (int(summary["start_qpc"]) - midpoint) * hz / float(bridge["managed_hz"])
        spans = [(1000 * (p[6] - native_start) / hz, 1000 * (p[9] - native_start) / hz, p) for p in pauses]
        frames = rows(root / (prefix + "-trace.csv"))
        peaks = []
        for frame in sorted(frames, key=lambda f: float(f["frame_ms"]), reverse=True)[:20]:
            end = 1000 * float(frame["t_s"])
            start = end - float(frame["frame_ms"])
            overlap = sum(max(0, min(end, b) - max(start, a)) for a, b, _ in spans)
            peaks.append({"t_s": float(frame["t_s"]), "frame_ms": float(frame["frame_ms"]),
                          "native_stop_resume_overlap_ms": overlap, "explorer_ms": float(frame["explorer_ms"])})
        in_window = [(a, b, p) for a, b, p in spans if b >= 0 and a <= 1000 * float(summary["seconds"])]
        windows.append({"site": summary["site"], "case": summary["case"],
                        "clock_bridge_half_span_ms": (int(bridge["managed_after"]) - int(bridge["managed_before"])) * 500 / float(bridge["managed_hz"]),
                        "stop_resume_intervals": len(in_window), "worst_frames": peaks,
                        "longest_pauses": [{"start_s": a / 1000, "total_ms": b - a,
                                            "suspend_ms": (p[7] - p[6]) * 1000 / hz,
                                            "stopped_ms": (p[8] - p[7]) * 1000 / hz,
                                            "resume_ms": (p[9] - p[8]) * 1000 / hz,
                                            "thread": p["thread"]}
                                           for a, b, p in sorted(in_window, key=lambda s: s[1] - s[0], reverse=True)[:20]]})
    return {"events": len(events), "unmatched_sequences": unmatched, "windows": windows}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    result = json.dumps(analyze(args.directory), indent=2)
    if args.output:
        args.output.write_text(result + "\n", encoding="utf-8")
    else:
        print(result)
