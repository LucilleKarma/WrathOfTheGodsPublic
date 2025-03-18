using NoxusBoss.Content.NPCs.Bosses.Avatar.FirstPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Core.World.Subworlds;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Friendly;

public partial class BattleSolyn : ModNPC
{
    /// <summary>
    /// Makes Solyn fight the Avatar of Emptiness.
    /// </summary>
    public void DoBehavior_FightAvatar()
    {
        bool avatarIsAbsent = AvatarRift.Myself is null && AvatarOfEmptiness.Myself is null;
        if (avatarIsAbsent || EternalGardenUpdateSystem.WasInSubworldLastUpdateFrame)
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

        if (AvatarOfEmptiness.Myself is not null)
            AvatarOfEmptiness.Myself.As<AvatarOfEmptiness>().SolynAction?.Invoke(this);
        else
            AvatarRift.Myself?.As<AvatarRift>().SolynAction?.Invoke(this);
    }
}
