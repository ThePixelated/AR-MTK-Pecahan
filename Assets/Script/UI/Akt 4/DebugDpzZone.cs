using System.Text;
using TMPro;
using UnityEngine;

// DEBUG: dpz 2D sementara supaya aturan dpz bisa dites sebelum dpz 3D (step 5b) ada.
public class DebugDpzZone : MonoBehaviour, IFractionDropZone
{
    [SerializeField] Activity4Controller controller;
    [SerializeField] RectTransform zoneRect;
    [SerializeField] TMP_Text listText;

    [SerializeField] DpzView dpzView;   // opsional: untuk melihat highlight di 3D saat ghost melewati zona debug

    FractionSession session;
    bool bound;
    int hoverTarget = -1;   // blok yang akan menerima potongan (hanya untuk tampilan)

    void OnEnable() { DropZoneRegistry.Register(this); Bind(); }
    void OnDisable() { DropZoneRegistry.Unregister(this); Unbind(); hoverTarget = -1; }

    void Bind()
    {
        if (bound) return;
        if (controller == null) { Debug.LogError($"[{name}] DebugDpzZone: controller belum diisi", this); return; }

        session = controller.Session;
        session.Dpz.Changed += Refresh;
        session.StateChanged += Refresh;
        bound = true;
        Refresh();
    }

    void Unbind()
    {
        if (!bound) return;
        session.Dpz.Changed -= Refresh;
        session.StateChanged -= Refresh;
        bound = false;
    }

    void Refresh()
    {
        var sb = new StringBuilder();
        sb.Append("DPZ (debug) | fase: ").Append(session.CurrentPhase)
          .Append(" | satuan: ").Append(GameRules.IsUnitUniform(session.Dpz) ? "seragam" : "campur").Append('\n');

        for (int i = 0; i < session.Dpz.Count; i++)
        {
            string label = FractionFormat.Label(session.Dpz.Pieces[i]);
            sb.Append(i == hoverTarget ? $"[{label}]" : label).Append("   ");   // [x] = blok yang akan menerima drop
        }
        sb.Append("\ntotal: ").Append(session.Dpz.Total().ToMixedString());
        listText.text = sb.ToString();
    }

    public void OnClearPressed() { if (bound) session.ClearDpz(); }
    public void OnRemoveLastPressed() { if (bound && !session.Dpz.IsEmpty) session.RemoveFromDpz(session.Dpz.Count - 1); }
    public void OnRemoveUnitLastPressed() { if (bound && !session.Dpz.IsEmpty) session.RemoveUnitFromDpz(session.Dpz.Count - 1); }

    // ---------- IFractionDropZone ----------
    public bool Contains(Vector2 p, Camera cam) => RectTransformUtility.RectangleContainsScreenPoint(zoneRect, p, cam);

    public void Hover(in DragPayload payload, Vector2 p, Camera cam, float dt)
    {
        if (!bound) return;
        Fraction piece = payload.piece;

        int target = session.FindDpzMergeTarget(piece);
        if (target != hoverTarget) { hoverTarget = target; Refresh(); }

        if (dpzView) dpzView.SetHover(piece);   // blok sejenis: highlight blok; selain itu preview slot baru
    }

    public void HoverExit()
    {
        if (dpzView) dpzView.ClearHover();
        if (hoverTarget == -1) return;
        hoverTarget = -1;
        if (bound) Refresh();
    }

    public RejectReason Accept(in DragPayload payload, Vector2 p, Camera cam)
    {
        if (payload.origin != DragOrigin.Bank) return RejectReason.None;
        hoverTarget = -1;
        if (dpzView) dpzView.ClearHover();
        return session.TryDropToDpz(payload.piece);
    }
}