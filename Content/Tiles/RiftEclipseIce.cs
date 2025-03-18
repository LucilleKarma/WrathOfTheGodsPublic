using Microsoft.Xna.Framework;
using NoxusBoss.Core.GlobalInstances;
using NoxusBoss.Core.World.GameScenes.RiftEclipse;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.Tiles;

public class RiftEclipseIce : ModTile
{
    /// <summary>
    /// How much a <see cref="Tile"/>'s <see cref="Tile.LiquidAmount"/> must be, at minimum, in order for it to be able to freeze into ice.
    /// </summary>
    public static int FreezeWaterQuantityRequirement => 85;

    public override string Texture => GetAssetPath("Content/Tiles", Name);

    public override void SetStaticDefaults()
    {
        Main.tileBlockLight[Type] = false;
        Main.tileSolid[Type] = true;
        Main.tileLighted[Type] = true;
        TileID.Sets.Ices[Type] = true;
        TileID.Sets.DrawsWalls[Type] = true;
        TileID.Sets.IceSkateSlippery[Type] = true;
        TileID.Sets.BlocksWaterDrawingBehindSelf[Type] = true;
        DustType = DustID.Ice;
        HitSound = SoundID.Item27 with { Volume = 0.4f, MaxInstances = 10 };
        AddMapEntry(new Color(164, 164, 204));

        // Merge with dirt.
        Main.tileMerge[Type][TileID.Dirt] = true;
        Main.tileMerge[TileID.Dirt][Type] = true;

        // Merge with stone.
        Main.tileMerge[Type][TileID.Stone] = true;
        Main.tileMerge[TileID.Stone][Type] = true;
        Main.tileMerge[Type][TileID.Ebonstone] = true;
        Main.tileMerge[TileID.Ebonstone][Type] = true;
        Main.tileMerge[Type][TileID.Crimstone] = true;
        Main.tileMerge[TileID.Crimstone][Type] = true;
        Main.tileMerge[Type][TileID.Pearlstone] = true;
        Main.tileMerge[TileID.Pearlstone][Type] = true;

        // Merge with sand.
        Main.tileMerge[Type][TileID.Sand] = true;
        Main.tileMerge[TileID.Sand][Type] = true;
        Main.tileMerge[Type][TileID.Ebonsand] = true;
        Main.tileMerge[TileID.Ebonsand][Type] = true;
        Main.tileMerge[Type][TileID.Crimsand] = true;
        Main.tileMerge[TileID.Crimsand][Type] = true;
        Main.tileMerge[Type][TileID.Pearlsand] = true;
        Main.tileMerge[TileID.Pearlsand][Type] = true;

        // Merge with snow.
        Main.tileMerge[Type][TileID.SnowBlock] = true;
        Main.tileMerge[TileID.SnowBlock][Type] = true;

        GlobalTileEventHandlers.RandomUpdateEvent += CreateIce;
        On_WorldGen.UpdateWorld_OvergroundTile += CreateIceWrapper;
    }

    private void CreateIceWrapper(On_WorldGen.orig_UpdateWorld_OvergroundTile orig, int i, int j, bool checkNPCSpawns, int wallDist)
    {
        CreateIce(i, j, Main.tile[i, j].TileType);
        orig(i, j, checkNPCSpawns, wallDist);
    }

    public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
    {
        r += 0.08f;
        b += 0.16f;
    }

    public void CreateIce(int x, int y, int type)
    {
        if (WorldGen.generatingWorld)
            return;

        // Melt ice if necessary instead of placing it.
        Tile above = Framing.GetTileSafely(x, y - 1);
        bool shouldMelt = type == Type && !RiftEclipseManagementSystem.RiftEclipseOngoing;
        if (shouldMelt)
        {
            Main.tile[x, y].Clear(TileDataType.Tile | TileDataType.TilePaint);
            Main.tile[x, y].Get<LiquidData>().Amount = 255;
            if (Main.netMode != NetmodeID.MultiplayerClient)
                NetMessage.SendTileSquare(-1, x, y);
            return;
        }

        // Only have a 50% chance for water to freeze.
        if (!Main.rand.NextBool())
            return;

        Tile t = Framing.GetTileSafely(x, y);
        Tile below = Framing.GetTileSafely(x, y + 1);
        if (t.LiquidAmount <= 0 || (t.LiquidAmount < FreezeWaterQuantityRequirement && below.LiquidAmount <= 0) || t.LiquidType != LiquidID.Water)
            return;

        // Only freeze tiles that are on the surface.
        int surfaceLine = (int)(AperiodicSin(x * 0.21f) * 6f) + 15;
        if (y >= Main.worldSurface - surfaceLine)
            return;

        // FOR THE LOVE OF GOD DO NOT BREAK CHESTS!!
        if ((TileID.Sets.IsAContainer[t.TileType] || TileID.Sets.BasicChest[t.TileType]) && t.HasTile)
            return;

        // Clear water tiles before attempting to place ice.
        if (t.HasTile && (type == TileID.LilyPad || type == TileID.Cattail || type == TileID.Bamboo || type == ModContent.TileType<RiftEclipseSnow>()))
            WorldGen.KillTile(x, y);

        Tile left = Framing.GetTileSafely(x - 1, y);
        Tile right = Framing.GetTileSafely(x + 1, y);
        bool atSurface = above.LiquidAmount <= 0 && !above.HasTile;
        bool iceAbove = above.HasUnactuatedTile && above.TileType == Type;
        bool iceToSides = (left.HasUnactuatedTile && left.TileType == Type) || (right.HasUnactuatedTile && right.TileType == Type);
        if ((atSurface && RiftEclipseSnowSystem.IceCanCoverWater) || ((iceAbove || iceToSides) && RiftEclipseSnowSystem.IceCanFreezeAllWater))
        {
            Main.tile[x, y].TileType = Type;
            Main.tile[x, y].Get<TileWallWireStateData>().HasTile = true;
            Main.tile[x - 1, y].Get<TileWallWireStateData>().Slope = SlopeType.Solid;
            Main.tile[x + 1, y].Get<TileWallWireStateData>().Slope = SlopeType.Solid;
            Main.tile[x, y + 1].Get<TileWallWireStateData>().Slope = SlopeType.Solid;
            if (Main.tile[x, y + 1].TileType == TileID.Grass)
                Main.tile[x, y + 1].TileType = TileID.Dirt;
            if (Main.tile[x - 1, y].TileType == TileID.Grass)
                Main.tile[x - 1, y].TileType = TileID.Dirt;
            if (Main.tile[x + 1, y].TileType == TileID.Grass)
                Main.tile[x + 1, y].TileType = TileID.Dirt;

            WorldGen.TileFrame(x - 1, y);
            WorldGen.TileFrame(x, y);
            WorldGen.TileFrame(x + 1, y);
            WorldGen.TileFrame(x, y - 1);
            WorldGen.TileFrame(x, y + 1);
        }
    }

    public override void NearbyEffects(int i, int j, bool closer)
    {
        Main.tile[i, j].LiquidAmount = 255;
    }
}
