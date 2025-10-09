using Unity.VisualScripting.YamlDotNet.Core;
using UnityEditor;
using UnityEngine;

namespace JelleKUL.Scanner
{
    [CustomEditor(typeof(CaptureObject))]
    public class CaptureObjectEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            // Reference to the target
            CaptureObject captureObject = (CaptureObject)target;

            // Draw default inspector (if you still want the normal fields shown)
            DrawDefaultInspector();

            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Isolation", EditorStyles.boldLabel);
                // Scan button
                if (GUILayout.Button("Isolate Points"))
                {
                    captureObject.IsolatePoints();
                }
            }

            // Save path field
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Save Path", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.TextField(captureObject.pointSavePath);

            if (GUILayout.Button("Browse", GUILayout.MaxWidth(80)))
            {
                string path = EditorUtility.SaveFilePanel(
                    "Select Save Location",
                    Application.dataPath,
                    captureObject.gameObject.name,
                    "txt" // change to your preferred extension
                );

                if (!string.IsNullOrEmpty(path))
                {
                    Undo.RecordObject(captureObject, "Set Save Path");
                    captureObject.pointSavePath = path;
                    EditorUtility.SetDirty(captureObject);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (Application.isPlaying)
            {
            EditorGUILayout.Space();
            // Scan button
            if (GUILayout.Button("Export"))
            {
                captureObject.ExportPointCloud();
            }
            }
        }
    }
}