using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Player
{
    /// <summary>
    /// Tunable movement parameters for PlayerController.
    /// Assign the PlayerConfig.asset instance on the Player prefab.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "Game/Config/Player Config")]
    public class PlayerConfigSO : ScriptableObject
    {
        [SerializeField] public float walkSpeed = 3f;
        [SerializeField] public float runSpeed = 6f;
        [Tooltip("Sneak locomotion speed. Sneak blend-tree positions sit at sneakSpeed / runSpeed — update the controller if you change either.")]
        [SerializeField] public float sneakSpeed = 1.5f;
        [SerializeField] public float rotationSpeed = 10f;
        [Tooltip("Bow aim stance: body yaw (degrees) added to the camera yaw while aiming — keeps the side-on archer " +
                 "look. BowAimCorrection turns the spine the rest of the way to the crosshair.")]
        [FormerlySerializedAs("aimBodyYawOffset")]
        [SerializeField] public float aimStanceYaw = 45f;

        [Header("Jump")]
        [SerializeField] public float jumpForce = 5f;

        [Header("Swimming")]
        [Min(0f)] [SerializeField] public float swimSpeed = 2f;
        [Tooltip("Sprint swim speed. The Swim Locomotion blend tree sits at swimSpeed / runSpeed and swimSprintSpeed / runSpeed — update the controller if you change them.")]
        [Min(0f)] [SerializeField] public float swimSprintSpeed = 3.5f;
        [Min(0f)] [SerializeField] public float swimSprintStaminaPerSecond = 8f;
        [Tooltip("Water depth at the feet (m) that starts swimming. Must be <= swimFloatDepth.")]
        [Min(0f)] [SerializeField] public float swimEnterDepth = 1.3f;
        [Tooltip("Water depth at the feet (m) below which swimming ends. Must be < swimEnterDepth.")]
        [Min(0f)] [SerializeField] public float swimExitDepth = 1.1f;
        [Tooltip("How far the feet hang below the surface while swimming (m): sets the waterline on the body.")]
        [Min(0f)] [SerializeField] public float swimFloatDepth = 1.45f;
        [Tooltip("Max speed (m/s) toward the float height, e.g. rising after a fall into the water.")]
        [Min(0.01f)] [SerializeField] public float swimRiseSpeed = 2f;
        [Tooltip("Metres swum between two stroke sounds.")]
        [Min(0.1f)] [SerializeField] public float swimStrokeDistance = 2f;

        [Header("Attack")]
        [Tooltip("Turn speed toward the move input during an attack's wind-up (until its HitboxEnable event commits the swing).")]
        [Min(0f)] [SerializeField] public float attackSteerSpeed = 8f;

        [Header("Animation")]
        [Tooltip("Seconds for a full 0↔1 fade of the UpperBody layer weight (attack / dodge / reaction start and end).")]
        [Min(0.01f)] [SerializeField] public float upperBodyBlendTime = 0.15f;

        private void OnValidate()
        {
            // exit < enter <= float, or floating would end the swim and restart it every frame.
            swimEnterDepth = Mathf.Min(swimEnterDepth, swimFloatDepth);
            swimExitDepth = Mathf.Min(swimExitDepth, swimEnterDepth - 0.05f);
        }
    }
}
