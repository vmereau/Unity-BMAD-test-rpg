using Game.Core;
using Game.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class ContainerDetailActions : MonoBehaviour
    {
        private const string TAG = "[ContainerDetailActions]";

        [SerializeField] private Button _takeButton;
        [SerializeField] private Button _putButton;

        // Cached lazily: Bind can run before Awake while the panel is still inactive.
        private TMP_Text _takeLabel;
        private TMP_Text _putLabel;

        /// <param name="quantity">Units the click moves (Shift bulk) — only drives the "Take ×N" label; the owner
        /// recomputes the batch when the button is clicked.</param>
        public void Bind(ContainerUI owner, int slotIndex, ItemSO item, ContainerSide side, bool takeOnly, int quantity = 1)
        {
            if (item == null || owner == null)
            {
                GameLog.Warn(TAG, "Bind: item or owner is null");
                return;
            }

            if (_takeButton != null) _takeButton.onClick.RemoveAllListeners();
            if (_putButton != null) _putButton.onClick.RemoveAllListeners();

            if (side == ContainerSide.Container)
            {
                if (_takeButton != null)
                {
                    _takeButton.gameObject.SetActive(true);
                    if (_takeLabel == null) _takeLabel = _takeButton.GetComponentInChildren<TMP_Text>(true);
                    if (_takeLabel != null) _takeLabel.text = BulkTransferRules.ActionLabel("Take", quantity);
                    _takeButton.onClick.AddListener(() => owner.TakeBatch(slotIndex));
                }
                if (_putButton != null) _putButton.gameObject.SetActive(false);
            }
            else // player side
            {
                if (_takeButton != null) _takeButton.gameObject.SetActive(false);
                if (_putButton != null)
                {
                    bool showPut = !takeOnly && ItemSO.CanPlayerPart(item); // quest items stay with the player
                    _putButton.gameObject.SetActive(showPut);
                    if (!showPut) return;
                    if (_putLabel == null) _putLabel = _putButton.GetComponentInChildren<TMP_Text>(true);
                    if (_putLabel != null) _putLabel.text = BulkTransferRules.ActionLabel("Put", quantity);
                    _putButton.onClick.AddListener(() => owner.PutBatch(slotIndex));
                }
            }
        }
    }
}
