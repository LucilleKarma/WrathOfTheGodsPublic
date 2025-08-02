using Luminance.Common.Easings;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.UI.Chat;

namespace NoxusBoss.Core.World.GameScenes.Stargazing;

public class StarViewObject
{
    /// <summary>
    /// The name of this object.
    /// </summary>
    public string NameKey
    {
        get;
        init;
    }

    /// <summary>
    /// The draw action responsible for rendering the object itself.
    /// </summary>
    public Action<Vector2> DrawAction
    {
        get;
        init;
    }

    /// <summary>
    /// The dialogue Solyn says when interacting with this object.
    /// </summary>
    public Func<string> DialogueKey
    {
        get;
        init;
    }

    /// <summary>
    /// How many times this object has been interacted with.
    /// </summary>
    public int TotalTimesInteracted
    {
        get;
        set;
    }

    /// <summary>
    /// The opacity of the interaction outline.
    /// </summary>
    public float OutlineOpacity
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this object has been selected.
    /// </summary>
    public bool Selected
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this object was previously hovered over.
    /// </summary>
    public bool WasPreviouslyHovering
    {
        get;
        set;
    }

    /// <summary>
    /// The click animation interpolant.
    /// </summary>
    public float ClickAnimationInterpolant
    {
        get;
        set;
    }

    /// <summary>
    /// The box that can be interacted with for this object.
    /// </summary>
    public Rectangle InteractionArea
    {
        get;
        set;
    }

    public StarViewObject(Rectangle interactionArea, Action<Vector2> drawAction, string keyBase) : this(interactionArea, drawAction, keyBase, () => $"Mods.NoxusBoss.StarGazing.{keyBase}.SolynDialogue") { }

    public StarViewObject(Rectangle interactionArea, Action<Vector2> drawAction, string keyBase, Func<string> dialogueKeyFunction)
    {
        InteractionArea = interactionArea;
        DrawAction = drawAction;

        NameKey = $"Mods.NoxusBoss.StarGazing.{keyBase}.Name";
        DialogueKey = dialogueKeyFunction;

        StargazingScene.starViewObjects.Add(this);
    }

    /// <summary>
    /// Updates the opacity of the outline.
    /// </summary>
    public void UpdateOutline(Vector2 viewOffset)
    {
        Vector2 mousePosition = Vector2.Transform(Main.MouseScreen, Main.GameViewMatrix.TransformationMatrix);
        Rectangle mouseArea = new Rectangle((int)(mousePosition.X - viewOffset.X), (int)(mousePosition.Y - viewOffset.Y), 2, 2);
        bool hoveringOverIntersection = InteractionArea.Intersects(mouseArea);
        bool outlineShouldAppear = Selected || hoveringOverIntersection;

        OutlineOpacity = Saturate(OutlineOpacity + outlineShouldAppear.ToDirectionInt() * 0.05f);
        if (Selected)
            OutlineOpacity = 1f;

        if (ClickAnimationInterpolant > 0f)
        {
            ClickAnimationInterpolant += 0.09f;
            if (ClickAnimationInterpolant >= 1f)
                ClickAnimationInterpolant = 0f;
        }

        if (hoveringOverIntersection != WasPreviouslyHovering)
        {
            if (hoveringOverIntersection)
                SoundEngine.PlaySound(SoundID.MenuTick);
            WasPreviouslyHovering = hoveringOverIntersection;
        }

        if (outlineShouldAppear && PlayerInput.Triggers.JustReleased.MouseLeft)
        {
            bool wasSelected = Selected;
            foreach (StarViewObject viewObject in StargazingScene.starViewObjects)
                viewObject.Selected = false;

            Selected = !wasSelected;

            if (Selected)
            {
                ClickAnimationInterpolant = 0.01f;
                StargazingScene.SelectedObject = this;
                StargazingScene.SolynDialogueToSpellOut = Language.GetTextValue(DialogueKey());
                SoundEngine.PlaySound(SoundID.MenuOpen);
            }
            else
                StargazingScene.SelectedObject = null;
        }
    }

    /// <summary>
    /// Draws this object's outline.
    /// </summary>
    public void DrawSelected(Vector2 viewOffset)
    {
        Color inUseColor = Color.Yellow;
        Color rectangleColor = Color.Lerp(Color.DarkGray * 0.15f, inUseColor, OutlineOpacity);
        Vector2 topLeft = InteractionArea.TopLeft();
        Vector2 topRight = InteractionArea.TopRight();
        Vector2 bottomLeft = InteractionArea.BottomLeft();
        Vector2 bottomRight = InteractionArea.BottomRight();
        Vector2 center = InteractionArea.Center();

        float squeezeInwardInterpolant = EasingCurves.Sine.Evaluate(EasingType.InOut, -0.18f, 0.1f, InverseLerp(0.3f, 0.7f, OutlineOpacity)) + Convert01To010(ClickAnimationInterpolant) * 0.075f;
        Vector2 universalOffset = viewOffset + Main.screenPosition;
        topLeft = Vector2.Lerp(topLeft, center, squeezeInwardInterpolant) + universalOffset;
        topRight = Vector2.Lerp(topRight, center, squeezeInwardInterpolant) + universalOffset;
        bottomLeft = Vector2.Lerp(bottomLeft, center, squeezeInwardInterpolant) + universalOffset;
        bottomRight = Vector2.Lerp(bottomRight, center, squeezeInwardInterpolant) + universalOffset;

        // Top.
        Utils.DrawLine(Main.spriteBatch, topLeft, Vector2.Lerp(topLeft, topRight, 0.2f), rectangleColor, Color.Transparent, 4f);
        Utils.DrawLine(Main.spriteBatch, topRight, Vector2.Lerp(topRight, topLeft, 0.2f), rectangleColor, Color.Transparent, 4f);

        // Bottom.
        Utils.DrawLine(Main.spriteBatch, bottomLeft, Vector2.Lerp(bottomLeft, bottomRight, 0.2f), rectangleColor, Color.Transparent, 4f);
        Utils.DrawLine(Main.spriteBatch, bottomRight, Vector2.Lerp(bottomRight, bottomLeft, 0.2f), rectangleColor, Color.Transparent, 4f);

        // Left.
        Utils.DrawLine(Main.spriteBatch, bottomLeft, Vector2.Lerp(bottomLeft, topLeft, 0.2f), rectangleColor, Color.Transparent, 4f);
        Utils.DrawLine(Main.spriteBatch, topLeft, Vector2.Lerp(topLeft, bottomLeft, 0.2f), rectangleColor, Color.Transparent, 4f);

        // Left.
        Utils.DrawLine(Main.spriteBatch, bottomRight, Vector2.Lerp(bottomRight, topRight, 0.2f), rectangleColor, Color.Transparent, 4f);
        Utils.DrawLine(Main.spriteBatch, topRight, Vector2.Lerp(topRight, bottomRight, 0.2f), rectangleColor, Color.Transparent, 4f);

        ReLogic.Graphics.DynamicSpriteFont font = FontAssets.MouseText.Value;
        string text = Language.GetTextValue(NameKey);
        Vector2 textOrigin = font.MeasureString(text) * new Vector2(0f, 0.5f);
        Vector2 textDrawPosition = topRight - universalOffset + new Vector2(20f, 32f) + viewOffset;
        Color textColor = Color.Yellow * OutlineOpacity;
        ChatManager.DrawColorCodedString(Main.spriteBatch, font, text, textDrawPosition, textColor, 0f, textOrigin, Vector2.One * 1.25f);
    }
}
