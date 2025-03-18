using NoxusBoss.Core.Autoloaders.SolynBooks;
using NoxusBoss.Core.GlobalInstances;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.Items;

public partial class SolynBooksSystem : ModSystem
{
    private static void LoadSeapmapCollectionObtainment()
    {
        GlobalNPCEventHandlers.ModifyNPCLootEvent += (npc, loot) =>
        {
            if (npc.type == NPCID.PirateCaptain)
                loot.Add(new CommonDrop(SolynBookAutoloader.Books["SeamapCollection"].Type, 3));
        };
    }
}
