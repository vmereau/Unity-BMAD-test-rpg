using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>
    /// The graybox worker tent camp and the camp centre. Tent quality falls from east to west: Mafia wall tents on platforms past
    /// the ditch, standard tents in the Player's Zone, lean-tos in Wretches' Corner by the latrine. The punishment post stands on a
    /// cleared square with three tent-free sight lanes to the arrival gate, the wilderness opening and the inner gate.
    /// <see cref="Area1SitesBuilder"/> reads the layout through <see cref="TryRead"/> before any root is replaced.
    /// </summary>
    internal static class Area1TentCamp
    {
        private enum Kind { LeanTo, Standard, WallTent, Fire, Prop }

        /// <summary>Something that takes ground: a tent or a fire, as a circle the checks can test.</summary>
        private readonly struct Footprint
        {
            public readonly string Name;
            public readonly Vector2 At;
            public readonly float Radius;
            public readonly Kind Kind;

            public Footprint(string name, Vector2 at, float radius, Kind kind)
            {
                Name = name;
                At = at;
                Radius = radius;
                Kind = kind;
            }
        }

        /// <summary>Everything the build needs, read and checked from the layout.</summary>
        internal sealed class Plan
        {
            public Vector2 Post;
            public Vector2 SquareSize;
            public Vector2[] LaneEnds;
            public Vector2 PlayersTent;
            public Vector2[] PlayersTents;
            public Vector2 PlayersFire;
            public Vector2[] WretchesTents;
            public Vector2 WretchesFire;
            public Vector2[] MafiaTents;
            public Vector2 MafiaFire;
            public Vector2 BlackMarket;
            public float BlackMarketYaw;
            public Vector2[] Ditch;
            public float DitchWidth;
            public Vector2 HuntersFire;
            public Vector2 InnerGate;
            public Vector2 WarehouseCorner;
            public List<Vector2> FlagPoles;
        }

        internal const string TENT_CAMP = "TentCamp";
        internal const string CAMP_CENTRE = "CampCentre";
        private const string CENTRE_FEATURE = "camp-centre";
        private const string PLAYERS_TENT_FEATURE = "players-tent";
        private const string BLACK_MARKET_FEATURE = "black-market";
        private const string DITCH_FEATURE = "zone-ditch";
        private const string ARRIVAL_GATE_FEATURE = "arrival-gate";
        private const string WILDERNESS_FEATURE = "wilderness-trail";
        private const string INNER_GATE_FEATURE = "inner-gate";
        private const string LAKESIDE_FEATURE = "lakeside";
        private const string FIGHT_PIT_FEATURE = "fight-pit";
        private const string WAREHOUSE_FEATURE = "warehouse";
        private const string WORK_GANG_FEATURE = "work-gang-tents";
        private const string LATRINE_FEATURE = "latrine";
        private const string HUNTERS_FIRE_FEATURE = "hunter-fire";
        internal static readonly string[] PaintOnly = { "path-camp-square", "path-arrival", "path-commute", "path-daily" };

        private const float LANE_HALF = 3f;           // 6 m wide: the Company keeps its own lines of view open
        private const float TENT_GAP = 1f;            // between two tents' footprints
        private const float FENCE_CLEARANCE = 1f;     // from the inner fence
        private const float PIT_BANK = 7f;            // the bank reaches w / 2 + this from the pit's centre
        private const float LATRINE_REACH = 3.2f;     // trench, bench and screens round the latrine point
        private const float MIN_SEGMENT = 0.5f;       // a shorter lane or ditch piece is a typo, and would skip its checks
        private const float POLE_CLEARANCE = 0.5f;    // flag poles stand at least this far outside a lane
        private const float POLE_SPACING = 3f;

        private const float STANDARD_RADIUS = 2.3f;   // Tent_Graybox with its sacks and log
        private const float LEAN_TO_RADIUS = 1.45f;
        private const float WALL_TENT_RADIUS = 3.2f;  // the platform's half diagonal
        private const float WRETCHES_FIRE_RADIUS = 0.9f;
        private const float PLAYERS_FIRE_RADIUS = 2f;   // two logs
        private const float CAMPFIRE_RADIUS = 1f;       // the player's: the card game sits beside it
        private const float CARD_GAME_RADIUS = 1.2f;
        private const float MAFIA_FIRE_RADIUS = 2f;     // benches

        // The player's group, laid out from the tent: the tent mate next door, the trunk between them, the fire in front.
        private static readonly Vector2 MateOffset = new Vector2(-5.6f, 1f);
        private static readonly Vector2 FireOffset = new Vector2(-1.5f, -3.5f);
        private static readonly Vector2 CardOffset = new Vector2(-2.25f, 0.2f); // from the fire, away from the wilderness lane
        private const float TRUNK_FORWARD = 1.2f;

        private const float STAGE_SIZE = 4.5f;
        private const float STAGE_HEIGHT = 1.2f;
        private const int STAGE_STEPS = 4;            // 0.24 m each, then the platform: under the player's 0.3 m step offset
        private const float MAX_RISE = 0.28f;         // wall tent steps, for the same reason
        private const float STEP_DEPTH = 0.4f;
        private const float STEP_WIDTH = 1.6f;
        private const float POST_HEIGHT = 3.2f;
        private const float POLE_HEIGHT = 6f;
        private const float WAREHOUSE_POLE_TOP = 13f;  // world height, over the warehouse ridge
        private static readonly Vector2[] GatePoles = { new Vector2(-4.8f, 1.2f), new Vector2(4f, 1.2f) }; // camp side of the inner gate posts

        private const float BURY = 0.3f;
        private const float PLATFORM_LIFT = 0.25f;
        private const float TENT_WALL = 2.1f;       // the door is the wall height: over the player's 1.7 m capsule and its 0.9 m centre
        private const float ROOF_PITCH = 35f;
        private const float CANVAS = 0.08f;
        private const float DITCH_PIECE = 3f;
        private static readonly float[] DitchCrossings = { 0.26f, 0.68f };

        /// <summary>Reads and checks every tent camp feature. Fails before anything is built, naming the feature or the tent.</summary>
        internal static bool TryRead(Dictionary<string, JObject> features, out Plan plan, out string problem)
        {
            plan = null;
            problem = null;
            foreach (string id in new[] { CENTRE_FEATURE, LAKESIDE_FEATURE, FIGHT_PIT_FEATURE, WAREHOUSE_FEATURE, WORK_GANG_FEATURE })
                if (!features.TryGetValue(id, out JObject zone) || !Area1BlockoutBuilder.HasPosition(zone) || !IsPositive(zone["w"]) || !IsPositive(zone["d"]))
                    return Fail($"Layout has no usable feature '{id}'", out problem);

            foreach (string id in new[] { PLAYERS_TENT_FEATURE, BLACK_MARKET_FEATURE, ARRIVAL_GATE_FEATURE, INNER_GATE_FEATURE, LATRINE_FEATURE, HUNTERS_FIRE_FEATURE, "fire-players", "fire-wretches", "fire-mafia" })
                if (!features.TryGetValue(id, out JObject point) || !Area1BlockoutBuilder.HasPosition(point))
                    return Fail($"Layout has no usable feature '{id}'", out problem);

            if (!Area1BlockoutBuilder.IsNumber(features[BLACK_MARKET_FEATURE]["yaw"]))
                return Fail($"Layout has no usable feature '{BLACK_MARKET_FEATURE}'", out problem);
            if (features.TryGetValue(DITCH_FEATURE, out JObject ditchFeature) && !IsPositive(ditchFeature["width"]))
                return Fail($"Layout has no usable feature '{DITCH_FEATURE}'", out problem);

            foreach (string id in new[] { "tents-players", "tents-wretches", "tents-mafia", DITCH_FEATURE, WILDERNESS_FEATURE }.Concat(PaintOnly))
                if (!features.TryGetValue(id, out JObject line) || !Area1BlockoutBuilder.HasPoints(line))
                    return Fail($"Layout has no usable feature '{id}'", out problem);

            JObject centre = features[CENTRE_FEATURE];
            JObject warehouse = features[WAREHOUSE_FEATURE];
            Vector2 warehouseAt = Area1BlockoutBuilder.Position(warehouse);
            plan = new Plan
            {
                Post = Area1BlockoutBuilder.Position(centre),
                SquareSize = new Vector2((float)centre["w"], (float)centre["d"]),
                LaneEnds = new[]
                {
                    Area1BlockoutBuilder.Position(features[ARRIVAL_GATE_FEATURE]),
                    Area1BlockoutBuilder.Points(features[WILDERNESS_FEATURE])[0],
                    Area1BlockoutBuilder.Position(features[INNER_GATE_FEATURE]),
                },
                PlayersTent = Area1BlockoutBuilder.Position(features[PLAYERS_TENT_FEATURE]),
                PlayersTents = Area1BlockoutBuilder.Points(features["tents-players"]),
                PlayersFire = Area1BlockoutBuilder.Position(features["fire-players"]),
                WretchesTents = Area1BlockoutBuilder.Points(features["tents-wretches"]),
                WretchesFire = Area1BlockoutBuilder.Position(features["fire-wretches"]),
                MafiaTents = Area1BlockoutBuilder.Points(features["tents-mafia"]),
                MafiaFire = Area1BlockoutBuilder.Position(features["fire-mafia"]),
                BlackMarket = Area1BlockoutBuilder.Position(features[BLACK_MARKET_FEATURE]),
                BlackMarketYaw = (float)features[BLACK_MARKET_FEATURE]["yaw"],
                Ditch = Area1BlockoutBuilder.Points(features[DITCH_FEATURE]),
                DitchWidth = (float)features[DITCH_FEATURE]["width"],
                HuntersFire = Area1BlockoutBuilder.Position(features[HUNTERS_FIRE_FEATURE]),
                InnerGate = Area1BlockoutBuilder.Position(features[INNER_GATE_FEATURE]),
                WarehouseCorner = warehouseAt - new Vector2((float)warehouse["w"], (float)warehouse["d"]) * 0.5f - new Vector2(0.3f, 0.3f),
            };
            foreach (Vector2 end in plan.LaneEnds)
                if (Vector2.Distance(plan.Post, end) < MIN_SEGMENT) return Fail($"The sight lane to {end} starts and ends at the post", out problem);
            for (int i = 1; i < plan.Ditch.Length; i++)
                if (Vector2.Distance(plan.Ditch[i - 1], plan.Ditch[i]) < MIN_SEGMENT) return Fail($"'{DITCH_FEATURE}' has a repeated point at {plan.Ditch[i]}", out problem);
            plan.FlagPoles = FlagPoles(plan);

            Rect square = Centred(plan.Post, plan.SquareSize);
            Rect lakeside = Centred(Area1BlockoutBuilder.Position(features[LAKESIDE_FEATURE]), new Vector2((float)features[LAKESIDE_FEATURE]["w"], (float)features[LAKESIDE_FEATURE]["d"]));
            Vector2 pit = Area1BlockoutBuilder.Position(features[FIGHT_PIT_FEATURE]);
            float pitReach = (float)features[FIGHT_PIT_FEATURE]["w"] * 0.5f + PIT_BANK;
            Rect warehouseRect = Centred(warehouseAt, new Vector2((float)warehouse["w"], (float)warehouse["d"]));
            JObject workGang = features[WORK_GANG_FEATURE];
            Rect workGangRect = Centred(Area1BlockoutBuilder.Position(workGang), new Vector2((float)workGang["w"], (float)workGang["d"]));
            Vector2 latrine = Area1BlockoutBuilder.Position(features[LATRINE_FEATURE]);

            List<Footprint> footprints = Footprints(plan);
            foreach (Footprint f in footprints)
            {
                for (int i = 0; i < plan.LaneEnds.Length; i++)
                    if (DistanceToSegment(f.At, plan.Post, plan.LaneEnds[i]) < LANE_HALF + f.Radius)
                        return Fail($"{f.Name} at {f.At} stands in the sight lane to {plan.LaneEnds[i]}", out problem);
                if (Overlaps(square, f)) return Fail($"{f.Name} at {f.At} stands on the camp square", out problem);
                if (Overlaps(lakeside, f)) return Fail($"{f.Name} at {f.At} stands on the lakeside", out problem);
                if (Vector2.Distance(f.At, pit) < pitReach + f.Radius) return Fail($"{f.Name} at {f.At} stands on the fight pit's bank", out problem);
                if (f.At.y - f.Radius < plan.InnerGate.y + FENCE_CLEARANCE) return Fail($"{f.Name} at {f.At} stands against the inner fence", out problem);
                if (Overlaps(warehouseRect, f)) return Fail($"{f.Name} at {f.At} stands in the warehouse", out problem);
                if (Overlaps(workGangRect, f)) return Fail($"{f.Name} at {f.At} stands among the work-gang tents", out problem);
                if (Vector2.Distance(f.At, latrine) < LATRINE_REACH + f.Radius) return Fail($"{f.Name} at {f.At} stands in the latrine", out problem);
                for (int i = 1; i < plan.Ditch.Length; i++)
                    if (DistanceToSegment(f.At, plan.Ditch[i - 1], plan.Ditch[i]) < plan.DitchWidth * 0.5f + f.Radius)
                        return Fail($"{f.Name} at {f.At} stands in the ditch", out problem);
            }

            for (int a = 0; a < footprints.Count; a++)
                for (int b = a + 1; b < footprints.Count; b++)
                {
                    Footprint fa = footprints[a], fb = footprints[b];
                    bool tents = IsTent(fa.Kind) && IsTent(fb.Kind);
                    if (Vector2.Distance(fa.At, fb.At) < fa.Radius + fb.Radius + (tents ? TENT_GAP : 0f))
                        return Fail($"{fa.Name} at {fa.At} and {fb.Name} at {fb.At} are too close", out problem);
                }

            if (plan.FlagPoles.Count < 3) return Fail("The camp square has room for fewer than three flag poles outside the lanes", out problem);
            return true;
        }

        internal static void BuildCampCentre(Plan plan, Terrain terrain, Material timber, Material stone, Material flag, Transform root)
        {
            Transform stage = Area1Stoneworks.Group("Stage", root, plan.Post, Yaw(plan.Post - plan.InnerGate), terrain); // steps on local -Z, towards the inner gate
            float half = STAGE_SIZE * 0.5f;
            Area1RemoteSites.Local("Platform", stage, timber, new Vector3(0f, (STAGE_HEIGHT - BURY) * 0.5f, 0f), new Vector3(STAGE_SIZE, STAGE_HEIGHT + BURY, STAGE_SIZE));
            float rise = STAGE_HEIGHT / (STAGE_STEPS + 1);
            for (int i = 0; i < STAGE_STEPS; i++)
            {
                float top = rise * (i + 1);
                float z = -half - STEP_DEPTH * (STAGE_STEPS - i - 0.5f);
                Area1RemoteSites.Local($"Step_{i}", stage, timber, new Vector3(0f, (top - BURY) * 0.5f, z), new Vector3(STEP_WIDTH, top + BURY, STEP_DEPTH));
            }

            Area1RemoteSites.Local("Post", stage, timber, new Vector3(0f, STAGE_HEIGHT + POST_HEIGHT * 0.5f, 0.8f), new Vector3(0.3f, POST_HEIGHT, 0.3f));
            Area1RemoteSites.Local("Pillory_Board", stage, timber, new Vector3(0f, STAGE_HEIGHT + 1.45f, 0.6f), new Vector3(1.6f, 0.35f, 0.12f));
            Area1RemoteSites.Local("Pillory_Top", stage, timber, new Vector3(0f, STAGE_HEIGHT + 1.75f, 0.6f), new Vector3(1.6f, 0.12f, 0.16f));
            Area1RemoteSites.Thin("Chain", stage, stone, new Vector3(0f, STAGE_HEIGHT + 0.6f, 0.62f), new Vector3(0.05f, 1.2f, 0.05f));
            Area1RemoteSites.Thin("Banner", stage, flag, new Vector3(0f, STAGE_HEIGHT + POST_HEIGHT - 0.7f, 0.98f), new Vector3(0.9f, 1.2f, 0.04f));

            for (int i = 0; i < plan.FlagPoles.Count; i++)
                Pole($"Flag_{i}", plan.FlagPoles[i], POLE_HEIGHT, i * 50f, terrain, timber, flag, root);
            Pole("Flag_InnerGate_West", plan.InnerGate + GatePoles[0], POLE_HEIGHT, 0f, terrain, timber, flag, root);
            Pole("Flag_InnerGate_East", plan.InnerGate + GatePoles[1], POLE_HEIGHT, 0f, terrain, timber, flag, root);
            float warehouseGround = Area1BlockoutBuilder.Ground(terrain, plan.WarehouseCorner);
            Pole("Flag_Warehouse", plan.WarehouseCorner, Mathf.Max(POLE_HEIGHT, WAREHOUSE_POLE_TOP - warehouseGround), 90f, terrain, timber, flag, root);
        }

        internal static void BuildTentCamp(Plan plan, GameObject tent, Terrain terrain, Material timber, Material stone, Material canvas, Transform root)
        {
            Transform players = Child("PlayersZone", root);
            Vector2 campfire = plan.PlayersTent + FireOffset;
            Vector2 mate = plan.PlayersTent + MateOffset;
            // Open at both ends: the back looks at the hunter's fire through the broken palisade, the front opens towards the campfire.
            Standard("PlayersTent", plan.PlayersTent, plan.PlayersTent * 2f - plan.HuntersFire, tent, terrain, players);
            Standard("TentMatesTent", mate, campfire, tent, terrain, players);
            Vector2 between = Vector2.Lerp(plan.PlayersTent, mate, 0.6f); // nearer the tent mate, clear of the player's tent sacks
            BuildTrunk(between + (campfire - between).normalized * TRUNK_FORWARD, campfire, terrain, timber, stone, players);
            FireRing("Campfire", campfire, 0, terrain, timber, stone, players);
            BuildCardGame(campfire + CardOffset, campfire, terrain, timber, canvas, players);
            Vector2[] playerFires = { campfire, plan.PlayersFire };
            for (int i = 0; i < plan.PlayersTents.Length; i++)
                Standard($"Tent_{i}", plan.PlayersTents[i], Nearest(plan.PlayersTents[i], playerFires), tent, terrain, players);
            FireRing("Fire", plan.PlayersFire, 2, terrain, timber, stone, players);

            Transform wretches = Child("WretchesCorner", root);
            for (int i = 0; i < plan.WretchesTents.Length; i++)
                LeanTo($"LeanTo_{i}", plan.WretchesTents[i], plan.WretchesFire, terrain, timber, canvas, wretches);
            FireRing("Fire", plan.WretchesFire, 0, terrain, timber, stone, wretches);

            Transform mafia = Child("MafiaZone", root);
            for (int i = 0; i < plan.MafiaTents.Length; i++)
            {
                Vector2 at = plan.MafiaTents[i];
                Transform wall = WallTent($"WallTent_{i}", at, Yaw(plan.MafiaFire - at), terrain, timber, canvas, mafia, out float floor);
                Area1RemoteSites.Local("Cot", wall, timber, new Vector3(-1.2f, floor + 0.25f, -1.2f), new Vector3(0.9f, 0.5f, 2f));
                Area1RemoteSites.Local("Chest", wall, timber, new Vector3(1.3f, floor + 0.3f, -1.9f), new Vector3(0.9f, 0.6f, 0.6f));
                Area1RemoteSites.Local("Barrel", wall, timber, new Vector3(-1.5f, floor + 0.45f, 1.9f), new Vector3(0.6f, 0.9f, 0.6f));
            }
            FireRing("Fire", plan.MafiaFire, 4, terrain, timber, stone, mafia);

            Transform market = WallTent("BlackMarket", plan.BlackMarket, plan.BlackMarketYaw, terrain, timber, canvas, mafia, out float marketFloor);
            Area1RemoteSites.Local("Counter", market, timber, new Vector3(-1.35f, marketFloor + 0.475f, 1.75f), new Vector3(0.5f, 0.95f, 1.3f)); // along the porch side, clear of the steps and door
            Area1RemoteSites.Local("Shelves", market, timber, new Vector3(0f, marketFloor + 0.8f, -2.15f), new Vector3(3.2f, 1.6f, 0.4f));
            Area1RemoteSites.Local("Crate_0", market, timber, new Vector3(-1.4f, marketFloor + 0.3f, -0.6f), new Vector3(0.7f, 0.6f, 0.7f));
            Area1RemoteSites.Local("Crate_1", market, timber, new Vector3(1.3f, marketFloor + 0.35f, -0.9f), new Vector3(0.8f, 0.7f, 0.8f));
            Area1RemoteSites.Thin("Goods", market, canvas, new Vector3(-1.35f, marketFloor + 1f, 1.75f), new Vector3(0.35f, 0.1f, 1f));

            BuildDitch(plan.Ditch, plan.DitchWidth, terrain, timber, stone, Child("Ditch", root));
        }

        /// <summary>The square's corners and side middles that stand clear of every lane, at least <see cref="POLE_SPACING"/> apart.</summary>
        private static List<Vector2> FlagPoles(Plan plan)
        {
            Vector2 half = plan.SquareSize * 0.5f;
            var candidates = new List<Vector2>();
            foreach (float x in new[] { -1f, 0f, 1f })
                foreach (float z in new[] { -1f, 0f, 1f })
                    if (x != 0f || z != 0f) candidates.Add(plan.Post + new Vector2(x * half.x, z * half.y));

            var poles = new List<Vector2>();
            foreach (Vector2 c in candidates)
            {
                if (plan.LaneEnds.Any(end => DistanceToSegment(c, plan.Post, end) < LANE_HALF + POLE_CLEARANCE)) continue;
                if (poles.Any(p => Vector2.Distance(p, c) < POLE_SPACING)) continue;
                poles.Add(c);
            }
            return poles;
        }

        private static List<Footprint> Footprints(Plan plan)
        {
            Vector2 campfire = plan.PlayersTent + FireOffset;
            var list = new List<Footprint>
            {
                new Footprint("Player's tent", plan.PlayersTent, STANDARD_RADIUS, Kind.Standard),
                new Footprint("Tent mate's tent", plan.PlayersTent + MateOffset, STANDARD_RADIUS, Kind.Standard),
                new Footprint("Player's campfire", campfire, CAMPFIRE_RADIUS, Kind.Fire),
                new Footprint("Card game", campfire + CardOffset, CARD_GAME_RADIUS, Kind.Prop),
                new Footprint("fire-players", plan.PlayersFire, PLAYERS_FIRE_RADIUS, Kind.Fire),
                new Footprint("fire-wretches", plan.WretchesFire, WRETCHES_FIRE_RADIUS, Kind.Fire),
                new Footprint("fire-mafia", plan.MafiaFire, MAFIA_FIRE_RADIUS, Kind.Fire),
                new Footprint("Black market", plan.BlackMarket, WALL_TENT_RADIUS, Kind.WallTent),
            };
            for (int i = 0; i < plan.PlayersTents.Length; i++) list.Add(new Footprint($"tents-players[{i}]", plan.PlayersTents[i], STANDARD_RADIUS, Kind.Standard));
            for (int i = 0; i < plan.WretchesTents.Length; i++) list.Add(new Footprint($"tents-wretches[{i}]", plan.WretchesTents[i], LEAN_TO_RADIUS, Kind.LeanTo));
            for (int i = 0; i < plan.MafiaTents.Length; i++) list.Add(new Footprint($"tents-mafia[{i}]", plan.MafiaTents[i], WALL_TENT_RADIUS, Kind.WallTent));
            return list;
        }

        /// <summary>Tent_Graybox is open at both ends of its ridge, which runs along local Z: one end faces the fire.</summary>
        private static void Standard(string name, Vector2 at, Vector2 fire, GameObject tent, Terrain terrain, Transform parent)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(tent, parent);
            instance.name = name;
            instance.transform.SetPositionAndRotation(new Vector3(at.x, Area1BlockoutBuilder.Ground(terrain, at), at.y), Quaternion.Euler(0f, Yaw(fire - at), 0f));
        }

        /// <summary>Canvas over two posts, high at the front (local +Z, towards the fire) and down to the ground at the back.</summary>
        private static void LeanTo(string name, Vector2 at, Vector2 fire, Terrain terrain, Material timber, Material canvas, Transform parent)
        {
            Transform shelter = Area1Stoneworks.Group(name, parent, at, Yaw(fire - at), terrain);
            foreach (float x in new[] { -1f, 1f })
                Area1RemoteSites.Local(x < 0f ? "Post_West" : "Post_East", shelter, timber, new Vector3(x, (1.4f - BURY) * 0.5f, 0.8f), new Vector3(0.1f, 1.4f + BURY, 0.1f));
            float slope = Mathf.Atan2(1.25f, 1.8f) * Mathf.Rad2Deg;
            Area1RemoteSites.Local("Roof", shelter, canvas, new Vector3(0f, 0.78f, 0f), new Vector3(2.3f, 0.05f, 2.2f)).localRotation = Quaternion.Euler(-slope, 0f, 0f);
            Area1RemoteSites.Thin("Rag", shelter, canvas, new Vector3(-1.05f, 0.4f, 0.1f), new Vector3(0.04f, 0.8f, 1.4f));
            Area1RemoteSites.Thin("Bedroll", shelter, canvas, new Vector3(0.25f, 0.08f, -0.1f), new Vector3(0.75f, 0.15f, 1.6f));
        }

        /// <summary>
        /// A plank platform over the highest ground under it, a canvas room with a door on local +Z, a ridge roof and an awning over
        /// the porch. Returns the group; <paramref name="floor"/> is the platform top, local to it.
        /// </summary>
        private static Transform WallTent(string name, Vector2 at, float yaw, Terrain terrain, Material timber, Material canvas, Transform parent, out float floor)
        {
            Transform tent = Area1Stoneworks.Group(name, parent, at, yaw, terrain);
            const float halfWidth = 2f, halfDepth = 2.5f, back = -2.5f, front = 1f;
            float baseY = tent.position.y, high = float.NegativeInfinity, low = float.PositiveInfinity;
            foreach (float x in new[] { -halfWidth, 0f, halfWidth })
                foreach (float z in new[] { -halfDepth, 0f, halfDepth })
                {
                    Vector3 world = tent.TransformPoint(new Vector3(x, 0f, z));
                    float ground = Area1BlockoutBuilder.Ground(terrain, new Vector2(world.x, world.z)) - baseY;
                    high = Mathf.Max(high, ground);
                    low = Mathf.Min(low, ground);
                }

            floor = high + PLATFORM_LIFT;
            Area1RemoteSites.Local("Platform", tent, timber, new Vector3(0f, (floor + low - BURY) * 0.5f, 0f), new Vector3(halfWidth * 2f, floor - low + BURY, halfDepth * 2f));
            Vector3 frontGround = tent.TransformPoint(new Vector3(0f, 0f, halfDepth + 0.3f));
            float frontLow = Area1BlockoutBuilder.Ground(terrain, new Vector2(frontGround.x, frontGround.z)) - baseY;
            // Enough steps that no rise is over MAX_RISE, the one nearest the platform highest.
            float lip = floor - frontLow;
            int steps = Mathf.CeilToInt(lip / MAX_RISE) - 1;
            for (int k = 0; k < steps; k++)
            {
                float stepTop = floor - lip * (k + 1) / (steps + 1);
                Area1RemoteSites.Local($"Step_{k}", tent, timber, new Vector3(0f, (stepTop + frontLow - BURY) * 0.5f, halfDepth + 0.2f + 0.4f * k), new Vector3(1.4f, stepTop - frontLow + BURY, 0.4f));
            }

            float wallY = floor + TENT_WALL * 0.5f, depth = front - back;
            Area1RemoteSites.Local("Wall_Back", tent, canvas, new Vector3(0f, wallY, back + CANVAS * 0.5f), new Vector3(halfWidth * 2f, TENT_WALL, CANVAS));
            foreach (float side in new[] { -1f, 1f })
            {
                Area1RemoteSites.Local(side < 0f ? "Wall_West" : "Wall_East", tent, canvas, new Vector3(side * (halfWidth - CANVAS * 0.5f), wallY, (back + front) * 0.5f), new Vector3(CANVAS, TENT_WALL, depth));
                Area1RemoteSites.Local(side < 0f ? "Wall_Front_West" : "Wall_Front_East", tent, canvas, new Vector3(side * 1.3f, wallY, front - CANVAS * 0.5f), new Vector3(1.4f, TENT_WALL, CANVAS));
                float span = (halfWidth + 0.2f) / Mathf.Cos(ROOF_PITCH * Mathf.Deg2Rad);
                Area1RemoteSites.Local(side < 0f ? "Roof_West" : "Roof_East", tent, canvas, new Vector3(side * (halfWidth + 0.2f) * 0.5f, floor + TENT_WALL + 0.7f, (back + front) * 0.5f),
                    new Vector3(span, CANVAS, depth + 0.3f)).localRotation = Quaternion.Euler(0f, 0f, -side * ROOF_PITCH);
            }

            // Stepped gable ends, each tier as wide as the roof is at its top, so none pokes through the slopes.
            float slopeRise = Mathf.Tan(ROOF_PITCH * Mathf.Deg2Rad);
            float ridge = TENT_WALL + 0.7f + (halfWidth + 0.2f) * 0.5f * slopeRise;
            const float tierHeight = 0.4f;
            for (int tier = 0; tier < 3; tier++)
            {
                float top = TENT_WALL + tierHeight * (tier + 1);
                float width = 2f * (ridge - top) / slopeRise;
                foreach (float z in new[] { back + CANVAS * 0.5f, front - CANVAS * 0.5f })
                    Area1RemoteSites.Local($"Gable_{(z < 0f ? "Back" : "Front")}_{tier}", tent, canvas, new Vector3(0f, floor + top - tierHeight * 0.5f, z), new Vector3(width, tierHeight, CANVAS));
            }

            foreach (float x in new[] { -1.8f, 1.8f })
                Area1RemoteSites.Local(x < 0f ? "AwningPost_West" : "AwningPost_East", tent, timber, new Vector3(x, floor + (TENT_WALL + 0.1f) * 0.5f, halfDepth - 0.1f), new Vector3(0.1f, TENT_WALL + 0.1f, 0.1f));
            Area1RemoteSites.Thin("Awning", tent, canvas, new Vector3(0f, floor + TENT_WALL + 0.25f, (front + halfDepth) * 0.5f), new Vector3(3.8f, 0.05f, halfDepth - front + 0.3f)).localRotation = Quaternion.Euler(10f, 0f, 0f);
            return tent;
        }

        /// <summary>Stones round the embers, with <paramref name="seats"/> benches (logs) round them.</summary>
        private static void FireRing(string name, Vector2 at, int seats, Terrain terrain, Material timber, Material stone, Transform parent)
        {
            Transform fire = Area1Stoneworks.Group(name, parent, at, 0f, terrain);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3f;
                Area1RemoteSites.Local($"Stone_{i}", fire, stone, new Vector3(Mathf.Cos(angle) * 0.7f, 0.12f, Mathf.Sin(angle) * 0.7f), new Vector3(0.3f, 0.24f, 0.3f));
            }
            Area1RemoteSites.Thin("Embers", fire, timber, new Vector3(0f, 0.08f, 0f), new Vector3(0.8f, 0.12f, 0.2f)).localRotation = Quaternion.Euler(0f, 35f, 0f);

            float reach = seats > 2 ? 1.7f : 1.4f;
            for (int i = 0; i < seats; i++)
            {
                float angle = (i + 0.5f) * 360f / seats;
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0f, reach);
                Area1RemoteSites.Local($"Seat_{i}", fire, timber, new Vector3(offset.x, 0.2f, offset.z), new Vector3(1.4f, 0.4f, 0.35f)).localRotation = Quaternion.Euler(0f, angle, 0f);
            }
        }

        private static void BuildTrunk(Vector2 at, Vector2 fire, Terrain terrain, Material timber, Material stone, Transform parent)
        {
            Transform trunk = Area1Stoneworks.Group("RepairTrunk", parent, at, Yaw(fire - at), terrain);
            Area1RemoteSites.Local("Chest", trunk, timber, new Vector3(0f, 0.3f, 0f), new Vector3(1.1f, 0.6f, 0.6f));
            Area1RemoteSites.Thin("Lid", trunk, timber, new Vector3(0f, 0.85f, -0.32f), new Vector3(1.1f, 0.5f, 0.05f)).localRotation = Quaternion.Euler(-15f, 0f, 0f);
            Area1RemoteSites.Thin("Whetstone", trunk, stone, new Vector3(0.75f, 0.12f, 0.35f), new Vector3(0.35f, 0.25f, 0.2f));
            Area1RemoteSites.Thin("Tools", trunk, timber, new Vector3(-0.2f, 0.63f, 0.05f), new Vector3(0.7f, 0.05f, 0.25f));
        }

        private static void BuildCardGame(Vector2 at, Vector2 fire, Terrain terrain, Material timber, Material canvas, Transform parent)
        {
            Transform game = Area1Stoneworks.Group("CardGame", parent, at, Yaw(fire - at), terrain);
            Area1RemoteSites.Local("Crate", game, timber, new Vector3(0f, 0.35f, 0f), new Vector3(0.75f, 0.7f, 0.75f));
            Area1RemoteSites.Thin("Cards", game, canvas, new Vector3(0f, 0.71f, 0f), new Vector3(0.45f, 0.02f, 0.3f));
            Area1RemoteSites.Local("Log_West", game, timber, new Vector3(-0.95f, 0.2f, 0f), new Vector3(0.4f, 0.4f, 1f));
            Area1RemoteSites.Local("Log_East", game, timber, new Vector3(0.95f, 0.2f, 0f), new Vector3(0.4f, 0.4f, 1f));
            Area1RemoteSites.Local("Log_Back", game, timber, new Vector3(0f, 0.2f, -0.95f), new Vector3(1f, 0.4f, 0.4f));
        }

        /// <summary>A dark strip with plank crossings, without colliders and out of the bake: it marks the border, it doesn't stop anyone.</summary>
        private static void BuildDitch(Vector2[] line, float width, Terrain terrain, Material timber, Material stone, Transform root)
        {
            root.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            int piece = 0;
            for (int i = 1; i < line.Length; i++)
            {
                Vector2 a = line[i - 1], b = line[i];
                float length = Vector2.Distance(a, b);
                int count = Mathf.Max(1, Mathf.CeilToInt(length / DITCH_PIECE));
                float yaw = Yaw(b - a);
                for (int p = 0; p < count; p++)
                {
                    Vector2 at = Vector2.Lerp(a, b, (p + 0.5f) / count);
                    Transform strip = Area1RemoteSites.Thin($"Strip_{piece++:000}", root, stone, Vector3.zero, new Vector3(width, 0.03f, length / count));
                    strip.SetPositionAndRotation(new Vector3(at.x, Area1BlockoutBuilder.Ground(terrain, at) + 0.015f, at.y), Quaternion.Euler(0f, yaw, 0f));
                }
            }

            float total = 0f;
            for (int i = 1; i < line.Length; i++) total += Vector2.Distance(line[i - 1], line[i]);
            for (int c = 0; c < DitchCrossings.Length; c++)
            {
                Vector2 at = AlongLine(line, DitchCrossings[c] * total, out Vector2 along);
                Transform plank = Area1RemoteSites.Thin($"Crossing_{c}", root, timber, Vector3.zero, new Vector3(width + 0.8f, 0.06f, 1.2f));
                plank.SetPositionAndRotation(new Vector3(at.x, Area1BlockoutBuilder.Ground(terrain, at) + 0.05f, at.y), Quaternion.Euler(0f, Yaw(along), 0f));
            }
        }

        /// <summary>A pole with a cloth near the top, turned by <paramref name="clothYaw"/> so neighbouring flags read from different sides.</summary>
        private static void Pole(string name, Vector2 at, float height, float clothYaw, Terrain terrain, Material timber, Material flag, Transform parent)
        {
            Transform pole = Area1Stoneworks.Group(name, parent, at, clothYaw, terrain);
            Area1RemoteSites.Local("Pole", pole, timber, new Vector3(0f, (height - BURY) * 0.5f, 0f), new Vector3(0.18f, height + BURY, 0.18f));
            Area1RemoteSites.Thin("Cloth", pole, flag, new Vector3(0.65f, height - 1.1f, 0f), new Vector3(1.2f, 1.8f, 0.04f));
        }

        private static Transform Child(string name, Transform parent)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static Vector2 AlongLine(Vector2[] line, float distance, out Vector2 direction)
        {
            for (int i = 1; i < line.Length; i++)
            {
                float length = Vector2.Distance(line[i - 1], line[i]);
                direction = (line[i] - line[i - 1]) / length;
                if (distance <= length || i == line.Length - 1) return line[i - 1] + direction * Mathf.Min(distance, length);
                distance -= length;
            }
            direction = Vector2.up;
            return line[0];
        }

        private static Vector2 Nearest(Vector2 at, Vector2[] options) => options.OrderBy(o => Vector2.Distance(at, o)).First();

        /// <summary>Degrees whose local +Z runs along <paramref name="direction"/>.</summary>
        private static float Yaw(Vector2 direction) => Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        private static Rect Centred(Vector2 centre, Vector2 size) => new Rect(centre - size * 0.5f, size);

        private static bool Overlaps(Rect rect, Footprint f) =>
            f.At.x + f.Radius > rect.xMin && f.At.x - f.Radius < rect.xMax && f.At.y + f.Radius > rect.yMin && f.At.y - f.Radius < rect.yMax;

        private static bool IsTent(Kind kind) => kind == Kind.LeanTo || kind == Kind.Standard || kind == Kind.WallTent;

        private static bool IsPositive(JToken token) => Area1BlockoutBuilder.IsNumber(token) && (float)token > 0f;

        private static bool Fail(string message, out string problem)
        {
            problem = $"{message}; nothing changed.";
            return false;
        }
    }
}
