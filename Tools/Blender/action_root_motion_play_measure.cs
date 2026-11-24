// Unity MCP execute_code body, PLAY MODE: runs one unarmed attack chain on the Player through PlayerCombat.TryAttack and
// logs one CSV row per rendered frame to `outFile` (spec-action-root-motion-unarmed-combo). Positions are metres in the
// frame the player had at the first press: f = forward, s = sideways (+ right), y = height.
//   presses[n] = clip seconds after the n-th attack state was entered at which the next press is sent (it is re-sent every
//                frame until PlayerCombat accepts it, so 0 = the earliest possible chain). A negative value ends the chain.
//   Examples: {-1} single jab; {0, 0, -1} earliest chain; {0.80, 1.10, -1} latest chain; {0, 0, 0.75, -1} + recovery restart.
// Columns: t, state(1-3, 0 = none), stateTime(s), blend(0 = none, else transition progress), next, attacking, rootMotion,
//          root f s, hips f s y, LFoot f s y, RFoot f s y, LHand f s y, RHand f s y, LToes y, RToes y
// The file is written when the attack has been over for 0.6 s (use a new file name per run and poll for it).
// Run it on flat, clear ground and put the player back between runs; refill StaminaSystem (5 swings empty it): a
// refused press ends the chain early. It leaves the cursor locked and the player in combat stance.
float[] presses = { 0f, 0f, -1f };
float animatorSpeed = 1f;
string outFile = "C:/Temp/arm_chain.csv"; // an existing folder: the log is only written at the end of the run
var pc = UnityEngine.Object.FindFirstObjectByType<Game.Combat.PlayerCombat>();
if (pc == null) return "no PlayerCombat in the scene (play mode?)";
var sm = pc.GetComponent<Game.Player.PlayerStateManager>();
var arm = pc.GetComponent<Game.Player.ActionRootMotion>();
var anim = pc.GetComponent<Animator>();
var gameView = System.Type.GetType("UnityEditor.GameView,UnityEditor"); if (gameView != null) EditorWindow.FocusWindowIfItsOpen(gameView);
Game.Core.CursorManager.Lock();
if (!sm.IsInCombat) sm.SetInCombat(true);
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var tryAttack = typeof(Game.Combat.PlayerCombat).GetMethod("TryAttack", flags);
var comboStep = typeof(Game.Combat.PlayerCombat).GetField("_comboStep", flags);
int[] hashes = { 0, Animator.StringToHash("Attack_1_State"), Animator.StringToHash("Attack_2_State"), Animator.StringToHash("Attack_3_State") };
System.Func<int, int> index = h => h == hashes[1] ? 1 : h == hashes[2] ? 2 : h == hashes[3] ? 3 : 0;
var bones = new[] { HumanBodyBones.Hips, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftHand, HumanBodyBones.RightHand };
var tf = new Transform[bones.Length]; for (int i = 0; i < bones.Length; i++) tf[i] = anim.GetBoneTransform(bones[i]);
Transform lToes = anim.GetBoneTransform(HumanBodyBones.LeftToes), rToes = anim.GetBoneTransform(HumanBodyBones.RightToes);
var sb = new System.Text.StringBuilder();
float settleUntil = Time.time + 1.0f, t0 = -1f, enteredAt = -1f, idleSince = -1f;
int entries = 0, lastDest = 0, lastFrame = -1; bool lastInTransition = false, pressed = false;
Vector3 origin = Vector3.zero, fwd = Vector3.forward, right = Vector3.right;
float restoreSpeed = anim.speed;
UnityEngine.Events.UnityAction handler = null;
handler = () => {
  if (Time.frameCount == lastFrame) return; lastFrame = Time.frameCount;
  if (pc == null) { Application.onBeforeRender -= handler; return; }
  if (Time.time < settleUntil) return;
  if (t0 < 0f) {
    t0 = Time.time; origin = pc.transform.position; fwd = pc.transform.forward; right = pc.transform.right;
    anim.speed = animatorSpeed; tryAttack.Invoke(pc, null);
  }
  var cur = anim.GetCurrentAnimatorStateInfo(0); bool inTr = anim.IsInTransition(0); var next = anim.GetNextAnimatorStateInfo(0);
  int curI = index(cur.shortNameHash), nextI = inTr ? index(next.shortNameHash) : 0, dest = inTr ? nextI : curI;
  if (dest != 0 && (dest != lastDest || (inTr && !lastInTransition))) { entries++; enteredAt = Time.time; pressed = false; }
  lastDest = dest; lastInTransition = inTr;
  if (entries > 0 && entries <= presses.Length && !pressed && presses[entries - 1] >= 0f && Time.time - enteredAt >= presses[entries - 1] / animatorSpeed) {
    int before = (int)comboStep.GetValue(pc); tryAttack.Invoke(pc, null);
    if ((int)comboStep.GetValue(pc) != before) pressed = true;
  }
  System.Func<Vector3, string> p3 = v => { Vector3 d = v - origin; return System.FormattableString.Invariant($"{Vector3.Dot(d, fwd):F4},{Vector3.Dot(d, right):F4},{d.y:F4}"); };
  Vector3 r = pc.transform.position - origin;
  var info = inTr && nextI != 0 ? next : cur;
  sb.Append(System.FormattableString.Invariant($"{Time.time - t0:F4},{dest},{(dest != 0 ? info.normalizedTime * info.length : 0f):F4},{(inTr ? anim.GetAnimatorTransitionInfo(0).normalizedTime : 0f):F3},{nextI},{(sm.IsAttacking ? 1 : 0)},{(arm != null && arm.IsActive ? 1 : 0)},{Vector3.Dot(r, fwd):F4},{Vector3.Dot(r, right):F4}"));
  foreach (var t in tf) sb.Append(',').Append(p3(t.position));
  sb.Append(System.FormattableString.Invariant($",{lToes.position.y - origin.y:F4},{rToes.position.y - origin.y:F4}")).Append('\n');
  if (sm.IsAttacking) idleSince = -1f; else if (idleSince < 0f) idleSince = Time.time;
  if ((idleSince > 0f && Time.time - idleSince > 0.6f) || Time.time - t0 > 12f) {
    anim.speed = restoreSpeed; Application.onBeforeRender -= handler;
    System.IO.File.WriteAllText(outFile, "t,state,stateTime,blend,next,attacking,rootMotion,rootF,rootS,hipsF,hipsS,hipsY,lfF,lfS,lfY,rfF,rfS,rfY,lhF,lhS,lhY,rhF,rhS,rhY,ltY,rtY\n" + sb);
  }
};
Application.onBeforeRender += handler;
return "started: presses " + string.Join(" / ", presses) + " -> " + outFile;
