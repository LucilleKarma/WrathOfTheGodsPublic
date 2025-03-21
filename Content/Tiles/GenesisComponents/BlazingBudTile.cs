﻿using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Core.SoundSystems;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace NoxusBoss.Content.Tiles.GenesisComponents;

public class BlazingBudTile : ModTile, ICustomPlacementSound
{
    /// <summary>
    /// The tiled width of the seedling.
    /// </summary>
    public const int Width = 9;

    /// <summary>
    /// The tiled height of the seedling.
    /// </summary>
    public const int Height = 4;

    public SoundStyle PlaceSound => GennedAssets.Sounds.Genesis.BlazingBudPlant;

    public override string Texture => GetAssetPath("Content/Tiles/GenesisComponents", Name);

    public override void SetStaticDefaults()
    {
        Main.tileLighted[Type] = true;
        Main.tileFrameImportant[Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style2xX);
        TileObjectData.newTile.Width = Width;
        TileObjectData.newTile.Height = Height;
        TileObjectData.newTile.Origin = new Point16(Width / 2, Height - 1);
        TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
        TileObjectData.newTile.CoordinateHeights = Enumerable.Repeat(16, TileObjectData.newTile.Height).ToArray();
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newTile.LavaDeath = false;
        TileObjectData.newTile.DrawYOffset = 2;
        TileObjectData.addTile(Type);

        HitSound = null;
        AddMapEntry(new(149, 39, 54));
    }

    public override bool CanDrop(int i, int j) => false;

    public override bool CreateDust(int i, int j, ref int type) => false;

    public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
    {
        r = 0.6f;
        g = 0.09f;
        b = 0.23f;
    }

    public override void NearbyEffects(int i, int j, bool closer)
    {
        Tile t = Main.tile[i, j];
        if (t.TileFrameX == (int)(Width * 0.5f) * 18 && t.TileFrameY == (Height - 1) * 18)
            ModContent.GetInstance<BlazingBudTileRenderSystem>().AddPoint(new(i, j));
    }

    public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) => false;
}
