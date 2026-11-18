using Game.AI;
using Game.Core;
using Game.Dialogue;
using Game.Economy;
using Game.Inventory;
using Game.NPC;
using Game.Player;
using Game.Progression;
using Game.Quest;
using Game.UI;
using UnityEngine;

namespace Game.World
{
    public class DialogueSystem : MonoBehaviour
    {
        private const string TAG = "[Dialogue]";

        [SerializeField] private GameEventSO_NPCDialogueRequest _onDialogueRequested;
        [SerializeField] private DialogueUI _dialogueUI;
        [SerializeField] private PlayerStateManager _playerStateManager;
        [SerializeField] private PlayerStats _playerStats;
        [SerializeField] private GoldSystem _goldSystem;
        [SerializeField] private LearningPointSystem _lpSystem;
        [SerializeField] private PlayerSkills _playerSkills;
        [SerializeField] private NPCTradeUI _tradeUI;
        [Tooltip("Give window opened by a ChoiceOption.requiredCategory choice (the player picks the item to hand over).")]
        [SerializeField] private GiveItemUI _giveItemUI;
        [Tooltip("The player's inventory: a ChoiceOption.requiredItem is taken from it.")]
        [SerializeField] private InventorySystem _playerInventory;
        [Tooltip("The player's equipment: a worn ChoiceOption.requiredItem counts as held and is unequipped on hand-over. " +
                 "Null = only the bag counts.")]
        [SerializeField] private EquipmentSystem _playerEquipment;
        [Tooltip("Coin item added to the NPC's inventory when the player pays a ChoiceOption.goldCost (Item_Gold).")]
        [SerializeField] private ItemSO _goldItem;
        [Tooltip("Raised when a picked ChoiceOption carries a DialogueSignalSO (OnDialogueSignal).")]
        [SerializeField] private GameEventSO_DialogueSignal _onDialogueSignal;
        [Tooltip("Player knocked out (OnPlayerKnockedOut): an open dialogue closes.")]
        [SerializeField] private GameEventSO_Void _onPlayerKnockedOut;
        [Tooltip("An NPC asks to close the dialogue it opened (OnDialogueCloseRequested): closes only that NPC's dialogue.")]
        [SerializeField] private GameEventSO_DialogueCloseRequest _onDialogueCloseRequested;

        public bool IsOpen { get; private set; }

        private TextDialogueNode _openingLineNode; // runtime node for NPCDialogueRequestData.forcedLine + forcedStartNode

        /// <summary>True while the Give window of a category choice is open over the dialogue (DialogueUI ignores input).</summary>
        public bool IsGiveWindowOpen => _giveItemUI != null && _giveItemUI.IsOpen;

        /// <summary>Give window open, or closed this frame (its Escape must not also close the dialogue).</summary>
        public bool IsGiveWindowOpenOrJustClosed => _giveItemUI != null && _giveItemUI.IsOpenOrJustClosed;

        private NPCMemoryComponent _currentNPCMemory;
        private NPCDialogueGraphComponent _currentGraph;
        private InventorySystem _currentNPCInventory;
        private GoldSystem _currentNPCGoldSystem;
        private StartDialogueNode _currentStartNode;
        private string _currentNPCName;
        private NPCPresence _currentRequester; // the NPC whose request opened this dialogue (null: forced line, none)
        private readonly System.Collections.Generic.HashSet<DialogueNode> _warnedNoId = new();

        private void OnEnable()
        {
            if (_onDialogueRequested == null)
            {
                GameLog.Warn(TAG, "No dialogue event assigned — DialogueSystem will not respond to NPC interactions");
                return;
            }
            _onDialogueRequested.AddListener(HandleDialogueRequested);
            if (_onPlayerKnockedOut != null) _onPlayerKnockedOut.AddListener(HandlePlayerKnockedOut);
            if (_onDialogueCloseRequested != null) _onDialogueCloseRequested.AddListener(HandleDialogueCloseRequested);
            else GameLog.Warn(TAG, "OnDialogueCloseRequested not assigned — NPCs can't close the dialogue they opened");
        }

        private void OnDisable()
        {
            if (_onPlayerKnockedOut != null) _onPlayerKnockedOut.RemoveListener(HandlePlayerKnockedOut);
            if (_onDialogueCloseRequested != null) _onDialogueCloseRequested.RemoveListener(HandleDialogueCloseRequested);
            if (_onDialogueRequested == null) return;
            _onDialogueRequested.RemoveListener(HandleDialogueRequested);
        }

        private void HandlePlayerKnockedOut(bool _)
        {
            if (!IsOpen) return;
            GameLog.Info(TAG, "Player knocked out — closing the dialogue");
            Close();
        }

        private void HandleDialogueCloseRequested(NPCPresence requester)
        {
            if (!ShouldCloseFor(IsOpen, _currentRequester, requester))
            {
                if (IsOpen) GameLog.Info(TAG, "Dialogue close request ignored — the open dialogue belongs to another NPC");
                return;
            }
            GameLog.Info(TAG, $"{_currentNPCName} closes the dialogue");
            Close();
        }

        /// <summary>
        /// A close request applies only to the dialogue its requester opened. Reference identity, not Unity's ==: during a
        /// region unload the requester may already be destroyed, and Unity treats two destroyed objects as equal.
        /// </summary>
        public static bool ShouldCloseFor(bool isOpen, object current, object requester) =>
            isOpen && current != null && ReferenceEquals(current, requester);

        private void HandleDialogueRequested(NPCDialogueRequestData data)
        {
            if (_dialogueUI == null)
            {
                GameLog.Error(TAG, "DialogueUI not assigned — cannot open dialogue");
                return;
            }

            if (IsOpen)
            {
                GameLog.Warn(TAG, $"Dialogue request from {data.npcName} ignored — a dialogue is already open");
                return;
            }

            // NPC-initiated (e.g. a witness scolding a thief): one line, no topics, the player closes it.
            // With a forced start node too, the line instead opens that topic (see below).
            if (!string.IsNullOrEmpty(data.forcedLine) && data.forcedStartNode == null)
            {
                _currentNPCMemory = null;
                _currentGraph = null;
                _currentNPCInventory = null;
                _currentNPCGoldSystem = null;
                _currentStartNode = null;
                _currentRequester = null;
                _dialogueUI.OpenLine(data.npcName, data.forcedLine);
                IsOpen = true;
                if (_playerStateManager != null)
                    _playerStateManager.SetInDialogue(true);
                CursorManager.Unlock();
                GameLog.Info(TAG, $"{data.npcName} confronts the player");
                return;
            }

            _currentNPCMemory = data.memories;
            _currentGraph = data.graph;
            _currentNPCInventory = data.npcInventory;
            _currentNPCGoldSystem = data.npcGoldSystem;
            _currentNPCName = data.npcName;
            _currentRequester = data.requester;

            // NPC-initiated topic (e.g. a toll collector stopping the player): straight into the chain, no topic list.
            if (data.forcedStartNode != null)
            {
                if (data.forcedStartNode.nextNode == null)
                {
                    GameLog.Warn(TAG, $"Forced topic '{data.forcedStartNode.name}' from {data.npcName} has no nextNode — ignored");
                    ClearContext();
                    return;
                }
                _dialogueUI.OpenForcedTopic(data.npcName, data.forcedTopicThenTopics);
                IsOpen = true;
                if (_playerStateManager != null)
                    _playerStateManager.SetInDialogue(true);
                CursorManager.Unlock();
                GameLog.Info(TAG, $"{data.npcName} forces topic '{data.forcedStartNode.name}' on the player");
                if (string.IsNullOrEmpty(data.forcedLine))
                {
                    StartTopic(data.forcedStartNode);
                    return;
                }
                // Opening line (e.g. a random scold bark) shown before the topic's chain.
                _currentStartNode = data.forcedStartNode;
                MarkSeen(data.forcedStartNode, data.forcedStartNode.Id);
                AdvanceToNode(GetOpeningLineNode(data.forcedLine, data.forcedStartNode.nextNode));
                return;
            }

            StartDialogueNode[] startNodes = _currentGraph != null
                ? _currentGraph.GetAvailableStartNodes(_currentNPCMemory)
                : System.Array.Empty<StartDialogueNode>();

            _dialogueUI.Open(data.npcName, startNodes);
            IsOpen = true;
            if (_playerStateManager != null)
                _playerStateManager.SetInDialogue(true);
            CursorManager.Unlock();
            GameLog.Info(TAG, $"Opened dialogue with {data.npcName} — {startNodes.Length} topic(s) available");
        }

        /// <summary>
        /// Advances dialogue to the given node. null = close dialogue.
        /// Called by DialogueUI buttons (start topic, text advance, choice selection).
        /// </summary>
        public void AdvanceToNode(DialogueNode node)
        {
            if (node == null)
            {
                Close();
                return;
            }

            switch (node)
            {
                case TextDialogueNode textNode:
                    _dialogueUI.ShowTextNode(textNode);
                    break;

                case ShopDialogueNode shopNode:
                    if (_currentNPCInventory == null)
                    {
                        GameLog.Warn(TAG, "ShopDialogueNode reached but NPC has no InventorySystem — treating as regular text");
                        _dialogueUI.ShowTextNode(shopNode);
                    }
                    else
                    {
                        _dialogueUI.ShowShopNode(shopNode, () =>
                        {
                            InventorySystem inv = _currentNPCInventory;
                            GoldSystem npcGold = _currentNPCGoldSystem;
                            Close();
                            if (_tradeUI != null) _tradeUI.Open(inv, npcGold);
                        });
                    }
                    break;

                case ChoiceDialogueNode choiceNode:
                    ChoiceOption[] availableChoices = _currentGraph != null
                        ? _currentGraph.GetAvailableChoices(choiceNode, _currentNPCMemory)
                        : choiceNode.choices ?? System.Array.Empty<ChoiceOption>();
                    _dialogueUI.ShowChoiceNode(choiceNode, FilterMissingGoodsChoices(availableChoices));
                    break;

                case TeachChoiceDialogueNode teachNode:
                    TeachChoiceOption[] availableTeachChoices = _currentGraph != null
                        ? _currentGraph.GetAvailableTeachChoices(teachNode, _currentNPCMemory)
                        : teachNode.choices ?? System.Array.Empty<TeachChoiceOption>();
                    availableTeachChoices = FilterLearnedSkillChoices(availableTeachChoices);
                    _dialogueUI.ShowTeachChoiceNode(teachNode, availableTeachChoices);
                    break;

                case StartDialogueNode _:
                    // Author error: StartDialogueNode should not appear mid-chain
                    GameLog.Warn(TAG, $"StartDialogueNode '{node.name}' referenced mid-chain — closing dialogue");
                    Close();
                    break;

                default:
                    GameLog.Warn(TAG, $"Unknown node type '{node.GetType().Name}' — closing dialogue");
                    Close();
                    break;
            }
        }

        /// <summary>
        /// Called by DialogueUI when the player selects a topic.
        /// Tracks the active start node for played-state recording, then advances.
        /// </summary>
        public void StartTopic(StartDialogueNode startNode)
        {
            _currentStartNode = startNode;
            if (startNode != null)
                MarkSeen(startNode, startNode.Id);
            if (startNode.nextNode == null)
            {
                GameLog.Warn(TAG, $"StartDialogueNode '{startNode.name}' has no nextNode — cannot start topic");
                return;
            }
            AdvanceToNode(startNode.nextNode);
        }

        /// <summary>
        /// Called by DialogueUI when a dialogue chain reaches its end node (nextNode == null).
        /// If the active topic is non-repeatable, records it as played in WorldStateManager.
        /// Always clears the active start node reference.
        /// </summary>
        public void NotifyTopicCompleted()
        {
            if (_currentStartNode == null)
            {
                GameLog.Warn(TAG, "NotifyTopicCompleted called but no active start node — played state not recorded");
                return;
            }
            
            if (_currentStartNode.dialogueFact == null)
            {
                // Repeatable topic (no fact to record) — nothing to do.
            }
            else if (WorldStateManager.Instance != null)
            {
                WorldStateManager.Instance.SetDialoguePlayed(_currentStartNode.dialogueFact);
                GameLog.Info(TAG, $"Dialogue topic '{_currentStartNode.name}' marked as played");
            }
            else
            {
                GameLog.Warn(TAG, $"WorldStateManager unavailable — dialogue topic '{_currentStartNode.name}' played state not recorded");
            }
            
            _currentStartNode = null;
        }

        /// <summary>
        /// Called by DialogueUI when the player selects a regular choice option.
        /// Marks the choice seen, records its dialogueFact as played (if assigned), then advances the chain.
        /// <paramref name="node"/> = the choice node that offered it (null = not tracked as seen).
        /// </summary>
        /// <returns>False when the choice wasn't applied (null, its gold cost could not be paid or its required item
        /// is missing).</returns>
        public bool SelectChoice(ChoiceOption choice, ChoiceDialogueNode node = null)
        {
            if (choice == null) return false;
            if (choice.requiredItem == null && choice.requiredCategory != GiveItemCategory.None)
                return OpenGiveWindow(choice, node);
            // Item first: a missing item must not cost the player gold.
            if (choice.requiredItem != null && !HasChoiceItem(choice.requiredItem))
            {
                GameLog.Warn(TAG, $"Choice '{choice.text}' needs {choice.requiredItem.itemName} — the player doesn't have it");
                return false;
            }
            // Gold checked before a worn item is unequipped: a refused choice leaves the equipment alone.
            if (!CanAffordGold(choice.goldCost)) return false;
            if (choice.requiredItem != null && !HandOverChoiceItem(choice)) return false;
            // Item before gold (as CompleteGiveChoice): a failed hand-over must not cost the player gold. PayChoiceGold
            // can't fail after CanAffordGold.
            if (choice.goldCost > 0) PayChoiceGold(choice.goldCost);
            ApplyChoice(choice, node);
            return true;
        }

        /// <summary>
        /// Called by the Give window when the player confirms <paramref name="item"/> for a category choice: re-checks the
        /// item, pays the gold, moves one unit to the NPC's inventory, then applies the choice (seen, fact, signal, next
        /// node). A choice that ends the chain returns the dialogue to its topic list.
        /// </summary>
        /// <returns>False when the dialogue was closed meanwhile or the item can no longer be given.</returns>
        public bool CompleteGiveChoice(ChoiceOption choice, ChoiceDialogueNode node, ItemSO item)
        {
            if (!IsOpen || choice == null) return false;
            if (!GiveItemRules.Matches(item, choice.requiredCategory) || _playerInventory == null
                || _playerInventory.CountOf(item) <= 0)
            {
                string itemName = item != null ? item.itemName : "null";
                GameLog.Warn(TAG, $"Choice '{choice.text}': {itemName} can't be given for {choice.requiredCategory}");
                return false;
            }
            if (!CanAffordGold(choice.goldCost)) return false;
            // Item before gold: a failed hand-over must not cost the player gold (PayChoiceGold can't fail after the check).
            if (!GiveItemToNpc(item)) return false;
            if (choice.goldCost > 0) PayChoiceGold(choice.goldCost);
            ApplyChoice(choice, node);
            if (choice.nextNode == null && IsOpen && _dialogueUI != null)
                _dialogueUI.RestoreTopics();
            return true;
        }

        // Category choice: nothing is applied yet, the Give window calls CompleteGiveChoice on Give. Returns false so
        // DialogueUI doesn't treat the choice as done.
        private bool OpenGiveWindow(ChoiceOption choice, ChoiceDialogueNode node)
        {
            if (!HasChoiceCategory(choice.requiredCategory))
            {
                GameLog.Warn(TAG, $"Choice '{choice.text}' needs a {choice.requiredCategory} item — the player has none");
                return false;
            }
            if (!CanAffordGold(choice.goldCost)) return false;
            if (_giveItemUI == null)
            {
                GameLog.Error(TAG, $"Choice '{choice.text}' needs the Give window but _giveItemUI is not assigned — choice refused");
                return false;
            }
            _giveItemUI.Open(_currentNPCName, choice.requiredCategory, _playerInventory,
                item => CompleteGiveChoice(choice, node, item));
            return false;
        }

        // Shared tail of a picked choice: seen state, dialogueFact, signal, then the next node (or topic completion).
        private void ApplyChoice(ChoiceOption choice, ChoiceDialogueNode node)
        {
            if (node != null)
                MarkSeen(node, node.ChoiceSeenId(choice));

            if (choice.dialogueFact && WorldStateManager.Instance != null)
            {
                WorldStateManager.Instance.SetDialoguePlayed(choice.dialogueFact);
                GameLog.Info(TAG, $"Choice '{choice.text}' marked as played via fact '{choice.dialogueFact}'");
            }

            if (choice.signal != null)
            {
                if (_onDialogueSignal != null)
                    _onDialogueSignal.Raise(new DialogueSignalData { signal = choice.signal, npcName = _currentNPCName });
                else
                    GameLog.Warn(TAG, $"Choice '{choice.text}' carries signal '{choice.signal.name}' but _onDialogueSignal is not assigned");
            }

            if (choice.nextNode != null)
                AdvanceToNode(choice.nextNode);
            else
                NotifyTopicCompleted();
        }

        /// <summary>True when the player holds at least one item of <paramref name="category"/> (a ChoiceOption.requiredCategory).</summary>
        public bool HasChoiceCategory(GiveItemCategory category)
        {
            if (category == GiveItemCategory.None) return true;
            if (_playerInventory == null) return false;
            foreach (var slot in _playerInventory.Items)
                if (GiveItemRules.Matches(slot.Item, category)) return true;
            return false;
        }

        /// <summary>True when the player holds what <paramref name="choice"/> hands over: its requiredItem, else its category.</summary>
        public bool HasChoiceGoods(ChoiceOption choice)
        {
            if (choice == null) return false;
            if (choice.requiredItem != null) return HasChoiceItem(choice.requiredItem);
            return HasChoiceCategory(choice.requiredCategory);
        }

        // ChoiceOption.hideWhenMissing: the row disappears while the player doesn't hold its item / category.
        private ChoiceOption[] FilterMissingGoodsChoices(ChoiceOption[] choices)
        {
            if (choices == null) return System.Array.Empty<ChoiceOption>();
            var kept = new System.Collections.Generic.List<ChoiceOption>(choices.Length);
            foreach (var c in choices)
                if (c != null && !(c.hideWhenMissing && !HasChoiceGoods(c))) kept.Add(c);
            return kept.Count == choices.Length ? choices : kept.ToArray();
        }

        // A category choice's item goes to the NPC (lootable back), unlike requiredItem which is consumed.
        private bool GiveItemToNpc(ItemSO item)
        {
            if (!_playerInventory.TryConsume(item))
            {
                GameLog.Warn(TAG, $"{item.itemName} couldn't be taken from the player");
                return false;
            }
            if (_currentNPCInventory == null)
                GameLog.Warn(TAG, $"{_currentNPCName} has no InventorySystem — {item.itemName} is consumed");
            else if (!_currentNPCInventory.AddItem(item))
            {
                _playerInventory.AddItem(item); // the NPC can't take it: give it back, nothing applied
                GameLog.Warn(TAG, $"{_currentNPCName}'s inventory refused {item.itemName} — returned to the player");
                return false;
            }
            GameLog.Info(TAG, $"Player gave {item.itemName} to {_currentNPCName}");
            return true;
        }

        /// <summary>True when the player has at least <paramref name="amount"/> gold (a ChoiceOption.goldCost).</summary>
        public bool CanAffordGold(int amount) => amount <= 0 || (_goldSystem != null && _goldSystem.Gold >= amount);

        /// <summary>True when the player holds at least one <paramref name="item"/> (a ChoiceOption.requiredItem).</summary>
        public bool HasChoiceItem(ItemSO item) =>
            item == null || ChoiceItemRules.Locate(BagCount(item), IsWorn(item)) != ChoiceItemSource.None;

        private int BagCount(ItemSO item) => _playerInventory != null ? _playerInventory.CountOf(item) : 0;
        private bool IsWorn(ItemSO item) => _playerEquipment != null && _playerEquipment.IsEquipped(item);

        // The requiredItem leaves the player: consumed, or into the NPC's inventory (giveToNpc). A worn copy is unequipped
        // first (back into the bag) and worn again if the hand-over fails, so a refused choice changes nothing.
        private bool HandOverChoiceItem(ChoiceOption choice)
        {
            ItemSO item = choice.requiredItem;
            bool unequipped = false;
            if (ChoiceItemRules.Locate(BagCount(item), IsWorn(item)) == ChoiceItemSource.Equipped)
            {
                foreach (var pair in _playerEquipment.Equipped)
                {
                    if (pair.Value != item) continue;
                    _playerEquipment.Unequip(pair.Key);
                    unequipped = true;
                    break;
                }
                if (BagCount(item) <= 0)
                {
                    GameLog.Warn(TAG, $"{item.itemName} couldn't be unequipped into the inventory — choice refused");
                    return false;
                }
            }
            if (choice.giveToNpc ? GiveItemToNpc(item) : TakeChoiceItem(item)) return true;
            if (unequipped) _playerEquipment.Equip(item);
            return false;
        }

        // The player hands the item over: one unit leaves the player's inventory (consumed, not given to the NPC).
        private bool TakeChoiceItem(ItemSO item)
        {
            if (_playerInventory == null)
            {
                GameLog.Error(TAG, $"Choice needs {item.itemName} but _playerInventory is not assigned — choice refused");
                return false;
            }
            if (!_playerInventory.TryConsume(item))
            {
                GameLog.Warn(TAG, $"Choice needs {item.itemName} — it couldn't be taken from the player");
                return false;
            }
            GameLog.Info(TAG, $"Player handed {item.itemName} to {_currentNPCName}");
            return true;
        }

        // The player pays the NPC: deducted from the player's GoldSystem, coins added to the NPC's inventory (lootable back).
        private bool PayChoiceGold(int amount)
        {
            if (_goldSystem == null)
            {
                GameLog.Error(TAG, $"Choice costs {amount} gold but GoldSystem is not assigned — choice refused");
                return false;
            }
            if (!_goldSystem.TrySpend(amount))
            {
                GameLog.Warn(TAG, $"Choice costs {amount} gold — the player can't pay it");
                return false;
            }
            if (_currentNPCInventory != null && _goldItem != null)
                _currentNPCInventory.AddItems(_goldItem, amount);
            else
                GameLog.Warn(TAG, $"Paid {amount} gold but the NPC has no InventorySystem or _goldItem is unassigned — coins not stored");
            GameLog.Info(TAG, $"Player paid {amount} gold to {_currentNPCName}");
            return true;
        }

        /// <summary>
        /// Returns available start nodes for the current NPC, re-evaluated to pick up
        /// played-state changes that occurred during this session.
        /// Called by DialogueUI when restoring the topic list after a chain completes.
        /// </summary>
        public StartDialogueNode[] GetCurrentStartNodes()
        {
            if (_currentGraph == null) return System.Array.Empty<StartDialogueNode>();
            return _currentGraph.GetAvailableStartNodes(_currentNPCMemory);
        }

        /// <summary>
        /// Returns true if the player currently meets the gold, LP, and stat requirements for the choice.
        /// Called by DialogueUI to set button interactable state.
        /// Does NOT check whether a skill is already learned — use requiredMemory authoring for that.
        /// </summary>
        public bool CanAffordTeachChoice(TeachChoiceOption choice) =>
            choice != null && GetTeachCostShortfall(choice) == TeachCostShortfall.None;

        /// <summary>Which of the choice's costs (gold / LP / stat requirements) the player can't currently meet.</summary>
        public TeachCostShortfall GetTeachCostShortfall(TeachChoiceOption choice)
        {
            var shortfall = TeachCostShortfall.None;
            if (choice == null) return shortfall;

            if (choice.goldCost > 0 && (_goldSystem == null || _goldSystem.Gold < choice.goldCost))
                shortfall |= TeachCostShortfall.Gold;

            int effectiveLpCost = choice.skill != null ? choice.skill.lpCost : choice.statPoints;
            if (effectiveLpCost > 0 && (_lpSystem == null || _lpSystem.CurrentLP < effectiveLpCost))
                shortfall |= TeachCostShortfall.LearningPoints;

            // Stat requirements only apply to skill choices (stat path has no prerequisites).
            if (choice.skill != null && choice.skill.statsRequirements.Count > 0)
            {
                if (_playerStats == null || !_playerStats.ValidateStats(choice.skill.statsRequirements))
                    shortfall |= TeachCostShortfall.Stats;
            }

            return shortfall;
        }

        // ── Seen state (persisted, no events) ────────────────────────────────

        /// <summary>True once the player has picked this topic (in any session of this save).</summary>
        public bool IsTopicSeen(StartDialogueNode node) => node != null && IsSeen(node, node.Id);

        /// <summary>True once the player has picked this choice of <paramref name="node"/>.</summary>
        public bool IsChoiceSeen(ChoiceDialogueNode node, ChoiceOption choice) =>
            node != null && IsSeen(node, node.ChoiceSeenId(choice));

        private bool IsSeen(DialogueNode node, string id)
        {
            if (!HasId(node, id)) return false;
            return WorldStateManager.Instance != null && WorldStateManager.Instance.IsDialogueSeen(id);
        }

        private void MarkSeen(DialogueNode node, string id)
        {
            if (!HasId(node, id) || WorldStateManager.Instance == null) return;
            WorldStateManager.Instance.MarkDialogueSeen(id);
        }

        // Empty id = asset never validated in the editor (or choice not part of the node): not tracked, warned once.
        private bool HasId(DialogueNode node, string id)
        {
            if (!string.IsNullOrEmpty(id)) return true;
            if (_warnedNoId.Add(node))
                GameLog.Warn(TAG, $"Dialogue node '{node.name}' has no id — seen state not tracked (reimport / validate the asset)");
            return false;
        }

        /// <summary>
        /// Deducts gold cost, then delegates to LearnSkill() (skill path) or TrySpendLP+UpgradeStat (stat path).
        /// Must only be called after CanAffordTeachChoice() returned true.
        /// Advances to choice.confirmNextNode on success (null = close dialogue), or denyNextNode when stat is at cap.
        /// </summary>
        public void ApplyTeachChoice(TeachChoiceOption choice)
        {
            if (choice == null) return;

            // ── Skill path ───────────────────────────────────────────────────────────
            if (choice.skill != null)
            {
                if (_playerSkills == null)
                {
                    GameLog.Warn(TAG, "ApplyTeachChoice: PlayerSkills not assigned — skill not granted");
                    AdvanceToNode(choice.confirmNextNode);
                    return;
                }

                // Guard: skill already learned (author omitted requiredMemory gate).
                // Check BEFORE spending gold so no currency is lost.
                if (_playerSkills.HasSkill(choice.skill.skillId))
                {
                    GameLog.Warn(TAG, $"ApplyTeachChoice: skill '{choice.skill.displayName}' already learned — requiredMemory gate missing on this TeachChoiceOption");
                    AdvanceToNode(choice.confirmNextNode);
                    return;
                }

                // Spend gold first, then let LearnSkill() handle LP deduction.
                if (choice.goldCost > 0)
                {
                    if (_goldSystem == null)
                        GameLog.Warn(TAG, "ApplyTeachChoice: GoldSystem not assigned — gold cost skipped");
                    else if (!_goldSystem.TrySpend(choice.goldCost))
                    {
                        GameLog.Error(TAG, $"ApplyTeachChoice: TrySpend({choice.goldCost}) failed — insufficient funds at apply time");
                        return;
                    }
                }

                if (!_playerSkills.LearnSkill(choice.skill))
                    GameLog.Error(TAG, $"ApplyTeachChoice: LearnSkill({choice.skill.displayName}) failed unexpectedly");
            }
            // ── Stat path ────────────────────────────────────────────────────────────
            else
            {
                // Stat cap check — no resources consumed if at or above limit
                if (choice.limitStat > 0)
                {
                    if (_playerStats == null)
                    {
                        GameLog.Warn(TAG, "ApplyTeachChoice: PlayerStats not assigned — cannot evaluate limitStat; proceeding without cap check");
                    }
                    else
                    {
                        int baseStat = _playerStats.GetBaseStat(choice.statToUpgrade);
                        if (baseStat >= choice.limitStat)
                        {
                            if (choice.denyNextNode == null)
                                GameLog.Warn(TAG, $"ApplyTeachChoice: {choice.statToUpgrade} at cap ({baseStat}/{choice.limitStat}) but denyNextNode is null — closing dialogue");
                            else
                                GameLog.Info(TAG, $"ApplyTeachChoice: {choice.statToUpgrade} at cap ({baseStat}/{choice.limitStat}) — routing to denyNextNode");
                            AdvanceToNode(choice.denyNextNode);
                            return;
                        }
                    }
                }

                if (choice.goldCost > 0)
                {
                    if (_goldSystem == null)
                        GameLog.Warn(TAG, "ApplyTeachChoice: GoldSystem not assigned — gold cost skipped");
                    else if (!_goldSystem.TrySpend(choice.goldCost))
                    {
                        GameLog.Error(TAG, $"ApplyTeachChoice: TrySpend({choice.goldCost}) failed — insufficient funds at apply time");
                        return;
                    }
                }

                if (!_lpSystem.TrySpendLP(choice.statPoints))
                {
                    GameLog.Error(TAG, $"ApplyTeachChoice: TrySpendLP({choice.statPoints}) failed — insufficient LP at apply time");
                    return;
                }

                if (_playerStats == null)
                    GameLog.Warn(TAG, "ApplyTeachChoice: PlayerStats not assigned — stat not upgraded");
                else
                    _playerStats.UpgradeStat(choice.statToUpgrade, choice.statPoints);
            }

            AdvanceToNode(choice.confirmNextNode);
        }

        // One runtime text node reused for every opening line (never an asset: no id, no seen state).
        private TextDialogueNode GetOpeningLineNode(string line, DialogueNode next)
        {
            if (_openingLineNode == null)
            {
                _openingLineNode = ScriptableObject.CreateInstance<TextDialogueNode>();
                _openingLineNode.name = "RuntimeOpeningLine";
                _openingLineNode.hideFlags = HideFlags.DontSave;
            }
            _openingLineNode.text = line;
            _openingLineNode.nextNode = next;
            return _openingLineNode;
        }

        private void OnDestroy()
        {
            if (_openingLineNode != null) Destroy(_openingLineNode);
        }

        private void ClearContext()
        {
            _currentNPCMemory = null;
            _currentGraph = null;
            _currentNPCInventory = null;
            _currentNPCGoldSystem = null;
            _currentStartNode = null;
            _currentNPCName = null;
            _currentRequester = null;
        }

        private TeachChoiceOption[] FilterLearnedSkillChoices(TeachChoiceOption[] choices)
        {
            if (_playerSkills == null || choices.Length == 0) return choices;
            var result = new System.Collections.Generic.List<TeachChoiceOption>(choices.Length);
            foreach (var choice in choices)
            {
                if (choice == null) continue;
                if (choice.teachingType == TeachingType.SkillBased && choice.skill != null && _playerSkills.HasSkill(choice.skill.skillId))
                    continue;
                result.Add(choice);
            }
            return result.Count == choices.Length ? choices : result.ToArray();
        }

        public void Close()
        {
            if (_giveItemUI != null && _giveItemUI.IsOpen) _giveItemUI.Close();
            if (_dialogueUI == null) return;
            _dialogueUI.Close();
            IsOpen = false;
            if (_playerStateManager != null)
                _playerStateManager.SetInDialogue(false);
            ClearContext();
            CursorManager.Lock();
            GameLog.Info(TAG, "Closed dialogue");
        }
    }
}
