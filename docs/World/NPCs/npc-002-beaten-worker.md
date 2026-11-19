# NPC 002 — Beaten Worker

> id: npc-002
> type: npc
> name: Tam
> status: draft
> location: area-001 — Player's Zone (recovers near campfire after event-001)
> faction: none
> role: social-informant
> personality: tired, gentle, observant
> unity-asset: Assets/_Game/Data/NPCs/BeatenWorker/
> quests-given: none
> quests-involved: quest-001
> events: event-001
> knows-of: npc-003 (stash owner), npc-005 (worker leader — indirectly), faction-002 (existence only)

---

## Identity

A long-timer in the encampment. Has been beaten before and will be beaten again. He knows it. His underperformance isn't incompetence or defiance: he is simply worn out. The camp took his strength, not his decency. He is tired, soft-spoken and friendly to anyone who treats him decently.

He is the first NPC the player can choose to interact with after the arrival sequence. He is found recovering near the campfire in the player's tent zone — conscious, bruised, exhausted.

Named Tam: an ordinary laborer's name, nothing remarkable.

---

## Role in Area

Social intelligence layer. Distinct from npc-001 (tent mate), who covers logistics.

This NPC knows the *people* — who to trust, who to avoid, who's hiding something. He doesn't open up to a stranger: the player has to offer help first (greeting opener). After that he shares freely, because he is kind and wants the newcomer to avoid what happened to him. Mocked, he doesn't fight back: he closes up, sad and tired, and keeps to himself for good.

He is **not a quest giver** — he points toward quests (the stash) and toward awareness (the rebel group), but doesn't assign them.

---

## Personality

**Tone:** Soft, weary, friendly. Grateful for small kindness, never bitter.

**Vocabulary:** Simple. Working-class. Short sentences. Never dramatic.

**Quirks:**
- Trails off when tired ("..."). Worries about the player before himself.
- Offers information unprompted when he judges the player ready to hear it.
- Never asks outright for help, never sneers. When hurt he withdraws instead of hitting back.

**Taboo topics:** None. He goes quiet when hurt.

**Sample lines:**
- "Tam. And yes, that was the Warden. It's not the first time and I doubt it'll be the last, so don't fret over me. I'll be fine, I just need to sit a while and let the ground stop moving."
- "Be careful, and if he catches you, please don't mention my name."
- "...Maybe. I'm too tired to argue with you." (when mocked)
- "There's people in this camp who've had enough. I'm too tired for that. But they're out there."

---

## Relationships

| NPC | Relationship |
|-----|-------------|
| npc-006 (Warden) | Has been beaten by him. No visible anger. Resigned. |
| npc-003 (Stash Owner) | Aware of the stash. Wary of him, not bitter. |
| faction-002 (Workers' Ring) | Knows they exist. Not a member. Admires from a distance he'll never close. |
| npc-001 (Tent Mate) | Aware of each other. Different social orbit. |

---

## State Changes

| Condition | Change |
|-----------|--------|
| event-001 not complete | Not yet met. Not accessible. |
| event-001 complete | Found recovering in player's zone. First conversation available. |
| quest-001 started | Grateful: "If you come across something to eat, I'd be grateful." |
| quest-001 success | Touched: "Thank you. Truly." |
| quest-001 caught | Worried for the player, not amused. |
| company path advanced | Grows more distant. Doesn't accuse. Just stops volunteering information. |
| worker path advanced | No change in tone — but occasionally slips something useful without framing it as help. |

---

## Dialogue Topics

| Topic ID | Trigger | Summary |
|----------|---------|---------|
| dlg-npc002-first-meeting | First interaction after event-001 | "Are you alright?" He says it's not his first time. Choice: offer help (he thanks you, says there's little to do today, warns about helping strangers; unlocks camp-people + stash), mock him ("...Maybe. I'm too tired to argue with you."; closes both; only a tired "...Not really, no. I'm tired. Leave me be." remains), or leave (opener stays) |
| dlg-npc002-camp-people | After offering help in the opener | Impressions of key NPCs — warden, stash owner, guards |
| dlg-npc002-stash-hint | After offering help in the opener | He's hungry and not picky about food; either answer starts quest-001 (Food for Tam). Only if the player says they have no money (a claim, not a gold check) does he point at Brann's chest: "if he catches you, please don't mention my name" |
| dlg-npc002-feed | quest-001 started | Player hands him food through the Give window: bread or an apple (base ending) or a cooked meal ("something even better than you hoped for": better ending, he won't forget it). Completes quest-001 |
| dlg-npc002-stash-reported | Hint + stash taken | "You were right about Brann's supplies." Worried approval, small XP, doesn't complete the quest |
| dlg-npc002-rebel-mention | Player asks about resistance | "Heard there's people who've had enough." No more than that. |

---

## Open Threads

- Opener, camp-people, stash-hint, feed and stash follow-up exist; rebel mention not written yet
- The meal ending (`DialogueFact_HiddenStash_FedMeal`) is meant to open something later: a favour, a tip, a contact
- Potential: does he have a personal quest of his own, or is he purely a support NPC?
