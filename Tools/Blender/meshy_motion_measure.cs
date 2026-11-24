// Unity MCP execute_code body: per-frame root travel and root-relative pose of clips read by ActionRootMotion, on the
// Player rig (metres, player axes: x right, z forward). Used to fit Tools/Blender/meshy_motion_fit.py configs:
//   - travel / sideways drift of a clip (root column);
//   - start_match / end_match offsets: (previous clip's foot at its combo window) - (this clip's foot at frame 0),
//     converted to fitted space: Blender x = -Unity x, Blender y = -Unity z, Blender z = Unity y.
// Set `files` and `outFile`. Columns: t | root x z | hips x y z yaw | LFoot | RFoot | LToes | RToes | LHand | RHand.
string dir = "Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Unarmed/";
string[] files = { "lead jab step.fbx", "hook step.fbx", "uppercut step.fbx" };
string outFile = null; // optional: write the table there and return only its size
var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player/Player.prefab"));
go.hideFlags = HideFlags.HideAndDontSave;
var sb = new System.Text.StringBuilder();
try {
  // Without the OnAnimatorMove owner the Animator applies the clip's root motion to the transform itself.
  var arm = go.GetComponent<Game.Player.ActionRootMotion>(); if (arm != null) UnityEngine.Object.DestroyImmediate(arm);
  var cc = go.GetComponent<CharacterController>(); if (cc != null) cc.enabled = false;
  var anim = go.GetComponentInChildren<Animator>(); anim.fireEvents = false; anim.applyRootMotion = true;
  var hb = new[] { HumanBodyBones.Hips, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes,
                   HumanBodyBones.RightToes, HumanBodyBones.LeftHand, HumanBodyBones.RightHand };
  foreach (var file in files) {
    AnimationClip clip = null;
    foreach (var o in AssetDatabase.LoadAllAssetsAtPath(dir + file)) { var c = o as AnimationClip; if (c != null && !c.name.StartsWith("__preview")) clip = c; }
    if (clip == null) { sb.AppendLine("## " + file + " NO CLIP"); continue; }
    sb.AppendLine(System.FormattableString.Invariant($"## {file} len {clip.length:F3}"));
    go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
    var graph = UnityEngine.Playables.PlayableGraph.Create("s");
    try {
      var output = UnityEngine.Animations.AnimationPlayableOutput.Create(graph, "o", anim);
      var p = UnityEngine.Animations.AnimationClipPlayable.Create(graph, clip); p.SetApplyFootIK(false);
      UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(output, p);
      anim.feetPivotActive = 0f;
      int frames = Mathf.RoundToInt(clip.length * 30f);
      UnityEngine.Playables.PlayableExtensions.SetTime(p, 0.0); graph.Evaluate(0f);
      go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
      for (int f = 0; f <= frames; f++) {
        if (f > 0) graph.Evaluate(1f / 30f);
        Vector3 r = go.transform.position;
        sb.Append(System.FormattableString.Invariant($"{f / 30f:F2} | {r.x:F3} {r.z:F3}"));
        foreach (var b in hb) {
          var t = anim.GetBoneTransform(b); Vector3 v = go.transform.InverseTransformPoint(t.position);
          sb.Append(System.FormattableString.Invariant($" | {v.x:F2} {v.y:F2} {v.z:F2}"));
          if (b == HumanBodyBones.Hips) { Vector3 fw = go.transform.InverseTransformDirection(t.forward); sb.Append(System.FormattableString.Invariant($" {Mathf.Atan2(fw.x, fw.z) * Mathf.Rad2Deg:F0}")); }
        }
        sb.AppendLine();
      }
    } finally { graph.Destroy(); }
  }
} finally { UnityEngine.Object.DestroyImmediate(go); }
if (outFile != null) { System.IO.File.WriteAllText(outFile, sb.ToString()); return sb.Length + " chars -> " + outFile; }
return sb.ToString();
