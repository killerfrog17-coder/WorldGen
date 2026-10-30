"""Open the separate Kimodo audition on the rightmost monitor."""
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
source = (REPO / 'scripts/open-human-builder-demo.py').read_text()
source = source.replace("ROOT=REPO/'artifacts/human-builder-demo'", "ROOT=REPO/'artifacts/kimodo-first-person-demo'")
source = source.replace('Human - Character Builder Comparison.blend', 'Kimodo - First Person Demo.blend')
source = source.replace('human-builder-demo-viewer.py', 'kimodo-first-person-demo-viewer.py')
source = source.replace('Human builder comparison viewer ready.', 'Kimodo first-person demo viewer ready.')
source = source.replace('Human comparison opened RIGHTMOST', 'Kimodo first-person demo opened RIGHTMOST')
exec(compile(source, str(REPO / 'scripts/open-human-builder-demo.py'), 'exec'))
