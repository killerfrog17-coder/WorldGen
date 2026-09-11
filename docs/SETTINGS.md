# Effective recipe settings

Both hosts enumerate the same `Recipe.Settings` metadata. Numeric inputs validate ranges at generation. Fractional values are quantized to millionths. Step is the UI increment; valid intermediate values are accepted. All fields are included in every saved recipe. Version fields are displayed and validated, not user-selectable alternate algorithms.

| Panel | Field | Meaning | Default (Balanced) | Range | UI step |
|---|---|---|---|---|---|
| World | `Seed` | Seed | 73421 | 0-2147483647 | 1 |
| World | `MacroCellCount` | Canonical cells (minimum) | 10242 | 642-40962 | 1 |
| World | `RadiusKm` | Planet radius (km) | 6371 | 1000-20000 | 100 |
| Plates | `MicroplateCount` | Microplates | 120 | 4-500 | 1 |
| Plates | `ParentCount` | Parent plates | 18 | 2-40 | 1 |
| Plates | `ParentInequality` | Parent size inequality | .35 | 0-1 | .05 |
| Plates | `MicroplateIrregularity` | Microplate irregularity | .35 | 0-1 | .05 |
| Plates | `ParentInertia` | Directional inertia | .65 | 0-1 | .05 |
| Plates | `ParentCurvature` | Growth curvature | .4 | 0-1 | .05 |
| Plates | `PreservedFraction` | Contested fraction | .1 | 0-.5 | .01 |
| Plates | `BoundaryRoughness` | Boundary roughness | .35 | 0-1 | .05 |
| Continents | `ContinentCount` | Continent identities | 5 | 1-12 | 1 |
| Continents | `LandFraction` | Target land fraction | .3 | .1-.8 | .01 |
| Continents | `ContinentVariation` | Continent size variation | .4 | 0-1 | .05 |
| Continents | `Connectedness` | Connectedness | .45 | 0-1 | .05 |
| Continents | `Isthmus` | Isthmus tendency | .45 | 0-1 | .05 |
| Continents | `NearStrait` | Near straits / shelves | .5 | 0-1 | .05 |
| Tectonics | `TectonicIntensity` | Tectonic intensity | 1 | 0-2 | .05 |
| Tectonics | `ParentStress` | Parent stress contribution | .65 | 0-1 | .05 |
| Tectonics | `CollisionFrequency` | Special collision frequency | .6 | 0-1 | .05 |
| Tectonics | `CollisionStrength` | Special collision strength | 1 | 0-2 | .05 |
| Tectonics | `EscapeDistanceKm` | Ocean escape search (km) | 1800 | 200-5000 | 100 |
| Tectonics | `GhostFrequency` | Ancient orogen frequency | .5 | 0-1 | .05 |
| Tectonics | `GhostStrength` | Ancient orogen strength | .6 | 0-2 | .05 |
| Tectonics | `GhostAge` | Ancient orogen age | .65 | 0-1 | .05 |
| Tectonics | `GhostWidthKm` | Ancient orogen width (km) | 500 | 100-1500 | 50 |
| Tectonics | `Volcanism` | Hotspot / volcanic activity | .5 | 0-1 | .05 |
| Tectonics | `ForelandStrength` | Foreland basin strength | .7 | 0-2 | .05 |
| Tectonics | `Relief` | Relief and detail | .55 | 0-1 | .05 |
| Tectonics | `OceanDetail` | Ocean floor detail | .5 | 0-1 | .05 |
| Tectonics | `PreserveInlandSeas` | Preserve inland seas | 1 | 0-1 | 1 |
| Tectonics | `InlandSeaMinAreaKm2` | Minimum inland sea (kmÃƒâ€šÃ‚Â²) | 25000 | 1000-1000000 | 1000 |
| Tectonics | `InlandSeaMinDepthM` | Minimum inland sea depth (m) | 100 | 10-2000 | 10 |
| Erosion | `Hydraulic` | Hydraulic erosion | .35 | 0-1 | .05 |
| Erosion | `Thermal` | Thermal erosion | .2 | 0-1 | .05 |
| Erosion | `Glacial` | Glacial erosion | .35 | 0-1 | .05 |
| Erosion | `RidgePreservation` | Ridge preservation | .4 | 0-1 | .05 |
| Hydrology | `Endorheic` | Endorheic tendency | .2 | 0-1 | .05 |
| Hydrology | `MinLakeAreaKm2` | Minimum lake (kmÃƒâ€šÃ‚Â²) | 1000 | 100-1000000 | 100 |
| Hydrology | `RiverThresholdKm2` | River contributing area (kmÃƒâ€šÃ‚Â²) | 150000 | 1000-10000000 | 1000 |

## Presets and output controls

Preview changes the canonical minimum to 2,562 and microplates to 80. Connected uses connectedness .95, isthmus .9 and land fraction .4. Isolated uses connectedness/isthmus 0 and land fraction .24. Glacial uses glacial .95 and endorheic .45. The 500-microplate preset changes only microplate count. Balanced uses the table defaults. The interfaces retain your seed when switching presets.

Sea-level offset (-1,000 to +1,000 m), diagnostic layer, camera orbit and zoom are view state, outside the authoritative recipe. Canonical count is a minimum and is rounded upward to a supported closed mesh. Render resolution never changes this count. Save/load roundtrips the full recipe; reapply is a complete replay. Result export includes the generated recipe even if current UI fields are dirty.

World and plate fields feed SphericalMesh/PlateHierarchy; continent fields feed Continents and coastal Terrain; tectonic fields feed Tectonics, province/kernel synthesis and inland-sea protection; erosion fields feed Erosion; hydrology fields feed Hydrology. Settings gated on an eligible feature can have no visible effect in a world without that feature. For example, minimum inland-sea size only affects qualifying enclosed marine blocks.

## Diagnostics

Terrain; microplates; parents; preserved contested actors; continent identities; landmasses; crust; main-ocean connectivity; boundary type; propagated stress; collision context; ghost seams; tectonic, pre-erosion and final elevation; bathymetry; lower-sea-level land bridges; erosion delta; water bodies; lakes/inland seas; watersheds; rivers; contributing area. The report exposes all parent target/achieved areas and stage hashes. Click a cell for plate/continent/water provenance and affected semantic features.
