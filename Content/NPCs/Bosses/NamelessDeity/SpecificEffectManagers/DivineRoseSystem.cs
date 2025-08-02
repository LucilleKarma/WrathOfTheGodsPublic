using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Core.DataStructures;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;

public class DivineRoseSystem : ModSystem
{
    public class Galaxy
    {
        public int Variant;

        public int Time;

        public int Lifetime;

        public float Eccentricity;

        public float Rotation;

        public float SpinRotation;

        public float GeneralScale;

        public Color GeneralColor;

        public Vector2 Center;

        public Vector2 Velocity;

        public float LifetimeCompletion => Time / (float)Lifetime;

        public float Scale => InverseLerp(0f, 6f, Time) * Lerp(1f, 1.5f, LifetimeCompletion) * GeneralScale;

        public float Opacity => InverseLerp(0f, 9f, Time) * InverseLerp(1f, 0.4f, Pow(LifetimeCompletion, 0.6f));

        public void Update()
        {
            Time++;
            Center += Velocity;
            Velocity = (Velocity * 0.97f).ClampLength(0f, 40f);
            Rotation += Velocity.X * 0.003f;
            SpinRotation += Velocity.X * 0.04f;
        }
    }

    public static LazyAsset<Texture2D>[] GalaxyTextures
    {
        get;
        private set;
    }

    public static int FrameIncrementRate => 8;

    public static int FrameCount => 26;

    public static int CensorStartingFrame => 14;

    public static int BlackOverlayStartTime => FrameIncrementRate * CensorStartingFrame;

    public static int AttackDelay => FrameIncrementRate * FrameCount + 54;

    public static int ExplosionDelay => AttackDelay + 30;

    public static float NamelessDeityZPosition => 7.5f;

    public static float HorizontalRoseOffset => (Main.screenPosition.X / Main.maxTilesX / 16f - 0.5f) * -400f;

    public static Vector2 RoseOffsetFromScreenCenter => -Vector2.UnitY * 300f;

    public static Vector2 BaseCensorOffset => -Vector2.UnitY * 100f;

    public static readonly List<Galaxy> ActiveGalaxies = [];

    /// <summary>
    /// The palette that galaxies spawned by the rose can cycle through.
    /// </summary>
    public static readonly Palette GalaxyPalette = new Palette().
        AddColor(Color.OrangeRed).
        AddColor(Color.Coral).
        AddColor(Color.HotPink).
        AddColor(Color.Magenta).
        AddColor(Color.DarkViolet).
        AddColor(Color.Cyan).
        AddColor(Color.White);

    public override void OnModLoad()
    {
        if (Main.netMode == NetmodeID.Server)
            return;

        GalaxyTextures = [GennedAssets.Textures.NamelessDeity.Galaxy1, GennedAssets.Textures.NamelessDeity.Galaxy2, GennedAssets.Textures.NamelessDeity.Galaxy3,
                          GennedAssets.Textures.NamelessDeity.Galaxy4, GennedAssets.Textures.NamelessDeity.Galaxy5, GennedAssets.Textures.NamelessDeity.Galaxy6,
                          GennedAssets.Textures.NamelessDeity.Galaxy7, GennedAssets.Textures.NamelessDeity.Galaxy8, GennedAssets.Textures.NamelessDeity.Galaxy9];
        Main.OnPostDraw += DrawCensor;
    }

    public override void OnModUnload() => Main.OnPostDraw -= DrawCensor;

    public static List<Vector2> GenerateGodRayPositions(Vector2 roseDrawPosition, Vector2 maxLength)
    {
        List<Vector2> godRayPositions = [];
        for (int i = 0; i < 20; i++)
            godRayPositions.Add(roseDrawPosition + maxLength * i / 19f);

        return godRayPositions;
    }

    public static void Draw(int time)
    {
        // Calculate draw variables.
        float scale = Utils.Remap(time, -1f, 75f, 0.01f, 1.6f);

        // Draw the rose.
        float generalOpacity = InverseLerp(1f, NamelessDeityZPosition - 1f, NamelessDeityBoss.Myself.As<NamelessDeityBoss>().ZPosition);
        Vector2 roseDrawPosition = ViewportSize * 0.5f + RoseOffsetFromScreenCenter;
        roseDrawPosition += Vector2.UnitX * HorizontalRoseOffset * Main.instance.GraphicsDevice.Viewport.Width / Main.instance.GraphicsDevice.DisplayMode.Width;

        DrawRose(time, generalOpacity, scale, roseDrawPosition);

        // Draw the glow effect when ready.
        float glowAnimationCompletion = InverseLerp(BlackOverlayStartTime, AttackDelay - 20f, time);
        if (glowAnimationCompletion > 0f && glowAnimationCompletion < 1f)
        {
            if (time == BlackOverlayStartTime + 1 && !Main.gamePaused)
                SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.StarConvergenceFast with { Volume = 0.8f });

            // Draw the colored bloom flares.
            float bloomFlareRotation = SmoothStep(0f, Pi, glowAnimationCompletion);
            float bloomFlareOpacity = Convert01To010(glowAnimationCompletion) * generalOpacity;
            for (int i = 0; i < 6; i++)
            {
                Color bloomFlareColor = Main.hslToRgb((i / 6f + glowAnimationCompletion * 0.84f) % 1f, 0.9f, 0.56f) * bloomFlareOpacity;
                bloomFlareColor.A = 0;
                Main.spriteBatch.Draw(BloomFlare, roseDrawPosition + BaseCensorOffset, null, bloomFlareColor, bloomFlareRotation + TwoPi * i / 6f, BloomFlare.Size() * 0.5f, (bloomFlareOpacity * 0.64f + i * 0.04f), 0, 0f);
            }

            // Draw the bloom glow.
            Main.spriteBatch.Draw(BloomCircle, roseDrawPosition + BaseCensorOffset, null, (Color.Wheat with { A = 0 }) * bloomFlareOpacity * 0.4f, bloomFlareRotation, BloomCircle.Size() * 0.5f, bloomFlareOpacity * 0.2f, 0, 0f);
            Main.spriteBatch.Draw(BloomCircle, roseDrawPosition + BaseCensorOffset, null, (Color.IndianRed with { A = 0 }) * bloomFlareOpacity * 0.1f, bloomFlareRotation, BloomCircle.Size() * 0.5f, bloomFlareOpacity * 0.5f, 0, 0f);
        }

        // Summon galaxies upward if the explosion has happened.
        if (time >= ExplosionDelay && generalOpacity >= 1f)
        {
            Color galaxyColor = GalaxyPalette.SampleColor(Main.rand.NextFloat().Squared() * 0.94f) * 1.9f;
            galaxyColor = Color.Lerp(galaxyColor, Color.Wheat, 0.55f);

            Vector2 galaxyVelocity = -Vector2.UnitY.RotatedByRandom(0.5f) * Main.rand.NextFloat(11f, 25f) + Main.rand.NextVector2Circular(5f, 5f);
            int galaxyLifetime = (int)Utils.Remap(galaxyVelocity.Length(), 10f, 19.2f, 40f, 70f) + Main.rand.Next(-12, 45);
            float galaxyScale = Utils.Remap(galaxyVelocity.Length(), 9.5f, 17.4f, 0.12f, 0.4f) + Main.rand.NextFloat().Cubed() * 0.8f;

            ActiveGalaxies.Add(new Galaxy()
            {
                Center = roseDrawPosition + BaseCensorOffset,
                Velocity = galaxyVelocity,
                GeneralColor = galaxyColor,
                Lifetime = galaxyLifetime,
                Rotation = Main.rand.NextFloat(TwoPi),
                GeneralScale = galaxyScale,
                Eccentricity = Main.rand.NextFloat(),
                Variant = Main.rand.Next(9)
            });
        }

        // Draw galaxies, along with a pulsating white behind the censor.
        if (time >= ExplosionDelay && ActiveGalaxies.Count != 0)
        {
            // Draw the white pulse.
            float pulseScale = Lerp(2.2f, 2.6f, Sin01(Main.GlobalTimeWrappedHourly * 55f)) * 0.64f;
            for (int i = 0; i < 4; i++)
            {
                Main.spriteBatch.Draw(BloomCircleSmall, roseDrawPosition + BaseCensorOffset, null, (Color.Wheat with { A = 0 }) * generalOpacity, 0f, BloomCircleSmall.Size() * 0.5f, pulseScale, 0, 0f);
                Main.spriteBatch.Draw(BloomCircleSmall, roseDrawPosition + BaseCensorOffset, null, (Color.Wheat with { A = 0 }) * generalOpacity * 0.7f, 0f, BloomCircleSmall.Size() * 0.5f, pulseScale * 1.5f, 0, 0f);
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.AnisotropicWrap, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
            DrawGalaxies();
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
        }
    }

    public static void DrawCensor(GameTime obj)
    {
        if (NamelessDeityBoss.Myself is null || NamelessDeityBoss.Myself.As<NamelessDeityBoss>().CurrentState != NamelessDeityBoss.NamelessAIType.MomentOfCreation)
            return;
        if (Main.mapFullscreen)
            return;

        int time = NamelessDeityBoss.Myself.As<NamelessDeityBoss>().AITimer;

        // Draw the censor over the rose when ready.
        float generalOpacity = InverseLerp(1f, NamelessDeityZPosition - 1f, NamelessDeityBoss.Myself.As<NamelessDeityBoss>().ZPosition);
        if (time >= BlackOverlayStartTime)
        {
            Vector2 censorPosition = ViewportSize * 0.5f + RoseOffsetFromScreenCenter;
            censorPosition.X += HorizontalRoseOffset;

            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicWrap, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

            // Apply a static shader.
            ManagedShader censorShader = ShaderManager.GetShader("NoxusBoss.StaticOverlayShader");
            censorShader.SetTexture(MulticoloredNoise, 1, SamplerState.PointWrap);
            censorShader.Apply();

            ulong offsetSeed = (ulong)(time / 4) << 10;
            float offsetAngle = Utils.RandomFloat(ref offsetSeed) * 1000f;
            Vector2 censorOffset = BaseCensorOffset + offsetAngle.ToRotationVector2() * 4f;

            Main.spriteBatch.Draw(WhitePixel, censorPosition + censorOffset, null, Color.Black * generalOpacity, 0f, WhitePixel.Size() * 0.5f, 156f, 0, 0f);

            Main.spriteBatch.End();
        }
    }

    public static void DrawRose(int time, float generalOpacity, float scale, Vector2 roseDrawPosition)
    {
        Texture2D rose = GennedAssets.Textures.NamelessDeity.DivineRose.Value;
        int frameY = Utils.Clamp(time / FrameIncrementRate, 0, FrameCount - 1);
        Rectangle frame = rose.Frame(1, FrameCount, 0, frameY);
        Vector2 origin = frame.Size() * 0.5f;
        Color roseColor = Color.Lerp(Color.DarkGray, Color.White, frameY / (float)FrameCount) * generalOpacity;

        // Draw the rose.
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.AnisotropicWrap, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

        Main.spriteBatch.Draw(rose, roseDrawPosition, frame, roseColor, 0.14f, origin, scale, 0, 0f);
        if (time >= ExplosionDelay)
        {
            float rosePulse = Main.GlobalTimeWrappedHourly * 5f % 1f;
            Main.spriteBatch.Draw(rose, roseDrawPosition, frame, roseColor * (1f - rosePulse), 0.14f, origin, scale * (1f + rosePulse * 0.2f), 0, 0f);
        }

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
    }

    public static void DrawGalaxies()
    {
        ManagedShader galaxyShader = ShaderManager.GetShader("NoxusBoss.GalaxyShader");

        ActiveGalaxies.RemoveAll(g => g.Time >= g.Lifetime);
        foreach (Galaxy g in ActiveGalaxies)
        {
            g.Update();

            Matrix flatScale = new Matrix()
            {
                M11 = Lerp(1f, 2.3f, g.Eccentricity),
                M12 = 0f,
                M21 = 0f,
                M22 = 1f
            };
            Matrix spinInPlaceRotation = Matrix.CreateRotationZ(g.SpinRotation);
            Matrix orientationRotation = Matrix.CreateRotationZ(g.Rotation);
            galaxyShader.TrySetParameter("transformation", orientationRotation * flatScale * spinInPlaceRotation);
            galaxyShader.TrySetParameter("time", Main.GlobalTimeWrappedHourly);
            galaxyShader.Apply();

            // Draw the main galaxy.
            float galaxyScale = g.Scale * 1.3f + Pow(g.LifetimeCompletion, 1.7f) * 19f;
            Texture2D galaxyTexture = GalaxyTextures[g.Variant];
            Vector2 galaxyDrawPosition = g.Center;
            Main.spriteBatch.Draw(galaxyTexture, galaxyDrawPosition, null, g.GeneralColor * g.Opacity, 0f, galaxyTexture.Size() * 0.5f, galaxyScale, 0, 0f);

            // Draw a secondary galaxy on top with some color contrast.
            Color secondaryGalaxyColor = Main.hslToRgb((Main.GlobalTimeWrappedHourly * 0.6f + g.LifetimeCompletion * 0.3f) % 1f, 0.8f, 0.6f) * 0.4f;
            Main.spriteBatch.Draw(galaxyTexture, galaxyDrawPosition, null, secondaryGalaxyColor * g.Opacity * 0.6f, 0f, galaxyTexture.Size() * 0.5f, galaxyScale * 1.1f, 0, 0f);
        }
    }
}
