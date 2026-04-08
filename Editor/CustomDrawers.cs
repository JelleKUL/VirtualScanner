using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;

namespace JelleKUL.Scanner
{
    /// <summary>
    /// A Custom GUI drawer for `UnitAttribute`, displaying the prefix of the resource
    /// </summary>
    [CustomPropertyDrawer(typeof(UnitAttribute))]
    public class UnitDrawer : PropertyDrawer
    {
        /// <summary>
        /// Override the default drawer
        /// </summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            UnitAttribute unitAttr = (UnitAttribute)attribute;
            int unitWidth = 8 * unitAttr.unit.Length + 7; // Adjust width based on unit text length;
            Rect fieldRect = new Rect(position.x, position.y, position.width - unitWidth - 5, position.height);
            Rect unitRect = new Rect(position.x + position.width - unitWidth, position.y, unitWidth, position.height);

            EditorGUI.PropertyField(fieldRect, property, label);
            GUIStyle unitStyle = new GUIStyle(EditorStyles.label);
            unitStyle.alignment = TextAnchor.MiddleRight;
            unitStyle.normal.textColor = Color.gray;
            EditorGUI.LabelField(unitRect, unitAttr.unit, unitStyle);
        }

    }

    [CustomPropertyDrawer(typeof(BrowsePathAttribute))]
    public class BrowsePathDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Use layout system for compatibility with DrawDefaultInspector
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(label);

            // Text field (layout version)
            property.stringValue = EditorGUILayout.TextField(property.stringValue);

            if (GUILayout.Button("Browse", GUILayout.MaxWidth(80)))
            {
                string result = "";
                var attr = (BrowsePathAttribute)attribute;

                if (attr.Mode == BrowseMode.File)
                {
                    result = EditorUtility.SaveFilePanel(
                        "Select Save Location",
                        Application.dataPath,
                        "NewFile",
                        attr.Extension
                    );
                }
                else
                {
                    result = EditorUtility.OpenFolderPanel(
                        "Select Folder",
                        Application.dataPath,
                        string.Empty
                    );
                }

                if (!string.IsNullOrEmpty(result))
                    property.stringValue = result;
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    [CustomPropertyDrawer(typeof(ButtonBoolAttribute))]
    public class ButtonBoolDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            ButtonBoolAttribute buttonAttribute = (ButtonBoolAttribute)attribute;
            Object targetObject = property.serializedObject.targetObject;

            // Disable button when not playing
            EditorGUI.BeginDisabledGroup(!Application.isPlaying);

            if (GUI.Button(position, label.text))
            {
                MethodInfo method = targetObject.GetType().GetMethod(
                    buttonAttribute.MethodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                );

                if (method != null)
                {
                    method.Invoke(targetObject, null);
                }
                else
                {
                    Debug.LogWarning($"Method '{buttonAttribute.MethodName}' not found on {targetObject.name}");
                }
            }

            EditorGUI.EndDisabledGroup();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }
    }
    
    [CustomPropertyDrawer(typeof(ReadOnlyValueAttribute))]
    public class ReadOnlyValueDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Disable GUI controls (makes field read-only)
            GUI.enabled = false;

            EditorGUI.PropertyField(position, property, label, true);

            // Re-enable GUI controls so other fields aren’t affected
            GUI.enabled = true;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }
}