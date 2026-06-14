# Headless Krita paintover — install (kritarunner)

The paintover stage (`tools/art/krita_paintover.py`) runs **headless** through Krita's
`kritarunner.exe`. Getting kritarunner to find a user plugin has three non-obvious requirements
(all learned the hard way — documented so the next run is one command):

1. **kritarunner has its OWN resource dir**, separate from the Krita GUI. On Windows that's
   `%APPDATA%\kritarunner\pykrita\` (NOT `%APPDATA%\krita\pykrita\`). The plugin must live there.
2. **The plugin must be enabled** in `%LOCALAPPDATA%\kritarunnerrc` under a `[python]` section:
   `enable_lm_paintover=true`.
3. **kritarunner calls the entry function with an args list**, so the entry must accept `*args`
   (the shim's `run_main(*args, **kwargs)` does).

## One-time install (PowerShell)

```powershell
$dst = "$env:APPDATA\kritarunner\pykrita"
New-Item -ItemType Directory -Force $dst | Out-Null
Copy-Item -Recurse -Force "tools\art\krita_plugin\lm_paintover" $dst
Copy-Item -Force "tools\art\krita_plugin\kritapykrita_lm_paintover.desktop" $dst
Add-Content "$env:LOCALAPPDATA\kritarunnerrc" "`n[python]`nenable_lm_paintover=true"
```

The shim resolves the repo via the `LM_REPO` env var (default is Drew's path); set it if the
repo lives elsewhere.

## Run the paintover

```bash
# 1) render clean Blender bases (overwrites godot/assets/diorama/*.png)
blender -b -P tools/art/render_diorama.py -- godot/assets/diorama
# 2) paint over them IN PLACE (headless Krita: gaussian blur -> unsharp -> edge-ink multiply)
LM_KRITA_MODE=apply kritarunner -s lm_paintover -f run_main
```

`LM_KRITA_MODE=probe` instead dumps the filter list + two sample renders for tuning. Progress is
logged to `tools/art/krita_paintover.log`. **The paintover is in-place and not idempotent** —
always re-render the Blender bases (step 1) before re-painting, or it double-applies.
