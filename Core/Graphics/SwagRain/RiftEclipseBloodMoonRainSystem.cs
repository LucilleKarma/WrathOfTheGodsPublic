using System.Reflection;
using Luminance.Core.Graphics;
using Luminance.Core.Hooking;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using NoxusBoss.Assets;
using NoxusBoss.Core.Graphics.FastParticleSystems;
using NoxusBoss.Core.Graphics.UI.GraphicalUniverseImager;
using NoxusBoss.Core.SoundSystems;
using NoxusBoss.Core.World.GameScenes.RiftEclipse;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Graphics.SwagRain;

[Autoload(Side = ModSide.Client)]
public class RiftEclipseBloodMoonRainSystem : ModSystem
{
    /// <summary>
    /// Whether the blood rain is currently in effect.
    /// </summary>
    public static bool EffectActive => Main.bloodMoon && RiftEclipseManagementSystem.RiftEclipseOngoing;

    /// <summary>
    /// Whether the blood rain is currently in effect.
    /// </summary>
    public static bool MonolithEffectActive
    {
        get
        {
            if (!ModContent.GetInstance<GraphicalUniverseImagerSky>().IsActive)
                return false;

            return GraphicalUniverseImagerSky.EclipseConfigOption == GraphicalUniverseImagerSettings.EclipseSecondaryAmbienceSetting.BloodRain;
        }
    }

    /// <summary>
    /// The particle responsible for the handling of rain.
    /// </summary>
    public static FastParticleSystem RainParticleSystem
    {
        get;
        private set;
    }

    /// <summary>
    /// The ambient sound style loop instance.
    /// </summary>
    public static LoopedSoundInstance AmbienceLoopSoundInstance
    {
        get;
        private set;
    }

    /// <summary>
    /// The blood water texture.
    /// </summary>
    public static LazyAsset<Texture2D> BloodWaterTexture
    {
        get;
        private set;
    }

    /// <summary>
    /// The ambient sound style loop.
    /// </summary>
    public static readonly SoundStyle AmbienceLoopSound = new SoundStyle("NoxusBoss/Assets/Sounds/Custom/Environment/BloodEclipseRainLoop");

    public override void OnModLoad()
    {
        BloodWaterTexture = LazyAsset<Texture2D>.FromPath(GetAssetPath("Extra", "BloodWater"));
        RainParticleSystem = FastParticleSystemManager.CreateNew(16384, PrepareParticleRendering, ExtraParticleUpdates);

        On_Main.DrawRain += RenderRain;

        new ManagedILEdit("Draw Blood Water Shader", Mod, edit =>
        {
            IL_Main.DoDraw += edit.SubscriptionWrapper;
        }, edit =>
        {
            IL_Main.DoDraw -= edit.SubscriptionWrapper;
        }, DrawBloodOnTopOfWaterWrapper).Apply(true);
    }

    private void RenderRain(On_Main.orig_DrawRain orig, Main self)
    {
        orig(self);

        if (RainParticleSystem.particles.Any(p => p.Active))
            RainParticleSystem.RenderAll();
    }

    private static void PrepareParticleRendering()
    {
        Matrix world = Matrix.CreateTranslation(-Main.screenPosition.X, -Main.screenPosition.Y, 0f);
        Matrix projection = Matrix.CreateOrthographicOffCenter(0f, Main.screenWidth, Main.screenHeight, 0f, -100f, 100f);

        ManagedShader rainShader = ShaderManager.GetShader("NoxusBoss.RainShader");
        rainShader.TrySetParameter("projection", world * Main.GameViewMatrix.TransformationMatrix * projection);
        rainShader.Apply();
    }

    private static void ExtraParticleUpdates(ref FastParticle particle)
    {
        float maxSpeed = Abs(Main.windSpeedCurrent) * 37f + 54f;
        particle.Velocity = (particle.Velocity * 1.06f + particle.Velocity.SafeNormalize(Vector2.Zero) * 3.5f).ClampLength(0f, maxSpeed);

        // Clear particles that go considerably below the screen, as a form of natural culling.
        if (particle.Position.Y >= Main.screenPosition.Y + Main.screenHeight + 400f)
            particle.Active = false;

        // Clear particles that interact with tiles.
        Vector2 bottom = particle.Position + particle.Rotation.ToRotationVector2() * particle.Size.Y * 0.5f + particle.Velocity * 1.2f;
        Point bottomPoint = bottom.ToTileCoordinates();

        if (WorldGen.InWorld(bottomPoint.X, bottomPoint.Y))
        {
            Tile checkTile = Main.tile[bottomPoint];
            if (checkTile.HasUnactuatedTile && Main.tileSolid[checkTile.TileType] || checkTile.LiquidAmount >= 128)
                particle.Active = false;
        }
    }

    private static void DrawBloodOnTopOfWaterWrapper(ILContext context, ManagedILEdit edit)
    {
        ILCursor cursor = new ILCursor(context);

        // Collect member information via reflection for the searching process.
        MethodInfo? whiteGetter = typeof(Color).GetProperty("White")?.GetMethod ?? null;
        MethodInfo? basicSpritebatchDraw = typeof(SpriteBatch).GetMethod("Draw", new Type[] { typeof(Texture2D), typeof(Vector2), typeof(Color) });

        if (whiteGetter is null)
        {
            edit.LogFailure("The Color.White getter method could not be found (somehow).");
            return;
        }
        if (basicSpritebatchDraw is null)
        {
            edit.LogFailure("The SpriteBatch.Draw method could not be found.");
            return;
        }

        if (!cursor.TryGotoNext(MoveType.After, i => i.MatchLdfld<Main>("backWaterTarget")))
        {
            edit.LogFailure("The Main.instance.backWaterTarget field load could not be found.");
            return;
        }

        // Move after Color.White, with the intent of replacing it with complete transparency if the water type is special.
        if (!cursor.TryGotoNext(MoveType.After, i => i.MatchCall(whiteGetter)))
        {
            edit.LogFailure("The Color.White property load could not be found.");
            return;
        }
        cursor.EmitDelegate<Func<Color, Color>>(originalColor =>
        {
            if (!EffectActive && !MonolithEffectActive)
                return originalColor;

            return Color.Transparent;
        });

        if (!cursor.TryGotoNext(MoveType.After, i => i.MatchLdsfld<Main>("waterTarget")))
        {
            edit.LogFailure("The Main.waterTarget field load could not be found.");
            return;
        }

        // Move after Color.White, with the intent of replacing it with complete transparency if the water type is special.
        if (!cursor.TryGotoNext(MoveType.After, i => i.MatchCall(whiteGetter)))
        {
            edit.LogFailure("The Color.White property load could not be found.");
            return;
        }
        cursor.EmitDelegate<Func<Color, Color>>(originalColor =>
        {
            if (!EffectActive && !MonolithEffectActive)
                return originalColor;

            return Color.Transparent;
        });

        // Move after the draw call.
        if (!cursor.TryGotoNext(MoveType.After, i => i.MatchCallOrCallvirt(basicSpritebatchDraw)))
        {
            edit.LogFailure("The Main.spriteBatch.Draw(Texture2D, Vector2, Color) call load could not be found.");
            return;
        }

        cursor.EmitDelegate(DrawBloodOnTopOfWater);
    }

    private static void DrawBloodOnTopOfWater()
    {
        if (!EffectActive && !MonolithEffectActive)
            return;

        // Reset the water render target if it's disposed for any reason. This can happen most notably if the game's screen recently got resized.
        if (Main.waterTarget.IsDisposed)
        {
            int width = Main.instance.GraphicsDevice.PresentationParameters.BackBufferWidth;
            int height = Main.instance.GraphicsDevice.PresentationParameters.BackBufferHeight;
            width += Main.offScreenRange * 2;
            height += Main.offScreenRange * 2;
            Main.waterTarget = new RenderTarget2D(Main.instance.GraphicsDevice, width, height, false, Main.instance.GraphicsDevice.PresentationParameters.BackBufferFormat, DepthFormat.None);
            return;
        }

        // Prepare for shader drawing.
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

        // Get and prepare the shader.
        Vector2 scenePosition = Main.sceneWaterPos;
        ManagedShader waterShader = ShaderManager.GetShader("NoxusBoss.RiftEclipseBloodWaterShader");
        waterShader.TrySetParameter("screenPosition", -scenePosition);
        waterShader.TrySetParameter("targetSize", Main.waterTarget.Size());
        waterShader.SetTexture(BloodWaterTexture.Value, 1);
        waterShader.SetTexture(TurbulentNoise, 2);
        waterShader.Apply();

        // Draw the water target.
        Main.spriteBatch.Draw(Main.waterTarget, scenePosition - Main.screenPosition, Color.White);

        Main.spriteBatch.ResetToDefault();
    }

    public override void OnWorldLoad() => AmbienceLoopSoundInstance?.Stop();

    public override void OnWorldUnload() => AmbienceLoopSoundInstance?.Stop();

    public override void PreUpdateEntities()
    {
        RainParticleSystem.UpdateAll();
        AmbienceLoopSoundInstance?.Update(Main.LocalPlayer.Center);

        bool effectActive = EffectActive || MonolithEffectActive;
        if (!effectActive || Main.screenPosition.Y >= Main.worldSurface * 16f)
        {
            AmbienceLoopSoundInstance?.Stop();
            return;
        }

        if ((AmbienceLoopSoundInstance is null || AmbienceLoopSoundInstance.HasBeenStopped) && EffectActive)
            AmbienceLoopSoundInstance = new(AmbienceLoopSound, () => !EffectActive || Main.screenPosition.Y >= Main.worldSurface * 16f);

        Vector2 rainSize = new Vector2(1f, 32f);
        Vector2 rainDirection = Vector2.UnitY.RotatedBy(Main.windSpeedCurrent * 0.56f);
        for (int i = 0; i < 175; i++)
        {
            Color rainColor = Color.Crimson * Main.rand.NextFloat(0.4f);
            Vector2 rainSpawnPosition = Main.screenPosition + new Vector2(Main.rand.NextFloat(-0.35f, 1.35f) * Main.screenWidth, -400f);
            Vector2 rainVelocity = rainDirection * Main.rand.NextFloat(10.5f, 19.5f);
            RainParticleSystem.CreateNew(rainSpawnPosition, rainVelocity, rainSize, rainColor);
        }
    }
}
