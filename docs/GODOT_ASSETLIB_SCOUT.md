# Godot Asset Library Scout V1

A scouting pass over the Godot Asset Library and reputable open-source Godot/GitHub resources
for tools and assets that could move Living Myth toward its **stylized semi-realistic fantasy
pixel diorama / parchment-atlas** look — without touching the deterministic sim or the presentation-only viewer
architecture. **Nothing here is installed.** This is a shortlist for later, deliberate evaluation.

- **Date:** 2026-06-13
- **Engine target:** Godot **4.6.3 mono (C#/.NET)**
- **Renderer reality:** the atlas is a **custom 2D `_Draw()` canvas** (lines/arcs/textures), not a
  TileMap and not node-based. This is the single biggest filter below: node-centric helpers
  (Camera2D extensions, node minimaps, Y-sort addons, dockable containers) only pay off if the
  viewer ever adopts node rendering. The highest-fit items are **CanvasItem shaders**, **permissive
  asset packs**, and **pipeline tools** that feed the existing Blender→PNG flow.

## Standing rules (this scout obeyed them; so must any follow-up)
- **License preference:** MIT, Apache-2.0, BSD, CC0, Unlicense, ISC. Every recommended item below
  is one of these, verified against the actual repo/page.
- **Flag** GPL/LGPL/AGPL, proprietary, unclear, or missing licenses (see *License flags*).
- **Do not import unknown assets into the main project.** Test in a throwaway Godot project or a
  sandbox branch first (see *Sandbox protocol*).
- **Nothing may replace the deterministic sim/viewer architecture.** Shaders/assets/tools only.
- **Determinism is untouched** by anything here — these are presentation/pipeline/editor concerns.

## Adoption policy — art direction, Kenney, license, AI (binding on every candidate)
- **Visual direction filter (overrides fit and price).** Every imported or candidate asset, tool,
  or shader must serve the locked look: **stylized semi-realistic fantasy pixel diorama** — serious,
  mythic, illustrated, material-rich, miniature-world, pixel-readable. Reject anything cute,
  retro-arcade, toy-like, board-game, bright-generic-fantasy, or effect-gimmick, no matter how
  permissive or convenient it is.
- **Kenney is CONDITIONALLY approved.** Use Kenney only for grounded, muted, medieval,
  semi-realistic low-poly / reference / kitbash work. Do NOT use bright, cute, arcade, toy-like, or
  generic cheerful Kenney assets. Raw untreated Kenney art is NOT final Living Myth art — it must be
  treated (palette / material / outline / dither finishing) and pass art-direction review before it
  ships.
- **Provenance is mandatory.** Any imported third-party asset, tool, or shader requires its exact
  source URL, license, and adoption notes recorded. Reject GPL / AGPL / unclear-license items by
  default unless Drew explicitly approves them.
- **No runtime generative AI** in the shipped game, and no AI-generated runtime story text.
  AI-assisted development / asset tooling is allowed, but its outputs must pass license + quality +
  art-direction review.

## Cross-cutting caveats (read before trusting any single entry)
1. **godotshaders.com is per-shader licensed.** The site lets submitters pick CC0, MIT, **or
   GPLv3**. There is no site-wide license — every shader's own page carries the stamp. Confirmed
   CC0 individually for the ones marked CC0 below; re-check any future pull on its own page.
2. **GDQuest repos split code vs art.** Their *code/shaders* are MIT (safe for a paid Steam
   release); some *art assets* are CC-BY-NC-SA (**non-commercial — cannot ship**). Use the shaders,
   never their textures.
3. **C#↔GDScript interop seam.** Nearly every Godot UI addon is GDScript. Two modes cross the
   language line for free: **`.tres` theme resources** and **SVG icons** (load identically from C#),
   and **`@tool` editor plugins** (run in the editor regardless of game language). Calling an
   addon's GDScript *nodes* from C# at runtime works but is stringly-typed and ugly — flagged per
   item. Often the right move is to **lift the pattern into C#** rather than take the runtime dep.
4. **Dimension mismatch.** Several "map" references are 3D mesh systems (Paradox-style). Mine their
   *data/interaction* patterns; they are not drop-ins for a 2D `_Draw()` canvas.

---

## Top 5 candidates (the short list)

| # | Candidate | License | Why it's top-5 | Class | Rec |
|---|---|---|---|---|---|
| 1 | **Retro Parchment Paper** (shader) | CC0 | The single most direct match for the parchment-ink chronicle backdrop/UI — drop on a ColorRect behind the atlas or on dock panels. CanvasItem, current, self-contained. | shippable | strong |
| 2 | **Lucide Icons** (SVG set) | ISC (+MIT subset) | 1,600+ clean line glyphs, engine-agnostic SVG, **zero interop risk**, no attribution required. Standardizes the ad-hoc unicode marks (`❀ ✕ ◆ ⟲`); line look pairs with parchment-ink. | shippable | strong |
| 3 | **Efficient 2D Pixel Outlines** (shader) | CC0 | Crisp CanvasItem outlines on markers and the painted island silhouette — the painted-pixel edge without a TileMap. | shippable | strong |
| 4 | **TexturePacker Godot Plugin** | MIT | Most direct win for the existing Blender→PNG pipeline: packs PNGs into `AtlasTexture`s you read in `_Draw()` via `DrawTextureRectRegion`. Godot-4-current, reputable. | shippable (importer) | strong |
| 5 | **Kuwahara painterly shader** | CC0 | Highest *upside* for the literal "painted" goal — an oil-painting post-pass over the rendered atlas. Listed for Godot 3.4, so **port + sandbox first** (the only top-5 with real porting risk). | prototype→shippable after port | test in sandbox |

**Why these five:** they cover the four levers that actually move the look on a custom 2D canvas —
**UI/atlas backdrop** (1), **iconography** (2), **marker/edge styling** (3), **asset throughput**
(4), and **painterly post** (5) — and every one is permissive. Items 1–4 are low-risk and close to
shippable; item 5 is the boldest aesthetic bet and is gated behind a sandbox port. Honorable
mentions that just missed: **GDQuest godot-shaders** (MIT reference library), **ThemeGen** (MIT
theming tool), and **Coding-Solo/godot-mcp** (editor launch/run/debug-capture for the F5 feel-test
loop — the workflow pain we keep hitting).

---

## Bucket 1 — 2D shaders & postprocess

### Retro Parchment Paper — **strong**
- **Link:** https://godotshaders.com/shader/retro-parchment-paper/
- **Category:** parchment/paper texture (sepia, vignette, procedural grain, ink-bleed)
- **Godot:** `shader_type canvas_item` (4.x syntax); posted 2026-03, updated 2026-05 — current
- **License:** **CC0** (page-verbatim: "under CC0 license and can be used freely")
- **Support:** community single-author, very recent
- **Helps with:** parchment chronicle UI + atlas backdrop
- **Risk:** low · **Class:** possible shipped dependency · **Rec:** strong candidate

### Efficient 2D Pixel Outlines — **strong**
- **Link:** https://godotshaders.com/shader/efficient-2d-pixel-outlines/
- **Category:** outline (round/square, thickness, color)
- **Godot:** Godot 4, `canvas_item`; updated through 2026-01
- **License:** **CC0** · **Support:** community, actively updated
- **Helps with:** outlines on site/home/territory markers; painted island silhouette
- **Risk:** low · **Class:** possible shipped dependency · **Rec:** strong candidate

### Kuwahara Shader (painterly / oil-painting) — **test in sandbox**
- **Link:** https://godotengine.org/asset-library/asset/1183 · repo https://github.com/PeterEve/godot-kuwahara
- **Category:** painterly post-pass (Kuwahara filter), `canvas_item` variant
- **Godot:** Asset Library lists **3.4**; not restamped for 4.x — expect a small port
- **License:** **CC0-1.0** (confirmed on asset page + repo) · **Support:** 27★, low activity
- **Helps with:** the literal "painted diorama" feel as a fullscreen post-pass
- **Risk:** med (Godot-3 port) · **Class:** prototype → shippable after port · **Rec:** test in sandbox

### Boujie Water Shader — **maybe (reference)**
- **Link:** https://github.com/Chrisknyfe/boujie_water_shader
- **Category:** water (Gerstner waves, foam, Fresnel) — **3D mesh ocean, not 2D**
- **Godot:** 4.1+ · **License:** **MIT** · **Support:** 177★, last release 2023-09
- **Helps with:** reference for shoreline foam / Fresnel math (NOT a 2D drop-in)
- **Risk:** med (wrong dimension) · **Class:** reference · **Rec:** maybe (mine the foam math)

### 2D Fog Overlay — **test in sandbox**
- **Link:** https://godotshaders.com/shader/2d-fog-overlay/
- **Category:** animated FBM fog over a noise texture
- **Godot:** `canvas_item`; posted 2021 — noise-uniform setup may need a Godot-4 (`FastNoiseLite`/`NoiseTexture2D`) port
- **License:** **CC0** · **Support:** community, old but trivial
- **Helps with:** mist over wilderness/unexplored regions; famine-scar mood
- **Risk:** med (2021 port) · **Class:** prototype → shippable after port · **Rec:** test in sandbox

### Animated 2D Fog (w/ pixelation) — **maybe (verify license)**
- **Link:** https://godotshaders.com/shader/procedural-2d-fog-with-pixelation/
- **Category:** fog with optional pixelation (on-theme)
- **Godot:** Godot 4, `canvas_item`
- **License:** **UNVERIFIED — open the page, confirm CC0/MIT vs GPLv3 stamp before use**
- **Risk:** med (license) · **Class:** prototype-only until verified · **Rec:** maybe

### Glow effect 2D — **maybe (verify license + env dependency)**
- **Link:** https://godotshaders.com/shader/glow-effect-2d/
- **Category:** glow/bloom (`canvas_item`; leans on a WorldEnvironment glow pass)
- **Godot:** Godot 4 · **License:** **UNVERIFIED — needs manual check**
- **Helps with:** soft glow on echo/memorial marks, divine pulses, important-event highlights
- **Risk:** med (license + HDR-2D/WorldEnvironment setup) · **Class:** prototype-only · **Rec:** maybe
- *Lower-priority siblings on godotshaders.com (license per-page): "2D Glow Screen (No WorldEnvironment)" and a canvas_item "Bloom" — the no-env one avoids the HDR-2D setup.*

---

## Bucket 2 — 2D map / rendering helpers

> Architecture filter: these are node-centric. They only help if the viewer adopts a real
> `Camera2D` / node scene tree. As long as the atlas stays a custom `_Draw()` canvas, prefer
> studying their math and reimplementing in C#.

### Phantom Camera — **maybe (if you node-ify the view)**
- **Link:** https://github.com/ramokz/phantom-camera · AssetLib https://godotengine.org/asset-library/asset/1822
- **Category:** Cinemachine-style camera (Camera2D + Camera3D)
- **Godot:** **4.4+** (runs on 4.6) · **License:** **MIT** · **Support:** very active, 3.4k★, v0.11.x (Mar 2026)
- **Helps with:** smooth damped follow/pan/zoom between regions/souls (your `FocusPerson/FocusRegion`)
- **Risk:** med (needs your transform routed through Camera2D) · **Class:** shipped dep if node-based, else reference · **Rec:** maybe (or mine the damping math)

### Camera2D+ — **ignore for ship / maybe for ideas**
- **Link:** https://godotengine.org/asset-library/asset/2205
- **Category:** camera juice (shake/flash/cinematic) · **Godot:** 4.0 (verify on 4.6)
- **License:** **MIT** · **Support:** light, single-maintainer, last update 2025-01
- **Helps with:** screen-shake/flash on battle/famine/turning-point beats
- **Risk:** med (node-based, untested 4.6) · **Class:** prototype-only · **Rec:** ignore for ship; maybe mine ideas

### Mini Map (sumri) — **ignore**
- **Link:** https://godotengine.org/asset-library/asset/4983
- **Category:** minimap node · **Godot:** 4.0 · **License:** **MIT** · **Support:** recent (2026-04), tiny
- **Risk:** high (assumes node scenes; your atlas *is* the map) · **Class:** prototype-only · **Rec:** ignore

### Y-sort / sprite layering — **use built-in only**
- Godot 4 has native Y-sorting (`YSortEnabled`); no third-party addon is worth adding, and it's moot
  for a `_Draw()` canvas where draw order is already explicit. **Rec:** ignore third-party.

---

## Bucket 3 — UI tools

### Lucide Icons — **strong**
- **Link:** https://github.com/lucide-icons/lucide · LICENSE https://github.com/lucide-icons/lucide/blob/main/LICENSE
- **Category:** 1,600+ SVG line icons (community fork of Feather)
- **Godot:** engine-agnostic SVG (Godot 4 imports SVG natively) — **no interop seam**
- **License:** **ISC** overall (verified verbatim; *not* MIT as some secondary sources claim) **+ MIT**
  for the Feather-derived subset. Both permissive, **no attribution required**.
- **Support:** 23k★, v1.19.0 (Jun 2026), extremely active
- **Helps with:** consistent glyphs for feed channels, dock buttons, Region Lens, filter chips
- **Risk:** low · **Class:** possible shipped dependency (asset) · **Rec:** strong candidate

### ThemeGen — **test in sandbox (best theming fit)**
- **Link:** https://github.com/Inspiaaa/ThemeGen
- **Category:** programmatic theme generator → reusable styles/semantic colors → `.tres`
- **Godot:** 4.x · GDScript **editor-time tool**; output is C#-consumable `.tres`
- **License:** **MIT** · **Support:** 241★, v1.4.0 (May 2026), active
- **Helps with:** single-sourcing the parchment palette/StyleBoxes the way `UiTheme.cs` already does
- **Risk:** low (build-time; nothing ships) · **Class:** tool-only (output could be a shipped `.tres`) · **Rec:** test in sandbox

### GodotRichTextLabel2 (RicherTextLabel) — **test in sandbox (weigh interop)**
- **Link:** https://github.com/chairfull/GodotRichTextLabel2
- **Category:** RichTextLabel/BBCode helper + text animation (the maintained Godot-4 successor to the 3.x `godot-text_effects`)
- **Godot:** 4.x · GDScript (ships custom nodes) · **License:** **MIT** · **Support:** 268★, v1.14 (Jan 2025)
- **Helps with:** color-name tags, expression interpolation, **char-by-char reveal/fade** for the
  chronicle feed, guard cards, chapter recaps (maps onto the dramatic auto-slow)
- **Risk:** med (its `RicherTextLabel` is a GDScript node → C# interop seam) · **Class:** shipped dep w/ interop cost, or pattern-only · **Rec:** test in sandbox — or reimplement its RichTextEffect patterns in C#

### Godot Theme Template — **maybe (reference asset)**
- **Link:** https://github.com/jonathanlake/godot-theme-template
- **Category:** base `.tres` recreating the default theme for customization
- **Godot:** 4.x · asset-only · **License:** **MIT** · **Support:** 8★, low signal
- **Helps with:** a fully-wired starting `.tres` so you don't theme each Control class from scratch
- **Risk:** low (pure data) · **Class:** reference asset · **Rec:** maybe

### Godot Dockable Container — **maybe (only if you want rearrangeable docks)**
- **Link:** https://github.com/gilzoide/godot-dockable-container
- **Category:** binary-tree tiling/docking panels, tabs, drag-to-rearrange
- **Godot:** 4.x · GDScript node · **License:** **CC0-1.0** · **Support:** 248★, active
- **Helps with:** the Watch/Inspect/Chronicle panel economy — but heavier than your fixed-dock
  contract and may **fight** the deliberately-authored `VISUAL_STYLE.md` panel design
- **Risk:** med (interop seam + overlaps an intentional contract) · **Class:** shipped dep w/ interop cost · **Rec:** maybe (likely ignore — your dock contract is intentional)

### Bloodyaugust/godot-ui-component-library — **ignore**
- **Link:** https://github.com/Bloodyaugust/godot-ui-component-library
- **Category:** themable dropdown/searchable-select Controls · **Godot:** 4.x · GDScript
- **License:** **MIT** · **Support:** 16★, low · **Risk:** med (interop, little payoff)
- **Rec:** ignore — a god-sim viewer has few form controls; you already built the filter chips

---

## Bucket 4 — Asset pipeline helpers

### TexturePacker Godot Plugin (CodeAndWeb) — **strong**
- **Link:** https://github.com/CodeAndWeb/texturepacker-godot-plugin
- **Category:** atlas importer (`AtlasTexture`/`TileSet` from packed sheets)
- **Godot:** **4.0+** (godot-3 branch for old) · **License:** **MIT** (verbatim) · **Support:** maintained by the TexturePacker vendor, 94★, v4.3.0 (Dec 2025)
- **Helps with:** pack the Blender PNGs into atlases; read sub-rects in `_Draw()` via `DrawTextureRectRegion` — fewer textures, same draw code
- **Risk:** low–med — the importer is MIT/free; the **TexturePacker app** is a separate product (free tier exists). You can also pack with free tools and only use the importer.
- **Class:** possible shipped dependency (importer) / tool-only (the app) · **Rec:** strong candidate

### GodotSpriteGenerator (DanTrz) — **test in sandbox (your exact stack)**
- **Link:** https://github.com/DanTrz/GodotSpriteGenerator
- **Category:** in-engine 3D-model → 2D sprite/sheet generation, **C#**
- **Godot:** **4.4, C#, Forward+** (from `project.godot`) — same mono lane as Living Myth
- **License:** **MIT** · **Support:** 8★, 2026, early-stage
- **Helps with:** could generate sprites/sheets inside Godot in C# (color reduction, outlines), possibly skipping the Blender hop for some assets; valuable as C# reference either way
- **Risk:** med–high (young, low-star) · **Class:** tool-only / prototype-only · **Rec:** test in sandbox

### Blender-Spritesheet-Renderer (chrishayesmu) — **maybe (reference)**
- **Link:** https://github.com/chrishayesmu/Blender-Spritesheet-Renderer
- **Category:** Blender → transparent-PNG sprite sheets (multi-angle) · **License:** **MIT**
- **Blender:** tested **2.9** only — predates 4.x, needs porting · **Support:** stale (2021), 27★
- **Helps with:** mirrors your pipeline; mine its PNG/RGBA/transparent render-settings logic
- **Risk:** high (Blender-2.9 era) · **Class:** reference · **Rec:** maybe (study, don't depend)

### blender-spritesheets (theloneplant) — **maybe (reference)**
- **Link:** https://github.com/theloneplant/blender-spritesheets
- **Category:** animated 3D → sprite sheet + import metadata · **License:** **MIT**
- **Blender:** built on **2.81**, modern status uncertain · **Support:** 262★ but maintenance uncertain
- **Risk:** high (old target, stale) · **Class:** reference · **Rec:** maybe (best-known workflow; sandbox-test first)

### blender-godot-pipeline (bikemurt) — **ignore (wrong shape)**
- **Link:** https://github.com/bikemurt/blender-godot-pipeline
- **Category:** Blender↔Godot **glTF mesh** pipeline · **License:** **MIT** (Blender-side addon is a separate paid product)
- **Why ignore:** glTF/mesh-oriented; your pipeline outputs 2D PNGs · **Risk:** med · **Rec:** ignore (until/unless 3D-in-Godot)

### blender-godot-pipeline (indiedevcasts) — **ignore**
- **Link:** https://github.com/indiedevcasts/blender-godot-pipeline
- **Category:** glTF export helper · **License:** **MIT** · **Support:** 8★, self-flagged "might be bugged"
- **Risk:** high (immature, wrong asset shape) · **Rec:** ignore

---

## Bucket 5 — Demos / templates (learn patterns; do not copy gameplay)

### GDQuest — godot-4-procedural-generation — **strong (reference)**
- **Link:** https://github.com/gdquest-demos/godot-4-procedural-generation
- **Category:** procedural map/terrain demos incl. **WorldMap with biomes + rivers**
- **Godot:** 3 **and** 4 (separate `godot4/`) · **License:** **MIT** (code) + **CC-BY-4.0** (art)
- **Support:** ~1,900★, reputable, 123 commits
- **Helps with:** closest reference for region/biome layout + river logic; algorithms translate to your C# `WorldSurface`
- **Risk:** low · **Class:** reference · **Rec:** strong candidate (reference)

### GDQuest — godot-shaders — **strong (reference; shaders only)**
- **Link:** https://github.com/gdquest-demos/godot-shaders
- **Category:** large shader library + playable demos (glow, outline, water, clouds, dissolve, blur)
- **Godot:** mid-port to 4; last release 2024-09 · **License:** **MIT** (code/shaders) + **CC-BY-NC-SA 4.0** (art — **non-commercial, do NOT ship their textures**)
- **Support:** ~4,000★, reputable
- **Helps with:** reference implementations of the Bucket-1 effects with working demos
- **Risk:** low for shaders; med if you pull an NC art asset by mistake · **Class:** reference (shaders = possible shipped dep) · **Rec:** strong candidate (shaders only, never the art)

### Procedural World Map Generator (edwin-cox) — **maybe (viewer/zoom pattern)**
- **Link:** https://godotengine.org/asset-library/asset/1913 · repo https://github.com/edwin-cox/godot-infinite-worldmap
- **Category:** procedural world-map generator + high-perf zoom/nav viewer · **Godot:** 4.0 · **License:** **MIT**
- **Support:** v0.0.2 (2023-10), early-stage
- **Helps with:** the **viewer half** — progressive/adaptive rendering + zoom-nav for a large atlas (your MapView perf work)
- **Risk:** low (MIT); quality unknown · **Class:** reference · **Rec:** maybe (study the viewer approach)

### OpenGS (C#) — Grand Strategy Map — **test in sandbox (C# interaction patterns)**
- **Link:** https://github.com/JDSweet/opengs-csharp
- **Category:** Paradox-style map: province selection, map modes, smooth borders, dynamic labels — **C#** but **3D mesh** renderer
- **Godot:** unstamped (confirm 4.x) · **License:** **MIT** · **Support:** 5★, 35 commits, tutorial series, low activity
- **Helps with:** the **C# match** for region selection / map-mode toggling / label placement (mine the data/interaction patterns, not the 3D renderer)
- **Risk:** med (small/young, 3D, version unconfirmed) · **Class:** reference · **Rec:** test in sandbox

### 2D Procedural Map Generator (Unreference) — **maybe (lower priority)**
- **Link:** https://godotengine.org/asset-library/asset/3070 · repo https://github.com/DereferenceMyPointer/2D-Map-Generator
- **Category:** 2D procedural map framework (path + progression + environment fill) · **Godot:** 4.2 · **License:** **MIT**
- **Helps with:** clean MIT 4.2 reference for procedural placement loops (roguelike-ish, less island-biome relevant)
- **Risk:** low · **Class:** reference · **Rec:** maybe (lower priority than the GDQuest/edwin-cox refs)

---

## Bucket 6 — MCP / editor automation (CANDIDATES ONLY — editor-time, never shipped)

### Coding-Solo/godot-mcp — **maybe / test in sandbox (most reputable)**
- **Link:** https://github.com/Coding-Solo/godot-mcp
- **Category:** Godot MCP server (Node.js/TS) — launch editor, run project, capture debug/stdout
- **Godot:** 4 (incl. 4.4+ UID handling); Node ≥18. Drives the **editor**, emits **GDScript** for scene/script ops, **no documented C#/.NET awareness**
- **License:** **MIT** · **Support:** **4.2k★** — by far the most reputable in this space
- **Helps with:** the **F5 feel-test loop** — launch + run + debug-capture back to an agent (the exact manual pain we keep hitting); editor-time only, zero shipping risk
- **Risk:** med for a mono project (launch/run/debug tools are language-agnostic and useful; the GDScript scene/script-gen half is not, and could emit GDScript that doesn't belong in the C# viewer)
- **Class:** tool-only (never a dependency) · **Rec:** maybe / test in sandbox — use the launch/run/debug subset only

### mkdevkit/godot-mcp — **ignore (unproven)**
- **Link:** https://github.com/mkdevkit/godot-mcp · Node server + GDScript editor plugin (173 tools), Godot 4.4+, GDScript-only
- **License:** **MIT** · **Support:** **0★, 4 commits** — brand new · **Risk:** high · **Rec:** ignore (prefer the 4.2k★ option)

### alexmeckes/godot-mcp — **ignore**
- **Link:** https://github.com/alexmeckes/godot-mcp · TS, 99 tools, Godot 4.2+, GDScript projects · **License:** **MIT** · **Support:** 21★, low
- **Helps with:** live editor control, input simulation, **screenshot capture** (could automate feel-test shots) — but GDScript-assumed, modest adoption
- **Risk:** med-high · **Rec:** ignore — Coding-Solo covers the same useful subset with far more validation

---

## License flags (anything not cleanly permissive)
- **godotshaders.com per-page GPLv3 risk** — *Animated 2D Fog (pixelation)* and *Glow effect 2D* are
  **UNVERIFIED**; confirm the per-page stamp before any use. (Marked-CC0 entries were individually confirmed.)
- **GDQuest art assets are CC-BY-NC-SA (non-commercial)** — incompatible with a paid release. Their
  MIT shaders/code are fine; **never ship their textures**.
- **GDQuest procedural-generation art is CC-BY-4.0** — attribution required if used (code is MIT).
- **Paid/proprietary companions (not the recommended code):** the **TexturePacker app** and
  **bikemurt's Blender-side addon** are separate commercial products; the Godot-side importer code in
  both is MIT.
- No GPL/LGPL/AGPL-licensed item is *recommended* anywhere above. Everything in the Top 5 is CC0,
  ISC, or MIT, verified against the source.

## Sandbox protocol (before anything touches the main project)
1. **Throwaway Godot project first** (not the Living Myth tree) for any shader/addon trial — or a
   short-lived `spike/<thing>` branch if it must run against real viewer data.
2. **Shaders** are lowest-risk: a single `.gdshader` on a `ColorRect`/`Sprite2D` proves the look with
   no project coupling. Port Godot-3-listed shaders (Kuwahara, 2D Fog) there first.
3. **Editor addons / MCP** run in the **mono editor** — verify they don't disturb the C# build or the
   `verify` baseline (they shouldn't; they're editor-time), and keep them out of `project.godot`'s
   shipped plugin list.
4. **Assets** (icons, atlases): import into the sandbox, eyeball at the atlas's real zoom levels,
   confirm license + transparency, *then* copy the vetted files into `godot/assets/`.
5. **Re-confirm license on the actual page at adoption time** — repos relicense; godotshaders.com is
   per-page.
6. **Determinism guard:** none of this can move `verify` (823/559/910/632). If a trial ever does,
   sim code was touched by accident — stop and revert.

## What stays out of scope (by rule)
- Anything that replaces the deterministic sim or the presentation-only viewer architecture.
- GDScript runtime *nodes* adopted casually into the C# viewer (interop seam) — prefer porting the
  pattern to C#.
- 3D mesh map systems as renderers (OpenGS, Boujie) — reference value only.
- Generative-AI runtime content (forbidden by the GAME_DESIGN.md AI Use Doctrine; none scouted).
