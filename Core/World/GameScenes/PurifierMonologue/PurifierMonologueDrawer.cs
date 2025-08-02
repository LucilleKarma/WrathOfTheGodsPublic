using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;
using static NoxusBoss.Core.Graphics.SpecificEffectManagers.TotalScreenOverlaySystem;

namespace NoxusBoss.Core.World.GameScenes.PurifierMonologue;

[Autoload(Side = ModSide.Client)]
public class PurifierMonologueDrawer : ModSystem
{
    public static int TimeSinceMonologueBegan
    {
        get;
        set;
    }

    public static int TimeSinceWorldgenFinished
    {
        get;
        set;
    }

    public override void OnModLoad()
    {
        DrawAfterWhiteEvent += DrawWorldgenMonologue;
        EffectInactiveEvent += () => TimeSinceMonologueBegan = 0;
        On_SoundEngine.PlaySound_int_int_int_int_float_float += DisableSoundsDuringMonologue;
    }

    public override void PostUpdateEverything()
    {
        // Edge case: If the world is being regenerated due to the Purifier, keep everything white.
        // This hook is only called if the world is active, so it won't interfere with natural world generation, only the special type of world generation the Purifier performs.
        if (WorldGen.generatingWorld)
            OverlayInterpolant = 1f;
    }

    private SoundEffectInstance? DisableSoundsDuringMonologue(On_SoundEngine.orig_PlaySound_int_int_int_int_float_float orig, int type, int x, int y, int Style, float volumeScale, float pitchOffset)
    {
        if (Main.gameMenu || TimeSinceMonologueBegan <= 60)
            return orig(type, x, y, Style, volumeScale, pitchOffset);

        return null;
    }

    private void DrawWorldgenMonologue()
    {
        // Temporarily lock the generating world bool in place once the world has finished regenerating, so that everything can smoothly fade out.
        if (TimeSinceWorldgenFinished >= 1)
            WorldGen.generatingWorld = true;

        // Draw a silly monologue if the world is being regenerated due to the Purifier.
        // If not, reset timers.
        if (!WorldGen.generatingWorld)
        {
            TimeSinceMonologueBegan = 0;
            return;
        }

        // Increment the monologue timer.
        TimeSinceMonologueBegan++;

        // Increment the post-worldgen timer if it's been activated.
        if (TimeSinceWorldgenFinished >= 1)
            TimeSinceWorldgenFinished++;
        if (TimeSinceWorldgenFinished >= 240)
        {
            WorldGen.generatingWorld = false;
            WorldGen.SaveAndQuit();
            OverlayInterpolant = 0f;
            TimeSinceWorldgenFinished = 0;
        }

        // Draw credits on the bottom right of the screen.
        DynamicSpriteFont font = FontAssets.MouseText.Value;
        int textLineCounter = 0;
        float creditTextScale = 1.2f;
        string creditText = Language.GetTextValue($"Mods.{Mod.Name}.Dialog.LofiMusicCreditText");
        Color textColor = Color.Black * InverseLerp(210f, 450f, TimeSinceMonologueBegan) * InverseLerp(210f, 60f, TimeSinceWorldgenFinished);
        foreach (string creditLine in creditText.Split('\n'))
        {
            Vector2 creditTextSize = font.MeasureString(creditLine);
            Vector2 creditDrawPosition = new Vector2(Main.screenWidth - 110f, Main.screenHeight - 90f) - Vector2.UnitX * creditTextSize * 0.5f + Vector2.UnitY * textLineCounter * creditTextScale * 32f;
            Main.spriteBatch.DrawString(font, creditLine, creditDrawPosition, textColor, 0f, Vector2.UnitY * creditTextSize * 0.5f, creditTextScale, 0, 0f);
            textLineCounter++;
        }
        textLineCounter = 0;

        // Draw the funny rant text.
        string rantText = Language.GetTextValue($"Mods.{Mod.Name}.Dialog.PurifierEntertainingRantText");
        foreach (string rantLine in Utils.WordwrapString(rantText, font, 560, 200, out _))
        {
            if (string.IsNullOrEmpty(rantLine))
                continue;

            Vector2 rantTextSize = font.MeasureString(rantLine);
            Vector2 rantDrawPosition = new Vector2(Main.screenWidth * 0.5f, Main.screenHeight - 50f) - Vector2.UnitX * rantTextSize * 0.5f + Vector2.UnitY * textLineCounter * creditTextScale * 32f;
            rantDrawPosition.Y -= TimeSinceMonologueBegan * 0.45f - 150f;

            Main.spriteBatch.DrawString(font, rantLine, rantDrawPosition, textColor, 0f, Vector2.UnitY * rantTextSize * 0.5f, creditTextScale, 0, 0f);
            textLineCounter++;
        }
    }
}
