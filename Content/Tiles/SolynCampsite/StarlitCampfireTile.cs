using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Content.Particles;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.Drawing;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Terraria.Utilities;

namespace NoxusBoss.Content.Tiles.SolynCampsite;

public class StarlitCampfireTile : ModTile
{
    private Asset<Texture2D> flameTexture;

    public override string Texture => GetAssetPath("Content/Tiles/SolynCampsite", Name);

    public override void SetStaticDefaults()
    {
        Main.tileLighted[Type] = true;
        Main.tileFrameImportant[Type] = true;
        TileID.Sets.HasOutlines[Type] = true;
        TileID.Sets.InteractibleByNPCs[Type] = true;
        TileID.Sets.Campfire[Type] = true;

        DustType = -1;
        AdjTiles = new int[] { TileID.Campfire };

        TileObjectData.newTile.CopyFrom(TileObjectData.GetTileData(TileID.Campfire, 0));
        TileObjectData.newTile.StyleLineSkip = 9;
        TileObjectData.addTile(Type);

        AddMapEntry(new Color(255, 68, 152), Language.GetText("ItemName.Campfire"));

        if (Main.netMode != NetmodeID.Server)
            flameTexture = ModContent.Request<Texture2D>($"{Texture}_Flame");
    }

    public override void NearbyEffects(int i, int j, bool closer)
    {
        if (Main.tile[i, j].TileFrameY < 36)
            Main.SceneMetrics.HasCampfire = true;
    }

    public override void MouseOver(int i, int j)
    {
        Player player = Main.LocalPlayer;
        player.noThrow = 2;
        player.cursorItemIconEnabled = true;

        int style = TileObjectData.GetTileStyle(Main.tile[i, j]);
        player.cursorItemIconID = TileLoader.GetItemDropFromTypeAndStyle(Type, style);
    }

    public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

    public override bool RightClick(int i, int j)
    {
        SoundEngine.PlaySound(SoundID.Mech, new Vector2(i * 16, j * 16));
        ToggleTile(i, j);
        return true;
    }

    public override void HitWire(int i, int j)
    {
        ToggleTile(i, j);
    }

    // ToggleTile is a method that contains code shared by HitWire and RightClick, since they both toggle the state of the tile.
    // Note that TileFrameY doesn't necessarily match up with the image that is drawn, AnimateTile and AnimateIndividualTile contribute to the drawing decisions.
    public static void ToggleTile(int i, int j)
    {
        Tile tile = Main.tile[i, j];
        int topX = i - tile.TileFrameX % 54 / 18;
        int topY = j - tile.TileFrameY % 36 / 18;

        short frameAdjustment = (short)(tile.TileFrameY >= 36 ? -36 : 36);

        for (int x = topX; x < topX + 3; x++)
        {
            for (int y = topY; y < topY + 2; y++)
            {
                Main.tile[x, y].TileFrameY += frameAdjustment;
                if (Wiring.running)
                    Wiring.SkipWire(x, y);
            }
        }

        if (Main.netMode != NetmodeID.SinglePlayer)
            NetMessage.SendTileSquare(-1, topX, topY, 3, 2);
    }

    public override void AnimateTile(ref int frame, ref int frameCounter)
    {
        frameCounter++;
        frame = frameCounter / 4 % 8;
    }
    public override void AnimateIndividualTile(int type, int i, int j, ref int frameXOffset, ref int frameYOffset)
    {
        Tile tile = Main.tile[i, j];
        if (tile.TileFrameY < 36)
            frameYOffset = Main.tileFrame[type] * 36;

        else
            frameYOffset = 252;
    }

    public override void DrawEffects(int i, int j, SpriteBatch spriteBatch, ref TileDrawInfo drawData)
    {
        if (Main.gamePaused || !Main.instance.IsActive)
            return;

        if (Main.tile[i, j].TileFrameY != 0)
            return;

        if (!Lighting.UpdateEveryFrame || new FastRandom(Main.TileFrameSeed).WithModifier(i, j).Next(4) == 0)
        {
            // Create smoke.
            int smokeLifetime = Main.rand.Next(60, 240);
            Color smokeColor = Main.hslToRgb(0.964f, Main.rand.NextFloat(0.7f, 1f), 0.5f);
            smokeColor = Color.Lerp(smokeColor, Color.DarkGray * 0.28f, 0.85f);

            if (Main.rand.NextBool())
            {
                Vector2 smokeSpawnPosition = new Vector2(i + Main.rand.NextFloatDirection() * 0.5f, j + 2f).ToWorldCoordinates();
                Vector2 smokeVelocity = new Vector2(Main.rand.NextFloatDirection() * 0.5f + Main.windSpeedCurrent * 0.6f, Main.rand.NextFloat(-3.6f, -2f));
                HighDefinitionSmokeParticle smoke = new HighDefinitionSmokeParticle(smokeSpawnPosition, smokeVelocity, smokeColor, smokeLifetime, 0.6f, 0.01f);
                smoke.Spawn();
            }

            // Create embers.
            if (Main.rand.NextBool(24))
            {
                int emberLifetime = Main.rand.Next(60, 420);
                Color emberColor = Main.hslToRgb(0.964f, Main.rand.NextFloat(0.7f, 1f), 0.5f);
                Vector2 emberSpawnPosition = new Vector2(i + Main.rand.NextFloatDirection() * 0.5f, j).ToWorldCoordinates();
                Vector2 emberVelocity = new Vector2(Main.rand.NextFloatDirection() * 0.95f + Main.windSpeedCurrent * 2f, Main.rand.NextFloat(-8f, -1f)).RotatedByRandom(0.7f);
                EmberParticle ember = new EmberParticle(emberSpawnPosition, emberVelocity, emberColor, 2f, emberLifetime);
                ember.Spawn();
            }
        }
    }

    public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
    {
        r = 0.8f;
        g = 0.15f;
        b = 0.42f;
    }

    public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Tile tile = Main.tile[i, j];

        if (!TileDrawing.IsVisible(tile))
            return;

        if (tile.TileFrameY < 36)
        {
            Color glowmaskColor = new Color(255, 255, 255, 0);

            Vector2 drawOffset = new Vector2(Main.offScreenRange, Main.offScreenRange);
            if (Main.drawToScreen)
                drawOffset = Vector2.Zero;

            int width = 16;
            int offsetY = 0;
            int height = 16;
            short frameX = tile.TileFrameX;
            short frameY = tile.TileFrameY;
            int addFrX = 0;
            int addFrY = 0;

            TileLoader.SetDrawPositions(i, j, ref width, ref offsetY, ref height, ref frameX, ref frameY);
            TileLoader.SetAnimationFrame(Type, i, j, ref addFrX, ref addFrY);

            Rectangle frame = new Rectangle(tile.TileFrameX, tile.TileFrameY + addFrY, 16, 16);

            // The flame is manually drawn separate from the tile texture so that it can be drawn at full brightness.
            spriteBatch.Draw(flameTexture.Value, new Vector2(i * 16 - (int)Main.screenPosition.X, j * 16 - (int)Main.screenPosition.Y + offsetY) + drawOffset, frame, glowmaskColor, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
        }
    }
}
