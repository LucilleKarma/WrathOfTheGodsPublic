using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Core.Graphics.RenderTargets;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;

public partial class AvatarOfEmptiness
{
    internal static Vector2 ShadowyPartsRenderTargetSize => Vector2.One * TargetDownscaleFactor * 4800f;

    internal static InstancedRequestableTarget ShadowyPartsRenderTarget;

    internal void LoadTargets_ShadowyParts()
    {
        if (Main.netMode == NetmodeID.Server)
            return;

        ShadowyPartsRenderTarget = new InstancedRequestableTarget();
        Main.ContentThatNeedsRenderTargets.Add(ShadowyPartsRenderTarget);
    }

    private void DrawBackLegs()
    {
        // Collect textures.
        Texture2D upperLeftLeg = GennedAssets.Textures.SecondPhaseForm.Leg1.Value;
        Texture2D lowerLeftLeg = GennedAssets.Textures.SecondPhaseForm.Leg2.Value;
        Texture2D upperRightLeg = GennedAssets.Textures.SecondPhaseForm.Leg3.Value;
        Texture2D lowerRightLeg = GennedAssets.Textures.SecondPhaseForm.Leg4.Value;

        // Calculate leg scale and draw position information.
        float legScale = TargetDownscaleFactor * 1.6f;
        Vector2 center = ShadowyPartsRenderTargetSize * 0.5f;
        Vector2 upperLeftLegStart = center + new Vector2(-22f, 46f) * legScale;
        Vector2 lowerLeftLegStart = center + new Vector2(-92f, -60f) * legScale;
        Vector2 upperRightLegStart = center + new Vector2(144f, -40f) * legScale;
        Vector2 lowerRightLegStart = center + new Vector2(72f, -26f) * legScale;

        // Calculate rotation information.
        float upperLeftLegRotation = 0.09f;
        float lowerLeftLegRotation = 0.65f;
        float upperRightLegRotation = -1.1f;
        float lowerRightLegRotation = -0.2f;

        // Draw the left legs.
        Main.spriteBatch.Draw(upperLeftLeg, upperLeftLegStart, null, Color.Black, upperLeftLegRotation, upperLeftLeg.Size() * Vector2.UnitX, LegScale * legScale, 0, 0f);
        Main.spriteBatch.Draw(lowerLeftLeg, lowerLeftLegStart, null, Color.Black, lowerLeftLegRotation, lowerLeftLeg.Size() * Vector2.UnitX, LegScale * legScale, 0, 0f);

        // Draw the right legs.
        Main.spriteBatch.Draw(upperRightLeg, upperRightLegStart, null, Color.Black, upperRightLegRotation, Vector2.Zero, LegScale * legScale, 0, 0f);
        Main.spriteBatch.Draw(lowerRightLeg, lowerRightLegStart, null, Color.Black, lowerRightLegRotation, Vector2.Zero, LegScale * legScale, 0, 0f);
    }

    private void DrawShadowArms()
    {
        Vector2 drawOffset = NPC.Center - ShadowyPartsRenderTargetSize * 0.5f;
        foreach (AvatarShadowArm arm in ShadowArms)
            arm.Draw(drawOffset, NPC);
    }

    private void DrawShadowGas()
    {
        // Prepare for shader drawing.
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

        // Prepare the smoke shader.
        ManagedShader smokeShader = ShaderManager.GetShader("NoxusBoss.AvatarSmokeShapeShader");
        smokeShader.TrySetParameter("appearanceCutoff", Saturate(1f - LegScale.Length() * 0.707f) * 0.56f);
        smokeShader.SetTexture(DendriticNoiseZoomedOut, 1, SamplerState.LinearWrap);
        smokeShader.Apply();

        // Draw the smoke.
        Vector2 smokeScale = new Vector2(800f, 1500f) / WhitePixel.Size() * NPC.Size / DefaultHitboxSize * TargetDownscaleFactor;
        Vector2 drawPosition = ShadowyPartsRenderTargetSize * 0.5f;
        DrawData targetData = new DrawData(WhitePixel, drawPosition, null, Color.Black, 0f, WhitePixel.Size() * new Vector2(0.5f, 1f), smokeScale, 0, 0f);
        targetData.Draw(Main.spriteBatch);

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.AnisotropicClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
    }

    private void ProcessTargets_ShadowyParts()
    {
        ShadowyPartsRenderTarget.Request((int)ShadowyPartsRenderTargetSize.X, (int)ShadowyPartsRenderTargetSize.Y, RenderTargetIdentifier, () =>
        {
            if (!TargetsShouldBeProcessed)
                return;

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);

            DrawBackLegs();
            DrawShadowArms();
            DrawShadowGas();

            Main.spriteBatch.End();
        });
    }

    public void RenderShadowPartsFromTarget(Vector2 drawPosition)
    {
        if (!ShadowyPartsRenderTarget.TryGetTarget(RenderTargetIdentifier, out RenderTarget2D? target) || target is null)
            return;

        Matrix transform = NPC.IsABestiaryIconDummy ? Main.UIScaleMatrix : Main.GameViewMatrix.TransformationMatrix;
        if (UsingFinalTarget)
            transform = Matrix.Identity;

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, transform);

        ManagedShader shadowOverlayShader = ShaderManager.GetShader("NoxusBoss.AvatarShadowOverlayShader");
        shadowOverlayShader.TrySetParameter("scale", 2f);
        shadowOverlayShader.TrySetParameter("fadeAtCenter", true);
        shadowOverlayShader.TrySetParameter("invertColor", true);
        shadowOverlayShader.TrySetParameter("textureSize", ShadowyPartsRenderTargetSize);
        shadowOverlayShader.Apply();
        Main.spriteBatch.Draw(target, drawPosition, null, Color.White * NPC.Opacity.Squared(), 0f, target.Size() * 0.5f, NPC.scale / TargetDownscaleFactor * ZPositionScale * 0.5f, 0, 0f);

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, transform);

        Main.spriteBatch.Draw(target, drawPosition, null, Color.Black, 0f, target.Size() * 0.5f, NPC.scale / TargetDownscaleFactor * ZPositionScale * 0.5f, 0, 0f);
    }
}
