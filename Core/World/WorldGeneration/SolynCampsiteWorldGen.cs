using System.Reflection;

using Luminance.Core.Hooking;

using Microsoft.Xna.Framework;

using Mono.Cecil.Cil;

using MonoMod.Cil;

using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Content.Tiles.SolynCampsite;
using NoxusBoss.Content.Tiles.TileEntities;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.GlobalInstances;
using NoxusBoss.Core.SolynEvents;

using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace NoxusBoss.Core.World.WorldGeneration;

public class SolynCampsiteWorldGen : ModSystem
{
    private static bool generating;

    /// <summary>
    /// The position of the camp site, in world coordinates.
    /// </summary>
    public static Vector2 CampSitePosition
    {
        get;
        set;
    }

    /// <summary>
    /// The position of the tent, in world coordinates.
    /// </summary>
    public static Vector2 TentPosition
    {
        get;
        set;
    }

    /// <summary>
    /// The position of the telescope, in tile coordinates.
    /// </summary>
    public static Point TelescopePosition
    {
        get;
        set;
    }

    /// <summary>
    /// The position of Solyn's flag, in tile coordinates. This is used for determining where Solyn should set up camp.
    /// </summary>
    public static Point FlagPosition
    {
        get;
        set;
    }

    /// <summary>
    /// The closest that meteors can generate to Solyn's campsite.
    /// </summary>
    public static int MinMeteorDistance => 120;

    /// <summary>
    /// Whether Solyn has successfully set up camp.
    /// </summary>
    public static bool CampHasBeenMade => CampSitePosition != Vector2.Zero;

    public override void OnModLoad()
    {
        On_WorldGen.meteor += DisallowMeteorsDestroyingTheCampsite;
        GlobalNPCEventHandlers.EditSpawnRateEvent += DisableSpawnsNearCampsite;

        if (ModLoader.TryGetMod(CalamityCompatibility.ModName, out Mod cal))
        {
            MethodInfo? astralMeteorPlacementMethod = cal.Code.GetType("CalamityMod.World.AstralBiome")?.GetMethod("PlaceAstralMeteor", UniversalBindingFlags);
            if (astralMeteorPlacementMethod is not null)
            {
                HookHelper.ModifyMethodWithIL(astralMeteorPlacementMethod, new ILContext.Manipulator(context =>
                {
                    ILCursor cursor = new ILCursor(context);
                    if (!cursor.TryGotoNext(i => i.MatchNewobj<List<ushort>>()))
                        return;

                    int avoidanceListIndex = 0;
                    if (!cursor.TryGotoNext(MoveType.After, i => i.MatchStloc(out avoidanceListIndex)))
                        return;

                    cursor.Emit(OpCodes.Ldloc, avoidanceListIndex);
                    cursor.EmitDelegate((IList<ushort> avoidanceList) =>
                    {
                        avoidanceList.Add((ushort)ModContent.TileType<SolynTent>());
                        avoidanceList.Add((ushort)ModContent.TileType<SolynTelescopeTile>());
                    });
                }));
            }
        }
    }

    // NOTE -- Use the CampSitePosition variable when inspecting for places to place camp.
    // This is really important for backwards compatibility with worlds made prior to the patch.
    public override void PreUpdateWorld()
    {
        // Don't do anything if Solyn isn't present.
        if (!NPC.AnyNPCs(ModContent.NPCType<Solyn>()))
            return;

        // Don't do anything if generation is already in progress on a separate thread.
        if (generating)
            return;

        // If Solyn has not created a camp, check if she's ready to do so.
        if (!CampHasBeenMade)
        {
            CheckCampViability();
        }

        // Constantly check if the flagpole is gone.
        // If it is, wait for the player to put it somewhere else and set up camp there instead.
        bool flagStillExists = FlagPosition != Point.Zero && Framing.GetTileSafely(FlagPosition).HasTile && Framing.GetTileSafely(FlagPosition).TileType == ModContent.TileType<SolynFlagTile>();
        if (!flagStillExists)
        {
            FlagPosition = Point.Zero;
            CampSitePosition = Vector2.Zero;
        }
    }

    private static void CheckCampViability()
    {
        if (FlagPosition == Point.Zero)
            return;

        Tile t = Framing.GetTileSafely(FlagPosition);
        if (!t.HasTile || t.TileType != ModContent.TileType<SolynFlagTile>())
            return;

        if (generating)
            return;

        if (CampSitePosition != Vector2.Zero)
            return;

        bool anyoneNearFlag = false;
        foreach (Player player in Main.ActivePlayers)
        {
            if (player.WithinRange(FlagPosition.ToWorldCoordinates(), 3300f))
            {
                anyoneNearFlag = true;
                break;
            }
        }

        if (anyoneNearFlag)
            return;

        if (Main.rand.NextBool(10))
            PlaceCampOnNewThread(FlagPosition);
    }

    private static void CleanUpOldCampsite(List<Point> protectedPoints, Point tentPosition, Point telescopePosition)
    {
        int chairID = TileID.Chairs;
        int campfireID = ModContent.TileType<StarlitCampfireTile>();
        for (int dx = -80; dx < 80; dx++)
        {
            for (int dy = -32; dy < 32; dy++)
            {
                Tile t = Framing.GetTileSafely(tentPosition.X + dx, tentPosition.Y + dy);
                if (!t.HasTile)
                    continue;

                if (protectedPoints.Contains(new Point(tentPosition.X + dx, tentPosition.Y + dy)))
                    continue;

                if (t.TileType == chairID || t.TileType == campfireID)
                    Main.tile[tentPosition.X + dx, tentPosition.Y + dy].Get<TileWallWireStateData>().HasTile = false;
            }
        }

        if (tentPosition != Point.Zero)
            WorldGen.KillTile(tentPosition.X, tentPosition.Y, noItem: true);
        if (telescopePosition != Point.Zero)
            WorldGen.KillTile(telescopePosition.X, telescopePosition.Y, noItem: true);

        Point syncPos = CampSitePosition.ToTileCoordinates();

        CampSitePosition = Vector2.Zero;
        TentPosition = Vector2.Zero;
        TelescopePosition = Point.Zero;

        if (Main.netMode == NetmodeID.Server)
        {
            NetMessage.SendTileSquare(-1, syncPos.X, syncPos.Y, 100);
        }
    }

    private static bool FlatTerrainExists(Point checkPoint, int width, out Point result)
    {
        result = Point.Zero;
        Point p = FindGroundVertical(checkPoint);

        if (Distance(p.Y, checkPoint.Y) > 25f)
            return false;

        int coverageCheck = (int)Math.Ceiling(width * 0.5f) + 2;
        for (int dx = -coverageCheck; dx <= coverageCheck; dx++)
        {
            Point groundCheck = FindGroundVertical(new Point(p.X + dx, p.Y));

            // Ground is not level, return false.
            if (groundCheck.Y != p.Y)
                return false;

            // Check if there's anything in the way.
            if (Framing.GetTileSafely(groundCheck.X, groundCheck.Y - 1).HasTile && !TileID.Sets.BreakableWhenPlacing[Framing.GetTileSafely(groundCheck.X, groundCheck.Y - 1).TileType])
                return false;
        }

        result = p;
        return true;
    }

    private static bool TryToPlaceTile(Point position, int tileID, int style = 0, int direction = -1)
    {
        // Clear tiles like grass and small rubble.
        Tile existingTile = Framing.GetTileSafely(position);
        if (existingTile.HasTile && TileID.Sets.BreakableWhenPlacing[existingTile.TileType])
            WorldGen.KillTile(position.X, position.Y);

        WorldGen.PlaceObject(position.X, position.Y, tileID, false, style, 0, -1, direction);
        bool successful = Main.tile[position].TileType == tileID;
        return successful;
    }

    internal static void GetPlacementPositions(Point point, out Point campfirePosition, out Point telescopePosition, out Point tentPosition)
    {
        campfirePosition = Point.Zero;
        tentPosition = Point.Zero;
        telescopePosition = Point.Zero;
        List<int> blacklistedOffsets = new List<int>();

        for (int i = 0; i < 160; i++)
        {
            int range = i / 3 + 1;
            int dx = range * (i % 2 == 0).ToDirectionInt();
            Point checkPoint = new Point(point.X + dx, point.Y);
            if (blacklistedOffsets.Contains(dx))
                continue;

            // Try to find a position for the tent.
            if (tentPosition == Point.Zero && FlatTerrainExists(checkPoint, SolynTent.Width, out Point tentResult))
            {
                tentPosition = tentResult;
                blacklistedOffsets.AddRange(Enumerable.Range(dx - SolynTent.Width / 2 - 5, SolynTent.Width + 10));
            }

            if (blacklistedOffsets.Contains(dx))
                continue;

            // Try to find a position for the campfire.
            if (campfirePosition == Point.Zero && FlatTerrainExists(checkPoint, 4, out Point campfireResult))
            {
                campfirePosition = campfireResult;
                blacklistedOffsets.AddRange(Enumerable.Range(dx - 7, 14));
            }

            if (blacklistedOffsets.Contains(dx))
                continue;

            // Try to find a position for the telescope.
            if (telescopePosition == Point.Zero && FlatTerrainExists(checkPoint, SolynTelescopeTile.Width, out Point telescopeResult))
            {
                telescopePosition = telescopeResult;
                blacklistedOffsets.AddRange(Enumerable.Range(dx - SolynTelescopeTile.Width / 2 - 5, SolynTelescopeTile.Width + 10));
            }

            if (campfirePosition != Point.Zero && tentPosition != Point.Zero && telescopePosition != Point.Zero)
                return;
        }
    }

    private static bool PlaceCamp(Point point, List<Point> protectedPoints, out Point telescopePosition, out Point tentPosition)
    {
        GetPlacementPositions(point, out Point campfirePosition, out telescopePosition, out tentPosition);
        if (tentPosition == Point.Zero || campfirePosition == Point.Zero || telescopePosition == Point.Zero)
            return false;

        // Try to place the tent.
        int tentID = ModContent.TileType<SolynTent>();
        List<Point> placedTilesSoFar = new List<Point>();
        if (TryToPlaceTile(tentPosition, tentID))
        {
            TileObjectData.CallPostPlacementPlayerHook(tentPosition.X, tentPosition.Y, tentID, 0, -1, 0, default);
            placedTilesSoFar.Add(tentPosition);
        }
        else
            return false;

        // Try to place the telescope.
        // If it fails, delete everything made so far.
        int telescopeID = ModContent.TileType<SolynTelescopeTile>();
        if (TryToPlaceTile(telescopePosition, telescopeID))
        {
            TileObjectData.CallPostPlacementPlayerHook(telescopePosition.X, telescopePosition.Y, telescopeID, 0, -1, 0, default);
            placedTilesSoFar.Add(telescopePosition);
        }
        else
        {
            foreach (Point p in placedTilesSoFar)
                WorldGen.KillTile(p.X, p.Y);
            return false;
        }

        // Try to place the campfire.
        // If it fails, delete everything made so far.
        if (TryToPlaceTile(campfirePosition, ModContent.TileType<StarlitCampfireTile>(), 0, -1))
        {
            int spacing = 4;
            if (TryToPlaceTile(FindGroundVertical(new Point(campfirePosition.X - spacing, campfirePosition.Y)), TileID.Chairs, 27, 1))
                protectedPoints.Add(new Point(campfirePosition.X - spacing, campfirePosition.Y));

            if (TryToPlaceTile(FindGroundVertical(new Point(campfirePosition.X + spacing, campfirePosition.Y)), TileID.Chairs, 27, -1))
                protectedPoints.Add(new Point(campfirePosition.X + spacing, campfirePosition.Y));

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -2; dy <= 0; dy++)
                    protectedPoints.Add(new Point(campfirePosition.X + dx, campfirePosition.Y + dy));
            }
        }
        else
        {
            foreach (Point p in placedTilesSoFar)
                WorldGen.KillTile(p.X, p.Y);
            return false;
        }

        return true;
    }

    public static void PlaceCampOnNewThread(Point point)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient) 
            return;

        if (generating)
            return;

        new Thread(p =>
        {
            if (p is not Point pointParameter)
                return;

            generating = true;
            try
            {
                Point oldTentPosition = new Point((int)Round(TentPosition.X / 16f), (int)Round(TentPosition.Y / 16f));
                Point oldTelescopePosition = TelescopePosition;

                WorldGen.KillTile(TelescopePosition.X, TelescopePosition.Y);
                List<Point> protectedPoints = new List<Point>();
                if (PlaceCamp(pointParameter, protectedPoints, out Point telescopePosition, out Point tentPosition))
                {
                    // Scuffed way of ensuring that tent points don't get broken if they happened to get placed in the exact same spot.
                    if (oldTentPosition == tentPosition)
                        oldTentPosition = Point.Zero;
                    if (oldTelescopePosition == telescopePosition)
                        oldTelescopePosition = Point.Zero;

                    CleanUpOldCampsite(protectedPoints, oldTentPosition, oldTelescopePosition);
                    TelescopePosition = telescopePosition;
                    TentPosition = tentPosition.ToWorldCoordinates(8f, -8f);
                    CampSitePosition = point.ToWorldCoordinates();

                    bool telescopeIsRepaired = ModContent.GetInstance<StargazingEvent>().Finished;
                    if (telescopeIsRepaired)
                    {
                        foreach (TileEntity te in TileEntity.ByID.Values)
                        {
                            if (te is TESolynTelescope telescope)
                                telescope.IsRepaired = true;
                        }
                    }

                    if (Main.netMode == NetmodeID.Server)
                    {
                        NetMessage.SendData(MessageID.WorldData);
                        NetMessage.SendTileSquare(-1, point.X, point.Y, 100);

                        foreach (TileEntity te in TileEntity.ByID.Values)
                        {
                            if (te is TESolynTelescope || te is TESolynFlag || te is TESolynTent)
                            {
                                NetMessage.SendData(MessageID.TileEntitySharing, -1, -1, null, te.ID);
                            }
                        }
                    }
                }
            }
            finally
            {
                generating = false;
            }
        }).Start(point);
    }

    private static void DisableSpawnsNearCampsite(Player player, ref int spawnRate, ref int maxSpawns)
    {
        if (CampSitePosition != Vector2.Zero && player.WithinRange(CampSitePosition, 884f))
        {
            spawnRate = int.MaxValue;
            maxSpawns = 0;
        }
    }

    public override void OnWorldLoad()
    {
        CampSitePosition = Vector2.Zero;
        TelescopePosition = Point.Zero;
        TentPosition = Vector2.Zero;
        FlagPosition = Point.Zero;
    }

    public override void OnWorldUnload()
    {
        CampSitePosition = Vector2.Zero;
        TelescopePosition = Point.Zero;
        TentPosition = Vector2.Zero;
        FlagPosition = Point.Zero;
    }

    public override void SaveWorldData(TagCompound tag)
    {
        tag["CampSitePositionX"] = CampSitePosition.X;
        tag["CampSitePositionY"] = CampSitePosition.Y;
        tag["TelescopePositionX"] = TelescopePosition.X;
        tag["TelescopePositionY"] = TelescopePosition.Y;
        tag["TentPositionX"] = TentPosition.X;
        tag["TentPositionY"] = TentPosition.Y;
        tag["FlagPositionX"] = FlagPosition.X;
        tag["FlagPositionY"] = FlagPosition.Y;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        Vector2 campPosition = Vector2.Zero;
        if (tag.TryGet("CampSitePositionX", out float campX))
            campPosition.X = campX;
        if (tag.TryGet("CampSitePositionY", out float campY))
            campPosition.Y = campY;
        CampSitePosition = campPosition;

        Point telescopePosition = Point.Zero;
        if (tag.TryGet("TelescopePositionX", out int telescopeX))
            telescopePosition.X = telescopeX;
        if (tag.TryGet("TelescopePositionY", out int telescopeY))
            telescopePosition.Y = telescopeY;
        TelescopePosition = telescopePosition;

        Vector2 tentPosition = Vector2.Zero;
        if (tag.TryGet("TentPositionX", out float tentX))
            tentPosition.X = tentX;
        if (tag.TryGet("TentPositionY", out float tentY))
            tentPosition.Y = tentY;
        TentPosition = tentPosition;

        Point flagPosition = Point.Zero;
        if (tag.TryGet("FlagPositionX", out int flagX))
            flagPosition.X = flagX;
        if (tag.TryGet("FlagPositionY", out int flagY))
            flagPosition.Y = flagY;
        FlagPosition = flagPosition;
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.WriteVector2(CampSitePosition);
        writer.WriteVector2(TelescopePosition.ToVector2());
        writer.WriteVector2(TentPosition);
        writer.WriteVector2(FlagPosition.ToVector2());
    }

    public override void NetReceive(BinaryReader reader)
    {
        CampSitePosition = reader.ReadVector2();
        TelescopePosition = reader.ReadVector2().ToPoint();
        TentPosition = reader.ReadVector2();
        FlagPosition = reader.ReadVector2().ToPoint();
    }

    private bool DisallowMeteorsDestroyingTheCampsite(On_WorldGen.orig_meteor orig, int i, int j, bool ignorePlayers)
    {
        if (CampSitePosition.X != 0f && Distance(i, CampSitePosition.X / 16f) <= MinMeteorDistance)
            return false;

        return orig(i, j, ignorePlayers);
    }
}
