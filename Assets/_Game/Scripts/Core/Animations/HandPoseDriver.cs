using Game.Core;
using Game.Inventory;
using UnityEngine;

namespace Game.Animations
{
    /// <summary>
    /// Picks the finger pose of each hand every frame (<see cref="HandPoseRule"/>) from the equipped weapon / shield and the
    /// Animator's combat flag, and writes it through <see cref="HumanoidAnimationBridge.SetHandPoses"/>. Same component on
    /// the Player and on humanoid NPCs. Without <see cref="EquipmentVisuals"/> the entity counts as unarmed.
    /// </summary>
    [RequireComponent(typeof(HumanoidAnimationBridge))]
    public class HandPoseDriver : MonoBehaviour
    {
        private const string TAG = "[Animation]";

        [SerializeField] private HumanoidAnimationBridge _bridge;
        [Tooltip("Optional: the entity's equipment visuals (weapon / shield in hand). Unassigned = unarmed (fists in combat).")]
        [SerializeField] private EquipmentVisuals _equipmentVisuals;

        private void Awake()
        {
            if (_bridge == null) _bridge = GetComponent<HumanoidAnimationBridge>();
            if (_bridge == null)
            {
                GameLog.Warn(TAG, $"{name}: no HumanoidAnimationBridge — HandPoseDriver disabled");
                enabled = false;
            }
        }

        private void Update()
        {
            if (_bridge == null) return;

            bool weaponInHand = false, leftHandWeapon = false, twoHanded = false, shieldInHand = false;
            if (_equipmentVisuals != null)
            {
                weaponInHand = _equipmentVisuals.IsDrawn && _equipmentVisuals.ActiveWeaponGO != null;
                var archetype = _equipmentVisuals.CurrentArchetype;
                if (archetype != null)
                {
                    leftHandWeapon = archetype.drawnHand == WeaponHand.Left;
                    twoHanded = archetype.twoHandedGrip;
                }
                shieldInHand = _equipmentVisuals.ShieldInHand;
            }

            HandPoseRule.Resolve(_bridge.IsInCombat, weaponInHand, leftHandWeapon, twoHanded, shieldInHand,
                _bridge.IsSwitchingWeapon, _bridge.IsBaseDown, out HandPose left, out HandPose right);
            _bridge.SetHandPoses(left, right);
        }
    }
}
