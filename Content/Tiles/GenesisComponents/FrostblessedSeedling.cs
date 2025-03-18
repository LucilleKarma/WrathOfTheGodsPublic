using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.Dusts;
using NoxusBoss.Content.Items.GenesisComponents;
using NoxusBoss.Content.Particles;
using NoxusBoss.Core.SolynEvents;
using NoxusBoss.Core.SoundSystems;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace NoxusBoss.Content.Tiles.GenesisComponents;

public class FrostblessedSeedling : ModTile, ICustomPlacementSound
{
    public SoundStyle PlaceSound => GennedAssets.Sounds.Genesis.FrostblessedSeedlingPlant;

    /// <summary>
    /// The tiled width of the seedling.
    /// </summary>
    public const int Width = 3;

    /// <summary>
    /// The tiled height of the seedling.
    /// </summary>
    public const int Height = 4;

    /// <summary>
    /// The glowmask of the seedling.
    /// </summary>
    public static LazyAsset<Texture2D> Glowmask
    {
        get;
        private set;
    }

    /// <summary>
    /// The edge glow of the seedling.
    /// </summary>
    public static LazyAsset<Texture2D> EdgeGlow
    {
        get;
        private set;
    }

    /// <summary>
    /// The back glow of the seedling.
    /// </summary>
    public static LazyAsset<Texture2D> BackGlow
    {
        get;
        private set;
    }

    public override string Texture => GetAssetPath("Content/Tiles/GenesisComponents", Name);

    public override void SetStaticDefaults()
    {
        Main.tileLighted[Type] = true;
        Main.tileFrameImportant[Type] = true;
        TileID.Sets.CanBeClearedDuringGeneration[Type] = false;
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

        AddMapEntry(new Color(132, 112, 172));

        HitSound = SoundID.Item27;
        DustType = DustID.Ice;
        RegisterItemDrop(ModContent.ItemType<FrostblessedSeedlingItem>());

        if (Main.netMode != NetmodeID.Server)
        {
            Glowmask = LazyAsset<Texture2D>.FromPath($"{Texture}Glowmask");
            EdgeGlow = LazyAsset<Texture2D>.FromPath($"{Texture}EdgeGlow");
            BackGlow = LazyAsset<Texture2D>.FromPath($"{Texture}BackGlow");
        }
    }

    public override bool CanExplode(int i, int j) => false;

    public override bool CanKillTile(int i, int j, ref bool blockDamaged) => ModContent.GetInstance<PermafrostKeepEvent>().Finished;

    public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
    {
        r = 2f;
        g = 2f;
        b = 2f;
    }

    public override void NearbyEffects(int i, int j, bool closer)
    {
        if (Main.gamePaused)
            return;

        if (Main.rand.NextBool(40))
        {
            Vector2 sparkleSpawnPosition = new Vector2(i, j).ToWorldCoordinates() + Main.rand.NextVector2Square(0f, 16f);
            TwinkleParticle sparkle = new TwinkleParticle(sparkleSpawnPosition, Vector2.Zero, Color.Wheat, 36, 5, Vector2.One * 0.4f);
            sparkle.Spawn();
        }
        if (Main.rand.NextBool(7))
        {
            Vector2 sparkleSpawnPosition = new Vector2(i, j).ToWorldCoordinates() + Main.rand.NextVector2Square(-24f, 24f);

            int dustID = ModContent.DustType<TwinkleDust>();
            Dust sparkle = Dust.NewDustPerfect(sparkleSpawnPosition, dustID);
            sparkle.color = Color.Lerp(Color.Wheat, Color.LightCyan, Main.rand.NextFloat());
            sparkle.velocity = -Vector2.UnitY * Main.rand.NextFloat(0.4f);
            sparkle.scale = 0.4f;
        }
    }

    public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Tile tile = Main.tile[i, j];
        Vector2 drawPosition = new Vector2(i * 16, j * 16 + 2) - Main.screenPosition + (Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange, Main.offScreenRange));

        Color color = Color.Lerp(Lighting.GetColor(i, j), Color.White, 0.75f);
        Texture2D texture = TextureAssets.Tile[Type].Value;

        Rectangle frame = new Rectangle(tile.TileFrameX, tile.TileFrameY + AnimationFrameHeight * Main.tileFrame[Type], 16, 16);
        spriteBatch.Draw(texture, drawPosition, frame, color);

        if (tile.TileFrameX == 36 && tile.TileFrameY == 54)
        {
            Vector2 glowCenter = drawPosition + new Vector2(-8f, 16f);
            spriteBatch.Draw(BackGlow.Value, glowCenter, null, Color.White with { A = 0 } * 0.67f, 0f, BackGlow.Size() * new Vector2(0.5f, 1f), 1f, 0, 0f);
            spriteBatch.Draw(EdgeGlow.Value, glowCenter, null, Color.White, 0f, EdgeGlow.Size() * new Vector2(0.5f, 1f), 1f, 0, 0f);
        }
        return false;
    }

    public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Color color = Color.Lerp(Lighting.GetColor(i, j), Color.White, 0.7f);
        color.A = 0;
        color *= 0.5f;

        Tile tile = Main.tile[i, j];
        Vector2 drawPosition = new Vector2(i * 16, j * 16 + 2) - Main.screenPosition + (Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange, Main.offScreenRange));
        Rectangle frame = new Rectangle(tile.TileFrameX, tile.TileFrameY + AnimationFrameHeight * Main.tileFrame[Type], 16, 16);
        spriteBatch.Draw(Glowmask.Value, drawPosition, frame, color);
    }
}
