using UnityEngine;

namespace RhinocerosGamesProduction.ValuedScriptableObjects.Editor
{
    public class ValuedScriptableObjectsGeneratorSettings : ScriptableObject
    {
        public bool showHints = true;
        public string valueTypes = string.Empty;
        public string sourceNamespace = string.Empty;
        public bool isObservable;
        public bool generateObserver;
        public string editorFieldTypes = string.Empty;

        [HideInInspector] public string destinationFolder = string.Empty;
        [HideInInspector] public string targetNamespace = string.Empty;
    }
}
