using UnityEngine;

namespace Game.Core
{
    /// <summary>Key format: World.{eventKey}</summary>
    [CreateAssetMenu(menuName = "Game/Facts/World Fact", fileName = "WorldFact_")]
    public class WorldFact : Fact
    {
        [SerializeField] private string _eventKey;

        public WorldFact Init(string eventKey)
        {
            Prefix = WorldFactPrefix.World;
            _eventKey = eventKey;
            return this;
        }

        private void OnEnable() => Prefix = WorldFactPrefix.World;

        public override string ToString() => $"{WorldFactPrefix.World}.{_eventKey}";
    }
}
