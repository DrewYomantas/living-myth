"""Living Myth — V5 Editable Atlas Proof Set V1 (pure Python + Geometry Script, no C++).

Builds THREE handcrafted, modular, reusable StaticMesh assets with material zones, noise-broken
silhouettes and bottom-center pivots, then a small proof scene (GeneratedAtlasV5_ProofScene) with
neutral atlas lighting and three review cameras. Nothing here reads or writes sim state; V2/V3/V4
assets and maps are never touched.

  SM_LM_Terrain_GrassStoneChunk_A   zones: 0 grass/moss · 1 dirt · 2 stone · 3 foliage accent
  SM_LM_Path_DirtStraight_A         zones: 0 path dirt · 1 pebble/compacted · 2 grass edge   (X-forward)
  SM_LM_Memory_HomeCairn_A          zones: 0 stone · 1 moss · 2 ribbon · 3 warm glow (emissive)

Pivots: every asset's pivot is ground-center bottom (append origin=BASE / base discs at z=0) so a
piece can be placed on a generated cell and raised/lowered/scaled/animated up from the surface.

Run headless to build (no viewport needed):
  UnrealEditor-Cmd <uproject> -run=pythonscript -script=tools/ue_v5_assets.py -unattended -nosplash -nopause
Capture is separate (needs a viewport): tools/ue_v5_capture.py from the open editor.
"""
import math
import os
import unreal

# --------------------------------------------------------------- paths
V5_DIR = "/Game/LivingMyth/V5"
MAT_DIR = "/Game/LivingMyth/Materials"
MASTER = MAT_DIR + "/M_LMV5"
LEVEL_PKG = "/Game/LivingMyth/Maps/GeneratedAtlasV5_ProofScene"
VERDICT_REL = "Saved/v5_assets_verdict.json"

GP = unreal.GeometryScript_Primitives
GD = unreal.GeometryScript_MeshDeformers
GN = unreal.GeometryScript_Normals
BASE = unreal.GeometryScriptPrimitiveOriginMode.BASE
MOVABLE = unreal.ComponentMobility.MOVABLE
EAS = None
_master = [None]
_mics = {}


# --------------------------------------------------------------- deterministic hash (V4 pattern)
def _fnv(*parts):
    h = 2166136261
    for p in parts:
        for ch in str(p):
            h = ((h ^ ord(ch)) * 16777619) & 0xFFFFFFFF
    return h


def frac(*parts):
    return (_fnv(*parts) % 1000000) / 1000000.0


# --------------------------------------------------------------- material master + instances
def author_master():
    if unreal.EditorAssetLibrary.does_asset_exist(MASTER):
        _master[0] = unreal.load_asset(MASTER)
        return
    tools = unreal.AssetToolsHelpers.get_asset_tools()
    mel = unreal.MaterialEditingLibrary
    m = tools.create_asset("M_LMV5", MAT_DIR, unreal.Material, unreal.MaterialFactoryNew())

    ca = mel.create_material_expression(m, unreal.MaterialExpressionVectorParameter, -640, -40)
    ca.set_editor_property("parameter_name", "ColorA")
    ca.set_editor_property("default_value", unreal.LinearColor(0.3, 0.3, 0.3, 1.0))
    cb = mel.create_material_expression(m, unreal.MaterialExpressionVectorParameter, -640, 160)
    cb.set_editor_property("parameter_name", "ColorB")
    cb.set_editor_property("default_value", unreal.LinearColor(0.55, 0.55, 0.55, 1.0))
    noise = mel.create_material_expression(m, unreal.MaterialExpressionNoise, -640, 360)
    try:
        noise.set_editor_property("scale", 0.0025)   # feature size of the painterly mottle
        noise.set_editor_property("output_min", 0.0)
        noise.set_editor_property("output_max", 1.0)
    except Exception:
        pass
    lerp = mel.create_material_expression(m, unreal.MaterialExpressionLinearInterpolate, -360, 60)
    mel.connect_material_expressions(ca, "", lerp, "A")
    mel.connect_material_expressions(cb, "", lerp, "B")
    mel.connect_material_expressions(noise, "", lerp, "Alpha")
    mel.connect_material_property(lerp, "", unreal.MaterialProperty.MP_BASE_COLOR)

    rough = mel.create_material_expression(m, unreal.MaterialExpressionScalarParameter, -360, 320)
    rough.set_editor_property("parameter_name", "Roughness")
    rough.set_editor_property("default_value", 0.9)
    mel.connect_material_property(rough, "", unreal.MaterialProperty.MP_ROUGHNESS)

    ec = mel.create_material_expression(m, unreal.MaterialExpressionVectorParameter, -640, 540)
    ec.set_editor_property("parameter_name", "EmissiveColor")
    ec.set_editor_property("default_value", unreal.LinearColor(0.0, 0.0, 0.0, 1.0))
    es = mel.create_material_expression(m, unreal.MaterialExpressionScalarParameter, -640, 720)
    es.set_editor_property("parameter_name", "EmissiveStrength")
    es.set_editor_property("default_value", 0.0)
    emul = mel.create_material_expression(m, unreal.MaterialExpressionMultiply, -360, 560)
    mel.connect_material_expressions(ec, "", emul, "A")
    mel.connect_material_expressions(es, "", emul, "B")
    mel.connect_material_property(emul, "", unreal.MaterialProperty.MP_EMISSIVE_COLOR)

    mel.recompile_material(m)
    _master[0] = m


def mic(name, ca, cb, rough=0.9, ec=(0, 0, 0), es=0.0):
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


def cyl(dm, mid, loc, radius, height):
    GP.append_cylinder(dm, prim(mid), xf(*loc), float(radius), float(height), radial_steps=18, origin=BASE)


def cone(dm, mid, loc, base_r, top_r, height):
    GP.append_cone(dm, prim(mid), xf(*loc), float(base_r), float(top_r), float(height), radial_steps=10, origin=BASE)


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
    path = V5_DIR + "/" + name
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
    """Try to bake material slots onto the asset itself (best-effort; component slots are the guarantee)."""
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


# --------------------------------------------------------------- the three assets
def build_terrain():
    dm = unreal.DynamicMesh()
    box(dm, 2, (0, 0, 0), 400, 400, 40)      # stone side wall (body)
    box(dm, 1, (0, 0, 30), 414, 414, 11)     # dirt band peeking under the grass lip
    box(dm, 0, (0, 0, 38), 432, 432, 16)     # mossy grass cap (overhang)
    noise(dm, 8.0, 0.02, 101)                # break the square outline + surface undulation
    recompute(dm)
    for i in range(7):                        # scattered small stones (crisp, post-noise)
        a = frac(i, "tst") * math.tau
        r = (0.25 + 0.6 * frac(i, "tsr")) * 170
        ball(dm, 2, (math.cos(a) * r, math.sin(a) * r, 54), 14 + 9 * frac(i, "tss"), rounded=False)
    for i in range(10):                       # grass tufts
        a = frac(i, "ttf") * math.tau
        r = (0.15 + 0.7 * frac(i, "ttr")) * 175
        cone(dm, 3, (math.cos(a) * r, math.sin(a) * r, 52), 9, 1.5, 34 + 30 * frac(i, "tth"))
    for i in range(4):                        # tiny flowers (cone stem + bud), foliage accent
        a = frac(i, "tflf") * math.tau
        r = (0.2 + 0.6 * frac(i, "tflr")) * 165
        px, py = math.cos(a) * r, math.sin(a) * r
        cone(dm, 3, (px, py, 52), 4, 1.0, 40)
        ball(dm, 3, (px, py, 94), 9, rounded=False)
    sm = to_asset(dm, "SM_LM_Terrain_GrassStoneChunk_A")
    mis = [mic("MI_LM_Terrain_GrassMoss_A", (0.18, 0.35, 0.12), (0.36, 0.47, 0.18), 0.9),
           mic("MI_LM_Terrain_Dirt_A", (0.28, 0.19, 0.11), (0.41, 0.29, 0.17), 0.95),
           mic("MI_LM_Terrain_Stone_A", (0.33, 0.33, 0.35), (0.53, 0.51, 0.48), 0.85),
           mic("MI_LM_Terrain_Foliage_A", (0.27, 0.40, 0.14), (0.74, 0.70, 0.30), 0.8)]
    names = ["GrassMoss", "Dirt", "Stone", "FoliageAccent"]
    bake_slots(sm, mis, names)
    return sm, mis


def build_path():
    dm = unreal.DynamicMesh()
    box(dm, 0, (0, 0, 0), 560, 150, 8)        # dirt slab, length along +X
    box(dm, 1, (0, -38, 6), 520, 26, 5)       # worn rut (compacted/pebble)
    box(dm, 1, (0, 38, 6), 520, 26, 5)        # worn rut
    box(dm, 2, (0, -80, 3), 540, 20, 11)      # grass edge strip
    box(dm, 2, (0, 80, 3), 540, 20, 11)       # grass edge strip
    noise(dm, 3.5, 0.03, 202)                 # uneven handmade shape + irregular borders
    recompute(dm)
    for i in range(11):                        # embedded pebbles
        px = (frac(i, "ppx") - 0.5) * 500
        py = (frac(i, "ppy") - 0.5) * 110
        ball(dm, 1, (px, py, 7), 5 + 5 * frac(i, "pps"), rounded=False)
    for i in range(8):                         # edge weeds (kept off the ±X ends so segments connect)
        px = (frac(i, "pwx") - 0.5) * 420
        py = 84 if i % 2 else -84
        cone(dm, 2, (px, py, 6), 6, 1.0, 26 + 22 * frac(i, "pwh"))
    sm = to_asset(dm, "SM_LM_Path_DirtStraight_A")
    mis = [mic("MI_LM_Path_Dirt_A", (0.33, 0.23, 0.14), (0.46, 0.34, 0.20), 0.95),
           mic("MI_LM_Path_Pebble_A", (0.40, 0.38, 0.34), (0.58, 0.55, 0.50), 0.8),
           mic("MI_LM_Path_GrassEdge_A", (0.19, 0.33, 0.12), (0.45, 0.52, 0.22), 0.85)]
    names = ["PathDirt", "Pebble", "GrassEdge"]
    bake_slots(sm, mis, names)
    return sm, mis


def build_cairn():
    dm = unreal.DynamicMesh()
    cyl(dm, 1, (0, 0, 0), 78, 8)              # moss/flower ground base (sits on terrain)
    stack = [(0, 0, 10, 46), (8, -6, 50, 40), (-7, 9, 84, 33), (5, 6, 110, 26), (-4, -5, 130, 19)]
    for j, (sx, sy, sz, sr) in enumerate(stack):
        ball(dm, 0, (sx, sy, sz), sr)         # stacked rounded stones
    ball(dm, 3, (0, 0, 28), 30)               # warm remembrance glow core, nestled at the base
    ball(dm, 3, (-4, -5, 138), 13)            # apex finial glow — the atlas-readable beacon
    try:                                       # cloth/ribbon wrap around the mid stones
        ro = unreal.GeometryScriptRevolveOptions()
        GP.append_torus(dm, prim(2), xf(0, 0, 66), ro, 40.0, 7.0, 16, 8, origin=BASE)
        ribbon = True
    except Exception as e:
        unreal.log_warning("torus ribbon fallback: %r" % e)
        box(dm, 2, (0, 0, 62), 78, 16, 10)
        ribbon = False
    for i in range(6):                         # moss tufts / tiny flowers at base
        a = (i / 6.0) * math.tau + frac(i, "cm") * 0.5
        cone(dm, 1, (math.cos(a) * 60, math.sin(a) * 60, 8), 6, 1.0, 22 + 16 * frac(i, "cmh"))
    noise(dm, 3.0, 0.05, 303)                 # rounded, hand-placed imperfection
    recompute(dm)
    sm = to_asset(dm, "SM_LM_Memory_HomeCairn_A")
    mis = [mic("MI_LM_Memory_Stone_A", (0.35, 0.34, 0.33), (0.55, 0.53, 0.50), 0.8),
           mic("MI_LM_Memory_Moss_A", (0.15, 0.29, 0.11), (0.38, 0.46, 0.20), 0.9),
           mic("MI_LM_Memory_Ribbon_A", (0.50, 0.11, 0.10), (0.74, 0.20, 0.16), 0.55),
           mic("MI_LM_Memory_Glow_A", (1.0, 0.72, 0.34), (1.0, 0.84, 0.50), 0.5, ec=(1.0, 0.62, 0.24), es=22.0)]
    names = ["Stone", "Moss", "Ribbon", "Glow"]
    bake_slots(sm, mis, names)
    return sm, mis, ribbon


# --------------------------------------------------------------- lighting (V4 recipe, no-bake)
def setup_lighting():
    n = 0
    dl = EAS.spawn_actor_from_class(unreal.DirectionalLight, unreal.Vector(0, 0, 9000))
    dl.set_actor_rotation(unreal.Rotator(0.0, -22.0, 35.0), False)
    dlc = dl.get_component_by_class(unreal.DirectionalLightComponent)
    for fn in (lambda: dlc.set_mobility(MOVABLE), lambda: dlc.set_intensity(4.0),
               lambda: dlc.set_light_color(unreal.LinearColor(1.0, 0.81, 0.57, 1.0)),
               lambda: dlc.set_editor_property("atmosphere_sun_light", True),
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
        slc.set_intensity(1.3)
        slc.set_editor_property("real_time_capture", True)
        slc.set_light_color(unreal.LinearColor(0.62, 0.72, 0.95, 1.0))
        slc.recapture_sky()
    except Exception:
        pass
    sl.set_actor_label("LM_SkyLight"); n += 1
    fog = EAS.spawn_actor_from_class(unreal.ExponentialHeightFog, unreal.Vector(0, 0, 40))
    try:
        fc = fog.get_component_by_class(unreal.ExponentialHeightFogComponent)
        fc.set_editor_property("fog_density", 0.004)
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
        s.set_editor_property("bloom_intensity", 0.5)
        s.set_editor_property("override_vignette_intensity", True)
        s.set_editor_property("vignette_intensity", 0.3)
        ppv.set_editor_property("settings", s)
    except Exception:
        pass
    ppv.set_actor_label("LM_PostProcess"); n += 1
    return n


def place(sm, mis, loc, label):
    a = EAS.spawn_actor_from_class(unreal.StaticMeshActor, unreal.Vector(loc[0], loc[1], loc[2]))
    smc = a.static_mesh_component
    smc.set_mobility(MOVABLE)
    smc.set_static_mesh(sm)
    for i, mi in enumerate(mis):
        try:
            smc.set_material(i, mi)
        except Exception:
            pass
    a.set_actor_label(label)
    a.tags = [unreal.Name("LMV5")]
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
    c.tags = [unreal.Name("LMV5"), unreal.Name("LMV5Camera")]


# --------------------------------------------------------------- main
def main():
    import json
    proj = unreal.Paths.project_dir()
    if unreal.EditorAssetLibrary.does_directory_exist(V5_DIR):
        unreal.EditorAssetLibrary.delete_directory(V5_DIR)

    author_master()
    terr_sm, terr_mis = build_terrain()
    path_sm, path_mis = build_path()
    cairn_sm, cairn_mis, ribbon = build_cairn()
    unreal.EditorAssetLibrary.save_directory(MAT_DIR, False, True)
    unreal.EditorAssetLibrary.save_directory(V5_DIR, False, True)

    # ---- proof scene ----
    unreal.EditorLoadingAndSavingUtils.new_blank_map(False)
    lights = setup_lighting()
    # neutral ground plane (engine plane, grass material)
    plane = unreal.load_object(None, "/Engine/BasicShapes/Plane.Plane")
    g = EAS.spawn_actor_from_class(unreal.StaticMeshActor, unreal.Vector(0, 0, 0))
    g.static_mesh_component.set_mobility(MOVABLE)
    g.static_mesh_component.set_static_mesh(plane)
    g.set_actor_scale3d(unreal.Vector(80, 80, 1))
    # calmer, lower-contrast ground so the placed assets read against it (its own MI, distinct from chunk top)
    g.static_mesh_component.set_material(0, mic("MI_LM_Ground_Calm_A", (0.17, 0.27, 0.13), (0.26, 0.35, 0.17), 0.95))
    g.set_actor_label("LMV5_Ground")
    g.tags = [unreal.Name("LMV5")]

    place(terr_sm, terr_mis, (0, 0, 0), "LMV5_TerrainChunk")
    place(path_sm, path_mis, (60, -360, 0), "LMV5_Path")
    place(cairn_sm, cairn_mis, (300, 300, 0), "LMV5_HomeCairn")

    tgt = (60, -40, 50)
    camera((-1700, -1700, 2300), tgt, 50.0, "CAM_V5_Atlas")     # fair elevated-atlas altitude for ~4 m assets
    camera((-1150, -1150, 900), tgt, 55.0, "CAM_V5_Region")
    camera((-680, -680, 430), (60, 40, 90), 60.0, "CAM_V5_Inspect")

    world = unreal.EditorLevelLibrary.get_editor_world()
    try:
        world.get_world_settings().set_editor_property("force_no_precomputed_lighting", True)
    except Exception:
        pass
    unreal.EditorAssetLibrary.save_directory(MAT_DIR, False, True)
    unreal.EditorLoadingAndSavingUtils.save_map(world, LEVEL_PKG)

    verdict = {
        "pass": "Editable Atlas Proof Set V1",
        "assets": {
            "SM_LM_Terrain_GrassStoneChunk_A": {"zones": ["GrassMoss", "Dirt", "Stone", "FoliageAccent"],
                                                "approxFootprintCm": [432, 432], "approxHeightCm": 110,
                                                "pivot": "ground-center bottom (z=0)"},
            "SM_LM_Path_DirtStraight_A": {"zones": ["PathDirt", "Pebble", "GrassEdge"],
                                          "approxLengthCm": 560, "approxWidthCm": 150, "approxHeightCm": 8,
                                          "forwardAxis": "X", "pivot": "ground-center bottom (z=0)"},
            "SM_LM_Memory_HomeCairn_A": {"zones": ["Stone", "Moss", "Ribbon", "Glow"],
                                         "approxFootprintCm": [156, 156], "approxHeightCm": 150,
                                         "ribbonTorus": ribbon, "glow": "emissive material (no light component)",
                                         "pivot": "ground-center bottom (z=0)"},
        },
        "master": MASTER, "materialDir": MAT_DIR, "assetDir": V5_DIR,
        "proofScene": LEVEL_PKG, "lightsSpawned": lights,
        "cameras": ["CAM_V5_Atlas", "CAM_V5_Region", "CAM_V5_Inspect"],
        "honesty": {"readsSimState": False, "writesSimState": False,
                    "touchesV2V3V4": False, "thirdPartyAssets": False, "runtimeGenerativeAI": False},
    }
    with open(os.path.join(proj, VERDICT_REL), "w", encoding="utf-8") as fh:
        json.dump(verdict, fh, indent=1)
    unreal.log("LM V5 ASSETS: built 3 SM + %d MICs, scene %s, lights=%d, ribbonTorus=%s"
               % (len(_mics), LEVEL_PKG, lights, ribbon))


def boot():
    global EAS
    EAS = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    main()


boot()
