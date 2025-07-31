using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using NoxusBoss.Assets;
using NoxusBoss.Assets.Fonts;
using NoxusBoss.Core.Graphics.GeneralScreenEffects;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.Localization;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Graphics.UI;

public class TerminusHintAnimationScene : ModSceneEffect
{
    public override bool IsSceneEffectActive(Player player) => TerminusHintAnimationSystem.AnimationTimer >= 1;

    public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;

    public override int Music => TerminusHintAnimationSystem.AnimationTimer >= 3 || TerminusHintAnimationSystem.AnimationDelay >= TerminusHintAnimationSystem.SolynAmbienceTime ?
        0 :
        MusicLoader.GetMusicSlot(Mod, "Assets/Sounds/Music/SolynDeathInterlude");
}

[Autoload(Side = ModSide.Client)]
public class TerminusHintAnimationSystem : ModSystem
{
    public static int AnimationTimer
    {
        get;
        private set;
    }

    public static int AnimationDelay
    {
        get;
        private set;
    }

    public static int SolynAmbienceTime => SecondsToFrames(26.163f);

    public static string[] TextLines => new string[]
    {
        Language.GetTextValue("Mods.NoxusBoss.Dialog.TerminusHintLine1"),
        Language.GetTextValue("Mods.NoxusBoss.Dialog.TerminusHintLine2")
    };

    public override void OnModLoad() => Main.OnPostDraw += DrawAnimation;

    public override void OnModUnload() => Main.OnPostDraw -= DrawAnimation;

    private void DrawAnimation(GameTime obj)
    {
        // Make the animation stop if on the game menu.
        if (Main.gameMenu)
            AnimationDelay = 0;

        // Don't do anything if the animation timer has not been started.
        if (AnimationDelay <= 0)
            return;

        // Don't do anything if the game isn't being focused on.
        if (!Main.instance.IsActive)
            return;

        float animationCompletion = InverseLerp(0f, 540f, AnimationTimer);
        float whiteOverlayOpacity = InverseLerpBump(0f, 0.19f, 0.875f, 1f, animationCompletion);

        // Stop the animation once it has completed.
        if (animationCompletion >= 1f)
        {
            AnimationTimer = 0;
            AnimationDelay = 0;
            return;
        }

        Main.spriteBatch.Begin();

        // Draw the white overlay.
        Vector2 pixelScale = Main.ScreenSize.ToVector2() * 2f / WhitePixel.Size();
        Main.spriteBatch.Draw(WhitePixel, Main.ScreenSize.ToVector2() * 0.5f, null, Color.White * whiteOverlayOpacity, 0f, WhitePixel.Size() * 0.5f, pixelScale, 0, 0f);

        // Draw the text that directs the player to seek the Terminus.
        DynamicSpriteFont font = FontRegistry.Instance.NamelessDeityText;
        for (int i = 0; i < TextLines.Length; i++)
        {
            string line = TextLines[i];
            float appearDelay = i * 0.255f;
            float lineTextOpacity = InverseLerpBump(appearDelay + 0.15f, appearDelay + 0.25f, 0.85f, 0.95f, animationCompletion);
            Vector2 drawPosition = Main.ScreenSize.ToVector2() * 0.5f - font.MeasureString(line) * 0.5f + Vector2.UnitY * i * 50f;
            Main.spriteBatch.DrawString(font, line, drawPosition, DialogColorRegistry.NamelessDeityTextColor * lineTextOpacity);
        }

        if (AnimationDelay < SolynAmbienceTime + 480)
        {
            AnimationDelay++;
            if (AnimationDelay == SolynAmbienceTime + 480)
                SoundEngine.PlaySound(GennedAssets.Sounds.Environment.AvatarDefeatNamelessDeityMessageAmbience with { Volume = 1.5f });
        }
        else
            AnimationTimer++;
        Main.spriteBatch.End();
    }

    public static void Start()
    {
        AnimationTimer = 1;
        AnimationDelay = 1;
        ScreenShakeSystem.StartShake(9f);
        GeneralScreenEffectSystem.ChromaticAberration.Start(Main.LocalPlayer.Center - Vector2.UnitY * 450f, 1.5f, 40);
    }
}
