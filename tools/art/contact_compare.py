# Old-vs-new silhouette comparison sheet: pairs each prop's V1 asset (left) against the new
# Biome Silhouette V1 asset (right). Run with Blender:
#   blender -b -P tools/art/contact_compare.py -- <old_dir> <new_dir> <out.png>
import bpy, sys, os
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OLD = os.path.abspath(argv[0]) if argv else os.path.abspath("/tmp/v1_assets")
NEW = os.path.abspath(argv[1]) if len(argv) > 1 else os.path.abspath("godot/assets/diorama")
OUT = os.path.abspath(argv[2]) if len(argv) > 2 else os.path.abspath("compare.png")

# the props whose FORM changed this milestone (crag is new — no old, shown new-only)
NAMES = ["tree_broadleaf_1", "tree_broadleaf_2", "tree_conifer_1", "tree_conifer_2",
         "rocks", "keep", "dock", "standing_stones"]
CELL, PAD, GAP = 200, 12, 22   # GAP between an old|new pair
BG = (0.10, 0.12, 0.14)

def load_rgba(path, size):
    if not os.path.exists(path):
        return np.zeros((size, size, 4), dtype=np.float32)
    img = bpy.data.images.load(path)
    img.scale(size, size)
    a = np.array(img.pixels[:], dtype=np.float32).reshape(size, size, 4)
    bpy.data.images.remove(img)
    return np.flipud(a)

PAIRW = CELL * 2          # old + new
COLS = 2                  # two pairs per row
rows = (len(NAMES) + COLS - 1) // COLS
CW = COLS * PAIRW + (COLS + 1) * PAD + COLS * GAP
CH = rows * CELL + (rows + 1) * PAD
canvas = np.zeros((CH, CW, 4), dtype=np.float32)
canvas[..., 0], canvas[..., 1], canvas[..., 2], canvas[..., 3] = (*BG, 1.0)

def blit(cell, y0, x0):
    a = cell[..., 3:4]
    canvas[y0:y0+CELL, x0:x0+CELL, :3] = cell[..., :3]*a + canvas[y0:y0+CELL, x0:x0+CELL, :3]*(1-a)

for i, name in enumerate(NAMES):
    r, c = divmod(i, COLS)
    y0 = PAD + r * (CELL + PAD)
    x0 = PAD + c * (PAIRW + PAD + GAP)
    blit(load_rgba(os.path.join(OLD, name + ".png"), CELL), y0, x0)          # old (left)
    blit(load_rgba(os.path.join(NEW, name + ".png"), CELL), y0, x0 + CELL)   # new (right)
    # a thin divider tint between old and new
    canvas[y0:y0+CELL, x0+CELL-1:x0+CELL+1, :3] = (0.8, 0.6, 0.25)

out = bpy.data.images.new("compare", CW, CH, alpha=True)
out.pixels = np.flipud(canvas).reshape(-1).tolist()
out.filepath_raw = OUT
out.file_format = "PNG"
out.save()
print("compare sheet ->", OUT, f"{CW}x{CH}")
