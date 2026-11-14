using System.Collections.Generic;
using Game.NPC;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Runtime toned copies of <c>SG_SkinLit</c> materials (spec-npc-skin-tones): one per (source material, tone), shared by
    /// every NPC with that tone. Plain material copies (no MaterialPropertyBlock), so toned renderers stay in the SRP
    /// Batcher. The cache is bounded (tones × skin materials) and lives for the session.
    /// </summary>
    public static class SkinToneMaterials
    {
        public const string SHADER_NAME = "Game/SG_SkinLit";
        private static readonly int SkinMaskId = Shader.PropertyToID("_SkinMask");
        private static readonly int SkinToneId = Shader.PropertyToID("_SkinTone");
        private static readonly int StrengthId = Shader.PropertyToID("_SkinToneStrength");

        private static readonly Dictionary<(Material, SkinToneSO), Material> Cache = new();
        private static readonly Dictionary<Material, Material> Toned = new(); // toned copy → its source

        /// <summary>True when <paramref name="material"/> uses the skin shader.</summary>
        public static bool IsSkin(Material material) => material != null && material.shader != null && material.shader.name == SHADER_NAME;

        /// <summary>True when <paramref name="material"/> is a skin material with a mask (it can take a tone).</summary>
        public static bool HasMask(Material material) => IsSkin(material) && material.GetTexture(SkinMaskId) != null;

        /// <summary>True when <paramref name="material"/> is a copy made by this cache.</summary>
        public static bool IsToned(Material material) => material != null && Toned.ContainsKey(material);

        /// <summary>The source of a toned copy, or <paramref name="material"/> itself when it isn't one.</summary>
        public static Material SourceOf(Material material) =>
            material != null && Toned.TryGetValue(material, out var source) ? source : material;

        public static int Count => Cache.Count;

        /// <summary>
        /// The toned copy of <paramref name="source"/>, created on first use. Returns <paramref name="source"/> itself when it
        /// is already a toned copy, isn't a masked skin material, or <paramref name="tone"/> is null.
        /// </summary>
        public static Material Get(Material source, SkinToneSO tone)
        {
            if (tone == null || IsToned(source) || !HasMask(source)) return source;
            if (Cache.TryGetValue((source, tone), out var toned) && toned != null) return toned;
            toned = new Material(source) { name = $"{source.name} ({tone.name})", hideFlags = HideFlags.DontSave };
            toned.SetColor(SkinToneId, tone.Tone);
            toned.SetFloat(StrengthId, tone.Strength);
            Cache[(source, tone)] = toned;
            Toned[toned] = source;
            return toned;
        }

        /// <summary>
        /// Destroys every cached copy (tests; session start with domain reload disabled). Renderers still using one lose
        /// their material — never call it while toned NPCs are alive.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            foreach (var material in Toned.Keys)
            {
                if (material == null) continue;
                if (Application.isPlaying) Object.Destroy(material);
                else Object.DestroyImmediate(material);
            }
            Cache.Clear();
            Toned.Clear();
        }
    }
}
