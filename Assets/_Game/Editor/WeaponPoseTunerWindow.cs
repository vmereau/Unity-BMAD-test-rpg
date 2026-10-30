using System.Collections.Generic;
using System.Linq;
using Game.Combat;
using Game.Core;
using Game.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.Editor
{
    /// <summary>
    /// <c>Tools/Combat/Weapon Pose Tuner</c> — preview a weapon's <c>_Visual</c> on the Player's drawn-hand or sheath
    /// socket while scrubbing a clip, move / rotate it with the scene gizmos, then save the family pose
    /// (<see cref="WeaponArchetypeSO.drawnPose"/> / <see cref="WeaponArchetypeSO.sheathedPose"/>) or the weapon's grip
    /// (the <c>Mesh</c> children of its <c>_Visual</c> prefab). The preview is a <see cref="HideFlags.DontSave"/>
    /// instance; the sampled pose is stopped and the rig restored on every exit path and before every scene / prefab
    /// save, so the target is never saved posed or carrying an extra child. Edit mode only.
    /// </summary>
    public class WeaponPoseTunerWindow : EditorWindow
    {
        private const string TAG = "[WeaponPoseTuner]";
        private const string MODE_DRAWN = "Drawn";
        private const string MODE_SHEATHED = "Sheathed";
        private const string MESH_CHILD = "Mesh";

        private static readonly Color HitboxColor = new(1f, 0.5f, 0f, 0.9f);
        private static readonly HumanBodyBones[] HumanBones = System.Enum.GetValues(typeof(HumanBodyBones))
            .Cast<HumanBodyBones>().Where(b => b != HumanBodyBones.LastBone).ToArray();

        // Target
        private GameObject _root;
        private Animator _animator;
        private EquipmentVisuals _visuals;

        // Weapon
        private WeaponSO _weapon;
        private bool _sheathedMode;
        private GameObject _preview;
        private bool _hasPreview; // detects a preview deleted by the user (fake-null)
        private WeaponArchetypeSO _countedArchetype;
        private int _archetypeWeaponCount;

        // Clip
        private readonly List<AnimationClip> _clips = new();
        private AnimationClip _clip;
        private int _frame;
        private int _frameCount = 1;
        // Scopes AnimationMode to this window so stopping our preview never kills the Animation window's.
        private AnimationModeDriver _modeDriver;
        // StopAnimationMode does not revert bones posed by a Humanoid clip — the rig's local transforms are
        // snapshotted when the preview starts and restored by StopPreview.
        private readonly List<(Transform t, Vector3 pos, Quaternion rot, Vector3 scale)> _restPose = new();
        private bool _resampleAfterSave;
        private bool _lastUsesTwoHandGrip;

        // UI
        private Label _targetLabel;
        private ObjectField _weaponField;
        private Label _weaponInfoLabel;
        private DropdownField _modeDropdown;
        private HelpBox _warningBox;
        private HelpBox _playModeBox;
        private VisualElement _clipSection;
        private DropdownField _clipDropdown;
        private ObjectField _clipOverrideField;
        private SliderInt _frameSlider;
        private Button _selectPoseButton;
        private Button _selectGripButton;
        private Button _savePoseButton;
        private Button _saveGripButton;
        private Button _revertButton;

        [MenuItem("Tools/Combat/Weapon Pose Tuner")]
        public static void ShowWindow()
        {
            WeaponPoseTunerWindow wnd = GetWindow<WeaponPoseTunerWindow>();
            wnd.titleContent = new GUIContent("Weapon Pose Tuner");
            wnd.minSize = new Vector2(360, 460);
        }

        private static bool IsPlaying => EditorApplication.isPlayingOrWillChangePlaymode;

        // --- Lifecycle ---

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            PrefabStage.prefabStageClosing += OnPrefabStageClosing;
            PrefabStage.prefabSaving += OnPrefabSaving;
            PrefabStage.prefabSaved += OnPrefabSaved;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            PrefabStage.prefabStageClosing -= OnPrefabStageClosing;
            PrefabStage.prefabSaving -= OnPrefabSaving;
            PrefabStage.prefabSaved -= OnPrefabSaved;
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            Undo.undoRedoPerformed -= OnUndoRedo;
            Cleanup();
            DestroyModeDriver();
        }

        private void OnBeforeAssemblyReload()
        {
            Cleanup();
            DestroyModeDriver();
        }

        private void DestroyModeDriver()
        {
            if (_modeDriver != null) DestroyImmediate(_modeDriver);
            _modeDriver = null;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode || change == PlayModeStateChange.EnteredEditMode) ResetTarget();
            else RefreshAll();
        }

        private void OnPrefabStageClosing(PrefabStage stage)
        {
            if (_root != null && stage.IsPartOfPrefabContents(_root)) ResetTarget();
        }

        // Saving while a frame is sampled would write the posed bones: restore the rig first, resample after.
        private void OnPrefabSaving(GameObject _) => SuspendForSave();
        private void OnSceneSaving(Scene _, string __) => SuspendForSave();
        private void OnPrefabSaved(GameObject _) => ResumeAfterSave();
        private void OnSceneSaved(Scene _) => ResumeAfterSave();

        private void SuspendForSave()
        {
            if (!IsSampling()) return;
            StopPreview();
            _resampleAfterSave = true;
        }

        private void ResumeAfterSave()
        {
            if (!_resampleAfterSave) return;
            _resampleAfterSave = false;
            EditorApplication.delayCall += () => { if (this != null && _clip != null && _animator != null) SampleCurrentFrame(); };
        }

        // Undo of Save Pose: re-apply the restored archetype to the preview and write it back to disk.
        private void OnUndoRedo()
        {
            WeaponArchetypeSO archetype = _weapon != null ? _weapon.archetype : null;
            if (archetype == null) return;
            AssetDatabase.SaveAssetIfDirty(archetype);
            if (_preview != null)
            {
                EquipmentVisuals.ApplyArchetypePoses(_preview, archetype);
                ApplyTwoHandGrip();
            }
        }

        // Target deleted, scene closed, preview deleted by the user → reset.
        private void OnEditorUpdate()
        {
            if (_root == null && (_preview != null || _animator != null || _visuals != null)) ResetTarget();
            else if (_hasPreview && _preview == null)
            {
                StopPreview();
                _hasPreview = false;
                RefreshAll();
            }
        }

        // --- UI ---

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;

            Label title = new Label("Weapon Pose Tuner");
            title.style.fontSize = 20;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 10;
            root.Add(title);

            _playModeBox = new HelpBox("Edit mode only — exit Play mode to tune poses.", HelpBoxMessageType.Warning);
            root.Add(_playModeBox);

            root.Add(Header("Target"));
            _targetLabel = new Label("No target");
            root.Add(_targetLabel);
            root.Add(new Button(UseSelection) { text = "Use Selection (Player, scene or Prefab Mode)" });

            root.Add(Header("Weapon"));
            _weaponField = new ObjectField("Weapon") { objectType = typeof(WeaponSO), allowSceneObjects = false };
            _weaponField.RegisterValueChangedCallback(evt => SetWeapon(evt.newValue as WeaponSO));
            root.Add(_weaponField);
            _weaponInfoLabel = new Label();
            _weaponInfoLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_weaponInfoLabel);
            _modeDropdown = new DropdownField("Pose", new List<string> { MODE_DRAWN, MODE_SHEATHED }, 0);
            _modeDropdown.RegisterValueChangedCallback(evt => SetMode(evt.newValue == MODE_SHEATHED));
            root.Add(_modeDropdown);
            _warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            root.Add(_warningBox);

            _clipSection = new VisualElement();
            root.Add(_clipSection);
            _clipSection.Add(Header("Clip"));
            _clipDropdown = new DropdownField("Clip", new List<string>(), -1);
            _clipDropdown.RegisterValueChangedCallback(_ => OnClipDropdownChanged());
            _clipSection.Add(_clipDropdown);
            _clipOverrideField = new ObjectField("Clip Override") { objectType = typeof(AnimationClip), allowSceneObjects = false };
            _clipOverrideField.RegisterValueChangedCallback(evt => OnClipOverrideChanged(evt.newValue as AnimationClip));
            _clipSection.Add(_clipOverrideField);
            _frameSlider = new SliderInt("Frame", 0, 1) { showInputField = true };
            _frameSlider.RegisterValueChangedCallback(evt => SetFrame(evt.newValue));
            _clipSection.Add(_frameSlider);
            var stepRow = Row();
            stepRow.Add(new Button(() => SetFrame(_frame - 1)) { text = "◀" });
            stepRow.Add(new Button(() => SetFrame(_frame + 1)) { text = "▶" });
            stepRow.Add(new Button(StopPreview) { text = "Stop Preview" });
            _clipSection.Add(stepRow);

            root.Add(Header("Edit"));
            var selectRow = Row();
            _selectPoseButton = new Button(() => SelectNode(grip: false)) { text = "Select Pose Node" };
            selectRow.Add(_selectPoseButton);
            _selectGripButton = new Button(() => SelectNode(grip: true)) { text = "Select Grip (Mesh)" };
            selectRow.Add(_selectGripButton);
            root.Add(selectRow);
            root.Add(new HelpBox(
                "Pose node = the archetype pose (whole family, Undo-able). Grip (Mesh) = this weapon only: where the hand " +
                "sits on the model; written to both Drawn/Mesh and Sheathed/Mesh, not Undo-able (use version control). " +
                "Move / rotate with the scene gizmos, check several frames, then save.", HelpBoxMessageType.Info));

            var saveRow = Row();
            _savePoseButton = new Button(SavePose) { text = "Save Pose to Archetype" };
            _savePoseButton.style.backgroundColor = new Color(0.2f, 0.4f, 0.2f);
            _savePoseButton.style.color = Color.white;
            saveRow.Add(_savePoseButton);
            _saveGripButton = new Button(SaveGrip) { text = "Save Grip to Weapon" };
            saveRow.Add(_saveGripButton);
            _revertButton = new Button(Revert) { text = "Revert" };
            saveRow.Add(_revertButton);
            root.Add(saveRow);

            RefreshAll();
        }

        private static Label Header(string text)
        {
            Label header = new Label(text);
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginTop = 10;
            header.style.marginBottom = 4;
            return header;
        }

        private static VisualElement Row()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            return row;
        }

        // --- Target / weapon / mode ---

        private void UseSelection()
        {
            if (IsPlaying) return;
            Cleanup();
            GameObject selected = Selection.activeGameObject;
            _visuals = selected != null ? ResolveVisuals(selected) : null;
            if (selected != null && _visuals == null)
                GameLog.Warn(TAG, $"'{selected.name}' has no EquipmentVisuals in its hierarchy — not a valid target");
            _animator = _visuals != null ? ResolveAnimator(_visuals) : null;
            if (_visuals != null && _animator == null)
                GameLog.Warn(TAG, $"'{_visuals.name}' has no Animator — not a valid target");
            _root = _animator != null ? _visuals.gameObject : null;
            if (_root == null) _visuals = null;
            RebuildPreview();
        }

        // The selection's own EquipmentVisuals (selected or a parent), else the first one under its stage / scene root.
        private static EquipmentVisuals ResolveVisuals(GameObject selected)
        {
            EquipmentVisuals visuals = selected.GetComponentInParent<EquipmentVisuals>(true);
            if (visuals != null) return visuals;
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            GameObject root = stage != null && stage.IsPartOfPrefabContents(selected)
                ? stage.prefabContentsRoot
                : selected.transform.root.gameObject;
            return root.GetComponentInChildren<EquipmentVisuals>(true);
        }

        // The Animator EquipmentVisuals drives (its serialized _animator), else the one on / under its GameObject.
        private static Animator ResolveAnimator(EquipmentVisuals visuals)
        {
            var animator = new SerializedObject(visuals).FindProperty("_animator")?.objectReferenceValue as Animator;
            return animator != null ? animator : visuals.GetComponentInChildren<Animator>(true);
        }

        private void ResetTarget()
        {
            Cleanup();
            _root = null;
            _visuals = null;
            _animator = null;
            RebuildClipList();
            RefreshAll();
        }

        private void SetWeapon(WeaponSO weapon)
        {
            Cleanup();
            _weapon = weapon;
            RebuildPreview();
        }

        private void SetMode(bool sheathed)
        {
            Cleanup();
            _sheathedMode = sheathed;
            RebuildPreview();
        }

        private void Revert() => RebuildPreview();

        // --- Preview ---

        private void RebuildPreview()
        {
            Cleanup();
            Transform socket = IsPlaying ? null : ResolveSocket(out _);
            if (socket != null && _weapon != null && _weapon.equipVisualPrefab != null)
            {
                _preview = Instantiate(_weapon.equipVisualPrefab);
                _preview.name = $"{_weapon.equipVisualPrefab.name} (Pose Tuner preview)";
                foreach (Transform t in _preview.GetComponentsInChildren<Transform>(true))
                    t.gameObject.hideFlags = HideFlags.DontSave;
                // Same placement as EquipmentVisuals.RefreshWeapon: root at identity on the socket, archetype poses on the children.
                _preview.transform.SetParent(socket, false);
                _preview.transform.localPosition = Vector3.zero;
                _preview.transform.localRotation = Quaternion.identity;
                EquipmentVisuals.ApplyArchetypePoses(_preview, _weapon.archetype);
                Transform drawn = _preview.transform.Find(MODE_DRAWN);
                Transform sheathed = _preview.transform.Find(MODE_SHEATHED);
                if (drawn == null || sheathed == null)
                    GameLog.Warn(TAG, $"'{_weapon.equipVisualPrefab.name}' is missing its Drawn / Sheathed child — nothing to tune for that state");
                if (drawn != null) drawn.gameObject.SetActive(!_sheathedMode);
                if (sheathed != null) sheathed.gameObject.SetActive(_sheathedMode);
                _hasPreview = true;
                ApplyTwoHandGrip();
            }
            RebuildClipList();
            RefreshAll();
            if (_preview != null && _clip != null) SampleCurrentFrame();
            SceneView.RepaintAll();
        }

        private void DestroyPreview()
        {
            _hasPreview = false;
            if (_preview == null) return;
            if (Selection.transforms.Any(t => t != null && t.IsChildOf(_preview.transform)))
                Selection.objects = Selection.objects.Where(o => !(o is GameObject go && go.transform.IsChildOf(_preview.transform))).ToArray();
            DestroyImmediate(_preview);
            _preview = null;
        }

        private void Cleanup()
        {
            StopPreview();
            DestroyPreview();
        }

        // Drawn: archetype hand (Left only when asked and assigned). Sheathed: archetype socket (Back only when asked and
        // assigned). A non-null warning on a fallback socket blocks Save Pose — the pose would be measured on the wrong bone.
        private Transform ResolveSocket(out string warning)
        {
            warning = null;
            if (_visuals == null) return null;
            var so = new SerializedObject(_visuals);
            Transform Socket(string field) => so.FindProperty(field)?.objectReferenceValue as Transform;
            WeaponArchetypeSO archetype = _weapon != null ? _weapon.archetype : null;

            Transform socket;
            if (_sheathedMode)
            {
                Transform back = Socket("_backWeaponSocket");
                socket = EquipmentVisuals.ResolveSheathSocket(archetype, Socket("_undrawnWeaponSocket"), back);
                if (archetype != null && archetype.sheathSocket == WeaponSheathSocket.Back && back == null)
                    warning = "Archetype asks for the Back socket but _backWeaponSocket is unassigned — previewing on the hip; Save Pose disabled.";
            }
            else
            {
                Transform left = Socket("_leftWeaponSocket");
                socket = EquipmentVisuals.ResolveDrawnSocket(archetype, Socket("_weaponSocket"), left);
                if (archetype != null && archetype.drawnHand == WeaponHand.Left && left == null)
                    warning = "Archetype asks for the Left hand but _leftWeaponSocket is unassigned — previewing in the right hand; Save Pose disabled.";
            }
            if (socket == null) warning = $"EquipmentVisuals has no {(_sheathedMode ? "sheath" : "hand")} socket assigned.";
            return socket;
        }

        private Transform PoseNode() =>
            _preview != null ? _preview.transform.Find(_sheathedMode ? MODE_SHEATHED : MODE_DRAWN) : null;

        // Drawn + two-hand-grip archetype: the pose comes from the hands every frame (TwoHandGripConstraint), so the
        // preview is re-solved instead of edited, and only gripRoll / gripSlide (archetype inspector) + the grip stay tunable.
        private bool UsesTwoHandGrip => !_sheathedMode && _weapon != null && _weapon.archetype != null && _weapon.archetype.twoHandedGrip;

        private void ApplyTwoHandGrip()
        {
            if (!UsesTwoHandGrip || _preview == null || _visuals == null) return;
            TwoHandGripConstraint.Apply(PoseNode(), _preview.transform.parent, _visuals.LeftHandSocket, _weapon.archetype);
        }

        // 10 Hz: archetype inspector edits (gripRoll / gripSlide) show without moving the mouse; toggling twoHandedGrip
        // rebuilds the preview so a stale solved pose can never be saved as the rigid drawnPose.
        private void OnInspectorUpdate()
        {
            bool uses = UsesTwoHandGrip;
            if (uses != _lastUsesTwoHandGrip)
            {
                _lastUsesTwoHandGrip = uses;
                if (_preview != null) RebuildPreview();
                return;
            }
            if (uses && _preview != null)
            {
                ApplyTwoHandGrip();
                SceneView.RepaintAll();
            }
        }

        // --- Clips ---

        private void RebuildClipList()
        {
            _clips.Clear();
            RuntimeAnimatorController controller = _weapon != null && _weapon.ResolvedAnimatorOverride != null
                ? _weapon.ResolvedAnimatorOverride
                : _animator != null ? _animator.runtimeAnimatorController : null;
            if (controller != null)
                _clips.AddRange(controller.animationClips.Where(c => c != null).Distinct().OrderBy(c => c.name));

            if (_clipDropdown == null) return;
            _clipDropdown.choices = _clips.Select(ClipLabel).ToList();
            bool keep = _clip != null && (_clips.Contains(_clip) || _clipOverrideField.value == _clip);
            if (!keep)
            {
                _clip = null;
                _frame = 0;
                _frameCount = 1;
            }
            int index = _clip != null ? _clips.IndexOf(_clip) : -1;
            _clipDropdown.SetValueWithoutNotify(index >= 0 ? _clipDropdown.choices[index] : null);
        }

        // Clip name + source file, so same-named clips (e.g. several "mixamo.com") stay distinguishable.
        private static string ClipLabel(AnimationClip clip) =>
            $"{clip.name}  ({System.IO.Path.GetFileName(AssetDatabase.GetAssetPath(clip))})";

        private void OnClipDropdownChanged()
        {
            int index = _clipDropdown.index;
            _clipOverrideField.SetValueWithoutNotify(null);
            SetClip(index >= 0 && index < _clips.Count ? _clips[index] : null);
        }

        private void OnClipOverrideChanged(AnimationClip clip)
        {
            _clipDropdown.SetValueWithoutNotify(null);
            SetClip(clip);
        }

        private void SetClip(AnimationClip clip)
        {
            StopPreview();
            if (clip != null && (clip.legacy || clip.frameRate <= 0f))
            {
                GameLog.Warn(TAG, $"Clip '{clip.name}' is legacy or has no frame rate — can't be previewed");
                clip = null;
                _clipOverrideField.SetValueWithoutNotify(null);
            }
            if (clip != null && _animator != null && clip.humanMotion != _animator.isHuman)
                GameLog.Warn(TAG, $"Clip '{clip.name}' is {(clip.humanMotion ? "Humanoid" : "Generic")} but the target rig is not — the pose may not move");
            _clip = clip;
            _frameCount = clip != null ? Mathf.RoundToInt(clip.length * clip.frameRate) + 1 : 1;
            _frame = 0;
            RefreshAll();
            if (_clip != null) SampleCurrentFrame();
        }

        private void SetFrame(int frame)
        {
            if (_animator == null || _clip == null) return;
            _frame = Mathf.Clamp(frame, 0, _frameCount - 1);
            _frameSlider?.SetValueWithoutNotify(_frame);
            SampleCurrentFrame();
        }

        private bool IsSampling() => _modeDriver != null && AnimationMode.InAnimationMode(_modeDriver);

        private void SampleCurrentFrame()
        {
            if (IsPlaying || _animator == null || _clip == null) return;
            if (_modeDriver == null)
            {
                _modeDriver = CreateInstance<AnimationModeDriver>();
                _modeDriver.hideFlags = HideFlags.HideAndDontSave;
            }
            if (!AnimationMode.InAnimationMode(_modeDriver))
            {
                SnapshotRestPose();
                AnimationMode.StartAnimationMode(_modeDriver);
            }
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(_animator.gameObject, _clip, _frame / _clip.frameRate);
            AnimationMode.EndSampling();
            ApplyTwoHandGrip();
            SceneView.RepaintAll();
        }

        private void StopPreview()
        {
            if (IsSampling()) AnimationMode.StopAnimationMode(_modeDriver);
            RestoreRestPose();
            SceneView.RepaintAll();
        }

        // Only what a clip can drive: the Humanoid bones (sockets and other children stay editable during a preview),
        // or for a Generic rig every transform under the Animator. The Animator's own transform and the preview are excluded.
        private void SnapshotRestPose()
        {
            _restPose.Clear();
            if (_animator == null) return;
            IEnumerable<Transform> driven = _animator.isHuman
                ? HumanBones.Select(_animator.GetBoneTransform)
                    .Where(t => t != null)
                : _animator.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in driven)
                if (t != _animator.transform && (_preview == null || !t.IsChildOf(_preview.transform)))
                    _restPose.Add((t, t.localPosition, t.localRotation, t.localScale));
        }

        private void RestoreRestPose()
        {
            foreach (var (t, pos, rot, scale) in _restPose)
            {
                if (t == null) continue;
                t.localPosition = pos;
                t.localRotation = rot;
                t.localScale = scale;
            }
            _restPose.Clear();
        }

        // --- Edit / save ---

        private void SelectNode(bool grip)
        {
            Transform node = PoseNode();
            if (node != null && grip) node = node.Find(MESH_CHILD);
            if (node == null)
            {
                GameLog.Warn(TAG, grip ? "Preview has no Mesh child under the pose node" : "Preview has no Drawn / Sheathed child");
                return;
            }
            Selection.activeGameObject = node.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        // Archetype pose = the node's pose in socket space (what ApplyArchetypePoses writes under an identity root),
        // computed from world space so a moved preview root is still saved correctly.
        private void SavePose()
        {
            if (UsesTwoHandGrip)
            {
                GameLog.Warn(TAG, "Pose not saved: two-hand grip archetype — tune gripRoll / gripSlide instead");
                return;
            }
            Transform node = PoseNode();
            WeaponArchetypeSO archetype = _weapon != null ? _weapon.archetype : null;
            Transform socket = ResolveSocket(out string warning);
            if (node == null || archetype == null || socket == null || warning != null)
            {
                GameLog.Warn(TAG, $"Pose not saved: {warning ?? "no preview pose node or no archetype"}");
                return;
            }
            if (_preview.transform.parent != socket)
            {
                GameLog.Warn(TAG, $"Pose not saved: the preview was moved off '{socket.name}' — press Revert");
                return;
            }
            var pose = new WeaponPose
            {
                localPosition = socket.InverseTransformPoint(node.position),
                localEulerAngles = (Quaternion.Inverse(socket.rotation) * node.rotation).eulerAngles,
            };

            Undo.RecordObject(archetype, $"Save {(_sheathedMode ? "sheathed" : "drawn")} pose");
            if (_sheathedMode) archetype.sheathedPose = pose;
            else archetype.drawnPose = pose;
            EditorUtility.SetDirty(archetype);
            AssetDatabase.SaveAssetIfDirty(archetype);
            GameLog.Info(TAG, $"{archetype.name}.{(_sheathedMode ? "sheathedPose" : "drawnPose")} = " +
                              $"{pose.localPosition.ToString("F4")} / {pose.localEulerAngles.ToString("F2")}");
        }

        // Grip = the Mesh child's local pose; written to both Drawn/Mesh and Sheathed/Mesh so the two states stay in sync.
        private void SaveGrip()
        {
            Transform node = PoseNode();
            Transform mesh = node != null ? node.Find(MESH_CHILD) : null;
            GameObject prefab = _weapon != null ? _weapon.equipVisualPrefab : null;
            string path = prefab != null ? AssetDatabase.GetAssetPath(prefab) : null;
            if (mesh == null || string.IsNullOrEmpty(path))
            {
                GameLog.Warn(TAG, "Grip not saved: no Mesh child under the pose node, or the weapon has no _Visual prefab asset");
                return;
            }
            PrefabAssetType type = PrefabUtility.GetPrefabAssetType(prefab);
            if (type != PrefabAssetType.Regular && type != PrefabAssetType.Variant)
            {
                GameLog.Error(TAG, $"Grip not saved: '{path}' is a {type} asset, not a prefab");
                return;
            }
            PrefabStage openStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (openStage != null && openStage.assetPath == path)
            {
                GameLog.Warn(TAG, $"Grip not saved: '{path}' is open in Prefab Mode — close it first");
                return;
            }
            if (type == PrefabAssetType.Variant)
                GameLog.Info(TAG, $"'{path}' is a variant — the grip is saved as an override on it");

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform drawnMesh = contents.transform.Find($"{MODE_DRAWN}/{MESH_CHILD}");
                Transform sheathedMesh = contents.transform.Find($"{MODE_SHEATHED}/{MESH_CHILD}");
                if (drawnMesh == null || sheathedMesh == null)
                {
                    GameLog.Error(TAG, $"'{path}' is missing Drawn/Mesh or Sheathed/Mesh — grip not saved");
                    return;
                }
                foreach (Transform t in new[] { drawnMesh, sheathedMesh })
                {
                    t.localPosition = mesh.localPosition;
                    t.localRotation = mesh.localRotation;
                }
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                GameLog.Info(TAG, $"Grip saved to '{path}': {mesh.localPosition.ToString("F4")} / {mesh.localEulerAngles.ToString("F2")}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // --- Scene GUI ---

        // Drawn mode: the WeaponHitbox sweep shape (box on the hitbox GO, wherever it sits under Drawn).
        private void OnSceneGUI(SceneView view)
        {
            if (_sheathedMode) return;
            if (Event.current.type == EventType.Repaint) ApplyTwoHandGrip();
            Transform node = PoseNode();
            WeaponHitbox hitbox = node != null ? node.GetComponentInChildren<WeaponHitbox>(true) : null;
            if (hitbox == null || !hitbox.TryGetComponent(out BoxCollider box)) return;
            Matrix4x4 previous = Handles.matrix;
            Handles.matrix = hitbox.transform.localToWorldMatrix;
            Handles.color = HitboxColor;
            Handles.DrawWireCube(box.center, box.size);
            Handles.matrix = previous;
        }

        // --- Refresh ---

        private void RefreshAll()
        {
            if (_targetLabel == null) return;

            bool playing = IsPlaying;
            _playModeBox.style.display = playing ? DisplayStyle.Flex : DisplayStyle.None;

            _targetLabel.text = _root != null
                ? $"Target: {_root.name}"
                : "No target — select the Player (scene or Prefab Mode) and press Use Selection.";

            WeaponArchetypeSO archetype = _weapon != null ? _weapon.archetype : null;
            _weaponInfoLabel.text = _weapon == null ? string.Empty
                : archetype == null ? "No archetype — preview uses the prefab poses; the pose can't be saved."
                : $"Archetype: {archetype.name} ({archetype.drawnHand} hand, {archetype.sheathSocket} sheath) — " +
                  $"pose shared by {CountWeaponsUsing(archetype)} weapon(s)" +
                  (UsesTwoHandGrip ? ". Two-hand grip: the drawn pose follows the hands — tune gripRoll / gripSlide on the " +
                                     "archetype (live preview); Save Pose / Select Pose Node are disabled." : string.Empty);

            ResolveSocket(out string warning);
            if (_weapon != null && _weapon.equipVisualPrefab == null) warning = "Weapon has no equipVisualPrefab.";
            _warningBox.text = warning ?? string.Empty;
            _warningBox.style.display = warning != null && _root != null && _weapon != null ? DisplayStyle.Flex : DisplayStyle.None;

            bool hasTarget = _root != null && !playing;
            bool hasPreview = _preview != null && !playing;
            _weaponField.SetEnabled(hasTarget);
            _modeDropdown.SetEnabled(hasTarget);
            _clipSection.SetEnabled(hasTarget);
            _frameSlider.highValue = Mathf.Max(1, _frameCount - 1);
            _frameSlider.SetValueWithoutNotify(_frame);
            _selectPoseButton.SetEnabled(hasPreview && !UsesTwoHandGrip);
            _selectGripButton.SetEnabled(hasPreview);
            _savePoseButton.SetEnabled(hasPreview && archetype != null && warning == null && !UsesTwoHandGrip);
            _saveGripButton.SetEnabled(hasPreview);
            _revertButton.SetEnabled(hasPreview);
        }

        // Cached per archetype: a FindAssets over every WeaponSO on each refresh is wasteful.
        private int CountWeaponsUsing(WeaponArchetypeSO archetype)
        {
            if (_countedArchetype == archetype) return _archetypeWeaponCount;
            _countedArchetype = archetype;
            _archetypeWeaponCount = AssetDatabase.FindAssets("t:WeaponSO")
                .Select(guid => AssetDatabase.LoadAssetAtPath<WeaponSO>(AssetDatabase.GUIDToAssetPath(guid)))
                .Count(w => w != null && w.archetype == archetype);
            return _archetypeWeaponCount;
        }
    }
}
