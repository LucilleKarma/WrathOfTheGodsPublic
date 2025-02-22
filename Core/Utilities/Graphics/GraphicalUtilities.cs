﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace NoxusBoss.Core.Utilities;

public static partial class Utilities
{
    /// <summary>
    /// Returns the size of the game's main <see cref="Viewport"/>.
    /// </summary>
    public static Vector2 ViewportSize => new Vector2(Main.instance.GraphicsDevice.Viewport.Width, Main.instance.GraphicsDevice.Viewport.Height);

    /// <summary>
    /// Returns the area that composes the game's main <see cref="Viewport"/> area.
    /// </summary>
    public static Rectangle ViewportArea => new Rectangle(0, 0, Main.instance.GraphicsDevice.Viewport.Width, Main.instance.GraphicsDevice.Viewport.Height);

    /// <summary>
    /// Draws a line significantly more efficiently than <see cref="Utils.DrawLine(SpriteBatch, Vector2, Vector2, Color, Color, float)"/> using just one scaled line texture. Positions are automatically converted to screen coordinates.
    /// </summary>
    /// <param name="spriteBatch">The sprite batch by which the line should be drawn.</param>
    /// <param name="start">The starting point of the line in world coordinates.</param>
    /// <param name="end">The ending point of the line in world coordinates.</param>
    /// <param name="color">The color of the line.</param>
    /// <param name="width">The width of the line.</param>
    public static void DrawLineBetter(this SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float width)
    {
        // Draw nothing if the start and end are equal, to prevent division by 0 problems.
        if (start == end)
            return;

        start -= Main.screenPosition;
        end -= Main.screenPosition;

        Texture2D line = WhitePixel;
        float rotation = (end - start).ToRotation();
        Vector2 scale = new Vector2(Vector2.Distance(start, end) / line.Width, width / line.Height);

        spriteBatch.Draw(line, start, null, color, rotation, line.Size() * Vector2.UnitY * 0.5f, scale, SpriteEffects.None, 0f);
    }
}
