---
title: 'Rename the test armor to Leather Armor'
type: 'chore'
created: '2026-11-03'
status: 'done'
route: 'one-shot'
---

# Rename the test armor to Leather Armor

## Intent

**Problem:** `Armor_TestArmor` now equips the skinned leather armor (`LeatherArmor_Visual`), but its item name was still "Test Armor" with an empty description.

**Approach:** Set `itemName` to "Leather Armor" and write a description matching the model (boiled-leather cuirass over a padded gambeson, bracers). The asset file name, `itemId`, stats and icon reference are unchanged; a new icon prompt was handed to the user (generated outside Unity).

## Suggested Review Order

- Display name and description, in the tone of the other item descriptions
  [`Armor_TestArmor.asset:16`](../../Assets/_Game/Data/Items/Armor_TestArmor.asset#L16)
