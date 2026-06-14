# Thin pykrita shim so kritarunner can run the repo's headless paintover (krita_paintover.py).
# kritarunner discovers plugins from its OWN resource dir (%APPDATA%/kritarunner/pykrita on
# Windows) and only loads ones enabled in kritarunnerrc — see INSTALL.md. It also calls the
# entry function WITH an args list, so run_main must tolerate extra args.
import os, sys
repo = os.environ.get("LM_REPO", r"C:/Users/beyon/OneDrive/Desktop/LIVING MYTH")
sys.path.insert(0, os.path.join(repo, "tools", "art"))
from krita_paintover import run_main as _rm  # noqa: E402


def run_main(*args, **kwargs):
    return _rm()
