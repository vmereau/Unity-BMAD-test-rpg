using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Animations;
using Game.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Game.Editor.Characters
{
    /// <summary>
    /// Edit-mode check of the <see cref="HandPoseBuilder"/> poses on the base hero (spec-hand-pose-layer): samples a clip in a
    /// preview scene, overrides the fingers with the pose's muscles (what the hand layers do at runtime), puts the matching
    /// weapon / shield in the sockets, renders hand close-ups and measures each hand. Reports, per hand, the finger skin
    /// that sinks below the palm surface (count and depth) and the per-joint curl (hand→proximal / proximal→intermediate /
    /// intermediate→distal, degrees).
    /// </summary>
    public static class HandPoseCapture
    {
        private const string PREFAB = "Assets/_Game/Prefabs/Characters/Humanoid_BaseHero.prefab";
        private const string ANIM = "Assets/_Game/Art/Characters/Humanoids/Animations/";
        private const string SWORD = "Assets/_Game/Data/Items/Weapons/Swords/Weapon_ShortIronSword.asset";
        private const string TWO_HANDER = "Assets/_Game/Data/Items/Weapons/TwoHanded/Weapon_Flamberge.asset";
        private const string BOW = "Assets/_Game/Data/Items/Weapons/Bow/Weapon_HuntingBow.asset";
        private const string SHIELD = "Assets/_Game/Data/Items/Shields/Shield_RoundWoodShield.asset";
        private const float PALM_SEARCH_RADIUS = 0.03f;
        private const float PENETRATION_TOLERANCE = 0.002f;

        public readonly struct Case
        {
            public readonly string Tag, Clip, Weapon, Shield;
            public readonly float NormalizedTime;
            public readonly HandPose? Left, Right;
            public Case(string tag, string clip, float normalizedTime, HandPose? left, HandPose? right, string weapon = null, string shield = null)
            {
                Tag = tag; Clip = clip; NormalizedTime = normalizedTime; Left = left; Right = right; Weapon = weapon; Shield = shield;
            }
        }

        /// <summary>Bind pose, then the clips of the spec's verification list with the pose the rule picks for them.</summary>
        public static readonly Case[] DefaultCases =
        {
            new Case("bind", null, 0f, null, null),
            new Case("idle", ANIM + "Idle.fbx", 0.2f, HandPose.Relaxed, HandPose.Relaxed),
            new Case("guard", ANIM + "Combat/Unarmed/Unarmed Idle Guard.anim", 0.2f, HandPose.Fist, HandPose.Fist),
            new Case("jab", ANIM + "Combat/Unarmed/lead jab step.fbx", 0.45f, HandPose.Fist, HandPose.Fist),
            new Case("2hand", ANIM + "Combat/two handed/2hand Idle.fbx", 0.2f, HandPose.TwoHandGrip, HandPose.TwoHandGrip, TWO_HANDER),
            new Case("braced", ANIM + "Combat/one handed/Sword Braced Guard.anim", 0.2f, HandPose.Relaxed, HandPose.OneHandGrip, SWORD),
            new Case("shield", ANIM + "Combat/Block Idle.fbx", 0.2f, HandPose.ShieldHold, HandPose.OneHandGrip, SWORD, SHIELD),
            new Case("bow", ANIM + "Combat/Bow/Standing Aim Overdraw.fbx", 0.3f, HandPose.BowGrip, HandPose.Relaxed, BOW),
        };

        /// <summary>
        /// Renders <c>{tag}_{L|R}_{knuckles|back|palm|thumb}.png</c> into <paramref name="outDir"/> (null = measure only) for the
        /// cases whose tag is in <paramref name="tags"/> (null = all). Returns the measurements.
        /// </summary>
        public static string Run(string outDir, string[] tags = null, int size = 600)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var report = new StringBuilder();
            var spawned = new List<GameObject>();
            RenderTexture rt = null;
            Mesh baked = null;
            HumanPoseHandler poseHandler = null;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB);
                if (prefab == null) throw new InvalidOperationException($"HandPoseCapture: no prefab at {PREFAB}");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene); // straight into the preview scene
                foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                var animator = go.GetComponentInChildren<Animator>();
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                    throw new InvalidOperationException("HandPoseCapture: the base hero has no humanoid Animator");
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                SkinnedMeshRenderer body = null;
                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.forceMatrixRecalculationPerRender = true;
                    smr.updateWhenOffscreen = true;
                    if (smr.name == "base_hero") body = smr;
                    else smr.enabled = false; // head: it hides the guard hands from the side views
                }
                if (body == null) throw new InvalidOperationException("HandPoseCapture: no base_hero body renderer");
                Transform rightSocket = null, leftSocket = null;
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "WeaponSocket") rightSocket = t;
                    if (t.name == "LeftWeaponSocket") leftSocket = t;
                }

                Camera cam = null;
                if (outDir != null)
                {
                    Directory.CreateDirectory(outDir);
                    AddLight(scene, Quaternion.Euler(30f, 160f, 0f), 1.3f);
                    AddLight(scene, Quaternion.Euler(-20f, -20f, 0f), 0.6f);
                    var camGo = new GameObject("CaptureCamera");
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camGo, scene);
                    cam = camGo.AddComponent<Camera>();
                    cam.scene = scene;
                    cam.fieldOfView = 28f;
                    cam.nearClipPlane = 0.2f; // cuts what sits between the camera and the hand (other arm, chest)
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.35f, 0.4f, 0.45f);
                    rt = new RenderTexture(size, size, 24);
                    cam.targetTexture = rt;
                }

                // Palm side of each hand, from the bind pose (palms down in the T-pose).
                animator.enabled = false;
                float leftSign = PalmSign(animator, true), rightSign = PalmSign(animator, false);

                poseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
                var pose = new HumanPose();
                baked = new Mesh();
                foreach (var c in DefaultCases)
                {
                    if (tags != null && Array.IndexOf(tags, c.Tag) < 0) continue;
                    if (c.Clip != null)
                    {
                        Sample(animator, c.Clip, c.NormalizedTime);
                        poseHandler.GetHumanPose(ref pose);
                        if (c.Left.HasValue) HandPoseBuilder.ApplyToMuscles(c.Left.Value, true, pose.muscles);
                        if (c.Right.HasValue) HandPoseBuilder.ApplyToMuscles(c.Right.Value, false, pose.muscles);
                        poseHandler.SetHumanPose(ref pose);
                    }
                    else
                    {
                        ResetToBind(animator, go);
                    }
                    Transform handle = null;
                    if (c.Weapon != null)
                    {
                        var weaponVisual = AttachWeapon(c.Weapon, rightSocket, leftSocket);
                        spawned.Add(weaponVisual);
                        handle = RequireChild(weaponVisual, "Drawn");
                    }
                    Transform shield = null;
                    if (c.Shield != null)
                    {
                        var shieldVisual = AttachShield(c.Shield, leftSocket);
                        spawned.Add(shieldVisual);
                        shield = RequireChild(shieldVisual, "Drawn");
                    }

                    report.AppendLine($"{c.Tag} [{c.Left?.ToString() ?? "-"} / {c.Right?.ToString() ?? "-"}]");
                    foreach (bool left in new[] { true, false })
                    {
                        report.AppendLine($"   {(left ? "L" : "R")} {Penetration(body, baked, left)} | {Curls(animator, left)}");
                        HandPose? handPose = left ? c.Left : c.Right;
                        if (handle != null && handPose is HandPose.OneHandGrip or HandPose.TwoHandGrip or HandPose.BowGrip)
                            report.AppendLine($"     grip {Wrap(animator, left, handle)}");
                        if (shield != null && handPose == HandPose.ShieldHold)
                            report.AppendLine($"     shield {Wrap(animator, left, shield)}");
                        if (cam != null) Shoot(cam, rt, animator, left, left ? leftSign : rightSign, Path.Combine(outDir, c.Tag));
                    }
                    foreach (var s in spawned) UnityEngine.Object.DestroyImmediate(s);
                    spawned.Clear();
                }
            }
            finally
            {
                foreach (var s in spawned) if (s != null) UnityEngine.Object.DestroyImmediate(s);
                RenderTexture.active = null;
                if (rt != null) UnityEngine.Object.DestroyImmediate(rt);
                if (baked != null) UnityEngine.Object.DestroyImmediate(baked);
                poseHandler?.Dispose();
                EditorSceneManager.ClosePreviewScene(scene);
            }
            return report.ToString();
        }

        private static void AddLight(UnityEngine.SceneManagement.Scene scene, Quaternion rotation, float intensity)
        {
            var go = new GameObject("CaptureLight");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            go.transform.rotation = rotation;
        }

        private static void Sample(Animator animator, string clipPath, float normalizedTime)
        {
            AnimationClip clip = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(clipPath))
                if (asset is AnimationClip ac && !ac.name.StartsWith("__preview")) { clip = ac; break; }
            if (clip == null) throw new InvalidOperationException($"HandPoseCapture: no clip in {clipPath}");

            animator.enabled = true;
            var graph = PlayableGraph.Create("HandPoseCapture");
            try
            {
                var output = AnimationPlayableOutput.Create(graph, "out", animator);
                var playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(false);
                output.SetSourcePlayable(playable);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                playable.SetTime(normalizedTime * clip.length);
                graph.Evaluate(0f);
            }
            finally
            {
                graph.Destroy();
                animator.enabled = false;
            }
        }

        private static void ResetToBind(Animator animator, GameObject instance)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB);
            var sourceTransforms = new Dictionary<string, Transform>();
            foreach (var t in source.GetComponentsInChildren<Transform>(true)) sourceTransforms[t.name] = t;
            foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                if (t != instance.transform && sourceTransforms.TryGetValue(t.name, out var s))
                    t.SetLocalPositionAndRotation(s.localPosition, s.localRotation);
            animator.enabled = false;
        }

        private static GameObject AttachWeapon(string weaponPath, Transform rightSocket, Transform leftSocket)
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponSO>(weaponPath);
            if (weapon == null || weapon.equipVisualPrefab == null)
                throw new InvalidOperationException($"HandPoseCapture: no weapon / visual prefab at {weaponPath}");
            var socket = EquipmentVisuals.ResolveDrawnSocket(weapon.archetype, rightSocket, leftSocket);
            var visual = (GameObject)UnityEngine.Object.Instantiate(weapon.equipVisualPrefab, socket);
            visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            EquipmentVisuals.ApplyArchetypePoses(visual, weapon.archetype);
            var drawn = RequireChild(visual, "Drawn");
            var sheathed = visual.transform.Find("Sheathed");
            drawn.gameObject.SetActive(true);
            if (sheathed != null) sheathed.gameObject.SetActive(false);
            if (weapon.archetype != null && weapon.archetype.twoHandedGrip)
                TwoHandGripConstraint.Apply(drawn, rightSocket, leftSocket, weapon.archetype);
            return visual;
        }

        private static GameObject AttachShield(string shieldPath, Transform leftSocket)
        {
            var shield = AssetDatabase.LoadAssetAtPath<ShieldSO>(shieldPath);
            if (shield == null || shield.equipVisualPrefab == null)
                throw new InvalidOperationException($"HandPoseCapture: no shield / visual prefab at {shieldPath}");
            var visual = (GameObject)UnityEngine.Object.Instantiate(shield.equipVisualPrefab, leftSocket);
            visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            var drawn = RequireChild(visual, "Drawn");
            var sheathed = visual.transform.Find("Sheathed");
            shield.heldPose.ApplyTo(drawn);
            drawn.gameObject.SetActive(true);
            if (sheathed != null) sheathed.gameObject.SetActive(false);
            return visual;
        }

        // The grip / shield gaps are measured on the Drawn child: fail loudly if a visual prefab renamed it.
        private static Transform RequireChild(GameObject visual, string child)
        {
            var found = visual.transform.Find(child);
            if (found == null) throw new InvalidOperationException($"HandPoseCapture: {visual.name} has no '{child}' child");
            return found;
        }

        private static HumanBodyBones FingerBone(bool left, int finger, int joint) =>
            (HumanBodyBones)((left ? (int)HumanBodyBones.LeftThumbProximal : (int)HumanBodyBones.RightThumbProximal) + finger * 3 + joint);

        private static void HandFrame(Animator animator, bool left, out Vector3 center, out Vector3 along, out Vector3 across, out Vector3 normal)
        {
            var hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var index = animator.GetBoneTransform(FingerBone(left, 1, 0)).position;
            var middle = animator.GetBoneTransform(FingerBone(left, 2, 0)).position;
            var little = animator.GetBoneTransform(FingerBone(left, 4, 0)).position;
            along = (middle - hand.position).normalized;
            across = Vector3.ProjectOnPlane(index - little, along).normalized; // towards the thumb side
            normal = Vector3.Cross(along, across).normalized;
            center = (hand.position + middle) * 0.5f + along * 0.02f;
        }

        private static float PalmSign(Animator animator, bool left)
        {
            HandFrame(animator, left, out _, out _, out _, out var normal);
            return Vector3.Dot(normal, Vector3.down) >= 0f ? 1f : -1f;
        }

        private static void Shoot(Camera cam, RenderTexture rt, Animator animator, bool left, float palmSign, string prefix)
        {
            HandFrame(animator, left, out var center, out var along, out var across, out var normal);
            Vector3 palm = normal * palmSign;
            var views = new (string name, Vector3 dir, Vector3 up)[]
            {
                ("knuckles", (along + palm * 0.5f).normalized, -palm),
                ("back", -palm, along),
                ("palm", palm, along),
                ("thumb", across, along),
            };
            foreach (var view in views)
            {
                cam.transform.position = center + view.dir * 0.32f;
                cam.transform.LookAt(center, view.up);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                File.WriteAllBytes($"{prefix}_{(left ? "L" : "R")}_{view.name}.png", tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        // Finger skin (proximal-to-tip vertices of the four fingers) lying below the palm's skin surface.
        private static string Penetration(SkinnedMeshRenderer body, Mesh baked, bool left)
        {
            body.BakeMesh(baked); // unscaled: localToWorldMatrix below applies the renderer's scale once
            var vertices = baked.vertices;
            var normals = baked.normals;
            var weights = body.sharedMesh.boneWeights;
            var bones = body.bones;
            var toWorld = body.transform.localToWorldMatrix;
            string hand = "mixamorig:" + (left ? "Left" : "Right") + "Hand";
            var palm = new List<int>();
            var fingers = new List<int>();
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i].weight0 < 0.5f) continue;
                string bone = bones[weights[i].boneIndex0].name;
                if (bone == hand) palm.Add(i);
                else if (bone.StartsWith(hand) && !bone.Contains("Thumb") && !bone.EndsWith("1")) fingers.Add(i);
            }

            int count = 0;
            float maxDepth = 0f;
            foreach (int f in fingers)
            {
                Vector3 p = toWorld.MultiplyPoint3x4(vertices[f]);
                int nearest = -1;
                float best = PALM_SEARCH_RADIUS * PALM_SEARCH_RADIUS;
                foreach (int q in palm)
                {
                    float d = (toWorld.MultiplyPoint3x4(vertices[q]) - p).sqrMagnitude;
                    if (d < best) { best = d; nearest = q; }
                }
                if (nearest < 0) continue;
                float depth = -Vector3.Dot(p - toWorld.MultiplyPoint3x4(vertices[nearest]), toWorld.MultiplyVector(normals[nearest]).normalized);
                if (depth <= PENETRATION_TOLERANCE) continue;
                count++;
                maxDepth = Mathf.Max(maxDepth, depth);
            }
            return $"inside palm {count}/{fingers.Count} verts, max {maxDepth * 1000f:F1} mm";
        }

        // Per finger: signed gap (mm) between the distal joint / fingertip and the weapon's surface (closest triangle of
        // the drawn meshes; negative = inside). A finger resting on the handle ≈ its own half-thickness (~8–10 mm).
        private static string Wrap(Animator animator, bool left, Transform weapon)
        {
            var triangles = new List<(Vector3 a, Vector3 b, Vector3 c)>();
            foreach (var filter in weapon.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;
                var toWorld = filter.transform.localToWorldMatrix;
                var vertices = mesh.vertices;
                var indices = mesh.triangles;
                for (int i = 0; i < indices.Length; i += 3)
                    triangles.Add((toWorld.MultiplyPoint3x4(vertices[indices[i]]), toWorld.MultiplyPoint3x4(vertices[indices[i + 1]]),
                        toWorld.MultiplyPoint3x4(vertices[indices[i + 2]])));
            }
            if (triangles.Count == 0) return "no weapon mesh";

            float Gap(Vector3 p)
            {
                float best = float.MaxValue, sign = 1f;
                foreach (var tri in triangles)
                {
                    Vector3 q = ClosestPointOnTriangle(p, tri.a, tri.b, tri.c);
                    float d = (p - q).sqrMagnitude;
                    if (d >= best) continue;
                    best = d;
                    sign = Vector3.Dot(p - q, Vector3.Cross(tri.b - tri.a, tri.c - tri.a)) < 0f ? -1f : 1f;
                }
                return sign * Mathf.Sqrt(best) * 1000f;
            }

            HandFrame(animator, left, out var center, out _, out _, out _);
            string[] names = { "Th", "In", "Mi", "Ri", "Li" };
            var sb = new StringBuilder($"palm {Gap(center):F0} | ");
            for (int f = 0; f < 5; f++)
            {
                var distal = animator.GetBoneTransform(FingerBone(left, f, 2));
                Vector3 tip = distal.childCount > 0 ? distal.GetChild(0).position : distal.position;
                sb.Append($"{names[f]} {Gap(distal.position):F0}/{Gap(tip):F0} ");
            }
            return sb.ToString();
        }

        // Ericson, Real-Time Collision Detection 5.1.5.
        private static Vector3 ClosestPointOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return a;
            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return b;
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + ab * (d1 / (d1 - d3));
            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return c;
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + ac * (d2 / (d2 - d6));
            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f) return b + (c - b) * ((d4 - d3) / (d4 - d3 + (d5 - d6)));
            float denom = 1f / (va + vb + vc);
            return a + ab * (vb * denom) + ac * (vc * denom);
        }

        private static string Curls(Animator animator, bool left)
        {
            string[] names = { "Th", "In", "Mi", "Ri", "Li" };
            var hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand).position;
            var sb = new StringBuilder();
            for (int f = 0; f < 5; f++)
            {
                var b0 = animator.GetBoneTransform(FingerBone(left, f, 0));
                var b1 = animator.GetBoneTransform(FingerBone(left, f, 1));
                var b2 = animator.GetBoneTransform(FingerBone(left, f, 2));
                Vector3 d0 = b1.position - b0.position, d1 = b2.position - b1.position;
                Vector3 d2 = b2.childCount > 0 ? b2.GetChild(0).position - b2.position : d1;
                sb.Append($"{names[f]} {Vector3.Angle(b0.position - hand, d0):F0}/{Vector3.Angle(d0, d1):F0}/{Vector3.Angle(d1, d2):F0} ");
            }
            return sb.ToString();
        }
    }
}
