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
    "plaster": "d8c39a", "plaster_warm": "e3cda0",
    "tile": "9a4f31", "tile_dark": "7a3d26", "tile_warm": "b0603a",
    "door": "3f2f1c", "field_gold": "9c853f", "field_green": "6f7e3a",
    "water": "5b8f96", "water_deep": "4a7d84", "cloth": "d8d2c2", "ink": "2a2012",
    "ember": "c7702e", "soil": "6e5638",
    # broad terrain-zone tones (painted ground swatches — meadow / worn dirt / packed plaza)
    "meadow": "6f8240", "meadow_dark": "566a32", "meadow_dry": "8a8a46",
    "path": "7a5c38", "path_pale": "977548", "earth": "8a6e44", "earth_dark": "6e5634",
    # cloaked-figure tones (Godot re-tints the cloak per soul; head/staff stay neutral)
    "cloak": "8a6a44", "cloak_dark": "5e482e", "skin": "caa074", "staff": "6a4f2a",
}

def _srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4

def rgba(hex_or_key, a=1.0, jitter=0.0, rng=None):
    # accept an already-built (r,g,b[,a]) literal — treat as sRGB, convert rgb to linear
    if isinstance(hex_or_key, (tuple, list)):
        r, g, b = hex_or_key[0], hex_or_key[1], hex_or_key[2]
        aa = hex_or_key[3] if len(hex_or_key) > 3 else a
        return (_srgb_to_linear(r), _srgb_to_linear(g), _srgb_to_linear(b), aa)
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
    # procedural mottle: break the flat single-colour look with TWO layered noises — a coarse
    # weathering blend toward a darker shadow tone, then a finer grain blend toward a lighter
    # warm tone. Two scales read as hand-painted texture rather than a single flat wash.
    # Fail-safe — flat colour if the node API differs.
    if mottle > 0:
        try:
            dark = (color[0]*0.70, color[1]*0.70, color[2]*0.73, 1.0)
            lite = (min(1.0, color[0]*1.22+0.03), min(1.0, color[1]*1.20+0.03), min(1.0, color[2]*1.14+0.02), 1.0)
            coarse = nt.nodes.new("ShaderNodeTexNoise")
            coarse.inputs["Scale"].default_value = 9.0
            coarse.inputs["Detail"].default_value = 6.0
            mix1 = nt.nodes.new("ShaderNodeMix")
            mix1.data_type = "RGBA"
            mix1.inputs[0].default_value = mottle          # Factor
            mix1.inputs[6].default_value = color           # A
            mix1.inputs[7].default_value = dark            # B
            nt.links.new(coarse.outputs["Fac"], mix1.inputs[0])

            fine = nt.nodes.new("ShaderNodeTexNoise")
            fine.inputs["Scale"].default_value = 34.0
            fine.inputs["Detail"].default_value = 8.0
            mix2 = nt.nodes.new("ShaderNodeMix")
            mix2.data_type = "RGBA"
            mix2.inputs[0].default_value = mottle * 0.65    # Factor
            mix2.inputs[7].default_value = lite             # B (highlight)
            nt.links.new(mix1.outputs[2], mix2.inputs[6])   # A = coarse result
            nt.links.new(fine.outputs["Fac"], mix2.inputs[0])
            nt.links.new(mix2.outputs[2], bsdf.inputs["Base Color"])
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
# Profiles give the broadleaf crown a real SILHOUETTE instead of a single round puffball:
#   round → irregular ball   wide → low broad oak umbrella   tall → upright birch/poplar column
_PROFILE = {            # (n masses, lateral spread×r, vertical lift, z-squash, crown tones)
    "round": (5, 0.55, 0.95, 0.92),
    "wide":  (6, 0.82, 0.70, 0.74),
    "tall":  (4, 0.34, 1.18, 1.20),
}

def canopy(x, y, base_z, r, h, tones, rng, profile="round"):
    """An ASYMMETRIC broadleaf crown built from a few big overlapping masses with an irregular,
    broken top — never a smooth dome. Shadowed underskirt for depth, a sun-kissed NW highlight,
    and a couple of top tufts that break the silhouette so it reads as a tree, not a bush."""
    nmass, spread, lift, squash = _PROFILE[profile]
    # shadowed underskirt (reads as foliage shadow, anchors the crown over the trunk)
    for _ in range(3):
        ang = rng.uniform(0, 6.28); rad = rng.uniform(0, r*spread*0.7)
        rr = r * rng.uniform(0.5, 0.8)
        add("primitive_ico_sphere_add", rgba("leaf_dark", jitter=0.08, rng=rng),
            loc=(x+math.cos(ang)*rad, y+math.sin(ang)*rad, base_z + h*0.34),
            scale=(rr, rr, rr*0.78*squash), subdivisions=2, radius=1, smooth=True)
    # main masses — fewer, larger, at irregular heights so the crown has lumps and gaps
    for k in range(nmass):
        ang = rng.uniform(0, 6.28); rad = rng.uniform(0, r*spread)
        rr = r * rng.uniform(0.55, 1.0)
        zz = base_z + h*0.42*lift + rng.uniform(0, h*0.55)
        tone = tones[rng.randrange(len(tones))]
        add("primitive_ico_sphere_add", rgba(tone, jitter=0.1, rng=rng),
            loc=(x+math.cos(ang)*rad, y+math.sin(ang)*rad, zz),
            scale=(rr, rr, rr*squash), subdivisions=2, radius=1, smooth=True)
    # small tufts that poke past the mass — break the round-dome read
    for _ in range(2):
        ang = rng.uniform(0, 6.28); rad = rng.uniform(r*0.2, r*spread*0.9)
        rr = r * rng.uniform(0.28, 0.42)
        add("primitive_ico_sphere_add", rgba(tones[rng.randrange(len(tones))], jitter=0.1, rng=rng),
            loc=(x+math.cos(ang)*rad, y+math.sin(ang)*rad, base_z + h*(0.7 + 0.4*lift) + rng.uniform(0, h*0.3)),
            scale=(rr, rr, rr*squash), subdivisions=2, radius=1, smooth=True)
    # sun-kissed crown highlight (NW, matching the key light)
    add("primitive_ico_sphere_add", rgba("leaf_warm", jitter=0.05, rng=rng),
        loc=(x - r*0.16, y + r*0.16, base_z + h*(0.74 + 0.3*lift)),
        scale=(r*0.46, r*0.46, r*0.44*squash), subdivisions=2, radius=1, smooth=True)

def broadleaf(x, y, scale, rng):
    profile = rng.choice(("round", "wide", "tall", "round", "wide"))
    # a TALLER, clearly visible two-segment trunk (wider at the base) so the form reads as a tree
    th = (0.66 if profile == "tall" else 0.48) * scale
    tr = 0.055 * scale
    add("primitive_cylinder_add", rgba("trunk_dark", jitter=0.1, rng=rng),
        loc=(x, y, th*0.28), scale=(tr*1.25, tr*1.25, 1), vertices=8, radius=1, depth=th*0.56)
    add("primitive_cylinder_add", rgba("trunk", jitter=0.1, rng=rng),
        loc=(x, y, th*0.7), scale=(tr*0.9, tr*0.9, 1), vertices=8, radius=1, depth=th*0.6)
    crown_r = (0.34 if profile == "tall" else (0.5 if profile == "wide" else 0.44)) * scale
    crown_h = (0.74 if profile == "tall" else 0.56) * scale
    canopy(x, y, th, crown_r, crown_h,
           ("leaf_warm", "leaf_mid", "leaf_dark", "leaf_cool"), rng, profile)

def conifer(x, y, scale, rng):
    # fir → a tall, narrow, slightly ragged spire; pine → a shorter, bushier, broader cone
    pine = rng.random() < 0.34
    th = 0.20 * scale
    add("primitive_cylinder_add", rgba("trunk_dark", jitter=0.1, rng=rng),
        loc=(x, y, th*0.5), scale=(0.038*scale, 0.038*scale, 1), vertices=6, radius=1, depth=th)
    layers = 5 if pine else 7
    top = 0.60 if pine else 0.98
    botr = 0.46 if pine else 0.38
    for i in range(layers):
        t = i / (layers - 1)
        cz = th + 0.06*scale + t * top * scale
        cr = (botr - (botr - 0.05) * t) * scale * (1 + rng.uniform(-0.10, 0.10))
        tone = "fir_dark" if i % 2 else "fir_mid"
        # each tier tilted a hair off-axis so the spire reads ragged/organic, not a stacked toy
        tilt = rng.uniform(-0.05, 0.05)
        add("primitive_cone_add", rgba(tone, jitter=0.07, rng=rng),
            loc=(x + tilt*0.3, y + tilt*0.2, cz), scale=(cr, cr, 1), vertices=8, radius1=1, radius2=0,
            depth=(0.26 if pine else 0.32)*scale, rot=(tilt, tilt*0.6, rng.uniform(0, 1.0)), smooth=True)
    # a sharp apex tip so the silhouette comes to a clean point
    add("primitive_cone_add", rgba("fir_dark", jitter=0.06, rng=rng),
        loc=(x, y, th + 0.06*scale + top*scale), scale=(0.08*scale, 0.08*scale, 1),
        vertices=6, radius1=1, radius2=0, depth=0.2*scale, smooth=True)

# ---- builders ----------------------------------------------------------------------------------
def _cluster(seed, kind):
    # a denser, deliberately OVERLAPPING copse: more stems pulled tighter together with a tall
    # dominant at the back so the cluster reads as one layered canopy MASS (a forest-edge clump),
    # not a few separated toy puffs. A back-to-front size gradient gives the clump real depth.
    rng = random.Random(seed)
    n = rng.randint(5, 7)
    fn = broadleaf if kind == "b" else conifer
    # the dominant anchor tree at the back, taller — the silhouette the clump is read by
    fn(rng.uniform(-0.18, 0.18), rng.uniform(0.12, 0.34), rng.uniform(1.05, 1.30), rng)
    for _ in range(n):
        x, y = rng.uniform(-0.62, 0.62), rng.uniform(-0.55, 0.30)
        s = rng.uniform(0.62, 1.02)
        fn(x, y, s, rng)

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
    # ANGULAR faceted boulders (hard-shaded cubes/cones, not smooth pebbles) on a low slab — reads
    # as broken stone, not a pile of eggs.
    rng = random.Random(404)
    add("primitive_cube_add", rgba("rock_dark", jitter=0.08, rng=rng), loc=(0, 0, 0.04),
        scale=(0.5, 0.42, 0.05), rot=(0, 0, rng.uniform(0, 0.6)), bevel=0.01)
    for _ in range(5):
        x, y = rng.uniform(-0.42, 0.42), rng.uniform(-0.38, 0.38)
        s = rng.uniform(0.2, 0.42)
        tone = "rock" if rng.random() < 0.55 else "rock_warm"
        if rng.random() < 0.5:   # a tilted faceted block
            add("primitive_cube_add", rgba(tone, jitter=0.1, rng=rng),
                loc=(x, y, s*0.5), scale=(s, s*rng.uniform(0.7, 1.0), s*rng.uniform(0.7, 1.1)),
                rot=(rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3), rng.uniform(0, 3.14)),
                smooth=False, bevel=0.015)
        else:                    # a crystalline shard (few-sided cone)
            add("primitive_cone_add", rgba(tone, jitter=0.1, rng=rng),
                loc=(x, y, s*0.4), scale=(s*0.8, s*0.8, 1), vertices=rng.choice((5, 6)),
                radius1=1, radius2=rng.uniform(0.2, 0.45), depth=s*1.5,
                rot=(rng.uniform(-0.25, 0.25), rng.uniform(-0.25, 0.25), rng.uniform(0, 3.14)),
                smooth=False)

def build_crag():
    # a STRATIFIED rock outcrop: stacked angular slabs stepping up to a tilted crag face on one
    # side — a highland landmark (ridge/pass shoulder), reads as layered bedrock.
    rng = random.Random(415)
    layers = 5
    for i in range(layers):
        t = i / (layers - 1)
        w = (0.66 - 0.34*t)
        tone = ("rock", "rock_dark", "rock_warm")[i % 3]
        add("primitive_cube_add", rgba(tone, jitter=0.08, rng=rng),
            loc=(0.12*t, -0.05*t, 0.07 + t*0.42), scale=(w, w*0.74, 0.1),
            rot=(0, rng.uniform(-0.04, 0.04), rng.uniform(-0.05, 0.05)), smooth=False, bevel=0.012)
    # the crag face — a tall tilted slab rising off the back edge
    add("primitive_cube_add", rgba("rock_dark", jitter=0.08, rng=rng), loc=(-0.18, 0.16, 0.66),
        scale=(0.2, 0.3, 0.5), rot=(0.12, -0.22, 0.2), smooth=False, bevel=0.012)
    # a couple of fallen blocks at the foot
    for sx in (-1, 1):
        add("primitive_cube_add", rgba("rock_warm", jitter=0.1, rng=rng),
            loc=(sx*0.42, -0.3, 0.1), scale=(0.16, 0.13, 0.12),
            rot=(rng.uniform(-0.3, 0.3), 0, rng.uniform(0, 3.14)), smooth=False, bevel=0.01)

def _beam(x, y, z, sx, sy, sz, ang, tone="timber_dark"):
    add("primitive_cube_add", rgba(tone), loc=(x, y, z), scale=(sx, sy, sz), rot=(0, 0, ang),
        bevel=0.004)

def _house(x, y, w, d, wall, roof, rng, ang=0.0, roof_pitch=0.86, eave=1.42, tall=1.0):
    """A richer dwelling, not a toy box: a STONE BASE COURSE grounds it, the walls carry exposed
    TIMBER FRAMING (corner posts, top plate, sill, a brace), a HEAVY OVERHANGING pitched roof with
    a ridge beam crowns it, plus a stone CHIMNEY and a recessed door + window. Proportions are
    jittered per call so a terrace never reads as repeated stamps."""
    ca, sa = math.cos(ang), math.sin(ang)
    base_h = w * 0.26
    wall_h = w * 1.34 * tall          # tall enough that base + framing read beneath the roof
    wz0 = base_h
    wtop = wz0 + wall_h
    # 1) STONE BASE COURSE — a wider, short plinth of weathered stone
    add("primitive_cube_add", rgba("stone_old", jitter=0.06, rng=rng),
        loc=(x, y, base_h*0.5), scale=(w*1.07, d*1.07, base_h*0.5), rot=(0, 0, ang), bevel=0.02)
    # 2) MAIN WALL — warm plaster/timber infill
    add("primitive_cube_add", rgba(wall, jitter=0.06, rng=rng),
        loc=(x, y, wz0 + wall_h*0.5), scale=(w, d, wall_h*0.5), rot=(0, 0, ang), bevel=0.01)
    # 3) TIMBER FRAMING on the two camera-facing walls — reads as half-timber.
    #    nx=1 → +X long wall (tangent = local Y, run = d); ny=1 → +Y end wall (tangent = X, run = w).
    def to_world(lx, ly):
        return (x + lx*ca - ly*sa, y + lx*sa + ly*ca)
    def face(nx, ny, run):
        # corner posts at the two wall ends
        for s in (-1, 1):
            lx = w if nx else s*run
            ly = d if ny else s*run
            px, py = to_world(lx, ly)
            _beam(px, py, wz0 + wall_h*0.5, 0.045, 0.045, wall_h*0.5, ang)
        # top plate + sill run ALONG the wall (plate long-axis = the tangent)
        cx, cy = to_world(w if nx else 0, d if ny else 0)
        plate_ang = ang + (1.5708 if nx else 0)   # long wall runs along Y
        for zz in (wz0 + wall_h*0.94, wz0 + wall_h*0.10):
            _beam(cx, cy, zz, run, 0.05, 0.05, plate_ang)
        # a diagonal brace for the crafted read
        _beam(cx, cy, wz0 + wall_h*0.5, run*0.85, 0.045, 0.045, plate_ang + 0.5)
    face(1, 0, d)   # +X long wall
    face(0, 1, w)   # +Y end wall
    # 4) PITCHED ROOF — a 45°-rotated cube reads as a closed ridge prism (gables capped). Sized so
    #    its bottom vertex sits just BELOW the wall top: eaves overhang the walls a little, the roof
    #    CAPS the dwelling instead of engulfing it. `eave` widens the diamond (deeper eaves), `pitch`
    #    raises the ridge. The gable end stays solid (cube faces), so no open peak.
    yz = d * eave                      # diamond half-size in the depth/height plane
    rz = wtop + yz*0.50                # centre so the lower vertex tucks just under the eaves line
    add("primitive_cube_add", rgba(roof, jitter=0.05, rng=rng),
        loc=(x, y, rz), scale=(w*1.16, yz, yz*roof_pitch), rot=(0.785, 0, ang), bevel=0.008)
    # ridge beam along the peak
    ridge_z = rz + yz*1.04*roof_pitch
    _beam(x, y, ridge_z, w*1.18, 0.05, 0.05, ang, "timber")
    # 5) CHIMNEY — stone, rear corner, with a darker cap
    chx = x + (-w*0.55*ca - d*0.4*sa)
    chy = y + (-w*0.55*sa + d*0.4*ca)
    add("primitive_cube_add", rgba("stone_old", jitter=0.05, rng=rng),
        loc=(chx, chy, wtop + yz*0.6), scale=(0.075, 0.075, yz*0.7 + 0.12), rot=(0, 0, ang), bevel=0.01)
    add("primitive_cube_add", rgba("ink"),
        loc=(chx, chy, wtop + yz*1.35 + 0.12), scale=(0.092, 0.092, 0.035), rot=(0, 0, ang))
    # 6) DOOR + window on the +X face, recessed dark
    dx = x + (w + 0.012)*ca
    dy = y + (w + 0.012)*sa
    add("primitive_cube_add", rgba("door"),
        loc=(dx, dy, wz0 + wall_h*0.30), scale=(0.02, d*0.20, wall_h*0.30), rot=(0, 0, ang))
    wx = x + (w + 0.012)*ca - d*0.42*sa
    wy = y + (w + 0.012)*sa + d*0.42*ca
    add("primitive_cube_add", rgba("ink"),
        loc=(wx, wy, wz0 + wall_h*0.58), scale=(0.02, d*0.16, wall_h*0.18), rot=(0, 0, ang))

def build_house_a():
    # a thatched cottage — smaller, STEEP warm-thatch roof, plaster + dark timber
    rng = random.Random(505)
    _house(0, 0, 0.40, 0.32, "plaster", "thatch", rng, roof_pitch=1.05, eave=0.82, tall=0.92)

def build_house_b():
    # a larger timber hall — lower-pitch WARM TILE roof (not cold slate), a lean-to porch off the side
    rng = random.Random(606)
    _house(0, 0, 0.58, 0.42, "plaster_warm", "tile", rng, roof_pitch=0.72, eave=0.74, tall=1.06)
    # lean-to / porch roof off the +Y end — a single sloped panel on two posts
    add("primitive_cube_add", rgba("tile_dark", jitter=0.05, rng=rng), loc=(0, 0.62, 0.66),
        scale=(0.5, 0.2, 0.03), rot=(0.5, 0, 0), bevel=0.006)
    for sx in (-1, 1):
        add("primitive_cylinder_add", rgba("timber_dark"), loc=(sx*0.42, 0.74, 0.28),
            scale=(0.03, 0.03, 1), vertices=6, radius=1, depth=0.56)

def build_keep():
    rng = random.Random(707)
    # A SEAT, not a chess piece: a low curtain-wall ring with corner turrets + a gatehouse, and a
    # tall keep tower rising well above it. Two-tone weathered stone.
    # 1) rocky motte/base
    add("primitive_cube_add", rgba("stone", jitter=0.07, rng=rng), loc=(0, 0, 0.12),
        scale=(0.78, 0.78, 0.12), bevel=0.03)
    # 2) curtain walls (four low runs forming a ring)
    for (lx, ly, sx, sy) in [(0, 0.62, 0.62, 0.07), (0, -0.62, 0.62, 0.07),
                             (0.62, 0, 0.07, 0.62), (-0.62, 0, 0.07, 0.62)]:
        add("primitive_cube_add", rgba("stone_old", jitter=0.06, rng=rng),
            loc=(lx, ly, 0.4), scale=(sx, sy, 0.28), bevel=0.015)
    # 3) corner turrets
    for cx in (-0.62, 0.62):
        for cy in (-0.62, 0.62):
            add("primitive_cylinder_add", rgba("stone", jitter=0.07, rng=rng),
                loc=(cx, cy, 0.5), scale=(0.12, 0.12, 1), vertices=8, radius=1, depth=0.86)
            add("primitive_cone_add", rgba("slate", jitter=0.05, rng=rng), loc=(cx, cy, 1.02),
                scale=(0.15, 0.15, 1), vertices=8, radius1=1, radius2=0, depth=0.22)
    # 4) gatehouse on the south wall
    add("primitive_cube_add", rgba("stone_old", jitter=0.06, rng=rng), loc=(0, 0.66, 0.52),
        scale=(0.22, 0.1, 0.42), bevel=0.015)
    add("primitive_cube_add", rgba("door"), loc=(0, 0.78, 0.34), scale=(0.1, 0.02, 0.24))
    # 5) the keep tower — tall, dominant, battlemented
    add("primitive_cube_add", rgba("stone_old", jitter=0.07, rng=rng), loc=(0, 0, 0.92),
        scale=(0.4, 0.4, 0.66), bevel=0.025)
    for i in range(4):
        for j in range(4):
            if (i in (0, 3)) or (j in (0, 3)):
                add("primitive_cube_add", rgba("stone" if (i + j) % 2 else "stone_old", jitter=0.06, rng=rng),
                    loc=(-0.34 + i*0.227, -0.34 + j*0.227, 1.66), scale=(0.075, 0.075, 0.1))
    for (wx, wy, sx, sy) in [(0, 0.41, 0.05, 0.02), (0.41, 0, 0.02, 0.05), (-0.41, 0, 0.02, 0.05)]:
        add("primitive_cube_add", rgba("ink"), loc=(wx, wy, 1.1), scale=(sx, sy, 0.13))
    add("primitive_cone_add", rgba("slate", jitter=0.05, rng=rng), loc=(0, 0, 1.86),
        scale=(0.38, 0.38, 1), vertices=4, radius1=1, radius2=0, depth=0.42, rot=(0, 0, 0.785))

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
    # taller megaliths + a true TRILITHON (two uprights bearing a lintel) so the marker reads as
    # a deliberate sacred ring/barrow, not scattered pebbles.
    rng = random.Random(909)
    n = 6
    for i in range(n):
        a = i / n * math.tau
        x, y = math.cos(a)*0.52, math.sin(a)*0.44
        h = rng.uniform(0.62, 0.98)
        add("primitive_cube_add", rgba("stone_old", jitter=0.06, rng=rng),
            loc=(x, y, h*0.5), scale=(0.11, 0.15, h*0.5),
            rot=(rng.uniform(-0.1, 0.1), rng.uniform(-0.1, 0.1), a), bevel=0.02)
    # the trilithon at the centre-back: two uprights + a bearing lintel
    for sx in (-1, 1):
        add("primitive_cube_add", rgba("stone", jitter=0.05, rng=rng), loc=(sx*0.2, 0.0, 0.5),
            scale=(0.11, 0.15, 0.5), bevel=0.02)
    add("primitive_cube_add", rgba("stone", jitter=0.05, rng=rng), loc=(0, 0.0, 1.04),
        scale=(0.34, 0.14, 0.09), bevel=0.02)
    # a low altar slab at the foot
    add("primitive_cube_add", rgba("stone_old"), loc=(0, -0.12, 0.1), scale=(0.26, 0.16, 0.09),
        rot=(0, 0, 0.3), bevel=0.02)

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
    # a clearer coast silhouette: a plank jetty running out over water on posts, a MOORED ROWBOAT
    # alongside, mooring posts, and a shore hut with stacked crates.
    add("primitive_cube_add", rgba("water"), loc=(0, -0.34, -0.01), scale=(1.0, 0.6, 0.01))
    add("primitive_cube_add", rgba("water_deep"), loc=(0, -0.7, -0.012), scale=(1.0, 0.3, 0.01))
    # jetty deck (a continuous boardwalk) + support posts dropping into the water
    add("primitive_cube_add", rgba("timber", jitter=0.06, rng=rng), loc=(0.0, -0.15, 0.085),
        scale=(0.16, 0.62, 0.025), bevel=0.006)
    for i in range(5):
        y = 0.32 - i * 0.24
        for sx in (-1, 1):
            add("primitive_cylinder_add", rgba("timber_dark"), loc=(sx*0.15, y, -0.02),
                scale=(0.022, 0.022, 1), vertices=6, radius=1, depth=0.26)
    # mooring posts at the seaward end
    for sx in (-1, 1):
        add("primitive_cylinder_add", rgba("timber_dark"), loc=(sx*0.2, -0.66, 0.1),
            scale=(0.03, 0.03, 1), vertices=6, radius=1, depth=0.34)
    # a moored rowboat — hull (flattened, tapered) + a thwart
    add("primitive_cube_add", rgba("timber_dark", jitter=0.05, rng=rng), loc=(0.34, -0.42, 0.05),
        scale=(0.12, 0.26, 0.05), rot=(0, 0, 0.18), bevel=0.04)
    add("primitive_cube_add", rgba("timber"), loc=(0.34, -0.42, 0.1), scale=(0.1, 0.04, 0.015), rot=(0, 0, 0.18))
    # shore hut + a couple of crates
    _house(-0.28, 0.5, 0.22, 0.2, "timber", "thatch", rng)
    for (cx, cy) in [(0.16, 0.52), (0.26, 0.46)]:
        add("primitive_cube_add", rgba("timber", jitter=0.07, rng=rng), loc=(cx, cy, 0.09),
            scale=(0.07, 0.07, 0.07), rot=(0, 0, rng.uniform(0, 0.5)), bevel=0.008)

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

def _stall(seed, awnA, awnB):
    """A scene-DEFINING market stall (not tiny clutter): four timber posts, a heavy plank counter
    laden with goods, barrels beneath, and a big PEAKED STRIPED CANVAS AWNING with deep overhang
    and a scalloped front valance. Two awning colours per call so the market reads varied."""
    rng = random.Random(seed)
    w, d = 0.46, 0.40
    postH = 0.66
    for sx in (-1, 1):
        for sy in (-1, 1):
            add("primitive_cylinder_add", rgba("timber_dark", jitter=0.06, rng=rng),
                loc=(sx*w*0.82, sy*d*0.82, postH*0.5), scale=(0.028, 0.028, 1),
                vertices=6, radius=1, depth=postH)
    # heavy plank counter along the front (+Y), with a couple of trestle legs
    add("primitive_cube_add", rgba("timber", jitter=0.05, rng=rng), loc=(0, d*0.78, 0.36),
        scale=(w*0.96, 0.07, 0.045), bevel=0.008)
    # goods on the counter — mounded produce + a stacked crate
    for (gx, tone) in [(-0.26, "field_gold"), (-0.05, "ember"), (0.18, "leaf_mid")]:
        add("primitive_ico_sphere_add", rgba(tone, jitter=0.12, rng=rng),
            loc=(gx, d*0.78, 0.45), scale=(0.07, 0.07, 0.05), subdivisions=2, radius=1, smooth=True)
    add("primitive_cube_add", rgba("timber", jitter=0.08, rng=rng), loc=(0.28, d*0.62, 0.46),
        scale=(0.06, 0.06, 0.06), rot=(0, 0, 0.4), bevel=0.006)
    # barrels beneath the counter
    for bx in (-0.24, 0.0):
        add("primitive_cylinder_add", rgba("timber_dark", jitter=0.07, rng=rng),
            loc=(bx, d*0.5, 0.12), scale=(0.075, 0.075, 1), vertices=12, radius=1, depth=0.24)
    # hanging sack from a front post
    add("primitive_ico_sphere_add", rgba("cloth", jitter=0.05, rng=rng),
        loc=(-w*0.82, d*0.7, 0.5), scale=(0.06, 0.06, 0.08), subdivisions=2, radius=1, smooth=True)
    # PEAKED STRIPED AWNING — a ridge prism (awnA) with awnB stripes running down both slopes
    az = postH + 0.04
    aw, ad = w*1.34, d*1.5
    add("primitive_cube_add", rgba(awnA, jitter=0.04, rng=rng), loc=(0, 0, az + ad*0.32),
        scale=(aw, ad*0.62, ad*0.62), rot=(0.785, 0, 0), bevel=0.004)
    for i in range(-2, 3):
        add("primitive_cube_add", rgba(awnB, jitter=0.04, rng=rng), loc=(i*0.155, 0, az + ad*0.325),
            scale=(0.05, ad*0.625, ad*0.625), rot=(0.785, 0, 0))
    # scalloped valance hanging off the front eave
    for i in range(-3, 4):
        add("primitive_cube_add", rgba(awnA if i % 2 else awnB, jitter=0.04, rng=rng),
            loc=(i*0.12, d*0.92, az + 0.02), scale=(0.05, 0.015, 0.055), bevel=0.004)

def build_stall_a():
    _stall(160, "tile_warm", "cloth")     # warm red + cream

def build_stall_b():
    _stall(170, "water_deep", "cloth")    # teal + cream

def _figure(seed, staff=False):
    """A tiny mythic diorama FIGURE: a strong cloaked-body silhouette (tapered cloak cone + hood +
    a peeking face), optionally bearing a staff. Cloak baked warm-neutral — Godot re-tints per soul.
    Read at sprite size it is a person, not a dot."""
    rng = random.Random(seed)
    lean = rng.uniform(-0.06, 0.06)
    # cloak — a tapered cone, wide hem to narrow shoulders
    add("primitive_cone_add", rgba("cloak", jitter=0.05, rng=rng), loc=(0, 0, 0.30),
        scale=(0.19, 0.16, 1), vertices=14, radius1=1, radius2=0.42, depth=0.60,
        rot=(lean, 0, rng.uniform(0, 3.14)), smooth=True)
    # a darker fold down the front for depth
    add("primitive_cube_add", rgba("cloak_dark", jitter=0.06, rng=rng), loc=(0, 0.07, 0.30),
        scale=(0.03, 0.02, 0.26), rot=(lean, 0, 0))
    # shoulders cap
    add("primitive_ico_sphere_add", rgba("cloak", jitter=0.05, rng=rng), loc=(0, 0, 0.60),
        scale=(0.13, 0.12, 0.09), subdivisions=2, radius=1, smooth=True)
    # hood + peeking face
    add("primitive_ico_sphere_add", rgba("cloak_dark", jitter=0.04, rng=rng), loc=(0, -0.01, 0.74),
        scale=(0.085, 0.085, 0.10), subdivisions=2, radius=1, smooth=True)
    add("primitive_ico_sphere_add", rgba("skin", jitter=0.05, rng=rng), loc=(0, 0.05, 0.73),
        scale=(0.055, 0.05, 0.06), subdivisions=2, radius=1, smooth=True)
    if staff:
        add("primitive_cylinder_add", rgba("staff", jitter=0.05, rng=rng), loc=(0.16, 0.02, 0.42),
            scale=(0.016, 0.016, 1), vertices=6, radius=1, depth=0.84, rot=(0.04, 0.02, 0))

def build_figure():
    _figure(180)

def build_figure_staff():
    _figure(190, staff=True)

def build_banner():
    rng = random.Random(140)
    # taller pole + brass finial; a fuller, clearer flag with a triangular pennant above it so
    # the silhouette reads as a banner at small on-map size. Cloth stays NEUTRAL — Godot
    # modulates the whole sprite to the faction colour, so don't bake a hue in.
    add("primitive_cylinder_add", rgba("timber_dark"), loc=(0, 0, 0.62),
        scale=(0.032, 0.032, 1), vertices=8, radius=1, depth=1.24)
    add("primitive_ico_sphere_add", rgba("thatch"), loc=(0, 0, 1.26),
        scale=(0.075, 0.075, 0.09), subdivisions=2, radius=1, smooth=True)
    # main flag — a gently waving rectangle (slight skew so it isn't a stiff board)
    add("primitive_cube_add", rgba("cloth"), loc=(0.24, 0, 0.82),
        scale=(0.26, 0.011, 0.34), rot=(0, 0.06, 0), bevel=0.0)
    # a darker cloth fold for depth, and a triangular pennant at the top
    add("primitive_cube_add", rgba((0.78, 0.74, 0.66, 1.0)), loc=(0.40, 0.004, 0.70),
        scale=(0.10, 0.010, 0.20), rot=(0, 0.10, 0), bevel=0.0)
    add("primitive_cone_add", rgba("cloth"), loc=(0.16, 0, 1.16),
        scale=(0.16, 0.10, 1), vertices=3, radius1=1, radius2=0, depth=0.02, rot=(1.571, 0, 0))

def build_pulse_marker():
    # an event/pulse glyph that sits above a site where a recent tale is anchored: a low ring
    # of pale stones around a raised ember, with a thin four-point spark. Rendered NEUTRAL-warm;
    # Godot tints it to the event class (war-red / harvest-ochre / founding-gold).
    rng = random.Random(150)
    n = 10
    for i in range(n):
        a = i / n * math.tau
        x, y = math.cos(a)*0.42, math.sin(a)*0.42
        add("primitive_ico_sphere_add", rgba("stone", jitter=0.08, rng=rng),
            loc=(x, y, 0.05), scale=(0.07, 0.07, 0.05), subdivisions=1, radius=1, smooth=True)
    add("primitive_cone_add", rgba("ember"), loc=(0, 0, 0.16),
        scale=(0.16, 0.16, 1), vertices=12, radius1=1, radius2=0, depth=0.30, smooth=True)
    add("primitive_ico_sphere_add", rgba((1.0, 0.86, 0.5, 1.0)), loc=(0, 0, 0.30),
        scale=(0.09, 0.09, 0.09), subdivisions=2, radius=1, smooth=True)
    for a in (0.0, 1.571):
        add("primitive_cube_add", rgba((1.0, 0.9, 0.6, 1.0)), loc=(0, 0, 0.42),
            scale=(0.28, 0.012, 0.012), rot=(0, 0, a))

BUILDERS = {
    "pulse_marker": build_pulse_marker,
    "tree_broadleaf_cluster": build_tree_broadleaf_cluster,
    "tree_conifer_cluster": build_tree_conifer_cluster,
    "hill": build_hill,
    "rocks": build_rocks,
    "crag": build_crag,
    "house_a": build_house_a,
    "house_b": build_house_b,
    "stall_a": build_stall_a,
    "stall_b": build_stall_b,
    "figure": build_figure,
    "figure_staff": build_figure_staff,
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

# ---- ground tiles -------------------------------------------------------------------------------
# Flat top-down PAINTERLY terrain swatches. DioramaView maps these onto its iso ground diamonds
# (one swatch per cell), so the flat-colour-polygon look becomes textured earth/sand/rock/water.
# Lit evenly + opaque so they tile without a baked directional shadow; the in-engine NW raking
# relief shade is layered on top at draw time. Authored procedural texture, not AI, not a photo.
def _ground_mat(tones, scale=5.0, foam=None):
    m = bpy.data.materials.new("ground")
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    bsdf.inputs["Roughness"].default_value = 0.95
    for spec in ("Specular IOR Level", "Specular"):
        if spec in bsdf.inputs:
            bsdf.inputs[spec].default_value = 0.04 if foam is None else 0.18
            break
    try:
        # build up the colour by chaining MixRGB nodes, each driven by a noise at a different
        # scale, blending toward the next palette tone — layered like coarse-to-fine brushwork.
        prev = tones[0]
        prev_is_color = True
        node_out = None
        for i, tone in enumerate(tones[1:], start=1):
            noise = nt.nodes.new("ShaderNodeTexNoise")
            noise.inputs["Scale"].default_value = scale * (1.0 + 0.9 * i)
            noise.inputs["Detail"].default_value = 7.0
            noise.inputs["Roughness"].default_value = 0.7
            mix = nt.nodes.new("ShaderNodeMix")
            mix.data_type = "RGBA"
            mix.inputs[0].default_value = 0.5
            if prev_is_color:
                mix.inputs[6].default_value = prev
            else:
                nt.links.new(prev, mix.inputs[6])
            mix.inputs[7].default_value = tone
            nt.links.new(noise.outputs["Fac"], mix.inputs[0])
            prev = mix.outputs[2]
            prev_is_color = False
            node_out = mix
        # optional foam/sparkle: bright speckle from a high-frequency voronoi, lifted into the mix
        if foam is not None and node_out is not None:
            vor = nt.nodes.new("ShaderNodeTexVoronoi")
            vor.inputs["Scale"].default_value = scale * 8.0
            ramp = nt.nodes.new("ShaderNodeValToRGB")
            ramp.color_ramp.elements[0].position = 0.82
            ramp.color_ramp.elements[1].position = 0.95
            nt.links.new(vor.outputs["Distance"], ramp.inputs["Fac"])
            fmix = nt.nodes.new("ShaderNodeMix")
            fmix.data_type = "RGBA"
            nt.links.new(node_out.outputs[2], fmix.inputs[6])
            fmix.inputs[7].default_value = foam
            nt.links.new(ramp.outputs["Color"], fmix.inputs[0])
            prev = fmix.outputs[2]
        if not prev_is_color:
            nt.links.new(prev, bsdf.inputs["Base Color"])
    except Exception as ex:
        print(f"[diorama] ground mat fallback: {ex}")
        bsdf.inputs["Base Color"].default_value = tones[0]
    return m

def _ground(tones, scale=5.0, foam=None):
    bpy.ops.mesh.primitive_plane_add(size=2.0, location=(0, 0, 0))
    p = bpy.context.active_object
    p.data.materials.append(_ground_mat([rgba(t) for t in tones], scale, rgba(foam) if foam else None))

GROUNDS = {
    "ground_forest":   lambda: _ground(["soil", "moss", "leaf_dark", "grass"], scale=6.0),
    "ground_coast":    lambda: _ground(["field_gold", "grass_dry", "rock_warm", "soil"], scale=5.0),
    "ground_highland": lambda: _ground(["rock", "rock_dark", "rock_warm", "moss"], scale=5.5),
    "ground_water":    lambda: _ground(["water_deep", "water", "water_deep"], scale=4.0,
                                       foam=(0.86, 0.92, 0.92, 1.0)),
    # broad painted terrain zones (the village's living ground): green meadow, worn dirt lanes,
    # and a trodden packed-earth plaza. Bigger noise scale → broad masses, not fine speckle.
    "ground_grass":    lambda: _ground(["meadow", "meadow_dark", "meadow_dry", "moss"], scale=4.2),
    "ground_dirt":     lambda: _ground(["path", "soil", "path_pale", "earth_dark"], scale=4.8),
    "ground_plaza":    lambda: _ground(["earth", "path_pale", "grass_dry", "earth_dark"], scale=4.5),
}

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

    # LM_ONLY="house_a,stall_a" renders just those props (fast iteration); else render all.
    only = [s for s in os.environ.get("LM_ONLY", "").split(",") if s]

    done = []
    for name, builder in BUILDERS.items():
        if only and name not in only:
            continue
        try:
            clear_meshes()
            add_shadow_catcher()
            builder()
            frame_camera(cam)
            scene.render.filepath = os.path.join(outdir, name + ".png")
            bpy.ops.render.render(write_still=True)
            done.append(name)
            print(f"[diorama] rendered {name}.png")
        except Exception as ex:
            print(f"[diorama] FAILED {name}: {ex}")
            import traceback; traceback.print_exc()

    # ---- second pass: top-down OPAQUE ground swatches (different camera + flat even light) -------
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    scene.render.film_transparent = False
    gcam_d = bpy.data.cameras.new("GCam")
    gcam_d.type = "ORTHO"
    gcam_d.ortho_scale = 2.0
    gcam = bpy.data.objects.new("GCam", gcam_d)
    scene.collection.objects.link(gcam)
    gcam.location = (0, 0, 8)
    gcam.rotation_euler = (0, 0, 0)
    scene.camera = gcam
    glight = bpy.data.lights.new("GKey", "SUN")
    glight.energy = 2.6
    go = bpy.data.objects.new("GKey", glight)
    scene.collection.objects.link(go)
    go.rotation_euler = (math.radians(8), 0, math.radians(-30))
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.95
    for name, builder in GROUNDS.items():
        if only and name not in only:
            continue
        clear_meshes()
        builder()
        scene.render.filepath = os.path.join(outdir, name + ".png")
        bpy.ops.render.render(write_still=True)
        done.append(name)
        print(f"[diorama] rendered {name}.png")

    print(f"[diorama] {len(done)} assets -> {outdir}")

main()
