using NoxusBoss.Content.HairStyles;
using NoxusBoss.Content.NPCs.Enemies.RiftEclipse;
using NoxusBoss.Core.CrossCompatibility.Inbound;
using NoxusBoss.Core.Graphics.UI.Books;
using NoxusBoss.Core.World;
using NoxusBoss.Core.World.GameScenes.RiftEclipse;
using NoxusBoss.Core.World.WorldGeneration;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Core.DialogueSystem;

public class RandomConversationRegistrar : ModSystem
{
    /// <summary>
    /// Whether it's currently dawn in the world.
    /// </summary>
    private static bool IsDawn => Main.dayTime && Main.time <= 10800;

    /// <summary>
    /// Whether it's currently the middle of the day in the world.
    /// </summary>
    private static bool IsMidDay => Main.dayTime && Main.time > 10800 && Main.time <= 47800;

    /// <summary>
    /// Whether it's currently dusk in the world.
    /// </summary>
    private static bool IsDusk => Main.dayTime && Main.time > 47800;

    public override void OnModLoad()
    {
        RegisterDawnDialogue();
        RegisterSunnyDayDialogue();
        RegisterRainyDayDialogue();
        RegisterDuskDialogue();
        RegisterBloodMoonDialogue();
        RegisterSlimeRainDialogue();
        RegisterStorageSailsDialogue();
        RegisterBooksDialogue();
        RegisterHairDialogue();
        RegisterPicnicDialogue();
        RegisterNearbyLunarPillarDialogue();

        RegisterBookshelfAcknowledgementDialogue();

        RegisterRiftBlizzardDialogue();
        RegisterWantingToGoHomeDialogue();
        RegisterRiftSunsetDialogue();
        RegisterRiftTownNPCDialogue();
        RegisterHopefulPastDialogue();
        RegisterRiftFogDialogue();

        RegisterLategameDialogue();
    }

    private static void RegisterDawnDialogue()
    {
        Conversation dawnDialogue = DialogueManager.RegisterNew("SolynDawnDialogue", "Start").
            WithAppearanceCondition(conversation => IsDawn && !RiftEclipseManagementSystem.RiftEclipseOngoing).
            WithRerollCondition(conversation => !conversation.AppearanceCondition()).
            MakeSpokenByPlayer("Player1", "Player2", "Player3", "Player4").
            MakeFallback();

        dawnDialogue.LinkChain("Start", "Player1", "Solyn1", "Player2", "Solyn2", "Player3");

        int totalDreams = 5;
        for (int i = 0; i < totalDreams; i++)
        {
            int copyForDelegate = i;
            dawnDialogue.LinkChain("Player3", $"Dream{i + 1}_Response1");
            dawnDialogue.GetByRelativeKey($"Dream{i + 1}_Response1").SelectionCondition = () => DaysCounterSystem.DayCounter % totalDreams == copyForDelegate;

            dawnDialogue.LinkChain($"Dream{i + 1}_Response1", $"Dream{i + 1}_Response2", $"Dream{i + 1}_Response3", $"Dream{i + 1}_Response4", $"Dream{i + 1}_Response5", $"Dream{i + 1}_Response6", "Player4");
        }

        dawnDialogue.LinkChain("Player4", "Solyn3");
    }

    private static void RegisterSunnyDayDialogue()
    {
        DialogueManager.RegisterNew("SolynSunnyDayDialogue", "Start").
            WithAppearanceCondition(conversation => IsMidDay && !Main.raining && !RiftEclipseManagementSystem.RiftEclipseOngoing).
            WithRerollCondition(conversation => !conversation.AppearanceCondition()).
            MakeSpokenByPlayer("Player1", "Player2").
            LinkFromStartToFinish().
            MakeFallback();
    }

    private static void RegisterRainyDayDialogue()
    {
        DialogueManager.RegisterNew("SolynRainyDayDialogue", "Start").
            WithAppearanceCondition(conversation => IsMidDay && Main.raining && !RiftEclipseManagementSystem.RiftEclipseOngoing).
            WithRerollCondition(conversation => !conversation.AppearanceCondition()).
            MakeSpokenByPlayer("Player1", "Player2").
            LinkFromStartToFinish().
            MakeFallback();
    }

    private static void RegisterDuskDialogue()
    {
        DialogueManager.RegisterNew("SolynDuskDialogue", "Start").
            WithAppearanceCondition(conversation => IsDusk && !RiftEclipseManagementSystem.RiftEclipseOngoing).
            WithRerollCondition(conversation => !conversation.AppearanceCondition()).
            LinkFromStartToFinish().
            MakeFallback();
    }

    private static void RegisterBloodMoonDialogue()
    {
        DialogueManager.RegisterNew("SolynBloodMoonDialogue", "Start").
            WithAppearanceCondition(conversation => Main.bloodMoon).
            WithRerollCondition(conversation => !conversation.AppearanceCondition()).
            LinkFromStartToFinish().
            MakeFallback(priority: 3);
    }

    private static void RegisterSlimeRainDialogue()
    {
        DialogueManager.RegisterNew("SolynSlimeRainDialogue", "Start").
            WithAppearanceCondition(conversation => Main.slimeRain && !RiftEclipseManagementSystem.RiftEclipseOngoing).
            WithRerollCondition(conversation => !conversation.AppearanceCondition()).
            MakeSpokenByPlayer("Player1").
            LinkFromStartToFinish().
            MakeFallback(priority: 2);
    }

    private static void RegisterStorageSailsDialogue()
    {
        DialogueManager.RegisterNew("SolynStorageSailsDialogue", "Start").
            WithAppearanceCondition(conversation => Main.rand.NextBool(3) && !RiftEclipseManagementSystem.RiftEclipseOngoing).
            WithRerollCondition(conversation => Main.rand.NextBool(1800) || RiftEclipseManagementSystem.RiftEclipseOngoing).
            MakeSpokenByPlayer("Player1", "Player2").
            LinkFromStartToFinish().
            MakeFallback();
    }

    private static void RegisterBooksDialogue()
    {
        static bool PlayerIsProbablyMage()
        {
            Item[] weapons = new Span<Item>(Main.LocalPlayer.inventory, 0, 39).ToArray().Where(i => i.axe <= 0 && i.pick <= 0 && i.hammer <= 0 && i.damage >= 1).OrderByDescending(i => i.damage).ToArray();
            bool playerIsProbablyMage = false;
            for (int i = 0; i < Math.Min(3, weapons.Length); i++)
            {
                if (weapons[i].DamageType == DamageClass.Magic)
                {
                    playerIsProbablyMage = true;
                    break;
                }
            }

            return playerIsProbablyMage;
        }

        Conversation bookDialogue = DialogueManager.RegisterNew("SolynBooksDialogue", "Start").
            WithAppearanceCondition(conversation => Main.rand.NextBool(3)).
            WithRerollCondition(conversation => Main.rand.NextBool(1800)).
            MakeSpokenByPlayer("Player1_Mage").
            MakeFallback();

        bookDialogue.LinkChain("Start", "Solyn1", "Solyn2", "Solyn3");
        bookDialogue.LinkChain("Solyn3", "Solyn4_Mage");
        bookDialogue.LinkChain("Solyn3", "Solyn4_OtherClass");

        bookDialogue.LinkChain("Solyn4_Mage", "Player1_Mage", "Solyn5");
        bookDialogue.LinkChain("Solyn4_OtherClass", "Solyn5");

        bookDialogue.GetByRelativeKey("Solyn4_Mage").SelectionCondition = PlayerIsProbablyMage;
        bookDialogue.GetByRelativeKey("Solyn4_OtherClass").SelectionCondition = () => !PlayerIsProbablyMage();
    }

    private static void RegisterHairDialogue()
    {
        DialogueManager.RegisterNew("SolynSameHairDialogue", "Start").
            WithAppearanceCondition(conversation =>
            {
                if (conversation.SeenBefore("Solyn2"))
                    return false;

                Player p = Main.LocalPlayer;
                bool hairIsVisible = p.head <= 0;
                return hairIsVisible && p.hair == ModContent.GetInstance<SolynHairStyle>().Type;
            }).
            WithRerollCondition(conversation => Main.rand.NextBool(1800) || !conversation.AppearanceCondition()).
            LinkFromStartToFinish().
            MakeFallback(priority: 2);
    }

    private static void RegisterPicnicDialogue()
    {
        DialogueManager.RegisterNew("SolynPicnicDialogue", "Start").
            WithAppearanceCondition(conversation => Main.rand.NextBool(3) && !RiftEclipseManagementSystem.RiftEclipseOngoing).
            WithRerollCondition(conversation => Main.rand.NextBool(1800) || RiftEclipseManagementSystem.RiftEclipseOngoing).
            LinkFromStartToFinish().
            MakeFallback();
    }

    private static void RegisterNearbyLunarPillarDialogue()
    {
        DialogueManager.RegisterNew("SolynNearbyLunarPillarDialogue", "Start").
            WithAppearanceCondition(conversation =>
            {
                bool pillarNearTent = false;
                foreach (NPC npc in Main.ActiveNPCs)
                {
                    if (npc.aiStyle == NPCAIStyleID.CelestialPillar && npc.WithinRange(SolynCampsiteWorldGen.TentPosition, 3900f))
                    {
                        pillarNearTent = true;
                        break;
                    }
                }

                return pillarNearTent;
            }).
            WithRerollCondition(conversation => !conversation.AppearanceCondition()).
            LinkFromStartToFinish().
            MakeFallback(priority: 6);
    }

    private static void RegisterBookshelfAcknowledgementDialogue()
    {
        DialogueManager.RegisterNew("SolynOneRedeemedBookDialogue", "Start").
            WithAppearanceCondition(conversation => SolynBookExchangeRegistry.TotalRedeemedBooks >= 1).
            WithRerollCondition(conversation => conversation.SeenBefore("Solyn1")).
            LinkFromStartToFinish().
            MakeFallback(priority: 5);

        DialogueManager.RegisterNew("SolynInscrutableTextsBookDialogue", "Start").
            WithAppearanceCondition(conversation => SolynBookExchangeRegistry.RedeemedBooks.Contains("InscrutableTexts")).
            WithRerollCondition(conversation => conversation.SeenBefore("Solyn3")).
            LinkFromStartToFinish().
            MakeFallback(priority: 1);

        DialogueManager.RegisterNew("SolynUnreadableBookDialogue", "Start").
            WithAppearanceCondition(conversation => SolynBookExchangeRegistry.RedeemedBooks.Contains("BookOfMiracles")).
            WithRerollCondition(conversation => conversation.SeenBefore("Solyn1")).
            LinkFromStartToFinish().
            MakeFallback(priority: 1);

        DialogueManager.RegisterNew("SolynManyRedeemedBooksDialogue", "Start").
            WithAppearanceCondition(conversation =>
            {
                bool redeemedEnoughBooks = SolynBookExchangeRegistry.TotalRedeemedBooks >= SolynBookExchangeRegistry.ObtainableBooks.Count / 2;
                bool seenPriorDialogue = DialogueManager.FindByRelativePrefix("SolynOneRedeemedBookDialogue").SeenBefore("Solyn1");
                return redeemedEnoughBooks && seenPriorDialogue;
            }).
            WithRerollCondition(conversation => conversation.SeenBefore("Solyn1")).
            LinkFromStartToFinish().
            MakeFallback(priority: 5);

        DialogueManager.RegisterNew("SolynCompleteBookshelfDialogue", "Start").
            WithAppearanceCondition(conversation =>
            {
                bool redeemedEnoughBooks = SolynBookExchangeRegistry.RedeemedAllBooks;
                bool seenPriorDialogue = DialogueManager.FindByRelativePrefix("SolynManyRedeemedBooksDialogue").SeenBefore("Solyn1");
                return redeemedEnoughBooks && seenPriorDialogue;
            }).
            WithRerollCondition(conversation => conversation.SeenBefore("Solyn2")).
            LinkFromStartToFinish().
            MakeFallback(priority: 5);
    }

    private static void RegisterRiftBlizzardDialogue()
    {
        DialogueManager.RegisterNew("SolynRiftBlizzardDialogue", "Start").
            WithAppearanceCondition(conversation => Main.raining && RiftEclipseManagementSystem.RiftEclipseOngoing).
            WithRerollCondition(conversation => Main.rand.NextBool(1800) || !conversation.AppearanceCondition()).
            LinkFromStartToFinish().
            MakeFallback();
    }

    private static void RegisterWantingToGoHomeDialogue()
    {
        DialogueManager.RegisterNew("SolynWantingToGoHomeDialogue", "Start").
            WithAppearanceCondition(conversation => !Main.raining && RiftEclipseManagementSystem.RiftEclipseOngoing).
            WithRerollCondition(conversation => Main.rand.NextBool(1800) || !conversation.AppearanceCondition()).
            LinkFromStartToFinish().
            MakeFallback();
    }

    private static void RegisterRiftSunsetDialogue()
    {
        DialogueManager.RegisterNew("SolynRiftSunsetDialogue", "Start").
            WithAppearanceCondition(conversation => IsDusk && RiftEclipseManagementSystem.RiftEclipseOngoing).
            WithRerollCondition(conversation => !conversation.AppearanceCondition()).
            LinkFromStartToFinish().
            MakeFallback(priority: 1);
    }

    private static void RegisterRiftTownNPCDialogue()
    {
        DialogueManager.RegisterNew("SolynRiftTownNPCDialogue", "Start").
            WithAppearanceCondition(conversation =>
            {
                if (conversation.SeenBefore("Solyn2"))
                    return false;

                if (!RiftEclipseManagementSystem.RiftEclipseOngoing)
                    return false;

                bool townExistsInWorld = Main.npc.Take(Main.maxNPCs).Count(n => n.townNPC) >= 4;
                if (!townExistsInWorld)
                    return false;

                return true;
            }).
            WithRerollCondition(conversation => Main.rand.NextBool(1800) || !conversation.AppearanceCondition()).
            LinkFromStartToFinish().
            MakeFallback();
    }

    private static void RegisterHopefulPastDialogue()
    {
        DialogueManager.RegisterNew("SolynHopefulPastDialogue", "Start").
            WithAppearanceCondition(conversation => !Main.raining && RiftEclipseManagementSystem.RiftEclipseOngoing).
            WithRerollCondition(conversation => Main.rand.NextBool(1800) || !conversation.AppearanceCondition()).
            LinkFromStartToFinish().
            MakeFallback();
    }

    private static void RegisterRiftFogDialogue()
    {
        DialogueManager.RegisterNew("SolynRiftFogDialogue", "Start").
            WithAppearanceCondition(conversation => RiftEclipseFogEventManager.EventOngoing && RiftEclipseManagementSystem.RiftEclipseOngoing).
            WithRerollCondition(conversation => !conversation.AppearanceCondition()).
            LinkFromStartToFinish().
            MakeFallback(priority: 2);

        DialogueManager.RegisterNew("SolynMirrorwalkerDialogue", "Start").
            WithAppearanceCondition(conversation => NPC.AnyNPCs(ModContent.NPCType<Mirrorwalker>()) && RiftEclipseManagementSystem.RiftEclipseOngoing).
            WithRerollCondition(conversation => !conversation.AppearanceCondition()).
            LinkFromStartToFinish().
            MakeFallback(priority: 3);
    }

    private static void RegisterLategameDialogue()
    {
        DialogueManager.RegisterNew("SolynLategameDialogue", "Start").
            WithAppearanceCondition(conversation => CommonCalamityVariables.DevourerOfGodsDefeated).
            WithRerollCondition(conversation => Main.rand.NextBool(1800)).
            LinkFromStartToFinish().
            MakeFallback();
    }
}
