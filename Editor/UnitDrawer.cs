using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

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
            Rect fieldRect = new Rect(position.x, position.y, position.width - 40, position.height);
            Rect unitRect = new Rect(position.x + position.width - 35, position.y, 35, position.height);

            EditorGUI.PropertyField(fieldRect, property, label);
            GUIStyle unitStyle = new GUIStyle(EditorStyles.label);
            unitStyle.alignment = TextAnchor.MiddleLeft;
            unitStyle.normal.textColor = Color.gray;
            EditorGUI.LabelField(unitRect, unitAttr.unit, unitStyle);
        }

    }
}