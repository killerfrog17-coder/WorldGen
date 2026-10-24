"""Use Riley's public 'No thanks, just take me to downloads' free flow.

Fresh anonymous cookies only. Never submits /purchase, payment, email or login.
"""
import hashlib,http.cookiejar,json,re,urllib.parse,urllib.request,zipfile
from html.parser import HTMLParser
from pathlib import Path
base='https://rkuhlf-assets.itch.io/ice-age';root=Path(__file__).resolve().parent.parent/'artifacts/fauna-motion-sources/ice-age-source';root.mkdir(exist_ok=True)
client=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
html=client.open(base+'/purchase',timeout=30).read().decode()
assert '"actual_price":0' in html and '"min_price":0' in html
class Parse(HTMLParser):
    def __init__(self):super().__init__();self.token=None;self.rows=[];self.scripts=[];self.current_id=None;self.zip_id=None
    def handle_starttag(self,tag,attrs):
        d=dict(attrs)
        if tag=='meta' and d.get('name')=='csrf_token':self.token=d.get('value')
        if 'upload' in str(d) or 'download_btn' in str(d):self.rows.append((tag,{k:v for k,v in d.items() if k!='csrf_token'}))
        if tag=='script' and 'src' in d:self.scripts.append(d['src'])
        if 'data-upload_id' in d:self.current_id=d['data-upload_id']
    def handle_data(self,data):
        if data.strip()=='IceAge.zip':self.zip_id=self.current_id
p=Parse();p.feed(html);assert p.token
payload=urllib.parse.urlencode({'csrf_token':p.token}).encode()
req=urllib.request.Request(base+'/download_url',data=payload,headers={'X-Requested-With':'XMLHttpRequest','Referer':base+'/purchase'})
response=json.load(client.open(req,timeout=30));url=urllib.parse.urljoin(base,response['url']);assert urllib.parse.urlparse(url).hostname=='rkuhlf-assets.itch.io'
download=client.open(url,timeout=30).read().decode();(root/'public-download-page.html').write_text(download,encoding='utf-8')
p=Parse();p.feed(download);print('PUBLIC DOWNLOAD ROWS',p.rows);print('PUBLIC SCRIPTS',p.scripts)
assert p.zip_id and p.token
query={'source':'game_download'}
for match in re.finditer(r'\{[^{}]*"key"[^{}]*\}',download):
    try:
        options=json.loads(match.group(0))
        if options.get('key'):query['key']=options['key'];break
    except json.JSONDecodeError:pass
request=urllib.request.Request(base+'/file/'+p.zip_id+'?'+urllib.parse.urlencode(query),data=urllib.parse.urlencode({'csrf_token':p.token}).encode(),headers={'X-Requested-With':'XMLHttpRequest','Referer':url})
result=json.load(client.open(request,timeout=30))
assert result.get('url') and not result.get('errors') and not result.get('lightbox'), 'Normal free download not available; no bypass attempted'
link=result['url'];assert urllib.parse.urlparse(link).scheme=='https'
archive=root/'IceAge.zip'
if not archive.exists():archive.write_bytes(client.open(link,timeout=120).read())
with zipfile.ZipFile(archive) as z:
    for item in z.infolist():assert (root/'download'/item.filename).resolve().is_relative_to((root/'download').resolve())
    z.extractall(root/'download')
print('Downloaded free IceAge.zip',archive.stat().st_size,'bytes')
(root/'free-flow-status.json').write_text(json.dumps({'source':base,'license':'CC0 verified on creator page','payment_submitted':False,'download_page_ready':True},indent=2))
(root/'download-provenance.json').write_text(json.dumps({'source':base,'license':'CC0 verified on creator page','payment_submitted':False,'file':'IceAge.zip','sha256':hashlib.sha256(archive.read_bytes()).hexdigest()},indent=2))
