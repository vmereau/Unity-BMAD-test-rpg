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

        public void Bind(QuestPart part)
        {
            if (_entryText == null) { GameLog.Warn(TAG, "_entryText is not assigned."); return; }
            _entryText.text = part.entry;
        }
    }
}
