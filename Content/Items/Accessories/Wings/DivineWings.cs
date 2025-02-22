﻿using Luminance.Core.Hooking;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using NoxusBoss.Content.Rarities;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace NoxusBoss.Content.Items.Accessories.Wings;

[AutoloadEquip(EquipType.Wings)]
public class DivineWings : ModItem
{
    public static int WingSlotID
    {
        get;
        private set;
    }

    public override string Texture => "NoxusBoss/Assets/Textures/Content/Items/Accessories/Wings/DivineWings";

    public override void SetStaticDefaults()
    {
        Item.ResearchUnlockCount = 1;

        WingSlotID = Item.wingSlot;

        // ArmorIDs.Wing.Sets.Stats appears to have an incomplete implementation for hover wings.
        // In my decompiled TML-altered Terraria source, every single specialized hover behavior is wrapped around in an 'if (player.wingsLogic == magicNumber)' check, as is the entire
        // if statement that enables hover movement in the first place. Modded wings are not included in player.

        // Evidently, this set allows for the specification of hover stats, but does not enable the hover behaviors.
        // As a result, I have to insert this IL edit to manually implement such behaviors.
        ArmorIDs.Wing.Sets.Stats[WingSlotID] = new WingStats(100000000, 16.67f, 3.7f, true, 23.5f, 4f);
        new ManagedILEdit("Let Divine Wings Hover", Mod, edit =>
        {
            IL_Player.Update += edit.SubscriptionWrapper;
        }, edit =>
        {
            IL_Player.Update -= edit.SubscriptionWrapper;
        }, LetWingsHover).Apply();

        On_Player.WingMovement += UseHoverMovement;
    }

    private static void LetWingsHover(ILContext context, ManagedILEdit edit)
    {
        ILCursor cursor = new ILCursor(context);

        /* This is the general layout of the code, with local variables cleaned up and extraneous comments added:
         *
         * bool usingWings = false;
         * if (((player.velocity.Y == 0f || player.sliding) && player.releaseJump) || (player.autoJump && player.justJumped))
         * {
         *     player.mount.ResetFlightTime(player.velocity.X);
         *     player.wingTime = (float)player.wingTimeMax;
         * }
         * 
         * // Performs the standard wings check.
         * if (player.wingsLogic > 0 && player.controlJump && player.wingTime > 0f && player.jump == 0 && player.velocity.Y != 0f)
         * {
         *     usingWings = true;
         * }
         * 
         * // Determine whether the player the player is using wings for a special hover.
         * // Notably, this does not include modded wing IDs.
         * if ((player.wingsLogic == 22 || player.wingsLogic == 28 || player.wingsLogic == 30 || player.wingsLogic == 32 || player.wingsLogic == 29 || player.wingsLogic == 33 || player.wingsLogic == 35 || player.wingsLogic == 37 || player.wingsLogic == 45) && player.controlJump && player.TryingToHoverDown && player.wingTime > 0f)
         * {
         *     usingWings = true;
         * }
         */

        // Search for the start of the if ((player.wingsLogic == 22 || player.wingsLogic == 28... || player.wingsLogic == 37 statement
        if (!cursor.TryGotoNext(MoveType.After, i => i.MatchLdcI4(37)))
        {
            edit.LogFailure("The 'if ((player.wingsLogic == 37' check could not be found.");
            return;
        }

        // Find the local index of the usingWings bool by going backwards to the first usingWings = true line.
        int usingWingsIndex = 0;
        if (!cursor.TryGotoPrev(MoveType.After, i => i.MatchStloc(out usingWingsIndex)))
        {
            edit.LogFailure("The usingWings local variable's index could not be found.");
            return;
        }

        // Go back to the start of the method and find the place where the usingWings bool is initialized with the usingWings = false line.
        cursor.Goto(0);
        if (!cursor.TryGotoNext(MoveType.Before, i => i.MatchStloc(usingWingsIndex)))
        {
            edit.LogFailure("The first initialization of the usingWings local variable could not be found.");
            return;
        }

        // Transform the usingWings = true line like so:
        // bool usingWings = true;
        // bool usingWings = true | (player.wingsLogic == WingSlotID && player.controlJump && player.TryingToHoverDown && player.wingTime > 0f);
        // Notice that this includes the same condition used for the "is the player using wings to hover right now?" check.

        // It would be more efficient to remove the true, but for defensive programming purposes this merely adds onto existing local variable definitions, rather than
        // completely replacing them.
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitDelegate((Player player) => player.wingsLogic == WingSlotID && player.controlJump && player.TryingToHoverDown && player.wingTime > 0f);
        cursor.Emit(OpCodes.Or);
    }

    private void UseHoverMovement(On_Player.orig_WingMovement orig, Player player)
    {
        orig(player);
        if (player.wingsLogic == WingSlotID && player.TryingToHoverDown)
            player.velocity.Y = -0.0001f;
    }

    public override void SetDefaults()
    {
        Item.width = 26;
        Item.height = 30;
        Item.rare = ModContent.RarityType<NamelessDeityRarity>();
        Item.accessory = true;
    }

    public override void UpdateAccessory(Player player, bool hideVisual)
    {
        player.noFallDmg = true;
        CalamityCompatibility.GrantInfiniteCalFlight(player);

        if (!hideVisual)
            Lighting.AddLight(player.Center, Vector3.One);
    }

    public override void ModifyTooltips(List<TooltipLine> tooltips)
    {
        int lastTooltipIndex = tooltips.FindLastIndex(t => t.Name.Contains("Tooltip"));

        tooltips.Add(new TooltipLine(Mod, "PressDownNotif", Language.GetTextValue("CommonItemTooltip.PressDownToHover")));
    }

    public override void VerticalWingSpeeds(Player player, ref float ascentWhenFalling, ref float ascentWhenRising, ref float maxCanAscendMultiplier, ref float maxAscentMultiplier, ref float constantAscend)
    {
        ascentWhenFalling = 2f;
        ascentWhenRising = 0.184f;
        maxCanAscendMultiplier = 1.2f;
        maxAscentMultiplier = 3.25f;
        constantAscend = 0.29f;
    }
}
