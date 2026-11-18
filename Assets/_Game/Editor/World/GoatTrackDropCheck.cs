using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Player;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>
    /// Proves the goat track's drop into Ruins 2 is one-way by driving a temporary CharacterController the size of the player:
    /// from a grid of starts in and around the landing pit it runs in 12 directions, then turns towards the ledge, jumping
    /// whenever grounded, and fails if any run gets back past the drop's top. It also walks off the ledge and expects to land in
    /// the pit. A heightmap model can't prove this: a CharacterController counts terrain contacts as ground at any steepness, so
    /// it jump-climbs even a near-vertical terrain face; only the vertical faces of the DropBlockers boxes stop it.
    /// </summary>
    public static class GoatTrackDropCheck
    {
        private const string TAG = "[GoatTrackDropCheck]";
        private const string PLAYER_PREFAB = "Assets/_Game/Prefabs/Player/Player.prefab";
        private const string PLAYER_CONFIG = "Assets/_Game/Data/Config/PlayerConfig.asset";
        private const float GRAVITY = -9.81f;          // PlayerController.GRAVITY
        private const float SPEED = 4f;                // a little over walkSpeed (3): jumps carry further
        private const float LEG_SECONDS = 8f;
        private const float STEP = 1f / 60f;
        private const float GRID = 2f;                 // metres between starts
        private const float START_REACH = 12f;         // starts from 1 m past the top, this far along the drop and to each side
        private const float LANDING_BELOW_TOP = 5f;    // a walk-off must end at least this far below the ledge
        private const float NEAR_LEDGE = 3f;           // metres past the top that still count as the ledge (the Lip's top)
        private const float LEDGE_HALF_WIDTH = 4.5f;   // to each side of the drop line: the ring gap / Lip (half 4.4 m), not the pit
        private const int DIRECTIONS = 12;

        [MenuItem("Tools/World/Check Goat Track Drop")]
        public static void RunFromMenu() => Run();

        /// <summary>Runs the check on the open scene. True when the drop is one-way; logs the verdict either way.</summary>
        public static bool Run()
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.name != Area1FortRamp.TERRAIN_NAME)
            {
                GameLog.Error(TAG, $"Need {Area1FortRamp.TERRAIN_NAME} as the active terrain.");
                return false;
            }
            if (!Area1BlockoutBuilder.TryReadLayout(out Dictionary<string, JObject> features, out string problem)
                || !features.TryGetValue(Area1FortRamp.GOAT_TRACK_FEATURE, out JObject track) || !Area1BlockoutBuilder.HasPoints(track))
            {
                GameLog.Error(TAG, problem ?? $"Layout has no usable '{Area1FortRamp.GOAT_TRACK_FEATURE}'.");
                return false;
            }
            Vector2[] line = Area1BlockoutBuilder.Points(track);
            int drop = Area1FortRamp.Drops(track).Where(d => d > 0 && d < line.Length).DefaultIfEmpty(-1).Min();
            if (drop < 0)
            {
                GameLog.Error(TAG, $"'{Area1FortRamp.GOAT_TRACK_FEATURE}' has no drop: the player can walk back out.");
                return false;
            }
            var playerController = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB)?.GetComponentInChildren<CharacterController>(true);
            var config = AssetDatabase.LoadAssetAtPath<PlayerConfigSO>(PLAYER_CONFIG);
            if (playerController == null || config == null)
            {
                GameLog.Error(TAG, $"Need the player's CharacterController ({PLAYER_PREFAB}) and {PLAYER_CONFIG}.");
                return false;
            }

            Vector2 top = line[drop - 1], foot = line[drop];
            Vector2 forward = (foot - top).normalized, side = new Vector2(-forward.y, forward.x);
            float Ground(Vector2 p) => terrain.SampleHeight(new Vector3(p.x, 0f, p.y)) + terrain.transform.position.y;
            float topHeight = Ground(top);

            var directions = new List<Vector3>();
            for (int i = 0; i < DIRECTIONS; i++) directions.Add(Quaternion.Euler(0f, i * 360f / DIRECTIONS, 0f) * Vector3.forward);
            Vector3 home = new Vector3(-forward.x, 0f, -forward.y);
            var turns = new[] { home, Quaternion.Euler(0f, 45f, 0f) * home, Quaternion.Euler(0f, -45f, 0f) * home };

            var host = new GameObject("GoatTrackDropCheck_Controller") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var cc = host.AddComponent<CharacterController>();
                cc.height = playerController.height;
                cc.radius = playerController.radius;
                cc.center = playerController.center;
                cc.slopeLimit = playerController.slopeLimit;
                cc.stepOffset = playerController.stepOffset;
                cc.skinWidth = playerController.skinWidth;

                int runs = 0;
                for (float along = 1f; along <= START_REACH; along += GRID)
                {
                    for (float across = -START_REACH; across <= START_REACH; across += GRID)
                    {
                        Vector2 start = top + forward * along + side * across;
                        float y = Ground(start);
                        if (y > topHeight - LANDING_BELOW_TOP) continue; // only starts down in the pit or on its banks
                        if (InsideCollider(cc, new Vector3(start.x, y + 0.05f, start.y))) continue; // in a wall: depenetration isn't a climb
                        foreach (Vector3 first in directions)
                        {
                            foreach (Vector3 then in turns)
                            {
                                runs++;
                                Vector2 end = Drive(cc, new Vector3(start.x, y + 0.05f, start.y), first, then, config.jumpForce, top, forward, topHeight - LANDING_BELOW_TOP, out bool escaped, out _);
                                if (!escaped) continue;
                                GameLog.Warn(TAG, $"Goat track drop is NOT one-way: from ({start.x:F1}, {start.y:F1}) heading {first:F1} then {then:F1} the player gets back to ({end.x:F1}, {end.y:F1}). Check SealedPassage/GoatTrack/DropBlockers.");
                                return false;
                            }
                        }
                    }
                }

                Vector2 approach = top - forward * 2f;
                Vector3 landed = Walk(cc, new Vector3(approach.x, Ground(approach) + 0.05f, approach.y), new Vector3(forward.x, 0f, forward.y));
                if (landed.y > topHeight - LANDING_BELOW_TOP)
                {
                    GameLog.Warn(TAG, $"Walking off the ledge stops at y {landed.y:F1} (ledge {topHeight:F1}): the drop is blocked.");
                    return false;
                }
                GameLog.Info(TAG, $"Goat track drop is one-way: {runs} jump-climb runs from the pit never got back past the ledge; walking off it lands at y {landed.y:F1} (ledge {topHeight:F1}).");
                return true;
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// Drives <paramref name="cc"/> from <paramref name="start"/> along <paramref name="first"/> then <paramref name="then"/>,
        /// jumping at <paramref name="jumpSpeed"/> whenever grounded. Returns the XZ point where it stopped (or escaped);
        /// <paramref name="peak"/> is the highest it got.
        /// </summary>
        public static Vector2 Drive(CharacterController cc, Vector3 start, Vector3 first, Vector3 then, float jumpSpeed,
            Vector2 top, Vector2 forward, float ledgeHeight, out bool escaped, out float peak)
        {
            cc.transform.position = start;
            Physics.SyncTransforms();
            float vy = 0f;
            peak = start.y;
            int steps = Mathf.RoundToInt(LEG_SECONDS / STEP);
            for (int i = 0; i < steps * 2; i++)
            {
                vy = cc.isGrounded ? jumpSpeed : vy + GRAVITY * STEP;
                cc.Move(((i < steps ? first : then) * SPEED + Vector3.up * vy) * STEP);
                Vector3 p = cc.transform.position;
                peak = Mathf.Max(peak, p.y);
                if (DropCheckRules.IsBackOnLedge(new Vector2(p.x, p.z), p.y, top, forward, NEAR_LEDGE, LEDGE_HALF_WIDTH, ledgeHeight))
                {
                    escaped = true;
                    return new Vector2(p.x, p.z);
                }
            }
            escaped = false;
            Vector3 end = cc.transform.position;
            return new Vector2(end.x, end.z);
        }

        // True when a capsule the size of cc at start overlaps a solid collider other than the terrain.
        private static bool InsideCollider(CharacterController cc, Vector3 start)
        {
            float half = Mathf.Max(cc.height * 0.5f - cc.radius, 0f);
            Vector3 centre = start + cc.center;
            foreach (Collider hit in Physics.OverlapCapsule(centre + Vector3.up * half, centre - Vector3.up * half + Vector3.up * 0.1f, cc.radius, ~0, QueryTriggerInteraction.Ignore))
                if (hit != cc && !(hit is TerrainCollider)) return true;
            return false;
        }

        /// <summary>Walks <paramref name="cc"/> from <paramref name="start"/> along <paramref name="direction"/> without jumping.</summary>
        internal static Vector3 Walk(CharacterController cc, Vector3 start, Vector3 direction)
        {
            cc.transform.position = start;
            Physics.SyncTransforms();
            float vy = 0f;
            for (int i = 0; i < Mathf.RoundToInt(LEG_SECONDS / STEP); i++)
            {
                vy = cc.isGrounded ? -2f : vy + GRAVITY * STEP; // PlayerController.GROUNDED_VELOCITY
                cc.Move((direction * SPEED + Vector3.up * vy) * STEP);
            }
            return cc.transform.position;
        }
    }

    /// <summary>Pure rules behind <see cref="GoatTrackDropCheck"/>.</summary>
    public static class DropCheckRules
    {
        /// <summary>
        /// True when a climber at <paramref name="point"/> (XZ), height <paramref name="height"/>, is back on the ledge side of a
        /// drop: at or behind its top point <paramref name="top"/> along the drop direction <paramref name="forward"/> (normalised,
        /// top → foot), or within <paramref name="nearLedge"/> metres past it, <paramref name="halfWidth"/> to either side of the
        /// drop line and at least <paramref name="ledgeHeight"/> high (standing over the ledge, e.g. on the Lip).
        /// </summary>
        public static bool IsBackOnLedge(Vector2 point, float height, Vector2 top, Vector2 forward, float nearLedge, float halfWidth,
            float ledgeHeight)
        {
            Vector2 offset = point - top;
            float along = Vector2.Dot(offset, forward);
            float across = Mathf.Abs(offset.x * forward.y - offset.y * forward.x);
            return along <= 0f || (along <= nearLedge && across <= halfWidth && height >= ledgeHeight);
        }
    }
}
