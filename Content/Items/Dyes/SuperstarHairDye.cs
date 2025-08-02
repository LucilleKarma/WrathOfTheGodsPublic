using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Core.Utilities;
using NoxusBoss.Content.Rarities;
using NoxusBoss.Core.Data;
using ReLogic.Content;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.Items.Dyes;

public class SuperstarHairDye : ModItem
{
    public override string Texture => "NoxusBoss/Assets/Textures/Content/Items/Dyes/SuperstarHairDye";

    public override void SetStaticDefaults()
    {
        // Avoid loading assets on dedicated servers. They don't use graphics cards.
        if (!Main.dedServ)
        {
            Asset<Effect> shader = ModContent.Request<Effect>("NoxusBoss/Assets/AutoloadedEffects/Shaders/Dyes/Hair/SuperstarHairDyeShader", AssetRequestMode.ImmediateLoad);

            string paletteFilePath = $"{this.GetModRelativeDirectory()}Palettes.json";
            Dictionary<string, Vector3[]>? palettes = LocalDataManager.Read<Vector3[]>(paletteFilePath);
            if (palettes is not null)
            {
                Vector3[] goldPalette = palettes["Gold"];
                Vector3[] redPalette = palettes["Red"];
                shader.Value.Parameters["goldHairGradient"]?.SetValue(goldPalette);
                shader.Value.Parameters["goldHairGradientCount"]?.SetValue(goldPalette.Length);
                shader.Value.Parameters["redHairGradient"]?.SetValue(redPalette);
                shader.Value.Parameters["redHairGradientCount"]?.SetValue(redPalette.Length);
            }

            GameShaders.Hair.BindShader(Type, new HairShaderData(shader, ManagedShader.DefaultPassName));
        }

        Item.ResearchUnlockCount = 3;
    }

    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 26;
        Item.maxStack = Item.CommonMaxStack;
        Item.value = Item.buyPrice(gold: 5);
        Item.rare = ModContent.RarityType<SolynRewardRarity>();
        Item.UseSound = SoundID.Item3;
        Item.useStyle = ItemUseStyleID.DrinkLiquid;
        Item.useTurn = true;
        Item.useAnimation = 17;
        Item.useTime = 17;
        Item.consumable = true;
    }
}
