# Visual Pipeline Spike V1

A minimal **asset-backed** visual pipeline proof for Living Myth: Blender renders stylized
placeholder dioramas → transparent PNGs → a Godot `res://` folder → an **opt-in, viewer-only**
overlay drawn on top of the existing data-driven atlas. It does **not** replace the renderer and
does **not** touch the sim. This is a spike to answer one question: *can authored assets move the
viewer toward the Northstar look while keeping the deterministic data-driven truth intact?*

> Status: **SPIKE / PLACEHOLDER**. Nothing here is final, shippable art. The rendered PNGs are
> authored procedural geometry (low-poly Blender), **not** AI-generated images. The AI concept
> images in `Visual references/` remain art-direction-only and are never imported.

---

## Source files

| File | Role |
|---|---|
| `tools/art/render_assets.py` | Standalone Blender script. Builds 7 procedural dioramas, renders each to a transparent 256×256 PNG. Deterministic (fixed seeds; no time/random-state leaks), palette locked to `docs/VISUAL_STYLE.md`. |
| `godot/assets/spike/*.png` | The rendered placeholder assets (Godot import target). |
| `godot/MapView.cs` | `DrawSpikeAssets()` + `SpikeTexture()` + `SpikeAssetsEnabled` flag — the opt-in overlay pass. |
| `godot/Main.cs` | The `▦ spike` toggle button (Lens group), off by default. |

## Export command

Run from the repo root (Blender 5.1.x; EEVEE, headless):

```powershell
& "C:\Program Files\Blender Foundation\Blender 5.1\blender.exe" -b -P tools/art/render_assets.py -- godot/assets/spike
```

The argument after `--` is the output directory (defaults to `godot/assets/spike` if omitted).
A separate headless Blender process is used so an open Blender GUI session is never disturbed.
~4 seconds for all 7 assets on a GTX 1660 SUPER.

## The seven assets / naming conventions

`snake_case`, one diorama per file, transparent background:

| File | What it is | Driven by (in the overlay) |
|---|---|---|
| `forest_patch.png` | conifer cluster on a forest-green disc | region `TerrainType == "forest"` |
| `grassland_patch.png` | grass/field tufts on a plains disc | default terrain (plains/coast/…) |
| `rocky_patch.png` | faceted stones on a highland disc | region `TerrainType == "highland"` |
| `road_path_decal.png` | a low, kinked dirt path | *(rendered; not yet overlaid — roads stay code-drawn)* |
| `shrine_ruin_marker.png` | a weathered stone dolmen | *(rendered; not yet overlaid)* |
| `settlement_cluster_marker.png` | thatch-roofed timber huts | region is **held** (`ControllingFactionId != null`) |
| `parched_famine_overlay.png` | cracked ochre dry-earth patch | region `InFamine == true` |

Palette is taken verbatim from `docs/VISUAL_STYLE.md` (the hexes are binding). Camera is a shared
orthographic iso (~55° elevation) so every asset shares scale and projection.

## Godot import location

- Assets live at **`godot/assets/spike/`** → loaded as **`res://assets/spike/<name>.png`**.
- Godot imports PNGs on first editor launch (or F5). `MapView.SpikeTexture()` loads them lazily via
  `ResourceLoader.Exists` + `GD.Load<Texture2D>`, caching results (including misses) so a missing
  asset is tried once, never per frame. A missing/un-imported asset simply doesn't draw — no crash.
- The generated `*.png.import` sidecars are Godot's import metadata; commit them alongside the PNGs
  if/when these assets are kept (out of scope for this spike — nothing is committed yet).

## How to see it

Launch the viewer (F5), then click **`▦ spike`** in the bottom **Lens** group. It is **off by
default** (opt-in). Toggling it overlays the dioramas; toggling off restores the pure code render.

## What is final vs placeholder

- **Placeholder:** every PNG in `godot/assets/spike/`, the iso camera framing, the exact geometry,
  the overlay sizing/anchoring. All disposable.
- **Final-ish (the reusable part):** the *pipeline shape* — a deterministic, palette-locked Blender
  script → transparent PNG → `res://` → a flagged, data-driven overlay seam that never invents sim
  facts. That seam (`DrawSpikeAssets` reading only `TerrainType` / `ControllingFactionId` /
  `InFamine`) is the pattern worth keeping.

## How this supports the Northstar look

The Northstar (`docs/VISUAL_STYLE.md`) is a *"warm mythic pixel diorama presented as a living
atlas"* — painted land, named places, settlement dioramas, history that leaves marks. Code-only
draw calls (lines, arcs, polylines) are reaching their ceiling for that hand-made density. Authored
assets are how a diorama gets *texture and silhouette* — conifers, thatch roofs, standing stones —
without faking data. This spike proves the assets can be produced deterministically, dropped into
`res://`, and composited over the live sim render with one flag, at the correct scale and anchor.

## What MUST stay data-driven (the non-negotiables)

- The overlay is **presentation only.** It reads sim state it never owns; it writes nothing back.
- **No invented locations or facts.** Assets are placed by *existing* sim truth (region center,
  terrain class, control, famine) — never a fabricated settlement, population, or place.
- **The sim is untouched.** No RNG, no tick order, no event records change. `verify` must stay
  **823/559/910/632**; viewer-only work cannot move it (and didn't).
- **The data-driven surface remains the base layer.** The spike draws *above* `WorldSurface`, below
  the lens rings, and is fully removable by toggling the flag — never a replacement renderer.
