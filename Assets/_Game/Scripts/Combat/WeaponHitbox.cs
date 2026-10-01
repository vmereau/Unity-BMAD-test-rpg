using Game.Core;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Owner-agnostic, attacker-side swept hitbox. Placed on a weapon's <c>Drawn</c> child (or a hand
    /// hitbox GO). The Box/Sphere/Capsule collider on the same GO is a <b>shape definition only</b> —
    /// it is kept disabled and never takes part in physics callbacks.
    /// While the hit window is open (Enable → Disable, driven by HitboxEnable/HitboxDisable animation
    /// events), every LateUpdate samples the shape between last frame's pose and this frame's pose with
    /// non-alloc overlap queries against an explicit LayerMask (default: CharacterHitbox), plus a
    /// downward reach capsule so swings catch low targets. No Rigidbody or collision-matrix dependency.
    /// Targets resolve as <see cref="IDamageable"/>; colliders inside <see cref="Owner"/> are skipped;
    /// each target is reported at most once per window via <see cref="OnHit"/>.
    /// The controlling component (PlayerCombat today, EntityBrain later) must call <see cref="SetOwner"/>.
    /// </summary>
    public class WeaponHitbox : MonoBehaviour
    {
        private const string TAG = "[Combat]";
        private const int MAX_OVERLAP_RESULTS = 16;

        [Tooltip("Layers treated as hurtboxes. Empty = CharacterHitbox.")]
        [SerializeField] private LayerMask _targetLayers;
        [Tooltip("Inflates the query shape on every side (forgiving reach), metres.")]
        [SerializeField, Min(0f)] private float _reachPadding = 0.1f;
        [Tooltip("Extra downward world-space reach from the shape centre so swings catch low targets, metres. 0 = off.")]
        [SerializeField, Min(0f)] private float _verticalReach = 0.6f;
        [Tooltip("Radius of the downward reach capsule, metres.")]
        [SerializeField, Min(0.01f)] private float _verticalReachRadius = 0.25f;
        [Tooltip("Max distance between swept samples within one frame, metres.")]
        [SerializeField, Min(0.01f)] private float _maxStepDistance = 0.1f;
        [Tooltip("Upper bound on samples per frame.")]
        [SerializeField, Min(1)] private int _maxSubSteps = 8;

        /// <summary>Raised once per target per hit window: (target, approximate hit point).</summary>
        public event System.Action<IDamageable, Vector3> OnHit;

        /// <summary>Root of the attacker's hierarchy — its colliders are never hit.</summary>
        public Transform Owner { get; private set; }

        private readonly HitSweepTracker _tracker = new();
        private readonly Collider[] _overlapBuffer = new Collider[MAX_OVERLAP_RESULTS];
        private Collider _shape;              // Box/Sphere/Capsule on this GO — shape definition only
        private IDamageable _ownerDamageable;
        private Vector3 _prevPosition;
        private Quaternion _prevRotation;
        private bool _saturationWarned;

        private void Awake()
        {
            _shape = GetComponent<Collider>();
            if (!IsSupportedShape(_shape))
            {
                GameLog.Error(TAG, $"WeaponHitbox on '{name}' needs a Box/Sphere/Capsule collider on the same GameObject — sweeps disabled");
                _shape = null;
            }
            else
            {
                _shape.enabled = false; // shape-only, never a physics participant
            }

            if (_targetLayers == 0)
                _targetLayers = LayerMask.GetMask(GameConstants.CHARACTER_HITBOX_LAYER_NAME);

            Disable(); // Always dormant until the attack window opens
        }

        /// <summary>Sets the attacker root whose hierarchy (and IDamageable) is excluded from hits.</summary>
        public void SetOwner(Transform owner)
        {
            Owner = owner;
            _ownerDamageable = owner != null ? owner.GetComponent<IDamageable>() : null;
        }

        /// <summary>Opens the hit window — call at the start of an attack's active frames.</summary>
        public void Enable()
        {
            // Duplicate HitboxEnable (e.g. overlapping crossfade events) must not reset the dedupe set
            // mid-swing. The next attack is safe: OnAttackStateEntered always calls Disable() first.
            if (_tracker.IsWindowOpen) return;
            if (Owner == null)
                GameLog.Warn(TAG, $"WeaponHitbox '{name}' enabled with no owner — self-hits not filtered");
            _tracker.BeginWindow();
            _saturationWarned = false;
            _prevPosition = transform.position;
            _prevRotation = transform.rotation;
        }

        /// <summary>Closes the hit window — call at the end of an attack window and on every interrupt.</summary>
        public void Disable()
        {
            _tracker.EndWindow();
        }

        // LateUpdate: the Animator has already posed the hand bone this frame.
        private void LateUpdate()
        {
            if (!_tracker.IsWindowOpen || _shape == null) return;

            Vector3 currentPosition = transform.position;
            Quaternion currentRotation = transform.rotation;
            int steps = HitSweepTracker.ComputeSubSteps(
                Vector3.Distance(_prevPosition, currentPosition), _maxStepDistance, _maxSubSteps);

            for (int i = 1; i <= steps; i++)
            {
                // A hit handler may close the window (e.g. attacker interrupted) — stop sampling.
                if (!_tracker.IsWindowOpen) break;
                float t = (float)i / steps;
                SampleAt(Vector3.Lerp(_prevPosition, currentPosition, t),
                         Quaternion.Slerp(_prevRotation, currentRotation, t));
            }

            _prevPosition = currentPosition;
            _prevRotation = currentRotation;
        }

        private void SampleAt(Vector3 pos, Quaternion rot)
        {
            Vector3 lossyScale = transform.lossyScale;
            Vector3 center = ComputeShapeCenter(_shape, pos, rot, lossyScale);
            int count;

            switch (_shape)
            {
                case BoxCollider box:
                    count = Physics.OverlapBoxNonAlloc(center,
                        HitSweepTracker.ComputeBoxHalfExtents(box.size, lossyScale, _reachPadding),
                        _overlapBuffer, rot, _targetLayers, QueryTriggerInteraction.Collide);
                    break;
                case SphereCollider sphere:
                    count = Physics.OverlapSphereNonAlloc(center,
                        HitSweepTracker.ComputeScaledRadius(sphere.radius, lossyScale, _reachPadding),
                        _overlapBuffer, _targetLayers, QueryTriggerInteraction.Collide);
                    break;
                case CapsuleCollider capsule:
                {
                    Vector3 axis = capsule.direction switch
                    {
                        0 => Vector3.right,
                        1 => Vector3.up,
                        _ => Vector3.forward,
                    };
                    float scaleAlongAxis = Mathf.Abs(capsule.direction switch
                    {
                        0 => lossyScale.x,
                        1 => lossyScale.y,
                        _ => lossyScale.z,
                    });
                    float scaledRadius = HitSweepTracker.ComputeScaledRadius(capsule.radius, lossyScale, 0f);
                    float halfSegment = Mathf.Max(0f, capsule.height * scaleAlongAxis * 0.5f - scaledRadius);
                    Vector3 offset = rot * axis * halfSegment;
                    count = Physics.OverlapCapsuleNonAlloc(center - offset, center + offset,
                        scaledRadius + _reachPadding, _overlapBuffer, _targetLayers, QueryTriggerInteraction.Collide);
                    break;
                }
                default:
                    return;
            }

            ProcessOverlaps(count, center);

            if (_verticalReach > 0f)
            {
                count = Physics.OverlapCapsuleNonAlloc(center, center + Vector3.down * _verticalReach,
                    _verticalReachRadius, _overlapBuffer, _targetLayers, QueryTriggerInteraction.Collide);
                ProcessOverlaps(count, center);
            }
        }

        private void ProcessOverlaps(int count, Vector3 queryCenter)
        {
            if (count >= MAX_OVERLAP_RESULTS && !_saturationWarned)
            {
                _saturationWarned = true;
                GameLog.Warn(TAG, $"WeaponHitbox '{name}' overlap buffer saturated ({MAX_OVERLAP_RESULTS}) — some targets may be missed");
            }

            for (int i = 0; i < count; i++)
            {
                if (!_tracker.IsWindowOpen) return;
                Collider col = _overlapBuffer[i];
                if (col == null) continue;

                // Self-hit exclusion by owner hierarchy. Future: faction filter goes here, next to the owner check.
                if (Owner != null && col.transform.IsChildOf(Owner)) continue;

                var target = col.GetComponentInParent<IDamageable>();
                if (!_tracker.TryRegisterHit(target, _ownerDamageable)) continue;

                OnHit?.Invoke(target, ClosestPointOn(col, queryCenter));
            }
        }

        private static Vector3 ClosestPointOn(Collider col, Vector3 point)
        {
            // Collider.ClosestPoint is unsupported on non-convex MeshColliders — fall back to bounds.
            if (col is MeshCollider mesh && !mesh.convex) return col.bounds.ClosestPoint(point);
            return col.ClosestPoint(point);
        }

        private static bool IsSupportedShape(Collider col) =>
            col is BoxCollider || col is SphereCollider || col is CapsuleCollider;

        private static Vector3 ComputeShapeCenter(Collider shape, Vector3 pos, Quaternion rot, Vector3 lossyScale)
        {
            Vector3 localCenter = shape switch
            {
                BoxCollider box => box.center,
                SphereCollider sphere => sphere.center,
                CapsuleCollider capsule => capsule.center,
                _ => Vector3.zero,
            };
            return pos + rot * Vector3.Scale(localCenter, lossyScale);
        }

        private void OnDrawGizmosSelected()
        {
            if (_verticalReach <= 0f) return;
            Collider shape = _shape != null ? _shape : GetComponent<Collider>();
            if (!IsSupportedShape(shape)) return;

            Vector3 top = ComputeShapeCenter(shape, transform.position, transform.rotation, transform.lossyScale);
            Vector3 bottom = top + Vector3.down * _verticalReach;
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
            Gizmos.DrawWireSphere(top, _verticalReachRadius);
            Gizmos.DrawWireSphere(bottom, _verticalReachRadius);
            Gizmos.DrawLine(top, bottom);
        }
    }
}
