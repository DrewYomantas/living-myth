# North Star Region/Site Vertical Slice V0 — "Greymarket" site view

**2026-06-16 · viewer/prototype-only · NOT a sim milestone, NOT main-atlas polish.**

A separate, isolated Godot prototype scene that proves the final desired **look and feel** of
Living Myth at **site scale** — a stylized fantasy pixel diorama / sacred living atlas, scene-first,
lived-in, wrapped in parchment-and-ink chronicle UI. Built to answer Drew's F5 verdict that the
production viewer "still feels like the same game" and is "nowhere near the locked end-product target."

Slice chosen: **A — Greymarket SITE view** (over B region / C replay). It is the reference hero shot and
the only slice that stacks all four reference superpowers at once: scene-first composition, lived-in
local density, illustrated fidelity, and warm diorama light. Confirmed independently by the gap audit.

## How to run
```
GODOT="…/Godot_v4.6.3-stable_mono_win64_console.exe"
"$GODOT" --path godot res://PrototypeGreymarket.tscn          # interactive
LM_NS_MODE=wide|inspect|detail "$GODOT" --path godot res://PrototypeGreymarket.tscn
LM_NS_SHOT=<dir> LM_NS_NAME=<name> LM_NS_MODE=wide …          # headless self-capture
```
Boots its own deterministic seed-7 world to year 462 for honest names (place "Morhallow", held by the
Mournfold, pinned soul "Sela of the eastern bays"), then overlays a hand-authored ("mocked") village
layout. It is launched by scene name and never boots `Main.tscn`; it cannot touch `_running`, the world
save, follows, or any production state.

## GAP CLOSED (this pass — composition / density / atmosphere / UX, all in code)
- **Scene-first, frame-filling** village — no floating diamond in a black void; framed by a warm
  parchment sea + haze.
- **Visible inhabitants** — ~14–20 tiny drawn folk ringing the market square + scattered along streets /
  shrine path / dock / fields. This was the **#1 element the production diorama entirely lacked.**
- **Connected built fabric** — a readable main-street road spine threading clustered, street-fronting
  thatch/timber houses (5 neighborhoods), not a uniform scatter or a central pile.
- **Focal open market square** — the bright negative-space center, ringed by 8 striped stalls + 2
  faction-tinted banners + a well; the densest crowd browses its edge.
- **Lived-in detail** — furrowed fenced field plots, a cart, a bay with drawn pier + moored boat,
  a shrine knoll with standing stones + procession at back, a single `keep` seat crowning the NE rise,
  chimney smoke.
- **Warm golden-hour atmosphere** — warm key gradient + long SE contact shadows + soft ambient-occlusion
  under clusters + a focal value falloff (market warmest/brightest → edges sink to cool shadow + haze) +
  the existing parchment post (grain/vignette/grade).
- **Parchment chronicle UI** — year plate, title cartouche, left **gazetteer** card with a **real
  inspectable soul** (sigil glyph + Inspect/Follow + a real recent beat + a callout **leader-line** to her
  figure in `inspect` mode), secondary narrow **Saga** of real region events, **bronze-medallion verb
  bar** (Inspect/Follow/Curse/Bless live-look + Prophecy/Plague/Terrain greyed), faction legend, speed pips.
- **Honesty** — a persistent visible ribbon `PROTOTYPE — illustrative composition, not sim truth`; real
  names come from the sim, the building/stall/crowd layout is authored mock and labeled as such. Nothing
  leaks into the production atlas or its honesty contract.

**Honest score vs `Visual references/gpt-northstar-site-view-greymarket.png`: ~7/10**
(production diorama baseline was ~3–4: floating broccoli island, zero people, black void).
It now unmistakably reads as a warm, inhabited medieval market town with a clear market, streets, and
tiny folk — resembling the reference in composition, density, warmth, and UI.

## STILL NOT THERE (the remaining gap is ART LABOR, not code/layout)
- **Per-asset painterly fidelity.** Buildings and trees are still the flat Blender billboards from the
  diorama kit — warm-toned now, but smooth: no hand-painted roof texture, window glow, eave detail, or
  per-roof variety. The reference's painterly material weight is the last fidelity step.
- **Figures are simple drawn ovoids** — great for the density read, low-fidelity in the `detail` crop. A
  2–3 frame pose/silhouette set closes it.

### How to close it (sanctioned routes, per `docs/GODOT_ASSETLIB_SCOUT.md`)
1. **Extend the Blender pipeline** (`tools/art/render_diorama.py`) with new prop builders — market stall,
   fence, cart, well, tiny figure, and richer textured roofs / canopy sheets. Original work, provenance-
   clean, the documented production art route. *Requires Blender installed (it is NOT available in this
   session's environment), so this is a follow-on on Drew's machine.*
2. **Treated-Kenney kitbash** (grounded/muted/medieval only) or **offline AI-tooled sprites** — both must
   be palette/material/outline-finished and pass license + art-direction review before shipping. Never
   raw, never runtime.

## Verification
- **No sim systems added; zero files under `src/LivingMyth.Sim/` changed.** `git diff HEAD` is empty for
  every tracked file — the prototype is entirely new, additive, isolated files.
- `Main.cs` / `Main.tscn` / `DioramaView.cs` / `project.godot` **unmodified**.
- **`verify` holds exactly 598/751/809/1065** (seeds 1/18/42/7). All 11 other gates HOLD
  (homes/story/canon/divine/save/sites/replay/harvest/plague/migration/prejudice). Sim + Godot builds
  clean (0/0).

## Files added (this pass only)
- `godot/PrototypeGreymarket.cs` — standalone `Control` + `GreymarketCanvas` custom-draw + `SigilGlyph` +
  `VerbBar`; references only the Sim read-model.
- `godot/PrototypeGreymarket.tscn`, `godot/PrototypeGreymarket.cs.uid`
- `docs/visual_pass/northstar_v0/ns_v0_{wide,inspect,detail}.png`, this report.

## Route A house fidelity — render commands (run on Drew's machine)
Route A (Blender-extend) added course-by-course roofs (thatch/tile/slate), a warm emissive window glow,
and a third roofline `house_c` (tall narrow slate towne-house). `render_diorama.py` + the Greymarket
scene are wired; **rendering needs Blender, which is NOT on this session's PATH**, so run the three
stages below on Drew's machine. Copy-paste from the repo root (Git Bash):

```bash
# Stage 1 — Blender: re-render JUST the three house bases (LM_ONLY keeps it fast)
BLENDER="/c/Program Files/Blender Foundation/Blender 5.1/blender.exe"
LM_ONLY=house_a,house_b,house_c "$BLENDER" -b -P tools/art/render_diorama.py -- godot/assets/diorama

# Stage 2 — Krita (headless): painterly pass IN PLACE over the fresh bases
#   (plugin must be installed/enabled per tools/art/krita_plugin/INSTALL.md; not idempotent —
#    always re-run stage 1 first). Run for each new base, e.g.:
LM_KRITA_MODE=apply kritarunner -s lm_paintover -f run_main

# Stage 3 — Godot Greymarket: just launch the prototype scene; PNGs load at runtime (no import step).
GODOT="…/Godot_v4.6.3-stable_mono_win64_console.exe"
LM_NS_MODE=wide "$GODOT" --path godot res://PrototypeGreymarket.tscn        # village now mixes 3 rooflines
```
After stage 1 produces `house_c.png`, `LoadTextures()` picks it up automatically (it globs every PNG in
`assets/diorama/`); the scene already selects house_a/house_b/house_c per terrace house.
