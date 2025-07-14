using System.Numerics;

using NoxusBoss.Content.Tiles.SolynCampsite;
using NoxusBoss.Core.Graphics.ClothSimulations;
using NoxusBoss.Core.World.TileDisabling;

using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.Tiles.TileEntities;

public class TESolynFlag : ModTileEntity, IClientSideTileEntityUpdater
{
    /// <summary>
    /// The flag cloth that this tile entity holds locally.
    /// </summary>
    public ClothSimulation FlagCloth
    {
        get;
        private set;
    } = new ClothSimulation(30, 15, 3.3f);

    public override bool IsTileValidForEntity(int x, int y)
    {
        Tile tile = Main.tile[x, y];
        return tile.HasTile && tile.TileType == ModContent.TileType<SolynFlagTile>() && tile.TileFrameX == 0 && tile.TileFrameY == 0;
    }

    public void ClientSideUpdate_RenderCycle()
    {
        for (int i = 0; i < FlagCloth.Height; i++)
            FlagCloth[0, i].Locked = true;

        if (!TileDisablingSystem.TilesAreUninteractable)
            FlagCloth.Update(new Vector3(FlagCloth.DesiredSpacing * 0.00451f, 0f, 0f), 0.15f);
    }

    public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate)
    {
        // If in multiplayer, tell the server to place the tile entity and DO NOT place it yourself. That would mismatch IDs.
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            NetMessage.SendData(MessageID.TileEntityPlacement, -1, -1, null, i, j, Type);
            return -1;
        }
        return Place(i, j);
    }

    // Sync the tile entity the moment it is place on the server.
    // This is done to cause it to register among all clients.
    public override void OnNetPlace() => NetMessage.SendData(MessageID.TileEntitySharing, -1, -1, null, ID, Position.X, Position.Y);
}
