using System.Collections.Generic;
using _Game.ScriptableObjects.Entities;
using Game.Core;
using Game.Factions;
using Game.NPC;
using Game.Stealth;
using Game.World;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Vision-cone perception of stealth targets (the player) for one entity. Owns an awareness meter
    /// (0..1) fed by cone + line-of-sight raycast + a small 360° proximity radius; the math lives in
    /// <see cref="StealthDetection"/>. Ticked by <see cref="EntityBrain"/> — perception never changes brain state
    /// itself. Non-stealth targets (NPC vs NPC) keep the brain's radius check.
    /// The mode is chosen per target: a <b>hostile</b> stealth target (DetectionRange &gt; 0) is perceived with the
    /// sneak-shortened sight range; otherwise, with an enabled <see cref="WitnessProfileSO"/>, a <b>non-hostile</b>
    /// one is witnessed (<see cref="IsWitnessing"/>) — only while it sneaks (awareness drains while it walks
    /// normally), sight range = the profile's WitnessRange. Hostile targets always win the scan.
    /// Inactive (no-op) when DetectionRange ≤ 0 and no witness profile is enabled.
    /// </summary>
    public class EntityPerception : MonoBehaviour
    {
        private const string TAG = "[AI]";

        [SerializeField] private PersistentID _persistentID;
        [SerializeField] private FactionMember _selfFactionMember;
        [SerializeField] private StealthConfigSO _config;

        private static readonly List<EntityPerception> _active = new();

        /// <summary>Enabled perceptions, for the runtime debug overlay.</summary>
        public static IReadOnlyList<EntityPerception> Active => _active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => _active.Clear();

        private float _scanTimer;
        private float _losTimer;
        private bool _hasLineOfSight;

        private Entity Entity => _persistentID != null ? _persistentID.Entity : null;

        private WitnessProfileSO WitnessProfile => Entity != null ? Entity.WitnessProfile : null;

        /// <summary>The Entity has an enabled witness profile (can witness a sneaking, non-hostile player).</summary>
        public bool CanWitness => WitnessProfileSO.IsEnabled(WitnessProfile);
        /// <summary>The current <see cref="Target"/> is non-hostile and perceived in witness mode.</summary>
        public bool IsWitnessing { get; private set; }
        public bool IsActive => enabled && _config != null && Entity != null && (Entity.DetectionRange > 0f || CanWitness);
        public StealthConfigSO Config => _config;

        public float Awareness { get; private set; }
        public bool IsFullyAware => Awareness >= 1f;
        public bool IsSuspicious => _config != null && Awareness > 0f && Awareness >= _config.suspicionThreshold;

        /// <summary>Current stealth-target candidate (closest hostile stealth target in range, else — witness — the closest non-hostile one), or null.</summary>
        public FactionMember Target { get; private set; }
        public bool CanSeeTarget { get; private set; }
        public Vector3 LastSeenPosition { get; private set; }
        public bool HasLastSeenPosition { get; private set; }
        public float TimeSinceSeen { get; private set; }

        // --- Debug read-outs (gizmos + StealthDebugOverlay) ---
        public float LastVisibility { get; private set; }
        public float LastFillPerSecond { get; private set; }
        public bool HasLineOfSight => _hasLineOfSight;
        public float DistanceToTarget { get; private set; }
        public float CurrentSightRange { get; private set; }
        public float CurrentProximityRadius { get; private set; }
        public bool TargetIsSneaking { get; private set; }
        public float ViewAngle => Entity != null ? Entity.ViewAngle : 0f;
        /// <summary>Hostile standing sight range (Entity.DetectionRange) — CurrentSightRange is the one in use.</summary>
        public float SightRange => Entity != null ? Entity.DetectionRange : 0f;
        /// <summary>Hostile sight range against a sneaking target.</summary>
        public float SneakingSightRange
        {
            get
            {
                if (Entity == null) return 0f;
                return _config != null ? Entity.DetectionRange * _config.sneakSightRangeMultiplier : Entity.DetectionRange;
            }
        }
        /// <summary>Witness sight range against a sneaking non-hostile target (no sneak multiplier); 0 without a profile.</summary>
        public float WitnessRange => CanWitness ? WitnessProfile.WitnessRange : 0f;
        /// <summary>Range within which a watching witness keeps tracking its target; 0 without a profile.</summary>
        public float WitnessWatchRange => CanWitness ? WitnessProfile.WatchRange : 0f;
        /// <summary>Standing proximity radius — CurrentProximityRadius is the sneak-adjusted one.</summary>
        public float ProximityRadius => Entity != null ? Entity.ProximityRadius : 0f;
        /// <summary>Sibling brain (debug views read its state name); may be null.</summary>
        public EntityBrain Brain { get; private set; }
        /// <summary>GameObject name cached in Awake (gameObject.name allocates on every access).</summary>
        public string DebugName { get; private set; }
        public Vector3 EyePosition => transform.position + Vector3.up * (Entity != null ? Entity.EyeHeight : 0f);

        private void Awake()
        {
            DebugName = gameObject.name;
            if (TryGetComponent(out EntityBrain brain)) Brain = brain;
            if (_persistentID == null) _persistentID = GetComponent<PersistentID>();
            if (_selfFactionMember == null) _selfFactionMember = GetComponent<FactionMember>();

            if (_config == null)
            {
                GameLog.Error(TAG, $"{gameObject.name}: StealthConfigSO not assigned — EntityPerception disabled");
                enabled = false;
                return;
            }
            if (Entity == null)
            {
                GameLog.Error(TAG, $"{gameObject.name}: Entity SO not found (PersistentID) — EntityPerception disabled");
                enabled = false;
                return;
            }
            if (_selfFactionMember == null && (Entity.DetectionRange > 0f || CanWitness))
            {
                GameLog.Error(TAG, $"{gameObject.name}: FactionMember not found — EntityPerception disabled");
                enabled = false;
            }
        }

        private void OnEnable() => _active.Add(this);

        private void OnDisable() => _active.Remove(this);

        /// <summary>
        /// Advances perception by <paramref name="dt"/>. <paramref name="engaged"/> = the brain is alerted
        /// (Warning / Engaging / Attacking, or Watching for a witness): the target is tracked 360° within
        /// DisengageRange (WitnessWatchRange when witnessing) and awareness stays at 1.
        /// </summary>
        public void Tick(float dt, bool engaged)
        {
            if (!IsActive) return;
            Entity entity = Entity;

            _scanTimer -= dt;
            if (!(engaged && HasLiveTarget()) && _scanTimer <= 0f)
            {
                _scanTimer = _config.targetScanInterval;
                ScanForTarget(entity);
            }
            if (Target != null && !HasLiveTarget()) { Target = null; IsWitnessing = false; }
            bool witness = IsWitnessing;

            if (Target == null)
            {
                ClearTargetReadouts();
                if (engaged) Awareness = 1f;
                else Awareness = StealthDetection.StepAwareness(Awareness, 0f, _config.awarenessDrainPerSecond, false, dt);
                TimeSinceSeen += dt;
                return;
            }

            IStealthTarget stealth = Target.StealthTarget;
            Vector3 eye = EyePosition;
            Vector3 toTarget = Target.Transform.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            DistanceToTarget = distance;
            TargetIsSneaking = stealth.IsSneaking;

            _losTimer -= dt;
            if (_losTimer <= 0f)
            {
                _losTimer = _config.losCheckInterval;
                _hasLineOfSight = CheckLineOfSight(eye, stealth.VisibilityPoint);
            }

            // A witness only ever sees a sneaking target: no sneak multiplier on its range, and its proximity radius
            // is always the sneaking one.
            CurrentSightRange = witness
                ? WitnessRange
                : StealthDetection.EffectiveSightRange(entity.DetectionRange, TargetIsSneaking, _config.sneakSightRangeMultiplier);
            CurrentProximityRadius = StealthDetection.EffectiveProximityRadius(
                entity.ProximityRadius, witness || TargetIsSneaking, _config.sneakProximityMultiplier);

            if (engaged)
            {
                // A watching witness keeps tracking even if the player stands up.
                CanSeeTarget = _hasLineOfSight && distance <= (witness ? WitnessWatchRange : entity.DisengageRange);
                LastVisibility = CanSeeTarget ? 1f : 0f;
                LastFillPerSecond = 0f;
                Awareness = 1f;
            }
            else
            {
                float angle = StealthDetection.AngleToTarget(transform.forward, toTarget);
                float visibility = !_hasLineOfSight ? 0f
                    : witness
                        ? StealthDetection.ComputeWitnessVisibility(TargetIsSneaking, distance, angle, CurrentSightRange,
                            entity.ViewAngle, CurrentProximityRadius, _config.edgeDistanceFactor, _config.peripheralAngleFactor)
                        : StealthDetection.ComputeVisibility(distance, angle, CurrentSightRange, entity.ViewAngle,
                            CurrentProximityRadius, _config.edgeDistanceFactor, _config.peripheralAngleFactor);
                float fill = StealthDetection.FillPerSecond(
                    visibility, entity.AwarenessFillTime, TargetIsSneaking, _config.sneakFillRateMultiplier);
                CanSeeTarget = visibility > 0f;
                LastVisibility = visibility;
                LastFillPerSecond = CanSeeTarget ? fill : -_config.awarenessDrainPerSecond;
                Awareness = StealthDetection.StepAwareness(
                    Awareness, fill, _config.awarenessDrainPerSecond, CanSeeTarget, dt);
            }

            if (CanSeeTarget)
            {
                LastSeenPosition = Target.Transform.position;
                HasLastSeenPosition = true;
                TimeSinceSeen = 0f;
            }
            else
            {
                TimeSinceSeen += dt;
            }
        }

        // Hostile stealth targets win; otherwise a non-hostile one is witnessed (profile enabled).
        private void ScanForTarget(Entity entity)
        {
            FactionSO faction = _selfFactionMember.Faction;
            FactionMember found = null;
            bool witnessing = false;
            if (faction != null)
            {
                if (entity.DetectionRange > 0f)
                    found = TargetRegistry.FindClosestHostileStealthTarget(faction, transform.position,
                        Mathf.Max(entity.DetectionRange, entity.DisengageRange));
                if (found == null && CanWitness)
                {
                    found = TargetRegistry.FindClosestNonHostileStealthTarget(faction, transform.position,
                        Mathf.Max(WitnessRange, WitnessWatchRange));
                    witnessing = found != null;
                }
            }
            if (found != Target || witnessing != IsWitnessing)
            {
                // Switched to another target or mode: awareness belongs to the previous one. Losing the target
                // (found == null) keeps the meter so it drains gradually.
                if (Target != null && found != null) Awareness = 0f;
                Target = found;
                IsWitnessing = witnessing;
                _losTimer = 0f; // fresh target → fresh line-of-sight check this tick
            }
        }

        /// <summary>Instantly fully aware of a hostile <paramref name="target"/> (e.g. after being hit).</summary>
        public void ForceAware(FactionMember target)
        {
            if (target == null) return;
            if (Target != target) _losTimer = 0f;
            Target = target;
            IsWitnessing = false;
            Awareness = 1f;
            LastSeenPosition = target.Transform.position;
            HasLastSeenPosition = true;
            TimeSinceSeen = 0f;
        }

        public void SetAwareness(float value) => Awareness = Mathf.Clamp01(value);

        public void ResetPerception()
        {
            Awareness = 0f;
            Target = null;
            HasLastSeenPosition = false;
            TimeSinceSeen = 0f;
            ClearTargetReadouts();
        }

        // No target: drop the per-target read-outs so the debug views don't show stale values.
        private void ClearTargetReadouts()
        {
            IsWitnessing = false;
            CanSeeTarget = false;
            _hasLineOfSight = false;
            LastVisibility = 0f;
            LastFillPerSecond = 0f;
            DistanceToTarget = 0f;
            TargetIsSneaking = false;
            CurrentSightRange = SightRange;
            CurrentProximityRadius = ProximityRadius;
        }

        private bool HasLiveTarget() =>
            Target != null && Target.Damageable != null && (Object)Target.Damageable != null && !Target.Damageable.IsDead;

        private bool CheckLineOfSight(Vector3 eye, Vector3 point)
        {
            Vector3 dir = point - eye;
            float dist = dir.magnitude;
            if (dist < 0.001f) return true;
            return !Physics.Raycast(eye, dir / dist, dist, _config.lineOfSightMask, QueryTriggerInteraction.Ignore);
        }

#if UNITY_EDITOR
        // --- Detection gizmos (selected entity, or all via Tools/Stealth/Show Detection Gizmos) ---

        private const int GIZMO_ARC_SEGMENTS = 24;
        private const float GIZMO_LAST_SEEN_RADIUS = 0.25f;
        private const float GIZMO_LABEL_HEIGHT = 0.6f;
        private static readonly Vector3[] _gizmoBuffer = new Vector3[(GIZMO_ARC_SEGMENTS + 2) * 2];

        private void OnDrawGizmos()
        {
            if (StealthDebug.DrawAllGizmos) DrawDetectionGizmos();
        }

        private void OnDrawGizmosSelected()
        {
            if (!StealthDebug.DrawAllGizmos) DrawDetectionGizmos();
        }

        private void DrawDetectionGizmos()
        {
            PersistentID id = _persistentID != null ? _persistentID : GetComponent<PersistentID>();
            Entity entity = id != null ? id.Entity : null;
            if (entity == null) return;
            WitnessProfileSO profile = entity.WitnessProfile;
            bool canWitness = WitnessProfileSO.IsEnabled(profile);
            if (entity.DetectionRange <= 0f && !canWitness) return;

            bool playing = Application.isPlaying && IsActive;
            float sneakSight = _config != null ? entity.DetectionRange * _config.sneakSightRangeMultiplier : entity.DetectionRange;
            float sneakProx = _config != null ? entity.ProximityRadius * _config.sneakProximityMultiplier : entity.ProximityRadius;
            float threshold = _config != null ? _config.suspicionThreshold : 0.5f;
            Color color = StealthDebugGeometry.AwarenessColor(playing ? Awareness : 0f, threshold);
            Color dim = new Color(color.r, color.g, color.b, 0.35f);
            Vector3 eye = transform.position + Vector3.up * entity.EyeHeight;
            Vector3 ground = transform.position + Vector3.up * 0.05f;

            if (entity.DetectionRange > 0f)
            {
                DrawSegments(StealthDebugGeometry.BuildConeOutline(
                    eye, transform.forward, entity.DetectionRange, entity.ViewAngle, GIZMO_ARC_SEGMENTS, _gizmoBuffer), color);
                DrawSegments(StealthDebugGeometry.BuildConeOutline(
                    eye, transform.forward, sneakSight, entity.ViewAngle, GIZMO_ARC_SEGMENTS, _gizmoBuffer), dim);
                DrawSegments(StealthDebugGeometry.BuildCircle(ground, entity.ProximityRadius, GIZMO_ARC_SEGMENTS, _gizmoBuffer), color);
            }
            DrawSegments(StealthDebugGeometry.BuildCircle(ground, sneakProx, GIZMO_ARC_SEGMENTS, _gizmoBuffer), dim);
            if (canWitness)
            {
                // Witness cone (non-hostile sneaking player): fixed colour, solid while witnessing.
                Color witnessColor = playing && IsWitnessing ? color : StealthDebugGeometry.WitnessConeColor;
                DrawSegments(StealthDebugGeometry.BuildConeOutline(
                    eye, transform.forward, profile.WitnessRange, entity.ViewAngle, GIZMO_ARC_SEGMENTS, _gizmoBuffer), witnessColor);
            }

            if (!playing) return;

            if (Target != null && Target.StealthTarget != null)
            {
                Gizmos.color = _hasLineOfSight ? Color.green : Color.red;
                Gizmos.DrawLine(eye, Target.StealthTarget.VisibilityPoint);
            }
            if (HasLastSeenPosition && TimeSinceSeen > 0f)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(LastSeenPosition, GIZMO_LAST_SEEN_RADIUS);
            }

            string state = Brain != null ? Brain.DebugStateName : "—";
            string seen = HasLastSeenPosition ? $"{TimeSinceSeen:0.0} s ago" : "never";
            string target = Target == null ? "none" : (TargetIsSneaking ? "sneaking" : "standing");
            UnityEditor.Handles.Label(eye + Vector3.up * GIZMO_LABEL_HEIGHT,
                $"{(IsWitnessing ? "Witness · " : "")}{state}\nAwareness {Awareness * 100f:0}%  Vis {LastVisibility:0.00}  {LastFillPerSecond:+0.00;-0.00}/s\n" +
                $"Dist {DistanceToTarget:0.0} m  LOS {(_hasLineOfSight ? "yes" : "no")}  Seen {seen}\nTarget: {target}");
        }

        private static void DrawSegments(int count, Color color)
        {
            Gizmos.color = color;
            for (int i = 0; i + 1 < count; i += 2)
                Gizmos.DrawLine(_gizmoBuffer[i], _gizmoBuffer[i + 1]);
        }
#endif
    }
}
