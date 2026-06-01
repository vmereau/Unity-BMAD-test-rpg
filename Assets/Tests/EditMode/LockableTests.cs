using System.Collections.Generic;
using System.Reflection;
using Game.Progression;
using Game.World;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for Lockable pure lock-data logic.
    /// Lockable is a MonoBehaviour, but its lock data + Unlock() are pure logic with no scene
    /// dependency. The private _isLocked / _requiredSkill fields are injected via reflection so we
    /// don't add a test-only setter to the production API (mirrors TargetRegistryTests).
    /// Door rotation + DoorSystem skill gate are runtime/MonoBehaviour → not Edit-mode tested
    /// (consistent with ContainerSystem).
    /// </summary>
    public class LockableTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo s_isLocked = typeof(Lockable).GetField("_isLocked", Instance);
        private static readonly FieldInfo s_requiredSkill = typeof(Lockable).GetField("_requiredSkill", Instance);

        private readonly List<Object> _cleanup = new();

        private Lockable MakeLockable(bool locked = false, SkillSO skill = null)
        {
            var go = new GameObject("Lockable");
            var lockable = go.AddComponent<Lockable>();
            s_isLocked.SetValue(lockable, locked);
            s_requiredSkill.SetValue(lockable, skill);
            _cleanup.Add(go);
            return lockable;
        }

        private SkillSO MakeSkill(string id)
        {
            var skill = ScriptableObject.CreateInstance<SkillSO>();
            // SkillSO.skillId is read-only; the backing field is private serialized.
            typeof(SkillSO).GetField("_skillId", Instance).SetValue(skill, id);
            _cleanup.Add(skill);
            return skill;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _cleanup) if (o != null) Object.DestroyImmediate(o);
            _cleanup.Clear();
        }

        [Test]
        public void Fresh_Lockable_IsNotLocked()
        {
            var lockable = MakeLockable();
            Assert.IsFalse(lockable.IsLocked);
        }

        [Test]
        public void RequiredSkillId_IsNull_WhenNoSkillAssigned()
        {
            var lockable = MakeLockable(locked: true);
            Assert.IsNull(lockable.RequiredSkillId);
        }

        [Test]
        public void RequiredSkillId_MapsFromSkillSO_WhenAssigned()
        {
            var skill = MakeSkill("lockpicking");
            var lockable = MakeLockable(locked: true, skill: skill);
            Assert.AreEqual("lockpicking", lockable.RequiredSkillId);
        }

        [Test]
        public void Unlock_SetsIsLockedFalse_WhenLocked()
        {
            var lockable = MakeLockable(locked: true);
            Assert.IsTrue(lockable.IsLocked);

            lockable.Unlock();

            Assert.IsFalse(lockable.IsLocked);
        }

        [Test]
        public void Unlock_IsIdempotent_WhenAlreadyUnlocked()
        {
            var lockable = MakeLockable(locked: false);
            Assert.DoesNotThrow(() => lockable.Unlock());
            Assert.IsFalse(lockable.IsLocked);
        }
    }
}
