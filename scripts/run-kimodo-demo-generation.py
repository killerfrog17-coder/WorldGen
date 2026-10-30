"""Generate the three requested movements locally, preserving raw Kimodo output."""
import argparse
import ast
import hashlib
import json
import os
from pathlib import Path
import subprocess
import time

import numpy as np

REPO = Path(__file__).resolve().parent.parent
ROOT = REPO / 'artifacts/kimodo-first-person-demo'
NATIVE = Path('E:/AI/WorldGen3D/kimodo-native-build')
MODELS = Path('E:/AI/WorldGen3D/models/Kimodo')
CASES = [
    {'id': 'empty_walk', 'label': 'Walk — empty hands', 'frames': 150, 'seed': 240,
     'prompt': 'A person walks forward at a relaxed steady pace with empty hands and natural alternating arm swings.'},
    {'id': 'spear_walk', 'label': 'Walk — carrying spear', 'frames': 150, 'seed': 241,
     'prompt': 'A person walks forward slowly holding a long spear in their right hand. The right elbow is bent and the spear is carried beside the body pointing forward. The left arm swings naturally.'},
    {'id': 'spear_throw', 'label': 'Throw spear', 'frames': 180, 'seed': 242,
     'prompt': 'A person stands holding a javelin in their right hand, raises it beside their head, draws the right arm back, steps forward with the left foot and throws the javelin straight forward overarm, following through and recovering balance.'},
]


def source_skeleton():
    """Read the official Apache-2.0 hierarchy and trusted neutral joint tensor."""
    import torch
    source = REPO / 'artifacts/tools/kimodo/kimodo'
    module = ast.parse((source / 'skeleton/definitions.py').read_text())
    cls = next(x for x in module.body if isinstance(x, ast.ClassDef) and x.name == 'SOMASkeleton30')
    assignment = next(x for x in cls.body if isinstance(x, ast.Assign) and
                      any(isinstance(t, ast.Name) and t.id == 'bone_order_names_with_parents' for t in x.targets))
    pairs = ast.literal_eval(assignment.value)
    names = [x[0] for x in pairs]
    joints = torch.load(source / 'assets/skeletons/somaskel30/joints.p', weights_only=True).squeeze().numpy()
    return {'names': names, 'parents': [-1 if p is None else names.index(p) for _, p in pairs],
            'neutral_joints': joints.tolist(), 'fps': 30, 'source': 'NVIDIA SOMASkeleton30'}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--case', choices=[x['id'] for x in CASES])
    parser.add_argument('--seed', type=int)
    parser.add_argument('--steps', type=int, default=50)
    parser.add_argument('--frames', type=int)
    parser.add_argument('--prompt')
    parser.add_argument('--encode-only', action='store_true')
    args = parser.parse_args()
    if not (ROOT / 'model-provenance.json').exists():
        raise RuntimeError('Finish the checked weight download first.')
    (ROOT / 'motions').mkdir(exist_ok=True)
    (ROOT / 'skeleton.json').write_text(json.dumps(source_skeleton(), indent=2))
    env = os.environ.copy()
    env['PATH'] = str(NATIVE / 'bin/Release') + os.pathsep + env['PATH']
    env['KIMODO_BACKEND'] = 'cpu'
    env['KIMODO_THREADS'] = '8'
    env['KIMODO_TEXT_LAYER_CHUNK'] = '32'
    encoder = MODELS / 'text/Llama-3-Kimodo-Q8_0.gguf'
    motion = MODELS / 'motion/models/kimodo-soma-rp-v1.1-f32.gguf'
    for case in CASES:
        if args.case and args.case != case['id']:
            continue
        case = dict(case)
        if args.frames:
            case['frames'] = args.frames
        if args.prompt:
            case['prompt'] = args.prompt
        folder = ROOT / 'motions' / case['id']
        folder.mkdir(exist_ok=True)
        prompt_hash = hashlib.sha256(case['prompt'].encode()).hexdigest()[:12]
        prompt = folder / ('prompt-' + prompt_hash + '.txt')
        prompt.write_text(case['prompt'], encoding='utf-8')
        embedding = folder / ('embedding-' + prompt_hash + '.f32')
        if not embedding.exists():
            started = time.monotonic()
            with (folder / 'encode.log').open('w') as log:
                subprocess.run([str(NATIVE / 'Release/kmd-encode.exe'), str(encoder), str(prompt), str(embedding)],
                               env=env, stdout=log, stderr=subprocess.STDOUT, check=True)
            print('ENCODED', case['id'], round(time.monotonic() - started, 2), flush=True)
        if args.encode_only:
            continue
        # The sampler takes our cached encoder output and explicit random noise.
        # Its output remains raw model motion; contact/grips are a separate pass.
        seed = args.seed if args.seed is not None else case['seed']
        take = folder / ('seed-' + str(seed))
        if (take / 'generation.json').exists():
            print('REUSE', str(take), flush=True)
            continue
        take.mkdir(exist_ok=True)
        noise = np.random.default_rng(seed).standard_normal((case['frames'], 369)).astype('<f4')
        noise_path = take / 'initial-noise.f32'
        noise.tofile(noise_path)
        started = time.monotonic()
        with (take / 'generate.log').open('w') as log:
            subprocess.run([str(NATIVE / 'Release/kmd-sample-embedding.exe'), str(motion), str(embedding), str(noise_path),
                            str(case['frames']), str(args.steps), str(take)],
                           env=env, stdout=log, stderr=subprocess.STDOUT, check=True)
        roots = np.fromfile(take / 'root_positions.f32', dtype='<f4').reshape(case['frames'], 3)
        rotations = np.fromfile(take / 'local_rotations_xyzw.f32', dtype='<f4').reshape(case['frames'], 30, 4)
        if not np.isfinite(roots).all() or not np.isfinite(rotations).all():
            raise RuntimeError('Nonfinite generated motion')
        record = {**case, 'seed': seed, 'steps': args.steps, 'seconds': time.monotonic() - started,
                  'model': 'Kimodo-SOMA-RP-v1.1', 'runtime': 'kimodo.cpp', 'fps': 30,
                  'initial_noise_sha256': hashlib.sha256(noise.tobytes()).hexdigest(),
                  'root_start': roots[0].tolist(), 'root_end': roots[-1].tolist(),
                  'raw_rotation_sha256': hashlib.sha256(rotations.tobytes()).hexdigest()}
        (take / 'generation.json').write_text(json.dumps(record, indent=2))
        print('GENERATED', case['id'], seed, round(record['seconds'], 2), flush=True)
    print('KIMODO AUDITION GENERATION FINISHED', flush=True)


if __name__ == '__main__':
    main()
