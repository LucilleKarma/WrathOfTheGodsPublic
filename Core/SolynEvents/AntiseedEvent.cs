using Luminance.Core.Cutscenes;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using NoxusBoss.Assets;
using NoxusBoss.Content.Items.GenesisComponents;
using NoxusBoss.Content.NPCs.Bosses.CeaselessVoid;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Core.CrossCompatibility.Inbound;
using NoxusBoss.Core.DialogueSystem;
using NoxusBoss.Core.World.GameScenes.AvatarUniverseExploration;
using NoxusBoss.Core.World.Subworlds;
using SubworldLibrary;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace NoxusBoss.Core.SolynEvents;

public class AntiseedEvent : SolynEvent
{
    /// <summary>
    /// Whether Solyn has played the rift enter sound or not.
    /// </summary>
    public static bool SolynHasPlayedRiftEnterSound
    {
        get;
        private set;
    }

    /// <summary>
    /// The place Solyn intends to wait before entering the rift.
    /// </summary>
    public static Vector2 WaitNearCeaselessVoidRift_WaitPosition
    {
        get;
        private set;
    }

    public override int TotalStages => 6;

    public static bool CanStart => ModContent.GetInstance<GenesisIntroductionEvent>().Finished && CommonCalamityVariables.ProvidenceDefeated;

    public override void OnModLoad()
    {
        // Part 1.
        Conversation part1 = DialogueManager.RegisterNew("CeaselessVoidDiscussionBeforeBattle", "Start").
            WithRootSelectionFunction(conversation =>
            {
                if (conversation.SeenBefore("Conversation9"))
                    return conversation.GetByRelativeKey("Conversation10");

                return conversation.GetByRelativeKey("Start");
            }).
            WithAppearanceCondition(instance => CanStart).
            WithRerollCondition(_ => Stage >= 1).
            MakeSpokenByPlayer("StartingQuestion", "ConversationQuestion1", "Conversation6ResponseYes", "Conversation6ResponseNo");

        part1.LinkChain("Start", "StartingQuestion", "Conversation1", "Conversation2", "Conversation3", "ConversationQuestion1", "Conversation4", "Conversation5", "Conversation6");
        part1.GetByRelativeKey("Conversation6").Children.Add(part1.GetByRelativeKey("Conversation6ResponseYes"));
        part1.GetByRelativeKey("Conversation6").Children.Add(part1.GetByRelativeKey("Conversation6ResponseNo"));
        part1.LinkChain("Conversation6ResponseYes", "Conversation7", "Conversation8", "Conversation9", "Conversation10");
        part1.LinkChain("Conversation6ResponseNo", "ConversationRejection");

        // Part 2.
        DialogueManager.RegisterNew("CeaselessVoidDiscussionAfterBattle", "Start").
            LinkFromStartToFinishExcluding("Repeat").
            MakeSpokenByPlayer("Conversation1", "ConversationResponse1", "ConversationResponse2").
            WithAppearanceCondition(instance => Stage == 1).
            WithRerollCondition(_ => Stage >= 2);
        DialogueManager.FindByRelativePrefix("CeaselessVoidDiscussionAfterBattle").GetByRelativeKey("Conversation8").EndAction = seenBefore =>
        {
            SafeSetStage(2);
        };

        // Part 3.
        DialogueManager.RegisterNew("CeaselessVoidDiscussionBeforeEnteringRift", "Start").
            LinkFromStartToFinish().
            MakeSpokenByPlayer("Confirmation").
            WithAppearanceCondition(instance => Stage == 2).
            WithRerollCondition(_ => Stage >= 4);
        DialogueManager.FindByRelativePrefix("CeaselessVoidDiscussionBeforeEnteringRift").GetByRelativeKey("Start").EndAction = seenBefore =>
        {
            BlockerSystem.Start(true, false, () => !Finished);
        };
        DialogueManager.FindByRelativePrefix("CeaselessVoidDiscussionBeforeEnteringRift").GetByRelativeKey("Conversation3").ClickAction = seenBefore =>
        {
            if (Solyn is not null)
            {
                WaitNearCeaselessVoidRift_WaitPosition = Solyn.NPC.Center;
                Solyn.AITimer = 0;
                Solyn.NPC.netUpdate = true;
            }
            SafeSetStage(3);
        };

        // Part 4.
        DialogueManager.RegisterNew("CeaselessVoidDiscussionAfterEnteringRift", "Start").
            LinkFromStartToFinish().
            MakeSpokenByPlayer("Question1", "Question2").
            WithAppearanceCondition(instance => Stage == 5).
            WithRerollCondition(_ => Finished);
        DialogueManager.FindByRelativePrefix("CeaselessVoidDiscussionAfterEnteringRift").GetByRelativeKey("Conversation6").ClickAction = seenBefore =>
        {
            if (!seenBefore)
                DialogueSaveSystem.GiveItemToPlayer<TheAntiseed>(Main.LocalPlayer);
        };
        DialogueManager.FindByRelativePrefix("CeaselessVoidDiscussionAfterEnteringRift").GetByRelativeKey("Conversation7").EndAction = seenBefore =>
        {
            SafeSetStage(6);
            Solyn?.SwitchState(SolynAIType.WaitToTeleportHome);

            // Desperation.
            if (!DialogueSaveSystem.ItemHasBeenGiven<TheAntiseed>())
                DialogueSaveSystem.GiveItemToPlayer<TheAntiseed>(Main.LocalPlayer);
        };

        ConversationSelector.PriorityConversationSelectionEvent += SelectAntiseedDialogue;
    }

    public override void OnWorldLoad() => SolynHasPlayedRiftEnterSound = false;

    public override void OnWorldUnload() => SolynHasPlayedRiftEnterSound = false;

    public override void SaveWorldData(TagCompound tag)
    {
        tag["WaitNearCeaselessVoidRift_WaitPositionX"] = WaitNearCeaselessVoidRift_WaitPosition.X;
        tag["WaitNearCeaselessVoidRift_WaitPositionY"] = WaitNearCeaselessVoidRift_WaitPosition.Y;
    }

    public override void LoadWorldData(TagCompound tag) =>
        WaitNearCeaselessVoidRift_WaitPosition = new Vector2(tag.GetFloat("WaitNearCeaselessVoidRift_WaitPositionX"), tag.GetFloat("WaitNearCeaselessVoidRift_WaitPositionY"));

    private Conversation? SelectAntiseedDialogue()
    {
        if (SubworldSystem.IsActive<EternalGardenNew>())
            return null;

        if (CanStart && Stage == 0)
            return DialogueManager.FindByRelativePrefix("CeaselessVoidDiscussionBeforeBattle");
        if (Stage == 1)
            return DialogueManager.FindByRelativePrefix("CeaselessVoidDiscussionAfterBattle");
        if (Stage == 2)
            return DialogueManager.FindByRelativePrefix("CeaselessVoidDiscussionBeforeEnteringRift");
        if (Stage == 5)
            return DialogueManager.FindByRelativePrefix("CeaselessVoidDiscussionAfterEnteringRift");

        return null;
    }

    public override void PostUpdateEverything()
    {
        if (Stage == 0 && CommonCalamityVariables.CeaselessVoidDefeated && DialogueManager.FindByRelativePrefix("CeaselessVoidDiscussionBeforeBattle").SeenBefore("Conversation9") && CanStart)
            SafeSetStage(1);

        // No.
        if (Stage >= 2 && !Finished)
            AvatarUniverseExplorationSystem.ClearDungeonEnemies();

        if (Solyn is not null)
        {
            if (Stage == 2)
                MakeSolynGoToRift(Solyn);
            if (Stage == 3)
                MakeSolynEnterRift(Solyn);
            if (Stage == 4)
                MakeSolynExitRift(Solyn);
            if (Stage == 5)
                Solyn?.PerformStandardFraming();
        }
    }

    private static void MakeSolynGoToRift(Solyn solyn)
    {
        int riftIndex = NPC.FindFirstNPC(ModContent.NPCType<CeaselessVoidRift>());
        if (riftIndex == -1)
            return;

        NPC rift = Main.npc[riftIndex];
        NPC npc = solyn.NPC;
        Player nearest = Main.player[Player.FindClosest(npc.Center, 1, 1)];

        if (solyn.CurrentState != SolynAIType.PuppeteeredByQuest)
            solyn.SwitchState(SolynAIType.PuppeteeredByQuest);

        if (!npc.WithinRange(rift.Center, 1600f))
        {
            float flySpeedInterpolant = InverseLerp(0f, 60f, solyn.AITimer).Squared() * 0.1f;
            Vector2 flyDestination = new Vector2(Main.dungeonX, Main.dungeonY) * 16f;

            npc.noTileCollide = true;
            npc.velocity = Vector2.Lerp(npc.velocity, npc.SafeDirectionTo(flyDestination) * 14f, flySpeedInterpolant);

            float riseUpwardAcceleration = InverseLerp(600f, 1800f, npc.Distance(flyDestination)) * 1.2f;
            if (Collision.SolidCollision(npc.TopLeft - Vector2.UnitX * 200f, npc.width + 400, npc.height + 200) || !Collision.CanHit(npc.Center, 1, 1, npc.Center + Vector2.UnitX * npc.spriteDirection * 800f, 1, 1))
                npc.velocity.Y -= riseUpwardAcceleration;

            solyn.UseStarFlyEffects();
            npc.spriteDirection = npc.velocity.X.NonZeroSign();

            if (!npc.WithinRange(nearest.Center, 3032f))
            {
                Vector2 riftPosition = rift.Center;
                Vector2 teleportPosition = riftPosition;

                for (int i = 0; i < 50; i++)
                {
                    Vector2 teleportOffset = Vector2.UnitX * Main.rand.NextFloatDirection() * 200f;
                    teleportPosition = FindGround((riftPosition + teleportOffset).ToTileCoordinates(), Vector2.UnitY).ToWorldCoordinates(8f, 0f);

                    if (teleportPosition.WithinRange(riftPosition, 350f) && Distance(teleportPosition.Y, teleportPosition.Y) <= 92f)
                        break;
                }

                solyn.TeleportTo(teleportPosition);

                npc.noTileCollide = false;
                npc.noGravity = false;
            }
        }

        else
        {
            npc.velocity.X = 0f;
            solyn.PerformStandardFraming();
            solyn.CanBeSpokenTo = true;
        }
    }

    private static void MakeSolynEnterRift(Solyn solyn)
    {
        int riftIndex = NPC.FindFirstNPC(ModContent.NPCType<CeaselessVoidRift>());
        if (riftIndex == -1)
            return;

        NPC rift = Main.npc[riftIndex];
        NPC npc = solyn.NPC;

        float flySpeedInterpolant = InverseLerp(0f, 60f, solyn.AITimer).Squared();
        Vector2 flyDestination = rift.Center;
        Vector2 idealVelocity = npc.SafeDirectionTo(flyDestination) * flySpeedInterpolant * 12f;
        if (npc.WithinRange(flyDestination, 20f))
            npc.velocity = Vector2.Zero;
        else
        {
            npc.velocity = Vector2.Lerp(npc.velocity, idealVelocity, flySpeedInterpolant * 0.1f);
            npc.spriteDirection = npc.velocity.X.NonZeroSign();
        }

        npc.Opacity = InverseLerp(36f, 90f, npc.Distance(flyDestination));
        npc.scale = Pow(npc.Opacity, 1.5f);
        npc.noTileCollide = true;
        npc.noGravity = true;

        // Update the event once the cutscene finishes.
        if (SolynHasPlayedRiftEnterSound && !CutsceneManager.IsActive(ModContent.GetInstance<SolynEnteringRiftScene>()) && solyn.AITimer >= 30)
        {
            ModContent.GetInstance<AntiseedEvent>().SafeSetStage(4);
            solyn.AITimer = 0;
            npc.netUpdate = true;
        }

        if (!SolynHasPlayedRiftEnterSound)
            solyn.UseStarFlyEffects();

        if (solyn.AITimer < 5)
            SolynHasPlayedRiftEnterSound = false;
        else if (!SolynHasPlayedRiftEnterSound && npc.WithinRange(flyDestination, 120f))
        {
            ScreenShakeSystem.StartShakeAtPoint(npc.Center, 5f);
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.RiftPlayerAbsorb, npc.Center);
            SolynHasPlayedRiftEnterSound = true;

            CutsceneManager.QueueCutscene(ModContent.GetInstance<SolynEnteringRiftScene>());
        }
    }

    private static void MakeSolynExitRift(Solyn solyn)
    {
        NPC npc = solyn.NPC;
        Player nearestPlayer = Main.player[Player.FindClosest(npc.Center, 1, 1)];
        if (solyn.AITimer == 1)
        {
            ScreenShakeSystem.StartShakeAtPoint(npc.Center, 6f);
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.RiftShoot with { MaxInstances = 8, Volume = 0.6f }, npc.Center);

            // Fly out of the rift in a hurry, and with reduced health.
            npc.life = (int)(npc.lifeMax * 0.51f);
            npc.velocity.X += npc.HorizontalDirectionTo(WaitNearCeaselessVoidRift_WaitPosition) * 56f;
            npc.netUpdate = true;
        }

        npc.scale = Saturate(npc.scale + 0.05f);
        npc.Opacity = Saturate(npc.Opacity + 0.075f);

        npc.velocity = Vector2.Lerp(npc.velocity, npc.SafeDirectionTo(WaitNearCeaselessVoidRift_WaitPosition) * 7.5f, 0.08f);
        npc.spriteDirection = npc.velocity.X.NonZeroSign();

        npc.noTileCollide = true;
        npc.noGravity = true;

        solyn.UseStarFlyEffects();
        solyn.Frame = 43;

        if (npc.WithinRange(WaitNearCeaselessVoidRift_WaitPosition, 18f) && solyn.AITimer >= 10)
        {
            ModContent.GetInstance<AntiseedEvent>().SafeSetStage(5);
            nearestPlayer.SetTalkNPC(npc.whoAmI);
            solyn.AITimer = 0;
            solyn.ShockedExpressionCountdown = SecondsToFrames(1.5f);
            npc.velocity = Vector2.Zero;
            npc.noTileCollide = false;
            npc.noGravity = true;
            npc.netUpdate = true;
        }
    }
}
