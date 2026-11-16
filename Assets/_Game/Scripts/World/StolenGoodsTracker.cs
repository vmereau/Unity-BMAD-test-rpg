using Game.Core;
using Game.Inventory;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Records what each theft took into <see cref="StolenGoodsLedger"/> (spec-theft-return-or-fight). On the Player.
    /// Item pickup / harvest: the item × its count (recorded at the theft — a refused pickup is rare and harmless: only
    /// goods still carried are ever returned). Owned container: the theft only marks the container — every unit then taken from it
    /// is recorded under that theft id (gold as an amount) until any other container is opened
    /// (or this one reopened without a theft). A save load forgets every theft.
    /// </summary>
    public class StolenGoodsTracker : MonoBehaviour
    {
        private const string TAG = "[StolenGoodsTracker]";

        [Tooltip("Listened to: every theft (OnTheftCommitted).")]
        [SerializeField] private GameEventSO_TheftCommitted _onTheftCommitted;
        [Tooltip("Listened to: every unit taken from a container (OnContainerItemTaken).")]
        [SerializeField] private GameEventSO_ContainerItemTaken _onContainerItemTaken;
        [Tooltip("Listened to: a container opening without a theft just before it ends the stolen-container session.")]
        [SerializeField] private GameEventSO_ContainerOpenRequest _onContainerOpenRequested;
        [Tooltip("Listened to: a save load forgets every recorded theft (the inventory is restored).")]
        [SerializeField] private GameEventSO_Void _onLoadStarted;

        private InventorySystem _stolenContainer;
        private int _containerTheftId;
        private bool _awaitingStolenOpen; // the theft fires before its own open request

        private void Awake()
        {
            if (_onTheftCommitted == null || _onContainerItemTaken == null || _onContainerOpenRequested == null)
                GameLog.Warn(TAG, $"{name}: theft / container-take channels not assigned — stolen goods won't be recorded (nothing to give back)");
        }

        private void OnEnable()
        {
            _onTheftCommitted?.AddListener(HandleTheftCommitted);
            _onContainerItemTaken?.AddListener(HandleContainerItemTaken);
            _onContainerOpenRequested?.AddListener(HandleContainerOpenRequested);
            _onLoadStarted?.AddListener(HandleLoadStarted);
        }

        private void OnDisable()
        {
            _onTheftCommitted?.RemoveListener(HandleTheftCommitted);
            _onContainerItemTaken?.RemoveListener(HandleContainerItemTaken);
            _onContainerOpenRequested?.RemoveListener(HandleContainerOpenRequested);
            _onLoadStarted?.RemoveListener(HandleLoadStarted);
        }

        private void HandleTheftCommitted(TheftCommittedData data)
        {
            if (data.item != null)
                for (int i = 0; i < Mathf.Max(1, data.itemCount); i++) RecordUnit(data.theftId, data.item);
            if (data.sourceInventory != null)
            {
                _stolenContainer = data.sourceInventory;
                _containerTheftId = data.theftId;
                _awaitingStolenOpen = true;
            }
        }

        // The stolen session is the open right after its theft; any other open (another container, or this one reopened
        // legally — owner dead) ends it, so later takes aren't counted as stolen.
        private void HandleContainerOpenRequested(ContainerOpenRequestData data)
        {
            if (_awaitingStolenOpen && data.containerInventory == _stolenContainer)
            {
                _awaitingStolenOpen = false;
                return;
            }
            _stolenContainer = null;
            _awaitingStolenOpen = false;
        }

        private void HandleLoadStarted(bool _)
        {
            _stolenContainer = null;
            _awaitingStolenOpen = false;
            StolenGoodsLedger.ClearAll();
        }

        private void HandleContainerItemTaken(ContainerItemTakenData data)
        {
            if (_stolenContainer == null || data.sourceInventory != _stolenContainer || data.item == null) return;
            RecordUnit(_containerTheftId, data.item);
        }

        private static void RecordUnit(int theftId, ItemSO item)
        {
            if (item is GoldItemSO gold) StolenGoodsLedger.RecordGold(theftId, gold.GoldValue);
            else StolenGoodsLedger.Record(theftId, item);
        }
    }
}
