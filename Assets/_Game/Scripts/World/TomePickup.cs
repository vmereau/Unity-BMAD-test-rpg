using Game.Core;
using Game.Progression;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Passive world object. Player interacts via proximity + E key to learn a skill using LP.
    /// Consumes the tome (deactivates GO) on successful skill acquisition.
    /// Cross-system ref to PlayerSkills is an intentional prototype pragmatism (see Dev Notes).
    /// Story 3.5: Initial implementation.
    /// </summary>
    public class TomePickup : MonoBehaviour
    {
        private const string TAG = "[World]";

        [SerializeField] private SkillSO _skill;
        [SerializeField] private PlayerSkills _playerSkills;
        [SerializeField] private Transform _playerTransform;
        [SerializeField] private float _interactionRadius = 2f;

        private PersistentID _persistentID;
        private InputSystem_Actions _input;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private GUIStyle _promptStyle;
#endif

        private void Awake()
        {
            if (_skill == null)
            {
                GameLog.Error(TAG, "TomePickup: _skill not assigned — component disabled.");
                enabled = false;
                return;
            }
            if (_playerSkills == null)
            {
                GameLog.Error(TAG, "TomePickup: _playerSkills not assigned — component disabled.");
                enabled = false;
                return;
            }
            if (_playerTransform == null)
            {
                GameLog.Error(TAG, "TomePickup: _playerTransform not assigned — component disabled.");
                enabled = false;
                return;
            }

            _persistentID = GetComponent<PersistentID>();
            if (_persistentID == null)
                GameLog.Warn(TAG, $"TomePickup on {gameObject.name}: no PersistentID — tome won't be tracked by WorldStateManager.");

            _input = new InputSystem_Actions();
        }

        private void OnEnable()
        {
            // Guard: Awake may disable before OnEnable runs (required refs missing)
            if (_skill == null || _playerSkills == null || _playerTransform == null) return;
            if (_input == null) _input = new InputSystem_Actions();
            _input.Player.Enable();
        }

        private void OnDisable()
        {
            if (_input == null) return; // Guard: Awake may disable before OnEnable runs
            _input.Player.Disable();
            _input.Dispose();
            _input = null; // Null after Dispose to prevent ObjectDisposedException on re-enable
        }

        private void Update()
        {
            if (Vector3.Distance(transform.position, _playerTransform.position) > _interactionRadius)
                return;

            if (!_input.Player.Interact.WasPressedThisFrame())
                return;

            bool learned = _playerSkills.LearnSkill(_skill);
            if (learned)
            {
                GameLog.Info(TAG, $"Tome consumed: {_skill.displayName}");
                _persistentID?.RegisterDeath();
                gameObject.SetActive(false);
            }
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void OnGUI()
        {
            if (_playerTransform == null || _skill == null) return;
            if (Vector3.Distance(transform.position, _playerTransform.position) > _interactionRadius) return;

            if (_promptStyle == null)
                _promptStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };

            GUI.Label(new Rect(Screen.width / 2f - 200, Screen.height * 0.65f, 400, 30),
                $"Press E to read: {_skill.displayName}", _promptStyle);
        }
#endif
    }
}
