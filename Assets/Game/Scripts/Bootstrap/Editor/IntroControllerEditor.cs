#if UNITY_EDITOR
using System.Linq;
using Operator.Bootstrap;
using UnityEditor;
using UnityEngine;

namespace Operator.Bootstrap.Editor
{
    [CustomEditor(typeof(IntroController))]
    public class IntroControllerEditor : UnityEditor.Editor
    {
        static readonly string[] VisibleFields =
        {
            "slideImage",
            "sourceFolder",
            "gameSceneName",
            "startInterval",
            "minInterval",
            "intervalDecay",
            "startScale",
            "endScale",
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            foreach (var fieldName in VisibleFields)
            {
                var property = serializedObject.FindProperty(fieldName);
                if (property != null)
                    EditorGUILayout.PropertyField(property);
            }

            var sourceFolder = serializedObject.FindProperty("sourceFolder");
            var slides = serializedObject.FindProperty("slides");
            var loadedCount = slides?.arraySize ?? 0;

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(sourceFolder?.objectReferenceValue == null))
            {
                if (GUILayout.Button("Load Slides From Folder"))
                    LoadSlides(sourceFolder, slides);
            }

            if (sourceFolder?.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "Перетащи папку со слайдами в Source Folder, затем нажми Load.",
                    MessageType.Info);
            }
            else if (loadedCount > 0)
            {
                EditorGUILayout.HelpBox($"Загружено слайдов: {loadedCount}", MessageType.None);
            }

            serializedObject.ApplyModifiedProperties();
        }

        static void LoadSlides(SerializedProperty sourceFolder, SerializedProperty slides)
        {
            if (sourceFolder?.objectReferenceValue == null || slides == null)
                return;

            var folderPath = AssetDatabase.GetAssetPath(sourceFolder.objectReferenceValue);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogWarning("Source Folder must be a project folder.");
                return;
            }

            var loaded = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .SelectMany(path => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
                .Where(sprite => sprite != null)
                .Distinct()
                .OrderBy(sprite =>
                {
                    var name = sprite.name;
                    var digitIndex = 0;
                    while (digitIndex < name.Length && !char.IsDigit(name[digitIndex]))
                        digitIndex++;

                    if (digitIndex >= name.Length)
                        return 0;

                    var end = digitIndex;
                    while (end < name.Length && char.IsDigit(name[end]))
                        end++;

                    return int.TryParse(name.Substring(digitIndex, end - digitIndex), out var index) ? index : 0;
                })
                .ToArray();

            if (loaded.Length == 0)
            {
                Debug.LogWarning($"No sprites found in {folderPath}");
                return;
            }

            Undo.RecordObject(slides.serializedObject.targetObject, "Load Intro Slides");
            slides.arraySize = loaded.Length;
            for (var i = 0; i < loaded.Length; i++)
                slides.GetArrayElementAtIndex(i).objectReferenceValue = loaded[i];

            slides.serializedObject.ApplyModifiedProperties();
            Debug.Log($"Loaded {loaded.Length} slides from {folderPath}");
        }
    }
}
#endif
