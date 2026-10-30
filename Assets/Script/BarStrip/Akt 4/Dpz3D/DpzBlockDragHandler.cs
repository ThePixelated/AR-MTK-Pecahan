using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Menekan lalu menggeser balok di dpz = mengangkatnya. Pakai FractionDragController yang sama dengan drag bank.
// Tekan lalu lepas tanpa geser = tap (pada balok, atau di luar balok).

public class DpzBlockDragHandler : MonoBehaviour
{
    [SerializeField] DpzView dpzView;
    [SerializeField] Camera worldCamera;                 // kosong = Camera.main
    [SerializeField] float dragThresholdPixels = 14f;
    [SerializeField] bool returnUpperOnTap = true;        // tap balok bagian atas = kembalikan ke Kotak asalnya
    
    public event Action<int> UpperBlockTapped;
    public event Action<int> BlockTapped;                // tap pada balok (index balok)
    public event Action BackgroundTapped;                // tap di luar balok dan di luar UI (mencabut pilihan)

    enum State { Idle, PressedBlock, PressedUpper, PressedAway, Dragging }
    static readonly List<RaycastResult> uiHits = new List<RaycastResult>();

    State state;
    int candidate = -1;
    Vector2 pressPos;

    Camera WorldCam => worldCamera ? worldCamera : Camera.main;

    void OnEnable()
    {
        if (dpzView == null)
            Debug.LogError($"[{name}] DpzBlockDragHandler: field 'Dpz View' kosong, angkat dan tap balok tidak akan jalan", this);
    }

    void OnDisable()
    {
        if (state == State.Dragging) FinishLift(Vector2.zero, cancel: true);
        state = State.Idle;
        candidate = -1;
    }

    bool Moved(Vector2 pos) => (pos - pressPos).sqrMagnitude > dragThresholdPixels * dragThresholdPixels;

    void Update()
    {
        Pointer ptr = Pointer.current;
        var drag = FractionDragController.Instance;
        if (ptr == null || drag == null || dpzView == null) return;

        Vector2 pos = ptr.position.ReadValue();

        switch (state)
        {
            case State.Idle:
                if (ptr.press.wasPressedThisFrame && !drag.IsDragging) TryPress(pos);
                break;

            case State.PressedBlock:
                if (!ptr.press.isPressed)
                {
                    int tapped = candidate;                 // lepas tanpa geser = tap pada balok
                    state = State.Idle;
                    candidate = -1;
                    BlockTapped?.Invoke(tapped);
                }
                else if (Moved(pos))
                {
                    BeginLift(pos, drag);
                }
                break;

            case State.PressedUpper:
                if (!ptr.press.isPressed)
                {
                    int tappedUpper = candidate;
                    state = State.Idle;
                    candidate = -1;
                    UpperBlockTapped?.Invoke(tappedUpper);
                    if (returnUpperOnTap) dpzView.ReturnUpperBlock(tappedUpper);
                }
                else if (Moved(pos))
                {
                    state = State.Idle;                     // digeser = bukan tap
                    candidate = -1;
                }
                break;

            case State.PressedAway:
                if (!ptr.press.isPressed)
                {
                    state = State.Idle;                     // lepas tanpa geser = tap di luar balok
                    BackgroundTapped?.Invoke();
                }
                else if (Moved(pos))
                {
                    state = State.Idle;                     // digeser = bukan tap (mis. menggeser kamera)
                }
                break;

            case State.Dragging:
                if (!drag.IsDragging)                       // dibatalkan dari luar
                {
                    dpzView.SetLiftedBlock(-1);
                    state = State.Idle;
                    candidate = -1;
                    break;
                }
                drag.Move(pos);
                if (!ptr.press.isPressed) FinishLift(pos, cancel: false);
                break;
        }
    }

    void TryPress(Vector2 pos)
    {
        if (IsOverUi(pos)) return;      // tekan di atas UI (HUD atau tombol world): bukan urusan handler ini

        pressPos = pos;
        if (dpzView.Raycast(pos, WorldCam, out DpzHit hit))
        {
            if (hit.kind == DpzColliderKind.Block)
            {
                candidate = hit.blockIndex;
                state = State.PressedBlock;
                return;
            }
            if (hit.kind == DpzColliderKind.UpperBlock)
            {
                candidate = hit.blockIndex;
                state = State.PressedUpper;
                return;
            }
        }
        candidate = -1;
        state = State.PressedAway;
    }

    void BeginLift(Vector2 pos, FractionDragController drag)
    {
        if (!dpzView.TryGetBlockPiece(candidate, out Fraction piece)
            || !drag.TryBegin(new DragPayload(piece, DragOrigin.Dpz, candidate), pos, null))
        {
            state = State.Idle;
            candidate = -1;
            return;
        }

        dpzView.SetLiftedBlock(candidate);
        state = State.Dragging;
    }

    void FinishLift(Vector2 pos, bool cancel)
    {
        var drag = FractionDragController.Instance;
        if (drag != null)
        {
            if (cancel) drag.Cancel(); else drag.End(pos);
        }

        dpzView.SetLiftedBlock(-1);    // data sudah berubah kalau dihapus atau digabung; kalau batal, sel kembali
        state = State.Idle;
        candidate = -1;
    }

    // UI HUD (overlay) selalu menghalangi. UI world-space (panel aksi, tombol) hanya menghalangi kalau interaktif
    // (punya handler klik/tekan/drag): label dan latar panel diteruskan ke balok di bawahnya.
    static bool IsOverUi(Vector2 pos)
    {
        if (EventSystem.current == null) return false;

        var data = new PointerEventData(EventSystem.current) { position = pos };
        uiHits.Clear();
        EventSystem.current.RaycastAll(data, uiHits);

        for (int i = 0; i < uiHits.Count; i++)
        {
            GameObject go = uiHits[i].gameObject;
            Canvas canvas = go.GetComponentInParent<Canvas>();
            bool worldSpace = canvas != null && canvas.rootCanvas.renderMode == RenderMode.WorldSpace;
            if (!worldSpace) return true;

            if (ExecuteEvents.GetEventHandler<IPointerClickHandler>(go) != null
                || ExecuteEvents.GetEventHandler<IPointerDownHandler>(go) != null
                || ExecuteEvents.GetEventHandler<IDragHandler>(go) != null)
                return true;
        }
        return false;
    }
}