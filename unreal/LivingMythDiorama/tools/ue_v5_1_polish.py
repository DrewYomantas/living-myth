# ===== NEXT SESSION STARTS HERE (2026-06-20 checkpoint) =====
# 1. PUSH FIRST: commit 16343b4 is local-only (ahead 1) — `git -C "C:/dev/LIVING MYTH" push origin main`
#    (last push timed out; GCM can't prompt in Claude's shell, so Drew runs it).
# 2. Feel-test V5.1 in the editor: open GeneratedAtlasV5_1_IntegrationPolish, F5 / pilot the 3 cameras,
#    judge the island vs the North-Star refs. Optional small polish: darken/cool the cairn stones more,
#    add foliage silhouette variety (cones are placeholders).
# 3. Then the real bridge: sim-truth-driven placement of this V5.1 kit (terrainType / region cell /
#    site / HomeRegionId -> which asset where), turning the proof scene into a real generated atlas.
# ============================================================
"""Living Myth — V5.1 Asset Integration Polish (pure Python + Geometry Script, no C++).

An ART-integration polish pass over the V5 proof set. V5 proved the factory; this proves the *look*.
It builds a SEPARATE V5.1 proof path so the V5 baseline survives for comparison:

  /Game/LivingMyth/V5_1/SM_LM_*_B            five polished modular assets
  /Game/LivingMyth/Materials/MI_LM_V5_1_*    earthier zone instances (reparented to the EXISTING M_LMV5)
  /Game/LivingMyth/Maps/GeneratedAtlasV5_1_IntegrationPolish    a coherent land-patch proof scene

Problems this pass fixes (judged from the committed V5 captures vs the North-Star reference sheets):
  1. giant flat carpet plane -> a small irregular land PATCH (island) so scale reads
  2. square box terrain tile  -> noised round knoll, no square outline
  3. striped board path        -> overlapping worn-dirt lumps, grass-blended edges, no parallel ruts
  4. washed-out cairn glow     -> darker/cooler stone + hotter amber heart + a proof-only point light
  5. floaty props              -> contact shadows + embedded bases
  6. yellow-green palette       -> earthy moss / warm dirt / cool grey stone

Nothing here reads or writes sim state. V2/V3/V4 and the V5 baseline (assets, map, M_LMV5 master,
the MI_LM_*_A instances) are NEVER touched — only NEW MI_LM_V5_1_* instances and the V5_1 folder/map.

Run headless to build (no viewport needed):
  UnrealEditor-Cmd <uproject> -run=pythonscript -script=tools/ue_v5_1_polish.py -unattended -nosplash -nopause
Capture is separate (needs a viewport): tools/ue_v5_1_capture.py from the open editor.
"""
import math
import os
import unreal

# --------------------------------------------------------------- paths
V51_DIR = "/Game/LivingMyth/V5_1"
MAT_DIR = "/Game/LivingMyth/Materials"
MASTER = MAT_DIR + "/M_LMV5"                 # REUSED read-only (built by ue_v5_assets.py)
LEVEL_PKG = "/Game/LivingMyth/Maps/GeneratedAtlasV5_1_IntegrationPolish"
VERDICT_REL = "Saved/v5_1_polish_verdict.json"

GP = unreal.GeometryScript_Primitives
GD = unreal.GeometryScript_MeshDeformers
GN = unreal.GeometryScript_Normals
BASE = unreal.GeometryScriptPrimitiveOriginMode.BASE
MOVABLE = unreal.ComponentMobility.MOVABLE
EAS = None
_master = [None]
_mics = {}


# --------------------------------------------------------------- deterministic hash (V4/V5 pattern)
def _fnv(*parts):
    h = 2166136261
    for p in parts:
        for ch in str(p):
            h = ((h ^ ord(ch)) * 16777619) & 0xFFFFFFFF
    return h


def frac(*parts):
    return (_fnv(*parts) % 1000000) / 1000000.0


# --------------------------------------------------------------- material master (read-only) + V5.1 instances
def load_master():
    if not unreal.EditorAssetLibrary.does_asset_exist(MASTER):
        raise RuntimeError("M_LMV5 missing — run ue_v5_assets.py first (V5.1 reuses the V5 master).")
    _master[0] = unreal.load_asset(MASTER)


def mic(name, ca, cb, rough=0.9, ec=(0, 0, 0), es=0.0):
    """Create/refresh a NEW MI_LM_V5_1_* instance on the existing M_LMV5 master."""
    if name in _mics:
        return _mics[name]
    path = MAT_DIR + "/" + name
    tools = unreal.AssetToolsHelpers.get_asset_tools()
    if unreal.EditorAssetLibrary.does_asset_exist(path):
        mi = unreal.load_asset(path)
    else:
        mi = tools.create_asset(name, MAT_DIR, unreal.MaterialInstanceConstant,
                                unreal.MaterialInstanceConstantFactoryNew())
        mi.set_editor_property("parent", _master[0])
    mel = unreal.MaterialEditingLibrary
    mel.set_material_instance_vector_parameter_value(mi, "ColorA", unreal.LinearColor(ca[0], ca[1], ca[2], 1.0))
    mel.set_material_instance_vector_parameter_value(mi, "ColorB", unreal.LinearColor(cb[0], cb[1], cb[2], 1.0))
    mel.set_material_instance_scalar_parameter_value(mi, "Roughness", rough)
    mel.set_material_instance_vector_parameter_value(mi, "EmissiveColor", unreal.LinearColor(ec[0], ec[1], ec[2], 1.0))
    mel.set_material_instance_scalar_parameter_value(mi, "EmissiveStrength", es)
    _mics[name] = mi
    return mi


# ---- the earthier V5.1 palette (less yellow-green; warm dirt; cool stone; sacred amber) -------------
def pal_grass():   return mic("MI_LM_V5_1_GrassMoss", (0.13, 0.22, 0.10), (0.30, 0.36, 0.16), 0.92)
def pal_dirt():    return mic("MI_LM_V5_1_Dirt", (0.24, 0.16, 0.10), (0.40, 0.28, 0.17), 0.95)
def pal_stone():   return mic("MI_LM_V5_1_Stone", (0.29, 0.29, 0.31), (0.45, 0.44, 0.43), 0.90)
def pal_foliage(): return mic("MI_LM_V5_1_Foliage", (0.20, 0.32, 0.12), (0.66, 0.56, 0.26), 0.85)
def pal_pdirt():   return mic("MI_LM_V5_1_PathDirt", (0.30, 0.21, 0.13), (0.44, 0.32, 0.19), 0.95)
def pal_pebble():  return mic("MI_LM_V5_1_Pebble", (0.32, 0.31, 0.29), (0.50, 0.48, 0.45), 0.85)
def pal_pedge():   return mic("MI_LM_V5_1_GrassEdge", (0.14, 0.24, 0.11), (0.33, 0.39, 0.17), 0.88)
def pal_cstone():  return mic("MI_LM_V5_1_CairnStone", (0.15, 0.17, 0.21), (0.30, 0.33, 0.39), 0.80)
def pal_cmoss():   return mic("MI_LM_V5_1_CairnMoss", (0.09, 0.15, 0.08), (0.21, 0.26, 0.12), 0.90)
def pal_ribbon():  return mic("MI_LM_V5_1_Ribbon", (0.46, 0.12, 0.10), (0.70, 0.22, 0.16), 0.55)
def pal_glow():    return mic("MI_LM_V5_1_Glow", (0.85, 0.42, 0.14), (0.95, 0.58, 0.22), 0.45,
                              ec=(1.0, 0.50, 0.16), es=9.0)
def pal_water():   return mic("MI_LM_V5_1_Water", (0.025, 0.065, 0.10), (0.05, 0.11, 0.15), 0.12,
                              ec=(0.02, 0.05, 0.09), es=0.25)


# --------------------------------------------------------------- geometry helpers
def prim(mid):
    po = unreal.GeometryScriptPrimitiveOptions()
    po.set_editor_property("material_id", mid)
    return po


def xf(x, y, z):
    t = unreal.Transform()
    t.set_editor_property("translation", unreal.Vector(float(x), float(y), float(z)))
    return t


def box(dm, mid, loc, dx, dy, dz):
    GP.append_box(dm, prim(mid), xf(*loc), float(dx), float(dy), float(dz), origin=BASE)


def disc(dm, mid, loc, radius, height, steps=24):
    GP.append_cylinder(dm, prim(mid), xf(*loc), float(radius), float(height), radial_steps=int(steps), origin=BASE)


def cone(dm, mid, loc, base_r, top_r, height, steps=10):
    GP.append_cone(dm, prim(mid), xf(*loc), float(base_r), float(top_r), float(height), radial_steps=int(steps), origin=BASE)


def ball(dm, mid, loc, radius, rounded=True):
    if rounded:
        GP.append_sphere_lat_long(dm, prim(mid), xf(*loc), float(radius), steps_phi=8, steps_theta=12)
    else:
        GP.append_sphere_box(dm, prim(mid), xf(*loc), float(radius), steps_x=4, steps_y=4, steps_z=4)


def noise(dm, magnitude, freq, seed):
    layer = unreal.GeometryScriptPerlinNoiseLayerOptions()
    layer.set_editor_property("magnitude", float(magnitude))
    layer.set_editor_property("frequency", float(freq))
    layer.set_editor_property("random_seed", int(seed))
    opts = unreal.GeometryScriptPerlinNoiseOptions()
    opts.set_editor_property("base_layer", layer)
    try:
        opts.set_editor_property("apply_along_normal", True)
    except Exception:
        pass
    GD.apply_perlin_noise_to_mesh(dm, unreal.GeometryScriptMeshSelection(), opts)


def recompute(dm):
    GN.recompute_normals(dm, unreal.GeometryScriptCalculateNormalsOptions())


def to_asset(dm, name):
    path = V51_DIR + "/" + name
    opts = unreal.GeometryScriptCreateNewStaticMeshAssetOptions()
    for k, v in (("enable_recompute_normals", True), ("enable_recompute_tangents", True),
                 ("enable_nanite", False), ("enable_collision", False)):
        try:
            opts.set_editor_property(k, v)
        except Exception:
            pass
    sm, outcome = unreal.GeometryScript_NewAssetUtils.create_new_static_mesh_asset_from_mesh(dm, path, opts)
    return sm


def bake_slots(sm, mis, names):
    try:
        arr = []
        for mi, nm in zip(mis, names):
            sm_mat = unreal.StaticMaterial()
            sm_mat.set_editor_property("material_interface", mi)
            sm_mat.set_editor_property("material_slot_name", unreal.Name(nm))
            arr.append(sm_mat)
        sm.set_editor_property("static_materials", arr)
        return True
    except Exception as e:
        unreal.log_warning("bake_slots(%s): %r" % (sm.get_name(), e))
        return False


# --------------------------------------------------------------- the assets
def build_land_patch():
    """Base island: an irregular thick land patch rising out of the water. Zones 0 grass 1 dirt 2 stone 3 foliage.
    NOTE the GeometryScript perlin node SQUARES frequency, so a visible-scale break needs freq ~sqrt(target)."""
    dm = unreal.DynamicMesh()
    # lumpy organic base from a few overlapping offset discs -> guarantees a NON-circular silhouette
    blobs = [(0, 0, 470), (190, 110, 360), (-180, 150, 340), (140, -210, 350), (-210, -130, 330)]
    for (bx, by, br) in blobs:
        disc(dm, 2, (bx, by, 0), br, 56, steps=30)            # stone body
        disc(dm, 1, (bx, by, 50), br - 14, 14, steps=30)      # dirt band peeking under the lip
        disc(dm, 0, (bx, by, 60), br - 22, 20, steps=30)      # mossy grass cap (top ~80)
    noise(dm, 46.0, 0.06, 401)                       # break the rims into shore lumps (0.06^2 effective)
    recompute(dm)
    for i in range(14):                              # boulders ringing the shore (crisp, post-noise)
        a = frac(i, "lps") * math.tau
        r = (0.55 + 0.42 * frac(i, "lpr")) * 500
        ball(dm, 2, (math.cos(a) * r, math.sin(a) * r, 44 + 14 * frac(i, "lpz")), 22 + 16 * frac(i, "lpb"), rounded=False)
    for i in range(34):                             # grass tufts scattered over the cap
        a = frac(i, "ltf") * math.tau
        r = (0.10 + 0.82 * frac(i, "ltr")) * 470
        cone(dm, 3, (math.cos(a) * r, math.sin(a) * r, 78), 10, 1.5, 30 + 34 * frac(i, "lth"))
    for i in range(10):                             # tiny flowers (foliage accent buds)
        a = frac(i, "lff") * math.tau
        r = (0.15 + 0.7 * frac(i, "lfr")) * 440
        px, py = math.cos(a) * r, math.sin(a) * r
        cone(dm, 3, (px, py, 78), 4, 1.0, 40)
        ball(dm, 3, (px, py, 120), 9, rounded=False)
    sm = to_asset(dm, "SM_LM_Terrain_LandPatch_B")
    mis = [pal_grass(), pal_dirt(), pal_stone(), pal_foliage()]
    bake_slots(sm, mis, ["GrassMoss", "Dirt", "Stone", "FoliageAccent"])
    return sm, mis


def build_knoll():
    """De-squared grassy knoll (the old terrain chunk, now round). Zones 0 grass 1 dirt 2 stone 3 foliage."""
    dm = unreal.DynamicMesh()
    disc(dm, 2, (0, 0, 0), 150, 28, steps=26)        # stone body
    disc(dm, 1, (0, 0, 24), 142, 10, steps=26)       # dirt band
    disc(dm, 0, (0, 0, 30), 152, 16, steps=26)       # grass cap overhang (top ~46)
    noise(dm, 18.0, 0.10, 102)                       # break the round outline into a hand-shaped knoll (freq is squared)
    recompute(dm)
    for i in range(6):                               # outcrop stones
        a = frac(i, "kst") * math.tau
        r = (0.30 + 0.55 * frac(i, "ksr")) * 130
        ball(dm, 2, (math.cos(a) * r, math.sin(a) * r, 34), 15 + 11 * frac(i, "kss"), rounded=False)
    for i in range(12):                              # grass tufts
        a = frac(i, "ktf") * math.tau
        r = (0.15 + 0.75 * frac(i, "ktr")) * 140
        cone(dm, 3, (math.cos(a) * r, math.sin(a) * r, 44), 9, 1.5, 26 + 26 * frac(i, "kth"))
    for i in range(4):                               # flowers
        a = frac(i, "kflf") * math.tau
        r = (0.2 + 0.55 * frac(i, "kflr")) * 130
        px, py = math.cos(a) * r, math.sin(a) * r
        cone(dm, 3, (px, py, 44), 4, 1.0, 34)
        ball(dm, 3, (px, py, 80), 8, rounded=False)
    sm = to_asset(dm, "SM_LM_Terrain_GrassStoneChunk_B")
    mis = [pal_grass(), pal_dirt(), pal_stone(), pal_foliage()]
    bake_slots(sm, mis, ["GrassMoss", "Dirt", "Stone", "FoliageAccent"])
    return sm, mis


def build_path():
    """Worn dirt path: overlapping organic lumps (no board, no parallel ruts). +X forward.
    Zones 0 path dirt 1 pebble/compacted 2 grass edge."""
    dm = unreal.DynamicMesh()
    for i in range(8):                               # overlapping worn-dirt lumps along +X
        px = (i / 7.0 - 0.5) * 540
        py = (frac(i, "pjy") - 0.5) * 34
        r = 50 + 12 * frac(i, "plr")
        disc(dm, 0, (px, py, 0), r, 6, steps=12)
    noise(dm, 7.0, 0.13, 202)                        # break every lump edge -> trodden, not cut (freq is squared)
    recompute(dm)
    for i in range(12):                             # darker compacted/pebble centers down the spine
        px = (frac(i, "ppx") - 0.5) * 500
        py = (frac(i, "ppy") - 0.5) * 40
        ball(dm, 1, (px, py, 6), 5 + 5 * frac(i, "pps"), rounded=False)
    for i in range(10):                             # grass tufts blending the edges (kept off the ±X ends so segments tile)
        px = (frac(i, "pwx") - 0.5) * 430
        py = (60 + 14 * frac(i, "pwo")) * (1 if i % 2 else -1)
        cone(dm, 2, (px, py, 5), 6, 1.0, 22 + 20 * frac(i, "pwh"))
    sm = to_asset(dm, "SM_LM_Path_DirtWorn_B")
    mis = [pal_pdirt(), pal_pebble(), pal_pedge()]
    bake_slots(sm, mis, ["PathDirt", "Pebble", "GrassEdge"])
    return sm, mis


def build_cairn():
    """Memory cairn whose glow READS: cool dark stone, a hot amber heart in a stack gap, dark mossy base.
    Zones 0 stone 1 moss 2 ribbon 3 glow (emissive)."""
    dm = unreal.DynamicMesh()
    disc(dm, 1, (0, 0, 0), 84, 7, steps=22)          # dark mossy/earth base ring (contrast for the glow)
    stack = [(0, 0, 8, 48), (7, -6, 50, 41), (-7, 8, 86, 34), (5, 6, 114, 26), (-4, -5, 136, 19)]
    for (sx, sy, sz, sr) in stack:
        ball(dm, 0, (sx, sy, sz), sr)                # stacked cool-stone
    ball(dm, 3, (0, -38, 44), 18)                    # AMBER HEART — seated forward in the lower stack gap, camera-visible
    ball(dm, 3, (-4, -5, 150), 9)                    # apex finial glow — the atlas beacon
    try:                                              # ribbon/cloth wrap
        ro = unreal.GeometryScriptRevolveOptions()
        GP.append_torus(dm, prim(2), xf(0, 0, 70), ro, 42.0, 7.0, 16, 8, origin=BASE)
        ribbon = True
    except Exception as e:
        unreal.log_warning("torus ribbon fallback: %r" % e)
        box(dm, 2, (0, 0, 66), 82, 16, 10)
        ribbon = False
    for i in range(7):                               # moss tufts at base
        a = (i / 7.0) * math.tau + frac(i, "cm") * 0.5
        cone(dm, 1, (math.cos(a) * 64, math.sin(a) * 64, 7), 6, 1.0, 20 + 16 * frac(i, "cmh"))
    noise(dm, 3.0, 0.05, 303)                        # hand-placed imperfection
    recompute(dm)
    sm = to_asset(dm, "SM_LM_Memory_HomeCairn_B")
    mis = [pal_cstone(), pal_cmoss(), pal_ribbon(), pal_glow()]
    bake_slots(sm, mis, ["Stone", "Moss", "Ribbon", "Glow"])
    return sm, mis, ribbon


def build_water():
    """Flat water disc the island rises out of (scale + 'atlas patch' read). Zone 0 water."""
    dm = unreal.DynamicMesh()
    disc(dm, 0, (0, 0, 0), 1150, 12, steps=48)
    noise(dm, 2.0, 0.08, 501)                        # the faintest ripple, not waves (freq is squared)
    recompute(dm)
    sm = to_asset(dm, "SM_LM_Water_Disc_B")
    mis = [pal_water()]
    bake_slots(sm, mis, ["Water"])
    return sm, mis


# --------------------------------------------------------------- lighting (V4 no-bake recipe, mood-tuned)
def setup_lighting():
    n = 0
    dl = EAS.spawn_actor_from_class(unreal.DirectionalLight, unreal.Vector(0, 0, 9000))
    dl.set_actor_rotation(unreal.Rotator(0.0, -30.0, 38.0), False)   # lower sun -> longer grounding shadows
    dlc = dl.get_component_by_class(unreal.DirectionalLightComponent)
    for fn in (lambda: dlc.set_mobility(MOVABLE), lambda: dlc.set_intensity(3.6),
               lambda: dlc.set_light_color(unreal.LinearColor(1.0, 0.85, 0.66, 1.0)),
               lambda: dlc.set_editor_property("atmosphere_sun_light", True),
               lambda: dlc.set_editor_property("contact_shadow_length", 0.06),
               lambda: dlc.set_editor_property("dynamic_shadow_distance_movable_light", 60000.0)):
        try:
            fn()
        except Exception:
            pass
    dl.set_actor_label("LM_KeyLight"); n += 1
    EAS.spawn_actor_from_class(unreal.SkyAtmosphere, unreal.Vector(0, 0, 0)).set_actor_label("LM_SkyAtmosphere"); n += 1
    sl = EAS.spawn_actor_from_class(unreal.SkyLight, unreal.Vector(0, 0, 3000))
    try:
        slc = sl.get_component_by_class(unreal.SkyLightComponent)
        slc.set_mobility(MOVABLE)
        slc.set_intensity(1.0)
        slc.set_editor_property("real_time_capture", True)
        slc.set_light_color(unreal.LinearColor(0.55, 0.66, 0.92, 1.0))   # cool fill -> cooler shadows
        slc.recapture_sky()
    except Exception:
        pass
    sl.set_actor_label("LM_SkyLight"); n += 1
    fog = EAS.spawn_actor_from_class(unreal.ExponentialHeightFog, unreal.Vector(0, 0, 40))
    try:
        fc = fog.get_component_by_class(unreal.ExponentialHeightFogComponent)
        fc.set_editor_property("fog_density", 0.005)
    except Exception:
        pass
    fog.set_actor_label("LM_HeightFog"); n += 1
    ppv = EAS.spawn_actor_from_class(unreal.PostProcessVolume, unreal.Vector(0, 0, 0))
    try:
        ppv.set_editor_property("unbound", True)
        s = ppv.settings
        s.set_editor_property("override_auto_exposure_min_brightness", True)
        s.set_editor_property("auto_exposure_min_brightness", 1.0)
        s.set_editor_property("override_auto_exposure_max_brightness", True)
        s.set_editor_property("auto_exposure_max_brightness", 1.0)
        s.set_editor_property("override_bloom_intensity", True)
        s.set_editor_property("bloom_intensity", 0.28)          # a soft halo on the amber heart, not a starburst
        s.set_editor_property("override_vignette_intensity", True)
        s.set_editor_property("vignette_intensity", 0.34)
        ppv.set_editor_property("settings", s)
    except Exception:
        pass
    ppv.set_actor_label("LM_PostProcess"); n += 1
    return n


def proof_light(loc):
    """One tiny warm PROOF-ONLY point light at the cairn heart. Locked exposure means a real local light
    reads where pure emissive can't. Performance-safe (single light), clearly labeled."""
    pl = EAS.spawn_actor_from_class(unreal.PointLight, unreal.Vector(loc[0], loc[1], loc[2]))
    plc = pl.get_component_by_class(unreal.PointLightComponent)
    for fn in (lambda: plc.set_mobility(MOVABLE),
               lambda: plc.set_editor_property("intensity_units", unreal.LightUnits.CANDELAS),
               lambda: plc.set_intensity(34.0),
               lambda: plc.set_light_color(unreal.LinearColor(1.0, 0.55, 0.20, 1.0)),
               lambda: plc.set_attenuation_radius(170.0),
               lambda: plc.set_editor_property("source_radius", 18.0)):
        try:
            fn()
        except Exception:
            pass
    pl.set_actor_label("LM_V51_CairnLight_PROOFONLY")
    pl.tags = [unreal.Name("LMV51"), unreal.Name("LMV51ProofLight")]
    return pl


# --------------------------------------------------------------- placement + cameras
def place(sm, mis, loc, label, yaw=0.0):
    a = EAS.spawn_actor_from_class(unreal.StaticMeshActor, unreal.Vector(loc[0], loc[1], loc[2]))
    smc = a.static_mesh_component
    smc.set_mobility(MOVABLE)
    smc.set_static_mesh(sm)
    if yaw:
        a.set_actor_rotation(unreal.Rotator(0.0, 0.0, yaw), False)
    for i, mi in enumerate(mis):
        try:
            smc.set_material(i, mi)
        except Exception:
            pass
    a.set_actor_label(label)
    a.tags = [unreal.Name("LMV51")]
    return a


def aim(loc, target):
    dx, dy, dz = target[0] - loc[0], target[1] - loc[1], target[2] - loc[2]
    yaw = math.degrees(math.atan2(dy, dx))
    pitch = math.degrees(math.atan2(dz, math.hypot(dx, dy)))
    return unreal.Rotator(0.0, pitch, yaw)


def camera(loc, target, fov, label):
    c = EAS.spawn_actor_from_class(unreal.CameraActor, unreal.Vector(loc[0], loc[1], loc[2]))
    c.set_actor_rotation(aim(loc, target), False)
    try:
        c.camera_component.set_field_of_view(fov)
    except Exception:
        pass
    c.set_actor_label(label)
    c.tags = [unreal.Name("LMV51"), unreal.Name("LMV51Camera")]


# --------------------------------------------------------------- main
def main():
    import json
    proj = unreal.Paths.project_dir()
    if unreal.EditorAssetLibrary.does_directory_exist(V51_DIR):
        unreal.EditorAssetLibrary.delete_directory(V51_DIR)

    load_master()
    patch_sm, patch_mis = build_land_patch()
    knoll_sm, knoll_mis = build_knoll()
    path_sm, path_mis = build_path()
    cairn_sm, cairn_mis, ribbon = build_cairn()
    water_sm, water_mis = build_water()
    unreal.EditorAssetLibrary.save_directory(MAT_DIR, False, True)
    unreal.EditorAssetLibrary.save_directory(V51_DIR, False, True)

    # ---- proof scene: a coherent place on a small island, no engine carpet ----
    unreal.EditorLoadingAndSavingUtils.new_blank_map(False)
    lights = setup_lighting()

    TOP = 78.0   # land-patch grass-cap top (things sit here, slightly embedded)
    place(water_sm, water_mis, (0, 0, 30), "LMV51_Water")          # water top ~42, island rises out of it
    place(patch_sm, patch_mis, (0, 0, 0), "LMV51_LandPatch")
    place(knoll_sm, knoll_mis, (-150, 150, TOP - 4), "LMV51_Knoll")
    place(path_sm, path_mis, (10, -40, TOP - 2), "LMV51_Path")     # crosses the land toward the cairn (+X)
    cairn_loc = (270, -40, TOP - 3)
    place(cairn_sm, cairn_mis, cairn_loc, "LMV51_HomeCairn")
    proof_light((cairn_loc[0], cairn_loc[1] - 30, cairn_loc[2] + 42))   # at the amber heart

    center = (40, 10, TOP + 20)
    camera((-470, -640, 1180), center, 42.0, "CAM_V51_Atlas")          # high, near top-down; island fills frame
    camera((-760, -700, 540), center, 50.0, "CAM_V51_Region")          # painterly mid
    camera((-360, -470, 300), (cairn_loc[0], cairn_loc[1], TOP + 90), 54.0, "CAM_V51_Inspect")

    world = unreal.EditorLevelLibrary.get_editor_world()
    try:
        world.get_world_settings().set_editor_property("force_no_precomputed_lighting", True)
    except Exception:
        pass
    unreal.EditorAssetLibrary.save_directory(MAT_DIR, False, True)
    unreal.EditorLoadingAndSavingUtils.save_map(world, LEVEL_PKG)

    verdict = {
        "pass": "Asset Integration Polish V5.1",
        "assets": {
            "SM_LM_Terrain_LandPatch_B": {"zones": ["GrassMoss", "Dirt", "Stone", "FoliageAccent"],
                                          "approxRadiusCm": 500, "approxHeightCm": 80, "role": "base island",
                                          "pivot": "ground-center bottom (z=0)"},
            "SM_LM_Terrain_GrassStoneChunk_B": {"zones": ["GrassMoss", "Dirt", "Stone", "FoliageAccent"],
                                                "approxRadiusCm": 150, "approxHeightCm": 46, "role": "knoll",
                                                "pivot": "ground-center bottom (z=0)"},
            "SM_LM_Path_DirtWorn_B": {"zones": ["PathDirt", "Pebble", "GrassEdge"],
                                      "approxLengthCm": 560, "approxWidthCm": 120, "forwardAxis": "X",
                                      "pivot": "ground-center bottom (z=0)"},
            "SM_LM_Memory_HomeCairn_B": {"zones": ["Stone", "Moss", "Ribbon", "Glow"],
                                         "approxFootprintCm": [168, 168], "approxHeightCm": 165,
                                         "ribbonTorus": ribbon, "glow": "emissive + proof-only point light",
                                         "pivot": "ground-center bottom (z=0)"},
            "SM_LM_Water_Disc_B": {"zones": ["Water"], "approxRadiusCm": 1150,
                                   "pivot": "ground-center bottom (z=0)"},
        },
        "master": MASTER, "materialDir": MAT_DIR, "assetDir": V51_DIR,
        "proofScene": LEVEL_PKG, "lightsSpawned": lights,
        "proofOnlyLight": "LM_V51_CairnLight_PROOFONLY (single warm point light at the cairn heart)",
        "cameras": ["CAM_V51_Atlas", "CAM_V51_Region", "CAM_V51_Inspect"],
        "honesty": {"readsSimState": False, "writesSimState": False,
                    "touchesV2V3V4": False, "touchesV5Baseline": False, "touchesMaster": False,
                    "thirdPartyAssets": False, "runtimeGenerativeAI": False},
    }
    with open(os.path.join(proj, VERDICT_REL), "w", encoding="utf-8") as fh:
        json.dump(verdict, fh, indent=1)
    unreal.log("LM V5.1 POLISH: built 5 SM + %d MICs, scene %s, lights=%d (+1 proof point), ribbonTorus=%s"
               % (len(_mics), LEVEL_PKG, lights, ribbon))


def boot():
    global EAS
    EAS = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    main()


boot()
