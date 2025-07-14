using System.Reflection;

using Luminance.Core.Graphics;
using Luminance.Core.Hooking;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Mono.Cecil.Cil;

using MonoMod.Cil;

using NoxusBoss.Content.Dusts;
using NoxusBoss.Content.NPCs.Bosses.Avatar.FirstPhaseForm;
using NoxusBoss.Content.Tiles;
using NoxusBoss.Core.CrossCompatibility.Inbound;
using NoxusBoss.Core.Graphics.SwagRain;
using NoxusBoss.Core.Graphics.UI.GraphicalUniverseImager;

using Terraria;
using Terraria.GameContent.Drawing;
using Terraria.GameContent.Shaders;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace NoxusBoss.Core.World.GameScenes.RiftEclipse;

public class RiftEclipseSnowSystem : ModSystem
{
    private static bool placingSnowOverWorld;

    /// <summary>
    /// The amount by which rain/snow effects are increased in their chance of occuring.
    /// </summary>
    public static float SnowFrequencyIncreaseFactor => 3f;

    /// <summary>
    /// The vertical height of all snow across the world.
    /// </summary>
    public static float SnowHeight
    {
        get;
        set;
    }

    /// <summary>
    /// The maximum height that snow can reach.
    /// </summary>
    public static float MaxSnowHeight => 10f;

    /// <summary>
    /// How intense the snow is. This increases throughout progression.
    /// </summary>
    public static float IntensityInterpolant
    {
        get
        {
            if (NPC.AnyNPCs(ModContent.NPCType<AvatarRift>()))
                return 0.5f;
            if (!RiftEclipseManagementSystem.RiftEclipseOngoing)
                return 0f;
            if (CommonCalamityVariables.DevourerOfGodsDefeated)
                return 1f;
            if (CommonCalamityVariables.StormWeaverDefeated || CommonCalamityVariables.CeaselessVoidDefeated || CommonCalamityVariables.SignusDefeated)
                return 0.45f;
            if (CommonCalamityVariables.ProvidenceDefeated)
                return 0.2f;

            return 0.1f;
        }
    }

    /// <summary>
    /// Whether the cold is sufficient enough for ice to cover the surface of aboveground lakes.
    /// </summary>
    public static bool IceCanCoverWater => CommonCalamityVariables.ProvidenceDefeated && RiftEclipseManagementSystem.RiftEclipseOngoing;

    /// <summary>
    /// Whether the cold is sufficient enough for ice to completely freeze aboveground lakes.
    /// </summary>
    public static bool IceCanFreezeAllWater => CommonCalamityVariables.YharonDefeated && RiftEclipseManagementSystem.RiftEclipseOngoing;

    /// <summary>
    /// How cold the trees are. This increases throughout progression.
    /// </summary>
    public static float TreeColdnessInterpolant
    {
        get
        {
            if (GraphicalUniverseImagerSky.EclipseConfigOption == GraphicalUniverseImagerSettings.EclipseSecondaryAmbienceSetting.Blizzard)
                return 1f;
            if (!RiftEclipseManagementSystem.RiftEclipseOngoing)
                return 0f;
            if (CommonCalamityVariables.DevourerOfGodsDefeated)
                return 1f;
            if (CommonCalamityVariables.StormWeaverDefeated || CommonCalamityVariables.CeaselessVoidDefeated || CommonCalamityVariables.SignusDefeated)
                return 0.67f;
            if (CommonCalamityVariables.ProvidenceDefeated)
                return 0.33f;

            return 0.25f;
        }
    }

    /// <summary>
    /// Whether the snow can appear.
    /// </summary>
    public static bool EventCanHappen => RiftEclipseManagementSystem.RiftEclipseOngoing && !AnyBosses();

    public override void OnModLoad()
    {
        new ManagedILEdit("Recolor Trees", Mod, edit =>
        {
            IL_TileDrawing.PostDrawTiles += edit.SubscriptionWrapper;
        }, edit =>
        {
            IL_TileDrawing.PostDrawTiles -= edit.SubscriptionWrapper;
        }, RecolorTrees).Apply();

        On_Rain.MakeRain += DisableRain;
        On_BlizzardShaderData.Apply += ChangeBlizzardShaderColors;
        On_NPC.ShouldBestiaryGirlBeLycantrope += DisableZoologistLycanthropeTransformation;
        On_TileDrawing.EmitTreeLeaves += DisableWindLeaves;
    }

    private void DisableWindLeaves(On_TileDrawing.orig_EmitTreeLeaves orig, TileDrawing self, int tilePosX, int tilePosY, int grassPosX, int grassPosY)
    {
        if (!RiftEclipseManagementSystem.RiftEclipseOngoing)
            orig(self, tilePosX, tilePosY, grassPosX, grassPosY);
    }

    private void ChangeBlizzardShaderColors(On_BlizzardShaderData.orig_Apply orig, BlizzardShaderData self)
    {
        // These are the default values used for the shader. These need to be redefined here since it only gets set by vanilla once in ScreenEffectInitializer.
        float intensity = 0.4f;
        Vector3 snowColor = new Vector3(1f, 1f, 1f);
        Vector3 vignetteColor = new Vector3(0.7f, 0.7f, 1f);

        // Use darker colors for the shader when the Avatar is covering the sun/moon.
        if (RiftEclipseManagementSystem.RiftEclipseOngoing && Main.raining && EventCanHappen)
        {
            snowColor = new(2f, 2f, 3f);
            vignetteColor = new(-0.4f, 0.23f, 0.64f);
            intensity = 0.2f;
        }

        self.UseColor(snowColor).UseSecondaryColor(vignetteColor).UseIntensity(intensity);
        orig(self);
    }

    private bool DisableZoologistLycanthropeTransformation(On_NPC.orig_ShouldBestiaryGirlBeLycantrope orig, NPC self)
    {
        return !RiftEclipseManagementSystem.RiftEclipseOngoing && orig(self);
    }

    private void RecolorTrees(ILContext context, ManagedILEdit edit)
    {
        static void DrawTreeTiles()
        {
            if (TreeColdnessInterpolant <= 0f)
                return;

            Vector2 topLeft = Main.Camera.UnscaledPosition;
            Vector2 drawOffset = Main.Camera.UnscaledPosition - Main.Camera.ScaledPosition;
            Vector2 screenOffset = -Main.screenPosition;
            if (!Main.drawToScreen)
                drawOffset += Vector2.One * Main.offScreenRange;

            int left = (int)((topLeft.X - drawOffset.X) / 16f - 1f);
            int right = (int)((topLeft.X + Main.screenWidth + drawOffset.X) / 16f) + 2;
            int top = (int)((topLeft.Y - drawOffset.Y) / 16f - 1f);
            int bottom = (int)((topLeft.Y + Main.screenHeight + drawOffset.Y) / 16f) + 5;
            if (left < 4)
                left = 4;
            if (right > Main.maxTilesX - 4)
                right = Main.maxTilesX - 4;
            if (top < 4)
                top = 4;
            if (bottom > Main.maxTilesY - 4)
                bottom = Main.maxTilesY - 4;

            for (int i = left; i < right + 4; i++)
            {
                for (int j = top - 2; j < bottom + 2; j++)
                {
                    Tile tile = Main.tile[i, j];
                    if (tile.HasTile && (tile.TileType == TileID.Trees || tile.TileType == TileID.PalmTree || tile.TileType == TileID.VanityTreeSakura || tile.TileType == TileID.VanityTreeYellowWillow))
                    {
                        short frameX = tile.TileFrameX;
                        short frameY = tile.TileFrameY;
                        Main.instance.TilesRenderer.GetTileDrawData(i, j, tile, tile.TileType, ref frameX, ref frameY, out int tileWidth,
                            out int tileHeight, out int tileTop, out int halfBrickHeight, out int frameDx, out int frameDy, out SpriteEffects direction, out Texture2D texture, out _, out _);

                        Vector2 tileDrawPosition = new Vector2(i, j) * 16f + screenOffset - Vector2.UnitX * 2f;
                        if (tile.TileType == TileID.PalmTree)
                        {
                            if (tile.TileFrameX > 132 || tile.TileFrameX < 88)
                                tileDrawPosition.X += tile.TileFrameY;
                        }

                        Texture2D tileTexture = Main.instance.TilesRenderer.GetTileDrawTexture(tile, i, j);
                        Main.spriteBatch.Draw(tileTexture, tileDrawPosition, new Rectangle(frameX, frameY, tileWidth, tileHeight), Lighting.GetColor(i, j), 0f, Vector2.Zero, 1f, 0, 0f);
                    }
                }
            }
        }
        static void PrepareTreeRecolorShader()
        {
            if (TreeColdnessInterpolant <= 0f)
                return;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);

            float freezeDissipation = InverseLerp(3300f, 0f, Main.screenPosition.Y - (float)Main.worldSurface * 16f);

            ManagedShader treeFreezeShader = ShaderManager.GetShader("NoxusBoss.TreeFreezeShader");
            treeFreezeShader.TrySetParameter("freezeInterpolant", TreeColdnessInterpolant * SnowHeight / MaxSnowHeight * freezeDissipation);
            treeFreezeShader.Apply();
        }

        static void ResetTreeRecolorShader()
        {
            if (TreeColdnessInterpolant <= 0f)
                return;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
        }

        ILCursor cursor = new ILCursor(context);
        MethodInfo? drawTreesMethod = typeof(TileDrawing).GetMethod("DrawTrees", BindingFlags.NonPublic | BindingFlags.Instance);
        if (drawTreesMethod is null)
        {
            edit.LogFailure("The TileDrawing.DrawTrees method search returned null.");
            return;
        }

        if (!cursor.TryGotoNext(MoveType.Before, new Func<Instruction, bool>[]
        {
            i => i.MatchLdarg0(),
            i => i.MatchCallOrCallvirt(drawTreesMethod)
        }))
        {
            edit.LogFailure("The DrawTrees method could not be found.");
            return;
        }

        // Prepare the shader.
        cursor.EmitDelegate(() =>
        {
            PrepareTreeRecolorShader();
            DrawTreeTiles();
        });

        // Go after the DrawTrees method this time and reset the sprite batch.
        cursor.Goto(0);
        if (!cursor.TryGotoNext(MoveType.After, new Func<Instruction, bool>[]
        {
            i => i.MatchLdarg0(),
            i => i.MatchCallOrCallvirt(drawTreesMethod)
        }))
        {
            edit.LogFailure("The DrawTrees method could not be found.");
            return;
        }

        // Reset the sprite batch.
        cursor.EmitDelegate(ResetTreeRecolorShader);
    }

    private void DisableRain(On_Rain.orig_MakeRain orig)
    {
        if (AvatarRift.Myself is not null)
            return;
        if (RiftEclipseBloodMoonRainSystem.EffectActive)
            return;

        if (!RiftEclipseManagementSystem.RiftEclipseOngoing || !EventCanHappen || IntensityInterpolant < 1f)
        {
            float originalCloudAlpha = Main.cloudAlpha;
            Main.cloudAlpha *= 1f - IntensityInterpolant;
            orig();
            Main.cloudAlpha = originalCloudAlpha;
        }
    }

    public override void PreUpdateWorld()
    {
        // Periodically check if the snow should be placed.
        if (!placingSnowOverWorld && (int)Main.time % 2018 == 1717)
        {
            placingSnowOverWorld = true;
            new Thread(PerformSnowCheck).Start();
        }

        bool snowIsRising = RiftEclipseManagementSystem.RiftEclipseOngoing && Main.raining;
        bool snowIsFalling = !RiftEclipseManagementSystem.RiftEclipseOngoing;
        float snowRiseRate = 0.006f;
        if (GraphicalUniverseImagerSky.EclipseConfigOption == GraphicalUniverseImagerSettings.EclipseSecondaryAmbienceSetting.Blizzard)
        {
            snowIsRising = true;
            snowIsFalling = false;
            snowRiseRate = 0.04f;
        }

        SnowHeight += snowIsRising.ToInt() * snowRiseRate;
        SnowHeight -= snowIsFalling.ToInt() * 0.03f;
        SnowHeight = Clamp(SnowHeight, 0f, MaxSnowHeight);
    }

    public override void PreUpdateDusts()
    {
        // Create snow if necessary.
        bool createSnow = (Main.raining && RiftEclipseManagementSystem.RiftEclipseOngoing && EventCanHappen) || NPC.AnyNPCs(ModContent.NPCType<AvatarRift>());
        float intensityInterpolant = IntensityInterpolant;
        if (GraphicalUniverseImagerSky.EclipseConfigOption == GraphicalUniverseImagerSettings.EclipseSecondaryAmbienceSetting.Blizzard)
        {
            createSnow = true;
            intensityInterpolant = 2f;
        }
        if (RiftEclipseBloodMoonRainSystem.EffectActive)
            createSnow = false;

        if (createSnow)
            CreateSnowParticles(intensityInterpolant);
    }

    public static void CreateSnowParticles(float intensityInterpolant)
    {
        if (Main.netMode == NetmodeID.Server)
            return;

        Vector2 cameraArea = Main.Camera.ScaledSize;
        Vector2 cameraPosition = Main.Camera.ScaledPosition;
        if (Main.gamePaused)
            return;

        if (Main.LocalPlayer.Top.Y >= Main.worldSurface * 16.0)
            return;

        // Calculate the amount of snow to try to spawn, along with its natural quantity limits.
        // These are increased the more intense the rain is.
        float screenWidthRatio = Main.Camera.ScaledSize.X / Main.maxScreenW;
        int snowDustLimit = (int)((int)(screenWidthRatio * 500f) * (Main.cloudAlpha * 2f + 1f));
        int snowDustSpawnTries = (int)Ceiling((Main.cloudAlpha * 20f + 12f) * Pow(intensityInterpolant, 1.9f));

        // Make the GFX quality setting affect dust spawn quantities.
        snowDustLimit = (int)(snowDustLimit * (Main.gfxQuality * 0.5f + 0.6f) * Pow(intensityInterpolant, 1.4f));

        int snowID = ModContent.DustType<EntropicSnowDust>();
        for (int i = 0; i < snowDustSpawnTries; i++)
        {
            if (Main.snowDust >= snowDustLimit)
                break;

            int dustSpawnX = Main.rand.Next(-1250, (int)cameraArea.X + 1250);
            int dustSpawnY = Main.rand.Next(-320, 0);
            if (Main.LocalPlayer.velocity.Y > 0f)
                dustSpawnY -= (int)Main.LocalPlayer.velocity.Y;

            // Offset dust to the screen area.
            dustSpawnX += (int)cameraPosition.X;
            dustSpawnY += (int)cameraPosition.Y;

            // Ignore potential spawn positions in space.
            if (dustSpawnY <= 4200f)
                continue;

            // Ignore potential spawn positions that have an unactuated tile or a wall in the wall.
            int tileX = dustSpawnX / 16;
            int tileY = dustSpawnY / 16;
            if (!WorldGen.InWorld(tileX, tileY) || Main.tile[tileX, tileY].HasUnactuatedTile || Main.tile[tileX, tileY].WallType != WallID.None)
                continue;

            // Calculate snow speed/scale variable and spawn the snow.
            float snowScale = Main.rand.NextFloat(0.8f, 1.32f) + Main.cloudAlpha * 0.55f;
            if (AvatarRift.Myself is not null)
                snowScale *= 1.24f;

            float snowSpeedX = Sqrt(Math.Abs(Main.windSpeedCurrent)) * Math.Sign(Main.windSpeedCurrent) * (Main.cloudAlpha + 0.5f) * 8f + Main.rand.NextFloat(-0.1f, 0.1f) * 0.7f;
            float snowSpeedY = Main.rand.NextFloat(1f, snowScale * 2f + 2f) * (Main.cloudAlpha * 0.3f + 1f);
            if (Main.rand.NextBool(5))
            {
                snowSpeedX *= 1.7f;
                snowSpeedY *= 0.5f;
            }

            Vector2 snowVelocity = new Vector2(snowSpeedX, snowSpeedY) * (1f + Main.cloudAlpha * 0.5f) * Pow(snowScale, 1.4f);
            Dust snow = Dust.NewDustPerfect(new Vector2(dustSpawnX, dustSpawnY), snowID);
            snow.scale = snowScale;
            snow.velocity = snowVelocity;
            snow.color = Color.White;
            snow.rotation = Main.rand.NextFloat(TwoPi);
            snow.fadeIn = 1.5f;

            Main.snowDust++;
        }
    }

    private static Point FindGroundVerticalSafe(Point p)
    {
        if (WorldGen.SolidTile(p))
        {
            while (p.Y >= 5 && WorldGen.SolidTile(p.X, p.Y - 1))
            {
                p.Y--;
            }
        }
        else
        {
            while (p.Y < Main.maxTilesY - 5 && !WorldGen.SolidTile(p.X, p.Y + 1))
            {
                p.Y++;
            }
        }

        return p;
    }

    public static void PerformSnowCheck()
    {
        int snowID = ModContent.TileType<RiftEclipseSnow>();
        bool placingSnow = RiftEclipseManagementSystem.RiftEclipseOngoing && Main.raining;
        bool destroyingSnow = !RiftEclipseManagementSystem.RiftEclipseOngoing;

        List<Point> snowPoints = [];

        for (int i = 5; i < Main.maxTilesX - 5; i++)
        {
            Point p = FindGroundVerticalSafe(new Point(i, (int)Main.worldSurface - 250));
            Point above = new Point(p.X, p.Y - 1);
            Point below = new Point(p.X, p.Y + 1);

            if (p.Y <= Main.worldSurface - 230)
                continue;

            Tile t = Framing.GetTileSafely(p);
            Tile aboveTile = Framing.GetTileSafely(above);
            Tile belowTile = Framing.GetTileSafely(below);
            if (!Main.tileSolid[belowTile.TileType] && !Main.tileSolidTop[belowTile.TileType])
                continue;

            // Ignore tiles that are not solid.
            if (belowTile.IsHalfBlock || belowTile.Slope != SlopeType.Solid)
                continue;

            // Ignore tiles that are submerged in liquids.
            if (aboveTile.LiquidAmount >= 1)
                continue;

            // Place or destroy snow.
            int checkTileID = t.TileType;
            bool plant = checkTileID == TileID.Plants || checkTileID == TileID.Plants2;
            bool plantJungle = checkTileID == TileID.JunglePlants || checkTileID == TileID.JunglePlants2;
            bool plantCorrupt = checkTileID == TileID.CorruptPlants;
            bool plantCrimson = checkTileID == TileID.CrimsonPlants;
            bool plantHallow = checkTileID == TileID.HallowedPlants || checkTileID == TileID.HallowedPlants2;
            bool plantDesert = checkTileID == TileID.SeaOats || checkTileID == TileID.Cactus;
            bool thorn = checkTileID == TileID.CorruptThorns || checkTileID == TileID.CrimsonThorns || checkTileID == TileID.JungleThorns;
            bool pebble = checkTileID == TileID.SmallPiles || checkTileID == TileID.LargePiles;
            bool tileCanBeDestroyed = plant || plantJungle || plantCorrupt || plantCrimson || plantHallow || plantDesert || thorn || pebble || !t.HasTile || checkTileID == snowID;
            if (placingSnow && aboveTile.TileType != snowID && tileCanBeDestroyed)
            {
                if (checkTileID != snowID)
                    WorldGen.KillTile(p.X, p.Y);
                Framing.GetTileSafely(p).TileType = (ushort)snowID;
                Framing.GetTileSafely(p).Get<TileWallWireStateData>().HasTile = true;
                snowPoints.Add(p);
            }
            if (destroyingSnow && aboveTile.TileType == snowID)
                WorldGen.KillTile(p.X, p.Y - 1);
            if (destroyingSnow && belowTile.TileType == snowID)
                WorldGen.KillTile(p.X, p.Y + 1);
            if (destroyingSnow && t.TileType == snowID)
                WorldGen.KillTile(p.X, p.Y);
        }

        // Apply framing and syncing.
        for (int i = 0; i < snowPoints.Count; i++)
        {
            WorldGen.TileFrame(snowPoints[i].X, snowPoints[i].Y, true);
            WorldGen.TileFrame(snowPoints[i].X, snowPoints[i].Y - 1, true);
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendTileSquare(-1, snowPoints[i].X, snowPoints[i].Y);
        }

        placingSnowOverWorld = false;
    }

    public override void SaveWorldData(TagCompound tag)
    {
        tag[nameof(SnowHeight)] = SnowHeight;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        if (tag.TryGet(nameof(SnowHeight), out float snowHeight))
            SnowHeight = snowHeight;
    }

    public override void OnWorldLoad()
    {
        SnowHeight = 0;
    }

    public override void OnWorldUnload()
    {
        SnowHeight = 0;
    }

    public override void NetReceive(BinaryReader reader)
    {
        SnowHeight = reader.ReadSingle();
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(SnowHeight);
    }
}
