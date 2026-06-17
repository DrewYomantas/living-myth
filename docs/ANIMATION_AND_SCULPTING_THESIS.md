# Living Myth — Animation & Sculpting Interaction Thesis V1

A **planning document only.** Nothing here is implemented. This is the design target for how
Living Myth's world should eventually *move, respond, and assemble itself* under the player's
hand — the interaction-and-animation companion to `VISUAL_STYLE.md` (the static look) and
`TIME_AND_STORY_PACING.md` (how time is shown). Future Claude Code sessions doing animation or
sculpting-interaction work start here.

Status: **thesis, not shipped.** No animation system, no sculpting verbs, and no prototype have
been built from this doc. When a slice is built, it is recorded in `PROJECT_STATE.md` and
`.claude/CLAUDE.md` at that time — never claimed here in advance.

---

## 0. Reference framing (what we are and aren't borrowing)

The felt target is the *adaptive-construction joy* of games like **Tiny Glade** and
**Townscaper**: you make one simple gesture and the world intelligently, beautifully adapts —
walls find their corners, roofs settle, paths thread themselves, everything responds with
authored procedural grace. That **interaction feel** is the reference.

This is **not** a request to copy those games, their art, their assets, or their tone. We borrow
only four interaction/animation properties, then translate them into a **serious mythic god-sim
language**:

1. **Simple player input** — one gesture, not a CAD panel.
2. **Authored procedural adaptation** — the world fills in the consequences by deterministic rule.
3. **Satisfying visual response** — the gesture is rewarded with motion and settling.
4. **Elements growing, settling, unfolding, adapting** — never instant pop-in, never static.

Where Tiny Glade is cozy and whimsical, Living Myth is **weighty, mythic, and consequential**.
The player is a god nudging a living chronicle, not a hobbyist decorating a garden. Flourish
serves awe and meaning, not cuteness.

---

## Reference Boundary: Tiny Glade / Townscaper

A hard line, stated up front so the reference can never drift into imitation:

- **Tiny Glade and Townscaper are interaction references — not visual targets and not genre
  targets.** They are cited for *how input feels*, nothing else.
- What we borrow is one thing: **the feeling of simple input causing satisfying, authored
  procedural adaptation.**
- **Do not copy** their cozy tone, their castle-builder / town-decorator scope, their assets, their
  UI, or their consequence-free sandbox premise.
- Living Myth's version must stay **mythic, serious, deterministic, story-aware, and tied to sim
  truth.** The look stays the stylized semi-realistic fantasy diorama of `VISUAL_STYLE.md`; the
  scope stays a god-sim over a living chronicle.
- **The player is not simply decorating. The player is applying divine pressure to a living world.**
  Every gesture has weight and, where it changes the world, consequence.
- Every sculpt / build response must preserve the **no-generative-AI rule** and **must not fabricate
  story facts** for the sake of a nicer frame — visuals read from sim truth, never author it.

---

## 1. Core animation thesis

> **The player is not using a sterile map editor. The player is nudging a living myth diorama.**

Every world element the player can touch or that the sim can change — terrain, buildings, roads,
groves, shrines, ruins, banners, fires, people, and sites — should respond with **authored,
deterministic flourish**: a short, legible, hand-tuned transition that says *the world felt that*.

Consequences of this thesis:

- **No instant state swaps in the player's view.** A change the player or sim makes is shown as a
  transition, not a teleport. (The *sim* still ticks discretely and deterministically; the
  **viewer** interpolates the visible result — see §6.)
- **The diorama is alive even at rest.** Idle ambient motion (smoke drift, banner sway, water
  shimmer, crowd micro-motion) keeps the world from reading as a frozen board, but stays quiet
  enough that *event-driven* motion still reads as significant.
- **Flourish is a reward, not noise.** Motion is spent where it earns meaning — an edit, an event,
  a site coming to life — and withheld elsewhere. Busy-everywhere motion is the failure mode.

---

## 2. Terrain sculpting language

Future terrain editing (a god-hand brush over `WorldSurface` cells) should animate through
**layered, staged transitions** — never an instant height/material change. Each edit plays a short
authored sequence (target ~0.5–1.5s, see §7) layered base → decal → prop → ambient:

- **Grass parts, darkens, and regrows** around the edit — a brief disturbed ring that settles
  back, so the meadow reads as living ground reacting, not a repainted rectangle.
- **Dirt paths press into meadow** — the path doesn't appear, it is *trodden in*: ruts deepen,
  stray stones surface, weeds gather at the worn edge where grass meets dirt.
- **Raised ground swells** rather than snapping to a new height — the elevation rises with a soft
  overshoot-and-settle, displacing/parting the surface skin as it lifts.
- **Lowered ground sinks** and gathers shadow and wetness in the new hollow — darker, cooler,
  damper at the bottom, reading as a real depression.
- **Shorelines push foam, reeds, wet sand, and rocks outward** as water advances or recedes — the
  waterline is a moving, frothing edge, leaving a wet-sand band and reed fringe behind it.
- **Forests sprout through staged canopy growth** — saplings rise, crowns inflate in layers, the
  copse thickens into an overlapping mass (mirrors the committed forest-edge massing), rather than
  full-grown trees popping in.
- **Ruins and sacred stones settle into place** — they arrive with weight: a small drop, a puff of
  dust, a scatter of debris, then stillness, as if set down by an unseen hand.

All of the above are **authored procedural rules over surface + decal + prop layers**, keyed to the
cell(s) edited. They are visual responses to a `WorldSurface` terraform edit (which is already
journaled and version-bumped); they do not invent terrain the surface doesn't hold.

---

## 3. Building / site growth language

Future buildings and sites should **not pop in — they should assemble**, in readable stages that
echo real construction and let the player feel the place earning its existence:

1. **Foundation / ground clearing** — grass is cleared, earth is worn flat, a footprint is marked
   (this reuses the worn-earth grounding already shipped under dwellings).
2. **Posts / timbers / stone base** — the skeleton rises first: corner posts, a stone base course,
   framing.
3. **Walls / canopy / roof** — the body fills in and the roof caps it (for a grove: trunks, then
   the canopy layers; for a shrine: the arch, then the stones).
4. **Life details** — smoke begins to rise, banners unfurl, lamps and market cloth appear, a shrine
   glow kindles. These are the signals that the structure has become *inhabited*.
5. **People begin using the site only after it visually settles** — crowd behavior (§4) attaches to
   a site once its growth sequence completes, so the world reads cause-then-effect: the place exists,
   *therefore* people gather. People never animate against a half-built structure.

Each site type already has identity (`SiteType`, `SiteIndex.TypeLabel`, seat/market/shrine/etc.); the
growth sequence is **authored per type** and driven by the site's existing data, not generated.

---

## 4. People animation language

People should eventually have **tiny authored behavioral loops** — short, looping, readable poses
and paths, not a full agent simulation. Early scope is deliberately small:

- A small vocabulary of authored loops: **gather, trade, mourn, pray, flee, celebrate, march,
  watch, work.**
- **No complex simulation animation required early.** A figure plays one looping authored clip
  appropriate to its current context; transitions between clips can be simple.
- **Prioritize readable silhouettes and event-linked crowd motion.** A crowd's *collective* motion
  (drifting toward a ritual, scattering from a battle, massing at a market) carries the story far
  more than any single figure's fidelity. Silhouette legibility beats joint detail.
- **Animation must serve story clarity, not random busyness.** A figure's loop is chosen by sim
  truth — what's happening at their site/region this year — so the motion *means* something. Idle
  ambient fidget is allowed but kept under the event-driven motion in volume and contrast.

Figures are already deterministic, sigil-marked souls (`Person`, `PersonSigils`); their loops are
chosen from sim state (their faction, their site, recent events they're a participant in), never
from invented behavior.

---

## 5. Myth / event animation language

Events should leave **visible local responses** on the diorama — a short flourish anchored to where
the event belongs, drawn from the event's real data and anchor channel:

- **Murders** — a hush (ambient motion briefly stills), a dark inward pulse at the spot; a cairn /
  memorial mark where the chronicle already warrants one (memorial logic already exists).
- **Battles** — dust kicked up, banners of the warring sides, movement lines between the fronts, and
  a lasting scar at the battle site (battles are already site-anchored to a stronghold).
- **Rituals / faith** — a circular glow, candles/embers, gathered figures facing inward (the
  "pray"/"gather" loops from §4).
- **Trade / plenty** — market motion: cart and lantern movement, a shimmer along the trade road /
  path between trading sites.
- **Succession / leadership** — banners raised, crowd attention turning toward the seat, a warm
  pulse over the seat site.
- **Omen** — a sky or ground symbol, birds, an unnatural light; **attention-only by design**, exactly
  as the existing Omen god-hand verb is (a surfacing/attention weight, no mechanical payoff).

Each effect is keyed to the event's **anchor channel** — `SiteId` (the place it belongs to),
`RegionId` (where it happened), `HomeRegionId` (where it's *remembered*, never a location), or null
(unplaced). The four channels never mix in animation any more than they do in the data: a home-anchored
death is remembered at the lineage home (a memorial mote at the home seat), never animated as "dying
at" a place it isn't tied to.

---

## 6. Technical guardrails

These are **binding** for any future animation/sculpting work:

- **Keep all animation deterministic.** Same seed + same player acts ⇒ same visible sequence. No
  `Math.random()`, no wall-clock-seeded variation; any per-element variety comes from existing
  deterministic hashes (the `Hash(x,y,seed)` pattern already used for layout/jitter) or from sim ids.
  Animation *phase* may be driven by real frame time for smoothness, but the *content and outcome* of
  a transition must be a pure function of sim truth + elapsed transition time.
- **The viewer stays presentation-only.** Animation may never call a sim verb or change tick order;
  `World.Tick()` must run the same number of times in the same order regardless of animation state.
  Therefore animation **cannot move the `verify` baseline** — if it does, sim code was touched by
  accident. (Same invariant the pacing/auto-slow and diorama overlay already honor; `verify` is the
  guard.)
- **Tie animation to existing sim truth** — regions, sites, events, people, factions, roads,
  home/place memory. Animation *reads* this state; it never authors it.
- **Do not fabricate story facts for visuals.** If the sim doesn't record it, the animation doesn't
  show it. No invented battles, deaths, foundings, or people for the sake of a nicer frame. Honest
  "unknown / unplaced" is shown honestly (no fake pins — the replay overlay rule already holds this
  line).
- **Do not create static baked paintings as the runtime map.** The world stays **data-driven and
  editable** — assembled from the surface grid, sites, props, and event effects at draw time. A
  concept-art image is a *reference* (per the Visual references README), never the shipped map.
- **Favor chunk / layer refreshes.** Compose the frame as independently-refreshed layers so a change
  redraws only what moved:
  - **terrain base** (the surface skin — rebuilt only on terraform/territory change, as today),
  - **decals** (paths, wear, scars, foam, wet sand),
  - **props** (buildings, trees, stones, banners),
  - **people** (the authored loops),
  - **event effects** (the §5 flourishes),
  - **UI overlays** (parchment chrome, marks, cards).
  A terrain edit refreshes base+decals near the edit; an event spawns an effect on the effects layer;
  the chrome never redraws because a tree swayed. Keep hot paths O(touched), never O(history) — the
  standing performance rule.
- **Use authored procedural rules and asset libraries, not generative AI.** Runtime storytelling and
  content remain **no-generative-AI**. Animation is authored curves, staged sequences, and the
  existing deterministic asset library (Blender → optional Krita paintover → billboard PNGs).
- **God-hand edits journal through the existing seam.** When sculpting becomes a real *act* (not just
  a viewer flourish), it goes through `DivinePressure` / the `PlayerWorld` input journal like every
  other god-hand verb, so a player-shaped world still replays byte-identically. A purely cosmetic
  preview that touches no sim state needs no journal — but the moment it changes the world, it is a
  journaled act, full stop.

---

## 7. Future-proof slice proposal — **Sculpt Response Prototype V1**

A later, deliberately tiny prototype to prove the §1–§2 feel **without touching sim truth**. Not to
be built until explicitly requested; defined here so the first slice is already scoped.

**Goal:** prove that one simple brush gesture produces a satisfying, staged, deterministic terrain
response in an isolated scene.

**Suggested first slice:**

- In the **prototype scene only** (the standalone `PrototypeGreymarket` is the natural host — it is
  additive, wired to nothing, and already renders the layered ground/decal/prop/people stack), let
  the player **brush a dirt path or a sacred grove** onto the ground.
- **Animate the transition over ~0.5–1.5 seconds.**
- Show the **staged response**, layer by layer:
  1. **ground color blend** (meadow → dirt, or bare → grove floor),
  2. **path wear / grass edge** (ruts and edge weeds press in, per §2),
  3. **stones / props settle** (path stones, or grove trunks + staged canopy, with the small
     drop-and-dust of §2/§3),
  4. **small ambient motes / crowd attention** (a few drifting motes; nearby figures briefly turn
     to watch — the lightest taste of §4/§5).
- **Must be reversible or safely resettable** — a clear undo/reset that returns the scene to its
  pristine state, since this is a feel experiment, not a saved edit.
- **Must not touch core sim truth.** It operates on a *throwaway visual overlay* in the prototype.
  It is wired into `WorldSurface` / `DivinePressure` / the `PlayerWorld` journal **only** if and when
  it is explicitly promoted to a real god-hand action later (at which point §6's journaling guardrail
  applies in full).

**Success criteria:** the gesture feels *good* (legible, weighty, rewarding), the sequence is
identical on repeat (deterministic), the reset is clean, and `verify` is provably untouched because
no sim code was involved.

**Explicitly out of scope for V1:** real terraform persistence, multi-cell brushes, building
assembly, the full people loop vocabulary, event-effect library, and any wiring into the production
atlas/diorama. Those follow only after the single-gesture feel is proven.

---

## Open questions (to resolve before building)

- **Transition ownership:** does each animatable element own its own transition state, or does a
  central "flourish scheduler" drive them? (Leaning per-element for the prototype, central later for
  ordering multi-element sequences like §3's build stages.)
- **Catch-up / fast-forward:** when the viewer fast-forwards (resume replay, speed ladder), do
  flourishes play, compress, or skip? (Likely: skip during fast-forward, play at normal speed — the
  pacing doc's territory; reconcile there.)
- **Where the seam lives** for "cosmetic preview" vs "journaled act" so the §6 guardrail is
  mechanically enforced, not just remembered.
