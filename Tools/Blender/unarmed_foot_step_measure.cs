// Unity MCP execute_code body: per-frame foot positions of an unarmed attack clip on the Player rig, player-local metres.
// Paste into execute_code after setting `path` (and `outFile`). The JSON is used by Tools/Blender/unarmed_foot_step.py:
//   source clip (Unarmed/old/<clip>.fbx)  -> configs/unarmed/<clip>_source.json (`unity_source_file` of the config)
//   edited clip (Unarmed/<clip>.fbx)      -> the `measured_path` of update_correction() (one correction pass)
// Keys: L / R = LeftToes / RightToes (the contact point), LA / RA = LeftFoot / RightFoot (ankle). Index = Unity clip
// frame (t = i / 30), so the Hook's index 0 is source frame 10.
string path = "Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Unarmed/old/lead jab.fbx";
string outFile = null; // optional: write the JSON there and return only the path
var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player/Player.prefab"));
go.hideFlags = HideFlags.HideAndDontSave; go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
var keys = new[] { "L", "R", "LA", "RA" };
var lists = new System.Collections.Generic.List<string>[4];
for (int i = 0; i < 4; i++) lists[i] = new System.Collections.Generic.List<string>();
try {
  var anim = go.GetComponentInChildren<Animator>(); anim.fireEvents = false; // clip events would hit uninitialised receivers
  var bones = new[] { anim.GetBoneTransform(HumanBodyBones.LeftToes), anim.GetBoneTransform(HumanBodyBones.RightToes),
                      anim.GetBoneTransform(HumanBodyBones.LeftFoot), anim.GetBoneTransform(HumanBodyBones.RightFoot) };
  if (System.Array.IndexOf(bones, null) >= 0) throw new System.Exception("Player avatar has no toe / foot bones mapped");
  AnimationClip clip = null; foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is AnimationClip c && !c.name.StartsWith("__preview")) clip = c;
  if (clip == null) throw new System.Exception("no animation clip in " + path);
  var graph = UnityEngine.Playables.PlayableGraph.Create("s");
  var output = UnityEngine.Animations.AnimationPlayableOutput.Create(graph, "o", anim);
  var p = UnityEngine.Animations.AnimationClipPlayable.Create(graph, clip); p.SetApplyFootIK(false);
  UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(output, p);
  try {
  int frames = Mathf.RoundToInt(clip.length * 30f); // the unarmed clips are 30 fps
  for (int f = 0; f <= frames; f++) {
    UnityEngine.Playables.PlayableExtensions.SetTime(p, f / 30.0); graph.Evaluate(0);
    for (int b = 0; b < 4; b++) {
      Vector3 v = go.transform.InverseTransformPoint(bones[b].position);
      lists[b].Add(System.FormattableString.Invariant($"[{v.x:F4},{v.y:F4},{v.z:F4}]"));
    }
  }
  } finally { graph.Destroy(); }
} finally { UnityEngine.Object.DestroyImmediate(go); }
var sb = new System.Text.StringBuilder("{");
for (int b = 0; b < 4; b++) sb.Append($"\"{keys[b]}\":[{string.Join(",", lists[b])}]{(b < 3 ? "," : "")}");
sb.Append("}");
if (outFile != null) { System.IO.File.WriteAllText(outFile, sb.ToString()); return outFile; }
return sb.ToString();
