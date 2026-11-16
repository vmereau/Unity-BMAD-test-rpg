using System.Collections.Generic;
using Game.Core;
using Game.Factions;
using Game.NPC;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// A place the player may not be. Shape = every <see cref="BoxCollider"/> on this GameObject and its children
    /// (triggers on Ignore Raycast — only their center / size are read, no trigger events). Owners = listed
    /// <see cref="NPCEntity"/> assets and/or every NPC of a listed <see cref="FactionSO"/>. An owner's
    /// <c>EntityPerception</c> perceives a player inside (while <see cref="IsRestrictedNow"/>) like a hostile target;
    /// once fully aware its brain warns + chases + scolds (theft pursuit flow). A scold starts a grace period
    /// (<see cref="StartGrace"/>) so the player can leave, and counts as a warning (<see cref="RecordWarning"/>). Once the
    /// area gave <c>_warningsBeforeFight</c> warnings (0 = attack on sight), owners brawl the trespasser instead
    /// (<see cref="ShouldFightTrespasser"/>). Warnings expire after a number of in-game hours and reset when the player is
    /// knocked out in such a fight. Runtime only, not saved.
    /// </summary>
    public class RestrictedArea : MonoBehaviour
    {
        private const string TAG = "[RestrictedArea]";

        [Header("Owners")]
        [Tooltip("NPCs that own this area (react to the player inside).")]
        [SerializeField] private List<NPCEntity> _ownerEntities = new();
        [Tooltip("Every NPC of these factions owns this area (guard corps, guild…).")]
        [SerializeField] private List<FactionSO> _ownerFactions = new();

        [Header("Hours")]
        [Tooltip("Off = restricted all day. On = restricted only from Start Hour (incl.) to End Hour (excl.), wrapping midnight.")]
        [SerializeField] private bool _useHours;
        [Range(0, 23)] [SerializeField] private int _startHour = 21;
        [Range(0, 23)] [SerializeField] private int _endHour = 6;

        [Header("Reaction")]
        [Tooltip("Scolds given before owners attack the trespasser instead of warning, chasing and scolding. 0 = attack on sight.")]
        [Min(0)] [SerializeField] private int _warningsBeforeFight = 1;

        private static readonly List<RestrictedArea> _active = new();
        private static int s_lastIncidentId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _active.Clear();
            s_lastIncidentId = 0;
        }

        private BoxCollider[] _boxes = System.Array.Empty<BoxCollider>();
        private float _graceUntil = float.NegativeInfinity;
        private int _warnings;
        private double _lastWarningAt;

        public string DebugName { get; private set; }

        /// <summary>Trespass incident currently chased by this area's owners (0 = none). Negative ids, never colliding
        /// with <c>InteractionSystem</c>'s positive theft ids.</summary>
        public int OpenIncidentId { get; private set; }

        /// <summary>Within the hours window and not in a grace period.</summary>
        public bool IsRestrictedNow
        {
            get
            {
                if (Time.time < _graceUntil) return false;
                GameClock clock = GameClock.Active;
                return RestrictedAreaRules.IsRestrictedHour(clock != null, _useHours, clock != null ? clock.Hour : 0,
                    _startHour, _endHour);
            }
        }

        private void Awake()
        {
            DebugName = gameObject.name;
            _boxes = GetComponentsInChildren<BoxCollider>(true);
            if (_boxes.Length == 0)
                GameLog.Warn(TAG, $"{DebugName}: no BoxCollider shape — the area contains nothing");
            if (_ownerEntities.Count == 0 && _ownerFactions.Count == 0)
                GameLog.Warn(TAG, $"{DebugName}: no owners — nobody will ever react");
        }

        private void OnEnable() => _active.Add(this);

        private void OnDisable() => _active.Remove(this);

        public bool IsOwnedBy(NPCEntity entity, FactionSO faction) =>
            RestrictedAreaRules.IsOwner(_ownerEntities, _ownerFactions, entity, faction);

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

        /// <summary>Opens a new trespass incident (the previous one was claimed or never opened) and returns its id.</summary>
        public int OpenIncident()
        {
            OpenIncidentId = --s_lastIncidentId;
            return OpenIncidentId;
        }

        /// <summary>The trespasser was scolded: owners ignore this area for <paramref name="seconds"/>.</summary>
        public void StartGrace(float seconds)
        {
            _graceUntil = Time.time + seconds;
            OpenIncidentId = 0;
            GameLog.Info(TAG, $"{DebugName}: trespasser scolded — grace {seconds:0.#} s");
        }

        /// <summary>Keeps the grace running for at least <paramref name="seconds"/> more (silent, called every frame while
        /// a scold dialogue is open).</summary>
        public void HoldGrace(float seconds)
        {
            _graceUntil = Mathf.Max(_graceUntil, Time.time + seconds);
            OpenIncidentId = 0;
        }

        /// <summary>Warnings still in force (expire <paramref name="expiryHours"/> in-game hours after the latest one).</summary>
        public int ActiveWarnings(float expiryHours)
        {
            GameClock clock = GameClock.Active;
            return RestrictedAreaRules.ActiveWarnings(_warnings, clock != null, clock != null ? clock.Now : 0d,
                _lastWarningAt, expiryHours);
        }

        /// <summary>Owners attack the trespasser instead of warning: enough warnings were already given.</summary>
        public bool ShouldFightTrespasser(float expiryHours) =>
            RestrictedAreaRules.ShouldFight(ActiveWarnings(expiryHours), _warningsBeforeFight);

        /// <summary>The trespasser was scolded here: one more warning (expired ones are dropped first).</summary>
        public void RecordWarning(float expiryHours)
        {
            GameClock clock = GameClock.Active;
            _warnings = ActiveWarnings(expiryHours) + 1;
            _lastWarningAt = clock != null ? clock.Now : 0d;
            GameLog.Info(TAG, $"{DebugName}: trespasser warned ({_warnings}/{_warningsBeforeFight} before a fight)");
        }

        /// <summary>The trespasser was punished (knocked out in an escalation fight): back to a first warning.</summary>
        public void ClearWarnings()
        {
            if (_warnings == 0) return;
            _warnings = 0;
            GameLog.Info(TAG, $"{DebugName}: trespasser punished — warnings reset");
        }

        /// <summary>
        /// First enabled area owned by (<paramref name="entity"/>, <paramref name="faction"/>), restricted now, that
        /// contains <paramref name="point"/>.
        /// </summary>
        public static bool TryFindTrespassedArea(Vector3 point, NPCEntity entity, FactionSO faction, out RestrictedArea area)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                RestrictedArea a = _active[i];
                if (a.IsOwnedBy(entity, faction) && a.IsRestrictedNow && a.Contains(point))
                {
                    area = a;
                    return true;
                }
            }
            area = null;
            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            BoxCollider[] boxes = GetComponentsInChildren<BoxCollider>(true);
            Gizmos.color = _warningsBeforeFight <= 0 || (Application.isPlaying && _warnings > 0) ? new Color(1f, 0f, 0f, 0.9f) : new Color(1f, 0.45f, 0.1f, 0.9f);
            foreach (BoxCollider box in boxes)
            {
                Gizmos.matrix = box.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
#endif
    }
}
