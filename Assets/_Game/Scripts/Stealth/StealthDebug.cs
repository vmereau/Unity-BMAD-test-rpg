#if UNITY_EDITOR
namespace Game.Stealth
{
    /// <summary>
    /// Editor detection gizmos for every active <c>EntityPerception</c>, toggled from
    /// <c>Tools/Stealth/Show Detection Gizmos</c> (persisted in EditorPrefs by StealthDebugMenu). When off, gizmos
    /// draw only for the selected entity. Editor-only — compiled out of builds.
    /// </summary>
    public static class StealthDebug
    {
        public static bool DrawAllGizmos;
    }
}
#endif
