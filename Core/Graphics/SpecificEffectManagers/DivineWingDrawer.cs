using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.Items.Accessories.Wings;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;
using Terraria;
using Terraria.DataStructures;
using Terraria.Graphics;
using Terraria.Graphics.Renderers;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Graphics.SpecificEffectManagers;

[Autoload(Side = ModSide.Client)]
public class DivineWingDrawer : ModSystem
{
    private static bool disallowSpecialWingDrawing;

    private static bool anyoneIsUsingWings;

    public static Vector3 WingColorShift
    {
        get
        {
            float pinkInterpolant = Cos01(Main.GlobalTimeWrappedHourly * 5.1f);
            Vector3 baseColor = new Vector3(Cos(Main.GlobalTimeWrappedHourly * 5.8f) * 0.35f + 1.9f, 0.25f, pinkInterpolant * 0.12f - 0.28f);
            if (NamelessDeitySky.DifferentStarsInterpolant >= 0.001f && NamelessDeityBoss.Myself is not null)
                baseColor = Vector3.Lerp(baseColor, new(Cos(Main.GlobalTimeWrappedHourly * 5.8f) * 0.2f + 0.3f, -1f, 0.35f), NamelessDeitySky.DifferentStarsInterpolant);

            return baseColor;
        }
    }

    private static ManagedRenderTarget AfterimageTarget
    {
        get;
        set;
    }

    public static ManagedRenderTarget AfterimageTargetPrevious
    {
        get;
        private set;
    }

    public override void OnModLoad()
    {
        RenderTargetManager.RenderTargetUpdateLoopEvent += PrepareAfterimageTarget;
        Main.QueueMainThreadAction(() =>
        {
            AfterimageTarget = new(true, ManagedRenderTarget.CreateScreenSizedTarget);
            AfterimageTargetPrevious = new(true, ManagedRenderTarget.CreateScreenSizedTarget);
        });
        On_LegacyPlayerRenderer.DrawPlayers += DrawWingsTarget;
        On_PlayerDrawLayers.DrawPlayer_09_Wings += DisallowWingDrawingIfNecessary;
    }

    private void DrawWingsTarget(On_LegacyPlayerRenderer.orig_DrawPlayers orig, LegacyPlayerRenderer self, Camera camera, IEnumerable<Player> players)
    {
        if (anyoneIsUsingWings)
        {
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicWrap, DepthStencilState.None, camera.Rasterizer, null, camera.GameViewMatrix.TransformationMatrix);

            // Optionally apply the wing shader. This will have some weird multiplayer oddities in terms of other's wings being affected by the client's shader but whatever.
            // The alternative of creating render targets for every player is not a pleasant thought.
            if (Main.LocalPlayer.cWings != 0)
                GameShaders.Armor.Apply(Main.LocalPlayer.cWings, Main.LocalPlayer);

            Main.spriteBatch.Draw(AfterimageTargetPrevious, Main.screenLastPosition - Main.screenPosition, LocalPlayerDrawManager.ShaderDrawAction is not null ? Color.Transparent : Color.White);
            Main.spriteBatch.End();
        }

        orig(self, camera, players);
    }

    private void DisallowWingDrawingIfNecessary(On_PlayerDrawLayers.orig_DrawPlayer_09_Wings orig, ref PlayerDrawSet drawinfo)
    {
        if (drawinfo.hideEntirePlayer || drawinfo.drawPlayer.dead)
            return;

        if (Main.gameMenu)
            disallowSpecialWingDrawing = false;

        if (drawinfo.drawPlayer.wings == DivineWings.WingSlotID && disallowSpecialWingDrawing)
        {
            // Calculate various draw data for the outline.
            Texture2D outlineTexture = GennedAssets.Textures.Wings.DivineWings_WingsOutline;
            Vector2 playerPosition = drawinfo.Position - Main.screenPosition + new Vector2(drawinfo.drawPlayer.width / 2, drawinfo.drawPlayer.height - drawinfo.drawPlayer.bodyFrame.Height / 2) + Vector2.UnitY * 7f;
            Vector2 wingDrawPosition = (playerPosition + new Vector2(-9f, 2f) * drawinfo.drawPlayer.Directions).Floor();
            Rectangle outlineFrame = outlineTexture.Frame(1, 4, 0, drawinfo.drawPlayer.wingFrame);
            Color outlineColor = drawinfo.drawPlayer.GetImmuneAlpha(Color.White, 0f) * Pow(drawinfo.stealth * (1f - drawinfo.shadow), 3f);
            Vector2 outlineOrigin = outlineFrame.Size() * 0.5f;

            DrawData outline = new DrawData(outlineTexture, wingDrawPosition, outlineFrame, outlineColor, drawinfo.drawPlayer.fullRotation, outlineOrigin, 1f, drawinfo.playerEffect, 0f)
            {
                shader = drawinfo.cWings
            };
            drawinfo.DrawDataCache.Add(outline);
            return;
        }

        orig(ref drawinfo);
    }

    private void PrepareAfterimageTarget()
    {
        // Check if anyone is using the special wings.
        anyoneIsUsingWings = false;
        for (int i = 0; i < Main.maxPlayers; i++)
        {
            Player p = Main.player[i];
            if (p.wings != DivineWings.WingSlotID || !p.active || p.dead)
                continue;

            anyoneIsUsingWings = true;
            break;
        }

        if (!ShaderManager.HasFinishedLoading || Main.gameMenu || !anyoneIsUsingWings)
            return;

        GraphicsDevice gd = Main.instance.GraphicsDevice;

        // Prepare the render target for drawing.
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.Default, RasterizerState.CullNone, null, Matrix.Identity);
        gd.SetRenderTarget(AfterimageTarget);
        gd.Clear(Color.Transparent);

        // Draw the contents of the previous frame to the target.
        bool probablyUsingSniperEffects = Main.LocalPlayer.scope || Main.LocalPlayer.HeldMouseItem().type == ItemID.SniperRifle || Main.LocalPlayer.HeldMouseItem().type == ItemID.Binoculars;
        if (!probablyUsingSniperEffects)
            Main.spriteBatch.Draw(AfterimageTargetPrevious, Vector2.Zero, Color.White);

        // Draw player wings.
        DrawPlayerWingsToTarget();

        // Draw the afterimage shader to the result.
        ApplyPsychedelicDiffusionEffects();

        // Return to the back-buffer.
        Main.spriteBatch.End();
        gd.SetRenderTarget(null);
    }

    public static void DrawPlayerWingsToTarget()
    {
        // Prepare the wing psychedelic shader.
        ManagedShader wingShader = ShaderManager.GetShader("NoxusBoss.NamelessDeityPsychedelicWingShader");
        wingShader.TrySetParameter("colorShift", WingColorShift);
        wingShader.TrySetParameter("lightDirection", Vector3.UnitZ);
        wingShader.TrySetParameter("normalMapCrispness", 0.86f);
        wingShader.TrySetParameter("normalMapZoom", new Vector2(0.7f, 0.4f));
        wingShader.SetTexture(TurbulentNoise, 1);
        wingShader.SetTexture(GennedAssets.Textures.Wings.DivineWings_WingNormalMap, 2);
        wingShader.SetTexture(GennedAssets.Textures.Extra.PsychedelicWingTextureOffsetMap, 3);
        wingShader.Apply();

        for (int i = 0; i < Main.maxPlayers; i++)
        {
            Player p = Main.player[i];
            if (p.wings != DivineWings.WingSlotID || p.dead || !p.active || p.sleeping.isSleeping)
                continue;

            // Append the player's wings to the draw cache.
            PlayerDrawSet drawInfo = default;
            drawInfo.BoringSetup(p, [], [], [], p.TopLeft + Vector2.UnitY * p.gfxOffY, 0f, p.fullRotation, p.fullRotationOrigin);
            disallowSpecialWingDrawing = false;
            PlayerDrawLayers.DrawPlayer_09_Wings(ref drawInfo);
            disallowSpecialWingDrawing = true;

            // Draw the wings with the activated shader.
            foreach (DrawData wingData in drawInfo.DrawDataCache)
            {
                DrawData copy = wingData with { rotation = p.fullRotation };
                copy.Draw(Main.spriteBatch);
            }
        }
    }

    public static void ApplyPsychedelicDiffusionEffects()
    {
        if (!ShaderManager.HasFinishedLoading || !anyoneIsUsingWings)
            return;

        GraphicsDevice gd = Main.instance.GraphicsDevice;
        gd.SetRenderTarget(AfterimageTargetPrevious);
        gd.Clear(Color.Transparent);

        // Prepare the afterimage psychedelic shader.
        ManagedShader afterimageShader = ShaderManager.GetShader("NoxusBoss.NamelessDeityPsychedelicAfterimageShader");
        afterimageShader.TrySetParameter("uScreenResolution", Main.ScreenSize.ToVector2());
        afterimageShader.TrySetParameter("warpSpeed", 0.00028f);
        afterimageShader.SetTexture(TurbulentNoise, 1);
        afterimageShader.Apply();

        Main.spriteBatch.Draw(AfterimageTarget, Vector2.Zero, Color.White);
    }
}
