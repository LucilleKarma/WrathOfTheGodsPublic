using Luminance.Core.Graphics;
using Luminance.Core.Hooking;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using NoxusBoss.Assets;
using NoxusBoss.Assets.Fonts;
using NoxusBoss.Content.Biomes;
using NoxusBoss.Content.Items;
using NoxusBoss.Content.Items.Accessories.VanityEffects;
using NoxusBoss.Content.Items.Accessories.Wings;
using NoxusBoss.Content.Items.Dyes;
using NoxusBoss.Content.Items.HuntAuricSouls;
using NoxusBoss.Content.Items.LoreItems;
using NoxusBoss.Content.Items.MiscOPTools;
using NoxusBoss.Content.Items.Pets;
using NoxusBoss.Content.Items.Placeable;
using NoxusBoss.Content.Items.Placeable.Paintings;
using NoxusBoss.Content.Items.Placeable.Trophies;
using NoxusBoss.Content.Items.SummonItems;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;
using NoxusBoss.Content.Rarities;
using NoxusBoss.Core.Autoloaders;
using NoxusBoss.Core.Bestiary;
using NoxusBoss.Core.CrossCompatibility.Inbound;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.CrossCompatibility.Inbound.BossChecklist;
using NoxusBoss.Core.CrossCompatibility.Inbound.Infernum;
using NoxusBoss.Core.CrossCompatibility.Inbound.WikiThis;
using NoxusBoss.Core.DataStructures.DropRules;
using NoxusBoss.Core.GlobalInstances;
using NoxusBoss.Core.Graphics.UI.Bestiary;
using NoxusBoss.Core.Netcode;
using NoxusBoss.Core.Netcode.Packets;
using NoxusBoss.Core.SoundSystems;
using NoxusBoss.Core.World.WorldSaving;

using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI.Chat;

using static NoxusBoss.Core.CrossCompatibility.Inbound.CalamityRemix.CalRemixCompatibilitySystem;

namespace NoxusBoss.Content.NPCs.Bosses.NamelessDeity;

public partial class NamelessDeityBoss : ModNPC, IBossChecklistSupport, IInfernumBossIntroCardSupport, IWikithisNameRedirect
{
    #region Crossmod Compatibility

    public string RedirectPageName => "Nameless_Deity_of_Light";

    public LocalizedText IntroCardTitleName => this.GetLocalization("InfernumCompatibility.Title").WithFormatArgs(NPC.GivenOrTypeName);

    public int IntroCardAnimationDuration => SecondsToFrames(2.3f);

    public bool IsMiniboss => false;

    public string ChecklistEntryName => "NamelessDeity";

    public bool IsDefeated => BossDownedSaveSystem.HasDefeated<NamelessDeityBoss>();

    public float ProgressionValue => 28f;

    public List<int> Collectibles => new List<int>()
    {
        ModContent.ItemType<LoreNamelessDeity>(),
        ModContent.ItemType<GraphicalUniverseImager>(),
        ModContent.ItemType<CheatPermissionSlip>(),
        MaskID,
        RelicID,
        ModContent.ItemType<NamelessDeityTrophy>(),
        ModContent.ItemType<BlackHole>(),
        ModContent.ItemType<Starseed>(),
        ModContent.ItemType<Erilucyxwyn>(),
    };

    public int? SpawnItem => FakeTerminus.TerminusID;

    public bool UsesCustomPortraitDrawing => true;

    public float IntroCardScale => 1.95f;

    public void DrawCustomPortrait(SpriteBatch spriteBatch, Rectangle area, Color color)
    {
        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);

        Texture2D bossChecklistTexture = GennedAssets.Textures.NamelessDeity.NamelessDeityBoss_BossChecklist;
        Vector2 centeredDrawPosition = area.Center.ToVector2() - bossChecklistTexture.Size() * 0.5f;
        spriteBatch.Draw(bossChecklistTexture, centeredDrawPosition, color);

        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
    }

    public bool ShouldDisplayIntroCard() => Myself is not null && Myself.As<NamelessDeityBoss>().CurrentState == NamelessAIType.IntroScreamAnimation;

    public SoundStyle ChooseIntroCardLetterSound() => GennedAssets.Sounds.NamelessDeity.ScaryFlash with { MaxInstances = 10 };

    public SoundStyle ChooseIntroCardMainSound() => GennedAssets.Sounds.NamelessDeity.Chuckle with { Volume = 1.6f };

    public Color GetIntroCardTextColor(float horizontalCompletion, float animationCompletion)
    {
        return Color.Lerp(Color.White, Color.Fuchsia, 0.1f);
    }

    #endregion Crossmod Compatibility

    #region Attack Cycles

    // These attack cycles for Nameless are specifically designed to go in a repeated quick paced -> precise dance that gradually increases in speed across the different cycles.
    // Please do not change them without careful consideration.
    public static NamelessAIType[] Phase1Cycle => new[]
    {
        // Start off with the arcing attack. It will force the player to move around to evade the starbursts.
        NamelessAIType.ArcingEyeStarbursts,

        // After the daggers have passed, it's a safe bet the player won't have much movement at the start to mess with the attack. As such, the exploding star attack happens next to work with that.
        NamelessAIType.ConjureExplodingStars,

        // Transition to the reality tear daggers attack.
        NamelessAIType.RealityTearDaggers,

        // Resume the slower pace with a "slower" attack in the form of a laserbeam attack.
        NamelessAIType.PerpendicularPortalLaserbeams,

        // Now that the player has spent a bunch of time doing weaving and tight, precise movements, get them back into the fast moving action again with the arcing starbursts.
        NamelessAIType.ArcingEyeStarbursts,

        // And again, follow up with a precise attack in the form of the star lasers. This naturally follows with the chasing quasar, which amps up the pacing again.
        NamelessAIType.SunBlenderBeams,
        NamelessAIType.CrushStarIntoQuasar,

        // Return to the fast starbursts attack again.
        NamelessAIType.ArcingEyeStarbursts,

        // Do the precise laserbeam charge attack to slow them down. From here the cycle will repeat at another high point.
        NamelessAIType.PerpendicularPortalLaserbeams
    };

    public static NamelessAIType[] Phase2Cycle => new[]
    {
        // Start out with a fast attack in the form of the screen slices.
        NamelessAIType.VergilScreenSlices,

        // Continue the fast pace with the punches + screen slices attack.
        NamelessAIType.RealityTearPunches,

        // Amp the pace up again with stars from the background. This will demand fast movement and zoning of the player.
        NamelessAIType.BackgroundStarJumpscares,

        // Get the player up close and personal with Nameless with the true-melee sword attack.
        NamelessAIType.SwordConstellation,

        // Return to something a bit slower again with the converging stars. This has a fast end point, however, which should naturally transition to the other attacks.
        NamelessAIType.InwardStarPatternedExplosions,

        // Make the player use their speed from the end of the previous attack with the punches.
        NamelessAIType.RealityTearPunches,
        
        // Use the zoning background stars attack again the continue applying fast pressure onto the player.
        NamelessAIType.BackgroundStarJumpscares,

        // Follow with a precise attack in the form of the star lasers. This naturally follows with the chasing quasar, which amps up the pacing again.
        // This is a phase 1 attack, but is faster in the second phase.
        NamelessAIType.SunBlenderBeams,
        NamelessAIType.CrushStarIntoQuasar,

        // Return to the fast paced cycle with the true melee sword constellation attack again.
        NamelessAIType.SwordConstellation,

        // Use the star convergence again, as the cycle repeats.
        NamelessAIType.InwardStarPatternedExplosions,
    };

    // With the exception of the clock attack this cycle should keep the player constantly on the move.
    public static NamelessAIType[] Phase3Cycle => new[]
    {
        // A chaotic slash chase sequence to keep the player constantly on their feet, acting as a quick introduction to the phase.
        NamelessAIType.DarknessWithLightSlashes,

        // A "slower" attack in the form of the clock constellation.
        NamelessAIType.ClockConstellation,

        // Use the true melee sword attack to help keep the flow between the music-muting attacks. This one is a bit faster than the original from the second phase.
        NamelessAIType.SwordConstellation,
        
        // Perform the cosmic laserbeam attack, ramping up the pace again.
        NamelessAIType.SuperCosmicLaserbeam,
        
        // Show the moment of creation as a final step.
        NamelessAIType.MomentOfCreation,
    };

    #endregion Attack Cycles

    #region Initialization

    /// <summary>
    /// The item ID of Nameless' autoloaded mask.
    /// </summary>
    public static int MaskID
    {
        get;
        private set;
    }

    /// <summary>
    /// The item ID of Nameless' autoloaded relic.
    /// </summary>
    public static int RelicID
    {
        get;
        private set;
    }

    /// <summary>
    /// The item ID of Nameless' autoloaded treasure bag.
    /// </summary>
    public static int TreasureBagID
    {
        get;
        private set;
    }

    public override void Load()
    {
        // Autoload the mask item.
        MaskID = MaskAutoloader.Create(Mod, GetAssetPath("Content/Items/Vanity", "NamelessDeityMask"), true);

        // Autoload the music boxes for Nameless.
        string musicPath = "Assets/Sounds/Music/NamelessDeity";
        string musicPathP3 = "Assets/Sounds/Music/ARIA BEYOND THE SHINING FIRMAMENT";
        MusicBoxAutoloader.Create(Mod, GetAssetPath("Content/Items/Placeable/MusicBoxes", "NamelessDeityMusicBox"), musicPath, out _, out _);
        MusicBoxAutoloader.Create(Mod, GetAssetPath("Content/Items/Placeable/MusicBoxes", "NamelessDeityMusicBoxP3"), musicPathP3, out _, out _, null, DrawPhase3MusicBoxItemTooltips);

        // Autoload the relic for Nameless.
        RelicAutoloader.Create(Mod, GetAssetPath("Content/Items/Placeable/Relics", "NamelessDeityRelic"), out int relicID, out _);
        RelicID = relicID;

        // Autoload the treasure bag for Nameless.
        TreasureBagID = TreasureBagAutoloader.Create(Mod, GetAssetPath("Content/Items/TreasureBags", "NamelessDeityTreasureBag"), bag =>
        {
            bag.rare = ModContent.RarityType<NamelessDeityRarity>();
        }, ModifyNPCBagLoot);

        AudioReversingSystem.FreezingConditionEvent += FreezeMusic;
    }

    private bool FreezeMusic() => Myself?.As<NamelessDeityBoss>()?.StopMusic ?? false;

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 1;
        NPCID.Sets.TrailingMode[NPC.type] = 3;
        NPCID.Sets.TrailCacheLength[NPC.type] = 90;
        NPCID.Sets.NPCBestiaryDrawModifiers value = new NPCID.Sets.NPCBestiaryDrawModifiers()
        {
            Scale = 0.16f,
            PortraitScale = 0.2f
        };
        NPCID.Sets.NPCBestiaryDrawOffset[Type] = value;
        NPCID.Sets.BossBestiaryPriority.Add(Type);
        NPCID.Sets.MPAllowedEnemies[Type] = true;
        NPCID.Sets.UsesNewTargetting[Type] = true;
        EmptinessSprayer.NPCsThatReflectSpray[Type] = true;
        BestiaryBossOrderingPrioritySystem.Priority[Type] = 2;

        // Apply miracleblight immunities.
        CalamityCompatibility.MakeImmuneToMiracleblight(NPC);

        // Allow Nameless' fists to optionally do contact damage.
        On_NPC.GetMeleeCollisionData += ExpandEffectiveHitboxForHands;

        // Define loot data for players when they defeat Nameless.
        PlayerDataManager.LoadDataEvent += LoadDefeatStateForPlayer;
        PlayerDataManager.SaveDataEvent += SaveDefeatStateForPlayer;
        PlayerDataManager.PostUpdateEvent += GivePlayerLootIfNecessary;

        // Allow Nameless to have more than five stars in the bestiary.
        On_UIBestiaryEntryInfoPage.GetBestiaryInfoCategory += AddDynamicFlavorText;
        new ManagedILEdit("Remove Bestiary Five-Star Limitation", Mod, edit =>
        {
            IL_NPCPortraitInfoElement.CreateStarsContainer += edit.SubscriptionWrapper;
        }, edit =>
        {
            IL_NPCPortraitInfoElement.CreateStarsContainer -= edit.SubscriptionWrapper;
        }, RemoveBestiaryStarLimits).Apply();

        // Load evil Fanny text.
        var hatred1 = new FannyDialog("NamelessDeityGFB1", "EvilFannyIdle").WithDuration(6f).WithEvilness().WithCondition(_ =>
        {
            return Myself is not null && Myself.As<NamelessDeityBoss>().CurrentState == NamelessAIType.RealityTearPunches;
        }).WithoutClickability();
        var hatred2 = new FannyDialog("NamelessDeityGFB2", "EvilFannyIdle").WithDuration(15f).WithEvilness().WithDrawSizes(900).WithParentDialog(hatred1, 3f);
        var hatred3 = new FannyDialog("NamelessDeityGFB3", "EvilFannyIdle").WithDuration(9f).WithEvilness().WithDrawSizes(600).WithParentDialog(hatred2, 3f);
        hatred1.Register();
        hatred2.Register();
        hatred3.Register();

        // Initialize AI states.
        LoadStates();
    }

    private static bool DrawPhase3MusicBoxItemTooltips(DrawableTooltipLine line, ref int yOffset)
    {
        string replacementString = "REPLACED VIA CODE DONT CHANGE THIS";
        if (line.Text.Contains(replacementString))
        {
            Color rarityColor = line.OverrideColor ?? line.Color;
            Vector2 drawPosition = new Vector2(line.X, line.Y);

            // Draw lines.
            List<string> lines = [.. line.Text.Split(replacementString)];
            float staticSpacing = 150f;
            Vector2 staticPosition = drawPosition;
            Vector2 staticSize = Vector2.One;
            for (int i = 0; i < lines.Count; i++)
            {
                ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch, line.Font, lines[i], drawPosition, rarityColor, line.Rotation, line.Origin, line.BaseScale, line.MaxWidth, line.Spread);
                drawPosition.X += line.Font.MeasureString(lines[i]).X * line.BaseScale.X;
                if (i == 0)
                {
                    drawPosition.X += staticSpacing;
                    staticPosition = drawPosition + new Vector2(-2f, -4f);
                    staticSize = new Vector2(staticSpacing - 6f, line.Font.MeasureString(lines[i]).Y * line.BaseScale.Y);
                }
            }

            // Draw static where the replaced name text would be.
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, null, null, null, null, Main.UIScaleMatrix);

            // Prepare the static shader.
            var staticShader = ShaderManager.GetShader("NoxusBoss.StaticOverlayShader");
            staticShader.TrySetParameter("staticInterpolant", 1f);
            staticShader.TrySetParameter("staticZoomFactor", 8f);
            staticShader.TrySetParameter("neutralizationInterpolant", 0f);
            staticShader.SetTexture(MulticoloredNoise, 1, SamplerState.PointWrap);
            staticShader.Apply();

            // Draw the pixel.
            Main.spriteBatch.Draw(WhitePixel, staticPosition, null, Color.Black, 0f, WhitePixel.Size() * Vector2.UnitX, staticSize / WhitePixel.Size(), 0, 0f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, null, null, Main.UIScaleMatrix);

            return false;
        }

        return true;
    }

    private int AddDynamicFlavorText(On_UIBestiaryEntryInfoPage.orig_GetBestiaryInfoCategory orig, UIBestiaryEntryInfoPage self, IBestiaryInfoElement element)
    {
        // UIBestiaryEntryInfoPage.BestiaryInfoCategory.Flavor is inaccessible due to access modifiers. Use its literal value of 2 instead.
        if (element is DynamicFlavorTextBestiaryInfoElement)
            return 2;

        return orig(self, element);
    }

    public override void SetDefaults()
    {
        NPC.npcSlots = 100f;
        NPC.damage = 300;
        NPC.width = 270;
        NPC.height = 500;
        NPC.defense = 150;
        NPC.lifeMax = GetAIInt("TotalHP");
        if (CalamityCompatibility.Enabled)
            CalamityCompatibility.SetLifeMaxByMode_ApplyCalBossHPBoost(NPC);

        if (Main.expertMode)
            NPC.damage = 275;

        NPC.aiStyle = -1;
        AIType = -1;
        NPC.knockBackResist = 0f;
        NPC.canGhostHeal = false;
        NPC.boss = true;
        NPC.noGravity = true;
        NPC.noTileCollide = true;
        NPC.HitSound = null;
        NPC.DeathSound = null;
        NPC.value = Item.buyPrice(100, 0, 0, 0) / 5;
        NPC.netAlways = true;
        NPC.hide = true;
        NPC.Opacity = 0f;
        NPC.BossBar = ModContent.GetInstance<NamelessBossBar>();
        CalamityCompatibility.MakeCalamityBossBarClose(NPC);

        SpawnModBiomes = [ModContent.GetInstance<EternalGardenBiome>().Type];
        RenderComposite = new(NPC);
    }

    public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
    {
        NPC.lifeMax = (int)Round(NPC.lifeMax * bossAdjustment / (Main.masterMode ? 3f : 2f));
    }

    public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
    {
        // Remove the original NPCPortraitInfoElement instance, so that a new one with far more stars can be added.
        bestiaryEntry.Info.RemoveAll(i => i is NPCPortraitInfoElement);

        // Remove the original name display text instance, so that a new one with nothing can be added.
        bestiaryEntry.Info.RemoveAll(i => i is NamePlateInfoElement);

        string[] bestiaryKeys = new string[12];
        for (int i = 0; i < bestiaryKeys.Length; i++)
            bestiaryKeys[i] = Language.GetTextValue($"Mods.{Mod.Name}.Bestiary.{Name}{i + 1}");

        bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[]
        {
            new MoonLordPortraitBackgroundProviderBestiaryInfoElement(),
            new NPCPortraitInfoElement(50),
            new NamePlateInfoElement(string.Empty, NPC.netID)
        });
        if (Main.netMode != NetmodeID.Server)
            bestiaryEntry.Info.Add(new DynamicFlavorTextBestiaryInfoElement(bestiaryKeys, FontRegistry.Instance.NamelessDeityText));
    }
    #endregion Initialization

    #region Loot

    public const string PlayerGiveLootFieldName = "GiveNamelessDeityLootUponReenteringWorld";

    private static void SaveDefeatStateForPlayer(PlayerDataManager p, TagCompound tag)
    {
        tag[PlayerGiveLootFieldName] = p.Player.GetValueRef<bool>(PlayerGiveLootFieldName).Value;
    }

    private static void LoadDefeatStateForPlayer(PlayerDataManager p, TagCompound tag)
    {
        p.GetValueRef<bool>(PlayerGiveLootFieldName).Value = tag.TryGet(PlayerGiveLootFieldName, out bool result) && result;
    }

    private static void GivePlayerLootIfNecessary(PlayerDataManager p)
    {
        // Give the player loot if they're entitled to it. If not, terminate immediately.
        if (!p.GetValueRef<bool>(PlayerGiveLootFieldName))
            return;

        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            PacketManager.SendPacket<GiveNamelessDeityLootPacket>(p.Player.whoAmI);
            p.GetValueRef<bool>(PlayerGiveLootFieldName).Value = false;
            return;
        }

        // Move Nameless up, so that the loot comes from the sky.
        NPC dummyNameless = new NPC();
        dummyNameless.SetDefaults(ModContent.NPCType<NamelessDeityBoss>());
        dummyNameless.Center = p.Player.Center - Vector2.UnitY * 275f;
        if (dummyNameless.position.Y < 400f)
            dummyNameless.position.Y = 400f;

        // Ensure that the loot does not appear in the middle of a bunch of blocks.
        for (int i = 0; i < 600; i++)
        {
            if (!Collision.SolidCollision(dummyNameless.Center, 1, 1))
                break;

            dummyNameless.position.Y++;
        }

        // Log a kill in the bestiary.
        Main.BestiaryTracker.Kills.RegisterKill(dummyNameless);

        // Drop Nameless' loot and mark him as defeated.
        dummyNameless.NPCLoot();
        dummyNameless.active = false;
        BossDownedSaveSystem.SetDefeatState<NamelessDeityBoss>(true);

        // Disable the loot flag.
        p.GetValueRef<bool>(PlayerGiveLootFieldName).Value = false;
    }

    public override void BossLoot(ref string name, ref int potionType)
    {
        potionType = ItemID.SuperHealingPotion;
        if (ModReferences.Calamity?.TryFind("OmegaHealingPotion", out ModItem potion) ?? false)
            potionType = potion.Type;
    }

    public override void ModifyNPCLoot(NPCLoot npcLoot)
    {
        // Add the boss bag.
        npcLoot.Add(ItemDropRule.BossBag(TreasureBagID));

        // Define non-expert specific loot.
        LeadingConditionRule normalOnly = new LeadingConditionRule(new Conditions.NotExpert());
        {
            // General drops.
            normalOnly.OnSuccess(ItemDropRule.Common(ModContent.ItemType<Moonscreen>()));
            normalOnly.OnSuccess(ItemDropRule.Common(ModContent.ItemType<CheatPermissionSlip>()));
            normalOnly.OnSuccess(ItemDropRule.Common(ModContent.ItemType<DeificTouch>()));
            normalOnly.OnSuccess(ItemDropRule.Common(ModContent.ItemType<DivineWings>()));
            normalOnly.OnSuccess(ItemDropRule.Common(ModContent.ItemType<Cattail>()));
            normalOnly.OnSuccess(ItemDropRule.Common(ModContent.ItemType<SeedOfWill>()));
            normalOnly.OnSuccess(ItemDropRule.Common(ModContent.ItemType<ThePurifier>()));
            normalOnly.OnSuccess(ItemDropRule.Common(ModContent.ItemType<GraphicalUniverseImager>()));
            normalOnly.OnSuccess(ItemDropRule.Common(ModContent.ItemType<NuminousDye>(), 1, 3, 5));
            normalOnly.OnSuccess(ItemDropRule.Common(ModContent.ItemType<GodglimmerDye>(), 1, 3, 5));
            normalOnly.OnSuccess(ItemDropRule.Common(ModContent.ItemType<MoonBurnDye>(), 1, 3, 5));
            normalOnly.OnSuccess(ItemDropRule.Common(ModContent.ItemType<TriadicFractureDye>(), 1, 3, 5));
        }
        npcLoot.Add(normalOnly);

        // Revengeance/Master exclusive items.
        LeadingConditionRule revOrMaster = new LeadingConditionRule(new RevengeanceOrMasterDropRule());
        revOrMaster.OnSuccess(ItemDropRule.Common(RelicID));
        revOrMaster.OnSuccess(ItemDropRule.Common(ModContent.ItemType<BlackHole>()));
        revOrMaster.OnSuccess(ItemDropRule.Common(ModContent.ItemType<Starseed>()));
        revOrMaster.OnSuccess(ItemDropRule.Common(ModContent.ItemType<Erilucyxwyn>(), 20000));
        npcLoot.Add(revOrMaster);

        // GFB exclusive items.
        LeadingConditionRule gfb = new LeadingConditionRule(new GFBDropRule());
        gfb.OnSuccess(ItemDropRule.Common(ModContent.ItemType<WrathOfTheGods>()));
        npcLoot.Add(gfb);

        // Lore item.
        npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<LoreNamelessDeity>()));

        // Hunt exclusive auric souls.
        LeadingConditionRule huntEnabled = new LeadingConditionRule(new HuntEnabledDropRule());
        {
            huntEnabled.OnSuccess(ItemDropRule.Common(ModContent.ItemType<NamelessAuricSoul>()));
        }
        npcLoot.Add(huntEnabled);

        // Vanity and decorations.
        npcLoot.Add(ItemDropRule.Common(MaskID, 7));
        npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<NamelessDeityTrophy>(), 10));
    }

    public static void ModifyNPCBagLoot(ItemLoot bagLoot)
    {
        // General drops.
        bagLoot.Add(ItemDropRule.Common(ModContent.ItemType<Moonscreen>()));
        bagLoot.Add(ItemDropRule.Common(ModContent.ItemType<CheatPermissionSlip>()));
        bagLoot.Add(ItemDropRule.Common(ModContent.ItemType<DeificTouch>()));
        bagLoot.Add(ItemDropRule.Common(ModContent.ItemType<DivineWings>()));
        bagLoot.Add(ItemDropRule.Common(ModContent.ItemType<Cattail>()));
        bagLoot.Add(ItemDropRule.Common(ModContent.ItemType<SeedOfWill>()));
        bagLoot.Add(ItemDropRule.Common(ModContent.ItemType<ThePurifier>()));
        bagLoot.Add(ItemDropRule.Common(ModContent.ItemType<GraphicalUniverseImager>()));
        bagLoot.Add(ItemDropRule.Common(ModContent.ItemType<NuminousDye>(), 1, 3, 5));
        bagLoot.Add(ItemDropRule.Common(ModContent.ItemType<GodglimmerDye>(), 1, 3, 5));
        bagLoot.Add(ItemDropRule.Common(ModContent.ItemType<MoonBurnDye>(), 1, 3, 5));
        bagLoot.Add(ItemDropRule.Common(ModContent.ItemType<TriadicFractureDye>(), 1, 3, 5));
    }

    #endregion Loot
}
