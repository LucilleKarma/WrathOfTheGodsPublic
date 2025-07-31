using NoxusBoss.Content.Items;
using NoxusBoss.Content.Items.Dyes;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Core.CrossCompatibility.Inbound;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using NoxusBoss.Core.Netcode;
using NoxusBoss.Core.Netcode.Packets;
using NoxusBoss.Core.World.GameScenes.RiftEclipse;
using NoxusBoss.Core.World.WorldGeneration;
using NoxusBoss.Core.World.WorldSaving; // Weird.
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using PermafrostNPC = CalamityMod.NPCs.TownNPCs.DILF;

namespace NoxusBoss.Core.GlobalInstances;

public partial class GlobalNPCEventHandlers : GlobalNPC
{
    // This kind of sucks, having a god method, but I dunno what would be better for this.
    public override void GetChat(NPC npc, ref string chat)
    {
        switch (npc.type)
        {
            case NPCID.Guide:
                break;
            case NPCID.Merchant:
                ProcessDialogue_Merchant(ref chat);
                break;
            case NPCID.Nurse:
                ProcessDialogue_Nurse(ref chat);
                break;
            case NPCID.Demolitionist:
                ProcessDialogue_Demolitionist(ref chat);
                break;
            case NPCID.DyeTrader:
                ProcessDialogue_DyeTrader(ref chat);
                break;
            case NPCID.Angler:
                ProcessDialogue_Angler(ref chat);
                break;
            case NPCID.BestiaryGirl: // Zoologist.
                ProcessDialogue_Zoologist(ref chat);
                break;
            case NPCID.Dryad:
                ProcessDialogue_Dryad(ref chat);
                break;
            case NPCID.Painter:
                ProcessDialogue_Painter(ref chat);
                break;
            case NPCID.Golfer:
                break;
            case NPCID.ArmsDealer:
                ProcessDialogue_ArmsDealer(ref chat);
                break;
            case NPCID.DD2Bartender: // Tavernkeep.
                ProcessDialogue_Tavernkeep(ref chat);
                break;
            case NPCID.Stylist:
                ProcessDialogue_Stylist(ref chat);
                break;
            case NPCID.GoblinTinkerer:
                ProcessDialogue_GoblinTinkerer(ref chat);
                break;
            case NPCID.WitchDoctor:
                break;
            case NPCID.Clothier:
                break;
            case NPCID.Mechanic:
                break;
            case NPCID.PartyGirl:
                ProcessDialogue_PartyGirl(ref chat);
                break;
            case NPCID.Wizard:
                ProcessDialogue_Wizard(ref chat);
                break;
            case NPCID.TaxCollector:
                break;
            case NPCID.Truffle:
                break;
            case NPCID.Pirate:
                ProcessDialogue_Pirate(ref chat);
                break;
            case NPCID.Steampunker:
                break;
            case NPCID.Cyborg:
                break;
            case NPCID.Princess:
                break;
            case NPCID.TravellingMerchant:
                ProcessDialogue_TravelingMerchant(ref chat);
                break;
            case NPCID.SkeletonMerchant:
                ProcessDialogue_SkeletonMerchant(npc, ref chat);
                break;
            case NPCID.TownSlimeOld:
                ProcessDialogue_ElderSlime(ref chat);
                break;
        }

        if (CalamityCompatibility.Enabled)
            GetChat_Calamity(npc, ref chat);
    }

    [JITWhenModsEnabled(CalamityCompatibility.ModName)]
    private void GetChat_Calamity(NPC npc, ref string chat)
    {
        if (npc.type == ModContent.NPCType<PermafrostNPC>() && !PermafrostKeepWorldGen.PlayerGivenKey)
        {
            PermafrostKeepWorldGen.PlayerGivenKey = true;
            if (Main.netMode != NetmodeID.SinglePlayer)
                PacketManager.SendPacket<PermafrostKeepKeyReceivePacket>();

            Main.LocalPlayer.QuickSpawnItem(npc.GetSource_FromThis(), ModContent.ItemType<DormantKey>());

            chat = GetLine("PermafrostChat.Saved");
        }
    }

    [JITWhenModsEnabled(CalamityCompatibility.ModName)]
    public override void OnChatButtonClicked(NPC npc, bool firstButton)
    {
    }

    private string GetLine(string key, params object[] formatting)
    {
        if (formatting.Length != 0)
            return Language.GetText($"Mods.{Mod.Name}.TownNPCs.{key}").Format(formatting);

        return Language.GetTextValue($"Mods.{Mod.Name}.TownNPCs.{key}");
    }

    public void ProcessDialogue_Merchant(ref string chat)
    {
        bool morning = Main.time < 16200D && Main.dayTime;
        bool afternoon = !morning && Main.time < 37800D && Main.dayTime;
        bool evening = !morning && !afternoon && Main.time < Main.dayLength && Main.dayTime;
        bool nightfall = Main.time < 9720D && !Main.dayTime;
        bool midnight = !nightfall && Main.time < 22680D && !Main.dayTime;
        bool nightsEnd = !nightfall && !midnight && Main.time < Main.nightLength && !Main.dayTime;
        if (RiftEclipseManagementSystem.RiftEclipseOngoing)
        {
            if (morning)
                chat = GetLine($"MerchantChat.RiftEclipse_Morning{Main.rand.Next(3) + 1}");
            if (afternoon)
                chat = GetLine($"MerchantChat.RiftEclipse_Afternoon{Main.rand.Next(3) + 1}");
            if (evening)
                chat = GetLine($"MerchantChat.RiftEclipse_Evening{Main.rand.Next(3) + 1}");
            if (nightfall)
                chat = GetLine($"MerchantChat.RiftEclipse_Nightfall{Main.rand.Next(3) + 1}");
            if (midnight)
                chat = GetLine($"MerchantChat.RiftEclipse_Midnight{Main.rand.Next(3) + 1}");
            if (nightsEnd)
                chat = GetLine($"MerchantChat.RiftEclipse_NightsEnd{Main.rand.Next(2) + 1}");
        }
    }

    public void ProcessDialogue_Nurse(ref string chat)
    {
        if (RiftEclipseManagementSystem.RiftEclipseOngoing)
        {
            Player p = Main.LocalPlayer;
            float playerLifeRatio = Saturate(p.statLife / (float)p.statLifeMax2);

            // Stay standard dialogue based on player HP.
            if (playerLifeRatio < 0.333f)
                chat = GetLine($"NurseChat.RiftEclipse_Below33PercentHP{Main.rand.Next(4) + 1}");
            else if (playerLifeRatio < 0.666f)
                chat = GetLine($"NurseChat.RiftEclipse_Below66PercentHP{Main.rand.Next(3) + 1}");
            else
                chat = GetLine($"NurseChat.RiftEclipse_Above66PercentHP{Main.rand.Next(4) + 1}");

            // Randomly mention the Demolitionist.
            int demolitionist = NPC.FindFirstNPC(NPCID.Demolitionist);
            if (demolitionist >= 0 && Main.rand.NextBool(8))
                chat = GetLine("NurseChat.RiftEclipse_Demolitionist", Main.npc[demolitionist].GivenName);

            // Say something silly if the player has a fire debuff.
            bool vanillaFireDebuff = p.HasBuff(BuffID.OnFire) || p.HasBuff(BuffID.CursedInferno) || p.HasBuff(BuffID.Frostburn) || p.HasBuff(BuffID.OnFire3);
            bool calamityFireDebuff = false;
            if (ModReferences.Calamity is not null)
            {
                if (ModReferences.Calamity.TryFind("Shadowflame", out ModBuff shadowflame) && p.HasBuff(shadowflame.Type))
                    calamityFireDebuff = true;
                if (ModReferences.Calamity.TryFind("SearingLava", out ModBuff cragsLava) && p.HasBuff(cragsLava.Type))
                    calamityFireDebuff = true;
                if (ModReferences.Calamity.TryFind("BrimstoneFlames", out ModBuff brimstoneFlames) && p.HasBuff(brimstoneFlames.Type))
                    calamityFireDebuff = true;
                if (ModReferences.Calamity.TryFind("HolyFlames", out ModBuff holyFlames) && p.HasBuff(holyFlames.Type))
                    calamityFireDebuff = true;
                if (ModReferences.Calamity.TryFind("GodSlayerInferno", out ModBuff godslayerInferno) && p.HasBuff(godslayerInferno.Type))
                    calamityFireDebuff = true;
                if (ModReferences.Calamity.TryFind("Dragonfire", out ModBuff dragonfire) && p.HasBuff(dragonfire.Type))
                    calamityFireDebuff = true;
            }
            bool fireDebuff = vanillaFireDebuff || calamityFireDebuff;
            if (fireDebuff)
                chat = GetLine("NurseChat.RiftEclipse_BurnDebuff");
        }
    }

    public void ProcessDialogue_Demolitionist(ref string chat)
    {
        if (RiftEclipseManagementSystem.RiftEclipseOngoing)
            chat = GetLine($"DemolitionistChat.RiftEclipse{Main.rand.Next(12) + 1}");
    }

    public void ProcessDialogue_DyeTrader(ref string chat)
    {
        if (RiftEclipseManagementSystem.RiftEclipseOngoing)
            chat = GetLine($"DyeTraderChat.RiftEclipse{Main.rand.Next(6) + 1}");

        if (Main.LocalPlayer.HasItem(ModContent.ItemType<GoodDye>()) && Main.rand.NextBool(3))
            chat = GetLine("DyeTraderChat.GoodDyeEasterEgg");
    }

    public void ProcessDialogue_Angler(ref string chat)
    {
        if (RiftEclipseManagementSystem.RiftEclipseOngoing)
        {
            chat = GetLine($"AnglerChat.RiftEclipse{Main.rand.Next(4) + 1}");

            // Randomly mention the Merchant.
            int merchant = NPC.FindFirstNPC(NPCID.Merchant);
            if (merchant >= 0 && Main.rand.NextBool(6))
                chat = GetLine("AnglerChat.RiftEclipse_Merchant", Main.npc[merchant].GivenName);
        }
    }

    public void ProcessDialogue_Zoologist(ref string chat)
    {
        if (RiftEclipseManagementSystem.RiftEclipseOngoing)
        {
            chat = GetLine($"ZoologistChat.RiftEclipse{Main.rand.Next(5) + 1}");
            if (!Main.dayTime && Main.rand.NextBool())
                chat = GetLine($"ZoologistChat.RiftEclipseNight{Main.rand.Next(4) + 1}");
        }
    }

    public void ProcessDialogue_Dryad(ref string chat)
    {
        if (RiftEclipseManagementSystem.RiftEclipseOngoing)
        {
            chat = GetLine($"DryadChat.RiftEclipse{Main.rand.Next(11) + 1}");
            if (Main.rand.NextBool(12))
                chat = GetLine("DryadChat.RiftEclipse12", Main.LocalPlayer.name);
            if (Main.rand.NextBool(10))
            {
                if (WorldGen.crimson)
                    chat = GetLine("DryadChat.RiftEclipse_Crimson1");
                else
                    chat = GetLine("DryadChat.RiftEclipse_Corruption1");
            }
        }
    }

    public void ProcessDialogue_Painter(ref string chat)
    {
        if (RiftEclipseManagementSystem.RiftEclipseOngoing)
        {
            chat = GetLine($"PainterChat.RiftEclipse{Main.rand.Next(6) + 1}");
            if (Main.rand.NextBool(6) && Main.raining)
                chat = GetLine("PainterChat.RiftEclipseSnow");
        }

        if (Main.rand.NextBool(50))
        {
            chat = GetLine("PainterChat.ArtisticCatharsis");
            LowTierGodLightningSystem.LightningDelayCountdown = 90;
        }
    }

    public void ProcessDialogue_ArmsDealer(ref string chat)
    {
        if (RiftEclipseManagementSystem.RiftEclipseOngoing)
        {
            chat = GetLine($"ArmsDealerChat.RiftEclipse{Main.rand.Next(6) + 1}");
            if (Main.rand.NextBool(6) && Main.raining)
                chat = GetLine("ArmsDealerChat.RiftEclipseSnow");
            if (Main.rand.NextBool(7) && !Main.dayTime)
                chat = GetLine("ArmsDealerChat.RiftEclipseNight");

            // Randomly mention the Demolitionist.
            int demolitionist = NPC.FindFirstNPC(NPCID.Demolitionist);
            if (demolitionist >= 0 && Main.rand.NextBool(8))
                chat = GetLine("ArmsDealerChat.RiftEclipse_Demolitionist", Main.npc[demolitionist].GivenName);
        }

        if (Main.rand.NextBool(5) && Main.zenithWorld)
            chat = GetLine("ArmsDealerChat.GFB");
    }

    public void ProcessDialogue_Tavernkeep(ref string chat)
    {
        if (RiftEclipseManagementSystem.RiftEclipseOngoing)
        {
            chat = GetLine($"TavernkeepChat.RiftEclipse{Main.rand.Next(7) + 1}");

            // Randomly mention the Dryad.
            int dryad = NPC.FindFirstNPC(NPCID.Dryad);
            if (dryad >= 0 && Main.rand.NextBool(7))
                chat = GetLine("TavernkeepChat.RiftEclipse_Dryad", Main.npc[dryad].GivenName);
        }
    }

    public void ProcessDialogue_Stylist(ref string chat)
    {
        if (RiftEclipseManagementSystem.RiftEclipseOngoing)
            chat = GetLine($"StylistChat.RiftEclipse{Main.rand.Next(7) + 1}");
        else if (Main.rand.NextBool(7) && NPC.AnyNPCs(ModContent.NPCType<Solyn>()) && NPC.downedPlantBoss)
            chat = GetLine("StylistChat.Solyn");
    }

    public void ProcessDialogue_GoblinTinkerer(ref string chat)
    {
        if (RiftEclipseManagementSystem.RiftEclipseOngoing)
            chat = GetLine($"GoblinTinkererChat.RiftEclipse{Main.rand.Next(4) + 1}");
    }

    public void ProcessDialogue_PartyGirl(ref string chat)
    {
        if (RiftEclipseManagementSystem.RiftEclipseOngoing)
        {
            chat = GetLine($"PartyGirlChat.RiftEclipse{Main.rand.Next(6) + 1}");
            if (Main.rand.NextBool(6) && Main.raining)
                chat = GetLine("PartyGirlChat.RiftEclipseSnow1");
        }
    }

    public void ProcessDialogue_Wizard(ref string chat)
    {
        // Randomly predict the Rift Eclipse.
        if (Main.rand.NextBool(8) && !RiftEclipseManagementSystem.RiftEclipseOngoing && !BossDownedSaveSystem.HasDefeated<AvatarOfEmptiness>() && Main.hardMode)
            chat = GetLine("WizardChat.RiftEclipsePremonition");

        // Mention Solyn.
        if (Main.rand.NextBool(8) && !RiftEclipseManagementSystem.RiftEclipseOngoing)
            chat = GetLine("WizardChat.SolynAcknowledgement");

        // Say things about the Rift Eclipse.
        if (RiftEclipseManagementSystem.RiftEclipseOngoing)
            chat = GetLine($"WizardChat.RiftEclipse{Main.rand.Next(7) + 1}");
    }

    public void ProcessDialogue_Pirate(ref string chat)
    {
        if (RiftEclipseManagementSystem.RiftEclipseOngoing)
            chat = GetLine($"PirateChat.RiftEclipse{Main.rand.Next(6) + 1}");

        if (Main.rand.NextBool(6))
        {
            if (RiftEclipseSnowSystem.IceCanCoverWater)
                chat = GetLine($"PirateChat.RiftEclipse_AfterFrozenSea{Main.rand.Next(2) + 1}");
            else
                chat = GetLine("PirateChat.RiftEclipse_BeforeFrozenSea1");
        }
    }

    public void ProcessDialogue_TravelingMerchant(ref string chat)
    {
        if (RiftEclipseManagementSystem.RiftEclipseOngoing)
            chat = GetLine("TravelingMerchantChat.RiftEclipse1");
    }

    public void ProcessDialogue_SkeletonMerchant(NPC npc, ref string chat)
    {
        if (RiftEclipseManagementSystem.RiftEclipseOngoing)
            chat = GetLine($"SkeletonMerchantChat.RiftEclipse{Main.rand.Next(6) + 1}");
        if (Main.rand.NextBool(6))
        {
            chat = GetLine("SkeletonMerchantChat.RiftEclipseUnderground");
            if (npc.Center.Y <= Main.worldSurface * 16f)
                chat = GetLine("SkeletonMerchantChat.RiftEclipseOnSurface");
        }
    }

    public void ProcessDialogue_ElderSlime(ref string chat)
    {
        if (RiftEclipseManagementSystem.RiftEclipseOngoing)
            chat = GetLine("ElderSlimeChat.RiftEclipse");
    }

    public override void ModifyShop(NPCShop shop)
    {
        ModifyShopEvent?.Invoke(shop);
    }
}
