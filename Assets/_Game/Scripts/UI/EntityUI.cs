using Game.AI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class EntityUI : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private Image _healthFill;
        [SerializeField] private GameObject _healthBarRoot;

        private Camera _mainCam;
        private EntityHealth _health;

        private void Awake()
        {
            // EntityUI lives on the entity root alongside EntityHealth (when the entity has health).
            _health = GetComponent<EntityHealth>();
            // Entities without health (e.g. plain NPCs) never show a health bar.
            if (_healthBarRoot != null) _healthBarRoot.SetActive(_health != null);
        }

        private void OnEnable()
        {
            if (_health == null) return;
            _health.HealthChanged += OnHealthChanged;
            // Seed the bar: HealthChanged may have already fired during EntityHealth's
            // Awake/OnEnable before we subscribed, so pull the current value now.
            OnHealthChanged(_health.CurrentHealth, _health.MaxHealth);
        }

        private void OnDisable()
        {
            if (_health == null) return; // Guard: Awake may find no EntityHealth
            _health.HealthChanged -= OnHealthChanged;
        }

        private void Start()
        {
            _mainCam = Camera.main;
        }

        public void SetName(string name)
        {
            if (_nameText != null) _nameText.text = name;
        }

        private void OnHealthChanged(float current, float max)
        {
            if (_healthFill != null && max > 0)
                _healthFill.fillAmount = current / max;
        }

        public void Show(bool visible)
        {
            if (_canvas != null && _canvas.enabled != visible)
                _canvas.enabled = visible;
        }

        private void LateUpdate()
        {
            if (_canvas != null && _canvas.enabled)
            {
                if (_mainCam == null) _mainCam = Camera.main;
                if (_mainCam == null) return;

                // Face the camera (billboard effect)
                _canvas.transform.LookAt(_canvas.transform.position + _mainCam.transform.rotation * Vector3.forward,
                    _mainCam.transform.rotation * Vector3.up);
            }
        }
    }
}
