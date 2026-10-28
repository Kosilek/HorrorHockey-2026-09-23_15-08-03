#if UNITY_EDITOR
using UnityEditor;

namespace _FM.Editor.SymbolsForGroup
{
    public static class SymbolsForGroupEditor
    {
        [MenuItem("_FM/SymbolsForGroup/AddSymbols")]
        public static void AddSymbolsForGroup()
        {
            AddSymbolsForGroupEditorWindow.OpenWindow();
        }
        [MenuItem("_FM/SymbolsForGroup/RemoveSymbols")]
        public static void RemoveSymbolsForGroup()
        {
            RemoveSymbolsForGroupEditorWindow.OpenWindow();
        }
        [MenuItem("_FM/SymbolsForGroup/ClearSymbols")]
        public static void ClearSymbolsForGroup()
        {
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, "");
        }
    }
}
#endif