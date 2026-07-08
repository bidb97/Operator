#if UNITY_EDITOR
using System.Linq;
using Operator.Drone.Unity;
using UnityEditor;
using UnityEngine;

namespace Operator.Drone.Editor
{
    [CustomEditor(typeof(DroneThrusterFx))]
    public class DroneThrusterFxEditor : UnityEditor.Editor
    {
        static readonly string[] VisibleFields =
        {
            "drone",
            "thrusterRenderer",
            "sourceFolder",
            "driveFps",
            "rotateFps",
            "idleFps",
            "fadeDuration",
            "driveScale",
            "rotateScale",
            "idleScale",
            "rotateSink",
            "idleSink",
            "visualSmooth",
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            foreach (var fieldName in VisibleFields)
            {
                var property = serializedObject.FindProperty(fieldName);
                if (property != null)
                {
                    EditorGUILayout.PropertyField(property);
                }
            }

            var sourceFolder = serializedObject.FindProperty("sourceFolder");
            var frames = serializedObject.FindProperty("frames");
            var loadedCount = frames?.arraySize ?? 0;

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(sourceFolder?.objectReferenceValue == null))
            {
                if (GUILayout.Button("Load Frames From Folder"))
                {
                    LoadFrames(sourceFolder, frames);
                }
            }

            if (sourceFolder?.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "Перетащи папку Fire/1 (или 2, 3) в Source Folder, затем нажми Load.",
                    MessageType.Info);
            }
            else if (loadedCount > 0)
            {
                EditorGUILayout.HelpBox($"Загружено кадров: {loadedCount}", MessageType.None);
            }

            serializedObject.ApplyModifiedProperties();
        }

        static void LoadFrames(SerializedProperty sourceFolder, SerializedProperty frames)
        {
            if (sourceFolder?.objectReferenceValue == null || frames == null)
            {
                return;
            }

            var folderPath = AssetDatabase.GetAssetPath(sourceFolder.objectReferenceValue);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogWarning("Source Folder must be a project folder.");
                return;
            }

            var sprites = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .SelectMany(path => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
                .Where(sprite => sprite != null)
                .Distinct()
                .OrderBy(SpriteOrderKey)
                .ToArray();

            if (sprites.Length == 0)
            {
                Debug.LogWarning($"No sprites found in {folderPath}");
                return;
            }

            Undo.RecordObject(frames.serializedObject.targetObject, "Load Thruster Frames");
            frames.arraySize = sprites.Length;
            for (var i = 0; i < sprites.Length; i++)
            {
                frames.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            }

            frames.serializedObject.ApplyModifiedProperties();
            Debug.Log($"Loaded {sprites.Length} sprites from {folderPath}");
        }

        static int SpriteOrderKey(Sprite sprite)
        {
            var name = sprite.name;
            var separator = name.LastIndexOf('_');
            if (separator >= 0 && int.TryParse(name.Substring(separator + 1), out var index))
            {
                return index;
            }

            return 0;
        }
    }
}
#endif
