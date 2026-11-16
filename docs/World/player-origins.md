# Player Origins and the Stripped Beat

> Status: first cut implemented (2026-11-16, `spec-player-origins`): pick panel in `MainMenu`, `Data/Origins/`,
> Voss's belongings topic. Blacksmith = Strength +1, Endurance +1, Smithing (30 gold shown); Joiner = Dexterity +2,
> Beginner Lockpicking (20 gold shown). Replaces the old "stripped beat / empty starting inventory" item
> deferred from `spec-area-1-arrival-escort-beating`, `-hut-briefing` and `-wardens-errand`.
> Story beats: **The Promise** → **Stripped** (narrative-design.md, Act 1). Event: event-001 (area-001).

## Intent

At a new game the player picks an **origin**: who the protagonist was before signing the Company contract. An origin
gives three things:

| Grant | Kept after arrival? |
|---|---|
| Small permanent stat bonus | **Yes** |
| Optional starting skill | **Yes** |
| Belongings: gold and origin items | **No**. Always taken on arrival |

The belongings are the bait. The recruiter promised they would travel separately (a baggage cart / sealed chest) and be
handed back at the camp. They never are. When the player asks about them, everyone in authority denies the cart ever
existed and tells them to forget it. The player should come away understanding that the "starting gear" was a lure and
that they have been robbed. Only the stat bonus and the skill remain, because the Company can't take what's in their
head and hands.

The **Wretch** origin starts with nothing and loses nothing. For a Wretch, the "where are my belongings?" topic never
appears.

## Origins

The roster is open. These are the first candidates. Bonuses stay small (one or two points) so an origin flavours a
build without defining it (the GDD is classless). Stats use the code names in `PlayerStats.StatType`: the
"vitality" bonus is **Endurance**.

| Origin | Stat bonus | Starting skill | Belongings (taken on arrival) |
|---|---|---|---|
| **Blacksmith's Apprentice** | small Strength + Endurance | Smithing (first tier) | smith's hammer, leather apron, some gold |
| **Joiner's Apprentice** | small Dexterity | Beginner Lockpicking | chisel set and joiner's plane in a tool roll, some gold |
| **Wretch** | none | none | none, so no belongings topic |
| *Later candidate:* Deserter | small Strength | — (or a basic weapon skill) | a sword, a soldier's coat, some gold |
| *Later candidate:* Poacher | small Dexterity | — | hunting bow, arrows, snares |

**First cut:** Blacksmith's Apprentice, Joiner's Apprentice, Wretch.

**Joiner's Apprentice** (confirmed 2026-11-16): a carpenter who did the fine work: cabinets, chests and strongboxes, including fitting their
locks. That explains the lockpicking (he knows how a lock sits in the wood and where it gives), and the Dexterity comes
from the fine handwork. It keeps the carpenter idea, and fits the camp's timber work. A plain locksmith's apprentice
would be the blunter alternative.

**Wretch:** nothing in exchange for now; starting with nothing *is* the intended design. A compensation (bonus, unique
lines, XP) may be revisited later.

The narrative premise ("a destitute young man fleeing a homeland at war") still holds. Every origin is someone with
nowhere else to go. The Wretch is the literal version of it, and the others had *something* left and were talked into
sending it ahead.

## Player flow

1. **Origin pick (new game).** Opened from the **New Game** entry of a start menu; the arrival sequence plays after the
   pick. A start menu doesn't exist yet (today a new game only starts on project launch or via
   `SaveSystem.RestartNewGame` from the death screen), so the menu is a prerequisite: its own spec, or part of the
   origins spec. The screen lists each origin with its stat bonus, skill and belongings.
   Belongings are labelled as "sent ahead with the Company baggage cart", which is the promise in the contract.
2. **Arrival.** Stats and skill are applied at once. The belongings were already taken in transit: they never reach the
   player's inventory in Area 1. At the arrival gate a guard / clerk line can confirm "your effects come up with the
   cart" as a final reassurance before the escort.
3. **Asking.** After event-001 (`WorldFact_Arrival_Done`), NPCs in authority get a **"Where are my belongings?"**
   topic, shown only when the origin had belongings.
   - **First cut: Voss only.** He is cold and final: there is no cart and no record of one. The player came in with
     what they're wearing and should forget it and think about their debt. His line alone has to make the theft
     clear: the denial is too flat, and too practiced, to be a mistake.
   - *Later:* escort / gate guards ("What cart?"), the storekeeper / Debt Manager (nothing in the ledger, but "lost"
     goods can be bought back on credit), and **Hobb**, who says it plainly: nobody's cart ever arrives, it's how they
     get men to come.
4. **Close.** No quest and no log entry. The beat is complete once the player has asked (or never asks). Asking sets
   the topic's dialogue fact, which later payoffs can read.

## Possible payoffs (later specs, not in the first cut)

- An origin item turns up **for sale** at the camp store or the black market, at a price the player can't afford yet.
  This is the proof the gear was stolen, and it gives the player an early goal ("buy back my father's hammer").
- The Company's false ledgers (staff house) list confiscated goods per arrival, as evidence for the Company-crimes ending.
- NPC lines that react to the origin (a smith recognising an apprentice's hands, a guard spotting a deserter).

## Data and implementation notes (for the quick-dev spec)

- **Origin data:** an `OriginSO` per origin (id, display name, description, stat bonuses, optional `SkillSO`, list of
  promised items + gold, shown on the pick screen only and kept for later payoffs, `hasBelongings` flag (false for the Wretch)).
- **Persistence:** the chosen origin must survive saves. Stat bonuses go into saved base stats and the skill into
  `PlayerSkills` (both already saved). The origin itself can be a `WorldFact_Origin_<Id>` set at new game, which also
  makes it readable by memory conditions and quests.
- **Topic gating:** the belongings topic needs "arrival done **and** origin had belongings". Either set a single
  `WorldFact_Origin_HadBelongings` at new game (simplest condition), or gate on each `WorldFact_Origin_<Id>`
  except Wretch. Prefer the single fact.
- **Belongings never spawn.** The items are only *shown* on the origin screen. Nothing is added and then removed, so
  there are no save / load edge cases mid-arrival. Gold and inventory stay empty from frame one (Wretch and the rest
  alike).
- **Dev skip:** `Dev: Skip Arrival` must still apply a default origin (Wretch, or the last pick) so dev runs are
  consistent.
- Dialogue goes through `NPC:dialogue`. The screen goes through `ui:create` for sprites and the usual `UIScreenManager`
  rules.

## Decisions (2026-11-16)

- **Wretch:** no starting gear, stats or skills. That is the intended design for now; a compensation may come later.
- **Entry point:** New Game in a start menu → origin pick → arrival sequence.
- **Belongings:** shown on the pick screen only, never in the inventory.
- **Belongings topic:** Voss only in the first cut.
- **Roster:** Blacksmith's Apprentice, Wretch and a Dexterity origin with Beginner Lockpicking: Joiner's Apprentice (confirmed).
