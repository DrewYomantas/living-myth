# Assemble a contact sheet of the diorama proof-kit assets into one PNG.
# Run with Blender (uses its bundled numpy + image API — no Pillow/ImageMagick needed):
#   blender -b -P tools/art/contact_sheet.py -- godot/assets/diorama <out.png>
import bpy, sys, os
import numpy as np

argv = sys.argv[argv.index("--") + 1:] if "--" in (argv := sys.argv) else []
SRC = os.path.abspath(argv[0]) if argv else os.path.abspath("godot/assets/diorama")
OUT = os.path.abspath(argv[1]) if len(argv) > 1 else os.path.abspath("contact.png")

# proof-kit order: grounds, then trees, then settlements/markers
NAMES = [
    "ground_coast", "ground_forest", "ground_highland", "ground_water",
    "tree_broadleaf_1", "tree_conifer_1", "rocks", "field",
    "keep", "house_b", "watchtower", "dock",
    "standing_stones", "shrine", "banner", "pulse_marker",
]
COLS, CELL, PAD = 4, 220, 14
BG = (0.10, 0.12, 0.14)   # dark slate so the parchment chrome / cut-outs read

def load_rgba(path, size):
    img = bpy.data.images.load(path)
    img.scale(size, size)
    a = np.array(img.pixels[:], dtype=np.float32).reshape(size, size, 4)
    bpy.data.images.remove(img)
    return np.flipud(a)   # Blender pixels are bottom-up; flip to top-down for placement

rows = (len(NAMES) + COLS - 1) // COLS
CW = COLS * CELL + (COLS + 1) * PAD
CH = rows * CELL + (rows + 1) * PAD
canvas = np.zeros((CH, CW, 4), dtype=np.float32)
canvas[..., 0], canvas[..., 1], canvas[..., 2], canvas[..., 3] = (*BG, 1.0)

for i, name in enumerate(NAMES):
    p = os.path.join(SRC, name + ".png")
    if not os.path.exists(p):
        print("contact: missing", name); continue
    cell = load_rgba(p, CELL)
    r, c = divmod(i, COLS)
    y0 = PAD + r * (CELL + PAD)
    x0 = PAD + c * (CELL + PAD)
    a = cell[..., 3:4]
    canvas[y0:y0 + CELL, x0:x0 + CELL, :3] = cell[..., :3] * a + canvas[y0:y0 + CELL, x0:x0 + CELL, :3] * (1 - a)

out = bpy.data.images.new("contact", CW, CH, alpha=True)
out.pixels = np.flipud(canvas).reshape(-1).tolist()
out.filepath_raw = OUT
out.file_format = "PNG"
out.save()
print("contact sheet ->", OUT, f"{CW}x{CH}")
