# Living Myth Sandbox

A no-generative-AI, Steam-first 2D god-sim. The C# port of a proven headless Python
prototype: a deterministic world that grows traceable emergent history, surfaces the
important events (importance score → Yours/Loud/Rising feed), and detects a growing set of
Myth Echoes (13 so far) after the fact. Design docs + Python reference: `~/Downloads/ClaudeCodeLivingMyth.zip`.

## The one architecture rule (non-negotiable)
`src/LivingMyth.Sim/` is a standalone C# class library with **ZERO Godot dependency**.
Godot only renders it. Authored content stays in `src/LivingMyth.Sim/data/*.json`,
separate from logic. Never let simulation logic leak into Godot nodes.

## Layout
- `src/LivingMyth.Sim/` — the sim (Rng, Models, Chronicle, World, Scoring, Echoes, Feed; WorldSurface
  — the editable terrain cell grid; the DivinePressure ledger in Models/World) plus pure
  read-models over it (StoryGrammar — proven causal connectors; Sites — the deterministic local
  place layer + `SiteAnchors` the Event.SiteId convention table; Replay — replay chains + turning-
  point classifier; PlayerCanon — the player-telling store, never read by World; PlayerWorld —
  the world-save input journal, never read by World). net8.0.
- `src/LivingMyth.Console/` — proof runner (run | divergence | surface | verify | homes | story |
  canon | divine | save | sites | replay | harvest).
- `godot/` — the viewer (.NET build), references the Sim: `Main.cs` (tick loop, pacing + dramatic
  auto-slow, live feed, inspectors, curse tool, causal catch-up + replay retelling, Follow/Yours
  channel, focus guard + memorial cards, chapter recaps, canon wiring, world-save journaling +
  resume fast-forward, the Site Card), `MapView.cs` (map render, clicks, pulses, place/home marks,
  Sites V1 markers/tags, replay overlay + turning-point marks), `RememberedPlaces.cs` (the atlas's
  memory panel — every truly-touched place, honest anchor language), `UiTheme.cs` (Ui.* styling,
  single-sourced accents), `RegionActivity.cs` (per-region event index — region/home/site channels),
  `PlaceSeeds.cs` (legacy hash helpers; its map hints retired by Sites V1), `StoryCopy.cs` (ALL
  connector/canon/anchor/replay/turning-point English + glossary), `CanonPanel.cs` (the canon writing
  desk), `PersonSigils.cs` (deterministic per-soul marks), `CastPanel.cs` (the dramatis-personae
  roster), `FateLedger.cs` (the god-hand's act-and-consequence sheet), `DioramaView.cs` (the
  read-only region-diorama bridge — Blender-rendered miniatures billboarded over an isometric
  terrain plane for the live selected region; freezes time while open). The diorama asset pipeline
  is `tools/art/render_diorama.py` (headless Blender → Cycles → transparent PNGs in
  `godot/assets/diorama/`); `godot/shaders/parchment_post.gdshader` is the warm-grade post.

## Milestones
- **M0–M5.1** — spatial island, regions, territory, extinction land-release.
- **M7** — culture pressure engine: per-faction value axes → named customs → clash/diffusion (The Vanished Way echo).
- **M8** — gossip/reputation layer: rumor events shift Person.Reputation, nudge tension, drive war (The Blackened Name + The War of Whispers echoes).
- **Visual: Living Atlas Foundation** (2026-06-10) — docs/VISUAL_STYLE.md style bible, parchment map place tags, framed docks, warmed atlas palette. See PROJECT_STATE.md for details.
- **Focus Time arc** (2026-06-10/11, viewer-only) — docs/TIME_AND_STORY_PACING.md design doc; focus guard (pause-on-drama off/★/all + recap/death cards); Guard V2 soul follow + memorial card; held-card return chip; chapter recaps (25 shown years or echo/memorial arc closure → queued recap card with top-3, Your Threads deltas, echoes).
- **Anchoring arc** (2026-06-11) — Person.HomeRegionId (founders ← founding seat, children inherit, null honest; `homes` gate) → Event.HomeRegionId on births/deaths/murders (memory anchor, never a location) → memorial cairns + followed regions (viewer-only). Baseline held 884/699/567/706 throughout.
- **Myth Authorship + Causal Chronicle V1** (2026-06-11) — truth model V1 (Recorded Fact /
  Causal Claim / Player Telling / Mechanical Truth, binding in PROJECT_STATE.md); StoryGrammar
  sim read-model (therefore/but/unresolved-until over `Event.Causes`, honest-unknown allow-list)
  + `story` gate; PlayerCanon store (`user://canon_seed{N}.json`, dormant/quarantine identity)
  + `canon` gate; viewer: causal catch-up, guard why-line, the writing desk (CanonPanel) with
  tellings/notes/inscriptions/legends/people-say, recap Still-Unresolved, reputation memory
  copy, glossary hints. Baseline held exactly 884/699/567/706 (pure read-models, zero sim changes).
- **The Cast** (2026-06-12, viewer-only) — the first playtest's cast-tracking fix: person sigils
  (PlaceSeeds pattern), the capped dramatis-personae panel, living introductions ("A NEW THREAD"
  cards) + mid-life tale-so-far on guard cards, your-story/world feed channels, why-you-care-first
  person cards, followed-land life-event flood tuning. Baseline held exactly 884/699/567/706.
- **Map-First Panel Economy V1** (2026-06-12, viewer-only) — the map made primary again: binding
  three-mode panel contract (Watch/Inspect/Chronicle) in docs/VISUAL_STYLE.md; guard toast (full
  card on click, memorial keeps the ceremony); cast+inspector in one left dock column; cast compact
  by default; How We Got Here → right side sheet; writing desk → right drawer; feed 300/world rows
  recede while focused; bottom dock 78; MapView.FocusPerson/FocusRegion (cast click = find them).
  Baseline held exactly 884/699/567/706.
- **Living Atlas Surface + God-Hand V1** (2026-06-12, sim + viewer + gate) — the editable world
  skin and the playable hand. `WorldSurface.cs` (Sim, Godot-free): 96×96 deterministic cell grid
  (terrain/elevation/vegetation/region bridge/rivers), pure coordinate hashes (zero Rng draws),
  journaled terraform edits + StateHash — baseline-inert by construction; MapView renders it as one
  nearest-filtered pixel texture rebuilt only on terraform/territory change; clicks resolve through
  surface cells. God-hand: DivinePressure ledger + Bless/Curse/Protect/Doom/Omen/SeedForest/
  CallSpring — multipliers on EXISTING rolls only (no new draws), divine cause-links with two new
  authored BUT rules, Fate Ledger right sheet, map payoff marks (binding table in VISUAL_STYLE.md).
  New gate `divine` (the `surface` name was taken by the surfacing demo). Baseline held exactly
  884/699/567/706 — deliberately unmoved.
- **Persistence + Sites V1** (2026-06-12, sim + viewer + two gates) — the player-shaped world
  survives relaunch and places became real data. `PlayerWorld.cs`: the world save as an INPUT
  JOURNAL (`user://world_seed{N}.json` — acts with year + identity snapshot, follows, last-seen,
  resume year; never a snapshot); the viewer fast-forwards to the resume year re-applying acts
  in place (deterministic sim ⇒ byte-identical world back), journals every act/follow, saves on
  pause/close/heartbeat; drifted targets quarantine. `Sites.cs`: 3–7 terrain-honest named sites
  per region — a baseline-inert READ-MODEL off the pristine surface (Event.SiteId deliberately
  DEFERRED; the `sites` gate asserts the field is ABSENT). `Replay.cs`: replay-ready beats.
  Viewer: site markers replace PlaceSeeds hints, site tags/cards/lens places, seat banners +
  roads. New gates `save` + `sites`. Baseline held exactly 884/699/567/706.
- **Chronicle Replay + Site-Anchored Memory V1** (2026-06-12, sim + viewer + gate) — history made
  visible on the atlas, and the first events that truly belong to a single place. `Event.SiteId`
  shipped via ONE authored convention table (`SiteAnchors.Expected`): founding/abandonment→seat,
  war→stronghold, ways→sacred site; everything else null; life events never anchor. Picks draw
  ZERO Rng (immutable sites, type-priority, lowest id) ⇒ baseline UNMOVED. `Replay.cs`: `ChainFor`
  (cause beats + bounded consequence rail, verbatim anchors, honest Status site/region/memory/
  unanchored) + `TurningPointKind` (bounded authored pivot classifier). Viewer: How We Got Here
  turning-point header + "What grew from this" rail + ⟲ Replay retelling on a dimmed atlas (numbered
  marks on anchored beats only, real cause edges, NO fake pins), turning-point map pulses,
  `RememberedPlaces.cs` panel, Site Card site memory ("known for" from counts). New gate `replay`;
  `sites` gate rewritten to PROVE the anchoring contract event-by-event. Baseline held exactly
  884/699/567/706.
- **Theater of War — Battle Sites V1** (2026-06-13, sim + read-models + viewer + gate) — the
  FIRST deliberate baseline move since M8. War casualties became `battle` events fought at real
  places: `World.RecordBattle` wraps the war's existing casualty rolls (lazily, first blood per
  war-year) anchored to the war's `FrontRegion` (a deterministic border region) + its stronghold
  (`SiteAnchors` extended: war/battle→stronghold). War declaration anchors to the front + carries
  leaders; peace carries leaders + the toll ("After N battles and M fallen … make peace"),
  closing the chapter-closing gap. Read-models: StoryGrammar `war-to-battle`/`battle-death`;
  Scoring `battle`=50; Echoes **The Field of Bones** (first place-keyed echo, ≥3 battles at one
  site). Viewer: crossed-swords scar, Remembered Places war filter, Site Card "fought here",
  catch-up connectors; war-pivots now pin the front. **Determinism keystone:** FrontRegion +
  RecordBattle draw ZERO Rng, so the stream stays byte-identical and the baseline moved by EXACTLY
  the battle count: 884/699/567/706 → **894/705/574/715** (+10/+6/+7/+9). Battles are NOT a
  turning-point kind (Replay untouched). `sites` gate proves battle anchoring non-vacuously (32
  battles / 22 sited). All eight gates green.
- **Harvest Economy V1** (2026-06-13, sim + read-models + gate + viewer copy — the SECOND
  deliberate baseline move): famine/plenty became a **region's harvest**. The random-walk moved
  from `Faction.Prosperity` to a per-`Region` `Harvest` (ground truth); `Prosperity` is now the
  derived controlled-region MEAN, with `InFamine`/`InBoom`/`FamineEvent` as worst/any rollups (so
  births/culture/trade/death read the same fields, source-shifted to the land). Only HELD regions
  emit `famine`/`boom`/`famine_end`, anchored to **RegionId, never SiteId** (`SiteAnchors` NOT
  extended; harvest + sites gates prove the non-leak). `famine_end` is a real region-anchored
  chapter-closing beat cause-linked to its onset. Famine deaths cause-link to the famine event but
  stay home-anchored (`HomeRegionId`/`RegionId==null`). Read-models: StoryGrammar `famine-breaks`;
  Scoring `famine_end`=35; Echoes **The Barren Years** (first famine echo keyed on RegionId,
  age-clustered ≥3). New gate `harvest`. **Baseline moved deliberately** (real new per-region RNG +
  faction-mean prosperity reshape births/trade/war): 894/705/574/715 → **823/559/910/632**
  (Δ −71/−146/+336/−83, BOTH directions). Balance held with NO tuning (5000-yr living
  168/157/306/150, no extinction, cap stays 300). All NINE gates green.
- **Harvest Memory Viewer Payoff V1** (2026-06-13, viewer-only — the Harvest Economy's deferred
  viewer half): the land's hunger made visible. `famine_end` Recovery event class (green `❀`, was
  falling back to "Tale ◆"); `MapView.MarkKind.FamineScar` — ochre cracked-earth scar on famine
  ONSET only (distinct from war-red `✕`, grey cairns, battle swords); Region Lens **"Harvest
  memory"** section (live `Region.InFamine`/`InBoom` condition line — qualitative, no numbers — +
  recent famine/recovery/plenty beats + the channel-split note; harvest beats excluded from "Tales
  anchored here"); Remembered Places **harvest** filter chip (`FilterOf` routes famine/famine_end/
  boom through the region-anchored loop, never site/home). Pure read-model: **verify held exactly
  823/559/910/632**, all 10 gates green, independent channel-mixing verifier PASS. Commit `85729fd`.
  F5 feel-tested live (Year 316): Recovery glyph + harvest filter + channel honesty confirmed;
  KNOWN ISSUE — famine scars are subtle at low zoom and, recurring often, crowd the 4-slot
  per-region mark ring (a dedicated famine-scar store/cap is the fix).
- **Beta-Readiness Pass V1** (2026-06-13, viewer + docs — beta-testability, not new systems): the
  Watcher's Guide (player-invoked Chronicle-Mode onboarding card — controls + honest map legend with
  every mark/ring drawn, glyphs mirrored from MapView; auto-opens once on a fresh world), the in-app
  `✶ New World` (confirmation-gated: discards the world save + reloads, keeps canon — the first
  fresh-world affordance, and the fix for an old save whose acts all quarantine after a baseline
  move), and a painted shoreline on the atlas surface (land cells touching sea darken — atlas
  signature). Empirically launched + screenshotted the real viewer (clean resume + fresh paths,
  Guide + shoreline confirmed); Drew's save backed up + restored byte-identical. Famine-scar polish
  was found ALREADY shipped (commit 55862fb) — docs were just stale, now reconciled. Viewer-only:
  verify held exactly 823/559/910/632, all 10 gates green, both builds clean. Docs locked the visual
  thesis to "stylized semi-realistic fantasy pixel diorama" + added the binding Kenney/license/AI
  adoption policy to the asset scout. Commits 97eac0a (docs) + c46f0c3 (viewer).
- **North Star Diorama arc** (2026-06-14, viewer + Blender pipeline + docs — visual prototype, NOT
  new sim) — built `DioramaView.cs`, an isometric region-diorama read-model (Blender miniatures over
  a terrain plane, parchment chrome, warm post). Prototype Pass V1 proved the direction; Bridge V1
  wired it to the LIVE selected region (overlay from the Region Lens / F3, never a scene swap) and
  removed the fake god-hand action bar (read-only — real verbs stay in the atlas where they journal);
  Hardening V1 fixed the true `IsSeat` seat label, added label collision-avoidance + title/edge
  clamping, and **froze time while open**. Honest judge rating: production atlas 3/10, diorama 5/10;
  the 5→7+ gap is dedicated art labor, not code. See docs/visual_pass/DIORAMA_PROTOTYPE.md (binding
  doctrine: the "Enter the Diorama" button is a TRANSITIONAL bridge — the final UX is seamless atlas
  zoom into a land, not a clicked mode; not built yet). Viewer-only: verify held 823/559/910/632.
- **North Star Art Pipeline V1 — Terrain + Prop Language** (2026-06-14, Blender + headless Krita +
  viewer + docs, NO sim) — the first reproducible art-pipeline slice, three stages: (1) Blender
  `render_diorama.py` — 2-noise painterly material on props + a top-down opaque ground-tile pass
  (`ground_coast/forest/highland/water`, water w/ foam) + `pulse_marker` + fuller `banner`;
  (2) **headless Krita** `tools/art/krita_paintover.py` via `kritarunner` (no GUI) — gaussian
  blur→unsharp + an edge-ink overlay (edge detection→invert→multiply, **alpha-inherited** so ink
  clips to the silhouette; `invert` flips alpha, so inheritance is load-bearing). kritarunner has
  its OWN resource dir (`%APPDATA%/kritarunner/pykrita`), needs the plugin **enabled** in
  `kritarunnerrc`, and calls the entry WITH an args list — all in `tools/art/krita_plugin/INSTALL.md`;
  (3) Godot `DioramaView.cs` (unchanged read-model) — textured iso ground for coast/forest/highland/
  water, shore foam, seat→places roads, ember pulse markers on recent site-anchored tales, all gated
  by `LM_DIORAMA_RAW` for before/after capture. Honest score 5→6.5 (flat teal water → textured+foam;
  props gain illustrated ink). **Recommendation: adopt as the production art route** — next gains are
  content (richer Krita chains, more silhouettes), not plumbing. Evidence: docs/visual_pass/
  ART_PIPELINE_V1.md + artpipeline_v1/. Viewer/asset-only: verify held 823/559/910/632, 9 gates green.
- **Next** — **terrain-typed harvest** (highland/coast/plains volatility, the deferred sim follow-up
  — moves the baseline); **deepen the art pipeline** (per-biome Krita chains — oilpaint/texture-bomb
  the grounds; more tree silhouette variety) or **diorama art fidelity** (hand-finished/licensed
  assets — the 6.5→8 gap) or
  seamless atlas→diorama zoom (retire the bridge button per its doctrine); more code-only visual
  treatment (territory boundary lines, elevation contours, marker outlines — sandbox/screenshot-verify
  each); still-unwatched Theater of War / Chronicle Replay / persistence / Cast / Harvest F5
  feel-tests; person↔site anchoring; timeline scrubbing; per-launch seed choice (today fixed at 7).

## Commands
```bash
dotnet build LivingMyth.slnx                                   # build everything
dotnet run --project src/LivingMyth.Console -- verify          # determinism gate (must pass)
dotnet run --project src/LivingMyth.Console -- homes           # home/anchor contract gate (must pass; --years N)
dotnet run --project src/LivingMyth.Console -- story           # causal-grammar gate (must pass; --years N)
dotnet run --project src/LivingMyth.Console -- canon           # player-canon contract gate (must pass)
dotnet run --project src/LivingMyth.Console -- divine          # god-hand + surface gate (must pass; --years N)
dotnet run --project src/LivingMyth.Console -- save            # world-save journal gate (must pass; --years N)
dotnet run --project src/LivingMyth.Console -- sites           # sites + Event.SiteId anchoring gate (must pass; --years N)
dotnet run --project src/LivingMyth.Console -- replay          # chronicle-replay + turning-point gate (must pass; --years N)
dotnet run --project src/LivingMyth.Console -- harvest         # per-region harvest economy gate (must pass; --years N)
dotnet run --project src/LivingMyth.Console -- run --seed 42
dotnet run --project src/LivingMyth.Console -- divergence --seed 18
dotnet run --project src/LivingMyth.Console -- surface --seed 1
dotnet run --project src/LivingMyth.Console -- run --seed 7 --years 3000 --cap 300  # --cap overrides carrying_capacity for balance tuning
dotnet build godot/LivingMyth.Godot.csproj                     # build Godot project headlessly
```

**Viewer:** open `godot/` with Godot mono editor and press **F5** to launch the viewer.

## Gotchas
- **The yearly tick is a fixed engine order** (`World.Tick`): Economy → ProcessWars → Deaths →
  Crime → ForbiddenRomance → Marriages → Births → DoReligion → Culture → Gossip → MaybeDeclareWars
  → DecayTension → ReleaseExtinctLands. New pressure engines slot into this order; where they sit
  changes RNG consumption (and the verify baseline), so placement is a deliberate choice.
- **Determinism is sacred.** All randomness routes through `Rng`. C# dicts/sets are not
  order-stable like Python's — every iteration that feeds RNG or results MUST be explicitly
  ordered (people/religions by id, factions in config order, member sets sorted). `verify`
  guards this. Intra-C# only: NOT bit-compatible with the Python seeds.
- **Hot paths must stay O(living), not O(history).** People and the chronicle grow forever;
  per-tick/per-frame work must NOT scan them. Iterate the living set (`Living()`/faction
  members), use `Chronicle.Get(id)` (id == list index) over rebuilding id maps, and stream
  the feed with incremental consequence counts. Reintroducing an all-history scan is the
  classic regression here.
- **Importance gates die on quiet event types.** Scoring TypeWeight: death=5, birth=3 — an
  importance bar for life events is dead code. Gate on person truth instead (memorial cairns:
  murder always, death only if EverLeader — final by death, so pacing-independent).
- **The story grammar is a read-model, not a narrator.** `StoryGrammar` (Sim, beside Echoes)
  emits structured connector kinds backed only by `Event.Causes` + person/faction state; the
  viewer voices them via `godot/StoryCopy.cs` — the ONLY home for connector/canon English
  (VISUAL_STYLE.md holds the binding copy tables). "But" is authored-only (`ButRules`),
  honest-unknown is an allow-list (prophet/schism/forbidden bond), every other rootless event
  stays silent. Extend the rule table and the `story` gate together — the gate proves evidence
  ids, real gap arithmetic, the war-despite-peace reversal, and double-run determinism.
- **Player canon is the third ledger, never sim truth.** `PlayerCanonStore` (Sim, path-injected
  → `user://canon_seed{N}.json`) is never read by `World` — the `canon` gate proves it by
  reflection + a behavioral double-run. Notes attach to deterministic ids and validate lazily
  per (note, world): dormant until this run re-reaches the entity, quarantined on snapshot
  drift (sim-code changes shift event ids; quarantined notes are kept in the file, never
  rendered, no recovery UI yet). Copy claims stay exactly as wide as the state they read:
  "unavenged" (`Murdered && !Avenged`), never "unpunished" — justice events are recorded apart.
- **Two anchor channels, never mixed.** `Event.RegionId` = where it happened; `Event.HomeRegionId`
  = where it's remembered (lineage home root). Home-anchor copy is binding (VISUAL_STYLE.md):
  "remembered in / of / rooted in {X}" — never "died/born/murdered in {X}", never bare "in {X}".
  Keep the stores/sections separate end to end (RegionActivity, MapView marks, Region Lens).
- **Population balance is the `carrying_capacity` param** (config.json, currently 300):
  logistic births → plateau. Too low (~120) drifts to extinction. With the Harvest Economy on,
  verified stable ~150–310 living over 5000 yrs at 300 across seeds 1/18/42/7 (168/157/306/150).
  `curse_death_multiplier` (2.5) and `famine_death_multiplier` (1.4) + `famine_threshold` (0.45)
  tune how deadly curses and collapse are. The economy is a net population suppressor (famine adds
  deaths, booms only help births), so raising multipliers drifts low seeds toward extinction.
  Harvest tuning note: deriving a people's death-pressure from its WORST controlled region (not a
  single faction walk) raises famine frequency; an early derive-after-trade ordering tipped seed 42
  to extinction by shifting the trade-guard RNG stream — derive BEFORE trade (guard reads fresh
  mean) + re-derive the two traders after each trade (zero Rng) is the balance-safe order.
- **The verify baseline moves whenever sim RNG consumption changes — OR a new event type is
  recorded.** Current `verify` counts (120 yr, cap 300): **823/559/910/632** (seeds 1/18/42/7,
  Harvest Economy V1 baseline — the harvest walk moved per-region, adding REAL new Rng per region
  and reshaping faction-mean prosperity, so the stream moved in BOTH directions per seed:
  894/705/574/715 → 823/559/910/632, Δ −71/−146/+336/−83). Prior baselines: Battle Sites
  894/705/574/715, M8 gossip 884/699/567/706, M7 culture 814/594/525/652. The determinism gate is
  self-consistency (same seed → byte-identical run), so it stays green regardless of feature work;
  these numbers are just the recorded expectation. NOTE: adding a recorded event with NO new Rng
  (the Battle Sites trick) moves the count but not the stream — but Harvest Economy is the opposite:
  a genuine new-Rng contract, so re-run the 5000-yr balance probe (no extinction) when touching it.
- **Battle Sites are zero-Rng by construction.** `World.RecordBattle` records a `battle` event
  but draws NO Rng; the war's casualties are the same `Rng.RandInt(0,2)`/`Pick` rolls as before,
  just cause-linked to the battle. `FrontRegion` (the border region a war is fought over) is a
  pure read over control + the fixed adjacency graph — also zero Rng, and computed BEFORE the
  YearsLeft `RandInt(1,2)` draw so that draw is unmoved. A battle is PLACED (RegionId=front,
  SiteId=front's stronghold via `SiteAnchors` war/battle case); its dead stay REMEMBERED at home
  (the death events keep HomeRegionId, never the battle's ground) — the four anchor channels still
  never mix. Battles are deliberately NOT a turning-point kind (only war/peace/land pivots are; a
  far-reaching battle surfaces via the ≥4-consequence fallback), so any new pivot type must extend
  BOTH `Replay.TurningPointKind` AND the `replay` gate's `tpKinds`+premise switch together.
- **Harvest is the economy's ground truth; Prosperity is DERIVED.** `Economy()` walks each
  `Region.Harvest` (list order == id order), then `DeriveProsperity(f)` sets `f.Prosperity` = the
  controlled-region harvest MEAN and `f.InFamine`/`InBoom`/`FamineEvent` as the worst/any rollup.
  These four `Faction` fields are now CACHES recomputed every tick, not independent state — never
  write them directly (births/culture/death read them; the `harvest` gate asserts they equal the
  rollup exactly). Only a HELD region (`ControllingFactionId != null`) emits `famine`/`boom`/
  `famine_end`; wilderness harvest walks silently. Economy events anchor to **RegionId, never
  SiteId** (`SiteAnchors` is NOT extended for them — a famine spans a land, not a site; the harvest
  AND sites gates both prove Expected==null). A famine death cause-links to the region's famine
  event but the death stays home-anchored (`HomeRegionId`/`RegionId==null`) — four channels, never
  mixed. Trade lifts HARVEST (the ground truth) and re-derives the two traders (zero Rng); derive
  runs BEFORE trade so the guard reads this tick's fresh mean (the ordering that keeps balance — see
  the baseline gotcha). `famine_end` always carries the onset id as a cause, so it's never rootless.
- **M8 gossip tuning note.** `Gossip()` watches `[_lastGossipEventCount, count)` each year (no all-
  history scan), gates on importance (≥`gossip_min_importance` 42, which is why low-key events like
  plain scandals never reach the mill), and never gossips a `rumor` (no recursion). `The Blackened
  Name` echo fires at **≥2** negative rumors on one person, not 3: the sim spreads crime across many
  hands (each murder a different killer; persecution picks a random enforcer), so even at 5000 yrs no
  single name draws a third rumor — max-per-person is 2. Raising it back to 3 needs a sim that
  concentrates infamy on individuals, which is out of scope for the gossip layer.
- **Identity-preservation mechanism (not the numbers) is the invariant:** `carrying_capacity` = 0
  cleanly disables the logistic birth damping (economy still runs). Recorded cap=0 baseline counts
  are now 1145/1097/535/893 (seeds 1/18/42/7 @ 120 yrs); re-baseline these when sim behavior changes.
- **The viewer is presentation-only over the sim.** Pacing (`BaseInterval`, `SpeedLadder`, the
  dramatic auto-slow) only changes the *wall-clock rate* at which existing ticks are shown — `Tick()`
  must still be called the same number of times in the same order. So viewer-only work can never move
  the `verify` counts; if it does, sim code was touched by accident. `verify` is the guard.
- **The diorama overlay freezes time, never forks the world.** `DioramaView` is opened over the
  live `World` (shared object, never a copy); its chrome is a SNAPSHOT of the opened year, so
  `Main.OpenDiorama` sets `_running = false` (and `CloseDiorama` restores it) — same pause pattern
  as Chronicle Mode's replay. It calls no sim verbs, so it's verify-inert by construction. Screenshot
  capture is env-driven and viewer-only: `LM_SHOTS=<dir>` (Main's self-capture sequence),
  `LM_DIORAMA_SHOT=<dir>` + `LM_DIORAMA_TERRAIN`/`LM_DIORAMA_NAME`/`LM_DIORAMA_NOAVOID` (DioramaView
  standalone self-shot — terrain pick, output name, collision-avoidance toggle for before/after).
- **Divine pressure is multiplier-only.** God-hand mechanics may only modulate EXISTING Rng
  rolls (bless eases the death roll; protect/doom scale the famine multiplier and bias the
  prosperity walk inside self-expiring windows) — never add a draw to the tick. That is why
  verify holds 884/699/567/706 with the whole system in place; a pressure that draws breaks
  it. Omen is attention-only by design (a viewer surfacing weight, no mechanics). The
  `divine` gate proves determinism, cause-link honesty, and channel rules.
- **WorldSurface is baseline-inert by construction.** Pure coordinate hashes (not even an Rng
  stream), never read by the tick; terraform edits are journaled and bump `Version` — the
  viewer's ONLY texture-rebuild signal (never rebuild per frame). The `divine` gate hashes
  surface state across double runs.
- **Sites derive from the PRISTINE surface, structurally.** `World.BuildSurface()` constructs
  the `SiteIndex` in the same breath as `WorldSurface`, so no terraform edit can exist before
  the index does — the `sites` gate proves it by editing FIRST in one of its double runs. Site
  type honesty is cell-checked (`SiteIndex.FitsCell` is the shared rule both generation and
  the gate use).
- **Event.SiteId comes from ONE table, never ad hoc.** `SiteAnchors.Expected` (Sites.cs) is the
  SINGLE authority for which events anchor to a place (founding/abandonment→seat, war→stronghold
  hill-fort→watch-post→ford, ways-born/fade→sacred shrine→grove→barrow→cairn; everything else
  null; life events never anchor). World calls it at record time via `AnchorSite`; the `sites`
  gate recomputes it per event and asserts equality, so the rule cannot drift in World alone.
  It uses immutable sites + type-priority + lowest-id — ZERO Rng — which is the only reason
  adding the field held the baseline at 884/699/567/706. New anchor conventions extend the table
  AND the gate together. SiteId is never set without RegionId, and is always inside that region.
  The four anchor channels never mix: SiteId (the place it belongs to) / RegionId (where it
  happened) / HomeRegionId (where remembered — never a location) / null (unplaced).
- **Replay is a read-model that NEVER invents a place.** `Replay.ChainFor` copies each beat's
  anchors verbatim and tags an honest Status; the viewer's replay overlay and turning-point marks
  draw a map pin ONLY for a beat/pivot with a true SiteId or RegionId — memory-only and unanchored
  beats live in the side rail, never on the map. `TurningPointKind` is a bounded authored
  classifier (no "every event is a pivot"). The `replay` gate proves determinism, verbatim
  anchors, honest statuses, bounded real consequences, the classifier, and save-safety.
- **The world save is an input journal, never sim truth.** `PlayerWorldStore` persists the
  player's HAND (acts with years + identity snapshots), not world state: replaying it against
  the same seed reproduces the world because acts are the only player input the sim feels.
  ApplyDue is the only door from the store into a World (the `save` gate proves an unapplied
  journal leaves a clean run byte-identical); the viewer must journal through the same
  DivinePressure the act created (JournalAct), never hand-build entries — and resume replay
  must call the SAME World verbs the live session did, or the chronicles diverge.
- **No new island assumptions** (World Forge plan, GAME_DESIGN.md): the world shape lives
  behind one seam in WorldSurface; new features must not hardcode disk topology or "the
  island" copy. Maps become data (`maps/*.json`) in a future milestone; famous-IP maps
  (Middle-Earth etc.) are off the table legally — Earth and original/public-domain only.
- **Solution file is `LivingMyth.slnx`** (new SDK-10 XML format), not `.sln`.
- **Data loads at runtime from next to the binary.** `DataLoader` reads
  `AppContext.BaseDirectory/data/{config,names}.json` (copied to output, reliable under both
  the console host and Godot's dynamic load). Editing a `data/*.json` only takes effect after a
  rebuild re-copies it; a new data file must be set to copy-to-output.
- **Runtime rollforward:** projects target net8.0 (Godot 4.6 compat) but only the net10
  runtime is installed, so the console sets `<RollForward>Major</RollForward>`.
- **Godot needs the .NET/mono build** (`Godot_v4.6.3-stable_mono_win64`), NOT the standard
  build — C# won't load otherwise.
- **Git:** this folder is its own repo (nested `.git`), remote
  `DrewYomantas/living-myth` (private). ⚠️ Note `C:\Users\beyon` is *also* an accidental
  git repo — always confirm `git rev-parse --show-toplevel` is the LIVING MYTH folder
  before any `git add`/commit, or you'll stage the whole home dir. Also: `.claude/CLAUDE.md`
  churns automatically (token-insights updates) — never stage it with feature commits;
  commit deliberate edits to it separately.

<!-- TOKENOMICS:START -->
## Token Optimization Insights

_Last updated: 2026-06-14_

### Context Management
- Your context snowballs at **turn 18** on average (36% of sessions). Use `/compact` proactively after turn 16-18 on long sessions to prevent unbounded growth.
- Some sessions use significantly more tokens than others. Consider shorter, more focused sessions with clear goals.
- You could benefit from subagents for parallel tasks. Consider splitting multi-file operations into parallel agent tasks.
- You read files you don't end up using. Use `Grep` first to locate relevant files before reading them — reduces unnecessary context by ~0%.
- You receive verbose command output. Prefer `Grep`/`Read` tools over bash commands when searching files to reduce output tokens.

### Model Usage
- You use Opus/Claude for **8%** of simple tasks. Prefer **Sonnet** for editing, small fixes, and exploration tasks to reduce token usage by ~5x on those sessions.
- MCP server(s) **unity-mcp, ide, computer-use, robinhood-trading, 465a7fa2-43f0-4b8d-88cf-f8c5c5acb227, claude_ai_Notion, 5575e70a-2d9c-4611-bffd-614e75e6c3dd, unity, Apify, PDF_Tools_-_View, pdf-viewer** are loaded but never used. Consider removing them to reduce per-session overhead.

### Prompt Quality
- **5%** of your prompts are under 10 words. Include specific file paths, function names, and expected outcomes to reduce clarification rounds.
<!-- TOKENOMICS:END -->
