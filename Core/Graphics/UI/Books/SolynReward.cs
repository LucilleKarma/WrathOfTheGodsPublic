using Newtonsoft.Json;
using Terraria.ModLoader;
using ItemIDs = Terraria.ID.ItemID;

namespace NoxusBoss.Core.Graphics.UI.Books;

public struct SolynReward
{
    /// <summary>
    /// The name associated with this reward's item, in English. This works with modded and vanilla names.
    /// </summary>
    ///
    /// <remarks>
    /// It is expected that names are formatted in PascalCase.
    /// </remarks>
    public string ItemName;

    /// <summary>
    /// The minimum stack of yields from this reward.
    /// </summary>
    public int MinStack;

    /// <summary>
    /// The maximum stack of yields from this reward.
    /// </summary>
    public int MaxStack;

    /// <summary>
    /// Whether this reward is gifted directly from Solyn, rather than from the standard reward UI.
    /// </summary>
    public bool GiftedDirectlyFromSolyn;

    /// <summary>
    /// The item ID associated with this reward.
    /// </summary>
    [JsonIgnore]
    public readonly int ItemID
    {
        get
        {
            if (VanillaItemNameRegistry.EnglishVanillaItemNameRelationship.TryGetValue(ItemName, out int vanillaID))
                return vanillaID;

            foreach (Mod mod in ModLoader.Mods)
            {
                if (ModContent.TryFind(mod.Name, ItemName, out ModItem item))
                    return item.Type;
            }

            return ItemIDs.None;
        }
    }
}
