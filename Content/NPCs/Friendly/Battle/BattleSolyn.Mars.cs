using NoxusBoss.Content.NPCs.Bosses.Draedon;
using NoxusBoss.Content.NPCs.Bosses.Draedon.Projectiles.SolynProjectiles;
using NoxusBoss.Core.World.Subworlds;

using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Friendly;

public partial class BattleSolyn : ModNPC
{
    /// <summary>
    /// Mars states in which solyn should not try to switch players
    /// </summary>
    private static readonly HashSet<MarsBody.MarsAIType> MARS_DONT_SWAP_STATES = [
        MarsBody.MarsAIType.CarvedLaserbeam,
        MarsBody.MarsAIType.ElectricCageBlasts,
        MarsBody.MarsAIType.EnergyWeaveSequence
    ];

    /// <summary>
    /// Makes Solyn fight Mars.
    /// </summary>
    public void DoBehavior_FightMars()
    {
        bool marsIsAbsent = MarsBody.Myself is null;
        if (marsIsAbsent || EternalGardenUpdateSystem.WasInSubworldLastUpdateFrame)
        {
            // Immediately vanish if this isn't actually Solyn.
            if (FakeGhostForm)
            {
                NPC.active = false;
                return;
            }

            // If this is actually Solyn, turn into her non-battle form again.
            NPC.Transform(ModContent.NPCType<Solyn>());
            return;
        }

        var mars = MarsBody.Myself.As<MarsBody>();
        if (Mars_ShouldSwap(mars))
        {
            SwitchTo(mars.Target);
        }

        NPC.scale = 1f;
        NPC.target = Player.whoAmI;
        NPC.immortal = true;
        NPC.noGravity = true;
        NPC.noTileCollide = true;
        mars.SolynAction?.Invoke(this);
    }

    private bool Mars_ShouldSwap(MarsBody mars)
    {
        if (IsMultiplayerClone) return false;
        if (mars.NPC.target == MultiplayerIndex) return false;

        //Dont swap if player is casting beam
        var beamId = ModContent.ProjectileType<SolynTagTeamBeam>();
        if (mars.SolynPlayerTeamAttackTimer != 0 || Player.ownedProjectileCounts[beamId] > 0) return false;

        return !MARS_DONT_SWAP_STATES.Contains(mars.CurrentState);
    }
}
