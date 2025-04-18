using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Content.NPCs.Bosses.Avatar;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers;
using NoxusBoss.Core.Graphics.UI.GraphicalUniverseImager;
using NoxusBoss.Core.World.TileDisabling;
using ReLogic.Content;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace NoxusBoss.Core.World.GameScenes.RiftEclipse;

public class RiftEclipseFogShaderScene : ModSceneEffect
{
    public override bool IsSceneEffectActive(Player player) => RiftEclipseFogEventManager.FogDrawIntensity >= 0.001f || RiftEclipseFogShaderData.FogDrawIntensityOverride >= 0.001f;

    public override void SpecialVisuals(Player player, bool isActive)
    {
        player.ManageSpecialBiomeVisuals("NoxusBoss:NoxusWorldFog", isActive);
        if (GraphicalUniverseImagerSky.EclipseConfigOption == GraphicalUniverseImagerSettings.EclipseSecondaryAmbienceSetting.Fog)
            RiftEclipseFogShaderData.FogDrawIntensityOverride = Lerp(RiftEclipseFogShaderData.FogDrawIntensityOverride, 1f, 0.11f);
    }
}

public class RiftEclipseFogShaderData(Asset<Effect> shader, string passName) : ScreenShaderData(shader, passName)
{
    private static readonly float[] smoothBrightnesses = new float[2];

    private static readonly Vector2[] sourcePositions = new Vector2[2];

    /// <summary>
    /// An overridable value for the fog intensity.
    /// </summary>
    public static float FogDrawIntensityOverride
    {
        get;
        set;
    }

    public override void Update(GameTime gameTime)
    {
        UseOpacity(0.001f);
    }

    public override void Apply()
    {
        Color fogColor = new Color(79, 102, 122);
        float belowSurfaceFadeout = InverseLerp((float)Main.worldSurface + 50f, (float)Main.worldSurface - 10f, Main.LocalPlayer.Center.Y / 16f);
        if (TileDisablingSystem.TilesAreUninteractable)
            belowSurfaceFadeout = 1f;
        if (AvatarOfEmptinessSky.Dimension == AvatarDimensionVariants.FogDimension)
            fogColor.B += 50;

        Main.instance.GraphicsDevice.Textures[1] = SmudgeNoise;
        Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
        UseColor(fogColor);
        UseIntensity(MathF.Max(RiftEclipseFogEventManager.FogDrawIntensity, FogDrawIntensityOverride) * belowSurfaceFadeout);

        // Calculate source positions. If a dismal seeker is present, it counts as a separate source.
        sourcePositions[0] = Main.LocalPlayer.Center;

        // Calculate brightness values for each source position.
        for (int i = 0; i < sourcePositions.Length; i++)
        {
            Vector2 sourcePosition = sourcePositions[i];
            float brightness = Pow(Clamp(Lighting.Brightness((int)(sourcePosition.X / 16f), (int)(sourcePosition.Y / 16f)), 0f, 1.2f), 2f);
            if (AvatarOfEmptiness.Myself is not null)
                brightness = 0.0001f;

            // Smoothly interpolate towards the desired brightness.
            smoothBrightnesses[i] = Lerp(smoothBrightnesses[i], brightness, 0.1f);
            if (brightness < smoothBrightnesses[i])
                smoothBrightnesses[i] = Clamp(smoothBrightnesses[i] - 0.01f, 0f, 10f);
        }

        Shader.Parameters["centerBrightnesses"]?.SetValue(smoothBrightnesses);
        Shader.Parameters["sourcePositions"]?.SetValue(sourcePositions.Select(WorldSpaceToScreenUV).ToArray());
        Shader.Parameters["windColor1"]?.SetValue(new Color(16, 191, 255).ToVector4());
        Shader.Parameters["windColor2"]?.SetValue(new Color(255, 11, 74).ToVector4());

        // Reset the intensity override.
        if (!Main.gamePaused)
            FogDrawIntensityOverride = Saturate(FogDrawIntensityOverride - 0.01f);

        base.Apply();
    }
}
