using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Core.World.GameScenes.TerminusStairway;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace NoxusBoss.Content.MainMenuThemes;

// I hate this internal name but it's necessary to ensure that the name is ordered after VisceralDimensionMainMenu.
public class XAscentMainMenu : ModMenu
{
    public override string DisplayName => Language.GetTextValue($"Mods.{Mod.Name}.MenuThemes.AscentMainMenu.DisplayName");

    public override int Music => MusicLoader.GetMusicSlot(Mod, "Assets/Sounds/Music/ascent");

    public override bool PreDrawLogo(SpriteBatch spriteBatch, ref Vector2 logoDrawCenter, ref float logoRotation, ref float logoScale, ref Color drawColor)
    {
        if (!ShaderManager.HasFinishedLoading)
            return false;

        Main.time = 9600D;
        Main.dayTime = true;
        TerminusStairwaySky.CompletionInterpolant = 0f;
        TerminusStairwaySky.CreateClouds();
        TerminusStairwaySky.Render();
        return true;
    }
}
