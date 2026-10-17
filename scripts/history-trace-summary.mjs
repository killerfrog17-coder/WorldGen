// Offline sampled-stack diagnosis only. No generation or simulation code.
import fs from 'node:fs';
const [input, output] = process.argv.slice(2);
if (!input) throw new Error('Usage: node history-trace-summary.mjs trace.speedscope.json [new-summary.json]');
if (output && fs.existsSync(output)) throw new Error('Refusing to overwrite an existing trace summary.');
const trace = JSON.parse(fs.readFileSync(input, 'utf8'));
const frames = trace.shared.frames.map(f => f.name);
const roots = ['AdvanceAggregateState', 'AdvanceAggregateRegion', 'AdvanceSeaMissions', 'AdvanceAggregateCity', 'AdvanceAggregateCulture', 'AdvanceAggregateCampaign'];
const rootIds = new Map(frames.flatMap((name, id) => roots.filter(root => name.includes(`Prehistory.${root}(`)).map(root => [id, root])));
const core = id => frames[id].startsWith('WorldGen.Core!');
const short = name => name.replace(/^WorldGen.Core!WorldGen.Core\./, '').replace(/\(.*/, '');
const add = (map, key, ms) => map.set(key, (map.get(key) ?? 0) + ms);
const groups = new Map(roots.map(root => [root, { milliseconds: 0, inclusive: new Map(), exclusiveCore: new Map(), externalLeaf: new Map(), hotCallers: new Map() }]));
let intervals = 0;
for (const profile of trace.profiles) {
  if (profile.type !== 'evented') throw new Error('This diagnostic expects evented Speedscope output.');
  const stack = [];
  let last = profile.startValue;
  for (const event of profile.events) {
    const dt = Math.max(0, event.at - last);
    const at = stack.findIndex(id => rootIds.has(id));
    if (dt > 0 && at >= 0) {
      const group = groups.get(rootIds.get(stack[at]));
      group.milliseconds += dt; intervals++;
      const methods = stack.slice(at).filter(core);
      for (const hot of ['HistoricalCellTree.Set', 'StateHash.Write', 'Prehistory.RememberInternationalRelation', 'Prehistory.NearbyLivingTowns']) {
        const hotAt=methods.findIndex(id=>short(frames[id])===hot);
        if(hotAt>=0){const caller=methods.slice(0,hotAt).reverse().find(id=>short(frames[id]).startsWith('Prehistory.'));add(group.hotCallers,`${hot} <- ${caller===undefined?'(inlined root)':short(frames[caller])}`,dt);}
      }
      for (const id of new Set(methods)) add(group.inclusive, short(frames[id]), dt);
      if (methods.length) add(group.exclusiveCore, short(frames[methods.at(-1)]), dt);
      const leaf = [...stack].reverse().find(id => frames[id] !== 'CPU_TIME' && frames[id] !== 'UNMANAGED_CODE_TIME');
      if (leaf !== undefined && !core(leaf)) add(group.externalLeaf, frames[leaf].replace(/\(.*/, ''), dt);
    }
    last = event.at;
    if (event.type === 'O') stack.push(event.frame);
    else {
      const closed = stack.pop();
      if (closed !== event.frame) throw new Error('Unbalanced stack events.');
    }
  }
}
const top = (map, total) => [...map].sort((a,b) => b[1] - a[1]).slice(0,25).map(([method,ms]) => ({ method, milliseconds: ms, percent: 100 * ms / Math.max(1,total) }));
const result = {
  input, warning: 'Managed .NET sampled thread time, not native Unity/Boehm timing. Child-inclusive rows overlap; do not sum them. Startup/load/validation are excluded by restricting to actor-event roots.', intervals,
  stages: [...groups].map(([stage,g]) => ({ stage, milliseconds:g.milliseconds, inclusive:top(g.inclusive,g.milliseconds), exclusiveCore:top(g.exclusiveCore,g.milliseconds), externalLeaf:top(g.externalLeaf,g.milliseconds),hotCallers:top(g.hotCallers,g.milliseconds) })).sort((a,b) => b.milliseconds-a.milliseconds)
};
if (output) fs.writeFileSync(output, JSON.stringify(result,null,2));
for (const stage of result.stages) {
  console.log(`\n${stage.stage}: ${stage.milliseconds.toFixed(1)} sampled ms`);
  console.table(stage.exclusiveCore.slice(0,12).map(r => ({ method:r.method, ms:r.milliseconds.toFixed(1), percent:r.percent.toFixed(1) })));
}
