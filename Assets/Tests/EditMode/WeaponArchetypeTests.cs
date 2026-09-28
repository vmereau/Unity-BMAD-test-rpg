using System.Collections.Generic;
using NUnit.Framework;
using Game.Inventory;
using UnityEngine;

namespace Tests.EditMode
{
    public class WeaponArchetypeTests
    {
        private const float POS_TOLERANCE = 1e-4f;
        private const float ANGLE_TOLERANCE = 0.01f;

        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created)
            {
                if (o != null)
                    Object.DestroyImmediate(o);
            }
            _created.Clear();
        }

        private T Track<T>(T o) where T : Object
        {
            _created.Add(o);
            return o;
        }

        private WeaponSO CreateWeapon(int comboSteps = 0, WeaponArchetypeSO archetype = null)
        {
            var w = Track(ScriptableObject.CreateInstance<SwordSO>());
            w.comboSteps = comboSteps;
            w.archetype = archetype;
            return w;
        }

        private WeaponArchetypeSO CreateArchetype(int defaultComboSteps = 2, WeaponSheathSocket socket = WeaponSheathSocket.Hip)
        {
            var a = Track(ScriptableObject.CreateInstance<WeaponArchetypeSO>());
            a.defaultComboSteps = defaultComboSteps;
            a.sheathSocket = socket;
            return a;
        }

        private AnimatorOverrideController CreateOverride() => Track(new AnimatorOverrideController());

        private Transform CreateTransform(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null)
                go.transform.SetParent(parent, false);
            else
                Track(go);
            return go.transform;
        }

        // ── ResolvedComboSteps ──────────────────────────────────────────────

        [Test]
        public void ResolvedComboSteps_WeaponValueOverridesArchetype()
            => Assert.That(CreateWeapon(3, CreateArchetype(2)).ResolvedComboSteps, Is.EqualTo(3));

        [Test]
        public void ResolvedComboSteps_ZeroUsesArchetypeDefault()
            => Assert.That(CreateWeapon(0, CreateArchetype(4)).ResolvedComboSteps, Is.EqualTo(4));

        [Test]
        public void ResolvedComboSteps_ZeroWithoutArchetype_UsesGlobalDefault()
            => Assert.That(CreateWeapon(0).ResolvedComboSteps, Is.EqualTo(WeaponSO.DEFAULT_COMBO_STEPS));

        [Test]
        public void ResolvedComboSteps_ArchetypeZero_ClampsToOne()
            => Assert.That(CreateWeapon(0, CreateArchetype(0)).ResolvedComboSteps, Is.EqualTo(1));

        // ── ResolvedAnimatorOverride ────────────────────────────────────────

        [Test]
        public void ResolvedAnimatorOverride_WeaponOverrideWins()
        {
            var archetype = CreateArchetype();
            archetype.animatorOverrideController = CreateOverride();
            var weapon = CreateWeapon(archetype: archetype);
            var own = CreateOverride();
            weapon.animatorOverrideController = own;

            Assert.That(weapon.ResolvedAnimatorOverride, Is.SameAs(own));
        }

        [Test]
        public void ResolvedAnimatorOverride_NullFallsBackToArchetype()
        {
            var archetype = CreateArchetype();
            var archOverride = CreateOverride();
            archetype.animatorOverrideController = archOverride;

            Assert.That(CreateWeapon(archetype: archetype).ResolvedAnimatorOverride, Is.SameAs(archOverride));
        }

        [Test]
        public void ResolvedAnimatorOverride_BothNull_ReturnsNull()
            => Assert.That(CreateWeapon(archetype: CreateArchetype()).ResolvedAnimatorOverride, Is.Null);

        // ── EquipmentVisuals.ResolveSheathSocket ────────────────────────────

        [Test]
        public void ResolveSheathSocket_NullArchetype_ReturnsHip()
        {
            var hip = CreateTransform("Hip");
            var back = CreateTransform("Back");
            Assert.That(EquipmentVisuals.ResolveSheathSocket(null, hip, back), Is.SameAs(hip));
        }

        [Test]
        public void ResolveSheathSocket_HipArchetype_ReturnsHip()
        {
            var hip = CreateTransform("Hip");
            var back = CreateTransform("Back");
            Assert.That(EquipmentVisuals.ResolveSheathSocket(CreateArchetype(socket: WeaponSheathSocket.Hip), hip, back), Is.SameAs(hip));
        }

        [Test]
        public void ResolveSheathSocket_BackArchetype_ReturnsBack()
        {
            var hip = CreateTransform("Hip");
            var back = CreateTransform("Back");
            Assert.That(EquipmentVisuals.ResolveSheathSocket(CreateArchetype(socket: WeaponSheathSocket.Back), hip, back), Is.SameAs(back));
        }

        [Test]
        public void ResolveSheathSocket_BackArchetypeWithoutBackSocket_FallsBackToHip()
        {
            var hip = CreateTransform("Hip");
            Assert.That(EquipmentVisuals.ResolveSheathSocket(CreateArchetype(socket: WeaponSheathSocket.Back), hip, null), Is.SameAs(hip));
        }

        // ── EquipmentVisuals.ApplyArchetypePoses ────────────────────────────

        [Test]
        public void ApplyArchetypePoses_SetsDrawnAndSheathedPoses()
        {
            var root = CreateTransform("Visual");
            var drawn = CreateTransform("Drawn", root);
            var sheathed = CreateTransform("Sheathed", root);
            var archetype = CreateArchetype();
            archetype.drawnPose = new WeaponPose { localPosition = new Vector3(0.1f, 0.2f, 0.3f), localEulerAngles = new Vector3(10f, 20f, 30f) };
            archetype.sheathedPose = new WeaponPose { localPosition = new Vector3(-0.1f, -0.2f, 0.05f), localEulerAngles = new Vector3(90f, 0f, 45f) };

            EquipmentVisuals.ApplyArchetypePoses(root.gameObject, archetype);

            Assert.That(Vector3.Distance(drawn.localPosition, archetype.drawnPose.localPosition), Is.LessThan(POS_TOLERANCE));
            Assert.That(Quaternion.Angle(drawn.localRotation, Quaternion.Euler(archetype.drawnPose.localEulerAngles)), Is.LessThan(ANGLE_TOLERANCE));
            Assert.That(Vector3.Distance(sheathed.localPosition, archetype.sheathedPose.localPosition), Is.LessThan(POS_TOLERANCE));
            Assert.That(Quaternion.Angle(sheathed.localRotation, Quaternion.Euler(archetype.sheathedPose.localEulerAngles)), Is.LessThan(ANGLE_TOLERANCE));
        }

        [Test]
        public void ApplyArchetypePoses_NullArchetype_LeavesTransformsUntouched()
        {
            var root = CreateTransform("Visual");
            var drawn = CreateTransform("Drawn", root);
            var pos = new Vector3(1f, 2f, 3f);
            var rot = Quaternion.Euler(5f, 15f, 25f);
            drawn.localPosition = pos;
            drawn.localRotation = rot;

            EquipmentVisuals.ApplyArchetypePoses(root.gameObject, null);

            Assert.That(drawn.localPosition, Is.EqualTo(pos));
            Assert.That(Quaternion.Angle(drawn.localRotation, rot), Is.LessThan(ANGLE_TOLERANCE));
        }

        [Test]
        public void ApplyArchetypePoses_WithoutConventionChildren_DoesNotThrow()
        {
            var root = CreateTransform("Visual");
            CreateTransform("SomethingElse", root);
            Assert.DoesNotThrow(() => EquipmentVisuals.ApplyArchetypePoses(root.gameObject, CreateArchetype()));
        }

        // ── WeaponGripMath ──────────────────────────────────────────────────

        [Test]
        public void NormalizationRotation_MapsBladeToUpAndEdgeToForward()
        {
            var rot = WeaponGripMath.NormalizationRotation(SignedAxis.NegY, SignedAxis.NegX);
            Assert.That(Vector3.Distance(rot * Vector3.down, Vector3.up), Is.LessThan(POS_TOLERANCE));
            Assert.That(Vector3.Distance(rot * Vector3.left, Vector3.forward), Is.LessThan(POS_TOLERANCE));
        }

        [Test]
        public void GetMeshChildLocal_MapsGripToOrigin()
        {
            var grip = new Vector3(0.02f, 0.35f, -0.01f);
            WeaponGripMath.GetMeshChildLocal(grip, SignedAxis.NegY, SignedAxis.NegX, out var pos, out var rot);
            Assert.That(Vector3.Distance(pos + rot * grip, Vector3.zero), Is.LessThan(POS_TOLERANCE));
        }

        [Test]
        public void AreValid_SameAxis_IsFalse()
        {
            Assert.That(WeaponGripMath.AreValid(SignedAxis.PosY, SignedAxis.NegY), Is.False);
            Assert.That(WeaponGripMath.AreValid(SignedAxis.PosX, SignedAxis.PosX), Is.False);
            Assert.That(WeaponGripMath.AreValid(SignedAxis.PosY, SignedAxis.PosZ), Is.True);
        }

        [Test]
        public void RebaseParentPose_KeepsMeshPointPlacement()
        {
            var oldPos = new Vector3(-0.305f, -0.029f, -0.13f);
            var oldRot = Quaternion.Euler(21.94f, 338.35f, 277.49f);
            WeaponGripMath.GetMeshChildLocal(new Vector3(0f, 0.35f, 0f), SignedAxis.NegY, SignedAxis.NegX, out var meshPos, out var meshRot);

            WeaponGripMath.RebaseParentPose(oldPos, oldRot, meshPos, meshRot, out var newPos, out var newRot);

            var meshPoint = new Vector3(0.05f, -0.3f, 0.01f);
            Vector3 before = oldPos + oldRot * meshPoint;                     // mesh directly under the old parent
            Vector3 after = newPos + newRot * (meshPos + meshRot * meshPoint); // mesh under the Mesh child under the new parent
            Assert.That(Vector3.Distance(before, after), Is.LessThan(POS_TOLERANCE));
        }
    }
}
