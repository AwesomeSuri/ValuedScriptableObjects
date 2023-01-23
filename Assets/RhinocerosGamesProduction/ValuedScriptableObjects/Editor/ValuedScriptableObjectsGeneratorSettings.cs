using UnityEngine;

namespace RhinocerosGamesProduction.ValuedScriptableObjects.Editor
{
    public class ValuedScriptableObjectsGeneratorSettings : ScriptableObject
    {
        public bool showHints = true;
        public string valueType = string.Empty;
        public string sourceNamespace = string.Empty;
        public string destinationFolder = string.Empty;
        public string targetNamespace = string.Empty;
        public bool isObservable;
        public bool generateObserver;
    }
}
