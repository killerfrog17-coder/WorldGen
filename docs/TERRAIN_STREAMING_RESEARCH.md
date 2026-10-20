# Terrain loading: research and focused correction

## Primary sources

- [Losasso/Hoppe: Geometry Clipmaps](https://hhoppe.com/geomclipmap.pdf)
  and [GPU implementation](https://developer.nvidia.com/gpugems/gpugems2/part-i-geometric-complexity/chapter-2-terrain-rendering-using-gpu-based-geometry):
  persistent nested grids, incremental newly exposed strips, bounded updates,
  and transitions between resolutions. A full clipmap renderer would be a
  deliberate replacement, not a quick scheduler patch.
- [Strugar: CDLOD paper and implementation](https://github.com/fstrugar/CDLOD):
  quadtree regular grids with continuous distance-based refinement and morphing.
  The existing quadtree is not inherently the reason nearby geometry loads late.
- [Cesium Native tile selection](https://cesium.com/learn/cesium-native/ref-doc/selection-algorithm-details.html):
  distinct render/load decisions, asynchronous prioritized work, bounded loading
  lanes and main-thread budgets, retained renderable coverage during replacement.
- [Unity writable mesh data](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Mesh.AllocateWritableMeshData.html):
  worker-accessible mesh buffers and batched application are an available later
  optimization, not evidence that rewriting the sampler into Burst fixes latency.

## Concrete findings in this repository

The active chart refreshed its loading plan after 1024 physical metres regardless
of horizontal scale. At default 0.1 scale a finest tile was only 204.8 metres wide.
The player could therefore pass five tiles before the plan noticed. Two worker
lanes were shared with scatter and hidden chart preparation; overdue hidden chart
work could suppress active-chart terrain scheduling altogether. Completed terrain
and fallback coverage were already retained, but that does not fix these delays.

## Small correction, not a new terrain engine

- Replan at one quarter of the finest tile width (25.6 metres at default scale).
- Split the finest work unit into 1024/64 instead of 2048/128. This preserves
  the original full-detail sample spacing (1.6 metres at default scale); it does
  not make heights or the near ground coarser. Distant physical tile sizes stay.
- Keep the immediate neighbourhood full detail, including below a flying viewer.
- Active near terrain has priority over scatter and hidden-chart preparation.
  If both lanes are blocked by background work, cancel only one job, with actual
  cancellation points during expensive triangulation. Do not repeatedly restart
  all background work. Obsolete active requests are also cancelled.
- Retain the accepted render coverage/handoff behavior and underlying height,
  hydrology, collision-query and scatter-placement definitions.

This is not full CDLOD or a GPU clipmap implementation. No universal instant-load
guarantee follows from a paper or an assertion count. A short native check times
the full-detail near ring and successive boundary crossings; no all-era campaign
or repeated screenshot cycle is needed for this narrow correction. River visuals
remain a separate, explicitly deferred issue.
