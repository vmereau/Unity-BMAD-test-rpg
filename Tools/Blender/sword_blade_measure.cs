// Unity MCP execute_code body (edit mode): per-frame blade and off-hand sampling of one-handed weapon clips on the
// Player rig (metres, player axes: x right, y up, z forward, relative to the moving root). The blade is rebuilt from
// the right-hand WeaponSocket and the archetype's drawnPose (blade = +Y, cutting edge = +Z of the Drawn child), so no
// weapon has to be equipped. Used to fit the `blade_aim` / `hand_shift` blocks of Tools/Blender/meshy_motion_fit.py
// configs and to time the hit events (spec-sword-three-hit-combo).
// Set `dir`, `files`, `stride`; `shieldPath` (a ShieldSO, or null) adds the shield as EquipmentVisuals holds it in
// the left hand and its distance to the blade. Columns:
//   t | root x z | hips x y z yaw | LFoot | RFoot | LHand | RHand | blade dir | edge dir | tip | LHand-RHand m | LHand-blade m
//   [| shield-blade m (closest point of the shield's oriented renderer boxes to 10 points along the blade, 10 cm
//      apart: the true minimum can be up to 5 cm lower) | shield centre]
string dir = "Assets/_Game/Art/Characters/Humanoids/Animations/Combat/one handed/";
string[] files = { "sword cut step.fbx", "sword backhand step.fbx", "sword thrust step.fbx" };
int stride = 1;            // frames (30 fps) between rows
float bladeLength = 0.9f;  // grip to tip, metres
string shieldPath = null;  // e.g. "Assets/_Game/Data/Items/Shields/Shield_KiteWoodShield.asset"
var arch = AssetDatabase.LoadAssetAtPath<Game.Inventory.WeaponArchetypeSO>("Assets/_Game/Data/Items/Weapons/Archetypes/Archetype_OneHandedSword.asset");
var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player/Player.prefab"));
go.hideFlags = HideFlags.HideAndDontSave;
var sb = new System.Text.StringBuilder();
try {
  // Without the OnAnimatorMove owner the Animator applies the clip's root motion to the transform itself.
  var arm = go.GetComponent<Game.Player.ActionRootMotion>(); if (arm != null) UnityEngine.Object.DestroyImmediate(arm);
  var cc = go.GetComponent<CharacterController>(); if (cc != null) cc.enabled = false;
  var anim = go.GetComponentInChildren<Animator>(); anim.fireEvents = false; anim.applyRootMotion = true;
  Transform socket = null; foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (t.name == "WeaponSocket") socket = t;
  if (socket == null || arch == null) return "WeaponSocket or archetype missing";
  Quaternion dq = Quaternion.Euler(arch.drawnPose.localEulerAngles); Vector3 dp = arch.drawnPose.localPosition;
  // The shield visual, placed like EquipmentVisuals.PlaceShield: under LeftWeaponSocket, its Drawn child on heldPose.
  var shieldRenderers = new System.Collections.Generic.List<Renderer>();
  var shield = shieldPath != null ? AssetDatabase.LoadAssetAtPath<Game.Inventory.ShieldSO>(shieldPath) : null;
  if (shield != null && shield.equipVisualPrefab != null) {
    Transform leftSocket = null; foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (t.name == "LeftWeaponSocket") leftSocket = t;
    if (leftSocket == null) return "LeftWeaponSocket missing"; // Instantiate under null would leave the shield in the open scene
    var visual = (GameObject)UnityEngine.Object.Instantiate(shield.equipVisualPrefab, leftSocket, false);
    visual.transform.localPosition = Vector3.zero; visual.transform.localRotation = Quaternion.identity;
    foreach (Transform child in visual.transform) {
      child.gameObject.SetActive(child.name == "Drawn");
      if (child.name == "Drawn") { child.localPosition = shield.heldPose.localPosition; child.localRotation = Quaternion.Euler(shield.heldPose.localEulerAngles); }
    }
    shieldRenderers.AddRange(visual.GetComponentsInChildren<Renderer>(false));
  }
  var hb = new[] { HumanBodyBones.Hips, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftHand, HumanBodyBones.RightHand };
  foreach (var file in files) {
    foreach (var o in AssetDatabase.LoadAllAssetsAtPath(dir + file)) {
      var clip = o as AnimationClip; if (clip == null || clip.name.StartsWith("__preview")) continue;
      sb.AppendLine(System.FormattableString.Invariant($"## {file} / {clip.name} len {clip.length:F3}"));
      foreach (var e in AnimationUtility.GetAnimationEvents(clip)) sb.AppendLine(System.FormattableString.Invariant($"  event {e.functionName} {e.time:F3}s"));
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
        for (int f = 0; f <= frames; f += stride) {
          if (f > 0) graph.Evaluate(stride / 30f);
          Vector3 r = go.transform.position;
          sb.Append(System.FormattableString.Invariant($"{f / 30f:F2} | {r.x:F3} {r.z:F3}"));
          foreach (var b in hb) {
            var t = anim.GetBoneTransform(b); Vector3 v = go.transform.InverseTransformPoint(t.position);
            sb.Append(System.FormattableString.Invariant($" | {v.x:F2} {v.y:F2} {v.z:F2}"));
            if (b == HumanBodyBones.Hips) { Vector3 fw = go.transform.InverseTransformDirection(t.forward); sb.Append(System.FormattableString.Invariant($" {Mathf.Atan2(fw.x, fw.z) * Mathf.Rad2Deg:F0}")); }
          }
          Vector3 grip = socket.TransformPoint(dp);
          Vector3 bladeWorld = socket.rotation * dq * Vector3.up;
          Vector3 bd = go.transform.InverseTransformDirection(bladeWorld);
          Vector3 ed = go.transform.InverseTransformDirection(socket.rotation * dq * Vector3.forward);
          Vector3 tip = go.transform.InverseTransformPoint(grip + bladeWorld * bladeLength);
          Vector3 lh = anim.GetBoneTransform(HumanBodyBones.LeftHand).position, rh = anim.GetBoneTransform(HumanBodyBones.RightHand).position;
          float along = Mathf.Clamp(Vector3.Dot(lh - grip, bladeWorld), 0f, bladeLength);
          float toBlade = Vector3.Distance(lh, grip + bladeWorld * along);
          sb.Append(System.FormattableString.Invariant($" | {bd.x:F3} {bd.y:F3} {bd.z:F3} | {ed.x:F3} {ed.y:F3} {ed.z:F3} | {tip.x:F2} {tip.y:F2} {tip.z:F2} | {Vector3.Distance(lh, rh):F2} | {toBlade:F2}"));
          if (shieldRenderers.Count > 0) {
            float off = float.MaxValue; Vector3 centre = Vector3.zero;
            foreach (var rend in shieldRenderers) {
              centre += rend.bounds.center / shieldRenderers.Count;
              for (int k = 0; k <= 9; k++) {
                Vector3 q = grip + bladeWorld * (bladeLength * k / 9f);
                Vector3 near = rend.transform.TransformPoint(rend.localBounds.ClosestPoint(rend.transform.InverseTransformPoint(q)));
                off = Mathf.Min(off, Vector3.Distance(q, near));
              }
            }
            Vector3 c = go.transform.InverseTransformPoint(centre);
            sb.Append(System.FormattableString.Invariant($" | {off:F2} | {c.x:F2} {c.y:F2} {c.z:F2}"));
          }
          sb.AppendLine();
        }
      } finally { graph.Destroy(); }
    }
  }
} finally { UnityEngine.Object.DestroyImmediate(go); }
return sb.ToString();
