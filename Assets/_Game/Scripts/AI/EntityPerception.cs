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
    /// <b>Trespass</b> mode sits between the two: a non-hostile stealth target standing inside a
    /// <see cref="RestrictedArea"/> this entity owns (restricted now) is perceived with the hostile formula (walking or
    /// sneaking, sneak-shortened) at <see cref="TrespassSightRange"/> (<see cref="IsTrespassing"/>,
    /// <see cref="TrespassArea"/>). Priority: hostile &gt; trespass &gt; witness.
    /// <b>Hearing</b> (hostile and trespass modes, never witness): a target whose <see cref="IStealthTarget.Noise"/> is
    /// Walk / Run inside the Entity's walk / run hearing radius (360°, × occludedHearingFactor without line of sight)
    /// fills the same meter at 1 / hearingFillTime. <see cref="CanSeeTarget"/> stays vision-only; the last seen
    /// position and <see cref="TimeSinceSeen"/> track the last time the target was <i>sensed</i> (seen or heard).
    /// Inactive (no-op) when DetectionRange ≤ 0 and no witness profile is enabled.
    /// </summary>
    public class EntityPerception : MonoBehaviour
    {
        private const string TAG = "[AI]";

        [SerializeField] private PersistentID _persistentID;
        [SerializeField] private StealthConfigSO _config;

        private FactionMember _selfFactionMember; // same-GO FactionMember, resolved in Awake

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
        /// <summary>The current <see cref="Target"/> is non-hostile but inside a restricted area this entity owns.</summary>
        public bool IsTrespassing { get; private set; }
        /// <summary>Area the current target trespasses in (null unless <see cref="IsTrespassing"/>).</summary>
        public RestrictedArea TrespassArea { get; private set; }
        public bool IsActive => enabled && _config != null && Entity != null && (Entity.DetectionRange > 0f || CanWitness);
        public StealthConfigSO Config => _config;

        public float Awareness { get; private set; }
        public bool IsFullyAware => Awareness >= 1f;
        public bool IsSuspicious => _config != null && Awareness > 0f && Awareness >= _config.suspicionThreshold;

        /// <summary>Current stealth-target candidate (closest hostile stealth target in range, else — witness — the closest non-hostile one), or null.</summary>
        public FactionMember Target { get; private set; }
        /// <summary>Vision only — hearing never sets it (see <see cref="CanHearTarget"/>).</summary>
        public bool CanSeeTarget { get; private set; }
        /// <summary>The target's movement noise is inside the current hearing radius (hostile / trespass modes).</summary>
        public bool CanHearTarget { get; private set; }
        /// <summary>Last position the target was seen <b>or heard</b> at.</summary>
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
        public MovementNoise TargetNoise { get; private set; }
        /// <summary>Hearing radius in use against the target's current noise (0 when silent or witnessing).</summary>
        public float CurrentHearingRadius { get; private set; }
        /// <summary>Hearing never reaches past the standing sight range (DetectionRange, or TheftSightRange for a
        /// passive owner): a target heard beyond it would enter Warning and be cancelled at once (dist &gt; DetectionRange).</summary>
        private float HearingCap => TrespassSightRange;
        /// <summary>Walk hearing radius in use (Entity value capped at the sight range), before the occlusion factor.</summary>
        public float WalkHearingRadius => Entity != null ? Mathf.Min(Entity.WalkHearingRadius, HearingCap) : 0f;
        /// <summary>Run hearing radius in use (Entity value capped at the sight range), before the occlusion factor.</summary>
        public float RunHearingRadius => Entity != null ? Mathf.Min(Entity.RunHearingRadius, HearingCap) : 0f;
        /// <summary>Hearing can apply at all: hostile detection, or trespass detection in owned areas (witness-only never hears).</summary>
        public bool CanHear => HearingCap > 0f;
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
        /// <summary>Standing sight range against a trespasser: DetectionRange, or the witness profile's TheftSightRange
        /// for a passive owner (DetectionRange 0).</summary>
        public float TrespassSightRange
        {
            get
            {
                if (Entity == null) return 0f;
                if (Entity.DetectionRange > 0f) return Entity.DetectionRange;
                return CanWitness ? WitnessProfile.TheftSightRange : 0f;
            }
        }
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
            _selfFactionMember = GetComponent<FactionMember>();

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
            if (Target != null && !HasLiveTarget()) { Target = null; IsWitnessing = false; IsTrespassing = false; TrespassArea = null; }
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
            TargetNoise = stealth.Noise;

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
                : StealthDetection.EffectiveSightRange(IsTrespassing ? TrespassSightRange : entity.DetectionRange,
                    TargetIsSneaking, _config.sneakSightRangeMultiplier);
            CurrentProximityRadius = StealthDetection.EffectiveProximityRadius(
                entity.ProximityRadius, witness || TargetIsSneaking, _config.sneakProximityMultiplier);
            // Hearing: hostile and trespass modes only (a witness only cares about a sneaking, silent target).
            CurrentHearingRadius = witness ? 0f
                : StealthDetection.HearingRadius(TargetNoise, WalkHearingRadius, RunHearingRadius,
                    _hasLineOfSight, _config.occludedHearingFactor);
            CanHearTarget = StealthDetection.IsHeard(distance, CurrentHearingRadius);

            if (engaged)
            {
                // A watching witness keeps tracking even if the player stands up.
                CanSeeTarget = _hasLineOfSight && distance <= (witness ? WitnessWatchRange : entity.DisengageRange);
                // Hearing never holds a target past the disengage range (a run radius above it would never lose it).
                if (distance > entity.DisengageRange) CanHearTarget = false;
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
                float fill = Mathf.Max(
                    StealthDetection.FillPerSecond(visibility, entity.AwarenessFillTime, TargetIsSneaking,
                        _config.sneakFillRateMultiplier),
                    StealthDetection.HearingFillPerSecond(CanHearTarget, _config.hearingFillTime));
                CanSeeTarget = visibility > 0f;
                bool sensed = CanSeeTarget || CanHearTarget;
                LastVisibility = visibility;
                LastFillPerSecond = sensed ? fill : -_config.awarenessDrainPerSecond;
                Awareness = StealthDetection.StepAwareness(
                    Awareness, fill, _config.awarenessDrainPerSecond, sensed, dt);
            }

            // Seen or heard: the brain turns to / chases / searches this point, and does not lose the target.
            if (CanSeeTarget || CanHearTarget)
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

        // Hostile stealth targets win; then a non-hostile one trespassing in an area this entity owns; otherwise a
        // non-hostile one is witnessed (profile enabled).
        private void ScanForTarget(Entity entity)
        {
            FactionSO faction = _selfFactionMember.Faction;
            FactionMember found = null;
            bool witnessing = false;
            bool trespassing = false;
            RestrictedArea area = null;
            if (faction != null)
            {
                if (entity.DetectionRange > 0f)
                    found = TargetRegistry.FindClosestHostileStealthTarget(faction, transform.position,
                        Mathf.Max(entity.DetectionRange, entity.DisengageRange));
                float trespassRange = TrespassSightRange;
                if (found == null && trespassRange > 0f)
                {
                    FactionMember candidate = TargetRegistry.FindClosestNonHostileStealthTarget(faction,
                        transform.position, Mathf.Max(trespassRange, entity.DisengageRange));
                    if (candidate != null && RestrictedArea.TryFindTrespassedArea(candidate.Transform.position,
                            entity as NPCEntity, faction, out area))
                    {
                        found = candidate;
                        trespassing = true;
                    }
                }
                if (found == null && CanWitness)
                {
                    found = TargetRegistry.FindClosestNonHostileStealthTarget(faction, transform.position,
                        Mathf.Max(WitnessRange, WitnessWatchRange));
                    witnessing = found != null;
                }
            }
            TrespassArea = area;
            if (found != Target || witnessing != IsWitnessing || trespassing != IsTrespassing)
            {
                // Switched to another target or mode: awareness belongs to the previous one. Losing the target
                // (found == null) keeps the meter so it drains gradually.
                if (Target != null && found != null) Awareness = 0f;
                Target = found;
                IsWitnessing = witnessing;
                IsTrespassing = trespassing;
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
            IsTrespassing = false;
            TrespassArea = null;
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
            IsTrespassing = false;
            TrespassArea = null;
            CanSeeTarget = false;
            CanHearTarget = false;
            CurrentHearingRadius = 0f;
            TargetNoise = MovementNoise.Silent;
            _hasLineOfSight = false;
            LastVisibility = 0f;
            LastFillPerSecond = 0f;
            DistanceToTarget = 0f;
            TargetIsSneaking = false;
            CurrentSightRange = SightRange;
            CurrentProximityRadius = ProximityRadius;
        }

        /// <summary>Immediate line-of-sight raycast from the eye to <paramref name="point"/> (blocked by lineOfSightMask).</summary>
        public bool HasLineOfSightTo(Vector3 point) => _config != null && CheckLineOfSight(EyePosition, point);

        /// <summary>Point a witness aims its line-of-sight ray at on <paramref name="target"/> (its visibility point if it has one).</summary>
        public Vector3 GetVisibilityPoint(FactionMember target) =>
            target.StealthTarget != null
                ? target.StealthTarget.VisibilityPoint
                : target.Transform.position + Vector3.up * (_config != null ? _config.standingVisibilityHeight : 0f);

        /// <summary>
        /// True when this witness sees <paramref name="thief"/> right now: inside the view cone at the profile's
        /// TheftSightRange (sneak-shortened) or the (sneak-adjusted) proximity radius, with clear line of sight.
        /// </summary>
        public bool CanSeeTheft(FactionMember thief)
        {
            if (!IsActive || !CanWitness || thief == null) return false;
            WitnessProfileSO profile = WitnessProfile;
            if (profile.TheftSightRange <= 0f) return false;

            Entity entity = Entity;
            bool sneaking = thief.StealthTarget != null && thief.StealthTarget.IsSneaking;
            Vector3 point = GetVisibilityPoint(thief);
            Vector3 toTarget = thief.Transform.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            float angle = StealthDetection.AngleToTarget(transform.forward, toTarget);
            float range = TheftDetection.EffectiveTheftRange(profile.TheftSightRange, sneaking, _config.sneakSightRangeMultiplier);
            float proximity = StealthDetection.EffectiveProximityRadius(entity.ProximityRadius, sneaking, _config.sneakProximityMultiplier);
            return TheftDetection.IsInView(distance, angle, range, entity.ViewAngle, proximity) && HasLineOfSightTo(point);
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
            // Hearing circles (walk solid, run dimmed): awareness colour while the target is heard.
            // Only entities that can hear (hostile detection, or a trespass range); radii are the unoccluded ones.
            float hearingCap = entity.DetectionRange > 0f ? entity.DetectionRange
                : canWitness ? profile.TheftSightRange : 0f;
            bool canHear = hearingCap > 0f;
            float walkHearing = Mathf.Min(entity.WalkHearingRadius, hearingCap);
            float runHearing = Mathf.Min(entity.RunHearingRadius, hearingCap);
            Color hearing = playing && CanHearTarget ? color : StealthDebugGeometry.HearingCircleColor;
            Color hearingDim = new Color(hearing.r, hearing.g, hearing.b, hearing.a * 0.6f);
            if (canHear && walkHearing > 0f)
                DrawSegments(StealthDebugGeometry.BuildCircle(ground, walkHearing, GIZMO_ARC_SEGMENTS, _gizmoBuffer), hearing);
            if (canHear && runHearing > 0f)
                DrawSegments(StealthDebugGeometry.BuildCircle(ground, runHearing, GIZMO_ARC_SEGMENTS, _gizmoBuffer), hearingDim);
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
                $"{(IsWitnessing ? "Witness · " : IsTrespassing ? "Trespass · " : "")}{state}\nAwareness {Awareness * 100f:0}%  Vis {LastVisibility:0.00}  {LastFillPerSecond:+0.00;-0.00}/s\n" +
                $"Dist {DistanceToTarget:0.0} m  LOS {(_hasLineOfSight ? "yes" : "no")}  Seen {seen}\nTarget: {target}, noise {TargetNoise}" +
                (CanHearTarget ? $"  Heard ({CurrentHearingRadius:0.0} m)" : ""));
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
