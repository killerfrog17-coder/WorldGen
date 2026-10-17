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
