using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Core.Graphics.RenderTargets;
using NoxusBoss.Core.Utilities;
using Terraria;
using Terraria.DataStructures;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace NoxusBoss.Content.Items.Accessories.VanityEffects;

public class PortalSkirtDrawLayer : PlayerDrawLayer
{
    /// <summary>
    /// The dye shader indices for the portal skirt, keyed by player.
    /// </summary>
    public static Dictionary<int, int> SkirtDyeMappings
    {
        get;
        private set;
    } = new Dictionary<int, int>(Main.maxPlayers);

    /// <summary>
    /// The render target responsible for rendering the portal skirt for players.
    /// </summary>
    public static InstancedRequestableTarget PortalTarget
    {
        get;
        private set;
    }

    public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) => drawInfo.drawPlayer.GetValueRef<bool>(PortalSkirt.WearingPortalSkirtVariableName) && !drawInfo.drawPlayer.invis;

    public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.Carpet);

    public override void Load()
    {
        On_PlayerDrawLayers.DrawPlayer_13_Leggings += What;
        On_Player.UpdateItemDye += FindSkirtItemDyeShader;

        PortalTarget = new InstancedRequestableTarget();
        Main.ContentThatNeedsRenderTargets.Add(PortalTarget);
    }

    private void FindSkirtItemDyeShader(On_Player.orig_UpdateItemDye orig, Player self, bool isNotInVanitySlot, bool isSetToHidden, Item armorItem, Item dyeItem)
    {
        orig(self, isNotInVanitySlot, isSetToHidden, armorItem, dyeItem);
        if (armorItem.type == ModContent.ItemType<PortalSkirt>())
            SkirtDyeMappings[self.whoAmI] = GameShaders.Armor.GetShaderIdFromItemId(dyeItem.type);
    }

    private void What(On_PlayerDrawLayers.orig_DrawPlayer_13_Leggings orig, ref PlayerDrawSet drawinfo)
    {
        if (drawinfo.drawPlayer.GetValueRef<bool>(PortalSkirt.WearingPortalSkirtVariableName))
        {
            Draw(ref drawinfo);
            return;
        }

        orig(ref drawinfo);
    }

    private static void RenderIntoTarget()
    {
        if (WavyBlotchNoise.Uninitialized || !WavyBlotchNoise.Asset.IsLoaded)
            return;

        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap, DepthStencilState.Default, RasterizerState.CullNone, null);

        Texture2D innerRiftTexture = GennedAssets.Textures.FirstPhaseForm.RiftInnerTexture.Value;
        var riftShader = ShaderManager.GetShader("NoxusBoss.DarkPortalShader");
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

        Main.spriteBatch.Draw(innerRiftTexture, ViewportSize * 0.5f, null, new Color(77, 0, 2), 0f, innerRiftTexture.Size() * 0.5f, ViewportSize / innerRiftTexture.Size() * 0.99f, 0, 0f);
        Main.spriteBatch.End();
    }

    protected override void Draw(ref PlayerDrawSet drawInfo)
    {
        if (drawInfo.shadow != 0f || drawInfo.drawPlayer.dead)
            return;

        PortalTarget.Request(208, 100, drawInfo.drawPlayer.whoAmI, RenderIntoTarget);
        if (!PortalTarget.TryGetTarget(drawInfo.drawPlayer.whoAmI, out RenderTarget2D? portalTexture) || portalTexture is null)
            return;

        Vector2 position = drawInfo.drawPlayer.legPosition + drawInfo.legVect + new Vector2(
            (int)(drawInfo.Position.X - Main.screenPosition.X - drawInfo.drawPlayer.legFrame.Width * 0.5f + drawInfo.drawPlayer.width * 0.5f),
            (int)(drawInfo.Position.Y - Main.screenPosition.Y + drawInfo.drawPlayer.height - drawInfo.drawPlayer.legFrame.Height + 4f) + 5
        );

        SkirtDyeMappings.TryGetValue(drawInfo.drawPlayer.whoAmI, out int dyeShaderIndex);

        DrawData rift = new DrawData(portalTexture, position, null, Color.White, drawInfo.drawPlayer.legRotation, portalTexture.Size() * 0.5f, 1f, 0, 0f)
        {
            shader = dyeShaderIndex
        };
        drawInfo.DrawDataCache.Add(rift);
    }
}
