using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Core.Configuration;
using NoxusBoss.Core.Data;
using NoxusBoss.Core.Graphics.RenderTargets;
using NoxusBoss.Core.Utilities;
using NoxusBoss.Core.World.GameScenes.AvatarAppearances;
using NoxusBoss.Core.World.Subworlds;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers;

public class AvatarOfEmptinessSky : CustomSky
{
    private bool isActive;

    internal static float intensity;

    public static AvatarDimensionVariant? Dimension
    {
        get;
        set;
    }

    public static TimeSpan DrawCooldown
    {
        get;
        set;
    }

    public static float WindVerticalStretchFactor
    {
        get;
        set;
    }

    public static float ScreenTearInterpolant
    {
        get;
        set;
    }

    public static bool InProximityOfMonolith
    {
        get;
        set;
    }

    public static TimeSpan LastFrameElapsedGameTime
    {
        get;
        set;
    }

    public static float SkyIntensityOverride
    {
        get;
        set;
    }

    // Ideally it'd be possible to just turn InProximityOfMidnightMonolith back to false if it was already on and its effects were registered, but since NearbyEffects hooks
    // don't run on the same update cycle as the PrepareDimensionTarget method this delay exists.
    public static int TimeSinceCloseToMonolith
    {
        get;
        set;
    }

    public static float WindSpeedFactor
    {
        get;
        set;
    }

    public static float WindTimer
    {
        get;
        set;
    }

    public static float CryonicWindSwirlTimer
    {
        get;
        set;
    }

    /// <summary>
    /// The render target that holds all downscaled sky contents.
    /// </summary>
    public static DownscaleOptimizedScreenTarget SkyTarget
    {
        get;
        private set;
    }

    /// <summary>
    /// The set of all palettes used by the Avatar's backgrounds for rendering.
    /// </summary>
    public static Dictionary<string, Vector3[]> Palettes
    {
        get;
        private set;
    } = [];

    public const string PaletteFilePath = "Content/NPCs/Bosses/Avatar/AvatarDimensionBackgroundPalettes.json";

    public const string ScreenShaderKey = "NoxusBoss:AvatarOfEmptinessSky";

    public override void OnLoad()
    {
        Palettes = LocalDataManager.Read<Vector3[]>(PaletteFilePath);
        SkyTarget = new(0.425f, RenderToSkyTarget);
    }

    private static void RenderToSkyTarget(int identifier)
    {
        if (Dimension is not null)
            Dimension.BackgroundDrawAction();
        else if (AvatarOfEmptiness.Myself is null || !AvatarOfEmptiness.Myself.As<AvatarOfEmptiness>().ParadiseReclaimedIsOngoing)
        {
            float windOpacity = InverseLerp(0f, 0.9f, Abs(WindVerticalStretchFactor));
            if (windOpacity < 1f)
                DrawBackground(0f, 1f - windOpacity);
            if (windOpacity > 0.01f)
                DrawBackground(WindVerticalStretchFactor, windOpacity);
        }
        AvatarDimensionVariants.DrawVortexBackground();
        AvatarDimensionVariants.DrawVortexBackHomeBackground();
    }

    public static void DrawBackground(float verticalStretchFactor = 0f, float opacity = 1f, float? intensityOverride = null)
    {
        // Disable this if the photosensitivity config is used, since the motion for the stretch is REALLY strong.
        if (WoTGConfig.Instance.PhotosensitivityMode)
            verticalStretchFactor = 0f;

        Vector2 screenArea = ViewportSize;
        Vector2 textureArea = screenArea / WhitePixel.Size();
        Vector3[] palette = Palettes["Standard"];

        float time = WindTimer;
        if (intensityOverride is not null || Main.gameMenu)
            time = Main.GlobalTimeWrappedHourly * 0.6f;

        // Draw the background with a special shader.
        ManagedShader backgroundShader = ShaderManager.GetShader("NoxusBoss.AvatarPhase2BackgroundShader");
        backgroundShader.TrySetParameter("intensity", intensityOverride ?? (Main.gameMenu ? 1f : Clamp(intensity, SkyIntensityOverride, 1f)));
        backgroundShader.TrySetParameter("screenOffset", Main.screenPosition * 0.00007f);
        backgroundShader.TrySetParameter("gradientCount", palette.Length);
        backgroundShader.TrySetParameter("gradient", palette);
        backgroundShader.TrySetParameter("time", time);
        backgroundShader.TrySetParameter("arcCurvature", InverseLerp(1.64f, 0f, verticalStretchFactor) * 3.4f);
        backgroundShader.TrySetParameter("windPrevalence", 1.22f);
        backgroundShader.TrySetParameter("brightnessMaskDetail", 22.5f);
        backgroundShader.TrySetParameter("brightnessNoiseVariance", 0.56f);
        backgroundShader.TrySetParameter("backgroundBaseColor", new Vector4(0f, 0f, 0.021f, 0f));
        backgroundShader.TrySetParameter("vignetteColor", new Vector4(0.29f, 0f, 0.38f, 0f));
        backgroundShader.TrySetParameter("vignettePower", 3.2f);
        backgroundShader.SetTexture(PerlinNoise, 1, SamplerState.LinearWrap);
        backgroundShader.SetTexture(WavyBlotchNoiseDetailed, 2, SamplerState.LinearWrap);
        backgroundShader.SetTexture(PerlinNoise, 3, SamplerState.LinearWrap);
        backgroundShader.SetTexture(WatercolorNoiseA, 4, SamplerState.LinearWrap);
        backgroundShader.Apply();

        textureArea.Y *= 1f + verticalStretchFactor;

        Color color = Color.White * opacity;
        Main.spriteBatch.Draw(WhitePixel, screenArea * 0.5f, null, color, 0f, WhitePixel.Size() * 0.5f, textureArea, 0, 0f);
    }

    public override void Activate(Vector2 position, params object[] args)
    {
        isActive = true;
    }

    public override void Deactivate(params object[] args)
    {
        isActive = false;
    }

    public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth)
    {
        if (EternalGardenUpdateSystem.WasInSubworldLastUpdateFrame)
            return;

        // Ensure that the background only draws once per frame for efficiency.
        DrawCooldown -= LastFrameElapsedGameTime;
        bool invalidDepth = minDepth >= -1000000f;
        if (ModContent.GetInstance<PostMLRiftAppearanceSystem>().Ongoing)
            invalidDepth = maxDepth < float.MaxValue || minDepth >= float.MaxValue;

        if (Dimension != AvatarDimensionVariants.AntishadowDimension)
        {
            if (invalidDepth || (DrawCooldown.TotalMilliseconds >= 17 && Main.instance.IsActive))
                return;
        }

        Main.spriteBatch.End();
        Main.spriteBatch.Begin();

        DrawCooldown = TimeSpan.FromSeconds(1D / 60D);

        Vector2 screenArea = ViewportSize;
        Vector2 textureArea = screenArea / WhitePixel.Size() * 2f;

        Main.spriteBatch.Draw(WhitePixel, screenArea * 0.5f, null, Color.Black * intensity, 0f, WhitePixel.Size() * 0.5f, textureArea, 0, 0f);
        SkyTarget.Render(Color.White, new Rectangle(0, 0, (int)screenArea.X, (int)screenArea.Y));

        Main.spriteBatch.ResetToDefault();

        if (ScreenTearInterpolant >= 0.001f && AvatarOfEmptiness.Myself is not null)
        {
            Vector2 worldPosition = AvatarOfEmptiness.Myself is null ? Vector2.Zero : AvatarOfEmptiness.Myself.Center;
            Vector2 avatarUV = (worldPosition - Main.screenPosition) / Main.ScreenSize.ToVector2();

            ManagedScreenFilter tearShader = ShaderManager.GetFilter("NoxusBoss.AvatarRealityTearShader");
            tearShader.TrySetParameter("avatarUV", avatarUV);
            tearShader.TrySetParameter("uIntensity", ScreenTearInterpolant);
            tearShader.TrySetParameter("worldTransform", Matrix.Invert(Main.GameViewMatrix.TransformationMatrix));
            tearShader.SetTexture(TurbulentNoise, 1, SamplerState.LinearWrap);
            tearShader.Activate();
        }
    }

    public override float GetCloudAlpha() => 1f - Pow(Clamp(intensity, SkyIntensityOverride, 1f), 0.45f);

    public override bool IsActive()
    {
        return isActive || intensity > 0f;
    }

    public override Color OnTileColor(Color inColor)
    {
        return Color.Lerp(inColor, new(255, 0, 81), intensity * 0.7f);
    }

    public override void Reset()
    {
        isActive = false;
    }

    public override void Update(GameTime gameTime)
    {
        // Increase the Midnight monolith proximity timer.
        if (!Main.gamePaused && Main.instance.IsActive)
            TimeSinceCloseToMonolith++;
        if (TimeSinceCloseToMonolith >= 10)
            InProximityOfMonolith = false;

        // Make the intensity go up or down based on whether the sky is in use.
        if (!Main.gamePaused)
            intensity = Saturate(intensity + (isActive ? 0.08f : -0.01f));

        // Make optional interpolants go down.
        if (!Main.gamePaused)
            ScreenTearInterpolant = Saturate(ScreenTearInterpolant - 0.05f);

        if (!Main.gamePaused)
            WindVerticalStretchFactor = Lerp(WindVerticalStretchFactor, 0f, 0.1f);

        // Disable ambient sky objects like wyverns and eyes appearing in front of the dark cloud of death.
        if (isActive)
        {
            SkyManager.Instance["Ambience"].Deactivate();
            SkyManager.Instance["Party"].Deactivate();
        }

        // Randomly create flashes.
        int avatarIndex = NPC.FindFirstNPC(ModContent.NPCType<AvatarOfEmptiness>());
        if (avatarIndex == -1)
            Dimension = null;

        if (InProximityOfMonolith)
        {
            SkyIntensityOverride = Saturate(SkyIntensityOverride + 0.08f);
            intensity = SkyIntensityOverride;
        }
        else
        {
            SkyIntensityOverride = Saturate(SkyIntensityOverride - 0.07f);
            if (SkyIntensityOverride > 0f)
                intensity = SkyIntensityOverride;
        }

        // Increment time.
        WindTimer += (float)gameTime.ElapsedGameTime.TotalSeconds * WindSpeedFactor * intensity;

        float windIntensityFactor = Cbrt(Abs(Main.windSpeedCurrent)) * Sign(Main.windSpeedCurrent) * 0.2f;
        CryonicWindSwirlTimer += (float)gameTime.ElapsedGameTime.TotalSeconds * WindSpeedFactor * intensity * windIntensityFactor;

        // Make the wind speed factor return to a default resting state.
        float idealWindSpeedFactor = 0.9f;
        if (AvatarOfEmptiness.Myself is not null && AvatarOfEmptiness.Myself.As<AvatarOfEmptiness>().Phase3)
            idealWindSpeedFactor = 1.4f;
        WindSpeedFactor = Lerp(WindSpeedFactor, idealWindSpeedFactor, 0.04f);
    }
}
