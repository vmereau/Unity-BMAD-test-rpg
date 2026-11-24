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

## Combat

### Root motion hybride : étendre après l'essai du jab
- **Date:** 2026-11-23 · **Status:** à designer
- **Constat :** l'essai (`spec-action-root-motion-jab-trial`) marche sur le jab à main nue du joueur : `ActionRootMotion`
  (`OnAnimatorMove`) déplace la capsule de 0,40 m par le clip seul, pied planté à ~1 cm, annulation et combo corrects.
  Clip Meshy (squelette SMPL-H) ajusté dans Blender (`Tools/Blender/meshy_motion_fit.py`).
- **Fait (2026-11-24, `spec-action-root-motion-unarmed-combo`) :** Hook et Uppercut (clips Meshy `hook step` /
  `uppercut step`) : tout le combo à main nue avance par ses clips, déplacement continu dans les fondus.
- **Idea:** étendre ensuite : armes,
  « motion warping » en verrouillage (s'arrêter à bonne distance), puis esquive / exécution / relevé / stagger.
- **To explore:** NPC (ils jouent le nouveau clip avec le pas scripté ; `NavMeshAgent.Move` + culling `AlwaysAnimate` en
  combat) ; pied arrière du jab qui traîne 5–7 cm au-dessus du sol ; remplacer `attackSteps` par `rootMotionScale`
  partout ou garder les deux ; Meshy ignore distance et direction (`.claude/rules/meshy-mcp.md`).

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

### Mouvement des attaques : root motion hybride au lieu de l'`AttackStep` scripté
- **Date:** 2026-11-22 · **Status:** designé (2026-11-23) → `_bmad-output/implementation-artifacts/spec-action-root-motion-jab-trial.md`
- **Constat :** le projet n'utilise déjà plus la root motion (`applyRootMotion` off partout depuis
  `spec-full-body-attacks-root-motion`). Les clips d'attaque sont sur place ; `PlayerAttackStep` / `EntityAttackStep`
  poussent la capsule à vitesse constante (`AttackStep` en secondes) ; les pieds des clips à main nue ont été retouchés
  dans Blender pour coller à ce déplacement (`spec-unarmed-attack-foot-step`). Problème : double source de vérité
  (chiffres `AttackStep` ↔ pas retouchés à la main), pas synchronisé si la vitesse de l'Animator change (hit-stop, stat
  de vitesse d'attaque — `deferred-work.md`), les NPC glissent encore (point ouvert dans `deferred-work.md`).
- **Idea:** hybride « root motion lue et filtrée par le code », limité aux actions. La locomotion reste pilotée par le
  code. Les clips d'action contiennent le vrai déplacement (on avance la racine dans Blender ou via Meshy, pieds
  plantés). Un composant commun `ActionRootMotion` sur l'objet de l'Animator implémente `OnAnimatorMove` et ne lit
  `animator.deltaPosition` que dans les états tagués choisis : garde l'horizontal seulement, multiplicateur par arme
  (`rootMotionScale` remplace `attackSteps`), détection de rebord reprise de `PlayerAttackStep`, « motion warping »
  léger en verrouillage (s'arrêter à bonne distance de la cible), puis `CharacterController.Move` (joueur) ou
  `NavMeshAgent.Move` (NPC). Étendre ensuite à l'esquive, l'exécution, le relevé, le stagger.
- **Variante :** outil éditeur qui extrait le déplacement de la racine de chaque clip en `AnimationCurve` (dans un SO),
  échantillonnée en temps normalisé : déterministe, testable en EditMode, garde la règle « root motion off ».
- **Première étape :** essai sur une seule attaque (jab ou coup à une main) ; juger la sensation, l'absence de
  glissement des pieds et le comportement en verrouillage. Puis spec via `gds-quick-dev`.
- **To explore:** `OnAnimatorMove` à l'exécution ou courbes extraites ; NPC dans le premier lot ou non (culling de
  l'Animator à mettre sur `AlwaysAnimate` en combat, sinon pas de mouvement hors caméra) ; refaire les clips
  (Meshy) ou retoucher les existants (Blender) ; règles à mettre à jour (« `applyRootMotion` off everywhere », garde
  de `HumanoidAIAnimationDriver`, `.claude/rules/attack-pipeline.md`).
