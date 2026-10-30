using Game.Core;
using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// Two-handed weapons: while drawn, re-aims the weapon's <c>Drawn</c> child every frame so the handle runs through
    /// both hands (<see cref="TwoHandGripMath"/>) — the Mixamo two-handed clips don't keep a constant hand relationship,
    /// so no rigid <see cref="WeaponArchetypeSO.drawnPose"/> fits them. Only for archetypes with
    /// <see cref="WeaponArchetypeSO.twoHandedGrip"/>; anything else is left to the rigid pose.
    /// Runs after the Animator and after <c>BowAimCorrection</c> (200), which moves the spine and therefore the hands.
    /// </summary>
    [DefaultExecutionOrder(250)]
    public class TwoHandGripConstraint : MonoBehaviour
    {
        private const string TAG = "[Inventory]";
        private const string DRAWN_CHILD = "Drawn";

        [SerializeField] private EquipmentVisuals _equipmentVisuals;

        private GameObject _cachedVisual;
        private Transform _drawn;
        private bool _leftSocketWarned;
        private bool _leftHandArchetypeWarned;

        private void Awake()
        {
            if (_equipmentVisuals == null) _equipmentVisuals = GetComponent<EquipmentVisuals>();
            if (_equipmentVisuals == null)
            {
                GameLog.Error(TAG, "TwoHandGripConstraint: no EquipmentVisuals — disabled");
                enabled = false;
            }
        }

        // Leaves the rigid pose behind when the component stops driving the weapon (no frozen mid-swing offset).
        private void OnDisable()
        {
            if (_equipmentVisuals == null) return; // Guard: Awake may disable before the reference is resolved
            WeaponArchetypeSO archetype = _equipmentVisuals.CurrentArchetype;
            Transform drawn = FindDrawn(_equipmentVisuals.ActiveWeaponGO);
            if (archetype != null && drawn != null) archetype.drawnPose.ApplyTo(drawn);
        }

        private void LateUpdate()
        {
            WeaponArchetypeSO archetype = _equipmentVisuals.CurrentArchetype;
            if (archetype == null || !archetype.twoHandedGrip || !_equipmentVisuals.IsDrawn) return;
            if (archetype.drawnHand == WeaponHand.Left)
            {
                if (!_leftHandArchetypeWarned)
                {
                    _leftHandArchetypeWarned = true;
                    GameLog.Warn(TAG, $"TwoHandGripConstraint: '{archetype.name}' combines twoHandedGrip with drawnHand = Left — unsupported, rigid pose kept");
                }
                return;
            }

            Transform drawn = FindDrawn(_equipmentVisuals.ActiveWeaponGO);
            if (drawn == null) return;

            Transform right = _equipmentVisuals.DrawnSocket;
            Transform left = _equipmentVisuals.LeftHandSocket;
            if (left == null)
            {
                if (!_leftSocketWarned)
                {
                    _leftSocketWarned = true;
                    GameLog.Warn(TAG, $"TwoHandGripConstraint: '{archetype.name}' uses the two-hand grip but _leftWeaponSocket is unassigned — rigid pose kept");
                }
                return;
            }

            Apply(drawn, right, left, archetype);
        }

        /// <summary>
        /// Writes on <paramref name="drawn"/> the rigid drawnPose blended toward the two-hand solve by its grip weight
        /// (<see cref="TwoHandGripMath.GripWeight"/>): full two-hand grip in normal clips, easing back to the rigid pose
        /// as the left hand lets go (sheathe, knockdown, death) or the solve degenerates — never a pop.
        /// Also used by the Weapon Pose Tuner preview.
        /// </summary>
        public static void Apply(Transform drawn, Transform rightSocket, Transform leftSocket, WeaponArchetypeSO archetype)
        {
            if (drawn == null || rightSocket == null || leftSocket == null || archetype == null) return;
            archetype.drawnPose.ApplyTo(drawn); // rigid pose first: the fallback and the blend's start
            // drawnPose's edge (+Z) in world space is the roll reference: keeps the tuned edge direction meaningful.
            Vector3 edgeReference = drawn.rotation * Vector3.forward;
            if (!TwoHandGripMath.TrySolve(rightSocket.position, leftSocket.position, edgeReference,
                    archetype.gripRoll, archetype.gripSlide, out Vector3 position, out Quaternion rotation, out float weight))
                return;
            drawn.SetPositionAndRotation(
                Vector3.Lerp(drawn.position, position, weight),
                Quaternion.Slerp(drawn.rotation, rotation, weight));
        }

        private Transform FindDrawn(GameObject visual)
        {
            if (visual != _cachedVisual)
            {
                _cachedVisual = visual;
                _drawn = visual != null ? visual.transform.Find(DRAWN_CHILD) : null;
            }
            return _drawn;
        }
    }
}
