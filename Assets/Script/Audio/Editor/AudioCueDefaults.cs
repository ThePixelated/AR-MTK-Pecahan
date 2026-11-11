#if UNITY_EDITOR
using System.Collections.Generic;

// DIHASILKAN otomatis dari daftar cue audio. Nilai awal tiap cue; hanya dipakai saat aset cue DIBUAT (tidak menimpa suntingan).
public static class AudioCueDefaults
{
    public readonly struct Row
    {
        public readonly AudioBus bus; public readonly bool loop; public readonly float volume, pitchJitter, spatialBlend, cooldown;
        public readonly int maxVoices, priority; public readonly float fadeIn, fadeOut, duckAmount, duckSeconds; public readonly bool stopWhenMuffled;

        public Row(AudioBus bus, bool loop, float volume, float pitchJitter, float spatialBlend, float cooldown, int maxVoices, int priority,
                   float fadeIn, float fadeOut, float duckAmount, float duckSeconds, bool stopWhenMuffled)
        {
            this.bus = bus; this.loop = loop; this.volume = volume; this.pitchJitter = pitchJitter; this.spatialBlend = spatialBlend;
            this.cooldown = cooldown; this.maxVoices = maxVoices; this.priority = priority; this.fadeIn = fadeIn; this.fadeOut = fadeOut;
            this.duckAmount = duckAmount; this.duckSeconds = duckSeconds; this.stopWhenMuffled = stopWhenMuffled;
        }
    }

    static readonly Dictionary<AudioId, Row> rows = new Dictionary<AudioId, Row>
    {
        { AudioId.Music_Home, new Row(AudioBus.Music, true, 1f, 0f, 0f, 0f, 1, 0, 1.2f, 1.2f, 0f, 0f, false) },
        { AudioId.Music_Explore, new Row(AudioBus.Music, true, 1f, 0f, 0f, 0f, 1, 0, 1.2f, 1.2f, 0f, 0f, false) },
        { AudioId.Music_Latihan, new Row(AudioBus.Music, true, 1f, 0f, 0f, 0f, 1, 0, 1.2f, 1.2f, 0f, 0f, false) },
        { AudioId.Ui_Click, new Row(AudioBus.Ui, false, 0.8f, 0.05f, 0f, 0.05f, 3, 128, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Ui_Back, new Row(AudioBus.Ui, false, 0.8f, 0.05f, 0f, 0.1f, 2, 128, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Ui_ToggleOn, new Row(AudioBus.Ui, false, 0.8f, 0.05f, 0f, 0.1f, 2, 128, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Ui_ToggleOff, new Row(AudioBus.Ui, false, 0.8f, 0.05f, 0f, 0.1f, 2, 128, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Ui_StepperTick, new Row(AudioBus.Ui, false, 0.7f, 0.05f, 0f, 0.03f, 3, 128, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Ui_NumpadKey, new Row(AudioBus.Ui, false, 0.75f, 0.05f, 0f, 0.03f, 4, 128, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Ui_NumpadBackspace, new Row(AudioBus.Ui, false, 0.75f, 0.05f, 0f, 0.05f, 2, 128, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Ui_NumpadEnter, new Row(AudioBus.Ui, false, 0.85f, 0.05f, 0f, 0.1f, 2, 128, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Ui_Select, new Row(AudioBus.Ui, false, 0.85f, 0.05f, 0f, 0.08f, 2, 128, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Ui_PanelOpen, new Row(AudioBus.Ui, false, 0.7f, 0.06f, 0f, 0.15f, 2, 140, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Ui_PanelClose, new Row(AudioBus.Ui, false, 0.7f, 0.05f, 0f, 0.15f, 2, 128, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Ui_PopupOpen, new Row(AudioBus.Ui, false, 0.8f, 0.05f, 0f, 0.2f, 1, 128, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Ui_PopupYes, new Row(AudioBus.Ui, false, 0.8f, 0.05f, 0f, 0.1f, 1, 128, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Ui_PopupNo, new Row(AudioBus.Ui, false, 0.8f, 0.05f, 0f, 0.1f, 1, 128, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Ui_NotifInfo, new Row(AudioBus.Ui, false, 0.7f, 0.05f, 0f, 0.3f, 1, 128, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Ui_NotifSuccess, new Row(AudioBus.Ui, false, 0.75f, 0.05f, 0f, 0.3f, 1, 128, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Ui_NotifWarning, new Row(AudioBus.Ui, false, 0.75f, 0.05f, 0f, 0.3f, 1, 128, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Ui_NotifError, new Row(AudioBus.Ui, false, 0.75f, 0.05f, 0f, 0.3f, 1, 128, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Ui_Locked, new Row(AudioBus.Ui, false, 0.8f, 0.05f, 0f, 0.2f, 1, 128, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Ui_DeleteReveal, new Row(AudioBus.Ui, false, 0.65f, 0.05f, 0f, 0.15f, 1, 128, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Ui_Swoosh, new Row(AudioBus.Ui, false, 0.6f, 0.08f, 0f, 0.15f, 2, 128, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Drag_Pickup, new Row(AudioBus.Ui, false, 0.8f, 0.05f, 0f, 0.08f, 2, 128, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Drag_Hover, new Row(AudioBus.Ui, false, 0.4f, 0.05f, 0f, 0.25f, 1, 200, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Drag_DropOk, new Row(AudioBus.Ui, false, 0.9f, 0.05f, 0f, 0.08f, 2, 128, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Drag_DropReject, new Row(AudioBus.Ui, false, 0.85f, 0.05f, 0f, 0.2f, 1, 128, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Drag_Cancel, new Row(AudioBus.Ui, false, 0.7f, 0.05f, 0f, 0.15f, 1, 128, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Drag_Slide, new Row(AudioBus.Ui, false, 0.6f, 0.05f, 0f, 0.15f, 1, 128, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Fb_Correct, new Row(AudioBus.Feedback, false, 1f, 0.05f, 0f, 0.5f, 1, 32, 0f, 0.1f, 0.5f, 0.9f, false) },
        { AudioId.Fb_Wrong, new Row(AudioBus.Feedback, false, 0.9f, 0.05f, 0f, 0.5f, 1, 32, 0f, 0.1f, 0.3f, 0.5f, false) },
        { AudioId.Fb_Incomplete, new Row(AudioBus.Feedback, false, 0.8f, 0.05f, 0f, 0.4f, 1, 32, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Fb_LevelComplete, new Row(AudioBus.Feedback, false, 1f, 0.05f, 0f, 1f, 1, 32, 0f, 0.1f, 0.7f, 2.5f, false) },
        { AudioId.Fb_LevelFail, new Row(AudioBus.Feedback, false, 0.9f, 0.05f, 0f, 1f, 1, 32, 0f, 0.1f, 0.5f, 1.2f, false) },
        { AudioId.Fb_LevelUnlock, new Row(AudioBus.Feedback, false, 0.9f, 0.05f, 0f, 0.6f, 1, 32, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Fb_NewQuestion, new Row(AudioBus.Feedback, false, 0.8f, 0.05f, 0f, 0.3f, 1, 32, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Fb_TimerTick, new Row(AudioBus.Feedback, false, 0.5f, 0.05f, 0f, 0.9f, 1, 32, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Fb_TimerWarning, new Row(AudioBus.Feedback, false, 0.7f, 0.05f, 0f, 0.9f, 1, 32, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Fb_StrikeLost, new Row(AudioBus.Feedback, false, 0.8f, 0.05f, 0f, 0.4f, 1, 32, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Fb_ResultFill, new Row(AudioBus.Feedback, false, 0.8f, 0.05f, 0f, 0.3f, 1, 32, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Liq_GlassTouch, new Row(AudioBus.World, false, 0.8f, 0.05f, 0.3f, 0.1f, 2, 100, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Liq_GlassReturn, new Row(AudioBus.World, false, 0.8f, 0.05f, 0.3f, 0.15f, 2, 100, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Liq_GlassSlide, new Row(AudioBus.World, false, 0.5f, 0.05f, 0.3f, 0.3f, 1, 100, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Liq_PourWater, new Row(AudioBus.World, true, 0.8f, 0.05f, 0.3f, 0.1f, 2, 100, 0.08f, 0.2f, 0f, 0f, true) },
        { AudioId.Liq_PourSyrup, new Row(AudioBus.World, true, 0.8f, 0.05f, 0.3f, 0.1f, 2, 100, 0.1f, 0.25f, 0f, 0f, true) },
        { AudioId.Liq_PourEnd, new Row(AudioBus.World, false, 0.7f, 0.05f, 0.3f, 0.2f, 1, 100, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Liq_Splash, new Row(AudioBus.World, false, 0.7f, 0.05f, 0.3f, 0.2f, 2, 100, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Liq_BottleAppear, new Row(AudioBus.World, false, 0.8f, 0.05f, 0.3f, 0.2f, 1, 100, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Liq_BottleVanish, new Row(AudioBus.World, false, 0.8f, 0.05f, 0.3f, 0.2f, 1, 100, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Liq_Slosh, new Row(AudioBus.World, true, 0.5f, 0.05f, 0.3f, 0.1f, 2, 100, 0.15f, 0.3f, 0f, 0f, true) },
        { AudioId.Liq_LevelRise, new Row(AudioBus.World, true, 0.45f, 0.05f, 0.3f, 0.1f, 1, 100, 0.1f, 0.2f, 0f, 0f, true) },
        { AudioId.Liq_Merge, new Row(AudioBus.World, false, 0.85f, 0.05f, 0.3f, 0.4f, 1, 100, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Liq_FinalCupAppear, new Row(AudioBus.World, false, 0.85f, 0.05f, 0.3f, 0.2f, 2, 100, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Liq_EntryAdded, new Row(AudioBus.World, false, 0.85f, 0.05f, 0.3f, 0.3f, 1, 100, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Liq_EntryRemoved, new Row(AudioBus.World, false, 0.7f, 0.05f, 0.3f, 0.2f, 1, 100, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Liq_CompareMode, new Row(AudioBus.World, false, 0.8f, 0.05f, 0.3f, 0.4f, 1, 100, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Blk_Place, new Row(AudioBus.World, false, 0.9f, 0.06f, 0.3f, 0.08f, 3, 100, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Blk_Merge, new Row(AudioBus.World, false, 0.85f, 0.05f, 0.3f, 0.06f, 3, 100, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Blk_Lift, new Row(AudioBus.World, false, 0.75f, 0.05f, 0.3f, 0.1f, 2, 100, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Blk_Delete, new Row(AudioBus.World, false, 0.8f, 0.05f, 0.3f, 0.15f, 2, 100, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Blk_UnitMinus, new Row(AudioBus.World, false, 0.75f, 0.05f, 0.3f, 0.1f, 2, 100, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Blk_Reflow, new Row(AudioBus.World, false, 0.55f, 0.05f, 0.3f, 0.25f, 1, 100, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Blk_CarryForm, new Row(AudioBus.World, false, 0.9f, 0.05f, 0.3f, 0.4f, 1, 100, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Blk_AreaGrow, new Row(AudioBus.World, false, 0.5f, 0.05f, 0.3f, 0.3f, 1, 100, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Blk_RowToArea, new Row(AudioBus.World, false, 0.9f, 0.05f, 0.3f, 0.1f, 2, 100, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Blk_RowBack, new Row(AudioBus.World, false, 0.75f, 0.05f, 0.3f, 0.1f, 2, 100, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Blk_Select, new Row(AudioBus.World, false, 0.6f, 0.05f, 0.3f, 0.08f, 1, 100, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Blk_HoverPreview, new Row(AudioBus.World, false, 0.35f, 0.05f, 0.3f, 0.25f, 1, 200, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Nl_TickSnap, new Row(AudioBus.World, false, 0.7f, 0f, 0f, 0.03f, 4, 110, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Nl_PionLift, new Row(AudioBus.World, false, 0.7f, 0.05f, 0f, 0.1f, 1, 110, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Nl_PionDrop, new Row(AudioBus.World, false, 0.75f, 0.05f, 0f, 0.1f, 1, 110, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Nl_HopForward, new Row(AudioBus.World, false, 0.85f, 0.06f, 0f, 0.08f, 2, 110, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Nl_HopBack, new Row(AudioBus.World, false, 0.85f, 0.06f, 0f, 0.08f, 2, 110, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Nl_EdgeBump, new Row(AudioBus.World, false, 0.75f, 0.05f, 0f, 0.25f, 1, 110, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Nl_MarkerAppear, new Row(AudioBus.World, false, 0.65f, 0.05f, 0f, 0.1f, 3, 110, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Nl_TrailArc, new Row(AudioBus.World, false, 0.5f, 0.05f, 0f, 0.1f, 2, 110, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Nl_NewProblem, new Row(AudioBus.World, false, 0.8f, 0.05f, 0f, 0.3f, 1, 110, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Nl_Retry, new Row(AudioBus.World, false, 0.75f, 0.05f, 0f, 0.3f, 1, 110, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Nl_LineChange, new Row(AudioBus.World, false, 0.6f, 0.05f, 0f, 0.2f, 1, 110, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Ar_MarkerFound, new Row(AudioBus.World, false, 0.9f, 0.05f, 0f, 0.5f, 1, 80, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Ar_MarkerLost, new Row(AudioBus.World, false, 0.85f, 0.05f, 0f, 0.5f, 1, 80, 0f, 0.1f, 0f, 0f, false) },
        { AudioId.Ar_TrackingRestored, new Row(AudioBus.World, false, 0.8f, 0.05f, 0f, 0.5f, 1, 80, 0f, 0.1f, 0f, 0f, false) },
    };

    public static bool TryGet(AudioId id, out Row row) => rows.TryGetValue(id, out row);
}
#endif
