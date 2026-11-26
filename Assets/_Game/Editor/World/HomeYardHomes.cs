using System.Collections.Generic;
using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>
    /// The owned huts of the home yard (spec-area-1-home-yard-huts-and-yard): who lives where, and where their bed and box
    /// stand. One table, read by <see cref="HomeYardDresser"/> (interiors), <see cref="HomeYardOwners"/> (beds, boxes,
    /// restricted areas, routines) and the tests. Positions are in the hut's own frame: +Z is the doorway, the origin
    /// is the floor centre, the boards' inner faces are at ±<see cref="ROOM_HALF"/>.
    /// </summary>
    public static class HomeYardHomes
    {
        /// <summary>Inner faces of the boards (Huts/CLAUDE.md → Shell contract: boards 1.28–1.315 m from the middle).</summary>
        public const float ROOM_HALF = 1.28f;
        /// <summary>Where the wall colliders start: a bed may tuck under the boards up to here.</summary>
        public const float WALL_COLLIDER = 1.35f;
        /// <summary>Footprint of a <c>Bed_WorkerPallet</c>'s solid box (its collider: 2.0 × 0.92 m along its X).</summary>
        public static readonly Vector2 BedSize = new Vector2(2.0f, 0.92f);
        /// <summary>Footprint of an owned box (Hobb's: a 0.38 m cube collider).</summary>
        public const float BOX_SIZE = 0.38f;
        /// <summary>The first metre inside the doorway stays clear, down to here (local z).</summary>
        public const float ENTRY_DEPTH = 0.75f;

        public const string NPCS = "Assets/_Game/Data/NPCs/";
        public const string TAM = NPCS + "BeatenWorker/NPC_BeatenWorker.asset";
        public const string PIM = NPCS + "FieldHand/NPC_FieldHand.asset";
        public const string GERRIT = NPCS + "BarnWorker/NPC_BarnWorker.asset";
        public const string BREN = NPCS + "QuarryWorker/NPC_QuarryWorker.asset";

        public sealed class BedSpot
        {
            /// <summary>Name of the bed under <c>PlayersZone</c> when it is made here; null for a bed that already exists.</summary>
            public readonly string Name;
            /// <summary>Scene path of an existing bed that moves into the hut (it keeps its parent).</summary>
            public readonly string ExistingPath;
            public readonly string Owner;
            public readonly string NameTag;
            /// <summary>Centre of the bed in the hut's frame. Every bed lies along a side wall, head to the back (yaw 90).</summary>
            public readonly Vector3 Local;

            public BedSpot(string name, string existingPath, string owner, string nameTag, Vector3 local)
            {
                Name = name;
                ExistingPath = existingPath;
                Owner = owner;
                NameTag = nameTag;
                Local = local;
            }

            /// <summary>The bed stands along the +X wall: its NPC approach goes on its −Z side (the room side).</summary>
            public bool OnPlusXWall => Local.x > 0f;
        }

        public sealed class Home
        {
            public readonly string Key;
            public readonly string Hut;
            public readonly bool Mirrored;
            public readonly string[] Owners;
            public readonly BedSpot[] Beds;
            public readonly string BoxName;
            public readonly string BoxTag;
            public readonly Vector3 BoxLocal;
            public readonly string KilledFactPath;
            public readonly (string item, int count)[] Loot;

            public Home(string key, string hut, bool mirrored, string[] owners, BedSpot[] beds, string boxName, string boxTag,
                Vector3 boxLocal, string killedFactPath, (string, int)[] loot)
            {
                Key = key;
                Hut = hut;
                Mirrored = mirrored;
                Owners = owners;
                Beds = beds;
                BoxName = boxName;
                BoxTag = boxTag;
                BoxLocal = boxLocal;
                KilledFactPath = killedFactPath;
                Loot = loot;
            }

            /// <summary>The doorway's span along local x (0..1.05, or −1.05..0 on a mirrored shell).</summary>
            public float DoorMinX => Mirrored ? -WorkerHutKitBuilder.DOOR_MAX_X : WorkerHutKitBuilder.DOOR_MIN_X;
            public float DoorMaxX => Mirrored ? WorkerHutKitBuilder.DOOR_MIN_X : WorkerHutKitBuilder.DOOR_MAX_X;
            /// <summary>Save key of the box (<c>KilledFact._guid</c>).</summary>
            public string BoxSaveKey => "StartingTown_Container_" + BoxName;
        }

        public static readonly Home[] All =
        {
            new Home("Tam", "worker-hut-03", true, new[] { TAM },
                new[] { new BedSpot("TamsBed", null, TAM, "Tam's bed", new Vector3(0.89f, 0f, -0.26f)) },
                "TamsBox", "Tam's box", new Vector3(0.75f, 0f, 1.0f), NPCS + "BeatenWorker/Facts/KilledFact_TamsBox.asset",
                new[] { ("Item_Gold", 3), ("Item_Bread", 1), ("Item_HealingPlant", 2) }),
            new Home("PimGerrit", "worker-hut", false, new[] { PIM, GERRIT },
                new[]
                {
                    new BedSpot(null, "TitheBarnShortfall/BarnWorkerBed", GERRIT, "Gerrit's bed", new Vector3(-0.89f, 0f, -0.26f)),
                    new BedSpot(null, "TitheBarnShortfall/FieldHandBed", PIM, "Pim's bed", new Vector3(0.89f, 0f, -0.34f)),
                },
                "PimAndGerritsBox", "Pim and Gerrit's box", new Vector3(-0.55f, 0f, 1.02f),
                NPCS + "FieldHand/Facts/KilledFact_PimAndGerritsBox.asset",
                new[] { ("Item_Gold", 5), ("Item_Apple", 2), ("Item_Onion", 2) }),
            new Home("Bren", "worker-hut-02 (5)", true, new[] { BREN },
                new[] { new BedSpot("BrensBed", null, BREN, "Bren's bed", new Vector3(0.89f, 0f, -0.26f)) },
                "BrensBox", "Bren's box", new Vector3(0.75f, 0f, 1.0f), NPCS + "QuarryWorker/Facts/KilledFact_BrensBox.asset",
                new[] { ("Item_Gold", 6), ("Item_SaltPork", 1), ("Item_Torch", 1) }),
        };

        /// <summary>A routine block as the owners command writes it (spot / bed named, resolved in the scene).</summary>
        public readonly struct Block
        {
            public readonly string Label;
            public readonly int Start, End;
            public readonly Game.AI.NpcActivity Activity;
            /// <summary>Scene path of the stand spot; null keeps the block's current spot.</summary>
            public readonly string Spot;
            /// <summary>Bed name under <c>PlayersZone</c> for a Sleep block.</summary>
            public readonly string Bed;

            public Block(string label, int start, int end, Game.AI.NpcActivity activity, string spot = null, string bed = null)
            {
                Label = label;
                Start = start;
                End = end;
                Activity = activity;
                Spot = spot;
                Bed = bed;
            }
        }

        /// <summary>Tam keeps the fire by day and sleeps 22–06; Bren works the quarry, sits at the Wretches' fire, sleeps 23–06.</summary>
        public static readonly (string npcPath, Block[] blocks)[] Routines =
        {
            ("BeatenWorker", new[]
            {
                new Block("Fire", 6, 22, Game.AI.NpcActivity.Stand, "NpcRoutineSpots/Spot_BeatenWorker_Campfire"),
                new Block("Sleep", 22, 6, Game.AI.NpcActivity.Sleep, bed: "TamsBed"),
            }),
            ("WorkSiteManagers/QuarryWorker", new[]
            {
                new Block("Work", 6, 21, Game.AI.NpcActivity.Stand, "WorkSiteManagers/QuarryWorker_Spot"),
                new Block("Fire", 21, 23, Game.AI.NpcActivity.Stand, "NpcRoutineSpots/Spot_QuarryWorker_Night"),
                new Block("Sleep", 23, 6, Game.AI.NpcActivity.Sleep, bed: "BrensBed"),
            }),
        };

        /// <summary>The bed's footprint in the hut's frame (it lies along z): min and max corners.</summary>
        public static Rect BedRect(BedSpot bed) =>
            Rect.MinMaxRect(bed.Local.x - BedSize.y / 2f, bed.Local.z - BedSize.x / 2f, bed.Local.x + BedSize.y / 2f, bed.Local.z + BedSize.x / 2f);

        public static Rect BoxRect(Home home) =>
            new Rect(home.BoxLocal.x - BOX_SIZE / 2f, home.BoxLocal.z - BOX_SIZE / 2f, BOX_SIZE, BOX_SIZE);

        /// <summary>The entry pocket inside the doorway, which nothing solid may fill.</summary>
        public static Rect EntryRect(Home home) =>
            Rect.MinMaxRect(home.DoorMinX, ENTRY_DEPTH, home.DoorMaxX, ROOM_HALF);

        /// <summary>
        /// Checks one home: beds and box inside the room, the entry pocket clear, nothing overlapping. Null when fine,
        /// else what is wrong. Pure: the tests and both tools call it before changing anything.
        /// </summary>
        public static string Problem(Home home)
        {
            if (home == null) return "no home";
            if (string.IsNullOrEmpty(home.Hut) || home.Owners == null || home.Owners.Length == 0) return $"{home.Key}: no hut or no owner";
            if (home.Beds == null || home.Beds.Length != home.Owners.Length) return $"{home.Key}: one bed per owner";
            var solids = new List<(string, Rect)>();
            foreach (BedSpot bed in home.Beds)
            {
                if (bed == null || (bed.Name == null) == (bed.ExistingPath == null)) return $"{home.Key}: a bed needs a new name or an existing path";
                solids.Add((bed.NameTag, BedRect(bed)));
            }
            solids.Add((home.BoxTag, BoxRect(home)));
            Rect entry = EntryRect(home);
            for (int i = 0; i < solids.Count; i++)
            {
                (string name, Rect r) = solids[i];
                if (r.xMin < -WALL_COLLIDER || r.xMax > WALL_COLLIDER || r.yMin < -WALL_COLLIDER || r.yMax > WALL_COLLIDER)
                    return $"{home.Key}: {name} reaches past the walls";
                if (r.Overlaps(entry)) return $"{home.Key}: {name} stands in the doorway's entry";
                for (int j = i + 1; j < solids.Count; j++)
                    if (r.Overlaps(solids[j].Item2)) return $"{home.Key}: {name} overlaps {solids[j].Item1}";
            }
            return null;
        }

        /// <summary>Every hour of the day falls in one of the routine's blocks.</summary>
        public static bool CoversTheDay(Block[] blocks)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                bool found = false;
                foreach (Block b in blocks)
                    if (Game.Core.GameTime.IsHourInWindow(b.Start, b.End, hour)) { found = true; break; }
                if (!found) return false;
            }
            return true;
        }
    }
}
