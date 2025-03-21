﻿using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Core.GlobalInstances;

[LegacyName("NoxusGlobalWall")]
public class GlobalWallEventHandlers : GlobalWall
{
    public delegate bool WallConditionDelegate(int x, int y, int type);

    public static event WallConditionDelegate? IsWallUnbreakableEvent;

    public override void Unload()
    {
        // Reset all events on mod unload.
        IsWallUnbreakableEvent = null;
    }

    public static bool IsWallUnbreakable(int x, int y)
    {
        // Use default behavior if the event has no subscribers.
        if (IsWallUnbreakableEvent is null)
            return false;

        int wallID = Framing.GetTileSafely(x, y).WallType;
        bool result = false;
        foreach (Delegate d in IsWallUnbreakableEvent.GetInvocationList())
            result |= ((WallConditionDelegate)d).Invoke(x, y, wallID);

        return result;
    }

    public override void KillWall(int i, int j, int type, ref bool fail)
    {
        if (IsWallUnbreakable(i, j))
            fail = true;
    }

    public override bool CanExplode(int i, int j, int type)
    {
        if (IsWallUnbreakable(i, j))
            return false;

        return true;
    }
}
