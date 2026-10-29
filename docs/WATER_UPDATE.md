# Water update

## Player interaction

Aim at a water surface within 2.8 m of the character and press E. The existing
contextual pickup prompt identifies freshwater or saline water; it does not
certify potability. Each sip is 250 ml. Temporary hand borrowing restores the
previous equipment. There are no water containers or purification actions yet.

Freshwater hydrates; seawater produces a net hydration loss. Ocean salinity is
35 ppt. Lakes use their existing simulated salinity, including saline lakes.
Tidal river water can be brackish using the habitat salinity and low-elevation
tidal influence. This is an approximation, not a transported estuary salinity
field. Clear moving water has a lower contamination prior than stagnant/muddy
water, never zero risk. Illness has a saved incubation period and subsequently
increases water and energy losses. The numerical probabilities are game balance
priors, not measured pathogen concentrations or a water-safety recommendation.

Deep water switches the walking motor to buoyancy, swimming and bounded current
advection. WASD follows the view direction; Space rises and Ctrl dives. Shallows
remain walkable. Existing authored human Swim clips replace jumping/walking
while swimming; original models, weights and animation assets are unchanged.
Survival tracks wetness, exertion and breath. After 45 seconds of accumulated
submerged exertion, suffocation damages health; resurfacing restores breath.
Breath time is real play time, not the accelerated calendar. Creative/Spectator
do not take drowning or drinking damage. Spectator retains noclip flight.

## Surface and underwater rendering

Water is a dedicated URP shader using a small periodic slope-data texture,
actual river flow vectors/speed, two ocean swell components, depth-dependent
absorption, screen-colour refraction, sky Fresnel reflection, sun glints and
restrained shoreline/turbulence foam. It requests the camera's opaque-colour and
depth copies. No fluid simulation, screen-space reflection ray tracing, extra
reflection camera, or extra wave tessellation is used. Surface waves taper at
shallow banks; lakes receive smaller wind waves. The swim motor samples the
same two swell functions as the shader.

Immersion adds distance absorption and colour haze after transparent rendering.
Survival also receives a quarter-resolution naked-eye defocus pass; Creative
and Spectator omit that blur as a stand-in for a mask, retaining water haze.
Visibility varies with water class and a sediment/turbidity prior. The upward
water path ends at the surface instead of fogging the sky to infinite distance.
These are visual approximations, not a physical eye/refraction simulation.

## River geometry corrections

Sea-level ribbon spans are no longer discarded, preserving the final mouth
sections. Terrain refinement includes the same outer bank overlap as the water
mesh, and welds submillimetre duplicate intersections. Numerical duplicates had
sampled opposing channel-side heights several metres apart and produced spikes.

Bounded river ribbons now clip only to their tile. Ordinary depth testing hides
their under-bank portions. Clipping them again against each refined ground face
was redundant and introduced fragmented surfaces. Channel/junction boundaries,
water elevations and finite widths remain; no broad extrapolated water plane is
introduced. Eight actual river tiles and three sea-level mouths are covered by
the regression. The steep sample tiles now contain roughly 70–80 water
triangles instead of several thousand; this is not a measured whole-game FPS
claim. Existing terrain/world/history source data are not mutated.

## Research

- [CDC: drinking water while hiking, camping and travelling](https://www.cdc.gov/drinking-water/prevention/water-treatment-hiking-camping-traveling.html):
  clear natural water can contain pathogens; flow and clarity do not prove safety.
- [NOAA: can humans drink seawater?](https://oceanservice.noaa.gov/facts/drinksw.html):
  salt removal costs more water than seawater supplies.
- [Unity URP asset reference](https://docs.unity.com/en-us/engine/6000.0/manual/render-pipelines/universal-render-pipeline/urp-reference/universalrp-asset):
  opaque-colour/depth inputs for water/refraction.

## Verification

Core: 37 drinking/swimming/save checks; 165 existing fall/death checks and 67
survival/mode checks. Actual-world river regression: 71,036 assertions across
764 spans, eight tile sites and three sea-level mouth sites; the existing 8 cm
exposed-edge clearance guard is retained. Native gameplay/rendering and final
package preservation evidence are recorded under artifacts/water-update-20261028.
The release IL2CPP gameplay run passed 18 checks, including actual sea-water
aim/drinking and occupied-hand restoration, real coastal buoyancy/diving,
authored Swim clip selection, naked-eye/mask render passes, drowning damage,
real save-format breath restoration and an inland freshwater river. Coastal
surface and both underwater captures were inspected. Two earlier runs stopped
on incorrect item-factory calls in the diagnostic setup; they are not passes.
No whole-game performance comparison or universal river-artifact claim is made.
The additional daylight river capture completed without errors, but vegetation
obstructs its view; it is not evidence of river appearance. Owner testing of
the previously affected rivers remains useful.
