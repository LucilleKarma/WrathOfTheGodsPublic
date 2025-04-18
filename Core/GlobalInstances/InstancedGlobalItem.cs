using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace NoxusBoss.Core.GlobalInstances;

public class InstancedGlobalItem : GlobalItem
{
    /// <summary>
    /// Whether this item is a Genesis component.
    /// </summary>
    public bool GenesisComponent
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this item is an unobtained Solyn book.
    /// </summary>
    public bool UnobtainedSolynBook
    {
        get;
        internal set;
    }

    /// <summary>
    /// Whether this item is intended to be gifted by Solyn, rather than via the rewards UI. This is only used by default in the context of Solyn's book reward system.
    /// </summary>
    public bool ToBeGiftedBySolyn
    {
        get;
        internal set;
    }

    /// <summary>
    /// Whether this item is a dummy item in Solyn's bookshelf UI.
    /// </summary>
    public bool SlotInBookshelfUI
    {
        get;
        internal set;
    }

    public override bool InstancePerEntity => true;

    public override GlobalItem Clone(Item? from, Item to)
    {
        InstancedGlobalItem clone = (InstancedGlobalItem)base.Clone(from, to);

        // SlotInBookshelfUI is deliberately not cloned, to ensure that it only is set once and not "sticky" across recreated item instances.
        clone.GenesisComponent = GenesisComponent;
        clone.UnobtainedSolynBook = UnobtainedSolynBook;
        clone.ToBeGiftedBySolyn = ToBeGiftedBySolyn;

        return clone;
    }

    public override void NetSend(Item item, BinaryWriter writer)
    {
        bool toBeGifted = false;
        if (item.TryGetGlobalItem(out InstancedGlobalItem g))
            toBeGifted = g.ToBeGiftedBySolyn;

        writer.Write((byte)toBeGifted.ToInt());
    }

    public override void NetReceive(Item item, BinaryReader reader)
    {
        bool toBeGifted = reader.ReadByte() != 0;
        if (item.TryGetGlobalItem(out InstancedGlobalItem g))
            g.ToBeGiftedBySolyn = toBeGifted;
    }

    public override void SaveData(Item item, TagCompound tag) => tag["ToBeGiftedBySolyn"] = ToBeGiftedBySolyn;

    public override void LoadData(Item item, TagCompound tag) => ToBeGiftedBySolyn = tag.GetBool("ToBeGiftedBySolyn");

    public override void Load()
    {
        On_Player.SellItem += DisallowSellingGenesisComponents;
        On_Main.MouseText_DrawItemTooltip_GetLinesInfo += OverrideItemName;
    }

    private void OverrideItemName(On_Main.orig_MouseText_DrawItemTooltip_GetLinesInfo orig, Item item, ref int yoyoLogo, ref int researchLine, float oldKB, ref int numLines, string[] toolTipLines, bool[] preFixLine, bool[] badPreFixLine, string[] toolTipNames, out int prefixlineIndex)
    {
        orig(item, ref yoyoLogo, ref researchLine, oldKB, ref numLines, toolTipLines, preFixLine, badPreFixLine, toolTipNames, out prefixlineIndex);
        if (!item.IsAir && item.TryGetGlobalItem(out InstancedGlobalItem globalItem) && globalItem.UnobtainedSolynBook)
            toolTipLines[0] = "???";
    }

    private static bool DisallowSellingGenesisComponents(On_Player.orig_SellItem orig, Player self, Item item, int stack)
    {
        if (item.Wrath().GenesisComponent)
            return false;

        return orig(self, item, stack);
    }
}
