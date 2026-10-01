using Game.Combat;
using UnityEditor;

namespace Game.Editor
{
    /// <summary>
    /// <c>Tools/Combat/Show Hitbox Sweeps</c> — toggles <see cref="HitboxDebug.DrawSweeps"/> (editor
    /// play-mode sweep gizmos on every WeaponHitbox). Persisted in EditorPrefs across domain reloads.
    /// </summary>
    public static class HitboxDebugMenu
    {
        private const string MENU = "Tools/Combat/Show Hitbox Sweeps";
        private const string PREF_KEY = "Game.HitboxDebug.DrawSweeps";

        [MenuItem(MENU)]
        private static void Toggle()
        {
            HitboxDebug.DrawSweeps = !HitboxDebug.DrawSweeps;
            EditorPrefs.SetBool(PREF_KEY, HitboxDebug.DrawSweeps);
        }

        [MenuItem(MENU, true)]
        private static bool Validate()
        {
            Menu.SetChecked(MENU, HitboxDebug.DrawSweeps);
            return true;
        }

        [InitializeOnLoadMethod]
        private static void Restore()
        {
            HitboxDebug.DrawSweeps = EditorPrefs.GetBool(PREF_KEY, false);
        }
    }
}
