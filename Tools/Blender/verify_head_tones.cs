// Unity MCP execute_code method body (spec-npc-faces-second-batch). Read this file and pass its contents as code.
// Isolated preview scene on the NPC model (Humanoid_BaseHero) in the idle pose; modifies no asset.
//  - Untoned check: every M_Head_* (with its skin mask) vs a full copy switched to URP Lit, face front + left neck;
//    reports the mean / max per-channel pixel difference (must be 0: a mask alone changes nothing).
//  - Temp/HeadTones/heads.png: one row per planned face + tone (PLAN below); columns = face front, face three-quarter,
//    left neck (Company brand side) bare, left neck in Company armor, bare body / armored torso.
var PLAN = new (string head, string tone, bool armored)[] {
    ("Tam", "Tone_Fair", false), ("Brann", "Tone_Olive", false), ("Storekeeper", "Tone_Deep", false),
    ("Harl", "Tone_Weathered", true), ("DockGuard", "Tone_Fair", true), ("GuardLeft", "Tone_Fair", true),
    ("GuardRight", "Tone_Olive", true), ("WarehouseGuard", "Tone_Deep", true), ("Bandit", "Tone_Weathered", false) };
var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Characters/Humanoid_BaseHero.prefab");
string folder = "Temp/HeadTones"; System.IO.Directory.CreateDirectory(folder);
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
Camera camera = null; RenderTexture rt = null;
var temp = new System.Collections.Generic.List<UnityEngine.Object>();
var report = new System.Text.StringBuilder();
const int S = 280;
bool asyncShaders = ShaderUtil.allowAsyncCompilation;
ShaderUtil.allowAsyncCompilation = false; // a variant compiled async renders nothing on its first use
try
{
var root = new GameObject("HeadToneVerification");
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
foreach (var (rot, intensity) in new[] { (Quaternion.Euler(30, 145, 0), 2f), (Quaternion.Euler(20, -35, 0), .7f), (Quaternion.Euler(25, 100, 0), .8f) })
{
    var lo = new GameObject("Light"); lo.transform.SetParent(root.transform); lo.transform.rotation = rot;
    var l = lo.AddComponent<Light>(); l.type = LightType.Directional; l.intensity = intensity; l.cullingMask = 1 << 31;
}
rt = new RenderTexture(S, S, 24); camera.targetTexture = rt;

var clip = AssetDatabase.LoadAllAssetsAtPath("Assets/_Game/Art/Characters/Humanoids/Animations/Idle.fbx").OfType<AnimationClip>().First(x => !x.name.StartsWith("__preview"));
var graph = UnityEngine.Playables.PlayableGraph.Create("HeadTonePreview");
try
{
    var output = UnityEngine.Animations.AnimationPlayableOutput.Create(graph, "out", animator);
    var playable = UnityEngine.Animations.AnimationClipPlayable.Create(graph, clip);
    UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(output, playable);
    UnityEngine.Playables.PlayableExtensions.SetTime(playable, clip.length * .5f); graph.Evaluate(0);

    var fwd = character.transform.forward; var right = character.transform.right;
    var face = animator.GetBoneTransform(HumanBodyBones.Head).position + Vector3.up * .06f;
    var neck = animator.GetBoneTransform(HumanBodyBones.Neck).position + Vector3.up * .05f;
    var chest = animator.GetBoneTransform(HumanBodyBones.Hips).position + Vector3.up * .15f;
    var leftNeck = (-right + fwd * .25f).normalized * .42f; // character's left (Blender +X): the brand side
    void Shot(Texture2D sheet, int col, int row, int rows, Vector3 target, Vector3 offset)
    {
        camera.transform.position = target + offset; camera.transform.LookAt(target); camera.Render();
        var old = RenderTexture.active; try { RenderTexture.active = rt; sheet.ReadPixels(new Rect(0, 0, S, S), col * S, (rows - 1 - row) * S); } finally { RenderTexture.active = old; }
    }
    var baseBody = body.sharedMaterial; var baseHeadMesh = head.sharedMesh; var baseHead = head.sharedMaterial; var baseArmor = armor.sharedMaterial;
    var appearances = AssetDatabase.FindAssets("t:HeadAppearanceSO", new[] { "Assets/_Game/Data/Characters/Heads" })
        .Select(g => AssetDatabase.LoadAssetAtPath<Game.NPC.HeadAppearanceSO>(AssetDatabase.GUIDToAssetPath(g)))
        .ToDictionary(h => h.name.Replace("HeadAppearance_", ""));
    var tones = AssetDatabase.FindAssets("t:SkinToneSO", new[] { "Assets/_Game/Data/Characters/SkinTones" })
        .Select(g => AssetDatabase.LoadAssetAtPath<Game.NPC.SkinToneSO>(AssetDatabase.GUIDToAssetPath(g)))
        .ToDictionary(t => t.name);
    if (appearances.Count == 0) throw new System.Exception("No HeadAppearance_* assets under Data/Characters/Heads.");

    // Untoned check: each head material (mask set, strength 0) vs a full URP Lit copy.
    var lit = Shader.Find("Universal Render Pipeline/Lit");
    double sum = 0; long count = 0; int max = 0; string worst = "";
    var a2 = new Texture2D(2 * S, S, TextureFormat.RGB24, false); var b2 = new Texture2D(2 * S, S, TextureFormat.RGB24, false);
    temp.Add(a2); temp.Add(b2);
    body.enabled = true; outfit.SetActive(false);
    foreach (var kv in appearances.OrderBy(k => k.Key))
    {
        head.sharedMesh = kv.Value.Mesh;
        var copy = new Material(kv.Value.Material) { shader = lit }; temp.Add(copy);
        if (copy.GetTexture("_BumpMap") != null) copy.EnableKeyword("_NORMALMAP");
        for (int k = 0; k < 2; k++)
        {
            var tex = k == 0 ? a2 : b2;
            head.sharedMaterial = k == 0 ? kv.Value.Material : copy;
            Shot(tex, 0, 0, 1, face, fwd * .6f);
            Shot(tex, 1, 0, 1, neck, leftNeck);
            tex.Apply();
        }
        var a = a2.GetPixels32(); var b = b2.GetPixels32(); int headMax = 0;
        for (int i = 0; i < a.Length; i++)
            foreach (var d in new[] { a[i].r - b[i].r, a[i].g - b[i].g, a[i].b - b[i].b }) { sum += Mathf.Abs(d); count++; headMax = Mathf.Max(headMax, Mathf.Abs(d)); }
        if (headMax > max) { max = headMax; worst = kv.Key; }
    }
    report.AppendLine($"Untoned {appearances.Count} heads, SG_SkinLit (masked) vs URP Lit: mean |diff| {sum / count:F3}/255, max {max}/255 {worst}");
    if (max > 0) report.AppendLine($"FAIL: untoned head {worst} differs from URP Lit (a mask alone must change nothing)");

    // Tone sheet.
    var sheet = new Texture2D(5 * S, PLAN.Length * S, TextureFormat.RGB24, false); temp.Add(sheet);
    for (int row = 0; row < PLAN.Length; row++)
    {
        var (headName, toneName, armored) = PLAN[row];
        if (!appearances.TryGetValue(headName, out var app)) throw new System.Exception($"No HeadAppearance_{headName}");
        if (!tones.TryGetValue(toneName, out var tone)) throw new System.Exception($"No {toneName} under Data/Characters/SkinTones");
        head.sharedMesh = app.Mesh;
        body.sharedMaterial = Game.AI.SkinToneMaterials.Get(baseBody, tone);
        head.sharedMaterial = Game.AI.SkinToneMaterials.Get(app.Material, tone);
        armor.sharedMaterial = Game.AI.SkinToneMaterials.Get(baseArmor, tone);
        body.enabled = !armored; outfit.SetActive(armored);
        Shot(sheet, 0, row, PLAN.Length, face, fwd * .6f);
        Shot(sheet, 1, row, PLAN.Length, face, (fwd - right * .8f).normalized * .6f);
        body.enabled = true; outfit.SetActive(false);
        Shot(sheet, 2, row, PLAN.Length, neck, leftNeck);
        body.enabled = false; outfit.SetActive(true);
        Shot(sheet, 3, row, PLAN.Length, neck, leftNeck);
        body.enabled = !armored; outfit.SetActive(armored);
        Shot(sheet, 4, row, PLAN.Length, chest + Vector3.up * .45f, fwd * 1.6f);
        report.AppendLine($"row {row}: {headName} {toneName}{(armored ? " (Company armor)" : "")}");
    }
    sheet.Apply();
    System.IO.File.WriteAllBytes($"{folder}/heads.png", sheet.EncodeToPNG());
    head.sharedMesh = baseHeadMesh; head.sharedMaterial = baseHead; body.sharedMaterial = baseBody; armor.sharedMaterial = baseArmor;
}
finally { graph.Destroy(); }
System.IO.File.WriteAllText($"{folder}/report.txt", report.ToString());
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
