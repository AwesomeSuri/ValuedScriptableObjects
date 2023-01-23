using UnityEngine;

namespace RhinocerosGamesProduction.ValuedScriptableObjects.Editor
{
    public class ValuedScriptableObjectsGeneratorSettings : ScriptableObject
    {
        public bool showHints = true;
        public string typeName;
        public string sourceNamespace;
        public string destinationFolder;
        public string targetNamespace;
    }
}
