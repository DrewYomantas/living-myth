# Living Myth — Unreal Import Smoke V1 (UE 5.8)

The **consumer** half of the Godot Snapshot Bridge. The bridge (`unreal-snapshot` console command,
shipped `092ced1`) emits a deterministic, honest JSON of a real Living Myth world; this UE 5.8 project
parses that JSON and lays it out in a level. One-way: **Living Myth → JSON → Unreal**. This project
never reads or writes the Living Myth side.

This is a *smoke test*, not the final diorama. It proves honest sim data round-trips into a real engine
with the channel rules intact. Fidelity (meshes, materials, lighting) is a later pass — here the atlas
is drawn as debug geometry on purpose, so there is **zero art commitment** to get the data validated.

## What it does
- `FLMSnapshot` (`LMSnapshotTypes.h`) mirrors schema **v1.0.0** field-for-field.
- `FLMSnapshotLoader` parses the JSON and **gates the schema major version** (`1.x.y` only).
- `ALMAtlasActor` lays out `regions` and `sites` by their normalized `x`/`y`, colored by
  `suggestedUnrealRole` / `displayRole`, and renders `memoryMarkers` **switching on `markerKind`**:
  - `home_memory_cairn` → anchored at the **home** region (and it asserts the feed did **not** carry a
    `regionId` — rendering a remembered home as an in-place event is the exact mistake the bridge forbids).
  - `faction_pulse` / `true_place_mark` / `chronicle_beat` → anchored at the true `regionId`.
  - Anything unanchored (both ids null, e.g. a war) is **left off the map**, never invented onto it.
- `exportWarnings` are logged (`LogLivingMyth`); `null` is honored as honest absence.
- `cameraHints` frames the focus region + atlas bounds.

## First run (Drew's machine — UE 5.8 editor required)
1. Right-click `LivingMythDiorama.uproject` → **Generate Visual Studio project files**.
2. Open the `.uproject` (or build `LivingMythDioramaEditor` in VS, then open). First open compiles the
   C++ module.
3. In an empty level, drag an **LMAtlasActor** into the world. Its `SnapshotPath` defaults to the
   committed sample (`Content/Snapshots/reference_seed1_year250.json`).
4. Select it → Details panel → click **Build From Snapshot** (CallInEditor). The atlas draws as
   persistent debug geometry. Set `FocusRegionFilter` to a region id (e.g. `3`, the sample's focus) to
   render **one honest region** in isolation.
5. Check the **Output Log** (filter `LogLivingMyth`): expect the load line, the two known
   `exportWarning` lines, and `markers placed … / honesty-violations 0`.

## Definition of done
Region 3 (or any) lays out with correct site markers; `home_memory_cairn`s sit at their home region as
remembrance (never as place marks); `honesty-violations 0` in the log; camera framed on the focus region.

## Refreshing the snapshot
From the Living Myth repo root:
```
dotnet run --project src/LivingMyth.Console -- unreal-snapshot --years 250 \
  --out unreal/LivingMythDiorama/Content/Snapshots/reference_seed1_year250.json
```
The same command is the CI gate, so the file is always schema-valid + deterministic.

## Schema note
Schema v1 is **additive-only**; the parser ignores unknown fields. A new *major* version is rejected
by `FLMSnapshotLoader` until the structs are updated to match.
