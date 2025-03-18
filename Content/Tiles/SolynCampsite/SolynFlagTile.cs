using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.Tiles.TileEntities;
using NoxusBoss.Core.Netcode;
using NoxusBoss.Core.Netcode.Packets;
using NoxusBoss.Core.World.WorldGeneration;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.Graphics;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace NoxusBoss.Content.Tiles.SolynCampsite;

public class SolynFlagTile : ModTile
{
    /// <summary>
    /// The tiled width of this flagpole.
    /// </summary>
    public const int Width = 1;

    /// <summary>
    /// The tiled height of this flagpole.
    /// </summary>
    public const int Height = 10;

    public override string Texture => GetAssetPath("Content/Tiles/SolynCampsite", Name);

    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileObsidianKill[Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style1xX);
        TileObjectData.newTile.Width = Width;
        TileObjectData.newTile.Height = Height;
        TileObjectData.newTile.Origin = new Point16(0, Height - 1);
        TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
        TileObjectData.newTile.CoordinateHeights = Enumerable.Repeat(16, TileObjectData.newTile.Height).ToArray();
        TileObjectData.newTile.LavaDeath = false;
        TileObjectData.newTile.DrawYOffset = 2;

        // Set the respective tile entity as a secondary element to incorporate when placing this tile.
        ModTileEntity tileEntity = ModContent.GetInstance<TESolynFlag>();
        TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(tileEntity.Hook_AfterPlacement, -1, 0, true);

        TileObjectData.addTile(Type);
        AddMapEntry(new Color(80, 53, 79));

        HitSound = SoundID.Tink;
    }

    public override void PlaceInWorld(int i, int j, Item item)
    {
        if (SolynCampsiteWorldGen.FlagPosition == Point.Zero)
        {
            SolynCampsiteWorldGen.FlagPosition = new Point(i, j);
            PacketManager.SendPacket<SetSolynFlagPositionPacket>(SolynCampsiteWorldGen.FlagPosition);
        }
    }

    public override void KillMultiTile(int i, int j, int frameX, int frameY)
    {
        Tile tile = Main.tile[i, j];
        int left = i - tile.TileFrameX % (Width * 16) / 16;
        int top = j - tile.TileFrameY % (Height * 16) / 16;

        // Kill the hosted tile entity directly and immediately.
        TESolynFlag? flag = FindTileEntity<TESolynFlag>(i, j, Width, Height);
        flag?.Kill(left, top);
    }

    public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Tile tile = Main.tile[i, j];
        TESolynFlag? flag = FindTileEntity<TESolynFlag>(i, j, Width, Height);
        if (flag is null || tile.TileFrameY != (Height - 2) * 18)
            return;

        int verticalOffset = 1 - Height;
        Color lightColorTopLeft = Lighting.GetColor(i, j + verticalOffset);
        Color lightColorTopRight = Lighting.GetColor(i + 3, j + verticalOffset);
        Color lightColorBottomLeft = Lighting.GetColor(i, j + verticalOffset + 2);
        Color lightColorBottomRight = Lighting.GetColor(i + 3, j + verticalOffset + 2);
        VertexColors color = new VertexColors(lightColorTopLeft, lightColorTopRight, lightColorBottomRight, lightColorBottomLeft);

        Vector2 drawOffset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
        drawOffset.Y += 28f;

        Vector2 start = new Vector2(i, j - Height + 1).ToWorldCoordinates() - Main.screenPosition + drawOffset;
        Matrix matrix = Matrix.CreateOrthographicOffCenter(0f, Main.instance.GraphicsDevice.Viewport.Width, Main.instance.GraphicsDevice.Viewport.Height, 0f, -1f, 1f);
        flag.FlagCloth.Render(start, color, matrix, GennedAssets.Textures.SolynCampsite.SolynFlag);
    }
}
