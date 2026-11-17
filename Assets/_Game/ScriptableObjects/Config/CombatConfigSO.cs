using Game.Dialogue;
using Game.Inventory;
using UnityEngine;
using UnityEngine.Serialization;

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
        public float baseHealth = 40f;

        [Header("Damage Mitigation")]
        [Tooltip("Armor floor: a hit always deals at least this fraction of its raw damage (min 1). " +
                 "Damage taken = max(raw - armor, raw * fraction). See _bmad-output/balance.md.")]
        [Range(0f, 1f)] public float minDamageFraction = 0.1f;

        [Header("Attack")]
        [Tooltip("Fists are a weapon: base damage added to Strength (1:1), then × the unarmed combo step's damageMultiplier.")]
        [Min(0f)] public float unarmedDamage = 10f;
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

        [Header("Unconscious (non-lethal humanoid knockouts — player and NPCs)")]
        [Tooltip("Seconds a knocked-out humanoid lies on the ground (knocked down clip held) before getting up.")]
        [Min(0.1f)] public float unconsciousDuration = 8f;
        [Tooltip("Fraction of max health regained, linearly, while unconscious.")]
        [Range(0.01f, 1f)] public float unconsciousRecoverHealthFraction = 0.15f;
        [Tooltip("Seconds still incapacitated after the get-up starts (match the 'getting up' clip length).")]
        [Min(0f)] public float getUpDuration = 2.7f;

        [Header("Knockout loot (the NPC that knocked the player out takes gold)")]
        [Tooltip("Gold taken on top of the fraction (all of it when the player has less).")]
        [Min(0)] public int knockoutLootBaseGold = 15;
        [Tooltip("Random fraction of the player's gold added to the base — lower bound.")]
        [Range(0f, 1f)] public float knockoutLootFractionMin = 0.10f;
        [Tooltip("Random fraction of the player's gold added to the base — upper bound.")]
        [Range(0f, 1f)] public float knockoutLootFractionMax = 0.20f;
        [Tooltip("Flat distance to the player's body at which the robber stops and plays the pickup clip.")]
        [Min(0.1f)] public float knockoutLootReachDistance = 1.0f;
        [Tooltip("Seconds into the pickup clip when the gold is taken (the player must still be lying down).")]
        [Min(0f)] public float knockoutLootGrabTime = 1.75f;
        [Tooltip("Seconds the robber stays in the pickup clip before returning to its routine (match the 'picking up' clip length).")]
        [Min(0.1f)] public float knockoutLootClipDuration = 5f;
        [Tooltip("Gold item the robber puts in its own inventory (one per coin).")]
        public GoldItemSO knockoutLootGold;
        [Tooltip("Said when the robber starts walking to the body.")]
        public BarkSetSO knockoutLootTauntBarks;
        [Tooltip("Said after the grab when the player had at least the base amount.")]
        public BarkSetSO knockoutLootThanksBarks;
        [Tooltip("Said after the grab when the player had less than the base amount (or none).")]
        public BarkSetSO knockoutLootPoorBarks;

        [Header("Player relationship (spec-player-relationship-tiers)")]
        [Tooltip("A Friendly NPC joins against anyone fighting the player within this distance of itself.")]
        [Min(0f)] public float friendlyAssistRange = 12f;
        [Tooltip("Seconds after a Friendly NPC's warning in which another hit from the player starts a brawl.")]
        [Min(0f)] public float friendlyGraceDuration = 8f;
        [Tooltip("Seconds after a Friendly NPC's warning in which further hits (the rest of the same combo) are absorbed " +
                 "instead of starting the brawl. Keep it longer than a player combo.")]
        [Min(0f)] public float friendlyWarnLockout = 2f;
        [Tooltip("Minimum seconds between two 'on sight' (hostile) or two 'joining' (friendly) barks of the same NPC.")]
        [FormerlySerializedAs("hostileSightBarkCooldown")]
        [Min(0f)] public float relationshipBarkCooldown = 30f;
        [Tooltip("Said by a hostile NPC when it spots the player.")]
        public BarkSetSO hostileSightBarks;
        [Tooltip("Said by a Friendly NPC joining the player's fight.")]
        public BarkSetSO friendlyAssistBarks;
        [Tooltip("Said by a Friendly NPC the player hits (grace window starts).")]
        public BarkSetSO friendlyWarnBarks;
        [Tooltip("Said by a Friendly NPC whose blow knocked the player out (it doesn't loot).")]
        public BarkSetSO friendlyDisappointedBarks;

        [Header("Mutual defense (spec-faction-mutual-defense)")]
        [Tooltip("A free NPC joins a fight a member it defends is in when the opponent is within this distance of itself " +
                 "(capped below its own DisengageRange).")]
        [Min(0f)] public float memberDefendRange = 12f;
        [Tooltip("Said by an NPC joining a fight to defend a fellow member (monsters never bark). Uses relationshipBarkCooldown.")]
        public BarkSetSO memberDefendBarks;

        [Header("Combat tactics (spec-ai-combat-tactics-foundation)")]
        [Tooltip("Max entities with a CombatStyle swinging at one target at once; the others circle and wait.")]
        [Min(1)] public int maxAttackersPerTarget = 2;

        [Header("Execute (stomp an unconscious NPC — spec-execute-unconscious-npc)")]
        [Tooltip("Max ground distance from the player to the body's chest for F to execute.")]
        [Min(0.1f)] public float executeRange = 1.8f;
        [Tooltip("Half-angle (degrees) of the forward cone the body must be in.")]
        [Range(0f, 180f)] public float executeConeHalfAngle = 70f;
        [Tooltip("Where the stomping foot lands, in player-local space (measured on execute stomp.fbx, frame 22).")]
        public Vector3 executeFootOffset = new Vector3(-0.45f, 0f, 0.88f);
        [Tooltip("Seconds the player slides / turns into the stomp stance at the start.")]
        [Min(0f)] public float executeAlignDuration = 0.2f;
        [Tooltip("Execute Stomp clip length (s). Watchdog = this + 0.5 s if ExecuteEnd never arrives.")]
        [Min(0.1f)] public float executeClipDuration = 1.867f;
        [Tooltip("Extra seconds past the clip length before the watchdog ends a stomp whose ExecuteEnd never came.")]
        [Min(0f)] public float executeWatchdogMargin = 0.5f;
        [Tooltip("Clip events earlier than this after F are ignored: a just-cancelled stomp blending out can still fire them.")]
        [Min(0f)] public float executeMinEventDelay = 0.3f;
        [Tooltip("Overlap radius beyond executeRange: hurtbox colliders sit away from the chest point that is range-checked.")]
        [Min(0f)] public float executeScanPadding = 1f;
        [Tooltip("Maximum chest height difference from the player's feet; rejects bodies on another floor.")]
        [Min(0f)] public float executeVerticalReach = 0.9f;
        [Tooltip("Maximum ground-plane gap from the calibrated landing foot to the chest at impact.")]
        [Min(0f)] public float executeImpactTolerance = 0.35f;
        [Tooltip("Solid scenery that blocks access to a body (Default and Interactable; triggers ignored).")]
        public LayerMask executeOcclusionMask = 257;

        [Header("Unarmed Combo")]
        [Tooltip("Player combo length with no weapon equipped (Humanoid_Template Attack_1..Attack_3). Capped by unarmedAttacks.")]
        [Min(1)] public int unarmedComboSteps = 3;
        [Tooltip("Watchdog: seconds an attack may go without any attack state enter/exit or clip event before it is " +
                 "force-ended. Must exceed the longest gap between attack callbacks of any attack clip.")]
        [Min(0.1f)] public float attackStallTimeout = 1.5f;
        [Tooltip("Recovery cancel (spec-attack-recovery-cancel): once the step's combo window closed, move input at least " +
                 "this strong (0–1 stick / WASD magnitude) ends the swing and blends to locomotion.")]
        [Range(0.05f, 1f)] public float recoveryCancelMoveThreshold = 0.2f;

        [Header("Attack Step (player forward step per swing)")]
        [Tooltip("Forward step of a weapon attack state the archetype doesn't list (WeaponArchetypeSO.attackSteps).")]
        public AttackStep defaultAttackStep = new AttackStep(0.5f, 0.35f, 0.35f);
        [Tooltip("Unarmed combo, one entry per attack state (element 0 = Attack_1 lead jab, 1 = hook, 2 = uppercut finisher): " +
                 "forward step, hand whose hitbox opens, health / stance damage multipliers. Missing entries use " +
                 "defaultAttackStep, right hand, x1.")]
        public UnarmedAttack[] unarmedAttacks =
        {
            new UnarmedAttack(new AttackStep(0.3f, 0.07f, 0.43f), WeaponHand.Left),
            new UnarmedAttack(new AttackStep(0.35f, 0.27f, 0.37f), WeaponHand.Right),
            new UnarmedAttack(new AttackStep(0.5f, 0.1f, 0.37f), WeaponHand.Left, 1.5f, 2f),
        };

        /// <summary>
        /// Forward step of attack state <paramref name="attackIndex"/> (1 = Attack_1), shared by the player and AI humanoids
        /// (spec-ai-attack-root-and-step): a weapon uses its archetype's <c>attackSteps</c> (else <see cref="defaultAttackStep"/>),
        /// fists use <see cref="unarmedAttacks"/> (else <see cref="defaultAttackStep"/>).
        /// </summary>
        public AttackStep ResolveAttackStep(WeaponSO weapon, int attackIndex)
        {
            if (weapon != null) return weapon.ResolveAttackStep(attackIndex, defaultAttackStep);
            return UnarmedAttack.Resolve(unarmedAttacks, attackIndex, UnarmedAttack.Fallback(defaultAttackStep)).step;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // A combo longer than the authored steps would play its extra steps with fallback data.
            if (unarmedAttacks == null) return;
            if (unarmedAttacks.Length > 0 && unarmedComboSteps > unarmedAttacks.Length)
                unarmedComboSteps = unarmedAttacks.Length;
            // An element added in the Inspector is zero-initialised (struct): a 0 multiplier would silently make
            // that step deal no damage. 0 is never a wanted value here, so it means "not authored" = x1.
            for (int i = 0; i < unarmedAttacks.Length; i++)
            {
                if (unarmedAttacks[i].damageMultiplier <= 0f) unarmedAttacks[i].damageMultiplier = 1f;
                if (unarmedAttacks[i].stanceMultiplier <= 0f) unarmedAttacks[i].stanceMultiplier = 1f;
            }
        }
#endif

        [Header("Draw / Sheathe")]
        [Tooltip("Playback speed of the draw / sheathe clips (Humanoid_Template WeaponSwitchSpeed).")]
        [Min(0.1f)] public float weaponSwitchAnimSpeed = 1.5f;
        [Tooltip("Watchdog: seconds (at speed 1 — divided by weaponSwitchAnimSpeed at runtime) a draw / sheathe may run " +
                 "without its WeaponSwap event or state exit before the weapon swap is forced. Must exceed the longest " +
                 "draw / sheathe clip at speed 1 (2hand sheath ≈ 2 s).")]
        [Min(0.1f)] public float weaponSwitchTimeout = 2.5f;

        [Header("Bow Aim")]
        [Tooltip("Camera vertical field of view while aiming (the default lens FOV is restored when the aim ends).")]
        [Range(10f, 90f)] public float aimFieldOfView = 30f;
        [Tooltip("Camera rig while aiming — x, y = ThirdPersonFollow shoulder offset, -z = arm distance " +
                 "(closer, over the right shoulder).")]
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
