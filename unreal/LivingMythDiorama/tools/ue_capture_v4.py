"""Editor-side capture for GeneratedAtlasV4 — RUN FROM THE OPEN EDITOR (it has a viewport).

Headless -run=pythonscript has no render viewport, so take_high_res_screenshot native-crashes
there. The editor DOES have one, so this helper works when run from the editor Python console:

    py "C:/dev/LIVING MYTH/unreal/LivingMythDiorama/tools/ue_capture_v4.py"

It loads GeneratedAtlasV4, snaps the perspective viewport to CAM_GeneratedAtlasV4_AtlasView,
enables game view (hide editor icons), and writes Saved/Screenshots/GeneratedAtlasV4.png.
"""
import os
import unreal

LEVEL = "/Game/LivingMyth/Maps/GeneratedAtlasV4"
CAM = "CAM_GeneratedAtlasV4_AtlasView"
OUT = os.path.join(unreal.Paths.project_dir(), "Saved", "Screenshots", "GeneratedAtlasV4.png")


def main():
    unreal.EditorLoadingAndSavingUtils.load_map(LEVEL)
    EAS = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    cam = next((a for a in EAS.get_all_level_actors() if a.get_actor_label() == CAM), None)
    if cam is None:
        unreal.log_error("capture: %s not found in %s" % (CAM, LEVEL))
        return
    loc = cam.get_actor_location()
    rot = cam.get_actor_rotation()
    try:
        unreal.EditorLevelLibrary.set_level_viewport_camera_info(loc, rot)
    except Exception as e:
        unreal.log_warning("set_level_viewport_camera_info: %r" % e)
    try:
        unreal.EditorLevelLibrary.editor_set_game_view(True)
    except Exception:
        pass
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    try:
        unreal.AutomationLibrary.take_high_res_screenshot(2560, 1440, OUT)
        unreal.log("capture: queued High-Res screenshot -> %s" % OUT)
    except Exception as e:
        unreal.log_error("take_high_res_screenshot failed: %r "
                         "(use Window > High Resolution Screenshot manually after piloting %s)" % (e, CAM))


main()
