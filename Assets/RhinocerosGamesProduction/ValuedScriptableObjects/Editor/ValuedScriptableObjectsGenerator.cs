using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RhinocerosGamesProduction.ValuedScriptableObjects.Editor
{
    public static class ValuedScriptableObjectsGenerator
    {
        [MenuItem("Tools/ValuedScriptableObject Generator")]
        private static void Generate()
        {
            ValuedScriptableObjectsGeneratorWindow.Init();
            var window = (ValuedScriptableObjectsGeneratorWindow)EditorWindow.GetWindow(
                typeof(ValuedScriptableObjectsGeneratorWindow), true, "Generate ValuedScriptableObject", true);
            window.Show();
        }
    }

    public class ValuedScriptableObjectsGeneratorWindow : EditorWindow
    {
        private const float SPACE = 20;
        private static ValuedScriptableObjectsGeneratorSettings settings;

        private Vector2 _scroll;
        private string _fileName = string.Empty;
        private string _observerFileName = string.Empty;

        public static void Init()
        {
            if (settings == null)
            {
                var path = GetCurrentFileName();
                path = path.Replace('\\', '/');
                path = path[..path.LastIndexOf('/')];
                path = $"{path}/ValuedScriptableObjectsGeneratorSettings.asset";
                if (File.Exists(path))
                {
                    settings = (ValuedScriptableObjectsGeneratorSettings)AssetDatabase
                        .LoadAssetAtPath(path[(Application.dataPath.Length - 6)..],
                            typeof(ValuedScriptableObjectsGeneratorSettings));
                }
                else
                {
                    var obj = CreateInstance<ValuedScriptableObjectsGeneratorSettings>();
                    settings = obj;
                    AssetDatabase.CreateAsset(obj, path[(Application.dataPath.Length - 6)..]);
                }
            }

            if (settings.valueType.Length == 0) settings.valueType = "int";
            if (settings.destinationFolder.Length == 0) settings.destinationFolder = Application.dataPath;
        }

        private static string GetCurrentFileName(
            [System.Runtime.CompilerServices.CallerFilePath]
            string fileName = null)
        {
            return fileName;
        }

        private void OnGUI()
        {
            GUILayout.BeginScrollView(_scroll);
            
            settings.showHints = EditorGUILayout.Toggle("show hints", settings.showHints);
            
            // additional
            GUILayout.BeginVertical("Features", "box");
            EditorGUILayout.Space(SPACE);
            if (settings.showHints)
                EditorGUILayout.HelpBox(
                    "An observable version of the scriptable object " +
                    "and the respective observer component can be generated.",
                    MessageType.Info);
            settings.isObservable = EditorGUILayout.Toggle("As observable", settings.isObservable);
            if (settings.isObservable)
                settings.generateObserver = EditorGUILayout.Toggle("Generate observer", settings.generateObserver);
            GUILayout.EndVertical();

            // source
            GUILayout.BeginVertical("Source", "box");
            EditorGUILayout.Space(SPACE);
            EditorGUILayout.LabelField("Value type", EditorStyles.boldLabel);
            if (settings.showHints)
                EditorGUILayout.HelpBox(
                    "Name of the class this scriptable object should store. Make sure that the class exists.",
                    MessageType.Info);
            settings.valueType = EditorGUILayout.TextField(settings.valueType);
            _fileName = $"{settings.valueType}Object.cs";
            _fileName = char.ToUpper(_fileName[0]) + _fileName[1..];
            if (settings.isObservable) _fileName = $"Observable{_fileName}";
            EditorGUI.indentLevel++;
            GUILayout.Label($"\tThe generated ScriptableObject script will be named as {_fileName}.");
            if (settings.isObservable && settings.generateObserver)
            {
                _observerFileName = $"{settings.valueType}Observer.cs";
                _observerFileName = char.ToUpper(_observerFileName[0]) + _observerFileName[1..];
                GUILayout.Label($"\tThe generated Observer script will be named as {_observerFileName}.");
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.LabelField("Namespace of stored type", EditorStyles.boldLabel);
            if (settings.showHints)
                EditorGUILayout.HelpBox(
                    "This makes sure the correct namespace is included.",
                    MessageType.Info);
            settings.sourceNamespace = EditorGUILayout.TextField(settings.sourceNamespace);
            GUILayout.EndVertical();

            // target
            GUILayout.BeginVertical("Target", "box");
            EditorGUILayout.Space(SPACE);
            EditorGUILayout.LabelField("Target path", EditorStyles.boldLabel);
            if (settings.showHints)
                EditorGUILayout.HelpBox(
                    "Where should the script be generated?",
                    MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            settings.destinationFolder = EditorGUILayout.TextField(settings.destinationFolder);
            if (GUILayout.Button("Open"))
            {
                settings.destinationFolder = EditorUtility.OpenFolderPanel(
                    "Destination folder for the generated ValuedScriptableObject", settings.destinationFolder,
                    string.Empty);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Target namespace", EditorStyles.boldLabel);
            if (settings.showHints)
                EditorGUILayout.HelpBox(
                    "Should the generated ValuedScriptableObject be inside of a namespace?",
                    MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            settings.targetNamespace = EditorGUILayout.TextField(settings.targetNamespace);
            if (GUILayout.Button("From path"))
            {
                settings.targetNamespace = GenerateNamespaceFromPath();
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.EndVertical();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel"))
            {
                Close();
            }

            if (GUILayout.Button("Generate"))
            {
                if (GenerateScript()) Close();
            }

            EditorGUILayout.EndHorizontal();
            
            GUILayout.EndScrollView();
        }

        private string GenerateNamespaceFromPath()
        {
            var newNamespace = new StringBuilder();

            settings.destinationFolder = settings.destinationFolder.Replace('\\', '/');
            var splits = settings.destinationFolder.Split('/');
            var assetsIndex = -1;
            for (int i = 0; i < splits.Length; i++)
            {
                if (splits[i].Equals("Assets"))
                {
                    assetsIndex = i;
                    break;
                }
            }

            if (assetsIndex >= 0)
            {
                for (int i = assetsIndex + 1; i < splits.Length; i++)
                {
                    if (newNamespace.Length > 0)
                    {
                        newNamespace.Append('.');
                    }

                    newNamespace.Append(splits[i].Replace(' ', '_'));
                }
            }

            return newNamespace.ToString();
        }

        private bool GenerateScript()
        {
            if (settings.valueType.Length == 0)
            {
                EditorUtility.DisplayDialog(string.Empty, "No value type has been specified.", "Ok");
                return false;
            }

            if (settings.destinationFolder.Length == 0)
            {
                EditorUtility.DisplayDialog(string.Empty, "Destination folder has not been specified.", "Ok");
                return false;
            }

            if (!Directory.Exists(settings.destinationFolder))
            {
                EditorUtility.DisplayDialog(string.Empty, "The given path does not exist.", "Ok");
                return false;
            }

            var filePath = $"{settings.destinationFolder}/{_fileName}";
            if (File.Exists(filePath))
            {
                var overwriteFile = EditorUtility.DisplayDialog(string.Empty,
                    "A file with the same name already exists in that path. Do you want to overwrite it?",
                    "Yes", "No");
                if (!overwriteFile) return false;
            }

            using (var outfile = new StreamWriter(filePath))
            {
                // using directories
                outfile.WriteLine("using UnityEngine;");
                if (settings.sourceNamespace.Length > 0  && !settings.targetNamespace.Equals(settings.sourceNamespace))
                    outfile.WriteLine($"using {settings.sourceNamespace};");
                outfile.WriteLine(string.Empty);

                // namespace
                var useNamespace = settings.targetNamespace.Length > 0 ? "\t" : string.Empty;
                if (useNamespace.Length > 0)
                {
                    outfile.WriteLine($"namespace {settings.targetNamespace}");
                    outfile.WriteLine("{");
                }

                // class start
                var className = _fileName.Split('.')[0];
                outfile.WriteLine($"{useNamespace}[CreateAssetMenu(" +
                                  $"fileName = \"{className}\", " +
                                  $"menuName = \"ValuedScriptableObjects/{settings.valueType}\")]");
                outfile.WriteLine($"{useNamespace}public class {className} : ScriptableObject");
                outfile.WriteLine($"{useNamespace}{{");

                // value
                outfile.WriteLine($"{useNamespace}\tpublic {settings.valueType} value;");

                // class end
                outfile.WriteLine($"{useNamespace}}}");
                if (useNamespace.Length > 0) outfile.WriteLine("}");
            }

            AssetDatabase.Refresh();
            return true;
        }
    }
}