using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SimpleConfirmDialog : MonoBehaviour
{
    [SerializeField] GameObject root;      // objek popup yang di-SetActive
    [SerializeField] TMP_Text description;
    [SerializeField] Button yesButton;
    [SerializeField] Button noButton;

    Action onYes;

    void Awake()
    {
        yesButton.onClick.AddListener(() => { Action a = onYes; onYes = null; root.SetActive(false); a?.Invoke(); });
        noButton.onClick.AddListener(() => { onYes = null; root.SetActive(false); });
    }

    void OnEnable() { onYes = null; if (root) root.SetActive(false); }

    public void Ask(string message, Action yes)
    {
        onYes = yes;
        description.text = message;
        root.SetActive(true);
    }
}