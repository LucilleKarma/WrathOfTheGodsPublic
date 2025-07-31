using NoxusBoss.Core.Graphics.UI.GraphicalUniverseImager;

using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace NoxusBoss.Content.Tiles.TileEntities;

public class TEGraphicalUniverseImager : ModTileEntity, IClientSideTileEntityUpdater
{
    /// <summary>
    /// Whether this tile entity is visible for the purposes of the UI.
    /// </summary>
    public bool UIEnabled
    {
        get;
        set;
    }

    /// <summary>
    /// The 0-1 interpolant that determines how visible the UI associated with this GUI should be.
    /// </summary>
    public float UIAppearanceInterpolant
    {
        get;
        set;
    }

    /// <summary>
    /// The settings associated with this GUI.
    /// </summary>
    public GraphicalUniverseImagerSettings Settings
    {
        get;
        set;
    } = new GraphicalUniverseImagerSettings();

    public override bool IsTileValidForEntity(int x, int y)
    {
        //This check did not work in MP for some reason, is it required tho?
        //Tile tile = Main.tile[x, y];
        //return tile.HasTile && tile.TileType == ModContent.TileType<GraphicalUniverseImagerTile>() && tile.TileFrameX == 0 && tile.TileFrameY == 0;

        return true;
    }

    public void ClientSideUpdate()
    {
        bool uiCanAppear = Main.LocalPlayer.WithinRange(Position.ToWorldCoordinates(), 150f);
        bool teSelectedByUI = ModContent.GetInstance<UniverseImagerUI>().VisibleTileEntity == this;
        bool uiIsVisible = uiCanAppear && teSelectedByUI && UIEnabled;
        UIAppearanceInterpolant = Saturate(UIAppearanceInterpolant + uiIsVisible.ToDirectionInt() * 0.035f);

        if (UIAppearanceInterpolant <= 0f && teSelectedByUI)
            ModContent.GetInstance<UniverseImagerUI>().VisibleTileEntity = null;
    }

    public override void PreGlobalUpdate()
    {
        base.PreGlobalUpdate();
    }

    public override void SaveData(TagCompound tag)
    {
        (Settings ??= new GraphicalUniverseImagerSettings()).Save(tag);
    }

    public override void LoadData(TagCompound tag)
    {
        Settings = new GraphicalUniverseImagerSettings();
        Settings.Load(tag);
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

    public override void NetSend(BinaryWriter writer)
    {
        Settings.Send(writer);
    }

    public override void NetReceive(BinaryReader reader)
    {
        Settings.Receive(reader);
    }
}
