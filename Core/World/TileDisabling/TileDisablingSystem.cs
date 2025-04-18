using System.Reflection;
using CalamityMod;
using CalamityMod.CalPlayer;
using CalamityMod.Skies;
using CalamityMod.Tiles.Astral;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Core.CrossCompatibility.Inbound;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.GlobalInstances;
using NoxusBoss.Core.Graphics.UI;
using NoxusBoss.Core.World.GameScenes.AvatarUniverseExploration;
using NoxusBoss.Core.World.GameScenes.TerminusStairway;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Light;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Map;
using Terraria.ModLoader;

namespace NoxusBoss.Core.World.TileDisabling;

public class TileDisablingSystem : ModSystem
{
    private static bool tilesAreUninteractable;

    /// <summary>
    /// Whether tiles are currently disabled across the world or not.
    /// </summary>
    public static bool TilesAreUninteractable
    {
        get => tilesAreUninteractable;
        set
        {
            if (value && tilesAreUninteractable != value && Main.netMode == NetmodeID.SinglePlayer)
            {
                foreach (Item item in Main.ActiveItems)
                    item.active = false;
            }

            tilesAreUninteractable = value;
        }
    }

    public delegate bool orig_TileFrame(int i, int j, int tileID, ref bool resetFrame, ref bool noBreak);

    public delegate bool hook_TileFrame(orig_TileFrame orig, int i, int j, int tileID, ref bool resetFrame, ref bool noBreak);

    public override void OnModLoad()
    {
        // All ye innocent ones, turn back now.
        // All gods across all time lay their greatest curse upon this forsaken code.
        On_Collision.WetCollision += DisableLiquidCollision;
        On_Collision.WaterCollision += DisableLiquidCollision2;
        On_Collision.DrownCollision += DisableLiquidCollision3;
        On_Collision.LavaCollision += DisableLavaCollision;
        On_Collision.SwitchTiles += DisableSwitchChecks;
        On_Collision.SwitchTilesNew += DisableSwitchChecks2;
        On_Collision.SolidCollision_Vector2_int_int += ImLosingIt1;
        On_Collision.SolidCollision_Vector2_int_int_bool += ImLosingIt2;
        On_Collision.StickyTiles += DisableCobwebInteractions;
        On_Gore.NewGore_IEntitySource_Vector2_Vector2_int_float += DisableIdleParticlesFromTiles;
        On_LightingEngine.GetColor += TemporarilyDisableBlackDrawingForLight;
        IL_Main.DrawMap += ObfuscateMap;
        On_Player.ApplyTouchDamage += DisableSuffocation;
        On_Player.FloorVisuals += DisableFloorVisuals;
        On_Main.DrawInterface_40_InteractItemIcon += DisableHoverItems;
        On_TileDrawing.Draw += MakeWallsInvisible_FuckYouMysteriousIndexError;
        On_Main.DoDraw_Tiles_Solid += MakeTilesInvisible_SolidLayer;
        On_Main.DoDraw_Tiles_NonSolid += MakeTilesInvisible_NonSolidLayer;
        On_Main.DoDraw_WallsAndBlacks += MakeWallsInvisible;
        On_Main.DrawLiquid += MakeLiquidsInvisible;
        On_Main.DrawBackground += DisableBackground;
        On_Main.DrawUnderworldBackground += DisableHellBackground;
        On_WaterfallManager.Draw += MakeWaterfallsInvisible;
        On_Collision.TileCollision += DisableTileCollision;
        On_Collision.SlopeCollision += DisableSlopeCollision;
        On_Collision.StepDown += DisableSlopeCollision2;
        On_Collision.StepUp += DisableSlopeCollision3;
        On_Main.DrawBackgroundBlackFill += DrawSkiesUnderground;
        On_Main.DrawWires += DisableWireDrawing;
        On_Player.DryCollision += DisableAllPlayerSolidCollisionChecks;
        On_WorldGen.UpdateWorld += FixObscureStackOverflowCrashes;
        On_WorldGen.KillTile_DropItems += DisableItemDrops;
        On_DoorOpeningHelper.LookForDoorsToOpen += DisableAutomaticDoorOpening;
        On_Minecart.GetOnTrack += DisableMinecarts;
        On_WorldGen.CheckPot += FuckYOUPots;
        On_Projectile.CutTilesAt += DisableTileCutBehaviors;
        On_Main.DrawMouseOver += DisableSignReading;
        On_Main.UpdateAudio += DisableWindAmbience;
        On_SceneMetrics.ScanAndExportToMain += DisableThingsIGuess;
        On_Player.PlaceThing += DisableTilePlacement;
        On_Player.ItemCheck_UseMiningTools += DisablePlayerTileDestruction;
        On_Player.CanSeeShimmerEffects += DisableShimmerVisuals;
        On_Framing.GetTileSafely_int_int += TheGoofiestDetourOfAllTime;
        On_SmartCursorHelper.SmartCursorLookup += DisableSmartCursor;
        On_MapHelper.CreateMapTile += TheHorrorsOfTheDreadedAstralChestBugsStrikeAgainIHerebyRescindMyConsentToAllowThemToArise;
        On_Framing.SelfFrame8Way += DisableTileFraming;
        On_Main.ShouldNormalEventsBeAbleToStart += StopGeneralEvents;
        On_Main.UpdateTime_StartDay += DisableDayEvents;
        On_Main.UpdateTime_StartNight += DisableNightEvents;
        On_WorldGen.Reframe += DisableSectionReframingBugs;
        On_LightMap.BlurPass += DisableLightingEffects;
        On_Lighting.Brightness += FixRetroLightingBugs;
        On_TileLightScanner.ExportTo += DisableLightingEngineA;
        On_LegacyLighting.GetColor += UseFullbright;
        On_Player.CanMoveForwardOnRope += DisableRopeUsage;
        On_Main.IsTileSpelunkable_int_int_ushort_short_short += DisableSpelunkerSecondaryEffects;
        On_WorldGen.SolidOrSlopedTile_Tile += FixCalamityDashBugs;
        On_SpawnMapLayer.Draw += DisableSpawnMapIcon;
        On_TeleportPylonsMapLayer.Draw += DisablePylonMapIcons;
        On_Rain.MakeRain += DisableRain;
        On_Collision.GetEntityEdgeTiles += DisableAuricOreRejection;

        MonoModHooks.Add(typeof(TileLoader).GetMethod("TileFrame", UniversalBindingFlags), new hook_TileFrame(DisableAllTileFraming));

        GlobalNPCEventHandlers.PreAIEvent += DeleteCultists;
        GlobalNPCEventHandlers.PreAIEvent += DisableTownNPCAI;
        GlobalNPCEventHandlers.PreDrawEvent += DisableTownNPCDrawing;
        GlobalNPCEventHandlers.EditSpawnRateEvent += DisableNPCSpawns;

        PlayerDataManager.UpdateBadLifeRegenEvent += DisableAbyssBreathMechanic;

        CellPhoneInfoModificationSystem.MoonPhaseReplacementTextEvent += (originalText) =>
        {
            if (TilesAreUninteractable && !TerminusStairwaySystem.Enabled)
                return Language.GetTextValue("Mods.NoxusBoss.CellPhoneInfoOverrides.MoonNotFoundText");

            return null;
        };
        CellPhoneInfoModificationSystem.TreasureTilesReplacementTextEvent += (originalText) =>
        {
            if (AvatarOfEmptiness.Myself is not null && TilesAreUninteractable)
                return Language.GetTextValue("GameUI.NoTreasureNearby");

            return null;
        };
    }

    private bool DisableSwitchChecks2(On_Collision.orig_SwitchTilesNew orig, Collision self, Vector2 Position, int Width, int Height, Vector2 oldPosition, int objType)
    {
        if (TilesAreUninteractable)
            return false;

        return orig(self, Position, Width, Height, oldPosition, objType);
    }

    private bool DisableSwitchChecks(On_Collision.orig_SwitchTiles orig, Vector2 Position, int Width, int Height, Vector2 oldPosition, int objType)
    {
        if (TilesAreUninteractable)
            return false;

        return orig(Position, Width, Height, oldPosition, objType);
    }

    private bool ImLosingIt1(On_Collision.orig_SolidCollision_Vector2_int_int orig, Vector2 Position, int Width, int Height)
    {
        if (TilesAreUninteractable)
            return false;

        return orig(Position, Width, Height);
    }

    private bool ImLosingIt2(On_Collision.orig_SolidCollision_Vector2_int_int_bool orig, Vector2 Position, int Width, int Height, bool acceptTopSurfaces)
    {
        if (TilesAreUninteractable)
            return false;

        return orig(Position, Width, Height, acceptTopSurfaces);
    }

    private bool DeleteCultists(NPC npc)
    {
        bool isCultistEntity = npc.type == NPCID.CultistArcherBlue || npc.type == NPCID.CultistDevote || npc.type == NPCID.CultistTablet;
        if (TilesAreUninteractable && isCultistEntity)
            npc.active = false;

        return true;
    }

    private bool DisableTownNPCAI(NPC npc)
    {
        if (npc.type == ModContent.NPCType<BattleSolyn>())
            return true;

        if (npc.aiStyle != 7 || NPCID.Sets.ActsLikeTownNPC[npc.type])
            return true;

        npc.dontTakeDamage = TilesAreUninteractable;
        npc.hide = TilesAreUninteractable;
        npc.ShowNameOnHover = !TilesAreUninteractable;
        if (TilesAreUninteractable)
        {
            npc.Bottom = new(npc.homeTileX * 16f, npc.homeTileY * 16f);
            npc.velocity = Vector2.Zero;
            return false;
        }

        return true;
    }

    private bool DisableTownNPCDrawing(NPC npc) => !TilesAreUninteractable || npc.aiStyle != 7 || NPCID.Sets.ActsLikeTownNPC[npc.type];

    private void DisableNPCSpawns(Player player, ref int spawnRate, ref int maxSpawns)
    {
        if (TilesAreUninteractable)
        {
            spawnRate = int.MaxValue;
            maxSpawns = 0;
        }
    }

    private void DisableAbyssBreathMechanic(PlayerDataManager p)
    {
        if (!TilesAreUninteractable || !CalamityCompatibility.Enabled)
            return;

        DisableAbyssBreathMechanicWrapper(p);
    }

    [JITWhenModsEnabled(CalamityCompatibility.ModName)]
    private static void DisableAbyssBreathMechanicWrapper(PlayerDataManager p)
    {
        CalamityPlayer calPlayer = p.Player.Calamity();
        if (calPlayer.ZoneAbyss)
        {
            p.Player.breath = p.Player.breathMax;
            if (!p.Player.IsUnderwater())
            {
                float calamityDebuffMultiplier = CommonCalamityVariables.DeathModeActive ? 1.25f : 1f;
                if (p.Player.statLife > 100)
                    p.Player.lifeRegen += (int)(160D * calamityDebuffMultiplier);
            }
        }
    }

    private bool DisableLiquidCollision(On_Collision.orig_WetCollision orig, Vector2 Position, int Width, int Height)
    {
        if (TilesAreUninteractable)
        {
            Collision.honey = false;
            Collision.shimmer = false;
        }

        return !TilesAreUninteractable && orig(Position, Width, Height);
    }

    private Vector2 DisableLiquidCollision2(On_Collision.orig_WaterCollision orig, Vector2 Position, Vector2 Velocity, int Width, int Height, bool fallThrough, bool fall2, bool lavaWalk)
    {
        if (TilesAreUninteractable)
            return Velocity;

        return orig(Position, Velocity, Width, Height, fallThrough, fall2, lavaWalk);
    }

    private bool DisableLiquidCollision3(On_Collision.orig_DrownCollision orig, Vector2 Position, int Width, int Height, float gravDir, bool includeSlopes)
    {
        return !TilesAreUninteractable && orig(Position, Width, Height, gravDir, includeSlopes);
    }

    private bool DisableLavaCollision(On_Collision.orig_LavaCollision orig, Vector2 Position, int Width, int Height)
    {
        return !TilesAreUninteractable && orig(Position, Width, Height);
    }

    private Vector2 DisableCobwebInteractions(On_Collision.orig_StickyTiles orig, Vector2 Position, Vector2 Velocity, int Width, int Height)
    {
        if (TilesAreUninteractable)
            return -Vector2.One;

        return orig(Position, Velocity, Width, Height);
    }

    private void DisableSuffocation(On_Player.orig_ApplyTouchDamage orig, Player self, int tileId, int x, int y)
    {
        if (!TilesAreUninteractable)
            orig(self, tileId, x, y);
    }

    private void DisableFloorVisuals(On_Player.orig_FloorVisuals orig, Player self, bool Falling)
    {
        if (!TilesAreUninteractable)
            orig(self, Falling);
    }

    private void DisableHoverItems(On_Main.orig_DrawInterface_40_InteractItemIcon orig, Main self)
    {
        if (!TilesAreUninteractable)
            orig(self);
    }

    private int DisableIdleParticlesFromTiles(On_Gore.orig_NewGore_IEntitySource_Vector2_Vector2_int_float orig, IEntitySource source, Vector2 Position, Vector2 Velocity, int Type, float Scale)
    {
        if (TilesAreUninteractable)
            return Main.maxGore - 1;

        return orig(source, Position, Velocity, Type, Scale);
    }

    private Vector3 TemporarilyDisableBlackDrawingForLight(On_LightingEngine.orig_GetColor orig, LightingEngine self, int x, int y)
    {
        if (TilesAreUninteractable)
            return Vector3.One;

        return orig(self, x, y);
    }

    // This code is copypasted from an Infernum IL edit I made a while ago. It will work with Infernum's edit, as it only applies an opacity multiplication to colors.
    internal void ObfuscateMap(ILContext il)
    {
        ILCursor cursor = new ILCursor(il);
        MethodInfo? colorFloatMultiply = typeof(Color).GetMethod("op_Multiply", new Type[] { typeof(Color), typeof(float) });
        ConstructorInfo? colorConstructor = typeof(Color).GetConstructor(new Type[] { typeof(int), typeof(int), typeof(int), typeof(int) });

        if (colorFloatMultiply is null || colorConstructor is null)
            return;

        // ==== APPLY EFFECT TO FULLSCREEN MAP =====

        // Find the map background draw method and use it as a hooking reference.
        if (!cursor.TryGotoNext(i => i.MatchCall<Main>("DrawMapFullscreenBackground")))
            return;

        // Go to the next 3 instances of Color.White being loaded and multiply them by the opacity factor.
        for (int i = 0; i < 3; i++)
        {
            if (!cursor.TryGotoNext(MoveType.After, i => i.MatchCall<Color>("get_White")))
                continue;

            cursor.EmitDelegate(() => 1f - TilesAreUninteractable.ToInt());
            cursor.Emit(OpCodes.Call, colorFloatMultiply);
        }

        // ==== APPLY EFFECT TO MAP RENDER TARGETS =====

        // Move after the map target color is decided, and multiply the result by the opacity factor/add blackness to it.
        if (!cursor.TryGotoNext(i => i.MatchLdfld<Main>("mapTarget")))
            return;
        if (!cursor.TryGotoNext(MoveType.After, i => i.MatchNewobj(colorConstructor)))
            return;

        cursor.EmitDelegate((Color c) =>
        {
            if (Main.mapFullscreen)
                return c * (1f - TilesAreUninteractable.ToInt());

            return Color.Lerp(c, Color.Black, TilesAreUninteractable.ToInt());
        });
    }

    public override void PostUpdatePlayers()
    {
        // Go through tiles.
        if (TilesAreUninteractable && (Collision.SolidCollision(Main.LocalPlayer.TopLeft, Main.LocalPlayer.width, Main.LocalPlayer.height) || Collision.WetCollision(Main.LocalPlayer.TopLeft, Main.LocalPlayer.width, Main.LocalPlayer.height)) && Main.LocalPlayer.velocity.Y > 0f)
            Main.LocalPlayer.position.Y += 4f;

        bool bottomHalfOfPlayerIsStuck = !Collision.SolidCollision(Main.LocalPlayer.TopLeft, Main.LocalPlayer.width, 1) && Collision.SolidCollision(Main.LocalPlayer.BottomLeft - Vector2.UnitY, Main.LocalPlayer.width, 1);
        if (TilesAreUninteractable && bottomHalfOfPlayerIsStuck)
            Main.LocalPlayer.position.Y += Main.LocalPlayer.velocity.Y.NonZeroSign() * 10f;
    }

    private void MakeWallsInvisible_FuckYouMysteriousIndexError(On_TileDrawing.orig_Draw orig, TileDrawing self, bool solidLayer, bool forRenderTargets, bool intoRenderTargets, int waterStyleOverride)
    {
        if (!TilesAreUninteractable)
            orig(self, solidLayer, forRenderTargets, intoRenderTargets, waterStyleOverride);
    }

    private void MakeTilesInvisible_SolidLayer(On_Main.orig_DoDraw_Tiles_Solid orig, Main self)
    {
        if (!TilesAreUninteractable)
            orig(self);
    }

    private void MakeTilesInvisible_NonSolidLayer(On_Main.orig_DoDraw_Tiles_NonSolid orig, Main self)
    {
        if (!TilesAreUninteractable)
            orig(self);
    }

    private void MakeWallsInvisible(On_Main.orig_DoDraw_WallsAndBlacks orig, Main self)
    {
        if (!TilesAreUninteractable)
            orig(self);
    }

    private void MakeLiquidsInvisible(On_Main.orig_DrawLiquid orig, Main self, bool bg, int waterStyle, float alpha, bool drawSinglePassLiquids)
    {
        if (!TilesAreUninteractable)
            orig(self, bg, waterStyle, alpha, drawSinglePassLiquids);
    }

    private void DisableBackground(On_Main.orig_DrawBackground orig, Main self)
    {
        if (!TilesAreUninteractable)
            orig(self);
    }

    private void DisableHellBackground(On_Main.orig_DrawUnderworldBackground orig, Main self, bool flat)
    {
        if (!TilesAreUninteractable)
            orig(self, flat);
    }

    private void MakeWaterfallsInvisible(On_WaterfallManager.orig_Draw orig, WaterfallManager self, SpriteBatch spriteBatch)
    {
        if (!TilesAreUninteractable)
            orig(self, spriteBatch);
    }

    private Vector2 DisableTileCollision(On_Collision.orig_TileCollision orig, Vector2 Position, Vector2 Velocity, int Width, int Height, bool fallThrough, bool fall2, int gravDir)
    {
        if (!TilesAreUninteractable)
            return orig(Position, Velocity, Width, Height, fallThrough, fall2, gravDir);

        return Velocity;
    }

    private Vector4 DisableSlopeCollision(On_Collision.orig_SlopeCollision orig, Vector2 Position, Vector2 Velocity, int Width, int Height, float gravity, bool fall)
    {
        if (!TilesAreUninteractable)
            return orig(Position, Velocity, Width, Height, gravity, fall);

        return new(Position.X, Position.Y, Velocity.X, Velocity.Y);
    }

    private void DisableSlopeCollision2(On_Collision.orig_StepDown orig, ref Vector2 position, ref Vector2 velocity, int width, int height, ref float stepSpeed, ref float gfxOffY, int gravDir, bool waterWalk)
    {
        if (!TilesAreUninteractable)
            orig(ref position, ref velocity, width, height, ref stepSpeed, ref gfxOffY, gravDir, waterWalk);
        else
            gfxOffY = 0f;
    }

    private void DisableSlopeCollision3(On_Collision.orig_StepUp orig, ref Vector2 position, ref Vector2 velocity, int width, int height, ref float stepSpeed, ref float gfxOffY, int gravDir, bool holdsMatching, int specialChecksMode)
    {
        if (!TilesAreUninteractable)
            orig(ref position, ref velocity, width, height, ref stepSpeed, ref gfxOffY, gravDir, holdsMatching, specialChecksMode);
    }

    private void DrawSkiesUnderground(On_Main.orig_DrawBackgroundBlackFill orig, Main self)
    {
        if (!TilesAreUninteractable)
            orig(self);
    }

    private void DisableWireDrawing(On_Main.orig_DrawWires orig, Main self)
    {
        if (!TilesAreUninteractable)
            orig(self);
    }

    private void FixObscureStackOverflowCrashes(On_WorldGen.orig_UpdateWorld orig)
    {
        if (!TilesAreUninteractable)
            orig();
    }

    private void DisableItemDrops(On_WorldGen.orig_KillTile_DropItems orig, int x, int y, Tile tileCache, bool includeLargeObjectDrops, bool includeAllModdedLargeObjectDrops)
    {
        if (!TilesAreUninteractable)
            orig(x, y, tileCache, includeAllModdedLargeObjectDrops, includeAllModdedLargeObjectDrops);
    }

    private void DisableAutomaticDoorOpening(On_DoorOpeningHelper.orig_LookForDoorsToOpen orig, DoorOpeningHelper self, Player player)
    {
        if (!TilesAreUninteractable)
            orig(self, player);
    }

    private bool DisableMinecarts(On_Minecart.orig_GetOnTrack orig, int tileX, int tileY, ref Vector2 Position, int Width, int Height)
    {
        if (TilesAreUninteractable)
            return false;

        return orig(tileX, tileY, ref Position, Width, Height);
    }

    private void FuckYOUPots(On_WorldGen.orig_CheckPot orig, int i, int j, int type)
    {
        if (!TilesAreUninteractable)
            orig(i, j, type);
    }

    private void DisableTileCutBehaviors(On_Projectile.orig_CutTilesAt orig, Projectile self, Vector2 boxPosition, int boxWidth, int boxHeight)
    {
        if (!TilesAreUninteractable)
            orig(self, boxPosition, boxWidth, boxHeight);
    }

    private void DisableSignReading(On_Main.orig_DrawMouseOver orig, Main self)
    {
        if (TilesAreUninteractable)
            Main.signHover = -1;

        orig(self);
    }

    private void DisableWindAmbience(On_Main.orig_UpdateAudio orig, Main self)
    {
        float oldAmbientVolume = Main.ambientVolume;
        if (TilesAreUninteractable)
            Main.ambientVolume = 0f;

        orig(self);

        if (TilesAreUninteractable)
            Main.ambientVolume = oldAmbientVolume;
    }

    private void DisableThingsIGuess(On_SceneMetrics.orig_ScanAndExportToMain orig, SceneMetrics self, SceneMetricsScanSettings settings)
    {
        if (!TilesAreUninteractable)
            orig(self, settings);
        else
            self.Reset();
    }

    private void DisableAllPlayerSolidCollisionChecks(On_Player.orig_DryCollision orig, Player self, bool fallThrough, bool ignorePlats)
    {
        if (TilesAreUninteractable && !TerminusStairwaySystem.Enabled)
        {
            self.position += self.velocity;
            return;
        }

        orig(self, fallThrough, ignorePlats);
    }

    private void DisableTilePlacement(On_Player.orig_PlaceThing orig, Player self, ref Player.ItemCheckContext context)
    {
        if (!TilesAreUninteractable)
            orig(self, ref context);
    }

    private void DisablePlayerTileDestruction(On_Player.orig_ItemCheck_UseMiningTools orig, Player self, Item sItem)
    {
        if (!TilesAreUninteractable)
            orig(self, sItem);
    }

    private bool DisableShimmerVisuals(On_Player.orig_CanSeeShimmerEffects orig, Player self)
    {
        return !TilesAreUninteractable && orig(self);
    }

    private Tile TheGoofiestDetourOfAllTime(On_Framing.orig_GetTileSafely_int_int orig, int i, int j)
    {
        if (TilesAreUninteractable)
            return default;

        return orig(i, j);
    }

    private void DisableSmartCursor(On_SmartCursorHelper.orig_SmartCursorLookup orig, Player player)
    {
        orig(player);
        if (TilesAreUninteractable)
            Main.SmartCursorShowing = false;
    }

    private MapTile TheHorrorsOfTheDreadedAstralChestBugsStrikeAgainIHerebyRescindMyConsentToAllowThemToArise(On_MapHelper.orig_CreateMapTile orig, int i, int j, byte Light)
    {
        if (TilesAreUninteractable && CalamityCompatibility.Enabled)
        {
            if (IsAstralChest(i, j))
                return default;
        }

        return orig(i, j, Light);
    }

    [JITWhenModsEnabled(CalamityCompatibility.ModName)]
    private static bool IsAstralChest(int x, int y)
    {
        if (x < 0 || x >= Main.maxTilesX)
            return false;
        if (y < 0 || y >= Main.maxTilesY)
            return false;

        return Main.tile[x, y].TileType == ModContent.TileType<AstralChestLocked>();
    }

    private static bool DisableAllTileFraming(orig_TileFrame orig, int i, int j, int tileID, ref bool resetFrame, ref bool noBreak)
    {
        if (TilesAreUninteractable)
            return false;

        return orig(i, j, tileID, ref resetFrame, ref noBreak);
    }

    private void DisableTileFraming(On_Framing.orig_SelfFrame8Way orig, int i, int j, Tile centerTile, bool resetFrame)
    {
        if (TilesAreUninteractable)
            return;

        orig(i, j, centerTile, resetFrame);
    }

    private bool StopGeneralEvents(On_Main.orig_ShouldNormalEventsBeAbleToStart orig) => TilesAreUninteractable || orig();

    private void DisableDayEvents(On_Main.orig_UpdateTime_StartDay orig, ref bool stopEvents)
    {
        if (TilesAreUninteractable)
            stopEvents = true;

        orig(ref stopEvents);
    }

    private void DisableNightEvents(On_Main.orig_UpdateTime_StartNight orig, ref bool stopEvents)
    {
        if (TilesAreUninteractable)
            stopEvents = true;

        orig(ref stopEvents);
    }

    private void DisableSectionReframingBugs(On_WorldGen.orig_Reframe orig, int x, int y, bool resetFrame)
    {
        if (TilesAreUninteractable)
            return;

        orig(x, y, resetFrame);
    }

    private void DisableLightingEffects(On_LightMap.orig_BlurPass orig, LightMap self)
    {
        if (TilesAreUninteractable)
        {
            self.LightDecayThroughAir = 0.91f;
            self.LightDecayThroughSolid = 0.56f;
            self.LightDecayThroughWater = new Vector3(0.88f, 0.96f, 1.015f) * 0.91f;
            self.LightDecayThroughHoney = new Vector3(0.75f, 0.7f, 0.6f) * 0.91f;
            return;
        }

        orig(self);
    }

    private float FixRetroLightingBugs(On_Lighting.orig_Brightness orig, int x, int y)
    {
        if (TilesAreUninteractable)
            return 1f;

        return orig(x, y);
    }

    private void DisableLightingEngineA(On_TileLightScanner.orig_ExportTo orig, TileLightScanner self, Rectangle area, LightMap outputMap, TileLightScannerOptions options)
    {
        if (!TilesAreUninteractable)
            orig(self, area, outputMap, options);
    }

    private Vector3 UseFullbright(On_LegacyLighting.orig_GetColor orig, LegacyLighting self, int x, int y)
    {
        if (TilesAreUninteractable)
            return Vector3.One;

        return orig(self, x, y);
    }

    private bool DisableRopeUsage(On_Player.orig_CanMoveForwardOnRope orig, Player self, int dir, int x, int y)
    {
        if (TilesAreUninteractable)
            return false;

        return orig(self, dir, x, y);
    }

    private bool DisableSpelunkerSecondaryEffects(On_Main.orig_IsTileSpelunkable_int_int_ushort_short_short orig, int tileX, int tileY, ushort typeCache, short tileFrameX, short tileFrameY)
    {
        if (TilesAreUninteractable)
            return false;

        return orig(tileX, tileY, typeCache, tileFrameX, tileFrameY);
    }

    private bool FixCalamityDashBugs(On_WorldGen.orig_SolidOrSlopedTile_Tile orig, Tile tile)
    {
        if (TilesAreUninteractable)
            return false;

        return orig(tile);
    }

    private void DisablePylonMapIcons(On_TeleportPylonsMapLayer.orig_Draw orig, TeleportPylonsMapLayer self, ref MapOverlayDrawContext context, ref string text)
    {
        if (!TilesAreUninteractable)
            orig(self, ref context, ref text);
    }

    private void DisableSpawnMapIcon(On_SpawnMapLayer.orig_Draw orig, SpawnMapLayer self, ref MapOverlayDrawContext context, ref string text)
    {
        if (!TilesAreUninteractable)
            orig(self, ref context, ref text);
    }

    private void DisableRain(On_Rain.orig_MakeRain orig)
    {
        if (!TilesAreUninteractable)
            orig();
    }

    private void DisableAuricOreRejection(On_Collision.orig_GetEntityEdgeTiles orig, List<Point> p, Entity entity, bool left, bool right, bool up, bool down)
    {
        if (!TilesAreUninteractable)
            orig(p, entity, left, right, up, down);
    }

    public override void OnWorldLoad() => TilesAreUninteractable = false;

    public override void OnWorldUnload() => TilesAreUninteractable = false;

    public override void PreUpdateProjectiles()
    {
        if (!TilesAreUninteractable)
            return;

        foreach (Projectile projectile in Main.ActiveProjectiles)
        {
            if (projectile.aiStyle == 7)
                projectile.active = false;
        }

        Main.LocalPlayer.gfxOffY = 0f;
    }

    public override void PostUpdateDusts()
    {
        if (TilesAreUninteractable)
            Dust.lavaBubbles = 1000;

        ClearBrimstoneCragCindersWrapper();
    }

    private static void ClearBrimstoneCragCindersWrapper()
    {
        if (TilesAreUninteractable && CalamityCompatibility.Enabled)
            ClearBrimstoneCragCineders();
    }

    [JITWhenModsEnabled(CalamityCompatibility.ModName)]
    private static void ClearBrimstoneCragCineders()
    {
        if (SkyManager.Instance["CalamityMod:BrimstoneCrag"] is not BrimstoneCragSky sky)
            return;

        sky.Cinders.Clear();
    }

    public override void PostUpdateEverything()
    {
        bool namelessSendingPlayersBackToOverworld = AvatarOfEmptiness.Myself is not null && AvatarOfEmptiness.Myself.As<AvatarOfEmptiness>().CurrentState == AvatarOfEmptiness.AvatarAIType.ParadiseReclaimed_NamelessReturnsEveryoneToOverworld;
        if (AvatarOfEmptinessSky.Dimension is null && !namelessSendingPlayersBackToOverworld && !AvatarUniverseExplorationSystem.InAvatarUniverse && !TerminusStairwaySystem.Enabled && !Main.LocalPlayer.dead)
        {
            if (TilesAreUninteractable)
            {
                TilesAreUninteractable = false;

                if (Main.netMode == NetmodeID.SinglePlayer)
                {
                    new Thread(WorldGen.EveryTileFrame).Start();
                }
            }
        }
    }
}
