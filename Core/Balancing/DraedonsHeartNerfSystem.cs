using System.Reflection;
using CalamityMod.CalPlayer;
using CalamityMod.Items.Accessories;
using Luminance.Core.Hooking;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.GlobalInstances;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Balancing;

[JITWhenModsEnabled(CalamityCompatibility.ModName)]
[ExtendsFromMod(CalamityCompatibility.ModName)]
public sealed class DraedonsHeartNerfSystem : ModSystem
{
    /// <summary>
    /// The amount of health players are healed per frame when activating the Draedon's heart.
    /// </summary>
    public static int DraedonsHeartHealPerFrame => 2;

    /// <summary>
    /// The IL edit responsible for the modifying per-frame Draedon's heart health yields.
    /// </summary>
    public static ILHook DraedonsHeartHealHook
    {
        get;
        private set;
    }

    public override void OnModLoad()
    {
        if (!ModLoader.TryGetMod(CalamityCompatibility.ModName, out Mod cal))
            return;

        // As of writing this (8/26/2024) Ozzatron has informed me that Calamity will nerf the Draedon's Heart on its own in the upcoming update.
        // To ensure that it doesn't get double-nerfed by this system, disable it if past the current public Calamity version.
        if (cal.Version > new Version("2.0.4.003"))
            return;

        MethodInfo? updateRippersMethod = typeof(CalamityPlayer).GetMethod("UpdateRippers", UniversalBindingFlags);
        if (updateRippersMethod is null)
        {
            Mod.Logger.Warn("Could not find the UpdateRippers method in CalamityPlayer.");
            return;
        }

        new ManagedILEdit("Tweak Per-frame Draedon's Heart Health Yields", Mod, edit =>
        {
            DraedonsHeartHealHook = new(updateRippersMethod, edit.SubscriptionWrapper);
        }, _ =>
        {
            DraedonsHeartHealHook?.Undo();
        }, UpdateDreadonsHeartHealthYields).Apply();

        GlobalItemEventHandlers.ModifyTooltipsEvent += ModifyDraedonsHeartTooltip;
    }

    private void ModifyDraedonsHeartTooltip(Item item, List<TooltipLine> tooltips)
    {
        if (item.type != ModContent.ItemType<DraedonsHeart>())
            return;

        string oldYield = "360";
        string newYield = (DraedonsHeartHealPerFrame * 120).ToString();
        TooltipLine? lineWithNumbers = tooltips.FirstOrDefault(t => t.Text.Contains(oldYield));
        if (lineWithNumbers is not null)
            lineWithNumbers.Text = lineWithNumbers.Text.Replace(oldYield, newYield);
    }

    private static void UpdateDreadonsHeartHealthYields(ILContext context, ManagedILEdit edit)
    {
        ILCursor cursor = new ILCursor(context);

        if (!cursor.TryGotoNext(MoveType.Before, i => i.MatchStfld<CalamityPlayer>("draedonsHeart")))
        {
            edit.LogFailure("The CalamityPlayer.adrenaline storage could not be found.");
            return;
        }
        if (!cursor.TryGotoNext(MoveType.After, i => i.MatchStfld<Player>("statLife")))
        {
            edit.LogFailure("The statLife storage could not be found.");
            return;
        }
        if (!cursor.TryGotoPrev(MoveType.Before, i => i.MatchAdd()))
        {
            edit.LogFailure("The Add opcode could not be found.");
            return;
        }

        cursor.Emit(OpCodes.Pop);
        cursor.EmitDelegate(() => DraedonsHeartHealPerFrame);
    }
}
