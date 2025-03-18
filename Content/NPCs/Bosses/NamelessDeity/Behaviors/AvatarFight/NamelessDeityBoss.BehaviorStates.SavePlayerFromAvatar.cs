using Luminance.Common.Easings;
using Luminance.Common.StateMachines;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Rendering.RenderSteps;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity;

public partial class NamelessDeityBoss : ModNPC
{
    [AutomatedMethodInvoke]
    public void LoadStateTransitions_SavePlayerFromAvatar()
    {
        // Load the AI state behavior.
        StateMachine.RegisterStateBehavior(NamelessAIType.SavePlayerFromAvatar, DoBehavior_SavePlayerFromAvatar);
    }

    public void DoBehavior_SavePlayerFromAvatar()
    {
        // Disappear if the Avatar is gone.
        if (AvatarOfEmptiness.Myself is null)
        {
            NPC.active = false;
            return;
        }

        // Instruct the render composite to use alpha blending instead of non-premultiplication blending, due to being drawn as a silhouette.
        // Not doing this reveals the outlines on Nameless' parts, which is not ideal when he's supposed to be obscured.
        if (Main.netMode != NetmodeID.Server)
            RenderComposite.BlendState = BlendState.AlphaBlend;

        AITimer = AvatarOfEmptiness.Myself.As<AvatarOfEmptiness>().AITimer;
        NPC.Center = Vector2.Lerp(NPC.Center, Target.Center - Vector2.UnitY * 300f, 0.06f);
        NPC.velocity = Vector2.Zero;
        NPC.dontTakeDamage = true;

        // To ensure that Nycro's nohit mod doesn't count this cutscene version of Nameless as a boss, disable the boss flag.
        NPC.boss = false;

        if (AvatarOfEmptiness.Myself.As<AvatarOfEmptiness>().CurrentState == AvatarOfEmptiness.AvatarAIType.ParadiseReclaimed_NamelessDispelsStatic)
        {
            ZPosition = EasingCurves.Quartic.Evaluate(EasingType.InOut, 0f, 3f, Pow(InverseLerp(120f, 0f, AITimer), 0.75f));
            NPC.Opacity = 1f;
        }
        else
        {
            ZPosition = Lerp(ZPosition, 21f, 0.031f);
            NPC.Opacity = 0f;
        }

        // Rapidly unfold the vines.
        if (AITimer <= 11)
        {
            for (int i = 0; i < 20; i++)
                RenderComposite.Find<DanglingVinesStep>().HandleDanglingVineRotation(NPC);
        }

        DefaultUniversalHandMotion();
        UpdateWings(AITimer / 45f);

        TargetClosest();

        CalamityCompatibility.MakeCalamityBossBarClose(NPC);
    }
}
