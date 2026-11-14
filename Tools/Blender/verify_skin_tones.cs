// Unity MCP execute_code method body (spec-npc-skin-tones). Read this file and pass its contents as code.
// Isolated preview scene on the NPC model (Humanoid_BaseHero) in the idle pose; modifies no asset.
//  - Untoned check: renders the model with its SG_SkinLit materials and with full copies of them switched to URP Lit
//    (every saved property) and reports the mean / max per-channel pixel difference (spec: untoned looks unchanged).
//  - Temp/SkinTones/tones.png: rows = Default + every Tone_* asset; columns = face front, face three-quarter, bare hand,
//    bare full body, Company armor hand, Company armor neck (rear three-quarter).
var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Characters/Humanoid_BaseHero.prefab");
var tones = AssetDatabase.FindAssets("t:SkinToneSO", new[] { "Assets/_Game/Data/Characters/SkinTones" })
    .Select(g => AssetDatabase.LoadAssetAtPath<Game.NPC.SkinToneSO>(AssetDatabase.GUIDToAssetPath(g)))
    .OrderByDescending(t => t.Tone.grayscale).ToArray();
string folder = "Temp/SkinTones"; System.IO.Directory.CreateDirectory(folder);
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
Camera camera = null; RenderTexture rt = null;
var temp = new System.Collections.Generic.List<UnityEngine.Object>();
var report = new System.Text.StringBuilder();
const int S = 280;
bool asyncShaders = ShaderUtil.allowAsyncCompilation;
ShaderUtil.allowAsyncCompilation = false; // a variant compiled async renders nothing on its first use
try
{
var root = new GameObject("SkinToneVerification");
UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
var character = (GameObject)UnityEngine.Object.Instantiate(model, root.transform);
var animator = character.GetComponent<Animator>();
animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.applyRootMotion = false; animator.fireEvents = false;
foreach (var rb in character.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;
var renderers = character.GetComponentsInChildren<SkinnedMeshRenderer>();
var body = System.Array.Find(renderers, r => r.name == "base_hero");
var head = System.Array.Find(renderers, r => r.name == "Head");
var outfit = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
    "Assets/_Game/Prefabs/Items/Armors/CompanyGruntArmor_Visual.prefab"), character.transform);
var part = outfit.GetComponent<Game.Inventory.SkinnedBodyPart>();
if (!Game.Inventory.SkinnedMeshRebinder.TryBind(part.Renderer, part.BoneNames, part.RootBoneName,
    Game.Inventory.SkinnedMeshRebinder.IndexBones(body.bones, body.rootBone), body.rootBone, new System.Collections.Generic.List<string>()))
    throw new System.Exception("Could not bind Company armor.");
var armor = part.Renderer;
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

var clip = AssetDatabase.LoadAllAssetsAtPath("Assets/_Game/Art/Characters/Humanoids/Animations/Idle.fbx").OfType<AnimationClip>().First(x => !x.name.StartsWith("__preview"));
var graph = UnityEngine.Playables.PlayableGraph.Create("SkinTonePreview");
try
{
    var output = UnityEngine.Animations.AnimationPlayableOutput.Create(graph, "out", animator);
    var playable = UnityEngine.Animations.AnimationClipPlayable.Create(graph, clip);
    UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(output, playable);
    UnityEngine.Playables.PlayableExtensions.SetTime(playable, clip.length * .5f); graph.Evaluate(0);

    var fwd = character.transform.forward; var right = character.transform.right;
    var face = animator.GetBoneTransform(HumanBodyBones.Head).position + Vector3.up * .06f;
    var hand = animator.GetBoneTransform(HumanBodyBones.RightHand).position;
    var neck = animator.GetBoneTransform(HumanBodyBones.Neck).position;
    var chest = animator.GetBoneTransform(HumanBodyBones.Hips).position + Vector3.up * .15f;
    void Shot(Texture2D sheet, int col, int row, int rows, Vector3 target, Vector3 offset)
    {
        camera.transform.position = target + offset; camera.transform.LookAt(target); camera.Render();
        var old = RenderTexture.active; try { RenderTexture.active = rt; sheet.ReadPixels(new Rect(0, 0, S, S), col * S, (rows - 1 - row) * S); } finally { RenderTexture.active = old; }
    }
    var baseBody = body.sharedMaterial; var baseHead = head.sharedMaterial; var baseArmor = armor.sharedMaterial;

    // Untoned check: SG_SkinLit vs URP Lit copies, bare face + body and armored hand.
    var lit = Shader.Find("Universal Render Pipeline/Lit");
    Material LitCopy(Material m)
    {
        // Full copy (every saved property: _BumpScale, maps, toggles), then the original shader back.
        var c = new Material(m) { shader = lit }; temp.Add(c);
        if (c.GetTexture("_BumpMap") != null) c.EnableKeyword("_NORMALMAP");
        return c;
    }
    var cmp = new[] { new Texture2D(4 * S, S, TextureFormat.RGB24, false), new Texture2D(4 * S, S, TextureFormat.RGB24, false) };
    temp.AddRange(cmp);
    for (int k = 0; k < 2; k++)
    {
        bool sg = k == 0;
        body.sharedMaterial = sg ? baseBody : LitCopy(baseBody); head.sharedMaterial = sg ? baseHead : LitCopy(baseHead); armor.sharedMaterial = sg ? baseArmor : LitCopy(baseArmor);
        body.enabled = true; outfit.SetActive(false);
        Shot(cmp[k], 0, 0, 1, face, fwd * .6f);
        Shot(cmp[k], 1, 0, 1, chest + Vector3.up * .5f, fwd * 3.2f);
        body.enabled = false; outfit.SetActive(true);
        Shot(cmp[k], 2, 0, 1, hand, (fwd + right * .4f).normalized * .45f);
        Shot(cmp[k], 3, 0, 1, chest + Vector3.up * .3f, fwd * .9f); // armor torso (normal-map strength)
        cmp[k].Apply();
    }
    var a = cmp[0].GetPixels32(); var b = cmp[1].GetPixels32(); double sum = 0; int max = 0;
    for (int i = 0; i < a.Length; i++)
        foreach (var d in new[] { a[i].r - b[i].r, a[i].g - b[i].g, a[i].b - b[i].b }) { sum += Mathf.Abs(d); max = Mathf.Max(max, Mathf.Abs(d)); }
    report.AppendLine($"Untoned SG_SkinLit vs URP Lit: mean |diff| {sum / (a.Length * 3):F3}/255, max {max}/255");
    var both = new Texture2D(4 * S, 2 * S, TextureFormat.RGB24, false); temp.Add(both);
    both.SetPixels(0, S, 4 * S, S, cmp[0].GetPixels()); both.SetPixels(0, 0, 4 * S, S, cmp[1].GetPixels()); both.Apply();
    System.IO.File.WriteAllBytes($"{folder}/untoned_sg_vs_lit.png", both.EncodeToPNG());

    // Tone sheet.
    var rows = new Game.NPC.SkinToneSO[] { null }.Concat(tones).ToArray();
    var sheet = new Texture2D(6 * S, rows.Length * S, TextureFormat.RGB24, false); temp.Add(sheet);
    for (int row = 0; row < rows.Length; row++)
    {
        var tone = rows[row];
        body.sharedMaterial = Game.AI.SkinToneMaterials.Get(baseBody, tone);
        head.sharedMaterial = Game.AI.SkinToneMaterials.Get(baseHead, tone);
        armor.sharedMaterial = Game.AI.SkinToneMaterials.Get(baseArmor, tone);
        body.enabled = true; outfit.SetActive(false);
        Shot(sheet, 0, row, rows.Length, face, fwd * .6f);
        Shot(sheet, 1, row, rows.Length, face, (fwd + right * .8f).normalized * .6f);
        Shot(sheet, 2, row, rows.Length, hand + Vector3.down * .06f, (fwd + right * .4f).normalized * .45f);
        Shot(sheet, 3, row, rows.Length, chest + Vector3.up * .5f, fwd * 3.2f);
        body.enabled = false; outfit.SetActive(true);
        Shot(sheet, 4, row, rows.Length, hand + Vector3.down * .06f, (fwd + right * .4f).normalized * .45f);
        Shot(sheet, 5, row, rows.Length, neck, (-fwd + right).normalized * .55f);
        report.AppendLine($"row {row}: {(tone != null ? tone.name : "Default")}");
    }
    sheet.Apply();
    System.IO.File.WriteAllBytes($"{folder}/tones.png", sheet.EncodeToPNG());
    body.sharedMaterial = baseBody; head.sharedMaterial = baseHead; armor.sharedMaterial = baseArmor;
}
finally { graph.Destroy(); }
return report.ToString();
}
finally
{
    ShaderUtil.allowAsyncCompilation = asyncShaders;
    if (camera != null) camera.targetTexture = null;
    if (rt != null) UnityEngine.Object.DestroyImmediate(rt);
    foreach (var o in temp) if (o != null) UnityEngine.Object.DestroyImmediate(o);
    // No SkinToneMaterials.Clear(): live NPCs (play mode) share the cached copies.
    if (scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
}
