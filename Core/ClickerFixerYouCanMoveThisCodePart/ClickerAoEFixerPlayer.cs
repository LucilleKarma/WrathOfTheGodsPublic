using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using Terraria.ModLoader;

namespace NoxusBoss.Core.ClickerFixerYouCanMoveThisCodePart
{
    public class ClickerAoEFixerPlayer : ModPlayer
    {
        public override void UpdateEquips()
        {
            if (Player.isNearNPC(ModContent.NPCType<AvatarOfEmptiness>()))
            {
                ClickerCompat.EnableClickEffect(Player, "ClickerClass:PhaseReach");
            }
        }
    }
}
