"""Living Myth — Unreal Import Smoke V1 (pure-Python, no C++ dependency).

Runs in any UE 5.8 Python context — headless commandlet or the editor console:
    UnrealEditor-Cmd <uproject> -run=pythonscript -script="C:/.../tools/ue_smoke.py"
    (or in-editor)   py "C:/.../tools/ue_smoke.py"

Parses the committed snapshot, enforces the markerKind honesty rule (a home_memory_cairn
must carry NO regionId), lays out regions/sites/markers as debug geometry when a viewport
exists, and writes the verdict to Saved/smoke_verdict.json. Needs no module rebuild.
"""

import json
import os
import unreal

FOCUS = -1          # -1 = whole atlas; a region id isolates one region
WORLD_SCALE = 20000.0
MARKER_HEIGHT = 400.0

proj = unreal.Paths.project_dir()
snap_path = os.path.join(proj, "Content/Snapshots/reference_seed1_year250.json")
with open(snap_path, "r", encoding="utf-8") as fh:
    snap = json.load(fh)

# Schema-major gate (v1 is additive-only; unknown fields ignored).
ver = snap.get("schemaVersion", "")
major = ver.split(".")[0] if ver else ""
if major != "1":
    raise RuntimeError("Incompatible schema major %r (need 1)" % ver)

regions = {r["id"]: r for r in snap["regions"]}


def passes(rid):
    return FOCUS < 0 or rid == FOCUS


def world_pos(nx, ny, z):
    return unreal.Vector((nx - 0.5) * WORLD_SCALE, (ny - 0.5) * WORLD_SCALE, z)


# Optional visual: draw debug geometry if a world is available (no-op/skipped when headless).
world = None
try:
    world = unreal.EditorLevelLibrary.get_editor_world()
except Exception:
    world = None


def line(a, b, color):
    if world:
        try:
            unreal.SystemLibrary.draw_debug_line(world, a, b, color, 0.0, 6.0)
        except Exception:
            pass


def box(center, color):
    if world:
        try:
            unreal.SystemLibrary.draw_debug_box(world, center, unreal.Vector(WORLD_SCALE * 0.02, WORLD_SCALE * 0.02, 20.0), color, unreal.Rotator(), 0.0, 8.0)
        except Exception:
            pass


region_centers = {}
for r in snap["regions"]:
    c = world_pos(r["x"], r["y"], 0.0)
    region_centers[r["id"]] = c
    if passes(r["id"]):
        box(c, unreal.LinearColor(0.84, 0.70, 0.36, 1.0))

for s in snap["sites"]:
    if passes(s["regionId"]) and world:
        loc = world_pos(s["x"], s["y"], 120.0)
        try:
            unreal.SystemLibrary.draw_debug_point(world, loc, 14.0, unreal.LinearColor(0.9, 0.6, 0.2, 1.0), 0.0)
        except Exception:
            pass

placed = unplaceable = violations = 0
for m in snap["memoryMarkers"]:
    kind = m["markerKind"]
    if kind == "home_memory_cairn":
        # Load-bearing honesty rule: a remembered home is never an in-place event.
        if m.get("regionId") is not None:
            violations += 1
            unreal.log_error("HONESTY VIOLATION: home_memory_cairn event %s ('%s') carries regionId %s"
                             % (m["eventId"], m.get("label"), m["regionId"]))
            continue
        anchor = m.get("homeRegionId")
    else:
        anchor = m.get("regionId")

    if anchor is None or anchor not in region_centers or not passes(anchor):
        if anchor is None or anchor not in region_centers:
            unplaceable += 1
        continue

    base = region_centers[anchor]
    top = unreal.Vector(base.x, base.y, base.z + MARKER_HEIGHT)
    col = unreal.LinearColor(1.0, 0.27, 0.27, 1.0) if kind == "true_place_mark" else unreal.LinearColor(1.0, 0.84, 0.0, 1.0)
    line(base, top, col)
    placed += 1

verdict = {
    "schemaVersion": ver,
    "worldName": snap.get("worldName"),
    "seed": snap.get("seed"),
    "year": snap.get("year"),
    "regions": len(snap["regions"]),
    "sites": len(snap["sites"]),
    "markersTotal": len(snap["memoryMarkers"]),
    "markersPlaced": placed,
    "markersUnplaceable": unplaceable,
    "honestyViolations": violations,
    "focusRegionId": snap.get("cameraHints", {}).get("regionFocusId"),
    "focusRegionFilter": FOCUS,
    "hadViewport": bool(world),
    "exportWarnings": snap.get("exportWarnings", []),
}

out_path = os.path.join(proj, "Saved/smoke_verdict.json")
with open(out_path, "w", encoding="utf-8") as fh:
    json.dump(verdict, fh, indent=1)

unreal.log("LivingMyth smoke verdict: %s" % json.dumps(verdict))
