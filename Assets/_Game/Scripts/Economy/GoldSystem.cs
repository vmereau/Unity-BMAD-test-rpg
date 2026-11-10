using Game.Core;
using UnityEngine;

namespace Game.Economy
{
    /// <summary>
    /// Minimal gold tracker for Epic 3 prototype. Holds the player's current gold total.
    /// Full economy integration (shops, loot, bribes) deferred to Epic 6.
    /// Story 3.4: Initial implementation.
    /// </summary>
    public class GoldSystem : MonoBehaviour
    {
        private const string TAG = "[Economy]";

        [SerializeField] private int _startingGold = 500;
        [SerializeField] private GameEventSO_Int _onGoldChanged;
        [Tooltip("Player only. Must match the inventory's gold collection channel.")]
        [SerializeField] private GameEventSO_GoldLootRequest _onGoldLootRequested;

        public int Gold { get; private set; }

#if false // DISABLED: debug OnGUI — to be reworked
        private GUIStyle _guiStyle;
#endif

        private void Awake()
        {
            if (_onGoldChanged == null)
                GameLog.Warn(TAG, "OnGoldChanged event not assigned — gold change signals will be silent.");

            Gold = _startingGold;
        }

        private void OnEnable() => _onGoldLootRequested?.AddListener(HandleGoldLootRequested);

        private void OnDisable() => _onGoldLootRequested?.RemoveListener(HandleGoldLootRequested);

        private void HandleGoldLootRequested(GoldLootRequest request)
        {
            if (request == null || request.Recipient != gameObject || request.Accepted) return;
            if (request.Amount <= 0 || Gold > int.MaxValue - request.Amount)
            {
                GameLog.Warn(TAG, "Gold collection rejected: amount is invalid or balance would overflow.");
                return;
            }
            if (!request.TryAccept()) return;
            Gold += request.Amount;
            GameLog.Info(TAG, $"Looted {request.Amount} gold. Total: {Gold}");
            _onGoldChanged?.Raise(Gold);
        }

        /// <summary>
        /// Attempts to spend the given amount. Returns false if insufficient funds.
        /// Deducts gold, logs, and raises the OnGoldChanged event on success.
        /// </summary>
        public bool TrySpend(int amount)
        {
            if (amount <= 0 || Gold < amount)
            {
                GameLog.Warn(TAG, $"TrySpend failed: requested {amount}, have {Gold}");
                return false;
            }

            Gold -= amount;
            GameLog.Info(TAG, $"Spent {amount} gold. Remaining: {Gold}");
            _onGoldChanged?.Raise(Gold);
            return true;
        }

        /// <summary>Adds gold to the total and raises the OnGoldChanged event.</summary>
        public void Add(int amount)
        {
            if (amount <= 0)
            {
                GameLog.Warn(TAG, $"Add called with non-positive amount: {amount} — ignored.");
                return;
            }

            Gold += amount;
            GameLog.Info(TAG, $"Gained {amount} gold. Total: {Gold}");
            _onGoldChanged?.Raise(Gold);
        }

        /// <summary>Save/load: sets gold directly (clamped to >= 0) and raises OnGoldChanged.</summary>
        public void RestoreGold(int gold)
        {
            Gold = Mathf.Max(0, gold);
            _onGoldChanged?.Raise(Gold);
        }

#if false // DISABLED: debug OnGUI — to be reworked
        private void OnGUI()
        {
            if (_guiStyle == null) _guiStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            GUI.Label(new Rect(10, 370, 300, 26), $"Gold: {Gold}", _guiStyle);
        }
#endif
    }
}
