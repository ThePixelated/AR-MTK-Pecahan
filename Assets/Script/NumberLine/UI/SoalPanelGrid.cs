using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Grid panel Soal Latihan (Level 2 dan 3): panel dibuat sebanyak input, dipakai ulang antar soal.
// Pasang di induk yang berisi ScrollRect; Content memakai GridLayoutGroup (kolom tetap) dan ContentSizeFitter vertikal.
public class SoalPanelGrid : MonoBehaviour
{
    [SerializeField] NumberLineSoalPanel panelPrefab;
    [SerializeField] RectTransform content;            // induk panel (GridLayoutGroup)
    [SerializeField] ScrollRect scroll;                // opsional: menggulir kalau baris melebihi viewport
    [SerializeField, Min(1)] int columns = 2;          // diterapkan ke GridLayoutGroup (maks 2 kolom)
    [SerializeField] string singlePanelTitle = "Jawaban";   // judul kalau hanya ada satu panel (Level 3)

    readonly List<NumberLineSoalPanel> panels = new List<NumberLineSoalPanel>();
    int activeCount;

    public int Count => activeCount;
    public event Action<NumberLineSoalPanel, NumberLineCell> CellTapped;

    public void Build(NumberLineSoalInput[] inputs)
    {
        if (panelPrefab == null || content == null)
        {
            Debug.LogError($"[{name}] SoalPanelGrid: Panel Prefab dan Content harus diisi.", this);
            return;
        }

        GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
        if (grid)
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
        }

        int n = inputs.Length;
        while (panels.Count < n)
        {
            NumberLineSoalPanel p = Instantiate(panelPrefab, content);
            p.CellTapped += OnCellTapped;
            panels.Add(p);
        }

        for (int i = 0; i < panels.Count; i++)
        {
            NumberLineSoalPanel p = panels[i];
            bool on = i < n;
            p.gameObject.SetActive(on);
            if (!on) { p.Unbind(); continue; }

            p.Bind(inputs[i]);
            p.SetIndex(i);
            p.SetTitle(n == 1 ? singlePanelTitle : "");       // kosong = "Panel A", "Panel B", ...
            p.SetIndicator(NumberLineIndicator.None);
            p.SetFocusedCell(null);

            int index = i;
            inputs[i].Changed += () => { if (index < panels.Count) panels[index].SetIndicator(NumberLineIndicator.None); };   // diedit: indikator lama tidak berlaku
        }

        activeCount = n;
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        if (scroll) scroll.verticalNormalizedPosition = 1f;
    }

    public void Clear()
    {
        foreach (NumberLineSoalPanel p in panels)
        {
            p.Unbind();
            p.gameObject.SetActive(false);
        }
        activeCount = 0;
    }

    public void ShowIndicators(NumberLineIndicator[] indicators)
    {
        if (indicators == null) return;
        for (int i = 0; i < activeCount && i < indicators.Length; i++) panels[i].SetIndicator(indicators[i]);
    }

    public void SetFocus(NumberLineSoalPanel panel, NumberLineCell? cell)
    {
        for (int i = 0; i < activeCount; i++) panels[i].SetFocusedCell(panels[i] == panel ? cell : null);
    }

    void OnCellTapped(NumberLineSoalPanel panel, NumberLineCell cell) => CellTapped?.Invoke(panel, cell);
}