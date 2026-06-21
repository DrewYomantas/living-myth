# Living Myth Sandbox — Game Design Notes

- **No runtime generative AI for storytelling or content generation inside the shipped game.** The game should not call LLMs or live AI systems during gameplay. It should use authored procedural systems: event templates, tags, conditions, cause-links, memory, and pacing. AI-assisted development and asset production are allowed under the AI Use Doctrine below.

This document captures high-level design direction that should guide future Claude Design, Figma, Claude Code, and implementation passes. It is not a sprint checklist. `PROJECT_STATE.md` remains the current milestone tracker.

## Core Pillars

- **No runtime generative AI for storytelling or content generation.** The shipped game uses authored procedural systems — event templates, tags, conditions, cause-links, memory, and pacing — and never calls a live AI during gameplay. AI-assisted *development* and asset production are allowed (see the AI Use Doctrine below).
- **Legible causality is the product.** A player should be able to ask why something happened and follow the thread through people, places, customs, rumors, faiths, and prior wounds.
- **Authored richness over infinite mush.** Prefer compact systems that combine meaningfully over unlimited text generation.
- **The world should feel lived in at multiple scales.** Island-scale events matter, but so do regions, settlements, families, shrines, roads, ruins, refugee camps, and local memory.
- **Time should serve attention.** The player should be able to watch history at god-scale, then descend into a place or event and naturally slow down enough to follow what is happening.

## Living Myth AI Use Doctrine

The final game must not rely on live generative AI during gameplay. No runtime LLM calls, no AI-written live dialogue, no AI-generated quests, no AI-authored player-facing facts.

AI tools are allowed and encouraged during development. Claude Code, ChatGPT, Blender automation, image generation, asset generation, code generation, design critique, test writing, and production tooling may all be used to help build the game.

The shipped game’s story system must remain deterministic, inspectable, and authored/procedural. Any player-facing AI-assisted assets or marketing materials must be tracked honestly for licensing, disclosure, and replacement decisions.

## Region Lens / Settlement Layer

The game should eventually support a deeper view beneath the current island/region map.

At island scale, the player sees peoples, regions, borders, wars, migrations, faith spread, culture/gossip pulses, and the rising chronicle. When the player clicks a region, they should have an option to enter a deeper **Region Lens** view. If they zoom in far enough, the game may also offer a seamless transition into this view.

Region Lens is not a city-builder. It is a story-resolution layer. Its purpose is to make places feel specific, remembered, and emotionally legible.

A region may contain:

- towns
- villages
- hamlets
- camps
- shrines
- temples
- forts
- markets
- roads or crossings
- ruins
- sacred groves
- plague pits
- refugee camps
- mixed settlements
- abandoned places

Each site can become a home for local memory: murders, rumors, plague outbreaks, migrations, hybrid families, famine, prophets, customs, wars, and old grudges.

### Scale Layers

1. **Island View**
   - Current macro view.
   - Shows regions, peoples, major event pulses, faction state, war/peace, large-scale faith/culture/gossip events, and global feed.

2. **Region View**
   - New middle layer.
   - Shows the selected region as a local constellation of settlements, shrines, roads, ruins, camps, and important sites.
   - Lets the player see who lives where, which peoples are mixed, what faiths/customs dominate, and what local wounds are active.

3. **Site Detail**
   - Inspector or vignette for a single settlement/shrine/fort/etc.
   - Shows population makeup, dominant faith, controlling faction, local customs, notable people, recent rumors, memory events, and current pressure.

### Hybrid Peoples and Local Identity

The Region Lens is the natural home for hybrid peoples.

Instead of hybridization being only an abstract faction/culture mechanic, the player should see it emerge from places:

- a border market where Shorefolk and Wood families have intermarried for generations
- a refugee camp that becomes a village
- a shrine shared by two peoples after a war
- a town ruled by one faction but populated by another
- children of a truce who eventually become named as a new people

A settlement might show population makeup like:

```text
Saltmere Town
Shorefolk: 54%
Wood Tribes: 31%
Highland Clans: 15%
```

Over time, tags or named local identities can emerge:

- Shore-Wood households
- children of the truce
- Greenfen folk
- Ashroad descendants
- Antlered Deep Mother cult
- Highland-Bay descendants

The design rule remains: species/people/culture are **seed, never destiny**. Hybrid identity should emerge from history, proximity, marriage, migration, faith, shared crisis, and memory, not from innate moral categories.

## Dynamic Local Time

Region and local detail modes should not simply pause the world by default. They should shift into a **hybrid dynamic slowed time** mode.

This is not cinematic slow motion. It is attention-aware pacing: time still moves, but at a cadence that feels natural for following an event as it plays out in a place.

At island scale, years can move quickly and the player reads broad historical movement.

At region scale, the viewer should slow enough that the player can follow local cause and effect:

- a rumor spreading from a market
- a feud crossing from one village to another
- refugees arriving at a camp
- a plague beginning in a port town
- a prophet gathering followers near a shrine
- a mixed settlement becoming culturally distinct

At site detail scale, time should become even more intimate when the player is inspecting an active event, but it should still feel like history is moving rather than frozen.

Possible pacing rules:

- Island View uses the existing speed ladder and drama mode.
- Region View applies a local-time multiplier when the selected region has active events.
- Site Detail applies a stronger local-time multiplier while an event chain is playing.
- Player can override time controls at any point.
- Important events can temporarily nudge the view into a readable cadence without taking control away from the player.

The goal is to let the player say: “I want to follow this place for a while,” and have the game meet them there.

## Replay / Chronicle Playback Mode

**V1 SHIPPED (2026-06-12, Chronicle Replay V2 read-model).** How We Got Here gained a ⟲ Replay
button that retells the cause chain on a dimmed atlas: numbered marks on the honestly anchored
beats, the proximate-cause spine drawn bold along real recorded edges, a beat card + scrubber
(step / jump). The core honesty rule — **the map never invents a place** — is structural: a
beat draws a pin only if it carries a true SiteId or RegionId; memory-only and unanchored beats
(births, schisms, forbidden bonds) live only in the side rail. Split-scale, timelapse auto-play,
and feed-isolation remain future polish; the path, the scrubber, and the honesty are in.

The current “How We Got Here” popup is valuable because it explains causality. A future official replay mode should go beyond reading the cause chain and **show the chain playing out**.

Instead of only listing events that led to a war, murder, plague, migration, or prophecy, the player should be able to trigger a short replay/timelapse:

- camera moves to the relevant region or site
- event pulses replay in order
- paths/links briefly draw between sites
- people/factions involved highlight as the chain advances
- the feed shows only the replay beats
- the player can scrub, pause, step forward/back, or jump to the full thread

Example: **War of Whispers replay**

1. A killing occurs at a shrine.
2. A rumor spreads through a town.
3. Tension rises between neighboring peoples.
4. A border market erupts in violence.
5. War is declared.

The replay should be compact. It should feel like watching a mythic map-table retell the past, not like a full cinematic cutscene.

### Replay Modes

- **Quick Replay**: 5–15 second timelapse of the most important beats.
- **Full Thread**: slower chronological playback of every cause-linked event.
- **Regional Replay**: shows where the chain happened inside the selected region.
- **Island Replay**: shows broader movement across regions.
- **Split Scale Replay**: starts at island scale, zooms into region/site when the cause chain becomes local.

### Design Principles for Replay

- Replays use existing chronicle events, causes, participants, region ids, and future settlement/site ids.
- Replays should never invent events.
- Replay should be deterministic presentation, not simulation rollback.
- The sim does not need to reverse time. It only needs enough event history and spatial anchors to visually retell what happened.
- Replay mode should become one of the main ways players understand and emotionally attach to the world.

## Implementation Order Idea

Do not jump straight into a giant settlement sim. Build this in slices.

1. **Viewer surfacing pass**
   - Surface culture, gossip, reputation, and cause chains better in the existing island view.

2. **Claude Design / Figma Region Lens mockup**
   - Design the region view, local feed, site inspector, transition language, local-time UI, and replay concept.
   - Must be Godot-implementable using shapes, text, tints, icons, panels, and simple pulses.

3. **Region Lens V1: visual/prototype only** — ✅ SHIPPED as **Sites V1** (2026-06-12),
   stronger than prototyped: the 3–7 sites per region are a SIM read-model (`Sites.cs`,
   baseline-inert, gate-proven), not viewer hints. Site nodes render on the island view
   with name tags, click-to-inspect Site Cards, seat banners, and local paths; the
   Region Lens lists the land's places. (A deeper dedicated region view remains future.)

4. **Event anchoring slice** — ✅ SHIPPED as **Event.SiteId** (2026-06-12), conservatively.
   - `Event.SiteId` is a fourth anchor channel governed by ONE authored convention table
     (`SiteAnchors.Expected`): foundings/abandonment → the region's seat, war seizures → its
     stronghold (hill fort → watch post → ford), ways sworn/shed → its sacred site (shrine →
     grove → barrow → cairn). Everything else stays null; life events never anchor. Picks draw
     zero Rng (immutable sites, type-priority, lowest id), so the verify baseline did not move.
   - The `sites` gate now PROVES the contract event-by-event (was: asserted the field absent).
   - **Battle sites SHIPPED** (2026-06-13, Theater of War — Battle Sites V1): war casualties now
     happen at recorded `battle` events anchored to the war's front + its stronghold (the
     convention table extended to `war`/`battle`). Battles wrap the war's existing casualty rolls
     (zero new Rng), so the baseline moved by exactly the battle count (894/705/574/715). Peace
     gained faction attribution + the toll; the first place-keyed echo, The Field of Bones, fires.
   - Still deferred: per-region economy (famine/plenty anchored to the land — the paired half),
     rumor/trade place anchors (no honest rule), person↔site home anchoring.

5. **Local memory slice** — ◐ FIRST HALF SHIPPED (2026-06-12, Site-Anchored Memory V1).
   - Sites now remember the events that truly belong to them (Event.SiteId): the Site Card shows
     site-anchored tales, a "known for" line from real recorded counts, and the divine hand upon
     the land. Remembered Places lists every truly-touched place with honest anchor language.
   - Still to come: rumors/faith/migration site-anchored (needs those event types to anchor),
     and the dedicated emotional site inspector (today the Site Card is a panel, not a view).

   **Chronicle Replay V1 shipped alongside** (the replay direction below, first slice): How We
   Got Here gained a ⟲ Replay that retells a cause chain on a dimmed atlas — numbered marks on
   honestly anchored beats along real cause edges, a scrubber, turning-point pulses on the live
   map. Honesty: unanchored/memory-only beats live only in the side rail, never a fake map pin.

6. **Hybrid peoples / migration / plague integration**
   - Settlement population makeup becomes useful.
   - Refugees, plague, local faith, mixed families, and emergent hybrid peoples use the region/site layer.

## The God-Hand (Divine Pressure — V1 shipped 2026-06-12)

The player's verbs are now canon, and they follow one design law: **a divine act is
explicit recorded state with a subtle mechanical lean — never a guarantee, never a lie.**

- The seven V1 verbs: **Bless** a soul (eases the existing death roll), **Curse** a
  bloodline (the original butterfly, now ledgered), **Protect** a people (famine weighs
  lighter, fortune mends faster, for a season of years), **Doom** a people (the inverse),
  **Seed an Omen** over a land (attention only — its tales surface louder; no roll changes
  until an honest mechanic exists), **Seed a Forest** and **Call a Spring** (real terrain
  edits on the world surface, witnessed by recorded events).
- Mechanics are multipliers and biases on rolls the sim already makes — a pressure-free
  world stays byte-identical to one where the system doesn't exist. Effects stay subtle
  enough that the world's own causality remains the star.
- Two consumption stories, deliberately distinct: the **tick lean** (a pressure's ongoing
  influence) is multiplier-only and draws no extra randomness in the tick — that is what
  keeps the verify baseline unmoved. The **act-time stroke** (the hand's immediate bite when
  you cast curse/bless — `World.StrikeFortune`) draws once at cast, records one honest
  "fortune" beat (a real reputation shift, weighted toward minor, never a kill), and is
  journaled + replayed by the same verb. Verify never casts, so its baseline still holds.
- Every act records a chronicle event; later events the pressure honestly influenced
  cause-link back to it, so "why did this happen" can answer "because of your hand" only
  when that is mechanical fact. Authored connector copy only.
- The **Fate Ledger** is the player's sacred record: every act, its state (holds / faded /
  wrought), and the consequences the chronicle traced to it.
- Future verbs (prophecy, plague, brush-scale terrain shaping) join only with honest sim
  mechanics behind them — no disabled fake buttons, ever.

## The World Surface (Living Atlas — V1 shipped 2026-06-12)

The island's renderable skin is a deterministic, editable cell grid (`WorldSurface` in the
sim library): terrain classes, elevation, vegetation, rivers/lakes, and a bridge to the
sim's regions. Regions stay the sim's spatial truth; the surface is what the viewer paints
and the god-hand terraforms. Terraforming is journaled cell edits plus recorded events —
never a repainted picture. Since Sites V1 (2026-06-12) the markers on the surface are
real sites from the sim's read-model; what stands AT a site (population, buildings,
daily life) remains a future sim contract and is never claimed.

## Persistence (V1 shipped 2026-06-12)

The world save is an **input journal**, never a snapshot: every divine act with its year
and target snapshot, the follows, and the attention state (`user://world_seed{N}.json`).
On launch the deterministic sim fast-forwards to the saved year, re-applying each act at
its recorded year — the player-shaped world returns exactly, edits and Fate Ledger
included, because the acts are the only player input the sim ever feels. Drifted targets
quarantine (kept in the file, never misapplied); corrupt files are preserved, not
destroyed. The sim never reads the store — the `save` gate proves a loaded-but-unapplied
journal leaves a clean run byte-identical.

## Design Handoff Rule

For visual/UI work, Claude Design, Figma, or another design tool should define the visual direction first. Claude Code should implement the design handoff, not invent the visual language while coding.

Claude Code implementation passes should preserve sim determinism, run `verify`, build the Godot project, and report whether the verify baseline moved. Viewer-only work should not move the baseline.
