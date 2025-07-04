using Luminance.Core.Hooking;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using NoxusBoss.Core.GlobalInstances;
using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.ModLoader;

namespace NoxusBoss.Content.Buffs;

public class AntimatterAnnihilation : ModBuff
{
    public const string DPSVariableName = "AntimatterAnnihilationDPS";

    public override string Texture => GetAssetPath("Content/Buffs", Name);

    public override void SetStaticDefaults()
    {
        Main.debuff[Type] = true;
        Main.buffNoSave[Type] = true;
        Main.buffNoTimeDisplay[Type] = true;
        PlayerDataManager.UpdateBadLifeRegenEvent += ApplyDPS;

        new ManagedILEdit("Use custom death text for Antimatter Annihilation", Mod, edit =>
        {
            IL_Player.KillMe += edit.SubscriptionWrapper;
        }, edit =>
        {
            IL_Player.KillMe -= edit.SubscriptionWrapper;
        }, UseCustomDeathMessage).Apply();
    }

    private static void UseCustomDeathMessage(ILContext context, ManagedILEdit edit)
    {
        ILCursor cursor = new ILCursor(context);
        if (!cursor.TryGotoNext(MoveType.After, i => i.MatchStfld<Player>("crystalLeaf")))
        {
            edit.LogFailure("Could not find the crystalLeaf storage");
            return;
        }

        if (!cursor.TryGotoNext(MoveType.After, i => i.MatchCallOrCallvirt<PlayerDeathReason>("GetDeathText")))
        {
            edit.LogFailure("Could not find the GetDeathText call");
            return;
        }

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitDelegate((NetworkText text, Player player) =>
        {
            if (player.HasBuff<AntimatterAnnihilation>())
            {
                NetworkText killText = NetworkText.FromKey($"Mods.NoxusBoss.Death.AntimatterAnnihilation{Main.rand.Next(5) + 1}", player.name);
                return PlayerDeathReason.ByCustomReason(killText).GetDeathText(player.name);
            }

            return text;
        });
    }

    private void ApplyDPS(PlayerDataManager p)
    {
        if (!p.Player.HasBuff<AntimatterAnnihilation>())
            return;

        int dps = p.GetValueRef<int>(DPSVariableName);
        if (p.Player.lifeRegen > 0)
            p.Player.lifeRegen = 0;
        p.Player.lifeRegenTime = 0;
        p.Player.lifeRegen -= dps * 2;
    }
}
