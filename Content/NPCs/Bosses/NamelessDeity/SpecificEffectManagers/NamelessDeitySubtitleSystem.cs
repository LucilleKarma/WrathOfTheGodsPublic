using Microsoft.Xna.Framework;
using NoxusBoss.Assets.Fonts;
using NoxusBoss.Core.Graphics;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;

public abstract class NamelessDeitySubtitleSystem : ModSystem
{
    public abstract List<NamelessDeityDialog> Sentences
    {
        get;
    }

    public abstract float SubtitleDisappearTime
    {
        get;
    }

    public int DialogueTimer
    {
        get;
        protected set;
    }

    public override void OnModLoad() => Main.OnPostDraw += DrawSubtitles;

    private void DrawSubtitles(GameTime time)
    {
        // Make the animation stop if on the game menu.
        if (Main.gameMenu)
            DialogueTimer = 0;

        if (DialogueTimer <= 0 || DialogueTimer >= SubtitleDisappearTime)
            return;

        Main.spriteBatch.Begin();

        ReLogic.Graphics.DynamicSpriteFont font = FontRegistry.Instance.NamelessDeityText;
        NamelessDeityDialog currentlyUsedSentence = Sentences.Last(s => DialogueTimer >= SecondsToFrames(s.DialogDelay)) ?? Sentences.Last();
        string subtitleText = Language.GetTextValue($"Mods.NoxusBoss.Subtitles.{currentlyUsedSentence.TextKey}");
        Color textColor = DialogColorRegistry.NamelessDeityTextColor;
        Vector2 scale = Vector2.One * Main.instance.GraphicsDevice.Viewport.Width / 2560f * 0.64f;
        Vector2 drawPosition = new Vector2(Main.instance.GraphicsDevice.Viewport.Width * 0.5f, Main.instance.GraphicsDevice.Viewport.Height * 0.75f);
        ChatManager.DrawColorCodedString(Main.spriteBatch, font, subtitleText, drawPosition, textColor, 0f, font.MeasureString(subtitleText) * 0.5f, scale);

        Main.spriteBatch.End();
    }
}
