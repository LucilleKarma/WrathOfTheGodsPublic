using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Core.World.GameScenes.AvatarAppearances;
using NoxusBoss.Core.World.GameScenes.RiftEclipse;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.FirstPhaseForm;

public static class AvatarRiftTargetContent
{
    public static readonly Vector2 Size = new Vector2(2400f, 2400f) * DownscaleFactor;

    /// <summary>
    /// The downscaling factor for the Avatar when drawn to the render target.
    /// </summary>
    public const float DownscaleFactor = 0.3f;

    public static void DrawRiftContentsToTarget(Vector2 screenPos, NPC npc, bool backgroundProp, AvatarRiftLiquidInfo? liquidDrawContents)
    {
        // Prepare for shader drawing.
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicWrap, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

        // Collect rift draw information.
        Texture2D riftTexture = WavyBlotchNoise;
        Vector2 riftScale = npc.Size / riftTexture.Size() * 2f;
        float vanishInterpolant = 0f;
        float time = 0f;
        float baseCutoffRadius = 0.1f;
        if (npc.type == ModContent.NPCType<AvatarRift>())
        {
            time = npc.As<AvatarRift>().RiftRotationTimer;
            if (npc.As<AvatarRift>().DrawnFromTelescope)
                time = Main.GlobalTimeWrappedHourly * 0.038f;
        }
        if (npc.type == ModContent.NPCType<AvatarOfEmptiness>())
            time = npc.As<AvatarOfEmptiness>().RiftRotationTimer;
        if (npc.type == ModContent.NPCType<AvatarRift>() && !npc.IsABestiaryIconDummy)
            vanishInterpolant = Saturate(1f - npc.scale / DownscaleFactor);
        if (npc.type == ModContent.NPCType<AvatarOfEmptiness>())
            vanishInterpolant = npc.As<AvatarOfEmptiness>().RiftVanishInterpolant;
        if (backgroundProp)
        {
            time = Main.GlobalTimeWrappedHourly * 0.008f;
            baseCutoffRadius = 0.16f;
            riftScale *= RiftEclipseSky.ProgressionScaleFactor;
            vanishInterpolant = Pow(vanishInterpolant, 1.25f);
        }

        // Apply the rift shader.
        var riftShader = ShaderManager.GetShader("NoxusBoss.AvatarRiftShapeShader");
        riftShader.TrySetParameter("time", time);
        riftShader.TrySetParameter("baseCutoffRadius", baseCutoffRadius);
        riftShader.TrySetParameter("swirlOutwardnessExponent", 0.151f);
        riftShader.TrySetParameter("swirlOutwardnessFactor", 3.67f);
        riftShader.TrySetParameter("vanishInterpolant", vanishInterpolant);
        riftShader.SetTexture(WavyBlotchNoise, 1, SamplerState.AnisotropicWrap);
        riftShader.SetTexture(WavyBlotchNoise, 2, SamplerState.AnisotropicWrap);
        riftShader.Apply();

        // Draw the rift.
        Vector2 riftDrawPosition = npc.Center - screenPos;
        Main.spriteBatch.Draw(riftTexture, riftDrawPosition, null, Color.White * npc.Opacity, 0f, riftTexture.Size() * 0.5f, riftScale, 0, 0f);

        // Draw liquids.
        Vector2 liquidOffset = Vector2.UnitY * Utils.Remap(npc.scale, 1f, 0f, 0f, -200f);
        liquidDrawContents?.DrawLiquid(Main.screenPosition - npc.Center + Size * 0.5f + liquidOffset);

        // Draw the liquid after the rift.
        Main.spriteBatch.End();
    }

    public static void DrawRiftWithShader(NPC npc, Vector2 screenPos, Matrix transformPerspective, bool backgroundProp, float suckOpacity, float rotation = 0f, int? identifierOverride = null)
    {
        // Initialize the rift drawer, with the Avatar as its current host.
        int identifier = identifierOverride ?? npc.whoAmI;
        AvatarRift.RiftDrawContents.Request((int)Size.X, (int)Size.Y, identifier, () =>
        {
            // Collect draw information.
            bool backgroundProp = false;
            float originalScale = npc.scale;
            Vector2 screenPos = npc.Center - Size * 0.5f;
            AvatarRiftLiquidInfo? liquidDrawContents = null;
            if (npc.ModNPC is AvatarRift rift)
            {
                backgroundProp = rift.BackgroundProp;
                liquidDrawContents = rift.LiquidDrawContents;
            }
            if (npc.ModNPC is AvatarOfEmptiness avatar)
                liquidDrawContents = avatar.LiquidDrawContents;

            // Draw the host's rift contents to the render target.
            npc.scale *= DownscaleFactor;
            DrawRiftContentsToTarget(screenPos, npc, backgroundProp, liquidDrawContents);
            npc.scale = originalScale;
        });

        // If the rift drawer is ready, draw it to the screen.
        if (!AvatarRift.RiftDrawContents.TryGetTarget(identifier, out RenderTarget2D? target) || target is null)
            return;

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicWrap, DepthStencilState.None, RasterizerState.CullNone, null, transformPerspective);

        // Prepare the rift shader.
        float scaleCorrection = 1f;
        if (backgroundProp)
            scaleCorrection = npc.scale * 0.6f;

        float what = 1f;
        float blurOffset = 0f;
        float opacity = 1f;
        float time = 0f;
        if (backgroundProp)
        {
            time = Main.GlobalTimeWrappedHourly * 0.2f;
            what *= 1.3f;
        }
        if (npc.type == ModContent.NPCType<AvatarRift>())
            time = npc.As<AvatarRift>().RiftRotationTimer * 10f;
        if (npc.type == ModContent.NPCType<AvatarOfEmptiness>())
            time = npc.As<AvatarOfEmptiness>().RiftRotationTimer * 10f;
        if (npc.type == ModContent.NPCType<AvatarRift>() && npc.As<AvatarRift>().DrawnFromTelescope)
        {
            float scaleFactor = 0.25f;
            if (NPC.downedPlantBoss)
                scaleFactor = 0.5f;
            if (NPC.downedAncientCultist)
                scaleFactor = 0.97f;
            if (RiftEclipseManagementSystem.RiftEclipseOngoing)
                scaleFactor = Lerp(3f, 5f, RiftEclipseManagementSystem.IntensityInterpolant);

            what *= scaleFactor;
            blurOffset = InverseLerp(0.5f, 0.25f, scaleFactor) * 0.006f;

            time = Main.GlobalTimeWrappedHourly * 0.2f;
            opacity *= 0.66f;
        }
        if (npc.type == ModContent.NPCType<AvatarOfEmptiness>())
            what *= npc.As<AvatarOfEmptiness>().ZPositionScale * 1.4f;
        if (Main.gameMenu)
            what = 0.08f;
        if (npc.IsABestiaryIconDummy)
        {
            scaleCorrection = 0.85f;
            what = npc.scale;
        }

        float[] blurWeights = new float[5];
        for (int i = 0; i < blurWeights.Length; i++)
            blurWeights[i] = GaussianDistribution(i - blurWeights.Length * 0.5f, 1f) / 8f;

        ManagedShader colorShader = ShaderManager.GetShader("NoxusBoss.AvatarRiftColorShader");
        colorShader.TrySetParameter("time", time);
        colorShader.TrySetParameter("suckSpeed", suckOpacity >= 0.01f ? 7.01f : 0f);
        colorShader.TrySetParameter("worldPositionOffset", npc.Center / Main.ScreenSize.ToVector2() * -2f);
        colorShader.TrySetParameter("scaleCorrection", scaleCorrection);
        colorShader.TrySetParameter("blurWeights", blurWeights);
        colorShader.TrySetParameter("blurOffset", blurOffset);
        colorShader.SetTexture(GennedAssets.Textures.FirstPhaseForm.RiftInnerTexture, 1, SamplerState.LinearWrap);
        colorShader.SetTexture(CheckeredNoise, 2, SamplerState.PointWrap);
        colorShader.SetTexture(BurnNoise, 3, SamplerState.LinearWrap);
        colorShader.Apply();

        // Draw the rift target.
        Color riftColor = backgroundProp ? Color.Gray : Color.White;
        if (backgroundProp && Main.bloodMoon)
            riftColor = Color.Black;

        Vector2 drawPosition = npc.Center - screenPos;
        DrawData targetData = new DrawData(target, drawPosition, target.Frame(), riftColor * npc.Opacity * opacity, rotation, target.Size() * 0.5f, what * 1.67f, 0, 0f);
        targetData.Draw(Main.spriteBatch);

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, transformPerspective);
    }
}
