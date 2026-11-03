#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Game.Inventory;
using Game.Core;

namespace Game.Editor
{
    public static class WireEquipmentVisuals
    {
        private const string TAG = "[WireEquipmentVisuals]";

        [MenuItem("Game/Dev/Wire EquipmentVisuals on Player Prefab")]
        public static void Wire()
        {
            const string prefabPath = "Assets/_Game/Prefabs/Player/Player.prefab";
            const string eventPath  = "Assets/_Game/Data/Events/OnEquipmentChanged.asset";

            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null) { GameLog.Error(TAG, "Player prefab not found"); return; }

            using var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath);
            var root = scope.prefabContentsRoot;

            var ev = root.GetComponent<EquipmentVisuals>();
            if (ev == null) { GameLog.Error(TAG, "EquipmentVisuals not found on Player root"); return; }

            var so = new SerializedObject(ev);

            // _equipmentSystem
            var equipSys = root.GetComponent<EquipmentSystem>();
            so.FindProperty("_equipmentSystem").objectReferenceValue = equipSys;

            // _onEquipmentChanged
            var evSO = AssetDatabase.LoadAssetAtPath<GameEventSO_Void>(eventPath);
            so.FindProperty("_onEquipmentChanged").objectReferenceValue = evSO;

            // _weaponSocket — find by name in rig hierarchy
            var weaponSocket = root.transform.Find("Character/mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2/mixamorig:RightShoulder/mixamorig:RightArm/mixamorig:RightForeArm/mixamorig:RightHand/WeaponSocket");
            so.FindProperty("_weaponSocket").objectReferenceValue = weaponSocket;

            // _leftWeaponSocket — find or create under LeftHand, mirroring the right-hand socket offset (bows)
            var leftHand = root.transform.Find("Character/mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2/mixamorig:LeftShoulder/mixamorig:LeftArm/mixamorig:LeftForeArm/mixamorig:LeftHand");
            Transform leftSocket = null;
            if (leftHand == null)
                GameLog.Warn(TAG, "mixamorig:LeftHand not found — _leftWeaponSocket left unassigned");
            else
            {
                leftSocket = leftHand.Find("LeftWeaponSocket");
                if (leftSocket == null)
                {
                    var go = new GameObject("LeftWeaponSocket") { layer = 3 };
                    leftSocket = go.transform;
                    leftSocket.SetParent(leftHand, worldPositionStays: false);
                    leftSocket.localPosition = new Vector3(-0.002f, 0.092f, 0.011f);
                    leftSocket.localRotation = Quaternion.identity;
                }
            }
            so.FindProperty("_leftWeaponSocket").objectReferenceValue = leftSocket;

            // _backWeaponSocket — find or create under Spine2 (starting placement; tune when the first Back weapon exists)
            var spine2 = root.transform.Find("Character/mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2");
            Transform backSocket = null;
            if (spine2 == null)
                GameLog.Warn(TAG, "mixamorig:Spine2 not found — _backWeaponSocket left unassigned");
            else
            {
                backSocket = spine2.Find("BackWeaponSocket");
                if (backSocket == null)
                {
                    var go = new GameObject("BackWeaponSocket") { layer = 3 };
                    backSocket = go.transform;
                    backSocket.SetParent(spine2, worldPositionStays: false);
                    backSocket.localPosition = new Vector3(0f, 0.05f, -0.15f);
                    backSocket.localRotation = Quaternion.identity;
                }
            }
            so.FindProperty("_backWeaponSocket").objectReferenceValue = backSocket;

            // _helmetSocket — find by name in rig hierarchy
            var helmetSocket = root.transform.Find("Character/mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2/mixamorig:Neck/mixamorig:Head/HelmetSocket");
            so.FindProperty("_helmetSocket").objectReferenceValue = helmetSocket;

            // _bodyRenderer — the naked body SMR (base_hero; Beta_Surface on the legacy model)
            SkinnedMeshRenderer bodyRenderer = null;
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.gameObject.name == "base_hero" || r.gameObject.name == "Beta_Surface")
                {
                    bodyRenderer = r;
                    break;
                }
            }
            // Keep the current reference when no known body is found — don't wipe a hand-wired renderer
            var bodyProp = so.FindProperty("_bodyRenderer");
            if (bodyRenderer != null)
                bodyProp.objectReferenceValue = bodyRenderer;
            else
            {
                bodyRenderer = bodyProp.objectReferenceValue as SkinnedMeshRenderer;
                GameLog.Warn(TAG, $"No base_hero / Beta_Surface body found — keeping the current _bodyRenderer ({(bodyRenderer != null ? bodyRenderer.name : "none")})");
            }

            // _skeletonRoot — armor visuals bind to this skeleton by bone name
            var hips = root.transform.Find("Character/mixamorig:Hips");
            if (hips == null) GameLog.Warn(TAG, "Character/mixamorig:Hips not found — _skeletonRoot left unchanged");
            else so.FindProperty("_skeletonRoot").objectReferenceValue = hips;

            // _animator → Animator component on Player root
            var animator = root.GetComponent<Animator>();
            so.FindProperty("_animator").objectReferenceValue = animator;

            // _defaultAnimatorController → Humanoid_Template.controller asset
            var defaultController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller");
            so.FindProperty("_defaultAnimatorController").objectReferenceValue = defaultController;

            so.ApplyModifiedProperties();

            GameLog.Info(TAG, $"Done. equipSys={equipSys != null}, evSO={evSO != null}, weaponSocket={weaponSocket != null}, leftSocket={leftSocket != null}, backSocket={backSocket != null}, helmetSocket={helmetSocket != null}, bodyRenderer={bodyRenderer != null}, skeletonRoot={hips != null}, animator={animator != null}, defaultController={defaultController != null}");
        }
    }
}
#endif
