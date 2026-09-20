import { dotnet } from './_framework/dotnet.js';
const runtime = await dotnet.withDiagnosticTracing(false).create();
runtime.setModuleImports('worldgen', { progress: (stage, percent) => self.postMessage({ type: 'progress', stage, percent }) });
const api = (await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName)).WorkerApi;
let viewTicket = 0;
self.onmessage = async event => {
    const d = event.data;
    try {
        if (d.recipe) self.postMessage({ type: 'complete', json: api.Generate(d.recipe) });
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
        else if (d.type === 'point') self.postMessage({ type: 'inspection', text: api.DescribePoint(d.x,d.y,d.z) });
        else if (d.type === 'pick') self.postMessage({ type: 'inspection', text: api.Describe(d.cell) });
        else if (d.type === 'export') self.postMessage({ type: 'export', kind: d.kind, data: api.Export(d.kind) });
    } catch (error) { self.postMessage({ type: 'error', message: String(error) }); }
};
self.postMessage({ type: 'ready' });
