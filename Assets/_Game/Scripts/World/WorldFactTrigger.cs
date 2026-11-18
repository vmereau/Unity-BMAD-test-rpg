using Game.AI;
using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Sets a <see cref="WorldFact"/> once when the player stands inside this volume. Shape = every
    /// <see cref="BoxCollider"/> on this GameObject and its children (triggers on Ignore Raycast, only center / size
    /// are read). The project has no trigger events: containment is polled every <c>_pollInterval</c> seconds with
    /// <see cref="RestrictedAreaRules.IsInsideBox"/>, like <see cref="RestrictedArea"/>. Idles once the fact is true
    /// (also after a load), so it never writes the fact twice.
    /// </summary>
    public class WorldFactTrigger : MonoBehaviour
    {
        private const string TAG = "[WorldFactTrigger]";

        [SerializeField] private WorldFact _fact;
        [Min(0.05f)] [SerializeField] private float _pollInterval = 0.25f;

        private BoxCollider[] _boxes = System.Array.Empty<BoxCollider>();
        private float _nextPoll;

        private void Awake()
        {
            _boxes = GetComponentsInChildren<BoxCollider>(true);
            if (_fact != null && _boxes.Length > 0) return;
            GameLog.Error(TAG, $"{name}: needs a WorldFact and at least one BoxCollider shape — disabled.");
            enabled = false;
        }

        private void Update()
        {
            if (Time.time < _nextPoll) return;
            _nextPoll = Time.time + _pollInterval;

            WorldStateManager state = WorldStateManager.Instance;
            var player = TargetRegistry.Player;
            if (state == null || player == null || state.GetFact(_fact)) return;
            if (!Contains(player.transform.position)) return;

            state.SetWorldEvent(_fact, true);
            GameLog.Info(TAG, $"{name}: player entered — {_fact.name} set");
        }

        private bool Contains(Vector3 worldPoint)
        {
            for (int i = 0; i < _boxes.Length; i++)
            {
                BoxCollider box = _boxes[i];
                if (box == null || !box.enabled || !box.gameObject.activeInHierarchy) continue;
                if (RestrictedAreaRules.IsInsideBox(box.transform.InverseTransformPoint(worldPoint), box.center, box.size))
                    return true;
            }
            return false;
        }
    }
}
