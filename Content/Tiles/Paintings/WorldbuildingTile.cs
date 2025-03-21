﻿using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace NoxusBoss.Content.Tiles.Paintings;

public class WorldbuildingTile : ModTile
{
    public override string Texture => GetAssetPath("Content/Tiles/Paintings", Name);

    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileLavaDeath[Type] = true;
        TileID.Sets.FramesOnKillWall[Type] = true;

        TileObjectData.newTile.Width = 4;
        TileObjectData.newTile.Height = 7;
        TileObjectData.newTile.Origin = new Point16(2, 6);
        TileObjectData.newTile.AnchorWall = true;
        TileObjectData.newTile.UsesCustomCanPlace = true;
        TileObjectData.newTile.CoordinateHeights = Enumerable.Repeat(16, TileObjectData.newTile.Height).ToArray();
        TileObjectData.newTile.CoordinateWidth = 16;
        TileObjectData.newTile.CoordinatePadding = 2;
        TileObjectData.newTile.LavaDeath = true;
        TileObjectData.addTile(Type);

        DustType = DustID.WoodFurniture;
    }
}
