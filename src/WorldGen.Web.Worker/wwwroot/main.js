import { dotnet } from './_framework/dotnet.js';
const runtime = await dotnet.withDiagnosticTracing(false).create();
runtime.setModuleImports('worldgen', { progress: (stage, percent) => self.postMessage({ type: 'progress', stage, percent }), preview: json => self.postMessage({type:'preview',json}) });
const api = (await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName)).WorkerApi;
let viewTicket = 0;
self.onmessage = async event => {
    const d = event.data;
    try {
        if (d.recipe) self.postMessage({ type: 'complete', json: api.Generate(d.recipe) });
        else if (d.type === 'load-preview') self.postMessage({type:'preview-ready',json:api.LoadPreview(d.json)});
        else if (d.type === 'load-world') self.postMessage({ type: 'complete', json: api.LoadWorld(d.json) });
        else if (d.type === 'load-world-file') self.postMessage({ type: 'complete', json: api.LoadWorldFile(d.data) });
        else if (d.type === 'select-layer') api.SelectLayer(d.layer);
        else if (d.type === 'palette') { const bytes=api.Palette(d.layer);self.postMessage({type:'palette',bytes,layer:d.layer},[bytes.buffer]); }
        else if (d.type === 'cached-view') {
            const ticket=++viewTicket;api.BeginCachedView(d.options);
            for(const layer of new Set([4,0,1,2,3,38,100,1000,1001,JSON.parse(d.options).Layer])){
                const bytes=api.Palette(layer);if(bytes.length)self.postMessage({type:'view-piece',bytes,ticket:d.ticket},[bytes.buffer]);
                await new Promise(resolve=>setTimeout(resolve,0));if(ticket!==viewTicket)return;
            }
            while(ticket===viewTicket){
                const bytes=api.NextCachedView();
                if(!bytes.length){self.postMessage({type:'view-complete',ticket:d.ticket});break;}
                self.postMessage({type:'view-piece',bytes,ticket:d.ticket},[bytes.buffer]);await new Promise(resolve=>setTimeout(resolve,0));
            }
        }
        else if (d.type === 'view') {
            const ticket = ++viewTicket; api.BeginView(d.options);
            // Pull one bounded C# mesh piece at a time. Yield so view cancellation
            // and replacement requests can be handled between pieces.
            while (ticket === viewTicket) {
                const bytes = api.NextView();
                if (!bytes.length) { self.postMessage({ type: 'view-complete', ticket: d.ticket }); break; }
                self.postMessage({ type: 'view-piece', bytes, ticket: d.ticket }, [bytes.buffer]);
                await new Promise(resolve => setTimeout(resolve, 0));
            }
        } else if (d.type === 'cancel-view') viewTicket++;
        else if(d.type==='people'){const json=api.People(d.command,d.value||0);if(d.command==='lore'||d.command==='calendar'||d.command.startsWith('atlas')){self.postMessage({type:'lore',text:json,requestId:d.requestId});return;}const bytes=JSON.parse(json).ClockOnly?new Uint8Array():api.Palette(100);self.postMessage({type:'people',json,bytes,requestId:d.requestId},[bytes.buffer]);}
        else if (d.type === 'point'||d.type==='pick') {self.postMessage({ type: 'inspection', text: d.type==='point'?api.DescribePoint(d.x,d.y,d.z):api.Describe(d.cell) });if(d.people){const json=api.People('status',0),bytes=api.Palette(100);self.postMessage({type:'people',json,bytes},[bytes.buffer]);}}
        else if (d.type === 'export') self.postMessage({ type: 'export', kind: d.kind, requestId: d.requestId, data: api.Export(d.kind) });
    } catch (error) { self.postMessage({ type: 'error', message: String(error) }); }
};
self.postMessage({ type: 'ready' });
