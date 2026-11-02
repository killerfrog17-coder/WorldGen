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
parser.add_argument("--candidate", type=int, default=20)
parser.add_argument("--production-source", required=True)
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
for name, requested in (("specialist-continents", 190000), ("specialist-earth", 30000), ("specialist-archipelago", 30000)):
    folder = evidence / name
    run = load(folder / "run.json")
    metrics = load(folder / "metrics.json")
    assert run["rulesVersion"] == "foragers-4", (name, "Legacy rules cannot validate the current model")
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
for suffix in ("fresh0", "continents40k", "archipelago30k", "continents100k", "continents180k", "continents190k"):
    name=f"view-specialist-{args.candidate}-{suffix}"
    folder = evidence / name
    assert int(read(folder / "exit-code.txt")) == 0
    log = read(folder / "player.log")
    assert "FORAGER VIEW PASS" in log and not re.search(r"Exception:|FORAGER VIEW FAIL", log)
    result = load(folder / "result.json")
    local_query = load(folder / "nearby-camp.json")
    assert local_query["radiusMeters"] == 100
    images = {p.name: sha(p) for p in folder.glob("*.png")}
    assert all(key in images for key in ("Languages-map.png", "Technology-map.png", "Territories-map.png", "atlas-human-species.png"))
    native.append({"case": name, **result, "exit_code": 0, "nearby_camp": local_query, "image_sha256": images})

game = evidence / f"native-gameplay-{args.candidate}-earth20k"
assert int(read(game / "exit-code.txt")) == 0
checks = read(game / "checks.txt").splitlines()
assert len(checks) == 32 and all(line.startswith("PASS ") for line in checks)
assert not re.search(r"Exception:|FAIL ", read(game / "player.log"))
proof = load(evidence / f"proof-specialist-{args.candidate}/package-proof.json")
stamp = load(ROOT / "builds/slot-a/build-inputs.json")
assert proof["BuiltUtc"] == stamp["BuiltUtc"] and not stamp["Development"]
assert proof["terrain_worker_sha256"] == stamp["TerrainWorkerSha256"]
assert proof["fauna_package_exact"] and proof["original_audio_exact"]
before_freeze = load(evidence / "proof-camp-query-16/source-freeze.json")
after_freeze = load(evidence / f"proof-specialist-{args.candidate}/source-freeze.json")
assert before_freeze.keys() == after_freeze.keys()
changed_inputs = sorted(key for key in before_freeze if before_freeze[key] != after_freeze[key])
assert changed_inputs == [
    "src/WorldGen.Core/ForagerModel.cs",
    "src/WorldGen.Core/ForagerPreparation.cs",
    "src/WorldGen.Core/ForagerSimulation.cs",
    "src/WorldGen.Core/ForagerTechnology.cs",
    "src/WorldGen.Presentation/ForagerMaps.cs",
    "src/WorldGen.Presentation/ForagerView.cs",
    "src/WorldGen.Presentation/HistoryView.cs",
], "Unexpected production changes since the frozen version3 control"
numerical_freeze = load(evidence / "proof-specialist-17/source-freeze.json")
assert sorted(key for key in numerical_freeze if numerical_freeze[key] != after_freeze[key]) == [
    "src/WorldGen.Presentation/ForagerMaps.cs",
    "src/WorldGen.Presentation/ForagerView.cs",
    "src/WorldGen.Presentation/HistoryView.cs",
], "The complete histories must retain exactly their frozen numerical model"
before_probe = json.loads(read(evidence / "query-history-before.log").splitlines()[0])
after_probe = json.loads(read(evidence / "query-history-parity.log").splitlines()[0])
assert before_probe["Hash"] == after_probe["Hash"] == "133e6446580b1808e6e23aeb085fdbab5406edf16bc421b36921c93511a1af5c"
legacy_probe = json.loads(read(evidence / "specialist-legacy190k-parity.log").splitlines()[0])
assert legacy_probe["Hash"] == "21b46a1bbfd1b70baba74cb57fd182a72f6b554ed07588c3cbf676978a6b7d16"

mechanisms = read(evidence / "specialist-mechanisms.log")
temporal = read(evidence / "specialist-temporal.log")
assert "PASS 43 forager mechanism checks" in mechanisms
assert "PASS 485 forager observer integration checks" in read(evidence / "specialist-presentation-local-traditions.log")
assert "PASS 12 temporal" in temporal
assert "83dc65c0015f5cf67747b9477faaf233921a976abc34b5229d4ea6fa3984496b" in temporal
assert "PASS 9 historical climate/weather/water checks" in read(evidence / "specialist-weather-100k.log")
assert "PASS 1679 dated habitat/map/assemblage checks" in read(evidence / "specialist-habitat-100k.log")
changes = load(evidence / "specialist-changes-0-60k/changes.json")
sample = load(evidence / "specialist-earth/npc-review/band-samples.json")
assert sample["Years"] == 30000 and sample["Hash"] == histories[1]["hash"]
large_save = load(evidence / "specialist-packed-final/result.json")
assert large_save["checks"] == 33 and large_save["before"] == histories[0]["hash"]
parity = evidence / f"native-runtime-parity-{args.candidate}"
assert int(read(parity / "exit-code.txt")) == 0
runtime_hash = read(parity / "actual-hash.txt").strip()
core_probe = json.loads(read(parity / "reference.log").splitlines()[0])
assert core_probe["years"] == 37 and core_probe["Hash"] == runtime_hash
assert "HISTORY RESUME PASS " + runtime_hash in read(parity / "player.log")

manifest = {
    "recorded_utc": datetime.now(timezone.utc).isoformat(), "production_source": args.production_source,
    "status": "Verified review candidate; owner visual feedback pending",
    "historical_rules_source": source,
    "specialist_teaching_revision": {"changed_frozen_inputs": changed_inputs,
        "unchanged_version3_history_hash": legacy_probe["Hash"], "older_query_probe_hash": after_probe["Hash"]},
    "candidate_slot": "builds/slot-a", "rules": "foragers-4", "build": stamp,
    "rollback_tag": "before-paleolithic-rebuild-20261102", "package_proof": proof,
    "mechanism_checks": 43, "temporal_budget_save_checks": 12,
    "dated_weather_checks": 9, "dated_habitat_assemblage_checks": 1679,
    "temporal_hash": "83dc65c0015f5cf67747b9477faaf233921a976abc34b5229d4ea6fa3984496b",
    "large_world_save": large_save,
    "actual_runtime_parity": {"years_advanced": 37, "hash": runtime_hash, "exit_code": 0},
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
        "Existing saves retain their original simulation model; generate a new world for this revision.",
        "Old completed version2/3 histories and failed diagnostic captures are excluded from current-model acceptance.",
    ],
}
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
print(f"Recorded {len(histories)} completed current-model histories and {sum(x['checks'] for x in native)} actual player map checks: {args.output}")
