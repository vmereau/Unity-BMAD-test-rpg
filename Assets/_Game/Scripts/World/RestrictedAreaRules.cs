using System.Collections.Generic;
using Game.Core;
using Game.Factions;
using Game.NPC;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// <b>Static, pure.</b> Rules behind <see cref="RestrictedArea"/>: box containment, hours window and owner matching.
    /// Tested by <c>RestrictedAreaRulesTests</c>.
    /// </summary>
    public static class RestrictedAreaRules
    {
        /// <summary>True when <paramref name="localPoint"/> (in the box's local space) is inside the box (faces inclusive).</summary>
        public static bool IsInsideBox(Vector3 localPoint, Vector3 center, Vector3 size)
        {
            Vector3 d = localPoint - center;
            return Mathf.Abs(d.x) <= size.x * 0.5f && Mathf.Abs(d.y) <= size.y * 0.5f && Mathf.Abs(d.z) <= size.z * 0.5f;
        }

        /// <summary>
        /// True when the area is restricted at <paramref name="hour"/>. Without an hours window, or without a clock
        /// (region played without Core), it is always restricted. Window rule = <see cref="GameTime.IsHourInWindow"/>.
        /// </summary>
        public static bool IsRestrictedHour(bool hasClock, bool useHours, int hour, int startHour, int endHour) =>
            !useHours || !hasClock || GameTime.IsHourInWindow(startHour, endHour, hour);

        /// <summary>True when <paramref name="entity"/> is a listed owner, or its <paramref name="faction"/> is a listed owner faction.</summary>
        public static bool IsOwner(IReadOnlyList<NPCEntity> ownerEntities, IReadOnlyList<FactionSO> ownerFactions,
            NPCEntity entity, FactionSO faction)
        {
            if (entity != null && ownerEntities != null)
                for (int i = 0; i < ownerEntities.Count; i++)
                    if (ownerEntities[i] == entity) return true;
            if (faction != null && ownerFactions != null)
                for (int i = 0; i < ownerFactions.Count; i++)
                    if (ownerFactions[i] == faction) return true;
            return false;
        }
    }
}
