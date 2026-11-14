using System.Collections.Generic;
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
        [SerializeField] private Transform _leftWeaponSocket;
        [SerializeField] private Transform _undrawnWeaponSocket;
        [SerializeField] private Transform _backWeaponSocket;
        [SerializeField] private Transform _helmetSocket;
        [Tooltip("Naked body below the neck — hidden while a skinned armor is worn. The Head SMR is separate and always visible.")]
        [SerializeField] private SkinnedMeshRenderer _bodyRenderer;
        [Tooltip("Skeleton root (mixamorig:Hips) that armor visuals are bound to by bone name.")]
        [SerializeField] private Transform _skeletonRoot;
        [SerializeField] private Animator _animator;
        [SerializeField] private RuntimeAnimatorController _defaultAnimatorController;
        // Story 7.9: raised at the end of Refresh() so listeners see a valid ActiveWeaponGO
        [SerializeField] private GameEventSO_Void _onVisualsRefreshed;

        private GameObject _weaponVisual;
        private WeaponArchetypeSO _currentArchetype;
        private AnimatorOverrideController _currentWeaponOverride; // equipped weapon's override, applied unless forced default
        private bool _forceDefaultController; // NPC fists with a weapon equipped (ForceDefaultController)
        private bool _backSocketWarned; // warn once per equipped weapon, not on every sheathe
        private bool _leftSocketWarned; // warn once per equipped weapon, not on every draw
        private GameObject _helmetVisual;
        private GameObject _armorVisual;
        private ItemSO _armorVisualItem; // armor the current _armorVisual was built for — skip rebuilds on unrelated refreshes
        private Dictionary<string, Transform> _skeleton;
        private readonly List<string> _missingBones = new List<string>();
        private bool _isInCombat = false;

        /// <summary>The currently instantiated armor visual, or null. Polled by NpcSkinTone to tone a newly built armor.</summary>
        public GameObject ArmorVisual => _armorVisual;

        /// <summary>The currently instantiated weapon visual GO. Exposed for PlayerCombat (story 7-9) to locate WeaponHitbox.</summary>
        public GameObject ActiveWeaponGO => _weaponVisual;

        /// <summary>Archetype of the equipped weapon (null when unarmed or no archetype).</summary>
        public WeaponArchetypeSO CurrentArchetype => _currentArchetype;
        /// <summary>True once the weapon visual sits in the hand (set by SetCombatState, i.e. at the draw's WeaponSwap).</summary>
        public bool IsDrawn => _isInCombat;
        /// <summary>Hand socket the drawn weapon uses for the current archetype (no warning side effect).</summary>
        public Transform DrawnSocket => ResolveDrawnSocket(_currentArchetype, _weaponSocket, _leftWeaponSocket);
        /// <summary>Controller currently on the animator (null without one) — lets NPC callers detect a swap.</summary>
        public RuntimeAnimatorController AppliedController => _animator != null ? _animator.runtimeAnimatorController : null;
        /// <summary>Left-hand socket (mirror of the right one), or null when unassigned.</summary>
        public Transform LeftHandSocket => _leftWeaponSocket;

        private void Awake()
        {
            if (_equipmentSystem == null)
            {
                GameLog.Error(TAG, "EquipmentVisuals: _equipmentSystem is not assigned");
                enabled = false;
                return;
            }

            if (_bodyRenderer == null)
                GameLog.Warn(TAG, "EquipmentVisuals: _bodyRenderer is not assigned — armor will not hide the body");
            if (_skeletonRoot == null)
                GameLog.Warn(TAG, "EquipmentVisuals: _skeletonRoot is not assigned — skinned armor visuals will be skipped");

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

        private void HandleEquipmentChanged(bool _) => Refresh(true);

        public void SetCombatState(bool isInCombat)
        {
            _isInCombat = isInCombat;
            if (_weaponVisual == null) return;
            var targetSocket = isInCombat ? CurrentDrawnSocket() : CurrentSheathSocket();
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

        /// <summary>
        /// Rebuilds the weapon / helmet / armor visuals from the equipped items. <paramref name="applyAnimatorController"/>
        /// false keeps the current controller (NPC knocked out / dying: a swap resets every animator parameter and state,
        /// which would stand the body up) — call again with true once that is safe.
        /// </summary>
        public void Refresh(bool applyAnimatorController = true)
        {
            if (_equipmentSystem == null) return;
            RefreshWeapon(applyAnimatorController);
            RefreshHelmet();
            RefreshBody();
            _onVisualsRefreshed?.Raise(false);
        }

        /// <summary>
        /// Fists while a weapon is equipped (NPC brawl asked with fists): forces the default controller instead of the
        /// weapon's override. Applied at once; the caller re-pushes the animator parameters it needs (IsInCombat).
        /// </summary>
        public void ForceDefaultController(bool force)
        {
            _forceDefaultController = force;
            ApplyAnimatorOverride(_currentWeaponOverride);
        }

        private void RefreshWeapon(bool applyAnimatorController)
        {
            if (_weaponVisual != null)
                Destroy(_weaponVisual);
            _weaponVisual = null;
            _currentArchetype = null;
            _currentWeaponOverride = null;
            _backSocketWarned = false;
            _leftSocketWarned = false;
            if (applyAnimatorController) ApplyAnimatorOverride(null); // Restore default controller when weapon removed

            var weapon = _equipmentSystem.GetEquipped(EquipmentSlot.Weapon) as EquipableItemSO;
            if (weapon == null || _weaponSocket == null) return;

            _currentArchetype = (weapon as WeaponSO)?.archetype;
            var sheathSocket = _isInCombat ? null : CurrentSheathSocket();
            var targetSocket = sheathSocket != null ? sheathSocket : CurrentDrawnSocket();
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
            _currentWeaponOverride = (weapon as WeaponSO)?.ResolvedAnimatorOverride;
            if (applyAnimatorController) ApplyAnimatorOverride(_currentWeaponOverride);
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
            var armor = _equipmentSystem.GetEquipped(EquipmentSlot.Armor) as EquipableItemSO;
            if (_armorVisual != null && armor != null && armor == _armorVisualItem) return; // same armor, still bound

            if (_armorVisual != null)
                Destroy(_armorVisual);
            _armorVisual = null;
            _armorVisualItem = null;

            bool bodyCovered = false;
            if (armor != null)
            {
                if (armor.equipVisualPrefab == null)
                    GameLog.Info(TAG, $"Armor '{armor.itemName}' has no equipVisualPrefab — body stays visible");
                else
                    bodyCovered = TryAttachArmor(armor);
            }

            if (_bodyRenderer != null)
                _bodyRenderer.enabled = !bodyCovered;
        }

        /// <summary>Instantiates the armor's skinned visual next to the body and binds it to the player skeleton by bone name.</summary>
        private bool TryAttachArmor(EquipableItemSO armor)
        {
            if (_skeletonRoot == null) return false;

            var parent = _bodyRenderer != null ? _bodyRenderer.transform.parent : _skeletonRoot.parent;
            var visual = Instantiate(armor.equipVisualPrefab, parent);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            var part = visual.GetComponent<SkinnedBodyPart>();
            if (part == null || part.Renderer == null)
            {
                GameLog.Warn(TAG, $"Armor '{armor.itemName}': equipVisualPrefab has no SkinnedBodyPart with a renderer — body stays visible");
                DiscardVisual(visual);
                return false;
            }

            _skeleton ??= _bodyRenderer != null
                ? SkinnedMeshRebinder.IndexBones(_bodyRenderer.bones, _skeletonRoot)
                : SkinnedMeshRebinder.IndexSkeleton(_skeletonRoot);
            if (!SkinnedMeshRebinder.TryBind(part.Renderer, part.BoneNames, part.RootBoneName, _skeleton, _skeletonRoot, _missingBones))
            {
                GameLog.Warn(TAG, $"Armor '{armor.itemName}': cannot bind to the player skeleton ({string.Join(", ", _missingBones)}) — body stays visible");
                DiscardVisual(visual);
                return false;
            }

            if (_bodyRenderer != null)
            {
                // Render like the body it replaces (layer, outline rendering layer, shadows); bounds = body's grown by the baked armor bounds.
                visual.layer = _bodyRenderer.gameObject.layer;
                part.Renderer.renderingLayerMask = _bodyRenderer.renderingLayerMask;
                part.Renderer.shadowCastingMode = _bodyRenderer.shadowCastingMode;
                part.Renderer.receiveShadows = _bodyRenderer.receiveShadows;
                part.Renderer.updateWhenOffscreen = _bodyRenderer.updateWhenOffscreen;
                var bounds = _bodyRenderer.localBounds;
                bounds.Encapsulate(part.Renderer.localBounds);
                part.Renderer.localBounds = bounds;
            }
            _armorVisual = visual;
            _armorVisualItem = armor;
            GameLog.Info(TAG, $"Armor visual attached (prefab: {armor.equipVisualPrefab.name})");
            return true;
        }

        private static void DiscardVisual(GameObject visual)
        {
            visual.SetActive(false); // Destroy is deferred to end of frame — never render the unbound mesh
            Destroy(visual);
        }

        private void ApplyAnimatorOverride(AnimatorOverrideController overrideController)
        {
            if (_animator == null) return;
            RuntimeAnimatorController target = overrideController != null && !_forceDefaultController
                ? overrideController
                : _defaultAnimatorController;
            if (_animator.runtimeAnimatorController == target) return; // a same-controller assign would still reset params
            _animator.runtimeAnimatorController = target;
        }

        private Transform CurrentDrawnSocket()
        {
            if (!_leftSocketWarned && _currentArchetype != null && _currentArchetype.drawnHand == WeaponHand.Left && _leftWeaponSocket == null)
            {
                _leftSocketWarned = true;
                GameLog.Warn(TAG, $"Archetype '{_currentArchetype.name}' requests the Left hand socket but _leftWeaponSocket is not assigned — falling back to the right hand");
            }
            return ResolveDrawnSocket(_currentArchetype, _weaponSocket, _leftWeaponSocket);
        }

        /// <summary>Left hand socket when the archetype asks for it and it exists; right hand socket otherwise (incl. null archetype).</summary>
        public static Transform ResolveDrawnSocket(WeaponArchetypeSO archetype, Transform rightSocket, Transform leftSocket)
        {
            if (archetype != null && archetype.drawnHand == WeaponHand.Left && leftSocket != null)
                return leftSocket;
            return rightSocket;
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
