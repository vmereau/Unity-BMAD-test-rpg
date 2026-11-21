# Ideas Inbox

> Quick notes, ideas and suggestions to explore or design **later**, in a fuller session.
> Not a backlog of technical debt (→ `_bmad-output/implementation-artifacts/deferred-work.md`) and not
> finished design (→ `docs/World/`). When the user says "note", "idée", "suggestion", "pour plus tard",
> "à explorer"… the entry goes here.

## How to use

- **Add** an entry under the matching domain section (create the section if missing). Keep it short:
  capture the idea, not the design.
- **Status:** `brut` (just captured) → `à designer` (worth a design session) → `designé` (link the doc /
  spec that picked it up) or `abandonné` (one line saying why).
- When an entry is `designé` or `abandonné`, move it to **Archived** at the bottom.
- `perso:what-next` reads this file and surfaces `à designer` entries.

Entry template:

```markdown
### <Short title>
- **Date:** YYYY-MM-DD · **Status:** brut
- **Idea:** 1–3 sentences.
- **To explore:** open questions (optional).
```

Domains (suggested): Combat · AI / NPCs · Quests / Narrative · World / Areas · Player / Progression ·
Inventory / Crafting · UI / UX · Audio / Art · Tech / Tools

---

<!-- Add domain sections and entries below. -->

## AI / NPCs

### Revoir Aldric (cuisinier) : intro cuisine + receleur, ou deux personnages ?
- **Date:** 2026-11-21 · **Status:** à designer
- **Decision (2026-11-21):** deux personnages. Aldric reste receleur ; Tob (npc-020,
  `Data/NPCs/KitchenHand/`), aide de cuisine, fait l'intro cuisine via une suite de quêtes et ne parle
  jamais des affaires d'Aldric. Reste à designer : la suite de quêtes cuisine (`quests:design`).
- **Idea:** Aldric Sorn devrait présenter le système de cuisine au joueur (recettes, bonus du premier
  repas de chaque plat, cf. `spec-meal-permanent-stat-bonus`), en plus de son rôle de receleur
  (informations, crochets, quêtes de vol / pickpocket). Aujourd'hui il ne fait que la partie receleur
  (boutique de crochets, tuyaux de TitheBarnShortfall), sans rien sur la cuisine.
- **To explore:** un seul personnage, ou deux ? Piste : Aldric garde le rôle de receleur et un aide de
  cuisine (commis / marmiton) fait l'intro cuisine, et sert aussi d'intermédiaire vers le réseau
  d'Aldric. Qui enseigne le crochetage ? Est-ce qu'il faut une chaîne de quêtes de voleur ?
  `docs/World/NPCs/npc-011-innkeeper.md` parle encore d'une auberge : à mettre à jour.

---

## Archived
