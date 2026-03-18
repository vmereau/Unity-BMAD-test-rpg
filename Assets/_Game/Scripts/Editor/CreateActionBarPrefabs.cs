#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.UI;

namespace Game.Editor
{
    /// <summary>
    /// One-time utility to create the ActionBar prefabs.
    /// Run via: Tools > Game > Create Action Bar Prefabs
    /// After running, delete this script.
    /// </summary>
    public static class CreateActionBarPrefabs
    {
        private const string FOLDER = "Assets/_Game/Prefabs/UI/ActionBar";
        private const string SLOT_PREFAB = FOLDER + "/ActionBarSlot.prefab";
        private const string BAR_PREFAB = FOLDER + "/ActionBar.prefab";

        [MenuItem("Tools/Game/Create Action Bar Prefabs")]
        public static void Create()
        {
            // Ensure folder exists
            if (!AssetDatabase.IsValidFolder(FOLDER))
                AssetDatabase.CreateFolder("Assets/_Game/Prefabs/UI", "ActionBar");

            CreateSlotPrefab();
            CreateBarPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ActionBar] Prefabs created at " + FOLDER);
        }

        private static void CreateSlotPrefab()
        {
            // Root: 64x64 Image (background)
            var root = new GameObject("ActionBarSlot");
            root.AddComponent<RectTransform>();
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
            var rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(64f, 64f);

            // Icon child: 52x52, center-anchored
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(root.transform, false);
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.color = Color.clear;
            var iconRT = iconGO.GetComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.5f, 0.5f);
            iconRT.anchorMax = new Vector2(0.5f, 0.5f);
            iconRT.pivot = new Vector2(0.5f, 0.5f);
            iconRT.anchoredPosition = Vector2.zero;
            iconRT.sizeDelta = new Vector2(52f, 52f);

            // StackCountText child: upper-left, 28x18, size 12 bold, inactive by default
            var stackGO = new GameObject("StackCountText");
            stackGO.transform.SetParent(root.transform, false);
            var stackTMP = stackGO.AddComponent<TextMeshProUGUI>();
            stackTMP.text = "1";
            stackTMP.fontSize = 12f;
            stackTMP.fontStyle = FontStyles.Bold;
            stackTMP.color = Color.white;
            stackTMP.alignment = TextAlignmentOptions.TopLeft;
            var stackRT = stackGO.GetComponent<RectTransform>();
            stackRT.anchorMin = new Vector2(0f, 1f);
            stackRT.anchorMax = new Vector2(0f, 1f);
            stackRT.pivot = new Vector2(0f, 1f);
            stackRT.anchoredPosition = new Vector2(2f, -2f);
            stackRT.sizeDelta = new Vector2(28f, 18f);
            stackGO.SetActive(false);

            // KeyLabel child: lower-right, 20x16, size 10 white
            var keyGO = new GameObject("KeyLabel");
            keyGO.transform.SetParent(root.transform, false);
            var keyTMP = keyGO.AddComponent<TextMeshProUGUI>();
            keyTMP.text = "1";
            keyTMP.fontSize = 10f;
            keyTMP.color = Color.white;
            keyTMP.alignment = TextAlignmentOptions.BottomRight;
            var keyRT = keyGO.GetComponent<RectTransform>();
            keyRT.anchorMin = new Vector2(1f, 0f);
            keyRT.anchorMax = new Vector2(1f, 0f);
            keyRT.pivot = new Vector2(1f, 0f);
            keyRT.anchoredPosition = new Vector2(-2f, 2f);
            keyRT.sizeDelta = new Vector2(20f, 16f);

            // ActionBarSlotUI component
            var slotUI = root.AddComponent<ActionBarSlotUI>();
            // Wire fields via SerializedObject
            var prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, SLOT_PREFAB);
            Object.DestroyImmediate(root);

            // Wire serialized fields using SerializedObject
            using var so = new SerializedObject(prefabAsset.GetComponent<ActionBarSlotUI>());
            so.FindProperty("_iconImage").objectReferenceValue =
                prefabAsset.transform.Find("Icon").GetComponent<Image>();
            so.FindProperty("_backgroundImage").objectReferenceValue =
                prefabAsset.GetComponent<Image>();
            so.FindProperty("_stackCountText").objectReferenceValue =
                prefabAsset.transform.Find("StackCountText").GetComponent<TextMeshProUGUI>();
            so.FindProperty("_keyLabelText").objectReferenceValue =
                prefabAsset.transform.Find("KeyLabel").GetComponent<TextMeshProUGUI>();
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SavePrefabAsset(prefabAsset);

            Debug.Log("[ActionBar] ActionBarSlot.prefab created.");
        }

        private static void CreateBarPrefab()
        {
            var slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SLOT_PREFAB);
            if (slotPrefab == null)
            {
                Debug.LogError("[ActionBar] ActionBarSlot.prefab not found — run CreateSlotPrefab first.");
                return;
            }

            // Root: ActionBar with HorizontalLayoutGroup
            var root = new GameObject("ActionBar");
            var rootRT = root.AddComponent<RectTransform>();
            // Anchored bottom-center, pivot (0.5, 0)
            rootRT.anchorMin = new Vector2(0.5f, 0f);
            rootRT.anchorMax = new Vector2(0.5f, 0f);
            rootRT.pivot = new Vector2(0.5f, 0f);
            rootRT.anchoredPosition = new Vector2(0f, 20f);

            var hlg = root.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4f;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            // Add ContentSizeFitter so root width auto-fits to children
            var csf = root.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ActionBarUI component
            root.AddComponent<ActionBarUI>();

            // 6 ActionBarSlot children
            var slotInstances = new GameObject[6];
            for (int i = 0; i < 6; i++)
            {
                var slotInstance = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab);
                slotInstance.name = "ActionBarSlot";
                slotInstance.transform.SetParent(root.transform, false);
                slotInstances[i] = slotInstance;
            }

            var prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, BAR_PREFAB);
            Object.DestroyImmediate(root);

            // Wire ActionBarUI._slotUIs array
            using var so = new SerializedObject(prefabAsset.GetComponent<ActionBarUI>());
            var slotUIsProperty = so.FindProperty("_slotUIs");
            slotUIsProperty.arraySize = 6;
            var slots = prefabAsset.GetComponentsInChildren<ActionBarSlotUI>();
            for (int i = 0; i < 6 && i < slots.Length; i++)
                slotUIsProperty.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SavePrefabAsset(prefabAsset);

            Debug.Log("[ActionBar] ActionBar.prefab created.");
        }
    }
}
#endif
