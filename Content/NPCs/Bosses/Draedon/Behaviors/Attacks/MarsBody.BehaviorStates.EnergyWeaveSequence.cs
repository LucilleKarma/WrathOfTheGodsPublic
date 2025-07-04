using Luminance.Common.StateMachines;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers.SolynDialogue;
using NoxusBoss.Content.NPCs.Bosses.Draedon.Projectiles;
using NoxusBoss.Content.NPCs.Bosses.Draedon.Projectiles.SolynProjectiles;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Content.Particles;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Draedon;

public partial class MarsBody
{
    /// <summary>
    /// How long it's been since Solyn has begun dashing at Mars during his energy weave sequence attack.
    /// </summary>
    public ref float EnergyWeaveSequence_SolynDashTimer => ref NPC.ai[0];

    /// <summary>
    /// How long it's been since Mars got hit by Solyn's impact during his energy weave sequence attack.
    /// </summary>
    public ref float EnergyWeaveSequence_PostDashImpactTimer => ref NPC.ai[1];

    /// <summary>
    /// How long Solyn spends redirecting near the player during Mars' energy weave sequence attack.
    /// </summary>
    public static int EnergyWeaveSequence_SolynRedirectTime => GetAIInt("EnergyWeaveSequence_SolynRedirectTime");

    /// <summary>
    /// How long Mars spends intercepting between Solyn and the player during Mars' energy weave sequence attack.
    /// </summary>
    public static int EnergyWeaveSequence_MarsInterceptionTime => GetAIInt("EnergyWeaveSequence_MarsInterceptionTime");

    /// <summary>
    /// How long Mars waits after creating his shockwave before creating tesla fields during his energy wave sequence attack.
    /// </summary>
    public static int EnergyWeaveSequence_FieldSummonDelay => GetAIInt("EnergyWeaveSequence_FieldSummonDelay");

    /// <summary>
    /// How long Mars spends being stunned after being hit by Solyn's dash his wave sequence attack.
    /// </summary>
    public static int EnergyWeaveSequence_PostImpactStunTime => GetAIInt("EnergyWeaveSequence_PostImpactStunTime");

    /// <summary>
    /// How much damage Mars' tesla fields do.
    /// </summary>
    public static int TeslaFieldDamage => GetAIInt("TeslaFieldDamage");

    [AutomatedMethodInvoke]
    public void LoadState_EnergyWeaveSequence()
    {
        StateMachine.RegisterTransition(MarsAIType.EnergyWeaveSequence, MarsAIType.PostEnergyWeaveSequenceStun, false, () =>
        {
            return EnergyWeaveSequence_PostDashImpactTimer >= EnergyWeaveSequence_PostImpactStunTime;
        });
        StateMachine.RegisterStateBehavior(MarsAIType.EnergyWeaveSequence, DoBehavior_EnergyWeaveSequence);
    }

    /// <summary>
    /// Performs Mars' energy weave sequence attack, making the player weave through energy and missiles in order to reach Solyn.
    /// </summary>
    public void DoBehavior_EnergyWeaveSequence()
    {
        NPC.rotation = NPC.rotation.AngleLerp(NPC.velocity.X * 0.01f, 0.4f);
        RailgunCannonAngle = NPC.rotation + PiOver2;
        SolynAction = DoBehavior_EnergyWeaveSequence_Solyn;

        float armPressInterpolant = InverseLerp(0f, EnergyWeaveSequence_MarsInterceptionTime * 0.75f, AITimer - EnergyWeaveSequence_SolynRedirectTime);
        if (AITimer >= EnergyWeaveSequence_SolynRedirectTime + EnergyWeaveSequence_MarsInterceptionTime + 30)
            armPressInterpolant = 0f;

        // Move Mars' arms.
        Vector2 leftArmOffset = new Vector2(-180f + armPressInterpolant * 160f, 190f - armPressInterpolant * 70f);
        Vector2 rightArmOffset = new Vector2(180f - armPressInterpolant * 160f, 190f - armPressInterpolant * 70f);
        MoveArmsTowards(leftArmOffset, rightArmOffset, InverseLerp(0f, 20f, AITimer) * 0.33f, 0.5f);

        // Handle special substates.
        if (EnergyWeaveSequence_PostDashImpactTimer >= 1f)
        {
            DoBehavior_EnergyWeaveSequence_PostSolynDashImpact();
            EnergyWeaveSequence_PostDashImpactTimer++;
            return;
        }
        if (EnergyWeaveSequence_SolynDashTimer >= 1f)
        {
            DoBehavior_EnergyWeaveSequence_SolynDash_Mars();
            EnergyWeaveSequence_SolynDashTimer++;
            return;
        }

        // Create the shoving shockwave once ready.
        if (Main.netMode != NetmodeID.MultiplayerClient && AITimer == EnergyWeaveSequence_SolynRedirectTime + EnergyWeaveSequence_MarsInterceptionTime)
            NewProjectileBetter(NPC.GetSource_FromAI(), NPC.Center + Vector2.UnitY.RotatedBy(NPC.rotation) * NPC.scale * 20f, Vector2.Zero, ModContent.ProjectileType<MassiveElectricShockwave>(), 0, 0f);

        // Intercept Solyn and the target at first before creating the shockwave, to ensure that they end up far away from each other when shoved.
        if (AITimer >= EnergyWeaveSequence_SolynRedirectTime && AITimer <= EnergyWeaveSequence_SolynRedirectTime + EnergyWeaveSequence_MarsInterceptionTime * 0.75f)
        {
            NPC solyn = Main.npc[NPC.FindFirstNPC(ModContent.NPCType<BattleSolyn>())];
            Vector2 hoverDestination = (Target.Center + solyn.Center) * 0.5f;
            NPC.Center = Vector2.Lerp(NPC.Center, hoverDestination, 0.03f);
            NPC.SmoothFlyNear(hoverDestination, 0.12f, 0.8f);
        }

        // Slow down momentarily after creating the shockwave.
        else if (AITimer <= EnergyWeaveSequence_SolynRedirectTime + EnergyWeaveSequence_MarsInterceptionTime + EnergyWeaveSequence_FieldSummonDelay + 30)
            NPC.velocity *= 0.85f;

        // Stay above the target and charge energy after creating the shockwave.
        else
        {
            Vector2 hoverDestination = Target.Center - Vector2.UnitY * 350f;
            NPC.SmoothFlyNear(hoverDestination, 0.04f, 0.92f);
        }

        // Grant the target infinite flight.
        Target.wingTime = Target.wingTimeMax;
        CalamityCompatibility.GrantInfiniteCalFlight(Target);

        // Release electric fields that block the player's attempt at reaching Solyn.
        bool hasBegunCreatingFields = AITimer >= EnergyWeaveSequence_SolynRedirectTime + EnergyWeaveSequence_MarsInterceptionTime + EnergyWeaveSequence_FieldSummonDelay;
        bool canCreateFields = Main.netMode != NetmodeID.MultiplayerClient && hasBegunCreatingFields;
        if (canCreateFields && AITimer % 6 == 5)
        {
            float fieldSpawnOffset = Main.rand.NextFloat(60f, 550f) + InverseLerp(0f, 7f, Target.velocity.Length()) * 250f;
            Vector2 fieldSpawnOffsetVector = Main.rand.NextVector2Unit() * fieldSpawnOffset + Target.velocity * 25f;
            if (fieldSpawnOffsetVector.Length() >= 210f || Target.velocity.Length() <= 2.75f)
                NewProjectileBetter(NPC.GetSource_FromAI(), Target.Center + fieldSpawnOffsetVector, Vector2.Zero, ModContent.ProjectileType<TeslaField>(), TeslaFieldDamage, 0f, -1, 60);
        }
    }

    /// <summary>
    /// Handles Mars' part in the dash for Mars' energy weave dash sequence attack.
    /// </summary>
    public void DoBehavior_EnergyWeaveSequence_SolynDash_Mars()
    {
        SolynAction = DoBehavior_EnergyWeaveSequence_SolynDash_Solyn;

        float flyAwayInterpolant = InverseLerp(0f, 10f, EnergyWeaveSequence_SolynDashTimer);
        NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(Target.Center) * flyAwayInterpolant * -50f, flyAwayInterpolant * 0.18f);
    }

    /// <summary>
    /// Handles Solyn's part in the dash for Mars' energy weave dash sequence attack.
    /// </summary>
    public void DoBehavior_EnergyWeaveSequence_SolynDash_Solyn(BattleSolyn solyn)
    {
        if (solyn.IsMultiplayerClone)
        {
            solyn.Invisible = true;
            return;
        }

        NPC solynNPC = solyn.NPC;
        solyn.WorldMapIconScale = Lerp(solyn.WorldMapIconScale, 1.6f, 0.12f);
        solynNPC.velocity += solynNPC.SafeDirectionTo(NPC.Center) * 3f;
        solynNPC.velocity = solynNPC.velocity.RotateTowards(solynNPC.AngleTo(NPC.Center), 0.04f);
        solynNPC.spriteDirection = (int)solynNPC.HorizontalDirectionTo(NPC.Center);
        solynNPC.rotation = solynNPC.rotation.AngleLerp(solynNPC.velocity.ToRotation() + PiOver2, 0.36f);
        solyn.OptionalPreDrawRenderAction = _ => DoBehavior_EnergyWeaveSequence_DrawSolynDashTrail(1f, solyn);
        solyn.OptionalPostDrawRenderAction = _ => DoBehavior_EnergyWeaveSequence_DrawSolynDashTrail(0.12f, solyn);
        solyn.UseStarFlyEffects();

        // Neutralize nearby tesla fields, to ensure that they don't hurt Solyn or the player.
        int fieldID = ModContent.ProjectileType<TeslaField>();
        foreach (Projectile field in Main.ActiveProjectiles)
        {
            if (field.type == fieldID && solynNPC.WithinRange(field.Center, 250f))
            {
                field.damage = 0;
                field.As<TeslaField>().Time += 4f;
            }
        }

        // Stop the dash immediately if Solyn is going to collide with tiles or send herself and the player outside of the world.
        if (!WorldGen.InWorld((int)(solynNPC.Center.X / 16f), (int)(solynNPC.Center.Y / 16f), 10) || !Collision.CanHit(solynNPC.Center, 1, 1, solynNPC.Center + solynNPC.velocity * 1.45f, 1, 1))
        {
            Target.velocity = NPC.velocity.ClampLength(0f, 19f);
            NPC.velocity *= -0.5f;
            NPC.netUpdate = true;
            EnergyWeaveSequence_SolynDashTimer = 0f;
            return;
        }

        // Keep the player close to Solyn.
        float playerStickToSolynInterpolant = InverseLerp(0f, 6f, EnergyWeaveSequence_SolynDashTimer);
        Target.velocity *= 1f - playerStickToSolynInterpolant;
        Target.Center = Vector2.Lerp(Target.Center, solynNPC.Center, playerStickToSolynInterpolant) + solynNPC.velocity * 0.8f;
        Target.mount?.Dismount(Target);

        // Apply impact effects and hurt Mars when he's collided with.
        if (solynNPC.Hitbox.Intersects(NPC.Hitbox))
        {
            ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 16f);

            Vector2 impactPosition = (solynNPC.Center + NPC.Center) * 0.5f;
            Vector2 impactDirection = solynNPC.velocity.SafeNormalize(Vector2.UnitY).RotatedBy(PiOver2);
            for (int i = 0; i < 240; i++)
            {
                float ironSpeedInterpolant = Main.rand.NextFloat();
                Vector2 ironSpawnPosition = impactPosition + Main.rand.NextVector2Circular(20f, 20f);
                Vector2 ironVelocity = impactDirection * Main.rand.NextFromList(-1f, 1f) * Lerp(2.5f, 29f, ironSpeedInterpolant) + Main.rand.NextVector2Circular(14f, 14f);
                int ironLifetime = (int)Lerp(67f, 31f, ironSpeedInterpolant);
                float ironScale = Lerp(0.37f, 0.17f, ironSpeedInterpolant);
                Color ironColor = SparkParticlePalette.SampleColor(Main.rand.NextFloat());
                Color glowColor = Color.Wheat * 0.95f;

                MetalSparkParticle iron = new MetalSparkParticle(ironSpawnPosition, ironVelocity, ironSpeedInterpolant <= 0.356f, ironLifetime, new Vector2(0.4f, Main.rand.NextFloat(0.4f, 0.85f)) * ironScale, 1f, ironColor, glowColor);
                iron.Spawn();
            }

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                EnergyWeaveSequence_SolynDashTimer = 0f;
                EnergyWeaveSequence_PostDashImpactTimer = 1f;
                NPC.SimpleStrikeNPC(NPC.lifeMax / 205 + Main.rand.Next(2000), 0, true);
                NPC.velocity += solynNPC.SafeDirectionTo(NPC.Center) * 60f + solynNPC.velocity * 0.45f;

                SoundEngine.PlaySound(GennedAssets.Sounds.NPCHit.MarsHeavyHurt, NPC.Center);
                SoundEngine.PlaySound(GennedAssets.Sounds.Mars.HitScream, NPC.Center);

                NewProjectileBetter(solynNPC.GetSource_FromAI(), impactPosition + NPC.velocity * 1.7f, Vector2.Zero, ModContent.ProjectileType<SolynImpact>(), 0, 0f);

                solynNPC.netUpdate = true;
                solynNPC.velocity *= 0.6f;

                NPC.netUpdate = true;
            }
        }
    }

    private float DoBehavior_EnergyWeaveSequence_SolynDashTrailWidthFunction(NPC solyn, float completionRatio)
    {
        float growInterpolant = InverseLerp(0f, 12f, EnergyWeaveSequence_SolynDashTimer);
        if (EnergyWeaveSequence_PostDashImpactTimer >= 1f)
            growInterpolant = InverseLerp(15f, 0f, EnergyWeaveSequence_PostDashImpactTimer);

        float maxWidth = Lerp(56f, 67f, Cos01(Main.GlobalTimeWrappedHourly * 30f));
        float width = SmoothStep(0f, maxWidth, growInterpolant) + InverseLerp(0f, 15f, EnergyWeaveSequence_PostDashImpactTimer) * 200f;
        float tipRounding = SmoothStep(0f, 1f, InverseLerp(0.01f, 0.06f, completionRatio));
        return solyn.scale * tipRounding * width;
    }

    private Color DoBehavior_EnergyWeaveSequence_SolynDashTrailColorFunction(NPC solyn)
    {
        return solyn.GetAlpha(Color.White) * InverseLerp(15f, 0f, EnergyWeaveSequence_PostDashImpactTimer);
    }

    /// <summary>
    /// Renders Solyn's dash trail for use during Mars' energy weave sequence attack.
    /// </summary>
    public void DoBehavior_EnergyWeaveSequence_DrawSolynDashTrail(float opacity, BattleSolyn solyn)
    {
        // Render the sprite batch for layering reasons.
        Main.spriteBatch.ResetToDefault();

        NPC solynNPC = solyn.NPC;

        Vector4[] boltPalette = [Color.DeepPink.ToVector4() * opacity, Color.Wheat.ToVector4() * opacity, new Color(32, 123, 249).ToVector4() * opacity];
        ManagedShader trailShader = ShaderManager.GetShader("NoxusBoss.SolynDashTrailShader");
        trailShader.TrySetParameter("gradient", boltPalette);
        trailShader.TrySetParameter("gradientCount", boltPalette.Length);
        trailShader.TrySetParameter("localTime", Main.GlobalTimeWrappedHourly * 3f);
        trailShader.SetTexture(WavyBlotchNoise.Value, 1, SamplerState.LinearWrap);
        trailShader.SetTexture(TextureAssets.Extra[ExtrasID.FlameLashTrailShape], 2, SamplerState.LinearWrap);
        trailShader.Apply();

        Vector2 perpendicular = solynNPC.velocity.SafeNormalize(Vector2.Zero).RotatedBy(PiOver2) * 20f;
        Vector2[] trailPositions = new Vector2[solynNPC.oldPos.Length];
        for (int i = 0; i < trailPositions.Length; i++)
        {
            if (solynNPC.oldPos[i] == Vector2.Zero)
                continue;

            float trailCompletionRatio = i / (float)trailPositions.Length;
            float sine = Sin(TwoPi * trailCompletionRatio + Main.GlobalTimeWrappedHourly * -12f) * InverseLerp(0.01f, 0.9f, trailCompletionRatio);
            trailPositions[i] = solynNPC.oldPos[i] + perpendicular * sine;
        }

        PrimitiveSettings settings = new PrimitiveSettings(c => DoBehavior_EnergyWeaveSequence_SolynDashTrailWidthFunction(solynNPC, c),
                                         c => DoBehavior_EnergyWeaveSequence_SolynDashTrailColorFunction(solynNPC),
                                         _ => solynNPC.Size * 0.5f + solynNPC.velocity, Shader: trailShader);
        PrimitiveRenderer.RenderTrail(trailPositions, settings, 80);
    }

    /// <summary>
    /// Handles post-dash-impact effects for Mars , making him wobble in place a bit.
    /// </summary>
    public void DoBehavior_EnergyWeaveSequence_PostSolynDashImpact()
    {
        float baseWobble = Sin(TwoPi * EnergyWeaveSequence_PostDashImpactTimer / 30f);

        float desiredEndWobble = 0.01f;
        float exponentialDecayCoefficient = Log(desiredEndWobble) / EnergyWeaveSequence_PostImpactStunTime;
        float exponentialFadeoff = Exp(EnergyWeaveSequence_PostDashImpactTimer * exponentialDecayCoefficient);

        float maxWobbleRotation = Utils.Remap(EnergyWeaveSequence_PostDashImpactTimer, 0f, 30f, 2.9f, 1.1f);
        float wobbleRotation = baseWobble * exponentialFadeoff * maxWobbleRotation;

        NPC.rotation = NPC.rotation.AngleLerp(wobbleRotation, 0.3f);
        NPC.velocity *= 0.74f;

        ScreenShakeSystem.SetUniversalRumble(Cos01(wobbleRotation) * exponentialFadeoff * 10f, Pi / 3f, Vector2.UnitY, 0.3f);
    }

    /// <summary>
    /// Instructs Solyn to stay in place for the energy weave sequence attack.
    /// </summary>
    public void DoBehavior_EnergyWeaveSequence_Solyn(BattleSolyn solyn)
    {
        NPC solynNPC = solyn.NPC;
        SolynStarAction = star => MakeSolynStarReturnToSolyn(solyn, star);

        bool separatedFromStar = AnyProjectiles(ModContent.ProjectileType<SolynSentientStar>());
        if (separatedFromStar)
        {
            solynNPC.noGravity = false;
            solynNPC.noTileCollide = false;
            solynNPC.velocity.X *= 0.95f;
            solyn.Frame = 46f;
        }
        else if (AITimer <= EnergyWeaveSequence_SolynRedirectTime)
        {
            Vector2 hoverDestination = Target.Center + Target.SafeDirectionTo(solynNPC.Center) * 300f;
            solynNPC.SmoothFlyNear(hoverDestination, 0.2f, 0.6f);
            solynNPC.spriteDirection = (int)solynNPC.HorizontalDirectionTo(Target.Center);

            solyn.UseStarFlyEffects();
        }
        else
        {
            // Stay opposite to Mars.
            if (AITimer >= EnergyWeaveSequence_SolynRedirectTime + EnergyWeaveSequence_MarsInterceptionTime * 0.5f)
            {
                float hoverSpeedInterpolant = InverseLerp(EnergyWeaveSequence_MarsInterceptionTime * 1.35f, EnergyWeaveSequence_MarsInterceptionTime * 0.5f, AITimer - EnergyWeaveSequence_SolynRedirectTime);
                Vector2 hoverDestination = NPC.Center + Target.SafeDirectionTo(NPC.Center) * 300f;
                solynNPC.Center = Vector2.Lerp(solynNPC.Center, hoverDestination, hoverSpeedInterpolant * 0.18f);
            }

            // Stay within the world, you silly woman...
            solynNPC.position.X = Clamp(solynNPC.position.X, 750f, Main.maxTilesX * 16f - 750f);
            if (solynNPC.position.Y < 2400f)
                solynNPC.position.Y = 2400f;

            Vector2 bobbingVelocity = Vector2.UnitY * Sin(TwoPi * AITimer / 210f) * 2f;
            if (Collision.SolidCollision(NPC.Top - Vector2.UnitY * 600f, 1, 600))
                bobbingVelocity = solynNPC.SafeDirectionTo(Target.Center) * 6f;

            solynNPC.velocity = Vector2.Lerp(solynNPC.velocity, bobbingVelocity, 0.2f);

            // Initialize Solyn's dash if the player reaches her.
            if (Target.WithinRange(solynNPC.Center, 60f) && AITimer >= EnergyWeaveSequence_SolynRedirectTime + EnergyWeaveSequence_MarsInterceptionTime + EnergyWeaveSequence_FieldSummonDelay &&
                EnergyWeaveSequence_PostDashImpactTimer <= 0f)
            {
                solynNPC.oldPos = new Vector2[solynNPC.oldPos.Length];
                EnergyWeaveSequence_SolynDashTimer = 1f;
                NPC.netUpdate = true;
            }

            if (Collision.SolidCollision(solynNPC.TopLeft, solynNPC.width, solynNPC.height))
                solynNPC.position.Y -= 15f;

            solyn.UseStarFlyEffects();
        }

        if (AITimer == EnergyWeaveSequence_SolynRedirectTime + EnergyWeaveSequence_MarsInterceptionTime + EnergyWeaveSequence_FieldSummonDelay)
            SolynWorldDialogueManager.CreateNew("Mods.NoxusBoss.Dialog.SolynGetToMe", NPC.spriteDirection, NPC.Top, 150, true);

        solyn.AfterimageCount = 8;
        solyn.AfterimageClumpInterpolant = 0.5f;
        solyn.AfterimageGlowInterpolant = 1f;
        solyn.BackglowScale = Lerp(solyn.BackglowScale, Utils.Remap(solynNPC.velocity.Length(), 2f, 10f, 3.1f, 1f), 0.1f);
        solyn.WorldMapIconScale = Lerp(solyn.WorldMapIconScale, 1.6f, 0.12f);
        solynNPC.dontTakeDamage = true;
        solynNPC.rotation = solynNPC.velocity.X * 0.02f;

        if (separatedFromStar)
            solyn.BackglowScale = 0f;

        if (EnergyWeaveSequence_PostDashImpactTimer >= 1f)
        {
            solyn.OptionalPreDrawRenderAction = _ => DoBehavior_EnergyWeaveSequence_DrawSolynDashTrail(1f, solyn);
            solyn.OptionalPostDrawRenderAction = _ => DoBehavior_EnergyWeaveSequence_DrawSolynDashTrail(0.5f, solyn);
        }
    }

    /// <summary>
    /// Makes Solyn's star return to Solyn after a short amount of time.
    /// </summary>
    public static void MakeSolynStarReturnToSolyn(BattleSolyn solyn, SolynSentientStar star)
    {
        Projectile starProjectile = star.Projectile;
        if (star.Time >= 20f)
        {
            float flySpeedInterpolant = InverseLerp(0f, 30f, star.Time - 20f).Squared();
            starProjectile.SmoothFlyNear(solyn.NPC.Center, flySpeedInterpolant * 0.14f, 0.85f);
            if (starProjectile.WithinRange(solyn.NPC.Center, 30f))
                starProjectile.Kill();
        }
        else
            starProjectile.velocity *= 0.9f;
    }
}
