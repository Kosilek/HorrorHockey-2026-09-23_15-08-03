#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace _FM.Editor.SymbolsForGroup
{
    public class AddSymbolsForGroupEditorWindow : EditorWindow
    {
        static readonly string[] Presets = { "DEV_CONSOLE", "DEBUG", "DEVELOPMENT_BUILD", "UNITY_EDITOR" };
        const string CustomLabel = "— Custom —";

        string _symbolInput = "";
        int _presetIndex;

        void OnGUI()
        {
            string[] options = new string[Presets.Length + 1];
            options[0] = CustomLabel;
            for (int i = 0; i < Presets.Length; i++)
                options[i + 1] = Presets[i];

            EditorGUI.BeginChangeCheck();
            _presetIndex = EditorGUILayout.Popup("Preset", _presetIndex, options);
            if (EditorGUI.EndChangeCheck() && _presetIndex > 0)
                _symbolInput = Presets[_presetIndex - 1];

            _symbolInput = EditorGUILayout.TextField("Symbol", _symbolInput);
            if (GUILayout.Button("Add"))
            {
                if (String.IsNullOrEmpty(_symbolInput))
                {
                    return;
                }
                AddNewSymbols();
                Close();
            }
        }

        private void AddNewSymbols()
        {
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            string current = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            string[] existing = string.IsNullOrEmpty(current) ? Array.Empty<string>() : current.Split(';');
            if (Array.Exists(existing, s => s == _symbolInput))
                return;

            string updated = string.IsNullOrEmpty(current) ? _symbolInput : $"{current};{_symbolInput}";
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, updated);
        }

        public static void OpenWindow()
        {
            var window = GetWindow<AddSymbolsForGroupEditorWindow>();
            window.titleContent = new UnityEngine.GUIContent("Add Symbols");
            float w = 400, h = 120;
            float x = (Screen.currentResolution.width - w) / 2f;
            float y = (Screen.currentResolution.height - h) / 2f;
            window.position = new Rect(x, y, w, h);
            window.Focus();
        }
    }
}
#endif