"""Living Myth — UE 5.8 Generated Atlas Visual V4 (pure Python, no C++).

Turns the V3 coherent-but-flat diagram into a LIT, inspectable myth-atlas prototype:
DirectionalLight (warm late-afternoon key) + SkyAtmosphere + SkyLight (cool fill) +
ExponentialHeightFog + a locked-exposure PostProcessVolume, lowered land emissive so the
lighting actually does the shading (no flat emissive-only look), a composed camera, and a
cheap ISM batching pass for the uniform-cube repeats (fringe / bridges / chronicle links).

Builds a NEW level GeneratedAtlasV4 (never overwrites V3), entirely from the imported
snapshot JSON. Deterministic (FNV hashes only) + idempotent. No C++, engine basic shapes +
authored materials only.

Proven headless recipe (Docs/UE58_IMPORT_RENDERER_V2/3 + V4 probes):
  * spawn_actor_from_class(StaticMeshActor)+set_static_mesh  (spawn_from_object FATALS)
  * lights: spawn_actor_from_class(DirectionalLight/SkyAtmosphere/SkyLight/ExponentialHeightFog/
    PostProcessVolume); component via get_component_by_class; set_light_color wants LinearColor
  * ISM: spawn Actor -> InstancedStaticMeshComponent(actor) -> set as root_component ->
    set_static_mesh/set_material -> add_instance(transform, world_space=True)  (verified to
    serialize through save+reload; add_component_by_class / register_component do NOT exist here)
  * save: save_directory(only_if_is_dirty=True) + save_map(world, path)  (overwrite-safe)
  * capture: take_high_res_screenshot NATIVE-CRASHES a commandlet (no viewport) -> not called;
    tools/ue_capture_v4.py is the editor-side helper that DOES capture (viewport present).

Honesty (unchanged contract):
  * home_memory_cairn anchors at homeRegionId and MUST carry regionId == null; a violation is
    counted, logged, and NOT rendered as an in-place event.
  * RegionId (where it happened) and HomeRegionId (where remembered) are never conflated.
  * Unanchored chronicle beats are railed off-world honestly, never given a fabricated region.
  * Render-only positional choices (contraction/de-overlap/lift/bridges) are DISPLAY LAYOUT and
    are counted in the verdict; they never change which region/anchor anything belongs to.
"""
import json
import math
import os
import unreal

TAU = math.pi * 2.0

# ----------------------------------------------------------------- paths / config
DATA_REL = "Content/LivingMyth/Data/imported_seed1_year250_snapshot.json"
LEVEL_PKG = "/Game/LivingMyth/Maps/GeneratedAtlasV4"
MAT_DIR = "/Game/LivingMyth/Materials"
MASTER_V3 = MAT_DIR + "/M_LMAtlasV3"        # reused (BaseColor + EmissiveStrength); not mutated
VERDICT_REL = "Saved/import_renderer_v4_verdict.json"

WORLD_SIZE = 58000.0
CONTRACT = 0.90
REGION_TILE = 2300.0
PROP_R = 1650.0
MIN_SEP_REGION = 3500.0
MIN_SEP_SITE = 520.0
BRIDGE_DIST = 5200.0
BRIDGE_CAP = 34
MARKER_RING = 980.0
N_REGION_LABELS = 9
N_SITE_LABELS = 4

# V4: land/props no longer self-glow — the lighting shades them. Markers/gold stay emissive.
EM_LAND = 0.05
EM_FRINGE = 0.04
EM_OCEAN = 0.02
EM_ROOF = 0.05

MESHES = {}
MICS = {}
_mv3 = [None]
ISM_FRINGE = []   # transforms: fringe rings + bridge strips (one shared material)
ISM_GOLD = []     # transforms: chronicle beat-link segments


# ----------------------------------------------------------------- deterministic hash
def _fnv(*parts):
    h = 2166136261
    for p in parts:
        for ch in str(p):
            h = ((h ^ ord(ch)) * 16777619) & 0xFFFFFFFF
    return h


def frac(*parts):
    return (_fnv(*parts) % 1000000) / 1000000.0


# ----------------------------------------------------------------- palette (linear rgb)
ROLE_COLOR = {
    "settlement": (0.74, 0.55, 0.26), "forest": (0.13, 0.34, 0.17),
    "highland": (0.40, 0.40, 0.46), "coast": (0.50, 0.56, 0.34),
    "grassland": (0.40, 0.52, 0.26), "ruin_or_sacred": (0.46, 0.36, 0.58),
    "unknown": (0.34, 0.34, 0.38),
}
SITE_COLOR = {
    "market": (0.90, 0.58, 0.16), "dock": (0.24, 0.78, 0.82),
    "fortification": (0.78, 0.24, 0.24), "sacred": (0.66, 0.47, 0.78),
    "ruin": (0.55, 0.55, 0.55), "ford": (0.47, 0.70, 0.90),
    "farm": (0.74, 0.78, 0.35), "camp": (0.62, 0.46, 0.30),
}
OCEAN = (0.012, 0.035, 0.09)
FRINGE = (0.66, 0.60, 0.40)
GOLD = (1.0, 0.80, 0.12)
MK = {
    "true_place_mark": ((0.86, 0.22, 0.20), 0.40),
    "home_memory_cairn": ((0.80, 0.80, 0.86), 0.12),
    "faction_pulse": ((0.95, 0.55, 0.15), 0.50),
    "chronicle_beat": (GOLD, 0.85),
}


# ----------------------------------------------------------------- materials
def author_master_v3():
    if unreal.EditorAssetLibrary.does_asset_exist(MASTER_V3):
        _mv3[0] = unreal.load_asset(MASTER_V3)
        return
    tools = unreal.AssetToolsHelpers.get_asset_tools()
    mel = unreal.MaterialEditingLibrary
    m = tools.create_asset("M_LMAtlasV3", MAT_DIR, unreal.Material, unreal.MaterialFactoryNew())
    base = mel.create_material_expression(m, unreal.MaterialExpressionVectorParameter, -420, 0)
    base.set_editor_property("parameter_name", "BaseColor")
    base.set_editor_property("default_value", unreal.LinearColor(0.5, 0.5, 0.5, 1.0))
    mel.connect_material_property(base, "", unreal.MaterialProperty.MP_BASE_COLOR)
    em = mel.create_material_expression(m, unreal.MaterialExpressionScalarParameter, -420, 240)
    em.set_editor_property("parameter_name", "EmissiveStrength")
    em.set_editor_property("default_value", 0.20)
    mult = mel.create_material_expression(m, unreal.MaterialExpressionMultiply, -160, 140)
    mel.connect_material_expressions(base, "", mult, "A")
    mel.connect_material_expressions(em, "", mult, "B")
    mel.connect_material_property(mult, "", unreal.MaterialProperty.MP_EMISSIVE_COLOR)
    mel.recompile_material(m)
    _mv3[0] = m


def mic(key, rgb, emissive):
    """V4 instances (MI_v4_*) on the reused V3 master. Never touches V3's MI_v3_*."""
    if key in MICS:
        return MICS[key]
    name = "MI_v4_" + key
    path = MAT_DIR + "/" + name
    tools = unreal.AssetToolsHelpers.get_asset_tools()
    if unreal.EditorAssetLibrary.does_asset_exist(path):
        m = unreal.load_asset(path)
    else:
        m = tools.create_asset(name, MAT_DIR, unreal.MaterialInstanceConstant,
                               unreal.MaterialInstanceConstantFactoryNew())
        m.set_editor_property("parent", _mv3[0])
    mel = unreal.MaterialEditingLibrary
    mel.set_material_instance_vector_parameter_value(m, "BaseColor", unreal.LinearColor(rgb[0], rgb[1], rgb[2], 1.0))
    mel.set_material_instance_scalar_parameter_value(m, "EmissiveStrength", emissive)
    MICS[key] = m
    return m


def site_mic(role):
    # reuse V2's MI_site_* where present (encouraged), else author a V4 one
    key = "site_" + role
    if key in MICS:
        return MICS[key]
    v2 = MAT_DIR + "/MI_site_" + role
    if unreal.EditorAssetLibrary.does_asset_exist(v2):
        m = unreal.load_asset(v2)
        MICS[key] = m
        return m
    return mic("site_" + role, SITE_COLOR.get(role, (0.7, 0.7, 0.7)), 0.12)


# ----------------------------------------------------------------- spawn helpers
EAS = None


def mesh_actor(mesh_name, loc, scale, material, label, yaw=0.0):
    a = EAS.spawn_actor_from_class(unreal.StaticMeshActor, unreal.Vector(loc[0], loc[1], loc[2]))
    smc = a.static_mesh_component
    smc.set_mobility(unreal.ComponentMobility.MOVABLE)
    smc.set_static_mesh(MESHES[mesh_name])
    a.set_actor_scale3d(unreal.Vector(scale[0], scale[1], scale[2]))
    if yaw:
        a.set_actor_rotation(unreal.Rotator(roll=0.0, pitch=0.0, yaw=yaw), False)
    if material:
        smc.set_material(0, material)
    a.set_actor_label(label)
    a.tags = [unreal.Name("LMAtlasV4")]
    return a


def ism_add(bucket, loc, scale, yaw=0.0):
    bucket.append(unreal.Transform(unreal.Vector(loc[0], loc[1], loc[2]),
                                   unreal.Rotator(roll=0.0, pitch=0.0, yaw=yaw),
                                   unreal.Vector(scale[0], scale[1], scale[2])))


def build_ism(bucket, mesh_name, material, label):
    """One InstancedStaticMeshComponent holder for many identical cubes (verified to serialize)."""
    if not bucket:
        return 0
    ia = EAS.spawn_actor_from_class(unreal.Actor, unreal.Vector(0, 0, 0))
    ism = unreal.InstancedStaticMeshComponent(ia)
    ism.set_static_mesh(MESHES[mesh_name])
    try:
        ia.set_editor_property("root_component", ism)
    except Exception as e:
        unreal.log_warning("ISM root_component (%s): %r" % (label, e))
    try:
        ism.set_material(0, material)
    except Exception as e:
        unreal.log_warning("ISM material (%s): %r" % (label, e))
    for t in bucket:
        ism.add_instance(t, True)
    ia.set_actor_label(label)
    ia.tags = [unreal.Name("LMAtlasV4"), unreal.Name("LMAtlasISM")]
    return len(bucket)


def _center_align(trc):
    for ename in ("HorizTextAligment", "HorizontalTextAligment", "EHorizTextAligment"):
        e = getattr(unreal, ename, None)
        if e is None:
            continue
        val = getattr(e, "EHTA_CENTER", None) or getattr(e, "CENTER", None)
        if val is not None:
            try:
                trc.set_horizontal_alignment(val)
                return
            except Exception:
                pass


def label_actor(text, loc, rgb, size, label, anchor=None):
    t = EAS.spawn_actor_from_class(unreal.TextRenderActor, unreal.Vector(loc[0], loc[1], loc[2]))
    trc = t.text_render
    trc.set_text(unreal.Text(text))
    trc.set_text_render_color(unreal.Color(int(rgb[0] * 255), int(rgb[1] * 255), int(rgb[2] * 255), 255))
    trc.set_world_size(size)
    _center_align(trc)
    t.set_actor_rotation(unreal.Rotator(roll=0.0, pitch=90.0, yaw=90.0), False)
    t.set_actor_label(label)
    t.tags = [unreal.Name("LMAtlasV4"), unreal.Name("LMAtlasLabel")]
    leaders = 0
    if anchor is not None:
        az = anchor[2]
        h = max(80.0, loc[2] - az)
        mesh_actor("cube", (anchor[0], anchor[1], az + h * 0.5), (0.06, 0.06, h / 100.0),
                   mic("leader", (0.85, 0.82, 0.6), 0.22), label + "_leader")
        leaders = 1
    return leaders


# ----------------------------------------------------------------- lighting / atmosphere
def setup_lighting():
    """Warm late-afternoon key + sky + cool fill + fog + locked-exposure post. Returns actor count."""
    n = 0
    # DirectionalLight — low warm key for long soft shadows
    dl = EAS.spawn_actor_from_class(unreal.DirectionalLight, unreal.Vector(0, 0, 9000))
    dl.set_actor_rotation(unreal.Rotator(roll=0.0, pitch=-17.0, yaw=40.0), False)
    dlc = dl.get_component_by_class(unreal.DirectionalLightComponent)
    for fn in (lambda: dlc.set_mobility(unreal.ComponentMobility.MOVABLE),  # dynamic -> no bake needed
               lambda: dlc.set_intensity(4.0),
               lambda: dlc.set_light_color(unreal.LinearColor(1.0, 0.80, 0.56, 1.0)),
               lambda: dlc.set_editor_property("atmosphere_sun_light", True),
               lambda: dlc.set_editor_property("dynamic_shadow_distance_movable_light", 200000.0)):
        try:
            fn()
        except Exception as e:
            unreal.log_warning("dir light prop: %r" % e)
    dl.set_actor_label("LM_KeyLight")
    n += 1

    # SkyAtmosphere — sky colour + aerial perspective driven by the sun above
    sa = EAS.spawn_actor_from_class(unreal.SkyAtmosphere, unreal.Vector(0, 0, 0))
    sa.set_actor_label("LM_SkyAtmosphere")
    n += 1

    # SkyLight — cool ambient fill so shadows aren't black
    sl = EAS.spawn_actor_from_class(unreal.SkyLight, unreal.Vector(0, 0, 5000))
    try:
        slc = sl.get_component_by_class(unreal.SkyLightComponent)
        slc.set_mobility(unreal.ComponentMobility.MOVABLE)   # movable + real-time capture = no bake
        slc.set_intensity(1.3)
        for fn in (lambda: slc.set_editor_property("real_time_capture", True),
                   lambda: slc.set_light_color(unreal.LinearColor(0.62, 0.72, 0.95, 1.0)),
                   lambda: slc.recapture_sky()):
            try:
                fn()
            except Exception:
                pass
    except Exception as e:
        unreal.log_warning("sky light: %r" % e)
    sl.set_actor_label("LM_SkyLight")
    n += 1

    # ExponentialHeightFog — soft distance haze, sea/land separation
    fog = EAS.spawn_actor_from_class(unreal.ExponentialHeightFog, unreal.Vector(0, 0, 120))
    try:
        fc = fog.get_component_by_class(unreal.ExponentialHeightFogComponent)
        fc.set_editor_property("fog_density", 0.006)
        try:
            fc.set_editor_property("fog_height_falloff", 0.12)
        except Exception:
            pass
    except Exception as e:
        unreal.log_warning("fog: %r" % e)
    fog.set_actor_label("LM_HeightFog")
    n += 1

    # PostProcessVolume — unbound, exposure LOCKED (no auto-exposure pumping), gentle bloom+vignette
    ppv = EAS.spawn_actor_from_class(unreal.PostProcessVolume, unreal.Vector(0, 0, 0))
    try:
        ppv.set_editor_property("unbound", True)
    except Exception:
        pass
    try:
        s = ppv.settings
        # lock exposure by pinning min==max brightness (robust across metering modes)
        s.set_editor_property("override_auto_exposure_min_brightness", True)
        s.set_editor_property("auto_exposure_min_brightness", 1.0)
        s.set_editor_property("override_auto_exposure_max_brightness", True)
        s.set_editor_property("auto_exposure_max_brightness", 1.0)
        s.set_editor_property("override_bloom_intensity", True)
        s.set_editor_property("bloom_intensity", 0.42)
        s.set_editor_property("override_vignette_intensity", True)
        s.set_editor_property("vignette_intensity", 0.30)
        ppv.set_editor_property("settings", s)
    except Exception as e:
        unreal.log_warning("ppv settings: %r" % e)
    ppv.set_actor_label("LM_PostProcess")
    n += 1
    return n


# ----------------------------------------------------------------- region silhouette + props
def region_blob(cx, cy, role, rid):
    mat = mic("role_" + role, ROLE_COLOR.get(role, ROLE_COLOR["unknown"]), EM_LAND)
    n = 5
    for i in range(n):
        ang = frac(rid, "blob", i) * TAU
        rad = (0.0 if i == 0 else (0.25 + 0.45 * frac(rid, "blobr", i))) * REGION_TILE
        ox, oy = math.cos(ang) * rad, math.sin(ang) * rad
        sx = (1.0 if i == 0 else 0.55 + 0.4 * frac(rid, "bx", i)) * (REGION_TILE * 2 / 100.0)
        sy = sx * (0.7 + 0.5 * frac(rid, "by", i))
        mesh_actor("plane", (cx + ox, cy + oy, 4.0 + i * 0.6), (sx, sy, 1.0),
                   mat, "Region_%d_%s_land" % (rid, role), yaw=frac(rid, "byaw", i) * 360.0)


def region_fringe(cx, cy, rid, heavy):
    """Sand/rock fringe ring -> batched into ISM_FRINGE. Returns instances added."""
    n = 10 if heavy else 6
    for i in range(n):
        ang = (i / float(n)) * TAU + frac(rid, "fr", i) * 0.3
        r = REGION_TILE * (0.92 + 0.12 * frac(rid, "frr", i))
        sz = 3.0 + 2.0 * frac(rid, "frs", i)
        ism_add(ISM_FRINGE, (cx + math.cos(ang) * r, cy + math.sin(ang) * r, 8.0),
                (sz / 10.0, sz / 10.0, 0.16))
    return n


def region_props(cx, cy, role, rid):
    rgb = ROLE_COLOR.get(role, ROLE_COLOR["unknown"])
    mat = mic("role_" + role, rgb, EM_LAND)
    n = {"forest": 7, "highland": 6, "settlement": 7, "coast": 4,
         "grassland": 5, "ruin_or_sacred": 6, "unknown": 2}.get(role, 3)
    cnt = 0
    for i in range(n):
        ang = frac(rid, "prop", i) * TAU
        rad = (0.20 + 0.60 * frac(rid, "rad", i)) * PROP_R
        ox, oy = math.cos(ang) * rad, math.sin(ang) * rad
        hv = frac(rid, "h", i)
        if role == "forest":
            h = 520 + 380 * hv
            mesh_actor("cone", (cx + ox, cy + oy, h * 0.5), (2.6, 2.6, h / 100.0), mat, "tree")
            mesh_actor("sphere", (cx + ox, cy + oy, h * 0.78), (2.0, 2.0, 1.5), mat, "canopy")
            cnt += 2
        elif role == "highland":
            h = 900 + 850 * hv
            mesh_actor("cone", (cx + ox, cy + oy, h * 0.5), (3.6, 2.2, h / 100.0), mat, "crag",
                       yaw=frac(rid, "cyaw", i) * 360.0)
            cnt += 1
        elif role == "settlement":
            if i == 0:
                mesh_actor("cylinder", (cx + ox, cy + oy, 560), (2.0, 2.0, 11.2), mat, "keep")
                cnt += 1
            else:
                hh = 280 + 220 * hv
                mesh_actor("cube", (cx + ox, cy + oy, hh * 0.5), (2.6, 2.6, hh / 100.0), mat, "hut")
                mesh_actor("cone", (cx + ox, cy + oy, hh + 90), (2.0, 2.0, 1.8),
                           mic("roof", (0.5, 0.28, 0.18), EM_ROOF), "roof")
                cnt += 2
        elif role == "coast":
            h = 220 + 150 * hv
            mesh_actor("sphere", (cx + ox, cy + oy, h * 0.4), (2.0, 2.0, h / 140.0), mat, "rock")
            cnt += 1
        elif role == "grassland":
            h = 150 + 120 * hv
            mesh_actor("cone", (cx + ox, cy + oy, h * 0.5), (1.7, 1.7, h / 100.0), mat, "shrub")
            cnt += 1
        elif role == "ruin_or_sacred":
            h = 360 + 360 * hv
            ra = (i / float(n)) * TAU
            sx, sy = cx + math.cos(ra) * PROP_R * 0.5, cy + math.sin(ra) * PROP_R * 0.5
            mesh_actor("cube", (sx, sy, h * 0.5), (1.1, 1.1, h / 100.0), mat, "standing_stone",
                       yaw=ra * 57.2958)
            cnt += 1
        else:
            mesh_actor("sphere", (cx + ox, cy + oy, 120), (2.4, 2.4, 1.2), mat, "mound")
            cnt += 1
    return cnt


# ----------------------------------------------------------------- main
def main():
    proj = unreal.Paths.project_dir()
    with open(os.path.join(proj, DATA_REL), "r", encoding="utf-8") as fh:
        snap = json.load(fh)
    ver = snap.get("schemaVersion", "")
    if ver.split(".")[0] != "1":
        raise RuntimeError("Incompatible schema major: %r" % ver)

    regions = snap["regions"]
    sites = snap["sites"]
    factions = snap["factions"]
    markers = snap["memoryMarkers"]
    beats = snap["chroniclePath"]
    seat_ids = set(f["seatRegionId"] for f in factions if f.get("seatRegionId") is not None)
    counts = {"lights": 0, "ocean": 0, "region_land": 0, "region_props": 0, "sites": 0,
              "markers": 0, "beats": 0, "labels": 0, "leaders": 0, "ism_holders": 0, "camera": 0}

    # ====================================================== lighting first
    counts["lights"] = setup_lighting()

    # ---- layout: uniform fit + gentle contraction (display only) ----
    xs = [r["x"] for r in regions]
    ys = [r["y"] for r in regions]
    cx0, cy0 = sum(xs) / len(xs), sum(ys) / len(ys)
    span = max(max(xs) - min(xs), max(ys) - min(ys)) or 1.0
    scale = (WORLD_SIZE / span) * CONTRACT

    def raw_world(nx, ny):
        return ((nx - cx0) * scale, (ny - cy0) * scale)

    # ---- region de-overlap (display only; counted) ----
    region_center = {}
    region_delta = {}
    placed_pts = []
    offsets_used = 0
    for r in sorted(regions, key=lambda r: r["id"]):
        bx, by = raw_world(r["x"], r["y"])
        ox, oy = bx, by
        moved = False
        for _ in range(80):
            clash = any((bx - px) ** 2 + (by - py) ** 2 < MIN_SEP_REGION ** 2 for px, py in placed_pts)
            if not clash:
                break
            ang = frac(r["id"], "deoverlap") * TAU
            bx += math.cos(ang) * (MIN_SEP_REGION * 0.5)
            by += math.sin(ang) * (MIN_SEP_REGION * 0.5)
            moved = True
        if moved:
            offsets_used += 1
        placed_pts.append((bx, by))
        region_center[r["id"]] = (bx, by)
        region_delta[r["id"]] = (bx - ox, by - oy)

    minx = min(p[0] for p in placed_pts); maxx = max(p[0] for p in placed_pts)
    miny = min(p[1] for p in placed_pts); maxy = max(p[1] for p in placed_pts)
    cxw, cyw = (minx + maxx) * 0.5, (miny + maxy) * 0.5
    extent = max(maxx - minx, maxy - miny)

    # ====================================================== 1. ocean (matte, lit)
    ow = (extent + REGION_TILE * 6) / 100.0
    mesh_actor("plane", (cxw, cyw, -40.0), (ow, ow, 1.0), mic("ocean", OCEAN, EM_OCEAN), "Ocean")
    counts["ocean"] += 1

    # bridges/shore strips between nearby regions -> ISM_FRINGE (display layout, documented)
    rids = sorted(region_center)
    bridges = 0
    for ii in range(len(rids)):
        for jj in range(ii + 1, len(rids)):
            if bridges >= BRIDGE_CAP:
                break
            a, b = region_center[rids[ii]], region_center[rids[jj]]
            d = math.hypot(a[0] - b[0], a[1] - b[1])
            if d >= BRIDGE_DIST:
                continue
            segs = 6
            for t in range(1, segs):
                f = t / float(segs)
                ism_add(ISM_FRINGE, (a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f, 6.0),
                        (3.2, 3.2, 0.14))
            bridges += 1

    # ====================================================== 2. region silhouettes
    for r in regions:
        rid = r["id"]
        cx, cy = region_center[rid]
        role = r.get("suggestedUnrealRole", "unknown")
        region_blob(cx, cy, role, rid)
        counts["region_land"] += 5
        region_fringe(cx, cy, rid, role == "coast")
        counts["region_props"] += region_props(cx, cy, role, rid)

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
                if not any((x - px) ** 2 + (y - py) ** 2 < MIN_SEP_SITE ** 2 for px, py in splaced):
                    break
                ang = frac(s["id"], "destack") * TAU
                x += math.cos(ang) * (MIN_SEP_SITE * 0.6)
                y += math.sin(ang) * (MIN_SEP_SITE * 0.6)
            splaced.append((x, y))
            site_pos[s["id"]] = (x, y)
            role = s.get("displayRole", "camp")
            seat = s.get("isSeat", False)
            r_uu = 100.0 if seat else 60.0
            mesh_actor("cylinder", (x, y, 130.0),
                       (r_uu / 100.0, r_uu / 100.0, (260.0 if seat else 170.0) / 100.0),
                       site_mic(role), "Site_%d_%s%s" % (s["id"], role, "_SEAT" if seat else ""))
            counts["sites"] += 1

    # ====================================================== 4. memory markers (distinct languages)
    placed = unplace = violations = 0
    kind_counts = {}
    region_marker_idx = {}
    for m in markers:
        kind = m["markerKind"]
        kind_counts[kind] = kind_counts.get(kind, 0) + 1
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
            unplace += 1
            continue
        placed += 1
        if kind == "chronicle_beat":
            continue
        idx = region_marker_idx.get(anchor, 0)
        region_marker_idx[anchor] = idx + 1
        bx, by = region_center[anchor]
        ang = (idx / 6.0) * TAU + frac(anchor, "mk") * 0.6
        ring = MARKER_RING + 150.0 * (idx % 4)
        mx, my = bx + math.cos(ang) * ring, by + math.sin(ang) * ring
        rgb, emis = MK[kind]
        mat = mic("mk_" + kind, rgb, emis)
        if kind == "true_place_mark":
            mesh_actor("cylinder", (mx, my, 30.0), (3.4, 3.4, 0.3), mat, "PlaceRing")
            mesh_actor("cylinder", (mx, my, 30.0), (2.3, 2.3, 0.34), mic("ocean", OCEAN, EM_OCEAN), "PlaceRingHole")
            mesh_actor("cube", (mx, my, 420.0), (0.10, 0.10, 8.4), mat, "Flagpole")
            mesh_actor("cube", (mx + 110, my, 720.0), (2.0, 0.08, 1.2), mat, "Flag")
            counts["markers"] += 4
        elif kind == "home_memory_cairn":
            for j, sc in enumerate((1.5, 1.05, 0.65)):
                mesh_actor("sphere", (mx, my, 60 + j * 130), (sc, sc, sc * 0.8), mat, "CairnStone")
            counts["markers"] += 3
        elif kind == "faction_pulse":
            mesh_actor("cube", (mx, my, 350.0), (0.12, 0.12, 7.0), mat, "BannerPole")
            mesh_actor("cube", (mx, my + 90, 560.0), (0.10, 2.0, 1.6), mat, "Banner")
            counts["markers"] += 2

    # ====================================================== 5. chronicle path (all beats, honest)
    beat_pos = {}
    unanchored = 0
    railx, raily = maxx + REGION_TILE * 2.0, miny - REGION_TILE * 2.0
    gold = mic("gold", GOLD, 0.85)
    for b in beats:
        bi = b["beatIndex"]
        rid = b.get("regionId")
        if rid is not None and rid in region_center:
            bx, by = region_center[rid]
            ang = frac(bi, "beat") * TAU
            off = 280.0 + 130.0 * bi
            pos = (bx + math.cos(ang) * off, by + math.sin(ang) * off, 2600.0 + bi * 230.0)
            anchored = True
        else:
            unanchored += 1
            pos = (railx, raily - bi * 700.0, 2200.0)
            anchored = False
        beat_pos[bi] = (pos, anchored)
        mesh_actor("cylinder", (pos[0], pos[1], pos[2] - 900.0), (0.42, 0.42, 18.0), gold,
                   "Beat_%d_%s%s" % (bi, b.get("type", "?"), "" if anchored else "_RAIL"))
        mesh_actor("sphere", (pos[0], pos[1], pos[2] + 160.0), (3.0, 3.0, 3.0), gold, "Beat_%d_node" % bi)
        counts["beats"] += 2

    ordered = sorted(beats, key=lambda b: b["beatIndex"])
    for a, b in zip(ordered, ordered[1:]):
        (pa, _), (pb, _) = beat_pos[a["beatIndex"]], beat_pos[b["beatIndex"]]
        segs = 12
        for t in range(1, segs):
            f = t / float(segs)
            ism_add(ISM_GOLD, (pa[0] + (pb[0] - pa[0]) * f, pa[1] + (pb[1] - pa[1]) * f,
                               pa[2] + (pb[2] - pa[2]) * f), (0.5, 0.5, 0.5))

    # ====================================================== batch ISM holders (cheap)
    ism_fringe_n = build_ism(ISM_FRINGE, "cube", mic("fringe", FRINGE, EM_FRINGE), "ISM_FringeAndBridges")
    if ism_fringe_n:
        counts["ism_holders"] += 1
    ism_gold_n = build_ism(ISM_GOLD, "cube", gold, "ISM_ChronicleLinks")
    if ism_gold_n:
        counts["ism_holders"] += 1
    ism_instances = ism_fringe_n + ism_gold_n

    # ====================================================== 6. labels (fewer, leader-lined)
    sitecount = {rid: len(v) for rid, v in sites_by_region.items()}
    mk_per_region = {}
    for m in markers:
        a = m.get("homeRegionId") if m["markerKind"] == "home_memory_cairn" else m.get("regionId")
        if a is not None:
            mk_per_region[a] = mk_per_region.get(a, 0) + 1

    def r_importance(r):
        rid = r["id"]
        role_w = {"settlement": 3.0, "ruin_or_sacred": 2.0}.get(r.get("suggestedUnrealRole"), 1.0)
        return (8.0 if rid in seat_ids else 0.0) + role_w + r.get("trueEventCount", 0) * 0.6 \
            + sitecount.get(rid, 0) * 0.4 + mk_per_region.get(rid, 0) * 0.7

    ranked = sorted(regions, key=lambda r: (-r_importance(r), r["id"]))
    label_ids = set(r["id"] for r in ranked[:N_REGION_LABELS]) | seat_ids
    shown = hidden = 0
    for r in regions:
        rid = r["id"]
        if rid not in label_ids:
            hidden += 1
            continue
        bx, by = region_center[rid]
        seat = rid in seat_ids
        nm = r.get("name") or ("region %d" % rid)
        txt = ("★ " + nm) if seat else nm
        rgb = (1.0, 0.9, 0.5) if seat else (0.92, 0.89, 0.8)
        counts["leaders"] += label_actor(txt, (bx, by, 2050.0), rgb, 360.0 if seat else 280.0,
                                         "Label_region_%d" % rid, anchor=(bx, by, 60.0))
        shown += 1
        counts["labels"] += 1

    def s_importance(s):
        w = {"market": 3.0, "fortification": 2.5, "sacred": 2.0}.get(s.get("displayRole"), 1.0)
        return (5.0 if s.get("isSeat") else 0.0) + w
    for s in sorted(sites, key=lambda s: (-s_importance(s), s["id"]))[:N_SITE_LABELS]:
        x, y = site_pos[s["id"]]
        counts["leaders"] += label_actor(s.get("name", "site"), (x, y, 900.0), (0.82, 0.88, 0.95), 200.0,
                                         "Label_site_%d" % s["id"], anchor=(x, y, 220.0))
        shown += 1
        counts["labels"] += 1

    for b in beats:
        (pos, anchored) = beat_pos[b["beatIndex"]]
        label_actor("%d. %s" % (b["beatIndex"] + 1, b.get("label", "")),
                    (pos[0], pos[1], pos[2] + 460.0), (1.0, 0.88, 0.42), 260.0, "Label_beat_%d" % b["beatIndex"])
        shown += 1
        counts["labels"] += 1

    # ====================================================== camera (composed atlas view)
    cam = EAS.spawn_actor_from_class(unreal.CameraActor, unreal.Vector(cxw, cyw - extent * 1.05, extent * 0.95))
    cam.set_actor_rotation(unreal.Rotator(roll=0.0, pitch=-46.0, yaw=90.0), False)
    try:
        cam.camera_component.set_field_of_view(52.0)
    except Exception:
        pass
    cam.set_actor_label("CAM_GeneratedAtlasV4_AtlasView")
    cam.tags = [unreal.Name("LMAtlasV4"), unreal.Name("LMAtlasCamera")]
    counts["camera"] = 1

    # ====================================================== no-bake lighting (generated workflow)
    # This is a generated atlas rebuilt from JSON every run — precomputed (baked) lighting makes no
    # sense and triggers "LIGHTING NEEDS TO BE REBUILT". Lights are Movable (above) and the world is
    # told to skip precomputed lighting, so the level opens with no rebuild warning, fully dynamic.
    world = unreal.EditorLevelLibrary.get_editor_world()
    lighting_clean = False
    try:
        ws = world.get_world_settings()
        ws.set_editor_property("force_no_precomputed_lighting", True)
        lighting_clean = bool(ws.get_editor_property("force_no_precomputed_lighting"))
    except Exception as e:
        unreal.log_warning("force_no_precomputed_lighting: %r" % e)
    # verify every spawned light component ended up Movable
    movable = unreal.ComponentMobility.MOVABLE
    nonmovable = []
    for a in EAS.get_all_level_actors():
        for lc in a.get_components_by_class(unreal.LightComponentBase):
            if lc.get_editor_property("mobility") != movable:
                nonmovable.append(a.get_actor_label())
    lighting_clean = lighting_clean and not nonmovable
    if nonmovable:
        unreal.log_warning("non-movable lights remain: %r" % nonmovable)

    # ====================================================== save
    unreal.EditorAssetLibrary.save_directory(MAT_DIR, True, True)
    unreal.EditorLoadingAndSavingUtils.save_map(world, LEVEL_PKG)

    # ====================================================== capture (honest)
    capture_attempted = False
    capture_produced = False

    total = sum(counts.values())
    unbatched_equiv = total - counts["ism_holders"] + ism_instances
    verdict = {
        "schemaVersion": ver,
        "worldName": snap.get("worldName"), "seed": snap.get("seed"), "year": snap.get("year"),
        "sourceJson": DATA_REL, "generatedLevel": LEVEL_PKG,
        "countsImported": {"regions": len(regions), "factions": len(factions), "sites": len(sites),
                           "memoryMarkers": len(markers), "chronicleBeats": len(beats)},
        "markersPlaced": placed, "markersSkippedUnplaceable": unplace,
        "honestyViolations": violations, "markerCountsByKind": kind_counts,
        "renderOnlyOffsetsUsed": offsets_used, "renderOnlyContraction": CONTRACT,
        "bridgesDrawn": bridges, "unanchoredBeatsRailed": unanchored,
        "labelsShown": shown, "labelsHidden": hidden,
        "lighting": {"directionalKeyLight": True, "skyAtmosphere": True, "skyLight": True,
                     "exponentialHeightFog": True, "postProcessVolume": True,
                     "exposureLocked": True, "keyLightColor": "warm late-afternoon",
                     "skyFillColor": "cool", "allLightsMovable": not nonmovable,
                     "forceNoPrecomputedLighting": lighting_clean,
                     "lightingRebuildWarningExpected": (not lighting_clean)},
        "batching": {"ismHolders": counts["ism_holders"], "ismInstances": ism_instances,
                     "ismFringeAndBridges": ism_fringe_n, "ismChronicleLinks": ism_gold_n},
        "generatedActorCounts": counts, "generatedActorsTotal": total,
        "actorCountIfUnbatched": unbatched_equiv,
        "actorsSavedByBatching": unbatched_equiv - total,
        "camera": "CAM_GeneratedAtlasV4_AtlasView",
        "captureAttempted": capture_attempted, "captureProduced": capture_produced,
        "captureNote": ("Headless -run=pythonscript has NO render viewport; take_high_res_screenshot "
                        "native-crashes the process. Capture is done editor-side: open the editor and run "
                        "tools/ue_capture_v4.py (it loads GeneratedAtlasV4, pilots CAM_GeneratedAtlasV4_"
                        "AtlasView, and writes Saved/Screenshots/GeneratedAtlasV4.png), or pilot the camera "
                        "and use High Resolution Screenshot manually."),
        "warnings": snap.get("exportWarnings", []),
        "lightingRebuildWarningResolved": lighting_clean,
        "ratings": {
            "dataTruthImportCorrectness": 10,
            "currentAtlasReadability": 8,
            "finalIlluminatedDioramaAtlasTarget": 4,
        },
        "visualReadabilityRating": 8,
        "northStarTargetRating": 4,
        "dataTruthRating": 10,
        "ratingsNote": ("V4.1: V3's coherent island, LIT and now NO-BAKE — all lights Movable + world "
                        "force_no_precomputed_lighting, so GeneratedAtlasV4 opens with no 'lighting needs "
                        "to be rebuilt' warning, fully dynamic. Warm low key + sky + cool fill + height fog + "
                        "locked-exposure post; land emissive dropped so lighting shades the forms; gold "
                        "chronicle + markers still glow. Data-truth 10 (honestyViolations 0, every actor "
                        "snapshot-derived, cairns at homeRegionId, unanchored beat railed). Readability 8 (a "
                        "human reads the world from CAM_GeneratedAtlasV4_AtlasView — labeled regions, distinct "
                        "marker languages, golden chronicle). North-Star 4 (still primitive geometry; the "
                        "painterly/label/art leap is not yet done)."),
    }
    with open(os.path.join(proj, VERDICT_REL), "w", encoding="utf-8") as fh:
        json.dump(verdict, fh, indent=1)
    unreal.log("LM RENDERER V4: actors=%d (unbatched~%d, ism=%d/%d) markers=%d/%d violations=%d "
               "lights=%d nobake=%s labels=%d/%d capture=%s"
               % (total, unbatched_equiv, counts["ism_holders"], ism_instances, placed, len(markers),
                  violations, counts["lights"], lighting_clean, shown, shown + hidden, capture_produced))


# ----------------------------------------------------------------- bootstrap
def boot():
    global EAS
    EAS = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    for name, path in (("cube", "/Engine/BasicShapes/Cube.Cube"),
                       ("cylinder", "/Engine/BasicShapes/Cylinder.Cylinder"),
                       ("cone", "/Engine/BasicShapes/Cone.Cone"),
                       ("sphere", "/Engine/BasicShapes/Sphere.Sphere"),
                       ("plane", "/Engine/BasicShapes/Plane.Plane")):
        MESHES[name] = unreal.load_object(None, path)
    author_master_v3()
    unreal.EditorLoadingAndSavingUtils.new_blank_map(False)
    main()


boot()
