using Luminance.Common.DataStructures;
using Luminance.Common.StateMachines;
using Microsoft.Xna.Framework;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Draedon.Projectiles;
using NoxusBoss.Content.NPCs.Friendly;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Draedon;

public partial class MarsBody
{
    /// <summary>
    /// How much damage unstable matter created by Mars does.
    /// </summary>
    public static int UnstableMatterDamage => GetAIInt("UnstableMatterDamage");

    public static int ElectricCageBlasts_TrapDelay => 32;

    public static int ElectricCageBlasts_TrapSolynTime => 20;

    public static float ElectricCageBlasts_ForcefieldSize => 180f;

    /// <summary>
    /// The position of Mars' forcefield during his electric cage blasts attack.
    /// </summary>
    public Vector2 ElectricCageBlasts_ForcefieldPosition
    {
        get
        {
            Vector2 verticalOffset = Vector2.UnitY.RotatedBy(NPC.rotation) * (ElectricCageBlasts_ForcefieldSize + 40f);
            return LeftHandPosition + verticalOffset;
        }
    }

    [AutomatedMethodInvoke]
    public void LoadState_ElectricCageBlasts()
    {
        StateMachine.RegisterTransition(MarsAIType.ElectricCageBlasts, null, false, () =>
        {
            return AITimer >= ElectricCageBlasts_TrapDelay + 5 && !NPC.AnyNPCs(ModContent.NPCType<TrappingHolographicForcefield>());
        });
        StateMachine.RegisterStateBehavior(MarsAIType.ElectricCageBlasts, DoBehavior_ElectricCageBlasts);
    }

    /// <summary>
    /// Performs Mars' electric cage blasts attack.
    /// </summary>
    public void DoBehavior_ElectricCageBlasts()
    {
        NPC.rotation = NPC.rotation.AngleLerp(NPC.velocity.X * 0.01f, 0.4f);
        SolynAction = DoBehavior_ElectricCageBlasts_Solyn;

        if (AITimer <= 5)
            IProjOwnedByBoss<BattleSolyn>.KillAll();

        // Stay near Solyn in anticipation of the trapping effect.
        int solynIndex = NPC.FindFirstNPC(ModContent.NPCType<BattleSolyn>());
        if (AITimer <= ElectricCageBlasts_TrapDelay && solynIndex != -1)
        {
            NPC solynNPC = Main.npc[solynIndex];
            float flySpeedInterpolant = InverseLerp(0f, ElectricCageBlasts_TrapDelay * 0.4f, AITimer);

            Vector2 hoverDestination = solynNPC.Center + new Vector2(100f, -ElectricCageBlasts_ForcefieldSize + 16f - NPC.height);

            NPC.SmoothFlyNear(hoverDestination, flySpeedInterpolant * 0.25f, 1f - flySpeedInterpolant * 0.3f);
        }

        AltCannonVisualInterpolant = InverseLerp(0f, 12f, AITimer);

        float cannonSwipe = Pow(Cos(TwoPi * AITimer / 180f), 4f) * 0.9f - 0.5f;
        Vector2 leftArmOffset = new Vector2(-160f, 175f);
        Vector2 rightArmOffset = NPC.SafeDirectionTo(Target.Center) * new Vector2(185f, 260f) + Vector2.UnitX * 160f;
        float armMovementSpeedSharpness = InverseLerp(0f, 12f, AITimer) * 0.2f;
        MoveArmsTowards(leftArmOffset, rightArmOffset, armMovementSpeedSharpness, 0.84f);

        RailgunCannonAngle = PiOver2 + NPC.velocity.X * 0.02f;
        EnergyCannonAngle = RightHandPosition.AngleTo(Target.Center);

        // Fly above the player when done trapping the player.
        bool doneTrapping = AITimer >= ElectricCageBlasts_TrapDelay + ElectricCageBlasts_TrapSolynTime;
        if (doneTrapping)
        {
            Vector2 hoverDestination = Target.Center - Vector2.UnitY * 350f;
            if (NPC.OnRightSideOf(Target))
                hoverDestination.X -= 120f;

            NPC.SmoothFlyNear(hoverDestination, 0.052f, 0.96f);
        }

        // Release unstable matter.
        if (AITimer % 80 == 0 && doneTrapping)
        {
            Vector2 matterSpawnPosition = RightHandPosition;
            SoundEngine.PlaySound(GennedAssets.Sounds.Mars.UnstableMatterFire, matterSpawnPosition).WithVolumeBoost(1.7f);

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                Vector2 matterVelocity = RightHandPosition.SafeDirectionTo(Target.Center) * 4f;
                NewProjectileBetter(NPC.GetSource_FromAI(), matterSpawnPosition, matterVelocity, ModContent.ProjectileType<UnstableMatter>(), UnstableMatterDamage, 0f);

                RightHandVelocity -= matterVelocity.SafeNormalize(Vector2.Zero) * 35f;
                NPC.netUpdate = true;
            }
        }
    }

    /// <summary>
    /// Handles Solyn's part of Mars' electric cage blasts attack.
    /// </summary>
    public void DoBehavior_ElectricCageBlasts_Solyn(BattleSolyn solyn)
    {
        if (solyn.IsMultiplayerClone)
        {
            solyn.Invisible = true;
            return;
        }

        NPC solynNPC = solyn.NPC;
        int forcefieldIndex = NPC.FindFirstNPC(ModContent.NPCType<TrappingHolographicForcefield>());
        NPC? forcefield = forcefieldIndex >= 0 ? Main.npc[forcefieldIndex] : null;

        // Slow down in place so that Mars can trap Solyn.
        if (AITimer <= ElectricCageBlasts_TrapDelay + ElectricCageBlasts_TrapSolynTime)
        {
            // Create the forcefield.
            if (Main.netMode != NetmodeID.MultiplayerClient && AITimer == ElectricCageBlasts_TrapDelay + 1)
                NPC.NewNPC(NPC.GetSource_FromAI(), (int)solynNPC.Center.X, (int)solynNPC.Center.Y - 10, ModContent.NPCType<TrappingHolographicForcefield>(), NPC.whoAmI);

            solynNPC.velocity *= 0.84f;
            solynNPC.rotation = solynNPC.velocity.X * 0.012f;
        }

        // Stay within the entrapping forcefield.
        else if (forcefield is not null)
        {
            solynNPC.velocity *= 0.95f;
            solynNPC.rotation = solynNPC.rotation.AngleLerp(solynNPC.velocity.X * 0.2f, 0.1f);
            if (Abs(solynNPC.velocity.X) >= 0.4f)
                solynNPC.spriteDirection = solynNPC.velocity.X.NonZeroSign();

            float maxRadius = ElectricCageBlasts_ForcefieldSize * 0.45f - MathF.Max(solynNPC.Hitbox.Width, solynNPC.Hitbox.Height);
            Vector2 forcefieldPosition = forcefield.Center + NPC.velocity;
            while (solynNPC.Hitbox.Distance(forcefieldPosition) > maxRadius)
            {
                solynNPC.Center += solynNPC.SafeDirectionTo(forcefieldPosition);

                if (solynNPC.velocity.Length() <= 5f)
                {
                    solynNPC.velocity += solynNPC.SafeDirectionTo(forcefieldPosition).RotatedByRandom(0.2f) * Main.rand.NextFloat(2f, 3f);
                    solynNPC.netUpdate = true;
                }
            }
        }

        // Draw the front and back half of the forcefield relative to Solyn's layering.
        if (forcefield is not null)
        {
            TrappingHolographicForcefield forcefieldModProjectile = forcefield.As<TrappingHolographicForcefield>();
            solyn.OptionalPreDrawRenderAction = _ =>
            {
                forcefieldModProjectile.DrawBack();
            };
            solyn.OptionalPostDrawRenderAction = _ =>
            {
                forcefieldModProjectile.DrawFront();
            };
        }

        solyn.UseStarFlyEffects();
    }
}
