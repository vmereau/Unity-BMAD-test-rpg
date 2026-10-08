using UnityEngine;

namespace Game.Stealth
{
    /// <summary>
    /// A target that AI perception detects through the vision cone / awareness meter instead of the
    /// plain detection radius. Implemented by the player (<c>PlayerSneak</c>); polled by
    /// <c>EntityPerception</c> through <c>FactionMember.StealthTarget</c> — no events across systems.
    /// </summary>
    public interface IStealthTarget
    {
        /// <summary>True while the target is in sneak stance (shorter sight range, slower fill).</summary>
        bool IsSneaking { get; }

        /// <summary>World point the line-of-sight ray aims at (chest height, lower while sneaking).</summary>
        Vector3 VisibilityPoint { get; }
    }
}
