using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Assets.Fonts;
using NoxusBoss.Core.DialogueSystem;
using NoxusBoss.Core.World.Subworlds;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace NoxusBoss.Core.Graphics.UI.SolynDialogue;

public class SolynDialogUI : UIState
{
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
    /// The opacity of text said by Solyn.
    /// </summary>
    public float DialogueTextOpacity
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
    /// The opacity of the backshade.
    /// </summary>
    public float BackshadeOpacity
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
    public Dialogue CurrentDialogueNode
    {
        get;
        set;
    }

    /// <summary>
    /// The UI handler for dialogue said by Solyn.
    /// </summary>
    public UIFancyText DialogueTextUI
    {
        get;
        private set;
    }

    /// <summary>
    /// The UI handler for dialogue said by the player.
    /// </summary>
    public UIFancyText PlayerTextUI
    {
        get;
        private set;
    }

    /// <summary>
    /// The UI image that handles the text divider.
    /// </summary>
    public UICustomImage DividerUI
    {
        get;
        private set;
    }

    /// <summary>
    /// The UI image that handles the backshade.
    /// </summary>
    public UICustomImage BackshadeUI
    {
        get;
        private set;
    }

    /// <summary>
    /// The UI image that houses the continue button.
    /// </summary>
    public UICustomImage ContinueButtonUI
    {
        get;
        private set;
    }

    public static float DividerScale => 1f;

    public static float DialogScale => 0.65f;

    /// <summary>
    /// How long Solyn should pause before speaking again when noticing a <see cref="PauseCharacter"/> in dialogue.
    /// </summary>
    public static int PauseDuration => SecondsToFrames(0.75f);

    /// <summary>
    /// The character used in localization to indicate that Solyn should pause.
    /// </summary>
    public const char PauseCharacter = '^';

    public override void OnInitialize()
    {
        ResetUIElements();

        // Initialize text lines as empty.
        DialogueText = string.Empty;
        PlayerResponseTextLines = null;

        // Zero out opacity values. They will fade in as needed.
        ContinueButtonOpacity = 0f;
        DialogueTextOpacity = 0f;
        PlayerTextOpacity = 0f;
    }

    public void ResetUIElements()
    {
        if (Main.netMode == NetmodeID.Server)
            return;

        Elements?.Clear();

        // Acquire assets.
        Asset<Texture2D> dividerTexture = ModContent.Request<Texture2D>(GetAssetPath("UI/SolynDialogue", "Divider"), AssetRequestMode.ImmediateLoad);
        Asset<Texture2D> backshade = ModContent.Request<Texture2D>(GetAssetPath("UI/SolynDialogue", "Backshade"), AssetRequestMode.ImmediateLoad);
        Vector2 screenArea = ViewportSize;
        Vector2 dividerPosition = screenArea * new Vector2(0.55f, 0.7f) + Vector2.UnitY * 60f - dividerTexture.Value.Size();

        // Create the backshade.
        BackshadeUI = new UICustomImage(backshade, Color.Transparent);
        BackshadeUI.Width.Set(DividerScale * 1270f, 0f);
        BackshadeUI.Height.Set(DividerScale * 588f, 0f);
        BackshadeUI.Top.Set(dividerPosition.Y - DividerScale * 294f, 0f);
        BackshadeUI.Left.Set(dividerPosition.X - DividerScale * 130f, 0f);
        Append(BackshadeUI);

        // Create the divider element.
        DividerUI = new(dividerTexture, Color.White);
        DividerUI.Width.Set(DividerScale * 1000f, 0f);
        DividerUI.Height.Set(DividerScale * 14f, 0f);
        DividerUI.Top.Set(dividerPosition.Y, 0f);
        DividerUI.Left.Set(dividerPosition.X, 0f);
        Append(DividerUI);

        // Create the continue button.
        Asset<Texture2D> buttonTexture = ModContent.Request<Texture2D>(GetAssetPath("UI/SolynDialogue", "NextButton"), AssetRequestMode.ImmediateLoad);
        ContinueButtonUI = new(buttonTexture, Color.White);
        ContinueButtonUI.Top.Set(24f, 0f);
        ContinueButtonUI.Left.Set(0f, 0f);
        ContinueButtonUI.Width.Set(44f, 0f);
        ContinueButtonUI.Height.Set(29f, 0f);
        ContinueButtonUI.OnLeftClick += ContinueToNextLine;
        ContinueButtonUI.Color = Color.Transparent;
        DividerUI.Append(ContinueButtonUI);

        // Create the text elements.
        DialogueTextUI = new(string.Empty, FontRegistry.Instance.SolynText, FontRegistry.Instance.SolynTextItalics, Color.White, DialogScale)
        {
            TextOriginY = 1f
        };
        DialogueTextUI.Top.Set(12f, 0f);
        DialogueTextUI.Left.Set(-20f, 0f);
        DialogueTextUI.OnClickTextLine += SpeedUpDialog;
        DividerUI.Append(DialogueTextUI);

        PlayerTextUI = new(string.Empty, FontAssets.DeathText.Value, FontAssets.DeathText.Value, Color.White, DialogScale * 0.8f)
        {
            TextOriginY = 0f,
            SpacingPerLine = 90f
        };
        PlayerTextUI.Top.Set(20f, 0f);
        PlayerTextUI.Left.Set(30f, 0f);
        PlayerTextUI.OnClickTextLine += SelectPlayerResponse;
        DividerUI.Append(PlayerTextUI);
    }

    private string WrapText(string text, UIFancyText textHandler)
    {
        string[] wrappedLines = Utils.WordwrapString(text.Replace("\n", string.Empty), textHandler.font, (int)((DividerUI.Width.Pixels - 100f) / DialogScale / DividerScale * 1.19f), 50, out _);
        return string.Join('\n', wrappedLines).TrimEnd('\n');
    }

    private void ContinueToNextLine()
    {
        var childrenNodes = CurrentDialogueNode.Children;
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
                    CurrentDialogueNode = childrenNodes.First();
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

    private void SpeedUpDialog(string text)
    {
        if (ResponseToSay is not null)
            DialogueText = ResponseToSay;
    }

    private void SelectPlayerResponse(string text)
    {
        // If the dialogue has no children on the dialogue tree, terminate immediately, since there's no dialogue to transfer to.
        // This should never happen in practice, but it's a useful sanity check.
        var childrenNodes = CurrentDialogueNode.Children;
        if (childrenNodes is null || childrenNodes.Count == 0)
            return;

        for (int i = 0; i < childrenNodes.Count; i++)
        {
            if (childrenNodes[i].Text == text)
            {
                childrenNodes[i].InvokeClickAction();
                childrenNodes[i].InvokeEndAction();
                List<Dialogue> availableChildren = childrenNodes[i].Children.Where(n => n.SelectionCondition()).ToList();

                if (availableChildren.Count == 1)
                {
                    CurrentDialogueNode = availableChildren.First();
                    ResponseToSay = CurrentDialogueNode.Text;
                    ResetDialogueData();
                }
                break;
            }
        }
    }

    public void ResetDialogueData()
    {
        DialogueText = string.Empty;
        DialogueTextOpacity = 0f;
        PlayerTextOpacity = 0f;
        ContinueButtonOpacity = 0f;
        NextCharacterDelay = 0;
    }

    public override void Update(GameTime gameTime)
    {
        // Initialize dialogue if necessary.
        ResponseToSay ??= CurrentDialogueNode.Text;

        // Pick text.
        DecideOnTextToDisplay();

        // Update the dialogue text UI.
        DialogueTextUI.SetText(WrapText(DialogueText.Replace(PauseCharacter.ToString(), string.Empty), DialogueTextUI));

        // Update the player text UI.
        string responseText = string.Empty;
        if (PlayerResponseTextLines is not null)
        {
            for (int i = 0; i < PlayerResponseTextLines.Length; i++)
                responseText += PlayerResponseTextLines[i] + "\n";
        }
        PlayerTextUI.SetText(responseText);

        // Update colors.
        UpdateUIColors();
    }

    public void DecideOnTextToDisplay()
    {
        // Check if the dialog node has children that are spoken by the player.
        // These only appear once the dialogue has been said completely.
        var childrenNodes = CurrentDialogueNode.Children;
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

    public void UpdateUIColors()
    {
        // Make the dialogue text opacity fade in.
        DialogueTextOpacity = Saturate(DialogueTextOpacity + 0.05f);
        BackshadeOpacity = Clamp(BackshadeOpacity + 0.05f, 0f, 0.8f);

        // Make the continue button fade in if there's no player options.
        bool continueButtonExists = PlayerResponseTextLines is null && PlayerTextOpacity <= 0f && (CurrentDialogueNode.Children?.Where(c => !c.SpokenByPlayer).Any() ?? false);
        if (PlayerResponseTextLines is null && PlayerTextOpacity <= 0f && CurrentDialogueNode.Children is not null && CurrentDialogueNode.Children.Count == 0)
            continueButtonExists = true;

        ContinueButtonOpacity = Saturate(ContinueButtonOpacity + continueButtonExists.ToDirectionInt() * 0.08f);

        // Update colors based on opacity.
        float continueButtonOpacity = ContinueButtonOpacity;
        DialogueTextUI.Color = (CurrentDialogueNode?.ColorOverrideFunction?.Invoke() ?? DialogColorRegistry.SolynTextColor) * DialogueTextOpacity;
        PlayerTextUI.Color = Color.White * PlayerTextOpacity;
        PlayerTextUI.TextHoverColor = Color.Yellow * PlayerTextOpacity;
        ContinueButtonUI.HoverColor = new Color(255, 255, 0, 0) * continueButtonOpacity;
        ContinueButtonUI.Color = Color.White * continueButtonOpacity;
        BackshadeUI.Color = Color.White * BackshadeOpacity;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);
    }
}
