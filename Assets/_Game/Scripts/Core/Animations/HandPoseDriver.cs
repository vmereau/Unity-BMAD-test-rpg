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

        private const float CARRY_BLEND_TIME = 0.15f; // ≈ the CombatIdle ⇄ BowDrawArrow transition
        private WeaponArchetypeSO _carryArchetype;
        private float _carryWeight;
        private bool _wasWeaponInHand;
        private float _offhandArmWeight;

        private void Awake()
        {
            if (_bridge == null) _bridge = GetComponent<HumanoidAnimationBridge>();
            if (_bridge == null)
            {
                GameLog.Warn(TAG, $"{name}: no HumanoidAnimationBridge — HandPoseDriver disabled");
                enabled = false;
            }
        }

        // A disabled driver (death list) must not leave the flag latched for the drivers' UpperBody weight.
        private void OnDisable()
        {
            if (_bridge != null) _bridge.FullBodyCombatLocomotion = false;
        }

        private void Update()
        {
            if (_bridge == null) return;

            bool weaponInHand = false, leftHandWeapon = false, twoHanded = false, shieldInHand = false, hasCarryPose = false,
                fullBodyLocomotion = false;
            OffhandArmPose armPose = OffhandArmPose.None;
            if (_equipmentVisuals != null)
            {
                armPose = _equipmentVisuals.OffhandArmPose;
                weaponInHand = _equipmentVisuals.IsDrawn && _equipmentVisuals.ActiveWeaponGO != null;
                var archetype = _equipmentVisuals.CurrentArchetype;
                if (archetype != null)
                {
                    leftHandWeapon = archetype.drawnHand == WeaponHand.Left;
                    twoHanded = archetype.twoHandedGrip;
                    hasCarryPose = archetype.hasCarryPose;
                    // Only while the weapon's own override plays: a forced default controller (NPC fists) or a sheathed
                    // weapon keeps the carry pose over the base combat legs.
                    fullBodyLocomotion = archetype.fullBodyCombatLocomotion && weaponInHand
                                         && !_equipmentVisuals.IsForcingDefaultController;
                }
                shieldInHand = _equipmentVisuals.ShieldInHand;
            }

            _bridge.FullBodyCombatLocomotion = fullBodyLocomotion; // read by the drivers' UpperBody weight
            bool switching = _bridge.IsSwitchingWeapon;
            bool carrySeat = HandPoseRule.CarrySeat(weaponInHand, hasCarryPose, leftHandWeapon && !twoHanded,
                _bridge.IsUpperBodyCombatIdle, _bridge.IsBaseFullBody);
            HandPoseRule.Resolve(_bridge.IsInCombat, weaponInHand, leftHandWeapon, twoHanded, shieldInHand,
                switching, _bridge.IsBaseDown, out HandPose left, out HandPose right, carrySeat);
            bool baseDown = _bridge.IsBaseDown;
            if (!baseDown) left = OffhandArmRule.LeftHand(armPose, left);
            _bridge.SetHandPoses(left, right);
            StepOffhandArm(OffhandArmRule.Target(armPose, _bridge.IsBaseFullBody, _bridge.IsUpperBodyAction, baseDown));
            if (hasCarryPose) StepCarrySeat(carrySeat, weaponInHand && !_wasWeaponInHand && !switching);
            else _carryArchetype = null;
            _wasWeaponInHand = weaponInHand;
        }

        // LeftArm layer (spec-sword-shield-take-combo): the off-hand arm pose fades in / out over OffhandArmRule.BLEND_TIME.
        private void StepOffhandArm(float target)
        {
            _offhandArmWeight = UpperBodyWeight.Step(_offhandArmWeight, target, Time.deltaTime, OffhandArmRule.BLEND_TIME);
            _bridge.SetOffhandArmWeight(_offhandArmWeight);
        }

        // Bow seat in the hand (spec-bow-idle-compensated-bake): eased toward the carry seat while idling, toward the aiming seat
        // otherwise, over the hand layers' blend time. Another weapon family, or a weapon that appears in the hand without a
        // draw clip (NPCs), starts on its target: nothing hides a slide there. A visual rebuilt for the same family (any
        // gear change refreshes it) keeps easing: EquipmentVisuals.RefreshWeapon keeps the weight too.
        private void StepCarrySeat(bool carrySeat, bool appearedInHand)
        {
            float target = carrySeat ? 1f : 0f;
            var archetype = _equipmentVisuals.CurrentArchetype;
            if (archetype != _carryArchetype || appearedInHand)
            {
                _carryArchetype = archetype;
                _carryWeight = target;
            }
            else
            {
                _carryWeight = Mathf.MoveTowards(_carryWeight, target, Time.deltaTime / CARRY_BLEND_TIME);
            }
            _equipmentVisuals.SetCarryWeight(_carryWeight);
        }
    }
}
