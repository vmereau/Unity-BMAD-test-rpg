using Game.Core;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Receives attack-clip Animation Events for AI entities and routes them to
    /// <see cref="EntityMeleeAttacker"/>. Must sit on the <b>Animator's GameObject</b> (for monsters:
    /// <c>CreatureVisual</c>, not the entity root) — Unity only delivers events there.
    /// Separate from the player's <c>AnimationEventReceiver</c> so <c>HitboxEnable(string)</c> never
    /// clashes with the player's parameterless overload. An event with no string parameter delivers
    /// <c>""</c> → all hitboxes.
    /// </summary>
    public class EntityAnimationEventReceiver : MonoBehaviour
    {
        private const string TAG = "[AI]";

        [Tooltip("Auto-resolved from the parents if null.")]
        [SerializeField] private EntityMeleeAttacker _attacker;

        private void Awake()
        {
            if (_attacker == null) _attacker = GetComponentInParent<EntityMeleeAttacker>();
            if (_attacker == null)
                GameLog.Warn(TAG, $"{gameObject.name}: no EntityMeleeAttacker in parents — hitbox events will be no-ops");
        }

        // Called from attack clips at the frame the hit window opens. Empty id = all hitboxes.
        public void HitboxEnable(string id)
        {
            if (_attacker == null) return;
            _attacker.OpenWindow(id);
        }

        // Called from attack clips at the frame the hit window closes. Empty id = all hitboxes.
        public void HitboxDisable(string id)
        {
            if (_attacker == null) return;
            _attacker.CloseWindow(id);
        }

        // Called by SMB_EntityAttackState.OnStateEnter — counts the active attack state.
        public void NotifyAttackEntered()
        {
            if (_attacker == null) return;
            _attacker.NotifyAttackStateEntered();
        }

        // Called by SMB_EntityAttackState.OnStateExit — the attack ends (all windows closed) once the last
        // active attack state exits: normal end, GetHit interrupt, death. Combo crossfades keep it alive.
        public void NotifyAttackExited()
        {
            if (_attacker == null) return;
            _attacker.NotifyAttackStateExited();
        }

        // Called from attack clips when the next combo step may be requested (humanoid combos).
        public void ComboWindowOpen()
        {
            if (_attacker == null) return;
            _attacker.OnComboWindowOpen();
        }

        // Player attack clips also fire this — no-op (the next step is requested on open).
        public void ComboWindowClose() { }
    }
}
