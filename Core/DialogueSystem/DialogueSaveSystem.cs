using SubworldLibrary;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace NoxusBoss.Core.DialogueSystem;

public class DialogueSaveSystem : ModSystem
{
    internal static List<string> seenDialogue = [];

    internal static List<string> clickedDialogue = [];

    internal static List<string> givenItems = [];

    public static bool ItemHasBeenGiven<TModItem>() where TModItem : ModItem =>
        givenItems.Contains(ModContent.GetInstance<TModItem>().FullName);

    public static void GiveItemToPlayer<TModItem>(Player player) where TModItem : ModItem
    {
        int itemID = ModContent.ItemType<TModItem>();
        player.QuickSpawnItem(new EntitySource_WorldEvent(), itemID);
        givenItems.Add(ModContent.GetInstance<TModItem>().FullName);
    }

    public override void ClearWorld()
    {
        if (!SubworldSystem.AnyActive())
        {
            seenDialogue.Clear();
            clickedDialogue.Clear();
            givenItems.Clear();
        }
    }

    public override void SaveWorldData(TagCompound tag)
    {
        tag["seenDialogue"] = seenDialogue.ToList();
        tag["clickedDialogue"] = clickedDialogue.ToList();
        tag["givenItems"] = givenItems.ToList();
    }

    public override void LoadWorldData(TagCompound tag)
    {
        seenDialogue = tag.GetList<string>("seenDialogue").ToList();
        clickedDialogue = tag.GetList<string>("clickedDialogue").ToList();
        givenItems = tag.GetList<string>("givenItems").ToList();
    }
}
