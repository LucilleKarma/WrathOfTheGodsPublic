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

    public static void GiveItemToPlayer<TModItem>(Player player, bool giveItem) where TModItem : ModItem
    {
        if (giveItem)
        {
            int itemID = ModContent.ItemType<TModItem>();
            player.QuickSpawnItem(new EntitySource_WorldEvent(), itemID);
        }

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

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(seenDialogue.Count);
        foreach (var item in seenDialogue) writer.Write(item);

        writer.Write(clickedDialogue.Count);
        foreach (var item in clickedDialogue) writer.Write(item);

        writer.Write(givenItems.Count);
        foreach (var item in givenItems) writer.Write(item);
    }

    public override void NetReceive(BinaryReader reader)
    {
        seenDialogue.Clear();
        clickedDialogue.Clear();
        givenItems.Clear();

        var seenCount = reader.ReadInt32();
        for (var i = 0; i < seenCount; i++)
            seenDialogue.Add(reader.ReadString());

        var clickedCount = reader.ReadInt32();
        for (var i = 0; i < clickedCount; i++)
            clickedDialogue.Add(reader.ReadString());

        var givenCount = reader.ReadInt32();
        for (var i = 0; i < givenCount; i++)
            givenItems.Add(reader.ReadString());
    }
}
