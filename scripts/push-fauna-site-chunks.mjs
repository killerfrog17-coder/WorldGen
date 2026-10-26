// Recover an oversized Sites Git transfer without changing any source files.
// The short-lived credential is accepted only on hidden stdin, never persisted.
import {spawnSync} from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
process.stdout.write('Ready for chunked Site transfer JSON on stdin (input is hidden).\n');
if(process.stdin.isTTY)process.stdin.setRawMode(true);
const input=await new Promise(resolve=>{let text='';process.stdin.setEncoding('utf8');process.stdin.on('data',chunk=>{text+=chunk;const end=text.search(/[\r\n]/);if(end>=0)resolve(text.slice(0,end))});process.stdin.resume()});
if(process.stdin.isTTY)process.stdin.setRawMode(false);process.stdin.pause();
let credential;
try{
 const config=JSON.parse(input);credential=config.credential;
 const project='appgprj_6ade6fbe0a5081918b25bcbf541da095',base=config.base;
 if(!/^[a-f0-9]{40}$/.test(base??''))throw Error('An exact previously opened source commit is required.');
 if(JSON.parse(fs.readFileSync('.openai/hosting.json')).project_id!==project)throw Error('Wrong Site checkout.');
 if(credential.branch!=='main'||!credential.remote_url.includes(project)||!credential.token)throw Error('Unexpected credential destination.');
 const env={...process.env,GIT_TERMINAL_PROMPT:'0',SITES_GIT_AUTHORIZATION:`Authorization: Bearer ${credential.token}`};
 for(const key of Object.keys(env))if(key.startsWith('GIT_TRACE')||key==='GIT_CURL_VERBOSE')delete env[key];
 function git(args,options={}){
  const auth=options.network?['-c','credential.helper=','-c','http.extraHeader=','-c','http.followRedirects=false',`--config-env=http.${credential.remote_url}.extraHeader=SITES_GIT_AUTHORIZATION`]:[];
  const r=spawnSync('git',[...auth,...args],{encoding:'utf8',env:{...env,...options.env},input:options.input,maxBuffer:16*1024*1024});
  if(r.status!==0)throw Error((r.stderr||'Git transfer failed.').split(credential.token).join('[redacted]'));
  return r.stdout.trimEnd();
 }
 if(git(['status','--porcelain']))throw Error('Preserve outstanding source edits before splitting upload.');
 const statePath=path.resolve(`../fauna-phone-media/checks/source-upload-chunks-${base.slice(0,12)}.json`);let state;
 if(fs.existsSync(statePath))state=JSON.parse(fs.readFileSync(statePath));
 else{
  const full=git(['rev-parse','HEAD']);if(git(['rev-parse',`${full}^`])!==base)throw Error('Unexpected checkpoint ancestry.');
  git(['update-ref',`refs/heads/phone-review-before-split-${base.slice(0,12)}`,full]);
  const changed=new Set(git(['diff','--name-only',base,full]).split('\n'));
  const entries=git(['ls-tree','-r','-l','-z',full]).split('\0').filter(Boolean).map(line=>{const [header,file]=line.split('\t');const [mode,type,oid,bytes]=header.trim().split(/\s+/);return {mode,oid,file,bytes:Number(bytes)}}).filter(e=>changed.has(e.file));
  const deleted=git(['diff','--name-only','--diff-filter=D','-z',base,full]).split('\0').filter(Boolean);
  const groups=[];let group=[],size=0;
  for(const entry of entries){const bytes=entry.bytes;if(bytes>8*1024*1024)throw Error('A single source asset exceeds this transfer batch size.');if(size+bytes>8*1024*1024&&group.length){groups.push(group);group=[];size=0}group.push(entry);size+=bytes}if(group.length)groups.push(group);
  const index=path.resolve('.git/phone-media-upload.index'),indexEnv={GIT_INDEX_FILE:index};let parent=base;const commits=[];
  try{
   git(['read-tree',base],{env:indexEnv});
   if(deleted.length)git(['update-index','--force-remove','--',...deleted],{env:indexEnv});
   for(let i=0;i<groups.length;i++){
    git(['update-index','-z','--index-info'],{env:indexEnv,input:groups[i].map(e=>`${e.mode} ${e.oid}\t${e.file}\0`).join('')});
    const tree=git(['write-tree'],{env:indexEnv});parent=git(['-c','user.name=Sites','-c','user.email=sites@users.noreply.openai.com','commit-tree',tree,'-p',parent],{input:`Add animal review media batch ${i+1} of ${groups.length}\n`});commits.push(parent);
   }
  }finally{if(fs.existsSync(index))fs.unlinkSync(index)}
  if(git(['rev-parse',`${parent}^{tree}`])!==git(['rev-parse',`${full}^{tree}`]))throw Error('Split source tree differs from checked source.');
  git(['update-ref','refs/heads/phone-media-upload-chunks',parent]);state={base,full,commits,final:parent};fs.writeFileSync(statePath,JSON.stringify(state,null,2)+'\n');
 }
 const remote=git(['ls-remote','--heads',credential.remote_url,'refs/heads/main'],{network:true}).split(/\s+/)[0];const offset=remote===state.base?0:state.commits.indexOf(remote)+1;if(!offset&&remote!==state.base)throw Error('Remote advanced outside this upload; reconcile first.');
 for(let i=offset;i<state.commits.length;i++){git(['push',credential.remote_url,`${state.commits[i]}:refs/heads/main`],{network:true});console.log(`Media transfer ${i+1}/${state.commits.length} complete.`)}
 if(git(['ls-remote','--heads',credential.remote_url,'refs/heads/main'],{network:true}).split(/\s+/)[0]!==state.final)throw Error('Remote verification failed.');
 const current=git(['rev-parse','HEAD']);if(current!==state.final)git(['update-ref','refs/heads/main',state.final,state.full]);
 if(git(['status','--porcelain']))throw Error('Source changed during transfer.');
 console.log(JSON.stringify({project_id:project,commit_sha:state.final,batches:state.commits.length,source_tree_preserved:true}));
}catch(error){console.error(String(error.message).split(credential?.token||'\0').join('[redacted]'));process.exitCode=1}
