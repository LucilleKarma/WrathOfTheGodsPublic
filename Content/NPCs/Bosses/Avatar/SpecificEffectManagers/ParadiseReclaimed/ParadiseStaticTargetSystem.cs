using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Core.Configuration;
using NoxusBoss.Core.Graphics.RenderTargets;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers.ParadiseReclaimed;

public class ParadiseStaticTargetSystem : ModSystem
{
    internal static InstancedRequestableTarget staticTarget = new InstancedRequestableTarget();

    /// <summary>
    /// The render target that contains the static.
    /// </summary>
    public static RenderTarget2D? StaticTarget
    {
        get
        {
            staticTarget.Request(Main.instance.GraphicsDevice.Viewport.Width, Main.instance.GraphicsDevice.Viewport.Height, 0, () =>
            {
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

                // Prepare the static shader.
                ManagedShader staticShader = ShaderManager.GetShader("NoxusBoss.StaticOverlayShader");
                staticShader.TrySetParameter("staticInterpolant", 1.1f);
                staticShader.TrySetParameter("staticZoomFactor", 4.5f);
                staticShader.TrySetParameter("neutralizationInterpolant", WoTGConfig.Instance.PhotosensitivityMode ? 0.7f : 0f);
                staticShader.TrySetParameter("scrollTimeFactor", WoTGConfig.Instance.PhotosensitivityMode ? -0.985f : -0.5f);
                staticShader.SetTexture(GrainyNoise, 1, SamplerState.PointWrap);
                staticShader.Apply();

                // Draw the static.
                Main.spriteBatch.Draw(WhitePixel, new Rectangle(0, 0, Main.instance.GraphicsDevice.Viewport.Width, Main.instance.GraphicsDevice.Viewport.Height), Color.White);

                Main.spriteBatch.End();
            });

            if (staticTarget.TryGetTarget(0, out RenderTarget2D? target) && target is not null)
                return target;

            return null;
        }
    }

    public override void OnModLoad() => Main.ContentThatNeedsRenderTargets.Add(staticTarget);
}
