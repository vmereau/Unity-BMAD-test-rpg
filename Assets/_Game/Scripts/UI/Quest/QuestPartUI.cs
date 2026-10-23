using Game.Core;
using Game.Quest;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    public class QuestPartUI : MonoBehaviour
    {
        private const string TAG = "[QuestPartUI]";

        [SerializeField] private TMP_Text _entryText;
        [Tooltip("Prepended to the entry so parts read as journal lines.")]
        [SerializeField] private string _bulletPrefix = "• ";

        public void Bind(QuestPart part)
        {
            if (_entryText == null) { GameLog.Warn(TAG, "_entryText is not assigned."); return; }
            // <indent> aligns wrapped lines under the text, not under the bullet
            _entryText.text = $"{_bulletPrefix}<indent=0.8em>{part.entry}</indent>";
        }
    }
}
