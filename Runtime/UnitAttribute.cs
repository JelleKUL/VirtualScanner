using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RDFSharp.Model;

namespace JelleKUL.Scanner
{
    /// <summary>
    /// A Custom property to add to variables in a script, this enables them to be serialized to RDF and contain the Correct namespace
    /// </summary>
    [System.Serializable]
    public class UnitAttribute : PropertyAttribute
    {
        /// <summary>
        /// The unit to display
        /// </summary>
        public string unit;

        public UnitAttribute(string _unit)
        {
            unit = _unit;
        }
    }
}