using Game.Core;
using Game.NPC;
using Game.World;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Gives an NPC its own face (spec-npc-head-variants). On the NPC root: in <c>Awake</c> reads
    /// <see cref="NPCEntity.Head"/> through <see cref="PersistentID"/> and swaps the model's <c>Head</c> SMR mesh and
    /// material. Head variants are baked on the canonical skeleton, so no rebinding is needed — a mesh whose bone count or
    /// bind poses differ from the current head is refused (warning) and the default head stays.
    /// </summary>
    public class NpcHeadAppearance : MonoBehaviour
    {
        private const string TAG = "[NpcAppearance]";
        private const float BIND_POSE_TOLERANCE = 1e-4f;

        [Tooltip("The model's Head SkinnedMeshRenderer (Character/Head in Humanoid_BaseHero).")]
        [SerializeField] private SkinnedMeshRenderer _headRenderer;

        public SkinnedMeshRenderer HeadRenderer => _headRenderer;

        private void Awake()
        {
            if (TryGetComponent(out PersistentID id) && id.Entity is NPCEntity npc && npc.Head != null)
                Apply(npc.Head);
        }

        /// <summary>Applies <paramref name="head"/> to the Head SMR. Returns false (default head kept) when it can't.</summary>
        public bool Apply(HeadAppearanceSO head)
        {
            if (head == null) return false;
            if (_headRenderer == null)
            {
                GameLog.Warn(TAG, $"{name}: no Head renderer assigned — {head.name} not applied");
                return false;
            }
            if (!IsCompatible(head.Mesh, _headRenderer.sharedMesh, out string reason)
                || !MatchesBones(head.Mesh, _headRenderer, out reason))
            {
                GameLog.Warn(TAG, $"{name}: head {head.name} is incompatible with the model ({reason}) — default head kept");
                return false;
            }
            _headRenderer.sharedMesh = head.Mesh;
            if (head.Material != null) _headRenderer.sharedMaterial = head.Material;
            return true;
        }

        /// <summary>
        /// True when <paramref name="candidate"/> can replace <paramref name="current"/> on the same bones: same bind-pose
        /// count, same bind poses, and no bone weight pointing past the bone list.
        /// </summary>
        public static bool IsCompatible(Mesh candidate, Mesh current, out string reason)
        {
            reason = null;
            if (candidate == null) { reason = "no mesh"; return false; }
            if (current == null) { reason = "renderer has no mesh to compare against"; return false; }
            var candidatePoses = candidate.bindposes;
            var currentPoses = current.bindposes;
            if (candidatePoses.Length != currentPoses.Length)
            {
                reason = $"{candidatePoses.Length} bind poses, model has {currentPoses.Length}";
                return false;
            }
            for (int i = 0; i < candidatePoses.Length; i++)
                for (int k = 0; k < 16; k++)
                    if (Mathf.Abs(candidatePoses[i][k] - currentPoses[i][k]) > BIND_POSE_TOLERANCE)
                    {
                        reason = $"bind pose {i} differs";
                        return false;
                    }
            var weights = candidate.GetAllBoneWeights(); // baked head assets are readable (m_IsReadable: 1)
            if (weights.Length == 0)
            {
                reason = "mesh has no bone weights";
                return false;
            }
            for (int i = 0; i < weights.Length; i++)
                if (weights[i].boneIndex < 0 || weights[i].boneIndex >= candidatePoses.Length)
                {
                    reason = $"bone weight index {weights[i].boneIndex} out of range";
                    return false;
                }
            return true;
        }

        /// <summary>True when the renderer has one bone per bind pose of <paramref name="candidate"/>.</summary>
        private static bool MatchesBones(Mesh candidate, SkinnedMeshRenderer renderer, out string reason)
        {
            int poses = candidate.bindposeCount;
            reason = renderer.bones.Length == poses ? null : $"renderer has {renderer.bones.Length} bones, mesh has {poses} bind poses";
            return reason == null;
        }
    }
}
