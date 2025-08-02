using Luminance.Core.Graphics;
using Luminance.Core.Hooking;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.Particles;

public class StringLineParticleRenderer : ManualParticleRenderer<StringLineParticle>, IExistingDetourProvider
{
    internal static ManagedRenderTarget DownscaledTarget;

    public override void RenderParticles() { }

    public void Subscribe()
    {
        On_Main.DrawDust += DrawSelfByDefault;

        if (Main.netMode != NetmodeID.Server)
        {
            DownscaledTarget = new(true, (width, height) => new(Main.instance.GraphicsDevice, width / 2, height / 2));
            RenderTargetManager.RenderTargetUpdateLoopEvent += RenderParticlesToTarget;
        }
    }

    private void RenderParticlesToTarget()
    {
        if ((ModContent.GetInstance<StringLineParticleRenderer>().Particles?.Count ?? 0) <= 0)
            return;

        GraphicsDevice graphicsDevice = Main.instance.GraphicsDevice;
        graphicsDevice.SetRenderTarget(DownscaledTarget);
        graphicsDevice.Clear(Color.Transparent);

        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.CreateScale(0.5f));

        foreach (StringLineParticle particle in ModContent.GetInstance<StringLineParticleRenderer>().Particles)
            Main.spriteBatch.Draw(WhitePixel, particle.Position - Main.screenPosition, null, particle.DrawColor * particle.Opacity, particle.Rotation, WhitePixel.Size() * 0.5f, particle.Scale, 0, 0f);

        Main.spriteBatch.End();

        RenderParticles();
    }

    public void Unsubscribe() => On_Main.DrawDust -= DrawSelfByDefault;

    private static void DrawSelfByDefault(On_Main.orig_DrawDust orig, Main self)
    {
        orig(self);

        if ((ModContent.GetInstance<StringLineParticleRenderer>().Particles?.Count ?? 0) <= 0)
            return;

        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        Main.spriteBatch.Draw(DownscaledTarget, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White);
        Main.spriteBatch.End();
    }
}
