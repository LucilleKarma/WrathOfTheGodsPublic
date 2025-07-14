using Microsoft.Xna.Framework;

using NoxusBoss.Content.NPCs.Bosses.Draedon.Draedon;
using NoxusBoss.Core.Netcode;
using NoxusBoss.Core.Netcode.Packets;
using NoxusBoss.Core.SolynEvents;

using ReLogic.Graphics;

using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.UI.Chat;

namespace NoxusBoss.Content.NPCs.Bosses.Draedon.DraedonDialogue;

public class DraedonAcceptanceUISystem : ModSystem
{
    /// <summary>
    /// Whether the text was previously hovered over or not.
    /// </summary>
    public static bool WasHoveringOverText
    {
        get;
        private set;
    }

    /// <summary>
    /// The opacity of the UI text.
    /// </summary>
    public static float Opacity
    {
        get;
        private set;
    }

    /// <summary>
    /// The standard color of the acceptance text.
    /// </summary>
    public static readonly Color TextColor = new Color(255, 255, 255);

    /// <summary>
    /// The color of the acceptance text when hovered over by the mouse.
    /// </summary>
    public static readonly Color TextHoverColor = new Color(255, 208, 74);

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int mouseIndex = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
        layers.Insert(mouseIndex, new LegacyGameInterfaceLayer("Wrath of the Gods: Draedon Acceptance UI", new(() =>
        {
            bool uiInUse = DrawAcceptanceUI();
            Opacity = Saturate(Opacity + uiInUse.ToDirectionInt() * 0.03f);
            return true;
        })));
    }

    public override void UpdateUI(GameTime gameTime)
    {
        if (Opacity <= 0f)
            WasHoveringOverText = false;
    }

    private static bool DrawAcceptanceUI()
    {
        int draedonIndex = NPC.FindFirstNPC(ModContent.NPCType<QuestDraedon>());
        if (draedonIndex == -1)
            return false;

        NPC draedon = Main.npc[draedonIndex];
        if (Main.myPlayer != draedon.target || !draedon.As<QuestDraedon>().WaitingOnPlayerResponse)
            return false;

        string text = Language.GetTextValue("Mods.NoxusBoss.Dialog.PlayerDraedonAcceptance");
        if (MarsCombatEvent.HasSpokenToDraedonBefore)
            text = Language.GetTextValue("Mods.NoxusBoss.Dialog.PlayerDraedonAcceptanceSuccessive");

        DynamicSpriteFont font = FontAssets.DeathText.Value;
        Vector2 origin = font.MeasureString(text) * 0.5f;
        Vector2 drawPosition = Main.ScreenSize.ToVector2() * new Vector2(0.5f, 0.6f);

        float animationValue = Cos(Main.GlobalTimeWrappedHourly * 2.1f);
        float scaleInterpolant = animationValue * 0.5f + 0.5f;
        float scaleFactor = Lerp(1f, 1.2f, scaleInterpolant);
        Vector2 textScale = Vector2.One * 0.75f;
        Rectangle textArea = Utils.CenteredRectangle(drawPosition, font.MeasureString(text) * textScale * new Vector2(1f, 0.75f));
        bool hoveringOverText = new Rectangle((int)Main.MouseScreen.X, (int)Main.MouseScreen.Y, 2, 2).Intersects(textArea);
        Color textColor = hoveringOverText ? TextHoverColor : TextColor;
        ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch, font, text, drawPosition, textColor * Opacity, Color.Black * Opacity, animationValue * 0.04f, origin, textScale * scaleFactor);

        if (hoveringOverText)
        {
            Main.LocalPlayer.mouseInterface = true;
            Main.blockMouse = true;

            if (Main.mouseLeft && Main.mouseLeftRelease)
            {
                draedon.As<QuestDraedon>().ChangeAIState(QuestDraedon.DraedonAIType.WaitForMarsToArrive);
                if (Main.netMode == NetmodeID.MultiplayerClient)
                {
                    PacketManager.SendPacket<SummonMarsPacket>();
                }
            }
        }

        if (WasHoveringOverText != hoveringOverText)
        {
            SoundEngine.PlaySound(SoundID.MenuTick);
            WasHoveringOverText = hoveringOverText;
        }

        return true;
    }
}
