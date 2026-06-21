# ===== NEXT SESSION STARTS HERE (2026-06-21 checkpoint) =====
# V5.2 is rendered, judged PASS (modest), committed 42804da, PUSHED (origin/main synced).
# THE ONE NEXT ACTION: fix the CAIRN snowman read — it is the single weakest element (lead + leashed
#   verifier both flagged it). Two levers, try relight FIRST (cheaper):
#   (1) RELIGHT: drop or relocate LM_V52_CairnLight_PROOFONLY (the proof point light washes the lower
#       stones pale white). Try removing it entirely and leaning on the emissive heart + bloom, OR move it
#       INSIDE/below the heart gap so only the heart pools, stones stay cool-dark.
#   (2) SCULPT: if relight isn't enough, replace the stacked faceted balls in build_cairn() with flatter
#       ANGULAR SLAB stones (wider-than-tall boxes, slight random yaw) — reads as hewn cairn, not a snowman.
#   Then re-render (UnrealEditor-Cmd ... -run=pythonscript -script=tools/ue_v5_2_living_diorama.py) +
#   re-capture (GUI editor -ExecCmds, copy this->space-free path) + re-judge V5_2_inspect.png vs V5.1.
#   Build/capture gotchas: dirty-only save is now correct; snapshot UnrealEditor PIDs before launching a
#   capture editor and kill ONLY the new one; a timeout-killed push may have still landed (verify ls-remote).
# ============================================================
"""Living Myth — V5.2 Living-Diorama Pass (pure Python + Geometry Script, no C++).

A SOURCE-SHAPE pass over the V5.1 island. V5.1 proved an earthy land patch; V5.2 pushes the form
vocabulary toward the three GPT North-Star reference sheets (LM_V5_*_ref01.png) while staying editable.
Built as a SEPARATE V5.2 path so the V5.1 baseline survives for comparison:

  /Game/LivingMyth/V5_2/SM_LM_*_C|_B          the polished assets
  /Game/LivingMyth/Materials/MI_LM_V5_2_*     zone instances (reparented to the EXISTING M_LMV5)
  /Game/LivingMyth/Maps/GeneratedAtlasV5_2_LivingDiorama    a coherent living-diorama proof scene

The achievable subset (see Docs/UE58_V5_2_LIVING_DIORAMA.md for the full reference analysis):
  1. land/edge MATERIAL ZONES  -> a 4-strata cutaway side: grass cap | warm-soil band | cool-stone
                                  cliff | pale shore lip at the waterline (V5.1 had 3, no shore)
  2. small CLUMP/DECAL details  -> baked rock clusters, flat moss-patch decals, pebbles, tufts, flowers
  3. varied TREE SILHOUETTES    -> broadleaf (asymmetric round crown) / conifer (ragged fir spire) /
                                  shrub (low cluster), scattered as a mixed grove (no uniform cones)
  4. cairn / memory READABILITY -> hand-balanced cool-stone cairn w/ contained amber heart, PLUS a new
                                  StandingStone menhir motif (rune glow + offerings) extending the mark vocab
  5. dirt PATH EDGE BREAKUP     -> worn-dirt lumps w/ irregular grass-fringed edges AND a Y-branch

Nothing here reads or writes sim state. V2/V3/V4, the V5 baseline, the V5.1 proof set, and the M_LMV5
master are NEVER touched -- only NEW MI_LM_V5_2_* instances and the V5_2 folder/map.

Run headless to build (no viewport needed):
  UnrealEditor-Cmd <uproject> -run=pythonscript -script=tools/ue_v5_2_living_diorama.py -unattended -nosplash -nopause
Capture is separate (needs a viewport): tools/ue_v5_2_capture.py from the open editor.
"""
import math
import os
import unreal

# --------------------------------------------------------------- paths
V52_DIR = "/Game/LivingMyth/V5_2"
MAT_DIR = "/Game/LivingMyth/Materials"
MASTER = MAT_DIR + "/M_LMV5"                 # REUSED read-only (built by ue_v5_assets.py)
LEVEL_PKG = "/Game/LivingMyth/Maps/GeneratedAtlasV5_2_LivingDiorama"
VERDICT_REL = "Saved/v5_2_living_diorama_verdict.json"

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


# --------------------------------------------------------------- material master (read-only) + V5.2 instances
def load_master():
    if not unreal.EditorAssetLibrary.does_asset_exist(MASTER):
        raise RuntimeError("M_LMV5 missing -- run ue_v5_assets.py first (V5.2 reuses the V5 master).")
    _master[0] = unreal.load_asset(MASTER)


def mic(name, ca, cb, rough=0.9, ec=(0, 0, 0), es=0.0):
    """Create/refresh a NEW MI_LM_V5_2_* instance on the existing M_LMV5 master."""
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


# ---- the V5.2 palette: earthy moss / warm soil / cool stone / pale shore + leaf-typed canopies --------
def pal_grass():    return mic("MI_LM_V5_2_GrassMoss", (0.12, 0.21, 0.09), (0.31, 0.37, 0.16), 0.92)
def pal_soil():     return mic("MI_LM_V5_2_Soil", (0.25, 0.16, 0.10), (0.41, 0.29, 0.17), 0.95)
def pal_stone():    return mic("MI_LM_V5_2_Stone", (0.27, 0.27, 0.30), (0.44, 0.43, 0.43), 0.90)
def pal_shore():    return mic("MI_LM_V5_2_Shore", (0.42, 0.38, 0.30), (0.62, 0.57, 0.46), 0.85)
def pal_foliage():  return mic("MI_LM_V5_2_FoliageAccent", (0.19, 0.31, 0.11), (0.64, 0.55, 0.24), 0.85)
# trees: a trunk + three leaf-typed canopies so a grove reads as a MIX, not one cone color
def pal_trunk():    return mic("MI_LM_V5_2_TreeTrunk", (0.18, 0.12, 0.07), (0.31, 0.22, 0.13), 0.92)
def pal_broad():    return mic("MI_LM_V5_2_BroadleafCanopy", (0.15, 0.27, 0.11), (0.34, 0.44, 0.18), 0.88)
def pal_conifer():  return mic("MI_LM_V5_2_ConiferCanopy", (0.10, 0.20, 0.14), (0.20, 0.32, 0.21), 0.86)
def pal_shrub():    return mic("MI_LM_V5_2_ShrubCanopy", (0.22, 0.30, 0.12), (0.42, 0.49, 0.21), 0.88)
# path
def pal_pdirt():    return mic("MI_LM_V5_2_PathDirt", (0.19, 0.12, 0.07), (0.30, 0.20, 0.11), 0.96)
def pal_pebble():   return mic("MI_LM_V5_2_Pebble", (0.32, 0.31, 0.29), (0.50, 0.48, 0.45), 0.85)
def pal_pedge():    return mic("MI_LM_V5_2_GrassEdge", (0.13, 0.23, 0.10), (0.33, 0.39, 0.17), 0.88)
# memory marks
def pal_cstone():   return mic("MI_LM_V5_2_CairnStone", (0.11, 0.13, 0.17), (0.23, 0.26, 0.32), 0.82)
def pal_cmoss():    return mic("MI_LM_V5_2_CairnMoss", (0.09, 0.15, 0.08), (0.21, 0.26, 0.12), 0.90)
def pal_ribbon():   return mic("MI_LM_V5_2_Ribbon", (0.46, 0.12, 0.10), (0.70, 0.22, 0.16), 0.55)
def pal_glow():     return mic("MI_LM_V5_2_Glow", (0.85, 0.42, 0.14), (0.95, 0.58, 0.22), 0.45,
                               ec=(1.0, 0.50, 0.16), es=9.0)
def pal_menhir():   return mic("MI_LM_V5_2_MenhirStone", (0.20, 0.21, 0.24), (0.37, 0.38, 0.41), 0.82)
def pal_rune():     return mic("MI_LM_V5_2_RuneGlow", (0.90, 0.52, 0.18), (0.98, 0.66, 0.26), 0.40,
                               ec=(1.0, 0.58, 0.20), es=7.0)
# water
def pal_water():    return mic("MI_LM_V5_2_Water", (0.025, 0.065, 0.10), (0.05, 0.11, 0.15), 0.12,
                               ec=(0.02, 0.05, 0.09), es=0.25)
def pal_foam():     return mic("MI_LM_V5_2_ShoreFoam", (0.38, 0.44, 0.45), (0.54, 0.59, 0.58), 0.35,
                               ec=(0.05, 0.07, 0.08), es=0.25)


# --------------------------------------------------------------- geometry helpers (verbatim from V5.1)
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
    path = V52_DIR + "/" + name
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
    """Base island with a 4-STRATA cutaway side. Zones 0 grass 1 soil 2 stone 3 shore 4 foliage.
    Band heights are tuned so each stratum reads from the side (the reference's editable-tile look).
    NOTE the GeometryScript perlin node SQUARES frequency, so a visible-scale break needs freq ~sqrt(target)."""
    dm = unreal.DynamicMesh()
    blobs = [(0, 0, 470), (190, 110, 360), (-180, 150, 340), (140, -210, 350), (-210, -130, 330)]
    for (bx, by, br) in blobs:
        disc(dm, 3, (bx, by, 0), br, 16, steps=30)            # pale SHORE lip at the waterline
        disc(dm, 2, (bx, by, 12), br - 10, 58, steps=30)      # cool STONE cliff mass
        disc(dm, 1, (bx, by, 68), br - 26, 20, steps=30)      # warm SOIL band
        disc(dm, 0, (bx, by, 86), br - 34, 22, steps=30)      # mossy GRASS cap (top ~108)
    noise(dm, 46.0, 0.06, 421)                       # break the rims into shore lumps (0.06^2 effective)
    recompute(dm)
    for i in range(14):                              # boulders punching the shore rim (crisp, post-noise)
        a = frac(i, "lps") * math.tau
        r = (0.55 + 0.42 * frac(i, "lpr")) * 500
        ball(dm, 2, (math.cos(a) * r, math.sin(a) * r, 30 + 14 * frac(i, "lpz")), 22 + 16 * frac(i, "lpb"), rounded=False)
    for i in range(9):                               # ROCK CLUSTERS on the cap (grouped 2-3 stones = a clump)
        a = frac(i, "rcg") * math.tau
        r = (0.18 + 0.62 * frac(i, "rcr")) * 430
        cx, cy = math.cos(a) * r, math.sin(a) * r
        for j in range(2 + int(frac(i, "rcn") * 2)):
            ox = (frac(i, j, "rcx") - 0.5) * 36
            oy = (frac(i, j, "rcy") - 0.5) * 36
            ball(dm, 2, (cx + ox, cy + oy, 108), 8 + 9 * frac(i, j, "rcs"), rounded=False)
    for i in range(11):                              # flat MOSS-PATCH decals on the cap
        a = frac(i, "mpg") * math.tau
        r = (0.10 + 0.74 * frac(i, "mpr")) * 420
        disc(dm, 4, (math.cos(a) * r, math.sin(a) * r, 107), 22 + 16 * frac(i, "mps"), 3, steps=12)
    for i in range(34):                              # grass tufts scattered over the cap
        a = frac(i, "ltf") * math.tau
        r = (0.10 + 0.82 * frac(i, "ltr")) * 460
        cone(dm, 0, (math.cos(a) * r, math.sin(a) * r, 108), 10, 1.5, 28 + 32 * frac(i, "lth"))
    for i in range(10):                              # tiny flowers (foliage accent buds)
        a = frac(i, "lff") * math.tau
        r = (0.15 + 0.7 * frac(i, "lfr")) * 430
        px, py = math.cos(a) * r, math.sin(a) * r
        cone(dm, 4, (px, py, 108), 4, 1.0, 38)
        ball(dm, 4, (px, py, 150), 9, rounded=False)
    sm = to_asset(dm, "SM_LM_Terrain_LandPatch_C")
    mis = [pal_grass(), pal_soil(), pal_stone(), pal_shore(), pal_foliage()]
    bake_slots(sm, mis, ["GrassMoss", "Soil", "Stone", "Shore", "FoliageAccent"])
    return sm, mis


def build_broadleaf():
    """Broadleaf tree: visible trunk + an ASYMMETRIC 3-ball round crown (reads as a tree, not a cone).
    Zones 0 trunk 1 canopy."""
    dm = unreal.DynamicMesh()
    disc(dm, 0, (0, 0, 0), 9, 96, steps=10)                  # trunk
    ball(dm, 1, (0, 0, 118), 46)                             # main crown
    ball(dm, 1, (24, 10, 104), 32)                           # offset lobes -> non-circular silhouette
    ball(dm, 1, (-20, -14, 110), 30)
    ball(dm, 1, (6, 22, 100), 26)
    noise(dm, 5.0, 0.09, 611)                                # ruffle the crown
    recompute(dm)
    sm = to_asset(dm, "SM_LM_Foliage_Broadleaf_B")
    mis = [pal_trunk(), pal_broad()]
    bake_slots(sm, mis, ["TreeTrunk", "BroadleafCanopy"])
    return sm, mis


def build_conifer():
    """Conifer: trunk + a RAGGED stacked fir spire (narrowing cones, slight offsets). Zones 0 trunk 1 canopy."""
    dm = unreal.DynamicMesh()
    disc(dm, 0, (0, 0, 0), 7, 40, steps=8)                   # short trunk
    tiers = [(0, 0, 28, 56, 96), (5, -4, 96, 42, 80), (-4, 5, 158, 30, 66), (2, 2, 210, 18, 52)]
    for (tx, ty, tz, tr, th) in tiers:                       # narrowing fir tiers
        cone(dm, 1, (tx, ty, tz), tr, tr * 0.18, th, steps=12)
    noise(dm, 4.0, 0.11, 622)                                # ragged needles
    recompute(dm)
    sm = to_asset(dm, "SM_LM_Foliage_Conifer_B")
    mis = [pal_trunk(), pal_conifer()]
    bake_slots(sm, mis, ["TreeTrunk", "ConiferCanopy"])
    return sm, mis


def build_shrub():
    """Shrub/bush: a low cluster of overlapping balls (no trunk). Zone 0 canopy."""
    dm = unreal.DynamicMesh()
    lobes = [(0, 0, 22, 28), (20, 8, 18, 22), (-16, 14, 18, 20), (8, -18, 16, 18)]
    for (lx, ly, lz, lr) in lobes:
        ball(dm, 0, (lx, ly, lz), lr)
    noise(dm, 3.5, 0.12, 633)
    recompute(dm)
    sm = to_asset(dm, "SM_LM_Foliage_Shrub_B")
    mis = [pal_shrub()]
    bake_slots(sm, mis, ["ShrubCanopy"])
    return sm, mis


def build_path():
    """Worn dirt path with a Y-BRANCH and irregular grass-fringed edges (no board, no parallel ruts).
    Main spine runs +X; a branch peels off toward +Y. Zones 0 path dirt 1 pebble 2 grass edge."""
    dm = unreal.DynamicMesh()
    # main spine: overlapping worn-dirt lumps along +X
    for i in range(8):
        px = (i / 7.0 - 0.5) * 540
        py = (frac(i, "pjy") - 0.5) * 34
        r = 60 + 12 * frac(i, "plr")
        disc(dm, 0, (px, py, 0), r, 8, steps=12)
    # Y-branch: lumps peeling off the spine toward +Y (the kit's "roads branch" language)
    for i in range(6):
        t = i / 5.0
        px = 40 + t * 160
        py = 20 + t * 230
        r = 52 - 8 * t
        disc(dm, 0, (px, py, 0), r, 8, steps=12)
    noise(dm, 7.0, 0.13, 222)                        # break every lump edge -> trodden, not cut (freq is squared)
    recompute(dm)
    for i in range(12):                              # darker compacted/pebble centers down the spine
        px = (frac(i, "ppx") - 0.5) * 500
        py = (frac(i, "ppy") - 0.5) * 40
        ball(dm, 1, (px, py, 6), 5 + 5 * frac(i, "pps"), rounded=False)
    for i in range(14):                              # grass tufts blending the edges, irregular offsets
        px = (frac(i, "pwx") - 0.5) * 430
        side = 1 if i % 2 else -1
        py = (58 + 22 * frac(i, "pwo")) * side
        cone(dm, 2, (px, py, 5), 6, 1.0, 20 + 22 * frac(i, "pwh"))
    for i in range(5):                               # tufts fringing the branch
        t = i / 4.0
        px = 40 + t * 150 + (frac(i, "pbx") - 0.5) * 30
        py = 20 + t * 220 + 46 * (1 if i % 2 else -1)
        cone(dm, 2, (px, py, 5), 6, 1.0, 20 + 18 * frac(i, "pbh"))
    sm = to_asset(dm, "SM_LM_Path_DirtBranch_B")
    mis = [pal_pdirt(), pal_pebble(), pal_pedge()]
    bake_slots(sm, mis, ["PathDirt", "Pebble", "GrassEdge"])
    return sm, mis


def build_cairn():
    """Memory cairn: hand-balanced cool-stone stack on a dark moss ring, a CONTAINED amber heart in a
    stack gap + apex coal. Zones 0 stone 1 moss 2 ribbon 3 glow (emissive)."""
    dm = unreal.DynamicMesh()
    disc(dm, 1, (0, 0, 0), 84, 7, steps=22)          # dark mossy/earth base ring (contrast for the glow)
    # ROUGH-HEWN faceted stones (sphere_box, not smooth spheres) hand-stacked with offsets -> a cairn, NOT a snowman
    stack = [(0, 0, 8, 48), (11, -8, 50, 40), (-10, 11, 86, 33), (7, 8, 114, 25), (-5, -6, 136, 18)]
    for (sx, sy, sz, sr) in stack:
        ball(dm, 0, (sx, sy, sz), sr, rounded=False)
    ball(dm, 3, (0, -38, 44), 18)                    # AMBER HEART -- seated forward in the lower stack gap
    ball(dm, 3, (-4, -5, 150), 9)                    # apex coal -- the atlas beacon
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
    noise(dm, 3.0, 0.05, 313)                        # hand-placed imperfection
    recompute(dm)
    sm = to_asset(dm, "SM_LM_Memory_HomeCairn_C")
    mis = [pal_cstone(), pal_cmoss(), pal_ribbon(), pal_glow()]
    bake_slots(sm, mis, ["Stone", "Moss", "Ribbon", "Glow"])
    return sm, mis, ribbon


def build_standing_stone():
    """NEW memory motif: a carved standing stone (menhir) with a faint contained rune glow + a small ring
    of offering stones on a moss base. Extends the mark vocabulary past the cairn. Zones 0 menhir 1 moss 2 rune."""
    dm = unreal.DynamicMesh()
    disc(dm, 1, (0, 0, 0), 70, 6, steps=20)          # dark moss/earth base ring
    box(dm, 0, (0, 0, 4), 46, 26, 196)               # the standing slab (tall, leaning forward read)
    box(dm, 2, (0, -14, 90), 16, 4, 40)              # carved RUNE channel (contained glow, faces camera)
    ball(dm, 2, (0, -14, 150), 7)                    # rune apex coal
    for i in range(5):                               # offering stones ringing the base
        a = (i / 5.0) * math.tau + frac(i, "ss") * 0.6
        ball(dm, 0, (math.cos(a) * 52, math.sin(a) * 52, 6), 11 + 7 * frac(i, "ssr"), rounded=False)
    for i in range(6):                               # moss tufts
        a = (i / 6.0) * math.tau + frac(i, "ssm") * 0.5
        cone(dm, 1, (math.cos(a) * 58, math.sin(a) * 58, 6), 5, 1.0, 16 + 14 * frac(i, "ssh"))
    noise(dm, 3.0, 0.06, 323)
    recompute(dm)
    sm = to_asset(dm, "SM_LM_Memory_StandingStone_B")
    mis = [pal_menhir(), pal_cmoss(), pal_rune()]
    bake_slots(sm, mis, ["MenhirStone", "Moss", "Rune"])
    return sm, mis


def build_water():
    """Flat water disc with a pale shore-FOAM ring where the island meets the sea. Zones 0 water 1 foam.
    Foam disc is slightly TALLER than the open water (14 vs 12) so it WINS the overlap in 0..600 and reads
    as a pale halo in the ~500..600 gap between the island shore and the open sea (its 0..500 core is hidden
    under the island that rises through it)."""
    dm = unreal.DynamicMesh()
    disc(dm, 1, (0, 0, 0), 560, 14, steps=44)        # foam halo (taller -> wins overlap at the shore gap)
    disc(dm, 0, (0, 0, 0), 1150, 12, steps=48)       # open water beyond the foam ring
    noise(dm, 2.0, 0.08, 521)                        # the faintest ripple, not waves (freq is squared)
    recompute(dm)
    sm = to_asset(dm, "SM_LM_Water_Disc_C")
    mis = [pal_water(), pal_foam()]
    bake_slots(sm, mis, ["Water", "ShoreFoam"])
    return sm, mis


# --------------------------------------------------------------- lighting (V5.1 recipe, mood-tuned)
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
        s.set_editor_property("bloom_intensity", 0.28)          # a soft halo on the warm hearts, not a starburst
        s.set_editor_property("override_vignette_intensity", True)
        s.set_editor_property("vignette_intensity", 0.34)
        ppv.set_editor_property("settings", s)
    except Exception:
        pass
    ppv.set_actor_label("LM_PostProcess"); n += 1
    return n


def proof_light(loc, label, intensity=34.0, radius=170.0, color=(1.0, 0.55, 0.20)):
    """Tiny warm PROOF-ONLY point light at a memory-mark glow. Locked exposure means a real local light
    reads where pure emissive can't. Performance-safe, clearly labeled."""
    pl = EAS.spawn_actor_from_class(unreal.PointLight, unreal.Vector(loc[0], loc[1], loc[2]))
    plc = pl.get_component_by_class(unreal.PointLightComponent)
    for fn in (lambda: plc.set_mobility(MOVABLE),
               lambda: plc.set_editor_property("intensity_units", unreal.LightUnits.CANDELAS),
               lambda: plc.set_intensity(intensity),
               lambda: plc.set_light_color(unreal.LinearColor(color[0], color[1], color[2], 1.0)),
               lambda: plc.set_attenuation_radius(radius),
               lambda: plc.set_editor_property("source_radius", 16.0)):
        try:
            fn()
        except Exception:
            pass
    pl.set_actor_label(label)
    pl.tags = [unreal.Name("LMV52"), unreal.Name("LMV52ProofLight")]
    return pl


# --------------------------------------------------------------- placement + cameras
def place(sm, mis, loc, label, yaw=0.0, scale=1.0):
    a = EAS.spawn_actor_from_class(unreal.StaticMeshActor, unreal.Vector(loc[0], loc[1], loc[2]))
    smc = a.static_mesh_component
    smc.set_mobility(MOVABLE)
    smc.set_static_mesh(sm)
    if yaw:
        a.set_actor_rotation(unreal.Rotator(0.0, 0.0, yaw), False)
    if scale != 1.0:
        a.set_actor_scale3d(unreal.Vector(scale, scale, scale))
    for i, mi in enumerate(mis):
        try:
            smc.set_material(i, mi)
        except Exception:
            pass
    a.set_actor_label(label)
    a.tags = [unreal.Name("LMV52")]
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
    c.tags = [unreal.Name("LMV52"), unreal.Name("LMV52Camera")]


# --------------------------------------------------------------- main
def main():
    import json
    proj = unreal.Paths.project_dir()
    if unreal.EditorAssetLibrary.does_directory_exist(V52_DIR):
        unreal.EditorAssetLibrary.delete_directory(V52_DIR)

    load_master()
    patch_sm, patch_mis = build_land_patch()
    broad_sm, broad_mis = build_broadleaf()
    conif_sm, conif_mis = build_conifer()
    shrub_sm, shrub_mis = build_shrub()
    path_sm, path_mis = build_path()
    cairn_sm, cairn_mis, ribbon = build_cairn()
    menhir_sm, menhir_mis = build_standing_stone()
    water_sm, water_mis = build_water()
    # dirty-only save: only the NEW MI_LM_V5_2_* are dirty, so V2..V5.1 materials are never re-written
    # (forcing only_if_is_dirty=False collided with the open editor's locks AND risked touching baselines).
    unreal.EditorAssetLibrary.save_directory(MAT_DIR, True, True)
    unreal.EditorAssetLibrary.save_directory(V52_DIR, True, True)

    # ---- proof scene: a coherent living-diorama place on a small island ----
    unreal.EditorLoadingAndSavingUtils.new_blank_map(False)
    lights = setup_lighting()

    TOP = 108.0   # land-patch grass-cap top (things sit here, slightly embedded)
    place(water_sm, water_mis, (0, 0, 30), "LMV52_Water")          # water top ~42, island rises out of it
    place(patch_sm, patch_mis, (0, 0, 0), "LMV52_LandPatch")

    # mixed grove -- broadleaf / conifer / shrub with per-instance yaw + scale jitter (no uniform cones)
    grove = [
        (broad_sm, broad_mis, (-210, 170, TOP - 4), 0.95, "Broadleaf_1"),
        (broad_sm, broad_mis, (-110, 240, TOP - 4), 1.12, "Broadleaf_2"),
        (conif_sm, conif_mis, (-260, 60, TOP - 4), 1.00, "Conifer_1"),
        (conif_sm, conif_mis, (-180, -40, TOP - 4), 0.86, "Conifer_2"),
        (shrub_sm, shrub_mis, (-120, 120, TOP - 3), 1.10, "Shrub_1"),
        (shrub_sm, shrub_mis, (-300, 140, TOP - 3), 0.90, "Shrub_2"),
        (shrub_sm, shrub_mis, (-60, 60, TOP - 3), 1.00, "Shrub_3"),
    ]
    for (sm, mis, loc, sc, nm) in grove:
        place(sm, mis, loc, "LMV52_" + nm, yaw=frac(nm, "yaw") * 360.0, scale=sc)

    place(path_sm, path_mis, (10, -40, TOP - 2), "LMV52_Path")     # spine crosses toward the cairn (+X), branch -> +Y
    cairn_loc = (290, -60, TOP - 3)
    place(cairn_sm, cairn_mis, cairn_loc, "LMV52_HomeCairn")
    menhir_loc = (180, 230, TOP - 4)                               # at the path branch end (second memory mark)
    place(menhir_sm, menhir_mis, menhir_loc, "LMV52_StandingStone", yaw=-26.0)

    proof_light((cairn_loc[0], cairn_loc[1] - 30, cairn_loc[2] + 36), "LM_V52_CairnLight_PROOFONLY",
                intensity=30.0, radius=140.0)   # pooled tighter at the heart so the cool stones stay dark
    proof_light((menhir_loc[0], menhir_loc[1] - 14, menhir_loc[2] + 95), "LM_V52_RuneLight_PROOFONLY",
                intensity=20.0, radius=120.0)

    center = (40, 40, TOP + 20)
    camera((-540, -660, 1240), center, 42.0, "CAM_V52_Atlas")          # high, near top-down; island fills frame
    camera((-820, -740, 560), center, 50.0, "CAM_V52_Region")          # painterly mid
    camera((-360, -470, 300), (cairn_loc[0], cairn_loc[1], TOP + 90), 54.0, "CAM_V52_Inspect")

    world = unreal.EditorLevelLibrary.get_editor_world()
    try:
        world.get_world_settings().set_editor_property("force_no_precomputed_lighting", True)
    except Exception:
        pass
    unreal.EditorAssetLibrary.save_directory(MAT_DIR, True, True)
    unreal.EditorLoadingAndSavingUtils.save_map(world, LEVEL_PKG)

    verdict = {
        "pass": "Living-Diorama V5.2",
        "assets": {
            "SM_LM_Terrain_LandPatch_C": {"zones": ["GrassMoss", "Soil", "Stone", "Shore", "FoliageAccent"],
                                          "approxRadiusCm": 500, "approxHeightCm": 108, "role": "base island (4-strata side)",
                                          "pivot": "ground-center bottom (z=0)"},
            "SM_LM_Foliage_Broadleaf_B": {"zones": ["TreeTrunk", "BroadleafCanopy"], "role": "broadleaf tree",
                                          "pivot": "ground-center bottom (z=0)"},
            "SM_LM_Foliage_Conifer_B": {"zones": ["TreeTrunk", "ConiferCanopy"], "role": "conifer fir spire",
                                        "pivot": "ground-center bottom (z=0)"},
            "SM_LM_Foliage_Shrub_B": {"zones": ["ShrubCanopy"], "role": "shrub/bush",
                                      "pivot": "ground-center bottom (z=0)"},
            "SM_LM_Path_DirtBranch_B": {"zones": ["PathDirt", "Pebble", "GrassEdge"], "role": "worn path w/ Y-branch",
                                        "forwardAxis": "X", "pivot": "ground-center bottom (z=0)"},
            "SM_LM_Memory_HomeCairn_C": {"zones": ["Stone", "Moss", "Ribbon", "Glow"], "role": "memory cairn",
                                         "ribbonTorus": ribbon, "glow": "emissive + proof-only point light",
                                         "pivot": "ground-center bottom (z=0)"},
            "SM_LM_Memory_StandingStone_B": {"zones": ["MenhirStone", "Moss", "Rune"], "role": "standing-stone memory mark",
                                             "glow": "contained rune emissive + proof-only point light",
                                             "pivot": "ground-center bottom (z=0)"},
            "SM_LM_Water_Disc_C": {"zones": ["Water", "ShoreFoam"], "approxRadiusCm": 1150,
                                   "pivot": "ground-center bottom (z=0)"},
        },
        "master": MASTER, "materialDir": MAT_DIR, "assetDir": V52_DIR,
        "proofScene": LEVEL_PKG, "lightsSpawned": lights,
        "proofOnlyLights": ["LM_V52_CairnLight_PROOFONLY", "LM_V52_RuneLight_PROOFONLY"],
        "cameras": ["CAM_V52_Atlas", "CAM_V52_Region", "CAM_V52_Inspect"],
        "honesty": {"readsSimState": False, "writesSimState": False,
                    "touchesV2V3V4": False, "touchesV5Baseline": False, "touchesV51Baseline": False,
                    "touchesMaster": False, "thirdPartyAssets": False, "runtimeGenerativeAI": False},
    }
    with open(os.path.join(proj, VERDICT_REL), "w", encoding="utf-8") as fh:
        json.dump(verdict, fh, indent=1)
    unreal.log("LM V5.2 LIVING DIORAMA: built 8 SM + %d MICs, scene %s, lights=%d (+2 proof points), ribbonTorus=%s"
               % (len(_mics), LEVEL_PKG, lights, ribbon))


def boot():
    global EAS
    EAS = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    main()


boot()
