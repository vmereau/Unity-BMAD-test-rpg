using UnityEngine;

namespace Game.Core
{
    /// <summary>Key format: Dialogue.Played.{nodeId}</summary>
    [CreateAssetMenu(menuName = "Game/Facts/Dialogue Fact", fileName = "DialogueFact_")]
    public class DialogueFact : Fact
    {
        [SerializeField] private string _nodeId;

        public DialogueFact Init(string nodeId)
        {
            Prefix = WorldFactPrefix.Dialogue;
            _nodeId = nodeId;
            return this;
        }

        private void OnEnable() => Prefix = WorldFactPrefix.Dialogue;

        public override string ToString() => $"{WorldFactPrefix.Dialogue}.Played.{_nodeId}";
    }
}
