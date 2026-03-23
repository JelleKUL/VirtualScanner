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


    public enum BrowseMode { File, Folder }

    public class BrowsePathAttribute : PropertyAttribute
    {
        public string Extension { get; }
        public BrowseMode Mode { get; }

        public BrowsePathAttribute(BrowseMode mode = BrowseMode.File, string extension = "txt")
        {
            Mode = mode;
            Extension = extension;
        }
    }

    public class ButtonBoolAttribute : PropertyAttribute
    {
        public string MethodName { get; private set; }

        public ButtonBoolAttribute(string methodName)
        {
            MethodName = methodName;
        }
    }

    public class ReadOnlyValueAttribute : PropertyAttribute
    {
    }
}