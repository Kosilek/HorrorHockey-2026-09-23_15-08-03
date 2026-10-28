using UnityEditor;
using UnityEngine;
using UnityLife.Tools.Navigation;

namespace Customizations.Editor.Tools.Navigation
{
    [CustomEditor(typeof(FlatMapFootprint))]
    internal sealed class FlatMapFootprintEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "Кинь сюда объекты сцены. Export Json запишет их нижние вершины, только x и z, в Assets/_MobCraftOnline/_Data/FlatMap. Draw Map нарисует эту карту на y = 0.",
                MessageType.Info);

            var footprint = (FlatMapFootprint)target;
            bool hasSource = HasSource();
            SerializedProperty jsonFile = serializedObject.FindProperty("_jsonFile");
            bool hasJson = jsonFile != null && jsonFile.objectReferenceValue != null;

            EditorGUI.BeginDisabledGroup(!hasSource);
            if (GUILayout.Button("Export Json", GUILayout.Height(28f)))
                footprint.ExportJson();
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(!hasSource && !hasJson);
            if (GUILayout.Button("Draw Map", GUILayout.Height(28f)))
                footprint.DrawMap();
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Clear Map", GUILayout.Height(24f)))
                footprint.ClearMap();
        }

        private bool HasSource()
        {
            SerializedProperty sources = serializedObject.FindProperty("_sources");
            if (sources == null)
                return false;

            for (int i = 0; i < sources.arraySize; i++)
            {
                if (sources.GetArrayElementAtIndex(i).objectReferenceValue != null)
                    return true;
            }

            return false;
        }
    }
}
