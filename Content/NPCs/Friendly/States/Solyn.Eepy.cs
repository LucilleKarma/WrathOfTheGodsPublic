using Luminance.Common.Easings;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using NoxusBoss.Content.Particles;
using NoxusBoss.Core.DialogueSystem;
using NoxusBoss.Core.SolynEvents;
using NoxusBoss.Core.World.WorldGeneration;
using Terraria;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Friendly;

public partial class Solyn : ModNPC, IPixelatedPrimitiveRenderer
{
    /// <summary>
    /// Whether Solyn should try to rest.
    /// </summary>
    public bool ShouldGoEepy
    {
        get
        {
            if (SolynCampsiteWorldGen.CampSitePosition == Vector2.Zero)
                return false;

            if (Main.dayTime)
                return false;

            if (ModContent.GetInstance<SolynIntroductionEvent>().Stage == 0)
            {
                bool doneWithIntroDialogue = DialogueManager.FindByRelativePrefix("SolynIntroduction").SeenBefore("Talk8");
                return Main.rand.NextBool(120) && doneWithIntroDialogue;
            }

            return true;
        }
    }

    public void DoBehavior_EnterTentToSleep()
    {
        float walkSpeed = 1.2f;
        float walkAccelerationInterpolant = 0.15f;
        bool onGround = Abs(NPC.velocity.Y) <= 0.3f && Collision.SolidCollision(NPC.BottomLeft, NPC.width, 16, true);

        // Hide at the default layer so that Solyn can draw over the backside of the tent.
        NPC.hide = true;

        // Calculate the ideal walk speed.
        float idealWalkSpeed = NPC.SafeDirectionTo(SolynCampsiteWorldGen.TentPosition).X * walkSpeed;

        // Walk towards the tent.
        if (onGround)
        {
            NPC.velocity.X = Lerp(NPC.velocity.X, idealWalkSpeed, walkAccelerationInterpolant);

            // Look forward.
            if (Abs(NPC.velocity.X) >= 0.4f)
                NPC.spriteDirection = NPC.velocity.X.NonZeroSign();
        }

        // Jump if there's a wall closely ahead.
        bool wallCloselyAhead = Collision.SolidTilesVersatile((int)(NPC.Center.X / 16f), (int)(NPC.Center.X + NPC.spriteDirection * 50) / 16, (int)NPC.Top.Y / 16, (int)NPC.Bottom.Y / 16 - 2);
        if (wallCloselyAhead && onGround)
        {
            NPC.velocity.Y = -6.4f;
            NPC.velocity.X = NPC.spriteDirection * 3f;
            NPC.netUpdate = true;
        }

        // Jump if there's a gap closely ahead.
        bool gapAhead = !Collision.SolidTiles(NPC.Bottom + Vector2.UnitX * NPC.spriteDirection * 48f, 80, 32);
        if (gapAhead && onGround)
        {
            NPC.velocity.Y = -5f;
            NPC.velocity.X = NPC.spriteDirection * 4f;
        }

        // Walk up tiles.
        float _ = 0.3f;
        Collision.StepUp(ref NPC.position, ref NPC.velocity, NPC.width, NPC.height, ref _, ref NPC.gfxOffY);

        PerformStandardFraming();

        // Teleport to the tent if nobody is looking.
        if (!Main.player[Player.FindClosest(NPC.Center, 1, 1)].WithinRange(NPC.Center, 1900f) && SolynCampsiteWorldGen.TentPosition != Vector2.Zero && SolynCampsiteWorldGen.TentPosition != Vector2.One)
        {
            NPC.Center = SolynCampsiteWorldGen.TentPosition;
            NPC.netUpdate = true;
        }

        if (NPC.WithinRange(SolynCampsiteWorldGen.TentPosition, 40f))
            SwitchState(SolynAIType.Eepy);
        if (Main.dayTime)
            SwitchState(SolynAIType.StandStill);
    }

    public void DoBehavior_Eepy()
    {
        NPC.velocity.X *= 0.8f;
        Frame = 23;
        NPC.Center = SolynCampsiteWorldGen.TentPosition + Vector2.UnitX * 46f;
        NPC.Opacity = 0f;

        CanBeSpokenTo = false;

        if ((AITimer % 45f == 44f || Main.rand.NextBool(350)) && AITimer >= 300f)
        {
            Color zzzColor = Color.Lerp(new(255, 193, 40), new(255, 108, 174), EasingCurves.Cubic.Evaluate(EasingType.InOut, Main.rand.NextFloat()));

            Vector2 zzzVelocity = new Vector2(-Main.rand.NextFloat(1.1f, 2.4f), -Main.rand.NextFloat(6f, 8.5f));
            SleepParticle zzz = new SleepParticle(NPC.Top + new Vector2(-30f, -24f), zzzVelocity, zzzColor, 0.5f, Main.rand.Next(90, 150));
            zzz.Spawn();
        }

        if (!ShouldGoEepy)
            SwitchState(SolynAIType.StandStill);
    }
}
