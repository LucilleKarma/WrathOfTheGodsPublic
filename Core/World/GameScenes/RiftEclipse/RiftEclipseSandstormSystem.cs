using Microsoft.Xna.Framework;
using NoxusBoss.Content.Particles.Metaballs;
using NoxusBoss.Core.CrossCompatibility.Inbound;
using Terraria;
using Terraria.GameContent.Events;
using Terraria.GameContent.Shaders;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace NoxusBoss.Core.World.GameScenes.RiftEclipse;

public class RiftEclipseSandstormSystem : ModSystem
{
    /// <summary>
    /// How intense the sandstorm is. This increases throughout progression.
    /// </summary>
    public static float IntensityInterpolant
    {
        get
        {
            if (CommonCalamityVariables.DevourerOfGodsDefeated)
                return 1f;
            if (CommonCalamityVariables.StormWeaverDefeated || CommonCalamityVariables.CeaselessVoidDefeated || CommonCalamityVariables.SignusDefeated)
                return 0.45f;
            if (CommonCalamityVariables.ProvidenceDefeated)
                return 0.2f;

            return 0.1f;
        }
    }

    public override void OnModLoad()
    {
        On_Sandstorm.EmitDust += CreateRiftSandstormParticlesWrapper;
        On_SandstormShaderData.Apply += ChangeSandstormShaderColors;
    }

    private void ChangeSandstormShaderColors(On_SandstormShaderData.orig_Apply orig, SandstormShaderData self)
    {
        // These are the default values used for the shader. These need to be redefined here since it only gets set by vanilla once in ScreenEffectInitializer.
        float intensity = 0.4f;
        Vector3 mainColor = new Vector3(1f, 0.9f, 0.45f);
        Vector3 darkColor = new Vector3(0.7f, 0.5f, 0.3f);

        // Use dark red/violet colors for the shader when the Avatar is covering the sun/moon.
        if (RiftEclipseManagementSystem.RiftEclipseOngoing && Sandstorm.Happening)
        {
            mainColor = new(0.15f, 0f, 0.5f);
            darkColor = new(0.47f, 0.1f, 0.2f);
            intensity = 0.45f;
        }

        self.UseColor(mainColor).UseSecondaryColor(darkColor).UseIntensity(intensity);
        orig(self);
    }

    private void CreateRiftSandstormParticlesWrapper(On_Sandstorm.orig_EmitDust orig)
    {
        if (RiftEclipseManagementSystem.RiftEclipseOngoing && Sandstorm.Happening)
        {
            CreateRiftSandstormParticles();
            return;
        }

        orig();
    }

    public static void CreateRiftSandstormParticles()
    {
        // Don't do anything if the game is not running.
        if (Main.gamePaused)
            return;

        // Don't do anything if the player isn't on the surface, is at the beach rather than the desert, of if there is no sand.
        bool sandstormShouldPersist = Sandstorm.ShouldSandstormDustPersist();
        Sandstorm.HandleEffectAndSky(sandstormShouldPersist && Main.UseStormEffects);
        if (Main.SceneMetrics.SandTileCount < 100 || Main.LocalPlayer.position.Y > Main.worldSurface * 16.0 || Main.LocalPlayer.ZoneBeach)
            return;

        // Don't do anything if the sandstorm should subside.
        if (!sandstormShouldPersist)
            return;

        // Calculate the wind speed and direction. If it's far too low, do nothing.
        int windDirection = Math.Sign(Main.windSpeedCurrent);
        float windSpeed = Math.Abs(Main.windSpeedCurrent);
        if (windSpeed < 0.01f)
            return;

        // Calculate particle variables.
        float horizontalSpeed = windDirection * Lerp(0.9f, IntensityInterpolant * 0.25f + 1f, windSpeed);
        float particleSpawnChance = 500f / Main.SceneMetrics.SandTileCount;
        float screenWidthRatio = Main.screenWidth / (float)Main.maxScreenW;
        int particleLimitFactor = (int)(screenWidthRatio * 1000f);
        float dustSpawnCountLimit = particleLimitFactor * (Main.gfxQuality * 0.5f + 0.5f) + particleLimitFactor * 0.1f;
        if (dustSpawnCountLimit <= 0f)
            return;

        // Store the tan and cyan particle singletons separately for later.
        TanDarkGasMetaball tanParticle = ModContent.GetInstance<TanDarkGasMetaball>();
        CyanDarkGasMetaball cyanParticle = ModContent.GetInstance<CyanDarkGasMetaball>();

        // Create the particle selector. This will decide which the particles are spawned based on weighted probabilities.
        WeightedRandom<ColoredDarkGasMetaball> particleSelector = new WeightedRandom<ColoredDarkGasMetaball>();
        particleSelector.Add(cyanParticle, 0.2f);
        particleSelector.Add(ModContent.GetInstance<RedDarkGasMetaball>(), 2f);
        particleSelector.Add(ModContent.GetInstance<VioletDarkGasMetaball>(), 4f);
        particleSelector.Add(tanParticle);

        int particleSpawnCounter = 0;
        int particleSpawnMax = (int)Lerp(20f, 32f, IntensityInterpolant);
        while (particleSpawnCounter < Sandstorm.Severity * particleSpawnMax)
        {
            if (Main.rand.NextBool((int)particleSpawnChance + 1))
            {
                Vector2 particleSpawnPosition = new Vector2(Main.rand.NextFloat(Main.screenWidth + 1000f) - 500f, Main.rand.NextFloat() * -50f);

                // Randomly set the particle's horizontal spawn position to just beyond the screen bounds.
                if (Main.rand.NextBool(3) && windDirection == 1)
                    particleSpawnPosition.X = Main.rand.Next(500) - 500;
                else if (Main.rand.NextBool(3) && windDirection == -1)
                    particleSpawnPosition.X = Main.rand.Next(500) + Main.screenWidth;

                // Adjust the particle's vertical spawn position a good amount if it has the luxury of being horizontally offscreen.
                if (particleSpawnPosition.X < 0f || particleSpawnPosition.X > Main.screenWidth)
                    particleSpawnPosition.Y += Main.rand.NextFloat() * Main.screenHeight * 0.9f;

                // Offset the particle spawn position to the camera position..
                particleSpawnPosition += Main.screenPosition + Main.LocalPlayer.velocity;

                int particleTileX = (int)particleSpawnPosition.X / 16;
                int particleTileY = (int)particleSpawnPosition.Y / 16;
                if (!WorldGen.InWorld(particleTileX, particleTileY, 10) || Main.tile[particleTileX, particleTileY].WallType != WallID.None)
                    continue;

                for (int i = 0; i < 1; i++)
                {
                    // Determine the particle type to spawn.
                    ColoredDarkGasMetaball particleType = particleSelector.Get();

                    // Calculate the and velocity of the particle.
                    // Bright particles are faster but smaller.
                    float particleSize = Main.rand.NextFloat(12f, 18f);
                    Vector2 dustParticleVelocity = new Vector2(horizontalSpeed * 5f + Main.rand.NextFloat(4f) + horizontalSpeed * 14f, Main.rand.NextFloat(0.7f, 4f));
                    dustParticleVelocity *= Lerp(1.1f, 1.2f, Sandstorm.Severity);
                    if (particleType == tanParticle || particleType == cyanParticle)
                    {
                        particleSize *= 0.5f;
                        dustParticleVelocity *= 2.3f;
                    }

                    // Spawn the particle.
                    particleType.CreateParticle(particleSpawnPosition, dustParticleVelocity, particleSize);

                    dustSpawnCountLimit--;
                    if (dustSpawnCountLimit <= 0f)
                        break;
                }
                if (dustSpawnCountLimit <= 0f)
                    break;
            }
            particleSpawnCounter++;
        }
    }
}
