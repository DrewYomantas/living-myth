# North Star Art Pipeline V1 — Terrain + Prop Language (2026-06-14)

The first **real art-pipeline slice** for the diorama: a repeatable, reproducible asset recipe
that pushes the live deterministic regions toward the North Star while staying fully
data-driven and read-only. This is the milestone that follows the North Star Diorama arc
(prototype → production bridge → hardening) and answers its open question: *how do we close the
5→7 fidelity gap without hand-illustrating every frame?*

The answer proven here: a **three-stage authored pipeline** — Blender form/light → headless
Krita painterly pass → Godot data-driven composite — that any future region can be run through
from one command, with zero sim/RNG/tick/save changes.

## The pipeline (three stages, all reproducible)

```
tools/art/render_diorama.py     Blender 5.1 (Cycles)  → clean lit base PNGs (form, colour, light)
tools/art/krita_paintover.py    Krita 5.3 (headless)  → painterly pass IN PLACE (texture, ink, grade)
godot/DioramaView.cs            Godot (read-model)    → composites assets onto live sim regions
```

Run it:
```bash
blender -b -P tools/art/render_diorama.py -- godot/assets/diorama         # stage 1
LM_KRITA_MODE=apply kritarunner -s lm_paintover -f run_main                # stage 2 (see below)
# stage 3 is just launching the viewer — assets load at runtime, no import step
```

### Stage 1 — Blender (base form/light)
Already established by the diorama arc; this milestone **extended** it:
- **Richer material recipe** (`mat()`): two layered procedural noises (coarse weathering toward a
  shadow tone + fine grain toward a warm highlight) instead of one flat wash — reads as brushwork,
  not a single tint. Applies to every prop at once.
- **New ground-tile pass**: a second top-down OPAQUE render of four painterly terrain swatches
  (`ground_coast/forest/highland/water`) — these become the textured ground (see stage 3). Water
  carries a voronoi foam speckle.
- **New props**: an event/pulse `pulse_marker` (stone ring + ember + spark) and a fuller `banner`
  (taller pole, brass finial, waving flag + triangular pennant) for clearer faction identity.

### Stage 2 — Krita (painterly pass, HEADLESS)
`tools/art/krita_paintover.py`, driven by Krita's `kritarunner` (no GUI). Per asset:
1. **gaussian blur (r≈2) → unsharp** — a painted smear that unsharp restores into painterly edge
   contrast (softens the low-poly facets without losing the silhouette).
2. **edge-ink overlay** — duplicate → `edge detection` → `invert` → **multiply**, **alpha-
   inherited** so the ink clips to the silhouette (opacity 120 props / 80 grounds). This gives
   forms hand-drawn outlines and inner contours — the single biggest "illustrated" lift.

Getting Krita to run a user plugin headless took three non-obvious fixes, all captured in
`tools/art/krita_plugin/INSTALL.md`: kritarunner uses its **own** resource dir
(`%APPDATA%/kritarunner/pykrita`), the plugin must be **enabled** in `kritarunnerrc`, and the
entry function must accept the **args list** kritarunner passes. The repo carries the plugin
(`tools/art/krita_plugin/`) so the install is one copy + one config line.

> The paintover is **in-place and not idempotent** — always re-render the Blender bases before
> re-painting, or the chain double-applies. (And the `invert` step inverts the alpha channel, so
> alpha-inheritance is load-bearing — without it every prop renders as an opaque grey box.)

### Stage 3 — Godot (data-driven composite)
`DioramaView.cs` (read-model, unchanged contract — reads regions/sites/chronicle, writes nothing):
- **Textured ground**: the iso ground diamonds are drawn with the painterly terrain swatch
  (`DrawColoredPolygon` + UVs + texture) for coast/forest/highland/water; the NW relief light +
  per-cell jitter ride as a brightness modulate. Unmapped terrains fall back to the flat colour.
- **Shore foam**: a pale fringe on every water-cell edge that meets land.
- **Roads**: warm dirt paths from the seat out to every other known place.
- **Pulse markers**: the 3 most recent site-anchored tales get the ember glyph, tinted to the
  event class (war-red / harvest-ochre / founding-gold) — never a fabricated fact.
- All of it gates behind `LM_DIORAMA_RAW=1`, which forces the pre-pipeline render so the same
  region can be captured **before vs after** from one deterministic build.

## The proof kit (small + high-signal, as scoped)
3 terrain treatments (coast/forest/highland) + water; 2 tree treatments (broadleaf/conifer, via
the shared material upgrade + ink); 1 road treatment; 1 water/shore treatment; 1 improved
settlement silhouette (keep, via the ink pass); 1 banner style; 1 event/pulse marker. 24 assets
total flow through the pipeline.

## Evidence
- `before_{coast,forest,highland}.png` / `after_{coast,forest,highland}.png` — same region, same
  build, `LM_DIORAMA_RAW` toggled. The only difference is the art pipeline.
- `mid_coast.png` — the Blender-textured stage before the Krita paintover (the 3-stage story).
- `contact_sheet.png` — the proof-kit assets after the full pipeline.

## What is authored vs still procedural
- **Authored now:** every prop/ground silhouette (Blender geometry + lighting), the two-noise
  material brushwork, the Krita ink/grade pass, the terrain swatches.
- **Still procedural / code-driven (honest):** asset PLACEMENT (sim site positions, terrain
  scatter), the iso projection + relief shading, roads/foam/pulses (drawn in C# from sim truth).
  Nothing invents a place or a fact.

## Honest North Star score
The diorama prototype/bridge was independently judged **5/10**. This pass earns an honest
**6.5/10** — a clear, real step, not a leap:
- **Biggest win — water/shore:** the coast/highland water went from a flat teal diamond to a
  textured body with a pale foam shoreline. This was the single worst placeholder; it now reads.
- **Ground:** coast/forest/highland are textured swatches instead of flat polygons (subtle at low
  zoom, obvious in clearings and open highland).
- **Props:** the Krita edge-ink gives every tree, keep, and marker hand-drawn outlines/contours —
  an *illustrated* quality that reads much closer to "semi-realistic painterly" than flat-shaded
  low-poly did.
- **New language:** roads connect the seat to its places; ember pulse markers flag recent
  site-anchored tales.

**Why not 7+:** the tree *massing* is still recognizably the same low-poly forms (better shaded
and inked, not re-sculpted); the composition is still a scatter over an iso plane rather than a
hand-composed painterly scene; ground texture is quiet at this zoom. Closing that needs richer
per-biome Krita chains (oilpaint/texture-bombing on grounds), more silhouette variety, and
art-directed composition — **all inside this same pipeline**, no new infrastructure.

## Recommendation
**Adopt this as the production art route.** The value of this milestone is not the 1.5-point bump
— it's that the bump came from a **repeatable, reproducible, fully data-driven pipeline** that any
region runs through with two commands and zero sim risk (verify held **823/559/910/632**; all
gates green). The three stages are cleanly separated (Blender owns form/light, Krita owns
painterly texture/ink, Godot owns data-driven placement), so each can deepen independently. The
next fidelity gains are *content* (more Krita chain tuning, more silhouettes), not *plumbing* —
which is exactly where a production art route should be. Keep the deterministic map as the base;
this only ever composites over it.

> Note on Krita-as-engine: the headless filter pass is real and re-runnable, but the painterly
> ceiling of *filters* is limited — they texture and ink existing forms, they don't re-draw them.
> If a later pass wants true hand-painted assets, the Krita stage is the natural place to graduate
> from scripted filters to authored paintovers (same slot, same plumbing).
