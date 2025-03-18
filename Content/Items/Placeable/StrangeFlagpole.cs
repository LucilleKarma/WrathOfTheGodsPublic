using Microsoft.Xna.Framework;
using NoxusBoss.Content.Tiles.SolynCampsite;
using NoxusBoss.Core.DialogueSystem;
using NoxusBoss.Core.World.WorldGeneration;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace NoxusBoss.Content.Items.Placeable;

public class StrangeFlagpole : ModItem
{
    public override string Texture => GetAssetPath("Content/Items/Placeable", Name);

    public override void SetStaticDefaults()
    {
        Item.ResearchUnlockCount = 1;
    }

    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<SolynFlagTile>());
        Item.width = 16;
        Item.height = 10;
        Item.value = 0;
    }

    public override bool? UseItem(Player p)
    {
        if (Main.myPlayer == NetmodeID.MultiplayerClient || p.itemAnimation != p.itemAnimationMax)
            return null;

        Point checkPoint = FindGroundVertical(new Point(Player.tileTargetX, Player.tileTargetY));

        bool validArea = false;
        SolynCampsiteWorldGen.GetPlacementPositions(checkPoint, out Point campfirePosition, out Point telescopePosition, out Point tentPosition);
        if (campfirePosition != Point.Zero && telescopePosition != Point.Zero && tentPosition != Point.Zero)
            validArea = true;

        if (validArea)
        {
            Main.NewText(this.GetLocalizedValue("FlatEnoughText"), new Color(0, 190, 128));
            Dust.QuickDust(checkPoint.ToWorldCoordinates(), Color.ForestGreen).scale *= 2f;
        }
        else
        {
            Main.NewText(this.GetLocalizedValue("NotFlatEnoughText"), new Color(174, 12, 0));
            Dust.QuickDust(checkPoint.ToWorldCoordinates(), Color.Red).scale *= 2f;
        }
        p.itemAnimation--;
        return null;
    }

    public override void AddRecipes()
    {
        CreateRecipe(1).
            AddTile(TileID.Anvils).
            AddRecipeGroup(RecipeGroupID.Wood, 20).
            AddIngredient(ItemID.Sapphire).
            AddCondition(Language.GetText("Mods.NoxusBoss.Conditions.ObtainedBefore"), () => DialogueManager.FindByRelativePrefix("SolynTentSetUpDialogue").SeenBefore("Solyn3")).
            Register();
    }
}
