using Luminance.Common.StateMachines;
using Luminance.Core.Graphics;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Draedon.Projectiles;
using NoxusBoss.Content.NPCs.Bosses.Draedon.Projectiles.SolynProjectiles;
using NoxusBoss.Content.NPCs.Bosses.Draedon.SpecificEffectManagers;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Content.Particles;
using NoxusBoss.Core.DataStructures;

using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Draedon;

public partial class MarsBody
{
    /// <summary>
    /// Whether Mars has successfully dashed past the target during his brutal barrage attack.
    /// </summary>
    public bool BrutalBarrage_HasDashedPastTarget
    {
        get => NPC.ai[0] == 1f;
        set => NPC.ai[0] = value.ToInt();
    }

    /// <summary>
    /// Mars' rebound state during his brutal barrage attack.
    /// </summary>
    public ref float BrutalBarrage_ReboundState => ref NPC.ai[1];

    /// <summary>
    /// How much longer Mars' rebound should take during his brutal barrage attack.
    /// </summary>
    public ref float BrutalBarrage_ReboundCountdown => ref NPC.ai[2];

    /// <summary>
    /// The amount of dashes Mars has currently performed during his brutal barrage attack.
    /// </summary>
    public ref float BrutalBarrage_DashCounter => ref NPC.ai[3];

    /// <summary>
    /// How long Mars' basic rebounds last during his brutal barrage attack.
    /// </summary>
    public static int BrutalBarrage_BasicReboundTime => GetAIInt("BrutalBarrage_BasicReboundTime");

    /// <summary>
    /// How long Mars spends reeling back during his brutal barrage attack.
    /// </summary>
    public static int BrutalBarrage_ReelBackTime => GetAIInt("BrutalBarrage_ReelBackTime");

    /// <summary>
    /// How long Mars spends dashing during his brutal barrage attack.
    /// </summary>
    public static int BrutalBarrage_DashTime => GetAIInt("BrutalBarrage_DashTime");

    /// <summary>
    /// How long Mars spends accelerating after dashing during his brutal barrage attack.
    /// </summary>
    public static int BrutalBarrage_PostDashAccelerateTime => GetAIInt("BrutalBarrage_PostDashAccelerateTime");

    /// <summary>
    /// How many dashes Mars should perform during his brutal barrage attack before transitioning to his next attack.
    /// </summary>
    public static int BrutalBarrage_DashCount => GetAIInt("BrutalBarrage_DashCount");

    /// <summary>
    /// How long Mars spends grinding Solyn's forcefield during his brutal barrage attack.
    /// </summary>
    public static int BrutalBarrage_ForcefieldGrindTime => GetAIInt("BrutalBarrage_ForcefieldGrindTime");

    /// <summary>
    /// Mars' maximum dash speed during his brutal barrage attack.
    /// </summary>
    public static float BrutalBarrage_MaxDashSpeed => GetAIFloat("BrutalBarrage_MaxDashSpeed");

    /// <summary>
    /// The palette that sparks spawned by this Mars can cycle through.
    /// </summary>
    public static readonly Palette SparkParticlePalette = new Palette().
        AddColor(Color.White).
        AddColor(Color.Wheat).
        AddColor(Color.Yellow).
        AddColor(Color.Orange).
        AddColor(Color.Red);

    [AutomatedMethodInvoke]
    public void LoadState_BrutalBarrage()
    {
        StateMachine.RegisterTransition(MarsAIType.BrutalBarrage, null, false, () =>
        {
            return BrutalBarrage_DashCounter >= BrutalBarrage_DashCount;
        }, DoBehavior_BrutalBarrage_EndEffects);
        StateMachine.RegisterTransition(MarsAIType.BrutalBarrage, MarsAIType.BrutalBarrageGrindForcefield, true, () =>
        {
            return DoBehavior_BrutalBarrage_PerformChainsawCollisionCheck(out Projectile? forcefield) && forcefield is not null && BrutalBarrage_ReboundState == 0f;
        });
        StateMachine.RegisterTransition(MarsAIType.BrutalBarrageGrindForcefield, null, false, () =>
        {
            return AITimer >= BrutalBarrage_ForcefieldGrindTime;
        }, () =>
        {
            BrutalBarrage_ReboundCountdown = BrutalBarrage_BasicReboundTime;
            BrutalBarrage_ReboundState = 1f;
            NPC.velocity -= NPC.SafeDirectionTo(Target.Center) * 50f;

            NPC.SimpleStrikeNPC(NPC.lifeMax / 200, 0, true);
            SoundEngine.PlaySound(GennedAssets.Sounds.NPCHit.MarsHeavyHurt);

            if (Main.netMode != NetmodeID.MultiplayerClient)
                NewProjectileBetter(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<ElectricShockwave>(), 0, 0f);
        });

        StateMachine.RegisterStateBehavior(MarsAIType.BrutalBarrage, DoBehavior_BrutalBarrage);
        StateMachine.RegisterStateBehavior(MarsAIType.BrutalBarrageGrindForcefield, DoBehavior_BrutalBarrageForcefieldGrind);

        GameSceneSlowdownSystem.RegisterConditionalEffect(false, () => InverseLerp(0f, 12f, AITimer), () => InverseLerp(0f, 20f, AITimer) * 0.4f, () =>
        {
            if (Myself is null)
                return false;

            if (Myself.As<MarsBody>().StateMachine.StateStack.Count <= 0)
                return false;

            if (Myself.As<MarsBody>().CurrentState != MarsAIType.BrutalBarrageGrindForcefield)
                return false;

            return true;
        });
    }

    /// <summary>
    /// Performs Mars' brutal barrage attack, making him dash from offscreen and have Solyn project a protective forcefield for herself and the player.
    /// </summary>
    public void DoBehavior_BrutalBarrage()
    {
        SolynAction = solyn => DoBehavior_BrutalBarrage_Solyn(solyn, true);
        ProvideForcefieldsToPlayers();

        int silhouetteAppearTime = BrutalBarrage_DashCounter <= 1f ? 56 : 29;
        int dashTime = BrutalBarrage_DashTime;
        int postDashAccelerateTime = BrutalBarrage_PostDashAccelerateTime;

        EnergyCannonChainsawActive = AITimer >= silhouetteAppearTime || BrutalBarrage_DashCounter >= 1f;

        // Handle rebounds at maximum priority.
        if (BrutalBarrage_ReboundState == 1f)
            DoBehavior_BrutalBarrage_Rebound();

        // Reel back away from the target.
        else if (AITimer < silhouetteAppearTime)
        {
            if (BrutalBarrage_DashCounter >= 1f)
            {
                if (AITimer == 1)
                {
                    int forcefieldID = ModContent.ProjectileType<DirectionalSolynForcefield>();
                    Vector2 teleportOffset = Main.rand.NextVector2Unit();
                    foreach (Projectile projectile in Main.ActiveProjectiles)
                    {
                        if (projectile.type == forcefieldID && projectile.owner == NPC.target)
                        {
                            teleportOffset = -projectile.velocity.RotatedByRandom(PiOver2);
                            break;
                        }
                    }

                    ImmediateTeleportTo(Target.Center + teleportOffset * new Vector2(795f, 600f) + Target.velocity * new Vector2(15f, 24f));
                    NPC.velocity = Vector2.Zero;
                    NPC.netUpdate = true;
                }
                SilhouetteInterpolant = Sqrt(InverseLerp(0.97f, 0.35f, AITimer / (float)silhouetteAppearTime));

                if (AITimer == 4)
                    SoundEngine.PlaySound(GennedAssets.Sounds.Mars.EyeFlicker).WithVolumeBoost(1.25f);
            }
            else
            {
                NPC.velocity *= 0.9f;
                if (AITimer == 1 && (NPC.WithinRange(Target.Center, 700f) || !NPC.WithinRange(Target.Center, 1100f)))
                {
                    BrutalBarrage_DashCounter++;
                    AITimer = -1;
                    NPC.netUpdate = true;
                }
            }

            MoveArmsTowards(new Vector2(-90f, 150f), new Vector2(90f, 150f));
        }

        // Dash and accelerate.
        else if (AITimer <= silhouetteAppearTime + dashTime)
        {
            float oldSpeed = NPC.velocity.Length();
            float newSpeed = oldSpeed * 1.04f + Exp(oldSpeed * -0.11f) + 0.9f;
            Vector2 maxVelocity = new Vector2(1f, 0.6f) * BrutalBarrage_MaxDashSpeed;
            NPC.velocity = NPC.SafeDirectionTo(Target.Center) * newSpeed;
            NPC.velocity = Vector2.Clamp(NPC.velocity, -maxVelocity, maxVelocity);
            NPC.damage = NPC.defDamage;

            if (NPC.WithinRange(Target.Center, 100f))
            {
                AITimer = silhouetteAppearTime + dashTime + postDashAccelerateTime / 2;
                NPC.netUpdate = true;
            }
        }

        // Accelerate after the dash.
        else if (AITimer <= silhouetteAppearTime + dashTime + postDashAccelerateTime || NPC.WithinRange(Target.Center, 1150f))
        {
            float homeInInterpolant = 1f - BrutalBarrage_HasDashedPastTarget.ToInt();
            float oldSpeed = NPC.velocity.Length();
            NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(Target.Center) * oldSpeed, homeInInterpolant);
            NPC.velocity = NPC.velocity.SafeNormalize(Vector2.UnitY) * oldSpeed;
            NPC.damage = NPC.defDamage;

            bool flyingAwayFromTarget = Vector2.Dot(NPC.velocity, NPC.SafeDirectionTo(Target.Center)) < 0f;
            if (flyingAwayFromTarget)
            {
                if (!BrutalBarrage_HasDashedPastTarget)
                {
                    BrutalBarrage_HasDashedPastTarget = true;
                    NPC.netUpdate = true;
                }

                NPC.velocity *= 1.072f;
            }
            else
                NPC.velocity *= 1.016f;

            if (NPC.WithinRange(Target.Center, 70f) && !BrutalBarrage_HasDashedPastTarget)
            {
                BrutalBarrage_HasDashedPastTarget = true;
                NPC.netUpdate = true;
            }
        }

        // Prepare for the next attack cycle.
        else
        {
            AITimer = 0;
            DoBehavior_BrutalBarrage_AttackCycleEndEffects();
            NPC.netUpdate = true;
        }

        // Orient Mars' arms during the dash.
        if (AITimer >= silhouetteAppearTime)
        {
            float armMoveSpeedInterpolant = InverseLerp(0f, 9f, AITimer - silhouetteAppearTime);
            Vector2 leftArmOffset = new Vector2(-10f, 160f);
            Vector2 forwardOffset = NPC.velocity.SafeNormalize(Vector2.Zero).RotatedBy(-NPC.rotation) * 440f;
            MoveArmsTowards(leftArmOffset, forwardOffset, armMoveSpeedInterpolant * 0.55f, 1f - armMoveSpeedInterpolant * 0.4f);

            RailgunCannonAngle = RailgunCannonAngle.AngleLerp(leftElbowPosition.AngleTo(LeftHandPosition) + PiOver2, 0.3f);
            EnergyCannonAngle = EnergyCannonAngle.AngleLerp(NPC.AngleTo(Target.Center), 0.3f);
        }

        if (BrutalBarrage_ReboundState == 0f)
            NPC.rotation = NPC.rotation.AngleLerp(NPC.velocity.X * 0.016f, 0.4f);

        // Disable damage when close to the player, to prevent rash-dash cheese.
        NPC.dontTakeDamage = NPC.WithinRange(Target.Center, 240f);
    }

    /// <summary>
    /// Performs Mars' brutal barrage forcefield grind effec.t
    /// </summary>
    public void DoBehavior_BrutalBarrageForcefieldGrind()
    {
        int forcefieldID = ModContent.ProjectileType<DirectionalSolynForcefield>();
        int forcefieldIndex = -1;
        float minDistance = float.MaxValue;
        foreach (Projectile projectile in Main.ActiveProjectiles)
        {
            if (projectile.type != forcefieldID)
                continue;

            float distanceToHand = projectile.Distance(RightHandPosition);
            if (distanceToHand < minDistance)
            {
                forcefieldIndex = projectile.whoAmI;
                minDistance = distanceToHand;
            }
        }

        if (forcefieldIndex == -1)
            return;

        // Clear residual speed.
        if (AITimer <= 3)
            NPC.velocity *= 0.2f;

        if (AITimer == 1)
            SoundEngine.PlaySound(GennedAssets.Sounds.Mars.Swagger).WithVolumeBoost(1.45f);

        SolynAction = solyn => DoBehavior_BrutalBarrage_Solyn(solyn, false);
        EnergyCannonChainsawActive = true;

        Projectile forcefield = Main.projectile[forcefieldIndex];
        BattleSolyn solyn = BattleSolyn.GetSolynRelatedTo(Main.player[forcefield.owner])!;

        NPC.rotation = NPC.rotation.AngleLerp(NPC.velocity.X * 0.006f + NPC.SafeDirectionTo(solyn.Player.Center).X * 0.04f, 0.4f);

        float chainsawBend = -0.01f;
        Vector2 hoverDestination = solyn.Player.Center + forcefield.velocity.SafeNormalize(Vector2.UnitY) * new Vector2(350f, 430f);
        hoverDestination.X -= InverseLerp(-200f, 0f, solyn.Player.Center.X - NPC.Center.X) * 190f;

        Vector2 chainsawDestination = forcefield.Center + forcefield.velocity.SafeNormalize(Vector2.UnitY) * -16f;
        NPC.Center = Vector2.Lerp(NPC.Center, hoverDestination, 0.24f);
        NPC.SmoothFlyNear(hoverDestination, 0.12f, 0.87f);
        MoveArmsTowards(new Vector2(-90f, 150f), chainsawDestination - NPC.Center);

        RailgunCannonAngle = leftElbowPosition.AngleTo(LeftHandPosition) - PiOver2;
        EnergyCannonAngle = rightElbowPosition.AngleTo(RightHandPosition) - chainsawBend;

        if (forcefield.Colliding(forcefield.Hitbox, Utils.CenteredRectangle(RightHandPosition, Vector2.One * 100f)))
        {
            DoBehavior_BrutalBarrageButtonMash_CreateSparks(Vector2.Lerp(RightHandPosition, forcefield.Center, 0.64f));
            NPC.Center += NPC.SafeDirectionTo(RightHandPosition).RotatedBy(PiOver2) * Main.rand.NextFloatDirection() * 2.8f;
        }

        if (solyn.MultiplayerIndex == Main.myPlayer)
        {
            float panInterpolant = SmoothStep(0f, 1f, InverseLerp(0f, 30f, AITimer));
            float shake = InverseLerp(0f, 20f, AITimer).Squared() * 3.3f + InverseLerp(0f, BrutalBarrage_ForcefieldGrindTime, AITimer).Cubed() * 10f;
            float zoom = panInterpolant * 0.4f;

            ScreenShakeSystem.SetUniversalRumble(shake, TwoPi, null, 0.5f);
            CameraPanSystem.PanTowards(forcefield.Center, panInterpolant);
            CameraPanSystem.ZoomIn(zoom);

            ManagedScreenFilter focusShader = ShaderManager.GetFilter("NoxusBoss.AnimeFocusLinesShader");
            focusShader.TrySetParameter("intensity", panInterpolant * 1.6f);
            focusShader.SetTexture(PerlinNoise, 1, SamplerState.LinearWrap);
            focusShader.Activate();
        }

        AITimer += 2;
    }

    /// <summary>
    /// Creates impact sparks relative to a given target.
    /// </summary>
    /// <param name="impactOrigin">The origin of the impact.</param>
    public void DoBehavior_BrutalBarrageButtonMash_CreateSparks(Vector2 impactOrigin)
    {
        for (int i = 0; i < 22; i++)
        {
            float sparkSpread = Main.rand.NextFloatDirection();
            float sparkSpeed = SmoothStep(50f, 20f, Abs(sparkSpread));
            int sparkLifetime = (int)SmoothStep(7f, 20f, Abs(sparkSpread));
            if (Main.rand.NextBool(15))
                sparkLifetime += 6;
            if (Main.rand.NextBool(75))
                sparkLifetime += 14;

            Vector2 sparkVelocity = (NPC.AngleTo(impactOrigin) + PiOver2 + sparkSpread * 0.29f + Main.rand.NextFloatDirection() * 0.04f + 0.3f).ToRotationVector2() * sparkSpeed;
            Vector2 sparkSpawnPosition = Main.rand.NextVector2Circular(10f, 10f) + impactOrigin;

            Palette sparkPalette = new Palette([
                Color.White,
                Color.Wheat.HueShift(Main.rand.NextFloat(0.3f)),
                Color.Orange.HueShift(Main.rand.NextFloat(-0.04f, 0.1f)) * 0.85f,
                Color.OrangeRed * 0.9f,
                Color.Transparent,
            ]);

            PalettedElectricSparkParticle spark = new PalettedElectricSparkParticle(sparkSpawnPosition, sparkVelocity, sparkPalette, sparkLifetime, new Vector2(0.0075f, 0.06f));
            spark.Spawn();
        }

        StrongBloom bloom = new StrongBloom(impactOrigin + Main.rand.NextVector2Circular(30f, 30f), Vector2.Zero, NPC.GetAlpha(Color.Wheat) * 0.85f, NPC.scale * 0.7f, 5);
        bloom.Spawn();
    }

    /// <summary>
    /// Determines whether a given forcefield projectile is being collided with by Mars' chainsaw.
    /// </summary>
    public bool DoBehavior_BrutalBarrage_ForcefieldIsCollidingWithChainsaw(Projectile projectile, float checkArea)
    {
        bool chainsawHitboxCollision = projectile.Colliding(projectile.Hitbox, Utils.CenteredRectangle(RightHandPosition, Vector2.One * NPC.scale * checkArea));
        bool hitboxCollision = chainsawHitboxCollision;

        bool reasonableIncomingAngle = projectile.velocity.AngleBetween(-NPC.velocity) <= 0.54f;
        bool flyingTowardsPlayer = Vector2.Dot(NPC.velocity, NPC.SafeDirectionTo(Target.Center)) >= 0f;

        // Don't let the player beat the attack by just spinning the shield rapidly.
        float spinMovingAverage = projectile.As<DirectionalSolynForcefield>().SpinSpeedMovingAverage;
        bool tryingToCheese = spinMovingAverage >= ToRadians(13f);

        return hitboxCollision && reasonableIncomingAngle && flyingTowardsPlayer && !tryingToCheese;
    }

    /// <summary>
    /// Returns whether Mars should be stopped by a forcefield during his brutal barrage attack.
    /// </summary>
    /// <returns></returns>
    public bool DoBehavior_BrutalBarrage_PerformChainsawCollisionCheck(out Projectile? reflectingForcefield)
    {
        reflectingForcefield = null;

        int forcefieldID = ModContent.ProjectileType<DirectionalSolynForcefield>();
        foreach (Projectile projectile in Main.ActiveProjectiles)
        {
            if (projectile.type == forcefieldID && DoBehavior_BrutalBarrage_ForcefieldIsCollidingWithChainsaw(projectile, 96f))
            {
                reflectingForcefield = projectile;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Performs Mars' rebound during his brutal barrage attack.
    /// </summary>
    public void DoBehavior_BrutalBarrage_Rebound()
    {
        if (BrutalBarrage_ReboundCountdown >= BrutalBarrage_BasicReboundTime - 5f)
            NPC.velocity -= NPC.SafeDirectionTo(Target.Center) * 5f;
        else
            NPC.velocity *= 0.946f;

        float dazeSpin = Sin(TwoPi * BrutalBarrage_ReboundCountdown / 35f) * InverseLerp(0f, BrutalBarrage_BasicReboundTime, BrutalBarrage_ReboundCountdown) * 0.4f;
        NPC.rotation = NPC.rotation.AngleLerp(dazeSpin, 0.56f);

        BrutalBarrage_ReboundCountdown--;
        if (BrutalBarrage_ReboundCountdown <= 0f)
        {
            DoBehavior_BrutalBarrage_AttackCycleEndEffects();
            BrutalBarrage_ReboundState = 0f;
            AITimer = 0;
            NPC.netUpdate = true;
        }
    }

    /// <summary>
    /// Handles general-purpose end-of-cycle effects for Mars' brutal barrage attack.
    /// </summary>
    public void DoBehavior_BrutalBarrage_AttackCycleEndEffects()
    {
        BrutalBarrage_HasDashedPastTarget = false;
        BrutalBarrage_DashCounter++;
    }

    /// <summary>
    /// Handles general-purpose end-of-attack effects for Mars' brutal barrage attack.
    /// </summary>
    public static void DoBehavior_BrutalBarrage_EndEffects()
    {
        int forcefieldID = ModContent.ProjectileType<DirectionalSolynForcefield>();
        foreach (Projectile projectile in Main.ActiveProjectiles)
        {
            if (projectile.type == forcefieldID)
                projectile.As<DirectionalSolynForcefield>().BeginDisappearing();
        }
    }

    /// <summary>
    /// Instructs Solyn to stay near the player for the brutal barrage attack.
    /// </summary>
    public void DoBehavior_BrutalBarrage_Solyn(BattleSolyn solyn, bool beamEnabled)
    {
        int forcefieldID = ModContent.ProjectileType<DirectionalSolynForcefield>();
        float forcefieldDirection = 0f;
        foreach (Projectile projectile in Main.ActiveProjectiles)
        {
            if (projectile.owner == solyn.Player.whoAmI && projectile.type == forcefieldID)
            {
                forcefieldDirection = WrapAngle360(projectile.velocity.ToRotation());
                break;
            }
        }

        NPC solynNPC = solyn.NPC;
        Vector2 lookDestination = solyn.Player.Center;

        float angleSnapValue = PiOver2;
        float snappedForcefieldDirection = Round(forcefieldDirection * angleSnapValue) / angleSnapValue;
        Vector2 hoverOffset = snappedForcefieldDirection.ToRotationVector2() * -56f + Vector2.UnitX * 4f;
        Vector2 hoverDestination = solyn.Player.Center + hoverOffset;

        solynNPC.SmoothFlyNear(hoverDestination, 0.2f, 0.8f);

        solyn.UseStarFlyEffects();

        solynNPC.spriteDirection = (int)solynNPC.HorizontalDirectionTo(lookDestination);
        if (beamEnabled)
            HandleSolynPlayerTeamAttack(solyn);
        solynNPC.rotation = solynNPC.rotation.AngleLerp(0f, 0.3f);
    }
}
