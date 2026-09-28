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
        [SerializeField] private Transform _undrawnWeaponSocket;
        [SerializeField] private Transform _backWeaponSocket;
        [SerializeField] private Transform _helmetSocket;
        [SerializeField] private Renderer _bodyRenderer;
        [SerializeField] private Material _armorPlaceholderMaterial;
        [SerializeField] private Animator _animator;
        [SerializeField] private RuntimeAnimatorController _defaultAnimatorController;
        // Story 7.9: raised at the end of Refresh() so listeners see a valid ActiveWeaponGO
        [SerializeField] private GameEventSO_Void _onVisualsRefreshed;

        private GameObject _weaponVisual;
        private WeaponArchetypeSO _currentArchetype;
        private bool _backSocketWarned; // warn once per equipped weapon, not on every sheathe
        private GameObject _helmetVisual;
        private Material _originalBodyMaterial;
        private bool _isInCombat = false;

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

        public void SetCombatState(bool isInCombat)
        {
            _isInCombat = isInCombat;
            if (_weaponVisual == null) return;
            var targetSocket = isInCombat ? _weaponSocket : CurrentSheathSocket();
            if (targetSocket == null)
            {
                GameLog.Warn(TAG, $"SetCombatState({isInCombat}): target socket is null — weapon visual not moved. Check socket assignments on EquipmentVisuals.");
                return;
            }
            _weaponVisual.transform.SetParent(targetSocket, worldPositionStays: false);
            _weaponVisual.transform.localPosition = Vector3.zero;
            _weaponVisual.transform.localRotation = Quaternion.identity;
            ApplyCombatVisibility(_weaponVisual, isInCombat);
            GameLog.Info(TAG, $"Weapon visual moved to {targetSocket.name}");
        }

        public void Refresh()
        {
            if (_equipmentSystem == null) return;
            RefreshWeapon();
            RefreshHelmet();
            RefreshBody();
            _onVisualsRefreshed?.Raise(false);
        }

        private void RefreshWeapon()
        {
            if (_weaponVisual != null)
                Destroy(_weaponVisual);
            _weaponVisual = null;
            _currentArchetype = null;
            _backSocketWarned = false;
            ApplyAnimatorOverride(null); // Restore default controller when weapon removed

            var weapon = _equipmentSystem.GetEquipped(EquipmentSlot.Weapon) as EquipableItemSO;
            if (weapon == null || _weaponSocket == null) return;

            _currentArchetype = (weapon as WeaponSO)?.archetype;
            var sheathSocket = _isInCombat ? null : CurrentSheathSocket();
            var targetSocket = sheathSocket != null ? sheathSocket : _weaponSocket;
            if (weapon.equipVisualPrefab != null)
            {
                _weaponVisual = Instantiate(weapon.equipVisualPrefab, targetSocket);
                _weaponVisual.transform.localPosition = Vector3.zero;
                _weaponVisual.transform.localRotation = Quaternion.identity;
                ApplyArchetypePoses(_weaponVisual, _currentArchetype);
                ApplyCombatVisibility(_weaponVisual, _isInCombat);
                GameLog.Info(TAG, $"Weapon visual attached (prefab: {weapon.equipVisualPrefab.name})");
            }
            else
            {
                _weaponVisual = CreatePlaceholder(PrimitiveType.Cube, targetSocket, new Vector3(0.07f, 0.07f, 0.5f), Color.yellow);
                GameLog.Info(TAG, "Weapon visual attached (placeholder)");
            }
            ApplyAnimatorOverride((weapon as WeaponSO)?.ResolvedAnimatorOverride);
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

        private Transform CurrentSheathSocket()
        {
            if (!_backSocketWarned && _currentArchetype != null && _currentArchetype.sheathSocket == WeaponSheathSocket.Back && _backWeaponSocket == null)
            {
                _backSocketWarned = true;
                GameLog.Warn(TAG, $"Archetype '{_currentArchetype.name}' requests the Back sheath socket but _backWeaponSocket is not assigned — falling back to hip");
            }
            return ResolveSheathSocket(_currentArchetype, _undrawnWeaponSocket, _backWeaponSocket);
        }

        /// <summary>Back socket when the archetype asks for it and it exists; hip socket otherwise (incl. null archetype).</summary>
        public static Transform ResolveSheathSocket(WeaponArchetypeSO archetype, Transform hipSocket, Transform backSocket)
        {
            if (archetype != null && archetype.sheathSocket == WeaponSheathSocket.Back && backSocket != null)
                return backSocket;
            return hipSocket;
        }

        /// <summary>
        /// Applies the archetype's Drawn / Sheathed poses to the matching children of a weapon visual.
        /// No-op when either argument is null (weapons without an archetype keep their prefab poses);
        /// missing children are skipped silently, consistent with ApplyCombatVisibility.
        /// </summary>
        public static void ApplyArchetypePoses(GameObject weaponVisual, WeaponArchetypeSO archetype)
        {
            if (weaponVisual == null || archetype == null) return;
            var drawn = weaponVisual.transform.Find("Drawn");
            var sheathed = weaponVisual.transform.Find("Sheathed");
            if (drawn != null) archetype.drawnPose.ApplyTo(drawn);
            if (sheathed != null) archetype.sheathedPose.ApplyTo(sheathed);
        }

        /// <summary>
        /// Shows the "Drawn" child and hides "Sheathed" (or vice versa) on weapon visual prefabs
        /// that use the Drawn/Sheathed child convention. No-ops silently if children are absent
        /// (e.g. placeholder cube, or weapons that haven't adopted the convention yet).
        /// </summary>
        private static void ApplyCombatVisibility(GameObject weaponVisual, bool isInCombat)
        {
            var drawn   = weaponVisual.transform.Find("Drawn");
            var sheathed = weaponVisual.transform.Find("Sheathed");
            if (drawn   != null) drawn.gameObject.SetActive(isInCombat);
            if (sheathed != null) sheathed.gameObject.SetActive(!isInCombat);
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
