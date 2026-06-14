# Living Myth — North Star diorama PAINTOVER stage (headless, scripted).
#
# Stage 2 of the art pipeline. Blender (render_diorama.py) produces clean lit base PNGs;
# this drives Krita HEADLESSLY (via kritarunner) to apply a reproducible PAINTERLY filter
# pass on top — texture/grain, edge ink, and a richer grade — turning the soft flat-shaded
# bases into hand-finished-reading diorama art. No hand painting, no AI: a deterministic
# Krita filter chain, re-runnable from the same Blender output.
#
# Run (headless):
#   LM_KRITA_MODE=apply "C:\Program Files\Krita (x64)\bin\kritarunner.com" \
#       -s "C:/Users/beyon/OneDrive/Desktop/LIVING MYTH/tools/art/krita_paintover" \
#       -f run_main
#
# MODE=probe  → dump filter list + test one ground + one prop to *_probe.png (learn the API)
# MODE=apply  → paint over every base PNG in-place (grounds get full chain, props get a
#               gentler alpha-safe chain)
#
# Why kritarunner: it runs one pykrita script with the `krita` module bound, no GUI, and
# returns — the official batch path. Output is logged to tools/art/krita_paintover.log so the
# result is inspectable even though headless stdout is unreliable on Windows.

import os, glob, traceback

DIORAMA = os.environ.get("LM_KRITA_DIR",
                         r"C:/Users/beyon/OneDrive/Desktop/LIVING MYTH/godot/assets/diorama")
LOGP = os.environ.get("LM_KRITA_LOG",
                      r"C:/Users/beyon/OneDrive/Desktop/LIVING MYTH/tools/art/krita_paintover.log")


def log(msg):
    with open(LOGP, "a", encoding="utf-8") as f:
        f.write(str(msg) + "\n")


def run_main():
    open(LOGP, "w", encoding="utf-8").close()   # truncate
    try:
        log("run_main entered")
        from krita import Krita
        log("krita imported")
        try:
            from krita import InfoObject
        except Exception:
            from PyKrita.krita import InfoObject   # fallback location
        log("InfoObject imported")
        app = Krita.instance()
        log("instance: " + repr(app))
        mode = os.environ.get("LM_KRITA_MODE", "probe")
        try:
            ver = app.version()
        except Exception as ex:
            ver = "?(" + repr(ex) + ")"
        log(f"=== start  mode={mode}  version={ver} ===")
        filters = sorted(app.filters())
        log("AVAILABLE FILTERS (%d): %s" % (len(filters), ", ".join(filters)))
        if mode == "probe":
            probe(app, InfoObject)
        else:
            apply_all(app, InfoObject)
        log("=== done ===")
    except Exception:
        log("RUN_MAIN EXC:\n" + traceback.format_exc())


def _open(app, path):
    doc = app.openDocument(path)
    doc.setBatchmode(True)
    doc.flatten()
    return doc


def _base_node(doc):
    kids = doc.rootNode().childNodes()
    return kids[-1] if kids else doc.rootNode()


def probe(app, InfoObject):
    for sample in ("ground_forest.png", "tree_broadleaf_1.png"):
        path = os.path.join(DIORAMA, sample)
        if not os.path.exists(path):
            log("probe: missing " + sample); continue
        doc = _open(app, path)
        w, h = doc.width(), doc.height()
        node = _base_node(doc)
        log(f"\nPROBE {sample}: {w}x{h}  node={node.name()} type={node.type()}")
        # 1) plain gaussian blur
        try:
            f = app.filter("gaussian blur")
            log("  gaussian blur cfg props: " + str(_cfg_props(f)))
            f.apply(node, 0, 0, w, h)
            doc.refreshProjection()
            doc.exportImage(path.replace(".png", "_probe_blur.png"), InfoObject())
            log("  blur OK")
        except Exception as ex:
            log("  blur FAIL: " + repr(ex))
        # 2) layered edge-ink (duplicate -> edge detect -> invert -> multiply)
        try:
            doc2 = _open(app, path)
            w2, h2 = doc2.width(), doc2.height()
            base = _base_node(doc2)
            dup = base.duplicate()
            doc2.rootNode().addChildNode(dup, base)
            for fn in ("edge detection", "invert"):
                ff = app.filter(fn)
                if ff is None: log("  no filter id: " + fn); continue
                ff.apply(dup, 0, 0, w2, h2)
            dup.setBlendingMode("multiply")
            dup.setOpacity(130)
            doc2.refreshProjection()
            doc2.flatten()
            doc2.exportImage(path.replace(".png", "_probe_ink.png"), InfoObject())
            log("  ink-layer OK")
        except Exception as ex:
            log("  ink-layer FAIL: " + repr(ex) + "\n" + traceback.format_exc())


def _cfg_props(f):
    try:
        c = f.configuration()
        return c.properties()
    except Exception as ex:
        return "cfg? " + repr(ex)


# --- the production chain -----------------------------------------------------------------------
GROUNDS = {"ground_forest", "ground_coast", "ground_highland", "ground_water"}


def apply_all(app, InfoObject):
    pngs = [p for p in glob.glob(os.path.join(DIORAMA, "*.png"))
            if "_probe" not in os.path.basename(p)]
    for path in sorted(pngs):
        name = os.path.splitext(os.path.basename(path))[0]
        is_ground = name in GROUNDS
        try:
            paint_one(app, InfoObject, path, is_ground)
            log(f"painted {name} ({'ground' if is_ground else 'prop'})")
        except Exception as ex:
            log(f"FAIL {name}: {repr(ex)}\n{traceback.format_exc()}")


def _set(f, **props):
    try:
        c = f.configuration()
        for k, v in props.items():
            c.setProperty(k, v)
        f.setConfiguration(c)
    except Exception as ex:
        log("  cfg skip: " + repr(ex))


def paint_one(app, InfoObject, path, is_ground):
    doc = _open(app, path)
    w, h = doc.width(), doc.height()
    node = _base_node(doc)

    # 1) painterly base: a small gaussian smear then unsharp restores edges with painted contrast
    fb = app.filter("gaussian blur")
    if fb:
        _set(fb, horizRadius=2.0, vertRadius=2.0, lockAspect=True)
        fb.apply(node, 0, 0, w, h)
    fu = app.filter("unsharp")
    if fu:
        fu.apply(node, 0, 0, w, h)

    # 2) edge-ink overlay: duplicate -> edge detection -> invert -> multiply. Gives forms hand-
    #    drawn outlines/contours (strong on props, subtle on smooth ground). Alpha is preserved:
    #    transparent regions invert to white and multiply to no-op, so prop silhouettes stay clean.
    try:
        dup = node.duplicate()
        doc.rootNode().addChildNode(dup, node)
        for fn in ("edge detection", "invert"):
            ff = app.filter(fn)
            if ff:
                ff.apply(dup, 0, 0, w, h)
        dup.setBlendingMode("multiply")
        # invert flips the ALPHA channel too, so the (transparent) background becomes opaque white
        # and multiply washes a grey box behind every prop. Alpha inheritance clips the ink layer
        # to the base silhouette, so transparent stays transparent. (No-op on opaque grounds.)
        dup.setInheritAlpha(True)
        dup.setOpacity(80 if is_ground else 120)
    except Exception as ex:
        log("  ink skip: " + repr(ex))

    doc.refreshProjection()
    doc.flatten()
    # export WITH alpha — default PNG export composites onto white and kills the prop cut-outs
    # (every sprite would render as an opaque white box). These keys force a transparent RGBA PNG.
    cfg = InfoObject()
    cfg.setProperty("alpha", True)
    cfg.setProperty("indexed", False)
    cfg.setProperty("compression", 3)
    cfg.setProperty("forceSRGB", False)
    cfg.setProperty("saveSRGBProfile", False)
    doc.exportImage(path, cfg)
    doc.close()
