using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.FirstPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Core.DataStructures;
using NoxusBoss.Core.GlobalInstances;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using NoxusBoss.Core.SoundSystems;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers;

public class AvatarRiftSuckVisualsManager : ModSystem
{
    public class ImpactParticle
    {
        public Vector2 DrawPosition;

        public float Rotation;

        public int Time;

        public int Lifetime;

        public int Variant;

        public float Scale;

        public void Update()
        {
            Time++;

            float lifeRatio = Time / (float)Lifetime;
            Scale = Pow(Convert01To010(lifeRatio), 7f) * Lerp(0.8f, 1.1f, Sin01(Time / 9f));
            Scale *= Utils.Remap(Lifetime, 22f, 45f, 0.15f, 0.8f);

            Rotation += 0.05f;
        }
    }

    public class RubbleParticle
    {
        public int FrameX;

        public int FrameY;

        public float Rotation;

        public Vector2 Velocity;

        public Vector2 DrawPosition;

        public float Scale;

        public void Update()
        {
            Velocity *= 1f + Scale * 0.02f;
            DrawPosition += Velocity;
            Rotation -= Velocity.Y / Scale * 0.01f;
        }
    }

    private static bool drawingAfterEverything;

    public static LoopedSoundInstance AmbienceLoop
    {
        get;
        private set;
    }

    public static List<ImpactParticle> ImpactParticles
    {
        get;
        private set;
    } = [];

    public static List<RubbleParticle> RubbleParticles
    {
        get;
        private set;
    } = [];

    public const string WasSuckedInVariableName = "WasSuckedIntoNoxusPortal";

    public const string ZoomInInterpolantName = "NoxusPortalZoomInInterpolant";

    public static Referenced<float> ZoomInInterpolant => Main.LocalPlayer.GetValueRef<float>(ZoomInInterpolantName);

    public static bool WasSuckedIntoNoxusPortal => Main.LocalPlayer.GetValueRef<bool>(WasSuckedInVariableName);

    public static NPC? Avatar
    {
        get
        {
            if (AvatarOfEmptiness.Myself is not null)
                return AvatarOfEmptiness.Myself;

            int riftIndex = NPC.FindFirstNPC(ModContent.NPCType<AvatarRift>());
            if (riftIndex != -1)
                return Main.npc[riftIndex];

            return null;
        }
    }

    public override void OnModLoad()
    {
        Main.OnPostDraw += DrawPortalDimension;
        PlayerDataManager.GetAlphaEvent += MakePlayersDisappear;
    }

    public override void OnModUnload()
    {
        Main.OnPostDraw -= DrawPortalDimension;
        PlayerDataManager.GetAlphaEvent -= MakePlayersDisappear;
    }

    private void MakePlayersDisappear(PlayerDataManager p, ref Color drawColor)
    {
        if (p.Player.GetValueRef<bool>(WasSuckedInVariableName))
            drawColor = drawingAfterEverything ? new Color(1f, 0f, 0.3f, 1f) * 0.4f : Color.Transparent;
        drawColor = drawColor.MultiplyRGBA(Color.Lerp(Color.White, Color.Black, InverseLerp(0.3f, 0.8f, ZoomInInterpolant)));
    }

    private void DrawPortalDimension(GameTime obj)
    {
        RubbleParticles.RemoveAll(p => !p.DrawPosition.Between(Vector2.One * -1500f, Main.ScreenSize.ToVector2() + Vector2.One * 1500f));
        foreach (RubbleParticle particle in RubbleParticles)
            particle.Update();

        if (Main.rand.NextBool(4))
            AddNewRubble(Main.rand.Next(3), Main.rand.Next(2));

        if (!Main.gameMenu && ZoomInInterpolant > 0f)
        {
            if (!Main.gamePaused)
                ZoomInInterpolant.Value = Saturate(ZoomInInterpolant + 0.025f);
            if (ZoomInInterpolant >= 0.67f)
            {
                TotalScreenOverlaySystem.OverlayColor = Color.Black;
                TotalScreenOverlaySystem.OverlayInterpolant = MathF.Max(TotalScreenOverlaySystem.OverlayInterpolant, InverseLerp(0.67f, 1f, ZoomInInterpolant) * 1.7f);
            }

            if (ZoomInInterpolant >= 1f)
            {
                if (AvatarOfEmptiness.Myself is null || AvatarOfEmptiness.Myself.As<AvatarOfEmptiness>().CurrentState != AvatarOfEmptiness.AvatarAIType.SendPlayerToMyUniverse)
                    ZoomInInterpolant.Value = 0f;

                if (AvatarOfEmptiness.Myself is not null && AvatarOfEmptiness.Myself.As<AvatarOfEmptiness>().CurrentState != AvatarOfEmptiness.AvatarAIType.SendPlayerToMyUniverse)
                    Main.LocalPlayer.GetValueRef<bool>(WasSuckedInVariableName).Value = false;
            }

            ManagedScreenFilter blurShader = ShaderManager.GetFilter("NoxusBoss.RadialMotionBlurShader");
            blurShader.TrySetParameter("blurIntensity", Sqrt(ZoomInInterpolant.Value));
            blurShader.Activate();

            CameraPanSystem.ZoomIn(ZoomInInterpolant.Value.Cubed() * 10f);
        }

        // Don't bother doing anything if the sucked in effect is not active.
        if (Main.gameMenu || !WasSuckedIntoNoxusPortal || ZoomInInterpolant != 0f)
        {
            ImpactParticles.Clear();
            return;
        }

        // Ensure that the player is not marked as being sucked in if they're dead or if the Avatar isn't even present.
        if (Main.LocalPlayer.dead || Avatar is null)
        {
            Main.LocalPlayer.GetValueRef<bool>(WasSuckedInVariableName).Value = false;
            return;
        }

        // Update ambience sounds.
        UpdateAmbienceSounds();

        // Prepare for shader drawing.
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

        // Draw chaos.
        Vector2 screenArea = ViewportSize;
        Vector2 textureArea = screenArea / WhitePixel.Size() * 2f;
        Main.spriteBatch.Draw(WhitePixel, screenArea * 0.5f, null, Color.Black, 9f, WhitePixel.Size() * 0.5f, textureArea, 0, 0f);
        DrawChaosRealm(0f, 2f);
        DrawChaosRealm(-0.3f, 0.9f);
        Main.spriteBatch.ResetToDefault();

        // Update and draw particles.
        DrawRubble();
        UpdateAndDrawImpactParticles();

        // Draw the player above everything.
        drawingAfterEverything = true;

        float hoverOffset = Sin(Main.GlobalTimeWrappedHourly * 12f) * 40f;
        float jitterRotation = Sin(Main.GlobalTimeWrappedHourly * 9.6f) * 0.3f;
        Main.PlayerRenderer.DrawPlayer(Main.Camera, Main.LocalPlayer, Main.LocalPlayer.TopLeft + Vector2.UnitY * hoverOffset + Main.rand.NextVector2Circular(30f, 30f), Main.LocalPlayer.fullRotation + jitterRotation, Main.LocalPlayer.fullRotationOrigin);
        drawingAfterEverything = false;

        // Flush the contents of the sprite batch.
        Main.spriteBatch.End();
    }

    public static void UpdateAmbienceSounds()
    {
        if (AmbienceLoop is null || AmbienceLoop.HasBeenStopped)
        {
            AmbienceLoop = LoopedSoundManager.CreateNew(GennedAssets.Sounds.Avatar.Ambience, () =>
            {
                return Main.gameMenu || !WasSuckedIntoNoxusPortal;
            });
        }
        AmbienceLoop.Update(Main.LocalPlayer.Center, sound =>
        {
            float idealVolume = 7f;
            if (sound.Volume != idealVolume)
                sound.Volume = idealVolume;
        });
    }

    public static void UpdateAndDrawImpactParticles()
    {
        // Remove all dead impact particles.
        ImpactParticles.RemoveAll(p => p.Time >= p.Lifetime);

        for (int i = 0; i < 3; i++)
        {
            if (Main.rand.NextBool())
            {
                ImpactParticles.Add(new ImpactParticle()
                {
                    DrawPosition = new(Main.rand.NextFloat(100f, Main.screenWidth - 100f), Main.rand.NextFloat(100f, Main.screenHeight - 100f)),
                    Lifetime = Main.rand.Next(22, 45),
                    Rotation = Main.rand.NextFloat(TwoPi),
                    Variant = Main.rand.Next(2)
                });

                if (Main.rand.NextBool(4))
                    SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.Explosion with { MaxInstances = 50, PitchVariance = 0.3f, Volume = 0.2f });
            }
        }

        // Draw and update particles.
        Texture2D texture = RadialFlare.Value;
        foreach (ImpactParticle particle in ImpactParticles)
        {
            particle.Update();
            Vector2 jitter = Main.rand.NextVector2Circular(5f, 5f);

            Rectangle frame = texture.Frame(1, 2, 0, particle.Variant);
            Main.spriteBatch.Draw(texture, particle.DrawPosition + jitter, frame, Color.Violet with { A = 0 }, particle.Rotation, frame.Size() * 0.5f, particle.Scale * 0.4f, 0, 0f);
            Main.spriteBatch.Draw(BloomCircleSmall, particle.DrawPosition + jitter, null, Color.Wheat with { A = 0 }, particle.Rotation, BloomCircleSmall.Size() * 0.5f, particle.Scale * 1.7f, 0, 0f);
            Main.spriteBatch.Draw(BloomCircleSmall, particle.DrawPosition + jitter, null, Color.MediumPurple with { A = 0 }, particle.Rotation, BloomCircleSmall.Size() * 0.5f, particle.Scale * 0.85f, 0, 0f);
        }
    }

    public static void DrawRubble()
    {
        // Draw and update particles.
        Texture2D texture = TextureAssets.Projectile[ModContent.ProjectileType<AcceleratingRubble>()].Value;
        foreach (RubbleParticle particle in RubbleParticles)
        {
            Rectangle frame = texture.Frame(3, 3, particle.FrameX, particle.FrameY);
            Main.spriteBatch.Draw(texture, particle.DrawPosition, frame, new(255, 124, 134), particle.Rotation, frame.Size() * 0.5f, particle.Scale * 2f, 0, 0f);
        }
    }

    public static void AddNewRubble(int frameX, int frameY)
    {
        float rubbleFlyAngle = Main.rand.NextFloat(-0.5f, 0.5f);
        Vector2 rubbleSpawnPosition = new Vector2(Main.rand.NextFloat(-1100f, -200f), Main.rand.NextFloat(-1100f, Main.screenHeight + 1100f)).RotatedBy(rubbleFlyAngle);

        RubbleParticles.Add(new RubbleParticle()
        {
            DrawPosition = rubbleSpawnPosition,
            Rotation = Main.rand.NextFloat(TwoPi),
            Velocity = new Vector2(7f, -3f).RotatedBy(rubbleFlyAngle),
            FrameX = frameX,
            FrameY = frameY,
            Scale = Main.rand.NextFloat(0.2f, 0.9f)
        });
    }

    public static void DrawChaosRealm(float rotation, float opacity)
    {
        // Make the background colors more muted based on how strong the fog is.
        Vector2 screenArea = ViewportSize;
        Vector2 textureArea = screenArea / WhitePixel.Size() * 2f;
        Vector3[] palette = AvatarOfEmptinessSky.Palettes["Standard"];

        // Draw the background with a special shader.
        var backgroundShader = ShaderManager.GetShader("NoxusBoss.AvatarPhase2BackgroundShader");
        backgroundShader.TrySetParameter("intensity", 1f);
        backgroundShader.TrySetParameter("screenOffset", Main.screenPosition * 0.00007f);
        backgroundShader.TrySetParameter("gradientCount", palette.Length);
        backgroundShader.TrySetParameter("gradient", palette);
        backgroundShader.TrySetParameter("time", Main.GlobalTimeWrappedHourly * -6f);
        backgroundShader.TrySetParameter("arcCurvature", 3.4f);
        backgroundShader.TrySetParameter("windPrevalence", 2f);
        backgroundShader.TrySetParameter("brightnessMaskDetail", 22.5f);
        backgroundShader.TrySetParameter("brightnessNoiseVariance", 0.56f);
        backgroundShader.TrySetParameter("backgroundBaseColor", new Vector4(0f, 0f, 0.015f, 0f));
        backgroundShader.TrySetParameter("vignetteColor", Vector4.Zero);
        backgroundShader.SetTexture(PerlinNoise, 1, SamplerState.LinearWrap);
        backgroundShader.SetTexture(WavyBlotchNoiseDetailed, 2, SamplerState.LinearWrap);
        backgroundShader.Apply();

        Main.spriteBatch.Draw(WhitePixel, screenArea * 0.5f + Main.rand.NextVector2Circular(15f, 15f), null, Color.White * opacity, rotation, WhitePixel.Size() * 0.5f, textureArea, 0, 0f);
    }
}
