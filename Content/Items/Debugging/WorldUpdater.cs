﻿using NoxusBoss.Core.World.WorldSaving;
using Terraria;
using Terraria.ID;

namespace NoxusBoss.Content.Items.Debugging;

public class WorldUpdater : DebugItem
{
    public override void SetDefaults()
    {
        Item.width = 36;
        Item.height = 36;
        Item.useAnimation = 40;
        Item.useTime = 40;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.useStyle = ItemUseStyleID.HoldUp;
        Item.UseSound = null;
        Item.rare = ItemRarityID.Blue;
        Item.value = 0;
    }

    public override bool? UseItem(Player player)
    {
        if (Main.myPlayer == NetmodeID.MultiplayerClient || player.itemAnimation != player.itemAnimationMax - 1)
            return false;

        WorldVersionSystem.WorldVersionText = Mod.Version.ToString();
        return null;
    }
}
