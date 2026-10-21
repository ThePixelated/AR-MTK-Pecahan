using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LiquidVessel))]
public class LiquidVesselEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var vessel = (LiquidVessel)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Alat kalibrasi", EditorStyles.boldLabel);

        if (GUILayout.Button("Auto-fit rongga dari mesh"))
        {
            Undo.RecordObject(vessel, "Auto-fit rongga");
            string report = VesselAutoFit.Fit(vessel);
            EditorUtility.SetDirty(vessel);
            Debug.Log(report, vessel);
        }

        if (GUILayout.Button("Pusatkan pivot di dasar rongga"))
        {
            string report = VesselAutoFit.CenterPivot(vessel);
            EditorUtility.SetDirty(vessel);
            Debug.Log(report, vessel);
        }

        if (Application.isPlaying && GUILayout.Button("Bangun ulang cairan")) vessel.RebuildLiquid();
    }
}