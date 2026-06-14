# Living Myth — North Star Diorama asset renderer (Prototype Pass V1).
#
# A serious step up from tools/art/render_assets.py (the flat low-poly spike): clustered,
# layered, organically-massed forms; soft 3-point lighting; Cycles + denoise; per-object
# colour jitter; bevelled edges; shadow-catcher grounding so each sprite drops a soft
# contact shadow into the transparent PNG (sits ON the diorama, not floating). Palette is
# locked to docs/VISUAL_STYLE.md. Deterministic (fixed seeds, no time/random-state leaks).
#
# Headless export (run from repo root):
#   & "C:\Program Files\Blender Foundation\Blender 5.1\blender.exe" -b -P tools/art/render_diorama.py -- godot/assets/diorama
#
# These are authored procedural geometry — NOT AI-generated art, NOT final. They prove the
# North Star diorama direction: stylized semi-realistic fantasy diorama miniatures.

import bpy, sys, os, math, random
import mathutils

PAL = {
    "trunk": "5a4228", "trunk_dark": "47341f",
    "leaf_warm": "6f8a3f", "leaf_mid": "5b7a36", "leaf_dark": "44602c", "leaf_cool": "5c7a4c",
    "fir_dark": "3c5230", "fir_mid": "4a6438",
    "grass": "6c7d3c", "grass_dry": "8a8146", "moss": "4e7d43",
    "rock": "8b8a82", "rock_dark": "6f6e66", "rock_warm": "9a9384",
    "timber": "73583a", "timber_dark": "5b4429", "thatch": "b08a4a", "thatch_dark": "8f6f38",
    "slate": "6a6f74", "slate_dark": "53585d", "stone": "9a9388", "stone_old": "857f72",
    "plaster": "c8b48a", "door": "3f2f1c", "field_gold": "9c853f", "field_green": "6f7e3a",
    "water": "5b8f96", "water_deep": "4a7d84", "cloth": "d8d2c2", "ink": "2a2012",
    "ember": "c7702e", "soil": "6e5638",
}

def _srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4

def rgba(hex_or_key, a=1.0, jitter=0.0, rng=None):
    h = PAL.get(hex_or_key, hex_or_key).lstrip("#")
    r, g, b = (int(h[i:i+2], 16) / 255.0 for i in (0, 2, 4))
    if jitter and rng is not None:
        f = 1.0 + rng.uniform(-jitter, jitter)
        r, g, b = (max(0.0, min(1.0, v * f)) for v in (r, g, b))
    return (_srgb_to_linear(r), _srgb_to_linear(g), _srgb_to_linear(b), a)

def mat(color, rough=0.82, mottle=0.16):
    m = bpy.data.materials.new("m")
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Roughness"].default_value = rough
    for spec in ("Specular IOR Level", "Specular"):
        if spec in bsdf.inputs:
            bsdf.inputs[spec].default_value = 0.06
            break
    # procedural mottle: break the flat single-colour look with a noise blend toward a darker
    # tone (weathering / material variation). Fail-safe — flat colour if the node API differs.
    if mottle > 0:
        try:
            dark = (color[0]*0.72, color[1]*0.72, color[2]*0.74, 1.0)
            noise = nt.nodes.new("ShaderNodeTexNoise")
            noise.inputs["Scale"].default_value = 11.0
            noise.inputs["Detail"].default_value = 5.0
            mix = nt.nodes.new("ShaderNodeMix")
            mix.data_type = "RGBA"
            mix.inputs[0].default_value = mottle          # Factor
            mix.inputs[6].default_value = color           # A
            mix.inputs[7].default_value = dark            # B
            nt.links.new(noise.outputs["Fac"], mix.inputs[0])
            nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
        except Exception as ex:
            print(f"[diorama] mottle skipped: {ex}")
    m.diffuse_color = color
    return m

_OFF = (0.0, 0.0, 0.0)

def add(op, color=None, loc=(0, 0, 0), scale=(1, 1, 1), rot=(0, 0, 0),
        smooth=False, bevel=0.0, **kw):
    getattr(bpy.ops.mesh, op)(**kw)
    o = bpy.context.active_object
    o.location = (loc[0] + _OFF[0], loc[1] + _OFF[1], loc[2] + _OFF[2])
    o.scale = scale
    o.rotation_euler = rot
    if color is not None:
        o.data.materials.append(mat(color) if isinstance(color, tuple) else color)
    if smooth:
        bpy.ops.object.shade_smooth()
    if bevel > 0:
        b = o.modifiers.new("bvl", "BEVEL")
        b.width = bevel
        b.segments = 2
    return o

def clear_meshes():
    for o in list(bpy.data.objects):
        if o.type == "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.materials):
        if m.users == 0:
            bpy.data.materials.remove(m)

# ---- foliage helpers ---------------------------------------------------------------------------
def canopy(x, y, base_z, r, h, tones, rng):
    """A rounded broadleaf crown: a few overlapping smooth spheres in mixed greens."""
    for i in range(rng.randint(3, 4)):
        dx, dy = rng.uniform(-r*0.4, r*0.4), rng.uniform(-r*0.4, r*0.4)
        rr = r * rng.uniform(0.6, 1.0)
        tone = tones[rng.randrange(len(tones))]
        add("primitive_ico_sphere_add", rgba(tone, jitter=0.08, rng=rng),
            loc=(x+dx, y+dy, base_z + h*0.5 + rng.uniform(0, h*0.25)),
            scale=(rr, rr, rr*0.92), subdivisions=2, radius=1, smooth=True)

def broadleaf(x, y, scale, rng):
    th = 0.34 * scale
    add("primitive_cylinder_add", rgba("trunk", jitter=0.1, rng=rng),
        loc=(x, y, th*0.5), scale=(0.05*scale, 0.05*scale, 1), vertices=8, radius=1, depth=th)
    canopy(x, y, th, 0.42*scale, 0.62*scale,
           ("leaf_warm", "leaf_mid", "leaf_dark", "leaf_cool"), rng)

def conifer(x, y, scale, rng):
    th = 0.22 * scale
    add("primitive_cylinder_add", rgba("trunk_dark", jitter=0.1, rng=rng),
        loc=(x, y, th*0.5), scale=(0.04*scale, 0.04*scale, 1), vertices=6, radius=1, depth=th)
    layers = 5
    for i in range(layers):
        t = i / (layers - 1)
        cz = th + 0.12*scale + t * 0.62*scale
        cr = (0.34 - 0.27*t) * scale
        tone = "fir_dark" if i % 2 else "fir_mid"
        add("primitive_cone_add", rgba(tone, jitter=0.07, rng=rng),
            loc=(x, y, cz), scale=(cr, cr, 1), vertices=9, radius1=1, radius2=0,
            depth=0.26*scale, smooth=True)

# ---- builders ----------------------------------------------------------------------------------
def _cluster(seed, kind):
    rng = random.Random(seed)
    n = rng.randint(3, 5)
    for _ in range(n):
        x, y = rng.uniform(-0.5, 0.5), rng.uniform(-0.5, 0.5)
        s = rng.uniform(0.6, 1.0)
        (broadleaf if kind == "b" else conifer)(x, y, s, rng)

def build_tree_broadleaf_cluster():
    _cluster(101, "b")

def build_tree_conifer_cluster():
    _cluster(202, "c")

def build_hill():
    rng = random.Random(303)
    add("primitive_ico_sphere_add", rgba("grass", jitter=0.05, rng=rng),
        loc=(0, 0, -0.55), scale=(1.05, 0.92, 0.75), subdivisions=3, radius=1, smooth=True)
    add("primitive_ico_sphere_add", rgba("grass_dry", jitter=0.06, rng=rng),
        loc=(0.35, 0.2, -0.5), scale=(0.55, 0.5, 0.6), subdivisions=3, radius=1, smooth=True)
    for _ in range(4):
        x, y = rng.uniform(-0.6, 0.6), rng.uniform(-0.5, 0.5)
        s = rng.uniform(0.1, 0.2)
        add("primitive_ico_sphere_add", rgba("rock", jitter=0.08, rng=rng),
            loc=(x, y, 0.3 + s*0.2), scale=(s, s*0.8, s*0.7), subdivisions=1, radius=1,
            rot=(0, 0, rng.uniform(0, 3.14)), smooth=True)

def build_rocks():
    rng = random.Random(404)
    for _ in range(6):
        x, y = rng.uniform(-0.5, 0.5), rng.uniform(-0.45, 0.45)
        s = rng.uniform(0.22, 0.46)
        tone = "rock" if rng.random() < 0.6 else "rock_warm"
        add("primitive_ico_sphere_add", rgba(tone, jitter=0.1, rng=rng),
            loc=(x, y, s*0.35), scale=(s, s*rng.uniform(0.7, 1.0), s*rng.uniform(0.55, 0.8)),
            subdivisions=1, radius=1, rot=(0, 0, rng.uniform(0, 3.14)), smooth=False, bevel=0.04)

def _house(x, y, w, d, wall, roof, rng, ang=0.0):
    bh = w * 0.95
    add("primitive_cube_add", rgba(wall, jitter=0.06, rng=rng),
        loc=(x, y, bh*0.5), scale=(w, d, bh*0.5), rot=(0, 0, ang), bevel=0.015)
    # pitched roof: a cube rotated 45° on its long axis, scaled to a ridge
    rz = bh + d*0.62
    add("primitive_cube_add", rgba(roof, jitter=0.05, rng=rng),
        loc=(x, y, rz), scale=(w*1.12, d*0.92, d*0.92), rot=(0.785, 0, ang), bevel=0.01)
    # door
    add("primitive_cube_add", rgba("door"),
        loc=(x + math.cos(ang)*(w+0.01), y + math.sin(ang)*(w+0.01), bh*0.32),
        scale=(0.02, d*0.22, bh*0.32), rot=(0, 0, ang))

def build_house_a():
    rng = random.Random(505)
    _house(0, 0, 0.42, 0.34, "timber", "thatch", rng)
    # chimney
    add("primitive_cube_add", rgba("stone_old"), loc=(0.22, -0.18, 0.62),
        scale=(0.07, 0.07, 0.34), bevel=0.012)

def build_house_b():
    rng = random.Random(606)
    _house(0, 0, 0.62, 0.46, "plaster", "slate", rng)
    add("primitive_cube_add", rgba("stone_old"), loc=(0.3, -0.26, 0.82),
        scale=(0.08, 0.08, 0.4), bevel=0.012)
    # timber posts at corners
    for sx in (-1, 1):
        add("primitive_cube_add", rgba("timber_dark"), loc=(sx*0.58, 0, 0.55),
            scale=(0.04, 0.04, 0.55))

def build_keep():
    rng = random.Random(707)
    # stone base + tower + battlements
    add("primitive_cube_add", rgba("stone", jitter=0.05, rng=rng), loc=(0, 0, 0.18),
        scale=(0.62, 0.62, 0.18), bevel=0.02)
    add("primitive_cube_add", rgba("stone", jitter=0.05, rng=rng), loc=(0, 0, 0.7),
        scale=(0.42, 0.42, 0.55), bevel=0.02)
    for i in range(4):
        for j in range(4):
            if (i in (0, 3)) or (j in (0, 3)):
                bx = -0.36 + i * 0.24
                by = -0.36 + j * 0.24
                add("primitive_cube_add", rgba("stone_old"), loc=(bx, by, 1.32),
                    scale=(0.08, 0.08, 0.1))
    add("primitive_cube_add", rgba("door"), loc=(0, 0.63, 0.42),
        scale=(0.13, 0.02, 0.22))

def build_watchtower():
    rng = random.Random(808)
    for sx in (-1, 1):
        for sy in (-1, 1):
            add("primitive_cylinder_add", rgba("timber_dark", jitter=0.08, rng=rng),
                loc=(sx*0.22, sy*0.22, 0.5), scale=(0.05, 0.05, 1), vertices=6, radius=1, depth=1.0,
                rot=(0, 0, 0))
    add("primitive_cube_add", rgba("timber"), loc=(0, 0, 1.02), scale=(0.34, 0.34, 0.06), bevel=0.01)
    add("primitive_cone_add", rgba("thatch_dark"), loc=(0, 0, 1.28), scale=(0.42, 0.42, 1),
        vertices=4, radius1=1, radius2=0, depth=0.4, rot=(0, 0, 0.785), smooth=False)

def build_standing_stones():
    rng = random.Random(909)
    n = 5
    for i in range(n):
        a = i / n * math.tau
        x, y = math.cos(a)*0.5, math.sin(a)*0.42
        h = rng.uniform(0.5, 0.82)
        add("primitive_cube_add", rgba("stone_old", jitter=0.06, rng=rng),
            loc=(x, y, h*0.5), scale=(0.1, 0.14, h*0.5),
            rot=(rng.uniform(-0.12, 0.12), rng.uniform(-0.12, 0.12), a), bevel=0.02)
    # a fallen lintel across the centre
    add("primitive_cube_add", rgba("stone"), loc=(0, 0, 0.12), scale=(0.4, 0.13, 0.1),
        rot=(0, 0, 0.5), bevel=0.02)

def build_shrine():
    rng = random.Random(110)
    add("primitive_cylinder_add", rgba("stone_old"), loc=(0, 0, 0.06),
        scale=(0.5, 0.5, 1), vertices=20, radius=1, depth=0.12)
    # a small arch: two posts + lintel
    for sx in (-1, 1):
        add("primitive_cube_add", rgba("stone", jitter=0.05, rng=rng), loc=(sx*0.22, 0, 0.34),
            scale=(0.07, 0.08, 0.34), bevel=0.015)
    add("primitive_cube_add", rgba("stone"), loc=(0, 0, 0.66), scale=(0.34, 0.09, 0.07), bevel=0.015)
    add("primitive_ico_sphere_add", rgba("ember"), loc=(0, 0, 0.16), scale=(0.07, 0.07, 0.07),
        subdivisions=2, radius=1, smooth=True)

def build_dock():
    rng = random.Random(120)
    add("primitive_cube_add", rgba("water"), loc=(0, -0.3, -0.01), scale=(1.0, 0.55, 0.01))
    add("primitive_cube_add", rgba("water_deep"), loc=(0, -0.62, -0.012),
        scale=(1.0, 0.25, 0.01))
    # planks running out over the water
    for i in range(5):
        y = 0.35 - i * 0.22
        add("primitive_cube_add", rgba("timber", jitter=0.07, rng=rng), loc=(0, y, 0.06),
            scale=(0.26, 0.09, 0.03))
        add("primitive_cylinder_add", rgba("timber_dark"), loc=(0.24, y, 0.0),
            scale=(0.03, 0.03, 1), vertices=6, radius=1, depth=0.22)
    # a small shore hut
    _house(-0.04, 0.5, 0.22, 0.2, "timber", "thatch", rng)

def build_field():
    rng = random.Random(130)
    add("primitive_cube_add", rgba("soil"), loc=(0, 0, 0.02), scale=(0.92, 0.78, 0.02), bevel=0.01)
    for i in range(7):
        x = -0.78 + i * 0.26
        tone = "field_gold" if i % 2 else "field_green"
        add("primitive_cube_add", rgba(tone, jitter=0.08, rng=rng), loc=(x, 0, 0.05),
            scale=(0.09, 0.74, 0.03))
    # a fence corner
    for i in range(4):
        add("primitive_cylinder_add", rgba("timber_dark"), loc=(-0.9, -0.7 + i*0.45, 0.1),
            scale=(0.02, 0.02, 1), vertices=5, radius=1, depth=0.22)

def build_banner():
    rng = random.Random(140)
    add("primitive_cylinder_add", rgba("timber_dark"), loc=(0, 0, 0.55),
        scale=(0.035, 0.035, 1), vertices=8, radius=1, depth=1.1)
    add("primitive_ico_sphere_add", rgba("thatch"), loc=(0, 0, 1.12),
        scale=(0.07, 0.07, 0.07), subdivisions=2, radius=1, smooth=True)
    # cloth rendered NEUTRAL — Godot modulates it to the faction colour
    cloth = add("primitive_cube_add", rgba("cloth"), loc=(0.22, 0, 0.78),
                scale=(0.22, 0.012, 0.3), rot=(0, 0, 0), bevel=0.0)

BUILDERS = {
    "tree_broadleaf_cluster": build_tree_broadleaf_cluster,
    "tree_conifer_cluster": build_tree_conifer_cluster,
    "hill": build_hill,
    "rocks": build_rocks,
    "house_a": build_house_a,
    "house_b": build_house_b,
    "keep": build_keep,
    "watchtower": build_watchtower,
    "standing_stones": build_standing_stones,
    "shrine": build_shrine,
    "dock": build_dock,
    "field": build_field,
    "banner": build_banner,
}

# tree-cluster variants — varied arrangements so the scatter never reads as a repeated stamp
for _v in (1, 2, 3):
    BUILDERS[f"tree_broadleaf_{_v}"] = (lambda s: lambda: _cluster(s, "b"))(110 + _v * 7)
    BUILDERS[f"tree_conifer_{_v}"] = (lambda s: lambda: _cluster(s, "c"))(220 + _v * 7)

# ---- scene / camera / lights -------------------------------------------------------------------
CAM_AZ = math.radians(48)
CAM_EL = math.radians(53)

def make_camera(scene):
    cd = bpy.data.cameras.new("Cam")
    cd.type = "ORTHO"
    cam = bpy.data.objects.new("Cam", cd)
    scene.collection.objects.link(cam)
    scene.camera = cam
    return cam

def frame_camera(cam, margin=1.18):
    """Aim the ortho camera at the built mesh bounds and size it to fill the frame."""
    verts = []
    for o in bpy.data.objects:
        if o.type == "MESH" and not getattr(o, "is_shadow_catcher", False):
            for v in o.bound_box:
                verts.append(o.matrix_world @ mathutils.Vector(v))
    if not verts:
        verts = [mathutils.Vector((0, 0, 0))]
    cx = sum(v.x for v in verts) / len(verts)
    cy = sum(v.y for v in verts) / len(verts)
    cz = sum(v.z for v in verts) / len(verts)
    centre = mathutils.Vector((cx, cy, cz))
    dist = 12.0
    dir = mathutils.Vector((math.cos(CAM_AZ)*math.cos(CAM_EL),
                            math.sin(CAM_AZ)*math.cos(CAM_EL),
                            math.sin(CAM_EL)))
    cam.location = centre + dir * dist
    look = centre - cam.location
    cam.rotation_euler = look.to_track_quat("-Z", "Y").to_euler()
    # project verts onto camera right/up to size ortho
    n = look.normalized()
    up0 = mathutils.Vector((0, 0, 1))
    right = n.cross(up0).normalized()
    up = right.cross(n).normalized()
    ext = 0.0
    for v in verts:
        d = v - centre
        ext = max(ext, abs(d.dot(right)), abs(d.dot(up)))
    cam.data.ortho_scale = max(0.6, ext * 2 * margin)

def setup_lights(scene):
    key = bpy.data.lights.new("Key", "SUN")
    key.energy = 3.6
    key.angle = math.radians(6)
    ko = bpy.data.objects.new("Key", key)
    scene.collection.objects.link(ko)
    ko.rotation_euler = (math.radians(48), math.radians(6), math.radians(-58))

    fill = bpy.data.lights.new("Fill", "SUN")
    fill.energy = 1.1
    fo = bpy.data.objects.new("Fill", fill)
    scene.collection.objects.link(fo)
    fo.rotation_euler = (math.radians(58), 0, math.radians(120))

    rim = bpy.data.lights.new("Rim", "SUN")
    rim.energy = 2.0
    ro = bpy.data.objects.new("Rim", rim)
    scene.collection.objects.link(ro)
    ro.rotation_euler = (math.radians(28), 0, math.radians(150))

    world = bpy.data.worlds.new("W")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0.55, 0.6, 0.66, 1)
    bg.inputs["Strength"].default_value = 0.45

def setup_engine(scene, res=512):
    try:
        scene.render.engine = "CYCLES"
        scene.cycles.samples = 96
        scene.cycles.use_denoising = True
        scene.cycles.device = "CPU"
    except Exception:
        scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.film_transparent = True
    scene.render.resolution_x = res
    scene.render.resolution_y = res
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"

def add_shadow_catcher():
    bpy.ops.mesh.primitive_plane_add(size=14, location=(0, 0, 0))
    p = bpy.context.active_object
    p.is_shadow_catcher = True
    return p

def main():
    argv = sys.argv
    outdir = argv[argv.index("--") + 1] if "--" in argv else "godot/assets/diorama"
    outdir = os.path.abspath(outdir)
    os.makedirs(outdir, exist_ok=True)

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()

    scene = bpy.context.scene
    setup_engine(scene)
    cam = make_camera(scene)
    setup_lights(scene)

    done = []
    for name, builder in BUILDERS.items():
        clear_meshes()
        add_shadow_catcher()
        builder()
        frame_camera(cam)
        scene.render.filepath = os.path.join(outdir, name + ".png")
        bpy.ops.render.render(write_still=True)
        done.append(name)
        print(f"[diorama] rendered {name}.png")

    print(f"[diorama] {len(done)} assets -> {outdir}")

main()
