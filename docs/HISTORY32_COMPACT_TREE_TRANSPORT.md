# Exact compact persistent-tree transport — 17 October 2026

This is a storage optimization, not a historical rule change. Save-file magic,
logical data, all authoritative hashes, event cadence and the existing decoded
4,000,000,000-byte / compressed256,000,000-byte guards remain unchanged.

## Cause and design

Persistent trees intentionally share immutable radix nodes. Government succession
inherits five geographical fact trees plus the two indexed-place bag trees while
retaining the predecessor's historical maps. Ordinary DataContractSerializer
serializes shared objects by value. Consequently, a compact in-memory DAG was
expanded repeatedly in the binary XML stream; reading that stream also lost the
original sharing.

WorldFile alone now uses an `ISerializationSurrogateProvider` mapping the private
logical radix node to a reference-enabled transport DTO with the identical XML
contract name, namespace and Children/Values fields. The logical node retains its
original three fields and its preceding by-value contract. In particular,
DataContractJsonSerializer rejects reference-enabled contracts: marking the
logical node itself IsReference=true would break JSON and is **not** the design.

During a synchronous binary read, a disposable thread-local scope interns nodes
bottom-up. Its hash set retains only unique logical nodes. Hash codes select
buckets, never trusted payload: equality checks all32 leaf integers or all32
already-canonical child identities. Temporary conversion/cycle/depth metadata
lives only in transport DTOs, not simulation nodes. Cancellation and errors restore
the preceding scope in finally; no dictionary survives the read. Cycles, malformed
node shapes and excessive radix depth are rejected. `.With` still constructs new
nodes without modifying shared source nodes.

Microsoft documents [default by-value serialization and reference preservation](https://learn.microsoft.com/en-us/dotnet/framework/wcf/feature-details/serialization-and-deserialization)
and the [serialization surrogate provider](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.serialization.iserializationsurrogateprovider).
The required serializer extension is present in the project's .NET Standard2.1
reference API. Native support still requires the explicit runtime probe.

## Evidence

The dedicated `--tree-reference-only` fixture passes279 assertions: preceding
binary wire, new selective-reference wire, old-reader compatibility, exact
fingerprints and JSON bytes, immutable updates, structural hash collisions,
concurrent/repeated/nested reads, cancellation, malformed cycles and unchanged
logical-node allocation layout. The synthetic128-version tree family shrinks from
42,364,928 to951,004 decoded bytes after old-wire read/content interning.

Actual M14 Continents Plus, year172600:

| Transport | Compressed bytes | Decoded bytes |
| --- | ---: | ---: |
| Preceding by-value save | 221,665,532 | 2,744,928,958 |
| Reference attribute alone after old read, rejected experiment | 240,647,742 | 2,832,688,836 |
| Scoped content interning + binary-only surrogate | 151,805,883 | 1,604,120,362 |

The accepted design reduces compressed bytes31.51% and decoded bytes41.56%; the
attribute-only experiment demonstrates why merely enabling references is
insufficient for old snapshots. The source's recomputed complete history hash
remains `03969648e3e62a43f0619f451aa12f939ae8606a0e9ce3974db78c6c6467515f`.
An actual mid-write cancellation succeeds. Original versus compact-save/cold-read
12-year continuations both produce
`9b4b63b93dd5bad02dbdba00c4fde7326b77be87fa3d131a907952b567333158`.
The actual frozen N15 executable, which has neither the new surrogate nor
interning implementation, reads the new compact file and produces that same
12-year continuation. Actual F6 Earth100-year replay with the new transport and
sidecar caches retains the preceding control hash
`93add43f84c56e5241d9c474a9e7d56538b654b069b898c6bc55e10ccf3801b4`.

Artifacts are under the external overnight build root; generated worlds/binaries
are not source checkpoints. The diagnostic Core entry point
`HistoricalTreeTransportDiagnostics.Verify()` exercises12 binary, old-reader,
JSON, immutable-update and cancellation vectors before native timing.

## Closed campaign sidecars

Theater, community-progress and military-operation paged records additionally
cache exact canonical record bytes only for private owned hashing with a matching
closed parent campaign. Public hashing always reads actual fields. Mutable
get/set and forks of exposed records invalidate caches. Internal nonserialized
memo fields do not enter either wire contracts or hashes.89 focused assertions
cover retained references, replacement, active-parent exclusion, literal bytes,
fork isolation and concurrent public integrity checks. Existing137 campaign-page
compatibility assertions also pass. This does not eliminate any canonical bytes
from the SHA stream or alter checkpoint frequency.

The12000-record three-sidecar CoreCLR microbenchmark (30 repeated hashes) measures
279.5ms public versus205.8ms warm-owned, with approximately2.05MB allocated in
either loop. This is not native acceptance. First cold complete-checkpoint
hash allocation grows from265.7MB to303.8MB while constructing sidecar memo
arrays; their native heap/cold-year effect must be included in the next run.

Native performance acceptance is separate: the preceding isolated N15 run still
fails at2.4666years/s and a2,223.44ms worst summed year. No throughput claim is
made from these storage/compatibility fixtures.

## Fresh first-save write interning

The preceding writer memoized logical-node object identity only. A fresh
simulation can construct equal immutable pages independently, whereas reading an
old file already interns equal pages. Therefore load-and-resave evidence alone
does not establish fresh first-save compression. The valid S18 CP run exceeding
the unchanged compressed cap motivates this separate exact-storage correction;
it is not a historical simulation failure.

`BinaryTransportScope` now canonicalizes its transport DTOs bottom-up during
each write. Its scope-local content set compares all32 literal leaf values or
all32 already-canonical child references. Hashes only choose buckets; neither
cached Merkle digests nor a structural hash collision can substitute for full
equality. The source logical nodes remain untouched. The identity memo avoids
reprocessing inherited nodes, while the content set merges independently built
equal pages. Both tables are cleared at disposal, including exceptional writes;
there is no global or cross-world pool. Cycles, malformed shapes and excessive
depth are also rejected before encoding a logical node.

The existing `--tree-reference-only` runner passes290 assertions from
`artifacts/history32/tree-write-intern2-bin`. Its new first-save fixture constructs
32 independent32768-entry trees and checks distinct source roots before any
read. A separately authored identity-only reference DTO reproduces the preceding
writer rather than comparing only to by-value encoding. Decoded bytes decrease
8,779,966 to310,530; ordinary gzip bytes decrease3,937,236 to123,810. Every actual value remains readable by the preceding
by-value reader, and exact fingerprints/source immutability are preserved.
New controls also exercise structural hash collisions with deliberately identical
stale digest caches, deterministic concurrent writes, mid-write cancellation,
table cleanup and direct malformed cyclic/deep wire. Invalid read fixtures are
authored independently of the new write guard, not silently skipped because the
writer now rejects malformed sources sooner. Existing JSON bytes remain exact.

The binary DTO contract and WGWRLD01 file magic are unchanged. Neither the
256,000,000 compressed-byte nor4,000,000,000 decoded-byte guard is raised; no
history, array entry, actor or event is removed. Actual cold continuation and a
fresh dense CP save remain separate acceptance evidence, not inferred from the
synthetic compression ratio.

An actual T19 Earth rewrite uses
`candidate-t19-earth200/final.world`, year171700, historical hash
`3283a015a915024d6dd18811870d9bf3e7389d173be0477b0fddba2502a2a7c0`.
The new `write-intern-t19-earth.world` is192,067,478 compressed bytes and
2,213,947,961 decoded bytes, written in37.60 seconds after a47.75-second
validated read. The original is218,178,318 compressed bytes. Real mid-write
cancellation passes, and public source-hash verification remains exact.
This is explicitly loaded-world evidence: the read-side pool also canonicalizes
the original fresh file, so this size difference is not attributed solely to
the new write-side pool. The fresh dense CP cap control is still required.

The new file's genuine cold12-year continuation is
`43d7ce2a8dd1a4826123058004a112b8fc7790e3964a1bcb603804da24647458`,
exactly matching both its in-memory source continuation and the frozen T19
executable's original-source12-year control. Evidence files are
`write-intern-t19-earth.world.cold-12.hash` and
`lay20-frozen19-earth12.hash` under the external overnight root.
The actual preceding `candidate-t19-freeze/WorldGen.Core.Tests.dll` also reads
the new192 MB compact file and independently advances12 years to that identical
hash, recorded as `write-intern-frozen19-earth12.hash`. This verifies real
old-reader compatibility in addition to the independent literal-node fixtures.
