using System;
using System.Collections.Generic;
using Game.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Game.Editor.Characters
{
    /// <summary>
    /// Bakes the Mixamo <c>Standing Bow Idle</c> into <c>Bow Idle.anim</c>, the bow's <c>CombatIdle</c> clip on the masked
    /// UpperBody layer (spec-bow-idle-compensated-bake). The source is a side-on stance (hips 63°, shoulders 55°, head
    /// forward); the mask takes the hips from locomotion, so unbaked the chest faces -6° and the head -57°. Constant spine
    /// offsets give back part of that turn (chest bladed 25°, lean kept relative to it) and neck / head offsets bring the
    /// head back to forward. Idempotent: rewrites the <c>.anim</c> in place (GUID kept).
    /// </summary>
    public static class BowIdleBaker
    {
        private const string TAG = "[BowIdleBaker]";
        private const string ANIM = "Assets/_Game/Art/Characters/Humanoids/Animations/";
        public const string SOURCE = ANIM + "Combat/Bow/Standing Bow Idle.fbx";
        public const string BAKED = ANIM + "Combat/Bow/Bow Idle.anim";
        public const string LEGS = ANIM + "Idle.fbx";
        public const string MASK = ANIM + "UpperBodyMask.mask";
        private const string PREFAB = "Assets/_Game/Prefabs/Characters/Humanoid_BaseHero.prefab";

        /// <summary>Constant offsets added to the source's muscle curves (humanoid muscle units).</summary>
        public static readonly (string muscle, float offset)[] Offsets =
        {
            // Chest (three equal steps per axis): the masked UpperChest equals the source's full-body UpperChest turned to
            // 25° right of the root (0.3° residual), so the clip's lean stays relative to where the chest faces.
            ("Spine Front-Back", -0.088f), ("Chest Front-Back", -0.088f), ("UpperChest Front-Back", -0.088f),
            ("Spine Left-Right", 0.163f), ("Chest Left-Right", 0.163f), ("UpperChest Left-Right", 0.163f),
            ("Spine Twist Left-Right", 0.294f), ("Chest Twist Left-Right", 0.294f), ("UpperChest Twist Left-Right", 0.294f),
            // Head (neck takes half): the source's head, level, turned to forward (source Head Turn -1.34: over the left shoulder).
            ("Neck Nod Down-Up", -0.016f), ("Head Nod Down-Up", -0.031f),
            ("Neck Tilt Left-Right", 0.109f), ("Head Tilt Left-Right", 0.219f),
            ("Neck Turn Left-Right", 0.203f), ("Head Turn Left-Right", 0.406f),
        };

        /// <summary>Body yaws (degrees right of the root's forward) of a sampled pose.</summary>
        public readonly struct Yaws
        {
            public readonly float Hips, Shoulders, Head;
            public Yaws(float hips, float shoulders, float head) { Hips = hips; Shoulders = shoulders; Head = head; }
            public override string ToString() => $"hips {Hips:F1} shoulders {Shoulders:F1} head {Head:F1}";
        }

        [MenuItem("Tools/Characters/Bake Bow Idle Clip")]
        private static void BakeFromMenu()
        {
            try { GameLog.Info(TAG, Bake()); }
            catch (Exception exception) { GameLog.Error(TAG, exception.ToString()); }
        }

        /// <summary>Bakes the clip. Returns a summary; throws (nothing written) on a missing source or muscle curve.</summary>
        public static string Bake()
        {
            var source = BowClipWristBaker.LoadSource(SOURCE);
            Validate(source, Offsets);
            var baked = BowClipWristBaker.BakeAsset(source, BAKED, Offsets, out int clamped);
            AssetDatabase.SaveAssets();
            return $"baked {baked.name} ({clamped} keys clamped)";
        }

        /// <summary>Throws when <paramref name="source"/> is not a humanoid clip or lacks one of the offset muscles' curves.</summary>
        public static void Validate(AnimationClip source, (string muscle, float offset)[] offsets)
        {
            if (!source.humanMotion) throw new InvalidOperationException($"BowIdleBaker: {SOURCE} is not a humanoid clip");
            foreach (var (muscle, _) in offsets)
                if (AnimationUtility.GetEditorCurve(source, BowClipWristBaker.MuscleBinding(muscle)) == null)
                    throw new InvalidOperationException($"BowIdleBaker: {SOURCE} has no '{muscle}' curve");
        }

        /// <summary>
        /// Yaws of the base hero with the source plus <paramref name="offsets"/> (null = <see cref="Offsets"/>) on the masked
        /// upper body over the <c>Idle</c> legs, at each normalized time.
        /// </summary>
        public static Yaws[] Measure((string muscle, float offset)[] offsets = null, params float[] times)
        {
            if (times == null || times.Length == 0) times = new[] { 0.1f, 0.5f, 0.9f };
            using var rig = new Rig();
            var result = new Yaws[times.Length];
            for (int i = 0; i < times.Length; i++)
            {
                rig.PoseMasked(offsets ?? Offsets, times[i]);
                result[i] = ReadYaws(rig.Animator);
            }
            return result;
        }

        /// <summary>
        /// The base hero in a preview scene, posed by the source clip: full body (the Mixamo pose) or, with offsets, on the
        /// masked upper body over the <c>Idle</c> legs. The masked layer can't be read with <c>SampleAnimation</c>: this
        /// evaluates the same two-layer blend as the Animator. Used to fit and to check <see cref="Offsets"/>.
        /// </summary>
        public sealed class Rig : IDisposable
        {
            private readonly UnityEngine.SceneManagement.Scene _scene;
            private readonly AnimationClip _source, _legs, _temp;
            private readonly AvatarMask _mask;
            private bool _disposed;
            public Animator Animator { get; }

            public Rig()
            {
                _source = BowClipWristBaker.LoadSource(SOURCE);
                _legs = BowClipWristBaker.LoadSource(LEGS);
                _mask = LoadMask();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB);
                if (prefab == null) throw new InvalidOperationException($"BowIdleBaker: no prefab at {PREFAB}");
                _temp = new AnimationClip();
                _scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _scene);
                    foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                    Animator = go.GetComponentInChildren<Animator>();
                    if (Animator == null || Animator.avatar == null || !Animator.avatar.isHuman)
                        throw new InvalidOperationException("BowIdleBaker: the base hero has no humanoid Animator");
                    Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // a preview-scene instance is never "visible"
                }
                catch
                {
                    Dispose(); // the caller's using never receives a half-built rig
                    throw;
                }
            }

            /// <summary>The untouched source clip on the whole body.</summary>
            public void PoseFullBody(float normalizedTime) => SampleLayered(Animator, _source, _source, _mask, normalizedTime);

            /// <summary>Source plus <paramref name="offsets"/> on the masked upper body over the <c>Idle</c> legs.</summary>
            public void PoseMasked((string muscle, float offset)[] offsets, float normalizedTime)
            {
                Validate(_source, offsets);
                BowClipWristBaker.CopyWithOffsets(_source, _temp, offsets);
                SampleLayered(Animator, _legs, _temp, _mask, normalizedTime);
            }

            /// <summary>Rotation of a bone in the animator root's space.</summary>
            public Quaternion Rotation(HumanBodyBones bone) =>
                Quaternion.Inverse(Animator.transform.rotation) * Animator.GetBoneTransform(bone).rotation;

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                EditorSceneManager.ClosePreviewScene(_scene);
                if (_temp != null) UnityEngine.Object.DestroyImmediate(_temp);
            }
        }

        public static AvatarMask LoadMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MASK);
            if (mask == null) throw new InvalidOperationException($"BowIdleBaker: no mask at {MASK}");
            return mask;
        }

        /// <summary>
        /// Poses <paramref name="animator"/> with <paramref name="legs"/> on the base layer and <paramref name="upper"/> on an
        /// Override layer limited by <paramref name="mask"/> (both at the same normalized time), then leaves it disabled.
        /// </summary>
        public static void SampleLayered(Animator animator, AnimationClip legs, AnimationClip upper, AvatarMask mask, float normalizedTime)
        {
            animator.enabled = true;
            var graph = PlayableGraph.Create("BowIdleBaker");
            try
            {
                var output = AnimationPlayableOutput.Create(graph, "out", animator);
                var mixer = AnimationLayerMixerPlayable.Create(graph, 2);
                var basePlayable = AnimationClipPlayable.Create(graph, legs);
                var upperPlayable = AnimationClipPlayable.Create(graph, upper);
                basePlayable.SetApplyFootIK(false);
                upperPlayable.SetApplyFootIK(false);
                graph.Connect(basePlayable, 0, mixer, 0);
                graph.Connect(upperPlayable, 0, mixer, 1);
                mixer.SetInputWeight(0, 1f);
                mixer.SetInputWeight(1, 1f);
                mixer.SetLayerMaskFromAvatarMask(1, mask);
                basePlayable.SetTime(normalizedTime * legs.length);
                upperPlayable.SetTime(normalizedTime * upper.length);
                output.SetSourcePlayable(mixer);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                graph.Evaluate(0f);
            }
            finally
            {
                graph.Destroy();
                animator.enabled = false;
            }
        }

        /// <summary>Hip line, shoulder line and head yaws relative to the animator root's forward (positive = turned right).</summary>
        public static Yaws ReadYaws(Animator animator)
        {
            var root = animator.transform;
            float LineYaw(HumanBodyBones left, HumanBodyBones right)
            {
                Vector3 across = root.InverseTransformDirection(animator.GetBoneTransform(right).position - animator.GetBoneTransform(left).position);
                Vector3 forward = Vector3.Cross(across, Vector3.up);
                return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            }
            Vector3 headForward = root.InverseTransformDirection(animator.GetBoneTransform(HumanBodyBones.Head).forward);
            return new Yaws(
                LineYaw(HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg),
                LineYaw(HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm),
                Mathf.Atan2(headForward.x, headForward.z) * Mathf.Rad2Deg);
        }
    }
}
