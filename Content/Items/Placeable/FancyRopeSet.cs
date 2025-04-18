using NoxusBoss.Content.Rarities;
using NoxusBoss.Content.Tiles.SolynRopes;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.Items.Placeable;

public class FancyRopeSet : ModItem
{
    public override string Texture => GetAssetPath("Content/Items/Placeable", Name);

    public override void SetStaticDefaults()
    {
        Item.ResearchUnlockCount = 10;
        On_Player.PlaceThing_Tiles += PlaceRope;
        On_Main.MouseText_DrawItemTooltip_GetLinesInfo += SayCanBePlacedInsteadOfConsumable;
    }

    private void SayCanBePlacedInsteadOfConsumable(On_Main.orig_MouseText_DrawItemTooltip_GetLinesInfo orig, Item item, ref int yoyoLogo, ref int researchLine, float oldKB, ref int numLines, string[] toolTipLine, bool[] preFixLine, bool[] badPreFixLine, string[] toolTipNames, out int prefixlineIndex)
    {
        int originalTileToPlace = item.createTile;
        if (item.type == Type)
            item.createTile = TileID.Stone;

        orig(item, ref yoyoLogo, ref researchLine, oldKB, ref numLines, toolTipLine, preFixLine, badPreFixLine, toolTipNames, out prefixlineIndex);

        if (item.type == Type)
            item.createTile = originalTileToPlace;
    }

    private void PlaceRope(On_Player.orig_PlaceThing_Tiles orig, Player self)
    {
        Item item = self.inventory[self.selectedItem];
        bool notInRangeToPlace = !(self.Left.X / 16f - Player.tileRangeX - item.tileBoost - self.blockRange <= Player.tileTargetX) ||
                              !(self.Right.X / 16f + Player.tileRangeX + item.tileBoost - 1f + self.blockRange >= Player.tileTargetX) ||
                              !(self.Top.Y / 16f - Player.tileRangeY - item.tileBoost - self.blockRange <= Player.tileTargetY) ||
                              !(self.Bottom.Y / 16f + Player.tileRangeY + item.tileBoost - 2f + self.blockRange >= Player.tileTargetY);
        bool usingItem = self.ItemTimeIsZero && self.itemAnimation == Item.useAnimation - 1 && self.controlUseItem;

        if (item.type == Type && !notInRangeToPlace && usingItem)
        {
            SolynRopePlacementPreviewSystem.CreateForPlayer(self, Main.MouseWorld);
            self.ConsumeItem(Type);
        }

        orig(self);
    }

    public override void SetDefaults()
    {
        Item.width = 16;
        Item.height = 10;
        Item.placeStyle = 0;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.useAnimation = 15;
        Item.useTime = 10;
        Item.maxStack = Item.CommonMaxStack;
        Item.useTurn = true;
        Item.autoReuse = true;
        Item.consumable = true;
        Item.rare = ModContent.RarityType<SolynRewardRarity>();
        Item.value = 0;
    }

    public override bool CanUseItem(Player player) => !SolynRopePlacementPreviewSystem.previews.TryGetValue(player.whoAmI, out _);
}

