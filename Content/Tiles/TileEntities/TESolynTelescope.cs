using Luminance.Common.Easings;
using Luminance.Core.Graphics;

using Microsoft.Xna.Framework;

using NoxusBoss.Content.Tiles.SolynCampsite;

using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;

namespace NoxusBoss.Content.Tiles.TileEntities;

public class TESolynTelescope : ModTileEntity, IClientSideTileEntityUpdater
{
    public float UIAppearanceInterpolant
    {
        get;
        set;
    }

    /// <summary>
    /// How long it's been, in frames, since this telescope was repaired.
    /// </summary>
    public int PostRepairTime
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this telescope is repaired or not.
    /// </summary>
    public bool IsRepaired
    {
        get;
        set;
    }

    /// <summary>
    /// The color of text in the UI if the player has a sufficient quantity of a given item requirement.
    /// </summary>
    public static readonly Color HasMaterialColor = new Color(21, 172, 81);

    /// <summary>
    /// The color of text in the UI if the player does not have a sufficient quantity of a given item requirement.
    /// </summary>
    public static readonly Color DoesntHaveMaterialColor = new Color(190, 37, 21);

    public override bool IsTileValidForEntity(int x, int y)
    {
        Tile tile = Main.tile[x, y];
        return tile.HasTile && tile.TileType == ModContent.TileType<SolynTelescopeTile>() && tile.TileFrameX == 0 && tile.TileFrameY == 0;
    }

    public override void SaveData(TagCompound tag)
    {
        if (IsRepaired)
            tag["IsRepaired"] = true;
        tag["PostRepairTime"] = PostRepairTime;
    }

    public override void LoadData(TagCompound tag)
    {
        IsRepaired = tag.ContainsKey("IsRepaired");
        PostRepairTime = tag.GetInt("PostRepairTime");
    }

    public void ClientSideUpdate()
    {
        bool uiShouldAppear = Main.LocalPlayer.WithinRange(Position.ToWorldCoordinates(), 120f) && !IsRepaired;
        UIAppearanceInterpolant = Saturate(UIAppearanceInterpolant + uiShouldAppear.ToDirectionInt() * 0.035f);

        if (IsRepaired)
            PostRepairTime++;

        if (PostRepairTime == 1 || PostRepairTime == 32)
            CreateRepairVisuals();
    }

    /// <summary>
    /// Creates impact repair visuals for this telescope.
    /// </summary>
    public void CreateRepairVisuals()
    {
        // Create sparkles to indicate repair.
        for (int i = 0; i < 25; i++)
        {
            Vector2 sparkleSpawnPosition = Position.ToWorldCoordinates() + Main.rand.NextVector2Square(0f, 1f) * new Vector2(SolynTelescopeTile.Width, SolynTelescopeTile.Height - 0.8f) * 16f;

            Dust sparkle = Dust.NewDustPerfect(sparkleSpawnPosition, 261);
            sparkle.color = Color.Lerp(Color.Wheat, Color.Gold, Main.rand.NextFloat());
            sparkle.velocity = Main.rand.NextVector2Circular(7f, 0.4f) - Vector2.UnitY * Main.rand.NextFloat(4f);
            sparkle.noGravity = true;
            sparkle.fadeIn = Main.rand.NextFloat(0.7f, 2f);
            sparkle.scale = 0.6f;
            sparkle.rotation = 0f;
        }

        ScreenShakeSystem.StartShakeAtPoint(Position.ToWorldCoordinates(), 2.7f, shakeStrengthDissipationIncrement: 0.45f);
    }

    /// <summary>
    /// Renders the UI for this telescope.
    /// </summary>
    public void RenderUI()
    {
        float opacity = UIAppearanceInterpolant;
        float scale = Lerp(1.2f, 1f, UIAppearanceInterpolant);

        // Without this the item rendering can momentarily flicker on the first few frames for some reason.
        if (opacity <= 0.2f)
        {
            foreach (var kv in SolynTelescopeTile.RepairRequirements)
                Main.instance.LoadItem(kv.Key);

            return;
        }

        Vector2 animationOffset = Vector2.UnitY * EasingCurves.Quintic.Evaluate(EasingType.InOut, 1f - UIAppearanceInterpolant) * -100f;
        animationOffset.Y += Cos01(Main.GlobalTimeWrappedHourly * 2f) * 10f - 10f;

        Vector2 uiBottom = Position.ToWorldCoordinates() + new Vector2(10f, -24f) - Main.screenPosition + animationOffset;

        // Draw the cost indicators.
        Vector2 uiTop = uiBottom - Vector2.UnitY * scale * 100f;
        Vector2 itemPosition = uiTop + Vector2.UnitY * scale * 30f - Vector2.One * scale * 17.5f;
        foreach (var kv in SolynTelescopeTile.RepairRequirements)
        {
            Color oldBackgroundColor = Main.inventoryBack;
            Main.inventoryBack *= opacity;

            Item item = new Item(kv.Key, kv.Value);
            ItemSlot.Draw(Main.spriteBatch, ref item, 1, itemPosition, Color.White * opacity.Squared());
            itemPosition.Y += scale * 40f;

            Main.inventoryBack = oldBackgroundColor;
        }
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
        writer.Write(IsRepaired);
    }

    public override void NetReceive(BinaryReader reader)
    {
        IsRepaired = reader.ReadBoolean();
    }
}
