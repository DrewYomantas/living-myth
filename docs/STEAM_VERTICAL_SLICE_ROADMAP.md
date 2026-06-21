# Steam Vertical Slice Roadmap

_Written 2026-06-21. Repo-grounded triage, not a vision doc._

This is the **shipping lens**: what does it take to put a small, sellable, demo-able slice
of Living Myth in front of players on Steam — and what work, however good, is not that.

It is deliberately ruthless. `living-myth-roadmap.md` is the dependency-ordered build plan;
`GAME_DESIGN.md`/`DESIGN.md` are the vision; `PROJECT_STATE.md` is the detailed log. This file
exists to answer one question: **of everything we've built and everything we could build, what
is on the critical path to a first playable demo, and what is a trap?**

## The brutal one-paragraph summary

The sim is years ahead of the product. We have six natural/social forces, thirteen myth echoes,
a full causal chronicle, a canon-authoring desk, persistence, and a god-hand — all deterministic
and gate-green (598/751/809/1065). What we do **not** have is a build anyone *wants to touch*. The
vertical slice does not need more depth. It needs the depth we have to become a **toy you poke**,
not a dataset you audit.

## Feel-test findings (2026-06-21, Drew)

The feel-test happened. Verdict: _"there's obviously a game here, but not an interactive one. I
watch these little dots and am shown mountains of data about these dots, but none of it inspires me
to interact — actually the opposite. If I follow a dot I get more info on top of the other info."_

Diagnosis: **the game is a reader, not a toy.** Three concrete failure modes:

1. **It pushes data; it doesn't invite action.** Always-on feed + panels shove information at the
   player. A toy keeps a near-empty screen and surfaces info *on demand* when you reach for it.
2. **The hand has no visible bite.** The god-hand is multiplier-only and subtle by design — honest,
   but nothing reacts to you in under a second, so there is no poke→react→poke loop and no reason to
   touch anything. (WorldBox lightning is instant and visceral; that's the missing feel.)
3. **Following a dot stacks a panel instead of becoming a focus.** The UI only ever *adds* info; a
   toy *subtracts* to focus you on one thing. So following makes it worse, not better.

**The fidelity hypothesis was already tested and came back negative.** This same playtest signal drove
the earlier Blender/Krita (~7/10) and Unreal visual pivots — yet Drew's own F5 verdict on the
production viewer *after* that work was that it "still feels like the same game" (the reason the
Greymarket prototype was built at all). Prettier dots are still dots you watch. The missing variable
is **aliveness + agency, not pixels.**

---

## 1. Already shipped (the asset base)

All committed, pushed, and gate-green. This is what the slice draws from — not work to redo.

**Simulation (standalone C# lib, 13 console gates green, verify 598/751/809/1065):**
- Core engines: family, government/succession, crime + revenge feuds, war, religion
  (prophets/schisms/conversion/persecution), economy.
- Natural & social forces: per-region **Harvest** economy, **Disease & Plague** (+ contagion-chain
  "Creeping Death"), **Migration** (flight & settlement), **Prejudice** (origin scapegoating),
  terrain-typed harvest, battle sites.
- Story layer: causal Chronicle (`Event.Causes`), StoryGrammar connectors (therefore/but/unresolved),
  truth model (Recorded Fact / Causal Claim / Player Telling / Mechanical Truth), **13 myth echoes**,
  importance-ranked feed.
- Spatial: `WorldSurface` editable cell grid, `Sites` (3–7 named sites/region), `Event.SiteId`
  anchoring via one authored convention table, four non-mixing anchor channels.
- God-hand: DivinePressure ledger, 7 verbs (Bless/Curse/Protect/Doom/Omen/SeedForest/CallSpring),
  multiplier-only (baseline-inert by construction), Fate Ledger.
- Persistence: world save as an **input journal** (replay-to-resume, deterministic).

**Viewer (Godot 4.6 mono):**
- Map/atlas render, time controls + dramatic auto-slow, live ranked feed (Yours/Loud/Rising).
- Inspectors (person/faction/region), the Cast panel + sigils, Map-First panel economy
  (Watch/Inspect/Chronicle).
- Focus guard (pause-on-drama) + memorial cards, chapter recaps, causal catch-up, Chronicle Replay
  on a dimmed atlas with turning-point marks.
- Canon writing desk, Remembered Places, Site Cards, Watcher's Guide onboarding card.
- Viewer payoffs for harvest/plague/migration/prejudice (scars, lens sections, filter chips, echo marks).
- Seed picker + in-app New World, painted shoreline.

**Shippability infra (scaffolded, not yet exercised on Drew's machine):**
- `godot/export_presets.cfg`, `tools/build/{build,stamp-version,feeltest}.ps1`, `VERSION`,
  `FEELTEST_CHECKLIST.md`, `godot/Version.cs`, `.github/workflows/ci.yml`, `dist/` gitignored.

---

## 2. Needed for first playable demo (the critical path)

Small list on purpose. Everything here is "the slice is not shippable without it."

1. **The F5 feel-test — actually sit and play.** (P0, blocks everything)
   Run `tools/build/feeltest.ps1`, follow a soul and a land through ~2 centuries across the 8 systems.
   This is a gate, not a chore: it tells us what's boring, what's illegible, and what's broken at
   play speed. Every tuning decision below is guessing until this happens. _It has been the "next
   step" for 9 days and has not been done. Do it first._

2. **Define the slice scope.** (P0, one document)
   What IS the demo? Proposed: a fixed (or curated-pick) seed, a bounded run length, a clear 15-minute
   hook (watch history rise → follow a soul → use the hand once → see a myth echo fire). Decide what's
   IN (the watch + light-touch god-hand loop) and what's explicitly OUT (canon desk? multiple save
   slots? all 7 verbs?). Without this, "demo" is undefined and scope creeps forever.

3. **First-run onboarding that lands in <60s.** (P1)
   The Watcher's Guide exists; the feel-test will say whether a cold player understands what they're
   looking at and what they can do. Likely needs a guided "first myth" moment, not just a legend card.

4. **God-hand feel pass.** (P1)
   The hand is mechanically honest but subtle by design. A demo needs at least one verb that feels
   *good* to use and visibly pays off. Tune the curse/bless feedback loop; the rest can stay.

5. **The Windows build actually works.** (P1, on Drew's machine)
   Install Godot 4.6.3-mono export templates → `tools/build/build.ps1` → smoke-test that `data/*.json`
   resolves beside the exe → launch the exported build clean. Infra is scaffolded but never run.

6. **Steam page minimum.** (P2, parallelizable, non-code)
   Capsule art, 4–6 screenshots from the real viewer, a 30–60s trailer (screen-capture of a rising
   myth), store copy ("WorldBox at the fingertips, deeper in the chronicle"), and the AI-disclosure
   line per the AI Use Doctrine. This can run alongside code work.

That's it. Six items. Notice none of them are new sim forces or new renderers.

---

## 3. Later (real, but post-demo)

Good work, correctly sequenced after the slice ships. From `living-myth-roadmap.md`:
- Timeline scrubbing (Phase 0 leftover).
- Person↔site home anchoring; per-event richer anchoring.
- Blessing depth, Prophecy-as-promise (Myth Authorship V2), taboo/bloodline-seeding.
- Hybrid peoples / mixed settlements surfaced from the region layer.
- Faith panel, rename system, more echo packs.
- Map editor + world templates, shareable seeds, creator/Workshop features.
- A dedicated Region Lens *view* (today it's a panel, not a view).
- Creeping-Death "spreading front" map trail (viewer payoff for the newest echo).

---

## 4. Visuals — split into two kinds (the feel-test sharpened this)

The feel-test corrects the earlier framing that lumped all visuals as distraction. There are two
very different kinds of visual work, and the playtest tells us which one matters:

**Visuals that create aliveness + agency → CORE, on the critical path.** Dots-with-data genuinely
does not read as a world, and fixing that *is* visual/presentation work. This means: entities that
move and react (render the migration/war/famine the sim already computes as *motion*), the hand
landing visibly and immediately, scene-not-dashboard composition, pull-not-push information. This is
the "the sim should be seen before it is read" doctrine, scoped around **agency** rather than polish.

**Asset fidelity → still off the critical path.** Pretty per-asset rendering and a second engine that
makes non-interactive snapshots do not fix any of the three feel-test failure modes — and we have
direct evidence (the "still feels like the same game" verdict) that they don't move felt experience:
- The Godot `DioramaView` overlay — judged 5/10, gap is art labor.
- The Blender `render_diorama.py` + headless Krita paintover — judged ~7.0/10, gains are content.
- The North Star Greymarket prototype (`PrototypeGreymarket.tscn`) — standalone look-target, ~7/10.

**Decision rule:** spend on aliveness/agency presentation now; cap asset-fidelity spend until a slice
*feels* like a toy. The bottleneck is not prettiness; it's that nothing reacts to the player.

---

## 5. Dangerous distraction (stop or strictly time-box)

This is the section that matters most for a solo dev.

- **The entire Unreal Engine 5.8 track** (`unreal/LivingMythDiorama/`, the V2→V5.2 render passes,
  the snapshot bridge consumer). It is a **parallel renderer with zero gameplay** that produces
  *static snapshots* — it cannot fix any of the three feel-test failure modes (push-not-pull,
  no visible hand, stacking panels), because none of those are about fidelity. The feel-test is the
  case *against* this track, not for it: the felt gap is aliveness + agency, which a snapshot renderer
  structurally cannot provide. **Freeze it.** Revisit only after a slice *feels* like a toy and a
  Steam demo is live. (Note: this is the trap the project fell into twice — "feel gap → make it
  pretty in another engine." The output was 7/10 renders and "still feels like the same game.")

- **Adding more sim forces.** We have six natural/social forces and the world is already richer than
  the viewer surfaces. A seventh force does not make the demo more sellable — it makes it longer to
  tune and harder to onboard. The era arc / species / expressive agents (Phase 4–5) are explicitly
  post-demo. **No new `Rng`-consuming engines until the slice ships.**

- **Endless Blender/Krita fidelity micro-passes.** "Fix the cairn snowman read," "Krita-ink the
  ground swatches," "Route A house render judge" — each is a 6.8→7.0 nudge on art that isn't in the
  shipping renderer. Diminishing returns on a non-critical-path asset. Batch or shelve.

- **Re-litigating the visual thesis.** The look is locked (DESIGN.md / VISUAL_STYLE.md: stylized
  semi-realistic fantasy pixel diorama as a living atlas). Stop shopping for a new visual identity;
  ship the one we have.

---

## The next milestone

**"Toy Feel" slice — make a small piece of the world something you want to poke, in Godot.**

The feel-test gave us the real target: not "is it pretty," but "do I want to touch it." Build the
cheapest possible test of the aliveness + agency hypothesis, in the ship engine (Godot — so it's the
first real step of the actual game, not throwaway like Unreal). Four moves, all over existing sim state:

1. **Make the dots move.** Render the migration/war/famine/plague the sim already computes as *motion*
   — clusters drift, peoples flee a starving land, war-bands march. See the story instead of reading it.
2. **Give the hand a visible, immediate bite.** Click curse → a dark pulse lands *there, now*, the
   target visibly reacts, the consequence telegraphs — felt in <1 second, within the honesty rules.
3. **Pull, don't push.** Default to a near-empty screen. Silence the always-on feed firehose. Surface
   at most one "something's happening here" beat the player can *choose* to look at.
4. **Following replaces, not stacks.** Follow a soul → the view focuses to that life's situation, one
   thing, the rest recedes.

Then **feel-test that one slice.** Decision gate:
- If it suddenly invites poking → we've found the real game and saved months of re-platforming.
  Proceed to scope the demo (`docs/VERTICAL_SLICE_SPEC.md`) around it.
- If it still feels dead → the bigger embodied-visual/agent bet is now *earned* and de-risked,
  because we ruled out the cheap fix first.

Rationale: this tests the cheap hypothesis (aliveness + agency) before betting the farm on the
expensive one (a full visual/agent engine) — the expensive bet already came back "same game" once, so
spend the days to learn before spending the months. Viewer-only; the verify baseline must hold at
598/751/809/1065 (the sim is not the problem).

### Verdict (2026-06-21): Toy Feel = still dead → pivot

The Toy Feel slice was built and F5'd. Verdict: still not interactive — _"too fast to react even at
0.5×; pinged/Saga'd without following anyone; still info overload, not enough feeling like I'm doing
anything."_ The deeper root: every fix so far **decorated a spectator seat**. Living Myth runs on an
autonomous wall-clock and the player watches; WorldBox feels like a toy because the player's input is
the *foreground* and the sim is *background* — Living Myth is the inverse.

**Decision (Drew):** commit to the WorldBox core model — **"the hand is the game"**: real-time, but
the player is always acting; the sim runs quiet in the background. (Rejected: stop-the-clock
player-paced; the big embodied-scene bet — held in reserve.)

### The new next milestone — "The Hand Is the Game" (first cut BUILT 2026-06-21, awaiting F5)

Foreground the hand, background the sim. Three load-bearing parts (the "pinged" and "too fast"
complaints stay valid in this model unless fixed deliberately):
1. **Quiet the world** — guard cards default OFF; autonomous churn no longer flashes the Saga chip or
   pulses the map. Only *your* threads and *your* pokes draw the eye.
2. **Calm the tempo** — default 0.5× over a longer base interval; the background never outruns you.
3. **A real power tray, every poke visible** — themed region blooms on all land verbs + **Smite**, a
   new instant-kill verb (draw-free, journaled, baseline-safe) — the destructive verb the hand lacked.

Built viewer-side + one sim verb; verify held 598/751/809/1065, all 13 gates green. **Next F5
question:** does foregrounding the hand + a quiet background feel like *playing*? If yes → expand the
palette (plague-touch / bounty / spawn / raise-terrain, each a draw-free state-set like `SeedForest`)
and scope the demo. If no → escalate to the embodied-scene bet.
