using UnityEngine;

// Jembatan status marker ke dpz. Marker hilang: dpz tidak bisa disentuh, drag yang berjalan dibatalkan dengan pesan.
// Nanti di AR: Image Target (DefaultObserverEventHandler) On Target Found -> NotifyFound, On Target Lost -> NotifyLost.
public class DpzTrackingGate : MonoBehaviour
{
    [SerializeField] DpzView dpzView;
    [SerializeField] string lostMessage = "Marker hilang. Arahkan kamera ke marker lagi.";
    [SerializeField] bool simulateLost;          // uji di editor tanpa marker: centang saat Play

    bool markerTracked = true;
    bool applied = true;

    public bool IsTracked => markerTracked && !simulateLost;

    void OnEnable()
    {
        if (dpzView == null)
        {
            Debug.LogError($"[{name}] DpzTrackingGate: field 'Dpz View' kosong", this);
            return;
        }
        applied = true;
        Apply();
    }

    public void NotifyFound() { markerTracked = true; Apply(); }
    public void NotifyLost() { markerTracked = false; Apply(); }

    void Update()
    {
        if (IsTracked != applied) Apply();       // menangkap centang simulateLost
    }

    void Apply()
    {
        if (dpzView == null) return;

        bool tracked = IsTracked;
        if (tracked == applied) return;
        applied = tracked;

        dpzView.SetTracked(tracked);
        if (tracked) return;

        var drag = FractionDragController.Instance;
        if (drag != null && drag.IsDragging)
        {
            drag.Cancel();                                      // ghost hilang, balok yang diangkat kembali
            Feedback.Show(lostMessage, FeedbackKind.Warning);
        }
    }
}