using Luminance.Core.Graphics;
using Luminance.Core.Hooking;
using MonoMod.Cil;
using NoxusBoss.Core.GlobalInstances;
using NoxusBoss.Core.Graphics.GeneralScreenEffects;
using NoxusBoss.Core.Netcode;
using NoxusBoss.Core.Netcode.Packets;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.UI.ResourceSets;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;

public class TestOfResolveSystem : ModSystem
{
    /// <summary>
    /// Whether the test is ongoing or not.
    /// </summary>
    public static bool IsActive
    {
        get;
        internal set;
    }

    public static string RemainingHitsVariableName => "TestOfResolveRemainingHits";

    public override void OnModLoad()
    {
        PlayerDataManager.PreKillEvent += ModifyKillText;
        PlayerDataManager.OnHurtEvent += RemoveHeartsOnHit;
        PlayerDataManager.ModifyHurtEvent += DisableHitAbsorption;

        new ManagedILEdit("Make Player Hearts Removeable: Classic Display Set", Mod, edit =>
        {
            IL_ClassicPlayerResourcesDisplaySet.DrawLife += edit.SubscriptionWrapper;
        }, edit =>
        {
            IL_ClassicPlayerResourcesDisplaySet.DrawLife -= edit.SubscriptionWrapper;
        }, RemovePlayerHearts_Classic).Apply();

        new ManagedILEdit("Make Player Hearts Removeable: Fancy Heart Display Set", Mod, edit =>
        {
            IL_FancyClassicPlayerResourcesDisplaySet.PrepareFields += edit.SubscriptionWrapper;
        }, edit =>
        {
            IL_FancyClassicPlayerResourcesDisplaySet.PrepareFields -= edit.SubscriptionWrapper;
        }, RemovePlayerHearts_FancyHearts).Apply();

        new ManagedILEdit("Make Player Hearts Removeable: Bar Display Set", Mod, edit =>
        {
            IL_HorizontalBarsPlayerResourcesDisplaySet.PrepareFields += edit.SubscriptionWrapper;
        }, edit =>
        {
            IL_HorizontalBarsPlayerResourcesDisplaySet.PrepareFields -= edit.SubscriptionWrapper;
        }, RemovePlayerHearts_Bar).Apply();
    }

    private static void RemovePlayerHearts_Classic(ILContext context, ManagedILEdit edit)
    {
        ILCursor cursor = new ILCursor(context);
        if (!cursor.TryGotoNext(MoveType.After,
            i => i.MatchDiv(),
            i => i.MatchConvI4(),
            i => i.MatchLdcI4(1),
            i => i.MatchAdd()))
        {
            edit.LogFailure("Could not locate the quantity of hearts to draw.");
            return;
        }

        cursor.EmitDelegate((int originalHeartCount) =>
        {
            return Math.Min(originalHeartCount, Main.LocalPlayer.GetValueRef<int>(RemainingHitsVariableName).Value + 1);
        });
    }

    private static void RemovePlayerHearts_FancyHearts(ILContext context, ManagedILEdit edit)
    {
        ILCursor cursor = new ILCursor(context);
        if (!cursor.TryGotoNext(MoveType.Before,
            i => i.MatchStfld<FancyClassicPlayerResourcesDisplaySet>("_heartCountRow1")))
        {
            edit.LogFailure("Could not locate the _heartCountRow1 storage.");
            return;
        }

        cursor.EmitDelegate((int originalHeartCount) =>
        {
            int remainingHearts = Main.LocalPlayer.GetValueRef<int>(RemainingHitsVariableName).Value;
            return Math.Min(originalHeartCount, Utils.Clamp(remainingHearts, 0, 10));
        });

        if (!cursor.TryGotoNext(MoveType.Before,
            i => i.MatchStfld<FancyClassicPlayerResourcesDisplaySet>("_heartCountRow2")))
        {
            edit.LogFailure("Could not locate the _heartCountRow2 storage.");
            return;
        }

        cursor.EmitDelegate((int originalHeartCount) =>
        {
            int remainingHearts = Main.LocalPlayer.GetValueRef<int>(RemainingHitsVariableName).Value - 10;
            return Math.Min(originalHeartCount, Utils.Clamp(remainingHearts, 0, 10));
        });
    }

    private static void RemovePlayerHearts_Bar(ILContext context, ManagedILEdit edit)
    {
        ILCursor cursor = new ILCursor(context);
        if (!cursor.TryGotoNext(MoveType.Before,
            i => i.MatchStfld<HorizontalBarsPlayerResourcesDisplaySet>("_hpSegmentsCount")))
        {
            edit.LogFailure("Could not locate the _hpSegmentsCount storage.");
            return;
        }

        cursor.EmitDelegate((int originalHeartCount) =>
        {
            int remainingHearts = Main.LocalPlayer.GetValueRef<int>(RemainingHitsVariableName).Value;
            return Math.Min(remainingHearts, originalHeartCount);
        });
    }

    private static bool ModifyKillText(PlayerDataManager p, ref PlayerDeathReason damageSource)
    {
        if (IsActive && NamelessDeityBoss.Myself is not null)
        {
            int seconds = NamelessDeityBoss.Myself.As<NamelessDeityBoss>().FightTimer / 60;
            string deathText = Language.GetText("Mods.NoxusBoss.Death.TestOfResolveDeath").Format(p.Player.name, seconds / 60, seconds % 60);
            damageSource = PlayerDeathReason.ByCustomReason(deathText);

            return Main.LocalPlayer.GetValueRef<int>(RemainingHitsVariableName).Value <= 0;
        }

        return true;
    }

    private void RemoveHeartsOnHit(PlayerDataManager p, Player.HurtInfo hurtInfo)
    {
        if (IsActive)
        {
            Main.LocalPlayer.GetValueRef<int>(RemainingHitsVariableName).Value -= 2;

            if (Main.myPlayer == p.Player.whoAmI)
            {
                ScreenShakeSystem.StartShake(5f);
                GeneralScreenEffectSystem.ChromaticAberration.Start(Main.LocalPlayer.Center, 0.85f, 60);
            }
        }
    }

    private void DisableHitAbsorption(PlayerDataManager p, ref Player.HurtModifiers hurtInfo)
    {
        if (IsActive)
        {
            // This resets the ModifyHurtInfo event subscribers prematurely.
            hurtInfo.ToHurtInfo(0, 0, 0f, 0f, true);
        }
    }

    public override void PostUpdateNPCs()
    {
        if (!IsActive)
        {
            Main.LocalPlayer.GetValueRef<int>(RemainingHitsVariableName).Value = 20;
            return;
        }

        bool allPlayersAreDead = true;
        foreach (Player player in Main.ActivePlayers)
        {
            float healthRatio = Saturate(player.GetValueRef<int>(RemainingHitsVariableName) / 20f);
            player.statLife = (int)Round(player.statLifeMax2 * healthRatio);

            if (!player.dead)
            {
                allPlayersAreDead = false;
                break;
            }
        }

        if (allPlayersAreDead && NamelessDeityBoss.Myself is null && Main.netMode != NetmodeID.MultiplayerClient)
        {
            IsActive = false;
            PacketManager.SendPacket<TestOfResolvePacket>();
        }
    }
}
