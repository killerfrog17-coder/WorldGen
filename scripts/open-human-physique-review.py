"""Open this audition on the rightmost monitor, preserving all other apps."""
import importlib.util
from pathlib import Path
REPO=Path(__file__).resolve().parent.parent
# Reuse the established window-placement implementation with explicit review paths.
source=(REPO/'scripts/open-human-builder-demo.py').read_text()
source=source.replace("ROOT=REPO/'assets-source/humans/research/builders'","ROOT=REPO/'assets-source/humans/appearance-review'")
source=source.replace("Human - Character Builder Comparison.blend","Human physique comparison.blend").replace('human-builder-demo-viewer.py','human-physique-viewer.py').replace('Human builder comparison viewer ready.','Human physique viewer ready.')
exec(compile(source,str(__file__),'exec'))
