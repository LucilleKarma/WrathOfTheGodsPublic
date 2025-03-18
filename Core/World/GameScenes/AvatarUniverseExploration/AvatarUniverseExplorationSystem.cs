using CalamityMod.NPCs.CeaselessVoid;
using CalamityMod.NPCs.Crags;
using CalamityMod.NPCs.NormalNPCs;
using CalamityMod.NPCs.Polterghast;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.GlobalInstances;
using NoxusBoss.Core.World.TileDisabling;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Core.World.GameScenes.AvatarUniverseExploration;

[JITWhenModsEnabled(CalamityCompatibility.ModName)]
[ExtendsFromMod(CalamityCompatibility.ModName)]
public class AvatarUniverseExplorationSystem : ModSystem
{
    private static bool inAvatarUniverse;

    private static bool wantsToEnterAvatarUniverse;

    /// <summary>
    /// The set of dungeon NPC IDs that should be checked for and deleted when players enter the Avatar's universe.
    /// </summary>
    public static readonly List<int> DungeonNPCIDs = new List<int>(64);

    /// <summary>
    /// Whether players are in the process of entering the rift.
    /// </summary>
    public static bool EnteringRift
    {
        get;
        set;
    }

    /// <summary>
    /// The event to fire upon entering the Avatar's universe.
    /// </summary>
    public static event Action? OnEnter;

    /// <summary>
    /// Whether the Avatar's universe is currently being explored.
    /// </summary>
    public static bool InAvatarUniverse
    {
        get => inAvatarUniverse;
        private set
        {
            if (value)
                TileDisablingSystem.TilesAreUninteractable = true;

            inAvatarUniverse = value;
        }
    }

    public override void OnModLoad()
    {
        RegisterDungeonEnemies();

        OnEnter += ClearDungeonEnemies;
        OnEnter += ClearGoreAndDust;
        GlobalItemEventHandlers.CanUseItemEvent += DisableTeleportItems;
    }

    public override void OnWorldLoad()
    {
        wantsToEnterAvatarUniverse = false;
        InAvatarUniverse = false;
        EnteringRift = false;
    }

    public override void OnWorldUnload()
    {
        wantsToEnterAvatarUniverse = false;
        InAvatarUniverse = false;
        EnteringRift = false;
    }

    private static bool DisableTeleportItems(Item item, Player player)
    {
        if (InAvatarUniverse)
        {
            int itemID = item.type;
            bool mirror = itemID == ItemID.MagicMirror || itemID == ItemID.IceMirror || itemID == ItemID.CellPhone || itemID == ItemID.Shellphone || itemID == ItemID.ShellphoneSpawn;
            bool conch = itemID == ItemID.MagicConch || itemID == ItemID.DemonConch || itemID == ItemID.ShellphoneOcean || itemID == ItemID.ShellphoneHell;
            bool teleportPotion = itemID == ItemID.RecallPotion || itemID == ItemID.TeleportationPotion || itemID == ItemID.PotionOfReturn;
            if (mirror || conch || teleportPotion)
                return false;
        }

        return true;
    }

    private static void RegisterDungeonEnemies()
    {
        DungeonNPCIDs.Add(NPCID.AngryBones);
        DungeonNPCIDs.Add(NPCID.AngryBonesBig);
        DungeonNPCIDs.Add(NPCID.AngryBonesBigHelmet);
        DungeonNPCIDs.Add(NPCID.AngryBonesBigMuscle);

        DungeonNPCIDs.Add(NPCID.DarkCaster);
        DungeonNPCIDs.Add(NPCID.WaterSphere);
        DungeonNPCIDs.Add(NPCID.CursedSkull);
        DungeonNPCIDs.Add(NPCID.DungeonSlime);
        DungeonNPCIDs.Add(NPCID.SpikeBall);
        DungeonNPCIDs.Add(NPCID.BlazingWheel);

        DungeonNPCIDs.Add(NPCID.BlueArmoredBones);
        DungeonNPCIDs.Add(NPCID.BlueArmoredBonesMace);
        DungeonNPCIDs.Add(NPCID.BlueArmoredBonesNoPants);
        DungeonNPCIDs.Add(NPCID.BlueArmoredBonesSword);

        DungeonNPCIDs.Add(NPCID.HellArmoredBones);
        DungeonNPCIDs.Add(NPCID.HellArmoredBonesMace);
        DungeonNPCIDs.Add(NPCID.HellArmoredBonesSpikeShield);
        DungeonNPCIDs.Add(NPCID.HellArmoredBonesSword);

        DungeonNPCIDs.Add(NPCID.Paladin);
        DungeonNPCIDs.Add(NPCID.Necromancer);
        DungeonNPCIDs.Add(NPCID.NecromancerArmored);
        DungeonNPCIDs.Add(NPCID.RaggedCaster);
        DungeonNPCIDs.Add(NPCID.RaggedCasterOpenCoat);
        DungeonNPCIDs.Add(NPCID.DiabolistRed);
        DungeonNPCIDs.Add(NPCID.DiabolistWhite);

        DungeonNPCIDs.Add(NPCID.SkeletonCommando);
        DungeonNPCIDs.Add(NPCID.SkeletonSniper);
        DungeonNPCIDs.Add(NPCID.TacticalSkeleton);
        DungeonNPCIDs.Add(NPCID.GiantCursedSkull);
        DungeonNPCIDs.Add(NPCID.BoneLee);
        DungeonNPCIDs.Add(NPCID.DungeonSpirit);

        DungeonNPCIDs.Add(NPCID.BoundMechanic);

        DungeonNPCIDs.Add(NPCID.DungeonGuardian);

        if (CalamityCompatibility.Enabled)
            RegisterDungeonEnemies_Calamity();
    }

    [JITWhenModsEnabled(CalamityCompatibility.ModName)]
    private static void RegisterDungeonEnemies_Calamity()
    {
        DungeonNPCIDs.Add(ModContent.NPCType<RenegadeWarlock>());

        DungeonNPCIDs.Add(ModContent.NPCType<PhantomSpirit>());
        DungeonNPCIDs.Add(ModContent.NPCType<PhantomSpiritS>());
        DungeonNPCIDs.Add(ModContent.NPCType<PhantomSpiritM>());
        DungeonNPCIDs.Add(ModContent.NPCType<PhantomSpiritL>());

        DungeonNPCIDs.Add(ModContent.NPCType<CeaselessVoid>());
        DungeonNPCIDs.Add(ModContent.NPCType<DarkEnergy>());

        DungeonNPCIDs.Add(ModContent.NPCType<Polterghast>());
    }

    internal static void ClearDungeonEnemies()
    {
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (DungeonNPCIDs.Contains(npc.type))
                npc.active = false;
        }
    }

    private static void ClearGoreAndDust()
    {
        foreach (Gore gore in Main.gore)
            gore.active = false;
        foreach (Dust dust in Main.dust)
            dust.active = false;
    }

    public override void PostUpdateEverything()
    {
        if (EnteringRift && Main.player.Where(p => p.active).All(p => p.GetValueRef<float>(AvatarRiftSuckVisualsManager.ZoomInInterpolantName).Value >= 0.8f))
        {
            Main.musicFade[Main.curMusic] = 0f;
            OnEnter?.Invoke();
            InAvatarUniverse = wantsToEnterAvatarUniverse;
            EnteringRift = false;

            if (!InAvatarUniverse)
            {
                SoundEngine.StopAmbientSounds();
                SoundEngine.StopTrackedSounds();
            }
        }
        if (EnteringRift)
            Main.musicFade[Main.curMusic] *= 0.9f;
    }

    /// <summary>
    /// Causes everyone to enter the Avatar's universe.
    /// </summary>
    public static void Enter()
    {
        foreach (Player player in Main.ActivePlayers)
        {
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.RiftPlayerAbsorb, player.Center);
            player.GetValueRef<float>(AvatarRiftSuckVisualsManager.ZoomInInterpolantName).Value = 0.001f;
        }
        EnteringRift = true;
        wantsToEnterAvatarUniverse = true;
    }

    /// <summary>
    /// Causes everyone to leave the Avatar's universe.
    /// </summary>
    public static void Exit()
    {
        Enter();
        wantsToEnterAvatarUniverse = false;
    }
}
