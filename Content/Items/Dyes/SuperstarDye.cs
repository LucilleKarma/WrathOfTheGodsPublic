using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using NoxusBoss.Content.Rarities;
using NoxusBoss.Core.Data;
using NoxusBoss.Core.Utilities;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Content.Items.Dyes;

public class SuperstarDye : BaseDye
{
    public override void RegisterShader()
    {
        ManagedShader dyeShader = ShaderManager.GetShader("NoxusBoss.SuperstarDyeShader");
        string paletteFilePath = $"{this.GetModRelativeDirectory()}Palettes.json";
        Dictionary<string, Vector3[]>? palettes = LocalDataManager.Read<Vector3[]>(paletteFilePath);
        if (palettes is not null)
        {
            Vector3[] goldPalette = palettes["Gold"];
            Vector3[] redPalette = palettes["Red"];

            dyeShader.TrySetParameter("goldHairGradient", goldPalette);
            dyeShader.TrySetParameter("goldHairGradientCount", goldPalette.Length);
            dyeShader.TrySetParameter("redHairGradient", redPalette);
            dyeShader.TrySetParameter("redHairGradientCount", redPalette.Length);
        }

        dyeShader.CreateDyeBindings(Type);
    }

    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.value = Item.buyPrice(gold: 5);
        Item.rare = ModContent.RarityType<SolynRewardRarity>();
    }
}
