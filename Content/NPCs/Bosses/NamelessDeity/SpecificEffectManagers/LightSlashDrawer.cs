using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Projectiles;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;

public class LightSlashDrawer : ModSystem
{
    /// <summary>
    /// A general-purpose countdown used to ensure that the light slashes can continue renderinging and vanish shortly after the projectiles are gone.
    /// </summary>
    public static int ContinueRenderingCountdown
    {
        get;
        private set;
    }

    /// <summary>
    /// The render target that contains all current slash vfx data.
    /// </summary>
    public static ManagedRenderTarget SlashTarget
    {
        get;
        private set;
    }

    /// <summary>
    /// The render target that contains all previous slash vfx data. Used for the purposes of blurring the overall effect.
    /// </summary>
    public static ManagedRenderTarget SlashTargetPrevious
    {
        get;
        private set;
    }

    public override void OnModLoad()
    {
        RenderTargetManager.RenderTargetUpdateLoopEvent += PrepareAfterimageTarget;
        Main.QueueMainThreadAction(() =>
        {
            SlashTarget = new(true, ManagedRenderTarget.CreateScreenSizedTarget);
            SlashTargetPrevious = new(true, ManagedRenderTarget.CreateScreenSizedTarget);
        });
    }

    private void PrepareAfterimageTarget()
    {
        if (ContinueRenderingCountdown >= 1)
            ContinueRenderingCountdown--;

        // Don't waste resources if there are no slashes.
        bool anySlashes = AnyProjectiles(ModContent.ProjectileType<LightSlash>());
        if (!anySlashes && ContinueRenderingCountdown <= 0)
            return;

        if (anySlashes)
            ContinueRenderingCountdown = 30;

        GraphicsDevice gd = Main.instance.GraphicsDevice;

        // Prepare the render target for drawing.
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.Default, RasterizerState.CullNone, null, Matrix.Identity);
        gd.SetRenderTarget(SlashTargetPrevious);
        gd.Clear(Color.Transparent);

        // Draw the contents of the previous frame to the target.
        // The color represents exponential decay factors for each RGBA component, since this performs repeated draws across multiple frames.
        Main.spriteBatch.Draw(SlashTarget, Vector2.Zero, new Color(0.52f, 0.95f, 0.95f, 0.94f));

        // Draw the blur shader to the result.
        ApplyBlurEffects();

        // Draw all slash projectiles to the render target.
        DrawAllSlashes();

        // Return to the backbuffer.
        Main.spriteBatch.End();
        gd.SetRenderTarget(null);

        PrepareScreenShader();
    }

    private static void DrawAllSlashes()
    {
        int slashID = ModContent.ProjectileType<LightSlash>();
        foreach (Projectile p in Main.ActiveProjectiles)
        {
            if (p.type != slashID)
                continue;

            p.As<LightSlash>().DrawToTarget();
        }
    }

    private static void ApplyBlurEffects()
    {
        GraphicsDevice gd = Main.instance.GraphicsDevice;
        gd.SetRenderTarget(SlashTarget);
        gd.Clear(Color.Transparent);

        // Prepare the blur shader.
        ManagedShader blurShader = ShaderManager.GetShader("NoxusBoss.GaussianBlurShader");
        blurShader.TrySetParameter("blurOffset", 0.0032f);
        blurShader.TrySetParameter("colorMask", Vector4.One);
        blurShader.TrySetParameter("invert", false);
        blurShader.Apply();

        Main.spriteBatch.Draw(SlashTargetPrevious, Vector2.Zero, Color.White);
    }

    private static void PrepareScreenShader()
    {
        float vignetteInterpolant = 0f;
        if (NamelessDeityBoss.Myself_CurrentState == NamelessDeityBoss.NamelessAIType.DarknessWithLightSlashes)
            vignetteInterpolant = NamelessDeityBoss.Myself?.ai[2] ?? 0f;

        ManagedScreenFilter slashShader = ShaderManager.GetFilter("NoxusBoss.LightSlashesOverlayShader");
        slashShader.TrySetParameter("splitBrightnessFactor", 3.2f);
        slashShader.TrySetParameter("splitTextureZoomFactor", 0.75f);
        slashShader.TrySetParameter("backgroundOffset", (Main.screenPosition - Main.screenLastPosition) / Main.ScreenSize.ToVector2());
        slashShader.TrySetParameter("vignetteInterpolant", vignetteInterpolant);
        slashShader.SetTexture(SlashTarget, 1, SamplerState.AnisotropicClamp);
        slashShader.SetTexture(GennedAssets.Textures.Extra.DivineLight, 2, SamplerState.AnisotropicWrap);
        slashShader.SetTexture(CrackedNoiseA, 3, SamplerState.AnisotropicWrap);
        slashShader.Activate();
    }
}
