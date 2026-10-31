"""Launch only the new animation comparison, on the rightmost monitor."""
from pathlib import Path

REPO=Path(__file__).resolve().parent.parent
source=(REPO/'scripts/open-human-builder-demo.py').read_text()
source=source.replace("ROOT=REPO/'assets-source/humans/research/builders'","ROOT=REPO/'assets-source/humans/base'")
source=source.replace('Human - Character Builder Comparison.blend','Human - Animation Comparison.blend')
source=source.replace('human-builder-demo-viewer.py','human-animation-preview-viewer.py')
source=source.replace('Human builder comparison viewer ready.','Human animation comparison viewer ready.')
source=source.replace('Human comparison opened RIGHTMOST','Human animation comparison opened RIGHTMOST')
exec(compile(source,str(REPO/'scripts/open-human-builder-demo.py'),'exec'))
