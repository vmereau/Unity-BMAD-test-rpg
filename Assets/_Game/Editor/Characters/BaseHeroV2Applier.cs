using System;
using System.Collections.Generic;
using System.Text;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Characters
{
    /// <summary>
    /// Moves the player and every <c>Humanoid_BaseHero</c> NPC onto the base hero v2 body (spec-base-body-regen) in place:
    /// the prefabs keep their FBX instance (sockets, ragdoll, components), only the finger bones change. The v2 rig has
    /// the same non-finger bones and rest transforms as the old rig, so this tool overrides the finger bone transforms,
    /// adds the Ring / Pinky chains, rebinds the <c>base_hero</c> SMR to the v2 bone order, and swaps the avatar.
    /// </summary>
    public static class BaseHeroV2Applier
    {
        private const string TAG = "[BaseHeroV2Applier]";
        private const string MODELS = "Assets/_Game/Art/Characters/Humanoids/models/";
        public const string V2_FBX = MODELS + "base hero v2/base hero v2.fbx";
        private const string OLD_FBX = MODELS + "test base hero/rigged test hero with textures.fbx";
        public const string BODY_ASSET = MODELS + "test base hero/base_hero_body_baked.asset";
        public const string AVATAR = MODELS + "base hero v2/BaseHeroV2Avatar.asset";
        private const string HEAD_ASSET = MODELS + "test base hero/base_hero_head_baked.asset";
        private const float SEAM = 1.585f;
        public const string MATERIAL = MODELS + "base hero v2/M_BaseHeroV2Body.mat";
        private const string OLD_MATERIAL = MODELS + "test base hero/M_TestBaseHero.mat";
        private const string BASE_COLOR = MODELS + "base hero v2/T_BaseHeroV2_BaseColor.png";
        private const string NORMAL = MODELS + "base hero v2/T_BaseHeroV2_Normal.png";
        private const string MASK = MODELS + "base hero v2/T_SkinMask_BaseHeroV2.png";
        private const float SKIN_REF_LUMINANCE = 0.180322f; // make_skin_masks.py, BaseHeroV2 entry
        private const string BODY_NAME = "base_hero";
        /// <summary>Non-finger bones must already sit at the v2 rest transform (metres / degrees).</summary>
        private const float REST_TOLERANCE = 1e-3f;

        private static readonly string[] PREFABS =
        {
            "Assets/_Game/Prefabs/Player/Player.prefab",
            "Assets/_Game/Prefabs/Characters/Humanoid_BaseHero.prefab",
        };

        [MenuItem("Tools/Characters/Apply Base Hero V2 Body")]
        private static void ApplyMenu()
        {
            try { GameLog.Info(TAG, ApplyAll()); }
            catch (Exception exception) { GameLog.Error(TAG, exception.Message); }
        }

        public static bool IsFingerBone(string name) =>
            name.Contains("HandThumb") || name.Contains("HandIndex") || name.Contains("HandMiddle")
            || name.Contains("HandRing") || name.Contains("HandPinky");

        public static string ApplyAll()
        {
            var v2 = AssetDatabase.LoadAssetAtPath<GameObject>(V2_FBX);
            var v2Body = v2 != null ? v2.GetComponentInChildren<SkinnedMeshRenderer>() : null;
            var fbxAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(V2_FBX);
            if (v2Body == null || fbxAvatar == null || !fbxAvatar.isValid || !fbxAvatar.isHuman)
                throw new InvalidOperationException($"{V2_FBX}: expected a skinned body and a valid humanoid avatar.");

            var report = new StringBuilder();
            // Check every prefab before writing anything: the body asset is shared, so a prefab that fails after it
            // was overwritten would be left bound to a mesh with another bone order.
            foreach (var path in PREFABS) ApplyPrefab(path, v2Body, null, null, null, commit: false);
            var avatar = BuildAvatar(v2);
            report.AppendLine($"avatar {avatar.name}");
            var material = BuildMaterial();
            report.AppendLine($"material {material.name}");
            var body = SaveBody(v2Body);
            report.AppendLine($"body {body.name}: {body.vertexCount} verts, {body.bindposes.Length} bind poses");
            foreach (var path in PREFABS)
                report.AppendLine(ApplyPrefab(path, v2Body, body, material, avatar, commit: true));
            AssetDatabase.SaveAssets();
            return report.ToString();
        }

        /// <summary>
        /// Blender writes the skinned mesh beside the armature node, so the FBX keeps an extra <c>Armature.001</c> above
        /// Hips and its own avatar doesn't bind to the prefabs (Hips sits directly under the root there). Rebuild the
        /// avatar from a flattened copy of the v2 hierarchy with the FBX's human mapping, and save it as an asset.
        /// </summary>
        private static Avatar BuildAvatar(GameObject v2)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(V2_FBX);
            var description = importer.humanDescription;
            var instance = UnityEngine.Object.Instantiate(v2);
            try
            {
                instance.name = v2.name;
                var hips = instance.GetComponentInChildren<SkinnedMeshRenderer>().rootBone;
                var armature = hips.parent;
                hips.SetParent(instance.transform, true);
                if (armature != instance.transform) UnityEngine.Object.DestroyImmediate(armature.gameObject);
                var skeleton = new List<SkeletonBone>();
                foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                {
                    if (t != instance.transform && !t.IsChildOf(hips)) continue;
                    skeleton.Add(new SkeletonBone
                    {
                        name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale
                    });
                }
                description.skeleton = skeleton.ToArray();
                var avatar = AvatarBuilder.BuildHumanAvatar(instance, description);
                if (!avatar.isValid || !avatar.isHuman)
                    throw new InvalidOperationException("The flattened v2 avatar is not a valid humanoid.");
                avatar.name = "BaseHeroV2Avatar";
                var existing = AssetDatabase.LoadAssetAtPath<Avatar>(AVATAR);
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(avatar, AVATAR);
                    return avatar;
                }
                EditorUtility.CopySerialized(avatar, existing); // keep the GUID the prefabs reference
                existing.name = "BaseHeroV2Avatar";
                EditorUtility.SetDirty(existing);
                UnityEngine.Object.DestroyImmediate(avatar);
                return existing;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static Material BuildMaterial()
        {
            var mask = (TextureImporter)AssetImporter.GetAtPath(MASK);
            if (mask == null) throw new InvalidOperationException($"{MASK} is missing (run make_skin_masks.py).");
            if (mask.textureType != TextureImporterType.SingleChannel || mask.sRGBTexture)
            {
                // Same import as T_SkinMask_BaseHero: single channel R, linear.
                mask.textureType = TextureImporterType.SingleChannel;
                var settings = new TextureImporterSettings();
                mask.ReadTextureSettings(settings);
                settings.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
                mask.SetTextureSettings(settings);
                mask.sRGBTexture = false;
                mask.maxTextureSize = 2048;
                mask.SaveAndReimport();
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL);
            if (material == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Material>(OLD_MATERIAL);
                if (source == null) throw new InvalidOperationException($"{OLD_MATERIAL} is missing.");
                material = new Material(source) { name = "M_BaseHeroV2Body" };
                AssetDatabase.CreateAsset(material, MATERIAL);
            }
            var baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(BASE_COLOR);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NORMAL);
            var skinMask = AssetDatabase.LoadAssetAtPath<Texture2D>(MASK);
            if (baseColor == null || normal == null || skinMask == null)
                throw new InvalidOperationException("Base hero v2 textures are missing.");
            material.SetTexture("_BaseMap", baseColor);
            material.SetTexture("_MainTex", baseColor);
            material.SetTexture("_BumpMap", normal);
            material.SetTexture("_SkinMask", skinMask);
            material.SetFloat("_SkinRefLuminance", SKIN_REF_LUMINANCE);
            material.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Overwrites base_hero_body_baked in place (GUID kept) with the v2 FBX body. Non-finger bind poses are snapped to
        /// the canonical rig's (the FBX round trip leaves ~2e-5 of float noise, above the seam validator's 1e-5), then the
        /// neck join against the canonical head is validated. Finger bones are left out of the join check: the head
        /// isn't weighted to them and their bind poses moved by design.
        /// </summary>
        private static Mesh SaveBody(SkinnedMeshRenderer v2Body)
        {
            var canonical = AssetDatabase.LoadAssetAtPath<GameObject>(OLD_FBX).GetComponentInChildren<SkinnedMeshRenderer>();
            var canonicalPoses = new Dictionary<string, Matrix4x4>();
            for (int i = 0; i < canonical.bones.Length; i++) canonicalPoses[canonical.bones[i].name] = canonical.sharedMesh.bindposes[i];

            var copy = UnityEngine.Object.Instantiate(v2Body.sharedMesh);
            var poses = copy.bindposes;
            var names = new string[v2Body.bones.Length];
            for (int i = 0; i < poses.Length; i++)
            {
                names[i] = v2Body.bones[i].name;
                if (IsFingerBone(names[i]) || !canonicalPoses.TryGetValue(names[i], out var pose)) continue;
                if (MaxDelta(poses[i], pose) > REST_TOLERANCE)
                    throw new InvalidOperationException($"v2 bind pose of {names[i]} is off the canonical rig by {MaxDelta(poses[i], pose):F4}.");
                poses[i] = pose;
            }
            copy.bindposes = poses;

            // Check copy of the head with the v2 finger bind poses (it has no finger weights, so nothing moves).
            var head = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<Mesh>(HEAD_ASSET));
            var headNames = Array.ConvertAll(canonical.bones, b => b.name);
            var headPoses = head.bindposes;
            for (int i = 0; i < headPoses.Length; i++)
            {
                if (!IsFingerBone(headNames[i])) continue;
                int v2Index = Array.IndexOf(names, headNames[i]);
                if (v2Index < 0)
                {
                    UnityEngine.Object.DestroyImmediate(head);
                    UnityEngine.Object.DestroyImmediate(copy);
                    throw new InvalidOperationException($"v2 body lacks the canonical bone {headNames[i]}.");
                }
                headPoses[i] = poses[v2Index];
            }
            head.bindposes = headPoses;
            int aligned = AlignRingTangents(copy, head);
            // Positions, normals and weights must match exactly. UVs: the neck band (submesh 1) is on the head's atlas
            // with the canonical body's UVs, but where the head ring is UV-split (two UVs, two tangents at one
            // position) the body may use only one side, so the shared check runs with UVs / tangents off and
            // RingUvError checks that each body ring UV is one of the head's UVs at that position.
            var join = SkinnedNeckValidator.ValidateExactJoin(head, headNames, copy, names, SEAM,
                uvTolerance: 10f, tangentToleranceDegrees: 180f);
            string uvError = RingUvError(head, copy);
            UnityEngine.Object.DestroyImmediate(head);
            if (!join.IsValid || uvError != null)
            {
                UnityEngine.Object.DestroyImmediate(copy);
                throw new InvalidOperationException($"Neck join failed ({aligned} ring tangents aligned): "
                    + string.Join(" | ", join.Errors) + (uvError != null ? " | " + uvError : ""));
            }
            return SkinnedMeshBaker.SaveMesh(copy, BODY_ASSET);
        }

        /// <summary>
        /// Gives each body ring vertex the tangent of the coincident head vertex (closest tangent among UV-split
        /// alternatives), so normal mapping has no crease at the seam. The head (shared by every NPC) never changes.
        /// </summary>
        private static int AlignRingTangents(Mesh body, Mesh head)
        {
            var hp = head.vertices; var hn = head.normals; var ht = head.tangents;
            var bp = body.vertices; var bn = body.normals; var bt = body.tangents;
            if (ht.Length != hp.Length || bt.Length != bp.Length)
                throw new InvalidOperationException("Head and body need complete tangent frames.");
            var ring = new List<int>();
            for (int i = 0; i < hp.Length; i++)
                if (Mathf.Abs(hp[i].y - SEAM) <= .00001f) ring.Add(i);
            int aligned = 0;
            for (int j = 0; j < bp.Length; j++)
            {
                if (Mathf.Abs(bp[j].y - SEAM) > .00001f) continue;
                int best = -1; float angle = float.PositiveInfinity;
                foreach (int i in ring)
                {
                    if (Vector3.Distance(hp[i], bp[j]) > .0001f || Vector3.Angle(hn[i], bn[j]) > 1f) continue;
                    float a = Vector3.Angle(ht[i], bt[j]);
                    if (a < angle) { best = i; angle = a; }
                }
                if (best < 0) throw new InvalidOperationException($"Body ring vertex {j} has no head counterpart.");
                bt[j] = ht[best];
                aligned++;
            }
            body.tangents = bt;
            return aligned;
        }

        /// <summary>Null when every body vertex on the seam has a UV the head uses at the same position.</summary>
        private static string RingUvError(Mesh head, Mesh body)
        {
            var hp = head.vertices; var hu = head.uv; var bp = body.vertices; var bu = body.uv;
            var ring = new List<int>();
            for (int i = 0; i < hp.Length; i++)
                if (Mathf.Abs(hp[i].y - SEAM) <= .00001f) ring.Add(i);
            int checkedCount = 0;
            for (int j = 0; j < bp.Length; j++)
            {
                if (Mathf.Abs(bp[j].y - SEAM) > .00001f) continue;
                checkedCount++;
                bool match = false;
                foreach (int i in ring)
                    if (Vector3.Distance(hp[i], bp[j]) <= .0001f && Vector2.Distance(hu[i], bu[j]) <= .0001f) { match = true; break; }
                if (!match) return $"Body ring vertex {j} UV {bu[j]} is not one of the head's UVs there.";
            }
            return checkedCount == 0 ? "No body vertex on the seam." : null;
        }

        private static float MaxDelta(Matrix4x4 a, Matrix4x4 b)
        {
            float max = 0f;
            for (int i = 0; i < 16; i++) max = Mathf.Max(max, Mathf.Abs(a[i] - b[i]));
            return max;
        }

        private static string ApplyPrefab(string path, SkinnedMeshRenderer v2Body, Mesh body, Material material, Avatar avatar, bool commit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var smr = FindBody(root);
                if (smr == null) throw new InvalidOperationException($"{path}: no '{BODY_NAME}' SkinnedMeshRenderer.");
                var hips = FindChild(smr.transform.parent, v2Body.rootBone.name);
                if (hips == null) throw new InvalidOperationException($"{path}: no {v2Body.rootBone.name} next to {BODY_NAME}.");

                // Index the prefab skeleton, add missing bones (Ring / Pinky chains, end bones) under their v2 parent.
                var bones = new Dictionary<string, Transform>();
                foreach (var t in hips.GetComponentsInChildren<Transform>(true)) bones.TryAdd(t.name, t);
                int added = 0, fingers = 0;
                var drift = new List<string>();
                foreach (var v2Bone in v2Body.rootBone.GetComponentsInChildren<Transform>(true))
                {
                    if (v2Bone == v2Body.rootBone) continue;
                    if (!bones.TryGetValue(v2Bone.name, out var bone))
                    {
                        if (!bones.TryGetValue(v2Bone.parent.name, out var parent))
                            throw new InvalidOperationException($"{path}: no parent {v2Bone.parent.name} for {v2Bone.name}.");
                        bone = new GameObject(v2Bone.name).transform;
                        bone.SetParent(parent, false);
                        bone.gameObject.layer = parent.gameObject.layer;
                        bones.Add(bone.name, bone);
                        added++;
                    }
                    if (IsFingerBone(v2Bone.name))
                    {
                        bone.SetLocalPositionAndRotation(v2Bone.localPosition, v2Bone.localRotation);
                        bone.localScale = v2Bone.localScale;
                        fingers++;
                    }
                    else if ((bone.localPosition - v2Bone.localPosition).magnitude > REST_TOLERANCE
                             || Quaternion.Angle(bone.localRotation, v2Bone.localRotation) > REST_TOLERANCE * 100f)
                    {
                        drift.Add(v2Bone.name);
                    }
                }
                if (drift.Count > 0)
                    throw new InvalidOperationException($"{path}: non-finger bones differ from the v2 rig: {string.Join(", ", drift)}.");

                if (!commit) return $"{path}: checked";
                var newBones = new Transform[v2Body.bones.Length];
                for (int i = 0; i < newBones.Length; i++)
                    newBones[i] = v2Body.bones[i] == v2Body.rootBone ? hips : bones[v2Body.bones[i].name];
                smr.sharedMesh = body;
                smr.bones = newBones;
                smr.rootBone = hips;
                // Submesh 1 = the neck band on the head's old atlas (fit_base_body.canonical_neck_uvs).
                smr.sharedMaterials = body.subMeshCount > 1
                    ? new[] { material, AssetDatabase.LoadAssetAtPath<Material>(OLD_MATERIAL) }
                    : new[] { material };
                smr.localBounds = v2Body.localBounds;

                int animators = 0;
                foreach (var animator in root.GetComponentsInChildren<Animator>(true))
                {
                    // Only animators on a base hero avatar (old rig or a previous v2 run).
                    if (animator.avatar == null) continue;
                    string avatarPath = AssetDatabase.GetAssetPath(animator.avatar);
                    if (avatarPath != OLD_FBX && avatarPath != V2_FBX && avatarPath != AVATAR) continue;
                    animator.avatar = avatar;
                    animators++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return $"{path}: {fingers} finger bones set ({added} added), {newBones.Length} bones bound, {animators} animator(s) on the v2 avatar";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static SkinnedMeshRenderer FindBody(GameObject root)
        {
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.name == BODY_NAME) return smr;
            return null;
        }

        private static Transform FindChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
                if (child.name == name) return child;
            return null;
        }
    }
}
