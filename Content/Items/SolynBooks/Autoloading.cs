using NoxusBoss.Core.Autoloaders.SolynBooks;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.Data;
using Terraria.ModLoader;
using static NoxusBoss.Core.Autoloaders.SolynBooks.SolynBookAutoloader;

namespace NoxusBoss.Content.Items;

public partial class SolynBooksSystem : ModSystem
{
    public override void Load()
    {
        string dataPath = "Content/Items/SolynBooks/SolynBooks.json";
        Dictionary<string, LoadableBookData> data = LocalDataManager.Read<LoadableBookData>(dataPath);

        foreach (KeyValuePair<string, LoadableBookData> kv in data)
        {
            // Special case: The absence notice is not loaded if Xeroc exists in the Calamity mod.
            bool xerocSomehowExists = ModLoader.TryGetMod(CalamityCompatibility.ModName, out Mod cal) && cal.TryFind("Xeroc", out ModNPC _);
            if (kv.Key == "AbsenceNotice" && xerocSomehowExists)
                continue;

            Create(Mod, kv.Value);
        }
    }
}
