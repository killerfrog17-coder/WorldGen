"""Restore the exact asset set recorded by the checked-out source version.

python scripts/restore-game-assets.py
python scripts/restore-game-assets.py --verify-only

Downloads are temporary. No local cache of historical packs is retained.
Changed local assets require explicit --replace; personal saves are untouched.
"""
import argparse
import concurrent.futures
import hashlib
import json
import re
import zipfile
from pathlib import Path,PurePosixPath

from game_asset_inventory import ROOT,sha
from github_asset_api import GitHubAssets


def target_path(root,relative,read_only=False):
    path=PurePosixPath(relative)
    if not path.parts or path.is_absolute() or '..' in path.parts or any(':' in p or '\\' in p or '\0' in p for p in path.parts):
        raise ValueError('Unsafe asset path: '+relative)
    target=root.joinpath(*path.parts)
    if not read_only and not target.resolve().is_relative_to(root):
        raise ValueError('Asset target leaves the checkout through a directory link: '+relative)
    return target


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--lock',type=Path,default=ROOT/'assets/game-assets.lock.json')
    parser.add_argument('--destination',type=Path,default=ROOT)
    parser.add_argument('--subset',choices=['all','runtime','authoring'],default='all')
    parser.add_argument('--verify-only',action='store_true')
    parser.add_argument('--replace',action='store_true')
    parser.add_argument('--report',type=Path)
    args=parser.parse_args()
    manifest=json.loads(args.lock.read_text(encoding='utf-8'))
    if manifest['version']!=1 or not re.fullmatch(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+',manifest['repository']):
        raise ValueError('Unknown asset lock schema or repository')
    destination=args.destination.resolve()
    records={p:r for p,r in manifest['files'].items() if args.subset=='all' or p.startswith('artifacts/')==(args.subset=='authoring')}
    targets={p:target_path(destination,p,read_only=True) for p in records}
    def present(path):
        target=targets[path];record=records[path]
        return target.is_file() and target.stat().st_size==record['bytes'] and sha(target)==record['sha256']
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        validity=dict(zip(records,pool.map(present,records)))
    pending={p:r for p,r in records.items() if not validity[p]}
    if args.verify_only:
        report={'files':len(records),'passed':sum(validity.values()),'missing_or_changed':list(pending),'manifest_sha256':sha(args.lock)}
        if args.report:
            args.report.parent.mkdir(parents=True,exist_ok=True);args.report.write_text(json.dumps(report,indent=2))
        print(json.dumps(report if len(pending)<10 else {**report,'missing_or_changed':list(pending)[:10],'missing_count':len(pending)}))
        return 1 if pending else 0
    changed=[p for p in pending if targets[p].exists()]
    if changed and not args.replace:
        raise RuntimeError('Local assets differ; commit/publish the changes or use --replace explicitly: '+', '.join(changed[:5]))
    for path in pending:
        targets[path]=target_path(destination,path)
    destination.mkdir(parents=True,exist_ok=True)
    staging=destination/'.game-asset-download'
    if not staging.resolve().is_relative_to(destination):
        raise ValueError('The download staging directory leaves the checkout through a link')
    staging.mkdir(exist_ok=True)
    client=GitHubAssets(manifest['repository'],optional_auth=True)
    pack_keys=sorted({r['pack'] for r in pending.values()})
    restored=0
    for index,key in enumerate(pack_keys):
        pack=manifest['packs'][key]
        download=staging/'current.pack'
        if download.exists():
            download.unlink()
        expected=[p for p,r in pending.items() if r['pack']==key]
        h=hashlib.sha256();size=0
        with client.download(pack) as response,download.open('wb') as stream:
            for block in iter(lambda:response.read(1024*1024),b''):
                size+=len(block)
                if size>pack['bytes']:
                    raise ValueError('Remote asset exceeds its locked length')
                h.update(block);stream.write(block)
        if size!=pack['bytes'] or h.hexdigest()!=pack['sha256']:
            raise ValueError('Remote asset pack differs from the checked-out lock')
        archive=zipfile.ZipFile(download) if pack['encoding']=='zip' else None
        try:
            for path in expected:
                record=pending[path];target=targets[path];target.parent.mkdir(parents=True,exist_ok=True)
                temporary=target.with_name(target.name+'.asset-pending')
                if temporary.exists():
                    raise RuntimeError('Inspect a prior unfinished restore before replacing: '+str(temporary))
                member=record['member']
                if archive is not None and member!='objects/'+record['sha256']:
                    raise ValueError('Invalid pack member')
                h=hashlib.sha256();written=0
                try:
                    with (archive.open(member) if archive is not None else download.open('rb')) as source,temporary.open('xb') as stream:
                        for block in iter(lambda:source.read(1024*1024),b''):
                            written+=len(block)
                            if written>record['bytes']:
                                raise ValueError('Asset exceeds its locked length')
                            h.update(block);stream.write(block)
                    if written!=record['bytes'] or h.hexdigest()!=record['sha256']:
                        raise ValueError('Restored file differs: '+path)
                    temporary.replace(target);restored+=1
                except:
                    temporary.unlink(missing_ok=True)
                    raise
        finally:
            if archive is not None:
                archive.close()
        download.unlink()
        print(json.dumps({'pack':index+1,'packs':len(pack_keys),'restored':restored,'files':len(pending)}),flush=True)
    staging.rmdir()
    report={'files':len(records),'unchanged':len(records)-len(pending),'restored':restored,'all_payloads_and_files_sha256_verified':True,'manifest_sha256':sha(args.lock)}
    if args.report:
        args.report.parent.mkdir(parents=True,exist_ok=True);args.report.write_text(json.dumps(report,indent=2))
    print(json.dumps(report))
    return 0


if __name__=='__main__':
    raise SystemExit(main())
