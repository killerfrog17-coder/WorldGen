# Continental Complex Redesign Requirements

## Status and purpose

Owner clarification, 1 October 2026: reject polygonal base outlines and broad territorial clipping. Complex members should be independently recognizable shapes placed with only a minor connection or almost touching: North/South America, the Bering near-connection, and Eurasia/Africa illustrate the intended relationships. Tests must allow real narrow bridges, not enforce separate landmasses for every identity.

This document is an owner-approved requirements handoff for redesigning continent and continental-complex generation. It describes the result the owner wants, the user-facing inputs, required semantics, examples, constraints, and acceptance criteria.

It intentionally does **not** prescribe the generation algorithm. The implementing agent is expected to inspect the current code and required repository documentation, identify an appropriate deterministic design, implement it through the authoritative pipeline, and verify the result.

## Core objective

Keep the existing distinction between:

- the total number of continents; and
- the total number of continental complexes.

Add explicit control over how many continents belong to each complex.

The current behavior is insufficient when complex membership merely makes several continent identities behave like one undifferentiated continent. A complex must instead contain recognizable, distinct continental shapes that occupy the same broad geographic neighborhood.

The initial version should support direct custom input only. Named map-type presets such as Earthlike, Pangaea, or Separated Continents are deferred. Those presets may later become shortcuts that populate the same underlying custom settings.

## Required terminology

### Continent

A continent is one recognizable, primary macro-scale land shape and identity.

Physical land connectivity does not, by itself, determine continent identity:

- Two continents may be connected by a narrow isthmus or land bridge and remain distinct continents.
- Two continents may be separated by a narrow strait or relatively small sea and remain distinct continents.
- Two broadly fused regions without a meaningful geographic bottleneck should generally be treated as one continent for this generator's purposes.

Under this definition, Eurasia is one continent. Europe and Asia are not separate continent identities because together they form one broad, continuous primary shape without a comparable narrow connector.

Africa is a distinct continent despite its narrow land connection to Eurasia. North America and South America are distinct despite the Central American connection. North America and Eurasia may belong to the same complex despite being separated around the Bering region.

Continents are not required to equal final connected landmass components. Final coastline detail, islands, narrow channels, and terrain variation must not erase continent identity or redefine the requested continent count.

### Continental complex

A continental complex is a geographically coherent grouping of one or more continents.

Continents in the same complex should generally be close to one another. Their relationships may include:

- a narrow land connection;
- a land bridge or isthmus;
- a narrow strait;
- a relatively small intervening sea; or
- another clearly close geographic arrangement.

Not every pair of continents within a complex must touch or be directly adjacent. The complex as a whole must nevertheless read as one coherent continental neighborhood rather than unrelated continents assigned the same hidden label.

A complex containing multiple continents must preserve those continents as visibly distinct primary shapes. Complex membership must not simply fuse all members into one broad shape that would reasonably be interpreted as a single continent.

A singleton complex containing one continent is valid.

### Separation between complexes

Different continental complexes should be meaningfully separated by ocean at the macro scale. They should not accidentally form one obvious geographic neighborhood through broad contact, repeated land bridges, or only a trivial gap.

The exact distance and geometric method are implementation decisions, but the generated result must visually and structurally distinguish separate complexes from separate continents inside the same complex.

## Required user inputs

The custom world recipe must allow the user to specify all three of the following:

1. **Continent count**: the total requested number of continents.
2. **Continental complex count**: the total requested number of complexes.
3. **Continents per complex**: an explicit ordered list of positive integers describing the complex composition.

Example:

```text
Continents: 6
Continental complexes: 3
Continents per complex: 4, 1, 1
```

The composition must obey these validity rules:

- The number of entries must equal the continental complex count.
- Every entry must be at least one.
- The entries must sum exactly to the total continent count.
- The complex count cannot exceed the continent count.
- Existing constraints involving available plates, land coverage, and recipe validity must remain enforced.

Invalid input must be rejected clearly rather than silently redistributed into a different composition.

The composition must be part of the complete recipe and therefore part of deterministic generation, serialization, reconstruction, caching, stage hashes, and host parity.

## Canonical examples

### Earthlike arrangement

For this system, the intended simplified Earth interpretation is:

```text
Continents: 6
Continental complexes: 3
Continents per complex: 4, 1, 1
```

The four-continent complex represents:

- Eurasia;
- Africa;
- North America; and
- South America.

Australia and Antarctica are singleton complexes.

This is an illustrative topology, not a request to reproduce Earth's exact geography. A generated `4,1,1` world should vary by seed while retaining the same high-level organization: one dominant four-continent neighborhood and two substantially isolated continents.

### Pangaea

```text
Continents: 1
Continental complexes: 1
Continents per complex: 1
```

This requests one primary supercontinental shape.

### Two fully distinct continents

```text
Continents: 2
Continental complexes: 2
Continents per complex: 1, 1
```

This requests two independently recognizable continents with meaningful ocean separation. They must not be connected by a land bridge or grouped into one close continental neighborhood.

### One complex containing several continents

```text
Continents: 4
Continental complexes: 1
Continents per complex: 4
```

This requests four recognizable continent shapes arranged into one coherent neighborhood. It does not request one four-lobed continent, nor does it require all four continents to be physically connected.

### Two populated complexes

```text
Continents: 7
Continental complexes: 2
Continents per complex: 4, 3
```

This requests two meaningfully separated continental neighborhoods. One must contain four recognizable continents and the other three.

## Required behavioral outcomes

Generation must satisfy the requested composition at the authoritative coarse continental level and preserve inspectable provenance through fine projection and final output.

For every generated world:

- The requested number of continent identities must exist, subject only to recipes that are invalid before generation.
- The requested number of complex identities must exist.
- Each complex must contain exactly its requested number of continent identities.
- Every continent must belong to exactly one complex.
- Continents within a multi-continent complex must form a coherent geographic grouping.
- Distinct continents within a complex must remain recognizable as distinct macro shapes.
- Singleton complexes must remain meaningfully separated from other complexes.
- Separate multi-continent complexes must remain meaningfully separated from one another.
- Variation between seeds must remain substantial; the composition is a topology requirement, not a fixed map template.
- Land coverage and continent-size variation must continue to influence area without violating the requested composition.
- Later terrain generation may add coastline detail and islands, but it must not destroy the requested macro organization.

The implementation must not claim success merely because internal IDs have the requested counts. The spatial result must express the requested grouping.

## UI requirements for the initial version

The initial UI should expose custom values directly. Preset selection is not required in this task.

The user must be able to enter the complex composition in a compact form such as:

```text
4,1,1
```

Equivalent separators or a structured editor are acceptable implementation choices, provided the final control is understandable, compact, and always visible within the existing settings panel constraints.

The UI must communicate invalid composition input before or when generation is requested. It must not silently rewrite `4,1,1` into a balanced or random distribution.

Recipe save/load must preserve the exact custom composition. Unity, CLI, and browser settings must remain semantically consistent.

## Inspectability and validation requirements

Generated output must retain enough information to inspect:

- each cell's continent identity;
- each continent's complex identity;
- the number of continents assigned to each complex;
- requested versus produced continent and complex counts; and
- whether required geographic grouping and separation checks passed.

Automated validation must cover, at minimum:

- `1` continent in composition `1`;
- `2` continents in composition `1,1`;
- `4` continents in composition `4`;
- `6` continents in composition `4,1,1`;
- `6` continents in composition `2,2,2`;
- `7` continents in composition `4,3`;
- malformed lists;
- lists with zero or negative entries;
- list length not matching complex count;
- list sum not matching continent count;
- deterministic fresh, reapply, serialized-recipe, and cached reconstruction results; and
- preservation of the fixed coarse partition contract when only fine-detail settings change.

Validation should test spatial outcomes, not only array lengths or ID counts. The implementing agent must define objective checks that distinguish coherent same-complex placement from meaningful between-complex separation.

## Non-goals

This task does not require:

- named map-type presets;
- automatic Earthlike, Pangaea, or fantasy-world preset selection;
- a historical plate-motion simulation;
- cultural definitions of continents;
- exact replication of Earth;
- persistent rivers, lakes, or hydrology;
- civilization or population systems; or
- replacement of the existing plate, terrain, climate, or rendering pipelines beyond changes necessary to satisfy these requirements.

## Repository and delivery constraints

Before implementation, follow the repository instructions in `AGENTS.md` and read the required generation documents, especially `OROGEN_ALIGNMENT.md`, `ALGORITHMS.md`, `FIRST_BUILD_SPEC.md`, and `REFERENCE_ANALYSIS.md`.

The implementation must preserve these project contracts:

- `WorldGen.Core` remains the sole authoritative generator.
- CLI, browser worker, and Unity continue to call `WorldPipeline`.
- Generation remains deterministic from seed, complete recipe, and algorithm version.
- New recipe state is serialized and validated consistently across hosts.
- Output-changing behavior receives an intentional algorithm-version update.
- Geological provenance and coarse/fine resolution separation remain inspectable.
- Persistent hydrology remains deferred.
- Golden changes are intentional and reviewed.
- Full core tests, solution build, CLI reconstruction verification, and relevant Unity/player checks are run.
- The standalone player is rebuilt and the root shortcut is explicitly verified after successful implementation.

## Implementation freedom

The implementing agent is responsible for determining how best to satisfy these requirements within the existing architecture. This document does not decide:

- how complex anchors are selected;
- how continent shapes are grown;
- how proximity or separation is measured;
- how land bridges, straits, or small seas emerge;
- how composition interacts internally with area budgeting;
- how spatial constraints are repaired; or
- which objective geometric thresholds should be used.

Those decisions should follow from inspection, measurement, deterministic design, and validation against the required observable behavior above.
