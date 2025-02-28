﻿using Luminance.Core.Hooking;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Fixes;

/* CONTEXT:
 * Nameless and the Avatar's projectiles are prone to ending up outside of the world due to the nature of their fights. This is most problematic with laserbeams.
 * To address this, natural projection deletion outside of the world is disabled if Nameless or the Avatar's second phase form are present.
 */
public class BossProjectileDeletionFixSystem : ModSystem
{
    public static bool HostileProjectilesCanBeDeleted => NamelessDeityBoss.Myself is null && AvatarOfEmptiness.Myself is null;

    public override void OnModLoad()
    {
        new ManagedILEdit("Prevent Nameless Deity and the Avatar's Projectiles from Out-of-World Despawning", Mod, edit =>
        {
            IL_Projectile.Update += edit.SubscriptionWrapper;
        }, edit =>
        {
            IL_Projectile.Update -= edit.SubscriptionWrapper;
        }, PreventNamelessDeityProjectilesFromDespawning).Apply();
    }

    private static void PreventNamelessDeityProjectilesFromDespawning(ILContext context, ManagedILEdit edit)
    {
        ILCursor cursor = new ILCursor(context);

        // Locate the instance where Main.leftWorld is loaded. It (along with a few other world variables) will be used to determine if the projectile has left the world
        // and must be deleted.
        if (!cursor.TryGotoNext(i => i.MatchLdsfld<Main>("leftWorld")))
        {
            edit.LogFailure("The Main.leftWorld field load could not be found.");
            return;
        }

        // Find the instances of Projectile.active being set.
        if (!cursor.TryGotoNext(MoveType.Before, i => i.MatchStfld<Entity>("active")))
        {
            edit.LogFailure("The projectile.active storage could not be found.");
            return;
        }

        // Replace the value of the Projectile.active set so that it remains unchanged when Nameless or the Avatar are present.
        cursor.Emit(OpCodes.Pop);
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitDelegate<Func<Projectile, bool>>(p => !HostileProjectilesCanBeDeleted && p.hostile && p.active);

        // Branch after the return if hostile projectiles should not be deleted.
        ILLabel afterReturn = cursor.DefineLabel();
        if (!cursor.TryGotoNext(MoveType.After, i => i.MatchRet()))
        {
            edit.LogFailure("The final method return could not be found.");
            return;
        }
        cursor.MarkLabel(afterReturn);

        // Go back and reassess whether the deletion should occur.
        if (!cursor.TryGotoPrev(MoveType.After, i => i.MatchStfld<Entity>("active")))
        {
            edit.LogFailure("The projectile.active storage before the final method return could not be found.");
            return;
        }
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitDelegate<Func<Projectile, bool>>(p => !HostileProjectilesCanBeDeleted && p.hostile);
        cursor.Emit(OpCodes.Brtrue, afterReturn);
    }
}
