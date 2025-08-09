using Luminance.Common.DataStructures;
using Luminance.Common.Easings;
using Luminance.Common.StateMachines;
using Luminance.Core.Graphics;

using Microsoft.Xna.Framework;

using NoxusBoss.Assets;
using NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles;
using NoxusBoss.Content.NPCs.Bosses.Avatar.Projectiles.SolynProjectiles;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers.ParadiseReclaimed;
using NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers.SolynDialogue;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity;
using NoxusBoss.Content.NPCs.Bosses.NamelessDeity.SpecificEffectManagers;
using NoxusBoss.Content.Particles.Metaballs;
using NoxusBoss.Core.CrossCompatibility.Inbound.BaseCalamity;
using NoxusBoss.Core.Graphics.SpecificEffectManagers;
using NoxusBoss.Core.SoundSystems;
using NoxusBoss.Core.SoundSystems.Music;
using NoxusBoss.Core.World.TileDisabling;
using NoxusBoss.Core.World.WorldSaving;

using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm;

public partial class AvatarOfEmptiness
{
    /// <summary>
    /// Whether the paradise reclaimed section of the battle is ongoing or not.
    /// </summary>
    public bool ParadiseReclaimedIsOngoing
    {
        get;
        set;
    }

    /// <summary>
    /// The X position of the static wall during paradise reclaimed.
    /// </summary>
    public float ParadiseReclaimed_RenderStaticWallXOrigin
    {
        get;
        set;
    }

    /// <summary>
    /// The Y position of the static wall during paradise reclaimed.
    /// </summary>
    public float ParadiseReclaimed_RenderStaticWallYPosition
    {
        get;
        set;
    }

    /// <summary>
    /// How much the static during paradise reclaimed should part.
    /// </summary>
    public float ParadiseReclaimed_StaticPartInterpolant
    {
        get;
        set;
    }

    /// <summary>
    /// The amount by which layers of static should be further separated during paradise reclaimed.
    /// </summary>
    public int ParadiseReclaimed_LayerHeightBoost
    {
        get;
        set;
    }

    /// <summary>
    /// The looped sound Solyn plays when being dissolved by static.
    /// </summary>
    public LoopedSoundInstance? SolynStaticLoop
    {
        get;
        private set;
    }

    /// <summary>
    /// How long Solyn spends speaking during paradise reclaimed.
    /// </summary>
    public static int ParadiseReclaimed_SolynDialogueTime => GetAIInt("ParadiseReclaimed_SolynDialogueTime");

    /// <summary>
    /// How long Solyn waits before speaking during paradise reclaimed.
    /// </summary>
    public static int ParadiseReclaimed_SolynDialogueDelay => GetAIInt("ParadiseReclaimed_SolynDialogueDelay");

    /// <summary>
    /// How long Solyn waits before saying her second line during paradise reclaimed.
    /// </summary>
    public static int ParadiseReclaimed_SolynDialogueDelay2 => GetAIInt("ParadiseReclaimed_SolynDialogueDelay2");

    /// <summary>
    /// How long Solyn waits before saying her third line during paradise reclaimed.
    /// </summary>
    public static int ParadiseReclaimed_SolynDialogueDelay3 => GetAIInt("ParadiseReclaimed_SolynDialogueDelay3");

    /// <summary>
    /// How long the static spends chasing Solyn and the player during paradise reclaimed.
    /// </summary>
    public static int ParadiseReclaimed_StaticChaseTime => GetAIInt("ParadiseReclaimed_StaticChaseTime");

    /// <summary>
    /// How long the player spends submerged in static after Solyn saves them during paradise reclaimed.
    /// </summary>
    public static int ParadiseReclaimed_StaticSubmergeTime => GetAIInt("ParadiseReclaimed_StaticSubmergeTime");

    /// <summary>
    /// How long Nameless moves around during paradise reclaimed in wait before sending everyone back to the overworld.
    /// </summary>
    public static int ParadiseReclaimed_NamelessAppearTime => GetAIInt("ParadiseReclaimed_NamelessAppearTime");

    /// <summary>
    /// How long the player and Solyn spend traveling through Nameless' vortex before going back home.
    /// </summary>
    public static int ParadiseReclaimed_ReturnHomeTime => GetAIInt("ParadiseReclaimed_ReturnHomeTime");

    /// <summary>
    /// How far down into the sea of static players have to be before being killed.
    /// </summary>
    public static float ParadiseReclaimed_SubmergeKillDistance => GetAIFloat("ParadiseReclaimed_SubmergeKillDistance");

    [AutomatedMethodInvoke]
    public void LoadState_DeathAnimation()
    {
        StateMachine.RegisterTransition(AvatarAIType.ParadiseReclaimed_SolynDialogue, AvatarAIType.ParadiseReclaimed_StaticChase, false, () =>
        {
            return AITimer >= ParadiseReclaimed_SolynDialogueTime;
        });
        StateMachine.RegisterTransition(AvatarAIType.ParadiseReclaimed_StaticChase, AvatarAIType.ParadiseReclaimed_SolynIsClaimed, false, () =>
        {
            return AITimer >= ParadiseReclaimed_StaticChaseTime || DoBehavior_CheckIfPlayersShouldBeSaved();
        });
        StateMachine.RegisterTransition(AvatarAIType.ParadiseReclaimed_SolynIsClaimed, AvatarAIType.ParadiseReclaimed_NamelessDispelsStatic, false, () =>
        {
            return AITimer >= ParadiseReclaimed_StaticSubmergeTime;
        });
        StateMachine.RegisterTransition(AvatarAIType.ParadiseReclaimed_NamelessDispelsStatic, AvatarAIType.ParadiseReclaimed_NamelessReturnsEveryoneToOverworld, false, () =>
        {
            return AITimer >= ParadiseReclaimed_NamelessAppearTime;
        });
        StateMachine.RegisterTransition(AvatarAIType.ParadiseReclaimed_NamelessReturnsEveryoneToOverworld, AvatarAIType.ParadiseReclaimed_FakeoutPhase, false, () =>
        {
            return AITimer >= ParadiseReclaimed_ReturnHomeTime;
        });

        StateMachine.AddTransitionStateHijack(originalState =>
        {
            if (WaitingForDeathAnimation && originalState != AvatarAIType.AbsoluteZeroOutburst && originalState != AvatarAIType.AbsoluteZeroOutburstPunishment && !ParadiseReclaimedIsOngoing)
            {
                IProjOwnedByBoss<AvatarOfEmptiness>.KillAll();

                StateMachine.StateStack.Clear();
                StateMachine.StateStack.Push(StateMachine.StateRegistry[AvatarAIType.ParadiseReclaimed_SolynDialogue]);
                StateMachine.StateStack.Push(StateMachine.StateRegistry[AvatarAIType.SendPlayerToMyUniverse]);
                WaitingForDeathAnimation = false;

                return null;
            }

            return originalState;
        });

        StatesToNotStartTeleportDuring.Add(AvatarAIType.ParadiseReclaimed_SolynDialogue);
        StatesToNotStartTeleportDuring.Add(AvatarAIType.ParadiseReclaimed_StaticChase);
        StatesToNotStartTeleportDuring.Add(AvatarAIType.ParadiseReclaimed_SolynIsClaimed);
        StatesToNotStartTeleportDuring.Add(AvatarAIType.ParadiseReclaimed_NamelessDispelsStatic);
        StatesToNotStartTeleportDuring.Add(AvatarAIType.ParadiseReclaimed_NamelessReturnsEveryoneToOverworld);
        StatesToNotStartTeleportDuring.Add(AvatarAIType.ParadiseReclaimed_FakeoutPhase);

        StateMachine.RegisterStateBehavior(AvatarAIType.ParadiseReclaimed_SolynDialogue, DoBehavior_ParadiseReclaimed_SolynDialogue);
        StateMachine.RegisterStateBehavior(AvatarAIType.ParadiseReclaimed_StaticChase, DoBehavior_ParadiseReclaimed_StaticChase);
        StateMachine.RegisterStateBehavior(AvatarAIType.ParadiseReclaimed_SolynIsClaimed, DoBehavior_ParadiseReclaimed_SolynIsClaimed);
        StateMachine.RegisterStateBehavior(AvatarAIType.ParadiseReclaimed_NamelessDispelsStatic, DoBehavior_ParadiseReclaimed_NamelessDispelsStatic);
        StateMachine.RegisterStateBehavior(AvatarAIType.ParadiseReclaimed_NamelessReturnsEveryoneToOverworld, DoBehavior_ParadiseReclaimed_NamelessReturnsEveryoneToOverworld);
        StateMachine.RegisterStateBehavior(AvatarAIType.ParadiseReclaimed_FakeoutPhase, DoBehavior_ParadiseReclaimed_FakeoutPhase);

        AttackDimensionRelationship[AvatarAIType.ParadiseReclaimed_SolynDialogue] = AvatarDimensionVariants.DarkDimension;
        AttackDimensionRelationship[AvatarAIType.ParadiseReclaimed_StaticChase] = AvatarDimensionVariants.DarkDimension;
        AttackDimensionRelationship[AvatarAIType.ParadiseReclaimed_SolynIsClaimed] = AvatarDimensionVariants.DarkDimension;
        AttackDimensionRelationship[AvatarAIType.ParadiseReclaimed_NamelessDispelsStatic] = AvatarDimensionVariants.DarkDimension;
    }

    public void DoBehavior_ParadiseReclaimed_SolynDialogue()
    {
        // Be absolutely certain that no residual projectiles make it into Paradise Reclaimed for some reason.
        if (AITimer <= 5)
            IProjOwnedByBoss<AvatarOfEmptiness>.KillAll();

        WaitingForDeathAnimation = false;
        if (AITimer == 1 && NPC.HasPlayerTarget)
        {
            TotalScreenOverlaySystem.OverlayColor = Color.Black;
            TotalScreenOverlaySystem.OverlayInterpolant = 1.35f;
            NPC.netUpdate = true;

            Player targetPlayer = Main.player[NPC.TranslatedTargetIndex];
            targetPlayer.position.Y = Main.maxTilesY * 16f * 0.9f;

            SolynAction = solyn => solyn.NPC.Center = solyn.Player.Center;
            return;
        }

        float rumbleBoost = InverseLerp(ParadiseReclaimed_SolynDialogueDelay3 - 45, ParadiseReclaimed_SolynDialogueDelay3, AITimer) * 5.4f;
        ScreenShakeSystem.SetUniversalRumble((AITimer / (float)ParadiseReclaimed_SolynDialogueTime).Cubed() * 4f + rumbleBoost, TwoPi, null, 0.2f);

        SolynAction = solyn =>
        {
            if (AITimer == ParadiseReclaimed_SolynDialogueDelay)
                SolynWorldDialogueManager.CreateNew("Mods.NoxusBoss.Dialog.SolynParadiseReclaimed1", -solyn.NPC.spriteDirection, solyn.NPC.Top, ParadiseReclaimed_SolynDialogueDelay2 - ParadiseReclaimed_SolynDialogueDelay, true);
            if (AITimer == ParadiseReclaimed_SolynDialogueDelay2)
                SolynWorldDialogueManager.CreateNew("Mods.NoxusBoss.Dialog.SolynParadiseReclaimed2", -solyn.NPC.spriteDirection, solyn.NPC.Top, ParadiseReclaimed_SolynDialogueDelay3 - ParadiseReclaimed_SolynDialogueDelay2, true);
            if (AITimer == ParadiseReclaimed_SolynDialogueDelay3)
                SolynWorldDialogueManager.CreateNew("Mods.NoxusBoss.Dialog.SolynParadiseReclaimed3", -solyn.NPC.spriteDirection, solyn.NPC.Top, 120, true);

            solyn.UseStarFlyEffects();
            if (AITimer <= ParadiseReclaimed_SolynDialogueDelay3)
            {
                solyn.NPC.SmoothFlyNearWithSlowdownRadius(solyn.Player.Center, 0.11f, 0.85f, 90f);
                solyn.NPC.spriteDirection = (int)solyn.NPC.HorizontalDirectionTo(solyn.Player.Center);
                solyn.NPC.rotation = solyn.NPC.rotation.AngleLerp(0f, 0.3f);
            }
            else
            {
                solyn.NPC.velocity *= 0.8f;
                solyn.NPC.dontTakeDamage = true;
            }
        };

        foreach (Player player in Main.ActivePlayers)
            player.velocity.Y = 0f;

        DoBehavior_ParadiseReclaimed_GeneralUpdates();
    }

    public void DoBehavior_ParadiseReclaimed_StaticChase()
    {
        float wallSpeed = InverseLerp(90f, 0f, AITimer) * 10f + InverseLerp(0f, ParadiseReclaimed_StaticChaseTime, AITimer).Cubed() * 19.5f + 10.5f;
        if (AITimer == 1)
        {
            ParadiseReclaimed_RenderStaticWallYPosition = Target.Center.Y + 1676f;
            NPC.netUpdate = true;
        }

        float riseInterpolant = InverseLerp(270f, 500f, ParadiseReclaimed_RenderStaticWallYPosition - Target.Center.Y) * 0.0021f;
        ParadiseReclaimed_RenderStaticWallYPosition -= wallSpeed;

        float oldYPosition = ParadiseReclaimed_RenderStaticWallYPosition;
        ParadiseReclaimed_RenderStaticWallYPosition = Lerp(ParadiseReclaimed_RenderStaticWallYPosition, Target.Center.Y, riseInterpolant);

        ParadiseReclaimed_LayerHeightBoost = (int)(Pow(InverseLerp(19f, 10f, wallSpeed), 0.7f) * 220f);

        // Rain objects down into the static.
        if (Main.netMode != NetmodeID.MultiplayerClient && AITimer % 5 == 0)
        {
            Vector2 objectSpawnPosition = Target.Center + new Vector2(Main.rand.NextFloatDirection() * 1600f, -1950f);
            Vector2 objectVelocity = Vector2.UnitY.RotatedByRandom(0.5f) * Main.rand.NextFloat(3f, 9.5f);
            NewProjectileBetter(NPC.GetSource_FromAI(), objectSpawnPosition, objectVelocity, ModContent.ProjectileType<FallingObject>(), 0, 0f, -1, Main.rand.Next(ParadiseStaticLayerHandlers.ParadiseStaticLayerCount - 1));
        }

        ScreenShakeSystem.SetUniversalRumble(6f, TwoPi, null, 0.2f);

        SolynAction = solyn =>
        {
            float solynUpwardFlySpeed = wallSpeed + 0.25f;
            if (solynUpwardFlySpeed > 24f)
                solynUpwardFlySpeed = 24f;

            solyn.NPC.velocity.X = Lerp(solyn.NPC.velocity.X, NPC.SafeDirectionTo(solyn.Player.Center + Vector2.UnitX * solyn.NPC.OnRightSideOf(solyn.Player.Center).ToDirectionInt() * 50f).X * 7f, 0.037f);
            solyn.NPC.velocity.Y = Lerp(solyn.NPC.velocity.Y, -solynUpwardFlySpeed, 0.073f);
            solyn.NPC.position.Y += ParadiseReclaimed_RenderStaticWallYPosition - oldYPosition;
            solyn.NPC.spriteDirection = (int)solyn.NPC.HorizontalDirectionTo(solyn.Player.Center);
            solyn.NPC.rotation = solyn.NPC.rotation.AngleLerp(0f, 0.3f);
            solyn.UseStarFlyEffects();
        };

        DoBehavior_ParadiseReclaimed_GeneralUpdates(wallSpeed);
    }

    public void DoBehavior_ParadiseReclaimed_SolynIsClaimed()
    {
        float wallSpeed = 30f;
        ParadiseReclaimed_RenderStaticWallYPosition -= wallSpeed;

        ScreenShakeSystem.SetUniversalRumble(7f, TwoPi, null, 0.2f);

        if (AITimer <= 1)
            IProjOwnedByBoss<AvatarOfEmptiness>.KillAll();

        SolynAction = solyn =>
        {
            if (AITimer == 2)
            {
                if (NPC.Center.Y <= ParadiseReclaimed_RenderStaticWallYPosition - 50f)
                    SolynWorldDialogueManager.CreateNew("Mods.NoxusBoss.Dialog.SolynParadiseReclaimedSaveText", -solyn.NPC.spriteDirection, solyn.NPC.Top, 150, true);
                
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    foreach (Player player in Main.ActivePlayers)
                        NewProjectileBetter(solyn.NPC.GetSource_FromAI(), player.Center, Vector2.Zero, ModContent.ProjectileType<SolynProtectiveForcefieldForPlayer>(), 0, 0f, -1, 0, player.whoAmI);
                }
            }

            solyn.NPC.velocity.X *= 0.95f;
            solyn.NPC.velocity.Y = Lerp(solyn.NPC.velocity.Y, wallSpeed * -0.8f, 0.1f);
            solyn.NPC.spriteDirection = (int)solyn.NPC.HorizontalDirectionTo(solyn.Player.Center);
            solyn.NPC.rotation = solyn.NPC.rotation.AngleLerp(0f, 0.3f);
            solyn.UseStarFlyEffects();
            solyn.Frame = 21f;
        };
        DoBehavior_ParadiseReclaimed_GeneralUpdates(wallSpeed);
    }

    public void DoBehavior_ParadiseReclaimed_NamelessDispelsStatic()
    {
        float wallSpeed = 30f;
        ParadiseReclaimed_RenderStaticWallYPosition -= wallSpeed;

        if (AITimer == 1)
        {
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.NamelessDispelsStatic);
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                Vector2 namelessSpawnPosition = Target.Center - Vector2.UnitY * 1300f;
                int nameless = NPC.NewNPC(new EntitySource_WorldEvent(), (int)namelessSpawnPosition.X, (int)namelessSpawnPosition.Y, ModContent.NPCType<NamelessDeityBoss>(), 1);
                Main.npc[nameless].As<NamelessDeityBoss>().StateMachine.StateStack.Push(Main.npc[nameless].As<NamelessDeityBoss>().StateMachine.StateRegistry[NamelessDeityBoss.NamelessAIType.SavePlayerFromAvatar]);
            }
        }

        if (AITimer == 120)
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.NamelessOpensVortex);

        ParadiseReclaimed_StaticPartInterpolant = EasingCurves.Quartic.Evaluate(EasingType.InOut, InverseLerp(0f, 90f, AITimer));

        MusicVolumeManipulationSystem.MuffleFactor = (1f - ParadiseReclaimed_StaticPartInterpolant).Squared();

        SolynAction = solyn =>
        {
            solyn.Frame = 21f;
            solyn.NPC.SmoothFlyNearWithSlowdownRadius(solyn.Player.Center, 0.02f, 0.96f, 60f);
            solyn.NPC.spriteDirection = (int)solyn.NPC.HorizontalDirectionTo(solyn.Player.Center);
            solyn.NPC.rotation *= 0.9f;
            solyn.NPC.Opacity = 1f;
            solyn.StaticOverlayInterpolant = 1f;
        };

        DoBehavior_ParadiseReclaimed_GeneralUpdates();
    }

    public void DoBehavior_ParadiseReclaimed_NamelessReturnsEveryoneToOverworld()
    {
        MusicVolumeManipulationSystem.MuffleFactor = (1f - ParadiseReclaimed_StaticPartInterpolant).Squared();

        Main.time = 7200D;
        Main.dayTime = true;

        SolynAction = solyn =>
        {
            solyn.Frame = 21f;
            solyn.NPC.SmoothFlyNearWithSlowdownRadius(solyn.Player.Center - Vector2.UnitY * 70f, 0.03f, 0.96f, 100f);
            solyn.NPC.spriteDirection = (int)solyn.NPC.HorizontalDirectionTo(solyn.Player.Center);
            solyn.NPC.rotation *= 0.9f;
            solyn.NPC.Opacity = 1f;
            solyn.StaticOverlayInterpolant = 1f;
        };

        TravelThroughVortex_RevealDimensionInterpolant = InverseLerp(-SecondsToFrames(4f), 0f, AITimer - ParadiseReclaimed_ReturnHomeTime);
        TravelThroughVortex_HoleGrowInterpolant = InverseLerp(-SecondsToFrames(4.2f), 0f, AITimer - ParadiseReclaimed_ReturnHomeTime);
        ParadiseReclaimed_StaticPartInterpolant = 1f;

        if (NamelessDeityVortexLoop is null || NamelessDeityVortexLoop.HasBeenStopped || !NamelessDeityVortexLoop.IsBeingPlayed)
            NamelessDeityVortexLoop = new(GennedAssets.Sounds.Avatar.NamelessVortexLoop, () => !NPC.active);
        if (AITimer == ParadiseReclaimed_ReturnHomeTime - SecondsToFrames(3.6f))
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.NamelessVortexLoopEndBuildup);

        if (AITimer <= 120)
            AvatarOfEmptinessSky.Dimension = AvatarDimensionVariants.DarkDimension;
        else
        {
            TileDisablingSystem.TilesAreUninteractable = true;

            TotalScreenOverlaySystem.OverlayColor = Color.White;
            TotalScreenOverlaySystem.OverlayInterpolant = InverseLerp(ParadiseReclaimed_ReturnHomeTime - 60f, ParadiseReclaimed_ReturnHomeTime - 1f, AITimer) * 1.4f;
        }

        Vector2 previousPlayerPosition = Main.LocalPlayer.Center;
        Main.LocalPlayer.Center = TargetPositionAtStart;

        if (AITimer == 1)
        {
            TotalScreenOverlaySystem.OverlayColor = Color.White;
            TotalScreenOverlaySystem.OverlayInterpolant = 1f;

            if (NamelessDeityBoss.Myself is not null)
            {
                NamelessDeityBoss.Myself.Center += Main.LocalPlayer.Center - previousPlayerPosition;
                NamelessDeityBoss.Myself.netUpdate = true;
            }

            SolynAction = solyn =>
            {
                solyn.NPC.Center += solyn.Player.Center - previousPlayerPosition;
                solyn.NPC.netUpdate = true;
            };
        }

        DoBehavior_ParadiseReclaimed_GeneralUpdates();
    }

    public void DoBehavior_ParadiseReclaimed_FakeoutPhase()
    {
        DoBehavior_ParadiseReclaimed_GeneralUpdates();
        ParadiseReclaimedIsOngoing = false;
        BattleIsDone = true;
        Music = 0;

        AvatarOfEmptinessSky.intensity = 0f;
        SoundMufflingSystem.MuffleFactor = 1f;

        if (NamelessDeityVortexLoop is not null)
        {
            NamelessDeityVortexLoop?.Stop();
            NamelessDeityVortexLoop = null;
        }
        if (AITimer == 1)
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.NamelessVortexLoopEnd);

        if (AITimer == 75)
            SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.RiftShoot with { MaxInstances = 8, Volume = 0.6f });

        SolynAction = solyn =>
        {
            solyn.NPC.rotation *= 0.9f;
            solyn.NPC.Opacity = Saturate(solyn.NPC.Opacity + 0.03f);
            solyn.NPC.noTileCollide = false;
            solyn.StaticOverlayInterpolant = 1f;
            solyn.StaticDissolveInterpolant = InverseLerp(360f, 480f, AITimer);

            SolynStaticLoop ??= LoopedSoundManager.CreateNew(GennedAssets.Sounds.Avatar.SolynStaticLoop, () => !solyn.NPC.active);
            SolynStaticLoop.Update(Main.LocalPlayer.Center, sound =>
            {
                sound.Volume = InverseLerp(480f, 279f, AITimer).Squared();
                if (AITimer <= 75)
                    sound.Volume = 0f;
            });

            if (AITimer == 279)
                SoundEngine.PlaySound(GennedAssets.Sounds.Avatar.SolynStaticDissolve);

            if (AITimer <= 75)
            {
                solyn.NPC.Center = Target.Center - Vector2.UnitY * 242f;
                while (Collision.SolidCollision(solyn.NPC.TopLeft, solyn.NPC.width, solyn.NPC.height) && solyn.NPC.Top.Y < solyn.Player.Center.Y)
                    solyn.NPC.position.Y += 4f;

                if (AITimer == 74)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                        NewProjectileBetter(NPC.GetSource_FromAI(), solyn.NPC.Center - Vector2.UnitY * 10f, Vector2.UnitY, ModContent.ProjectileType<DarkPortal>(), 0, 0f, -1, 0.82f, 104);
                    solyn.NPC.netUpdate = true;
                }

                solyn.NPC.velocity = Vector2.Zero;
                solyn.NPC.Opacity = 0f;
            }
            else
            {
                ParadiseStaticLayer frontLayer = ParadiseStaticLayerHandlers.GetLayerByDepth(0);
                for (int i = 0; i < 3; i++)
                {
                    if (Main.rand.NextBool(InverseLerpBump(0f, 0.5f, 0.6f, 0.85f, solyn.StaticDissolveInterpolant) + 0.035f))
                    {
                        Vector2 blobDirection = -Vector2.UnitY.RotatedByRandom(0.51f);
                        Vector2 blobSpawnPosition = solyn.NPC.Center + Main.rand.NextVector2Circular(32f, 16f) + Vector2.UnitY * 10f;
                        Vector2 blobVelocity = blobDirection * Main.rand.NextFloat(0.6f, 2.7f);
                        ModContent.GetInstance<EmptinessSprayerMetaball>().CreateParticle(blobSpawnPosition, blobVelocity.RotatedByRandom(0.02f), Main.rand.NextFloat(10f, 20f));
                    }
                }

                if (Abs(solyn.NPC.velocity.Y) >= 0.4f)
                    solyn.Frame = 21f;
                else if (AITimer % 8 == 0)
                    solyn.Frame = Clamp(solyn.Frame + 1f, 21f, 23f);

                solyn.NPC.velocity.Y = Clamp(solyn.NPC.velocity.Y + 0.9f, -4f, 15.99f);
            }

            CalamityCompatibility.ResetStealthBarOpacity(Main.LocalPlayer);
            CameraPanSystem.PanTowards(solyn.NPC.Center, InverseLerp(140f, 160f, AITimer));
            CameraPanSystem.ZoomIn(InverseLerp(140f, 160f, AITimer) * 0.6f);

            if (AITimer >= 480)
                solyn.NPC.active = false;
        };

        if (AITimer >= (BossDownedSaveSystem.HasDefeated<AvatarOfEmptiness>() ? 60 : 480))
        {
            NPC.Center = Target.Center - Vector2.UnitY * 450f;
            NPC.checkDead();
            NPC.NPCLoot();
            NPC.active = false;

            if (NamelessDeityBoss.Myself is not null)
                NamelessDeityBoss.Myself.active = false;
            NamelessDeitySky.HeavenlyBackgroundIntensity = 0f;
            NamelessDeitySky.Intensity = 0f;
        }
    }

    /// <summary>
    /// Handles general-purpose updates of the paradise reclaimed section.
    /// </summary>
    public void DoBehavior_ParadiseReclaimed_GeneralUpdates(float wallSpeed = 0f)
    {
        DoBehavior_ParadiseReclaimed_StayOutOfSight();
        DoBehavior_ParadiseReclaimed_HandleGeneralSoundDesign();
        if (ParadiseReclaimed_RenderStaticWallYPosition != 0f)
        {
            if (ParadiseReclaimed_RenderStaticWallXOrigin == 0f)
                ParadiseReclaimed_RenderStaticWallXOrigin = Main.LocalPlayer.Center.X;

            DoBehavior_ParadiseReclaimed_CreateParticles(Abs(Target.Velocity.Y) + wallSpeed * 0.5f);
        }

        if (ParadiseReclaimed_RenderStaticWallYPosition < -700f)
            ParadiseReclaimed_RenderStaticWallYPosition = -700f;

        ParadiseReclaimedIsOngoing = true;
        AmbientSoundVolumeFactor = 0f;

        NamelessDeityVortexLoop?.Update(Main.LocalPlayer.Center, sound =>
        {
            if (CurrentState == AvatarAIType.ParadiseReclaimed_NamelessReturnsEveryoneToOverworld && sound.Sound is not null)
                sound.Sound.Volume = Main.soundVolume * InverseLerp(ParadiseReclaimed_ReturnHomeTime - 30f, ParadiseReclaimed_ReturnHomeTime - 120f, AITimer);
        });

        TargetClosest();
    }

    /// <summary>
    /// Checks all players and sees if they are sufficiently submerged during paradise reclaimed due to being below the static.
    /// </summary>
    public bool DoBehavior_CheckIfPlayersShouldBeSaved()
    {
        if (ParadiseReclaimed_RenderStaticWallYPosition == 0f)
            return false;

        int forcefieldID = ModContent.ProjectileType<SolynProtectiveForcefieldForPlayer>();
        foreach (Player player in Main.ActivePlayers)
        {
            if (player.Center.Y < ParadiseReclaimed_RenderStaticWallYPosition + ParadiseReclaimed_SubmergeKillDistance || player.dead)
                continue;

            if (player.ownedProjectileCounts[forcefieldID] >= 1)
                continue;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Handles the Avatar-specific AI code for the paradise reclaimed section, making him stay offscreen and invincible.
    /// </summary>
    public void DoBehavior_ParadiseReclaimed_StayOutOfSight()
    {
        NPC.dontTakeDamage = true;
        NPC.Opacity = 0f;
        NPC.Center = Target.Center + Vector2.UnitY * 5000f;
        HideBar = true;
    }

    /// <summary>
    /// Handles the general sound design aspect of the paradise reclaimed section, playing eerie yet welcome ambience and silencing all other sounds completely.
    /// </summary>
    public void DoBehavior_ParadiseReclaimed_HandleGeneralSoundDesign()
    {
        Music = MusicLoader.GetMusicSlot(Mod, "Assets/Sounds/Music/blissful dream in time denied");
        Main.musicFade[Music] = 1f;
        if (Main.musicVolume >= 0.001f && Main.musicVolume < 0.3f)
            Main.musicVolume = 0.3f;

        if (CurrentState != AvatarAIType.ParadiseReclaimed_NamelessReturnsEveryoneToOverworld)
            SoundMufflingSystem.MuffleFactor = 0f;
    }

    public void DoBehavior_ParadiseReclaimed_RenderStaticWall(int layer, Color color)
    {
        // Don't do anything if the fluid wall is not in use.
        if (ParadiseReclaimed_RenderStaticWallYPosition == 0f)
            return;

        int forcefieldID = ModContent.ProjectileType<SolynProtectiveForcefieldForPlayer>();
        float forcefieldRadius = 0f;
        Vector2 forcefieldPosition = Vector2.One * -5000f;
        if (layer <= 2 && Main.LocalPlayer.ownedProjectileCounts[forcefieldID] >= 1)
        {
            forcefieldPosition = Main.LocalPlayer.Center - Main.screenPosition;
            foreach (Projectile projectile in Main.ActiveProjectiles)
            {
                if (projectile.owner == Main.myPlayer && projectile.type == forcefieldID)
                {
                    forcefieldRadius = projectile.width * projectile.scale * 0.46f;
                    break;
                }
            }
        }

        int top = (int)ParadiseReclaimed_RenderStaticWallYPosition - (int)Main.screenPosition.Y;
        int heightPerLayer = (int)(InverseLerp(0f, 700f, top).Squared() * 80f) + ParadiseReclaimed_LayerHeightBoost;
        ManagedShader waveShader = ShaderManager.GetShader("NoxusBoss.WaveCutoutShader");
        waveShader.TrySetParameter("wavePeriod", 18.1f - layer * 4.1f);
        waveShader.TrySetParameter("waveMoveSpeed", (layer % 2 == 1).ToDirectionInt() * (layer * 0.31f + 1.4f));
        waveShader.TrySetParameter("forcefieldRadius", forcefieldRadius * Main.GameViewMatrix.Zoom.X);
        waveShader.TrySetParameter("forcefieldPosition", forcefieldPosition);
        waveShader.TrySetParameter("centerPartInterpolant", ParadiseReclaimed_StaticPartInterpolant * 0.5f);
        waveShader.Apply();

        // Draw the wall.
        Main.spriteBatch.Draw(WhitePixel, new Rectangle(-400, top - layer * heightPerLayer, Main.screenWidth + 800, Main.screenHeight * 50), color);
    }

    public void DoBehavior_ParadiseReclaimed_CreateParticles(float wallChangeFromLastFrame)
    {
        if (Main.netMode == NetmodeID.Server) 
            return;

        if (ParadiseReclaimed_StaticPartInterpolant >= 0.01f)
            return;

        int depth = Main.rand.Next(1, 4);
        if (Main.rand.NextBool(11))
            depth = 0;

        ParadiseStaticLayer layer = ParadiseStaticLayerHandlers.GetLayerByDepth(depth);
        for (float x = -600f; x < 600f; x += 120f)
        {
            for (float y = -50f; y < 240f; y += 60f)
            {
                Vector2 blobDirection = -Vector2.UnitY.RotatedByRandom(0.51f);
                Vector2 blobSpawnPosition = new Vector2(Target.Center.X + Main.rand.NextFloatDirection() * 1200f + x, ParadiseReclaimed_RenderStaticWallYPosition + Main.rand.NextFloat(300f) + y);
                blobSpawnPosition.Y -= layer.Depth * 150f;

                Vector2 blobVelocity = blobDirection * (wallChangeFromLastFrame + Main.rand.NextFloat(5f, 20f));
                layer.ParticleSystem.CreateNew(blobSpawnPosition, blobVelocity, Vector2.One * Main.rand.NextFloat(110f, 124f), Color.White);
            }
        }
    }

    public void DoBehavior_ParadiseReclaimed_RenderGodRays(int depth)
    {
        UnifiedRandom rng = new UnifiedRandom(depth * 29 + 17);
        Color color = Color.White.MultiplyRGB(Color.White * (0.8f - depth * 0.22f)) * (1f / (depth + 1f));

        int i = 0;
        for (float x = -3000f; x < 3000f; x += 800f)
        {
            i++;
            if (i % 3 != 0 && depth <= 1)
                continue;

            float width = rng.NextFloat(95f, 250f);
            float height = rng.NextFloat(800f, 1770f);
            Vector2 bottom = new Vector2(x + ParadiseReclaimed_RenderStaticWallXOrigin + depth * 200f, ParadiseReclaimed_RenderStaticWallYPosition + 300f);

            // Randomly offset rays.
            bottom.X += rng.NextFloatDirection() * 675f;

            // Apply parallax.
            bottom.X += (Main.LocalPlayer.Center.X - ParadiseReclaimed_RenderStaticWallXOrigin) * (depth * 0.3f + 0.15f);

            Vector2 top = bottom - Vector2.UnitY * height;

            ManagedShader shader = ShaderManager.GetShader("NoxusBoss.ParadiseStaticGodRayShader");
            PrimitiveSettings settings = new PrimitiveSettings(c => Lerp(width * 0.95f, width, c), c => color * (1f - c), null, Pixelate: false, Shader: shader);
            PrimitiveRenderer.RenderTrail(new Vector2[]
            {
                top,
                Vector2.Lerp(top, bottom, 0.2f),
                Vector2.Lerp(top, bottom, 0.4f),
                Vector2.Lerp(top, bottom, 0.6f),
                Vector2.Lerp(top, bottom, 0.8f),
                bottom
            }, settings, 38);
        }
    }

    public void DoBehavior_ParadiseReclaimed_DrawThreadActions()
    {
        for (int i = 0; i < 5; i++)
        {
            ParadiseStaticLayer layer = ParadiseStaticLayerHandlers.GetLayerByDepth(i);
            layer.IntoTargetRenderQueue.Enqueue(() =>
            {
                Color color = Color.Lerp(Color.White, Color.DarkGray, layer.Depth / 3f);
                DoBehavior_ParadiseReclaimed_RenderStaticWall(layer.Depth, color);
            });
            layer.IndependentRenderQueue.Enqueue(() =>
            {
                DoBehavior_ParadiseReclaimed_RenderGodRays(layer.Depth);
            });
        }
    }
}
