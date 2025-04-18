using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace NoxusBoss.Core;

public class VanillaItemNameRegistry : ModSystem
{
    /// <summary>
    /// The set of all English vanilla item names mapped to their respective item ID.
    /// </summary>
    public static readonly Dictionary<string, int> EnglishVanillaItemNameRelationship = [];

    public override void PostSetupContent()
    {
        string oldCultureName = Language.ActiveCulture.Name;
        LanguageManager.Instance.SetLanguage(GameCulture.FromCultureName(GameCulture.CultureName.English));

        for (int i = ItemID.None + 1; i < ItemID.Count; i++)
        {
            string itemName = Lang.GetItemNameValue(i).Replace(" ", string.Empty);
            EnglishVanillaItemNameRelationship[itemName] = i;
        }

        LanguageManager.Instance.SetLanguage(oldCultureName);
    }
}
