using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class HistoryManager : MonoBehaviour
{
    public static HistoryManager instance;

    [Header("Debug")]
    [SerializeField] private TextMeshProUGUI indexTxt;
    [SerializeField] private TextMeshProUGUI liquidADebugTxt;
    [SerializeField] private TextMeshProUGUI liquidBDebugTxt;

    [Header("Component")]
    [SerializeField] private Transform parentContent;
    [SerializeField] private GameObject historyBarPrefab;
    [SerializeField] private GameObject currentHistoryBar;
    [SerializeField] private List<GameObject> listHistoryBar = new List<GameObject>();

    private void Awake()
    {
        instance = this;
    }

    private void OnEnable()
    {
        onBarPressed += DebugPanel;
    }

    private void OnDisable()
    {
        onBarPressed -= DebugPanel;
    }

    public Action<HistoryBar> onBarPressed;
    public void BarButtonPressed(HistoryBar HB)
    {
        onBarPressed?.Invoke(HB);
    }

    public void DebugPanel(HistoryBar HB)
    {
        indexTxt.text = HB.indexUrut.ToString();
        liquidADebugTxt.text = HB.liquidAVal.ToString();
        liquidBDebugTxt.text = HB.liquidBVal.ToString();

        currentHistoryBar = HB.gameObject;
    }

    public void AddHistoryBar()
    {
        int currentIndex = listHistoryBar.Count + 1;
        int maxLiquidVal = 100;

        GameObject historyBar = Instantiate(historyBarPrefab, parentContent);
        historyBar.name = "History Bar - " + currentIndex;

        // Setup
        HistoryBar hb = historyBar.GetComponent<HistoryBar>();
        hb.indexUrut = currentIndex;
        hb.liquidAVal = UnityEngine.Random.Range(0, maxLiquidVal);
        hb.liquidBVal = UnityEngine.Random.Range(0, maxLiquidVal);
        hb.indexTxt.text = currentIndex.ToString();
        hb.liquidATxt.text = hb.liquidAVal.ToString();
        hb.liquidBTxt.text = hb.liquidBVal.ToString();

        hb.ButtonPressed();

        currentHistoryBar = historyBar;
        listHistoryBar.Add(historyBar);
    }

    public void RemoveAll()
    {
        indexTxt.text = "..";
        liquidADebugTxt.text = "..";
        liquidBDebugTxt.text = "..";
        currentHistoryBar = null;

        foreach (var item in listHistoryBar)
        {
            Destroy(item);
        }

        listHistoryBar.Clear();
        listHistoryBar = new List<GameObject>();

        
    }
}
