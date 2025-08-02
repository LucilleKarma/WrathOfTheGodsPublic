using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using LuminanceUtilities = Luminance.Common.Utilities.Utilities;

namespace NoxusBoss.Core.Fixes;

public class LuminanceFindGroundVerticalFix : ModSystem
{
    public override void OnModLoad()
    {
        MethodInfo? orig = typeof(LuminanceUtilities).GetMethod(nameof(FindGroundVertical), BindingFlags.Public | BindingFlags.Static);
        MonoModHooks.Add(orig, FindGroundVertical);
    }

    public static Point FindGroundVertical(Func<Point, Point> orig, Point p)
    {
        // TODO -- fix this in Luminance itself. The bug is caused by the following:
        // 1) Not normalizing point.
        // 2) Having p.Y check AFTER World.SolidTile(...), not before.
        p = new Point((int)Clamp(p.X, 0f, Main.maxTilesX), (int)Clamp(p.Y, 0f, Main.maxTilesY));

        if (WorldGen.SolidTile(p))
        {
            while (p.Y >= 1 && WorldGen.SolidTile(p.X, p.Y - 1))
            {
                p.Y--;
            }
        }
        else
        {
            while (p.Y < Main.maxTilesY && !WorldGen.SolidTile(p.X, p.Y + 1))
            {
                p.Y++;
            }
        }

        return p;
    }
}
