using Luminance.Common.StateMachines;
using Microsoft.Xna.Framework;
using NoxusBoss.Assets;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using Terraria.Audio;
using Terraria.ModLoader;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity;

public partial class NamelessDeityBoss : ModNPC
{
    /// <summary>
    /// Whether Nameless should start a teleport this frame or not.
    /// </summary>
    public bool ShouldStartTeleportAnimation
    {
        get;
        set;
    }

    /// <summary>
    /// How long Nameless should spend disappearing at the start of his teleport.
    /// </summary>
    public int TeleportInTime
    {
        get;
        set;
    }

    /// <summary>
    /// How long Nameless should spend reappearing at the end of his teleport.
    /// </summary>
    public int TeleportOutTime
    {
        get;
        set;
    }

    /// <summary>
    /// Where Nameless should teleport himself to.
    /// </summary>
    public Func<Vector2> TeleportDestination
    {
        get;
        set;
    }

    /// <summary>
    /// How long Nameless' teleport animations typically last.
    /// </summary>
    public static int DefaultTeleportTime => GetAIInt("DefaultTeleportDuration");

    [AutomatedMethodInvoke]
    public void LoadStateTransitions_Teleport()
    {
        // Allow any attack to at any time raise the ShouldStartTeleportAnimation flag to start a teleport.
        // Once the teleport concludes, the previous attack is returned to where it was before.
        ApplyToAllStatesWithCondition(state =>
        {
            StateMachine.RegisterTransition(state, NamelessAIType.Teleport, true, () => ShouldStartTeleportAnimation, () =>
            {
                ShouldStartTeleportAnimation = false;
                AITimer = 0;
            });
        }, _ => true);

        // Return to the original attack after the teleport.
        StateMachine.RegisterTransition(NamelessAIType.Teleport, null, false, () =>
        {
            return AITimer >= TeleportInTime + TeleportOutTime;
        }, () =>
        {
            TeleportInTime = DefaultTeleportTime / 2;
            TeleportOutTime = DefaultTeleportTime / 2;
            TeleportVisualsInterpolant = 0f;
        });

        // Load the AI state behavior.
        StateMachine.RegisterStateBehavior(NamelessAIType.Teleport, DoBehavior_Teleport);
    }

    public void DoBehavior_Teleport()
    {
        // Manipulate the teleport visuals.
        TeleportVisualsInterpolant = InverseLerp(0f, TeleportInTime, AITimer) * 0.5f + InverseLerp(0f, TeleportOutTime, AITimer - TeleportInTime) * 0.5f;

        // Close the boss HP bar.
        CalamityCompatibility.MakeCalamityBossBarClose(NPC);

        // Play the teleport in sound.
        if (AITimer == 1)
            SoundEngine.PlaySound(GennedAssets.Sounds.Common.TeleportIn with { Volume = 0.65f, MaxInstances = 5, PitchVariance = 0.16f });

        // Update the position and create teleport visuals once the teleport is at the midpoint of the animation.
        if (AITimer == TeleportInTime && TeleportDestination is not null)
            ImmediateTeleportTo(TeleportDestination());

        // Disable damage.
        NPC.damage = 0;
    }
}
