"""Generate the initial shared human bank locally; retain raw model output."""
import importlib.util
import json
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
ROOT = REPO / 'artifacts/kimodo-human-bank'
DEMO = REPO / 'artifacts/kimodo-first-person-demo'

# Simple motion-capture descriptions work better than long prop/scene instructions.
DESCRIPTIONS = [
    ('idle', 120, 'A person stands relaxed, breathing gently, with their arms resting naturally at their sides.'),
    ('jog', 150, 'A person jogs forwards at a steady moderate pace.'),
    ('sprint', 150, 'A person sprints forwards quickly with powerful strides and natural arm swings.'),
    ('crouch_walk', 150, 'A person walks forwards slowly in a low crouch, keeping their knees bent.'),
    ('crouch_idle', 120, 'A person stays still in a crouched position with bent knees, keeping their balance.'),
    ('jump', 90, 'A person bends their knees, jumps forwards, lands on both feet and stands upright.'),
    ('swim', 150, 'A person swims forwards using a continuous breaststroke, reaching forwards with both arms and kicking their legs.'),
    ('fists_ready', 120, 'A person stands in a boxing stance with both fists raised in front of their chest.'),
    ('fists_guard', 120, 'A person raises both forearms to protect their face and holds a defensive boxing guard.'),
    ('punch', 90, 'A person throws a straight right-handed punch forwards and brings their fist back to guard.'),
    ('tool_ready', 120, 'A person stands holding a small tool in their right hand beside their waist, with a relaxed bent elbow.'),
    ('tool_walk', 150, 'A person walks forwards carrying a small tool in their right hand, keeping it beside their waist.'),
    ('tool_cut', 90, 'A person makes a controlled right-handed diagonal knife slash in front of their body and recovers.'),
    ('tool_stab', 90, 'A person thrusts a small knife forwards with their right hand and withdraws it.'),
    ('tool_blunt', 90, 'A person strikes forwards and downwards with a hammer in their right hand and recovers.'),
    ('tool_chop', 120, 'A person swings a small axe downwards with their right hand to chop wood, then raises it again.'),
    ('tool_guard', 120, 'A person holds a club diagonally in front of their upper body with their right hand to block an attack.'),
    ('spear_ready_one', 120, 'A person stands holding a long spear in their right hand beside their hip, pointing it forwards.'),
    ('spear_thrust_one', 90, 'A person makes a strong forwards thrust with a spear in their right hand and pulls it back.'),
    ('spear_ready_two', 120, 'A person holds a long spear horizontally in both hands, pointing it forwards, ready to thrust.'),
    ('spear_thrust_two', 120, 'A person thrusts a long spear forwards with both hands, steps into the thrust, then withdraws it.'),
    ('throw', 120, 'A person throws a stone forwards with a right-handed overarm throw and follows through.'),
    ('sling_ready', 120, 'A person stands holding a sling in their right hand, ready to throw a stone.'),
    ('sling_whirl', 150, 'A person swings a sling continuously in a vertical circle beside their right shoulder, ready to throw.'),
    ('sling_release', 120, 'A person throws a stone forwards with a sling in their right hand and follows through.'),
    ('bow_ready', 120, 'A person stands holding a bow vertically in their left hand, with their right hand resting near the bowstring.'),
    ('bow_shoot', 180, 'A person raises a bow in their left hand, draws the string back to their cheek with their right hand, holds the aim briefly, releases an arrow and lowers the bow.'),
    ('gather', 120, 'A person bends down, reaches with their right hand to pick up a small object from the ground, and stands up.'),
    ('drink', 150, 'A person cups both hands, brings them to their mouth to drink water, and lowers their hands.'),
    ('eat', 150, 'A person brings a small piece of food to their mouth with their right hand, eats it and lowers their hand.'),
    ('work_knapping', 150, 'A person holds a stone in their left hand and repeatedly taps it with a small hammerstone in their right hand.'),
    ('work_cut', 150, 'A person holds a piece of wood in their left hand and repeatedly carves it with a small knife in their right hand.'),
    ('work_twist', 150, 'A person holds plant fibres in both hands near their waist and repeatedly twists and rolls them together to make cord.'),
    ('work_dig', 150, 'A person bends forwards and repeatedly pushes a digging stick into the ground with both hands and levers it backwards.'),
    ('carry_large', 120, 'A person stands carrying a heavy large rock in both hands in front of their waist, keeping both elbows bent.'),
    ('walk_back', 150, 'A person walks backwards steadily while keeping their chest and face pointing forwards.'),
    ('strafe_left', 150, 'A person sidesteps continuously to their left while keeping their chest and face pointing forwards.'),
    ('strafe_right', 150, 'A person sidesteps continuously to their right while keeping their chest and face pointing forwards.'),
    ('run_back', 150, 'A person jogs backwards steadily while keeping their chest and face pointing forwards.'),
    ('run_left', 150, 'A person runs sideways to their left with quick lateral shuffling steps, keeping their chest pointing forwards.'),
    ('run_right', 150, 'A person runs sideways to their right with quick lateral shuffling steps, keeping their chest pointing forwards.'),
]


def main():
    ROOT.mkdir(exist_ok=True)
    provenance = ROOT / 'model-provenance.json'
    if not provenance.exists():
        provenance.write_bytes((DEMO / 'model-provenance.json').read_bytes())
    spec = importlib.util.spec_from_file_location('kimodo_generate', REPO / 'scripts/run-kimodo-demo-generation.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    module.ROOT = ROOT
    module.CASES = [{'id': name, 'label': name, 'frames': frames, 'seed': 900 + i,
                     'prompt': prompt} for i, (name, frames, prompt) in enumerate(DESCRIPTIONS)]
    (ROOT / 'queue.json').write_text(json.dumps(module.CASES, indent=2))
    module.main()


if __name__ == '__main__':
    main()
