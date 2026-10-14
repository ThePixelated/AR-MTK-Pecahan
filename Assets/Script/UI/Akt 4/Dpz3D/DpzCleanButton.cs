using UnityEngine;
using UnityEngine.UI;

// Tombol di canvas world space dekat dpz: kosongkan dpz, dengan konfirmasi kalau tidak kosong
public class DpzCleanButton : MonoBehaviour
{
    [SerializeField] Activity4Controller controller;
    [SerializeField] Button button;

    const string Warning = "Hapus semua balok di area susun?";

    FractionSession session;
    bool bound;

    void OnEnable()
    {
        if (bound) return;
        if (controller == null || button == null) { Debug.LogError($"[{name}] DpzCleanButton: controller/button belum diisi", this); return; }

        session = controller.Session;
        session.Dpz.Changed += Refresh;
        button.onClick.AddListener(OnClick);
        bound = true;
        Refresh();
    }

    void OnDisable()
    {
        if (!bound) return;
        session.Dpz.Changed -= Refresh;
        button.onClick.RemoveListener(OnClick);
        bound = false;
    }

    void Refresh() => button.interactable = !session.Dpz.IsEmpty;

    void OnClick() => controller.ConfirmOrRun(!session.Dpz.IsEmpty, Warning, session.ClearDpz);
}