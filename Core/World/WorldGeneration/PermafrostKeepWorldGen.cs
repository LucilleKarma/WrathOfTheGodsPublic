using Microsoft.Xna.Framework;
using NoxusBoss.Content.Tiles;
using NoxusBoss.Core.CrossCompatibility.Inbound.CalamityRemix;
using NoxusBoss.Core.GlobalInstances;
using StructureHelper;
using Terraria;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.WorldBuilding;

namespace NoxusBoss.Core.World.WorldGeneration;

public class PermafrostKeepWorldGen : ModSystem
{
    /// <summary>
    /// Whether Permafrost has given a player the key to the keep.
    /// </summary>
    public static bool PlayerGivenKey
    {
        get;
        set;
    }

    /// <summary>
    /// Whether the door has been unlocked or not.
    /// </summary>
    public static bool DoorHasBeenUnlocked
    {
        get;
        set;
    }

    /// <summary>
    /// The area of Permafrost's keep in the world.
    /// </summary>
    public static Rectangle KeepArea
    {
        get;
        set;
    }

    /// <summary>
    /// The variable name that dictates whether a given player picked up Permafrost's key.
    /// </summary>
    public const string PlayerWasGivenKeyVariableName = "WasGivenPermafrostKey";

    public override void OnModLoad()
    {
        PlayerDataManager.SaveDataEvent += SavePlayerKeyData;
        PlayerDataManager.LoadDataEvent += LoadPlayerKeyData;
        GlobalItemEventHandlers.CanUseItemEvent += DisallowRodOfDiscordIntoKeep;
        GlobalNPCEventHandlers.EditSpawnRateEvent += DisallowSpawnsNearKeep;
        On_Player.PlaceThing_Walls += DisableWallPlacementNearKeep;
        On_Player.PlaceThing_Tiles += DisableTilePlacementNearKeep;
    }

    private static bool DisallowRodOfDiscordIntoKeep(Item item, Player player)
    {
        if (item.type == ItemID.RodofDiscord || item.type == ItemID.RodOfHarmony)
        {
            Vector2 teleportPosition = Main.MouseWorld;
            player.LimitPointToPlayerReachableArea(ref teleportPosition);

            Point teleportPoint = teleportPosition.ToTileCoordinates();
            if (IsProtected(teleportPoint.X, teleportPoint.Y))
                return false;
        }

        return true;
    }

    private static void DisallowSpawnsNearKeep(Player player, ref int spawnRate, ref int maxSpawns)
    {
        if (player.WithinRange(KeepArea.Center().ToWorldCoordinates(), 1600f))
        {
            spawnRate = 1000000;
            maxSpawns = 0;
        }
    }

    private static void DisableWallPlacementNearKeep(On_Player.orig_PlaceThing_Walls orig, Player self)
    {
        Point placementPoint = new Point(Player.tileRangeX, Player.tileRangeY);
        if (!IsProtected(placementPoint))
            orig(self);
    }

    private static void DisableTilePlacementNearKeep(On_Player.orig_PlaceThing_Tiles orig, Player self)
    {
        Point placementPoint = new Point(Player.tileRangeX, Player.tileRangeY);
        if (!IsProtected(placementPoint))
            orig(self);
    }

    public override void OnWorldLoad()
    {
        PlayerGivenKey = false;
        DoorHasBeenUnlocked = false;
    }

    public override void OnWorldUnload()
    {
        PlayerGivenKey = false;
        DoorHasBeenUnlocked = false;
    }

    public override void SaveWorldData(TagCompound tag)
    {
        if (PlayerGivenKey)
            tag["PlayerGivenKey"] = true;
        if (DoorHasBeenUnlocked)
            tag["DoorHasBeenUnlocked"] = true;

        tag["KeepAreaX"] = KeepArea.X;
        tag["KeepAreaY"] = KeepArea.Y;
        tag["KeepAreaWidth"] = KeepArea.Width;
        tag["KeepAreaHeight"] = KeepArea.Height;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        PlayerGivenKey = tag.ContainsKey("PlayerGivenKey");
        DoorHasBeenUnlocked = tag.ContainsKey("DoorHasBeenUnlocked");

        if (tag.TryGet("KeepAreaX", out int x) && tag.TryGet("KeepAreaY", out int y) && tag.TryGet("KeepAreaWidth", out int width) && tag.TryGet("KeepAreaHeight", out int height))
            KeepArea = new(x, y, width, height);
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write((byte)DoorHasBeenUnlocked.ToInt());
        writer.Write(KeepArea.X);
        writer.Write(KeepArea.Y);
        writer.Write(KeepArea.Width);
        writer.Write(KeepArea.Height);
    }

    public override void NetReceive(BinaryReader reader)
    {
        DoorHasBeenUnlocked = reader.ReadByte() >= 1;
        int keepX = reader.ReadInt32();
        int keepY = reader.ReadInt32();
        int keepWidth = reader.ReadInt32();
        int keepHeight = reader.ReadInt32();
        KeepArea = new Rectangle(keepX, keepY, keepWidth, keepHeight);
    }

    public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
    {
        tasks.Insert(tasks.Count, new PassLegacy("Generate Permafrost Keep", (progress, config) =>
        {
            progress.Message = "Burying frozen secrets...";
            Generate();
        }));
    }

    /// <summary>
    /// Determines whether a given tile point is protected by the keep's magic seal.
    /// </summary>
    /// <param name="x">The X position in tile coordinates.</param>
    /// <param name="y">The Y position in tile coordinates.</param>
    public static bool IsProtected(int x, int y) => !DoorHasBeenUnlocked && KeepArea.Contains(x, y);

    /// <summary>
    /// Determines whether a given tile point is protected by the keep's magic seal.
    /// </summary>
    /// <param name="p">The position in tile coordinates.</param>
    public static bool IsProtected(Point p) => IsProtected(p.X, p.Y);

    /// <summary>
    /// Attempts to generate Permafrost's keep somewhere in the underground snow biome.
    /// </summary>
    public static void Generate()
    {
        string path = "Core/World/WorldGeneration/Structures/PermafrostKeep";
        TagCompound tag = (TagCompound)typeof(Generator).GetMethod("GetTag", UniversalBindingFlags)!.Invoke(null, [path, ModContent.GetInstance<NoxusBoss>(), false])!;

        Vector2 cutoutArea = new Vector2(tag.GetInt("Width"), tag.GetInt("Height"));
        Point? generationCenter = DecideGenerationCenter(cutoutArea);
        if (generationCenter is null)
            return;

        // Clear water and falling tiles (such as silt) around the keep.
        KeepArea = Utils.CenteredRectangle(generationCenter.Value.ToVector2() + cutoutArea * 0.5f, cutoutArea);
        for (int i = KeepArea.Left - 25; i < KeepArea.Right + 25; i++)
        {
            for (int j = KeepArea.Top - 25; j < KeepArea.Bottom + 25; j++)
            {
                Main.tile[i, j].Get<LiquidData>().Amount = 0;

                int tileID = Main.tile[i, j].TileType;
                bool isFallingTile = tileID == TileID.Sand || tileID == TileID.Silt || tileID == TileID.Slush;
                if (isFallingTile)
                    Main.tile[i, j].Get<TileWallWireStateData>().HasTile = false;
            }
        }

        GenVars.structures.AddProtectedStructure(KeepArea);
        FargosCompatibilitySystem.MarkRectangleAsIndestructible(KeepArea, true);

        Generator.GenerateStructure(path, new(generationCenter.Value.X, generationCenter.Value.Y), ModContent.GetInstance<NoxusBoss>());

        // Replace the first normal book tile with a special book tile that Solyn takes.
        for (int i = KeepArea.Left; i < KeepArea.Right; i++)
        {
            for (int j = KeepArea.Top; j < KeepArea.Bottom; j++)
            {
                int tileID = Main.tile[i, j].TileType;
                if (tileID == TileID.Books)
                {
                    Main.tile[i, j].TileType = (ushort)ModContent.TileType<InscrutableTextsPlaced>();
                    return;
                }
            }
        }
    }

    private static Point? DecideGenerationCenter(Vector2 scanArea)
    {
        int underworldTop = Main.UnderworldLayer;
        for (int i = 0; i < 10000; i++)
        {
            int placementPositionX = WorldGen.genRand.Next(120, Main.maxTilesX - 120);
            int placementPositionY = WorldGen.genRand.Next((int)Main.worldSurface + 160, underworldTop - 100);

            // Do a simple scan for snow or ice tiles.
            // This only checks a single tile, for efficiency.
            Tile t = Framing.GetTileSafely(placementPositionX, placementPositionY);
            if (t.TileType != TileID.SnowBlock && t.TileType != TileID.IceBlock)
                continue;

            // Verify that the given position won't collide with existing structures.
            Rectangle scanAreaRectangle = Utils.CenteredRectangle(new(placementPositionX, placementPositionY), scanArea);
            if (!GenVars.structures.CanPlace(scanAreaRectangle))
                continue;

            // Now that a narrow search on a single tile has been performed, thus reducing the quantity of potential candidates, check again for
            // ice and snow, this time more broadly.
            // This will search many tiles at random within the scan area and determine what ratio of them were snow and ice, to eliminate edge-cases.
            int totalValidTiles = 0;
            for (int j = 0; j < 200; j++)
            {
                Tile searchTile = Framing.GetTileSafely(WorldGen.genRand.Next(scanAreaRectangle.Left, scanAreaRectangle.Right), WorldGen.genRand.Next(scanAreaRectangle.Top, scanAreaRectangle.Bottom));
                bool searchTileIsValidType = searchTile.TileType == TileID.SnowBlock || t.TileType == TileID.IceBlock;
                if (searchTile.HasUnactuatedTile && searchTileIsValidType)
                    totalValidTiles++;
            }
            if (totalValidTiles < 100)
                continue;

            // All checks have been passed. Return the randomly selected point.
            return new(placementPositionX, placementPositionY);
        }

        return null;
    }

    private void SavePlayerKeyData(PlayerDataManager p, TagCompound tag)
    {
        if (p.GetValueRef<bool>(PlayerWasGivenKeyVariableName))
            tag[PlayerWasGivenKeyVariableName] = true;
    }

    private void LoadPlayerKeyData(PlayerDataManager p, TagCompound tag)
    {
        p.GetValueRef<bool>(PlayerWasGivenKeyVariableName).Value = tag.ContainsKey(PlayerWasGivenKeyVariableName);
    }
}
