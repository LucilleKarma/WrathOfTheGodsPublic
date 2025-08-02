using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Assets.Fonts;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.FormPresets;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Projectiles;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Rendering;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Rendering.RenderSteps;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;
using NoxusBoss.Core.Graphics;
using NoxusBoss.Core.Utilities;
using NoxusBoss.Core.World.WorldSaving;
using ReLogic.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity;

public partial class NamelessDeityBoss : ModNPC
{
    /// <summary>
    ///     Whether hands should inherit the opacity Nameless has.
    /// </summary>
    /// <remarks>
    ///     When this is <see langword="false"/>, hands will draw at full opacity, irrespective of <see cref="NPC.Opacity"/>.
    ///     For most circumstances this should be left on, but if hands should be enabled while Nameless is invisible (such as during his spawn animation), this should be used.
    /// </remarks>
    public bool HandsShouldInheritOpacity
    {
        get;
        set;
    } = true;

    /// <summary>
    ///     Whether Nameless should display his "You have passed the test." dialog over the screen.
    /// </summary>
    public bool DrawCongratulatoryText
    {
        get;
        set;
    }

    /// <summary>
    ///     A 0-1 interpolant that determines how strongly a black overlay should be applied to the screen during the death animation.
    /// </summary>
    public float UniversalBlackOverlayInterpolant
    {
        get;
        set;
    }

    /// <summary>
    ///     The current world position of the censor box.
    /// </summary>
    public Vector2 CensorPosition
    {
        get;
        set;
    }

    /// <summary>
    ///     The handler for Nameless' wing set.
    /// </summary>
    public NamelessDeityWingSet Wings => RenderComposite.Find<WingsStep>().Wings;

    /// <summary>
    ///     Determines whether Nameless' hands should be drawn manually, separate from the render target.<br></br>
    ///     This is necessary because in certain contexts the hands get cut off by the render target bounds due to being detached and too far from Nameless.
    /// </summary>
    public bool DrawHandsSeparateFromRT
    {
        get;
        set;
    }

    /// <summary>
    ///     Safely resets the <see cref="Main.spriteBatch"/> in a way that works for render targets.
    /// </summary>
    private static void ResetSpriteBatch()
    {
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
    }

    /// <summary>
    ///     Safely resets the <see cref="Main.spriteBatch"/> blend state in a way that works for render targets.
    /// </summary>
    private static void SetBlendState(BlendState blendState)
    {
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, blendState, SamplerState.AnisotropicClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
    }

    public override Color? GetAlpha(Color drawColor)
    {
        // Display as a black silhouette when saving the player from the Avatar.
        if (CurrentState == NamelessAIType.SavePlayerFromAvatar)
            return Color.Black * NPC.Opacity;

        return base.GetAlpha(drawColor);
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        DoBehavior_RealityTearPunches_UpdateScreenShader();

        // Ensure that bestiary dummies are visible, have a censor, and various things like wings are updated.
        float opacityFactor = 1f;
        if (NPC.IsABestiaryIconDummy)
        {
            NPC.position.Y += NPC.scale * 100f;
            CensorPosition = IdealCensorPosition;
            NPC.Opacity = 1f;
            Wings.Update(WingMotionState.Flap, Main.GlobalTimeWrappedHourly % 1f);
            NPC.scale = 0.175f;

            RenderComposite ??= new NamelessDeityRenderComposite(NPC);
            RenderComposite.Update();
            FightTimer++;

            if (FightTimer % 120 == 37)
                RerollAllSwappableTextures();
        }

        // Draw all afterimages.
        else
        {
            // Become a bit more transparent as afterimages draw.
            opacityFactor -= AfterimageSpawnChance * AfterimageOpacityFactor * 0.1f;

            BlendState afterimageBlend = TestOfResolveSystem.IsActive ? BlendState.Additive : SubtractiveBlending;
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, afterimageBlend, SamplerState.AnisotropicWrap, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            IEnumerable<Projectile> afterimages = AllProjectilesByID(ModContent.ProjectileType<NamelessDeityAfterimage>());
            foreach (Projectile afterimage in afterimages)
                afterimage.As<NamelessDeityAfterimage>().DrawSelf();
            Main.spriteBatch.ResetToDefault();
        }

        // Draw the Nameless Deity target.
        DrawSelfWrapper(screenPos, opacityFactor);

        // Draw the top hat if the player's name is "Blast" as a "dev preset".
        if (NamelessDeityFormPresetRegistry.UsingBlastPreset && !NPC.IsABestiaryIconDummy)
            DrawTopHat(screenPos);

        // Draw the hands manually without regard for the render target if necessary.
        if (DrawHandsSeparateFromRT)
            RenderComposite.Find<ArmsStep>().RenderArms(NPC, false);

        // Draw the censor.
        bool canRenderCensor = UniversalBlackOverlayInterpolant < 1f;
        if (canRenderCensor && (RenderComposite.UsedPreset?.Data.UseCensor ?? true))
            RenderComposite.RenderCensor(CensorPosition - screenPos, NPC.IsABestiaryIconDummy);

        DrawDeathAnimationCutscene(screenPos);

        Main.spriteBatch.ResetToDefault();
        NamelessFireParticleSystemManager.ParticleSystem.RenderAll();
        Main.spriteBatch.ResetToDefault();

        return false;
    }

    public void PrepareGeneralOverlayShader(Texture2D target)
    {
        if (CurrentState == NamelessAIType.SavePlayerFromAvatar)
            return;

        // Use a pixelation shader by default.
        if ((RenderComposite.UsedPreset?.ShaderOverlayEffect ?? null) is null && NPC.Opacity >= 0.001f)
        {
            ManagedShader pixelationShader = ShaderManager.GetShader("NoxusBoss.PixelationShader");
            pixelationShader.TrySetParameter("pixelationFactor", Vector2.One * 1.75f / target.Size());
            pixelationShader.Apply();
        }

        // If a special shader overlay effect is specified, use that instead.
        else
            RenderComposite.UsedPreset?.ShaderOverlayEffect?.Invoke(target);
    }

    public void DrawSelfWrapper(Vector2 screenPos, float opacityFactor)
    {
        RenderComposite.PrepareRendering();
        if (!NamelessDeityRenderComposite.CompositeTarget.TryGetTarget(RenderComposite.TargetIdentifier, out RenderTarget2D? target) || target is null)
            return;

        if (CurrentState == NamelessAIType.SavePlayerFromAvatar)
            DrawBackglow(screenPos);

        // Prepare the overlay shader if this isn't a bestiary dummy.
        if (!NPC.IsABestiaryIconDummy)
        {
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            PrepareGeneralOverlayShader(target);
        }

        // Calculate the base color.
        Color color = Color.Lerp(Color.White, Color.White with { A = 0 }, Sqrt(1f - opacityFactor));

        // Make the color darker and more transparent as Nameless enters the background, to give the illusion of actually entering it and not just becoming tiny.
        color = Color.Lerp(color, new(141, 96, 92), InverseLerp(2.1f, 6f, ZPosition)) * Utils.Remap(ZPosition, 4f, 9f, 1f, 0.6f);

        // Apply relative darkening.
        color = Color.Lerp(color, Color.Black, RelativeDarkening);

        // Draw the target contents.
        if (TestOfResolveSystem.IsActive)
        {
            float pulse = Main.GlobalTimeWrappedHourly * 5f % 1f;
            Main.spriteBatch.Draw(target, NPC.Center - screenPos, null, NPC.GetAlpha(color) * opacityFactor * (1f - pulse).Squared(), NPC.rotation, target.Size() * 0.5f, TeleportVisualsAdjustedScale * (1f + pulse * 0.2f), 0, 0f);
        }
        DrawSelf(target, NPC.Center - screenPos, color, opacityFactor);

        // Reset the sprite batch.
        if (!NPC.IsABestiaryIconDummy)
            Main.spriteBatch.ResetToDefault();
    }

    public void DrawSelf(Texture2D target, Vector2 drawPosition, Color color, float opacityFactor)
    {
        if (CurrentState == NamelessAIType.OpenScreenTear)
        {
            RenderComposite.Find<ArmsStep>().RenderArms(NPC, false);
            return;
        }

        Main.spriteBatch.Draw(target, drawPosition, null, NPC.GetAlpha(color) * opacityFactor, NPC.rotation, target.Size() * 0.5f, TeleportVisualsAdjustedScale, 0, 0f);
    }

    public void DrawBackglow(Vector2 screenPos)
    {
        Main.spriteBatch.Draw(BloomFlare, NPC.Center - screenPos, null, new Color(255, 182, 193, 0) * NPC.Opacity * 0.75f, Main.GlobalTimeWrappedHourly * -0.3f, BloomFlare.Size() * 0.5f, TeleportVisualsAdjustedScale * 7f, 0, 0f);
        Main.spriteBatch.Draw(BloomFlare, NPC.Center - screenPos, null, new Color(255, 230, 168, 0) with { A = 0 } * NPC.Opacity * 0.75f, Main.GlobalTimeWrappedHourly * 0.2f, BloomFlare.Size() * 0.5f, TeleportVisualsAdjustedScale * 8.5f, 0, 0f);
        Main.spriteBatch.Draw(BloomCircleSmall, NPC.Center - screenPos, null, new Color(255, 230, 168, 0) with { A = 0 } * NPC.Opacity * 0.9f, 0f, BloomCircleSmall.Size() * 0.5f, TeleportVisualsAdjustedScale * 10f, 0, 0f);

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, SubtractiveBlending, SamplerState.AnisotropicClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

        Vector2 ringDrawPosition = NPC.Center - screenPos;
        Texture2D ring = GennedAssets.Textures.NamelessDeity.GlowRing.Value;
        Color ringColor = new Color(255, 255, 0);
        Color subtractiveRingColor = new Color(255 - ringColor.R, 255 - ringColor.G, 255 - ringColor.B);
        Main.EntitySpriteDraw(ring, ringDrawPosition, null, subtractiveRingColor * NPC.Opacity.Squared(), 0f, ring.Size() * 0.5f, TeleportVisualsAdjustedScale * 0.7f, 0);
        Main.EntitySpriteDraw(ring, ringDrawPosition, null, subtractiveRingColor * NPC.Opacity.Squared(), 0f, ring.Size() * 0.5f, TeleportVisualsAdjustedScale * 0.9f, 0);

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
    }

    public static void DrawDeathAnimationTerminationText()
    {
        DynamicSpriteFont font = FontRegistry.Instance.NamelessDeityText;
        string text = Language.GetTextValue("Mods.NoxusBoss.Dialog.NamelessDeityEndScreenSkipText");
        float scale = 0.6f;
        float maxHeight = 200f;
        Vector2 textSize = font.MeasureString(text);
        if (textSize.Y > maxHeight)
            scale = maxHeight / textSize.Y;
        Vector2 textDrawPosition = Main.ScreenSize.ToVector2() * 0.8f;
        textDrawPosition -= textSize * scale * new Vector2(1f, 0.5f);
        ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch, font, text, textDrawPosition, DialogColorRegistry.NamelessDeityTextColor, 0f, Vector2.Zero, new(scale), -1f, 2f);
    }

    public void DrawTopHat(Vector2 screenPos)
    {
        Texture2D topHat = GennedAssets.Textures.NamelessDeity.TopHat.Value;
        Vector2 topHatDrawPosition = IdealCensorPosition - screenPos - Vector2.UnitY.RotatedBy(NPC.rotation) * TeleportVisualsAdjustedScale * 240f;
        Main.EntitySpriteDraw(topHat, topHatDrawPosition, null, NPC.GetAlpha(Color.White), NPC.rotation, topHat.Size() * 0.5f, TeleportVisualsAdjustedScale * 0.7f, 0);
    }

    public void DrawDeathAnimationCutscene(Vector2 screenPos)
    {
        // Draw the universal black overlay if necessary.
        bool screenShattered = NPC.ai[2] == 1f;
        if (UniversalBlackOverlayInterpolant > 0f)
        {
            Vector2 overlayScale = Vector2.One * Lerp(0.1f, 15f, UniversalBlackOverlayInterpolant);
            Color overlayColor = ZPosition <= -0.7f ? Color.Transparent : Color.Black;
            if (screenShattered)
            {
                float overlayInterpolant = Sin01(Main.GlobalTimeWrappedHourly * 7f) * 0.37f + 0.43f;
                overlayInterpolant = Lerp(overlayInterpolant, 1f, InverseLerp(240f, 0f, TimeSinceScreenSmash));
                overlayColor = Color.Lerp(Color.Wheat, Color.White, overlayInterpolant);

                Main.spriteBatch.PrepareForShaders(BlendState.NonPremultiplied);

                ManagedShader staticShader = ShaderManager.GetShader("NoxusBoss.StaticOverlayShader");
                staticShader.TrySetParameter("staticInterpolant", Pow(InverseLerp(0f, 240f, TimeSinceScreenSmash), 2f));
                staticShader.TrySetParameter("staticZoomFactor", InverseLerp(0f, 210f, TimeSinceScreenSmash) * 6f + 3f);
                staticShader.TrySetParameter("neutralizationInterpolant", 0f);
                staticShader.SetTexture(MulticoloredNoise, 1, SamplerState.PointWrap);
                staticShader.Apply();

                overlayScale = Vector2.One * Utils.Remap(TimeSinceScreenSmash, 0f, 210f, 6f, 10f);
            }

            for (int i = 0; i < 3; i++)
                Main.spriteBatch.Draw(BloomCircle, Main.ScreenSize.ToVector2() * 0.5f, null, overlayColor * Sqrt(UniversalBlackOverlayInterpolant), 0f, BloomCircle.Size() * 0.5f, overlayScale, 0, 0f);

            if (screenShattered)
                Main.spriteBatch.ResetToDefault();
        }

        // Draw extra text about terminating the attack if Nameless has been defeated before.
        if (Main.netMode == NetmodeID.SinglePlayer && BossDownedSaveSystem.HasDefeated<NamelessDeityBoss>() && CurrentState == NamelessAIType.DeathAnimation && NPC.ai[3] == 1f)
            DrawDeathAnimationTerminationText();

        // Draw congratulatory text if necessary.
        DynamicSpriteFont font = FontRegistry.Instance.NamelessDeityText;
        if (DrawCongratulatoryText)
        {
            string text = Language.GetTextValue("Mods.NoxusBoss.Dialog.NamelessDeityEndScreenText");
            float scale = 0.8f;
            float maxHeight = 225f;
            Vector2 textSize = font.MeasureString(text);
            if (textSize.Y > maxHeight)
                scale = maxHeight / textSize.Y;
            Vector2 textDrawPosition = Main.ScreenSize.ToVector2() * 0.5f - textSize * scale * 0.5f;
            ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch, font, text, textDrawPosition, DialogColorRegistry.NamelessDeityTextColor, 0f, Vector2.Zero, new(scale), -1f, 2f);
        }
        else if (UniversalBlackOverlayInterpolant >= 1f && !screenShattered)
            RenderComposite.Find<ArmsStep>().RenderArms(NPC, false);
    }

    public static void ApplyHealthyGrayscaleEffect(Texture2D target)
    {
        ManagedShader blueShader = ShaderManager.GetShader("NoxusBoss.GrayscaleShader");
        blueShader.Apply();
    }

    public static void ApplyMoonburnBlueEffect(Texture2D target)
    {
        ManagedShader blueShader = ShaderManager.GetShader("NoxusBoss.MoonburnBlueOverlayShader");
        blueShader.TrySetParameter("swapHarshness", 0.85f);
        blueShader.Apply();
    }

    public static void ApplyMyraGoldEffect(Texture2D target)
    {
        ManagedShader goldShader = ShaderManager.GetShader("NoxusBoss.MyraGoldOverlayShader");
        goldShader.TrySetParameter("swapHarshness", 0.5f);
        goldShader.Apply();
    }
}
