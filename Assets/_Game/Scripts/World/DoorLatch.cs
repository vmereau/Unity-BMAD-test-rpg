using Game.AI;
using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Inner-face latch of a locked door: from the latch side, "Lift the latch" unlocks and opens the target door with
    /// no skill, key or lockpick, so nobody is trapped behind it. Interactable only while the door is locked and the
    /// player stands on the latch side (in front of this transform's +Z), so it can't be reached through the door.
    /// Same-system direct calls into <see cref="DoorInteractable"/>. Needs its own collider on the Interactable layer.
    /// </summary>
    public class DoorLatch : MonoBehaviour, IInteractable
    {
        private const string TAG = "[DoorLatch]";

        [SerializeField] private DoorInteractable _door;
        [SerializeField] private string _interactPrompt = "Lift the latch";

        private Lockable _lockable;

        public string InteractPrompt => _interactPrompt;
        public string NameTag => "";

        public bool CanInteract
        {
            get
            {
                if (_door == null || _lockable == null || !_lockable.IsLocked) return false;
                var player = TargetRegistry.Player;
                return player != null && IsOnLatchSide(transform.position, transform.forward, player.transform.position);
            }
        }

        private void Awake()
        {
            if (_door == null)
            {
                GameLog.Error(TAG, $"{name}: no door assigned — disabled.");
                enabled = false;
                return;
            }
            _lockable = _door.GetComponent<Lockable>();
            if (_lockable == null)
                GameLog.Warn(TAG, $"{name}: door '{_door.name}' has no Lockable — the latch is never needed");
        }

        public void Interact()
        {
            if (!CanInteract) return;
            _door.Unlock();
            if (!_door.IsOpen) _door.ToggleOpen();
            GameLog.Info(TAG, $"{_door.name} unlatched from the inside");
        }

        /// <summary>Pure: true when <paramref name="point"/> is in front of the latch (its +Z side).</summary>
        public static bool IsOnLatchSide(Vector3 latchPosition, Vector3 latchForward, Vector3 point) =>
            Vector3.Dot(point - latchPosition, latchForward) > 0f;
    }
}
