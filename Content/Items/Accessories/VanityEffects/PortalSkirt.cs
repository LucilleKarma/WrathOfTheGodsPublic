using Luminance.Assets;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.Rarities;
using NoxusBoss.Core.GlobalInstances;
using NoxusBoss.Core.Utilities;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.Items.Accessories.VanityEffects;

public class PortalSkirt : ModItem
{
    public const string WearingPortalSkirtVariableName = "WearingPortalSkirt";

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetStaticDefaults()
    {
        Item.ResearchUnlockCount = 1;
        ItemID.Sets.ItemNoGravity[Type] = true;

        PlayerDataManager.ResetEffectsEvent += ResetSkirt;
        PlayerDataManager.PlayerModifyDrawInfoEvent += HideLegs;
    }

    private void HideLegs(PlayerDataManager p, ref PlayerDrawSet drawInfo)
    {
        if (!p.GetValueRef<bool>(WearingPortalSkirtVariableName))
            return;

        drawInfo.colorLegs = Color.Transparent;
        drawInfo.colorArmorLegs = Color.Transparent;
    }

    private void ResetSkirt(PlayerDataManager p) => p.GetValueRef<bool>(WearingPortalSkirtVariableName).Value = false;

    public override void SetDefaults()
    {
        Item.width = 36;
        Item.height = 36;
        Item.rare = ModContent.RarityType<AvatarRarity>();
        Item.value = Item.buyPrice(2, 0, 0, 0);

        Item.accessory = true;
        Item.vanity = true;
    }

    public override void UpdateVanity(Player player) => player.GetValueRef<bool>(WearingPortalSkirtVariableName).Value = true;

    public override void UpdateAccessory(Player player, bool hideVisual)
    {
        if (!hideVisual)
            player.GetValueRef<bool>(WearingPortalSkirtVariableName).Value = true;
    }

    private static void DrawRift(Vector2 drawPosition)
    {
        Texture2D innerRiftTexture = GennedAssets.Textures.FirstPhaseForm.RiftInnerTexture.Value;
        Vector2 textureArea = Vector2.One * 100f / innerRiftTexture.Size();

        ManagedShader riftShader = ShaderManager.GetShader("NoxusBoss.DarkPortalShader");
        riftShader.TrySetParameter("time", Main.GlobalTimeWrappedHourly * 0.1f);
        riftShader.TrySetParameter("baseCutoffRadius", 0.1f);
        riftShader.TrySetParameter("swirlOutwardnessExponent", 0.151f);
        riftShader.TrySetParameter("swirlOutwardnessFactor", 3.67f);
        riftShader.TrySetParameter("vanishInterpolant", 0f);
        riftShader.TrySetParameter("edgeColor", new Vector4(1f, 0.08f, 0.08f, 1f));
        riftShader.TrySetParameter("edgeColorBias", 0.15f);
        riftShader.SetTexture(WavyBlotchNoise, 1, SamplerState.AnisotropicWrap);
        riftShader.SetTexture(WavyBlotchNoise, 2, SamplerState.AnisotropicWrap);
        riftShader.Apply();

        Main.spriteBatch.Draw(innerRiftTexture, drawPosition, null, new Color(77, 0, 2), 0f, innerRiftTexture.Size() * 0.5f, textureArea, 0, 0f);
    }

    public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
    {
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, null, null, null, null, null, Main.UIScaleMatrix);
        DrawRift(position);
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, Main.UIScaleMatrix);

        return false;
    }

    public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
    {
        Main.spriteBatch.PrepareForShaders();
        DrawRift(Item.position - Main.screenPosition);
        Main.spriteBatch.ResetToDefault();

        return false;
    }
}
