using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Quest;
using Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.Editor.QuestExplorer
{
    /// <summary>
    /// <c>Tools/Quests/Quest Explorer</c> — quest structure, fact setters/readers, validation, play-mode
    /// live state and Scene-view highlight, backed by <see cref="QuestReferenceIndex"/>. The index is
    /// rebuilt once per editor idle after asset / hierarchy / scene / play-mode changes.
    /// </summary>
    public class QuestExplorerWindow : EditorWindow
    {
        private const string TAG = "[QuestExplorer]";
        private const string WINDOW_TITLE = "Quest Explorer";
        private const int LIVE_INTERVAL_MS = 250;
        private const float LIST_PANE_WIDTH = 220f;

        // Persisted across domain reloads.
        [SerializeField] private string _selectedQuestGuid;
        [SerializeField] private string _selectedFactGuid;
        [SerializeField] private bool _factsMode;
        [SerializeField] private bool _highlight;

        private QuestReferenceIndex _index;
        private readonly Dictionary<QuestSO, List<ValidationIssue>> _issues = new Dictionary<QuestSO, List<ValidationIssue>>();
        private readonly Dictionary<string, bool> _foldoutStates = new Dictionary<string, bool>();
        private readonly List<object> _rows = new List<object>();
        private bool _rebuildScheduled;
        private string _search = string.Empty;

        // UI
        private ToolbarToggle _questsToggle;
        private ToolbarToggle _factsToggle;
        private ToolbarToggle _highlightToggle;
        private Label _summary;
        private HelpBox _banner;
        private ListView _list;
        private ScrollView _detail;
        private QuestDetailView _questView;
        private FactDetailView _factView;
        private IVisualElementScheduledItem _liveTick;

        internal QuestReferenceIndex Index => _index;
        internal Dictionary<string, bool> FoldoutStates => _foldoutStates;

        // ── Entry points ──────────────────────────────────────────────────────

        [MenuItem("Tools/Quests/Quest Explorer")]
        public static QuestExplorerWindow Open()
        {
            var w = GetWindow<QuestExplorerWindow>();
            w.titleContent = new GUIContent(WINDOW_TITLE);
            w.minSize = new Vector2(640, 360);
            return w;
        }

        public static void ShowQuest(QuestSO quest)
        {
            if (quest == null) return;
            Open().SelectQuest(quest);
        }

        public static void ShowFact(Fact fact)
        {
            if (fact == null) return;
            Open().SelectFact(fact);
        }

        [MenuItem("CONTEXT/QuestSO/Open in Quest Explorer")]
        private static void ContextOpenQuest(MenuCommand command) => ShowQuest(command.context as QuestSO);

        [MenuItem("CONTEXT/Fact/Show in Quest Explorer")]
        private static void ContextShowFact(MenuCommand command) => ShowFact(command.context as Fact);

        [MenuItem("CONTEXT/PersistentID/Show KilledFact in Quest Explorer")]
        private static void ContextShowPersistentIdFact(MenuCommand command)
        {
            var pid = command.context as PersistentID;
            var fact = QuestIndexCollector.ReadKilledFact(pid);
            if (fact == null)
            {
                GameLog.Warn(TAG, $"PersistentID on '{(pid != null ? pid.gameObject.name : "?")}' has no KilledFact assigned");
                return;
            }
            ShowFact(fact);
        }

        [MenuItem("Assets/Show in Quest Explorer", false, 2000)]
        private static void AssetsShow()
        {
            switch (Selection.activeObject)
            {
                case QuestSO quest: ShowQuest(quest); break;
                case Fact fact: ShowFact(fact); break;
            }
        }

        [MenuItem("Assets/Show in Quest Explorer", true)]
        private static bool AssetsShowValidate() => Selection.activeObject is QuestSO || Selection.activeObject is Fact;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void OnEnable()
        {
            EditorApplication.projectChanged += MarkIndexDirty;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
            Undo.undoRedoPerformed += MarkIndexDirty;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= MarkIndexDirty;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
            Undo.undoRedoPerformed -= MarkIndexDirty;
            EditorApplication.delayCall -= DelayedRebuild;
            _rebuildScheduled = false;
            _liveTick?.Pause();
            QuestSceneHighlighter.Disable();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;

            var toolbar = new Toolbar();
            _questsToggle = new ToolbarToggle { text = "Quests" };
            _questsToggle.RegisterValueChangedCallback(evt => SetMode(facts: !evt.newValue));
            _factsToggle = new ToolbarToggle { text = "Facts" };
            _factsToggle.RegisterValueChangedCallback(evt => SetMode(facts: evt.newValue));
            toolbar.Add(_questsToggle);
            toolbar.Add(_factsToggle);

            var search = new ToolbarSearchField();
            search.style.flexGrow = 1;
            search.RegisterValueChangedCallback(evt =>
            {
                _search = evt.newValue ?? string.Empty;
                RefreshList();
            });
            toolbar.Add(search);

            toolbar.Add(new ToolbarButton(RebuildIndexNow) { text = "Refresh" });
            _highlightToggle = new ToolbarToggle { text = "Highlight in Scene", value = _highlight };
            _highlightToggle.RegisterValueChangedCallback(evt =>
            {
                _highlight = evt.newValue;
                UpdateHighlighter();
            });
            toolbar.Add(_highlightToggle);
            _summary = new Label { style = { unityTextAlign = TextAnchor.MiddleRight, marginLeft = 6, marginRight = 4 } };
            toolbar.Add(_summary);
            root.Add(toolbar);

            _banner = new HelpBox("WorldStateManager not running (load Core.unity)", HelpBoxMessageType.Warning)
            {
                style = { display = DisplayStyle.None }
            };
            root.Add(_banner);

            var split = new TwoPaneSplitView(0, LIST_PANE_WIDTH, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1;

            _list = new ListView(_rows, 20, MakeRow, BindRow) { selectionType = SelectionType.Single };
            _list.style.flexGrow = 1;
            _list.selectionChanged += OnListSelectionChanged;
            split.Add(_list);

            _detail = new ScrollView(ScrollViewMode.Vertical);
            _detail.style.flexGrow = 1;
            _detail.contentContainer.style.paddingLeft = 8;
            _detail.contentContainer.style.paddingRight = 8;
            _detail.contentContainer.style.paddingTop = 6;
            split.Add(_detail);
            root.Add(split);

            _questsToggle.SetValueWithoutNotify(!_factsMode);
            _factsToggle.SetValueWithoutNotify(_factsMode);

            _liveTick = root.schedule.Execute(UpdateLive).Every(LIVE_INTERVAL_MS);
            if (!EditorApplication.isPlaying) _liveTick.Pause();

            RebuildIndexNow();
        }

        // ── Index lifecycle ───────────────────────────────────────────────────

        private void OnHierarchyChanged()
        {
            // Play-mode spawns would rebuild constantly; play-mode transitions are handled separately.
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            MarkIndexDirty();
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode mode) => MarkIndexDirty();

        private void OnSceneClosed(Scene scene) => MarkIndexDirty();

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    _liveTick?.Resume();
                    MarkIndexDirty();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    _liveTick?.Pause();
                    if (_banner != null) _banner.style.display = DisplayStyle.None;
                    MarkIndexDirty();
                    break;
            }
        }

        /// <summary>Schedules a single rebuild for the next editor idle (debounced).</summary>
        internal void MarkIndexDirty()
        {
            if (_rebuildScheduled) return;
            _rebuildScheduled = true;
            EditorApplication.delayCall += DelayedRebuild;
        }

        private void DelayedRebuild()
        {
            _rebuildScheduled = false;
            if (this == null || _list == null) return;
            RebuildIndexNow();
        }

        internal void RebuildIndexNow()
        {
            if (_list == null) return;
            _index = QuestIndexCollector.BuildIndex();
            Revalidate();
            RefreshList();
            RefreshDetail();
            UpdateHighlighter();
        }

        /// <summary>
        /// Called by the views after an edit. Fact/structure edits schedule one debounced index rebuild
        /// (coalesced with the projectChanged it triggers); text edits only revalidate, and the detail
        /// pane is rebuilt only if the issue list changed so the user keeps focus in the next field.
        /// </summary>
        internal void OnDataEdited(bool structureChanged)
        {
            if (structureChanged)
            {
                MarkIndexDirty();
                return;
            }
            var quest = SelectedQuest;
            string before = IssueSignature(GetIssues(quest));
            Revalidate();
            RefreshList();
            if (_factsMode || IssueSignature(GetIssues(quest)) != before) RefreshDetail();
        }

        private static string IssueSignature(List<ValidationIssue> issues) =>
            string.Join("\n", issues.Select(i => i.ToString()));

        private void Revalidate()
        {
            _issues.Clear();
            if (_index == null) return;
            int errors = 0, warnings = 0;
            foreach (var quest in _index.Quests)
            {
                var issues = QuestValidator.Validate(quest, _index, _index.Quests);
                _issues[quest] = issues;
                errors += issues.Count(i => i.Severity == IssueSeverity.Error);
                warnings += issues.Count(i => i.Severity == IssueSeverity.Warning);
            }
            _summary.text = $"{errors} error{(errors == 1 ? "" : "s")} · {warnings} warning{(warnings == 1 ? "" : "s")}";
        }

        internal List<ValidationIssue> GetIssues(QuestSO quest) =>
            quest != null && _issues.TryGetValue(quest, out var list) ? list : new List<ValidationIssue>();

        // ── Selection / mode ──────────────────────────────────────────────────

        private QuestSO SelectedQuest => LoadByGuid<QuestSO>(_selectedQuestGuid);

        private Fact SelectedFact => LoadByGuid<Fact>(_selectedFactGuid);

        internal void SelectQuest(QuestSO quest)
        {
            _selectedQuestGuid = GuidOf(quest);
            _factsMode = false;
            ApplyModeAndRefresh();
        }

        internal void SelectFact(Fact fact)
        {
            _selectedFactGuid = GuidOf(fact);
            _factsMode = true;
            ApplyModeAndRefresh();
        }

        private void SetMode(bool facts)
        {
            _factsMode = facts;
            ApplyModeAndRefresh();
        }

        private void ApplyModeAndRefresh()
        {
            if (_list == null) return; // CreateGUI will pick up the serialized selection
            _questsToggle.SetValueWithoutNotify(!_factsMode);
            _factsToggle.SetValueWithoutNotify(_factsMode);
            RefreshList();
            RefreshDetail();
            UpdateHighlighter();
        }

        private void OnListSelectionChanged(IEnumerable<object> selection)
        {
            switch (selection.FirstOrDefault())
            {
                case QuestSO quest:
                    _selectedQuestGuid = GuidOf(quest);
                    break;
                case Fact fact:
                    _selectedFactGuid = GuidOf(fact);
                    break;
                default:
                    return; // header row — keep the current detail
            }
            RefreshDetail();
            UpdateHighlighter();
        }

        // ── List ──────────────────────────────────────────────────────────────

        private void RefreshList()
        {
            if (_list == null) return;
            _rows.Clear();
            if (_index != null)
            {
                if (_factsMode)
                {
                    foreach (var group in _index.AllFacts.Where(MatchesSearch).GroupBy(f => f.GetType().Name).OrderBy(g => g.Key))
                    {
                        _rows.Add($"{group.Key} ({group.Count()})");
                        _rows.AddRange(group.OrderBy(f => f.name));
                    }
                }
                else
                {
                    _rows.AddRange(_index.Quests.Where(MatchesSearch).OrderBy(QuestReferenceIndex.QuestDisplayName));
                }
            }
            _list.RefreshItems();

            object selected = _factsMode ? SelectedFact : SelectedQuest;
            int selectedIndex = selected != null ? _rows.IndexOf(selected) : -1;
            if (selectedIndex >= 0) _list.SetSelectionWithoutNotify(new[] { selectedIndex });
            else _list.ClearSelection();
        }

        private bool MatchesSearch(QuestSO quest) =>
            string.IsNullOrEmpty(_search) || Contains(quest.name) || Contains(quest.title) || Contains(quest.questId);

        private bool MatchesSearch(Fact fact) => string.IsNullOrEmpty(_search) || Contains(fact.name);

        private bool Contains(string value) =>
            !string.IsNullOrEmpty(value) && value.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) >= 0;

        private static VisualElement MakeRow()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, paddingLeft = 4, paddingRight = 4 } };
            var dot = new VisualElement { name = "dot" };
            dot.style.width = dot.style.height = 8;
            dot.style.borderTopLeftRadius = dot.style.borderTopRightRadius = 4;
            dot.style.borderBottomLeftRadius = dot.style.borderBottomRightRadius = 4;
            dot.style.marginRight = 6;
            row.Add(dot);
            row.Add(new Label { name = "name", style = { flexGrow = 1, overflow = Overflow.Hidden } });
            row.Add(new Label { name = "tag", style = { color = ExplorerStyles.MutedText, fontSize = 10 } });
            return row;
        }

        private void BindRow(VisualElement row, int i)
        {
            var dot = row.Q("dot");
            var name = row.Q<Label>("name");
            var tag = row.Q<Label>("tag");
            object item = i >= 0 && i < _rows.Count ? _rows[i] : null;

            name.style.unityFontStyleAndWeight = FontStyle.Normal;
            dot.style.visibility = Visibility.Visible;
            tag.text = string.Empty;

            switch (item)
            {
                case string header:
                    name.text = header;
                    name.style.unityFontStyleAndWeight = FontStyle.Bold;
                    dot.style.visibility = Visibility.Hidden;
                    break;
                case QuestSO quest:
                    name.text = QuestReferenceIndex.QuestDisplayName(quest);
                    dot.style.backgroundColor = ExplorerStyles.SeverityColor(GetIssues(quest));
                    if (EditorApplication.isPlaying && WorldStateManager.Instance != null)
                        tag.text = QuestStateTag(quest);
                    break;
                case Fact fact:
                    name.text = fact.name;
                    int setters = _index?.GetSetters(fact).Count ?? 0;
                    int readers = _index?.GetReaders(fact).Count ?? 0;
                    bool orphan = QuestValidator.IsStored(fact) && setters == 0;
                    dot.style.backgroundColor = orphan ? ExplorerStyles.ErrorColor : ExplorerStyles.OkColor;
                    tag.text = $"S:{setters} R:{readers}";
                    break;
                default:
                    name.text = string.Empty;
                    dot.style.visibility = Visibility.Hidden;
                    break;
            }
        }

        // ── Detail ────────────────────────────────────────────────────────────

        private void RefreshDetail()
        {
            if (_detail == null) return;
            Vector2 offset = _detail.scrollOffset;
            _detail.Clear();
            _questView = null;
            _factView = null;

            if (_index == null)
            {
                _detail.Add(new Label("Building index…"));
                return;
            }

            if (_factsMode)
            {
                var fact = SelectedFact;
                if (fact != null) _detail.Add(_factView = new FactDetailView(this, fact));
                else _detail.Add(new Label("Select a fact."));
            }
            else
            {
                var quest = SelectedQuest;
                if (quest != null) _detail.Add(_questView = new QuestDetailView(this, quest, GetIssues(quest)));
                else _detail.Add(new Label("Select a quest."));
            }

            _detail.schedule.Execute(() => _detail.scrollOffset = offset);
            if (EditorApplication.isPlaying) UpdateLive();
        }

        internal void ScrollDetailTo(VisualElement element)
        {
            if (_detail == null || element == null) return;
            _detail.schedule.Execute(() => _detail.ScrollTo(element));
        }

        // ── Play mode ─────────────────────────────────────────────────────────

        internal void UpdateLive()
        {
            if (_banner == null) return;
            if (!EditorApplication.isPlaying)
            {
                _banner.style.display = DisplayStyle.None;
                return;
            }

            var wsm = WorldStateManager.Instance;
            _banner.style.display = wsm == null ? DisplayStyle.Flex : DisplayStyle.None;
            if (wsm == null) return;

            if (!_factsMode) _list.RefreshItems(); // state tags
            _questView?.UpdateLive(wsm);
            _factView?.UpdateLive(wsm);
            if (QuestSceneHighlighter.IsEnabled) SceneView.RepaintAll();
        }

        /// <summary>Failed &gt; Completed &gt; Active &gt; Not started. Requires a running WorldStateManager.</summary>
        internal static string QuestStateTag(QuestSO quest)
        {
            if (quest.IsFailed) return "Failed";
            if (quest.IsCompleted) return "Completed";
            if (quest.IsStarted) return "Active";
            return "Not started";
        }

        // ── Scene ─────────────────────────────────────────────────────────────

        private void UpdateHighlighter()
        {
            var quest = _factsMode ? null : SelectedQuest;
            if (_highlight && _index != null && quest != null) QuestSceneHighlighter.Enable(_index, quest);
            else QuestSceneHighlighter.Disable();
        }

        /// <summary>Opens a closed scene additively (after the save prompt) and selects the PersistentID bound to the link's fact.</summary>
        internal void OpenSceneAndSelect(FactLink link)
        {
            if (link == null || string.IsNullOrEmpty(link.ScenePath) || EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.OpenScene(link.ScenePath, OpenSceneMode.Additive);
            RebuildIndexNow();

            var setter = _index.GetSetters(link.Fact).FirstOrDefault(s =>
                s.Source == FactLinkSource.ScenePersistentID && s.ScenePath == link.ScenePath && s.Owner != null);
            if (setter == null)
            {
                GameLog.Warn(TAG, $"'{link.Fact.name}' not found in '{link.ScenePath}' after opening it");
                return;
            }
            LinkRowFactory.Frame(setter.Owner);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string GuidOf(Object asset) =>
            asset == null ? null : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));

        private static T LoadByGuid<T>(string guid) where T : Object =>
            string.IsNullOrEmpty(guid) ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
    }
}
