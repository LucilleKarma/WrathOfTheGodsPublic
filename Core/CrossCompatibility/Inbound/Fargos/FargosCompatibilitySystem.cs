using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace NoxusBoss.Core.CrossCompatibility.Inbound.CalamityRemix;

public class FargosCompatibilitySystem : ModSystem
{
    /// <summary>
    /// The baseline Fargos mod.
    /// </summary>
    public static Mod? Fargowiltas
    {
        get;
        private set;
    }

    public override void OnModLoad()
    {
        if (ModLoader.TryGetMod("Fargowiltas", out Mod fargo))
            Fargowiltas = fargo;
    }

    /// <summary>
    /// Marks a given rectangle area as being indestructible by Fargo tools, such as instavators and platform generators.
    /// </summary>
    public static void MarkRectangleAsIndestructible(Rectangle indestructibleArea, bool tileCoords)
    {
        if (tileCoords)
        {
            indestructibleArea.X *= 16;
            indestructibleArea.Y *= 16;
            indestructibleArea.Width *= 16;
            indestructibleArea.Height *= 16;
        }

        string command = "AddIndestructibleRectangle";
        Fargowiltas?.Call(command, indestructibleArea);
    }
}
