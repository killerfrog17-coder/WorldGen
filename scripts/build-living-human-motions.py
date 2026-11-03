"""Bake camp-life motions onto existing skeletons, preserving production banks."""
import importlib.util
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location('npc_bake', REPO / 'scripts/build-kimodo-human-bank.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
module.ROOT = REPO / 'assets-source/humans/motion/npc'
module.OUTPUT = REPO / 'assets/human-motion-npc'
module.ROLE_SOURCE = {'SleepSide': ('sleep_side', True), 'SitGround': ('sit_ground', True), 'Social': ('social_stand', True), 'WorkKneeling': ('work_kneeling', True)}
module.MIRROR = set()
module.LOCO = set(module.ROLE_SOURCE)  # relaxed fingers rather than weapon grips
if __name__ == '__main__':
    module.main()
