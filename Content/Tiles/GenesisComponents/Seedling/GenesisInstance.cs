using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Core.Graphics.GenesisEffects;
using NoxusBoss.Core.Graphics.LightingMask;
using NoxusBoss.Core.SolynEvents;
using NoxusBoss.Core.World.TileDisabling;
using NoxusBoss.Core.World.WorldSaving;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;
using static NoxusBoss.Content.Tiles.GenesisComponents.GenesisPlantTileRenderingSystem;

namespace NoxusBoss.Content.Tiles.GenesisComponents.Seedling;

public class GenesisInstance
{
    /// <summary>
    /// How long this Genesis has to wait before it should begin changing its growth stage in accordance with how many nearby plants there are.
    /// </summary>
    public int GrowthDelayCountdown
    {
        get;
        set;
    }

    /// <summary>
    /// How long this Genesis should wait before playing another sound.
    /// </summary>
    public int SoundDelay
    {
        get;
        set;
    }

    /// <summary>
    /// The current growth stage of this Genesis instance. Influenced by nearby plants.
    /// </summary>
    ///
    /// <remarks>
    /// The fractional part of the growth stage corresponds to how much the Genesis should shift from one stage to the next.
    /// </remarks>
    public float GrowthStage
    {
        get;
        set;
    }

    /// <summary>
    /// The anchor point for this Genesis instance.
    /// </summary>
    public Point Anchor
    {
        get;
        set;
    }

    /// <summary>
    /// The amount of influencing plants are near this Genesis, such as the Antiseed or Synthetic Seedling.
    /// </summary>
    public int TotalNearbyPlants => NearbyPlants.Count;

    /// <summary>
    /// The identifier in the render target for this Genesis instance's snapshot.
    /// </summary>
    public int SnapshotIdentifier => Anchor.X * 2 + Anchor.Y * 20000;

    /// <summary>
    /// The identifier in the render target for this Genesis instance's future state.
    /// </summary>
    public int IdealIdentifier => SnapshotIdentifier + 1;

    /// <summary>
    /// The set of nearby Genesis plants.
    /// </summary>
    public readonly List<GenesisPlantTileRenderingSystem> NearbyPlants = [];

    /// <summary>
    /// The chance for this Genesis instance to create random creaking sounds.
    /// </summary>
    public static int CreakChance => SecondsToFrames(6.7f);

    public GenesisInstance(Point anchor) => Anchor = anchor;

    /// <summary>
    /// Updates this Genesis instance, affecting things such as lighting and the natural change of growth state based on nearby Genesis instances.
    /// </summary>
    public void Update()
    {
        int previousNearbyPlantCount = TotalNearbyPlants;
        NearbyPlants.Clear();

        // Check for nearby plants that could influence this Genesis.
        Dictionary<GenesisPlantTileRenderingSystem, List<PlantTileData>> allTilePoints = GenesisPlantTileRenderingSystem.allTilePoints;
        foreach (GenesisPlantTileRenderingSystem system in allTilePoints.Keys)
        {
            List<PlantTileData> tilePoints = allTilePoints[system];
            foreach (PlantTileData plantTileData in tilePoints)
            {
                if (Anchor.ToVector2().WithinRange(plantTileData.Position.ToVector2(), GenesisGrass.ConversionRadius))
                {
                    NearbyPlants.Add(system);
                    break;
                }
            }
        }

        // Enforce a short wait period before this Genesis can grow, so that it doesn't weirdly do so the instant a new plant is introduced.
        if (TotalNearbyPlants != previousNearbyPlantCount)
        {
            GrowthStage -= 0.001f; // Necessary to make the floor/ceiling calculations during rendering work properly.
            GrowthDelayCountdown = 180;
        }

        // Record if a Genesis instance was completed.
        if (!WorldSaveSystem.HasCompletedGenesis && TotalNearbyPlants >= GrowingGenesisRenderSystem.GrowthStageNeededToUse)
            WorldSaveSystem.HasCompletedGenesis = true;

        // Grow this Genesis, waiting if necessary.
        if (GrowthDelayCountdown <= 0)
            GrowthStage = GrowthStage.StepTowards(TotalNearbyPlants, 0.01f);
        else
        {
            GrowthDelayCountdown--;
            if (GrowthDelayCountdown <= 0)
                SoundEngine.PlaySound(GennedAssets.Sounds.Genesis.Grow, Anchor.ToWorldCoordinates());
        }

        // Make tiles update near this Genesis instances far more commonly.
        for (int i = 0; i < 450; i++)
        {
            Vector2 offset = Main.rand.NextVector2Circular(GenesisGrass.ConversionRadius, GenesisGrass.ConversionRadius) * 16f;
            Point samplePosition = (Anchor.ToWorldCoordinates() + offset).ToTileCoordinates();
            TileLoader.RandomUpdate(samplePosition.X, samplePosition.Y, Main.tile[samplePosition].TileType);
        }

        // Randomly play sounds.
        SoundDelay--;
        if (Main.rand.NextBool(CreakChance) && GrowthStage >= 2f && SoundDelay <= 0 && GrowthStage == (int)GrowthStage && GenesisVisualsSystem.ActivationPhase == GenesisActivationPhase.Inactive)
        {
            SoundDelay = SecondsToFrames(Main.rand.NextFloat(4f, 8f));
            SoundEngine.PlaySound(GennedAssets.Sounds.Genesis.CreakyAmbient, Anchor.ToWorldCoordinates());
        }

        CheckForActivation();
    }

    /// <summary>
    /// Render this Genesis instance.
    /// </summary>
    public void Render()
    {
        if (TileDisablingSystem.TilesAreUninteractable)
            return;

        // Take a snapshot of this Genesis instance before growing.
        if (GrowthDelayCountdown == 3)
            GrowingGenesisRenderSystem.GenesisTarget.Request(720, 720, SnapshotIdentifier, () => RenderByStage((int)Floor(GrowthStage)));

        GrowingGenesisRenderSystem.GenesisTarget.Request(720, 720, IdealIdentifier, () => RenderByStage((int)Ceiling(GrowthStage)));
        if (!GrowingGenesisRenderSystem.GenesisTarget.TryGetTarget(IdealIdentifier, out RenderTarget2D? idealTarget) || idealTarget is null)
            return;

        Texture2D previousStage = idealTarget;
        if (GrowingGenesisRenderSystem.GenesisTarget.TryGetTarget(SnapshotIdentifier, out RenderTarget2D? snapshot) && snapshot is not null)
            previousStage = snapshot;

        float lightInfluenceFactor = InverseLerp(120f, 0f, GenesisVisualsSystem.Time);
        if (GenesisVisualsSystem.ActivationPhase == GenesisActivationPhase.MomentOfSuspense)
            lightInfluenceFactor = 0f;
        if (GenesisVisualsSystem.ActivationPhase == GenesisActivationPhase.LaserFires)
            lightInfluenceFactor = 0f;

        float morphInterpolant = SmoothStep(0f, 1f, GrowthStage % 1.0001f);
        if (GrowthStage == 0f)
            morphInterpolant = 1f;

        bool canBeActivated = GrowthStage >= GrowingGenesisRenderSystem.GrowthStageNeededToUse && ModContent.GetInstance<GenesisCompletionEvent>().Stage >= 2 && !GenesisVisualsSystem.EffectActive;
        Color outlineColor = canBeActivated && Main.LocalPlayer.WithinRange(Anchor.ToWorldCoordinates(), 300f) ? new Color(255, 255, 47) : Color.Transparent;

        ManagedShader overlayShader = ShaderManager.GetShader("NoxusBoss.BaseGenesisOverlayShader");
        overlayShader.TrySetParameter("pixelationFactor", Vector2.One * 0.01f / idealTarget.Size());
        overlayShader.TrySetParameter("textureSize0", idealTarget.Size());
        overlayShader.TrySetParameter("lightInfluenceFactor", lightInfluenceFactor);
        overlayShader.TrySetParameter("morphToGenesisInterpolant", morphInterpolant);
        overlayShader.TrySetParameter("screenArea", Main.ScreenSize.ToVector2());
        overlayShader.TrySetParameter("zoom", Main.GameViewMatrix.Zoom);
        overlayShader.TrySetParameter("outlineColor", outlineColor.ToVector4());
        overlayShader.SetTexture(previousStage, 1, SamplerState.PointClamp);
        overlayShader.SetTexture(GennedAssets.Textures.Extra.PsychedelicWingTextureOffsetMap, 2, SamplerState.LinearWrap);
        overlayShader.SetTexture(LightingMaskTargetManager.LightTarget, 3, SamplerState.LinearWrap);
        overlayShader.Apply();

        Vector2 lowerStemPosition = Anchor.ToWorldCoordinates(8f, 18f) - Main.screenPosition;
        Main.spriteBatch.Draw(idealTarget, lowerStemPosition, null, Color.White, 0f, idealTarget.Size() * new Vector2(0.5f, 1f), 1f, 0, 0f);
    }

    /// <summary>
    /// Checks for Genesis activation, assuming that's valid.
    /// </summary>
    public void CheckForActivation()
    {
        if (!GenesisVisualsSystem.EffectActive)
        {
            Vector2 position = Anchor.ToWorldCoordinates();
            Rectangle clickArea = new Rectangle((int)(position.X - 60f), (int)(position.Y - 300f), 120, 300);
            bool clicked = Main.mouseRight && Main.mouseRightRelease && Utils.CenteredRectangle(Main.MouseWorld, Vector2.One).Intersects(clickArea) && Main.LocalPlayer.WithinRange(Anchor.ToWorldCoordinates(), 300f);
            if (clicked && GrowthStage >= GrowingGenesisRenderSystem.GrowthStageNeededToUse && ModContent.GetInstance<GenesisCompletionEvent>().Stage >= 2)
                GenesisVisualsSystem.Start(position + new Vector2(-20f, -390f));
        }
    }

    /// <summary>
    /// Render this Genesis instance by stage.
    /// </summary>
    /// <param name="stage">The stage to render.</param>
    public void RenderByStage(int stage)
    {
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, CullOnlyScreen);

        switch (stage)
        {
            case 0:
                Render_Stage1();
                break;
            case 1:
                Render_Stage2();
                break;
            case 2:
                Render_Stage3();
                break;
            case 3:
                Render_Stage4();
                break;
        }

        Main.spriteBatch.End();
    }

    /// <summary>
    /// Render the stage 1 variant of this Genesis instance.
    /// </summary>
    public void Render_Stage1()
    {
        Texture2D bulb = GennedAssets.Textures.SeedlingStage1.Bulb;
        Texture2D bulbGlowmask = GennedAssets.Textures.SeedlingStage1.BulbGlow;

        Vector2 bulbPosition = new Vector2(Main.instance.GraphicsDevice.Viewport.Width * 0.5f, Main.instance.GraphicsDevice.Viewport.Height + 2f);

        // Add a touch of wind to the bulb.
        float wind = AperiodicSin(Main.GlobalTimeWrappedHourly * 0.56f + Anchor.X + Anchor.Y) * 0.033f + Main.windSpeedCurrent * 0.17f;

        // Draw the bulb.
        float bulbSquimsh = Cos(Main.GlobalTimeWrappedHourly * 4.5f + Anchor.X + Anchor.Y) * 0.011f;
        Vector2 bulbScale = new Vector2(1f - bulbSquimsh, 1f + bulbSquimsh);
        Color glowmaskColor = new Color(2, 0, 156);
        ManagedShader shader = ShaderManager.GetShader("NoxusBoss.GenesisGlowmaskShader");
        shader.SetTexture(bulbGlowmask, 1);
        shader.Apply();
        Main.spriteBatch.Draw(bulb, bulbPosition, null, glowmaskColor, wind, bulb.Size() * new Vector2(0.5f, 1f), bulbScale, 0, 0f);
    }

    /// <summary>
    /// Render the stage 2 variant of this Genesis instance.
    /// </summary>
    public void Render_Stage2()
    {
        Texture2D stem = GennedAssets.Textures.SeedlingStage2.Stem;
        Texture2D bulb = GennedAssets.Textures.SeedlingStage2.Bulb;
        Texture2D bulbGlowmask = GennedAssets.Textures.SeedlingStage2.BulbGlow;

        Vector2 stemPosition = new Vector2(Main.instance.GraphicsDevice.Viewport.Width * 0.5f, Main.instance.GraphicsDevice.Viewport.Height);
        Vector2 bulbPosition = stemPosition + Vector2.UnitY * (-stem.Height + 18f);

        // Add a touch of wind to the bulb.
        float wind = AperiodicSin(Main.GlobalTimeWrappedHourly * 0.56f + Anchor.X + Anchor.Y) * 0.029f + Main.windSpeedCurrent * 0.13f;

        // Draw the bulb. This is done before the upper stem is rendered to ensure that it draws behind it.
        float bulbSquimsh = Cos(Main.GlobalTimeWrappedHourly * 4.5f + Anchor.X + Anchor.Y) * 0.013f;
        Vector2 bulbScale = new Vector2(1f - bulbSquimsh, 1f + bulbSquimsh);
        Color glowmaskColor = new Color(3, 20, 179);
        ManagedShader shader = ShaderManager.GetShader("NoxusBoss.GenesisGlowmaskShader");
        shader.SetTexture(bulbGlowmask, 1);
        shader.Apply();
        Main.spriteBatch.Draw(bulb, bulbPosition, null, glowmaskColor, wind, bulb.Size() * new Vector2(0.5f, 1f), bulbScale, 0, 0f);

        Main.pixelShader.CurrentTechnique.Passes[0].Apply();

        // Draw the stem.
        Main.spriteBatch.Draw(stem, stemPosition, null, Color.White, 0f, stem.Size() * new Vector2(0.5f, 1f), 1f, 0, 0f);
    }

    /// <summary>
    /// Render the stage 3 variant of this Genesis instance.
    /// </summary>
    public void Render_Stage3()
    {
        Texture2D stem = GennedAssets.Textures.SeedlingStage3.Stem;
        Texture2D bulb = GennedAssets.Textures.SeedlingStage3.Bulb;
        Texture2D bulbGlowmask = GennedAssets.Textures.SeedlingStage3.BulbGlow;

        Vector2 stemPosition = new Vector2(Main.instance.GraphicsDevice.Viewport.Width * 0.5f, Main.instance.GraphicsDevice.Viewport.Height);
        Vector2 bulbPosition = stemPosition + Vector2.UnitY * (-stem.Height + 32f);

        // Add a touch of wind to the bulb.
        float wind = AperiodicSin(Main.GlobalTimeWrappedHourly * 0.56f + Anchor.X + Anchor.Y) * 0.025f + Main.windSpeedCurrent * 0.1f;

        // Draw the bulb. This is done before the upper stem is rendered to ensure that it draws behind it.
        float bulbSquimsh = Cos(Main.GlobalTimeWrappedHourly * 4.5f + Anchor.X + Anchor.Y) * 0.013f;
        Vector2 bulbScale = new Vector2(1f - bulbSquimsh, 1f + bulbSquimsh);
        Color glowmaskColor = new Color(3, 48, 179);
        ManagedShader shader = ShaderManager.GetShader("NoxusBoss.GenesisGlowmaskShader");
        shader.SetTexture(bulbGlowmask, 1);
        shader.Apply();
        Main.spriteBatch.Draw(bulb, bulbPosition, null, glowmaskColor, wind, bulb.Size() * new Vector2(0.5f, 1f), bulbScale, 0, 0f);

        Main.pixelShader.CurrentTechnique.Passes[0].Apply();

        // Draw the stem.
        Main.spriteBatch.Draw(stem, stemPosition, null, Color.White, 0f, stem.Size() * new Vector2(0.5f, 1f), 1f, 0, 0f);

        // Draw a god ray on the Genesis' light point.
        Vector2 godRayStart = bulbPosition + Main.screenPosition + (new Vector2(-39f, -66f) * bulbScale).RotatedBy(wind);
        DrawGodRay(new(255, 157, 0, 0), new(255, 247, 0), godRayStart, godRayStart + 2.7f.ToRotationVector2() * 75f, 8f, 24f);
    }

    /// <summary>
    /// Render the stage 4 variant of this Genesis instance.
    /// </summary>
    public void Render_Stage4()
    {
        Texture2D stem = GennedAssets.Textures.SeedlingStage4.Stem;
        Texture2D stemBack = GennedAssets.Textures.SeedlingStage4.StemBack;
        Texture2D bulb = GennedAssets.Textures.SeedlingStage4.Bulb;
        Texture2D bulbGlowmask = GennedAssets.Textures.SeedlingStage4.BulbGlow;

        Vector2 stemPosition = new Vector2(Main.instance.GraphicsDevice.Viewport.Width * 0.5f, Main.instance.GraphicsDevice.Viewport.Height);
        Vector2 bulbPosition = stemPosition + Vector2.UnitY * (-stem.Height + 108f);

        // Draw the back of the stem.
        Main.spriteBatch.Draw(stemBack, stemPosition, null, Color.White, 0f, stemBack.Size() * new Vector2(0.5f, 1f), 1f, 0, 0f);

        // Add a touch of wind to the bulb.
        float wind = AperiodicSin(Main.GlobalTimeWrappedHourly * 0.56f + Anchor.X + Anchor.Y) * 0.02f + Main.windSpeedCurrent * 0.07f;

        // Draw the bulb. This is done before the upper stem is rendered to ensure that it draws behind it.
        float bulbSquimsh = Cos(Main.GlobalTimeWrappedHourly * 4.5f + Anchor.X + Anchor.Y) * 0.013f;
        Vector2 bulbScale = new Vector2(1f - bulbSquimsh, 1f + bulbSquimsh);
        Color glowmaskColor = Color.Lerp(new Color(3, 48, 179), new Color(3, 133, 179), Cos01(Main.GlobalTimeWrappedHourly * 1.9f));

        float glowMotionInterpolant = InverseLerp(0f, 120f, GenesisVisualsSystem.Time);
        if (GenesisVisualsSystem.ActivationPhase == GenesisActivationPhase.MomentOfSuspense)
            glowMotionInterpolant = 1f;
        if (GenesisVisualsSystem.ActivationPhase == GenesisActivationPhase.LaserFires)
            glowMotionInterpolant = 1f;

        ManagedShader shader = ShaderManager.GetShader("NoxusBoss.Stage4GenesisGlowmaskShader");
        shader.TrySetParameter("glowMotionInterpolant", glowMotionInterpolant);
        shader.SetTexture(bulbGlowmask, 1);
        shader.SetTexture(GennedAssets.Textures.SeedlingStage4.BulbGlowTimeScroll, 2);
        shader.SetTexture(GennedAssets.Textures.SeedlingStage4.BulbGlowmaskFireGradient, 3, SamplerState.LinearWrap);
        shader.Apply();

        Main.spriteBatch.Draw(bulb, bulbPosition, null, glowmaskColor, wind, bulb.Size() * new Vector2(0.5f, 1f), bulbScale, 0, 0f);

        Main.pixelShader.CurrentTechnique.Passes[0].Apply();

        // Draw the stem.
        Main.spriteBatch.Draw(stem, stemPosition, null, Color.White, 0f, stem.Size() * new Vector2(0.5f, 1f), 1f, 0, 0f);

        // Draw god rays on the Genesis' light points.
        Vector2 godRayStart = bulbPosition + Main.screenPosition + (new Vector2(-39f, -66f) * bulbScale).RotatedBy(wind);
        DrawGodRay(new(255, 157, 0, 0), new(255, 247, 0), godRayStart, godRayStart + 2.7f.ToRotationVector2() * 120f, 24f, 62f);

        godRayStart = bulbPosition + Main.screenPosition + (new Vector2(58f, -130f) * bulbScale).RotatedBy(wind);
        DrawGodRay(new(255, 132, 0, 0), new(255, 132, 0), godRayStart, godRayStart + (-0.5f).ToRotationVector2() * 100f, 11f, 42f);
    }

    private static float GodRayWidthFunction(float completionRatio, float startingWidth, float endingWidth)
    {
        return Lerp(startingWidth, endingWidth, completionRatio);
    }

    private static Color GodRayColorFunction(float completionRatio, Color startingColor, Color endingColor)
    {
        return Color.Lerp(startingColor, endingColor, completionRatio) * Pow(1f - completionRatio, 1.4f);
    }

    private static void DrawGodRay(Color startingColor, Color endingColor, Vector2 start, Vector2 end, float startingWidth, float endingWidth)
    {
        ManagedShader godRayShader = ShaderManager.GetShader("NoxusBoss.GenesisGodRayShader");

        float startingWidthCopy = startingWidth;
        float endingWidthCopy = endingWidth;
        Color startingColorCopy = startingColor;
        Color endingColorCopy = endingColor;

        PrimitiveSettings settings = new PrimitiveSettings(c => GodRayWidthFunction(c, startingWidthCopy, endingWidthCopy), c => GodRayColorFunction(c, startingColorCopy, endingColorCopy), Shader: godRayShader,
            UseUnscaledMatrix: true, ProjectionAreaWidth: Main.instance.GraphicsDevice.Viewport.Width, ProjectionAreaHeight: Main.instance.GraphicsDevice.Viewport.Height);
        PrimitiveRenderer.RenderTrail(new Vector2[]
        {
            start,
            Vector2.Lerp(start, end, 0.2f),
            Vector2.Lerp(start, end, 0.4f),
            Vector2.Lerp(start, end, 0.6f),
            Vector2.Lerp(start, end, 0.8f),
            end
        }, settings, 90);
    }
}
