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
    }
}
