using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.Items.Accessories.VanityEffects;
using NoxusBoss.Core.Graphics.Shaders.Screen;
using NoxusBoss.Core.Graphics.UI.GraphicalUniverseImager;
using NoxusBoss.Core.Utilities;
using NoxusBoss.Core.World.GameScenes.EndCredits;
using NoxusBoss.Core.World.Subworlds;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;

public class NamelessDeitySkyScene : ModSceneEffect
{
    public override bool IsSceneEffectActive(Player player)
    {
        if (NamelessDeityBoss.Myself_CurrentState == NamelessDeityBoss.NamelessAIType.SavePlayerFromAvatar)
            return false;

        return NamelessDeitySky.SkyIntensityOverride > 0f || (NPC.AnyNPCs(ModContent.NPCType<NamelessDeityBoss>()) && !ModContent.GetInstance<EndCreditsScene>().IsActive);
    }

    public override void Load()
    {
        Filters.Scene["NoxusBoss:NamelessDeitySky"] = new Filter(new GenericScreenShaderData("FilterMiniTower").UseColor(Color.Transparent).UseOpacity(0f), EffectPriority.VeryHigh);
        SkyManager.Instance["NoxusBoss:NamelessDeitySky"] = new NamelessDeitySky();

        Main.QueueMainThreadAction(() =>
        {
            On_Main.DrawSunAndMoon += NoMoonInGarden;
            On_Main.DrawBackground += NoBackgroundDuringNamelessDeityFight;
            On_Main.DrawSurfaceBG += NoBackgroundDuringNamelessDeityFight2;
        });

        GraphicalUniverseImagerOptionManager.RegisterNew(new GraphicalUniverseImagerOption("Mods.NoxusBoss.UI.GraphicalUniverseImager.NamelessDeityStars", true,
            GennedAssets.Textures.GraphicalUniverseImager.ShaderSource_Deity, RenderGUIPortrait_Stars, (minDepth, maxDepth, settings) =>
            {
                NamelessDeityDimensionSkyGenerator.InProximityOfMonolith = true;
                NamelessDeityDimensionSkyGenerator.TimeSinceCloseToMonolith = 5;
            }));
        GraphicalUniverseImagerOptionManager.RegisterNew(new GraphicalUniverseImagerOption("Mods.NoxusBoss.UI.GraphicalUniverseImager.NamelessDeityKaleidoscope", true,
            GennedAssets.Textures.GraphicalUniverseImager.ShaderSource_Deity, RenderGUIPortrait_Kaleidoscope, (minDepth, maxDepth, settings) =>
            {
                NamelessDeityDimensionSkyGenerator.DrawKaleidoscopicBackground(ModContent.GetInstance<GraphicalUniverseImagerSky>().EffectiveIntensity, true);
            }));
    }

    private static void RenderGUIPortrait_Stars(GraphicalUniverseImagerSettings settings)
    {
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
        CosmicBackgroundSystem.Draw(ViewportSize * 0.5f, 3f);

        Main.spriteBatch.End();
    }

    private static void RenderGUIPortrait_Kaleidoscope(GraphicalUniverseImagerSettings settings)
    {
        Main.spriteBatch.Begin();
        NamelessDeityDimensionSkyGenerator.DrawKaleidoscopicBackground(1f, true);
        Main.spriteBatch.End();
    }

    private void NoMoonInGarden(On_Main.orig_DrawSunAndMoon orig, Main self, Main.SceneArea sceneArea, Color moonColor, Color sunColor, float tempMushroomInfluence)
    {
        // The moon does not exist in the garden subworld, because it is not the base Terraria world.
        if (!EternalGardenUpdateSystem.WasInSubworldLastUpdateFrame)
            orig(self, sceneArea, moonColor * Pow(1f - NamelessDeitySky.SkyEyeOpacity, 2f), sunColor, tempMushroomInfluence);
    }

    private void NoBackgroundDuringNamelessDeityFight(On_Main.orig_DrawBackground orig, Main self)
    {
        if (NamelessDeitySky.HeavenlyBackgroundIntensity < 0.3f)
            orig(self);
    }

    private void NoBackgroundDuringNamelessDeityFight2(On_Main.orig_DrawSurfaceBG orig, Main self)
    {
        if (NamelessDeitySky.HeavenlyBackgroundIntensity < 0.3f)
            orig(self);
        else
        {
            SkyManager.Instance.ResetDepthTracker();
            SkyManager.Instance.DrawToDepth(Main.spriteBatch, 1f / 0.12f);
            if (!Main.mapFullscreen)
                SkyManager.Instance.DrawRemainingDepth(Main.spriteBatch);
        }
    }

    public override void SpecialVisuals(Player player, bool isActive)
    {
        player.ManageSpecialBiomeVisuals("NoxusBoss:NamelessDeitySky", isActive);

        if (!isActive)
            NamelessDeitySky.GraduallyResetEverything();
    }
}

public class NamelessDeitySky : CustomSky
{
    public class BackgroundSmoke
    {
        public int Time;

        public int Lifetime;

        public float Rotation;

        public Vector2 DrawPosition;

        public Vector2 Velocity;

        public Color SmokeColor;

        public void Update()
        {
            Time++;
            DrawPosition += Velocity;
            Velocity *= 0.983f;
            SmokeColor *= 0.997f;
            Rotation += Velocity.X * 0.01f;
        }
    }

    public static bool IsEffectActive
    {
        get;
        private set;
    }

    public static float Intensity
    {
        get;
        internal set;
    }

    public static float StarRecedeInterpolant
    {
        get;
        set;
    }

    public static float SkyIntensityOverride
    {
        get;
        set;
    }

    public static float SkyEyeOpacity
    {
        get;
        set;
    }

    public static float SkyEyeScale
    {
        get;
        set;
    } = 1f;

    public static float SkyPupilScale
    {
        get;
        set;
    } = 1f;

    public static float KaleidoscopeInterpolant
    {
        get;
        set;
    }

    public static float BlackOverlayInterpolant
    {
        get;
        set;
    }

    public static Vector2 SkyPupilOffset
    {
        get;
        set;
    }

    public static float SeamScale
    {
        get;
        set;
    }

    public static float HeavenlyBackgroundIntensity
    {
        get;
        set;
    }

    public static float ManualSunScale
    {
        get;
        set;
    } = 1f;

    public static float DifferentStarsInterpolant
    {
        get;
        set;
    }

    // Used during the GFB glock attack. It's awesome.
    public static float UnitedStatesFlagOpacity
    {
        get;
        set;
    }

    public static List<BackgroundSmoke> SmokeParticles
    {
        get;
        private set;
    } = [];

    public static TimeSpan DrawCooldown
    {
        get;
        set;
    }

    public static TimeSpan LastFrameElapsedGameTime
    {
        get;
        set;
    }

    public static float SeamAngle => 1.67f;

    public override void Update(GameTime gameTime)
    {
        LastFrameElapsedGameTime = gameTime.ElapsedGameTime;

        // Make the intensity go up or down based on whether the sky is in use.
        Intensity = Saturate(Intensity + IsEffectActive.ToDirectionInt() * 0.01f);

        // Make the star recede interpolant go up or down based on how strong the intensity is. If the intensity is at its maximum the effect is uninterrupted.
        StarRecedeInterpolant = Saturate(StarRecedeInterpolant - (1f - Intensity) * 0.11f);

        // Disable ambient sky objects like wyverns and eyes appearing in front of the background.
        if (IsEffectActive)
            SkyManager.Instance["Ambience"].Deactivate();

        if (!NamelessDeityDimensionSkyGenerator.InProximityOfMonolith)
            SkyIntensityOverride = Saturate(SkyIntensityOverride - 0.07f);
        if (Intensity < 1f)
            SkyEyeOpacity = Clamp(SkyEyeOpacity - 0.02f, 0f, Intensity + 0.001f);

        float minKaleidoscopeInterpolant = 0f;
        if (NamelessDeityBoss.Myself is not null && NamelessDeityBoss.Myself.As<NamelessDeityBoss>().CurrentPhase >= 2)
        {
            NamelessDeityBoss.NamelessAIType namelessAIState = NamelessDeityBoss.Myself.As<NamelessDeityBoss>().CurrentState;
            if (namelessAIState is NamelessDeityBoss.NamelessAIType.SuperCosmicLaserbeam or NamelessDeityBoss.NamelessAIType.MomentOfCreation)
                minKaleidoscopeInterpolant = 0f;
            else if (namelessAIState == NamelessDeityBoss.NamelessAIType.ClockConstellation)
                minKaleidoscopeInterpolant = 0.7f;
            else
                minKaleidoscopeInterpolant = 0.9f;
        }

        // Make a bunch of things return to their base values.
        UnitedStatesFlagOpacity = Saturate(UnitedStatesFlagOpacity - 0.01f);
        if (!Main.gamePaused && !IsEffectActive)
            GraduallyResetEverything(minKaleidoscopeInterpolant);
        else if (KaleidoscopeInterpolant < minKaleidoscopeInterpolant || (HeavenlyBackgroundIntensity < 1f && minKaleidoscopeInterpolant >= 0.01f))
        {
            KaleidoscopeInterpolant = minKaleidoscopeInterpolant;
            HeavenlyBackgroundIntensity = 1f;
        }

        if (NamelessDeityBoss.Myself is not null && NamelessDeityBoss.Myself_CurrentState != NamelessDeityBoss.NamelessAIType.DarknessWithLightSlashes)
            BlackOverlayInterpolant = Saturate(BlackOverlayInterpolant - 0.09f);

        // Make the eye disappear from the background if Nameless is already visible in the foreground.
        if (NamelessDeityBoss.Myself is not null && NamelessDeityBoss.Myself.Opacity >= 0.3f)
            SkyEyeScale = 0f;

        if (!IsEffectActive)
            SeamScale = 0f;
    }

    public static void GraduallyResetEverything(float minKaleidoscopeInterpolant = 0f)
    {
        SkyPupilOffset = Utils.MoveTowards(Vector2.Lerp(SkyPupilOffset, Vector2.Zero, 0.03f), Vector2.Zero, 4f);
        SkyPupilScale = Lerp(SkyPupilScale, 1f, 0.05f);
        SkyEyeScale = Lerp(SkyEyeScale, 1f, 0.05f);
        SeamScale = Clamp(SeamScale * 0.87f - 0.023f, 0f, 300f);
        HeavenlyBackgroundIntensity = Clamp(HeavenlyBackgroundIntensity - 0.02f, 0f, 2.5f);
        ManualSunScale = Clamp(ManualSunScale * 0.92f - 0.3f, 0f, 50f);
        DifferentStarsInterpolant = Saturate(DifferentStarsInterpolant - 0.1f);
        KaleidoscopeInterpolant = Clamp(KaleidoscopeInterpolant * 0.95f - 0.15f, minKaleidoscopeInterpolant, 1f);
    }

    public static void UpdateSmokeParticles()
    {
        // Randomly emit smoke.
        int smokeReleaseChance = 6;
        if (Main.rand.NextBool(smokeReleaseChance))
        {
            for (int i = 0; i < 4; i++)
            {
                SmokeParticles.Add(new BackgroundSmoke()
                {
                    DrawPosition = new Vector2(Main.rand.NextFloat(-400f, Main.screenWidth + 400f), Main.screenHeight + 372f),
                    Velocity = -Vector2.UnitY * Main.rand.NextFloat(5f, 23f) + Main.rand.NextVector2Circular(3f, 3f),
                    SmokeColor = Color.Lerp(Color.Coral, Color.Wheat, Main.rand.NextFloat(0.5f, 0.85f)) * 0.9f,
                    Rotation = Main.rand.NextFloat(TwoPi),
                    Lifetime = Main.rand.Next(120, 480)
                });
            }
        }

        // Update smoke particles.
        SmokeParticles.RemoveAll(s => s.Time >= s.Lifetime);
        foreach (BackgroundSmoke smoke in SmokeParticles)
            smoke.Update();
    }

    public override Color OnTileColor(Color inColor)
    {
        return Color.Lerp(inColor, Color.White, Intensity * Lerp(0.4f, 1f, HeavenlyBackgroundIntensity) * 0.9f);
    }

    public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth)
    {
        // Ensure that the background only draws once per frame for efficiency.
        DrawCooldown -= LastFrameElapsedGameTime;
        if (minDepth >= -1000000f || (DrawCooldown.TotalMilliseconds >= 0.01 && Main.instance.IsActive))
            return;

        // Draw the sky background overlay, sun, and smoke.
        DrawCooldown = TimeSpan.FromSeconds(1D / 60D);
        if (maxDepth >= 0f && minDepth < 0f)
        {
            Main.spriteBatch.End();
            Main.spriteBatch.Begin();

            Vector2 screenArea = ViewportSize;
            Main.spriteBatch.Draw(NamelessDeitySkyTargetManager.SkyTarget, new Rectangle(0, 0, (int)screenArea.X, (int)screenArea.Y), Color.White);

            // Draw the divine rose.
            if (NamelessDeityBoss.Myself_CurrentState == NamelessDeityBoss.NamelessAIType.MomentOfCreation)
                DivineRoseSystem.Draw(NamelessDeityBoss.Myself.As<NamelessDeityBoss>().AITimer);

            Main.spriteBatch.ResetToDefault();
        }
    }

    public static void DrawUnitedStatesFlag(Vector2 screenArea)
    {
        Texture2D flag = GennedAssets.Textures.NamelessDeity.UnitedStatesFlag.Value;
        Main.spriteBatch.Draw(flag, screenArea * 0.5f, null, Color.White * UnitedStatesFlagOpacity, 0f, flag.Size() * 0.5f, screenArea / flag.Size(), 0, 0f);
    }

    public static void DrawStarDimension()
    {
        Luminance.Core.Graphics.ManagedRenderTarget starTexture = NamelessDeityDimensionSkyGenerator.NamelessDeityDimensionTarget;
        float opacity = Intensity.Squared();
        if (DeificTouch.UsingEffect)
            opacity = 1f;

        Main.spriteBatch.Draw(starTexture, Vector2.Zero, Color.White * opacity);

        if (KaleidoscopeInterpolant >= 0.001f)
        {
            Main.pixelShader.CurrentTechnique.Passes[0].Apply();
            NamelessDeityDimensionSkyGenerator.DrawKaleidoscopicBackground(Intensity * KaleidoscopeInterpolant, false);
        }
    }

    public static void ReplaceMoonWithNamelessDeityEye(Vector2 eyePosition, Matrix perspectiveMatrix)
    {
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, perspectiveMatrix);

        // Draw a glowing orb over the moon.
        float glowDissipateFactor = Utils.Remap(SkyEyeOpacity, 0.2f, 1f, 1f, 0.74f);
        Vector2 backglowOrigin = BloomCircleSmall.Size() * 0.5f;
        Vector2 baseScale = Vector2.One * SkyEyeOpacity * Lerp(1.9f, 2f, Cos01(Main.GlobalTimeWrappedHourly * 4f)) * SkyEyeScale * 1.4f;

        // Make everything "blink" at first.
        baseScale.Y *= 1f - Convert01To010(InverseLerp(0.25f, 0.75f, SkyEyeOpacity));

        Color additiveWhite = Color.White with { A = 0 };
        Main.spriteBatch.Draw(BloomCircleSmall, eyePosition, null, additiveWhite * glowDissipateFactor * 0.42f, 0f, backglowOrigin, baseScale * 1.4f, 0, 0f);
        Main.spriteBatch.Draw(BloomCircleSmall, eyePosition, null, (Color.IndianRed with { A = 0 }) * glowDissipateFactor * 0.22f, 0f, backglowOrigin, baseScale * 2.4f, 0, 0f);
        Main.spriteBatch.Draw(BloomCircleSmall, eyePosition, null, (Color.Coral with { A = 0 }) * glowDissipateFactor * 0.13f, 0f, backglowOrigin, baseScale * 3.3f, 0, 0f);

        // Draw a bloom flare over the orb.
        Main.spriteBatch.Draw(BloomFlare, eyePosition, null, (Color.LightCoral with { A = 0 }) * glowDissipateFactor * 0.6f, Main.GlobalTimeWrappedHourly * 0.4f, BloomFlare.Size() * 0.5f, baseScale * 0.7f, 0, 0f);
        Main.spriteBatch.Draw(BloomFlare, eyePosition, null, (Color.Coral with { A = 0 }) * glowDissipateFactor * 0.6f, Main.GlobalTimeWrappedHourly * -0.26f, BloomFlare.Size() * 0.5f, baseScale * 0.7f, 0, 0f);

        // Draw the spires over the bloom flare.
        Main.spriteBatch.Draw(ChromaticSpires, eyePosition, null, additiveWhite * glowDissipateFactor, 0f, ChromaticSpires.Size() * 0.5f, baseScale * 0.8f, 0, 0f);

        // Draw the eye.
        Texture2D eyeTexture = GennedAssets.Textures.NamelessDeity.NamelessDeityEye.Value;
        Texture2D pupilTexture = GennedAssets.Textures.NamelessDeity.NamelessDeityPupil.Value;
        Vector2 eyeScale = baseScale * 0.4f;
        Main.spriteBatch.Draw(eyeTexture, eyePosition, null, additiveWhite * SkyEyeOpacity, 0f, eyeTexture.Size() * 0.5f, eyeScale, 0, 0f);
        Main.spriteBatch.Draw(pupilTexture, eyePosition + (new Vector2(6f, 0f) + SkyPupilOffset) * eyeScale, null, additiveWhite * SkyEyeOpacity, 0f, pupilTexture.Size() * 0.5f, eyeScale * SkyPupilScale, 0, 0f);
    }

    public override float GetCloudAlpha() => 1f - Clamp(Intensity, SkyIntensityOverride, 1f);

    public override void Activate(Vector2 position, params object[] args)
    {
        IsEffectActive = true;
    }

    public override void Deactivate(params object[] args)
    {
        IsEffectActive = false;
    }

    public override void Reset()
    {
        IsEffectActive = false;
    }

    public override bool IsActive()
    {
        return IsEffectActive || Intensity > 0f;
    }
}
