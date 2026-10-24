"""Download one verified public asset link; extract only inside its source root."""
import argparse,hashlib,json,re,urllib.parse,urllib.request,zipfile
from pathlib import Path
ap=argparse.ArgumentParser();ap.add_argument('--page',required=True);ap.add_argument('--file',required=True);ap.add_argument('--root',type=Path,required=True)
a=ap.parse_args();root=a.root.resolve();root.mkdir(parents=True,exist_ok=True)
request=urllib.request.Request(a.page,headers={'User-Agent':'Mozilla/5.0 WorldGen asset research'})
html=urllib.request.urlopen(request,timeout=60).read().decode('utf-8')
links=[urllib.parse.urljoin(a.page,x) for x in re.findall(r'href=[\"\x27]([^\"\x27]+)',html)]
url=next(x for x in links if urllib.parse.unquote(urllib.parse.urlparse(x).path).rsplit('/',1)[-1]==a.file)
assert urllib.parse.urlparse(url).netloc=='opengameart.org'
target=root/a.file
if not target.exists():target.write_bytes(urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'Mozilla/5.0'}),timeout=120).read())
if target.suffix.lower()=='.zip':
    with zipfile.ZipFile(target) as z:
        for member in z.infolist():
            resolved=(root/'download'/member.filename).resolve();assert resolved.is_relative_to(root/'download')
        z.extractall(root/'download')
(root/'download-provenance.json').write_text(json.dumps({'page':a.page,'asset_url':url,'file':str(target),'sha256':hashlib.sha256(target.read_bytes()).hexdigest(),'license':'CC0 verified on creator source page','donor_geometry_admitted':False},indent=2))
print(target)
