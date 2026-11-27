using System.Collections.Generic;
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
    /// The contract the game relies on in the generated warehouse prefab (Tools/World/Build Warehouse Prefab):
    /// the camp ramp's walk box the home yard's tarp is tied to, clear cart doorways and decks, night lanterns.
    /// </summary>
    public class WarehousePrefabTests
    {
        private const string PREFAB = "Assets/_Game/Prefabs/World/Warehouse/Warehouse.prefab";
        private const string RAMP_CAMP = "Ramp_Camp";
        private const float CART_WIDTH = 3.5f;
        private const float HEADROOM = 2.1f;       // the camp's doors: the player's capsule reaches 1.8 m
        private const float DOOR_X = 2f;           // both cart doorways, east of the hall's middle
        private const float DOOR_WIDTH = 2.4f;
        private const float HALF_DEPTH = 4f;
        // Far from anything a loaded scene holds, so only the prefab's own colliders answer the overlap tests.
        private static readonly Vector3 Away = new Vector3(0f, 5000f, 0f);
        private static readonly string[] Walks = { "Floor", "Landing", "Ramp_River", RAMP_CAMP, "Cellar" };
        private const string PARTS = "Assets/_Game/Art/Environment/Camp/models/warehouse/Warehouse.json";
        private const float PLAYER_RADIUS = 0.3f;

        private GameObject _instance;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB);
            Assert.IsNotNull(prefab, $"{PREFAB} is missing: run Tools/World/Build Warehouse Prefab");
            _instance = Object.Instantiate(prefab, Away, Quaternion.identity);
            _instance.hideFlags = HideFlags.DontSave; // never saved into the open scene if a run is cut short
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            if (_instance != null) Object.DestroyImmediate(_instance);
        }

        [Test]
        public void CampRamp_IsAnActiveScaledBox_DirectlyUnderTheRoot()
        {
            Transform ramp = _instance.transform.Find(RAMP_CAMP);
            Assert.IsNotNull(ramp, "HomeYardDresser ties the east yard's tarp to Warehouse/Ramp_Camp");
            Assert.IsTrue(ramp.gameObject.activeSelf);
            Assert.IsNotNull(ramp.GetComponent<BoxCollider>());
            Assert.AreEqual(Vector3.one, ramp.GetComponent<BoxCollider>().size, "its local ±0.5 must be the deck's faces");
            Assert.AreEqual(CART_WIDTH, ramp.localScale.x, 0.001f);
            Assert.AreEqual(0.2f, ramp.localScale.y, 0.001f);
            // The deck's length follows the ground at the ramp's foot: 13.2 m in plan, about 13.85 m today.
            Assert.That(ramp.localScale.z, Is.InRange(13.2f, 14.2f));
        }

        [Test]
        public void OverlapQueries_SeeThePrefabsColliders()
        {
            // The clearance tests below only prove something if a box through a wall is reported.
            var centre = Away + new Vector3(-3f, 2f, -(HALF_DEPTH - 0.2f));
            CollectionAssert.IsNotEmpty(Blockers(centre, new Vector3(0.5f, 0.5f, 0.5f), Quaternion.identity));
        }

        [Test]
        public void EveryWalkSurface_Exists()
        {
            foreach (string name in Walks)
            {
                Transform walk = _instance.transform.Find(name);
                Assert.IsNotNull(walk, name);
                Assert.IsNotNull(walk.GetComponent<BoxCollider>(), name);
            }
        }

        [Test]
        public void BothCartDoorways_AreClear()
        {
            foreach (float side in new[] { -1f, 1f })
            {
                var centre = Away + new Vector3(DOOR_X, 0.05f + HEADROOM * 0.5f, side * (HALF_DEPTH - 0.2f));
                var half = new Vector3(DOOR_WIDTH * 0.5f - 0.005f, HEADROOM * 0.5f, 0.4f);
                CollectionAssert.IsEmpty(Blockers(centre, half, Quaternion.identity), $"doorway on z {side * HALF_DEPTH}");
            }
        }

        [Test]
        public void Decks_AreClear_ACartWideAndHeadHigh()
        {
            foreach (string name in new[] { "Landing", "Ramp_River", RAMP_CAMP })
            {
                Transform walk = _instance.transform.Find(name);
                Assert.IsNotNull(walk, name);
                Vector3 size = walk.localScale;
                // From just over the deck's top up to head height, a little short of both ends.
                Vector3 centre = walk.position + walk.up * (size.y * 0.5f + 0.05f + HEADROOM * 0.5f);
                var half = new Vector3(size.x * 0.5f - 0.005f, HEADROOM * 0.5f, size.z * 0.5f - 0.3f);
                Assert.GreaterOrEqual(size.x, CART_WIDTH, name);
                CollectionAssert.IsEmpty(Blockers(centre, half, walk.rotation), name);
            }
        }

        [Test]
        public void Decks_HaveHandrails()
        {
            Transform rails = _instance.transform.Find("Colliders/Rails");
            Assert.IsNotNull(rails);
            // Camp ramp: both sides. River ramp: the bank side, and the hall side past its west end. Landing: two edges.
            Assert.GreaterOrEqual(rails.childCount, 6);
        }

        [Test]
        public void NothingPassesUnderTheHall()
        {
            // A box under each wall and one on the palisade line, from below the ground up to the floor. The river
            // wall's box and the palisade line's are each in two pieces: the cellar lies between them, closed by its
            // own walls.
            Transform rubble = _instance.transform.Find("Colliders/Rubble");
            Assert.IsNotNull(rubble);
            Assert.AreEqual(7, rubble.childCount);
            Transform west = rubble.Find("Skirt"), east = rubble.Find("Skirt_Line_East");
            Assert.IsNotNull(west, "the palisade line under the floor stays closed");
            Assert.IsNotNull(east, "the palisade line under the floor stays closed");
            // From each gable to the cellar's inside face: its side walls stand behind those faces.
            WarehouseBuilder.CellarFacts cellar = Cellar();
            var westBox = west.GetComponent<BoxCollider>();
            var eastBox = east.GetComponent<BoxCollider>();
            Assert.AreEqual(-6f, west.localPosition.x - westBox.size.x * 0.5f, 0.01f, "from the west gable");
            Assert.AreEqual(cellar.Inside.xMin, west.localPosition.x + westBox.size.x * 0.5f, 0.01f);
            Assert.AreEqual(cellar.Inside.xMax, east.localPosition.x - eastBox.size.x * 0.5f, 0.01f);
            Assert.AreEqual(6f, east.localPosition.x + eastBox.size.x * 0.5f, 0.01f, "to the east gable");
            foreach (Transform box in rubble)
            {
                var collider = box.GetComponent<BoxCollider>();
                Assert.IsNotNull(collider, box.name);
                Assert.AreEqual(0f, box.localPosition.y + collider.size.y * 0.5f, 0.01f, $"{box.name} must reach the floor");
                Assert.Greater(collider.size.y, 4f, $"{box.name} must reach below the lowest ground");
            }
        }

        [Test]
        public void DoorLanterns_AreNightLights_OffByDefault()
        {
            int lanterns = 0;
            foreach (Transform child in _instance.transform)
            {
                if (!child.name.StartsWith("DoorLantern")) continue;
                lanterns++;
                Assert.IsNotNull(child.GetComponent<ClockNightLight>(), child.name);
                Light light = child.GetComponentInChildren<Light>(true);
                Assert.IsNotNull(light, child.name);
                Assert.IsFalse(light.enabled, "ClockNightLight switches it on at dusk");
            }
            Assert.AreEqual(2, lanterns, "one lantern by each cart door");
        }

        // ------------------------------------------------------------------------------------ cellar

        private static WarehouseBuilder.CellarFacts Cellar()
        {
            JObject parts = JObject.Parse(File.ReadAllText(PARTS));
            Assert.IsTrue(WarehouseBuilder.TryReadCellar(parts, out WarehouseBuilder.CellarFacts cellar, out string problem), problem);
            return cellar;
        }

        [Test]
        public void CellarDoorway_IsADoorWideAndHeadHigh()
        {
            JObject parts = JObject.Parse(File.ReadAllText(PARTS));
            var opening = (JArray)parts["cellar"]["door"]["opening"];   // min x, max x, height
            float from = (float)opening[0], to = (float)opening[1], height = (float)opening[2];
            WarehouseBuilder.CellarFacts cellar = Cellar();
            Assert.GreaterOrEqual(to - from, 1f, "the camp's doorways are at least 1 m wide");
            Assert.GreaterOrEqual(height, HEADROOM);

            // With the leaf swung away, a player-wide box passes through the wall from the step to the room.
            Transform door = _instance.transform.Find("CellarDoor");
            Assert.IsNotNull(door, "the dock door is an instance of the game's door prefab");
            door.gameObject.SetActive(false);
            Physics.SyncTransforms();
            // From just over the threshold up to the camp's 2.1 m of headroom.
            float tall = HEADROOM * 0.5f - 0.03f;
            var centre = Away + new Vector3((from + to) * 0.5f, cellar.FloorY + 0.05f + tall, cellar.Inside.yMax + 0.15f);
            var half = new Vector3(PLAYER_RADIUS, tall, 0.6f);
            CollectionAssert.IsEmpty(Blockers(centre, half, Quaternion.identity), "the doorway");
        }

        [Test]
        public void CellarDoor_IsUnlocked_SwingsIn_AndKeepsItsSaveId()
        {
            Transform door = _instance.transform.Find("CellarDoor");
            Assert.IsNotNull(door);
            Assert.IsNotNull(door.GetComponent<DoorInteractable>());
            Assert.IsFalse(new SerializedObject(door.GetComponent<Lockable>()).FindProperty("_isLocked").boolValue, "locking it is a later spec");
            var saveable = door.GetComponent<SaveableObject>();
            Assert.IsNotNull(saveable);
            Assert.AreEqual("area1-warehouse-cellar-door", new SerializedObject(saveable).FindProperty("_saveId").stringValue);
            // The leaf hangs on the east jamb and lies west of it when shut.
            WarehouseBuilder.CellarFacts cellar = Cellar();
            Assert.Less((door.rotation * Vector3.right).x, -0.99f);
            Assert.AreEqual(cellar.DoorPosition.x, door.localPosition.x, 0.001f);
        }

        [Test]
        public void CellarFloor_IsClear_BetweenItsPillars()
        {
            WarehouseBuilder.CellarFacts cellar = Cellar();
            Transform walk = _instance.transform.Find("Cellar");
            Assert.IsNotNull(walk);
            Assert.AreEqual(cellar.FloorY, walk.localPosition.y + walk.localScale.y * 0.5f, 0.01f, "the walk box's top is the floor");
            // The walk from the door to the back: in from the door, west between the two rows of pillars (a hall
            // post stands straight ahead of the door), then south between the hall's posts and the beam's to the back wall.
            float y = cellar.FloorY + 0.1f + HEADROOM * 0.5f, tall = HEADROOM * 0.5f - 0.02f;
            var legs = new (string name, Vector3 centre, Vector3 half)[]
            {
                ("in from the door", new Vector3(3.45f, y, 5.6f), new Vector3(PLAYER_RADIUS, tall, 1.3f)),
                ("between the rows", new Vector3(2.72f, y, 4.45f), new Vector3(1.03f, tall, PLAYER_RADIUS)),
                ("to the back", new Vector3(2f, y, (cellar.Inside.yMin + 0.3f + 4.6f) * 0.5f), new Vector3(PLAYER_RADIUS, tall, (4.6f - cellar.Inside.yMin - 0.3f) * 0.5f)),
            };
            foreach ((string name, Vector3 centre, Vector3 half) in legs)
                CollectionAssert.IsEmpty(Blockers(Away + centre, half, Quaternion.identity), name);
        }

        [Test]
        public void Cellar_IsClosedOnEverySide()
        {
            WarehouseBuilder.CellarFacts cellar = Cellar();
            var middle = Away + new Vector3(cellar.Inside.center.x + 1.2f, cellar.FloorY + 1f, cellar.Inside.center.y);
            foreach (Vector3 way in new[] { Vector3.left, Vector3.right, Vector3.back })
                Assert.IsTrue(Physics.Raycast(middle, way, 8f, ~0, QueryTriggerInteraction.Ignore), $"no wall toward {way}");
            // North is the front wall: a ray west of the doorway meets it.
            var westOfDoor = Away + new Vector3(2.4f, cellar.FloorY + 1f, cellar.Inside.center.y);
            Assert.IsTrue(Physics.Raycast(westOfDoor, Vector3.forward, 8f, ~0, QueryTriggerInteraction.Ignore), "no front wall");
        }

        [Test]
        public void CellarHatch_IsSolidFloor()
        {
            JObject parts = JObject.Parse(File.ReadAllText(PARTS));
            var hatch = (JArray)parts["cellar"]["hatch"];   // min x, max x, min z, max z
            var over = Away + new Vector3(((float)hatch[0] + (float)hatch[1]) * 0.5f, 1f, ((float)hatch[2] + (float)hatch[3]) * 0.5f);
            Assert.IsTrue(Physics.Raycast(over, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore));
            Assert.AreEqual("Floor", hit.collider.name, "opening the hatch is a later spec");
            Assert.AreEqual(Away.y, hit.point.y, 0.01f);
        }

        [Test]
        public void CellarLanterns_OneNightLightOutside_TwoAlwaysOnInside()
        {
            Transform outside = _instance.transform.Find("CellarLantern_Door");
            Transform inside = _instance.transform.Find("CellarLantern_Inside");
            Assert.IsNotNull(outside);
            Assert.IsNotNull(inside);
            Assert.IsNotNull(outside.GetComponent<ClockNightLight>());
            Assert.IsFalse(outside.GetComponentInChildren<Light>(true).enabled, "ClockNightLight switches it on at dusk");
            Transform back = _instance.transform.Find("CellarLantern_Back");
            Assert.IsNotNull(back);
            WarehouseBuilder.CellarFacts cellar = Cellar();
            foreach (Transform lantern in new[] { inside, back })
            {
                Assert.IsNull(lantern.GetComponent<ClockNightLight>(), "the room has no window: its lanterns burn at all hours");
                Assert.IsTrue(lantern.GetComponentInChildren<Light>(true).enabled);
                Assert.AreNotEqual(LightShadows.None, lantern.GetComponentInChildren<Light>(true).shadows, "unshadowed, it lights the dock through the wall");
                Assert.IsTrue(cellar.Inside.Contains(new Vector2(lantern.localPosition.x, lantern.localPosition.z)), lantern.name);
            }
            Assert.Greater(outside.localPosition.z, cellar.Inside.yMax);
        }

        [Test]
        public void CellarRestrictedBox_AndHole_StayInsideTheWalls()
        {
            WarehouseBuilder.CellarFacts cellar = Cellar();
            Rect area = Rect.MinMaxRect(cellar.AreaCentre.x - cellar.AreaSize.x * 0.5f, cellar.AreaCentre.z - cellar.AreaSize.z * 0.5f,
                cellar.AreaCentre.x + cellar.AreaSize.x * 0.5f, cellar.AreaCentre.z + cellar.AreaSize.z * 0.5f);
            Assert.GreaterOrEqual(area.xMin, cellar.Inside.xMin);
            Assert.LessOrEqual(area.xMax, cellar.Inside.xMax);
            Assert.GreaterOrEqual(area.yMin, cellar.Inside.yMin);
            Assert.LessOrEqual(area.yMax, cellar.Inside.yMax);
            Assert.Less(cellar.AreaCentre.y + cellar.AreaSize.y * 0.5f, 0f, "under the hall's floor: the hall has its own area");

            // The hole's edge is jagged on the terrain's cell grid: it must fall inside a wall's collider on each
            // side, 0.5 m off the floor, and never north of the front wall's inner face.
            Assert.Less(cellar.Hole.yMax, cellar.Inside.yMax, "a hole past the front wall would open the shelf outside");
            float y = cellar.FloorY + 0.5f;
            var probes = new[]
            {
                new Vector3(cellar.Hole.xMin, y, cellar.Inside.center.y), new Vector3(cellar.Hole.xMax, y, cellar.Inside.center.y),
                new Vector3(cellar.Hole.xMin, y, cellar.Inside.yMax - 0.3f), new Vector3(cellar.Hole.xMax, y, cellar.Inside.yMax - 0.3f),
                new Vector3(cellar.Inside.center.x, y, cellar.Hole.yMin + 0.3f),
            };
            foreach (Vector3 probe in probes)
                CollectionAssert.IsNotEmpty(Blockers(Away + probe, Vector3.one * 0.02f, Quaternion.identity), $"hole edge at {probe} is not inside a wall");
        }

        [Test]
        public void HoleCells_AreTheCellsWhollyInsideTheRectangle()
        {
            // Area 1's terrain: 1000 m from (-500, -500), 1024 cells. The rectangle of the build brief.
            var world = Rect.MinMaxRect(13.6f, 49.15f, 20.6f, 58.95f);
            RectInt cells = WarehouseBuilder.HoleCells(world, new Vector2(-500f, -500f), 1000f, 1024);
            Assert.AreEqual(new RectInt(526, 563, 7, 9), cells);
            float cell = 1000f / 1024f;
            Assert.GreaterOrEqual(-500f + cells.xMin * cell, world.xMin);
            Assert.LessOrEqual(-500f + cells.xMax * cell, world.xMax);
            Assert.AreEqual(0, WarehouseBuilder.HoleCells(Rect.MinMaxRect(13.7f, 51.8f, 14.5f, 52.6f), new Vector2(-500f, -500f), 1000f, 1024).width,
                "a rectangle narrower than a cell holds none");
            Assert.AreEqual(0, WarehouseBuilder.HoleCells(world, new Vector2(-500f, -500f), 0f, 1024).width);
        }

        [TestCase("{}")]
        [TestCase("{\"cellar\": 3}")]
        [TestCase("{\"cellar\": {\"hole\": [0, 0, 1], \"inside\": [0, 0, 1, 1], \"floorY\": -4}}")]
        [TestCase("{\"cellar\": {\"hole\": [1, 0, 0, 1], \"inside\": [0, 0, 1, 1], \"floorY\": -4}}")]
        [TestCase("{\"cellar\": {\"hole\": [0, 0, 1, 1], \"inside\": [0, 0, 1, 1], \"floorY\": \"low\"}}")]
        [TestCase("{\"cellar\": {\"hole\": [0, 0, 1, 1], \"inside\": [0, 0, 1, 1], \"floorY\": -4, \"door\": {\"position\": [0, 0], \"yaw\": 0, \"scaleY\": 1, \"openAngle\": 90}}}")]
        [TestCase("{\"cellar\": {\"hole\": [0, 0, 1, 1], \"inside\": [0, 0, 1, 1], \"floorY\": -4, \"door\": {\"position\": [0, 0, 0], \"yaw\": 0, \"scaleY\": 0, \"openAngle\": 90}}}")]
        [TestCase("{\"cellar\": {\"hole\": [0, 0, 1, 1], \"inside\": [0, 0, 1, 1], \"floorY\": -4, \"door\": {\"position\": [0, 0, 0], \"yaw\": 0, \"scaleY\": 1, \"openAngle\": 90}, \"restricted\": {\"centre\": [0, 0, 0], \"size\": [1, 0, 1]}}}")]
        public void MalformedCellar_IsRefused_WithoutAThrow(string json)
        {
            Assert.IsFalse(WarehouseBuilder.TryReadCellar(JObject.Parse(json), out _, out string problem));
            Assert.IsNotEmpty(problem);
        }

        [Test]
        public void CutCellarHole_OpensOnlyTheCellarsCells()
        {
            var holes = new bool[1024, 1024];
            for (int z = 0; z < 1024; z++)
                for (int x = 0; x < 1024; x++)
                    holes[z, x] = true;
            Assert.IsTrue(WarehouseBuilder.CutCellarHole(holes, new Vector2(-500f, -500f), 1000f));
            int open = 0;
            foreach (bool solid in holes)
                if (!solid) open++;
            Assert.That(open, Is.InRange(49, 70), "about 7 x 9 cells of 0.98 m");
            Assert.IsFalse(holes[568, 529], "the middle of the room");
            Assert.IsTrue(holes[573, 529], "the dock shelf in front of the door");
            Assert.IsFalse(holes[563, 529], "under the hall, south of the palisade line");
            Assert.IsTrue(holes[561, 529], "behind the back wall");
        }

        /// <summary>Solid colliders of the prefab in a box, walk surfaces left out.</summary>
        private List<string> Blockers(Vector3 centre, Vector3 half, Quaternion rotation)
        {
            var found = new List<string>();
            foreach (Collider collider in Physics.OverlapBox(centre, half, rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!collider.transform.IsChildOf(_instance.transform)) continue;
                if (collider.transform.parent == _instance.transform && System.Array.IndexOf(Walks, collider.name) >= 0) continue;
                found.Add($"{collider.transform.parent.name}/{collider.name}");
            }
            return found;
        }
    }
}
