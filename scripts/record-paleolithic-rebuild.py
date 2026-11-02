"""Record a completed, source-matched review; refuse incomplete/stale evidence.

The long-run worlds and screenshots are disposable evidence. This manifest keeps
their hashes, measured outcomes and explicit limitations in ordinary Git.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--evidence", type=Path, default=ROOT / "artifacts/paleolithic-rebuild-20261102")
parser.add_argument("--output", type=Path, default=ROOT / "assets/verification/paleolithic-rebuild-20261102.json")
args = parser.parse_args()
evidence = args.evidence.resolve()


def read(path):
    raw = path.read_bytes()
    return raw.decode("utf-16" if raw[:2] in (b"\xff\xfe", b"\xfe\xff") else "utf-8-sig")


def load(path):
    return json.loads(read(path))


def sha(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(4 * 1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


histories = []
source = None
for name, requested in (("linear-continents", 190000), ("linear-earth", 30000), ("linear-archipelago", 30000)):
    folder = evidence / name
    run = load(folder / "run.json")
    metrics = load(folder / "metrics.json")
    assert run["rulesVersion"] == "foragers-3", (name, "Legacy rules cannot validate the current model")
    assert run["requestedYears"] == requested and metrics[-1]["year"] == requested, (name, "Incomplete history")
    source = source or run["source"]
    assert run["source"] == source, (name, "Different production sources")
    summary = load(folder / "audit/summary.json")
    behavior = load(folder / "review/behavior.json")
    assert summary["year"] == requested == behavior["Years"]
    assert summary["Hash"] == behavior["Hash"]
    assert behavior["source"] == source and behavior["checks"] == 9
    assert abs(summary["ledgerBalance"]) < .1
    transitions = list(zip(metrics, metrics[1:]))
    histories.append({
        "name": name, "seed": run["Seed"], "preset": run["preset"], "detail": run["Detail"],
        "rules": run["rulesVersion"], "years": requested, "hash": summary["Hash"],
        "world_sha256": sha(folder / f"year-{requested}.world"),
        "population": behavior["total"], "active_cohorts": behavior["active"],
        "retired_cohorts": behavior["retired"], "occupied_districts": behavior["inhabitedDistricts"],
        "population_decline_intervals": sum(b["population"] < a["population"] * .98 for a, b in transitions),
        "range_retreat_intervals": sum(b["districts"] < a["districts"] for a, b in transitions),
        "maximum_density_per_km2": behavior["maximumDensity"],
        "unprotected_perennial_ice_population": behavior["exposedIcePopulation"],
        "actual_use_toolkits": behavior["toolkits"], "resident_speech_varieties": behavior["languageVarieties"],
        "resident_families": behavior["languageFamilies"],
        "single_variety_family_population_share": behavior["singletonFamilyPopulationShare"],
        "belief_traditions": behavior["beliefTraditions"],
        "final_sapiens_ancestry_share": metrics[-1]["sapiensAncestry"] / metrics[-1]["population"],
        "last_metric_elapsed_seconds": metrics[-1]["elapsed"],
        "broad_error_gates": behavior["checks"],
    })

native = []
for name in ("view-resource-sharing-15-continents40k", "view-resource-sharing-15-archipelago30k", "view-resource-sharing-15-continents190k"):
    folder = evidence / name
    assert int(read(folder / "exit-code.txt")) == 0
    log = read(folder / "player.log")
    assert "FORAGER VIEW PASS" in log and not re.search(r"Exception:|FORAGER VIEW FAIL", log)
    result = load(folder / "result.json")
    images = {p.name: sha(p) for p in folder.glob("*.png")}
    assert all(key in images for key in ("Languages-map.png", "Technology-map.png", "Territories-map.png", "atlas-human-species.png"))
    native.append({"case": name, **result, "exit_code": 0, "image_sha256": images})

game = evidence / "native-gameplay-15-earth20k"
assert int(read(game / "exit-code.txt")) == 0
checks = read(game / "checks.txt").splitlines()
assert len(checks) == 32 and all(line.startswith("PASS ") for line in checks)
assert not re.search(r"Exception:|FAIL ", read(game / "player.log"))
proof = load(evidence / "proof-resource-sharing-15/package-proof.json")
stamp = load(ROOT / "builds/slot-a/build-inputs.json")
assert proof["BuiltUtc"] == stamp["BuiltUtc"] and not stamp["Development"]
assert proof["terrain_worker_sha256"] == stamp["TerrainWorkerSha256"]
assert proof["fauna_package_exact"] and proof["original_audio_exact"]

mechanisms = read(evidence / "linear-mechanisms.log")
temporal = read(evidence / "linear-temporal.log")
assert "PASS 38 forager mechanism checks" in mechanisms
assert "PASS 12 temporal" in temporal
assert "72b0e4c696c18cc2d91bde47d169671e229e8b06160c710541ae456b7e12e9f0" in temporal
changes = load(evidence / "linear-changes-0-60k/changes.json")
sample = load(evidence / "linear-earth/npc-review/band-samples.json")
assert sample["Years"] == 30000 and sample["Hash"] == histories[1]["hash"]

manifest = {
    "recorded_utc": datetime.now(timezone.utc).isoformat(), "production_source": source,
    "selected_slot": "builds/slot-a", "rules": "foragers-3", "build": stamp,
    "rollback_tag": "before-paleolithic-rebuild-20261102", "package_proof": proof,
    "mechanism_checks": 38, "temporal_budget_save_checks": 12,
    "temporal_hash": "72b0e4c696c18cc2d91bde47d169671e229e8b06160c710541ae456b7e12e9f0",
    "histories": histories, "actual_player_maps": native,
    "actual_gameplay": {"case": game.name, "exit_code": 0, "checks": checks, "terrain_loading": read(game / "initial-terrain.txt")},
    "dated_ecology": {key: changes[key] for key in ("fromYear", "toYear", "arrivals", "localExtinctions", "changedDistricts", "maximumForestDelta")},
    "local_people": {"year": sample["Years"], "checks": sample["checks"], "bands": len(sample["bands"]),
        "people": sum(len(band["Members"]) for band in sample["bands"]),
        "multilingual": sum(len(person["Languages"]) > 1 for band in sample["bands"] for person in band["Members"])},
    "scope_limits": [
        "Numerical ecological, demographic and cultural rates are model priors; broad error gates do not prove historical reconstruction.",
        "No scripted sapiens victory, fixed extinction year or exact historical language count.",
        "Long high-detail histories remain computationally expensive; no 60-second full-history claim.",
        "Physical NPC camps/dialogue/AI, new hominin bodies and moving vegetation/coastline terrain are separate implementation stages.",
        "Existing version2 saves retain original competition rules; create a new world or restart its simulation for version3.",
        "Old completed version2 histories and failed diagnostic captures are excluded from current-model acceptance.",
    ],
}
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
print(f"Recorded {len(histories)} completed current-model histories and {sum(x['checks'] for x in native)} actual player map checks: {args.output}")
