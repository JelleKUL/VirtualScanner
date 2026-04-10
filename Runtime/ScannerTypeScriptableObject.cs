using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JelleKUL.Scanner
{
    [CreateAssetMenu(fileName = "ScannerType", menuName = "ScriptableObjects/ScannerType", order = 1)]
    public class ScannerTypeScriptableObject : ScriptableObject
    {
        public string scannerType = "Default";
        [Tooltip("mm at 10M distance (default 12.5mm)")]
        [Unit("mm")]
        [Min(0)]
        public float scanDensity = 12.5f;
        [Tooltip("The max range of the scanner")]
        [Unit("m")]
        [Min(0f)]
        public float scanRange = 100;
        [Tooltip("The total degrees the vertical axis can cover")]
        [Unit("deg")]
        [Range(0f, 360f)]
        public float VerticalScanRange = 290;
        [Tooltip("The standard deviation for the artificial noise expressed in mm")]
        [Unit("mm")]
        [Min(0f)]
        public float systemNoise = 1;
        [Tooltip("The standard deviation for the artificial noise expressed in %")]
        [Unit("mm/10m")]
        [Min(0f)]
        public float distanceNoise = 1f;
    }
}