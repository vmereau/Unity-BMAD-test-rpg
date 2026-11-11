using Unity.AI.Navigation;
using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>Small, enterable split-plank shelters. Dimensions include the roof overhang.</summary>
    internal static class Area1WorkerHut
    {
        internal const float EAVE = 2.35f;
        internal const float OVERHANG = 0.18f;
        internal const float DOOR = 1.5f;

        internal static Transform Build(string name, Vector2 at, Vector2 size, float yaw, bool rearOpening,
            bool partial, float lift, Terrain terrain, Material timber, Material canvas, Transform parent)
        {
            Transform hut = Area1Stoneworks.Group(name, parent, at, yaw, terrain);
            float w = size.x - OVERHANG * 2f, d = size.y - OVERHANG * 2f;
            // Dirt floors avoid a platform lip through the existing player bed and spawn.
            // Low Mafia decks retain the social distinction without a long flight of steps.
            float floor = lift;
            if (lift > 0f)
                Area1RemoteSites.Local("PlankFloor", hut, timber, new Vector3(0, floor * 0.5f, 0), new Vector3(w, floor, d));
            float wallHeight = partial ? 1.25f : EAVE;
            foreach (float side in new[] { -1f, 1f })
            {
                PlankWall("Side" + side, hut, timber, new Vector3(side * (w * 0.5f - 0.08f), floor + wallHeight * 0.5f, 0), d, wallHeight, 90f);
                foreach (float end in new[] { -1f, 1f })
                    Area1RemoteSites.Local("FramePost", hut, timber, new Vector3(side * (w * 0.5f - 0.08f), floor + EAVE * 0.5f, end * (d * 0.5f - 0.08f)), new Vector3(0.16f, EAVE, 0.16f));
            }
            foreach (float end in new[] { -1f, 1f })
            {
                bool open = end > 0f || rearOpening || partial;
                float z = end * (d * 0.5f - 0.08f);
                if (!open) PlankWall("Back", hut, timber, new Vector3(0, floor + EAVE * 0.5f, z), w, EAVE, 0);
                else
                {
                    float piece = (w - DOOR) * 0.5f;
                    foreach (float side in new[] { -1f, 1f })
                        PlankWall("DoorSide", hut, timber, new Vector3(side * (DOOR + piece) * 0.5f, floor + wallHeight * 0.5f, z), piece, wallHeight, 0);
                }
                Area1RemoteSites.Local("Lintel", hut, timber, new Vector3(0, floor + EAVE, z), new Vector3(w, 0.16f, 0.16f));
            }
            float pitch = 40f, rise = size.x * 0.5f * Mathf.Tan(pitch * Mathf.Deg2Rad);
            foreach (float side in new[] { -1f, 1f })
            {
                var roof = Area1RemoteSites.Local("PlankRoof", hut, partial ? canvas : timber,
                    new Vector3(side * size.x * 0.25f, floor + EAVE + rise * 0.5f, 0),
                    new Vector3(size.x * 0.5f / Mathf.Cos(pitch * Mathf.Deg2Rad), 0.12f, size.y));
                roof.localRotation = Quaternion.Euler(0, 0, -side * pitch);
                var modifier = roof.gameObject.AddComponent<NavMeshModifier>();
                modifier.overrideArea = true;
                modifier.area = 1;
                // Battens make the roof read as rough boards even with flat graybox materials.
                for (int n = 0; n < 6; n++)
                    Area1RemoteSites.Thin("RoofBatten", roof, timber, new Vector3(0, 0.56f, -0.46f + n * 0.184f), new Vector3(1.02f, 0.3f, 0.018f));
            }
            Area1RemoteSites.Thin("RidgeBeam", hut, timber, new Vector3(0, floor + EAVE + rise, 0), new Vector3(0.18f, 0.18f, size.y + 0.12f));
            // Gable braces leave the rear viewing opening clear below the lintel.
            foreach (float end in new[] { -1f, 1f })
                foreach (float side in new[] { -1f, 1f })
                {
                    var brace = Area1RemoteSites.Thin("GableBrace", hut, timber,
                        new Vector3(side * w * 0.25f, floor + EAVE + rise * 0.45f, end * (d * 0.5f)),
                        new Vector3(w * 0.5f / Mathf.Cos(pitch * Mathf.Deg2Rad), 0.14f, 0.14f));
                    brace.localRotation = Quaternion.Euler(0, 0, -side * pitch);
                }
            return hut;
        }

        internal static void PlankWall(string name, Transform parent, Material timber, Vector3 at, float width, float height, float yaw)
        {
            var wall = Area1RemoteSites.Local(name, parent, timber, at, new Vector3(width, height, 0.14f));
            wall.localRotation = Quaternion.Euler(0, yaw, 0);
            int boards = Mathf.Max(2, Mathf.CeilToInt(width / 0.42f));
            for (int i = 0; i < boards; i++)
                Area1RemoteSites.Thin("SplitPlank", wall, timber, new Vector3(-0.5f + (i + 0.5f) / boards, 0, 0.58f), new Vector3(0.92f / boards, i % 3 == 0 ? 0.96f : 1f, 0.28f));
        }

        /// <summary>A sagging two-piece canvas span with posts at the corners, beds along one side.</summary>
        internal static Transform SleepingBay(string name, Vector2 at, Vector2 size, float yaw, Terrain terrain,
            Material timber, Material canvas, Transform parent, bool beds = true)
        {
            Transform bay = Area1Stoneworks.Group(name, parent, at, yaw, terrain);
            foreach (float x in new[] { -size.x * 0.5f, size.x * 0.5f })
                foreach (float z in new[] { -size.y * 0.5f, size.y * 0.5f })
                    Area1RemoteSites.Local("CanvasPost", bay, timber, new Vector3(x, 1.25f, z), new Vector3(0.12f, 2.5f, 0.12f));
            float drop = 0.22f, half = size.x * 0.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                var roof = Area1RemoteSites.Thin("SaggingCanvas", bay, canvas, new Vector3(side * half * 0.5f, 2.5f - drop * 0.5f, 0), new Vector3(Mathf.Sqrt(half * half + drop * drop), 0.06f, size.y));
                roof.localRotation = Quaternion.Euler(0, 0, side * Mathf.Atan2(drop, half) * Mathf.Rad2Deg);
            }
            if (beds)
            {
                for (int i = 0; i < Mathf.FloorToInt(size.x / 1f); i++)
                    Area1RemoteSites.Thin("OverflowBedroll", bay, canvas, new Vector3(-size.x * 0.5f + 0.5f + i, 0.09f, 0), new Vector3(0.72f, 0.16f, Mathf.Min(1.7f, size.y - 0.25f)));
            }
            return bay;
        }
    }
}
