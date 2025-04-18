using Microsoft.Xna.Framework;
using NoxusBoss.Content.Items.Placeable;
using NoxusBoss.Core.Netcode;
using NoxusBoss.Core.Netcode.Packets;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.Tiles.SolynRopes;

public class SolynRopePlacementPreviewSystem : ModSystem
{
    /// <summary>
    /// The set of all solyn rope placement previews, keyed by player index.
    /// </summary>
    internal static readonly Dictionary<int, SolynRopeData> previews = new Dictionary<int, SolynRopeData>(Main.maxPlayers);

    /// <summary>
    /// Creates a new preview rope for a given player.
    /// </summary>
    /// <param name="player">The player that owns the preview rope.</param>
    /// <param name="ropeStart">The starting position of the rope.</param>
    public static void CreateForPlayer(Player player, Vector2 ropeStart)
    {
        if (!IsValidPlacementSpot(player, ropeStart.ToTileCoordinates()))
            return;

        Point ropeStartPoint = ropeStart.ToPoint();
        previews[player.whoAmI] = new SolynRopeData(ropeStartPoint, ropeStartPoint)
        {
            DropsItem = true
        };
    }

    /// <summary>
    /// Tries to remove a preview rope for a given player.
    /// </summary>
    /// <param name="player">The player to destroy the preview rope for.</param>
    public static void DestroyForPlayer(Player player) => previews.Remove(player.whoAmI);

    internal static bool IsValidPlacementSpot(Player player, Point end)
    {
        Rectangle placementRectangle = Utils.CenteredRectangle(player.Center / 16f, new Vector2(player.lastTileRangeX + 7, player.lastTileRangeY + 7));
        Tile ropeEndTile = Framing.GetTileSafely(new Point(end.X, end.Y));

        bool success = true;
        if (!placementRectangle.Contains(end))
            success = false;
        if (!ropeEndTile.HasTile)
            success = false;

        return success;
    }

    private static void ProcessRopePlacementAttempt(Player player, SolynRopeData rope)
    {
        if (IsValidPlacementSpot(player, rope.VerletRope.Rope[^1].Position.ToTileCoordinates()))
        {
            SolynRopeSystem.Register(rope);
            if (Main.netMode != NetmodeID.SinglePlayer)
                PacketManager.SendPacket<RegisterSolynRopePacket>(rope.Start.X, rope.Start.Y, rope.End.X, rope.End.Y, (byte)rope.DropsItem.ToInt());
        }

        // Give the player their placed rope set back if it failed to place.
        else if (Main.myPlayer == player.whoAmI)
            Item.NewItem(new EntitySource_TileBreak(rope.Start.X, rope.Start.Y), rope.Start.ToVector2(), ModContent.ItemType<FancyRopeSet>());

        DestroyForPlayer(player);
    }

    public override void PostUpdatePlayers()
    {
        foreach (var kv in previews)
        {
            SolynRopeData rope = kv.Value;
            if (Main.myPlayer == kv.Key)
            {
                if (Main.mouseLeft && Main.mouseLeftRelease)
                    ProcessRopePlacementAttempt(Main.LocalPlayer, rope);
                rope.Update_Preview();
            }
        }
    }

    public override void PostDrawTiles()
    {
        if (previews.Count <= 0)
            return;

        Main.spriteBatch.ResetToDefault(false);

        foreach (var kv in previews)
        {
            SolynRopeData rope = kv.Value;
            if (Main.myPlayer == kv.Key)
            {
                bool validPlacementSpot = IsValidPlacementSpot(Main.LocalPlayer, rope.VerletRope.Rope[^1].Position.ToTileCoordinates());
                Color colorModifier = validPlacementSpot ? Color.White : (Color.Red * 0.7f);
                rope.Render(false, colorModifier);
            }
        }

        Main.spriteBatch.End();
    }
}
