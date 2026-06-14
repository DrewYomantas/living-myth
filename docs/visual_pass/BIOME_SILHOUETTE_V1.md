# North Star Biome Silhouette V1 (2026-06-14)

The follow-up to Art Pipeline V1. The pipeline (Blender → headless Krita → Godot) was already
proven; this milestone spent its budget where the gap actually is — **art direction: silhouette,
biome identity, and the seat as a landmark** — by improving the **Blender source forms**, not the
Krita filters. Same pipeline, stronger shapes.

## The problem (audited from Art Pipeline V1 afters)
- **Broadleaf** = round green puffballs (broccoli): no trunk, no crown structure, tiled into a
  uniform bumpy carpet.
- **Conifer** = neat stacked-cone toys: uniform, lifeless.
- **Rocks** = smooth grey pebbles: no stone character.
- **Keep** = a squat chess-piece: didn't read as a *seat*.
- Coast / forest / highland all read as "green blobs + occasional stone" — no biome identity.

## What changed (Blender forms — `tools/art/render_diorama.py`)
- **Broadleaf** → three crown **profiles** (round / wide-oak / tall-birch), a taller two-segment
  visible trunk, and an irregular broken crown with top tufts. Reads as a tree, and a *cluster*
  now mixes profiles so a wood has silhouette variety instead of one stamp.
- **Conifer** → a tall ragged **fir spire** (7 tilted tiers + a sharp apex) vs a shorter bushier
  **pine** variant, with height/radius jitter so the stand looks grown, not assembled.
- **Rocks** → **angular faceted** blocks + crystalline shards on a slab (hard-shaded), not eggs.
- **Crag** (NEW asset) → a **stratified stepped outcrop** with a tilted crag face + fallen blocks —
  a highland ridge landmark. Wired into highland scatter (`DioramaView` highland is now
  stone-first: crag + rock dominate, conifers recede).
- **Keep** → a real **seat**: curtain-wall ring + corner turrets (conical roofs) + a gatehouse +
  a dominant battlemented keep tower. The single biggest read improvement.
- **Dock** → a clearer coast silhouette: plank jetty on posts + a **moored rowboat** + mooring
  posts + crates.
- **Standing stones** → taller megaliths + a true **trilithon** (two uprights bearing a lintel) +
  an altar slab — reads as a deliberate sacred ring/barrow.

Biome hierarchy now: **coast** = open shore + water/foam + dock + seat; **forest** = layered
broadleaf canopy depth + grove/clearing breaks; **highland** = stone/crags/ridges first, firs
second, an imposing hill-fort seat.

## Evidence
- `before_{coast,forest,highland}.png` / `after_{coast,forest,highland}.png` — same region/build,
  the V1 forms (before) vs the new forms (after).
- `compare_old_new.png` — old (left) vs new (right) for each changed prop silhouette.
- `contact_sheet.png` — the new proof kit (incl. crag).

## Honest North Star score: **7.0 / 10** (up from 6.5)
The wins are real and legible:
- **Keep** now reads as a fortified seat (biggest single jump).
- **Firs/conifers** read as grown stands; **rocks/crag** give highland genuine stone identity.
- **Trilithon** and **dock+boat** read as authored places, not blobs.
- Forest gained canopy **depth** and tree-to-tree variation; the three biomes are now visibly
  distinct rather than interchangeable green.

## What still blocks 7.5+
Honest, and all **art direction / authoring**, not plumbing:
1. **Broadleaf macro-massing.** Individual crowns are better, but a dense wood still reads as
   clustered spheres at region scale. Closing this needs real trunk+branch structure and a
   non-spherical canopy mesh (or a painted canopy sheet), not lobe clusters.
2. **Composition is still uniform-density scatter.** The North Star frames have *art-directed*
   composition — a hero settlement, focal clearings, ridgelines, negative space. Our density is
   even everywhere; there's no visual hierarchy guiding the eye.
3. **Macro depth cues.** Per-sprite contact shadows exist, but there's no inter-element shadow,
   ambient occlusion, or atmospheric perspective grounding the scene as one place.
4. **Ground texture is quiet at zoom** and trunks/branches don't read — the eye still lands on
   canopy mass, not structure.

These are the next milestone's targets (composition + canopy authoring + macro depth), and they
are *content* inside the existing pipeline — still no new plumbing.

## Constraints honored
No sim/RNG/tick/save changes (verify held **823/559/910/632**, all 9 console gates green, both
builds clean). Diorama stayed read-only; atlas default; the bridge button untouched; no seamless
zoom; no generative AI; no invented places/people/events; no static painted map.
