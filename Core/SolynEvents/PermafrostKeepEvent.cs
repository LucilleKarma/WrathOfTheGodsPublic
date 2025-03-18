using Microsoft.Xna.Framework;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Core.DialogueSystem;
using NoxusBoss.Core.Pathfinding;
using NoxusBoss.Core.World.Subworlds;
using NoxusBoss.Core.World.WorldGeneration;
using SubworldLibrary;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Core.SolynEvents;

public class PermafrostKeepEvent : SolynEvent
{
    public override int TotalStages => 3;

    public static bool CanStart => ModContent.GetInstance<StargazingEvent>().Finished && PermafrostKeepWorldGen.PlayerGivenKey;

    public override void OnModLoad()
    {
        // Part 1.
        DialogueManager.RegisterNew("DormantKeyDiscussion", "Start").
            LinkFromStartToFinish().
            MakeSpokenByPlayer("StartingQuestion", "Conversation4").
            WithAppearanceCondition(instance => CanStart).
            WithRerollCondition(_ => Stage >= 1);
        DialogueManager.FindByRelativePrefix("DormantKeyDiscussion").GetByRelativeKey("Conversation8").EndAction += seenBefore =>
        {
            Main.LocalPlayer.SetTalkNPC(-1);

            SafeSetStage(1);
            if (Solyn is not null)
            {
                Solyn.NPC.spriteDirection = -1;
                Solyn.NPC.velocity.X = 0f;
                Solyn.SwitchState(SolynAIType.PuppeteeredByQuest);
            }
        };

        // Part 2.
        DialogueManager.RegisterNew("PermafrostKeepDiscussion_BeforeOpening", "Start").
            LinkFromStartToFinish().
            WithAppearanceCondition(instance => Stage == 1).
            WithRerollCondition(_ => Stage >= 2);

        // Part 3.
        DialogueManager.RegisterNew("PermafrostKeepDiscussion_AtSeed", "Start").
            LinkFromStartToFinish().
            MakeSpokenByPlayer("Response1").
            WithAppearanceCondition(instance => Stage >= 2).
            WithRerollCondition(_ => Finished);
        DialogueManager.FindByRelativePrefix("PermafrostKeepDiscussion_AtSeed").GetByRelativeKey("Conversation6").EndAction += seenBefore =>
        {
            SafeSetStage(3);
            if (Solyn is not null)
                Solyn.SwitchState(SolynAIType.WaitToTeleportHome);
        };

        ConversationSelector.PriorityConversationSelectionEvent += SelectKeepDialogue;
    }

    private Conversation? SelectKeepDialogue()
    {
        if (SubworldSystem.IsActive<EternalGardenNew>())
            return null;

        if (!CanStart || Finished)
            return null;

        if (Stage == 0)
            return DialogueManager.FindByRelativePrefix("DormantKeyDiscussion");
        if (Stage == 1)
            return DialogueManager.FindByRelativePrefix("PermafrostKeepDiscussion_BeforeOpening");
        if (Stage == 2)
            return DialogueManager.FindByRelativePrefix("PermafrostKeepDiscussion_AtSeed");

        return null;
    }

    public override void PostUpdateNPCs()
    {
        if (Solyn is null)
            return;

        // If the door was unlocked already and Solyn is waiting where the door would be, just transition to the next stage.
        // This is important for old worlds where the door was already unlocked by the Solyn questline is incomplete.
        // Patch fix 3/17/2025: ONLY do this if Solyn is near the actual door, otherwise she will attempt to pathfind to the door from miles away and cause utterly insufferable lag.
        if (Stage == 1 && PermafrostKeepWorldGen.DoorHasBeenUnlocked)
        {
            bool nearKeep = Solyn.NPC.WithinRange(PermafrostKeepWorldGen.KeepArea.Center().ToWorldCoordinates(), 2300f);
            if (nearKeep)
                SafeSetStage(2);
        }

        switch (Stage)
        {
            case 1:
                PuppeteerSolyn_TeleportToKeep(Solyn);
                break;
            case 2:
                PuppeteerSolyn_NavigateToSeed(Solyn);
                break;
        }
    }

    private static void PuppeteerSolyn_TeleportToKeep(Solyn solyn)
    {
        NPC npc = solyn.NPC;

        if (solyn.AITimer == 60)
        {
            Vector2 keepPosition = PermafrostKeepWorldGen.KeepArea.Center() * 16f + new Vector2(-524f, 48f);
            solyn.TeleportTo(keepPosition);
            npc.spriteDirection = 1;
        }

        npc.velocity.X = 0f;

        Player player = Main.player[Player.FindClosest(npc.Center, 1, 1)];
        if (Collision.CanHit(npc, player))
            npc.spriteDirection = npc.HorizontalDirectionTo(player.Center).NonZeroSign();
        else
            npc.spriteDirection = 1;

        if (solyn.AITimer >= 60)
            solyn.CanBeSpokenTo = false;
    }

    private static void PuppeteerSolyn_NavigateToSeed(Solyn solyn)
    {
        NPC npc = solyn.NPC;

        Vector2 walkDestination = PermafrostKeepWorldGen.KeepArea.Center() * 16f + new Vector2(40f, 322f);
        List<Vector2> path = AStarPathfinding.PathfindThroughTiles(npc.Center - Vector2.UnitX * npc.spriteDirection * 10f, walkDestination, point =>
        {
            if (!WorldGen.InWorld(point.X, point.Y, 20))
                return 100f;

            return 0f;
        });
        Vector2 aheadPathfindingPoint = walkDestination;
        if (path.Count >= 6)
            aheadPathfindingPoint = path[5];

        bool reachedSeed = npc.WithinRange(walkDestination, 80f);

        if (Abs(npc.velocity.X) >= 0.1f)
            npc.spriteDirection = npc.velocity.X.NonZeroSign();

        solyn.DescendThroughSlopes = true;
        solyn.CanBeSpokenTo = reachedSeed;

        npc.velocity.X = Lerp(npc.velocity.X, npc.SafeDirectionTo(aheadPathfindingPoint).X * 1.8f, 0.012f);
        if (reachedSeed)
            npc.spriteDirection = npc.HorizontalDirectionTo(walkDestination).NonZeroSign();

        InteractWithDoors(solyn);
        solyn.PerformStandardFraming();
    }

    private static void InteractWithDoors(Solyn solyn)
    {
        NPC npc = solyn.NPC;
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        int checkX = (int)((npc.Center.X + npc.spriteDirection * 15f) / 16f);
        int checkY = (int)(npc.Bottom.Y / 16f) - 3;
        Tile checkTile = Framing.GetTileSafely(checkX, checkY);

        if (checkTile.HasUnactuatedTile && (checkTile.TileType == TileID.ClosedDoor || TileID.Sets.OpenDoorID[checkTile.TileType] != -1))
        {
            if (WorldGen.OpenDoor(checkX, checkY, -npc.spriteDirection))
            {
                npc.closeDoor = true;
                npc.doorX = checkX;
                npc.doorY = checkY;
                NetMessage.SendData(MessageID.ToggleDoorState, -1, -1, null, 0, checkX, checkY, -npc.spriteDirection);
            }
            else if (WorldGen.OpenDoor(checkX, checkY, npc.spriteDirection))
            {
                npc.closeDoor = true;
                npc.doorX = checkX;
                npc.doorY = checkY;
                NetMessage.SendData(MessageID.ToggleDoorState, -1, -1, null, 0, checkX, checkY, npc.spriteDirection);
            }
        }

        Vector2 doorPosition = new Vector2(npc.doorX, npc.doorY).ToWorldCoordinates();
        if (!npc.WithinRange(doorPosition, 60f))
        {
            Tile doorTile = Framing.GetTileSafely(npc.doorX, npc.doorY);
            if (doorTile.HasUnactuatedTile && (doorTile.TileType == TileID.OpenDoor || TileID.Sets.CloseDoorID[doorTile.TileType] != -1) && WorldGen.CloseDoor(npc.doorX, npc.doorY))
            {
                npc.closeDoor = false;
                NetMessage.SendData(MessageID.ToggleDoorState, -1, -1, null, 0, npc.doorX, npc.doorY, npc.spriteDirection);
            }
        }
    }
}
