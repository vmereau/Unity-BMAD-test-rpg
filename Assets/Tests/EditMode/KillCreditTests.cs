using System.Collections.Generic;
using System.Reflection;
using Game.AI;
using Game.Core;
using Game.NPC;
using Game.Progression;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Kill credit (spec-kill-xp-credit-assist): the player earns kill XP only for their own kill, a Friendly NPC's kill
    /// or an assist; the kill fact and matching bonus rewards don't depend on it.
    /// </summary>
    public class KillCreditTests
    {
        private const BindingFlags PRIVATE = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo s_instanceField =
            typeof(WorldStateManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);

        private readonly List<Object> _cleanup = new List<Object>();
        private readonly List<EntityKilled> _raised = new List<EntityKilled>();
        private WorldStateManager _wsm;
        private GameEventSO_EntityKilled _onKilled;
        private KilledFact _fact;
        private NPCEntity _npc;

        [SetUp]
        public void SetUp()
        {
            Assert.IsNotNull(s_instanceField, "WorldStateManager.Instance is no longer an auto-property");
            s_instanceField.SetValue(null, null);
            var go = new GameObject("WorldStateManager_Test");
            _cleanup.Add(go);
            _wsm = go.AddComponent<WorldStateManager>();
            s_instanceField.SetValue(null, _wsm);

            _onKilled = Create<GameEventSO_EntityKilled>();
            _onKilled.AddListener(_raised.Add);
            Set(_wsm, "_onEntityKilled", _onKilled);

            _fact = Create<KilledFact>().Init("kill-credit-test");
            _npc = Create<NPCEntity>();
        }

        [TearDown]
        public void TearDown()
        {
            s_instanceField.SetValue(null, null);
            foreach (var o in _cleanup)
                if (o != null) Object.DestroyImmediate(o);
            _cleanup.Clear();
            _raised.Clear();
        }

        // ── Pure rule ─────────────────────────────────────────────────────────

        [TestCase(true,  false, false, KillCreditKind.Player)]
        [TestCase(true,  true,  true,  KillCreditKind.Player)]
        [TestCase(false, true,  false, KillCreditKind.Friendly)]
        [TestCase(false, true,  true,  KillCreditKind.Friendly)]
        [TestCase(false, false, true,  KillCreditKind.Assist)]
        [TestCase(false, false, false, KillCreditKind.None)]
        public void Evaluate_FirstMatchWins(bool player, bool friendly, bool assisted, KillCreditKind expected)
        {
            Assert.AreEqual(expected, KillCredit.Evaluate(player, friendly, assisted));
        }

        [Test]
        public void Resolve_UnknownKiller_CreditsOnlyAnAssist()
        {
            Assert.AreEqual(KillCreditKind.None, KillCredit.Resolve(null, false));
            Assert.AreEqual(KillCreditKind.Assist, KillCredit.Resolve(null, true));
        }

        // ── Kill event ────────────────────────────────────────────────────────

        [TestCase(true)]
        [TestCase(false)]
        public void RegisterKill_AlwaysSetsFact_AndCarriesCredit(bool credited)
        {
            _wsm.RegisterKill(_fact, _npc, credited);

            Assert.IsTrue(_wsm.IsKilled(_fact));
            Assert.AreEqual(1, _raised.Count);
            Assert.AreEqual(credited, _raised[0].playerCredited);
        }

        [Test]
        public void RegisterKill_DefaultsToNoCredit()
        {
            _wsm.RegisterKill(_fact, _npc);

            Assert.AreEqual(1, _raised.Count);
            Assert.IsFalse(_raised[0].playerCredited);
        }

        // ── Rewards ───────────────────────────────────────────────────────────

        [Test]
        public void PlayerRewards_CreditedKill_PaysKillXp()
        {
            var xp = MakeRewards(null);

            _wsm.RegisterKill(_fact, _npc, playerCredited: true);

            Assert.Greater(_npc.XpOnKill, 0);
            Assert.AreEqual(_npc.XpOnKill, xp.CurrentXP);
            Assert.AreEqual(1, xp.TotalKills);
        }

        [Test]
        public void PlayerRewards_UncreditedKill_PaysNoKillXp()
        {
            var xp = MakeRewards(null);

            _wsm.RegisterKill(_fact, _npc, playerCredited: false);

            Assert.AreEqual(0, xp.CurrentXP);
            Assert.AreEqual(0, xp.TotalKills);
        }

        [Test]
        public void PlayerRewards_UncreditedKill_StillPaysMatchingBonus()
        {
            var bonus = Create<PlayerRewardSO>();
            Set(bonus, "_factType", RewardFactType.Killed);
            Set(bonus, "_killedFact", _fact);
            Set(bonus, "_xpReward", 7);
            var xp = MakeRewards(bonus);

            _wsm.RegisterKill(_fact, _npc, playerCredited: false);

            Assert.AreEqual(7, xp.CurrentXP);
            Assert.AreEqual(0, xp.TotalKills, "a bonus never counts the kill");
        }

        [Test]
        public void PlayerRewards_CreditedKillWithBonus_CountsOneKill()
        {
            var bonus = Create<PlayerRewardSO>();
            Set(bonus, "_factType", RewardFactType.Killed);
            Set(bonus, "_killedFact", _fact);
            Set(bonus, "_xpReward", 7);
            var xp = MakeRewards(bonus);

            _wsm.RegisterKill(_fact, _npc, playerCredited: true);

            Assert.AreEqual(_npc.XpOnKill + 7, xp.CurrentXP);
            Assert.AreEqual(1, xp.TotalKills);
        }

        private XPSystem MakeRewards(PlayerRewardSO bonus)
        {
            var go = new GameObject("PlayerRewards_Test");
            _cleanup.Add(go);
            var xp = go.AddComponent<XPSystem>();
            var rewards = go.AddComponent<PlayerRewards>();
            Set(rewards, "_xpSystem", xp);
            Set(rewards, "_onEntityKilled", _onKilled);
            if (bonus != null) Set(rewards, "_rewards", new List<PlayerRewardSO> { bonus });
            // Edit mode runs no OnEnable on plain MonoBehaviours: subscribe through the real method.
            typeof(PlayerRewards).GetMethod("OnEnable", PRIVATE).Invoke(rewards, null);
            return xp;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            _cleanup.Add(so);
            return so;
        }

        private static void Set(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, PRIVATE);
            Assert.IsNotNull(f, $"{target.GetType().Name}.{field} not found");
            f.SetValue(target, value);
        }
    }
}
