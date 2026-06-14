# Living Myth — Visual Style Bible (Living Atlas Foundation)

The implementation-facing companion to `DESIGN.md`. `DESIGN.md` is the strategic art
direction (thesis, reference stack, pillars, palette guardrails, concept-art batch plan);
this file locks the **Batch 1 "Style Lock / North Star"** references into concrete,
buildable viewer language: which components exist, what each reference element maps to in
code, what is honest today, what is aspirational, and what is forbidden until the sim
models it. Future Claude Code sessions doing visual work start here.

Everything in this file is **viewer-only**. Nothing here may touch `src/LivingMyth.Sim/`
or move the verify baseline (`dotnet run --project src/LivingMyth.Console -- verify`,
currently 823/559/910/632 for seeds 1/18/42/7).

## Visual thesis

Living Myth is a **stylized semi-realistic fantasy pixel diorama** presented as a
**living atlas**: a sacred map-table where history unfolds, wrapped in a parchment-and-ink
chronicle UI. Stylized and warm, mythic and semi-realistic — pixel-rooted but with painterly
depth, light, and material weight; never flat retro pixel art, never photoreal. The world
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

### Batch 2 references (2026-06-12) — identity and attention

Six more concept images (`Visual references/gpt-b2-*.png`, concept-reference-only): the
style is first named as a **stylized semi-realistic fantasy pixel diorama** presented as the
living mythic atlas — now the locked visual thesis above. What Batch 2 locks beyond Batch 1 is the *identity language* — and
it arrived alongside the playtest verdict that cast-tracking is the build's weak point:

| File | What it locks |
|---|---|
| `gpt-b2-soul-follow-card.png` | The followed-soul card: portrait/mark + role line + "you last saw" + recent beats + follow verbs — much already shipped in text form |
| `gpt-b2-person-glimpse-portraits.png` | Person glimpse with portrait, age, reputation label, children, faith chip |
| `gpt-b2-faction-lens-filters.png` | Faction lens with sigil banner, leader portrait, lands list, anchored tales; chronicle category filters |
| `gpt-b2-remembered-places.png` | "Remembered Places" — Place Memory surfaced as a panel of scarred sites |
| `gpt-b2-memorial-in-memoriam.png` | The In-Memoriam card — its italic epitaph line IS the shipped memorial-inscription slot (player canon) |
| `gpt-b2-pulse-turning-point.png` | Turning-point pulse constellation between faction nodes + a chronicle-pulse timeline |

Honest today or one slice away: everything The Cast milestone builds (identity marks,
role lines, last-beat), the epitaph slot (shipped), last-seen/last-deeds (shipped),
follow verbs (shipped), chronicle filters (cheap). **Aspirational:** portraits (the
deterministic sigil is the V1 stand-in), the pulse constellation, Remembered Places as
a panel. **Forbidden until modeled:** resource counts with deltas, "recent beats" of
unmodeled daily life (trained with the guard, spoke at the hearth), attitude chips
(devout/steadfast as *checkboxes* — devout exists only as a custom), trade routes,
watcher posts, sites, omen intensity, mechanical effect labels ("+10% loyalty",
"chance of secession"), drought place marks (no drought events exist; battle marks
SHIPPED 2026-06-13 — battle events now carry a real front anchor).

### Reference interpretation — honest / aspirational / forbidden

Each image mixes three kinds of content. Read them with this discipline:

**Honest today (sim truth or already-shipped viewer derivation):**
- Year + souls + tales card (top-left) — `World.Year`, `LivingCount`, chronicle count.
- The Saga feed with event-class chips, small-caps category labels, year stamps.
- Region names (`Region.Name`), terrain types, holders, adjacency — sim truth.
- Place tags on local sites ("sacred grove", "hill fort") — REAL places since Sites V1
  (2026-06-12): the sim's deterministic site read-model (`Sites.cs`), each a named,
  typed, terrain-honest position on the land. The old `PlaceSeeds` viewer hints are
  retired from the map.
- Inspect / Follow / Curse tools; speed ladder; drama toggle.
- Catch-up causal threads ("How We Got Here" text view) — real `Event.Causes` chains.

**Aspirational (build toward, in roadmap order):**
- Painted/diorama terrain, settlement clusters as tiny dioramas, local lanes and people
  at site scale (needs renderer work, then sim site contracts).
- Chronicle replay as a visual path with numbered turning points and a timeline
  scrubber (real events + cause-links only; replay is a renderer for the chronicle).
- A named world ("The Mossenwild") — needs a deliberate world-naming pass (authored
  name pools, deterministic), not faked before that.
- Settlement populations, named buildings/features, regional resources — still need sim
  contracts (site LISTS shipped with Sites V1; what stands AT a site did not).

**Forbidden to render until truly modeled (the honesty contract):**
- Resource counts (stone/wood/grain/iron/herbs) — no economy at that granularity.
- Per-settlement population ("98 souls") — people are not anchored to sites.
- Named buildings/features ("Market Square", "Fishery Docks") — no site state.
- Event location pins for events that carry no `RegionId`.
- God tools that don't exist: **Prophecy, Plague** appear in the references but remain
  unmodeled — do NOT render them (not even disabled) until the sim has them.
  *(Original Batch-1 reading, now SUPERSEDED: this once also forbade Bless and Terrain
  and said "Only Inspect, Follow, and Curse are real." God-Hand V1 shipped Bless,
  Protect, Doom, Omen, SeedForest, and CallSpring — see the God-Hand visual language
  section below for the live mark table; only Prophecy/Plague stay forbidden.)*
- Any generative-AI storytelling, any network calls.

## Target zoom levels

The four scales from `DESIGN.md`, with current status:

1. **Atlas (whole island)** — SHIPPED, abstract: island polygon + shallows rim,
   territory tints, roads, place-seed markers, parchment place tags (zoom-gated),
   faction tags, event pulses.
2. **Region Lens** — SHIPPED as inspector + gold ring + always-on parchment tag for the
   selected region. Future: V3 gazetteer card, then local terrain once region polygons
   exist.
3. **Local / site** — DATA SHIPPED (Sites V1: real sites, markers, tags, Site Cards);
   the immersive site-scale VIEW is still not built. Do not fake what stands at a site.
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
| Watched-soul mark | `MapView` soul halo + `SoulNameTag` | The divine bookmark: breathing gold halo (min findable size at fit zoom), saga-sighting flare, gold-bordered ★-name pill; clicking opens the living soul glimpse card (Main) |

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
- **Materials:** timber/thatch/stone/dirt marker palette per DESIGN.md — stylized fantasy,
  not generic fantasy-MMO; prefer ancient-world materials.
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

## Visual Storytelling Doctrine — the sim should be seen before it is read

The Living Diorama north star: a player looking at the map should begin to understand
**who matters, what changed, where memory lives, and which lives are being watched**
before opening a single text card. Cards, tooltips, catch-up, and replay exist to
*explain visual facts the player has already noticed* — they must never be the only
way the player learns the sim exists. When a sim truth has no visual voice, that is a
named gap in the audit below, not a permanent arrangement.

The vocabulary, per subject (status: ✅ shipped · ◐ partial · ✎ text-only · ⛭ modeled
but invisible · ✖ not modeled, forbidden to render):

**Souls / people**
- ✅ Normal soul — faction-colored dot (women lightened), deterministic scatter inside
  their people's lands (disclosed as presentation, not homes).
- ✅ Watched soul — the **divine bookmark**: breathing warm-gold halo with a minimum
  findable screen size at fit zoom, gold-bordered ★-name tag (overlap-skip), a flare
  when a newly shown saga row truly names them, the living glimpse card on click.
- ✅ Leader — larger dot + thin `LensGold` ring (distinct from the halo by weight and
  steadiness: leader rings are thin and constant; the bookmark breathes).
- ✅ Cursed — ember-colored dot.
- ⛭ Elder / child / youth — age is real sim data but the dot doesn't show it.
  Candidate future slice (honest: derived from `BirthYear`).
- ⛭ Prophet — `IsProphet` is real but undrawn. Candidate glyph slice.
- ✎ Dead / remembered soul — exists only in cards and the chronicle; grave markers
  wait on the event-anchoring sim contract (Place Memory V1 is built and ready to
  receive death anchors the moment they exist).
- ✎ Killer / victim — real event context (`KillerId`, `Murdered`, `Avenged`) told in
  text only.
- ✖ Omen-marked souls — no omen/prophecy-as-promise system; forbidden.

**Peoples / factions**
- ✅ Color identity — muted banner colors (cloth, never paint spills).
- ✅ Banners/pennants on held regions; territory tint + ring; faction label tag with
  population + leader name.
- ◐ Leader presence — the leader's dot is ringed, but nothing links dot ↔ label
  visually.
- ◐ Faction follow attention — gold YOURS rows in the feed and the guard signal, but
  the *map* shows nothing for a followed people. Gap.
- ✎ Dominant faith / customs — real sim state, inspector text only.

**Regions / places**
- ✅ Wild (neutral, faint) vs held (tint + banner).
- ✅ Place identity — Sites V1: real named sites with terrain-honest types, marker
  silhouettes, zoom-gated name tags, Site Cards (no longer viewer hints).
- ◐ Contested — war exists but no per-region contested state; territory-change pulses
  are the only voice. Partly a sim-contract question.
- ✎ Quiet vs storied — anchored-tale counts live in the Region Lens text only.
- ◐ Scarred by events — Place Memory V1 (below): founding stones, war scars, cairns,
  culture ribbons from truly anchored events; memorial cairns for cairn-worthy lives
  remembered at home (V2 first slice). Battles/famine wait on the anchoring contract.

**Events**
- ✅ Anchored events — expanding gold region pulse + feed row flash + dramatic
  auto-slow + drama camera lean.
- ◐ Pulses are anonymous (class color/glyph lives only on the feed chip) — known gap,
  also flagged in the pacing doc ("pulses should carry identity").
- ✅ Unanchored events — feed/cards only, honestly: no invented locations, ever.

**Memory**
- Temporary: pulses (~1.2 s), feed-row flash, the rolling saga window.
- Persistent today: territory tint changes, leader rings moving, customs appearing in
  text — current-state — plus Place Memory V1 marks: real scars from truly anchored
  events, aged by sim-year.
- ◐ Mark coverage is bounded by what carries a true `Event.RegionId` (territory +
  culture today) — broader memory waits on the anchoring contract.

## Panel economy contract (Map-First V1, 2026-06-12 — binding)

The map owns the screen unless the player explicitly opens the chronicle. Every viewer
surface belongs to exactly one of three modes, and every new panel must declare its row
in the classification table before it ships.

**The three modes:**

1. **Watch Mode** (default, running) — the map dominates. Panels are compact HUD or
   toasts; nothing covers the map's center. At 1366×768 the map must stay visually
   dominant (the left column and feed rail are the only standing chrome).
2. **Inspect Mode** (player selected a person / region / faction) — exactly one
   inspector surface, docked in the left column under the cast. Opening it folds the
   cast to its sigil strip. Never floats, never stacks, never covers center map.
3. **Chronicle Mode** (player chose to read) — full-width reading and ceremony: the
   catch-up full thread, chapter recap, memorial, and the writing drawer. These may
   cover more of the screen because the player asked to read.

**Classification (as built):**

| Surface | Class | Placement |
|---|---|---|
| Year card + guard signal | persistent HUD | top-left |
| Recap chip / return chip | persistent HUD chip | by the year card / top-center |
| The Cast | persistent HUD, compact by default | left column top; collapsed = sigil chips + 1–2 names, expanded = full roles/ages |
| Saga feed | persistent HUD rail | right, 300px; world rows recede (0.78 alpha, 1 line) while focused unless loud |
| Bottom dock | persistent HUD | bottom, 78px |
| "A NEW THREAD" card | compact toast | top-center, ambient, self-fades |
| **Guard toast** | compact toast | top-center: why-you-care chip + the tale + Resume / the full tale / how we got here; pauses but never covers the map |
| Living soul glimpse | compact toast | near the marker, clamped to map |
| Person/Faction/Region inspector | side inspector | left column, under the folded cast |
| How We Got Here (quick beats) | side sheet | right, 400px over the feed rail |
| How We Got Here (full thread) | chronicle sheet | right, widened to 620px — the explicit deeper read |
| Fate Ledger | side sheet | right, 420px — shares the reading slot with How We Got Here (each closes the other) |
| Chapter recap | chronicle modal | center; only on an intentional pause or chip click |
| Full Focus Guard card | chronicle modal | center; only via toast/return-chip click |
| **Memorial** | ceremony modal | center + ink veil — the one earned interruption |
| Writing desk (canon) | writing drawer | right drawer over the feed + light ink wash (0.22, edit stays atomic); closing returns to the inspected object |

**The rules:**

- One major panel at a time: chronicle/ceremony surfaces (catch-up sheet, recap, full
  guard card, memorial, writing drawer) are mutually exclusive — opening one closes or
  outranks the others. A side inspector may coexist with a right sheet (opposite
  edges, no overlap), never with a second inspector.
- Stacking is structural, not behavioral: the cast and inspector share one left
  VBox column (overlap is impossible); reading surfaces share the right edge.
- Center-map blocking is reserved for Chronicle Mode and ceremony. The guard's
  default voice is the compact toast; the full card opens only on click. The memorial
  is the single event allowed to take the center unasked.
- Toasts point at the map, honestly: a true `RegionId` pulses the place; a
  home-only anchor gets remembered-home language in the gold-brown memory tint and
  pulses nothing.
- Map-first verbs: a cast click is "find them" — inspect + ease the lens onto their
  place (`MapView.FocusPerson`, the same deterministic scatter the dots use; dead and
  landless souls fall back to the home of their line, honestly nowhere if null).
  Manual pan/zoom always cancels an automated lens move.

**Attention (the player's divine gaze, in rank order)**
1. **Memorial** (followed soul's death) — dimmed world, ceremonial frame. Outranks all.
2. **Guard toast → card** — the compact pause toast (Map-First V1); the gold center card only on click.
3. **Chapter recap** — queued chip, never interrupts a guard card.
4. **Living glimpse / soul halo / saga side rule / pulses** — ambient, non-modal,
   never pause, never compete with the above.
- ✅ Followed soul — divine bookmark + glimpse + gold saga side rule.
- ✅ Followed bloodline — cyan rings + gold YOURS rows (cool kin-mark vs the warm
  soul-mark, deliberately distinct).
- ◐ Followed people — feed + signal only (no map voice).
- ✅ Followed land (region) — quiet persistent gold ring on the map (fainter and
  tighter than the lens ring), ★ state in the lens; events surface as YOURS through
  the two honest channels (tales anchored here, lives remembered here).
- ✖ Followed prophecy — future; needs a sim system.

## Living Atlas Surface V1 — SHIPPED (2026-06-12)

The island finally has a **data-driven editable skin**: `src/LivingMyth.Sim/WorldSurface.cs`
— a 96×96 deterministic cell grid (terrain class, elevation, vegetation, nearest-seat
region bridge) generated from the world seed via pure coordinate hashes (no Rng stream at
all), with gradient-descent rivers and small honest lakes. The sim's `Region` list remains
spatial truth; cells are the renderable, terraformable world surface bridged onto it.
Three binding properties:

- **Baseline-inert by construction**: generation draws no Rng, the tick never reads a
  cell, and the `divine` gate hashes the full state across double runs.
- **Editable, journaled, replayable**: `SeedForestAt` / `CallSpringAt` mutate cells
  deterministically, append to an edit journal, and bump `Version` — the viewer's only
  rebuild signal. Terraforming later means more edit verbs, not repainting a picture.
- **Honest hits**: map clicks resolve through `RegionAtNorm` (the cell under the cursor
  names its region) — terrain itself is the hit target now, not abstract circles.

MapView renders the surface as one nearest-filtered pixel texture (2 texels per cell,
hash speckle, two-tone forest canopy, elevation shading, restrained banner-cloth
territory wash at 0.13) rebuilt **only** when `Surface.Version` bumps or territory
changes hands — never per frame. Retired: the island polygon, the adjacency web, the
flat region-circle tint (the old "abstract circles as dominant map language"), and —
since Sites V1 — the `PlaceSeeds` hint markers/hut clusters: every structure on the map
is now a real site from the sim's read-model.

Terrain palette (warm, per DESIGN.md guardrails): forest `3f5230`/`36482a` two-tone,
plains `5d5e38`, highland `6a665a`, wetland `495843`, river/lake `3a6a74`, coast sand
`6b6a48`, sea/shallows unchanged.

## Painterly Atlas V1 — SHIPPED (2026-06-13, binding)

The surface coloring moved out of MapView into **`src/LivingMyth.Sim/SurfacePainter.cs`** — a
pure read-model (zero Rng, never read by `Tick`, same baseline-inert contract as Sites/Replay).
It is the **single source of atlas pixels**: the Godot viewer builds its `Rgb8` texture from
`SurfacePainter.Paint`, and the console `paint` command writes the *same* bytes to a PNG — so a
screenshot is byte-faithful to the viewer (no mock-ups). This is the spine of the locked North
Star, **stylized semi-realistic fantasy pixel diorama, a living atlas**. Binding properties:

- **Painted sea, not flat fill**: a BFS depth field ramps the water from lit shore-shallows
  (`3b6b72`) out to cold deep ocean (`1a333d`), a low-frequency value swell gives it motion, and
  a pale surf line (`83a9a6`) traces every coast.
- **Soft coast**: the first one–two land cells off the water blend toward warm beach sand
  (`8f8154`) — a painterly shoreline, never the old hard darkened rim.
- **Relief, contours, ink borders**: NW-lit hillshade from the elevation gradient gives the land
  real form; faint contour ink (`23190d` @ ~0.11) traces elevation bands; the *same* ink at
  ~0.34 traces where one people's land meets another's — the atlas reads as an inked political
  map. Two-octave mottling (coarse value noise + fine per-texel grain) keeps any region from
  reading as one bucket-fill. 3 texels/cell (was 2) for finer grain and hairline ink.
- **Markers are miniatures**: every site marker gets a soft elliptical contact shadow
  (`DrawGroundShadow`) so it reads as a diorama piece resting on the ground, not a flat icon.

All of it is a deterministic function of cell coordinates + surface data — zero Rng, viewer-only,
`verify` unmoved at 823/559/910/632. Tunables live at the top of `SurfacePainter.Paint`
(`ContourStep`, relief strength, mottle/grain amplitudes, depth radius).

## Lens heraldry (Inspect mode, 2026-06-13, binding)

Every inspector (region / person / site / faction) is crowned by a **5px heraldic stripe in the
holder's cloth color** (`FactionTint`: highland `6b7a99`, shore `4f8f89`, wood `5d8a4e`,
wilderness = stone) — the lens wears whose land it is, so Inspect reads as a chronicle page, not a
data table. The stripe is the only chrome added; the parchment panel recipe is unchanged.

## God-Hand visual language (Divine Pressure V1 — SHIPPED 2026-06-12, binding)

Every act of the player's hand is explicit sim state (`World.DivinePressures`) with a
recorded chronicle event; the viewer may only show what the ledger holds. The marks,
deliberately unmixable with place scars (RegionId), home cairns (HomeRegionId), and
follow marks:

| State | Map voice | Card voice |
|---|---|---|
| Blessed soul | thin steady pale-gold ring (`f2e2b0`) — quieter than the breathing follow halo, paler than the leader ring | `✦ BLESSED — fate leans kindly toward this soul` |
| Cursed soul | ember dot (existing) | `✳ CURSED — a god's mark lies on this bloodline` |
| Protected people | thin gold thread under the faction tag | `❧ UNDER PROTECTION — until Yr {X}` |
| Doomed people | thin ember thread under the faction tag | `☄ UNDER A DOOM — until Yr {X}` |
| Omen-marked land | violet ✶ + slow-breathing violet ring at the region (apart from scars/cairns) | `✶ an omen hangs over this land` |
| Terrain act | the land itself changes (surface texture rebuild) | feed/ledger rows; the lens reads the new terrain |

Copy rules: an omen is **attention, not mechanics** — its only effect is a viewer
surfacing weight (+25) on tales truly anchored in the marked land; never claim more.
Person-target acts (bless/curse) carry **no place anchor**; region-target acts anchor
exactly where the hand touched; **no divine act ever carries a home anchor** (the
`divine` gate enforces all three). Connector copy (authored, evidence-backed only):
`the curse found another life — therefore,` · `the doom upon them bore down —
therefore,` · `but even the old blessing could not hold them —` · `but even under the
protection laid upon them —`.

**The Fate Ledger** (right reading sheet, shares the slot with How We Got Here): every
act with its target, year, state (holds / faded / wrought), a link to the recorded act,
and up to two consequences the chronicle honestly traced back to it — a sacred record,
not a debug table.

## Place Memory V1 — SHIPPED (2026-06-11, viewer-only)

The first persistent-memory slice: **real anchored events leave subtle marks on the
place where they happened.** Only events that truly carry `Event.RegionId` may mark a
region; unanchored events cannot scar anything — no exceptions, no inference.

As built, against what's honestly anchored today:

| Anchored event (real `RegionId`) | Mark (parchment-atlas language) |
|---|---|
| territory founding | standing stone (`⌑`) |
| territory seized in war | scorch + snapped banner pole (`✕`) |
| territory abandonment (extinct people) | cairn (`∴`) |
| custom born / faded / clash / diffusion | culture ribbon (`❧`, violet) |
| **battle** (Battle Sites V1, 2026-06-13) | **crossed swords (`⚔`, ember)** — full-length crossed blades at the front, brighter and pole-free so it reads apart from the war scorch |
| **famine onset** (Harvest Memory polish, 2026-06-13) | **ochre cracked-earth disc** — drawn on famine ONSET only; kept in its OWN per-region 1-slot scar store (NOT the 4-slot place-mark ring, so rare founding/war/battle marks survive recurring famines), at a dedicated reserved slot angle, drawn larger with a higher alpha floor than ring marks for low-zoom legibility |
| rumor | **no mark by design** — gossip is social, not a physical scar |

Constraints honored: marks age/fade deterministically by sim-year (no RNG, no wall
clock — full at 0 yrs, faded to a 0.30 floor by ~250 yrs, never gone until evicted),
capped at 4 per region (the oldest yields), drawn beneath place markers and labels
(layer 5a), fixed slot angles ringing the region centre. The Region Lens "Marks upon
the land" section lists the real event behind every mark as a catch-up link, and says
"unmarked" honestly when nothing recorded has scarred a place.

Battle scars at the front **SHIPPED** (2026-06-13, Battle Sites V1): the `battle` event
carries a real `RegionId` (the war's front) + `SiteId` (its stronghold), so it marks like
any anchored event — crossed swords, drawn from the same `MapView.MarkKind` channel.
Still deferred to the event-anchoring **sim contract** (PROJECT_STATE.md): prophecy omens,
plenty/famine land mood (the per-region economy half) — none of those events carry a
`RegionId` yet, so they may not mark. (Murder/death cairns shipped 2026-06-11 as
**memorial cairns** on the separate home-memory channel — see below.)

### Memorial cairns — SHIPPED (2026-06-11, viewer-only, Place Memory V2 first slice)

The first home-memory mark: **a cairn-worthy life is remembered at the home of its
line** (`Event.HomeRegionId`), never at an invented death place. The gate is person
truth, not scoring: murders always raise a cairn (violent grief is carried home);
deaths only of those who `EverLeader` (a plain death never marks, so cairns stay rare
enough to read); births never mark — a cairn is a memorial. As built:

- Separate channel end to end: `MapView._homeMarks` (own store, `AddHomeMark`), fed
  from `HomeRegionId` only — structurally impossible to mix with true place marks.
- Visually apart: three deliberately stacked stones with a small gold remembrance
  light at the top, drawn at the **rim** of the home lands (radius 0.78 vs 0.55,
  own slot angles) — warm and intentional where the abandon cairn (`∴`, scattered
  round stones) is cold ruin. Capped 3 per region (oldest yields), same deterministic
  sim-year fade as V1 marks, no RNG.
- Verbally apart: Region Lens lists cairns inside "Lives rooted here" (`∆ memorial
  cairn`, gold-brown), under the existing not-where-it-happened caption — never in
  "Marks upon the land". The death guard card adds "a memorial cairn is raised in
  {X}, the home of their line" **only when the mark truly stands**.

### Place Memory V2 readiness (anchoring audit, 2026-06-11)

The full Record()-call-site audit (PROJECT_STATE.md, "Event Anchoring Contract")
found **no new safe anchors** — the sim already stamps every region it truthfully
knows. So **nothing new is unlocked for Place Memory V2 yet**; every candidate mark
below waits on a deliberate, baseline-moving sim contract:

| Future mark | Blocked on |
|---|---|
| death / murder cairn | **SHIPPED 2026-06-11** as the memorial cairn (home-memory channel, above) — "remembered here", never "died here" |
| battle scar at the battlefield | battle-site contract (battles aren't events at all today) |
| famine / drought / plenty land mood | per-region economy (prosperity is per-faction; drought unmodeled) |
| peace ribbon | faction ids on peace events + a treaty-site convention |
| ritual shrine ribbon | ritual events (unmodeled) + seat/site contract |
| succession stone / banner | seat-of-power contract (no seat is modeled) |
| prophecy / omen glyph | a prophecy system (only the prophet-arises event exists) |

Forbidden until those contracts exist: marking any of the above from faction land,
nearest-region guesses, participants' homelands, or prose inference. The existing
custom/rumor `PrimaryRegion` anchors are a disclosed pre-doctrine seat-proxy
convention — kept for continuity, slated for replacement by the seat contract.

### Home-memory anchor language (Life Memory V1, 2026-06-11 — binding)

Life events (births, deaths, murders) carry `Event.HomeRegionId` — where the life is
**remembered** (the lineage's home root), a separate field from `Event.RegionId`
(where it happened, still null on life events). Viewer rules:

- Allowed copy: "of {X}", "rooted in {X}", "remembered in {X}", "the home of their
  line", "memorial raised in {X}".
- Forbidden copy: bare "in {X}", "born in {X}", "died in {X}", "murdered at {X}" —
  any phrasing that reads as the event's physical location.
- Null `HomeRegionId` → no place language at all; the memorial's honest
  "the chronicle records no place for this passing" line stays.
- Home memory and true place anchors never share a surface unlabeled: the memorial
  where-line distinguishes "in {X}" (true place) from "remembered in {X}" (home),
  and the Region Lens keeps "Lives rooted here" (with a not-where-it-happened
  caption) apart from "Tales anchored here".
- Place Memory map marks read `RegionId` only. Home anchors mark the land solely
  through the visually distinct **memorial cairn** channel (shipped 2026-06-11,
  above) — never through the place-mark store, never with place-event language.

## Causal story language (Myth Authorship V1 — SHIPPED 2026-06-11)

The chronicle explains what it can prove, in these exact voicings — single-sourced in
`godot/StoryCopy.cs`; this table is the review checklist. Connectors are **lead-in lines
between record rows** (small, faded, italic, indented), never merged into event text:

- `therefore —` (proven consequence, gap ≤ 2 yrs)
- `{N} years passed — therefore,` (proven consequence across a real gap)
- `the whispers fed it — therefore,` (war ← rumor)
- `but —` / `{N} years on — but` (authored complication/reversal only)
- `the grievance lay unresolved for {N} years, until —` (revenge, provably open the whole gap)
- `the chronicle does not record what first stirred {Name}.` (prophet — honest unknown)
- `the chronicle does not record what doctrine divided them.` (schism)
- `what drew them together, the chronicle does not say.` (forbidden bond)

Forbidden: any causal connective ("therefore", "because", "led to", "drove them to")
without StoryGrammar evidence; any interior life the sim does not model ("plotted",
"stewed", "dreamed of revenge"); voicing a connector against a row the current view has
hidden (quick mode) — the connector is suppressed, never re-aimed.

Reputation transitions read as memory, not debug output: `{name}'s name darkens: little
known → whispered against` — band words carry glossary hints (`[hint=…]`), and the
"unremarked" fallback is written out as `little known`.

## Player canon display language (Myth Authorship V1 — SHIPPED 2026-06-11)

Player-authored text is the player's hand laid over the world — never the chronicle's
voice, never sim truth:

- The only five labels: **Your telling** (person), **Chronicler's note** (event),
  **Memorial inscription** (dead soul), **Place legend** (region), **What the people
  say** (faction).
- Treatment: italic, attributed `— your hand`, visually apart from record rows. Busy
  surfaces show a ~90-char preview; the full text lives on the entity's own card.
- Empty notes render nothing — no placeholder rows, no empty sections. Canon never
  enters the saga feed. Canon wording never borrows the record's authority ("the
  chronicle records…" is reserved for Recorded Fact).
- The write affordance is a quiet `✎` link ("✎ write what stirred {name}", "✎ set a
  memorial inscription", "✎ set a place legend") — gold-family, never a loud button,
  shown only where an honest gap or an owned surface invites it.
- The editor is a modal parchment card captioned `YOUR HAND — KEPT APART FROM THE
  RECORD`; it pauses the world while open and restores the prior pace on close. 500
  characters; saving empty text removes the note.
- A note written in a past session attaches to deterministic ids: it stays dormant until
  this run's chronicle reaches its entity again, and a note whose identity snapshot no
  longer matches (sim build drift) is quarantined — kept in the file, never rendered
  against the wrong entity.

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
4. **Chronicle Replay Prototype** — ✅ SHIPPED (2026-06-12, Chronicle Replay V2): glowing
   causal path over the dimmed atlas, numbered beats on honestly anchored events only, a
   beat scrubber, turning-point pulses. The binding rules are in "Chronicle Replay + turning
   points" above. Future polish: split-scale zoom, timelapse auto-play (with the separately
   planned timeline-scrubbing milestone), feed-isolation during replay. Viewer-only.
5. **Terrain Geometry / Diorama Exploration** — viewer-side region polygons (Voronoi
   or authored bands) so the atlas reads as landforms; gateway to diorama rendering.
   Viewer-only, deterministic from seed.
6. **Site/Settlement Data Contract** — ◐ MOSTLY SHIPPED (Sites V1 2026-06-12 + Event.SiteId
   2026-06-12): 3–7 deterministic, terrain-honest sites per region (baseline-inert read-model),
   PLUS `Event.SiteId` — events now anchor to a single place where one authored convention
   honestly assigns it (foundings/abandonment→seat, war→stronghold, ways→sacred site; zero Rng,
   verify did not move). Rendered as markers/tags/Site Cards/Region-Lens place lists with real
   site memory ("known for", site-anchored tales). STILL FUTURE: battle sites, rumor/trade
   anchors, people-at-site, settlement populations, buildings/features — those need new sim
   events/RNG and a deliberate baseline-moving milestone; the remaining "forbidden" items
   graduate to "honest" only through that gate.

### Sites V1 surface (binding, as built 2026-06-12)

What a site may claim, and the map/card payoff for each:

| Claim | Source of truth | Surface |
|---|---|---|
| name, type, seat-ness | `SiteIndex` (deterministic at world seed) | marker silhouette + zoom-gated name tag (name 11 / type sub 9), Region Lens "Places of this land" rows, Site Card title |
| position | a real surface cell inside its own region | the marker stands on that cell; clicks hit it before the land |
| ground it stands on | `Surface.TerrainAt(cell)` | Site Card "stands on {terrain} ground" |
| holder | DERIVED live from the region's `ControllingFactionId` | seat banner on the map; "held, with all {region}, by {people}" on the card |
| tales | the events `Event.SiteId` anchors HERE (shipped 2026-06-12), plus the wider land's, clearly apart | card "Tales at this place" + a "known for" line from recorded counts; the land's other tales under "Tales of {region}" |

Forbidden on any site surface until modeled: population, named dwellers,
buildings/stores, daily life, loyalty/defense values. (Site-anchored events are now
honest — see "Event.SiteId" below — but only those the convention table truly places.)
The Site Card says the rest plainly under "Not yet in the record". Site name tags appear at
`SiteTagZoom` (2.4) and always for the inspected land's sites; overlap-skip applies.
Roads run seat-to-seat between same-faction neighbours; fainter local paths run from
each seat to its region's other sites. The inspected site gets a small `LensGold` ring
(beside, never replacing, the region lens ring).

## Anchor language (binding — four channels, never mixed; shipped 2026-06-12)

Every place-naming phrase reads its anchor honestly. Single-sourced in `StoryCopy.cs`
(`AnchorPhrase`, `StatusLabel`); this is the review checklist:

- **`at {site}, in {region}`** — ONLY for a true `Event.SiteId` (the convention table placed it).
- **`in {region}`** — `RegionId` only: a land, no single place.
- **`remembered in {region}`** — `HomeRegionId` only: where a life is remembered, NEVER where it
  happened. (The Life Memory rules above still govern its wording.)
- **(nothing / "the chronicle does not place this")** — no anchor at all.

Replay-beat Status words (one per `ReplayBeat.Status`): site-anchored = "a true place",
region-only = "a land, no single place", memory-only = "remembered at a home — not where it
happened", unanchored = "unplaced — the chronicle does not say where". Forbidden: "at {site}"
for anything but a real SiteId; pinning a memory-only or unanchored beat to the map; inferring
a site from a region.

## Chronicle Replay + turning points (binding, shipped 2026-06-12)

The replay overlay is a retelling on the dimmed atlas, never a new simulation. Rules:

- The dim is a translucent warm-dark wash over the whole map; the live feed/world recede.
- Numbered parchment marks appear ONLY on honestly anchored beats (a true SiteId cell, else the
  region heart). Memory-only and unanchored beats get NO mark — they live only in the side rail
  (the catch-up sheet) and the beat card. This is the load-bearing honesty: **the map never
  claims a place the record does not hold.**
- Edges between marks are real recorded cause links only. The proximate-cause spine draws bold
  (gold glow under a bright core); any other real branch draws faint. No alternate/speculative
  paths are ever drawn.
- The current beat's mark is larger and breathes; the beat card names the connector (the same
  authored StoryGrammar phrase), the honest anchor + status, and "open this tale's thread".
- Entering replay pauses time (Chronicle Mode) and restores the prior pace on close.

Turning points (the live map, not replay): a small ember-gold diamond + slow halo on each
recent pivot, aged by sim year. Marks appear ONLY for pivots with a true place anchor; placeless
pivots (most schisms, prophet calls) surface through the thread header instead, never a fake pin.
The thread header names the authored kind ("✦ TURNING POINT — {label}"), who it touches, their
peoples, and the honest place. Kinds and labels are single-sourced in `StoryCopy.cs`.

## Current viewer audit (2026-06-11, post-Living-Diorama-V1)

Per-subject visibility statuses now live in the Visual Storytelling Doctrine above
(✅/◐/✎/⛭/✖). Headline reading: followed souls are now fully visible in the diorama;
bloodlines visible (cyan); factions and regions are identity-visible but their *state
changes* are still text-first; events pulse but anonymously; persistent memory does
not exist on the map yet (Place Memory V1, gated on event anchoring). The notes below
predate the diorama pass but remain accurate.

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
