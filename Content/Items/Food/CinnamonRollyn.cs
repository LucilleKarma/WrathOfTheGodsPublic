using Microsoft.Xna.Framework;
using NoxusBoss.Content.Buffs;
using NoxusBoss.Content.Rarities;
using NoxusBoss.Content.Tiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.Items.Food;

public class CinnamonRollyn : ModItem
{
    public override string Texture => "NoxusBoss/Assets/Textures/Content/Items/Food/CinnamonRollyn";

    public override void SetStaticDefaults()
    {
        Item.ResearchUnlockCount = 5;

        ItemID.Sets.FoodParticleColors[Item.type] = new Color[]
        {
            new Color(180, 112, 82),
            new Color(205, 133, 81),
            new Color(255, 139, 190),
            new Color(255, 224, 96)
        };

        ItemID.Sets.IsFood[Type] = true;

        Main.RegisterItemAnimation(Type, new DrawAnimationVertical(int.MaxValue, 3));

        On_Player.AddBuff += PreventOtherFoodBuffsFromBeingGranted;
    }

    private static void PreventOtherFoodBuffsFromBeingGranted(On_Player.orig_AddBuff orig, Player player, int type, int timeToAdd, bool quiet, bool foodHack)
    {
        bool isFoodBuff = type == BuffID.WellFed || type == BuffID.WellFed2 || type == BuffID.WellFed3;
        if (isFoodBuff && player.HasBuff<StarstrikinglySatiated>())
            return;

        orig(player, type, timeToAdd, quiet, foodHack);
    }

    public override void SetDefaults()
    {
        Item.DefaultToFood(32, 28, ModContent.BuffType<StarstrikinglySatiated>(), MinutesToFrames(10f));
        Item.value = Item.sellPrice(0, 1, 0, 0);
        Item.rare = ModContent.RarityType<SolynRewardRarity>();
    }

    public override void AddRecipes()
    {
        CreateRecipe(1).
            AddTile(ModContent.TileType<StarlitForgeTile>()).
            Register();
    }
}
