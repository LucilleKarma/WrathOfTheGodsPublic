using Luminance.Core.Graphics;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using NoxusBoss.Core.Graphics.FastParticleSystems;

using Terraria;
using Terraria.ID;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers.ParadiseReclaimed;

public class ParadiseStaticLayer
{
    /// <summary>
    /// The depth of this layer. Higher values correspond to rendering further back.
    /// </summary>
    public int Depth;

    /// <summary>
    /// The particle system associated with this layer.
    /// </summary>
    public readonly FastParticleSystem ParticleSystem;

    /// <summary>
    /// The set of actions that should be performed by this layer when going into the render target.
    /// </summary>
    public readonly Queue<Action> IntoTargetRenderQueue = [];

    /// <summary>
    /// The set of actions that should be performed by this layer alongside the final render target. Used for rendering things at a given layer without being modified with a post-processing shader.
    /// </summary>
    public readonly Queue<Action> IndependentRenderQueue = [];

    public ParadiseStaticLayer()
    {
        if (Main.netMode != NetmodeID.Server)
        {
            ParticleSystem = FastParticleSystemManager.CreateNew(1024, PrepareParticleRendering, ExtraParticleUpdates);
        }
    }

    private void PrepareParticleRendering()
    {
        Matrix world = Matrix.CreateTranslation(-Main.screenPosition.X, -Main.screenPosition.Y, 1f + Depth);
        Matrix projection = Matrix.CreateOrthographicOffCenter(0f, Main.screenWidth, Main.screenHeight, 0f, -100f, 100f);

        Texture2D circle = ParticleLight;
        ManagedShader overlayShader = ShaderManager.GetShader("NoxusBoss.BasicPrimitiveOverlayShader");
        overlayShader.TrySetParameter("uWorldViewProjection", world * Main.GameViewMatrix.TransformationMatrix * projection);
        overlayShader.SetTexture(circle, 1, SamplerState.LinearClamp);
        overlayShader.Apply();
    }

    private static void ExtraParticleUpdates(ref FastParticle particle)
    {
        particle.Size *= 0.9f;
        particle.Velocity.X *= 0.99f;
        particle.Velocity.Y += 0.15f;
        if ((particle.Size.X + particle.Size.Y) * 0.5f <= 2f)
            particle.Active = false;
    }

    /// <summary>
    /// Renders this layer.
    /// </summary>
    /// <param name="color">The general color of the layer.</param>
    public void Render(Color color)
    {
        if (IndependentRenderQueue.Count <= 0 && IntoTargetRenderQueue.Count <= 0)
            return;

        Main.spriteBatch.PrepareForShaders();

        // Exhaust the independent render queue.
        while (IndependentRenderQueue.TryDequeue(out Action? action))
            action();

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

        // Render static atop the base.
        Texture2D staticLayer = ParadiseStaticTargetSystem.StaticTarget ?? WhitePixel.Value;
        if (ParadiseStaticLayerHandlers.layerTarget.TryGetTarget(Depth, out RenderTarget2D? target) && target is not null)
        {
            ManagedShader shader = ShaderManager.GetShader("Luminance.MetaballEdgeShader");
            shader.TrySetParameter("layerSize", staticLayer.Size());
            shader.TrySetParameter("screenSize", Main.ScreenSize.ToVector2());
            shader.TrySetParameter("layerOffset", Vector2.Zero);
            shader.TrySetParameter("edgeColor", Color.Transparent);
            shader.TrySetParameter("singleFrameScreenOffset", (Main.screenLastPosition - Main.screenPosition) / Main.ScreenSize.ToVector2());
            shader.SetTexture(staticLayer, 1, SamplerState.LinearWrap);
            shader.Apply();

            Main.spriteBatch.Draw(target, Vector2.Zero, color);
        }
    }
}
