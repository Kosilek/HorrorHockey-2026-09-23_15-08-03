#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace _FM.Editor.SymbolsForGroup
{
    public class RemoveSymbolsForGroupEditorWindow : EditorWindow
    {
        private int _index = 0;

        void OnGUI()
        {
            string[] array = Array.Empty<string>();
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            string current = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            if (string.IsNullOrEmpty(current))
            {
                Debug.Log("RemoveSymbols: no symbols to remove, window closed");
                Close();
                return;
            }

            array = current.Split(';');
            _index = EditorGUILayout.Popup("Select Remove", _index, array);
            if (GUILayout.Button("Remove"))
            {
                if (array.Length == 0)
                {
                    Debug.Log("RemoveSymbols: no symbols to remove, window closed");
                    Close();
                    return;
                }

                var list = new List<string>(array);
                list.RemoveAt(_index);
                string updated = string.Join(";", list);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, updated);
                Close();
            }
        }

        public static void OpenWindow()
        {
            var window = GetWindow<RemoveSymbolsForGroupEditorWindow>();
            window.titleContent = new UnityEngine.GUIContent("Remove Symbols");
            float w = 400, h = 25;
            float x = (Screen.currentResolution.width - w) / 2f;
            float y = (Screen.currentResolution.height - h) / 2f;
            window.position = new Rect(x, y, w, h);
            window.Focus();
        }
    }
}
#endif