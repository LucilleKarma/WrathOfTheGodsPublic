using System.Runtime.CompilerServices;

using Luminance.Core.Graphics;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using NoxusBoss.Core.Graphics.GenesisEffects;
using NoxusBoss.Core.Graphics.RenderTargets;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using NoxusBoss.Core.Netcode;
using NoxusBoss.Core.Netcode.Packets;
using NoxusBoss.Core.World.TileDisabling;

using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace NoxusBoss.Content.Tiles.GenesisComponents.Seedling;

public class GrowingGenesisRenderSystem : ModSystem
{
    private static float magicOverlayTime;

    /// <summary>
    /// The render target that contains all Genesis information, for the purposes of overlay visuals.
    /// </summary>
    public static InstancedRequestableTarget GenesisTarget
    {
        get;
        private set;
    }

    /// <summary>
    /// The set of all Genesis points in the world.
    /// </summary>
    internal static readonly List<GenesisInstance> genesisPoints = [];

    /// <summary>
    /// The amount of growth stages needed in order for a Genesis to be usable to summon the Avatar.
    /// </summary>
    public static int GrowthStageNeededToUse => 3;

    public override void OnModLoad()
    {
        if (Main.netMode != NetmodeID.Server)
        {
            On_Main.DoDraw_Tiles_Solid += RenderGenesisInstancesWrapper;

            GenesisTarget = new InstancedRequestableTarget();
            Main.ContentThatNeedsRenderTargets.Add(GenesisTarget);
        }
    }

    public override void PreUpdateEntities()
    {
        if (TileDisablingSystem.TilesAreUninteractable)
            return;

        int genesisID = ModContent.TileType<GenesisTile>();
        for (int i = 0; i < genesisPoints.Count; i++)
        {
            Tile tile = Framing.GetTileSafely(genesisPoints[i].Anchor);
            if (!tile.HasTile || tile.TileType != genesisID)
            {
                genesisPoints.RemoveAt(i);
                i--;
                continue;
            }
        }

        foreach (GenesisInstance genesis in genesisPoints)
            genesis.Update();
    }

    public override void OnWorldLoad() => genesisPoints.Clear();

    public override void OnWorldUnload() => genesisPoints.Clear();

    private static void RenderGenesisInstancesWrapper(On_Main.orig_DoDraw_Tiles_Solid orig, Main self)
    {
        if (genesisPoints.Count >= 1 && !TileDisablingSystem.TilesAreUninteractable)
        {
            RenderGenesisInstances();
            UpdateGenesisScreenShader();
        }

        orig(self);
    }

    private static void UpdateGenesisScreenShader()
    {
        float[] genesisIntensities = new float[15];
        Vector2[] genesisPositions = new Vector2[15];

        int i = 0;
        float distanceToClosestPoint = float.MaxValue;
        foreach (Point point in genesisPoints.Select(g => g.Anchor))
        {
            if (i < genesisPoints.Count)
            {
                GenesisInstance? genesis = TryGet(point);
                genesisPositions[i] = point.ToWorldCoordinates();

                if (genesis is not null)
                    genesisIntensities[i] = genesis.GrowthStage / GrowthStageNeededToUse;
            }
            distanceToClosestPoint = MathF.Min(distanceToClosestPoint, Main.LocalPlayer.Distance(point.ToWorldCoordinates()));

            i++;
        }

        if (distanceToClosestPoint >= Main.screenWidth * 1.5f)
            return;
        if (Main.LocalPlayer.gravity == -1f)
            return;

        float timeIncrement = 0.01667f;
        float electricityHeight = 12f;
        float electricityCoverge = 1f;
        if (GenesisVisualsSystem.EffectActive)
        {
            timeIncrement *= 5f;
            electricityHeight *= 2.7f;
            electricityCoverge *= 2f;
        }

        magicOverlayTime += timeIncrement;

        ManagedScreenFilter overlayShader = ShaderManager.GetFilter("NoxusBoss.GenesisOverlayShader");
        overlayShader.TrySetParameter("zoom", Main.GameViewMatrix.Zoom);
        overlayShader.TrySetParameter("screenOffset", (Main.screenPosition - Main.screenLastPosition) / Main.ScreenSize.ToVector2());
        overlayShader.TrySetParameter("lastScreenPosition", Main.screenLastPosition);
        overlayShader.TrySetParameter("genesisPositions", genesisPositions);
        overlayShader.TrySetParameter("genesisIntensities", genesisIntensities);
        overlayShader.TrySetParameter("glowRadius", GenesisGrass.ConversionRadius * 16f);
        overlayShader.TrySetParameter("maxElectricityHeight", electricityHeight);
        overlayShader.TrySetParameter("maxDistortionOffset", 6f);
        overlayShader.TrySetParameter("time", magicOverlayTime);
        overlayShader.TrySetParameter("electricityCoverage", electricityCoverge);
        overlayShader.TrySetParameter("magicColor", new Color(12, 105, 255).ToVector4());
        overlayShader.SetTexture(TileTargetManagers.TileTarget, 1, SamplerState.LinearClamp);
        overlayShader.SetTexture(PerlinNoise, 2, SamplerState.LinearWrap);
        overlayShader.SetTexture(GenesisGrassTargetManager.GrassTarget, 3, SamplerState.AnisotropicWrap);
        overlayShader.SetTexture(WatercolorNoiseA, 4, SamplerState.LinearWrap);
        overlayShader.Activate();
    }

    private static void RenderGenesisInstances()
    {
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

        foreach (GenesisInstance genesis in genesisPoints)
            genesis.Render();

        Main.spriteBatch.End();
    }

    /// <summary>
    /// Finds the nearest Genesis point to a given tile point, returning null if no genesis points are present.
    /// </summary>
    /// <param name="point">The point to check relative to.</param>
    public static Point? NearestGenesisPoint(Point point)
    {
        if (genesisPoints.Count <= 0)
            return null;

        Vector2 position = point.ToVector2();
        return genesisPoints.OrderBy(g => g.Anchor.ToVector2().Distance(position)).First().Anchor;
    }

    /// <summary>
    /// Tries to get the nearest Genesis instance at a given point, returning null if nothing was found.
    /// </summary>
    /// <param name="point">The point to search at.</param>
    public static GenesisInstance? TryGet(Point point)
    {
        return genesisPoints.FirstOrDefault(g => g.Anchor == point);
    }

    internal static void AddGenesisPointInternal(Point point)
    {
        if (!genesisPoints.Any(g => g.Anchor == point))
            genesisPoints.Add(new GenesisInstance(point));
    }

    /// <summary>
    /// Registers a given point as a position where a Genesis instance should be rendered.
    /// </summary>
    /// <param name="point">The genesis' position in world coordinates.</param>
    public static void AddGenesisPoint(Point point)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient && !genesisPoints.Any(g => g.Anchor == point))
            PacketManager.SendPacket<AddGenesisPointPacket>(point.X, point.Y);
        AddGenesisPointInternal(point);
    }

    public override void SaveWorldData(TagCompound tag)
    {
        tag["GenesisPoints"] = genesisPoints.Select(g => g.Anchor).ToList();
        tag["GenesisGrowthStages"] = genesisPoints.Select(g => g.GrowthStage).ToList();

        GenesisGrassMergeData[] tileData = Main.tile.GetData<GenesisGrassMergeData>();
        ushort[] byteData = Unsafe.As<GenesisGrassMergeData[], ushort[]>(ref tileData);
        List<ushort> copy = byteData.ToList();
        tag["GenesisMergeData"] = copy;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        genesisPoints.Clear();
        foreach (Point p in tag.GetList<Point>("GenesisPoints").ToList())
            genesisPoints.Add(new GenesisInstance(p));

        List<float> growthStages = tag.GetList<float>("GenesisGrowthStages").ToList();
        for (int i = 0; i < growthStages.Count; i++)
            genesisPoints[i].GrowthStage = growthStages[i];

        ushort[] byteData = tag.GetList<ushort>("GenesisMergeData").ToArray();
        Span<GenesisGrassMergeData> loadedTileData = new Span<GenesisGrassMergeData>(Unsafe.As<ushort[], GenesisGrassMergeData[]>(ref byteData));
        Span<GenesisGrassMergeData> tileData = new Span<GenesisGrassMergeData>(Main.tile.GetData<GenesisGrassMergeData>());

        if (loadedTileData.Length <= tileData.Length)
            loadedTileData.CopyTo(tileData);
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(genesisPoints.Count);
        foreach (GenesisInstance genesisPoint in genesisPoints)
        {
            writer.Write(genesisPoint.Anchor.X);
            writer.Write(genesisPoint.Anchor.Y);
            writer.Write(genesisPoint.GrowthStage);
        }
    }

    public override void NetReceive(BinaryReader reader)
    {
        genesisPoints.Clear();

        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            Point anchor = new Point(reader.ReadInt32(), reader.ReadInt32());
            float growth = reader.ReadSingle();
            genesisPoints.Add(new GenesisInstance(anchor) { GrowthStage = growth });
        }
    }
}
