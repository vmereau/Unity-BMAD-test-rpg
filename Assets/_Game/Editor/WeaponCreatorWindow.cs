using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Game.Inventory;
using Game.Combat;

namespace Game.Editor
{
    public class WeaponCreatorWindow : EditorWindow
    {
        private TextField _weaponNameField;
        private TextField _descriptionField;
        private ObjectField _baseModelField;
        private FloatField _damageBonusField;
        private IntegerField _comboStepsField;
        private ObjectField _animatorOverrideField;
        private DropdownField _weaponTypeField;

        private List<Type> _weaponTypes;

        [MenuItem("Tools/Items/Weapon Creator")]
        public static void ShowWindow()
        {
            WeaponCreatorWindow wnd = GetWindow<WeaponCreatorWindow>();
            wnd.titleContent = new GUIContent("Weapon Creator");
            wnd.minSize = new Vector2(350, 450);
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;

            // Style
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;

            Label title = new Label("Create New Weapon");
            title.style.fontSize = 20;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 10;
            root.Add(title);

            _weaponNameField = new TextField("Weapon Name");
            root.Add(_weaponNameField);

            _descriptionField = new TextField("Description");
            _descriptionField.multiline = true;
            _descriptionField.style.height = 60;
            root.Add(_descriptionField);

            _weaponTypes = GetWeaponTypes();
            List<string> typeNames = _weaponTypes.Select(t => t.Name).ToList();
            _weaponTypeField = new DropdownField("Weapon Type", typeNames, 0);
            root.Add(_weaponTypeField);

            _baseModelField = new ObjectField("Base Model (Prefab/Mesh)") { objectType = typeof(GameObject) };
            root.Add(_baseModelField);

            _damageBonusField = new FloatField("Damage Bonus");
            root.Add(_damageBonusField);

            _comboStepsField = new IntegerField("Combo Steps") { value = 2 };
            root.Add(_comboStepsField);

            _animatorOverrideField = new ObjectField("Animator Override") { objectType = typeof(AnimatorOverrideController) };
            root.Add(_animatorOverrideField);

            VisualElement spacer = new VisualElement();
            spacer.style.height = 20;
            root.Add(spacer);

            Button createButton = new Button(OnCreateClicked) { text = "Create Weapon Assets" };
            createButton.style.height = 30;
            createButton.style.backgroundColor = new Color(0.2f, 0.4f, 0.2f);
            createButton.style.color = Color.white;
            root.Add(createButton);

            Button aiIconButton = new Button(OnRequestAIIconClicked) { text = "Request AI Icon (Assistant)" };
            aiIconButton.style.marginTop = 10;
            root.Add(aiIconButton);
        }

        private List<Type> GetWeaponTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => typeof(WeaponSO).IsAssignableFrom(p) && !p.IsAbstract)
                .ToList();
        }

        private void OnCreateClicked()
        {
            string weaponName = _weaponNameField.value;
            if (string.IsNullOrEmpty(weaponName))
            {
                EditorUtility.DisplayDialog("Error", "Please enter a weapon name.", "OK");
                return;
            }

            GameObject baseModel = _baseModelField.value as GameObject;
            if (baseModel == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a base model.", "OK");
                return;
            }

            Type selectedType = _weaponTypes[_weaponTypeField.index];
            string typeFolderName = selectedType.Name.Replace("SO", "") + "s"; // e.g. SwordSO -> Swords
            
            // 1. Initialize Folders
            string dataPath = $"Assets/_Game/Data/Items/Weapons/{typeFolderName}";
            string prefabsPath = $"Assets/_Game/Prefabs/Items/Weapons/{typeFolderName}/{weaponName}";

            if (!AssetDatabase.IsValidFolder(dataPath))
                Directory.CreateDirectory(dataPath);
            
            if (!AssetDatabase.IsValidFolder(prefabsPath))
                Directory.CreateDirectory(prefabsPath);

            AssetDatabase.Refresh();

            // 2. Create ScriptableObject
            WeaponSO weaponSO = ScriptableObject.CreateInstance(selectedType) as WeaponSO;
            weaponSO.itemName = weaponName;
            weaponSO.description = _descriptionField.value;
            weaponSO.damageBonus = _damageBonusField.value;
            weaponSO.comboSteps = _comboStepsField.value;
            weaponSO.animatorOverrideController = _animatorOverrideField.value as AnimatorOverrideController;

            string soPath = $"{dataPath}/Weapon_{weaponName}.asset";
            AssetDatabase.CreateAsset(weaponSO, soPath);

            // 3. Create Visual Prefab
            GameObject visualRoot = new GameObject($"{weaponName}_Visual");
            visualRoot.AddComponent<Rigidbody>().isKinematic = true;

            GameObject drawn = new GameObject("Drawn");
            drawn.transform.SetParent(visualRoot.transform);
            SetupModelChild(drawn, baseModel, true);

            GameObject sheathed = new GameObject("Sheathed");
            sheathed.transform.SetParent(visualRoot.transform);
            SetupModelChild(sheathed, baseModel, false);

            string visualPrefabPath = $"{prefabsPath}/{weaponName}_Visual.prefab";
            GameObject visualPrefab = PrefabUtility.SaveAsPrefabAsset(visualRoot, visualPrefabPath);
            DestroyImmediate(visualRoot);

            // 4. Create World Prefab
            GameObject worldRoot = new GameObject($"{weaponName}_World");
            worldRoot.layer = LayerMask.NameToLayer("Interactable");
            var pickup = worldRoot.AddComponent<ItemPickup>();
            pickup.Configure(weaponSO);
            
            var worldRb = worldRoot.AddComponent<Rigidbody>();
            var worldCollider = worldRoot.AddComponent<BoxCollider>();
            // Guess size based on base model if possible
            if (baseModel.GetComponentInChildren<Renderer>() != null)
            {
                worldCollider.center = baseModel.GetComponentInChildren<Renderer>().localBounds.center;
                worldCollider.size = baseModel.GetComponentInChildren<Renderer>().localBounds.size;
            }

            GameObject worldModel = new GameObject("Model");
            worldModel.transform.SetParent(worldRoot.transform);
            SetupModelChild(worldModel, baseModel, false);

            string worldPrefabPath = $"{prefabsPath}/{weaponName}_World.prefab";
            GameObject worldPrefab = PrefabUtility.SaveAsPrefabAsset(worldRoot, worldPrefabPath);
            DestroyImmediate(worldRoot);

            // 5. Finalize SO
            weaponSO.equipVisualPrefab = visualPrefab;
            weaponSO.worldItemPrefab = worldPrefab;
            EditorUtility.SetDirty(weaponSO);
            AssetDatabase.SaveAssets();

            // 6. Generate Icon
            ItemIconGenerator.GenerateMissingIcons();

            EditorUtility.DisplayDialog("Success", $"Weapon '{weaponName}' created successfully!", "OK");
        }

        private void SetupModelChild(GameObject parent, GameObject baseModel, bool isDrawn)
        {
            GameObject modelInstance = Instantiate(baseModel, parent.transform);
            modelInstance.name = "Mesh";

            if (isDrawn)
            {
                var trigger = parent.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                parent.AddComponent<WeaponHitbox>();

                // Set layer of the trigger child? The script says it checks CharacterHitbox layer for 'other'.
                // The hitbox itself doesn't need a specific layer unless it's for something else.
            }
        }

        private void OnRequestAIIconClicked()
        {
            string weaponName = _weaponNameField.value;
            string desc = _descriptionField.value;
            string prompt = $"Assistant, please generate a weapon icon for '{weaponName}'. Description: {desc}. Match the project art style.";
            
            Debug.Log($"[AssistantRequest] {prompt}");
            EditorUtility.DisplayDialog("Assistant Request", "Request logged to Console. Please copy the prompt from the Console and paste it to the Assistant in the chat.", "OK");
        }
    }
}
