"""Editor-side capture for GeneratedAtlasV4 — RUN FROM THE OPEN EDITOR (it has a viewport).

Headless -run=pythonscript has no render viewport, so take_high_res_screenshot native-crashes
there. The editor DOES have one, so this helper works when run from the editor Python console:

    py "C:/dev/LIVING MYTH/unreal/LivingMythDiorama/tools/ue_capture_v4.py"

It loads GeneratedAtlasV4, snaps the perspective viewport to CAM_GeneratedAtlasV4_AtlasView,
enables game view (hide editor icons), then waits ~180 render frames for auto-exposure / eye
adaptation (and the movable SkyLight's realtime capture) to SETTLE before taking the high-res
screenshot — otherwise the screenshot fires on an un-adapted frame and writes a near-black image.
Writes Saved/Screenshots/GeneratedAtlasV4.png. Returns immediately; the screenshot lands a few
seconds later once the editor has ticked the settle frames.
"""
import os
import unreal

LEVEL = "/Game/LivingMyth/Maps/GeneratedAtlasV4"
CAM = "CAM_GeneratedAtlasV4_AtlasView"
OUT = os.path.join(unreal.Paths.project_dir(), "Saved", "Screenshots", "GeneratedAtlasV4.png")
SETTLE_FRAMES = 180  # editor ticks to let auto-exposure / skylight realtime-capture converge


def main():
    unreal.EditorLoadingAndSavingUtils.load_map(LEVEL)
    EAS = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    cam = next((a for a in EAS.get_all_level_actors() if a.get_actor_label() == CAM), None)
    if cam is None:
        unreal.log_error("capture: %s not found in %s" % (CAM, LEVEL))
        return
    loc = cam.get_actor_location()
    rot = cam.get_actor_rotation()

    state = {"n": 0, "shot": False, "handle": None}

    def _tick(dt):
        state["n"] += 1
        try:
            unreal.EditorLevelLibrary.set_level_viewport_camera_info(loc, rot)
            unreal.EditorLevelLibrary.editor_set_game_view(True)
        except Exception:
            pass
        if state["n"] >= SETTLE_FRAMES and not state["shot"]:
            state["shot"] = True
            os.makedirs(os.path.dirname(OUT), exist_ok=True)
            try:
                unreal.AutomationLibrary.take_high_res_screenshot(2560, 1440, OUT)
                unreal.log("capture: queued High-Res screenshot after %d settle frames -> %s"
                           % (state["n"], OUT))
            except Exception as e:
                unreal.log_error("take_high_res_screenshot failed: %r "
                                 "(use Window > High Resolution Screenshot manually after piloting %s)"
                                 % (e, CAM))
            if state["handle"] is not None:
                unreal.unregister_slate_post_tick_callback(state["handle"])

    state["handle"] = unreal.register_slate_post_tick_callback(_tick)
    unreal.log("capture: settling %d frames before screenshot (it will land shortly)..." % SETTLE_FRAMES)


main()
