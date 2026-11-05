using System.IO;
using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>
    /// Hand edits to the valley terrain, stored as height offsets over what <see cref="Area1TerrainBuilder"/> generates, so a
    /// rebuild keeps them. <see cref="BuiltHash"/> is the height hash of the last build or capture: a different live terrain
    /// has edits this file doesn't hold.
    /// </summary>
    public sealed class Area1TerrainSculpt
    {
        public const string FILE = "docs/World/Areas/area-001/terrain-sculpt.bin";

        private const int MAGIC = 0x31534131;
        private const float NOISE = 0.02f; // heightmap rounding between a build and the live terrain
        private const float CENTIMETRES = 100f;

        public int X, Z, Width, Depth;
        public uint BuiltHash;
        public short[] Offsets = new short[0];

        public static string FullPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", FILE));

        /// <summary>Offsets from <paramref name="generated"/> to <paramref name="live"/>, cropped to what changed. Both in metres.</summary>
        public static Area1TerrainSculpt Capture(float[,] generated, float[,] live)
        {
            int rows = generated.GetLength(0), columns = generated.GetLength(1);
            int minX = columns, maxX = -1, minZ = rows, maxZ = -1;
            for (int z = 0; z < rows; z++)
            {
                for (int x = 0; x < columns; x++)
                {
                    if (Mathf.Abs(live[z, x] - generated[z, x]) <= NOISE) continue;
                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minZ = Mathf.Min(minZ, z);
                    maxZ = Mathf.Max(maxZ, z);
                }
            }

            var sculpt = new Area1TerrainSculpt();
            if (maxX < 0) return sculpt;

            sculpt.X = minX;
            sculpt.Z = minZ;
            sculpt.Width = maxX - minX + 1;
            sculpt.Depth = maxZ - minZ + 1;
            sculpt.Offsets = new short[sculpt.Width * sculpt.Depth];
            for (int z = 0; z < sculpt.Depth; z++)
            {
                for (int x = 0; x < sculpt.Width; x++)
                {
                    float offset = live[z + minZ, x + minX] - generated[z + minZ, x + minX];
                    if (Mathf.Abs(offset) > NOISE) sculpt.Offsets[z * sculpt.Width + x] = (short)Mathf.RoundToInt(offset * CENTIMETRES);
                }
            }
            return sculpt;
        }

        public void Apply(float[,] heights)
        {
            for (int z = 0; z < Depth; z++)
                for (int x = 0; x < Width; x++)
                    heights[z + Z, x + X] += Offsets[z * Width + x] / CENTIMETRES;
        }

        public void Save()
        {
            using var writer = new BinaryWriter(File.Create(FullPath));
            writer.Write(MAGIC);
            writer.Write(BuiltHash);
            writer.Write(X);
            writer.Write(Z);
            writer.Write(Width);
            writer.Write(Depth);
            foreach (short offset in Offsets) writer.Write(offset);
        }

        public static bool TryLoad(out Area1TerrainSculpt sculpt)
        {
            sculpt = null;
            if (!File.Exists(FullPath)) return false;

            using var reader = new BinaryReader(File.OpenRead(FullPath));
            if (reader.BaseStream.Length < 24 || reader.ReadInt32() != MAGIC) return false;

            var loaded = new Area1TerrainSculpt
            {
                BuiltHash = reader.ReadUInt32(), X = reader.ReadInt32(), Z = reader.ReadInt32(), Width = reader.ReadInt32(), Depth = reader.ReadInt32(),
            };
            if (reader.BaseStream.Length != 24L + loaded.Width * loaded.Depth * 2L) return false;

            loaded.Offsets = new short[loaded.Width * loaded.Depth];
            for (int i = 0; i < loaded.Offsets.Length; i++) loaded.Offsets[i] = reader.ReadInt16();
            sculpt = loaded;
            return true;
        }
    }
}
