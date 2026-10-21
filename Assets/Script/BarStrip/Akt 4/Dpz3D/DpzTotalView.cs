using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Teks "Total = ..." di dunia: isian kosong "..." sampai ada balok, lalu nilai total (disederhanakan, campuran).
// Tombol toggle menyembunyikan atau menampilkan teks Total.
public class DpzTotalView : MonoBehaviour
{
    [SerializeField] Activity4Controller controller;
    [SerializeField] TMP_Text totalLabel;
    [SerializeField] Button toggleButton;
    [SerializeField] TMP_Text buttonText;          // opsional: teks di dalam tombol
    [SerializeField] GameObject iconVisible;       // opsional: tampil saat Total terlihat
    [SerializeField] GameObject iconHidden;        // opsional: tampil saat Total disembunyikan
    [SerializeField] bool startVisible = true;
    [SerializeField] string prefix = "Total = ";
    [SerializeField] string placeholder = "Total = ...";
    [SerializeField] string textWhenVisible = "Sembunyikan";
    [SerializeField] string textWhenHidden = "Tampilkan";

    FractionSession session;
    bool bound;
    bool visible;

    void OnEnable()
    {
        if (bound) return;
        if (controller == null || totalLabel == null || toggleButton == null)
        {
            Debug.LogError($"[{name}] DpzTotalView: 'Controller', 'Total Label', dan 'Toggle Button' harus diisi", this);
            return;
        }

        session = controller.Session;
        session.Dpz.Changed += Refresh;
        session.StateChanged += Refresh;
        toggleButton.onClick.AddListener(OnToggle);
        bound = true;

        visible = startVisible;
        Refresh();
    }

    void OnDisable()
    {
        if (!bound) return;
        session.Dpz.Changed -= Refresh;
        session.StateChanged -= Refresh;
        toggleButton.onClick.RemoveListener(OnToggle);
        bound = false;
    }

    void OnToggle()
    {
        visible = !visible;
        Refresh();
    }

    void Refresh()
    {
        bool hasTotal = session.CurrentPhase != Phase.Menyusun && !session.Dpz.IsEmpty;
        totalLabel.text = hasTotal ? prefix + ReducedMixed(session.Dpz.Total()) : placeholder;
        totalLabel.gameObject.SetActive(visible);

        if (buttonText) buttonText.text = visible ? textWhenVisible : textWhenHidden;
        if (iconVisible) iconVisible.SetActive(visible);
        if (iconHidden) iconHidden.SetActive(!visible);
    }

    // 10/4 -> "2 1/2", 6/3 -> "2", 1/2 -> "1/2"
    static string ReducedMixed(Fraction f)
    {
        long n = f.numerator, d = f.denominator;
        if (d == 0) return "0";

        long g = Gcd(n, d);
        if (g > 1) { n /= g; d /= g; }

        long whole = n / d, rem = n % d;
        if (rem == 0) return whole.ToString();
        return whole == 0 ? $"{rem}/{d}" : $"{whole} {rem}/{d}";
    }

    static long Gcd(long a, long b)
    {
        a = System.Math.Abs(a); b = System.Math.Abs(b);
        while (b != 0) { long t = a % b; a = b; b = t; }
        return a == 0 ? 1 : a;
    }
}