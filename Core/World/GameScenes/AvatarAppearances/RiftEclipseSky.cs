using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.FirstPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers;
using NoxusBoss.Core.CrossCompatibility.Inbound;
using NoxusBoss.Core.CrossCompatibility.Inbound.RealisticSky;
using NoxusBoss.Core.Graphics;
using NoxusBoss.Core.Graphics.Shaders.Screen;
using NoxusBoss.Core.Graphics.UI.GraphicalUniverseImager;
using NoxusBoss.Core.World.GameScenes.RiftEclipse;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

using static NoxusBoss.Core.World.GameScenes.AvatarAppearances.RiftEclipseSky;
namespace NoxusBoss.Core.World.GameScenes.AvatarAppearances;

public class RiftEclipseSkyScene : ModSceneEffect
{
    internal static Main.SceneArea PreviousSceneDetails
    {
        get;
        private set;
    }

    /// <summary>
    /// The rift eclipse option for the Graphical Universe Imager.
    /// </summary>
    public static GraphicalUniverseImagerOption RiftEclipseGUIOption
    {
        get;
        private set;
    }

    public override bool IsSceneEffectActive(Player player) => IsEnabled;

    public override SceneEffectPriority Priority => SceneEffectPriority.Environment;

    public override void Load()
    {
        Filters.Scene["NoxusBoss:NoxusRiftBackgroundSky"] = new Filter(new GenericScreenShaderData("FilterMiniTower").UseColor(Color.Transparent).UseOpacity(0f), EffectPriority.VeryHigh);
        SkyManager.Instance["NoxusBoss:NoxusRiftBackgroundSky"] = new RiftEclipseSky();

        RiftEclipseGUIOption = new GraphicalUniverseImagerOption("Mods.NoxusBoss.UI.GraphicalUniverseImager.RiftEclipse", false,
            GennedAssets.Textures.GraphicalUniverseImager.ShaderSource_Rift, RenderGUIPortrait, RenderGUIBackground, ModifyTileAndSkyColorsForGUI);
        GraphicalUniverseImagerOptionManager.RegisterNew(RiftEclipseGUIOption);
    }

    private static void RenderGUIPortrait(GraphicalUniverseImagerSettings settings)
    {
        float oldRiftScaleFactor = RiftScaleFactor;
        RiftScaleFactor = 1f;

        NPC riftDummy = new NPC();
        riftDummy.SetDefaults(ModContent.NPCType<AvatarRift>());
        riftDummy.TopLeft = ViewportSize * 0.3f;
        riftDummy.scale = Lerp(0.99f, 1f, Cos01(Main.GlobalTimeWrappedHourly * 0.9f)) * RiftScaleFactor * Lerp(0.3f, 0.85f, settings.RiftSize);
        riftDummy.As<AvatarRift>().BackgroundProp = true;
        riftDummy.As<AvatarRift>().TargetIdentifierOverride = -10;

        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

        Main.spriteBatch.Draw(WhitePixel, new Rectangle(0, 0, Main.instance.GraphicsDevice.Viewport.Width, Main.instance.GraphicsDevice.Viewport.Height), new Color(30, 30, 40));

        riftDummy.As<AvatarRift>().PreDraw(Main.spriteBatch, Vector2.Zero, Color.White);
        RiftScaleFactor = oldRiftScaleFactor;

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

        RenderFog(Matrix.Identity, 1f, 0f);
        Main.spriteBatch.End();
    }

    private static void RenderGUIBackground(float minDepth, float maxDepth, GraphicalUniverseImagerSettings settings)
    {
        RealisticSkyCompatibility.SunBloomOpacity = 0f;
        RealisticSkyCompatibility.TemporarilyDisable();

        float intensity = ModContent.GetInstance<GraphicalUniverseImagerSky>().EffectiveIntensity;

        if (minDepth <= float.MinValue)
            RenderFog(GetCustomSkyBackgroundMatrix(), intensity, 0.3f);

        // Prevent rendering anything but fog beyond the back layer.
        if (maxDepth < float.MaxValue || minDepth >= float.MaxValue)
            return;

        // Calculate the position of the sun so that the Avatar can potentially eclipse it.
        Vector2 riftDrawPosition = Main.dayTime ? SunMoonPositionRecorder.SunPosition : SunMoonPositionRecorder.MoonPosition;

        NPC riftDummy = new NPC();
        riftDummy.SetDefaults(ModContent.NPCType<AvatarRift>());
        riftDummy.TopLeft = riftDrawPosition;
        riftDummy.scale = Lerp(0.99f, 1f, Cos01(Main.GlobalTimeWrappedHourly * 0.9f)) * Lerp(0.5f, 3.5f, settings.RiftSize) * intensity;
        riftDummy.As<AvatarRift>().BackgroundProp = true;
        riftDummy.As<AvatarRift>().TargetIdentifierOverride = -20;

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

        float oldRiftScaleFactor = RiftScaleFactor;
        RiftScaleFactor = 1f;
        riftDummy.As<AvatarRift>().PreDraw(Main.spriteBatch, Vector2.Zero, Color.White);
        RiftScaleFactor = oldRiftScaleFactor;

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, GetCustomSkyBackgroundMatrix());

        Color _ = default;
        ModifyTileAndSkyColors(ref _, ref Main.ColorOfTheSkies, intensity);
    }

    public override void SpecialVisuals(Player player, bool isActive)
    {
        player.ManageSpecialBiomeVisuals("NoxusBoss:NoxusRiftBackgroundSky", isActive);
    }
}

public class RiftEclipseSky : CustomSky
{
    private bool isActive;

    private float intensity;

    internal static NPC riftDummy;

    public static bool IsEnabled
    {
        get;
        set;
    }

    public static float RiftScaleFactor
    {
        get;
        set;
    }

    public static bool ReducedHorizontalOffset
    {
        get;
        set;
    }

    public static float MoveOverSunInterpolant
    {
        get;
        set;
    }

    public static float ScaleWhenOverSun => 3f;

    public static float ProgressionScaleFactor
    {
        get
        {
            float scaleFactor = 0.2f;
            if (CommonCalamityVariables.ProvidenceDefeated)
                scaleFactor = 0.35f;
            if (CommonCalamityVariables.DevourerOfGodsDefeated)
                scaleFactor = 0.55f;
            if (CommonCalamityVariables.YharonDefeated)
                scaleFactor = 0.7f;
            if (CommonCalamityVariables.DraedonDefeated || CommonCalamityVariables.CalamitasDefeated)
                scaleFactor = 1f;
            return scaleFactor;
        }
    }

    public override void Update(GameTime gameTime)
    {
        isActive = IsEnabled;

        // Make the intensity go up or down based on whether the sky is in use.
        if (AvatarRiftSky.intensity > 0f)
            intensity *= 0.9f;
        else
            intensity = Saturate(intensity + isActive.ToDirectionInt() * 0.01f);
    }

    public static void ModifyTileAndSkyColors(ref Color tileColor, ref Color backgroundColor, float intensity)
    {
        float bloodMoonIntensity = Filters.Scene["BloodMoon"].Opacity;
        if (GraphicalUniverseImagerSky.EclipseConfigOption == GraphicalUniverseImagerSettings.EclipseSecondaryAmbienceSetting.BloodRain)
            bloodMoonIntensity = ModContent.GetInstance<GraphicalUniverseImagerSky>().EffectiveIntensity;

        Color idealColor = Color.Lerp(new(171, 93, 157), new(123, 40, 70), bloodMoonIntensity);
        if (Main.eclipse)
            idealColor = Color.OrangeRed;

        float skyInfluenceFactor = 0.4f;
        if (!Main.dayTime)
            skyInfluenceFactor = InverseLerpBump(0f, 1200f, (float)Main.nightLength - 1200f, (float)Main.nightLength, (float)Main.time);

        backgroundColor = Color.Lerp(backgroundColor, Color.Black, Saturate(intensity * skyInfluenceFactor));

        tileColor = Color.Lerp(Color.Lerp(tileColor, idealColor, intensity * 0.4f), new(194, 51, 23), bloodMoonIntensity * 0.6f);
    }

    public static void ModifyTileAndSkyColorsForGUI(ref Color tileColor, ref Color backgroundColor)
    {
        ModifyTileAndSkyColors(ref tileColor, ref backgroundColor, ModContent.GetInstance<GraphicalUniverseImagerSky>().EffectiveIntensity * 5f);
    }

    public override Color OnTileColor(Color inColor)
    {
        ModifyTileAndSkyColors(ref inColor, ref Main.ColorOfTheSkies, intensity * RiftScaleFactor / ScaleWhenOverSun);
        return inColor;
    }

    public static void RenderFog(Matrix matrix, float intensity, float existingDarkness)
    {
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, matrix);

        Rectangle fogArea = new Rectangle(0, -300, Main.instance.GraphicsDevice.Viewport.Width * 2, Main.instance.GraphicsDevice.Viewport.Height + 420);
        Color fogColor = Main.bloodMoon ? new Color(139, 61, 52, 40) : Color.Gray * 0.8f;
        if (Main.eclipse)
            fogColor = new(60, 21, 29);

        // Weaken the fog as the player goes underground.
        intensity *= InverseLerp(3300f, 0f, Main.screenPosition.Y - (float)Main.worldSurface * 16f);

        ManagedShader shader = ShaderManager.GetShader("NoxusBoss.FogNoiseHelperShader");
        shader.TrySetParameter("screenUV", Main.screenPosition / Main.ScreenSize.ToVector2());
        shader.TrySetParameter("endY", 1f);
        shader.SetTexture(BurnNoise, 1, SamplerState.LinearWrap);
        shader.Apply();

        Main.spriteBatch.Draw(GennedAssets.Textures.Skies.EclipseFog.Value, fogArea, fogColor * intensity * (1f - existingDarkness) * 0.7f);

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, GetCustomSkyBackgroundMatrix());
    }

    public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth)
    {
        float bloomOpacity = Pow(1f - RiftScaleFactor / ScaleWhenOverSun, 50f);
        RealisticSkyCompatibility.SunBloomOpacity = bloomOpacity;
        if (!ModContent.GetInstance<PostMLRiftAppearanceSystem>().Ongoing)
        {
            RiftScaleFactor = ScaleWhenOverSun;
            RealisticSkyCompatibility.SunBloomOpacity = 0f;
        }
        RealisticSkyCompatibility.TemporarilyDisable();

        if (minDepth <= float.MinValue)
            RenderFog(GetCustomSkyBackgroundMatrix(), intensity * RiftScaleFactor / ScaleWhenOverSun, InverseLerp(0.1f, 0.4f, Main.ColorOfTheSkies.ToVector3().Length() / 1.732f));

        if (maxDepth < float.MaxValue || minDepth >= float.MaxValue)
            return;

        if (!IsEnabled)
            return;

        if (AvatarRift.Myself is not null || AvatarOfEmptiness.Myself is not null)
            return;

        // Calculate the position of the sun so that the Avatar can potentially eclipse it.
        Vector2 riftDrawPosition = new Vector2(Main.instance.GraphicsDevice.Viewport.Width * 0.5f, Main.instance.GraphicsDevice.Viewport.Height * 0.3f);
        Vector2 sunPosition = Main.dayTime ? SunMoonPositionRecorder.SunPosition : SunMoonPositionRecorder.MoonPosition;

        // Move towards the sun as instructed.
        riftDrawPosition = Vector2.Lerp(riftDrawPosition, sunPosition, MoveOverSunInterpolant);

        if (riftDummy is null)
        {
            riftDummy = new NPC();
            riftDummy.SetDefaults(ModContent.NPCType<AvatarRift>());
        }
        riftDummy.TopLeft = riftDrawPosition;
        riftDummy.scale = Lerp(0.99f, 1f, Cos01(Main.GlobalTimeWrappedHourly * 0.9f)) * RiftScaleFactor * ProgressionScaleFactor * RiftEclipseManagementSystem.RiftScale;
        riftDummy.As<AvatarRift>().BackgroundProp = true;
        riftDummy.As<AvatarRift>().TargetIdentifierOverride = -30;

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

        riftDummy.As<AvatarRift>().PreDraw(Main.spriteBatch, Vector2.Zero, Color.White);

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, SubtractiveBlending, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
        DrawBlackSpeck(riftDrawPosition);

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, GetCustomSkyBackgroundMatrix());
    }

    private static void DrawBlackSpeck(Vector2 speckDrawPosition)
    {
        float speckScaleInterpolant = InverseLerpBump(0f, 0.24f, 0.85f, 0.95f, RiftScaleFactor / ScaleWhenOverSun);
        float speckScale = SmoothStep(0f, 0.4f, speckScaleInterpolant);
        for (int i = 0; i < 25; i++)
            Main.spriteBatch.Draw(BloomCircleSmall, speckDrawPosition, null, Color.White, 0f, BloomCircleSmall.Size() * 0.5f, speckScale, 0, 0f);
    }

    public override void Activate(Vector2 position, params object[] args)
    {
        isActive = true;
    }

    public override void Deactivate(params object[] args)
    {
        isActive = false;
    }

    public override void Reset()
    {
        isActive = false;
    }

    public override bool IsActive()
    {
        return isActive || intensity > 0f;
    }
}
