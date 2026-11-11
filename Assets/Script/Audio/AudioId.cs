// DIHASILKAN otomatis dari daftar cue audio. Jangan diubah manual; minta daftar diperbarui lalu hasilkan ulang.
// Nama enum = nama aset cue = awalan nama file klip (mis. Ui_Click_01.wav).
public enum AudioId
{
    None = 0,

    // ---- Musik ----
    Music_Home,
    Music_Explore,
    Music_Latihan,

    // ---- UI umum ----
    Ui_Click,
    Ui_Back,
    Ui_ToggleOn,
    Ui_ToggleOff,
    Ui_StepperTick,
    Ui_NumpadKey,
    Ui_NumpadBackspace,
    Ui_NumpadEnter,
    Ui_Select,
    Ui_PanelOpen,
    Ui_PanelClose,
    Ui_PopupOpen,
    Ui_PopupYes,
    Ui_PopupNo,
    Ui_NotifInfo,
    Ui_NotifSuccess,
    Ui_NotifWarning,
    Ui_NotifError,
    Ui_Locked,
    Ui_DeleteReveal,
    Ui_Swoosh,

    // ---- Drag dan drop UI ----
    Drag_Pickup,
    Drag_Hover,
    Drag_DropOk,
    Drag_DropReject,
    Drag_Cancel,
    Drag_Slide,

    // ---- Umpan balik belajar ----
    Fb_Correct,
    Fb_Wrong,
    Fb_Incomplete,
    Fb_LevelComplete,
    Fb_LevelFail,
    Fb_LevelUnlock,
    Fb_NewQuestion,
    Fb_TimerTick,
    Fb_TimerWarning,
    Fb_StrikeLost,
    Fb_ResultFill,

    // ---- Cairan (Media 1 dan 3) ----
    Liq_GlassTouch,
    Liq_GlassReturn,
    Liq_GlassSlide,
    Liq_PourWater,
    Liq_PourSyrup,
    Liq_PourEnd,
    Liq_Splash,
    Liq_BottleAppear,
    Liq_BottleVanish,
    Liq_Slosh,
    Liq_LevelRise,
    Liq_Merge,
    Liq_FinalCupAppear,
    Liq_EntryAdded,
    Liq_EntryRemoved,
    Liq_CompareMode,

    // ---- Balok (Media 4 dan 6) ----
    Blk_Place,
    Blk_Merge,
    Blk_Lift,
    Blk_Delete,
    Blk_UnitMinus,
    Blk_Reflow,
    Blk_CarryForm,
    Blk_AreaGrow,
    Blk_RowToArea,
    Blk_RowBack,
    Blk_Select,
    Blk_HoverPreview,

    // ---- Garis bilangan (Media 2 dan 5) ----
    Nl_TickSnap,
    Nl_PionLift,
    Nl_PionDrop,
    Nl_HopForward,
    Nl_HopBack,
    Nl_EdgeBump,
    Nl_MarkerAppear,
    Nl_TrailArc,
    Nl_NewProblem,
    Nl_Retry,
    Nl_LineChange,

    // ---- AR ----
    Ar_MarkerFound,
    Ar_MarkerLost,
    Ar_TrackingRestored,
}
