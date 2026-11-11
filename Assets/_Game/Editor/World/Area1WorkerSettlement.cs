using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>Explicit housing plots captured from the hand-arranged camp, independent of legacy tent arrays.</summary>
    internal static class Area1WorkerSettlement
    {
        internal const string PREFIX = "worker-";
        // The finalized scene contains hand-authored duplicates and child geometry the plot generator cannot reproduce.
        public const string HAND_OWNED = "settlement-hand-owned";
        public static bool IsHandOwned(Dictionary<string, JObject> features) =>
            features.TryGetValue(HAND_OWNED, out JObject ownership) && (bool?)ownership["enabled"] == true;
        internal sealed class Plot
        {
            internal string Id, Name, Zone, Style, Replaces;
            internal Vector2 At, Size;
            internal float Yaw;
            internal bool Rear;
            internal string[] Attachments;
            internal Vector2[] Corners => Area1WorkerGeometry.Corners(At, Size, Yaw);
        }

        internal static bool TryRead(Dictionary<string, JObject> features, out List<Plot> plots, out string problem)
        {
            plots = new List<Plot>(); problem = null;
            foreach (var pair in features.Where(p => p.Key.StartsWith(PREFIX)))
            {
                JObject f = pair.Value;
                foreach (string key in new[] { "x", "z", "w", "d", "yaw" })
                    if (!Area1BlockoutBuilder.IsNumber(f[key]) || float.IsNaN((float)f[key]) || float.IsInfinity((float)f[key]))
                    { problem = $"{pair.Key}: invalid {key}"; return false; }
                string style = (string)f["style"], zone = (string)f["zone"];
                if (!new[] { "hut", "partial", "market", "tarp", "canteen", "dining" }.Contains(style)
                    || !new[] { "PlayersZone", "WretchesCorner", "MafiaZone", "Canteen" }.Contains(zone)
                    || (float)f["w"] < 1f || (float)f["d"] < 1f)
                { problem = $"{pair.Key}: invalid style, zone or dimensions"; return false; }
                plots.Add(new Plot { Id = pair.Key, Name = (string)f["name"], At = Area1BlockoutBuilder.Position(f),
                    Size = new Vector2((float)f["w"], (float)f["d"]), Yaw = (float)f["yaw"], Zone = zone, Style = style,
                    Replaces = (string)f["replaces"], Rear = (bool?)f["rearOpening"] ?? false,
                    Attachments = (f["attachments"] as JArray)?.Values<string>().ToArray() ?? new string[0] });
            }
            if (plots.Count == 0) return true;
            if (!plots.Any(p => p.Id == "worker-player") || !plots.Any(p => p.Style == "canteen"))
            { problem = "Worker settlement requires player housing and canteen"; return false; }
            var allPlots = plots;
            foreach (Plot p in plots)
            {
                if (p.Style == "tarp")
                {
                    if (p.Attachments.Length != 2 || p.Attachments[0] == p.Attachments[1]
                        || p.Attachments.Any(id => !allPlots.Any(q => q.Id == id && q.Style != "tarp" && q.Zone == p.Zone)))
                    { problem = $"{p.Id}: tarp needs two distinct same-zone hut attachments"; return false; }
                    foreach (string id in p.Attachments)
                    {
                        Plot q = plots.First(x => x.Id == id);
                        if (!Area1WorkerGeometry.Overlap(p.Corners, q.Corners, 0.65f))
                        { problem = $"{p.Id}: attachment {id} is out of reach"; return false; }
                    }
                }
                for (int i = 0; i < plots.Count; i++)
                {
                    Plot q = plots[i];
                    if (q == p || string.CompareOrdinal(p.Id, q.Id) >= 0) continue;
                    bool attached = p.Attachments.Contains(q.Id) || q.Attachments.Contains(p.Id);
                    if (!attached && Area1WorkerGeometry.Overlap(p.Corners, q.Corners, 0.1f))
                    { problem = $"{p.Id} overlaps {q.Id}"; return false; }
                }
                // The footprint includes roof overhangs; no new solid projects into protected ground.
                foreach (string id in new[] { "camp-centre", "lakeside", "warehouse" })
                {
                    JObject z = features[id];
                    if (Area1WorkerGeometry.Overlap(p.Corners, Area1WorkerGeometry.Corners(Area1BlockoutBuilder.Position(z), new Vector2((float)z["w"], (float)z["d"]), 0)))
                    { problem = $"{p.Id} intersects {id}"; return false; }
                }
            }
            return true;
        }

        /// <summary>Replace only captured housing, leaving live courtyard props and the shifted ditch untouched.</summary>
        internal static void Build(List<Plot> plots, Transform camp, Terrain terrain, Material timber, Material canvas)
        {
            foreach (Plot p in plots)
            {
                Transform zone = camp.Find(p.Zone);
                if (zone == null) { zone = new GameObject(p.Zone).transform; zone.SetParent(camp, false); }
                Transform old = zone.Find(p.Id);
                if (old != null) Object.DestroyImmediate(old.gameObject);
                if (!string.IsNullOrEmpty(p.Replaces))
                {
                    old = zone.Find(p.Replaces);
                    if (old != null) Object.DestroyImmediate(old.gameObject);
                }
                Transform built;
                if (p.Style == "canteen") built = Area1WorkerCanteen.Build(p.At, p.Size, p.Yaw, terrain, timber, canvas, zone);
                else if (p.Style == "tarp" || p.Style == "dining")
                {
                    built = Area1WorkerHut.SleepingBay(p.Id, p.At, p.Size, p.Yaw, terrain, timber, canvas, zone, p.Style == "tarp");
                    if (p.Style == "dining")
                        foreach (float x in new[] { -1.5f, 1.5f })
                        {
                            Area1RemoteSites.Local("Table", built, timber, new Vector3(x, 0.75f, 0), new Vector3(1.8f, 0.12f, 0.8f));
                            foreach (float z in new[] { -0.8f, 0.8f }) Area1RemoteSites.Local("Bench", built, timber, new Vector3(x, 0.25f, z), new Vector3(1.8f, 0.5f, 0.3f));
                        }
                }
                else
                {
                    built = Area1WorkerHut.Build(p.Id, p.At, p.Size, p.Yaw, p.Rear, p.Style == "partial", p.Zone == "MafiaZone" ? 0.15f : 0f, terrain, timber, canvas, zone);
                    if (p.Style == "market")
                        Area1RemoteSites.Local("TradingCounter", built, timber, new Vector3(-0.7f, 0.6f, 0.3f), new Vector3(0.5f, 1f, 1.7f));
                }
                built.name = p.Id;
            }
        }
    }
}
