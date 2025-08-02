using Microsoft.Xna.Framework;

using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Core.CrossCompatibility.Inbound.ClickerClass;

/// <summary>
///     Whether the given NPC should enable Clicker Class "Phase Reach" which
///     allows the player to click enemies through tiles (no longer requires
///     line of sight).
///     <br />
///     This will let the player click any enemy through tiles and not just the
///     NPC this interface applies to, so should be used sparingly.
/// </summary>
internal interface IClickerClassPhaseReach;

internal sealed class ClickerClassCompatPlayer : ModPlayer
{
    public override bool IsLoadingEnabled(Mod mod)
    {
        return ModLoader.HasMod("ClickerClass");
    }

    public override void UpdateEquips()
    {
        base.UpdateEquips();

        foreach (NPC? npc in Main.ActiveNPCs)
        {
            if (npc.ModNPC is not IClickerClassPhaseReach || !(Vector2.Distance(npc.Center, Player.Center) < NPC.sWidth))
                continue;
            
            // We can include the proper wrapper later, but it's rather bloated.
            ModReferences.ClickerClass?.Call("EnableClickEffect", "1.4", Player, "ClickerClass:PhaseReach");
        }
    }
}
