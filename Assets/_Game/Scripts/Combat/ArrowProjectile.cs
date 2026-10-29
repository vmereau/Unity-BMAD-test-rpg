using System;
using Game.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Combat
{
    /// <summary>
    /// Arrow projectile (no Rigidbody): flies straight for its gravity delay, then gravity eases in
    /// (<see cref="ArrowFlightMath.GravityFactor"/>) and the arrow tilts along its velocity. Each frame it sphere-casts
    /// this frame's displacement:
    /// a collider resolving to a living <see cref="IDamageable"/> → <c>onHit</c> callback, projectile destroyed;
    /// any other solid collider → the projectile is replaced by its stuck prefab (a kinematic pickable item) at
    /// the impact pose, in the hit collider's scene (region, so save / load and region unloads own it). Triggers
    /// count only on the CharacterHitbox layer (hurtboxes) — perception / interaction zones are ignored.
    /// Destroyed after its lifetime.
    /// The prefab root is the arrow TIP, with the shaft pointing back along -Z (flight direction = +Z).
    /// </summary>
    public class ArrowProjectile : MonoBehaviour
    {
        private const string TAG = "[Arrow]";
        private const int HIT_BUFFER_SIZE = 32;
        // Safety net for an instance never launched (spawner failed) — Launch() overrides it.
        private const float UNLAUNCHED_LIFETIME = 10f;
        private const float MIN_STEP_DISTANCE = 1e-5f;

        [Tooltip("Sweep radius — small forgiveness around the tip.")]
        [SerializeField, Min(0.001f)] private float _radius = 0.05f;
        [Tooltip("Layers the arrow collides with. Empty = Default + CharacterHitbox.")]
        [SerializeField] private LayerMask _hitLayers;
        [Tooltip("How deep the tip sinks into world geometry when it sticks.")]
        [SerializeField, Min(0f)] private float _stickDepth = 0.08f;

        private readonly RaycastHit[] _hitBuffer = new RaycastHit[HIT_BUFFER_SIZE];

        private Transform _owner;
        private Vector3 _velocity;
        private Vector3 _gravity;         // full gravity (Physics.gravity × scale), eased in after the delay
        private float _gravityDelay;
        private float _gravityRampTime;
        private float _launchTime;
        private float _expireTime;
        private Action<IDamageable, Vector3> _onHit;
        private GameObject _stuckPrefab;
        private bool _launched;

        private int _hurtboxLayer;

        private void Awake()
        {
            if (_hitLayers == 0)
                _hitLayers = LayerMask.GetMask(GameConstants.DEFAULT_LAYER_NAME, GameConstants.CHARACTER_HITBOX_LAYER_NAME);
            _hurtboxLayer = LayerMask.NameToLayer(GameConstants.CHARACTER_HITBOX_LAYER_NAME);
            _expireTime = Time.time + UNLAUNCHED_LIFETIME;
        }

        /// <param name="owner">Shooter — its colliders and its own IDamageable are ignored.</param>
        /// <param name="onHit">Hit resolution (TryReceiveHit → TakeDamage), owned by the shooter.</param>
        /// <param name="stuckPrefab">Spawned where the arrow sticks in the world (null = just destroyed).</param>
        /// <param name="gravityScale">Share of <c>Physics.gravity</c> once fully ramped in (0 = straight line).</param>
        /// <param name="gravityDelay">Seconds of dead-straight flight before gravity starts.</param>
        /// <param name="gravityRampTime">Seconds for gravity to ramp from 0 to full after the delay.</param>
        public void Launch(Transform owner, Vector3 direction, float speed, float lifetime,
            Action<IDamageable, Vector3> onHit, GameObject stuckPrefab,
            float gravityScale, float gravityDelay, float gravityRampTime)
        {
            _owner = owner;
            _velocity = direction.normalized * speed;
            _gravity = Physics.gravity * Mathf.Max(0f, gravityScale);
            _gravityDelay = gravityDelay;
            _gravityRampTime = gravityRampTime;
            _launchTime = Time.time;
            _expireTime = Time.time + lifetime;
            _onHit = onHit;
            _stuckPrefab = stuckPrefab;
            transform.rotation = Quaternion.LookRotation(_velocity);
            _launched = true;
        }

        private void Update()
        {
            if (Time.time >= _expireTime)
            {
                Destroy(gameObject);
                return;
            }
            if (!_launched) return;

            float dt = Time.deltaTime;
            // Gravity sampled mid-step and applied as v·dt + ½·a·dt², so the drop barely depends on the frame rate.
            float midTime = Time.time - _launchTime - dt * 0.5f;
            Vector3 acceleration = _gravity * ArrowFlightMath.GravityFactor(midTime, _gravityDelay, _gravityRampTime);
            Vector3 displacement = _velocity * dt + acceleration * (0.5f * dt * dt);
            _velocity += acceleration * dt;
            float distance = displacement.magnitude;
            if (distance < MIN_STEP_DISTANCE) return; // paused (dt 0) — nothing to sweep
            Vector3 direction = displacement / distance;
            transform.rotation = Quaternion.LookRotation(direction);

            Vector3 origin = transform.position;
            int count = Physics.SphereCastNonAlloc(origin, _radius, direction, _hitBuffer, distance,
                _hitLayers, QueryTriggerInteraction.Collide);

            if (TryFindFirstHit(count, out RaycastHit hit, out IDamageable target))
            {
                if (target != null)
                {
                    HitTarget(target, hit);
                    return;
                }
                Stick(hit, origin, direction);
                return;
            }

            transform.position = origin + displacement;
        }

        // Closest valid hit: skips the owner, dead targets and non-hurtbox triggers (zones). Results are unsorted.
        private bool TryFindFirstHit(int count, out RaycastHit best, out IDamageable bestTarget)
        {
            best = default;
            bestTarget = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = _hitBuffer[i];
                Collider col = candidate.collider;
                if (col == null) continue;
                if (_owner != null && col.transform.IsChildOf(_owner)) continue;

                // Triggers: only hurtboxes count. An NPC's perception / interaction sphere must not take the hit.
                if (col.isTrigger && col.gameObject.layer != _hurtboxLayer) continue;

                IDamageable damageable = col.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    if (damageable.IsDead) continue;
                    if (_owner != null && damageable is Component c && c.transform == _owner) continue;
                }

                // A cast that starts overlapping reports distance 0 and point zero — use the origin instead.
                if (candidate.distance <= 0f) candidate.point = transform.position;

                if (candidate.distance >= bestDistance) continue;
                bestDistance = candidate.distance;
                best = candidate;
                bestTarget = damageable;
            }
            return bestDistance < float.MaxValue;
        }

        private void HitTarget(IDamageable target, RaycastHit hit)
        {
            if (_onHit != null)
                _onHit(target, hit.point);
            else
                GameLog.Warn(TAG, "Arrow hit a target but has no hit handler — no damage");
            Destroy(gameObject);
        }

        private void Stick(RaycastHit hit, Vector3 origin, Vector3 direction)
        {
            if (_stuckPrefab != null)
            {
                // Tip = root: travel to the impact, then sink a little along the flight direction.
                Vector3 tip = origin + direction * (hit.distance + _stickDepth);
                GameObject stuck = Instantiate(_stuckPrefab, tip, transform.rotation);
                if (stuck.TryGetComponent(out Rigidbody body))
                    body.isKinematic = true;
                // Instantiate targets the active scene (Core) — move it into the region it landed in, so a
                // reload / region change clears it (otherwise reloading duplicates the arrow).
                Scene scene = hit.collider.gameObject.scene;
                if (scene.IsValid() && scene != stuck.scene)
                    SceneManager.MoveGameObjectToScene(stuck, scene);
            }
            Destroy(gameObject);
        }
    }
}
