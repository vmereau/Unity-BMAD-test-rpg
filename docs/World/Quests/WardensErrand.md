# The Warden's Errand

- ID: `WardensErrand` (no quest number; an access task from the area-001 doc).
- Location: Company Encampment and its four work sites.
- Giver: Warden Voss (npc-006). Managers: Overseer Dace (quarry), Gorm Holl (wood camp), Corvin Ash (npc-010, hunting camp), Reeve Odo Pell (farm).
- Status: implemented (`spec-area-1-wardens-errand`; report-only ending: `spec-wardens-errand-no-work-choice`).

It starts on its own when event-001 ends (`WorldFact_Arrival_Done`), and Hobb's arrival briefing ends by pointing the player to Voss. Voss's "I'm here for work." topic gives four quest-item notices (`Item_Notice_Quarry` / `_Timber` / `_Hunting` / `_Farm`, granted by `PlayerReward_WardensErrand_Assigned`). Each manager has a one-time "I have a notice from the Warden." topic. Its choice "Hand over the Warden's notice." needs and uses up that site's notice, sets the delivered fact, then the manager plants the hook for their later quest. Every manager also has a repeatable "What's the work here?" topic, available before the errand too. When all four are delivered, Voss's "The notices are delivered." topic closes the errand: finishing it sets `DialogueFact_Warden_Errand_Reported` and completes the quest (100 XP, `PlayerReward_WardensErrand_Completed`). There is no work-site choice: every site stays open, and the player can go to any manager and take their quests (decided 2026-11-16, `spec-wardens-errand-no-work-choice`). The errand is optional: no timer, no penalty. Notices can't be sold, dropped, stored or bound to the action bar.

## Authored data

| Fact | Setter | Purpose |
|---|---|---|
| `WorldFact_Arrival_Done` | `ArrivalSequence` (end of event-001, or dev skip) | Start; unlocks Voss's assign topic |
| `DialogueFact_Warden_Errand_Assigned` | `Start_Warden_ErrandAssign` | Step 0 part 1; unlocks the managers' notice topics; grants the notices |
| `DialogueFact_{QuarryOverseer\|TimberBoss\|LeadHunter\|FarmReeve}_NoticeDelivered` | the "Hand over" choice in each `Choice_<Manager>_Notice` | Step 0 parts 2–5; hides that notice topic |
| `QuestFact_WardensErrand_Step0` | computed (step 0 complete) | Step 1; unlocks Voss's report topic |
| `DialogueFact_Warden_Errand_Reported` | `Start_Warden_ErrandReport` (one-time topic, set at its end) | Completed part |
| `QuestFact_WardensErrand_Completed` | computed | Hides the report topic; XP reward |

Assets: `Data/Quests/WardensErrand/Quest_WardensErrand`, facts in `Data/Facts/`, notices in `Data/Items/Quest/`, rewards in `Data/Rewards/`, dialogues in `Warden/Dialogues/Errand/` and `<Manager>/Dialogues/{Notice,Work}/`. Managers stand under the hand-owned scene root `WorkSiteManagers`.

## Verification route

New game (or Dev: Skip Arrival) → quest log shows "report to Voss" → Voss assigns → four notices in the Misc tab with "Quest Item" category, no sell / drop / put → deliver in any order, saving and loading in between → report back → quest completes with 100 XP, the report and notice topics are gone, the work topics remain.
