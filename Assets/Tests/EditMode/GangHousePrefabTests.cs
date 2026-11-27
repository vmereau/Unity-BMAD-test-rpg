using System.IO;
using Game.Editor.World;
using Game.World;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The contract the game relies on in the generated gang house prefab (Tools/World/Build Gang House Prefab) and
    /// in the wilderness gate beside it (Tools/World/Build Wilderness Gate Prefab): doorways a person passes, walk
    /// surfaces for every deck, Brann and his chest left clear, night lights on the clock, the roof under the
    /// tents-to-ruins sight line, and parts files that are refused when malformed.
    /// </summary>
    public class GangHousePrefabTests
    {
        private const string PREFAB = "Assets/_Game/Prefabs/World/GangHouse/GangHouse.prefab";
        private const string PARTS = "Assets/_Game/Art/Environment/Camp/models/gang house/GangHouse.json";
        private const string GATE_PREFAB = "Assets/_Game/Prefabs/World/DockGate/WildernessGate.prefab";
        private const string GATE_PARTS = "Assets/_Game/Art/Environment/Camp/models/wilderness gate/WildernessGate.json";
        private const float DOOR_WIDTH = 1f, DOOR_HEIGHT = 2.1f;       // STARTING_TOWN.md -> Camp centre
        private const float SIGHT_LINE = 10f;                          // STARTING_TOWN.md -> Lakeside: ridge and chimney under y 10
        private const float WATER = -2f;
        // HiddenStashQuest (hand-owned): where Brann stands by day and where his chest lies, in the world.
        private static readonly Vector3 BrannSpot = new Vector3(44.6f, 2.18f, 52.8f);
        private static readonly Vector3 Stash = new Vector3(45.57f, 2.45f, 52.98f);
        // Far from anything a loaded scene holds, so only the prefab's own colliders answer the overlap tests.
        private static readonly Vector3 Away = new Vector3(0f, 5000f, 600f);

        private GameObject _instance;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB);
            Assert.IsNotNull(prefab, $"{PREFAB} is missing: run Tools/World/Build Gang House Prefab");
            _instance = Object.Instantiate(prefab, Away, Quaternion.identity);
            _instance.hideFlags = HideFlags.DontSave; // never saved into the open scene if a run is cut short
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            if (_instance != null) Object.DestroyImmediate(_instance);
        }

        private static GangHouseBuilder.HouseFacts House()
        {
            JObject parts = JObject.Parse(File.ReadAllText(PARTS));
            Assert.IsTrue(GangHouseBuilder.TryReadHouse(parts, out GangHouseBuilder.HouseFacts house, out string problem), problem);
            return house;
        }

        /// <summary>A world point of the scene, in the prefab's frame.</summary>
        private static Vector3 Local(GangHouseBuilder.HouseFacts house, Vector3 world) => Quaternion.Inverse(house.Rotation) * (world - house.Origin);

        /// <summary>Solid colliders (everything under <c>Colliders</c>) overlapping a box of the prefab's frame.</summary>
        private string Blocking(Vector3 localCentre, Vector3 size)
        {
            Transform solids = _instance.transform.Find("Colliders");
            Assert.IsNotNull(solids);
            foreach (Collider hit in Physics.OverlapBox(Away + localCentre, size * 0.5f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                if (hit.transform.IsChildOf(solids)) return hit.transform.parent.name + "/" + hit.name;
            return null;
        }

        [TestCase("campDoor")]
        [TestCase("lakeDoor")]
        public void Doorway_LetsAPersonThrough(string anchor)
        {
            float[] door = House().Anchor(anchor);   // from x, to x, head, the wall's line (z)
            Assert.GreaterOrEqual(door[1] - door[0], DOOR_WIDTH, "width");
            Assert.GreaterOrEqual(door[2], DOOR_HEIGHT, "head");
            // the player's capsule (0.6 m wide, 1.8 m tall) through the wall's thickness
            var centre = new Vector3((door[0] + door[1]) * 0.5f, 0.95f, door[3]);
            Assert.IsNull(Blocking(centre, new Vector3(0.6f, 1.8f, 0.9f)), "something stands in the doorway");
        }

        [Test]
        public void EveryDeck_HasItsWalkBox_AsADirectChild()
        {
            foreach (string name in new[] { "Floor", "Balcony", "Deck", "Pier", "Walkway", "Ramp", "Basement", "Basement_Lake", "BasementSteps", "BasementLanding", "Tunnel_0" })
            {
                Transform walk = _instance.transform.Find(name);
                Assert.IsNotNull(walk, name);
                Assert.IsNotNull(walk.GetComponent<BoxCollider>(), name);
                Assert.IsNull(walk.GetComponent<Renderer>(), $"{name} is a walk box, not a visual");
            }
        }

        [Test]
        public void DeckAndPier_StandAboveTheWater_AndTheWalkwayReachesTheDeck()
        {
            GangHouseBuilder.HouseFacts house = House();
            float[] deck = house.Anchor("deck");         // min x, max x, min z, max z, y
            float[] pier = house.Anchor("pier");
            float[] walkway = house.Anchor("walkway");   // min x, max x, head z, foot z, head y
            Assert.Greater(house.FloorWorldY + deck[4], WATER + 1f, "the deck would be awash");
            Assert.AreEqual(deck[4], pier[4], 0.01f, "a step between the deck and the pier");
            Assert.GreaterOrEqual(walkway[0], deck[0] - 0.01f, "the walkway's foot lands beside the deck, not on it");
            Assert.LessOrEqual(walkway[3], deck[3], "the walkway stops short of the deck");
            Assert.GreaterOrEqual(walkway[1] - walkway[0], 1f, "walkway width");
        }

        [Test]
        public void BrannAndHisChest_AreLeftClear()
        {
            GangHouseBuilder.HouseFacts house = House();
            float[] room = house.Anchor("room");   // min x, max x, min z, max z, wall height
            foreach ((string what, Vector3 world, Vector3 size) in new[]
                     {
                         ("Brann's spot", BrannSpot + Vector3.up * 0.9f, new Vector3(0.7f, 1.7f, 0.7f)),
                         ("Brann's chest", Stash, new Vector3(1.0f, 0.5f, 1.0f)),
                     })
            {
                Vector3 local = Local(house, world);
                Assert.IsTrue(local.x > room[0] && local.x < room[1] && local.z > room[2] && local.z < room[3], $"{what} is outside the room");
                Assert.IsNull(Blocking(local, size), $"{what} is inside a collider");
            }
            Assert.AreEqual(0f, Local(house, BrannSpot).y, 0.1f, "Brann stands on the main floor");
        }

        [Test]
        public void RoofAndChimney_StayUnderTheSightLine()
        {
            GangHouseBuilder.HouseFacts house = House();
            Assert.Less(house.FloorWorldY + house.Anchor("ridge")[0], SIGHT_LINE, "ridge");
            Assert.Less(house.FloorWorldY + house.Anchor("chimneyTop")[1], SIGHT_LINE, "chimney");
            foreach (Renderer renderer in _instance.GetComponentsInChildren<Renderer>())
                Assert.Less(renderer.bounds.max.y - Away.y + house.FloorWorldY, SIGHT_LINE, renderer.name);
        }

        [TestCase("DoorLantern")]
        [TestCase("DeckLantern")]
        [TestCase("HearthFire")]
        public void Light_IsANightLight(string name)
        {
            Transform holder = _instance.transform.Find(name);
            Assert.IsNotNull(holder, name);
            Assert.IsNotNull(holder.GetComponent<ClockNightLight>(), $"{name} must be driven by the clock");
            Light light = holder.GetComponentInChildren<Light>(true);
            Assert.IsNotNull(light, name);
            Assert.IsFalse(light.enabled, $"{name} is on by day");
        }

        [Test]
        public void RoofsFurnitureAndBase_AreNotWalkable_FloorAndDecksAreBaked()
        {
            Transform model = _instance.transform.Find("Model");
            Assert.IsNotNull(model);
            foreach (string name in new[] { "Roof", "Shelters", "Furniture", "Hearth", "Doors", "Base", "Piles", "Nets", "BasementWalls", "TunnelTimber" })
            {
                Component modifier = model.Find(name)?.GetComponent("NavMeshModifier");
                Assert.IsNotNull(modifier, name);
                Assert.AreEqual(1, new SerializedObject(modifier).FindProperty("m_Area").intValue, $"{name} must be Not Walkable");
            }
            foreach (string name in new[] { "Floor", "Decks", "Frame", "Walls", "Basement", "Tunnel" })
                Assert.IsNull(model.Find(name)?.GetComponent("NavMeshModifier"), $"{name} must take part in the bake as it is");
        }

        [TestCase("{}")]
        [TestCase("{\"version\": 1}")]
        [TestCase("{\"version\": 2, \"objects\": [], \"walk\": [], \"colliders\": [], \"notWalkable\": [], \"lanterns\": [], \"lights\": []}")]
        [TestCase("{\"version\": 2, \"objects\": [], \"walk\": [], \"colliders\": [], \"notWalkable\": [], \"lanterns\": [], \"lights\": [], \"goods\": [], \"centre\": [1], \"yaw\": 0, \"floorWorldY\": 0, \"anchors\": {}}")]
        [TestCase("{\"version\": 2, \"objects\": [], \"walk\": [{\"name\": \"x\", \"centre\": [0, 0, 0], \"size\": [1, 1]}], \"colliders\": [], \"notWalkable\": [], \"lanterns\": [], \"lights\": [], \"goods\": [], \"centre\": [1, 2], \"yaw\": 0, \"floorWorldY\": 0, \"anchors\": {}}")]
        [TestCase("{\"version\": 2, \"objects\": [], \"walk\": [], \"colliders\": [], \"notWalkable\": [], \"lanterns\": [], \"lights\": [], \"goods\": [{\"kit\": \"x\"}], \"centre\": [1, 2], \"yaw\": 0, \"floorWorldY\": 0, \"anchors\": {}}")]
        [TestCase("{\"version\": 2, \"objects\": [], \"walk\": [], \"colliders\": [], \"notWalkable\": [], \"lanterns\": [], \"lights\": [], \"goods\": [], \"centre\": [1, 2], \"yaw\": 0, \"floorWorldY\": 0, \"anchors\": {\"campDoor\": [0, 1]}}")]
        public void MalformedParts_AreRefused_WithoutAThrow(string json)
        {
            Assert.IsFalse(GangHouseBuilder.TryReadHouse(JObject.Parse(json), out _, out string problem));
            Assert.IsNotEmpty(problem);
        }

        [Test]
        public void HiddenDoor_IsTheGamesDoor_Unlocked_InAPersonSizedOpening()
        {
            float[] opening = House().Anchor("hiddenDoor");   // min x, max x, height, the wall's outer face (z), the sill's y
            Assert.GreaterOrEqual(opening[1] - opening[0], DOOR_WIDTH, "width");
            Assert.GreaterOrEqual(opening[2], DOOR_HEIGHT, "height");
            Transform door = _instance.transform.Find("HiddenDoor");
            Assert.IsNotNull(door, "HiddenDoor");
            Assert.IsNotNull(door.GetComponent<DoorInteractable>());
            var lockable = door.GetComponent<Lockable>();
            Assert.IsNotNull(lockable);
            Assert.IsFalse(new SerializedObject(lockable).FindProperty("_isLocked").boolValue, "locking it is a later spec");
            // the wall's own boxes leave the opening free (the leaf is the door's, not the wall's)
            var centre = new Vector3((opening[0] + opening[1]) * 0.5f, opening[4] + 1.0f, opening[3] + 0.33f);
            Transform walls = _instance.transform.Find("Colliders/Base");
            Assert.IsNotNull(walls);
            foreach (Collider hit in Physics.OverlapBox(Away + centre, new Vector3(0.3f, 0.9f, 0.25f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                Assert.IsFalse(hit.transform.IsChildOf(walls), $"{hit.name} closes the hidden door's opening");
        }

        [Test]
        public void Tunnel_IsClearFromEndToEnd_AndStaysAboveTheWater()
        {
            GangHouseBuilder.HouseFacts house = House();
            Assert.GreaterOrEqual(house.TunnelWidth, 1.2f, "width between the lagging");
            Assert.GreaterOrEqual(house.TunnelHeight, DOOR_HEIGHT, "height under the caps");
            Transform solids = _instance.transform.Find("Colliders");
            for (int i = 0; i + 1 < house.TunnelNodes.Length; i++)
            {
                Vector3 a = house.TunnelNodes[i], b = house.TunnelNodes[i + 1];
                Assert.Greater(house.FloorWorldY + Mathf.Min(a.y, b.y), WATER + 0.2f, $"leg {i} would be awash");
                float length = Vector3.Distance(a, b);
                for (float along = 0.3f; along < length - 0.3f; along += 0.25f)
                {
                    // the player's capsule on the middle of the floor
                    Vector3 at = Away + Vector3.Lerp(a, b, along / length) + Vector3.up * 1.0f;
                    foreach (Collider hit in Physics.OverlapBox(at, new Vector3(0.3f, 0.85f, 0.3f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                        Assert.IsFalse(hit.transform.IsChildOf(solids), $"{hit.transform.parent.name}/{hit.name} stands in leg {i}, {along:F1} m along");
                }
            }
            Transform strip = _instance.transform.Find("NavMesh/TunnelStrip");
            Assert.IsNotNull(strip, "agents are kept out of the tunnel by a Not Walkable strip");
            Assert.AreEqual(1, new SerializedObject(strip.GetComponent("NavMeshModifierVolume")).FindProperty("m_Area").intValue);
        }

        [Test]
        public void Tunnel_StartsInTheWarehouseSideRoomsOpening()
        {
            GangHouseBuilder.HouseFacts house = House();
            JObject warehouse = JObject.Parse(File.ReadAllText("Assets/_Game/Art/Environment/Camp/models/warehouse/Warehouse.json"));
            var opening = warehouse["cellar"]?["tunnel"] as JArray;   // min x, max x, height, outer z, inner z: the warehouse's frame
            Assert.IsNotNull(opening, "Warehouse.json has no cellar.tunnel: run build_warehouse.py");
            Assert.GreaterOrEqual((float)opening[1] - (float)opening[0], 1.1f, "opening width");
            Assert.GreaterOrEqual((float)opening[2], 2.0f, "opening height");
            var fitted = (JArray)warehouse["centre"];
            Vector3 start = house.World(house.TunnelNodes[0]);
            float x = start.x - (float)fitted[0], z = start.z - (float)fitted[fitted.Count - 1];
            Assert.AreEqual(((float)opening[0] + (float)opening[1]) * 0.5f, x, 0.05f, "the tunnel misses the opening sideways");
            Assert.AreEqual((float)opening[3], z, 0.05f, "the tunnel does not start on the wall's outer face");
        }

        [Test]
        public void BasementHole_LiesInsideTheFootprint_AndOverTheRoom()
        {
            GangHouseBuilder.HouseFacts house = House();
            System.Collections.Generic.List<Vector2Int> cells = GangHouseBuilder.HoleCells(house, new Vector2(-500f, -500f), 1000f, 1024);
            Assert.Greater(cells.Count, 20, "the hole is too small to clear the room");
            float cell = 1000f / 1024f;
            float[] room = house.Anchor("basement");   // min x, max x, min z, max z, floor y, top y
            Quaternion toLocal = Quaternion.Inverse(house.Rotation);
            var covered = new System.Collections.Generic.HashSet<Vector2Int>(cells);
            foreach (Vector2Int c in cells)
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector3 local = toLocal * (new Vector3(-500f + (c.x + (corner & 1)) * cell, house.FloorWorldY, -500f + (c.y + (corner >> 1)) * cell) - house.Origin);
                    Assert.LessOrEqual(Mathf.Abs(local.x), 4.5f, "a hole cell shows outside an end wall");
                    Assert.LessOrEqual(Mathf.Abs(local.z), 3.0f, "a hole cell shows outside a long wall");
                }
            // everywhere in the room except the strip along the lake wall (lowered instead) the ground is gone
            for (float x = room[0] + 0.1f; x < room[1]; x += 0.3f)
                for (float z = -1.2f; z < room[3]; z += 0.3f)
                {
                    Vector3 world = house.World(new Vector3(x, 0f, z));
                    var at = new Vector2Int(Mathf.FloorToInt((world.x + 500f) / cell), Mathf.FloorToInt((world.z + 500f) / cell));
                    Assert.IsTrue(covered.Contains(at), $"solid ground left in the room at local ({x:F1}, {z:F1})");
                }
        }

        [TestCase("BasementLantern")]
        [TestCase("TunnelLantern_West")]
        [TestCase("TunnelLantern_Middle")]
        [TestCase("TunnelLantern_East")]
        public void UndergroundLantern_AlwaysBurns_WithShadows(string name)
        {
            Transform holder = _instance.transform.Find(name);
            Assert.IsNotNull(holder, name);
            Assert.IsNull(holder.GetComponent<ClockNightLight>(), $"{name}: no daylight down there, so no clock");
            Light light = holder.GetComponentInChildren<Light>(true);
            Assert.IsNotNull(light, name);
            Assert.IsTrue(light.enabled, name);
            Assert.AreNotEqual(LightShadows.None, light.shadows, $"{name} would show through the ground");
        }

        [Test]
        public void NullParts_AreRefused()
        {
            Assert.IsFalse(GangHouseBuilder.TryReadHouse(null, out _, out string problem));
            Assert.IsNotEmpty(problem);
        }

        [Test]
        public void WildernessGate_IsACartGateway_WithoutAFire()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GATE_PREFAB);
            Assert.IsNotNull(prefab, $"{GATE_PREFAB} is missing: run Tools/World/Build Wilderness Gate Prefab");
            JObject parts = JObject.Parse(File.ReadAllText(GATE_PARTS));
            Assert.IsTrue(DockGateBuilder.TryReadGate(parts, false, out DockGateBuilder.GateFacts gate, out string problem), problem);
            Assert.GreaterOrEqual(gate.Opening.y - gate.Opening.x, 2.5f, "between the posts");
            Assert.GreaterOrEqual(gate.Opening.z, 2.8f, "under the lintel");
            Assert.IsNull(prefab.transform.Find("BrazierFire"), "this gate has no fire basket");
            Assert.IsNull(prefab.transform.Find("Model/LeanTo"), "this gate has no lean-to");

            GameObject gateInstance = Object.Instantiate(prefab, Away + Vector3.right * 100f, Quaternion.identity);
            gateInstance.hideFlags = HideFlags.DontSave;
            try
            {
                Physics.SyncTransforms();
                // a cart-high box through the opening, from 3 m on one side to 3 m on the other (the leaves stand along +x)
                float half = (gate.Opening.y - gate.Opening.x) * 0.5f - 0.05f;
                var centre = gateInstance.transform.position + new Vector3(0f, 0.05f + 1.37f, (gate.Opening.x + gate.Opening.y) * 0.5f);
                foreach (Collider hit in Physics.OverlapBox(centre, new Vector3(3f, 1.37f, half), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                    Assert.IsFalse(hit.transform.IsChildOf(gateInstance.transform), $"{hit.name} stands in the gateway");
            }
            finally
            {
                Object.DestroyImmediate(gateInstance);
            }
        }
    }
}
