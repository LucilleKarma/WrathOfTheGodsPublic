using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.UI;
using Terraria.UI.Chat;

namespace NoxusBoss.Core.Graphics.UI;

public class UIFancyText : UIElement
{
    internal struct TextPart
    {
        /// <summary>
        /// Whether a given snippet of text was created out of a regex-induced change or not.
        /// </summary>
        private readonly bool changedByRegex;

        /// <summary>
        /// The color of the text.
        /// </summary>
        public Color TextColor;

        /// <summary>
        /// The contents of the text.
        /// </summary>
        public string Text;

        /// <summary>
        /// Whether the text should be drawn with italics or not.
        /// </summary>
        public bool Italics;

        /// <summary>
        /// The original index that a line was created from. This is used because loop index is unreliable in cases where regex-induced splits cause the text to go from one instance to three.
        /// </summary>
        public readonly int LineIndex;

        /// <summary>
        /// The scale of the text.
        /// </summary>
        public readonly float TextScale;

        /// <summary>
        /// The amount of horizontal space filled by the text.
        /// </summary>
        public readonly float HorizontalSpaceUsed;

        /// <summary>
        /// The font that the text should be drawn with.
        /// </summary>
        public readonly DynamicSpriteFont Font;

        /// <summary>
        /// A regex that searches for and identifies an italics pattern in the following format:<br></br>
        /// **Text**
        /// </summary>
        public static readonly Regex ItalicsEmphasis = new Regex(@".*(\*\*[\p{L}0-9\s\p{P}]+\*\*).*", RegexOptions.Compiled);

        /// <summary>
        /// A regex that searches for and identifies a color tag pattern in the following format:<br></br>
        /// [c/COLORHEX: Text]
        /// </summary>
        public static readonly Regex ColorHexSpecifier = new Regex(@"\[c\/([0-9a-fа-яA-FА-Я]{6})\:(.*)\]", RegexOptions.Compiled);

        private TextPart(string text, int lineIndex, bool italics, float textScale, DynamicSpriteFont font, Color color, float alreadyUsedHorixontalSpace = 0f)
        {
            changedByRegex = false;
            Text = text;
            LineIndex = lineIndex;
            Italics = italics;
            Font = font;
            TextScale = textScale;
            TextColor = color;
            HorizontalSpaceUsed = alreadyUsedHorixontalSpace + Font.MeasureString(Text).X * TextScale;
        }

        public TextPart(string text, int lineIndex, bool italics, float textScale, DynamicSpriteFont font, Color color, float alreadyUsedHorixontalSpace = 0f, bool changedByRegex = false) :
            this(text, lineIndex, italics, textScale, font, color, alreadyUsedHorixontalSpace)
        {
            this.changedByRegex = changedByRegex;
        }

        /// <summary>
        /// Duplicates this text snippet with the marker of being made from regex.
        /// </summary>
        public readonly TextPart CreateFromRegex()
        {
            return new(Text, LineIndex, Italics, TextScale, Font, TextColor, HorizontalSpaceUsed, true);
        }

        public static void SplitByRegex(List<TextPart> lines, Regex regex, float textScale, DynamicSpriteFont font, bool includeLeftAndRightSides, Func<Match, TextPart, TextPart> matchAction)
        {
            // Search for instances of a given pattern as an indicator for for change.
            for (int i = 0; i < lines.Count; i++)
            {
                // Verify that there are any instances of the above pattern. If there are, split the line into three parts:
                // 1. The left side.
                // 2. The center, with the italics in use.
                // 3. The right side.
                // This also involves removing the original line.
                if (regex.IsMatch(lines[i].Text) && !lines[i].changedByRegex)
                {
                    // Remove the old, soon to be split line.
                    int lineIndex = lines[i].LineIndex;
                    string wholeLine = lines[i].Text;
                    Color originalColor = lines[i].TextColor;
                    lines.RemoveAt(i);

                    // Acquire the matched instance. If there are more than one successive loop instances will catch it.
                    Match match = regex.Match(wholeLine);
                    string textThatUsesPattern = match.Groups[1].Value;

                    // Add the separated text to the list of lines.
                    TextPart left = new TextPart(wholeLine.Split(textThatUsesPattern).First(), lineIndex, false, textScale, font, originalColor, 0f).CreateFromRegex();
                    TextPart center = new TextPart(textThatUsesPattern, lineIndex, false, textScale, font, originalColor, 0f).CreateFromRegex();
                    TextPart right = new TextPart(wholeLine.Split(textThatUsesPattern)[1], lineIndex, false, textScale, font, originalColor, 0f).CreateFromRegex();

                    if (includeLeftAndRightSides)
                        lines.Insert(i, right);
                    lines.Insert(i, matchAction(match, center));
                    if (includeLeftAndRightSides)
                        lines.Insert(i, left);

                    // Go back to the start of the loop due to the fact that the line count is going to inevitably be altered.
                    i = 0;
                }
            }

            // Reset the changed by regex attribute of the text.
            for (int i = 0; i < lines.Count; i++)
                lines[i] = new(lines[i].Text, lines[i].LineIndex, lines[i].Italics, lines[i].TextScale, lines[i].Font, lines[i].TextColor, lines[i].HorizontalSpaceUsed, false);
        }

        public static TextPart[] SplitRawText(string text, float textScale, DynamicSpriteFont font, Color textColor)
        {
            // Firstly separate the base text by newlines.
            List<TextPart> lines = text.Split('\n').Select((t, index) => new TextPart(t, index, false, textScale, font, textColor, 0f)).ToList();

            // Search for instances of a [c/Hex:Text] pattern as an indicator for color overrides.
            SplitByRegex(lines, ColorHexSpecifier, textScale, font, false, (match, line) =>
            {
                Color lineColor = line.TextColor;

                // Define the text color and replace the text such that only the inside of the formatting is displayed.
                int colorHex = Convert.ToInt32(match.Groups[1].Value, 16);
                return line with
                {
                    TextColor = new(colorHex >> 16 & 255, colorHex >> 8 & 255, colorHex & 255),
                    Text = match.Groups[2].Value
                };
            });

            // Search for instances of a **Text** pattern as an indicator for italics.
            SplitByRegex(lines, ItalicsEmphasis, textScale, font, true, (match, line) =>
            {
                line.Italics = true;
                line.Text = line.Text.Replace("**", string.Empty);
                return line;
            });

            return lines.ToArray();
        }
    }

    /// <summary>
    /// The general scale of the text.
    /// </summary>
    private float textScale = 1f;

    /// <summary>
    /// The internal visible text. Copied from vanilla code.
    /// </summary>
    private string visibleText;

    /// <summary>
    /// The last text reference. Copied from vanilla code.
    /// </summary>
    private string lastTextReference;

    /// <summary>
    /// The font text should be drawn with by default.
    /// </summary>
    internal readonly DynamicSpriteFont font;

    /// <summary>
    /// The font text should be drawn with, assuming it has italics.
    /// </summary>
    internal readonly DynamicSpriteFont fontItalics;

    /// <summary>
    /// The unscaled size in pixels of the text.
    /// </summary>
    private Vector2 textSize = Vector2.Zero;

    /// <summary>
    /// The color of the text.
    /// </summary>
    public Color Color = Color.White;

    /// <summary>
    /// The color of the shadow backglow for the text.
    /// </summary>
    public Color ShadowColor = new Color(44, 44, 44);

    /// <summary>
    /// The color of the text when it's being hovered over by the mouse. By default is null and unused.
    /// </summary>
    public Color? TextHoverColor;

    /// <summary>
    /// An event that fires when lines of text are clicked on.
    /// </summary>
    public event Action<string> OnClickTextLine;

    /// <summary>
    /// The draw origin of the text. At 0, everything is from the top, at 1 everything is drawn from the bottom.
    /// </summary>
    public float TextOriginY
    {
        get;
        set;
    }

    /// <summary>
    /// The amount of spacing between vertically separated lines. 
    /// </summary>
    public float SpacingPerLine
    {
        get;
        set;
    }

    /// <summary>
    /// The overall text to display. This should be pre-wrapped.
    /// </summary>
    public string Text
    {
        get;
        private set;
    } = string.Empty;

    public UIFancyText(string text, DynamicSpriteFont font, Color textColor, float textScale = 1f)
    {
        this.font = font;
        TextOriginY = 0f;
        Color = textColor;
        SpacingPerLine = 42f;
        InternalSetText(text, textScale);
    }

    public UIFancyText(string text, DynamicSpriteFont font, DynamicSpriteFont fontItalics, Color textColor, float textScale = 1f) : this(text, font, textColor, textScale)
    {
        this.fontItalics = fontItalics;
    }

    public override void Recalculate()
    {
        InternalSetText(Text, textScale);
        base.Recalculate();
    }

    public void SetText(string text)
    {
        InternalSetText(text, textScale);
    }

    public void SetText(string text, float textScale)
    {
        InternalSetText(text, textScale);
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        base.DrawSelf(spriteBatch);
        VerifyTextState();
        float scale = textScale;

        CalculatedStyle innerDimensions = GetInnerDimensions();
        Vector2 position = innerDimensions.Position() - Vector2.UnitY * scale * 2f;

        // Do a bunch of offset and scaling math.
        position.Y -= TextOriginY * textSize.Y;
        Vector2 origin = Vector2.Zero;
        Vector2 baseScale = new Vector2(scale);

        // Split the text into parts and draw them individually.
        // This is necessary because certain things such as emphasis or color variance have to be drawn separately from the rest of the line.
        TextPart[] splitText = TextPart.SplitRawText(visibleText, scale, font, Color);
        int totalLines = splitText.Max(t => t.LineIndex);
        for (int i = 0; i < totalLines + 1; i++)
        {
            List<TextPart> linesForText = splitText.Where(t => t.LineIndex == i).ToList();

            // Draw the line parts.
            int partIndex = 0;
            float horizontalOffset = 0f;
            foreach (TextPart line in linesForText)
            {
                Vector2 currentPosition = position + new Vector2(horizontalOffset, i * scale * SpacingPerLine);
                DynamicSpriteFont font = line.Italics ? fontItalics ?? this.font : this.font;

                Color lineColor = line.TextColor;

                // Check if the text line should be recolored due to being hovered over.
                Vector2 textSize = font.MeasureString(line.Text) * baseScale;
                Rectangle textArea = new Rectangle((int)currentPosition.X, (int)currentPosition.Y, (int)textSize.X, (int)textSize.Y);
                textArea.Y += 6;
                textArea.Height -= 12;

                if (new Rectangle((int)Main.MouseScreen.X, (int)Main.MouseScreen.Y, 2, 2).Intersects(textArea))
                {
                    lineColor = TextHoverColor ?? lineColor;

                    // Handle text clicking.
                    Main.LocalPlayer.mouseInterface = true;
                    if (Main.mouseLeft && Main.mouseLeftRelease)
                        OnClickTextLine?.Invoke(line.Text);
                }

                ChatManager.DrawColorCodedStringShadow(spriteBatch, font, line.Text, currentPosition, ShadowColor * (Color.A / 255f), 0f, origin, baseScale, -1f, 0.2f);
                ChatManager.DrawColorCodedString(spriteBatch, font, line.Text, currentPosition, lineColor, 0f, origin, baseScale, -1f);
                partIndex++;
                horizontalOffset += font.MeasureString(line.Text).X * line.TextScale;
            }
        }
    }

    private void VerifyTextState()
    {
        if (lastTextReference == Text)
            return;

        InternalSetText(Text, textScale);
    }

    private void InternalSetText(string text, float textScale)
    {
        Text = text;
        this.textScale = textScale;
        lastTextReference = text.ToString();

        visibleText = lastTextReference;

        Vector2 textSize = font.MeasureString(visibleText);
        Vector2 clampTextSize = new Vector2(textSize.X, textSize.Y + SpacingPerLine) * textScale;

        this.textSize = clampTextSize;
        MinWidth.Set(clampTextSize.X + PaddingLeft + PaddingRight, 0f);
        MinHeight.Set(clampTextSize.Y + PaddingTop + PaddingBottom, 0f);
    }
}
