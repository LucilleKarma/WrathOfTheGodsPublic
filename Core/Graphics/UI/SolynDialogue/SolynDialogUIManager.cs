using Luminance.Common.Easings;

using Microsoft.CodeAnalysis;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using NoxusBoss.Assets;
using NoxusBoss.Assets.Fonts;
using NoxusBoss.Core.DialogueSystem;
using NoxusBoss.Core.Netcode;
using NoxusBoss.Core.Netcode.Packets;
using NoxusBoss.Core.World.Subworlds;

using ReLogic.Graphics;

using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.UI.Chat;

using static NoxusBoss.Core.Graphics.UI.UIFancyText;

namespace NoxusBoss.Core.Graphics.UI.SolynDialogue;

public class SolynDialogUIManager
{
    private static float scale => Main.UIScale * Main.screenWidth / 1920f;

    /// <summary>
    /// The full string of text Solyn should say.
    /// </summary>
    public string? ResponseToSay
    {
        get;
        set;
    }

    /// <summary>
    /// How much more time should be waited on before the next character is displayed.
    /// </summary>
    public int NextCharacterDelay
    {
        get;
        set;
    }

    /// <summary>
    /// The opacity of text said by the player.
    /// </summary>
    public float PlayerTextOpacity
    {
        get;
        set;
    }

    /// <summary>
    /// The opacity of the continue button.
    /// </summary>
    public float ContinueButtonOpacity
    {
        get;
        set;
    }

    /// <summary>
    /// A general-purpose timer for how long the continue button has been hovered over.
    /// </summary>
    public float ContinueButtonHoverTimer
    {
        get;
        set;
    }

    /// <summary>
    /// The text spoken by Solyn.
    /// </summary>
    public string DialogueText
    {
        get;
        set;
    }

    /// <summary>
    /// The collection of all player response dialogue options.
    /// </summary>
    public string[]? PlayerResponseTextLines
    {
        get;
        set;
    }

    /// <summary>
    /// The current node of the dialogue tree that's being displayed.
    /// </summary>
    public Dialogue? CurrentDialogueNode
    {
        get;
        private set;
    }

    public static float DialogScale => 0.4f;

    /// <summary>
    /// How long Solyn should pause before speaking again when noticing a <see cref="PauseCharacter"/> in dialogue.
    /// </summary>
    public static int PauseDuration => SecondsToFrames(0.75f);

    // TODO -- These hardcoded characters are quite clunky. Consider using something more robust in the text.
    /// <summary>
    /// The character used in localization to indicate that Solyn should pause.
    /// </summary>
    public const char PauseCharacter = '^';

    /// <summary>
    /// The character used in localization to indicate that Solyn's dialogue should shake.
    /// </summary>
    public const char ShakeIndicatorCharacter = '&';

    public void ResetDialogueData()
    {
        DialogueText = string.Empty;
        PlayerTextOpacity = 0f;
        NextCharacterDelay = 0;
    }

    /// <summary>
    /// Updates this UI.
    /// </summary>
    public void Update()
    {
        // Initialize dialogue if necessary.
        if (CurrentDialogueNode is not null)
            ResponseToSay ??= CurrentDialogueNode.Text;

        DecideOnTextToDisplay();

        // Make the continue button fade in if there's no player options.
        bool continueButtonExists = PlayerResponseTextLines is null && PlayerTextOpacity <= 0f && (CurrentDialogueNode?.Children?.Where(c => !c.SpokenByPlayer)?.Any() ?? false);
        if (PlayerResponseTextLines is null && PlayerTextOpacity <= 0f && CurrentDialogueNode?.Children is not null && CurrentDialogueNode.Children.Count == 0)
            continueButtonExists = true;

        ContinueButtonOpacity = Saturate(ContinueButtonOpacity + continueButtonExists.ToDirectionInt() * 0.04f);
    }

    private void DecideOnTextToDisplay()
    {
        // Check if the dialog node has children that are spoken by the player.
        // These only appear once the dialogue has been said completely.
        List<Dialogue>? childrenNodes = CurrentDialogueNode?.Children;
        List<string> playerResponses = [];
        if (childrenNodes is not null && childrenNodes.Count != 0 && DialogueText == ResponseToSay)
        {
            playerResponses.AddRange(childrenNodes.Where(n => n.SpokenByPlayer && n.SelectionCondition()).Select(n =>
            {
                string text = n.Text;
                if (n.ColorOverrideFunction is not null)
                    text = $"[c/{n.ColorOverrideFunction().Hex3()}:{text}]";

                return text;
            }));
        }

        // If there are player responses, make the player dialogue fade in.
        bool playerResponsesExist = playerResponses.Count != 0;
        PlayerTextOpacity = Saturate(PlayerTextOpacity + playerResponsesExist.ToDirectionInt() * 0.06f);

        // Decide on player text.
        if (PlayerTextOpacity >= 0.1f)
            PlayerResponseTextLines = [.. playerResponses];
        if (PlayerTextOpacity <= 0f)
            PlayerResponseTextLines = null;

        // Decide on dialogue text once the delay has passed.
        if (NextCharacterDelay > 0)
            NextCharacterDelay--;

        else
        {
            bool italicsSpelt = false;
            bool italicsEnded = false;
            for (int i = 0; i < 1; i++)
            {
                // Just get out of the loop if the text has already completed. There's nothing more to do.
                if (ResponseToSay is not null && DialogueText.Length >= ResponseToSay.Length)
                {
                    if (DialogueText.Length == 0)
                        CurrentDialogueNode?.InvokeEndAction();
                    break;
                }

                char previousCharacter = DialogueText.Length >= 1 ? DialogueText[^1] : ' ';
                char nextCharacter = ResponseToSay?[DialogueText.Length] ?? '?';
                DialogueText += nextCharacter;

                // Apply a delay if the spacing character was applied.
                if (nextCharacter == PauseCharacter)
                    NextCharacterDelay = PauseDuration;

                // Play the speak sound.
                else if (!italicsSpelt)
                {
                    SoundStyle speakSound = GennedAssets.Sounds.Solyn.Speak with { Volume = 0.4f, MaxInstances = 1, SoundLimitBehavior = SoundLimitBehavior.IgnoreNew };
                    SoundStyle ghostSpeakSound = GennedAssets.Sounds.Solyn.GhostSpeak with { Volume = 0.4f, MaxInstances = 1, SoundLimitBehavior = SoundLimitBehavior.IgnoreNew };
                    SoundEngine.PlaySound(EternalGardenUpdateSystem.WasInSubworldLastUpdateFrame ? ghostSpeakSound : speakSound);
                }

                // Apply end actions if the dialog was completed.
                if (DialogueText == ResponseToSay)
                    CurrentDialogueNode?.InvokeEndAction();

                // If a ** has been spelt out continue looping until the closing ** is also spelt out, to prevent the italics formatting from revealing itself.
                if (nextCharacter == '*' && previousCharacter == '*')
                {
                    if (!italicsSpelt)
                        italicsSpelt = true;
                    else
                    {
                        italicsSpelt = false;
                        italicsEnded = false;
                    }
                }

                // Continue iterating if necessary.
                if (italicsSpelt && !italicsEnded)
                    i--;
            }
        }
    }

    /// <summary>
    /// Renders this UI.
    /// </summary>
    public void Render()
    {
        Vector2 screenArea = ViewportSize;

        Texture2D divider = GennedAssets.Textures.SolynDialogue.Divider;
        Texture2D backshade = GennedAssets.Textures.SolynDialogue.Backshade;
        Vector2 dividerCenter = screenArea * new Vector2(0.5f, 0.75f);
        Main.spriteBatch.Draw(backshade, dividerCenter, null, Color.White, 0f, backshade.Size() * 0.5f, scale * new Vector2(1.2f, 0.45f), 0, 0f);
        Main.spriteBatch.Draw(divider, dividerCenter, null, Color.White, 0f, divider.Size() * 0.5f, scale, 0, 0f);

        RenderSolynText(dividerCenter + new Vector2(-divider.Width * 0.52f, DialogScale * -74f) * scale);
        RenderPlayerResponses(dividerCenter + new Vector2(-divider.Width * 0.46f, DialogScale * 30f) * scale);
        RenderContinueButton(dividerCenter + new Vector2(-divider.Width * 0.485f, DialogScale * 50f) * scale);
    }

    public void SetDialogue(Dialogue? dialogue)
    {
        CurrentDialogueNode = dialogue;
        if (dialogue is not null)
        {
            PacketManager.SendPacket<PlayDialoguePacket>(dialogue.TextKey);
        }
    }

    private static string WrapText(string text, DynamicSpriteFont font)
    {
        Texture2D divider = GennedAssets.Textures.SolynDialogue.Divider;
        string[] wrappedLines = Utils.WordwrapString(text.Replace("\n", string.Empty), font, (int)(divider.Width / DialogScale * 1.04f), 50, out _);
        return string.Join('\n', wrappedLines).TrimEnd('\n');
    }

    private void RenderSolynText(Vector2 position)
    {
        // Split the text into parts and draw them individually.
        // This is necessary because certain things such as emphasis or color variance have to be drawn separately from the rest of the line.
        DynamicSpriteFont font = FontRegistry.Instance.SolynText;
        DynamicSpriteFont fontItalics = FontRegistry.Instance.SolynTextItalics;
        float shakeFactor = DialogueText.Contains(ShakeIndicatorCharacter) ? 3f : 0f;
        string cleanedDialogueText = WrapText(DialogueText.Replace(PauseCharacter.ToString(), string.Empty).Replace(ShakeIndicatorCharacter.ToString(), string.Empty), font);

        float textScale = scale * DialogScale;
        TextPart[] splitText = TextPart.SplitRawText(cleanedDialogueText, textScale, font, DialogColorRegistry.SolynTextColor);
        int totalLines = splitText.Max(t => t.LineIndex);
        float verticalPadding = 2f;

        for (int i = 0; i < totalLines; i++)
            position.Y -= textScale * (font.MeasureString(splitText[i].Text).Y + verticalPadding);

        for (int i = 0; i < totalLines + 1; i++)
        {
            List<TextPart> linesForText = splitText.Where(t => t.LineIndex == i).ToList();

            // Draw the line parts.
            int partIndex = 0;
            float horizontalOffset = 0f;
            foreach (TextPart line in linesForText)
            {
                Vector2 currentPosition = position + Vector2.UnitX * horizontalOffset;
                DynamicSpriteFont lineFont = line.Italics ? fontItalics ?? font : font;

                Color lineColor = line.TextColor;

                // Check if mouse click interactions should be disabled due to hovering over the text.
                Vector2 textSize = lineFont.MeasureString(line.Text) * textScale;
                Rectangle textArea = new Rectangle((int)currentPosition.X, (int)currentPosition.Y, (int)textSize.X, (int)textSize.Y);
                textArea.Y += 6;
                textArea.Height -= 12;

                if (new Rectangle((int)Main.MouseScreen.X, (int)Main.MouseScreen.Y, 2, 2).Intersects(textArea))
                {
                    Main.LocalPlayer.mouseInterface = true;
                    if (Main.mouseLeft && Main.mouseLeftRelease)
                        SpeedUpDialog();
                }

                RenderTextLineByLine(lineFont, line.Text, currentPosition, lineColor, textScale, shakeFactor);

                partIndex++;
                horizontalOffset += lineFont.MeasureString(line.Text).X * line.TextScale;
            }

            position.Y += textScale * (font.MeasureString(splitText[i].Text).Y + verticalPadding);
        }
    }

    private static void RenderTextLineByLine(DynamicSpriteFont font, string text, Vector2 drawPosition, Color color, float scale, float shakeFactor)
    {
        for (int i = 0; i < text.Length; i++)
        {
            Vector2 displacement = Main.rand.NextVector2Circular(shakeFactor, shakeFactor) * scale;
            Vector2 displacedDrawPosition = drawPosition + displacement;

            Utils.DrawBorderStringFourWay(Main.spriteBatch, font, text[i].ToString(), displacedDrawPosition.X, displacedDrawPosition.Y, color, Color.Black, Vector2.Zero, scale);
            drawPosition.X += ChatManager.GetStringSize(font, text[i].ToString(), Vector2.One).X * scale;
        }
    }

    private void RenderPlayerResponses(Vector2 position)
    {
        if (PlayerResponseTextLines is null || PlayerResponseTextLines.Length <= 0)
            return;

        float textScale = scale * DialogScale * 0.78f;
        DynamicSpriteFont font = FontAssets.DeathText.Value;
        for (int i = 0; i < PlayerResponseTextLines.Length; i++)
        {
            string line = PlayerResponseTextLines[i];

            // Check if the text line should be recolored due to being hovered over.
            Color textColor = Color.White;
            Vector2 textSize = font.MeasureString(line) * textScale;
            Rectangle textArea = new Rectangle((int)position.X, (int)position.Y, (int)textSize.X, (int)textSize.Y);
            textArea.Inflate(4, 2);

            List<TextPart> splitLine = [new TextPart(line, 0, false, textScale, font, textColor)];
            TextPart.SplitByRegex(splitLine, TextPart.ColorHexSpecifier, textScale, font, false, (match, line) =>
            {
                int colorHex = Convert.ToInt32(match.Groups[1].Value, 16);
                return line with
                {
                    TextColor = new Color(colorHex >> 16 & 255, colorHex >> 8 & 255, colorHex & 255),
                    Text = match.Groups[2].Value
                };
            });

            textColor = splitLine[0].TextColor;
            line = splitLine[0].Text;

            bool hoveringOverText = new Rectangle((int)Main.MouseScreen.X, (int)Main.MouseScreen.Y, 2, 2).Intersects(textArea);
            if (hoveringOverText)
            {
                Main.LocalPlayer.mouseInterface = true;

                if (Main.mouseLeft && Main.mouseLeftRelease)
                    SelectPlayerResponse(line);
            }

            if (hoveringOverText)
                textColor = Color.Yellow;

            ChatManager.DrawColorCodedString(Main.spriteBatch, font, line, position, textColor * PlayerTextOpacity, 0f, Vector2.Zero, Vector2.One * textScale, -1f);

            position.Y += textArea.Height + textScale * 6f;
        }
    }

    private void RenderContinueButton(Vector2 position)
    {
        float scaleFactor = SmoothStep(0.45f, 0.54f, ContinueButtonHoverTimer);
        float buttonScale = scale * EasingCurves.Elastic.Evaluate(EasingType.Out, ContinueButtonOpacity) * scaleFactor;
        Color buttonColor = Color.White * ContinueButtonOpacity;
        Texture2D buttonTexture = GennedAssets.Textures.SolynDialogue.NextButton;
        Rectangle buttonArea = Utils.CenteredRectangle(position, Vector2.One * buttonTexture.Size() * buttonScale);
        bool mouseOverButton = new Rectangle((int)Main.MouseScreen.X, (int)Main.MouseScreen.Y, 2, 2).Intersects(buttonArea);

        if (mouseOverButton)
        {
            buttonColor = new Color(255, 255, 0, 0) * ContinueButtonOpacity;
            Main.LocalPlayer.mouseInterface = true;
            if (Main.mouseLeft && Main.mouseLeftRelease && ContinueButtonHoverTimer > 0f)
            {
                ContinueToNextLine();
                ContinueButtonHoverTimer = 0f;
            }
        }

        ContinueButtonHoverTimer = Saturate(ContinueButtonHoverTimer + (mouseOverButton ? 0.15f : -1f));

        Main.spriteBatch.Draw(buttonTexture, position, null, buttonColor, 0f, buttonTexture.Size() * 0.5f, buttonScale, 0, 0f);
    }

    private void ContinueToNextLine()
    {
        if (CurrentDialogueNode is null)
            return;

        List<Dialogue>? childrenNodes = CurrentDialogueNode.Children;
        if (childrenNodes is null)
            return;

        bool anyChildren = childrenNodes.Count >= 1;
        if (ContinueButtonOpacity >= 0.75f)
        {
            Dialogue oldNode = CurrentDialogueNode;

            bool textChanged = true;
            if (anyChildren)
            {
                oldNode.InvokeClickAction();
                if (DialogueText == ResponseToSay || ResponseToSay is null)
                {
                    SetDialogue(childrenNodes.First());
                    ResponseToSay = CurrentDialogueNode.Text;
                }
                else
                {
                    DialogueText = ResponseToSay;
                    textChanged = false;
                }
            }
            else
            {
                oldNode.InvokeClickAction();
                if (!DialogueSaveSystem.seenDialogue.Contains(oldNode.TextKey))
                    DialogueSaveSystem.seenDialogue.Add(oldNode.TextKey);

                SolynDialogSystem.HideUI();
                Main.LocalPlayer.SetTalkNPC(-1);
            }
            oldNode.InvokeEndAction();

            if (textChanged)
                ResetDialogueData();
        }
    }

    private void SpeedUpDialog()
    {
        if (ResponseToSay is not null)
            DialogueText = ResponseToSay;
    }

    private void SelectPlayerResponse(string text)
    {
        // If the dialogue has no children on the dialogue tree, terminate immediately, since there's no dialogue to transfer to.
        // This should never happen in practice, but it's a useful sanity check.
        List<Dialogue>? childrenNodes = CurrentDialogueNode?.Children;
        if (childrenNodes is null || childrenNodes.Count == 0)
            return;

        for (int i = 0; i < childrenNodes.Count; i++)
        {
            if (childrenNodes[i].Text == text)
            {
                PacketManager.SendPacket<PlayDialoguePacket>(childrenNodes[i].TextKey);

                childrenNodes[i].InvokeClickAction();
                childrenNodes[i].InvokeEndAction();
                List<Dialogue> availableChildren = childrenNodes[i].Children.Where(n => n.SelectionCondition()).ToList();

                if (availableChildren.Count == 1)
                {
                    SetDialogue(availableChildren.First());
                    ResponseToSay = CurrentDialogueNode!.Text;
                    ResetDialogueData();
                }
                break;
            }
        }
    }
}
