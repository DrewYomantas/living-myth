"""Editor-side V5.1 capture — RUN FROM THE OPEN EDITOR (needs a viewport).

Loads GeneratedAtlasV5_1_IntegrationPolish and captures the three review cameras, each after a
frame-settle so auto-exposure / skylight realtime-capture converge (else the first shot is near-black):
  CAM_V51_Atlas    -> Saved/Screenshots/V5_1_atlas.png
  CAM_V51_Region   -> Saved/Screenshots/V5_1_region.png
  CAM_V51_Inspect  -> Saved/Screenshots/V5_1_inspect.png
"""
import os
import unreal

LEVEL = "/Game/LivingMyth/Maps/GeneratedAtlasV5_1_IntegrationPolish"
SHOTS = os.path.join(unreal.Paths.project_dir(), "Saved", "Screenshots")
SEQ = [("CAM_V51_Atlas", "V5_1_atlas.png"), ("CAM_V51_Region", "V5_1_region.png"),
       ("CAM_V51_Inspect", "V5_1_inspect.png")]
SETTLE = 150   # frames to converge exposure before the shot
HOLD = 70      # frames to let the async screenshot write before switching cameras


def main():
    unreal.EditorLoadingAndSavingUtils.load_map(LEVEL)
    EAS = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    actors = {a.get_actor_label(): a for a in EAS.get_all_level_actors()}
    cams = []
    for label, out in SEQ:
        a = actors.get(label)
        if a is None:
            unreal.log_error("V5.1 capture: camera %s not found" % label)
            continue
        cams.append((a.get_actor_location(), a.get_actor_rotation(), os.path.join(SHOTS, out), label))
    if not cams:
        return
    os.makedirs(SHOTS, exist_ok=True)
    state = {"i": 0, "n": 0, "shot": False, "handle": None}

    def tick(dt):
        if state["i"] >= len(cams):
            if state["handle"] is not None:
                unreal.unregister_slate_post_tick_callback(state["handle"])
                state["handle"] = None
            return
        loc, rot, out, label = cams[state["i"]]
        try:
            unreal.EditorLevelLibrary.set_level_viewport_camera_info(loc, rot)
            unreal.EditorLevelLibrary.editor_set_game_view(True)
        except Exception:
            pass
        state["n"] += 1
        if state["n"] >= SETTLE and not state["shot"]:
            state["shot"] = True
            try:
                unreal.AutomationLibrary.take_high_res_screenshot(2560, 1440, out)
                unreal.log("V5.1 capture: %s -> %s (after %d frames)" % (label, out, state["n"]))
            except Exception as e:
                unreal.log_error("V5.1 capture %s failed: %r" % (label, e))
        if state["n"] >= SETTLE + HOLD:
            state["i"] += 1
            state["n"] = 0
            state["shot"] = False

    state["handle"] = unreal.register_slate_post_tick_callback(tick)
    unreal.log("V5.1 capture: sequencing %d cameras (settle %d / hold %d)..." % (len(cams), SETTLE, HOLD))


main()
