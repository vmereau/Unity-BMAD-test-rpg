using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// What <see cref="BowAimCorrection"/> turns the spine toward (spec-ai-ranged-combat-style): the player's crosshair
    /// (<c>PlayerCombat</c>) or an NPC archer's target (<c>EntityArcher</c>). On the same root as the correction.
    /// </summary>
    public interface IBowAimSource
    {
        /// <summary>The bow is raised (IsAiming): the correction blends in.</summary>
        bool IsBowAiming { get; }

        /// <summary>
        /// Current aim point and the view ray it lies on (player: the camera ray; NPC: chest → target). False without a bow
        /// or target — the correction holds the last point while blending out.
        /// </summary>
        bool TryGetBowAim(out Vector3 point, out Ray viewRay);
    }
}
