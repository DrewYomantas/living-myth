# Living Myth Sandbox

A no-generative-AI, Steam-first 2D god-sim. The C# port of a proven headless Python
prototype: a deterministic world that grows traceable emergent history, surfaces the
important events (importance score → Yours/Loud/Rising feed), and detects 8 Myth Echoes
after the fact. Design docs + Python reference: `~/Downloads/ClaudeCodeLivingMyth.zip`.

## The one architecture rule (non-negotiable)
`src/LivingMyth.Sim/` is a standalone C# class library with **ZERO Godot dependency**.
Godot only renders it. Authored content stays in `src/LivingMyth.Sim/data/*.json`,
separate from logic. Never let simulation logic leak into Godot nodes.

## Layout
- `src/LivingMyth.Sim/` — the sim (Rng, Models, Chronicle, World, Scoring, Echoes, Feed). net8.0.
- `src/LivingMyth.Console/` — proof runner (run | divergence | surface | verify).
- `godot/` — the viewer (.NET build): `MapView.cs` (map render + click) and `Main.cs`
  (tick loop, live feed, inspectors, curse tool, catch-up, Follow/Yours channel). References the
  Sim; open the folder with the Godot mono editor and press F5. M0–M3 + longevity done; next is the
  visual/UX pass + more pressure engines.

## Commands
```bash
dotnet build LivingMyth.slnx                                   # build everything
dotnet run --project src/LivingMyth.Console -- verify          # determinism gate (must pass)
dotnet run --project src/LivingMyth.Console -- run --seed 42
dotnet run --project src/LivingMyth.Console -- divergence --seed 18
dotnet run --project src/LivingMyth.Console -- surface --seed 1
dotnet run --project src/LivingMyth.Console -- run --seed 7 --years 3000 --cap 300  # --cap overrides carrying_capacity for balance tuning
dotnet build godot/LivingMyth.Godot.csproj                     # build Godot project headlessly
```

## Gotchas
- **Determinism is sacred.** All randomness routes through `Rng`. C# dicts/sets are not
  order-stable like Python's — every iteration that feeds RNG or results MUST be explicitly
  ordered (people/religions by id, factions in config order, member sets sorted). `verify`
  guards this. Intra-C# only: NOT bit-compatible with the Python seeds.
- **Hot paths must stay O(living), not O(history).** People and the chronicle grow forever;
  per-tick/per-frame work must NOT scan them. Iterate the living set (`Living()`/faction
  members), use `Chronicle.Get(id)` (id == list index) over rebuilding id maps, and stream
  the feed with incremental consequence counts. Reintroducing an all-history scan is the
  classic regression here.
- **Population balance is the `carrying_capacity` param** (config.json, currently 300):
  logistic births → plateau. Too low (~120) drifts to extinction; verified stable ~450
  living over 5000 yrs at 300. `curse_death_multiplier` (2.5) tunes how apocalyptic curses are.
- **Perf changes must stay identity-preserving:** set `carrying_capacity` to 0 and `verify`
  must reproduce baseline counts 1309/450/628/800 (seeds 1/18/42/7 @ 120 yrs).
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
  before any `git add`/commit, or you'll stage the whole home dir.

<!-- TOKENOMICS:START -->
## Token Optimization Insights

_Last updated: 2026-06-08_

### Context Management
- Your context snowballs at **turn 18** on average (41% of sessions). Use `/compact` proactively after turn 16-18 on long sessions to prevent unbounded growth.
- Some sessions use significantly more tokens than others. Consider shorter, more focused sessions with clear goals.
- You could benefit from subagents for parallel tasks. Consider splitting multi-file operations into parallel agent tasks.
- You read files you don't end up using. Use `Grep` first to locate relevant files before reading them — reduces unnecessary context by ~0%.
- You receive verbose command output. Prefer `Grep`/`Read` tools over bash commands when searching files to reduce output tokens.

### Model Usage
- You use Opus/Claude for **12%** of simple tasks. Prefer **Sonnet** for editing, small fixes, and exploration tasks to reduce token usage by ~5x on those sessions.
- MCP server(s) **unity-mcp** are loaded but never used. Consider removing them to reduce per-session overhead.

### Prompt Quality
- **7%** of your prompts are under 10 words. Include specific file paths, function names, and expected outcomes to reduce clarification rounds.
<!-- TOKENOMICS:END -->
