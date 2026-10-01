#if UNITY_EDITOR
namespace Game.Combat
{
    /// <summary>
    /// Editor play-mode sweep gizmos for every <see cref="WeaponHitbox"/>, toggled from
    /// <c>Tools/Combat/Show Hitbox Sweeps</c> (persisted in EditorPrefs by HitboxDebugMenu).
    /// Editor-only — compiled out of builds.
    /// </summary>
    public static class HitboxDebug
    {
        public static bool DrawSweeps;
    }
}
#endif
