using System.Collections.Generic;
using System.Linq;
using Game.AI;
using Game.Combat;
using Game.Core;
using Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Editor
{
    /// <summary>
    /// <c>Tools/Combat/Hitbox Tuner</c> — scrub an attack clip on a prefab/scene entity, see every
    /// <see cref="WeaponHitbox"/> at the posed frame and the swept trail of the authored window, add
    /// hitboxes to bones and write <c>HitboxEnable(id)</c> / <c>HitboxDisable(id)</c> events
    /// (FBX importer events are normalized time, <c>.anim</c> events are seconds — see
    /// <see cref="HitWindowEvents"/>). Preview uses <see cref="AnimationMode"/> and is stopped on
    /// every exit path so the target is never left posed or dirtied.
    /// </summary>
    public class HitboxTunerWindow : EditorWindow
    {
        private const string TAG = "[HitboxTuner]";
        private const string ALL_ID_LABEL = "(all)";
        private const float NEW_HITBOX_RADIUS = 0.15f;
        private const float TRAIL_DISC_RADIUS = 0.02f;

        private static readonly Color SelectedColor = Color.cyan;
        private static readonly Color OtherColor = new(0.6f, 0.6f, 0.6f, 0.9f);
        private static readonly Color ReachColor = new(1f, 0.5f, 0f, 0.8f);
        private static readonly Color TrailColor = Color.yellow;

        // Target
        private GameObject _root;
        private Animator _animator;
        private EntityMeleeAttacker _attacker;
        private readonly List<WeaponHitbox> _hitboxes = new();

        // Clip
        private readonly List<AnimationClip> _clips = new();
        private AnimationClip _clip;
        private int _frame;
        private int _frameCount = 1;
        private bool _previewActive;
        // Scopes AnimationMode to this window so stopping our preview never kills the Animation window's.
        private AnimationModeDriver _modeDriver;
        private bool _loopWindow;
        private double _lastLoopTime;

        // Window authoring
        private string _hitboxId = string.Empty;
        private int _startFrame;
        private int _endFrame = 1;

        // Swept trail cache: keyed by clip + id + start + end.
        private readonly Dictionary<WeaponHitbox, List<Vector3>> _trails = new();
        private string _trailKey;

        // UI
        private Label _targetLabel;
        private VisualElement _attackerBox;
        private VisualElement _clipSection;
        private DropdownField _clipDropdown;
        private ObjectField _clipOverrideField;
        private Label _clipSourceLabel;
        private SliderInt _frameSlider;
        private Toggle _loopToggle;
        private DropdownField _idDropdown;
        private IntegerField _startField;
        private IntegerField _endField;
        private HelpBox _windowError;
        private Button _writeButton;
        private Button _removeButton;
        private TextField _newIdField;
        private Button _addHitboxButton;

        [MenuItem("Tools/Combat/Hitbox Tuner")]
        public static void ShowWindow()
        {
            HitboxTunerWindow wnd = GetWindow<HitboxTunerWindow>();
            wnd.titleContent = new GUIContent("Hitbox Tuner");
            wnd.minSize = new Vector2(360, 520);
        }

        // --- Lifecycle ---

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
            Selection.selectionChanged += RefreshAddHitboxState;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= StopPreview;
            Selection.selectionChanged -= RefreshAddHitboxState;
            StopPreview();
            if (_modeDriver != null) DestroyImmediate(_modeDriver);
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode) StopPreview();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;

            Label title = new Label("Hitbox Tuner");
            title.style.fontSize = 20;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 10;
            root.Add(title);

            // Target
            root.Add(Header("Target"));
            _targetLabel = new Label("No target");
            root.Add(_targetLabel);
            root.Add(new Button(UseSelection) { text = "Use Selection" });
            _attackerBox = new VisualElement();
            root.Add(_attackerBox);

            // Clip + scrubbing
            _clipSection = new VisualElement();
            root.Add(_clipSection);

            _clipSection.Add(Header("Clip"));
            _clipDropdown = new DropdownField("Clip", new List<string>(), -1);
            _clipDropdown.RegisterValueChangedCallback(_ => OnClipDropdownChanged());
            _clipSection.Add(_clipDropdown);
            _clipOverrideField = new ObjectField("Clip Override") { objectType = typeof(AnimationClip), allowSceneObjects = false };
            _clipOverrideField.RegisterValueChangedCallback(evt => SetClip(evt.newValue as AnimationClip));
            _clipSection.Add(_clipOverrideField);
            _clipSourceLabel = new Label();
            _clipSection.Add(_clipSourceLabel);

            _frameSlider = new SliderInt("Frame", 0, 1) { showInputField = true };
            _frameSlider.RegisterValueChangedCallback(evt => SetFrame(evt.newValue));
            _clipSection.Add(_frameSlider);

            var stepRow = Row();
            stepRow.Add(new Button(() => SetFrame(_frame - 1)) { text = "◀" });
            stepRow.Add(new Button(() => SetFrame(_frame + 1)) { text = "▶" });
            _loopToggle = new Toggle("Loop window");
            _loopToggle.RegisterValueChangedCallback(evt => { _loopWindow = evt.newValue; _lastLoopTime = EditorApplication.timeSinceStartup; });
            stepRow.Add(_loopToggle);
            stepRow.Add(new Button(StopPreview) { text = "Stop Preview" });
            _clipSection.Add(stepRow);

            // Window authoring
            _clipSection.Add(Header("Hit Window"));
            _idDropdown = new DropdownField("Hitbox Id", new List<string> { ALL_ID_LABEL }, 0);
            _idDropdown.RegisterValueChangedCallback(evt =>
            {
                _hitboxId = evt.newValue == ALL_ID_LABEL ? string.Empty : evt.newValue;
                LoadWindowFromClip();
            });
            _clipSection.Add(_idDropdown);

            _startField = new IntegerField("Start Frame");
            _startField.RegisterValueChangedCallback(evt => { _startFrame = evt.newValue; OnWindowChanged(); });
            _clipSection.Add(_startField);
            _endField = new IntegerField("End Frame");
            _endField.RegisterValueChangedCallback(evt => { _endFrame = evt.newValue; OnWindowChanged(); });
            _clipSection.Add(_endField);

            var currentRow = Row();
            currentRow.Add(new Button(() => _startField.value = _frame) { text = "Start = current" });
            currentRow.Add(new Button(() => _endField.value = _frame) { text = "End = current" });
            _clipSection.Add(currentRow);

            _windowError = new HelpBox(string.Empty, HelpBoxMessageType.Error);
            _clipSection.Add(_windowError);

            var writeRow = Row();
            _writeButton = new Button(() => WriteWindow(remove: false)) { text = "Write Events" };
            _writeButton.style.backgroundColor = new Color(0.2f, 0.4f, 0.2f);
            _writeButton.style.color = Color.white;
            writeRow.Add(_writeButton);
            _removeButton = new Button(() => WriteWindow(remove: true)) { text = "Remove Window" };
            writeRow.Add(_removeButton);
            _clipSection.Add(writeRow);

            // Add hitbox
            _clipSection.Add(Header("Add Hitbox"));
            _newIdField = new TextField("New Hitbox Id");
            _newIdField.RegisterValueChangedCallback(_ => RefreshAddHitboxState());
            _clipSection.Add(_newIdField);
            _addHitboxButton = new Button(AddHitboxToSelectedBone) { text = "Add Hitbox To Selected Bone" };
            _clipSection.Add(_addHitboxButton);

            root.Add(new HelpBox(
                "Events on FBX clips from a vendor pack are stored in its .meta — re-importing the pack wipes them.",
                HelpBoxMessageType.Info) { style = { marginTop = 10 } });

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

        // --- Target ---

        private void UseSelection()
        {
            StopPreview();
            GameObject selected = Selection.activeGameObject;
            _root = selected != null ? ResolveTargetRoot(selected) : null;
            if (selected != null && _root == null)
                GameLog.Warn(TAG, $"'{selected.name}' has no Animator in its hierarchy — not a valid target");
            ResolveTarget();
            RebuildClipList();
            RefreshAll();
        }

        // Outermost object of the selection's prefab stage / scene hierarchy that has an Animator in children.
        private static GameObject ResolveTargetRoot(GameObject selected)
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            GameObject root = stage != null && stage.IsPartOfPrefabContents(selected)
                ? stage.prefabContentsRoot
                : selected.transform.root.gameObject;
            return root.GetComponentInChildren<Animator>(true) != null ? root : null;
        }

        private void ResolveTarget()
        {
            _hitboxes.Clear();
            _animator = null;
            _attacker = null;
            if (_root == null) return;
            _animator = _root.GetComponentInChildren<Animator>(true);
            _attacker = _root.GetComponent<EntityMeleeAttacker>();
            _root.GetComponentsInChildren(true, _hitboxes);
        }

        // Target deleted, prefab stage closed, domain reloaded… → reset gracefully.
        private bool ValidateTarget()
        {
            if (_root != null && _animator != null) return true;
            if (_root != null || _animator != null || _previewActive)
            {
                StopPreview();
                _root = null;
                ResolveTarget();
                RebuildClipList();
                RefreshAll();
            }
            return false;
        }

        private void AddAttacker()
        {
            if (_root == null) return;
            _attacker = Undo.AddComponent<EntityMeleeAttacker>(_root);
            RefreshAll();
        }

        // --- Clips ---

        private void RebuildClipList()
        {
            _clips.Clear();
            if (_animator != null)
            {
                PersistentID persistentID = _root.GetComponent<PersistentID>();
                RuntimeAnimatorController controller =
                    persistentID != null && persistentID.Entity != null && persistentID.Entity.AnimatorOverride != null
                        ? persistentID.Entity.AnimatorOverride
                        : _animator.runtimeAnimatorController;
                if (controller != null)
                    _clips.AddRange(controller.animationClips.Where(c => c != null).Distinct().OrderBy(c => c.name));
            }

            if (_clipDropdown == null) return;
            _clipDropdown.choices = _clips.Select(c => c.name).ToList();
            AnimationClip keep = _clip != null && _clips.Contains(_clip) ? _clip : null;
            _clipDropdown.SetValueWithoutNotify(keep != null ? keep.name : null);
            if (keep == null && _clipOverrideField.value == null) SetClip(null);
        }

        private void OnClipDropdownChanged()
        {
            AnimationClip clip = _clips.FirstOrDefault(c => c.name == _clipDropdown.value);
            _clipOverrideField.SetValueWithoutNotify(null);
            SetClip(clip);
        }

        private void SetClip(AnimationClip clip)
        {
            StopPreview();
            _clip = clip;
            _frameCount = clip != null ? Mathf.RoundToInt(clip.length * clip.frameRate) + 1 : 1;
            _frame = 0;
            InvalidateTrail();
            LoadWindowFromClip();
            RefreshAll();
        }

        private bool IsFbxClip(out ModelImporter importer)
        {
            importer = _clip != null ? AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(_clip)) as ModelImporter : null;
            return importer != null;
        }

        private static int FindImporterClip(ModelImporterClipAnimation[] clips, string name)
        {
            for (int i = 0; i < clips.Length; i++)
                if (clips[i].name == name) return i;
            return -1;
        }

        private static ModelImporterClipAnimation[] GetImporterClips(ModelImporter importer)
        {
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            // Empty clipAnimations must be seeded from the defaults, or writing collapses the clip list.
            return clips.Length == 0 ? importer.defaultClipAnimations : clips;
        }

        // Pre-fills start/end from the clip's existing HitboxEnable/HitboxDisable events for the selected id.
        private void LoadWindowFromClip()
        {
            if (_clip == null) return;
            bool found = false;
            int start = 0, end = 0;

            if (IsFbxClip(out ModelImporter importer))
            {
                ModelImporterClipAnimation[] clips = GetImporterClips(importer);
                int index = FindImporterClip(clips, _clip.name);
                if (index >= 0 && HitWindowEvents.TryGetWindow(clips[index].events, _hitboxId, out float s, out float e))
                {
                    start = HitWindowEvents.NormalizedToFrame(s, clips[index].firstFrame, clips[index].lastFrame);
                    end = HitWindowEvents.NormalizedToFrame(e, clips[index].firstFrame, clips[index].lastFrame);
                    found = true;
                }
            }
            else if (HitWindowEvents.TryGetWindow(AnimationUtility.GetAnimationEvents(_clip), _hitboxId, out float s, out float e))
            {
                start = HitWindowEvents.SecondsToFrame(s, _clip.frameRate);
                end = HitWindowEvents.SecondsToFrame(e, _clip.frameRate);
                found = true;
            }

            if (found)
            {
                _startFrame = start;
                _endFrame = end;
            }
            _startField?.SetValueWithoutNotify(_startFrame);
            _endField?.SetValueWithoutNotify(_endFrame);
            OnWindowChanged();
        }

        // --- Preview ---

        private void SetFrame(int frame)
        {
            if (!ValidateTarget() || _clip == null) return;
            _frame = Mathf.Clamp(frame, 0, _frameCount - 1);
            _frameSlider?.SetValueWithoutNotify(_frame);
            SampleCurrentFrame();
        }

        private void SampleCurrentFrame()
        {
            if (_animator == null || _clip == null) return;
            if (_modeDriver == null)
            {
                _modeDriver = CreateInstance<AnimationModeDriver>();
                _modeDriver.hideFlags = HideFlags.HideAndDontSave;
            }
            if (!AnimationMode.InAnimationMode(_modeDriver)) AnimationMode.StartAnimationMode(_modeDriver);
            _previewActive = true;

            if (_trailKey != BuildTrailKey()) RebuildTrail();

            SampleFrame(_frame);
            SceneView.RepaintAll();
        }

        private void SampleFrame(int frame)
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(_animator.gameObject, _clip, frame / _clip.frameRate);
            AnimationMode.EndSampling();
        }

        private void StopPreview()
        {
            if (_modeDriver != null && AnimationMode.InAnimationMode(_modeDriver)) AnimationMode.StopAnimationMode(_modeDriver);
            _previewActive = false;
            _loopWindow = false;
            _loopToggle?.SetValueWithoutNotify(false);
            InvalidateTrail();
            SceneView.RepaintAll();
        }

        private void OnEditorUpdate()
        {
            if (!_loopWindow || _clip == null || !IsWindowValid(out _)) return;
            double now = EditorApplication.timeSinceStartup;
            if (now - _lastLoopTime < 1.0 / _clip.frameRate) return;
            _lastLoopTime = now;
            int next = _frame + 1;
            if (next < _startFrame || next > _endFrame) next = _startFrame;
            SetFrame(next);
        }

        // --- Window authoring ---

        private bool IsWindowValid(out string reason) =>
            HitWindowEvents.IsValidWindow(_startFrame, _endFrame, _frameCount, out reason);

        private void OnWindowChanged()
        {
            InvalidateTrail();
            if (_previewActive) SampleCurrentFrame();
            RefreshWindowState();
        }

        private void WriteWindow(bool remove)
        {
            if (!ValidateTarget() || _clip == null) return;
            if (!remove && !IsWindowValid(out _)) return;
            StopPreview();

            string clipName = _clip.name;
            string path = AssetDatabase.GetAssetPath(_clip);

            if (IsFbxClip(out ModelImporter importer))
            {
                ModelImporterClipAnimation[] clips = GetImporterClips(importer);
                int index = FindImporterClip(clips, clipName);
                if (index < 0)
                {
                    GameLog.Error(TAG, $"Clip '{clipName}' not found in the importer of '{path}' — nothing written");
                    return;
                }
                clips[index].events = remove
                    ? HitWindowEvents.RemoveWindow(clips[index].events, _hitboxId)
                    : HitWindowEvents.ApplyWindow(clips[index].events, _hitboxId,
                        HitWindowEvents.FrameToNormalized(_startFrame, clips[index].firstFrame, clips[index].lastFrame),
                        HitWindowEvents.FrameToNormalized(_endFrame, clips[index].firstFrame, clips[index].lastFrame));
                importer.clipAnimations = clips;
                importer.SaveAndReimport();

                // Reimport replaces the clip sub-asset — re-resolve it by name.
                AnimationClip reloaded = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(c => c.name == clipName);
                if (reloaded != null)
                {
                    _clip = reloaded;
                    if (_clipOverrideField.value != null) _clipOverrideField.SetValueWithoutNotify(reloaded);
                }
                RebuildClipList();
            }
            else
            {
                Undo.RecordObject(_clip, remove ? "Remove Hit Window" : "Write Hit Window");
                AnimationEvent[] existing = AnimationUtility.GetAnimationEvents(_clip);
                AnimationUtility.SetAnimationEvents(_clip, remove
                    ? HitWindowEvents.RemoveWindow(existing, _hitboxId)
                    : HitWindowEvents.ApplyWindow(existing, _hitboxId,
                        HitWindowEvents.FrameToSeconds(_startFrame, _clip.frameRate),
                        HitWindowEvents.FrameToSeconds(_endFrame, _clip.frameRate)));
                EditorUtility.SetDirty(_clip);
                AssetDatabase.SaveAssets();
            }

            string idLabel = string.IsNullOrEmpty(_hitboxId) ? ALL_ID_LABEL : _hitboxId;
            if (remove)
                GameLog.Info(TAG, $"Removed hit window '{idLabel}' from '{clipName}'");
            else
                GameLog.Info(TAG, $"Wrote hit window '{idLabel}' on '{clipName}': frames {_startFrame}→{_endFrame}");
            RefreshAll();
        }

        // --- Add hitbox ---

        private bool CanAddHitbox(out Transform bone, out string id)
        {
            bone = Selection.activeTransform;
            id = _newIdField != null ? _newIdField.value?.Trim() : null;
            return _root != null && _attacker != null && bone != null && bone.IsChildOf(_root.transform)
                   && !string.IsNullOrEmpty(id) && !HasHitboxId(id);
        }

        private bool HasHitboxId(string id)
        {
            if (_attacker == null) return false;
            foreach (EntityMeleeAttacker.NamedHitbox entry in _attacker.Hitboxes)
                if (entry.Id == id) return true;
            return false;
        }

        private void AddHitboxToSelectedBone()
        {
            if (!ValidateTarget() || !CanAddHitbox(out Transform bone, out string id)) return;
            StopPreview();

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName($"Add Hitbox {id}");
            int group = Undo.GetCurrentGroup();

            var go = new GameObject($"Hitbox_{id}") { layer = 0 };
            go.transform.SetParent(bone, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            var sphere = go.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = NEW_HITBOX_RADIUS;
            sphere.enabled = false; // shape definition only — see WeaponHitbox
            var hitbox = go.AddComponent<WeaponHitbox>();
            Undo.RegisterCreatedObjectUndo(go, $"Add Hitbox {id}");

            Undo.RecordObject(_attacker, $"Add Hitbox {id}");
            _attacker.EditorAddHitbox(id, hitbox);
            PrefabUtility.RecordPrefabInstancePropertyModifications(_attacker);
            Undo.CollapseUndoOperations(group);

            Selection.activeGameObject = go;
            GameLog.Info(TAG, $"Added hitbox '{id}' under bone '{bone.name}'");
            _newIdField.SetValueWithoutNotify(string.Empty);
            ResolveTarget();
            RefreshAll();
        }

        // --- UI refresh ---

        private void RefreshAll()
        {
            if (_targetLabel == null) return; // GUI not built yet

            _targetLabel.text = _root != null
                ? $"{_root.name}  (Animator: {(_animator != null ? _animator.gameObject.name : "—")}, {_hitboxes.Count} hitbox(es))"
                : "No target — select an entity (scene or Prefab Mode) and press Use Selection.";

            _attackerBox.Clear();
            if (_root != null && _attacker == null)
            {
                _attackerBox.Add(new HelpBox("No EntityMeleeAttacker on the target root — hitboxes cannot be registered.", HelpBoxMessageType.Warning));
                _attackerBox.Add(new Button(AddAttacker) { text = "Add EntityMeleeAttacker" });
            }

            _clipSection.SetEnabled(_root != null && _animator != null);

            if (_clip != null)
            {
                string kind = IsFbxClip(out _) ? "FBX (importer events, normalized time)" : ".anim (events in seconds)";
                _clipSourceLabel.text = $"{kind} — {_frameCount} frames @ {_clip.frameRate:0.##} fps";
            }
            else
            {
                _clipSourceLabel.text = "No clip selected";
            }
            _frameSlider.highValue = Mathf.Max(1, _frameCount - 1);
            _frameSlider.SetValueWithoutNotify(_frame);

            var ids = new List<string> { ALL_ID_LABEL };
            if (_attacker != null)
                foreach (EntityMeleeAttacker.NamedHitbox entry in _attacker.Hitboxes)
                    if (!string.IsNullOrEmpty(entry.Id) && !ids.Contains(entry.Id)) ids.Add(entry.Id);
            _idDropdown.choices = ids;
            if (!ids.Contains(string.IsNullOrEmpty(_hitboxId) ? ALL_ID_LABEL : _hitboxId)) _hitboxId = string.Empty;
            _idDropdown.SetValueWithoutNotify(string.IsNullOrEmpty(_hitboxId) ? ALL_ID_LABEL : _hitboxId);

            _startField.SetValueWithoutNotify(_startFrame);
            _endField.SetValueWithoutNotify(_endFrame);
            RefreshWindowState();
            RefreshAddHitboxState();
        }

        private void RefreshWindowState()
        {
            if (_windowError == null) return;
            bool valid = IsWindowValid(out string reason);
            _windowError.text = reason;
            _windowError.style.display = valid || _clip == null ? DisplayStyle.None : DisplayStyle.Flex;
            _writeButton.SetEnabled(valid && _clip != null);
            _removeButton.SetEnabled(_clip != null);
        }

        private void RefreshAddHitboxState()
        {
            _addHitboxButton?.SetEnabled(CanAddHitbox(out _, out _));
        }

        // --- Scene view ---

        private string BuildTrailKey() =>
            _clip == null ? null : $"{_clip.GetEntityId()}|{_hitboxId}|{_startFrame}|{_endFrame}";

        private void InvalidateTrail()
        {
            _trails.Clear();
            _trailKey = null;
        }

        // Samples every frame of the window and records each relevant hitbox's shape centre.
        // Caller re-samples the current frame afterwards.
        private void RebuildTrail()
        {
            _trails.Clear();
            _trailKey = BuildTrailKey();
            if (!IsWindowValid(out _)) return;

            foreach (WeaponHitbox hitbox in _hitboxes)
                if (hitbox != null && IsSelectedHitbox(hitbox)) _trails[hitbox] = new List<Vector3>();
            if (_trails.Count == 0) return;

            for (int f = _startFrame; f <= _endFrame; f++)
            {
                SampleFrame(f);
                foreach (KeyValuePair<WeaponHitbox, List<Vector3>> trail in _trails)
                    trail.Value.Add(ShapeCenter(trail.Key));
            }
        }

        private bool IsSelectedHitbox(WeaponHitbox hitbox)
        {
            if (string.IsNullOrEmpty(_hitboxId)) return true;
            return HitboxIdOf(hitbox) == _hitboxId;
        }

        private string HitboxIdOf(WeaponHitbox hitbox)
        {
            if (_attacker == null) return null;
            foreach (EntityMeleeAttacker.NamedHitbox entry in _attacker.Hitboxes)
                if (entry.Hitbox == hitbox) return entry.Id;
            return null;
        }

        private static Vector3 ShapeCenter(WeaponHitbox hitbox)
        {
            Collider shape = hitbox.GetComponent<Collider>();
            Vector3 local = shape switch
            {
                BoxCollider box => box.center,
                SphereCollider sphere => sphere.center,
                CapsuleCollider capsule => capsule.center,
                _ => Vector3.zero,
            };
            Transform t = hitbox.transform;
            return t.position + t.rotation * Vector3.Scale(local, t.lossyScale);
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_root == null || _animator == null) return;

            foreach (WeaponHitbox hitbox in _hitboxes)
            {
                if (hitbox == null) continue;
                Handles.color = IsSelectedHitbox(hitbox) && !string.IsNullOrEmpty(_hitboxId) ? SelectedColor : OtherColor;
                DrawShape(hitbox);
                DrawVerticalReach(hitbox);
                string id = HitboxIdOf(hitbox);
                Handles.Label(ShapeCenter(hitbox), string.IsNullOrEmpty(id) ? hitbox.name : id);
            }

            if (!_previewActive) return;
            Handles.color = TrailColor;
            foreach (KeyValuePair<WeaponHitbox, List<Vector3>> trail in _trails)
            {
                List<Vector3> points = trail.Value;
                if (points.Count == 0) continue;
                for (int i = 0; i < points.Count; i++)
                {
                    if (i > 0) Handles.DrawLine(points[i - 1], points[i], 2f);
                    Handles.DrawSolidDisc(points[i], sceneView.camera.transform.forward, TRAIL_DISC_RADIUS);
                }
                float radius = ApproxRadius(trail.Key);
                DrawWireSphere(points[0], radius);
                DrawWireSphere(points[points.Count - 1], radius);
            }
        }

        private static float ApproxRadius(WeaponHitbox hitbox)
        {
            Collider shape = hitbox.GetComponent<Collider>();
            Vector3 s = hitbox.transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            return shape switch
            {
                SphereCollider sphere => sphere.radius * maxScale,
                CapsuleCollider capsule => capsule.radius * maxScale,
                BoxCollider box => Vector3.Scale(box.size, s).magnitude * 0.5f,
                _ => 0.1f,
            };
        }

        private static void DrawShape(WeaponHitbox hitbox)
        {
            Collider shape = hitbox.GetComponent<Collider>();
            Transform t = hitbox.transform;
            switch (shape)
            {
                case SphereCollider sphere:
                    DrawWireSphere(ShapeCenter(hitbox), ApproxRadius(hitbox));
                    break;
                case BoxCollider box:
                {
                    Matrix4x4 previous = Handles.matrix;
                    Handles.matrix = Matrix4x4.TRS(t.position, t.rotation, t.lossyScale);
                    Handles.DrawWireCube(box.center, box.size);
                    Handles.matrix = previous;
                    break;
                }
                case CapsuleCollider capsule:
                {
                    Vector3 axis = capsule.direction switch { 0 => t.right, 1 => t.up, _ => t.forward };
                    float scaleAlongAxis = Mathf.Abs(capsule.direction switch
                    {
                        0 => t.lossyScale.x,
                        1 => t.lossyScale.y,
                        _ => t.lossyScale.z,
                    });
                    float radius = ApproxRadius(hitbox);
                    float halfSegment = Mathf.Max(0f, capsule.height * scaleAlongAxis * 0.5f - radius);
                    Vector3 center = ShapeCenter(hitbox);
                    DrawWireSphere(center - axis * halfSegment, radius);
                    DrawWireSphere(center + axis * halfSegment, radius);
                    Handles.DrawLine(center - axis * halfSegment, center + axis * halfSegment);
                    break;
                }
            }
        }

        private static void DrawVerticalReach(WeaponHitbox hitbox)
        {
            using var so = new SerializedObject(hitbox);
            float reach = so.FindProperty("_verticalReach").floatValue;
            float radius = so.FindProperty("_verticalReachRadius").floatValue;
            if (reach <= 0f) return;

            Color previous = Handles.color;
            Handles.color = ReachColor;
            Vector3 top = ShapeCenter(hitbox);
            Vector3 bottom = top + Vector3.down * reach;
            DrawWireSphere(top, radius);
            DrawWireSphere(bottom, radius);
            Handles.DrawLine(top, bottom);
            Handles.color = previous;
        }

        private static void DrawWireSphere(Vector3 center, float radius)
        {
            Handles.DrawWireDisc(center, Vector3.up, radius);
            Handles.DrawWireDisc(center, Vector3.right, radius);
            Handles.DrawWireDisc(center, Vector3.forward, radius);
        }
    }
}
