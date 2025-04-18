using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Core.GlobalInstances;
using NoxusBoss.Core.World.GameScenes.RiftEclipse;
using NoxusBoss.Core.World.TileDisabling;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.GameContent.RGB;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace NoxusBoss.Content.Tiles;

public class RiftEclipseSnow : ModTile
{
    private static LazyAsset<Texture2D> snowGrassTexture;

    private static LazyAsset<Texture2D> snowMudGrassTexture;

    public static readonly SoundStyle SnowStepSound = GennedAssets.Sounds.Environment.SnowStep with { Volume = 0.1f, MaxInstances = 0 };

    public override string Texture => GetAssetPath("Content/Tiles", Name);

    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = false;
        Main.tileBlockLight[Type] = false;
        TileID.Sets.BreakableWhenPlacing[Type] = true;
        DustType = DustID.SnowBlock;
        HitSound = SoundID.Item48 with { Pitch = -0.2f, Volume = 0.4f };

        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
        TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
        TileObjectData.newTile.CoordinateHeights = Enumerable.Repeat(16, TileObjectData.newTile.Height).ToArray();
        TileObjectData.addTile(Type);

        GlobalTileEventHandlers.PreDrawEvent += DrawSpecialSnowOnGrassEvent;

        if (Main.netMode != NetmodeID.Server)
        {
            snowGrassTexture = LazyAsset<Texture2D>.FromPath("NoxusBoss/Assets/Textures/Content/Tiles/RiftEclipseSnowGrass");
            snowMudGrassTexture = LazyAsset<Texture2D>.FromPath("NoxusBoss/Assets/Textures/Content/Tiles/RiftEclipseSnowMudGrass");
        }

        // Ok so you would ask "Hey why aren't you just using ModTile.FloorVisuals?" Well, becase FloorVisuals DOESNT INCLUDE THE FALLING BOOL??? WHY??
        On_Player.FloorVisuals += CreateSnowParticlesWhenWalking;
    }

    private void CreateSnowParticlesWhenWalking(On_Player.orig_FloorVisuals orig, Player self, bool falling)
    {
        orig(self, falling);

        if (self.gravDir < 0 || RiftEclipseSnowSystem.SnowHeight <= 1f)
            return;

        CreateSnowWalkEffects(self, falling);
    }

    public static void CreateSnowWalkEffects(Entity self, bool falling)
    {
        if (TileDisablingSystem.TilesAreUninteractable)
            return;

        int tileX = (int)(self.Center.X / 16f);
        int tileY = (int)(self.Bottom.Y / 16f) - 1;
        Tile floor = Framing.GetTileSafely(tileX, tileY);

        if (floor.TileType != ModContent.TileType<RiftEclipseSnow>() || !floor.HasUnactuatedTile || tileY >= Main.worldSurface)
            return;

        // Dust on ground collision.
        Vector2 dustSpawnBottom = self.Bottom + Vector2.UnitY * 3f;
        if (falling)
        {
            // Big heavy snow.
            for (int i = 0; i < 32; i++)
            {
                Vector2 dustPosition = dustSpawnBottom + Vector2.UnitX * Main.rand.NextFloat(-self.width / 2, self.width / 2);
                Vector2 dustVelocity = dustPosition.DirectionFrom(dustSpawnBottom + Vector2.UnitY * 45f) * new Vector2(2.5f, 0.4f) * Main.rand.NextFloat(1f, 6f);
                Dust snow = Dust.NewDustPerfect(dustPosition, DustID.Snow, dustVelocity, Scale: Main.rand.NextFloat(1f, 2.3f));
                snow.fadeIn = Main.rand.NextFloat(1.2f, 2.1f);
                snow.noGravity = true;
            }

            // A bunch of tiny snow.
            for (int i = 0; i < 12; i++)
            {
                Vector2 dustPosition = dustSpawnBottom + Vector2.UnitX * Main.rand.NextFloat(-self.width / 2, self.width / 2);
                Vector2 dustVelocity = dustPosition.DirectionFrom(dustSpawnBottom + Vector2.UnitY * 45f) * Main.rand.NextFloat(4f, 6f);

                Dust snow = Dust.NewDustPerfect(dustPosition, DustID.Snow, dustVelocity, Scale: Main.rand.NextFloat(0.65f, 1f));
                snow.fadeIn = 1.2f;
                snow.noGravity = true;
            }

            // Play an impact sound.
            SoundEngine.PlaySound(SnowStepSound with { Pitch = 0.23f, Volume = 0.3f }, dustSpawnBottom);
        }

        // Dust when walking.
        else if (Math.Abs(self.velocity.X) >= 0.1f)
        {
            // Lingering snow.
            if (Main.rand.NextBool(3))
            {
                Vector2 dustPosition = dustSpawnBottom + Vector2.UnitX * Main.rand.NextFloat(-self.width / 2, self.width / 2);
                Dust snow = Dust.NewDustPerfect(dustPosition, DustID.Snow, -Vector2.UnitY * Main.rand.NextFloat(1f, 2f) + Vector2.UnitX * self.velocity.X * 0.1f, Scale: Main.rand.NextFloat(0.95f, 1.2f));
                snow.fadeIn = 1.2f;
                snow.noGravity = true;
            }

            // Super light small snow.
            if (Main.rand.NextBool(2))
            {
                Vector2 dustPosition = dustSpawnBottom + Vector2.UnitX * Main.rand.NextFloat(-self.width / 2, self.width / 2);
                Vector2 dustVelocity = new Vector2(Math.Sign(self.velocity.X) * Math.Clamp(Math.Abs(self.velocity.X) * Main.rand.NextFloat(1.5f, 4f), 0f, 10f), -Main.rand.NextFloat(1.5f, 4f));
                Dust snow = Dust.NewDustPerfect(dustPosition, DustID.Snow, dustVelocity, Scale: Main.rand.NextFloat(0.65f, 1f));
                snow.fadeIn = 0.4f;
                snow.noGravity = true;
            }

            // Lighter snow near the feet.
            if (Main.rand.NextBool(3))
            {
                Vector2 dustPosition = dustSpawnBottom + Vector2.UnitX * Main.rand.NextFloat(-self.width / 2, self.width / 2);
                Dust snow = Dust.NewDustPerfect(dustPosition, DustID.Snow, -Vector2.UnitY * Main.rand.NextFloat(0.5f, 2f) + Vector2.UnitX * self.velocity.X * Main.rand.NextFloat(1f, 2f), Scale: Main.rand.NextFloat(0.8f, 1.6f));
                snow.fadeIn = 0.6f;
                snow.noGravity = true;
            }

            // Heavy snow that doesnt move much.
            if (Main.rand.NextBool(9))
            {
                Vector2 dustPosition = dustSpawnBottom + Vector2.UnitX * Main.rand.NextFloat(-self.width / 2, self.width / 2);
                Dust snow = Dust.NewDustPerfect(dustPosition, DustID.Snow, -Vector2.UnitY * Main.rand.NextFloat(0.5f, 0.5f) + Vector2.UnitX * self.velocity.X * Main.rand.NextFloat(0.4f, 0.7f), Scale: Main.rand.NextFloat(1f, 2.2f));
                snow.fadeIn = 1.33f;
                snow.noGravity = true;
            }

            // Create step sounds.
            if (Math.Abs(self.velocity.X) >= 3f && Main.GameUpdateCount % 5 == 4)
                SoundEngine.PlaySound(SnowStepSound with { MaxInstances = 5, SoundLimitBehavior = SoundLimitBehavior.IgnoreNew });
        }
    }

    private bool DrawSpecialSnowOnGrassEvent(int x, int y, int type, SpriteBatch spriteBatch)
    {
        if (type == TileID.Grass || type == TileID.JungleGrass || type == TileID.CorruptGrass || type == TileID.CrimsonGrass || type == TileID.HallowedGrass)
        {
            DrawSpecialSnowOnGrass(x, y, type);
            return false;
        }

        return true;
    }

    private static void DrawSpecialSnowOnGrass(int x, int y, int type)
    {
        Tile t = Main.tile[x, y];
        int frameX = t.TileFrameX;
        int frameY = t.TileFrameY;

        // Draw the main tile texture.
        Texture2D mainTexture = TextureAssets.Tile[type].Value;
        if (t.TileColor != PaintID.None)
        {
            Texture2D paintedTexture = Main.instance.TilePaintSystem.TryGetTileAndRequestIfNotReady(t.TileType, 0, t.TileColor);
            mainTexture = paintedTexture ?? mainTexture;
        }

        Vector2 drawOffset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
        Color lightColor = Lighting.GetColor(x, y);

        Rectangle frame = new Rectangle(frameX, frameY, 16, 16);
        float snowDrawInterpolant = InverseLerp(0f, 0.2f, RiftEclipseSnowSystem.SnowHeight / RiftEclipseSnowSystem.MaxSnowHeight);
        snowDrawInterpolant *= InverseLerp(0f, 20f, (int)Main.worldSurface - y);

        if (snowDrawInterpolant < 1f)
            DrawTileWithSlope(x, y, mainTexture, frame, lightColor * (1f - snowDrawInterpolant), drawOffset);
        if (snowDrawInterpolant > 0f)
        {
            bool mudBase = type == TileID.JungleGrass || type == TileID.CorruptJungleGrass || type == TileID.CrimsonJungleGrass || type == TileID.MushroomGrass;
            Texture2D grassTexture = mudBase ? snowMudGrassTexture.Value : snowGrassTexture.Value;
            DrawTileWithSlope(x, y, grassTexture, frame, lightColor * snowDrawInterpolant, drawOffset);
        }
    }

    public override bool TileFrame(int i, int j, ref bool resetFrame, ref bool noBreak)
    {
        Tile bottom = Framing.GetTileSafely(i, j + 1);
        short standardXFrame = (short)((i * 13 + j * 9) % 3 * 18 + 18);

        Main.tile[i, j].TileFrameX = standardXFrame;
        if (!WorldGen.SolidTile(bottom))
            WorldGen.KillTile(i, j);

        return false;
    }

    public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Tile t = Main.tile[i, j];
        int frameX = t.TileFrameX;
        int frameY = t.TileFrameY;

        // Draw the main tile texture.
        Texture2D mainTexture = TextureAssets.Tile[Type].Value;
        Vector2 drawOffset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
        Vector2 drawPosition = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + drawOffset + new Vector2(8f, 16f);
        Color lightColor = Lighting.GetColor(i, j + 1);
        drawPosition.Y += RiftEclipseSnowSystem.MaxSnowHeight - RiftEclipseSnowSystem.SnowHeight + 2f;
        Rectangle frame = new Rectangle(frameX, frameY, 16, 16);

        Tile left = Framing.GetTileSafely(i - 1, j);
        Tile right = Framing.GetTileSafely(i + 1, j);
        float widthFactor = (WorldGen.SolidTile(i - 1, j) && left.TileType != Type) || (WorldGen.SolidTile(i + 1, j) && right.TileType != Type) ? 1.25f : 1f;
        float heightFactor = 1f + NoiseHelper.GetStaticNoise(i, j) * RiftEclipseSnowSystem.SnowHeight / RiftEclipseSnowSystem.MaxSnowHeight * 0.7f;
        if ((!WorldGen.SolidTile(left) && left.TileType != Type) || (!WorldGen.SolidTile(right) && right.TileType != Type))
        {
            heightFactor *= 0.8f;
            drawPosition.Y += 4f;
        }

        spriteBatch.Draw(mainTexture, drawPosition, frame, lightColor, 0f, frame.Size() * new Vector2(0.5f, 1f), new Vector2(widthFactor, heightFactor), 0, 0f);

        return false;
    }
}
