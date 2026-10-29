"""GitHub asset-pack transport using the existing Git credential helper.

Credentials stay in memory and are never printed, saved or placed in command
arguments. Release storage is separate from metered Git LFS storage.
"""
import http.client
import json
import os
import ssl
import subprocess
import time
import urllib.error
import urllib.parse
import urllib.request


class SafeRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, fp, code, msg, headers, new_url):
        result=super().redirect_request(request,fp,code,msg,headers,new_url)
        if result is not None and urllib.parse.urlparse(new_url).hostname!=urllib.parse.urlparse(request.full_url).hostname:
            result.remove_header('Authorization')
        return result


class GitHubAssets:
    def __init__(self, repository='killerfrog17-coder/WorldGen', optional_auth=False):
        self.repository=repository
        environment=dict(os.environ,GCM_INTERACTIVE='Never')
        result=subprocess.run(['git','-c','credential.interactive=false','credential','fill'],input='protocol=https\nhost=github.com\n\n',text=True,capture_output=True,env=environment)
        fields=dict(line.split('=',1) for line in result.stdout.splitlines() if '=' in line)
        self._token=fields.get('password')
        if not self._token and not optional_auth:
            raise RuntimeError('The existing GitHub credential helper has no usable account access.')
        # Windows' native trust store can obtain missing intermediate CA
        # certificates. Certificate and hostname checks remain mandatory.
        try:
            import truststore
        except ImportError:
            self.context=ssl.create_default_context()
        else:
            self.context=truststore.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
        self.opener=urllib.request.build_opener(SafeRedirect(),urllib.request.HTTPSHandler(context=self.context))

    def request(self, path, method='GET', body=None, binary=False):
        url=path if path.startswith('https://') else 'https://api.github.com'+path
        assert urllib.parse.urlparse(url).hostname=='api.github.com'
        data=None if body is None else json.dumps(body).encode('utf-8')
        request=urllib.request.Request(url,data=data,method=method,headers={'Accept':'application/octet-stream' if binary else 'application/vnd.github+json','User-Agent':'WorldGen-asset-versioning','X-GitHub-Api-Version':'2022-11-28'})
        if self._token:
            request.add_unredirected_header('Authorization','Bearer '+self._token)
        if body is not None:
            request.add_header('Content-Type','application/json')
        for attempt in range(3):
            try:
                response=self.opener.open(request,timeout=180)
                break
            except urllib.error.HTTPError as error:
                if method!='GET' or error.code<500 or attempt==2:
                    raise
            except (urllib.error.URLError,TimeoutError):
                # Keep certificate validation enabled on every attempt. Do not
                # repeat writes whose result may already have reached GitHub.
                if method!='GET' or attempt==2:
                    raise
            time.sleep(.5*(attempt+1))
        return response if binary else json.load(response)

    def download(self, pack):
        if self._token:
            return self.request('/repos/'+self.repository+'/releases/assets/'+str(pack['asset_id']),binary=True)
        url='https://github.com/'+self.repository+'/releases/download/'+urllib.parse.quote(pack['tag'],safe='')+'/'+urllib.parse.quote(pack['name'],safe='')
        return self.opener.open(urllib.request.Request(url,headers={'User-Agent':'WorldGen-asset-restoration'}),timeout=180)

    def upload(self, release, path, name):
        if not self._token:
            raise RuntimeError('Publishing assets requires the existing GitHub account.')
        base=release['upload_url'].split('{',1)[0]
        url=urllib.parse.urlparse(base+'?name='+urllib.parse.quote(name))
        assert url.hostname=='uploads.github.com'
        connection=http.client.HTTPSConnection(url.hostname,timeout=1800,context=self.context)
        try:
            connection.putrequest('POST',url.path+'?'+url.query)
            connection.putheader('Authorization','Bearer '+self._token)
            connection.putheader('Accept','application/vnd.github+json')
            connection.putheader('User-Agent','WorldGen-asset-versioning')
            connection.putheader('Content-Type','application/octet-stream')
            connection.putheader('Content-Length',str(path.stat().st_size))
            connection.endheaders()
            with path.open('rb') as stream:
                for block in iter(lambda:stream.read(1024*1024),b''):
                    connection.send(block)
            response=connection.getresponse()
            result=json.loads(response.read())
            if response.status not in (200,201):
                raise RuntimeError('GitHub asset upload HTTP '+str(response.status)+': '+str(result.get('message','')))
            return result
        finally:
            connection.close()
