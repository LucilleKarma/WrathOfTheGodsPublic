using Luminance.Core.Graphics;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Core.Graphics.RenderTargets;

using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers.ParadiseReclaimed;

public class ParadiseStaticLayerHandlers : ModSystem
{
    internal static InstancedRequestableTarget layerTarget = new InstancedRequestableTarget();

    internal static List<ParadiseStaticLayer> layers = new List<ParadiseStaticLayer>(4);

    public override void OnModLoad()
    {
        Main.ContentThatNeedsRenderTargets.Add(layerTarget);
        Main.OnPreDraw += ProcessRenderQueues;
        Main.OnPostDraw += RenderFrontLayer;
        On_Main.DrawProjectiles += RenderBackLayers;
    }

    public override void OnModUnload()
    {
        Main.OnPreDraw -= ProcessRenderQueues;
        Main.OnPostDraw -= RenderFrontLayer;
    }

    private static void RenderFrontLayer(GameTime obj)
    {
        if (!ShaderManager.HasFinishedLoading)
            return;
        if (Main.mapFullscreen || Main.gameMenu)
            return;

        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
        GetLayerByDepth(0).Render(Color.White);
        Main.spriteBatch.End();
    }

    private static void RenderBackLayers(On_Main.orig_DrawProjectiles orig, Main self)
    {
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
        for (int i = layers.Count - 1; i >= 1; i--)
        {
            Color color = Color.Lerp(Color.White, new Color(46, 46, 46), Pow(i / (float)(layers.Count - 1f), 0.8f));
            GetLayerByDepth(i).Render(color);
        }
        Main.spriteBatch.End();

        orig(self);
    }

    private static void ProcessRenderQueues(GameTime obj)
    {
        if (AvatarOfEmptiness.Myself is not null)
        {
            AvatarOfEmptiness avatar = AvatarOfEmptiness.Myself.As<AvatarOfEmptiness>();
            if (avatar.ParadiseReclaimed_RenderStaticWallYPosition != 0f)
                avatar.DoBehavior_ParadiseReclaimed_DrawThreadActions();
        }

        foreach (ParadiseStaticLayer layer in layers)
        {
            Queue<Action> renderQueue = layer.IntoTargetRenderQueue;
            if (renderQueue.Count >= 1)
            {
                layerTarget.Request((int)ViewportSize.X, (int)ViewportSize.Y, layer.Depth, () =>
                {
                    Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
                    while (renderQueue.TryDequeue(out Action? action))
                        action();

                    // Render particles.
                    if (layer.ParticleSystem.particles.Any(p => p.Active))
                        layer.ParticleSystem.RenderAll();

                    Main.spriteBatch.End();
                });
            }
        }
    }

    public override void PreUpdateProjectiles()
    {
        if (Main.netMode == NetmodeID.Server) return;

        foreach (ParadiseStaticLayer layer in layers)
        {
            if (layer.ParticleSystem.particles.Any(p => p.Active))
                layer.ParticleSystem.UpdateAll();
        }
    }

    /// <summary>
    /// Gets a layer with a given depth value, creating a new one if necessary.
    /// </summary>
    /// <param name="depth">The depth value to search for.</param>
    public static ParadiseStaticLayer GetLayerByDepth(int depth)
    {
        ParadiseStaticLayer? layer = layers.FirstOrDefault(l => l.Depth == depth);
        if (layer is null)
        {
            layer = new ParadiseStaticLayer()
            {
                Depth = depth
            };
            layers.Add(layer);
        }

        return layer;
    }
}
