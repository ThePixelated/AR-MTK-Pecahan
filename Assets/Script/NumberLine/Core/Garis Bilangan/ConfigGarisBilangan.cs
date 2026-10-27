using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class ConfigGarisBilangan : MonoBehaviour
{
    [Header("Slider Config")]
    [SerializeField] private GameObject scrollBarTrans;
    [SerializeField] private Scrollbar scrollbar;
    [SerializeField] private int wholeNum;
    [SerializeField] private int fractNum;
    [SerializeField] private int totalSteps = 0;
    [SerializeField] private float valueStep = 0;

    public int TotalStep { get { return totalSteps; } private set { totalSteps = value; } }
    public float ValueStep { get { return valueStep; } private set { valueStep = value; } }

    public Scrollbar ScrollBar { get { return scrollbar; } }

    private List<GameObject> wholeLines = new List<GameObject>();
    private List<GameObject> fractionLines = new List<GameObject>();
    //private List<GameObject> wholeNumTxt = new List<GameObject>();


    [Header("Style")]
    [SerializeField] private Color lineColor;
    [SerializeField] private RectTransform lineParent;
    [SerializeField] private GameObject wholeNumSlitPrefab;
    [SerializeField] private GameObject fractNumSlitPrefab;

    private void OnEnable()
    {
        Media25Manager.Instance.onPanjangGarisPressed += ValPanjangGarisChange;
        Media25Manager.Instance.PartialUIStepper.Changed += ValPartialSatuan;
    }

    private void OnDisable()
    {
        Media25Manager.Instance.onPanjangGarisPressed -= ValPanjangGarisChange;
        Media25Manager.Instance.PartialUIStepper.Changed -= ValPartialSatuan;

    }

    private void Awake()
    {
        scrollbar = scrollbar ? scrollbar : scrollBarTrans.GetComponent<Scrollbar>();

        Image imageLine = scrollBarTrans.GetComponent<Image>();
        imageLine.color = lineColor;

        ScrollBar.value = .5f;
        ScrollBar.size = 0f;
        ScrollBar.numberOfSteps = TotalStep;

        SetValueLineScrollBar();
    }

    private void ValPanjangGarisChange(int index)
    {
        wholeNum = index;
        SetValueLineScrollBar();
    }

    private void ValPartialSatuan(int index)
    {
        fractNum = index;
        SetValueLineScrollBar();
    }

    [ContextMenu("Set Value Config")]
    private void SetValueLineScrollBar()
    {
        if (wholeNum <= 0 || fractNum <= 0)
        {
            Debug.LogWarning("wholeNum dan fractNum harus lebih dari 0.");
            return;
        }

        // Jumlah interval terkecil pada garis bilangan.
        TotalStep = wholeNum * fractNum;

        // Jarak satu langkah pada normalized value 0..1.
        ValueStep = 1f / TotalStep;

        // Ada TotalStep interval, tetapi TotalStep + 1 titik.
        if (scrollbar != null)
            scrollbar.numberOfSteps = TotalStep + 1;

        SetVisualLineGraph();
    }

    private void ClearGeneratedLines(List<GameObject> lines)
    {
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            GameObject item = lines[i];

            if (item == null)
                continue;

            if (Application.isPlaying)
                Destroy(item);
            else
                DestroyImmediate(item);
        }

        lines.Clear();
    }

    private void SetVisualLineGraph()
    {
        ClearGeneratedLines(wholeLines);
        ClearGeneratedLines(fractionLines);

        if (lineParent == null || wholeNumSlitPrefab == null || fractNumSlitPrefab == null || wholeNum <= 0 || fractNum <= 0)
            return;
        

        // Pastikan ukuran RectTransform sudah diperbarui.
        Canvas.ForceUpdateCanvases();

        int totalIntervals = wholeNum * fractNum;
        float width = lineParent.rect.width;

        if (width <= 0f)
        {
            Debug.LogWarning("Width lineParent belum valid.", this);
            return;
        }

        for (int step = 0; step <= totalIntervals; step++)
        {
            // Setiap kelipatan fractNum adalah garis bilangan bulat.
            bool isWholeTick = step % fractNum == 0;

            GameObject prefab = isWholeTick ? wholeNumSlitPrefab : fractNumSlitPrefab;

            List<GameObject> targetList = isWholeTick ? wholeLines : fractionLines;

            int wholeIndex = step / fractNum;
            int numerator = step % fractNum;

            // Semua prefab memakai rumus posisi yang sama.
            float normalizedPosition = step / (float)totalIntervals;
            float x = normalizedPosition * width;

            GameObject tick = Instantiate(prefab, lineParent, false);
            RectTransform tickRect = tick.GetComponent<RectTransform>();

            // Prefab harus memakai anchor Left-Center dan pivot Center.
            tickRect.anchoredPosition = new Vector2(x, 0f);

            Image tickImage = tick.GetComponent<Image>();

            if (tickImage != null)
                tickImage.color = lineColor;

            if (isWholeTick)
            {
                tick.name = $"WholeLine ({wholeIndex})";

                TextMeshProUGUI label = tick.GetComponentInChildren<TextMeshProUGUI>(true);

                if (label != null)
                {
                    label.text = wholeIndex.ToString();
                    label.color = lineColor;
                }
            }
            else
            {
                tick.name = $"FractionLine ({wholeIndex}:{numerator})";
            }

            targetList.Add(tick);
        }

        Image baseLine = scrollBarTrans != null ? scrollBarTrans.GetComponent<Image>() : null;

        if (baseLine != null)
            baseLine.color = lineColor;
    }
}
