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
- [x] Persistence + Sites V1 (2026-06-12, sim + viewer + two new gates): the player-shaped
      world survives relaunch, and places became real data. **Persistence** (`PlayerWorld.cs`,
      canon-store pattern): the world save is an INPUT JOURNAL at `user://world_seed{N}.json` —
      never a snapshot — holding every divine act (kind/target/year + identity snapshot), the
      follows (souls/bloodlines/peoples/lands with person snapshots), last-seen attention
      state, and the resume year; on launch the deterministic sim fast-forwards to the resume
      year with each act re-applied at its recorded year (`ApplyDue`), so the same world
      returns byte-identically — edits, Fate Ledger, follows, and all. Drifted targets
      quarantine (kept in file, never misapplied); corrupt preserved read-only; future schema
      preserved untouched; atomic writes; the sim never reads the store. Viewer saves on every
      act / follow change / settle-into-pause / autosave heartbeat / window close; replayed
      history feeds every index but never cards, pulses, or introduces; resumed worlds start
      paused. **Sites V1** (`Sites.cs`): 3–7 named, terrain-honest sites per region — a SITE
      READ-MODEL (not a sim contract): pure hashes off seed + regions + the PRISTINE surface
      (built in the same breath as WorldSurface, so no terraform edit can precede it), never
      read by the tick, type honesty cell-checked (ford by the river, dock on the shore, fort
      on the heights; seats typed from their own cell), names from authored fragments in
      data/names.json, holder derived live from the region. **Event.SiteId deliberately
      DEFERRED** — the `sites` gate asserts the field is ABSENT, so a fake anchor is
      structurally impossible. **Replay prep** (`Replay.cs`): replay-ready beats over
      StoryGrammar.Annotate (SiteId honestly null). **Viewer**: site markers replace the
      PlaceSeeds hints (every structure on the map is now a real place; seat banners, local
      paths, seat-to-seat roads), zoom-gated site name tags, site click targets + gold ring,
      the Site Card (honest holder/ground/the-land's-tales/not-modeled lines), Region Lens V2
      "Places of this land", faction lands name their seats. New gates `save` + `sites`
      green; verify held EXACTLY 884/699/567/706 (read-models + replay of the player's own
      inputs). Live-driven feel-check: followed a land + called a spring, killed the viewer,
      relaunched — resumed paused at Yr 242 with the spring replayed, the follow restored,
      and the Fate Ledger intact.
- [x] Chronicle Replay + Site-Anchored Memory V1 (2026-06-12, sim + viewer + new gate): history
      made visible across the atlas, and the first events that truly belong to a single place.
      **Event.SiteId shipped** (the deferral above, lifted deliberately) — a fourth, conservative
      anchor channel governed by ONE authored convention table (`SiteAnchors.Expected`, Sites.cs):
      territory+founding/abandonment → the region's seat; territory+war → its stronghold (hill
      fort → watch post → river ford); custom-born/fade → its sacred site (shrine → grove →
      barrow → cairn). Everything else stays null; births/deaths/murders never anchor (memory
      channel only). Picks are immutable-site, type-priority, lowest-id — **zero Rng**, so the
      baseline did not move: verify held EXACTLY 884/699/567/706 (6–14 site-anchored events/seed).
      **Replay V2** (`Replay.cs`): `ChainFor` returns the focal event's cause beats + a bounded
      (cap 8) direct-consequence rail; each `ReplayBeat` copies its anchors VERBATIM and carries
      an honest Status (site-anchored / region-only / memory-only / unanchored) + authored copy
      key + faction ids; `TurningPointKind` is a bounded authored pivot classifier (war/peace,
      land lost/abandoned, violent succession, faith torn/proclaimed, ways hardened, divine-
      influenced, far-reaching). **Viewer**: How We Got Here gained a turning-point header (who/
      peoples/place), an honest anchor phrase per row ("at {site}" only for a true SiteId), a
      "What grew from this" consequence rail, and a ⟲ Replay button that retells the chain on a
      dimmed atlas — numbered marks on honestly anchored beats along real cause edges (spine bold),
      a beat card + scrubber, memory-only/unanchored beats living ONLY in the rail (never a fake
      pin); turning-point diamonds pulse on the map (placeless pivots never mark); Remembered
      Places panel (`RememberedPlaces.cs`) lists every truly-touched place with honest filters +
      anchor language; the Site Card grew real site memory ("known for" from recorded counts,
      site-anchored tales, the hand upon the land). All copy authored in `StoryCopy.cs`. New gate
      `replay` green (deterministic chains, verbatim anchors, honest statuses, bounded real
      consequences, authored turning points, save-safe); `sites` gate rewritten to PROVE the
      anchoring contract event-by-event. Live-driven feel-check: turning-point thread header, the
      ⟲ retelling stepped 1→10 with the focal seizure pinned at Morburgh, unanchored schism beats
      staying pinless, Remembered Places war filter, the Morburgh Site Card "known for: fought
      over 2 times; a people's first hold was raised here".
- [x] Theater of War — Battle Sites V1 (2026-06-13, sim + read-models + viewer + gate; the
      first DELIBERATE baseline move since M8): war stopped being abstract yearly attrition and
      became battles fought at real places. A new `battle` event is recorded the first time
      blood is drawn in a war-year (lazy — a standoff year records none), anchored to the war's
      **front** (a real border region World resolves deterministically) and, when one stands
      there, its **stronghold** site (hill fort → watch post → river ford) via the ONE authored
      convention table (`SiteAnchors.Expected`, now covering `war`/`battle` alongside
      territory+war). War casualties cause-link to the battle ("dies in the fighting"); the war
      declaration is anchored to its front and both events carry their peoples' leaders, so the
      peace event finally carries faction attribution **and the toll** ("After 2 battles and 3
      souls fallen, … make peace") — closing the chapter-closing gap the recaps noted. **The
      determinism keystone:** battles WRAP the war's existing casualty rolls — `FrontRegion` and
      the battle record draw ZERO Rng, so the stream stays byte-identical and population balance
      is provably preserved; `verify` moved by EXACTLY the battle-event count (884/699/567/706 →
      **894/705/574/715**, i.e. +10/+6/+7/+9 battles per seed at 120 yr). New showcase echo **The
      Field of Bones** — the first echo keyed on a place (`Event.SiteId`): a single site that saw
      ≥3 battles across the wars of the age. Authored grammar rules `war-to-battle` and
      `battle-death` (full coverage, no generic fallback); Scoring weight `battle`=50; battles are
      NOT a turning-point kind by design (only the war/peace/land pivots are — a far-reaching
      battle surfaces via the existing ≥4-consequence fallback), so `Replay`/the replay gate were
      untouched. Battle deaths stay home-remembered (the four anchor channels never mix). Viewer:
      crossed-swords battle scar (Place Memory), battle events in Remembered Places' war filter +
      the Site Card "known for" ("N battles were fought here"), catch-up connectors for the new
      edges; war-pivots now pin the map at their front (war events gained RegionId+SiteId). The
      `sites` gate rewritten to PROVE the battle convention non-vacuously (32 battles / 22
      site-anchored across the suite). All EIGHT gates green; Godot build clean. Full Battle Sites
      contract below ("Battle Sites V1 — the battle contract").
- [x] Harvest Economy V1 (2026-06-13, sim + read-models + new gate + viewer copy; the SECOND
      deliberate baseline move): famine and plenty stopped being abstract faction numbers and became
      a **region's harvest**, so they anchor to the land. The harvest random-walk moved from
      `Faction.Prosperity` to a new per-`Region` `Harvest` (the economy's ground truth); every
      region walks each tick (god-hand protect/doom biases the holder's lands, additive on the same
      draw), but **only a held region emits** `famine`/`boom`/`famine_end`, anchored to **RegionId,
      never SiteId** (a famine spans a land, it isn't at one site — `SiteAnchors` deliberately NOT
      extended; the harvest + sites gates both prove the non-leak). `Faction.Prosperity` is now a
      **derived compatibility surface** (the controlled-region harvest **mean**), and
      `InFamine`/`InBoom`/`FamineEvent` are derived rollups (worst region starves / any region
      feasts / worst region's onset event) — so births, culture, trade, and famine death pressure
      read the same fields unchanged, only their source moved to the land. `famine_end` is a real,
      region-anchored, chapter-closing beat (cause-linked to the onset it answers — the gap the
      recaps had been missing). Famine deaths cause-link to the region's famine event but stay
      **home-memory anchored** (`HomeRegionId` set, `RegionId == null`) — the four anchor channels
      never mix. Read-models: `StoryGrammar` `famine-breaks`; `Scoring` `famine_end`=35; **The
      Barren Years** echo (one land that starved ≥3 times in a single age — the first famine echo
      keyed on `Event.RegionId`, clustered like The Long Famine). New gate `harvest` proves
      derivation (Prosperity == mean; rollups), landless-faction neutrality, land-anchoring,
      no-SiteId-leakage, `famine_end` pairing, life-event channel honesty, and harvest-state
      determinism (264 land-anchored economy events / 45 recoveries / 3 landless checks across the
      120-yr suite, non-vacuous). **The deliberate baseline move:** unlike Battle Sites (zero-Rng),
      this adds real per-region draws + reshapes faction-mean prosperity, so the stream moved in
      BOTH directions per seed — **894/705/574/715 → 823/559/910/632** (Δ −71/−146/+336/−83, seeds
      1/18/42/7). Balance preserved with **no tuning**: 5000-yr living `168/157/306/150`, all stable,
      no extinction (seed 42, the canary, holds 306; `carrying_capacity` stays 300). All NINE gates
      green; Godot build clean. Full contract below ("Harvest Economy V1 — the harvest contract").
- [x] Beta-Readiness Pass V1 (2026-06-13, viewer + docs, lead + agent team): the first pass aimed at
      Drew-beta-testability rather than new systems. **Onboarding & discoverability** (`godot/Main.cs`):
      "The Watcher's Guide" — a player-invoked Chronicle-Mode reading card (and the one onboarding
      surface, auto-opened once on a fresh world, "▶ Begin watching" dismisses) covering controls,
      an honest map legend (every mark/ring drawn — founding stone / battle swords / war scorch /
      abandon cairn / famine scar / memorial cairn / culture ribbon / turning-point diamond, and the
      follow/blessed/omen rings — glyphs & colours mirrored from MapView so it can't drift), and the
      powers of the hand (where the god-verbs live, since they're inspector-only). **Fresh-world
      affordance** (`✶ New World`, confirmation-gated): discards the world save (acts/follows/resume)
      and reloads the scene for a clean start, keeping the player's canon — closing the "delete the
      save file by hand" gap (and the fix for an old save whose acts all quarantine after a
      baseline-moving sim change). **Visual treatment** (`godot/MapView.cs`): a painted shoreline —
      land cells touching the sea darken a touch, so the island reads as a placed thing on the water
      (atlas signature). Empirically verified by launching the real viewer: clean resume (only the
      expected act-quarantine warnings) and clean fresh launch (zero warnings), Guide renders,
      shoreline confirmed on-direction. Viewer-only — verify held exactly 823/559/910/632, all 10
      gates green, both builds clean. Docs: visual direction locked to "stylized semi-realistic
      fantasy pixel diorama" across DESIGN/VISUAL_STYLE/VISUAL_PIPELINE/roadmap, stale verify
      baselines corrected to 823/559/910/632, the binding Kenney/AI adoption policy added to the
      Godot asset scout, famine-scar polish status reconciled to shipped.
- [x] Visual North Star Push V1 (2026-06-13, sim read-model + viewer + console + docs): the first
      pass to actually move the **atlas** toward the locked North Star — *stylized semi-realistic
      fantasy pixel diorama, a living atlas*. The surface coloring moved out of MapView into a shared
      pure read-model **`src/LivingMyth.Sim/SurfacePainter.cs`** (zero Rng, never read by `Tick` —
      baseline-inert like Sites/Replay), so the viewer and a new headless **`paint`** console command
      render byte-identical pixels (a screenshot is the real render, not a mock). The painter replaces
      flat terrain fills with: a **BFS depth-graded sea** + low-freq swell + pale surf line; a **warm
      painterly coast** (beach-sand blend on the first land cells, retiring the hard shore-darken);
      **NW-lit hillshade relief** off the elevation gradient; faint **contour ink** tracing elevation
      bands and stronger **inked political borders** where holders meet; **two-octave mottling** so no
      region reads as bucket-fill; 3 texels/cell. Markers gained a **contact shadow** (`DrawGroundShadow`)
      so each site reads as a diorama miniature, and every inspector now wears a **heraldic
      holder-colored header stripe** (`FactionTint`). New tooling: `PngWriter` (dependency-free PNG via
      `ZLibStream`+CRC32) and a viewer **self-capture** (`LM_SHOTS=<dir>` fast-forwards a fresh world,
      shoots the atlas + a region lens in-engine, quits — never touches the player's save). Evidence in
      `docs/visual_pass/` (before/after + seed variety + in-engine shots, see its README). Viewer/read-
      model only — **verify held exactly 823/559/910/632**, all 9 console gates green, both builds clean.
- [x] North Star Diorama Prototype Pass V1 (2026-06-14, viewer + Blender pipeline + docs, lead +
      brutal North Star judge agent): after the Visual North Star Push V1 atlas pass was honestly
      rated "still alpha", a course-correction to build a REAL, sandboxed visual prototype proving
      the *stylized fantasy diorama* direction rather than polishing the atlas further. **New asset
      pipeline** `tools/art/render_diorama.py` (headless Blender 5.1 → Cycles 96spp + denoise →
      shadow-catcher-grounded transparent PNGs → 19 diorama miniatures: tree clusters w/ 3 species
      variants, hill, rocks, cottage/hall, keep, watchtower, standing stones, shrine, dock, field,
      banner) — a real step up from the flat-cone spike (layered organic foliage, 3-point light,
      bevels, per-object jitter, procedural material mottle). **New Godot view** `godot/DioramaView.cs`
      + `.tscn` (F3 from the atlas / standalone / `LM_DIORAMA_SHOT` self-capture): an **isometric
      region diorama** — tilted per-cell ground plane with NW raking-light relief, Blender miniatures
      billboarded + depth-sorted at REAL `Sites` positions, low-freq clearing mask exposing earth/
      water, settlement clearings, faction banners, parchment label callouts — wrapped in North Star
      parchment/brass/ink chrome (Year plate, serif title, inspector card + house chip, region-
      anchored Saga feed, legend, brass action bar) + `godot/shaders/parchment_post.gdshader`
      (warm-grade + grain + vignette). 100% read-model: builds its own world, never writes/saves.
      **Independent judge moved it 2→3→4→5/10** across four iterations (camera→variety→iso
      projection→legibility); final verdict: a clear leap over the production viewer (~3/10) and a
      legitimate prototype proving the direction. **The honest ceiling is ~5/10 without dedicated
      art**: the miniatures are stylized flat-shaded low-poly, not the hand-painted material-rich
      North Star references — closing 5→7+ is art labor, not code. Viewer/read-model + tooling only —
      **verify held exactly 823/559/910/632**, all 9 console gates green, both builds clean. Evidence:
      `docs/visual_pass/DIORAMA_PROTOTYPE.md` (+ prototype / before-after / asset contact sheet).
- [x] Production Diorama Bridge V1 (2026-06-14, viewer + small art pass + docs): turned the
      sandboxed F3 diorama prototype into an honest production bridge for selected regions, without
      replacing the atlas. **Wired to the live world** — `DioramaView` takes `SourceWorld` +
      `SourceRegionId` from Main and renders the *currently selected region* at the *live year*
      (souls/tales/holder/harvest read live), not a seed-7-only world. **Entry/exit** — a
      "⛰ Enter the Diorama" button in the Region Lens (region & site context) + `Main.OpenDiorama`/
      `CloseDiorama` open it as a full-rect **read-only overlay** (NOT a scene swap — atlas, follows,
      and save stay intact underneath); F3 kept as a dev shortcut for the selected/most-built region;
      Esc / "← Back to the Atlas" closes. **Honest controls** — the fake 7-disc action bar
      (Curse/Bless/**Prophecy/Plague/Terrain** mocks) is removed; the real god-hand verbs stay in the
      atlas inspector where they journal to the save, and the diorama bar now reads "READ-ONLY
      CHRONICLE VIEW · ART IN PROGRESS" + a real Back button — no mock tool presented as real.
      **Fallbacks** — wild/unclaimed regions render with no banner + "unclaimed country"; no-sites
      regions show "an unwritten country"; sparse regions frame on their centroid. **Small art pass**
      — fuller sun-kissed multi-lobe broadleaf canopies + a better-reading keep (slate roof,
      arrow-slit windows, two-tone stone). Evidence captured via the REAL overlay flow in-engine
      (`docs/visual_pass/` 01 atlas → 02 region lens → 03 diorama bridge [same region] → 04 wild
      fallback). Viewer/read-model + offline-asset only — **verify held exactly 823/559/910/632**,
      all 9 console gates green, both builds clean (0 warnings). Sim determinism untouched.
- [x] Diorama Bridge Hardening V1 (2026-06-14, viewer + docs — hardening, no new art, no sim
      changes): made the bridge robust and honest about time. **True seat** — the inspector card's
      "… seat" line reads the region's actual `IsSeat` site (not `sites[0]`; "no seat yet" when a
      region has none). **Label collision avoidance** — site callouts in dense clusters are nudged
      downward off one another (then sideways if the column fills) so a knot of sites stays legible
      instead of stacking. **Label clamping** — pills are clamped clear of the title/Year band (top)
      and the legend/bottom bar, so they never collide with the title or clip off the top edge.
      **Time FREEZES while open** — `Main.OpenDiorama` pauses `Tick()` (mirroring Chronicle Mode's
      replay freeze) and restores the prior play state on close, so the diorama's chrome stays an
      honest snapshot of the opened year and you return to the atlas at the same year you left
      (pacing-only — never changes Tick() count/order). **Doctrine note** (binding, in
      DIORAMA_PROTOTYPE.md): the "⛰ Enter the Diorama" button is a *transitional bridge / debug
      affordance*; the final North Star UX is seamless atlas zoom/lens travel into a land, NOT a
      separate clicked mode — seamless zoom deliberately not built yet, the note exists so the bridge
      doesn't calcify. Evidence: `docs/visual_pass/` diorama_forest/coast/highland/wild + a
      label_before/after pair on the dense 7-site Stone Crown (collision avoidance OFF→ON via the
      `LM_DIORAMA_NOAVOID` capture toggle). Viewer-only — **verify held exactly 823/559/910/632**,
      all 9 console gates green, both builds clean (0 warnings).
- [x] North Star Art Pipeline V1 — Terrain + Prop Language (2026-06-14, Blender + headless Krita +
      viewer + docs; NO sim changes): the first **real, reproducible art-pipeline slice** — proving
      a repeatable three-stage recipe that pushes live deterministic regions toward the North Star.
      **Stage 1 Blender** (`tools/art/render_diorama.py`): richer 2-noise material brushwork (coarse
      weathering + fine grain) on every prop; a new top-down OPAQUE ground-tile pass
      (`ground_coast/forest/highland/water`, water with voronoi foam); new `pulse_marker` + a fuller
      `banner`. **Stage 2 headless Krita** (`tools/art/krita_paintover.py`, run via `kritarunner` —
      no GUI): per asset, gaussian-blur→unsharp painted smear + an **edge-ink overlay**
      (edge detection→invert→multiply, **alpha-inherited** so ink clips to the silhouette). The
      kritarunner plumbing (its OWN resource dir, `enable_` flag, args-tolerant entry) is captured in
      `tools/art/krita_plugin/INSTALL.md`; the plugin shim ships in `tools/art/krita_plugin/`.
      **Stage 3 Godot** (`DioramaView.cs`, unchanged read-model contract): textured iso ground
      diamonds (UV+texture) for coast/forest/highland/water, pale **shore foam** on water↔land
      edges, warm **roads** seat→places, ember **pulse markers** on the 3 most-recent site-anchored
      tales (tinted to event class). All gated behind `LM_DIORAMA_RAW=1` so the same region captures
      **before vs after** from one build. Honest North Star score **5→6.5** (biggest win: flat teal
      water diamond → textured water + foam; props gain illustrated ink outlines); the value is the
      *proven repeatable pipeline*, not the bump. Evidence + recipe + score + recommendation:
      `docs/visual_pass/ART_PIPELINE_V1.md` + `docs/visual_pass/artpipeline_v1/`
      (before/after coast·forest·highland, mid_coast 3-stage, contact_sheet). **Recommendation: adopt
      as the production art route** — next gains are content (richer Krita chains, more silhouettes),
      not plumbing. Viewer/asset-only — **verify held 823/559/910/632**, all 9 gates green, both
      builds clean.
- [x] North Star Biome Silhouette V1 (2026-06-14, Blender forms + 1 scatter tweak + docs, NO sim) —
      spent the budget on art direction (silhouette + biome identity + the seat as a landmark), not
      plumbing, by improving the **Blender source forms** in `render_diorama.py`: broadleaf gets 3
      crown profiles (round/wide-oak/tall-birch) + a visible 2-seg trunk + broken crown; conifer
      gets a tall ragged fir spire + a bushier pine variant; rocks become angular faceted stone;
      a NEW **crag** (stratified ridge outcrop) wired into highland scatter (stone-first now); the
      **keep** becomes a real seat (curtain wall + corner turrets + gatehouse + dominant tower — the
      biggest read jump); dock gains a moored rowboat; standing-stones gain a true trilithon. The
      three biomes now read distinct (coast=open shore+dock+seat, forest=canopy depth, highland=
      stone/crags/firs+hill-fort). **Honest North Star score 6.5→7.0**; what blocks 7.5+ is all art
      direction (broadleaf macro-massing still reads as sphere clusters; composition is uniform-
      density scatter with no focal hierarchy; no macro depth/AO; quiet ground at zoom) — content,
      not plumbing. Evidence: `docs/visual_pass/BIOME_SILHOUETTE_V1.md` + `biome_silhouette_v1/`
      (before/after coast·forest·highland, compare_old_new, contact_sheet). Viewer/asset-only —
      **verify held exactly 823/559/910/632**, all 9 gates green, both builds clean.
- [x] Terrain-Typed Harvest V1 (2026-06-15, sim + gate; the THIRD deliberate baseline move — lead +
      Explore/architect/implement/review agent team): the deferred sim follow-up to Harvest Economy
      V1 — biomes that *look* distinct (Biome Silhouette V1) now *behave* distinct. Each region's
      yearly harvest walk keys off its immutable `Region.TerrainType` via two data-driven levers
      applied to the SAME single `Rng.RandInt(-1,1)` draw (ZERO new draws — the determinism keystone):
      per-terrain **volatility** (`harvest_vol_*`, a multiplier on the step) and a per-terrain revert
      **target** (`harvest_target_*`, replacing the hardcoded 1.0 the walk reverts toward — the
      fertility lever). `TerrainHarvestParams(string)` is a pure switch over the immutable terrain +
      `Params` (zero Rng, fail-fast `throw` on unknown terrain). Forest stays vol 1.0 / target 1.0 —
      algebraically byte-identical to the old walk, so the mechanism reduces EXACTLY to old behavior
      at all-1.0 (balance-neutral by construction). Final balance-safe band: coast 1.0/0.7 (steady
      safe land), forest 1.0/1.0 (baseline), plains 1.05/1.15 (fertile + swingy), highland 0.95/1.1
      (poorer + famine-prone). **The balance lesson (caught by the probe, invisible to 120-yr gates):**
      the architect's first band (highland target 0.78 / vol 1.3) passed all 9 gates at 120 yr but
      EXTINCTED 2 of 4 seeds at 5000 yr — highland-heavy founding peoples (the Highland Clans seat on
      highland terrain) starved early and collapsed to a revenge-murder spiral by ~yr 94. Fertility-
      via-mean is balance-constrained: a strong sub-1.0 highland mean chronically suppresses faction
      prosperity → death-spiral. The fix narrowed the band (lean on volatility for famine drama, keep
      means in a tight survivable band). `harvest` gate extended to prove differentiation at the SAFE
      band: plains fertility by suite mean (1.070 vs forest 1.024), highland harshness + coast safety
      by famine RATE (region-normalized: highland 31 famines / forest 23 / coast 0). **Deliberate
      baseline move** (real harvest-distribution reshape → downstream births/trade/war/deaths):
      823/559/910/632 → **657/691/528/726**. 5000-yr balance **158/139/162/160 living, no extinction**
      (old range 168/157/306/150). All 9 gates green, both builds clean (0 warnings). NO viewer payoff
      yet — biomes behave differently but nothing surfaces it in-game (the next slice).
- [ ] Later — **viewer carding of terrain-typed harvest** (← NEXT: surface the new biome behavior
      in-game — Region Lens "hard country / a breadbasket / a steady shore" condition language off
      `Region.TerrainType` + its harvest state, terrain-aware famine/plenty framing, deepen `The
      Barren Years`; viewer-only, must hold 657/691/528/726); **diorama art fidelity** (turn the
      Blender blockout props into hand-finished illustrated assets: textured albedo, painterly
      canopies, roof/timber/stone detail — the 5→7+ gap; or adopt licensed grounded-medieval assets
      per the asset-scout policy); person↔site anchoring (a home site, not just a home region);
      a map-table vignette/framing pass + marker outlines (sandbox/screenshot-verify each); relationship
      constellation; local site-scale view; memorial tableau upgrade; surface culture + gossip in the
      viewer; echo packs; timeline scrubbing; followed-faith audit; per-launch seed choice
      (today seed is fixed at 7). ← NEXT

## Session log
- [2026-06-15] Session: Terrain-Typed Harvest V1 (lead dev + Explore/architect/implement/review
  agent team, all 5 phases run through the lead for critical review). The deferred sim follow-up to
  Harvest Economy V1 and the third deliberate baseline move. Per-terrain volatility + revert target
  on the SAME single harvest Rng draw (zero new draws); forest byte-identical to old. The pivotal
  moment was Phase 5: the architect's first band passed all 9 gates at 120 yr but the 5000-yr balance
  probe EXTINCTED seeds 18 & 42 (highland-heavy peoples starved early → murder-spiral collapse by
  ~yr 94). Lead diagnosed (traced the chronicle to the cause of death), recognized fertility-via-mean
  is balance-constrained, narrowed the band to survivable, and recalibrated the gate to prove the
  REAL safe-band differentiation (plains fertility by mean; highland harshness + coast safety by
  region-normalized famine rate) rather than an aggressive mean spread that only existed at extinction
  params. Final: all 9 gates green, both builds clean, 5000-yr 158/139/162/160 (no extinction), verify
  re-baselined 823/559/910/632 → 657/691/528/726. Commit 32420cd (sim+gate). NO viewer payoff yet —
  next slice is viewer carding of the new biome behavior. The lesson worth keeping: short-gate-green
  ≠ balance-safe; the 5000-yr probe is the only thing that catches early-population extinction, and
  any sub-1.0 revert target is a chronic faction-suppressor, not just a flavor knob.
- [2026-06-14] Session: North Star Art Pipeline V1 + Biome Silhouette V1 (two milestones). Proved
  the reproducible Blender → headless Krita → Godot art pipeline (kritarunner plumbing solved: own
  resource dir + enable flag + args-tolerant entry + alpha-inherit ink fix), then improved the
  Blender source FORMS (broadleaf profiles, fir/pine, angular rocks, new crag, imposing keep, dock
  boat, trilithon). Score 5→6.5→7.0. verify held 823/559/910/632, all 9 gates green throughout.
  Next: close the 7.0→7.5+ gap's #1 blocker — broadleaf macro-massing. Replace the sphere-cluster
  canopy in `render_diorama.py:canopy()/broadleaf()` with real trunk+branch structure + a
  non-spherical canopy mass so a dense wood stops reading as clustered balls at region zoom.
- [2026-06-13] Session: Beta-Readiness Pass V1 (lead as integrator + a read-only scout team +
  a parallel doc-truth agent). Goal was beta-testability, not new systems. Alignment first proved
  the project already green: both builds clean, all 10 gates pass, verify at 823/559/910/632, and
  the documented "#1 known issue" (famine-scar ring crowding) was in fact ALREADY fixed in commit
  55862fb — only the docs were stale. Shipped, all viewer-only (verify held exactly 823/559/910/632,
  10 gates green): (1) The Watcher's Guide — controls + honest map legend + the powers of the hand,
  auto-opened once on a fresh world; (2) `✶ New World` — a confirmation-gated fresh start that
  discards the world save and reloads, keeping canon (the first in-app fresh-world affordance);
  (3) a painted shoreline on the atlas surface. Empirically launched the real Godot viewer (console
  exe, scene passed explicitly per the binary gotcha) and screenshotted: resume path clean (only the
  expected act-quarantine warnings from a pre-Harvest save), fresh path zero warnings, Guide + new
  shoreline confirmed rendering on-direction. Drew's `world_seed7.json` was backed up before any
  launch and restored byte-identical after — fully non-destructive. Docs (parallel agent): visual
  thesis locked to "stylized semi-realistic fantasy pixel diorama" everywhere, stale 884/699/567/706
  "current baseline" references fixed to 823/559/910/632, binding Kenney/license/AI adoption policy
  added to docs/GODOT_ASSETLIB_SCOUT.md, famine-scar status reconciled, god-tool forbidden list
  corrected (Bless/Terrain shipped; only Prophecy/Plague stay forbidden). Untracked stray
  `docs/spike_offon_compare.png` (1.4 MB, undocumented) left out of the commits by the screenshot rule.
- [2026-06-13] Session: Harvest Memory Viewer Payoff V1 (viewer-only, small agent team + live F5
  feel-test). Wired the Harvest Economy's deferred viewer half across 4 godot/ files: `famine_end`
  Recovery event class (green ❀); `MapView.MarkKind.FamineScar` (ochre cracked-earth, famine onset
  only, distinct from war/cairn/battle marks); Region Lens "Harvest memory" section (live
  `Region.InFamine`/`InBoom` condition, qualitative no-numbers, + recent harvest beats + channel
  note); Remembered Places harvest filter chip. Pure read-model — verify held exactly
  823/559/910/632, all 10 gates green, independent channel-mixing verifier PASS. Commit 85729fd;
  claude.md milestone/Next refresh 4132cce. Live F5 feel-test (Year 316): confirmed Recovery glyph
  + harvest filter + region-anchored channel honesty. The KNOWN ISSUE here (famine scars subtle at
  low zoom and crowding the 4-slot per-region mark ring) was RESOLVED by the famine-scar polish pass
  below (commit 55862fb). Next: terrain-typed harvest, plus the still-unwatched F5 feel-tests.
- [2026-06-13] Session: famine-scar polish (viewer-only, commit 55862fb "viewer: polish famine
  scars outside place mark ring") — the deferred #1 feel-test finding fixed. Famine scars now live
  in their OWN per-region scar store with a 1-most-recent cap (no longer competing for the 4-slot
  place-mark ring, so rare founding/war/battle marks survive recurring famines), drawn at a
  dedicated reserved slot angle outside the ring, and rendered larger with a higher alpha floor for
  low-zoom legibility. Pure read-model / viewer-only — verify held 823/559/910/632, all 10 gates
  green.
- [2026-06-13] Session: Harvest Economy V1 (lead dev, single coherent pass). The paired half of
  the M4 economy and the SECOND deliberate baseline move since M8 — famine/plenty became a
  region's harvest. Moved the random-walk from `Faction.Prosperity` to a per-`Region` `Harvest`
  (the ground truth); `Prosperity` is now the derived controlled-region mean, with `InFamine`/
  `InBoom`/`FamineEvent` as worst/any rollups (so births/culture/trade/death read the same fields,
  source-shifted to the land). Only held regions emit `famine`/`boom`/`famine_end`, anchored to
  RegionId, never SiteId (`SiteAnchors` deliberately not extended; harvest + sites gates prove the
  non-leak). `famine_end` is a real region-anchored chapter-closing beat cause-linked to its onset
  — closing the recap gap. Famine deaths cause-link to the famine event but stay home-anchored
  (`HomeRegionId`/`RegionId==null`). Read-models: `StoryGrammar` `famine-breaks`, `Scoring`
  `famine_end`=35, **The Barren Years** echo (first famine echo keyed on RegionId, age-clustered).
  New gate `harvest` (derivation / landless neutrality / land-anchoring / no-SiteId-leak /
  famine_end pairing / channel honesty / determinism) green and non-vacuous. Caught and fixed an
  extinction during tuning: a derive-after-trade ordering tipped seed 42 to 0 living by shifting
  the trade-guard RNG stream; the fix (derive-before-trade + per-trade re-derive of the two
  traders, zero Rng) restored healthy balance AND made the mean invariant exact. Final baseline
  **894/705/574/715 → 823/559/910/632** (Δ −71/−146/+336/−83); 5000-yr balance `168/157/306/150`,
  no extinction, no param tuning. All NINE gates green; sim + Godot build clean, zero warnings.
  Viewer touched only for connector copy (`famine-breaks`) — full viewer carding of `famine_end` /
  The Barren Years / a famine land-scar is the deferred follow-up. Docs: spec in
  docs/superpowers/specs/, PROJECT_STATE milestone + harvest contract, CLAUDE.md pending (separate
  commit per the churn rule). NOT yet F5 feel-tested in the Godot viewer.
- [2026-06-13] Session: Theater of War — Battle Sites V1 (lead dev + recon/viewer/review
  subagent team). The first deliberate baseline move since M8, executed as a zero-new-Rng
  additive slice: `World.RecordBattle` wraps the existing war casualty rolls into a lazily
  recorded `battle` event anchored to the war's deterministic `FrontRegion` and its stronghold
  (`SiteAnchors` extended with war/battle → stronghold; `FrontRegion`/`WarLeaders`/`PeaceText`
  helpers added). War declarations anchor to the front and carry leaders; peace carries leaders
  + the toll (chapter-closing gap closed). Read-models: StoryGrammar `war-to-battle` +
  `battle-death` rules; Scoring `battle`=50 / tag +10; Echoes **The Field of Bones** (first
  place-keyed echo). Gates: `verify` re-baselined 894/705/574/715 (battles draw no Rng — the
  delta IS the battle count, balance preserved); `sites` gate rewritten to PROVE battle anchoring
  non-vacuously (32 battles / 22 sited across the suite); all eight green (verify/homes/story/
  canon/divine/save/sites/replay). Viewer (subagent, per the recon map): crossed-swords mark,
  Remembered Places war filter, Site Card "known for", catch-up connectors; war-pivots now pin
  the front. Field of Bones confirmed firing on a long run ("the Bracken Fastness saw 3 battles
  over 25 years"). Determinism note: battles are deliberately NOT a turning-point kind (war/peace/
  land pivots only), so Replay + the replay gate were untouched.
- [2026-06-12] Session: Chronicle Replay + Site-Anchored Memory V1 (five commits). (1) Replay
  read-model + gate: `Event.SiteId` shipped through the ONE authored convention table
  (`SiteAnchors.Expected` — founding/abandonment→seat, war→stronghold, ways→sacred site;
  zero Rng); `Replay.ChainFor` (cause beats + bounded consequence rail, verbatim anchors,
  honest Status) + `TurningPointKind` (bounded authored classifier); `LinkBetween` for literal
  edges; new `replay` gate (deterministic chains, verbatim anchors, honest statuses, bounded
  real consequences, authored turning points, save-safe) + `sites` gate rewritten to PROVE the
  anchoring contract event-by-event. (2) Site memory + Remembered Places: RegionActivity gained
  a site channel + "known for" kind tallies; `RememberedPlaces.cs` panel (honest filters +
  anchor language); Site Card grew real site memory. (3) Chronicle Replay viewer: turning-point
  thread header + "What grew from this" rail + honest anchor phrases + ⟲ Replay button; dimmed-
  atlas overlay (numbered marks on anchored beats, real cause edges, spine bold), beat card +
  scrubber, no fake pins. (4) Turning-point pulses on the live map (placeless pivots never
  mark). (5) Docs. verify held EXACTLY 884/699/567/706 throughout (SiteId picks draw no Rng;
  everything else is read-model/viewer); all EIGHT gates green (verify/homes/story/canon/divine/
  save/sites/replay) + Godot build clean, zero warnings. Feel-checked live by driving the running
  game: schism thread turning-point header, the ⟲ retelling stepped beat 1→10 with the focal
  Iron-Pass seizure pinned at Morburgh and the unanchored bond/schism beats staying pinless,
  Remembered Places war-&-land filter, the Morburgh Site Card ("known for: fought over 2 times;
  a people's first hold was raised here"). Test save deleted afterwards.
- [2026-06-12] Session: Persistence + Sites V1 (four commits). (1) PlayerWorldStore — the
  world save as an input journal (acts + follows + attention + resume year), viewer
  journaling/fast-forward/restore, the `save` gate (11 checks: roundtrip, byte-identical
  replay of chronicle+ledger+surface, unapplied-inert, follow/act quarantine, corrupt/future
  preservation, canon separation). (2) Sites.cs — 3–7 terrain-honest sites per region as a
  baseline-inert read-model off the pristine surface; Replay.cs replay-ready beats; the
  `sites` gate (determinism incl. an edited-world run, cell-in-region, per-cell type honesty,
  unique names, Event.SiteId asserted ABSENT, replay-beat honesty). (3) Viewer: site markers
  replace PlaceSeeds hints, site tags + click targets + Site Card + Region Lens V2 places +
  seat roads/paths/banners. (4) Docs. verify held exactly 884/699/567/706 throughout; all
  eight gates green (verify/homes/story/canon/divine/save/sites + Godot build). Feel-checked
  live by driving the running game: zoomed site tags, opened the Dun Cairns site card, opened
  Greyspire's lens (places listed, seat marked), followed the land, called a spring, killed
  the process, relaunched — resumed paused at Yr 242, spring replayed and on the ledger as
  wrought, "guard watches 1 land" restored. Test save deleted afterwards so Drew's first F5
  starts on his own world.
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
| war declared | ✅ the front (Battle Sites V1) | yes (a real border region) | shipped (2026-06-13) | `FrontRegion` resolves a border region deterministically; the war anchors there + its stronghold |
| battle / skirmish / raid | ✅ the front + its stronghold (Battle Sites V1) | yes | shipped (2026-06-13) | the `battle` event wraps the war's existing casualty rolls (zero new Rng) and anchors to the front's stronghold; the baseline moved by exactly the battle count |
| peace | — (placeless by design) | n/a | faction ids shipped (2026-06-13) | no treaty site is modeled, so peace stays placeless; it now carries both peoples' leaders + the war's toll (the chapter-closing gap, closed) |
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

## The world save — schema V1 (binding, shipped 2026-06-12)

`user://world_seed{N}.json`, schema_version 1, written atomically (.tmp then move):

```json
{
  "schema_version": 1, "seed": 7, "app_note": "…", "resume_year": 242,
  "acts": [{ "seq": 0, "kind": "spring", "target_type": "region", "target_id": "10",
             "year": 242, "snapshot": { "name": "Greyspire", "terrain": "highland" } }],
  "follows": { "souls": [], "bloodlines": [], "peoples": [], "lands": [10],
               "snapshots": { "p:12": { "name": "…", "birth_year": "…" } } },
  "last_seen": { "12": 5012 }
}
```

Rules: an input JOURNAL, never sim state — replaying it against the same seed reproduces
the world because the acts are the only player input the sim feels. Act kinds: bless |
curse | protect | doom | omen | forest | spring. Snapshots are identity claims (person
name/birth_year, faction name, region name) — a mismatch on replay QUARANTINES the act
(skipped, kept in the file); dropped follows are warned and removed on the next save.
Corrupt file → preserved, store read-only, viewer sets it aside as .bak; future schema →
preserved untouched, read-only. The sim never reads the store (`save` gate: reflection +
a loaded-but-unapplied journal leaves a clean run byte-identical). Known semantics: the
resumed feed rebuilds only the recent rows (~70) of replayed history; chapter recaps and
echo cards restart at the resume year (replayed echoes are primed as already-seen).

## Sites V1 — the site contract (binding, shipped 2026-06-12)

What a site IS: a stable id (index order), a region id, an authored-fragment name
(unique island-wide), a type the terrain honestly supports (cell-checked), a real
surface cell inside its own region, and a seat flag (first site, nearest the region's
heart, typed from its own cell). Holder is DERIVED live from the region — never stored.
What a site may NOT claim until modeled: population, named dwellers, buildings, stores,
daily life, loyalty/defense. (A site now CAN carry events of its own — see the anchoring
contract below.) Site types V1: market village, watch post, sacred grove, old barrow,
river ford, farmstead, hill fort, fishing dock, shrine, cairn field, wilderness camp.

## Event.SiteId — anchoring conventions V1 (binding, shipped 2026-06-12)

A FOURTH anchor channel, the most conservative. `Event.SiteId` is the single modeled place
an event truly belongs to. It is set by exactly ONE authored table — `SiteAnchors.Expected`
(Sites.cs) — called at record time and recomputed by the `sites` gate per event, so the rule
can never drift in `World` alone. The conventions:
- **territory + founding** → the region's SEAT (a people's first hold is its seat).
- **territory + abandonment** → the region's SEAT (the hold that falls silent).
- **territory + war (seized)** → the region's STRONGHOLD: hill fort → watch post → river ford
  (the defensible place the fighting was over); none present ⇒ region-only, honestly.
- **custom born / fade** → the region's SACRED site: shrine → sacred grove → old barrow →
  cairn field (where a people's ways are sworn and shed); none present ⇒ region-only.
- **everything else** → null. Births/deaths/murders never carry RegionId at all (memory
  channel only), so they can never carry SiteId; rumor/divine/trade/war stay region-or-less
  because no rule honestly places them.

The four channels, never mixed: **SiteId** = the modeled place it belongs to; **RegionId** =
where it happened; **HomeRegionId** = where a life is remembered (never a location); null =
the chronicle does not place it. SiteId is never set without RegionId, and the anchored site
always lies inside that region. Picks are immutable-site, type-priority, lowest-id — **zero
Rng**, so adding the field did not move the verify baseline (held EXACTLY 884/699/567/706;
6–14 site-anchored events/seed over 120 yrs). The `sites` gate proves SiteId equals the
convention table for EVERY event (the old absence-assertion, inverted into a presence proof).

## Battle Sites V1 — the battle contract (binding, shipped 2026-06-13)

War is no longer abstract yearly attrition: the fighting of a war is recorded as **battles at
real places**, and the baseline moved deliberately for the first time since M8.

- **The front.** `World.FrontRegion(fa, fb)` resolves the border region a war is fought over: a
  region held by one combatant whose land touches the other's, preferring one carrying a
  stronghold (hill fort → watch post → river ford — the defensible ground), then lowest id. Null
  when the two hold no adjacent land — the war has no fixed front and its battles are placeless
  raids, honestly. It is a pure read over current control + the fixed adjacency graph: **zero Rng.**
- **The battle event.** In `ProcessWars`, a `battle` event is recorded LAZILY — the first time
  blood is drawn in a war-year (so a standoff year records none; the chronicle never invents a
  fight that did not happen). It anchors to the front (RegionId) and its stronghold (SiteId, via
  `SiteAnchors.Expected` extended to cover `war`/`battle`), names both peoples' leaders, and is
  caused by the war's declaration. The war's casualties — the **same `Rng.RandInt(0,2)` + `Pick`
  rolls the war already made** — now cause-link to the battle ("dies in the fighting"). A per-war
  tally (battles fought, fallen) feeds the ordinal naming and the peace toll.
- **The determinism keystone.** `FrontRegion` and `RecordBattle` draw **no Rng**, so the stream
  stays byte-identical and population balance is provably preserved. `verify` moved by EXACTLY
  the battle-event count: **884/699/567/706 → 894/705/574/715** (+10/+6/+7/+9 battles per seed at
  120 yr). The `sites` gate proves this non-vacuously (32 battles / 22 site-anchored across the
  suite) — the same discipline that let `Event.SiteId` ship without moving the baseline.
- **War + peace framing.** The war declaration now anchors to its front (RegionId+SiteId) and
  carries leaders, so war-pivots pin the map. Peace carries both leaders + the toll ("After 2
  battles and 3 souls fallen, … make peace, though the grudge lingers") — the faction
  attribution the recaps needed to close a chapter on a war's end. Peace stays **placeless** (no
  treaty site is modeled).
- **Anchor channels still never mix.** A battle is PLACED (SiteId/RegionId on the front); its
  dead are REMEMBERED at home (the death events keep HomeRegionId, never the battle's ground).
  Battles never carry HomeRegionId; war/battle SiteId is never set without RegionId.
- **Not a turning point.** Battles are deliberately NOT a turning-point kind (only war/peace/land
  pivots are) — a far-reaching battle still surfaces through the existing ≥4-consequence
  fallback, so `Replay.TurningPointKind` and the replay gate were untouched.
- **Echo.** `Echoes.DetectFieldOfBones` — the first echo keyed on a place (`Event.SiteId`): a
  single site that saw ≥3 battles across the wars of the age ("a field of bones").
- **Now shipped (was deferred here):** per-region economy — see "Harvest Economy V1" below.

## Harvest Economy V1 — the harvest contract (binding, shipped 2026-06-13)

Famine and plenty belong to **the land**, not an abstract faction number. The economy's ground
truth moved from `Faction.Prosperity` to a per-`Region` `Harvest`, and the baseline moved
deliberately for the second time since M8 (Battle Sites was zero-Rng; this adds real draws).

- **Harvest is the ground truth.** Each `Region` carries `Harvest` (0..2, neutral 1.0) and walks
  every tick in list order (id == index, deterministic): `Harvest += RandInt(-1,1)*step + revert`.
  God-hand protect/doom biases the **holder's** lands — additive on the SAME draw while the window
  holds, never an extra draw (inert without player acts). **This is the new RNG.**
- **Prosperity is derived.** `DeriveProsperity(f)`: a people's `Prosperity` = the **mean** of its
  controlled regions' `Harvest`; `InFamine` = its **worst** controlled region starves;
  `FamineEvent` = that region's onset event; `InBoom` = **any** controlled region feasts. A
  **landless** people holds neutral 1.0 and never famines. Derivation runs after the region walk;
  trade lifts the trading peoples' lands and re-derives the two of them at once (no Rng), so
  end-of-tick `Prosperity` equals the current controlled-region mean **exactly**. Births, culture,
  trade, and famine death pressure read `f.Prosperity`/`f.InFamine`/`f.InBoom` **unchanged** — only
  the source moved to the land.
- **Land-anchored events.** Only a **held** region emits, anchored to **RegionId**: `famine` onset
  ("Famine grips {region}", keeps doom/protect divine cause-links), `famine_end` ("The land
  recovers; the famine in {region} breaks" — a real chapter-closing beat, cause-linked to the
  onset it answers), and `boom`/"plenty continues". Wilderness harvest walks **silently**.
- **SiteId never leaks.** A famine spans a region, it isn't *at* one site, so `famine`/`boom`/
  `famine_end` carry **no SiteId** — `SiteAnchors.Expected` is deliberately NOT extended, and both
  the `harvest` and `sites` gates prove the convention agrees (Expected == null for all three).
- **Anchor channels still never mix.** Economy events are PLACED (RegionId, no HomeRegionId). A
  famine death cause-links to the region's famine event but the death stays REMEMBERED at home
  (`HomeRegionId` set, `RegionId == null`). Four channels: SiteId / RegionId / HomeRegionId / null.
- **Read-models.** `StoryGrammar` `famine-breaks` (`famine_end` ← `famine`, therefore);
  `Scoring` `famine_end`=35 / `recovery` tag=10; **The Barren Years** echo — one land that starved
  ≥3 times in a single age (≤25-year gaps), the first famine echo keyed on `Event.RegionId`,
  clustered like The Long Famine.
- **Gate `harvest`** proves, per faction + event: derivation (Prosperity == mean; rollups exact),
  landless neutrality, land-anchoring with valid RegionId, no-SiteId-leakage (incl. the convention
  table), `famine_end` answers an earlier famine in the SAME region, life-event channel honesty,
  and double-run determinism of harvest state. Non-vacuous: 264 land-anchored economy events / 45
  recoveries / 3 landless checks across the 120-yr suite.
- **The baseline move (documented before/after).** `verify` **894/705/574/715 → 823/559/910/632**
  (Δ −71/−146/+336/−83, seeds 1/18/42/7) — the stream moves in BOTH directions per seed because
  per-region draws + faction-mean prosperity reshape births/trade/war RNG. Determinism holds
  (byte-identical double run). Balance preserved with **no tuning** — 5000-yr living
  `168/157/306/150`, all stable, no extinction (`carrying_capacity` stays 300). All NINE gates green.
- **Changed assumptions.** `Faction.LastBoomYear` removed (region owns boom-beat timing).
  `Faction.InFamine`/`InBoom`/`FamineEvent` are now **derived caches** recomputed each tick, not
  independent state. Trade now lifts harvest (the ground truth) rather than prosperity directly.

## Chronicle Replay V2 — the replay contract (binding, shipped 2026-06-12)

`Replay.ChainFor(world, eventId)` is a pure, deterministic, Rng-free read-model: the focal
event's annotated cause chain (record order, via StoryGrammar) plus a bounded direct-
consequence rail (cap 8; the real total is reported even past the cap; each consequence's
connector names the literal focal→consequence edge, proven by the grammar). Every
`ReplayBeat` copies RegionId/SiteId/HomeRegionId VERBATIM — never inferred, never substituted
— and carries a Status that names exactly what the anchors allow: **site-anchored** (true
SiteId), **region-only** (RegionId, no place), **memory-only** (HomeRegionId — remembered,
NOT where it happened; the viewer must never pin it), **unanchored** (placeless — rail/
timeline only). The viewer's replay overlay marks ONLY honestly anchored beats on the map,
along real recorded cause edges (proximate-cause spine bold, real branches faint); memory-
only and unanchored beats live only in the rail and beat card — no fake pins, ever.
`Replay.TurningPointKind` is a bounded authored pivot classifier (war-pivot, peace-pivot,
land-lost, land-abandoned, violent-succession, faith-torn, faith-proclaimed, ways-hardened,
divine-influenced, far-reaching) — deterministic over (event content, consequence count),
each premise gate-checked. All replay/anchor/turning-point/known-for English lives in
`StoryCopy.cs`. The `replay` gate proves determinism, verbatim anchors, honest statuses,
bounded real consequences, the authored classifier, and survival of the world-save journal.

## Region Lens — data contracts still missing (design notes, not promises)
The viewer-side lens is honest about these; each needs a deliberate sim-side milestone because
they move the verify baseline (new RNG draws and/or new ordered iteration):
- **Person ↔ site anchoring.** People have no home site; the atlas scatter (p.Id % regions)
  is presentation only. (Person.HomeRegionId shipped; the SITE granularity did not.)
- **Event ↔ site anchoring.** SHIPPED 2026-06-12 — `Event.SiteId` via the conservative
  convention table above (foundings/abandonment → seat, war → stronghold, ways → sacred site).
  Still missing: BATTLE sites (war casualties anchor nowhere yet — needs a sim battle event),
  rumor/trade place anchors (no honest rule), and per-site population/economy.
- **Terrain geography.** Region POLYGONS are no longer needed (the WorldSurface cell bridge
  ships real landforms); what remains is making famous-shape worlds data (`maps/*.json`).

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
**Drew's F5 feel-test of Harvest Economy V1** (newest this session, sim-side — the viewer is
NOT yet wired): run a few centuries and read the chronicle — does "Famine grips {region}" /
"The land recovers; the famine in {region} breaks" / "A season of plenty blesses {region}" read
like the land itself starving and recovering? Do famine deaths still read as belonging to the
person and their home (NOT the starved region — the channel split must hold)? Over a long run,
does a single land that keeps starving feel like "the barren years"? The deferred viewer follow-up
shipped (Harvest Memory Viewer Payoff V1 + the famine-scar polish pass, commit 55862fb): `famine_end`
cards as a chapter close, The Barren Years echo, and the famine onset scar on the map (now in its own
per-region 1-slot scar store outside the 4-slot ring, larger + higher alpha floor for low-zoom
legibility). The remaining sim follow-up is terrain-typed harvest. Baseline moved deliberately to
**823/559/910/632** (from 894/705/574/715); balance held (no extinction, no tuning).

**Drew's F5 feel-test of Chronicle Replay + Site-Anchored Memory V1** (prior session):
open How We Got Here on a war or a custom-born event — does the turning-point header land
(who/peoples/place)? Press ⟲ Replay — does the dimmed-atlas retelling read as "watching the
map-table retell the past": numbered marks tracing the cause edges, the beat card + scrubber
clear, the focal beat pinned at its true place? Confirm the honesty holds: unanchored beats
(schisms, forbidden bonds) stay in the rail with NO map pin, memory-only life events say
"remembered at a home — not where it happened". Do the turning-point diamonds on the live
map feel meaningful, not spammy? Does Remembered Places (❖ places) read as the atlas's
memory, with every row's anchor named honestly ("at {site}" / "in {land}" / "remembered
in")? Does the Site Card's "known for" + site-anchored tales make a place feel remembered?
Known liveable limits: turning marks only appear for events with a true place anchor (far-
reaching pivots surface later, through the thread header — consequences aren't known at
stream time); replay marks need a SiteId or RegionId, so a chain of all-unanchored beats
draws no path (rail only); the consequence rail caps at 8 (the real total is still stated).
Then the still-unwatched **Persistence + Sites V1** feel-test (resume, site tags, Site Card):
quit mid-story, relaunch — does the resume feel like *your* world returning? Fresh world
still needs the save deleted by hand (`%APPDATA%\Godot\app_userdata\Living Myth\world_seed7.json`).

Then the previous sitting's checklist (still valid):
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
