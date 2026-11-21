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

### Tob (aide de cuisine) : suite de quêtes tutoriel cuisine
- **Date:** 2026-11-21 · **Status:** à designer
- **Decision (2026-11-21):** deux personnages. Aldric (`Data/NPCs/Cook/`) reste receleur. Tob (npc-020,
  `Data/NPCs/KitchenHand/`, ~20 ans) enseigne la cuisine et **ne parle jamais des affaires d'Aldric** (ni
  indice, ni renvoi vers lui). Fiche : `Data/NPCs/KitchenHand/CLAUDE.md` ; doc monde : `docs/World/NPCs/npc-020-tob.md`.
- **Idea:** une suite de quêtes donnée par Tob qui enseigne les systèmes de cuisine existants :
  découverte par expérimentation (onglet Experiment, `spec-cooking-experiment-and-recipes`), objets recette
  (Storekeeper, `spec-recipe-items-and-craft-count`) et bonus du premier repas (`spec-meal-permanent-stat-bonus`),
  expliqué en jeu comme « varier ce qu'on mange, c'est bon pour le corps ».
- **Piste de suite (brouillon, non validé) :**
  1. Premier plat : Tob demande des ingrédients, fait cuisiner une recette qu'il donne, le joueur mange →
     explication du premier repas.
  2. Expérimenter : Tob donne seulement un indice / les ingrédients, le joueur trouve la combinaison
     (candidat : Apple Mushroom Sandwich, +5 PV max au premier repas).
  3. Acheter une recette chez le Storekeeper (ex. Apple Trout Sandwich) et la cuisiner.
  4. (Optionnel) Varier : manger plusieurs plats différents pour la première fois → récompense.
- **Prochaine session (Unity ouvert) :**
  - Lancer `quests:design` sur la piste ci-dessus, puis `quests:implement` et `quests:audit`.
  - `NPC_KitchenHand.asset` a été écrit à la main en YAML (copie de `NPC_FieldHand`) : vérifier qu'il
    s'importe sans erreur (`read_console`).
  - Placer Tob dans StartingTown au cookhouse (Cook à (-6,20), feu à (-7,22)) avec routine, `PersistentID`
    et `KilledFact` (même fact dans `_killedFact`) ; tête / teinte de peau (`characters:portrait-face`).
- **To explore:** qui enseigne le crochetage ; une chaîne de quêtes de voleur / pickpocket pour Aldric ;
  `docs/World/NPCs/npc-011-innkeeper.md` parle encore d'une auberge (à mettre à jour).

---

## Archived
