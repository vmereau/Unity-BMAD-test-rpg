// Unity MCP execute_code body, PLAY MODE: equips a weapon (and an optional off-hand item), runs attack chains on the
// Player through PlayerCombat.TryAttack and logs one CSV row per rendered frame, one file per run
// (spec-sword-three-hit-combo). Same run logic and columns as action_root_motion_play_measure.cs, read by
// action_root_motion_analyse.py, plus the blade and the off-hand:
//   tipF tipS tipY = blade tip (grip + bladeLength along the Drawn child's +Y), lhRh = left hand to right hand (m),
//   lhBlade = left hand to the blade segment (m), offBlade = closest point of the off-hand item's oriented renderer boxes to
//   10 points along the blade, 10 cm apart (m, -1 without an off-hand item; the true minimum can be up to 5 cm lower).
// `runs`: one spec per run, played one after the other, "key=value;key=value":
//   presses = clip seconds after the n-th attack state was entered at which the next press is sent (re-sent every frame
//             until accepted, so 0 = the earliest possible chain); a negative value ends the chain.
//             -1 single hit | 0,0,-1 earliest chain | 0.88,0.83,-1 latest chain | 0,0,0.85,-1 + recovery restart
//   out     = CSV path (existing folder; written when the attack has been over for 0.6 s)
//   offhand = asset path of an off-hand item (empty = none), weapon = asset path, speed = Animator speed
//   cancel  = "state:seconds" calls PlayerCombat.CancelAttack() that long after that attack state was entered
// Flat, clear ground, no NPC walking by (one pushed the player 10 cm at a time during a run). Each run refills stamina
// and starts from where the script was started; the equipped items stay equipped and in the inventory afterwards, the
// cursor stays locked. A malformed spec or a failed write stops the remaining runs with one console error.
// A clip re-imported during play mode is NOT picked up by the running Animator: leave play mode after a re-fit.
string[] runs = {
  "presses=-1;out=C:/Temp/sword_single.csv",
  "presses=0,0,-1;out=C:/Temp/sword_early.csv",
};
string weaponPath = "Assets/_Game/Data/Items/Weapons/Swords/Weapon_ShortIronSword.asset";
float bladeLength = 0.9f;
var pc = UnityEngine.Object.FindFirstObjectByType<Game.Combat.PlayerCombat>();
if (pc == null) return "no PlayerCombat in the scene (play mode?)";
var sm = pc.GetComponent<Game.Player.PlayerStateManager>();
var arm = pc.GetComponent<Game.Player.ActionRootMotion>();
var anim = pc.GetComponent<Animator>();
var equipment = pc.GetComponent<Game.Inventory.EquipmentSystem>(); // NPCs carry one too: never FindFirstObjectByType
var visuals = pc.GetComponent<Game.Inventory.EquipmentVisuals>();
var stamina = pc.GetComponent<Game.Combat.StaminaSystem>();
var controller = pc.GetComponent<CharacterController>();
var gameView = System.Type.GetType("UnityEditor.GameView,UnityEditor"); if (gameView != null) EditorWindow.FocusWindowIfItsOpen(gameView);
Game.Core.CursorManager.Lock();
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
// Equip(ItemSO) only takes an item that is in the player's inventory: add it first (the system EquipmentSystem reads).
var inventory = typeof(Game.Inventory.EquipmentSystem).GetField("_inventorySystem", flags).GetValue(equipment) as Game.Inventory.InventorySystem;
System.Action<string, Game.Inventory.EquipmentSlot> equip = (path, slot) => {
  var item = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Game.Inventory.ItemSO>(path);
  if (equipment.GetEquipped(slot) == item) return;
  if (item == null) { equipment.Unequip(slot); return; }
  inventory.AddItem(item); equipment.Equip(item);
};
var tryAttack = typeof(Game.Combat.PlayerCombat).GetMethod("TryAttack", flags);
var comboStep = typeof(Game.Combat.PlayerCombat).GetField("_comboStep", flags);
int[] hashes = { 0, Animator.StringToHash("Attack_1_State"), Animator.StringToHash("Attack_2_State"), Animator.StringToHash("Attack_3_State") };
System.Func<int, int> index = h => h == hashes[1] ? 1 : h == hashes[2] ? 2 : h == hashes[3] ? 3 : 0;
var bones = new[] { HumanBodyBones.Hips, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftHand, HumanBodyBones.RightHand };
var tf = new Transform[bones.Length]; for (int i = 0; i < bones.Length; i++) tf[i] = anim.GetBoneTransform(bones[i]);
Transform lToes = anim.GetBoneTransform(HumanBodyBones.LeftToes), rToes = anim.GetBoneTransform(HumanBodyBones.RightToes);
Transform socket = null, leftSocket = null;
foreach (var t in pc.GetComponentsInChildren<Transform>(true)) { if (t.name == "WeaponSocket") socket = t; if (t.name == "LeftWeaponSocket") leftSocket = t; }
var home = new Pose(pc.transform.position, pc.transform.rotation);
var sb = new System.Text.StringBuilder();
float[] presses = null; string outFile = null; bool hasOffhand = false; float animatorSpeed = 1f, restoreSpeed = anim.speed;
int cancelState = 0; float cancelAfter = 0f; bool cancelled = false;
float settleUntil = 0f, t0 = -1f, enteredAt = -1f, idleSince = -1f;
int runIndex = -1, entries = 0, lastDest = 0, lastFrame = -1; bool lastInTransition = false, pressed = false;
Vector3 origin = Vector3.zero, fwd = Vector3.forward, right = Vector3.right;
var inv = System.Globalization.CultureInfo.InvariantCulture;
System.Func<bool> nextRun = () => {
  if (++runIndex >= runs.Length) return false;
  presses = new[] { -1f }; outFile = "C:/Temp/sword_run" + runIndex + ".csv"; animatorSpeed = 1f; cancelState = 0;
  string offhandPath = null, weapon = weaponPath;
  foreach (string part in runs[runIndex].Split(';')) {
    int eq = part.IndexOf('='); if (eq < 0) continue;
    string key = part.Substring(0, eq).Trim(), value = part.Substring(eq + 1).Trim();
    if (key == "presses") presses = System.Array.ConvertAll(value.Split(','), s => float.Parse(s, inv));
    else if (key == "offhand") offhandPath = value;
    else if (key == "weapon") weapon = value;
    else if (key == "out") outFile = value;
    else if (key == "speed") animatorSpeed = float.Parse(value, inv);
    else if (key == "cancel") { string[] c = value.Split(':'); cancelState = int.Parse(c[0]); cancelAfter = float.Parse(c[1], inv); }
  }
  equip(weapon, Game.Inventory.EquipmentSlot.Weapon); equip(offhandPath, Game.Inventory.EquipmentSlot.Offhand);
  hasOffhand = !string.IsNullOrEmpty(offhandPath);
  if (stamina != null) stamina.RefillToMax();
  controller.enabled = false; pc.transform.SetPositionAndRotation(home.position, home.rotation); controller.enabled = true;
  if (!sm.IsInCombat) sm.SetInCombat(true);
  // SetInCombat alone leaves the weapon in its sheath (the draw clip's WeaponSwap event moves it): put it in the hand.
  if (visuals != null) visuals.SetCombatState(true);
  sb.Clear(); settleUntil = Time.time + 1.5f; t0 = -1f; enteredAt = -1f; idleSince = -1f;
  entries = 0; lastDest = 0; lastInTransition = false; pressed = false; cancelled = false;
  return true;
};
if (!nextRun()) return "no runs";
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
  if (!cancelled && cancelState > 0 && entries == cancelState && Time.time - enteredAt >= cancelAfter / animatorSpeed) { cancelled = true; pc.CancelAttack(); }
  System.Func<Vector3, string> p3 = v => { Vector3 d = v - origin; return System.FormattableString.Invariant($"{Vector3.Dot(d, fwd):F4},{Vector3.Dot(d, right):F4},{d.y:F4}"); };
  Vector3 r = pc.transform.position - origin;
  var info = inTr && nextI != 0 ? next : cur;
  sb.Append(System.FormattableString.Invariant($"{Time.time - t0:F4},{dest},{(dest != 0 ? info.normalizedTime * info.length : 0f):F4},{(inTr ? anim.GetAnimatorTransitionInfo(0).normalizedTime : 0f):F3},{nextI},{(sm.IsAttacking ? 1 : 0)},{(arm != null && arm.IsActive ? 1 : 0)},{Vector3.Dot(r, fwd):F4},{Vector3.Dot(r, right):F4}"));
  foreach (var t in tf) sb.Append(',').Append(p3(t.position));
  sb.Append(System.FormattableString.Invariant($",{lToes.position.y - origin.y:F4},{rToes.position.y - origin.y:F4}"));
  // blade from the drawn weapon visual under the right-hand socket (Drawn child: grip at origin, blade +Y)
  Transform drawn = null;
  foreach (var t in socket.GetComponentsInChildren<Transform>(false)) if (t.name == "Drawn") { drawn = t; break; }
  if (drawn != null) {
    Vector3 grip = drawn.position, blade = drawn.up, tip = grip + blade * bladeLength, lh = tf[3].position;
    System.Func<Vector3, float> toBlade = p => Vector3.Distance(p, grip + blade * Mathf.Clamp(Vector3.Dot(p - grip, blade), 0f, bladeLength));
    float off = -1f;
    if (hasOffhand && leftSocket != null) {
      off = float.MaxValue;
      // closest point of each renderer's own (oriented) box: the world AABB of a tilted shield swallows the sword hand
      foreach (var rend in leftSocket.GetComponentsInChildren<Renderer>(false))
        for (int k = 0; k <= 9; k++) {
          Vector3 q = grip + blade * (bladeLength * k / 9f);
          Vector3 near = rend.transform.TransformPoint(rend.localBounds.ClosestPoint(rend.transform.InverseTransformPoint(q)));
          off = Mathf.Min(off, Vector3.Distance(q, near));
        }
      if (off == float.MaxValue) off = -1f;
    }
    sb.Append(',').Append(p3(tip)).Append(System.FormattableString.Invariant($",{Vector3.Distance(lh, tf[4].position):F3},{toBlade(lh):F3},{off:F3}"));
  } else sb.Append(",0,0,0,-1,-1,-1");
  sb.Append('\n');
  if (sm.IsAttacking) idleSince = -1f; else if (idleSince < 0f) idleSince = Time.time;
  if ((idleSince > 0f && Time.time - idleSince > 0.6f) || Time.time - t0 > 12f) {
    anim.speed = restoreSpeed;
    // A failed write or a malformed next spec must not leave the handler running every frame.
    try {
      System.IO.File.WriteAllText(outFile, "t,state,stateTime,blend,next,attacking,rootMotion,rootF,rootS,hipsF,hipsS,hipsY,lfF,lfS,lfY,rfF,rfS,rfY,lhF,lhS,lhY,rhF,rhS,rhY,ltY,rtY,tipF,tipS,tipY,lhRh,lhBlade,offBlade\n" + sb);
      if (!nextRun()) Application.onBeforeRender -= handler;
    } catch (System.Exception e) { Application.onBeforeRender -= handler; Debug.LogError("sword_combo_play_measure stopped at run " + runIndex + ": " + e.Message); }
  }
};
Application.onBeforeRender += handler;
return "started " + runs.Length + " runs";
