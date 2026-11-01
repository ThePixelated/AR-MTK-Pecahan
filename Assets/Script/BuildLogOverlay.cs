using System.Collections.Generic;
using UnityEngine;

public class BuildLogOverlay : MonoBehaviour
{
    [SerializeField] bool onlyErrors = true;
    [SerializeField] int maxLines = 40;

    static readonly List<string> buffer = new List<string>();
    static bool onlyErrorsStatic = true;

    bool open;
    Vector2 scroll;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Capture()
    {
        buffer.Clear();
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
    }

    static void OnLog(string message, string stack, LogType type)
    {
        bool error = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
        if (onlyErrorsStatic && !error) return;

        string[] lines = (stack ?? "").Split('\n');
        string shortStack = string.Join("\n", lines, 0, Mathf.Min(3, lines.Length));
        buffer.Add($"[{type}] {message}\n{shortStack}");
        while (buffer.Count > 60) buffer.RemoveAt(0);
    }

    void Awake() => onlyErrorsStatic = onlyErrors;

    void OnGUI()
    {
        float s = Mathf.Max(1f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float w = Screen.width / s, h = Screen.height / s;

        if (GUI.Button(new Rect(8, 8, 90, 44), open ? "TUTUP" : $"LOG ({buffer.Count})")) open = !open;
        if (!open) return;

        if (GUI.Button(new Rect(106, 8, 90, 44), "BERSIH")) buffer.Clear();

        GUI.Box(new Rect(8, 60, w - 16, h - 68), "");
        var view = new Rect(8, 60, w - 16, h - 68);
        var content = new Rect(0, 0, w - 40, Mathf.Max(h, buffer.Count * 110f));
        scroll = GUI.BeginScrollView(view, scroll, content);
        for (int i = buffer.Count - 1, y = 0; i >= 0; i--, y++)
            GUI.Label(new Rect(4, y * 110f, w - 48, 108f), buffer[i]);
        GUI.EndScrollView();
    }
}