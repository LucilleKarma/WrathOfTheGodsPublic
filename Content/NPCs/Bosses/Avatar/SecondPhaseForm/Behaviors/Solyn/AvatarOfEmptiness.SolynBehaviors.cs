using Microsoft.Xna.Framework;

using NoxusBoss.Content.NPCs.Bosses.Avatar.FirstPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles.SolynProjectiles;
using NoxusBoss.Content.NPCs.Friendly;

using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;

public partial class AvatarOfEmptiness
{
    /// <summary>
    /// The amount of damage homing star bolts from Solyn do to enemies.
    /// </summary>
    public static int SolynHomingStarBoltDamage => GetAIInt("SolynHomingStarBoltDamage");

    // NOTE -- I at one point complained about this method and the one below needing an explicit static NPC instance, rather than just implicitly accessing the NPC argument for simpler syntax.
    // Sorry, past me, you didn't do anything wrong. This is necessary because this method is not just used by the AvatarOfEmptiness (Avatar's phase 2) ModNPC.
    // It is also used by the AvatarRift (Avatar's phase 1) ModNPC. Consequently, it must be static and require an explicit argument to account for both use cases across the different NPCs.

    // Could thereotically fuse the two mod NPCs like Calamity did a while ago, but I don't really see the point. It'd be a bunch of work for little tangible return.

    /// <summary>
    /// Instructs Solyn to fly near the player.
    /// </summary>
    public static void StandardSolynBehavior_FlyNearPlayer(BattleSolyn solyn, NPC? avatar)
    {
        NPC solynNPC = solyn.NPC;
        Player playerToFollow = solyn.Player;
        Vector2 lookDestination = playerToFollow.Center;
        Vector2 hoverDestination = playerToFollow.Center + new Vector2(solynNPC.HorizontalDirectionTo(playerToFollow.Center) * -66f, -10f);

        AvatarAIType? currentP2State = null;
        if (avatar is not null && avatar.type == ModContent.NPCType<AvatarOfEmptiness>())
            currentP2State = avatar.As<AvatarOfEmptiness>().CurrentState;

        if (currentP2State == AvatarAIType.Awaken_RiftSizeIncrease || currentP2State == AvatarAIType.Awaken_LegEmergence ||
            currentP2State == AvatarAIType.Awaken_ArmJutOut || currentP2State == AvatarAIType.Awaken_HeadEmergence ||
            currentP2State == AvatarAIType.Awaken_Scream)
        {
            if (avatar is not null)
            {
                hoverDestination = Vector2.Lerp(hoverDestination, avatar.Center, 0.45f);
                lookDestination = avatar.Center;
            }
        }

        Vector2 force = solynNPC.SafeDirectionTo(hoverDestination) * InverseLerp(36f, 250f, solynNPC.Distance(hoverDestination)) * 0.8f;
        if (Vector2.Dot(solynNPC.velocity, solynNPC.SafeDirectionTo(hoverDestination)) < 0f)
        {
            solynNPC.Center = Vector2.Lerp(solynNPC.Center, hoverDestination, 0.02f);
            force *= 4f;
        }

        // Try to not fly directly into the ground.
        if (Collision.SolidCollision(solynNPC.TopLeft, solynNPC.width, solynNPC.height))
            force.Y -= 0.6f;

        // Try to avoid dangerous projectiles.
        Rectangle dangerCheckZone = Utils.CenteredRectangle(solynNPC.Center, Vector2.One * 450f);
        foreach (Projectile projectile in Main.ActiveProjectiles)
        {
            bool isThreat = projectile.hostile && projectile.Colliding(projectile.Hitbox, dangerCheckZone);
            if (!isThreat)
                continue;

            float repelForceIntensity = Clamp(300f / (projectile.Hitbox.Distance(solynNPC.Center) + 3f), 0f, 1.9f);
            force += projectile.SafeDirectionTo(solynNPC.Center) * repelForceIntensity;
        }

        solynNPC.velocity += force;

        solyn.UseStarFlyEffects();
        solynNPC.spriteDirection = (int)solynNPC.HorizontalDirectionTo(lookDestination);
        solynNPC.rotation = solynNPC.rotation.AngleLerp(0f, 0.3f);
    }

    /// <summary>
    /// Instructs Solyn to attack the Avatar.
    /// </summary>
    public static void StandardSolynBehavior_AttackAvatar(BattleSolyn solyn)
    {
        NPC solynNPC = solyn.NPC;
        solyn.UseStarFlyEffects();

        int dashPrepareTime = 10;
        int dashTime = 4;
        int waitTime = 12;
        int slowdownTime = 11;
        int wrappedTimer = solyn.AITimer % (dashPrepareTime + dashTime + waitTime + slowdownTime);

        NPC? target = Myself is null ? AvatarRift.Myself : Myself;

        if (target is null)
        {
            StandardSolynBehavior_FlyNearPlayer(solyn, target);
            return;
        }

        float accelerationFactor = target.type == ModContent.NPCType<AvatarOfEmptiness>() ? target.As<AvatarOfEmptiness>().ZPositionScale : 1f;
        Vector2 destination = target.type == ModContent.NPCType<AvatarOfEmptiness>() ? target.As<AvatarOfEmptiness>().SpiderLilyPosition : target.Center;

        // Prepare for the dash, drifting towards the Avatar at an accelerating pace.
        if (wrappedTimer <= dashPrepareTime)
        {
            if (wrappedTimer == 1)
                solynNPC.oldPos = new Vector2[solynNPC.oldPos.Length];

            solynNPC.velocity += solynNPC.SafeDirectionTo(destination) * wrappedTimer * accelerationFactor / dashPrepareTime * 8f;
        }

        // Initiate the dash.
        else if (wrappedTimer <= dashPrepareTime + dashTime)
        {
            if (Vector2.Dot(solynNPC.velocity, solynNPC.SafeDirectionTo(destination)) < 0f)
                solynNPC.velocity *= 0.75f;
            else
                solynNPC.velocity *= 1.67f;
            solynNPC.velocity = solynNPC.velocity.ClampLength(0f, 100f);
        }

        // Fly upward after the dash has reached its maximum speed, releasing homing star bolts.
        else if (wrappedTimer <= dashPrepareTime + dashTime + waitTime)
        {
            solynNPC.velocity.Y -= 4f;

            if (Main.netMode != NetmodeID.MultiplayerClient && !solyn.IsMultiplayerClone)
            {
                for (int i = 0; i < 2; i++)
                {
                    Vector2 boltVelocity = Main.rand.NextVector2Circular(16f, 16f);
                    Projectile.NewProjectile(solynNPC.GetSource_FromAI(), solynNPC.Center, boltVelocity, ModContent.ProjectileType<HomingStarBolt>(), SolynHomingStarBoltDamage, 0f, -1, 0f, 0f, 1f);
                }
            }
        }

        // Slow down after the dash.
        else
            solynNPC.velocity *= 0.76f;

        // SPIN
        if (wrappedTimer <= dashPrepareTime || wrappedTimer >= dashPrepareTime + dashTime + waitTime)
            solynNPC.rotation = solynNPC.rotation.AngleLerp(solynNPC.velocity.X * 0.0097f, 0.21f);
        else
            solynNPC.rotation += solynNPC.spriteDirection * TwoPi * 0.18f;

        // Decide Solyn's direction.
        if (Abs(solynNPC.velocity.X) >= 1.3f)
            solynNPC.spriteDirection = solynNPC.velocity.X.NonZeroSign();

        // Make afterimages stronger than usual.
        solyn.AfterimageCount = 14;
        solyn.AfterimageClumpInterpolant = 0.5f;
        solyn.AfterimageGlowInterpolant = 1f;
    }
}
