using System.Diagnostics;
using System.Reflection;
using Luminance.Core.Cutscenes;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;
using NoxusBoss.Content.NPCs.Bosses.Draedon;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.Rendering.RenderSteps;
using NoxusBoss.Content.NPCs.Friendly;
using NoxusBoss.Content.Particles;
using NoxusBoss.Content.Projectiles.Visuals;
using NoxusBoss.Content.Tiles;
using NoxusBoss.Content.Tiles.TileEntities;
using NoxusBoss.Core.Autoloaders;
using NoxusBoss.Core.CrossCompatibility.Inbound;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using NoxusBoss.Core.SoundSystems;
using NoxusBoss.Core.SoundSystems.Music;
using NoxusBoss.Core.World.WorldGeneration;
using NoxusBoss.Core.World.WorldSaving;
using ReLogic.Graphics;
using SubworldLibrary;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace NoxusBoss.Core.World.GameScenes.EndCredits;

public class EndCreditsScene : Cutscene
{
    public enum CreditsState
    {
        MoveSolynAndPlayerToTree,
        SitDownForAWhile,
        GrabApplesFromTree,
        PlayerAndSolynEatApples,
        WaitAfterEatingApples,
        NamelessGivesSolynAndPlayerHeadpats,
        NamelessAppears,
        NamelessFliesAway,
        WaitAfterNamelessFliesAway,
        ShyAvatarAppears,
        MarsBows,
        ScreenFadesToBlack,
        RecordingSoftwareRant_Regular,
        RecordingSoftwareRant_WhyDidYouSkipTheBosses,
        RecordingSoftwareRant_HowDidYouDoThisSoQuicklyLmao,
    }

    /// <summary>
    /// How far long the Lotus of Creation is in its appearance animation.
    /// </summary>
    public float LotusAppearanceInterpolant
    {
        get;
        set;
    }

    /// <summary>
    /// The timer used by Nameless during his recording based rants.
    /// </summary>
    public int RantTimer
    {
        get;
        set;
    }

    /// <summary>
    /// The ID override for the music.
    /// </summary>
    public int? MusicIDOverride
    {
        get;
        set;
    }

    /// <summary>
    /// The current state of the credits.
    /// </summary>
    public CreditsState State
    {
        get;
        set;
    }

    /// <summary>
    /// How much the camera is being panned.
    /// </summary>
    public float CameraPanInterpolant => SmoothStep(0f, 1f, InverseLerp(0f, CameraPanTime, Timer));

    /// <summary>
    /// How long the camera spends panning at the start.
    /// </summary>
    public static int CameraPanTime => SecondsToFrames(2.5f);

    /// <summary>
    /// How long Solyn and the player spend sitting down together the first time.
    /// </summary>
    public static int InitialSitWaitTime => SecondsToFrames(13f);

    /// <summary>
    /// How long Solyn spends dislodging apples from the tree.
    /// </summary>
    public static int AppleDislodgeTime => SecondsToFrames(4.5f);

    /// <summary>
    /// How long Solyn spends bringing applies to herself and the player after they're dislodged from the tree.
    /// </summary>
    public static int AppleTelekinesisTime => SecondsToFrames(3f);

    /// <summary>
    /// How long Solyn waits before taking a bite out of her apple.
    /// </summary>
    public static int AppleBiteDelayTimeSolyn => SecondsToFrames(1.2f);

    /// <summary>
    /// How long the player waits before taking a bite out of their apple.
    /// </summary>
    public static int AppleBiteDelayTimePlayer => SecondsToFrames(1.97f);

    /// <summary>
    /// How long Solyn and the player wait after the apples fly away before sitting in place again and starting the credits.
    /// </summary>
    public static int AppleBiteTransitionDelay => SecondsToFrames(2f);

    /// <summary>
    /// How long Solyn and the player spend sitting down together the second time, after eating apples.
    /// </summary>
    public static int SecondSitWaitTime => SecondsToFrames(13.5f);

    /// <summary>
    /// How long Solyn and the player spend receiving headpats from Nameless.
    /// </summary>
    public static int HeadpatTime => SecondsToFrames(8f);

    /// <summary>
    /// How long Solyn and the player spend sitting down together the second time, after Nameless flies away.
    /// </summary>
    public static int ThirdSitWaitTime => SecondsToFrames(16f);

    /// <summary>
    /// How long Nameless spends doing his lotus summon animation.
    /// </summary>
    public static int NamelessAppearanceTime => SecondsToFrames(7.75f);

    /// <summary>
    /// How long Nameless spends flying away.
    /// </summary>
    public static int NamelessFlyAwayTime => SecondsToFrames(3.75f);

    /// <summary>
    /// How long the Avatar of Emptiness spends hovering at the top of the screen.
    /// </summary>
    public static int ShyAvatarAnimationTime => SecondsToFrames(8f);

    /// <summary>
    /// How long Mars waits before appearing to do his bow animation.
    /// </summary>
    public static int MarsBowAnimationDelay => SecondsToFrames(2f);

    /// <summary>
    /// How long Mars spends doing his bow animation.
    /// </summary>
    public static int MarsBowAnimationTime => SecondsToFrames(6.5f);

    /// <summary>
    /// How long it takes for the screen to fade to black.
    /// </summary>
    public static int ScreenFadesToBlackTime => SecondsToFrames(20f);

    /// <summary>
    /// The color used for the programmer title.
    /// </summary>
    public static Color ProgrammerTitleColor => new(183, 1, 27);

    /// <summary>
    /// The color used for developers distinguised as programmers.
    /// </summary>
    public static Color ProgrammerNameColor => new(255, 108, 113);

    /// <summary>
    /// The color used for the artist title.
    /// </summary>
    public static Color ArtistTitleColor => new(23, 165, 100);

    /// <summary>
    /// The color used for developers distinguised as artists.
    /// </summary>
    public static Color ArtistNameColor => new(150, 255, 170);

    /// <summary>
    /// The color used for the musician title.
    /// </summary>
    public static Color MusicianTitleColor => new(12, 155, 198);

    /// <summary>
    /// The color used for developers distinguised as musicians.
    /// </summary>
    public static Color MusicianNameColor => new(155, 255, 255);

    /// <summary>
    /// The color used for the sound designer title.
    /// </summary>
    public static Color SoundDesignerTitleColor => new(255, 205, 25);

    /// <summary>
    /// The color used for developers distinguised as sound designers.
    /// </summary>
    public static Color SoundDesignerNameColor => new(255, 248, 186);

    /// <summary>
    /// The color used for the quality assurance team title.
    /// </summary>
    public static Color QualityAssuranceTitleColor => new(116, 51, 206);

    /// <summary>
    /// The color used for testers distinguised as part of the quality assurance team.
    /// </summary>
    public static Color QualityAssuranceNameColor => new(234, 193, 255);

    /// <summary>
    /// The threshold for how long the player's play time has to be for Nameless to not be suspicious of them.
    /// </summary>
    public static TimeSpan LikelyCheatingPlayerSaveTime => new(hours: 4, minutes: 0, seconds: 0);

    /// <summary>
    /// The threshold for how many bosses the player has to have defeated for Nameless to not be suspicious of them.
    /// </summary>
    public static int LikelyCheatingPlayerBossKillCount => CalamityCompatibility.Enabled ? 7 : 12;

    /// <summary>
    /// The set of software names that Nameless searches for to determine if the player is recording the cutscene.
    /// </summary>
    public static readonly string[] RecordingSoftwareSearchNames = new string[]
    {
        "OBS",
        "Bandicam",
        "XSplit",
        "ShadowPlay"
    };

    public override int CutsceneLength => SecondsToFrames(9999f);

    public override BlockerSystem.BlockCondition GetBlockCondition => new(true, true, () => IsActive);

    public override void Load()
    {
        TotalScreenOverlaySystem.DrawAfterWhiteEvent += RenderNamelessDuringRant;

        string contentPath = GetAssetPath("Content/Items/Placeable/MusicBoxes", "EndCreditsMusicBox");
        string musicPath = "Assets/Sounds/Music/EndCredits";
        MusicBoxAutoloader.Create(Mod, contentPath, musicPath, out _, out _);
    }

    public override void OnBegin()
    {
        State = CreditsState.MoveSolynAndPlayerToTree;
    }

    public override void Update()
    {
        // This definitely isn't gonna work well in multiplayer and honestly it's more special if the player sees it alone.
        // Also I'm lazy.
        if (Main.netMode != NetmodeID.SinglePlayer)
        {
            EndAbruptly = true;
            return;
        }

        // Where's Solyn????
        int solynIndex = NPC.FindFirstNPC(ModContent.NPCType<Solyn>());
        if (solynIndex == -1)
        {
            EndAbruptly = true;
            return;
        }

        MusicIDOverride = null;

        // Oh there she is. Thank god.
        // Or is it thank Nameless? I don't know.
        NPC solyn = Main.npc[solynIndex];
        solyn.As<Solyn>().CurrentState = SolynAIType.EndCreditsCutscene;

        Main.gamePaused = false;

        switch (State)
        {
            case CreditsState.MoveSolynAndPlayerToTree:
                MoveSolynAndPlayerToTree(solyn);
                break;
            case CreditsState.SitDownForAWhile:
                SitDownForAWhile(solyn);
                break;
            case CreditsState.GrabApplesFromTree:
                GrabApplesFromTree(solyn);
                break;
            case CreditsState.WaitAfterEatingApples:
                WaitAfterEatingApples(solyn);
                break;
            case CreditsState.NamelessGivesSolynAndPlayerHeadpats:
                ReceiveHeadpatsFromNameless(solyn);
                break;
            case CreditsState.NamelessAppears:
                NamelessAppears(solyn);
                break;
            case CreditsState.NamelessFliesAway:
                NamelessFliesAway(solyn);
                break;
            case CreditsState.WaitAfterNamelessFliesAway:
                WaitAfterNamelessFliesAway(solyn);
                break;
            case CreditsState.ShyAvatarAppears:
                ShyAvatarAppears(solyn);
                break;
            case CreditsState.MarsBows:
                MarsBows(solyn);
                break;
            case CreditsState.ScreenFadesToBlack:
                ScreenFadesToBlack(solyn);
                break;
            case CreditsState.RecordingSoftwareRant_Regular:
            case CreditsState.RecordingSoftwareRant_WhyDidYouSkipTheBosses:
            case CreditsState.RecordingSoftwareRant_HowDidYouDoThisSoQuicklyLmao:
                RecordingSoftwareRant();
                break;
        }

        // Ensure that Solyn blinks from time to time, rather than awkwardly starting into nothingness.
        if (Timer % 256 <= 5)
        {
            ref int solynFrame = ref solyn.As<Solyn>().Frame;
            if (solynFrame == 0f)
                solynFrame = 20;
            if (solynFrame == 18f)
                solynFrame = 41;
        }

        CalamityCompatibility.ResetRippers(Main.LocalPlayer);
    }

    /// <summary>
    /// Executes the cutscene state that makes the player and Solyn walk to the tree.
    /// </summary>
    public void MoveSolynAndPlayerToTree(NPC solyn)
    {
        Vector2 playerDestination = FindGroundVertical(new(Main.maxTilesX / 2 - 4, EternalGardenWorldGen.SurfaceTilePoint + 15)).ToWorldCoordinates();
        Vector2 solynDestination = FindGroundVertical(new(Main.maxTilesX / 2, EternalGardenWorldGen.SurfaceTilePoint + 15)).ToWorldCoordinates();
        if (Distance(solyn.Center.X, solynDestination.X) <= 3f && Distance(Main.LocalPlayer.Center.X, playerDestination.X) <= 3f)
        {
            State = CreditsState.SitDownForAWhile;
            return;
        }

        Main.LocalPlayer.position.X = Lerp(Main.LocalPlayer.position.X, playerDestination.X - Main.LocalPlayer.width * 0.5f, 0.002f);
        Main.LocalPlayer.velocity.X = Main.LocalPlayer.SafeDirectionTo(playerDestination).X * 2.2f;
        Main.LocalPlayer.direction = Main.LocalPlayer.velocity.X.NonZeroSign();

        solynDestination.X = Lerp(solynDestination.X, solynDestination.X - solyn.width * 0.5f, 0.002f);
        solyn.velocity.X = solyn.SafeDirectionTo(solynDestination).X * 2f;
        solyn.spriteDirection = solyn.velocity.X.NonZeroSign();
        solyn.As<Solyn>().PerformStandardFraming();

        // I LOVE YOU HAMMERED BLOCKS!!
        float _ = 0.3f;
        Collision.StepUp(ref solyn.position, ref solyn.velocity, solyn.width, solyn.height, ref _, ref solyn.gfxOffY);

        // Wait until Solyn and the player are in position before letting the animation proceed further.
        if (Timer > CameraPanTime)
            Timer = CameraPanTime;
    }

    /// <summary>
    /// Executes the cutscene state that makes the player and Solyn sit under the tree after walking to it.
    /// </summary>
    public void SitDownForAWhile(NPC solyn)
    {
        Main.LocalPlayer.velocity.X *= 0.85f;
        Main.LocalPlayer.direction = -1;

        solyn.As<Solyn>().Frame = 18;
        solyn.velocity.X *= 0.85f;
        solyn.spriteDirection = -1;

        if (Timer > CameraPanTime + InitialSitWaitTime)
            State = CreditsState.GrabApplesFromTree;
    }

    /// <summary>
    /// Executes the cutscene state that makes the player and Solyn grab and eat two good apples from the tree.
    /// </summary>
    public void GrabApplesFromTree(NPC solyn)
    {
        int appleID = ModContent.ProjectileType<EndCreditsGoodApple>();
        if (Timer > CameraPanTime + InitialSitWaitTime + AppleDislodgeTime + AppleTelekinesisTime + AppleBiteDelayTimePlayer + AppleBiteTransitionDelay + AppleBiteTransitionDelay)
        {
            State = CreditsState.WaitAfterEatingApples;
            return;
        }

        if (Timer == CameraPanTime + InitialSitWaitTime + 2)
        {
            foreach (Projectile proj in Main.ActiveProjectiles)
            {
                if (proj.type == appleID)
                    proj.Kill();
            }

            TEGoodAppleTree? tree = null;
            foreach (TileEntity te in TileEntity.ByPosition.Values)
            {
                if (te is TEGoodAppleTree treeInstance)
                {
                    tree = treeInstance;
                    break;
                }
            }

            if (tree is not null)
            {
                TEGoodAppleTree.Apple solynsAppleOnTheTree = tree.ApplesOnTree[8];
                TEGoodAppleTree.Apple yourAppleOnTheTree = tree.ApplesOnTree[0];

                Vector2 treeOffset = tree.Position.ToWorldCoordinates() + new Vector2(120f, 150f);
                NewProjectileBetter(new EntitySource_WorldEvent(), treeOffset + solynsAppleOnTheTree.StandardOffset, Vector2.Zero, appleID, 0, 0f, Main.myPlayer, 0.75f, 0f);
                NewProjectileBetter(new EntitySource_WorldEvent(), treeOffset + yourAppleOnTheTree.StandardOffset, Vector2.Zero, appleID, 0, 0f, Main.myPlayer, 0.75f, 1f);

                solynsAppleOnTheTree.Active = false;
                yourAppleOnTheTree.Active = false;
            }
        }

        int relativeTimer = Timer - CameraPanTime - InitialSitWaitTime;
        solyn.As<Solyn>().Frame = relativeTimer <= 60 ? 20 : 0;

        var apples = AllProjectilesByID(appleID);
        if (apples.Count() < 2)
            return;

        Projectile yourApple = apples.Where(a => !a.As<EndCreditsGoodApple>().ForSolyn).First();
        Projectile solynsApple = apples.Where(a => a.As<EndCreditsGoodApple>().ForSolyn).First();

        if (relativeTimer <= AppleDislodgeTime && relativeTimer % 60 == 14)
        {
            yourApple.As<EndCreditsGoodApple>().AngularVelocity += Main.rand.NextFloatDirection() * Sqrt(relativeTimer / (float)AppleDislodgeTime) * 0.3f;
            solynsApple.As<EndCreditsGoodApple>().AngularVelocity += Main.rand.NextFloatDirection() * Sqrt(relativeTimer / (float)AppleDislodgeTime) * 0.3f;
            yourApple.As<EndCreditsGoodApple>().PsychicPulseOpacity = 1f;
            solynsApple.As<EndCreditsGoodApple>().PsychicPulseOpacity = 1f;
        }

        if (relativeTimer >= AppleDislodgeTime && relativeTimer < AppleDislodgeTime + AppleTelekinesisTime + AppleBiteDelayTimePlayer)
        {
            float speedFactor = InverseLerp(0f, 30f, relativeTimer - AppleDislodgeTime);

            Vector2 playerHandPosition = Main.LocalPlayer.Center + new Vector2(Main.LocalPlayer.direction * 0.9f, 1.15f).RotatedBy(Main.LocalPlayer.compositeBackArm.rotation + 0.2f) * 16f;
            yourApple.Center = yourApple.Center.MoveTowards(playerHandPosition, 0.11f);
            yourApple.velocity = (playerHandPosition - yourApple.Center) * speedFactor * 0.022f;
            yourApple.rotation = yourApple.rotation.AngleLerp(0f, 0.02f);

            float idealPlayerHandRotation = InverseLerp(60f, 120f, relativeTimer - AppleDislodgeTime) * 1.55f;
            if (relativeTimer >= AppleDislodgeTime + AppleTelekinesisTime + AppleBiteDelayTimePlayer)
                idealPlayerHandRotation = 0f;

            float playerHandRotation = Main.LocalPlayer.compositeBackArm.rotation.AngleLerp(idealPlayerHandRotation, 0.5f);
            Main.LocalPlayer.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters, playerHandRotation);

            Vector2 solynHandPosition = solyn.Center + new Vector2(solyn.spriteDirection * 19f, -12f);
            solynsApple.Center = solynsApple.Center.MoveTowards(solynHandPosition, 0.11f);
            solynsApple.velocity = (solynHandPosition - solynsApple.Center) * speedFactor * 0.022f;
            solynsApple.rotation = solynsApple.rotation.AngleLerp(0f, 0.02f);
        }

        // Make Solyn bite her apple.
        if (relativeTimer == AppleDislodgeTime + AppleTelekinesisTime + AppleBiteDelayTimeSolyn)
            solynsApple.As<EndCreditsGoodApple>().Bite();
        if (relativeTimer >= AppleDislodgeTime + AppleTelekinesisTime + AppleBiteDelayTimeSolyn && relativeTimer <= AppleDislodgeTime + AppleTelekinesisTime + AppleBiteDelayTimeSolyn + 9)
            solyn.As<Solyn>().Frame = 20;

        // Make the player bite their apple.
        if (relativeTimer == AppleDislodgeTime + AppleTelekinesisTime + AppleBiteDelayTimePlayer)
            yourApple.As<EndCreditsGoodApple>().Bite();
        if (relativeTimer >= AppleDislodgeTime + AppleTelekinesisTime + AppleBiteDelayTimePlayer && relativeTimer <= AppleDislodgeTime + AppleTelekinesisTime + AppleBiteDelayTimePlayer + 9)
            Main.LocalPlayer.eyeHelper.BlinkBecausePlayerGotHurt();
    }

    /// <summary>
    /// Executes the cutscene state that makes the player and Solyn sit and wait for a while after eating good apples.
    /// </summary>
    public void WaitAfterEatingApples(NPC solyn)
    {
        solyn.As<Solyn>().Frame = 18;

        if (Timer >= CameraPanTime + InitialSitWaitTime + AppleDislodgeTime + AppleTelekinesisTime + AppleBiteDelayTimePlayer + AppleBiteTransitionDelay + AppleBiteTransitionDelay + SecondSitWaitTime)
            State = CreditsState.NamelessGivesSolynAndPlayerHeadpats;
    }

    /// <summary>
    /// Executes the cutscene state that makes Solyn receive headpats from Nameless while the player looks at her.
    /// </summary>
    public void ReceiveHeadpatsFromNameless(NPC solyn)
    {
        int relativeTime = Timer - (CameraPanTime + InitialSitWaitTime + AppleDislodgeTime + AppleTelekinesisTime + AppleBiteDelayTimePlayer + AppleBiteTransitionDelay + AppleBiteTransitionDelay + SecondSitWaitTime);
        if (relativeTime >= 5 && NamelessDeityBoss.Myself is null)
            NPC.NewNPC(new EntitySource_WorldEvent(), Main.maxTilesX * 8, 100, ModContent.NPCType<NamelessDeityBoss>(), 1);
        if (NamelessDeityBoss.Myself is not null && NamelessDeityBoss.Myself_CurrentState != NamelessDeityBoss.NamelessAIType.EndCreditsScene)
            NamelessDeityBoss.Myself.As<NamelessDeityBoss>().StateMachine.StateStack.Push(NamelessDeityBoss.Myself.As<NamelessDeityBoss>().StateMachine.StateRegistry[NamelessDeityBoss.NamelessAIType.EndCreditsScene]);

        solyn.As<Solyn>().Frame = 18;

        if (NamelessDeityBoss.Myself is null)
            return;

        NamelessDeityBoss nameless = NamelessDeityBoss.Myself.As<NamelessDeityBoss>();
        nameless.RenderComposite.Find<ArmsStep>().ArmTexture.ForceToTexture("Arm1");
        nameless.RenderComposite.Find<ArmsStep>().ForearmTexture.ForceToTexture("Forearm1");
        nameless.RenderComposite.Find<ArmsStep>().HandTexture.ForceToTexture("Hand1");

        if (relativeTime == 6)
            nameless.ConjureHandsAtPosition(solyn.Top - Vector2.UnitY * 100f, Vector2.Zero);

        bool doneDoingHeadpats = relativeTime >= HeadpatTime - 90;
        bool playerIsLookingAtSolyn = relativeTime >= 75 && relativeTime <= HeadpatTime - 30;
        Main.LocalPlayer.direction = (int)Main.LocalPlayer.HorizontalDirectionTo(solyn.Center) * playerIsLookingAtSolyn.ToDirectionInt();

        if (nameless.Hands.Count >= 1)
        {
            NamelessDeityBoss.Myself.Opacity = 1f;
            NamelessDeityBoss.Myself.position.X = Main.LocalPlayer.Center.X - 600f;

            int headpatPeriod = 75;
            float headpatSinusoid = Cos01(TwoPi * relativeTime / headpatPeriod);
            Vector2 headpatPosition = solyn.Top - Vector2.UnitY * (Pow(headpatSinusoid + 0.001f, 0.4f) * 100f + 64f);
            headpatPosition.X += 120f;

            // Make Solyn squish and squeak when she's patted.
            if (relativeTime % headpatPeriod == headpatPeriod / 2 && !doneDoingHeadpats)
            {
                SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.Headpat with { Volume = 0.6f, MaxInstances = 0, PitchVariance = 0.14f });
                solyn.As<Solyn>().Squish += 0.135f;
            }

            // Make Solyn blush after being patted.
            if (relativeTime % headpatPeriod >= headpatPeriod / 2 && relativeTime % headpatPeriod <= headpatPeriod / 2 + 10 && !doneDoingHeadpats)
                solyn.As<Solyn>().Frame = 40;

            nameless.Hands[0].HasArms = false;
            nameless.Hands[0].DirectionOverride = -1;
            if (doneDoingHeadpats)
            {
                if (nameless.Hands[0].FreeCenter != Vector2.Zero)
                {
                    NamelessDeityBoss.CreateHandVanishVisuals(nameless.Hands[0]);
                    nameless.Hands[0].FreeCenter = Vector2.Zero;
                }
            }
            else
            {
                nameless.Hands[0].Velocity = Vector2.Zero;
                nameless.Hands[0].FreeCenter = headpatPosition;
                nameless.Hands[0].RotationOffset = Pi + Lerp(-0.1f, 0.31f, headpatSinusoid);
            }
            nameless.DrawHandsSeparateFromRT = true;
            nameless.UpdateWings(Timer / 50f);
        }

        if (relativeTime >= HeadpatTime)
            State = CreditsState.NamelessAppears;
    }

    /// <summary>
    /// Executes the cutscene state that makes Nameless appear and create a special lotus in the garden.
    /// </summary>
    public void NamelessAppears(NPC solyn)
    {
        int relativeTime = Timer - (CameraPanTime + InitialSitWaitTime + AppleDislodgeTime + AppleTelekinesisTime + AppleBiteDelayTimePlayer + AppleBiteTransitionDelay + AppleBiteTransitionDelay + SecondSitWaitTime + HeadpatTime);
        if (NamelessDeityBoss.Myself is null)
            return;

        NamelessDeityBoss nameless = NamelessDeityBoss.Myself.As<NamelessDeityBoss>();

        if (relativeTime == 2)
        {
            nameless.RerollAllSwappableTextures();
            nameless.StartTeleportAnimation(() => Main.LocalPlayer.Center + new Vector2(-254f, -440f), 10, 15);
        }

        // Manage hands.
        while (nameless.Hands.Count(h => h.HasArms) < 2)
            nameless.Hands.Insert(0, new(NamelessDeityBoss.Myself.Center, true));

        float handBobOffset = Sin(TwoPi * Timer / 125f) * 50f;
        float raiseHandInterpolant = InverseLerp(NamelessAppearanceTime - SecondsToFrames(4.5f), NamelessAppearanceTime - SecondsToFrames(4.5f) + 11, relativeTime);
        float handHoverOffset = Lerp(950f, 600f, raiseHandInterpolant);
        Vector2 leftHandOffset = new Vector2(-handHoverOffset, 100f - raiseHandInterpolant * 900f + handBobOffset);
        Vector2 rightHandOffset = new Vector2(handHoverOffset, 100f - raiseHandInterpolant * 900f + handBobOffset);

        nameless.DefaultHandDrift(nameless.Hands[0], NamelessDeityBoss.Myself.Center + leftHandOffset * nameless.TeleportVisualsAdjustedScale, 1f);
        nameless.DefaultHandDrift(nameless.Hands[1], NamelessDeityBoss.Myself.Center + rightHandOffset * nameless.TeleportVisualsAdjustedScale, 1f);
        nameless.Hands[0].DirectionOverride = 0;
        nameless.Hands[1].DirectionOverride = 0;
        nameless.Hands[0].RotationOffset = 0f;
        nameless.Hands[1].RotationOffset = 0f;
        nameless.Hands[0].HasArms = true;
        nameless.Hands[1].HasArms = true;

        nameless.UpdateWings(Timer / 60f);

        NamelessDeityBoss.Myself.dontTakeDamage = true;

        if (relativeTime >= 34)
            solyn.As<Solyn>().Frame = 0;

        if (relativeTime == NamelessAppearanceTime - SecondsToFrames(4.7f))
        {
            SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.Mumble with { Volume = 0.5f });
            ScreenShakeSystem.StartShake(4f);
        }

        // Place the lotus.
        if (relativeTime == NamelessAppearanceTime - SecondsToFrames(3f))
        {
            nameless.Hands[0].Velocity.X += 10f;
            nameless.Hands[1].Velocity.X -= 10f;
            SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.FingerSnap with { Volume = 1.1f });
            SoundEngine.PlaySound(GennedAssets.Sounds.NamelessDeity.CreateCreditsLotus).WithVolumeBoost(1.37f);
            ScreenShakeSystem.StartShake(6f);

            while (NamelessDeityBoss.Myself.Center.Y <= 100f)
                NamelessDeityBoss.Myself.position.Y += 100f;

            Point lotusPoint = FindGround(NamelessDeityBoss.Myself.Center.ToTileCoordinates(), Vector2.UnitY);
            Vector2 lotusPointWorld = lotusPoint.ToWorldCoordinates(0f, 8f) - Vector2.UnitY * 16f;
            WorldGen.PlaceTile(lotusPoint.X, lotusPoint.Y, ModContent.TileType<LotusOfCreationTile>());

            MagicBurstParticle burst = new MagicBurstParticle(lotusPointWorld, Vector2.Zero, Color.Wheat, 12, 2f);
            burst.Spawn();

            StrongBloom bloom = new StrongBloom(lotusPointWorld, Vector2.Zero, Color.LightPink, 1.25f, 60);
            bloom.Spawn();

            NewProjectileBetter(new EntitySource_WorldEvent(), lotusPointWorld, Vector2.Zero, ModContent.ProjectileType<LotusOfCreationAppearanceVisual>(), 0, 0f);
        }

        LotusAppearanceInterpolant = InverseLerp(NamelessAppearanceTime - SecondsToFrames(3f), NamelessAppearanceTime - SecondsToFrames(1.5f), relativeTime);

        if (relativeTime >= NamelessAppearanceTime)
            State = CreditsState.NamelessFliesAway;
    }

    /// <summary>
    /// Executes the cutscene state that makes Nameless fly away.
    /// </summary>
    public void NamelessFliesAway(NPC solyn)
    {
        int relativeTime = Timer - (CameraPanTime + InitialSitWaitTime + AppleDislodgeTime + AppleTelekinesisTime + AppleBiteDelayTimePlayer + AppleBiteTransitionDelay + AppleBiteTransitionDelay + SecondSitWaitTime + HeadpatTime + NamelessAppearanceTime);
        if (NamelessDeityBoss.Myself is null)
            return;

        NamelessDeityBoss nameless = NamelessDeityBoss.Myself.As<NamelessDeityBoss>();
        float handBobOffset = Sin(TwoPi * Timer / 125f) * 50f;
        Vector2 leftHandOffset = new Vector2(-600f, -800f + handBobOffset);
        Vector2 rightHandOffset = new Vector2(600f, -800f + handBobOffset);
        nameless.DefaultHandDrift(nameless.Hands[0], NamelessDeityBoss.Myself.Center + leftHandOffset * nameless.TeleportVisualsAdjustedScale, 1f);
        nameless.DefaultHandDrift(nameless.Hands[1], NamelessDeityBoss.Myself.Center + rightHandOffset * nameless.TeleportVisualsAdjustedScale, 1f);
        nameless.Hands[0].DirectionOverride = 0;
        nameless.Hands[1].DirectionOverride = 0;
        nameless.Hands[0].RotationOffset = 0f;
        nameless.Hands[1].RotationOffset = 0f;
        nameless.Hands[0].HasArms = true;
        nameless.Hands[1].HasArms = true;
        nameless.NPC.velocity.X -= 0.2f;
        nameless.NPC.velocity.Y -= 0.7f;
        nameless.UpdateWings(Timer / 60f);

        NamelessDeityBoss.Myself.dontTakeDamage = true;

        if (relativeTime >= 40)
            solyn.As<Solyn>().Frame = 18;

        if (relativeTime >= NamelessFlyAwayTime)
            State = CreditsState.WaitAfterNamelessFliesAway;
    }

    /// <summary>
    /// Executes the cutscene state that makes Solyn and the player sit in place after Nameless flies away.
    /// </summary>
    public void WaitAfterNamelessFliesAway(NPC solyn)
    {
        solyn.As<Solyn>().Frame = 18;

        if (NamelessDeityBoss.Myself is not null)
            NamelessDeityBoss.Myself.velocity = Vector2.Zero;

        if (Timer >= CameraPanTime + InitialSitWaitTime + AppleDislodgeTime + AppleTelekinesisTime + AppleBiteDelayTimePlayer + AppleBiteTransitionDelay +
            AppleBiteTransitionDelay + SecondSitWaitTime + HeadpatTime + NamelessAppearanceTime + NamelessFlyAwayTime + ThirdSitWaitTime)
        {
            State = CreditsState.ShyAvatarAppears;
        }
    }

    /// <summary>
    /// Executes the cutscene state that makes the Avatar appear and wave at Solyn and the player.
    /// </summary>
    public void ShyAvatarAppears(NPC solyn)
    {
        int relativeTime = Timer - (CameraPanTime + InitialSitWaitTime + AppleDislodgeTime + AppleTelekinesisTime + AppleBiteDelayTimePlayer + AppleBiteTransitionDelay + AppleBiteTransitionDelay + SecondSitWaitTime + HeadpatTime + NamelessAppearanceTime + NamelessFlyAwayTime + ThirdSitWaitTime);
        if (relativeTime >= 5 && AvatarOfEmptiness.Myself is null)
        {
            int topOfScreen = (int)Main.screenPosition.Y - 1800;
            NPC.NewNPC(new EntitySource_WorldEvent(), Main.maxTilesX * 8, topOfScreen, ModContent.NPCType<AvatarOfEmptiness>(), 1);
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.Phase2IntroHeadEmerge);
        }
        if (NamelessDeityBoss.Myself is not null)
        {
            NamelessDeityBoss.Myself.velocity = Vector2.Zero;
            NamelessDeityBoss.Myself.Center = Main.LocalPlayer.Center - Vector2.UnitY * 7000f;
            NamelessDeityBoss.Myself.dontTakeDamage = true;
        }
        if (AvatarOfEmptiness.Myself is not null && AvatarOfEmptiness.Myself.As<AvatarOfEmptiness>().CurrentState != AvatarOfEmptiness.AvatarAIType.EndCreditsScene && AvatarOfEmptiness.Myself.As<AvatarOfEmptiness>().CurrentState != AvatarOfEmptiness.AvatarAIType.Teleport)
            AvatarOfEmptiness.Myself.As<AvatarOfEmptiness>().StateMachine.StateStack.Push(AvatarOfEmptiness.Myself.As<AvatarOfEmptiness>().StateMachine.StateRegistry[AvatarOfEmptiness.AvatarAIType.EndCreditsScene]);

        solyn.As<Solyn>().Frame = 18;

        if (relativeTime >= 50)
            Main.LocalPlayer.direction = 1;
        if (relativeTime >= 80)
        {
            solyn.spriteDirection = 1;
            solyn.As<Solyn>().Frame = 0;
        }

        if (AvatarOfEmptiness.Myself is null)
            return;

        AvatarOfEmptiness avatar = AvatarOfEmptiness.Myself.As<AvatarOfEmptiness>();
        if (relativeTime <= ShyAvatarAnimationTime - 30)
        {
            float verticalOffset = SmoothStep(-1050f, -200f, InverseLerp(0f, 90f, relativeTime));
            float reachOutInterpolant = InverseLerp(ShyAvatarAnimationTime - 175f, ShyAvatarAnimationTime - 90f, relativeTime);
            float waveInterpolant = InverseLerpBump(ShyAvatarAnimationTime - 175f, ShyAvatarAnimationTime - 90f, ShyAvatarAnimationTime - 42f, ShyAvatarAnimationTime - 30f, relativeTime);
            Vector2 idealHeadOffset = Vector2.UnitY * 750f;
            idealHeadOffset.X -= Lerp(Sin(TwoPi * relativeTime / (ShyAvatarAnimationTime - 30)).Cubed(), -1f, reachOutInterpolant * 0.8f) * -350f;

            Vector2 leftArmOffset = Vector2.UnitX * reachOutInterpolant * -150f;
            leftArmOffset.X -= Sin(TwoPi * relativeTime / 60f) * waveInterpolant * 120f;

            AvatarOfEmptiness.Myself.Center = new(Main.maxTilesX * 8f + 400f, Main.screenPosition.Y + verticalOffset);
            avatar.PerformBasicFrontArmUpdates(1f, leftArmOffset);
            avatar.HeadPosition = Vector2.Lerp(avatar.HeadPosition, AvatarOfEmptiness.Myself.Center + idealHeadOffset * avatar.HeadScale * avatar.NeckAppearInterpolant, 0.16f);
            avatar.LeftFrontArmOpacity = 1f;
            avatar.RightFrontArmOpacity = 1f;
            avatar.LeftFrontArmScale = 1f;
            avatar.RightFrontArmScale = 1f;
            avatar.LegScale = Vector2.One;
            avatar.HeadOpacity = 1f;
            avatar.HeadScaleFactor = 1f;
            avatar.NeckAppearInterpolant = InverseLerp(0f, 30f, relativeTime);
            avatar.MaskFrame = 23;
        }
        if (relativeTime == ShyAvatarAnimationTime - 30)
            avatar.StartTeleportAnimation(() => Main.LocalPlayer.Center - Vector2.UnitY * 7000f);

        if (relativeTime >= ShyAvatarAnimationTime)
        {
            AvatarOfEmptiness.Myself.active = false;
            State = CreditsState.MarsBows;
        }
    }

    /// <summary>
    /// Executes the cutscene state that makes the Avatar appear and wave at Solyn and the player.
    /// </summary>
    public void MarsBows(NPC solyn)
    {
        int relativeTime = Timer - (CameraPanTime + InitialSitWaitTime + AppleDislodgeTime + AppleTelekinesisTime + AppleBiteDelayTimePlayer + AppleBiteTransitionDelay + AppleBiteTransitionDelay + SecondSitWaitTime + HeadpatTime + NamelessAppearanceTime + NamelessFlyAwayTime + ThirdSitWaitTime + ShyAvatarAnimationTime + MarsBowAnimationDelay);
        if (relativeTime >= 5 && MarsBody.Myself is null)
        {
            int topOfScreen = (int)Main.screenPosition.Y - 100;
            NPC.NewNPC(new EntitySource_WorldEvent(), (int)Main.LocalPlayer.Center.X - 250, topOfScreen, ModContent.NPCType<MarsBody>(), 1);
            SoundEngine.PlaySound(GennedAssets.Sounds.Mars.PowerUp);
        }
        if (MarsBody.Myself is not null && MarsBody.Myself.As<MarsBody>().CurrentState != MarsBody.MarsAIType.EndCreditsScene)
            MarsBody.Myself.As<MarsBody>().StateMachine.StateStack.Push(MarsBody.Myself.As<MarsBody>().StateMachine.StateRegistry[MarsBody.MarsAIType.EndCreditsScene]);

        if (relativeTime >= MarsBowAnimationDelay + MarsBowAnimationTime)
        {
            if (MarsBody.Myself is not null)
                MarsBody.Myself.active = false;
            State = CreditsState.ScreenFadesToBlack;
        }

        if (MarsBody.Myself is null)
            return;

        float leaveInterpolant = InverseLerp(MarsBowAnimationTime - 90f, MarsBowAnimationTime - 30f, relativeTime);
        float bowInterpolant = InverseLerp(120f, 165f, relativeTime).Squared();
        Vector2 armHoverOffset = new Vector2(150f, 200f);
        armHoverOffset.X *= SmoothStep(1f, 0.5f, bowInterpolant);
        armHoverOffset.Y *= SmoothStep(1f, 0.67f, bowInterpolant.Squared());

        Vector2 leftHoverOffset = armHoverOffset * new Vector2(-1f, 1f);
        Vector2 rightHoverOffset = armHoverOffset;

        leftHoverOffset.X -= bowInterpolant * 300f;

        NPC mars = MarsBody.Myself;
        MarsBody marsModNPC = mars.As<MarsBody>();
        marsModNPC.MoveArmsTowards(leftHoverOffset, rightHoverOffset);
        marsModNPC.RailgunCannonAngle = marsModNPC.leftElbowPosition.AngleTo(marsModNPC.IdealLeftHandPosition) - PiOver2 * (1f - bowInterpolant);
        marsModNPC.EnergyCannonAngle = marsModNPC.rightElbowPosition.AngleTo(marsModNPC.IdealRightHandPosition);
        marsModNPC.ThrusterStrength = SmoothStep(0.6f, 1.3f, leaveInterpolant);

        // Descend.
        if (relativeTime <= 60)
        {
            if (relativeTime <= 2)
                mars.velocity = Vector2.Zero;

            mars.velocity = Vector2.UnitY * SmoothStep(0f, 60f, InverseLerp(60f, 0f, relativeTime).Squared());
        }
        else if (leaveInterpolant <= 0f)
            mars.velocity = Vector2.Lerp(mars.velocity, Vector2.UnitY * Cos(relativeTime * 0.05f) * (1f - bowInterpolant) * 2f, 0.02f);
        else
            mars.velocity.Y -= leaveInterpolant * 0.9f;

        if (relativeTime == 195)
        {
            ScreenShakeSystem.StartShake(2f);
            SoundEngine.PlaySound(GennedAssets.Sounds.Mars.Farewell);
        }

        // Bow down.
        mars.rotation = SmoothStep(0f, 0.2f, bowInterpolant);

        // Make Solyn and the player look at mars.
        solyn.As<Solyn>().PerformStandardFraming();
        solyn.spriteDirection = -1;
        Main.LocalPlayer.direction = -1;

        // Use the chainsaw.
        marsModNPC.ChainsawActivationInterpolant = 0f;
        marsModNPC.EnergyCannonChainsawActive = true;
    }

    /// <summary>
    /// Executes the cutscene state that makes the screen fade to black.
    /// </summary>
    public void ScreenFadesToBlack(NPC solyn)
    {
        int relativeTime = Timer - (CameraPanTime + InitialSitWaitTime + AppleDislodgeTime + AppleTelekinesisTime + AppleBiteDelayTimePlayer + AppleBiteTransitionDelay + AppleBiteTransitionDelay + SecondSitWaitTime + HeadpatTime + NamelessAppearanceTime + NamelessFlyAwayTime + ThirdSitWaitTime + ShyAvatarAnimationTime + MarsBowAnimationDelay + MarsBowAnimationTime);
        solyn.As<Solyn>().Frame = 18;

        if (NamelessDeityBoss.Myself is not null)
            NamelessDeityBoss.Myself.Center = Main.LocalPlayer.Center - Vector2.UnitY * 7000f;

        TotalScreenOverlaySystem.OverlayColor = Color.Black;
        TotalScreenOverlaySystem.OverlayInterpolant = InverseLerp(0f, ScreenFadesToBlackTime, relativeTime).Cubed() * 1.1f;

        if (relativeTime >= 60)
            solyn.spriteDirection = -1;

        MusicVolumeManipulationSystem.MuffleFactor = InverseLerp(ScreenFadesToBlackTime, ScreenFadesToBlackTime - 360f, relativeTime);

        if (relativeTime >= ScreenFadesToBlackTime)
        {
            if (IsUserRecording())
            {
                if (relativeTime >= 5 && NamelessDeityBoss.Myself is null)
                    NPC.NewNPC(new EntitySource_WorldEvent(), Main.maxTilesX * 8, 100, ModContent.NPCType<NamelessDeityBoss>(), 1);

                State = CreditsState.RecordingSoftwareRant_Regular;

                bool eitherHiveBossDefeated = CommonCalamityVariables.HiveMindDefeated || CommonCalamityVariables.PerforatorHiveDefeated;
                int totalDefeatedBosses =
                    NPC.downedSlimeKing.ToInt() + CommonCalamityVariables.DesertScourgeDefeated.ToInt() + NPC.downedBoss1.ToInt() +
                    CommonCalamityVariables.CrabulonDefeated.ToInt() + NPC.downedBoss2.ToInt() + eitherHiveBossDefeated.ToInt() +
                    NPC.downedQueenBee.ToInt() + NPC.downedDeerclops.ToInt() + NPC.downedBoss3.ToInt() + CommonCalamityVariables.SlimeGodDefeated.ToInt() +
                    Main.hardMode.ToInt() + NPC.downedQueenSlime.ToInt() +
                    NPC.downedMechBoss1.ToInt() + NPC.downedMechBoss2.ToInt() + NPC.downedMechBoss3.ToInt() +
                    CommonCalamityVariables.CryogenDefeated.ToInt() + CommonCalamityVariables.BrimstoneElementalDefeated.ToInt() + CommonCalamityVariables.AquaticScourgeDefeated.ToInt() +
                    CommonCalamityVariables.CalamitasCloneDefeated.ToInt() + NPC.downedPlantBoss.ToInt() + CommonCalamityVariables.LeviathanDefeated.ToInt() + CommonCalamityVariables.AstrumAureusDefeated.ToInt() +
                    NPC.downedGolemBoss.ToInt() + CommonCalamityVariables.PeanutButterGoliathDefeated.ToInt() + CommonCalamityVariables.RavagerDefeated.ToInt() + NPC.downedFishron.ToInt() +
                    NPC.downedEmpressOfLight.ToInt() + NPC.downedAncientCultist.ToInt() + CommonCalamityVariables.AstrumDeusDefeated.ToInt() + NPC.downedMoonlord.ToInt() +
                    CommonCalamityVariables.DragonfollyDefeated.ToInt() + CommonCalamityVariables.ProfanedGuardiansDefeated.ToInt() + CommonCalamityVariables.ProvidenceDefeated.ToInt() +
                    CommonCalamityVariables.CeaselessVoidDefeated.ToInt() + CommonCalamityVariables.StormWeaverDefeated.ToInt() + CommonCalamityVariables.SignusDefeated.ToInt() +
                    CommonCalamityVariables.PolterghastDefeated.ToInt() + CommonCalamityVariables.OldDukeDefeated.ToInt() + CommonCalamityVariables.DevourerOfGodsDefeated.ToInt() +
                    CommonCalamityVariables.YharonDefeated.ToInt() + CommonCalamityVariables.DraedonDefeated.ToInt() + CommonCalamityVariables.CalamitasDefeated.ToInt() +
                    BossDownedSaveSystem.HasDefeated<AvatarOfEmptiness>().ToInt() + BossDownedSaveSystem.HasDefeated<NamelessDeityBoss>().ToInt();

                if (totalDefeatedBosses <= LikelyCheatingPlayerBossKillCount)
                    State = CreditsState.RecordingSoftwareRant_WhyDidYouSkipTheBosses;
                else if (Main.ActivePlayerFileData.GetPlayTime() <= LikelyCheatingPlayerSaveTime)
                    State = CreditsState.RecordingSoftwareRant_HowDidYouDoThisSoQuicklyLmao;
            }
            else
                ReturnPlayerToMainMenu();
        }
    }

    /// <summary>
    /// Checks and returns whether the user is recording, based on whether any of the active processes match the <see cref="RecordingSoftwareSearchNames"/> list.<br></br>
    /// If anything goes wrong, such as the user having insufficient permissions to call <see cref="Process.GetProcesses()"/>, this returns false by default.
    /// </summary>
    public static bool IsUserRecording()
    {
        try
        {
            Process[] processes = Process.GetProcesses();
            foreach (Process p in processes)
            {
                if (!string.IsNullOrEmpty(p.MainWindowTitle) && RecordingSoftwareSearchNames.Any(n => p.MainWindowTitle.Contains(n, StringComparison.InvariantCultureIgnoreCase)))
                    return true;
            }
        }
        catch { }

        return false;
    }

    /// <summary>
    /// Executes the cutscene state that makes Nameless speak directly to the player, asking them to not reveal the cutscene.
    /// </summary>
    public void RecordingSoftwareRant()
    {
        int relativeTime = Timer - (CameraPanTime + InitialSitWaitTime + AppleDislodgeTime + AppleTelekinesisTime + AppleBiteDelayTimePlayer + AppleBiteTransitionDelay + AppleBiteTransitionDelay + SecondSitWaitTime + HeadpatTime + NamelessAppearanceTime + NamelessFlyAwayTime + ThirdSitWaitTime + ShyAvatarAnimationTime + MarsBowAnimationDelay + MarsBowAnimationTime + ScreenFadesToBlackTime);
        TotalScreenOverlaySystem.OverlayColor = Color.Black;
        TotalScreenOverlaySystem.OverlayInterpolant = 1.4f;

        RantTimer = relativeTime;

        if (RantTimer <= 5)
            Main.musicFade[Main.curMusic] = 0f;

        switch (State)
        {
            case CreditsState.RecordingSoftwareRant_Regular:
                MusicIDOverride = MusicLoader.GetMusicSlot(Mod, "Assets/Sounds/Music/CreditsRecordingRant_Regular");
                break;
            case CreditsState.RecordingSoftwareRant_WhyDidYouSkipTheBosses:
                MusicIDOverride = MusicLoader.GetMusicSlot(Mod, "Assets/Sounds/Music/CreditsRecordingRant_WhyDidYouSkipTheBosses");
                break;
            case CreditsState.RecordingSoftwareRant_HowDidYouDoThisSoQuicklyLmao:
                MusicIDOverride = MusicLoader.GetMusicSlot(Mod, "Assets/Sounds/Music/CreditsRecordingRant_HowDidYouDoThisSoQuicklyLmao");
                break;
        }
        if (NamelessDeityBoss.Myself is not null)
        {
            if (NamelessDeityBoss.Myself.As<NamelessDeityBoss>().CurrentState != NamelessDeityBoss.NamelessAIType.EndCreditsScene)
            {
                NamelessDeityBoss.Myself.As<NamelessDeityBoss>().StateMachine.StateStack.Clear();
                NamelessDeityBoss.Myself.As<NamelessDeityBoss>().StateMachine.StateStack.Push(NamelessDeityBoss.Myself.As<NamelessDeityBoss>().StateMachine.StateRegistry[NamelessDeityBoss.NamelessAIType.EndCreditsScene]);
            }
            NamelessDeityBoss.Myself.As<NamelessDeityBoss>().ZPosition = 0.6f;
        }

        // Bring the player to the main menu if Nameless is gone.
        // The various subtitle systems are responsible for making him leave when they're completed.
        if (RantTimer >= SecondsToFrames(10f) && NamelessDeityBoss.Myself is null)
        {
            Main.musicFade[MusicIDOverride ?? Main.curMusic] = 0f;
            ReturnPlayerToMainMenu();
        }
    }

    /// <summary>
    /// Sends the player to the main menu.
    /// </summary>
    public void ReturnPlayerToMainMenu()
    {
        Timer = 0;
        State = CreditsState.MoveSolynAndPlayerToTree;
        EndAbruptly = true;

        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            Player.SavePlayer(Main.ActivePlayerFileData);
            WorldFile.SaveWorld();
        }

        // Get out of the subworld.
        typeof(SubworldSystem).GetField("current", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, null);
        typeof(SubworldSystem).GetField("cache", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, null);

        // Reset residual music muffling.
        MusicVolumeManipulationSystem.MuffleFactor = 0f;

        // Bring the player to the main menu.
        Main.menuMode = 0;
        Main.gameMenu = true;
    }

    /// <summary>
    /// Renders Nameless directly, for the purposes of appearing during the recording rant.
    /// </summary>
    private void RenderNamelessDuringRant()
    {
        if (State != CreditsState.RecordingSoftwareRant_Regular && State != CreditsState.RecordingSoftwareRant_WhyDidYouSkipTheBosses && State != CreditsState.RecordingSoftwareRant_HowDidYouDoThisSoQuicklyLmao)
            return;

        if (NamelessDeityBoss.Myself is null)
            return;

        NamelessDeityBoss.Myself.Opacity = InverseLerp(0f, 60f, RantTimer).Squared();
        NamelessDeityBoss.Myself.Center = Main.screenPosition + ViewportSize * 0.5f - Vector2.UnitY * 80f;
        NamelessDeityBoss.Myself.As<NamelessDeityBoss>().PreDraw(Main.spriteBatch, Main.screenPosition, Color.White);
        NamelessDeityBoss.Myself.As<NamelessDeityBoss>().UpdateWings(RantTimer / 60f);
        NamelessDeityBoss.Myself.As<NamelessDeityBoss>().DefaultUniversalHandMotion();

        SoundMufflingSystem.MuffleFactor = 0f;
    }

    public override void PostDraw(SpriteBatch spriteBatch)
    {
        if (Main.gameMenu)
            return;

        Main.spriteBatch.Begin();

        int creditsTimer = Timer - (CameraPanTime + InitialSitWaitTime + AppleDislodgeTime + AppleTelekinesisTime + AppleBiteDelayTimePlayer + AppleBiteTransitionDelay + AppleBiteTransitionDelay);
        if (creditsTimer < 0)
            creditsTimer = 0;

        float creditsTop = Main.screenHeight + 100f - creditsTimer * 1.035f;
        float currentY = creditsTop;
        float spacingBetweenSets = 184f;

        static void drawLine(ref float currentY, Color textColor, string key, bool title = false)
        {
            float scale = title ? 1.3f : 0.75f;
            DynamicSpriteFont font = FontAssets.DeathText.Value;
            string lineText = Language.GetTextValue($"Mods.NoxusBoss.Credits.{key}");
            Vector2 textPosition = new Vector2(Main.screenWidth * 0.7f, currentY);
            Vector2 textSize = font.MeasureString(lineText);
            ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch, font, lineText, textPosition, textColor, 0f, textSize * 0.5f, Vector2.One * scale, -1f, 1.72f);

            currentY += title ? 126f : 72f;
        }

        // Programmers.
        drawLine(ref currentY, ProgrammerTitleColor, "ProgrammerTitle", true);
        drawLine(ref currentY, ProgrammerNameColor, "DylanDoe");
        drawLine(ref currentY, ProgrammerNameColor, "TheIndividualMakingThisLocalizationFile");
        drawLine(ref currentY, ProgrammerNameColor, "Tomat");
        currentY += spacingBetweenSets;

        // Artists.
        drawLine(ref currentY, ArtistTitleColor, "ArtworkTitle", true);
        drawLine(ref currentY, ArtistNameColor, "Blockaroz");
        drawLine(ref currentY, ArtistNameColor, "RedstoneBro");
        drawLine(ref currentY, ArtistNameColor, "IbanPlay");
        drawLine(ref currentY, ArtistNameColor, "Iris");
        drawLine(ref currentY, ArtistNameColor, "Moonburn");
        drawLine(ref currentY, ArtistNameColor, "Vaikyia");
        currentY += spacingBetweenSets;

        // Musicians.
        drawLine(ref currentY, MusicianTitleColor, "MusicTitle", true);
        drawLine(ref currentY, MusicianNameColor, "CDMusic");
        drawLine(ref currentY, MusicianNameColor, "Ennway");
        drawLine(ref currentY, MusicianNameColor, "HeartPlusUp");
        drawLine(ref currentY, MusicianNameColor, "Moonburn");
        currentY += spacingBetweenSets;

        // Sound designers.
        drawLine(ref currentY, SoundDesignerTitleColor, "SoundDesignTitle", true);
        drawLine(ref currentY, SoundDesignerNameColor, "TheIndividualMakingThisLocalizationFile");
        drawLine(ref currentY, SoundDesignerNameColor, "Moonburn");
        currentY += spacingBetweenSets;

        // Testers.
        drawLine(ref currentY, QualityAssuranceTitleColor, "QualityAssuranceTitle", true);
        drawLine(ref currentY, QualityAssuranceNameColor, "Ammyan");
        drawLine(ref currentY, QualityAssuranceNameColor, "Angel");
        drawLine(ref currentY, QualityAssuranceNameColor, "Beeleav");
        drawLine(ref currentY, QualityAssuranceNameColor, "Blast");
        drawLine(ref currentY, QualityAssuranceNameColor, "Bronze");
        drawLine(ref currentY, QualityAssuranceNameColor, "Citrus");
        drawLine(ref currentY, QualityAssuranceNameColor, "TheIndividualMakingThisLocalizationFile");
        drawLine(ref currentY, QualityAssuranceNameColor, "Fluffy");
        drawLine(ref currentY, QualityAssuranceNameColor, "Habble");
        drawLine(ref currentY, QualityAssuranceNameColor, "Healthy");
        drawLine(ref currentY, QualityAssuranceNameColor, "Ian");
        drawLine(ref currentY, QualityAssuranceNameColor, "Jareto15");
        drawLine(ref currentY, QualityAssuranceNameColor, "LGL");
        drawLine(ref currentY, QualityAssuranceNameColor, "Lynel");
        drawLine(ref currentY, QualityAssuranceNameColor, "Moonburn");
        drawLine(ref currentY, QualityAssuranceNameColor, "Myra");
        drawLine(ref currentY, QualityAssuranceNameColor, "Pengolin");
        drawLine(ref currentY, QualityAssuranceNameColor, "PurpleMattik");
        drawLine(ref currentY, QualityAssuranceNameColor, "Seasalt");
        drawLine(ref currentY, QualityAssuranceNameColor, "Shade");
        drawLine(ref currentY, QualityAssuranceNameColor, "Shuki");
        drawLine(ref currentY, QualityAssuranceNameColor, "Smh");
        drawLine(ref currentY, QualityAssuranceNameColor, "Spooktacular");
        drawLine(ref currentY, QualityAssuranceNameColor, "SomeSnakeOilSalesman");
        drawLine(ref currentY, QualityAssuranceNameColor, "Teiull");
        drawLine(ref currentY, QualityAssuranceNameColor, "YuH");
        currentY += spacingBetweenSets;

        // The player.
        Color playerColor = Main.hslToRgb(Main.GlobalTimeWrappedHourly * 0.3f % 1f, 0.95f, 0.55f);
        drawLine(ref currentY, playerColor, "PlayerThanks");

        Main.spriteBatch.End();
    }

    public override void ModifyScreenPosition()
    {
        Vector2 cameraPanPosition = new Vector2(Main.maxTilesX * 8f, EternalGardenWorldGen.SurfaceTilePoint * 16f - 300f) - Main.ScreenSize.ToVector2() * 0.5f;
        Main.screenPosition = Vector2.Lerp(Main.screenPosition, cameraPanPosition, CameraPanInterpolant);
    }

    public override void ModifyTransformMatrix(ref SpriteViewMatrix transform)
    {
        transform.Zoom *= Lerp(1f, 1.3f, CameraPanInterpolant);
    }
}
