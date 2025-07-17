using Luminance.Core.Graphics;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using NoxusBoss.Core.DataStructures;

using Terraria;
using Terraria.Graphics;
using Terraria.Graphics.Renderers;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Graphics.Players;

[Autoload(Side = ModSide.Client)]
public class PlayerPostProcessingShaderSystem : ModSystem
{
    private static bool renderingToTargets;

    internal static Dictionary<int, ManagedRenderTarget> PlayerTargets
    {
        get;
        private set;
    } = new(1);

    internal static Dictionary<int, ManagedRenderTarget> AuxiliaryTargets
    {
        get;
        private set;
    } = new(1);

    internal static Dictionary<int, ManagedRenderTarget?> FinalPlayerTargets = new Dictionary<int, ManagedRenderTarget?>(1);

    /// <summary>
    /// The name of the variable that holds all per-player post-processing effects.
    /// </summary>
    public const string PostProcessingEffectsVariableName = "PostProcessingEffects";

    public override void OnModLoad()
    {
        RenderTargetManager.RenderTargetUpdateLoopEvent += UpdateTargets;
        On_LegacyPlayerRenderer.DrawPlayerFull += RenderPlayerWithPostProcessing;
    }

    private void RenderPlayerWithPostProcessing(On_LegacyPlayerRenderer.orig_DrawPlayerFull orig, LegacyPlayerRenderer self, Camera camera, Player drawPlayer)
    {
        bool usingPostProcessingEffects = FinalPlayerTargets.TryGetValue(drawPlayer.whoAmI, out ManagedRenderTarget? finalTarget) && finalTarget is not null;
        if (renderingToTargets || !usingPostProcessingEffects)
        {
            orig(self, camera, drawPlayer);
            return;
        }

        Main.spriteBatch.Begin();
        Main.spriteBatch.Draw(finalTarget, Main.screenLastPosition - Main.screenPosition, Color.White);
        Main.spriteBatch.End();
    }

    private static void VerifyPlayerTargetSizes()
    {
        foreach (var player in Main.ActivePlayers)
        {
            if (!PlayerTargets.ContainsKey(player.whoAmI))
            {
                PlayerTargets.Add(player.whoAmI, new(true, ManagedRenderTarget.CreateScreenSizedTarget));
            }

            if (!AuxiliaryTargets.ContainsKey(player.whoAmI))
            {
                AuxiliaryTargets.Add(player.whoAmI, new(true, ManagedRenderTarget.CreateScreenSizedTarget));
            }
        }
    }

    private static void UpdateTargets()
    {
        if (Main.gameMenu)
            return;

        VerifyPlayerTargetSizes();

        FinalPlayerTargets.Clear();

        renderingToTargets = true;
        foreach (var player in Main.ActivePlayers)
        {
            GraphicsDevice gd = Main.instance.GraphicsDevice;

            gd.SetRenderTarget(PlayerTargets[player.whoAmI]);
            gd.Clear(Color.Transparent);

            Main.PlayerRenderer.DrawPlayers(Main.Camera, [player]);

            // Store the final targets for later.
            ManagedRenderTarget? finalTarget = ApplyAllPostProcessingEffects(player);
            FinalPlayerTargets[player.whoAmI] = finalTarget;
        }

        renderingToTargets = false;
    }

    private static ManagedRenderTarget? ApplyAllPostProcessingEffects(Player player)
    {
        ManagedRenderTarget? finalTarget = null;
        Referenced<List<PlayerPostProcessingEffect>> effectsRef = player.GetValueRef<List<PlayerPostProcessingEffect>>(PostProcessingEffectsVariableName);
        effectsRef.Value ??= [];
        List<PlayerPostProcessingEffect> effects = effectsRef.Value;

        if (effects.Count <= 0)
            return finalTarget;

        GraphicsDevice gd = Main.instance.GraphicsDevice;

        // Swap back and forth between the main and auxiliary render target, ensuring that each effect is applied successively.
        for (int i = 0; i < effects.Count; i++)
        {
            ManagedRenderTarget drawContentsTarget = i % 2 == 0 ? PlayerTargets[player.whoAmI] : AuxiliaryTargets[player.whoAmI];
            ManagedRenderTarget shaderApplicationTarget = i % 2 == 0 ? AuxiliaryTargets[player.whoAmI] : PlayerTargets[player.whoAmI];
            gd.SetRenderTarget(shaderApplicationTarget);
            gd.Clear(Color.Transparent);

            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

            effects[i].Effect(player);

            Main.spriteBatch.Draw(drawContentsTarget, Vector2.Zero, Color.White);
            Main.spriteBatch.End();

            if (i == effects.Count - 1)
                finalTarget = shaderApplicationTarget;
        }

        // Clear the player's effects for the next frame.
        effects.RemoveAll(e => !e.ClearOnWorldUpdateCycle);

        // Return the render target that holds the final draw contents.
        return finalTarget;
    }

    public override void PreUpdateEntities()
    {
        // Clear the player's effects for the next frame.
        Referenced<List<PlayerPostProcessingEffect>> effectsRef = Main.LocalPlayer.GetValueRef<List<PlayerPostProcessingEffect>>(PostProcessingEffectsVariableName);
        effectsRef.Value ??= [];
        List<PlayerPostProcessingEffect> effects = effectsRef.Value;

        effects.RemoveAll(e => e.ClearOnWorldUpdateCycle);
    }

    /// <summary>
    /// Applies a given post-processing effect to a given player for the next frame.
    /// </summary>
    /// <param name="player">The player to apply the effect to.</param>
    /// <param name="effect"></param>
    public static void ApplyPostProcessingEffect(Player player, PlayerPostProcessingEffect effect)
    {
        Referenced<List<PlayerPostProcessingEffect>> effectsRef = player.GetValueRef<List<PlayerPostProcessingEffect>>(PostProcessingEffectsVariableName);
        effectsRef.Value ??= [];
        List<PlayerPostProcessingEffect> effects = effectsRef.Value;
        effects.Add(effect);
    }
}
