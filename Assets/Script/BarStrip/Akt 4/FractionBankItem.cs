using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FractionBankItem : MonoBehaviour,
    IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] TMP_Text label;
    [SerializeField] Image background;                 // Image di root kartu (warna = warna penyebut)
    [SerializeField] FractionBarView barView;          // bar proporsional + divider (opsional)
    [SerializeField] bool tintBackground = true;       // kartu pucat, bar pakai warna penuh
    [SerializeField, Range(0f, 1f)] float backgroundTint = 0.75f;   // 0 = warna penuh, 1 = putih
    [SerializeField, Min(1)] int denominator = 1;      // 1 = utuh. Jangan simpan Fraction di Inspector (default struct = 0/0)

    enum Mode { None, Scroll, Carry }
    Mode mode;
    ScrollRect parentScroll;

    public Fraction Piece => Fraction.Unit(denominator);

    public void Setup(int den, Activity4Controller owner)
    {
        Debug.Log(den);
        Debug.Log(owner);

        Debug.Log(denominator);
        denominator = Mathf.Max(1, den);
        Debug.Log(denominator);

        Debug.Log(label);
        label.text = FractionFormat.Label(Piece);
        Debug.Log(label);
        Debug.Log(label.text);

        Color c = owner.Palette.Get(denominator);            // warna data: sama dengan row Kotak dan balok 3D
        Debug.Log(c);

        if (barView) barView.Show(Piece, c);

        if (!background) return;
        if (!barView) background.color = c;                  // tanpa bar: kartu sendiri yang berwarna
        else if (tintBackground) background.color = Color.Lerp(c, Color.white, backgroundTint);
    }

    public void OnInitializePotentialDrag(PointerEventData e)
    {
        if (!parentScroll) parentScroll = GetComponentInParent<ScrollRect>();
        if (parentScroll) parentScroll.OnInitializePotentialDrag(e);   // hentikan inertia kalau bank lagi meluncur
    }

    public void OnBeginDrag(PointerEventData e)
    {
        Vector2 d = e.position - e.pressPosition;
        bool carry = Mathf.Abs(d.y) > Mathf.Abs(d.x);   // dominan vertikal = ambil kartu, dominan horizontal = scroll bank

        if (!carry)
        {
            mode = Mode.Scroll;
            if (parentScroll) parentScroll.OnBeginDrag(e);
            return;
        }

        var drag = FractionDragController.Instance;
        bool started = drag != null && drag.TryBegin(DragPayload.FromBank(Piece), e.position, e.pressEventCamera);
        mode = started ? Mode.Carry : Mode.None;
    }

    public void OnDrag(PointerEventData e)
    {
        if (mode == Mode.Scroll) { if (parentScroll) parentScroll.OnDrag(e); }
        else if (mode == Mode.Carry) FractionDragController.Instance?.Move(e.position);
    }

    public void OnEndDrag(PointerEventData e)
    {
        Mode m = mode;
        mode = Mode.None;   // reset dulu: kalau End() melempar exception, state kartu tetap bersih

        if (m == Mode.Scroll) { if (parentScroll) parentScroll.OnEndDrag(e); }
        else if (m == Mode.Carry) FractionDragController.Instance?.End(e.position);
    }

    void OnDisable()
    {
        if (mode == Mode.Carry) FractionDragController.Instance?.Cancel();
        mode = Mode.None;
    }
}