# Living Myth — Time & Story Pacing Bible (The Four Clocks)

The implementation-facing companion to `GAME_DESIGN.md`'s "Time should serve attention"
pillar. `GAME_DESIGN.md` says what time should feel like (Dynamic Local Time, Chronicle
Playback); `docs/VISUAL_STYLE.md` says what the game looks like; this file defines **how
time actually works** — the model, the vocabulary, the audit of today's code against it,
and the staged roadmap. Future pacing/story sessions start here.

Binding constraint, restated from the honesty contract: **pacing is wall-clock
presentation only.** `Tick()` count and order are sacred; one tick is one year; viewer
pacing work must leave `verify` at its recorded baseline (884/699/567/706, seeds
1/18/42/7). Anything in this doc that needs new sim state or new RNG draws is marked
**[sim contract]** and is a deliberate, baseline-moving milestone — never a side effect.

## The problem, measured

The sim is healthy; the *presentation* of time is what loses the player. At 1× speed
(`BaseInterval` 1.2 s/yr, `Main.cs`):

| Arc | Sim duration | Wall clock at 1× | At 8× |
|---|---|---|---|
| A full human life (median death ~60–75, mortality curve `World.cs:73`) | ~70 yrs | **~84 seconds** | ~10 s |
| A generation (fertility 18–44, ~25–30 yr cadence) | ~28 yrs | ~34 s | ~4 s |
| A war (declared → fought → peace, `World.cs:1292`) | 1–2 yrs | **~2.4 s** | ~0.3 s |
| A famine arc (hysteresis-bounded) | ~3–10 yrs | ~4–12 s | ~1 s |
| The dramatic auto-slow beat (`SlowdownWindow`) | — | 1.6 s | 1.6 s |

The one dramatic device we have — a 1.6-second slowdown — is shorter than reading a
single feed row. A romance, marriage, children, betrayal, and death of a followed soul
fit inside ninety seconds. Wars begin and end between two glances at the map. The
chronicle is legible *after the fact* (catch-up works); it is not yet *followable as it
happens*. That is the gap this model closes.

What we will NOT do (anti-goals, from the design principles):

- No flat global slowdown — a world where nothing moves feels dead, and the sweep of
  civilizations is the other half of the product.
- No faking: no invented family homes, no event locations the sim didn't record, no
  prophecy threads before a prophecy system exists, no generated narrative.
- Not every event is equal — pacing devices key off the importance engine, never off
  raw event count.
- The game must not collapse into a feed reader. The map, the lens, and the chronicle
  share the storytelling.

## The model: four clocks

### 1. World Time (sim truth — never changes)

One `Tick()` == one year, thirteen fixed phases (`World.Tick`, `World.cs:891`). Sequential
event ids are the only within-year order. There is no sub-year unit and this milestone
does not add one: seasons/months would be a sim rewrite with no story payoff that Drama
Time can't deliver more cheaply. World Time is append-only memory: the chronicle, cause
links, participants, tags, and (partial) region anchors. Everything the other three
clocks do is a *projection* of World Time — never a mutation of it.

### 2. Drama Time (the shown pace — what the player watches)

The wall-clock rate and emphasis with which World Time is presented. Exists today as:
the speed ladder (0.25–16×), the dramatic auto-slow, feed-row flash, region pulses,
echo rationing, and the drama camera. Drama Time's job: **important moments take more
wall-clock and screen weight than routine ones, in both directions** — slow into drama,
accelerate through quiet years. It is global and ambient; it does not know what the
player cares about, only what the importance engine says is loud.

### 3. Focus Time (protected attention — what the player follows)

When the player follows a subject — a soul's bloodline, a people, later a region — the
game protects that thread: major events on followed subjects can pause the world, drop
a recap card, and remember what the player last saw. Focus Time is personal where Drama
Time is ambient. Exists today as the YOURS channel (weight boost + gold rows + cyan
rings) plus, since the focus-guard slice, pause-on-drama with recap cards, "you last
saw…" memory, and the followed-death card.

### 4. Chronicle Replay Time (retrospective — how the player understands)

Revisiting World Time after the fact: How We Got Here (shipped, text), turning-point
filtering (shipped as "Quick beats"), the visual replay path, and timeline scrubbing
(designed in `GAME_DESIGN.md`, unbuilt). Replay is deterministic presentation over the
recorded chronicle — never rollback, never invention. Per `GAME_DESIGN.md`: "Replay mode
should become one of the main ways players understand and emotionally attach to the
world."

The four clocks are layered, not exclusive: World Time always advances the same way;
Drama Time shapes its live rendering; Focus Time interrupts on the player's behalf;
Replay Time revisits it. A design that confuses layers (e.g. pausing the *sim* per
region, or scrubbing that *re-runs* the sim) is wrong by construction.

## Event weight bands (shared vocabulary)

The importance engine (`Scoring.cs`) already produces one number per event; the viewer
already uses four ad-hoc thresholds. This model names them as **bands**, so every pacing
device speaks the same language:

| Band | Live score (`ImportanceFast` + YOURS boost) | Today's anchor | Treatment |
|---|---|---|---|
| **Background** | < chattiness threshold (default 60) | `_chatSlider` default, `Main.cs` | Recorded, not surfaced live. Visible in catch-up Full thread and inspectors. |
| **Notable** | ≥ threshold, < 100 | the feed gate | A feed row. No drama devices. |
| **Major** | ≥ 100 (`NotableBar`), or any YOURS row | `NotableBar`, `Main.cs` | Row flash + region pulse + dramatic auto-slow; focus-guard pause when YOURS *and* ≥ the bar (a followed soul's death cards at any weight). |
| **Turning point** | echo anchor clearing the significance bar (80 on full `Importance`), or consequence-rich majors | `EchoSignificanceBar`, `Main.cs` | Echo card today; numbered beats in future replay; chapter-recap headline. |

Notes for implementers:
- Two scoring paths exist on purpose: `ImportanceFast` (streaming, incremental
  consequence counts) for live bands, full `Importance` (adds trace depth) for
  retrospective bands. Don't mix them in one comparison.
- Consequence counts grow after the fact — an event can *graduate* bands as history
  cites it. Live treatment uses the band at surfacing time; replay/recap use the
  matured band. That asymmetry is honest (it's how memory works) and we keep it.
- `gossip_min_importance` (42, sim-side) is below the Background/Notable line by
  design: the rumor mill hears things the player's feed doesn't.

## Drama Time — design

Shipped: speed ladder, auto-slow (re-armed, eased, frame-rate independent), pulses,
echo rationing, drama camera with manual-override cooldown. Direction:

1. **Name the paces.** The ladder buttons are bare multipliers; players reason in
   stories, not factors. Each speed gets a pace name and an honest meaning in lives:
   *linger* (0.25× — slow enough to follow one soul), *watch* (0.5×), *unfold* (1× —
   the chronicle's natural pace), *drift* (2×), *hasten* (4×), *sweep* (8×), *ages*
   (16× — generations pass in moments). Shipped this pass as tooltips on the existing
   buttons; candidates for labels in a later dock polish.
2. **Beats should scale with speed.** `SlowdownWindow` is 1.6 wall-clock seconds at
   every speed, so at 16× a "beat" swallows ~20 sim-years and at 0.25× it stretches
   nothing. Express the beat as *shown years held at crawl* (e.g. the year of the event
   plus one), derived from current speed — still wall-clock-only math.
3. **Accelerate the quiet years.** The inverse of auto-slow: when no Notable+ event has
   surfaced for K shown years, ease effective speed up toward the next ladder step
   (clearly indicated, instantly cancelled by any Notable+ event or input). Drama-aware
   pacing in both directions is what keeps "slow enough to care" from becoming "too slow
   to live." Off by default until feel-tested.
4. **Pulses should carry identity.** `PulseRegion(regionId)` is anonymous — the map
   can't distinguish a war from a founding, and a second pulse steals the camera
   (last-writer-wins, `MapView.cs:104`). Pass the event class color/glyph into the
   pulse, and queue camera leans (dwell ≥ the beat) instead of overwriting.

## Focus Time — design

The followable subjects, with honesty status:

| Subject | Status | Basis |
|---|---|---|
| **Soul / bloodline** | SHIPPED (verbs split in Guard V2) | Two verbs on the person inspector: *Follow this soul* — one person, a per-soul set never expanded into kin — and *Follow this bloodline* — directed lineage (`Feed.Bloodline`), viral growth at birth (`Main.cs`, `StreamNewHeadlines`) |
| **People (faction)** | SHIPPED | `Follow` on faction inspector; `_markedFactions` |
| **Region** | Viewer-ready | `RegionActivity` already indexes anchored events; follow = YOURS treatment for events whose `RegionId` matches. Coverage caveat: only territory/culture/rumor events carry `RegionId` today, so a followed region is honest but quiet until the anchoring **[sim contract]** lands. Say so in the lens, like the existing not-modeled notes. |
| **Faith** | Needs audit | `ReligionId` on people and religion lineage exist; most faith events name participants, not the faith. Audit event coverage before promising it. |
| **Prophecy** | **FORBIDDEN until modeled** | The sim has prophet *events*, not prophecies-as-promises (no fulfillment conditions, no open/closed state). A followed prophecy before that system exists would be fake. **[sim contract]** |

Mechanics, in dependency order:

1. **Pause on drama (the focus guard) — SHIPPED.** A toggle beside the drama toggle,
   three states: *off / ★ followed / all*. In *followed*, when a Major+ YOURS event
   surfaces (or a followed soul dies — that triggers below the chattiness threshold
   too, because a follow is an explicit ask), the tick finishes, the world pauses, and
   a gold-bordered card shows the event, context, and two actions — *Resume* and *How
   We Got Here*. A death outranks a recap within the same tick. The year card shows a
   "⛨ guard watches…" signal while the guard is armed and something is followed.
   Default: *followed* — the single highest-leverage investment feature in the model.
2. **"You last saw…" memory — SHIPPED (per soul).** The viewer keeps, per followed
   person id, the last YOURS event *actually shown to the player* — a rendered feed
   row or a guard card, never a filtered or cap-displaced event (O(1) dictionary
   updates). The guard card and the person inspector honestly say: *"you last saw
   Maia: Yr 412 — wed Edda of the Shorefolk."* Still to come: the per-faction variant
   ("this people has changed: 2 regions lost since you last looked") and later per
   followed region ("this region remembers"). All chronicle-derived; no new sim state.
3. **Notifications without pause.** In *off* mode, YOURS Major+ events still deserve
   more than a gold row: a brief toast anchored to the year card ("◆ Your bloodline:
   Maia was murdered — click to pause & trace"). Quietly skippable, never modal.
4. **Death of a followed soul is a chapter beat — SHIPPED; memorial moment in Guard V2.**
   When a followed person dies, the guard shows "Their Tale Ends": born–died years,
   reputation, children, their last deeds (the existing per-person event filter, one-shot
   on card open), and the last-seen line — all real events. Guard V2 makes the *specific*
   soul's death a memorial, not an info card: the atlas dims behind a larger ceremonial
   frame (3px gold border, gold rule, event-class medallion), the name stands centered
   with faction and years lived, and the lead reads "the world holds its breath." A
   bloodline-only death keeps the standard card and says so ("a tale of a bloodline you
   follow closes") — the card language always matches what the player actually followed.
   A soul death outranks a bloodline death, which outranks a recap, within one tick. No
   recorded region → a gentle "no place recorded" line, and the map pulses only a real
   region. This is the emotional payoff loop for following, and it costs nothing the
   chronicle doesn't already know.

## Chapter recaps (defining "era recap" at last)

"Timeline scrubbing + era recap" has been deferred in PROJECT_STATE.md with no design
behind "era recap" (before this doc, the phrase appeared exactly once in the repo). Definition:

- **A chapter is a span of *shown* years, not sim years** — it belongs to Drama/Focus
  Time, not World Time. Default: 25 shown years (≈ one generation) *or* an arc closure
  involving a followed subject (war's peace, famine's end, an echo carding), whichever
  comes first. Chapters are a presentation rhythm; the sim has no chapter state.
- **A chapter recap is a card, not a cinematic:** chapter span ("Years 380–405"), the
  top 3 events by matured importance in the span, followed-subject deltas (bloodline
  births/deaths, reputation shifts, regions gained/lost), and any echo carded. Every
  line links into catch-up. All of it derives from the chronicle slice already streamed
  — the recap aggregator must stay O(new events), never a history scan.
- Recaps queue politely: shown at the next pause or chapter boundary, never interrupting
  a focus-guard card.

## Chronicle Replay Time — design

Shipped: catch-up modal (Quick beats / Full thread), region anchors shown when known
("· in Greymoor"), echo cards opening on their anchor event. Direction, in order:

1. **Turning-point numbering in catch-up.** Quick beats already filters to the spine;
   number the beats (1→N) and show each beat's band. Pure modal rendering.
2. **The visual replay path** (VISUAL_STYLE roadmap 4): draw the causal chain over the
   atlas for events that carry `RegionId`; events without one stay in the side list —
   never invent a location. Numbered beats, restrained glow, per the north-star
   reference's honest subset.
3. **Timeline scrubbing** — mostly derivable today, one audit hole. Scrubbing shows the
   world-state at year Y as a *reconstruction*: living set from `BirthYear`/`DeathYear`,
   territory by replaying founding/seizure/abandonment events (all carry `RegionId`),
   dot positions deterministic from ids. Known hole: historical faction membership
   (`Person.FactionId` is current-state; audit whether anyone ever switches factions
   before promising historical faction colors). Scrub is read-only over the chronicle —
   the sim never reverses.
4. **Alternate-path notes: FORBIDDEN.** The north-star replay image shows an
   "alternate-path ghost." The sim records what happened, not what nearly happened
   (averted wars are tension that decayed silently; spared lives are death rolls that
   missed). Until the sim *records* near-miss candidates as first-class data
   **[sim contract — and a philosophically deliberate one]**, replay shows one history,
   honestly.

## Audit: today's code vs the model

| Model piece | Today | Gap |
|---|---|---|
| World Time | `World.Tick` yearly, deterministic, append-only chronicle with causes/participants/tags/partial regions | None — this layer is done and load-bearing. |
| Drama Time: ladder | 0.25–16× buttons, `BaseInterval` 1.2 | Unnamed paces (tooltips shipped this pass); beats don't scale with speed; no quiet-years acceleration. |
| Drama Time: beats | Auto-slow 1.6 s → 0.15×, re-armed; pulse 1.2 s; camera lean ≤ pulse life, last-writer-wins | Beat too short to read; pulses anonymous; camera steals/abandons. |
| Drama Time: rationing | Chattiness slider; YOURS cap 60% of window; echo cooldown/bar/cap | Healthy. Bands formalize what these already do. |
| Focus Time: follow | Soul + bloodline + faction follow (soul = per-person set, gold rings; bloodline = viral growth, cyan rings), YOURS boost +70; focus guard (pause off/★/all, recap + death cards, soul-death memorial card, per-soul last-seen, year-card signal with soul/bloodline counts) | No region/faith follow; no per-faction last-seen deltas; no toasts in *off* mode; one memorial per tick. |
| Chapter recaps | Nothing (undesigned until this doc) | Aggregator + card; arc-closure detection for followed subjects. |
| Replay Time | Catch-up modal Quick/Full; region line when anchored | No beat numbering, no visual path, no scrub; alternate paths impossible (correctly absent). |
| Subjects the sim can't support yet | — | Region-anchored personal events, person homes, sites, prophecies, near-misses: all **[sim contract]**, below. |

## Near-term safe slices (viewer-only, baseline-safe, in order)

1. **Pace-tier tooltips + doc anchor** — SHIPPED this pass (`Main.cs`). Validates the
   vocabulary at zero risk.
2. **Focus guard slice** — SHIPPED: pause-on-drama toggle (off/★ followed/all) + guard
   recap card + per-soul "you last saw" memory (card + inspector) + "Their Tale Ends"
   followed-death card + year-card guard signal. One pass in `Main.cs`, parchment
   panels and the existing stream loop reused. **Guard V2 — SHIPPED:** specific-soul
   follow (distinct verb, never expanded into kin; gold soul rings, souls counted in
   the year-card signal) + the memorial death card (dim backdrop, ceremonial frame,
   honest place/kin lines). Remaining from this slice: toasts in *off* mode,
   per-faction last-seen deltas, multiple followed deaths in one tick, richer
   relationship cards / family tree / portrait-token treatments, region/home/site
   anchoring (the **[sim contract]** below).
3. **Chapter recap slice:** the 25-shown-year aggregator + recap card + arc-closure
   triggers. Subsumes the deferred "era recap."
4. **Followed regions (viewer-only):** Follow on the Region Lens; YOURS treatment for
   region-anchored events; honest "quiet until anchored" copy.
5. **Drama polish:** speed-scaled beats, identity-carrying pulses, camera dwell queue,
   quiet-years acceleration (off by default until feel-tested).
6. **Replay step 1:** numbered turning points in catch-up; then the visual path per
   VISUAL_STYLE roadmap 4.

Each slice ends the same way: `dotnet build LivingMyth.slnx` clean, `verify` at
884/699/567/706, Godot build clean, F5 feel-check.

## Later sim/data contracts (each moves the verify baseline; deliberate milestones)

1. **Event.RegionId coverage** for personal events (birth/death/murder/marriage/romance/
   justice/war/peace/famine/boom/trade/prophet/schism/friction/divine) — derived from
   data the sim already has (participants' faction primary region), so it *may* be
   RNG-neutral; verify must prove it. Unlocks: followed regions that actually speak,
   personal-event pulses, replay paths for personal arcs.
2. **Person.HomeRegionId** assigned deterministically at birth/migration. Unlocks honest
   "where" for lives, and the atlas scatter stops being fiction.
3. **Settlement/site contract** (GAME_DESIGN.md slice 3): 3–7 deterministic sites per
   region, then optional `Event.SiteId`. Unlocks site memory, local view, Dynamic Local
   Time's deepest layer.
4. **Prophecy/omen engine:** prophecies as first-class promises (origin event,
   conditions, open/fulfilled/failed state, fulfillment cause-links). A new tick phase —
   placement moves RNG consumption, so it is a deliberate baseline event. Unlocks
   Followed Prophecies and the strongest possible Focus Time thread ("the omen you
   marked has come true").
5. **Near-miss records** (far later, only if we want alternate-path ghosts): record
   averted wars / spared executions as low-weight shadow events. Philosophically heavy —
   decide deliberately, not as a replay convenience.

Dynamic Local Time (GAME_DESIGN.md): the region/site local-time multipliers slot into
this model as *Drama Time scoped by lens* — same effective-speed math, gated on the open
lens and its active events. No new clock needed; build it after the focus guard proves
the interaction language.

## Principles (restated, binding)

- Pacing and camera are wall-clock presentation only; `Tick()` count and order are
  sacred; viewer passes leave `verify` untouched.
- Never fake: no invented homes, locations, prophecies, alternate paths, or narrative.
  Honest gaps are stated in-UI (the Region Lens pattern).
- Not every event is equal: every pacing device keys off the bands.
- Followed threads and drama-aware pacing over any flat global slowdown.
- The sim never slows, reverses, or forks for presentation's sake.
- No generative AI anywhere in the loop.
