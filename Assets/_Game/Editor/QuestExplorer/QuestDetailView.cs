using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Dialogue;
using Game.NPC;
using Game.Quest;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Editor.QuestExplorer
{
    /// <summary>
    /// Right pane for a quest: header, issues, Start / Steps / Completed / Failed with part rows and fact links,
    /// and the NPC memories that read or set the quest's facts.
    /// </summary>
    internal sealed class QuestDetailView : VisualElement
    {
        private readonly QuestExplorerWindow _host;
        private readonly QuestSO _quest;
        private readonly QuestReferenceIndex _index;
        private readonly string _keyPrefix;

        private readonly Dictionary<QuestPartLocation, VisualElement> _locationRows = new Dictionary<QuestPartLocation, VisualElement>();
        private readonly Dictionary<QuestPartLocation, Foldout> _locationFoldouts = new Dictionary<QuestPartLocation, Foldout>();
        private readonly List<LiveFactBadge> _liveBadges = new List<LiveFactBadge>();
        private readonly List<(int stepIndex, Label label)> _stepStates = new List<(int stepIndex, Label label)>();
        private readonly Dictionary<NPCMemoryEntrySO, VisualElement> _memoryRows = new Dictionary<NPCMemoryEntrySO, VisualElement>();
        private readonly Dictionary<NPCMemoryEntrySO, Foldout> _memoryFoldouts = new Dictionary<NPCMemoryEntrySO, Foldout>();
        private readonly List<(NPCMemoryEntrySO memory, Label badge)> _memoryStates = new List<(NPCMemoryEntrySO memory, Label badge)>();
        private Foldout _memoriesSection;
        private const long DUPLICATE_NOTICE_MS = 2000;
        private Label _stateTag;

        public QuestDetailView(QuestExplorerWindow host, QuestSO quest, List<ValidationIssue> issues)
        {
            _host = host;
            _quest = quest;
            _index = host.Index;
            _keyPrefix = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(quest));

            BuildHeader();
            BuildIssues(issues);
            BuildSections();
        }

        // ── Header ────────────────────────────────────────────────────────────

        private void BuildHeader()
        {
            var title = DelayedText(null, _quest.title, false, v => Edit(QuestEditActions.SetTitle(_quest, v), false));
            title.style.fontSize = 16;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            Add(title);

            var idRow = ExplorerStyles.Row();
            idRow.Add(new Label($"questId: {(string.IsNullOrEmpty(_quest.questId) ? "(empty)" : _quest.questId)}") { style = { marginRight = 8 } });
            var asset = ExplorerStyles.ReadOnlyObjectField(_quest, typeof(QuestSO));
            asset.style.flexGrow = 1;
            idRow.Add(asset);
            _stateTag = new Label { style = { display = DisplayStyle.None, marginLeft = 8, unityFontStyleAndWeight = FontStyle.Bold } };
            idRow.Add(_stateTag);
            Add(idRow);

            var chips = ExplorerStyles.Row();
            chips.style.marginTop = 4;
            chips.Add(Chip("EventsManager", _index.IsInEventsManager(_quest), () =>
            {
                QuestEditActions.SyncEventsManager();
                _host.RebuildIndexNow();
            }));
            chips.Add(Chip("QuestLog", _index.IsInQuestLog(_quest), () =>
            {
                QuestEditActions.AddToQuestLog(_quest);
                _host.RebuildIndexNow();
            }));
            Add(chips);

            Add(DelayedText("Description", _quest.description, true, v => Edit(QuestEditActions.SetDescription(_quest, v), true)));
        }

        private static VisualElement Chip(string name, bool ok, Action fix)
        {
            var chip = ExplorerStyles.Row();
            chip.style.marginRight = 8;
            chip.Add(new Label($"{name} {(ok ? "✓" : "✗")}") { style = { color = ok ? ExplorerStyles.OkColor : ExplorerStyles.ErrorColor } });
            if (!ok) chip.Add(new Button(fix) { text = "Fix" });
            return chip;
        }

        // ── Issues ────────────────────────────────────────────────────────────

        private void BuildIssues(List<ValidationIssue> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                Add(new HelpBox("No issues.", HelpBoxMessageType.Info));
                return;
            }
            foreach (var issue in issues)
            {
                var box = new HelpBox(issue.Message, ExplorerStyles.ToHelpBoxType(issue.Severity))
                {
                    tooltip = issue.Location.HasValue || issue.Context != null ? "Click to locate" : null
                };
                var captured = issue;
                box.RegisterCallback<ClickEvent>(_ => OnIssueClicked(captured));
                Add(box);
            }
        }

        private void OnIssueClicked(ValidationIssue issue)
        {
            if (issue.Context != null) EditorGUIUtility.PingObject(issue.Context);
            if (issue.Context is NPCMemoryEntrySO memory && _memoryRows.TryGetValue(memory, out var memoryRow))
            {
                if (_memoriesSection != null) _memoriesSection.value = true;
                if (_memoryFoldouts.TryGetValue(memory, out var npcFoldout)) npcFoldout.value = true;
                _host.ScrollDetailTo(memoryRow);
                ExplorerStyles.Flash(memoryRow);
                return;
            }
            if (!issue.Location.HasValue || !_locationRows.TryGetValue(issue.Location.Value, out var row)) return;

            if (_locationFoldouts.TryGetValue(issue.Location.Value, out var foldout)) foldout.value = true;
            _host.ScrollDetailTo(row);
            ExplorerStyles.Flash(row);
        }

        // ── Sections ──────────────────────────────────────────────────────────

        private void BuildSections()
        {
            var start = Section("Start", "start");
            start.Add(PartRow(QuestPartLocation.Start, _quest.startPart, start));
            Add(start);

            var steps = _quest.steps ?? new List<QuestStep>();
            for (int i = 0; i < steps.Count; i++) Add(StepSection(i, steps[i], steps.Count));

            var addStep = new Button(() => Edit(QuestEditActions.AddStep(_quest), true)) { text = "＋ Step" };
            addStep.style.alignSelf = Align.FlexStart;
            Add(addStep);

            Add(PartListSection("Completed", "completed", QuestPartSlot.Completed, _quest.completedParts));
            Add(PartListSection("Failed", "failed", QuestPartSlot.Failed, _quest.failedParts));
            Add(MemoriesSection());
        }

        private Foldout StepSection(int i, QuestStep step, int stepCount)
        {
            string title = string.IsNullOrEmpty(step.title) ? "(no title)" : step.title;
            var foldout = Section($"Step {i + 1} \"{title}\"", $"step{i}");
            var stepLoc = QuestPartLocation.Step(i);
            _locationRows[stepLoc] = foldout;
            _locationFoldouts[stepLoc] = foldout;

            var controls = ExplorerStyles.Row();
            var titleField = DelayedText("Title", step.title, false, v => Edit(QuestEditActions.SetStepTitle(_quest, i, v), false));
            titleField.style.flexGrow = 1;
            controls.Add(titleField);
            var state = new Label { style = { display = DisplayStyle.None, marginLeft = 6, unityFontStyleAndWeight = FontStyle.Bold } };
            _stepStates.Add((i, state));
            controls.Add(state);

            var up = new Button(() => Edit(QuestEditActions.MoveStep(_quest, i, true, _index), true)) { text = "▲", tooltip = "Move step up" };
            up.SetEnabled(i > 0);
            var down = new Button(() => Edit(QuestEditActions.MoveStep(_quest, i, false, _index), true)) { text = "▼", tooltip = "Move step down" };
            down.SetEnabled(i < stepCount - 1);
            controls.Add(up);
            controls.Add(down);
            controls.Add(new Button(() => Edit(QuestEditActions.RemoveStep(_quest, i, _index), true)) { text = "✕", tooltip = "Remove step" });
            foldout.Add(controls);

            foldout.Add(DelayedText("Description", step.description, true, v => Edit(QuestEditActions.SetStepDescription(_quest, i, v), false)));

            var parts = step.parts ?? new List<QuestPart>();
            for (int j = 0; j < parts.Count; j++)
                foldout.Add(PartRow(QuestPartLocation.StepPart(i, j), parts[j], foldout));
            foldout.Add(AddPartButton(QuestPartSlot.Step, i));
            return foldout;
        }

        private Foldout PartListSection(string title, string key, QuestPartSlot slot, List<QuestPart> parts)
        {
            var foldout = Section(title, key);
            parts ??= new List<QuestPart>();
            for (int j = 0; j < parts.Count; j++)
            {
                var loc = slot == QuestPartSlot.Completed ? QuestPartLocation.Completed(j) : QuestPartLocation.Failed(j);
                foldout.Add(PartRow(loc, parts[j], foldout));
            }
            foldout.Add(AddPartButton(slot, -1));
            return foldout;
        }

        private Button AddPartButton(QuestPartSlot slot, int stepIndex)
        {
            var button = new Button(() => Edit(QuestEditActions.AddPart(_quest, slot, stepIndex), true)) { text = "＋ Part" };
            button.style.alignSelf = Align.FlexStart;
            return button;
        }

        // ── Part row ──────────────────────────────────────────────────────────

        private VisualElement PartRow(QuestPartLocation loc, QuestPart part, Foldout section)
        {
            var row = ExplorerStyles.Box();
            _locationRows[loc] = row;
            _locationFoldouts[loc] = section;

            var line = ExplorerStyles.Row();
            line.Add(new Label(loc.ToString()) { style = { minWidth = 110, unityFontStyleAndWeight = FontStyle.Bold } });

            var factField = new ObjectField { objectType = typeof(Fact), allowSceneObjects = false, value = part.fact };
            factField.style.flexGrow = 1;
            factField.style.flexShrink = 1;
            factField.RegisterValueChangedCallback(evt => Edit(QuestEditActions.SetPartFact(_quest, loc, evt.newValue as Fact), true));
            line.Add(factField);

            if (part.fact != null) line.Add(ExplorerStyles.Badge(ExplorerStyles.FactTypeName(part.fact)));

            var live = new LiveFactBadge(part.fact, _host);
            _liveBadges.Add(live);
            line.Add(live.Badge);
            line.Add(live.Toggle);

            var factMenu = new Button { text = "＋ Fact ▾", tooltip = "Create or wire a fact for this part" };
            factMenu.clicked += () => ShowFactMenu(loc, factMenu);
            line.Add(factMenu);

            bool isStart = loc.Slot == QuestPartSlot.Start;
            line.Add(new Button(() => Edit(QuestEditActions.RemovePart(_quest, loc), true))
            {
                text = isStart ? "Clear" : "✕",
                tooltip = isStart ? "Clear the start part" : "Remove part"
            });
            row.Add(line);

            row.Add(DelayedText("Entry", part.entry, true, v => Edit(QuestEditActions.SetPartEntry(_quest, loc, v), false)));

            if (part.fact == null)
            {
                row.Add(new Label("no fact") { style = { color = ExplorerStyles.ErrorColor } });
                return row;
            }

            row.Add(SetterSummary(part.fact));

            var setters = _index.GetSetters(part.fact);
            var readers = _index.GetReaders(part.fact);
            var links = MakeFoldout($"Links (S:{setters.Count} R:{readers.Count})", $"{_keyPrefix}/links/{loc}", false);
            AddLinkRows(links, _host, setters, readers, _quest);
            row.Add(links);
            return row;
        }

        private Label SetterSummary(Fact fact)
        {
            if (!QuestValidator.IsStored(fact))
            {
                string text = fact is QuestFact qf
                    ? $"computed → {QuestReferenceIndex.QuestDisplayName(qf.Quest)} · {QuestReferenceIndex.QuestFactStateLabel(qf)}"
                    : "computed";
                return new Label(text) { style = { color = ExplorerStyles.MutedText } };
            }

            var setters = _index.GetSetters(fact);
            if (setters.Count == 0)
                return new Label("never set") { style = { color = ExplorerStyles.ErrorColor, unityFontStyleAndWeight = FontStyle.Bold } };

            string summary = "set by " + string.Join(", ", setters.Take(2).Select(s => s.Label));
            if (setters.Count > 2) summary += $" (+{setters.Count - 2} more)";
            return new Label(summary) { style = { color = ExplorerStyles.MutedText, whiteSpace = WhiteSpace.Normal } };
        }

        private void ShowFactMenu(QuestPartLocation loc, VisualElement anchor)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("KilledFact from selected GameObject"), false,
                () => Edit(QuestEditActions.AssignKilledFactFromSelection(_quest, loc), true));
            menu.AddItem(new GUIContent("New DialogueFact…"), false, () => Edit(QuestEditActions.CreateDialogueFact(_quest, loc), true));
            menu.AddItem(new GUIContent("New WorldFact…"), false, () => Edit(QuestEditActions.CreateWorldFact(_quest, loc), true));
            menu.AddItem(new GUIContent("New QuestFact…"), false, () => Edit(QuestEditActions.CreateQuestFact(_quest, loc), true));
            menu.DropDown(anchor.worldBound);
        }

        // ── NPC memories ──────────────────────────────────────────────────────

        private Foldout MemoriesSection()
        {
            var memories = MemoriesToShow();
            _memoriesSection = Section($"NPC Memories ({memories.Count})", "memories");
            if (memories.Count == 0)
            {
                _memoriesSection.Add(new Label("No NPC memory reads or sets this quest's facts.") { style = { color = ExplorerStyles.MutedText } });
                return _memoriesSection;
            }

            // Grouped by NPC reference (GroupBy keeps first-appearance order: NPC name, no NPC last).
            foreach (var group in memories.GroupBy(im => im.Npc))
            {
                var npc = group.Key;
                var npcFoldout = MakeFoldout(QuestReferenceIndex.NpcDisplayName(npc),
                    $"{_keyPrefix}/memnpc/{(npc != null ? npc.name : "none")}", true);
                foreach (var go in _index.GetSceneObjectsForNpc(npc))
                    npcFoldout.Add(LinkRowFactory.BuildSceneObject(go));
                foreach (var im in group) npcFoldout.Add(MemoryRow(im, npcFoldout));
                _memoriesSection.Add(npcFoldout);
            }
            return _memoriesSection;
        }

        /// <summary>
        /// Involved memories, plus memories edited here this session that are no longer involved —
        /// so an edit that removes the last quest link doesn't make the row vanish mid-edit.
        /// </summary>
        private List<InvolvedMemory> MemoriesToShow()
        {
            var result = _index.GetInvolvedMemories(_quest).ToList();
            if (!_host.EditedMemories.TryGetValue(_quest, out var edited)) return result;
            foreach (var memory in edited)
            {
                if (memory == null || result.Any(im => im.Memory == memory)) continue;
                var stale = new InvolvedMemory { Memory = memory, Npc = _index.GetMemoryOwner(memory) };
                stale.ReasonLabels.Add("edited — no longer involved");
                result.Add(stale);
            }
            return result;
        }

        private VisualElement MemoryRow(InvolvedMemory im, Foldout npcFoldout)
        {
            var memory = im.Memory;
            var row = ExplorerStyles.Box();
            _memoryRows[memory] = row;
            _memoryFoldouts[memory] = npcFoldout;

            var header = ExplorerStyles.Row();
            var field = ExplorerStyles.ReadOnlyObjectField(memory, typeof(NPCMemoryEntrySO));
            field.style.flexGrow = 1;
            field.style.flexShrink = 1;
            header.Add(field);
            foreach (var label in im.ReasonLabels) header.Add(ExplorerStyles.Badge(label));
            var state = ExplorerStyles.Badge(string.Empty);
            state.style.display = DisplayStyle.None;
            _memoryStates.Add((memory, state));
            header.Add(state);
            row.Add(header);

            AddConditionList(row, memory, MemoryConditionList.Unlock, "Unlock — all must be true", memory.unlockConditions);
            AddConditionList(row, memory, MemoryConditionList.Invalidation, "Invalidation — any closes it", memory.invalidationConditions);

            row.Add(SubHeading("Start dialogue"));
            var startDialog = memory.effects?.startdialog;
            row.Add(startDialog != null
                ? (VisualElement)ExplorerStyles.ReadOnlyObjectField(startDialog, typeof(StartDialogueNode))
                : MutedLabel("none"));

            var gates = _index.GetGates(memory);
            row.Add(SubHeading($"Gates choices ({gates.Count})"));
            if (gates.Count == 0) row.Add(MutedLabel("none"));
            foreach (var gate in gates)
            {
                var gateRow = ExplorerStyles.Row();
                var node = ExplorerStyles.ReadOnlyObjectField(gate.Node, typeof(DialogueNode));
                node.style.minWidth = 160;
                gateRow.Add(node);
                gateRow.Add(new Label(gate.Label) { style = { flexGrow = 1, flexShrink = 1, whiteSpace = WhiteSpace.Normal, marginLeft = 4 } });
                row.Add(gateRow);
            }
            return row;
        }

        private void AddConditionList(VisualElement row, NPCMemoryEntrySO memory, MemoryConditionList list, string title, Fact[] facts)
        {
            row.Add(SubHeading(title));
            if (facts != null)
                for (int i = 0; i < facts.Length; i++) row.Add(ConditionRow(row, memory, list, i, facts[i]));
            row.Add(AddConditionField(row, memory, list));
        }

        /// <summary>
        /// After a successful edit, locks the memory row until the debounced rebuild redraws it: its
        /// controls captured array indices that the edit may have shifted.
        /// </summary>
        private bool MemoryEdit(VisualElement row, NPCMemoryEntrySO memory, bool changed)
        {
            if (!changed) return false;
            if (!_host.EditedMemories.TryGetValue(_quest, out var edited))
                _host.EditedMemories[_quest] = edited = new HashSet<NPCMemoryEntrySO>();
            edited.Add(memory);
            row.SetEnabled(false);
            Edit(true, true);
            return true;
        }

        private VisualElement ConditionRow(VisualElement row, NPCMemoryEntrySO memory, MemoryConditionList list, int i, Fact fact)
        {
            var line = ExplorerStyles.Row();
            line.style.paddingLeft = 12;

            var field = new ObjectField { objectType = typeof(Fact), allowSceneObjects = false, value = fact };
            field.style.flexGrow = 1;
            field.style.flexShrink = 1;
            field.RegisterValueChangedCallback(evt =>
            {
                // None removes the slot instead of leaving a null (V14); a fact already listed is rejected.
                bool changed = evt.newValue == null
                    ? QuestEditActions.RemoveMemoryCondition(memory, list, i)
                    : QuestEditActions.SetMemoryCondition(memory, list, i, evt.newValue as Fact);
                if (!MemoryEdit(row, memory, changed)) field.SetValueWithoutNotify(evt.previousValue);
            });
            line.Add(field);

            if (fact == null)
            {
                line.Add(new Label("missing — skipped at runtime") { style = { color = ExplorerStyles.ErrorColor, marginLeft = 4 } });
            }
            else
            {
                var loc = QuestLocationOf(fact);
                line.Add(loc.HasValue ? ExplorerStyles.Badge(loc.Value.ToString()) : MutedLabel("outside quest"));

                var live = new LiveFactBadge(fact, _host);
                _liveBadges.Add(live);
                line.Add(live.Badge);
                line.Add(live.Toggle);
            }

            line.Add(new Button(() => MemoryEdit(row, memory, QuestEditActions.RemoveMemoryCondition(memory, list, i)))
            {
                text = "✕", tooltip = "Remove condition"
            });
            return line;
        }

        /// <summary>Trailing empty field — conditions are appended, never inserted as null slots (V14).</summary>
        private VisualElement AddConditionField(VisualElement row, NPCMemoryEntrySO memory, MemoryConditionList list)
        {
            var line = ExplorerStyles.Row();
            line.style.paddingLeft = 12;
            var field = new ObjectField("+ add") { objectType = typeof(Fact), allowSceneObjects = false };
            field.style.flexGrow = 1;
            var notice = new Label("already listed") { style = { color = ExplorerStyles.WarnColor, marginLeft = 4, display = DisplayStyle.None } };
            field.RegisterValueChangedCallback(evt =>
            {
                if (!(evt.newValue is Fact fact)) return;
                if (MemoryEdit(row, memory, QuestEditActions.AddMemoryCondition(memory, list, fact))) return;
                field.SetValueWithoutNotify(null);
                notice.style.display = DisplayStyle.Flex;
                notice.schedule.Execute(() => notice.style.display = DisplayStyle.None).StartingIn(DUPLICATE_NOTICE_MS);
            });
            line.Add(field);
            line.Add(notice);
            return line;
        }

        private QuestPartLocation? QuestLocationOf(Fact fact)
        {
            foreach (var (loc, part) in QuestReferenceIndex.EnumerateParts(_quest))
                if (part.fact == fact) return loc;
            return null;
        }

        private static Label SubHeading(string text) =>
            new Label(text) { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 2 } };

        private static Label MutedLabel(string text) =>
            new Label(text) { style = { color = ExplorerStyles.MutedText, paddingLeft = 12 } };

        // ── Shared link rows ──────────────────────────────────────────────────

        internal static void AddLinkRows(VisualElement parent, QuestExplorerWindow host,
            IReadOnlyList<FactLink> setters, IReadOnlyList<FactLink> readers, QuestSO currentQuest)
        {
            parent.Add(new Label($"Setters ({setters.Count})") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 2 } });
            foreach (var link in setters)
            {
                parent.Add(LinkRowFactory.Build(link, host, currentQuest));
                if (link.Npc == null) continue;
                foreach (var go in host.Index.GetSceneObjectsForNpc(link.Npc))
                    parent.Add(LinkRowFactory.BuildSceneObject(go));
            }
            parent.Add(new Label($"Readers ({readers.Count})") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 2 } });
            foreach (var link in readers) parent.Add(LinkRowFactory.Build(link, host, currentQuest));
        }

        // ── Live ──────────────────────────────────────────────────────────────

        public void UpdateLive(WorldStateManager wsm)
        {
            _stateTag.text = QuestExplorerWindow.QuestStateTag(_quest);
            _stateTag.style.display = DisplayStyle.Flex;
            foreach (var badge in _liveBadges) badge.Update(wsm);
            foreach (var (i, label) in _stepStates)
            {
                if (_quest.steps == null || i >= _quest.steps.Count) continue;
                string state = _quest.IsStepCompleted(i) ? "Done" : _quest.steps[i].IsActive() ? "Active" : "Pending";
                label.text = state;
                label.style.color = state == "Done" ? ExplorerStyles.OkColor : state == "Active" ? ExplorerStyles.WarnColor : ExplorerStyles.MutedText;
                label.style.display = DisplayStyle.Flex;
            }

            foreach (var (memory, badge) in _memoryStates)
            {
                if (memory == null) continue;
                // Unevaluable facts count as false, so polling never logs warnings.
                var state = QuestReferenceIndex.EvaluateMemory(memory,
                    f => QuestValidator.IsEvaluable(f) && wsm.GetFact(f),
                    d => wsm.IsDialoguePlayed(d));
                switch (state)
                {
                    case MemoryLiveState.Active:
                        badge.text = "active";
                        badge.style.backgroundColor = ExplorerStyles.OkColor;
                        break;
                    case MemoryLiveState.ActiveDialoguePlayed:
                        badge.text = "active · dialogue played";
                        badge.style.backgroundColor = ExplorerStyles.WarnColor;
                        break;
                    case MemoryLiveState.Invalidated:
                        badge.text = "invalidated";
                        badge.style.backgroundColor = ExplorerStyles.MutedText;
                        break;
                    default:
                        badge.text = "locked";
                        badge.style.backgroundColor = ExplorerStyles.BadgeBackground;
                        break;
                }
                badge.style.display = DisplayStyle.Flex;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void Edit(bool changed, bool structureChanged)
        {
            if (changed) _host.OnDataEdited(structureChanged);
        }

        private Foldout Section(string text, string key)
        {
            var foldout = MakeFoldout(text, $"{_keyPrefix}/section/{key}", true);
            foldout.style.marginTop = 6;
            foldout.Q<Toggle>().style.unityFontStyleAndWeight = FontStyle.Bold;
            return foldout;
        }

        private Foldout MakeFoldout(string text, string key, bool defaultOpen) => ExplorerStyles.PersistentFoldout(_host, text, key, defaultOpen);

        private static TextField DelayedText(string label, string value, bool multiline, Action<string> onChanged)
        {
            var field = new TextField(label) { value = value ?? string.Empty, isDelayed = true, multiline = multiline };
            if (multiline) field.style.whiteSpace = WhiteSpace.Normal;
            field.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue != evt.previousValue) onChanged(evt.newValue);
            });
            return field;
        }
    }

    /// <summary>Right pane for a single fact: key, stored/computed, live value, setters and readers.</summary>
    internal sealed class FactDetailView : VisualElement
    {
        private readonly LiveFactBadge _live;

        public FactDetailView(QuestExplorerWindow host, Fact fact)
        {
            var index = host.Index;

            var title = new Label(fact.name) { style = { fontSize = 16, unityFontStyleAndWeight = FontStyle.Bold } };
            Add(title);
            Add(ExplorerStyles.ReadOnlyObjectField(fact, typeof(Fact)));

            var info = ExplorerStyles.Row();
            info.Add(ExplorerStyles.Badge(ExplorerStyles.FactTypeName(fact)));
            info.Add(ExplorerStyles.Badge(QuestValidator.IsStored(fact) ? "stored" : "computed"));
            info.Add(new Label($"Key: {fact}") { style = { marginLeft = 6 } });
            _live = new LiveFactBadge(fact, host);
            info.Add(_live.Badge);
            info.Add(_live.Toggle);
            Add(info);

            if (fact is QuestFact qf)
            {
                var target = ExplorerStyles.Row();
                target.Add(new Label($"Targets: {QuestReferenceIndex.QuestDisplayName(qf.Quest)} · {QuestReferenceIndex.QuestFactStateLabel(qf)}"));
                if (qf.Quest != null) target.Add(new Button(() => host.SelectQuest(qf.Quest)) { text = "Show" });
                Add(target);
            }

            var setters = index.GetSetters(fact);
            if (QuestValidator.IsStored(fact) && setters.Count == 0)
            {
                string msg = $"'{fact.name}' is never set.";
                if (fact is WorldFact) msg += " No WorldFact setter exists in code yet.";
                Add(new HelpBox(msg, HelpBoxMessageType.Error));
            }

            QuestDetailView.AddLinkRows(this, host, setters, index.GetReaders(fact), null);
        }

        public void UpdateLive(WorldStateManager wsm) => _live.Update(wsm);
    }

    /// <summary>Play-mode value badge + raw toggle button for one fact. Hidden in edit mode.</summary>
    internal sealed class LiveFactBadge
    {
        private const string TOGGLE_TOOLTIP = "Raw set — fires OnFactChanged, no XP/rewards, does not despawn entities.";

        private readonly Fact _fact;
        private readonly QuestExplorerWindow _host;
        public readonly Label Badge;
        public readonly Button Toggle;

        public LiveFactBadge(Fact fact, QuestExplorerWindow host)
        {
            _fact = fact;
            _host = host;
            Badge = ExplorerStyles.Badge(string.Empty);
            Badge.style.display = DisplayStyle.None;
            Toggle = new Button(OnToggle) { text = "Toggle", tooltip = TOGGLE_TOOLTIP };
            Toggle.style.display = DisplayStyle.None;
        }

        public void Update(WorldStateManager wsm)
        {
            if (_fact == null || wsm == null) return;
            if (!QuestValidator.IsEvaluable(_fact))
            {
                Badge.text = "invalid";
                Badge.style.backgroundColor = ExplorerStyles.ErrorColor;
                Badge.style.display = DisplayStyle.Flex;
                return;
            }
            bool value = wsm.GetFact(_fact);
            Badge.text = value ? "true" : "false";
            Badge.style.backgroundColor = value ? ExplorerStyles.OkColor : ExplorerStyles.BadgeBackground;
            Badge.style.display = DisplayStyle.Flex;
            // Computed facts (QuestFact / SkillFact / StatFact) are read-only.
            Toggle.style.display = QuestValidator.IsStored(_fact) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnToggle()
        {
            var wsm = WorldStateManager.Instance;
            if (wsm == null || _fact == null) return;
            wsm.SetFact(_fact, !wsm.GetFact(_fact));
            _host.UpdateLive();
        }
    }

    /// <summary>Builds link rows (Ping / Select / Frame / Open scene / Show) shared by the quest and fact views.</summary>
    internal static class LinkRowFactory
    {
        public static VisualElement Build(FactLink link, QuestExplorerWindow host, QuestSO currentQuest)
        {
            var row = ExplorerStyles.Row();
            row.style.paddingLeft = 12;
            row.Add(new Label(SourceTag(link.Source)) { style = { minWidth = 64, color = ExplorerStyles.MutedText } });

            bool missing = link.Source != FactLinkSource.ClosedScene && link.Owner == null;
            string text = missing ? $"{link.Label} — object no longer loaded" : link.Label;
            row.Add(new Label(text) { style = { flexGrow = 1, flexShrink = 1, whiteSpace = WhiteSpace.Normal } });
            if (missing) return row;

            if (link.Source == FactLinkSource.ClosedScene)
            {
                var open = new Button(() => host.OpenSceneAndSelect(link)) { text = "Open scene & select" };
                open.SetEnabled(!EditorApplication.isPlaying);
                row.Add(open);
                return row;
            }

            AddObjectButtons(row, link.Owner);
            if (link.Owner is QuestSO quest && quest != currentQuest)
                row.Add(new Button(() => host.SelectQuest(quest)) { text = "Show" });
            return row;
        }

        /// <summary>Sub-row for an NPC scene object under a DialogueFact setter.</summary>
        public static VisualElement BuildSceneObject(GameObject go)
        {
            var row = ExplorerStyles.Row();
            row.style.paddingLeft = 28;
            if (go == null)
            {
                row.Add(new Label("↳ object no longer loaded") { style = { color = ExplorerStyles.MutedText } });
                return row;
            }
            row.Add(new Label($"↳ {go.name} [{go.scene.name}]") { style = { flexGrow = 1 } });
            AddObjectButtons(row, go);
            return row;
        }

        private static void AddObjectButtons(VisualElement row, Object owner)
        {
            row.Add(new Button(() => Ping(owner)) { text = "Ping" });
            row.Add(new Button(() => Select(owner)) { text = "Select" });
            if (IsSceneObject(owner)) row.Add(new Button(() => Frame(owner)) { text = "Frame" });
        }

        public static void Ping(Object owner)
        {
            if (owner != null) EditorGUIUtility.PingObject(AsGameObject(owner) ?? owner);
        }

        public static void Select(Object owner)
        {
            if (owner == null) return;
            var go = AsGameObject(owner);
            Selection.activeObject = go != null ? go : owner;
        }

        public static void Frame(Object owner)
        {
            if (!IsSceneObject(owner)) return;
            Select(owner);
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private static bool IsSceneObject(Object owner)
        {
            var go = AsGameObject(owner);
            return go != null && go.scene.IsValid() && !EditorUtility.IsPersistent(go);
        }

        private static GameObject AsGameObject(Object owner) => owner switch
        {
            Component c when c != null => c.gameObject,
            GameObject g when g != null => g,
            _ => null
        };

        private static string SourceTag(FactLinkSource source) => source switch
        {
            FactLinkSource.ScenePersistentID => "[Scene]",
            FactLinkSource.PrefabPersistentID => "[Prefab]",
            FactLinkSource.ClosedScene => "[Closed]",
            FactLinkSource.StartDialogueNode => "[Start]",
            FactLinkSource.ChoiceOption => "[Choice]",
            FactLinkSource.QuestPart => "[Quest]",
            FactLinkSource.MemoryUnlock => "[Unlock]",
            FactLinkSource.MemoryInvalidation => "[Invalid.]",
            FactLinkSource.PlayerReward => "[Reward]",
            FactLinkSource.QuestFactTarget => "[Target]",
            _ => source.ToString()
        };
    }

    /// <summary>Inline styles and small element factories (no USS files, like the other editor tools).</summary>
    internal static class ExplorerStyles
    {
        public static readonly Color ErrorColor = new Color(0.9f, 0.3f, 0.3f);
        public static readonly Color WarnColor = new Color(0.95f, 0.75f, 0.2f);
        public static readonly Color InfoColor = new Color(0.4f, 0.65f, 0.95f);
        public static readonly Color OkColor = new Color(0.3f, 0.75f, 0.35f);
        public static readonly Color MutedText = new Color(0.6f, 0.6f, 0.6f);
        public static readonly Color BadgeBackground = new Color(0.3f, 0.3f, 0.3f);
        public static readonly Color BoxBackground = new Color(0f, 0f, 0f, 0.12f);
        private static readonly Color FlashColor = new Color(1f, 0.8f, 0.2f, 0.35f);
        private const long FLASH_MS = 700;

        public static VisualElement Row() =>
            new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, flexWrap = Wrap.Wrap } };

        public static VisualElement Box()
        {
            var box = new VisualElement();
            box.style.backgroundColor = BoxBackground;
            box.style.marginTop = 3;
            box.style.marginBottom = 3;
            box.style.paddingLeft = box.style.paddingRight = 4;
            box.style.paddingTop = box.style.paddingBottom = 3;
            box.style.borderTopLeftRadius = box.style.borderTopRightRadius = 3;
            box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = 3;
            return box;
        }

        public static Label Badge(string text)
        {
            var badge = new Label(text);
            badge.style.backgroundColor = BadgeBackground;
            badge.style.color = Color.white;
            badge.style.fontSize = 10;
            badge.style.paddingLeft = badge.style.paddingRight = 4;
            badge.style.marginLeft = badge.style.marginRight = 2;
            badge.style.borderTopLeftRadius = badge.style.borderTopRightRadius = 3;
            badge.style.borderBottomLeftRadius = badge.style.borderBottomRightRadius = 3;
            return badge;
        }

        /// <summary>Object field that pings on click but cannot be reassigned.</summary>
        public static ObjectField ReadOnlyObjectField(Object value, Type type)
        {
            var field = new ObjectField { objectType = type, allowSceneObjects = false, value = value };
            field.RegisterValueChangedCallback(_ => field.SetValueWithoutNotify(value));
            return field;
        }

        public static Foldout PersistentFoldout(QuestExplorerWindow host, string text, string key, bool defaultOpen)
        {
            var foldout = new Foldout { text = text };
            foldout.value = host.FoldoutStates.TryGetValue(key, out bool open) ? open : defaultOpen;
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target == foldout) host.FoldoutStates[key] = evt.newValue; // ignore bubbling child toggles
            });
            return foldout;
        }

        public static void Flash(VisualElement element)
        {
            var previous = element.style.backgroundColor;
            element.style.backgroundColor = FlashColor;
            element.schedule.Execute(() => element.style.backgroundColor = previous).StartingIn(FLASH_MS);
        }

        public static string FactTypeName(Fact fact) => fact switch
        {
            KilledFact => "Killed",
            DialogueFact => "Dialogue",
            WorldFact => "World",
            QuestFact => "Quest",
            SkillFact => "Skill",
            StatFact => "Stat",
            null => "-",
            _ => fact.GetType().Name
        };

        public static HelpBoxMessageType ToHelpBoxType(IssueSeverity severity) => severity switch
        {
            IssueSeverity.Error => HelpBoxMessageType.Error,
            IssueSeverity.Warning => HelpBoxMessageType.Warning,
            _ => HelpBoxMessageType.Info
        };

        public static Color SeverityColor(List<ValidationIssue> issues)
        {
            if (issues.Any(i => i.Severity == IssueSeverity.Error)) return ErrorColor;
            if (issues.Any(i => i.Severity == IssueSeverity.Warning)) return WarnColor;
            if (issues.Count > 0) return InfoColor;
            return OkColor;
        }
    }
}
