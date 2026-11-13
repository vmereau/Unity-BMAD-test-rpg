using System.Collections.Generic;
using Game.Core;
using Game.Dialogue;
using Game.NPC;
using Game.World;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// A camp exit where a racket collector (Harl) stops the player for a one-time protection fee (spec-racket-guard-exit-toll).
    /// Zone = every <see cref="BoxCollider"/> on this GO and its children (span the whole gate; containment computed on
    /// demand, no trigger events). While in hours, unpaid, unbeaten, re-armed and with the collector on duty at
    /// <c>_post</c>, a player entering the zone is intercepted (<see cref="EntityBrain.BeginIntercept"/>) and handed a
    /// forced toll topic. The answer comes back as a <see cref="DialogueSignalSO"/>: pay → this gate's paid fact; can't pay →
    /// the racket's shared deferred fact; refuse / no answer → <see cref="EntityBrain.StartBrawl"/>. Losing the brawl pays
    /// this gate (the collector robs the player through the normal knockout looting); knocking the collector out in any fight
    /// with the player sets the racket's beaten fact (no gate collects again). After a non-fight ending the gate re-arms only
    /// once the player has left the zone. Runtime state is not saved; the facts are.
    /// </summary>
    public class TollGate : MonoBehaviour
    {
        private const string TAG = "[Toll]";

        private enum Phase { Watching, Approaching, Talking, Brawl }

        private static readonly List<TollGate> _active = new List<TollGate>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _active.Clear();
            s_answeringGate = null;
        }

        [SerializeField] private TollRacketSO _racket;
        [Tooltip("The collector (Harl's scene instance).")]
        [SerializeField] private EntityBrain _collector;
        [Tooltip("Where the collector stands on duty for this gate (his routine spot). Null = this transform.")]
        [SerializeField] private Transform _post;
        [Tooltip("Set once the toll is paid (or taken by force) at this gate.")]
        [SerializeField] private WorldFact _paidFact;
        [Tooltip("Collecting hours: start (inclusive) → end (exclusive), wraps midnight. Always without a GameClock.")]
        [SerializeField, Range(0, 23)] private int _startHour = 6;
        [SerializeField, Range(0, 23)] private int _endHour = 14;
        [Tooltip("Listened to: the answer picked in the toll dialogue (OnDialogueSignal).")]
        [SerializeField] private GameEventSO_DialogueSignal _onDialogueSignal;

        private BoxCollider[] _boxes = System.Array.Empty<BoxCollider>();
        private EntityHealth _collectorHealth;
        private NPCPresence _collectorPresence;
        // The gate whose toll dialogue was opened last: pay / defer answers go to it even if its encounter ended meanwhile
        // (collector hit mid-talk), because the dialogue may still be open and the gold is taken on the pick.
        private static TollGate s_answeringGate;

        private Phase _phase = Phase.Watching;
        private bool _armed = true;
        private bool _answered; // pay / defer picked in the open toll dialogue (applied at once)
        private bool _collectorFoughtPlayerLastFrame;
        private bool _subscribedKnockout;
        private bool _valid;

        private Transform Post => _post != null ? _post : transform;

        private void Awake()
        {
            _boxes = GetComponentsInChildren<BoxCollider>(true);
            if (_boxes.Length == 0) GameLog.Warn(TAG, $"{name}: no BoxCollider zone — the gate never stops anyone");
            if (_collector != null)
            {
                _collectorHealth = _collector.GetComponent<EntityHealth>();
                _collectorPresence = _collector.GetComponent<NPCPresence>();
            }
            _valid = _racket != null && _collector != null && _collectorHealth != null && _collectorPresence != null &&
                     _paidFact != null && _onDialogueSignal != null && _racket.firstStart != null;
            if (!_valid)
                GameLog.Error(TAG, $"{name}: needs a racket (with firstStart), a collector with EntityHealth + NPCPresence, a paid fact and _onDialogueSignal — gate disabled");
        }

        private void OnEnable()
        {
            _active.Add(this);
            if (!_valid) return;
            _onDialogueSignal.AddListener(HandleDialogueSignal);
            // Same system (AI), same-GO-style C# event on the collector's health.
            _collectorHealth.KnockedOut += HandleCollectorKnockedOut;
            _subscribedKnockout = true;
        }

        private void OnDisable()
        {
            _active.Remove(this);
            if (!_valid) return;
            _onDialogueSignal.RemoveListener(HandleDialogueSignal);
            if (_subscribedKnockout) _collectorHealth.KnockedOut -= HandleCollectorKnockedOut;
            _subscribedKnockout = false;
            // Region unload mid-talk: the Core-owned dialogue outlives Harl — close his toll panel (no gate hears a late pick).
            if (_phase == Phase.Talking) _collectorPresence.RequestDialogueClose();
            if (_phase == Phase.Approaching || _phase == Phase.Talking) _collector.EndIntercept();
            _phase = Phase.Watching;
            if (s_answeringGate == this) s_answeringGate = null;
        }

        private void Update()
        {
            if (!_valid) return;
            FactionMember player = TargetRegistry.Player;
            if (player == null || player.Damageable == null) return;
            bool foughtLastFrame = _collectorFoughtPlayerLastFrame;
            _collectorFoughtPlayerLastFrame = _collector.IsFighting(player);

            switch (_phase)
            {
                case Phase.Watching:    TickWatching(player);    break;
                case Phase.Approaching: TickApproaching(player); break;
                case Phase.Talking:     TickTalking(player);     break;
                case Phase.Brawl:       TickBrawl(player, foughtLastFrame); break;
            }
        }

        // --- Phases ---

        private void TickWatching(FactionMember player)
        {
            bool inZone = Contains(player.Transform.position);
            if (!inZone)
            {
                _armed = true;
                return;
            }
            if (player.Damageable.IsDead || player.Damageable.IsUnconscious || !CursorManager.IsLocked) return;

            WorldStateManager wsm = WorldStateManager.Instance;
            if (wsm == null) return;
            GameClock clock = GameClock.Active;
            bool inHours = TollGateRules.IsCollectingHour(clock != null, clock != null ? clock.Hour : 0, _startHour, _endHour);
            bool beaten = _racket.beatenFact != null && wsm.GetFact(_racket.beatenFact);
            if (!TollGateRules.CanCollect(inHours, wsm.GetFact(_paidFact), beaten, _armed, IsCollectorOnDuty())) return;

            if (!_collector.BeginIntercept(player, _racket.talkDistance, _racket.interceptTimeout)) return;
            _phase = Phase.Approaching;
            GameLog.Info(TAG, $"{name}: unpaid player in the zone — {_collector.name} intercepts");
        }

        private void TickApproaching(FactionMember player)
        {
            switch (_collector.Intercept)
            {
                case EntityBrain.InterceptStatus.Reached:
                    OpenTollDialogue();
                    return;
                case EntityBrain.InterceptStatus.Approaching:
                    // Walking past without answering (out of the zone, or leading him away from his post) = refusing.
                    if (!Contains(player.Transform.position) ||
                        FlatDistance(_collector.transform.position, Post.position) > _racket.dutyRadius)
                        StartBrawlOrEnd(player, "the player walked past without paying");
                    return;
                default: // Aborted (hit, hostile, timeout, own knockout) or cleared elsewhere
                    _collector.ClearIntercept();
                    if (_collector.IsFighting(player)) { EnterBrawl(); return; }
                    // Not the player's doing (timeout, a hostile, a third party): stay armed, he tries again.
                    EndEncounter("intercept aborted", rearm: true);
                    return;
            }
        }

        private void OpenTollDialogue()
        {
            // A menu opened since he reached the player: wait (the intercept times out if it stays open).
            if (!CursorManager.IsLocked) return;

            bool otherPaid = TollGateRules.UseOtherGateOpener(AnyOtherGatePaid());
            StartDialogueNode start = otherPaid && _racket.otherGatePaidStart != null ? _racket.otherGatePaidStart : _racket.firstStart;
            _answered = false;
            s_answeringGate = this;
            // The request is handled synchronously: an opened dialogue has unlocked the cursor by now. Still locked = it was
            // refused (another dialogue open, misconfigured node), which must never count as refusing the toll.
            if (!_collectorPresence.RequestForcedTopic(start) || CursorManager.IsLocked)
            {
                s_answeringGate = null;
                _collector.EndIntercept();
                EndEncounter("the toll dialogue could not open", rearm: true);
                return;
            }
            _phase = Phase.Talking;
            GameLog.Info(TAG, $"{name}: toll dialogue opened ({(otherPaid ? "other gate paid" : "first")})");
        }

        private void TickTalking(FactionMember player)
        {
            // Hit / disturbed while talking: the intercept ends under us. Close the toll panel he opened (a pay / defer already
            // picked has been applied on the pick); no answer can come in after this.
            if (_collector.Intercept != EntityBrain.InterceptStatus.Reached)
            {
                if (!CursorManager.IsLocked) _collectorPresence.RequestDialogueClose();
                if (s_answeringGate == this) s_answeringGate = null;
                _collector.ClearIntercept();
                if (_collector.IsFighting(player)) { EnterBrawl(); return; }
                EndEncounter("collector disturbed during the toll talk");
                return;
            }

            if (!CursorManager.IsLocked) return; // dialogue still open
            ResolveAnswer(player);
        }

        // Dialogue closed: pay / defer were applied on the pick; anything else (refuse, Escape) is a refusal.
        private void ResolveAnswer(FactionMember player)
        {
            if (_answered)
            {
                _collector.EndIntercept();
                EndEncounter("toll answered");
                return;
            }
            StartBrawlOrEnd(player, "toll refused");
        }

        // Refusal: brawl. False (and the encounter ends peacefully) when the collector can't fight.
        private bool StartBrawlOrEnd(FactionMember player, string reason)
        {
            if (_collector.StartBrawl(player))
            {
                EnterBrawl();
                GameLog.Info(TAG, $"{name}: {reason}: brawl");
                return true;
            }
            _collector.EndIntercept();
            EndEncounter($"{reason} but the collector can't fight");
            return false;
        }

        private void EnterBrawl()
        {
            _phase = Phase.Brawl;
            _armed = false;
        }

        private void TickBrawl(FactionMember player, bool collectorFoughtPlayerLastFrame)
        {
            // Only a knockout during the fight with the collector counts (he goes Looting the frame he lands it, so read the
            // previous frame too); a third party knocking the player out ends the brawl without paying.
            bool collectorInvolved = collectorFoughtPlayerLastFrame || _collector.IsFighting(player);
            TollBrawlResult result = TollGateRules.EvaluateBrawl(player.Damageable.IsUnconscious && collectorInvolved,
                _collectorHealth.IsUnconscious, _collector.IsFighting(player));
            switch (result)
            {
                case TollBrawlResult.Ongoing:
                    return;
                case TollBrawlResult.PlayerLost:
                    // The collector robs the knocked-out player through the normal knockout looting; the toll counts as paid.
                    if (WorldStateManager.Instance != null) WorldStateManager.Instance.SetWorldEvent(_paidFact, true);
                    EndEncounter("player knocked out — toll taken by force");
                    return;
                case TollBrawlResult.CollectorLost:
                    EndEncounter("collector knocked out"); // beaten fact set by HandleCollectorKnockedOut
                    return;
                default:
                    EndEncounter("brawl ended (player fled or collector disengaged)");
                    return;
            }
        }

        // rearm = false: the gate waits for the player to leave its zone before stopping them again.
        private void EndEncounter(string reason, bool rearm = false)
        {
            _phase = Phase.Watching;
            _armed = rearm;
            GameLog.Info(TAG, $"{name}: encounter over — {reason}");
        }

        // --- Events ---

        // Pay / defer take effect on the pick (the gold is already taken), whatever happens to the encounter afterwards.
        private void HandleDialogueSignal(DialogueSignalData data)
        {
            if (s_answeringGate != this || data.signal == null) return;
            WorldStateManager wsm = WorldStateManager.Instance;
            if (data.signal == _racket.paySignal)
            {
                if (wsm != null) wsm.SetWorldEvent(_paidFact, true);
                _answered = true;
                GameLog.Info(TAG, $"{name}: toll paid");
            }
            else if (data.signal == _racket.deferSignal)
            {
                if (wsm != null && _racket.deferredFact != null) wsm.SetWorldEvent(_racket.deferredFact, true);
                _answered = true;
                GameLog.Info(TAG, $"{name}: toll deferred (once for the racket)");
            }
            s_answeringGate = null; // one answer per dialogue (refuse included)
        }

        // Knocked out by the player's blow (toll brawl, any other fight, a sneak hit): the racket is broken for good.
        private void HandleCollectorKnockedOut()
        {
            FactionMember player = TargetRegistry.Player;
            if (player == null || !_collector.WasHitThisFrameBy(player)) return;
            WorldStateManager wsm = WorldStateManager.Instance;
            if (wsm == null || _racket.beatenFact == null || wsm.GetFact(_racket.beatenFact)) return;
            wsm.SetWorldEvent(_racket.beatenFact, true);
            GameLog.Info(TAG, $"{_collector.name} knocked out by the player — the racket stops collecting");
        }

        // --- Helpers ---

        private bool IsCollectorOnDuty() =>
            _collector.isActiveAndEnabled && _collector.IsInNonCombatRoutine && !_collectorHealth.IsDead &&
            !_collectorHealth.IsUnconscious && FlatDistance(_collector.transform.position, Post.position) <= _racket.dutyRadius;

        private bool AnyOtherGatePaid()
        {
            WorldStateManager wsm = WorldStateManager.Instance;
            if (wsm == null) return false;
            for (int i = 0; i < _active.Count; i++)
            {
                TollGate gate = _active[i];
                if (gate == this || gate._racket != _racket || gate._paidFact == null) continue;
                if (wsm.GetFact(gate._paidFact)) return true;
            }
            return false;
        }

        public bool Contains(Vector3 worldPoint)
        {
            for (int i = 0; i < _boxes.Length; i++)
            {
                BoxCollider box = _boxes[i];
                if (box == null || !box.enabled || !box.gameObject.activeInHierarchy) continue;
                if (RestrictedAreaRules.IsInsideBox(box.transform.InverseTransformPoint(worldPoint), box.center, box.size))
                    return true;
            }
            return false;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            BoxCollider[] boxes = GetComponentsInChildren<BoxCollider>(true);
            Gizmos.color = new Color(0.95f, 0.8f, 0.2f, 0.9f);
            foreach (BoxCollider box in boxes)
            {
                Gizmos.matrix = box.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);
            }
            Gizmos.matrix = Matrix4x4.identity;
            if (_post != null) Gizmos.DrawWireSphere(_post.position, 0.5f);
        }
#endif
    }
}
