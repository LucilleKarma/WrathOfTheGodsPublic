using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Core.CrossCompatibility.Inbound;
using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers;

public class AvatarDeathMessagesPlayer : ModPlayer
{
    public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource)
    {
        if (AvatarOfEmptiness.Myself is not null)
        {
            int terribleMessageChance = 1000;
            if (ModReferences.CalamityHuntMod is not null && ModReferences.CalamityHuntMod.TryFind("BadApple", out ModItem badApple) && Player.HasItem(badApple.Type))
                terribleMessageChance = 1;

            if (Main.rand.NextBool(terribleMessageChance))
            {
                NetworkText killText = NetworkText.FromKey("Mods.NoxusBoss.PlayerDeathMessages.ThisModReallyIsJustShitpostsMaskedByBossesHuh", Player.name);
                damageSource = PlayerDeathReason.ByCustomReason(killText);
            }
        }

        return true;
    }
}
