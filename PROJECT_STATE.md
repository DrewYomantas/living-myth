# Living Myth Sandbox — Project State

A no-generative-AI 2D god-sim. C# port of a proven Python prototype, rendered in Godot 4.6 (.NET).
Architecture rule: `src/LivingMyth.Sim/` is a standalone class library with ZERO Godot dependency;
`godot/` only renders it. See `.claude/CLAUDE.md` for commands, gotchas, and invariants.

## Milestones
- [x] M0 — sim port + console proof (run/divergence/surface/verify); determinism gate green.
- [x] M1 — viewer: map, time controls (play/pause, 1–8×), live rising feed, click-to-inspect.
- [x] M2 — god hand (curse tool) + catch-up (clickable feed → causal thread, Quick/Full depth).
- [x] Longevity — logistic carrying_capacity (300) + O(living) hot paths; stable ~450 living over 5000 yrs.
- [x] M3 — marking + the Yours channel: Follow a bloodline/people, YOURS rows surface in the feed.
- [x] M4 — economy pressure engine: per-faction prosperity → famine/boom/trade events, famine death
      pressure (mirrors curse), prosperity-linked births; "The Long Famine" + "The Golden Age" echoes.
- [x] M7 — culture pressure engine: per-faction value axes (valor/piety/cunning/harmony) → named
      customs (adopt/fade hysteresis + self-reinforcement) → clash (tension) / diffusion (eases
      tension); "The Vanished Way" echo. verify 814/594/525/652.
- [x] M8 — gossip/reputation layer: `Gossip()` reads a bounded per-year chronicle cursor and rolls
      rumor events off notable deeds — shifting `Person.Reputation` (-5..5) and nudging cross-faction
      tension (rumor ids enter grievance memory). Couples to crime discovery, prophet credibility, and
      war. "The Blackened Name" + "The War of Whispers" echoes. verify 884/699/567/706.
- [x] Map readability pass (viewer-only): deterministic place-seed markers (PlaceSeeds.cs, FNV hash
      of seed+region id — never sim Rng), same-faction roads, label tags + de-overlap, territory
      tint reduction, fixed draw-order. verify unchanged 884/699/567/706.
- [x] Region Lens foundation (viewer-only): clicking a region opens a Region Lens inspector (land /
      neighbours / tales anchored here / honest not-modeled notes) instead of silently redirecting
      to the faction panel; RegionActivity read-model indexes Event.RegionId incrementally
      (O(new events), no history scans); inspector cross-links (e:/r:/f: via RichTextLabel meta)
      interconnect person ↔ faction ↔ region ↔ catch-up; gold lens ring on the selected region.
      verify unchanged 884/699/567/706.
- [x] Living atlas visual style foundation (viewer-only): `docs/VISUAL_STYLE.md` style bible locks
      the Batch 1 north-star references (honest/aspirational/forbidden reading + staged roadmap);
      foundation slice — parchment map place tags (zoom-gated, always-on for the selected region),
      framed bottom dock groups, warmed atlas palette + shallows rim, medallion feed chips, shared
      gold/ember accents single-sourced in UiTheme. verify unchanged 884/699/567/706.
- [x] Time & story pacing model (docs-first): `docs/TIME_AND_STORY_PACING.md` — the four-clocks
      hybrid time model (World / Drama / Focus / Replay), event weight bands mapped onto the
      existing thresholds, focus-guard + chapter-recap designs (finally defining "era recap"),
      honest follow-subject table (prophecies forbidden until modeled), timeline-scrub
      feasibility notes, staged roadmap + later sim contracts. Viewer slice: pace-tier tooltips
      on the speed ladder + doc-anchor TODO at the Drama Time constants in `Main.cs`.
      verify unchanged 884/699/567/706.
- [x] Focus guard slice (viewer-only, roadmap 2 of docs/TIME_AND_STORY_PACING.md): pause-on-drama
      toggle (off / ★ followed / all) in the Time dock; gold guard card (Resume / How We Got Here)
      on Major+ YOURS events; followed-death "Their Tale Ends" card (fires even below the
      chattiness threshold — a follow is an explicit ask); per-soul "you last saw…" memory on the
      card + person inspector; "⛨ guard watches…" signal on the year card. All wall-clock
      presentation — pausing only gates whether _Process keeps calling Tick().
      verify unchanged 884/699/567/706.
- [x] Focus Guard V2 — followed soul + memorial moment (viewer-only): "Follow this soul" on the
      person inspector — a per-soul set, never expanded into kin, distinct from the bloodline verb;
      a followed soul's death upgrades the guard card to a memorial — dimmed atlas backdrop, larger
      ceremonial gold frame, event-class medallion, centered name/faction/born–died/age, reputation
      + children lines only when real, last six deeds clickable into catch-up, honest "no place
      recorded" when the death carries no RegionId (the map pulses only a real region); card
      language distinguishes soul-follow from bloodline-follow; year-card guard signal counts souls
      separately; warm-gold soul rings on the map (bloodline stays cyan).
      verify unchanged 884/699/567/706.
- [x] Chapter recaps (viewer-only, roadmap 3 in docs/TIME_AND_STORY_PACING.md): chapters of 25
      SHOWN years (or an arc closure — echo carding / followed-soul memorial, whichever first);
      closing queues a recap behind a "❖ Years X–Y — a chapter closed" chip by the year card,
      auto-opened on the next transition into pause, never over a guard card, never auto-pausing
      the stream; card = span + reason, top-3 by matured Importance (BuildReverse one-shot on
      open), Your Threads deltas (births into follows, losses, reputation band shifts, regions
      gained/lost — measured from chapter start or follow, whichever later), echoes carded; every
      line links into catch-up; only the latest unread chapter is kept; the return chip also
      covers the recap. verify unchanged 884/699/567/706.
- [x] Living Diorama V1 — watched souls made visible (viewer-only; Main.cs, MapView.cs): a
      followed soul reads as a marked life in the diorama before death asks the player to mourn.
      Map: "divine bookmark" — gold halo with a minimum findable screen size at fit zoom,
      soft breathing pulse (alive cue), flare when a newly shown saga event names the soul,
      gold-bordered ★-name tag at any zoom (overlap-skip); placement is still the existing
      deterministic scatter, no new precision implied. Clicking the marker opens a living soul
      glimpse — a small non-modal parchment card (never pauses, memorial always outranks it):
      name/faction/leader-or-once-leader, alive·age·born, reputation band, children count,
      "you last saw…" (real shown-event memory), last 3 recorded deeds as catch-up links;
      thread / the record / unfollow buttons. Saga rows whose participants truly include a
      watched soul carry a 4px gold side rule + tooltip note. Alive cues: guard-line tooltip
      names up to 2 watched souls; inspector says "★ you are watching this soul".
      Docs: Visual Storytelling Doctrine ("the sim should be seen before it is read") + per-
      subject visibility audit (✅/◐/✎/⛭/✖) + Place Memory V1 spec (documented, not built) in
      docs/VISUAL_STYLE.md. verify unchanged 884/699/567/706.
- [x] Place Memory V1 — anchored events scar the land (viewer-only; Main.cs, MapView.cs): real
      events that truly carry Event.RegionId leave persistent marks near the region centre —
      standing stone (territory founding), scorch + snapped pole (territory seized in war),
      cairn (abandonment after a people's extinction), violet ribbon (custom born/faded/clash/
      diffusion). Rumors anchor but don't mark by design (gossip is social, not a scar).
      Marks classified O(new events) in the existing stream loop, capped 4/region (oldest
      yields), fixed slot angles, alpha aged by sim-year (full → 0.30 floor over ~250 yrs, no
      RNG/wall clock), drawn beneath place markers + labels. Region Lens gained "Marks upon
      the land": every mark lists its real event as a catch-up link; "unmarked" stated honestly.
      VISUAL_STYLE.md Place Memory section flipped to SHIPPED with the as-built table; deferred
      rows (death cairns, battle sites, famine land mood, omens) wait on the anchoring contract.
      verify unchanged 884/699/567/706.
- [x] Event Anchoring Contract V1 — audit-only (docs): all 31 Record() call sites walked; zero
      new safe anchors exist (the sim already stamps every region it truthfully knows). Full
      audit table + per-contract unlock map below ("Event Anchoring Contract — audit"). The
      anchoring work is now scoped into concrete sim contracts, ranked by unlock value:
      Person.HomeRegionId (deaths/murders/births → memorial places, cairns), per-region economy
      (famine/plenty land mood), seat-of-power (successions, true custom seats), battle sites.
- [x] Person.HomeRegionId Contract V1 (2026-06-11, sim + console gate + one inspector line):
      every soul now knows where its line is rooted, with zero fabricated geography. Rules:
      founders receive their people's **founding seat** — the exact region the founding-territory
      event already anchors (`owned[0]`, backfilled in that same GenerateMap loop); newborns
      inherit `father.HomeRegionId ?? mother.HomeRegionId` (father first — the child's faction
      follows the father); home is **immutable heritage** (conquest never rewrites it); null is
      honest (landless people at founding, and descendants of all-null parents). No RNG draws, no
      new events — verify held **exactly 884/699/567/706** (baseline-safe, as predicted). New
      console gate `homes [--years N]` proves it: founder-seat invariant (seats recovered from the
      chronicle's own founding anchors, not sim internals), inheritance invariant, valid-region +
      honest-null checks, and double-run determinism of the whole home map — green on seeds
      1/18/42/7 at 120 and 1000 yrs (100% coverage; all founding peoples are landed at these
      seeds). Viewer: one "home:" line in the person inspector ("—" when null) — the only visual
      touch; death/murder/memorial anchoring deliberately NOT done yet (next slice).
- [x] Life Memory Home Anchors V1 (2026-06-11, sim metadata + viewer memory language): births,
      deaths, and murders now carry `Event.HomeRegionId` — a **memory anchor** (where the life is
      remembered: the lineage's home root), kept strictly apart from `Event.RegionId` (where it
      happened, still null on life events). Birth ← child's home; death ← deceased's; murder ←
      **victim's** home (the grief belongs to the victim's line). Null home → null anchor, no
      fallback. Event texts untouched, no RNG — verify held **exactly 884/699/567/706**. `homes`
      gate extended: life anchors match the soul's home, RegionId stays null on life events, no
      text names its anchor region, anchors deterministic across double runs — green at 120 yrs
      (life anchors 542/419/335/434, seeds 1/18/42/7) and 1000 yrs. Viewer: memorial where-line
      gains "remembered in {home}, the home of their line" (births: "of a line rooted in {X}")
      below true "in {X}", above the honest no-place line; memorial pulse falls back to the home;
      Region Lens "Lives rooted here" section (separate from "Tales anchored here", with its own
      not-where-it-happened caption) off a second incremental RegionActivity channel. Full
      anchor-semantics table below ("Life Memory — anchor contract").
- [x] Memorial cairns (2026-06-11, viewer-only, Place Memory V2 first slice): cairn-worthy lives
      now mark the map at the home of their line. Gate is person truth, not scoring: murders
      always (violent grief is carried home); deaths only of those who EverLeader (plain deaths
      never mark, so cairns stay rare enough to read); births never (a cairn is a memorial).
      Separate channel end to end — MapView._homeMarks fed from Event.HomeRegionId only,
      structurally unmixable with the V1 place-mark store; capped 3/region (oldest yields), own
      rim slot angles (0.78 radius vs 0.55), same deterministic sim-year fade, no RNG. Visual:
      three deliberately stacked stones + a small gold remembrance light — warm and intentional
      where the abandon cairn is cold ruin. Region Lens lists cairns inside "Lives rooted here"
      ("∆ memorial cairn", with catch-up link); death guard cards add "a memorial cairn is
      raised in {X}, the home of their line" only when the mark truly stands. verify unchanged
      884/699/567/706 (viewer-only).
- [x] Followed regions (2026-06-11, viewer-only, TIME_AND_STORY_PACING roadmap 4): a land can
      now be followed like a soul or a people. "Follow this land" on the Region Lens
      (_followedRegions, session-only like all follows); events become YOURS through the two
      honest channels only — tales truly anchored here (Event.RegionId) and lives remembered
      here (Event.HomeRegionId) — via RegionYours() in IsYours, so feed gold rows, the YoursBoost,
      and guard arming all inherit it. Guard card gains the lead "fate touches a land you watch"
      (region-yours events without a marked participant); time-bar guard signal counts lands;
      chapter recaps add Your-Threads land deltas with the channels counted apart ("N tales
      anchored here · M lives remembered here"); map draws a quiet persistent gold ring (fainter
      + tighter than the lens ring). Lens states honestly that much of history carries no place
      anchor yet. verify unchanged 884/699/567/706 (viewer-only).
- [x] Myth Authorship + Causal Chronicle V1 (2026-06-11): the chronicle now explains what it
      can prove, and the player may write what it cannot. Truth model V1 (four ledgers —
      Recorded Fact / Causal Claim / Player Telling / Mechanical Truth) documented and binding.
      `StoryGrammar` (sim read-model beside Echoes/Feed): proven connectors (therefore / but /
      unresolved-until) over `Event.Causes` with an authored rule table (~24 rules; the
      generic fallback never fired on the verify seeds — full authored coverage), root
      classification with an honest-unknown allow-list (prophet / schism / forbidden bond),
      `OpenGrievances`/`OpenWars`. `PlayerCanon` (sim-blind store, versioned JSON at
      `user://canon_seed{N}.json`): one note per (entity, type) — tellings, chronicler's
      notes, memorial inscriptions, place legends, what-the-people-say — dormant until the
      re-run reaches the entity again, quarantined on identity drift, atomic saves. Viewer:
      catch-up voices connector lead-ins + honest unknowns + ✎ write affordances; guard cards
      carry a proven why-line; memorials take inscriptions and say "lies unavenged" (exactly
      `Murdered && !Avenged`, never wider); recaps gain "Still unresolved" + reputation
      memory copy ("{name}'s name darkens: little known → whispered against") + glossary
      hints. New gates `story` + `canon` green; verify held **exactly 884/699/567/706**
      throughout (zero sim-behavior changes — the grammar is a pure read-model).
- [x] The Cast (2026-06-12, viewer-only): the answer to the first playtest's cast-tracking
      overwhelm — identity and attention, no new content. Person sigils (`PersonSigils.cs`,
      PlaceSeeds pattern: deterministic seeded mark + inked tint per soul, same mark on the
      cast panel, feed rows, care lines, memorial names — the V1 stand-in for portraits).
      Cast panel (`CastPanel.cs`): a capped persistent dramatis-personae roster — followed
      souls, eldest of a followed line, leaders of followed peoples, holders of watched
      lands — with role lines, ages, and last-sighting tooltips; membership recomputes only
      on dirty (follow toggles, YOURS events, or a dead implied member — present-tense roles
      may never outlive their holder). Living introductions: ambient "A NEW THREAD" cards
      when someone enters your story (watched-seat heirs, a followed soul's child/spouse,
      whoever slays one of yours), each soul introduced once; guard cards gained a mid-life
      "their tale so far" so the memorial stops being the first framing. Feed channels:
      "Your story" pinned above "The world" (own windows 14/60), world rows compress to one
      line while focused; followed-land plain births/deaths half-boosted and beat-free
      (feel-test flood fix) — Region Lens/recap memory channels untouched. Person cards
      open with why-you-care. verify exact 884/699/567/706 throughout.
- [x] Map-First Followability + Panel Economy V1 (2026-06-12, viewer-only): the map made the
      primary stage again after The Cast's panels crowded it. Binding panel economy contract in
      docs/VISUAL_STYLE.md — three modes (Watch / Inspect / Chronicle), a full surface
      classification table, one-major-panel rule, center-map blocking reserved for Chronicle
      Mode + ceremony. As built: guard moments now voice as a compact top toast (why-you-care
      chip + the tale with honest place language + Resume / the full tale / how we got here);
      the full gold card opens only on click — the memorial keeps its unasked center ceremony;
      true-place toasts pulse the region, home-only anchors get remembered-home language in the
      memory tint and pulse nothing. Cast + inspector now share one left VBox column
      (stacking structurally impossible — they previously both sat at (12,132)); the cast is
      compact by default (sigil chips + 1–2 names, full roles on unfold) and folds whenever an
      inspector takes the column. How We Got Here is a right side sheet over the feed rail
      (quick beats 400px; full thread widens to 620px — the explicit Chronicle Mode read). The
      writing desk became a right drawer with a light ink wash (edit stays atomic; closing
      still returns to the inspected object). Feed rail 320→300 with world rows receding
      (0.78 alpha) while focused unless loud; bottom dock 96→78. Map-first verbs:
      MapView.FocusPerson/FocusRegion — a cast click inspects AND eases the lens onto their
      deterministic scatter region (dead/landless fall back to the line's home, honestly
      nowhere if null); manual pan/zoom cancels any automated lens move. Feel-checked live via
      window captures: toast → full card → thread escalation, side sheet, left dock, compact
      cast all verified in-game. verify exact 884/699/567/706 throughout.
- [x] Living Atlas Surface + God-Hand V1 (2026-06-12, sim + viewer + new gate): the map became
      a data-driven editable world skin and the game became playable through real divine verbs.
      **Surface** (`WorldSurface.cs`, Godot-free): 96×96 deterministic cell grid — terrain
      classes (ocean/shallows/coast/plains/forest/highland/wetland/river/lake), elevation,
      vegetation, nearest-seat region bridge, gradient-descent rivers, journaled terraform
      edits + StateHash; generated from pure coordinate hashes (zero Rng draws), never read by
      the tick — provably baseline-inert. MapView renders it as ONE nearest-filtered pixel
      texture (2 texels/cell, hash speckle, two-tone canopy, elevation shading, 0.13 banner
      wash) rebuilt only on terraform/territory change; clicks resolve through surface cells;
      island polygon + adjacency web + region-circle paint retired; held places read as hut
      clusters. **God-hand** (`DivinePressure` ledger in World): BlessPerson / PlantCurse
      (ledgered) / ProtectFaction / DoomFaction / SeedOmen / SeedForest / CallSpring — all
      multipliers on EXISTING rolls (bless eases the death roll; protect/doom scale the famine
      multiplier + bias the prosperity walk in self-expiring windows; omen is attention-only
      by design; terrain acts mutate the surface), so zero new draws and verify held EXACTLY
      884/699/567/706 — deliberate, the baseline did not move. Cause-links where mechanically
      true: blessed death → BUT death-despite-blessing; famine under doom → THEREFORE
      famine-under-doom; famine despite protection → BUT famine-despite-protection (ButRules
      extended; StoryCopy voices all four authored phrases). **Viewer**: verbs live on their
      target's inspector (bless/curse on souls, protect/doom on peoples, omen/forest/spring on
      the Region Lens — no fake buttons anywhere); Fate Ledger right sheet (acts + state +
      consequences traced via an incremental index); map payoff per the binding table in
      VISUAL_STYLE.md (pale-gold blessed ring, gold/ember faction tag threads, violet omen
      star, terrain changes are the terrain); Region Lens surfaces the holder's customs; cast
      tooltips carry reputation bands. **Gate**: `divine` proves double-run determinism of
      chronicle+ledger+surface hash, target validation, curse traceability, authored
      classification of every divine edge, real terrain deltas, channel honesty (person acts
      placeless, region acts anchored, nothing divine home-anchored), and canon-blindness.
      (`surface` gate name was taken by the surfacing demo — surface proofs live in `divine`.)
      Live-driven playtest: all seven verbs exercised in the running game, 576 shown years at
      16×, ledger + BUT-connector verified on a real blessed death (lived to 72), terrain
      edits visible immediately. Save/persistence of pressures + edits is session-only — the
      documented deferral.
- [ ] Later — battle sites and per-region
      economy (add events/RNG and move the verify baseline; together they extend place memory to
      battles/famine and let war's-peace / famine's-end close chapters); mythic glosses / entity
      links; relationship constellation; local site/tableau visuals; memorial tableau upgrade;
      visual/UX pass (surface culture + gossip in the viewer); echo packs; timeline scrubbing;
      followed-faith audit. ← NEXT

## Session log
- [2026-06-12] Session: Living Atlas Surface + God-Hand V1 (three commits). (1) WorldSurface
  cell grid in the Sim + MapView pixel-diorama texture render (island polygon/circles retired,
  surface-cell hit-testing). (2) DivinePressure ledger + seven god-hand verbs with
  multiplier-only mechanics, divine cause-links, two new authored BUT rules, and the `divine`
  console gate (chronicle+ledger+surface determinism, validation, channel honesty,
  canon-blindness). (3) Viewer verbs on inspectors, Fate Ledger sheet, StoryCopy divine
  phrases, map payoff marks, culture/reputation surfacing, docs. verify held exactly
  884/699/567/706 through all three (multipliers modulate existing rolls; surface draws no
  Rng) — gates verify/homes/story/canon/divine all green; Godot build clean. Playtested live
  by driving the running game: blessed Roesia (died at 72, thread voiced "but even the old
  blessing could not hold them —"), cursed Roduin's line, protected the Highland Clans,
  doomed the Wood Tribes, seeded omen + forest + two springs on the Tangled Wood II (terrain
  visibly changed), ran 576 shown years at 16×, read the Fate Ledger end to end.
- [2026-06-12] Session: Map-First Followability + Panel Economy V1 (viewer-only, 5 files:
  Main.cs, CastPanel.cs, CanonPanel.cs, MapView.cs, docs/VISUAL_STYLE.md). Audited every panel
  and found the structural stacking bug (cast + inspector both pinned at (12,132)); wrote the
  binding panel economy contract (Watch / Inspect / Chronicle modes + classification table)
  into VISUAL_STYLE.md. Guard toast replaces the default center guard card; left dock VBox for
  cast (compact default) + inspector; catch-up → right side sheet (full thread widens);
  writing desk → right drawer; feed 300px + world-row recede; bottom dock 78px; cast clicks
  ease the lens via MapView.FocusPerson (manual camera always wins). Verified live by driving
  the running game (window captures at ~1366×768): recap-on-pause, side sheet quick/full,
  Region Lens left dock, follow-land → compact cast strip, guard toast firing with honest
  "in the Deep Green" place language, toast → full card escalation. All gates green: verify +
  homes + story + canon, exact 884/699/567/706; Godot build clean.
- [2026-06-07] Session: built M0→M2 + longevity pass (carrying capacity + perf refactor, proven
  identity-preserving). 6 commits pushed to DrewYomantas/living-myth. Set up isolated nested git repo
  (home dir was an accidental repo).
- [2026-06-07] Session: M3 Yours channel. Follow button on the person + faction inspectors; YOURS rows
  gold-tagged + weight-boosted in the feed; followed dots ringed cyan in MapView. Marked-set check is
  inline + O(living) in `StreamNewHeadlines`; the bloodline grows virally at birth (mirrors the curse),
  so no per-tick `Feed.BuildFeed`. Build clean, `verify` green.
- [2026-06-08] Session: Yours-channel surfacing fixes (directed `Bloodline` lineage; YOURS feed-share
  cap). Then M4 economy engine: `Faction.Prosperity` random-walks (mean-revert to 1.0); threshold
  crossings emit `famine`/`boom` events (InFamine/InBoom hysteresis), trade between prospering factions
  eases tension; famine death multiplier mirrors the curse with a cause-chain; births scale 0.7–1.3×
  with prosperity. Two new echoes. Re-baselined: `verify` 720/455/527/677, cap=0 807/523/452/987.
  Population stable ~165–490 over 5000 yrs after softening famine_death_multiplier 1.8→1.4. Determinism
  green. NOT yet committed.

- [2026-06-09] Session: Visual/UX pass — Phase A. Pass 1 (viewer-only): readable 1× (BaseInterval
  0.5→1.2 s/yr), speed ladder 0.25×–16×, dramatic auto-slow on notable ticks, feed-row flash +
  map region pulse (`e166623`). Pass 2: (1, sim) schism rate cut — `schism_chance_per_year`
  0.02→0.006 + new `schism_min_members` 8→14; **re-baselined verify 934/704/292/621** (seeds
  1/18/42/7), prior was 678/363/383/558; schisms 80yr 4/5/4/9→2/0/2/0, ~13–20 per 600yr. (2, sim
  metadata) `Religion.OriginEventId` set at every faith creation; persecution now causes-links to the
  faith's founding so catch-up walks back to the heresy origin — counts unchanged. (3, viewer) echo
  rationing: per-archetype 60yr cooldown + significance bar (≥80) + window cap (2/40yr). (4, viewer)
  MapView camera: cursor-anchored wheel/button zoom + drag pan (clamped), hit-test correct via shared
  P() transform, drama-follow eases toward pulses unless player took manual control (`7adfd67`).
  Both commits pushed. Determinism green. NOT yet feel-tested in the Godot viewer.

- [2026-06-09] Session: M7 culture engine, then M8 gossip/reputation layer. M8 — `Gossip()` runs
  after `Culture()` in the tick, reads only events recorded since last year (`_lastGossipEventCount`
  cursor, no all-history scan), and for the notable few (importance ≥42, chance-gated, per-person 8yr
  cooldown, ≤2/yr) records a `rumor` event cause-linked to the real deed. Reputation shifts ±1
  (clamped -5..5); cross-faction rumors call `AddTension` so the rumor id lands in grievance memory
  and a war it helps cause traces back through the whisper. Couplings: blackened names are caught more
  easily (murder discovery scales with -reputation), respected prophets win one extra early follower.
  Caught a sentinel-overflow bug (`int.MinValue` LastRumorYear made `Year - LastRumorYear` overflow
  negative → cooldown always tripped → zero rumors; fixed with explicit sentinel guard). Re-baselined
  verify 884/699/567/706, cap=0 1145/1097/535/893. Determinism green; Godot builds.

- [2026-06-10] Session: two viewer passes. (1) Map readability + place seeds (`fa6fc88`): PlaceSeeds
  viewer hints, roads, label tags, layering. (2) Region Lens foundation: RegionActivity read-model
  over Event.RegionId, Region Lens inspector with honest "not yet modeled" copy, e:/r:/f: inspector
  cross-links, "Inspect <faction>" hand-off button, gold selected-region ring. No sim files touched
  either pass; verify green at 884/699/567/706 throughout. Feel-checked via window captures.

- [2026-06-10] Session: living atlas visual style foundation. Drew delivered the four Batch 1
  north-star concept images (renamed `Visual references/gpt-northstar-*.png`, concept-reference-only
  per that folder's README); wrote `docs/VISUAL_STYLE.md` — the implementation-facing style bible
  (reference interpretation split honest/aspirational/forbidden, component language tied to the
  `Ui.*` API, palette/typography direction, F5 visual check ritual, staged roadmap 1–6, viewer
  audit). Viewer foundation slice (viewer-only, 3 files): `Ui.ParchmentTag`/`Ui.DockBox`/`LensGold` +
  medallion-rim `ChipBox` in UiTheme; MapView — parchment place tags (region name + place-kind hint,
  zoom ≥ CaptionZoom, always-on gold-bordered for the selected region, skip-on-overlap vs faction
  tags), warmed sea/land/coast + shallows rim (derived geometry, zero extra rng draws), fainter
  adjacency web, shared accents now from `Ui.*`; Main — framed dock groups, feed breathing 6→8,
  brighter chip glyphs. Sim untouched; verify green at 884/699/567/706. Next visual milestone:
  Atlas Composition Pass (roadmap 2 in docs/VISUAL_STYLE.md).

- [2026-06-10] Session: time & story pacing model (docs-first milestone). Audited the whole
  time surface — sim clock (one Tick == one year, no sub-year unit), viewer pacing consts,
  importance thresholds, follow/catch-up loops — and measured the core problem: at 1× a full
  human life passes in ~84 wall-clock seconds and a war resolves in ~2.4 s, so the chronicle is
  legible after the fact but not followable as it happens. Wrote `docs/TIME_AND_STORY_PACING.md`:
  the four clocks (World Time sim truth / Drama Time shown pace / Focus Time protected attention /
  Chronicle Replay Time), event weight bands named over the existing thresholds (background <
  chattiness 60 / notable / major ≥ NotableBar 100 / turning point ≥ echo bar 80), focus-guard
  design (pause-on-drama, "you last saw…" memory, followed-death cards), chapter recaps (25 shown
  years or arc closure — "era recap" finally defined), quiet-years acceleration, replay/scrub
  feasibility (scrub is chronicle reconstruction, one audit hole: historical faction membership),
  alternate-path ghosts ruled forbidden until near-misses are modeled. Tiny viewer slice: pace-tier
  tooltips on the speed buttons (linger/watch/unfold/drift/hasten/sweep/ages) + doc-anchor TODO.
  Sim untouched; verify green at 884/699/567/706.

- [2026-06-10] Session: focus guard slice (viewer-only, Main.cs). GuardMode off/★ followed/all
  cycles on a new Time-dock toggle; MaybeArmGuard runs in the stream loop BEFORE the chattiness
  gate (a followed soul's death always cards; otherwise Major+ YOURS, or any Major in "all");
  a death outranks a same-tick recap; the armed trigger is consumed after the tick completes
  (and after the curse tool's immediate stream), pausing via the existing _running gate. Gold-
  bordered guard card: event line with band glyph + region, death cards add the record (born/
  died/age, reputation, children) + last-6-deeds thread (one-shot scan, inspector-click cost
  class), "you last saw…" from a per-marked-person last-YOURS-event dictionary (O(1) updates,
  also shown in the person inspector). Year card grew 14 px for the "⛨ guard watches…" signal
  (inspector panel shifted to match). Kill()/RecordMurder audit confirmed types "death"/"murder"
  with the victim as a participant cover every real death path (executions Kill first; martyr/
  justice are post-death commentary). Sim untouched; verify green at 884/699/567/706.

- [2026-06-11] Session: Focus Guard V2 — followed soul + memorial moment (viewer-only; Main.cs,
  MapView.cs). Drew's playtest verdict on V1: the followed-death card read as a normal info card,
  and only bloodlines were followable. Added `_followedSouls` — an explicit per-soul set consulted
  by IsYours, RememberSeen, the guard death trigger, and the inspector last-seen line, but never
  passed through `Feed.ExpandMarked` and never grown virally at birth (soul ≠ line). Two distinct
  inspector verbs: "Follow this soul" (living people; a dead soul can still be unfollowed) above
  "Follow this bloodline". Memorial path in ShowGuardCard/StyleGuardCard: ink-veil backdrop that
  swallows clicks, 640×500 frame, 3px gold border + gold rule + class-glyph medallion, centered
  [font_size] name with faction/born–died/age, "the world holds its breath" lead; a soul death
  outranks a bloodline death which outranks a recap within one tick. Honesty: no RegionId → gentle
  "the chronicle records no place for this passing" (never an invented place; pulse only a real
  region); "N children carry the line" only from real Children.Count; "once their leader" only from
  EverLeader; bloodline-only deaths say "a tale of a bloodline you follow closes". Future work:
  richer relationship cards, family tree view, portrait/token system, region/home/site anchoring,
  chapter recap cards, handling multiple followed deaths in one tick. Sim untouched; build + Godot
  build clean; verify green 884/699/567/706.
- [2026-06-11] Playtest fix: chasing a link off a guard card (a child, a deed) lost the card with no
  way back. Added a floating "↩ Return to the memorial / tale" chip (top-center) that reopens the
  held card — content survives CloseGuardCard, so reopening is just re-show + restore the dim. The
  chip lives only while the world is still paused from that card; once time resumes the moment has
  passed and it disappears (a later manual pause never resurrects a stale card).
- [2026-06-11] Session: chapter recaps (viewer-only; Main.cs). Chapters count SHOWN years (ticks
  displayed), default 25, closing early on an echo carding or a followed soul's memorial; the sim
  has no chapter state. Closing QUEUES the recap — chip by the year card, card auto-opens on the
  next transition into pause (never over a guard card; the stream is never auto-paused for a
  boundary, which at 16× would interrupt every ~2 s); latest unread chapter only. Card sections:
  Loudest of the Age (top-3 full Importance over the chapter slice, BuildReverse one-shot on card
  open — echo-scan cost class), Your Threads (births into follows, losses with event links,
  reputation BAND shifts for followed souls, region counts for followed peoples — all measured
  from chapter start or the follow, whichever later; "passed the age quietly" when nothing moved;
  section omitted entirely when nothing is followed), Myths That Echoed. The guard-card return
  chip generalizes to "↩ Return to the chapter". Honesty: war's-peace and famine's-end arc
  closures are NOT implemented — peace events carry no faction attribution and famine's end
  records no chronicle event; both filed under the region/sim contract. Verified live: boundary
  chip at Yr 25/50/…, auto-open on pause, link→catch-up→return loop, guard card outranking a
  queued recap, Your Threads with 41 births + 14→10 regions on a followed people. Sim untouched;
  verify green 884/699/567/706.

- [2026-06-11] Session: Living Diorama V1 — watched souls made visible (viewer-only; Main.cs,
  MapView.cs). Drew's lead-dev verdict after recaps: enough cards and text — the player must
  witness a followed soul living in the diorama before the game asks them to mourn. Map presence:
  the soul halo gained a minimum screen radius (findable at fit zoom where dots are 4px), a
  breathing outer ring (sin on a wall-clock accumulator — O(followed) per frame), a flare via
  `MapView.PulseSoul` when a displayed saga row truly names the soul, and a gold-bordered ★-name
  tag drawn in the label layer with overlap-skip; soul hit radius widened to the halo. Clicking a
  watched soul's dot routes to `SoulPicked` → the living soul glimpse: a small parchment card
  positioned near the marker (clamped to the map area), non-modal, never touches `_running`;
  every line is a real field (faction, IsLeader/EverLeader, age/born, reputation band, children
  count, `_lastSeenEvent` memory, last-3 deeds one-shot scan — inspector cost class); closes
  whenever an inspector, guard card, or recap opens (memorial precedence unchanged — guard
  backdrop is also built later, so it draws above). Saga: rows with a watched-soul participant
  get `BorderWidthLeft = 4` gold + tooltip prefix, computed O(participants) only when souls are
  followed. Honesty: nothing new is invented — no homes, routines, or locations; the glimpse
  states absence plainly ("nothing of them has crossed the saga…"). Sim untouched; verify green
  884/699/567/706.

- [2026-06-11] Session: Place Memory V1 — anchored events scar the land (viewer-only; Main.cs,
  MapView.cs). The first persistent-memory slice, built strictly against what's honestly
  anchored: only territory (founding/war/abandonment) and culture events truly carry
  Event.RegionId, so only those mark — `ClassifyMark` in the existing O(new events) stream loop
  feeds `MapView.AddPlaceMark`. Rumors anchor but are excluded by design. Marks: standing stone,
  war scorch + snapped pole, cairn, violet culture ribbon — drawn at layer 5a (beneath place
  markers and labels), fixed slot angles around the region centre, capped 4/region with the
  oldest yielding, alpha aged deterministically by sim-year (no RNG, no wall clock). Region Lens
  "Marks upon the land" lists the real event behind every mark as a catch-up link; unmarked
  places say so plainly. Docs: VISUAL_STYLE.md Place Memory section flipped to SHIPPED with the
  as-built table + deferred rows gated on the anchoring contract; audit lines updated (Scarred
  by events ✖→◐, Memory coverage note). Sim untouched; verify green 884/699/567/706.

- [2026-06-11] Session: Event Anchoring Contract V1 — audit-only, no code changed. Walked all 31
  `Chronicle.Record()` call sites in World.cs against the no-fake-locations principle (anchor only
  when the code path already holds a specific real region; never faction land for convenience,
  never nearest, never inferred). Finding: **zero new safe anchors exist** — territory events are
  already exactly anchored, custom/rumor carry the pre-doctrine PrimaryRegion seat-proxy
  convention (kept, flagged for the seat contract), and every other category genuinely has no
  region in scope: people have no locations, wars have no battlefields (battles aren't modeled),
  the economy is faction-scoped, leadership has no seat, peace has no treaty site. Full audit
  table added above ("Event Anchoring Contract — audit"); VISUAL_STYLE.md readiness note: no new
  Place Memory V2 mark kinds unlocked this pass, with the per-contract unlock map. Confirmed
  nothing sim-side reads Event.RegionId (write-only metadata) and viewer place language is gated
  on real anchors everywhere (memorial: "the chronicle records no place for this passing").
  Sim untouched; verify green 884/699/567/706.

- [2026-06-11] Session: Person.HomeRegionId Contract V1 — the first sim contract off the
  anchoring audit's unlock map, and it proved **baseline-safe**: verify held exactly
  884/699/567/706 because the contract draws no RNG, records no events, and changes no
  iteration that feeds either (the founders' backfill rides the existing founding-territory
  loop; verify hashes only `Chronicle.Render()`). Rules: founders ← founding seat (the region
  their founding event already anchors), newborns ← father's home else mother's, immutable for
  life, null stays honestly null. New console gate `homes` (pattern-matched to `verify`)
  asserts founder-seat (recovered from the chronicle's own anchors), inheritance, valid-region,
  honest-null, and double-run home-map determinism — green at 120 and 1000 yrs, 100% coverage
  on seeds 1/18/42/7. Viewer got exactly one line (inspector "home: {region}" / "—"). Event
  anchoring to homes (deaths, murders, births) deliberately deferred to its own slice.

- [2026-06-11] Session: Life Memory Home Anchors V1 — the payoff slice, and it stayed
  baseline-safe (884/699/567/706 exact) because the design adds a **second anchor channel**
  instead of bending the first: `Event.HomeRegionId` (where a life is remembered) lives beside
  `Event.RegionId` (where it happened), so every existing place consumer — catch-up "· in {X}",
  place pulses, Place Memory marks, "Tales anchored here" — is structurally unable to mistake
  memory for location. Event texts and RNG untouched; `Render()` prints only text, so verify
  could not move. Wording contract: home anchors may say only "of {X}" / "rooted in {X}" /
  "remembered in {X}" — never bare "in {X}", never died/born/murdered in/at. Murder anchors to
  the victim's home, documented. `homes` gate extended (anchor-matches-soul, RegionId-null on
  life events, text-never-names-anchor, double-run anchor determinism). Viewer: memorial
  where-line + pulse fallback, Region Lens "Lives rooted here" via a second incremental
  RegionActivity channel — all O(new events), no history scans.
- [2026-06-11] Session: Memorial cairns — Place Memory V2's first slice (viewer-only; Main.cs,
  MapView.cs). The home-memory data finally scars the land, on its own channel:
  `MapView._homeMarks` (AddHomeMark/HomeMarksFor/HasHomeMark) is a separate store from the V1
  place marks, fed only from `Event.HomeRegionId` in the existing O(new events) stream loop —
  mixing the channels is structurally impossible. Gate deliberately person-truth instead of
  importance (a plain death scores 5; an importance bar would have been dead code): murders
  always, deaths only of EverLeader souls, births never. EverLeader is final by death, so marks
  never depend on playback pacing. Drawn as stacked stones + a gold remembrance light at the
  region rim (own slots, radius 0.78), cap 3/region, V1's deterministic fade. Lens cairn rows
  live inside "Lives rooted here"; the death guard card claims a cairn only after
  `HasHomeMark` confirms it stands. Language audit clean: "remembered/raised at the home of
  their line", never died-here. verify 884/699/567/706 exact.
- [2026-06-11] Session: followed regions — the fourth followable subject (viewer-only; Main.cs,
  MapView.cs). The pacing doc's "honest but quiet" caveat aged well: Life Memory V1 means a
  followed land now speaks through TWO honest channels (anchored tales + remembered lives), not
  one. Implementation rides the existing yours machinery: RegionYours() folded into IsYours, so
  gold rows, YoursBoost, guard arming, and "born into what you follow" recap counts all inherit
  region follows for free; no new code path can disagree with the old ones. Death guard cards
  still require a followed *person* — a stranger's death remembered in a watched land can
  surface as a generic guard card ("fate touches a land you watch") but never fakes a memorial. Recap land
  deltas count the two channels apart and never merge them. Map: persistent quiet gold ring
  (Ui.Gold A=0.45 at r+1.5 vs the lens ring's LensGold A=0.85 at r+3). verify 884/699/567/706.
- [2026-06-11] Checkpoint: shipped memorial cairns + followed regions in one peer-reviewed pass
  (ab0a2d9 — two recon subagents, two adversarial reviewers, one BLOCKER caught: murder cairn
  line misattributing the victim's home to a killer-focused card) and refreshed .claude/CLAUDE.md
  (840efbf: anchoring arc, homes gate, scoring + anchor-channel gotchas). All gates green, tree
  clean, pushed. Next: F5 feel-test (see "Next session starts with").

- [2026-06-11] Session: Myth Authorship + Causal Chronicle V1 — the milestone that turns
  "and then" into "therefore". Recon found the decisive fact early: `Event.Causes` already
  records the chains (revenge→murder, martyr→murder, persecution→faith origin,
  succession→death, war→grievances incl. rumors), so the whole grammar could be a pure
  read-model — baseline structurally safe. Built in seven slices, each gate-green:
  (1) truth-model docs; (2) `StoryGrammar.cs` + `story` gate — proximate-cause pick is
  latest-year (provably max-id), "but" is authored-only (the showcase rule:
  war-despite-peace, because a blessed union's −3.5 AddTension still lands its event in
  grievance memory — the one edge where generic "therefore" would lie), honest-unknown is
  an allow-list with Routine-silence default so "the chronicle does not say" can never
  flood; (3) `PlayerCanon.cs` + `canon` gate — the gate caught a real store bug
  (StateOf memoized Active across *different* world instances; now keyed per (note, world));
  (4) causal catch-up — connectors voiced only under a visible cause row, year-named when
  branches interleave so a connector can never visually re-aim; (5) the writing desk
  (CanonPanel) + tellings/notes/inscriptions/legends/people-say across person/region/
  faction/catch-up/memorial surfaces, guard-card body extracted into BuildGuardBody so a
  fresh inscription shows on return; (6) recap "Still unresolved" + reputation memory copy
  + glossary [hint]s; (7) adversarial verifier pass over the full diff — 2 BLOCKERs caught
  and fixed (catch-up RichTextLabel had no MetaClicked handler, every ✎ link dead; recap
  said "the killer died unpunished" where a justice event may be recorded — copy narrowed
  to exactly what `Murdered && !Avenged` proves) + 4 WARNs (connector re-aim, stale
  inspector selection reopening on canon save, dismissed guard card force-returning, .bak
  rescue missing UnauthorizedAccessException). Gates after everything: build clean ×2,
  verify exactly 884/699/567/706, homes green, story green (120 + 1000 yrs), canon green.
  Known accepted limits: faith notes have a reserved `rel:{id}` key but no surface (no
  religion panel exists); rename is schema-reserved only; quarantined notes have no
  recovery UI (the file keeps them); overwriting a quarantined note's slot replaces it.

### Life Memory — anchor contract (2026-06-11)

| Event | `RegionId` (where it happened) | `HomeRegionId` (where it is remembered) | Whose home, and why |
|---|---|---|---|
| birth | null — no birthplace is modeled | child's home | the new life's own root (inherited at birth) |
| death (all causes) | null — no death place is modeled | deceased's home | the line that mourns is the dead's own |
| murder | null — no murder site is modeled | **victim's** home | the grief and memorial belong to the victim's line, not the killer's provenance |
| everything else | unchanged (exact anchors / disclosed seat conventions) | null | home memory is life-event semantics only |

Viewer language rules for `HomeRegionId` (binding): "of {X}", "rooted in {X}", "remembered in
{X}", "home of their line" — never "in {X}" alone, never "born/died/murdered in/at {X}". Null
anchors produce no place language at all (the memorial's honest no-place line remains). Home
memory and true place anchors must never share a viewer surface without distinct labeling.

Core principle: **no fake locations.** `Event.RegionId` is set only when the recording code
path already holds a specific real region. It is never derived from faction land for
convenience, never the nearest region, never inferred from prose. Nothing in the sim *reads*
`RegionId` (write-only metadata), so anchoring can never shift RNG draws or outcomes.

**Audit finding: there are no new safe anchors to add today.** Every call site with a real
region in scope already passes it; every unanchored site genuinely has no region available.
The audit's value is the contract map below — what each blocked category waits on.

| Event category | RegionId today | Real region in scope? | Safe to anchor? | Reason / future contract |
|---|---|---|---|---|
| founding (world begins) | — | no (map not yet generated) | no | world-scoped by design |
| founding territory ("hold the lands") | ✅ eldest hold (`owned[0]`) | yes (exact list) | shipped | multi-region event; anchors to the eldest hold by id |
| territory seized in war | ✅ exact region | yes | shipped | the gold standard — the code iterates the seized region |
| territory abandonment | ✅ eldest freed (`freed[0]`) | yes (full list) | shipped | multi-region event; anchors to the eldest freed hold |
| custom (born/fade/clash/diffusion) | ◐ `PrimaryRegion(f)` | convention only | shipped (M7) | **seat-proxy convention**: the people's eldest hold (lowest id — same rule as the territory anchors) stands in for "the heart of their lands". Pre-dates the strict doctrine; kept because Place Memory V1 consumes it, flagged for replacement by a seat-of-power contract |
| rumor | ◐ `PrimaryRegion(fac)` | convention only | shipped (M8) | same convention; the viewer deliberately never marks rumors |
| birth | — | no | no | parents have no location → `Person.HomeRegionId` contract |
| death (age/illness/famine/curse/war) | — | no | no | the person has no location; war deaths additionally have no battlefield → `Person.HomeRegionId` + battle-site contract |
| murder (ambition/revenge/persecution/honor) | — | no | no | killer and victim are unplaced → `Person.HomeRegionId` |
| succession / leadership | — | no | no | no seat of power is modeled → seat-of-power contract |
| war declared | — | no | no | war is faction-pair scoped; no front or mustering ground exists |
| battle / skirmish / raid | not modeled | n/a | n/a | war yields abstract yearly casualties, no battle events; a battle-site contract would *create* events (moves the baseline — deliberate future milestone) |
| peace | — | no | no | no treaty site; peace also carries no faction ids (separate known gap) |
| famine | — | no | no | prosperity is per-*faction*, not per-region → per-region economy contract |
| boom / plenty | — | no | no | same as famine |
| drought | not modeled | n/a | n/a | no drought system exists |
| trade | — | no | no | no routes or markets are modeled |
| prophet | — | no | no | the prophet is a person, unplaced → `Person.HomeRegionId` |
| prophecy / omen | not modeled | n/a | n/a | no prophecy-as-promise system exists (only the prophet event above) |
| schism / martyr | — | no | no | religions span factions; no holy site is modeled |
| justice (execution/exile) | — | no | no | unplaced people |
| romance / scandal / marriage | — | no | no | unplaced people |
| friction | — | no | no | faction-pair scoped |
| divine (curse) | — | no | no | the cursed person is unplaced; the map click position is viewer scatter, **not** sim truth |

## Truth model V1 — four ledgers (Myth Authorship arc — SHIPPED 2026-06-11)

Binding from the Myth Authorship milestone on. Every piece of story the player sees belongs
to exactly one ledger, and the ledgers never blur:

| Ledger | What it is | Where it lives |
|---|---|---|
| **Recorded Fact** | What the sim knows happened: Event rows. Text is immutable — `Render()` is the verify comparand | `Chronicle` |
| **Causal Claim** | A relationship the viewer may assert because it is deterministically provable: an `Event.Causes` link, stored person/faction state, or an authored rule over those. Derived on demand, never stored, never invented | `LivingMyth.Sim/StoryGrammar.cs` (read-model beside Echoes/Feed/Scoring) |
| **Player Telling** | Freeform player-authored lore: tellings, chronicler's notes, memorial inscriptions, place legends, what-the-people-say. Always labeled, always visually apart, never interleaved into record text | `LivingMyth.Sim/PlayerCanon.cs` store → `user://canon_seed{N}.json` |
| **Mechanical Truth** | What the sim acts on: live state (`Avenged`, `Tension`, `Prosperity`, …) | `World` |

V1 rules: **Player Telling never becomes Mechanical Truth** (a future structured nudge
system would be the only promotion path, and it does not exist). The sim never reads the
canon store — enforced by the `canon` gate (reflection over sim types + a behavioral
double-run). Canon is save-specific (per-seed file); world-template and shared/mod canon
are future scopes the data shape must not block.

### Connector provenance contract (binding)

A causal connector may appear in the viewer ONLY when backed by deterministic evidence:

| Connector | Meaning | Required evidence |
|---|---|---|
| therefore | proven consequence | the effect's `Causes` contains the cause (the generic default — `Causes` literally means "events that led to this one") |
| but | proven complication or reversal | **authored rules only, never generic**: persecution-of-faith (faith proclaimed → adherent killed for it), scandal/honor killing off a forbidden bond, war-despite-peace (a blessed union provably *eased* tension yet its event sits in the war's grievance memory), ways shed/grate (customs) |
| N years passed | real time gap | `effect.Year − cause.Year`, actual arithmetic — never mood |
| unresolved until | a grievance provably open across the gap | revenge only: `victim.MurderEventId == cause.Id`, `victim.Avenged` flipped by exactly this event, the slain is `victim.KillerId` |
| the chronicle does not say | honest unknown | authored allow-list of rootless events only: prophet (what first stirred them), schism (what doctrine divided them), forbidden bond (what drew them together) |

Rootless events otherwise classify **RecordedMotive** (the text states it: ambition murders,
natural deaths, friction, the founding), **ThresholdState** (famine/boom/trade/custom
adoption — a threshold crossing), or **Routine** (births, weddings — say nothing). The
default for an unrecognized rootless event is Routine: silence can never overclaim.

Thread lifecycle vocabulary (viewer language over chains — never stored sim state):
opened → deepened (therefore) → complicated (but) → quiet (no recorded consequence yet) →
resolved (peace / avenged / faded) or transformed (martyrdom, seizure). When the sim cannot
prove what filled a gap, the viewer states the gap honestly ("the grievance lay
unresolved") — never "they plotted for years".

### Rename / display-name contract (scaffold only)

`Person.Name` and `Event.Text` are immutable identity: internal names live forever, and
historical event text always renders the original names (it is also the verify comparand,
so it cannot change). Any future rename feature is display-layer only — the canon schema
reserves `display_name_override` (documented, not built). No rename UI exists in V1.

## Region Lens — data contracts still missing (design notes, not promises)
The viewer-side lens is honest about these; each needs a deliberate sim-side milestone because all
of them move the verify baseline (new RNG draws and/or new ordered iteration):
- **Person ↔ site anchoring.** People have no home region/site; the atlas scatter (p.Id % regions)
  is presentation only. Contract: a Person.HomeRegionId assigned at birth/migration, deterministic.
- **Settlement/site state.** No sites exist in the sim. Contract per GAME_DESIGN.md slice 3: 3–7
  deterministic sites per region (id, kind, position), then optional Event.SiteId.
- **Terrain geography.** Regions are points (X/Y + radius circles). Contract: deterministic region
  polygons/bands so the atlas can read as landforms instead of circles.

## Playtest verdict (2026-06-12, Drew — the F5 feel-test happened)
The causal arc landed: following a region, a bloodline, and a soul is much easier, and
investment in *how and why* is real. The new problem is **cast-tracking overwhelm**: following
a land + one soul, only 1–2 names stick; the flood from follows plus the world's own churn
evicts the rest. Cards help but arrive mostly at death — the memorial is doing the
introduction's job. Diagnosis (lead-dev): not primarily an art problem — (1) names are the
only identity handle (no stable per-person visual mark), (2) people are introduced at death,
not when they enter your story, (3) yours and the world share one feed channel (gold trim is
decoration, not separation). **Decision: next milestone is "The Cast" (viewer-only)** — see
the roadmap. Also received: north-star Batch 2 (four concept images — the stylized
semi-realistic pixel-diorama atlas, settlement views, a Chronicle Replay screen remarkably
close to the shipped causal grammar) — pending placement in `Visual references/`, to be read
with the same honest/aspirational/forbidden discipline as Batch 1.

## Next session starts with
**F5 feel-test of The Cast** (shipped 2026-06-12, not yet watched): follow a soul + a land
+ a people, then judge — does the cast panel make names stick? do sigils read at a glance
(and never read as event chips)? do thread cards introduce at the right rate (too chatty /
too quiet)? does the your-story/world split calm the flood without muting the world? does
a watched land still speak enough after the life-event tuning (YoursBoost/2)? does the
mid-life "tale so far" make the eventual memorial land harder? Tune: YoursWindow (14),
the intro trigger set, the quiet-region-life boost divisor, cast cap (8). Drew also opens
the Godot editor → commit the new `*.cs.uid` sidecars it generates for PersonSigils/
CastPanel. Then: surface culture + gossip, or timeline scrubbing.

The original feel-test checklist (kept for the items not yet judged in a long sitting):
- Pacing basics (oldest unseen work): 1× readability, dramatic auto-slow depth/length
  (`BaseInterval`, `SlowdownWindow`/`SlowdownFactor`), echoes as punctuation
  (`EchoArchetypeCooldown`/`EchoSignificanceBar`/`EchoWindowCap`), drama-camera lean
  (`FollowZoom`, `ManualCamCooldownSecs`).
- Focus arc: guard pause/resume rhythm, memorial card weight, held-card return chip, chapter
  recap cadence (25 shown years — too long? too short?), living glimpse usefulness.
- This session's work: are memorial cairns legible at fit zoom and distinct from abandon cairns
  (`HomeMarksPerRegion`, rim radius 0.78, the gold light); is the followed-land ring visible but
  quiet (Gold A=0.45 at r+1.5); does following a populous land throttle high-speed playback too
  hard (every home-rooted birth/death is YOURS + notable — if so, consider gating the dramatic
  beat or the boost for region-yours life events); does the "fate touches a land you watch" card
  fire at a sane rate.
Myth-authorship additions to the same sitting (none of it seen running either):
- Open How We Got Here on a revenge murder: connector lead-ins read naturally? gap years
  right? "the grievance lay unresolved for N years, until —" lands?
- Find a prophet event → "the chronicle does not record what first stirred X" + ✎ link →
  write a telling → confirm it shows on the person card and back in the catch-up thread.
- Memorial card: why-line, "lies unavenged", ✎ inscription → save → card returns with the
  inscription shown.
- Chapter recap: "Still unresolved" grievance lines; reputation "name darkens" copy.
- Hover a [hint] term (whispered against, remembered in) — confirm the gloss tooltip renders
  (Godot 4.6 BBCode [hint="…"]; fallback plan: TooltipText, one-line StoryCopy change).
- Restart the viewer: the telling is dormant until the year it was written about returns,
  then reappears (save/load roundtrip live).
Then the next implementation pass: surface culture + gossip in the viewer, or timeline scrubbing
(close the historical-faction-membership audit hole first, per docs/TIME_AND_STORY_PACING.md).

### Backlog (after Phase A)
**More pressure engines, then richer surfacing.** The three core loops (watch → mark → trace) are done.
- Visual/UX: the map is deliberate placeholder art (three columns of dots). Consider real island
  geography, faction territory shapes, settlement clustering, and a cleaner feed/inspector skin.
- More pressure engines: a culture system in `World.cs` (alongside religion/war/economy) for richer
  event types — keep all randomness through `Rng` and every result-feeding iteration explicitly
  ordered, or `verify` will break.
- Echo packs: more archetypes in `Echoes.cs` beyond the current 10.
- Gossip distortion layer: a stretch goal — events get retold/mutated as they spread.
