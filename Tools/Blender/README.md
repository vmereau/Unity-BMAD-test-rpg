# Tools/Blender

Blender-side tools for editing animation clips. Outside `Assets/`, so Unity never imports them. Driven from Claude Code
through the Blender MCP (`mcp__blender__execute_blender_code`) or from the Blender command line. Recipe and gotchas:
`Assets/_Game/Art/Characters/Humanoids/Animations/Combat/CLAUDE.md` → *Blender edit recipe*.

| File | What |
|------|------|
| `twohand_blade_line.py` | Re-aims the blade line (left → right hand) of a Mixamo 2H clip by moving the left arm with IK inside an edit window, bakes it, exports an armature-only FBX |
| `configs/2hand_attack_1.json` | Final inputs that produced `two handed/2hand attack refined.fbx` (Attack_1) — constant `target_pitch_deg` |
| `configs/2hand_attack_2.json` | Final inputs that produced `two handed/2hand attack 2 refined.fbx` (Attack_2 finisher) — `pitch_curve` |

**Pitch target.** `target_pitch_deg` is one constant and only ever pulls the blade down (Attack_1 path). `pitch_curve`
(`[[normalized t, Unity pitch deg], ...]`, sorted, piecewise linear) replaces it when present and can raise or lower the
blade per frame; yaw is always kept and the result is clamped to ±89°. Outside its first / last point the curve holds the
end value for the whole `window`, so the window's fade-in / fade-out blends toward that held value. The curve values are what you feed the script, not what you get: the hand-bone proxy
misses large pitches by up to ~10°, so after a pass set each point to `point + (wanted − measured)` (Attack_2: points on
every frame of the edit, 3 passes). Ramp a big change over several frames — Attack_2 spreads a 75° raise over 0.21–0.42
to keep the tip speed within the original's + 8 m/s.

## Running

Blender MCP (Blender open, add-on connected):

```python
exec(open(r"D:/github/perso/Unity-BMAD-test-rpg/Tools/Blender/twohand_blade_line.py").read())
result = run(load_config(r"D:/github/perso/Unity-BMAD-test-rpg/Tools/Blender/configs/2hand_attack_1.json"))
```

Command line: `blender -b -P Tools/Blender/twohand_blade_line.py -- Tools/Blender/configs/2hand_attack_1.json`

The script clears the current Blender scene (objects, armatures, actions) before importing.

Verified 2026-11-03: running it with `configs/2hand_attack_1.json` reproduces the committed `2hand attack refined.fbx`
exactly (0° on every bone, every frame). When comparing in Blender, note that **re-importing a Blender-exported FBX shifts
its keys by +1 frame** (2–57 instead of 1–56); compare source frame f with export frame f + 1. Unity reads the take as
1/30 s – 1.867 s, and the clip range 1–56 lines up with the source frames 0–55.

## The measure → edit loop (new clip)

1. In Unity, sample the **source** clip with the snippet below → paste the values into `blade_pitch_unity`
   (one per source frame). Start with no `distance_scale`.
2. Run the script, re-import the output in Unity (first time: copy the source root-bake flags, avatar setup, compression and
   events onto the new `.meta`, keep its own take `Scene` and frame range).
3. Sample source and output: per frame, `distance_scale[f] *= sourceDist / outputDist` (key = Blender frame = source frame + 1),
   and nudge `target_pitch_deg` / `pitch_curve` if the cut lands off target. Repeat until the hand distance is within
   ~0.02 m (Attack_1 and Attack_2 each took 3 passes).
4. For a full check (yaw, tip, speeds vs the original, blade clearances to spine / head / legs, body bones), extend the
   snippet; exclude the left arm **and the left fingers** from the "body unchanged" check (they follow the hand).

```csharp
// Unity MCP execute_code: per-frame blade pitch and hand distance of a clip on the Player rig.
string path = "Assets/_Game/Art/Characters/Humanoids/Animations/Combat/two handed/2hand attack.fbx";
var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player/Player.prefab"));
go.hideFlags = HideFlags.HideAndDontSave; go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
var sb = new System.Text.StringBuilder();
try {
  var anim = go.GetComponentInChildren<Animator>(); anim.fireEvents = false; // clip events would hit uninitialised receivers
  var so = new SerializedObject(go.GetComponentInChildren<Game.Inventory.EquipmentVisuals>());
  var rs = (Transform)so.FindProperty("_weaponSocket").objectReferenceValue;
  var ls = (Transform)so.FindProperty("_leftWeaponSocket").objectReferenceValue;
  AnimationClip clip = null; foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is AnimationClip c && !c.name.StartsWith("__preview")) clip = c;
  var graph = UnityEngine.Playables.PlayableGraph.Create("s");
  var output = UnityEngine.Animations.AnimationPlayableOutput.Create(graph, "o", anim);
  var p = UnityEngine.Animations.AnimationClipPlayable.Create(graph, clip); p.SetApplyFootIK(false);
  UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(output, p);
  int frames = Mathf.RoundToInt(clip.length * clip.frameRate);
  for (int f = 0; f <= frames; f++) {
    UnityEngine.Playables.PlayableExtensions.SetTime(p, (double)(f / (float)frames * clip.length)); graph.Evaluate(0);
    Vector3 line = go.transform.InverseTransformDirection(rs.position - ls.position);
    sb.Append(System.FormattableString.Invariant($"{Mathf.Asin(line.normalized.y) * Mathf.Rad2Deg:F1}/{line.magnitude:F3},"));
  }
  graph.Destroy();
} finally { UnityEngine.Object.DestroyImmediate(go); }
return sb.ToString(); // "pitch/distance," per frame
```

Instantiating `Player.prefab` in edit mode logs one harmless NRE (`EquipmentVisuals.OnEnable` → `PlayerAnimationDriver`).
