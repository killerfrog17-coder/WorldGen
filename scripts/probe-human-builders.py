"""Inspect the isolated MPFB installation inside Blender, without user preference writes."""
import json
import sys
from pathlib import Path

import bpy

REPO = Path(__file__).resolve().parent.parent
ROOT = REPO / 'artifacts/human-builder-demo'
sys.path.insert(0, str(REPO / 'artifacts/tools/mpfb2/src'))
original_extension_path = bpy.utils.extension_path_user
bpy.utils.extension_path_user = lambda package, **kwargs: str(ROOT / 'mpfb-user') if package == 'mpfb' else original_extension_path(package, **kwargs)
import mpfb
bpy.context.preferences.addons.new().module = 'mpfb'
mpfb.register()
from mpfb.services.humanservice import HumanService
from mpfb.services.locationservice import LocationService
from mpfb.services.targetservice import TargetService

LocationService._user_data = str(ROOT / 'mpfb-assets')
print('MPFB_PROBE', json.dumps({'macro': TargetService.get_default_macro_info_dict(), 'assets': LocationService.get_user_data()}), flush=True)
body = HumanService.create_human()
print('MPFB_BODY', body.name, len(body.data.vertices), len(body.data.polygons), flush=True)
print('MPFB_READY', flush=True)
