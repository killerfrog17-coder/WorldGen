"""Offline local motion generation for camp life; no inference runs in the game."""
import importlib.util
import json
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
ROOT = REPO / 'assets-source/humans/motion/npc'
CASES = [
    ('sleep_side', 150, 6131, 'A person lies on their side on the ground, with their knees gently bent and their hands near their face, resting quietly in this sleeping position.'),
    ('sit_ground', 150, 6132, 'A person sits cross-legged on the ground with a relaxed upright torso and their hands resting in their lap, gently looking around.'),
    ('social_stand', 150, 6133, 'A person stands relaxed and talks to someone, making occasional small calm gestures with their hands.'),
    ('work_kneeling', 150, 6134, 'A person kneels on both knees on the ground, holding a small object in their left hand and carefully working on it with their right hand.'),
]

if __name__ == '__main__':
    ROOT.mkdir(parents=True, exist_ok=True)
    (ROOT / 'model-provenance.json').write_bytes((REPO / 'assets-source/humans/motion/first-person/model-provenance.json').read_bytes())
    spec = importlib.util.spec_from_file_location('npc_generate', REPO / 'scripts/run-kimodo-demo-generation.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    module.ROOT = ROOT
    module.CASES = [dict(id=name, label=name, frames=frames, seed=seed, prompt=prompt) for name, frames, seed, prompt in CASES]
    (ROOT / 'queue.json').write_text(json.dumps(module.CASES, indent=2))
    module.main()
