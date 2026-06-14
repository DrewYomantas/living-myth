# Living Myth — Visual Pipeline Spike V1: Blender asset renderer.
#
# Renders the seven stylized low-poly diorama placeholders the viewer spike overlays
# on the data-driven atlas. Deterministic by construction (fixed seeds, no time/random
# state leaks), palette locked to docs/VISUAL_STYLE.md, transparent PNG out.
#
# Headless export (the documented command — run from the repo root):
#   & "C:\Program Files\Blender Foundation\Blender 5.1\blender.exe" -b -P tools/art/render_assets.py -- godot/assets/spike
#
# These are PLACEHOLDER assets: authored procedural geometry, NOT AI-generated art, NOT final.
# They exist only to prove the pipeline (Blender -> transparent PNG -> Godot res:// -> overlay).

import bpy, sys, os, math, random
import mathutils

# --- palette (docs/VISUAL_STYLE.md — hexes are binding) -----------------------------------------
PAL = {
    "forest_base": "3f5230", "forest_dark": "36482a", "plains": "5d5e38", "highland": "6a665a",
    "coast": "5d6242", "ochre": "b8862e", "ochre_dark": "8a5d12", "stone": "8a8a86",
    "stone_old": "90908a", "timber": "6e5639", "thatch": "a3854f", "moss": "4e7d43",
    "field_gold": "8f7c43", "road_dirt": "9c7c4a", "ink": "3a2c19", "tree_green": "55703f",
}

def _srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4

def rgba(name, a=1.0):
    h = PAL[name].lstrip("#")
    r, g, b = (int(h[i:i+2], 16) / 255.0 for i in (0, 2, 4))
    return (_srgb_to_linear(r), _srgb_to_linear(g), _srgb_to_linear(b), a)

def mat(name, color_key, rough=0.85):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    c = rgba(color_key)
    bsdf.inputs["Base Color"].default_value = c
    bsdf.inputs["Roughness"].default_value = rough
    for spec in ("Specular IOR Level", "Specular"):
        if spec in bsdf.inputs:
            bsdf.inputs[spec].default_value = 0.12
            break
    m.diffuse_color = c          # workbench fallback colour
    return m

# --- scene scaffold -----------------------------------------------------------------------------
def add(op, mat_key=None, loc=(0, 0, 0), scale=(1, 1, 1), rot=(0, 0, 0), **kw):
    getattr(bpy.ops.mesh, op)(**kw)
    o = bpy.context.active_object
    o.location = loc
    o.scale = scale
    o.rotation_euler = rot
    if mat_key is not None:
        o.data.materials.append(MATS[mat_key])
    return o

def aim(cam, loc, target=(0, 0, 0.28)):
    cam.location = loc
    d = mathutils.Vector(target) - mathutils.Vector(loc)
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()

def clear_all():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.lights, bpy.data.cameras):
        for b in list(block):
            if b.users == 0:
                block.remove(b)

def clear_meshes():
    for o in list(bpy.data.objects):
        if o.type == "MESH":
            bpy.data.objects.remove(o, do_unlink=True)

def ground(color_key, r=1.12, d=0.12):
    return add("primitive_cylinder_add", color_key, loc=(0, 0, d / 2), scale=(r, r, 1),
               vertices=24, radius=1, depth=d)

# --- the seven asset builders -------------------------------------------------------------------
def build_forest_patch():
    ground("forest_base")
    random.seed(11)
    for _ in range(8):
        x, y = random.uniform(-0.75, 0.75), random.uniform(-0.75, 0.75)
        h = random.uniform(0.45, 0.7)
        add("primitive_cylinder_add", "timber", loc=(x, y, 0.12 + 0.06),
            scale=(0.045, 0.045, 1), vertices=6, radius=1, depth=0.12)
        tone = "forest_dark" if random.random() < 0.5 else "tree_green"
        add("primitive_cone_add", tone, loc=(x, y, 0.12 + 0.12 + h * 0.45),
            scale=(0.22, 0.22, 1), vertices=7, radius1=1, radius2=0, depth=h)
        add("primitive_cone_add", "forest_base", loc=(x, y, 0.12 + 0.12 + h * 0.85),
            scale=(0.14, 0.14, 1), vertices=7, radius1=1, radius2=0, depth=h * 0.6)

def build_grassland_patch():
    ground("plains")
    random.seed(22)
    for _ in range(26):
        x, y = random.uniform(-0.85, 0.85), random.uniform(-0.85, 0.85)
        h = random.uniform(0.12, 0.26)
        key = "field_gold" if random.random() < 0.25 else "moss"
        add("primitive_cone_add", key, loc=(x, y, 0.12 + h * 0.45),
            scale=(0.03, 0.03, 1), vertices=4, radius1=1, radius2=0, depth=h,
            rot=(random.uniform(-0.2, 0.2), random.uniform(-0.2, 0.2), 0))

def build_rocky_patch():
    ground("highland")
    random.seed(33)
    for _ in range(6):
        x, y = random.uniform(-0.7, 0.7), random.uniform(-0.7, 0.7)
        s = random.uniform(0.18, 0.34)
        add("primitive_ico_sphere_add", "stone",
            loc=(x, y, 0.12 + s * 0.35), subdivisions=1, radius=1,
            scale=(s, s * random.uniform(0.7, 1.0), s * random.uniform(0.5, 0.8)),
            rot=(0, 0, random.uniform(0, 3.14)))

def build_road_path_decal():
    # a low worn path, gently kinked (a few overlapping raised strips) — reads at the iso angle
    random.seed(44)
    seg = [(-0.9, -0.35, 0.18), (-0.2, 0.0, 0.1), (0.45, 0.2, 0.16), (0.95, 0.55, 0.12)]
    for i in range(len(seg) - 1):
        x0, y0, _ = seg[i]
        x1, y1, _ = seg[i + 1]
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        ang = math.atan2(y1 - y0, x1 - x0)
        length = math.dist((x0, y0), (x1, y1))
        add("primitive_cube_add", "road_dirt", loc=(cx, cy, 0.03),
            scale=(length / 2 * 1.05, 0.16, 0.03), rot=(0, 0, ang))
    for _ in range(10):  # ruts / stones along the way
        t = random.random()
        x = -0.9 + t * 1.85
        y = -0.35 + t * 0.9 + random.uniform(-0.05, 0.05)
        add("primitive_cube_add", "timber", loc=(x, y, 0.05),
            scale=(0.04, 0.04, 0.02), rot=(0, 0, random.uniform(0, 3.14)))

def build_shrine_ruin_marker():
    ground("coast", r=0.8, d=0.08)
    # a weathered dolmen — two leaning uprights + a lintel (ancient, not fantasy)
    add("primitive_cube_add", "stone_old", loc=(-0.32, 0, 0.08 + 0.36),
        scale=(0.12, 0.16, 0.42), rot=(0, 0.08, 0))
    add("primitive_cube_add", "stone_old", loc=(0.32, 0, 0.08 + 0.36),
        scale=(0.12, 0.16, 0.42), rot=(0, -0.06, 0))
    add("primitive_cube_add", "stone", loc=(0, 0, 0.08 + 0.78),
        scale=(0.5, 0.18, 0.1), rot=(0, 0, 0))
    add("primitive_cube_add", "stone", loc=(0.55, -0.4, 0.08 + 0.08),  # a fallen stone
        scale=(0.22, 0.12, 0.08), rot=(0, 1.2, 0.4))

def build_settlement_cluster_marker():
    ground("plains", r=1.0)
    random.seed(55)
    spots = [(-0.4, -0.25), (0.3, -0.4), (0.0, 0.35), (0.5, 0.25)]
    for (x, y) in spots:
        w = random.uniform(0.18, 0.24)
        add("primitive_cube_add", "timber", loc=(x, y, 0.12 + w * 0.6),
            scale=(w, w, w * 0.6), rot=(0, 0, random.uniform(-0.3, 0.3)))
        add("primitive_cone_add", "thatch", loc=(x, y, 0.12 + w * 1.2 + 0.08),
            scale=(w * 1.25, w * 1.25, 1), vertices=4, radius1=1, radius2=0, depth=0.26,
            rot=(0, 0, random.uniform(-0.3, 0.3)))

def build_parched_famine_overlay():
    # cracked dry-earth patch — echoes the in-engine famine scar glyph (ochre, fissures)
    ground("ochre", r=1.1, d=0.06)
    random.seed(66)
    # a base discolour ring slightly inset
    add("primitive_cylinder_add", "ochre_dark", loc=(0, 0, 0.062),
        scale=(0.7, 0.7, 1), vertices=20, radius=1, depth=0.01)
    for _ in range(7):  # fissures radiating from centre
        ang = random.uniform(0, 6.28)
        length = random.uniform(0.35, 0.85)
        cx, cy = math.cos(ang) * length / 2, math.sin(ang) * length / 2
        add("primitive_cube_add", "ink", loc=(cx, cy, 0.07),
            scale=(length / 2, 0.012, 0.012), rot=(0, 0, ang))

BUILDERS = {
    "forest_patch": build_forest_patch,
    "grassland_patch": build_grassland_patch,
    "rocky_patch": build_rocky_patch,
    "road_path_decal": build_road_path_decal,
    "shrine_ruin_marker": build_shrine_ruin_marker,
    "settlement_cluster_marker": build_settlement_cluster_marker,
    "parched_famine_overlay": build_parched_famine_overlay,
}

# --- main ---------------------------------------------------------------------------------------
def setup_scene():
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.film_transparent = True
    scene.render.resolution_x = 256
    scene.render.resolution_y = 256
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    if hasattr(scene, "eevee") and hasattr(scene.eevee, "taa_render_samples"):
        scene.eevee.taa_render_samples = 24

    cam_data = bpy.data.cameras.new("SpikeCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 3.0
    cam = bpy.data.objects.new("SpikeCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    aim(cam, (1.9, -1.9, 3.4))

    sun_data = bpy.data.lights.new("Sun", "SUN")
    sun_data.energy = 3.4
    sun_data.angle = 0.35
    sun = bpy.data.objects.new("Sun", sun_data)
    scene.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(52), math.radians(8), math.radians(-48))

    world = bpy.data.worlds.new("SpikeWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = rgba("highland")
    bg.inputs["Strength"].default_value = 0.4
    return scene

def main():
    argv = sys.argv
    outdir = argv[argv.index("--") + 1] if "--" in argv else "godot/assets/spike"
    outdir = os.path.abspath(outdir)
    os.makedirs(outdir, exist_ok=True)

    clear_all()
    global MATS
    MATS = {k: mat(f"m_{k}", k) for k in PAL}
    scene = setup_scene()

    done = []
    for name, builder in BUILDERS.items():
        clear_meshes()
        builder()
        scene.render.filepath = os.path.join(outdir, name + ".png")
        bpy.ops.render.render(write_still=True)
        done.append(name + ".png")
        print(f"[living-myth-spike] rendered {name}.png")

    print(f"[living-myth-spike] {len(done)} assets -> {outdir}")

main()
