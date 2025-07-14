using Luminance.Common.Easings;
using Luminance.Core.Cutscenes;

using Microsoft.Xna.Framework;

using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Core.Netcode;
using NoxusBoss.Core.Netcode.Packets;
using NoxusBoss.Core.SoundSystems;
using NoxusBoss.Core.World.WorldGeneration;

using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Core.World.GameScenes.Stargazing;

public class BecomeDuskScene : Cutscene
{
    /// <summary>
    /// The <see cref="Main.time"/> value at the start of this scene.
    /// </summary>
    public static int TimeAtStartOfAnimation
    {
        get;
        internal set;
    }

    /// <summary>
    /// The index of the player that started the cutscene.
    /// </summary>
    public static int CutsceneStarterPlayerIndex
    {
        get;
        set;
    } = -1;

    /// <summary>
    /// The screen position at the start of the animation.
    /// </summary>
    public static Vector2 OriginalScreenPosition
    {
        get;
        set;
    }

    /// <summary>
    /// The action that should be performed when this scene ends.
    /// </summary>
    public static Action? EndAction
    {
        get;
        set;
    }

    /// <summary>
    /// The sound that plays during this cutscene.
    /// </summary>
    public static LoopedSoundInstance ClockTickSound
    {
        get;
        set;
    }

    public override int CutsceneLength => SecondsToFrames(7.5f);

    internal void SetActivity(bool value)
    {
        typeof(Cutscene).GetProperty("IsActive", UniversalBindingFlags)?.SetMethod?.Invoke(this, [value]);
    }

    public override void OnBegin()
    {
        OriginalScreenPosition = Main.screenPosition;
        if (CutsceneStarterPlayerIndex != -1) return;

        TimeAtStartOfAnimation = (int)Main.time;
        CutsceneStarterPlayerIndex = Main.myPlayer;
        if (Main.netMode != NetmodeID.SinglePlayer)
            PacketManager.SendPacket<DuskCutsceneTimePacket>();
    }

    public override void OnEnd()
    {
        ClockTickSound?.Stop();

        EndAction?.Invoke();
        EndAction = null;

        if (CutsceneStarterPlayerIndex == Main.myPlayer && Main.netMode != NetmodeID.SinglePlayer)
            PacketManager.SendPacket<DuskCutsceneTimePacket>();

        CutsceneStarterPlayerIndex = -1;
    }

    public override void Update()
    {
        if (!Main.dayTime)
        {
            EndAbruptly = true;
            return;
        }

        ClockTickSound ??= LoopedSoundManager.CreateNew(GennedAssets.Sounds.NamelessDeity.ClockTick with { Volume = 1.1f, IsLooped = true }, () => !IsActive);
        ClockTickSound?.Update(Main.screenPosition + Main.ScreenSize.ToVector2() * 0.5f);

        if (CutsceneStarterPlayerIndex == Main.myPlayer && Timer == CutsceneLength / 2)
        {
            Vector2 telescopePosition = SolynCampsiteWorldGen.TelescopePosition.ToWorldCoordinates(30f, 8f);
            Main.LocalPlayer.Bottom = FindGroundVertical(telescopePosition.ToTileCoordinates()).ToWorldCoordinates(8f, 0f);
            Main.LocalPlayer.direction = -1;

            int solynID = ModContent.NPCType<Solyn>();
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.type != solynID)
                    continue;

                npc.Bottom = FindGroundVertical((telescopePosition + Vector2.UnitX * 27f).ToTileCoordinates()).ToWorldCoordinates(8f, 0f);
                npc.direction = -1;
                npc.netUpdate = true;

                //As cutscene plays on a client & server handles the NPC's, we need to tell server to teleport her
                PacketManager.SendPacket<TeleportNPCPacket>(npc.whoAmI);
            }
        }

        Main.time = (int)Lerp(TimeAtStartOfAnimation, (int)Main.dayLength - 5, Pow(LifetimeRatio, 3.3f));
    }

    public override void ModifyScreenPosition()
    {
        if (CutsceneStarterPlayerIndex != Main.myPlayer) return;

        float moveBackInterpolant = InverseLerp(0.93f, 1f, LifetimeRatio);
        float moveUpInterpolant = InverseLerpBump(0f, 0.3f, 0.93f, 1f, LifetimeRatio);
        float moveUpOffset = EasingCurves.Quadratic.Evaluate(EasingType.InOut, 0f, 1f, moveUpInterpolant) * Main.screenHeight;
        Vector2 originalScreenPosition = Main.screenPosition;
        Main.screenPosition = Vector2.SmoothStep(OriginalScreenPosition - Vector2.UnitY * moveUpOffset, originalScreenPosition, moveBackInterpolant);
    }
}
