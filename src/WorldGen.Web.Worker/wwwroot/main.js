import { dotnet } from './_framework/dotnet.js';
const runtime = await dotnet.withDiagnosticTracing(false).create();
runtime.setModuleImports('worldgen', { progress: (stage, percent) => self.postMessage({ type: 'progress', stage, percent }) });
const api = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
self.onmessage = event => {
    try { self.postMessage({ type: 'complete', json: api.WorkerApi.Generate(event.data.recipe) }); }
    catch (error) { self.postMessage({ type: 'error', message: String(error) }); }
};
self.postMessage({ type: 'ready' });
