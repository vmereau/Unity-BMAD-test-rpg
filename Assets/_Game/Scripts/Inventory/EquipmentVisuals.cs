using Game.Core;
using UnityEngine;

namespace Game.Inventory
{
    public class EquipmentVisuals : MonoBehaviour
    {
        private const string TAG = "[Inventory]";

        [SerializeField] private EquipmentSystem _equipmentSystem;
        [SerializeField] private GameEventSO_Void _onEquipmentChanged;
        [SerializeField] private Transform _weaponSocket;
        [SerializeField] private Transform _helmetSocket;
        [SerializeField] private Renderer _bodyRenderer;
        [SerializeField] private Material _armorPlaceholderMaterial;
        [SerializeField] private Animator _animator;
        [SerializeField] private RuntimeAnimatorController _defaultAnimatorController;

        private GameObject _weaponVisual;
        private GameObject _helmetVisual;
        private Material _originalBodyMaterial;

        /// <summary>The currently instantiated weapon visual GO. Exposed for PlayerCombat (story 7-9) to locate WeaponHitbox.</summary>
        public GameObject ActiveWeaponGO => _weaponVisual;

        private void Awake()
        {
            if (_equipmentSystem == null)
            {
                GameLog.Error(TAG, "EquipmentVisuals: _equipmentSystem is not assigned");
                enabled = false;
                return;
            }

            if (_bodyRenderer == null)
                GameLog.Warn(TAG, "EquipmentVisuals: _bodyRenderer is not assigned — body material swap will be skipped");

            _originalBodyMaterial = _bodyRenderer != null ? _bodyRenderer.sharedMaterial : null;

            if (_animator == null)
                GameLog.Warn(TAG, "EquipmentVisuals: _animator not assigned — animator override will be skipped");
            if (_defaultAnimatorController == null)
                GameLog.Warn(TAG, "EquipmentVisuals: _defaultAnimatorController not assigned — override restore will set controller to null");
        }

        private void OnEnable()
        {
            _onEquipmentChanged?.AddListener(HandleEquipmentChanged);
            Refresh(); // Ensure visuals match equipment state on (re-)enable, not just on change events
        }

        private void OnDisable()
        {
            if (_onEquipmentChanged == null) return; // Guard: field may be unassigned in Inspector
            _onEquipmentChanged.RemoveListener(HandleEquipmentChanged);
        }

        private void HandleEquipmentChanged(bool _) => Refresh();

        public void Refresh()
        {
            if (_equipmentSystem == null) return;
            RefreshWeapon();
            RefreshHelmet();
            RefreshBody();
        }

        private void RefreshWeapon()
        {
            if (_weaponVisual != null)
                Destroy(_weaponVisual);
            _weaponVisual = null;
            ApplyAnimatorOverride(null); // Restore default controller when weapon removed

            var weapon = _equipmentSystem.GetEquipped(EquipmentSlot.Weapon) as EquipableItemSO;
            if (weapon == null || _weaponSocket == null) return;

            if (weapon.equipVisualPrefab != null)
            {
                _weaponVisual = Object.Instantiate(weapon.equipVisualPrefab, _weaponSocket);
                _weaponVisual.transform.localPosition = Vector3.zero;
                _weaponVisual.transform.localRotation = Quaternion.identity;
                GameLog.Info(TAG, $"Weapon visual attached (prefab: {weapon.equipVisualPrefab.name})");
            }
            else
            {
                _weaponVisual = CreatePlaceholder(PrimitiveType.Cube, _weaponSocket, new Vector3(0.07f, 0.07f, 0.5f), Color.yellow);
                GameLog.Info(TAG, "Weapon visual attached (placeholder)");
            }
            ApplyAnimatorOverride((weapon as WeaponSO)?.animatorOverrideController);
        }

        private void RefreshHelmet()
        {
            if (_helmetVisual != null)
                Destroy(_helmetVisual);
            _helmetVisual = null;

            var helmet = _equipmentSystem.GetEquipped(EquipmentSlot.Helmet) as EquipableItemSO;
            if (helmet == null || _helmetSocket == null) return;

            if (helmet.equipVisualPrefab != null)
            {
                _helmetVisual = Object.Instantiate(helmet.equipVisualPrefab, _helmetSocket);
                _helmetVisual.transform.localPosition = Vector3.zero;
                _helmetVisual.transform.localRotation = Quaternion.identity;
                GameLog.Info(TAG, $"Helmet visual attached (prefab: {helmet.equipVisualPrefab.name})");
            }
            else
            {
                _helmetVisual = CreatePlaceholder(PrimitiveType.Sphere, _helmetSocket, new Vector3(0.28f, 0.28f, 0.28f), Color.cyan);
                GameLog.Info(TAG, "Helmet visual attached (placeholder)");
            }
        }

        private void RefreshBody()
        {
            if (_bodyRenderer == null) return;

            var armor = _equipmentSystem.GetEquipped(EquipmentSlot.Armor);
            if (armor != null && _armorPlaceholderMaterial != null)
                _bodyRenderer.material = _armorPlaceholderMaterial;
            else
                _bodyRenderer.material = _originalBodyMaterial;
        }

        private void ApplyAnimatorOverride(AnimatorOverrideController overrideController)
        {
            if (_animator == null) return;
            _animator.runtimeAnimatorController = overrideController != null
                ? overrideController
                : _defaultAnimatorController;
        }

        private static GameObject CreatePlaceholder(PrimitiveType type, Transform socket, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = $"Placeholder_{type}";
            go.transform.SetParent(socket, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = scale;
            Object.Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().material.color = color;
            return go;
        }
    }
}
