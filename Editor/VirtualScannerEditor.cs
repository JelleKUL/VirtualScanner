using Unity.VisualScripting.YamlDotNet.Core;
using UnityEditor;
using UnityEngine;
/*
namespace JelleKUL.Scanner
{
    [CustomEditor(typeof(VirtualScanner))]
    public class VirtualScannerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            VirtualScanner scanner = (VirtualScanner)target;

            DrawDefaultInspector(); // uses proper layout for all PropertyDrawers

            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Scanning", EditorStyles.boldLabel);
                // Scan button
                if (GUILayout.Button("Scan"))
                {
                    scanner.ScanEnvironment();
                }
                EditorGUILayout.Space();
                // export button
                if (GUILayout.Button("Export"))
                {
                    scanner.ExportPointCloud(scanner.scannedPoints, scanner.pointSavePath);
                }
            }

        }
    }
}
*/