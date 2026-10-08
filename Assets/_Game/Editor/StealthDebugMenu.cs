using Game.Stealth;
using UnityEditor;

namespace Game.Editor
{
    /// <summary>
    /// <c>Tools/Stealth/Show Detection Gizmos</c> — toggles <see cref="StealthDebug.DrawAllGizmos"/> (detection
    /// gizmos on every EntityPerception, not just the selected one). Persisted in EditorPrefs across domain reloads.
    /// </summary>
    public static class StealthDebugMenu
    {
        private const string MENU = "Tools/Stealth/Show Detection Gizmos";
        private const string PREF_KEY = "Game.StealthDebug.DrawAllGizmos";

        [MenuItem(MENU)]
        private static void Toggle()
        {
            StealthDebug.DrawAllGizmos = !StealthDebug.DrawAllGizmos;
            EditorPrefs.SetBool(PREF_KEY, StealthDebug.DrawAllGizmos);
            SceneView.RepaintAll();
        }

        [MenuItem(MENU, true)]
        private static bool Validate()
        {
            Menu.SetChecked(MENU, StealthDebug.DrawAllGizmos);
            return true;
        }

        [InitializeOnLoadMethod]
        private static void Restore()
        {
            StealthDebug.DrawAllGizmos = EditorPrefs.GetBool(PREF_KEY, false);
        }
    }
}
