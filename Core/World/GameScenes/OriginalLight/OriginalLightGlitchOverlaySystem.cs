using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity;
using NoxusBoss.Core.Configuration;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Core.World.GameScenes.OriginalLight;

[Autoload(Side = ModSide.Client)]
public class OriginalLightGlitchOverlaySystem : ModSystem
{
    public static float OverlayInterpolant
    {
        get;
        set;
    }

    public static float GlitchIntensity
    {
        get;
        set;
    }

    public static float EyeOverlayOpacity
    {
        get;
        set;
    }

    public static float WhiteOverlayInterpolant
    {
        get;
        set;
    }

    // These are immediately loaded to ensure that the animation plays smoothly.
    public static Texture2D[] LightFrames
    {
        get;
        private set;
    }

    public override void OnModLoad()
    {
        if (Main.netMode != NetmodeID.Server)
        {
            LightFrames = new Texture2D[15];
            for (int i = 0; i < LightFrames.Length; i++)
                LightFrames[i] = ModContent.Request<Texture2D>(GetAssetPath("Skies/NamelessDeity/TheOriginalLight", $"frame{i + 1}"), AssetRequestMode.ImmediateLoad).Value;
        }

        Main.OnPostDraw += DrawOverlay;
    }

    public override void OnModUnload() => Main.OnPostDraw -= DrawOverlay;

    public override void PreUpdateEntities()
    {
        // Get rid of the overlay if Nameless is not present.
        // Otherwise, though, it is Nameless' responsibility to turn this effect off.
        if (NamelessDeityBoss.Myself is null)
        {
            OverlayInterpolant = 0f;
            GlitchIntensity = 0f;
            EyeOverlayOpacity = 0f;
        }

        // Make the white overlay rapidly dissipate.
        WhiteOverlayInterpolant = Saturate(WhiteOverlayInterpolant * 0.92f - 0.056f);
        if (WhiteOverlayInterpolant >= 0.001f)
            TotalScreenOverlaySystem.OverlayInterpolant = WhiteOverlayInterpolant;
    }

    private void DrawOverlay(GameTime obj)
    {
        if (OverlayInterpolant <= 0f || Main.gameMenu)
            return;

        // Disable this effect if the player has photosensitivity mode enabled, since the animations performed during this are very fast.
        if (WoTGConfig.Instance.PhotosensitivityMode)
            return;

        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

        // Determine Nameless' attack timer. The longer the attack persists, the more intense the jitter effects are.
        float aiTimer = NamelessDeityBoss.Myself.As<NamelessDeityBoss>().AITimer;

        // Calcuate the general opacity of everything. This sharply contrasts the white overlay system's white interpolant, so that it takes priority when drawing.
        float generalOpacity = Pow(1f - TotalScreenOverlaySystem.OverlayInterpolant, 4f);
        DrawLightTextureOverlay(aiTimer, generalOpacity, out Vector2 lightScale);

        // Disable the shader so that the eye can be overlayed if necessary.
        Main.spriteBatch.End();
        Main.spriteBatch.Begin();

        // Draw Nameless' eye if necessary.
        Texture2D namelessEye = GennedAssets.Textures.NamelessDeity.NamelessDeityEye.Value;
        Vector2 eyeOffset = -Vector2.UnitY * Main.rand.NextVector2Circular(1f, 1f) * lightScale.X * aiTimer * 0.125f;
        Main.spriteBatch.Draw(namelessEye, Main.ScreenSize.ToVector2() * 0.5f + eyeOffset, null, (Color.White with { A = 0 }) * generalOpacity * EyeOverlayOpacity * 1.2f, 0f, namelessEye.Size() * 0.5f, lightScale.X * 0.67f, 0, 0f);

        Main.spriteBatch.End();
    }

    public static void DrawLightTextureOverlay(float attackTimer, float generalOpacity, out Vector2 lightScale)
    {
        // Calculate information about the draw effect. The frames interpolate based on Nameless' attack timer from above, and the effect is rotated 180 degrees when the glitch is active.
        int lightFrame = Utils.Clamp((int)(attackTimer * 0.44f) + 1, 1, 15);
        SpriteEffects direction = SpriteEffects.None;
        if (GlitchIntensity >= 0.01f)
            direction = SpriteEffects.FlipHorizontally | SpriteEffects.FlipVertically;

        // Collect the light texture and calculate how much it needs to be scaled by to cover the entire screen.
        Texture2D theOriginalLight = LightFrames[lightFrame - 1];
        lightScale = Main.ScreenSize.ToVector2() / theOriginalLight.Size() * 1.05f;

        // Draw the base background.
        PrepareNoiseShader();
        Main.spriteBatch.Draw(theOriginalLight, Main.ScreenSize.ToVector2() * 0.5f, null, Color.White * generalOpacity * OverlayInterpolant, 0f, theOriginalLight.Size() * 0.5f, lightScale, direction, 0f);

        // Draw a second, additive layer on top of the background that jitters.
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive);
        PrepareNoiseShader();

        Vector2 jitterOffset = Main.rand.NextVector2Unit() * Pow(attackTimer, 1.42f) * 0.09f;
        Main.spriteBatch.Draw(theOriginalLight, Main.ScreenSize.ToVector2() * 0.5f + jitterOffset, null, Color.White * generalOpacity * OverlayInterpolant * 0.8f, 0f, theOriginalLight.Size() * 0.5f, lightScale, direction, 0f);
    }

    private static void PrepareNoiseShader()
    {
        ManagedShader glitchShader = ShaderManager.GetShader("NoxusBoss.GlitchShader");
        glitchShader.SetTexture(SharpNoise, 1);
        glitchShader.TrySetParameter("coordinateZoomFactor", Vector2.One * 0.5f);
        glitchShader.TrySetParameter("glitchInterpolant", GlitchIntensity);
        glitchShader.Apply();
    }
}
