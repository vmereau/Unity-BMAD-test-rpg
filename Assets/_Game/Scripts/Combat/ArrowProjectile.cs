using System;
using Game.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Combat
{
    /// <summary>
    /// Straight-line projectile (no gravity, no Rigidbody). Each frame it sphere-casts the distance it travels:
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

        [Tooltip("Sweep radius — small forgiveness around the tip.")]
        [SerializeField, Min(0.001f)] private float _radius = 0.05f;
        [Tooltip("Layers the arrow collides with. Empty = Default + CharacterHitbox.")]
        [SerializeField] private LayerMask _hitLayers;
        [Tooltip("How deep the tip sinks into world geometry when it sticks.")]
        [SerializeField, Min(0f)] private float _stickDepth = 0.08f;

        private readonly RaycastHit[] _hitBuffer = new RaycastHit[HIT_BUFFER_SIZE];

        private Transform _owner;
        private Vector3 _direction;
        private float _speed;
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
        public void Launch(Transform owner, Vector3 direction, float speed, float lifetime,
            Action<IDamageable, Vector3> onHit, GameObject stuckPrefab)
        {
            _owner = owner;
            _direction = direction.normalized;
            _speed = speed;
            _expireTime = Time.time + lifetime;
            _onHit = onHit;
            _stuckPrefab = stuckPrefab;
            transform.rotation = Quaternion.LookRotation(_direction);
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

            float distance = _speed * Time.deltaTime;
            Vector3 origin = transform.position;
            int count = Physics.SphereCastNonAlloc(origin, _radius, _direction, _hitBuffer, distance,
                _hitLayers, QueryTriggerInteraction.Collide);

            if (TryFindFirstHit(count, out RaycastHit hit, out IDamageable target))
            {
                if (target != null)
                {
                    HitTarget(target, hit);
                    return;
                }
                Stick(hit, origin);
                return;
            }

            transform.position = origin + _direction * distance;
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

        private void Stick(RaycastHit hit, Vector3 origin)
        {
            if (_stuckPrefab != null)
            {
                // Tip = root: travel to the impact, then sink a little along the flight direction.
                Vector3 tip = origin + _direction * (hit.distance + _stickDepth);
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
