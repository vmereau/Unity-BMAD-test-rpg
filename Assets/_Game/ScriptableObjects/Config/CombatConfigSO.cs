using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Balancing values for the combat system. Assign CombatConfig.asset in Inspector.
    /// All tunable gameplay values live here — never hardcode in combat scripts.
    /// </summary>
    [CreateAssetMenu(menuName = "Config/Combat", fileName = "CombatConfig")]
    public class CombatConfigSO : ScriptableObject
    {
        [Header("Stamina Pool")]
        public float baseStaminaPool = 100f;

        [Header("Stamina Costs")]
        public float attackStaminaCost = 20f;
        public float blockStaminaCostPerHit = 15f;
        public float dodgeStaminaCost = 25f;

        [Header("Stamina Recovery")]
        [Tooltip("Stamina units recovered per second after the regen delay elapses.")]
        public float staminaRegenRate = 20f;
        [Tooltip("Seconds after last Consume() call before regen begins.")]
        public float staminaRegenDelay = 1.5f;

        [Header("Perfect Block")]
        [Tooltip("Seconds after raising block during which a hit counts as a perfect block.")]
        public float perfectBlockWindowDuration = 0.25f;

        [Header("Dodge")]
        [Tooltip("Duration in seconds of the dodge roll movement and i-frame window.")]
        public float dodgeDuration = 0.5f;
        [Tooltip("Horizontal speed in units/sec during the dodge roll.")]
        public float dodgeSpeed = 8f;

        [Header("Player Health")]
        public float baseHealth = 100f;

        [Header("Attack Damage")]
        [Tooltip("Damage dealt to enemies per player attack hit.")]
        public float attackDamage = 25f;
        [Tooltip("Radius of the sphere overlap used for player attack hit detection.")]
        public float attackHitRange = 2f;

        [Header("Sneak Attack")]
        [Tooltip("Damage multiplier on a player hit against an entity that has not detected the player.")]
        [Min(1f)] public float sneakAttackDamageMultiplier = 2f;

        [Header("Stance (player hits on AI)")]
        [Tooltip("Stance damage per unarmed hit. Unarmed hits never knock down.")]
        [Min(0f)] public float unarmedStanceDamage = 15f;
        [Tooltip("Extra stance damage per Strength point above the base Strength.")]
        [Min(0f)] public float stancePerStrength = 0.5f;
        [Tooltip("Stance damage multiplier on the last step of a combo (finisher).")]
        [Min(1f)] public float finisherStanceMultiplier = 1.5f;
        [Tooltip("Flat stance damage added per melee hit when the Power Strike skill is learned.")]
        [Min(0f)] public float powerStrikeStanceBonus = 5f;

        [Header("Player Stance (AI hits on the player)")]
        [Tooltip("Hidden player stance meter. AI hits drain it; it regenerates after a delay without stance damage.")]
        [Min(1f)] public float playerMaxStance = 100f;
        [Tooltip("Fraction of max stance at/below which a hit staggers the player (once per drain). 0 stance = knockdown.")]
        [Range(0f, 1f)] public float playerStaggerThreshold = 0.5f;
        [Tooltip("Seconds without stance damage before the player's stance regenerates.")]
        [Min(0f)] public float playerStanceRegenDelay = 2f;
        [Tooltip("Stance regenerated per second once regen starts.")]
        [Min(0f)] public float playerStanceRegenRate = 20f;
        [Tooltip("Hyper-armor: fraction of stance damage ignored while the player attacks (0 = none, 1 = immune).")]
        [Range(0f, 1f)] public float playerAttackStanceResistance = 0f;
        [Tooltip("Stance damage multiplier on a hit absorbed by a regular block (never knocks down).")]
        [Range(0f, 1f)] public float blockStanceMultiplier = 0.5f;
        [Tooltip("Seconds the player is locked in a stagger (match the stagger clip length).")]
        [Min(0.1f)] public float playerStaggerDuration = 1.1f;
        [Tooltip("Seconds the player is down: knocked down + getting up clips (match their combined length).")]
        [Min(0.1f)] public float playerKnockdownDuration = 4.6f;

        [Header("Unarmed Combo")]
        [Tooltip("Player combo length with no weapon equipped (Humanoid_Template Attack_1..Attack_3).")]
        [Min(1)] public int unarmedComboSteps = 3;
        [Tooltip("Watchdog: seconds an attack may go without any attack state enter/exit or clip event before it is " +
                 "force-ended. Must exceed the longest gap between attack callbacks of any attack clip.")]
        [Min(0.1f)] public float attackStallTimeout = 1.5f;

        [Header("Draw / Sheathe")]
        [Tooltip("Playback speed of the draw / sheathe clips (Humanoid_Template WeaponSwitchSpeed).")]
        [Min(0.1f)] public float weaponSwitchAnimSpeed = 1.5f;
        [Tooltip("Watchdog: seconds (at speed 1 — divided by weaponSwitchAnimSpeed at runtime) a draw / sheathe may run " +
                 "without its WeaponSwap event or state exit before the weapon swap is forced. Must exceed the longest " +
                 "draw / sheathe clip at speed 1 (Sheath Sword ≈ 1.3 s).")]
        [Min(0.1f)] public float weaponSwitchTimeout = 2f;

        [Header("Bow Aim")]
        [Tooltip("Camera vertical field of view while aiming (the default lens FOV is restored when the aim ends).")]
        [Range(10f, 90f)] public float aimFieldOfView = 30f;
        [Tooltip("CinemachineFollow offset while aiming — closer, over the right shoulder.")]
        public Vector3 aimFollowOffset = new Vector3(0.6f, 0.3f, -1.8f);
        [Tooltip("Mouse look sensitivity multiplier while aiming.")]
        [Range(0.05f, 1f)] public float aimSensitivityMultiplier = 0.5f;
        [Tooltip("Aim camera blend speed (1 / seconds): 8 ≈ 0.12 s in or out.")]
        [Min(0.1f)] public float aimBlendSpeed = 8f;
        [Tooltip("Watchdog: seconds an aim may wait for the nocked aim pose (BowAim_State enter) before the arrow is " +
                 "forced nocked. Must exceed Draw arrow (+ Fire arrow) at speed 1 (≈ 1.8 s).")]
        [Min(0.1f)] public float bowNockTimeout = 2.5f;

        [Header("Bow Aim Spread")]
        [Tooltip("Accuracy cone half-angle (degrees) when the arrow is nocked; shrinks to 0 while holding still.")]
        [Range(0f, 30f)] public float aimSpreadMaxAngle = 5f;
        [Tooltip("Seconds of still aim (from the nock) for the cone to shrink from max to 0 — a perfect shot.")]
        [Min(0.05f)] public float aimSpreadShrinkDuration = 1.5f;
        [Tooltip("Horizontal speed (m/s) above which the player counts as moving: the cone widens back instead.")]
        [Min(0f)] public float aimMoveSpeedThreshold = 0.5f;
        [Tooltip("Widen rate while moving, as a multiple of the shrink rate.")]
        [Min(0f)] public float aimMoveWidenMultiplier = 1f;

        [Header("Bow Aim Correction")]
        [Tooltip("Post-animation spine correction toward the crosshair while aiming (BowAimCorrection).")]
        public bool aimCorrectionEnabled = true;
        [Tooltip("Max yaw / pitch (degrees) each of Spine, Chest and UpperChest may add.")]
        [Range(0f, 90f)] public float aimCorrectionMaxAnglePerBone = 35f;
        [Tooltip("Aim points closer than this (metres along the crosshair ray) are pushed out to it, so a close wall " +
                 "doesn't whip the spine.")]
        [Min(0f)] public float aimCorrectionMinDistance = 2f;

        [Header("Lock-On")]
        [Tooltip("Max acquisition distance in metres.")]
        public float lockOnRange = 15f;
        [Tooltip("Half-angle of detection cone in degrees (120° total cone).")]
        public float lockOnFOV = 60f;
        [Tooltip("Camera yaw/pitch lerp factor toward locked target per second.")]
        public float lockOnLerpSpeed = 5f;
        [Tooltip("Auto-unlock threshold if target exceeds this distance.")]
        public float lockOnBreakDistance = 20f;
        [Tooltip("Vertical offset applied to the locked target position for camera tracking (e.g. ~half enemy height).")]
        public float lockOnTargetHeightOffset = 1.0f;
    }
}
