// Host storage only. Complete compressed snapshots are produced/validated by .NET.
const databaseName='worldgen-worlds';
function database(){return new Promise((resolve,reject)=>{
 const request=indexedDB.open(databaseName,1);
 request.onupgradeneeded=()=>request.result.createObjectStore('worlds',{keyPath:'id'});
 request.onsuccess=()=>resolve(request.result);
 request.onerror=()=>reject(new Error('World storage is unavailable: '+request.error?.message));
 request.onblocked=()=>reject(new Error('Close other WorldGen tabs and try again.'));
});}
async function transaction(mode,action){const db=await database();try{return await new Promise((resolve,reject)=>{
 const tx=db.transaction('worlds',mode);let result;
 try{const request=action(tx.objectStore('worlds'));request.onsuccess=()=>result=request.result;}catch(error){reject(error);return;}
 tx.oncomplete=()=>resolve(result);
 tx.onerror=tx.onabort=()=>reject(new Error('Could not access world storage: '+(tx.error?.message||'storage full or unavailable')));
});}finally{db.close();}}
export async function list(){const db=await database();try{return await new Promise((resolve,reject)=>{
 const rows=[],tx=db.transaction('worlds','readonly'),request=tx.objectStore('worlds').openCursor();
 request.onsuccess=()=>{const cursor=request.result;if(cursor){const {data,...metadata}=cursor.value;if(!metadata.deleted){metadata.groupId ||= metadata.id;rows.push(metadata);}cursor.continue();}};
 tx.oncomplete=()=>resolve(rows.sort((a,b)=>b.savedUtc.localeCompare(a.savedUtc)||a.id.localeCompare(b.id)));
 tx.onerror=tx.onabort=()=>reject(new Error('Could not list saved worlds: '+tx.error?.message));
 });}finally{db.close();}}
export async function write(name,base64,summary,groupId=null){name=name.trim();if(!name||name.length>80)throw new Error('Enter a world name of 1–80 characters.');
 const data=new Blob([Uint8Array.from(atob(base64),c=>c.charCodeAt(0))],{type:'application/octet-stream'});
 const record={id:crypto.randomUUID(),name,savedUtc:new Date().toISOString(),cells:summary.CellCount,algorithm:summary.Recipe.AlgorithmVersion,data};
 record.groupId=groupId||record.id;await transaction('readwrite',store=>store.add(record));
 // Permission is advisory: saves still succeed if persistent storage is declined.
 try{await navigator.storage?.persist?.();}catch{}
 return record.id;
}
export async function remove(id,group=false){
 const db=await database();try{await new Promise((resolve,reject)=>{
  const tx=db.transaction('worlds','readwrite'),request=tx.objectStore('worlds').openCursor();
  request.onsuccess=()=>{const cursor=request.result;if(!cursor)return;const row=cursor.value;if(group?(row.groupId||row.id)===id:row.id===id){row.deleted=new Date().toISOString();cursor.update(row);}cursor.continue();};
  tx.oncomplete=resolve;tx.onerror=tx.onabort=()=>reject(new Error('Could not delete save.'));
 });}finally{db.close();}
}
export async function read(id){const row=await transaction('readonly',store=>store.get(id));if(!row)throw new Error('This world is no longer in the library.');
 const bytes=row.data instanceof Blob?new Uint8Array(await row.data.arrayBuffer()):row.data;
 let binary='';for(let offset=0;offset<bytes.length;offset+=32768)binary+=String.fromCharCode(...bytes.subarray(offset,offset+32768));
 return btoa(binary);
}
