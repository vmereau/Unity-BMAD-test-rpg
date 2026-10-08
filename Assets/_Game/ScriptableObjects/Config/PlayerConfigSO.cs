using UnityEngine;

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

        [Header("Jump")]
        [SerializeField] public float jumpForce = 5f;
    }
}
