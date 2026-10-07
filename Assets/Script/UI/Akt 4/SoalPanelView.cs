using TMPro;
using UnityEngine;

public class SoalPanelView : MonoBehaviour
{
    [SerializeField] Activity4Controller controller;
    [SerializeField] MixedFractionView slotA;
    [SerializeField] TMP_Text operatorText;
    [SerializeField] MixedFractionView slotB;
    [SerializeField] TMP_Text equalsText;
    [SerializeField] MixedFractionView slotResult;

    FractionSession session;
    bool bound;

    void OnEnable() => Bind();
    void OnDisable() => Unbind();

    void Bind()
    {
        if (bound) return;
        if (controller == null) { Debug.LogError($"[{name}] SoalPanelView: controller belum diisi", this); return; }

        session = controller.Session;
        session.StateChanged += Refresh;
        bound = true;
        Refresh();   // sinkron ulang dari data tiap kali aktif
    }

    void Unbind()
    {
        if (!bound) return;
        session.StateChanged -= Refresh;
        bound = false;
    }

    void Refresh()
    {
        if (session.CurrentPhase == Phase.Menyusun)
        {
            slotA.Clear(); slotB.Clear(); slotResult.Clear();
            operatorText.text = "";
            equalsText.text = "";
            return;
        }

        Problem p = session.Current;
        slotA.Show(p.a);
        slotB.Show(p.b);
        operatorText.text = p.op == Operation.Add ? "+" : "-";
        equalsText.text = "=";

        // Hasil hanya kelihatan saat Benar; ubah dpz setelahnya = fase balik ke Merakit = "?" lagi
        bool solved = session.CurrentPhase == Phase.Dicek && session.LastVerdict == CheckVerdict.Benar;
        if (solved) slotResult.Show(p.result); else slotResult.ShowUnknown();
    }
}