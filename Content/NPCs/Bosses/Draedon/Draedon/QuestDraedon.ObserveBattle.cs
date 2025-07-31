using Microsoft.Xna.Framework;
using NoxusBoss.Content.NPCs.Bosses.Draedon.Projectiles;
using NoxusBoss.Content.NPCs.Friendly;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.Draedon.Draedon;

public partial class QuestDraedon : ModNPC
{
    /// <summary>
    /// The AI method that makes Draedon observe the battle against Mars.
    /// </summary>
    public void DoBehavior_ObserveBattle()
    {
        if (MarsBody.Myself is null)
        {
            bool everyoneIsDead = true;
            foreach (Player player in Main.ActivePlayers)
            {
                if (!player.dead)
                    everyoneIsDead = false;
            }

            if (everyoneIsDead)
                ChangeAIState(DraedonAIType.Leave);
            else
                ChangeAIState(DraedonAIType.WaitForSomeoneToTakeSeed);

            return;
        }

        float flySpeedInterpolant = InverseLerp(0f, 32f, AITimer);
        Vector2 hoverDestination = PlayerToFollow.Center + PlayerToFollow.SafeDirectionTo(NPC.Center) * new Vector2(800f, 560f);

        // Stay within the safe zone if Mars is firing his disintegration ray.
        int solynIndex = NPC.FindFirstNPC(ModContent.NPCType<BattleSolyn>());
        List<Projectile> disintegrationRays = AllProjectilesByID(ModContent.ProjectileType<ExoelectricDisintegrationRay>()).ToList();
        bool disintegrationRayIsBeingFired = disintegrationRays.Count >= 1;
        bool standUp = false;
        if (solynIndex != -1 && disintegrationRayIsBeingFired)
        {
            NPC solyn = Main.npc[solynIndex];
            Vector2 disintegrationRaySource = disintegrationRays.First().Center;
            float hoverDistance = PlayerToFollow.Distance(disintegrationRaySource) - solyn.Distance(disintegrationRaySource) + 600f;
            hoverDestination = solyn.Center + MarsBody.Myself.As<MarsBody>().CarvedLaserbeam_LaserbeamDirection.ToRotationVector2() * hoverDistance;
            flySpeedInterpolant = 2f;
            standUp = true;
        }

        NPC.SmoothFlyNear(hoverDestination, flySpeedInterpolant * 0.06f, 1f - flySpeedInterpolant * 0.1f);
        NPC.spriteDirection = (int)NPC.HorizontalDirectionTo(MarsBody.Myself.Center);

        if (standUp)
        {
            // One the first frame of being asked to stand up, Draedon's frame state will be beyond the natural range of frames 0-15 for the purposes of his standing up animation.
            // To ensure that Draedon starts his standing up animation from the get go, his frame state is reset to 0 if he's outside of said natural range.
            if (Frame >= 16)
                Frame = 0;

            if (Frame >= 15)
                Frame = 11;

            if (FrameTimer >= 7f)
            {
                Frame++;
                FrameTimer = 0f;
            }
        }

        else if (FrameTimer % 7f == 6f)
        {
            Frame++;
            if (Frame >= 48)
                Frame = 23;
        }
    }
}
