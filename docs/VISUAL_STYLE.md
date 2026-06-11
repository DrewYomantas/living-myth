# Living Myth — Visual Style Bible (Living Atlas Foundation)

The implementation-facing companion to `DESIGN.md`. `DESIGN.md` is the strategic art
direction (thesis, reference stack, pillars, palette guardrails, concept-art batch plan);
this file locks the **Batch 1 "Style Lock / North Star"** references into concrete,
buildable viewer language: which components exist, what each reference element maps to in
code, what is honest today, what is aspirational, and what is forbidden until the sim
models it. Future Claude Code sessions doing visual work start here.

Everything in this file is **viewer-only**. Nothing here may touch `src/LivingMyth.Sim/`
or move the verify baseline (`dotnet run --project src/LivingMyth.Console -- verify`,
currently 884/699/567/706 for seeds 1/18/42/7).

## Visual thesis

Living Myth is a warm mythic pixel diorama presented as a **living atlas**: a sacred
map-table where history unfolds, wrapped in a parchment-and-ink chronicle UI. The world
reads as a place (painted island, named sites, roads, banners); the UI reads as a history
book laid over it (parchment cards, soft ink borders, brass/gold accents, serif editorial
type). Never a SaaS dashboard, never a glossy mobile god-game.

## The Batch 1 north-star references

Four generated concept images in `Visual references/` (see that folder's README — they
are **concept references only**, never in-game assets):

| File | View | What it locks |
|---|---|---|
| `gpt-northstar-atlas-view.png` | Wide atlas | Whole-island composition: painted landmass on deep teal-slate sea, parchment place tags, restrained faction identity, centered world title, right saga feed, bottom fate/time docks |
| `gpt-northstar-region-view-ashen-vale.png` | Region view | Entering a place: dense diorama terrain, gazetteer card on the left (overview/leader/faith sections), place tags on local sites |
| `gpt-northstar-site-view-greymarket.png` | Local/site view | One settlement as a social subject: market lanes, people, features list, person hover ("Maia — click to inspect") |
| `gpt-northstar-chronicle-replay.png` | Chronicle replay | "How We Got Here" as a glowing path across the world: numbered turning points, alternate-path ghost, event detail card, timeline scrubber |

### Reference interpretation — honest / aspirational / forbidden

Each image mixes three kinds of content. Read them with this discipline:

**Honest today (sim truth or already-shipped viewer derivation):**
- Year + souls + tales card (top-left) — `World.Year`, `LivingCount`, chronicle count.
- The Saga feed with event-class chips, small-caps category labels, year stamps.
- Region names (`Region.Name`), terrain types, holders, adjacency — sim truth.
- Place-kind tags ("sacred grove", "hill fort") — deterministic viewer hints
  (`PlaceSeeds`), already disclosed as such in the Region Lens.
- Inspect / Follow / Curse tools; speed ladder; drama toggle.
- Catch-up causal threads ("How We Got Here" text view) — real `Event.Causes` chains.

**Aspirational (build toward, in roadmap order):**
- Painted/diorama terrain, settlement clusters as tiny dioramas, local lanes and people
  at site scale (needs renderer work, then sim site contracts).
- Chronicle replay as a visual path with numbered turning points and a timeline
  scrubber (real events + cause-links only; replay is a renderer for the chronicle).
- A named world ("The Mossenwild") — needs a deliberate world-naming pass (authored
  name pools, deterministic), not faked before that.
- Site/feature lists, settlement populations, regional resources — need sim contracts
  (see PROJECT_STATE.md "Region Lens — data contracts still missing").

**Forbidden to render until truly modeled (the honesty contract):**
- Resource counts (stone/wood/grain/iron/herbs) — no economy at that granularity.
- Per-settlement population ("98 souls") — people are not anchored to sites.
- Named buildings/features ("Market Square", "Fishery Docks") — no site state.
- Event location pins for events that carry no `RegionId`.
- God tools that don't exist: **Bless, Prophecy, Plague, Terrain** appear in the
  references — do NOT render them (not even disabled) until the sim has them. Only
  Inspect, Follow, and Curse are real.
- Any generative-AI storytelling, any network calls.

## Target zoom levels

The four scales from `DESIGN.md`, with current status:

1. **Atlas (whole island)** — SHIPPED, abstract: island polygon + shallows rim,
   territory tints, roads, place-seed markers, parchment place tags (zoom-gated),
   faction tags, event pulses.
2. **Region Lens** — SHIPPED as inspector + gold ring + always-on parchment tag for the
   selected region. Future: V3 gazetteer card, then local terrain once region polygons
   exist.
3. **Local / site** — NOT BUILT. Blocked on the settlement/site sim contract. Do not
   fake it.
4. **Chronicle Replay** — text-only today (catch-up modal). The visual path is a major
   future feature; prototype only with real events and cause-links.

## UI component language (the actual API)

All UI styling routes through `godot/UiTheme.cs` (`Ui.*`). Components and their recipes:

| Component | Helper | Use |
|---|---|---|
| Parchment panel | `Ui.PanelBox()` | Year card, saga feed, inspectors, catch-up modal, bottom bar |
| Gazetteer row | `Ui.RowBox(bg, border)` | Feed rows, buttons (via `StyleButton`) |
| Event medallion | `Ui.ChipBox(color)` | Feed chip: event-class color circle, 1px darkened rim, glyph in `ParchmentHi` |
| Parchment map tag | `Ui.ParchmentTag(selected)` | Atlas place-name pill: region name (SerifBold 13, InkDeep) over place-kind hint (SmallCaps 10, Faded); gold border when its Region Lens is open |
| Dock frame | `Ui.DockBox()` | Bottom-bar group frames (Time / Lens / Chronicle) |
| Section label | `Ui.SectionLabel(text)` | Small-caps uppercase headers, dock captions, feed category labels |
| Buttons | `Ui.StyleButton(b, active, activeBg)` | Parchment face, gold when active, ember for the curse tool |
| Event classes | `Ui.ClassOf(type)` | 22 event types → (label, color, glyph); the chronicle's icon language |
| Soft dark map tag | `MapView.LabelTag` | Faction tags + hover tag — dark backing for text over open terrain (kept distinct from parchment pills on purpose: cloth-and-ink for peoples, parchment for places) |

Composition rules:
- Hierarchy through tone and border, never size jumps or drop-shadow stacks.
- Gold (`Gold`/`LensGold`/`GoldGlow`) is for *selection, drama, and the player's mark*
  (Yours rows, lens ring, pulses, leader rings) — never decoration.
- Rounded corners stay small (6–12px); no pill-shaped panels except chips/tags.
- No flat hard-modern shapes, no neon outlines, no glossy gradients.

## Color & material direction

UI palette lives in `UiTheme.cs`; map paint lives at the top of `MapView.cs` (kept local
deliberately — it has one consumer and a different job: world surface vs parchment shell).
Shared accents (`LensGold`, `Gold`, `Ember`, `Parchment`, `RowBorder`, `GoldGlow`) are
single-sourced in `Ui` — never re-inline hex duplicates in MapView.

- **Parchment shell:** `Parchment f2e5c2` / `ParchmentHi` / `ParchmentLo`, ink text
  (`Ink 3a2c19`, `InkDeep`), warm borders (`PanelBorder 5c4830`, `RowBorder c9b288`).
- **Atlas:** sea `22424d` (muted teal-slate) with shallows rim `2e5560`, land `474c31`
  (dry-grass-warmed moss), coast `5d6242`, neutral wilderness `6f6a58`.
- **Faction cloth:** muted banner colors (highland `6b7a99`, shore `4f8f89`, wood
  `5d8a4e`) — cloth accents, pennants, and territory hints, never paint spills.
- **Materials:** timber/thatch/stone/dirt marker palette per DESIGN.md ("ancient, not
  generic").
- Guardrails (from DESIGN.md): no neon, no candy greens, no pure saturated blues;
  ritual gold sparingly.

## Typography

- **Alegreya** (serif, OFL) — body and titles; `SerifBold` (wght 800) for names, year,
  panel titles. **Alegreya SC** — small-caps system voice: section headers, category
  labels, dock captions, map tag subtitles.
- Scale in use: 30 year numeral · 19–20 panel titles · 13–14 body/map names ·
  11–12 metadata · 10–11 small-caps labels.
- Editorial tone: short, concrete, old-world but not flowery ("The Saga", "How We Got
  Here", "the old road remembers"). Glyphs (⚔ ♛ ☾ …) come from `Ui.ClassOf` with the
  engine fallback font chained behind Alegreya.

## Per-view direction (smallest honest next steps)

**Atlas.** Shipped this pass: warmed sea/land, shallows rim, parchment place tags
(zoom ≥ 2, always for the selected region), fainter adjacency web. Next (Atlas
Composition Pass): island silhouette identity, terrain banding, label/typography
balance at fit zoom, region-circle de-emphasis so markers+tags carry place identity.

**Region Lens.** Shipped: lens inspector with honest "not yet modeled" notes,
cross-links, gold ring + named map tag. Next (Lens V3): gazetteer-card layout closer to
the Ashen Vale reference using only honest fields (holder, terrain, neighbours, anchored
tales, customs of the holder), local-feel framing.

**Chronicle Replay.** Today: parchment catch-up modal (Quick beats / Full thread).
Next (prototype): draw the causal chain as a restrained glowing path over the atlas for
events that carry `RegionId`, numbered beats, no invented locations — events without a
region stay in the side list.

**Bottom dock.** Shipped: captioned parchment-framed groups (Time / Lens / Chronicle).
Fate tools join the dock **only as the sim grows them**.

## Visual check ritual (the F5 checklist)

No screenshot harness exists (and none is warranted yet). After any visual pass, F5 the
mono Godot editor and compare these states against the named reference:

1. **Early world (year ~10–50), fit zoom** — atlas mood vs `gpt-northstar-atlas-view`:
   sea/shallows/land tones, marker+banner readability, no tag clutter.
2. **Mid-game (year 300+, drama on)** — saga feed density vs atlas reference's feed:
   chips, category colors, breathing room; pulses read over labels.
3. **Region Lens open (held region, then wild region)** — gold ring + gold-bordered
   place tag at any zoom; honest not-modeled copy intact; vs region-view reference.
4. **Faction inspector open** — territory ring emphasis, customs section.
5. **How We Got Here modal** — parchment thread vs chronicle-replay reference (text
   form for now).
6. **Zoom ≥ 2 over the densest cluster** — place tags: skip-on-overlap working, names
   legible, tags not dominating the diorama.
7. **Bottom docks** — three framed groups, toggles restyle (gold) correctly.

## Sim-truth honesty contract (restated, binding)

The viewer may *derive* presentation (place kinds, scatter positions, road kinks, island
outline) deterministically from sim state — and must label such derivations as hints
where the player could mistake them for simulation (the Region Lens does this). The
viewer must **never invent** sim facts: no fake settlements, populations, resources,
buildings, home regions, event locations, world names, god tools, or narrative. Pacing
and camera are wall-clock presentation only — `Tick()` count and order are sacred.
Viewer-only passes must leave `verify` at its recorded baseline; if it moves, sim code
was touched by accident — stop and investigate, never re-baseline silently.

## Staged visual roadmap

1. **Visual Theme Consolidation** — ✅ this pass: style bible, shared accent
   single-sourcing, parchment tags, dock frames, atlas mood warm-up, medallion chips.
2. **Atlas Composition Pass** — island silhouette identity, terrain bands, fit-zoom
   label balance, territory de-emphasis. Viewer-only.
3. **Region Lens V3** — gazetteer card layout (honest fields only), richer anchored
   tales, local identity framing. Viewer-only.
4. **Chronicle Replay Prototype** — glowing causal path over the atlas for
   region-anchored events; numbered beats; timeline scrub comes with the separately
   planned timeline-scrubbing milestone. Viewer-only.
5. **Terrain Geometry / Diorama Exploration** — viewer-side region polygons (Voronoi
   or authored bands) so the atlas reads as landforms; gateway to diorama rendering.
   Viewer-only, deterministic from seed.
6. **Site/Settlement Data Contract** — sim-side: 3–7 deterministic sites per region,
   `Person.HomeRegionId`, broader `Event.RegionId` coverage (see PROJECT_STATE.md).
   **Moves the verify baseline; deliberate sim milestone.** Unlocks honest buildings,
   features, people-at-site, and settlement populations — everything in the
   "forbidden" list above graduates to "honest" only through this gate.

## Current viewer audit (2026-06-10, post-foundation-slice)

**Already strong:** centralized `UiTheme` (almost no hardcoded styling left in Main.cs — a
handful of pre-existing inline colors remain, candidates for roadmap item 1 cleanup); parchment
shell on every panel; Alegreya serif + small-caps voice; saga feed rows structurally
match the reference (chip + category + year + body); sectioned inspectors with honest
disclosure copy and cross-links; deterministic place markers, roads, banner pennants;
gold lens ring; event pulses; drama pacing + camera.

**Clashes with target (small code, near-term):** region identity still circle-first at
fit zoom; faction tags are plain dark pills (could take parchment treatment later);
year-card / world-title composition diverges from the reference's centered title (blocked
on world naming); inspector is BBCode text rather than a card layout (Lens V3).

**Needs renderer work (not small):** painted/banded terrain, settlement diorama
clusters, chronicle replay path, site-scale view.

**Needs sim contracts first:** everything in the forbidden list; see roadmap item 6.
