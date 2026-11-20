# CLAUDE.md — Assets/_Game/Art/UI/Inventory

> Loaded when Claude accesses files in this folder. Inventory filter-tab glyph icons (not 9-slice sprites):
> their prompt and import. Art direction: `Art/ART_DIRECTION.md`; shared UI sprite rules: `../CLAUDE.md`.

---

## Filter Glyph Prompt

**Filter glyph icons** `UI_Filter_<Name>.png` (All, Weapons, Armor, Potions,
Meals, Ingredients, Crafting, BooksRecipes, Misc): the `Icon` of `InventoryFilterTab` (inset 5 in a 52×52
`SlotWell` tab), imported Sprite, Max Size 128. Bone glyph on the flat panel brown, made by the user from:

```
Square minimalist UI icon for a dark medieval fantasy RPG in the style of Gothic 1 and 2, square image.
Subject: {GLYPH}.
A simple bold flat silhouette emblem, like a mark burned into dark leather: few shapes, thick clean
strokes, no fine detail, no texture noise, readable at 32 pixels.
The emblem is one flat color, pale bone-parchment (#D8CCB0), with a very slightly hand-made edge.
Centered, seen straight-on, filling about 70% of the frame, with an even margin on all sides.
On a flat solid near-black brown background (#2B2118), no gradient, no vignette, no frame, no border.
No text, no letters, no numbers, no shading, no highlights, no glow, no drop shadow, no watermark.
```

`{GLYPH}` = one plain object (e.g. Weapons `a single straight sword pointing upward, with a plain crossguard
and a round pommel`). A new filter tab needs a new glyph in the same wording.
