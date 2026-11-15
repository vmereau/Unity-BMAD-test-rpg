using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.NPC;
using Game.Progression;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Knockout XP (spec-knockout-xp-reward): <see cref="WorldStateManager.RegisterKnockout"/> raises once per
    /// identity (claim saved with the facts), and <see cref="PlayerRewards"/> grants <see cref="NPCEntity.XpOnKnockout"/>
    /// without counting a kill.
    /// </summary>
    public class KnockoutXpTests
    {
        private const BindingFlags PRIVATE = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo s_instanceField =
            typeof(WorldStateManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);

        private readonly List<Object> _cleanup = new List<Object>();
        private readonly List<EntityKilled> _raised = new List<EntityKilled>();
        private WorldStateManager _wsm;
        private GameEventSO_EntityKilled _onKnockedOut;
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

            _onKnockedOut = Create<GameEventSO_EntityKilled>();
            _onKnockedOut.AddListener(_raised.Add);
            Set(_wsm, "_onEntityKnockedOut", _onKnockedOut);

            _fact = Create<KilledFact>().Init("knockout-test-a");
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

        [Test]
        public void FirstKnockout_RaisesOnce_AndClaims()
        {
            _wsm.RegisterKnockout(_fact, _npc);
            _wsm.RegisterKnockout(_fact, _npc);

            Assert.AreEqual(1, _raised.Count);
            Assert.AreSame(_npc, _raised[0].entity);
            Assert.IsTrue(_wsm.IsKnockoutClaimed(_fact));
        }

        [Test]
        public void DifferentIdentities_EachRaiseOnce()
        {
            _wsm.RegisterKnockout(_fact, _npc);
            _wsm.RegisterKnockout(Create<KilledFact>().Init("knockout-test-b"), _npc);

            Assert.AreEqual(2, _raised.Count);
        }

        [Test]
        public void Claim_SurvivesCaptureRestore()
        {
            _wsm.RegisterKnockout(_fact, _npc);
            var saved = _wsm.CaptureFacts();

            _wsm.RestoreFacts(new Dictionary<string, bool>());
            Assert.IsFalse(_wsm.IsKnockoutClaimed(_fact));

            _wsm.RestoreFacts(saved);
            _raised.Clear();
            _wsm.RegisterKnockout(_fact, _npc);

            Assert.IsTrue(_wsm.IsKnockoutClaimed(_fact));
            Assert.AreEqual(0, _raised.Count);
        }

        [Test]
        public void UnwiredChannel_DoesNotClaim()
        {
            Set(_wsm, "_onEntityKnockedOut", null);
            _wsm.RegisterKnockout(_fact, _npc);

            Assert.IsFalse(_wsm.IsKnockoutClaimed(_fact));
        }

        [Test]
        public void Claim_IsNotAKill()
        {
            _wsm.RegisterKnockout(_fact, _npc);

            Assert.IsFalse(_wsm.IsKilled(_fact));
        }

        [Test]
        public void NullFact_IsIgnored()
        {
            _wsm.RegisterKnockout(null, _npc);

            Assert.AreEqual(0, _raised.Count);
            Assert.IsFalse(_wsm.IsKnockoutClaimed(null));
        }

        [Test]
        public void PlayerRewards_GrantsKnockoutXp_WithoutCountingAKill()
        {
            var go = new GameObject("PlayerRewards_Test");
            _cleanup.Add(go);
            var xp = go.AddComponent<XPSystem>();
            var rewards = go.AddComponent<PlayerRewards>();
            Set(rewards, "_xpSystem", xp);
            Set(rewards, "_onEntityKnockedOut", _onKnockedOut);
            // Edit mode runs no OnEnable on plain MonoBehaviours: subscribe through the real method.
            typeof(PlayerRewards).GetMethod("OnEnable", PRIVATE).Invoke(rewards, null);

            _wsm.RegisterKnockout(_fact, _npc);

            Assert.AreEqual(_npc.XpOnKnockout, xp.CurrentXP);
            Assert.Greater(xp.CurrentXP, 0);
            Assert.AreEqual(0, xp.TotalKills);
            typeof(PlayerRewards).GetMethod("OnDisable", PRIVATE).Invoke(rewards, null);
        }

        [Test]
        public void GiveExperience_DefaultStillCountsAKill()
        {
            var go = new GameObject("XP_Test");
            _cleanup.Add(go);
            var xp = go.AddComponent<XPSystem>();

            xp.GiveExperience(10);
            xp.GiveExperience(5, countAsKill: false);

            Assert.AreEqual(15, xp.CurrentXP);
            Assert.AreEqual(1, xp.TotalKills);
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
