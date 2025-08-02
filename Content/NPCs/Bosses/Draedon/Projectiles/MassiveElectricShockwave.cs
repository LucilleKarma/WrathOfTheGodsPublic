using Luminance.Assets;
using Luminance.Common.DataStructures;
using Luminance.Common.Easings;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Draedon.Projectiles.SolynProjectiles;
using NoxusBoss.Content.NPCs.Friendly;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Draedon.Projectiles;

public class MassiveElectricShockwave : ModProjectile, IProjOwnedByBoss<MarsBody>
{
    /// <summary>
    /// How long this shockwave has existed for, in frames.
    /// </summary>
    public ref float Time => ref Projectile.ai[0];

    /// <summary>
    /// How long this shockwave should exist for, in frames.
    /// </summary>
    public static int Lifetime => SecondsToFrames(1.2f);

    public override string Texture => MiscTexturesRegistry.InvisiblePixelPath;

    public override void SetDefaults()
    {
        Projectile.width = 4000;
        Projectile.height = 4000;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.timeLeft = Lifetime;
        Projectile.Opacity = 0f;
        CooldownSlot = ImmunityCooldownID.Bosses;
    }

    public override void AI()
    {
        if (Time == 15f && Main.LocalPlayer.WithinRange(Projectile.Center, 4000f))
            SoundEngine.PlaySound(GennedAssets.Sounds.Mars.ElectricRingBlast).WithVolumeBoost(1.6f);

        float lifetimeRatio = Time / Lifetime;
        Projectile.Opacity = 1f - lifetimeRatio;
        Projectile.scale = EasingCurves.Sextic.Evaluate(EasingType.InOut, lifetimeRatio);

        int particleCount = (int)(Projectile.scale * Projectile.Opacity.Squared() * 30f);
        for (int i = 0; i < particleCount; i++)
        {
            Vector2 particleSpawnPosition = Projectile.Center + Main.rand.NextVector2Circular(Projectile.width, Projectile.height) * Projectile.scale * 0.45f;
            Dust particle = Dust.NewDustPerfect(particleSpawnPosition, 264);
            particle.color = Color.Lerp(Color.Wheat, Color.Cyan, Main.rand.NextFloat());
            particle.alpha = Projectile.alpha;
            particle.velocity = -Vector2.UnitY * Main.rand.NextFloat(0.6f);
            particle.noGravity = true;
            particle.noLight = true;
            particle.scale = Main.rand.NextFloat(0.6f, 0.786f);
        }

        // Push away players and Solyns within the radius of this shockwave.
        float generalPushBackSpeed = Lerp(11.5f, 30f, Sqrt(Projectile.Opacity));
        List<Entity> entitiesToPushBack = [];
        foreach (Player player in Main.ActivePlayers)
            entitiesToPushBack.Add(player);

        int solynID = ModContent.NPCType<BattleSolyn>();
        foreach (NPC solyn in Main.ActiveNPCs)
        {
            if (solyn.type == solynID)
                entitiesToPushBack.Add(solyn);
        }

        bool solynStarExists = AnyProjectiles(ModContent.ProjectileType<SolynSentientStar>());
        float softDistanceLimit = Projectile.scale * Projectile.width * 0.41f;
        float hardDistanceLimit = Projectile.scale * Projectile.width * 0.38f;
        foreach (Entity entity in entitiesToPushBack)
        {
            if (entity.WithinRange(Projectile.Center, softDistanceLimit) && Projectile.Opacity >= 0.3f)
            {
                if (entity is Player player)
                {
                    if (Main.myPlayer == player.whoAmI)
                        ScreenShakeSystem.StartShake(3.7f);

                    player.mount?.Dismount(player);
                }
                if (Main.netMode != NetmodeID.MultiplayerClient && !solynStarExists && entity is NPC npc && npc.type == solynID)
                    NewProjectileBetter(npc.GetSource_FromAI(), npc.Center, -Vector2.UnitY * 50f, ModContent.ProjectileType<SolynSentientStar>(), 0, 0f);

                float pushBackSpeed = generalPushBackSpeed;
                entity.velocity = Vector2.Lerp(entity.velocity, -entity.SafeDirectionTo(Projectile.Center) * pushBackSpeed, 0.36f);

                if (entity.WithinRange(Projectile.Center, hardDistanceLimit))
                {
                    Vector2 shoveDestination = Projectile.Center + Projectile.SafeDirectionTo(entity.Center) * hardDistanceLimit;
                    bool wouldShoveEntityIntoTiles = Collision.SolidTiles(shoveDestination - entity.Size * 0.5f, entity.width, entity.height);
                    bool wouldEffectivelyTeleportEntity = !shoveDestination.WithinRange(entity.Center, 150f);
                    if (!wouldShoveEntityIntoTiles && !wouldEffectivelyTeleportEntity)
                        entity.Center = shoveDestination;
                }
            }
        }

        Time++;
    }

    public override void OnKill(int timeLeft)
    {
        // Solyn's positions are getting desynced after shockwave for some reason.
        foreach (NPC? npc in Main.ActiveNPCs)
        {
            if (npc.ModNPC is BattleSolyn)
                npc.netUpdate = true;
        }
    }

    public void RenderGlow()
    {
        float glow = Convert01To010(InverseLerp(13f, 36f, Time).Squared()) + 0.001f;
        Vector2 glowSize = Vector2.One * Pow(glow, 1.3f) * 1750f;

        ManagedShader fieldShader = ShaderManager.GetShader("NoxusBoss.TeslaFieldShader");
        fieldShader.TrySetParameter("size", glowSize * 1.7f);
        fieldShader.TrySetParameter("posterizationDetail", 12f);
        fieldShader.SetTexture(TechyNoise.Value, 1, SamplerState.LinearWrap);
        fieldShader.Apply();

        Vector2 drawPosition = Projectile.Center - Main.screenPosition;
        Color color = Color.Lerp(new(255, 35, 26), new(255, 105, 31), Projectile.Center.Length() * 0.28f % 1f);
        color = Color.Lerp(color, Color.White, glow * 0.6f);

        Main.spriteBatch.Draw(WhitePixel, drawPosition, null, Projectile.GetAlpha(color), 0f, WhitePixel.Size() * 0.5f, glowSize * 1.75f, 0, 0f);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Main.spriteBatch.PrepareForShaders();

        Vector2 drawPosition = Projectile.Center - Main.screenPosition;
        ManagedShader shockwaveShader = ShaderManager.GetShader("NoxusBoss.MassiveElectricShockwaveShader");
        shockwaveShader.SetTexture(DendriticNoise, 1, SamplerState.LinearWrap);
        shockwaveShader.Apply();

        Vector2 size = Projectile.scale * Projectile.Size;

        float colorInterpolant = InverseLerp(0.25f, 0.7f, Time / Lifetime);
        Color startingColor = new Color(255, 228, 143);
        Color endingColor = new Color(255, 0, 10);
        Color color = Color.Lerp(startingColor, endingColor, colorInterpolant);

        Main.spriteBatch.Draw(WhitePixel, drawPosition, null, Projectile.GetAlpha(color), Projectile.rotation, WhitePixel.Size() * 0.5f, size, 0, 0f);

        Main.spriteBatch.PrepareForShaders();
        RenderGlow();

        Main.spriteBatch.ResetToDefault();
        return false;
    }
}
