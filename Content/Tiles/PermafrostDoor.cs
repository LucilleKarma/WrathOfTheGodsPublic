using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using NoxusBoss.Assets;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using NoxusBoss.Core.Netcode;
using NoxusBoss.Core.Netcode.Packets;
using NoxusBoss.Core.SolynEvents;
using NoxusBoss.Core.World.WorldGeneration;

using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace NoxusBoss.Content.Tiles;

public class PermafrostDoor : ModTile
{
    /// <summary>
    /// The tiled width of this door.
    /// </summary>
    public const int Width = 1;

    /// <summary>
    /// The tiled height of this door.
    /// </summary>
    public const int Height = 3;

    /// <summary>
    /// The white overlay texture for this door.
    /// </summary>
    public static LazyAsset<Texture2D> WhiteTexture
    {
        get;
        private set;
    }

    public static bool CanTryToUnlock => Main.LocalPlayer.GetValueRef<bool>(PermafrostKeepWorldGen.PlayerWasGivenKeyVariableName) && ModContent.GetInstance<PermafrostKeepEvent>().Stage >= 1;

    public override string Texture => GetAssetPath("Content/Tiles", Name);

    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileObsidianKill[Type] = false;
        Main.tileSolid[Type] = true;
        TileID.Sets.CanBeClearedDuringGeneration[Type] = false;

        TileID.Sets.HasOutlines[Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style1xX);
        TileObjectData.newTile.Width = Width;
        TileObjectData.newTile.Height = Height;
        TileObjectData.newTile.Origin = new Point16(0, Height - 1);
        TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
        TileObjectData.newTile.CoordinateHeights = Enumerable.Repeat(16, TileObjectData.newTile.Height).ToArray();
        TileObjectData.newTile.LavaDeath = false;

        TileObjectData.addTile(Type);
        AddMapEntry(new Color(32, 100, 210));

        WhiteTexture = LazyAsset<Texture2D>.FromPath($"{Texture}White");

        HitSound = SoundID.Tink;
    }

    public override bool CanExplode(int i, int j) => false;

    public override bool CanKillTile(int i, int j, ref bool blockDamaged) => false;

    public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => CanTryToUnlock;

    public override bool RightClick(int i, int j)
    {
        if (CanTryToUnlock)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                PacketManager.SendPacket<UnlockPermafrostKeepDoorPacket>(i, j);
            }

            PermafrostDoorUnlockSystem.Start(new(i, j));

            return true;
        }

        return false;
    }

    public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Tile t = Main.tile[i, j];
        int frameX = t.TileFrameX;
        int frameY = t.TileFrameY;

        // Draw the main tile texture.
        Vector2 drawOffset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
        Vector2 drawPosition = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + drawOffset;

        Point topLeft = new Point(i - frameX / 16, j - frameY / 16);
        if (PermafrostDoorUnlockSystem.DoorBrightnesses.TryGetValue(topLeft, out float brightness))
        {
            brightness = InverseLerpBump(0f, 1.1f, 1.11f, 2.25f, brightness).Squared() * 1.85f;

            Texture2D white = WhiteTexture.Value;
            spriteBatch.Draw(white, drawPosition, new Rectangle(frameX, frameY, 16, 16), Color.White * brightness, 0f, Vector2.Zero, 1f, 0, 0f);
        }
    }
}
