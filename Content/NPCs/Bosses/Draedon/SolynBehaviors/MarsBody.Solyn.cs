using Microsoft.Xna.Framework;
using NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles.SolynProjectiles;
using NoxusBoss.Content.NPCs.Friendly;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Draedon;

public partial class MarsBody : ModNPC
{
    /// <summary>
    /// The amount of damage star bolts from Solyn do against mars.
    /// </summary>
    public static int SolynHomingStarBoltDamage => GetAIInt("SolynHomingStarBoltDamage");

    /// <summary>
    /// Instructs Solyn to fly near the player.
    /// </summary>
    public static void StandardSolynBehavior_FlyNearPlayer(BattleSolyn solyn)
    {
        NPC solynNPC = solyn.NPC;
        Player playerToFollow = solyn.Player;
        Vector2 lookDestination = playerToFollow.Center;
        Vector2 hoverDestination = playerToFollow.Center + new Vector2(solynNPC.HorizontalDirectionTo(playerToFollow.Center) * -66f, -10f);

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
    /// Instructs Solyn to attack Mars.
    /// </summary>
    /// <param name="solyn"></param>
    public static void StandardSolynBehavior_AttackMars(BattleSolyn solyn)
    {
        NPC solynNPC = solyn.NPC;
        solyn.UseStarFlyEffects();

        int dashPrepareTime = 8;
        int dashTime = 4;
        int waitTime = 10;
        int slowdownTime = 11;
        int wrappedTimer = solyn.AITimer % (dashPrepareTime + dashTime + waitTime + slowdownTime);

        NPC? target = Myself;

        if (target is null)
        {
            StandardSolynBehavior_FlyNearPlayer(solyn);
            return;
        }

        float accelerationFactor = 1f;
        Vector2 destination = target.Center;

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
            solynNPC.velocity = solynNPC.velocity.ClampLength(0f, 70f);
        }

        // Fly upward after the dash has reached its maximum speed, releasing homing star bolts.
        else if (wrappedTimer <= dashPrepareTime + dashTime + waitTime)
        {
            solynNPC.velocity.Y -= 4f;

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                for (int i = 0; i < 2; i++)
                {
                    Vector2 boltVelocity = Main.rand.NextVector2Circular(16f, 16f);
                    Projectile.NewProjectile(solynNPC.GetSource_FromAI(), solynNPC.Center, boltVelocity, ModContent.ProjectileType<HomingStarBolt>(), SolynHomingStarBoltDamage, 0f, solynNPC.target);
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
