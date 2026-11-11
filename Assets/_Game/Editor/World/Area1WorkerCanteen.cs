using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>Cookhouse and roofed eating area, with service access on both sides.</summary>
    internal static class Area1WorkerCanteen
    {
        internal static Transform Build(Vector2 at, Vector2 size, float yaw, Terrain terrain, Material timber, Material canvas, Transform parent)
        {
            Transform root = Area1Stoneworks.Group("Cookhouse", parent, at, yaw, terrain);
            // Open frontage reads as a serving bay and avoids putting a new wall through the existing Cook.
            Area1WorkerHut.PlankWall("BackWall", root, timber, new Vector3(0, 1.2f, -size.y * 0.5f + 0.1f), size.x, 2.4f, 0);
            foreach (float side in new[] { -1f, 1f })
            {
                Area1WorkerHut.PlankWall("SideWall", root, timber, new Vector3(side * (size.x * 0.5f - 0.1f), 1.2f, -size.y * 0.18f), size.y * 0.62f, 2.4f, 90);
                Area1RemoteSites.Local("FrontPost", root, timber, new Vector3(side * (size.x * 0.5f - 0.1f), 1.3f, size.y * 0.5f), new Vector3(0.2f, 2.6f, 0.2f));
                var roof = Area1RemoteSites.Local("TimberRoof", root, timber, new Vector3(side * size.x * 0.25f, 3.1f, 0), new Vector3(size.x * 0.5f / Mathf.Cos(25f * Mathf.Deg2Rad), 0.16f, size.y + 0.3f));
                roof.localRotation = Quaternion.Euler(0, 0, -side * 25f);
                var nav = roof.gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>(); nav.overrideArea = true; nav.area = 1;
            }
            Area1RemoteSites.Local("ServingCounter", root, timber, new Vector3(0, 0.5f, size.y * 0.5f - 0.25f), new Vector3(size.x - 3f, 1f, 0.5f));
            Area1RemoteSites.Thin("ServingAwning", root, canvas, new Vector3(0, 2.65f, size.y * 0.5f + 0.55f), new Vector3(size.x, 0.06f, 1.4f));
            Area1RemoteSites.Local("Stores", root, timber, new Vector3(-size.x * 0.25f, 0.35f, -size.y * 0.3f), new Vector3(1f, 0.7f, 0.8f));
            return root;
        }
    }
}
