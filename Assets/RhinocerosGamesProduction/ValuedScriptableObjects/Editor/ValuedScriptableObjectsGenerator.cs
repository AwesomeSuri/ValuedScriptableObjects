using System.IO;
using System.Linq;
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

            if (settings.valueTypes.Length == 0) settings.valueTypes = "int";
            if (settings.destinationFolder.Length == 0)
                settings.destinationFolder =
                    $"{Application.dataPath}/RhinocerosGamesProduction/ValuedScriptableObjects";
            if (settings.targetNamespace.Length == 0)
                settings.targetNamespace = "RhinocerosGamesProduction.ValuedScriptableObjects";
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
                    "Name of the class this scriptable object should store. Make sure that the class exists. " +
                    "Generate multiple ValuedScriptableObjects at once by listing multiple classes seperated with a ','.",
                    MessageType.Info);
            settings.valueTypes = EditorGUILayout.TextField(settings.valueTypes);
            PreviewNames();

            EditorGUILayout.LabelField("Namespace of stored type", EditorStyles.boldLabel);
            if (settings.showHints)
                EditorGUILayout.HelpBox(
                    "This makes sure the correct namespace is included.",
                    MessageType.Info);
            settings.sourceNamespace = EditorGUILayout.TextField(settings.sourceNamespace);
            GUILayout.EndVertical();

            // observable settings
            if (settings.isObservable)
            {
                GUILayout.BeginVertical("Observable settings", "box");
                EditorGUILayout.Space(SPACE);
                EditorGUILayout.LabelField("Editor field type", EditorStyles.boldLabel);
                if (settings.showHints)
                    EditorGUILayout.HelpBox(
                        "Type of inputfield shown on the inspector of the scriptable object, e.g. Int, Vector2, Bool. " +
                        "In case of multiple generations, each must be specified seperated with a ','. " +
                        "For reference types use \"Reference\".",
                        MessageType.Info);
                settings.editorFieldTypes = EditorGUILayout.TextField(settings.editorFieldTypes);

                GUILayout.EndVertical();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel"))
            {
                Close();
            }

            if (GUILayout.Button("Generate"))
            {
                if (GenerateScripts()) Close();
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.EndScrollView();
        }

        private void PreviewNames()
        {
            EditorGUILayout.LabelField("Preview of the generated files:");

            settings.valueTypes = settings.valueTypes.Replace(" ", string.Empty);
            var types = settings.valueTypes.Split(',');
            foreach (var type in types)
            {
                if (type.Length == 0) continue;

                var fileName = $"{type}Object.cs";
                fileName = char.ToUpper(fileName[0]) + fileName[1..];
                if (settings.isObservable) fileName = $"Observable{fileName}";
                GUILayout.Label($"\t{fileName}");
                if (settings.isObservable && settings.generateObserver)
                {
                    fileName = $"{type}Observer.cs";
                    fileName = char.ToUpper(fileName[0]) + fileName[1..];
                    GUILayout.Label($"\t{fileName}");
                }
            }
        }

        private bool GenerateScripts()
        {
            if (settings.valueTypes.Length == 0)
            {
                EditorUtility.DisplayDialog(string.Empty, "No value type has been specified.", "Ok");
                return false;
            }

            settings.valueTypes = settings.valueTypes.Replace(" ", string.Empty);
            var types = settings.valueTypes.Split(',').Where(t => t.Length > 0).ToArray();

            if (settings.isObservable)
            {
                settings.editorFieldTypes = settings.editorFieldTypes.Replace(" ", string.Empty);
                var fieldTypes = settings.editorFieldTypes.Split(',').Where(t => t.Length > 0).ToArray();
                if (fieldTypes.Length != types.Length)
                {
                    EditorUtility.DisplayDialog(string.Empty,
                        "The amount of types is not the same as the amount of editor field types.", "Ok");
                    return false;
                }
                
                WriteFileObservableBase();

                for (var i = 0; i < types.Length; i++)
                {
                    var type = types[i];
                    var fieldType = fieldTypes[i];
                    if (type.Length == 0) continue;
                    if (!WriteFileObservable(type, fieldType)) return false;
                }
            }
            else if (types.Where(type => type.Length != 0).Any(type => !WriteFileSimple(type)))
            {
                return false;
            }

            AssetDatabase.Refresh();
            return true;
        }

        private bool WriteFileSimple(string type)
        {
            var fileName = $"{char.ToUpper(type[0])}{type[1..]}Object";
            var filePath = $"{settings.destinationFolder}/{fileName}.cs";
            if (File.Exists(filePath))
            {
                var overwriteFile = EditorUtility.DisplayDialog(string.Empty,
                    $"A file with the same name {fileName} already exists. Do you want to overwrite it?",
                    "Yes", "No");
                if (!overwriteFile) return false;
            }

            using var outfile = new StreamWriter(filePath);

            // using directories
            outfile.WriteLine("using UnityEngine;");
            if (settings.sourceNamespace.Length > 0 && !settings.targetNamespace.Equals(settings.sourceNamespace))
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
            var className = fileName.Split('.')[0];
            outfile.WriteLine($"{useNamespace}[CreateAssetMenu(" +
                              $"fileName = \"{className}\", " +
                              $"menuName = \"ValuedScriptableObjects/{type}\")]");
            outfile.WriteLine($"{useNamespace}public class {className} : ScriptableObject");
            outfile.WriteLine($"{useNamespace}{{");

            // value
            outfile.WriteLine($"{useNamespace}\tpublic {type} value;");

            // class end
            outfile.WriteLine($"{useNamespace}}}");
            if (useNamespace.Length > 0) outfile.WriteLine("}");

            return true;
        }

        private void WriteFileObservableBase()
        {
            var fileName = "ObservableObject";
            var filePath = $"{settings.destinationFolder}/{fileName}.cs";
            if (File.Exists(filePath)) return;

            using var outfile = new StreamWriter(filePath);

            // using directories
            outfile.WriteLine("using UnityEngine;");
            outfile.WriteLine("using UnityEngine.Events;");
            outfile.WriteLine(string.Empty);
            outfile.WriteLine("#if UNITY_EDITOR");
            outfile.WriteLine("using UnityEditor;");
            outfile.WriteLine("#endif");
            outfile.WriteLine(string.Empty);

            // namespace
            var useNamespace = settings.targetNamespace.Length > 0 ? "\t" : string.Empty;
            if (useNamespace.Length > 0)
            {
                outfile.WriteLine($"namespace {settings.targetNamespace}");
                outfile.WriteLine("{");
            }

            // interface
            outfile.WriteLine($"{useNamespace}public interface IEditorToObservableObject");
            outfile.WriteLine($"{useNamespace}{{");
            outfile.WriteLine($"{useNamespace}\tvoid ForceNotify();");
            outfile.WriteLine($"{useNamespace}}}");
            outfile.WriteLine(string.Empty);

            // class
            outfile.WriteLine($"{useNamespace}public abstract class ObservableObject<T> : " +
                              "ScriptableObject, IEditorToObservableObject");
            outfile.WriteLine($"{useNamespace}{{");
            // value
            outfile.WriteLine($"{useNamespace}\tprivate T value;");
            outfile.WriteLine($"{useNamespace}\tpublic T Value");
            outfile.WriteLine($"{useNamespace}\t{{");
            outfile.WriteLine($"{useNamespace}\t\tget => value;");
            outfile.WriteLine($"{useNamespace}\t\tset");
            outfile.WriteLine($"{useNamespace}\t\t{{");
            outfile.WriteLine($"{useNamespace}\t\t\tthis.value = value;");
            outfile.WriteLine($"{useNamespace}\t\t\t_event?.Invoke(value);");
            outfile.WriteLine($"{useNamespace}\t\t}}");
            outfile.WriteLine($"{useNamespace}\t}}");
            outfile.WriteLine(string.Empty);
            // event
            outfile.WriteLine($"{useNamespace}\tprivate readonly UnityEvent<T> _event = new();");
            outfile.WriteLine(string.Empty);
            // subscribe
            outfile.WriteLine($"{useNamespace}\tpublic void Subscribe(UnityAction<T> callback)");
            outfile.WriteLine($"{useNamespace}\t{{");
            outfile.WriteLine($"{useNamespace}\t\t_event.AddListener(callback);");
            outfile.WriteLine($"{useNamespace}\t}}");
            outfile.WriteLine(string.Empty);
            // unsubscribe
            outfile.WriteLine($"{useNamespace}\tpublic void Unsubscribe(UnityAction<T> callback)");
            outfile.WriteLine($"{useNamespace}\t{{");
            outfile.WriteLine($"{useNamespace}\t\t_event.RemoveListener(callback);");
            outfile.WriteLine($"{useNamespace}\t}}");
            outfile.WriteLine(string.Empty);
            // set value without notify
            outfile.WriteLine($"{useNamespace}\tpublic void SetValueWithoutNotify(T newValue)");
            outfile.WriteLine($"{useNamespace}\t{{");
            outfile.WriteLine($"{useNamespace}\t\tvalue = newValue;");
            outfile.WriteLine($"{useNamespace}\t}}");
            outfile.WriteLine(string.Empty);
            // force notify
            outfile.WriteLine($"{useNamespace}\tpublic void ForceNotify()");
            outfile.WriteLine($"{useNamespace}\t{{");
            outfile.WriteLine($"{useNamespace}\t\t_event?.Invoke(value);");
            outfile.WriteLine($"{useNamespace}\t}}");
            // class end
            outfile.WriteLine($"{useNamespace}}}");
            outfile.WriteLine(string.Empty);

            // editor
            outfile.WriteLine("#if UNITY_EDITOR");
            outfile.WriteLine($"{useNamespace}public abstract class ObservableObjectEditor : Editor");
            outfile.WriteLine($"{useNamespace}{{");
            outfile.WriteLine($"{useNamespace}\tpublic override void OnInspectorGUI()");
            outfile.WriteLine($"{useNamespace}\t{{");
            // gui
            outfile.WriteLine($"{useNamespace}\t\tbase.OnInspectorGUI();");
            outfile.WriteLine($"{useNamespace}\t\tShowValue();");
            outfile.WriteLine($"{useNamespace}\t\tif (GUILayout.Button(\"Force Notify\"))");
            outfile.WriteLine($"{useNamespace}\t\t{{");
            outfile.WriteLine($"{useNamespace}\t\t\tvar script = (IEditorToObservableObject)target;");
            outfile.WriteLine($"{useNamespace}\t\t\tscript.ForceNotify();");
            outfile.WriteLine($"{useNamespace}\t\t}}");
            outfile.WriteLine($"{useNamespace}\t}}");
            outfile.WriteLine(string.Empty);
            // show value
            outfile.WriteLine($"{useNamespace}\tprotected abstract void ShowValue();");
            outfile.WriteLine($"{useNamespace}}}");
            outfile.WriteLine("#endif");

            if (useNamespace.Length > 0) outfile.WriteLine("}");
        }

        private bool WriteFileObservable(string type, string fieldType)
        {
            var fileName = $"Observable{char.ToUpper(type[0])}{type[1..]}Object";
            var filePath = $"{settings.destinationFolder}/{fileName}.cs";
            if (File.Exists(filePath))
            {
                var overwriteFile = EditorUtility.DisplayDialog(string.Empty,
                    $"A file with the same name {fileName} already exists. Do you want to overwrite it?",
                    "Yes", "No");
                if (!overwriteFile) return false;
            }

            using var outfile = new StreamWriter(filePath);

            // using directories
            outfile.WriteLine("using UnityEngine;");
            if (settings.sourceNamespace.Length > 0 && !settings.targetNamespace.Equals(settings.sourceNamespace))
                outfile.WriteLine($"using {settings.sourceNamespace};");
            outfile.WriteLine(string.Empty);
            outfile.WriteLine("#if UNITY_EDITOR");
            outfile.WriteLine("using UnityEditor;");
            outfile.WriteLine("#endif");
            outfile.WriteLine(string.Empty);

            // namespace
            var useNamespace = settings.targetNamespace.Length > 0 ? "\t" : string.Empty;
            if (useNamespace.Length > 0)
            {
                outfile.WriteLine($"namespace {settings.targetNamespace}");
                outfile.WriteLine("{");
            }

            // class
            var className = fileName.Split('.')[0];
            outfile.WriteLine($"{useNamespace}[CreateAssetMenu(" +
                              $"fileName = \"{className}\", " +
                              $"menuName = \"ValuedScriptableObjects/Observable {type}\")]");
            outfile.WriteLine($"{useNamespace}public class {className} : ObservableObject<{type}>");
            outfile.WriteLine($"{useNamespace}{{");
            outfile.WriteLine($"{useNamespace}}}");
            outfile.WriteLine(string.Empty);

            // editor
            outfile.WriteLine("#if UNITY_EDITOR");
            outfile.WriteLine($"{useNamespace}[CustomEditor(typeof({className}))]");
            outfile.WriteLine($"{useNamespace}public class {className}Editor : ObservableObjectEditor");
            outfile.WriteLine($"{useNamespace}{{");
            outfile.WriteLine($"{useNamespace}\tprotected override void ShowValue()");
            outfile.WriteLine($"{useNamespace}\t{{");
            outfile.WriteLine($"{useNamespace}\t\tvar script = ({className})target;");
            if (fieldType.Equals("Reference"))
            {
                outfile.WriteLine($"{useNamespace}\t\tscript.Value = ({type})EditorGUILayout.ObjectField(" +
                                  $"\"Value\", script.Value, typeof({type}), false);");
            }
            else
            {
                outfile.WriteLine($"{useNamespace}\t\tscript.Value = EditorGUILayout.{fieldType}Field(" +
                                  "\"Value\", script.Value);");
            }

            outfile.WriteLine($"{useNamespace}\t}}");
            outfile.WriteLine($"{useNamespace}}}");
            outfile.WriteLine("#endif");

            if (useNamespace.Length > 0) outfile.WriteLine("}");

            return true;
        }

        private bool WriteFileObserver(string type, string fieldType)
        {
            var fileName = $"Observable{char.ToUpper(type[0])}{type[1..]}Object";
            var filePath = $"{settings.destinationFolder}/{fileName}.cs";
            if (File.Exists(filePath))
            {
                var overwriteFile = EditorUtility.DisplayDialog(string.Empty,
                    $"A file with the same name {fileName} already exists. Do you want to overwrite it?",
                    "Yes", "No");
                if (!overwriteFile) return false;
            }

            using var outfile = new StreamWriter(filePath);

            // using directories
            outfile.WriteLine("using UnityEngine;");
            if (settings.sourceNamespace.Length > 0 && !settings.targetNamespace.Equals(settings.sourceNamespace))
                outfile.WriteLine($"using {settings.sourceNamespace};");
            outfile.WriteLine(string.Empty);
            outfile.WriteLine("#if UNITY_EDITOR");
            outfile.WriteLine("using UnityEditor;");
            outfile.WriteLine("#endif");
            outfile.WriteLine(string.Empty);

            // namespace
            var useNamespace = settings.targetNamespace.Length > 0 ? "\t" : string.Empty;
            if (useNamespace.Length > 0)
            {
                outfile.WriteLine($"namespace {settings.targetNamespace}");
                outfile.WriteLine("{");
            }

            // class
            var className = fileName.Split('.')[0];
            outfile.WriteLine($"{useNamespace}[CreateAssetMenu(" +
                              $"fileName = \"{className}\", " +
                              $"menuName = \"ValuedScriptableObjects/Observable {type}\")]");
            outfile.WriteLine($"{useNamespace}public class {className} : ObservableObject<{type}>");
            outfile.WriteLine($"{useNamespace}{{");
            outfile.WriteLine($"{useNamespace}}}");
            outfile.WriteLine(string.Empty);

            // editor
            outfile.WriteLine("#if UNITY_EDITOR");
            outfile.WriteLine($"{useNamespace}[CustomEditor(typeof({className}))]");
            outfile.WriteLine($"{useNamespace}public class {className}Editor : ObservableObject<{type}>");
            outfile.WriteLine($"{useNamespace}{{");
            outfile.WriteLine($"{useNamespace}\tprotected override void ShowValue()");
            outfile.WriteLine($"{useNamespace}\t{{");
            outfile.WriteLine($"{useNamespace}\t\tvar script = ({className})target;");
            if (fieldType.Equals("Reference"))
            {
                outfile.WriteLine($"{useNamespace}\t\tscript.Value = ({type})EditorGUILayout.ObjectField(" +
                                  $"\"Value\", script.Value, typeof({type}), false);");
            }
            else
            {
                outfile.WriteLine($"{useNamespace}\t\tscript.Value = EditorGUILayout.{fieldType}Field(" +
                                  "\"Value\", script.Value);");
            }

            outfile.WriteLine($"{useNamespace}\t}}");
            outfile.WriteLine($"{useNamespace}}}");
            outfile.WriteLine("#endif");

            if (useNamespace.Length > 0) outfile.WriteLine("}");

            return true;
        }
    }
}