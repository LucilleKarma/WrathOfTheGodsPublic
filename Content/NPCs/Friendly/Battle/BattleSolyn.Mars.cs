using NoxusBoss.Content.NPCs.Bosses.Draedon;
using NoxusBoss.Core.World.Subworlds;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Friendly;

public partial class BattleSolyn : ModNPC
{
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

        NPC.scale = 1f;
        NPC.target = Player.FindClosest(NPC.Center, 1, 1);
        NPC.immortal = true;
        NPC.noGravity = true;
        NPC.noTileCollide = true;
        MarsBody.Myself?.As<MarsBody>().SolynAction?.Invoke(this);
    }
}
