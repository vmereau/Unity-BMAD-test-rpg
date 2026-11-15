# NPC 002 — Beaten Worker

> id: npc-002
> type: npc
> name: Tam
> status: draft
> location: area-001 — Player's Zone (recovers near campfire after event-001)
> faction: none
> role: social-informant
> personality: sardonic, resigned, observant
> unity-asset: Assets/_Game/Data/NPCs/BeatenWorker/
> quests-given: none
> quests-involved: quest-001
> events: event-001
> knows-of: npc-003 (stash owner), npc-005 (worker leader — indirectly), faction-002 (existence only)

---

## Identity

A long-timer in the encampment. Has been beaten before and will be beaten again. He knows it. His underperformance isn't incompetence — it's passive resignation. He figured out the minimum viable existence and stopped pretending otherwise. Dark humor is his armor.

He is the first NPC the player can choose to interact with after the arrival sequence. He is found recovering near the campfire in the player's tent zone — conscious, bruised, unbothered.

Named Tam: an ordinary laborer's name, nothing remarkable.

---

## Role in Area

Social intelligence layer. Distinct from npc-001 (tent mate), who covers logistics.

This NPC knows the *people* — who to trust, who to avoid, who's hiding something. He doesn't open up to a stranger: the player has to offer help first (greeting opener). After that he shares freely, not because he's kind, but because he stopped caring about consequences. Information is the one currency he still has and can't be beaten out of him.

He is **not a quest giver** — he points toward quests (the stash) and toward awareness (the rebel group), but doesn't assign them.

---

## Personality

**Tone:** Dry, sardonic, deadpan. Speaks as if mildly amused by everything, including his own suffering.

**Vocabulary:** Simple. Working-class. Short sentences. Never dramatic.

**Quirks:**
- Underreacts to everything. The world's awfulness is weather to him.
- Offers information unprompted when he judges the player ready to hear it.
- Never asks for anything in return.

**Taboo topics:** None visible — but he deflects anything that requires him to admit he still cares.

**Sample lines:**
- "Tam. And yes, that was the Warden. Not my first time. I'll manage."
- "Nobody said you had to pay. And if you get caught, you never met me."
- "There's people in this camp who've had enough. Not me. But people."

---

## Relationships

| NPC | Relationship |
|-----|-------------|
| npc-006 (Warden) | Has been beaten by him. No visible anger. Resigned. |
| npc-003 (Stash Owner) | Aware of the stash. Casual contempt — "he found his angle." |
| faction-002 (Workers' Ring) | Knows they exist. Not a member. Admires from a distance he'll never close. |
| npc-001 (Tent Mate) | Aware of each other. Different social orbit. |

---

## State Changes

| Condition | Change |
|-----------|--------|
| event-001 not complete | Not yet met. Not accessible. |
| event-001 complete | Found recovering in player's zone. First conversation available. |
| quest-001 started | Dry acknowledgment. "Told you it was there." |
| quest-001 success | Quiet approval. "Hm. Not bad." |
| quest-001 caught | Dark amusement. "Worth a try." |
| company path advanced | Grows more distant. Doesn't accuse. Just stops volunteering information. |
| worker path advanced | No change in tone — but occasionally slips something useful without framing it as help. |

---

## Dialogue Topics

| Topic ID | Trigger | Summary |
|----------|---------|---------|
| dlg-npc002-first-meeting | First interaction after event-001 | "Are you alright?" He says it's not his first time. Choice: offer help (he refuses, warns about helping strangers; unlocks camp-people + stash), mock him (closes both; only "Not for you." remains), or leave (opener stays) |
| dlg-npc002-camp-people | After offering help in the opener | Impressions of key NPCs — warden, stash owner, guards |
| dlg-npc002-stash-hint | After offering help in the opener | He's hungry and doesn't care where food comes from. Only if the player says they have no money (a claim, not a gold check) does he point at Brann's chest (starts quest-001): "if you get caught, you never met me" |
| dlg-npc002-rebel-mention | Player asks about resistance | "Heard there's people who've had enough." No more than that. |

---

## Open Threads

- Opener, camp-people, stash-hint and stash follow-up exist; rebel mention not written yet
- Potential: does he have a personal quest of his own, or is he purely a support NPC?
