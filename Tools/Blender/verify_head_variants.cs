// Unity MCP execute_code method body (spec-npc-head-variants). Read this file and pass its contents as code.
// Isolated preview scene on the NPC model (Humanoid_BaseHero); modifies no asset. For every HeadAppearance_* asset:
//  - posed exposed-join gap (head ring vs canonical body ring at 1.585 m) over 8 samples of 5 clips, bare body;
//  - Temp/HeadVariants/unity_<Name>.png: rows = clips (mid-clip pose), columns = bare front / bare side /
//    Company armor front / Company armor rear three-quarter, neck close-ups;
//  - Temp/HeadVariants/unity_portraits.png: idle face close-ups (front + three-quarter) of every head.
var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Characters/Humanoid_BaseHero.prefab");
var heads = AssetDatabase.FindAssets("t:HeadAppearanceSO", new[] { "Assets/_Game/Data/Characters/Heads" })
    .Select(g => AssetDatabase.LoadAssetAtPath<Game.NPC.HeadAppearanceSO>(AssetDatabase.GUIDToAssetPath(g))).OrderBy(h => h.name).ToArray();
if (heads.Length == 0) throw new System.Exception("No HeadAppearance_* assets under Data/Characters/Heads.");
var clips = new[] { "Idle.fbx", "Walking.fbx", "Running.fbx", "Combat/one handed/AttackOverhead.fbx", "Combat/dodge roll.fbx" };
string folder = "Temp/HeadVariants"; System.IO.Directory.CreateDirectory(folder);
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
Camera camera = null; RenderTexture rt = null;
var report = new System.Text.StringBuilder();
const int S = 300;
try
{
var root = new GameObject("HeadVerification");
UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
var character = (GameObject)UnityEngine.Object.Instantiate(model, root.transform);
var animator = character.GetComponent<Animator>();
animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.applyRootMotion = false; animator.fireEvents = false;
foreach (var rb in character.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;
var renderers = character.GetComponentsInChildren<SkinnedMeshRenderer>();
var body = System.Array.Find(renderers, r => r.name == "base_hero");
var head = System.Array.Find(renderers, r => r.name == "Head");
if (body == null || head == null) throw new System.Exception("Model has no base_hero / Head SkinnedMeshRenderer.");
var defaultMesh = head.sharedMesh;
var outfit = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
    "Assets/_Game/Prefabs/Items/Armors/CompanyGruntArmor_Visual.prefab"), character.transform);
var part = outfit.GetComponent<Game.Inventory.SkinnedBodyPart>();
if (!Game.Inventory.SkinnedMeshRebinder.TryBind(part.Renderer, part.BoneNames, part.RootBoneName,
    Game.Inventory.SkinnedMeshRebinder.IndexBones(body.bones, body.rootBone), body.rootBone, new System.Collections.Generic.List<string>()))
    throw new System.Exception("Could not bind Company armor.");
foreach (var r in character.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { r.updateWhenOffscreen = true; r.forceMatrixRecalculationPerRender = true; }
foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
var camObj = new GameObject("Cam"); camObj.transform.SetParent(root.transform); camObj.layer = 31;
camera = camObj.AddComponent<Camera>();
camera.scene = scene; camera.overrideSceneCullingMask = UnityEditor.SceneManagement.EditorSceneManager.GetSceneCullingMask(scene);
camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.11f, .12f, .14f);
camera.nearClipPlane = .01f; camera.farClipPlane = 20; camera.fieldOfView = 30;
foreach (var (rot, intensity) in new[] { (Quaternion.Euler(30, 145, 0), 2f), (Quaternion.Euler(20, -35, 0), .7f) })
{
    var lo = new GameObject("Light"); lo.transform.SetParent(root.transform); lo.transform.rotation = rot;
    var l = lo.AddComponent<Light>(); l.type = LightType.Directional; l.intensity = intensity; l.cullingMask = 1 << 31;
}
rt = new RenderTexture(S, S, 24); camera.targetTexture = rt;

// Rest pairs: canonical body ring vertex -> nearest head vertex (identical positions at rest).
var restBody = body.sharedMesh.vertices;
Matrix4x4[] Skin(SkinnedMeshRenderer r) { var bp = r.sharedMesh.bindposes; var m = new Matrix4x4[bp.Length]; for (int i = 0; i < bp.Length; i++) m[i] = r.bones[i].localToWorldMatrix * bp[i]; return m; }
Vector3 Pose(int i, Vector3[] v, BoneWeight[] w, Matrix4x4[] m)
{
    var b = w[i]; var p = v[i];
    return m[b.boneIndex0].MultiplyPoint3x4(p) * b.weight0 + m[b.boneIndex1].MultiplyPoint3x4(p) * b.weight1
         + m[b.boneIndex2].MultiplyPoint3x4(p) * b.weight2 + m[b.boneIndex3].MultiplyPoint3x4(p) * b.weight3;
}
void Shot(Texture2D sheet, int col, int row, int rows, Vector3 target, Vector3 offset)
{
    camera.transform.position = target + offset; camera.transform.LookAt(target); camera.Render();
    var old = RenderTexture.active; try { RenderTexture.active = rt; sheet.ReadPixels(new Rect(0, 0, S, S), col * S, (rows - 1 - row) * S); } finally { RenderTexture.active = old; }
}
var portraits = new Texture2D(2 * S, heads.Length * S, TextureFormat.RGB24, false);
try
{
foreach (var appearance in heads)
{
    if (!Game.AI.NpcHeadAppearance.IsCompatible(appearance.Mesh, defaultMesh, out string why)) throw new System.Exception(appearance.name + ": " + why);
    head.sharedMesh = appearance.Mesh; if (appearance.Material != null) head.sharedMaterial = appearance.Material;
    var restHead = head.sharedMesh.vertices;
    var pairs = new System.Collections.Generic.List<(int h, int b)>();
    for (int i = 0; i < restBody.Length; i++)
    {
        if (Mathf.Abs(restBody[i].y - 1.585f) > .00001f) continue;
        int best = 0; float d = float.MaxValue;
        for (int j = 0; j < restHead.Length; j++) { float e = (restHead[j] - restBody[i]).sqrMagnitude; if (e < d) { d = e; best = j; } }
        pairs.Add((best, i));
    }
    if (pairs.Count < 12) throw new System.Exception(appearance.name + ": only " + pairs.Count + " exposed-join pairs at 1.585 m - seam height or mesh space changed");
    float maxGap = 0; var hw = head.sharedMesh.boneWeights; var bw = body.sharedMesh.boneWeights;
    var sheet = new Texture2D(4 * S, clips.Length * S, TextureFormat.RGB24, false);
    try
    {
        for (int c = 0; c < clips.Length; c++)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath("Assets/_Game/Art/Characters/Humanoids/Animations/" + clips[c]).OfType<AnimationClip>().First(x => !x.name.StartsWith("__preview"));
            var graph = UnityEngine.Playables.PlayableGraph.Create("HeadPreview");
            try
            {
                var output = UnityEngine.Animations.AnimationPlayableOutput.Create(graph, "out", animator);
                var playable = UnityEngine.Animations.AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(false);
                UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(output, playable);
                for (int f = 0; f < 8; f++)
                {
                    UnityEngine.Playables.PlayableExtensions.SetTime(playable, clip.length * f / 8); graph.Evaluate(0);
                    var hm = Skin(head); var bm = Skin(body);
                    foreach (var (h, b) in pairs) maxGap = Mathf.Max(maxGap, (Pose(h, restHead, hw, hm) - Pose(b, restBody, bw, bm)).magnitude);
                }
                UnityEngine.Playables.PlayableExtensions.SetTime(playable, clip.length * .5f); graph.Evaluate(0);
                var neck = animator.GetBoneTransform(HumanBodyBones.Neck).position;
                var fwd = character.transform.forward; var right = character.transform.right;
                for (int armored = 0; armored < 2; armored++)
                {
                    body.enabled = armored == 0; outfit.SetActive(armored == 1);
                    Shot(sheet, armored * 2, c, clips.Length, neck, fwd * .55f + Vector3.up * .03f);
                    Shot(sheet, armored * 2 + 1, c, clips.Length, neck, armored == 0 ? right * .55f : (-fwd + right).normalized * .55f);
                }
                if (c == 0)
                {
                    body.enabled = true; outfit.SetActive(false);
                    var face = animator.GetBoneTransform(HumanBodyBones.Head).position + Vector3.up * .06f;
                    int row = System.Array.IndexOf(heads, appearance);
                    Shot(portraits, 0, row, heads.Length, face, fwd * .6f);
                    Shot(portraits, 1, row, heads.Length, face, (fwd + right * .8f).normalized * .6f);
                }
            }
            finally { graph.Destroy(); }
        }
        sheet.Apply();
        string name = appearance.name.Replace("HeadAppearance_", "");
        System.IO.File.WriteAllBytes($"{folder}/unity_{name}.png", sheet.EncodeToPNG());
        report.AppendLine($"{name}: {pairs.Count} join pairs, max posed exposed-join gap {maxGap * 1000:F4} mm over {clips.Length}x8 poses");
    }
    finally { UnityEngine.Object.DestroyImmediate(sheet); }
}
portraits.Apply();
System.IO.File.WriteAllBytes($"{folder}/unity_portraits.png", portraits.EncodeToPNG());
}
finally { UnityEngine.Object.DestroyImmediate(portraits); }
return report.ToString();
}
finally
{
    if (camera != null) camera.targetTexture = null;
    if (rt != null) UnityEngine.Object.DestroyImmediate(rt);
    if (scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
}
