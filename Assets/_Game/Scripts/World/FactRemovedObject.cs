using Game.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Game.World
{
    /// <summary>
    /// Hides a target object while a <see cref="WorldFact"/> is true, such as a blocker a quest clears. Lives on a parent
    /// of the target, so it keeps listening after the target goes. Reads the fact in <c>Start</c>, which covers a load:
    /// facts are restored before the region reloads.
    /// </summary>
    public class FactRemovedObject : MonoBehaviour
    {
        private const string TAG = "[FactRemovedObject]";

        [SerializeField] private WorldFact _fact;
        [SerializeField] private GameObject _target;
        [SerializeField] private GameEventSO_Fact _onFactChanged;

        [Tooltip("Optional. Left off in the scene, because a bake cuts an enabled carving obstacle into the NavMesh for good.")]
        [SerializeField] private NavMeshObstacle _runtimeCarve;

        private void Awake()
        {
            if (_runtimeCarve != null) _runtimeCarve.enabled = true;
            if (_fact != null && _target != null && _onFactChanged != null) return;
            GameLog.Error(TAG, $"{name}: needs a fact, a target and the fact event — disabled.");
            enabled = false;
        }

        private void OnEnable()
        {
            if (_onFactChanged != null) _onFactChanged.AddListener(HandleFactChanged);
        }

        private void OnDisable()
        {
            if (_onFactChanged != null) _onFactChanged.RemoveListener(HandleFactChanged);
        }

        private void Start()
        {
            if (WorldStateManager.Instance == null)
            {
                GameLog.Warn(TAG, $"{name}: no WorldStateManager loaded — the target stays.");
                return;
            }
            Apply(WorldStateManager.Instance.GetFact(_fact));
        }

        private void HandleFactChanged(FactData data)
        {
            if (data.key == _fact.ToString()) Apply(data.value);
        }

        private void Apply(bool factIsTrue) => _target.SetActive(!factIsTrue);
    }
}
