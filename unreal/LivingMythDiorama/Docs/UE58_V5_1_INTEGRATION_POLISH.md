# UE 5.8 — V5.1 Asset Integration Polish (Living Myth)

_2026-06-20. Pure Python + **Geometry Script**, no C++. An ART-integration polish pass over the V5
proof set ([UE58_V5_PROOF_SET.md](UE58_V5_PROOF_SET.md)). V5 proved the asset *factory*; V5.1 proves
the *look*. Built as a SEPARATE V5.1 path so the V5 baseline survives for side-by-side comparison.
V2/V3/V4 and the V5 baseline (assets, map, the `M_LMV5` master, the `MI_LM_*_A` instances) are
byte-untouched; nothing here reads or writes sim state._

## Why V5.1
Judging the committed V5 captures directly against the three North-Star reference sheets
(`LM_V5_*_ref01.png`) confirmed the V5 verdict's own "what still fails" list:

1. A giant flat carpet plane (`/Engine/BasicShapes/Plane` ×80) made the trio read as test props on a
   training field — no sense of scale or place.
2. The terrain chunk was a 45°-square box tile (the perlin mag-8 barely warped the outline).
3. The path was a flat board with two clean parallel rut stripes and straight grass-strip edges.
4. The cairn's amber emissive (`es=22`) was **invisible** under the locked midday exposure.
5. Assets floated — weak contact/grounding.
6. The whole palette skewed yellow-green; the references are earthy moss / warm dirt / cool grey
   stone with a warm amber memory glow.

## What V5.1 does
A separate namespace — `/Game/LivingMyth/V5_1/`, `MI_LM_V5_1_*` (reparented to the **existing**
`M_LMV5`, read-only), and `GeneratedAtlasV5_1_IntegrationPolish`.

| Asset (`/Game/LivingMyth/V5_1/`) | Zones (slots) | Role | Pivot |
|---|---|---|---|
| `SM_LM_Terrain_LandPatch_B` | GrassMoss · Dirt · Stone · FoliageAccent | base island (~10 m) | ground-center bottom |
| `SM_LM_Terrain_GrassStoneChunk_B` | GrassMoss · Dirt · Stone · FoliageAccent | grassy knoll | ground-center bottom |
| `SM_LM_Path_DirtWorn_B` | PathDirt · Pebble · GrassEdge | worn path (+X forward) | ground-center bottom |
| `SM_LM_Memory_HomeCairn_B` | Stone · Moss · Ribbon · Glow (emissive) | memory cairn | ground-center bottom |
| `SM_LM_Water_Disc_B` | Water | the sea the island rises from | ground-center bottom |

**Problem → fix**
- **Carpet → island.** The engine plane is gone. The scene is a coherent *place*: an irregular
  `LandPatch` island rising out of a dark `Water_Disc`, the knoll as a side feature, the worn path
  crossing the land to the cairn as the focal memory. Cameras frame tight so the island fills the frame.
- **Square/coin tile → organic land.** `LandPatch` is built from a few **overlapping offset discs**
  (guaranteeing a non-circular silhouette) then perlin-broken at the rims into shore lumps. The knoll
  is a noised round form, not a stacked box.
  - _Geometry-Script gotcha:_ the engine's `apply_perlin_noise_to_mesh` **squares the frequency**
    parameter (it logs a deprecation about this). A visible-scale outline break therefore needs
    `freq ≈ sqrt(target)` — V5's `0.02` collapsed to `0.0004` (no break); V5.1 uses `0.06–0.13`.
- **Board path → worn trail.** The slab + parallel rut boxes + grass-strip boxes are gone. The path
  is now **overlapping worn-dirt lumps** along +X with broken edges, scattered compacted/pebble
  centers, and grass tufts blending the borders (kept off the ±X ends so segments still tile).
- **Washed-out glow → sacred heart.** Cooler/darker cairn stone + a dark mossy base ring for contrast;
  a contained hot-amber **heart** seated forward in the lower stack gap (camera-visible) + a small
  apex finial; emissive tuned down from a flood to a glow; bloom `0.28`. Plus **one proof-only warm
  PointLight** at the heart (`LM_V51_CairnLight_PROOFONLY`, ~34 cd / 170 cm radius) — under locked
  exposure a real local light reads where pure emissive can't. Single light, performance-safe, clearly
  labeled; the production atlas would drive this from event/site truth, not place a fixed light.
- **Floaty → grounded.** Lower sun (−30° pitch) + directional contact shadows; asset bases embedded
  a few cm into the land surface.
- **Yellow-green → earthy.** New `MI_LM_V5_1_*` palette: desaturated brown-leaning moss, warm dirt,
  cool grey stone, dark cool cairn stone, controlled amber glow, deep teal water; warmer key / cooler
  sky fill so highlights warm and shadows cool.

## Commands
```bash
# build assets + scene (headless, no viewport)
UnrealEditor-Cmd <uproject> -run=pythonscript -script=tools/ue_v5_1_polish.py -unattended -nosplash -nopause
# capture the three cameras (GUI editor; settles exposure ~150 frames/cam; needs -unattended)
UnrealEditor <uproject> -ExecCmds="py <space-free copy of tools/ue_v5_1_capture.py>" -nosplash -unattended
```
Captures → `Saved/Screenshots/V5_1_atlas.png` · `V5_1_region.png` · `V5_1_inspect.png` (gitignored).

## Honest verdict
- **Atlas (the gate): PASS.** Reads as an organic living land patch in a sea (no coin/tile, no
  carpet); the cairn glow reads as a contained warm mark from altitude — sacred, not a neon flood
  (the first iteration over-lit it gold; tamed light + emissive + bloom fixed it).
- **Region: PASS.** A little island diorama — stone-stack cairn with a warm heart at its base, worn
  path, scattered foliage, moody water. Clearly closer to the North Star than V5.
- **Inspect: PASS.** Cairn glow reads as memory; the path reads as a trodden trail, not a board;
  material zones + broken forms read; earthier than V5.
- **What still fails / compromises:** (1) cairn stones light a touch brighter than the cool-dark
  intent under the key+proof light; (2) foliage is uniform cones — stylized placeholders, not varied
  silhouettes; (3) surface "painterness" is still procedural noise — the next leap is hand-painted
  texture / better foliage forms, i.e. **art labor, not plumbing** (same ceiling V5 documented).
  Honest score ~5/10 (V5 atlas) → ~6.5–7/10.
- **Honesty:** presentation-only. Reads/writes no sim state, invents no place/person/event, no
  third-party packs, no runtime generative AI. V2/V3/V4 and the V5 baseline + `M_LMV5` master are
  byte-untouched. The proof-only cairn light and the authored composition are review scaffolding;
  real placement must be driven by sim truth (terrainType / region cell / controlling faction / site
  truth / event RegionId / home `HomeRegionId`) — out of scope here.

## Files
- `tools/ue_v5_1_polish.py` — asset + material + proof-scene builder (deterministic, idempotent).
- `tools/ue_v5_1_capture.py` — editor-side three-camera settle capture.
- `Content/LivingMyth/V5_1/SM_LM_*_B` — the five StaticMesh assets.
- `Content/LivingMyth/Materials/MI_LM_V5_1_*` — twelve zone instances on the reused `M_LMV5`.
- `Content/LivingMyth/Maps/GeneratedAtlasV5_1_IntegrationPolish.umap` — proof scene.
- `Saved/v5_1_polish_verdict.json` — machine-readable build verdict (gitignored).
