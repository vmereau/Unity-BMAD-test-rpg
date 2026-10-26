using System.Collections.Generic;
using System.Text;
using Game.Core;
using Game.Crafting;
using Game.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Cooking screen opened by <see cref="CookingSystem"/>: player inventory grid + two tabs.
    /// <b>Experiment</b>: up to 3 ingredient reference slots; Cook gives the matching station recipe's output (and
    /// teaches the recipe the first time, via its <see cref="RecipeFact"/>) or the failed dish.
    /// <b>Recipes</b>: the station's recipes the player knows; select one and cook it if the inventory holds its ingredients.
    /// Same lifecycle as <see cref="ContainerUI"/>: Esc / player death closes it.
    /// </summary>
    public class CookingUI : MonoBehaviour, IScreenPanel
    {
        private const string TAG = "[CookingUI]";
        private const string EMPTY_HINT = "Place ingredients";
        private const string NOT_ENOUGH_HINT = "Not enough ingredients";
        private const string UNKNOWN_RESULT_HINT = "Unknown result";
        private const string NO_KNOWN_RECIPES_HINT = "No known recipes";
        private const string MISSING_COLOR = "#C06050";
        private const string NEVER_COOKED = "Never cooked";
        private const string COOKED_ONCE = "Cooked once";

        [SerializeField] private InventorySystem _playerInventory;
        [SerializeField] private Transform _playerContentRoot;
        [SerializeField] private GameObject _itemSlotPrefab;
        [SerializeField] private TMP_Text _titleText;

        [Header("Tabs")]
        [SerializeField] private Button _experimentTabButton;
        [SerializeField] private Button _recipesTabButton;
        [SerializeField] private GameObject _experimentPanel;
        [SerializeField] private GameObject _recipesPanel;
        [SerializeField] private Color _inactiveTabColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        [Header("Experiment")]
        [SerializeField] private CookingIngredientSlotUI[] _ingredientSlots;
        [SerializeField] private Image _outputIcon;
        [SerializeField] private TMP_Text _outputText;
        [SerializeField] private TMP_Text _resultText;
        [SerializeField] private Button _cookButton;
        [Tooltip("Given when the experiment matches no station recipe.")]
        [SerializeField] private ItemSO _failedDish;

        [Header("Recipes")]
        [SerializeField] private Transform _recipeListRoot;
        [SerializeField] private CookingRecipeRowUI _recipeRowPrefab;
        [SerializeField] private TMP_Text _noRecipesText;
        [SerializeField] private GameObject _recipeDetail;
        [SerializeField] private Image _recipeOutputIcon;
        [SerializeField] private TMP_Text _recipeNameText;
        [SerializeField] private TMP_Text _recipeIngredientsText;
        [SerializeField] private Button _recipeCookButton;
        [Tooltip("\"Cooked N times\" under the selected recipe (WorldStateManager craft count, saved).")]
        [SerializeField] private TMP_Text _recipeCraftCountText;

        // Closes on death: an open screen would block loading and its Esc would re-lock the death screen's cursor.
        [SerializeField] private GameEventSO_Void _onPlayerDied;
        // Action bar hotkeys stay live while the screen is open (and Cook must refresh the HUD bar): both sides use this channel.
        [SerializeField] private GameEventSO_Int _onActionBarUsed;

        private readonly List<RecipeSO> _knownRecipes = new List<RecipeSO>();
        private readonly List<ItemSO> _consumed = new List<ItemSO>();
        private readonly StringBuilder _sb = new StringBuilder();
        private RecipeSO[] _recipes;
        private ItemSO[] _slots;
        private RecipeSO _selectedRecipe;
        private bool _recipesTabActive;
        private InputSystem_Actions _input;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            _input = new InputSystem_Actions();
            _slots = new ItemSO[_ingredientSlots != null ? _ingredientSlots.Length : 0];
            if (_slots.Length != RecipeSO.MAX_INGREDIENTS)
                GameLog.Warn(TAG, $"{_slots.Length} ingredient slots wired, recipes allow up to {RecipeSO.MAX_INGREDIENTS}");
            if (_failedDish == null)
                GameLog.Warn(TAG, "No failed dish assigned — unmatched experiments can't be cooked");
            _cookButton?.onClick.AddListener(CookExperiment);
            _recipeCookButton?.onClick.AddListener(CookSelectedRecipe);
            _experimentTabButton?.onClick.AddListener(ShowExperimentTab);
            _recipesTabButton?.onClick.AddListener(ShowRecipesTab);
        }

        private void OnEnable()
        {
            if (_input == null) return;
            _input.UI.Enable();
            _input.UI.Cancel.performed += HandleCancel;
            _onPlayerDied?.AddListener(HandlePlayerDied);
            _onActionBarUsed?.AddListener(HandleActionBarUsed);
        }

        private void OnDisable()
        {
            if (_input == null) return;
            _input.UI.Cancel.performed -= HandleCancel;
            _input.UI.Disable();
            _onPlayerDied?.RemoveListener(HandlePlayerDied);
            _onActionBarUsed?.RemoveListener(HandleActionBarUsed);
        }

        private void OnDestroy()
        {
            _cookButton?.onClick.RemoveListener(CookExperiment);
            _recipeCookButton?.onClick.RemoveListener(CookSelectedRecipe);
            _experimentTabButton?.onClick.RemoveListener(ShowExperimentTab);
            _recipesTabButton?.onClick.RemoveListener(ShowRecipesTab);
            _input?.Dispose();
        }

        private void HandleCancel(InputAction.CallbackContext ctx)
        {
            if (gameObject.activeInHierarchy)
                Close();
        }

        private void HandlePlayerDied(bool _)
        {
            if (IsOpen) Close();
        }

        private void HandleActionBarUsed(int _)
        {
            if (IsOpen) Refresh(); // a hotkey consumed an item → grid and counts follow
        }

        public void Open(RecipeSO[] recipes, string stationName)
        {
            _recipes = recipes;
            if (_titleText != null) _titleText.text = stationName;
            gameObject.SetActive(true);
            OnScreenOpen();
        }

        public void Close()
        {
            OnScreenClose();
            gameObject.SetActive(false);
        }

        public void OnScreenOpen()
        {
            ClearSlots(); // the slots are references — always empty on open
            _selectedRecipe = null;
            SetResult(null);
            SetTab(false);
            CursorManager.Unlock();
            IsOpen = true;
            GameLog.Info(TAG, "Cooking UI opened");
        }

        public void OnScreenClose()
        {
            ClearSlots();
            CursorManager.Lock();
            IsOpen = false;
            GameLog.Info(TAG, "Cooking UI closed");
        }

        // ── Tabs ──────────────────────────────────────────────────────────────

        public void ShowExperimentTab() => SetTab(false);

        public void ShowRecipesTab() => SetTab(true);

        private void SetTab(bool recipes)
        {
            _recipesTabActive = recipes;
            if (_experimentPanel != null) _experimentPanel.SetActive(!recipes);
            if (_recipesPanel != null) _recipesPanel.SetActive(recipes);
            TintTab(_experimentTabButton, !recipes);
            TintTab(_recipesTabButton, recipes);
            Refresh();
        }

        // No tab sprites yet: the inactive tab is darkened (the button's ColorTint multiplies this base color).
        private void TintTab(Button tab, bool active)
        {
            if (tab != null && tab.targetGraphic != null)
                tab.targetGraphic.color = active ? Color.white : _inactiveTabColor;
        }

        // ── Experiment slots ──────────────────────────────────────────────────

        public void SetIngredient(int slotIndex, ItemSO item)
        {
            if (item == null || _slots == null || slotIndex < 0 || slotIndex >= _slots.Length) return;
            if (!item.isIngredient)
            {
                SetResult($"{item.itemName} isn't an ingredient");
                return;
            }
            _slots[slotIndex] = item;
            SetResult(null);
            RefreshExperiment();
        }

        public void SetIngredientFromInventory(int index)
        {
            if (_recipesTabActive) return; // double-click only fills experiment slots
            // Read the item the player sees on the clicked slot, not Items[index] — the grid may lag the inventory.
            ItemSlotUI slot = FindGridSlot(index);
            if (slot == null || slot.Item == null) return;
            if (!slot.Item.isIngredient) return; // the grid lists ingredients only — defensive
            int empty = System.Array.IndexOf(_slots, null);
            if (empty >= 0) SetIngredient(empty, slot.Item);
        }

        // The grid lists ingredients only, so a child's position isn't its inventory index — match SlotIndex instead.
        private ItemSlotUI FindGridSlot(int inventoryIndex)
        {
            if (_playerContentRoot == null || inventoryIndex < 0) return null;
            foreach (Transform child in _playerContentRoot)
                if (child.TryGetComponent(out ItemSlotUI slot) && slot.SlotIndex == inventoryIndex)
                    return slot;
            return null;
        }

        public void ClearIngredient(int slotIndex)
        {
            if (_slots == null || slotIndex < 0 || slotIndex >= _slots.Length) return;
            _slots[slotIndex] = null;
            SetResult(null);
            RefreshExperiment();
        }

        private void ClearSlots()
        {
            if (_slots != null) System.Array.Clear(_slots, 0, _slots.Length);
        }

        // ── Cooking ───────────────────────────────────────────────────────────

        public void CookExperiment()
        {
            if (_playerInventory == null) return;
            EventSystem.current?.SetSelectedGameObject(null); // Submit (Enter / Space) must not repeat the cook
            if (CookingRules.EvaluateExperiment(_slots, CountOf) != ExperimentState.Ready) return;

            RecipeSO recipe = CookingRules.FindRecipe(_recipes, _slots);
            ItemSO result = recipe != null ? recipe.output : _failedDish;
            if (result == null)
            {
                GameLog.Warn(TAG, "No recipe matches and no failed dish is assigned — nothing cooked");
                return;
            }
            if (!TryCraft(_slots, result)) return;

            if (recipe != null && recipe.knownFact != null && WorldStateManager.Instance == null)
                GameLog.Warn(TAG, $"No WorldStateManager (Core not loaded?) — {recipe.name} can't be learned");
            bool learned = recipe != null && recipe.knownFact != null && WorldStateManager.Instance != null && !IsKnown(recipe);
            if (learned) WorldStateManager.Instance.SetRecipeKnown(recipe.knownFact);
            if (recipe != null) CountCraft(recipe);
            SetResult(learned ? $"New recipe: {result.itemName}" : $"Cooked {result.itemName}");
            GameLog.Info(TAG, $"Experiment → {result.itemName}{(learned ? " (recipe learned)" : string.Empty)}");

            ClearShortSlots();
            NotifyInventoryChanged();
        }

        public void CookSelectedRecipe()
        {
            if (_playerInventory == null || _selectedRecipe == null) return;
            EventSystem.current?.SetSelectedGameObject(null);
            if (!CookingRules.HasIngredients(_selectedRecipe.ingredients, CountOf)) return;
            if (!TryCraft(_selectedRecipe.ingredients, _selectedRecipe.output)) return;

            CountCraft(_selectedRecipe);
            GameLog.Info(TAG, $"Cooked recipe {_selectedRecipe.name} → {_selectedRecipe.output.itemName}");
            NotifyInventoryChanged();
        }

        /// <summary>Consumes one unit per (non-null) ingredient then adds <paramref name="result"/>; refunds everything on failure.</summary>
        private bool TryCraft(IReadOnlyList<ItemSO> ingredients, ItemSO result)
        {
            _consumed.Clear();
            for (int i = 0; i < ingredients.Count; i++)
            {
                ItemSO item = ingredients[i];
                if (item == null) continue;
                if (!_playerInventory.TryConsume(item))
                {
                    GameLog.Warn(TAG, $"Could not consume {item.itemName} — nothing cooked");
                    Refund();
                    return false;
                }
                _consumed.Add(item);
            }
            if (!_playerInventory.AddItem(result))
            {
                GameLog.Warn(TAG, $"Could not add {result.itemName} — ingredients refunded");
                Refund();
                return false;
            }
            return true;
        }

        private void Refund()
        {
            foreach (ItemSO item in _consumed)
                if (!_playerInventory.AddItem(item))
                    GameLog.Error(TAG, $"Refund failed — {item.itemName} lost");
            _consumed.Clear();
            NotifyInventoryChanged(); // consume + re-add may reorder slots → the HUD bar re-validates too
        }

        private void NotifyInventoryChanged()
        {
            // Inventory slots changed → the HUD action bar re-validates its bindings; our own listener refreshes this screen.
            if (_onActionBarUsed != null) _onActionBarUsed.Raise(ActionBarSystem.REFRESH_ONLY_SLOT);
            else Refresh();
        }

        /// <summary>After a cook: a slot whose item no longer has enough units (counting earlier slots) clears.</summary>
        private void ClearShortSlots()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                ItemSO item = _slots[i];
                if (item == null) continue;
                int usedSoFar = 0;
                for (int j = 0; j <= i; j++)
                    if (_slots[j] == item) usedSoFar++;
                if (usedSoFar > CountOf(item)) _slots[i] = null;
            }
        }

        // ── Refresh ───────────────────────────────────────────────────────────

        private void Refresh()
        {
            RefreshGrid();
            if (_recipesTabActive) RefreshRecipes();
            else RefreshExperiment();
        }

        private void RefreshGrid()
        {
            if (_playerContentRoot == null) return;

            foreach (Transform child in _playerContentRoot)
                Destroy(child.gameObject);

            if (_playerInventory == null || _itemSlotPrefab == null) return;

            // Only ingredients are listed; each slot keeps its inventory index (double-click resolves it by SlotIndex).
            var items = _playerInventory.Items;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Item == null || !items[i].Item.isIngredient) continue;
                var go = Instantiate(_itemSlotPrefab, _playerContentRoot);
                go.GetComponent<ItemSlotUI>()?.Bind(items[i].Item, i, items[i].Count);
            }
        }

        private void RefreshExperiment()
        {
            if (_slots == null) return;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null && CountOf(_slots[i]) <= 0) _slots[i] = null; // item gone → slot clears
                _ingredientSlots[i]?.Bind(_slots[i]);
            }

            ExperimentState state = CookingRules.EvaluateExperiment(_slots, CountOf);
            RecipeSO match = state == ExperimentState.Ready ? CookingRules.FindRecipe(_recipes, _slots) : null;
            // Never reveal an undiscovered recipe: the output is previewed only when the match is already known.
            ItemSO preview = match != null && IsKnown(match) ? match.output : null;

            SetIcon(_outputIcon, preview);
            if (_outputText != null)
            {
                _outputText.text = state switch
                {
                    ExperimentState.Empty => EMPTY_HINT,
                    ExperimentState.NotEnough => NOT_ENOUGH_HINT,
                    _ => preview != null ? preview.itemName : UNKNOWN_RESULT_HINT
                };
            }
            if (_cookButton != null) _cookButton.interactable = state == ExperimentState.Ready && (match != null || _failedDish != null);
        }

        private void RefreshRecipes()
        {
            _knownRecipes.Clear();
            if (_recipes != null)
                foreach (RecipeSO recipe in _recipes)
                    if (recipe != null && recipe.IsComplete && IsKnown(recipe) && !_knownRecipes.Contains(recipe))
                        _knownRecipes.Add(recipe);

            if (_selectedRecipe == null || !_knownRecipes.Contains(_selectedRecipe))
                _selectedRecipe = _knownRecipes.Count > 0 ? _knownRecipes[0] : null;

            if (_recipeListRoot != null)
            {
                foreach (Transform child in _recipeListRoot)
                    Destroy(child.gameObject);
                if (_recipeRowPrefab != null)
                    foreach (RecipeSO recipe in _knownRecipes)
                        Instantiate(_recipeRowPrefab, _recipeListRoot)
                            .Bind(recipe, recipe == _selectedRecipe, CookingRules.HasIngredients(recipe.ingredients, CountOf));
            }

            if (_noRecipesText != null)
            {
                _noRecipesText.text = NO_KNOWN_RECIPES_HINT;
                _noRecipesText.gameObject.SetActive(_knownRecipes.Count == 0);
            }
            if (_recipeDetail != null) _recipeDetail.SetActive(_selectedRecipe != null);
            if (_selectedRecipe == null) return;

            SetIcon(_recipeOutputIcon, _selectedRecipe.output);
            if (_recipeNameText != null) _recipeNameText.text = _selectedRecipe.output.itemName;
            if (_recipeIngredientsText != null) _recipeIngredientsText.text = FormatIngredients(_selectedRecipe.ingredients);
            if (_recipeCookButton != null)
                _recipeCookButton.interactable = CookingRules.HasIngredients(_selectedRecipe.ingredients, CountOf);
            if (_recipeCraftCountText != null)
                _recipeCraftCountText.text = FormatCraftCount(CraftCount(_selectedRecipe));
        }

        /// <summary>"Never cooked" / "Cooked once" / "Cooked N times".</summary>
        public static string FormatCraftCount(int count) => count switch
        {
            <= 0 => NEVER_COOKED,
            1 => COOKED_ONCE,
            _ => $"Cooked {count} times"
        };

        public void SelectRecipe(RecipeSO recipe)
        {
            if (recipe == null) return;
            _selectedRecipe = recipe;
            RefreshRecipes();
        }

        /// <summary>One line per distinct ingredient: "Apple 1/1", missing ones tinted.</summary>
        private string FormatIngredients(ItemSO[] ingredients)
        {
            _sb.Clear();
            for (int i = 0; i < ingredients.Length; i++)
            {
                ItemSO item = ingredients[i];
                if (System.Array.IndexOf(ingredients, item) != i) continue; // first occurrence only
                int need = CookingRules.CountIn(ingredients, item);
                int have = CountOf(item);
                if (_sb.Length > 0) _sb.Append('\n');
                if (have < need) _sb.Append("<color=").Append(MISSING_COLOR).Append('>');
                _sb.Append(item.itemName).Append("  ").Append(have).Append('/').Append(need);
                if (have < need) _sb.Append("</color>");
            }
            return _sb.ToString();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private int CountOf(ItemSO item) =>
            _playerInventory != null && item != null ? _playerInventory.CountOf(item) : 0;

        /// <summary>Counts a successful cook of <paramref name="recipe"/> (no fact or no WorldStateManager → not counted).</summary>
        private static void CountCraft(RecipeSO recipe)
        {
            if (recipe.knownFact == null || WorldStateManager.Instance == null) return;
            WorldStateManager.Instance.AddRecipeCraft(recipe.knownFact);
        }

        private static int CraftCount(RecipeSO recipe) =>
            recipe.knownFact != null && WorldStateManager.Instance != null
                ? WorldStateManager.Instance.GetRecipeCraftCount(recipe.knownFact) : 0;

        private static bool IsKnown(RecipeSO recipe) =>
            recipe.knownFact != null && WorldStateManager.Instance != null && WorldStateManager.Instance.IsRecipeKnown(recipe.knownFact);

        private void SetResult(string message)
        {
            if (_resultText == null) return;
            _resultText.text = message ?? string.Empty;
            _resultText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        private static void SetIcon(Image image, ItemSO item)
        {
            if (image == null) return;
            image.sprite = item != null ? item.icon : null;
            image.color = item != null && item.icon == null ? Color.gray : Color.white; // like ItemSlotUI
            image.enabled = item != null;
        }
    }
}
