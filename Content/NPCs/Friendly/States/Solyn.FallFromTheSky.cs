using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity;
using NoxusBoss.Content.Particles;
using NoxusBoss.Core.Graphics.GeneralScreenEffects;
using NoxusBoss.Core.World.GameScenes.SolynEventHandlers;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Friendly;

public partial class Solyn : ModNPC, IPixelatedPrimitiveRenderer
{
    /// <summary>
    /// The horizontal direction in which Solyn falls.
    /// </summary>
    public int SkyFallDirection
    {
        get;
        set;
    }

    /// <summary>
    /// How long it takes for Solyn to begin falling from the sky.
    /// </summary>
    public static int FallFromSky_CrashDelay => SecondsToFrames(3f);

    public void DoBehavior_FallFromTheSky()
    {
        Player player = Main.player[NPC.target];
        bool collisionCheck = NPC.Bottom.Y >= player.Center.Y && Collision.SolidCollision(NPC.TopLeft, NPC.width, NPC.height + 16);

        // NOTE -- During this state Solyn can occasionally bug out and not actually collide with tiles correctly, causing her to slide forward at mach 23 for a while.
        // This is a bug, and should be acknowledged as such. However, it is so darn funny that I am electing to not fix the problem.
        // She does, however, crash into walls if necessary, so that she doesn't slide so far that the player can't find her without map assistance.
        if (NPC.Bottom.Y < player.Center.Y && Collision.SolidCollision(NPC.TopLeft, NPC.width, 1))
            collisionCheck = true;
        if (!collisionCheck && Distance(NPC.Center.X, player.Center.X) >= 960f)
            collisionCheck = true;
        if (!collisionCheck && (NPC.Center.X <= 1000f || NPC.Center.X >= Main.maxTilesX * 16f - 1000f))
            collisionCheck = true;

        if (AITimer >= FallFromSky_CrashDelay && collisionCheck)
        {
            SoundEngine.PlaySound(GennedAssets.Sounds.Solyn.StarFallImpact, NPC.Center);
            GeneralScreenEffectSystem.RadialBlur.Start(NPC.Center, 0.5f, 10);
            ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 16f);

            NPC.velocity.Y = 0f;
            SwitchState(SolynAIType.GetUpAfterStarFall);
        }
        Frame = 21;

        HasBackglow = true;

        // Disable natural gravity clamping.
        NPC.noGravity = true;

        // Disable speaking.
        CanBeSpokenTo = false;

        int crashDelay = FallFromSky_CrashDelay;
        float startingCrashSpeed = 0.5f;
        float endingCrashSpeed = 80f;
        float crashAcceleration = 1.15f;
        Vector2 startingPosition = player.Center + new Vector2(SkyFallDirection * -550f, -440f);

        if (SkyFallDirection == 0)
        {
            SkyFallDirection = player.direction;
            NPC.netUpdate = true;
        }

        // Choose a player to follow on the first frame.
        if (AITimer <= 1)
        {
            NPC.TargetClosest();

            // Verify that the player is in an open surface area and not in space.
            // If they aren't, despawn as though nothing happened.
            bool openAir = !player.ZoneSkyHeight;
            for (int dy = 4; dy < 36; dy++)
            {
                Tile t = Framing.GetTileSafely((int)(player.Center.X / 16f), (int)(player.Center.Y / 16f) - dy);
                if (t.HasUnactuatedTile && WorldGen.SolidTile(t))
                {
                    openAir = false;
                    break;
                }

                t = Framing.GetTileSafely((int)(startingPosition.X / 16f), (int)(startingPosition.Y / 16f) - dy);
                if (t.HasUnactuatedTile && WorldGen.SolidTile(t))
                {
                    openAir = false;
                    break;
                }
            }

            if (!openAir)
                NPC.active = false;

            return;
        }

        if (AITimer == 5)
        {
            NamelessDeityBoss.CreateTwinkle(startingPosition, Vector2.One, Color.Cyan);
            RandomSolynSpawnSystem.SolynHasAppearedBefore = true;
        }

        if (AITimer == crashDelay - 75)
            SoundEngine.PlaySound(GennedAssets.Sounds.Solyn.StarFall, NPC.Center);

        if (AITimer < crashDelay)
            ScreenShakeSystem.SetUniversalRumble(InverseLerpBump(0.3f, 0.75f, 0.8f, 0.84f, AITimer / (float)crashDelay) * 1.4f, TwoPi, null, 0.2f);

        // Stick near the player as the twinkle does its animation.
        if (AITimer <= crashDelay)
        {
            NPC.Center = startingPosition;
            NPC.Opacity = 1f;
            NPC.scale = 0.001f;
        }

        // Crash in front of the player after the twinkle is gone.
        if (AITimer == crashDelay)
        {
            NPC.velocity = NPC.SafeDirectionTo(player.Center - Vector2.UnitX * SkyFallDirection * 200f) * startingCrashSpeed;
            NPC.oldPos = new Vector2[NPC.oldPos.Length];
            NPC.netUpdate = true;
        }

        // Accelerate and fade in.
        if (NPC.velocity.Length() < endingCrashSpeed)
            NPC.velocity *= crashAcceleration;
        if (AITimer >= crashDelay)
        {
            NPC.scale = Clamp(NPC.scale + 0.029f, 0f, 1f);

            if (NPC.velocity != Vector2.Zero)
            {
                NPC.spriteDirection = NPC.velocity.X.NonZeroSign();
                NPC.rotation = (NPC.velocity * new Vector2(1f, 6f)).ToRotation();
                if (NPC.spriteDirection == -1)
                    NPC.rotation += Pi;
            }
        }
    }

    public void DoBehavior_GetUpAfterStarFall()
    {
        NPC.velocity.X *= 0.8f;
        while (Collision.SolidCollision(NPC.BottomLeft, NPC.width, 2))
            NPC.position.Y -= 2f;

        // Disable speaking.
        CanBeSpokenTo = false;

        if (AITimer == 1)
        {
            StrongBloom bloom = new StrongBloom(NPC.Center, Vector2.Zero, Color.Yellow, 0.7f, 8);
            bloom.Spawn();

            bloom = new(NPC.Center, Vector2.Zero, Color.HotPink * 0.85f, 1.3f, 10);
            bloom.Spawn();

            for (int i = 0; i < 24; i++)
            {
                int starPoints = Main.rand.Next(3, 9);
                float starScaleInterpolant = Main.rand.NextFloat();
                int starLifetime = (int)Lerp(30f, 67f, starScaleInterpolant);
                float starScale = Lerp(0.42f, 0.7f, starScaleInterpolant) * NPC.scale;
                Color starColor = Main.hslToRgb(Main.rand.NextFloat(), 0.9f, 0.64f);
                starColor = Color.Lerp(starColor, Color.Wheat, 0.4f) * 0.4f;

                Vector2 starVelocity = Main.rand.NextVector2Circular(12f, 3f) - Vector2.UnitY * Main.rand.NextFloat(4f, 7.5f);
                TwinkleParticle star = new TwinkleParticle(NPC.Center, starVelocity, starColor, starLifetime, starPoints, new Vector2(Main.rand.NextFloat(0.4f, 1.6f), 1f) * starScale, starColor * 0.5f);
                star.Spawn();
            }

            // Lose a bit of HP from the impact.
            CombatText.NewText(NPC.Hitbox, CombatText.DamagedFriendly, 10, true);
            NPC.life = Utils.Clamp(NPC.life - 10, 1, NPC.lifeMax);
            NPC.netUpdate = true;

            // Move forward.
            NPC.velocity.X = NPC.spriteDirection * 40f;
        }

        NPC.rotation = 0f;

        if (AITimer <= 154)
            Frame = 24;
        if (AITimer <= 120)
            Frame = 23;
        if (AITimer <= 30)
            Frame = 22;

        if (AITimer >= 154)
            SwitchState(SolynAIType.StandStill);
    }
}
