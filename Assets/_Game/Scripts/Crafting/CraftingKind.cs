namespace Game.Crafting
{
    /// <summary>
    /// What a crafting station does. Picks the screen mode (<c>CookingUI</c>) and which inventory items count as
    /// materials (<see cref="CookingRules.IsMaterialFor"/>). Cooking has the Experiment tab; Smithing is Recipes-only.
    /// </summary>
    public enum CraftingKind { Cooking, Smithing }
}
