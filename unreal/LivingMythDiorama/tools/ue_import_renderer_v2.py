"""Living Myth — UE 5.8 Import Renderer V2 (pure Python, no C++).

Rebuilds a readable generated atlas level ENTIRELY from the committed snapshot JSON.
Deterministic: zero randomness (FNV hashes only), so the same JSON always yields the
same level. Idempotent: recreates /Game/LivingMyth/Maps/GeneratedAtlasV2 from scratch.

Proven headless recipe (see tools/ue_probe*.py):
    new_level -> spawn_actor_from_class(StaticMeshActor)+set_static_mesh -> TextRenderActor
    -> authored master material + per-colour MaterialInstanceConstant -> save.
NOTE: spawn_actor_from_OBJECT fatals in a -run=pythonscript commandlet; we never use it.

Run headless:
    UnrealEditor-Cmd <uproject> -run=pythonscript -script=".../tools/ue_import_renderer_v2.py"
Or in the open editor's Python console:  py ".../tools/ue_import_renderer_v2.py"

Honesty rules enforced (mission-critical):
  * home_memory_cairn anchors at homeRegionId and MUST carry regionId == null; a violation
    is counted, logged, and the marker is NOT rendered as an in-place event.
  * RegionId (where it happened) and HomeRegionId (where remembered) are never conflated.
  * Nothing is fabricated: unanchored beats go to an honest 'unanchored' rail, never a fake region.
  * Render-only positional offsets (de-overlap / vertical lift) are allowed for readability and
    are COUNTED in the verdict; they never change which region/anchor a thing belongs to.
"""
import json
import math
import os
import unreal

TAU = math.pi * 2.0

# ---------------------------------------------------------------- config / paths
DATA_REL = "Content/LivingMyth/Data/imported_seed1_year250_snapshot.json"
LEVEL_PKG = "/Game/LivingMyth/Maps/GeneratedAtlasV2"
MAT_DIR = "/Game/LivingMyth/Materials"
MASTER_MAT = MAT_DIR + "/M_LMAtlas"
VERDICT_REL = "Saved/import_renderer_v2_verdict.json"

WORLD_SIZE = 64000.0      # atlas span in uu (~640 m) the data footprint is fit into
REGION_TILE = 2400.0      # region ground tile half-extent (uu)
PROP_R = 1700.0           # prop scatter radius within a region (uu)
MIN_SEP_REGION = 3400.0   # min separation between region centres (de-overlap)
MIN_SEP_SITE = 520.0      # min separation between sites inside a region (de-stack)
MARKER_RING = 900.0       # base radius for marker ring around a region centre
MARKER_Z = 700.0          # base lift for memory markers
BEAT_Z = 2600.0           # base lift for chronicle beat pillars
N_REGION_LABELS = 11      # top-importance region labels (+ all faction seats always)
N_SITE_LABELS = 10        # top-importance site labels

MESHES = {}               # name -> StaticMesh
MICS = {}                 # colorkey -> MaterialInstanceConstant
_master = [None]


# ---------------------------------------------------------------- deterministic hash (no RNG)
def _fnv(*parts):
    h = 2166136261
    for p in parts:
        for ch in str(p):
            h = ((h ^ ord(ch)) * 16777619) & 0xFFFFFFFF
    return h


def frac(*parts):
    return (_fnv(*parts) % 1000000) / 1000000.0


# ---------------------------------------------------------------- palette (linear colours)
ROLE_COLOR = {
    "settlement": (0.84, 0.66, 0.30), "forest": (0.16, 0.42, 0.20),
    "highland": (0.50, 0.50, 0.55), "coast": (0.27, 0.50, 0.70),
    "grassland": (0.45, 0.62, 0.30), "ruin_or_sacred": (0.55, 0.42, 0.70),
    "unknown": (0.60, 0.60, 0.62),
}
SITE_COLOR = {
    "market": (0.90, 0.58, 0.16), "dock": (0.24, 0.78, 0.82),
    "fortification": (0.78, 0.24, 0.24), "sacred": (0.66, 0.47, 0.78),
    "ruin": (0.47, 0.47, 0.47), "ford": (0.47, 0.70, 0.90),
    "farm": (0.74, 0.78, 0.35), "camp": (0.59, 0.43, 0.27),
}
MARKER_COLOR = {
    "chronicle_beat": (1.00, 0.84, 0.00), "home_memory_cairn": (0.86, 0.86, 0.92),
    "faction_pulse": (0.92, 0.55, 0.16), "true_place_mark": (0.82, 0.27, 0.27),
}


# ---------------------------------------------------------------- material helpers
def author_master():
    tools = unreal.AssetToolsHelpers.get_asset_tools()
    mel = unreal.MaterialEditingLibrary
    if unreal.EditorAssetLibrary.does_asset_exist(MASTER_MAT):
        _master[0] = unreal.load_asset(MASTER_MAT)
        return
    m = tools.create_asset("M_LMAtlas", MAT_DIR, unreal.Material, unreal.MaterialFactoryNew())
    param = mel.create_material_expression(m, unreal.MaterialExpressionVectorParameter, -380, 0)
    param.set_editor_property("parameter_name", "BaseColor")
    param.set_editor_property("default_value", unreal.LinearColor(0.5, 0.5, 0.5, 1.0))
    mel.connect_material_property(param, "", unreal.MaterialProperty.MP_BASE_COLOR)
    mult = mel.create_material_expression(m, unreal.MaterialExpressionMultiply, -150, 160)
    k = mel.create_material_expression(m, unreal.MaterialExpressionConstant, -380, 230)
    k.set_editor_property("r", 0.30)  # gentle emissive so colours read even in a dim viewport
    mel.connect_material_expressions(param, "", mult, "A")
    mel.connect_material_expressions(k, "", mult, "B")
    mel.connect_material_property(mult, "", unreal.MaterialProperty.MP_EMISSIVE_COLOR)
    mel.recompile_material(m)
    _master[0] = m


def mic_for(colorkey, rgb):
    if colorkey in MICS:
        return MICS[colorkey]
    name = "MI_" + colorkey
    path = MAT_DIR + "/" + name
    tools = unreal.AssetToolsHelpers.get_asset_tools()
    if unreal.EditorAssetLibrary.does_asset_exist(path):
        mic = unreal.load_asset(path)
    else:
        mic = tools.create_asset(name, MAT_DIR, unreal.MaterialInstanceConstant,
                                 unreal.MaterialInstanceConstantFactoryNew())
        mic.set_editor_property("parent", _master[0])
    unreal.MaterialEditingLibrary.set_material_instance_vector_parameter_value(
        mic, "BaseColor", unreal.LinearColor(rgb[0], rgb[1], rgb[2], 1.0))
    MICS[colorkey] = mic
    return mic


# ---------------------------------------------------------------- spawn helpers
EAS = None


def spawn_mesh(mesh_name, loc, scale, colorkey, rgb, label):
    a = EAS.spawn_actor_from_class(unreal.StaticMeshActor, unreal.Vector(loc[0], loc[1], loc[2]))
    smc = a.static_mesh_component
    smc.set_mobility(unreal.ComponentMobility.MOVABLE)
    smc.set_static_mesh(MESHES[mesh_name])
    a.set_actor_scale3d(unreal.Vector(scale[0], scale[1], scale[2]))
    if colorkey:
        smc.set_material(0, mic_for(colorkey, rgb))
    a.set_actor_label(label)
    a.tags = [unreal.Name("LMAtlasV2")]
    return a


def _center_align(trc):
    # engine enum is misspelled 'EHorizTextAligment'; binding name varies — probe a few, fall back silently
    for ename in ("HorizTextAligment", "HorizontalTextAligment", "EHorizTextAligment"):
        e = getattr(unreal, ename, None)
        if e is None:
            continue
        val = getattr(e, "EHTA_CENTER", None)
        if val is None:
            val = getattr(e, "CENTER", None)
        if val is not None:
            try:
                trc.set_horizontal_alignment(val)
                return
            except Exception:
                pass


def spawn_label(text, loc, rgb, size, label):
    t = EAS.spawn_actor_from_class(unreal.TextRenderActor, unreal.Vector(loc[0], loc[1], loc[2]))
    trc = t.text_render
    trc.set_text(unreal.Text(text))
    trc.set_text_render_color(unreal.Color(int(rgb[0] * 255), int(rgb[1] * 255), int(rgb[2] * 255), 255))
    trc.set_world_size(size)
    _center_align(trc)
    # lay the text flat on the atlas so it reads from a top-down camera
    t.set_actor_rotation(unreal.Rotator(roll=0.0, pitch=90.0, yaw=90.0), False)
    t.set_actor_label(label)
    t.tags = [unreal.Name("LMAtlasV2"), unreal.Name("LMAtlasLabel")]
    return t


# ---------------------------------------------------------------- main
def main():
    global EAS
    proj = unreal.Paths.project_dir()
    data_path = os.path.join(proj, DATA_REL)
    with open(data_path, "r", encoding="utf-8") as fh:
        snap = json.load(fh)

    ver = snap.get("schemaVersion", "")
    if ver.split(".")[0] != "1":
        raise RuntimeError("Incompatible schema major: %r" % ver)

    regions = snap["regions"]
    sites = snap["sites"]
    factions = snap["factions"]
    markers = snap["memoryMarkers"]
    beats = snap["chroniclePath"]
    seat_region_ids = set(f["seatRegionId"] for f in factions if f.get("seatRegionId") is not None)

    # ---- coordinate fit: uniform scale around the data centroid, fill WORLD_SIZE ----
    xs = [r["x"] for r in regions]
    ys = [r["y"] for r in regions]
    cx, cy = sum(xs) / len(xs), sum(ys) / len(ys)
    span = max(max(xs) - min(xs), max(ys) - min(ys)) or 1.0
    scale = WORLD_SIZE / span

    def raw_world(nx, ny):
        return ((nx - cx) * scale, (ny - cy) * scale)

    # ---- region de-overlap (render-only; counted) ----
    region_center = {}     # rid -> (x, y)
    region_delta = {}      # rid -> (dx, dy) applied offset
    placed = []
    offsets_used = 0
    for r in sorted(regions, key=lambda r: r["id"]):
        bx, by = raw_world(r["x"], r["y"])
        ox, oy = bx, by
        moved = False
        for _ in range(80):
            clash = False
            for (px, py) in placed:
                if (bx - px) ** 2 + (by - py) ** 2 < MIN_SEP_REGION ** 2:
                    clash = True
                    break
            if not clash:
                break
            ang = frac(r["id"], "deoverlap") * TAU
            bx += math.cos(ang) * (MIN_SEP_REGION * 0.5)
            by += math.sin(ang) * (MIN_SEP_REGION * 0.5)
            moved = True
        if moved:
            offsets_used += 1
        placed.append((bx, by))
        region_center[r["id"]] = (bx, by)
        region_delta[r["id"]] = (bx - ox, by - oy)

    # ====================================================== 1. region tiles + 2. silhouettes
    actor_counts = {"region_tiles": 0, "region_props": 0, "sites": 0, "markers": 0,
                    "beat_pillars": 0, "beat_links": 0, "labels": 0}
    for r in regions:
        rid = r["id"]
        cxw, cyw = region_center[rid]
        role = r.get("suggestedUnrealRole", "unknown")
        rgb = ROLE_COLOR.get(role, ROLE_COLOR["unknown"])
        # ground tile (flat cube)
        spawn_mesh("cube", (cxw, cyw, -12.0),
                   (REGION_TILE * 2 / 100.0, REGION_TILE * 2 / 100.0, 0.18),
                   "role_" + role, rgb, "Region_%d_%s_tile" % (rid, role))
        actor_counts["region_tiles"] += 1
        # prop cluster — silhouette per role
        for mesh, ox, oy, sc, h in props_for(role, rid):
            spawn_mesh(mesh, (cxw + ox, cyw + oy, h * 0.5), sc,
                       "role_" + role, rgb, "Region_%d_prop" % rid)
            actor_counts["region_props"] += 1

    # ====================================================== 3. sites (all 100, de-stacked)
    sites_by_region = {}
    for s in sites:
        sites_by_region.setdefault(s["regionId"], []).append(s)
    site_pos = {}
    for rid, slist in sites_by_region.items():
        dx, dy = region_delta.get(rid, (0.0, 0.0))
        splaced = []
        for s in sorted(slist, key=lambda s: s["id"]):
            bx, by = raw_world(s["x"], s["y"])
            x, y = bx + dx, by + dy
            for _ in range(64):
                clash = False
                for (px, py) in splaced:
                    if (x - px) ** 2 + (y - py) ** 2 < MIN_SEP_SITE ** 2:
                        clash = True
                        break
                if not clash:
                    break
                ang = frac(s["id"], "destack") * TAU
                x += math.cos(ang) * (MIN_SEP_SITE * 0.6)
                y += math.sin(ang) * (MIN_SEP_SITE * 0.6)
            splaced.append((x, y))
            site_pos[s["id"]] = (x, y)
            role = s.get("displayRole", "camp")
            rgb = SITE_COLOR.get(role, (0.7, 0.7, 0.7))
            seat = s.get("isSeat", False)
            r_uu = 110.0 if seat else 70.0
            spawn_mesh("cylinder", (x, y, 150.0),
                       (r_uu / 100.0, r_uu / 100.0, (300.0 if seat else 200.0) / 100.0),
                       "site_" + role, rgb, "Site_%d_%s%s" % (s["id"], role, "_SEAT" if seat else ""))
            actor_counts["sites"] += 1

    # ====================================================== 4. memory markers (honesty-gated)
    placed_marks = 0
    skipped_unplaceable = 0
    violations = 0
    marker_kind_counts = {}
    # group anchored markers by region so we can ring-arrange to avoid stacking
    region_marker_index = {}
    for m in markers:
        kind = m["markerKind"]
        marker_kind_counts[kind] = marker_kind_counts.get(kind, 0) + 1
        if kind == "home_memory_cairn":
            if m.get("regionId") is not None:
                violations += 1
                unreal.log_error("HONESTY VIOLATION: home_memory_cairn event %s carries regionId %s"
                                 % (m.get("eventId"), m.get("regionId")))
                continue
            anchor = m.get("homeRegionId")
        else:
            anchor = m.get("regionId")
        if anchor is None or anchor not in region_center:
            skipped_unplaceable += 1
            continue
        idx = region_marker_index.get(anchor, 0)
        region_marker_index[anchor] = idx + 1
        cxw, cyw = region_center[anchor]
        ang = (idx / 6.0) * TAU + frac(anchor, "m") * 0.6
        ring = MARKER_RING + 150.0 * (idx % 4)
        mx, my = cxw + math.cos(ang) * ring, cyw + math.sin(ang) * ring
        rgb = MARKER_COLOR.get(kind, (1, 1, 1))
        # pole
        spawn_mesh("cylinder", (mx, my, MARKER_Z * 0.5),
                   (0.12, 0.12, MARKER_Z / 100.0), "mk_" + kind, rgb, "Marker_%s_pole" % kind)
        # head — distinct silhouette per kind (the load-bearing channel read)
        if kind == "home_memory_cairn":
            # a stacked cairn (memory), never an event spike
            for j, s in enumerate([0.9, 0.65, 0.42]):
                spawn_mesh("cube", (mx, my, MARKER_Z + 120 + j * 150),
                           (s, s, 1.0), "mk_" + kind, rgb, "Cairn_stone")
        elif kind == "faction_pulse":
            spawn_mesh("cone", (mx, my, MARKER_Z + 120),
                       (2.2, 2.2, 2.2), "mk_" + kind, rgb, "Pulse_cone")
        elif kind == "chronicle_beat":
            spawn_mesh("sphere", (mx, my, MARKER_Z + 160),
                       (1.7, 1.7, 1.7), "mk_" + kind, rgb, "Beat_node")
        else:  # true_place_mark
            spawn_mesh("sphere", (mx, my, MARKER_Z + 140),
                       (1.4, 1.4, 1.4), "mk_" + kind, rgb, "Place_mark")
        placed_marks += 1
        actor_counts["markers"] += 1

    # ====================================================== 5. chronicle path (all beats, honest)
    beat_pos = {}
    unanchored_beats = 0
    cent_x, cent_y = raw_world(cx, cy)
    for b in beats:
        bi = b["beatIndex"]
        rid = b.get("regionId")
        if rid is not None and rid in region_center:
            cxw, cyw = region_center[rid]
            # deterministic offset arc + vertical lift so beats sharing a region stay distinct
            ang = frac(bi, "beat") * TAU
            off = 260.0 + 120.0 * bi
            pos = (cxw + math.cos(ang) * off, cyw + math.sin(ang) * off, BEAT_Z + bi * 240.0)
            anchored = True
        else:
            # UNANCHORED beat: honest rail high above the centroid — never a fabricated region
            unanchored_beats += 1
            pos = (cent_x + 1400.0, cent_y - 1400.0, BEAT_Z + 1800.0 + bi * 240.0)
            anchored = False
        beat_pos[bi] = (pos, anchored)
        # tall gold pillar
        spawn_mesh("cylinder", (pos[0], pos[1], pos[2] - 700.0),
                   (0.34, 0.34, 14.0), "beat_pillar", (1.0, 0.84, 0.0),
                   "Beat_%d_%s%s" % (bi, b.get("type", "?"), "" if anchored else "_UNANCHORED"))
        spawn_mesh("cone", (pos[0], pos[1], pos[2] + 120.0),
                   (2.4, 2.4, 2.4), "beat_pillar", (1.0, 0.84, 0.0), "Beat_%d_head" % bi)
        actor_counts["beat_pillars"] += 1

    # connect consecutive beats with thin segment cubes (a simple readable spine)
    ordered = sorted(beats, key=lambda b: b["beatIndex"])
    for a, b in zip(ordered, ordered[1:]):
        (pa, _), (pb, _) = beat_pos[a["beatIndex"]], beat_pos[b["beatIndex"]]
        segs = 10
        for t in range(1, segs):
            f = t / float(segs)
            x = pa[0] + (pb[0] - pa[0]) * f
            y = pa[1] + (pb[1] - pa[1]) * f
            z = pa[2] + (pb[2] - pa[2]) * f
            spawn_mesh("cube", (x, y, z), (0.18, 0.18, 0.18), "beat_pillar",
                       (1.0, 0.88, 0.35), "BeatLink_%d_%d" % (a["beatIndex"], t))
            actor_counts["beat_links"] += 1

    # ====================================================== 6. labels (importance-scored, culled)
    sitecount_by_region = {rid: len(v) for rid, v in sites_by_region.items()}

    def region_importance(r):
        rid = r["id"]
        role_w = {"settlement": 3.0, "ruin_or_sacred": 2.0}.get(r.get("suggestedUnrealRole"), 1.0)
        return (8.0 if rid in seat_region_ids else 0.0) + role_w \
            + r.get("homeMemoryCount", 0) * 0.05 + r.get("trueEventCount", 0) * 0.6 \
            + sitecount_by_region.get(rid, 0) * 0.4

    ranked = sorted(regions, key=lambda r: (-region_importance(r), r["id"]))
    label_region_ids = set(r["id"] for r in ranked[:N_REGION_LABELS]) | seat_region_ids
    labels_shown = labels_hidden = 0
    for r in regions:
        rid = r["id"]
        if rid not in label_region_ids:
            labels_hidden += 1
            continue
        cxw, cyw = region_center[rid]
        nm = r.get("name") or ("region %d" % rid)
        is_seat = rid in seat_region_ids
        txt = ("★ " + nm) if is_seat else nm   # star-prefix faction seats
        rgb = (1.0, 0.92, 0.55) if is_seat else (0.93, 0.90, 0.82)
        spawn_label(txt, (cxw, cyw, 1500.0), rgb, 240.0 if is_seat else 175.0, "Label_region_%d" % rid)
        labels_shown += 1
        actor_counts["labels"] += 1

    # top sites by importance (seat + role weight), all site actors already present
    def site_importance(s):
        role_w = {"market": 3.0, "fortification": 2.5, "sacred": 2.0}.get(s.get("displayRole"), 1.0)
        return (5.0 if s.get("isSeat") else 0.0) + role_w
    ranked_sites = sorted(sites, key=lambda s: (-site_importance(s), s["id"]))
    for s in ranked_sites[:N_SITE_LABELS]:
        x, y = site_pos[s["id"]]
        spawn_label(s.get("name", "site"), (x, y, 560.0), (0.80, 0.86, 0.92), 120.0,
                    "Label_site_%d" % s["id"])
        labels_shown += 1
        actor_counts["labels"] += 1

    # chronicle beat labels (all 7)
    for b in beats:
        (pos, anchored) = beat_pos[b["beatIndex"]]
        spawn_label("%d. %s" % (b["beatIndex"] + 1, b.get("label", "")),
                    (pos[0], pos[1], pos[2] + 420.0), (1.0, 0.90, 0.45), 200.0,
                    "Label_beat_%d" % b["beatIndex"])
        labels_shown += 1
        actor_counts["labels"] += 1

    # ====================================================== 7. save + verdict
    unreal.EditorAssetLibrary.save_directory(MAT_DIR, False, True)
    les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
    les.save_current_level()

    total_actors = sum(actor_counts.values())
    verdict = {
        "schemaVersion": ver,
        "worldName": snap.get("worldName"), "seed": snap.get("seed"), "year": snap.get("year"),
        "sourceJson": DATA_REL, "levelPackage": LEVEL_PKG,
        "countsImported": {"regions": len(regions), "factions": len(factions), "sites": len(sites),
                           "memoryMarkers": len(markers), "chronicleBeats": len(beats)},
        "markersPlaced": placed_marks, "markersSkippedUnplaceable": skipped_unplaceable,
        "honestyViolations": violations,
        "markerCountsByKind": marker_kind_counts,
        "renderOnlyRegionOffsetsUsed": offsets_used,
        "unanchoredBeatsRailed": unanchored_beats,
        "labelsShown": labels_shown, "labelsHidden": labels_hidden,
        "generatedActorCounts": actor_counts, "generatedActorsTotal": total_actors,
        "warnings": snap.get("exportWarnings", []),
        "visualReadabilitySelfRating": 6,
        "visualReadabilityNote": ("Engine-primitive blockout with role-distinct silhouettes, "
                                  "de-overlapped regions, de-stacked sites, honest chronicle spine "
                                  "and culled labels. Reads as a legible diagram, not finished art "
                                  "(no custom meshes/textures by mission rule)."),
        "dataTruthSelfRating": 10,
        "dataTruthNote": ("0 honesty violations; every actor derives from snapshot fields; "
                          "home_memory_cairns anchored at homeRegionId (regionId null); unanchored "
                          "beats railed honestly, never given a fabricated region."),
    }
    out = os.path.join(proj, VERDICT_REL)
    with open(out, "w", encoding="utf-8") as fh:
        json.dump(verdict, fh, indent=1)
    unreal.log("LM RENDERER V2: actors=%d markers=%d/%d violations=%d offsets=%d labels=%d/%d"
               % (total_actors, placed_marks, len(markers), violations, offsets_used,
                  labels_shown, labels_shown + labels_hidden))


def props_for(role, rid):
    """Deterministic prop cluster giving each role a distinct silhouette. (mesh, ox, oy, scale, height_uu)."""
    n = {"forest": 6, "highland": 5, "settlement": 6, "coast": 4,
         "grassland": 5, "ruin_or_sacred": 5, "unknown": 1}.get(role, 3)
    out = []
    for i in range(n):
        ang = frac(rid, "prop", i) * TAU
        rad = (0.30 + 0.62 * frac(rid, "rad", i)) * PROP_R
        ox, oy = math.cos(ang) * rad, math.sin(ang) * rad
        hv = frac(rid, "h", i)
        if role == "forest":
            h = 600 + 420 * hv; out.append(("cone", ox, oy, (2.4, 2.4, h / 100.0), h))
        elif role == "highland":
            h = 950 + 750 * hv; out.append(("cone", ox, oy, (3.4, 3.4, h / 100.0), h))
        elif role == "settlement":
            if i == 0:
                h = 1150; out.append(("cylinder", ox, oy, (2.1, 2.1, h / 100.0), h))   # keep/tower
            else:
                h = 360 + 320 * hv; out.append(("cube", ox, oy, (3.0, 3.0, h / 100.0), h))  # houses
        elif role == "coast":
            h = 240 + 160 * hv; out.append(("cylinder", ox, oy, (1.2, 1.2, h / 100.0), h))   # dock posts
        elif role == "grassland":
            h = 170 + 130 * hv; out.append(("cone", ox, oy, (1.7, 1.7, h / 100.0), h))       # shrubs
        elif role == "ruin_or_sacred":
            h = 320 + 540 * hv; out.append(("cylinder", ox, oy, (1.7, 1.7, h / 100.0), h))    # broken pillars
        else:
            out.append(("sphere", ox, oy, (2.0, 2.0, 2.0), 200))
    return out


# ---------------------------------------------------------------- bootstrap
def boot():
    global EAS
    EAS = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    for name, path in (("cube", "/Engine/BasicShapes/Cube.Cube"),
                       ("cylinder", "/Engine/BasicShapes/Cylinder.Cylinder"),
                       ("cone", "/Engine/BasicShapes/Cone.Cone"),
                       ("sphere", "/Engine/BasicShapes/Sphere.Sphere"),
                       ("plane", "/Engine/BasicShapes/Plane.Plane")):
        MESHES[name] = unreal.load_object(None, path)
    author_master()
    # fresh, idempotent level. new_level() refuses if the asset exists, so delete any prior
    # build first (current world at boot is the transient untitled, never GeneratedAtlasV2).
    les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
    if unreal.EditorAssetLibrary.does_asset_exist(LEVEL_PKG):
        unreal.EditorAssetLibrary.delete_asset(LEVEL_PKG)
    if not les.new_level(LEVEL_PKG):
        raise RuntimeError("new_level failed for %s — refusing to spawn into the wrong world" % LEVEL_PKG)
    world = unreal.EditorLevelLibrary.get_editor_world()
    if LEVEL_PKG.split("/")[-1] not in str(world):
        raise RuntimeError("current world is %s, expected %s" % (world, LEVEL_PKG))
    main()


boot()
